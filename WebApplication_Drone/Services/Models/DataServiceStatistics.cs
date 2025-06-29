namespace WebApplication_Drone.Services.Models
{
    /// <summary>
    /// 数据服务统计信息
    /// </summary>
    public class DataServiceStatistics
    {
        /// <summary>总无人机数量</summary>
        public int TotalDrones { get; set; }
        
        /// <summary>在线无人机数量</summary>
        public int OnlineDrones { get; set; }
        
        /// <summary>总任务数量</summary>
        public int TotalTasks { get; set; }
        
        /// <summary>活跃任务数量</summary>
        public int ActiveTasks { get; set; }
        
        /// <summary>总操作次数</summary>
        public long TotalOperations { get; set; }
        
        /// <summary>缓存命中次数</summary>
        public long CacheHits { get; set; }
        
        /// <summary>缓存未命中次数</summary>
        public long CacheMisses { get; set; }
        
        /// <summary>缓存命中率</summary>
        public double CacheHitRate => TotalOperations > 0 ? (double)CacheHits / TotalOperations * 100 : 0;
        
        /// <summary>平均响应时间(毫秒)</summary>
        public double AverageResponseTimeMs { get; set; }
        
        /// <summary>最后更新时间</summary>
        public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;
        
        /// <summary>数据库连接状态</summary>
        public bool DatabaseConnected { get; set; }
        
        /// <summary>内存使用量(MB)</summary>
        public long MemoryUsageMB { get; set; }
        
        /// <summary>活跃连接数</summary>
        public int ActiveConnections { get; set; }
    }
} 