using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Mission;
using ClassLibrary_Core.Data;
using WebApplication_Drone.Services.Models;

namespace WebApplication_Drone.Services.Interfaces
{
    /// <summary>
    /// 数据服务接口 - 定义统一的数据访问契约
    /// </summary>
    public interface IDataService
    {
        // 无人机相关操作
        Task<List<Drone>> GetDronesAsync();
        Task<Drone?> GetDroneAsync(Guid id);
        Task<Drone?> GetDroneByNameAsync(string droneName);
        Task<bool> AddDroneAsync(Drone drone);
        Task<bool> UpdateDroneAsync(Drone drone);
        Task<bool> DeleteDroneAsync(Guid id);
        Task<int> GetDroneCountAsync();
        
        // 任务相关操作
        Task<List<MainTask>> GetTasksAsync();
        Task<MainTask?> GetTaskAsync(Guid id);
        Task<bool> AddTaskAsync(MainTask task, string createdBy);
        Task<bool> UpdateTaskAsync(MainTask task);
        Task<bool> DeleteTaskAsync(Guid id);
        Task<int> GetTaskCountAsync();
        
        // 子任务相关操作
        Task<List<SubTask>> GetSubTasksAsync(Guid mainTaskId);
        Task<SubTask?> GetSubTaskAsync(Guid mainTaskId, Guid subTaskId);
        Task<bool> AddSubTaskAsync(SubTask subTask);
        Task<bool> UpdateSubTaskAsync(SubTask subTask);
        Task<bool> DeleteSubTaskAsync(Guid mainTaskId, Guid subTaskId);
        
        // 数据点相关操作
        Task<List<DroneDataPoint>> GetDroneDataAsync(Guid droneId, DateTime startTime, DateTime endTime);
        Task<List<DroneDataPoint>> GetTaskDataAsync(Guid taskId, Guid droneId);
        Task<List<DroneDataPoint>> GetAllDronesDataAsync(DateTime startTime, DateTime endTime);
        
        // 图片相关操作
        Task<Guid> SaveImageAsync(Guid subTaskId, byte[] imageData, string fileName, int imageIndex = 1, string? description = null);
        Task<List<SubTaskImage>> GetImagesAsync(Guid subTaskId);
        Task<SubTaskImage?> GetImageAsync(Guid imageId);
        Task<bool> DeleteImageAsync(Guid imageId);
        
        // 批量操作
        Task<bool> BulkUpdateDronesAsync(IEnumerable<Drone> drones);
        Task<bool> BulkUpdateTasksAsync(IEnumerable<MainTask> tasks);
        Task<bool> BulkRecordDroneStatusAsync(IEnumerable<Drone> drones);
        
        // 统计和监控
        Task<DataServiceStatistics> GetStatisticsAsync();
        Task<bool> IsHealthyAsync();
    }
} 