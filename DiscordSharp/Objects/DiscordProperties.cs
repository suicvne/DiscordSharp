using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DiscordSharp.Objects
{
    /// <summary>
    /// Properties that Discord uses upon connection to the websocket. Mostly used for analytics internally.
    /// </summary>
    public class DiscordProperties
    {
        /// <summary>
        /// The OS you're on
        /// </summary>
        [JsonProperty("$os")]
        public string OS { get; set; }

        /// <summary>
        /// The "browser" you're using.
        /// </summary>
        [JsonProperty("$browser")]
        public string Browser { get; set; }

        /// <summary>
        /// Whatever device you want to be on. (Default: DiscordSharp Bot)
        /// </summary>
        [JsonProperty("$device")]
        public string Device
        { get; set; } = "DiscordSharp Bot";

        /// <summary>
        ///
        /// </summary>
        [JsonProperty("$referrer")]
        public string referrer { get; set; }

        /// <summary>
        ///
        /// </summary>
        [JsonProperty("$referring_domain")]
        public string referring_domain { get; set; }

        /// <summary>
        /// Default constructor setting the OS property to Environment.OSVersion.ToString();
        /// </summary>
        public DiscordProperties()
        {
            OS = Environment.OSVersion.ToString();
        }

        /// <summary>
        /// Serializes this object as json.
        /// </summary>
        /// <returns>Json string of this object serialized</returns>
        public string AsJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}