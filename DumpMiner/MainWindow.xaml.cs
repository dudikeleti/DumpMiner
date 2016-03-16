using System;
using System.Windows.Input;
using DumpMiner.Common;
using FirstFloor.ModernUI.Windows.Controls;
using System.Diagnostics;
using System.Windows.Threading;
using FirstFloor.ModernUI.Presentation;

namespace DumpMiner
{
    public partial class MainWindow : ModernWindow
    {
        readonly string TitleConst;
        DispatcherTimer timer;
        PerformanceCounter exceptionsCounter;
        public MainWindow()
        {
            InitializeComponent();
            TitleConst = "Master Dump " + (Environment.Is64BitProcess ? "(x64) - " : "(x86) - ");
            exceptionsCounter = new PerformanceCounter(".NET CLR Exceptions", "# of Exceps Thrown", "DumpMiner", true);
            Title = TitleConst + GetMemoryInfo();
            var kb = new KeyBinding(GlobalCommands.LoadDumpCommand, new KeyGesture(Key.O, ModifierKeys.Control));
            InputBindings.Add(kb);
            kb = new KeyBinding(GlobalCommands.DetachCommand, new KeyGesture(Key.F5, ModifierKeys.Shift));
            InputBindings.Add(kb);
            kb = new KeyBinding(GlobalCommands.ShowProccessCommand, new KeyGesture(Key.P, ModifierKeys.Control));
            InputBindings.Add(kb);
            kb = new KeyBinding(DoGcCommand, new KeyGesture(Key.G, ModifierKeys.Control));
            InputBindings.Add(kb);
            timer = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Normal, OnTimerTick, App.Current.Dispatcher);
        }

        private RelayCommand _doGcCommand;
        public RelayCommand DoGcCommand
        {
            get
            {
                return _doGcCommand ?? (_doGcCommand = new RelayCommand(o =>
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }));
            }
        }

        private void OnTimerTick(object o, EventArgs args)
        {
            Title = TitleConst + GetMemoryInfo();
        }

        private string GetMemoryInfo()
        {
            var gcMemory = GC.GetTotalMemory(false) / 1000000;
            long processMemory;
            using (var process = Process.GetCurrentProcess())
            {
                processMemory = process.WorkingSet64 / 1000000;
            }
            float? numOfException = null;
#if !DEBUG
            try
            {
                numOfException = exceptionsCounter.NextValue();
            }
            catch {}
#endif
            return "GC size: " + gcMemory + " mb. Working set size: " + processMemory + " mb. # of exceptions: " + (numOfException.HasValue ? numOfException.ToString() : "NA");
        }
    }
}
