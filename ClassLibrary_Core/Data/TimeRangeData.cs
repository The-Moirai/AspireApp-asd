using ClassLibrary_Core.Drone;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary_Core.Data
{
    public class TimeRangeData
    {
        public string Name { get; set; } // 数据范围的名称或描述
        public int RecordCount { get; set; } // 数据点的数量
        public DateTime EarliestTime { get; set; } // 最早的数据点时间
        public DateTime LatestTime { get; set; } // 最晚的数据点时间
        public Dictionary<DroneStatus,int> StatusDistribution { get; set; } // 无人机状态列表
    }
}
