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
    [Export(OperationNames.DumpTypeInfo, typeof(IDebuggerOperation))]
    class DumpTypeInfoOperation : BaseAIOperation
    {
        public override string Name => OperationNames.DumpTypeInfo;

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

                List<object> result = new List<object>();
                foreach (var propertyInfo in type.GetType().GetProperties())
                {
                    object value = propertyInfo.GetValue(type);
                    if (value == null)
                    {
                        continue;
                    }

                    if (propertyInfo.Name == "MetadataToken" || propertyInfo.Name == "MethodTable")
                    {
                        value = $"0x{System.Convert.ToUInt64(value):X8}";
                    }

                    result.Add(new { Name = propertyInfo.Name, Value = value });
                }

                return result;
            });
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"Type Information: {operationResults?.Count ?? 0} properties");

            if (operationResults == null || !operationResults.Any())
            {
                insights.AppendLine("⚠️ No type information found - object address may be invalid");
                return insights.ToString();
            }

            // Look for key properties
            var properties = operationResults.Cast<object>().ToDictionary(
                p => OperationHelpers.GetPropertyValue<string>(p, "Name", "Unknown"),
                p => OperationHelpers.GetPropertyValue(p, "Value")?.ToString() ?? "null"
            );

            foreach (var kvp in properties.Take(10))
            {
                insights.AppendLine($"{kvp.Key}: {kvp.Value}");
            }

            insights.AppendLine("\nKey Information:");
            insights.AppendLine("- Type metadata provides detailed CLR type information");
            insights.AppendLine("- Use DumpMethods to see methods of this type");
            insights.AppendLine("- Use DumpHeap -type to find instances");
            insights.AppendLine("- Check IsValueType for value vs reference type");

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
TYPE INFORMATION ANALYSIS SPECIALIZATION:
- Focus on detailed type metadata and characteristics
- Identify type structure and memory layout
- Analyze type hierarchy and relationships
- Look for unusual type properties or configurations

When analyzing type information, pay attention to:
1. Type size and memory layout
2. Value vs reference type characteristics
3. Generic type parameters
4. Method table and metadata tokens
5. Type inheritance hierarchy
";
        }
    }
}
