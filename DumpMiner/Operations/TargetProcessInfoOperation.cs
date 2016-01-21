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
    [Export(OperationNames.TargetProcessInfo, typeof(IDebuggerOperation))]
    class TargetProcessInfoOperation : IDebuggerOperation
    {
        public string Name => OperationNames.TargetProcessInfo;

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customeParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var runtime = DebuggerSession.Instance.Runtime;
                var infoModel = new TargetProcessInfoOperationModel();
                infoModel.AppDomains = string.Concat(runtime.AppDomains.Select(ad => ad.Name + ", ")).TrimEnd();
                infoModel.AppDomains = infoModel.AppDomains.Remove(infoModel.AppDomains.Length - 1, 1);
                infoModel.AppDomainsCount = runtime.AppDomains.Count;
                infoModel.ThreadsCount = runtime.Threads.Count;
                infoModel.ModulesCount = runtime.AppDomains.Sum(appDomain => appDomain.Modules.Count);
                infoModel.SymbolPath = runtime.DataTarget.SymbolLocator.SymbolPath;
                infoModel.ClrVersions = string.Concat(runtime.DataTarget.ClrVersions.Select(clrVer => clrVer.Version + ", ")).TrimEnd();
                infoModel.ClrVersions = infoModel.ClrVersions.Remove(infoModel.ClrVersions.Length - 1, 1);
                infoModel.DacInfo = string.Concat(runtime.DataTarget.ClrVersions.Select(ver => ver.DacInfo.FileName + ", ")).TrimEnd();
                infoModel.DacInfo = infoModel.DacInfo.Remove(infoModel.DacInfo.Length - 1, 1);
                infoModel.Architecture = runtime.DataTarget.Architecture.ToString();
                infoModel.IsGcServer = runtime.ServerGC;
                infoModel.HeapCount = runtime.HeapCount;
                infoModel.DumpCreatedTime = DebuggerSession.Instance.AttachedTime.ToShortTimeString();
                infoModel.PointerSize = runtime.PointerSize;

                var enumerable = from prop in infoModel.GetType().GetProperties()
                                 select new { Name = prop.Name, Value = prop.GetValue(infoModel) };
                return enumerable.ToList();
            });
        }
    }
}
