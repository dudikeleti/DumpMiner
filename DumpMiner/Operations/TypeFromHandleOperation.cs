using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.Linq;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;
using DumpMiner.Operations.Shared;

namespace DumpMiner.Operations
{
    [Export(OperationNames.TypeFromHandle, typeof(IDebuggerOperation))]
    class TypeFromHandleOperation : BaseAIOperation
    {
        public override string Name => OperationNames.TypeFromHandle;

        public override async Task<IEnumerable<object>> Execute(Models.OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var heap = DebuggerSession.Instance.Heap;

                var type = heap.GetTypeByMethodTable(model.ObjectAddress);

                if (type == null)
                {
                    return new[] { new { Name = "Type not found" } };
                }

                return new[]
                 {
                        new
                        {
                            Name = type.Name,
                            BaseTYpe = type.BaseType.Name,
                            MetadataToken = type.MetadataToken,
                            MethodTable = type.MethodTable,
                        }
                };
            });
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"Type Resolution: {operationResults.Count} result(s)");

            if (!operationResults.Any())
                return insights.ToString();

            var typeInfo = operationResults.First();
            var typeName = OperationHelpers.GetPropertyValue<string>(typeInfo, "Name", "Unknown");
            var baseType = OperationHelpers.GetPropertyValue<string>(typeInfo, "BaseTYpe", "Unknown");
            var methodTable = OperationHelpers.GetPropertyValue<ulong>(typeInfo, "MethodTable", 0);

            if (typeName == "Type not found")
            {
                insights.AppendLine("⚠️ Type not found - method table address may be invalid");
                return insights.ToString();
            }

            insights.AppendLine($"Type: {typeName}");
            insights.AppendLine($"Base Type: {baseType}");
            insights.AppendLine($"Method Table: {OperationHelpers.FormatAddress(methodTable)}");

            insights.AppendLine("\nKey Information:");
            insights.AppendLine("- Use DumpMethods to see methods of this type");
            insights.AppendLine("- Use DumpTypeInfo for detailed type information");
            insights.AppendLine("- Use DumpHeap -type to find instances of this type");

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
TYPE RESOLUTION SPECIALIZATION:
- Focus on type information and inheritance hierarchy
- Help identify type relationships and characteristics
- Suggest related operations for deeper type analysis
- Analyze type metadata and structure

When analyzing type resolution data, pay attention to:
1. Type inheritance hierarchy
2. Type name patterns that might indicate framework types
3. Method table addresses for further investigation
4. Base type relationships
";
        }
    }
}
