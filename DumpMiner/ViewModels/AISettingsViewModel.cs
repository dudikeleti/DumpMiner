using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using DumpMiner.Common;
using DumpMiner.Services.Configuration;
using FirstFloor.ModernUI.Presentation;

namespace DumpMiner.ViewModels
{
    public class AISettingsViewModel : BaseViewModel
    {
        private readonly ConfigurationService _configService;
        private AISettings _aiSettings;

        // Model capability information
        private static readonly Dictionary<string, (string description, int contextWindow, int maxOutput)> OpenAIModelInfo = new()
        {
            // GPT-4.1 Family - Latest models with 1M context
            ["gpt-4.1"] = ("Most capable model with massive context", 1000000, 32768),
            ["gpt-4.1-mini"] = ("High performance, cost-efficient with 1M context", 1000000, 32768),
            ["gpt-4.1-nano"] = ("Fastest, cheapest with 1M context", 1000000, 32768),
            
            // Reasoning Models - Advanced problem-solving
            ["o3"] = ("Advanced reasoning and complex problem-solving", 200000, 100000),
            ["o4-mini"] = ("Fast reasoning model, cost-efficient", 200000, 100000),
            ["o3-mini"] = ("Lightweight reasoning model", 200000, 100000),
            ["o1"] = ("General reasoning model", 200000, 100000),
            
            // GPT-4o Family - Omni-modal capabilities
            ["gpt-4o"] = ("Multimodal model with vision and audio", 128000, 16384),
            ["gpt-4o-mini"] = ("Compact multimodal model", 128000, 16384),
            
            // Other Models
            ["gpt-4.5"] = ("Enhanced general intelligence model", 128000, 16384),
        };

        private static readonly Dictionary<string, (string description, int contextWindow, int maxOutput)> AnthropicModelInfo = new()
        {
            // Claude 4 Family - Most capable
            ["claude-opus-4"] = ("Most capable model for complex tasks", 200000, 32000),
            ["claude-sonnet-4"] = ("High-performance balanced model", 200000, 64000),
            
            // Claude 3.7 Family - Extended thinking
            ["claude-sonnet-3.7"] = ("High intelligence with extended thinking", 200000, 64000),
            
            // Claude 3.5 Family - Previous generation
            ["claude-sonnet-3.5"] = ("Intelligent model for various tasks", 200000, 8192),
            ["claude-haiku-3.5"] = ("Fastest model for simple tasks", 200000, 8192),
        };

        private static readonly Dictionary<string, (string description, int contextWindow, int maxOutput)> GoogleModelInfo = new()
        {
            // Gemini 2.5 Family - Latest and most capable
            ["gemini-2.5-pro"] = ("Most capable for complex reasoning", 1048576, 65536),
            ["gemini-2.5-flash"] = ("Fast and efficient general purpose", 1048576, 65536),
            
            // Gemini 2.0 Family - Previous generation  
            ["gemini-2.0-flash"] = ("Fast general-purpose model", 1048576, 65536),
        };

        public AISettingsViewModel()
        {
            _configService = ConfigurationService.Instance;
            _aiSettings = _configService.Configuration.AI;

            // Initialize collections
            AvailableProviders = new ObservableCollection<string> { "OpenAI", "Anthropic", "Google" };
            
            // OpenAI models - Updated with official specifications
            OpenAIModels = new ObservableCollection<string>(OpenAIModelInfo.Keys);
            
            // Anthropic models - Updated with official specifications  
            AnthropicModels = new ObservableCollection<string>(AnthropicModelInfo.Keys);
            
            // Google models - Updated with official specifications
            GoogleModels = new ObservableCollection<string>(GoogleModelInfo.Keys);

            // Initialize commands
            TestConnectionCommand = new RelayCommand(_ => TestConnection());
            ResetToDefaultsCommand = new RelayCommand(_ => ResetToDefaults());

            LoadSettings();
        }

        // Collections
        public ObservableCollection<string> AvailableProviders { get; }
        public ObservableCollection<string> OpenAIModels { get; }
        public ObservableCollection<string> AnthropicModels { get; }
        public ObservableCollection<string> GoogleModels { get; }

        // Commands
        public ICommand TestConnectionCommand { get; }
        public ICommand ResetToDefaultsCommand { get; }

        // Model capability properties for display
        private string _selectedModelInfo = string.Empty;
        public string SelectedModelInfo
        {
            get => _selectedModelInfo;
            set
            {
                if (_selectedModelInfo != value)
                {
                    _selectedModelInfo = value;
                    OnPropertyChanged();
                }
            }
        }

