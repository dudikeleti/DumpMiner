using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Infrastructure.Mef;
using DumpMiner.Services.AI;
using FirstFloor.ModernUI.Presentation;
using Microsoft.Extensions.Logging;

namespace DumpMiner.ViewModels
{
    [ViewModel(ViewModelNames.DumpAnalyzerViewModel)]
    class DumpAnalyzerViewModel : BaseViewModel
    {
        private readonly ILogger<DumpAnalyzerViewModel> _logger;
        private IComprehensiveDumpAnalyzer _analyzer;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isInitialized = false;

        public DumpAnalyzerViewModel()
        {
            // Initialize logger
            _logger = LoggingExtensions.CreateLogger<DumpAnalyzerViewModel>();
            
            // Initialize collections immediately
            CriticalIssues = new ObservableCollection<string>();
            Recommendations = new ObservableCollection<ActionableRecommendation>();

            // Set initial state
            AnalysisProgress = 0;
            AnalysisStatus = "Initializing...";
            IsAnalysisRunning = false;

            // Create commands with safe predicates
            StartAnalysisCommand = new RelayCommand(async _ => await StartAnalysisAsync(), _ => CanStartAnalysisSafe());
            CancelAnalysisCommand = new RelayCommand(_ => CancelAnalysis(), _ => IsAnalysisRunning);
            ShowDiagnosticsCommand = new RelayCommand(_ => ShowDiagnostics(), _ => _analyzer != null);

            _logger.LogDebug("DumpAnalyzerViewModel initialized");

            // Initialize analyzer asynchronously to avoid blocking UI
            _ = InitializeAnalyzerAsync();
        }

