using AspireApp_Drone.BlazorApp_Drone.Hubs;
using ClassLibrary_Core.Mission;
using ClassLibrary_Core.Data;
using Microsoft.AspNetCore.SignalR;

namespace BlazorApp_Web.Service
{
    /// <summary>
    /// 任务推送后台服务
    /// </summary>
    public class TaskPushBackgroundService : BackgroundService
    {
        private readonly IHubContext<TaskHub> _hubContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TaskPushBackgroundService> _logger;

        public TaskPushBackgroundService(IHubContext<TaskHub> hubContext,
                                        IHttpClientFactory httpClientFactory,
                                        ILogger<TaskPushBackgroundService> logger)
        {
            _hubContext = hubContext;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            
            // 🎯 订阅图片保存事件（跨项目事件监听）
            // 注意：这里需要通过反射或其他方式订阅，因为是跨程序集的静态事件
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var tasks = await GetTasksAsync();

                    // 推送任务数据到所有客户端
                    await _hubContext.Clients.All.SendAsync("ReceiveTaskPosition", tasks, cancellationToken: stoppingToken);

                    // 🖼️ 推送最近的图片更新（每5秒检查一次新图片）
                    //await PushRecentImageUpdates(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "推送任务数据时发生异常");
                }
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // 每5秒推送一次，降低CPU占用
            }
        }

        /// <summary>
        /// 推送最近的图片更新
        /// </summary>
        private async Task PushRecentImageUpdates(CancellationToken cancellationToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("ApiService");
                
                // 获取最近5分钟的图片更新
                var recentImages = await client.GetFromJsonAsync<List<object>>(
                    "api/Tasks/images/recent", cancellationToken);

                if (recentImages != null && recentImages.Any())
                {
                    // 推送图片更新事件到前端
                    await _hubContext.Clients.All.SendAsync("ReceiveImageUpdates", recentImages, cancellationToken: cancellationToken);
                    _logger.LogDebug("推送了 {Count} 个图片更新", recentImages.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "推送图片更新时发生异常");
            }
        }

        private async Task<List<MainTask>?> GetTasksAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("ApiService");
                client.Timeout = TimeSpan.FromSeconds(10);
                var tasks = await client.GetFromJsonAsync<List<MainTask>>("api/tasks");
                return tasks ?? new List<MainTask>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "无法连接到API服务");
                return null;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogWarning("API请求超时");
                return null;
            }
        }
    }
} 