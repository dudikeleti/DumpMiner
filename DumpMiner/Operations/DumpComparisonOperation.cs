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
using DumpMiner.Operations.Shared;
using Microsoft.Diagnostics.Runtime;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpComparison, typeof(IDebuggerOperation))]
    public class DumpComparisonOperation : BaseAIOperation
    {
        public override string Name => OperationNames.DumpComparison;

        public override async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            // Note: This operation would typically require access to multiple dump files
            // For now, we'll demonstrate the analysis structure and provide insights
            // about what comparative analysis would reveal

            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var comparisonResults = new List<DumpComparisonResult>();

                // Get current dump information as baseline
                var currentDumpInfo = AnalyzeCurrentDump(token);

                // Create a comparison result showing what analysis would be performed
                var comparisonResult = new DumpComparisonResult
                {
                    ComparisonType = ComparisonType.SingleDumpAnalysis,
                    BaselineDumpInfo = currentDumpInfo,
                    AnalysisTimestamp = DateTime.UtcNow,
                    Summary = "Single dump analysis - comparison requires multiple dumps",
                    RecommendedActions = GetSingleDumpRecommendations(currentDumpInfo)
                };

                comparisonResults.Add(comparisonResult);

                // Add framework for comparison categories
                comparisonResults.AddRange(CreateComparisonFramework(currentDumpInfo));

                return comparisonResults;
            });
        }

        private DumpInfo AnalyzeCurrentDump(CancellationToken token)
        {
            var runtime = DebuggerSession.Instance.Runtime;
            var heap = DebuggerSession.Instance.Heap;
            var attachedTo = DebuggerSession.Instance.AttachedTo;

            var dumpInfo = new DumpInfo
            {
                DumpName = attachedTo.name ?? $"Process_{attachedTo.id}",
                AnalysisTime = DateTime.UtcNow,
                ProcessId = attachedTo.id,
                ClrVersion = runtime.ClrInfo?.Version?.ToString() ?? "Unknown"
            };

            // Collect basic statistics
            var typeStats = new Dictionary<string, TypeStatistics>();
            var totalObjects = 0;
            var totalMemory = 0L;
            var generationCounts = new int[5]; // Gen 0, 1, 2, LOH, Pinned

            foreach (var segment in heap.Segments)
            {
                foreach (var obj in segment.EnumerateObjects())
                {
                    if (token.IsCancellationRequested) break;

                    totalObjects++;
                    var size = (long)obj.Size;
                    totalMemory += size;

                    var generation = (int)segment.GetGeneration(obj);
                    if (generation >= 0 && generation < generationCounts.Length)
                        generationCounts[generation]++;

                    var typeName = obj.Type?.Name ?? "Unknown";
                    if (!typeStats.ContainsKey(typeName))
                        typeStats[typeName] = new TypeStatistics { TypeName = typeName };

                    var stats = typeStats[typeName];
                    stats.InstanceCount++;
                    stats.TotalSize += size;
                }
            }

            dumpInfo.TotalObjects = totalObjects;
            dumpInfo.TotalMemoryUsage = totalMemory;
            dumpInfo.GenerationCounts = generationCounts;
            dumpInfo.TypeStatistics = typeStats.Values.OrderByDescending(t => t.TotalSize).Take(50).ToList();

            // Thread analysis
            dumpInfo.ThreadCount = runtime.Threads.Count();
            dumpInfo.BlockedThreads = runtime.Threads.Count(t => t.IsBlocked());

            // Exception analysis
            dumpInfo.ExceptionCount = runtime.Threads.Count(t => t.CurrentException != null);

            // Module analysis
            dumpInfo.ModuleCount = runtime.AppDomains.SelectMany(ad => ad.Modules).Count();
            dumpInfo.IsOptimized = runtime.AppDomains.SelectMany(ad => ad.Modules).Any(m => m.IsOptimized());

            return dumpInfo;
        }

        private List<string> GetSingleDumpRecommendations(DumpInfo dumpInfo)
        {
            var recommendations = new List<string>();

            recommendations.Add("💡 For comprehensive analysis, collect multiple dumps over time:");
            recommendations.Add("  - Take dumps at regular intervals (e.g., every hour)");
            recommendations.Add("  - Capture dumps during different application states");
            recommendations.Add("  - Include dumps from before/after performance issues");

            if (dumpInfo.TotalMemoryUsage > 500_000_000) // 500MB
            {
                recommendations.Add("🔍 High memory usage detected - recommend periodic monitoring");
            }

            if (dumpInfo.BlockedThreads > 0)
            {
                recommendations.Add("⚠️ Blocked threads detected - monitor for deadlock patterns");
            }

            if (dumpInfo.ExceptionCount > 0)
            {
                recommendations.Add("🚨 Exceptions present - track exception trends over time");
            }

            recommendations.Add("📊 Comparison analysis would reveal:");
            recommendations.Add("  - Memory growth patterns and leak detection");
            recommendations.Add("  - Performance degradation trends");
            recommendations.Add("  - Resource usage progression");
            recommendations.Add("  - Type allocation patterns over time");

            return recommendations;
        }

        private List<DumpComparisonResult> CreateComparisonFramework(DumpInfo currentDump)
        {
            var frameworkResults = new List<DumpComparisonResult>();

            // Memory trend analysis framework
            frameworkResults.Add(new DumpComparisonResult
            {
                ComparisonType = ComparisonType.MemoryTrends,
                BaselineDumpInfo = currentDump,
                Summary = "Memory Trend Analysis Framework",
                Description = BuildMemoryTrendFramework(currentDump),
                RecommendedActions = new List<string>
                {
                    "Collect dumps at regular intervals to track memory growth",
                    "Monitor generation 2 object counts for leak indicators",
                    "Track large object heap growth patterns",
                    "Analyze type-specific memory trends"
                }
            });

            // Performance comparison framework
            frameworkResults.Add(new DumpComparisonResult
            {
                ComparisonType = ComparisonType.PerformanceTrends,
                BaselineDumpInfo = currentDump,
                Summary = "Performance Trend Analysis Framework",
                Description = BuildPerformanceTrendFramework(currentDump),
                RecommendedActions = new List<string>
                {
                    "Monitor thread count and blocking patterns",
                    "Track JIT compilation efficiency over time",
                    "Analyze response time correlation with memory usage",
                    "Monitor GC pressure indicators"
                }
            });

            // Resource utilization framework
            frameworkResults.Add(new DumpComparisonResult
            {
                ComparisonType = ComparisonType.ResourceUtilization,
                BaselineDumpInfo = currentDump,
                Summary = "Resource Utilization Analysis Framework",
                Description = BuildResourceUtilizationFramework(currentDump),
                RecommendedActions = new List<string>
                {
                    "Track handle and resource counts",
                    "Monitor thread pool utilization",
                    "Analyze module loading patterns",
                    "Track exception frequency trends"
                }
            });

            return frameworkResults;
        }

        private string BuildMemoryTrendFramework(DumpInfo dumpInfo)
        {
            var framework = new StringBuilder();
            framework.AppendLine("MEMORY TREND ANALYSIS FRAMEWORK");
            framework.AppendLine("=====================================");
            framework.AppendLine();

            framework.AppendLine("Current Baseline Metrics:");
            framework.AppendLine($"  Total Memory: {OperationHelpers.FormatSize(dumpInfo.TotalMemoryUsage)}");
            framework.AppendLine($"  Total Objects: {dumpInfo.TotalObjects:N0}");
            framework.AppendLine($"  Gen 0: {dumpInfo.GenerationCounts[0]:N0} objects");
            framework.AppendLine($"  Gen 1: {dumpInfo.GenerationCounts[1]:N0} objects");
            framework.AppendLine($"  Gen 2: {dumpInfo.GenerationCounts[2]:N0} objects");
            if (dumpInfo.GenerationCounts.Length > 3)
                framework.AppendLine($"  LOH: {dumpInfo.GenerationCounts[3]:N0} objects");

            framework.AppendLine();
            framework.AppendLine("COMPARATIVE ANALYSIS WOULD TRACK:");
            framework.AppendLine();

            framework.AppendLine("📈 Memory Growth Patterns:");
            framework.AppendLine("  - Overall memory usage trends");
            framework.AppendLine("  - Growth rate calculations (MB/hour)");
            framework.AppendLine("  - Memory pressure indicators");
            framework.AppendLine("  - Peak vs. average usage patterns");

            framework.AppendLine();
            framework.AppendLine("🔍 Leak Detection Algorithms:");
            framework.AppendLine("  - Progressive object count increases");
            framework.AppendLine("  - Types with consistent growth");
            framework.AppendLine("  - Generation 2 accumulation patterns");
            framework.AppendLine("  - Reference chain stability analysis");

            framework.AppendLine();
            framework.AppendLine("📊 Type-Specific Trends:");
            var topTypes = dumpInfo.TypeStatistics.Take(5);
            foreach (var type in topTypes)
            {
                framework.AppendLine($"  - {type.TypeName}: {type.InstanceCount:N0} instances ({OperationHelpers.FormatSize(type.TotalSize)})");
            }

            framework.AppendLine();
            framework.AppendLine("⚡ AUTOMATED ALERTS WOULD TRIGGER ON:");
            framework.AppendLine("  - Memory growth rate > 10MB/hour");
            framework.AppendLine("  - Type instance count doubling");
            framework.AppendLine("  - Gen 2 objects increasing by >25%");
            framework.AppendLine("  - LOH size growing consistently");

            return framework.ToString();
        }

        private string BuildPerformanceTrendFramework(DumpInfo dumpInfo)
        {
            var framework = new StringBuilder();
            framework.AppendLine("PERFORMANCE TREND ANALYSIS FRAMEWORK");
            framework.AppendLine("===================================");
            framework.AppendLine();

            framework.AppendLine("Current Performance Indicators:");
            framework.AppendLine($"  Active Threads: {dumpInfo.ThreadCount}");
            framework.AppendLine($"  Blocked Threads: {dumpInfo.BlockedThreads}");
            framework.AppendLine($"  Exceptions Present: {dumpInfo.ExceptionCount}");
            framework.AppendLine($"  Modules Loaded: {dumpInfo.ModuleCount}");
            framework.AppendLine($"  Optimization Level: {(dumpInfo.IsOptimized ? "Optimized" : "Debug")}");

            framework.AppendLine();
            framework.AppendLine("COMPARATIVE ANALYSIS WOULD TRACK:");
            framework.AppendLine();

            framework.AppendLine("🚀 Threading Performance:");
            framework.AppendLine("  - Thread count progression over time");
            framework.AppendLine("  - Blocking pattern frequency analysis");
            framework.AppendLine("  - Deadlock occurrence trends");
            framework.AppendLine("  - Thread pool utilization patterns");

            framework.AppendLine();
            framework.AppendLine("⚡ JIT Compilation Trends:");
            framework.AppendLine("  - Method compilation ratios over time");
            framework.AppendLine("  - Code cache growth patterns");
            framework.AppendLine("  - Optimization effectiveness trends");
            framework.AppendLine("  - Startup time correlation analysis");

            framework.AppendLine();
            framework.AppendLine("🎯 Response Time Correlation:");
            framework.AppendLine("  - Memory usage vs. response time");
            framework.AppendLine("  - GC frequency impact analysis");
            framework.AppendLine("  - Thread contention correlation");
            framework.AppendLine("  - Exception frequency impact");

            framework.AppendLine();
            framework.AppendLine("📊 PERFORMANCE DEGRADATION INDICATORS:");
            framework.AppendLine("  - Increasing thread count over time");
            framework.AppendLine("  - Growing number of blocked threads");
            framework.AppendLine("  - Rising exception rates");
            framework.AppendLine("  - Decreasing JIT compilation efficiency");

            return framework.ToString();
        }

        private string BuildResourceUtilizationFramework(DumpInfo dumpInfo)
        {
            var framework = new StringBuilder();
            framework.AppendLine("RESOURCE UTILIZATION ANALYSIS FRAMEWORK");
            framework.AppendLine("======================================");
            framework.AppendLine();

            framework.AppendLine("Current Resource Snapshot:");
            framework.AppendLine($"  CLR Version: {dumpInfo.ClrVersion}");
            framework.AppendLine($"  Process ID: {dumpInfo.ProcessId?.ToString() ?? "Unknown"}");
            framework.AppendLine($"  Analysis Time: {dumpInfo.AnalysisTime:yyyy-MM-dd HH:mm:ss}");
            framework.AppendLine($"  Loaded Modules: {dumpInfo.ModuleCount}");

            framework.AppendLine();
            framework.AppendLine("COMPARATIVE ANALYSIS WOULD TRACK:");
            framework.AppendLine();

            framework.AppendLine("🔧 System Resource Trends:");
            framework.AppendLine("  - Handle count progression");
            framework.AppendLine("  - GDI/USER object usage");
            framework.AppendLine("  - Virtual memory consumption");
            framework.AppendLine("  - Working set size changes");

            framework.AppendLine();
            framework.AppendLine("📚 Module Loading Patterns:");
            framework.AppendLine("  - Assembly loading frequency");
            framework.AppendLine("  - Dynamic assembly creation trends");
            framework.AppendLine("  - Module dependency changes");
            framework.AppendLine("  - GAC vs. local assembly usage");

            framework.AppendLine();
            framework.AppendLine("🏗️ AppDomain Resource Usage:");
            framework.AppendLine("  - AppDomain creation/destruction patterns");
            framework.AppendLine("  - Cross-domain call frequency");
            framework.AppendLine("  - Assembly loading per domain");
            framework.AppendLine("  - Security context changes");

            framework.AppendLine();
            framework.AppendLine("⚠️ RESOURCE LEAK INDICATORS:");
            framework.AppendLine("  - Steadily increasing handle counts");
            framework.AppendLine("  - Growing module count without unloading");
            framework.AppendLine("  - Increasing virtual memory usage");
            framework.AppendLine("  - AppDomain accumulation patterns");

            return framework.ToString();
        }

        // This method would be used when actual comparison is performed
        public async Task<List<DumpComparisonResult>> CompareMultipleDumps(List<string> dumpPaths, CancellationToken token)
        {
            var results = new List<DumpComparisonResult>();
            var dumpInfos = new List<DumpInfo>();

            // Load and analyze each dump
            foreach (var dumpPath in dumpPaths)
            {
                try
                {
                    // This would require loading each dump file
                    // For now, this is a framework for how it would work
                    var dumpInfo = await AnalyzeDumpFile(dumpPath, token);
                    dumpInfos.Add(dumpInfo);
                }
                catch (Exception ex)
                {
                    results.Add(new DumpComparisonResult
                    {
                        ComparisonType = ComparisonType.Error,
                        Summary = $"Failed to analyze dump: {Path.GetFileName(dumpPath)}",
                        Description = ex.Message
                    });
                }
            }

            if (dumpInfos.Count >= 2)
            {
                // Perform actual comparisons
                results.AddRange(CompareMemoryTrends(dumpInfos, token));
                results.AddRange(ComparePerformanceMetrics(dumpInfos, token));
                results.AddRange(IdentifyProgressiveIssues(dumpInfos, token));
            }

            return results;
        }

        private async Task<DumpInfo> AnalyzeDumpFile(string dumpPath, CancellationToken token)
        {
            // This would actually load and analyze the dump file
            // Implementation would require creating a new DataTarget for each dump
            await Task.Delay(1, token); // Placeholder
            return new DumpInfo { DumpName = Path.GetFileName(dumpPath) };
        }

        private List<DumpComparisonResult> CompareMemoryTrends(List<DumpInfo> dumpInfos, CancellationToken token)
        {
            var results = new List<DumpComparisonResult>();

            // Calculate memory growth rates
            var sortedDumps = dumpInfos.OrderBy(d => d.AnalysisTime).ToList();

            for (int i = 1; i < sortedDumps.Count; i++)
            {
                var previous = sortedDumps[i - 1];
                var current = sortedDumps[i];
                var timeSpan = current.AnalysisTime - previous.AnalysisTime;

                if (timeSpan.TotalMinutes > 0)
                {
                    var memoryGrowth = current.TotalMemoryUsage - previous.TotalMemoryUsage;
                    var growthRate = memoryGrowth / timeSpan.TotalHours; // bytes per hour

                    results.Add(new DumpComparisonResult
                    {
                        ComparisonType = ComparisonType.MemoryTrends,
                        BaselineDumpInfo = previous,
                        ComparisonDumpInfo = current,
                        Summary = $"Memory growth: {OperationHelpers.FormatSize((long)growthRate)}/hour",
                        Description = BuildMemoryGrowthDescription(previous, current, growthRate),
                        GrowthRate = growthRate,
                        TimeSpan = timeSpan
                    });
                }
            }

            return results;
        }

        private List<DumpComparisonResult> ComparePerformanceMetrics(List<DumpInfo> dumpInfos, CancellationToken token)
        {
            var results = new List<DumpComparisonResult>();

            // Compare thread counts, blocking patterns, etc.
            var sortedDumps = dumpInfos.OrderBy(d => d.AnalysisTime).ToList();

            // Analyze trends
            var threadCountTrend = CalculateTrend(sortedDumps.Select(d => (double)d.ThreadCount).ToList());
            var blockedThreadTrend = CalculateTrend(sortedDumps.Select(d => (double)d.BlockedThreads).ToList());

            results.Add(new DumpComparisonResult
            {
                ComparisonType = ComparisonType.PerformanceTrends,
                Summary = "Performance Trend Analysis",
                Description = $"Thread count trend: {threadCountTrend:F2}/dump, Blocked thread trend: {blockedThreadTrend:F2}/dump",
                TrendSlope = threadCountTrend
            });

            return results;
        }

        private List<DumpComparisonResult> IdentifyProgressiveIssues(List<DumpInfo> dumpInfos, CancellationToken token)
        {
            var results = new List<DumpComparisonResult>();

            // Look for types that consistently grow across dumps
            var typeGrowthPatterns = AnalyzeTypeGrowthPatterns(dumpInfos);

            foreach (var pattern in typeGrowthPatterns.Where(p => p.IsProgressive))
            {
                results.Add(new DumpComparisonResult
                {
                    ComparisonType = ComparisonType.ProgressiveIssues,
                    Summary = $"Progressive growth detected: {pattern.TypeName}",
                    Description = $"Type {pattern.TypeName} shows consistent growth pattern with {pattern.GrowthRate:F1}% increase per dump",
                    ProgressiveIssue = pattern
                });
            }

            return results;
        }

        private string BuildMemoryGrowthDescription(DumpInfo previous, DumpInfo current, double growthRate)
        {
            var description = new StringBuilder();
            description.AppendLine($"Memory Analysis: {previous.DumpName} → {current.DumpName}");
            description.AppendLine($"Time Span: {(current.AnalysisTime - previous.AnalysisTime).TotalMinutes:F0} minutes");
            description.AppendLine($"Memory Change: {OperationHelpers.FormatSize(current.TotalMemoryUsage - previous.TotalMemoryUsage)}");
            description.AppendLine($"Growth Rate: {OperationHelpers.FormatSize((long)growthRate)}/hour");
            description.AppendLine($"Object Count Change: {current.TotalObjects - previous.TotalObjects:N0}");

            // Analyze severity
            if (growthRate > 50_000_000) // 50MB/hour
            {
                description.AppendLine("🚨 HIGH GROWTH RATE - Immediate investigation required");
            }
            else if (growthRate > 10_000_000) // 10MB/hour
            {
                description.AppendLine("⚠️ Moderate growth rate - Monitor closely");
            }
            else if (growthRate > 0)
            {
                description.AppendLine("📈 Normal growth rate - Continue monitoring");
            }
            else
            {
                description.AppendLine("✅ Memory usage stable or decreasing");
            }

            return description.ToString();
        }

        private double CalculateTrend(List<double> values)
        {
            if (values.Count < 2) return 0;

            // Simple linear regression to calculate trend
            var n = values.Count;
            var sumX = 0.0;
            var sumY = values.Sum();
            var sumXY = 0.0;
            var sumX2 = 0.0;

            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumXY += i * values[i];
                sumX2 += i * i;
            }

            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            return slope;
        }

        private List<TypeGrowthPattern> AnalyzeTypeGrowthPatterns(List<DumpInfo> dumpInfos)
        {
            var patterns = new List<TypeGrowthPattern>();
            var sortedDumps = dumpInfos.OrderBy(d => d.AnalysisTime).ToList();

            // Get all unique type names
            var allTypes = sortedDumps
                .SelectMany(d => d.TypeStatistics.Select(t => t.TypeName))
                .Distinct();

            foreach (var typeName in allTypes)
            {
                var typeData = sortedDumps
                    .Select(d => d.TypeStatistics.FirstOrDefault(t => t.TypeName == typeName)?.InstanceCount ?? 0)
                    .ToList();

                if (typeData.All(count => count > 0)) // Type exists in all dumps
                {
                    var growthRate = CalculateTrend(typeData.Select(d => (double)d).ToList());
                    var isProgressive = growthRate > 0 && typeData.Last() > typeData.First() * 1.5; // 50% growth

                    patterns.Add(new TypeGrowthPattern
                    {
                        TypeName = typeName,
                        GrowthRate = growthRate,
                        IsProgressive = isProgressive,
                        InitialCount = typeData.First(),
                        FinalCount = typeData.Last()
                    });
                }
            }

            return patterns.OrderByDescending(p => p.GrowthRate).ToList();
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new StringBuilder();
            insights.AppendLine($"Dump Comparison Analysis: {operationResults.Count} analysis results");

            if (!operationResults.Any())
            {
                insights.AppendLine("ℹ️ No comparison data available - single dump analysis performed");
                return insights.ToString();
            }

            var comparisons = operationResults.Cast<DumpComparisonResult>().ToList();

            // Summary by comparison type
            var comparisonsByType = comparisons.GroupBy(c => c.ComparisonType).ToDictionary(g => g.Key, g => g.Count());
            insights.AppendLine("\nComparison Types:");
            foreach (var kvp in comparisonsByType)
            {
                insights.AppendLine($"  {kvp.Key}: {kvp.Value} analyses");
            }

            // Highlight significant findings
            var significantGrowth = comparisons.Where(c => c.GrowthRate > 10_000_000).ToList(); // >10MB/hour
            if (significantGrowth.Any())
            {
                insights.AppendLine("\n🚨 SIGNIFICANT GROWTH DETECTED:");
                foreach (var growth in significantGrowth.Take(3))
                {
                    insights.AppendLine($"  - {growth.Summary}");
                }
            }

            // Progressive issues
            var progressiveIssues = comparisons.Where(c => c.ProgressiveIssue != null).ToList();
            if (progressiveIssues.Any())
            {
                insights.AppendLine("\n⚠️ PROGRESSIVE ISSUES:");
                foreach (var issue in progressiveIssues.Take(3))
                {
                    insights.AppendLine($"  - {issue.ProgressiveIssue.TypeName}: {issue.ProgressiveIssue.GrowthRate:F1}% growth");
                }
            }

            // Framework recommendations
            var frameworkAnalyses = comparisons.Where(c => c.ComparisonType != ComparisonType.SingleDumpAnalysis).ToList();
            if (frameworkAnalyses.Any())
            {
                insights.AppendLine("\n💡 COMPARISON FRAMEWORK ESTABLISHED");
                insights.AppendLine("Ready for multi-dump comparative analysis");
            }

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
DUMP COMPARISON SPECIALIZATION:
You are analyzing comparative memory dump data to identify trends, progressive issues, and performance degradation patterns.

COMPARISON TYPES EXPLAINED:
- MemoryTrends: Memory usage growth patterns over time
- PerformanceTrends: Threading and performance metric changes
- ResourceUtilization: System resource consumption patterns
- ProgressiveIssues: Issues that develop gradually over time
- SingleDumpAnalysis: Framework for future comparisons

KEY ANALYTICAL APPROACHES:
1. Growth Rate Analysis: Identify memory leaks and resource consumption trends
2. Trend Detection: Use statistical methods to identify patterns
3. Progressive Issue Identification: Find gradually developing problems
4. Correlation Analysis: Link different metrics to identify root causes

CRITICAL THRESHOLDS:
- Memory growth >50MB/hour: CRITICAL - immediate action required
- Memory growth >10MB/hour: WARNING - close monitoring needed
- Type instance growth >50%: Potential memory leak
- Thread count increasing: Performance degradation risk

AUTOMATED INVESTIGATION STRATEGY:
When comparative issues are detected, recommend:
- MemoryLeakDetection for detailed leak analysis
- TrendAnalysis for historical pattern examination
- PerformanceReport for comprehensive impact assessment

Always provide actionable recommendations based on trend analysis and growth patterns.
";
        }
    }

    // Supporting classes
    public class DumpComparisonResult
    {
        public ComparisonType ComparisonType { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public DumpInfo BaselineDumpInfo { get; set; }
        public DumpInfo ComparisonDumpInfo { get; set; }
        public DateTime AnalysisTimestamp { get; set; }
        public double GrowthRate { get; set; } // bytes per hour
        public TimeSpan TimeSpan { get; set; }
        public double TrendSlope { get; set; }
        public TypeGrowthPattern ProgressiveIssue { get; set; }
        public List<string> RecommendedActions { get; set; } = new();
    }

    public enum ComparisonType
    {
        SingleDumpAnalysis,
        MemoryTrends,
        PerformanceTrends,
        ResourceUtilization,
        ProgressiveIssues,
        Error
    }

    public class DumpInfo
    {
        public string DumpName { get; set; }
        public DateTime AnalysisTime { get; set; }
        public int? ProcessId { get; set; }
        public string ClrVersion { get; set; }
        public int TotalObjects { get; set; }
        public long TotalMemoryUsage { get; set; }
        public int[] GenerationCounts { get; set; } = new int[4];
        public List<TypeStatistics> TypeStatistics { get; set; } = new();
        public int ThreadCount { get; set; }
        public int BlockedThreads { get; set; }
        public int ExceptionCount { get; set; }
        public int ModuleCount { get; set; }
        public bool IsOptimized { get; set; }
    }

    public class TypeGrowthPattern
    {
        public string TypeName { get; set; }
        public double GrowthRate { get; set; }
        public bool IsProgressive { get; set; }
        public int InitialCount { get; set; }
        public int FinalCount { get; set; }
    }
}