        /// <summary>
        /// Safely initializes the analyzer without blocking the UI
        /// </summary>
        private async Task InitializeAnalyzerAsync()
        {
            try
            {
                // Defer MEF resolution to avoid blocking constructor
                await Task.Run(() =>
                {
                    try
                    {
                        _analyzer = App.Container?.GetExportedValue<IComprehensiveDumpAnalyzer>();
                    }
                    catch (Exception ex)
                    {
                        // Log the error but don't crash the UI
                        _logger.LogWarning(ex, "Failed to resolve IComprehensiveDumpAnalyzer - analyzer service may not be available");
                    }
                });

                // Back on UI thread
                if (_analyzer != null)
                {
                    _analyzer.ProgressChanged += OnAnalysisProgressChanged;
                    AnalysisStatus = "Ready to analyze";
                }
                else
                {
                    AnalysisStatus = "Analyzer service not available";
                }

                _isInitialized = true;

                // Refresh command states
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    (StartAnalysisCommand as RelayCommand)?.OnCanExecuteChanged();
                });
            }
            catch (Exception ex)
            {
                AnalysisStatus = $"Initialization failed: {ex.Message}";
                _isInitialized = true;
            }
        }

        /// <summary>
        /// Safe version of CanStartAnalysis that won't block the UI
        /// </summary>
        private bool CanStartAnalysisSafe()
        {
            try
            {
                // Don't check debugger state during initialization
                if (!_isInitialized)
                {
                    AnalysisStatus = "Initializing...";
                    return false;
                }

                // Check if we have analyzer
                if (_analyzer == null)
                {
                    AnalysisStatus = "Analysis service not available";
                    return false;
                }

                // Check if already running
                if (IsAnalysisRunning)
                {
                    return false;
                }

                // Safely check debugger state
                var isAttached = IsDebuggerAttachedSafe();
                if (!isAttached)
                {
                    AnalysisStatus = "No debugger session attached - please attach to a process or load a dump file";
                }
                else
                {
                    AnalysisStatus = "Ready to analyze";
                }
                
                return isAttached;
            }
            catch (Exception ex)
            {
                AnalysisStatus = $"Error checking analysis availability: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Safely checks if debugger is attached without blocking
        /// </summary>
        private bool IsDebuggerAttachedSafe()
        {
            try
            {
                return DebuggerSession.Instance?.IsAttached == true;
            }
            catch
            {
                return false;
            }
        }

        // Properties
        private ComprehensiveAnalysisResult _analysisResult;
        public ComprehensiveAnalysisResult AnalysisResult
        {
            get => _analysisResult;
            set
            {
                _analysisResult = value;
                OnPropertyChanged();
                UpdateUIFromResult();
            }
        }

        private int _analysisProgress;
        public int AnalysisProgress
        {
            get => _analysisProgress;
            set
            {
                _analysisProgress = value;
                OnPropertyChanged();
            }
        }

        private string _analysisStatus;
        public string AnalysisStatus
        {
            get => _analysisStatus;
            set
            {
                _analysisStatus = value;
                OnPropertyChanged();
            }
        }

        private bool _isAnalysisRunning;
        public bool IsAnalysisRunning
        {
            get => _isAnalysisRunning;
            set
            {
                _isAnalysisRunning = value;
                OnPropertyChanged();
            }
        }

        private bool _hasAnalysisResults;
        public bool HasAnalysisResults
        {
            get => _hasAnalysisResults;
            set
            {
                _hasAnalysisResults = value;
                OnPropertyChanged();
            }
        }

        private string _rootCauseSummary;
        public string RootCauseSummary
        {
            get => _rootCauseSummary;
            set
            {
                _rootCauseSummary = value;
                OnPropertyChanged();
            }
        }

        private string _aiAnalysis;
        public string AIAnalysis
        {
            get => _aiAnalysis;
            set
            {
                _aiAnalysis = value;
                OnPropertyChanged();
            }
        }

        private string _memoryAnalysis;
        public string MemoryAnalysis
        {
            get => _memoryAnalysis;
            set
            {
                _memoryAnalysis = value;
                OnPropertyChanged();
            }
        }

        private string _threadingAnalysis;
        public string ThreadingAnalysis
        {
            get => _threadingAnalysis;
            set
            {
                _threadingAnalysis = value;
                OnPropertyChanged();
            }
        }

        private string _exceptionAnalysis;
        public string ExceptionAnalysis
        {
            get => _exceptionAnalysis;
            set
            {
                _exceptionAnalysis = value;
                OnPropertyChanged();
            }
        }

        private int _totalObjects;
        public int TotalObjects
        {
            get => _totalObjects;
            set
            {
                _totalObjects = value;
                OnPropertyChanged();
            }
        }

        private string _totalMemoryUsage;
        public string TotalMemoryUsage
        {
            get => _totalMemoryUsage;
            set
            {
                _totalMemoryUsage = value;
                OnPropertyChanged();
            }
        }

        private int _threadCount;
        public int ThreadCount
        {
            get => _threadCount;
            set
            {
                _threadCount = value;
                OnPropertyChanged();
            }
        }

        private int _exceptionCount;
        public int ExceptionCount
        {
            get => _exceptionCount;
            set
            {
                _exceptionCount = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> CriticalIssues { get; }
        public ObservableCollection<ActionableRecommendation> Recommendations { get; }

        // Commands
        public ICommand StartAnalysisCommand { get; }
        public ICommand CancelAnalysisCommand { get; }
        public ICommand ShowDiagnosticsCommand { get; }

        private async Task StartAnalysisAsync()
        {
            if (_analyzer == null)
            {
                AnalysisStatus = "Analyzer service not available";
                return;
            }

            try
            {
                IsAnalysisRunning = true;
                HasAnalysisResults = false;
                ClearPreviousResults();

                _cancellationTokenSource = new CancellationTokenSource();
                AnalysisStatus = "Starting comprehensive analysis...";

                var result = await _analyzer.AnalyzeDumpAsync(_cancellationTokenSource.Token);
                AnalysisResult = result;

                if (result.IsSuccess)
                {
                    AnalysisStatus = "Analysis completed successfully";
                    HasAnalysisResults = true;
                }
                else
                {
                    AnalysisStatus = $"Analysis failed: {result.ErrorMessage}";
                }
            }
            catch (OperationCanceledException)
            {
                AnalysisStatus = "Analysis was cancelled";
            }
            catch (Exception ex)
            {
                AnalysisStatus = $"Analysis error: {ex.Message}";
            }
            finally
            {
                IsAnalysisRunning = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void CancelAnalysis()
        {
            _cancellationTokenSource?.Cancel();
            AnalysisStatus = "Cancelling analysis...";
        }

        private void ShowDiagnostics()
        {
            if (_analyzer != null)
            {
                var diagnosticInfo = _analyzer.GetDiagnosticInfo();
                System.Windows.MessageBox.Show(diagnosticInfo, "AI Diagnostic Information", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }

        private void ClearPreviousResults()
        {
            CriticalIssues.Clear();
            Recommendations.Clear();

            TotalObjects = 0;
            TotalMemoryUsage = "0 bytes";
            ThreadCount = 0;
            ExceptionCount = 0;
            RootCauseSummary = string.Empty;
            AIAnalysis = string.Empty;
            MemoryAnalysis = string.Empty;
            ThreadingAnalysis = string.Empty;
            ExceptionAnalysis = string.Empty;
        }

        private void UpdateUIFromResult()
        {
            if (AnalysisResult == null) return;

            TotalObjects = AnalysisResult.MemoryAnalysis.TotalObjects;
            TotalMemoryUsage = FormatBytes(AnalysisResult.MemoryAnalysis.TotalMemoryUsage);
            ThreadCount = AnalysisResult.ThreadingAnalysis.ThreadCount;
            ExceptionCount = AnalysisResult.ExceptionAnalysis.ExceptionCount;

            foreach (var issue in AnalysisResult.CriticalIssues.Issues)
            {
                CriticalIssues.Add(issue);
            }

            MemoryAnalysis = AnalysisResult.MemoryAnalysis.HeapAnalysisAI;
            ThreadingAnalysis = AnalysisResult.ThreadingAnalysis.StackAnalysisAI;
            ExceptionAnalysis = AnalysisResult.ExceptionAnalysis.DetailedAnalysisAI;
            RootCauseSummary = AnalysisResult.RootCauseAnalysis.RootCause;
            AIAnalysis = AnalysisResult.RootCauseAnalysis.DetailedAnalysis;

            foreach (var recommendation in AnalysisResult.Recommendations)
            {
                Recommendations.Add(recommendation);
            }
        }

        private void OnAnalysisProgressChanged(object sender, AnalysisProgressEventArgs e)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                AnalysisProgress = e.PercentageComplete;
                AnalysisStatus = e.Message;
            });
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
            if (bytes >= 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F2} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024.0:F2} KB";
            return $"{bytes} bytes";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();

                if (_analyzer != null)
                {
                    _analyzer.ProgressChanged -= OnAnalysisProgressChanged;
                }
            }
            base.Dispose(disposing);
        }
    }
}
