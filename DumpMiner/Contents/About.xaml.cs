using System.Reflection;
using System.Windows.Controls;
using DumpMiner.Infrastructure.Mef;
using FirstFloor.ModernUI.Windows;

namespace DumpMiner.Contents
{
    [Content("/Settings/About")]
    public partial class About : IContent
    {
        public About()
        {
            InitializeComponent();
            DataContext = this;
        }

        public string ApplicationVersion
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return $"Version {version.Major}.{version.Minor}.{version.Build}";
            }
        }

        public string FrameworkVersion
        {
            get
            {
                var frameworkDescription = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
                return frameworkDescription;
            }
        }

        public string VersionInfo => $"{ApplicationVersion} • {FrameworkVersion}";

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
