using System.Collections.Generic;
using DumpMiner.Common;
using DumpMiner.Infrastructure.Mef;
using FirstFloor.ModernUI.Windows;

namespace DumpMiner.Contents
{
    [OperationView("/OperationTypes/DumpLargeObjects", "Dump Large Objects")]
    public partial class DumpLargeObjects : IContent, IHasViewModel
    {
        public DumpLargeObjects()
        {
            InitializeComponent();
            ExtendedData = new Dictionary<string, object> {["OperationName"] = OperationNames.DumpLargeObjects};
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

        public string ViewModelName => ViewModelNames.DumpLargeObjectsViewModel;

        public bool IsViewModelLoaded { get; set; }

        public Dictionary<string, object> ExtendedData { get; }
    }
}
