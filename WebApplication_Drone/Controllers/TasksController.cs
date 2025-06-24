using ClassLibrary_Core.Mission;
using ClassLibrary_Core.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cors;
using WebApplication_Drone.Services;

namespace WebApplication_Drone.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly TaskDataService _taskDataService;
        private readonly SqlserverService _sqlserverService;
        private readonly ILogger<TasksController> _logger;

        public TasksController(
            TaskDataService taskDataService,
            SqlserverService sqlserverService,
            ILogger<TasksController> logger)
        {
            _taskDataService = taskDataService;
            _sqlserverService = sqlserverService;
            _logger = logger;
        }
        [HttpGet]
        public ActionResult<IEnumerable<MainTask>> GetAll()
        {
            var mainTasks = _taskDataService.GetTasks();
            return Ok(mainTasks);
        }
        [HttpGet("{id:guid}")]
        public ActionResult<MainTask> Get(Guid id)
        {
            var mainTask = _taskDataService.GetTask(id);
            return mainTask is not null ? Ok(mainTask) : NotFound();
        }
        [HttpPost]
        public ActionResult<MainTask> Create(MainTask maintask)
        {
            _taskDataService.AddTask(maintask,"User");
            return CreatedAtAction(nameof(Get), new { id = maintask.Id }, maintask);
        }
        [HttpPost("upload")]
        public async Task<IActionResult> UploadTaskWithVideo([FromForm] string Description, [FromForm] Guid Id, [FromForm] DateTime CreationTime, [FromForm] IFormFile VideoFile)
        {
           var videosDir = Path.Combine(Directory.GetCurrentDirectory(), "TaskVideos");
           if (!Directory.Exists(videosDir))
           {
               Directory.CreateDirectory(videosDir);
           }

           // 保存视频文件
           var savePath = Path.Combine("TaskVideos", VideoFile.FileName);
           using (var stream = System.IO.File.Create(savePath))
           {
               await VideoFile.CopyToAsync(stream);
           }


            // 创建任务
            var task = new MainTask
            {
                Id = Id,
                Description = Description,
                CreationTime = CreationTime,
                // 其他字段按需补充
            };
            // 保存任务到数据源
            _taskDataService.AddTask(task,"User");

           return Ok();
        }

        #region 图片管理功能

        /// <summary>
        /// 获取子任务的所有图片列表
        /// </summary>
        [HttpGet("subtask/{subTaskId:guid}/images")]
        public async Task<ActionResult<List<SubTaskImageInfo>>> GetSubTaskImages(Guid subTaskId)
        {
            try
            {
                var images = await _sqlserverService.GetSubTaskImagesAsync(subTaskId);
                var imageInfos = images.Select(img => new SubTaskImageInfo
                {
                    Id = img.Id,
                    FileName = img.FileName,
                    FileExtension = img.FileExtension,
                    FileSize = img.FileSize,
                    ImageIndex = img.ImageIndex,
                    UploadTime = img.UploadTime,
                    Description = img.Description,
                    ThumbnailUrl = $"/api/Tasks/images/{img.Id}/thumbnail",
                    FullImageUrl = $"/api/Tasks/images/{img.Id}/image"
                }).OrderBy(img => img.ImageIndex).ToList();

                return Ok(imageInfos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取子任务图片列表失败: SubTaskId={SubTaskId}", subTaskId);
                return StatusCode(500, "获取图片列表失败");
            }
        }

        /// <summary>
        /// 获取子任务的图片数量（实时从数据库查询）
        /// </summary>
        [HttpGet("subtask/{subTaskId:guid}/images-count")]
        public async Task<ActionResult<int>> GetSubTaskImageCount(Guid subTaskId)
        {
            try
            {
                var imageCount = await _sqlserverService.GetSubTaskImageCountAsync(subTaskId);
                return Ok(imageCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取子任务图片数量失败: SubTaskId={SubTaskId}", subTaskId);
                return StatusCode(500, "获取图片数量失败");
            }
        }

        /// <summary>
        /// 根据任务ID获取所有子任务的图片统计
        /// </summary>
        [HttpGet("{taskId:guid}/images-summary")]
        public async Task<ActionResult<TaskImageSummary>> GetTaskImagesSummary(Guid taskId)
        {
            try
            {
                var mainTask = _taskDataService.GetTask(taskId);
                if (mainTask == null)
                {
                    return NotFound("任务不存在");
                }

                var summary = new TaskImageSummary
                {
                    TaskId = taskId,
                    SubTaskImageCounts = new Dictionary<Guid, int>()
                };

                // 统计每个子任务的图片数量
                foreach (var subTask in mainTask.SubTasks)
                {
                    var imageCount = await _sqlserverService.GetSubTaskImageCountAsync(subTask.Id);
                    summary.SubTaskImageCounts[subTask.Id] = imageCount;
                    summary.TotalImages += imageCount;
                }

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取任务图片统计失败: TaskId={TaskId}", taskId);
                return StatusCode(500, "获取图片统计失败");
            }
        }

        /// <summary>
        /// 获取图片内容（用于显示）
        /// </summary>
        [HttpGet("images/{imageId:guid}/image")]
        public async Task<IActionResult> GetImage(Guid imageId)
        {
            try
            {
                _logger.LogInformation("请求下载图片: ImageId={ImageId}", imageId);
                
                var image = await _sqlserverService.GetSubTaskImageAsync(imageId);
                if (image == null)
                {
                    _logger.LogWarning("图片不存在: ImageId={ImageId}", imageId);
                    return NotFound($"图片不存在: {imageId}");
                }

                if (image.ImageData == null || image.ImageData.Length == 0)
                {
                    _logger.LogWarning("图片数据为空: ImageId={ImageId}", imageId);
                    return NotFound("图片数据为空");
                }

                _logger.LogInformation("成功获取图片: ImageId={ImageId}, FileName={FileName}, Size={Size}字节", 
                    imageId, image.FileName, image.ImageData.Length);

                // 设置适当的响应头以支持下载
                Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{image.FileName}\"");
                Response.Headers.Add("Content-Length", image.ImageData.Length.ToString());
                
                return File(image.ImageData, image.ContentType, image.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取图片失败: ImageId={ImageId}", imageId);
                return StatusCode(500, $"获取图片失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取图片内容（仅用于显示，不触发下载）- 优化缓存策略
        /// </summary>
        [HttpGet("images/{imageId:guid}/view")]
        [EnableCors("ImagePolicy")]
        public async Task<IActionResult> ViewImage(Guid imageId)
        {
            try
            {
                _logger.LogDebug("请求查看图片: ImageId={ImageId}", imageId);
                
                var image = await _sqlserverService.GetSubTaskImageAsync(imageId);
                if (image == null)
                {
                    _logger.LogWarning("图片不存在: ImageId={ImageId}", imageId);
                    return NotFound();
                }

                if (image.ImageData == null || image.ImageData.Length == 0)
                {
                    _logger.LogWarning("图片数据为空: ImageId={ImageId}", imageId);
                    return NotFound();
                }

                // 添加强缓存策略，防止InteractiveServer重渲染时重新加载图片
                var etag = $"\"{imageId}-{image.UploadTime.Ticks}\"";
                Response.Headers.Add("Cache-Control", "public, max-age=86400, immutable"); // 缓存1天且不可变
                Response.Headers.Add("ETag", etag);
                Response.Headers.Add("Last-Modified", image.UploadTime.ToString("R"));
                
                // 检查条件请求头，如果客户端已有缓存则返回304
                var ifNoneMatch = Request.Headers["If-None-Match"].FirstOrDefault();
                var ifModifiedSince = Request.Headers["If-Modified-Since"].FirstOrDefault();
                
                if (ifNoneMatch == etag || 
                    (DateTime.TryParse(ifModifiedSince, out var modifiedSince) && 
                     image.UploadTime <= modifiedSince.AddSeconds(1))) // 允许1秒误差
                {
                    _logger.LogDebug("图片未修改，返回304: ImageId={ImageId}", imageId);
                    return StatusCode(304); // Not Modified，浏览器使用缓存
                }

                _logger.LogDebug("返回图片内容: ImageId={ImageId}, Size={Size}字节", imageId, image.ImageData.Length);
                
                // 不设置Content-Disposition，让浏览器直接显示图片
                return File(image.ImageData, image.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查看图片失败: ImageId={ImageId}", imageId);
                return StatusCode(500, "查看图片失败");
            }
        }

        /// <summary>
        /// 获取图片缩略图（优化加载速度）
        /// </summary>
        [HttpGet("images/{imageId:guid}/thumbnail")]
        public async Task<IActionResult> GetThumbnail(Guid imageId)
        {
            try
            {
                var image = await _sqlserverService.GetSubTaskImageAsync(imageId);
                if (image == null)
                {
                    return NotFound();
                }

                // TODO: 实现缩略图生成逻辑
                // 现在先返回原图，后续可以集成图片处理库生成缩略图
                return File(image.ImageData, image.ContentType, $"thumb_{image.FileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取缩略图失败: ImageId={ImageId}", imageId);
                return StatusCode(500, "获取缩略图失败");
            }
        }

        /// <summary>
        /// 删除图片
        /// </summary>
        [HttpDelete("images/{imageId:guid}")]
        public async Task<IActionResult> DeleteImage(Guid imageId)
        {
            try
            {
                var success = await _sqlserverService.DeleteSubTaskImageAsync(imageId);
                if (!success)
                {
                    return NotFound("图片不存在");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除图片失败: ImageId={ImageId}", imageId);
                return StatusCode(500, "删除图片失败");
            }
        }

        /// <summary>
        /// 获取最近上传的图片（用于实时更新）
        /// </summary>
        [HttpGet("images/recent")]
        public async Task<ActionResult<List<SubTaskImageInfo>>> GetRecentImages(int minutes = 5, int limit = 50)
        {
            try
            {
                var since = DateTime.Now.AddMinutes(-minutes);
                var recentImages = await _sqlserverService.GetRecentSubTaskImagesAsync(since, limit);
                
                var imageInfos = recentImages.Select(img => new SubTaskImageInfo
                {
                    Id = img.Id,
                    SubTaskId = img.SubTaskId,
                    FileName = img.FileName,
                    FileExtension = img.FileExtension,
                    FileSize = img.FileSize,
                    ImageIndex = img.ImageIndex,
                    UploadTime = img.UploadTime,
                    Description = img.Description,
                    ThumbnailUrl = $"/api/Tasks/images/{img.Id}/thumbnail",
                    FullImageUrl = $"/api/Tasks/images/{img.Id}/image"
                }).OrderByDescending(img => img.UploadTime).ToList();

                return Ok(imageInfos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取最近图片失败");
                return StatusCode(500, "获取最近图片失败");
            }
        }

        #endregion
    }

    #region 数据传输对象

    /// <summary>
    /// 子任务图片信息
    /// </summary>
    public class SubTaskImageInfo
    {
        public Guid Id { get; set; }
        public Guid SubTaskId { get; set; }
        public string FileName { get; set; } = "";
        public string FileExtension { get; set; } = "";
        public long FileSize { get; set; }
        public int ImageIndex { get; set; }
        public DateTime UploadTime { get; set; }
        public string Description { get; set; } = "";
        public string ThumbnailUrl { get; set; } = "";
        public string FullImageUrl { get; set; } = "";
    }

    /// <summary>
    /// 任务图片统计摘要
    /// </summary>
    public class TaskImageSummary
    {
        public Guid TaskId { get; set; }
        public int TotalImages { get; set; }
        public Dictionary<Guid, int> SubTaskImageCounts { get; set; } = new();
    }

    #endregion
}
