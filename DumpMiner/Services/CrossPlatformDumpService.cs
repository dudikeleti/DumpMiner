using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Models;
using Microsoft.Diagnostics.NETCore.Client;

namespace DumpMiner.Services
{
    /// <summary>
    /// Cross-platform implementation of dump creation service using Microsoft.Diagnostics.NETCore.Client.
    /// Provides robust, enterprise-grade dump creation capabilities for .NET processes.
    /// </summary>
    [Export(typeof(IDumpCreationService))]
    public sealed class CrossPlatformDumpService : IDumpCreationService, IDisposable
    {
        private static readonly Dictionary<Models.DumpType, Microsoft.Diagnostics.NETCore.Client.DumpType> DumpTypeMapping =
            new()
            {
                { Models.DumpType.Mini, Microsoft.Diagnostics.NETCore.Client.DumpType.Normal },
                { Models.DumpType.Heap, Microsoft.Diagnostics.NETCore.Client.DumpType.WithHeap },
                { Models.DumpType.Triage, Microsoft.Diagnostics.NETCore.Client.DumpType.Triage },
                { Models.DumpType.Full, Microsoft.Diagnostics.NETCore.Client.DumpType.Full }
            };

        private static readonly Dictionary<Models.DumpType, (double minFactor, double maxFactor)> SizeEstimationFactors =
            new()
            {
                { Models.DumpType.Mini, (0.01, 0.05) },      // 1-5% of working set
                { Models.DumpType.Heap, (0.3, 0.8) },       // 30-80% of working set
                { Models.DumpType.Triage, (0.005, 0.02) },  // 0.5-2% of working set
                { Models.DumpType.Full, (0.8, 1.2) }        // 80-120% of working set
            };

        private readonly SemaphoreSlim _concurrencyLimiter;
        private volatile bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CrossPlatformDumpService"/> class.
        /// </summary>
        public CrossPlatformDumpService()
        {
            // Limit concurrent dump operations to prevent system overload
            _concurrencyLimiter = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
        }

