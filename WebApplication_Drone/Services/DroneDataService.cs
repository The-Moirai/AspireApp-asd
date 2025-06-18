using ClassLibrary_Core.Data;
using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Message;
using ClassLibrary_Core.Mission;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using System.Timers;
namespace WebApplication_Drone.Services
{
    public class DroneDataService
    {
        /// <summary>
        /// 上一次的无人机快照
        /// </summary>
        private List<Drone> _lastDrones = new(); 
        /// <summary>
        /// 无人机数据列表
        /// </summary>
        private readonly List<Drone> _drones = new();
        /// <summary>
        /// 无人机数据
        /// </summary>
        private readonly object _lock = new();
        /// <summary>
        /// 数据库连接
        /// </summary>
        private readonly SqlserverService _sqlserverService;
        /// <summary>
        /// 无人机数据变更事件
        /// </summary>
        public event EventHandler<DroneChangedEventArgs>? DroneChanged;

        public DroneDataService(SqlserverService sqlserverService) 
        {
            _sqlserverService = sqlserverService;
        }
      



        /// <summary>
        /// 无人机数据变更事件触发方法
        /// </summary>
        /// <param name="action"></param>
        /// <param name="drone"></param>
        protected virtual void OnDroneChanged(string action, Drone drone)
        {
            DroneChanged?.Invoke(this, new DroneChangedEventArgs
            {
                Action = action,
                Drone = CloneDrone(drone) // 避免外部修改
            });
        }
        /// <summary>
        /// 获取所有无人机数据的副本
        /// </summary>
        /// <returns>
        /// 所有无人机数据的副本列表
        /// </returns>
        public List<Drone> GetDrones()
        {
            lock (_lock)
            {
                return _drones.Select(d => d).ToList();
            }
        }
        /// <summary>
        /// 获取指定无人机实体
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Drone? GetDrone(Guid id)
        {
            lock (_lock)
            {
                return _drones.FirstOrDefault(d => d.Id == id);
            }
        }
        /// <summary>
        /// 设置无人机数据，并将丢失的无人机状态设为Offline
        /// </summary>
        /// <param name="drones">无人机数据列表</param>
        public void SetDrones(IEnumerable<Drone> drones)
        {
            lock (_lock)
            {

                // 处理名称匹配的无人机ID
                var nameIdMap = _drones.ToDictionary(d => d.Name, d => d.Id);
                var newList = drones.Select(d => {
                    if (nameIdMap.TryGetValue(d.Name, out var id)) d.Id = id;
                    return d;
                }).ToList();

                // 检测离线无人机
                var newIds = new HashSet<Guid>(newList.Select(d => d.Id));
                var lostDrones = _lastDrones
                    .Where(d => !newIds.Contains(d.Id))
                    .Select(d => { d.Status = DroneStatus.Offline; return d; })
                    .ToList();

                // 更新邻接关系
                UpdateDroneConnections(newList);

                // 合并数据
                _drones.Clear();
                _drones.AddRange(newList);
                _drones.AddRange(lostDrones);
                _lastDrones = _drones.Select(CloneDrone).ToList();

                // 同步到数据库
                _ = Task.Run(() => _sqlserverService.BulkUpdateDrones(_drones));
            }
        }
        /// <summary>
        /// 更新邻接关系
        /// </summary>
        /// <param name="newDrones"></param>
        private void UpdateDroneConnections(List<Drone> newDrones)
        {
            var onlineDrones = newDrones.Where(d => d.Status != DroneStatus.Offline).ToList();

            // 清空旧连接
            foreach (var drone in _drones)
                drone.ConnectedDroneIds.Clear();

            // 重建连接
            foreach (var drone in onlineDrones)
            {
                drone.ConnectedDroneIds = onlineDrones
                    .Where(d => d.Id != drone.Id &&
                        d.CurrentPosition.DistanceTo(drone.CurrentPosition) <= drone.radius)
                    .Select(d => d.Id)
                    .ToList();
            }
        }
        /// <summary>
        /// 添加新的无人机
        /// </summary>
        /// <param name="drone">无人机实体</param>
        public void AddDrone(Drone drone)
        {
            lock (_lock)
            {
                var existing = _drones.FirstOrDefault(d => d.Name == drone.Name);
                if (existing != null) drone.Id = existing.Id;

                if (!_drones.Any(d => d.Id == drone.Id))
                {
                    // 建立新连接
                    var nearby = _drones
                        .Where(d => d.Status != DroneStatus.Offline &&
                            d.CurrentPosition.DistanceTo(drone.CurrentPosition) <= drone.radius)
                        .ToList();

                    drone.ConnectedDroneIds = nearby.Select(d => d.Id).ToList();
                    foreach (var neighbor in nearby)
                        neighbor.ConnectedDroneIds.Add(drone.Id);

                    _drones.Add(drone);
                    _sqlserverService.AddOrUpdateDroneAsync(drone);
                    OnDroneChanged("Add", drone);
                }
            }
        }
        /// <summary>
        /// 更新指定无人机信息
        /// </summary>
        /// <param name="drone">指定无人机实体</param>
        /// <returns>更新成功与否</returns>
        public bool UpdateDrone(Drone drone)
        {
            lock (_lock)
            {
                var index = _drones.FindIndex(d => d.Id == drone.Id);
                if (index < 0) return false;

                var oldDrone = _drones[index];
                _drones[index] = drone;

                // 状态处理
                if (drone.Status == DroneStatus.Offline)
                {
                    drone.AssignedSubTasks.Clear();
                    // 断开所有连接
                    foreach (var other in _drones)
                        other.ConnectedDroneIds.Remove(drone.Id);
                }

                _sqlserverService.AddOrUpdateDroneAsync(drone);
                OnDroneChanged("Update", drone);
                return true;
            }
        }
        /// <summary>
        /// 删除指定无人机
        /// </summary>
        /// <param name="id">指定无人机id</param>
        /// <returns>删除成功与否</returns>
        public bool DeleteDrone(Guid id)
        {
            lock (_lock)
            {
                var drone = _drones.FirstOrDefault(d => d.Id == id);
                if (drone != null)
                {
                    _drones.Remove(drone);
                    return true;
                }
                return false;
            }
        }
        /// <summary>
        /// 获取指定无人机的所有子任务
        /// </summary>
        public List<SubTask> GetSubTasks(Guid droneId)
        {
            lock (_lock)
            {
                var drone = _drones.FirstOrDefault(d => d.Id == droneId);
                return drone?.AssignedSubTasks.ToList() ?? new List<SubTask>();
            }
        }
        /// <summary>
        /// 获取指定无人机的指定子任务
        /// </summary>
        public SubTask? GetSubTask(Guid droneId, Guid subTaskId)
        {
            lock (_lock)
            {
                var drone = _drones.FirstOrDefault(d => d.Id == droneId);
                return drone?.AssignedSubTasks.FirstOrDefault(st => st.Id == subTaskId);
            }
        }
        /// <summary>
        /// 为指定无人机添加子任务
        /// </summary>
        public bool AddSubTask(Guid droneId, SubTask subTask)
        {
            lock (_lock)
            {
                var drone = _drones.FirstOrDefault(d => d.Id == droneId);
                if (drone == null) return false;
                if (!drone.AssignedSubTasks.Any(st => st.Id == subTask.Id))
                {
                    drone.AssignedSubTasks.Add(subTask);
                    return true;
                }
                return false;
            }
        }
        /// <summary>
        /// 更新指定无人机的子任务
        /// </summary>
        public bool UpdateSubTask(Guid droneId, SubTask subTask)
        {
            lock (_lock)
            {
                var drone = _drones.FirstOrDefault(d => d.Id == droneId);
                if (drone == null) return false;
                var idx = drone.AssignedSubTasks.FindIndex(st => st.Id == subTask.Id);
                if (idx >= 0)
                {
                    drone.AssignedSubTasks[idx] = subTask;
                    return true;
                }
                return false;
            }
        }
        /// <summary>
        /// 删除指定无人机的子任务
        /// </summary>
        public bool RemoveSubTask(Guid droneId, Guid subTaskId)
        {
            lock (_lock)
            {
                var drone = _drones.FirstOrDefault(d => d.Id == droneId);
                if (drone == null) return false;
                var subTask = drone.AssignedSubTasks.FirstOrDefault(st => st.Id == subTaskId);
                if (subTask != null)
                {
                    drone.AssignedSubTasks.Remove(subTask);
                    return true;
                }
                return false;
            }
        }
        /// <summary>
        /// 简单克隆，避免引用问题
        /// </summary>
        private Drone CloneDrone(Drone d)
        {
            return new Drone
            {
                Id = d.Id,
                Name = d.Name,
                ModelStatus = d.ModelStatus,
                CurrentPosition = d.CurrentPosition, // 若GPSPosition为引用类型且可变，建议深拷贝
                Status = d.Status,
                cpu_used_rate = d.cpu_used_rate,
                radius = d.radius,
                left_bandwidth = d.left_bandwidth,
                memory = d.memory,
                ConnectedDroneIds = new List<Guid>(d.ConnectedDroneIds),
                AssignedSubTasks = d.AssignedSubTasks.ToList()
            };
        }

