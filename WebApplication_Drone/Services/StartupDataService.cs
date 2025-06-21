using Microsoft.Extensions.Logging;

namespace WebApplication_Drone.Services
{
    /// <summary>
    /// 系统启动时的数据同步服务
    /// </summary>
    public class StartupDataService
    {
        private readonly DroneDataService _droneDataService;
        private readonly TaskDataService _taskDataService;
        private readonly SqlserverService _sqlserverService;
        private readonly ILogger<StartupDataService> _logger;

        public StartupDataService(
            DroneDataService droneDataService,
            TaskDataService taskDataService,
            SqlserverService sqlserverService,
            ILogger<StartupDataService> logger)
        {
            _droneDataService = droneDataService;
            _taskDataService = taskDataService;
            _sqlserverService = sqlserverService;
            _logger = logger;
        }

        /// <summary>
        /// 系统启动时初始化所有数据
        /// </summary>
        public async Task InitializeAllDataAsync()
        {
            var startTime = DateTime.Now;
            _logger.LogInformation("🚀 开始系统数据初始化...");
            
            try
            {
                // 检查数据量并选择合适的加载策略
                var strategy = await DetermineLoadingStrategyAsync();
                _logger.LogInformation("📊 选择加载策略: {Strategy}", strategy);

                // 并行初始化无人机和任务数据
                var droneTask = LoadDronesWithStrategyAsync(strategy);
                var taskTask = LoadTasksWithStrategyAsync(strategy);

                await Task.WhenAll(droneTask, taskTask);

                var duration = DateTime.Now - startTime;
                _logger.LogInformation("✅ 系统数据初始化完成，耗时: {Duration:mm\\:ss}", duration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 系统数据初始化失败: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 确定数据加载策略
        /// </summary>
        private async Task<LoadingStrategy> DetermineLoadingStrategyAsync()
        {
            try
            {
                var droneCount = await _sqlserverService.GetDroneCountAsync();
                var taskCount = await _sqlserverService.GetMainTaskCountAsync();

                _logger.LogInformation("📈 数据统计 - 无人机: {DroneCount}, 主任务: {TaskCount}", droneCount, taskCount);

                // 根据数据量选择策略
                var totalItems = droneCount + taskCount;
                
                if (totalItems < 1000)
                {
                    return LoadingStrategy.FullLoad;
                }
                else if (totalItems < 10000)
                {
                    return LoadingStrategy.BatchLoad;
                }
                else
                {
                    return LoadingStrategy.LazyLoad;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ 无法确定数据量，使用默认批量加载策略");
                return LoadingStrategy.BatchLoad;
            }
        }

        /// <summary>
        /// 根据策略加载无人机数据
        /// </summary>
        private async Task LoadDronesWithStrategyAsync(LoadingStrategy strategy)
        {
            try
            {
                switch (strategy)
                {
                    case LoadingStrategy.FullLoad:
                        _logger.LogDebug("🚁 使用全量加载无人机数据");
                        await _droneDataService.LoadDronesFromDatabaseAsync();
                        break;
                        
                    case LoadingStrategy.BatchLoad:
                        _logger.LogDebug("🚁 使用分批加载无人机数据");
                        await _droneDataService.LoadDronesFromDatabaseBatchAsync(100, 5);
                        break;
                        
                    case LoadingStrategy.LazyLoad:
                        _logger.LogDebug("🚁 使用懒加载无人机数据");
                        // 只加载最近活跃的无人机
                        await LoadRecentActiveDronesAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 加载无人机数据失败");
                throw;
            }
        }

        /// <summary>
        /// 根据策略加载任务数据
        /// </summary>
        private async Task LoadTasksWithStrategyAsync(LoadingStrategy strategy)
        {
            try
            {
                switch (strategy)
                {
                    case LoadingStrategy.FullLoad:
                        _logger.LogDebug("📋 使用全量加载任务数据");
                        await _taskDataService.LoadTasksFromDatabaseAsync();
                        break;
                        
                    case LoadingStrategy.BatchLoad:
                        _logger.LogDebug("📋 使用分批加载任务数据");
                        await _taskDataService.LoadTasksFromDatabaseBatchAsync(50, 5);
                        break;
                        
                    case LoadingStrategy.LazyLoad:
                        _logger.LogDebug("📋 使用懒加载任务数据");
                        // 只加载最近的活跃任务
                        await LoadRecentActiveTasksAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 加载任务数据失败");
                throw;
            }
        }

        /// <summary>
        /// 加载最近活跃的无人机（懒加载策略）
        /// </summary>
        private async Task LoadRecentActiveDronesAsync()
        {
            try
            {
                _logger.LogInformation("🚁 懒加载策略：回退到批量加载无人机数据");
                // 回退到批量加载
                await _droneDataService.LoadDronesFromDatabaseBatchAsync(100, 3);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 懒加载无人机数据失败");
                // 再次回退到全量加载
                await _droneDataService.LoadDronesFromDatabaseAsync();
            }
        }

        /// <summary>
        /// 加载最近活跃的任务（懒加载策略）
        /// </summary>
        private async Task LoadRecentActiveTasksAsync()
        {
            try
            {
                _logger.LogInformation("📋 懒加载策略：回退到批量加载任务数据");
                // 回退到批量加载
                await _taskDataService.LoadTasksFromDatabaseBatchAsync(50, 3);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 懒加载任务数据失败");
                // 再次回退到全量加载
                await _taskDataService.LoadTasksFromDatabaseAsync();
            }
        }

        /// <summary>
        /// 健康检查 - 验证数据加载完整性
        /// </summary>
        public async Task<DataHealthStatus> PerformHealthCheckAsync()
        {
            var status = new DataHealthStatus();
            
            try
            {
                // 检查无人机数据
                var memoryDroneCount = _droneDataService.GetDrones().Count;
                var dbDroneCount = await _sqlserverService.GetDroneCountAsync();
                status.DroneDataHealth = CalculateDataHealth(memoryDroneCount, dbDroneCount);
                
                // 检查任务数据
                var memoryTaskCount = _taskDataService.GetTasks().Count;
                var dbTaskCount = await _sqlserverService.GetMainTaskCountAsync();
                status.TaskDataHealth = CalculateDataHealth(memoryTaskCount, dbTaskCount);
                
                status.OverallHealth = Math.Min(status.DroneDataHealth, status.TaskDataHealth);
                
                _logger.LogInformation("🏥 数据健康检查完成 - 无人机: {DroneHealth:P}, 任务: {TaskHealth:P}, 总体: {OverallHealth:P}", 
                    status.DroneDataHealth, status.TaskDataHealth, status.OverallHealth);
                
                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 数据健康检查失败");
                status.OverallHealth = 0;
                return status;
            }
        }

        private static double CalculateDataHealth(int memoryCount, int dbCount)
        {
            if (dbCount == 0) return 1.0; // 数据库为空时认为100%健康
            return Math.Min(1.0, (double)memoryCount / dbCount);
        }
    }

    /// <summary>
    /// 数据加载策略
    /// </summary>
    public enum LoadingStrategy
    {
        /// <summary>全量加载</summary>
        FullLoad,
        /// <summary>分批加载</summary>
        BatchLoad,
        /// <summary>懒加载</summary>
        LazyLoad
    }

    /// <summary>
    /// 数据健康状态
    /// </summary>
    public class DataHealthStatus
    {
        /// <summary>无人机数据健康度 (0-1)</summary>
        public double DroneDataHealth { get; set; }
        
        /// <summary>任务数据健康度 (0-1)</summary>
        public double TaskDataHealth { get; set; }
        
        /// <summary>总体健康度 (0-1)</summary>
        public double OverallHealth { get; set; }
        
        /// <summary>是否健康 (>0.9)</summary>
        public bool IsHealthy => OverallHealth > 0.9;
    }
} 