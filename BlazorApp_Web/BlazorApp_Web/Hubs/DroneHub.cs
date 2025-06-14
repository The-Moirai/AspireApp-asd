using ClassLibrary_Core.Drone;
using Microsoft.AspNetCore.SignalR;

namespace AspireApp_Drone.BlazorApp_Drone.Hubs
{
    public class DroneHub : Hub
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public DroneHub(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }
        /// <summary>
        /// 客户端请求将指定无人机设为离线，并同步所有客户端
        /// </summary>
        public async Task SetDroneOffline(string droneName)
        {
            // 1. 调用API更新无人机状态
            var client = _httpClientFactory.CreateClient("ApiService");
            // 获取当前无人机
            var drone = await client.GetFromJsonAsync<Drone>($"api/drones/{droneName}");
            if (drone != null)
            {
                drone.Status = DroneStatus.Offline;
                await client.PutAsJsonAsync($"api/drones/{droneName}", drone);
            }
            // 2. 获取最新无人机列表
            var drones = await client.GetFromJsonAsync<List<Drone>>("api/drones") ?? new List<Drone>();

            // 3. 推送到所有客户端
            await Clients.All.SendAsync("ReceiveDronesPosition", drones);
        }
        public async Task BroadcastDronesPosition()
        {
            var client = _httpClientFactory.CreateClient("ApiService");
            var drones = await client.GetFromJsonAsync<List<Drone>>("api/drones") ?? new List<Drone>();
            await Clients.All.SendAsync("ReceiveDronesPosition", drones);
        }

    }
}