        /// <summary>
        /// 获取指定无人机在最近一段时间的数据
        /// </summary>
        /// <param name="droneId"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        public async Task<List<DroneDataPoint>> GetRecentDroneDataAsync(Guid droneId, TimeSpan duration)
        {
              return await _sqlserverService.GetDroneStatusHistoryAsync(droneId, DateTime.UtcNow - duration, DateTime.UtcNow);            
        }
        /// <summary>
        /// 获取指定无人机在指定任务期间的数据
        /// </summary>
        /// <param name="droneId"></param>
        /// <param name="taskId"></param>
        /// <returns></returns>
            public async Task<List<DroneDataPoint>> GetDroneTaskDataAsync(Guid droneId, Guid taskId)
            {
            // 获取任务时间范围
            var taskTimeRange = await _sqlserverService.GetTaskTimeRangeAsync(taskId);
            if (taskTimeRange == null)
            {
                return new List<DroneDataPoint>();
            }

            // 获取无人机在任务时间范围内的数据
            return await _sqlserverService.GetDroneDataInTimeRangeAsync(
                droneId,
                taskTimeRange.StartTime,
                taskTimeRange.EndTime
            );
        }
        /// <summary>
        /// 获取指定时间段中指定无人机的数据
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        public async Task<List<DroneDataPoint>> GetAllDronesDataInTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            List<DroneDataPoint> recentData = new List<DroneDataPoint>();
            return recentData;
        }
    }
    /// <summary>
    /// 无人机数据变更事件参数
    /// </summary>
    public class DroneChangedEventArgs : EventArgs
    {
        public string Action { get; set; } = "";
        public Drone Drone { get; set; }
    }
}