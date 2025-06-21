using ClassLibrary_Core.Common;
using ClassLibrary_Core.Data;
using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Mission;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace WebApplication_Drone.Services
{
    public class SqlserverService
    {
        private readonly string _connectionString;
        private readonly ILogger<SqlserverService> _logger;
        
        public SqlserverService(ILogger<SqlserverService> logger)
        {
            _connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__app-db") ?? throw new InvalidOperationException("Connection string not found");
            _logger = logger;
            _logger.LogInformation("SqlserverService initialized with connection string");
            _logger.LogInformation("Connection string: {ConnectionString}", _connectionString);
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

        /// <summary>
        /// 限制数值范围以适应数据库字段精度
        /// </summary>
        private static decimal ClampDecimal(double value, decimal min, decimal max)
        {
            var decimalValue = (decimal)value;
            return Math.Min(max, Math.Max(min, decimalValue));
        }

        /// <summary>
        /// 限制GPS坐标值范围
        /// </summary>
        private static object ClampGpsCoordinate(double? value)
        {
            if (value == null) return DBNull.Value;
            return Math.Min(999.9999999m, Math.Max(-999.9999999m, (decimal)value.Value));
        }
        
        public void run()
        {
            // 连接池模式下不需要显式打开连接
            _logger.LogInformation("SqlServerService ready - using connection pool mode");
        }
        
        // 通用执行方法 - 使用连接池
        private async Task<int> ExecuteNonQueryAsync(string sql, params SqlParameter[] parameters)
        {
            try
            {
                using var connection = await CreateConnectionAsync();
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddRange(parameters);
                return await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing query: {sql}");
                throw;
            }
        }

        // 查询方法 - 使用连接池
        private async Task<SqlDataReader> ExecuteReaderAsync(string sql, params SqlParameter[] parameters)
        {
            try
            {
                var connection = await CreateConnectionAsync();
                var command = new SqlCommand(sql, connection);
                command.Parameters.AddRange(parameters);
                var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.CloseConnection);
                return reader;
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
            using var connection = await CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                foreach (var drone in drones)
                    await AddOrUpdateDroneAsync(drone, connection, transaction);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
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
                // 使用MERGE语句实现原子性的插入或更新操作，避免竞争条件
                var mergeSql = @"
                    MERGE Drones AS target
                    USING (SELECT @id AS Id, @name AS Name, @modelStatus AS ModelStatus, @modelType AS ModelType, @now AS LastHeartbeat) AS source
                    ON target.Id = source.Id
                    WHEN MATCHED THEN
                        UPDATE SET 
                            Name = source.Name,
                            ModelStatus = source.ModelStatus,
                            ModelType = source.ModelType,
                            LastHeartbeat = source.LastHeartbeat
                    WHEN NOT MATCHED THEN
                        INSERT (Id, Name, ModelStatus, ModelType, RegistrationDate, LastHeartbeat)
                        VALUES (source.Id, source.Name, source.ModelStatus, source.ModelType, @registrationDate, source.LastHeartbeat);";

                using var cmd = new SqlCommand(mergeSql, targetConnection, transaction);
                cmd.Parameters.AddRange(new[]
                {
                    new SqlParameter("@id", drone.Id),
                    new SqlParameter("@name", drone.Name),
                    new SqlParameter("@modelStatus", (int)drone.ModelStatus),
                    new SqlParameter("@modelType", drone.ModelType),
                    new SqlParameter("@registrationDate", DateTime.Now),
                    new SqlParameter("@now", DateTime.Now)
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

            using var connection = await CreateConnectionAsync();
            using var cmd = new SqlCommand(sql, connection);
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
        public async Task<Guid> AddDroneAsync(Drone drone)
        {
            drone.Id = Guid.NewGuid();
            var sql = @"
            INSERT INTO Drones (Id, Name, ModelStatus, ModelType, RegistrationDate, LastHeartbeat)
            VALUES (@Id, @Name, @ModelStatus, @ModelType, @RegistrationDate, @LastHeartbeat)";

            var parameters = new[]
            {
            new SqlParameter("@Id", drone.Id),
            new SqlParameter("@Name", drone.Name),
            new SqlParameter("@ModelStatus", (int)drone.ModelStatus),
            new SqlParameter("@ModelType", drone.ModelType),
            new SqlParameter("@RegistrationDate", DateTime.Now),
            new SqlParameter("@LastHeartbeat", DateTime.Now)
        };

            await ExecuteNonQueryAsync(sql, parameters);
            return drone.Id;
        }

        // 更新无人机最后心跳
        public async Task UpdateDroneHeartbeatAsync(Guid droneId)
        {
            var sql = "UPDATE Drones SET LastHeartbeat = GETUTCDATE() WHERE Id = @Id";
            await ExecuteNonQueryAsync(sql, new SqlParameter("@Id", droneId));
        }

        /// <summary>
        /// 获取所有无人机基本信息
        /// </summary>
        /// <returns>无人机基本信息列表</returns>
        public async Task<List<Drone>> GetAllDronesAsync()
        {
            var drones = new List<Drone>();
            var sql = "SELECT Id, Name, ModelStatus, ModelType, RegistrationDate, LastHeartbeat FROM Drones";

            try
            {
                using var connection = await CreateConnectionAsync();
                using var cmd = new SqlCommand(sql, connection);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var drone = new Drone
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        ModelStatus = (ModelStatus)reader.GetByte(reader.GetOrdinal("ModelStatus")),
                        ModelType = reader.GetString(reader.GetOrdinal("ModelType"))
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
            var sql = "SELECT Id, Name, ModelStatus, ModelType, RegistrationDate, LastHeartbeat FROM Drones WHERE Id = @Id";

            try
            {
                using var connection = await CreateConnectionAsync();
                using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@Id", droneId);
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new Drone
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        ModelStatus = (ModelStatus)reader.GetByte(reader.GetOrdinal("ModelStatus")),
                        ModelType = reader.GetString(reader.GetOrdinal("ModelType"))
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
        /// 通过名称获取无人机的基本信息
        /// </summary>
        /// <param name="droneName">无人机名称</param>
        /// <returns>无人机基本信息</returns>
        public async Task<Drone?> GetDroneByNameAsync(string droneName)
        {
            var sql = "SELECT Id, Name, ModelStatus, ModelType, RegistrationDate, LastHeartbeat FROM Drones WHERE Name = @Name";

            try
            {
                using var connection = await CreateConnectionAsync();
                using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@Name", droneName);
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new Drone
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        ModelStatus = (ModelStatus)reader.GetByte(reader.GetOrdinal("ModelStatus")),
                        ModelType = reader.GetString(reader.GetOrdinal("ModelType"))
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting drone by name {DroneName}", droneName);
            }

            return null;
        }

        /// <summary>
        /// 完整同步无人机数据（基本信息 + 状态历史）
        /// 使用独立连接避免并发冲突，包含死锁重试机制
        /// </summary>
        /// <param name="drone">无人机对象</param>
        public async Task FullSyncDroneAsync(Drone drone)
        {
            _logger.LogDebug("Starting full sync for drone {DroneId} ({DroneName})", drone.Id, drone.Name);
            
            const int maxRetries = 3;
            const int baseDelayMs = 100;
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await ExecuteFullSyncWithTransaction(drone);
                    _logger.LogDebug("Successfully completed full sync for drone {DroneId} on attempt {Attempt}", drone.Id, attempt);
                    return; // 成功，退出重试循环
                }
                catch (SqlException sqlEx) when (sqlEx.Number == 1205) // 死锁错误
                {
                    if (attempt == maxRetries)
                    {
                        _logger.LogError(sqlEx, "Failed to sync drone {DroneId} after {MaxRetries} attempts due to deadlock", drone.Id, maxRetries);
                        throw;
                    }
                    
                    var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1) + Random.Shared.Next(0, 50);
                    _logger.LogWarning("Deadlock detected for drone {DroneId} on attempt {Attempt}, retrying after {Delay}ms", 
                        drone.Id, attempt, delay);
                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during full sync for drone {DroneId} on attempt {Attempt}: {Message}", 
                        drone.Id, attempt, ex.Message);
                    throw; // 非死锁错误，直接抛出
                }
            }
        }

        /// <summary>
        /// 执行完整同步的核心事务逻辑
        /// </summary>
        private async Task ExecuteFullSyncWithTransaction(Drone drone)
        {
            using var connection = await CreateConnectionAsync();
            // 使用ReadCommitted隔离级别减少死锁概率，添加超时设置
            using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
            
            try
            {
                // 1. 同步基本信息 - 使用更高效的MERGE语句避免多次查询
                var mergeSql = @"
                    MERGE Drones WITH (HOLDLOCK) AS target
                    USING (SELECT @id AS Id, @name AS Name, @modelStatus AS ModelStatus, @modelType AS ModelType, @now AS LastHeartbeat) AS source
                    ON target.Id = source.Id
                    WHEN MATCHED THEN
                        UPDATE SET 
                            Name = source.Name,
                            ModelStatus = source.ModelStatus,
                            ModelType = source.ModelType,
                            LastHeartbeat = source.LastHeartbeat
                    WHEN NOT MATCHED THEN
                        INSERT (Id, Name, ModelStatus, ModelType, RegistrationDate, LastHeartbeat)
                        VALUES (source.Id, source.Name, source.ModelStatus, source.ModelType, @registrationDate, source.LastHeartbeat);";

                using var mergeCmd = new SqlCommand(mergeSql, connection, transaction);
                mergeCmd.CommandTimeout = 30; // 设置超时
                mergeCmd.Parameters.AddRange(new[]
                {
                    new SqlParameter("@id", drone.Id),
                    new SqlParameter("@name", drone.Name ?? ""),
                    new SqlParameter("@modelStatus", (int)drone.ModelStatus),
                    new SqlParameter("@modelType", drone.ModelType ?? ""),
                    new SqlParameter("@registrationDate", DateTime.Now),
                    new SqlParameter("@now", DateTime.Now)
                });
                await mergeCmd.ExecuteNonQueryAsync();
                _logger.LogDebug("Successfully merged drone {DroneId} basic info", drone.Id);

                // 2. 记录状态历史（分离事务，减少锁持有时间）
                if (drone.Status != DroneStatus.Offline)
                {
                    await RecordDroneStatusInTransaction(drone, connection, transaction);
                }

                // 3. 同步子任务关联（如果有）
                if (drone.AssignedSubTasks.Any())
                {
                    await SyncDroneSubTasksInTransaction(drone, connection, transaction);
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// 在事务中记录无人机状态历史
        /// </summary>
        private async Task RecordDroneStatusInTransaction(Drone drone, SqlConnection connection, SqlTransaction transaction)
        {
            var statusSql = @"
                INSERT INTO DroneStatusHistory (DroneId, Status, Timestamp, CpuUsage, BandwidthAvailable, MemoryUsage, Latitude, Longitude)
                VALUES (@DroneId, @Status, @Timestamp, @CpuUsage, @BandwidthAvailable, @MemoryUsage, @Latitude, @Longitude)";

            using var statusCmd = new SqlCommand(statusSql, connection, transaction);
            statusCmd.CommandTimeout = 15;
            statusCmd.Parameters.AddRange(new[]
            {
                new SqlParameter("@DroneId", drone.Id),
                new SqlParameter("@Status", (int)drone.Status),
                new SqlParameter("@Timestamp", DateTime.Now),
                new SqlParameter("@CpuUsage", ClampDecimal(drone.cpu_used_rate, 0m, 999.99m)),
                new SqlParameter("@BandwidthAvailable", ClampDecimal(drone.left_bandwidth, 0m, 9999.99m)),
                new SqlParameter("@MemoryUsage", ClampDecimal(drone.memory, 0m, 9999.99m)),
                new SqlParameter("@Latitude", ClampGpsCoordinate(drone.CurrentPosition?.Latitude_x)),
                new SqlParameter("@Longitude", ClampGpsCoordinate(drone.CurrentPosition?.Longitude_y))
            });
            await statusCmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 在事务中同步无人机子任务关联
        /// </summary>
        private async Task SyncDroneSubTasksInTransaction(Drone drone, SqlConnection connection, SqlTransaction transaction)
        {
            // 停用当前无人机的所有任务分配
            var deactivateSql = "UPDATE DroneSubTasks SET IsActive = 0 WHERE DroneId = @DroneId";
            using var deactivateCmd = new SqlCommand(deactivateSql, connection, transaction);
            deactivateCmd.CommandTimeout = 15;
            deactivateCmd.Parameters.AddWithValue("@DroneId", drone.Id);
            await deactivateCmd.ExecuteNonQueryAsync();

            // 批量添加新的任务分配
            var insertSql = @"
                INSERT INTO DroneSubTasks (DroneId, SubTaskId, AssignmentTime, IsActive)
                VALUES (@DroneId, @SubTaskId, @AssignmentTime, 1)";

            foreach (var subTask in drone.AssignedSubTasks)
            {
                using var insertCmd = new SqlCommand(insertSql, connection, transaction);
                insertCmd.CommandTimeout = 15;
                insertCmd.Parameters.AddRange(new[]
                {
                    new SqlParameter("@DroneId", drone.Id),
                    new SqlParameter("@SubTaskId", subTask.Id),
                    new SqlParameter("@AssignmentTime", DateTime.Now)
                });
                await insertCmd.ExecuteNonQueryAsync();
            }
        }

        // 主任务方法
        public async Task<Guid> AddMainTaskAsync(MainTask task, string createdBy)
        {
            task.Id = Guid.NewGuid();
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
                new SqlParameter("@CreatedBy", createdBy)
            };

            await ExecuteNonQueryAsync(sql, parameters);
            return task.Id;
        }

        public async Task<Guid> AddSubTaskAsync(SubTask subTask)
        {
            subTask.Id = Guid.NewGuid();
            var sql = @"
            INSERT INTO SubTasks (Id, Description, Status, CreationTime, AssignedTime, CompletedTime, ParentTask, ReassignmentCount)
            VALUES (@Id, @Description, @Status, @CreationTime, @AssignedTime, @CompletedTime, @ParentTask, @ReassignmentCount)";

            var parameters = new[]
            {
                new SqlParameter("@Id", subTask.Id),
                new SqlParameter("@Description", subTask.Description),
                new SqlParameter("@Status", (int)subTask.Status),
                new SqlParameter("@CreationTime", subTask.CreationTime),
                new SqlParameter("@AssignedTime", subTask.AssignedTime ?? (object)DBNull.Value),
                new SqlParameter("@CompletedTime", subTask.CompletedTime ?? (object)DBNull.Value),
                new SqlParameter("@ParentTask", subTask.ParentTask),
                new SqlParameter("@ReassignmentCount", subTask.ReassignmentCount)
            };

            await ExecuteNonQueryAsync(sql, parameters);
            return subTask.Id;
        }

        public async Task UpdateMainTaskAsync(MainTask task)
        {
            var sql = @"
            UPDATE MainTasks 
            SET Description = @Description, Status = @Status, CompletedTime = @CompletedTime
            WHERE Id = @Id";

            var parameters = new[]
            {
                new SqlParameter("@Id", task.Id),
                new SqlParameter("@Description", task.Description),
                new SqlParameter("@Status", (int)task.Status),
                new SqlParameter("@CompletedTime", task.CompletedTime ?? (object)DBNull.Value)
            };

            await ExecuteNonQueryAsync(sql, parameters);
        }

        public async Task DeleteMainTaskAsync(Guid taskId)
        {
            var sql = "DELETE FROM MainTasks WHERE Id = @Id";
            await ExecuteNonQueryAsync(sql, new SqlParameter("@Id", taskId));
        }

        public async Task UpdateSubTaskAsync(SubTask subTask)
        {
            var sql = @"
            UPDATE SubTasks 
            SET Description = @Description, Status = @Status, AssignedTime = @AssignedTime, 
                CompletedTime = @CompletedTime, ReassignmentCount = @ReassignmentCount
            WHERE Id = @Id";

            var parameters = new[]
            {
                new SqlParameter("@Id", subTask.Id),
                new SqlParameter("@Description", subTask.Description),
                new SqlParameter("@Status", (int)subTask.Status),
                new SqlParameter("@AssignedTime", subTask.AssignedTime ?? (object)DBNull.Value),
                new SqlParameter("@CompletedTime", subTask.CompletedTime ?? (object)DBNull.Value),
                new SqlParameter("@ReassignmentCount", subTask.ReassignmentCount)
            };

            await ExecuteNonQueryAsync(sql, parameters);
        }

        public async Task<List<MainTask>> GetAllMainTasksAsync()
        {
            var mainTasks = new List<MainTask>();
            var sql = "SELECT Id, Description, Status, CreationTime, CompletedTime FROM MainTasks ORDER BY CreationTime DESC";

            using var connection = await CreateConnectionAsync();
            using var cmd = new SqlCommand(sql, connection);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var mainTask = new MainTask
                {
                    Id = reader.GetGuid(reader.GetOrdinal("Id")),
                    Description = reader.GetString(reader.GetOrdinal("Description")),
                    Status = (System.Threading.Tasks.TaskStatus)reader.GetByte(reader.GetOrdinal("Status")),
                    CreationTime = reader.GetDateTime(reader.GetOrdinal("CreationTime")),
                    CompletedTime = reader.IsDBNull(reader.GetOrdinal("CompletedTime")) ? null : reader.GetDateTime(reader.GetOrdinal("CompletedTime"))
                };
                mainTasks.Add(mainTask);
            }

            return mainTasks;
        }

        /// <summary>
        /// 获取主任务总数
        /// </summary>
        /// <returns>主任务总数</returns>
        public async Task<int> GetMainTaskCountAsync()
        {
            try
            {
                var sql = "SELECT COUNT(*) FROM MainTasks";
                using var connection = await CreateConnectionAsync();
                using var cmd = new SqlCommand(sql, connection);
                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取主任务总数失败");
                return 0;
            }
        }

        /// <summary>
        /// 分页获取主任务数据
        /// </summary>
        /// <param name="page">页码（从0开始）</param>
        /// <param name="pageSize">每页数量</param>
        /// <returns>主任务列表</returns>
        public async Task<List<MainTask>> GetMainTasksByPageAsync(int page, int pageSize)
        {
            var mainTasks = new List<MainTask>();
            var sql = @"
                SELECT Id, Description, Status, CreationTime, CompletedTime 
                FROM MainTasks 
                ORDER BY CreationTime DESC
                OFFSET @Offset ROWS 
                FETCH NEXT @PageSize ROWS ONLY";

            try
            {
                using var connection = await CreateConnectionAsync();
                using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@Offset", page * pageSize);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);
                
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var mainTask = new MainTask
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        Description = reader.GetString(reader.GetOrdinal("Description")),
                        Status = (System.Threading.Tasks.TaskStatus)reader.GetByte(reader.GetOrdinal("Status")),
                        CreationTime = reader.GetDateTime(reader.GetOrdinal("CreationTime")),
                        CompletedTime = reader.IsDBNull(reader.GetOrdinal("CompletedTime")) ? null : reader.GetDateTime(reader.GetOrdinal("CompletedTime"))
                    };
                    mainTasks.Add(mainTask);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "分页获取主任务数据失败: Page={Page}, PageSize={PageSize}", page, pageSize);
            }

            return mainTasks;
        }

        public async Task<List<SubTask>> GetSubTasksByParentAsync(Guid parentTaskId)
        {
            var subTasks = new List<SubTask>();
            var sql = "SELECT Id, Description, Status, CreationTime, AssignedTime, CompletedTime, ParentTask, ReassignmentCount FROM SubTasks WHERE ParentTask = @ParentTask ORDER BY CreationTime";

            using var connection = await CreateConnectionAsync();
            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@ParentTask", parentTaskId);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var subTask = new SubTask
                {
                    Id = reader.GetGuid(reader.GetOrdinal("Id")),
                    Description = reader.GetString(reader.GetOrdinal("Description")),
                    Status = (System.Threading.Tasks.TaskStatus)reader.GetByte(reader.GetOrdinal("Status")),
                    CreationTime = reader.GetDateTime(reader.GetOrdinal("CreationTime")),
                    AssignedTime = reader.IsDBNull(reader.GetOrdinal("AssignedTime")) ? null : reader.GetDateTime(reader.GetOrdinal("AssignedTime")),
                    CompletedTime = reader.IsDBNull(reader.GetOrdinal("CompletedTime")) ? null : reader.GetDateTime(reader.GetOrdinal("CompletedTime")),
                    ParentTask = reader.GetGuid(reader.GetOrdinal("ParentTask")),
                    ReassignmentCount = reader.GetInt32(reader.GetOrdinal("ReassignmentCount"))
                };
                subTasks.Add(subTask);
            }

            return subTasks;
        }

        /// <summary>
        /// 完整同步主任务及其子任务
        /// </summary>
        public async Task FullSyncMainTaskAsync(MainTask mainTask)
        {
            using var connection = await CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                // 1. 更新或插入主任务
                var existsTask = await GetMainTaskExistsAsync(mainTask.Id, transaction, connection);
                if (existsTask)
                {
                    var updateSql = @"
                    UPDATE MainTasks 
                    SET Description = @Description, Status = @Status, CompletedTime = @CompletedTime 
                    WHERE Id = @Id";

                    using var updateCmd = new SqlCommand(updateSql, connection, transaction);
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

                    using var insertCmd = new SqlCommand(insertSql, connection, transaction);
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
                    var existsSubTask = await GetSubTaskExistsAsync(subTask.Id, transaction, connection);
                    if (existsSubTask)
                    {
                        var updateSubSql = @"
                        UPDATE SubTasks 
                        SET Description = @Description, Status = @Status, AssignedTime = @AssignedTime, 
                            CompletedTime = @CompletedTime, ReassignmentCount = @ReassignmentCount
                        WHERE Id = @Id";

                        using var updateSubCmd = new SqlCommand(updateSubSql, connection, transaction);
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
                        INSERT INTO SubTasks (Id, Description, Status, CreationTime, AssignedTime, CompletedTime, ParentTask, ReassignmentCount)
                        VALUES (@Id, @Description, @Status, @CreationTime, @AssignedTime, @CompletedTime, @ParentTask, @ReassignmentCount)";

                        using var insertSubCmd = new SqlCommand(insertSubSql, connection, transaction);
                        insertSubCmd.Parameters.AddRange(new[]
                        {
                            new SqlParameter("@Id", subTask.Id),
                            new SqlParameter("@Description", subTask.Description),
                            new SqlParameter("@Status", (int)subTask.Status),
                            new SqlParameter("@CreationTime", subTask.CreationTime),
                            new SqlParameter("@AssignedTime", subTask.AssignedTime ?? (object)DBNull.Value),
                            new SqlParameter("@CompletedTime", subTask.CompletedTime ?? (object)DBNull.Value),
                            new SqlParameter("@ParentTask", mainTask.Id),
                            new SqlParameter("@ReassignmentCount", subTask.ReassignmentCount)
                        });
                        await insertSubCmd.ExecuteNonQueryAsync();
                    }
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// 检查主任务是否存在
        /// </summary>
        private async Task<bool> GetMainTaskExistsAsync(Guid taskId, SqlTransaction transaction, SqlConnection connection)
        {
            var sql = "SELECT 1 FROM MainTasks WHERE Id = @Id";
            using var cmd = new SqlCommand(sql, connection, transaction);
            cmd.Parameters.AddWithValue("@Id", taskId);
            return (await cmd.ExecuteScalarAsync()) != null;
        }

        /// <summary>
        /// 检查子任务是否存在
        /// </summary>
        private async Task<bool> GetSubTaskExistsAsync(Guid subTaskId, SqlTransaction transaction, SqlConnection connection)
        {
            var sql = "SELECT 1 FROM SubTasks WHERE Id = @Id";
            using var cmd = new SqlCommand(sql, connection, transaction);
            cmd.Parameters.AddWithValue("@Id", subTaskId);
            return (await cmd.ExecuteScalarAsync()) != null;
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
            WHERE ParentTask = @taskId";

            try
            {
                using var connection = await CreateConnectionAsync();
                using var command = new SqlCommand(sql, connection);
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

        // 记录无人机状态
        public async Task RecordDroneStatusFromDroneAsync(Drone drone)
        {
            using var connection = await CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();
            
            try
            {
                var sql = @"
                INSERT INTO DroneStatusHistory (DroneId, Status, Timestamp, CpuUsage, BandwidthAvailable, MemoryUsage, Latitude, Longitude)
                VALUES (@DroneId, @Status, @Timestamp, @CpuUsage, @BandwidthAvailable, @MemoryUsage, @Latitude, @Longitude)";

                using var cmd = new SqlCommand(sql, connection, transaction);
                cmd.Parameters.AddRange(new[]
                {
                    new SqlParameter("@DroneId", drone.Id),
                    new SqlParameter("@Status", (int)drone.Status),
                    new SqlParameter("@Timestamp", DateTime.Now),
                    new SqlParameter("@CpuUsage", ClampDecimal(drone.cpu_used_rate, 0m, 999.99m)),
                    new SqlParameter("@BandwidthAvailable", ClampDecimal(drone.left_bandwidth, 0m, 9999.99m)),
                    new SqlParameter("@MemoryUsage", ClampDecimal(drone.memory, 0m, 9999.99m)),
                    new SqlParameter("@Latitude", ClampGpsCoordinate(drone.CurrentPosition?.Latitude_x)),
                    new SqlParameter("@Longitude", ClampGpsCoordinate(drone.CurrentPosition?.Longitude_y))
                });
                
                await cmd.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error recording drone status for {DroneId}", drone.Id);
                throw;
            }
        }

        public async Task BulkRecordDroneStatusAsync(IEnumerable<Drone> drones)
        {
            using var connection = await CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();
            
            try
            {
                foreach (var drone in drones)
                {
                    var sql = @"
                    INSERT INTO DroneStatusHistory (DroneId, Status, Timestamp, CpuUsage, BandwidthAvailable, MemoryUsage, Latitude, Longitude)
                    VALUES (@DroneId, @Status, @Timestamp, @CpuUsage, @BandwidthAvailable, @MemoryUsage, @Latitude, @Longitude)";

                    using var cmd = new SqlCommand(sql, connection, transaction);
                    cmd.Parameters.AddRange(new[]
                    {
                        new SqlParameter("@DroneId", drone.Id),
                        new SqlParameter("@Status", (int)drone.Status),
                        new SqlParameter("@Timestamp", DateTime.Now),
                        new SqlParameter("@CpuUsage", ClampDecimal(drone.cpu_used_rate, 0m, 999.99m)),
                        new SqlParameter("@BandwidthAvailable", ClampDecimal(drone.left_bandwidth, 0m, 9999.99m)),
                        new SqlParameter("@MemoryUsage", ClampDecimal(drone.memory, 0m, 9999.99m)),
                        new SqlParameter("@Latitude", ClampGpsCoordinate(drone.CurrentPosition?.Latitude_x)),
                        new SqlParameter("@Longitude", ClampGpsCoordinate(drone.CurrentPosition?.Longitude_y))
                    });
                    
                    await cmd.ExecuteNonQueryAsync();
                    cmd.Parameters.Clear();
                }
                
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error bulk recording drone status");
                throw;
            }
        }

        // 数据查询方法
        public async Task<List<DroneDataPoint>> GetDroneDataInTimeRangeAsync(
            Guid droneId,
            DateTime startTime,
            DateTime endTime)
        {
            var dataPoints = new List<DroneDataPoint>();
            var sql = @"
                SELECT DroneId, Status, Timestamp, CpuUsage, BandwidthAvailable, MemoryUsage, Latitude, Longitude
                FROM DroneStatusHistory
                WHERE DroneId = @DroneId AND Timestamp BETWEEN @StartTime AND @EndTime
                ORDER BY Timestamp";

            using var connection = await CreateConnectionAsync();
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddRange(new[]
            {
                new SqlParameter("@DroneId", droneId),
                new SqlParameter("@StartTime", startTime),
                new SqlParameter("@EndTime", endTime)
            });

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                dataPoints.Add(new DroneDataPoint
                {
                    DroneId = reader.GetGuid(reader.GetOrdinal("DroneId")),
                    Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp")),
                    cpuUsage = reader.GetDecimal(reader.GetOrdinal("CpuUsage")),
                    Latitude = reader.IsDBNull(reader.GetOrdinal("Latitude")) ? 0 : reader.GetDecimal(reader.GetOrdinal("Latitude")),
                    Longitude = reader.IsDBNull(reader.GetOrdinal("Longitude")) ? 0 : reader.GetDecimal(reader.GetOrdinal("Longitude"))
                });
            }

            return dataPoints;
        }

        public async Task<List<DroneDataPoint>> GetAllDronesDataInTimeRangeAsync(
            DateTime startTime,
            DateTime endTime)
        {
            var dataPoints = new List<DroneDataPoint>();
            var sql = @"
                SELECT DroneId, Status, Timestamp, CpuUsage, BandwidthAvailable, MemoryUsage, Latitude, Longitude
                FROM DroneStatusHistory
                WHERE Timestamp BETWEEN @StartTime AND @EndTime
                ORDER BY Timestamp";

            using var connection = await CreateConnectionAsync();
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddRange(new[]
            {
                new SqlParameter("@StartTime", startTime),
                new SqlParameter("@EndTime", endTime)
            });

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    dataPoints.Add(new DroneDataPoint
                    {
                        DroneId = reader.GetGuid(reader.GetOrdinal("DroneId")),
                        Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp")),
                        cpuUsage = reader.GetDecimal(reader.GetOrdinal("CpuUsage")),
                        Latitude = reader.IsDBNull(reader.GetOrdinal("Latitude")) ? 0 : reader.GetDecimal(reader.GetOrdinal("Latitude")),
                        Longitude = reader.IsDBNull(reader.GetOrdinal("Longitude")) ? 0 : reader.GetDecimal(reader.GetOrdinal("Longitude"))
                    });
                }
            }

            return dataPoints;
        }

        #region 子任务图片管理

        /// <summary>
        /// 保存子任务图片到数据库
        /// </summary>
        /// <param name="subTaskId">子任务ID</param>
        /// <param name="imageData">图片二进制数据</param>
        /// <param name="fileName">文件名</param>
        /// <param name="imageIndex">图片序号</param>
        /// <param name="description">图片描述</param>
        /// <returns>图片ID</returns>
        public async Task<Guid> SaveSubTaskImageAsync(Guid subTaskId, byte[] imageData, string fileName, int imageIndex = 1, string? description = null)
        {
            var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
            var contentType = GetContentType(fileExtension);
            var imageId = Guid.NewGuid();

            var sql = @"
                INSERT INTO SubTaskImages (Id, SubTaskId, ImageData, FileName, FileExtension, FileSize, ContentType, ImageIndex, Description)
                VALUES (@Id, @SubTaskId, @ImageData, @FileName, @FileExtension, @FileSize, @ContentType, @ImageIndex, @Description);";

            var parameters = new[]
            {
                new SqlParameter("@Id", imageId),
                new SqlParameter("@SubTaskId", subTaskId),
                new SqlParameter("@ImageData", imageData),
                new SqlParameter("@FileName", fileName),
                new SqlParameter("@FileExtension", fileExtension),
                new SqlParameter("@FileSize", imageData.Length),
                new SqlParameter("@ContentType", contentType),
                new SqlParameter("@ImageIndex", imageIndex),
                new SqlParameter("@Description", description ?? (object)DBNull.Value)
            };

            try
            {
                using var connection = await CreateConnectionAsync();
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddRange(parameters);
                await command.ExecuteNonQueryAsync();
                
                _logger.LogInformation("图片保存到数据库成功: SubTaskId={SubTaskId}, ImageId={ImageId}, FileName={FileName}, Size={Size}字节", 
                    subTaskId, imageId, fileName, imageData.Length);
                
                return imageId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存子任务图片到数据库失败: SubTaskId={SubTaskId}, FileName={FileName}", subTaskId, fileName);
                throw;
            }
        }

        /// <summary>
        /// 获取子任务的所有图片（包含二进制数据）
        /// </summary>
        /// <param name="subTaskId">子任务ID</param>
        /// <returns>图片列表</returns>
        public async Task<List<SubTaskImage>> GetSubTaskImagesAsync(Guid subTaskId)
        {
            var images = new List<SubTaskImage>();
            var sql = @"
                SELECT Id, SubTaskId, FileName, FileExtension, FileSize, ContentType, ImageIndex, UploadTime, Description
                FROM SubTaskImages
                WHERE SubTaskId = @SubTaskId
                ORDER BY ImageIndex, UploadTime";

            try
            {
                using var connection = await CreateConnectionAsync();
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@SubTaskId", subTaskId);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    images.Add(new SubTaskImage
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        SubTaskId = reader.GetGuid(reader.GetOrdinal("SubTaskId")),
                        FileName = reader.GetString(reader.GetOrdinal("FileName")),
                        FileExtension = reader.GetString(reader.GetOrdinal("FileExtension")),
                        FileSize = reader.GetInt64(reader.GetOrdinal("FileSize")),
                        ContentType = reader.GetString(reader.GetOrdinal("ContentType")),
                        ImageIndex = reader.GetInt32(reader.GetOrdinal("ImageIndex")),
                        UploadTime = reader.GetDateTime(reader.GetOrdinal("UploadTime")),
                        Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取子任务图片失败: SubTaskId={SubTaskId}", subTaskId);
            }

            return images;
        }

        /// <summary>
        /// 获取子任务的图片元数据（不包含二进制数据，用于内存优化）
        /// </summary>
        /// <param name="subTaskId">子任务ID</param>
        /// <returns>图片元数据列表</returns>
        public async Task<List<SubTaskImage>> GetSubTaskImageMetadataAsync(Guid subTaskId)
        {
            var images = new List<SubTaskImage>();
            var sql = @"
                SELECT Id, SubTaskId, FileName, FileExtension, FileSize, ContentType, ImageIndex, UploadTime, Description
                FROM SubTaskImages
                WHERE SubTaskId = @SubTaskId
                ORDER BY ImageIndex, UploadTime";

            try
            {
                using var connection = await CreateConnectionAsync();
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@SubTaskId", subTaskId);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    images.Add(new SubTaskImage
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        SubTaskId = reader.GetGuid(reader.GetOrdinal("SubTaskId")),
                        // 不加载ImageData字段，节省内存
                        ImageData = null, 
                        FileName = reader.GetString(reader.GetOrdinal("FileName")),
                        FileExtension = reader.GetString(reader.GetOrdinal("FileExtension")),
                        FileSize = reader.GetInt64(reader.GetOrdinal("FileSize")),
                        ContentType = reader.GetString(reader.GetOrdinal("ContentType")),
                        ImageIndex = reader.GetInt32(reader.GetOrdinal("ImageIndex")),
                        UploadTime = reader.GetDateTime(reader.GetOrdinal("UploadTime")),
                        Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description"))
                    });
                }
                
                _logger.LogDebug("获取子任务图片元数据成功: SubTaskId={SubTaskId}, 图片数={ImageCount}", subTaskId, images.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取子任务图片元数据失败: SubTaskId={SubTaskId}", subTaskId);
            }

            return images;
        }

        /// <summary>
        /// 根据图片ID获取图片数据
        /// </summary>
        /// <param name="imageId">图片ID</param>
        /// <returns>图片数据</returns>
        public async Task<SubTaskImage?> GetSubTaskImageAsync(Guid imageId)
        {
            var sql = @"
                SELECT Id, SubTaskId, ImageData, FileName, FileExtension, FileSize, ContentType, ImageIndex, UploadTime, Description
                FROM SubTaskImages
                WHERE Id = @ImageId";

            try
            {
                using var connection = await CreateConnectionAsync();
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@ImageId", imageId);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new SubTaskImage
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        SubTaskId = reader.GetGuid(reader.GetOrdinal("SubTaskId")),
                        ImageData = (byte[])reader["ImageData"],
                        FileName = reader.GetString(reader.GetOrdinal("FileName")),
                        FileExtension = reader.GetString(reader.GetOrdinal("FileExtension")),
                        FileSize = reader.GetInt64(reader.GetOrdinal("FileSize")),
                        ContentType = reader.GetString(reader.GetOrdinal("ContentType")),
                        ImageIndex = reader.GetInt32(reader.GetOrdinal("ImageIndex")),
                        UploadTime = reader.GetDateTime(reader.GetOrdinal("UploadTime")),
                        Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description"))
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取图片数据失败: ImageId={ImageId}", imageId);
            }

            return null;
        }

        /// <summary>
        /// 删除子任务的所有图片
        /// </summary>
        /// <param name="subTaskId">子任务ID</param>
        /// <returns>删除的图片数量</returns>
        public async Task<int> DeleteSubTaskImagesAsync(Guid subTaskId)
        {
            var sql = "DELETE FROM SubTaskImages WHERE SubTaskId = @SubTaskId";
            
            try
            {
                var deletedCount = await ExecuteNonQueryAsync(sql, new SqlParameter("@SubTaskId", subTaskId));
                _logger.LogInformation("删除子任务图片: SubTaskId={SubTaskId}, 删除数量={DeletedCount}", subTaskId, deletedCount);
                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除子任务图片失败: SubTaskId={SubTaskId}", subTaskId);
                throw;
            }
        }

        /// <summary>
        /// 删除指定图片
        /// </summary>
        /// <param name="imageId">图片ID</param>
        /// <returns>是否删除成功</returns>
        public async Task<bool> DeleteSubTaskImageAsync(Guid imageId)
        {
            var sql = "DELETE FROM SubTaskImages WHERE Id = @ImageId";
            
            try
            {
                var result = await ExecuteNonQueryAsync(sql, new SqlParameter("@ImageId", imageId));
                _logger.LogInformation("删除图片: ImageId={ImageId}, 结果={Result}", imageId, result > 0);
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除图片失败: ImageId={ImageId}", imageId);
                return false;
            }
        }

        /// <summary>
        /// 根据文件扩展名获取MIME类型
        /// </summary>
        /// <param name="fileExtension">文件扩展名</param>
        /// <returns>MIME类型</returns>
        private static string GetContentType(string fileExtension)
        {
            return fileExtension.ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                _ => "image/png" // 默认为PNG
            };
        }

        #endregion

        /// <summary>
        /// 从数据库获取单个主任务
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns>主任务对象，如果不存在则返回null</returns>
        public async Task<MainTask?> GetMainTaskAsync(Guid taskId)
        {
            try
            {
                var sql = @"
                    SELECT Id, Description, Status, CreationTime, CompletedTime
                    FROM MainTasks 
                    WHERE Id = @TaskId";

                using var connection = await CreateConnectionAsync();
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@TaskId", taskId);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var mainTask = new MainTask
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        Description = reader.GetString(reader.GetOrdinal("Description")),
                        Status = (System.Threading.Tasks.TaskStatus)reader.GetInt32(reader.GetOrdinal("Status")),
                        CreationTime = reader.GetDateTime(reader.GetOrdinal("CreationTime")),
                        CompletedTime = reader.IsDBNull(reader.GetOrdinal("CompletedTime")) ? null : reader.GetDateTime(reader.GetOrdinal("CompletedTime"))
                    };

                    // 关闭当前reader
                    reader.Close();

                    // 加载子任务
                    var subTasks = await GetSubTasksByParentAsync(taskId);
                    mainTask.SubTasks.AddRange(subTasks);

                    _logger.LogDebug("从数据库获取主任务成功: TaskId={TaskId}, SubTaskCount={SubTaskCount}", 
                        taskId, subTasks.Count);

                    return mainTask;
                }

                _logger.LogDebug("未找到主任务: TaskId={TaskId}", taskId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从数据库获取主任务失败: TaskId={TaskId}", taskId);
                return null;
            }
        }

        /// <summary>
        /// 获取子任务的图片数量
        /// </summary>
        public async Task<int> GetSubTaskImageCountAsync(Guid subTaskId)
        {
            try
            {
                var sql = "SELECT COUNT(*) FROM SubTaskImages WHERE SubTaskId = @SubTaskId";
                using var connection = await CreateConnectionAsync();
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@SubTaskId", subTaskId);
                
                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取子任务图片数量失败: SubTaskId={SubTaskId}", subTaskId);
                return 0;
            }
        }

        /// <summary>
        /// 获取最近上传的图片
        /// </summary>
        public async Task<List<SubTaskImage>> GetRecentSubTaskImagesAsync(DateTime since, int limit = 50)
        {
            try
            {
                var sql = @"
                    SELECT TOP(@Limit) Id, SubTaskId, ImageData, FileName, FileExtension, FileSize, ContentType, ImageIndex, UploadTime, Description
                    FROM SubTaskImages 
                    WHERE UploadTime >= @Since
                    ORDER BY UploadTime DESC";

                using var connection = await CreateConnectionAsync();
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@Since", since);
                command.Parameters.AddWithValue("@Limit", limit);

                var images = new List<SubTaskImage>();
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    images.Add(new SubTaskImage
                    {
                        Id = reader.GetGuid("Id"),
                        SubTaskId = reader.GetGuid("SubTaskId"),
                        ImageData = (byte[])reader["ImageData"],
                        FileName = reader.GetString("FileName"),
                        FileExtension = reader.GetString("FileExtension"),
                        FileSize = reader.GetInt64("FileSize"),
                        ContentType = reader.GetString("ContentType"),
                        ImageIndex = reader.GetInt32("ImageIndex"),
                        UploadTime = reader.GetDateTime("UploadTime"),
                        Description = reader.IsDBNull("Description") ? "" : reader.GetString("Description")
                    });
                }
                
                return images;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取最近图片失败");
                return new List<SubTaskImage>();
            }
        }

        /// <summary>
        /// 从数据库获取子任务信息（根据子任务名称）
        /// </summary>
        /// <param name="taskId">主任务ID</param>
        /// <param name="subTaskDescription">子任务描述</param>
        /// <returns>子任务对象，如果不存在则返回null</returns>
        public async Task<SubTask?> GetSubTaskByDescriptionAsync(Guid taskId, string subTaskDescription)
        {
            try
            {
                var sql = @"
                    SELECT Id, Description, Status, CreationTime, AssignedTime, CompletedTime, ParentTask, ReassignmentCount, AssignedDrone
                    FROM SubTasks 
                    WHERE ParentTask = @TaskId AND Description = @Description";

                using var connection = await CreateConnectionAsync();
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@TaskId", taskId);
                command.Parameters.AddWithValue("@Description", subTaskDescription);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var subTask = new SubTask
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        Description = reader.GetString(reader.GetOrdinal("Description")),
                        Status = (TaskStatus)Convert.ToInt32(reader.GetOrdinal("Status")),
                        CreationTime = reader.GetDateTime(reader.GetOrdinal("CreationTime")),
                        AssignedTime = reader.IsDBNull(reader.GetOrdinal("AssignedTime")) ? null : reader.GetDateTime(reader.GetOrdinal("AssignedTime")),
                        CompletedTime = reader.IsDBNull(reader.GetOrdinal("CompletedTime")) ? null : reader.GetDateTime(reader.GetOrdinal("CompletedTime")),
                        ParentTask = reader.GetGuid(reader.GetOrdinal("ParentTask")),
                        ReassignmentCount = reader.GetInt32(reader.GetOrdinal("ReassignmentCount")),
                        AssignedDrone = reader.IsDBNull(reader.GetOrdinal("AssignedDrone")) ? string.Empty : reader.GetString(reader.GetOrdinal("AssignedDrone"))
                    };

                    _logger.LogDebug("从数据库获取子任务成功: SubTaskId={SubTaskId}, Description={Description}", 
                        subTask.Id, subTaskDescription);

                    return subTask;
                }

                _logger.LogDebug("未找到子任务: TaskId={TaskId}, Description={Description}", taskId, subTaskDescription);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从数据库获取子任务失败: TaskId={TaskId}, Description={Description}", taskId, subTaskDescription);
                return null;
            }
        }

        /// <summary>
        /// 获取无人机总数
        /// </summary>
        /// <returns>无人机总数</returns>
        public async Task<int> GetDroneCountAsync()
        {
            try
            {
                var sql = "SELECT COUNT(*) FROM Drones";
                using var connection = await CreateConnectionAsync();
                using var cmd = new SqlCommand(sql, connection);
                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取无人机总数失败");
                return 0;
            }
        }

        /// <summary>
        /// 分页获取无人机数据
        /// </summary>
        /// <param name="page">页码（从0开始）</param>
        /// <param name="pageSize">每页数量</param>
        /// <returns>无人机列表</returns>
        public async Task<List<Drone>> GetDronesByPageAsync(int page, int pageSize)
        {
            var drones = new List<Drone>();
            var sql = @"
                SELECT Id, Name, ModelStatus, ModelType, RegistrationDate, LastHeartbeat 
                FROM Drones 
                ORDER BY RegistrationDate
                OFFSET @Offset ROWS 
                FETCH NEXT @PageSize ROWS ONLY";

            try
            {
                using var connection = await CreateConnectionAsync();
                using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@Offset", page * pageSize);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);
                
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var drone = new Drone
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        ModelStatus = (ModelStatus)reader.GetByte(reader.GetOrdinal("ModelStatus")),
                        ModelType = reader.GetString(reader.GetOrdinal("ModelType"))
                    };
                    drones.Add(drone);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "分页获取无人机数据失败: Page={Page}, PageSize={PageSize}", page, pageSize);
            }

            return drones;
        }

    }
}