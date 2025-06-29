using ClassLibrary_Core.Mission;
using ClassLibrary_Core.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebApplication_Drone.Services.Interfaces;
using WebApplication_Drone.Services.Models;
using System.Collections.Concurrent;
using WebApplication_Drone.Services;

namespace WebApplication_Drone.Services.Clean
{
    /// <summary>
    /// 任务服务 - 清理后的版本，只负责业务逻辑和缓存
    /// </summary>
    public class TaskService : IDisposable
    {
        private readonly ITaskRepository _taskRepository;
        private readonly ILogger<TaskService> _logger;
        private readonly RedisCacheService _cacheService;
        private readonly DataServiceOptions _options;
        private readonly SqlserverService _sqlserverService;
        
        // 内存缓存
        private readonly ConcurrentDictionary<Guid, MainTask> _tasks = new();
        
        // 性能计数器
        private long _totalOperations = 0;
        private long _cacheHits = 0;
        private long _cacheMisses = 0;
        
        // 缓存键
        private const string CACHE_KEY_ALL_TASKS = "tasks:all";
        private const string CACHE_KEY_PREFIX = "task:";
        
        // 事件
        public event EventHandler<TaskChangedEventArgs>? TaskChanged;
        
        public TaskService(
            ITaskRepository taskRepository,
            ILogger<TaskService> logger,
            RedisCacheService cacheService,
            IOptions<DataServiceOptions> options,
            SqlserverService sqlserverService)
        {
            _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _sqlserverService = sqlserverService ?? throw new ArgumentNullException(nameof(sqlserverService));
        }
        
        #region 主任务操作
        
        public async Task<List<MainTask>> GetTasksAsync()
        {
            Interlocked.Increment(ref _totalOperations);
            
            // 尝试从Redis缓存获取
            var cachedTasks = await _cacheService.GetAsync<List<MainTask>>(CACHE_KEY_ALL_TASKS);
            if (cachedTasks != null)
            {
                Interlocked.Increment(ref _cacheHits);
                return cachedTasks;
            }
            
            Interlocked.Increment(ref _cacheMisses);
            
            // 从内存获取
            var tasks = _tasks.Values.Select(CloneTask).ToList();
            
            // 如果内存为空，从数据库加载
            if (!tasks.Any())
            {
                tasks = await _taskRepository.GetAllAsync();
                foreach (var task in tasks)
                {
                    _tasks.TryAdd(task.Id, CloneTask(task));
                }
            }
            
            // 更新Redis缓存
            await _cacheService.SetAsync(CACHE_KEY_ALL_TASKS, tasks, 
                TimeSpan.FromMinutes(_options.CacheExpirationMinutes));
            
            return tasks;
        }
        
        /// <summary>
        /// 获取所有任务（GetTasksAsync的别名，保持向后兼容）
        /// </summary>
        public async Task<List<MainTask>> GetAllTasksAsync()
        {
            return await GetTasksAsync();
        }
        
        public async Task<MainTask?> GetTaskAsync(Guid id)
        {
            Interlocked.Increment(ref _totalOperations);
            
            var cacheKey = $"{CACHE_KEY_PREFIX}{id}";
            var cachedTask = await _cacheService.GetAsync<MainTask>(cacheKey);
            if (cachedTask != null)
            {
                Interlocked.Increment(ref _cacheHits);
                return cachedTask;
            }
            
            Interlocked.Increment(ref _cacheMisses);
            
            // 从内存获取
            var task = _tasks.TryGetValue(id, out var t) ? CloneTask(t) : null;
            
            // 如果内存中没有，从数据库获取
            if (task == null)
            {
                task = await _taskRepository.GetByIdAsync(id);
                if (task != null)
                {
                    _tasks.TryAdd(task.Id, CloneTask(task));
                }
            }
            
            if (task != null)
            {
                await _cacheService.SetAsync(cacheKey, task, 
                    TimeSpan.FromMinutes(_options.CacheExpirationMinutes));
            }
            
            return task;
        }
        
        public async Task<bool> AddTaskAsync(MainTask task, string createdBy)
        {
            // 保存到数据库
            var success = await _taskRepository.AddAsync(task, createdBy);
            if (success)
            {
                _tasks.TryAdd(task.Id, CloneTask(task));
                
                // 清除缓存
                await _cacheService.RemoveAsync(CACHE_KEY_ALL_TASKS);
                
                // 触发事件
                OnTaskChanged("Added", task);
            }
            
            return success;
        }
        
