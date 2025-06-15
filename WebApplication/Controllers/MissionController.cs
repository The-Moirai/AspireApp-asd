using Microsoft.AspNetCore.Mvc;
using WebApplication.Service;
using ClassLibrary_Core.Mission;

namespace WebApplication.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MissionController : ControllerBase
    {
        private readonly IMissionService _missionService;

        public MissionController(IMissionService missionService)
        {
            _missionService = missionService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MissionHistory>>> GetAllMissions()
        {
            var missions = await _missionService.GetAllMissionHistoriesAsync();
            return Ok(missions);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<MissionHistory>> GetMissionById(Guid id)
        {
            var mission = await _missionService.GetMissionHistoryByIdAsync(id);
            if (mission == null)
                return NotFound();
            return Ok(mission);
        }

        [HttpPost]
        public async Task<ActionResult<MissionHistory>> CreateMission(MissionHistory missionHistory)
        {
            var created = await _missionService.CreateMissionHistoryAsync(missionHistory);
            return CreatedAtAction(nameof(GetMissionById), new { id = created.SubTaskId }, created);
        }

        [HttpGet("drone/{droneName}/recent")]
        public async Task<ActionResult<IEnumerable<MissionHistory>>> GetDroneRecentMissions(
            string droneName,
            [FromQuery] int hours = 24)
        {
            var timeSpan = TimeSpan.FromHours(hours);
            var missions = await _missionService.GetDroneRecentMissionsAsync(droneName, timeSpan);
            return Ok(missions);
        }

        [HttpGet("drone/{droneName}/task/{taskId}")]
        public async Task<ActionResult<IEnumerable<MissionHistory>>> GetDroneMissionsByTask(
            string droneName,
            Guid taskId)
        {
            var missions = await _missionService.GetDroneMissionsByTaskAsync(droneName, taskId);
            return Ok(missions);
        }

        [HttpGet("task/{taskId}/drone/{droneName}")]
        public async Task<ActionResult<IEnumerable<MissionHistory>>> GetTaskMissionsByDrone(
            Guid taskId,
            string droneName)
        {
            var missions = await _missionService.GetTaskMissionsByDroneAsync(taskId, droneName);
            return Ok(missions);
        }

        [HttpGet("task/{taskId}/all-drones")]
        public async Task<ActionResult<IEnumerable<MissionHistory>>> GetTaskMissionsForAllDrones(
            Guid taskId)
        {
            var missions = await _missionService.GetTaskMissionsForAllDronesAsync(taskId);
            return Ok(missions);
        }

        [HttpGet("time-range")]
        public async Task<ActionResult<IEnumerable<MissionHistory>>> GetMissionsByTimeRange(
            [FromQuery] DateTime startTime,
            [FromQuery] DateTime endTime)
        {
            var missions = await _missionService.GetMissionsByTimeRangeAsync(startTime, endTime);
            return Ok(missions);
        }

        [HttpGet("time-range/all-drones")]
        public async Task<ActionResult<Dictionary<string, IEnumerable<MissionHistory>>>> GetDronesMissionsByTimeRange(
            [FromQuery] DateTime startTime,
            [FromQuery] DateTime endTime)
        {
            var missions = await _missionService.GetDronesMissionsByTimeRangeAsync(startTime, endTime);
            return Ok(missions);
        }
    }
} 