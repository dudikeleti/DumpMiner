using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;
using DumpMiner.Services.AI.Context;
using DumpMiner.Services.AI.Models;
using DumpMiner.Services.AI.Orchestration;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;

namespace DumpMiner.Services.AI
{
    /// <summary>
    /// Professional-grade comprehensive dump analyzer
    /// </summary>
    public interface IComprehensiveDumpAnalyzer
    {
        Task<ComprehensiveAnalysisResult> AnalyzeDumpAsync(CancellationToken cancellationToken = default);
        event EventHandler<AnalysisProgressEventArgs> ProgressChanged;
        string GetDiagnosticInfo();
    }

    [Export(typeof(IComprehensiveDumpAnalyzer))]
    public class ComprehensiveDumpAnalyzer : IComprehensiveDumpAnalyzer
    {
        private readonly IAIOrchestrator _aiOrchestrator;
        private readonly IDumpAnalysisContextBuilder _contextBuilder;
        private readonly ILogger<ComprehensiveDumpAnalyzer> _logger;

        public event EventHandler<AnalysisProgressEventArgs> ProgressChanged;

        /// <summary>
        /// Constructor with optional dependencies for MEF compatibility
        /// </summary>
        [ImportingConstructor]
        public ComprehensiveDumpAnalyzer(
            [Import(AllowDefault = true)] IAIOrchestrator aiOrchestrator,
            [Import(AllowDefault = true)] IDumpAnalysisContextBuilder contextBuilder,
            [Import(AllowDefault = true)] ILogger<ComprehensiveDumpAnalyzer> logger)
        {
            _aiOrchestrator = aiOrchestrator;
            _contextBuilder = contextBuilder ?? CreateFallbackContextBuilder();
            _logger = logger ?? CreateFallbackLogger();
        }

