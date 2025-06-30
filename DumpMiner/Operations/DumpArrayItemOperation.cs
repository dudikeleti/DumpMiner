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
using static DumpMiner.Debugger.ClrObject;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpArrayItem, typeof(IDebuggerOperation))]
    class DumpArrayItemOperation : BaseAIOperation
    {
        public override string Name => OperationNames.DumpArrayItem;

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

                if (!type.IsArray)
                {
                    throw new ArgumentException($"Type is not array{Environment.NewLine}Type is {type.Name}");
                }

                if (type.ComponentType != null && !type.ComponentType.IsPrimitive)
                {
                    throw new NotImplementedException();
                }

                var index = int.Parse(model.Types);
                var array = heap.GetObject(model.ObjectAddress).AsArray();
                var itemValue = array.GetObjectValue(index);
                var itemAddress = type.GetArrayElementAddress(model.ObjectAddress, index);
                if (type.ComponentType == null || type.ComponentType.IsPrimitive)
                {
                    // return new List<ClrObjectModel>() { new ClrObjectModel() { Address = itemAddress, Value = itemValue, MetadataToken = type.ComponentType?.MetadataToken ?? 0, Offset = (ulong)(type.ElementSize * index), TypeName = type.ComponentType?.Name ?? type.Name.Replace("[]", string.Empty) } };
                    return new DumpMiner.Debugger.ClrObject(itemAddress, type.ComponentType, token).Fields.Value;
                }

                if (type.ComponentType.Name == "System.String")
                {
                    throw new NotImplementedException();
                }

                if (type.ComponentType?.IsObjectReference == true)
                {
                    return new DumpMiner.Debugger.ClrObject((ulong)itemValue, heap.GetObjectType((ulong)itemValue), token).Fields.Value;
                }

                return null;
            });
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"Array Item Analysis: {operationResults.Count} fields");

            if (!operationResults.Any()) 
            {
                insights.AppendLine("⚠️ No array item data found");
                return insights.ToString();
            }

            // Analyze the array item's fields
            var fieldTypes = OperationHelpers.GetTopGroups(operationResults, 
                field => OperationHelpers.GetPropertyValue<string>(field, "TypeName", "Unknown"));

            insights.AppendLine("Field types:");
            foreach (var fieldType in fieldTypes.Take(5))
            {
                insights.AppendLine($"  {fieldType.Key}: {fieldType.Value} fields");
            }

            // Check for potential issues
            var totalSize = operationResults.Sum(field => 
                (long)OperationHelpers.GetPropertyValue<ulong>(field, "Size", 0));

            if (totalSize > 10_000_000) // 10MB
            {
                insights.AppendLine("\nPotential Issues:");
                insights.AppendLine("  ⚠️ Large array element - may indicate memory bloat");
            }

            insights.AppendLine("\nKey Information:");
            insights.AppendLine("- Array element structure shows memory layout");
            insights.AppendLine("- Use DumpObject on individual fields for deeper analysis");

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
ARRAY ITEM ANALYSIS SPECIALIZATION:
- Focus on array element structure and field analysis
- Identify primitive vs reference type patterns
- Analyze memory layout and potential optimization opportunities
- Look for unexpected field values or types

When analyzing array item data, pay attention to:
1. Field types distribution and memory usage
2. Large elements that might indicate bloated objects
3. Reference patterns that might affect GC
4. Primitive field values for data validation
5. Nested object references that might need investigation
";
        }
    }
}
