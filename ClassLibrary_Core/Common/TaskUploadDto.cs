using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary_Core.Common
{
    public class TaskUploadDto
    {
        public Guid Id { get; set; }
        public string Description { get; set; }
        public DateTime CreationTime { get; set; }
        public string FileName { get; set; }
        public byte[] FileContent { get; set; }
        public DateTime UploadTime { get; set; }
        public string? VideoFileName { get; set; }
        public string? VideoFilePath { get; set; }
    }
}
