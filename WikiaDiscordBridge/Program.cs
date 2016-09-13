using Discord;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
                Config = deserializer.Deserialize(streamReader);
                                
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

            new Thread(() =>
            {
                int time = 1000 * 60 * int.Parse(Config["restart_timer"]);

                Thread.Sleep(time);

                Restart();

            }).Start();
        }

        public static void Restart()
        {
            // System.Diagnostics.Process.Start(Assembly.GetExecutingAssembly().Location);
            Environment.Exit(0);
        }
    }
}
