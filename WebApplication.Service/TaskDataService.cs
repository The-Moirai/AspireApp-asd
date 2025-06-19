using WebApplication.Data;
using ClassLibrary_Core.Mission;
using ClassLibrary_Core.Data;
using ClassLibrary_Core.Common;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace WebApplication.Service
{
    public class TaskDataService : ITaskDataService
    {
        private readonly IDatabaseService _database;
        private readonly ILogger<TaskDataService> _logger;
        private readonly ConcurrentDictionary<Guid, MainTask> _taskCache = new();
        private readonly object _lock = new();

        public event EventHandler<TaskChangedEventArgs>? TaskChanged;

        public TaskDataService(IDatabaseService database, ILogger<TaskDataService> logger)
        {
            _database = database;
            _logger = logger;
        }

        protected virtual void OnTaskChanged(string action, MainTask mainTask, SubTask? subTask = null)
        {
            TaskChanged?.Invoke(this, new TaskChangedEventArgs
            {
                Action = action,
                MainTask = CloneTask(mainTask),
                SubTask = subTask != null ? CloneSubTask(subTask) : new SubTask()
            });
        }

        #region 主任务管理

        public async Task<List<MainTask>> GetTasksAsync()
        {
            const string sql = @"
                SELECT Id, Description, Status, CreationTime, CompletedTime 
                FROM MainTasks 
                ORDER BY CreationTime DESC";
            
            var results = await _database.QueryAsync<MainTask>(sql);
            return results.ToList();
        }

        public async Task<MainTask> GetTaskAsync(Guid id)
        {
            const string sql = @"
                SELECT Id, Description, Status, CreationTime, CompletedTime 
                FROM MainTasks 
                WHERE Id = @Id";
            
            var results = await _database.QueryAsync<MainTask>(sql, new { Id = id });
            return results.FirstOrDefault();
        }

        public async Task AddTaskAsync(MainTask task, string createdBy)
        {
            if (task.Id == Guid.Empty)
                task.Id = Guid.NewGuid();
            
            task.CreationTime = DateTime.UtcNow;
            task.Status = System.Threading.Tasks.TaskStatus.Created;

            const string sql = @"
                INSERT INTO MainTasks (Id, Description, Status, CreationTime, CreatedBy)
                VALUES (@Id, @Description, @Status, @CreationTime, @CreatedBy)";
            
            await _database.ExecuteAsync(sql, new 
            { 
                task.Id,
                task.Description,
                Status = (int)task.Status,
                task.CreationTime,
                CreatedBy = createdBy
            });

            _taskCache.TryAdd(task.Id, CloneTask(task));
            OnTaskChanged("Add", task);
        }

        public async Task<bool> UpdateTaskAsync(MainTask task)
        {
            const string sql = @"
                UPDATE MainTasks 
                SET Description = @Description, Status = @Status, CompletedTime = @CompletedTime
                WHERE Id = @Id";
            
            var result = await _database.ExecuteAsync(sql, new 
            { 
                task.Id,
                task.Description,
                Status = (int)task.Status,
                task.CompletedTime
            }) > 0;

            if (result)
            {
                _taskCache.AddOrUpdate(task.Id, task, (key, oldValue) => CloneTask(task));
                OnTaskChanged("Update", task);
            }

            return result;
        }

        public async Task<bool> DeleteTaskAsync(Guid id)
        {
            var task = await GetTaskAsync(id);
            if (task != null)
            {
                const string sql = "DELETE FROM MainTasks WHERE Id = @Id";
                var result = await _database.ExecuteAsync(sql, new { Id = id }) > 0;

                if (result)
                {
                    _taskCache.TryRemove(id, out _);
                    OnTaskChanged("Delete", task);
                }

                return result;
            }
            return false;
        }

        public async Task<List<MainTask>> GetTasksByStatusAsync(System.Threading.Tasks.TaskStatus status)
        {
            const string sql = @"
                SELECT Id, Description, Status, CreationTime, CompletedTime 
                FROM MainTasks 
                WHERE Status = @Status
                ORDER BY CreationTime DESC";
            
            var results = await _database.QueryAsync<MainTask>(sql, new { Status = (int)status });
            return results.ToList();
        }

        #endregion

        #region 子任务管理

        public async Task<List<SubTask>> GetSubTasksAsync(Guid mainTaskId)
        {
            const string sql = @"
                SELECT Id, Description, Status, CreationTime, AssignedTime, CompletedTime, 
                       ParentTask, ReassignmentCount, AssignedDrone 
                FROM SubTasks 
                WHERE ParentTask = @MainTaskId 
                ORDER BY CreationTime";
            
            var results = await _database.QueryAsync<SubTask>(sql, new { MainTaskId = mainTaskId });
            return results.ToList();
        }

        public async Task<SubTask> GetSubTaskAsync(Guid mainTaskId, Guid subTaskId)
        {
            const string sql = @"
                SELECT Id, Description, Status, CreationTime, AssignedTime, CompletedTime, 
                       ParentTask, ReassignmentCount, AssignedDrone 
                FROM SubTasks 
                WHERE ParentTask = @MainTaskId AND Id = @SubTaskId";
            
            var results = await _database.QueryAsync<SubTask>(sql, new { MainTaskId = mainTaskId, SubTaskId = subTaskId });
            return results.FirstOrDefault();
        }

        public async Task AddSubTaskAsync(Guid mainTaskId, SubTask subTask)
        {
            if (subTask.Id == Guid.Empty)
                subTask.Id = Guid.NewGuid();
            
            subTask.ParentTask = mainTaskId;
            subTask.Status = System.Threading.Tasks.TaskStatus.Created;
            subTask.CreationTime = DateTime.UtcNow;

            const string sql = @"
                INSERT INTO SubTasks (Id, Description, Status, CreationTime, ParentTask, ReassignmentCount, AssignedDrone)
                VALUES (@Id, @Description, @Status, @CreationTime, @ParentTask, @ReassignmentCount, @AssignedDrone)";
            
            await _database.ExecuteAsync(sql, new 
            { 
                subTask.Id,
                subTask.Description,
                Status = (int)subTask.Status,
                subTask.CreationTime,
                subTask.ParentTask,
                subTask.ReassignmentCount,
                subTask.AssignedDrone
            });

            var mainTask = await GetTaskAsync(mainTaskId);
            if (mainTask != null)
            {
                OnTaskChanged("SubTaskAdded", mainTask, subTask);
            }
        }

        public async Task<bool> UpdateSubTaskAsync(Guid mainTaskId, SubTask subTask)
        {
            const string sql = @"
                UPDATE SubTasks 
                SET Description = @Description, Status = @Status, AssignedTime = @AssignedTime, 
                    CompletedTime = @CompletedTime, ReassignmentCount = @ReassignmentCount, AssignedDrone = @AssignedDrone
                WHERE Id = @Id";
            
            var result = await _database.ExecuteAsync(sql, new 
            { 
                subTask.Id,
                subTask.Description,
                Status = (int)subTask.Status,
                subTask.AssignedTime,
                subTask.CompletedTime,
                subTask.ReassignmentCount,
                subTask.AssignedDrone
            }) > 0;

            if (result)
            {
                var mainTask = await GetTaskAsync(mainTaskId);
                if (mainTask != null)
                {
                    OnTaskChanged("SubTaskUpdated", mainTask, subTask);
                }
            }

            return result;
        }

        public async Task<bool> DeleteSubTaskAsync(Guid mainTaskId, Guid subTaskId)
        {
            var subTask = await GetSubTaskAsync(mainTaskId, subTaskId);
            if (subTask != null)
            {
                const string sql = "DELETE FROM SubTasks WHERE Id = @Id";
                var result = await _database.ExecuteAsync(sql, new { Id = subTaskId }) > 0;

                if (result)
                {
                    var mainTask = await GetTaskAsync(mainTaskId);
                    if (mainTask != null)
                    {
                        OnTaskChanged("SubTaskDeleted", mainTask, subTask);
                    }
                }

                return result;
            }
            return false;
        }

        #endregion

        #region 任务分配和完成

        public async Task<bool> AssignSubTaskAsync(Guid mainTaskId, Guid subTaskId, string droneName)
        {
            var subTask = await GetSubTaskAsync(mainTaskId, subTaskId);
            if (subTask != null)
            {
                subTask.AssignedDrone = droneName;
                subTask.Status = System.Threading.Tasks.TaskStatus.Running;
                subTask.AssignedTime = DateTime.UtcNow;

                var result = await UpdateSubTaskAsync(mainTaskId, subTask);
                if (result)
                {
                    var mainTask = await GetTaskAsync(mainTaskId);
                    if (mainTask != null)
                    {
                        OnTaskChanged("SubTaskAssigned", mainTask, subTask);
                    }
                }
                return result;
            }
            return false;
        }

        public async Task<bool> CompleteSubTaskAsync(Guid mainTaskId, string subTaskDescription)
        {
            const string sql = @"
                SELECT Id, Description, Status, CreationTime, AssignedTime, CompletedTime, 
                       ParentTask, ReassignmentCount, AssignedDrone 
                FROM SubTasks 
                WHERE ParentTask = @MainTaskId AND Description = @Description";
            
            var results = await _database.QueryAsync<SubTask>(sql, new { MainTaskId = mainTaskId, Description = subTaskDescription });
            var subTask = results.FirstOrDefault();

            if (subTask != null)
            {
                subTask.Status = System.Threading.Tasks.TaskStatus.RanToCompletion;
                subTask.CompletedTime = DateTime.UtcNow;

                var result = await UpdateSubTaskAsync(mainTaskId, subTask);
                if (result)
                {
                    var mainTask = await GetTaskAsync(mainTaskId);
                    if (mainTask != null)
                    {
                        OnTaskChanged("SubTaskCompleted", mainTask, subTask);
                    }
                }
                return result;
            }
            return false;
        }

        public async Task<bool> UnloadSubTaskAsync(Guid mainTaskId, Guid subTaskId)
        {
            var subTask = await GetSubTaskAsync(mainTaskId, subTaskId);
            if (subTask != null)
            {
                subTask.AssignedDrone = null;
                subTask.Status = System.Threading.Tasks.TaskStatus.Created;
                subTask.AssignedTime = null;
                subTask.CompletedTime = null;

                var result = await UpdateSubTaskAsync(mainTaskId, subTask);
                if (result)
                {
                    var mainTask = await GetTaskAsync(mainTaskId);
                    if (mainTask != null)
                    {
                        OnTaskChanged("SubTaskUnloaded", mainTask, subTask);
                    }
                }
                return result;
            }
            return false;
        }

        public async Task<bool> ReloadSubTaskAsync(Guid mainTaskId, Guid subTaskId, string droneName)
        {
            return await AssignSubTaskAsync(mainTaskId, subTaskId, droneName);
        }

        #endregion

        #region 数据查询 - 简化版本

        public async Task<List<DroneDataPoint>> GetTaskDroneDataAsync(Guid taskId, Guid droneId)
        {
            // 简化实现 - 返回空列表
            return new List<DroneDataPoint>();
        }

        public async Task<List<DroneDataPoint>> GetTaskAllDronesDataAsync(Guid taskId)
        {
            // 简化实现 - 返回空列表
            return new List<DroneDataPoint>();
        }

        public async Task<List<DroneDataPoint>> GetAllTasksDataInTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            // 简化实现 - 返回空列表
            return new List<DroneDataPoint>();
        }

        #endregion

        #region 批量操作 - 简化版本

        public async Task<int> BatchUpdateSubTaskStatusAsync(List<Guid> subTaskIds, System.Threading.Tasks.TaskStatus newStatus, string reason = null)
        {
            if (!subTaskIds.Any()) return 0;
            
            var idsParam = string.Join(",", subTaskIds.Select(id => $"'{id}'"));
            var sql = $@"
                UPDATE SubTasks 
                SET Status = @Status, CompletedTime = CASE WHEN @Status = 5 THEN GETUTCDATE() ELSE CompletedTime END
                WHERE Id IN ({idsParam})";
            
            return await _database.ExecuteAsync(sql, new { Status = (int)newStatus });
        }

        public async Task<int> ReassignFailedSubTasksAsync()
        {
            const string sql = @"
                UPDATE SubTasks 
                SET Status = 0, AssignedDrone = NULL, AssignedTime = NULL, CompletedTime = NULL
                WHERE Status = 7";  // Faulted
            
            return await _database.ExecuteAsync(sql);
        }

        public async Task<int> CleanupOldCompletedTasksAsync(TimeSpan maxAge)
        {
            var cutoffDate = DateTime.UtcNow - maxAge;
            const string sql = @"
                DELETE FROM MainTasks 
                WHERE Status = 5 AND CreationTime < @CutoffDate";  // RanToCompletion
            
            return await _database.ExecuteAsync(sql, new { CutoffDate = cutoffDate });
        }

        #endregion

        #region 统计和分析 - 简化版本

        public async Task<TaskStatistics> GetTaskStatisticsAsync()
        {
            const string mainTaskSql = "SELECT Status, COUNT(*) as Count FROM MainTasks GROUP BY Status";
            const string subTaskSql = "SELECT Status, COUNT(*) as Count FROM SubTasks GROUP BY Status";
            
            // 简化实现 - 返回基本统计
            return new TaskStatistics
            {
                TotalTasks = 0,
                CompletedTasks = 0,
                InProgressTasks = 0,
                PendingTasks = 0,
                FailedTasks = 0,
                TotalSubTasks = 0,
                CompletedSubTasks = 0,
                InProgressSubTasks = 0,
                PendingSubTasks = 0,
                FailedSubTasks = 0
            };
        }

        public async Task<TaskPerformanceAnalysis> GetTaskPerformanceAnalysisAsync()
        {
            // 简化实现
            return new TaskPerformanceAnalysis
            {
                AverageCompletionTime = 0,
                TasksCompletedToday = 0,
                TasksCompletedThisWeek = 0,
                SuccessRate = 0,
                DronePerformance = new List<DronePerformanceMetric>()
            };
        }

        public async Task<List<SubTask>> GetExpiredSubTasksAsync(TimeSpan timeout)
        {
            var cutoffTime = DateTime.UtcNow - timeout;
            const string sql = @"
                SELECT Id, Description, Status, CreationTime, AssignedTime, CompletedTime, 
                       ParentTask, ReassignmentCount, AssignedDrone 
                FROM SubTasks 
                WHERE Status = 3 AND AssignedTime < @CutoffTime";  // Running
            
            var results = await _database.QueryAsync<SubTask>(sql, new { CutoffTime = cutoffTime });
            return results.ToList();
        }

        public async Task<List<SubTask>> GetActiveSubTasksForDroneAsync(string droneName)
        {
            const string sql = @"
                SELECT Id, Description, Status, CreationTime, AssignedTime, CompletedTime, 
                       ParentTask, ReassignmentCount, AssignedDrone 
                FROM SubTasks 
                WHERE AssignedDrone = @DroneName AND Status IN (0, 3)";  // Created, Running
            
            var results = await _database.QueryAsync<SubTask>(sql, new { DroneName = droneName });
            return results.ToList();
        }

        #endregion

        #region 数据库同步

        public async Task LoadTasksFromDatabaseAsync()
        {
            var tasks = await GetTasksAsync();
            _taskCache.Clear();
            foreach (var task in tasks)
            {
                _taskCache.TryAdd(task.Id, CloneTask(task));
            }
        }

        public async Task SyncAllTasksToDatabaseAsync()
        {
            // 由于我们已经在直接操作数据库，这个方法主要用于缓存同步
            await LoadTasksFromDatabaseAsync();
        }

        #endregion

        #region 文件上传 - 简化版本

        public async Task<TaskUploadDto> SaveTaskWithVideoAsync(TaskUploadDto taskUpload, object videoFile)
        {
            // 简化实现 - 不处理文件上传
            taskUpload.UploadTime = DateTime.UtcNow;
            
            // 创建主任务
            var mainTask = new MainTask
            {
                Id = taskUpload.Id,
                Description = taskUpload.Description,
                CreationTime = taskUpload.CreationTime,
                Status = System.Threading.Tasks.TaskStatus.Created
            };

            await AddTaskAsync(mainTask, "System");
            return taskUpload;
        }

        #endregion

        #region 私有方法

        private static MainTask CloneTask(MainTask task)
        {
            return new MainTask
            {
                Id = task.Id,
                Description = task.Description,
                CreationTime = task.CreationTime,
                Status = task.Status,
                CompletedTime = task.CompletedTime,
                SubTasks = new List<SubTask>()
            };
        }

        private static SubTask CloneSubTask(SubTask subTask)
        {
            return new SubTask
            {
                Id = subTask.Id,
                ParentTask = subTask.ParentTask,
                Description = subTask.Description,
                AssignedDrone = subTask.AssignedDrone,
                Status = subTask.Status,
                AssignedTime = subTask.AssignedTime,
                CompletedTime = subTask.CompletedTime,
                ReassignmentCount = subTask.ReassignmentCount,
                CreationTime = subTask.CreationTime
            };
        }

        #endregion
    }
} 