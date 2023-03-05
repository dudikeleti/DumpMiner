using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;


namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpSyncBlock, typeof(IDebuggerOperation))]
    class DumpSyncBlockOperation : IDebuggerOperation
    {
        /// <summary>
        /// https://blogs.msdn.microsoft.com/tess/2006/01/09/a-hang-scenario-locks-and-critical-sections/
        /// </summary>

        public string Name => OperationNames.DumpObject;

        [Obsolete("Obsolete")]
        public async Task<IEnumerable<object>> Execute(Models.OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() => DebuggerSession.Instance.Heap.EnumerateBlockingObjects());
        }
    }
}
