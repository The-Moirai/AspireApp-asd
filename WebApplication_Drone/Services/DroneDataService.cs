using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Data;
using ClassLibrary_Core.Mission;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace WebApplication_Drone.Services
{
    /// <summary>
    /// 无人机服务配置选项
    /// </summary>
    public class DroneServiceOptions
    {
        public int MaxRetryAttempts { get; set; } = 3;
        public int CacheExpirationMinutes { get; set; } = 10;
        public int MaxConcurrentOperations { get; set; } = 10;
        public bool EnableRealTimeUpdates { get; set; } = true;
        public int PerformanceMonitoringInterval { get; set; } = 300;
    }

    /// <summary>
    /// 无人机服务统计信息
    /// </summary>
    public class DroneServiceStatistics
    {
        public int TotalDrones { get; set; }
        public int OnlineDrones { get; set; }
        public int OfflineDrones { get; set; }
        public long TotalOperations { get; set; }
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public double CacheHitRate { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }

    /// <summary>
    /// 无人机数据服务 - 优化版本
    /// </summary>
    public class DroneDataService : IDisposable
    {
        private readonly ConcurrentDictionary<Guid, Drone> _drones = new();
        private readonly ConcurrentDictionary<string, Guid> _droneNameMapping = new();
        private readonly ReaderWriterLockSlim _rwLock = new();
        private readonly SqlserverService _sqlserverService;
        private readonly ILogger<DroneDataService> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly DroneServiceOptions _options;
        private long _totalOperations = 0;
        private long _cacheHits = 0;
        private long _cacheMisses = 0;
        private const string CACHE_KEY_PREFIX = "drone:";
        private const string CACHE_KEY_ALL_DRONES = "drones:all";
        private const string CACHE_KEY_DRONE_STATUS = "drones:status:";
        private bool _disposed = false;

        public DroneDataService(
            SqlserverService sqlserverService, 
            ILogger<DroneDataService> logger,
            IMemoryCache memoryCache,
            IOptions<DroneServiceOptions> options) 
        {
            _sqlserverService = sqlserverService ?? throw new ArgumentNullException(nameof(sqlserverService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public event EventHandler<DroneChangedEventArgs>? DroneChanged;

        protected virtual void OnDroneChanged(string action, Drone drone)
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
                _logger.LogError(ex, "Error invoking DroneChanged event: {Message}", ex.Message);
            }
        }

        public async Task<List<Drone>> GetDronesAsync()
        {
            Interlocked.Increment(ref _totalOperations);

            try
            {
                if (_memoryCache.TryGetValue(CACHE_KEY_ALL_DRONES, out List<Drone>? cachedDrones))
                {
                    Interlocked.Increment(ref _cacheHits);
                    return cachedDrones ?? new List<Drone>();
                }

                Interlocked.Increment(ref _cacheMisses);

                var drones = await Task.Run(() =>
                {
                    _rwLock.EnterReadLock();
                    try
                    {
                        return _drones.Values.Select(CloneDrone).ToList();
                    }
                    finally
                    {
                        _rwLock.ExitReadLock();
                    }
                });

                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheExpirationMinutes),
                    SlidingExpiration = TimeSpan.FromMinutes(_options.CacheExpirationMinutes / 2)
                };
                _memoryCache.Set(CACHE_KEY_ALL_DRONES, drones, cacheOptions);

                return drones;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting drones: {Message}", ex.Message);
                return new List<Drone>();
            }
        }

        public async Task<Drone?> GetDroneAsync(Guid id)
        {
            Interlocked.Increment(ref _totalOperations);

            try
            {
                var cacheKey = CACHE_KEY_PREFIX + id.ToString();
                
                if (_memoryCache.TryGetValue(cacheKey, out Drone? cachedDrone))
                {
                    Interlocked.Increment(ref _cacheHits);
                    return cachedDrone;
                }

                Interlocked.Increment(ref _cacheMisses);

                var drone = await Task.Run(() =>
                {
                    _rwLock.EnterReadLock();
                    try
                    {
                        return _drones.TryGetValue(id, out var d) ? CloneDrone(d) : null;
                    }
                    finally
                    {
                        _rwLock.ExitReadLock();
                    }
                });

                if (drone != null)
                {
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheExpirationMinutes),
                        SlidingExpiration = TimeSpan.FromMinutes(_options.CacheExpirationMinutes / 2)
                    };
                    _memoryCache.Set(cacheKey, drone, cacheOptions);
                }

                return drone;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting drone {DroneId}: {Message}", id, ex.Message);
                return null;
            }
        }

        public async Task<Drone?> GetDroneByNameAsync(string droneName)
        {
            if (string.IsNullOrWhiteSpace(droneName)) return null;

            try
            {
                if (_droneNameMapping.TryGetValue(droneName, out var droneId))
                {
                    return await GetDroneAsync(droneId);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting drone by name {DroneName}: {Message}", droneName, ex.Message);
                return null;
            }
        }

        public async Task SetDronesAsync(IEnumerable<Drone> drones)
        {
            if (drones == null) return;

            var droneList = drones.ToList();
            if (!droneList.Any()) return;

            try
            {
                _logger.LogInformation("开始批量更新 {Count} 个无人机数据", droneList.Count);

                using var semaphore = new SemaphoreSlim(_options.MaxConcurrentOperations);
                var updatedDrones = new ConcurrentBag<Drone>();
                var addedDrones = new ConcurrentBag<Drone>();

                var tasks = droneList.Select(async drone =>
                {
                    await ProcessDroneUpdateAsync(drone, updatedDrones, addedDrones, semaphore);
                });

                await Task.WhenAll(tasks);

                var activeDrones = droneList.Where(d => d.Status != DroneStatus.Offline).ToList();
                await UpdateDroneConnectionsAsync(activeDrones);

                var allDrones = updatedDrones.Concat(addedDrones);
                await SyncDronesToDatabaseAsync(allDrones);

                await InvalidateAllCachesAsync();

                _logger.LogInformation("批量更新完成 - 更新: {UpdatedCount}, 新增: {AddedCount}", 
                    updatedDrones.Count, addedDrones.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量更新无人机数据失败: {Message}", ex.Message);
            }
        }

        private async Task ProcessDroneUpdateAsync(
            Drone newDrone, 
            ConcurrentBag<Drone> updatedDrones, 
            ConcurrentBag<Drone> addedDrones,
            SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            try
            {
                await Task.Run(() =>
                {
                    _rwLock.EnterWriteLock();
                    try
                    {
                        if (_drones.TryGetValue(newDrone.Id, out var existingDrone))
                        {
                            var updatedDrone = UpdateDroneData(existingDrone, newDrone);
                            _drones[newDrone.Id] = updatedDrone;
                            _droneNameMapping[updatedDrone.Name] = updatedDrone.Id;
                            updatedDrones.Add(updatedDrone);
                            OnDroneChanged("Update", updatedDrone);
                        }
                        else
                        {
                            _drones[newDrone.Id] = newDrone;
                            _droneNameMapping[newDrone.Name] = newDrone.Id;
                            addedDrones.Add(newDrone);
                            OnDroneChanged("Add", newDrone);
                        }
                    }
                    finally
                    {
                        _rwLock.ExitWriteLock();
                    }
                });
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task UpdateDroneConnectionsAsync(List<Drone> activeDrones)
        {
            await Task.Run(() =>
            {
                _rwLock.EnterWriteLock();
                try
                {
                    foreach (var drone in _drones.Values)
                    {
                        drone.ConnectedDroneIds.Clear();
                    }

                    foreach (var drone in activeDrones)
                    {
                        if (_drones.TryGetValue(drone.Id, out var currentDrone))
                        {
                            currentDrone.ConnectedDroneIds = activeDrones
                                .Where(d => d.Id != drone.Id && 
                                       CalculateDistance(d.CurrentPosition, drone.CurrentPosition) <= drone.radius)
                                .Select(d => d.Id)
                                .ToList();
                        }
                    }
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
            });
        }

        private double CalculateDistance(ClassLibrary_Core.Common.GPSPosition pos1, ClassLibrary_Core.Common.GPSPosition pos2)
        {
            if (pos1 == null || pos2 == null) return double.MaxValue;
            return pos1.DistanceTo(pos2);
        }

        private async Task SyncDronesToDatabaseAsync(IEnumerable<Drone> drones)
        {
            try
            {
                var tasks = drones.Select(async drone =>
                {
                    try
                    {
                        await _sqlserverService.FullSyncDroneAsync(drone);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "同步无人机到数据库失败 - DroneId: {DroneId}, Name: {Name}", 
                            drone.Id, drone.Name);
                    }
                });
                
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量同步无人机到数据库失败: {Message}", ex.Message);
            }
        }

        private async Task InvalidateAllCachesAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    _memoryCache.Remove(CACHE_KEY_ALL_DRONES);
                    
                    foreach (DroneStatus status in Enum.GetValues<DroneStatus>())
                    {
                        _memoryCache.Remove(CACHE_KEY_DRONE_STATUS + status);
                    }
                    
                    foreach (var droneId in _drones.Keys)
                    {
                        _memoryCache.Remove(CACHE_KEY_PREFIX + droneId.ToString());
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "清理缓存失败: {Message}", ex.Message);
                }
            });
        }

        public DroneServiceStatistics GetStatistics()
        {
            return new DroneServiceStatistics
            {
                TotalDrones = _drones.Count,
                TotalOperations = _totalOperations,
                CacheHitRate = _totalOperations > 0 ? (double)_cacheHits / _totalOperations : 0,
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses,
                OnlineDrones = _drones.Values.Count(d => d.Status != DroneStatus.Offline),
                OfflineDrones = _drones.Values.Count(d => d.Status == DroneStatus.Offline),
                LastUpdateTime = DateTime.UtcNow
            };
        }

        public List<Drone> GetDrones()
        {
            return GetDronesAsync().GetAwaiter().GetResult();
        }

        public Drone? GetDrone(Guid id)
        {
            return GetDroneAsync(id).GetAwaiter().GetResult();
        }

        public async void SetDrones(IEnumerable<Drone> drones)
        {
            await SetDronesAsync(drones);
        }

        private Drone UpdateDroneData(Drone existingDrone, Drone newDrone)
        {
            var updatedDrone = CloneDrone(existingDrone);
            
            updatedDrone.ModelStatus = newDrone.ModelStatus;
            updatedDrone.CurrentPosition = newDrone.CurrentPosition;
            updatedDrone.Status = newDrone.Status;
            updatedDrone.cpu_used_rate = newDrone.cpu_used_rate;
            updatedDrone.radius = newDrone.radius;
            updatedDrone.left_bandwidth = newDrone.left_bandwidth;
            updatedDrone.memory = newDrone.memory;
            
            if (newDrone.Status == DroneStatus.Offline)
            {
                updatedDrone.AssignedSubTasks.Clear();
            }

            return updatedDrone;
        }

        private Drone CloneDrone(Drone d)
        {
            return new Drone
            {
                Id = d.Id,
                Name = d.Name,
                ModelStatus = d.ModelStatus,
                CurrentPosition = new ClassLibrary_Core.Common.GPSPosition(d.CurrentPosition.Latitude_x, d.CurrentPosition.Longitude_y),
                Status = d.Status,
                cpu_used_rate = d.cpu_used_rate,
                radius = d.radius,
                left_bandwidth = d.left_bandwidth,
                memory = d.memory,
                ConnectedDroneIds = new List<Guid>(d.ConnectedDroneIds),
                AssignedSubTasks = new List<SubTask>(d.AssignedSubTasks)
            };
        }

        public async Task LoadDronesFromDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("开始从数据库加载无人机数据");
                var drones = await _sqlserverService.GetAllDronesAsync();
                
                _rwLock.EnterWriteLock();
                try
                {
                    _drones.Clear();
                    _droneNameMapping.Clear();
                    
                    foreach (var drone in drones)
                    {
                        _drones[drone.Id] = drone;
                        _droneNameMapping[drone.Name] = drone.Id;
                    }
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
                
                await InvalidateAllCachesAsync();
                _logger.LogInformation("从数据库加载了 {Count} 个无人机", drones.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从数据库加载无人机数据失败: {Message}", ex.Message);
            }
        }

        public async Task LoadDronesFromDatabaseBatchAsync(int pageSize = 100, int maxConcurrency = 5)
        {
            try
            {
                _logger.LogInformation("开始分批从数据库加载无人机数据 - PageSize: {PageSize}, MaxConcurrency: {MaxConcurrency}", 
                    pageSize, maxConcurrency);

                var totalCount = await _sqlserverService.GetDroneCountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                using var semaphore = new SemaphoreSlim(maxConcurrency);
                var tasks = Enumerable.Range(0, totalPages).Select(async page =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var drones = await _sqlserverService.GetDronesByPageAsync(page, pageSize);
                        
                        _rwLock.EnterWriteLock();
                        try
                        {
                            foreach (var drone in drones)
                            {
                                _drones[drone.Id] = drone;
                                _droneNameMapping[drone.Name] = drone.Id;
                            }
                        }
                        finally
                        {
                            _rwLock.ExitWriteLock();
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
                await InvalidateAllCachesAsync();
                
                _logger.LogInformation("分批加载完成 - 总计: {TotalCount} 个无人机", totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "分批加载无人机数据失败: {Message}", ex.Message);
            }
        }

        public async Task<List<DroneDataPoint>> GetRecentDroneDataAsync(Guid droneId, TimeSpan duration)
        {
            try
            {
                return await _sqlserverService.GetDroneStatusHistoryAsync(droneId, DateTime.Now - duration, DateTime.Now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取最近无人机数据失败 - DroneId: {DroneId}: {Message}", droneId, ex.Message);
                return new List<DroneDataPoint>();
            }
        }

        public async Task<List<DroneDataPoint>> GetDroneTaskDataAsync(Guid droneId, Guid taskId)
        {
            try
            {
                var taskTimeRange = await _sqlserverService.GetTaskTimeRangeAsync(taskId);
                if (taskTimeRange == null)
                {
                    return new List<DroneDataPoint>();
                }

                return await _sqlserverService.GetDroneDataInTimeRangeAsync(
                    droneId,
                    taskTimeRange.StartTime,
                    taskTimeRange.EndTime
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取无人机任务数据失败 - DroneId: {DroneId}, TaskId: {TaskId}: {Message}", 
                    droneId, taskId, ex.Message);
                return new List<DroneDataPoint>();
            }
        }

        public async Task<List<DroneDataPoint>> GetAllDronesDataInTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            try
            {
                return await _sqlserverService.GetAllDronesDataInTimeRangeAsync(startTime, endTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取时间范围内无人机数据失败: {Message}", ex.Message);
                return new List<DroneDataPoint>();
            }
        }

        public async Task AddDroneAsync(Drone drone)
        {
            if (drone == null) return;

            try
            {
                _rwLock.EnterWriteLock();
                try
                {
                    if (drone.Id == Guid.Empty)
                    {
                        drone.Id = Guid.NewGuid();
                    }

                    _drones[drone.Id] = drone;
                    _droneNameMapping[drone.Name] = drone.Id;
                    OnDroneChanged("Add", drone);
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }

                await InvalidateAllCachesAsync();
                await _sqlserverService.FullSyncDroneAsync(drone);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加无人机失败 - DroneId: {DroneId}: {Message}", drone.Id, ex.Message);
            }
        }

        public void AddDrone(Drone drone)
        {
            _ = Task.Run(async () => await AddDroneAsync(drone));
        }

        public async Task<bool> UpdateDroneAsync(Drone drone)
        {
            if (drone == null) return false;

            try
            {
                _rwLock.EnterWriteLock();
                try
                {
                    if (!_drones.ContainsKey(drone.Id))
                    {
                        return false;
                    }

                    _drones[drone.Id] = drone;
                    _droneNameMapping[drone.Name] = drone.Id;
                    OnDroneChanged("Update", drone);
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }

                await InvalidateAllCachesAsync();
                await _sqlserverService.FullSyncDroneAsync(drone);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新无人机失败 - DroneId: {DroneId}: {Message}", drone.Id, ex.Message);
                return false;
            }
        }

        public bool UpdateDrone(Drone drone)
        {
            return UpdateDroneAsync(drone).GetAwaiter().GetResult();
        }

        public async Task<bool> DeleteDroneAsync(Guid id)
        {
            try
            {
                _rwLock.EnterWriteLock();
                try
                {
                    if (!_drones.TryGetValue(id, out var drone))
                    {
                        return false;
                    }

                    _drones.TryRemove(id, out _);
                    _droneNameMapping.TryRemove(drone.Name, out _);
                    OnDroneChanged("Delete", drone);
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }

                await InvalidateAllCachesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除无人机失败 - DroneId: {DroneId}: {Message}", id, ex.Message);
                return false;
            }
        }

        public bool DeleteDrone(Guid id)
        {
            return DeleteDroneAsync(id).GetAwaiter().GetResult();
        }

        public List<SubTask> GetSubTasks(Guid droneId)
        {
            try
            {
                _rwLock.EnterReadLock();
                try
                {
                    if (_drones.TryGetValue(droneId, out var drone))
                    {
                        return new List<SubTask>(drone.AssignedSubTasks);
                    }
                    return new List<SubTask>();
                }
                finally
                {
                    _rwLock.ExitReadLock();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取无人机子任务失败 - DroneId: {DroneId}: {Message}", droneId, ex.Message);
                return new List<SubTask>();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        _rwLock?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "释放读写锁时发生错误: {Message}", ex.Message);
                    }
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class DroneChangedEventArgs : EventArgs
    {
        public string Action { get; set; } = "";
        public Drone Drone { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }
} 