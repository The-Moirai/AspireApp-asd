using Microsoft.AspNetCore.Mvc;
using WebApplication.Service;
using ClassLibrary_Core.Mission;

namespace WebApplication.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MissionHistoryController : ControllerBase
    {
        private readonly IMissionService _missionService;

        public MissionHistoryController(IMissionService missionService)
        {
            _missionService = missionService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MissionHistory>>> GetMissionHistory(
            [FromQuery] string? droneName = null,
            [FromQuery] Guid? taskId = null,
            [FromQuery] DateTime? startTime = null,
            [FromQuery] DateTime? endTime = null,
            [FromQuery] int? recentHours = null,
            [FromQuery] bool groupByDrone = false)
        {
            // 如果指定了recentHours，则计算时间范围
            if (recentHours.HasValue)
            {
                endTime = DateTime.UtcNow;
                startTime = endTime.Value.AddHours(-recentHours.Value);
            }

            // 如果只指定了droneName，获取该无人机的所有历史记录
            if (!string.IsNullOrEmpty(droneName) && !taskId.HasValue && !startTime.HasValue && !endTime.HasValue)
            {
                var missions = await _missionService.GetDroneRecentMissionsAsync(droneName, TimeSpan.FromDays(365));
                return Ok(missions);
            }

            // 如果指定了droneName和taskId，获取该无人机特定任务的历史记录
            if (!string.IsNullOrEmpty(droneName) && taskId.HasValue)
            {
                var missions = await _missionService.GetDroneMissionsByTaskAsync(droneName, taskId.Value);
                return Ok(missions);
            }

            // 如果只指定了taskId，获取该任务的所有无人机历史记录
            if (taskId.HasValue && string.IsNullOrEmpty(droneName))
            {
                var missions = await _missionService.GetTaskMissionsForAllDronesAsync(taskId.Value);
                return Ok(missions);
            }

            // 如果指定了时间范围
            if (startTime.HasValue && endTime.HasValue)
            {
                if (groupByDrone)
                {
                    var missions = await _missionService.GetDronesMissionsByTimeRangeAsync(startTime.Value, endTime.Value);
                    return Ok(missions);
                }
                else
                {
                    var missions = await _missionService.GetMissionsByTimeRangeAsync(startTime.Value, endTime.Value);
                    return Ok(missions);
                }
            }

            // 如果没有指定任何过滤条件，返回所有历史记录
            var allMissions = await _missionService.GetAllMissionHistoriesAsync();
            return Ok(allMissions);
        }
    }
} 