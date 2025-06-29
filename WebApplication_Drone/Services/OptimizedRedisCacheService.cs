using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebApplication_Drone.Services.Models;

namespace WebApplication_Drone.Services
{
    /// <summary>
    /// 优化的Redis缓存服务 - 基于测试结果实现分层缓存和性能优化
    /// </summary>
    public class OptimizedRedisCacheService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<OptimizedRedisCacheService> _logger;
        private readonly CacheOptions _cacheOptions;
        private readonly JsonSerializerOptions _jsonOptions;

        // 缓存统计
        private long _totalRequests = 0;
        private long _memoryHits = 0;
        private long _redisHits = 0;
        private long _cacheMisses = 0;

        public OptimizedRedisCacheService(
            IDistributedCache distributedCache,
            IMemoryCache memoryCache,
            ILogger<OptimizedRedisCacheService> logger,
            IOptions<CacheOptions> cacheOptions)
        {
            _distributedCache = distributedCache;
            _memoryCache = memoryCache;
            _logger = logger;
            _cacheOptions = cacheOptions.Value;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// 获取缓存项（分层缓存策略）
        /// </summary>
        public async Task<T?> GetAsync<T>(string key, CachePriority priority = CachePriority.Medium)
        {
            Interlocked.Increment(ref _totalRequests);

            try
            {
                // 1. 首先尝试从内存缓存获取（最快）
                if (_memoryCache.TryGetValue(key, out T? memoryValue))
                {
                    Interlocked.Increment(ref _memoryHits);
                    _logger.LogDebug("内存缓存命中: {Key}", key);
                    return memoryValue;
                }

                // 2. 从Redis获取
                var redisValue = await _distributedCache.GetStringAsync(key);
                if (string.IsNullOrEmpty(redisValue))
                {
                    Interlocked.Increment(ref _cacheMisses);
                    _logger.LogDebug("缓存未命中: {Key}", key);
                    return default;
                }

                Interlocked.Increment(ref _redisHits);

                // 3. 反序列化
                var value = JsonSerializer.Deserialize<T>(redisValue, _jsonOptions);
                
                // 4. 根据优先级设置到内存缓存
                if (value != null)
                {
                    var memoryExpiration = GetMemoryCacheExpiration(priority);
                    var memoryOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = memoryExpiration,
                        Size = 1 // 为每个缓存项分配大小为1
                    };
                    _memoryCache.Set(key, value, memoryOptions);
                    _logger.LogDebug("Redis缓存命中并设置到内存缓存: {Key}, 优先级: {Priority}", key, priority);
                }

                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取缓存失败: {Key}", key);
                return default;
            }
        }

