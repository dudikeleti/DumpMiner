using System;
using System.Collections.ObjectModel;
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

        public AISettingsViewModel()
        {
            _configService = ConfigurationService.Instance;
            _aiSettings = _configService.Configuration.AI;

            // Initialize collections
            AvailableProviders = new ObservableCollection<string> { "OpenAI", "Anthropic", "Google" };
            OpenAIModels = new ObservableCollection<string> { "gpt-4", "gpt-3.5-turbo", "gpt-4-turbo" };
            AnthropicModels = new ObservableCollection<string> { "claude-3-sonnet-20240229", "claude-3-haiku-20240307", "claude-3-opus-20240229" };
            GoogleModels = new ObservableCollection<string> { "gemini-pro", "gemini-pro-vision" };

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
        private bool _openAIEnabled;
        public bool OpenAIEnabled
        {
            get => _openAIEnabled;
            set
            {
                if (_openAIEnabled != value)
                {
                    _openAIEnabled = value;
                    OnPropertyChanged();
                    _aiSettings.Providers.OpenAI.IsEnabled = value;
                    SaveSettings();
                }
            }
        }

        private string _openAIApiKey;
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

        private string _selectedOpenAIModel;
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
                    SaveSettings();
                }
            }
        }

        private double _openAITemperature;
        public double OpenAITemperature
        {
            get => _openAITemperature;
            set
            {
                if (_openAITemperature != value)
                {
                    _openAITemperature = value;
                    OnPropertyChanged();
                    _aiSettings.Providers.OpenAI.Temperature = value;
                    SaveSettings();
                }
            }
        }

        // Anthropic Settings
        private bool _anthropicEnabled;
        public bool AnthropicEnabled
        {
            get => _anthropicEnabled;
            set
            {
                if (_anthropicEnabled != value)
                {
                    _anthropicEnabled = value;
                    OnPropertyChanged();
                    _aiSettings.Providers.Anthropic.IsEnabled = value;
                    SaveSettings();
                }
            }
        }

        private string _anthropicApiKey;
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

        private string _selectedAnthropicModel;
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
                    SaveSettings();
                }
            }
        }

        // Google Settings
        private bool _googleEnabled;
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

        private string _googleApiKey;
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

        private string _selectedGoogleModel;
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
                    SaveSettings();
                }
            }
        }

        private void LoadSettings()
        {
            // Load general settings
            SelectedProvider = _aiSettings.DefaultProvider.ToString();
            MaxTokens = _aiSettings.MaxTokens;
            TimeoutSeconds = _aiSettings.TimeoutSeconds;
            EnableCaching = _aiSettings.EnableCaching;
            MaxAutoFunctionCalls = _aiSettings.MaxAutoFunctionCalls;
            MaxObjectAnalysisDepth = _aiSettings.MaxObjectAnalysisDepth;

            // Load OpenAI settings
            OpenAIEnabled = _aiSettings.Providers.OpenAI.IsEnabled;
            OpenAIApiKey = _aiSettings.Providers.OpenAI.ApiKey;
            SelectedOpenAIModel = _aiSettings.Providers.OpenAI.Model;
            OpenAITemperature = _aiSettings.Providers.OpenAI.Temperature;

            // Load Anthropic settings
            AnthropicEnabled = _aiSettings.Providers.Anthropic.IsEnabled;
            AnthropicApiKey = _aiSettings.Providers.Anthropic.ApiKey;
            SelectedAnthropicModel = _aiSettings.Providers.Anthropic.Model;

            // Load Google settings
            GoogleEnabled = _aiSettings.Providers.Google.IsEnabled;
            GoogleApiKey = _aiSettings.Providers.Google.ApiKey;
            SelectedGoogleModel = _aiSettings.Providers.Google.Model;
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
    }
}