using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary_Core.Data
{
    public class SubTaskHistory
    {
        public long Id { get; set; }
        public Guid SubTaskId { get; set; }
        public TaskStatus? OldStatus { get; set; }
        public TaskStatus NewStatus { get; set; }
        public DateTime ChangeTime { get; set; }
        public string ChangedBy { get; set; }
        public Guid? DroneId { get; set; }
        public string Reason { get; set; }
        public string AdditionalInfo { get; set; }
    }
}
