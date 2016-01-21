using System.Collections.Generic;
using DumpMiner.Common;
using DumpMiner.Infrastructure.Mef;
using FirstFloor.ModernUI.Windows;

namespace DumpMiner.Contents
{
    [OperationView("/OperationTypes/DumpHeapStat", "Dump Heap Stat")]
    public partial class DumpHeapStat : IContent, IHasViewModel
    {
        public DumpHeapStat()
        {
            InitializeComponent();
            ExtendedData = new Dictionary<string, object> {["OperationName"] = OperationNames.DumpHeapStat};
        }

        public void OnFragmentNavigation(FirstFloor.ModernUI.Windows.Navigation.FragmentNavigationEventArgs e)
        {
        }

        public void OnNavigatedFrom(FirstFloor.ModernUI.Windows.Navigation.NavigationEventArgs e)
        {
        }

        public void OnNavigatedTo(FirstFloor.ModernUI.Windows.Navigation.NavigationEventArgs e)
        {
        }

        public void OnNavigatingFrom(FirstFloor.ModernUI.Windows.Navigation.NavigatingCancelEventArgs e)
        {
        }

        public string ViewModelName => ViewModelNames.DumpHeapOperationViewModel;

        public Dictionary<string, object> ExtendedData { get; }

        public bool IsViewModelLoaded { get; set; }
    }
}
