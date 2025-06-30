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
    [Export(OperationNames.DumpHeapSegments, typeof(IDebuggerOperation))]
    class DumpHeapSegmentsOperation : BaseAIOperation
    {
        public override string Name => OperationNames.DumpHeapSegments;

        public override async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var heap = DebuggerSession.Instance.Heap;
                var enumerable = from segment in heap.Segments
                                 select new
                                 {
                                     Start = segment.Start,
                                     End = segment.End,
                                     CommittedStart = segment.CommittedMemory.Start,
                                     CommittedEnd = segment.CommittedMemory.End,
                                     ReservedStart = segment.ReservedMemory.Start,
                                     ReservedEnd = segment.ReservedMemory.End,
                                     //ProcessorAffinity = segment.ProcessorAffinity,
                                     Type = segment.Kind,
                                     Length = segment.Length,
                                     NotInUse = segment.CommittedMemory.End - segment.End
                                 };
                return enumerable.ToList();
            });
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"Heap Segment Analysis: {operationResults.Count} segments");

            if (!operationResults.Any()) 
                return insights.ToString();

            var segments = operationResults.Select(r => new 
            { 
                Start = OperationHelpers.GetPropertyValue<ulong>(r, "Start", 0),
                End = OperationHelpers.GetPropertyValue<ulong>(r, "End", 0),
                Length = OperationHelpers.GetPropertyValue<ulong>(r, "Length", 0),
                Type = OperationHelpers.GetPropertyValue(r, "Type")?.ToString() ?? "Unknown",
                NotInUse = OperationHelpers.GetPropertyValue<ulong>(r, "NotInUse", 0)
            }).ToList();

            var totalLength = segments.Sum(s => (long)s.Length);
            var totalNotInUse = segments.Sum(s => (long)s.NotInUse);
            var fragmentationRatio = totalLength > 0 ? (double)totalNotInUse / totalLength : 0;

            insights.AppendLine($"Total heap size: {OperationHelpers.FormatSize(totalLength)}");
            insights.AppendLine($"Unused space: {OperationHelpers.FormatSize(totalNotInUse)}");
            insights.AppendLine($"Fragmentation ratio: {fragmentationRatio:P1}");

            // Group by segment type
            var typeGroups = OperationHelpers.GetTopGroups(segments, s => s.Type);
            if (typeGroups.Any())
            {
                insights.AppendLine("Segment types:");
                foreach (var typeGroup in typeGroups)
                {
                    insights.AppendLine($"  {typeGroup.Key}: {typeGroup.Value} segments");
                }
            }

            // Analyze potential issues
            if (fragmentationRatio > 0.3)
            {
                insights.AppendLine("⚠️ High fragmentation detected (>30%) - may impact GC performance");
            }

            if (segments.Count > 100)
            {
                insights.AppendLine("⚠️ High number of segments - may indicate memory pressure");
            }

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
HEAP SEGMENT ANALYSIS SPECIALIZATION:
- Focus on heap memory layout and fragmentation
- Identify memory pressure and allocation patterns
- Analyze GC segment efficiency
- Look for unusual heap growth patterns

When analyzing heap segment data, pay attention to:
1. Fragmentation ratios and unused space
2. Segment count and sizes
3. Different segment types and their purposes
4. Memory pressure indicators
5. Heap growth patterns
";
        }
    }
}
