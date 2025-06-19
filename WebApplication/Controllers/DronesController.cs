using WebApplication.Service;
using ClassLibrary_Core.Drone;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ClassLibrary_Core.Common;
using ClassLibrary_Core.Mission;

namespace WebApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DronesController : ControllerBase
    {
        private readonly IDroneService _droneService;

        public DronesController(IDroneService droneService)
        {
            _droneService = droneService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Drone>>> GetAll()
        {
            var drones = await _droneService.GetAllDronesAsync();
            return Ok(drones);
        }

        [HttpGet("{id:Guid}")]
        public async Task<ActionResult<Drone>> Get(Guid id)
        {
            var drone = await _droneService.GetDroneByIdAsync(id);
            return drone is not null ? Ok(drone) : NotFound();
        }

        [HttpGet("name/{name}")]
        public async Task<ActionResult<Drone>> GetByName(string name)
        {
            var drone = await _droneService.GetDroneByNameAsync(name);
            return drone is not null ? Ok(drone) : NotFound();
        }

        [HttpPost]
        public async Task<ActionResult<Drone>> Create(Drone drone)
        {
            var createdDrone = await _droneService.CreateDroneAsync(drone);
            return CreatedAtAction(nameof(Get), new { id = createdDrone.Id }, createdDrone);
        }

        [HttpPut("{id:Guid}")]
        public async Task<IActionResult> Update(Guid id, Drone updated)
        {
            if (id != updated.Id)
                return BadRequest("ID不匹配");

            var existingDrone = await _droneService.GetDroneByIdAsync(id);
            if (existingDrone == null)
                return NotFound();

            var result = await _droneService.UpdateDroneAsync(updated);
            return Ok(result);
        }

        [HttpDelete("{id:Guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var existingDrone = await _droneService.GetDroneByIdAsync(id);
            if (existingDrone == null)
                return NotFound();

            await _droneService.DeleteDroneAsync(id);
            return NoContent();
        }

        [HttpGet("cluster/status")]
        public async Task<IActionResult> GetClusterStatus()
        {
            var clusterStatus = await _droneService.GetClusterStatusAsync();
            return Ok(clusterStatus);
        }

        [HttpPut("{id:Guid}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] DroneStatus status)
        {
            var drone = await _droneService.UpdateDroneStatusAsync(id, status);
            return drone is not null ? Ok(drone) : NotFound();
        }

        [HttpPut("{id:Guid}/position")]
        public async Task<IActionResult> UpdatePosition(Guid id, [FromBody] GPSPosition position)
        {
            var drone = await _droneService.UpdateDronePositionAsync(id, position);
            return drone is not null ? Ok(drone) : NotFound();
        }

        [HttpPut("{id:Guid}/heartbeat")]
        public async Task<IActionResult> UpdateHeartbeat(Guid id)
        {
            await _droneService.UpdateDroneHeartbeatAsync(id);
            return Ok();
        }

        [HttpGet("{id:Guid}/subtasks")]
        public async Task<IActionResult> GetSubTasks(Guid id)
        {
            var subTasks = await _droneService.GetDroneSubTasksAsync(id);
            return Ok(subTasks);
        }

        [HttpPost("{id:Guid}/subtasks")]
        public async Task<IActionResult> AddSubTask(Guid id, [FromBody] SubTask subTask)
        {
            var result = await _droneService.AddSubTaskToDroneAsync(id, subTask);
            return result ? Ok() : BadRequest("添加子任务失败");
        }

        [HttpPut("{id:Guid}/subtasks/{subTaskId:Guid}")]
        public async Task<IActionResult> UpdateSubTask(Guid id, Guid subTaskId, [FromBody] SubTask subTask)
        {
            if (subTaskId != subTask.Id)
                return BadRequest("子任务ID不匹配");

            var result = await _droneService.UpdateDroneSubTaskAsync(id, subTask);
            return result ? Ok() : NotFound();
        }

        [HttpDelete("{id:Guid}/subtasks/{subTaskId:Guid}")]
        public async Task<IActionResult> RemoveSubTask(Guid id, Guid subTaskId)
        {
            var result = await _droneService.RemoveSubTaskFromDroneAsync(id, subTaskId);
            return result ? NoContent() : NotFound();
        }

        [HttpGet("{id:Guid}/data/recent")]
        public async Task<IActionResult> GetRecentData(Guid id, [FromQuery] int hours = 1)
        {
            var duration = TimeSpan.FromHours(hours);
            var data = await _droneService.GetRecentDroneDataAsync(id, duration);
            return Ok(data);
        }

        [HttpGet("{id:Guid}/data/task/{taskId:Guid}")]
        public async Task<IActionResult> GetTaskData(Guid id, Guid taskId)
        {
            var data = await _droneService.GetDroneTaskDataAsync(id, taskId);
            return Ok(data);
        }

        [HttpPost("bulk-update")]
        public async Task<IActionResult> BulkUpdate([FromBody] IEnumerable<Drone> drones)
        {
            await _droneService.BulkUpdateDronesAsync(drones);
            return Ok();
        }

        [HttpPost("{id:Guid}/record-status")]
        public async Task<IActionResult> RecordStatus(Guid id)
        {
            var drone = await _droneService.GetDroneByIdAsync(id);
            if (drone == null)
                return NotFound();

            await _droneService.RecordDroneStatusAsync(drone);
            return Ok();
        }

        [HttpPost("bulk-record-status")]
        public async Task<IActionResult> BulkRecordStatus([FromBody] IEnumerable<Guid> droneIds)
        {
            var drones = new List<Drone>();
            foreach (var id in droneIds)
            {
                var drone = await _droneService.GetDroneByIdAsync(id);
                if (drone != null)
                    drones.Add(drone);
            }

            await _droneService.BulkRecordDroneStatusAsync(drones);
            return Ok();
        }

        [HttpGet("name/{droneName}/active-tasks")]
        public async Task<IActionResult> GetActiveTasks(string droneName)
        {
            var activeTasks = await _droneService.GetActiveSubTasksForDroneAsync(droneName);
            return Ok(activeTasks);
        }

        [HttpPost("update-connections")]
        public async Task<IActionResult> UpdateConnections([FromBody] List<Drone> drones)
        {
            await _droneService.UpdateDroneConnectionsAsync(drones);
            return Ok();
        }

        [HttpGet("{id:Guid}/exists")]
        public async Task<IActionResult> CheckExists(Guid id)
        {
            var exists = await _droneService.DroneExistsAsync(id);
            return Ok(new { exists });
        }
    }
} 