﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Infrastructure.Mef;
using FirstFloor.ModernUI.Presentation;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Win32;

namespace DumpMiner.ViewModels
{
    [ViewModel(ViewModelNames.AttachDetachViewModel)]
    public class AttachDetachViewModel : BaseViewModel
    {
        public AttachDetachViewModel()
        {
            DetachVisiblity = Visibility.Hidden;
            IsGetProcessesEnabled = true;
            RunningProcesses = new ObservableCollection<object>();
            _processesView = (ListCollectionView)CollectionViewSource.GetDefaultView(RunningProcesses);
            _processesView.Filter = ResourceFilter;
        }
        #region props
        private readonly ListCollectionView _processesView;
        public ListCollectionView ProcessesView => _processesView;

        private ObservableCollection<object> _runningProcesses;
        public ObservableCollection<object> RunningProcesses
        {
            get => _runningProcesses;
            set
            {
                _runningProcesses = value;
                OnPropertyChanged();
            }
        }

        private string _filterProcesses;
        public string FilterProcesses
        {
            get => _filterProcesses;
            set
            {
                _filterProcesses = value;
                OnPropertyChanged();
                _processesView.Refresh();
            }
        }

        private bool _isGetProcessesEnabled;
        public bool IsGetProcessesEnabled
        {
            get => _isGetProcessesEnabled;
            set
            {
                _isGetProcessesEnabled = value;
                OnPropertyChanged();
            }
        }
        private Visibility _detachVisiblity;
        public Visibility DetachVisiblity
        {
            get => _detachVisiblity;
            set
            {
                _detachVisiblity = value;
                OnPropertyChanged();
            }
        }

        private string _attachedProcessName;
        public string AttachedProcessName
        {
            get => _attachedProcessName;
            set
            {
                _attachedProcessName = value;
                OnPropertyChanged();
            }
        }

        private bool ResourceFilter(object item)
        {
            var process = item as dynamic;
            return string.IsNullOrEmpty(FilterProcesses) || process.Name.ToLower().Contains(FilterProcesses.ToLower());
        }

        private Dictionary<string, Process> _process;
        public dynamic SelectedItem { get; set; }

        private RelayCommand _getRunningProcessesCommand;
        [Command("cmd://home/GetRunningProcesses")]
        public RelayCommand GetRunningProcessesCommand
        {
            get
            {
                return _getRunningProcessesCommand ?? (_getRunningProcessesCommand = new RelayCommand(o => GetRunningProcessesExecute(), o => !DebuggerSession.Instance.IsAttached));
            }
        }

        private RelayCommand _attachToProcessCommand;
        [Command("cmd://home/AttachToProcessCommand")]
        public RelayCommand AttachToProcessCommand
        {
            get
            {
                return _attachToProcessCommand ?? (_attachToProcessCommand = new RelayCommand(o => AttachToProcessExecute(), o => RunningProcesses != null && RunningProcesses.Count > 0 && !DebuggerSession.Instance.IsAttached));
            }
        }

        private RelayCommand _loadDumpCommand;
        [Command("cmd://home/LoadDumpCommand")]
        public RelayCommand LoadDumpCommand
        {
            get
            {
                return _loadDumpCommand ?? (_loadDumpCommand = new RelayCommand(o => LoadDumpExecute(), o => !DebuggerSession.Instance.IsAttached));
            }
        }

        private RelayCommand _detachProcessesCommand;
        [Command("cmd://home/DetachProcessesCommand")]
        public RelayCommand DetachProcessesCommand
        {
            get
            {
                return _detachProcessesCommand ?? (_detachProcessesCommand = new RelayCommand(o => Detach(), o => DebuggerSession.Instance.IsAttached));
            }
        }
        #endregion

        private void AttachToProcessExecute()
        {
            try
            {
                LastError = null;
                DebuggerSession.Instance.Attach(_process[SelectedItem.Name], 5000);
            }
            catch (AggregateException aggregate)
            {
                var message = new StringBuilder();
                foreach (var innerException in aggregate.InnerExceptions)
                {
                    message.AppendLine(innerException.Message);
                }
                LastError = message.ToString();
                return;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                return;
            }

            AttachedProcessName = SelectedItem.Name;
            DetachVisiblity = Visibility.Visible;
            IsGetProcessesEnabled = false;
            Dispose(true);
            DetachProcessesCommand.OnCanExecuteChanged();
        }

