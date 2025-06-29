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
            // 1. 调用API更新任务状态
            var client = _httpClientFactory.CreateClient("ApiService");
  
           
            await client.PutAsJsonAsync($"api/tasks/{mainTask.Id}", mainTask);
            // 2. 获取最新任务列表
            var tasks = await client.GetFromJsonAsync<List<MainTask>>("api/tasks") ?? new List<MainTask>();
            // 3. 推送到所有客户端
            await Clients.All.SendAsync("ReceiveTaskPosition", tasks);
        }
    }
}
