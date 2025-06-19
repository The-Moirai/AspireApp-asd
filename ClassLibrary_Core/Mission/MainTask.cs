namespace ClassLibrary_Core.Mission
{
    public class MainTask
    {
        /// <summary>
        /// 大任务的唯一标识符
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();
        /// <summary>
        /// 大任务的描述信息
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// 大任务的状态
        /// </summary>
        public System.Threading.Tasks.TaskStatus Status { get; set; }
        /// <summary>
        /// 大任务的创建时间
        /// </summary>
        public DateTime CreationTime { get; set; } = DateTime.Now;
        /// <summary>
        /// 大任务的完成时间
        /// </summary>
        public DateTime? CompletedTime { get; set; }
        /// <summary>
        /// 大任务的子任务列表
        /// </summary>
        public List<SubTask> SubTasks { get; set; } = new();
    }
}
