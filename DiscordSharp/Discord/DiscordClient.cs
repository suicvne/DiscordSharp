using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiscordSharp.Events;
using DiscordSharp.Objects;
using DiscordSharp.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ID = System.String;

namespace DiscordSharp.Discord
{
    /// <summary>
    ///     Where the magic happens.
    /// </summary>
    public partial class DiscordClient
    {
        /// <summary>
        ///     The token associated with authenticating your bot and ensuring they can send messages.
        /// </summary>
        public static string token { get; internal set; }

        /// <summary>
        ///     If this is true, the account this client is running on is a special bot account.
        /// </summary>
        public static bool IsBotAccount { get; internal set; }

        /// <summary>
        ///     The URL that the client websocket is currently connected to.
        /// </summary>
        public string CurrentGatewayURL { get; internal set; }

        /// <summary>
        ///     Any private information assosciated with the account (regular clients only)
        /// </summary>
        public DiscordUserInformation ClientPrivateInformation { get; set; }

        /// <summary>
        ///     Custom properties containing parameters such as
        ///     * OS
        ///     * Referrer
        ///     * Browser
        ///     Used by Discord internally for connection.
        /// </summary>
        public DiscordProperties DiscordProperties { get; set; } = new DiscordProperties();

        /// <summary>
        ///     The current DiscordMember object assosciated with the account you're connected to.
        /// </summary>
        public DiscordMember Me { get; internal set; }

        /// <summary>
        ///     Returns the debug logger used to log various debug events.
        /// </summary>
        public Logger GetTextClientLogger { get; } = new Logger();

        /// <summary>
        ///     Returns the last debug logger for when the voice client was last connected.
        /// </summary>
        public Logger GetLastVoiceClientLogger;

        /// <summary>
        ///     If true, the logger will log everything.
        ///     Everything.
        /// </summary>
        public bool EnableVerboseLogging { get; set; } = false;

        /// <summary>
        ///     The version of the gateway.
        /// </summary>
        public int DiscordGatewayVersion { get; set; }

        [Obsolete]
        internal bool V4Testing { get; set; } = false;

        /// <summary>
        ///     V4 related things. Getting this means our session has been successfully initiated.
        /// </summary>
        private string SessionID;

        /// <summary>
        ///     The last sequence we received used for v4 heartbeat.
        /// </summary>
        private int Sequence;

        /// <summary>
        ///     Whether or not to send Opcode 6 (resume) upon a socket being closed.
        /// </summary>
        public bool Autoconnect { get; set; } = true;

        private IDiscordWebSocket ws;
        private List<DiscordServer> ServersList { get; set; }
        private readonly int? IdleSinceUnixTime = null;

        private static string UserAgentString =
            $" (http://github.com/Luigifan/DiscordSharp, {typeof(DiscordClient).Assembly.GetName().Version})";

        private DiscordVoiceClient VoiceClient;
        private CancellationTokenSource KeepAliveTaskTokenSource = new CancellationTokenSource();
        private CancellationToken KeepAliveTaskToken;
        private Task KeepAliveTask;
        private Thread VoiceThread; //yuck
        private static string StrippedEmail = "";

        /// <summary>
        ///     Testing.
        /// </summary>
        private readonly List<DiscordMember> RemovedMembers = new List<DiscordMember>();

        /// <summary>
        ///     Whether or not to write the latest READY upon receiving it.
        ///     If this is true, the client will write the contents of the READY message to 'READY_LATEST.txt'
        ///     If your client is connected to a lot of servers, this file will be quite large.
        /// </summary>
        public bool WriteLatestReady { get; set; } = false;

        /// <summary>
        ///     Whether or not to request all users in a guild (including offlines) on startup.
        /// </summary>
        public bool RequestAllUsersOnStartup { get; set; } = false;

        /// <summary>
        ///     A log of messages kept in a KeyValuePair.
        ///     The key is the id of the message, and the value is a DiscordMessage object. If you need raw json, this is contained
        ///     inside of the DiscordMessage object now.
        /// </summary>
        private readonly Dictionary<string, DiscordMessage> MessageLog = new Dictionary<string, DiscordMessage>();

        //private List<KeyValuePair<string, DiscordMessage>> MessageLog = new List<KeyValuePair<string, DiscordMessage>>();
        private List<DiscordPrivateChannel> PrivateChannels = new List<DiscordPrivateChannel>();

        /// <summary>
        /// </summary>
        /// <param name="tokenOverride">If you have a token you wish to use, provide it here. Else, a login attempt will be made.</param>
        /// <param name="isBotAccount">Set this to true if your bot is going to be a bot account</param>
        public DiscordClient(string tokenOverride = null, bool isBotAccount = false, bool enableLogging = true)
        {
            if (isBotAccount && tokenOverride == null)
                throw new Exception("Token override cannot be null if using a bot account!");
            GetTextClientLogger.EnableLogging = enableLogging;

            token = tokenOverride;
            IsBotAccount = isBotAccount;

            if (IsBotAccount)
                UserAgentString = "DiscordBot " + UserAgentString;
            else
                UserAgentString = "Custom Discord Client " + UserAgentString;

            if (ClientPrivateInformation == null)
                ClientPrivateInformation = new DiscordUserInformation();

            GetTextClientLogger.LogMessageReceived += (sender, e) =>
            {
                if (e.message.Level == MessageLevel.Error)
                    DisconnectFromVoice();
                if (TextClientDebugMessageReceived != null)
                    TextClientDebugMessageReceived(this, e);
            };
        }

        /// <summary>
        ///     Current DiscordServers you're connected to.
        /// </summary>
        /// <returns>DiscordServer list of servers you're currently connected to.</returns>
        public List<DiscordServer> GetServersList()
        {
            return ServersList;
        }

        /// <summary>
        ///     Any messages logged since connection to the websocket.
        /// </summary>
        /// <returns>A KeyValuePair list of string-DiscordMessage. Where string is the message's ID</returns>
        public Dictionary<string, DiscordMessage> GetMessageLog()
        {
            return MessageLog;
        }

        /// <summary>
        ///     Private channels assosciated with the account.
        /// </summary>
        /// <returns>a list of DiscordPrivateChannels.</returns>
        public List<DiscordPrivateChannel> GetPrivateChannels()
        {
            return PrivateChannels;
        }

        /// <summary>
        /// </summary>
        /// <returns>True if connected to voice.</returns>
        public bool ConnectedToVoice()
        {
            return VoiceClient != null ? VoiceClient.Connected : false;
        }

