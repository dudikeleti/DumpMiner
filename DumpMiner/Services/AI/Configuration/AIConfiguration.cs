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
        /// Maximum tokens for AI responses - optimized for debugging scenarios
        /// </summary>
        [Range(100, 200000)]
        public int MaxTokens { get; set; } = 16000;

        /// <summary>
        /// Request timeout in seconds - accounts for complex debugging operations
        /// </summary>
        [Range(30, 600)]
        public int TimeoutSeconds { get; set; } = 180;

        /// <summary>
        /// Overall orchestration timeout in seconds - total time for all AI operations
        /// </summary>
        [Range(60, 1800)]
        public int OrchestrationTimeoutSeconds { get; set; } = 600;

        /// <summary>
        /// Maximum auto function calls per analysis - prevents infinite loops
        /// </summary>
        [Range(1, 10)]
        public int MaxAutoFunctionCalls { get; set; } = 5;

        /// <summary>
        /// Maximum object analysis depth - how deep to investigate object hierarchies
        /// </summary>
        [Range(1, 10)]
        public int MaxObjectAnalysisDepth { get; set; } = 3;

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
        /// Stack analysis specific settings
        /// </summary>
        public StackAnalysisSettings StackAnalysis { get; set; } = new();

        /// <summary>
        /// Provider-specific configurations
        /// </summary>
        public ProviderConfigurations Providers { get; set; } = new();
    }

    /// <summary>
    /// Settings specific to stack analysis operations
    /// </summary>
    public sealed class StackAnalysisSettings
    {
        /// <summary>
        /// Maximum detailed threads to analyze
        /// </summary>
        [Range(1, 50)]
        public int MaxDetailedThreads { get; set; } = 10;

        /// <summary>
        /// Maximum stack frames per thread
        /// </summary>
        [Range(10, 200)]
        public int MaxFramesPerThread { get; set; } = 30;

        /// <summary>
        /// Maximum total payload characters for AI analysis
        /// </summary>
        [Range(10000, 200000)]
        public int MaxTotalPayloadChars { get; set; } = 40000;

        /// <summary>
        /// Filter out system code from analysis
        /// </summary>
        public bool FilterSystemCode { get; set; } = true;
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
        
        public string Model { get; set; } = "o3";
        public string BaseUrl { get; set; } = "https://api.openai.com/v1";
        
        /// <summary>
        /// Temperature - optimized for coding/debugging (0.2 for deterministic, accurate responses)
        /// </summary>
        [Range(0.0, 2.0)]
        public double Temperature { get; set; } = 0.2;
        
        /// <summary>
        /// Top-p nucleus sampling - optimized for coding (0.1 for focused responses)
        /// </summary>
        [Range(0.0, 1.0)]
        public double TopP { get; set; } = 0.1;
        
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// Maximum tokens for responses - optimized for debugging scenarios
        /// </summary>
        [Range(100, 200000)]
        public int MaxTokens { get; set; } = 16000;
        
        /// <summary>
        /// Individual API call timeout - accounts for complex analysis
        /// </summary>
        [Range(30, 600)]
        public int TimeoutSeconds { get; set; } = 180;
    }

    /// <summary>
    /// Anthropic Claude provider configuration
    /// </summary>
    public sealed class AnthropicConfiguration
    {
        [Required]
        public string ApiKey { get; set; } = string.Empty;
        
        public string Model { get; set; } = "claude-sonnet-4";
        public string BaseUrl { get; set; } = "https://api.anthropic.com";
        
        /// <summary>
        /// Temperature - optimized for coding/debugging (0.2 for technical precision)
        /// </summary>
        [Range(0.0, 1.0)]
        public double Temperature { get; set; } = 0.2;
        
        /// <summary>
        /// Top-p nucleus sampling - optimized for coding (0.99 for quality responses)
        /// </summary>
        [Range(0.0, 1.0)]
        public double TopP { get; set; } = 0.99;
        
        public bool IsEnabled { get; set; } = false;
        
        /// <summary>
        /// Maximum tokens for responses - optimized for debugging scenarios
        /// </summary>
        [Range(100, 200000)]
        public int MaxTokens { get; set; } = 16000;
        
        /// <summary>
        /// Individual API call timeout - accounts for complex analysis
        /// </summary>
        [Range(30, 600)]
        public int TimeoutSeconds { get; set; } = 180;
    }

    /// <summary>
    /// Google Gemini provider configuration
    /// </summary>
    public sealed class GoogleConfiguration
    {
        [Required]
        public string ApiKey { get; set; } = string.Empty;
        
        public string Model { get; set; } = "gemini-2.5-pro";
        public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
        
        /// <summary>
        /// Temperature - optimized for coding/debugging (0.2 for technical precision)
        /// </summary>
        [Range(0.0, 1.0)]
        public double Temperature { get; set; } = 0.2;
        
        /// <summary>
        /// Top-p nucleus sampling - optimized for coding (0.95 for coherent responses)
        /// </summary>
        [Range(0.0, 1.0)]
        public double TopP { get; set; } = 0.95;
        
        /// <summary>
        /// Top-k sampling - optimized for coding (30 for focused token selection)
        /// </summary>
        [Range(1, 100)]
        public int TopK { get; set; } = 30;
        
        public bool IsEnabled { get; set; } = false;
        
        /// <summary>
        /// Maximum tokens for responses - optimized for debugging scenarios
        /// </summary>
        [Range(100, 200000)]
        public int MaxTokens { get; set; } = 16000;
        
        /// <summary>
        /// Individual API call timeout - accounts for complex analysis
        /// </summary>
        [Range(30, 600)]
        public int TimeoutSeconds { get; set; } = 180;
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