using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;
using DumpMiner.Operations.Shared;
using DumpMiner.Services.AI.Orchestration;

namespace DumpMiner.Operations
{
    [Export(OperationNames.AutomatedAnalysis, typeof(IDebuggerOperation))]
    public class AutomatedAnalysisOperation : BaseAIOperation
    {
        private readonly IAIOrchestrator _aiOrchestrator;

        [ImportingConstructor]
        public AutomatedAnalysisOperation([Import(typeof(IAIOrchestrator), AllowDefault = true)] IAIOrchestrator aiOrchestrator)
        {
            _aiOrchestrator = aiOrchestrator;
        }

        public override string Name => OperationNames.AutomatedAnalysis;

        public override async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var analysisResults = new List<AutomatedAnalysisResult>();

                try
                {
                    // Execute comprehensive automated analysis
                    var comprehensiveAnalysis = PerformComprehensiveAnalysis(model, token);
                    analysisResults.AddRange(comprehensiveAnalysis);

                    // Perform intelligent issue detection
                    var issueDetection = PerformIntelligentIssueDetection(token);
                    analysisResults.AddRange(issueDetection);

                    // Generate automated recommendations
                    var recommendations = GenerateAutomatedRecommendations(analysisResults, token);
                    analysisResults.AddRange(recommendations);

                    // Create executive summary
                    var executiveSummary = CreateExecutiveSummary(analysisResults);
                    analysisResults.Insert(0, executiveSummary);

                    return analysisResults;
                }
                catch (Exception ex)
                {
                    return new List<AutomatedAnalysisResult>
                    {
                        new AutomatedAnalysisResult
                        {
                            AnalysisCategory = AnalysisCategory.Error,
                            Title = "Analysis Error",
                            Severity = IssueSeverity.High,
                            Description = $"Error during automated analysis: {ex.Message}",
                            Recommendations = new List<string> { "Check logs for detailed error information", "Retry analysis with different parameters" }
                        }
                    };
                }
            });
        }

        private List<AutomatedAnalysisResult> PerformComprehensiveAnalysis(OperationModel model, CancellationToken token)
        {
            var results = new List<AutomatedAnalysisResult>();

            // 1. Memory Analysis
            var memoryAnalysis = PerformMemoryAnalysis(token);
            results.AddRange(memoryAnalysis);

            // 2. Threading Analysis
            var threadingAnalysis = PerformThreadingAnalysis(token);
            results.AddRange(threadingAnalysis);

            // 3. Performance Analysis
            var performanceAnalysis = PerformPerformanceAnalysis(token);
            results.AddRange(performanceAnalysis);

            // 4. Exception Analysis
            var exceptionAnalysis = PerformExceptionAnalysis(token);
            results.AddRange(exceptionAnalysis);

            // 5. Resource Analysis
            var resourceAnalysis = PerformResourceAnalysis(token);
            results.AddRange(resourceAnalysis);

            return results;
        }

        private List<AutomatedAnalysisResult> PerformMemoryAnalysis(CancellationToken token)
        {
            var results = new List<AutomatedAnalysisResult>();
            var heap = DebuggerSession.Instance.Heap;

            try
            {
                var memoryMetrics = new MemoryAnalysisMetrics();
                var typeDistribution = new Dictionary<string, TypeMetrics>();
                var generationStats = new int[5]; // Gen 0, 1, 2, LOH, Pinned

                // Collect memory statistics
                foreach (var segment in heap.Segments)
                {
                    foreach (var obj in segment.EnumerateObjects())
                    {
                        if (token.IsCancellationRequested) break;

                        memoryMetrics.TotalObjects++;
                        memoryMetrics.TotalSize += (long)obj.Size;

                        var generation = (int)segment.GetGeneration(obj);
                        if (generation >= 0 && generation < generationStats.Length)
                            generationStats[generation]++;

                        var typeName = obj.Type?.Name ?? "Unknown";
                        if (!typeDistribution.ContainsKey(typeName))
                            typeDistribution[typeName] = new TypeMetrics();

                        var typeMetrics = typeDistribution[typeName];
                        typeMetrics.TypeName = typeName;
                        typeMetrics.InstanceCount++;
                        typeMetrics.TotalSize += (long)obj.Size;

                        if (obj.Size > 85000) // LOH threshold
                        {
                            typeMetrics.LargeObjectCount++;
                            memoryMetrics.LargeObjectCount++;
                        }
                    }

                }

                // Analyze memory health
                var memoryHealth = AnalyzeMemoryHealth(memoryMetrics, typeDistribution, generationStats);
                results.Add(memoryHealth);

                // Identify memory issues
                var memoryIssues = IdentifyMemoryIssues(typeDistribution, generationStats);
                results.AddRange(memoryIssues);

                // Check for memory pressure indicators
                var pressureIndicators = CheckMemoryPressureIndicators(memoryMetrics, generationStats);
                if (pressureIndicators != null)
                    results.Add(pressureIndicators);

            }
            catch (Exception ex)
            {
                results.Add(new AutomatedAnalysisResult
                {
                    AnalysisCategory = AnalysisCategory.Memory,
                    Title = "Memory Analysis Error",
                    Severity = IssueSeverity.Medium,
                    Description = $"Error during memory analysis: {ex.Message}"
                });
            }

            return results;
        }

        private List<AutomatedAnalysisResult> PerformThreadingAnalysis(CancellationToken token)
        {
            var results = new List<AutomatedAnalysisResult>();
            var runtime = DebuggerSession.Instance.Runtime;

            try
            {
                var threadMetrics = new ThreadingMetrics();
                var blockedThreads = new List<ThreadInfo>();

                foreach (var thread in runtime.Threads)
                {
                    if (token.IsCancellationRequested) break;

                    threadMetrics.TotalThreads++;

                    if (!thread.IsAlive)
                        threadMetrics.DeadThreads++;
                    else if (thread.IsBackground())
                        threadMetrics.BackgroundThreads++;
                    else
                        threadMetrics.ForegroundThreads++;

                    if (thread.IsBlocked())
                    {
                        threadMetrics.BlockedThreads++;
                        blockedThreads.Add(new ThreadInfo
                        {
                            ManagedThreadId = thread.ManagedThreadId,
                            OSThreadId = thread.OSThreadId,
                            IsAlive = thread.IsAlive,
                            BlockingObjectCount = thread.GetBlockingObjectCount()
                        });
                    }

                    if (thread.CurrentException != null)
                        threadMetrics.ThreadsWithExceptions++;
                }

                // Analyze threading health
                var threadingHealth = AnalyzeThreadingHealth(threadMetrics, blockedThreads);
                results.Add(threadingHealth);

                // Check for deadlock potential
                if (threadMetrics.BlockedThreads > 1)
                {
                    var deadlockRisk = AssessDeadlockRisk(blockedThreads, threadMetrics);
                    results.Add(deadlockRisk);
                }

                // Check for thread pool issues
                var threadPoolAnalysis = AnalyzeThreadPoolUsage(threadMetrics);
                if (threadPoolAnalysis != null)
                    results.Add(threadPoolAnalysis);

            }
            catch (Exception ex)
            {
                results.Add(new AutomatedAnalysisResult
                {
                    AnalysisCategory = AnalysisCategory.Threading,
                    Title = "Threading Analysis Error",
                    Severity = IssueSeverity.Medium,
                    Description = $"Error during threading analysis: {ex.Message}"
                });
            }

            return results;
        }

        private List<AutomatedAnalysisResult> PerformPerformanceAnalysis(CancellationToken token)
        {
            var results = new List<AutomatedAnalysisResult>();
            var runtime = DebuggerSession.Instance.Runtime;

            try
            {
                var performanceMetrics = new PerformanceMetrics();

                // Analyze JIT compilation
                foreach (var appDomain in runtime.AppDomains)
                {
                    if (token.IsCancellationRequested) break;

                    foreach (var module in appDomain.Modules)
                    {
                        performanceMetrics.TotalModules++;
                        if (module.IsOptimized())
                            performanceMetrics.OptimizedModules++;

                        foreach (var type in module.EnumerateTypes())
                        {
                            foreach (var method in type.Methods)
                            {
                                performanceMetrics.TotalMethods++;

                                if (method.NativeCode != 0)
                                {
                                    performanceMetrics.CompiledMethods++;

                                    var codeSize = method.HotColdInfo.HotSize;
                                    if (codeSize > 5000) // Large method threshold
                                        performanceMetrics.LargeMethods++;

                                    performanceMetrics.TotalCodeSize += codeSize;
                                }

                                if (method.IsGeneric())
                                    performanceMetrics.GenericMethods++;
                            }
                        }
                    }
                }

                // Analyze performance health
                var performanceHealth = AnalyzePerformanceHealth(performanceMetrics);
                results.Add(performanceHealth);

                // Check for compilation issues
                var compilationIssues = CheckCompilationIssues(performanceMetrics);
                if (compilationIssues != null)
                    results.Add(compilationIssues);

            }
            catch (Exception ex)
            {
                results.Add(new AutomatedAnalysisResult
                {
                    AnalysisCategory = AnalysisCategory.Performance,
                    Title = "Performance Analysis Error",
                    Severity = IssueSeverity.Medium,
                    Description = $"Error during performance analysis: {ex.Message}"
                });
            }

            return results;
        }

        private List<AutomatedAnalysisResult> PerformExceptionAnalysis(CancellationToken token)
        {
            var results = new List<AutomatedAnalysisResult>();
            var runtime = DebuggerSession.Instance.Runtime;

            try
            {
                var exceptionMetrics = new ExceptionMetrics();
                var exceptionTypes = new Dictionary<string, int>();

                foreach (var thread in runtime.Threads)
                {
                    if (token.IsCancellationRequested) break;

                    var exception = thread.CurrentException;
                    if (exception != null)
                    {
                        exceptionMetrics.TotalExceptions++;
                        var exceptionType = exception.Type?.Name ?? "Unknown";
                        exceptionTypes[exceptionType] = exceptionTypes.GetValueOrDefault(exceptionType, 0) + 1;
                    }
                }

                if (exceptionMetrics.TotalExceptions > 0)
                {
                    var exceptionAnalysis = AnalyzeExceptions(exceptionMetrics, exceptionTypes);
                    results.Add(exceptionAnalysis);

                    // Check for critical exceptions
                    var criticalExceptions = CheckForCriticalExceptions(exceptionTypes);
                    if (criticalExceptions != null)
                        results.Add(criticalExceptions);
                }
                else
                {
                    results.Add(new AutomatedAnalysisResult
                    {
                        AnalysisCategory = AnalysisCategory.Exceptions,
                        Title = "Exception Analysis",
                        Severity = IssueSeverity.Low,
                        Description = "✅ No active exceptions detected in any threads",
                        Score = 1.0
                    });
                }

            }
            catch (Exception ex)
            {
                results.Add(new AutomatedAnalysisResult
                {
                    AnalysisCategory = AnalysisCategory.Exceptions,
                    Title = "Exception Analysis Error",
                    Severity = IssueSeverity.Medium,
                    Description = $"Error during exception analysis: {ex.Message}"
                });
            }

            return results;
        }

        private List<AutomatedAnalysisResult> PerformResourceAnalysis(CancellationToken token)
        {
            var results = new List<AutomatedAnalysisResult>();
            var runtime = DebuggerSession.Instance.Runtime;

            try
            {
                var resourceMetrics = new ResourceMetrics();

                // Analyze AppDomains
                resourceMetrics.AppDomainCount = runtime.AppDomains.Length;

                // Analyze modules and assemblies
                foreach (var appDomain in runtime.AppDomains)
                {
                    resourceMetrics.TotalModules += appDomain.Modules.Length;

                    foreach (var module in appDomain.Modules)
                    {
                        resourceMetrics.TotalAssemblySize += module.Size;

                        if (module.IsDynamic)
                            resourceMetrics.DynamicModules++;
                    }
                }

                // Analyze resource health
                var resourceHealth = AnalyzeResourceHealth(resourceMetrics);
                results.Add(resourceHealth);

                // Check for resource leaks
                var resourceLeaks = CheckForResourceLeaks(resourceMetrics);
                if (resourceLeaks != null)
                    results.Add(resourceLeaks);

            }
            catch (Exception ex)
            {
                results.Add(new AutomatedAnalysisResult
                {
                    AnalysisCategory = AnalysisCategory.Resources,
                    Title = "Resource Analysis Error",
                    Severity = IssueSeverity.Medium,
                    Description = $"Error during resource analysis: {ex.Message}"
                });
            }

            return results;
        }

        private List<AutomatedAnalysisResult> PerformIntelligentIssueDetection(CancellationToken token)
        {
            var results = new List<AutomatedAnalysisResult>();

            try
            {
                // Pattern-based issue detection
                var patternIssues = DetectCommonPatterns(token);
                results.AddRange(patternIssues);

                // Correlation analysis
                var correlationIssues = PerformCorrelationAnalysis(token);
                results.AddRange(correlationIssues);

                // Anomaly detection
                var anomalies = DetectAnomalies(token);
                results.AddRange(anomalies);

            }
            catch (Exception ex)
            {
                results.Add(new AutomatedAnalysisResult
                {
                    AnalysisCategory = AnalysisCategory.Intelligence,
                    Title = "Intelligent Issue Detection Error",
                    Severity = IssueSeverity.Medium,
                    Description = $"Error during intelligent issue detection: {ex.Message}"
                });
            }

            return results;
        }

        private List<AutomatedAnalysisResult> GenerateAutomatedRecommendations(List<AutomatedAnalysisResult?> analysisResults, CancellationToken token)
        {
            var recommendations = new List<AutomatedAnalysisResult>();

            try
            {
                // Prioritize issues by severity and impact
                var criticalIssues = analysisResults.Where(r => r?.Severity == IssueSeverity.Critical).ToList();
                var highIssues = analysisResults.Where(r => r?.Severity == IssueSeverity.High).ToList();

                if (criticalIssues.Any())
                {
                    recommendations.Add(new AutomatedAnalysisResult
                    {
                        AnalysisCategory = AnalysisCategory.Recommendations,
                        Title = "Critical Issues Detected",
                        Severity = IssueSeverity.Critical,
                        Description = $"🚨 {criticalIssues.Count} critical issues require immediate attention",
                        Recommendations = criticalIssues.SelectMany(i => i.Recommendations).Distinct().ToList(),
                        Priority = 1
                    });
                }

                if (highIssues.Any())
                {
                    recommendations.Add(new AutomatedAnalysisResult
                    {
                        AnalysisCategory = AnalysisCategory.Recommendations,
                        Title = "High Priority Issues",
                        Severity = IssueSeverity.High,
                        Description = $"⚠️ {highIssues.Count} high priority issues identified",
                        Recommendations = highIssues.SelectMany(i => i.Recommendations).Distinct().ToList(),
                        Priority = 2
                    });
                }

                // Generate proactive recommendations
                var proactiveRecommendations = GenerateProactiveRecommendations(analysisResults);
                recommendations.AddRange(proactiveRecommendations);

            }
            catch (Exception ex)
            {
                recommendations.Add(new AutomatedAnalysisResult
                {
                    AnalysisCategory = AnalysisCategory.Recommendations,
                    Title = "Recommendation Generation Error",
                    Severity = IssueSeverity.Medium,
                    Description = $"Error generating recommendations: {ex.Message}"
                });
            }

            return recommendations;
        }

        private AutomatedAnalysisResult CreateExecutiveSummary(List<AutomatedAnalysisResult> analysisResults)
        {
            var summary = new StringBuilder();
            summary.AppendLine("AUTOMATED ANALYSIS EXECUTIVE SUMMARY");
            summary.AppendLine("===================================");
            summary.AppendLine();

            // Overall health score
            var healthScore = CalculateOverallHealthScore(analysisResults);
            summary.AppendLine($"Overall Health Score: {healthScore:F1}/10.0 {GetHealthIndicator(healthScore)}");
            summary.AppendLine();

            // Issue summary
            var issuesByCategory = analysisResults
                .Where(r => r.Severity != IssueSeverity.Low)
                .GroupBy(r => r.AnalysisCategory)
                .ToDictionary(g => g.Key, g => g.Count());

            if (issuesByCategory.Any())
            {
                summary.AppendLine("Issues by Category:");
                foreach (var kvp in issuesByCategory.OrderByDescending(kvp => kvp.Value))
                {
                    summary.AppendLine($"  {kvp.Key}: {kvp.Value} issues");
                }
                summary.AppendLine();
            }

            // Top recommendations
            var topRecommendations = analysisResults
                .Where(r => r.AnalysisCategory == AnalysisCategory.Recommendations)
                .OrderBy(r => r.Priority)
                .Take(5);

            if (topRecommendations.Any())
            {
                summary.AppendLine("Top Recommendations:");
                foreach (var rec in topRecommendations)
                {
                    summary.AppendLine($"  • {rec.Title}");
                }
                summary.AppendLine();
            }

            // Analysis timestamp
            summary.AppendLine($"Analysis completed: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

            return new AutomatedAnalysisResult
            {
                AnalysisCategory = AnalysisCategory.Summary,
                Title = "Executive Summary",
                Severity = GetOverallSeverity(analysisResults),
                Description = summary.ToString(),
                Score = healthScore,
                Priority = 0
            };
        }

        // Helper methods for analysis
        private AutomatedAnalysisResult AnalyzeMemoryHealth(MemoryAnalysisMetrics metrics, Dictionary<string, TypeMetrics> typeDistribution, int[] generationStats)
        {
            var severity = IssueSeverity.Low;
            var issues = new List<string>();
            var recommendations = new List<string>();
            var score = 10.0;

            // Check total memory usage
            if (metrics.TotalSize > 2_000_000_000) // 2GB
            {
                severity = IssueSeverity.Critical;
                issues.Add("Very high memory usage (>2GB)");
                recommendations.Add("Investigate memory leaks immediately");
                score -= 4.0;
            }
            else if (metrics.TotalSize > 500_000_000) // 500MB
            {
                severity = IssueSeverity.High;
                issues.Add("High memory usage (>500MB)");
                recommendations.Add("Monitor memory usage patterns");
                score -= 2.0;
            }

            // Check large object heap
            if (metrics.LargeObjectCount > 1000)
            {
                if (severity < IssueSeverity.Medium) severity = IssueSeverity.Medium;
                issues.Add($"High number of large objects ({metrics.LargeObjectCount})");
                recommendations.Add("Review large object allocations");
                score -= 1.0;
            }

            // Check generation 2 objects
            var gen2Percentage = generationStats.Length > 2 ? (double)generationStats[2] / metrics.TotalObjects * 100 : 0;
            if (gen2Percentage > 50)
            {
                if (severity < IssueSeverity.Medium) severity = IssueSeverity.Medium;
                issues.Add($"High Gen 2 object percentage ({gen2Percentage:F1}%)");
                recommendations.Add("Investigate objects surviving garbage collection");
                score -= 1.5;
            }

            var description = new StringBuilder();
            description.AppendLine($"Memory Health Analysis:");
            description.AppendLine($"  Total Objects: {metrics.TotalObjects:N0}");
            description.AppendLine($"  Total Memory: {OperationHelpers.FormatSize(metrics.TotalSize)}");
            description.AppendLine($"  Large Objects: {metrics.LargeObjectCount:N0}");
            description.AppendLine($"  Gen 2 Objects: {gen2Percentage:F1}%");

            if (issues.Any())
            {
                description.AppendLine();
                description.AppendLine("Issues Detected:");
                issues.ForEach(issue => description.AppendLine($"  ⚠️ {issue}"));
            }

            return new AutomatedAnalysisResult
            {
                AnalysisCategory = AnalysisCategory.Memory,
                Title = "Memory Health Assessment",
                Severity = severity,
                Description = description.ToString(),
                Score = Math.Max(0, score),
                Recommendations = recommendations
            };
        }

        private List<AutomatedAnalysisResult> IdentifyMemoryIssues(Dictionary<string, TypeMetrics> typeDistribution, int[] generationStats)
        {
            var issues = new List<AutomatedAnalysisResult>();

            // Identify types with excessive instances
            var excessiveTypes = typeDistribution.Values
                .Where(t => t.InstanceCount > 100000)
                .OrderByDescending(t => t.InstanceCount)
                .Take(5);

            foreach (var type in excessiveTypes)
            {
                issues.Add(new AutomatedAnalysisResult
                {
                    AnalysisCategory = AnalysisCategory.Memory,
                    Title = $"Excessive Instances: {type.TypeName}",
                    Severity = IssueSeverity.Medium,
                    Description = $"Type has {type.InstanceCount:N0} instances consuming {OperationHelpers.FormatSize(type.TotalSize)}",
                    Recommendations = new List<string>
                    {
                        $"Investigate {type.TypeName} allocation patterns",
                        "Check for memory leaks or excessive object creation",
                        "Consider object pooling if appropriate"
                    }
                });
            }

            return issues;
        }

        // Additional helper methods would be implemented here...
        // (Methods like AnalyzeThreadingHealth, AssessDeadlockRisk, etc.)

        private double CalculateOverallHealthScore(List<AutomatedAnalysisResult> results)
        {
            var scores = results.Where(r => r.Score.HasValue).Select(r => r.Score.Value);
            return scores.Any() ? scores.Average() : 5.0;
        }

        private string GetHealthIndicator(double score)
        {
            return score switch
            {
                >= 8.0 => "🟢 Excellent",
                >= 6.0 => "🟡 Good",
                >= 4.0 => "🟠 Fair",
                >= 2.0 => "🔴 Poor",
                _ => "🚨 Critical"
            };
        }

        private IssueSeverity GetOverallSeverity(List<AutomatedAnalysisResult> results)
        {
            if (results.Any(r => r.Severity == IssueSeverity.Critical))
                return IssueSeverity.Critical;
            if (results.Any(r => r.Severity == IssueSeverity.High))
                return IssueSeverity.High;
            if (results.Any(r => r.Severity == IssueSeverity.Medium))
                return IssueSeverity.Medium;
            return IssueSeverity.Low;
        }

        // Placeholder implementations for remaining analysis methods
        private AutomatedAnalysisResult? CheckMemoryPressureIndicators(MemoryAnalysisMetrics metrics, int[] generationStats) => null;
        private AutomatedAnalysisResult? AnalyzeThreadingHealth(ThreadingMetrics metrics, List<ThreadInfo> blockedThreads) => null;
        private AutomatedAnalysisResult? AssessDeadlockRisk(List<ThreadInfo> blockedThreads, ThreadingMetrics metrics) => null;
        private AutomatedAnalysisResult? AnalyzeThreadPoolUsage(ThreadingMetrics metrics) => null;
        private AutomatedAnalysisResult? AnalyzePerformanceHealth(PerformanceMetrics metrics) => null;
        private AutomatedAnalysisResult? CheckCompilationIssues(PerformanceMetrics metrics) => null;
        private AutomatedAnalysisResult? AnalyzeExceptions(ExceptionMetrics metrics, Dictionary<string, int> types) => null;
        private AutomatedAnalysisResult? CheckForCriticalExceptions(Dictionary<string, int> types) => null;
        private AutomatedAnalysisResult? AnalyzeResourceHealth(ResourceMetrics metrics) => null;
        private AutomatedAnalysisResult? CheckForResourceLeaks(ResourceMetrics metrics) => null;
        private List<AutomatedAnalysisResult> DetectCommonPatterns(CancellationToken token) => new();
        private List<AutomatedAnalysisResult> PerformCorrelationAnalysis(CancellationToken token) => new();
        private List<AutomatedAnalysisResult> DetectAnomalies(CancellationToken token) => new();
        private List<AutomatedAnalysisResult> GenerateProactiveRecommendations(List<AutomatedAnalysisResult> results) => new();

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new StringBuilder();
            insights.AppendLine($"Automated Analysis: {operationResults.Count} analysis results");

            var results = operationResults.Cast<AutomatedAnalysisResult>().ToList();

            // Executive summary
            var summary = results.FirstOrDefault(r => r.AnalysisCategory == AnalysisCategory.Summary);
            if (summary != null)
            {
                insights.AppendLine($"\n🎯 HEALTH SCORE: {summary.Score:F1}/10.0 {GetHealthIndicator(summary.Score.Value)}");
            }

            // Critical issues
            var criticalIssues = results.Where(r => r.Severity == IssueSeverity.Critical).ToList();
            if (criticalIssues.Any())
            {
                insights.AppendLine($"\n🚨 CRITICAL ISSUES ({criticalIssues.Count}):");
                foreach (var issue in criticalIssues.Take(3))
                {
                    insights.AppendLine($"  - {issue.Title}");
                }
            }

            // Category breakdown
            var categories = results.GroupBy(r => r.AnalysisCategory).ToDictionary(g => g.Key, g => g.Count());
            insights.AppendLine("\n📊 ANALYSIS BREAKDOWN:");
            foreach (var kvp in categories.OrderByDescending(kvp => kvp.Value))
            {
                insights.AppendLine($"  {kvp.Key}: {kvp.Value} findings");
            }

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
AUTOMATED ANALYSIS SPECIALIZATION:
You are analyzing comprehensive automated analysis results from a state-of-the-art memory dump analyzer.

ANALYSIS CATEGORIES:
- Memory: Memory usage patterns, leaks, and optimization opportunities
- Threading: Thread states, deadlocks, and synchronization issues  
- Performance: JIT compilation, method optimization, and performance bottlenecks
- Exceptions: Exception patterns and stability issues
- Resources: Resource utilization and potential leaks
- Intelligence: Pattern recognition and anomaly detection
- Recommendations: Prioritized action items and optimizations
- Summary: Executive overview and health scoring

HEALTH SCORING SYSTEM:
- 8.0-10.0: Excellent health, minimal issues
- 6.0-7.9: Good health, minor optimizations possible
- 4.0-5.9: Fair health, attention needed
- 2.0-3.9: Poor health, significant issues
- 0.0-1.9: Critical health, immediate action required

AUTOMATED INVESTIGATION WORKFLOW:
1. Analyze overall health score and severity distribution
2. Prioritize critical and high-severity issues
3. Identify correlations between different issue categories
4. Provide specific, actionable remediation steps
5. Suggest proactive monitoring and prevention strategies

When critical issues are detected, immediately recommend specific operations for deeper analysis.
Always provide clear, prioritized action plans based on automated findings.
";
        }
    }

    // Supporting classes and enums
    public class AutomatedAnalysisResult
    {
        public AnalysisCategory AnalysisCategory { get; set; }
        public string Title { get; set; }
        public IssueSeverity Severity { get; set; }
        public string Description { get; set; }
        public List<string> Recommendations { get; set; } = new();
        public double? Score { get; set; }
        public int Priority { get; set; } = int.MaxValue;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public enum AnalysisCategory
    {
        Summary,
        Memory,
        Threading,
        Performance,
        Exceptions,
        Resources,
        Intelligence,
        Recommendations,
        Error
    }

    public enum IssueSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    // Metrics classes
    public class MemoryAnalysisMetrics
    {
        public int TotalObjects { get; set; }
        public long TotalSize { get; set; }
        public int LargeObjectCount { get; set; }
    }

    public class TypeMetrics
    {
        public int InstanceCount { get; set; }
        public long TotalSize { get; set; }
        public int LargeObjectCount { get; set; }
        public string TypeName { get; set; }
    }

    public class ThreadingMetrics
    {
        public int TotalThreads { get; set; }
        public int ForegroundThreads { get; set; }
        public int BackgroundThreads { get; set; }
        public int BlockedThreads { get; set; }
        public int DeadThreads { get; set; }
        public int ThreadsWithExceptions { get; set; }
    }

    public class ThreadInfo
    {
        public int ManagedThreadId { get; set; }
        public uint OSThreadId { get; set; }
        public bool IsAlive { get; set; }
        public int BlockingObjectCount { get; set; }
    }

    public class PerformanceMetrics
    {
        public int TotalModules { get; set; }
        public int OptimizedModules { get; set; }
        public int TotalMethods { get; set; }
        public int CompiledMethods { get; set; }
        public int GenericMethods { get; set; }
        public int LargeMethods { get; set; }
        public uint TotalCodeSize { get; set; }
    }

    public class ExceptionMetrics
    {
        public int TotalExceptions { get; set; }
    }

    public class ResourceMetrics
    {
        public int AppDomainCount { get; set; }
        public int TotalModules { get; set; }
        public int DynamicModules { get; set; }
        public ulong TotalAssemblySize { get; set; }
    }
}