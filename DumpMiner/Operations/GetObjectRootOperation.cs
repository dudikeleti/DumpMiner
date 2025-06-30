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
    [Export(OperationNames.GetObjectRoot, typeof(IDebuggerOperation))]
    class GetObjectRootOperation : BaseAIOperation
    {
        private ClrHeap _heap;
        private bool _found;
        private CancellationToken _token;
        public override string Name => OperationNames.GetObjectRoot;

        public override async Task<IEnumerable<object>> Execute(Models.OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                _token = token;
                _heap = DebuggerSession.Instance.Heap;
                _found = false;
                var stack = new Stack<ulong>();
                foreach (var root in _heap.EnumerateRoots())
                {
                    stack.Clear();
                    stack.Push(root.Object);
                    if (token.IsCancellationRequested)
                        break;
                    GetRefChainFromRootToObject(model.ObjectAddress, stack, new HashSet<ulong>());
                    if (_found) break;
                }
                var enumerable = from address in stack
                                 orderby address ascending
                                 let type = _heap.GetObjectType(address)
                                 select new { Address = address, Type = type.Name, MetadataToken = type.MetadataToken, };
                return enumerable.ToList();
            });
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"Object Root Analysis: {operationResults.Count} objects in reference chain");

            if (!operationResults.Any()) 
            {
                insights.AppendLine("⚠️ No root path found - object may be unreachable or already collected");
                return insights.ToString();
            }

            // Analyze the reference chain
            var chain = operationResults.Select(r => new 
            { 
                Address = OperationHelpers.GetPropertyValue<ulong>(r, "Address", 0),
                Type = OperationHelpers.GetPropertyValue<string>(r, "Type", "Unknown")
            }).ToList();

            insights.AppendLine("Reference chain (root to target):");
            for (int i = 0; i < chain.Count; i++)
            {
                var obj = chain[i];
                var indent = new string(' ', i * 2);
                insights.AppendLine($"{indent}{i + 1}. {obj.Type} ({OperationHelpers.FormatAddress(obj.Address)})");
            }

            // Analyze root types
            var rootTypes = OperationHelpers.GetTopGroups(chain, obj => obj.Type);
            if (rootTypes.Any())
            {
                insights.AppendLine("\nTypes in reference chain:");
                foreach (var typeGroup in rootTypes.Take(5))
                {
                    insights.AppendLine($"  {typeGroup.Key}: {typeGroup.Value} instances");
                }
            }

            // Analyze potential retention issues
            var potentialIssues = new List<string>();
            
            if (chain.Count > 10)
                potentialIssues.Add("⚠️ Long reference chain - complex object relationships");
                
            if (chain.Any(obj => obj.Type.Contains("EventHandler") || obj.Type.Contains("Delegate")))
                potentialIssues.Add("⚠️ Event handlers/delegates in chain - potential subscription leak");
                
            if (chain.Any(obj => obj.Type.Contains("Cache") || obj.Type.Contains("Dictionary")))
                potentialIssues.Add("⚠️ Cache/collection in chain - may need cleanup");

            if (potentialIssues.Any())
            {
                insights.AppendLine("\nPotential Issues:");
                foreach (var issue in potentialIssues)
                {
                    insights.AppendLine($"  {issue}");
                }
            }

            insights.AppendLine("\nKey Information:");
            insights.AppendLine("- Reference chain shows why object is kept alive");
            insights.AppendLine("- Break references at any point to allow collection");
            insights.AppendLine("- Focus on root objects for memory management");

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
OBJECT ROOT ANALYSIS SPECIALIZATION:
- Focus on memory retention and reference chain analysis
- Identify root causes preventing garbage collection
- Analyze reference patterns and potential memory leaks
- Look for event subscription issues and circular references

When analyzing object root data, pay attention to:
1. Length of reference chains (complexity indicator)
2. Event handlers and delegates keeping objects alive
3. Static references preventing collection
4. Collections and caches that might need pruning
5. Circular reference patterns that block GC
";
        }

        private void GetRefChainFromRootToObject(ulong objPtr, Stack<ulong> refChain, HashSet<ulong> visited)
        {
            _token.ThrowIfCancellationRequested();
            if (_found) return;

            var currentObj = refChain.Peek();

            if (!visited.Add(currentObj))
                return;

            if (currentObj == objPtr)
            {
                _found = true;
                return;
            }

            ClrType type = _heap.GetObjectType(currentObj);
            if (type == null || type.IsFree)
                return;

            // Handle strings as leaves
            if (type.Name == "System.String")
                return;

            // Handle arrays
            if (type.IsArray)
            {
                var arrayObj = _heap.GetObject(currentObj);
                int len = arrayObj.AsArray().Length;
                var compType = type.ComponentType;
                if (compType != null)
                {
                    if (compType.IsObjectReference)
                    {
                        for (int i = 0; i < len; i++)
                        {
                            ulong elementAddress = type.GetArrayElementAddress(currentObj, i);
                            if (elementAddress == 0 || visited.Contains(elementAddress)) continue;
                            refChain.Push(elementAddress);
                            GetRefChainFromRootToObject(objPtr, refChain, visited);
                            if (_found) return;
                            refChain.Pop();
                        }
                    }
                    else if (compType.ElementType == ClrElementType.Struct)
                    {
                        for (int i = 0; i < len; i++)
                        {
                            ulong elementAddress = type.GetArrayElementAddress(currentObj, i);
                            TraverseStructFields(elementAddress, compType, objPtr, refChain, visited);
                            if (_found) return;
                        }
                    }
                }
                return;
            }

            // Handle fields (reference and struct fields)
            foreach (var field in type.Fields)
            {
                if (field.IsObjectReference)
                {
                    ulong address = field.GetAddress(currentObj, false);
                    if (address == 0 || visited.Contains(address)) continue;
                    refChain.Push(address);
                    GetRefChainFromRootToObject(objPtr, refChain, visited);
                    if (_found) return;
                    refChain.Pop();
                }
                else if (field.ElementType == ClrElementType.Struct && field.Type != null)
                {
                    ulong structAddress = field.GetAddress(currentObj, false);
                    TraverseStructFields(structAddress, field.Type, objPtr, refChain, visited);
                    if (_found) return;
                }
            }
        }

        // Helper to traverse struct fields recursively
        private void TraverseStructFields(ulong structAddress, ClrType structType, ulong objPtr, Stack<ulong> refChain, HashSet<ulong> visited)
        {
            if (structType == null) return;
            foreach (var field in structType.Fields)
            {
                if (field.IsObjectReference)
                {
                    ulong address = field.GetAddress(structAddress, false);
                    if (address == 0 || visited.Contains(address)) continue;
                    refChain.Push(address);
                    GetRefChainFromRootToObject(objPtr, refChain, visited);
                    if (_found) return;
                    refChain.Pop();
                }
                else if (field.ElementType == ClrElementType.Struct && field.Type != null)
                {
                    ulong nestedStructAddress = field.GetAddress(structAddress, false);
                    TraverseStructFields(nestedStructAddress, field.Type, objPtr, refChain, visited);
                    if (_found) return;
                }
            }
        }
    }
}
