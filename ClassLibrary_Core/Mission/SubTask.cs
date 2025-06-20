using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClassLibrary_Core.Data;

namespace ClassLibrary_Core.Mission
{
    public class SubTask
    {
        /// <summary>
        /// 子任务的唯一标识符
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();
        /// <summary>
        /// 子任务的描述信息
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// 子任务的状态
        /// </summary>
        public TaskStatus Status { get; set; }
        /// <summary>
        /// 子任务的创建时间
        /// </summary>
        public DateTime CreationTime { get; set; } = DateTime.Now;
        /// <summary>
        /// 子任务的开始时间
        /// </summary>
        public DateTime? AssignedTime { get; set; }
        /// <summary>
        /// 子任务的完成时间
        /// </summary>
        public DateTime? CompletedTime { get; set; }
        /// <summary>
        /// 子任务所属的大任务
        /// </summary>
        public Guid ParentTask { get; set; }
        /// <summary>
        /// 子任务重分配次数
        /// </summary>
        public int ReassignmentCount { get; set; } = 0;
        /// <summary>
        /// 子任务分配的无人机
        /// </summary>
        public string AssignedDrone { get; set; }
        /// <summary>
        /// 子任务处理结果图片路径列表（向后兼容）
        /// </summary>
        public List<string> ImagePaths { get; set; } = new List<string>();
        
        /// <summary>
        /// 子任务处理结果图片数据列表（存储在数据库中）
        /// </summary>
        public List<SubTaskImage> Images { get; set; } = new List<SubTaskImage>();

        /// <summary>
        /// 获取图片总数（包含文件路径和数据库存储的图片）
        /// </summary>
        public int GetTotalImageCount()
        {
            return (ImagePaths?.Count ?? 0) + (Images?.Count ?? 0);
        }

        /// <summary>
        /// 获取所有图片的显示URL列表
        /// </summary>
        public List<string> GetAllImageUrls()
        {
            var urls = new List<string>();
            
            // 添加文件路径（向后兼容）
            if (ImagePaths?.Any() == true)
            {
                urls.AddRange(ImagePaths);
            }
            
            // 添加数据库存储的图片URL
            if (Images?.Any() == true)
            {
                urls.AddRange(Images.OrderBy(img => img.ImageIndex).Select(img => img.GetImageUrl()));
            }
            
            return urls;
        }
    }
}
