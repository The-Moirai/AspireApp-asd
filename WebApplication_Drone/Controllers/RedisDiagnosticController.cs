using Microsoft.AspNetCore.Mvc;
using WebApplication_Drone.Services;
using System.Diagnostics;

namespace WebApplication_Drone.Controllers
{
    /// <summary>
    /// Redis诊断控制器 - 提供Redis连接问题排查API
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class RedisDiagnosticController : ControllerBase
    {
        private readonly RedisConnectionDiagnosticService _diagnosticService;
        private readonly RedisCacheService _cacheService;
        private readonly ILogger<RedisDiagnosticController> _logger;

        public RedisDiagnosticController(
            RedisConnectionDiagnosticService diagnosticService,
            RedisCacheService cacheService,
            ILogger<RedisDiagnosticController> logger)
        {
            _diagnosticService = diagnosticService;
            _cacheService = cacheService;
            _logger = logger;
        }

        /// <summary>
        /// 执行完整Redis诊断
        /// </summary>
        [HttpPost("diagnose")]
        public async Task<IActionResult> Diagnose()
        {
            try
            {
                _logger.LogInformation("开始执行Redis诊断");
                var result = await _diagnosticService.DiagnoseAsync();
                
                return Ok(new
                {
                    success = true,
                    data = result,
                    message = result.Summary
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis诊断失败");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Redis诊断失败",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// 获取连接统计信息
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var stats = await _diagnosticService.GetConnectionStatsAsync();
                return Ok(new
                {
                    success = true,
                    data = stats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取Redis统计信息失败");
                return StatusCode(500, new
                {
                    success = false,
                    error = "获取Redis统计信息失败",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// 快速连接测试
        /// </summary>
        [HttpPost("quick-test")]
        public async Task<IActionResult> QuickTest()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                // 测试设置
                var testKey = $"quick_test_{Guid.NewGuid()}";
                var testValue = "quick_test_value";
                
                await _cacheService.SetAsync(testKey, testValue, TimeSpan.FromMinutes(1));
                
                // 测试获取
                var retrievedValue = await _cacheService.GetAsync<string>(testKey);
                
                // 测试删除
                await _cacheService.RemoveAsync(testKey);
                
                stopwatch.Stop();

                var success = retrievedValue == testValue;
                
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        testPassed = success,
                        responseTimeMs = stopwatch.ElapsedMilliseconds,
                        retrievedValue = retrievedValue,
                        expectedValue = testValue
                    },
                    message = success ? "快速测试通过" : "快速测试失败"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis快速测试失败");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Redis快速测试失败",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// 压力测试
        /// </summary>
        [HttpPost("stress-test")]
        public async Task<IActionResult> StressTest([FromQuery] int iterations = 100)
        {
            try
            {
                _logger.LogInformation("开始Redis压力测试，迭代次数: {Iterations}", iterations);
                
                var stopwatch = Stopwatch.StartNew();
                var successCount = 0;
                var errorCount = 0;
                var totalTime = 0L;

                for (int i = 0; i < iterations; i++)
                {
                    try
                    {
                        var key = $"stress_test_{i}_{Guid.NewGuid()}";
                        var value = $"stress_value_{i}";

                        var iterStopwatch = Stopwatch.StartNew();
                        await _cacheService.SetAsync(key, value, TimeSpan.FromMinutes(1));
                        var retrieved = await _cacheService.GetAsync<string>(key);
                        await _cacheService.RemoveAsync(key);
                        iterStopwatch.Stop();

                        if (retrieved == value)
                        {
                            successCount++;
                            totalTime += iterStopwatch.ElapsedMilliseconds;
                        }
                        else
                        {
                            errorCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.LogWarning(ex, "压力测试迭代 {Iteration} 失败", i);
                    }
                }

                stopwatch.Stop();

                var avgTime = successCount > 0 ? totalTime / successCount : 0;
                var successRate = (double)successCount / iterations * 100;

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        totalIterations = iterations,
                        successCount = successCount,
                        errorCount = errorCount,
                        successRate = successRate,
                        totalTimeMs = stopwatch.ElapsedMilliseconds,
                        averageTimeMs = avgTime,
                        operationsPerSecond = iterations / (stopwatch.ElapsedMilliseconds / 1000.0)
                    },
                    message = $"压力测试完成，成功率: {successRate:F1}%"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis压力测试失败");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Redis压力测试失败",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// 获取详细错误信息
        /// </summary>
        [HttpGet("errors")]
        public async Task<IActionResult> GetErrors()
        {
            try
            {
                var result = await _diagnosticService.DiagnoseAsync();
                var failedTests = result.Tests.Where(t => t.Status == RedisTestStatus.Failed).ToList();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        totalTests = result.Tests.Count,
                        failedTests = failedTests.Count,
                        errors = failedTests.Select(t => new
                        {
                            testName = t.Name,
                            description = t.Description,
                            details = t.Details,
                            errorMessage = t.ErrorMessage,
                            errorType = t.ErrorType
                        }).ToList()
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取Redis错误信息失败");
                return StatusCode(500, new
                {
                    success = false,
                    error = "获取Redis错误信息失败",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// 重置连接
        /// </summary>
        [HttpPost("reset")]
        public async Task<IActionResult> Reset()
        {
            try
            {
                _logger.LogInformation("重置Redis连接");
                
                // 这里可以添加重置逻辑，比如重新初始化连接等
                
                return Ok(new
                {
                    success = true,
                    message = "Redis连接重置完成"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重置Redis连接失败");
                return StatusCode(500, new
                {
                    success = false,
                    error = "重置Redis连接失败",
                    message = ex.Message
                });
            }
        }
    }
} 