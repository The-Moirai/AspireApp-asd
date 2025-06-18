using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebApplication_Drone.Services;
using ClassLibrary_Core.Data;
using ClassLibrary_Core.Mission;

namespace WebApplication_Drone.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HistoryDataController : ControllerBase
    {
        private readonly DroneDataService _droneDataService;
        private readonly TaskDataService _taskDataService;

        public HistoryDataController(
            DroneDataService droneDataService,
            TaskDataService taskDataService)
        {
            _droneDataService = droneDataService;
            _taskDataService = taskDataService;
        }

        #region 无人机历史数据查询

        // 1. 单个无人机最新一段时间的数据
        [HttpGet("drone/{droneId:guid}/recent")]
        public async Task<IActionResult> GetRecentDroneData(
            Guid droneId,
            [FromQuery] TimeSpan duration)
        {
            try
            {
                var data = await _droneDataService.GetRecentDroneDataAsync(droneId, duration);
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 2. 单个无人机指定任务期间的数据
        [HttpGet("drone/{droneId:guid}/task/{taskId:guid}")]
        public async Task<IActionResult> GetDroneTaskData(
            Guid droneId,
            Guid taskId)
        {
            try
            {
                var data = await _droneDataService.GetDroneTaskDataAsync(droneId, taskId);
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 3. 获取所有无人机记录
        [HttpGet("drones/all")]
        public IActionResult GetAllDronesHistory()
        {
            try
            {
                var history = _droneDataService.GetDrones();
                return Ok(new { success = true, data = history });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 4. 获取指定无人机详细信息
        [HttpGet("drone/{droneId:guid}")]
        public IActionResult GetDroneDetails(Guid droneId)
        {
            try
            {
                var drone = _droneDataService.GetDrone(droneId);
                if (drone == null)
                    return NotFound(new { success = false, message = "无人机未找到" });

                return Ok(new { success = true, data = drone });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 5. 获取指定无人机的子任务
        [HttpGet("drone/{droneId:guid}/subtasks")]
        public IActionResult GetDroneSubTasks(Guid droneId)
        {
            try
            {
                var subTasks = _droneDataService.GetSubTasks(droneId);
                return Ok(new { success = true, data = subTasks });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region 任务历史数据查询

        // 6. 单个任务中指定无人机的数据
        [HttpGet("task/{taskId:guid}/drone/{droneId:guid}")]
        public async Task<IActionResult> GetTaskDroneData(
            Guid taskId,
            Guid droneId)
        {
            try
            {
                var data = await _taskDataService.GetTaskDroneDataAsync(taskId, droneId);
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 7. 单个任务期间所有无人机的数据
        [HttpGet("task/{taskId:guid}/drones")]
        public async Task<IActionResult> GetTaskAllDronesData(Guid taskId)
        {
            try
            {
                var data = await _taskDataService.GetTaskAllDronesDataAsync(taskId);
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 8. 获取所有任务
        [HttpGet("tasks/all")]
        public IActionResult GetAllTasks()
        {
            try
            {
                var tasks = _taskDataService.GetTasks();
                return Ok(new { success = true, data = tasks });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 9. 获取指定任务详细信息
        [HttpGet("task/{taskId:guid}")]
        public IActionResult GetTaskDetails(Guid taskId)
        {
            try
            {
                var task = _taskDataService.GetTask(taskId);
                if (task == null)
                    return NotFound(new { success = false, message = "任务未找到" });

                return Ok(new { success = true, data = task });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 10. 根据状态获取任务
        [HttpGet("tasks/status/{status:int}")]
        public IActionResult GetTasksByStatus(int status)
        {
            try
            {
                if (!Enum.IsDefined(typeof(TaskStatus), status))
                    return BadRequest(new { success = false, message = "无效的任务状态" });

                var tasks = _taskDataService.GetTasksByStatus((TaskStatus)status);
                return Ok(new { success = true, data = tasks });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 11. 获取指定任务的子任务
        [HttpGet("task/{taskId:guid}/subtasks")]
        public IActionResult GetTaskSubTasks(Guid taskId)
        {
            try
            {
                var subTasks = _taskDataService.GetSubTasks(taskId);
                return Ok(new { success = true, data = subTasks });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region 时间范围查询

        // 12. 指定时间段内所有无人机的数据
        [HttpGet("drones/time-range")]
        public async Task<IActionResult> GetAllDronesDataInTimeRange(
            [FromQuery] DateTime startTime,
            [FromQuery] DateTime endTime)
        {
            try
            {
                if (startTime >= endTime)
                    return BadRequest(new { success = false, message = "开始时间必须早于结束时间" });

                var data = await _droneDataService.GetAllDronesDataInTimeRangeAsync(startTime, endTime);
                return Ok(new { success = true, data, timeRange = new { startTime, endTime } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 13. 指定时间段内所有任务的数据
        [HttpGet("tasks/time-range")]
        public async Task<IActionResult> GetAllTasksDataInTimeRange(
            [FromQuery] DateTime startTime,
            [FromQuery] DateTime endTime)
        {
            try
            {
                if (startTime >= endTime)
                    return BadRequest(new { success = false, message = "开始时间必须早于结束时间" });

                var data = await _taskDataService.GetAllTasksDataInTimeRangeAsync(startTime, endTime);
                return Ok(new { success = true, data, timeRange = new { startTime, endTime } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region 统计和分析功能

        // 14. 获取任务统计信息
        [HttpGet("statistics/tasks")]
        public IActionResult GetTaskStatistics()
        {
            try
            {
                var statistics = _taskDataService.GetTaskStatistics();
                return Ok(new { success = true, data = statistics });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 15. 获取任务性能分析
        [HttpGet("analysis/task-performance")]
        public IActionResult GetTaskPerformanceAnalysis()
        {
            try
            {
                var analysis = _taskDataService.GetTaskPerformanceAnalysis();
                return Ok(new { success = true, data = analysis });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 16. 获取指定无人机的活跃子任务
        [HttpGet("drone/{droneName}/active-tasks")]
        public IActionResult GetDroneActiveTasks(string droneName)
        {
            try
            {
                var activeTasks = _taskDataService.GetActiveSubTasksForDrone(droneName);
                return Ok(new { success = true, data = activeTasks, droneName });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 17. 获取过期的子任务
        [HttpGet("analysis/expired-tasks")]
        public IActionResult GetExpiredSubTasks([FromQuery] int timeoutMinutes = 30)
        {
            try
            {
                var timeout = TimeSpan.FromMinutes(timeoutMinutes);
                var expiredTasks = _taskDataService.GetExpiredSubTasks(timeout);
                return Ok(new { 
                    success = true, 
                    data = expiredTasks, 
                    timeoutMinutes,
                    count = expiredTasks.Count 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region 数据管理功能

        // 18. 从数据库加载所有任务
        [HttpPost("tasks/load-from-database")]
        public async Task<IActionResult> LoadTasksFromDatabase()
        {
            try
            {
                await _taskDataService.LoadTasksFromDatabaseAsync();
                return Ok(new { success = true, message = "任务数据已从数据库加载" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 19. 同步所有任务到数据库
        [HttpPost("tasks/sync-to-database")]
        public async Task<IActionResult> SyncTasksToDatabase()
        {
            try
            {
                await _taskDataService.SyncAllTasksToDatabaseAsync();
                return Ok(new { success = true, message = "任务数据已同步到数据库" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 20. 重新分配失败的子任务
        [HttpPost("tasks/reassign-failed")]
        public async Task<IActionResult> ReassignFailedSubTasks()
        {
            try
            {
                var count = await _taskDataService.ReassignFailedSubTasksAsync();
                return Ok(new { 
                    success = true, 
                    message = $"已重新分配 {count} 个失败的子任务",
                    reassignedCount = count 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 21. 清理旧的已完成任务
        [HttpDelete("tasks/cleanup-old")]
        public async Task<IActionResult> CleanupOldCompletedTasks([FromQuery] int maxAgeDays = 30)
        {
            try
            {
                var maxAge = TimeSpan.FromDays(maxAgeDays);
                var count = await _taskDataService.CleanupOldCompletedTasksAsync(maxAge);
                return Ok(new { 
                    success = true, 
                    message = $"已清理 {count} 个超过 {maxAgeDays} 天的已完成任务",
                    cleanedCount = count,
                    maxAgeDays 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 22. 批量更新子任务状态
        [HttpPut("subtasks/batch-update-status")]
        public async Task<IActionResult> BatchUpdateSubTaskStatus(
            [FromBody] BatchUpdateRequest request)
        {
            try
            {
                if (request?.SubTaskIds == null || !request.SubTaskIds.Any())
                    return BadRequest(new { success = false, message = "子任务ID列表不能为空" });

                if (!Enum.IsDefined(typeof(TaskStatus), request.NewStatus))
                    return BadRequest(new { success = false, message = "无效的任务状态" });

                var count = await _taskDataService.BatchUpdateSubTaskStatusAsync(
                    request.SubTaskIds, request.NewStatus, request.Reason);

                return Ok(new { 
                    success = true, 
                    message = $"已更新 {count} 个子任务的状态",
                    updatedCount = count 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region 健康检查和系统信息

        // 23. 获取系统概览
        [HttpGet("overview")]
        public IActionResult GetSystemOverview()
        {
            try
            {
                var drones = _droneDataService.GetDrones();
                var tasks = _taskDataService.GetTasks();
                var taskStats = _taskDataService.GetTaskStatistics();
                var performanceAnalysis = _taskDataService.GetTaskPerformanceAnalysis();

                var overview = new
                {
                    timestamp = DateTime.UtcNow,
                    drones = new
                    {
                        total = drones.Count,
                        online = drones.Count(d => d.Status != ClassLibrary_Core.Drone.DroneStatus.Offline),
                        offline = drones.Count(d => d.Status == ClassLibrary_Core.Drone.DroneStatus.Offline)
                    },
                    tasks = taskStats,
                    performance = performanceAnalysis
                };

                return Ok(new { success = true, data = overview });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        #endregion
    }

    #region 请求模型

    /// <summary>
    /// 批量更新请求模型
    /// </summary>
    public class BatchUpdateRequest
    {
        public List<Guid> SubTaskIds { get; set; } = new();
        public TaskStatus NewStatus { get; set; }
        public string? Reason { get; set; }
    }

    #endregion
}
