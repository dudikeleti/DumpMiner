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
    // !Dump heap -stat
    [Export(OperationNames.DumpHeapStat, typeof(IDebuggerOperation))]
    class DumpHeapStatOperation : BaseAIOperation
    {
        public override string Name => OperationNames.DumpHeapStat;

        public override async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            List<string> types = model.Types?.Split(';').ToList();

            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var heap = DebuggerSession.Instance.Heap;
                var enumerable = from o in heap.EnumerateObjects()
                                 let type = heap.GetObjectType(o)
                                 where type == null || types == null || types.Any(t => type.Name.ToLower().Contains(t.ToLower()))
                                 group o by type
                                     into g
                                     let size = g.Sum(o => (uint)o.Size)
                                     orderby size
                                     select new
                                     {
                                         Name = g.Key.Name,
                                         MetadataToken = g.Key.MetadataToken,
                                         Size = size,
                                         Count = g.Count()
                                     };

                var results = new List<object>();
                foreach (var item in enumerable)
                {
                    results.Add(item);
                    if (token.IsCancellationRequested)
                        break;
                }
                return results;
            });
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"Heap Statistics: {operationResults.Count} unique types");

            if (!operationResults.Any()) 
                return insights.ToString();

            var stats = operationResults.Select(r => new 
            { 
                Name = OperationHelpers.GetPropertyValue<string>(r, "Name", "Unknown"),
                Size = OperationHelpers.GetPropertyValue<uint>(r, "Size", 0),
                Count = OperationHelpers.GetPropertyValue<int>(r, "Count", 0)
            }).ToList();

            var totalSize = stats.Sum(s => (long)s.Size);
            var totalCount = stats.Sum(s => s.Count);

            insights.AppendLine($"Total objects: {totalCount:N0}");
            insights.AppendLine($"Total size: {OperationHelpers.FormatSize(totalSize)}");

            // Top consumers by size
            var topBySize = stats.OrderByDescending(s => s.Size).Take(10);
            insights.AppendLine("\nTop types by total size:");
            foreach (var item in topBySize.Take(5))
            {
                var avgSize = item.Count > 0 ? item.Size / item.Count : 0;
                insights.AppendLine($"  {item.Name}: {OperationHelpers.FormatSize(item.Size)} ({item.Count:N0} objects, avg {avgSize} bytes)");
            }

            // Top consumers by count
            var topByCount = stats.OrderByDescending(s => s.Count).Take(5);
            insights.AppendLine("\nTop types by object count:");
            foreach (var item in topByCount)
            {
                var percentage = totalCount > 0 ? (double)item.Count / totalCount * 100 : 0;
                insights.AppendLine($"  {item.Name}: {item.Count:N0} objects ({percentage:F1}%)");
            }

            // Analyze potential issues
            var potentialIssues = new List<string>();
            var largeTypeCount = stats.Count(s => s.Size > 100_000_000); // 100MB
            if (largeTypeCount > 0)
            {
                potentialIssues.Add($"⚠️ {largeTypeCount} type(s) consuming >100MB each");
            }

            var highCountTypes = stats.Count(s => s.Count > 1_000_000); // 1M objects
            if (highCountTypes > 0)
            {
                potentialIssues.Add($"⚠️ {highCountTypes} type(s) with >1M instances");
            }

            if (potentialIssues.Any())
            {
                insights.AppendLine("\nPotential Issues:");
                foreach (var issue in potentialIssues)
                {
                    insights.AppendLine($"  {issue}");
                }
            }

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
HEAP STATISTICS ANALYSIS SPECIALIZATION:
- Focus on type-based memory usage patterns
- Identify memory-intensive types and allocation patterns
- Analyze object distribution and potential optimizations
- Look for types that might indicate memory leaks

When analyzing heap statistics, pay attention to:
1. Types consuming disproportionate memory
2. Types with extremely high object counts
3. Unusual type distributions
4. Average object sizes that seem abnormal
5. Types that commonly indicate leaks (StringBuilder, List<T>, etc.)
";
        }
    }
}
