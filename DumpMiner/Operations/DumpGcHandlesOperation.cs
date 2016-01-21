using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpGcHnadles, typeof(IDebuggerOperation))]
    class DumpGcHandlesOperation : IDebuggerOperation
    {
        public string Name => OperationNames.DumpGcHnadles;

        public async Task<IEnumerable<object>> Execute(Models.OperationModel model, CancellationToken token, object customeParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var enumerable = from handle in DebuggerSession.Instance.Runtime.EnumerateHandles()
                                 where model.ObjectAddress <= 0 || handle.Address == model.ObjectAddress
                                 select new
                                 {
                                     Address = handle.Address,
                                     Type = handle.Type != null ? handle.Type.Name : "{UNKNOWN}",
                                     IsStrong = handle.IsStrong,
                                     IsPinned = handle.IsPinned,
                                     HandlType = handle.HandleType,
                                     RefCount = handle.RefCount,
                                     DependentTarget = handle.DependentTarget,
                                     DependentType = handle.DependentType != null ? handle.DependentType.Name : "{UNKNOWN}",
                                     AppDomain = handle.AppDomain != null ? handle.AppDomain.Name : "{UNKNOWN}",
                                 };
                return enumerable.ToList();
            });
        }
    }
}
