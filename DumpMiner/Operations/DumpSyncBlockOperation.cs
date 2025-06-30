using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Operations.Shared;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpSyncBlock, typeof(IDebuggerOperation))]
    class DumpSyncBlockOperation : BaseAIOperation
    {
        /// <summary>
        /// https://blogs.msdn.microsoft.com/tess/2006/01/09/a-hang-scenario-locks-and-critical-sections/
        /// </summary>

        public override string Name => OperationNames.DumpSyncBlock;

        [Obsolete("Obsolete")]
        public override async Task<IEnumerable<object>> Execute(Models.OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() => DebuggerSession.Instance.Heap.EnumerateSyncBlocks());
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"Synchronization Block Analysis: {operationResults.Count} sync blocks");

            if (!operationResults.Any())
            {
                insights.AppendLine("✅ No active sync blocks - no thread synchronization issues");
                return insights.ToString();
            }

            // Analyze potential issues
            var potentialIssues = OperationHelpers.AnalyzePotentialIssues(operationResults.Count, itemType: "sync blocks");
            if (potentialIssues.Any())
            {
                insights.AppendLine("Potential Issues:");
                foreach (var issue in potentialIssues)
                {
                    insights.AppendLine($"  {issue}");
                }
            }

            insights.AppendLine("\nKey Information:");
            insights.AppendLine("- Sync blocks indicate thread synchronization activity");
            insights.AppendLine("- High numbers may suggest deadlock or contention issues");
            insights.AppendLine("- Use DumpClrStack to analyze thread states");
            insights.AppendLine("- Consider thread synchronization patterns");

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
SYNCHRONIZATION BLOCK ANALYSIS SPECIALIZATION:
- Focus on thread synchronization and potential deadlocks
- Identify lock contention and threading issues
- Analyze patterns that might indicate deadlocks
- Suggest thread analysis operations for deeper investigation

When analyzing sync block data, pay attention to:
1. High number of active sync blocks
2. Patterns suggesting lock contention
3. Potential deadlock scenarios
4. Thread synchronization bottlenecks
";
        }
    }
}
