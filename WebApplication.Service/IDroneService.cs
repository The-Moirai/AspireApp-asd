using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Common;
using ClassLibrary_Core.Data;
using ClassLibrary_Core.Mission;

namespace WebApplication.Service
{
    public interface IDroneService
    {
        // 基础 CRUD 操作
        Task<IEnumerable<Drone>> GetAllDronesAsync();
        Task<Drone> GetDroneByIdAsync(Guid id);
        Task<Drone> GetDroneByNameAsync(string name);
        Task<Drone> CreateDroneAsync(Drone drone);
        Task<Drone> UpdateDroneAsync(Drone drone);
        Task DeleteDroneAsync(Guid id);

        // 状态和位置更新
        Task<Drone> UpdateDroneStatusAsync(Guid id, DroneStatus status);
        Task<Drone> UpdateDronePositionAsync(Guid id, GPSPosition position);
        Task UpdateDroneHeartbeatAsync(Guid droneId);

        // 子任务管理
        Task<List<SubTask>> GetDroneSubTasksAsync(Guid droneId);
        Task<bool> AddSubTaskToDroneAsync(Guid droneId, SubTask subTask);
        Task<bool> UpdateDroneSubTaskAsync(Guid droneId, SubTask subTask);
        Task<bool> RemoveSubTaskFromDroneAsync(Guid droneId, Guid subTaskId);
        Task<List<SubTask>> GetActiveSubTasksForDroneAsync(string droneName);

        // 数据历史查询
        Task<List<DroneDataPoint>> GetRecentDroneDataAsync(Guid droneId, TimeSpan duration);
        Task<List<DroneDataPoint>> GetDroneTaskDataAsync(Guid droneId, Guid taskId);
        Task<List<DroneDataPoint>> GetAllDronesDataInTimeRangeAsync(DateTime startTime, DateTime endTime);

        // 集群状态
        Task<ClusterStatus> GetClusterStatusAsync();
        Task BulkUpdateDronesAsync(IEnumerable<Drone> drones);

        // 数据记录
        Task RecordDroneStatusAsync(Drone drone);
        Task BulkRecordDroneStatusAsync(IEnumerable<Drone> drones);

        // 连接管理
        Task UpdateDroneConnectionsAsync(List<Drone> drones);
        Task<bool> DroneExistsAsync(Guid id);

        // 事件处理
        event EventHandler<DroneChangedEventArgs> DroneChanged;
    }

    public class ClusterStatus
    {
        public int Total { get; set; }
        public int Online { get; set; }
        public int InMission { get; set; }
        public int Offline { get; set; }
    }

    public class DroneChangedEventArgs : EventArgs
    {
        public string Action { get; set; } = "";
        public Drone Drone { get; set; }
    }
} 