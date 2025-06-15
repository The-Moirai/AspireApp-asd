using ClassLibrary_Core.Mission;
using ClassLibrary_Core.Drone;

namespace WebApplication.Service
{
    public interface IMissionService
    {
        // 基础CRUD操作
        Task<IEnumerable<MissionHistory>> GetAllMissionHistoriesAsync();
        Task<MissionHistory> GetMissionHistoryByIdAsync(Guid id);
        Task<MissionHistory> CreateMissionHistoryAsync(MissionHistory missionHistory);

        // 单个无人机相关查询
        Task<IEnumerable<MissionHistory>> GetDroneRecentMissionsAsync(string droneName, TimeSpan timeSpan);
        Task<IEnumerable<MissionHistory>> GetDroneMissionsByTaskAsync(string droneName, Guid taskId);
        
        // 单个任务相关查询
        Task<IEnumerable<MissionHistory>> GetTaskMissionsByDroneAsync(Guid taskId, string droneName);
        Task<IEnumerable<MissionHistory>> GetTaskMissionsForAllDronesAsync(Guid taskId);
        
        // 时间范围查询
        Task<IEnumerable<MissionHistory>> GetMissionsByTimeRangeAsync(DateTime startTime, DateTime endTime);
        Task<Dictionary<string, IEnumerable<MissionHistory>>> GetDronesMissionsByTimeRangeAsync(DateTime startTime, DateTime endTime);
    }
} 