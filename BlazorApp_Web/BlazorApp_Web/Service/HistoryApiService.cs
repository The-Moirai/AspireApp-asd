using ClassLibrary_Core.Data;
using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Mission;

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

        #region 无人机历史数据查询

        /// <summary>
        /// 获取所有无人机数据
        /// </summary>
        public async Task<List<Drone>> GetAllDroneDataAsync()
        {
            try
            {
                var response = await _http.GetAsync("api/historydata/drones/all");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<Drone>>>();
                return result?.Data ?? new List<Drone>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all drone data");
                throw;
            }
        }

        /// <summary>
        /// 获取无人机最近一段时间的数据
        /// </summary>
        public async Task<List<DroneDataPoint>> GetRecentDroneDataAsync(Guid droneId, TimeSpan duration)
        {
            try
            {
                var response = await _http.GetAsync($"api/historydata/drone/{droneId}/recent?duration={duration}");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<DroneDataPoint>>>();
                return result?.Data ?? new List<DroneDataPoint>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching recent drone data");
                throw;
            }
        }

        /// <summary>
        /// 获取无人机在指定任务期间的数据
        /// </summary>
        public async Task<List<DroneDataPoint>> GetDroneTaskDataAsync(Guid droneId, Guid taskId)
        {
            try
            {
                var response = await _http.GetAsync($"api/historydata/drone/{droneId}/task/{taskId}");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<DroneDataPoint>>>();
                return result?.Data ?? new List<DroneDataPoint>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching drone task data");
                throw;
            }
        }

        /// <summary>
        /// 获取指定无人机详细信息
        /// </summary>
        public async Task<Drone?> GetDroneDetailsAsync(Guid droneId)
        {
            try
            {
                var response = await _http.GetAsync($"api/historydata/drone/{droneId}");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<Drone>>();
                return result?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching drone details");
                throw;
            }
        }

        /// <summary>
        /// 获取指定无人机的子任务
        /// </summary>
        public async Task<List<SubTask>> GetDroneSubTasksAsync(Guid droneId)
        {
            try
            {
                var response = await _http.GetAsync($"api/historydata/drone/{droneId}/subtasks");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<SubTask>>>();
                return result?.Data ?? new List<SubTask>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching drone subtasks");
                throw;
            }
        }

        #endregion

        #region 任务历史数据查询

        /// <summary>
        /// 获取任务中指定无人机的数据
        /// </summary>
        public async Task<List<DroneDataPoint>> GetTaskDroneDataAsync(Guid taskId, Guid droneId)
        {
            try
            {
                var response = await _http.GetAsync($"api/historydata/task/{taskId}/drone/{droneId}");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<DroneDataPoint>>>();
                return result?.Data ?? new List<DroneDataPoint>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching task drone data");
                throw;
            }
        }

        /// <summary>
        /// 获取任务期间所有无人机的数据
        /// </summary>
        public async Task<List<DroneDataPoint>> GetTaskAllDronesDataAsync(Guid taskId)
        {
            try
            {
                var response = await _http.GetAsync($"api/historydata/task/{taskId}/drones");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<DroneDataPoint>>>();
                return result?.Data ?? new List<DroneDataPoint>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching task all drones data");
                throw;
            }
        }

        /// <summary>
        /// 获取所有任务
        /// </summary>
        public async Task<List<MainTask>> GetAllTasksAsync()
        {
            try
            {
                var response = await _http.GetAsync("api/historydata/tasks/all");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<MainTask>>>();
                return result?.Data ?? new List<MainTask>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all tasks");
                throw;
            }
        }

        /// <summary>
        /// 获取指定任务详细信息
        /// </summary>
        public async Task<MainTask?> GetTaskDetailsAsync(Guid taskId)
        {
            try
            {
                var response = await _http.GetAsync($"api/historydata/task/{taskId}");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<MainTask>>();
                return result?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching task details");
                throw;
            }
        }

        /// <summary>
        /// 根据状态获取任务
        /// </summary>
        public async Task<List<MainTask>> GetTasksByStatusAsync(TaskStatus status)
        {
            try
            {
                var response = await _http.GetAsync($"api/historydata/tasks/status/{(int)status}");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<MainTask>>>();
                return result?.Data ?? new List<MainTask>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tasks by status");
                throw;
            }
        }

        /// <summary>
        /// 获取指定任务的子任务
        /// </summary>
        public async Task<List<SubTask>> GetTaskSubTasksAsync(Guid taskId)
        {
            try
            {
                var response = await _http.GetAsync($"api/historydata/task/{taskId}/subtasks");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<SubTask>>>();
                return result?.Data ?? new List<SubTask>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching task subtasks");
                throw;
            }
        }

        #endregion

        #region 时间范围查询

        /// <summary>
        /// 获取指定时间段内所有无人机的数据
        /// </summary>
        public async Task<List<DroneDataPoint>> GetAllDronesDataInTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            try
            {
                var response = await _http.GetAsync($"api/historydata/drones/time-range?startTime={startTime:O}&endTime={endTime:O}");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<DroneDataPoint>>>();
                return result?.Data ?? new List<DroneDataPoint>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching drones data in time range");
                throw;
            }
        }

        /// <summary>
        /// 获取指定时间段内所有任务的数据
        /// </summary>
        public async Task<List<DroneDataPoint>> GetAllTasksDataInTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            try
            {
                var response = await _http.GetAsync($"api/historydata/tasks/time-range?startTime={startTime:O}&endTime={endTime:O}");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<DroneDataPoint>>>();
                return result?.Data ?? new List<DroneDataPoint>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tasks data in time range");
                throw;
            }
        }

        #endregion

        #region 统计和分析功能

        /// <summary>
        /// 获取任务统计信息
        /// </summary>
        public async Task<TaskStatistics?> GetTaskStatisticsAsync()
        {
            try
            {
                var response = await _http.GetAsync("api/historydata/statistics/tasks");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<TaskStatistics>>();
                return result?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching task statistics");
                throw;
            }
        }

        /// <summary>
        /// 获取任务性能分析
        /// </summary>
        public async Task<TaskPerformanceAnalysis?> GetTaskPerformanceAnalysisAsync()
        {
            try
            {
                var response = await _http.GetAsync("api/historydata/analysis/task-performance");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<TaskPerformanceAnalysis>>();
                return result?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching task performance analysis");
                throw;
            }
        }

        /// <summary>
        /// 获取指定无人机的活跃子任务
        /// </summary>
        public async Task<List<SubTask>> GetDroneActiveTasksAsync(string droneName)
        {
            try
            {
                var response = await _http.GetAsync($"api/historydata/drone/{droneName}/active-tasks");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<SubTask>>>();
                return result?.Data ?? new List<SubTask>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching drone active tasks");
                throw;
            }
        }

        /// <summary>
        /// 获取过期的子任务
        /// </summary>
        public async Task<List<SubTask>> GetExpiredSubTasksAsync(int timeoutMinutes = 30)
        {
            try
            {
                var response = await _http.GetAsync($"api/historydata/analysis/expired-tasks?timeoutMinutes={timeoutMinutes}");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<SubTask>>>();
                return result?.Data ?? new List<SubTask>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching expired subtasks");
                throw;
            }
        }

        /// <summary>
        /// 获取系统概览
        /// </summary>
        public async Task<SystemOverview?> GetSystemOverviewAsync()
        {
            try
            {
                var response = await _http.GetAsync("api/historydata/overview");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<SystemOverview>>();
                return result?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching system overview");
                throw;
            }
        }

        #endregion

        #region 数据管理功能

        /// <summary>
        /// 从数据库加载所有任务
        /// </summary>
        public async Task<bool> LoadTasksFromDatabaseAsync()
        {
            try
            {
                var response = await _http.PostAsync("api/historydata/tasks/load-from-database", null);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading tasks from database");
                throw;
            }
        }

        /// <summary>
        /// 同步所有任务到数据库
        /// </summary>
        public async Task<bool> SyncTasksToDatabaseAsync()
        {
            try
            {
                var response = await _http.PostAsync("api/historydata/tasks/sync-to-database", null);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing tasks to database");
                throw;
            }
        }

        /// <summary>
        /// 重新分配失败的子任务
        /// </summary>
        public async Task<int> ReassignFailedSubTasksAsync()
        {
            try
            {
                var response = await _http.PostAsync("api/historydata/tasks/reassign-failed", null);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<ManagementResult>>();
                return result?.Data?.ReassignedCount ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reassigning failed subtasks");
                throw;
            }
        }

        /// <summary>
        /// 清理旧的已完成任务
        /// </summary>
        public async Task<int> CleanupOldCompletedTasksAsync(int maxAgeDays = 30)
        {
            try
            {
                var response = await _http.DeleteAsync($"api/historydata/tasks/cleanup-old?maxAgeDays={maxAgeDays}");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<ManagementResult>>();
                return result?.Data?.CleanedCount ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old tasks");
                throw;
            }
        }

        #endregion
    }

    #region 辅助类

    /// <summary>
    /// API响应包装类
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>
    /// 系统概览数据
    /// </summary>
    public class SystemOverview
    {
        public DateTime Timestamp { get; set; }
        public DroneOverview Drones { get; set; } = new();
        public TaskStatistics Tasks { get; set; } = new();
        public TaskPerformanceAnalysis Performance { get; set; } = new();
    }

    public class DroneOverview
    {
        public int Total { get; set; }
        public int Online { get; set; }
        public int Offline { get; set; }
    }

    /// <summary>
    /// 管理操作结果
    /// </summary>
    public class ManagementResult
    {
        public int ReassignedCount { get; set; }
        public int CleanedCount { get; set; }
        public int UpdatedCount { get; set; }
    }

    #endregion
}
