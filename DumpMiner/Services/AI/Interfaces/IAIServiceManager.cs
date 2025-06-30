using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Services.AI.Configuration;
using DumpMiner.Services.AI.Models;
using DumpMiner.Models;

namespace DumpMiner.Services.AI.Interfaces
{
    /// <summary>
    /// Main interface for AI service management and orchestration
    /// </summary>
    public interface IAIServiceManager
    {
        /// <summary>
        /// Gets available AI providers
        /// </summary>
        IEnumerable<AIProviderType> AvailableProviders { get; }

        /// <summary>
        /// Gets the current default provider
        /// </summary>
        AIProviderType DefaultProvider { get; }

        /// <summary>
        /// Checks if a provider is available and configured
        /// </summary>
        /// <param name="provider">Provider to check</param>
        /// <returns>True if provider is available</returns>
        Task<bool> IsProviderAvailableAsync(AIProviderType provider);

        /// <summary>
        /// Sends a request to analyze dump data using AI
        /// </summary>
        /// <param name="request">The AI analysis request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>AI response</returns>
        Task<AIResponse> AnalyzeDumpAsync(AIRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets conversation history for a specific session
        /// </summary>
        /// <param name="sessionId">Session identifier</param>
        /// <returns>Conversation history</returns>
        Task<IEnumerable<ConversationMessage>> GetConversationHistoryAsync(string sessionId);

        /// <summary>
        /// Clears conversation history for a session
        /// </summary>
        /// <param name="sessionId">Session identifier</param>
        Task ClearConversationHistoryAsync(string sessionId);

        /// <summary>
        /// Updates provider configuration
        /// </summary>
        /// <param name="provider">Provider type</param>
        /// <param name="configuration">Provider configuration</param>
        Task UpdateProviderConfigurationAsync(AIProviderType provider, object configuration);

        /// <summary>
        /// Tests provider connectivity and authentication
        /// </summary>
        /// <param name="provider">Provider to test</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Test result</returns>
        Task<ProviderTestResult> TestProviderAsync(AIProviderType provider, CancellationToken cancellationToken = default);

        /// <summary>
        /// Registers an AI provider
        /// </summary>
        /// <param name="provider">Provider to register</param>
        Task RegisterProviderAsync(IAIProvider provider);

        /// <summary>
        /// Simple AI helper method for operations (similar to old Gpt.Ask)
        /// </summary>
        /// <param name="systemPrompt">System prompt</param>
        /// <param name="userPrompt">User prompt</param>
        /// <param name="provider">Optional provider override</param>
        /// <returns>AI response content or null if failed</returns>
        Task<string> AskAsync(string systemPrompt, string userPrompt, AIProviderType? provider = null);

        /// <summary>
        /// Estimates the cost for a given request
        /// </summary>
        /// <param name="request">AI request to estimate cost for</param>
        /// <param name="providerType">Optional provider type override</param>
        /// <returns>Estimated cost in USD or null if not supported</returns>
        decimal? EstimateCost(AIRequest request, AIProviderType? providerType = null);

        /// <summary>
        /// Sends a request to the AI service with completion API
        /// </summary>
        Task<AIResponse> CompleteAsync(AIRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if AI services are available
        /// </summary>
        Task<bool> IsAvailableAsync();
    }

    /// <summary>
    /// Result of provider connectivity test
    /// </summary>
    public sealed class ProviderTestResult
    {
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
        public TimeSpan ResponseTime { get; init; }
        public string? Model { get; init; }
        public Dictionary<string, object> Metadata { get; init; } = new();

        public static ProviderTestResult Success(TimeSpan responseTime, string? model = null, Dictionary<string, object>? metadata = null)
            => new() { IsSuccess = true, ResponseTime = responseTime, Model = model, Metadata = metadata ?? new() };

        public static ProviderTestResult Failure(string errorMessage)
            => new() { IsSuccess = false, ErrorMessage = errorMessage };
    }

    /// <summary>
    /// AI usage statistics
    /// </summary>
    public class AIUsageStatistics
    {
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public long TotalTokensUsed { get; set; }
        public decimal TotalCost { get; set; }
        public TimeSpan AverageResponseTime { get; set; }
        public Dictionary<AIProviderType, int> RequestsByProvider { get; set; } = new();
        public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
    }
} 