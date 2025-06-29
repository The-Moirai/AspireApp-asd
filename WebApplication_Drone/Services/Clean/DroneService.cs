using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebApplication_Drone.Services.Interfaces;
using WebApplication_Drone.Services.Models;
using System.Collections.Concurrent;
using ClassLibrary_Core.Mission;
using ClassLibrary_Core.Common;

namespace WebApplication_Drone.Services.Clean
{
    /// <summary>
    /// 无人机服务 - 清理后的版本，只负责业务逻辑和缓存
    /// </summary>
    public class DroneService : IDisposable
    {
        private readonly IDroneRepository _droneRepository;
        private readonly ILogger<DroneService> _logger;
        private readonly RedisCacheService _cacheService;
        private readonly DataServiceOptions _options;
        
        // 内存缓存
        private readonly ConcurrentDictionary<Guid, Drone> _drones = new();
        private readonly ConcurrentDictionary<string, Guid> _droneNameMapping = new();
        
        // 性能计数器
        private long _totalOperations = 0;
        private long _cacheHits = 0;
        private long _cacheMisses = 0;
        
        // 实时数据统计
        private DateTime _lastRealTimeUpdate = DateTime.MinValue;
        private int _realTimeRequestCount = 0;
        private int _realTimeSuccessCount = 0;
        
        // 缓存键
        private const string CACHE_KEY_ALL_DRONES = "drones:all";
        private const string CACHE_KEY_PREFIX = "drone:";
        
        // 事件
        public event EventHandler<DroneChangedEventArgs>? DroneChanged;
        
        public DroneService(
            IDroneRepository droneRepository,
            ILogger<DroneService> logger,
            RedisCacheService cacheService,
            IOptions<DataServiceOptions> options)
        {
            _droneRepository = droneRepository ?? throw new ArgumentNullException(nameof(droneRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }
        
        #region 实时数据获取（直接从内存缓存）
        
        /// <summary>
        /// 获取实时无人机数据 - 直接从内存缓存获取，不依赖数据库
        /// </summary>
        public async Task<List<Drone>> GetRealTimeDronesAsync()
        {
            try
            {
                Interlocked.Increment(ref _realTimeRequestCount);
                
                // 直接从内存缓存获取最新数据
                var drones = _drones.Values.Select(CloneDrone).ToList();
                
                Interlocked.Increment(ref _realTimeSuccessCount);
                _lastRealTimeUpdate = DateTime.UtcNow;
                
                _logger.LogDebug("获取实时无人机数据成功，数量: {Count}", drones.Count);
                
                return drones;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取实时无人机数据失败");
                throw;
            }
        }
        
        /// <summary>
        /// 获取指定无人机的实时数据
        /// </summary>
        public async Task<Drone?> GetRealTimeDroneAsync(Guid droneId)
        {
            try
            {
                // 直接从内存缓存获取
                var drone = _drones.TryGetValue(droneId, out var d) ? CloneDrone(d) : null;
                
                if (drone != null)
                {
                    _logger.LogDebug("获取无人机实时数据成功: {DroneName}", drone.Name);
                }
                
                return drone;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取无人机实时数据失败: {DroneId}", droneId);
                throw;
            }
        }
        
        /// <summary>
        /// 根据名称获取无人机实时数据
        /// </summary>
        public async Task<Drone?> GetRealTimeDroneByNameAsync(string droneName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(droneName)) return null;
                
                // 从内存映射获取
                if (_droneNameMapping.TryGetValue(droneName, out var droneId))
                {
                    return await GetRealTimeDroneAsync(droneId);
                }
                
                // 如果映射中没有，遍历内存缓存查找
                var drone = _drones.Values.FirstOrDefault(d => d.Name == droneName);
                return drone != null ? CloneDrone(drone) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "根据名称获取无人机实时数据失败: {DroneName}", droneName);
                throw;
            }
        }
        
        /// <summary>
        /// 检查实时数据新鲜度
        /// </summary>
        public bool IsRealTimeDataFresh(TimeSpan maxAge = default)
        {
            if (maxAge == default)
                maxAge = TimeSpan.FromSeconds(30); // 默认30秒

            var dataAge = DateTime.UtcNow - _lastRealTimeUpdate;
            return dataAge <= maxAge;
        }
        
        /// <summary>
        /// 获取实时数据统计信息
        /// </summary>
        public RealTimeDataStatistics GetRealTimeStatistics()
        {
            return new RealTimeDataStatistics
            {
                TotalDrones = _drones.Count,
                LastUpdate = _lastRealTimeUpdate,
                RequestCount = _realTimeRequestCount,
                SuccessCount = _realTimeSuccessCount,
                SuccessRate = _realTimeRequestCount > 0 ? (double)_realTimeSuccessCount / _realTimeRequestCount : 0,
                DataFreshnessSeconds = DateTime.UtcNow - _lastRealTimeUpdate
            };
        }
        
