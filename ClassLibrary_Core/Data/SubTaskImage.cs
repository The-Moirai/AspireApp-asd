using System;

namespace ClassLibrary_Core.Data
{
    /// <summary>
    /// 子任务图片数据模型
    /// </summary>
    public class SubTaskImage
    {
        /// <summary>
        /// 图片唯一标识符
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 所属子任务ID
        /// </summary>
        public Guid SubTaskId { get; set; }

        /// <summary>
        /// 图片二进制数据
        /// </summary>
        public byte[] ImageData { get; set; } = new byte[0];

        /// <summary>
        /// 原始文件名
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// 文件扩展名
        /// </summary>
        public string FileExtension { get; set; } = string.Empty;

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// MIME内容类型
        /// </summary>
        public string ContentType { get; set; } = "image/png";

        /// <summary>
        /// 图片序号（用于多张图片排序）
        /// </summary>
        public int ImageIndex { get; set; } = 1;

        /// <summary>
        /// 上传时间
        /// </summary>
        public DateTime UploadTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 图片描述
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 生成Web访问URL（用于前端显示）
        /// </summary>
        /// <returns>图片访问URL</returns>
        public string GetImageUrl()
        {
            return $"/api/ImageProxy/view/{Id}";
        }

        /// <summary>
        /// 生成图片查看URL（不触发下载）
        /// </summary>
        /// <returns>图片查看URL</returns>
        public string GetViewUrl()
        {
            return $"/api/ImageProxy/view/{Id}";
        }

        /// <summary>
        /// 生成图片下载URL（强制下载）
        /// </summary>
        /// <returns>图片下载URL</returns>
        public string GetDownloadUrl()
        {
            return $"/api/ImageProxy/download/{Id}";
        }

        /// <summary>
        /// 生成缩略图URL
        /// </summary>
        /// <returns>缩略图URL</returns>
        public string GetThumbnailUrl()
        {
            return $"/api/ImageProxy/thumbnail/{Id}";
        }

        /// <summary>
        /// 获取格式化的文件大小
        /// </summary>
        /// <returns>格式化的文件大小字符串</returns>
        public string GetFormattedFileSize()
        {
            const int unit = 1024;
            if (FileSize < unit) return $"{FileSize} B";
            int exp = (int)(Math.Log(FileSize) / Math.Log(unit));
            return $"{FileSize / Math.Pow(unit, exp):F2} {("KMGTPE")[exp - 1]}B";
        }
    }
} 