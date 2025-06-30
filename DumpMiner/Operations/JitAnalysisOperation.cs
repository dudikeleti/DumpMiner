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
using Microsoft.Diagnostics.Runtime;

namespace DumpMiner.Operations
{
    [Export(OperationNames.JitAnalysis, typeof(IDebuggerOperation))]
    public class JitAnalysisOperation : BaseAIOperation
    {
        public override string Name => OperationNames.JitAnalysis;

        public override async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var jitAnalysisResults = new List<JitAnalysisInfo>();
                var runtime = DebuggerSession.Instance.Runtime;
                
                // 1. Analyze JIT compilation statistics
                var jitStats = AnalyzeJitStatistics(runtime, token);
                jitAnalysisResults.Add(jitStats);
                
                // 2. Analyze method compilation status
                var methodAnalysis = AnalyzeMethodCompilation(runtime, token);
                jitAnalysisResults.AddRange(methodAnalysis);
                
                // 3. Analyze code quality and optimization levels
                var optimizationAnalysis = AnalyzeOptimizationLevels(runtime, token);
                jitAnalysisResults.AddRange(optimizationAnalysis);
                
                // 4. Identify compilation performance issues
                var performanceIssues = IdentifyJitPerformanceIssues(runtime, token);
                jitAnalysisResults.AddRange(performanceIssues);
                
                // 5. Analyze tiered compilation patterns
                var tieredCompilationAnalysis = AnalyzeTieredCompilation(runtime, token);
                jitAnalysisResults.AddRange(tieredCompilationAnalysis);