        private async void LoadDumpExecute()
        {
            var file = new OpenFileDialog
            {
                Filter = "Dump files (*.dmp, *.dump)|*.dmp;*.dump|All files (*.*)|*.*",
                Title = "Select dump file",
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = "dmp",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer)
            };
            var result = file.ShowDialog();
            if (result.HasValue && result.Value && !string.IsNullOrEmpty(file.FileName))
            {
                bool success = await DebuggerSession.Instance.LoadDump(file.FileName, CrashDumpReader.DbgEng);
                if (!success)
                {
                    App.Container.GetExport<IDialogService>().Value.ShowDialog("Load dump failed");
                }
                AttachedProcessName = file.FileName;
                DetachVisiblity = Visibility.Visible;
                IsGetProcessesEnabled = false;
                DetachProcessesCommand.OnCanExecuteChanged();
            }
        }

        private BitmapSource CreateBitmapSourceFromFilePath(string path)
        {
            using (var icon = Icon.ExtractAssociatedIcon(path))
            {
                return Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
        }

        private static bool? Is64BitProcess(Process process)
        {
            bool isWow64 = false;

            if (Environment.Is64BitOperatingSystem)
            {
                // On 64-bit OS, if a process is not running under Wow64 mode, 
                // the process must be a 64-bit process.
                try
                {
                    isWow64 = !(NativeMethods.IsWow64Process(process.Handle, out isWow64) && isWow64);
                }
                catch (Win32Exception accessDenied)
                {
                    if (accessDenied.NativeErrorCode != 0x00000005)
                    {
                        if (System.Diagnostics.Debugger.IsAttached)
                            System.Diagnostics.Debugger.Break();
                    }
                    // Log
                    return null;
                }
                catch (InvalidOperationException processExited)
                {
                    // Log
                    return null;
                }
                catch (Exception e)
                {
                    if (System.Diagnostics.Debugger.IsAttached)
                        System.Diagnostics.Debugger.Break();
                    return null;
                }
            }
            return isWow64;
        }

        private void GetRunningProcessesExecute()
        {
            IsLoading = true;
            RunningProcesses.Clear();
            //var isDebugger64Bit = IntPtr.Size == 8;
            var isDebugger64Bit = Environment.Is64BitProcess;
            _process = new Dictionary<string, Process>();
            string currentProcessName;
            using (var proc = Process.GetCurrentProcess())
                currentProcessName = proc.ProcessName;

            foreach (var process in Process.GetProcesses().Where(p => p.ProcessName != currentProcessName && isDebugger64Bit == Is64BitProcess(p) && IsManagedProcess(p)))
            {
                string fileName;
                string fileDescription;
                _process[process.ProcessName] = process;
                try
                {
                    fileName = process.MainModule.FileName;
                    fileDescription = process.MainModule.FileVersionInfo.FileDescription;
                }
                catch (Exception e) //access denied
                {
                    continue;
                }
                RunningProcesses.Add(
                    new
                    {
                        Icon = CreateBitmapSourceFromFilePath(fileName),
                        ID = process.Id,
                        Name = process.ProcessName,
                        Title = process.MainWindowTitle,
                        Description = fileDescription
                    });
            }
            IsLoading = false;
        }

        private bool IsManagedProcess(Process process)
        {
            try
            {
                // for .NET version 4 and up. (for lower versions, use StartWith("mscore"))
                return process.Modules.Cast<ProcessModule>().Any(m => m.ModuleName.ToLower().StartsWith("clr"));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void Detach()
        {
            DebuggerSession.Instance.Detach();
            AttachedProcessName = "";
            DetachVisiblity = Visibility.Hidden;
            IsGetProcessesEnabled = true;
            AttachToProcessCommand.OnCanExecuteChanged();
            LoadDumpCommand.OnCanExecuteChanged();
        }

        private void DisposeProcesses()
        {
            if (_process == null) return;
            string selectedProcessName = "";
            if (SelectedItem != null)
                selectedProcessName = SelectedItem.Name;
            foreach (var process in _process.Where(p => p.Value != null && p.Value.ProcessName != selectedProcessName))
            {
                process.Value.Dispose();
            }
            _process = null;
        }

        protected override void Dispose(bool dispose)
        {
            DisposeProcesses();
            if (dispose)
            {
                FilterProcesses = "";
                RunningProcesses.Clear();
                GC.SuppressFinalize(this);
            }
            base.Dispose(dispose);
        }

        ~AttachDetachViewModel()
        {
            Dispose(false);
        }
    }
}
