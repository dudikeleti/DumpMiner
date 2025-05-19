using System.Collections.Generic;
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
    [Export(OperationNames.TargetProcessInfo, typeof(IDebuggerOperation))]
    class TargetProcessInfoOperation : IDebuggerOperation
    {
        public string Name => OperationNames.TargetProcessInfo;

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var runtime = DebuggerSession.Instance.Runtime;
                var infoModel = new TargetProcessInfoOperationModel();
                infoModel.AppDomains = string.Concat(runtime.AppDomains.Select(ad => ad.Name + ", ")).TrimEnd();
                infoModel.AppDomains = infoModel.AppDomains.Remove(infoModel.AppDomains.Length - 1, 1);
                infoModel.AppDomainsCount = runtime.AppDomains.Length;
                infoModel.ThreadsCount = runtime.Threads.Length;
                infoModel.ModulesCount = runtime.AppDomains.Sum(appDomain => appDomain.Modules.Length);
                //infoModel.SymbolPath = runtime.DataTarget.;
                infoModel.ClrVersions = string.Concat(runtime.DataTarget.ClrVersions.Select(clrVer => clrVer.Version + ", ")).TrimEnd();
                infoModel.ClrVersions = infoModel.ClrVersions.Remove(infoModel.ClrVersions.Length - 1, 1);
                //infoModel.DacInfo = string.Concat(runtime.DataTarget.ClrVersions.Select(ver => ver.Dac.FileName + ", ")).TrimEnd();
                //infoModel.DacInfo = infoModel.DacInfo.Remove(infoModel.DacInfo.Length - 1, 1);
                infoModel.DacInfo = string.Join(";", runtime.DataTarget.ClrVersions.Select(ver => string.Join(",", ver.DebuggingLibraries.Select(dl => dl.FileName))));
                infoModel.Architecture = runtime.DataTarget.DataReader.Architecture.ToString();
                infoModel.IsGcServer = runtime.Heap.IsServer;
                infoModel.HeapCount = runtime.Heap.Segments.Length;
                infoModel.CreatedTime = DebuggerSession.Instance.AttachedTime?.ToUniversalTime().ToString("G");
                infoModel.PointerSize = runtime.DataTarget.DataReader.PointerSize;

                var enumerable = from prop in infoModel.GetType().GetProperties()
                                 select new { Name = prop.Name, Value = prop.GetValue(infoModel) };
                return enumerable.ToList();
            });
        }

        public async Task<string> AskGpt(OperationModel model, Collection<object> items, CancellationToken token, object parameter)
        {
            throw new System.NotImplementedException();
        }
    }
}
