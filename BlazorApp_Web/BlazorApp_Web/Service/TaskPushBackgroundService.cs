using AspireApp_Drone.BlazorApp_Drone.Hubs;
using ClassLibrary_Core.Mission;
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
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var tasks = await GetTasksAsync();

                    // 推送到所有客户端
                    await _hubContext.Clients.All.SendAsync("ReceiveTaskPosition", tasks, cancellationToken: stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "推送任务数据时发生异常");
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // 每5秒推送一次，降低CPU占用
            }
        }

        private async Task<List<MainTask>> GetTasksAsync()
        {
            var client = _httpClientFactory.CreateClient("ApiService");
            var tasks = await client.GetFromJsonAsync<List<MainTask>>("api/tasks") ?? new List<MainTask>();
            return tasks;
        }
    }
} 