using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Mission;
using Microsoft.AspNetCore.SignalR;

namespace AspireApp_Drone.BlazorApp_Drone.Hubs
{
    public class TaskHub : Hub
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public TaskHub(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }
        public async Task BroadcastTasksPosition(List<MainTask> mainTask)
        {
            await Clients.All.SendAsync("ReceiveTaskPosition", mainTask);
        }
        public async Task AddTask(MainTask mainTask)
        {
            // 1. 调用API更新无人机状态
            var client = _httpClientFactory.CreateClient("ApiService");
            // 获取当前无人机
            var drone = await client.GetFromJsonAsync<MainTask>($"api/task/{mainTask}");
           
            await client.PutAsJsonAsync($"api/Task/{mainTask.Id}", mainTask);
            // 2. 获取最新无人机列表
            var drones = await client.GetFromJsonAsync<List<Drone>>("api/task") ?? new List<Drone>();
            // 3. 推送到所有客户端
            await Clients.All.SendAsync("ReceiveDronesPosition", drones);
        }
    }
}
