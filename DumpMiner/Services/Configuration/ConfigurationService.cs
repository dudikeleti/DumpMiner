using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DumpMiner.Services.Configuration
{
    /// <summary>
    /// Unified configuration service that manages all application settings
    /// </summary>
    public sealed class ConfigurationService : INotifyPropertyChanged
    {
        private static readonly Lazy<ConfigurationService> _instance = new(() => new ConfigurationService());
        public static ConfigurationService Instance => _instance.Value;

        private ApplicationConfiguration _configuration;
        private readonly string _configurationFilePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public event PropertyChangedEventHandler PropertyChanged;

        private ConfigurationService()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null, // Use PascalCase to match root appsettings.json
                PropertyNameCaseInsensitive = true, // Allow reading both cases during migration
                Converters = { new JsonStringEnumConverter() }
            };

            // Use root appsettings.json file for consistent configuration
            var baseDirectory = Path.GetDirectoryName(typeof(ConfigurationService).Assembly.Location);
            _configurationFilePath = Path.Combine(baseDirectory, "appsettings.json");

            LoadConfiguration();
        }

        /// <summary>
        /// Current application configuration
        /// </summary>
        public ApplicationConfiguration Configuration
        {
            get => _configuration;
            private set
            {
                _configuration = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Load configuration from file, migrating from legacy settings if needed
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configurationFilePath))
                {
                    var json = File.ReadAllText(_configurationFilePath);
                    
                    // Try to deserialize with "Application" wrapper structure first
                    try 
                    {
                        var wrapper = JsonSerializer.Deserialize<ApplicationConfigurationWrapper>(json, _jsonOptions);
                        _configuration = wrapper?.Application ?? new ApplicationConfiguration();
                    }
                    catch 
                    {
                        // Fallback to direct ApplicationConfiguration structure
                        _configuration = JsonSerializer.Deserialize<ApplicationConfiguration>(json, _jsonOptions)
                                       ?? new ApplicationConfiguration();
                    }
                }
                else
                {
                    _configuration = new ApplicationConfiguration();
                    SaveConfiguration();
                }

                ValidateConfiguration();
            }
            catch (Exception ex)
            {
                // Log error and use defaults
                System.Diagnostics.Debug.WriteLine($"Error loading configuration: {ex.Message}");
                _configuration = new ApplicationConfiguration();
                SaveConfiguration();
            }
        }

        /// <summary>
        /// Validate configuration settings
        /// </summary>
        private void ValidateConfiguration()
        {
            var context = new ValidationContext(_configuration, serviceProvider: null, items: null);
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateObject(_configuration, context, results, true))
            {
                var errors = string.Join(Environment.NewLine, results.Select(r => r.ErrorMessage));
                System.Diagnostics.Debug.WriteLine($"Configuration validation errors: {errors}");

                // Reset to defaults for invalid configurations
                _configuration = new ApplicationConfiguration();
            }
        }

        /// <summary>
        /// Save configuration to file
        /// </summary>
        public void SaveConfiguration()
        {
            try
            {
                ValidateConfiguration();
                
                // Save with "Application" wrapper structure to match root appsettings.json format
                var wrapper = new ApplicationConfigurationWrapper { Application = _configuration };
                var json = JsonSerializer.Serialize(wrapper, _jsonOptions);
                File.WriteAllText(_configurationFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving configuration: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Update a setting and save to file
        /// </summary>
        public void UpdateSetting<T>(T newValue, [CallerMemberName] string settingName = null)
        {
            if (settingName == null) return;

            try
            {
                // Use reflection to update the setting
                var parts = settingName.Split('.');
                object target = _configuration;

                for (int i = 0; i < parts.Length - 1; i++)
                {
                    var property = target.GetType().GetProperty(parts[i]);
                    target = property?.GetValue(target);
                    if (target == null) return;
                }

                var finalProperty = target.GetType().GetProperty(parts[parts.Length - 1]);
                finalProperty?.SetValue(target, newValue);

                SaveConfiguration();
                NotifyPropertyChanged(settingName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating setting {settingName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset all settings to defaults
        /// </summary>
        public void ResetToDefaults()
        {
            Configuration = new ApplicationConfiguration();
            SaveConfiguration();
        }

        /// <summary>
        /// Reset a specific section to defaults
        /// </summary>
        public void ResetSection(string sectionName)
        {
            switch (sectionName.ToLower())
            {
                case "general":
                    _configuration.General = new GeneralSettings();
                    break;
                case "appearance":
                    _configuration.Appearance = new AppearanceSettings();
                    break;
                case "ai":
                    _configuration.AI = new AISettings();
                    break;
                case "advanced":
                    _configuration.Advanced = new AdvancedSettings();
                    break;
            }

            SaveConfiguration();
            NotifyPropertyChanged($"Configuration.{sectionName}");
        }

        /// <summary>
        /// Export configuration to file
        /// </summary>
        public void ExportConfiguration(string filePath)
        {
            var wrapper = new ApplicationConfigurationWrapper { Application = _configuration };
            var json = JsonSerializer.Serialize(wrapper, _jsonOptions);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Import configuration from file
        /// </summary>
        public void ImportConfiguration(string filePath)
        {
            if (!File.Exists(filePath)) return;

            var json = File.ReadAllText(filePath);
            ApplicationConfiguration imported = null;
            
            // Try both wrapper and direct structures
            try 
            {
                var wrapper = JsonSerializer.Deserialize<ApplicationConfigurationWrapper>(json, _jsonOptions);
                imported = wrapper?.Application;
            }
            catch 
            {
                imported = JsonSerializer.Deserialize<ApplicationConfiguration>(json, _jsonOptions);
            }

            if (imported != null)
            {
                Configuration = imported;
                SaveConfiguration();
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Wrapper class for the "Application" section structure in appsettings.json
    /// </summary>
    internal class ApplicationConfigurationWrapper
    {
        public ApplicationConfiguration Application { get; set; } = new();
    }

    /// <summary>
    /// Extension methods for validation
    /// </summary>
    public static class ValidationExtensions
    {
        public static bool TryValidateObjectRecursively<T>(T obj, ValidationContext context, ICollection<ValidationResult> results)
        {
            return Validator.TryValidateObject(obj, context, results, true);
        }
    }
}