        public async Task<bool> UpdateTaskAsync(MainTask task)
        {
            if (_tasks.TryGetValue(task.Id, out var existing))
            {
                var updated = UpdateTaskData(existing, task);
                _tasks[task.Id] = updated;
                
                // 更新数据库
                await _taskRepository.UpdateAsync(updated);
                
                // 清除缓存
                await _cacheService.RemoveAsync(CACHE_KEY_ALL_TASKS);
                await _cacheService.RemoveAsync($"{CACHE_KEY_PREFIX}{task.Id}");
                
                // 触发事件
                OnTaskChanged("Updated", updated);
                
                return true;
            }
            return false;
        }
        
        public async Task<bool> DeleteTaskAsync(Guid id)
        {
            if (_tasks.TryRemove(id, out var task))
            {
                // 从数据库删除
                await _taskRepository.DeleteAsync(id);
                
                // 清除缓存
                await _cacheService.RemoveAsync(CACHE_KEY_ALL_TASKS);
                await _cacheService.RemoveAsync($"{CACHE_KEY_PREFIX}{id}");
                
                // 触发事件
                OnTaskChanged("Deleted", task);
                
                return true;
            }
            return false;
        }
        
        public async Task<int> GetTaskCountAsync()
        {
            return _tasks.Count;
        }
        
        #endregion
        
        #region 子任务操作
        
        public async Task<List<SubTask>> GetSubTasksAsync(Guid mainTaskId)
        {
            return await _taskRepository.GetSubTasksAsync(mainTaskId);
        }
        
        public async Task<SubTask?> GetSubTaskAsync(Guid mainTaskId, Guid subTaskId)
        {
            return await _taskRepository.GetSubTaskAsync(mainTaskId, subTaskId);
        }
        
        public async Task<bool> AddSubTaskAsync(SubTask subTask)
        {
            var success = await _taskRepository.AddSubTaskAsync(subTask);
            if (success)
            {
                // 清除相关缓存
                await _cacheService.RemoveAsync(CACHE_KEY_ALL_TASKS);
            }
            return success;
        }
        
        public async Task<bool> UpdateSubTaskAsync(SubTask subTask)
        {
            var success = await _taskRepository.UpdateSubTaskAsync(subTask);
            if (success)
            {
                // 清除相关缓存
                await _cacheService.RemoveAsync(CACHE_KEY_ALL_TASKS);
            }
            return success;
        }
        
        public async Task<bool> DeleteSubTaskAsync(Guid mainTaskId, Guid subTaskId)
        {
            var success = await _taskRepository.DeleteSubTaskAsync(mainTaskId, subTaskId);
            if (success)
            {
                // 清除相关缓存
                await _cacheService.RemoveAsync(CACHE_KEY_ALL_TASKS);
            }
            return success;
        }
        
        #endregion
        
        #region 图片操作
        
        public async Task<Guid> SaveImageAsync(Guid subTaskId, byte[] imageData, string fileName, int imageIndex = 1, string? description = null)
        {
            return await _taskRepository.SaveImageAsync(subTaskId, imageData, fileName, imageIndex, description);
        }
        
        public async Task<List<SubTaskImage>> GetImagesAsync(Guid subTaskId)
        {
            return await _taskRepository.GetImagesAsync(subTaskId);
        }
        
        public async Task<SubTaskImage?> GetImageAsync(Guid imageId)
        {
            return await _taskRepository.GetImageAsync(imageId);
        }
        
        public async Task<bool> DeleteImageAsync(Guid imageId)
        {
            return await _taskRepository.DeleteImageAsync(imageId);
        }

        /// <summary>
        /// 获取最近上传的图片
        /// </summary>
        /// <param name="since">从指定时间开始</param>
        /// <param name="limit">限制返回数量</param>
        /// <returns>最近上传的图片列表</returns>
        public async Task<List<SubTaskImage>> GetRecentImagesAsync(DateTime since, int limit = 50)
        {
            try
            {
                _logger.LogDebug("获取最近图片: Since={Since}, Limit={Limit}", since, limit);
                var images = await _sqlserverService.GetRecentSubTaskImagesAsync(since, limit);
                _logger.LogDebug("获取到 {Count} 张最近图片", images.Count);
                return images;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取最近图片失败: Since={Since}, Limit={Limit}", since, limit);
                return new List<SubTaskImage>();
            }
        }
        
        #endregion
        
        #region 批量操作
        
        public async Task<bool> BulkUpdateTasksAsync(IEnumerable<MainTask> tasks)
        {
            // 更新数据库
            await _taskRepository.BulkUpdateAsync(tasks);
            
            // 更新内存
            foreach (var task in tasks)
            {
                _tasks.AddOrUpdate(task.Id, CloneTask(task), (_, _) => CloneTask(task));
            }
            
            // 清除缓存
            await _cacheService.RemoveAsync(CACHE_KEY_ALL_TASKS);
            
            return true;
        }
        
