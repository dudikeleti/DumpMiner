using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;

namespace DumpMiner.Operations
{
    // !DumpHeap
    [Export(OperationNames.DumpHeap, typeof(IDebuggerOperation))]
    class DumpHeapOperation : IDebuggerOperation
    {
        public string Name => OperationNames.DumpHeap;

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customeParameter)
        {
            List<string> types = model.Types?.Split(';').ToList();
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var generation = (int)customeParameter;
                var heap = DebuggerSession.Instance.Heap;
                var results = new List<object>();
                foreach (var obj in heap.EnumerateObjectAddresses().Where(ptr => (generation != -1 && generation == heap.GetGeneration(ptr)) || generation == -1))
                {
                    if (token.IsCancellationRequested)
                        break;

                    var type = heap.GetObjectType(obj);
                    if (type == null)
                        continue;

                    if (types?.Any(t => type.Name.ToLower().Contains(t.ToLower())) ?? true)
                        results.Add(new { Address = obj, Type = type.Name, Generation = heap.GetGeneration(obj), Size = type.GetSize(obj) });
                }
                return results;
            });
        }
    }
}
