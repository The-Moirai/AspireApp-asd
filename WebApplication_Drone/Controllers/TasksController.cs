using Microsoft.AspNetCore.Mvc;
using WebApplication_Drone.Services.Clean;
using ClassLibrary_Core.Mission;
using ClassLibrary_Core.Data;

namespace WebApplication_Drone.Controllers
{
    /// <summary>
    /// 任务控制器 - 使用新的分层架构
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly TaskService _taskService;
        private readonly ILogger<TasksController> _logger;

        public TasksController(TaskService taskService, ILogger<TasksController> logger)
        {
            _taskService = taskService;
            _logger = logger;
        }

        /// <summary>
        /// 获取所有任务
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MainTask>>> GetAll()
        {
            try
            {
                var tasks = await _taskService.GetTasksAsync();
                return Ok(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取所有任务失败");
                return StatusCode(500, new { error = "获取任务失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 根据ID获取任务
        /// </summary>
        [HttpGet("{id:Guid}")]
        public async Task<ActionResult<MainTask>> Get(Guid id)
        {
            try
            {
                var task = await _taskService.GetTaskAsync(id);
                return task is not null ? Ok(task) : NotFound(new { error = "任务未找到" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取任务失败: {TaskId}", id);
                return StatusCode(500, new { error = "获取任务失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 创建任务（支持文件上传）
        /// </summary>
        [HttpPost("upload")]
        public async Task<ActionResult<MainTask>> Create([FromForm] string? Description, [FromForm] string? Id, [FromForm] string? CreationTime, [FromForm] string? Notes, [FromForm] IFormFile? VideoFile)
        {
            try
            {
                // 解析任务ID
                Guid taskId;
                if (!string.IsNullOrEmpty(Id) && Guid.TryParse(Id, out var parsedId))
                {
                    taskId = parsedId;
                }
                else
                {
                    taskId = Guid.NewGuid();
                }

                // 解析创建时间
                DateTime creationTime;
                if (!string.IsNullOrEmpty(CreationTime) && DateTime.TryParse(CreationTime, out var parsedTime))
                {
                    creationTime = parsedTime;
                }
                else
                {
                    creationTime = DateTime.Now;
                }

                // 创建任务对象
                var task = new MainTask
                {
                    Id = taskId,
                    Description = Description ?? "未命名任务",
                    Status = System.Threading.Tasks.TaskStatus.Created,
                    CreationTime = creationTime
                };

                // 如果有文件上传，保存文件信息
                if (VideoFile != null && VideoFile.Length > 0)
                {
                    _logger.LogInformation("收到文件上传: {FileName}, 大小: {FileSize} 字节", 
                        VideoFile.FileName, VideoFile.Length);
                    
                    // 保存文件到TaskVideos目录
                    var fileName = $"{task.Id}_{VideoFile.FileName}";
                    var filePath = Path.Combine("TaskVideos", fileName);
                    
                    // 确保目录存在
                    Directory.CreateDirectory("TaskVideos");
                    
                    // 保存文件
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await VideoFile.CopyToAsync(stream);
                    }
                    
                    _logger.LogInformation("文件保存成功: {FilePath}", filePath);
                    
                }

                // 创建任务
                var success = await _taskService.AddTaskAsync(task, "System");
                if (success)
                {
                    _logger.LogInformation("任务创建成功: {TaskId}, {Description}", task.Id, task.Description);
                    return CreatedAtAction(nameof(Get), new { id = task.Id }, task);
                }
                
                return BadRequest(new { error = "创建任务失败" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建任务失败");
                return StatusCode(500, new { error = "创建任务失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 创建任务（JSON格式，保持向后兼容）
        /// </summary>
        [HttpPost("create")]
        public async Task<ActionResult<MainTask>> CreateFromJson([FromBody] CreateTaskRequest request)
        {
            try
            {
                var success = await _taskService.AddTaskAsync(request.Task, request.CreatedBy);
                if (success)
                {
                    return CreatedAtAction(nameof(Get), new { id = request.Task.Id }, request.Task);
                }
                return BadRequest(new { error = "创建任务失败" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建任务失败");
                return StatusCode(500, new { error = "创建任务失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 更新任务
        /// </summary>
        [HttpPut("{id:Guid}")]
        public async Task<IActionResult> Update(Guid id, MainTask updated)
        {
            try
            {
                if (id != updated.Id)
                {
                    return BadRequest(new { error = "ID不匹配" });
                }

                var result = await _taskService.UpdateTaskAsync(updated);
                return result ? Ok(updated) : NotFound(new { error = "任务未找到" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新任务失败: {TaskId}", id);
                return StatusCode(500, new { error = "更新任务失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 删除任务
        /// </summary>
        [HttpDelete("{id:Guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var result = await _taskService.DeleteTaskAsync(id);
                return result ? NoContent() : NotFound(new { error = "任务未找到" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除任务失败: {TaskId}", id);
                return StatusCode(500, new { error = "删除任务失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取任务的子任务
        /// </summary>
        [HttpGet("{id:Guid}/subtasks")]
        public async Task<ActionResult<IEnumerable<SubTask>>> GetSubTasks(Guid id)
        {
            try
            {
                var subTasks = await _taskService.GetSubTasksAsync(id);
                return Ok(subTasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取子任务失败: {TaskId}", id);
                return StatusCode(500, new { error = "获取子任务失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取特定子任务
        /// </summary>
        [HttpGet("{taskId:Guid}/subtasks/{subTaskId:Guid}")]
        public async Task<ActionResult<SubTask>> GetSubTask(Guid taskId, Guid subTaskId)
        {
            try
            {
                var subTask = await _taskService.GetSubTaskAsync(taskId, subTaskId);
                return subTask is not null ? Ok(subTask) : NotFound(new { error = "子任务未找到" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取子任务失败: {TaskId}, {SubTaskId}", taskId, subTaskId);
                return StatusCode(500, new { error = "获取子任务失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 添加子任务
        /// </summary>
        [HttpPost("{taskId:Guid}/subtasks")]
        public async Task<IActionResult> AddSubTask(Guid taskId, [FromBody] SubTask subTask)
        {
            try
            {
                var success = await _taskService.AddSubTaskAsync(subTask);
                return success ? CreatedAtAction(nameof(GetSubTask), new { taskId, subTaskId = subTask.Id }, subTask) 
                              : BadRequest(new { error = "添加子任务失败" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加子任务失败: {TaskId}", taskId);
                return StatusCode(500, new { error = "添加子任务失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 更新子任务
        /// </summary>
        [HttpPut("{taskId:Guid}/subtasks/{subTaskId:Guid}")]
        public async Task<IActionResult> UpdateSubTask(Guid taskId, Guid subTaskId, [FromBody] SubTask subTask)
        {
            try
            {
                if (subTaskId != subTask.Id)
                {
                    return BadRequest(new { error = "子任务ID不匹配" });
                }

                var success = await _taskService.UpdateSubTaskAsync(subTask);
                return success ? Ok(subTask) : NotFound(new { error = "子任务未找到" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新子任务失败: {TaskId}, {SubTaskId}", taskId, subTaskId);
                return StatusCode(500, new { error = "更新子任务失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 删除子任务
        /// </summary>
        [HttpDelete("{taskId:Guid}/subtasks/{subTaskId:Guid}")]
        public async Task<IActionResult> DeleteSubTask(Guid taskId, Guid subTaskId)
        {
            try
            {
                var success = await _taskService.DeleteSubTaskAsync(taskId, subTaskId);
                return success ? NoContent() : NotFound(new { error = "子任务未找到" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除子任务失败: {TaskId}, {SubTaskId}", taskId, subTaskId);
                return StatusCode(500, new { error = "删除子任务失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取任务图片
        /// </summary>
        [HttpGet("subtasks/{subTaskId:Guid}/images")]
        public async Task<IActionResult> GetImages(Guid taskId, Guid subTaskId)
        {
            try
            {
                var images = await _taskService.GetImagesAsync(subTaskId);
                return Ok(new { success = true, data = images });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取图片失败: {TaskId}, {SubTaskId}", taskId, subTaskId);
                return StatusCode(500, new { error = "获取图片失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取特定图片
        /// </summary>
        [HttpGet("images/{imageId:Guid}")]
        public async Task<IActionResult> GetImage(Guid imageId)
        {
            try
            {
                var image = await _taskService.GetImageAsync(imageId);
                return image is not null ? Ok(image) : NotFound(new { error = "图片未找到" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取图片失败: {ImageId}", imageId);
                return StatusCode(500, new { error = "获取图片失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取最近上传的图片
        /// </summary>
        [HttpGet("images/recent")]
        public async Task<IActionResult> GetRecentImages([FromQuery] int minutes = 5, [FromQuery] int limit = 20)
        {
            try
            {
                var since = DateTime.UtcNow.AddMinutes(-minutes);
                _logger.LogDebug("获取最近图片: Minutes={Minutes}, Limit={Limit}, Since={Since}", minutes, limit, since);
                
                var images = await _taskService.GetRecentImagesAsync(since, limit);
                
                _logger.LogDebug("返回 {Count} 张最近图片", images.Count);
                return Ok(new { success = true, data = images });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取最近图片失败: Minutes={Minutes}, Limit={Limit}", minutes, limit);
                return StatusCode(500, new { error = "获取最近图片失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 上传图片
        /// </summary>
        [HttpPost("{taskId:Guid}/subtasks/{subTaskId:Guid}/images")]
        public async Task<IActionResult> UploadImage(
            Guid taskId, 
            Guid subTaskId, 
            [FromForm] IFormFile file,
            [FromForm] int imageIndex = 1,
            [FromForm] string? description = null)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { error = "文件不能为空" });
                }

                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                var imageData = memoryStream.ToArray();

                var imageId = await _taskService.SaveImageAsync(subTaskId, imageData, file.FileName, imageIndex, description);
                
                return Ok(new { success = true, imageId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "上传图片失败: {TaskId}, {SubTaskId}", taskId, subTaskId);
                return StatusCode(500, new { error = "上传图片失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 删除图片
        /// </summary>
        [HttpDelete("images/{imageId:Guid}")]
        public async Task<IActionResult> DeleteImage(Guid imageId)
        {
            try
            {
                var success = await _taskService.DeleteImageAsync(imageId);
                return success ? NoContent() : NotFound(new { error = "图片未找到" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除图片失败: {ImageId}", imageId);
                return StatusCode(500, new { error = "删除图片失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取任务数据点
        /// </summary>
        [HttpGet("{taskId:Guid}/data/{droneId:Guid}")]
        public async Task<IActionResult> GetTaskData(Guid taskId, Guid droneId)
        {
            try
            {
                var data = await _taskService.GetTaskDataAsync(taskId, droneId);
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取任务数据失败: {TaskId}, {DroneId}", taskId, droneId);
                return StatusCode(500, new { error = "获取任务数据失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 批量更新任务
        /// </summary>
        [HttpPut("bulk")]
        public async Task<IActionResult> BulkUpdate([FromBody] IEnumerable<MainTask> tasks)
        {
            try
            {
                var result = await _taskService.BulkUpdateTasksAsync(tasks);
                return result ? Ok(new { message = "批量更新成功" }) : BadRequest(new { error = "批量更新失败" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量更新任务失败");
                return StatusCode(500, new { error = "批量更新失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取服务统计信息
        /// </summary>
        [HttpGet("statistics")]
        public IActionResult GetStatistics()
        {
            try
            {
                var stats = _taskService.GetStatistics();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取统计信息失败");
                return StatusCode(500, new { error = "获取统计信息失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取任务状态统计
        /// </summary>
        [HttpGet("status/statistics")]
        public async Task<IActionResult> GetStatusStatistics()
        {
            try
            {
                var tasks = await _taskService.GetTasksAsync();
                var statusStats = tasks.GroupBy(t => t.Status)
                                     .Select(g => new { Status = g.Key, Count = g.Count() })
                                     .ToList();

                return Ok(new { success = true, data = statusStats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取状态统计失败");
                return StatusCode(500, new { error = "获取状态统计失败", message = ex.Message });
            }
        }
    }

    /// <summary>
    /// 创建任务请求模型
    /// </summary>
    public class CreateTaskRequest
    {
        public MainTask Task { get; set; } = null!;
        public string CreatedBy { get; set; } = string.Empty;
    }
} 