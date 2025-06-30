using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Services.AI.Configuration;
using DumpMiner.Services.AI.Context;
using DumpMiner.Services.AI.Interfaces;
using DumpMiner.Services.AI.Models;
using DumpMiner.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DumpMiner.Services.AI
{
    /// <summary>
    /// Main AI service manager that orchestrates requests across multiple providers
    /// </summary>
    [Export(typeof(IAIServiceManager))]
    public sealed class AIServiceManager : IAIServiceManager, IDisposable
    {
        private readonly Dictionary<AIProviderType, IAIProvider> _providers = new();
        private readonly Dictionary<string, List<ConversationMessage>> _conversationHistory = new();
        private readonly IMemoryCache _cache;
        private readonly ILogger<AIServiceManager> _logger;
        private readonly AIConfiguration _config;
        private readonly IDumpAnalysisContextBuilder _contextBuilder;
        private readonly object _lockObject = new();
        private bool _disposed;

        /// <summary>
        /// Default provider to use when none is specified
        /// </summary>
        public AIProviderType DefaultProvider => _config.DefaultProvider;

        /// <summary>
        /// All registered providers
        /// </summary>
        public IEnumerable<IAIProvider> RegisteredProviders => _providers.Values;

        /// <summary>
        /// Available providers (configured and working)
        /// </summary>
        public IEnumerable<AIProviderType> AvailableProviders =>
            _providers.Values.Where(p => p.IsConfigured).Select(p => p.ProviderType);

        [ImportingConstructor]
        public AIServiceManager(
            AIConfiguration config,
            IMemoryCache cache,
            ILogger<AIServiceManager> logger,
            IDumpAnalysisContextBuilder contextBuilder)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        }

        /// <summary>
        /// Registers an AI provider
        /// </summary>
        public async Task RegisterProviderAsync(IAIProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            lock (_lockObject)
            {
                _providers[provider.ProviderType] = provider;
            }

            _logger.LogInformation("Registered AI provider: {Provider}", provider.ProviderType);
        }

        /// <summary>
        /// Sends a request to the AI service
        /// </summary>
        public async Task<AIResponse> SendRequestAsync(AIRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var stopwatch = Stopwatch.StartNew();
            _logger.LogDebug("Processing AI request {RequestId}", request.RequestId);

            try
            {
                // TODO: Add dump context enrichment when context builder is ready
                _logger.LogDebug("Processing AI request {RequestId}", request.RequestId);

                // For now, use the request as-is
                var enhancedRequest = request;

                // Check cache first
                if (request.EnableCaching)
                {
                    var cacheKey = GenerateCacheKey(enhancedRequest);
                    if (_cache.TryGetValue(cacheKey, out AIResponse cachedResponse))
                    {
                        _logger.LogDebug("Cache hit for request {RequestId}", request.RequestId);
                        return cachedResponse;
                    }
                }

                // Select appropriate provider
                var provider = await SelectProviderAsync(enhancedRequest.PreferredProvider);
                if (provider == null)
                {
                    return new AIResponse
                    {
                        RequestId = request.RequestId,
                        IsSuccess = false,
                        ErrorMessage = "No available AI provider found",
                        Provider = DefaultProvider,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                }

                _logger.LogDebug("Selected provider {Provider} for request {RequestId}",
                    provider.ProviderType, request.RequestId);

                // Execute the request
                var response = await provider.CompleteAsync(enhancedRequest, cancellationToken);

                stopwatch.Stop();

                // Create new response with updated processing time
                var finalResponse = new AIResponse
                {
                    RequestId = response.RequestId,
                    Content = response.Content,
                    Provider = response.Provider,
                    Model = response.Model,
                    IsSuccess = response.IsSuccess,
                    ErrorMessage = response.ErrorMessage,
                    Metadata = new ResponseMetadata
                    {
                        PromptTokens = response.Metadata.PromptTokens,
                        CompletionTokens = response.Metadata.CompletionTokens,
                        TotalTokens = response.Metadata.TotalTokens,
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        EstimatedCost = response.Metadata.EstimatedCost
                    },
                    Timestamp = response.Timestamp,
                    IsFromCache = response.IsFromCache
                };

                // Cache successful responses
                if (finalResponse.IsSuccess && request.EnableCaching)
                {
                    var cacheKey = GenerateCacheKey(enhancedRequest);
                    var cacheExpiry = TimeSpan.FromMinutes(_config.CacheExpirationMinutes);
                    _cache.Set(cacheKey, finalResponse, cacheExpiry);
                }

                // Update conversation history using RequestId as session
                UpdateConversationHistory(request.RequestId, request.UserPrompt, finalResponse.Content);

                _logger.LogInformation("AI request {RequestId} completed in {Duration}ms",
                    request.RequestId, stopwatch.ElapsedMilliseconds);

                return finalResponse;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("AI request {RequestId} was cancelled", request.RequestId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI request {RequestId} failed", request.RequestId);
                return new AIResponse
                {
                    RequestId = request.RequestId,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    Provider = DefaultProvider,
                    Timestamp = DateTimeOffset.UtcNow,
                    Metadata = new ResponseMetadata
                    {
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                    }
                };
            }
        }

        /// <summary>
        /// Checks if a provider is available
        /// </summary>
        public async Task<bool> IsProviderAvailableAsync(AIProviderType providerType)
        {
            if (!_providers.TryGetValue(providerType, out var provider))
                return false;

            try
            {
                // Test if provider is configured and working
                if (!provider.IsConfigured)
                    return false;

                var testResult = await provider.TestConnectionAsync();
                return testResult.IsSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Provider {Provider} availability check failed", providerType);
                return false;
            }
        }

        /// <summary>
        /// Gets conversation history for a session
        /// </summary>
        public List<ConversationMessage> GetConversationHistory(string sessionId)
        {
            lock (_lockObject)
            {
                return _conversationHistory.TryGetValue(sessionId, out var history)
                    ? new List<ConversationMessage>(history)
                    : new List<ConversationMessage>();
            }
        }

        /// <summary>
        /// Clears conversation history for a session
        /// </summary>
        public void ClearConversationHistory(string sessionId)
        {
            lock (_lockObject)
            {
                _conversationHistory.Remove(sessionId);
            }

            _logger.LogDebug("Cleared conversation history for session {SessionId}", sessionId);
        }

        /// <summary>
        /// Estimates cost for a request
        /// </summary>
        public decimal? EstimateCost(AIRequest request, AIProviderType? providerType = null)
        {
            var targetProvider = providerType ?? request.PreferredProvider ?? DefaultProvider;

            if (!_providers.TryGetValue(targetProvider, out var provider))
                return null;

            return provider.EstimateCost(request);
        }

        /// <summary>
        /// Simple AI helper method for operations (similar to old Gpt.Ask)
        /// </summary>
        public async Task<string> AskAsync(string systemPrompt, string userPrompt, AIProviderType? provider = null)
        {
            var request = new AIRequest
            {
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
                PreferredProvider = provider,
                EnableCaching = true
            };

            var response = await SendRequestAsync(request);
            return response.IsSuccess ? response.Content : null;
        }

        /// <summary>
        /// Alias for SendRequestAsync to maintain interface compatibility
        /// </summary>
        public Task<AIResponse> AnalyzeDumpAsync(AIRequest request, CancellationToken cancellationToken = default)
        {
            return SendRequestAsync(request, cancellationToken);
        }

        /// <summary>
        /// Async version of GetConversationHistory
        /// </summary>
        public Task<IEnumerable<ConversationMessage>> GetConversationHistoryAsync(string sessionId)
        {
            var history = GetConversationHistory(sessionId);
            return Task.FromResult<IEnumerable<ConversationMessage>>(history);
        }

        /// <summary>
        /// Async version of ClearConversationHistory
        /// </summary>
        public Task ClearConversationHistoryAsync(string sessionId)
        {
            ClearConversationHistory(sessionId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Tests a provider connection
        /// </summary>
        public async Task<ProviderTestResult> TestProviderAsync(AIProviderType providerType, CancellationToken cancellationToken = default)
        {
            if (!_providers.TryGetValue(providerType, out var provider))
            {
                return ProviderTestResult.Failure($"Provider {providerType} not found");
            }

            try
            {
                _logger.LogInformation("Testing provider {Provider}", providerType);
                var result = await provider.TestConnectionAsync(cancellationToken);

                _logger.LogInformation("Provider {Provider} test result: {Success}",
                    providerType, result.IsSuccess);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Provider {Provider} test failed", providerType);
                return ProviderTestResult.Failure(ex.Message);
            }
        }

        /// <summary>
        /// Updates provider configuration (placeholder for future implementation)
        /// </summary>
        public Task UpdateProviderConfigurationAsync(AIProviderType providerType, object configuration)
        {
            _logger.LogInformation("Provider {Provider} configuration update requested", providerType);
            // TODO: Implement dynamic provider reconfiguration
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sends a request to the AI service with completion API
        /// </summary>
        public async Task<AIResponse> CompleteAsync(AIRequest request, CancellationToken cancellationToken = default)
        {
            // CompleteAsync is just an alias for SendRequestAsync for compatibility
            return await SendRequestAsync(request, cancellationToken);
        }

        /// <summary>
        /// Checks if AI services are available
        /// </summary>
        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                return _providers.Values.Any(p => p.IsConfigured);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking AI availability");
                return false;
            }
        }

        #region Private Methods

        private async Task<IAIProvider> SelectProviderAsync(AIProviderType? preferredProvider)
        {
            // Try preferred provider first
            if (preferredProvider.HasValue && _providers.TryGetValue(preferredProvider.Value, out var preferred))
            {
                if (preferred.IsConfigured)
                {
                    try
                    {
                        var testResult = await preferred.TestConnectionAsync();
                        if (testResult.IsSuccess)
                            return preferred;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Preferred provider {Provider} test failed", preferredProvider);
                    }
                }

                _logger.LogWarning("Preferred provider {Provider} is not available, falling back", preferredProvider);
            }

            // Try default provider
            if (_providers.TryGetValue(DefaultProvider, out var defaultProvider))
            {
                if (defaultProvider.IsConfigured)
                {
                    try
                    {
                        var testResult = await defaultProvider.TestConnectionAsync();
                        if (testResult.IsSuccess)
                            return defaultProvider;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Default provider {Provider} test failed", DefaultProvider);
                    }
                }
            }

            // Try any available provider
            foreach (var provider in _providers.Values.Where(p => p.IsConfigured))
            {
                try
                {
                    var testResult = await provider.TestConnectionAsync();
                    if (testResult.IsSuccess)
                    {
                        _logger.LogInformation("Using fallback provider {Provider}", provider.ProviderType);
                        return provider;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Provider {Provider} test failed", provider.ProviderType);
                }
            }

            return null;
        }

        private string GenerateCacheKey(AIRequest request)
        {
            // Simple cache key generation based on prompt content
            var keyContent = $"{request.SystemPrompt}|{request.UserPrompt}";
            return $"ai_cache_{keyContent.GetHashCode():X}";
        }

        private void UpdateConversationHistory(string sessionId, string userPrompt, string aiResponse)
        {
            lock (_lockObject)
            {
                if (!_conversationHistory.ContainsKey(sessionId))
                {
                    _conversationHistory[sessionId] = new List<ConversationMessage>();
                }

                var history = _conversationHistory[sessionId];

                // Add user message
                history.Add(new ConversationMessage
                {
                    Role = "user",
                    Content = userPrompt,
                    Timestamp = DateTimeOffset.UtcNow
                });

                // Add AI response
                history.Add(new ConversationMessage
                {
                    Role = "assistant",
                    Content = aiResponse,
                    Timestamp = DateTimeOffset.UtcNow
                });

                // Trim history if it gets too long
                while (history.Count > _config.MaxConversationHistory * 2) // *2 for user+assistant pairs
                {
                    history.RemoveAt(0);
                }
            }
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var provider in _providers.Values)
                {
                    provider?.Dispose();
                }

                _providers.Clear();
                _conversationHistory.Clear();

                _disposed = true;
                _logger.LogDebug("AIServiceManager disposed");
            }
        }

        #endregion
    }
}