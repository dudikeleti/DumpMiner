using System;
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
    [Export(OperationNames.DumpExceptions, typeof(IDebuggerOperation))]
    class DumpExceptionsOperation : IDebuggerOperation
    {
        public string Name => OperationNames.DumpExceptions;

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var heap = DebuggerSession.Instance.Heap;
                foreach (ClrException ex in DebuggerSession.Instance.Runtime.AppDomains.SelectMany(d => d.Runtime.Threads.Select(t => t.CurrentException)))
                {
                    if (ex == null)
                    {
                        continue;
                    }

                    Console.WriteLine(ex.Address);
                }

                //TODO: Add support of inner exceptions
                //var heap = DebuggerSession.Instance.Heap;
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

        public async Task<string> AskGpt(OperationModel model, Collection<object> items, CancellationToken token, object parameter)
        {
            throw new NotImplementedException();
        }
    }
}
