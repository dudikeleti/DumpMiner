using System.Collections.Generic;
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
    // !EEHeap
    [Export(OperationNames.DumpMemoryRegions, typeof(IDebuggerOperation))]
    class DumpMemoryRegionsOperation : IDebuggerOperation
    {
        public string Name => OperationNames.DumpMemoryRegions;

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var result = from r in DebuggerSession.Instance.Runtime.EnumerateMemoryRegions()
                             where r.Type != ClrMemoryRegionType.ReservedGCSegment
                             group r by r.Type.ToString() into g
                             let total = g.Sum(p => (uint)p.Size)
                             orderby total ascending
                             select new
                             {
                                 TotalSize = total,
                                 Count = g.Count().ToString(),
                                 Type = g.Key
                             };

                var list = result.ToList();
                list.Add(new
                {
                    TotalSize = result.Sum(item => item.TotalSize),
                    Count = "",
                    Type = "All"
                });
                return list;
            });
        }
    }
}
