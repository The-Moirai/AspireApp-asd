using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Data;

namespace WebApplication_Drone.Services.Interfaces
{
    /// <summary>
    /// 无人机数据访问接口
    /// </summary>
    public interface IDroneRepository
    {
        // 基础CRUD操作
        Task<List<Drone>> GetAllAsync();
        Task<Drone?> GetByIdAsync(Guid id);
        Task<Drone?> GetByNameAsync(string droneName);
        Task<bool> AddAsync(Drone drone);
        Task<bool> UpdateAsync(Drone drone);
        Task<bool> DeleteAsync(Guid id);
        Task<int> GetCountAsync();
        
        // 批量操作
        Task<bool> BulkUpdateAsync(IEnumerable<Drone> drones);
        Task<bool> BulkRecordStatusAsync(IEnumerable<Drone> drones);
        
        // 数据点操作
        Task<List<DroneDataPoint>> GetDataInTimeRangeAsync(Guid droneId, DateTime startTime, DateTime endTime);
        Task<List<DroneDataPoint>> GetAllDronesDataInTimeRangeAsync(DateTime startTime, DateTime endTime);
        
        // 状态检查
        Task<bool> ExistsAsync(Guid id);
        Task<bool> IsHealthyAsync();
        
        // 统计信息
        Task<DroneRepositoryStatistics> GetStatisticsAsync();
    }
    
    /// <summary>
    /// 无人机数据访问统计信息
    /// </summary>
    public class DroneRepositoryStatistics
    {
        public int TotalDrones { get; set; }
        public int OnlineDrones { get; set; }
        public int OfflineDrones { get; set; }
        public long TotalOperations { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;
        public bool DatabaseConnected { get; set; }
    }
} 