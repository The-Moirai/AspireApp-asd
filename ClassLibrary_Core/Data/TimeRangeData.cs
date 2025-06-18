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
        public string Name { get; set; } = ""; // 数据范围的名称或描述
        public string Type { get; set; } = ""; // 数据类型 (Drone, Task, etc.)
        public int RecordCount { get; set; } // 数据点的数量
        public DateTime StartTime { get; set; } // 查询开始时间
        public DateTime EndTime { get; set; } // 查询结束时间
        public DateTime EarliestTime { get; set; } // 最早的数据点时间
        public DateTime LatestTime { get; set; } // 最晚的数据点时间
        public Dictionary<string, int> StatusDistribution { get; set; } = new(); // 状态分布
        public decimal? AverageCpuUsage { get; set; } // 平均CPU使用率 (仅无人机数据)
        public decimal? AverageMemoryUsage { get; set; } // 平均内存使用率 (仅无人机数据)
        public decimal? MinValue { get; set; } // 最小值
        public decimal? MaxValue { get; set; } // 最大值
        public List<string> Tags { get; set; } = new(); // 标签或分类信息
    }
}