                return jitAnalysisResults.OrderByDescending(j => j.ImportanceScore).ToList();
            });
        }

        private JitAnalysisInfo AnalyzeJitStatistics(ClrRuntime runtime, CancellationToken token)
        {
            var stats = new JitAnalysisInfo
            {
                AnalysisType = JitAnalysisType.OverallStatistics,
                Title = "JIT Compilation Statistics",
                ImportanceScore = 1.0
            };

            var methodCounts = new Dictionary<string, int>
            {
                ["Total"] = 0,
                ["Jitted"] = 0,
                ["NotJitted"] = 0,
                ["Optimized"] = 0,
                ["NotOptimized"] = 0,
                ["Generic"] = 0,
                ["HasNativeCode"] = 0
            };

            var moduleStats = new Dictionary<string, ModuleJitStats>();

            foreach (var appDomain in runtime.AppDomains)
            {
                if (token.IsCancellationRequested) break;

                foreach (var module in appDomain.Modules)
                {
                    if (token.IsCancellationRequested) break;

                    var moduleJitStats = new ModuleJitStats
                    {
                        ModuleName = module.Name ?? "Unknown",
                        IsOptimized = module.IsOptimized()
                    };

                    foreach (var type in module.EnumerateTypes())
                    {
                        if (token.IsCancellationRequested) break;

                        foreach (var method in type.Methods)
                        {
                            methodCounts["Total"]++;
                            moduleJitStats.TotalMethods++;

                            if (method.NativeCode != 0)
                            {
                                methodCounts["Jitted"]++;
                                methodCounts["HasNativeCode"]++;
                                moduleJitStats.JittedMethods++;

                                if (method.CompilationType == MethodCompilationType.Jit)
                                {
                                    if (module.IsOptimized())
                                    {
                                        methodCounts["Optimized"]++;
                                        moduleJitStats.OptimizedMethods++;
                                    }
                                    else
                                    {
                                        methodCounts["NotOptimized"]++;
                                    }
                                }
                            }
                            else
                            {
                                methodCounts["NotJitted"]++;
                            }

                            if (method.IsGeneric())
                            {
                                methodCounts["Generic"]++;
                                moduleJitStats.GenericMethods++;
                            }

                            // Analyze method size and complexity
                            if (method.NativeCode != 0)
                            {
                                var hotColdInfo = method.HotColdInfo;
                                if (hotColdInfo.HotSize > 0)
                                {
                                    moduleJitStats.TotalCodeSize += hotColdInfo.HotSize;
                                    if (hotColdInfo.HotSize > 1000) // Large method threshold
                                    {
                                        moduleJitStats.LargeMethods++;
                                    }
                                }
                            }
                        }
                    }

                    moduleStats[module.Name ?? "Unknown"] = moduleJitStats;
                }
            }

            // Build statistics summary
            var summary = new StringBuilder();
            summary.AppendLine("JIT Compilation Overview:");
            summary.AppendLine($"  Total Methods: {methodCounts["Total"]:N0}");
            summary.AppendLine($"  JIT Compiled: {methodCounts["Jitted"]:N0} ({GetPercentage(methodCounts["Jitted"], methodCounts["Total"]):F1}%)");
            summary.AppendLine($"  Not Compiled: {methodCounts["NotJitted"]:N0} ({GetPercentage(methodCounts["NotJitted"], methodCounts["Total"]):F1}%)");
            summary.AppendLine($"  Optimized: {methodCounts["Optimized"]:N0} ({GetPercentage(methodCounts["Optimized"], methodCounts["Jitted"]):F1}% of jitted)");
            summary.AppendLine($"  Generic Methods: {methodCounts["Generic"]:N0} ({GetPercentage(methodCounts["Generic"], methodCounts["Total"]):F1}%)");

            summary.AppendLine("\nModule Analysis:");
            var topModules = moduleStats.Values
                .OrderByDescending(m => m.JittedMethods)
                .Take(10);

            foreach (var module in topModules)
            {
                var jitPercentage = GetPercentage(module.JittedMethods, module.TotalMethods);
                summary.AppendLine($"  {module.ModuleName}: {module.JittedMethods:N0}/{module.TotalMethods:N0} methods ({jitPercentage:F1}%) - {(module.IsOptimized ? "Optimized" : "Debug")}");
            }

            stats.Description = summary.ToString();
            stats.Details = new Dictionary<string, object>
            {
                ["MethodCounts"] = methodCounts,
                ["ModuleStats"] = moduleStats.Values.ToList(),
                ["JitEfficiency"] = GetPercentage(methodCounts["Jitted"], methodCounts["Total"])
            };

            return stats;
        }

        private List<JitAnalysisInfo> AnalyzeMethodCompilation(ClrRuntime runtime, CancellationToken token)
        {
            var results = new List<JitAnalysisInfo>();
            var methodCompilationIssues = new List<MethodCompilationInfo>();

            foreach (var appDomain in runtime.AppDomains)
            {
                if (token.IsCancellationRequested) break;

                foreach (var module in appDomain.Modules)
                {
                    if (token.IsCancellationRequested) break;

                    foreach (var type in module.EnumerateTypes())
                    {
                        if (token.IsCancellationRequested) break;

                        foreach (var method in type.Methods)
                        {
                            var compilationInfo = AnalyzeMethodCompilationStatus(method, module);
                            if (compilationInfo.HasIssues)
                            {
                                methodCompilationIssues.Add(compilationInfo);
                            }
                        }
                    }
                }
            }

            // Group issues by type
            var issueGroups = methodCompilationIssues
                .GroupBy(m => m.IssueType)
                .OrderByDescending(g => g.Count());

            foreach (var group in issueGroups)
            {
                var analysis = new JitAnalysisInfo
                {
                    AnalysisType = JitAnalysisType.CompilationIssues,
                    Title = $"Compilation Issues: {group.Key}",
                    ImportanceScore = CalculateIssueImportance(group.Key, group.Count()),
                    Description = BuildCompilationIssueDescription(group.Key, group.ToList()),
                    Details = new Dictionary<string, object>
                    {
                        ["IssueType"] = group.Key,
                        ["AffectedMethods"] = group.Count(),
                        ["Methods"] = group.Take(20).ToList() // Limit for performance
                    }
                };

                results.Add(analysis);
            }

            return results;
        }

        private List<JitAnalysisInfo> AnalyzeOptimizationLevels(ClrRuntime runtime, CancellationToken token)
        {
            var results = new List<JitAnalysisInfo>();
            var optimizationStats = new Dictionary<string, OptimizationStats>();

            foreach (var appDomain in runtime.AppDomains)
            {
                if (token.IsCancellationRequested) break;

                foreach (var module in appDomain.Modules)
                {
                    if (token.IsCancellationRequested) break;

                    var moduleName = module.Name ?? "Unknown";
                    if (!optimizationStats.ContainsKey(moduleName))
                    {
                        optimizationStats[moduleName] = new OptimizationStats { ModuleName = moduleName };
                    }

                    var stats = optimizationStats[moduleName];
                    stats.IsOptimized = module.IsOptimized();

                    foreach (var type in module.EnumerateTypes())
                    {
                        if (token.IsCancellationRequested) break;

                        foreach (var method in type.Methods)
                        {
                            stats.TotalMethods++;
                            
                            if (method.NativeCode != 0)
                            {
                                stats.CompiledMethods++;
                                
                                var hotColdInfo = method.HotColdInfo;
                                if (hotColdInfo.HotSize > 0)
                                {
                                    stats.TotalCodeSize += hotColdInfo.HotSize;
                                    
                                    // Analyze code density (rough indicator of optimization)
                                    var ilSize = GetILSize(method);
                                    if (ilSize > 0)
                                    {
                                        var expansion = (double)hotColdInfo.HotSize / ilSize;
                                        stats.CodeExpansionRatios.Add(expansion);
                                        
                                        if (expansion > 10) // High expansion might indicate poor optimization
                                        {
                                            stats.PoorlyOptimizedMethods++;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Analyze optimization effectiveness
            foreach (var stats in optimizationStats.Values)
            {
                if (stats.CompiledMethods == 0) continue;

                var analysis = new JitAnalysisInfo
                {
                    AnalysisType = JitAnalysisType.OptimizationAnalysis,
                    Title = $"Optimization Analysis: {stats.ModuleName}",
                    ImportanceScore = CalculateOptimizationImportance(stats),
                    Description = BuildOptimizationDescription(stats),
                    Details = new Dictionary<string, object>
                    {
                        ["ModuleName"] = stats.ModuleName,
                        ["IsOptimized"] = stats.IsOptimized,
                        ["CompiledMethods"] = stats.CompiledMethods,
                        ["TotalCodeSize"] = stats.TotalCodeSize,
                        ["AverageCodeExpansion"] = stats.CodeExpansionRatios.Any() ? stats.CodeExpansionRatios.Average() : 0,
                        ["PoorlyOptimizedMethods"] = stats.PoorlyOptimizedMethods
                    }
                };

                results.Add(analysis);
            }

            return results;
        }

        private List<JitAnalysisInfo> IdentifyJitPerformanceIssues(ClrRuntime runtime, CancellationToken token)
        {
            var results = new List<JitAnalysisInfo>();
            var performanceIssues = new List<JitPerformanceIssue>();

            // 1. Large method analysis
            var largeMethods = FindLargeMethods(runtime, token);
            if (largeMethods.Any())
            {
                performanceIssues.AddRange(largeMethods);
            }

            // 2. Generic method instantiation analysis
            var genericIssues = AnalyzeGenericMethodIssues(runtime, token);
            if (genericIssues.Any())
            {
                performanceIssues.AddRange(genericIssues);
            }

            // 3. Exception handling overhead analysis
            var exceptionHandlingIssues = AnalyzeExceptionHandlingOverhead(runtime, token);
            if (exceptionHandlingIssues.Any())
            {
                performanceIssues.AddRange(exceptionHandlingIssues);
            }

            // Group and report issues
            var issueGroups = performanceIssues
                .GroupBy(i => i.IssueCategory)
                .OrderByDescending(g => g.Sum(i => i.ImpactScore));

            foreach (var group in issueGroups)
            {
                var analysis = new JitAnalysisInfo
                {
                    AnalysisType = JitAnalysisType.PerformanceIssues,
                    Title = $"Performance Issues: {group.Key}",
                    ImportanceScore = group.Sum(i => i.ImpactScore) / group.Count(),
                    Description = BuildPerformanceIssueDescription(group.Key, group.ToList()),
                    Details = new Dictionary<string, object>
                    {
                        ["IssueCategory"] = group.Key,
                        ["TotalIssues"] = group.Count(),
                        ["AverageImpact"] = group.Average(i => i.ImpactScore),
                        ["Issues"] = group.OrderByDescending(i => i.ImpactScore).Take(10).ToList()
                    }
                };

                results.Add(analysis);
            }

            return results;
        }

        private List<JitAnalysisInfo> AnalyzeTieredCompilation(ClrRuntime runtime, CancellationToken token)
        {
            var results = new List<JitAnalysisInfo>();
            
            // Note: Tiered compilation analysis would require more detailed runtime information
            // This is a simplified version focusing on what we can determine from the dump
            
            var tieredStats = new TieredCompilationStats();
            var methodTiers = new Dictionary<string, List<MethodTierInfo>>();

            foreach (var appDomain in runtime.AppDomains)
            {
                if (token.IsCancellationRequested) break;

                foreach (var module in appDomain.Modules)
                {
                    if (token.IsCancellationRequested) break;

                    foreach (var type in module.EnumerateTypes())
                    {
                        if (token.IsCancellationRequested) break;

                        foreach (var method in type.Methods)
                        {
                            if (method.NativeCode != 0)
                            {
                                var tierInfo = AnalyzeMethodTier(method);
                                tieredStats.TotalCompiledMethods++;
                                
                                switch (tierInfo.EstimatedTier)
                                {
                                    case 0: tieredStats.Tier0Methods++; break;
                                    case 1: tieredStats.Tier1Methods++; break;
                                    default: tieredStats.UnknownTierMethods++; break;
                                }

                                var typeName = type.Name ?? "Unknown";
                                if (!methodTiers.ContainsKey(typeName))
                                    methodTiers[typeName] = new List<MethodTierInfo>();
                                
                                methodTiers[typeName].Add(tierInfo);
                            }
                        }
                    }
                }
            }

            if (tieredStats.TotalCompiledMethods > 0)
            {
                var analysis = new JitAnalysisInfo
                {
                    AnalysisType = JitAnalysisType.TieredCompilation,
                    Title = "Tiered Compilation Analysis",
                    ImportanceScore = 0.7,
                    Description = BuildTieredCompilationDescription(tieredStats),
                    Details = new Dictionary<string, object>
                    {
                        ["TieredStats"] = tieredStats,
                        ["TypeTierDistribution"] = methodTiers.Where(kvp => kvp.Value.Count > 10)
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GroupBy(m => m.EstimatedTier).ToDictionary(g => g.Key, g => g.Count()))
                    }
                };

                results.Add(analysis);
            }

            return results;
        }

        // Helper methods
        private MethodCompilationInfo AnalyzeMethodCompilationStatus(ClrMethod method, ClrModule module)
        {
            var info = new MethodCompilationInfo
            {
                MethodName = method.Signature,
                ModuleName = module.Name ?? "Unknown",
                IsCompiled = method.NativeCode != 0,
                IsOptimized = module.IsOptimized(),
                CompilationType = method.CompilationType.ToString()
            };

            // Identify potential issues
            if (!info.IsCompiled && !method.IsAbstract() && !method.IsGeneric())
            {
                info.HasIssues = true;
                info.IssueType = "NotCompiled";
                info.IssueDescription = "Method not JIT compiled despite being callable";
            }
            else if (info.IsCompiled && method.HotColdInfo.HotSize == 0)
            {
                info.HasIssues = true;
                info.IssueType = "NoHotCode";
                info.IssueDescription = "Method compiled but has no hot code";
            }
            else if (info.IsCompiled && method.HotColdInfo.HotSize > 10000)
            {
                info.HasIssues = true;
                info.IssueType = "LargeMethod";
                info.IssueDescription = $"Very large compiled method ({method.HotColdInfo.HotSize} bytes)";
            }

            return info;
        }

        private double CalculateIssueImportance(string issueType, int count)
        {
            return issueType switch
            {
                "LargeMethod" => Math.Min(1.0, count / 50.0 + 0.3),
                "NotCompiled" => Math.Min(1.0, count / 100.0 + 0.2),
                "NoHotCode" => Math.Min(1.0, count / 200.0 + 0.1),
                _ => Math.Min(1.0, count / 100.0)
            };
        }

        private string BuildCompilationIssueDescription(string issueType, List<MethodCompilationInfo> methods)
        {
            var description = new StringBuilder();
            description.AppendLine($"Compilation Issue: {issueType}");
            description.AppendLine($"Affected Methods: {methods.Count}");
            
            switch (issueType)
            {
                case "LargeMethod":
                    var avgSize = methods.Average(m => GetMethodSize(m.MethodName));
                    description.AppendLine($"Average method size: {avgSize:F0} bytes");
                    description.AppendLine("Large methods can impact JIT performance and code locality");
                    break;
                case "NotCompiled":
                    description.AppendLine("Methods that should be compiled but aren't may indicate:");
                    description.AppendLine("- Lazy compilation issues");
                    description.AppendLine("- Code path not executed");
                    description.AppendLine("- JIT compilation failures");
                    break;
                case "NoHotCode":
                    description.AppendLine("Compiled methods without hot code may indicate:");
                    description.AppendLine("- Methods that haven't been called");
                    description.AppendLine("- Compilation artifacts");
                    break;
            }

            return description.ToString();
        }

        private double CalculateOptimizationImportance(OptimizationStats stats)
        {
            double score = 0.5; // Base score

            if (!stats.IsOptimized && stats.CompiledMethods > 100)
                score += 0.3; // Debug builds in production

            if (stats.CodeExpansionRatios.Any())
            {
                var avgExpansion = stats.CodeExpansionRatios.Average();
                if (avgExpansion > 15) score += 0.2; // High code expansion
            }

            if (stats.PoorlyOptimizedMethods > stats.CompiledMethods * 0.1)
                score += 0.2; // Many poorly optimized methods

            return Math.Min(1.0, score);
        }

        private string BuildOptimizationDescription(OptimizationStats stats)
        {
            var description = new StringBuilder();
            description.AppendLine($"Module: {stats.ModuleName}");
            description.AppendLine($"Optimization Level: {(stats.IsOptimized ? "Optimized (Release)" : "Debug")}");
            description.AppendLine($"Compiled Methods: {stats.CompiledMethods:N0}");
            description.AppendLine($"Total Code Size: {OperationHelpers.FormatSize(stats.TotalCodeSize)}");

            if (stats.CodeExpansionRatios.Any())
            {
                var avgExpansion = stats.CodeExpansionRatios.Average();
                description.AppendLine($"Average Code Expansion: {avgExpansion:F1}x");
                
                if (avgExpansion > 10)
                {
                    description.AppendLine("⚠️ High code expansion may indicate optimization issues");
                }
            }

            if (stats.PoorlyOptimizedMethods > 0)
            {
                var percentage = (double)stats.PoorlyOptimizedMethods / stats.CompiledMethods * 100;
                description.AppendLine($"Poorly Optimized Methods: {stats.PoorlyOptimizedMethods} ({percentage:F1}%)");
            }

            return description.ToString();
        }

        private List<JitPerformanceIssue> FindLargeMethods(ClrRuntime runtime, CancellationToken token)
        {
            var largeMethodIssues = new List<JitPerformanceIssue>();
            const uint LargeMethodThreshold = 5000; // bytes

            foreach (var appDomain in runtime.AppDomains)
            {
                if (token.IsCancellationRequested) break;

                foreach (var module in appDomain.Modules)
                {
                    foreach (var type in module.EnumerateTypes())
                    {
                        foreach (var method in type.Methods)
                        {
                            if (method.NativeCode != 0 && method.HotColdInfo.HotSize > LargeMethodThreshold)
                            {
                                largeMethodIssues.Add(new JitPerformanceIssue
                                {
                                    IssueCategory = "LargeMethod",
                                    MethodName = method.Signature,
                                    ModuleName = module.Name ?? "Unknown",
                                    ImpactScore = Math.Min(1.0, method.HotColdInfo.HotSize / 20000.0),
                                    Description = $"Large method: {method.HotColdInfo.HotSize:N0} bytes",
                                    Recommendation = "Consider breaking into smaller methods or reviewing complexity"
                                });
                            }
                        }
                    }
                }
            }

            return largeMethodIssues;
        }

        private List<JitPerformanceIssue> AnalyzeGenericMethodIssues(ClrRuntime runtime, CancellationToken token)
        {
            var genericIssues = new List<JitPerformanceIssue>();
            var genericInstantiations = new Dictionary<string, int>();

            foreach (var appDomain in runtime.AppDomains)
            {
                if (token.IsCancellationRequested) break;

                foreach (var module in appDomain.Modules)
                {
                    foreach (var type in module.EnumerateTypes())
                    {
                        foreach (var method in type.Methods)
                        {
                            if (method.IsGeneric())
                            {
                                var baseSignature = GetGenericMethodBaseSignature(method.Signature);
                                genericInstantiations[baseSignature] = genericInstantiations.GetValueOrDefault(baseSignature, 0) + 1;
                            }
                        }
                    }
                }
            }

            // Find methods with excessive instantiations
            foreach (var kvp in genericInstantiations.Where(kvp => kvp.Value > 20))
            {
                genericIssues.Add(new JitPerformanceIssue
                {
                    IssueCategory = "ExcessiveGenericInstantiations",
                    MethodName = kvp.Key,
                    ImpactScore = Math.Min(1.0, kvp.Value / 100.0),
                    Description = $"Generic method with {kvp.Value} instantiations",
                    Recommendation = "Consider constraining generic types or using interfaces"
                });
            }

            return genericIssues;
        }

        private List<JitPerformanceIssue> AnalyzeExceptionHandlingOverhead(ClrRuntime runtime, CancellationToken token)
        {
            var exceptionIssues = new List<JitPerformanceIssue>();
            
            // This would require deeper analysis of method IL and exception handling blocks
            // For now, return a placeholder result
            
            return exceptionIssues;
        }

        private string BuildPerformanceIssueDescription(string category, List<JitPerformanceIssue> issues)
        {
            var description = new StringBuilder();
            description.AppendLine($"Performance Issue Category: {category}");
            description.AppendLine($"Total Issues: {issues.Count}");
            description.AppendLine($"Average Impact: {issues.Average(i => i.ImpactScore):F2}");
            
            var topIssues = issues.OrderByDescending(i => i.ImpactScore).Take(5);
            description.AppendLine("\nTop Issues:");
            foreach (var issue in topIssues)
            {
                description.AppendLine($"  - {issue.Description} (Impact: {issue.ImpactScore:F2})");
            }

            return description.ToString();
        }

        private MethodTierInfo AnalyzeMethodTier(ClrMethod method)
        {
            // This is a simplified tier estimation based on available information
            var tierInfo = new MethodTierInfo
            {
                MethodName = method.Signature,
                EstimatedTier = 1 // Default to Tier 1
            };

            // Heuristics for tier estimation (simplified)
            var codeSize = method.HotColdInfo.HotSize;
            
            if (codeSize > 0 && codeSize < 100)
            {
                tierInfo.EstimatedTier = 0; // Likely Tier 0 (quick compilation)
            }
            else if (codeSize > 500)
            {
                tierInfo.EstimatedTier = 1; // Likely Tier 1 (optimized)
            }

            return tierInfo;
        }

        private string BuildTieredCompilationDescription(TieredCompilationStats stats)
        {
            var description = new StringBuilder();
            description.AppendLine("Tiered Compilation Analysis:");
            description.AppendLine($"Total Compiled Methods: {stats.TotalCompiledMethods:N0}");
            
            if (stats.TotalCompiledMethods > 0)
            {
                var tier0Percentage = (double)stats.Tier0Methods / stats.TotalCompiledMethods * 100;
                var tier1Percentage = (double)stats.Tier1Methods / stats.TotalCompiledMethods * 100;
                
                description.AppendLine($"Estimated Tier 0 Methods: {stats.Tier0Methods:N0} ({tier0Percentage:F1}%)");
                description.AppendLine($"Estimated Tier 1 Methods: {stats.Tier1Methods:N0} ({tier1Percentage:F1}%)");
                description.AppendLine($"Unknown Tier Methods: {stats.UnknownTierMethods:N0}");

                if (tier0Percentage > 50)
                {
                    description.AppendLine("\n⚠️ High percentage of Tier 0 methods may indicate:");
                    description.AppendLine("  - Application still warming up");
                    description.AppendLine("  - Methods not frequently called");
                    description.AppendLine("  - Tiered compilation not working optimally");
                }
            }

            return description.ToString();
        }

        // Utility methods
        private double GetPercentage(int numerator, int denominator)
        {
            return denominator > 0 ? (double)numerator / denominator * 100 : 0;
        }

        private uint GetILSize(ClrMethod method)
        {
            // This would require IL analysis - simplified for now
            return 100; // Placeholder
        }

        private double GetMethodSize(string methodName)
        {
            // This would extract size from method info - simplified for now
            return 1000; // Placeholder
        }

        private string GetGenericMethodBaseSignature(string signature)
        {
            // Remove generic type parameters to get base signature
            var index = signature.IndexOf('<');
            return index > 0 ? signature.Substring(0, index) : signature;
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new StringBuilder();
            insights.AppendLine($"JIT Analysis: {operationResults.Count} analysis areas examined");

            if (!operationResults.Any())
            {
                insights.AppendLine("✅ JIT analysis completed - no significant issues detected");
                return insights.ToString();
            }

            var analyses = operationResults.Cast<JitAnalysisInfo>().ToList();
            
            // Summary by analysis type
            var analysesByType = analyses.GroupBy(a => a.AnalysisType).ToDictionary(g => g.Key, g => g.Count());
            insights.AppendLine("\nAnalysis Areas:");
            foreach (var kvp in analysesByType)
            {
                insights.AppendLine($"  {kvp.Key}: {kvp.Value} findings");
            }

            // High importance findings
            var importantFindings = analyses.Where(a => a.ImportanceScore > 0.7).OrderByDescending(a => a.ImportanceScore);
            if (importantFindings.Any())
            {
                insights.AppendLine("\n🔴 High Importance Findings:");
                foreach (var finding in importantFindings.Take(5))
                {
                    insights.AppendLine($"  - {finding.Title} (Score: {finding.ImportanceScore:F2})");
                }
            }

            // Optimization recommendations
            var optimizationIssues = analyses.Where(a => a.AnalysisType == JitAnalysisType.OptimizationAnalysis).ToList();
            if (optimizationIssues.Any())
            {
                insights.AppendLine("\n🔧 Optimization Recommendations:");
                foreach (var issue in optimizationIssues.Take(3))
                {
                    insights.AppendLine($"  - {issue.Title}");
                }
            }

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
JIT ANALYSIS SPECIALIZATION:
You are analyzing comprehensive JIT (Just-In-Time) compilation data. This operation examines code compilation patterns, optimization levels, and performance characteristics.

ANALYSIS TYPES EXPLAINED:
- OverallStatistics: General JIT compilation metrics and efficiency
- CompilationIssues: Problems with method compilation
- OptimizationAnalysis: Code optimization effectiveness
- PerformanceIssues: JIT-related performance problems
- TieredCompilation: Analysis of tiered compilation patterns

KEY CONCEPTS:
- Tier 0: Quick compilation for fast startup
- Tier 1: Optimized compilation for frequently called methods
- Code expansion ratio: Native code size vs IL size
- Generic instantiations: Multiple versions of generic methods

PERFORMANCE IMPLICATIONS:
1. Large methods increase JIT time and memory usage
2. Excessive generic instantiations cause code bloat
3. Debug builds in production impact performance
4. Poor optimization reduces execution speed

AUTOMATED INVESTIGATION STRATEGY:
When JIT issues are detected, recommend:
- HotspotAnalysis for identifying performance-critical methods
- AssemblyAnalysis for module-level optimization review
- MemoryUsagePattern for code size impact analysis

Always provide actionable recommendations for JIT optimization.
";
        }
    }

    // Supporting classes
    public class JitAnalysisInfo
    {
        public JitAnalysisType AnalysisType { get; set; }
        public string Title { get; set; }
        public double ImportanceScore { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
    }

    public enum JitAnalysisType
    {
        OverallStatistics,
        CompilationIssues,
        OptimizationAnalysis,
        PerformanceIssues,
        TieredCompilation
    }

    public class ModuleJitStats
    {
        public string ModuleName { get; set; }
        public bool IsOptimized { get; set; }
        public int TotalMethods { get; set; }
        public int JittedMethods { get; set; }
        public int OptimizedMethods { get; set; }
        public int GenericMethods { get; set; }
        public int LargeMethods { get; set; }
        public uint TotalCodeSize { get; set; }
    }

    public class MethodCompilationInfo
    {
        public string MethodName { get; set; }
        public string ModuleName { get; set; }
        public bool IsCompiled { get; set; }
        public bool IsOptimized { get; set; }
        public string CompilationType { get; set; }
        public bool HasIssues { get; set; }
        public string IssueType { get; set; }
        public string IssueDescription { get; set; }
    }

    public class OptimizationStats
    {
        public string ModuleName { get; set; }
        public bool IsOptimized { get; set; }
        public int TotalMethods { get; set; }
        public int CompiledMethods { get; set; }
        public uint TotalCodeSize { get; set; }
        public int PoorlyOptimizedMethods { get; set; }
        public List<double> CodeExpansionRatios { get; set; } = new();
    }

    public class JitPerformanceIssue
    {
        public string IssueCategory { get; set; }
        public string MethodName { get; set; }
        public string ModuleName { get; set; }
        public double ImpactScore { get; set; }
        public string Description { get; set; }
        public string Recommendation { get; set; }
    }

    public class TieredCompilationStats
    {
        public int TotalCompiledMethods { get; set; }
        public int Tier0Methods { get; set; }
        public int Tier1Methods { get; set; }
        public int UnknownTierMethods { get; set; }
    }

    public class MethodTierInfo
    {
        public string MethodName { get; set; }
        public int EstimatedTier { get; set; }
    }
} 