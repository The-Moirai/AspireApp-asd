using ClassLibrary_Core.Mission;
using ClassLibrary_Core.Data;

namespace WebApplication_Drone.Services.Interfaces
{
    /// <summary>
    /// 任务数据访问接口
    /// </summary>
    public interface ITaskRepository
    {
        // 主任务CRUD操作
        Task<List<MainTask>> GetAllAsync();
        Task<MainTask?> GetByIdAsync(Guid id);
        Task<bool> AddAsync(MainTask task, string createdBy);
        Task<bool> UpdateAsync(MainTask task);
        Task<bool> DeleteAsync(Guid id);
        Task<int> GetCountAsync();
        
        // 子任务操作
        Task<List<SubTask>> GetSubTasksAsync(Guid mainTaskId);
        Task<SubTask?> GetSubTaskAsync(Guid mainTaskId, Guid subTaskId);
        Task<bool> AddSubTaskAsync(SubTask subTask);
        Task<bool> UpdateSubTaskAsync(SubTask subTask);
        Task<bool> DeleteSubTaskAsync(Guid mainTaskId, Guid subTaskId);
        
        // 图片操作
        Task<Guid> SaveImageAsync(Guid subTaskId, byte[] imageData, string fileName, int imageIndex = 1, string? description = null);
        Task<List<SubTaskImage>> GetImagesAsync(Guid subTaskId);
        Task<SubTaskImage?> GetImageAsync(Guid imageId);
        Task<bool> DeleteImageAsync(Guid imageId);
        
        // 批量操作
        Task<bool> BulkUpdateAsync(IEnumerable<MainTask> tasks);
        
        // 数据点操作
        Task<List<DroneDataPoint>> GetTaskDataAsync(Guid taskId, Guid droneId);
        
        // 状态检查
        Task<bool> ExistsAsync(Guid id);
        Task<bool> IsHealthyAsync();
        
        // 统计信息
        Task<TaskRepositoryStatistics> GetStatisticsAsync();
    }
    
    /// <summary>
    /// 任务数据访问统计信息
    /// </summary>
    public class TaskRepositoryStatistics
    {
        public int TotalTasks { get; set; }
        public int ActiveTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int TotalSubTasks { get; set; }
        public int TotalImages { get; set; }
        public long TotalOperations { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;
        public bool DatabaseConnected { get; set; }
    }
} 