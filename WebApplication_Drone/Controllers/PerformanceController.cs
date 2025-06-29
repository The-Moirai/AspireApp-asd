using Microsoft.AspNetCore.Mvc;
using WebApplication_Drone.Services.Clean;
using WebApplication_Drone.Services.Models;
using WebApplication_Drone.Services;
using System.Diagnostics;
using System.Runtime;

namespace WebApplication_Drone.Controllers
{
    /// <summary>
    /// 性能监控控制器 - 提供系统性能监控API
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class PerformanceController : ControllerBase
    {
        private readonly DroneService _droneService;
        private readonly TaskService _taskService;
        private readonly PerformanceMonitoringService _performanceService;
        private readonly ILogger<PerformanceController> _logger;
        private readonly Process _currentProcess;

        public PerformanceController(
            DroneService droneService,
            TaskService taskService,
            PerformanceMonitoringService performanceService,
            ILogger<PerformanceController> logger)
        {
            _droneService = droneService;
            _taskService = taskService;
            _performanceService = performanceService;
            _logger = logger;
            _currentProcess = Process.GetCurrentProcess();
        }

        /// <summary>
        /// 获取系统整体性能概览
        /// </summary>
        [HttpGet("overview")]
        public IActionResult GetSystemOverview()
        {
            try
            {
                var droneStats = _droneService.GetStatistics();
                var taskStats = _taskService.GetStatistics();
                var performanceMetrics = _performanceService.GetCurrentMetrics();

                var overview = new
                {
                    timestamp = DateTime.UtcNow,
                    system = new
                    {
                        cpuUsage = performanceMetrics?.CpuUsagePercent ?? 0,
                        memoryUsageMB = performanceMetrics?.MemoryUsageMB ?? 0,
                        availableMemoryMB = performanceMetrics?.AvailableMemoryMB ?? 0,
                        threadCount = performanceMetrics?.ThreadCount ?? 0,
                        handleCount = performanceMetrics?.HandleCount ?? 0,
                        uptime = DateTime.Now - _currentProcess.StartTime
                    },
                    gc = new
                    {
                        gen0Collections = performanceMetrics?.Gen0Collections ?? 0,
                        gen1Collections = performanceMetrics?.Gen1Collections ?? 0,
                        gen2Collections = performanceMetrics?.Gen2Collections ?? 0,
                        totalAllocatedBytes = performanceMetrics?.TotalAllocatedBytes ?? 0
                    },
                    services = new
                    {
                        drone = new
                        {
                            totalDrones = droneStats.TotalDrones,
                            onlineDrones = droneStats.OnlineDrones,
                            totalOperations = droneStats.TotalOperations,
                            cacheHitRate = droneStats.CacheHitRate,
                            averageResponseTimeMs = droneStats.AverageResponseTimeMs
                        },
                        task = new
                        {
                            totalTasks = taskStats.TotalTasks,
                            activeTasks = taskStats.ActiveTasks,
                            totalOperations = taskStats.TotalOperations,
                            cacheHitRate = taskStats.CacheHitRate,
                            averageResponseTimeMs = taskStats.AverageResponseTimeMs
                        }
                    },
                    performance = new
                    {
                        requestsPerSecond = performanceMetrics?.RequestsPerSecond ?? 0,
                        averageResponseTimeMs = performanceMetrics?.AverageResponseTimeMs ?? 0,
                        totalExceptions = performanceMetrics?.TotalExceptions ?? 0,
                        activeConnections = performanceMetrics?.ActiveConnections ?? 0
                    }
                };

                return Ok(new { success = true, data = overview });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取系统概览失败");
                return StatusCode(500, new { error = "获取系统概览失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取无人机服务性能统计
        /// </summary>
        [HttpGet("drone-stats")]
        public IActionResult GetDroneStatistics()
        {
            try
            {
                var stats = _droneService.GetStatistics();
                return Ok(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取无人机统计失败");
                return StatusCode(500, new { error = "获取无人机统计失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取任务服务性能统计
        /// </summary>
        [HttpGet("task-stats")]
        public IActionResult GetTaskStatistics()
        {
            try
            {
                var stats = _taskService.GetStatistics();
                return Ok(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取任务统计失败");
                return StatusCode(500, new { error = "获取任务统计失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取性能监控历史数据
        /// </summary>
        [HttpGet("history")]
        public IActionResult GetPerformanceHistory([FromQuery] int count = 100)
        {
            try
            {
                var history = _performanceService.GetMetricsHistory(count);
                return Ok(new { success = true, data = history });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取性能历史数据失败");
                return StatusCode(500, new { error = "获取性能历史数据失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取性能警告历史
        /// </summary>
        [HttpGet("alerts")]
        public IActionResult GetPerformanceAlerts([FromQuery] int count = 50)
        {
            try
            {
                var alerts = _performanceService.GetAlertHistory(count);
                return Ok(new { success = true, data = alerts });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取性能警告失败");
                return StatusCode(500, new { error = "获取性能警告失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取当前性能指标
        /// </summary>
        [HttpGet("current")]
        public IActionResult GetCurrentMetrics()
        {
            try
            {
                var metrics = _performanceService.GetCurrentMetrics();
                return Ok(new { success = true, data = metrics });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取当前性能指标失败");
                return StatusCode(500, new { error = "获取当前性能指标失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取系统资源使用情况
        /// </summary>
        [HttpGet("resources")]
        public IActionResult GetResourceUsage()
        {
            try
            {
                var resources = new
                {
                    timestamp = DateTime.UtcNow,
                    memory = new
                    {
                        workingSetMB = _currentProcess.WorkingSet64 / 1024 / 1024,
                        privateMemoryMB = _currentProcess.PrivateMemorySize64 / 1024 / 1024,
                        virtualMemoryMB = _currentProcess.VirtualMemorySize64 / 1024 / 1024,
                        totalMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024,
                        gcGen0Collections = GC.CollectionCount(0),
                        gcGen1Collections = GC.CollectionCount(1),
                        gcGen2Collections = GC.CollectionCount(2)
                    },
                    cpu = new
                    {
                        totalProcessorTimeMs = _currentProcess.TotalProcessorTime.TotalMilliseconds,
                        userProcessorTimeMs = _currentProcess.UserProcessorTime.TotalMilliseconds,
                        privilegedProcessorTimeMs = _currentProcess.PrivilegedProcessorTime.TotalMilliseconds
                    },
                    system = new
                    {
                        threadCount = _currentProcess.Threads.Count,
                        handleCount = _currentProcess.HandleCount,
                        processId = _currentProcess.Id,
                        startTime = _currentProcess.StartTime,
                        uptime = DateTime.Now - _currentProcess.StartTime,
                        machineName = Environment.MachineName,
                        processorCount = Environment.ProcessorCount,
                        osVersion = Environment.OSVersion.ToString()
                    }
                };

                return Ok(new { success = true, data = resources });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取资源使用情况失败");
                return StatusCode(500, new { error = "获取资源使用情况失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        [HttpGet("cache-stats")]
        public IActionResult GetCacheStatistics()
        {
            try
            {
                var droneStats = _droneService.GetStatistics();
                var taskStats = _taskService.GetStatistics();

                var cacheStats = new
                {
                    timestamp = DateTime.UtcNow,
                    droneService = new
                    {
                        totalOperations = droneStats.TotalOperations,
                        cacheHits = droneStats.CacheHits,
                        cacheMisses = droneStats.CacheMisses,
                        cacheHitRate = droneStats.CacheHitRate,
                        averageResponseTimeMs = droneStats.AverageResponseTimeMs
                    },
                    taskService = new
                    {
                        totalOperations = taskStats.TotalOperations,
                        cacheHits = taskStats.CacheHits,
                        cacheMisses = taskStats.CacheMisses,
                        cacheHitRate = taskStats.CacheHitRate,
                        averageResponseTimeMs = taskStats.AverageResponseTimeMs
                    },
                    overall = new
                    {
                        totalOperations = droneStats.TotalOperations + taskStats.TotalOperations,
                        totalCacheHits = droneStats.CacheHits + taskStats.CacheHits,
                        totalCacheMisses = droneStats.CacheMisses + taskStats.CacheMisses,
                        overallCacheHitRate = (droneStats.TotalOperations + taskStats.TotalOperations) > 0 
                            ? (double)(droneStats.CacheHits + taskStats.CacheHits) / (droneStats.TotalOperations + taskStats.TotalOperations) * 100 
                            : 0
                    }
                };

                return Ok(new { success = true, data = cacheStats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取缓存统计失败");
                return StatusCode(500, new { error = "获取缓存统计失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 重置性能统计
        /// </summary>
        [HttpPost("reset")]
        public IActionResult ResetStatistics()
        {
            try
            {
                _performanceService.ResetStatistics();
                _logger.LogInformation("性能统计已重置");
                return Ok(new { success = true, message = "性能统计已重置" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重置性能统计失败");
                return StatusCode(500, new { error = "重置性能统计失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 手动触发垃圾回收
        /// </summary>
        [HttpPost("gc")]
        public IActionResult TriggerGarbageCollection()
        {
            try
            {
                var beforeMemory = GC.GetTotalMemory(false);
                
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                var afterMemory = GC.GetTotalMemory(false);
                var freedMemory = beforeMemory - afterMemory;
                
                _logger.LogInformation("手动垃圾回收完成 - 释放内存: {FreedMemory} bytes", freedMemory);
                
                return Ok(new
                {
                    success = true,
                    message = "垃圾回收完成",
                    data = new
                    {
                        beforeMemoryMB = beforeMemory / 1024 / 1024,
                        afterMemoryMB = afterMemory / 1024 / 1024,
                        freedMemoryMB = freedMemory / 1024 / 1024,
                        timestamp = DateTime.UtcNow
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "触发垃圾回收失败");
                return StatusCode(500, new { error = "触发垃圾回收失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取系统健康状态
        /// </summary>
        [HttpGet("health")]
        public IActionResult GetHealthStatus()
        {
            try
            {
                var droneStats = _droneService.GetStatistics();
                var taskStats = _taskService.GetStatistics();
                var performanceMetrics = _performanceService.GetCurrentMetrics();

                var healthStatus = new
                {
                    timestamp = DateTime.UtcNow,
                    overall = "Healthy",
                    checks = new
                    {
                        droneService = droneStats.DatabaseConnected ? "Healthy" : "Unhealthy",
                        taskService = taskStats.DatabaseConnected ? "Healthy" : "Unhealthy",
                        memory = (performanceMetrics?.MemoryUsageMB ?? 0) < 1024 ? "Healthy" : "Warning",
                        cpu = (performanceMetrics?.CpuUsagePercent ?? 0) < 80 ? "Healthy" : "Warning"
                    },
                    metrics = new
                    {
                        memoryUsageMB = performanceMetrics?.MemoryUsageMB ?? 0,
                        cpuUsagePercent = performanceMetrics?.CpuUsagePercent ?? 0,
                        cacheHitRate = droneStats.CacheHitRate,
                        averageResponseTimeMs = droneStats.AverageResponseTimeMs
                    }
                };

                return Ok(new { success = true, data = healthStatus });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取健康状态失败");
                return StatusCode(500, new { error = "获取健康状态失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取性能趋势分析
        /// </summary>
        [HttpGet("trends")]
        public IActionResult GetPerformanceTrends([FromQuery] int hours = 24)
        {
            try
            {
                var history = _performanceService.GetMetricsHistory(288); // 24小时的数据点
                var cutoffTime = DateTime.UtcNow.AddHours(-hours);
                
                var filteredHistory = history.Where(h => h.Timestamp >= cutoffTime).ToList();
                
                if (!filteredHistory.Any())
                {
                    return Ok(new { success = true, data = new { message = "没有足够的历史数据" } });
                }

                var trends = new
                {
                    period = $"{hours}小时",
                    dataPoints = filteredHistory.Count,
                    averages = new
                    {
                        cpuUsage = filteredHistory.Average(h => h.CpuUsagePercent),
                        memoryUsage = filteredHistory.Average(h => h.MemoryUsageMB),
                        responseTime = filteredHistory.Average(h => h.AverageResponseTimeMs),
                        requestsPerSecond = filteredHistory.Average(h => h.RequestsPerSecond)
                    },
                    peaks = new
                    {
                        maxCpuUsage = filteredHistory.Max(h => h.CpuUsagePercent),
                        maxMemoryUsage = filteredHistory.Max(h => h.MemoryUsageMB),
                        maxResponseTime = filteredHistory.Max(h => h.AverageResponseTimeMs),
                        maxRequestsPerSecond = filteredHistory.Max(h => h.RequestsPerSecond)
                    },
                    trends = new
                    {
                        cpuTrend = CalculateTrend(filteredHistory.Select(h => h.CpuUsagePercent)),
                        memoryTrend = CalculateTrend(filteredHistory.Select(h => (double)h.MemoryUsageMB)),
                        responseTimeTrend = CalculateTrend(filteredHistory.Select(h => h.AverageResponseTimeMs))
                    }
                };

                return Ok(new { success = true, data = trends });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取性能趋势失败");
                return StatusCode(500, new { error = "获取性能趋势失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 计算趋势（简单线性回归斜率）
        /// </summary>
        private string CalculateTrend(IEnumerable<double> values)
        {
            var valuesList = values.ToList();
            if (valuesList.Count < 2) return "Stable";

            var x = Enumerable.Range(0, valuesList.Count).Select(i => (double)i).ToArray();
            var y = valuesList.ToArray();

            var n = x.Length;
            var sumX = x.Sum();
            var sumY = y.Sum();
            var sumXY = x.Zip(y, (xi, yi) => xi * yi).Sum();
            var sumX2 = x.Select(xi => xi * xi).Sum();

            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);

            if (Math.Abs(slope) < 0.1) return "Stable";
            return slope > 0 ? "Increasing" : "Decreasing";
        }
    }
} 