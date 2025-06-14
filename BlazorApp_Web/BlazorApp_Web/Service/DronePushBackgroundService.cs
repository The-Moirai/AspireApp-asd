using AspireApp_Drone.BlazorApp_Drone.Hubs;
using ClassLibrary_Core.Drone;
using Microsoft.AspNetCore.SignalR;
using System.Net.Http;

namespace BlazorApp_Web.Service
{
    // DronePushBackgroundService.cs
    public class DronePushBackgroundService : BackgroundService
    {
        private readonly IHubContext<DroneHub> _hubContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DronePushBackgroundService> _logger;

        public DronePushBackgroundService(IHubContext<DroneHub> hubContext,
                                        IHttpClientFactory httpClientFactory,
                                            ILogger<DronePushBackgroundService  > logger)
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
                    var drones = await GetDronesAsync();

                    // 推送到所有客户端
                    await _hubContext.Clients.All.SendAsync("ReceiveDronesPosition", drones, cancellationToken: stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "推送无人机数据时发生异常");
                }

                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); // 每5秒推送一次
            }
        }

        private async Task<List<Drone>> GetDronesAsync()
        {
            var client = _httpClientFactory.CreateClient("ApiService");
            var drones = await client.GetFromJsonAsync<List<Drone>>("api/drones") ?? new List<Drone>();
            return drones;
        }
    }
}

