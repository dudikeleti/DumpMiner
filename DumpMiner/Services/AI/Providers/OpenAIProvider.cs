using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Services.AI.Configuration;
using DumpMiner.Services.AI.Interfaces;
using DumpMiner.Services.AI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace DumpMiner.Services.AI.Providers
{
    /// <summary>
    /// OpenAI provider implementation using Semantic Kernel
    /// </summary>
    public sealed class OpenAIProvider : IAIProvider, IDisposable
    {
        private readonly ILogger<OpenAIProvider> _logger;
        private Kernel? _kernel;
        private IChatCompletionService? _chatService;
        private OpenAIConfiguration? _configuration;
        private bool _disposed;

        // Model pricing per 1K tokens (input/output) - as of 2024
        private static readonly Dictionary<string, (decimal input, decimal output)> ModelPricing = new()
        {
            { "gpt-4", (0.03m, 0.06m) },
            { "gpt-4-turbo", (0.01m, 0.03m) },
            { "gpt-4o", (0.005m, 0.015m) },
            { "gpt-3.5-turbo", (0.0015m, 0.002m) },
            { "gpt-3.5-turbo-16k", (0.003m, 0.004m) }
        };

        public OpenAIProvider(ILogger<OpenAIProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public AIProviderType ProviderType => AIProviderType.OpenAI;
        public string DisplayName => "OpenAI";
        public bool IsConfigured => _configuration != null && !string.IsNullOrEmpty(_configuration.ApiKey);
        public IEnumerable<string> SupportedModels => ModelPricing.Keys;
        public int MaxContextLength => GetMaxContextLength();

        public async Task InitializeAsync(object configuration, CancellationToken cancellationToken = default)
        {
            if (configuration is not OpenAIConfiguration openAIConfig)
                throw new ArgumentException("Invalid configuration type", nameof(configuration));

            _configuration = openAIConfig;

            if (string.IsNullOrEmpty(_configuration.ApiKey))
                throw new InvalidOperationException("OpenAI API key is required");

            try
            {
                var builder = Kernel.CreateBuilder();

                builder.AddOpenAIChatCompletion(
                    modelId: _configuration.Model,
                    apiKey: _configuration.ApiKey,
                    httpClient: CreateHttpClient());

                _kernel = builder.Build();
                _chatService = _kernel.GetRequiredService<IChatCompletionService>();

                _logger.LogInformation("OpenAI provider initialized with model {Model}", _configuration.Model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize OpenAI provider");
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
                var executionSettings = new OpenAIPromptExecutionSettings
                {
                    MaxTokens = request.MaxTokens ?? _configuration.MaxTokens,
                    Temperature = request.Temperature ?? _configuration.Temperature,
                    TopP = 1.0,
                    FrequencyPenalty = 0.0,
                    PresencePenalty = 0.0
                };

                // Execute the request
                var result = await _chatService.GetChatMessageContentAsync(
                    chatHistory,
                    executionSettings,
                    _kernel,
                    cancellationToken);

                stopwatch.Stop();

                // Extract metadata
                var usage = result.Metadata?.TryGetValue("Usage", out var value) is true ? value as OpenAI.Chat.ChatTokenUsage : null;

                var metadata = new ResponseMetadata
                {
                    PromptTokens = usage?.InputTokenCount ?? 0,
                    CompletionTokens = usage?.OutputTokenCount ?? 0,
                    TotalTokens = usage?.TotalTokenCount ?? 0,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    EstimatedCost = CalculateCost(usage?.InputTokenCount ?? 0, usage?.OutputTokenCount ?? 0)
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

                _logger.LogInformation("OpenAI request completed successfully in {ElapsedMs}ms, Tokens: {TotalTokens}",
                    stopwatch.ElapsedMilliseconds, metadata.TotalTokens);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "OpenAI request failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

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
                _logger.LogError(ex, "OpenAI connection test failed");
                return ProviderTestResult.Failure(ex.Message);
            }
        }

        public decimal? EstimateCost(AIRequest request)
        {
            if (!ModelPricing.TryGetValue(_configuration?.Model ?? "", out var pricing))
                return null;

            // Rough estimation based on text length
            var inputTokens = EstimateTokenCount(request.SystemPrompt + request.UserPrompt);
            var outputTokens = request.MaxTokens ?? 1000;

            var inputCost = (inputTokens / 1000m) * pricing.input;
            var outputCost = (outputTokens / 1000m) * pricing.output;

            return inputCost + outputCost;
        }

        private HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(_configuration?.TimeoutSeconds ?? 60);
            return client;
        }

        private int GetMaxContextLength()
        {
            return _configuration?.Model switch
            {
                "gpt-4" => 8192,
                "gpt-4-turbo" => 128000,
                "gpt-4o" => 128000,
                "gpt-3.5-turbo" => 4096,
                "gpt-3.5-turbo-16k" => 16384,
                _ => 4096
            };
        }

        private decimal? CalculateCost(int promptTokens, int completionTokens)
        {
            if (!ModelPricing.TryGetValue(_configuration?.Model ?? "", out var pricing))
                return null;

            var inputCost = (promptTokens / 1000m) * pricing.input;
            var outputCost = (completionTokens / 1000m) * pricing.output;

            return inputCost + outputCost;
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