using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiscordSharpRefactored.Models
{
    public class DiscordServer
    {
        public string id { get; set; }
        public string name { get; set; }
        public string owner_id { get; set; }
        public List<DiscordChannel> channels { get; set; }
        public List<DiscordUser> members { get; set; }

        public DiscordServer()
        {
            channels = new List<DiscordChannel>();
            members = new List<DiscordUser>();
        }

        /// <summary>
        /// Returns a DiscordMessage List of the specified number, count, of messages before or after an optional message ID.
        /// </summary>
        /// <param name="count">The amount of messages to return.</param>
        /// <param name="idBefore">(Optional) Return messages before this specified message ID</param>
        /// <param name="idAfter">(Optional) Return messages after this specified message ID</param>
        /// <returns></returns>
        public List<DiscordMessage> GetChannelHistory(DiscordChannel channel, int count, int? idBefore, int? idAfter)
        {
            if (channel.is_private)
                return null;

            string url = Endpoints.BaseAPI + Endpoints.Channels + id + $"/messages?&limit={count}";
            if (idBefore != null)
                url += $"&before={idBefore}";
            if (idAfter != null)
                url += $"&after={idAfter}";

            var returnedMessage = JArray.Parse(WebWrapper.Get(url, DiscordClient.token));
            if (returnedMessage != null)
            {
                List<DiscordMessage> messageList = new List<DiscordMessage>();

                foreach (var item in returnedMessage.Children())
                {
                    messageList.Add(new DiscordMessage
                    {
                        id = item["id"].ToString(),
                        channel = channel,
                        author = GetMember(item["author"]["id"].ToObject<long>()),
                        content = item["content"].ToString(),
                        mentions = item["mentions"].ToObject<string[]>(),
                        RawJson = item.ToObject<JObject>(),
                        timestamp = DateTime.Parse(item["timestamp"].ToString())
                    });
                }
                return messageList;
            }

            return null;
        }

        /// <summary>
        /// Used internally to generate a proper DiscordMessage with mentions and whatnot.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private DiscordMessage GenerateMessage(string message)
        {
            DiscordMessage dm = new DiscordMessage();
            List<string> foundIDS = new List<string>();
            Regex r = new Regex("\\@\\w+");
            List<KeyValuePair<string, string>> toReplace = new List<KeyValuePair<string, string>>();
            foreach(Match m in r.Matches(message))
            {
                if (m.Index > 0 && message[m.Index - 1] == '<')
                    continue;
                DiscordUser u = members.Find(x => x.username == m.Value.Trim('@'));
                foundIDS.Add(u.id);
                toReplace.Add(new KeyValuePair<string, string>(m.Value, u.id));
            }
            foreach(var k in toReplace)
            {
                message = message.Replace(k.Key, "<@" + k.Value + ">");
            }

            dm.content = message;
            dm.mentions = null;
            return dm;
        }

        public DiscordUser GetMember(long id) => members.Find(x => x.id == id.ToString());

    }
}
