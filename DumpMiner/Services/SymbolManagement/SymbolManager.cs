using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Extensions.Logging;
using System.ComponentModel.Composition;
using DumpMiner.Common;
using DumpMiner.Debugger;

namespace DumpMiner.Services.SymbolManagement
{
    /// <summary>
    /// Advanced symbol management system with Microsoft Symbol Server integration
    /// </summary>
    [Export(typeof(ISymbolManager))]
    public class SymbolManager : ISymbolManager, IDisposable
    {
        private readonly ILogger<SymbolManager> _logger;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, SymbolInfo> _symbolCache;
        private readonly string _localSymbolCache;

        // Microsoft Symbol Server URLs
        private readonly string[] _symbolServers =
        {
            "https://msdl.microsoft.com/download/symbols/",
            "https://referencesource.microsoft.com/download/symbols/",
            "https://nuget.smbsrc.net/download/symbols/"
        };

        [ImportingConstructor]
        public SymbolManager(ILogger<SymbolManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = new HttpClient();
            _symbolCache = new Dictionary<string, SymbolInfo>();
            _localSymbolCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DumpMiner", "SymbolCache");

            Directory.CreateDirectory(_localSymbolCache);
            InitializeSymbolPaths();
        }

        public async Task<SymbolResolutionResult> ResolveSymbolsAsync(ClrModule module, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = new SymbolResolutionResult
                {
                    ModuleName = module.Name,
                    ModuleAddress = module.ImageBase,
                    Success = false
                };

                // Try to get PDB info
                var pdbInfo = module.Pdb;
                if (pdbInfo != null)
                {
                    var symbolPath = await DownloadSymbolsAsync(pdbInfo, cancellationToken);
                    if (!string.IsNullOrEmpty(symbolPath))
                    {
                        result.SymbolPath = symbolPath;
                        result.Success = true;
                        result.HasSourceInfo = await AnalyzeSourceInfoAsync(symbolPath, cancellationToken);

                        _symbolCache[module.Name] = new SymbolInfo
                        {
                            ModuleName = module.Name,
                            SymbolPath = symbolPath,
                            PdbGuid = pdbInfo.Guid,
                            TimeStamp = pdbInfo.Revision,
                            HasSourceInfo = result.HasSourceInfo,
                            LoadTime = DateTime.UtcNow
                        };
                    }
                }

                // Analyze available debugging information
                result.DebuggingInfo = AnalyzeDebuggingInfo(module);

                _logger.LogInformation("Symbol resolution for {ModuleName}: {Success}", module.Name, result.Success);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving symbols for module {ModuleName}", module.Name);
                return new SymbolResolutionResult
                {
                    ModuleName = module.Name,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<SourceMappingResult> MapToSourceAsync(ClrMethod method, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = new SourceMappingResult
                {
                    MethodName = method.Signature,
                    Success = false
                };

                // Check if we have symbols for this module
                var module = method.Type?.Module;
                if (module == null)
                {
                    result.ErrorMessage = "Module not available";
                    return result;
                }

                // Ensure symbols are loaded
                var symbolResult = await ResolveSymbolsAsync(module, cancellationToken);
                if (!symbolResult.Success || !symbolResult.HasSourceInfo)
                {
                    result.ErrorMessage = "No source information available";
                    return result;
                }

                // Try to get source location from debugging information
                var sourceInfo = ExtractSourceInfo(method);
                if (sourceInfo != null)
                {
                    result.SourceFile = sourceInfo.FileName;
                    result.LineNumber = sourceInfo.LineNumber;
                    result.ColumnNumber = sourceInfo.ColumnNumber;
                    result.Success = true;

                    // Try to load actual source code
                    if (File.Exists(sourceInfo.FileName))
                    {
                        result.SourceCode = await LoadSourceCodeAsync(sourceInfo.FileName, sourceInfo.LineNumber, cancellationToken);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping method to source: {MethodName}", method.Signature);
                return new SourceMappingResult
                {
                    MethodName = method.Signature,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<PdbAnalysisResult> AnalyzePdbAsync(string pdbPath, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = new PdbAnalysisResult
                {
                    PdbPath = pdbPath,
                    Success = false
                };

                if (!File.Exists(pdbPath))
                {
                    result.ErrorMessage = "PDB file not found";
                    return result;
                }

                // Analyze PDB structure and contents
                var fileInfo = new FileInfo(pdbPath);
                result.FileSize = fileInfo.Length;
                result.LastModified = fileInfo.LastWriteTime;

                // Extract metadata from PDB
                await Task.Run(() =>
                {
                    try
                    {
                        // Use available debugging APIs to analyze PDB
                        result.HasSourceInfo = AnalyzePdbSourceInfo(pdbPath);
                        result.HasTypeInfo = AnalyzePdbTypeInfo(pdbPath);
                        result.HasLineInfo = AnalyzePdbLineInfo(pdbPath);
                        result.Success = true;
                    }
                    catch (Exception ex)
                    {
                        result.ErrorMessage = ex.Message;
                    }
                }, cancellationToken);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing PDB: {PdbPath}", pdbPath);
                return new PdbAnalysisResult
                {
                    PdbPath = pdbPath,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public void ConfigureSymbolPaths(IEnumerable<string> symbolPaths)
        {
            try
            {
                var pathString = string.Join(";", symbolPaths.Concat(new[] { _localSymbolCache }));

                // Configure symbol path for debugging session
                if (DebuggerSession.Instance.IsAttached)
                {
                    DebuggerSession.Instance.SetSymbolPath(pathString.Split(';'), false);
                }

                _logger.LogInformation("Symbol paths configured: {SymbolPaths}", pathString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error configuring symbol paths");
            }
        }

        public SymbolCacheStatus GetCacheStatus()
        {
            try
            {
                var cacheDir = new DirectoryInfo(_localSymbolCache);
                if (!cacheDir.Exists)
                    return new SymbolCacheStatus();

                var files = cacheDir.GetFiles("*.pdb", SearchOption.AllDirectories);
                var totalSize = files.Sum(f => f.Length);

                return new SymbolCacheStatus
                {
                    CachePath = _localSymbolCache,
                    FileCount = files.Length,
                    TotalSize = totalSize,
                    LastAccessed = files.Any() ? files.Max(f => f.LastAccessTime) : (DateTime?)null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache status");
                return new SymbolCacheStatus { ErrorMessage = ex.Message };
            }
        }

        private async Task<string> DownloadSymbolsAsync(PdbInfo pdbInfo, CancellationToken cancellationToken)
        {
            try
            {
                var fileName = Path.GetFileName(pdbInfo.Path);
                var localPath = Path.Combine(_localSymbolCache, pdbInfo.Guid.ToString("N"), pdbInfo.Revision.ToString(), fileName);

                // Check if already cached
                if (File.Exists(localPath))
                {
                    _logger.LogDebug("Using cached symbols: {LocalPath}", localPath);
                    return localPath;
                }

                // Try to download from symbol servers
                foreach (var serverUrl in _symbolServers)
                {
                    try
                    {
                        var symbolUrl = $"{serverUrl.TrimEnd('/')}/{fileName}/{pdbInfo.Guid:N}{pdbInfo.Revision:x}/{fileName}";
                        _logger.LogDebug("Attempting to download symbols from: {SymbolUrl}", symbolUrl);

                        var response = await _httpClient.GetAsync(symbolUrl, cancellationToken);
                        if (response.IsSuccessStatusCode)
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                            await using var fileStream = File.Create(localPath);
                            await response.Content.CopyToAsync(fileStream, cancellationToken);

                            _logger.LogInformation("Downloaded symbols to: {LocalPath}", localPath);
                            return localPath;
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogDebug("Failed to download from {ServerUrl}: {Error}", serverUrl, ex.Message);
                    }
                }

                _logger.LogWarning("Could not download symbols for {FileName}", fileName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading symbols for {FileName}", pdbInfo.Path);
                return null;
            }
        }

        private async Task<bool> AnalyzeSourceInfoAsync(string symbolPath, CancellationToken cancellationToken)
        {
            try
            {
                return await Task.Run(() => AnalyzePdbSourceInfo(symbolPath), cancellationToken);
            }
            catch
            {
                return false;
            }
        }

        private DebuggingInfo AnalyzeDebuggingInfo(ClrModule module)
        {
            return new DebuggingInfo
            {
                HasPdb = module.Pdb != null,
                PdbPath = module.Pdb?.Path,
                IsOptimized = module.IsOptimized(),
                DebuggingMode = module.IsOptimized() ? "Release" : "Debug",
                ModuleSize = module.Size,
                ImageBase = module.ImageBase,
                AssemblyName = module.AssemblyName,
                // Version = module.Version?.ToString()
            };
        }

        private SourceLocationInfo ExtractSourceInfo(ClrMethod method)
        {
            try
            {
                // This would use debugging information to map IL offset to source location
                // Implementation depends on available debugging APIs

                // For now, return mock data - in real implementation this would
                // use PDB information to get actual source locations
                return null; // Placeholder for actual implementation
            }
            catch
            {
                return null;
            }
        }

        private async Task<string> LoadSourceCodeAsync(string filePath, int lineNumber, CancellationToken cancellationToken)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
                if (lineNumber < 1 || lineNumber > lines.Length)
                    return null;

                // Return context around the target line
                var startLine = Math.Max(0, lineNumber - 10);
                var endLine = Math.Min(lines.Length - 1, lineNumber + 10);

                var contextLines = new List<string>();
                for (int i = startLine; i <= endLine; i++)
                {
                    var prefix = i == lineNumber - 1 ? " >>> " : "     ";
                    contextLines.Add($"{i + 1:D4}{prefix}{lines[i]}");
                }

                return string.Join(Environment.NewLine, contextLines);
            }
            catch
            {
                return null;
            }
        }

        private bool AnalyzePdbSourceInfo(string pdbPath)
        {
            // Placeholder for PDB source info analysis
            // Real implementation would use debugging APIs
            return File.Exists(pdbPath);
        }

        private bool AnalyzePdbTypeInfo(string pdbPath)
        {
            // Placeholder for PDB type info analysis
            return File.Exists(pdbPath);
        }

        private bool AnalyzePdbLineInfo(string pdbPath)
        {
            // Placeholder for PDB line info analysis
            return File.Exists(pdbPath);
        }

        private void InitializeSymbolPaths()
        {
            var defaultPaths = new[]
            {
                _localSymbolCache,
                Environment.CurrentDirectory,
                @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319",
                @"C:\Windows\Microsoft.NET\Framework\v4.0.30319"
            };

            ConfigureSymbolPaths(defaultPaths.Where(Directory.Exists));
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    // Supporting classes
    public class SymbolInfo
    {
        public string ModuleName { get; set; }
        public string SymbolPath { get; set; }
        public Guid PdbGuid { get; set; }
        public int TimeStamp { get; set; }
        public bool HasSourceInfo { get; set; }
        public DateTime LoadTime { get; set; }
    }

    public class SymbolResolutionResult
    {
        public string ModuleName { get; set; }
        public ulong ModuleAddress { get; set; }
        public bool Success { get; set; }
        public string SymbolPath { get; set; }
        public bool HasSourceInfo { get; set; }
        public DebuggingInfo DebuggingInfo { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class SourceMappingResult
    {
        public string MethodName { get; set; }
        public bool Success { get; set; }
        public string SourceFile { get; set; }
        public int LineNumber { get; set; }
        public int ColumnNumber { get; set; }
        public string SourceCode { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class PdbAnalysisResult
    {
        public string PdbPath { get; set; }
        public bool Success { get; set; }
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public bool HasSourceInfo { get; set; }
        public bool HasTypeInfo { get; set; }
        public bool HasLineInfo { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class DebuggingInfo
    {
        public bool HasPdb { get; set; }
        public string PdbPath { get; set; }
        public bool IsOptimized { get; set; }
        public string DebuggingMode { get; set; }
        public ulong ModuleSize { get; set; }
        public ulong ImageBase { get; set; }
        public string AssemblyName { get; set; }
        public string Version { get; set; }
    }

    public class SourceLocationInfo
    {
        public string FileName { get; set; }
        public int LineNumber { get; set; }
        public int ColumnNumber { get; set; }
    }

    public class SymbolCacheStatus
    {
        public string CachePath { get; set; }
        public int FileCount { get; set; }
        public long TotalSize { get; set; }
        public DateTime? LastAccessed { get; set; }
        public string ErrorMessage { get; set; }
    }
}