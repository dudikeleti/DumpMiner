using System.Collections.Generic;
using DumpMiner.Common;
using DumpMiner.Infrastructure.Mef;
using FirstFloor.ModernUI.Windows;

namespace DumpMiner.Pages
{
    [Content("/DumpAnalyzer")]
    public partial class DumpAnalyzerPage : IContent, IHasViewModel
    {
        public DumpAnalyzerPage()
        {
            InitializeComponent();
            ExtendedData = new Dictionary<string, object>();
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

        public string ViewModelName => ViewModelNames.DumpAnalyzerViewModel;

        public bool IsViewModelLoaded { get; set; }

        public Dictionary<string, object> ExtendedData { get; }
    }
}
