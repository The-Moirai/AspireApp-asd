using ClassLibrary_Core.Data;
using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Mission;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

namespace WebApplication_Drone.Services
{
    public class TaskDataService
    {
        private readonly List<MainTask> _tasks = new();
        private readonly object _lock = new();
        private readonly SqlserverService _sqlserverService;

        public TaskDataService(SqlserverService sqlserverService)
        {
            _sqlserverService = sqlserverService;
        }

        /// <summary>
        /// 任务数据变更事件
        /// </summary>
        public event EventHandler<TaskChangedEventArgs>? TaskChanged;

        /// <summary>
        /// 任务数据变更事件触发方法
        /// </summary>
        /// <param name="action"></param>
        /// <param name="drone"></param>
        protected virtual void OnDroneChanged(string action, MainTask mainTask)
        {
            TaskChanged?.Invoke(this, new TaskChangedEventArgs
            {
                Action = action,
                MainTask = CloneTask(mainTask) // 深度克隆
            });
        }

       
        /// <summary>
        /// 获取所有大任务数据的副本
        /// </summary>
        /// <returns>任务列表</returns>
        public List<MainTask> GetTasks()
        {
            lock (_lock)
            {
                return _tasks.Select(t => t).ToList();
            }
        }
        /// <summary>
        /// 获取指定ID的大任务
        /// </summary>
        /// <param name="id">指定大任务id</param>
        /// <returns>大任务实体</returns>
        public MainTask? GetTask(Guid id)
        {
            lock (_lock)
            {
                return _tasks.FirstOrDefault(t => t.Id == id);
            }
        }
        /// <summary>
        /// 大任务数据设置
        /// </summary>
        /// <param name="tasks">大任务数据列表</param>
        public void SetTasks(IEnumerable<MainTask> tasks)
        {
            lock (_lock)
            {
                _tasks.Clear();
                _tasks.AddRange(tasks);
            }
        }
        /// <summary>
        /// 添加新的大任务
        /// </summary>
        /// <param name="task">
        /// 大任务实体
        /// </param>
        public void AddTask(MainTask task,string CreatedBy)
        {
            lock (_lock)
            {
                if (!_tasks.Any(t => t.Id == task.Id))
                    _tasks.Add(task);
                OnDroneChanged("add",task);
                _sqlserverService.AddMainTask(task,CreatedBy);
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
            lock (_lock)
            {
                var idx = _tasks.FindIndex(t => t.Id == task.Id);
                if (idx >= 0)
                {
                    _tasks[idx] = task;
                  
                    // 触发任务变更事件
                    OnDroneChanged("update", task);
                    //数据库更新操作

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
            lock (_lock)
            {
                var task = _tasks.FirstOrDefault(t => t.Id == id);
                if (task != null)
                {
                    _tasks.Remove(task);
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
            lock (_lock)
            {
                var mainTask = _tasks.FirstOrDefault(t => t.Id == mainTaskId);
                if (mainTask == null) return false;
                var subTask = mainTask.SubTasks.FirstOrDefault(st => st.Id == subTaskId);
                if (subTask == null) return false;                
                subTask.AssignedDrone = null;
                subTask.Status = TaskStatus.WaitingForActivation;
                subTask.AssignedTime = null;
                subTask.CompletedTime = null;
                
                // 触发任务变更事件
                OnDroneChanged("update", mainTask);
                return true;
            }
        }
        /// <summary>
        /// 重装（重新分配）指定子任务到无人机
        /// </summary>
        public bool ReloadSubTask(Guid mainTaskId, Guid subTaskId, string droneName)
        {
            lock (_lock)
            {
                var mainTask = _tasks.FirstOrDefault(t => t.Id == mainTaskId);
                if (mainTask == null) return false;
                var subTask = mainTask.SubTasks.FirstOrDefault(st => st.Id == subTaskId);
                if (subTask == null) return false;
                
                subTask.AssignedDrone = droneName;
                subTask.Status = TaskStatus.Running;
                subTask.AssignedTime = DateTime.Now;
                
                // 触发任务变更事件
                OnDroneChanged("update", mainTask);
                return true;
            }
        }
        /// <summary>
        /// 分配子任务到无人机
        /// </summary>
        public bool AssignSubTask(Guid mainTaskId,string subTasksName, string droneName)
        {
            lock (_lock)
            {
                var mainTask = _tasks.FirstOrDefault(t => t.Id == mainTaskId);
                if (mainTask == null) return false;
                var subTask = mainTask.SubTasks.FirstOrDefault(st => st.Description == subTasksName);
                if (subTask == null) return false;
                
                subTask.AssignedDrone = droneName;
                subTask.Status = TaskStatus.Running;
                subTask.AssignedTime = DateTime.Now;
                
                // 触发任务变更事件
                OnDroneChanged("update", mainTask);
                return true;
            }
        }
        /// <summary>
        /// 完成指定子任务
        /// </summary>
        public bool CompleteSubTask(Guid mainTaskId, string subTaskId)
        {
            lock (_lock)
            {
                var mainTask = _tasks.FirstOrDefault(t => t.Id == mainTaskId);
                if (mainTask == null) return false;
                var subTask = mainTask.SubTasks.FirstOrDefault(st => st.Description == subTaskId);
                if (subTask == null) return false;

                subTask.Status = TaskStatus.RanToCompletion;
                subTask.CompletedTime = DateTime.Now;
                
                // 触发任务变更事件
                OnDroneChanged("update", mainTask);
                return true;
            }
        }
        /// <summary>
        /// 获取指定大任务下的所有子任务
        /// </summary>
        public List<SubTask> GetSubTasks(Guid mainTaskId)
        {
            lock (_lock)
            {
                var mainTask = _tasks.FirstOrDefault(t => t.Id == mainTaskId);
                return mainTask?.SubTasks.ToList() ?? new List<SubTask>();
            }
        }

        /// <summary>
        /// 子任务装载在主任务
        /// </summary>
        /// <param name="mainTaskId">主任务uuid</param>
        /// <param name="subTask">子任务实体</param>
        public void addSubTasks(Guid mainTaskId, SubTask subTask)
        {
            lock (_lock)
            {
                var mainTask = _tasks.FirstOrDefault(t => t.Id == mainTaskId);
                if (mainTask != null)
                {
                    mainTask.SubTasks.Add(subTask);
                    // 触发任务变更事件
                    OnDroneChanged("update", mainTask);
                }
            }
        }
        /// <summary>
        /// 获取指定子任务
        /// </summary>
        public SubTask? GetSubTask(Guid mainTaskId, Guid subTaskId)
        {
            lock (_lock)
            {
                var mainTask = _tasks.FirstOrDefault(t => t.Id == mainTaskId);
                return mainTask?.SubTasks.FirstOrDefault(st => st.Id == subTaskId);
            }
        }
        /// <summary>
        /// 子任务装载在主任务
        /// </summary>
        /// <param name="mainTaskId">主任务Guid</param>
        /// <param name="subTask">子任务实体</param>
        public void addSubTasks(Guid mainTaskId, SubTask subTask)
        {
            lock (_lock)
            {
                var mainTask = _tasks.FirstOrDefault(t => t.Id == mainTaskId);
                if (mainTask != null)
                {
                    mainTask.SubTasks.Add(subTask);
                   // 触发任务变更事件
                    OnDroneChanged("update", mainTask);
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
        /// <exception cref="NotImplementedException"></exception>
        public async Task<List<DroneDataPoint>> GetAllTasksDataInTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            List<DroneDataPoint> recentData = new List<DroneDataPoint>();
            return recentData;
        }
        // 新增：深度克隆方法
        public static MainTask CloneTask(MainTask task) => new()
        {
            Id = task.Id,
            Description = task.Description,
            SubTasks = task.SubTasks.Select(CloneSubTask).ToList()
        };

        private static SubTask CloneSubTask(SubTask st) => new()
        {
            Id = st.Id,
            Description = st.Description,
            AssignedDrone = st.AssignedDrone,
            Status = st.Status
        };
    }

    /// <summary>
    /// 无人机数据变更事件参数
    /// </summary>
    public class TaskChangedEventArgs : EventArgs
    {
        public string Action { get; set; } = "";
        public MainTask MainTask { get; set; }
        public SubTask SubTask { get; set; } = new SubTask();
    }
}