        //eh
        private void GetChannelsList(JObject m)
        {
            if (ServersList == null)
                ServersList = new List<DiscordServer>();
            foreach (var j in m["d"]["guilds"])
            {
                if (!j["unavailable"].IsNullOrEmpty() && j["unavailable"].ToObject<bool>())
                    continue; //unavailable server
                var temp = new DiscordServer();
                temp.parentclient = this;
                temp.JoinedAt = j["joined_at"].ToObject<DateTime>();
                temp.ID = j["id"].ToString();
                temp.Name = j["name"].ToString();
                if (!j["icon"].IsNullOrEmpty())
                    temp.icon = j["icon"].ToString();
                else
                    temp.icon = null;

                //temp.owner_id = j["owner_id"].ToString();
                var tempSubs = new List<DiscordChannel>();

                var tempRoles = new List<DiscordRole>();
                foreach (var u in j["roles"])
                {
                    var t = new DiscordRole
                    {
                        Color = new Color(u["color"].ToObject<int>().ToString("x")),
                        Name = u["name"].ToString(),
                        Permissions = new DiscordPermission(u["permissions"].ToObject<uint>()),
                        Position = u["position"].ToObject<int>(),
                        Managed = u["managed"].ToObject<bool>(),
                        ID = u["id"].ToString(),
                        Hoist = u["hoist"].ToObject<bool>()
                    };
                    tempRoles.Add(t);
                }
                temp.Roles = tempRoles;
                foreach (var u in j["channels"])
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
                    tempSub.Parent = temp;
                    var permissionoverrides = new List<DiscordPermissionOverride>();
                    foreach (var o in u["permission_overwrites"])
                    {
                        var dpo =
                            new DiscordPermissionOverride(o["allow"].ToObject<uint>(), o["deny"].ToObject<uint>());
                        dpo.id = o["id"].ToString();

                        if (o["type"].ToString() == "member")
                            dpo.type = DiscordPermissionOverride.OverrideType.member;
                        else
                            dpo.type = DiscordPermissionOverride.OverrideType.role;

                        permissionoverrides.Add(dpo);
                    }
                    tempSub.PermissionOverrides = permissionoverrides;

                    tempSubs.Add(tempSub);
                }
                temp.Channels = tempSubs;
                foreach (var mm in j["members"])
                {
                    var member = JsonConvert.DeserializeObject<DiscordMember>(mm["user"].ToString());
                    member.parentclient = this;
                    member.Roles = new List<DiscordRole>();
                    var rawRoles = JArray.Parse(mm["roles"].ToString());
                    if (rawRoles.Count > 0)
                        foreach (var role in rawRoles.Children())
                            member.Roles.Add(temp.Roles.Find(x => x.ID == role.Value<string>()));
                    else
                        member.Roles.Add(temp.Roles.Find(x => x.Name == "@everyone"));
                    temp.AddMember(member);
                }
                if (!j["presences"].IsNullOrEmpty())
                    foreach (var presence in j["presences"])
                    {
                        var member = temp.GetMemberByKey(presence["user"]["id"].ToString());
                        if (member != null)
                        {
                            member.SetPresence(presence["status"].ToString());
                            if (!presence["game"].IsNullOrEmpty())
                            {
                                member.CurrentGame = presence["game"]["name"].ToString();
                                if (presence["game"]["type"].ToObject<int>() == 1)
                                {
                                    member.Streaming = true;
                                    if (presence["game"]["url"].ToString() != null)
                                        member.StreamURL = presence["game"]["url"].ToString();
                                }
                            }
                        }
                    }
                temp.Region = j["region"].ToString();
                temp.Owner = temp.GetMemberByKey(j["owner_id"].ToString());
                ServersList.Add(temp);
            }
            if (PrivateChannels == null)
                PrivateChannels = new List<DiscordPrivateChannel>();
            foreach (var privateChannel in m["d"]["private_channels"])
            {
                var tempPrivate = JsonConvert.DeserializeObject<DiscordPrivateChannel>(privateChannel.ToString());
                tempPrivate.Client = this;
                tempPrivate.user_id = privateChannel["recipient"]["id"].ToString();
                var potentialServer = new DiscordServer();
                ServersList.ForEach(x =>
                {
                    if (x.GetMemberByKey(privateChannel["recipient"]["id"].ToString()) != null)
                        potentialServer = x;
                });
                if (potentialServer.Owner != null) //should be a safe test..i hope
                {
                    var recipient = potentialServer.GetMemberByKey(privateChannel["recipient"]["id"].ToString());
                    if (recipient != null)
                        tempPrivate.Recipient = recipient;
                    else
                        GetTextClientLogger.Log("Recipient was null!!!!", MessageLevel.Critical);
                }
                else
                {
                    GetTextClientLogger.Log(
                        "No potential server found for user's private channel null! This will probably fix itself.",
                        MessageLevel.Debug);
                }
                PrivateChannels.Add(tempPrivate);
            }
        }

        /// <summary>
        ///     Sends an http DELETE request to leave the server you send in this parameter.
        /// </summary>
        /// <param name="server">The DiscordServer object you want to leave.</param>
        public void LeaveServer(DiscordServer server)
        {
            LeaveServer(server.ID);
        }

        /// <summary>
        ///     (Owner only, non-bot only) Sends an http DELETE request to delete the server you specify.
        /// </summary>
        /// <param name="server">The DiscordServer object you want to delete.</param>
        public void DeleteServer(DiscordServer server)
        {
            DeleteServer(server.ID);
        }