        #endregion
        
        #region 无人机操作
        
        public async Task<List<Drone>> GetDronesAsync()
        {
            Interlocked.Increment(ref _totalOperations);
            
            // 尝试从Redis缓存获取
            var cachedDrones = await _cacheService.GetAsync<List<Drone>>(CACHE_KEY_ALL_DRONES);
            if (cachedDrones != null)
            {
                Interlocked.Increment(ref _cacheHits);
                return cachedDrones;
            }
            
            Interlocked.Increment(ref _cacheMisses);
            
            // 从内存获取
            var drones = _drones.Values.Select(CloneDrone).ToList();
            
            // 如果内存为空，从数据库加载
            if (!drones.Any())
            {
                drones = await _droneRepository.GetAllAsync();
                foreach (var drone in drones)
                {
                    _drones.TryAdd(drone.Id, CloneDrone(drone));
                    _droneNameMapping.TryAdd(drone.Name, drone.Id);
                }
            }
            
            // 更新Redis缓存
            await _cacheService.SetAsync(CACHE_KEY_ALL_DRONES, drones, 
                TimeSpan.FromMinutes(_options.CacheExpirationMinutes));
            
            return drones;
        }
        
        /// <summary>
        /// 获取所有无人机（GetDronesAsync的别名，保持向后兼容）
        /// </summary>
        public async Task<List<Drone>> GetAllDronesAsync()
        {
            return await GetDronesAsync();
        }
        
        public async Task<Drone?> GetDroneAsync(Guid id)
        {
            Interlocked.Increment(ref _totalOperations);
            
            var cacheKey = $"{CACHE_KEY_PREFIX}{id}";
            var cachedDrone = await _cacheService.GetAsync<Drone>(cacheKey);
            if (cachedDrone != null)
            {
                Interlocked.Increment(ref _cacheHits);
                return cachedDrone;
            }
            
            Interlocked.Increment(ref _cacheMisses);
            
            // 从内存获取
            var drone = _drones.TryGetValue(id, out var d) ? CloneDrone(d) : null;
            
            // 如果内存中没有，从数据库获取
            if (drone == null)
            {
                drone = await _droneRepository.GetByIdAsync(id);
                if (drone != null)
                {
                    _drones.TryAdd(drone.Id, CloneDrone(drone));
                    _droneNameMapping.TryAdd(drone.Name, drone.Id);
                }
            }
            
            if (drone != null)
            {
                await _cacheService.SetAsync(cacheKey, drone, 
                    TimeSpan.FromMinutes(_options.CacheExpirationMinutes));
            }
            
            return drone;
        }
        
        public async Task<Drone?> GetDroneByNameAsync(string droneName)
        {
            if (string.IsNullOrWhiteSpace(droneName)) return null;
            
            // 从内存映射获取
            if (_droneNameMapping.TryGetValue(droneName, out var droneId))
            {
                return await GetDroneAsync(droneId);
            }
            
            // 从数据库获取
            var drone = await _droneRepository.GetByNameAsync(droneName);
            if (drone != null)
            {
                _drones.TryAdd(drone.Id, CloneDrone(drone));
                _droneNameMapping.TryAdd(drone.Name, drone.Id);
            }
            
            return drone;
        }
        
        public async Task<bool> AddDroneAsync(Drone drone)
        {
            if (_drones.TryAdd(drone.Id, CloneDrone(drone)))
            {
                _droneNameMapping.TryAdd(drone.Name, drone.Id);
                
                // 保存到数据库
                await _droneRepository.AddAsync(drone);
                
                // 清除缓存
                await _cacheService.RemoveAsync(CACHE_KEY_ALL_DRONES);
                
                // 触发事件
                OnDroneChanged("Added", drone);
                
                return true;
            }
            return false;
        }
        
        public async Task<bool> UpdateDroneAsync(Drone drone)
        {
            if (_drones.TryGetValue(drone.Id, out var existing))
            {
                var updated = UpdateDroneData(existing, drone);
                _drones[drone.Id] = updated;
                _droneNameMapping[drone.Name] = drone.Id;
                
                // 更新数据库
                await _droneRepository.UpdateAsync(updated);
                
                // 清除缓存
                await _cacheService.RemoveAsync(CACHE_KEY_ALL_DRONES);
                await _cacheService.RemoveAsync($"{CACHE_KEY_PREFIX}{drone.Id}");
                
                // 触发事件
                OnDroneChanged("Updated", updated);
                
                return true;
            }
            return false;
        }
        
