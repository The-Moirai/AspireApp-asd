using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Message;
using ClassLibrary_Core.Mission;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Timers;
namespace WebApplication_Drone.Services
{
    public class DroneDataService
    {
        private readonly ILogger<DroneDataService> _logger;
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


        public DroneDataService(ILogger<DroneDataService> logger)
        {
            _logger = logger;
            _logger.LogInformation("DroneDataService initialized.");
        }


        /// <summary>
        /// 无人机数据变更事件
        /// </summary>
        public event EventHandler<DroneChangedEventArgs>? DroneChanged;



        /// <summary>
        /// 无人机数据变更事件触发方法
        /// </summary>
        /// <param name="action"></param>
        /// <param name="drone"></param>
        protected virtual void OnDroneChanged(string action, Drone drone)
        {
            DroneChanged?.Invoke(this, new DroneChangedEventArgs { Action = action, Drone = drone });
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
        public Drone? GetDrone(int id)
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
            if (drones == null || !drones.Any())
            {
                _logger.LogWarning("Received null or empty drone data collection. All existing drones will be marked as offline if any.");
            }
            else
            {
                _logger.LogInformation("Received {DroneCount} drones from source.", drones.Count());
                _logger.LogDebug("Received drone data: {DroneData}", JsonSerializer.Serialize(drones));
            }

            lock (_lock)
            {
                // 1. 记录本次所有无人机Id
                var nameIdMap = _drones.ToDictionary(d => d.Name, d => d.Id);
                var newList = drones.Select(d =>
                {
                    if (nameIdMap.TryGetValue(d.Name, out var existingId))
                    {
                        
                        d.Id = existingId;
                    }


                    return d;
                }).ToList();
                var newIds = new HashSet<int>(newList.Select(d => d.Id));

                // 2. 找出上一次有但本次没有的无人机
                var lostDrones = _lastDrones
                    .Where(d => !newIds.Contains(d.Id))
                    .Select(d =>
                    {
                        d.Status = DroneStatus.Offline;
                        return d;
                    })
                    .ToList();

                if (lostDrones.Any())
                {
                    _logger.LogInformation("{LostDroneCount} drones are now considered offline: {LostDroneIds}", lostDrones.Count, string.Join(", ", lostDrones.Select(d => d.Id)));
                }

                // 3. 全面重新计算所有无人机的邻接关系
                foreach (var drone in newList)
                {
                    if (drone.Status == DroneStatus.Offline)
                    {
                        drone.ConnectedDroneIds.Clear();
                        continue;
                    }

                    // 找出所有在当前无人机半径范围内的其他无人机
                    drone.ConnectedDroneIds = newList
                        .Where(otherDrone =>
                        {
                            if (drone.Id == otherDrone.Id || otherDrone.Status == DroneStatus.Offline)
                            {
                                return false; // 排除自己和离线无人机
                            }
                            // 检查距离是否在半径内
                            return drone.CurrentPosition.DistanceTo(otherDrone.CurrentPosition) <= drone.radius;
                        })
                        .Select(d => d.Id)
                        .ToList();
                }

                // 4. 合并：本次数据 + 丢失的无人机（状态已设为Offline）
                _drones.Clear();
                _drones.AddRange(newList);
                _drones.AddRange(lostDrones);

                _logger.LogInformation("Drone data updated. Total drones in service: {TotalDrones}. Drones received: {NewListCount}, Drones now offline: {OfflineDronesCount}", _drones.Count, newList.Count, lostDrones.Count);

                // 5. 更新快照
                _lastDrones = _drones.Select(d => CloneDrone(d)).ToList();
            }
        }
        /// <summary>
        /// 添加新的无人机
        /// </summary>
        /// <param name="drone">无人机实体</param>
        public void AddDrone(Drone drone)
        {
            _logger.LogInformation("Attempting to add drone with Name: {DroneName}", drone.Name);
            lock (_lock)
            {
                var existingDrone = _drones.FirstOrDefault(d => d.Name == drone.Name);
                if (existingDrone != null)
                {
                    // 保持相同ID
                    drone.Id = existingDrone.Id;
                    _logger.LogInformation("Drone with Name {DroneName} already exists. Re-assigning Id {DroneId}.", drone.Name, drone.Id);
                }
                if (!_drones.Any(d => d.Id == drone.Id))
                { // 检测半径范围内的无人机
                    var nearbyDrones = _drones
                        .Where(d =>
                            d.Status != DroneStatus.Offline &&
                            d.CurrentPosition.DistanceTo(drone.CurrentPosition) <= drone.radius)
                        .Select(d => d.Id)
                        .ToList();
                    drone.ConnectedDroneIds = new List<int>(nearbyDrones);
                    foreach (var nearbyId in nearbyDrones)
                    {
                        var nearbyDrone = _drones.First(d => d.Id == nearbyId);
                        if (!nearbyDrone.ConnectedDroneIds.Contains(drone.Id))
                        {
                            nearbyDrone.ConnectedDroneIds.Add(drone.Id);
                        }
                    }
                    _drones.Add(drone);
                    _logger.LogInformation("Successfully added new drone with Id {DroneId} and Name {DroneName}.", drone.Id, drone.Name);
                    OnDroneChanged("Add", drone);
                }
                else
                {
                    _logger.LogWarning("Drone with Id {DroneId} already in the list. Skipping add operation.", drone.Id);
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
            _logger.LogInformation("Attempting to update drone with ID: {DroneId}", drone.Id);
            lock (_lock)
            {
                var idx = _drones.FindIndex(d => d.Id == drone.Id);
                if (idx >= 0)
                {
                    _logger.LogInformation("Drone with ID: {DroneId} found. Updating.", drone.Id);
                    _drones[idx] = drone;

                    if (drone.Status == DroneStatus.Offline)
                    {
                        _logger.LogInformation("Drone {DroneId} is offline, clearing subtasks and firing 'Delete' event.", drone.Id);
                        _drones[idx].AssignedSubTasks.Clear();
                        OnDroneChanged("Delete", drone);
                    }
                    else
                    {
                        _logger.LogInformation("Drone {DroneId} status updated, firing 'Update' event.", drone.Id);
                        OnDroneChanged("Update", drone);
                    }
                    return true;
                }
                _logger.LogWarning("Update failed. Drone with ID: {DroneId} not found.", drone.Id);
                return false;
            }
        }
        /// <summary>
        /// 删除指定无人机
        /// </summary>
        /// <param name="id">指定无人机id</param>
        /// <returns>删除成功与否</returns>
        public bool DeleteDrone(int id)
        {
            _logger.LogInformation("Attempting to delete drone with ID: {DroneId}", id);
            lock (_lock)
            {
                var drone = _drones.FirstOrDefault(d => d.Id == id);
                if (drone != null)
                {
                    _drones.Remove(drone);
                    OnDroneChanged("Delete", drone);
                    _logger.LogInformation("Successfully deleted drone with ID: {DroneId}", id);
                    return true;
                }
                _logger.LogWarning("Delete failed. Drone with ID: {DroneId} not found.", id);
                return false;
            }
        }
        /// <summary>
        /// 获取指定无人机的所有子任务
        /// </summary>
        public List<SubTask> GetSubTasks(int droneId)
        {
            _logger.LogInformation("Getting all subtasks for drone {DroneId}", droneId);
            lock (_lock)
            {
                var drone = _drones.FirstOrDefault(d => d.Id == droneId);
                return drone?.AssignedSubTasks.ToList() ?? new List<SubTask>();
            }
        }
        /// <summary>
        /// 获取指定无人机的指定子任务
        /// </summary>
        public SubTask? GetSubTask(int droneId, Guid subTaskId)
        {
            _logger.LogInformation("Getting subtask {SubTaskId} for drone {DroneId}", subTaskId, droneId);
            lock (_lock)
            {
                var drone = _drones.FirstOrDefault(d => d.Id == droneId);
                return drone?.AssignedSubTasks.FirstOrDefault(st => st.Id == subTaskId);
            }
        }
        /// <summary>
        /// 为指定无人机添加子任务
        /// </summary>
        public bool AddSubTask(int droneId, SubTask subTask)
        {
            _logger.LogInformation("Attempting to add subtask {SubTaskId} to drone {DroneId}", subTask.Id, droneId);
            lock (_lock)
            {
                var drone = _drones.FirstOrDefault(d => d.Id == droneId);
                if (drone == null)
                {
                    _logger.LogWarning("AddSubTask failed. Drone with ID: {DroneId} not found.", droneId);
                    return false;
                }
                if (!drone.AssignedSubTasks.Any(st => st.Id == subTask.Id))
                {
                    drone.AssignedSubTasks.Add(subTask);
                    _logger.LogInformation("Successfully added subtask {SubTaskId} to drone {DroneId}", subTask.Id, droneId);
                    return true;
                }
                _logger.LogWarning("AddSubTask failed. Subtask with ID: {SubTaskId} already exists on drone {DroneId}.", subTask.Id, droneId);
                return false;
            }
        }
        /// <summary>
        /// 更新指定无人机的子任务
        /// </summary>
        public bool UpdateSubTask(int droneId, SubTask subTask)
        {
            _logger.LogInformation("Attempting to update subtask {SubTaskId} on drone {DroneId}", subTask.Id, droneId);
            lock (_lock)
            {
                var drone = _drones.FirstOrDefault(d => d.Id == droneId);
                if (drone == null)
                {
                    _logger.LogWarning("UpdateSubTask failed. Drone with ID: {DroneId} not found.", droneId);
                    return false;
                }
                var idx = drone.AssignedSubTasks.FindIndex(st => st.Id == subTask.Id);
                if (idx >= 0)
                {
                    drone.AssignedSubTasks[idx] = subTask;
                    _logger.LogInformation("Successfully updated subtask {SubTaskId} on drone {DroneId}", subTask.Id, droneId);
                    return true;
                }
                _logger.LogWarning("UpdateSubTask failed. Subtask with ID: {SubTaskId} not found on drone {DroneId}.", subTask.Id, droneId);
                return false;
            }
        }
        /// <summary>
        /// 删除指定无人机的子任务
        /// </summary>
        public bool RemoveSubTask(int droneId, Guid subTaskId)
        {
            _logger.LogInformation("Attempting to remove subtask {SubTaskId} from drone {DroneId}", subTaskId, droneId);
            lock (_lock)
            {
                var drone = _drones.FirstOrDefault(d => d.Id == droneId);
                if (drone == null)
                {
                    _logger.LogWarning("RemoveSubTask failed. Drone with ID: {DroneId} not found.", droneId);
                    return false;
                }
                var subTask = drone.AssignedSubTasks.FirstOrDefault(st => st.Id == subTaskId);
                if (subTask != null)
                {
                    drone.AssignedSubTasks.Remove(subTask);
                    _logger.LogInformation("Successfully removed subtask {SubTaskId} from drone {DroneId}", subTaskId, droneId);
                    return true;
                }
                _logger.LogWarning("RemoveSubTask failed. Subtask with ID: {SubTaskId} not found on drone {DroneId}.", subTaskId, droneId);
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
                ConnectedDroneIds = new List<int>(d.ConnectedDroneIds),
                AssignedSubTasks = d.AssignedSubTasks.ToList()
            };
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