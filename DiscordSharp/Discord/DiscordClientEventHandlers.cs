using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiscordSharp.Events;

namespace DiscordSharp.Discord
{
    public partial class DiscordClient
    {
        #region Event declaration

        public event EventHandler<DiscordMessageEventArgs> MessageReceived;

        public event EventHandler<DiscordConnectEventArgs> Connected;

        public event EventHandler<EventArgs> SocketOpened;

        public event EventHandler<DiscordSocketClosedEventArgs> SocketClosed;

        public event EventHandler<DiscordChannelCreateEventArgs> ChannelCreated;

        public event EventHandler<DiscordPrivateChannelEventArgs> PrivateChannelCreated;

        public event EventHandler<DiscordPrivateMessageEventArgs> PrivateMessageReceived;

        public event EventHandler<DiscordKeepAliveSentEventArgs> KeepAliveSent;

        public event EventHandler<DiscordMessageEventArgs> MentionReceived;

        public event EventHandler<DiscordTypingStartEventArgs> UserTypingStart;

        public event EventHandler<DiscordMessageEditedEventArgs> MessageEdited;

        public event EventHandler<DiscordPresenceUpdateEventArgs> PresenceUpdated;

        public event EventHandler<DiscordURLUpdateEventArgs> URLMessageAutoUpdate;

        public event EventHandler<DiscordVoiceStateUpdateEventArgs> VoiceStateUpdate;

        public event EventHandler<UnknownMessageEventArgs> UnknownMessageTypeReceived;

        public event EventHandler<DiscordMessageDeletedEventArgs> MessageDeleted;

        public event EventHandler<DiscordUserUpdateEventArgs> UserUpdate;

        public event EventHandler<DiscordGuildMemberAddEventArgs> UserAddedToServer;

        public event EventHandler<DiscordGuildMemberRemovedEventArgs> UserRemovedFromServer;

        public event EventHandler<DiscordGuildCreateEventArgs> GuildCreated;

        /// <summary>
        /// Occurs when a guild becomes available after being unavailable.
        /// </summary>
        public event EventHandler<DiscordGuildCreateEventArgs> GuildAvailable;

        public event EventHandler<DiscordGuildDeleteEventArgs> GuildDeleted;

        public event EventHandler<DiscordChannelUpdateEventArgs> ChannelUpdated;

        public event EventHandler<LoggerMessageReceivedArgs> TextClientDebugMessageReceived;

        public event EventHandler<LoggerMessageReceivedArgs> VoiceClientDebugMessageReceived;

        public event EventHandler<DiscordChannelDeleteEventArgs> ChannelDeleted;

        public event EventHandler<DiscordServerUpdateEventArgs> GuildUpdated;

        public event EventHandler<DiscordGuildRoleDeleteEventArgs> RoleDeleted;

        public event EventHandler<DiscordGuildRoleUpdateEventArgs> RoleUpdated;

        public event EventHandler<DiscordGuildMemberUpdateEventArgs> GuildMemberUpdated;

        public event EventHandler<DiscordGuildBanEventArgs> GuildMemberBanned;

        public event EventHandler<DiscordPrivateChannelDeleteEventArgs> PrivateChannelDeleted;

        public event EventHandler<DiscordBanRemovedEventArgs> BanRemoved;

        public event EventHandler<DiscordPrivateMessageDeletedEventArgs> PrivateMessageDeleted;

        public event EventHandler<DiscordPrivateMessageEventArgs> PrivateMessageUpdate;

        #region Voice

        /// <summary>
        /// For use when connected to voice only.
        /// </summary>
        public event EventHandler<DiscordAudioPacketEventArgs> AudioPacketReceived;

        /// <summary>
        /// For use when connected to voice only.
        /// </summary>
        public event EventHandler<DiscordVoiceUserSpeakingEventArgs> UserSpeaking;

        /// <summary>
        /// For use when connected to voice only.
        /// </summary>
        public event EventHandler<DiscordLeftVoiceChannelEventArgs> UserLeftVoiceChannel;

        /// <summary>
        /// Occurs when the voice client is fully connected to voice.
        /// </summary>
        public event EventHandler<EventArgs> VoiceClientConnected;

        /// <summary>
        /// Occurs when the voice queue is emptied.
        /// </summary>
        public event EventHandler<EventArgs> VoiceQueueEmpty;

        #endregion Voice

        #endregion Event declaration
    }
}