using Microsoft.AspNetCore.Mvc;
using WebApplication_Drone.Services.Clean;
using WebApplication_Drone.Services;
using WebApplication_Drone.Services.Models;
using System.Diagnostics;

namespace WebApplication_Drone.Controllers
{
    /// <summary>
    /// 仪表板控制器 - 提供系统监控仪表板数据
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly DroneService _droneService;
        private readonly TaskService _taskService;
        private readonly PerformanceMonitoringService _performanceService;
        private readonly ILogger<DashboardController> _logger;
        private readonly Process _currentProcess;

        public DashboardController(
            DroneService droneService,
            TaskService taskService,
            PerformanceMonitoringService performanceService,
            ILogger<DashboardController> logger)
        {
            _droneService = droneService;
            _taskService = taskService;
            _performanceService = performanceService;
            _logger = logger;
            _currentProcess = Process.GetCurrentProcess();
        }

        /// <summary>
        /// 获取仪表板概览数据
        /// </summary>
        [HttpGet("overview")]
        public IActionResult GetDashboardOverview()
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
                        status = "Healthy",
                        uptime = DateTime.Now - _currentProcess.StartTime,
                        cpuUsage = performanceMetrics?.CpuUsagePercent ?? 0,
                        memoryUsage = performanceMetrics?.MemoryUsageMB ?? 0,
                        totalMemory = performanceMetrics?.AvailableMemoryMB ?? 0,
                        threadCount = performanceMetrics?.ThreadCount ?? 0
                    },
                    services = new
                    {
                        drone = new
                        {
                            total = droneStats.TotalDrones,
                            online = droneStats.OnlineDrones,
                            offline = droneStats.TotalDrones - droneStats.OnlineDrones,
                            operations = droneStats.TotalOperations,
                            cacheHitRate = droneStats.CacheHitRate,
                            responseTime = droneStats.AverageResponseTimeMs
                        },
                        task = new
                        {
                            total = taskStats.TotalTasks,
                            active = taskStats.ActiveTasks,
                            completed = taskStats.TotalTasks - taskStats.ActiveTasks,
                            operations = taskStats.TotalOperations,
                            cacheHitRate = taskStats.CacheHitRate,
                            responseTime = taskStats.AverageResponseTimeMs
                        }
                    },
                    performance = new
                    {
                        requestsPerSecond = performanceMetrics?.RequestsPerSecond ?? 0,
                        averageResponseTime = performanceMetrics?.AverageResponseTimeMs ?? 0,
                        totalExceptions = performanceMetrics?.TotalExceptions ?? 0,
                        activeConnections = performanceMetrics?.ActiveConnections ?? 0
                    },
                    alerts = new
                    {
                        count = 0, // 可以从性能服务获取警告数量
                        critical = 0,
                        warning = 0,
                        info = 0
                    }
                };

                return Ok(new { success = true, data = overview });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取仪表板概览失败");
                return StatusCode(500, new { error = "获取仪表板概览失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取实时性能图表数据
        /// </summary>
        [HttpGet("charts/performance")]
        public IActionResult GetPerformanceCharts([FromQuery] int hours = 6)
        {
            try
            {
                var history = _performanceService.GetMetricsHistory(288); // 24小时的数据点
                var cutoffTime = DateTime.UtcNow.AddHours(-hours);
                
                var filteredHistory = history.Where(h => h.Timestamp >= cutoffTime).ToList();
                
                var chartData = new
                {
                    labels = filteredHistory.Select(h => h.Timestamp.ToString("HH:mm")).ToArray(),
                    datasets = new object[]
                    {
                        new
                        {
                            label = "CPU使用率 (%)",
                            data = filteredHistory.Select(h => h.CpuUsagePercent).ToArray(),
                            borderColor = "#FF6384",
                            backgroundColor = "rgba(255, 99, 132, 0.1)",
                            tension = 0.4
                        },
                        new
                        {
                            label = "内存使用 (MB)",
                            data = filteredHistory.Select(h => (double)h.MemoryUsageMB).ToArray(),
                            borderColor = "#36A2EB",
                            backgroundColor = "rgba(54, 162, 235, 0.1)",
                            tension = 0.4
                        },
                        new
                        {
                            label = "响应时间 (ms)",
                            data = filteredHistory.Select(h => h.AverageResponseTimeMs).ToArray(),
                            borderColor = "#FFCE56",
                            backgroundColor = "rgba(255, 206, 86, 0.1)",
                            tension = 0.4
                        }
                    }
                };

                return Ok(new { success = true, data = chartData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取性能图表数据失败");
                return StatusCode(500, new { error = "获取性能图表数据失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取服务状态图表数据
        /// </summary>
        [HttpGet("charts/services")]
        public IActionResult GetServicesCharts()
        {
            try
            {
                var droneStats = _droneService.GetStatistics();
                var taskStats = _taskService.GetStatistics();

                var serviceData = new
                {
                    drone = new
                    {
                        labels = new string[] { "在线", "离线" },
                        data = new int[] { droneStats.OnlineDrones, droneStats.TotalDrones - droneStats.OnlineDrones },
                        backgroundColor = new string[] { "#4BC0C0", "#FF6384" }
                    },
                    task = new
                    {
                        labels = new string[] { "活跃", "已完成" },
                        data = new int[] { taskStats.ActiveTasks, taskStats.TotalTasks - taskStats.ActiveTasks },
                        backgroundColor = new string[] { "#36A2EB", "#FFCE56" }
                    },
                    cache = new
                    {
                        labels = new string[] { "命中", "未命中" },
                        data = new long[] 
                        { 
                            droneStats.CacheHits + taskStats.CacheHits,
                            droneStats.CacheMisses + taskStats.CacheMisses
                        },
                        backgroundColor = new string[] { "#4BC0C0", "#FF6384" }
                    }
                };

                return Ok(new { success = true, data = serviceData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取服务图表数据失败");
                return StatusCode(500, new { error = "获取服务图表数据失败", message = ex.Message });
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
                        workingSet = _currentProcess.WorkingSet64 / 1024 / 1024,
                        privateMemory = _currentProcess.PrivateMemorySize64 / 1024 / 1024,
                        virtualMemory = _currentProcess.VirtualMemorySize64 / 1024 / 1024,
                        totalAllocated = GC.GetTotalMemory(false) / 1024 / 1024,
                        gcCollections = new
                        {
                            gen0 = GC.CollectionCount(0),
                            gen1 = GC.CollectionCount(1),
                            gen2 = GC.CollectionCount(2)
                        }
                    },
                    cpu = new
                    {
                        totalProcessorTime = _currentProcess.TotalProcessorTime.TotalMilliseconds,
                        userProcessorTime = _currentProcess.UserProcessorTime.TotalMilliseconds,
                        privilegedProcessorTime = _currentProcess.PrivilegedProcessorTime.TotalMilliseconds
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
        /// 获取性能指标摘要
        /// </summary>
        [HttpGet("metrics/summary")]
        public IActionResult GetMetricsSummary()
        {
            try
            {
                var droneStats = _droneService.GetStatistics();
                var taskStats = _taskService.GetStatistics();
                var performanceMetrics = _performanceService.GetCurrentMetrics();

                var summary = new
                {
                    timestamp = DateTime.UtcNow,
                    keyMetrics = new
                    {
                        totalRequests = performanceMetrics?.TotalRequests ?? 0,
                        requestsPerSecond = performanceMetrics?.RequestsPerSecond ?? 0,
                        averageResponseTime = performanceMetrics?.AverageResponseTimeMs ?? 0,
                        errorRate = performanceMetrics?.TotalRequests > 0 
                            ? (double)(performanceMetrics?.TotalExceptions ?? 0) / performanceMetrics.TotalRequests * 100 
                            : 0,
                        cacheHitRate = (droneStats.CacheHitRate + taskStats.CacheHitRate) / 2
                    },
                    systemHealth = new
                    {
                        cpuUsage = performanceMetrics?.CpuUsagePercent ?? 0,
                        memoryUsage = performanceMetrics?.MemoryUsageMB ?? 0,
                        threadCount = performanceMetrics?.ThreadCount ?? 0,
                        activeConnections = performanceMetrics?.ActiveConnections ?? 0
                    },
                    serviceHealth = new
                    {
                        droneService = droneStats.DatabaseConnected ? "Healthy" : "Unhealthy",
                        taskService = taskStats.DatabaseConnected ? "Healthy" : "Unhealthy",
                        performanceService = "Healthy"
                    }
                };

                return Ok(new { success = true, data = summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取指标摘要失败");
                return StatusCode(500, new { error = "获取指标摘要失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取性能警告列表
        /// </summary>
        [HttpGet("alerts")]
        public IActionResult GetPerformanceAlerts([FromQuery] int count = 20)
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
        /// 获取系统状态快照
        /// </summary>
        [HttpGet("snapshot")]
        public IActionResult GetSystemSnapshot()
        {
            try
            {
                var droneStats = _droneService.GetStatistics();
                var taskStats = _taskService.GetStatistics();
                var performanceMetrics = _performanceService.GetCurrentMetrics();

                var snapshot = new
                {
                    timestamp = DateTime.UtcNow,
                    system = new
                    {
                        status = "Running",
                        uptime = DateTime.Now - _currentProcess.StartTime,
                        version = typeof(DashboardController).Assembly.GetName().Version?.ToString(),
                        environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
                    },
                    performance = new
                    {
                        cpuUsage = performanceMetrics?.CpuUsagePercent ?? 0,
                        memoryUsage = performanceMetrics?.MemoryUsageMB ?? 0,
                        requestsPerSecond = performanceMetrics?.RequestsPerSecond ?? 0,
                        averageResponseTime = performanceMetrics?.AverageResponseTimeMs ?? 0,
                        totalRequests = performanceMetrics?.TotalRequests ?? 0,
                        totalExceptions = performanceMetrics?.TotalExceptions ?? 0
                    },
                    services = new
                    {
                        drone = new
                        {
                            total = droneStats.TotalDrones,
                            online = droneStats.OnlineDrones,
                            operations = droneStats.TotalOperations,
                            cacheHitRate = droneStats.CacheHitRate
                        },
                        task = new
                        {
                            total = taskStats.TotalTasks,
                            active = taskStats.ActiveTasks,
                            operations = taskStats.TotalOperations,
                            cacheHitRate = taskStats.CacheHitRate
                        }
                    },
                    health = new
                    {
                        overall = "Healthy",
                        database = droneStats.DatabaseConnected && taskStats.DatabaseConnected ? "Connected" : "Disconnected",
                        cache = "Available",
                        memory = (performanceMetrics?.MemoryUsageMB ?? 0) < 1024 ? "Normal" : "High",
                        cpu = (performanceMetrics?.CpuUsagePercent ?? 0) < 80 ? "Normal" : "High"
                    }
                };

                return Ok(new { success = true, data = snapshot });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取系统快照失败");
                return StatusCode(500, new { error = "获取系统快照失败", message = ex.Message });
            }
        }
    }
} 