using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;
using DumpMiner.Operations.Shared;
using Microsoft.Diagnostics.Runtime;
using System.Text;

namespace DumpMiner.Operations
{
    // !EEHeap
    [Export(OperationNames.DumpMemoryRegions, typeof(IDebuggerOperation))]
    class DumpMemoryRegionsOperation : BaseAIOperation
    {
        public override string Name => OperationNames.DumpMemoryRegions;

        public override async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var runtime = DebuggerSession.Instance.Runtime;
                var dataTarget = DebuggerSession.Instance.DataTarget;
                var allResults = new List<object>();
                var progressReporter = model.ProgressReporter;
                var startTime = DateTime.Now;

                try
                {
                    // Phase 1: Analyze heap segments (40% of total work)
                    progressReporter?.ReportPhase("Heap Analysis", "Analyzing heap segments and memory regions");
                    progressReporter?.ReportProgress(0, "Starting heap analysis", "Examining heap segment structure");
                    
                    var heapSegments = AnalyzeHeapSegments(runtime, token);
                    
                    progressReporter?.ReportProgress(20, "Categorizing heap segments", $"Analyzing {heapSegments.Count} heap segments");
                    
                    // Group by specific generation types for better insights
                    var heapByGeneration = heapSegments
                        .Where(h => h.Usage.Contains("Ephemeral") || h.Usage.Contains("Large") || h.Usage.Contains("Pinned"))
                        .GroupBy(h => ExtractGenerationType(h.Usage))
                        .Select(g => new
                        {
                            Type = $"Heap {g.Key}",
                            Count = g.Count(),
                            TotalSize = g.Sum(h => (long)h.Size)
                        })
                        .OrderBy(h => GetGenerationOrder(h.Type));
                    
                    allResults.AddRange(heapByGeneration.Cast<object>());

                    // Phase 2: Analyze other heap types (20% of total work)
                    progressReporter?.ReportProgress(40, "Other heap types", "Analyzing remaining heap segments");
                    
                    var otherHeapTypes = heapSegments
                        .Where(h => !h.Usage.Contains("Ephemeral") && !h.Usage.Contains("Large") && !h.Usage.Contains("Pinned"))
                        .GroupBy(h => ExtractHeapType(h.Usage))
                        .Select(g => new
                        {
                            Type = $"Heap {g.Key}",
                            Count = g.Count(),
                            TotalSize = g.Sum(h => (long)h.Size)
                        });
                    
                    allResults.AddRange(otherHeapTypes.Cast<object>());

                    // Phase 3: Memory usage analysis (20% of total work)
                    progressReporter?.ReportProgress(60, "Memory usage analysis", "Analyzing committed vs reserved memory");
                    
                    var totalCommitted = heapSegments.Sum(h => GetCommittedSize(h.Details));
                    var totalReserved = heapSegments.Sum(h => GetReservedSize(h.Details));
                    
                    if (totalCommitted > 0)
                    {
                        allResults.Add(new
                        {
                            Type = "Committed Memory",
                            Count = heapSegments.Count,
                            TotalSize = totalCommitted
                        });
                    }
                    
                    if (totalReserved > 0)
                    {
                        allResults.Add(new
                        {
                            Type = "Reserved Memory",
                            Count = heapSegments.Count,
                            TotalSize = totalReserved
                        });
                    }

                    // Phase 4: Memory fragmentation analysis (10% of total work)
                    progressReporter?.ReportProgress(80, "Fragmentation analysis", "Analyzing memory fragmentation patterns");
                    
                    var fragmentationInfo = AnalyzeMemoryFragmentation(heapSegments, token);
                    if (fragmentationInfo.Any())
                    {
                        var fragmentation = fragmentationInfo.First();
                        allResults.Add(new
                        {
                            Type = "Memory Fragmentation",
                            Count = GetFragmentationCount(fragmentation.Details),
                            TotalSize = (long)fragmentation.Size
                        });
                    }

                    // Phase 5: Overall statistics (10% of total work)
                    progressReporter?.ReportProgress(90, "Overall statistics", "Calculating final statistics");
                    
                    var totalHeapSize = heapSegments.Sum(h => (long)h.Size);
                    allResults.Add(new
                    {
                        Type = "Total Heap Memory",
                        Count = heapSegments.Count,
                        TotalSize = totalHeapSize
                    });

                    // If no results, add placeholder
                    if (!allResults.Any())
                    {
                        allResults.Add(new
                        {
                            Type = "No Memory Data",
                            Count = 0,
                            TotalSize = 0L
                        });
                    }

                    // Completion
                    var totalTime = DateTime.Now - startTime;
                    progressReporter?.ReportCompleted(allResults.Count, totalTime);

                    return allResults;
                }
                catch (Exception ex)
                {
                    return new List<object>
                    {
                        new
                        {
                            Type = "Analysis Error",
                            Count = 0,
                            TotalSize = 0L
                        }
                    };
                }
            });
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"Memory Regions Analysis: {operationResults.Count} region types");

            if (!operationResults.Any())
            {
                insights.AppendLine("⚠️ No memory regions found - this may indicate an issue with the dump or target process");
                return insights.ToString();
            }

            // Extract region data from the new structure
            var regions = operationResults.Select(r => new
            {
                Type = OperationHelpers.GetPropertyValue<string>(r, "Type", "Unknown"),
                Count = OperationHelpers.GetPropertyValue<int>(r, "Count", 0),
                TotalSize = OperationHelpers.GetPropertyValue<long>(r, "TotalSize", 0)
            }).ToList();

            // Calculate total memory usage
            var totalMemory = regions.Sum(r => r.TotalSize);
            var totalRegions = regions.Sum(r => r.Count);

            insights.AppendLine($"Total Memory: {OperationHelpers.FormatSize(totalMemory)}");
            insights.AppendLine($"Total Regions: {totalRegions}");

            // Show top memory consumers
            var topConsumers = regions
                .Where(r => r.TotalSize > 0)
                .OrderByDescending(r => r.TotalSize)
                .Take(5)
                .ToList();

            if (topConsumers.Any())
            {
                insights.AppendLine("\nTop Memory Consumers:");
                foreach (var region in topConsumers)
                {
                    var percentage = totalMemory > 0 ? (double)region.TotalSize / totalMemory * 100 : 0;
                    insights.AppendLine($"  {region.Type}: {OperationHelpers.FormatSize(region.TotalSize)} ({region.Count} regions, {percentage:F1}%)");
                }
            }

            // Analyze potential issues
            var potentialIssues = OperationHelpers.AnalyzePotentialIssues(totalRegions, totalMemory, "memory regions");
            if (potentialIssues.Any())
            {
                insights.AppendLine("\nPotential Issues:");
                foreach (var issue in potentialIssues)
                {
                    insights.AppendLine($"  {issue}");
                }
            }

            // Check for specific memory issues
            var largeRegions = regions.Where(r => r.TotalSize > 1_000_000_000L).ToList(); // 1GB+
            if (largeRegions.Any())
            {
                insights.AppendLine("\nLarge Memory Regions (>1GB):");
                foreach (var region in largeRegions)
                {
                    insights.AppendLine($"  ⚠️ {region.Type}: {OperationHelpers.FormatSize(region.TotalSize)}");
                }
            }

            insights.AppendLine("\nKey Information:");
            insights.AppendLine("- Memory regions show different areas of process memory");
            insights.AppendLine("- Large heap regions may indicate memory pressure");
            insights.AppendLine("- High fragmentation can cause performance issues");
            insights.AppendLine("- Reserved vs. committed memory shows allocation patterns");

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
MEMORY REGIONS ANALYSIS SPECIALIZATION:
- Focus on .NET heap memory segments and their organization
- Identify different heap generations (Gen0, Gen1, Gen2, LOH) and their sizes
- Analyze memory fragmentation patterns between segments
- Look for memory pressure indicators and allocation efficiency

