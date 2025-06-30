using System.Collections.Generic;
using DumpMiner.Common;
using DumpMiner.Infrastructure.Mef;
using FirstFloor.ModernUI.Windows;

namespace DumpMiner.Contents
{
    [OperationView("/OperationTypes/DumpGcHandles", "Dump GC Handles")]
    public partial class DumpGcHandles : IContent, IHasViewModel
    {
        public DumpGcHandles()
        {
            InitializeComponent();
            ExtendedData = new Dictionary<string, object> {["OperationName"] = OperationNames.DumpGcHandles};
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

        public string ViewModelName => ViewModelNames.BaseOperationViewModel;

        public bool IsViewModelLoaded { get; set; }

        public Dictionary<string, object> ExtendedData { get; }
    }
}
