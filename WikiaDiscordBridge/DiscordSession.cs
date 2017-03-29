using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace WikiaDiscordBridge
{
    class DiscordSession
    {
        private static readonly DiscordSocketClient client = new DiscordSocketClient( new DiscordSocketConfig
        {
            LogLevel = LogSeverity.Error
        });

        private static ulong trackedChannelId;
        private static string wikiaName;

        static DiscordSession()
        {
            client.Connected += () =>
            {
                Tools.Log("Discord", $"Connected to Discord as {client.CurrentUser.Username}#{client.CurrentUser.DiscriminatorValue}.");
                return Task.CompletedTask;
            };
            client.MessageReceived += async msg =>
            {
                if (msg.Channel.Id != trackedChannelId || msg.Author.Id == client.CurrentUser.Id) return;

                var displayName = string.IsNullOrWhiteSpace((msg.Author as IGuildUser)?.Nickname)
                    ? msg.Author.Username
                    : ((IGuildUser) msg.Author).Nickname;

                if (msg.Attachments.Count > 0)
                {
                    foreach (var attachment in msg.Attachments)
                    {
                        await WikiaSession.SendMessage($"{displayName}: {attachment.Url}");
                    }
                }

                if (string.IsNullOrWhiteSpace(msg.Content)) return;

                await WikiaSession.SendMessage($"{displayName}: {msg.Content}");

                var matches = Regex.Matches(msg.Content, @"\[{2}(.+?)\]{2}");
                if (matches.Count <= 0) return;
                foreach (Match match in matches)
                {
                    var resourceName = match.Groups[1].Value.Replace(" ", "_");
                    var escapedName = Uri.EscapeUriString(resourceName);

                    await msg.Channel.SendMessageAsync($"<http://{wikiaName}.wikia.com/wiki/{escapedName}>");
                }
            };
        }

        public static async Task Init(string token, ulong channelId, string wikia)
        {
            trackedChannelId = channelId;
            wikiaName = wikia;
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();
        }

        public static async Task SendMessage(string message)
        {
            if (client.GetChannel(trackedChannelId) is IMessageChannel channel)
            {
                await channel.SendMessageAsync(message);
            }
        }
    }
}
