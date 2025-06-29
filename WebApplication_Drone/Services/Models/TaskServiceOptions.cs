namespace WebApplication_Drone.Services.Models
{
    /// <summary>
    /// 任务服务配置选项
    /// </summary>
    public class TaskServiceOptions
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
        
        /// <summary>任务超时时间(分钟)</summary>
        public int TaskTimeoutMinutes { get; set; } = 30;
        
        /// <summary>子任务超时时间(分钟)</summary>
        public int SubTaskTimeoutMinutes { get; set; } = 15;
        
        /// <summary>启用任务重试</summary>
        public bool EnableTaskRetry { get; set; } = true;
        
        /// <summary>最大重试次数</summary>
        public int MaxRetryAttempts { get; set; } = 3;
        
        /// <summary>重试间隔(秒)</summary>
        public int RetryIntervalSeconds { get; set; } = 30;
        
        /// <summary>启用任务优先级</summary>
        public bool EnableTaskPriority { get; set; } = true;
        
        /// <summary>启用任务依赖</summary>
        public bool EnableTaskDependency { get; set; } = true;
        
        /// <summary>启用任务调度</summary>
        public bool EnableTaskScheduling { get; set; } = true;
        
        /// <summary>调度间隔(秒)</summary>
        public int SchedulingIntervalSeconds { get; set; } = 60;
    }
} 