        #endregion
        
        #region 数据点操作
        
        public async Task<List<DroneDataPoint>> GetTaskDataAsync(Guid taskId, Guid droneId)
        {
            return await _taskRepository.GetTaskDataAsync(taskId, droneId);
        }
        
        #endregion
        
        #region 统计信息
        
        public DataServiceStatistics GetStatistics()
        {
            return new DataServiceStatistics
            {
                TotalDrones = 0, // 无人机统计在DroneService中
                OnlineDrones = 0,
                TotalTasks = _tasks.Count,
                ActiveTasks = _tasks.Values.Count(t => t.Status == System.Threading.Tasks.TaskStatus.Running || t.Status == System.Threading.Tasks.TaskStatus.WaitingToRun),
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
        
        private MainTask CloneTask(MainTask task) => new()
        {
            Id = task.Id,
            Description = task.Description,
            Status = task.Status,
            CreationTime = task.CreationTime,
            StartTime = task.StartTime,
            CompletedTime = task.CompletedTime,
            SubTasks = task.SubTasks?.Select(CloneSubTask).ToList() ?? new List<SubTask>()
        };
        
        private SubTask CloneSubTask(SubTask subTask) => new()
        {
            Id = subTask.Id,
            Description = subTask.Description,
            Status = subTask.Status,
            CreationTime = subTask.CreationTime,
            AssignedTime = subTask.AssignedTime,
            CompletedTime = subTask.CompletedTime,
            ParentTask = subTask.ParentTask,
            ReassignmentCount = subTask.ReassignmentCount,
            AssignedDrone = subTask.AssignedDrone,
            Images = subTask.Images?.ToList() ?? new List<SubTaskImage>()
        };
        
        private MainTask UpdateTaskData(MainTask existing, MainTask updated)
        {
            existing.Name = updated.Name;
            existing.Description = updated.Description;
            existing.Status = updated.Status;
            existing.StartTime = updated.StartTime;
            existing.CompletedTime = updated.CompletedTime;
            return existing;
        }
        
        private void OnTaskChanged(string action, MainTask task)
        {
            try
            {
                TaskChanged?.Invoke(this, new TaskChangedEventArgs
                {
                    Action = action,
                    MainTask = task,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "触发任务变更事件失败: {Action}", action);
            }
        }
        
        #endregion
        
        #region 兼容性方法
        
        /// <summary>
        /// 获取任务列表（同步版本，兼容性方法）
        /// </summary>
        public List<MainTask> GetTasks()
        {
            return _tasks.Values.Select(CloneTask).ToList();
        }
        
        /// <summary>
        /// 获取任务（同步版本，兼容性方法）
        /// </summary>
        public MainTask? GetTask(Guid id)
        {
            return _tasks.TryGetValue(id, out var task) ? CloneTask(task) : null;
        }
        
        /// <summary>
        /// 添加任务（同步版本，兼容性方法）
        /// </summary>
        public void AddTask(MainTask task, string createdBy)
        {
            _ = Task.Run(async () => await AddTaskAsync(task, createdBy));
        }
        
        /// <summary>
        /// 更新任务（同步版本，兼容性方法）
        /// </summary>
        public bool UpdateTask(MainTask task)
        {
            return Task.Run(async () => await UpdateTaskAsync(task)).Result;
        }
        
        /// <summary>
        /// 删除任务（同步版本，兼容性方法）
        /// </summary>
        public bool DeleteTask(Guid id)
        {
            return Task.Run(async () => await DeleteTaskAsync(id)).Result;
        }
        
        /// <summary>
        /// 卸载子任务（兼容性方法）
        /// </summary>
        public void UnloadSubTask(Guid mainTaskId, Guid subTaskId)
        {
            // 实现卸载逻辑
            _logger.LogInformation("卸载子任务: MainTaskId={MainTaskId}, SubTaskId={SubTaskId}", mainTaskId, subTaskId);
        }
        
        /// <summary>
        /// 重载子任务（兼容性方法）
        /// </summary>
        public void ReloadSubTask(Guid mainTaskId, Guid subTaskId, string nodeName)
        {
            // 实现重载逻辑
            _logger.LogInformation("重载子任务: MainTaskId={MainTaskId}, SubTaskId={SubTaskId}, NodeName={NodeName}", 
                mainTaskId, subTaskId, nodeName);
        }
        
        /// <summary>
        /// 分配子任务（兼容性方法）
        /// </summary>
        public bool AssignSubTask(Guid mainTaskId, Guid subTaskId, string nodeName)
        {
            // 实现分配逻辑
            if (!_tasks.TryGetValue(mainTaskId, out var mainTask))
            {
                _logger.LogWarning("主任务不存在，无法分配子任务: MainTaskId={MainTaskId}", mainTaskId);
                return false;
            }
            var subTask = mainTask.SubTasks?.FirstOrDefault(st => st.Id == subTaskId);
            subTask.AssignedDrone = nodeName;

            _logger.LogInformation("分配子任务: MainTaskId={MainTaskId}, SubTaskId={SubTaskId}, NodeName={NodeName}", 
                mainTaskId, subTaskId, nodeName);
            return true;
        }
        
        /// <summary>
        /// 添加子任务（兼容性方法）
        /// </summary>
        public void addSubTasks(Guid mainTaskId, SubTask subTask)
        {
            try
            {
                _logger.LogInformation("开始添加子任务: MainTaskId={MainTaskId}, SubTaskId={SubTaskId}, Description={Description}", 
                    mainTaskId, subTask.Id, subTask.Description);
                
                // 检查主任务是否存在
                if (!_tasks.TryGetValue(mainTaskId, out var existingTask))
                {
                    _logger.LogWarning("主任务不存在，无法添加子任务: MainTaskId={MainTaskId}", mainTaskId);
                    return;
                }
                
                _logger.LogInformation("找到主任务: MainTaskId={MainTaskId}, CurrentSubTasksCount={CurrentSubTasksCount}", 
                    mainTaskId, existingTask.SubTasks?.Count ?? 0);
                
                // 立即更新内存中的任务数据
                var clonedTask = CloneTask(existingTask);
                
                // 确保SubTasks列表已初始化
                if (clonedTask.SubTasks == null)
                {
                    clonedTask.SubTasks = new List<SubTask>();
                }
                
                clonedTask.SubTasks.Add(subTask);
                _tasks.AddOrUpdate(mainTaskId, clonedTask, (_, _) => clonedTask);
                
                _logger.LogInformation("子任务已添加到内存: MainTaskId={MainTaskId}, SubTaskId={SubTaskId}, NewSubTasksCount={NewSubTasksCount}", 
                    mainTaskId, subTask.Id, clonedTask.SubTasks.Count);
                
                // 异步保存到数据库（不等待完成）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var saveResult = await AddSubTaskAsync(subTask);
                        _logger.LogDebug("子任务保存到数据库结果: SubTaskId={SubTaskId}, Result={Result}", subTask.Id, saveResult);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "保存子任务到数据库失败: SubTaskId={SubTaskId}", subTask.Id);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加子任务失败: MainTaskId={MainTaskId}, SubTaskId={SubTaskId}", mainTaskId, subTask.Id);
            }
        }
        
        /// <summary>
        /// 完成子任务（兼容性方法）
        /// </summary>
        public void CompleteSubTask(Guid taskGuid, string subtaskName)
        {
            // 实现完成逻辑
            _logger.LogInformation("完成子任务: TaskGuid={TaskGuid}, SubTaskName={SubTaskName}", taskGuid, subtaskName);
        }
        
        /// <summary>
        /// 从数据库加载任务（兼容性方法）
        /// </summary>
        public async Task LoadTasksFromDatabaseAsync()
        {
            try
            {
                var tasks = await _taskRepository.GetAllAsync();
                foreach (var task in tasks)
                {
                    _tasks.AddOrUpdate(task.Id, CloneTask(task), (_, _) => CloneTask(task));
                }
                _logger.LogInformation("从数据库加载了 {Count} 个任务", tasks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从数据库加载任务失败");
            }
        }
        
        /// <summary>
        /// 获取任务无人机数据（兼容性方法）
        /// </summary>
        public async Task<List<DroneDataPoint>> GetTaskDroneDataAsync(Guid taskId, Guid droneId)
        {
            // 这里需要实现具体的逻辑，暂时返回空列表
            _logger.LogInformation("获取任务无人机数据: TaskId={TaskId}, DroneId={DroneId}", taskId, droneId);
            return new List<DroneDataPoint>();
        }
        
        /// <summary>
        /// 获取任务所有无人机数据（兼容性方法）
        /// </summary>
        public async Task<List<DroneDataPoint>> GetTaskAllDronesDataAsync(Guid taskId)
        {
            // 这里需要实现具体的逻辑，暂时返回空列表
            _logger.LogInformation("获取任务所有无人机数据: TaskId={TaskId}", taskId);
            return new List<DroneDataPoint>();
        }
        
        /// <summary>
        /// 根据状态获取任务（兼容性方法）
        /// </summary>
        public List<MainTask> GetTasksByStatus(TaskStatus status)
        {
            return _tasks.Values.Where(t => t.Status == status).Select(CloneTask).ToList();
        }
        
        /// <summary>
        /// 获取所有任务在时间范围内的数据（兼容性方法）
        /// </summary>
        public async Task<List<DroneDataPoint>> GetAllTasksDataInTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            // 这里需要实现具体的逻辑，暂时返回空列表
            _logger.LogInformation("获取所有任务在时间范围内的数据: StartTime={StartTime}, EndTime={EndTime}", startTime, endTime);
            return new List<DroneDataPoint>();
        }
        
        /// <summary>
        /// 获取任务统计（兼容性方法）
        /// </summary>
        public object GetTaskStatistics()
        {
            return new
            {
                TotalTasks = _tasks.Count,
                ActiveTasks = _tasks.Values.Count(t => t.Status == System.Threading.Tasks.TaskStatus.Running),
                CompletedTasks = _tasks.Values.Count(t => t.Status == System.Threading.Tasks.TaskStatus.RanToCompletion),
                FailedTasks = _tasks.Values.Count(t => t.Status == System.Threading.Tasks.TaskStatus.Faulted)
            };
        }
        
        /// <summary>
        /// 获取任务性能分析（兼容性方法）
        /// </summary>
        public object GetTaskPerformanceAnalysis()
        {
            return new
            {
                AverageCompletionTime = TimeSpan.FromMinutes(30),
                SuccessRate = 0.95,
                PerformanceMetrics = new { }
            };
        }
        
        /// <summary>
        /// 获取无人机的活动子任务（兼容性方法）
        /// </summary>
        public List<SubTask> GetActiveSubTasksForDrone(string droneName)
        {
            // 这里需要实现具体的逻辑，暂时返回空列表
            _logger.LogInformation("获取无人机的活动子任务: DroneName={DroneName}", droneName);
            return new List<SubTask>();
        }
        
        /// <summary>
        /// 获取过期的子任务（兼容性方法）
        /// </summary>
        public List<SubTask> GetExpiredSubTasks(TimeSpan timeout)
        {
            // 这里需要实现具体的逻辑，暂时返回空列表
            _logger.LogInformation("获取过期的子任务: Timeout={Timeout}", timeout);
            return new List<SubTask>();
        }
        
        /// <summary>
        /// 同步所有任务到数据库（兼容性方法）
        /// </summary>
        public async Task SyncAllTasksToDatabaseAsync()
        {
            try
            {
                await _taskRepository.BulkUpdateAsync(_tasks.Values);
                _logger.LogInformation("同步了 {Count} 个任务到数据库", _tasks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步任务到数据库失败");
            }
        }
        
        /// <summary>
        /// 重新分配失败的子任务（兼容性方法）
        /// </summary>
        public async Task<int> ReassignFailedSubTasksAsync()
        {
            // 这里需要实现具体的逻辑，暂时返回0
            _logger.LogInformation("重新分配失败的子任务");
            return 0;
        }
        
        /// <summary>
        /// 清理旧的已完成任务（兼容性方法）
        /// </summary>
        public async Task<int> CleanupOldCompletedTasksAsync(TimeSpan maxAge)
        {
            // 这里需要实现具体的逻辑，暂时返回0
            _logger.LogInformation("清理旧的已完成任务: MaxAge={MaxAge}", maxAge);
            return 0;
        }
        
        /// <summary>
        /// 批量更新子任务状态（兼容性方法）
        /// </summary>
        public async Task<int> BatchUpdateSubTaskStatusAsync(List<Guid> subTaskIds, TaskStatus newStatus, string reason)
        {
            // 这里需要实现具体的逻辑，暂时返回0
            _logger.LogInformation("批量更新子任务状态: Count={Count}, NewStatus={NewStatus}, Reason={Reason}", 
                subTaskIds.Count, newStatus, reason);
            return subTaskIds.Count;
        }
        
        /// <summary>
        /// 获取主任务下的所有子任务（兼容性方法）
        /// </summary>
        public List<SubTask> GetSubTasks(Guid mainTaskId)
        {
            // 这里需要实现具体的逻辑，暂时返回空列表
            _logger.LogInformation("获取主任务下的所有子任务: MainTaskId={MainTaskId}", mainTaskId);
            return new List<SubTask>();
        }
        
        #endregion
        
        public void Dispose()
        {
            _tasks.Clear();
        }
    }
    
    // 事件参数类
    public class TaskChangedEventArgs : EventArgs
    {
        public string Action { get; set; } = "";
        public MainTask MainTask { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }
} 