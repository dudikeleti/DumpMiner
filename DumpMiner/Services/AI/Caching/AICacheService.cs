using System;
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Services.AI.Orchestration;
using Microsoft.Extensions.Logging;

namespace DumpMiner.Services.AI.Caching
{
    /// <summary>
    /// In-memory implementation of AI cache service with intelligent features
    /// </summary>
    [Export(typeof(IAICacheService))]
    public class AICacheService : IAICacheService
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private readonly ILogger<AICacheService> _logger;
        private long _hitCount;
        private long _missCount;

        [ImportingConstructor]
        public AICacheService(ILogger<AICacheService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AIAnalysisResult> GetCachedResponseAsync(
            string cacheKey,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (_cache.TryGetValue(cacheKey, out var entry))
                {
                    if (entry.ExpiresAt > DateTimeOffset.UtcNow)
                    {
                        entry.LastAccessed = DateTimeOffset.UtcNow;
                        entry.AccessCount++;
                        Interlocked.Increment(ref _hitCount);
                        
                        _logger.LogDebug("Cache hit for key {CacheKey}", cacheKey);
                        return entry.Result;
                    }
                    else
                    {
                        // Remove expired entry
                        _cache.TryRemove(cacheKey, out _);
                        _logger.LogDebug("Cache entry expired for key {CacheKey}", cacheKey);
                    }
                }

                Interlocked.Increment(ref _missCount);
                _logger.LogDebug("Cache miss for key {CacheKey}", cacheKey);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cached response for key {CacheKey}", cacheKey);
                return null;
            }
        }

        public async Task SetCachedResponseAsync(
            string cacheKey,
            AIAnalysisResult response,
            TimeSpan? expiration = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var expirationTime = expiration ?? TimeSpan.FromMinutes(30);
                var entry = new CacheEntry
                {
                    Result = response,
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.Add(expirationTime),
                    LastAccessed = DateTimeOffset.UtcNow,
                    AccessCount = 0
                };

                _cache.AddOrUpdate(cacheKey, entry, (key, existing) => entry);
                
                _logger.LogDebug("Cached AI response for key {CacheKey}, expires at {ExpiresAt}", 
                    cacheKey, entry.ExpiresAt);

                // Cleanup old entries periodically
                if (_cache.Count > 1000) // Prevent unbounded growth
                {
                    await CleanupExpiredEntriesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching AI response for key {CacheKey}", cacheKey);
            }
        }

        public async Task InvalidateCacheAsync(
            string pattern,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var keys = _cache.Keys.Where(key => key.Contains(pattern, StringComparison.OrdinalIgnoreCase)).ToList();
                
                foreach (var key in keys)
                {
                    _cache.TryRemove(key, out _);
                }

                _logger.LogInformation("Invalidated {Count} cache entries matching pattern {Pattern}", 
                    keys.Count, pattern);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache entries for pattern {Pattern}", pattern);
            }
        }

        public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var count = _cache.Count;
                _cache.Clear();
                Interlocked.Exchange(ref _hitCount, 0);
                Interlocked.Exchange(ref _missCount, 0);
                
                _logger.LogInformation("Cleared {Count} cache entries", count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache");
            }
        }

        public async Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                var entries = _cache.Values.ToList();
                
                return new CacheStatistics
                {
                    TotalEntries = entries.Count,
                    HitCount = _hitCount,
                    MissCount = _missCount,
                    TotalSizeBytes = EstimateCacheSize(entries),
                    LastAccessed = entries.Any() ? entries.Max(e => e.LastAccessed) : DateTimeOffset.MinValue,
                    AverageResponseTime = TimeSpan.Zero // Would need response time tracking
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache statistics");
                return new CacheStatistics();
            }
        }

        public string GenerateCacheKey(
            string operationName,
            string contentHash,
            string userQuery = null)
        {
            var content = $"{operationName}|{contentHash}|{userQuery ?? ""}";
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
            return $"ai_cache_{Convert.ToHexString(hash)[..16]}";
        }

        private async Task CleanupExpiredEntriesAsync()
        {
            try 
            {
                var now = DateTimeOffset.UtcNow;
                var expiredKeys = _cache
                    .Where(kvp => kvp.Value.ExpiresAt < now)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _cache.TryRemove(key, out _);
                }

                if (expiredKeys.Any())
                {
                    _logger.LogDebug("Cleaned up {Count} expired cache entries", expiredKeys.Count);
                }

                // Also cleanup least recently used entries if cache is still too large
                if (_cache.Count > 500)
                {
                    var lruKeys = _cache
                        .OrderBy(kvp => kvp.Value.LastAccessed)
                        .Take(_cache.Count - 500)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var key in lruKeys)
                    {
                        _cache.TryRemove(key, out _);
                    }

                    _logger.LogDebug("Cleaned up {Count} LRU cache entries", lruKeys.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache cleanup");
            }
        }

        private long EstimateCacheSize(System.Collections.Generic.List<CacheEntry> entries)
        {
            try
            {
                long totalSize = 0;
                foreach (var entry in entries)
                {
                    // Rough estimation
                    totalSize += entry.Result.Content?.Length ?? 0;
                    totalSize += entry.Result.ErrorMessage?.Length ?? 0;
                    totalSize += 1000; // Overhead for object structure
                }
                return totalSize;
            }
            catch
            {
                return 0;
            }
        }

        private class CacheEntry
        {
            public AIAnalysisResult Result { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
            public DateTimeOffset ExpiresAt { get; set; }
            public DateTimeOffset LastAccessed { get; set; }
            public int AccessCount { get; set; }
        }
    }
} 