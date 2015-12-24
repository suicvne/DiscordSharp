using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordSharpRefactored
{
    public class DiscordUser
    {
        public string username { get; internal set; }
        public string id { get; internal set; }
        public string discriminator { get; internal set; }
        public string avatar { get; internal set; }
        public bool verified { get; internal set; }
        public string email { get; internal set; }

        public void Mention()
        {
            throw new NotImplementedException();
        }
    }
}
