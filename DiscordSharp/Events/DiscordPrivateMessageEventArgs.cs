using System;
using DiscordSharp.Objects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DiscordSharp
{
    public class DiscordPrivateMessageEventArgs : EventArgs
    {
        public DiscordMessageDirection MessageDirection { get; internal set; }
        public DiscordPrivateChannel Channel { get; internal set; }
        public DiscordMember Author { get; internal set; }
        public string Message { get; internal set; }
        public MessageAttachment[] Attachments { get; internal set; }
        public BaseMessage BaseMessage { get; internal set; }
        public JObject RawJson { get; internal set; }
    }

    public enum DiscordMessageDirection
    {
        Default,
        In,
        Out,
    }

    public class BaseMessage
    {
        public static BaseMessage TryParse(JToken input)
        {
            if (input == null)
                return null;
            try
            {
                return input.ToObject<BaseMessage>();
            }
            catch (Exception e)
            {
                // Console.WriteLine(e);
            }
            return null;
        }

        [JsonProperty("attachments")]
        public object[] Attachments { get; set; }

        [JsonProperty("author")]
        public MessageAuthor Author { get; set; }

        [JsonProperty("channel_id")]
        public string ChannelId { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("edited_timestamp")]
        public object EditedTimestamp { get; set; }

        [JsonProperty("embeds")]
        public object[] Embeds { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("mention_everyone")]
        public bool MentionEveryone { get; set; }

        [JsonProperty("mention_roles")]
        public object[] MentionRoles { get; set; }

        [JsonProperty("mentions")]
        public object[] Mentions { get; set; }

        [JsonProperty("nonce")]
        public string Nonce { get; set; }

        [JsonProperty("pinned")]
        public bool Pinned { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty("tts")]
        public bool Tts { get; set; }

        [JsonProperty("type")]
        public long Type { get; set; }
    }

    public class MessageAuthor
    {
        [JsonProperty("avatar")]
        public object Avatar { get; set; }

        [JsonProperty("discriminator")]
        public string Discriminator { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }
    }
}