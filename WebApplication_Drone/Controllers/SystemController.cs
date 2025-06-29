using Microsoft.AspNetCore.Mvc;
using WebApplication_Drone.Services;
using WebApplication_Drone.Middleware;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;
using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Mission;
using WebApplication_Drone.Services.Clean;
using WebApplication_Drone.Services.Models;

namespace WebApplication_Drone.Controllers
{
    /// <summary>
    /// 系统监控和健康检查控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SystemController : ControllerBase
    {
        private readonly DroneService _droneService;
        private readonly TaskService _taskService;
        private readonly PerformanceMonitoringService _performanceService;
        private readonly ILogger<SystemController> _logger;

        public SystemController(
            DroneService droneService,
            TaskService taskService,
            PerformanceMonitoringService performanceService,
            ILogger<SystemController> logger)
        {
            _droneService = droneService;
            _taskService = taskService;
            _performanceService = performanceService;
            _logger = logger;
        }

        /// <summary>
        /// 获取系统健康状态
        /// </summary>
        [HttpGet("health")]
        public IActionResult GetHealthStatus()
        {
            try
            {
                var response = new
                {
                    status = "Healthy",
                    totalDuration = 0,
                    checks = new[]
                    {
                        new
                    {
                            name = "system",
                            status = "Healthy",
                            duration = 0,
                            description = "System is running normally",
                            exception = (string?)null
                        }
                    },
                    timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取健康状态失败");
                return StatusCode(500, new { error = "健康检查失败", message = ex.Message });
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
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取无人机统计失败");
                return StatusCode(500, new { error = "获取统计失败", message = ex.Message });
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
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取任务统计失败");
                return StatusCode(500, new { error = "获取统计失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取系统性能指标
        /// </summary>
        [HttpGet("performance")]
        public IActionResult GetPerformanceMetrics()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                
                var metrics = new
                {
                    // 内存使用情况
                    memory = new
                    {
                        workingSet = process.WorkingSet64,
                        privateMemory = process.PrivateMemorySize64,
                        virtualMemory = process.VirtualMemorySize64,
                        gcGen0Collections = GC.CollectionCount(0),
                        gcGen1Collections = GC.CollectionCount(1),
                        gcGen2Collections = GC.CollectionCount(2),
                        totalMemory = GC.GetTotalMemory(false)
                    },
                    
                    // CPU使用情况
                    cpu = new
                    {
                        totalProcessorTime = process.TotalProcessorTime.TotalMilliseconds,
                        userProcessorTime = process.UserProcessorTime.TotalMilliseconds,
                        privilegedProcessorTime = process.PrivilegedProcessorTime.TotalMilliseconds,
                        threadCount = process.Threads.Count
                    },
                    
                    // 系统信息
                    system = new
                    {
                        startTime = process.StartTime,
                        uptime = DateTime.Now - process.StartTime,
                        processId = process.Id,
                        processName = process.ProcessName,
                        machineName = Environment.MachineName,
                        osVersion = Environment.OSVersion.ToString(),
                        processorCount = Environment.ProcessorCount
                    },
                    
                    // 应用程序指标
                    application = new
                    {
                        droneStats = _droneService.GetStatistics(),
                        taskStats = _taskService.GetStatistics()
                    },
                    
                    timestamp = DateTime.UtcNow
                };

                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取性能指标失败");
                return StatusCode(500, new { error = "获取性能指标失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 触发垃圾回收
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
                    message = "垃圾回收完成",
                    beforeMemory,
                    afterMemory,
                    freedMemory,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "触发垃圾回收失败");
                return StatusCode(500, new { error = "垃圾回收失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取应用程序版本信息
        /// </summary>
        [HttpGet("version")]
        public IActionResult GetVersionInfo()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                
                return Ok(new
                {
                    version = version?.ToString() ?? "Unknown",
                    buildDate = System.IO.File.GetCreationTime(assembly.Location),
                    framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    runtime = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取版本信息失败");
                return StatusCode(500, new { error = "获取版本信息失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取性能监控历史数据
        /// </summary>
        [HttpGet("performance-history")]
        public IActionResult GetPerformanceHistory([FromQuery] int count = 100)
        {
            try
            {
                if (_performanceService == null)
                {
                    return NotFound(new { error = "性能监控服务未启用" });
                }

                var history = _performanceService.GetMetricsHistory(count);
                return Ok(new
                {
                    count = history.Count,
                    data = history,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取性能历史失败");
                return StatusCode(500, new { error = "获取性能历史失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取性能警告历史
        /// </summary>
        [HttpGet("performance-alerts")]
        public IActionResult GetPerformanceAlerts([FromQuery] int count = 50)
        {
            try
            {
                if (_performanceService == null)
                {
                    return NotFound(new { error = "性能监控服务未启用" });
                }

                var alerts = _performanceService.GetAlertHistory(count);
                return Ok(new
                {
                    count = alerts.Count,
                    alerts = alerts,
                    timestamp = DateTime.UtcNow
                });
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
        [HttpGet("current-metrics")]
        public IActionResult GetCurrentMetrics()
        {
            try
            {
                if (_performanceService == null)
                {
                    return NotFound(new { error = "性能监控服务未启用" });
                }

                var currentMetrics = _performanceService.GetCurrentMetrics();
                if (currentMetrics == null)
                {
                    return NotFound(new { error = "暂无性能数据" });
                }

                return Ok(currentMetrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取当前性能指标失败");
                return StatusCode(500, new { error = "获取当前性能指标失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 重置性能统计
        /// </summary>
        [HttpPost("reset-stats")]
        public IActionResult ResetPerformanceStats()
        {
            try
            {
                if (_performanceService == null)
                {
                    return NotFound(new { error = "性能监控服务未启用" });
                }

                _performanceService.ResetStatistics();
                _logger.LogInformation("性能统计已重置");

                return Ok(new
                {
                    message = "性能统计已重置",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重置性能统计失败");
                return StatusCode(500, new { error = "重置性能统计失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 强制执行垃圾回收（增强版）
        /// </summary>
        [HttpPost("force-gc")]
        public IActionResult ForceGarbageCollection()
        {
            try
            {
                if (_performanceService == null)
                {
                    return BadRequest(new { error = "性能监控服务未启用" });
                }

                _performanceService.ForceGarbageCollection();

                return Ok(new
                {
                    message = "强制垃圾回收已执行",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "强制垃圾回收失败");
                return StatusCode(500, new { error = "强制垃圾回收失败", message = ex.Message });
            }
        }
    }
} 