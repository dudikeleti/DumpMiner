using System.Windows.Input;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Infrastructure.Mef;
using FirstFloor.ModernUI.Presentation;

namespace DumpMiner.ViewModels
{
    [ViewModel(ViewModelNames.DumpHeapOperationViewModel)]
    class DumpHeapOperationViewModel : BaseOperationViewModel
    {
        public DumpHeapOperationViewModel()
        {
            _generation = -1;
        }
        private int _generation;
        public int Generation
        {
            get { return _generation; }
            set
            {
                _generation = value;
                OnPropertyChanged();
            }
        }

        private ICommand _executeOperationCommand;
        public override ICommand ExecuteOperationCommand
        {
            get
            {
                return _executeOperationCommand ??
                (_executeOperationCommand = new RelayCommand(o => ExecuteOperation(Generation),
                    o => Operation != null && DebuggerSession.Instance.IsAttached));
            }
        }
    }
}
