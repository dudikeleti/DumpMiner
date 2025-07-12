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

                // Use ClrMD 4.0 API to enumerate memory regions
                var result = from segment in runtime.Heap.Segments
                             group segment by segment.Kind into g
                             let total = g.Sum(p => (long)p.Length)
                             orderby total descending
                             select new
                             {
                                 TotalSize = (ulong)total,
                                 Count = g.Count().ToString(),
                                 Type = g.Key.ToString(),
                                 FormattedSize = OperationHelpers.FormatSize(total)
                             };

                var list = result.ToList();

                // Add summary row
                if (list.Any())
                {
                    var totalSize = list.Sum(item => (long)item.TotalSize);
                    list.Add(new
                    {
                        TotalSize = (ulong)totalSize,
                        Count = "",
                        Type = "Total",
                        FormattedSize = OperationHelpers.FormatSize(totalSize)
                    });
                }

                return list;
            });
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"Memory Regions Analysis: {operationResults.Count} memory region types");

            if (!operationResults.Any())
            {
                insights.AppendLine("⚠️ No memory regions found - this may indicate an issue with the dump or target process");
                return insights.ToString();
            }

            // Calculate total memory usage
            var totalMemory = operationResults
                .Select(r => r.GetType().GetProperty("TotalSize")?.GetValue(r))
                .OfType<ulong>()
                .Sum(size => (long)size);

            insights.AppendLine($"Total Memory: {OperationHelpers.FormatSize(totalMemory)}");

            // Analyze potential issues
            var potentialIssues = OperationHelpers.AnalyzePotentialIssues(operationResults.Count, totalMemory, "memory regions");
            if (potentialIssues.Any())
            {
                insights.AppendLine("\nPotential Issues:");
                foreach (var issue in potentialIssues)
                {
                    insights.AppendLine($"  {issue}");
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
- Focus on process memory layout and usage patterns
- Identify different memory region types and their purposes
- Analyze memory pressure and allocation efficiency
- Look for unusual memory usage patterns and fragmentation

When analyzing memory region data, pay attention to:
1. Different memory region types (Heap, Stack, Image, etc.) and their sizes
2. Total memory usage and distribution across region types
3. Memory fragmentation patterns
4. Unusual memory allocation patterns that might indicate leaks
5. Reserved vs. committed memory ratios
6. Large individual regions that might indicate memory pressure

CRITICAL INDICATORS:
- Very large heap regions (>1GB) may indicate memory leaks
- High number of small regions can indicate fragmentation
- Unusual region types or sizes may point to specific issues
- Growing reserved memory without corresponding commits suggests allocation issues
";
        }
    }
}
