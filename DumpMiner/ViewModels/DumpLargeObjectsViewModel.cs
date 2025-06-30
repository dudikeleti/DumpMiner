using System.Windows.Input;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Infrastructure.Mef;
using FirstFloor.ModernUI.Presentation;

namespace DumpMiner.ViewModels
{
    [ViewModel(ViewModelNames.DumpLargeObjectsViewModel)]
    class DumpLargeObjectsViewModel : BaseOperationViewModel
    {
        public DumpLargeObjectsViewModel()
        {
            // Default to 85KB (Large Object Heap threshold)
            NumOfResults = 85000;
        }

        private ICommand _executeOperationCommand;
        public override ICommand ExecuteOperationCommand
        {
            get
            {
                return _executeOperationCommand ??
                (_executeOperationCommand = new RelayCommand(o => ExecuteOperation(NumOfResults),
                    o => Operation != null && DebuggerSession.Instance.IsAttached));
            }
        }
    }
} 