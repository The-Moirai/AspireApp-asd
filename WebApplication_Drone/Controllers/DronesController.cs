using Microsoft.AspNetCore.Mvc;
using WebApplication_Drone.Services.Clean;
using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Data;

namespace WebApplication_Drone.Controllers
{
    /// <summary>
    /// 无人机控制器 - 使用新的分层架构
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class DronesController : ControllerBase
    {
        private readonly DroneService _droneService;
        private readonly ILogger<DronesController> _logger;

        public DronesController(DroneService droneService, ILogger<DronesController> logger)
        {
            _droneService = droneService;
            _logger = logger;
        }

        /// <summary>
        /// 获取所有无人机
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Drone>>> GetAll()
        {
            try
            {
                var drones = await _droneService.GetDronesAsync();
                return Ok(drones);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取所有无人机失败");
                return StatusCode(500, new { error = "获取无人机失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 根据ID获取无人机
        /// </summary>
        [HttpGet("{id:Guid}")]
        public async Task<ActionResult<Drone>> Get(Guid id)
        {
            try
            {
                var drone = await _droneService.GetDroneAsync(id);
                return drone is not null ? Ok(drone) : NotFound(new { error = "无人机未找到" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取无人机失败: {DroneId}", id);
                return StatusCode(500, new { error = "获取无人机失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 根据名称获取无人机
        /// </summary>
        [HttpGet("name/{droneName}")]
        public async Task<ActionResult<Drone>> GetByName(string droneName)
        {
            try
            {
                var drone = await _droneService.GetDroneByNameAsync(droneName);
                return drone is not null ? Ok(drone) : NotFound(new { error = "无人机未找到" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "根据名称获取无人机失败: {DroneName}", droneName);
                return StatusCode(500, new { error = "获取无人机失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 创建无人机
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Drone>> Create(Drone drone)
        {
            try
            {
                var success = await _droneService.AddDroneAsync(drone);
                if (success)
                {
                    return CreatedAtAction(nameof(Get), new { id = drone.Id }, drone);
                }
                return BadRequest(new { error = "创建无人机失败" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建无人机失败");
                return StatusCode(500, new { error = "创建无人机失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 更新无人机
        /// </summary>
        [HttpPut("{id:Guid}")]
        public async Task<IActionResult> Update(Guid id, Drone updated)
        {
            try
            {
                if (id != updated.Id)
                {
                    return BadRequest(new { error = "ID不匹配" });
                }

                var result = await _droneService.UpdateDroneAsync(updated);
                return result ? Ok(updated) : NotFound(new { error = "无人机未找到" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新无人机失败: {DroneId}", id);
                return StatusCode(500, new { error = "更新无人机失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 删除无人机
        /// </summary>
        [HttpDelete("{id:Guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var result = await _droneService.DeleteDroneAsync(id);
                return result ? NoContent() : NotFound(new { error = "无人机未找到" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除无人机失败: {DroneId}", id);
                return StatusCode(500, new { error = "删除无人机失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取无人机集群状态
        /// </summary>
        [HttpGet("cluster/status")]
        public async Task<IActionResult> GetClusterStatus()
        {
            try
            {
                var drones = await _droneService.GetDronesAsync();
                var total = drones.Count;
                var online = drones.Count(d => d.Status !=DroneStatus.Offline&& d.Status != DroneStatus.Maintenance && d.Status != DroneStatus.Emergency);
                var offline = drones.Count(d => d.Status == DroneStatus.Offline);

                return Ok(new
                {
                    Total = total,
                    Online = online,
                    Offline = offline,
                    OnlineRate = total > 0 ? (double)online / total * 100 : 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取集群状态失败");
                return StatusCode(500, new { error = "获取集群状态失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取无人机数据点
        /// </summary>
        [HttpGet("{id:Guid}/data")]
        public async Task<IActionResult> GetDroneData(
            Guid id,
            [FromQuery] DateTime startTime,
            [FromQuery] DateTime endTime)
        {
            try
            {
                var data = await _droneService.GetDroneDataAsync(id, startTime, endTime);
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取无人机数据失败: {DroneId}", id);
                return StatusCode(500, new { error = "获取无人机数据失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取所有无人机数据点
        /// </summary>
        [HttpGet("data/all")]
        public async Task<IActionResult> GetAllDronesData(
            [FromQuery] DateTime startTime,
            [FromQuery] DateTime endTime)
        {
            try
            {
                var data = await _droneService.GetAllDronesDataAsync(startTime, endTime);
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取所有无人机数据失败");
                return StatusCode(500, new { error = "获取所有无人机数据失败", message = ex.Message });
            }
        }

        #region 实时数据API（直接从数据层获取）

        /// <summary>
        /// 获取所有实时无人机数据
        /// </summary>
        [HttpGet("realtime")]
        public async Task<ActionResult<IEnumerable<Drone>>> GetRealTimeDrones()
        {
            try
            {
                var drones = await _droneService.GetRealTimeDronesAsync();
                
                _logger.LogInformation("获取实时无人机数据成功，数量: {Count}", drones.Count);
                
                return Ok(new
                {
                    Success = true,
                    Data = drones,
                    Count = drones.Count,
                    Timestamp = DateTime.UtcNow,
                    DataSource = "RealTime"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取实时无人机数据失败");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "获取实时无人机数据失败",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// 获取指定无人机的实时数据
        /// </summary>
        [HttpGet("realtime/{droneId:Guid}")]
        public async Task<ActionResult<Drone>> GetRealTimeDrone(Guid droneId)
        {
            try
            {
                var drone = await _droneService.GetRealTimeDroneAsync(droneId);
                
                if (drone == null)
                {
                    return NotFound(new
                    {
                        Success = false,
                        Message = $"未找到ID为 {droneId} 的无人机",
                        DroneId = droneId
                    });
                }

                _logger.LogInformation("获取无人机实时数据成功: {DroneName}", drone.Name);
                
                return Ok(new
                {
                    Success = true,
                    Data = drone,
                    Timestamp = DateTime.UtcNow,
                    DataSource = "RealTime"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取无人机实时数据失败: {DroneId}", droneId);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "获取无人机实时数据失败",
                    Error = ex.Message,
                    DroneId = droneId
                });
            }
        }

        /// <summary>
        /// 根据名称获取无人机实时数据
        /// </summary>
        [HttpGet("realtime/name/{droneName}")]
        public async Task<ActionResult<Drone>> GetRealTimeDroneByName(string droneName)
        {
            try
            {
                var drone = await _droneService.GetRealTimeDroneByNameAsync(droneName);
                
                if (drone == null)
                {
                    return NotFound(new
                    {
                        Success = false,
                        Message = $"未找到名称为 {droneName} 的无人机",
                        DroneName = droneName
                    });
                }

                _logger.LogInformation("根据名称获取无人机实时数据成功: {DroneName}", drone.Name);
                
                return Ok(new
                {
                    Success = true,
                    Data = drone,
                    Timestamp = DateTime.UtcNow,
                    DataSource = "RealTime"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "根据名称获取无人机实时数据失败: {DroneName}", droneName);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "根据名称获取无人机实时数据失败",
                    Error = ex.Message,
                    DroneName = droneName
                });
            }
        }

        /// <summary>
        /// 获取实时数据统计信息
        /// </summary>
        [HttpGet("realtime/statistics")]
        public ActionResult<RealTimeDataStatistics> GetRealTimeStatistics()
        {
            try
            {
                var statistics = _droneService.GetRealTimeStatistics();
                
                _logger.LogInformation("获取实时数据统计信息成功");
                
                return Ok(new
                {
                    Success = true,
                    Data = statistics,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取实时数据统计信息失败");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "获取实时数据统计信息失败",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// 检查实时数据新鲜度
        /// </summary>
        [HttpGet("realtime/freshness")]
        public ActionResult CheckRealTimeDataFreshness([FromQuery] int maxAgeSeconds = 30)
        {
            try
            {
                var maxAge = TimeSpan.FromSeconds(maxAgeSeconds);
                var isFresh = _droneService.IsRealTimeDataFresh(maxAge);
                var statistics = _droneService.GetRealTimeStatistics();
                
                return Ok(new
                {
                    Success = true,
                    Data = new
                    {
                        IsFresh = isFresh,
                        MaxAgeSeconds = maxAgeSeconds,
                        DataAgeSeconds = statistics.DataFreshnessSeconds.TotalSeconds,
                        LastUpdate = statistics.LastUpdate
                    },
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查实时数据新鲜度失败");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "检查实时数据新鲜度失败",
                    Error = ex.Message
                });
            }
        }

        #endregion

        /// <summary>
        /// 批量更新无人机
        /// </summary>
        [HttpPut("bulk")]
        public async Task<IActionResult> BulkUpdate([FromBody] IEnumerable<Drone> drones)
        {
            try
            {
                var result = await _droneService.BulkUpdateDronesAsync(drones);
                return result ? Ok(new { success = true, message = "批量更新成功" }) : BadRequest(new { error = "批量更新失败" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量更新无人机失败");
                return StatusCode(500, new { error = "批量更新失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取服务统计信息
        /// </summary>
        [HttpGet("statistics")]
        public IActionResult GetStatistics()
        {
            try
            {
                var statistics = _droneService.GetStatistics();
                return Ok(new { success = true, data = statistics });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取统计信息失败");
                return StatusCode(500, new { error = "获取统计信息失败", message = ex.Message });
            }
        }
    }
} 