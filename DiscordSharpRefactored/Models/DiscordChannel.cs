using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordSharpRefactored.Models
{
    public class ChannelMessageDeletedArgs
    {
        public string IDDeleted { get; internal set; }
        public DiscordChannel Channel { get; internal set; }
    }

    public delegate void MessageDeleted(object sender, ChannelMessageDeletedArgs e);
    public class DiscordChannel
    {
        public string type { get; set; }
        public string name { get; set; }
        public string id { get; set; }
        public string topic { get; set; }
        public bool is_private { get; set; } = false;

        public event MessageDeleted ChannelMessageDeleted;

        public void DeleteMessage(string id)
        {
            SendDeleteRequest(id);
            if (ChannelMessageDeleted != null)
                ChannelMessageDeleted(this, new ChannelMessageDeletedArgs { IDDeleted = id, Channel = this });
        }

        private void SendDeleteRequest(string id)
        {
            string url = Endpoints.BaseAPI + Endpoints.Channels + "/" + id + Endpoints.Messages + "/" + id;
            WebWrapper.Delete(url, DiscordClient.token);
        }

        public void ChangeChannelTopic(string NewChannelTopic)
        {
            string topicChangeJson = JsonConvert.SerializeObject(new { name = name, topic = NewChannelTopic });
            string url = Endpoints.BaseAPI + Endpoints.Channels + "/" + id;

            try
            {
                var response = WebWrapper.Patch(url, DiscordClient.token, topicChangeJson);
                var result = JObject.Parse(response);
                topic = NewChannelTopic;
            }
            catch(Exception ex)
            {
                //TODO: uh idk
            }
        }
    }
}