        // General AI Settings
        private string _selectedProvider;
        public string SelectedProvider
        {
            get => _selectedProvider;
            set
            {
                if (_selectedProvider != value)
                {
                    _selectedProvider = value;
                    OnPropertyChanged();
                    _aiSettings.DefaultProvider = Enum.Parse<AIProviderType>(value);
                    UpdateSelectedModelInfo();
                    SaveSettings();
                }
            }
        }

        private int _maxTokens;
        public int MaxTokens
        {
            get => _maxTokens;
            set
            {
                if (_maxTokens != value)
                {
                    _maxTokens = value;
                    OnPropertyChanged();
                    _aiSettings.MaxTokens = value;
                    SaveSettings();
                }
            }
        }

        private int _timeoutSeconds;
        public int TimeoutSeconds
        {
            get => _timeoutSeconds;
            set
            {
                if (_timeoutSeconds != value)
                {
                    _timeoutSeconds = value;
                    OnPropertyChanged();
                    _aiSettings.TimeoutSeconds = value;
                    SaveSettings();
                }
            }
        }

        private bool _enableCaching;
        public bool EnableCaching
        {
            get => _enableCaching;
            set
            {
                if (_enableCaching != value)
                {
                    _enableCaching = value;
                    OnPropertyChanged();
                    _aiSettings.EnableCaching = value;
                    SaveSettings();
                }
            }
        }

        private int _maxAutoFunctionCalls;
        public int MaxAutoFunctionCalls
        {
            get => _maxAutoFunctionCalls;
            set
            {
                if (_maxAutoFunctionCalls != value)
                {
                    _maxAutoFunctionCalls = value;
                    OnPropertyChanged();
                    _aiSettings.MaxAutoFunctionCalls = value;
                    SaveSettings();
                }
            }
        }

        private int _maxObjectAnalysisDepth;
        public int MaxObjectAnalysisDepth
        {
            get => _maxObjectAnalysisDepth;
            set
            {
                if (_maxObjectAnalysisDepth != value)
                {
                    _maxObjectAnalysisDepth = value;
                    OnPropertyChanged();
                    _aiSettings.MaxObjectAnalysisDepth = value;
                    SaveSettings();
                }
            }
        }

        // OpenAI Settings
        private bool _isOpenAIEnabled;
        private string _openAIApiKey = string.Empty;
        private string _selectedOpenAIModel = string.Empty;

        // OpenAI Properties
        public bool IsOpenAIEnabled
        {
            get => _isOpenAIEnabled;
            set
            {
                if (_isOpenAIEnabled != value)
                {
                    _isOpenAIEnabled = value;
                    OnPropertyChanged();
                    _aiSettings.Providers.OpenAI.IsEnabled = value;
                    SaveSettings();
                }
            }
        }

        public string OpenAIApiKey
        {
            get => _openAIApiKey;
            set
            {
                if (_openAIApiKey != value)
                {
                    _openAIApiKey = value;
                    OnPropertyChanged();
                    _aiSettings.Providers.OpenAI.ApiKey = value;
                    SaveSettings();
                }
            }
        }

        public string SelectedOpenAIModel
        {
            get => _selectedOpenAIModel;
            set
            {
                if (_selectedOpenAIModel != value)
                {
                    _selectedOpenAIModel = value;
                    OnPropertyChanged();
                    _aiSettings.Providers.OpenAI.Model = value;
                    UpdateSelectedModelInfo();
                    SaveSettings();
                }
            }
        }

        // Anthropic Settings
        private bool _isAnthropicEnabled;
        private string _anthropicApiKey = string.Empty;
        private string _selectedAnthropicModel = string.Empty;

        // Anthropic properties
        public bool IsAnthropicEnabled
        {
            get => _isAnthropicEnabled;
            set
            {
                if (_isAnthropicEnabled != value)
                {
                    _isAnthropicEnabled = value;
                    OnPropertyChanged();
                    _aiSettings.Providers.Anthropic.IsEnabled = value;
                    SaveSettings();
                }
            }
        }

        public string AnthropicApiKey
        {
            get => _anthropicApiKey;
            set
            {
                if (_anthropicApiKey != value)
                {
                    _anthropicApiKey = value;
                    OnPropertyChanged();
                    _aiSettings.Providers.Anthropic.ApiKey = value;
                    SaveSettings();
                }
            }
        }

        public string SelectedAnthropicModel
        {
            get => _selectedAnthropicModel;
            set
            {
                if (_selectedAnthropicModel != value)
                {
                    _selectedAnthropicModel = value;
                    OnPropertyChanged();
                    _aiSettings.Providers.Anthropic.Model = value;
                    UpdateSelectedModelInfo();
                    SaveSettings();
                }
            }
        }

        // Google Settings
        private bool _googleEnabled;
        private string _googleApiKey = string.Empty;
        private string _selectedGoogleModel = string.Empty;

