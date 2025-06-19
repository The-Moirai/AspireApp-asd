using ClassLibrary_Core.Drone;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace AspireApp_Drone.BlazorApp_Drone.Hubs
{
    public class DroneHub : Hub
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DroneHub> _logger;

        public DroneHub(IHttpClientFactory httpClientFactory, ILogger<DroneHub> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }
        /// <summary>
        /// 客户端请求将指定无人机设为离线，并同步所有客户端
        /// </summary>
        public async Task SetDroneOffline(Guid droneId)
        {
            _logger.LogInformation("Hub: 收到请求，设置无人机 {DroneId} 为离线状态。", droneId);
            // 1. 调用API更新无人机状态
            var client = _httpClientFactory.CreateClient("ApiService");
            try
            {
                var drone = await client.GetFromJsonAsync<Drone>($"api/drones/{droneId}");
                if (drone != null)
                {
                    _logger.LogInformation("Hub: 已通过API找到无人机 {DroneId}，正在更新其状态。", droneId);
                    drone.Status = DroneStatus.Offline;
                    var response = await client.PutAsJsonAsync($"api/drones/{droneId}", drone);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Hub: 通过API更新无人机 {DroneId} 状态成功。", droneId);
                        // 2. 获取最新无人机列表并推送到所有客户端
                        var drones = await client.GetFromJsonAsync<List<Drone>>("api/drones") ?? new List<Drone>();
                        _logger.LogInformation("Hub: 正在向所有客户端广播最新的无人机列表。");
                        await Clients.All.SendAsync("ReceiveDronesPosition", drones);
                    }
                    else
                    {
                        _logger.LogError("Hub: 更新无人机 {DroneId} 的API调用失败，状态码: {StatusCode}。", droneId, response.StatusCode);
                    }
                }
                else
                {
                    _logger.LogWarning("Hub: 未通过API找到ID为 {DroneId} 的无人机。", droneId);
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Hub: 处理设置无人机 {DroneId} 离线状态时发生异常。", droneId);
            }
        }
        public async Task BroadcastDronesPosition()
        {
            var client = _httpClientFactory.CreateClient("ApiService");
            var drones = await client.GetFromJsonAsync<List<Drone>>("api/drones") ?? new List<Drone>();
            await Clients.All.SendAsync("ReceiveDronesPosition", drones);
        }

    }
}