        /// <summary>
        /// 设置缓存项（分层缓存策略）
        /// </summary>
        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CachePriority priority = CachePriority.Medium)
        {
            try
            {
                var jsonValue = JsonSerializer.Serialize(value, _jsonOptions);
                var redisExpiration = expiration ?? GetRedisCacheExpiration(priority);
                var memoryExpiration = GetMemoryCacheExpiration(priority);

                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = redisExpiration
                };

                // 1. 设置到Redis（持久化）
                await _distributedCache.SetStringAsync(key, jsonValue, options);
                
                // 2. 设置到内存缓存（快速访问）
                var memoryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = memoryExpiration,
                    Size = 1 // 为每个缓存项分配大小为1
                };
                _memoryCache.Set(key, value, memoryOptions);

                _logger.LogDebug("设置分层缓存: {Key}, Redis过期: {RedisExp}, 内存过期: {MemoryExp}, 优先级: {Priority}", 
                    key, redisExpiration, memoryExpiration, priority);
            }
            catch (Exception ex)
            {
                // Redis设置失败时，只设置内存缓存作为降级方案
                _logger.LogWarning(ex, "Redis设置缓存失败，降级到内存缓存: {Key}", key);
                
                try
                {
                    var memoryExpiration = GetMemoryCacheExpiration(priority);
                    var memoryOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = memoryExpiration,
                        Size = 1 // 为每个缓存项分配大小为1
                    };
                    _memoryCache.Set(key, value, memoryOptions);
                    _logger.LogDebug("降级设置内存缓存成功: {Key}", key);
                }
                catch (Exception memoryEx)
                {
                    _logger.LogError(memoryEx, "内存缓存设置也失败: {Key}", key);
                }
            }
        }

        /// <summary>
        /// 获取或设置缓存项（缓存预热策略）
        /// </summary>
        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CachePriority priority = CachePriority.Medium)
        {
            var value = await GetAsync<T>(key, priority);
            if (value != null)
            {
                return value;
            }

            // 缓存未命中，执行工厂方法
            value = await factory();
            if (value != null)
            {
                await SetAsync(key, value, expiration, priority);
            }

            return value;
        }

        /// <summary>
        /// 批量获取缓存项（性能优化）
        /// </summary>
        public async Task<Dictionary<string, T?>> GetMultipleAsync<T>(IEnumerable<string> keys, CachePriority priority = CachePriority.Medium)
        {
            var result = new Dictionary<string, T?>();
            var tasks = new List<Task<KeyValuePair<string, T?>>>();

            foreach (var key in keys)
            {
                tasks.Add(GetSingleAsync<T>(key, priority));
            }

            var results = await Task.WhenAll(tasks);
            foreach (var kvp in results)
            {
                result[kvp.Key] = kvp.Value;
            }

            return result;
        }

        /// <summary>
        /// 批量设置缓存项（性能优化）
        /// </summary>
        public async Task SetMultipleAsync<T>(Dictionary<string, T> keyValuePairs, TimeSpan? expiration = null, CachePriority priority = CachePriority.Medium)
        {
            var tasks = new List<Task>();

            foreach (var kvp in keyValuePairs)
            {
                tasks.Add(SetAsync(kvp.Key, kvp.Value, expiration, priority));
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 移除缓存项（分层失效）
        /// </summary>
        public async Task RemoveAsync(string key)
        {
            try
            {
                // 1. 从Redis移除
                await _distributedCache.RemoveAsync(key);
                
                // 2. 从内存缓存移除
                _memoryCache.Remove(key);

                _logger.LogDebug("移除分层缓存: {Key}", key);
            }
            catch (Exception ex)
            {
                // Redis移除失败时，只清除内存缓存作为降级方案
                _logger.LogWarning(ex, "Redis移除缓存失败，降级清除内存缓存: {Key}", key);
                
                try
                {
                    _memoryCache.Remove(key);
                    _logger.LogDebug("降级清除内存缓存成功: {Key}", key);
                }
                catch (Exception memoryEx)
                {
                    _logger.LogError(memoryEx, "内存缓存清除也失败: {Key}", key);
                }
            }
        }

        /// <summary>
        /// 刷新缓存项（延长过期时间）
        /// </summary>
        public async Task RefreshAsync(string key)
        {
            try
            {
                await _distributedCache.RefreshAsync(key);
                _logger.LogDebug("刷新缓存: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新缓存失败: {Key}", key);
            }
        }

        /// <summary>
        /// 检查缓存项是否存在
        /// </summary>
        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                // 先检查内存缓存
                if (_memoryCache.TryGetValue(key, out _))
                {
                    return true;
                }

                // 再检查Redis
                var value = await _distributedCache.GetStringAsync(key);
                return !string.IsNullOrEmpty(value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查缓存存在性失败: {Key}", key);
                return false;
            }
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            var total = _totalRequests;
            var memoryHitRate = total > 0 ? (double)_memoryHits / total : 0;
            var redisHitRate = total > 0 ? (double)_redisHits / total : 0;
            var missRate = total > 0 ? (double)_cacheMisses / total : 0;

            return new CacheStatistics
            {
                Timestamp = DateTime.UtcNow,
                TotalRequests = total,
                MemoryHits = _memoryHits,
                RedisHits = _redisHits,
                CacheMisses = _cacheMisses,
                MemoryHitRate = memoryHitRate,
                RedisHitRate = redisHitRate,
                MissRate = missRate,
                OverallHitRate = memoryHitRate + redisHitRate
            };
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public async Task ClearAllAsync()
        {
            try
            {
                // 注意：这里只能清除内存缓存，Redis需要单独处理
                if (_memoryCache is MemoryCache memoryCache)
                {
                    memoryCache.Compact(1.0);
                }
                _logger.LogInformation("清除所有内存缓存");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清除缓存失败");
            }
        }

        #region 私有方法

        private async Task<KeyValuePair<string, T?>> GetSingleAsync<T>(string key, CachePriority priority)
        {
            var value = await GetAsync<T>(key, priority);
            return new KeyValuePair<string, T?>(key, value);
        }

        private TimeSpan GetRedisCacheExpiration(CachePriority priority)
        {
            return priority switch
            {
                CachePriority.Critical => TimeSpan.FromMinutes(_cacheOptions.Redis.ShortTermExpirationMinutes),
                CachePriority.High => TimeSpan.FromMinutes(_cacheOptions.Redis.DefaultExpirationMinutes),
                CachePriority.Medium => TimeSpan.FromMinutes(_cacheOptions.Redis.DefaultExpirationMinutes * 2),
                CachePriority.Low => TimeSpan.FromMinutes(_cacheOptions.Redis.LongTermExpirationMinutes),
                _ => TimeSpan.FromMinutes(_cacheOptions.Redis.DefaultExpirationMinutes)
            };
        }

        private TimeSpan GetMemoryCacheExpiration(CachePriority priority)
        {
            return priority switch
            {
                CachePriority.Critical => TimeSpan.FromMinutes(1),
                CachePriority.High => TimeSpan.FromMinutes(2),
                CachePriority.Medium => TimeSpan.FromMinutes(5),
                CachePriority.Low => TimeSpan.FromMinutes(10),
                _ => TimeSpan.FromMinutes(5)
            };
        }

        #endregion
    }

    /// <summary>
    /// 增强的缓存统计信息
    /// </summary>
    public class CacheStatistics
    {
        public DateTime Timestamp { get; set; }
        public long TotalRequests { get; set; }
        public long MemoryHits { get; set; }
        public long RedisHits { get; set; }
        public long CacheMisses { get; set; }
        public double MemoryHitRate { get; set; }
        public double RedisHitRate { get; set; }
        public double MissRate { get; set; }
        public double OverallHitRate { get; set; }
    }
} 