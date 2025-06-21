using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Input;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Infrastructure.Mef;
using DumpMiner.Infrastructure.UI;
using DumpMiner.Models;
using FirstFloor.ModernUI.Presentation;
using Microsoft.Diagnostics.Runtime;

namespace DumpMiner.ViewModels
{
    [ViewModel(ViewModelNames.DumpAnalyzerViewModel)]
    class DumpAnalyzerViewModel : BaseOperationViewModel
    {
        public DumpAnalyzerViewModel()
        {
        }

        private string _heapStats;
        public string HeapStats
        {
            get { return _heapStats; }
            set
            {
                _heapStats = value;
                OnPropertyChanged();
            }
        }

        private string _roots;
        public string Roots
        {
            get { return _roots; }
            set
            {
                _roots = value;
                OnPropertyChanged();
            }
        }

        private string _syncBlock;
        public string SyncBlock
        {
            get { return _syncBlock; }
            set
            {
                _syncBlock = value;
                OnPropertyChanged();
            }
        }

        private string _exceptions;
        public string Exceptions
        {
            get { return _exceptions; }
            set
            {
                _exceptions = value;
                OnPropertyChanged();
            }
        }

        private ICommand _executeOperationCommand;
        public override ICommand ExecuteOperationCommand
        {
            get
            {
                return _executeOperationCommand ??
                (_executeOperationCommand = new RelayCommand(o => Execute(),
                    o => DebuggerSession.Instance.IsAttached));
            }
        }

        private async void Execute()
        {
            CancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var resultBuilder = new StringBuilder();

            var heapStatsCommand = App.Container.GetExportedValue<IDebuggerOperation>(OperationNames.DumpHeapStat);
            var stats = (await heapStatsCommand.Execute(new OperationModel(), CancellationTokenSource.Token, 2).ConfigureAwait(false)).ToList();
            BytesToKbOrMbConverter sizeConverter = new BytesToKbOrMbConverter();

            foreach (dynamic stat in stats)
            {
                if (stat.Name == "Free")
                {
                    continue;
                }

                resultBuilder.AppendLine($"Type: {stat.Name}, Size: {sizeConverter.Convert(stat.Size, null, null, CultureInfo.CurrentCulture)}, Count: {stat.Count}");
            }

            HeapStats = resultBuilder.ToString();

            var objectRootCommand = App.Container.GetExportedValue<IDebuggerOperation>(OperationNames.GetObjectRoot);
            var dumpHeapCommand = App.Container.GetExportedValue<IDebuggerOperation>(OperationNames.DumpHeap);
            resultBuilder = new StringBuilder();
            foreach (dynamic result in stats)
            {
                if (result.Size > 10000)
                {
                    var heapObjects = await dumpHeapCommand.Execute(new OperationModel { Types = result.Name }, CancellationTokenSource.Token, 2).ConfigureAwait(false);
                    string objectName = result.Name;
                    foreach (dynamic heapObject in heapObjects)
                    {
                        if (heapObject.MetadataToken != result.MetadataToken)
                        {
                            continue;
                        }

                        resultBuilder.AppendLine($"{objectName} root:");
                        var roots = (await objectRootCommand.Execute(new OperationModel { ObjectAddress = (ulong)heapObject.Address },
                            CancellationTokenSource.Token, null).ConfigureAwait(false)).ToList();

                        foreach (dynamic root in roots)
                        {
                            resultBuilder.AppendLine($"Address: 0x{((ulong)root.Address).ToString("X8")}, Type: {root.Type}");
                        }

                        resultBuilder.AppendLine();
                    }
                }
            }

            Roots = resultBuilder.ToString();

            resultBuilder = new StringBuilder();
            var syncBlockCommand = App.Container.GetExportedValue<IDebuggerOperation>(OperationNames.DumpSyncBlock);
            var syncBlocks = (await syncBlockCommand.Execute(new OperationModel(), CancellationTokenSource.Token, null).ConfigureAwait(false)).ToList();
            foreach (SyncBlock syncBlock in syncBlocks)
            {
                if (syncBlock.IsMonitorHeld && syncBlock.WaitingThreadCount > 0)
                {
                    resultBuilder.AppendLine(
                        $"Lock object: 0x{((ulong)syncBlock.Object).ToString("X8")}, Holding thread: 0x{syncBlock.HoldingThreadAddress.ToString("X8")}, Waiters count: {syncBlock.WaitingThreadCount}");
                }
            }

            SyncBlock = resultBuilder.ToString();


            //resultBuilder = new StringBuilder();
            //var syncBlockCommand = App.Container.GetExportedValue<IDebuggerOperation>(OperationNames.DumpSyncBlock);
            //var syncBlocks = (await syncBlockCommand.Execute(new OperationModel(), CancellationTokenSource.Token, null).ConfigureAwait(false)).ToList();
            //foreach (BlockingObject syncBlock in syncBlocks)
            //{
            //    if (syncBlock.Taken && syncBlock.Waiters.Count > 0)
            //    {
            //        resultBuilder.AppendLine(
            //            $"Lock object: 0x{((ulong)syncBlock.Object).ToString("X8")}, Waiters id's{syncBlock.Waiters.Count}: {string.Join(", ", syncBlock.Waiters.Select(t => t?.ManagedThreadId))}, Block reason: {syncBlock.Reason}");
            //    }
            //}


            //Exceptions = resultBuilder.ToString();
            resultBuilder = null;
        }
    }
}
