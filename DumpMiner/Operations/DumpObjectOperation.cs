﻿using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;
using Microsoft.Diagnostics.Runtime;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpObject, typeof(IDebuggerOperation))]
    class DumpObjectOperation : IDebuggerOperation
    {
        public string Name => OperationNames.DumpObject;

        public async Task<IEnumerable<object>> Execute(Models.OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var heap = DebuggerSession.Instance.Heap;
                ClrType type = heap.GetObjectType(model.ObjectAddress);
                if (type == null)
                {
                    return null;
                }
                return new DumpMiner.Debugger.ClrObject(model.ObjectAddress, type, token).Fields.Value;
            });
        }
    }
}
