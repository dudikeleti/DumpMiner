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
    [Export(OperationNames.GetObjectSize, typeof(IDebuggerOperation))]
    class GetObjectSizeOperation : BaseAIOperation
    {
        private CancellationToken _token;
        public override string Name => OperationNames.GetObjectSize;

        public override async Task<IEnumerable<object>> Execute(Models.OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                _token = token;
                uint count;
                ulong size;
                GetObjSize(DebuggerSession.Instance.Heap, model.ObjectAddress, out count, out size);
                var enumerable = new List<object> { new { ReferencedCount = count, TotalSize = size } };
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
            insights.AppendLine($"Object Size Analysis: {operationResults.Count} objects analyzed");

            if (!operationResults.Any()) 
            {
                insights.AppendLine("⚠️ No object size data found");
                return insights.ToString();
            }

            var result = operationResults.FirstOrDefault();
            if (result != null)
            {
                var referencedCount = OperationHelpers.GetPropertyValue<uint>(result, "ReferencedCount", 0);
                var totalSize = OperationHelpers.GetPropertyValue<ulong>(result, "TotalSize", 0);

                insights.AppendLine($"Referenced objects: {referencedCount:N0}");
                insights.AppendLine($"Total size: {OperationHelpers.FormatSize((long)totalSize)}");

                // Analyze potential issues
                var potentialIssues = new List<string>();
                
                if (referencedCount > 10000)
                    potentialIssues.Add("⚠️ High number of referenced objects - potential memory retention");
                    
                if (totalSize > 100_000_000) // 100MB
                    potentialIssues.Add("⚠️ Large object graph - significant memory usage");
                    
                if (referencedCount > 0 && totalSize / referencedCount > 50000) // Average >50KB per object
                    potentialIssues.Add("⚠️ Large average object size - potential bloated objects");

                if (potentialIssues.Any())
                {
                    insights.AppendLine("\nPotential Issues:");
                    foreach (var issue in potentialIssues)
                    {
                        insights.AppendLine($"  {issue}");
                    }
                }

                // Calculate retention metrics
                if (referencedCount > 1)
                {
                    var avgObjectSize = totalSize / referencedCount;
                    insights.AppendLine($"\nObject Graph Metrics:");
                    insights.AppendLine($"  Average object size: {OperationHelpers.FormatSize((long)avgObjectSize)}");
                    insights.AppendLine($"  Reference density: {referencedCount} objects in {OperationHelpers.FormatSize((long)totalSize)}");
                }
            }

            insights.AppendLine("\nKey Information:");
            insights.AppendLine("- Total size includes all reachable objects from the root");
            insights.AppendLine("- High reference counts may indicate memory retention issues");
            insights.AppendLine("- Use GetObjectRoot to find what keeps objects alive");

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
OBJECT SIZE ANALYSIS SPECIALIZATION:
- Focus on memory retention and object graph analysis
- Identify objects with unexpectedly large memory footprints
- Analyze reference patterns that affect garbage collection
- Look for memory leaks through retained object graphs

When analyzing object size data, pay attention to:
1. Disproportionately large object graphs
2. High reference counts that might indicate retention issues
3. Objects that should be collected but aren't
4. Memory usage patterns that affect GC performance
5. Opportunities to break reference cycles
";
        }

        private void GetObjSize(ClrHeap heap, ulong obj, out uint count, out ulong size)
        {
            // Evaluation stack
            var eval = new Stack<ulong>();
            var considered = new HashSet<ulong>();

            count = 0;
            size = 0;
            eval.Push(obj);

            while (eval.Count > 0)
            {
                obj = eval.Pop();
                if (!considered.Add(obj))
                    continue;

                ClrType type = heap.GetObjectType(obj);
                if (type == null)
                    continue;

                count++;
                size += heap.GetObject(obj).Size;

                // Manually enumerate all references from this object
                EnumerateObjectReferences(heap, obj, type, considered, eval);
            }
        }

        // Helper to enumerate all references from an object (fields, arrays, structs)
        private void EnumerateObjectReferences(ClrHeap heap, ulong obj, ClrType type, HashSet<ulong> considered, Stack<ulong> eval)
        {
            if (type == null || type.IsFree)
                return;

            if (type.IsArray)
            {
                var arrayObj = heap.GetObject(obj);
                int len = arrayObj.AsArray().Length;
                var compType = type.ComponentType;
                if (compType != null)
                {
                    if (compType.IsObjectReference)
                    {
                        for (int i = 0; i < len; i++)
                        {
                            ulong elementAddress = type.GetArrayElementAddress(obj, i);
                            if (elementAddress != 0 && considered.Add(elementAddress))
                                eval.Push(elementAddress);
                        }
                    }
                    else if (compType.ElementType == ClrElementType.Struct)
                    {
                        for (int i = 0; i < len; i++)
                        {
                            ulong elementAddress = type.GetArrayElementAddress(obj, i);
                            EnumerateStructReferences(heap, elementAddress, compType, considered, eval);
                        }
                    }
                }
                return;
            }

            foreach (var field in type.Fields)
            {
                if (field.IsObjectReference)
                {
                    ulong address = field.GetAddress(obj, false);
                    if (address != 0 && considered.Add(address))
                        eval.Push(address);
                }
                else if (field.ElementType == ClrElementType.Struct && field.Type != null)
                {
                    ulong structAddress = field.GetAddress(obj, false);
                    EnumerateStructReferences(heap, structAddress, field.Type, considered, eval);
                }
            }
        }

        // Helper to enumerate references in a struct
        private void EnumerateStructReferences(ClrHeap heap, ulong structAddress, ClrType structType, HashSet<ulong> considered, Stack<ulong> eval)
        {
            if (structType == null) return;
            foreach (var field in structType.Fields)
            {
                if (field.IsObjectReference)
                {
                    ulong address = field.GetAddress(structAddress, false);
                    if (address != 0 && considered.Add(address))
                        eval.Push(address);
                }
                else if (field.ElementType == ClrElementType.Struct && field.Type != null)
                {
                    ulong nestedStructAddress = field.GetAddress(structAddress, false);
                    EnumerateStructReferences(heap, nestedStructAddress, field.Type, considered, eval);
                }
            }
        }
    }
}
