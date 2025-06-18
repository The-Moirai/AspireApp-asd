using Microsoft.EntityFrameworkCore;
using WebApplication.Data;
using ClassLibrary_Core.Mission;
using ClassLibrary_Core.Drone;

namespace WebApplication.Service
{
    public class MissionService : IMissionService
    {
        private readonly ApplicationDbContext _context;

        public MissionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<MissionHistory>> GetAllMissionHistoriesAsync()
        {
            return await _context.MissionHistories
                .OrderByDescending(m => m.Time)
                .ToListAsync();
        }

        public async Task<MissionHistory> GetMissionHistoryByIdAsync(Guid id)
        {
            return await _context.MissionHistories.FindAsync(id);
        }

        public async Task<MissionHistory> CreateMissionHistoryAsync(MissionHistory missionHistory)
        {
            _context.MissionHistories.Add(missionHistory);
            await _context.SaveChangesAsync();
            return missionHistory;
        }

        public async Task<IEnumerable<MissionHistory>> GetDroneRecentMissionsAsync(string droneName, TimeSpan timeSpan)
        {
            var startTime = DateTime.UtcNow - timeSpan;
            return await _context.MissionHistories
                .Where(m => m.DroneName == droneName && m.Time >= startTime)
                .OrderByDescending(m => m.Time)
                .ToListAsync();
        }

        public async Task<IEnumerable<MissionHistory>> GetDroneMissionsByTaskAsync(string droneName, Guid taskId)
        {
            return await _context.MissionHistories
                .Where(m => m.DroneName == droneName && m.SubTaskId == taskId)
                .OrderByDescending(m => m.Time)
                .ToListAsync();
        }

        public async Task<IEnumerable<MissionHistory>> GetTaskMissionsByDroneAsync(Guid taskId, string droneName)
        {
            return await _context.MissionHistories
                .Where(m => m.SubTaskId == taskId && m.DroneName == droneName)
                .OrderByDescending(m => m.Time)
                .ToListAsync();
        }

        public async Task<IEnumerable<MissionHistory>> GetTaskMissionsForAllDronesAsync(Guid taskId)
        {
            return await _context.MissionHistories
                .Where(m => m.SubTaskId == taskId)
                .OrderByDescending(m => m.Time)
                .ToListAsync();
        }

        public async Task<IEnumerable<MissionHistory>> GetMissionsByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            return await _context.MissionHistories
                .Where(m => m.Time >= startTime && m.Time <= endTime)
                .OrderByDescending(m => m.Time)
                .ToListAsync();
        }

        public async Task<Dictionary<string, IEnumerable<MissionHistory>>> GetDronesMissionsByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            var missions = await _context.MissionHistories
                .Where(m => m.Time >= startTime && m.Time <= endTime)
                .OrderByDescending(m => m.Time)
                .ToListAsync();

            return missions
                .GroupBy(m => m.DroneName)
                .ToDictionary(
                    g => g.Key,
                    g => g.AsEnumerable()
                );
        }
    }
} 