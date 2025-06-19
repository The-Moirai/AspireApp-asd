using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebApplication.Service;
using ClassLibrary_Core.Data;
using ClassLibrary_Core.Mission;

namespace WebApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HistoryDataController : ControllerBase
    {
        private readonly IDroneService _droneService;
        private readonly ITaskDataService _taskDataService;
        private readonly IMissionService _missionService;

        public HistoryDataController(
            IDroneService droneService,
            ITaskDataService taskDataService,
            IMissionService missionService)
        {
            _droneService = droneService;
            _taskDataService = taskDataService;
            _missionService = missionService;
        }

        #region 无人机历史数据查询

        [HttpGet("drone/{droneId:Guid}/recent")]
        public async Task<IActionResult> GetRecentDroneData(
            Guid droneId,
            [FromQuery] int hours = 1)
        {
            try
            {
                var duration = TimeSpan.FromHours(hours);
                var data = await _droneService.GetRecentDroneDataAsync(droneId, duration);
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("drone/{droneId:Guid}/task/{taskId:Guid}")]
        public async Task<IActionResult> GetDroneTaskData(
            Guid droneId,
            Guid taskId)
        {
            try
            {
                var data = await _droneService.GetDroneTaskDataAsync(droneId, taskId);
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("drones/all")]
        public async Task<IActionResult> GetAllDronesHistory()
        {
            try
            {
                var history = await _droneService.GetAllDronesAsync();
                return Ok(new { success = true, data = history });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("drone/{droneId:Guid}")]
        public async Task<IActionResult> GetDroneDetails(Guid droneId)
        {
            try
            {
                var drone = await _droneService.GetDroneByIdAsync(droneId);
                if (drone == null)
                    return NotFound(new { success = false, message = "无人机未找到" });

                return Ok(new { success = true, data = drone });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("drone/{droneId:Guid}/subtasks")]
        public async Task<IActionResult> GetDroneSubTasks(Guid droneId)
        {
            try
            {
                var subTasks = await _droneService.GetDroneSubTasksAsync(droneId);
                return Ok(new { success = true, data = subTasks });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region 任务历史数据查询

        [HttpGet("task/{taskId:Guid}/drone/{droneId:Guid}")]
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

        [HttpGet("task/{taskId:Guid}/drones")]
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

        [HttpGet("tasks/all")]
        public async Task<IActionResult> GetAllTasks()
        {
            try
            {
                var tasks = await _taskDataService.GetTasksAsync();
                return Ok(new { success = true, data = tasks });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("task/{taskId:Guid}")]
        public async Task<IActionResult> GetTaskDetails(Guid taskId)
        {
            try
            {
                var task = await _taskDataService.GetTaskAsync(taskId);
                if (task == null)
                    return NotFound(new { success = false, message = "任务未找到" });

                return Ok(new { success = true, data = task });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("tasks/status/{status:int}")]
        public async Task<IActionResult> GetTasksByStatus(int status)
        {
            try
            {
                if (!Enum.IsDefined(typeof(TaskStatus), status))
                    return BadRequest(new { success = false, message = "无效的任务状态" });

                var tasks = await _taskDataService.GetTasksByStatusAsync((TaskStatus)status);
                return Ok(new { success = true, data = tasks });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("task/{taskId:Guid}/subtasks")]
        public async Task<IActionResult> GetTaskSubTasks(Guid taskId)
        {
            try
            {
                var subTasks = await _taskDataService.GetSubTasksAsync(taskId);
                return Ok(new { success = true, data = subTasks });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region 时间范围查询

        [HttpGet("drones/time-range")]
        public async Task<IActionResult> GetAllDronesDataInTimeRange(
            [FromQuery] DateTime startTime,
            [FromQuery] DateTime endTime)
        {
            try
            {
                var data = await _droneService.GetAllDronesDataInTimeRangeAsync(startTime, endTime);
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("tasks/time-range")]
        public async Task<IActionResult> GetAllTasksDataInTimeRange(
            [FromQuery] DateTime startTime,
            [FromQuery] DateTime endTime)
        {
            try
            {
                var data = await _taskDataService.GetAllTasksDataInTimeRangeAsync(startTime, endTime);
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("missions/time-range")]
        public async Task<IActionResult> GetMissionsByTimeRange(
            [FromQuery] DateTime startTime,
            [FromQuery] DateTime endTime)
        {
            try
            {
                var missions = await _missionService.GetMissionsByTimeRangeAsync(startTime, endTime);
                return Ok(new { success = true, data = missions });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("missions/drones/time-range")]
        public async Task<IActionResult> GetDronesMissionsByTimeRange(
            [FromQuery] DateTime startTime,
            [FromQuery] DateTime endTime)
        {
            try
            {
                var missions = await _missionService.GetDronesMissionsByTimeRangeAsync(startTime, endTime);
                return Ok(new { success = true, data = missions });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region 统计和分析

        [HttpGet("statistics/tasks")]
        public async Task<IActionResult> GetTaskStatistics()
        {
            try
            {
                var statistics = await _taskDataService.GetTaskStatisticsAsync();
                return Ok(new { success = true, data = statistics });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("analysis/task-performance")]
        public async Task<IActionResult> GetTaskPerformanceAnalysis()
        {
            try
            {
                var analysis = await _taskDataService.GetTaskPerformanceAnalysisAsync();
                return Ok(new { success = true, data = analysis });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("drone/{droneName}/active-tasks")]
        public async Task<IActionResult> GetDroneActiveTasks(string droneName)
        {
            try
            {
                var activeTasks = await _droneService.GetActiveSubTasksForDroneAsync(droneName);
                return Ok(new { success = true, data = activeTasks });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("analysis/expired-tasks")]
        public async Task<IActionResult> GetExpiredSubTasks([FromQuery] int timeoutMinutes = 30)
        {
            try
            {
                var timeout = TimeSpan.FromMinutes(timeoutMinutes);
                var expiredTasks = await _taskDataService.GetExpiredSubTasksAsync(timeout);
                return Ok(new { success = true, data = expiredTasks });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("cluster/status")]
        public async Task<IActionResult> GetClusterStatus()
        {
            try
            {
                var clusterStatus = await _droneService.GetClusterStatusAsync();
                return Ok(new { success = true, data = clusterStatus });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region 任务管理操作

        [HttpPost("tasks/load-from-database")]
        public async Task<IActionResult> LoadTasksFromDatabase()
        {
            try
            {
                await _taskDataService.LoadTasksFromDatabaseAsync();
                return Ok(new { success = true, message = "任务已从数据库加载" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("tasks/sync-to-database")]
        public async Task<IActionResult> SyncTasksToDatabase()
        {
            try
            {
                await _taskDataService.SyncAllTasksToDatabaseAsync();
                return Ok(new { success = true, message = "任务已同步到数据库" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("tasks/reassign-failed")]
        public async Task<IActionResult> ReassignFailedSubTasks()
        {
            try
            {
                var reassignCount = await _taskDataService.ReassignFailedSubTasksAsync();
                return Ok(new { success = true, reassignedCount = reassignCount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("tasks/cleanup-old")]
        public async Task<IActionResult> CleanupOldCompletedTasks([FromQuery] int maxAgeDays = 30)
        {
            try
            {
                var maxAge = TimeSpan.FromDays(maxAgeDays);
                var deletedCount = await _taskDataService.CleanupOldCompletedTasksAsync(maxAge);
                return Ok(new { success = true, deletedCount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut("subtasks/batch-update-status")]
        public async Task<IActionResult> BatchUpdateSubTaskStatus(
            [FromBody] BatchUpdateRequest request)
        {
            try
            {
                var updateCount = await _taskDataService.BatchUpdateSubTaskStatusAsync(
                    request.SubTaskIds, request.NewStatus, request.Reason);
                return Ok(new { success = true, updatedCount = updateCount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region 系统概览

        [HttpGet("overview")]
        public async Task<IActionResult> GetSystemOverview()
        {
            try
            {
                var clusterStatus = await _droneService.GetClusterStatusAsync();
                var taskStatistics = await _taskDataService.GetTaskStatisticsAsync();
                var taskPerformance = await _taskDataService.GetTaskPerformanceAnalysisAsync();

                var overview = new
                {
                    ClusterStatus = clusterStatus,
                    TaskStatistics = taskStatistics,
                    TaskPerformance = taskPerformance,
                    LastUpdated = DateTime.UtcNow
                };

                return Ok(new { success = true, data = overview });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region 任务历史相关

        [HttpGet("missions/all")]
        public async Task<IActionResult> GetAllMissions()
        {
            try
            {
                var missions = await _missionService.GetAllMissionHistoriesAsync();
                return Ok(new { success = true, data = missions });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("missions/drone/{droneName}/recent")]
        public async Task<IActionResult> GetDroneRecentMissions(
            string droneName, 
            [FromQuery] int hours = 24)
        {
            try
            {
                var timeSpan = TimeSpan.FromHours(hours);
                var missions = await _missionService.GetDroneRecentMissionsAsync(droneName, timeSpan);
                return Ok(new { success = true, data = missions });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("missions")]
        public async Task<IActionResult> CreateMissionHistory([FromBody] MissionHistory missionHistory)
        {
            try
            {
                var created = await _missionService.CreateMissionHistoryAsync(missionHistory);
                return Ok(new { success = true, data = created });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        #endregion
    }

    public class BatchUpdateRequest
    {
        public List<Guid> SubTaskIds { get; set; } = new();
        public TaskStatus NewStatus { get; set; }
        public string? Reason { get; set; }
    }
} 