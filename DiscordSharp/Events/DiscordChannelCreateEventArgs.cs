using System;
using DiscordSharp.Objects;
namespace DiscordSharp
{
    public enum DiscordChannelCreateType
    {
        PRIVATE, CHANNEL
    }
    public class DiscordChannelCreateEventArgs : EventArgs
    {
        public DiscordChannelCreateType ChannelType { get; set; }
        public DiscordChannel ChannelCreated { get; set; }
    }
}