using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DumpMiner.Services.AI.Configuration;
using DumpMiner.Services.AI.Context;
using DumpMiner.Services.AI.Interfaces;
using DumpMiner.Services.AI.Orchestration;
using DumpMiner.Services.AI.Caching;
using DumpMiner.Services.AI.Functions;
using DumpMiner.Services.AI.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace DumpMiner.Services.AI
{
    /// <summary>
    /// Service registration helper for AI services
    /// </summary>
    public static class ServiceRegistration
    {
        /// <summary>
        /// Configures AI services in the dependency injection container
        /// </summary>
        public static IServiceCollection AddAIServices(this IServiceCollection services)
        {
            // Load configuration
            var configuration = LoadConfiguration();
            services.AddSingleton(configuration);

            // Register core services
            services.AddSingleton<IMemoryCache, MemoryCache>();
            services.AddSingleton<IDumpAnalysisContextBuilder, DumpAnalysisContextBuilder>();
            services.AddSingleton<IAIServiceManager, AIServiceManager>();

            // Register providers
            services.AddTransient<OpenAIProvider>();
            services.AddTransient<AnthropicProvider>();
            services.AddTransient<GoogleProvider>();
            
            // Register new AI orchestration services
            services.AddSingleton<IAIOrchestrator, AIOrchestrator>();
            services.AddSingleton<IOperationContextBuilder, OperationContextBuilder>();
            services.AddSingleton<IAICacheService, AICacheService>();
            services.AddSingleton<IAIFunctionRegistry, AIFunctionRegistry>();

            // Add Serilog logging integration
            services.AddLogging(builder => builder.AddSerilog());

            return services;
        }

        /// <summary>
        /// Creates a simple service provider for WPF integration
        /// </summary>
        public static IServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddAIServices();
            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Initializes AI service manager with providers
        /// </summary>
        public static async Task<IAIServiceManager> CreateAIServiceManagerAsync()
        {
            var serviceProvider = CreateServiceProvider();
            var aiServiceManager = serviceProvider.GetRequiredService<IAIServiceManager>();
            
            var configuration = serviceProvider.GetRequiredService<AIConfiguration>();

            // Register OpenAI provider if enabled
            if (configuration.Providers.OpenAI.IsEnabled && !string.IsNullOrEmpty(configuration.Providers.OpenAI.ApiKey))
            {
                var openAIProvider = serviceProvider.GetRequiredService<OpenAIProvider>();
                await openAIProvider.InitializeAsync(configuration.Providers.OpenAI);
                await aiServiceManager.RegisterProviderAsync(openAIProvider);
            }

            // Register Anthropic provider if enabled
            if (configuration.Providers.Anthropic.IsEnabled && !string.IsNullOrEmpty(configuration.Providers.Anthropic.ApiKey))
            {
                var anthropicProvider = serviceProvider.GetRequiredService<AnthropicProvider>();
                await anthropicProvider.InitializeAsync(configuration.Providers.Anthropic);
                await aiServiceManager.RegisterProviderAsync(anthropicProvider);
            }

            // Register Google provider if enabled
            if (configuration.Providers.Google.IsEnabled && !string.IsNullOrEmpty(configuration.Providers.Google.ApiKey))
            {
                var googleProvider = serviceProvider.GetRequiredService<GoogleProvider>();
                await googleProvider.InitializeAsync(configuration.Providers.Google);
                await aiServiceManager.RegisterProviderAsync(googleProvider);
            }

            return aiServiceManager;
        }

        /// <summary>
        /// Creates a simple AI helper for backward compatibility
        /// </summary>
        public static async Task<AIHelper> CreateAIHelperAsync()
        {
            var aiServiceManager = await CreateAIServiceManagerAsync();
            return new AIHelper(aiServiceManager);
        }

        private static AIConfiguration LoadConfiguration()
        {
            var logger = Log.ForContext(typeof(ServiceRegistration));
            
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                logger.Debug("Looking for AI configuration at {ConfigPath}", configPath);
                
                if (File.Exists(configPath))
                {
                    logger.Debug("Config file found, loading AI configuration");
                    var json = File.ReadAllText(configPath);
                    logger.Debug("JSON content loaded, length: {ContentLength} characters", json.Length);
                    
                    // Add more detailed debugging
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    };
                    
                    // Add custom converter for enum handling
                    options.Converters.Add(new AIProviderTypeConverter());
                    
                    logger.Debug("Attempting to deserialize AI configuration JSON");
                    
                    // Try two structures: direct AI config or nested under Application
                    AIConfiguration config = null;
                    
                    try
                    {
                        // First try: Application.AI structure (current user's structure)
                        var appConfigRoot = JsonSerializer.Deserialize<ApplicationConfigurationRoot>(json, options);
                        if (appConfigRoot?.Application?.AI != null)
                        {
                            config = appConfigRoot.Application.AI;
                            logger.Information("Successfully loaded AI config from Application.AI structure");
                        }
                    }
                    catch (Exception appEx)
                    {
                        logger.Debug(appEx, "Application.AI configuration structure parsing failed");
                    }
                    
                    if (config == null)
                    {
                        try
                        {
                            // Second try: direct AI structure
                            var configRoot = JsonSerializer.Deserialize<ConfigurationRoot>(json, options);
                            config = configRoot?.AI ?? new AIConfiguration();
                            logger.Information("Successfully loaded AI config from direct AI structure");
                        }
                        catch (Exception directEx)
                        {
                            logger.Debug(directEx, "Direct AI configuration structure parsing failed");
                        }
                    }
                    
                    if (config == null)
                    {
                        logger.Warning("Both configuration structures failed, using default AI configuration");
                        config = new AIConfiguration();
                    }
                    
                    logger.Information("AI Configuration loaded - OpenAI: {OpenAIConfigured}, Anthropic: {AnthropicConfigured}, Google: {GoogleConfigured}, Default Provider: {DefaultProvider}",
                        !string.IsNullOrEmpty(config.Providers.OpenAI.ApiKey),
                        !string.IsNullOrEmpty(config.Providers.Anthropic.ApiKey),
                        !string.IsNullOrEmpty(config.Providers.Google.ApiKey),
                        config.DefaultProvider);
                    
                    return config;
                }
                else
                {
                    logger.Warning("AI configuration file not found at {ConfigPath}, using defaults", configPath);
                }
            }
            catch (JsonException jsonEx)
            {
                logger.Error(jsonEx, "JSON parsing error in AI configuration at Path: {Path}, Line: {LineNumber}, Position: {BytePosition}",
                    jsonEx.Path, jsonEx.LineNumber, jsonEx.BytePositionInLine);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error loading AI configuration");
            }

            return new AIConfiguration();
        }

        private class ConfigurationRoot
        {
            public AIConfiguration AI { get; set; } = new();
        }

        // Support for Application.AI structure
        private class ApplicationConfigurationRoot
        {
            public ApplicationSection Application { get; set; } = new();
        }

        private class ApplicationSection
        {
            public AIConfiguration AI { get; set; } = new();
        }
    }

    /// <summary>
    /// Simple AI helper class for backward compatibility with old Gpt.Ask pattern
    /// </summary>
    public class AIHelper
    {
        internal readonly IAIServiceManager _aiServiceManager;

        public AIHelper(IAIServiceManager aiServiceManager)
        {
            _aiServiceManager = aiServiceManager;
        }

        /// <summary>
        /// Gets the underlying AI service manager
        /// </summary>
        public IAIServiceManager ServiceManager => _aiServiceManager;

        /// <summary>
        /// Simple Ask method compatible with old Gpt.Ask usage
        /// </summary>
        public async Task<string> Ask(string[] systemPrompts, string[] userPrompts)
        {
            try
            {
                var systemPrompt = string.Join("\n\n", systemPrompts);
                var userPrompt = string.Join("\n\n", userPrompts);
                
                return await _aiServiceManager.AskAsync(systemPrompt, userPrompt);
            }
            catch (Exception ex)
            {
                // Return error message similar to old implementation
                return $"AI Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Simple ask with single prompts
        /// </summary>
        public async Task<string> Ask(string systemPrompt, string userPrompt)
        {
            return await Ask(new[] { systemPrompt }, new[] { userPrompt });
        }

        /// <summary>
        /// Check if AI is available
        /// </summary>
        public async Task<bool> IsAvailableAsync()
        {
            return await _aiServiceManager.IsProviderAvailableAsync(AIProviderType.OpenAI);
        }
    }
} 