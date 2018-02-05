using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using DumpMiner.Common;
using DumpMiner.Infrastructure.Mef;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Navigation;

namespace DumpMiner.Pages
{
    [Content("/AttachDetach")]
    public partial class AttachDetach : IContent, IHasViewModel
    {
        public AttachDetach()
        {
            InitializeComponent();
        }

        public void OnFragmentNavigation(FirstFloor.ModernUI.Windows.Navigation.FragmentNavigationEventArgs e)
        { }

        public void OnNavigatedFrom(FirstFloor.ModernUI.Windows.Navigation.NavigationEventArgs e)
        { }

        public void OnNavigatedTo(FirstFloor.ModernUI.Windows.Navigation.NavigationEventArgs e)
        { }

        public void OnNavigatingFrom(FirstFloor.ModernUI.Windows.Navigation.NavigatingCancelEventArgs e)
        { }

        public string ViewModelName => ViewModelNames.AttachDetachViewModel;

        public bool IsViewModelLoaded { get; set; }

        public Dictionary<string, object> ExtendedData { get; private set; }

        private void EventSetter_OnHandler(object sender, MouseButtonEventArgs e)
        {
            var cmd = App.Container.GetExportedValues<ICommand>("cmd://home/AttachToProcessCommand").FirstOrDefault();
            if (cmd == null) return;
            if (cmd.CanExecute(null))
            {
                try
                {
                    cmd.Execute(null);
                    var lastError = ((BaseViewModel)DataContext).LastError;
                    if (!string.IsNullOrEmpty(lastError))
                    {
                        ModernDialog.ShowMessage("Error in attach to process. \n" + lastError, "Error", MessageBoxButton.OK);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    ModernDialog.ShowMessage("Error in attach to process. \n" + ex.Message, "Error", MessageBoxButton.OK);
                    return;
                }

                var bbBlock = new BBCodeBlock();
                bbBlock.LinkNavigator.Navigate(new Uri("/OperationTypes", UriKind.Relative), this, NavigationHelper.FrameSelf);
            }
        }
    }
}