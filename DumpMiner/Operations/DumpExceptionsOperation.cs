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
    [Export(OperationNames.DumpExceptions, typeof(IDebuggerOperation))]
    class DumpExceptionsOperation : IDebuggerOperation
    {
        public string Name => OperationNames.DumpExceptions;

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customeParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                //TODO: Add support of inner exceptions
                var heap = DebuggerSession.Instance.Heap;
                var enumerable = from obj in heap.EnumerateObjectAddresses()
                                 let type = heap.GetObjectType(obj)
                                 where type != null && type.IsException
                                 let ex = heap.GetExceptionObject(obj)
                                 from frame in ex.StackTrace
                                 let o = new
                                 {
                                     Address = ex.Address,
                                     Name = ex.Type.Name,
                                     Message = ex.Message,
                                     HResult = ex.HResult,
                                     DisplayString = frame.DisplayString,
                                     InstructionPointer = frame.InstructionPointer,
                                     StackPointer = frame.StackPointer,
                                     Method = frame.Method,
                                     Kind = frame.Kind,
                                     ModuleName = frame.ModuleName
                                 }
                                 group o by o.Address;

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
