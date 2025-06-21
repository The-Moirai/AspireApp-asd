using System;

namespace ClassLibrary_Core.Data
{
    /// <summary>
    /// 图片保存完成事件参数
    /// </summary>
    public class ImageSavedEventArgs : EventArgs
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public Guid TaskId { get; set; }

        /// <summary>
        /// 子任务ID
        /// </summary>
        public Guid SubTaskId { get; set; }

        /// <summary>
        /// 图片ID（数据库主键）
        /// </summary>
        public Guid ImageId { get; set; }

        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; } = "";

        /// <summary>
        /// 图片序号
        /// </summary>
        public int ImageIndex { get; set; }

        /// <summary>
        /// 文件大小
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 保存时间
        /// </summary>
        public DateTime SavedAt { get; set; }

        /// <summary>
        /// Web访问路径
        /// </summary>
        public string WebPath { get; set; } = "";

        /// <summary>
        /// 子任务名称
        /// </summary>
        public string SubTaskName { get; set; } = "";
    }
} 