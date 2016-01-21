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
    [Export(OperationNames.DumpLargeObjects, typeof(IDebuggerOperation))]
    class DumpLargeObjectsOperation : IDebuggerOperation
    {
        //object lockObject = new object();
        public string Name => OperationNames.DumpLargeObjects;

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customeParameter)
        {
            ulong size;
            if (!ulong.TryParse(customeParameter.ToString(), out size))
                return null;

            var operation = App.Container.GetExportedValue<IDebuggerOperation>(OperationNames.GetObjectSize);
            if (operation == null)
                return null;

            List<string> types = model.Types?.Split(';').ToList();
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var heap = DebuggerSession.Instance.Heap;
                var results = new List<object>();

                var heapObjects = (from obj in heap.EnumerateObjectAddresses()
                                   let type = heap.GetObjectType(obj)
                                   where types?.Any(t => type != null && type.Name.ToLower().Contains(t.ToLower())) ?? true
                                   select obj).AsParallel();

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

                foreach (var obj in heapObjects)
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
                        results.Add(new { Address = obj, Type = type.Name, Generation = heap.GetGeneration(obj), Size = result.TotalSize });
                    }
                }
                return results;
            });
        }
    }
}
