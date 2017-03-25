using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WikiaDiscordBridge
{
    class WikiaDiscordBridge
    {        
        private static dynamic config;
        private static CancellationTokenSource cts;

        static void Main(string[] args) => StartAsync(args).GetAwaiter().GetResult();

        private static async Task StartAsync(string[] args)
        {
            using (var fileStream = new FileStream("config.yaml", FileMode.Open))
            using (var streamReader = new StreamReader(fileStream))
            {
                var deserializer = new YamlDotNet.Serialization.Deserializer();
                config = deserializer.Deserialize(streamReader);
                                
                Console.WriteLine("Successfully loaded configuration.");
            }

            await WikiaSession.Init((string)config["wikia_name"], (string)config["wikia_username"], (string)config["wikia_password"]);
            await WikiaSession.GetChatInfo((string)config["wikia_username"]);

            string botToken = config["discord_bot_token"];
            ulong discordChannel = ulong.Parse(config["discord_channel"]);
            string wikiaName = config["wikia_name"];

            await DiscordSession.Init(botToken, discordChannel,
                wikiaName);
            
            cts = new CancellationTokenSource(TimeSpan.FromMinutes(int.Parse(config["restart_timer"])));
			var completionSource = new TaskCompletionSource<object>();
            cts.Token.Register(() => completionSource.TrySetCanceled());
            await Task.WhenAny(WikiaSession.ConnectToChat(), completionSource.Task);
        }

        public static void Restart()
        {
            cts.Cancel();
        }
    }
}
