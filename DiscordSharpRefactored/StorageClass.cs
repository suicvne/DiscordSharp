using DiscordSharpRefactored.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordSharpRefactored
{
    internal class StorageClass
    {
        public static List<DiscordServer> ServersList = new List<DiscordServer>();
        public static List<KeyValuePair<string, DiscordMessage>> MessageLog = new List<KeyValuePair<string, DiscordMessage>>();
    }
}