When analyzing memory region data, pay attention to:
1. Different heap generations and their relative sizes
2. Large Object Heap (LOH) usage patterns
3. Memory fragmentation between heap segments
4. Total heap memory consumption
5. Generation balance (Gen0 vs Gen1 vs Gen2)

CRITICAL INDICATORS:
- Very large heap generations (>1GB) may indicate memory pressure
- High fragmentation suggests inefficient memory usage
- Disproportionate LOH usage can indicate large object allocation issues
- Unbalanced generation sizes may point to GC tuning needs
";
        }

        private List<MemoryRegionInfo> AnalyzeHeapSegments(ClrRuntime runtime, CancellationToken token)
        {
            var results = new List<MemoryRegionInfo>();
            
            foreach (var segment in runtime.Heap.Segments)
            {
                if (token.IsCancellationRequested) break;
                
                results.Add(new MemoryRegionInfo
                {
                    RegionType = "Heap Segment",
                    StartAddress = segment.Start,
                    EndAddress = segment.End,
                    Size = segment.Length,
                    State = "Committed",
                    Protection = "ReadWrite",
                    Usage = $"{segment.Kind} - {segment.SubHeap}",
                    Details = $"Committed: {OperationHelpers.FormatSize((long)segment.CommittedMemory.Length)}, " +
                             $"Reserved: {OperationHelpers.FormatSize((long)segment.ReservedMemory.Length)}"
                });
            }
            
            return results;
        }



        private List<MemoryRegionInfo> AnalyzeMemoryFragmentation(List<MemoryRegionInfo> existingRegions, CancellationToken token)
        {
            var results = new List<MemoryRegionInfo>();
            
            try
            {
                // Sort regions by start address
                var sortedRegions = existingRegions
                    .Where(r => r.StartAddress > 0)
                    .OrderBy(r => r.StartAddress)
                    .ToList();
                
                var totalGapSize = 0UL;
                var gapCount = 0;
                
                for (int i = 1; i < sortedRegions.Count; i++)
                {
                    if (token.IsCancellationRequested) break;
                    
                    var prevRegion = sortedRegions[i - 1];
                    var currentRegion = sortedRegions[i];
                    
                    if (currentRegion.StartAddress > prevRegion.EndAddress)
                    {
                        var gapSize = currentRegion.StartAddress - prevRegion.EndAddress;
                        if (gapSize > 0)
                        {
                            totalGapSize += gapSize;
                            gapCount++;
                        }
                    }
                }
                
                if (gapCount > 0)
                {
                    results.Add(new MemoryRegionInfo
                    {
                        RegionType = "Fragmentation Analysis",
                        StartAddress = 0,
                        EndAddress = 0,
                        Size = totalGapSize,
                        State = "Analysis",
                        Protection = "N/A",
                        Usage = $"Memory Fragmentation: {gapCount} gaps totaling {OperationHelpers.FormatSize((long)totalGapSize)}",
                        Details = $"Average gap size: {OperationHelpers.FormatSize((long)(totalGapSize / (ulong)gapCount))}"
                    });
                }
            }
            catch (Exception ex)
            {
                results.Add(new MemoryRegionInfo
                {
                    RegionType = "Fragmentation Analysis Error",
                    StartAddress = 0,
                    EndAddress = 0,
                    Size = 0,
                    State = "Error",
                    Protection = "N/A",
                    Usage = $"Fragmentation analysis error: {ex.Message}"
                });
            }
            
            return results;
        }

        private string ExtractGenerationType(string usage)
        {
            // segment.Kind returns values like "Ephemeral", "Large", "Pinned"
            // We need to map these to proper generation names
            if (usage.Contains("Ephemeral"))
            {
                // Ephemeral segments contain Gen0 and Gen1
                // We can distinguish based on SubHeap number usually
                if (usage.Contains("- 0")) return "Gen0";
                if (usage.Contains("- 1")) return "Gen1";
                return "Gen0/Gen1 (Ephemeral)";
            }
            if (usage.Contains("Large")) return "LOH (Large Objects)";
            if (usage.Contains("Pinned")) return "POH (Pinned Objects)";
            
            // For other segment types, try to extract generation info
            if (usage.Contains("Gen0")) return "Gen0";
            if (usage.Contains("Gen1")) return "Gen1";
            if (usage.Contains("Gen2")) return "Gen2";
            if (usage.Contains("LOH")) return "LOH (Large Objects)";
            if (usage.Contains("POH")) return "POH (Pinned Objects)";
            
            return "Other";
        }

        private string ExtractHeapType(string usage)
        {
            if (usage.Contains("Ephemeral")) return "Ephemeral";
            if (usage.Contains("Large")) return "Large";
            if (usage.Contains("Pinned")) return "Pinned";
            return "Other";
        }

        private int GetGenerationOrder(string type)
        {
            if (type.Contains("Gen0")) return 1;
            if (type.Contains("Gen1")) return 2;
            if (type.Contains("Gen0/Gen1")) return 1; // Ephemeral will be first
            if (type.Contains("Gen2")) return 3;
            if (type.Contains("LOH")) return 4;
            if (type.Contains("POH")) return 5;
            return 6;
        }

        private long GetCommittedSize(string details)
        {
            try
            {
                if (details?.Contains("Committed:") == true)
                {
                    var start = details.IndexOf("Committed:") + "Committed:".Length;
                    var end = details.IndexOf(",", start);
                    if (end > start)
                    {
                        var sizeStr = details.Substring(start, end - start).Trim();
                        // Parse size string like "1.5 MB" back to bytes
                        return ParseSizeString(sizeStr);
                    }
                }
            }
            catch { }
            return 0;
        }

        private long GetReservedSize(string details)
        {
            try
            {
                if (details?.Contains("Reserved:") == true)
                {
                    var start = details.IndexOf("Reserved:") + "Reserved:".Length;
                    var sizeStr = details.Substring(start).Trim();
                    return ParseSizeString(sizeStr);
                }
            }
            catch { }
            return 0;
        }

        private long ParseSizeString(string sizeStr)
        {
            try
            {
                sizeStr = sizeStr.Replace(" ", "").ToUpper();
                if (sizeStr.EndsWith("B")) sizeStr = sizeStr.Substring(0, sizeStr.Length - 1);
                
                double multiplier = 1;
                if (sizeStr.EndsWith("KB")) { multiplier = 1024; sizeStr = sizeStr.Substring(0, sizeStr.Length - 2); }
                else if (sizeStr.EndsWith("MB")) { multiplier = 1024 * 1024; sizeStr = sizeStr.Substring(0, sizeStr.Length - 2); }
                else if (sizeStr.EndsWith("GB")) { multiplier = 1024 * 1024 * 1024; sizeStr = sizeStr.Substring(0, sizeStr.Length - 2); }
                
                if (double.TryParse(sizeStr, out double size))
                {
                    return (long)(size * multiplier);
                }
            }
            catch { }
            return 0;
        }

        private int GetFragmentationCount(string details)
        {
            try
            {
                if (details?.Contains("gaps") == true)
                {
                    var parts = details.Split(' ');
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        if (parts[i + 1] == "gaps" && int.TryParse(parts[i], out int count))
                        {
                            return count;
                        }
                    }
                }
            }
            catch { }
            return 1;
        }




    }

    public class MemoryRegionInfo
    {
        public string RegionType { get; set; }
        public ulong StartAddress { get; set; }
        public ulong EndAddress { get; set; }
        public ulong Size { get; set; }
        public string State { get; set; }
        public string Protection { get; set; }
        public string Usage { get; set; }
        public string Details { get; set; }
        public string FormattedStartAddress => $"0x{StartAddress:X8}";
        public string FormattedEndAddress => $"0x{EndAddress:X8}";
        public string FormattedSize => OperationHelpers.FormatSize((long)Size);
    }
}
