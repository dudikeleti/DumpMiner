using System.Collections.Generic;
using DumpMiner.Infrastructure.Mef;
using FirstFloor.ModernUI.Windows;

namespace DumpMiner.Contents
{
    [OperationView("/OperationTypes/CompareObjects", "Compare Objects")]
    public partial class CompareObjects : IContent
    {
        public CompareObjects()
        {
            InitializeComponent();
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

        public Dictionary<string, object> ExtendedData { get; }

        public string ViewModelName => "";

        public bool IsViewModelLoaded { get; set; }
    }
}
