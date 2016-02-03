using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordSharp.Events
{
    public class DiscordGuildMemberAddEventArgs : EventArgs
    {
        public DiscordMember AddedMember { get; internal set; }
        public DiscordServer Guild { get; internal set; }
        public DateTime JoinedAt { get; internal set; }
        public string[] roles { get; internal set; }
        public JObject RawJson { get; internal set; }
    }
}
