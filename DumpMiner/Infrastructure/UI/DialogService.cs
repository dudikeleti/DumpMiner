using System.ComponentModel.Composition;
using System.Windows;
using DumpMiner.Common;
using FirstFloor.ModernUI.Windows.Controls;

namespace DumpMiner.Infrastructure.UI
{
    [Export(typeof(IDialogService))]
    class DialogService : IDialogService
    {
        public void ShowDialog(string text)
        {
            if (Application.Current != null)
                ModernDialog.ShowMessage(text, string.Empty, MessageBoxButton.OK, Application.Current.MainWindow);
        }
    }
}
