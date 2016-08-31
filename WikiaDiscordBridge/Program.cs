using Discord;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WikiaDiscordBridge
{
    class WikiaDiscordBridge
    {
        
        public static dynamic Config;

        static void Main(string[] args)
        {
            using (var streamReader = new StreamReader("config.yaml"))
            {
                var deserializer = new YamlDotNet.Serialization.Deserializer();
                WikiaDiscordBridge.Config = deserializer.Deserialize(streamReader);
                                
                Console.WriteLine("Successfully loaded configuration.");
            }

            new Thread(() =>
            {
                try
                {
                    DiscordSession.Connect();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }

            }).Start();

            new Thread(() =>
            {
                try
                {
                    WikiaSession.GetChatInfo();
                    WikiaSession.ConnectToChat();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }

            }).Start();
        }

    }
}
