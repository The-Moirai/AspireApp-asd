namespace WebApplication_Drone.Services.Models
{
    /// <summary>
    /// 无人机服务配置选项
    /// </summary>
    public class DroneServiceOptions
    {
        /// <summary>缓存过期时间(分钟)</summary>
        public int CacheExpirationMinutes { get; set; } = 10;
        
        /// <summary>最大并发操作数</summary>
        public int MaxConcurrentOperations { get; set; } = 10;
        
        /// <summary>启用实时更新</summary>
        public bool EnableRealTimeUpdates { get; set; } = true;
        
        /// <summary>批处理大小</summary>
        public int BatchSize { get; set; } = 50;
        
        /// <summary>启用性能监控</summary>
        public bool EnablePerformanceMonitoring { get; set; } = true;
        
        /// <summary>数据同步间隔(秒)</summary>
        public int DataSyncIntervalSeconds { get; set; } = 30;
        
        /// <summary>启用自动重连</summary>
        public bool EnableAutoReconnect { get; set; } = true;
        
        /// <summary>重连最大尝试次数</summary>
        public int MaxReconnectAttempts { get; set; } = 5;
        
        /// <summary>重连间隔(秒)</summary>
        public int ReconnectIntervalSeconds { get; set; } = 10;
        
        /// <summary>启用健康检查</summary>
        public bool EnableHealthCheck { get; set; } = true;
        
        /// <summary>健康检查间隔(秒)</summary>
        public int HealthCheckIntervalSeconds { get; set; } = 60;
    }
} 