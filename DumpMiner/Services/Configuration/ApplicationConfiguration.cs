using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DumpMiner.Services.Configuration
{
    /// <summary>
    /// Unified application configuration containing all settings in a hierarchical structure
    /// </summary>
    public sealed class ApplicationConfiguration
    {
        public const string SectionName = "Application";

        /// <summary>
        /// General application settings
        /// </summary>
        public GeneralSettings General { get; set; } = new();

        /// <summary>
        /// Appearance and UI settings
        /// </summary>
        public AppearanceSettings Appearance { get; set; } = new();

        /// <summary>
        /// AI service configuration
        /// </summary>
        public AISettings AI { get; set; } = new();

        /// <summary>
        /// Advanced configuration settings
        /// </summary>
        public AdvancedSettings Advanced { get; set; } = new();
    }

    /// <summary>
    /// General application settings
    /// </summary>
    public sealed class GeneralSettings
    {
        /// <summary>
        /// Path to symbol cache directory
        /// </summary>
        [Required]
        public string SymbolCachePath { get; set; } = @"c:\dev";

        /// <summary>
        /// Default operation timeout in milliseconds
        /// </summary>
        [Range(1000, 300000)]
        public int DefaultTimeoutMs { get; set; } = 60000;

        /// <summary>
        /// Enable automatic updates check
        /// </summary>
        public bool AutoCheckUpdates { get; set; } = true;

        /// <summary>
        /// Maximum number of recent files to track
        /// </summary>
        [Range(5, 50)]
        public int MaxRecentFiles { get; set; } = 10;
    }

    /// <summary>
    /// Appearance and UI configuration
    /// </summary>
    public sealed class AppearanceSettings
    {
        /// <summary>
        /// Application theme (Light, Dark)
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ThemeType Theme { get; set; } = ThemeType.Dark;

        /// <summary>
        /// UI accent color in hex format
        /// </summary>
        [RegularExpression(@"^#[0-9A-Fa-f]{6}$")]
        public string AccentColor { get; set; } = "#1BA1E2";

        /// <summary>
        /// Font size setting
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FontSizeType FontSize { get; set; } = FontSizeType.Normal;

        /// <summary>
        /// Show detailed tooltips
        /// </summary>
        public bool ShowDetailedTooltips { get; set; } = true;

        /// <summary>
        /// Enable animations
        /// </summary>
        public bool EnableAnimations { get; set; } = true;
    }

    /// <summary>
    /// AI service configuration
    /// </summary>
    public sealed class AISettings
    {
        /// <summary>
        /// Default AI provider to use
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AIProviderType DefaultProvider { get; set; } = AIProviderType.OpenAI;

        /// <summary>
        /// Maximum tokens for AI responses
        /// </summary>
        [Range(100, 32000)]
        public int MaxTokens { get; set; } = 4000;

        /// <summary>
        /// Request timeout in seconds
        /// </summary>
        [Range(5, 300)]
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Enable response caching
        /// </summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// Cache expiration in minutes
        /// </summary>
        [Range(1, 1440)]
        public int CacheExpirationMinutes { get; set; } = 60;

        /// <summary>
        /// Maximum conversation history to maintain
        /// </summary>
        [Range(1, 100)]
        public int MaxConversationHistory { get; set; } = 20;

        /// <summary>
        /// Maximum automatic function calls per request
        /// </summary>
        [Range(1, 20)]
        public int MaxAutoFunctionCalls { get; set; } = 5;

        /// <summary>
        /// Maximum object nesting depth for AI analysis
        /// </summary>
        [Range(1, 10)]
        public int MaxObjectAnalysisDepth { get; set; } = 3;

        /// <summary>
        /// Stack analysis specific settings
        /// </summary>
        public StackAnalysisSettings StackAnalysis { get; set; } = new();

        /// <summary>
        /// Provider-specific configurations
        /// </summary>
        public AIProviderConfigurations Providers { get; set; } = new();
    }

    /// <summary>
    /// Stack analysis configuration
    /// </summary>
    public sealed class StackAnalysisSettings
    {
        [Range(1, 50)]
        public int MaxDetailedThreads { get; set; } = 10;

        [Range(5, 100)]
        public int MaxFramesPerThread { get; set; } = 30;

        [Range(10000, 100000)]
        public int MaxTotalPayloadChars { get; set; } = 40000;

        public bool FilterSystemCode { get; set; } = true;
    }

    /// <summary>
    /// Advanced configuration for power users
    /// </summary>
    public sealed class AdvancedSettings
    {
        /// <summary>
        /// Enable debug logging
        /// </summary>
        public bool EnableDebugLogging { get; set; } = false;

        /// <summary>
        /// Log level for application logging (managed by Serilog configuration)
        /// </summary>
        public string LogLevel { get; set; } = "Information";

        /// <summary>
        /// Enable performance profiling
        /// </summary>
        public bool EnableProfiling { get; set; } = false;

        /// <summary>
        /// Maximum log file size in MB
        /// </summary>
        [Range(1, 100)]
        public int MaxLogFileSizeMB { get; set; } = 10;

        /// <summary>
        /// Memory usage warning threshold in MB
        /// </summary>
        [Range(100, 8192)]
        public int MemoryWarningThresholdMB { get; set; } = 1024;
    }

    /// <summary>
    /// AI provider configurations
    /// </summary>
    public sealed class AIProviderConfigurations
    {
        public OpenAIConfiguration OpenAI { get; set; } = new();
        public AnthropicConfiguration Anthropic { get; set; } = new();
        public GoogleConfiguration Google { get; set; } = new();
    }

    /// <summary>
    /// OpenAI provider configuration
    /// </summary>
    public sealed class OpenAIConfiguration
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-4";
        public string BaseUrl { get; set; } = "https://api.openai.com/v1";
        public double Temperature { get; set; } = 0.7;
        public bool IsEnabled { get; set; } = true;
        
        [Range(100, 32000)]
        public int MaxTokens { get; set; } = 4000;
        
        [Range(5, 300)]
        public int TimeoutSeconds { get; set; } = 60;
    }

    /// <summary>
    /// Anthropic Claude provider configuration
    /// </summary>
    public sealed class AnthropicConfiguration
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "claude-3-sonnet-20240229";
        public string BaseUrl { get; set; } = "https://api.anthropic.com";
        public double Temperature { get; set; } = 0.7;
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// Google Gemini provider configuration
    /// </summary>
    public sealed class GoogleConfiguration
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gemini-pro";
        public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
        public double Temperature { get; set; } = 0.7;
        public bool IsEnabled { get; set; } = true;
    }

    // Enums
    public enum ThemeType
    {
        Light,
        Dark
    }

    public enum FontSizeType
    {
        Small,
        Normal,
        Large
    }

    public enum AIProviderType
    {
        OpenAI,
        Anthropic,
        Google
    }


} 