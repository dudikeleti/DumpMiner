using DumpMiner.Infrastructure.Mef;
using DumpMiner.ViewModels;
using FirstFloor.ModernUI.Windows;

namespace DumpMiner.Contents
{
    [Content("/Settings/Appearance")]
    public partial class Appearance : IContent
    {
        public Appearance()
        {
            InitializeComponent();
            DataContext = new AppearanceViewModel();
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
    }
}
