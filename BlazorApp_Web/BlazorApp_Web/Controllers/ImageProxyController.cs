using Microsoft.AspNetCore.Mvc;

namespace BlazorApp_Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImageProxyController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ImageProxyController> _logger;

        public ImageProxyController(IHttpClientFactory httpClientFactory, ILogger<ImageProxyController> logger)
        {
            _httpClient = httpClientFactory.CreateClient("ApiService");
            _logger = logger;
        }

        /// <summary>
        /// 获取图片内容（用于查看显示）
        /// </summary>
        [HttpGet("view/{imageId:guid}")]
        public async Task<IActionResult> ViewImage(Guid imageId)
        {
            try
            {
                _logger.LogDebug("代理请求查看图片: ImageId={ImageId}", imageId);

                // 通过HttpClient调用后端API
                var response = await _httpClient.GetAsync($"api/Tasks/images/{imageId}/view");

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("图片不存在: ImageId={ImageId}", imageId);
                        return NotFound();
                    }
                    
                    _logger.LogError("后端API调用失败: ImageId={ImageId}, StatusCode={StatusCode}", imageId, response.StatusCode);
                    return StatusCode((int)response.StatusCode, "获取图片失败");
                }

                // 获取响应内容和头信息
                var imageData = await response.Content.ReadAsByteArrayAsync();
                var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/jpeg";

                // 转发缓存相关的头信息
                if (response.Headers.ETag != null)
                {
                    Response.Headers.Add("ETag", response.Headers.ETag.ToString());
                }
                
                if (response.Headers.CacheControl != null)
                {
                    Response.Headers.Add("Cache-Control", response.Headers.CacheControl.ToString());
                }

                if (response.Content.Headers.LastModified.HasValue)
                {
                    Response.Headers.Add("Last-Modified", response.Content.Headers.LastModified.Value.ToString("R"));
                }

                _logger.LogDebug("成功代理图片: ImageId={ImageId}, Size={Size}字节", imageId, imageData.Length);

                return File(imageData, contentType);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "网络请求失败: ImageId={ImageId}", imageId);
                return StatusCode(503, "服务暂不可用");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "代理图片查看失败: ImageId={ImageId}", imageId);
                return StatusCode(500, "代理请求失败");
            }
        }

        /// <summary>
        /// 下载图片
        /// </summary>
        [HttpGet("download/{imageId:guid}")]
        public async Task<IActionResult> DownloadImage(Guid imageId)
        {
            try
            {
                _logger.LogDebug("代理请求下载图片: ImageId={ImageId}", imageId);

                var response = await _httpClient.GetAsync($"api/Tasks/images/{imageId}/image");

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return NotFound();
                    }
                    return StatusCode((int)response.StatusCode, "下载图片失败");
                }

                var imageData = await response.Content.ReadAsByteArrayAsync();
                var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

                // 转发下载相关的头信息
                if (response.Content.Headers.ContentDisposition != null)
                {
                    Response.Headers.Add("Content-Disposition", response.Content.Headers.ContentDisposition.ToString());
                }

                _logger.LogDebug("成功代理下载图片: ImageId={ImageId}, Size={Size}字节", imageId, imageData.Length);

                return File(imageData, contentType);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "网络请求失败: ImageId={ImageId}", imageId);
                return StatusCode(503, "服务暂不可用");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "代理图片下载失败: ImageId={ImageId}", imageId);
                return StatusCode(500, "代理请求失败");
            }
        }

        /// <summary>
        /// 获取缩略图
        /// </summary>
        [HttpGet("thumbnail/{imageId:guid}")]
        public async Task<IActionResult> GetThumbnail(Guid imageId)
        {
            try
            {
                _logger.LogDebug("代理请求获取缩略图: ImageId={ImageId}", imageId);

                var response = await _httpClient.GetAsync($"api/Tasks/images/{imageId}/thumbnail");

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return NotFound();
                    }
                    return StatusCode((int)response.StatusCode, "获取缩略图失败");
                }

                var imageData = await response.Content.ReadAsByteArrayAsync();
                var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/jpeg";

                // 添加缓存头信息
                Response.Headers.Add("Cache-Control", "public, max-age=3600"); // 缓存1小时

                _logger.LogDebug("成功代理缩略图: ImageId={ImageId}, Size={Size}字节", imageId, imageData.Length);

                return File(imageData, contentType);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "网络请求失败: ImageId={ImageId}", imageId);
                return StatusCode(503, "服务暂不可用");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "代理缩略图获取失败: ImageId={ImageId}", imageId);
                return StatusCode(500, "代理请求失败");
            }
        }

        /// <summary>
        /// 获取子任务图片列表
        /// </summary>
        [HttpGet("subtask/{subTaskId:guid}/images")]
        public async Task<IActionResult> GetSubTaskImages(Guid subTaskId)
        {
            try
            {
                _logger.LogDebug("代理请求获取子任务图片列表: SubTaskId={SubTaskId}", subTaskId);

                var response = await _httpClient.GetAsync($"api/Tasks/subtask/{subTaskId}/images");

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return NotFound();
                    }
                    return StatusCode((int)response.StatusCode, "获取图片列表失败");
                }

                var content = await response.Content.ReadAsStringAsync();
                
                _logger.LogDebug("成功代理图片列表: SubTaskId={SubTaskId}", subTaskId);

                return Content(content, "application/json; charset=utf-8");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "网络请求失败: SubTaskId={SubTaskId}", subTaskId);
                return StatusCode(503, "服务暂不可用");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "代理图片列表获取失败: SubTaskId={SubTaskId}", subTaskId);
                return StatusCode(500, "代理请求失败");
            }
        }

        /// <summary>
        /// 获取最近上传的图片
        /// </summary>
        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentImages([FromQuery] int minutes = 5, [FromQuery] int limit = 50)
        {
            try
            {
                _logger.LogDebug("代理请求获取最近图片: Minutes={Minutes}, Limit={Limit}", minutes, limit);

                var response = await _httpClient.GetAsync($"api/Tasks/images/recent?minutes={minutes}&limit={limit}");

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, "获取最近图片失败");
                }

                var content = await response.Content.ReadAsStringAsync();
                
                _logger.LogDebug("成功代理最近图片列表");

                return Content(content, "application/json");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "网络请求失败: 获取最近图片");
                return StatusCode(503, "服务暂不可用");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "代理最近图片获取失败");
                return StatusCode(500, "代理请求失败");
            }
        }

        /// <summary>
        /// 获取子任务图片数量
        /// </summary>
        [HttpGet("subtask/{subTaskId:guid}/images-count")]
        public async Task<IActionResult> GetSubTaskImageCount(Guid subTaskId)
        {
            try
            {
                _logger.LogDebug("代理请求获取子任务图片数量: SubTaskId={SubTaskId}", subTaskId);

                var response = await _httpClient.GetAsync($"api/Tasks/subtask/{subTaskId}/images-count");

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return NotFound();
                    }
                    return StatusCode((int)response.StatusCode, "获取图片数量失败");
                }

                var content = await response.Content.ReadAsStringAsync();
                
                _logger.LogDebug("成功代理图片数量: SubTaskId={SubTaskId}", subTaskId);

                return Content(content, "application/json");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "网络请求失败: SubTaskId={SubTaskId}", subTaskId);
                return StatusCode(503, "服务暂不可用");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "代理图片数量获取失败: SubTaskId={SubTaskId}", subTaskId);
                return StatusCode(500, "代理请求失败");
            }
        }
    }
} 