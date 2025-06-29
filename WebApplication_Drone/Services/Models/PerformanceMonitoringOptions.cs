namespace WebApplication_Drone.Services.Models
{
    /// <summary>
    /// 性能监控配置选项
    /// </summary>
    public class PerformanceMonitoringOptions
    {
        /// <summary>
        /// 是否启用性能监控
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 性能数据收集间隔（秒）
        /// </summary>
        public int CollectionIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// 历史数据保留时间（小时）
        /// </summary>
        public int HistoryRetentionHours { get; set; } = 24;

        /// <summary>
        /// 最大历史数据点数量
        /// </summary>
        public int MaxHistoryPoints { get; set; } = 2880; // 24小时 * 120个数据点/小时

        /// <summary>
        /// CPU使用率警告阈值（百分比）
        /// </summary>
        public double CpuWarningThreshold { get; set; } = 80.0;

        /// <summary>
        /// 内存使用率警告阈值（MB）
        /// </summary>
        public double MemoryWarningThresholdMB { get; set; } = 1024.0;

        /// <summary>
        /// 响应时间警告阈值（毫秒）
        /// </summary>
        public double ResponseTimeWarningThresholdMs { get; set; } = 1000.0;

        /// <summary>
        /// 是否启用详细日志记录
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = false;

        /// <summary>
        /// 是否启用性能警告
        /// </summary>
        public bool EnableAlerts { get; set; } = true;

        /// <summary>
        /// 性能警告检查间隔（秒）
        /// </summary>
        public int AlertCheckIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// 缓存统计收集间隔（秒）
        /// </summary>
        public int CacheStatsIntervalSeconds { get; set; } = 10;

        /// <summary>
        /// 是否启用请求追踪
        /// </summary>
        public bool EnableRequestTracing { get; set; } = true;

        /// <summary>
        /// 请求追踪采样率（0.0-1.0）
        /// </summary>
        public double RequestTracingSampleRate { get; set; } = 0.1;

        /// <summary>
        /// 慢查询阈值（毫秒）
        /// </summary>
        public double SlowQueryThresholdMs { get; set; } = 500.0;

        /// <summary>
        /// 是否启用数据库性能监控
        /// </summary>
        public bool EnableDatabaseMonitoring { get; set; } = true;

        /// <summary>
        /// 数据库查询超时阈值（毫秒）
        /// </summary>
        public double DatabaseTimeoutThresholdMs { get; set; } = 5000.0;
    }
} 