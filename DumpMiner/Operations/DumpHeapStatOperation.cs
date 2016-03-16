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
    // !Dump heap -stat
    [Export(OperationNames.DumpHeapStat, typeof(IDebuggerOperation))]
    class DumpHeapStatOperation : IDebuggerOperation
    {
        public string Name => OperationNames.DumpHeapStat;

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customeParameter)
        {
            List<string> types = model.Types?.Split(';').ToList();

            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var heap = DebuggerSession.Instance.Heap;
                var enumerable = from o in heap.EnumerateObjectAddresses()
                                 let type = heap.GetObjectType(o)
                                 where type == null || types == null || types.Any(t => type.Name.Contains(t))
                                 group o by type
                                     into g
                                     let size = g.Sum(o => (uint)g.Key.GetSize(o))
                                     orderby size
                                     select new
                                     {
                                         Name = g.Key.Name,
                                         Size = size,
                                         Count = g.Count()
                                     };

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
    }
}
