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
        private readonly string _connectionString;
        private readonly SqlConnection _connection;
        private readonly ILogger<SqlserverService> _logger;
        
        public SqlserverService(ILogger<SqlserverService> logger)
        {
            _connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__app-db") ?? throw new InvalidOperationException("Connection string not found");
            _connection = new SqlConnection(_connectionString);
            _logger = logger;
            _logger.LogInformation("SqlserverService initialized with connection string");
        }
        
        /// <summary>
        /// 创建新的数据库连接
        /// </summary>
        private async Task<SqlConnection> CreateConnectionAsync()
        {
            var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }
        
        public void run()
        {
            _connection.Open();
            _logger.LogInformation("Database connection opened successfully");
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
                    await AddOrUpdateDroneAsync(drone, _connection, transaction);

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        // 无人机存在检查
        public async Task<bool> DroneExistsAsync(Guid id, SqlConnection? connection = null)
        {
            var targetConnection = connection ?? await CreateConnectionAsync();
            var shouldDisposeConnection = connection == null;
            
            try
            {
                var cmd = new SqlCommand("SELECT 1 FROM Drones WHERE Id=@id", targetConnection);
                cmd.Parameters.AddWithValue("@id", id);
                return (await cmd.ExecuteScalarAsync()) != null;
            }
            finally
            {
                if (shouldDisposeConnection)
                    targetConnection?.Dispose();
            }
        }
        // 异步添加/更新无人机
        public async Task AddOrUpdateDroneAsync(Drone drone, SqlConnection? connection = null, SqlTransaction? transaction = null)
        {
            var targetConnection = connection ?? await CreateConnectionAsync();
            var shouldDisposeConnection = connection == null;
            
            try
            {
                var exists = await DroneExistsAsync(drone.Id, targetConnection);
                var sql = exists
                    ? @"UPDATE Drones SET 
                        DroneId=@name, 
                        ModelType=@modelType, 
                        LastHeartbeat=@now 
                        WHERE Id=@id"
                    : @"INSERT INTO Drones (Id, DroneId, ModelType, RegistrationDate, LastHeartbeat) 
                       VALUES (@id, @name, @modelType, @registrationDate, @now)";

                using var cmd = new SqlCommand(sql, targetConnection, transaction);
                cmd.Parameters.AddRange(new[]
                {
                    new SqlParameter("@id", drone.Id),
                    new SqlParameter("@name", drone.Name),
                    new SqlParameter("@modelType", drone.ModelStatus.ToString()),
                    new SqlParameter("@registrationDate", exists ? (object)DBNull.Value : DateTime.UtcNow),
                    new SqlParameter("@now", DateTime.UtcNow)
                });

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                if (shouldDisposeConnection)
                    targetConnection?.Dispose();
            }
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
                    cpuUsage = Convert.ToDecimal(reader["CpuUsage"]),
                    Latitude = Convert.ToDecimal(reader["Latitude"]),
                    Longitude = Convert.ToDecimal(reader["Longitude"])
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
            new SqlParameter("@RegistrationDate", DateTime.UtcNow),
            new SqlParameter("@LastHeartbeat", DateTime.UtcNow)
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

        /// <summary>
        /// 获取所有无人机基本信息
        /// </summary>
        /// <returns>无人机基本信息列表</returns>
        public async Task<List<Drone>> GetAllDronesAsync()
        {
            var drones = new List<Drone>();
            var sql = "SELECT Id, DroneId, ModelType, RegistrationDate, LastHeartbeat FROM Drones";

            try
            {
                using var cmd = new SqlCommand(sql, _connection);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var drone = new Drone
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        Name = reader.GetString(reader.GetOrdinal("DroneId")),
                        ModelStatus = Enum.Parse<ModelStatus>(reader.GetString(reader.GetOrdinal("ModelType")))
                    };
                    drones.Add(drone);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all drones");
            }

            return drones;
        }

        /// <summary>
        /// 获取指定无人机的基本信息
        /// </summary>
        /// <param name="droneId">无人机ID</param>
        /// <returns>无人机基本信息</returns>
        public async Task<Drone?> GetDroneAsync(Guid droneId)
        {
            var sql = "SELECT Id, DroneId, ModelType, RegistrationDate, LastHeartbeat FROM Drones WHERE Id = @Id";

            try
            {
                using var cmd = new SqlCommand(sql, _connection);
                cmd.Parameters.AddWithValue("@Id", droneId);
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new Drone
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        Name = reader.GetString(reader.GetOrdinal("DroneId")),
                        ModelStatus = Enum.Parse<ModelStatus>(reader.GetString(reader.GetOrdinal("ModelType")))
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting drone {DroneId}", droneId);
            }

            return null;
        }

        /// <summary>
        /// 完整同步无人机数据（基本信息 + 状态历史）
        /// 使用独立连接避免并发冲突
        /// </summary>
        /// <param name="drone">无人机对象</param>
        public async Task FullSyncDroneAsync(Drone drone)
        {
            // 为每个调用创建独立的连接，避免并发冲突
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            
            try
            {
                // 1. 检查无人机是否存在
                var existsCmd = new SqlCommand("SELECT 1 FROM Drones WHERE Id=@id", connection, transaction);
                existsCmd.Parameters.AddWithValue("@id", drone.Id);
                var exists = (await existsCmd.ExecuteScalarAsync()) != null;

                // 2. 更新基本信息
                var sql = exists
                    ? @"UPDATE Drones SET 
                        DroneId=@name, 
                        ModelType=@modelType, 
                        LastHeartbeat=@now 
                        WHERE Id=@id"
                    : @"INSERT INTO Drones (Id, DroneId, ModelType, RegistrationDate, LastHeartbeat) 
                       VALUES (@id, @name, @modelType, @registrationDate, @now)";

                using var cmd = new SqlCommand(sql, connection, transaction);
                cmd.Parameters.AddRange(new[]
                {
                    new SqlParameter("@id", drone.Id),
                    new SqlParameter("@name", drone.Name),
                    new SqlParameter("@modelType", drone.ModelStatus.ToString()),
                    new SqlParameter("@registrationDate", exists ? (object)DBNull.Value : DateTime.UtcNow),
                    new SqlParameter("@now", DateTime.UtcNow)
                });
                await cmd.ExecuteNonQueryAsync();

                // 3. 记录状态历史（如果不是离线状态）
                if (drone.Status != DroneStatus.Offline)
                {
                    var statusSql = @"
                    INSERT INTO DroneStatusHistory (DroneId, Status, Timestamp, CpuUsage, BandwidthAvailable, MemoryUsage, Latitude, Longitude)
                    VALUES (@DroneId, @Status, @Timestamp, @CpuUsage, @BandwidthAvailable, @MemoryUsage, @Latitude, @Longitude)";

                    using var statusCmd = new SqlCommand(statusSql, connection, transaction);
                    statusCmd.Parameters.AddRange(new[]
                    {
                        new SqlParameter("@DroneId", drone.Id),
                        new SqlParameter("@Status", (int)drone.Status),
                        new SqlParameter("@Timestamp", DateTime.UtcNow),
                        new SqlParameter("@CpuUsage", (decimal)drone.cpu_used_rate),
                        new SqlParameter("@BandwidthAvailable", (decimal)drone.left_bandwidth),
                        new SqlParameter("@MemoryUsage", (decimal)drone.memory),
                        new SqlParameter("@Latitude", drone.CurrentPosition?.Latitude_x ?? (object)DBNull.Value),
                        new SqlParameter("@Longitude", drone.CurrentPosition?.Longitude_y ?? (object)DBNull.Value)
                    });
                    await statusCmd.ExecuteNonQueryAsync();
                }

                // 4. 同步子任务关联
                if (drone.AssignedSubTasks.Any())
                {
                    // 停用当前无人机的所有任务分配
                    var deactivateSql = "UPDATE DroneSubTasks SET IsActive = 0 WHERE DroneId = @DroneId";
                    using var deactivateCmd = new SqlCommand(deactivateSql, connection, transaction);
                    deactivateCmd.Parameters.AddWithValue("@DroneId", drone.Id);
                    await deactivateCmd.ExecuteNonQueryAsync();

                    // 添加新的任务分配
                    foreach (var subTask in drone.AssignedSubTasks)
                    {
                        var insertSql = @"
                        INSERT INTO DroneSubTasks (DroneId, SubTaskId, AssignmentTime, IsActive)
                        VALUES (@DroneId, @SubTaskId, @AssignmentTime, 1)";

                        using var insertCmd = new SqlCommand(insertSql, connection, transaction);
                        insertCmd.Parameters.AddRange(new[]
                        {
                            new SqlParameter("@DroneId", drone.Id),
                            new SqlParameter("@SubTaskId", subTask.Id),
                            new SqlParameter("@AssignmentTime", DateTime.UtcNow)
                        });
                        await insertCmd.ExecuteNonQueryAsync();
                    }
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Failed to sync drone {DroneId} to database", drone.Id);
                throw;
            }
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

        /// <summary>
        /// 异步添加子任务
        /// </summary>
        public async Task<Guid> AddSubTaskAsync(SubTask subTask)
        {
            subTask.Id = Guid.NewGuid();
            subTask.CreationTime = DateTime.UtcNow;

            var sql = @"
            INSERT INTO SubTasks (Id, Description, Status, CreationTime, CompletedTime, ParentTaskId, ReassignmentCount)
            VALUES (@Id, @Description, @Status, @CreationTime, @CompletedTime, @ParentTaskId, @ReassignmentCount)";

            using var cmd = new SqlCommand(sql, _connection);
            cmd.Parameters.AddRange(new[]
            {
                new SqlParameter("@Id", subTask.Id),
                new SqlParameter("@Description", subTask.Description),
                new SqlParameter("@Status", (int)subTask.Status),
                new SqlParameter("@CreationTime", subTask.CreationTime),
                new SqlParameter("@CompletedTime", subTask.CompletedTime ?? (object)DBNull.Value),
                new SqlParameter("@ParentTaskId", subTask.ParentTask),
                new SqlParameter("@ReassignmentCount", subTask.ReassignmentCount)
            });

            await cmd.ExecuteNonQueryAsync();
            return subTask.Id;
        }

        /// <summary>
        /// 更新主任务
        /// </summary>
        public async Task UpdateMainTaskAsync(MainTask task)
        {
            var sql = @"
            UPDATE MainTasks 
            SET Description = @Description, 
                Status = @Status, 
                CompletedTime = @CompletedTime 
            WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, _connection);
            cmd.Parameters.AddRange(new[]
            {
                new SqlParameter("@Id", task.Id),
                new SqlParameter("@Description", task.Description),
                new SqlParameter("@Status", (int)task.Status),
                new SqlParameter("@CompletedTime", task.CompletedTime ?? (object)DBNull.Value)
            });

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 删除主任务（级联删除子任务）
        /// </summary>
        public async Task DeleteMainTaskAsync(Guid taskId)
        {
            var sql = "DELETE FROM MainTasks WHERE Id = @Id";
            using var cmd = new SqlCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@Id", taskId);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 更新子任务
        /// </summary>
        public async Task UpdateSubTaskAsync(SubTask subTask)
        {
            var sql = @"
            UPDATE SubTasks 
            SET Description = @Description,
                Status = @Status,
                AssignedTime = @AssignedTime,
                CompletedTime = @CompletedTime,
                ReassignmentCount = @ReassignmentCount
            WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, _connection);
            cmd.Parameters.AddRange(new[]
            {
                new SqlParameter("@Id", subTask.Id),
                new SqlParameter("@Description", subTask.Description),
                new SqlParameter("@Status", (int)subTask.Status),
                new SqlParameter("@AssignedTime", subTask.AssignedTime ?? (object)DBNull.Value),
                new SqlParameter("@CompletedTime", subTask.CompletedTime ?? (object)DBNull.Value),
                new SqlParameter("@ReassignmentCount", subTask.ReassignmentCount)
            });

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 获取所有主任务
        /// </summary>
        public async Task<List<MainTask>> GetAllMainTasksAsync()
        {
            var tasks = new List<MainTask>();
            var sql = "SELECT Id, Description, Status, CreationTime, CompletedTime, CreatedBy FROM MainTasks ORDER BY CreationTime DESC";

            try
            {
                using var cmd = new SqlCommand(sql, _connection);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var task = new MainTask
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        Description = reader.GetString(reader.GetOrdinal("Description")),
                        Status = (TaskStatus)reader.GetByte(reader.GetOrdinal("Status")),
                        CreationTime = reader.GetDateTime(reader.GetOrdinal("CreationTime")),
                        CompletedTime = reader.IsDBNull(reader.GetOrdinal("CompletedTime")) ? null : reader.GetDateTime(reader.GetOrdinal("CompletedTime"))
                    };
                    tasks.Add(task);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all main tasks");
            }

            return tasks;
        }

        /// <summary>
        /// 获取主任务的所有子任务
        /// </summary>
        public async Task<List<SubTask>> GetSubTasksByParentAsync(Guid parentTaskId)
        {
            var subTasks = new List<SubTask>();
            var sql = "SELECT Id, Description, Status, CreationTime, AssignedTime, CompletedTime, ParentTaskId, ReassignmentCount FROM SubTasks WHERE ParentTaskId = @ParentTaskId ORDER BY CreationTime";

            try
            {
                using var cmd = new SqlCommand(sql, _connection);
                cmd.Parameters.AddWithValue("@ParentTaskId", parentTaskId);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var subTask = new SubTask
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        Description = reader.GetString(reader.GetOrdinal("Description")),
                        Status = (TaskStatus)reader.GetByte(reader.GetOrdinal("Status")),
                        CreationTime = reader.GetDateTime(reader.GetOrdinal("CreationTime")),
                        AssignedTime = reader.IsDBNull(reader.GetOrdinal("AssignedTime")) ? null : reader.GetDateTime(reader.GetOrdinal("AssignedTime")),
                        CompletedTime = reader.IsDBNull(reader.GetOrdinal("CompletedTime")) ? null : reader.GetDateTime(reader.GetOrdinal("CompletedTime")),
                        ParentTask = reader.GetGuid(reader.GetOrdinal("ParentTaskId")),
                        ReassignmentCount = reader.GetInt32(reader.GetOrdinal("ReassignmentCount"))
                    };
                    subTasks.Add(subTask);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting subtasks for parent {ParentTaskId}", parentTaskId);
            }

            return subTasks;
        }

        /// <summary>
        /// 完整同步主任务及其子任务
        /// </summary>
        public async Task FullSyncMainTaskAsync(MainTask mainTask)
        {
            using var transaction = _connection.BeginTransaction();
            try
            {
                // 1. 更新或插入主任务
                var existsTask = await GetMainTaskExistsAsync(mainTask.Id, transaction);
                if (existsTask)
                {
                    var updateSql = @"
                    UPDATE MainTasks 
                    SET Description = @Description, Status = @Status, CompletedTime = @CompletedTime 
                    WHERE Id = @Id";

                    using var updateCmd = new SqlCommand(updateSql, _connection, transaction);
                    updateCmd.Parameters.AddRange(new[]
                    {
                        new SqlParameter("@Id", mainTask.Id),
                        new SqlParameter("@Description", mainTask.Description),
                        new SqlParameter("@Status", (int)mainTask.Status),
                        new SqlParameter("@CompletedTime", mainTask.CompletedTime ?? (object)DBNull.Value)
                    });
                    await updateCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    var insertSql = @"
                    INSERT INTO MainTasks (Id, Description, Status, CreationTime, CompletedTime, CreatedBy)
                    VALUES (@Id, @Description, @Status, @CreationTime, @CompletedTime, @CreatedBy)";

                    using var insertCmd = new SqlCommand(insertSql, _connection, transaction);
                    insertCmd.Parameters.AddRange(new[]
                    {
                        new SqlParameter("@Id", mainTask.Id),
                        new SqlParameter("@Description", mainTask.Description),
                        new SqlParameter("@Status", (int)mainTask.Status),
                        new SqlParameter("@CreationTime", mainTask.CreationTime),
                        new SqlParameter("@CompletedTime", mainTask.CompletedTime ?? (object)DBNull.Value),
                        new SqlParameter("@CreatedBy", "System")
                    });
                    await insertCmd.ExecuteNonQueryAsync();
                }

                // 2. 同步子任务
                foreach (var subTask in mainTask.SubTasks)
                {
                    var existsSubTask = await GetSubTaskExistsAsync(subTask.Id, transaction);
                    if (existsSubTask)
                    {
                        var updateSubSql = @"
                        UPDATE SubTasks 
                        SET Description = @Description, Status = @Status, AssignedTime = @AssignedTime, 
                            CompletedTime = @CompletedTime, ReassignmentCount = @ReassignmentCount
                        WHERE Id = @Id";

                        using var updateSubCmd = new SqlCommand(updateSubSql, _connection, transaction);
                        updateSubCmd.Parameters.AddRange(new[]
                        {
                            new SqlParameter("@Id", subTask.Id),
                            new SqlParameter("@Description", subTask.Description),
                            new SqlParameter("@Status", (int)subTask.Status),
                            new SqlParameter("@AssignedTime", subTask.AssignedTime ?? (object)DBNull.Value),
                            new SqlParameter("@CompletedTime", subTask.CompletedTime ?? (object)DBNull.Value),
                            new SqlParameter("@ReassignmentCount", subTask.ReassignmentCount)
                        });
                        await updateSubCmd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        var insertSubSql = @"
                        INSERT INTO SubTasks (Id, Description, Status, CreationTime, AssignedTime, CompletedTime, ParentTaskId, ReassignmentCount)
                        VALUES (@Id, @Description, @Status, @CreationTime, @AssignedTime, @CompletedTime, @ParentTaskId, @ReassignmentCount)";

                        using var insertSubCmd = new SqlCommand(insertSubSql, _connection, transaction);
                        insertSubCmd.Parameters.AddRange(new[]
                        {
                            new SqlParameter("@Id", subTask.Id),
                            new SqlParameter("@Description", subTask.Description),
                            new SqlParameter("@Status", (int)subTask.Status),
                            new SqlParameter("@CreationTime", subTask.CreationTime),
                            new SqlParameter("@AssignedTime", subTask.AssignedTime ?? (object)DBNull.Value),
                            new SqlParameter("@CompletedTime", subTask.CompletedTime ?? (object)DBNull.Value),
                            new SqlParameter("@ParentTaskId", mainTask.Id),
                            new SqlParameter("@ReassignmentCount", subTask.ReassignmentCount)
                        });
                        await insertSubCmd.ExecuteNonQueryAsync();
                    }
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// 检查主任务是否存在
        /// </summary>
        private async Task<bool> GetMainTaskExistsAsync(Guid taskId, SqlTransaction transaction)
        {
            var sql = "SELECT 1 FROM MainTasks WHERE Id = @Id";
            using var cmd = new SqlCommand(sql, _connection, transaction);
            cmd.Parameters.AddWithValue("@Id", taskId);
            return (await cmd.ExecuteScalarAsync()) != null;
        }

        /// <summary>
        /// 检查子任务是否存在
        /// </summary>
        private async Task<bool> GetSubTaskExistsAsync(Guid subTaskId, SqlTransaction transaction)
        {
            var sql = "SELECT 1 FROM SubTasks WHERE Id = @Id";
            using var cmd = new SqlCommand(sql, _connection, transaction);
            cmd.Parameters.AddWithValue("@Id", subTaskId);
            return (await cmd.ExecuteScalarAsync()) != null;
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

        /// <summary>
        /// 从Drone对象记录状态历史到DroneStatusHistory表
        /// </summary>
        /// <param name="drone">无人机对象</param>
        public async Task RecordDroneStatusFromDroneAsync(Drone drone)
        {
            var sql = @"
            INSERT INTO DroneStatusHistory (DroneId, Status, Timestamp, CpuUsage, BandwidthAvailable, MemoryUsage, Latitude, Longitude)
            VALUES (@DroneId, @Status, @Timestamp, @CpuUsage, @BandwidthAvailable, @MemoryUsage, @Latitude, @Longitude)";

            try
            {
                using var cmd = new SqlCommand(sql, _connection);
                cmd.Parameters.AddRange(new[]
                {
                    new SqlParameter("@DroneId", drone.Id),
                    new SqlParameter("@Status", (int)drone.Status),
                    new SqlParameter("@Timestamp", DateTime.UtcNow),
                    new SqlParameter("@CpuUsage", (decimal)drone.cpu_used_rate),
                    new SqlParameter("@BandwidthAvailable", (decimal)drone.left_bandwidth),
                    new SqlParameter("@MemoryUsage", (decimal)drone.memory),
                    new SqlParameter("@Latitude", drone.CurrentPosition?.Latitude_x ?? (object)DBNull.Value),
                    new SqlParameter("@Longitude", drone.CurrentPosition?.Longitude_y ?? (object)DBNull.Value)
                });

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording drone status for drone {DroneId}", drone.Id);
                throw;
            }
        }

        /// <summary>
        /// 批量记录无人机状态历史
        /// </summary>
        /// <param name="drones">无人机列表</param>
        public async Task BulkRecordDroneStatusAsync(IEnumerable<Drone> drones)
        {
            using var transaction = _connection.BeginTransaction();
            try
            {
                foreach (var drone in drones.Where(d => d.Status != DroneStatus.Offline))
                {
                    var sql = @"
                    INSERT INTO DroneStatusHistory (DroneId, Status, Timestamp, CpuUsage, BandwidthAvailable, MemoryUsage, Latitude, Longitude)
                    VALUES (@DroneId, @Status, @Timestamp, @CpuUsage, @BandwidthAvailable, @MemoryUsage, @Latitude, @Longitude)";

                    using var cmd = new SqlCommand(sql, _connection, transaction);
                    cmd.Parameters.AddRange(new[]
                    {
                        new SqlParameter("@DroneId", drone.Id),
                        new SqlParameter("@Status", (int)drone.Status),
                        new SqlParameter("@Timestamp", DateTime.UtcNow),
                        new SqlParameter("@CpuUsage", (decimal)drone.cpu_used_rate),
                        new SqlParameter("@BandwidthAvailable", (decimal)drone.left_bandwidth),
                        new SqlParameter("@MemoryUsage", (decimal)drone.memory),
                        new SqlParameter("@Latitude", drone.CurrentPosition?.Latitude_x ?? (object)DBNull.Value),
                        new SqlParameter("@Longitude", drone.CurrentPosition?.Longitude_y ?? (object)DBNull.Value)
                    });

                    await cmd.ExecuteNonQueryAsync();
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
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

        /// <summary>
        /// 同步无人机的子任务关联到数据库
        /// </summary>
        /// <param name="drone">无人机对象</param>
        public async Task SyncDroneSubTasksAsync(Drone drone)
        {
            using var transaction = _connection.BeginTransaction();
            try
            {
                // 停用当前无人机的所有任务分配
                var deactivateSql = "UPDATE DroneSubTasks SET IsActive = 0 WHERE DroneId = @DroneId";
                using var deactivateCmd = new SqlCommand(deactivateSql, _connection, transaction);
                deactivateCmd.Parameters.AddWithValue("@DroneId", drone.Id);
                await deactivateCmd.ExecuteNonQueryAsync();

                // 添加新的任务分配
                foreach (var subTask in drone.AssignedSubTasks)
                {
                    var insertSql = @"
                    INSERT INTO DroneSubTasks (DroneId, SubTaskId, AssignmentTime, IsActive)
                    VALUES (@DroneId, @SubTaskId, @AssignmentTime, 1)";

                    using var insertCmd = new SqlCommand(insertSql, _connection, transaction);
                    insertCmd.Parameters.AddRange(new[]
                    {
                        new SqlParameter("@DroneId", drone.Id),
                        new SqlParameter("@SubTaskId", subTask.Id),
                        new SqlParameter("@AssignmentTime", DateTime.UtcNow)
                    });
                    await insertCmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// 批量同步所有无人机的子任务关联
        /// </summary>
        /// <param name="drones">无人机列表</param>
        public async Task BulkSyncDroneSubTasksAsync(IEnumerable<Drone> drones)
        {
            foreach (var drone in drones)
            {
                await SyncDroneSubTasksAsync(drone);
            }
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
                        cpuUsage = Convert.ToDecimal(reader["CpuUsage"]),
                        Latitude = Convert.ToDecimal(reader["Latitude"]),
                        Longitude = Convert.ToDecimal(reader["Longitude"])
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
                dsh.DroneId,
                dsh.Timestamp,
                dsh.CpuUsage,
                dsh.Latitude,
                dsh.Longitude,
                dsh.BandwidthAvailable,
                dsh.MemoryUsage,
                dsh.BatteryLevel,
                dsh.NetworkStrength
            FROM DroneStatusHistory dsh
            WHERE dsh.Timestamp BETWEEN @StartTime AND @EndTime
            ORDER BY dsh.DroneId, dsh.Timestamp";

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
                                    DroneId = reader.GetGuid(reader.GetOrdinal("DroneId")),
                                    Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp")),
                                    cpuUsage=reader.IsDBNull(reader.GetOrdinal("CpuUsage")) ? 0 : reader.GetOrdinal("CpuUsage"),
                                    Latitude = reader.IsDBNull(reader.GetOrdinal("Latitude")) ? 0 : Convert.ToDecimal(reader["Latitude"]),
                                    Longitude = reader.IsDBNull(reader.GetOrdinal("Longitude")) ? 0 : Convert.ToDecimal(reader["Longitude"])
                                    };
                                

                                // 处理可选字段
                                if (reader["BandwidthAvailable"] != DBNull.Value)
                                    dataPoint.bandwidthUsage = Convert.ToDecimal(reader["BandwidthAvailable"]);

                                if (reader["MemoryUsage"] != DBNull.Value)
                                    dataPoint.memoryUsage = Convert.ToDecimal(reader["MemoryUsage"]);
                                
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