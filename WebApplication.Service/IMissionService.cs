using ClassLibrary_Core.Mission;
using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Data;
using ClassLibrary_Core.Common;

namespace WebApplication.Service
{
    public interface IMissionService
    {
        // 基础CRUD操作
        Task<IEnumerable<MissionHistory>> GetAllMissionHistoriesAsync();
        Task<MissionHistory> GetMissionHistoryByIdAsync(Guid id);
        Task<MissionHistory> CreateMissionHistoryAsync(MissionHistory missionHistory);

        // 单个无人机相关查询
        Task<IEnumerable<MissionHistory>> GetDroneRecentMissionsAsync(string droneName, TimeSpan timeSpan);
        Task<IEnumerable<MissionHistory>> GetDroneMissionsByTaskAsync(string droneName, Guid taskId);
        
        // 单个任务相关查询
        Task<IEnumerable<MissionHistory>> GetTaskMissionsByDroneAsync(Guid taskId, string droneName);
        Task<IEnumerable<MissionHistory>> GetTaskMissionsForAllDronesAsync(Guid taskId);
        
        // 时间范围查询
        Task<IEnumerable<MissionHistory>> GetMissionsByTimeRangeAsync(DateTime startTime, DateTime endTime);
        Task<Dictionary<string, IEnumerable<MissionHistory>>> GetDronesMissionsByTimeRangeAsync(DateTime startTime, DateTime endTime);
    }

    public interface ITaskDataService
    {
        // 主任务管理
        Task<List<MainTask>> GetTasksAsync();
        Task<MainTask> GetTaskAsync(Guid id);
        Task AddTaskAsync(MainTask task, string createdBy);
        Task<bool> UpdateTaskAsync(MainTask task);
        Task<bool> DeleteTaskAsync(Guid id);
        Task<List<MainTask>> GetTasksByStatusAsync(System.Threading.Tasks.TaskStatus status);

        // 子任务管理
        Task<List<SubTask>> GetSubTasksAsync(Guid mainTaskId);
        Task<SubTask> GetSubTaskAsync(Guid mainTaskId, Guid subTaskId);
        Task AddSubTaskAsync(Guid mainTaskId, SubTask subTask);
        Task<bool> UpdateSubTaskAsync(Guid mainTaskId, SubTask subTask);
        Task<bool> DeleteSubTaskAsync(Guid mainTaskId, Guid subTaskId);

        // 任务分配和完成
        Task<bool> AssignSubTaskAsync(Guid mainTaskId, Guid subTaskId, string droneName);
        Task<bool> CompleteSubTaskAsync(Guid mainTaskId, string subTaskDescription);
        Task<bool> UnloadSubTaskAsync(Guid mainTaskId, Guid subTaskId);
        Task<bool> ReloadSubTaskAsync(Guid mainTaskId, Guid subTaskId, string droneName);

        // 数据查询
        Task<List<DroneDataPoint>> GetTaskDroneDataAsync(Guid taskId, Guid droneId);
        Task<List<DroneDataPoint>> GetTaskAllDronesDataAsync(Guid taskId);
        Task<List<DroneDataPoint>> GetAllTasksDataInTimeRangeAsync(DateTime startTime, DateTime endTime);

        // 批量操作
        Task<int> BatchUpdateSubTaskStatusAsync(List<Guid> subTaskIds, System.Threading.Tasks.TaskStatus newStatus, string reason = null);
        Task<int> ReassignFailedSubTasksAsync();
        Task<int> CleanupOldCompletedTasksAsync(TimeSpan maxAge);

        // 统计和分析
        Task<TaskStatistics> GetTaskStatisticsAsync();
        Task<TaskPerformanceAnalysis> GetTaskPerformanceAnalysisAsync();
        Task<List<SubTask>> GetExpiredSubTasksAsync(TimeSpan timeout);
        Task<List<SubTask>> GetActiveSubTasksForDroneAsync(string droneName);

        // 数据库同步
        Task LoadTasksFromDatabaseAsync();
        Task SyncAllTasksToDatabaseAsync();

        // 文件上传
        Task<TaskUploadDto> SaveTaskWithVideoAsync(TaskUploadDto taskUpload, object videoFile);

        // 事件
        event EventHandler<TaskChangedEventArgs> TaskChanged;
    }

    public interface ISocketService
    {
        Task ConnectAsync(string host, int port);
        Task SendMessageAsync(object message);
        Task SendFileAsync(string filePath);
        void Disconnect();
        bool IsConnected();

        // 事件
        event EventHandler<DroneChangedEventArgs> DroneChanged;
        event EventHandler<TaskChangedEventArgs> TaskChanged;
    }

    public class TaskStatistics
    {
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int PendingTasks { get; set; }
        public int FailedTasks { get; set; }
        public int TotalSubTasks { get; set; }
        public int CompletedSubTasks { get; set; }
        public int InProgressSubTasks { get; set; }
        public int PendingSubTasks { get; set; }
        public int FailedSubTasks { get; set; }
    }

    public class TaskPerformanceAnalysis
    {
        public double AverageCompletionTime { get; set; }
        public int TasksCompletedToday { get; set; }
        public int TasksCompletedThisWeek { get; set; }
        public double SuccessRate { get; set; }
        public List<DronePerformanceMetric> DronePerformance { get; set; } = new();
    }

    public class DronePerformanceMetric
    {
        public string DroneName { get; set; }
        public int CompletedTasks { get; set; }
        public double AverageCompletionTime { get; set; }
        public double SuccessRate { get; set; }
    }

    public class TaskChangedEventArgs : EventArgs
    {
        public string Action { get; set; } = "";
        public MainTask MainTask { get; set; }
        public SubTask SubTask { get; set; } = new SubTask();
    }
} 