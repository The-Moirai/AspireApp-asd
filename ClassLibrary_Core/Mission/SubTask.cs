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
        /// 获取所有图片的显示URL列表（优先使用数据库图片API）
        /// 如果内存中没有图片数据，返回空列表，前端应该调用API实时获取
        /// </summary>
        public List<string> GetAllImageUrls()
        {
            var urls = new List<string>();
            
            // 优先使用数据库存储的图片URL（通过API实时获取）
            if (Images?.Any() == true)
            {
                urls.AddRange(Images.OrderBy(img => img.ImageIndex).Select(img => img.GetImageUrl()));
            }
            // 如果内存中没有图片元数据，返回空列表，让前端通过API获取
            // 前端应该调用: GET /api/Tasks/subtask/{subTaskId}/images
            
            return urls;
        }

        /// <summary>
        /// 获取用于API调用的子任务图片列表URL
        /// </summary>
        public string GetImageListApiUrl()
        {
            return $"/api/ImageProxy/subtask/{Id}/images";
        }

        /// <summary>
        /// 获取用于API调用的子任务图片数量URL
        /// </summary>
        public string GetImageCountApiUrl()
        {
            return $"/api/ImageProxy/subtask/{Id}/images-count";
        }

        /// <summary>
        /// 获取图片总数（从数据库实时查询，不依赖内存缓存）
        /// 注意：这个方法应该异步调用API来获取准确的图片数量
        /// </summary>
        public int GetTotalImageCount()
        {
            // 如果内存中有图片元数据，直接返回
            if (Images?.Any() == true)
            {
                return Images.Count;
            }
            
            // 否则返回0，让前端通过API获取准确数量
            // 前端应该调用 /api/Tasks/subtask/{subTaskId}/images 来获取图片列表
            return 0;
        }
    }
}
