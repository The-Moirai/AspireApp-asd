using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace WebApplication_Drone.Services
{
    /// <summary>
    /// Redis缓存服务 - 提供统一的缓存操作接口
    /// </summary>
    public class RedisCacheService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public RedisCacheService(
            IDistributedCache distributedCache,
            IMemoryCache memoryCache,
            ILogger<RedisCacheService> logger)
        {
            _distributedCache = distributedCache;
            _memoryCache = memoryCache;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                PropertyNameCaseInsensitive= true,
            };
        }

        /// <summary>
        /// 获取缓存项（优先从内存缓存获取）
        /// </summary>
        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                // 首先尝试从内存缓存获取
                if (_memoryCache.TryGetValue(key, out T? memoryValue))
                {
                    _logger.LogDebug("从内存缓存获取: {Key}", key);
                    return memoryValue;
                }

                // 从Redis获取
                var redisValue = await _distributedCache.GetStringAsync(key);
                if (string.IsNullOrEmpty(redisValue))
                {
                    _logger.LogDebug("缓存未命中: {Key}", key);
                    return default;
                }

                // 反序列化
                var value = JsonSerializer.Deserialize<T>(redisValue, _jsonOptions);
                
                // 同时设置到内存缓存（短期缓存）
                if (value != null)
                {
                    var memoryOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1),
                        Size = 1 // 为每个缓存项分配大小为1
                    };
                    _memoryCache.Set(key, value, memoryOptions);
                    _logger.LogDebug("从Redis缓存获取并设置到内存缓存: {Key}", key);
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
        /// 设置缓存项（同时设置到Redis和内存缓存）
        /// </summary>
        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            try
            {
                var jsonValue = JsonSerializer.Serialize(value, _jsonOptions);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(30)
                };

                // 设置到Redis
                await _distributedCache.SetStringAsync(key, jsonValue, options);
                
                // 同时设置到内存缓存（短期缓存）
                //var memoryExpiration = TimeSpan.FromMinutes(Math.Min(5, (expiration ?? TimeSpan.FromMinutes(30)).TotalMinutes));
                //var memoryOptions = new MemoryCacheEntryOptions
                //{
                //    AbsoluteExpirationRelativeToNow = memoryExpiration,
                //    Size = 1 // 为每个缓存项分配大小为1
                //};
                //_memoryCache.Set(key, value, memoryOptions);

                _logger.LogDebug("设置缓存: {Key}, 过期时间: {Expiration}", key, expiration);
            }
            catch (Exception ex)
            {
                // Redis设置失败时，只设置内存缓存作为降级方案
                _logger.LogWarning(ex, "Redis设置缓存失败，降级到内存缓存: {Key}", key);         
                try
                {
                    var memoryExpiration = TimeSpan.FromMinutes(Math.Min(5, (expiration ?? TimeSpan.FromMinutes(30)).TotalMinutes));
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
        /// 移除缓存项
        /// </summary>
        public async Task RemoveAsync(string key)
        {
            try
            {
                // 从Redis移除
                await _distributedCache.RemoveAsync(key);
                
                // 从内存缓存移除
                _memoryCache.Remove(key);

                _logger.LogDebug("移除缓存: {Key}", key);
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
        /// 获取或设置缓存项（如果不存在则设置）
        /// </summary>
        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
        {
            var value = await GetAsync<T>(key);
            if (value != null)
            {
                return value;
            }

            // 缓存未命中，执行工厂方法
            value = await factory();
            if (value != null)
            {
                await SetAsync(key, value, expiration);
            }

            return value;
        }

        /// <summary>
        /// 批量获取缓存项
        /// </summary>
        public async Task<Dictionary<string, T?>> GetMultipleAsync<T>(IEnumerable<string> keys)
        {
            var result = new Dictionary<string, T?>();
            
            foreach (var key in keys)
            {
                result[key] = await GetAsync<T>(key);
            }

            return result;
        }

        /// <summary>
        /// 批量设置缓存项
        /// </summary>
        public async Task SetMultipleAsync<T>(Dictionary<string, T> items, TimeSpan? expiration = null)
        {
            foreach (var item in items)
            {
                await SetAsync(item.Key, item.Value, expiration);
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

                // 检查Redis缓存
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
        public async Task<CacheStatistics> GetStatisticsAsync()
        {
            try
            {
                // 这里可以添加更详细的统计信息收集逻辑
                return new CacheStatistics
                {
                    Timestamp = DateTime.UtcNow,
                    TotalRequests = 0,
                    MemoryHits = 0,
                    RedisHits = 0,
                    CacheMisses = 0,
                    MemoryHitRate = 0,
                    RedisHitRate = 0,
                    MissRate = 0,
                    OverallHitRate = 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取缓存统计信息失败");
                return new CacheStatistics
                {
                    Timestamp = DateTime.UtcNow,
                    TotalRequests = 0,
                    MemoryHits = 0,
                    RedisHits = 0,
                    CacheMisses = 0,
                    MemoryHitRate = 0,
                    RedisHitRate = 0,
                    MissRate = 0,
                    OverallHitRate = 0
                };
            }
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public async Task ClearAllAsync()
        {
            try
            {
                // 清空内存缓存
                if (_memoryCache is MemoryCache memoryCache)
                {
                    memoryCache.Compact(1.0);
                }

                // 注意：Redis的清空操作需要谨慎使用，这里只是示例
                // 实际使用时可能需要更精确的键模式匹配
                _logger.LogWarning("清空所有缓存操作已执行");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清空缓存失败");
            }
        }
    }
} 