        public async Task<bool> DeleteDroneAsync(Guid id)
        {
            if (_drones.TryRemove(id, out var drone))
            {
                _droneNameMapping.TryRemove(drone.Name, out _);
                
                // 从数据库删除
                await _droneRepository.DeleteAsync(id);
                
                // 清除缓存
                await _cacheService.RemoveAsync(CACHE_KEY_ALL_DRONES);
                await _cacheService.RemoveAsync($"{CACHE_KEY_PREFIX}{id}");
                
                // 触发事件
                OnDroneChanged("Deleted", drone);
                
                return true;
            }
            return false;
        }
        
        public async Task<int> GetDroneCountAsync()
        {
            return _drones.Count;
        }
        
        #endregion
        
        #region 数据点操作
        
        public async Task<List<DroneDataPoint>> GetDroneDataAsync(Guid droneId, DateTime startTime, DateTime endTime)
        {
            return await _droneRepository.GetDataInTimeRangeAsync(droneId, startTime, endTime);
        }
        
        public async Task<List<DroneDataPoint>> GetAllDronesDataAsync(DateTime startTime, DateTime endTime)
        {
            return await _droneRepository.GetAllDronesDataInTimeRangeAsync(startTime, endTime);
        }
        
        #endregion
        
        #region 批量操作
        
        public async Task<bool> BulkUpdateDronesAsync(IEnumerable<Drone> drones)
        {
            // 更新数据库
            await _droneRepository.BulkUpdateAsync(drones);
            
            // 更新内存
            foreach (var drone in drones)
            {
                _drones.AddOrUpdate(drone.Id, CloneDrone(drone), (_, _) => CloneDrone(drone));
                _droneNameMapping.AddOrUpdate(drone.Name, drone.Id, (_, _) => drone.Id);
            }
            
            // 清除缓存
            await _cacheService.RemoveAsync(CACHE_KEY_ALL_DRONES);
            
            return true;
        }
        
        public async Task<bool> BulkRecordDroneStatusAsync(IEnumerable<Drone> drones)
        {
            return await _droneRepository.BulkRecordStatusAsync(drones);
        }
        
        #endregion
        
        #region 统计信息
        
        public DataServiceStatistics GetStatistics()
        {
            return new DataServiceStatistics
            {
                TotalDrones = _drones.Count,
                OnlineDrones = _drones.Values.Count(d => d.Status == DroneStatus.Idle),
                TotalTasks = 0, // 任务统计在TaskService中
                ActiveTasks = 0,
                TotalOperations = _totalOperations,
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses,
                LastUpdateTime = DateTime.UtcNow,
                DatabaseConnected = true,
                MemoryUsageMB = GC.GetTotalMemory(false) / 1024 / 1024
            };
        }
        
        #endregion
        
        #region 私有方法
        
        private Drone CloneDrone(Drone drone) => new()
        {
            Id = drone.Id,
            Name = drone.Name,
            Status = drone.Status,
            ModelStatus = drone.ModelStatus,
            ModelType = drone.ModelType,
            CurrentPosition = drone.CurrentPosition != null ? new GPSPosition(drone.CurrentPosition.Latitude_x, drone.CurrentPosition.Longitude_y) : null,
            cpu_used_rate = drone.cpu_used_rate,
            memory = drone.memory,
            left_bandwidth = drone.left_bandwidth,
            AssignedSubTasks = drone.AssignedSubTasks?.ToList() ?? new List<SubTask>()
        };
        
        private Drone UpdateDroneData(Drone existing, Drone updated)
        {
            // 保持原有ID不变
            // existing.Id = updated.Id; // 不更新ID
            
            // 更新基本信息
            existing.Name = updated.Name;
            existing.Status = updated.Status;
            existing.ModelStatus = updated.ModelStatus;
            existing.ModelType = updated.ModelType;
            
            // 更新位置信息
            if (updated.CurrentPosition != null)
            {
                existing.CurrentPosition = new GPSPosition(updated.CurrentPosition.Latitude_x, updated.CurrentPosition.Longitude_y);
            }
            
            // 更新性能指标
            existing.cpu_used_rate = updated.cpu_used_rate;
            existing.memory = updated.memory;
            existing.left_bandwidth = updated.left_bandwidth;
            
            // 更新任务分配
            if (updated.AssignedSubTasks != null)
            {
                existing.AssignedSubTasks = updated.AssignedSubTasks.ToList();
            }
            
            return existing;
        }
        
