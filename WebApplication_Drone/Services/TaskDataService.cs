using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Mission;

namespace WebApplication_Drone.Services
{
    public class TaskDataService
    {
        private readonly List<MainTask> _tasks = new();
        private readonly List<MissionHistory> _history = new();
        private readonly object _lock = new();


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
            TaskChanged?.Invoke(this, new TaskChangedEventArgs { Action = action, MainTask = mainTask });
        }

        /// <summary>
        /// 添加历史记录
        /// </summary>
        private void AddHistory(SubTask subTask, string operation)
        {
            _history.Add(new MissionHistory
            {
                SubTaskDescription = subTask.Description,
                SubTaskId = subTask.Id,
                Operation = operation,
                DroneName = subTask.AssignedDrone,
                Time = DateTime.Now
            });
        }
        /// <summary>
        /// 获取所有历史记录
        /// </summary>
        public List<MissionHistory> GetHistory()
        {
            lock (_lock)
            {
                return _history.ToList();
            }
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
        public void AddTask(MainTask task)
        {
            lock (_lock)
            {
                if (!_tasks.Any(t => t.Id == task.Id))
                    _tasks.Add(task);

            }
        }
        /// <summary>
        /// 更新指定的大任务信息
        /// </summary>
        /// <param name="task">
        /// 大任务实体
        /// </param>
        /// <returns>
        /// 返回更新成功与否
        /// </returns>
        public bool UpdateTask(MainTask task)
        {
            lock (_lock)
            {
                var idx = _tasks.FindIndex(t => t.Id == task.Id);
                if (idx >= 0)
                {
                    _tasks[idx] = task;
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

                AddHistory(subTask, "卸载");
                subTask.AssignedDrone = null;
                subTask.Status = TaskStatus.WaitingForActivation;
                subTask.AssignedTime = null;
                subTask.CompletedTime = null;
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

                AddHistory(subTask, "重装");
                subTask.AssignedDrone = droneName;
                subTask.Status = TaskStatus.Running;
                subTask.AssignedTime = DateTime.Now;
                return true;
            }
        }
        /// <summary>
        /// 分配子任务到无人机
        /// </summary>
        public bool AssignSubTask(Guid mainTaskId, Guid subTaskId, string droneName)
        {
            lock (_lock)
            {
                var mainTask = _tasks.FirstOrDefault(t => t.Id == mainTaskId);
                if (mainTask == null) return false;
                var subTask = mainTask.SubTasks.FirstOrDefault(st => st.Id == subTaskId);
                if (subTask == null) return false;

                AddHistory(subTask, "分配");
                subTask.AssignedDrone = droneName;
                subTask.Status = TaskStatus.Running;
                subTask.AssignedTime = DateTime.Now;
                return true;
            }
        }
        /// <summary>
        /// 完成指定子任务
        /// </summary>
        public bool CompleteSubTask(Guid mainTaskId, Guid subTaskId)
        {
            lock (_lock)
            {
                var mainTask = _tasks.FirstOrDefault(t => t.Id == mainTaskId);
                if (mainTask == null) return false;
                var subTask = mainTask.SubTasks.FirstOrDefault(st => st.Id == subTaskId);
                if (subTask == null) return false;

                AddHistory(subTask, "完成");
                subTask.Status = TaskStatus.RanToCompletion;
                subTask.CompletedTime = DateTime.Now;
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
