using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary_Core.Message
{
    public class Message
    {
        public string type { get; set; }
        public Dictionary<string, List<object>> content { get; set; }
        public string? next_node { get; set; }
    }
}
