using ClassLibrary_Core.Data;
using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Mission;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace WebApplication_Drone.Services
{
    /// <summary>
    /// 任务服务配置选项
    /// </summary>
    public class TaskServiceOptions
    {
        /// <summary>最大重试次数</summary>
        public int MaxRetryAttempts { get; set; } = 3;
        
        /// <summary>缓存过期时间(分钟)</summary>
        public int CacheExpirationMinutes { get; set; } = 10;
        
        /// <summary>最大并发操作数</summary>
        public int MaxConcurrentOperations { get; set; } = 10;
        
        /// <summary>启用实时更新</summary>
        public bool EnableRealTimeUpdates { get; set; } = true;
        
        /// <summary>批处理大小</summary>
        public int BatchSize { get; set; } = 50;
    }

    /// <summary>
    /// 任务服务统计信息
    /// </summary>
    public class TaskServiceStatistics
    {
        /// <summary>总任务数量</summary>
        public int TotalTasks { get; set; }
        
        /// <summary>活跃任务数量</summary>
        public int ActiveTasks { get; set; }
        
        /// <summary>已完成任务数量</summary>
        public int CompletedTasks { get; set; }
        
        /// <summary>总操作次数</summary>
        public long TotalOperations { get; set; }
        
        /// <summary>缓存命中次数</summary>
        public long CacheHits { get; set; }
        
        /// <summary>缓存未命中次数</summary>
        public long CacheMisses { get; set; }
        
        /// <summary>缓存命中率</summary>
        public double CacheHitRate { get; set; }
        
        /// <summary>最后更新时间</summary>
        public DateTime LastUpdateTime { get; set; }
    }

    /// <summary>
    /// 优化后的任务数据服务 - 使用缓存和并发集合提升性能
    /// </summary>
    public class TaskDataService : IDisposable
    {
        #region 私有字段

        /// <summary>
        /// 线程安全的任务数据字典 - 使用ConcurrentDictionary替代List+Lock
        /// </summary>
        private readonly ConcurrentDictionary<Guid, MainTask> _tasks = new();

        /// <summary>
        /// 读写锁 - 只在必要时使用
        /// </summary>
        private readonly ReaderWriterLockSlim _rwLock = new();
        
        /// <summary>
        /// 数据库服务
        /// </summary>
        private readonly SqlserverService _sqlserverService;
        
        /// <summary>
        /// 日志记录器
        /// </summary>
        private readonly ILogger<TaskDataService> _logger;
        
        /// <summary>
        /// 内存缓存
        /// </summary>
        private readonly IMemoryCache _memoryCache;
        
        /// <summary>
        /// 配置选项
        /// </summary>
        private readonly TaskServiceOptions _options;
        
        /// <summary>
        /// 性能计数器
        /// </summary>
        private long _totalOperations = 0;
        private long _cacheHits = 0;
        private long _cacheMisses = 0;
        
        /// <summary>
        /// 缓存键前缀
        /// </summary>
        private const string CACHE_KEY_PREFIX = "task:";
        private const string CACHE_KEY_ALL_TASKS = "tasks:all";
        private const string CACHE_KEY_TASK_STATUS = "tasks:status:";
        
        /// <summary>
        /// 释放标志
        /// </summary>
        private bool _disposed = false;

        #endregion

        #region 构造函数

        public TaskDataService(
            SqlserverService sqlserverService, 
            ILogger<TaskDataService> logger,
            IMemoryCache memoryCache,
            IOptions<TaskServiceOptions> options)
        {
            _sqlserverService = sqlserverService;
            _logger = logger;
            _memoryCache = memoryCache;
            _options = options.Value;
            
            _logger.LogInformation("📋 TaskDataService 初始化完成 - 使用优化架构");
        }

        #endregion

        #region 事件

        /// <summary>
        /// 任务数据变更事件
        /// </summary>
        public event EventHandler<TaskChangedEventArgs>? TaskChanged;

        /// <summary>
        /// 任务数据变更事件触发方法
        /// </summary>
        protected virtual void OnTaskChanged(string action, MainTask mainTask)
        {
            try
            {
                TaskChanged?.Invoke(this, new TaskChangedEventArgs
                {
                    Action = action,
                    MainTask = CloneTask(mainTask),
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "触发任务变更事件失败: {Action}, TaskId: {TaskId}", action, mainTask.Id);
            }
        }

        #endregion

        #region 核心CRUD操作 - 优化版本

        /// <summary>
        /// 获取所有任务数据 - 优先从缓存获取
        /// </summary>
        public async Task<List<MainTask>> GetTasksAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                // 尝试从缓存获取
                if (_memoryCache.TryGetValue(CACHE_KEY_ALL_TASKS, out List<MainTask>? cachedTasks))
                {
                    Interlocked.Increment(ref _cacheHits);
                    _logger.LogDebug("从缓存获取所有任务数据 - 耗时: {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                    return cachedTasks!;
                }

                Interlocked.Increment(ref _cacheMisses);
                
                // 从内存获取
                var tasks = _tasks.Values.Select(CloneTask).ToList();
                
                // 更新缓存
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheExpirationMinutes),
                    SlidingExpiration = TimeSpan.FromMinutes(_options.CacheExpirationMinutes / 2),
                    Priority = CacheItemPriority.High
                };
                
                _memoryCache.Set(CACHE_KEY_ALL_TASKS, tasks, cacheOptions);
                
                _logger.LogDebug("获取所有任务数据 - 数量: {Count}, 耗时: {ElapsedMs}ms", 
                    tasks.Count, stopwatch.ElapsedMilliseconds);
                
                return tasks;
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        /// <summary>
        /// 获取指定任务 - 优化版本
        /// </summary>
        public async Task<MainTask?> GetTaskAsync(Guid id)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                var cacheKey = CACHE_KEY_PREFIX + id.ToString();
                
                // 尝试从缓存获取
                if (_memoryCache.TryGetValue(cacheKey, out MainTask? cachedTask))
                {
                    Interlocked.Increment(ref _cacheHits);
                    return cachedTask;
                }

                Interlocked.Increment(ref _cacheMisses);
                
                // 从内存获取
                if (_tasks.TryGetValue(id, out var task))
                {
                    var clonedTask = CloneTask(task);
                    
                    // 更新缓存
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheExpirationMinutes),
                        Priority = CacheItemPriority.Normal
                    };
                    
                    _memoryCache.Set(cacheKey, clonedTask, cacheOptions);
                    return clonedTask;
                }
                
                return null;
            }
            finally
            {
                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds > 100)
                {
                    _logger.LogWarning("获取任务数据耗时过长: {ElapsedMs}ms, TaskId: {TaskId}", 
                        stopwatch.ElapsedMilliseconds, id);
                }
            }
        }

        #endregion

        /// <summary>
        /// 获取所有大任务数据的副本
        /// </summary>
        /// <returns>任务列表</returns>
        public List<MainTask> GetTasks()
        {
            lock (_rwLock)
            {
                return _tasks.Values.Select(t => t).ToList();
            }
        }
        /// <summary>
        /// 获取指定ID的大任务
        /// </summary>
        /// <param name="id">指定大任务id</param>
        /// <returns>大任务实体</returns>
        public MainTask? GetTask(Guid id)
        {
            lock (_rwLock)
            {
                _tasks.TryGetValue(id, out var task);
                return task;
            }
        }
        /// <summary>
        /// 大任务数据设置
        /// </summary>
        /// <param name="tasks">大任务数据列表</param>
        public async void SetTasks(IEnumerable<MainTask> tasks)
        {
            lock (_rwLock)
            {
                _tasks.Clear();
                foreach (var task in tasks)
                {
                    _tasks.TryAdd(task.Id, task);
                }

                // 异步同步到数据库 - 创建快照避免集合修改异常
                var tasksSnapshot = tasks.Select(CloneTask).ToList();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        foreach (var task in tasksSnapshot)
                        {
                            await _sqlserverService.FullSyncMainTaskAsync(task);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "任务数据库同步错误: {Message}", ex.Message);
                    }
                });
            }
        }
        /// <summary>
        /// 添加新的大任务
        /// </summary>
        /// <param name="task">
        /// 大任务实体
        /// </param>
        public async void AddTask(MainTask task, string CreatedBy)
        {
            lock (_rwLock)
            {
                if (!_tasks.ContainsKey(task.Id))
                {
                    _tasks.TryAdd(task.Id, task);
                    OnTaskChanged("Add", task);

                    // 异步同步到数据库 - 创建快照避免集合修改异常
                    var taskSnapshot = CloneTask(task);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _sqlserverService.FullSyncMainTaskAsync(taskSnapshot);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "添加任务数据库同步错误: {Message}", ex.Message);
                        }
                    });
                }
            }
        }
        /// <summary>
        /// 更新指定的大任务信息
        /// </summary>
        /// <param name="task">
        /// 大任务实体
        /// </param>
        /// <returns>
        /// 更新成功与否
        /// </returns>
        public bool UpdateTask(MainTask task)
        {
            lock (_rwLock)
            {
                if (_tasks.ContainsKey(task.Id))
                {
                    _tasks[task.Id] = task;
                    OnTaskChanged("update", task);

                    // 异步同步到数据库 - 创建快照避免集合修改异常
                    var taskSnapshot = CloneTask(task);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _sqlserverService.FullSyncMainTaskAsync(taskSnapshot);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "更新任务数据库同步错误: {Message}", ex.Message);
                        }
                    });

                    return true;
                }
                return false;
            }
        }
        /// <summary>
        /// 删除指定的大任务
        /// </summary>
        /// <param name="id">
        /// 大任务的唯一标识符
        /// </param>
        /// <returns>
        /// 返回删除成功与否
        /// </returns>
        public bool DeleteTask(Guid id)
        {
            lock (_rwLock)
            {
                if (_tasks.TryRemove(id, out var task))
                {
                    OnTaskChanged("delete", task);

                    // 异步删除数据库记录
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _sqlserverService.DeleteMainTaskAsync(id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "删除任务数据库同步错误: {Message}", ex.Message);
                        }
                    });

                    return true;
                }
                return false;
            }
        }
        /// <summary>
        /// 卸载指定子任务（将其AssignedDrone置空，状态设为WaitingForActivation，清除分配时间）
        /// </summary>
        public bool UnloadSubTask(Guid mainTaskId, Guid subTaskId)
        {
            lock (_rwLock)
            {
                if (!_tasks.ContainsKey(mainTaskId)) return false;
                var mainTask = _tasks[mainTaskId];
                var subTask = mainTask.SubTasks.FirstOrDefault(st => st.Id == subTaskId);
                if (subTask == null) return false;

                var oldDroneName = subTask.AssignedDrone;
                subTask.AssignedDrone = null;
                subTask.Status = TaskStatus.WaitingForActivation;
                subTask.AssignedTime = null;
                subTask.CompletedTime = null;
                subTask.ReassignmentCount++;

                // 异步同步到数据库 - 创建快照避免集合修改异常
                var subTaskSnapshot = CloneSubTask(subTask);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // 更新子任务状态
                        await _sqlserverService.UpdateSubTaskAsync(subTaskSnapshot);

                        // 如果之前有分配的无人机，更新DroneSubTasks表
                        if (!string.IsNullOrEmpty(oldDroneName))
                        {
                            var drone = await _sqlserverService.GetDroneByNameAsync(oldDroneName);
                            if (drone != null)
                            {
                                // 清空无人机的子任务分配
                                await _sqlserverService.SyncDroneSubTasksInTransaction(drone, new List<SubTask>());
                            }
                        }

                        _logger.LogInformation("子任务卸载成功：SubTaskId={SubTaskId}, OldDrone={OldDroneName}", subTaskId, oldDroneName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "卸载子任务数据库同步错误: {Message}", ex.Message);
                    }
                });

                return true;
            }
        }
        /// <summary>
        /// 重装（重新分配）指定子任务到无人机
        /// </summary>
        public bool ReloadSubTask(Guid mainTaskId, Guid subTaskId, string droneName)
        {
            lock (_rwLock)
            {
                if (!_tasks.ContainsKey(mainTaskId)) return false;
                var mainTask = _tasks[mainTaskId];
                var subTask = mainTask.SubTasks.FirstOrDefault(st => st.Id == subTaskId);
                if (subTask == null) return false;

                var oldDroneName = subTask.AssignedDrone;
                subTask.AssignedDrone = droneName;
                subTask.Status = TaskStatus.Running;
                subTask.AssignedTime = DateTime.Now;
                subTask.ReassignmentCount++;

                // 异步同步到数据库 - 创建快照避免集合修改异常
                var subTaskSnapshot = CloneSubTask(subTask);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // 获取新无人机
                        var newDrone = await _sqlserverService.GetDroneByNameAsync(droneName);
                        if (newDrone == null)
                        {
                            _logger.LogError("重装子任务失败：找不到无人机 {DroneName}", droneName);
                            return;
                        }

                        // 更新子任务状态
                        await _sqlserverService.UpdateSubTaskAsync(subTaskSnapshot);

                        // 更新DroneSubTasks表
                        await _sqlserverService.SyncDroneSubTasksInTransaction(newDrone, new List<SubTask> { subTaskSnapshot });

                        _logger.LogInformation("子任务重装成功：SubTaskId={SubTaskId}, OldDrone={OldDroneName}, NewDrone={NewDroneName}", 
                            subTaskId, oldDroneName, droneName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "重装子任务数据库同步错误: {Message}", ex.Message);
                    }
                });

                return true;
            }
        }
        /// <summary>
        /// 分配子任务到无人机
        /// </summary>
        public bool AssignSubTask(Guid mainTaskId, Guid subTaskId, string droneName)
        {
            lock (_rwLock)
            {
                if (!_tasks.ContainsKey(mainTaskId)) return false;
                var mainTask = _tasks[mainTaskId];
                var subTask = mainTask.SubTasks.FirstOrDefault(st => st.Id == subTaskId);
                if (subTask == null) return false;

                subTask.AssignedDrone = droneName;
                subTask.Status = TaskStatus.Running;
                subTask.AssignedTime = DateTime.Now;

                // 异步同步到数据库 - 创建快照避免集合修改异常
                var subTaskSnapshot = CloneSubTask(subTask);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // 获取无人机ID
                        var drone = await _sqlserverService.GetDroneByNameAsync(droneName);
                        if (drone == null)
                        {
                            _logger.LogError("分配子任务失败：找不到无人机 {DroneName}", droneName);
                            return;
                        }

                        // 更新子任务状态
                        await _sqlserverService.UpdateSubTaskAsync(subTaskSnapshot);

                        // 更新DroneSubTasks表
                        await _sqlserverService.SyncDroneSubTasksInTransaction(drone, new List<SubTask> { subTaskSnapshot });

                        _logger.LogInformation("子任务分配成功：SubTaskId={SubTaskId}, Drone={DroneName}", subTaskId, droneName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "分配子任务数据库同步错误: {Message}", ex.Message);
                    }
                });

                return true;
            }
        }
        /// <summary>
        /// 完成指定的子任务
        /// </summary>
        /// <param name="mainTaskId">大任务的唯一标识符</param>
        /// <param name="subTaskDescription">子任务的描述信息</param>
        /// <returns>返回完成成功与否</returns>
        public bool CompleteSubTask(Guid mainTaskId, string subTaskDescription)
        {
            lock (_rwLock)
            {
                if (_tasks.ContainsKey(mainTaskId))
                {
                    var mainTask = _tasks[mainTaskId];
                    var subTask = mainTask.SubTasks.FirstOrDefault(st => st.Description == subTaskDescription);
                    if (subTask != null && subTask.Status != System.Threading.Tasks.TaskStatus.RanToCompletion)
                    {
                        subTask.Status = System.Threading.Tasks.TaskStatus.RanToCompletion;
                        subTask.CompletedTime = DateTime.Now; // 使用当地时间保持一致性
                        OnTaskChanged("SubTaskCompleted", mainTask);
                        
                        _logger.LogInformation("子任务完成: TaskId={TaskId}, SubTask={SubTaskDescription}, CompletedTime={CompletedTime}", 
                            mainTaskId, subTaskDescription, subTask.CompletedTime);

                        // 异步同步到数据库
                        var taskSnapshot = CloneTask(mainTask);
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _sqlserverService.FullSyncMainTaskAsync(taskSnapshot);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "完成子任务数据库同步错误: {Message}", ex.Message);
                            }
                        });

                        return true;
                    }
                }
                return false;
            }
        }
        /// <summary>
        /// 获取指定大任务下的所有子任务
        /// </summary>
        public List<SubTask> GetSubTasks(Guid mainTaskId)
        {
            lock (_rwLock)
            {
                if (_tasks.ContainsKey(mainTaskId))
                {
                    var mainTask = _tasks[mainTaskId];
                    return mainTask.SubTasks.ToList();
                }
                return new List<SubTask>();
            }
        }
        /// <summary>
        /// 获取指定子任务
        /// </summary>
        public SubTask? GetSubTask(Guid mainTaskId, Guid subTaskId)
        {
            lock (_rwLock)
            {
                if (_tasks.ContainsKey(mainTaskId))
                {
                    var mainTask = _tasks[mainTaskId];
                    return mainTask.SubTasks.FirstOrDefault(st => st.Id == subTaskId);
                }
                return null;
            }
        }
        /// <summary>
        /// 子任务装载在主任务
        /// </summary>
        /// <param name="mainTaskId">主任务Guid</param>
        /// <param name="subTask">子任务实体</param>
        public async Task addSubTasks(Guid mainTaskId, SubTask subTask)
        {
            lock (_rwLock)
            {
                if (_tasks.ContainsKey(mainTaskId))
                {
                    var mainTask = _tasks[mainTaskId];
                    
                    // 检查是否已存在相同描述的子任务
                    var existingSubTask = mainTask.SubTasks.FirstOrDefault(st => st.Description == subTask.Description);
                    if (existingSubTask != null)
                    {
                        _logger.LogWarning("子任务已存在于内存中，跳过添加: TaskId={TaskId}, Description={Description}", 
                            mainTaskId, subTask.Description);
                        return;
                    }

                    subTask.ParentTask = mainTaskId; // 确保设置父任务ID
                    subTask.CreationTime = DateTime.Now; // 确保设置创建时间
                    subTask.AssignedTime = DateTime.Now;

                    mainTask.SubTasks.Add(subTask);

                    // 异步同步到数据库 - 创建快照避免集合修改异常
                    var subTaskSnapshot = CloneSubTask(subTask);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _sqlserverService.AddSubTaskAsync(subTaskSnapshot);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "添加子任务数据库同步错误: {Message}", ex.Message);
                        }
                    });
                }
            }
        }
        /// <summary>
        /// 获取指定无人机在指定任务期间的数据
        /// </summary>
        /// <param name="taskId"></param>
        /// <param name="droneId"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<List<DroneDataPoint>> GetTaskDroneDataAsync(Guid taskId, Guid droneId)
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
        /// 获取指定任务期间所有无人机的数据
        /// </summary>
        /// <param name="taskId"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<List<DroneDataPoint>> GetTaskAllDronesDataAsync(Guid taskId)
        {
            // 获取任务时间范围
            var taskTimeRange = await _sqlserverService.GetTaskTimeRangeAsync(taskId);
            if (taskTimeRange == null)
            {
                return new List<DroneDataPoint>();
            }

            // 获取所有无人机在任务时间范围内的数据
            return await _sqlserverService.GetAllDronesDataInTimeRangeAsync(
                taskTimeRange.StartTime,
                taskTimeRange.EndTime
            );
        }
        /// <summary>
        /// 获取指定时间段内所有无人机的数据
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        public async Task<List<DroneDataPoint>> GetAllTasksDataInTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            try
            {
                return await _sqlserverService.GetAllDronesDataInTimeRangeAsync(startTime, endTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取时间范围内数据错误: {Message}", ex.Message);
                return new List<DroneDataPoint>();
            }
        }
        /// <summary>
        /// 从数据库加载所有主任务（优化版本）
        /// </summary>
        public async Task LoadTasksFromDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("开始从数据库加载任务数据...");
                
                var dbTasks = await _sqlserverService.GetAllMainTasksAsync();
                _logger.LogInformation("加载了 {MainTaskCount} 个主任务", dbTasks.Count);
                
                if (!dbTasks.Any())
                {
                    _logger.LogInformation("数据库中未找到任务数据");
                    return;
                }

                // 并行加载子任务和图片数据
                await LoadTasksDataInParallel(dbTasks);

                // 原子性更新内存数据
                lock (_rwLock)
                {
                    _tasks.Clear();
                    foreach (var task in dbTasks)
                    {
                        _tasks.TryAdd(task.Id, task);
                    }
                }
                
                _logger.LogInformation("任务数据加载完成，总计 {MainTaskCount} 个主任务，{SubTaskCount} 个子任务", 
                    dbTasks.Count, dbTasks.Sum(t => t.SubTasks.Count));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从数据库加载任务错误: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 分批从数据库加载任务数据（大数据量优化）
        /// </summary>
        /// <param name="batchSize">批次大小</param>
        /// <param name="maxConcurrency">最大并发数</param>
        public async Task LoadTasksFromDatabaseBatchAsync(int batchSize = 50, int maxConcurrency = 5)
        {
            try
            {
                _logger.LogInformation("开始分批从数据库加载任务数据，批次大小: {BatchSize}", batchSize);
                
                var totalTasks = await _sqlserverService.GetMainTaskCountAsync();
                var batchCount = (int)Math.Ceiling((double)totalTasks / batchSize);
                
                _logger.LogInformation("总主任务数: {TotalTasks}, 分批数: {BatchCount}", totalTasks, batchCount);
                
                var semaphore = new SemaphoreSlim(maxConcurrency);
                var allTasks = new ConcurrentBag<MainTask>();
                var loadTasks = new List<Task>();
                
                for (int batch = 0; batch < batchCount; batch++)
                {
                    var currentBatch = batch;
                    var task = Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            var tasks = await _sqlserverService.GetMainTasksByPageAsync(currentBatch, batchSize);
                            
                            // 并行加载每个任务的子任务和图片
                            await LoadTasksDataInParallel(tasks);
                            
                            foreach (var t in tasks)
                            {
                                allTasks.Add(t);
                            }
                            
                            _logger.LogDebug("完成第 {Batch}/{TotalBatches} 批次，{TaskCount} 个任务", 
                                currentBatch + 1, batchCount, tasks.Count);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                    
                    loadTasks.Add(task);
                }
                
                await Task.WhenAll(loadTasks);
                
                // 原子性更新内存数据
                lock (_rwLock)
                {
                    _tasks.Clear();
                    foreach (var task in allTasks)
                    {
                        _tasks.TryAdd(task.Id, task);
                    }
                }
                
                _logger.LogInformation("分批加载完成，总计 {MainTaskCount} 个主任务，{SubTaskCount} 个子任务", 
                    allTasks.Count, allTasks.Sum(t => t.SubTasks.Count));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "分批加载任务数据失败: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 并行加载任务的子数据
        /// </summary>
        public async Task LoadTasksDataInParallel(List<MainTask> tasks)
        {
            var loadTasks = tasks.Select(async task =>
            {
                try
                {
                    // 并行加载子任务和图片
                    var subTasksTask = _sqlserverService.GetSubTasksByParentAsync(task.Id);
                    
                    var subTasks = await subTasksTask;
                    task.SubTasks.AddRange(subTasks);
                    
                                         // 并行加载图片元数据（不加载二进制数据）
                     if (subTasks.Any())
                     {
                         var imageLoadTasks = subTasks.Select(async subTask =>
                         {
                             try
                             {
                                 // 只加载图片的基本信息，不加载ImageData二进制数据
                                 var imageMetadata = await _sqlserverService.GetSubTaskImageMetadataAsync(subTask.Id);
                                 subTask.Images = imageMetadata;
                                 
                                 _logger.LogDebug("为子任务 {SubTaskId} 加载了 {ImageCount} 条图片元数据", 
                                     subTask.Id, imageMetadata.Count);
                             }
                             catch (Exception ex)
                             {
                                 _logger.LogWarning(ex, "加载子任务图片元数据失败: SubTaskId={SubTaskId}", subTask.Id);
                             }
                         });
                         
                         await Task.WhenAll(imageLoadTasks);
                     }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "加载任务数据失败: TaskId={TaskId}", task.Id);
                }
            });
            
            await Task.WhenAll(loadTasks);
        }
        /// <summary>
        /// 同步所有任务到数据库
        /// </summary>
        public async Task SyncAllTasksToDatabaseAsync()
        {
            try
            {
                List<MainTask> tasksToSync;
                lock (_rwLock)
                {
                    tasksToSync = _tasks.Values.ToList();
                }

                foreach (var task in tasksToSync)
                {
                    await _sqlserverService.FullSyncMainTaskAsync(task);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步任务到数据库错误: {Message}", ex.Message);
            }
        }
        // 新增：深度克隆方法
        public static MainTask CloneTask(MainTask task) => new()
        {
            Id = task.Id,
            Description = task.Description,
            Status = task.Status,
            CreationTime = task.CreationTime,
            CompletedTime = task.CompletedTime,
            SubTasks = task.SubTasks.Select(CloneSubTask).ToList()
        };

        private static SubTask CloneSubTask(SubTask st) => new()
        {
            Id = st.Id,
            Description = st.Description,
            Status = st.Status,
            CreationTime = st.CreationTime,
            AssignedTime = st.AssignedTime,
            CompletedTime = st.CompletedTime,
            ParentTask = st.ParentTask,
            ReassignmentCount = st.ReassignmentCount,
            AssignedDrone = st.AssignedDrone,
            ImagePaths = st.ImagePaths.ToList(),
            Images = st.Images.ToList()
        };

        /// <summary>
        /// 获取任务统计信息
        /// </summary>
        public TaskStatistics GetTaskStatistics()
        {
            lock (_rwLock)
            {
                var stats = new TaskStatistics();
                
                foreach (var task in _tasks.Values)
                {
                    stats.TotalMainTasks++;
                    stats.TotalSubTasks += task.SubTasks.Count;
                    
                    switch (task.Status)
                    {
                        case TaskStatus.WaitingForActivation:
                            stats.PendingMainTasks++;
                            break;
                        case TaskStatus.Running:
                            stats.ActiveMainTasks++;
                            break;
                        case TaskStatus.RanToCompletion:
                            stats.CompletedMainTasks++;
                            break;
                        case TaskStatus.Faulted:
                        case TaskStatus.Canceled:
                            stats.FailedMainTasks++;
                            break;
                    }
                    
                    foreach (var subTask in task.SubTasks)
                    {
                        switch (subTask.Status)
                        {
                            case TaskStatus.WaitingForActivation:
                            case TaskStatus.WaitingToRun:
                                stats.PendingSubTasks++;
                                break;
                            case TaskStatus.Running:
                                stats.ActiveSubTasks++;
                                break;
                            case TaskStatus.RanToCompletion:
                                stats.CompletedSubTasks++;
                                break;
                            case TaskStatus.Faulted:
                            case TaskStatus.Canceled:
                                stats.FailedSubTasks++;
                                break;
                        }
                    }
                }
                
                return stats;
            }
        }

        /// <summary>
        /// 获取服务性能统计 - 用于系统监控
        /// </summary>
        public TaskServiceStatistics GetStatistics()
        {
            return new TaskServiceStatistics
            {
                TotalTasks = _tasks.Count,
                TotalOperations = _totalOperations,
                CacheHitRate = _totalOperations > 0 ? (double)_cacheHits / _totalOperations : 0,
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses,
                ActiveTasks = _tasks.Values.Count(t => t.Status == TaskStatus.Running || t.Status == TaskStatus.WaitingToRun),
                CompletedTasks = _tasks.Values.Count(t => t.Status == TaskStatus.RanToCompletion),
                LastUpdateTime = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 根据状态获取主任务
        /// </summary>
        public List<MainTask> GetTasksByStatus(TaskStatus status)
        {
            lock (_rwLock)
            {
                return _tasks.Values.Where(t => t.Status == status).ToList();
            }
        }

        /// <summary>
        /// 获取指定无人机的所有活跃子任务
        /// </summary>
        public List<SubTask> GetActiveSubTasksForDrone(string droneName)
        {
            lock (_rwLock)
            {
                var activeTasks = new List<SubTask>();
                foreach (var mainTask in _tasks.Values)
                {
                    var droneSubTasks = mainTask.SubTasks
                        .Where(st => st.AssignedDrone == droneName && 
                                   (st.Status == TaskStatus.Running || st.Status == TaskStatus.WaitingToRun))
                        .ToList();
                    activeTasks.AddRange(droneSubTasks);
                }
                return activeTasks;
            }
        }

        /// <summary>
        /// 批量更新子任务状态
        /// </summary>
        public async Task<int> BatchUpdateSubTaskStatusAsync(List<Guid> subTaskIds, TaskStatus newStatus, string? reason = null)
        {
            var updatedCount = 0;
            
            lock (_rwLock)
            {
                foreach (var subTaskId in subTaskIds)
                {
                    if (_tasks.ContainsKey(subTaskId))
                    {
                        var mainTask = _tasks[subTaskId];
                        var subTask = mainTask.SubTasks.FirstOrDefault(st => st.Id == subTaskId);
                        if (subTask != null)
                        {
                            subTask.Status = newStatus;
                            if (newStatus == TaskStatus.RanToCompletion || newStatus == TaskStatus.Faulted)
                            {
                                subTask.CompletedTime = DateTime.Now;
                            }
                            updatedCount++;
                            
                            // 异步同步到数据库 - 创建快照避免集合修改异常
                            var subTaskSnapshot = CloneSubTask(subTask);
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await _sqlserverService.UpdateSubTaskAsync(subTaskSnapshot);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "批量更新子任务数据库同步错误: {Message}", ex.Message);
                                }
                            });
                        }
                    }
                }
            }
            
            return updatedCount;
        }

        /// <summary>
        /// 获取过期的子任务（超时未完成）
        /// </summary>
        public List<SubTask> GetExpiredSubTasks(TimeSpan timeout)
        {
            var cutoffTime = DateTime.Now - timeout;
            
            lock (_rwLock)
            {
                var expiredTasks = new List<SubTask>();
                foreach (var mainTask in _tasks.Values)
                {
                    var expired = mainTask.SubTasks
                        .Where(st => st.Status == TaskStatus.Running && 
                                   st.AssignedTime.HasValue && 
                                   st.AssignedTime.Value < cutoffTime)
                        .ToList();
                    expiredTasks.AddRange(expired);
                }
                return expiredTasks;
            }
        }

        /// <summary>
        /// 重新分配失败的子任务
        /// </summary>
        public async Task<int> ReassignFailedSubTasksAsync()
        {
            var reassignedCount = 0;
            
            lock (_rwLock)
            {
                foreach (var mainTask in _tasks.Values)
                {
                    var failedSubTasks = mainTask.SubTasks
                        .Where(st => st.Status == TaskStatus.Faulted && st.ReassignmentCount < 3)
                        .ToList();
                    
                    foreach (var subTask in failedSubTasks)
                    {
                        subTask.Status = TaskStatus.WaitingForActivation;
                        subTask.AssignedDrone = null;
                        subTask.AssignedTime = null;
                        subTask.CompletedTime = null;
                        subTask.ReassignmentCount++;
                        reassignedCount++;
                        
                        // 异步同步到数据库 - 创建快照避免集合修改异常
                        var subTaskSnapshot = CloneSubTask(subTask);
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _sqlserverService.UpdateSubTaskAsync(subTaskSnapshot);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "重新分配子任务数据库同步错误: {Message}", ex.Message);
                            }
                        });
                    }
                }
            }
            
            return reassignedCount;
        }

        /// <summary>
        /// 清理已完成的旧任务
        /// </summary>
        public async Task<int> CleanupOldCompletedTasksAsync(TimeSpan maxAge)
        {
            var cutoffTime = DateTime.Now - maxAge;
            var cleanedCount = 0;
            
            lock (_rwLock)
            {
                var tasksToRemove = _tasks.Values
                    .Where(t => t.Status == TaskStatus.RanToCompletion && 
                               t.CompletedTime.HasValue && 
                               t.CompletedTime.Value < cutoffTime)
                    .ToList();
                
                foreach (var task in tasksToRemove)
                {
                    _tasks.TryRemove(task.Id, out _);
                    cleanedCount++;
                    
                    // 异步删除数据库记录 - 使用任务ID快照
                    var taskId = task.Id;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _sqlserverService.DeleteMainTaskAsync(taskId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "清理旧任务数据库同步错误: {Message}", ex.Message);
                        }
                    });
                }
            }
            
            return cleanedCount;
        }

        /// <summary>
        /// 获取任务执行时间分析
        /// </summary>
        public TaskPerformanceAnalysis GetTaskPerformanceAnalysis()
        {
            lock (_rwLock)
            {
                var analysis = new TaskPerformanceAnalysis();
                var completedSubTasks = new List<SubTask>();
                
                foreach (var mainTask in _tasks.Values)
                {
                    completedSubTasks.AddRange(
                        mainTask.SubTasks.Where(st => st.Status == TaskStatus.RanToCompletion && 
                                                    st.AssignedTime.HasValue && 
                                                    st.CompletedTime.HasValue));
                }
                
                if (completedSubTasks.Any())
                {
                    var executionTimes = completedSubTasks
                        .Select(st => (st.CompletedTime!.Value - st.AssignedTime!.Value).TotalMinutes)
                        .ToList();
                    
                    analysis.AverageExecutionTimeMinutes = executionTimes.Average();
                    analysis.MinExecutionTimeMinutes = executionTimes.Min();
                    analysis.MaxExecutionTimeMinutes = executionTimes.Max();
                    analysis.TotalCompletedTasks = completedSubTasks.Count;
                }
                
                return analysis;
            }
        }

        /// <summary>
        /// 更新子任务的图片路径
        /// </summary>
        /// <param name="mainTaskId">大任务的唯一标识符</param>
        /// <param name="subTaskDescription">子任务的描述信息</param>
        /// <param name="imagePath">图片路径</param>
        /// <returns>返回更新成功与否</returns>
        public bool UpdateSubTaskImage(Guid mainTaskId, string subTaskDescription, string imagePath)
        {
            lock (_rwLock)
            {
                if (_tasks.ContainsKey(mainTaskId))
                {
                    var mainTask = _tasks[mainTaskId];
                    var subTask = mainTask.SubTasks.FirstOrDefault(st => st.Description == subTaskDescription);
                    if (subTask != null)
                    {
                        // 添加新图片路径到列表中
                        if (!subTask.ImagePaths.Contains(imagePath))
                        {
                            subTask.ImagePaths.Add(imagePath);
                        }
                        OnTaskChanged("SubTaskImageUpdated", mainTask);

                        // 异步同步到数据库
                        var taskSnapshot = CloneTask(mainTask);
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _sqlserverService.FullSyncMainTaskAsync(taskSnapshot);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "更新子任务图片数据库同步错误: {Message}", ex.Message);
                            }
                        });

                        _logger.LogInformation("子任务图片更新成功: TaskId={TaskId}, SubTask={SubTask}, ImagePath={ImagePath}, 总图片数={ImageCount}", 
                            mainTaskId, subTaskDescription, imagePath, subTask.ImagePaths.Count);
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// 批量添加子任务图片
        /// </summary>
        /// <param name="mainTaskId">大任务的唯一标识符</param>
        /// <param name="subTaskDescription">子任务的描述信息</param>
        /// <param name="imagePaths">图片路径列表</param>
        /// <returns>返回更新成功与否</returns>
        public bool AddSubTaskImages(Guid mainTaskId, string subTaskDescription, List<string> imagePaths)
        {
            lock (_rwLock)
            {
                if (_tasks.ContainsKey(mainTaskId))
                {
                    var mainTask = _tasks[mainTaskId];
                    var subTask = mainTask.SubTasks.FirstOrDefault(st => st.Description == subTaskDescription);
                    if (subTask != null)
                    {
                        // 批量添加图片路径，避免重复
                        foreach (var imagePath in imagePaths)
                        {
                            if (!subTask.ImagePaths.Contains(imagePath))
                            {
                                subTask.ImagePaths.Add(imagePath);
                            }
                        }
                        OnTaskChanged("SubTaskImagesUpdated", mainTask);

                        // 异步同步到数据库
                        var taskSnapshot = CloneTask(mainTask);
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _sqlserverService.FullSyncMainTaskAsync(taskSnapshot);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "批量更新子任务图片数据库同步错误: {Message}", ex.Message);
                            }
                        });

                        _logger.LogInformation("子任务图片批量更新成功: TaskId={TaskId}, SubTask={SubTask}, 新增图片数={NewImageCount}, 总图片数={TotalImageCount}", 
                            mainTaskId, subTaskDescription, imagePaths.Count, subTask.ImagePaths.Count);
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// 为所有子任务加载图片元数据（不包含二进制数据）
        /// </summary>
        public async Task LoadSubTaskImageMetadataFromDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("开始从数据库加载子任务图片元数据...");
                
                var tasks = GetTasks(); // 获取任务快照，避免长时间锁定
                
                var loadTasks = tasks.Select(async task =>
                        {
                    var subTaskLoadTasks = task.SubTasks.Select(async subTask =>
                            {
                                try
                                {
                            // 只加载图片元数据，不加载二进制数据
                            var imageMetadata = await _sqlserverService.GetSubTaskImageMetadataAsync(subTask.Id);
                            if (imageMetadata.Any())
                                    {
                                subTask.Images = imageMetadata;
                                _logger.LogDebug("为子任务 {SubTaskId} 加载了 {ImageCount} 条图片元数据", 
                                    subTask.Id, imageMetadata.Count);
                                    }
                                }
                                catch (Exception ex)
                                {
                            _logger.LogError(ex, "加载子任务图片元数据失败: SubTaskId={SubTaskId}", subTask.Id);
                                }
                    });
                    
                    await Task.WhenAll(subTaskLoadTasks);
                });
                
                await Task.WhenAll(loadTasks);
                
                _logger.LogInformation("子任务图片元数据加载完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从数据库加载子任务图片元数据失败");
            }
        }

        /// <summary>
        /// 按需加载指定子任务的完整图片数据
        /// </summary>
        /// <param name="subTaskId">子任务ID</param>
        /// <returns>包含二进制数据的图片列表</returns>
        public async Task<List<SubTaskImage>> LoadFullImageDataAsync(Guid subTaskId)
        {
            try
            {
                _logger.LogDebug("按需加载子任务完整图片数据: SubTaskId={SubTaskId}", subTaskId);
                
                // 从数据库获取包含二进制数据的完整图片信息
                var fullImages = new List<SubTaskImage>();
                
                lock (_rwLock)
                {
                    var mainTask = _tasks.Values.FirstOrDefault(t => t.SubTasks.Any(st => st.Id == subTaskId));
                    if (mainTask != null)
                    {
                        var subTask = mainTask.SubTasks.FirstOrDefault(st => st.Id == subTaskId);
                        if (subTask != null && subTask.Images.Any())
                        {
                            // 如果内存中已有图片元数据，则根据ID逐个加载完整数据
                            var loadImageTasks = subTask.Images.Select(async img =>
                            {
                                var fullImage = await _sqlserverService.GetSubTaskImageAsync(img.Id);
                                if (fullImage != null)
                                {
                                    fullImages.Add(fullImage);
                                }
                            });
                            
                            Task.WhenAll(loadImageTasks).Wait();
                        }
                    }
                }
                
                if (!fullImages.Any())
                {
                    // 如果内存中没有图片元数据，直接从数据库加载
                    fullImages = await _sqlserverService.GetSubTaskImagesAsync(subTaskId);
                }
                
                _logger.LogDebug("按需加载完成: SubTaskId={SubTaskId}, 图片数={ImageCount}", 
                    subTaskId, fullImages.Count);
                
                return fullImages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "按需加载图片数据失败: SubTaskId={SubTaskId}", subTaskId);
                return new List<SubTaskImage>();
            }
        }

        #region IDisposable Implementation

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _rwLock.Dispose();
                }

                // Dispose unmanaged resources
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    /// <summary>
    /// 无人机数据变更事件参数
    /// </summary>
    public class TaskChangedEventArgs : EventArgs
    {
        public string Action { get; set; } = "";
        public MainTask MainTask { get; set; } = null!;
        public SubTask SubTask { get; set; } = new SubTask();
        public DateTime Timestamp { get; set; }
    }
}

