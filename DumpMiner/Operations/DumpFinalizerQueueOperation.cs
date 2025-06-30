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
    [Export(OperationNames.DumpFinalizerQueue, typeof(IDebuggerOperation))]
    class DumpFinalizerQueueOperation : BaseAIOperation
    {
        public override string Name => OperationNames.DumpFinalizerQueue;

        public override async Task<IEnumerable<object>> Execute(Models.OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var heap = DebuggerSession.Instance.Heap;
                var enumerable = from finalizer in DebuggerSession.Instance.Runtime.Heap.EnumerateFinalizableObjects()
                                 let type = heap.GetObjectType(finalizer)
                                 select new ClrObject(finalizer, type, token).Fields.Value;
                return enumerable.ToList();
            });
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"Finalizer Queue Analysis: {operationResults.Count} objects waiting for finalization");

            if (!operationResults.Any())
            {
                insights.AppendLine("✅ No objects in finalizer queue - good for GC performance");
                return insights.ToString();
            }

            // Analyze potential issues
            var potentialIssues = OperationHelpers.AnalyzePotentialIssues(operationResults.Count, itemType: "finalizable objects");
            if (potentialIssues.Any())
            {
                insights.AppendLine("Potential Issues:");
                foreach (var issue in potentialIssues)
                {
                    insights.AppendLine($"  {issue}");
                }
            }

            insights.AppendLine();
            insights.AppendLine("Key Information:");
            insights.AppendLine("- Objects in finalizer queue prevent GC collection until finalized");
            insights.AppendLine("- High numbers may indicate finalizer bottlenecks");
            insights.AppendLine("- Consider implementing IDisposable pattern instead of finalizers");

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
FINALIZER QUEUE ANALYSIS SPECIALIZATION:
- Focus on finalizer performance and GC impact
- Identify potential finalizer bottlenecks
- Look for objects that should use IDisposable instead
- Analyze finalizer thread performance issues
- Suggest optimization strategies for finalization

When analyzing finalizer queue data, pay attention to:
1. High number of objects waiting for finalization
2. Types that frequently appear in finalizer queue
3. Objects that could use dispose pattern instead
4. Potential finalizer thread blocking
";
        }
    }
}
