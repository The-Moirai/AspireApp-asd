using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Mission;
using ClassLibrary_Core.Data;

namespace ClassLibrary_Core.Services
{
    /// <summary>
    /// 无人机数据服务接口
    /// </summary>
    public interface IDroneDataService
    {
        // 事件
        event EventHandler<DroneChangedEventArgs>? DroneChanged;

        // 基本CRUD操作
        Task<IEnumerable<Drone>> GetAllDronesAsync();
        Task<Drone?> GetDroneAsync(Guid droneId);
        Task<Drone?> GetDroneByNameAsync(string droneName);
        Task<bool> AddDroneAsync(Drone drone);
        Task<bool> UpdateDroneAsync(Drone drone);
        Task<bool> RemoveDroneAsync(Guid droneId);

        // 批量操作
        Task SetDronesAsync(IEnumerable<Drone> drones);
        Task<bool> BulkUpdateDronesAsync(IEnumerable<Drone> drones);

        // 状态查询
        Task<IEnumerable<Drone>> GetDronesByStatusAsync(DroneStatus status);
        Task<IEnumerable<Drone>> GetAvailableDronesAsync();
        Task<Dictionary<DroneStatus, int>> GetDroneStatusSummaryAsync();

        // 性能监控
        Task<IEnumerable<DroneDataPoint>> GetRecentDataPointsAsync(Guid droneId, TimeSpan timeRange);
        Task RecordDroneDataPointAsync(DroneDataPoint dataPoint);
    }

    /// <summary>
    /// 任务数据服务接口
    /// </summary>
    public interface ITaskDataService
    {
        // 事件
        event EventHandler<TaskChangedEventArgs>? TaskChanged;

        // 主任务操作
        Task<IEnumerable<MainTask>> GetAllTasksAsync();
        Task<MainTask?> GetTaskAsync(Guid taskId);
        Task<bool> AddTaskAsync(MainTask task);
        Task<bool> UpdateTaskAsync(MainTask task);
        Task<bool> RemoveTaskAsync(Guid taskId);

        // 子任务操作
        Task<bool> AddSubTaskAsync(Guid mainTaskId, SubTask subTask);
        Task<bool> UpdateSubTaskAsync(SubTask subTask);
        Task<bool> AssignSubTaskAsync(Guid mainTaskId, Guid subTaskId, string droneName);
        Task<bool> CompleteSubTaskAsync(Guid mainTaskId, Guid subTaskId);
        Task<bool> ReassignFailedSubTasksAsync();

        // 批量操作
        Task SetTasksAsync(IEnumerable<MainTask> tasks);
        Task<bool> BatchUpdateSubTaskStatusAsync(IEnumerable<(Guid SubTaskId, TaskStatus Status)> updates);

        // 查询操作
        Task<IEnumerable<MainTask>> GetTasksByStatusAsync(TaskStatus status);
        Task<IEnumerable<SubTask>> GetSubTasksByDroneAsync(string droneName);
        Task<Dictionary<TaskStatus, int>> GetTaskStatusSummaryAsync();
        Task<(DateTime Start, DateTime End)> GetTaskTimeRangeAsync();

        // 清理操作
        Task CleanupOldCompletedTasksAsync(TimeSpan olderThan);
    }

    /// <summary>
    /// 数据库服务接口
    /// </summary>
    public interface IDatabaseService
    {
        // 连接管理
        Task<bool> TestConnectionAsync();
        Task<string> GetConnectionStringAsync();

        // 无人机数据持久化
        Task<bool> SaveDroneAsync(Drone drone);
        Task<bool> SaveDroneStatusAsync(Guid droneId, DroneStatus status, DateTime timestamp);
        Task<bool> BulkSaveDroneStatusAsync(IEnumerable<(Guid DroneId, DroneStatus Status, DateTime Timestamp)> statusUpdates);
        Task<IEnumerable<Drone>> LoadAllDronesAsync();
        Task<Drone?> LoadDroneAsync(Guid droneId);
        Task<Drone?> LoadDroneByNameAsync(string droneName);

        // 任务数据持久化
        Task<bool> SaveMainTaskAsync(MainTask task);
        Task<bool> SaveSubTaskAsync(SubTask subTask);
        Task<IEnumerable<MainTask>> LoadAllTasksAsync();
        Task<MainTask?> LoadTaskAsync(Guid taskId);
        Task<bool> DeleteTaskAsync(Guid taskId);

        // 历史数据
        Task<IEnumerable<DroneDataPoint>> LoadDroneHistoryAsync(Guid droneId, DateTime startTime, DateTime endTime);
        Task<bool> SaveDroneDataPointAsync(DroneDataPoint dataPoint);
        Task<bool> BulkSaveDroneDataPointsAsync(IEnumerable<DroneDataPoint> dataPoints);

        // 性能优化
        Task<bool> OptimizeDatabaseAsync();
        Task<DatabaseStatistics> GetDatabaseStatisticsAsync();
    }

    /// <summary>
    /// 缓存服务接口
    /// </summary>
    public interface ICacheService
    {
        // 基本缓存操作
        Task<T?> GetAsync<T>(string key) where T : class;
        Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        Task<bool> RemoveAsync(string key);
        Task<bool> ExistsAsync(string key);

        // 批量操作
        Task<Dictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys) where T : class;
        Task<bool> SetManyAsync<T>(Dictionary<string, T> keyValues, TimeSpan? expiration = null) where T : class;
        Task<bool> RemoveManyAsync(IEnumerable<string> keys);

        // 模式操作
        Task<IEnumerable<string>> GetKeysByPatternAsync(string pattern);
        Task<bool> RemoveByPatternAsync(string pattern);

        // 缓存统计
        Task<long> GetCacheCountAsync();
        Task<bool> ClearCacheAsync();
    }

    /// <summary>
    /// 无人机状态变更事件参数
    /// </summary>
    public class DroneChangedEventArgs : EventArgs
    {
        public Drone Drone { get; }
        public DroneChangeAction Action { get; }
        public DateTime Timestamp { get; }

        public DroneChangedEventArgs(Drone drone, DroneChangeAction action)
        {
            Drone = drone;
            Action = action;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 任务状态变更事件参数
    /// </summary>
    public class TaskChangedEventArgs : EventArgs
    {
        public MainTask Task { get; }
        public SubTask? SubTask { get; }
        public TaskChangeAction Action { get; }
        public DateTime Timestamp { get; }

        public TaskChangedEventArgs(MainTask task, TaskChangeAction action, SubTask? subTask = null)
        {
            Task = task;
            SubTask = subTask;
            Action = action;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 无人机变更操作类型
    /// </summary>
    public enum DroneChangeAction
    {
        Add,
        Update,
        Delete,
        StatusChanged,
        Offline,
        Online
    }

    /// <summary>
    /// 任务变更操作类型
    /// </summary>
    public enum TaskChangeAction
    {
        Add,
        Update,
        Delete,
        StatusChanged,
        SubTaskAdded,
        SubTaskAssigned,
        SubTaskCompleted,
        SubTaskFailed
    }

    /// <summary>
    /// 数据库统计信息
    /// </summary>
    public class DatabaseStatistics
    {
        public int DroneCount { get; set; }
        public int TaskCount { get; set; }
        public int SubTaskCount { get; set; }
        public int DataPointCount { get; set; }
        public long DatabaseSizeBytes { get; set; }
        public DateTime LastOptimized { get; set; }
        public TimeSpan AverageQueryTime { get; set; }
    }
} 