using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DumpMiner.Common;
using DumpMiner.Infrastructure.Mef;
using DumpMiner.Models;
using DumpMiner.Services;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace DumpMiner.ViewModels
{
    /// <summary>
    /// ViewModel for the dump options dialog, providing comprehensive configuration and validation
    /// for memory dump creation with real-time size estimation and error handling.
    /// </summary>
    [ViewModel(ViewModelNames.DumpOptionsViewModel)]
    public sealed class DumpOptionsViewModel : BaseViewModel, INotifyPropertyChanged
    {
        private readonly IDumpCreationService _dumpCreationService;
        private Process _targetProcess;
        private bool _isMinDumpSelected = true;
        private bool _isHeapDumpSelected;
        private bool _isTriageDumpSelected;
        private bool _isFullDumpSelected;
        private bool _verifyAfterCreation = true;
        private bool _showDetailedProgress = true;
        private int _timeoutMinutes = 10;
        private string _estimatedDumpSize = "Calculating...";
        private Visibility _sizeEstimationVisibility = Visibility.Collapsed;
        private readonly ObservableCollection<string> _warnings = new();
        private DumpSizeEstimation _currentSizeEstimation;
        private TaskCompletionSource<DumpOptionsResult> _completionSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="DumpOptionsViewModel"/> class using MEF dependency injection.
        /// </summary>
        /// <param name="dumpCreationService">The dump creation service.</param>
        [ImportingConstructor]
        public DumpOptionsViewModel(IDumpCreationService dumpCreationService)
        {
            _dumpCreationService = dumpCreationService;
            InitializeCommands();
        }

        /// <summary>
        /// Initializes a new instance for design-time data.
        /// </summary>
        public DumpOptionsViewModel() : this(null)
        {
            // Design-time constructor - create a dummy process for design-time
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()) ||
                System.Diagnostics.Debugger.IsAttached)
            {
                _targetProcess = Process.GetCurrentProcess();
                InitializeProcessInfo();
            }
        }

        /// <summary>
        /// Sets the target process for the dump creation and initializes process-specific data.
        /// </summary>
        /// <param name="targetProcess">The process to create a dump from.</param>
        public void SetTargetProcess(Process targetProcess)
        {
            _targetProcess = targetProcess ?? throw new ArgumentNullException(nameof(targetProcess));
            InitializeProcessInfo();
            _ = UpdateSizeEstimationAsync(); // Fire-and-forget initial estimation
        }

        #region Properties

        /// <summary>
        /// Gets the name of the target process.
        /// </summary>
        public string ProcessName => _targetProcess?.ProcessName ?? "Unknown";

        /// <summary>
        /// Gets the ID of the target process.
        /// </summary>
        public int ProcessId => _targetProcess?.Id ?? 0;

        /// <summary>
        /// Gets the formatted memory usage of the target process.
        /// </summary>
        public string FormattedMemoryUsage => _targetProcess != null
            ? FormatBytes(_targetProcess.WorkingSet64)
            : "Unknown";

        /// <summary>
        /// Gets or sets whether mini dump is selected.
        /// </summary>
        public bool IsMinDumpSelected
        {
            get => _isMinDumpSelected;
            set
            {
                if (SetProperty(ref _isMinDumpSelected, value) && value)
                {
                    IsHeapDumpSelected = IsTriageDumpSelected = IsFullDumpSelected = false;
                    _ = UpdateSizeEstimationAsync();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether heap dump is selected.
        /// </summary>
        public bool IsHeapDumpSelected
        {
            get => _isHeapDumpSelected;
            set
            {
                if (SetProperty(ref _isHeapDumpSelected, value) && value)
                {
                    IsMinDumpSelected = IsTriageDumpSelected = IsFullDumpSelected = false;
                    _ = UpdateSizeEstimationAsync();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether triage dump is selected.
        /// </summary>
        public bool IsTriageDumpSelected
        {
            get => _isTriageDumpSelected;
            set
            {
                if (SetProperty(ref _isTriageDumpSelected, value) && value)
                {
                    IsMinDumpSelected = IsHeapDumpSelected = IsFullDumpSelected = false;
                    _ = UpdateSizeEstimationAsync();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether full dump is selected.
        /// </summary>
        public bool IsFullDumpSelected
        {
            get => _isFullDumpSelected;
            set
            {
                if (SetProperty(ref _isFullDumpSelected, value) && value)
                {
                    IsMinDumpSelected = IsHeapDumpSelected = IsTriageDumpSelected = false;
                    _ = UpdateSizeEstimationAsync();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to verify the dump after creation.
        /// </summary>
        public bool VerifyAfterCreation
        {
            get => _verifyAfterCreation;
            set => SetProperty(ref _verifyAfterCreation, value);
        }

        /// <summary>
        /// Gets or sets whether to show detailed progress.
        /// </summary>
        public bool ShowDetailedProgress
        {
            get => _showDetailedProgress;
            set => SetProperty(ref _showDetailedProgress, value);
        }

        /// <summary>
        /// Gets or sets the timeout in minutes.
        /// </summary>
        public int TimeoutMinutes
        {
            get => _timeoutMinutes;
            set => SetProperty(ref _timeoutMinutes, Math.Max(1, Math.Min(60, value)));
        }

        /// <summary>
        /// Gets the estimated dump size display text.
        /// </summary>
        public string EstimatedDumpSize
        {
            get => _estimatedDumpSize;
            private set => SetProperty(ref _estimatedDumpSize, value);
        }

        /// <summary>
        /// Gets the visibility of the size estimation panel.
        /// </summary>
        public Visibility SizeEstimationVisibility
        {
            get => _sizeEstimationVisibility;
            private set => SetProperty(ref _sizeEstimationVisibility, value);
        }

        /// <summary>
        /// Gets the collection of warnings to display.
        /// </summary>
        public ObservableCollection<string> Warnings => _warnings;

        /// <summary>
        /// Gets the selected dump type.
        /// </summary>
        public DumpType SelectedDumpType
        {
            get
            {
                if (IsHeapDumpSelected) return DumpType.Heap;
                if (IsTriageDumpSelected) return DumpType.Triage;
                if (IsFullDumpSelected) return DumpType.Full;
                return DumpType.Mini;
            }
        }

        /// <summary>
        /// Gets the dump creation options based on current settings.
        /// </summary>
        public DumpCreationOptions Options => new()
        {
            Timeout = TimeSpan.FromMinutes(TimeoutMinutes),
            DetailedProgress = ShowDetailedProgress,
            VerifyAfterCreation = VerifyAfterCreation,
            CustomMetadata = new Dictionary<string, object>
            {
                ["CreatedVia"] = "DumpMiner UI",
                ["DumpType"] = SelectedDumpType.ToString(),
                ["ProcessInfo"] = new
                {
                    Name = ProcessName,
                    Id = ProcessId,
                    MemoryUsage = FormattedMemoryUsage
                }
            }
        };

        #endregion

        #region Commands

        /// <summary>
        /// Gets the command to create the dump.
        /// </summary>
        public ICommand CreateDumpCommand { get; private set; }

        /// <summary>
        /// Gets the command to cancel the dialog.
        /// </summary>
        public ICommand CancelCommand { get; private set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows the dump options dialog and returns the result.
        /// </summary>
        /// <returns>The dump options result, or null if cancelled.</returns>
        public async Task<DumpOptionsResult> ShowDialogAsync()
        {
            _completionSource = new TaskCompletionSource<DumpOptionsResult>();

            // Show the dialog (this would typically be done by the calling code)
            return await _completionSource.Task.ConfigureAwait(false);
        }

        #endregion

        #region Private Methods

        private void InitializeCommands()
        {
            CreateDumpCommand = new RelayCommand(
                _ => CreateDump(),
                _ => CanCreateDump());

            CancelCommand = new RelayCommand(
                _ => Cancel(),
                _ => true);
        }

        private void InitializeProcessInfo()
        {
            if (_targetProcess?.HasExited == false)
            {
                // Refresh process info
                _targetProcess.Refresh();
                OnPropertyChanged(nameof(ProcessName));
                OnPropertyChanged(nameof(ProcessId));
                OnPropertyChanged(nameof(FormattedMemoryUsage));
            }
            else
            {
                // Even if process is null or has exited, notify property changes
                // so the UI updates to show "Unknown" values
                OnPropertyChanged(nameof(ProcessName));
                OnPropertyChanged(nameof(ProcessId));
                OnPropertyChanged(nameof(FormattedMemoryUsage));
            }
        }

        private bool CanCreateDump()
        {
            return _targetProcess != null &&
                   !_targetProcess.HasExited &&
                   (IsMinDumpSelected || IsHeapDumpSelected || IsTriageDumpSelected || IsFullDumpSelected);
        }

        private void CreateDump()
        {
            try
            {
                var result = new DumpOptionsResult
                {
                    DumpType = SelectedDumpType,
                    Options = Options,
                    SizeEstimation = _currentSizeEstimation,
                    Cancelled = false
                };

                _completionSource?.SetResult(result);
                
                // Close the dialog by finding the parent ModernDialog and setting DialogResult
                CloseDialog(true);
            }
            catch (Exception ex)
            {
                _completionSource?.SetException(ex);
            }
        }

        private void Cancel()
        {
            var result = new DumpOptionsResult { Cancelled = true };
            _completionSource?.SetResult(result);
            
            // Close the dialog by finding the parent ModernDialog and setting DialogResult
            CloseDialog(false);
        }

        private void CloseDialog(bool dialogResult)
        {
            // Find the parent ModernDialog window
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var window = Application.Current.Windows.OfType<ModernDialog>()
                    .FirstOrDefault(w => w.Content is Contents.DumpOptions);
                
                if (window != null)
                {
                    window.DialogResult = dialogResult;
                    window.Close();
                }
            });
        }

        private async Task UpdateSizeEstimationAsync()
        {
            if (_dumpCreationService == null || _targetProcess?.HasExited != false)
            {
                SizeEstimationVisibility = Visibility.Collapsed;
                return;
            }

            try
            {
                EstimatedDumpSize = "Calculating...";
                SizeEstimationVisibility = Visibility.Visible;

                var estimation = await _dumpCreationService.EstimateDumpSizeAsync(_targetProcess.Id, SelectedDumpType)
                    .ConfigureAwait(false);

                _currentSizeEstimation = estimation;

                if (estimation.Success)
                {
                    EstimatedDumpSize = estimation.FormattedSizeRange;

                    // Update warnings
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _warnings.Clear();
                        foreach (var warning in estimation.Warnings)
                        {
                            _warnings.Add(warning);
                        }

                        // Add contextual warnings
                        if (SelectedDumpType == DumpType.Full && estimation.EstimatedMaxSizeBytes > 1024L * 1024 * 1024)
                        {
                            _warnings.Add("Full dump may be very large (>1GB). Consider using Heap dump instead.");
                        }
                    });
                }
                else
                {
                    EstimatedDumpSize = "Unable to estimate";
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _warnings.Clear();
                        _warnings.Add("Could not estimate dump size. Process may not be accessible.");
                    });
                }
            }
            catch (Exception ex)
            {
                EstimatedDumpSize = "Estimation failed";
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _warnings.Clear();
                    _warnings.Add($"Size estimation failed: {ex.Message}");
                });
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            if (bytes == 0) return "0 B";

            var place = Convert.ToInt32(Math.Floor(Math.Log(Math.Abs(bytes), 1024)));
            place = Math.Min(place, suffixes.Length - 1);
            var num = Math.Round(bytes / Math.Pow(1024, place), 2);
            return $"{num:F2} {suffixes[place]}";
        }

        private bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        #region INotifyPropertyChanged

        public new event PropertyChangedEventHandler PropertyChanged;

        internal new void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// Represents the result from the dump options dialog.
    /// </summary>
    public sealed class DumpOptionsResult
    {
        /// <summary>
        /// Gets or sets whether the dialog was cancelled.
        /// </summary>
        public bool Cancelled { get; set; }

        /// <summary>
        /// Gets or sets the selected dump type.
        /// </summary>
        public DumpType DumpType { get; set; }

        /// <summary>
        /// Gets or sets the dump creation options.
        /// </summary>
        public DumpCreationOptions Options { get; set; }

        /// <summary>
        /// Gets or sets the size estimation if available.
        /// </summary>
        public DumpSizeEstimation SizeEstimation { get; set; }
    }
}