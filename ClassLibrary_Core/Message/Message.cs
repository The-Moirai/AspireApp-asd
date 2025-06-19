using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace ClassLibrary_Core.Message
{
    public class Message
    {
        public string type { get; set; }
        public object content { get; set; }  // 改为object类型，可以接受JsonElement或其他类型
        public string? next_node { get; set; }
    }
}
