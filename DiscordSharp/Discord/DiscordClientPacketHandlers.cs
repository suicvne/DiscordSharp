using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiscordSharp.Events;
using DiscordSharp.Objects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DiscordSharp.Discord
{
    public partial class DiscordClient
    {
        private void ClientPacketReceived(JObject message)
        {
            switch (message["t"].ToString())
            {
                case "READY":
                    Sequence = message["s"].ToObject<int>();
                    DiscordGatewayVersion = message["d"]["v"].ToObject<int>();
                    HeartbeatInterval = message["d"]["heartbeat_interval"].ToObject<int>();
                    BeginHeartbeatTask();
                    if (WriteLatestReady)
                        using (var sw = new StreamWriter("READY_LATEST.txt"))
                        {
                            sw.Write(message);
                        }
                    Me = JsonConvert.DeserializeObject<DiscordMember>(message["d"]["user"].ToString());
                    Me.parentclient = this;
                    IsBotAccount = message["d"]["user"]["bot"].IsNullOrEmpty()
                        ? false
                        : message["d"]["user"]["bot"].ToObject<bool>();
                    ClientPrivateInformation.Avatar = Me.Avatar;
                    ClientPrivateInformation.Username = Me.Username;
                    GetChannelsList(message);
                    SessionID = message["d"]["session_id"].ToString();

                    //TESTING
                    var guildID = new string[ServersList.Count];
                    for (var i = 0; i < guildID.Length; i++)
                        guildID[i] = ServersList[i].ID;

                    if (RequestAllUsersOnStartup)
                    {
                        var wsChunkTest = JsonConvert.SerializeObject(new
                        {
                            op = 8,
                            d = new
                            {
                                guild_id = guildID,
                                query = "",
                                limit = 0
                            }
                        });
                        ws.Send(wsChunkTest);
                    }

                    ReadyComplete = true;

                    Task.Run(async () =>
                    {
                        await Task.Delay(3000);
                        Connected?.Invoke(this, new DiscordConnectEventArgs { User = Me });
                    }); //fire and forget waiting of up to 3 seconds for guilds to become available.
                    break;

                case "GUILD_MEMBERS_CHUNK":
                    GuildMemberChunkEvents(message);
                    break;

                case "GUILD_MEMBER_REMOVE":
                    GuildMemberRemoveEvents(message);
                    break;

                case "GUILD_MEMBER_ADD":
                    GuildMemberAddEvents(message);
                    break;

                case "GUILD_DELETE":
                    GuildDeleteEvents(message);
                    break;

                case "GUILD_CREATE":
                    GuildCreateEvents(message);
                    break;

                case "GUILD_MEMBER_UPDATE":
                    GuildMemberUpdateEvents(message);
                    break;

                case "GUILD_UPDATE":
                    GuildUpdateEvents(message);
                    break;

                case "GUILD_ROLE_DELETE":
                    GuildRoleDeleteEvents(message);
                    break;

                case "GUILD_ROLE_UPDATE":
                    GuildRoleUpdateEvents(message);
                    break;

                case "PRESENCE_UPDATE":
                    PresenceUpdateEvents(message);
                    break;

                case "MESSAGE_UPDATE":
                    MessageUpdateEvents(message);
                    break;

                case "TYPING_START":
                    var server = ServersList.Find(
                        x => x.Channels.Find(y => y.ID == message["d"]["channel_id"].ToString()) != null);
                    if (server != null)
                    {
                        var channel = server.Channels.Find(x => x.ID == message["d"]["channel_id"].ToString());
                        var uuser = server.GetMemberByKey(message["d"]["user_id"].ToString());
                        if (UserTypingStart != null)
                            UserTypingStart(this,
                                new DiscordTypingStartEventArgs
                                {
                                    user = uuser,
                                    Channel = channel,
                                    Timestamp = int.Parse(message["d"]["timestamp"].ToString())
                                });
                    }
                    break;

                case "MESSAGE_CREATE":
                    MessageCreateEvents(message);
                    break;

                case "CHANNEL_CREATE":
                    ChannelCreateEvents(message);
                    break;

                case "VOICE_STATE_UPDATE":
                    VoiceStateUpdateEvents(message);
                    break;

                case "VOICE_SERVER_UPDATE":
                    VoiceServerUpdateEvents(message);
                    break;

                case "MESSAGE_DELETE":
                    MessageDeletedEvents(message);
                    break;

                case "USER_UPDATE":
                    UserUpdateEvents(message);
                    break;

                case "CHANNEL_UPDATE":
                    ChannelUpdateEvents(message);
                    break;

                case "CHANNEL_DELETE":
                    ChannelDeleteEvents(message);
                    break;

                case "GUILD_BAN_ADD":
                    GuildMemberBannedEvents(message);
                    break;

                case "GUILD_BAN_REMOVE":
                    GuildMemberBanRemovedEvents(message);
                    break;

                case "MESSAGE_ACK": //ignore this message, it's irrelevant
                    break;

                default:
                    if (UnknownMessageTypeReceived != null)
                        UnknownMessageTypeReceived(this, new UnknownMessageEventArgs { RawJson = message });
                    break;
            }
        }

        #region GuildMember

        private void GuildMemberAddEvents(JObject message)
        {
            DiscordGuildMemberAddEventArgs e = new DiscordGuildMemberAddEventArgs();
            e.RawJson = message;
            e.Guild = ServersList.Find(x => x.ID == message["d"]["guild_id"].ToString());

            DiscordMember existingMember = e.Guild.GetMemberByKey(message["d"]["user"]["id"].ToString());
            if (existingMember != null)
            {
                DiscordMember newMember = JsonConvert.DeserializeObject<DiscordMember>(message["d"]["user"].ToString());
                newMember.parentclient = this;
                e.AddedMember = newMember;
                newMember.Parent = e.Guild;
                e.Roles = message["d"]["roles"].ToObject<string[]>();
                e.JoinedAt = DateTime.Parse(message["d"]["joined_at"].ToString());

                ServersList.Find(x => x == e.Guild).AddMember(newMember);
                if (UserAddedToServer != null)
                    UserAddedToServer(this, e);
            }
            else
            {
                GetTextClientLogger.Log($"Skipping guild member add because user already exists in server.", MessageLevel.Debug);
            }
        }

        private void GuildMemberUpdateEvents(JObject message)
        {
            DiscordServer server = ServersList.Find(x => x.ID == message["d"]["guild_id"].ToString());

            DiscordMember memberUpdated = server.GetMemberByKey(message["d"]["user"]["id"].ToString());
            if (memberUpdated != null)
            {
                memberUpdated.Username = message["d"]["user"]["username"].ToString();
                if (message["d"]["nick"] != null)
                {
                    if (message["d"]["nick"].ToString() == null)
                        memberUpdated.Nickname = ""; //No nickname
                    else
                        memberUpdated.Nickname = message["d"]["nick"].ToString();
                }

                if (!message["d"]["user"]["avatar"].IsNullOrEmpty())
                    memberUpdated.Avatar = message["d"]["user"]["avatar"].ToString();
                memberUpdated.Discriminator = message["d"]["user"]["discriminator"].ToString();
                memberUpdated.ID = message["d"]["user"]["id"].ToString();

                foreach (var roles in message["d"]["roles"])
                {
                    memberUpdated.Roles.Add(server.Roles.Find(x => x.ID == roles.ToString()));
                }

                server.AddMember(memberUpdated);
                GuildMemberUpdated?.Invoke(this, new DiscordGuildMemberUpdateEventArgs { MemberUpdate = memberUpdated, RawJson = message, ServerUpdated = server });
            }
            else
            {
                GetTextClientLogger.Log("memberUpdated was null?!?!?!", MessageLevel.Debug);
            }
        }

        private void GuildMemberChunkEvents(JObject message)
        {
            if (!message["d"]["members"].IsNullOrEmpty())
            {
                DiscordServer inServer = ServersList.Find(x => x.ID == message["d"]["guild_id"].ToString());
                JArray membersAsArray = JArray.Parse(message["d"]["members"].ToString());
                foreach (var member in membersAsArray)
                {
                    //if (GuildHasMemberWithID(inServer, member["user"]["id"].ToString()))
                    //    continue;
                    DiscordMember _member = JsonConvert.DeserializeObject<DiscordMember>(member["user"].ToString());
                    if (!member["user"]["roles"].IsNullOrEmpty())
                    {
                        JArray rollsArray = JArray.Parse(member["user"]["roles"].ToString());
                        if (rollsArray.Count > 0)
                        {
                            foreach (var rollID in rollsArray)
                                _member.Roles.Add(inServer.Roles.Find(x => x.ID == rollID.ToString()));
                        }
                    }
                    _member.Muted = member["mute"].ToObject<bool>();
                    _member.Deaf = member["deaf"].ToObject<bool>();
                    _member.Roles.Add(inServer.Roles.Find(x => x.Name == "@everyone"));
                    _member.Status = Status.Offline;
                    _member.parentclient = this;
                    _member.Parent = inServer;
                    inServer.AddMember(_member);

                    ///Check private channels
                    DiscordPrivateChannel _channel = PrivateChannels.Find(x => x.user_id == _member.ID);
                    if (_channel != null)
                    {
                        GetTextClientLogger.Log("Found user for private channel!", MessageLevel.Debug);
                        _channel.Recipient = _member;
                    }
                }
            }
        }

        private void GuildMemberRemoveEvents(JObject message)
        {
            DiscordGuildMemberRemovedEventArgs e = new DiscordGuildMemberRemovedEventArgs();
            DiscordMember removed = new DiscordMember(this);
            removed.parentclient = this;

            List<DiscordMember> membersToRemove = new List<DiscordMember>();
            foreach (var server in ServersList)
            {
                if (server.ID != message["d"]["guild_id"].ToString())
                    continue;
                foreach (var member in server.Members)
                {
                    if (member.Value.ID == message["d"]["user"]["id"].ToString())
                    {
                        removed = member.Value;
                        membersToRemove.Add(removed);
                        RemovedMembers.Add(removed);
                    }
                }
            }

            foreach (var member in membersToRemove)
            {
                foreach (var server in ServersList)
                {
                    try
                    {
                        server.RemoveMember(member.ID);
                    }
                    catch { } //oh, you mean useless?
                }
            }
            e.MemberRemoved = removed;
            e.Server = ServersList.Find(x => x.ID == message["d"]["guild_id"].ToString());
            e.RawJson = message;

            if (UserRemovedFromServer != null)
                UserRemovedFromServer(this, e);
        }

        private void GuildMemberBannedEvents(JObject message)
        {
            DiscordGuildBanEventArgs e = new DiscordGuildBanEventArgs();
            e.Server = ServersList.Find(x => x.ID == message["d"]["guild_id"].ToString());
            if (e.Server != null)
            {
                e.MemberBanned = e.Server.GetMemberByKey(message["d"]["user"]["id"].ToString());
                if (e.MemberBanned != null)
                {
                    if (GuildMemberBanned != null)
                        GuildMemberBanned(this, e);
                    ServersList.Find(x => x.ID == e.Server.ID).RemoveMember(e.MemberBanned.ID);
                }
                else
                {
                    GetTextClientLogger.Log("Error in GuildMemberBannedEvents: MemberBanned is null, attempting internal index of removed members.", MessageLevel.Error);
                    e.MemberBanned = RemovedMembers.Find(x => x.ID == message["d"]["user"]["id"].ToString());
                    if (e.MemberBanned != null)
                    {
                        if (GuildMemberBanned != null)
                            GuildMemberBanned(this, e);
                    }
                    else
                    {
                        GetTextClientLogger.Log("Error in GuildMemberBannedEvents: MemberBanned is null, not even found in internal index!", MessageLevel.Error);
                    }
                }
            }
            else
            {
                GetTextClientLogger.Log("Error in GuildMemberBannedEvents: Server is null?!", MessageLevel.Error);
            }
        }

        private void GuildMemberBanRemovedEvents(JObject message)
        {
            DiscordBanRemovedEventArgs e = new DiscordBanRemovedEventArgs();

            e.Guild = ServersList.Find(x => x.ID == message["d"]["guild_id"].ToString());
            e.MemberStub = JsonConvert.DeserializeObject<DiscordMember>(message["d"]["user"].ToString());

            if (BanRemoved != null)
                BanRemoved(this, e);
        }

        #endregion GuildMember

        private void VoiceServerUpdateEvents(JObject message)
        {
            // TODO VoiceClient is null when disconnecting from voice
            if (VoiceClient == null)
            {
                return;
            }
            VoiceClient.VoiceEndpoint = message["d"]["endpoint"].ToString();
            VoiceClient.Token = message["d"]["token"].ToString();

            VoiceClient.Guild = ServersList.Find(x => x.ID == message["d"]["guild_id"].ToString());
            VoiceClient.Me = Me;

            VoiceClient.PacketReceived += (sender, e) =>
            {
                AudioPacketReceived?.Invoke(sender, e);
            };

            VoiceClient.DebugMessageReceived += (sender, e) =>
            {
                if (VoiceClientDebugMessageReceived != null)
                    VoiceClientDebugMessageReceived(this, e);
            };

            ConnectToVoiceAsync();
        }

        private void VoiceStateUpdateEvents(JObject message)
        {
            var f = message["d"]["channel_id"];
            if (f.ToString() == null)
            {
                var le = new DiscordLeftVoiceChannelEventArgs();
                var inServer = ServersList.Find(x => x.ID == message["d"]["guild_id"].ToString());
                le.User = inServer.GetMemberByKey(message["d"]["user_id"].ToString());
                le.Guild = inServer;
                le.RawJson = message;

                if (VoiceClient != null && VoiceClient.Connected)
                    VoiceClient.MemberRemoved(le.User);
                if (UserLeftVoiceChannel != null)
                    UserLeftVoiceChannel(this, le);
                return;
            }
            var e = new DiscordVoiceStateUpdateEventArgs();
            e.Guild = ServersList.Find(x => x.ID == message["d"]["guild_id"].ToString());
            var memberToUpdate = e.Guild.GetMemberByKey(message["d"]["user_id"].ToString());
            if (memberToUpdate != null)
            {
                e.Channel = e.Guild.Channels.Find(x => x.ID == message["d"]["channel_id"].ToString());
                memberToUpdate.CurrentVoiceChannel = e.Channel;
                if (!message["d"]["self_deaf"].IsNullOrEmpty())
                    e.SelfDeaf = message["d"]["self_deaf"].ToObject<bool>();
                e.Deaf = message["d"]["deaf"].ToObject<bool>();
                if (!message["d"]["self_mute"].IsNullOrEmpty())
                    e.SelfMute = message["d"]["self_mute"].ToObject<bool>();
                e.Mute = message["d"]["mute"].ToObject<bool>();
                memberToUpdate.Muted = e.Mute;
                e.Suppress = message["d"]["suppress"].ToObject<bool>();
                memberToUpdate.Deaf = e.Suppress;
                e.RawJson = message;

                e.User = memberToUpdate;

                if (VoiceClient != null && VoiceClient.Connected)
                    VoiceClient.MemberAdded(e.User);

                if (!message["d"]["session_id"].IsNullOrEmpty()) //then this has to do with you
                    if (e.User.ID == Me.ID)
                    {
                        Me.Muted = e.SelfMute;
                        Me.Deaf = e.SelfDeaf;
                        if (VoiceClient != null)
                            VoiceClient.SessionID = message["d"]["session_id"].ToString();
                    }

                if (VoiceStateUpdate != null)
                    VoiceStateUpdate(this, e);
            }
        }

        private void GuildCreateEvents(JObject message)
        {
            var e = new DiscordGuildCreateEventArgs();
            e.RawJson = message;
            var server = new DiscordServer();
            server.JoinedAt = message["d"]["joined_at"].ToObject<DateTime>();
            server.parentclient = this;
            server.ID = message["d"]["id"].ToString();
            server.Name = message["d"]["name"].ToString();
            server.Members = new Dictionary<string, DiscordMember>();
            server.Channels = new List<DiscordChannel>();
            server.Roles = new List<DiscordRole>();
            foreach (var roll in message["d"]["roles"])
            {
                var t = new DiscordRole
                {
                    Color = new Color(roll["color"].ToObject<int>().ToString("x")),
                    Name = roll["name"].ToString(),
                    Permissions = new DiscordPermission(roll["permissions"].ToObject<uint>()),
                    Position = roll["position"].ToObject<int>(),
                    Managed = roll["managed"].ToObject<bool>(),
                    ID = roll["id"].ToString(),
                    Hoist = roll["hoist"].ToObject<bool>()
                };
                server.Roles.Add(t);
            }
            foreach (var chn in message["d"]["channels"])
            {
                var tempChannel = new DiscordChannel();
                tempChannel.Client = this;
                tempChannel.ID = chn["id"].ToString();
                tempChannel.Type = chn["type"].ToObject<ChannelType>();

                if (!chn["topic"].IsNullOrEmpty())
                    tempChannel.Topic = chn["topic"].ToString();
                if (tempChannel.Type == ChannelType.Voice && !chn["bitrate"].IsNullOrEmpty())
                    tempChannel.Bitrate = chn["bitrate"].ToObject<int>();

                tempChannel.Name = chn["name"].ToString();
                tempChannel.Private = false;
                tempChannel.PermissionOverrides = new List<DiscordPermissionOverride>();
                tempChannel.Parent = server;
                foreach (var o in chn["permission_overwrites"])
                {
                    if (tempChannel.Type == ChannelType.Voice)
                        continue;
                    var dpo = new DiscordPermissionOverride(o["allow"].ToObject<uint>(), o["deny"].ToObject<uint>());
                    dpo.id = o["id"].ToString();

                    if (o["type"].ToString() == "member")
                        dpo.type = DiscordPermissionOverride.OverrideType.member;
                    else
                        dpo.type = DiscordPermissionOverride.OverrideType.role;

                    tempChannel.PermissionOverrides.Add(dpo);
                }
                server.Channels.Add(tempChannel);
            }
            foreach (var mbr in message["d"]["members"])
            {
                var member = JsonConvert.DeserializeObject<DiscordMember>(mbr["user"].ToString());
                if (mbr["nick"] != null)
                    member.Nickname = mbr["nick"].ToString();

                member.parentclient = this;
                member.Parent = server;

                foreach (var rollid in mbr["roles"])
                    member.Roles.Add(server.Roles.Find(x => x.ID == rollid.ToString()));
                if (member.Roles.Count == 0)
                    member.Roles.Add(server.Roles.Find(x => x.Name == "@everyone"));
                server.AddMember(member);
            }
            foreach (var voiceStateJSON in message["d"]["voice_states"])
            {
                var voiceState = JsonConvert.DeserializeObject<DiscordVoiceState>(voiceStateJSON.ToString());
                var member = server.GetMemberByKey(voiceState.UserID);

                member.CurrentVoiceChannel = server.Channels.Find(x => x.ID == voiceState.ChannelID);
                member.VoiceState = voiceState;
            }
            server.Owner = server.GetMemberByKey(message["d"]["owner_id"].ToString());
            e.Server = server;

            if (!message["d"]["unavailable"].IsNullOrEmpty() && message["d"]["unavailable"].ToObject<bool>() == false)
            {
                var oldServer = ServersList.Find(x => x.ID == server.ID);
                if (oldServer != null && oldServer.Unavailable)
                    ServersList.Remove(oldServer);

                ServersList.Add(server);

                GetTextClientLogger.Log($"Guild with ID {server.ID} ({server.Name}) became available.");
                GuildAvailable?.Invoke(this, e);
                return;
            }

            ServersList.Add(server);
            GuildCreated?.Invoke(this, e);
        }

        private void GuildUpdateEvents(JObject message)
        {
            var oldServer = ServersList.Find(x => x.ID == message["d"]["id"].ToString());
            var newServer = oldServer.ShallowCopy();

            newServer.Name = message["d"]["name"].ToString();
            newServer.ID = message["d"]["id"].ToString();
            newServer.parentclient = this;
            newServer.Roles = new List<DiscordRole>();
            newServer.Region = message["d"]["region"].ToString();
            if (!message["d"]["icon"].IsNullOrEmpty())
                newServer.icon = message["d"]["icon"].ToString();
            if (!message["d"]["roles"].IsNullOrEmpty())
                foreach (var roll in message["d"]["roles"])
                {
                    var t = new DiscordRole
                    {
                        Color = new Color(roll["color"].ToObject<int>().ToString("x")),
                        Name = roll["name"].ToString(),
                        Permissions = new DiscordPermission(roll["permissions"].ToObject<uint>()),
                        Position = roll["position"].ToObject<int>(),
                        Managed = roll["managed"].ToObject<bool>(),
                        ID = roll["id"].ToString(),
                        Hoist = roll["hoist"].ToObject<bool>()
                    };
                    newServer.Roles.Add(t);
                }
            else
                newServer.Roles = oldServer.Roles;
            newServer.Channels = new List<DiscordChannel>();
            if (!message["d"]["channels"].IsNullOrEmpty())
                foreach (var u in message["d"]["channels"])
                {
                    var tempSub = new DiscordChannel();
                    tempSub.Client = this;
                    tempSub.ID = u["id"].ToString();
                    tempSub.Name = u["name"].ToString();
                    tempSub.Type = u["type"].ToObject<ChannelType>();

                    if (!u["topic"].IsNullOrEmpty())
                        tempSub.Topic = u["topic"].ToString();
                    if (tempSub.Type == ChannelType.Voice && !u["bitrate"].IsNullOrEmpty())
                        tempSub.Bitrate = u["bitrate"].ToObject<int>();

                    tempSub.Parent = newServer;
                    var permissionoverrides = new List<DiscordPermissionOverride>();
                    foreach (var o in u["permission_overwrites"])
                    {
                        var dpo =
                            new DiscordPermissionOverride(o["allow"].ToObject<uint>(), o["deny"].ToObject<uint>());
                        dpo.type = o["type"].ToObject<DiscordPermissionOverride.OverrideType>();
                        dpo.id = o["id"].ToString();

                        permissionoverrides.Add(dpo);
                    }
                    tempSub.PermissionOverrides = permissionoverrides;

                    newServer.Channels.Add(tempSub);
                }
            else
                newServer.Channels = oldServer.Channels;
            if (!message["d"]["members"].IsNullOrEmpty())
                foreach (var mm in message["d"]["members"])
                {
                    var member = JsonConvert.DeserializeObject<DiscordMember>(mm["user"].ToString());
                    member.parentclient = this;
                    member.Parent = newServer;

                    var rawRoles = JArray.Parse(mm["roles"].ToString());
                    if (rawRoles.Count > 0)
                        foreach (var role in rawRoles.Children())
                            member.Roles.Add(newServer.Roles.Find(x => x.ID == role.Value<string>()));
                    else
                        member.Roles.Add(newServer.Roles.Find(x => x.Name == "@everyone"));

                    newServer.AddMember(member);
                }
            else
                newServer.Members = oldServer.Members;
            if (!message["d"]["owner_id"].IsNullOrEmpty())
            {
                newServer.Owner = newServer.GetMemberByKey(message["d"]["owner_id"].ToString());
                GetTextClientLogger.Log(
                    $"Transferred ownership from user '{oldServer.Owner.Username}' to {newServer.Owner.Username}.");
            }
            ServersList.Remove(oldServer);
            ServersList.Add(newServer);
            var dsuea = new DiscordServerUpdateEventArgs { NewServer = newServer, OldServer = oldServer };
            GuildUpdated?.Invoke(this, dsuea);
        }

        private void GuildDeleteEvents(JObject message)
        {
            var e = new DiscordGuildDeleteEventArgs();
            e.Server = ServersList.Find(x => x.ID == message["d"]["id"].ToString());
            e.RawJson = message;
            ServersList.Remove(e.Server);
            if (GuildDeleted != null)
                GuildDeleted(this, e);
        }

        private void GuildRoleUpdateEvents(JObject message)
        {
            var inServer = ServersList.Find(x => x.ID == message["d"]["guild_id"].ToString());
            var roleUpdated = new DiscordRole
            {
                Name = message["d"]["role"]["name"].ToString(),
                Position = message["d"]["role"]["position"].ToObject<int>(),
                Permissions = new DiscordPermission(message["d"]["role"]["permissions"].ToObject<uint>()),
                Managed = message["d"]["role"]["managed"].ToObject<bool>(),
                Hoist = message["d"]["role"]["hoist"].ToObject<bool>(),
                Color = new Color(message["d"]["role"]["color"].ToObject<int>().ToString("x")),
                ID = message["d"]["role"]["id"].ToString()
            };

            ServersList.Find(x => x.ID == inServer.ID)
                .Roles.Remove(ServersList.Find(x => x.ID == inServer.ID).Roles.Find(y => y.ID == roleUpdated.ID));
            ServersList.Find(x => x.ID == inServer.ID).Roles.Add(roleUpdated);

            RoleUpdated?.Invoke(this,
                new DiscordGuildRoleUpdateEventArgs
                {
                    RawJson = message,
                    RoleUpdated = roleUpdated,
                    InServer = inServer
                });
        }

        private void GuildRoleDeleteEvents(JObject message)
        {
            var inServer = ServersList.Find(x => x.ID == message["d"]["guild_id"].ToString());
            var deletedRole = inServer.Roles.Find(x => x.ID == message["d"]["role_id"].ToString());

            try
            {
                ServersList.Find(x => x.ID == inServer.ID)
                    .Roles.Remove(ServersList.Find(x => x.ID == inServer.ID).Roles.Find(y => y.ID == deletedRole.ID));
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Couldn't delete role with ID {message["d"]["role_id"]}! ({ex.Message})",
                    MessageLevel.Critical);
            }

            RoleDeleted?.Invoke(this,
                new DiscordGuildRoleDeleteEventArgs { DeletedRole = deletedRole, Guild = inServer, RawJson = message });
        }

        private void UserUpdateEvents(JObject message)
        {
            var e = new DiscordUserUpdateEventArgs();
            e.RawJson = message;
            var newMember = JsonConvert.DeserializeObject<DiscordMember>(message["d"].ToString());
            newMember.parentclient = this;

            var oldMember = new DiscordMember(this);
            oldMember.parentclient = this;
            //Update members
            foreach (var server in ServersList)
                foreach (var member in server.Members)
                    if (member.Value.ID == newMember.ID)
                    {
                        oldMember = member.Value;
                        server.AddMember(newMember);
                        break;
                    }

            newMember.Parent = oldMember.Parent;

            if (!message["roles"].IsNullOrEmpty())
            {
                var rawRoles = JArray.Parse(message["roles"].ToString());
                if (rawRoles.Count > 0)
                    foreach (var role in rawRoles.Children())
                        newMember.Roles.Add(newMember.Parent.Roles.Find(x => x.ID == role.ToString()));
                else
                    newMember.Roles.Add(newMember.Parent.Roles.Find(x => x.Name == "@everyone"));
            }

            e.NewMember = newMember;
            e.OriginalMember = oldMember;
            UserUpdate?.Invoke(this, e);
        }

        private void PresenceUpdateEvents(JObject message)
        {
            var dpuea = new DiscordPresenceUpdateEventArgs();
            dpuea.RawJson = message;

            if (!message["d"]["guild_id"].IsNullOrEmpty())
            {
                var server = ServersList.Find(x => x.ID == message["d"]["guild_id"].ToString());
                if (server != null)
                {
                    var user = server.GetMemberByKey(message["d"]["user"]["id"].ToString().Trim());
                    if (user != null)
                    {
                        //If usernames change.
                        if (!message["d"]["user"]["username"].IsNullOrEmpty())
                            user.Username = message["d"]["user"]["username"].ToString();

                        //If avatar changes.
                        if (!message["d"]["user"]["avatar"].IsNullOrEmpty())
                            user.Avatar = message["d"]["user"]["avatar"].ToString();

                        if (message["d"]["nick"].ToString() == null)
                            user.Nickname = null;
                        else
                            user.Nickname = message["d"]["nick"].ToString();

                        //Actual presence update
                        user.SetPresence(message["d"]["status"].ToString());

                        //Updating games.
                        var game = message["d"]["game"].ToString();
                        if (message["d"]["game"].IsNullOrEmpty()) //null means not playing
                        {
                            dpuea.Game = "";
                            user.CurrentGame = null;
                        }
                        else
                        {
                            if (message["d"]["game"]["name"].IsNullOrEmpty())
                                if (message["d"]["game"]["game"].IsNullOrEmpty())
                                    dpuea.Game = "";
                                else
                                    dpuea.Game = message["d"]["game"]["game"].ToString();
                            else
                                dpuea.Game = message["d"]["game"]["name"].ToString();
                            user.CurrentGame = dpuea.Game;

                            if (message["d"]["game"]["type"] != null &&
                                message["d"]["game"]["type"].ToObject<int>() == 1)
                            {
                                user.Streaming = true;
                                if (message["d"]["game"]["url"].ToString() != null)
                                    user.StreamURL = message["d"]["game"]["url"].ToString();
                            }
                        }
                        dpuea.User = user;

                        if (message["d"]["status"].ToString() == "online")
                            dpuea.Status = DiscordUserStatus.ONLINE;
                        else if (message["d"]["status"].ToString() == "idle")
                            dpuea.Status = DiscordUserStatus.IDLE;
                        else if (message["d"]["status"].ToString() == null ||
                                 message["d"]["status"].ToString() == "offline")
                            dpuea.Status = DiscordUserStatus.OFFLINE;
                        if (PresenceUpdated != null)
                            PresenceUpdated(this, dpuea);
                    }
                    else
                    {
                        if (!message["d"]["guild_id"].IsNullOrEmpty()
                        ) //if this is null or empty, that means this pertains to friends list
                            if (!message["d"]["user"]["username"].IsNullOrEmpty() &&
                                !message["d"]["user"]["id"].IsNullOrEmpty())
                            {
                                GetTextClientLogger.Log(
                                    $"User {message["d"]["user"]["username"]} ({message["d"]["user"]["id"]}) doesn't exist in server {server.Name} ({server.ID}) no problemo. Creating/adding",
                                    MessageLevel.Debug);
                                var memeber =
                                    JsonConvert.DeserializeObject<DiscordMember>(message["d"]["user"].ToString());
                                memeber.parentclient = this;
                                memeber.SetPresence(message["d"]["status"].ToString());
                                memeber.Parent = ServersList.Find(x => x.ID == message["d"]["guild_id"].ToString());

                                if (message["d"]["game"].IsNullOrEmpty())
                                {
                                    dpuea.Game = "";
                                    memeber.CurrentGame = null;
                                }
                                else
                                {
                                    dpuea.Game = message["d"]["game"]["name"].ToString();
                                    memeber.CurrentGame = dpuea.Game;
                                    if (message["d"]["game"]["type"].ToObject<int>() == 1)
                                    {
                                        user.Streaming = true;
                                        if (message["d"]["game"]["url"].ToString() != null)
                                            user.StreamURL = message["d"]["game"]["url"].ToString();
                                    }
                                }

                                if (message["d"]["status"].ToString() == "online")
                                    dpuea.Status = DiscordUserStatus.ONLINE;
                                else if (message["d"]["status"].ToString() == "idle")
                                    dpuea.Status = DiscordUserStatus.IDLE;
                                else if (message["d"]["status"].ToString() == null ||
                                         message["d"]["status"].ToString() == "offline")
                                    dpuea.Status = DiscordUserStatus.OFFLINE;

                                memeber.Parent.AddMember(memeber);
                            }
                    }
                }
            }
        }

        private void ChannelCreateEvents(JObject message)
        {
            if (message["d"]["is_private"].ToString().ToLower() == "false")
            {
                var foundServer = ServersList.Find(x => x.ID == message["d"]["guild_id"].ToString());
                if (foundServer != null)
                {
                    var tempChannel = new DiscordChannel();
                    tempChannel.Client = this;
                    tempChannel.Name = message["d"]["name"].ToString();
                    tempChannel.Type = message["d"]["type"].ToObject<ChannelType>();
                    if (tempChannel.Type == ChannelType.Voice && !message["d"]["bitrate"].IsNullOrEmpty())
                        tempChannel.Bitrate = message["d"]["bitrate"].ToObject<int>();

                    tempChannel.ID = message["d"]["id"].ToString();
                    tempChannel.Parent = foundServer;
                    foundServer.Channels.Add(tempChannel);
                    var fae = new DiscordChannelCreateEventArgs();
                    fae.ChannelCreated = tempChannel;
                    fae.ChannelType = DiscordChannelCreateType.CHANNEL;
                    if (ChannelCreated != null)
                        ChannelCreated(this, fae);
                }
            }
            else
            {
                var tempPrivate = new DiscordPrivateChannel();
                tempPrivate.Client = this;
                tempPrivate.ID = message["d"]["id"].ToString();
                var recipient = ServersList
                    .Find(x => x.GetMemberByKey(message["d"]["recipient"]["id"].ToString()) != null)
                    .GetMemberByKey(message["d"]["recipient"]["id"].ToString());
                tempPrivate.Recipient = recipient;
                PrivateChannels.Add(tempPrivate);
                var fak = new DiscordPrivateChannelEventArgs
                {
                    ChannelType = DiscordChannelCreateType.PRIVATE,
                    ChannelCreated = tempPrivate
                };
                if (PrivateChannelCreated != null)
                    PrivateChannelCreated(this, fak);
            }
        }

        private void ChannelUpdateEvents(JObject message)
        {
            var e = new DiscordChannelUpdateEventArgs();
            e.RawJson = message;
            var oldChannel = ServersList.Find(x => x.Channels.Find(y => y.ID == message["d"]["id"].ToString()) != null)
                .Channels.Find(x => x.ID == message["d"]["id"].ToString());
            e.OldChannel = oldChannel.ShallowCopy();
            var newChannel = oldChannel;
            newChannel.Name = message["d"]["name"].ToString();
            if (!message["d"]["topic"].IsNullOrEmpty())
                newChannel.Topic = message["d"]["topic"].ToString();
            else
                newChannel.Topic = oldChannel.Topic;

            newChannel.Private = message["d"]["is_private"].ToObject<bool>();

            var permissionoverrides = new List<DiscordPermissionOverride>();
            foreach (var o in message["d"]["permission_overwrites"])
            {
                var dpo = new DiscordPermissionOverride(o["allow"].ToObject<uint>(), o["deny"].ToObject<uint>());
                dpo.type = o["type"].ToObject<DiscordPermissionOverride.OverrideType>();
                dpo.id = o["id"].ToString();

                permissionoverrides.Add(dpo);
            }
            newChannel.PermissionOverrides = permissionoverrides;

            e.NewChannel = newChannel;

            var serverToRemoveFrom = ServersList.Find(x => x.Channels.Find(y => y.ID == newChannel.ID) != null);
            newChannel.Parent = serverToRemoveFrom;
            var indexOfServer = ServersList.IndexOf(serverToRemoveFrom);
            serverToRemoveFrom.Channels.Remove(oldChannel);
            serverToRemoveFrom.Channels.Add(newChannel);

            ServersList.RemoveAt(indexOfServer);
            ServersList.Insert(indexOfServer, serverToRemoveFrom);

            if (ChannelUpdated != null)
                ChannelUpdated(this, e);
        }

        private void ChannelDeleteEvents(JObject message)
        {
            if (!message["d"]["recipient"].IsNullOrEmpty())
            {
                //private channel removed
                var e = new DiscordPrivateChannelDeleteEventArgs();
                e.PrivateChannelDeleted = PrivateChannels.Find(x => x.ID == message["d"]["id"].ToString());
                if (e.PrivateChannelDeleted != null)
                {
                    if (PrivateChannelDeleted != null)
                        PrivateChannelDeleted(this, e);
                    PrivateChannels.Remove(e.PrivateChannelDeleted);
                }
                else
                {
                    GetTextClientLogger.Log("Error in ChannelDeleteEvents: PrivateChannel is null!",
                        MessageLevel.Error);
                }
            }
            else
            {
                var e = new DiscordChannelDeleteEventArgs
                {
                    ChannelDeleted = GetChannelByID(message["d"]["id"].ToObject<long>())
                };
                DiscordServer server;
                server = e.ChannelDeleted.Parent;
                server.Channels.Remove(server.Channels.Find(x => x.ID == e.ChannelDeleted.ID));

                if (ChannelDeleted != null)
                    ChannelDeleted(this, e);
            }
        }

        private void MessageCreateEvents(JObject message)
        {
            var potentialChannel = GetDiscordChannelByID(message["d"]["channel_id"].ToString());
            if (potentialChannel == null) //private message create
            {
                var dpmea = new DiscordPrivateMessageEventArgs
                {
                    Channel = PrivateChannels.Find(x => x.ID == message["d"]["channel_id"].ToString()),
                    MessageDirection = message["d"]["author"]["id"].ToString() == Me.ID
                        ? DiscordMessageDirection.Out
                        : DiscordMessageDirection.In,
                    Message = message["d"]["content"].ToString(),
                    Attachments = message["d"]["attachments"]
                        .AsEnumerable()
                        .Select(m => m.ToObject<MessageAttachment>())
                        .ToArray(),
                    RawJson = message,
                    BaseMessage = BaseMessage.TryParse(message["d"]),

                    Author = new DiscordMember(this)
                    {
                        Username = message["d"]["author"]["username"].ToString(),
                        ID = message["d"]["author"]["id"].ToString(),
                        parentclient = this
                    }
                };

                PrivateMessageReceived?.Invoke(this, dpmea);
            }
            else
            {
                var dmea = new DiscordMessageEventArgs();
                dmea.RawJson = message;
                dmea.Channel = potentialChannel;

                dmea.MessageText = message["d"]["content"].ToString();

                DiscordMember tempMember = null;
                tempMember = potentialChannel.Parent.GetMemberByKey(message["d"]["author"]["id"].ToString());
                if (tempMember == null)
                {
                    tempMember = JsonConvert.DeserializeObject<DiscordMember>(message["author"].ToString());
                    tempMember.parentclient = this;
                    tempMember.Parent = potentialChannel.Parent;

                    potentialChannel.Parent.AddMember(tempMember);
                }

                dmea.Author = tempMember;

                var m = new DiscordMessage();
                m.Author = dmea.Author;
                m.channel = dmea.Channel;
                m.TypeOfChannelObject = dmea.Channel.GetType();
                m.Content = dmea.MessageText;
                m.ID = message["d"]["id"].ToString();
                m.RawJson = message;
                m.timestamp = DateTime.Now;
                dmea.Message = m;
                if (!message["d"]["attachments"].IsNullOrEmpty())
                {
                    var tempList = new List<DiscordAttachment>();
                    foreach (var attachment in message["d"]["attachments"])
                        tempList.Add(JsonConvert.DeserializeObject<DiscordAttachment>(attachment.ToString()));
                    m.Attachments = tempList.ToArray();
                }

                if (!message["d"]["mentions"].IsNullOrEmpty())
                {
                    var mentionsAsArray = JArray.Parse(message["d"]["mentions"].ToString());
                    foreach (var mention in mentionsAsArray)
                    {
                        var id = mention["id"].ToString();
                        if (id.Equals(Me.ID))
                            if (MentionReceived != null)
                                MentionReceived(this, dmea);
                    }
                }

                var toAdd = new KeyValuePair<string, DiscordMessage>(message["d"]["id"].ToString(), m);
                MessageLog.Add(message["d"]["id"].ToString(), m);

                MessageReceived?.Invoke(this, dmea);
            }
            //}
            //catch (Exception ex)
            //{
            //    DebugLogger.Log("Error ocurred during MessageCreateEvents: " + ex.Message, MessageLevel.Error);
            //}
        }

        private void MessageUpdateEvents(JObject message)
        {
            try
            {
                var pserver = ServersList.Find(
                    x => x.Channels.Find(y => y.ID == message["d"]["channel_id"].ToString()) != null);
                if (pserver == null)
                {
                    //Private message?
                    var channel = PrivateChannels.Find(x => x.ID == message["d"]["channel_id"].ToString());
                    if (channel == null) return;

                    //Private message!
                    var dpmea = new DiscordPrivateMessageEventArgs
                    {
                        Channel = channel,
                        MessageDirection = message["d"]["author"]["id"].ToString() == Me.ID
                            ? DiscordMessageDirection.Out
                            : DiscordMessageDirection.In,
                        Message = message["d"]["content"].ToString(),
                        Attachments = message["d"]["attachments"]
                            .AsEnumerable()
                            .Select(m => m.ToObject<MessageAttachment>())
                            .ToArray(),
                        RawJson = message,
                        BaseMessage = BaseMessage.TryParse(message["d"]),
                        Author = new DiscordMember(this)
                        {
                            Username = message["d"]["author"]["username"].ToString(),
                            ID = message["d"]["author"]["id"].ToString(),
                            parentclient = this
                        }
                    };
                    PrivateMessageUpdate?.Invoke(this, dpmea);
                }
                else
                {
                    var pchannel = pserver.Channels.Find(x => x.ID == message["d"]["channel_id"].ToString());
                    if (pchannel != null)
                        if (message["d"]["author"] != null)
                        {
                            var toRemove = FindInMessageLog(message["d"]["id"].ToString());
                            if (toRemove == null)
                                return; //No message exists
                            var jsonToEdit = toRemove.RawJson;
                            jsonToEdit["d"]["content"].Replace(JToken.FromObject(message["d"]["content"].ToString()));
                            if (MessageEdited != null)
                                MessageEdited(this, new DiscordMessageEditedEventArgs
                                {
                                    Author = pserver.GetMemberByKey(message["d"]["author"]["id"].ToString()),
                                    Channel = pchannel,
                                    MessageText = message["d"]["content"].ToString(),
                                    MessageType = DiscordMessageType.CHANNEL,
                                    MessageEdited = new DiscordMessage
                                    {
                                        Author = pserver.GetMemberByKey(message["d"]["author"]["id"].ToString()),
                                        Content = toRemove.Content,
                                        Attachments = message["d"]["attachments"].ToObject<DiscordAttachment[]>(),
                                        channel =
                                            pserver.Channels.Find(x => x.ID == message["d"]["channel_id"].ToString()),
                                        RawJson = message,
                                        ID = message["d"]["id"].ToString(),
                                        timestamp = message["d"]["timestamp"].ToObject<DateTime>()
                                    },
                                    EditedTimestamp = message["d"]["edited_timestamp"].ToObject<DateTime>()
                                });
                            MessageLog.Remove(message["d"]["id"].ToString());

                            var newMessage = toRemove;
                            newMessage.Content = jsonToEdit["d"]["content"].ToString();
                            MessageLog.Add(message["d"]["id"].ToString(), newMessage);
                        }
                        else //I know they say assume makes an ass out of you and me...but we're assuming it's Discord's weird auto edit of a just URL message
                        {
                            if (URLMessageAutoUpdate != null)
                            {
                                var asdf =
                                    new
                                        DiscordURLUpdateEventArgs(); //I'm running out of clever names and should probably split these off into different internal voids soon...
                                asdf.ID = message["d"]["id"].ToString();
                                asdf.Channel = ServersList
                                    .Find(x => x.Channels.Find(y => y.ID == message["d"]["channel_id"].ToString()) !=
                                               null)
                                    .Channels.Find(x => x.ID == message["d"]["channel_id"].ToString());
                                foreach (var embed in message["d"]["embeds"])
                                {
                                    var temp = new DiscordEmbeds();
                                    temp.URL = embed["url"].ToString();
                                    temp.Description = embed["description"].ToString();
                                    try
                                    {
                                        temp.ProviderName =
                                            embed["provider"]["name"] == null
                                                ? null
                                                : embed["provider"]["name"].ToString();
                                        temp.ProviderURL = embed["provider"]["url"].ToString();
                                    }
                                    catch
                                    {
                                    } //noprovider
                                    temp.Title = embed["title"].ToString();
                                    temp.Type = embed["type"].ToString();
                                    asdf.Embeds.Add(temp);
                                }
                                URLMessageAutoUpdate(this, asdf);
                            }
                        }
                    else
                        GetTextClientLogger.Log("Couldn't find channel!", MessageLevel.Critical);
                }
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log(
                    $"Exception during MessageUpdateEvents.\n\tMessage: {ex.Message}\n\tStack: {ex.StackTrace}",
                    MessageLevel.Critical);
            }
        }

        private void MessageDeletedEvents(JObject message)
        {
            var e = new DiscordMessageDeletedEventArgs();
            e.DeletedMessage = FindInMessageLog(message["d"]["id"].ToString());

            DiscordServer inServer;
            inServer = ServersList.Find(
                x => x.Channels.Find(y => y.ID == message["d"]["channel_id"].ToString()) != null);
            if (inServer == null) //dm delete
            {
                var dm = new DiscordPrivateMessageDeletedEventArgs();
                dm.DeletedMessage = e.DeletedMessage;
                dm.RawJson = message;
                dm.Channel = PrivateChannels.Find(x => x.ID == message["d"]["channel_id"].ToString());
                if (PrivateMessageDeleted != null)
                    PrivateMessageDeleted(this, dm);
            }
            else
            {
                e.Channel = inServer.Channels.Find(x => x.ID == message["d"]["channel_id"].ToString());
                e.RawJson = message;
            }

            if (MessageDeleted != null)
                MessageDeleted(this, e);
        }
    }
}