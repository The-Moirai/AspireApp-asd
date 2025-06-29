namespace WebApplication_Drone.Services.Models
{
    /// <summary>
    /// 数据服务配置选项
    /// </summary>
    public class DataServiceOptions
    {
        /// <summary>最大重试次数</summary>
        public int MaxRetryAttempts { get; set; } = 3;
        
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
        
        /// <summary>数据库连接超时(秒)</summary>
        public int DatabaseTimeoutSeconds { get; set; } = 30;
        
        /// <summary>启用连接池</summary>
        public bool EnableConnectionPooling { get; set; } = true;
        
        /// <summary>连接池最大大小</summary>
        public int MaxPoolSize { get; set; } = 100;
        
        /// <summary>连接池最小大小</summary>
        public int MinPoolSize { get; set; } = 5;
    }
} 