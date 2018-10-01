using DiscordSharp.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordSharp.Events
{
    public class DiscordGuildMembersLoadedEventArgs : EventArgs
    {
        public DiscordServer Server { get; internal set; }
        public int MemberSize { get; internal set; }
    }
}
