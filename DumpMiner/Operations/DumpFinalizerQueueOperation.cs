﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpFinalizerQueue, typeof(IDebuggerOperation))]
    class DumpFinalizerQueueOperation : IDebuggerOperation
    {
        public string Name => OperationNames.DumpFinalizerQueue;

        public async Task<IEnumerable<object>> Execute(Models.OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var heap = DebuggerSession.Instance.Heap;
                var enumerable = from finalizer in DebuggerSession.Instance.Runtime.Heap.EnumerateFinalizableObjects()
                                 let type = heap.GetObjectType(finalizer)
                                 select new ClrObject(finalizer, type, token).Fields.Value;
                return enumerable.ToList();
            });
        }

        public async Task<string> AskGpt(OperationModel model, Collection<object> items, CancellationToken token, object parameter)
        {
            throw new System.NotImplementedException();
        }
    }
}
