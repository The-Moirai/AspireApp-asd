using System.Diagnostics;
using System.Runtime;
using Microsoft.Extensions.Options;
using WebApplication_Drone.Services.Models;
using WebApplication_Drone.Services.Clean;

namespace WebApplication_Drone.Services
{
    /// <summary>
    /// 性能指标数据
    /// </summary>
    public class PerformanceMetrics
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public double CpuUsagePercent { get; set; }
        public long MemoryUsageMB { get; set; }
        public long AvailableMemoryMB { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public long Gen0Collections { get; set; }
        public long Gen1Collections { get; set; }
        public long Gen2Collections { get; set; }
        public long TotalAllocatedBytes { get; set; }
        public int ActiveConnections { get; set; }
        public int TotalDrones { get; set; }
        public int TotalTasks { get; set; }
        public double RequestsPerSecond { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public long TotalRequests { get; set; }
        public long TotalExceptions { get; set; }
    }

    /// <summary>
    /// 性能警告信息
    /// </summary>
    public class PerformanceAlert
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string AlertType { get; set; } = "";
        public string Message { get; set; } = "";
        public string Severity { get; set; } = ""; // Info, Warning, Critical
        public Dictionary<string, object> Metrics { get; set; } = new();
    }

    /// <summary>
    /// 性能监控服务
    /// </summary>
    public class PerformanceMonitoringService : BackgroundService
    {
        private readonly ILogger<PerformanceMonitoringService> _logger;
        private readonly PerformanceMonitoringOptions _options;
        private readonly DroneService _droneService;
        private readonly TaskService _taskService;
        private readonly IServiceProvider _serviceProvider;

        // 性能计数器
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memoryCounter;
        private readonly Process _currentProcess;

        // 历史数据存储
        private readonly Queue<PerformanceMetrics> _metricsHistory = new();
        private readonly Queue<PerformanceAlert> _alertHistory = new();
        private readonly object _historyLock = new();

        // 请求统计
        private long _totalRequests = 0;
        private long _totalResponseTime = 0;
        private long _totalExceptions = 0;
        private DateTime _lastResetTime = DateTime.UtcNow;

        public PerformanceMonitoringService(
            ILogger<PerformanceMonitoringService> logger,
            IOptions<PerformanceMonitoringOptions> options,
            DroneService droneService,
            TaskService taskService,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _options = options.Value;
            _droneService = droneService;
            _taskService = taskService;
            _serviceProvider = serviceProvider;

            _currentProcess = Process.GetCurrentProcess();
            
            // 初始化性能计数器
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
                
                // 预热CPU计数器
                _cpuCounter.NextValue();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "无法初始化性能计数器，将使用替代方法");
                _cpuCounter = null!;
                _memoryCounter = null!;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("性能监控已禁用");
                return;
            }

            _logger.LogInformation("性能监控服务已启动，收集间隔: {Interval}秒", _options.CollectionIntervalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CollectMetricsAsync();
                    await Task.Delay(TimeSpan.FromSeconds(_options.CollectionIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "收集性能指标时发生错误");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }

            _logger.LogInformation("性能监控服务已停止");
        }

        /// <summary>
        /// 收集性能指标
        /// </summary>
        private async Task CollectMetricsAsync()
        {
            try
            {
                var metrics = new PerformanceMetrics();

                // 收集系统指标
                await CollectSystemMetricsAsync(metrics);

                // 收集GC指标
                    CollectGCMetrics(metrics);

                // 收集业务指标
                    await CollectBusinessMetricsAsync(metrics);

                // 收集请求统计
                CollectRequestMetrics(metrics);

                // 存储历史数据
                StoreMetricsHistory(metrics);

                // 检查性能警告
                if (_options.EnableAlerts)
                {
                    await CheckPerformanceAlertsAsync(metrics);
                }

                _logger.LogDebug("性能指标收集完成 - CPU: {Cpu}%, 内存: {Memory}MB, 线程: {Threads}", 
                    metrics.CpuUsagePercent.ToString("F1"), 
                    metrics.MemoryUsageMB, 
                    metrics.ThreadCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "收集性能指标失败");
            }
        }

