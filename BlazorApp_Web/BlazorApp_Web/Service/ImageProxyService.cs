using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace BlazorApp_Web.Service
{
    /// <summary>
    /// 图片代理服务，提供统一的图片访问接口
    /// </summary>
    public class ImageProxyService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ImageProxyService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;


        public ImageProxyService(HttpClient httpClient, ILogger<ImageProxyService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }
        private string GetAbsoluteUrl(string relativePath)
        {
            var request = _httpContextAccessor.HttpContext?.Request;
            if (request == null)
                throw new InvalidOperationException("No active HTTP request context.");

            var baseUrl = $"{request.Scheme}://{request.Host}";
            return $"{baseUrl}/{relativePath.TrimStart('/')}";
        }
        /// <summary>
        /// 获取子任务的图片列表
        /// </summary>
        /// <param name="subTaskId">子任务ID</param>
        /// <returns>图片ID列表</returns>
        public async Task<List<SubTaskImageInfo>> GetSubTaskImagesAsync(Guid subTaskId)
        {
            try
            {
                _logger.LogDebug("获取子任务图片列表: SubTaskId={SubTaskId}", subTaskId);
                var url = GetAbsoluteUrl($"api/ImageProxy/subtask/{subTaskId}/images");
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("获取子任务图片列表失败: SubTaskId={SubTaskId}, StatusCode={StatusCode}", 
                        subTaskId, response.StatusCode);
                    return new List<SubTaskImageInfo>();
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var images = JsonSerializer.Deserialize<List<SubTaskImageInfo>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<SubTaskImageInfo>();

                _logger.LogDebug("成功获取子任务图片列表: SubTaskId={SubTaskId}, 图片数={Count}", subTaskId, images.Count);
                return images;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取子任务图片列表异常: SubTaskId={SubTaskId}", subTaskId);
                return new List<SubTaskImageInfo>();
            }
        }

        /// <summary>
        /// 获取子任务图片数量
        /// </summary>
        /// <param name="subTaskId">子任务ID</param>
        /// <returns>图片数量</returns>
        public async Task<int> GetSubTaskImageCountAsync(Guid subTaskId)
        {
            try
            {
                _logger.LogDebug("获取子任务图片数量: SubTaskId={SubTaskId}", subTaskId);

                var response = await _httpClient.GetAsync($"api/ImageProxy/subtask/{subTaskId}/images-count");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("获取子任务图片数量失败: SubTaskId={SubTaskId}, StatusCode={StatusCode}", 
                        subTaskId, response.StatusCode);
                    return 0;
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var count = JsonSerializer.Deserialize<int>(jsonContent);

                _logger.LogDebug("成功获取子任务图片数量: SubTaskId={SubTaskId}, Count={Count}", subTaskId, count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取子任务图片数量异常: SubTaskId={SubTaskId}", subTaskId);
                return 0;
            }
        }

        /// <summary>
        /// 获取最近上传的图片
        /// </summary>
        /// <param name="minutes">时间范围（分钟）</param>
        /// <param name="limit">数量限制</param>
        /// <returns>最近图片列表</returns>
        public async Task<List<SubTaskImageInfo>> GetRecentImagesAsync(int minutes = 5, int limit = 50)
        {
            try
            {
                _logger.LogDebug("获取最近图片: Minutes={Minutes}, Limit={Limit}", minutes, limit);

                var response = await _httpClient.GetAsync($"api/ImageProxy/recent?minutes={minutes}&limit={limit}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("获取最近图片失败: StatusCode={StatusCode}", response.StatusCode);
                    return new List<SubTaskImageInfo>();
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var images = JsonSerializer.Deserialize<List<SubTaskImageInfo>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<SubTaskImageInfo>();

                _logger.LogDebug("成功获取最近图片: Count={Count}", images.Count);
                return images;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取最近图片异常: Minutes={Minutes}, Limit={Limit}", minutes, limit);
                return new List<SubTaskImageInfo>();
            }
        }

        /// <summary>
        /// 获取图片查看URL
        /// </summary>
        /// <param name="imageId">图片ID</param>
        /// <returns>图片查看URL</returns>
        public string GetImageViewUrl(Guid imageId)
        {
            return $"/api/ImageProxy/view/{imageId}";
        }

        /// <summary>
        /// 获取图片下载URL
        /// </summary>
        /// <param name="imageId">图片ID</param>
        /// <returns>图片下载URL</returns>
        public string GetImageDownloadUrl(Guid imageId)
        {
            return $"/api/ImageProxy/download/{imageId}";
        }

        /// <summary>
        /// 获取缩略图URL
        /// </summary>
        /// <param name="imageId">图片ID</param>
        /// <returns>缩略图URL</returns>
        public string GetThumbnailUrl(Guid imageId)
        {
            return $"/api/ImageProxy/thumbnail/{imageId}";
        }

        /// <summary>
        /// 检查图片是否存在
        /// </summary>
        /// <param name="imageId">图片ID</param>
        /// <returns>是否存在</returns>
        public async Task<bool> ImageExistsAsync(Guid imageId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/ImageProxy/view/{imageId}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 子任务图片信息 DTO
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
} 