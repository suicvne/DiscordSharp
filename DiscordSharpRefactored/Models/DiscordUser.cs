using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiscordSharpRefactored
{
    public class DiscordUser
    {
        public string username { get; internal set; }
        public string id { get; internal set; }
        public string discriminator { get; internal set; }
        public string avatar { get; internal set; }
        public bool verified { get; internal set; }
        public string email { get; internal set; }

        public void Mention()
        {
            throw new NotImplementedException();
        }

        public void SendDirectMessage(string message)
        {
            string initMessage = JsonConvert.SerializeObject(new { recipient_id = id });
            string url = Endpoints.BaseAPI + Endpoints.Users + $"/{DiscordClient.Me.id}" + Endpoints.Channels;
            var result = JObject.Parse(WebWrapper.Post(url, initMessage));
            if(result != null)
            {
                SendActualMessage(result["id"].ToString(), message);
            }
        }

        private void SendActualMessage(string id, string message)
        {
            string url = Endpoints.BaseAPI + Endpoints.Channels + $"/{id}" + Endpoints.Messages;
            WebWrapper.Post(url, JsonConvert.SerializeObject(GenerateMessage(message)));
        }

        /// <summary>
        /// Used internally to generate a proper DiscordMessage with mentions and whatnot.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private DiscordMessage GenerateMessage(string message)
        {
            DiscordMessage dm = new DiscordMessage();
            dm.content = message;
            return dm;
        }
    }
}
