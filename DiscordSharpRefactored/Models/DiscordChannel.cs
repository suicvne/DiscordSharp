using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordSharpRefactored.Models
{
    public class DiscordChannel
    {
        public string type { get; set; }
        public string name { get; set; }
        public string id { get; set; }
        public string topic { get; set; }
        public bool is_private { get; set; } = false;
    }
}
