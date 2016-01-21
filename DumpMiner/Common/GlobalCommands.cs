using System.Linq;
using System.Windows.Input;
using FirstFloor.ModernUI.Presentation;

namespace DumpMiner.Common
{
    public class GlobalCommands
    {
        public static RelayCommand LoadDumpCommand = new RelayCommand(o =>
        {
            var cmd = App.Container.GetExportedValues<ICommand>("cmd://home/LoadDumpCommand").FirstOrDefault();
            if (cmd != null && cmd.CanExecute(null))
                cmd.Execute(null);
        });

        public static RelayCommand DetachCommand = new RelayCommand(o =>
        {
            var cmd = App.Container.GetExportedValues<ICommand>("cmd://home/DetachProcessesCommand").FirstOrDefault();
            if (cmd != null && cmd.CanExecute(null))
                cmd.Execute(null);
        });

        public static RelayCommand ShowProccessCommand = new RelayCommand(o =>
        {
            var cmd = App.Container.GetExportedValues<ICommand>("cmd://home/GetRunningProcesses").FirstOrDefault();
            if (cmd != null && cmd.CanExecute(null))
                cmd.Execute(null);
        });
    }
}
