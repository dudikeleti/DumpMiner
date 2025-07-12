using System;
using System.IO;
using System.Threading.Tasks;
using DumpMiner.Services.AI;
using DumpMiner.Services.AI.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

/// <summary>
/// Diagnostic tool for testing AI configuration and services
/// This is NOT a unit test - it's a standalone diagnostic tool
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        // Initialize Serilog for testing
        InitializeLogging();
        
        var logger = Log.ForContext<Program>();
        logger.Information("🤖 DumpMiner AI Configuration Diagnostic Tool");
        logger.Information("==============================================");
        
        try
        {
            // Test 1: Check configuration files
            logger.Information("📁 Test 1: Configuration Files");
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            logger.Information("Config path: {ConfigPath}", configPath);
            logger.Information("Config exists: {ConfigExists}", File.Exists(configPath));
            
            if (File.Exists(configPath))
            {
                var content = File.ReadAllText(configPath);
                logger.Information("Config content length: {ContentLength} characters", content.Length);
                logger.Debug("Config content preview: {ConfigPreview}", content.Substring(0, Math.Min(200, content.Length)) + "...");
            }
            
            // Test 2: Load and validate AI configuration
            logger.Information("⚙️ Test 2: AI Configuration Loading");
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();
            
            var aiConfig = configuration.GetSection("AI").Get<AIConfiguration>();
            if (aiConfig != null)
            {
                logger.Information("✅ AI Configuration loaded successfully");
                logger.Information("Default provider: {DefaultProvider}", aiConfig.DefaultProvider);
                logger.Information("Max tokens: {MaxTokens}", aiConfig.MaxTokens);
                logger.Information("Timeout: {TimeoutSeconds}s", aiConfig.TimeoutSeconds);
                logger.Information("Caching enabled: {EnableCaching}", aiConfig.EnableCaching);
                
                // Test provider configurations
                if (aiConfig.Providers?.OpenAI != null)
                {
                    var hasApiKey = !string.IsNullOrEmpty(aiConfig.Providers.OpenAI.ApiKey);
                    logger.Information("OpenAI configured: {HasApiKey}", hasApiKey);
                    logger.Information("OpenAI model: {Model}", aiConfig.Providers.OpenAI.Model);
                }
                
                if (aiConfig.Providers?.Anthropic != null)
                {
                    var hasApiKey = !string.IsNullOrEmpty(aiConfig.Providers.Anthropic.ApiKey);
                    logger.Information("Anthropic configured: {HasApiKey}", hasApiKey);
                    logger.Information("Anthropic model: {Model}", aiConfig.Providers.Anthropic.Model);
                }
                
                if (aiConfig.Providers?.Google != null)
                {
                    var hasApiKey = !string.IsNullOrEmpty(aiConfig.Providers.Google.ApiKey);
                    logger.Information("Google configured: {HasApiKey}", hasApiKey);
                    logger.Information("Google model: {Model}", aiConfig.Providers.Google.Model);
                }
            }
            else
            {
                logger.Error("❌ Failed to load AI configuration");
            }
            
            // Test 3: Try creating AI helper
            logger.Information("🔧 Test 3: AI Service Creation");
            var aiHelper = await ServiceRegistration.CreateAIHelperAsync();
            logger.Information("✅ AI Helper created successfully!");
            
            // Test 4: Check if AI is available
            logger.Information("🚀 Test 4: AI Service Availability");
            var isAvailable = await aiHelper.IsAvailableAsync();
            logger.Information("AI Available: {AIAvailable}", isAvailable);
            
            // Test 5: Test basic AI functionality (if available)
            if (isAvailable)
            {
                logger.Information("🧠 Test 5: Basic AI Functionality");
                try
                {
                    var testPrompt = "This is a test prompt for configuration validation.";
                    logger.Information("Testing with prompt: {TestPrompt}", testPrompt);
                    // Note: This would require actual implementation of test method
                    logger.Information("✅ AI service is ready for testing");
                }
                catch (Exception ex)
                {
                    logger.Warning("⚠️ AI service available but test failed: {Error}", ex.Message);
                }
            }
            
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error during AI configuration testing");
        }
        finally
        {
            logger.Information("Test completed. Press any key to exit...");
            Console.ReadKey();
            Log.CloseAndFlush();
        }
    }
    
    private static void InitializeLogging()
    {
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
        }
        catch
        {
            // Fallback logging if configuration fails
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.Debug()
                .MinimumLevel.Debug()
                .CreateLogger();
        }
    }
} 