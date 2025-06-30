using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Infrastructure.Mef;
using DumpMiner.Models;
using DumpMiner.Services;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Presentation;
using Microsoft.Win32;

namespace DumpMiner.ViewModels
{
    [ViewModel(ViewModelNames.AttachDetachViewModel)]
    public class AttachDetachViewModel : BaseViewModel
    {
        private CancellationTokenSource _cancellationTokenSource;
        public AttachDetachViewModel()
        {
            DetachVisibility = Visibility.Hidden;
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
        private Visibility _detachVisibility;
        public Visibility DetachVisibility
        {
            get => _detachVisibility;
            set
            {
                _detachVisibility = value;
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

        private RelayCommand _createDumpCommand;
        [Command("cmd://home/CreateDumpCommand")]
        public RelayCommand CreateDumpCommand
        {
            get
            {
                return _createDumpCommand ?? (_createDumpCommand = new RelayCommand(o => CreateDumpExecute(), o => CanCreateDump()));
            }
        }
        #endregion

        private void AttachToProcessExecute()
        {
            try
            {
                _cancellationTokenSource.Cancel();
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
            DetachVisibility = Visibility.Visible;
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
                DetachVisibility = Visibility.Visible;
                IsGetProcessesEnabled = false;
                DetachProcessesCommand.OnCanExecuteChanged();
            }
        }

        private BitmapSource CreateBitmapSourceFromFilePath(string path)
        {
            using (var icon = Icon.ExtractAssociatedIcon(path))
            {
                if (icon == null)
                {
                    return null;
                }
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
                catch (InvalidOperationException) // processExited
                {
                    // Log
                    return null;
                }
                catch (Exception)
                {
                    return null;
                }
            }
            return isWow64;
        }

        private async void GetRunningProcessesExecute()
        {
            IsLoading = true;
            RunningProcesses.Clear();
            var isDebugger64Bit = Environment.Is64BitProcess;
            _process = new Dictionary<string, Process>();
            string currentProcessName;
            using (var proc = Process.GetCurrentProcess())
                currentProcessName = proc.ProcessName;

            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource = new CancellationTokenSource();
            }

            var dispatcher = Application.Current.Dispatcher;
            try
            {
                await Task.Run(() =>
               {
                   try
                   {
                       string fileName;
                       string fileDescription;
                       Parallel.ForEach(Process.GetProcesses().Where(p => p.ProcessName != currentProcessName && (string.IsNullOrWhiteSpace(FilterProcesses) || p.ProcessName.ToLower().Contains(FilterProcesses.ToLower()))),
                          new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, TaskScheduler = TaskScheduler.Current, CancellationToken = _cancellationTokenSource.Token },
                          process =>
                          {
                              try
                              {
                                  if (process.HasExited || isDebugger64Bit != Is64BitProcess(process) || !IsManagedProcess(process))
                                  {
                                      return;
                                  }

                                  _process[process.ProcessName] = process;

                                  fileName = process.MainModule?.FileName;
                                  fileDescription = process.MainModule?.FileVersionInfo.FileDescription;
                              }
                              catch (Exception) //access denied, invalid operation
                              {
                                  return;
                              }

                              dispatcher.InvokeAsync(() =>
                              {
                                  RunningProcesses.Add(
                                      new
                                      {
                                          Icon = CreateBitmapSourceFromFilePath(fileName),
                                          ID = process.Id,
                                          Name = process.ProcessName,
                                          Description = fileDescription,
                                          Title = process.MainWindowTitle
                                      });
                              }, DispatcherPriority.Normal, _cancellationTokenSource.Token);
                          });
                   }
                   catch (OperationCanceledException)
                   {
                   }
               }, _cancellationTokenSource.Token
               ).ConfigureAwait(false);
            }
            finally
            {
                IsLoading = false;
            }
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
            DetachVisibility = Visibility.Hidden;
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

        /// <summary>
        /// Determines whether a dump can be created from the currently selected process.
        /// </summary>
        private bool CanCreateDump()
        {
            return SelectedItem != null &&
                   !DebuggerSession.Instance.IsAttached &&
                   _process != null &&
                   _process.ContainsKey(SelectedItem.Name) &&
                   !_process[SelectedItem.Name].HasExited;
        }

        /// <summary>
        /// Executes the create dump operation with comprehensive error handling and user feedback.
        /// </summary>
        private async void CreateDumpExecute()
        {
            if (SelectedItem == null || !_process.ContainsKey(SelectedItem.Name))
            {
                ShowErrorDialog("No process selected or process no longer available.");
                return;
            }

            var targetProcess = _process[SelectedItem.Name];
            if (targetProcess.HasExited)
            {
                ShowErrorDialog("Selected process has exited.");
                return;
            }

            try
            {
                IsLoading = true;
                LastError = null;

                // Get the dump creation service
                var dumpService = App.Container.GetExportedValueOrDefault<IDumpCreationService>();
                if (dumpService == null)
                {
                    ShowErrorDialog("Dump creation service is not available. Please check your installation.");
                    return;
                }

                // Verify process is dumpable
                var isDumpable = await dumpService.IsProcessDumpableAsync(targetProcess.Id);
                if (!isDumpable)
                {
                    ShowErrorDialog($"Process '{targetProcess.ProcessName}' is not a valid .NET process or cannot be accessed for dump creation.");
                    return;
                }

                // Show dump options dialog
                var dumpOptionsResult = await ShowDumpOptionsDialog(targetProcess, dumpService);
                if (dumpOptionsResult?.Cancelled != false)
                {
                    return; // User cancelled
                }

                // Show file save dialog
                var filePath = ShowSaveFileDialog(targetProcess, dumpOptionsResult.DumpType);
                if (string.IsNullOrEmpty(filePath))
                {
                    return; // User cancelled
                }

                // Create progress tracking (using simple loading indicator)
                var progress = new Progress<DumpProgress>(p =>
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        // Update the existing loading state (you could extend this with a progress window later)
                        // For now, we'll just ensure IsLoading remains true during the operation
                    });
                });

                try
                {
                    // Create the dump
                    var result = await dumpService.CreateDumpAsync(
                        targetProcess.Id,
                        filePath,
                        dumpOptionsResult.DumpType,
                        progress,
                        CancellationToken.None);

                    if (result.Success)
                    {
                        var successMessage = BuildSuccessMessage(result);
                        var dialogResult = ModernDialog.ShowMessage(
                            successMessage,
                            "Dump Created Successfully",
                            MessageBoxButton.YesNo);

                        if (dialogResult == MessageBoxResult.Yes)
                        {
                            // Ask if user wants to load the dump
                            await LoadCreatedDump(result.FilePath);
                        }
                    }
                    else
                    {
                        ShowErrorDialog($"Failed to create dump:\n\n{result.ErrorMessage}");
                    }
                }
                catch (OperationCanceledException)
                {
                    ShowInfoDialog("Dump creation was cancelled by user.");
                }
                catch (Exception ex)
                {
                    ShowErrorDialog($"Unexpected error creating dump:\n\n{ex.Message}");
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                ShowErrorDialog($"Error initializing dump creation:\n\n{ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Shows the dump options dialog and returns the user's selection.
        /// </summary>
        private async Task<DumpOptionsResult> ShowDumpOptionsDialog(Process process, IDumpCreationService dumpService)
        {
            try
            {
                // Create the dialog content using MEF
                var dialogContent = new Contents.DumpOptions();

                // Pass the process via ExtendedData for the ViewModel
                dialogContent.ExtendedData["TargetProcess"] = process;

                // Load the ViewModel using MEF ViewModelLoader 
                var viewModelLoader = App.Container.GetExport<MefViewModelLoader>().Value;
                var viewModel = viewModelLoader.Load(dialogContent) as DumpOptionsViewModel;

                if (viewModel == null)
                {
                    throw new InvalidOperationException("Failed to load DumpOptionsViewModel through MEF");
                }

                // Set the target process after MEF construction
                viewModel.SetTargetProcess(process);

                // CRITICAL FIX: Set the ViewModel as the DataContext
                dialogContent.DataContext = viewModel;

                // Create ModernDialog with custom content and no default buttons
                var dialogWindow = new ModernDialog
                {
                    Title = "Create Memory Dump",
                    Content = dialogContent,
                    MinWidth = 680,
                    MinHeight = 860,
                    Width = 720,
                    Height = 920,
                    ResizeMode = ResizeMode.CanResize
                };

                // Set empty buttons collection so our custom ones in the content are used
                dialogWindow.Buttons = new System.Windows.Controls.Button[0];

                // Wait for the dialog result using the ViewModel's async method
                var dialogTask = viewModel.ShowDialogAsync();

                var dialogResult = dialogWindow.ShowDialog();

                if (dialogResult == true)
                {
                    var result = await dialogTask;
                    return result ?? new DumpOptionsResult { Cancelled = true };
                }

                return new DumpOptionsResult { Cancelled = true };
            }
            catch (Exception ex)
            {
                ShowErrorDialog($"Error showing dump options dialog:\n\n{ex.Message}");
                return new DumpOptionsResult { Cancelled = true };
            }
        }

        /// <summary>
        /// Shows the file save dialog for dump creation.
        /// </summary>
        private string ShowSaveFileDialog(Process process, DumpType dumpType)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var suggestedName = $"{process.ProcessName}_{process.Id}_{timestamp}_{dumpType.ToString().ToLower()}.dmp";

                var saveDialog = new SaveFileDialog
                {
                    Title = "Save Memory Dump",
                    Filter = "Memory Dump Files (*.dmp)|*.dmp|All Files (*.*)|*.*",
                    DefaultExt = "dmp",
                    FileName = suggestedName,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    OverwritePrompt = true,
                    ValidateNames = true
                };

                return saveDialog.ShowDialog() == true ? saveDialog.FileName : null;
            }
            catch (Exception ex)
            {
                ShowErrorDialog($"Error showing save dialog:\n\n{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Builds a comprehensive success message for dump creation.
        /// </summary>
        private static string BuildSuccessMessage(DumpCreationResult result)
        {
            var message = new StringBuilder();
            message.AppendLine("Memory dump created successfully!");
            message.AppendLine();
            message.AppendLine($"📁 File: {Path.GetFileName(result.FilePath)}");
            message.AppendLine($"📏 Size: {result.FormattedFileSize}");
            message.AppendLine($"⏱️ Duration: {result.Duration.TotalSeconds:F1} seconds");
            message.AppendLine($"🔧 Type: {result.DumpType}");
            message.AppendLine();
            message.AppendLine("Would you like to load this dump for analysis?");

            return message.ToString();
        }

        /// <summary>
        /// Loads a created dump file into the application.
        /// </summary>
        private async Task LoadCreatedDump(string filePath)
        {
            try
            {
                IsLoading = true;

                var success = await DebuggerSession.Instance.LoadDump(filePath, CrashDumpReader.DbgEng);
                if (success)
                {
                    AttachedProcessName = Path.GetFileName(filePath);
                    DetachVisibility = Visibility.Visible;
                    IsGetProcessesEnabled = false;
                    DetachProcessesCommand.OnCanExecuteChanged();

                    ShowInfoDialog("Dump loaded successfully! You can now analyze it using the available operations.");
                }
                else
                {
                    ShowErrorDialog("Failed to load the created dump file.");
                }
            }
            catch (Exception ex)
            {
                ShowErrorDialog($"Error loading dump:\n\n{ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Shows an error dialog with consistent styling.
        /// </summary>
        private static void ShowErrorDialog(string message)
        {
            ModernDialog.ShowMessage(message, "Error", MessageBoxButton.OK);
        }

        /// <summary>
        /// Shows an info dialog with consistent styling.
        /// </summary>
        private static void ShowInfoDialog(string message)
        {
            ModernDialog.ShowMessage(message, "Information", MessageBoxButton.OK);
        }

        ~AttachDetachViewModel()
        {
            Dispose(false);
        }
    }
}
