namespace ClassLibrary_Core.Mission
{
    public class MissionHistory
    {
        /// <summary>
        /// 子任务描述
        /// </summary>
        public string SubTaskDescription { get; set; }
        /// <summary>
        /// 子任务ID
        /// </summary>
        public Guid SubTaskId { get; set; }
        /// <summary>
        /// 子任务操作类型
        /// </summary>
        public string Operation { get; set; } // 如 "卸载", "重装", "分配", "完成"
        /// <summary>
        /// 子任务关联的无人机名称
        /// </summary>
        public string? DroneName { get; set; }
        /// <summary>
        /// 子任务操作时间
        /// </summary>
        public DateTime Time { get; set; }
    }
}
