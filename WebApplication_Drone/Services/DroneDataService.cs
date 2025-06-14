using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Message;
using ClassLibrary_Core.Mission;
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

                // 3. 更新邻接关系（新增部分）
                // 3.1 移除所有无人机中指向离线无人机的连接
                foreach (var drone in _drones)
                {
                    drone.ConnectedDroneIds.RemoveAll(id => !newIds.Contains(id));
                }

                // 3.2 为新增无人机建立邻接关系（类似AddDrone中的逻辑）
                foreach (var drone in newList.Where(d => !_lastDrones.Any(ld => ld.Id == d.Id)))
                {
                    var nearbyDrones = newList
                        .Where(d =>
                            d.Id != drone.Id &&
                            d.Status != DroneStatus.Offline &&
                            d.CurrentPosition.DistanceTo(drone.CurrentPosition) <= drone.radius)
                        .Select(d => d.Id)
                        .ToList();

                    drone.ConnectedDroneIds = new List<int>(nearbyDrones);

                    foreach (var nearbyId in nearbyDrones)
                    {
                        var nearbyDrone = newList.First(d => d.Id == nearbyId);
                        if (!nearbyDrone.ConnectedDroneIds.Contains(drone.Id))
                        {
                            nearbyDrone.ConnectedDroneIds.Add(drone.Id);
                        }
                    }
                }
                // 4. 合并：本次数据 + 丢失的无人机（状态已设为Offline）
                _drones.Clear();
                _drones.AddRange(newList);
                _drones.AddRange(lostDrones);

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
            lock (_lock)
            {
                var existingDrone = _drones.FirstOrDefault(d => d.Name == drone.Name);
                if (existingDrone != null)
                {
                    // 保持相同ID
                    drone.Id = existingDrone.Id;
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
                var idx = _drones.FindIndex(d => d.Id == drone.Id);
                if (idx >= 0)
                {
                    _drones[idx] = drone;
                    if(drone.Status==DroneStatus.Offline)
                    {
                        // 如果状态是Offline，清除子任务
                        _drones[idx].AssignedSubTasks.Clear();
                        OnDroneChanged("Delete", drone);
                    }
                   return true;
                }
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
        public List<SubTask> GetSubTasks(int droneId)
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
        public SubTask? GetSubTask(int droneId, Guid subTaskId)
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
        public bool AddSubTask(int droneId, SubTask subTask)
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
        public bool UpdateSubTask(int droneId, SubTask subTask)
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
        public bool RemoveSubTask(int droneId, Guid subTaskId)
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