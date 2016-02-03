using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordSharp.Events
{
    public class DiscordChannelUpdateEventArgs : EventArgs
    {
        public JObject RawJson { get; internal set; }
        public DiscordChannel OldChannel { get; internal set; }
        public DiscordChannel NewChannel { get; internal set; }
    }
}
