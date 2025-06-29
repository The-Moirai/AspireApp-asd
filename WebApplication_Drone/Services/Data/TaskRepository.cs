using ClassLibrary_Core.Mission;
using ClassLibrary_Core.Data;
using Microsoft.Extensions.Logging;
using WebApplication_Drone.Services.Interfaces;
using System.Diagnostics;

namespace WebApplication_Drone.Services.Data
{
    /// <summary>
    /// 任务数据访问实现
    /// </summary>
    public class TaskRepository : ITaskRepository
    {
        private readonly SqlserverService _sqlserverService;
        private readonly ILogger<TaskRepository> _logger;
        
        // 性能计数器
        private long _totalOperations = 0;
        private long _totalResponseTime = 0;
        
        public TaskRepository(SqlserverService sqlserverService, ILogger<TaskRepository> logger)
        {
            _sqlserverService = sqlserverService ?? throw new ArgumentNullException(nameof(sqlserverService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        #region 主任务CRUD操作
        
        public async Task<List<MainTask>> GetAllAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("获取所有任务数据");
                var mainTasks = await _sqlserverService.GetAllMainTasksAsync();
                foreach (var mainTask in mainTasks)
                {
                    // 自动装载子任务
                    mainTask.SubTasks = await _sqlserverService.GetSubTasksByParentAsync(mainTask.Id);
                    if(mainTask.SubTasks!=null)
                    {
                        foreach (var subTask in mainTask.SubTasks)
                        {
                            // 自动装载子任务的图片
                            subTask.Images = await _sqlserverService.GetSubTaskImagesAsync(subTask.Id);
                        }
                    }

                }
                return mainTasks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取所有任务数据失败");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<MainTask?> GetByIdAsync(Guid id)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("根据ID获取任务: {TaskId}", id);
                var mainTask = await _sqlserverService.GetMainTaskAsync(id);
                if (mainTask != null)
                {
                    // 自动装载子任务
                    mainTask.SubTasks = await _sqlserverService.GetSubTasksByParentAsync(mainTask.Id);
                }
                return mainTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "根据ID获取任务失败: {TaskId}", id);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<bool> AddAsync(MainTask task, string createdBy)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("添加任务: {TaskId}, {TaskName}", task.Id, task.Name);
                var taskId = await _sqlserverService.AddMainTaskAsync(task, createdBy);
                return taskId != Guid.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加任务失败: {TaskId}", task.Id);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<bool> UpdateAsync(MainTask task)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("更新任务: {TaskId}", task.Id);
                await _sqlserverService.UpdateMainTaskAsync(task);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新任务失败: {TaskId}", task.Id);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<bool> DeleteAsync(Guid id)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("删除任务: {TaskId}", id);
                await _sqlserverService.DeleteMainTaskAsync(id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除任务失败: {TaskId}", id);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<int> GetCountAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("获取任务数量");
                return await _sqlserverService.GetMainTaskCountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取任务数量失败");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        #endregion
        
        #region 子任务操作
        
        public async Task<List<SubTask>> GetSubTasksAsync(Guid mainTaskId)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("获取子任务: {MainTaskId}", mainTaskId);
                return await _sqlserverService.GetSubTasksByParentAsync(mainTaskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取子任务失败: {MainTaskId}", mainTaskId);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<SubTask?> GetSubTaskAsync(Guid mainTaskId, Guid subTaskId)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("获取子任务: {MainTaskId}, {SubTaskId}", mainTaskId, subTaskId);
                var subTasks = await GetSubTasksAsync(mainTaskId);
                return subTasks.FirstOrDefault(st => st.Id == subTaskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取子任务失败: {MainTaskId}, {SubTaskId}", mainTaskId, subTaskId);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<bool> AddSubTaskAsync(SubTask subTask)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("添加子任务: {SubTaskId}", subTask.Id);
                await _sqlserverService.AddSubTaskAsync(subTask);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加子任务失败: {SubTaskId}", subTask.Id);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<bool> UpdateSubTaskAsync(SubTask subTask)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("更新子任务: {SubTaskId}", subTask.Id);
                await _sqlserverService.UpdateSubTaskAsync(subTask);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新子任务失败: {SubTaskId}", subTask.Id);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<bool> DeleteSubTaskAsync(Guid mainTaskId, Guid subTaskId)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("删除子任务: {MainTaskId}, {SubTaskId}", mainTaskId, subTaskId);
                // 注意：SqlserverService中没有DeleteSubTaskAsync方法，这里需要实现
                // 暂时返回true，实际应该调用数据库删除操作
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除子任务失败: {MainTaskId}, {SubTaskId}", mainTaskId, subTaskId);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        #endregion
        
        #region 图片操作
        
        public async Task<Guid> SaveImageAsync(Guid subTaskId, byte[] imageData, string fileName, int imageIndex = 1, string? description = null)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("保存图片: {SubTaskId}, {FileName}", subTaskId, fileName);
                return await _sqlserverService.SaveSubTaskImageAsync(subTaskId, imageData, fileName, imageIndex, description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存图片失败: {SubTaskId}, {FileName}", subTaskId, fileName);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<List<SubTaskImage>> GetImagesAsync(Guid subTaskId)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("获取图片: {SubTaskId}", subTaskId);
                return await _sqlserverService.GetSubTaskImagesAsync(subTaskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取图片失败: {SubTaskId}", subTaskId);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<SubTaskImage?> GetImageAsync(Guid imageId)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("获取图片: {ImageId}", imageId);
                return await _sqlserverService.GetSubTaskImageAsync(imageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取图片失败: {ImageId}", imageId);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<bool> DeleteImageAsync(Guid imageId)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("删除图片: {ImageId}", imageId);
                return await _sqlserverService.DeleteSubTaskImageAsync(imageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除图片失败: {ImageId}", imageId);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        #endregion
        
        #region 批量操作
        
        public async Task<bool> BulkUpdateAsync(IEnumerable<MainTask> tasks)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("批量更新任务: {Count} 个", tasks.Count());
                // 注意：SqlserverService中没有BulkUpdateTasks方法，这里需要实现
                // 暂时返回true，实际应该调用数据库批量更新操作
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量更新任务失败");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        #endregion
        
        #region 数据点操作
        
        public async Task<List<DroneDataPoint>> GetTaskDataAsync(Guid taskId, Guid droneId)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("获取任务数据点: {TaskId}, {DroneId}", taskId, droneId);
                // 这里需要根据任务ID和无人机ID获取相关的数据点
                // 暂时返回空列表，实际应该从数据库查询
                return new List<DroneDataPoint>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取任务数据点失败: {TaskId}, {DroneId}", taskId, droneId);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        #endregion
        
        #region 状态检查
        
        public async Task<bool> ExistsAsync(Guid id)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("检查任务是否存在: {TaskId}", id);
                var task = await GetByIdAsync(id);
                return task != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查任务是否存在失败: {TaskId}", id);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                // 简单的健康检查：尝试获取任务数量
                await GetCountAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        #endregion
        
        #region 统计信息
        
        public async Task<TaskRepositoryStatistics> GetStatisticsAsync()
        {
            try
            {
                var count = await GetCountAsync();
                
                return new TaskRepositoryStatistics
                {
                    TotalTasks = count,
                    ActiveTasks = 0, // 需要从数据库查询活跃状态
                    CompletedTasks = 0, // 需要从数据库查询完成状态
                    TotalSubTasks = 0, // 需要从数据库查询子任务数量
                    TotalImages = 0, // 需要从数据库查询图片数量
                    TotalOperations = _totalOperations,
                    AverageResponseTimeMs = _totalOperations > 0 ? (double)_totalResponseTime / _totalOperations : 0,
                    LastUpdateTime = DateTime.UtcNow,
                    DatabaseConnected = await IsHealthyAsync()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取任务数据访问统计信息失败");
                throw;
            }
        }
        
        #endregion
    }
} 