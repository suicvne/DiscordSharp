using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordSharp.Objects
{
    /// <summary>
    /// The type of message that the Discord message is.
    /// </summary>
    public enum DiscordMessageType
    {
        /// <summary>
        /// Private/DM
        /// </summary>
        PRIVATE,

        /// <summary>
        /// Public/in a channel.
        /// </summary>
        CHANNEL
    }
}