using ClassLibrary_Core.Data;
using ClassLibrary_Core.Drone;

namespace BlazorApp_Web.Service
{
    public class HistoryApiService
    {
        private readonly HttpClient _http;
        private readonly ILogger<HistoryApiService> _logger;

        public HistoryApiService(
            IHttpClientFactory httpClientFactory,
            ILogger<HistoryApiService> logger)
        {
            _http = httpClientFactory.CreateClient("ApiService");
            _logger = logger;
        }
        public async Task<List<Drone>> GetAllDroneDataAsync()
        {
            try
            {
                var response = await _http.GetAsync($"api/historydata/drone");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<List<Drone>>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching recent drone data");
                throw;
            }
        }
        public async Task<List<DroneDataPoint>> GetRecentDroneDataAsync(Guid droneId, TimeSpan duration)
        {
            try
            {
                var response = await _http.GetAsync($"api/historydata/drone/{droneId}/recent?duration={duration.TotalMinutes}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<List<DroneDataPoint>>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching recent drone data");
                throw;
            }
        }
        public async Task<List<DroneDataPoint>> GetDroneTaskDataAsync(Guid droneId, Guid taskId)
        {
            try
            {
                var response = await _http.GetAsync($"api/historydata/drone/{droneId}/task/{taskId}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<List<DroneDataPoint>>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching drone task data");
                throw;
            }
        }

    }
}
