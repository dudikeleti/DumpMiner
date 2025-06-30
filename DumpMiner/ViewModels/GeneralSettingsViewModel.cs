using DumpMiner.Common;
using DumpMiner.Services.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Serilog;
using Serilog.Events;
using FirstFloor.ModernUI.Presentation;

namespace DumpMiner.ViewModels
{
    /// <summary>
    /// View model for general application settings
    /// </summary>
    public class GeneralSettingsViewModel : BaseViewModel
    {
        private readonly ConfigurationService _configService;
        private GeneralSettings _settings;
        private AdvancedSettings _advancedSettings;

        public GeneralSettingsViewModel()
        {
            _configService = ConfigurationService.Instance;
            _settings = _configService.Configuration.General;
            _advancedSettings = _configService.Configuration.Advanced;
            LoadSettings();
            InitializeCommands();
        }

        public string SymbolCache
        {
            get => _settings.SymbolCachePath;
            set
            {
                if (_settings.SymbolCachePath != value)
                {
                    _settings.SymbolCachePath = value;
                    _configService.SaveConfiguration();
                    OnPropertyChanged();
                }
            }
        }

        public int DefaultTimeoutMs
        {
            get => _settings.DefaultTimeoutMs;
            set
            {
                if (_settings.DefaultTimeoutMs != value)
                {
                    _settings.DefaultTimeoutMs = value;
                    _configService.SaveConfiguration();
                    OnPropertyChanged();
                }
            }
        }

        public bool AutoCheckUpdates
        {
            get => _settings.AutoCheckUpdates;
            set
            {
                if (_settings.AutoCheckUpdates != value)
                {
                    _settings.AutoCheckUpdates = value;
                    _configService.SaveConfiguration();
                    OnPropertyChanged();
                }
            }
        }

        public int MaxRecentFiles
        {
            get => _settings.MaxRecentFiles;
            set
            {
                if (_settings.MaxRecentFiles != value)
                {
                    _settings.MaxRecentFiles = value;
                    _configService.SaveConfiguration();
                    OnPropertyChanged();
                }
            }
        }

        public string LogLevel
        {
            get => _advancedSettings.LogLevel;
            set
            {
                if (_advancedSettings.LogLevel != value)
                {
                    _advancedSettings.LogLevel = value;
                    _configService.SaveConfiguration();
                    OnPropertyChanged();
                    
                    // Update Serilog level at runtime
                    UpdateSerilogLevel(value);
                }
            }
        }

        public bool EnableFileLogging
        {
            get => !string.IsNullOrEmpty(GetLogFilePath());
            set
            {
                // This is a read-only computed property based on Serilog configuration
                // In a full implementation, you'd need to modify the Serilog configuration
                OnPropertyChanged();
            }
        }

        public bool EnableConsoleLogging
        {
            get => true; // Default enabled, could be made configurable
            set
            {
                // This is a read-only computed property based on Serilog configuration
                OnPropertyChanged();
            }
        }

        public int LogFileRetentionDays
        {
            get => 30; // From current Serilog config
            set
            {
                // In a full implementation, you'd update the Serilog configuration
                OnPropertyChanged();
            }
        }

        public int MemoryWarningThresholdMB
        {
            get => _advancedSettings.MemoryWarningThresholdMB;
            set
            {
                if (_advancedSettings.MemoryWarningThresholdMB != value)
                {
                    _advancedSettings.MemoryWarningThresholdMB = value;
                    _configService.SaveConfiguration();
                    OnPropertyChanged();
                }
            }
        }

        public bool EnableProfiling
        {
            get => _advancedSettings.EnableProfiling;
            set
            {
                if (_advancedSettings.EnableProfiling != value)
                {
                    _advancedSettings.EnableProfiling = value;
                    _configService.SaveConfiguration();
                    OnPropertyChanged();
                }
            }
        }

        public string LogFileLocation
        {
            get
            {
                var logPath = GetLogFilePath();
                return string.IsNullOrEmpty(logPath) 
                    ? "Log files are not currently being written to disk" 
                    : $"Log files location: {logPath}";
            }
        }

        public RelayCommand OpenLogDirectoryCommand { get; private set; }
        public RelayCommand ClearOldLogsCommand { get; private set; }

        private void InitializeCommands()
        {
            OpenLogDirectoryCommand = new RelayCommand(_ => OpenLogDirectory(), _ => true);
            ClearOldLogsCommand = new RelayCommand(_ => ClearOldLogs(), _ => true);
        }

        private void LoadSettings()
        {
            // Trigger property change notifications to update UI
            OnPropertyChanged(nameof(SymbolCache));
            OnPropertyChanged(nameof(DefaultTimeoutMs));
            OnPropertyChanged(nameof(AutoCheckUpdates));
            OnPropertyChanged(nameof(MaxRecentFiles));
            
            // New logging properties
            OnPropertyChanged(nameof(LogLevel));
            OnPropertyChanged(nameof(EnableFileLogging));
            OnPropertyChanged(nameof(EnableConsoleLogging));
            OnPropertyChanged(nameof(LogFileRetentionDays));
            OnPropertyChanged(nameof(MemoryWarningThresholdMB));
            OnPropertyChanged(nameof(EnableProfiling));
            OnPropertyChanged(nameof(LogFileLocation));
        }

        private void UpdateSerilogLevel(string level)
        {
            try
            {
                if (Enum.TryParse<LogEventLevel>(level, out var logEventLevel))
                {
                    // Update the global minimum level
                    var levelSwitch = new Serilog.Core.LoggingLevelSwitch(logEventLevel);
                    Log.Logger = Log.Logger.ForContext("MinimumLevel", levelSwitch);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to update Serilog level to {Level}", level);
            }
        }

        private string GetLogFilePath()
        {
            try
            {
                var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs");
                return Directory.Exists(logDirectory) ? logDirectory : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void OpenLogDirectory()
        {
            try
            {
                var logPath = GetLogFilePath();
                if (!string.IsNullOrEmpty(logPath) && Directory.Exists(logPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = logPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show("Log directory not found or logging to file is disabled.", 
                                  "Log Directory", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open log directory: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }

        private void ClearOldLogs()
        {
            try
            {
                var logPath = GetLogFilePath();
                if (string.IsNullOrEmpty(logPath) || !Directory.Exists(logPath))
                {
                    MessageBox.Show("Log directory not found.", "Clear Logs", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show("Are you sure you want to delete old log files?\n\nThis action cannot be undone.", 
                                           "Confirm Delete", 
                                           MessageBoxButton.YesNo, 
                                           MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var logFiles = Directory.GetFiles(logPath, "*.log");
                    var deletedCount = 0;

                    // Keep logs from last 3 days, delete older ones
                    var cutoffDate = DateTime.Now.AddDays(-3);
                    
                    foreach (var file in logFiles)
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTime < cutoffDate)
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                    }

                    MessageBox.Show($"Deleted {deletedCount} old log files.", 
                                  "Clear Logs", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to clear logs: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }
    }
}
