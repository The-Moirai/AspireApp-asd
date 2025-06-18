using WebApplication_Drone.Services;
using ClassLibrary_Core.Drone;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebApplication_Drone.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DronesController : ControllerBase
    {
        private readonly DroneDataService _droneDataService;

        public DronesController(DroneDataService droneDataService)
        {
            _droneDataService = droneDataService;
        }
        [HttpGet]
        public ActionResult<IEnumerable<Drone>> GetAll()
        {
            var drones = _droneDataService.GetDrones();
            return Ok(drones);
        }

        [HttpGet("{id:int}")]
        public ActionResult<Drone> Get(Guid id)
        {
            var drone = _droneDataService.GetDrone(id);
            return drone is not null ? Ok(drone) : NotFound();
        }
        [HttpPost]
        public ActionResult<Drone> Create(Drone drone)
        {
            _droneDataService.AddDrone(drone);
            return CreatedAtAction(nameof(Get), new { id = drone.Id }, drone);
        }
        [HttpPut("{id:int}")]
        public IActionResult Update(int id, Drone updated)
        {
            var result = _droneDataService.UpdateDrone(updated);
            return result ? Ok(updated) : NotFound();
        }

        [HttpDelete("{id:int}")]
        public IActionResult Delete(Guid id)
        {
            var result = _droneDataService.DeleteDrone(id);
            return result ? NoContent() : NotFound();

        }

        [HttpGet("cluster/status")]
        public IActionResult GetClusterStatus()
        {
            var drones = _droneDataService.GetDrones();
            var total = drones.Count;
            var online = drones.Count(d => d.Status != DroneStatus.Offline);
            var inMission = drones.Count(d => d.Status == DroneStatus.InMission);
            return Ok(new
            {
                Total = total,
                Online = online,
                InMission = inMission
            });
        }
    }
}
