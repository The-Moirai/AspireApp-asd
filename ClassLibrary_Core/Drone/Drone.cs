using ClassLibrary_Core.Mission;
using ClassLibrary_Core.Common;
namespace ClassLibrary_Core.Drone
{
    public class Drone 
    {
        /// <summary>
        /// 无人机的唯一标识符
        /// </summary>
        public Guid Id { get; set; }
        /// <summary>
        /// 无人机的名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 无人机类型
        /// </summary>
        public ModelStatus ModelStatus { get; set; }
        /// <summary>
        /// 无人机型号名称 (用于显示)
        /// </summary>
        public string ModelType { get; set; } = "";
        /// <summary>
        /// 无人机的位置
        /// </summary>
        public GPSPosition? CurrentPosition { get; set; }
        /// <summary>
        /// 无人机的状态
        /// </summary>
        public DroneStatus Status { get; set; } = DroneStatus.Idle;
        /// <summary>
        /// 无人机的 CPU 使用率
        /// </summary>
        public double cpu_used_rate { get; set; } = 0; // 默认值为 0
        /// <summary>
        /// 半径
        /// </summary>
        public double radius { get; set; } =1000;
        /// <summary>
        /// 带宽
        /// </summary>
        public double left_bandwidth { get; set; } = 500;
        /// <summary>
        /// 内存
        /// </summary>
        public double memory { get; set; }
        /// <summary>
        /// 无人机的邻接表
        /// </summary>
        public List<Guid> ConnectedDroneIds { get; set; } = new();
        /// <summary>
        /// 无人机的子任务列表
        /// </summary>
        public List<SubTask> AssignedSubTasks { get; set; } = new();
    }
}
