using System.Windows;

namespace DumpMiner.Common
{
    interface IDialogService
    {
        MessageBoxResult ShowDialog(string text, string title = "", MessageBoxButton button = MessageBoxButton.OK);
    }
}