        /// <summary>
        /// 收集系统指标
        /// </summary>
        private async Task CollectSystemMetricsAsync(PerformanceMetrics metrics)
        {
            try
            {
                // CPU使用率
                if (_cpuCounter != null)
                {
                    metrics.CpuUsagePercent = _cpuCounter.NextValue();
                }

                // 内存使用情况
                _currentProcess.Refresh();
                metrics.MemoryUsageMB = _currentProcess.WorkingSet64 / 1024 / 1024;
                
                if (_memoryCounter != null)
                {
                    metrics.AvailableMemoryMB = (long)_memoryCounter.NextValue();
                }

                // 线程和句柄数
                metrics.ThreadCount = _currentProcess.Threads.Count;
                metrics.HandleCount = _currentProcess.HandleCount;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "收集系统指标失败");
            }
        }

        /// <summary>
        /// 收集GC指标
        /// </summary>
        private void CollectGCMetrics(PerformanceMetrics metrics)
        {
            try
            {
                metrics.Gen0Collections = GC.CollectionCount(0);
                metrics.Gen1Collections = GC.CollectionCount(1);
                metrics.Gen2Collections = GC.CollectionCount(2);
                metrics.TotalAllocatedBytes = GC.GetTotalAllocatedBytes();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "收集GC指标失败");
            }
        }

        /// <summary>
        /// 收集业务指标
        /// </summary>
        private async Task CollectBusinessMetricsAsync(PerformanceMetrics metrics)
        {
            try
            {
                // 无人机统计
                var droneStats = _droneService.GetStatistics();
                metrics.TotalDrones = droneStats.TotalDrones;

                // 任务统计
                var taskStats = _taskService.GetStatistics();
                metrics.TotalTasks = taskStats.TotalTasks;

                // 活跃连接数（如果有MissionSocketService）
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var missionService = scope.ServiceProvider.GetService<MissionSocketService>();
                    if (missionService != null)
                    {
                        // 这里需要MissionSocketService提供GetActiveConnections方法
                        // metrics.ActiveConnections = missionService.GetActiveConnections();
                    }
                }
                catch
                {
                    // 忽略服务获取失败
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "收集业务指标失败");
            }
        }

        /// <summary>
        /// 收集请求统计
        /// </summary>
        private void CollectRequestMetrics(PerformanceMetrics metrics)
        {
            try
            {
                var timeSpan = DateTime.UtcNow - _lastResetTime;
                if (timeSpan.TotalSeconds > 0)
                {
                    metrics.RequestsPerSecond = _totalRequests / timeSpan.TotalSeconds;
                    metrics.AverageResponseTimeMs = _totalRequests > 0 ? 
                        (double)_totalResponseTime / _totalRequests : 0;
                }
                metrics.TotalExceptions = _totalExceptions;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "收集请求统计失败");
            }
        }

        /// <summary>
        /// 存储指标历史
        /// </summary>
        private void StoreMetricsHistory(PerformanceMetrics metrics)
        {
            lock (_historyLock)
            {
                _metricsHistory.Enqueue(metrics);
                
                // 保持历史数据大小限制
                while (_metricsHistory.Count > _options.MaxHistoryPoints)
                {
                    _metricsHistory.Dequeue();
                }
            }
        }

        /// <summary>
        /// 检查性能警告
        /// </summary>
        private async Task CheckPerformanceAlertsAsync(PerformanceMetrics metrics)
        {
            var alerts = new List<PerformanceAlert>();

            // CPU使用率警告
            if (metrics.CpuUsagePercent > _options.CpuWarningThreshold)
            {
                alerts.Add(new PerformanceAlert
                {
                    AlertType = "HighCpuUsage",
                    Message = $"CPU使用率过高: {metrics.CpuUsagePercent:F1}%",
                    Severity = metrics.CpuUsagePercent > 90 ? "Critical" : "Warning",
                    Metrics = new Dictionary<string, object>
                    {
                        ["CpuUsage"] = metrics.CpuUsagePercent,
                        ["Threshold"] = _options.CpuWarningThreshold
                    }
                });
            }

            // 内存使用警告
            if (metrics.MemoryUsageMB > _options.MemoryWarningThresholdMB)
            {
                alerts.Add(new PerformanceAlert
                {
                    AlertType = "HighMemoryUsage",
                    Message = $"内存使用过高: {metrics.MemoryUsageMB}MB",
                    Severity = metrics.MemoryUsageMB > _options.MemoryWarningThresholdMB * 1.5 ? "Critical" : "Warning",
                    Metrics = new Dictionary<string, object>
                    {
                        ["MemoryUsage"] = metrics.MemoryUsageMB,
                        ["Threshold"] = _options.MemoryWarningThresholdMB
                    }
                });
            }

            // 响应时间警告
            if (metrics.AverageResponseTimeMs > _options.ResponseTimeWarningThresholdMs)
            {
                alerts.Add(new PerformanceAlert
                {
                    AlertType = "HighResponseTime",
                    Message = $"响应时间过长: {metrics.AverageResponseTimeMs:F1}ms",
                    Severity = metrics.AverageResponseTimeMs > _options.ResponseTimeWarningThresholdMs * 2 ? "Critical" : "Warning",
                    Metrics = new Dictionary<string, object>
                    {
                        ["ResponseTime"] = metrics.AverageResponseTimeMs,
                        ["Threshold"] = _options.ResponseTimeWarningThresholdMs
                    }
                });
            }

            // 线程数警告
            if (metrics.ThreadCount > 200)
            {
                alerts.Add(new PerformanceAlert
                {
                    AlertType = "HighThreadCount",
                    Message = $"线程数过多: {metrics.ThreadCount}",
                    Severity = metrics.ThreadCount > 500 ? "Critical" : "Warning",
                    Metrics = new Dictionary<string, object>
                    {
                        ["ThreadCount"] = metrics.ThreadCount
                    }
                });
            }

            // 记录警告
            foreach (var alert in alerts)
            {
                var logLevel = alert.Severity switch
                {
                    "Critical" => LogLevel.Critical,
                    "Warning" => LogLevel.Warning,
                    _ => LogLevel.Information
                };

                _logger.Log(logLevel, "性能警告: {AlertType} - {Message}", alert.AlertType, alert.Message);

                // 存储警告历史
                lock (_historyLock)
                {
                    _alertHistory.Enqueue(alert);
                    while (_alertHistory.Count > 100) // 保持最近100个警告
                    {
                        _alertHistory.Dequeue();
                    }
                }
            }
        }

        /// <summary>
        /// 记录请求
        /// </summary>
        public void RecordRequest(long responseTimeMs)
        {
            Interlocked.Increment(ref _totalRequests);
            Interlocked.Add(ref _totalResponseTime, responseTimeMs);
        }

        /// <summary>
        /// 记录异常
        /// </summary>
        public void RecordException()
        {
            Interlocked.Increment(ref _totalExceptions);
        }

        /// <summary>
        /// 获取当前性能指标
        /// </summary>
        public PerformanceMetrics? GetCurrentMetrics()
        {
            lock (_historyLock)
            {
                return _metricsHistory.LastOrDefault();
            }
        }

        /// <summary>
        /// 获取性能历史数据
        /// </summary>
        public List<PerformanceMetrics> GetMetricsHistory(int count = 100)
        {
            lock (_historyLock)
            {
                return _metricsHistory.TakeLast(count).ToList();
            }
        }

        /// <summary>
        /// 获取警告历史
        /// </summary>
        public List<PerformanceAlert> GetAlertHistory(int count = 50)
        {
            lock (_historyLock)
            {
                return _alertHistory.TakeLast(count).ToList();
            }
        }

        /// <summary>
        /// 重置统计
        /// </summary>
        public void ResetStatistics()
        {
            Interlocked.Exchange(ref _totalRequests, 0);
            Interlocked.Exchange(ref _totalResponseTime, 0);
            Interlocked.Exchange(ref _totalExceptions, 0);
            _lastResetTime = DateTime.UtcNow;
        }

        /// <summary>
        /// 强制垃圾回收
        /// </summary>
        public void ForceGarbageCollection()
        {
            _logger.LogInformation("开始强制垃圾回收");
            var before = GC.GetTotalMemory(false);
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var after = GC.GetTotalMemory(false);
            var freed = before - after;
            
            _logger.LogInformation("垃圾回收完成，释放内存: {FreedMB}MB", freed / 1024 / 1024);
        }

        public void RecordRequestStart()
        {
            // 兼容旧接口，无需实现内容
        }

        public void RecordRequestComplete(long responseTimeMs)
        {
            RecordRequest(responseTimeMs);
        }

        public override void Dispose()
        {
            try
            {
                _cpuCounter?.Dispose();
                _memoryCounter?.Dispose();
                _currentProcess?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "释放性能监控资源时发生错误");
            }
            
            base.Dispose();
        }
    }
} 