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
    [Export(OperationNames.GetObjectSize, typeof(IDebuggerOperation))]
    class GetObjectSizeOperation : IDebuggerOperation
    {
        private CancellationToken _token;
        public string Name => OperationNames.GetObjectSize;

        public async Task<IEnumerable<object>> Execute(Models.OperationModel model, CancellationToken token, object customParameter)
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

        public async Task<string> AskGpt(OperationModel model, Collection<object> items, CancellationToken token, object parameter)
        {
            throw new System.NotImplementedException();
        }

        private void GetObjSize(ClrHeap heap, ulong obj, out uint count, out ulong size)
        {
            // Evaluation stack
            var eval = new Stack<ulong>();

            // To make sure we don't count the same object twice, we'll keep a set of all objects
            // we've seen before.
            var considered = new HashSet<ulong>();

            count = 0;
            size = 0;
            eval.Push(obj);

            while (eval.Count > 0)
            {
                // Pop an object, ignore it if we've seen it before.
                obj = eval.Pop();
                if (!considered.Add(obj))
                    continue;

                // Grab the type. We will only get null here in the case of heap corruption.
                ClrType type = heap.GetObjectType(obj);
                if (type == null)
                    continue;

                count++;
                size += type.GetSize(obj);

                // Now enumerate all objects that this object points to, add them to the
                // evaluation stack if we haven't seen them before.
                type.EnumerateRefsOfObject(obj, (child, offset) =>
                {
                    if (child != 0 && !considered.Contains(child))
                        eval.Push(child);
                });
            }
        }
    }
}
