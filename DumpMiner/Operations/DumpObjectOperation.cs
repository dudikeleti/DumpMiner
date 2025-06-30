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

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpObject, typeof(IDebuggerOperation))]
    class DumpObjectOperation : BaseAIOperation
    {
        public override string Name => OperationNames.DumpObject;

        public override async Task<IEnumerable<object>> Execute(Models.OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var heap = DebuggerSession.Instance.Heap;
                ClrType type = heap.GetObjectType(model.ObjectAddress);
                if (type == null)
                {
                    return null;
                }
                return new DumpMiner.Debugger.ClrObject(model.ObjectAddress, type, token).Fields.Value;
            });
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"Object Analysis: {operationResults?.Count ?? 0} fields");

            if (operationResults == null || !operationResults.Any())
            {
                insights.AppendLine("⚠️ No object data found - object may be null or address invalid");
                return insights.ToString();
            }

            // Analyze field information
            var fields = operationResults.Cast<object>().ToList();
            insights.AppendLine($"Object contains {fields.Count} fields");

            // Look for common patterns
            var fieldTypes = OperationHelpers.GetTopGroups(fields, f => 
                OperationHelpers.GetPropertyValue<string>(f, "Type", "Unknown"));

            if (fieldTypes.Any())
            {
                insights.AppendLine("Field type distribution:");
                foreach (var fieldType in fieldTypes.Take(5))
                {
                    insights.AppendLine($"  {fieldType.Key}: {fieldType.Value} fields");
                }
            }

            // Look for potential issues
            insights.AppendLine("\nKey Information:");
            insights.AppendLine("- Use GetObjectRoot to find what's keeping this object alive");
            insights.AppendLine("- Use GetObjectSize to see total memory footprint");
            insights.AppendLine("- Look for circular references in object graphs");

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
OBJECT ANALYSIS SPECIALIZATION:
- Focus on individual object structure and field analysis
- Identify potential memory issues in object graphs
- Look for circular references and retention patterns
- Analyze field values for data integrity issues
- Suggest related operations for deeper investigation

When analyzing object data, pay attention to:
1. Null references that might indicate issues
2. Large string or array fields
3. Collection sizes and contents
4. Reference patterns that might cause leaks
5. Field values that seem unusual or suspicious
";
        }
    }
}
