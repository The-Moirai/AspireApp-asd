using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebApplication_Drone.Services;

namespace WebApplication_Drone.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HistoryDataController : ControllerBase
    {
            private readonly DroneDataService _droneDataService;
            private readonly TaskDataService _taskDataService;

            public HistoryDataController(
                DroneDataService droneDataService,
                TaskDataService taskDataService)
            {
                _droneDataService = droneDataService;
                _taskDataService = taskDataService;
            }

            // 1. 单个无人机最新一段时间的数据
            [HttpGet("drone/{droneId}/recent")]
            public async Task<IActionResult> GetRecentDroneData(
                string droneId,
                [FromQuery] TimeSpan duration)
            {
                var data = await _droneDataService.GetRecentDroneDataAsync(droneId, duration);
                return Ok(data);
            }

            // 2. 单个无人机指定任务期间的数据
            [HttpGet("drone/{droneId}/task/{taskId}")]
            public async Task<IActionResult> GetDroneTaskData(
                string droneId,
                string taskId)
            {
                var data = await _droneDataService.GetDroneTaskDataAsync(droneId, taskId);
                return Ok(data);
            }
            // 3. 单个任务中指定无人机的数据
            [HttpGet("task/{taskId}/drone/{droneId}")]
            public async Task<IActionResult> GetTaskDroneData(
                string taskId,
                string droneId)
            {
                var data = await _taskDataService.GetTaskDroneDataAsync(taskId, droneId);
                return Ok(data);
            }

            // 4. 单个任务期间所有无人机的数据
            [HttpGet("task/{taskId}/drones")]
            public async Task<IActionResult> GetTaskAllDronesData(string taskId)
            {
                var data = await _taskDataService.GetTaskAllDronesDataAsync(taskId);
                return Ok(data);
            }

            // 5. 指定时间段内所有无人机的数据
            [HttpGet("drones/time-range")]
            public async Task<IActionResult> GetAllDronesDataInTimeRange(
                [FromQuery] DateTime startTime,
                [FromQuery] DateTime endTime)
            {
                var data = await _droneDataService.GetAllDronesDataInTimeRangeAsync(startTime, endTime);
                return Ok(data);
            }

            // 6. 指定时间段内所有任务的数据
            [HttpGet("tasks/time-range")]
            public async Task<IActionResult> GetAllTasksDataInTimeRange(
                [FromQuery] DateTime startTime,
                [FromQuery] DateTime endTime)
            {
                var data = await _taskDataService.GetAllTasksDataInTimeRangeAsync(startTime, endTime);
                return Ok(data);
            }       
    }
    }