        /// <summary>
        ///     (Owner only, non-bot only) Sends an http DELETE request to delete the server you specify.
        /// </summary>
        /// <param name="ServerID">The server's ID you want to delete.</param>
        public void LeaveServer(string ServerID)
        {
            var url = //Endpoints.BaseAPI + Endpoints.Guilds + $"/{ServerID}";
                Endpoints.BaseAPI + Endpoints.Users + Endpoints.Me + Endpoints.Guilds +
                $"/{ServerID}"; //old, left for lulz
            try
            {
                WebWrapper.Delete(url, token);
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Error ocurred while leaving server ({ServerID}): {ex.Message}",
                    MessageLevel.Error);
            }
        }

        /// <summary>
        ///     (Owner only, non-bot only) Sends an http DELETE request to delete the server you specify by ID.
        /// </summary>
        /// <param name="ServerID">The server's ID you want to delete.</param>
        public void DeleteServer(string ServerID)
        {
            if (IsBotAccount)
                throw new Exception("Bot accounts can't own servers!");

            var url = Endpoints.BaseAPI + Endpoints.Guilds + $"/{ServerID}";
            try
            {
                WebWrapper.Delete(url, token);
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Error ocurred while deleting server ({ServerID}): {ex.Message}",
                    MessageLevel.Error);
            }
        }

        /// <summary>
        ///     Sends a message to a channel, what else did you expect?
        /// </summary>
        /// <param name="message">The text to send</param>
        /// <param name="channel">DiscordChannel object to send the message to.</param>
        /// <returns>A DiscordMessage object of the message sent to Discord.</returns>
        public DiscordMessage SendMessageToChannel(string message, DiscordChannel channel)
        {
            var url = Endpoints.BaseAPI + Endpoints.Channels + $"/{channel.ID}" + Endpoints.Messages;
            try
            {
                var result = JObject.Parse(WebWrapper.Post(url, token,
                    JsonConvert.SerializeObject(Utils.GenerateMessage(message))));
                if (result["content"].IsNullOrEmpty())
                    throw new InvalidOperationException(
                        "Request returned a blank message, you may not have permission to send messages yet!");

                var m = new DiscordMessage
                {
                    ID = result["id"].ToString(),
                    Attachments = result["attachments"].ToObject<DiscordAttachment[]>(),
                    Author = channel.Parent.GetMemberByKey(result["author"]["id"].ToString()),
                    channel = channel,
                    TypeOfChannelObject = channel.GetType(),
                    Content = result["content"].ToString(),
                    RawJson = result,
                    timestamp = result["timestamp"].ToObject<DateTime>()
                };
                return m;
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log(
                    $"Error ocurred while sending message to channel ({channel.Name}): {ex.Message}",
                    MessageLevel.Error);
            }
            return null;
        }

        /// <summary>
        ///     Sends a file to the specified DiscordChannel with the given message.
        /// </summary>
        /// <param name="channel">The channel to send the message to.</param>
        /// <param name="message">The message you want the file to have with it.</param>
        /// <param name="pathToFile">The path to the file you wish to send (be careful!)</param>
        public void AttachFile(DiscordChannel channel, string message, string pathToFile)
        {
            var url = Endpoints.BaseAPI + Endpoints.Channels + $"/{channel.ID}" + Endpoints.Messages;
            //WebWrapper.PostWithAttachment(url, message, pathToFile);
            try
            {
                var uploadResult = JObject.Parse(
                    WebWrapper.HttpUploadFile(url, token, pathToFile, "file", "image/jpeg", null));

                if (!string.IsNullOrEmpty(message))
                    EditMessage(uploadResult["id"].ToString(), message, channel);
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log(
                    $"Error ocurred while sending file ({pathToFile}) to {channel.Name}: {ex.Message}",
                    MessageLevel.Error);
            }
        }

        /// <summary>
        ///     Sends a file to the specified DiscordChannel with the given message.
        /// </summary>
        /// <param name="channel">The channel to send the message to.</param>
        /// <param name="message">The message you want the file to have with it.</param>
        /// <param name="stream">A stream object to send the bytes from.</param>
        public void AttachFile(DiscordChannel channel, string message, Stream stream)
        {
            var url = Endpoints.BaseAPI + Endpoints.Channels + $"/{channel.ID}" + Endpoints.Messages;
            //WebWrapper.PostWithAttachment(url, message, pathToFile);
            try
            {
                var uploadResult =
                    JObject.Parse(WebWrapper.HttpUploadFile(url, token, stream, "file", "image/jpeg", null));

                if (!string.IsNullOrEmpty(message))
                    EditMessage(uploadResult["id"].ToString(), message, channel);
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Error ocurred while sending file by stream to {channel.Name}: {ex.Message}",
                    MessageLevel.Error);
            }
        }

        /// <summary>
        ///     Changes the current client's avatar.
        ///     Any high resolution pictures are automatically downscaled and Discord will perform jpeg compression on them.
        /// </summary>
        /// <param name="image">The Bitmap object assosciated with the avatar you wish to upload.</param>
        public void ChangeClientAvatar(Bitmap image)
        {
            var base64 = Convert.ToBase64String(Utils.ImageToByteArray(image));
            var type = "image/jpeg;base64";
            var req = $"data:{type},{base64}";
            var usernameRequestJson = JsonConvert.SerializeObject(new
            {
                avatar = req,
                email = ClientPrivateInformation.Email,
                password = ClientPrivateInformation.Password,
                username = ClientPrivateInformation.Username
            });
            var url = Endpoints.BaseAPI + Endpoints.Users + "/@me";
            try
            {
                WebWrapper.Patch(url, token, usernameRequestJson);
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Error ocurred while changing client's avatar: {ex.Message}",
                    MessageLevel.Error);
            }
        }

        /// <summary>
        ///     Changes the icon assosciated with the guild. Discord will perform jpeg compression and this image is automatically
        ///     downscaled.
        /// </summary>
        /// <param name="image">The bitmap object associated </param>
        /// <param name="guild">The guild of the icon you wish to change.</param>
        public void ChangeGuildIcon(Bitmap image, DiscordServer guild)
        {
            var resized = new Bitmap(image, 200, 200);

            var base64 = Convert.ToBase64String(Utils.ImageToByteArray(resized));
            var type = "image/jpeg;base64";
            var req = $"data:{type},{base64}";
            var guildjson = JsonConvert.SerializeObject(new { icon = req, name = guild.Name });
            var url = Endpoints.BaseAPI + Endpoints.Guilds + "/" + guild.ID;
            try
            {
                var result = JObject.Parse(WebWrapper.Patch(url, token, guildjson));
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Error ocurred while changing guild {guild.Name}'s icon: {ex.Message}",
                    MessageLevel.Error);
            }
        }

        /// <summary>
        ///     Returns a List of DiscordMessages.
        /// </summary>
        /// <param name="channel">The channel to return them from.</param>
        /// <param name="count">How many to return</param>
        /// <param name="idBefore">Messages before this message ID.</param>
        /// <param name="idAfter">Messages after this message ID.</param>
        /// <returns>A List of DiscordMessages that you can iterate through.</returns>
        public List<DiscordMessage> GetMessageHistory(DiscordChannelBase channel, int count, string idBefore = "",
            string idAfter = "")
        {
            var request = "https://discordapp.com/api/channels/" + channel.ID + $"/messages?&limit={count}";
            if (!string.IsNullOrEmpty(idBefore))
                request += $"&before={idBefore}";
            if (string.IsNullOrEmpty(idAfter))
                request += $"&after={idAfter}";

            JArray result = null;

            try
            {
                var res = WebWrapper.Get(request, token);
                result = JArray.Parse(res);
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log(
                    $"Error ocurred while getting message history for channel {channel.ID}: {ex.Message}",
                    MessageLevel.Error);
            }

            if (result != null)
            {
                var messageList = new List<DiscordMessage>();
                /// NOTE
                /// For some reason, the d object is excluded from this.
                foreach (var item in result.Children())
                    messageList.Add(new DiscordMessage
                    {
                        ID = item["id"].ToString(),
                        channel = channel,
                        Attachments = item["attachments"].ToObject<DiscordAttachment[]>(),
                        TypeOfChannelObject = channel.GetType(),
                        Author = GetMemberFromChannel(channel, item["author"]["id"].ToString()),
                        Content = item["content"].ToString(),
                        RawJson = item.ToObject<JObject>(),
                        timestamp = DateTime.Parse(item["timestamp"].ToString())
                    });
                return messageList;
            }

            return null;
        }

        /// <summary>
        ///     Changes the channel topic assosciated with the Discord text channel.
        /// </summary>
        /// <param name="Channeltopic">The new channel topic.</param>
        /// <param name="channel">The channel you wish to change the topic for.</param>
        public void ChangeChannelTopic(string Channeltopic, DiscordChannel channel)
        {
            var topicChangeJson = JsonConvert.SerializeObject(
                new
                {
                    name = channel.Name,
                    topic = Channeltopic
                });
            var url = Endpoints.BaseAPI + Endpoints.Channels + $"/{channel.ID}";
            try
            {
                var result = JObject.Parse(WebWrapper.Patch(url, token, topicChangeJson));
                ServersList.Find(x => x.Channels.Find(y => y.ID == channel.ID) != null)
                    .Channels.Find(x => x.ID == channel.ID)
                    .Topic = Channeltopic;
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log(
                    $"Error ocurred while changing channel topic for channel {channel.Name}: {ex.Message}",
                    MessageLevel.Error);
            }
        }

        /*
        public List<DiscordRole> GetRoles(DiscordServer server)
        {
            return null;
        }
        */

        /// <summary>
        ///     Used for changing the client's email, password, username, etc.
        /// </summary>
        /// <param name="info"></param>
        public void ChangeClientInformation(DiscordUserInformation info)
        {
            string usernameRequestJson;
            if (info.Password != ClientPrivateInformation.Password)
            {
                usernameRequestJson = JsonConvert.SerializeObject(new
                {
                    email = info.Email,
                    new_password = info.Password,
                    password = ClientPrivateInformation.Password,
                    username = info.Username,
                    avatar = info.Avatar
                });
                ClientPrivateInformation.Password = info.Password;
                try
                {
                    File.Delete("token_cache");
                    GetTextClientLogger.Log("Deleted token_cache due to change of password.");
                }
                catch (Exception)
                {
                    /*ignore*/
                }
            }
            else
            {
                usernameRequestJson = JsonConvert.SerializeObject(new
                {
                    email = info.Email,
                    password = info.Password,
                    username = info.Username,
                    avatar = info.Avatar
                });
            }

            var url = Endpoints.BaseAPI + Endpoints.Users + "/@me";
            try
            {
                var result = JObject.Parse(WebWrapper.Patch(url, token, usernameRequestJson));
                foreach (var server in ServersList)
                    if (server.Members[Me.ID] != null)
                        server.Members[Me.ID].Username = info.Username;
                Me.Username = info.Username;
                Me.Email = info.Email;
                Me.Avatar = info.Avatar;
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Error ocurred while changing client's information: {ex.Message}",
                    MessageLevel.Error);
            }
        }

        private void ChangeClientUsername(string newUsername)
        {
            var url = Endpoints.BaseAPI + Endpoints.Users + "/@me";
            var usernameRequestJson = JsonConvert.SerializeObject(new
            {
                email = ClientPrivateInformation.Email,
                password = ClientPrivateInformation.Password,
                username = newUsername,
                avatar = Me.Avatar
            });
            try
            {
                var result = JObject.Parse(WebWrapper.Patch(url, token, usernameRequestJson));
                if (result != null)
                {
                    foreach (var server in ServersList)
                        if (server.Members[Me.ID] != null)
                            server.Members[Me.ID].Username = newUsername;
                    Me.Username = newUsername;
                }
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Error ocurred while changing client's username: {ex.Message}",
                    MessageLevel.Error);
            }
        }

        /// <summary>
        ///     Sends a private message to the given user.
        /// </summary>
        /// <param name="message">The message text to send them.</param>
        /// <param name="member">The member you want to send this to.</param>
        /// <returns></returns>
        public DiscordMessage SendMessageToUser(string message, DiscordMember member)
        {
            var url = Endpoints.BaseAPI + Endpoints.Users + $"/{Me.ID}" + Endpoints.Channels;
            var initMessage = "{\"recipient_id\":" + member.ID + "}";

            try
            {
                var result = JObject.Parse(WebWrapper.Post(url, token, initMessage));
                if (result != null)
                {
                    var recipient = ServersList.Find(
                            x => x.GetMemberByKey(result["recipient"]["id"].ToString()) != null)
                        .Members[result["recipient"]["id"].ToString()];
                    return SendActualMessage(result["id"].ToString(), message, recipient);
                }
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Error ocurred while sending message to user, step 1: {ex.Message}",
                    MessageLevel.Error);
            }

            return null;
        }

        private DiscordMessage SendActualMessage(string id, string message, DiscordMember recipient)
        {
            var url = Endpoints.BaseAPI + Endpoints.Channels + $"/{id}" + Endpoints.Messages;
            var toSend = Utils.GenerateMessage(message);

            try
            {
                var result = JObject.Parse(WebWrapper.Post(url, token, JsonConvert.SerializeObject(toSend)));
                var d = JsonConvert.DeserializeObject<DiscordMessage>(result.ToString());
                d.Recipient = recipient;
                d.channel = PrivateChannels.Find(x => x.ID == result["channel_id"].ToString());
                d.TypeOfChannelObject = typeof(DiscordPrivateChannel);
                d.Author = Me;
                return d;
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Error ocurred while sending message to user, step 2: {ex.Message}",
                    MessageLevel.Error);
            }
            return null;
        }

        /// <summary>
        ///     Gets the string value of the current game your bot is 'playing'.
        /// </summary>
        public string GetCurrentGame { get; private set; } = "";

        /// <summary>
        ///     Returns true if the websocket is not null and is alive.
        /// </summary>
        public bool WebsocketAlive
        {
            get { return ws != null ? ws.IsAlive : false; }
        }

        public bool ReadyComplete { get; private set; }

        #region Message Received Crap..

        /// <summary>
        ///     Updates the bot's 'Currently playing' status to the following text. Pass in null if you want to remove this.
        /// </summary>
        /// <param name="gameName">The game's name. Old gameid lookup can be seen at: http://hastebin.com/azijiyaboc.json/ </param>
        /// <param name="streaming">
        ///     Whether or not you want your bot to appear as if it is streaming. True means it will show it's
        ///     streaming.
        /// </param>
        /// <param name="url">The 'url' for the stream, if your bot is streaming.</param>
        public void UpdateCurrentGame(string gameName, bool streaming, string url = null)
        {
            string msg;
            if (gameName.ToLower().Trim() != "")
            {
                msg = JsonConvert.SerializeObject(
                    new
                    {
                        op = 3,
                        d = new
                        {
                            idle_since = IdleSinceUnixTime == null ? (object)null : IdleSinceUnixTime,
                            game = new
                            {
                                name = gameName,
                                type = streaming ? 1 : 0,
                                url = url != null ? url : (object)null
                            }
                        }
                    });
                GetCurrentGame = gameName;
                GetTextClientLogger.Log($"Updating client's current game as '{gameName}'");
            }
            else
            {
                msg = JsonConvert.SerializeObject(
                    new
                    {
                        op = 3,
                        d = new
                        {
                            idle_since = IdleSinceUnixTime == null ? (object)null : IdleSinceUnixTime,
                            game = (object)null
                        }
                    });
                GetTextClientLogger.Log("Setting current game to null.");
            }
            ws.Send(msg);
        }

        /// <summary>
        ///     Updates the bot's status.
        /// </summary>
        /// <param name="idle">True if you want the bot to report as idle.</param>
        public void UpdateBotStatus(bool idle)
        {
            string msg;
            msg = JsonConvert.SerializeObject(
                new
                {
                    op = 3,
                    d = new
                    {
                        idle_since = idle ? (int)(DateTime.UtcNow - epoch).TotalMilliseconds : (object)null,
                        game = GetCurrentGame.ToLower().Trim() == "" ? (object)null : new { name = GetCurrentGame }
                    }
                });
            ws.Send(msg); //let's try it!
        }

        /// <summary>
        ///     Deletes a message with a specified ID.
        ///     This method will only work if the message was sent since the bot has ran.
        /// </summary>
        /// <param name="id"></param>
        public void DeleteMessage(string id)
        {
            var message = MessageLog[id];
            if (message != null)
                SendDeleteRequest(message);
        }

        /// <summary>
        ///     Deletes a specified DiscordMessage.
        /// </summary>
        /// <param name="message"></param>
        public void DeleteMessage(DiscordMessage message)
        {
            SendDeleteRequest(message);
        }

        //public void DeletePrivateMessage(DiscordMessage message)
        //{
        //    SendDeleteRequest(message, true);
        //}

        /// <summary>
        ///     Deletes all messages made by the bot since running.
        /// </summary>
        /// <returns>A count of messages deleted.</returns>
        public int DeleteAllMessages()
        {
            var count = 0;

            foreach (var kvp in MessageLog)
                if (kvp.Value.Author.ID == Me.ID)
                {
                    SendDeleteRequest(kvp.Value);
                    count++;
                }
            return count;
        }

        /// <summary>
        ///     Deletes the specified number of messages in a given channel.
        ///     Thank you to Siegen for this idea/method!
        /// </summary>
        /// <param name="channel">The channel to delete messages in.</param>
        /// <param name="count">The amount of messages to delete (max 100)</param>
        /// <returns>The count of messages deleted.</returns>
        public int DeleteMultipleMessagesInChannel(DiscordChannel channel, int count)
        {
            if (count > 100)
                count = 100;

            var __count = 0;

            var messages = GetMessageHistory(channel, count, null, null);

            messages.ForEach(x =>
            {
                if (x.channel.ID == channel.ID)
                {
                    SendDeleteRequest(x);
                    __count++;
                }
            });

            return __count;
        }

        /// <summary>
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="username"></param>
        /// <param name="caseSensitive"></param>
        /// <returns></returns>
        public DiscordMember GetMemberFromChannel(DiscordChannelBase channel, string username, bool caseSensitive)
        {
            if (string.IsNullOrEmpty(username))
                throw new ArgumentException("Argument given for username was null/empty.");
            if (channel != null)
            {
                if (channel.GetType() == typeof(DiscordChannel)) //regular channel
                {
                    var foundMember = ((DiscordChannel)channel).Parent.GetMemberByUsername(username, caseSensitive);
                    if (foundMember != null)
                        return foundMember;
                    GetTextClientLogger.Log("Error in GetMemberFromChannel: foundMember was null!", MessageLevel.Error);
                }
                else if (channel.GetType() == typeof(DiscordPrivateChannel))
                {
                    return ((DiscordPrivateChannel)channel).Recipient;
                }
            }
            else
            {
                GetTextClientLogger.Log("Error in GetMemberFromChannel: channel was null!", MessageLevel.Error);
            }
            return null;
        }

        /// <summary>
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public DiscordMember GetMemberFromChannel(DiscordChannelBase channel, string id)
        {
            if (channel != null)
            {
                if (channel.GetType() == typeof(DiscordChannel)) //regular
                {
                    var foundMember = ((DiscordChannel)channel).Parent.GetMemberByKey(id);
                    if (foundMember != null)
                        return foundMember;
                    GetTextClientLogger.Log($"Error in GetMemberFromChannel: foundMember was null! ID: {id}",
                        MessageLevel.Error);
                }
                else if (channel.GetType() == typeof(DiscordPrivateChannel))
                {
                    return ((DiscordPrivateChannel)channel).Recipient;
                }
            }
            else
            {
                GetTextClientLogger.Log("Error in GetMemberFromChannel: channel was null!", MessageLevel.Error);
            }
            return null;
        }

        /// <summary>
        ///     you probably shouldn't use this.
        /// </summary>
        /// <param name="channelName"></param>
        /// <returns></returns>
        public DiscordChannel GetChannelByName(string channelName)
        {
            try
            {
                return ServersList.Find(x => x.Channels.Find(y => y.Name.ToLower() == channelName.ToLower()) != null)
                    .Channels.Find(x => x.Name.ToLower() == channelName.ToLower());
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public DiscordChannel GetChannelByID(long id)
        {
            return ServersList.Find(x => x.Channels.Find(y => y.ID == id.ToString()) != null)
                .Channels.Find(z => z.ID == id.ToString());
        }

        /// <summary>
        ///     (Client account only) accepts an invite to a server.
        /// </summary>
        /// <param name="inviteID">The ID of the invite you want to accept. This is NOT the full URL of the invite</param>
        public void AcceptInvite(string inviteID)
        {
            if (!IsBotAccount)
            {
                if (inviteID.StartsWith("http://"))
                    inviteID = inviteID.Substring(inviteID.LastIndexOf('/') + 1);

                var url = Endpoints.BaseAPI + Endpoints.Invite + $"/{inviteID}";
                try
                {
                    var result = WebWrapper.Post(url, token, "", true);
                    GetTextClientLogger.Log("Accept invite result: " + result);
                }
                catch (Exception ex)
                {
                    GetTextClientLogger.Log($"Error accepting invite: {ex.Message}", MessageLevel.Error);
                }
            }
            else
            {
                throw new InvalidOperationException(
                    "Bot accounts can't accept invites normally! Please use the OAuth flow to add bots to servers you have the \"Manage Server\" permission in.");
            }
        }

        /// <summary>
        /// </summary>
        /// <returns>The last DiscordMessage sent</returns>
        public DiscordMessage GetLastMessageSent()
        {
            foreach (var message in MessageLog)
                if (message.Value.Author.ID == Me.ID)
                    return message.Value;
            return null;
        }

        /// <summary>
        /// </summary>
        /// <param name="inChannel"></param>
        /// <returns>The last DiscordMessage sent in the given channel</returns>
        public DiscordMessage GetLastMessageSent(DiscordChannel inChannel)
        {
            foreach (var message in MessageLog)
                if (message.Value.Author.ID == Me.ID && message.Value.channel.ID == inChannel.ID)
                    return message.Value;
            return null;
        }

        /// <summary>
        ///     If you screwed up, you can use this method to edit a given message. This sends out an http patch request with a
        ///     replacement message
        /// </summary>
        /// <param name="MessageID">The ID of the message you want to edit.</param>
        /// <param name="replacementMessage">What you want the text to be edited to.</param>
        /// <param name="channel">The channel the message is in</param>
        /// <returns>the new and improved DiscordMessage object.</returns>
        public DiscordMessage EditMessage(string MessageID, string replacementMessage, DiscordChannel channel)
        {
            var url = Endpoints.BaseAPI + Endpoints.Channels + $"/{channel.ID}" + Endpoints.Messages + $"/{MessageID}";
            try
            {
                var replacement = JsonConvert.SerializeObject(
                    new
                    {
                        content = replacementMessage,
                        mentions = new string[0]
                    }
                );
                var result = JObject.Parse(WebWrapper.Patch(url, token, replacement));

                var m = new DiscordMessage
                {
                    RawJson = result,
                    Attachments = result["attachments"].ToObject<DiscordAttachment[]>(),
                    Author = channel.Parent.GetMemberByKey(result["author"]["id"].ToString()),
                    TypeOfChannelObject = channel.GetType(),
                    channel = channel,
                    Content = result["content"].ToString(),
                    ID = result["id"].ToString(),
                    timestamp = result["timestamp"].ToObject<DateTime>()
                };
                return m;
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log("Exception ocurred while editing: " + ex.Message, MessageLevel.Error);
            }

            return null;
        }

        private void SendDeleteRequest(DiscordMessage message)
        {
            string url;
            //if(!user)
            url = Endpoints.BaseAPI + Endpoints.Channels + $"/{message.channel.ID}" + Endpoints.Messages +
                  $"/{message.ID}";
            //else
            //url = Endpoints.BaseAPI + Endpoints.Channels + $"/{message.channel.id}" + Endpoints.Messages + $"/{message.id}";
            try
            {
                var result = WebWrapper.Delete(url, token);
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Exception ocurred while deleting message (ID: {message.ID}): " + ex.Message,
                    MessageLevel.Error);
            }
        }

        private DiscordMessage FindInMessageLog(string id)
        {
            foreach (var message in MessageLog)
                if (message.Key == id)
                    return message.Value;

            return null;
        }

        private DiscordChannel GetDiscordChannelByID(string id)
        {
            var returnVal = new DiscordChannel { ID = "-1" };
            ServersList.ForEach(x =>
            {
                x.Channels.ForEach(y =>
                {
                    if (y.ID == id)
                        returnVal = y;
                });
            });
            if (returnVal.ID != "-1")
                return returnVal;
            return null;
        }

        #endregion Message Received Crap..

        private string GetGatewayUrl()
        {
            if (token == null)
                throw new NullReferenceException("token was null!");

            //i'm ashamed of myself for this but i'm tired
            tryAgain:
            var url = Endpoints.BaseAPI + Endpoints.Gateway;
            if (V4Testing)
                url = "https://ptb.discordapp.com/api/gateway";
            try
            {
                var gateway = JObject.Parse(WebWrapper.Get(url, token))["url"].ToString();
                if (!string.IsNullOrEmpty(gateway))
                    return gateway + (V4Testing ? "?encoding=json&v=4" : "");
                throw new NullReferenceException("Failed to retrieve Gateway urL!");
            }
            catch (UnauthorizedAccessException) //bad token
            {
                GetTextClientLogger.Log("Got 401 from Discord. Token bad, deleting and retrying login...");
                if (File.Exists((uint)StrippedEmail.GetHashCode() + ".cache"))
                    File.Delete((uint)StrippedEmail.GetHashCode() + ".cache");
                SendLoginRequest();
                goto tryAgain;
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log("Exception ocurred while retrieving Gateway URL: " + ex.Message,
                    MessageLevel.Error);
                return null;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public DiscordServer GetServerChannelIsIn(DiscordChannel channel)
        {
            return ServersList.Find(x => x.Channels.Find(y => y.ID == channel.ID) != null);
        }

        /// <summary>
        ///     Deletes a specified Discord channel given you have the permission.
        /// </summary>
        /// <param name="channel">The DiscordChannel object to delete</param>
        public void DeleteChannel(DiscordChannel channel)
        {
            var url = Endpoints.BaseAPI + Endpoints.Channels + $"/{channel.ID}";
            try
            {
                WebWrapper.Delete(url, token);
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log("Exception ocurred while deleting channel: " + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>
        ///     Creates either a text or voice channel in a DiscordServer given a name. Given you have the permission of course.
        /// </summary>
        /// <param name="server">The server to create the channel in.</param>
        /// <param name="ChannelName">The name of the channel (will automatically be lowercased if text)</param>
        /// <param name="voice">True if you want the channel to be a voice channel.</param>
        /// <returns>The newly created DiscordChannel</returns>
        public DiscordChannel CreateChannel(DiscordServer server, string ChannelName, bool voice)
        {
            var url = Endpoints.BaseAPI + Endpoints.Guilds + $"/{server.ID}" + Endpoints.Channels;
            var reqJson = JsonConvert.SerializeObject(new { name = ChannelName, type = voice ? "voice" : "text" });
            try
            {
                var result = JObject.Parse(WebWrapper.Post(url, token, reqJson));
                if (result != null)
                {
                    var dc = new DiscordChannel
                    {
                        Client = this,
                        Name = result["name"].ToString(),
                        ID = result["id"].ToString(),
                        Type = result["type"].ToObject<ChannelType>(),
                        Private = result["is_private"].ToObject<bool>()
                    };
                    if (!result["topic"].IsNullOrEmpty())
                        dc.Topic = result["topic"].ToString();
                    if (dc.Type == ChannelType.Voice && !result["bitrate"].IsNullOrEmpty())
                        dc.Bitrate = result["bitrate"].ToObject<int>();

                    server.Channels.Add(dc);
                    return dc;
                }
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log("Exception ocurred while creating channel: " + ex.Message, MessageLevel.Error);
            }
            return null;
        }

        /// <summary>
        ///     Creates an empty guild with only this client in it given the following name.
        ///     Unknown if works on bot accounts or not.
        /// </summary>
        /// <param name="GuildName">The name of the guild you wish to create.</param>
        /// <returns>the created DiscordServer</returns>
        public DiscordServer CreateGuild(string GuildName)
        {
            var createGuildUrl = Endpoints.BaseAPI + Endpoints.Guilds;
            var req = JsonConvert.SerializeObject(new { name = GuildName });

            try
            {
                var response = JObject.Parse(WebWrapper.Post(createGuildUrl, token, req));
                if (response != null)
                {
                    var server = new DiscordServer();
                    server.JoinedAt = response["joined_at"].ToObject<DateTime>();
                    server.ID = response["id"].ToString();
                    server.Name = response["name"].ToString();
                    server.parentclient = this;

                    var channelGuildUrl = createGuildUrl + $"/{server.ID}" + Endpoints.Channels;
                    var channelRespone = JArray.Parse(WebWrapper.Get(channelGuildUrl, token));
                    foreach (var item in channelRespone.Children())
                        server.Channels.Add(new DiscordChannel
                        {
                            Client = this,
                            Name = item["name"].ToString(),
                            ID = item["id"].ToString(),
                            Topic = item["topic"].ToString(),
                            Private = item["is_private"].ToObject<bool>(),
                            Type = item["type"].ToObject<ChannelType>()
                        });

                    server.AddMember(Me);
                    server.Owner = server.GetMemberByKey(response["owner_id"].ToString());
                    if (server.Owner == null)
                        GetTextClientLogger.Log("Owner is null in CreateGuild!", MessageLevel.Critical);

                    ServersList.Add(server);
                    return server;
                }
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log("Exception ocurred while creating guild: " + ex.Message, MessageLevel.Error);
            }
            return null;
        }

        /// <summary>
        ///     Edits the name of the guild, given you have the permission.
        /// </summary>
        /// <param name="guild">The guild's name you wish to edit.</param>
        /// <param name="NewGuildName">The new guild name.</param>
        public void EditGuildName(DiscordServer guild, string NewGuildName)
        {
            var editGuildUrl = Endpoints.BaseAPI + Endpoints.Guilds + $"/{guild.ID}";
            var newNameJson = JsonConvert.SerializeObject(new { name = NewGuildName });
            try
            {
                WebWrapper.Patch(editGuildUrl, token, newNameJson);
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Exception ocurred while editing guild ({guild.Name}) name: " + ex.Message,
                    MessageLevel.Error);
            }
        }

        /// <summary>
        ///     Assigns a specified role to a member, given you have the permission.
        /// </summary>
        /// <param name="guild">The guild you and the user are in.</param>
        /// <param name="role">The role you wish to assign them.</param>
        /// <param name="member">The member you wish to assign the role to.</param>
        public void AssignRoleToMember(DiscordServer guild, DiscordRole role, DiscordMember member)
        {
            var url = Endpoints.BaseAPI + Endpoints.Guilds + $"/{guild.ID}" + Endpoints.Members + $"/{member.ID}";
            var message = JsonConvert.SerializeObject(new { roles = new[] { role.ID } });
            try
            {
                WebWrapper.Patch(url, token, message);
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log(
                    $"Exception ocurred while assigning role ({role.Name}) to member ({member.Username}): "
                    + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>
        ///     Assigns the specified roles to a member, given you have the permission.
        /// </summary>
        /// <param name="guild">The guild you and the user are in.</param>
        /// <param name="roles">The roles you wish to assign them.</param>
        /// <param name="member">The member you wish to assign the role to.</param>
        public void AssignRoleToMember(DiscordServer guild, List<DiscordRole> roles, DiscordMember member)
        {
            var url = Endpoints.BaseAPI + Endpoints.Guilds + $"/{guild.ID}" + Endpoints.Members + $"/{member.ID}";
            var rolesAsIds = new List<string>();
            roles.ForEach(x => rolesAsIds.Add(x.ID));
            var message = JsonConvert.SerializeObject(new { roles = rolesAsIds.ToArray() });
            try
            {
                WebWrapper.Patch(url, token, message);
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log(
                    $"Exception ocurred while assigning {roles.Count} role(s) to member ({member.Username}): "
                    + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>
        ///     Creates and invite to the given channel.
        /// </summary>
        /// <param name="channel"></param>
        /// <returns>The invite's id.</returns>
        public string CreateInvite(DiscordChannel channel)
        {
            var url = Endpoints.BaseAPI + Endpoints.Channels + $"/{channel.ID}" + Endpoints.Invites;
            try
            {
                var resopnse = JObject.Parse(WebWrapper.Post(url, token, "{\"validate\":\"\"}"));
                if (resopnse != null)
                    return resopnse["code"].ToString();
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Error ocurred while creating invite for channel {channel.Name}: {ex.Message}",
                    MessageLevel.Error);
            }
            return null;
        }

        /// <summary>
        ///     Deletes an invite by id
        /// </summary>
        /// <param name="id">The ID of the invite you wish to delete.</param>
        public void DeleteInvite(string id)
        {
            var url = Endpoints.BaseAPI + Endpoints.Invites + $"/{id}";
            try
            {
                WebWrapper.Delete(url, token);
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Error ocurred while deleting invite: {ex.Message}", MessageLevel.Error);
            }
        }

        /// <summary>
        ///     Just prepends https://discord.gg/ to a given invite :)
        /// </summary>
        /// <param name="id"></param>
        /// <returns>A full invite URL.</returns>
        public string MakeInviteURLFromCode(string id)
        {
            return "https://discord.gg/" + id;
        }

        /// <summary>
        ///     Runs the websocket connection for the client hooking up the appropriate events.
        /// </summary>
        /// <param name="useDotNetWebsocket">
        ///     If true, DiscordSharp will connect using the .Net Framework's built-in WebSocketClasses.
        ///     Please do not use this on Mono or versions of Windows below 8/8.1
        /// </param>
        public void Connect(bool useDotNetWebsocket = false)
        {
            CurrentGatewayURL = GetGatewayUrl();
            if (string.IsNullOrEmpty(CurrentGatewayURL))
            {
                GetTextClientLogger.Log("Gateway URL was null or empty?!", MessageLevel.Critical);
                return;
            }
            GetTextClientLogger.Log("Gateway retrieved: " + CurrentGatewayURL);

            if (useDotNetWebsocket)
            {
                ws = new NetWebSocket(CurrentGatewayURL);
                GetTextClientLogger.Log("Using the built-in .Net websocket..");
            }
            else
            {
                ws = new WebSocketSharpSocket(CurrentGatewayURL);
                GetTextClientLogger.Log("Using WebSocketSharp websocket..");
            }

            ws.MessageReceived += (sender, e) =>
            {
                var message = new JObject();
                try
                {
                    message = JObject.Parse(e.Message);
                }
                catch (Exception ex)
                {
                    GetTextClientLogger.Log($"MessageReceived Error: {ex.Message}\n\n```{e.Message}\n```\n",
                        MessageLevel.Error);
                }

                if (EnableVerboseLogging)
                    if (message["t"].ToString() != "READY")
                        GetTextClientLogger.Log(message.ToString(), MessageLevel.Unecessary);

                if (!message["t"].IsNullOrEmpty()) //contains a t parameter used for client events.
                    ClientPacketReceived(message);
                else
                    MiscellaneousOpcodes(message);

                if (!message["s"].IsNullOrEmpty())
                    Sequence = message["s"].ToObject<int>();
            };
            ws.SocketOpened += (sender, e) =>
            {
                SendIdentifyPacket();
                SocketOpened?.Invoke(this, null);
            };
            ws.SocketClosed += (sender, e) =>
            {
                var scev = new DiscordSocketClosedEventArgs();
                scev.Code = e.Code;
                scev.Reason = e.Reason;
                scev.WasClean = e.WasClean;
                SocketClosed?.Invoke(this, scev);

                if (Autoconnect && !e.WasClean)
                    PerformReconnection();
            };
            ws.Connect();
            GetTextClientLogger.Log("Connecting..");
        }

        private void MiscellaneousOpcodes(JObject message)
        {
            switch (message["d"].ToObject<int>())
            {
                case Opcodes.INVALIDATE_SESSION:
                    // TODO: the session was invalidated and a full reconnection must be performed.
                    GetTextClientLogger.Log($"The session was invalidated. ", MessageLevel.Critical);
                    break;
            }
        }

        private void PerformReconnection()
        {
            var resumeJson = JsonConvert.SerializeObject(new
            {
                op = Opcodes.RESUME,
                d = new
                {
                    seq = Sequence,
                    token,
                    session_id = SessionID
                }
            });
        }

        private void SendIdentifyPacket()
        {
            var initJson = JsonConvert.SerializeObject(new
            {
                op = 2,
                d = new
                {
                    v = 4,
                    token,
                    /*large_threshold = 50,*/
                    properties = DiscordProperties
                }
            });

            GetTextClientLogger.Log("Sending initJson ( " + initJson + " )");

            ws.Send(initJson);
        }

        private void BeginHeartbeatTask()
        {
            KeepAliveTaskTokenSource = new CancellationTokenSource();
            KeepAliveTaskToken = KeepAliveTaskTokenSource.Token;
            KeepAliveTask = new Task(() =>
            {
                while (true)
                {
                    GetTextClientLogger.Log("Hello from inside KeepAliveTask!");
                    Thread.Sleep(HeartbeatInterval);
                    KeepAlive();
                }
            }, KeepAliveTaskToken);
            KeepAliveTask.Start();
            GetTextClientLogger.Log("Began keepalive task..");
        }

#if NETFX4_5

        private void ConnectToVoiceAsync()
        {
            VoiceClient.InitializeOpusEncoder();
            VoiceThread = new Thread(() => VoiceClient.Initiate());
            VoiceThread.Start();
        }

#else
        private Task ConnectToVoiceAsync()
        {
            VoiceClient.InitializeOpusEncoder();
            return Task.Factory.StartNew(() => VoiceClient.Initiate());
        }
#endif

        /// <summary>
        ///     Kicks a specified DiscordMember from the guild that's assumed from their
        ///     parent property.
        /// </summary>
        /// <param name="member"></param>
        public void KickMember(DiscordMember member)
        {
            var url = Endpoints.BaseAPI + Endpoints.Guilds + $"/{member.Parent.ID}" + Endpoints.Members +
                      $"/{member.ID}";
            try
            {
                WebWrapper.Delete(url, token);
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Error during KickMember\n\t{ex.Message}\n\t{ex.StackTrace}",
                    MessageLevel.Error);
            }
        }

        /// <summary>
        ///     Bans a specified DiscordMember from the guild that's assumed from their
        ///     parent property.
        /// </summary>
        /// <param name="member"></param>
        /// <param name="days">The number of days the user should be banned for, or 0 for infinite.</param>
        public DiscordMember BanMember(DiscordMember member, int days = 0)
        {
            var url = Endpoints.BaseAPI + Endpoints.Guilds + $"/{member.Parent.ID}" + Endpoints.Bans + $"/{member.ID}";
            url += $"?delete-message-days={days}";
            try
            {
                WebWrapper.Put(url, token);
                return member;
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Error during BanMember\n\t{ex.Message}\n\t{ex.StackTrace}",
                    MessageLevel.Error);
                return null;
            }
        }

        /// <summary>
        ///     Bans a specified DiscordMember from the guild that's assumed from their
        ///     parent property.
        /// </summary>
        /// <param name="member"></param>
        /// <param name="serverOverride"></param>
        /// <param name="days"></param>
        /// <returns></returns>
        public DiscordMember BanMember(DiscordMember member, DiscordServer serverOverride, int days = 0)
        {
            var url = Endpoints.BaseAPI + Endpoints.Guilds + $"/{serverOverride.ID}" + Endpoints.Bans + $"/{member.ID}";
            url += $"?delete-message-days={days}";
            try
            {
                WebWrapper.Put(url, token);
                return member;
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Error during BanMember\n\t{ex.Message}\n\t{ex.StackTrace}",
                    MessageLevel.Error);
                return null;
            }
        }

        /// <summary>
        ///     Retrieves a DiscordMember List of members banned in the specified server.
        /// </summary>
        /// <param name="server"></param>
        /// <returns>Null if no permission.</returns>
        public List<DiscordMember> GetBans(DiscordServer server)
        {
            var returnVal = new List<DiscordMember>();
            var url = Endpoints.BaseAPI + Endpoints.Guilds + $"/{server.ID}" + Endpoints.Bans;
            try
            {
                var __res = WebWrapper.Get(url, token);
                var permissionCheck = JObject.Parse(__res);
                {
                    if (!permissionCheck["message"].IsNullOrEmpty())
                        return null; //no permission
                }
                var response = JArray.Parse(__res);
                if (response != null && response.Count > 0)
                {
                    GetTextClientLogger.Log($"Ban count: {response.Count}");

                    foreach (var memberStub in response)
                    {
                        var temp = JsonConvert.DeserializeObject<DiscordMember>(memberStub["user"].ToString());
                        if (temp != null)
                            returnVal.Add(temp);
                        else
                            GetTextClientLogger.Log(
                                $"memberStub[\"user\"] was null?! Username: {memberStub["user"]["username"]} ID: {memberStub["user"]["username"]}",
                                MessageLevel.Error);
                    }
                }
                else
                {
                    return returnVal;
                }
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log(
                    $"An error ocurred while retrieving bans for server \"{server.Name}\"\n\tMessage: {ex.Message}\n\tStack: {ex.StackTrace}",
                    MessageLevel.Error);
            }
            return returnVal;
        }

        /// <summary>
        ///     Removes a ban on the user.
        /// </summary>
        /// <param name="guild">The guild to lift the ban from.</param>
        /// <param name="userID">The ID of the user to lift the ban.</param>
        public void RemoveBan(DiscordServer guild, string userID)
        {
            var url = Endpoints.BaseAPI + Endpoints.Guilds + $"/{guild.ID}" + Endpoints.Bans + $"/{userID}";
            try
            {
                WebWrapper.Delete(url, token);
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Error during RemoveBan\n\tMessage: {ex.Message}\n\tStack: {ex.StackTrace}",
                    MessageLevel.Error);
            }
        }

        /// <summary>
        ///     Removes a ban on the user.
        /// </summary>
        /// <param name="guild">The guild to lift the ban from.</param>
        /// <param name="member">The DiscordMember object of the user to lift the ban from, assuming you have it.</param>
        public void RemoveBan(DiscordServer guild, DiscordMember member)
        {
            var url = Endpoints.BaseAPI + Endpoints.Guilds + $"/{guild.ID}" + Endpoints.Bans + $"/{member.ID}";
            try
            {
                WebWrapper.Delete(url, token);
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Error during RemoveBan\n\tMessage: {ex.Message}\n\tStack: {ex.StackTrace}",
                    MessageLevel.Error);
            }
        }

        /// <summary>
        ///     Echoes a received audio packet back.
        /// </summary>
        /// <param name="packet"></param>
        public void EchoPacket(DiscordAudioPacket packet)
        {
            if (VoiceClient != null && ConnectedToVoice())
                VoiceClient.EchoPacket(packet).Wait();
        }

        /// <summary>
        ///     Connects to a given voice channel.
        /// </summary>
        /// <param name="channel">The channel to connect to. </param>
        /// <param name="voiceConfig">The voice configuration to use. If null, default values will be used.</param>
        /// <param name="clientMuted">Whether or not the client will connect muted. Defaults to false.</param>
        /// <param name="clientDeaf">Whether or not the client will connect deaf. Defaults to false.</param>
        public void ConnectToVoiceChannel(DiscordChannel channel, DiscordVoiceConfig voiceConfig = null,
            bool clientMuted = false, bool clientDeaf = false)
        {
            if (channel.Type != ChannelType.Voice)
                throw new InvalidOperationException($"Channel '{channel.ID}' is not a voice channel!");

            if (ConnectedToVoice())
                DisconnectFromVoice();

            if (VoiceClient == null)
                if (voiceConfig == null)
                    VoiceClient = new DiscordVoiceClient(this, new DiscordVoiceConfig());
                else
                    VoiceClient = new DiscordVoiceClient(this, voiceConfig);
            VoiceClient.Channel = channel;
            VoiceClient.ErrorReceived += (sender, e) =>
            {
                if (GetLastVoiceClientLogger != null)
                {
                    GetLastVoiceClientLogger = VoiceClient.GetDebugLogger;
                    DisconnectFromVoice();
                }
            };
            VoiceClient.UserSpeaking += (sender, e) =>
            {
                if (UserSpeaking != null)
                    UserSpeaking(this, e);
            };
            VoiceClient.VoiceConnectionComplete += (sender, e) =>
            {
                if (VoiceClientConnected != null)
                    VoiceClientConnected(this, e);
            };
            VoiceClient.QueueEmpty += (sender, e) => { VoiceQueueEmpty?.Invoke(this, e); };

            var joinVoicePayload = JsonConvert.SerializeObject(new
            {
                op = 4,
                d = new
                {
                    guild_id = channel.Parent.ID,
                    channel_id = channel.ID,
                    self_mute = clientMuted,
                    self_deaf = clientDeaf
                }
            });

            ws.Send(joinVoicePayload);
        }

        /// <summary>
        ///     Clears the internal message log cache
        /// </summary>
        /// <returns>The number of internal messages cleared.</returns>
        public int ClearInternalMessageLog()
        {
            var totalCount = MessageLog.Count;
            MessageLog.Clear();
            return totalCount;
        }

        /// <summary>
        ///     Iterates through a server's members and removes offline users.
        /// </summary>
        /// <param name="server"></param>
        /// <returns>The amount of users cleared.</returns>
        public int ClearOfflineUsersFromServer(DiscordServer server)
        {
            return server.ClearOfflineMembers();
        }

        /// <summary>
        ///     Also disposes
        /// </summary>
        public void DisconnectFromVoice()
        {
            var disconnectMessage = JsonConvert.SerializeObject(new
            {
                op = 4,
                d = new
                {
                    guild_id = VoiceClient != null && VoiceClient.Channel != null
                        ? VoiceClient.Channel.Parent.ID
                        : (object)null,
                    channel_id = (object)null,
                    self_mute = true,
                    self_deaf = false
                }
            });
            if (VoiceClient != null)
                try
                {
                    VoiceClient.Dispose();
                    VoiceClient = null;

                    ws.Send(disconnectMessage);
                }
                catch
                {
                }
            if (ws != null)
                ws.Send(disconnectMessage);
            VoiceClient = null;
            if (VoiceThread != null)
                VoiceThread.Abort();
            GetTextClientLogger.Log($"Disconnected from voice. VoiceClient null: {VoiceClient == null}");
        }

        /// <summary>
        /// </summary>
        /// <returns>The current VoiceClient or null.</returns>
        public DiscordVoiceClient GetVoiceClient()
        {
            if (ConnectedToVoice() && VoiceClient != null)
                return VoiceClient;

            return null;
        }

        /// <summary>
        ///     Creates a default role in the specified guild.
        /// </summary>
        /// <param name="guild">The guild to make the role in.</param>
        /// <returns>The newly created role.</returns>
        public DiscordRole CreateRole(DiscordServer guild)
        {
            var url = Endpoints.BaseAPI + Endpoints.Guilds + $"/{guild.ID}" + Endpoints.Roles;

            try
            {
                var result = JObject.Parse(WebWrapper.Post(url, token, ""));

                if (result != null)
                {
                    var d = new DiscordRole
                    {
                        Color = new Color(result["color"].ToObject<int>().ToString("x")),
                        Hoist = result["hoist"].ToObject<bool>(),
                        ID = result["id"].ToString(),
                        Managed = result["managed"].ToObject<bool>(),
                        Name = result["name"].ToString(),
                        Permissions = new DiscordPermission(result["permissions"].ToObject<uint>()),
                        Position = result["position"].ToObject<int>()
                    };

                    ServersList.Find(x => x.ID == guild.ID).Roles.Add(d);
                    return d;
                }
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Error ocurred while creating role in guild {guild.Name}: {ex.Message}",
                    MessageLevel.Error);
            }
            return null;
        }

        /// <summary>
        ///     Edits a role with the new role information.
        /// </summary>
        /// <param name="guild">The guild the role is in.</param>
        /// <param name="newRole">the new role.</param>
        /// <returns>The newly edited role returned from Discord.</returns>
        public DiscordRole EditRole(DiscordServer guild, DiscordRole newRole)
        {
            var url = Endpoints.BaseAPI + Endpoints.Guilds + $"/{guild.ID}" + Endpoints.Roles + $"/{newRole.ID}";
            var request = JsonConvert.SerializeObject(
                new
                {
                    color = decimal.Parse(newRole.Color.ToDecimal().ToString()),
                    hoist = newRole.Hoist,
                    name = newRole.Name,
                    permissions = newRole.Permissions.GetRawPermissions()
                }
            );

            try
            {
                var result = JObject.Parse(WebWrapper.Patch(url, token, request));
                if (result != null)
                {
                    var d = new DiscordRole
                    {
                        Color = new Color(result["color"].ToObject<int>().ToString("x")),
                        Hoist = result["hoist"].ToObject<bool>(),
                        ID = result["id"].ToString(),
                        Managed = result["managed"].ToObject<bool>(),
                        Name = result["name"].ToString(),
                        Permissions = new DiscordPermission(result["permissions"].ToObject<uint>()),
                        Position = result["position"].ToObject<int>()
                    };

                    ServersList.Find(x => x.ID == guild.ID).Roles.Remove(d);
                    ServersList.Find(x => x.ID == guild.ID).Roles.Add(d);
                    return d;
                }
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Error ocurred while editing role ({newRole.Name}): {ex.Message}",
                    MessageLevel.Error);
            }

            return null;
        }

        /// <summary>
        ///     Deletes a specified role.
        /// </summary>
        /// <param name="guild">The guild the role is in.</param>
        /// <param name="role">The role to delete.</param>
        public void DeleteRole(DiscordServer guild, DiscordRole role)
        {
            var url = Endpoints.BaseAPI + Endpoints.Guilds + $"/{guild.ID}" + Endpoints.Roles + $"/{role.ID}";
            try
            {
                WebWrapper.Delete(url, token);
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log($"Error ocurred while deleting role ({role.Name}): {ex.Message}",
                    MessageLevel.Error);
            }
        }

        private JObject ServerInfo(string channelOrServerId)
        {
            var url = Endpoints.BaseAPI + Endpoints.Guilds + $"/{channelOrServerId}";
            try
            {
                return JObject.Parse(WebWrapper.Get(url, token));
            }
            catch (Exception ex)
            {
                GetTextClientLogger.Log("(Private) Error ocurred while retrieving server info: " + ex.Message,
                    MessageLevel.Error);
            }
            return null;
        }

        private int HeartbeatInterval = 41250;
        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private void KeepAlive()
        {
            //string keepAliveJson = "{\"op\":" + Opcodes.HEARTBEAT + ", \"d\":" + Sequence + "}";
            var keepAliveJson = JsonConvert.SerializeObject(new
            {
                op = Opcodes.HEARTBEAT,
                d = Sequence
            });
            if (ws != null)
            {
                ws.Send(keepAliveJson);
                KeepAliveSent?.Invoke(this,
                    new DiscordKeepAliveSentEventArgs { SentAt = DateTime.Now, JsonSent = keepAliveJson });
            }
        }

        /// <summary>
        ///     Disposes.
        /// </summary>
        public void Dispose()
        {
            try
            {
                KeepAliveTaskTokenSource.Cancel();
                ws.Close();
                ws = null;
                ServersList = null;
                PrivateChannels = null;
                Me = null;
                token = null;
                ClientPrivateInformation = null;
            }
            catch
            {
                /*already been disposed elsewhere */
            }
        }

        /// <summary>
        ///     Logs out of Discord and then disposes.
        /// </summary>
        public void Logout()
        {
            var url = Endpoints.BaseAPI + Endpoints.Auth + "/logout";
            var msg = JsonConvert.SerializeObject(new { token });
            WebWrapper.Post(url, msg);
            Dispose();
        }

        /// <summary>
        ///     Sends a login request.
        /// </summary>
        /// <returns>The token if login was succesful, or null if not</returns>
        public string SendLoginRequest()
        {
            if (token == null) //no token override provided, need to read token
            {
                if (string.IsNullOrEmpty(ClientPrivateInformation.Email))
                    throw new ArgumentNullException("Email was null/invalid!");
                StrippedEmail =
                    ClientPrivateInformation.Email.Replace('@', '_')
                        .Replace('.', '_'); //strips characters from email for hashing

                if (File.Exists(StrippedEmail.GetHashCode() + ".cache"))
                {
                    var read = "";
                    using (var sr = new StreamReader(StrippedEmail.GetHashCode() + ".cache"))
                    {
                        read = sr.ReadLine();
                        if (read.StartsWith("#")) //comment
                        {
                            token = sr.ReadLine();
                            GetTextClientLogger.Log("Loading token from cache.");
                        }
                        token = token.Trim(); //trim any excess whitespace
                    }
                }
                else
                {
                    if (ClientPrivateInformation == null || ClientPrivateInformation.Email == null ||
                        ClientPrivateInformation.Password == null)
                        throw new ArgumentNullException("You didn't supply login information!");
                    var url = Endpoints.BaseAPI + Endpoints.Auth + Endpoints.Login;
                    var msg = JsonConvert.SerializeObject(new
                    {
                        email = ClientPrivateInformation.Email,
                        password = ClientPrivateInformation.Password
                    });
                    GetTextClientLogger.Log("No token present, sending login request..");
                    var result = JObject.Parse(WebWrapper.Post(url, msg));

                    if (result["token"].IsNullOrEmpty())
                    {
                        var message = "Failed to login to Discord.";
                        if (!result["email"].IsNullOrEmpty())
                            message += " Email was invalid: " + result["email"];
                        if (!result["password"].IsNullOrEmpty())
                            message += " password was invalid: " + result["password"];

                        throw new DiscordLoginException(message);
                    }
                    token = result["token"].ToString();

                    using (var sw = new StreamWriter(StrippedEmail.GetHashCode() + ".cache"))
                    {
                        sw.WriteLine($"#Token cache for {ClientPrivateInformation.Email}");
                        sw.WriteLine(token);
                        GetTextClientLogger.Log($"{StrippedEmail.GetHashCode()}.cache written!");
                    }
                }
            }
            return token;
        }
    }
}