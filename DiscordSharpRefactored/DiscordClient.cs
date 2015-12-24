using DiscordSharpRefactored.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace DiscordSharpRefactored
{
    public class DiscordClient
    {
        public static string token { get; set; }
        public string CurrentGatewayURL { get; set; }
        public DiscordUserInformation ClientPrivateInformation { get; set; }
        public static DiscordUser Me { get; internal set; }
        private WebSocket MainWebSocket;
        private int HeartbeatInterval { get; set; }
        private static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);


        public DiscordClient()
        {
            if (ClientPrivateInformation == null)
                ClientPrivateInformation = new DiscordUserInformation();
        }

        private string GetGatewayUrl()
        {
            string url = Endpoints.BaseAPI + Endpoints.Gateway;
            JObject result = JObject.Parse(WebWrapper.Get(url, token));
            return result["url"].ToString();
        }

        public DiscordServer GetServer(string name)
        {
            return StorageClass.ServersList.Find(x => x.name == name);
        }

        public DiscordServer GetServer(long id)
        {
            return StorageClass.ServersList.Find(x => x.id == id.ToString());
        }

        internal void OnMessageDeleted(ChannelMessageDeletedArgs cmda)
        {
            StorageClass.MessageLog.Remove(StorageClass.MessageLog.Find(x => x.Key == cmda.IDDeleted));
        }

        private void GetChannelsList(JObject m)
        {
            if (StorageClass.ServersList == null)
                StorageClass.ServersList = new List<DiscordServer>();
            foreach (var j in m["d"]["guilds"])
            {
                DiscordServer temp = new DiscordServer();
                temp.id = j["id"].ToString();
                temp.name = j["name"].ToString();
                temp.owner_id = j["owner_id"].ToString();
                List<DiscordChannel> tempSubs = new List<DiscordChannel>();
                foreach (var u in j["channels"])
                {
                    DiscordChannel tempSub = new DiscordChannel();
                    tempSub.id = u["id"].ToString();
                    tempSub.name = u["name"].ToString();
                    tempSub.type = u["type"].ToString();
                    tempSub.topic = u["topic"].ToString();
                    tempSub.ChannelMessageDeleted += (sender, e) => OnMessageDeleted(e);
                    tempSubs.Add(tempSub);
                }
                temp.channels = tempSubs;
                foreach (var mm in j["members"])
                {
                    
                    DiscordUser member = new DiscordUser();
                    member.id = mm["user"]["id"].ToString();
                    member.username = mm["user"]["username"].ToString();
                    member.avatar = mm["user"]["avatar"].ToString();
                    member.discriminator = mm["user"]["discriminator"].ToString();
                    temp.members.Add(member);
                }
                StorageClass.ServersList.Add(temp);
            }
        }

        private void WebsocketConnection()
        {
            CurrentGatewayURL = GetGatewayUrl();
            MainWebSocket = new WebSocket(CurrentGatewayURL);
            MainWebSocket.EnableRedirection = true;
            MainWebSocket.Log.File = "websocketlog.txt";
            MainWebSocket.OnMessage += (sender, e) =>
            {
                var message = JObject.Parse(e.Data);
                switch(message["t"].ToString())
                {
                    case ("READY"):
                        Me = new DiscordUser
                        {
                            username = message["d"]["user"]["username"].ToString(),
                            id = message["d"]["user"]["id"].ToString(),
                            verified = message["d"]["user"]["verified"].ToObject<bool>(),
                            avatar = message["d"]["user"]["avatar"].ToString(),
                            discriminator = message["d"]["user"]["discriminator"].ToString(),
                            email = message["d"]["user"]["email"].ToString()
                        };
                        ClientPrivateInformation.avatar = Me.avatar;
                        ClientPrivateInformation.username = Me.username;
                        HeartbeatInterval = message["d"]["heartbeat_interval"].ToObject<int>();
                        GetChannelsList(message);
                        //TODO: Connected event
                        break;
                }
            };
            MainWebSocket.OnOpen += (sender, e) =>
            {
                string initObj = JsonConvert.SerializeObject(new { op = 2, d = new { token = token } });
                MainWebSocket.Send(initObj);
                //TODO: Socket opened event
                if (HeartbeatInterval == 0)
                    HeartbeatInterval = 1;
                Thread keepAliveTimer = new Thread(KeepAlive);
                keepAliveTimer.Start();
            };
            MainWebSocket.OnClose += (sender, e) =>
            {
                Console.WriteLine(e.Code + " " + e.Reason);
            };
            MainWebSocket.Connect();
        }

        private void KeepAlive()
        {
            string msg = JsonConvert.SerializeObject(new { op = 1, d = DateTime.Now.Millisecond });
            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Elapsed += (sender, e) =>
            {
                if(MainWebSocket != null)
                {
                    if(MainWebSocket.IsAlive)
                    {
                        int unixTime = (int)(DateTime.UtcNow - epoch).TotalMilliseconds;
                        msg = JsonConvert.SerializeObject(new { op = 1, d = unixTime });
                        MainWebSocket.Send(msg);

                        //TODO: KeepAliveSent event
                    }
                }
            };
            timer.Interval = HeartbeatInterval;
            timer.Enabled = true;
        }

        public void Dispose()
        {
            try
            {
                MainWebSocket.Close();
                MainWebSocket = null;
                Me = null;
                token = null;
                ClientPrivateInformation = null;
                //TODO: Dispose storage class
            }
            catch { /**Already been disposed*/ }
        }

        public void Login()
        {
            if(ClientPrivateInformation == null || ClientPrivateInformation.email == null || ClientPrivateInformation.password == null)
                throw new ArgumentNullException("You didn't supply login information!");

            string msg = JsonConvert.SerializeObject(new { email = ClientPrivateInformation.email, password = ClientPrivateInformation.password });
            string url = Endpoints.BaseAPI + Endpoints.Auth + Endpoints.Login;

            var potentialToken = JObject.Parse( WebWrapper.Post(url, msg));
            if(potentialToken["token"].ToString().Trim() != "")
            {
                token = potentialToken["token"].ToString();
            }

            WebsocketConnection();
        }
    }
}
