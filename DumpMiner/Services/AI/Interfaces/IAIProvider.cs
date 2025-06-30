using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Services.AI.Configuration;
using DumpMiner.Services.AI.Models;

namespace DumpMiner.Services.AI.Interfaces
{
    /// <summary>
    /// Interface for AI provider implementations
    /// </summary>
    public interface IAIProvider
    {
        /// <summary>
        /// Provider type identifier
        /// </summary>
        AIProviderType ProviderType { get; }

        /// <summary>
        /// Provider display name
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Whether the provider is configured and ready to use
        /// </summary>
        bool IsConfigured { get; }

        /// <summary>
        /// Supported models for this provider
        /// </summary>
        IEnumerable<string> SupportedModels { get; }

        /// <summary>
        /// Initializes the provider with configuration
        /// </summary>
        /// <param name="configuration">Provider configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task InitializeAsync(object configuration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a completion request to the AI provider
        /// </summary>
        /// <param name="request">AI request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>AI response</returns>
        Task<AIResponse> CompleteAsync(AIRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tests the provider connectivity and authentication
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Test result</returns>
        Task<ProviderTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Estimates the cost for a given request (if supported)
        /// </summary>
        /// <param name="request">AI request</param>
        /// <returns>Estimated cost in USD or null if not supported</returns>
        decimal? EstimateCost(AIRequest request);

        /// <summary>
        /// Gets the maximum context length for this provider
        /// </summary>
        int MaxContextLength { get; }

        /// <summary>
        /// Disposes resources used by the provider
        /// </summary>
        void Dispose();
    }
} 