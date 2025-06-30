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
    [Export(OperationNames.DumpGcHandles, typeof(IDebuggerOperation))]
    class DumpGcHandlesOperation : BaseAIOperation
    {
        public override string Name => OperationNames.DumpGcHandles;

        public override async Task<IEnumerable<object>> Execute(Models.OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var enumerable = from handle in DebuggerSession.Instance.Runtime.EnumerateHandles()
                                 where model.ObjectAddress <= 0 || handle.Address == model.ObjectAddress
                                 select new
                                 {
                                     Address = handle.Address,
                                     //Type = handle.Type != null ? handle.Type.Name : "{UNKNOWN}",
                                     IsStrong = handle.IsStrong,
                                     IsPinned = handle.IsPinned,
                                     //HandlType = handle.HandleType,
                                     //RefCount = handle.RefCount,
                                     //DependentTarget = handle.DependentTarget,
                                     //DependentType = handle.DependentType != null ? handle.DependentType.Name : "{UNKNOWN}",
                                     AppDomain = handle.AppDomain != null ? handle.AppDomain.Name : "{UNKNOWN}",
                                 };
                return enumerable.ToList();
            });
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"GC Handle Analysis: {operationResults.Count} handles");

            if (!operationResults.Any())
            {
                insights.AppendLine("✅ No GC handles found");
                return insights.ToString();
            }

            var handles = operationResults.Select(r => new 
            { 
                Address = OperationHelpers.GetPropertyValue<ulong>(r, "Address", 0),
                IsStrong = OperationHelpers.GetPropertyValue<bool>(r, "IsStrong", false),
                IsPinned = OperationHelpers.GetPropertyValue<bool>(r, "IsPinned", false),
                AppDomain = OperationHelpers.GetPropertyValue<string>(r, "AppDomain", "Unknown")
            }).ToList();

            var strongCount = handles.Count(h => h.IsStrong);
            var pinnedCount = handles.Count(h => h.IsPinned);

            insights.AppendLine($"Strong references: {strongCount}");
            insights.AppendLine($"Pinned handles: {pinnedCount}");

            // Analyze potential issues
            var potentialIssues = OperationHelpers.AnalyzePotentialIssues(operationResults.Count, itemType: "GC handles");
            if (pinnedCount > 1000)
            {
                potentialIssues.Add("⚠️ High number of pinned handles - may cause heap fragmentation");
            }

            if (potentialIssues.Any())
            {
                insights.AppendLine("\nPotential Issues:");
                foreach (var issue in potentialIssues)
                {
                    insights.AppendLine($"  {issue}");
                }
            }

            insights.AppendLine("\nKey Information:");
            insights.AppendLine("- Strong handles prevent GC collection");
            insights.AppendLine("- Pinned handles can cause heap fragmentation");
            insights.AppendLine("- Excessive handles may indicate resource leaks");

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
GC HANDLE ANALYSIS SPECIALIZATION:
- Focus on GC handle usage and potential memory issues
- Identify excessive handle usage that might cause leaks
- Analyze pinning patterns that could cause fragmentation
- Look for handle management issues

When analyzing GC handle data, pay attention to:
1. High number of strong references keeping objects alive
2. Excessive pinned handles causing fragmentation
3. Handle leaks from P/Invoke scenarios
4. AppDomain-specific handle patterns
";
        }
    }
}
