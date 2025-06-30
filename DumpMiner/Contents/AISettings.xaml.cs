using DumpMiner.Infrastructure.Mef;
using DumpMiner.ViewModels;
using FirstFloor.ModernUI.Windows;

namespace DumpMiner.Contents
{
    [Content("/Settings/AI")]
    public partial class AISettings : IContent
    {
        public AISettings()
        {
            InitializeComponent();
            DataContext = new AISettingsViewModel();
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