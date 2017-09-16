using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Net.Queue;
using Discord.WebSocket;

namespace WikiaDiscordBridge
{
    class DiscordSession
    {
        private static readonly DiscordSocketClient client = new DiscordSocketClient( new DiscordSocketConfig
        {
            LogLevel = LogSeverity.Info
        });

        private static ulong trackedChannelId;
        private static string wikiaName;
        private static readonly Regex LinkRegex = new Regex(@"\[{2}([\w!""#$%&'()*+,\-./:;<=>?@[\]^`{|}~\ ]+?)\]{2}", RegexOptions.Compiled);

        static DiscordSession()
        {
            client.Connected += () =>
            {
                Tools.Log("Discord", $"Connected to Discord as {client.CurrentUser.Username}#{client.CurrentUser.DiscriminatorValue}.");
                return Task.CompletedTask;
            };
            client.Log += msg =>
            {
                Tools.Log("Discord", $"{msg.Severity}: {msg.Message}");
                return Task.CompletedTask;
            };
            client.MessageReceived += async msg =>
            {
                var userMessage = msg as IUserMessage;
                if(userMessage == null) return;

                if (userMessage.Author.Id == client.CurrentUser.Id) return;

                var matches = LinkRegex.Matches(userMessage.Content);
                if (matches.Count > 0)
                {
                    foreach (Match match in matches)
                    {
                        var resourceName = match.Groups[1].Value.Replace(" ", "_");
                        var escapedName = Uri.EscapeDataString(resourceName);

                        await userMessage.Channel.SendMessageAsync($"<http://{wikiaName}.wikia.com/wiki/{escapedName}>");
                    }
                }


                if (userMessage.Channel.Id != trackedChannelId) return;

                var displayName = string.IsNullOrWhiteSpace((userMessage.Author as IGuildUser)?.Nickname)
                    ? userMessage.Author.Username
                    : ((IGuildUser) userMessage.Author).Nickname;

                if (userMessage.Attachments.Count > 0)
                {
                    foreach (var attachment in userMessage.Attachments)
                    {
                        await WikiaSession.SendMessage($"{displayName}: {attachment.Url}");
                    }
                }

                if (string.IsNullOrWhiteSpace(userMessage.Content)) return;

                await WikiaSession.SendMessage($"{displayName}: {userMessage.Resolve()}");
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