        private void OnDroneChanged(string action, Drone drone)
        {
            try
            {
                DroneChanged?.Invoke(this, new DroneChangedEventArgs
                {
                    Action = action,
                    Drone = drone,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "触发无人机变更事件失败: {Action}", action);
            }
        }
        
        #endregion
        
        #region 兼容性方法
        
        /// <summary>
        /// 设置无人机列表（兼容性方法）- 使用drone.name作为区分依据
        /// </summary>
        public void SetDrones(List<Drone> drones)
        {
            _drones.Clear();
            _droneNameMapping.Clear();
            
            foreach (var drone in drones)
            {
                // 使用drone.name作为区分依据，查找是否已存在同名无人机
                if (_droneNameMapping.TryGetValue(drone.Name, out var existingId))
                {
                    // 如果已存在同名无人机，更新现有记录
                    if (_drones.TryGetValue(existingId, out var existingDrone))
                    {
                        // 更新现有无人机数据，保持原有ID
                        var updatedDrone = UpdateDroneData(existingDrone, drone);
                        _drones[existingId] = updatedDrone;
                        
                        _logger.LogDebug("更新现有无人机: {DroneName} (ID: {DroneId})", drone.Name, existingId);
                    }
                }
                else
                {
                    // 如果不存在同名无人机，添加新记录
                    _drones.TryAdd(drone.Id, CloneDrone(drone));
                    _droneNameMapping.TryAdd(drone.Name, drone.Id);
                    
                    _logger.LogDebug("添加新无人机: {DroneName} (ID: {DroneId})", drone.Name, drone.Id);
                }
            }
            
            // 清除缓存
            _ = Task.Run(async () => await _cacheService.RemoveAsync(CACHE_KEY_ALL_DRONES));
            
            _logger.LogInformation("设置无人机列表完成，总数: {TotalCount}, 内存缓存: {CacheCount}", 
                drones.Count, _drones.Count);
        }
        /// <summary>
        /// 获取无人机列表（同步版本，兼容性方法）
        /// </summary>
        public List<Drone> GetDrones()
        {
            return _drones.Values.Select(CloneDrone).ToList();
        }
        
        /// <summary>
        /// 获取无人机（同步版本，兼容性方法）
        /// </summary>
        public Drone? GetDrone(Guid id)
        {
            return _drones.TryGetValue(id, out var drone) ? CloneDrone(drone) : null;
        }
        
        /// <summary>
        /// 添加无人机（同步版本，兼容性方法）
        /// </summary>
        public void AddDrone(Drone drone)
        {
            _ = Task.Run(async () => await AddDroneAsync(drone));
        }
        
        /// <summary>
        /// 更新无人机（同步版本，兼容性方法）
        /// </summary>
        public bool UpdateDrone(Drone drone)
        {
            return Task.Run(async () => await UpdateDroneAsync(drone)).Result;
        }
        
        /// <summary>
        /// 删除无人机（同步版本，兼容性方法）
        /// </summary>
        public bool DeleteDrone(Guid id)
        {
            return Task.Run(async () => await DeleteDroneAsync(id)).Result;
        }
        
        /// <summary>
        /// 获取最近的无人机数据（兼容性方法）
        /// </summary>
        public async Task<List<DroneDataPoint>> GetRecentDroneDataAsync(Guid droneId, TimeSpan duration)
        {
            var endTime = DateTime.UtcNow;
            var startTime = endTime.Subtract(duration);
            return await GetDroneDataAsync(droneId, startTime, endTime);
        }
        
        /// <summary>
        /// 获取无人机任务数据（兼容性方法）
        /// </summary>
        public async Task<List<DroneDataPoint>> GetDroneTaskDataAsync(Guid droneId, Guid taskId)
        {
            // 这里需要实现具体的逻辑，暂时返回空列表
            _logger.LogInformation("获取无人机任务数据: DroneId={DroneId}, TaskId={TaskId}", droneId, taskId);
            return new List<DroneDataPoint>();
        }
        
        /// <summary>
        /// 获取子任务（兼容性方法）
        /// </summary>
        public List<SubTask> GetSubTasks(Guid droneId)
        {
            // 这里需要实现具体的逻辑，暂时返回空列表
            _logger.LogInformation("获取无人机子任务: DroneId={DroneId}", droneId);
            return new List<SubTask>();
        }
        
        /// <summary>
        /// 获取所有无人机在时间范围内的数据（兼容性方法）
        /// </summary>
        public async Task<List<DroneDataPoint>> GetAllDronesDataInTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            return await GetAllDronesDataAsync(startTime, endTime);
        }
        
        #endregion
        
        public void Dispose()
        {
            _drones.Clear();
            _droneNameMapping.Clear();
        }
    }
    
    // 事件参数类
    public class DroneChangedEventArgs : EventArgs
    {
        public string Action { get; set; } = "";
        public Drone Drone { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }
    
    /// <summary>
    /// 实时数据统计信息
    /// </summary>
    public class RealTimeDataStatistics
    {
        public int TotalDrones { get; set; }
        public DateTime LastUpdate { get; set; }
        public int RequestCount { get; set; }
        public int SuccessCount { get; set; }
        public double SuccessRate { get; set; }
        public TimeSpan DataFreshnessSeconds { get; set; }
    }
} 