        /// <inheritdoc />
        public async Task<DumpCreationResult> CreateDumpAsync(
            int processId,
            string outputPath,
            Models.DumpType dumpType,
            IProgress<DumpProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (processId <= 0)
                throw new ArgumentException("Process ID must be positive.", nameof(processId));

            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path cannot be null or empty.", nameof(outputPath));

            var stopwatch = Stopwatch.StartNew();
            var processName = "Unknown";

            // Acquire semaphore to limit concurrent operations
            await _concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                // Validate process and get basic information
                var (isValid, process) = await ValidateProcessAsync(processId, cancellationToken).ConfigureAwait(false);
                if (!isValid || process == null)
                {
                    return DumpCreationResult.CreateFailure(
                        "Process not found, has exited, or is not a valid .NET process",
                        processId, processName, dumpType, stopwatch.Elapsed);
                }

                processName = process.ProcessName;

                progress?.Report(DumpProgress.Create(5, "Validating process and permissions...",
                    elapsedTime: stopwatch.Elapsed));

                // Validate output path
                var pathValidation = await ValidateDumpPathAsync(outputPath).ConfigureAwait(false);
                if (!pathValidation.IsValid)
                {
                    var errors = string.Join("; ", pathValidation.Errors);
                    return DumpCreationResult.CreateFailure(
                        $"Invalid output path: {errors}",
                        processId, processName, dumpType, stopwatch.Elapsed);
                }

                progress?.Report(DumpProgress.Create(15, "Estimating dump size...",
                    elapsedTime: stopwatch.Elapsed));

                // Estimate dump size and check available space
                var sizeEstimation = await EstimateDumpSizeAsync(processId, dumpType).ConfigureAwait(false);
                if (sizeEstimation.Success && sizeEstimation.EstimatedMaxSizeBytes > pathValidation.AvailableFreeSpaceBytes)
                {
                    return DumpCreationResult.CreateFailure(
                        $"Insufficient disk space. Estimated dump size: {sizeEstimation.FormattedSizeRange}, Available: {FormatBytes(pathValidation.AvailableFreeSpaceBytes)}",
                        processId, processName, dumpType, stopwatch.Elapsed);
                }

                progress?.Report(DumpProgress.Create(25, "Initializing dump creation...",
                    elapsedTime: stopwatch.Elapsed,
                    estimatedTotalBytes: sizeEstimation.EstimatedMaxSizeBytes));

                // Create the dump
                var result = await CreateDumpInternalAsync(
                    processId, processName, outputPath, dumpType, progress, stopwatch, cancellationToken)
                    .ConfigureAwait(false);

                return result;
            }
            catch (OperationCanceledException)
            {
                return DumpCreationResult.CreateFailure(
                    "Dump creation was cancelled",
                    processId, processName, dumpType, stopwatch.Elapsed);
            }
            catch (UnauthorizedAccessException ex)
            {
                return DumpCreationResult.CreateFailure(
                    $"Access denied: {ex.Message}. Try running as administrator/root.",
                    processId, processName, dumpType, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                return DumpCreationResult.CreateFailure(
                    $"Unexpected error: {ex.Message}",
                    processId, processName, dumpType, stopwatch.Elapsed);
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        }

        /// <inheritdoc />
        public async Task<bool> IsProcessDumpableAsync(int processId)
        {
            ThrowIfDisposed();

            try
            {
                var (isValid, process) = await ValidateProcessAsync(processId, CancellationToken.None).ConfigureAwait(false);
                return isValid && process != null;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public IEnumerable<Models.DumpType> GetSupportedDumpTypes()
        {
            ThrowIfDisposed();
            return DumpTypeMapping.Keys.ToArray();
        }

        /// <inheritdoc />
        public async Task<PathValidationResult> ValidateDumpPathAsync(string proposedPath)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(proposedPath))
                return PathValidationResult.CreateInvalid(proposedPath, new[] { "Path cannot be null or empty" });

            var errors = new List<string>();
            var warnings = new List<string>();

            try
            {
                // Normalize path
                var fullPath = Path.GetFullPath(proposedPath);
                var directory = Path.GetDirectoryName(fullPath);
                var fileName = Path.GetFileName(fullPath);

                // Validate directory
                if (string.IsNullOrEmpty(directory))
                {
                    errors.Add("Invalid directory path");
                    return PathValidationResult.CreateInvalid(proposedPath, errors, warnings);
                }

                // Check if directory exists, create if possible
                if (!Directory.Exists(directory))
                {
                    try
                    {
                        Directory.CreateDirectory(directory);
                        warnings.Add("Directory was created");
                    }
                    catch (UnauthorizedAccessException)
                    {
                        errors.Add("Cannot create directory - access denied");
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Cannot create directory: {ex.Message}");
                    }
                }

                // Validate filename
                if (string.IsNullOrEmpty(fileName))
                {
                    errors.Add("Filename cannot be empty");
                    return PathValidationResult.CreateInvalid(proposedPath, errors, warnings);
                }

                // Check for invalid characters
                var invalidChars = Path.GetInvalidFileNameChars();
                if (fileName.Any(c => invalidChars.Contains(c)))
                {
                    errors.Add("Filename contains invalid characters");
                }

                // Ensure .dmp extension
                if (!Path.GetExtension(fileName).Equals(".dmp", StringComparison.OrdinalIgnoreCase))
                {
                    fullPath = Path.ChangeExtension(fullPath, ".dmp");
                    warnings.Add("Added .dmp extension");
                }

                // Check if file already exists
                if (File.Exists(fullPath))
                {
                    warnings.Add("File already exists and will be overwritten");
                }

                // Get available free space
                var driveInfo = new DriveInfo(Path.GetPathRoot(fullPath));
                var availableSpace = driveInfo.AvailableFreeSpace;

                if (errors.Count > 0)
                    return PathValidationResult.CreateInvalid(proposedPath, errors, warnings);

                return PathValidationResult.CreateValid(fullPath, availableSpace, warnings);
            }
            catch (Exception ex)
            {
                errors.Add($"Path validation failed: {ex.Message}");
                return PathValidationResult.CreateInvalid(proposedPath, errors, warnings);
            }
        }

        /// <inheritdoc />
        public async Task<DumpSizeEstimation> EstimateDumpSizeAsync(int processId, Models.DumpType dumpType)
        {
            ThrowIfDisposed();

            try
            {
                var process = Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    return new DumpSizeEstimation { Success = false };
                }

                var memoryInfo = new ProcessMemoryInfo
                {
                    WorkingSetBytes = process.WorkingSet64,
                    VirtualMemoryBytes = process.VirtualMemorySize64,
                    PrivateMemoryBytes = process.PrivateMemorySize64,
                    Is64BitProcess = Environment.Is64BitProcess, // Approximation
                    ThreadCount = process.Threads.Count,
                    HandleCount = process.HandleCount
                };

                var (minFactor, maxFactor) = SizeEstimationFactors[dumpType];
                var baseSize = process.WorkingSet64;

                var estimatedMin = (long)(baseSize * minFactor);
                var estimatedMax = (long)(baseSize * maxFactor);

                // Add some safety margin for metadata and overhead
                estimatedMax = (long)(estimatedMax * 1.1);

                var warnings = new List<string>();
                if (baseSize > 2L * 1024 * 1024 * 1024) // 2GB
                {
                    warnings.Add("Process has large memory footprint - dump may be very large");
                }

                return new DumpSizeEstimation
                {
                    Success = true,
                    EstimatedMinSizeBytes = estimatedMin,
                    EstimatedMaxSizeBytes = estimatedMax,
                    ConfidenceLevel = 0.7, // Moderate confidence
                    ProcessMemoryInfo = memoryInfo,
                    Warnings = warnings
                };
            }
            catch
            {
                return new DumpSizeEstimation { Success = false };
            }
        }

