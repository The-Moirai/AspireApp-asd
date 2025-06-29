using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Data;
using Microsoft.Extensions.Logging;
using WebApplication_Drone.Services.Interfaces;
using System.Diagnostics;

namespace WebApplication_Drone.Services.Data
{
    /// <summary>
    /// 无人机数据访问实现
    /// </summary>
    public class DroneRepository : IDroneRepository
    {
        private readonly SqlserverService _sqlserverService;
        private readonly ILogger<DroneRepository> _logger;
        
        // 性能计数器
        private long _totalOperations = 0;
        private long _totalResponseTime = 0;
        
        public DroneRepository(SqlserverService sqlserverService, ILogger<DroneRepository> logger)
        {
            _sqlserverService = sqlserverService ?? throw new ArgumentNullException(nameof(sqlserverService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        #region 基础CRUD操作
        
        public async Task<List<Drone>> GetAllAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("获取所有无人机数据");
                var drones = await _sqlserverService.GetAllDronesAsync();
                return drones;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取所有无人机数据失败");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<Drone?> GetByIdAsync(Guid id)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("根据ID获取无人机: {DroneId}", id);
                return await _sqlserverService.GetDroneAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "根据ID获取无人机失败: {DroneId}", id);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<Drone?> GetByNameAsync(string droneName)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("根据名称获取无人机: {DroneName}", droneName);
                return await _sqlserverService.GetDroneByNameAsync(droneName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "根据名称获取无人机失败: {DroneName}", droneName);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<bool> AddAsync(Drone drone)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("添加无人机: {DroneId}, {DroneName}", drone.Id, drone.Name);
                await _sqlserverService.AddOrUpdateDroneAsync(drone);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加无人机失败: {DroneId}", drone.Id);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<bool> UpdateAsync(Drone drone)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("更新无人机: {DroneId}", drone.Id);
                await _sqlserverService.AddOrUpdateDroneAsync(drone);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新无人机失败: {DroneId}", drone.Id);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<bool> DeleteAsync(Guid id)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("删除无人机: {DroneId}", id);
                // 注意：SqlserverService中没有DeleteDroneAsync方法，这里需要实现
                // 暂时返回true，实际应该调用数据库删除操作
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除无人机失败: {DroneId}", id);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<int> GetCountAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("获取无人机数量");
                return await _sqlserverService.GetDroneCountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取无人机数量失败");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        #endregion
        
        #region 批量操作
        
        public async Task<bool> BulkUpdateAsync(IEnumerable<Drone> drones)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("批量更新无人机: {Count} 个", drones.Count());
                await _sqlserverService.BulkUpdateDrones(drones);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量更新无人机失败");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<bool> BulkRecordStatusAsync(IEnumerable<Drone> drones)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("批量记录无人机状态: {Count} 个", drones.Count());
                await _sqlserverService.BulkRecordDroneStatusAsync(drones);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量记录无人机状态失败");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        #endregion
        
        #region 数据点操作
        
        public async Task<List<DroneDataPoint>> GetDataInTimeRangeAsync(Guid droneId, DateTime startTime, DateTime endTime)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("获取无人机数据点: {DroneId}, {StartTime} - {EndTime}", droneId, startTime, endTime);
                return await _sqlserverService.GetDroneDataInTimeRangeAsync(droneId, startTime, endTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取无人机数据点失败: {DroneId}", droneId);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<List<DroneDataPoint>> GetAllDronesDataInTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("获取所有无人机数据点: {StartTime} - {EndTime}", startTime, endTime);
                return await _sqlserverService.GetAllDronesDataInTimeRangeAsync(startTime, endTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取所有无人机数据点失败");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        #endregion
        
        #region 状态检查
        
        public async Task<bool> ExistsAsync(Guid id)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                _logger.LogDebug("检查无人机是否存在: {DroneId}", id);
                return await _sqlserverService.DroneExistsAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查无人机是否存在失败: {DroneId}", id);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                // 简单的健康检查：尝试获取无人机数量
                await GetCountAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        #endregion
        
        #region 统计信息
        
        public async Task<DroneRepositoryStatistics> GetStatisticsAsync()
        {
            try
            {
                var count = await GetCountAsync();
                
                return new DroneRepositoryStatistics
                {
                    TotalDrones = count,
                    OnlineDrones = 0, // 需要从数据库查询在线状态
                    OfflineDrones = 0, // 需要从数据库查询离线状态
                    TotalOperations = _totalOperations,
                    AverageResponseTimeMs = _totalOperations > 0 ? (double)_totalResponseTime / _totalOperations : 0,
                    LastUpdateTime = DateTime.UtcNow,
                    DatabaseConnected = await IsHealthyAsync()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取无人机数据访问统计信息失败");
                throw;
            }
        }
        
        #endregion
    }
} 