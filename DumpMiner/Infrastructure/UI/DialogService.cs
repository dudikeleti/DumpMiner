using System.ComponentModel.Composition;
using System.Windows;
using DumpMiner.Common;
using FirstFloor.ModernUI.Windows.Controls;

namespace DumpMiner.Infrastructure.UI
{
    [Export(typeof(IDialogService))]
    class DialogService : IDialogService
    {
        public MessageBoxResult ShowDialog(string text, string title = "", MessageBoxButton button = MessageBoxButton.OK)
        {
            if (Application.Current.Dispatcher.CheckAccess())
            {
                return Application.Current == null
                    ? MessageBoxResult.Cancel
                    : ModernDialog.ShowMessage(text, string.Empty, MessageBoxButton.OK, Application.Current.MainWindow);
            }
            else
            {
                return Application.Current.Dispatcher.Invoke(() => Application.Current == null
                    ? MessageBoxResult.Cancel
                    : ModernDialog.ShowMessage(text, string.Empty, MessageBoxButton.OK,
                        Application.Current.MainWindow));
            }
        }
    }
}
