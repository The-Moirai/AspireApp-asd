using WebApplication.Data;
using ClassLibrary_Core.Mission;
using ClassLibrary_Core.Drone;

namespace WebApplication.Service
{
    public class MissionService : IMissionService
    {
        private readonly IDatabaseService _database;

        public MissionService(IDatabaseService database)
        {
            _database = database;
        }

        public async Task<IEnumerable<MissionHistory>> GetAllMissionHistoriesAsync()
        {
            const string sql = @"
                SELECT SubTaskDescription, SubTaskId, Operation, DroneName, Time 
                FROM MissionHistory 
                ORDER BY Time DESC";
            
            return await _database.QueryAsync<MissionHistory>(sql);
        }

        public async Task<MissionHistory> GetMissionHistoryByIdAsync(Guid id)
        {
            const string sql = @"
                SELECT SubTaskDescription, SubTaskId, Operation, DroneName, Time 
                FROM MissionHistory 
                WHERE SubTaskId = @Id";
            
            var result = await _database.QueryAsync<MissionHistory>(sql, new { Id = id });
            return result.FirstOrDefault();
        }

        public async Task<MissionHistory> CreateMissionHistoryAsync(MissionHistory missionHistory)
        {
            if (missionHistory.Time == DateTime.MinValue)
                missionHistory.Time = DateTime.UtcNow;

            const string sql = @"
                INSERT INTO MissionHistory (SubTaskDescription, SubTaskId, Operation, DroneName, Time)
                VALUES (@SubTaskDescription, @SubTaskId, @Operation, @DroneName, @Time)";
            
            await _database.ExecuteAsync(sql, new 
            { 
                missionHistory.SubTaskDescription,
                missionHistory.SubTaskId,
                missionHistory.Operation,
                missionHistory.DroneName,
                missionHistory.Time
            });
            
            return missionHistory;
        }

        public async Task<IEnumerable<MissionHistory>> GetDroneRecentMissionsAsync(string droneName, TimeSpan timeSpan)
        {
            var startTime = DateTime.UtcNow - timeSpan;
            const string sql = @"
                SELECT SubTaskDescription, SubTaskId, Operation, DroneName, Time 
                FROM MissionHistory 
                WHERE DroneName = @DroneName AND Time >= @StartTime
                ORDER BY Time DESC";
            
            return await _database.QueryAsync<MissionHistory>(sql, new { DroneName = droneName, StartTime = startTime });
        }

        public async Task<IEnumerable<MissionHistory>> GetDroneMissionsByTaskAsync(string droneName, Guid taskId)
        {
            const string sql = @"
                SELECT SubTaskDescription, SubTaskId, Operation, DroneName, Time 
                FROM MissionHistory 
                WHERE DroneName = @DroneName AND SubTaskId = @TaskId
                ORDER BY Time DESC";
            
            return await _database.QueryAsync<MissionHistory>(sql, new { DroneName = droneName, TaskId = taskId });
        }

        public async Task<IEnumerable<MissionHistory>> GetTaskMissionsByDroneAsync(Guid taskId, string droneName)
        {
            const string sql = @"
                SELECT SubTaskDescription, SubTaskId, Operation, DroneName, Time 
                FROM MissionHistory 
                WHERE SubTaskId = @TaskId AND DroneName = @DroneName
                ORDER BY Time DESC";
            
            return await _database.QueryAsync<MissionHistory>(sql, new { TaskId = taskId, DroneName = droneName });
        }

        public async Task<IEnumerable<MissionHistory>> GetTaskMissionsForAllDronesAsync(Guid taskId)
        {
            const string sql = @"
                SELECT SubTaskDescription, SubTaskId, Operation, DroneName, Time 
                FROM MissionHistory 
                WHERE SubTaskId = @TaskId
                ORDER BY Time DESC";
            
            return await _database.QueryAsync<MissionHistory>(sql, new { TaskId = taskId });
        }

        public async Task<IEnumerable<MissionHistory>> GetMissionsByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            const string sql = @"
                SELECT SubTaskDescription, SubTaskId, Operation, DroneName, Time 
                FROM MissionHistory 
                WHERE Time >= @StartTime AND Time <= @EndTime
                ORDER BY Time DESC";
            
            return await _database.QueryAsync<MissionHistory>(sql, new { StartTime = startTime, EndTime = endTime });
        }

        public async Task<Dictionary<string, IEnumerable<MissionHistory>>> GetDronesMissionsByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            const string sql = @"
                SELECT SubTaskDescription, SubTaskId, Operation, DroneName, Time 
                FROM MissionHistory 
                WHERE Time >= @StartTime AND Time <= @EndTime
                ORDER BY Time DESC";
            
            var missions = await _database.QueryAsync<MissionHistory>(sql, new { StartTime = startTime, EndTime = endTime });
            
            return missions
                .GroupBy(m => m.DroneName ?? "Unknown")
                .ToDictionary(
                    g => g.Key,
                    g => g.AsEnumerable()
                );
        }
    }
} 