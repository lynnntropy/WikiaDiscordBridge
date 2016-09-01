using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiaDiscordBridge
{
    class DiscordSession
    {
        static DiscordClient DiscordClient;
        static Channel TrackedChannel;

        public static void Connect()
        {
            DiscordSession.DiscordClient = new DiscordClient(x =>
            {
                x.AppName = "WikiaDiscordBridge";
            });

            DiscordSession.DiscordClient.ExecuteAndWait(async () =>
            {
                while (true)
                {
                    try
                    {
                        await DiscordClient.Connect(WikiaDiscordBridge.Config["discord_bot_token"]);
                        break;
                    }
                    catch (Exception ex)
                    {
                        DiscordClient.Log.Error("Login Failed", ex);
                        await Task.Delay(DiscordClient.Config.FailedReconnectDelay);
                    }
                }

                await Task.Delay(5000); // Not everything is instantly loaded if using a bot account.

                TrackedChannel = DiscordClient.GetChannel(ulong.Parse(WikiaDiscordBridge.Config["discord_channel"]));

                Console.WriteLine($"Connected to Discord as @{DiscordClient.CurrentUser.Name}.");

                DiscordClient.MessageReceived += (s, e) =>
                {
                    if (e.Channel.Id == TrackedChannel.Id)
                    {
                        if (e.User.Id != DiscordClient.CurrentUser.Id)
                        {
                            if (e.Message.Attachments.Count() > 0)
                            {
                                WikiaSession.SendMessage($"{e.User.Name}: {e.Message.Attachments[0].Url}");
                            }
                            else
                            {
                                WikiaSession.SendMessage($"{e.User.Name}: {e.Message.Text}");
                            }
                        }
                    }
                };
            });
        }

        public static void SendMessage(string message)
        {
           TrackedChannel.SendMessage(message);
        }
    }
}
