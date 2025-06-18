using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary_Core.Data
{
    /// <summary>
    /// 表示任务的时间范围
    /// </summary>
    public class TaskTimeRange
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    /// <summary>
    /// 任务统计信息
    /// </summary>
    public class TaskStatistics
    {
        public int TotalMainTasks { get; set; }
        public int TotalSubTasks { get; set; }
        public int PendingMainTasks { get; set; }
        public int ActiveMainTasks { get; set; }
        public int CompletedMainTasks { get; set; }
        public int FailedMainTasks { get; set; }
        public int PendingSubTasks { get; set; }
        public int ActiveSubTasks { get; set; }
        public int CompletedSubTasks { get; set; }
        public int FailedSubTasks { get; set; }

        /// <summary>
        /// 主任务完成率百分比
        /// </summary>
        public double MainTaskCompletionRate =>
            TotalMainTasks > 0 ? (double)CompletedMainTasks / TotalMainTasks * 100 : 0;

        /// <summary>
        /// 子任务完成率百分比
        /// </summary>
        public double SubTaskCompletionRate =>
            TotalSubTasks > 0 ? (double)CompletedSubTasks / TotalSubTasks * 100 : 0;

        /// <summary>
        /// 主任务失败率百分比
        /// </summary>
        public double MainTaskFailureRate =>
            TotalMainTasks > 0 ? (double)FailedMainTasks / TotalMainTasks * 100 : 0;

        /// <summary>
        /// 子任务失败率百分比
        /// </summary>
        public double SubTaskFailureRate =>
            TotalSubTasks > 0 ? (double)FailedSubTasks / TotalSubTasks * 100 : 0;
    }

    /// <summary>
    /// 任务性能分析
    /// </summary>
    public class TaskPerformanceAnalysis
    {
        public double AverageExecutionTimeMinutes { get; set; }
        public double MinExecutionTimeMinutes { get; set; }
        public double MaxExecutionTimeMinutes { get; set; }
        public int TotalCompletedTasks { get; set; }

        /// <summary>
        /// 执行效率评级（基于平均执行时间）
        /// </summary>
        public string EfficiencyRating
        {
            get
            {
                if (AverageExecutionTimeMinutes <= 5) return "优秀";
                if (AverageExecutionTimeMinutes <= 15) return "良好";
                if (AverageExecutionTimeMinutes <= 30) return "一般";
                return "需要优化";
            }
        }

        /// <summary>
        /// 时间跨度（最大-最小执行时间）
        /// </summary>
        public double ExecutionTimeRange => MaxExecutionTimeMinutes - MinExecutionTimeMinutes;
    }
}
