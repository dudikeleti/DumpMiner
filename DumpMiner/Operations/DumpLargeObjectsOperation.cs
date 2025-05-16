using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;
using Microsoft.Diagnostics.Runtime;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClrObject = Microsoft.Diagnostics.Runtime.ClrObject;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpLargeObjects, typeof(IDebuggerOperation))]
    class DumpLargeObjectsOperation : IDebuggerOperation
    {
        //object lockObject = new object();
        public string Name => OperationNames.DumpLargeObjects;

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            ulong size;
            if (!ulong.TryParse(customParameter.ToString(), out size))
                return null;

            var operation = App.Container.GetExportedValue<IDebuggerOperation>(OperationNames.GetObjectSize);
            if (operation == null)
                return null;

            List<string> types = model.Types?.Split(';').ToList();
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var heap = DebuggerSession.Instance.Heap;
                var results = new List<object>();

                var segmentsObjectsDictionary = new Dictionary<ClrSegment, IEnumerable<ClrObject>>();
                foreach (var segment in heap.Segments)
                {
                    segmentsObjectsDictionary[segment] = segment.EnumerateObjects().AsParallel();
                }

                foreach (var kvp in segmentsObjectsDictionary)
                {
                    var seg = kvp.Key;
                    foreach (var obj in kvp.Value)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        var type = heap.GetObjectType(obj);
                        if (type == null)
                            continue;

                        if (types?.Any(t => type.Name.ToLower().Contains(t.ToLower())) ?? true)
                        {
                            dynamic result = operation.Execute(new OperationModel { ObjectAddress = obj }, token, null).Result.FirstOrDefault();
                            if (result == null || result.TotalSize < size)
                                continue;
                            results.Add(new { Address = obj, Type = type.Name, Generation = seg.GetGeneration(obj), Size = result.TotalSize });
                        }   
                    }
                }

                //It will not work properly because in the end i must be serial because the debugger operation must run on the same thread that attach to dump\process
                //It will work if we are inspecting a dump file and the dump reader is ClrMD
                //Parallel.ForEach(
                //    // The values to be aggregated 
                //    heapObjects,

                //    // The local initial partial result
                //    () => new List<object>(),

                //    // The loop body
                //    (obj, loopState, partialResult) =>
                //    {
                //        if (token.IsCancellationRequested)
                //            return partialResult;

                //        var type = heap.GetObjectType(obj);
                //        dynamic result = operation.Execute(new OperationModel { ObjectAddress = obj }, token, null).Result.FirstOrDefault();
                //        if (result != null && result.TotalSize >= size)
                //            partialResult.Add(new { Address = obj, Type = type.Name, Generation = heap.GetGeneration(obj), Size = result.TotalSize });
                //        return partialResult;
                //    },

                //    // The final step of each local context            
                //    (localPartialSum) =>
                //    {
                //        // Enforce serial access to single, shared result
                //        lock (lockObject)
                //        {
                //            results.AddRange(localPartialSum);
                //        }
                //    });

                
                return results;
            });
        }

        public async Task<string> AskGpt(OperationModel model, Collection<object> items, CancellationToken token, object parameter)
        {
            throw new System.NotImplementedException();
        }
    }
}
