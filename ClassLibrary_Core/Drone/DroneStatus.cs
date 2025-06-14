using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary_Core.Drone
{
    public enum DroneStatus
    {
        /// <summary>
        /// 待机
        /// </summary>
        Idle,
        /// <summary>
        /// 任务中
        /// </summary>
        InMission,
        /// <summary>
        /// 返回
        /// </summary>
        Returning,
        /// <summary>
        /// 维修
        /// </summary>
        Maintenance,
        /// <summary>
        /// 离线
        /// </summary>
        Offline,
        /// <summary>
        /// 紧急
        /// </summary>
        Emergency
    }
}
