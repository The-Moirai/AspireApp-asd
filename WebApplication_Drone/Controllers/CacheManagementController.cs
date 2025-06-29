using Microsoft.AspNetCore.Mvc;
using WebApplication_Drone.Services;
using WebApplication_Drone.Services.Models;
using Microsoft.Extensions.Options;

namespace WebApplication_Drone.Controllers
{
    /// <summary>
    /// 缓存管理控制器 - 提供缓存监控、统计和管理功能
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CacheManagementController : ControllerBase
    {
        private readonly OptimizedRedisCacheService _optimizedCacheService;
        private readonly RedisCacheService _cacheService;
        private readonly RedisConnectionDiagnosticService _diagnosticService;
        private readonly ILogger<CacheManagementController> _logger;
        private readonly CacheOptions _cacheOptions;

        public CacheManagementController(
            OptimizedRedisCacheService optimizedCacheService,
            RedisCacheService cacheService,
            RedisConnectionDiagnosticService diagnosticService,
            ILogger<CacheManagementController> logger,
            IOptions<CacheOptions> cacheOptions)
        {
            _optimizedCacheService = optimizedCacheService;
            _cacheService = cacheService;
            _diagnosticService = diagnosticService;
            _logger = logger;
            _cacheOptions = cacheOptions.Value;
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var optimizedStats = _optimizedCacheService.GetStatistics();
                var legacyStats = await _cacheService.GetStatisticsAsync();
                var redisStats = await _diagnosticService.GetConnectionStatsAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        optimizedCache = optimizedStats,
                        legacyCache = legacyStats,
                        redisConnection = redisStats,
                        configuration = new
                        {
                            redis = _cacheOptions.Redis,
                            memory = _cacheOptions.Memory,
                            strategy = _cacheOptions.Strategy
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取缓存统计信息失败");
                return StatusCode(500, new
                {
                    success = false,
                    error = "获取缓存统计信息失败",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// 获取缓存配置信息
        /// </summary>
        [HttpGet("configuration")]
        public IActionResult GetConfiguration()
        {
            try
            {
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        redis = _cacheOptions.Redis,
                        memory = _cacheOptions.Memory,
                        strategy = _cacheOptions.Strategy
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取缓存配置信息失败");
                return StatusCode(500, new
                {
                    success = false,
                    error = "获取缓存配置信息失败",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// 检查缓存项是否存在
        /// </summary>
        [HttpGet("exists/{key}")]
        public async Task<IActionResult> Exists(string key)
        {
            try
            {
                var exists = await _optimizedCacheService.ExistsAsync(key);
                
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        key = key,
                        exists = exists
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查缓存项存在性失败: {Key}", key);
                return StatusCode(500, new
                {
                    success = false,
                    error = "检查缓存项存在性失败",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// 移除缓存项
        /// </summary>
        [HttpDelete("remove/{key}")]
        public async Task<IActionResult> Remove(string key)
        {
            try
            {
                await _optimizedCacheService.RemoveAsync(key);
                
                return Ok(new
                {
                    success = true,
                    message = $"缓存项 {key} 已移除"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "移除缓存项失败: {Key}", key);
                return StatusCode(500, new
                {
                    success = false,
                    error = "移除缓存项失败",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// 刷新缓存项（延长过期时间）
        /// </summary>
        [HttpPost("refresh/{key}")]
        public async Task<IActionResult> Refresh(string key)
        {
            try
            {
                await _optimizedCacheService.RefreshAsync(key);
                
                return Ok(new
                {
                    success = true,
                    message = $"缓存项 {key} 已刷新"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新缓存项失败: {Key}", key);
                return StatusCode(500, new
                {
                    success = false,
                    error = "刷新缓存项失败",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        [HttpPost("clear-all")]
        public async Task<IActionResult> ClearAll()
        {
            try
            {
                await _optimizedCacheService.ClearAllAsync();
                await _cacheService.ClearAllAsync();
                
                return Ok(new
                {
                    success = true,
                    message = "所有缓存已清除"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清除所有缓存失败");
                return StatusCode(500, new
                {
                    success = false,
                    error = "清除所有缓存失败",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// 批量移除缓存项
        /// </summary>
        [HttpPost("batch-remove")]
        public async Task<IActionResult> BatchRemove([FromBody] List<string> keys)
        {
            try
            {
                var tasks = keys.Select(key => _optimizedCacheService.RemoveAsync(key));
                await Task.WhenAll(tasks);
                
                return Ok(new
                {
                    success = true,
                    message = $"已移除 {keys.Count} 个缓存项",
                    data = new
                    {
                        removedCount = keys.Count,
                        keys = keys
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量移除缓存项失败");
                return StatusCode(500, new
                {
                    success = false,
                    error = "批量移除缓存项失败",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// 获取缓存性能报告
        /// </summary>
        [HttpGet("performance-report")]
        public async Task<IActionResult> GetPerformanceReport()
        {
            try
            {
                var optimizedStats = _optimizedCacheService.GetStatistics();
                var redisStats = await _diagnosticService.GetConnectionStatsAsync();

                var report = new
                {
                    timestamp = DateTime.UtcNow,
                    optimizedCache = new
                    {
                        totalRequests = optimizedStats.TotalRequests,
                        memoryHitRate = $"{optimizedStats.MemoryHitRate:P2}",
                        redisHitRate = $"{optimizedStats.RedisHitRate:P2}",
                        overallHitRate = $"{optimizedStats.OverallHitRate:P2}",
                        missRate = $"{optimizedStats.MissRate:P2}"
                    },
                    redisConnection = new
                    {
                        isConnected = redisStats.IsConnected,
                        connectionCount = redisStats.ConnectionCount,
                        operationCount = redisStats.OperationCount
                    },
                    recommendations = GetRecommendations(optimizedStats, redisStats)
                };

                return Ok(new
                {
                    success = true,
                    data = report
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取缓存性能报告失败");
                return StatusCode(500, new
                {
                    success = false,
                    error = "获取缓存性能报告失败",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// 测试缓存性能
        /// </summary>
        [HttpPost("performance-test")]
        public async Task<IActionResult> PerformanceTest([FromQuery] int iterations = 100)
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var successCount = 0;
                var errorCount = 0;

                for (int i = 0; i < iterations; i++)
                {
                    try
                    {
                        var key = $"perf_test_{i}_{Guid.NewGuid()}";
                        var value = $"test_value_{i}";

                        await _optimizedCacheService.SetAsync(key, value, TimeSpan.FromMinutes(1), CachePriority.High);
                        var retrieved = await _optimizedCacheService.GetAsync<string>(key);
                        await _optimizedCacheService.RemoveAsync(key);

                        if (retrieved == value)
                        {
                            successCount++;
                        }
                        else
                        {
                            errorCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.LogWarning(ex, "性能测试迭代 {Iteration} 失败", i);
                    }
                }

                stopwatch.Stop();

                var successRate = (double)successCount / iterations * 100;
                var avgTime = successCount > 0 ? stopwatch.ElapsedMilliseconds / successCount : 0;

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        totalIterations = iterations,
                        successCount = successCount,
                        errorCount = errorCount,
                        successRate = $"{successRate:F1}%",
                        totalTimeMs = stopwatch.ElapsedMilliseconds,
                        averageTimeMs = avgTime,
                        operationsPerSecond = iterations / (stopwatch.ElapsedMilliseconds / 1000.0)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "缓存性能测试失败");
                return StatusCode(500, new
                {
                    success = false,
                    error = "缓存性能测试失败",
                    message = ex.Message
                });
            }
        }

        #region 私有方法

        private List<string> GetRecommendations(CacheStatistics stats, RedisConnectionStats redisStats)
        {
            var recommendations = new List<string>();

            if (stats.OverallHitRate < 0.8)
            {
                recommendations.Add("缓存命中率较低，建议增加缓存预热策略");
            }

            if (stats.MemoryHitRate < 0.3)
            {
                recommendations.Add("内存缓存命中率较低，建议调整内存缓存过期时间");
            }

            if (stats.RedisHitRate < 0.5)
            {
                recommendations.Add("Redis缓存命中率较低，建议优化缓存键策略");
            }

            if (!redisStats.IsConnected)
            {
                recommendations.Add("Redis连接异常，请检查Redis服务状态");
            }

            if (stats.TotalRequests > 0 && stats.TotalRequests < 100)
            {
                recommendations.Add("缓存请求较少，建议增加缓存使用");
            }

            if (recommendations.Count == 0)
            {
                recommendations.Add("缓存性能良好，无需优化");
            }

            return recommendations;
        }

        #endregion
    }
} 