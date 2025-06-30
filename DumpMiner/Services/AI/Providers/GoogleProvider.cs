using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Services.AI.Configuration;
using DumpMiner.Services.AI.Interfaces;
using DumpMiner.Services.AI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;

#pragma warning disable SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates

namespace DumpMiner.Services.AI.Providers
{
    /// <summary>
    /// Google Gemini provider implementation using Semantic Kernel
    /// </summary>
    public sealed class GoogleProvider : IAIProvider, IDisposable
    {
        private readonly ILogger<GoogleProvider> _logger;
        private Kernel? _kernel;
        private IChatCompletionService? _chatService;
        private GoogleConfiguration? _configuration;
        private bool _disposed;

        // Model pricing per 1K tokens (input/output) - as of 2024
        private static readonly Dictionary<string, (decimal input, decimal output)> ModelPricing = new()
        {
            { "gemini-pro", (0.0005m, 0.0015m) },
            { "gemini-pro-vision", (0.0005m, 0.0015m) },
            { "gemini-1.5-pro", (0.0035m, 0.0105m) },
            { "gemini-1.5-flash", (0.000075m, 0.0003m) }
        };

        public GoogleProvider(ILogger<GoogleProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public AIProviderType ProviderType => AIProviderType.Google;
        public string DisplayName => "Google Gemini";
        public bool IsConfigured => _configuration != null && !string.IsNullOrEmpty(_configuration.ApiKey);
        public IEnumerable<string> SupportedModels => ModelPricing.Keys;
        public int MaxContextLength => GetMaxContextLength();

        public async Task InitializeAsync(object configuration, CancellationToken cancellationToken = default)
        {
            if (configuration is not GoogleConfiguration googleConfig)
                throw new ArgumentException("Invalid configuration type", nameof(configuration));

            _configuration = googleConfig;

            if (string.IsNullOrEmpty(_configuration.ApiKey))
                throw new InvalidOperationException("Google API key is required");

            if (!_configuration.IsEnabled)
            {
                _logger.LogInformation("Google provider is disabled in configuration");
                return;
            }

            try
            {
                var builder = Kernel.CreateBuilder();

                builder.AddGoogleAIGeminiChatCompletion(
                    modelId: _configuration.Model,
                    apiKey: _configuration.ApiKey,
                    httpClient: CreateHttpClient());

                _kernel = builder.Build();
                _chatService = _kernel.GetRequiredService<IChatCompletionService>();

                _logger.LogInformation("Google provider initialized with model {Model}", _configuration.Model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Google provider");
                throw;
            }
        }

        public async Task<AIResponse> CompleteAsync(AIRequest request, CancellationToken cancellationToken = default)
        {
            if (_chatService == null || _configuration == null)
                throw new InvalidOperationException("Provider not initialized");

            var stopwatch = Stopwatch.StartNew();
            var requestId = request.RequestId;

            try
            {
                var chatHistory = new ChatHistory();

                // Add system message
                if (!string.IsNullOrEmpty(request.SystemPrompt))
                {
                    chatHistory.AddSystemMessage(request.SystemPrompt);
                }

                // Add conversation history
                foreach (var message in request.ConversationHistory)
                {
                    switch (message.Role.ToLower())
                    {
                        case "user":
                            chatHistory.AddUserMessage(message.Content);
                            break;
                        case "assistant":
                            chatHistory.AddAssistantMessage(message.Content);
                            break;
                    }
                }

                // Add current user message
                chatHistory.AddUserMessage(request.UserPrompt);

                // Prepare execution settings
                var executionSettings = new GeminiPromptExecutionSettings
                {
                    MaxTokens = request.MaxTokens ?? 1000,
                    Temperature = (float)(request.Temperature ?? _configuration.Temperature),
                    TopP = 1.0f
                };

                // Execute the request
                var result = await _chatService.GetChatMessageContentAsync(
                    chatHistory,
                    executionSettings,
                    _kernel,
                    cancellationToken);

                stopwatch.Stop();

                // Extract metadata (Google may not provide detailed token usage)
                var metadata = new ResponseMetadata
                {
                    PromptTokens = 0, // Google doesn't always provide token counts
                    CompletionTokens = 0,
                    TotalTokens = 0,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    EstimatedCost = EstimateCostForRequest(request)
                };

                var response = new AIResponse
                {
                    RequestId = requestId,
                    Content = result.Content ?? string.Empty,
                    Provider = ProviderType,
                    Model = _configuration.Model,
                    IsSuccess = true,
                    Metadata = metadata,
                    Timestamp = DateTimeOffset.UtcNow
                };

                _logger.LogInformation("Google request completed successfully in {ElapsedMs}ms",
                    stopwatch.ElapsedMilliseconds);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Google request failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

                return new AIResponse
                {
                    RequestId = requestId,
                    Provider = ProviderType,
                    Model = _configuration.Model,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    Timestamp = DateTimeOffset.UtcNow,
                    Metadata = new ResponseMetadata { ProcessingTimeMs = stopwatch.ElapsedMilliseconds }
                };
            }
        }

        public async Task<ProviderTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (_chatService == null || _configuration == null)
                return ProviderTestResult.Failure("Provider not initialized");

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var testRequest = new AIRequest
                {
                    UserPrompt = "Hello, can you respond with 'Test successful'?",
                    MaxTokens = 10,
                    Temperature = 0.0
                };

                var response = await CompleteAsync(testRequest, cancellationToken);
                stopwatch.Stop();

                if (response.IsSuccess)
                {
                    var metadata = new Dictionary<string, object>
                    {
                        ["model"] = _configuration.Model,
                        ["totalTokens"] = response.Metadata.TotalTokens,
                        ["estimatedCost"] = response.Metadata.EstimatedCost ?? 0m
                    };

                    return ProviderTestResult.Success(stopwatch.Elapsed, _configuration.Model, metadata);
                }
                else
                {
                    return ProviderTestResult.Failure(response.ErrorMessage ?? "Unknown error");
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Google connection test failed");
                return ProviderTestResult.Failure(ex.Message);
            }
        }

        public decimal? EstimateCost(AIRequest request)
        {
            return EstimateCostForRequest(request);
        }

        private decimal? EstimateCostForRequest(AIRequest request)
        {
            if (!ModelPricing.TryGetValue(_configuration?.Model ?? "", out var pricing))
                return null;

            // Rough estimation based on text length
            var inputTokens = EstimateTokenCount(request.SystemPrompt + request.UserPrompt + 
                string.Join(" ", request.ConversationHistory.Select(h => h.Content)));
            var outputTokens = request.MaxTokens ?? 1000;

            var inputCost = (inputTokens / 1000m) * pricing.input;
            var outputCost = (outputTokens / 1000m) * pricing.output;

            return inputCost + outputCost;
        }

        private HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(60); // Default timeout
            return client;
        }

        private int GetMaxContextLength()
        {
            return _configuration?.Model switch
            {
                "gemini-pro" => 32768,
                "gemini-pro-vision" => 16384,
                "gemini-1.5-pro" => 2097152, // 2M tokens
                "gemini-1.5-flash" => 1048576, // 1M tokens
                _ => 32768
            };
        }

        private static int EstimateTokenCount(string text)
        {
            // Rough estimation: ~4 characters per token for English text
            return string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / 4.0);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Kernel doesn't implement IDisposable in current version
                _kernel = null;
                _chatService = null;
                _disposed = true;
            }
        }
    }
} 