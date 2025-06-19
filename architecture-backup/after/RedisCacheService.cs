using ClassLibrary_Core.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using StackExchange.Redis;

namespace WebApplication_Drone.Services
{
    /// <summary>
    /// Redis分布式缓存服务实现
    /// </summary>
    public class RedisCacheService : ICacheService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _database;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public RedisCacheService(
            IDistributedCache distributedCache,
            IConnectionMultiplexer redis,
            ILogger<RedisCacheService> logger)
        {
            _distributedCache = distributedCache;
            _redis = redis;
            _database = redis.GetDatabase();
            _logger = logger;
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <summary>
        /// 获取缓存项
        /// </summary>
        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                var cachedValue = await _distributedCache.GetStringAsync(key);
                if (string.IsNullOrEmpty(cachedValue))
                {
                    return null;
                }

                return JsonSerializer.Deserialize<T>(cachedValue, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从缓存获取键 {Key} 时出错", key);
                return null;
            }
        }

        /// <summary>
        /// 设置缓存项
        /// </summary>
        public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var json = JsonSerializer.Serialize(value, _jsonOptions);
                var options = new DistributedCacheEntryOptions();
                
                if (expiration.HasValue)
                {
                    options.SetAbsoluteExpiration(expiration.Value);
                }
                else
                {
                    // 默认过期时间1小时
                    options.SetAbsoluteExpiration(TimeSpan.FromHours(1));
                }

                await _distributedCache.SetStringAsync(key, json, options);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置缓存键 {Key} 时出错", key);
                return false;
            }
        }

        /// <summary>
        /// 移除缓存项
        /// </summary>
        public async Task<bool> RemoveAsync(string key)
        {
            try
            {
                await _distributedCache.RemoveAsync(key);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "移除缓存键 {Key} 时出错", key);
                return false;
            }
        }

        /// <summary>
        /// 检查缓存项是否存在
        /// </summary>
        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                return await _database.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查缓存键 {Key} 存在性时出错", key);
                return false;
            }
        }

        /// <summary>
        /// 批量获取缓存项
        /// </summary>
        public async Task<Dictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys) where T : class
        {
            var result = new Dictionary<string, T?>();
            
            try
            {
                var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
                var values = await _database.StringGetAsync(redisKeys);

                for (int i = 0; i < keys.Count(); i++)
                {
                    var key = keys.ElementAt(i);
                    var value = values[i];
                    
                    if (value.HasValue)
                    {
                        try
                        {
                            result[key] = JsonSerializer.Deserialize<T>(value!, _jsonOptions);
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "反序列化缓存键 {Key} 时出错", key);
                            result[key] = null;
                        }
                    }
                    else
                    {
                        result[key] = null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量获取缓存时出错");
                // 返回空值字典
                foreach (var key in keys)
                {
                    result[key] = null;
                }
            }

            return result;
        }

        /// <summary>
        /// 批量设置缓存项
        /// </summary>
        public async Task<bool> SetManyAsync<T>(Dictionary<string, T> keyValues, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var keyValuePairs = new List<KeyValuePair<RedisKey, RedisValue>>();
                
                foreach (var kvp in keyValues)
                {
                    var json = JsonSerializer.Serialize(kvp.Value, _jsonOptions);
                    keyValuePairs.Add(new KeyValuePair<RedisKey, RedisValue>(kvp.Key, json));
                }

                var success = await _database.StringSetAsync(keyValuePairs.ToArray());
                
                // 设置过期时间
                if (success && expiration.HasValue)
                {
                    var tasks = keyValues.Keys.Select(key => _database.KeyExpireAsync(key, expiration.Value));
                    await Task.WhenAll(tasks);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量设置缓存时出错");
                return false;
            }
        }

        /// <summary>
        /// 批量移除缓存项
        /// </summary>
        public async Task<bool> RemoveManyAsync(IEnumerable<string> keys)
        {
            try
            {
                var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
                await _database.KeyDeleteAsync(redisKeys);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量移除缓存时出错");
                return false;
            }
        }

        /// <summary>
        /// 根据模式获取键
        /// </summary>
        public async Task<IEnumerable<string>> GetKeysByPatternAsync(string pattern)
        {
            try
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                var keys = server.Keys(pattern: pattern);
                return keys.Select(k => k.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "根据模式 {Pattern} 获取键时出错", pattern);
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// 根据模式移除缓存项
        /// </summary>
        public async Task<bool> RemoveByPatternAsync(string pattern)
        {
            try
            {
                var keys = await GetKeysByPatternAsync(pattern);
                if (keys.Any())
                {
                    await RemoveManyAsync(keys);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "根据模式 {Pattern} 移除缓存时出错", pattern);
                return false;
            }
        }

        /// <summary>
        /// 获取缓存项数量
        /// </summary>
        public async Task<long> GetCacheCountAsync()
        {
            try
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                return await server.DatabaseSizeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取缓存项数量时出错");
                return 0;
            }
        }

        /// <summary>
        /// 清空缓存
        /// </summary>
        public async Task<bool> ClearCacheAsync()
        {
            try
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                await server.FlushDatabaseAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清空缓存时出错");
                return false;
            }
        }
    }

    /// <summary>
    /// 缓存键生成器
    /// </summary>
    public static class CacheKeys
    {
        private const string PREFIX = "AspireApp:";
        
        // 无人机相关缓存键
        public static string DroneKey(Guid droneId) => $"{PREFIX}Drone:{droneId}";
        public static string DroneByNameKey(string droneName) => $"{PREFIX}Drone:Name:{droneName}";
        public static string AllDronesKey() => $"{PREFIX}Drones:All";
        public static string DronesByStatusKey(string status) => $"{PREFIX}Drones:Status:{status}";
        public static string DroneStatusSummaryKey() => $"{PREFIX}Drones:StatusSummary";
        
        // 任务相关缓存键
        public static string TaskKey(Guid taskId) => $"{PREFIX}Task:{taskId}";
        public static string AllTasksKey() => $"{PREFIX}Tasks:All";
        public static string TasksByStatusKey(string status) => $"{PREFIX}Tasks:Status:{status}";
        public static string TaskStatusSummaryKey() => $"{PREFIX}Tasks:StatusSummary";
        public static string SubTasksByDroneKey(string droneName) => $"{PREFIX}SubTasks:Drone:{droneName}";
        
        // 历史数据相关缓存键
        public static string DroneHistoryKey(Guid droneId, DateTime startTime, DateTime endTime) => 
            $"{PREFIX}DroneHistory:{droneId}:{startTime:yyyyMMddHHmm}:{endTime:yyyyMMddHHmm}";
        
        // 统计数据相关缓存键
        public static string DatabaseStatsKey() => $"{PREFIX}Stats:Database";
        public static string SystemHealthKey() => $"{PREFIX}Stats:Health";
        
        // 模式匹配
        public static string DronePattern() => $"{PREFIX}Drone:*";
        public static string TaskPattern() => $"{PREFIX}Task:*";
        public static string StatsPattern() => $"{PREFIX}Stats:*";
    }
} 