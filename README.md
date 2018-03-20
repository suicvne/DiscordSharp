# DiscordSharp REVIVED! 
# DiscordSharp [![Build status](https://ci.appveyor.com/api/projects/status/6ufv2gtyrc087xrd?svg=true)](https://ci.appveyor.com/project/Luigifan/discordsharp)

Welcome to the DiscordSharp dev branch!
A fork of the original DiscordSharp at [![Link](https://github.com/suicvne/DiscordSharp)].
Long term support guaranteed.

A C# API for Discord.

# From Nuget

- Unavailable for now. Please compile from source -

# How to use

Discord is what I like to call an *"event-based"* client. In other words, you get your instance of your client and hook up to its various events: either by lambda or by delegating to external voids. A simple example is as follows..

```
DiscordClient client = new DiscordClient();
client.ClientPrivateInformation.Email = "<YOUR DISCORD EMAIL>";
client.ClientPrivateInformation.Password = "<YOUR DISCORD PASSWORD>";

client.DiscordProperties.OS = "Windows";
client.DiscordProperties.Browser = "Chrome";
client.DiscordProperties.Device = string.Empty;
client.DiscordProperties.referrer = string.Empty;
client.DiscordProperties.referring_domain = string.Empty;

client.DiscordSyncedGuilds = new string[] { }; // List of Discord guild IDs to subscribe to. With this you'll be able to get information such as idle/offline/online status of users.

client.Connected += (sender, e) =>
{
  Console.WriteLine($"Connected! User: {e.User.Username}");
};
client.SendLoginRequest();
Thread t = new Thread(client.Connect);
t.Start();
```
This will get you logged in and print out a login notification to the console with the username you've logged in as.

## Example Bot
* https://github.com/NaamloosDT/DiscordSharp_Starter 
 * Kindly donated by NaamloosDT :)

# Notes
* This is such a beta it's not even funny.
* All of the internal classes are meant to model Discord's internal Json. This is why DiscordMember has a subset ("user") with the actual information.

# Cousins
We're all one big happy related family. 

Discord.Net, another great C# library - https://github.com/RogueException/Discord.Net

Discord4J, a Java library - https://github.com/austinv11/Discord4J

JDiscord, another Java library - https://github.com/NotGGhost/jDiscord

JDA, anotha one - https://github.com/DV8FromTheWorld/JDA

Javacord, anotha one - https://github.com/BtoBastian/Javacord

discord.io, a Node.js library which I referenced a lot - https://github.com/izy521/discord.io

discord.js, an alternate Node.js library - https://github.com/discord-js/discord.js

discordie, another Node.js library - https://github.com/qeled/discordie

DiscordPHP, a PHP library - https://github.com/teamreflex/DiscordPHP

discord-hypertext, the alternate php library https://github.com/Cleanse/discord-php

discordrb, a Ruby library - https://github.com/meew0/discordrb

discord.py, a Python library - https://github.com/Rapptz/discord.py

discord-akka, a Scala library - https://github.com/eaceaser/discord-akka

go-discord, a Go library (Google Go) - https://github.com/gdraynz/go-discord

discordgo, alternate Google Go library - https://github.com/bwmarrin/discordgo
