using ClassLibrary_Core.Drone;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary_Core.Data
{
    public class DroneStatusHistory
    {
        public long Id { get; set; }
        public Guid DroneId { get; set; }
        public DroneStatus Status { get; set; }
        public DateTime Timestamp { get; set; }
        public decimal? CpuUsage { get; set; }
        public decimal? BandwidthAvailable { get; set; }
        public decimal? MemoryUsage { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public decimal? BatteryLevel { get; set; }
        public byte? NetworkStrength { get; set; }
    }
}
