using ClassLibrary_Core.Drone;
namespace ClassLibrary_Core.Data
{
    public class DroneDataRequest
    {
        public int model { get; set; } // 模型
        public Guid drone { get; set; }
        public DateTime? startTime { get; set; }
        public DateTime? endTime { get; set; }
        public string? taskId { get; set; }
        public TimeSpan timeSpan { get; set; }

    }
}
