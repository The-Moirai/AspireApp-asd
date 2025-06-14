using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary_Core.Mission
{
    public enum MissionStatus
    {
        /// <summary>
        /// 待定
        /// </summary>
        Pending,
        /// <summary>
        /// 进行中
        /// </summary>
        InProgress,
        /// <summary>
        /// 完成
        /// </summary>
        Completed,
        /// <summary>
        /// 取消
        /// </summary>
        Cancelled,
        /// <summary>
        /// 失败
        /// </summary>
        Failed
    }
}
