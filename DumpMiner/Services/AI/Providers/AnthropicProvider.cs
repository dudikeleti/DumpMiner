using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using DumpMiner.Services.AI.Configuration;
using DumpMiner.Services.AI.Interfaces;
using DumpMiner.Services.AI.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace DumpMiner.Services.AI.Providers
{
    /// <summary>
    /// Anthropic Claude AI provider using Anthropic.SDK
    /// </summary>
    public sealed class AnthropicProvider : IAIProvider, IDisposable
    {
        private readonly ILogger<AnthropicProvider> _logger;
        private AnthropicClient? _client;
        private AnthropicConfiguration? _configuration;
        private bool _disposed;

        // Model pricing per 1M tokens (input/output) - as of 2024
        private static readonly Dictionary<string, (decimal input, decimal output)> ModelPricing = new()
        {
            { "claude-sonnet-3.7", (3.00m, 15.00m) },
            { "claude-sonnet-4", (3.00m, 15.00m) },
            { "claude-opus-4", (15.00m, 75.00m) },
            { "claude-sonnet-3.5", (3.00m, 15.00m) }
        };

        public AnthropicProvider(ILogger<AnthropicProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public AIProviderType ProviderType => AIProviderType.Anthropic;
        public string DisplayName => "Anthropic Claude";
        public bool IsConfigured => _configuration != null && !string.IsNullOrEmpty(_configuration.ApiKey) && _configuration.IsEnabled;
        public IEnumerable<string> SupportedModels => ModelPricing.Keys;
        public int MaxContextLength => GetMaxContextLength();

        public async Task InitializeAsync(object configuration, CancellationToken cancellationToken = default)
        {
            if (configuration is not AnthropicConfiguration anthropicConfig)
                throw new ArgumentException("Invalid configuration type", nameof(configuration));

            _configuration = anthropicConfig;

            if (string.IsNullOrEmpty(_configuration.ApiKey))
                throw new InvalidOperationException("Anthropic API key is required");

            if (!_configuration.IsEnabled)
            {
                _logger.LogInformation("Anthropic provider is disabled in configuration");
                return;
            }

            try
            {
                _client = new AnthropicClient(_configuration.ApiKey);
                _logger.LogInformation("Anthropic provider initialized with model {Model}", _configuration.Model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Anthropic provider");
                throw;
            }
        }

        public async Task<AIResponse> CompleteAsync(AIRequest request, CancellationToken cancellationToken = default)
        {
            if (_client == null || _configuration == null)
                throw new InvalidOperationException("Provider not initialized");

            var stopwatch = Stopwatch.StartNew();
            var requestId = request.RequestId;

            try
            {
                var messages = new List<Message>();

                // Add conversation history
                foreach (var message in request.ConversationHistory)
                {
                    var role = message.Role.ToLower() switch
                    {
                        "user" => RoleType.User,
                        "assistant" => RoleType.Assistant,
                        _ => RoleType.User
                    };

                    messages.Add(new Message(role, message.Content));
                }

                // Add current user message
                messages.Add(new Message(RoleType.User, request.UserPrompt));

                // Create the message parameters using correct SDK API
                var parameters = new MessageParameters()
                {
                    Model = _configuration.Model,
                    MaxTokens = request.MaxTokens ?? 4000,
                    Temperature = (decimal)(request.Temperature ?? _configuration.Temperature),
                    TopP = (decimal)_configuration.TopP,
                    Messages = messages,
                    Stream = false
                };

                // Add system prompt if provided
                if (!string.IsNullOrEmpty(request.SystemPrompt))
                {
                    parameters.System = new List<SystemMessage>
                    {
                        new SystemMessage(request.SystemPrompt)
                    };
                }

                // Execute the request
                var response = await _client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);

                stopwatch.Stop();

                // Extract content from response
                var content = response.Message.ToString();

                return new AIResponse
                {
                    RequestId = requestId,
                    Content = content,
                    Provider = AIProviderType.Anthropic,
                    Model = _configuration.Model,
                    IsSuccess = true,
                    Metadata = new ResponseMetadata
                    {
                        PromptTokens = response.Usage?.InputTokens ?? 0,
                        CompletionTokens = response.Usage?.OutputTokens ?? 0,
                        TotalTokens = (response.Usage?.InputTokens ?? 0) + (response.Usage?.OutputTokens ?? 0),
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        EstimatedCost = CalculateCost(response.Usage?.InputTokens ?? 0, response.Usage?.OutputTokens ?? 0)
                    },
                    Timestamp = DateTimeOffset.UtcNow
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Anthropic API request failed for request {RequestId}", requestId);

                return new AIResponse
                {
                    RequestId = requestId,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    Provider = AIProviderType.Anthropic,
                    Model = _configuration.Model,
                    Timestamp = DateTimeOffset.UtcNow,
                    Metadata = new ResponseMetadata
                    {
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                    }
                };
            }
        }

        public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            return IsConfigured && _client != null;
        }

        public async Task<ProviderTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConfigured || _client == null)
            {
                return ProviderTestResult.Failure("Anthropic provider not configured or initialized");
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Send a minimal test message
                var testParameters = new MessageParameters()
                {
                    Model = _configuration.Model,
                    MaxTokens = 10,
                    Temperature = 0.1m,
                    Messages = new List<Message>
                    {
                        new Message(RoleType.User, "Hello")
                    },
                    Stream = false
                };

                var response = await _client.Messages.GetClaudeMessageAsync(testParameters, cancellationToken);
                
                stopwatch.Stop();

                if (response?.Message != null)
                {
                    var metadata = new Dictionary<string, object>
                    {
                        ["model"] = _configuration.Model,
                        ["totalTokens"] = (response.Usage?.InputTokens ?? 0) + (response.Usage?.OutputTokens ?? 0),
                        ["estimatedCost"] = CalculateCost(response.Usage?.InputTokens ?? 0, response.Usage?.OutputTokens ?? 0) ?? 0m
                    };

                    return ProviderTestResult.Success(stopwatch.Elapsed, _configuration.Model, metadata);
                }
                else
                {
                    return ProviderTestResult.Failure("Anthropic provider returned empty response");
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Anthropic provider test failed");
                return ProviderTestResult.Failure($"Anthropic provider test failed: {ex.Message}");
            }
        }

        public decimal? EstimateCost(AIRequest request)
        {
            if (!ModelPricing.TryGetValue(_configuration?.Model ?? "", out var pricing))
                return null;

            // Build complete prompt text including dump context
            var promptText = new StringBuilder();
            
            // Add system prompt
            if (!string.IsNullOrEmpty(request.SystemPrompt))
            {
                promptText.AppendLine(request.SystemPrompt);
            }
            
            // Add dump context if available
            if (request.DumpContext != null)
            {
                promptText.AppendLine(FormatDumpContextForCostEstimation(request.DumpContext));
            }
            
            // Add user prompt
            if (!string.IsNullOrEmpty(request.UserPrompt))
            {
                promptText.AppendLine(request.UserPrompt);
            }
            
            // Add conversation history
            if (request.ConversationHistory.Any())
            {
                promptText.AppendLine(string.Join(" ", request.ConversationHistory.Select(h => h.Content)));
            }

            // Rough estimation based on complete text length
            var inputTokens = EstimateTokenCount(promptText.ToString());
            var outputTokens = request.MaxTokens ?? 1000;

            var inputCost = (inputTokens / 1_000_000m) * pricing.input;
            var outputCost = (outputTokens / 1_000_000m) * pricing.output;

            return inputCost + outputCost;
        }

        private int GetMaxContextLength()
        {
            return _configuration?.Model switch
            {
                "claude-sonnet-3.7" => 200000,
                "claude-sonnet-4" => 200000,
                "claude-opus-4" => 200000,
                "claude-sonnet-3.5" => 200000,
                _ => 200000
            };
        }

        private decimal? CalculateCost(int promptTokens, int completionTokens)
        {
            if (!ModelPricing.TryGetValue(_configuration?.Model ?? "", out var pricing))
                return null;

            var inputCost = (promptTokens / 1_000_000m) * pricing.input;
            var outputCost = (completionTokens / 1_000_000m) * pricing.output;

            return inputCost + outputCost;
        }

        private static int EstimateTokenCount(string text)
        {
            // Rough estimation: ~4 characters per token for English text
            return string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / 4.0);
        }

        private static string FormatDumpContextForCostEstimation(DumpContext dumpContext)
        {
            var context = new StringBuilder();

            if (dumpContext.ProcessInfo != null)
            {
                context.AppendLine($"Process: {dumpContext.ProcessInfo.ProcessName} (PID: {dumpContext.ProcessInfo.ProcessId})");
                context.AppendLine($"CLR Version: {dumpContext.ProcessInfo.ClrVersion}");
                context.AppendLine($"Thread Count: {dumpContext.ProcessInfo.ThreadCount}");
            }

            if (dumpContext.HeapStats != null)
            {
                context.AppendLine($"Heap Size: {FormatBytes(dumpContext.HeapStats.TotalSize)}");
                context.AppendLine($"Gen0: {FormatBytes(dumpContext.HeapStats.Gen0Size)}, Gen1: {FormatBytes(dumpContext.HeapStats.Gen1Size)}, Gen2: {FormatBytes(dumpContext.HeapStats.Gen2Size)}");
                context.AppendLine($"LOH: {FormatBytes(dumpContext.HeapStats.LargeObjectHeapSize)}");
            }

            if (dumpContext.Exceptions?.Any() == true)
            {
                context.AppendLine($"Exceptions Found: {dumpContext.Exceptions.Count}");
                foreach (var exception in dumpContext.Exceptions.Take(3))
                {
                    context.AppendLine($"  - {exception.Type}: {exception.Message}");
                }
            }

            if (dumpContext.LargeObjects?.Any() == true)
            {
                context.AppendLine($"Large Objects: {dumpContext.LargeObjects.Count}");
                foreach (var obj in dumpContext.LargeObjects.Take(3))
                {
                    context.AppendLine($"  - {obj.Type}: {FormatBytes(obj.Size)}");
                }
            }

            return context.ToString();
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }
            return $"{number:n1}{suffixes[counter]}";
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _client?.Dispose();
                _client = null;
                _disposed = true;
            }
        }
    }
} 