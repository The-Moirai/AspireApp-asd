using ClassLibrary_Core.Mission;
using ClassLibrary_Core.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebApplication.Service;

namespace WebApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly ITaskDataService _taskDataService;

        public TasksController(ITaskDataService taskDataService)
        {
            _taskDataService = taskDataService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MainTask>>> GetAll()
        {
            var mainTasks = await _taskDataService.GetTasksAsync();
            return Ok(mainTasks);
        }

        [HttpGet("{id:Guid}")]
        public async Task<ActionResult<MainTask>> Get(Guid id)
        {
            var mainTask = await _taskDataService.GetTaskAsync(id);
            return mainTask is not null ? Ok(mainTask) : NotFound();
        }

        [HttpPost]
        public async Task<ActionResult<MainTask>> Create(MainTask mainTask, [FromQuery] string createdBy = "User")
        {
            await _taskDataService.AddTaskAsync(mainTask, createdBy);
            return CreatedAtAction(nameof(Get), new { id = mainTask.Id }, mainTask);
        }

        [HttpPut("{id:Guid}")]
        public async Task<IActionResult> Update(Guid id, MainTask mainTask)
        {
            if (id != mainTask.Id)
                return BadRequest("ID不匹配");

            var result = await _taskDataService.UpdateTaskAsync(mainTask);
            return result ? Ok(mainTask) : NotFound();
        }

        [HttpDelete("{id:Guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _taskDataService.DeleteTaskAsync(id);
            return result ? NoContent() : NotFound();
        }

        [HttpGet("status/{status:int}")]
        public async Task<ActionResult<IEnumerable<MainTask>>> GetByStatus(int status)
        {
            if (!Enum.IsDefined(typeof(TaskStatus), status))
                return BadRequest("无效的任务状态");

            var tasks = await _taskDataService.GetTasksByStatusAsync((TaskStatus)status);
            return Ok(tasks);
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadTaskWithVideo(
            [FromForm] string Description, 
            [FromForm] Guid Id, 
            [FromForm] DateTime CreationTime, 
            [FromForm] IFormFile VideoFile)
        {
            try
            {
                var taskUpload = new TaskUploadDto
                {
                    Id = Id,
                    Description = Description,
                    CreationTime = CreationTime
                };

                var result = await _taskDataService.SaveTaskWithVideoAsync(taskUpload, VideoFile);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 子任务管理
        [HttpGet("{id:Guid}/subtasks")]
        public async Task<IActionResult> GetSubTasks(Guid id)
        {
            var subTasks = await _taskDataService.GetSubTasksAsync(id);
            return Ok(subTasks);
        }

        [HttpGet("{id:Guid}/subtasks/{subTaskId:Guid}")]
        public async Task<IActionResult> GetSubTask(Guid id, Guid subTaskId)
        {
            var subTask = await _taskDataService.GetSubTaskAsync(id, subTaskId);
            return subTask is not null ? Ok(subTask) : NotFound();
        }

        [HttpPost("{id:Guid}/subtasks")]
        public async Task<IActionResult> AddSubTask(Guid id, [FromBody] SubTask subTask)
        {
            await _taskDataService.AddSubTaskAsync(id, subTask);
            return Ok();
        }

        [HttpPut("{id:Guid}/subtasks/{subTaskId:Guid}")]
        public async Task<IActionResult> UpdateSubTask(Guid id, Guid subTaskId, [FromBody] SubTask subTask)
        {
            if (subTaskId != subTask.Id)
                return BadRequest("子任务ID不匹配");

            var result = await _taskDataService.UpdateSubTaskAsync(id, subTask);
            return result ? Ok() : NotFound();
        }

        [HttpDelete("{id:Guid}/subtasks/{subTaskId:Guid}")]
        public async Task<IActionResult> DeleteSubTask(Guid id, Guid subTaskId)
        {
            var result = await _taskDataService.DeleteSubTaskAsync(id, subTaskId);
            return result ? NoContent() : NotFound();
        }

        // 任务分配和控制
        [HttpPost("{id:Guid}/subtasks/{subTaskId:Guid}/assign")]
        public async Task<IActionResult> AssignSubTask(Guid id, Guid subTaskId, [FromBody] string droneName)
        {
            var result = await _taskDataService.AssignSubTaskAsync(id, subTaskId, droneName);
            return result ? Ok() : BadRequest("分配失败");
        }

        [HttpPost("{id:Guid}/subtasks/{subTaskId:Guid}/unload")]
        public async Task<IActionResult> UnloadSubTask(Guid id, Guid subTaskId)
        {
            var result = await _taskDataService.UnloadSubTaskAsync(id, subTaskId);
            return result ? Ok() : BadRequest("卸载失败");
        }

        [HttpPost("{id:Guid}/subtasks/{subTaskId:Guid}/reload")]
        public async Task<IActionResult> ReloadSubTask(Guid id, Guid subTaskId, [FromBody] string droneName)
        {
            var result = await _taskDataService.ReloadSubTaskAsync(id, subTaskId, droneName);
            return result ? Ok() : BadRequest("重新加载失败");
        }

        [HttpPost("{id:Guid}/subtasks/complete")]
        public async Task<IActionResult> CompleteSubTask(Guid id, [FromBody] string subTaskDescription)
        {
            var result = await _taskDataService.CompleteSubTaskAsync(id, subTaskDescription);
            return result ? Ok() : BadRequest("完成任务失败");
        }

        // 数据查询
        [HttpGet("{id:Guid}/data/drone/{droneId:Guid}")]
        public async Task<IActionResult> GetTaskDroneData(Guid id, Guid droneId)
        {
            var data = await _taskDataService.GetTaskDroneDataAsync(id, droneId);
            return Ok(data);
        }

        [HttpGet("{id:Guid}/data/all-drones")]
        public async Task<IActionResult> GetTaskAllDronesData(Guid id)
        {
            var data = await _taskDataService.GetTaskAllDronesDataAsync(id);
            return Ok(data);
        }

        [HttpGet("data/time-range")]
        public async Task<IActionResult> GetAllTasksDataInTimeRange(
            [FromQuery] DateTime startTime, 
            [FromQuery] DateTime endTime)
        {
            var data = await _taskDataService.GetAllTasksDataInTimeRangeAsync(startTime, endTime);
            return Ok(data);
        }

        // 批量操作
        [HttpPost("subtasks/batch-update-status")]
        public async Task<IActionResult> BatchUpdateSubTaskStatus([FromBody] BatchUpdateRequest request)
        {
            var updateCount = await _taskDataService.BatchUpdateSubTaskStatusAsync(
                request.SubTaskIds, request.NewStatus, request.Reason);
            return Ok(new { UpdatedCount = updateCount });
        }

        [HttpPost("subtasks/reassign-failed")]
        public async Task<IActionResult> ReassignFailedSubTasks()
        {
            var reassignCount = await _taskDataService.ReassignFailedSubTasksAsync();
            return Ok(new { ReassignedCount = reassignCount });
        }

        [HttpDelete("cleanup-old")]
        public async Task<IActionResult> CleanupOldCompletedTasks([FromQuery] int maxAgeDays = 30)
        {
            var maxAge = TimeSpan.FromDays(maxAgeDays);
            var deletedCount = await _taskDataService.CleanupOldCompletedTasksAsync(maxAge);
            return Ok(new { DeletedCount = deletedCount });
        }

        // 统计和分析
        [HttpGet("statistics")]
        public async Task<IActionResult> GetTaskStatistics()
        {
            var statistics = await _taskDataService.GetTaskStatisticsAsync();
            return Ok(statistics);
        }

        [HttpGet("performance-analysis")]
        public async Task<IActionResult> GetTaskPerformanceAnalysis()
        {
            var analysis = await _taskDataService.GetTaskPerformanceAnalysisAsync();
            return Ok(analysis);
        }

        [HttpGet("expired-subtasks")]
        public async Task<IActionResult> GetExpiredSubTasks([FromQuery] int timeoutMinutes = 30)
        {
            var timeout = TimeSpan.FromMinutes(timeoutMinutes);
            var expiredTasks = await _taskDataService.GetExpiredSubTasksAsync(timeout);
            return Ok(expiredTasks);
        }

        [HttpGet("drone/{droneName}/active-subtasks")]
        public async Task<IActionResult> GetActiveSubTasksForDrone(string droneName)
        {
            var activeTasks = await _taskDataService.GetActiveSubTasksForDroneAsync(droneName);
            return Ok(activeTasks);
        }

        // 数据库同步
        [HttpPost("load-from-database")]
        public async Task<IActionResult> LoadTasksFromDatabase()
        {
            await _taskDataService.LoadTasksFromDatabaseAsync();
            return Ok();
        }

        [HttpPost("sync-to-database")]
        public async Task<IActionResult> SyncTasksToDatabase()
        {
            await _taskDataService.SyncAllTasksToDatabaseAsync();
            return Ok();
        }
    }

    
} 