        // Google properties
        public bool GoogleEnabled
        {
            get => _googleEnabled;
            set
            {
                if (_googleEnabled != value)
                {
                    _googleEnabled = value;
                    OnPropertyChanged();
                    _aiSettings.Providers.Google.IsEnabled = value;
                    SaveSettings();
                }
            }
        }

        public string GoogleApiKey
        {
            get => _googleApiKey;
            set
            {
                if (_googleApiKey != value)
                {
                    _googleApiKey = value;
                    OnPropertyChanged();
                    _aiSettings.Providers.Google.ApiKey = value;
                    SaveSettings();
                }
            }
        }

        public string SelectedGoogleModel
        {
            get => _selectedGoogleModel;
            set
            {
                if (_selectedGoogleModel != value)
                {
                    _selectedGoogleModel = value;
                    OnPropertyChanged();
                    _aiSettings.Providers.Google.Model = value;
                    UpdateSelectedModelInfo();
                    SaveSettings();
                }
            }
        }

        private void LoadSettings()
        {
            try
            {
                _aiSettings = _configService.Configuration.AI;

                // Load general settings
                SelectedProvider = _aiSettings.DefaultProvider.ToString();
                MaxTokens = _aiSettings.MaxTokens;
                TimeoutSeconds = _aiSettings.TimeoutSeconds;
                EnableCaching = _aiSettings.EnableCaching;
                MaxAutoFunctionCalls = _aiSettings.MaxAutoFunctionCalls;
                MaxObjectAnalysisDepth = _aiSettings.MaxObjectAnalysisDepth;

                // Load OpenAI settings
                IsOpenAIEnabled = _aiSettings.Providers.OpenAI.IsEnabled;
                OpenAIApiKey = _aiSettings.Providers.OpenAI.ApiKey;
                SelectedOpenAIModel = _aiSettings.Providers.OpenAI.Model;

                // Load Anthropic settings
                IsAnthropicEnabled = _aiSettings.Providers.Anthropic.IsEnabled;
                AnthropicApiKey = _aiSettings.Providers.Anthropic.ApiKey;
                SelectedAnthropicModel = _aiSettings.Providers.Anthropic.Model;

                // Load Google settings
                GoogleEnabled = _aiSettings.Providers.Google.IsEnabled;
                GoogleApiKey = _aiSettings.Providers.Google.ApiKey;
                SelectedGoogleModel = _aiSettings.Providers.Google.Model;

                UpdateSelectedModelInfo();
            }
            catch (Exception ex)
            {
                // Log error but don't crash the UI
                System.Diagnostics.Debug.WriteLine($"Error loading AI settings: {ex.Message}");
            }
        }

        private void SaveSettings()
        {
            _configService.SaveConfiguration();
        }

        private void TestConnection()
        {
            // Implementation for testing AI provider connection
            // This would typically make a test API call to verify connectivity
            System.Windows.MessageBox.Show("Test connection functionality to be implemented", "Test Connection",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        private void ResetToDefaults()
        {
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to reset all AI settings to defaults? This action cannot be undone.",
                "Reset to Defaults",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _configService.ResetSection("AI");
                _aiSettings = _configService.Configuration.AI;
                LoadSettings();
            }
        }

        private void UpdateSelectedModelInfo()
        {
            var info = GetCurrentModelInfo();
            if (info.HasValue)
            {
                var contextMB = info.Value.contextWindow >= 1000000 ? $"{info.Value.contextWindow / 1000000}M" : $"{info.Value.contextWindow / 1000}K";
                var outputMB = info.Value.maxOutput >= 1000000 ? $"{info.Value.maxOutput / 1000000}M" : $"{info.Value.maxOutput / 1000}K";
                SelectedModelInfo = $"{info.Value.description} • Context: {contextMB} tokens • Max Output: {outputMB} tokens";
            }
            else
            {
                SelectedModelInfo = "Select a model to see capabilities";
            }
        }

        private (string description, int contextWindow, int maxOutput)? GetCurrentModelInfo()
        {
            return SelectedProvider switch
            {
                "OpenAI" when !string.IsNullOrEmpty(SelectedOpenAIModel) && OpenAIModelInfo.ContainsKey(SelectedOpenAIModel) 
                    => OpenAIModelInfo[SelectedOpenAIModel],
                "Anthropic" when !string.IsNullOrEmpty(SelectedAnthropicModel) && AnthropicModelInfo.ContainsKey(SelectedAnthropicModel) 
                    => AnthropicModelInfo[SelectedAnthropicModel],
                "Google" when !string.IsNullOrEmpty(SelectedGoogleModel) && GoogleModelInfo.ContainsKey(SelectedGoogleModel) 
                    => GoogleModelInfo[SelectedGoogleModel],
                _ => null
            };
        }
    }
}