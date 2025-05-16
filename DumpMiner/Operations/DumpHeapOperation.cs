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
    // !DumpHeap
    [Export(OperationNames.DumpHeap, typeof(IDebuggerOperation))]
    class DumpHeapOperation : IDebuggerOperation
    {
        public string Name => OperationNames.DumpHeap;

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            List<string> types = model.Types?.Split(';').ToList();
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var generation = (int)customParameter;
                var heap = DebuggerSession.Instance.Heap;
                var results = new List<object>();
                foreach (var seg in heap.Segments)
                {
                    foreach (var clrObject in seg.EnumerateObjects())
                    {
                        var type = clrObject.Type;
                        if (type == null)
                        {
                            continue;
                        }

                        var objectGeneration = seg.GetGeneration(clrObject.Address);
                        if (generation == -1 || (Generation)generation == objectGeneration)
                        {
                            if (types?.Any(t => type.Name.ToLower().Contains(t.ToLower())) ?? true)
                                results.Add(new { Address = clrObject.Address, Type = type.Name, MetadataToken = type.MetadataToken, Generation = objectGeneration.ToString(), Size = clrObject.Size });
                        }
                        
                    }
                }

                return results;
            });
        }

        public async Task<string> AskGpt(OperationModel model, Collection<object> items, CancellationToken token, object parameter)
        {
            throw new System.NotImplementedException();
        }
    }
}
