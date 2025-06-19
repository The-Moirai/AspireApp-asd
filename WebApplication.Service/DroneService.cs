using WebApplication.Data;
using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Common;
using ClassLibrary_Core.Data;
using ClassLibrary_Core.Mission;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace WebApplication.Service
{
    public class DroneService : IDroneService
    {
        private readonly IDatabaseService _database;
        private readonly ILogger<DroneService> _logger;
        private readonly ConcurrentDictionary<Guid, Drone> _droneCache = new();
        private readonly object _lock = new();

        public event EventHandler<DroneChangedEventArgs>? DroneChanged;

        public DroneService(IDatabaseService database, ILogger<DroneService> logger)
        {
            _database = database;
            _logger = logger;
        }

        protected virtual void OnDroneChanged(string action, Drone drone)
        {
            DroneChanged?.Invoke(this, new DroneChangedEventArgs
            {
                Action = action,
                Drone = CloneDrone(drone)
            });
        }

        #region 基础 CRUD 操作

        public async Task<IEnumerable<Drone>> GetAllDronesAsync()
        {
            const string sql = "SELECT Id, Name, ModelStatus, ModelType, RegistrationDate, LastHeartbeat FROM Drones";
            return await _database.QueryAsync<Drone>(sql);
        }

        public async Task<Drone> GetDroneByIdAsync(Guid id)
        {
            const string sql = "SELECT Id, Name, ModelStatus, ModelType, RegistrationDate, LastHeartbeat FROM Drones WHERE Id = @Id";
            var drones = await _database.QueryAsync<Drone>(sql, new { Id = id });
            return drones.FirstOrDefault();
        }

        public async Task<Drone> GetDroneByNameAsync(string name)
        {
            const string sql = "SELECT Id, Name, ModelStatus, ModelType, RegistrationDate, LastHeartbeat FROM Drones WHERE Name = @Name";
            var drones = await _database.QueryAsync<Drone>(sql, new { Name = name });
            return drones.FirstOrDefault();
        }

        public async Task<Drone> CreateDroneAsync(Drone drone)
        {
            drone.Id = Guid.NewGuid();
            drone.RegistrationDate = DateTime.UtcNow;
            drone.LastHeartbeat = DateTime.UtcNow;
            
            const string sql = @"
                INSERT INTO Drones (Id, Name, ModelStatus, ModelType, RegistrationDate, LastHeartbeat)
                VALUES (@Id, @Name, @ModelStatus, @ModelType, @RegistrationDate, @LastHeartbeat)";
            
            await _database.ExecuteAsync(sql, new 
            { 
                drone.Id, 
                drone.Name, 
                ModelStatus = (int)drone.ModelStatus, 
                drone.ModelType,
                drone.RegistrationDate, 
                drone.LastHeartbeat 
            });
            
            _droneCache.TryAdd(drone.Id, CloneDrone(drone));
            OnDroneChanged("Add", drone);
            
            return drone;
        }

        public async Task<Drone> UpdateDroneAsync(Drone drone)
        {
            const string sql = @"
                UPDATE Drones 
                SET Name = @Name, ModelStatus = @ModelStatus, ModelType = @ModelType, LastHeartbeat = @LastHeartbeat
                WHERE Id = @Id";
            
            await _database.ExecuteAsync(sql, new 
            { 
                drone.Id, 
                drone.Name, 
                ModelStatus = (int)drone.ModelStatus, 
                drone.ModelType,
                drone.LastHeartbeat 
            });
            
            _droneCache.AddOrUpdate(drone.Id, drone, (key, oldValue) => CloneDrone(drone));
            OnDroneChanged("Update", drone);
            
            return drone;
        }

        public async Task DeleteDroneAsync(Guid id)
        {
            var drone = await GetDroneByIdAsync(id);
            if (drone != null)
            {
                const string sql = "DELETE FROM Drones WHERE Id = @Id";
                await _database.ExecuteAsync(sql, new { Id = id });
                
                _droneCache.TryRemove(id, out _);
                OnDroneChanged("Delete", drone);
            }
        }

        #endregion

        #region 状态和位置更新

        public async Task<Drone> UpdateDroneStatusAsync(Guid id, DroneStatus status)
        {
            const string sql = @"
                UPDATE Drones 
                SET Status = @Status, LastHeartbeat = @LastHeartbeat
                WHERE Id = @Id";
            
            var now = DateTime.UtcNow;
            await _database.ExecuteAsync(sql, new { Id = id, Status = (int)status, LastHeartbeat = now });
            
            var drone = await GetDroneByIdAsync(id);
            if (drone != null)
            {
                drone.Status = status;
                drone.LastHeartbeat = now;
                _droneCache.AddOrUpdate(id, drone, (key, oldValue) => CloneDrone(drone));
                OnDroneChanged("StatusUpdate", drone);
            }
            return drone;
        }

        public async Task<Drone> UpdateDronePositionAsync(Guid id, GPSPosition position)
        {
            // 需要将GPSPosition存储逻辑适配到数据库schema
            var drone = await GetDroneByIdAsync(id);
            if (drone != null)
            {
                drone.CurrentPosition = position;
                drone.LastHeartbeat = DateTime.UtcNow;
                
                // 这里需要根据实际的数据库设计来更新位置信息
                const string sql = "UPDATE Drones SET LastHeartbeat = @LastHeartbeat WHERE Id = @Id";
                await _database.ExecuteAsync(sql, new { Id = id, LastHeartbeat = drone.LastHeartbeat });
                
                _droneCache.AddOrUpdate(id, drone, (key, oldValue) => CloneDrone(drone));
                OnDroneChanged("PositionUpdate", drone);
            }
            return drone;
        }

        public async Task UpdateDroneHeartbeatAsync(Guid droneId)
        {
            const string sql = "UPDATE Drones SET LastHeartbeat = @LastHeartbeat WHERE Id = @Id";
            await _database.ExecuteAsync(sql, new { Id = droneId, LastHeartbeat = DateTime.UtcNow });
        }

        #endregion

        #region 子任务管理

        public async Task<List<SubTask>> GetDroneSubTasksAsync(Guid droneId)
        {
            // 需要根据数据库schema重写
            throw new NotImplementedException("需要重写为原生SQL");
        }

        public async Task<bool> AddSubTaskToDroneAsync(Guid droneId, SubTask subTask)
        {
            throw new NotImplementedException("需要重写为原生SQL");
        }

        public async Task<bool> UpdateDroneSubTaskAsync(Guid droneId, SubTask subTask)
        {
            throw new NotImplementedException("需要重写为原生SQL");
        }

        public async Task<bool> RemoveSubTaskFromDroneAsync(Guid droneId, Guid subTaskId)
        {
            throw new NotImplementedException("需要重写为原生SQL");
        }

        public async Task<List<SubTask>> GetActiveSubTasksForDroneAsync(string droneName)
        {
            throw new NotImplementedException("需要重写为原生SQL");
        }

        #endregion

        #region 数据历史查询

        public async Task<List<DroneDataPoint>> GetRecentDroneDataAsync(Guid droneId, TimeSpan duration)
        {
            throw new NotImplementedException("需要重写为原生SQL");
        }

        public async Task<List<DroneDataPoint>> GetDroneTaskDataAsync(Guid droneId, Guid taskId)
        {
            throw new NotImplementedException("需要重写为原生SQL");
        }

        public async Task<List<DroneDataPoint>> GetAllDronesDataInTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            throw new NotImplementedException("需要重写为原生SQL");
        }

        #endregion

        #region 集群状态

        public async Task<ClusterStatus> GetClusterStatusAsync()
        {
            throw new NotImplementedException("需要重写为原生SQL");
        }

        public async Task BulkUpdateDronesAsync(IEnumerable<Drone> drones)
        {
            throw new NotImplementedException("需要重写为原生SQL");
        }

        #endregion

        #region 数据记录

        public async Task RecordDroneStatusAsync(Drone drone)
        {
            throw new NotImplementedException("需要重写为原生SQL");
        }

        public async Task BulkRecordDroneStatusAsync(IEnumerable<Drone> drones)
        {
            throw new NotImplementedException("需要重写为原生SQL");
        }

        #endregion

        #region 连接管理

        public async Task UpdateDroneConnectionsAsync(List<Drone> drones)
        {
            throw new NotImplementedException("需要重写为原生SQL");
        }

        public async Task<bool> DroneExistsAsync(Guid id)
        {
            const string sql = "SELECT COUNT(1) FROM Drones WHERE Id = @Id";
            var count = await _database.QuerySingleAsync<int>(sql, new { Id = id });
            return count > 0;
        }

        #endregion

        private void UpdateDroneData(Drone existingDrone, Drone newDrone)
        {
            existingDrone.Name = newDrone.Name;
            existingDrone.ModelStatus = newDrone.ModelStatus;
            existingDrone.ModelType = newDrone.ModelType;
            existingDrone.CurrentPosition = newDrone.CurrentPosition;
            existingDrone.Status = newDrone.Status;
            existingDrone.LastHeartbeat = DateTime.UtcNow;
        }

        private Drone CloneDrone(Drone drone)
        {
            return new Drone
            {
                Id = drone.Id,
                Name = drone.Name,
                ModelStatus = drone.ModelStatus,
                ModelType = drone.ModelType,
                RegistrationDate = drone.RegistrationDate,
                LastHeartbeat = drone.LastHeartbeat,
                CurrentPosition = drone.CurrentPosition != null ? 
                    new GPSPosition 
                    { 
                        Latitude_x = drone.CurrentPosition.Latitude_x, 
                        Longitude_y = drone.CurrentPosition.Longitude_y 
                    } : null,
                Status = drone.Status,
                AssignedSubTasks = drone.AssignedSubTasks?.ToList() ?? new List<SubTask>()
            };
        }

        private double CalculateDistance(GPSPosition pos1, GPSPosition pos2)
        {
            const double R = 6371e3; // 地球半径，米
            var φ1 = pos1.Latitude_x * Math.PI / 180;
            var φ2 = pos2.Longitude_y * Math.PI / 180;
            var Δφ = (pos2.Latitude_x - pos1.Latitude_x) * Math.PI / 180;
            var Δλ = (pos2.Longitude_y - pos1.Longitude_y) * Math.PI / 180;

            var a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
                    Math.Cos(φ1) * Math.Cos(φ2) *
                    Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }
    }
} 