using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordSharpRefactored.Models
{
    public class DiscordServer
    {
        public string id { get; set; }
        public string name { get; set; }
        public string owner_id { get; set; }
        public List<DiscordChannel> channels { get; set; }
        public List<DiscordUser> members { get; set; }

        public DiscordServer()
        {
            channels = new List<DiscordChannel>();
            members = new List<DiscordUser>();
        }
    }
}
