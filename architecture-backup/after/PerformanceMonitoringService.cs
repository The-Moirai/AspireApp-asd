using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ClassLibrary_Core.Services;

namespace WebApplication_Drone.Services
{
    /// <summary>
    /// 性能监控服务
    /// </summary>
    public class PerformanceMonitoringService : BackgroundService
    {
        private readonly ILogger<PerformanceMonitoringService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly Meter _meter;
        private readonly Counter<long> _droneOperationCounter;
        private readonly Counter<long> _taskOperationCounter;
        private readonly Histogram<double> _databaseOperationDuration;
        private readonly Gauge<long> _activeDroneCount;
        private readonly Gauge<long> _activeTaskCount;
        private readonly Timer _monitoringTimer;

        public PerformanceMonitoringService(
            ILogger<PerformanceMonitoringService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            
            // 创建指标收集器
            _meter = new Meter("AspireApp.DroneService", "1.0.0");
            
            _droneOperationCounter = _meter.CreateCounter<long>(
                "aspireapp_drone_operations_total",
                description: "无人机操作总数");
                
            _taskOperationCounter = _meter.CreateCounter<long>(
                "aspireapp_task_operations_total", 
                description: "任务操作总数");
                
            _databaseOperationDuration = _meter.CreateHistogram<double>(
                "aspireapp_database_operation_duration_seconds",
                "seconds",
                "数据库操作持续时间");
                
            _activeDroneCount = _meter.CreateGauge<long>(
                "aspireapp_active_drones",
                description: "活跃无人机数量");
                
            _activeTaskCount = _meter.CreateGauge<long>(
                "aspireapp_active_tasks",
                description: "活跃任务数量");

            // 每30秒收集一次指标
            _monitoringTimer = new Timer(CollectMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("性能监控服务已启动");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CollectDetailedMetricsAsync();
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // 每5分钟收集详细指标
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "收集详细指标时发生错误");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
            
            _logger.LogInformation("性能监控服务已停止");
        }

        /// <summary>
        /// 收集基本指标
        /// </summary>
        private async void CollectMetrics(object? state)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var droneService = scope.ServiceProvider.GetService<IDroneDataService>();
                var taskService = scope.ServiceProvider.GetService<ITaskDataService>();

                if (droneService != null)
                {
                    var drones = await droneService.GetAllDronesAsync();
                    var activeDrones = drones.Count(d => d.Status != ClassLibrary_Core.Drone.DroneStatus.Offline);
                    _activeDroneCount.Record(activeDrones);
                }

                if (taskService != null)
                {
                    var tasks = await taskService.GetAllTasksAsync();
                    var activeTasks = tasks.Count(t => t.Status == System.Threading.Tasks.TaskStatus.Running || 
                                                      t.Status == System.Threading.Tasks.TaskStatus.WaitingToRun);
                    _activeTaskCount.Record(activeTasks);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "收集基本指标时发生错误");
            }
        }

        /// <summary>
        /// 收集详细指标
        /// </summary>
        private async Task CollectDetailedMetricsAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var databaseService = scope.ServiceProvider.GetService<IDatabaseService>();
                
                if (databaseService != null)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var testResult = await databaseService.TestConnectionAsync();
                    stopwatch.Stop();
                    
                    _databaseOperationDuration.Record(stopwatch.Elapsed.TotalSeconds, 
                        new KeyValuePair<string, object?>("operation", "health_check"));
                    
                    if (!testResult)
                    {
                        _logger.LogWarning("数据库健康检查失败");
                    }

                    // 收集数据库统计信息
                    try
                    {
                        stopwatch.Restart();
                        var stats = await databaseService.GetDatabaseStatisticsAsync();
                        stopwatch.Stop();
                        
                        _databaseOperationDuration.Record(stopwatch.Elapsed.TotalSeconds,
                            new KeyValuePair<string, object?>("operation", "get_statistics"));
                        
                        _logger.LogInformation("数据库统计: 无人机数量={DroneCount}, 任务数量={TaskCount}, 数据库大小={DatabaseSizeMB}MB",
                            stats.DroneCount, stats.TaskCount, stats.DatabaseSizeBytes / 1024 / 1024);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "获取数据库统计信息失败");
                    }
                }

                // 收集系统资源使用情况
                CollectSystemMetrics();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "收集详细指标时发生错误");
            }
        }

        /// <summary>
        /// 收集系统资源指标
        /// </summary>
        private void CollectSystemMetrics()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                
                // 内存使用情况
                var memoryUsageMB = process.WorkingSet64 / 1024 / 1024;
                _logger.LogDebug("当前内存使用: {MemoryUsageMB}MB", memoryUsageMB);
                
                // CPU使用情况（简化版）
                var cpuTime = process.TotalProcessorTime;
                _logger.LogDebug("总CPU时间: {CpuTime}", cpuTime);
                
                // GC信息
                var gen0Collections = GC.CollectionCount(0);
                var gen1Collections = GC.CollectionCount(1);
                var gen2Collections = GC.CollectionCount(2);
                var totalMemory = GC.GetTotalMemory(false) / 1024 / 1024;
                
                _logger.LogDebug("GC信息: Gen0={Gen0}, Gen1={Gen1}, Gen2={Gen2}, 托管内存={TotalMemoryMB}MB",
                    gen0Collections, gen1Collections, gen2Collections, totalMemory);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "收集系统指标时发生错误");
            }
        }

        /// <summary>
        /// 记录无人机操作指标
        /// </summary>
        public void RecordDroneOperation(string operation, bool success)
        {
            _droneOperationCounter.Add(1, 
                new KeyValuePair<string, object?>("operation", operation),
                new KeyValuePair<string, object?>("success", success));
        }

        /// <summary>
        /// 记录任务操作指标
        /// </summary>
        public void RecordTaskOperation(string operation, bool success)
        {
            _taskOperationCounter.Add(1,
                new KeyValuePair<string, object?>("operation", operation),
                new KeyValuePair<string, object?>("success", success));
        }

        /// <summary>
        /// 记录数据库操作持续时间
        /// </summary>
        public void RecordDatabaseOperation(string operation, TimeSpan duration)
        {
            _databaseOperationDuration.Record(duration.TotalSeconds,
                new KeyValuePair<string, object?>("operation", operation));
        }

        public override void Dispose()
        {
            _monitoringTimer?.Dispose();
            _meter?.Dispose();
            base.Dispose();
        }
    }

    /// <summary>
    /// 性能监控扩展方法
    /// </summary>
    public static class PerformanceMonitoringExtensions
    {
        /// <summary>
        /// 添加性能监控服务
        /// </summary>
        public static IServiceCollection AddPerformanceMonitoring(this IServiceCollection services)
        {
            services.AddSingleton<PerformanceMonitoringService>();
            services.AddHostedService<PerformanceMonitoringService>(provider => 
                provider.GetRequiredService<PerformanceMonitoringService>());
            
            return services;
        }
    }
} 