        /// <summary>
        /// Creates a fallback context builder if MEF resolution fails
        /// </summary>
        private static IDumpAnalysisContextBuilder CreateFallbackContextBuilder()
        {
            try
            {
                return new DumpAnalysisContextBuilder(CreateFallbackLogger<DumpAnalysisContextBuilder>());
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Creates a fallback logger if MEF resolution fails
        /// </summary>
        private static ILogger<ComprehensiveDumpAnalyzer> CreateFallbackLogger()
        {
            return CreateFallbackLogger<ComprehensiveDumpAnalyzer>();
        }

        /// <summary>
        /// Creates a simple fallback logger for any type using Serilog
        /// </summary>
        private static ILogger<T> CreateFallbackLogger<T>()
        {
            var serilogLogger = Serilog.Log.ForContext<T>();
            return new SerilogLoggerFactory(serilogLogger).CreateLogger<T>();
        }

        public async Task<ComprehensiveAnalysisResult> AnalyzeDumpAsync(CancellationToken cancellationToken = default)
        {
            var result = new ComprehensiveAnalysisResult { StartTime = DateTimeOffset.UtcNow };

            try
            {
                // Check debugger session first
                if (!DebuggerSession.Instance.IsAttached)
                {
                    result.ErrorMessage = "❌ No debugger session is attached. Please attach to a process or load a dump file first.";
                    result.IsSuccess = false;
                    return result;
                }

                // Check if required dependencies are available
                if (_contextBuilder == null)
                {
                    result.ErrorMessage = "❌ Analysis context builder not available";
                    result.IsSuccess = false;
                    return result;
                }

                // Check AI orchestrator availability and provide fallback
                var aiAvailable = _aiOrchestrator != null;
                if (!aiAvailable)
                {
                    _logger?.LogWarning("AI Orchestrator not available - analysis will run without AI insights");
                }

                _logger?.LogInformation("Starting comprehensive dump analysis (AI Available: {AIAvailable})", aiAvailable);
                ReportProgress("Initializing analysis...", 0);

                // Phase 1: Build dump context
                var dumpContext = await _contextBuilder.BuildDumpContextAsync();
                result.DumpContext = dumpContext;
                ReportProgress("Built dump context", 10);

                // Phase 2: Critical Issue Detection
                result.CriticalIssues = await AnalyzeCriticalIssuesAsync(cancellationToken);
                ReportProgress("Analyzed critical issues", 25);

                // Phase 3: Memory Analysis
                result.MemoryAnalysis = await AnalyzeMemoryAsync(cancellationToken);
                ReportProgress("Completed memory analysis", 40);

                // Phase 4: Threading Analysis
                result.ThreadingAnalysis = await AnalyzeThreadingAsync(cancellationToken);
                ReportProgress("Completed threading analysis", 55);

                // Phase 5: Exception Analysis
                result.ExceptionAnalysis = await AnalyzeExceptionsAsync(cancellationToken);
                ReportProgress("Completed exception analysis", 70);

                // Phase 6: Performance Analysis
                result.PerformanceAnalysis = await AnalyzePerformanceAsync(cancellationToken);
                ReportProgress("Completed performance analysis", 85);

                // Phase 7: AI Root Cause Analysis (if AI available)
                if (aiAvailable)
                {
                    result.RootCauseAnalysis = await PerformRootCauseAnalysisAsync(result, cancellationToken);
                    ReportProgress("Completed root cause analysis", 95);
                }
                else
                {
                    result.RootCauseAnalysis = new RootCauseAnalysisResult
                    {
                        DetailedAnalysis = "🤖 AI analysis not available - please check AI service configuration in Settings.",
                        RootCause = "AI services not configured",
                        AnalysisComplete = false,
                        ConfidenceLevel = 0
                    };
                    ReportProgress("Skipped AI analysis (not available)", 95);
                }

                // Phase 8: Generate recommendations
                result.Recommendations = GenerateRecommendations(result);
                result.IsSuccess = true;
                result.EndTime = DateTimeOffset.UtcNow;
                
                ReportProgress("Analysis complete", 100);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during comprehensive dump analysis");
                result.ErrorMessage = $"❌ Analysis failed: {ex.Message}";
                result.IsSuccess = false;
                result.EndTime = DateTimeOffset.UtcNow;
                return result;
            }
        }

        private async Task<CriticalIssueAnalysis> AnalyzeCriticalIssuesAsync(CancellationToken cancellationToken)
        {
            var analysis = new CriticalIssueAnalysis();
            
            try
            {
                // Check for exceptions
                var exceptionsOp = App.Container.GetExportedValue<IDebuggerOperation>(OperationNames.DumpExceptions);
                var exceptionResults = await exceptionsOp.Execute(new OperationModel(), cancellationToken, null);
                var exceptions = new Collection<object>(exceptionResults.ToList());

                if (exceptions.Any())
                {
                    analysis.HasCriticalExceptions = true;
                    analysis.Issues.Add($"🔴 CRITICAL: {exceptions.Count} exceptions detected");
                    
                    if (_aiOrchestrator != null)
                    {
                        var aiResult = await _aiOrchestrator.AnalyzeOperationAsync(
                            OperationNames.DumpExceptions,
                            new OperationModel(),
                            exceptions,
                            "Analyze these exceptions for crash root cause and priority fixes.",
                            cancellationToken);

                        if (aiResult.IsSuccess)
                            analysis.ExceptionAnalysisAI = aiResult.Content;
                        else
                            analysis.ExceptionAnalysisAI = $"❌ AI analysis failed: {aiResult.ErrorMessage}";
                    }
                    else
                    {
                        analysis.ExceptionAnalysisAI = $"📋 Found {exceptions.Count} exceptions. AI analysis not available - please configure AI services to get detailed analysis.";
                    }
                }
                else
                {
                    analysis.ExceptionAnalysisAI = "✅ No exceptions found in the dump.";
                }

                analysis.Severity = analysis.HasCriticalExceptions ? IssueSeverity.Critical : IssueSeverity.Low;
            }
            catch (Exception ex)
            {
                analysis.Issues.Add($"❌ Error analyzing critical issues: {ex.Message}");
                _logger?.LogError(ex, "Error in critical issues analysis");
            }

            return analysis;
        }

        private async Task<MemoryAnalysisResult> AnalyzeMemoryAsync(CancellationToken cancellationToken)
        {
            var analysis = new MemoryAnalysisResult();

            try
            {
                var heapStatOp = App.Container.GetExportedValue<IDebuggerOperation>(OperationNames.DumpHeapStat);
                var heapStatResults = await heapStatOp.Execute(new OperationModel(), cancellationToken, null);
                var heapStats = new Collection<object>(heapStatResults.ToList());

                analysis.TotalObjects = heapStats.Count;
                analysis.TotalMemoryUsage = CalculateTotalMemoryUsage(heapStats);

                if (_aiOrchestrator != null)
                {
                    var heapAiResult = await _aiOrchestrator.AnalyzeOperationAsync(
                        OperationNames.DumpHeapStat,
                        new OperationModel(),
                        heapStats,
                        "Analyze heap statistics for memory leaks and optimization opportunities.",
                        cancellationToken);

                    if (heapAiResult.IsSuccess)
                        analysis.HeapAnalysisAI = heapAiResult.Content;
                    else
                        analysis.HeapAnalysisAI = $"❌ AI analysis failed: {heapAiResult.ErrorMessage}";
                }
                else
                {
                    analysis.HeapAnalysisAI = $"📊 Memory Statistics:\n" +
                        $"• Total Objects: {analysis.TotalObjects:N0}\n" +
                        $"• Total Memory: {FormatBytesForAnalysis(analysis.TotalMemoryUsage)}\n" +
                        $"• Memory Pressure: {DetermineMemoryPressure(analysis)}\n\n" +
                        $"🤖 AI analysis not available - configure AI services for detailed insights.";
                }

                analysis.MemoryPressureLevel = DetermineMemoryPressure(analysis);
            }
            catch (Exception ex)
            {
                analysis.ErrorMessage = $"Memory analysis error: {ex.Message}";
                _logger?.LogError(ex, "Error in memory analysis");
            }

            return analysis;
        }

        private async Task<ThreadingAnalysisResult> AnalyzeThreadingAsync(CancellationToken cancellationToken)
        {
            var analysis = new ThreadingAnalysisResult();

            try
            {
                var clrStackOp = App.Container.GetExportedValue<IDebuggerOperation>(OperationNames.DumpClrStack);
                var clrStackResults = await clrStackOp.Execute(new OperationModel(), cancellationToken, null);
                var clrStacks = new Collection<object>(clrStackResults.ToList());

                analysis.ThreadCount = clrStacks.Count;
                
                if (_aiOrchestrator != null)
                {
                    var stackAiResult = await _aiOrchestrator.AnalyzeOperationAsync(
                        OperationNames.DumpClrStack,
                        new OperationModel(),
                        clrStacks,
                        "Analyze thread stacks for deadlocks and performance issues.",
                        cancellationToken);

                    if (stackAiResult.IsSuccess)
                        analysis.StackAnalysisAI = stackAiResult.Content;
                    else
                        analysis.StackAnalysisAI = $"❌ AI analysis failed: {stackAiResult.ErrorMessage}";
                }
                else
                {
                    analysis.StackAnalysisAI = $"🧵 Threading Information:\n" +
                        $"• Active Threads: {analysis.ThreadCount}\n" +
                        $"• Status: {(analysis.ThreadCount > 0 ? "Threads detected" : "No threads found")}\n\n" +
                        $"🤖 AI analysis not available - configure AI services for detailed thread analysis.";
                }
            }
            catch (Exception ex)
            {
                analysis.ErrorMessage = $"Threading analysis error: {ex.Message}";
                _logger?.LogError(ex, "Error in threading analysis");
            }

            return analysis;
        }

        private async Task<ExceptionAnalysisResult> AnalyzeExceptionsAsync(CancellationToken cancellationToken)
        {
            var analysis = new ExceptionAnalysisResult();

            try
            {
                var exceptionsOp = App.Container.GetExportedValue<IDebuggerOperation>(OperationNames.DumpExceptions);
                var exceptionResults = await exceptionsOp.Execute(new OperationModel(), cancellationToken, null);
                var exceptions = new Collection<object>(exceptionResults.ToList());

                analysis.ExceptionCount = exceptions.Count;

                if (exceptions.Any())
                {
                    if (_aiOrchestrator != null)
                    {
                        var exceptionAiResult = await _aiOrchestrator.AnalyzeOperationAsync(
                            OperationNames.DumpExceptions,
                            new OperationModel(),
                            exceptions,
                            "Perform comprehensive exception analysis with root cause identification.",
                            cancellationToken);

                        if (exceptionAiResult.IsSuccess)
                            analysis.DetailedAnalysisAI = exceptionAiResult.Content;
                        else
                            analysis.DetailedAnalysisAI = $"❌ AI analysis failed: {exceptionAiResult.ErrorMessage}";
                    }
                    else
                    {
                        analysis.DetailedAnalysisAI = $"⚠️ Exception Summary:\n" +
                            $"• Total Exceptions: {exceptions.Count}\n" +
                            $"• Severity: {(exceptions.Count > 5 ? "High" : "Medium")}\n\n" +
                            $"🤖 AI analysis not available - configure AI services for detailed exception analysis.";
                    }
                }
                else
                {
                    analysis.DetailedAnalysisAI = "✅ No exceptions found in heap";
                }
            }
            catch (Exception ex)
            {
                analysis.ErrorMessage = $"Exception analysis error: {ex.Message}";
                _logger?.LogError(ex, "Error in exception analysis");
            }

            return analysis;
        }

        private async Task<PerformanceAnalysisResult> AnalyzePerformanceAsync(CancellationToken cancellationToken)
        {
            var analysis = new PerformanceAnalysisResult();

            try
            {
                var processInfoOp = App.Container.GetExportedValue<IDebuggerOperation>(OperationNames.TargetProcessInfo);
                var processResults = await processInfoOp.Execute(new OperationModel(), cancellationToken, null);
                var processInfo = new Collection<object>(processResults.ToList());

                if (processInfo.Any())
                {
                    var processAiResult = await _aiOrchestrator.AnalyzeOperationAsync(
                        OperationNames.TargetProcessInfo,
                        new OperationModel(),
                        processInfo,
                        "Analyze process performance and resource usage.",
                        cancellationToken);

                    if (processAiResult.IsSuccess)
                        analysis.ProcessAnalysisAI = processAiResult.Content;
                }
            }
            catch (Exception ex)
            {
                analysis.ErrorMessage = $"Performance analysis error: {ex.Message}";
            }

            return analysis;
        }

        private async Task<RootCauseAnalysisResult> PerformRootCauseAnalysisAsync(ComprehensiveAnalysisResult result, CancellationToken cancellationToken)
        {
            var analysis = new RootCauseAnalysisResult();

            try
            {
                var contextPrompt = BuildRootCauseContext(result);
                
                var rootCauseAiResult = await _aiOrchestrator.AnalyzeOperationAsync(
                    "ComprehensiveAnalysis",
                    new OperationModel { UserPrompt = new StringBuilder(contextPrompt) },
                    new Collection<object>(),
                    "Provide comprehensive root cause analysis with primary cause and remediation steps.",
                    cancellationToken);

                if (rootCauseAiResult.IsSuccess)
                {
                    analysis.DetailedAnalysis = rootCauseAiResult.Content;
                    analysis.RootCause = "AI-identified primary root cause";
                    analysis.ConfidenceLevel = 85;
                }

                analysis.AnalysisComplete = true;
            }
            catch (Exception ex)
            {
                analysis.ErrorMessage = $"Root cause analysis error: {ex.Message}";
            }

            return analysis;
        }

        private List<ActionableRecommendation> GenerateRecommendations(ComprehensiveAnalysisResult result)
        {
            var recommendations = new List<ActionableRecommendation>();

            if (result.CriticalIssues.HasCriticalExceptions)
            {
                recommendations.Add(new ActionableRecommendation
                {
                    Priority = RecommendationPriority.Critical,
                    Category = "Exception Handling",
                    Title = "Fix Critical Exceptions",
                    Description = "Critical exceptions detected that likely caused the crash",
                    ActionItems = new List<string>
                    {
                        "Review exception stack traces",
                        "Implement proper exception handling",
                        "Add defensive coding practices"
                    }
                });
            }

            return recommendations.OrderBy(r => r.Priority).ToList();
        }

        private void ReportProgress(string message, int percentage)
        {
            ProgressChanged?.Invoke(this, new AnalysisProgressEventArgs
            {
                Message = message,
                PercentageComplete = percentage,
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        private long CalculateTotalMemoryUsage(Collection<object> heapStats)
        {
            long total = 0;
            foreach (var stat in heapStats)
            {
                try
                {
                    var sizeProperty = stat.GetType().GetProperty("Size");
                    if (sizeProperty != null)
                        total += Convert.ToInt64(sizeProperty.GetValue(stat));
                }
                catch { }
            }
            return total;
        }

        private MemoryPressureLevel DetermineMemoryPressure(MemoryAnalysisResult analysis)
        {
            if (analysis.TotalMemoryUsage > 2_000_000_000) return MemoryPressureLevel.Critical;
            if (analysis.TotalMemoryUsage > 500_000_000) return MemoryPressureLevel.High;
            if (analysis.TotalMemoryUsage > 100_000_000) return MemoryPressureLevel.Medium;
            return MemoryPressureLevel.Low;
        }

        private string BuildRootCauseContext(ComprehensiveAnalysisResult result)
        {
            var context = new StringBuilder();
            context.AppendLine("=== COMPREHENSIVE DUMP ANALYSIS SUMMARY ===");
            context.AppendLine($"Analysis Duration: {result.EndTime - result.StartTime}");
            
            if (result.CriticalIssues.Issues.Any())
            {
                context.AppendLine("CRITICAL ISSUES:");
                foreach (var issue in result.CriticalIssues.Issues)
                    context.AppendLine($"- {issue}");
            }

            context.AppendLine($"MEMORY: {result.MemoryAnalysis.TotalObjects} objects");
            context.AppendLine($"THREADS: {result.ThreadingAnalysis.ThreadCount} threads");
            context.AppendLine($"EXCEPTIONS: {result.ExceptionAnalysis.ExceptionCount} exceptions");

            return context.ToString();
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }
            return $"{number:n1}{suffixes[counter]}";
        }

        private static string FormatBytesForAnalysis(long bytes)
        {
            return FormatBytes(bytes);
        }

        /// <summary>
        /// Diagnostic method to check AI service availability
        /// </summary>
        public string GetDiagnosticInfo()
        {
            var diagnostics = new StringBuilder();
            diagnostics.AppendLine("🔍 DumpMiner AI Diagnostic Information");
            diagnostics.AppendLine("=================================");
            
            // Check debugger session
            diagnostics.AppendLine($"Debugger Session: {(DebuggerSession.Instance.IsAttached ? "✅ Attached" : "❌ Not Attached")}");
            
            // Check AI orchestrator
            diagnostics.AppendLine($"AI Orchestrator: {(_aiOrchestrator != null ? "✅ Available" : "❌ Not Available")}");
            
            // Check context builder
            diagnostics.AppendLine($"Context Builder: {(_contextBuilder != null ? "✅ Available" : "❌ Not Available")}");
            
            // Check AIHelper
            diagnostics.AppendLine($"AI Helper: {(App.AIHelper != null ? "✅ Available" : "❌ Not Available")}");
            
            // Check MEF container
            diagnostics.AppendLine($"MEF Container: {(App.Container != null ? "✅ Available" : "❌ Not Available")}");
            
            diagnostics.AppendLine();
            
            if (App.AIHelper?.ServiceManager != null)
            {
                var serviceManager = App.AIHelper.ServiceManager;
                var availableProviders = serviceManager.AvailableProviders.ToList();
                
                diagnostics.AppendLine($"Default Provider: {serviceManager.DefaultProvider}");
                diagnostics.AppendLine($"Available Providers: {(availableProviders.Any() ? string.Join(", ", availableProviders) : "❌ NONE")}");
                
                if (!availableProviders.Any())
                {
                    diagnostics.AppendLine();
                    diagnostics.AppendLine("🚨 ROOT CAUSE: No AI providers are available!");
                    diagnostics.AppendLine("This means API keys are missing or invalid.");
                    diagnostics.AppendLine();
                    
                    // Check configuration structure
                    try
                    {
                        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                        diagnostics.AppendLine($"Configuration file: {configPath}");
                        diagnostics.AppendLine($"File exists: {File.Exists(configPath)}");
                        
                        if (File.Exists(configPath))
                        {
                            var json = File.ReadAllText(configPath);
                            var hasAISection = json.Contains("\"AI\"");
                            var hasApplicationAI = json.Contains("\"Application\"") && json.Contains("\"AI\"");
                            var hasOpenAIKey = json.Contains("\"OpenAI\"") && json.Contains("\"ApiKey\"");
                            
                            diagnostics.AppendLine($"Has AI section: {hasAISection}");
                            diagnostics.AppendLine($"Has Application.AI structure: {hasApplicationAI}");
                            diagnostics.AppendLine($"Has OpenAI API key: {hasOpenAIKey}");
                            
                            if (hasApplicationAI && !hasAISection)
                            {
                                diagnostics.AppendLine();
                                diagnostics.AppendLine("🔧 CONFIGURATION ISSUE DETECTED:");
                                diagnostics.AppendLine("Your AI config is under 'Application.AI' but the service expects 'AI' at root level.");
                                diagnostics.AppendLine("This is why providers aren't loading.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        diagnostics.AppendLine($"Error checking config: {ex.Message}");
                    }
                }
            }
            else
            {
                diagnostics.AppendLine("AI Service Manager: ❌ Not Available");
            }
            
            diagnostics.AppendLine();
            diagnostics.AppendLine("💡 To fix AI issues:");
            diagnostics.AppendLine("1. Go to Settings → AI Settings");
            diagnostics.AppendLine("2. Configure at least one AI provider (OpenAI, Anthropic, or Google)");
            diagnostics.AppendLine("3. Enter your API key");
            diagnostics.AppendLine("4. Test the connection");
            diagnostics.AppendLine("5. Restart the application if needed");
            
            return diagnostics.ToString();
        }
    }

    // Supporting data classes
    public class ComprehensiveAnalysisResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public DumpContext DumpContext { get; set; } = new();
        public CriticalIssueAnalysis CriticalIssues { get; set; } = new();
        public MemoryAnalysisResult MemoryAnalysis { get; set; } = new();
        public ThreadingAnalysisResult ThreadingAnalysis { get; set; } = new();
        public ExceptionAnalysisResult ExceptionAnalysis { get; set; } = new();
        public PerformanceAnalysisResult PerformanceAnalysis { get; set; } = new();
        public RootCauseAnalysisResult RootCauseAnalysis { get; set; } = new();
        public List<ActionableRecommendation> Recommendations { get; set; } = new();
    }

    public class CriticalIssueAnalysis
    {
        public bool HasCriticalExceptions { get; set; }
        public bool HasDeadlocks { get; set; }
        public List<string> Issues { get; set; } = new();
        public IssueSeverity Severity { get; set; }
        public string ExceptionAnalysisAI { get; set; } = string.Empty;
        public string DeadlockAnalysisAI { get; set; } = string.Empty;
    }

    public class MemoryAnalysisResult
    {
        public int TotalObjects { get; set; }
        public long TotalMemoryUsage { get; set; }
        public int LargeObjectCount { get; set; }
        public MemoryPressureLevel MemoryPressureLevel { get; set; }
        public string HeapAnalysisAI { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class ThreadingAnalysisResult
    {
        public int ThreadCount { get; set; }
        public string StackAnalysisAI { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class ExceptionAnalysisResult
    {
        public int ExceptionCount { get; set; }
        public string DetailedAnalysisAI { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class PerformanceAnalysisResult
    {
        public string ProcessAnalysisAI { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class RootCauseAnalysisResult
    {
        public string RootCause { get; set; } = string.Empty;
        public string DetailedAnalysis { get; set; } = string.Empty;
        public int ConfidenceLevel { get; set; }
        public bool AnalysisComplete { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class ActionableRecommendation
    {
        public RecommendationPriority Priority { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> ActionItems { get; set; } = new();
    }

    public class AnalysisProgressEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public int PercentageComplete { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }

    public enum IssueSeverity { Low, Medium, High, Critical }
    public enum MemoryPressureLevel { Low, Medium, High, Critical }
    public enum RecommendationPriority { Low = 1, Medium = 2, High = 3, Critical = 4 }


} 