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
        public string? DroneName { get; set; } // 无人机名称
        public DroneStatus? Status { get; set; } // 无人机状态
        public DateTime Timestamp { get; set; } = DateTime.Now; // 数据点的时间戳  
        public decimal cpuUsage { get; set; } // CPU 使用率
        public decimal bandwidthUsage { get; set; } // 带宽使用率
        public decimal memoryUsage { get; set; } // 内存使用率
        public decimal Latitude { get; set; } // 纬度
        public decimal Longitude { get; set; } // 经度
        public decimal BatteryLevel { get; set; } // 电池电量百分比
        public decimal Altitude { get; set; } // 海拔高度
        public decimal Speed { get; set; } // 飞行速度
        public string? CurrentTask { get; set; } // 当前执行的任务
        public Dictionary<string, object> AdditionalData { get; set; } = new(); // 额外数据

    }
}
