using ClassLibrary_Core.Common;
using ClassLibrary_Core.Data;
using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Mission;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Data.Common;

namespace WebApplication_Drone.Services
{
    public class SqlserverService
    {
        private readonly string connectionString;
        private readonly SqlConnection _connection;
        private readonly ILogger<SqlserverService> _logger;
        public SqlserverService(ILogger<SqlserverService> logger)
        {
            connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__app-db");
            _connection = new SqlConnection(connectionString);
            _logger = logger;
            _logger.LogDebug(connectionString);

        }
        public void run()
        {
            _connection.Open();
        }
        // 通用执行方法
        private int ExecuteNonQuery(string sql, params SqlParameter[] parameters)
        {
            try
            {
                using var command = new SqlCommand(sql, _connection);
                command.Parameters.AddRange(parameters);
                return command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing query: {sql}");
                throw;
            }
        }

        // 查询方法
        private SqlDataReader ExecuteReader(string sql, params SqlParameter[] parameters)
        {
            try
            {
                using var command = new SqlCommand(sql, _connection);
                command.Parameters.AddRange(parameters);
                return command.ExecuteReader();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing query: {sql}");
                throw;
            }
        }

        // 批量无人机更新
        public async Task BulkUpdateDrones(IEnumerable<Drone> drones)
        {
            using var transaction = _connection.BeginTransaction();
            try
            {
                foreach (var drone in drones)
                    await AddOrUpdateDroneAsync(drone, transaction);

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        // 无人机存在检查
        public async Task<bool> DroneExistsAsync(Guid id)
        {
            var cmd = new SqlCommand("SELECT 1 FROM Drones WHERE Id=@id", _connection);
            cmd.Parameters.AddWithValue("@id", id);
            return (await cmd.ExecuteScalarAsync()) != null;
        }
        // 异步添加/更新无人机
        public async Task AddOrUpdateDroneAsync(Drone drone, SqlTransaction? transaction = null)
        {
            var sql = DroneExistsAsync(drone.Id).Result
                ? @"UPDATE Drones SET Status=@status, LastHeartbeat=@now, 
                  PositionX=@x, PositionY=@y WHERE Id=@id"
                : @"INSERT INTO Drones (Id, DroneId, Status, LastHeartbeat, PositionX, PositionY) 
               VALUES (@id, @name, @status, @now, @x, @y)";

            using var cmd = new SqlCommand(sql, _connection, transaction);
            cmd.Parameters.AddRange(new[]
            {
            new SqlParameter("@id", drone.Id),
            new SqlParameter("@name", drone.Name),
            new SqlParameter("@status", (int)drone.Status),
            new SqlParameter("@now", DateTime.UtcNow),
            new SqlParameter("@x", drone.CurrentPosition.Latitude_x),
            new SqlParameter("@y", drone.CurrentPosition.Longitude_y)
        });

            await cmd.ExecuteNonQueryAsync();
        }
        // 完善：获取无人机状态历史
        public async Task<List<DroneDataPoint>> GetDroneStatusHistoryAsync(
            Guid droneId,
            DateTime start,
            DateTime end)
        {
            var sql = @"SELECT * FROM DroneStatusHistory 
                   WHERE DroneId=@droneId AND Timestamp BETWEEN @start AND @end";

            using var cmd = new SqlCommand(sql, _connection);
            cmd.Parameters.AddRange(new[]
            {
            new SqlParameter("@droneId", droneId),
            new SqlParameter("@start", start),
            new SqlParameter("@end", end)
        });

            var results = new List<DroneDataPoint>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (reader.Read())
            {
                results.Add(new DroneDataPoint
                {
                    DroneId = droneId,
                    Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp")),
                    cpuUsage = reader.GetDouble(reader.GetOrdinal("CpuUsage")),
                    Latitude = reader.GetDouble(reader.GetOrdinal("Latitude")),
                    Longitude = reader.GetDouble(reader.GetOrdinal("Longitude"))
                });
            }
            return results;
        }


        // 插入无人机
        public Guid AddDrone(Drone drone)
        {
            drone.Id = Guid.NewGuid();
            var sql = @"
            INSERT INTO Drones (Id, DroneId, ModelType, RegistrationDate, LastHeartbeat)
            VALUES (@Id, @DroneId, @ModelType, @RegistrationDate, @LastHeartbeat)";

            var parameters = new[]
            {
            new SqlParameter("@Id", drone.Id),
            new SqlParameter("@DroneId", drone.Name),
            new SqlParameter("@ModelType", drone.ModelStatus.ToString()),
            new SqlParameter("@RegistrationDate", DateTime.Now)
        };

            ExecuteNonQuery(sql, parameters);
            return drone.Id;
        }

        // 更新无人机最后心跳
        public void UpdateDroneHeartbeat(Guid droneId)
        {
            var sql = "UPDATE Drones SET LastHeartbeat = GETUTCDATE() WHERE Id = @Id";
            ExecuteNonQuery(sql, new SqlParameter("@Id", droneId));
        }

        // 添加主任务
        public Guid AddMainTask(MainTask task, string CreatedBy)
        {
            task.Id = Guid.NewGuid();
            task.CreationTime = DateTime.UtcNow;

            var sql = @"
            INSERT INTO MainTasks (Id, Description, Status, CreationTime, CompletedTime, CreatedBy)
            VALUES (@Id, @Description, @Status, @CreationTime, @CompletedTime, @CreatedBy)";

            var parameters = new[]
            {
            new SqlParameter("@Id", task.Id),
            new SqlParameter("@Description", task.Description),
            new SqlParameter("@Status", (int)task.Status),
            new SqlParameter("@CreationTime", task.CreationTime),
            new SqlParameter("@CompletedTime", task.CompletedTime ?? (object)DBNull.Value),
            new SqlParameter("@CreatedBy", CreatedBy)
        };

            ExecuteNonQuery(sql, parameters);
            return task.Id;
        }

        // 添加子任务
        public Guid AddSubTask(SubTask subTask)
        {
            subTask.Id = Guid.NewGuid();
            subTask.CreationTime = DateTime.UtcNow;

            var sql = @"
            INSERT INTO SubTasks (Id, Description, Status, CreationTime, CompletedTime, ParentTaskId, ReassignmentCount)
            VALUES (@Id, @Description, @Status, @CreationTime, @CompletedTime, @ParentTaskId, @ReassignmentCount)";

            var parameters = new[]
            {
            new SqlParameter("@Id", subTask.Id),
            new SqlParameter("@Description", subTask.Description),
            new SqlParameter("@Status", (int)subTask.Status),
            new SqlParameter("@CreationTime", subTask.CreationTime),
            new SqlParameter("@CompletedTime", subTask.CompletedTime ?? (object)DBNull.Value),
            new SqlParameter("@ParentTaskId", subTask.ParentTask),
            new SqlParameter("@ReassignmentCount", subTask.ReassignmentCount)
        };

            ExecuteNonQuery(sql, parameters);
            return subTask.Id;
        }

        // 记录无人机状态
        public void RecordDroneStatus(DroneStatusHistory status)
        {
            status.Timestamp = DateTime.UtcNow;

            var sql = @"
            INSERT INTO DroneStatusHistory (DroneId, Status, Timestamp, CpuUsage, BandwidthAvailable, MemoryUsage, Latitude, Longitude, BatteryLevel, NetworkStrength)
            VALUES (@DroneId, @Status, @Timestamp, @CpuUsage, @BandwidthAvailable, @MemoryUsage, @Latitude, @Longitude, @BatteryLevel, @NetworkStrength)";

            var parameters = new[]
            {
            new SqlParameter("@DroneId", status.DroneId),
            new SqlParameter("@Status", (int)status.Status),
            new SqlParameter("@Timestamp", status.Timestamp),
            new SqlParameter("@CpuUsage", status.CpuUsage ?? (object)DBNull.Value),
            new SqlParameter("@BandwidthAvailable", status.BandwidthAvailable ?? (object)DBNull.Value),
            new SqlParameter("@MemoryUsage", status.MemoryUsage ?? (object)DBNull.Value),
            new SqlParameter("@Latitude", status.Latitude ?? (object)DBNull.Value),
            new SqlParameter("@Longitude", status.Longitude ?? (object)DBNull.Value),
            new SqlParameter("@BatteryLevel", status.BatteryLevel ?? (object)DBNull.Value),
            new SqlParameter("@NetworkStrength", status.NetworkStrength ?? (object)DBNull.Value)
        };

            ExecuteNonQuery(sql, parameters);
        }

        // 分配子任务给无人机
        public void AssignSubTaskToDrone(Guid droneId, Guid subTaskId)
        {
            // 停用之前的分配
            var deactivateSql = "UPDATE DroneSubTasks SET IsActive = 0 WHERE SubTaskId = @SubTaskId";
            ExecuteNonQuery(deactivateSql, new SqlParameter("@SubTaskId", subTaskId));

            // 创建新分配
            var sql = @"
            INSERT INTO DroneSubTasks (DroneId, SubTaskId, AssignmentTime, IsActive)
            VALUES (@DroneId, @SubTaskId, @AssignmentTime, 1)";

            var parameters = new[]
            {
            new SqlParameter("@DroneId", droneId),
            new SqlParameter("@SubTaskId", subTaskId),
            new SqlParameter("@AssignmentTime", DateTime.UtcNow)
        };

            ExecuteNonQuery(sql, parameters);

            // 更新子任务状态
            UpdateSubTaskStatus(subTaskId, TaskStatus.WaitingToRun);
        }

        // 更新子任务状态
        public void UpdateSubTaskStatus(Guid subTaskId, TaskStatus newStatus, string reason = null)
        {
            // 获取当前状态
            var currentStatus = GetSubTaskStatus(subTaskId);

            // 记录状态变更历史
            var historySql = @"
            INSERT INTO SubTaskHistory (SubTaskId, OldStatus, NewStatus, ChangeTime, ChangedBy, Reason)
            VALUES (@SubTaskId, @OldStatus, @NewStatus, @ChangeTime, @ChangedBy, @Reason)";

            var historyParams = new[]
            {
            new SqlParameter("@SubTaskId", subTaskId),
            new SqlParameter("@OldStatus", currentStatus.HasValue ? (int)currentStatus.Value : (object)DBNull.Value),
            new SqlParameter("@NewStatus", (int)newStatus),
            new SqlParameter("@ChangeTime", DateTime.UtcNow),
            new SqlParameter("@ChangedBy", Environment.UserName),
            new SqlParameter("@Reason", reason ?? "Status update")
        };

            ExecuteNonQuery(historySql, historyParams);

            // 更新子任务状态
            var updateSql = "UPDATE SubTasks SET Status = @Status WHERE Id = @Id";

            var updateParams = new[]
            {
            new SqlParameter("@Status", (int)newStatus),
            new SqlParameter("@Id", subTaskId)
        };

            // 如果是完成状态，设置完成时间
            if (newStatus == TaskStatus.RanToCompletion || newStatus == TaskStatus.Faulted)
            {
                updateSql = "UPDATE SubTasks SET Status = @Status, CompletedTime = @CompletedTime WHERE Id = @Id";
                updateParams = new[]
                {
                new SqlParameter("@Status", (int)newStatus),
                new SqlParameter("@CompletedTime", DateTime.UtcNow),
                new SqlParameter("@Id", subTaskId)
            };
            }

            ExecuteNonQuery(updateSql, updateParams);
        }

        // 获取子任务当前状态
        private TaskStatus? GetSubTaskStatus(Guid subTaskId)
        {
            var sql = "SELECT Status FROM SubTasks WHERE Id = @Id";
            using var reader = ExecuteReader(sql, new SqlParameter("@Id", subTaskId));

            if (reader.Read())
            {
                return (TaskStatus)reader.GetByte(0);
            }
            return null;
        }

        // 获取无人机的活动任务
        public List<SubTask> GetDroneActiveTasks(Guid droneId)
        {
            var tasks = new List<SubTask>();
            var sql = @"
            SELECT st.* 
            FROM DroneSubTasks dst
            JOIN SubTasks st ON dst.SubTaskId = st.Id
            WHERE dst.DroneId = @DroneId AND dst.IsActive = 1";

            using var reader = ExecuteReader(sql, new SqlParameter("@DroneId", droneId));

            while (reader.Read())
            {
                tasks.Add(new SubTask
                {
                    Id = reader.GetGuid(0),
                    Description = reader.GetString(1),
                    Status = (TaskStatus)reader.GetByte(2),
                    CreationTime = reader.GetDateTime(3),
                    AssignedTime = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    CompletedTime = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    ParentTask = reader.GetGuid(6),
                    ReassignmentCount = reader.GetInt32(7),
                });
            }

            return tasks;
        }

        // 获取任务历史
        public List<SubTaskHistory> GetSubTaskHistory(Guid subTaskId)
        {
            var history = new List<SubTaskHistory>();
            var sql = "SELECT * FROM SubTaskHistory WHERE SubTaskId = @SubTaskId ORDER BY ChangeTime DESC";

            using var reader = ExecuteReader(sql, new SqlParameter("@SubTaskId", subTaskId));

            while (reader.Read())
            {
                history.Add(new SubTaskHistory
                {
                    Id = reader.GetInt64(0),
                    SubTaskId = reader.GetGuid(1),
                    OldStatus = reader.IsDBNull(2) ? null : (TaskStatus?)reader.GetByte(2),
                    NewStatus = (TaskStatus)reader.GetByte(3),
                    ChangeTime = reader.GetDateTime(4),
                    ChangedBy = reader.IsDBNull(5) ? null : reader.GetString(5),
                    DroneId = reader.IsDBNull(6) ? null : (Guid?)reader.GetGuid(6),
                    Reason = reader.IsDBNull(7) ? null : reader.GetString(7),
                    AdditionalInfo = reader.IsDBNull(8) ? null : reader.GetString(8)
                });
            }

            return history;
        }

        // 获取无人机状态历史
        public List<DroneStatusHistory> GetDroneStatusHistory(Guid droneId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var history = new List<DroneStatusHistory>();
            var sql = "SELECT * FROM DroneStatusHistory WHERE DroneId = @DroneId";
            var parameters = new List<SqlParameter>
        {
            new SqlParameter("@DroneId", droneId)
        };

            if (startDate.HasValue)
            {
                sql += " AND Timestamp >= @StartDate";
                parameters.Add(new SqlParameter("@StartDate", startDate.Value));
            }

            if (endDate.HasValue)
            {
                sql += " AND Timestamp <= @EndDate";
                parameters.Add(new SqlParameter("@EndDate", endDate.Value));
            }

            sql += " ORDER BY Timestamp DESC";

            using var reader = ExecuteReader(sql, parameters.ToArray());

            while (reader.Read())
            {
                history.Add(new DroneStatusHistory
                {
                    Id = reader.GetInt64(0),
                    DroneId = reader.GetGuid(1),
                    Status = (DroneStatus)reader.GetByte(2),
                    Timestamp = reader.GetDateTime(3),
                    CpuUsage = reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                    BandwidthAvailable = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                    MemoryUsage = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                    Latitude = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                    Longitude = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                    BatteryLevel = reader.IsDBNull(9) ? null : reader.GetDecimal(9),
                    NetworkStrength = reader.IsDBNull(10) ? null : (byte?)reader.GetByte(10)
                });
            }

            return history;
        }
        /// <summary>
        /// 获取任务的时间范围
        /// </summary>
        public async Task<TaskTimeRange?> GetTaskTimeRangeAsync(Guid taskId)
        {
            const string sql = @"
            SELECT MIN(CreationTime) AS StartTime, 
                   MAX(COALESCE(CompletedTime, GETUTCDATE())) AS EndTime
            FROM SubTasks
            WHERE ParentTaskId = @taskId";

            try
            {
                using var command = new SqlCommand(sql, _connection);
                command.Parameters.AddWithValue("@taskId", taskId);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    // 如果没有记录，返回null
                    if (reader.IsDBNull(0))
                    {
                        return null;
                    }

                    return new TaskTimeRange
                    {
                        StartTime = reader.GetDateTime(0),
                        EndTime = reader.GetDateTime(1)
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting task time range for task {TaskId}", taskId);
            }

            return null;
        }
        /// <summary>
        /// 获取无人机在指定时间范围内的数据点
        /// </summary>
        public async Task<List<DroneDataPoint>> GetDroneDataInTimeRangeAsync(
            Guid droneId,
            DateTime startTime,
            DateTime endTime)
        {
            const string sql = @"
            SELECT Timestamp, CpuUsage, Latitude, Longitude, 
                   BandwidthAvailable, MemoryUsage, BatteryLevel
            FROM DroneStatusHistory 
            WHERE DroneId = @droneId 
              AND Timestamp BETWEEN @startTime AND @endTime
            ORDER BY Timestamp";

            var results = new List<DroneDataPoint>();

            try
            {
                using var command = new SqlCommand(sql, _connection);
                command.Parameters.AddWithValue("@droneId", droneId);
                command.Parameters.AddWithValue("@startTime", startTime);
                command.Parameters.AddWithValue("@endTime", endTime);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new DroneDataPoint
                    {
                        DroneId = droneId,
                        Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp")),
                        cpuUsage = reader.GetDouble(reader.GetOrdinal("CpuUsage")),
                        Latitude = reader.GetDouble(reader.GetOrdinal("Latitude")),
                        Longitude = reader.GetDouble(reader.GetOrdinal("Longitude"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting drone data for {DroneId} between {Start} and {End}",
                    droneId, startTime, endTime);
            }

            return results;
        }

        /// <summary>
        /// 获取所有无人机在指定时间范围内的数据点
        /// </summary>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <returns>无人机数据点列表</returns>
        public async Task<List<DroneDataPoint>> GetAllDronesDataInTimeRangeAsync(
            DateTime startTime,
            DateTime endTime)
        {
            const string sql = @"
            SELECT 
                d.DroneName AS DroneId,
                dsh.Timestamp,
                dsh.CpuUsage,
                dsh.Latitude,
                dsh.Longitude,
                dsh.BandwidthAvailable,
                dsh.MemoryUsage,
                dsh.BatteryLevel,
                dsh.NetworkStrength
            FROM DroneStatusHistory dsh
            INNER JOIN Drones d ON dsh.DroneId = d.Id
            WHERE dsh.Timestamp BETWEEN @StartTime AND @EndTime
            ORDER BY d.DroneName, dsh.Timestamp";

            var results = new List<DroneDataPoint>();

            try
            {
                using (var command = new SqlCommand(sql, _connection))
                {
                    command.Parameters.AddWithValue("@StartTime", startTime);
                    command.Parameters.AddWithValue("@EndTime", endTime);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            try
                            {
                                var dataPoint = new DroneDataPoint
                                {
                                    DroneId = reader.GetGuid(0),
                                    Timestamp = (DateTime)reader["Timestamp"],
                                    cpuUsage = Convert.ToDouble(reader["CpuUsage"]),
                                    Latitude = reader.GetDouble(reader.GetOrdinal("Latitude")),
                                    Longitude = reader.GetDouble(reader.GetOrdinal("Longitude"))
                                };

                                // 处理可选字段
                                if (reader["BandwidthAvailable"] != DBNull.Value)
                                    dataPoint.bandwidthUsage = Convert.ToDouble(reader["BandwidthAvailable"]);

                                if (reader["MemoryUsage"] != DBNull.Value)
                                    dataPoint.memoryUsage = Convert.ToDouble(reader["MemoryUsage"]);
                                results.Add(dataPoint);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error parsing drone data row");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error retrieving drone data between {StartTime} and {EndTime}",
                    startTime, endTime);
            }

            return results;
        }


    }
}