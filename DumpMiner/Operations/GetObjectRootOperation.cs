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

            type?.EnumerateRefsOfObject(currentObj, (innerObj, fieldOffset) =>
            {
                if (innerObj == 0 || visited.Contains(innerObj)) return;
                refChain.Push(innerObj);
                GetRefChainFromRootToObject(objPtr, refChain, visited);
                if (_found) return;
                refChain.Pop();
            });
        }
    }
}
