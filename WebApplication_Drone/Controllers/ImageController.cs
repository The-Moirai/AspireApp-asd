using Microsoft.AspNetCore.Mvc;
using WebApplication_Drone.Services;
using ClassLibrary_Core.Data;

namespace WebApplication_Drone.Controllers
{
    /// <summary>
    /// 图片控制器，处理从数据库获取图片的请求
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ImageController : ControllerBase
    {
        private readonly SqlserverService _sqlserverService;
        private readonly ILogger<ImageController> _logger;

        public ImageController(SqlserverService sqlserverService, ILogger<ImageController> logger)
        {
            _sqlserverService = sqlserverService;
            _logger = logger;
        }

        /// <summary>
        /// 根据图片ID获取图片
        /// </summary>
        /// <param name="imageId">图片ID</param>
        /// <returns>图片数据</returns>
        [HttpGet("{imageId}")]
        public async Task<IActionResult> GetImage(long imageId)
        {
            try
            {
                var image = await _sqlserverService.GetSubTaskImageAsync(imageId);
                if (image == null)
                {
                    return NotFound("图片不存在");
                }

                return File(image.ImageData, image.ContentType, image.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取图片失败: ImageId={ImageId}", imageId);
                return StatusCode(500, "获取图片失败");
            }
        }

        /// <summary>
        /// 根据子任务ID和图片ID获取图片
        /// </summary>
        /// <param name="subTaskId">子任务ID</param>
        /// <param name="imageId">图片ID</param>
        /// <returns>图片数据</returns>
        [HttpGet("subtask/{subTaskId}/{imageId}")]
        public async Task<IActionResult> GetSubTaskImage(Guid subTaskId, long imageId)
        {
            try
            {
                var image = await _sqlserverService.GetSubTaskImageAsync(imageId);
                if (image == null || image.SubTaskId != subTaskId)
                {
                    return NotFound("图片不存在或不属于指定子任务");
                }

                // 设置缓存策略
                Response.Headers.Add("Cache-Control", "public, max-age=3600"); // 缓存1小时
                Response.Headers.Add("ETag", $"\"{image.Id}-{image.UploadTime.Ticks}\"");

                return File(image.ImageData, image.ContentType, image.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取子任务图片失败: SubTaskId={SubTaskId}, ImageId={ImageId}", subTaskId, imageId);
                return StatusCode(500, "获取图片失败");
            }
        }

        /// <summary>
        /// 获取子任务的所有图片信息（不包含二进制数据）
        /// </summary>
        /// <param name="subTaskId">子任务ID</param>
        /// <returns>图片信息列表</returns>
        [HttpGet("subtask/{subTaskId}")]
        public async Task<IActionResult> GetSubTaskImages(Guid subTaskId)
        {
            try
            {
                var images = await _sqlserverService.GetSubTaskImagesAsync(subTaskId);
                
                var imageInfos = images.Select(img => new
                {
                    Id = img.Id,
                    SubTaskId = img.SubTaskId,
                    FileName = img.FileName,
                    FileExtension = img.FileExtension,
                    FileSize = img.FileSize,
                    FormattedFileSize = img.GetFormattedFileSize(),
                    ContentType = img.ContentType,
                    ImageIndex = img.ImageIndex,
                    UploadTime = img.UploadTime,
                    Description = img.Description,
                    ImageUrl = img.GetImageUrl()
                }).ToList();

                return Ok(new
                {
                    SubTaskId = subTaskId,
                    ImageCount = imageInfos.Count,
                    Images = imageInfos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取子任务图片列表失败: SubTaskId={SubTaskId}", subTaskId);
                return StatusCode(500, "获取图片列表失败");
            }
        }

        /// <summary>
        /// 删除指定图片
        /// </summary>
        /// <param name="imageId">图片ID</param>
        /// <returns>删除结果</returns>
        [HttpDelete("{imageId}")]
        public async Task<IActionResult> DeleteImage(long imageId)
        {
            try
            {
                var success = await _sqlserverService.DeleteSubTaskImageAsync(imageId);
                if (success)
                {
                    return Ok(new { Message = "图片删除成功" });
                }
                else
                {
                    return NotFound("图片不存在");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除图片失败: ImageId={ImageId}", imageId);
                return StatusCode(500, "删除图片失败");
            }
        }

        /// <summary>
        /// 删除子任务的所有图片
        /// </summary>
        /// <param name="subTaskId">子任务ID</param>
        /// <returns>删除结果</returns>
        [HttpDelete("subtask/{subTaskId}")]
        public async Task<IActionResult> DeleteSubTaskImages(Guid subTaskId)
        {
            try
            {
                var deletedCount = await _sqlserverService.DeleteSubTaskImagesAsync(subTaskId);
                return Ok(new
                {
                    Message = $"成功删除 {deletedCount} 张图片",
                    DeletedCount = deletedCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除子任务图片失败: SubTaskId={SubTaskId}", subTaskId);
                return StatusCode(500, "删除图片失败");
            }
        }

        /// <summary>
        /// 获取图片缩略图
        /// </summary>
        /// <param name="imageId">图片ID</param>
        /// <param name="width">缩略图宽度（默认150px）</param>
        /// <param name="height">缩略图高度（默认150px）</param>
        /// <returns>缩略图数据</returns>
        [HttpGet("{imageId}/thumbnail")]
        public async Task<IActionResult> GetImageThumbnail(long imageId, int width = 150, int height = 150)
        {
            try
            {
                var image = await _sqlserverService.GetSubTaskImageAsync(imageId);
                if (image == null)
                {
                    return NotFound("图片不存在");
                }

                // 这里可以集成图片处理库来生成缩略图
                // 暂时返回原图，实际项目中可以使用 SkiaSharp 或 ImageSharp 等库
                
                // 设置缓存策略
                Response.Headers.Add("Cache-Control", "public, max-age=86400"); // 缓存24小时
                Response.Headers.Add("ETag", $"\"{image.Id}-thumb-{width}x{height}-{image.UploadTime.Ticks}\"");

                return File(image.ImageData, image.ContentType, $"thumb_{image.FileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取缩略图失败: ImageId={ImageId}", imageId);
                return StatusCode(500, "获取缩略图失败");
            }
        }
    }
} 