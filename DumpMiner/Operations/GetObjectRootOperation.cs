using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;
using Microsoft.Diagnostics.Runtime;

namespace DumpMiner.Operations
{
    [Export(OperationNames.GetObjectRoot, typeof(IDebuggerOperation))]
    class GetObjectRootOperation : IDebuggerOperation
    {
        private ClrHeap _heap;
        private bool _found;
        private CancellationToken _token;
        public string Name => OperationNames.GetObjectRoot;

        public async Task<IEnumerable<object>> Execute(Models.OperationModel model, CancellationToken token, object customParameter)
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

        public async Task<string> AskGpt(OperationModel model, Collection<object> items, CancellationToken token, object parameter)
        {
            throw new System.NotImplementedException();
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