        private async Task<DumpCreationResult> CreateDumpInternalAsync(
            int processId,
            string processName,
            string outputPath,
            Models.DumpType dumpType,
            IProgress<DumpProgress> progress,
            Stopwatch stopwatch,
            CancellationToken cancellationToken)
        {
            try
            {
                progress?.Report(DumpProgress.Create(30, "Connecting to process...",
                    elapsedTime: stopwatch.Elapsed));

                // Create diagnostics client
                var client = new DiagnosticsClient(processId);

                progress?.Report(DumpProgress.Create(40, "Initiating dump creation...",
                    elapsedTime: stopwatch.Elapsed));

                // Convert dump type
                var nativeDumpType = DumpTypeMapping[dumpType];

                // Create the dump with timeout
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromMinutes(10)); // 10-minute timeout

                progress?.Report(DumpProgress.Create(50, $"Creating {dumpType} dump...",
                    elapsedTime: stopwatch.Elapsed));

                // Perform the actual dump creation
                await client.WriteDumpAsync(nativeDumpType, outputPath, logDumpGeneration: false, timeoutCts.Token)
                    .ConfigureAwait(false);

                progress?.Report(DumpProgress.Create(90, "Verifying dump file...",
                    elapsedTime: stopwatch.Elapsed));

                // Verify file was created and get info
                if (!File.Exists(outputPath))
                {
                    return DumpCreationResult.CreateFailure(
                        "Dump file was not created successfully",
                        processId, processName, dumpType, stopwatch.Elapsed);
                }

                var fileInfo = new FileInfo(outputPath);
                if (fileInfo.Length == 0)
                {
                    return DumpCreationResult.CreateFailure(
                        "Dump file was created but is empty",
                        processId, processName, dumpType, stopwatch.Elapsed);
                }

                progress?.Report(DumpProgress.Create(100, "Dump created successfully",
                    elapsedTime: stopwatch.Elapsed,
                    bytesWritten: fileInfo.Length));

                // Create metadata
                var metadata = new Dictionary<string, object>
                {
                    ["CreatedBy"] = "DumpMiner CrossPlatformDumpService",
                    ["Platform"] = RuntimeInformation.OSDescription,
                    ["Architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
                    ["DotNetVersion"] = Environment.Version.ToString(),
                    ["MachineName"] = Environment.MachineName,
                    ["UserName"] = Environment.UserName
                };

                return DumpCreationResult.CreateSuccess(
                    outputPath,
                    fileInfo.Length,
                    stopwatch.Elapsed,
                    dumpType,
                    processId,
                    processName,
                    metadata);
            }
            catch (DiagnosticsClientException ex)
            {
                var errorMessage = ex.Message.Contains("HRESULT")
                    ? "Process may not be a .NET process or may have insufficient permissions"
                    : $"Diagnostics error: {ex.Message}";

                return DumpCreationResult.CreateFailure(
                    errorMessage, processId, processName, dumpType, stopwatch.Elapsed);
            }
            catch (TimeoutException)
            {
                return DumpCreationResult.CreateFailure(
                    "Dump creation timed out", processId, processName, dumpType, stopwatch.Elapsed);
            }
            catch (IOException ex)
            {
                return DumpCreationResult.CreateFailure(
                    $"File I/O error: {ex.Message}", processId, processName, dumpType, stopwatch.Elapsed);
            }
        }

        private static async Task<(bool isValid, Process process)> ValidateProcessAsync(int processId, CancellationToken cancellationToken)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (process.HasExited)
                    return (false, null);

                // Quick check to see if it's a .NET process
                // This is a lightweight operation that doesn't require attaching
                var client = new DiagnosticsClient(processId);

                // Try to connect to see if it's a .NET process - this will throw if not a .NET process
                await Task.Run(() => client.GetProcessEnvironment(), cancellationToken).ConfigureAwait(false);

                return (true, process);
            }
            catch (ArgumentException) // Process not found
            {
                return (false, null);
            }
            catch (InvalidOperationException) // Process exited
            {
                return (false, null);
            }
            catch (DiagnosticsClientException) // Not a .NET process
            {
                return (false, null);
            }
            catch
            {
                return (false, null);
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            if (bytes == 0) return "0 B";

            var place = Convert.ToInt32(Math.Floor(Math.Log(Math.Abs(bytes), 1024)));
            place = Math.Min(place, suffixes.Length - 1);
            var num = Math.Round(bytes / Math.Pow(1024, place), 2);
            return $"{num:F2} {suffixes[place]}";
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CrossPlatformDumpService));
        }

        /// <summary>
        /// Releases all resources used by the <see cref="CrossPlatformDumpService"/>.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _concurrencyLimiter?.Dispose();
                _disposed = true;
            }
        }
    }
}