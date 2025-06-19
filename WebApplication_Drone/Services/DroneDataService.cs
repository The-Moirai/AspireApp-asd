using ClassLibrary_Core.Data;
using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Message;
using ClassLibrary_Core.Mission;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
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
        /// 日志记录器
        /// </summary>
        private readonly ILogger<DroneDataService> _logger;
        /// <summary>
        /// 无人机数据变更事件
        /// </summary>
        public event EventHandler<DroneChangedEventArgs>? DroneChanged;

        public DroneDataService(SqlserverService sqlserverService, ILogger<DroneDataService> logger) 
        {
            _sqlserverService = sqlserverService;
            _logger = logger;
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
        /// 设置无人机数据，智能比较并更新无人机列表
        /// 1. 新列表中存在但当前列表中不存在的 -> 添加新无人机
        /// 2. 当前列表中存在但新列表中不存在的 -> 设为离线状态
        /// 3. 新旧列表都存在的 -> 更新数据（除了Id和Name）
        /// </summary>
        /// <param name="drones">新的无人机数据列表</param>
        public async void SetDrones(IEnumerable<Drone> drones)
        {
            lock (_lock)
            {
                var newDrones = drones.ToList();
                
                // 创建名称到无人机的映射（用于匹配）
                var newDronesByName = newDrones.ToDictionary(d => d.Name, d => d);
                var currentDronesByName = _drones.ToDictionary(d => d.Name, d => d);

                var updatedDrones = new List<Drone>();
                var addedDrones = new List<Drone>();
                var offlineDrones = new List<Drone>();

                // 1. 处理新列表中的无人机
                foreach (var newDrone in newDrones)
                {
                    if (currentDronesByName.TryGetValue(newDrone.Name, out var existingDrone))
                    {
                        // 存在于当前列表中 -> 更新数据（保持原有的Id和Name）
                        var updatedDrone = UpdateDroneData(existingDrone, newDrone);
                        updatedDrones.Add(updatedDrone);
                        OnDroneChanged("Update", updatedDrone);
                    }
                    else
                    {
                        // 新增的无人机 -> 生成新的ID
                        newDrone.Id = Guid.NewGuid();
                        addedDrones.Add(CloneDrone(newDrone));
                        OnDroneChanged("Add", newDrone);
                    }
                }

                // 2. 处理当前列表中存在但新列表中不存在的无人机 -> 设为离线
                foreach (var currentDrone in _drones)
                {
                    if (!newDronesByName.ContainsKey(currentDrone.Name))
                    {
                        var offlineDrone = CloneDrone(currentDrone);
                        offlineDrone.Status = DroneStatus.Offline;
                        offlineDrone.AssignedSubTasks.Clear(); // 清空任务
                        // 断开所有连接
                        offlineDrone.ConnectedDroneIds.Clear();
                        offlineDrones.Add(offlineDrone);
                        OnDroneChanged("Offline", offlineDrone);
                    }
                }

                // 3. 更新邻接关系
                var allActiveDrones = updatedDrones.Concat(addedDrones).Where(d => d.Status != DroneStatus.Offline).ToList();
                UpdateDroneConnections(allActiveDrones);

                // 4. 更新内部列表
                _drones.Clear();
                _drones.AddRange(updatedDrones);
                _drones.AddRange(addedDrones);
                _drones.AddRange(offlineDrones);

                // 5. 更新快照（保存当前在线无人机）
                _lastDrones = _drones.Where(d => d.Status != DroneStatus.Offline).Select(CloneDrone).ToList();

                // 6. 异步同步到数据库 - 创建快照避免集合修改异常
                var dronesSnapshot = _drones.Select(CloneDrone).ToList();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // 完整同步所有无人机数据（使用快照）
                        foreach (var drone in dronesSnapshot)
                        {
                            await _sqlserverService.FullSyncDroneAsync(drone);
                        }
                    }
                    catch (Exception ex)
                    {
                        // 记录错误但不影响主流程
                        _logger.LogError(ex, "数据库同步错误: {Message}", ex.Message);
                    }
                });
            }
        }

        /// <summary>
        /// 更新无人机数据（除了Id和Name）
        /// </summary>
        /// <param name="existingDrone">现有无人机</param>
        /// <param name="newDrone">新数据无人机</param>
        /// <returns>更新后的无人机</returns>
        private Drone UpdateDroneData(Drone existingDrone, Drone newDrone)
        {
            var updatedDrone = CloneDrone(existingDrone);
            
            // 保持原有的Id和Name不变
            // updatedDrone.Id = existingDrone.Id;          // 保持不变
            // updatedDrone.Name = existingDrone.Name;      // 保持不变
            
            // 更新其他所有数据
            updatedDrone.ModelStatus = newDrone.ModelStatus;
            updatedDrone.CurrentPosition = newDrone.CurrentPosition;
            //updatedDrone.Status = newDrone.Status;
            updatedDrone.cpu_used_rate = newDrone.cpu_used_rate;
            updatedDrone.radius = newDrone.radius;
            updatedDrone.left_bandwidth = newDrone.left_bandwidth;
            updatedDrone.memory = newDrone.memory;
            
            // 连接关系将在UpdateDroneConnections中重新计算
            // 任务列表保持现有的，除非状态为离线
            if (newDrone.Status == DroneStatus.Offline)
            {
                updatedDrone.AssignedSubTasks.Clear();
            }

            return updatedDrone;
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
        /// 检查数据库中是否存在同名无人机，若存在则将无人机ID与数据库ID同步，其余属性以新无人机为准
        /// </summary>
        /// <param name="drone">无人机实体</param>
        public async void AddDrone(Drone drone)
        {
            // 首先检查数据库中是否存在同名无人机
            Drone? databaseDrone = null;
            try
            {
                databaseDrone = await _sqlserverService.GetDroneByNameAsync(drone.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking database for drone name {DroneName}: {Message}", drone.Name, ex.Message);
            }

            lock (_lock)
            {
                // 优先使用数据库中的ID，如果数据库中存在同名无人机
                if (databaseDrone != null)
                {
                    _logger.LogInformation("Found existing drone in database with name {DroneName}, using database ID {DatabaseId}", 
                        drone.Name, databaseDrone.Id);
                    drone.Id = databaseDrone.Id;
                }
                else
                {
                    // 如果数据库中不存在，检查内存中是否有同名无人机
                    var existing = _drones.FirstOrDefault(d => d.Name == drone.Name);
                    if (existing != null) 
                    {
                        drone.Id = existing.Id;
                        _logger.LogInformation("Found existing drone in memory with name {DroneName}, using memory ID {MemoryId}", 
                            drone.Name, existing.Id);
                    }
                    else
                    {
                        // 如果都不存在，生成新的ID
                        if (drone.Id == Guid.Empty)
                        {
                            drone.Id = Guid.NewGuid();
                            _logger.LogInformation("Generated new ID {NewId} for drone {DroneName}", drone.Id, drone.Name);
                        }
                    }
                }

                // 检查是否已经在内存列表中存在相同ID的无人机
                var existingById = _drones.FirstOrDefault(d => d.Id == drone.Id);
                if (existingById != null)
                {
                    // 如果存在，更新现有无人机的属性（除了ID和Name）
                    var updatedDrone = UpdateDroneData(existingById, drone);
                    var index = _drones.FindIndex(d => d.Id == drone.Id);
                    _drones[index] = updatedDrone;
                    OnDroneChanged("Update", updatedDrone);
                    _logger.LogInformation("Updated existing drone {DroneId} ({DroneName}) with new data", drone.Id, drone.Name);
                    
                    // 异步同步到数据库
                    var droneSnapshot = CloneDrone(updatedDrone);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _sqlserverService.FullSyncDroneAsync(droneSnapshot);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Database sync error for updated drone {DroneId}: {Message}", drone.Id, ex.Message);
                        }
                    });
                }
                else
                {
                    // 如果不存在，添加新无人机
                    // 建立新连接
                    var nearby = _drones
                        .Where(d => d.Status != DroneStatus.Offline &&
                            d.CurrentPosition.DistanceTo(drone.CurrentPosition) <= drone.radius)
                        .ToList();

                    drone.ConnectedDroneIds = nearby.Select(d => d.Id).ToList();
                    foreach (var neighbor in nearby)
                        neighbor.ConnectedDroneIds.Add(drone.Id);

                    _drones.Add(drone);
                    OnDroneChanged("Add", drone);
                    _logger.LogInformation("Added new drone {DroneId} ({DroneName}) to memory", drone.Id, drone.Name);

                    // 异步同步到数据库 - 创建快照避免集合修改异常
                    var droneSnapshot = CloneDrone(drone);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _sqlserverService.FullSyncDroneAsync(droneSnapshot);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Database sync error for new drone {DroneId}: {Message}", drone.Id, ex.Message);
                        }
                    });
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
                    OnDroneChanged("Delete", drone);
                }

                OnDroneChanged("Update", drone);

                // 异步同步到数据库 - 创建快照避免集合修改异常
                var droneSnapshot = CloneDrone(drone);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _sqlserverService.FullSyncDroneAsync(droneSnapshot);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Sql Server Wrong: {Message}", ex.Message);
                    }
                });

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
            return await _sqlserverService.GetAllDronesDataInTimeRangeAsync(startTime, endTime);
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