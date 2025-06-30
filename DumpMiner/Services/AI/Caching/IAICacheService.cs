using System;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Services.AI.Orchestration;

namespace DumpMiner.Services.AI.Caching
{
    /// <summary>
    /// Intelligent caching service for AI responses
    /// </summary>
    public interface IAICacheService
    {
        /// <summary>
        /// Gets cached AI response if available
        /// </summary>
        Task<AIAnalysisResult> GetCachedResponseAsync(
            string cacheKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Caches AI response with intelligent expiration
        /// </summary>
        Task SetCachedResponseAsync(
            string cacheKey,
            AIAnalysisResult response,
            TimeSpan? expiration = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates cache entries matching pattern
        /// </summary>
        Task InvalidateCacheAsync(
            string pattern,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all cached responses
        /// </summary>
        Task ClearCacheAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates intelligent cache key from context
        /// </summary>
        string GenerateCacheKey(
            string operationName,
            string contentHash,
            string userQuery = null);
    }

    /// <summary>
    /// Cache statistics
    /// </summary>
    public class CacheStatistics
    {
        public long TotalEntries { get; set; }
        public long HitCount { get; set; }
        public long MissCount { get; set; }
        public double HitRatio => TotalRequests > 0 ? (double)HitCount / TotalRequests : 0;
        public long TotalRequests => HitCount + MissCount;
        public long TotalSizeBytes { get; set; }
        public DateTimeOffset LastAccessed { get; set; }
        public TimeSpan AverageResponseTime { get; set; }
    }
} 