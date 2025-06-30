using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DumpMiner.Services.AI.Configuration
{
    /// <summary>
    /// Configuration model for AI service providers and settings
    /// </summary>
    public sealed class AIConfiguration
    {
        public const string SectionName = "AI";

        /// <summary>
        /// Default AI provider to use when none is specified
        /// </summary>
        [Required]
        [JsonConverter(typeof(AIProviderTypeConverter))]
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
        /// Provider-specific configurations
        /// </summary>
        public ProviderConfigurations Providers { get; set; } = new();
    }

    /// <summary>
    /// Provider-specific configuration settings
    /// </summary>
    public sealed class ProviderConfigurations
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
        [Required]
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
        [Required]
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
        [Required]
        public string ApiKey { get; set; } = string.Empty;
        
        public string Model { get; set; } = "gemini-pro";
        public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
        public double Temperature { get; set; } = 0.7;
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// Supported AI provider types
    /// </summary>
    public enum AIProviderType
    {
        OpenAI,
        Anthropic,
        Google
    }
} 