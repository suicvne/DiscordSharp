using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;

namespace DiscordSharpRefactored
{
    public class DiscordClient
    {
        public string token { get; set; }
        public string CurrentGatewayURL { get; set; }
        public DiscordUserInformation ClientPrivateInformation { get; set; }
        public DiscordUser Me { get; internal set; }
        private WebSocket MainWebSocket;


        public DiscordClient()
        {
            if (ClientPrivateInformation == null)
                ClientPrivateInformation = new DiscordUserInformation();
        }

        private string GetGatewayUrl()
        {
            string url = Endpoints.BaseAPI + Endpoints.Gateway;
            JObject result = JObject.Parse( WebWrapper.Get(url, token));
            return result["url"].ToString();
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
            };
            MainWebSocket.OnOpen += (sender, e) =>
            {

            };
            MainWebSocket.Connect();
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
        }
    }
}
