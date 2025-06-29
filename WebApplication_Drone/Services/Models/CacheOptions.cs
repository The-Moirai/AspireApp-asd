using System.ComponentModel.DataAnnotations;

namespace WebApplication_Drone.Services.Models
{
    /// <summary>
    /// Redis缓存配置选项
    /// </summary>
    public class RedisCacheOptions
    {
        /// <summary>
        /// 实例名称前缀
        /// </summary>
        public string InstanceName { get; set; } = "AspireApp_";

        /// <summary>
        /// 默认过期时间（分钟）
        /// </summary>
        [Range(1, 1440)]
        public int DefaultExpirationMinutes { get; set; } = 30;

        /// <summary>
        /// 短期缓存过期时间（分钟）
        /// </summary>
        [Range(1, 60)]
        public int ShortTermExpirationMinutes { get; set; } = 5;

        /// <summary>
        /// 长期缓存过期时间（分钟）
        /// </summary>
        [Range(10, 1440)]
        public int LongTermExpirationMinutes { get; set; } = 120;

        /// <summary>
        /// 启用压缩
        /// </summary>
        public bool EnableCompression { get; set; } = true;

        /// <summary>
        /// 启用序列化优化
        /// </summary>
        public bool EnableSerializationOptimization { get; set; } = true;
    }

    /// <summary>
    /// 内存缓存配置选项
    /// </summary>
    public class MemoryCacheOptions
    {
        /// <summary>
        /// 缓存大小限制
        /// </summary>
        [Range(100, 10000)]
        public int SizeLimit { get; set; } = 1000;

        /// <summary>
        /// 压缩百分比
        /// </summary>
        [Range(0.01, 0.5)]
        public double CompactionPercentage { get; set; } = 0.1;

        /// <summary>
        /// 过期扫描频率
        /// </summary>
        public TimeSpan ExpirationScanFrequency { get; set; } = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// 缓存策略配置选项
    /// </summary>
    public class CacheStrategyOptions
    {
        /// <summary>
        /// 启用分层缓存
        /// </summary>
        public bool EnableTieredCaching { get; set; } = true;

        /// <summary>
        /// 启用缓存预热
        /// </summary>
        public bool EnableCacheWarming { get; set; } = true;

        /// <summary>
        /// 启用缓存失效
        /// </summary>
        public bool EnableCacheInvalidation { get; set; } = true;

        /// <summary>
        /// 启用缓存统计
        /// </summary>
        public bool EnableCacheStatistics { get; set; } = true;

        /// <summary>
        /// 实时数据优先级
        /// </summary>
        public CachePriority RealTimeDataPriority { get; set; } = CachePriority.High;

        /// <summary>
        /// 历史数据优先级
        /// </summary>
        public CachePriority HistoricalDataPriority { get; set; } = CachePriority.Medium;
    }

    /// <summary>
    /// 缓存优先级
    /// </summary>
    public enum CachePriority
    {
        /// <summary>
        /// 低优先级
        /// </summary>
        Low = 0,

        /// <summary>
        /// 中等优先级
        /// </summary>
        Medium = 1,

        /// <summary>
        /// 高优先级
        /// </summary>
        High = 2,

        /// <summary>
        /// 最高优先级
        /// </summary>
        Critical = 3
    }

    /// <summary>
    /// 缓存配置根选项
    /// </summary>
    public class CacheOptions
    {
        /// <summary>
        /// Redis缓存配置
        /// </summary>
        public RedisCacheOptions Redis { get; set; } = new();

        /// <summary>
        /// 内存缓存配置
        /// </summary>
        public MemoryCacheOptions Memory { get; set; } = new();

        /// <summary>
        /// 缓存策略配置
        /// </summary>
        public CacheStrategyOptions Strategy { get; set; } = new();
    }
} 