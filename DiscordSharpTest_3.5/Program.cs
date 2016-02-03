using DiscordSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace DiscordSharpTest_3._5
{
    class TestClient
    {
        private DiscordClient client;
        private Thread clientThread;

        public TestClient()
        {
            client = new DiscordClient();

            SetupEvents();
        }

        public TestClient(string email, string password)
        {
            client = new DiscordClient();
            client.ClientPrivateInformation = new DiscordUserInformation();
            client.ClientPrivateInformation.email = email;
            client.ClientPrivateInformation.password = password;

            SetupEvents();
        }

        private void SetupEvents()
        {
            client.MessageReceived += (sender, e) =>
            {
                Console.WriteLine($"[{e.Channel.parent.name}->{e.Channel.name}] <{e.author.Username}> {e.message}");
                if(e.author.Username == "Axiom") //that me
                {
                    if(e.message.content.StartsWith("?testjoinvoice"))
                    {
                        string[] split = e.message.content.Split(new char[] { ' ' }, 2);
                        if(split.Length > 1)
                        {
                            DiscordChannel potentialChannel = e.Channel.parent.channels.Find(x => x.name.ToLower() == split[1].ToLower() && x.type == "voice");
                            if(potentialChannel != null)
                            {
                                client.ConnectToVoiceChannel(potentialChannel, true, true);
                            }
                            else
                            {
                                e.Channel.SendMessage($"Couldn't find voice channel named '{split[1]}'!");
                            }
                        }
                    }
                }
            };
            client.Connected += (sender, e) =>
            {
                Console.WriteLine("Connected as user " + e.user.Username);
                client.UpdateCurrentGame("DiscordSharp testing.");
            };
        }

        public void Run()
        {
            if (client.SendLoginRequest() != null)
            {
                clientThread = new Thread(client.Connect);
                clientThread.Start();
            }
            else
            {
                Console.WriteLine("Couldn't login.");
            }
        }

        public void Dispose()
        {
            client.Logout();
            clientThread.Abort();
            client = null;
        }
    }

    class Program
    {
        public static void Main(string[] args)
        {
            TestClient client = new TestClient();
            if(File.Exists("credentials.txt"))
            {
                using (var sr = new StreamReader("credentials.txt"))
                {
                    string email, password;
                    email = sr.ReadLine();
                    password = sr.ReadLine();
                    client = new TestClient(email, password);
                }
            }
            else
            {
                Console.WriteLine("Need credentials.txt...");
                Environment.Exit(-1);
            }

            client.Run();
            Console.ReadLine();
        }
    }
}
