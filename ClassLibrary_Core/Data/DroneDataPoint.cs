using ClassLibrary_Core.Common;
using ClassLibrary_Core.Drone;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary_Core.Data
{
    public class DroneDataPoint
    {
        public Guid? DroneId { get; set; } // 无人机的唯一标识符
        public DroneStatus? Status { get; set; } // 无人机状态
        public DateTime? Timestamp { get; set; } // 数据点的时间戳  
        public double? cpuUsage { get; set; } // CPU 使用率
        public double? bandwidthUsage { get; set; } // 带宽使用率
        public double? memoryUsage { get; set; } // 内存使用率
        public double? Latitude { get; set; } // 纬度
        public double? Longitude { get; set; } // 经度

    }
}
