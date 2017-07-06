using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace WikiaDiscordBridge
{
    class WikiaSession
    {
        private static readonly System.Net.CookieContainer SharedCookieContainer = new System.Net.CookieContainer();
        private static readonly HttpClientHandler handler = new HttpClientHandler { CookieContainer = SharedCookieContainer };
        
        private static HttpClient loginHttpClient = new HttpClient(handler);
        private static HttpClient chatHttpClient = new HttpClient(handler);
        private static HttpClient pingHttpClient = new HttpClient(handler);

        private static readonly Dictionary<string, string> ChatRoomData = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> ChatHeaders = new Dictionary<string, string>();
        static string chatHost;

        static string botName;
        private static string wikiaName = string.Empty;
        
        private static Timer pingTimer;

        public static async Task Init(string wikia, string username, string password)
        {
            wikiaName = wikia;
            loginHttpClient.BaseAddress = new Uri($"http://{wikiaName}.wikia.com/");
            chatHttpClient.BaseAddress = new Uri($"http://{wikiaName}.wikia.com/");
            pingHttpClient.BaseAddress = new Uri($"http://{wikiaName}.wikia.com/");

            var content = new Dictionary<string, string>
            {
                { "action", "login" },
                { "lgname", username },
                { "lgpassword", password },
                { "format", "json" }, 
            };
            

            var response = await loginHttpClient.PostAsync("api.php", new FormUrlEncodedContent(content));
            while(!response.IsSuccessStatusCode) response = await loginHttpClient.PostAsync("api.php", new FormUrlEncodedContent(content));
            dynamic responseData = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());

            content.Add("lgtoken", (string)responseData["login"]["token"]);

            var cookieResponse = await loginHttpClient.PostAsync("api.php", new FormUrlEncodedContent(content));

            Tools.Log("Wikia","Init complete.");
        }

        public static async Task GetChatInfo(string username)
        {
            ChatHeaders.Add("User-Agent", "Wikia-Discord Bridge by OmegaVesko");
            //chatHeaders.Add("Content-Type", "application/octet-stream");
            ChatHeaders.Add("Accept", "*/*");
            ChatHeaders.Add("Pragma", "no-cache");
            ChatHeaders.Add("Cache-Control", "no-cache");

            var request = new HttpRequestMessage(HttpMethod.Get, "wikia.php?controller=Chat&format=json");
            foreach (var pair in ChatHeaders) request.Headers.Add(pair.Key, pair.Value);

            var response = await loginHttpClient.SendAsync(request);
            while (!response.IsSuccessStatusCode) response = await loginHttpClient.SendAsync(request);
            dynamic responseData = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());

            string chatKey = responseData.chatkey;
            string chatRoom = responseData.roomId;
            chatHost = responseData.chatServerHost;

            var cityIdRequest = new HttpRequestMessage(HttpMethod.Get, "api.php?action=query&meta=siteinfo&siprop=wikidesc&format=json");
            foreach (var pair in ChatHeaders) cityIdRequest.Headers.Add(pair.Key, pair.Value);

            var cityIdResponse = await loginHttpClient.SendAsync(cityIdRequest);
            while(!cityIdResponse.IsSuccessStatusCode) cityIdResponse = await loginHttpClient.SendAsync(cityIdRequest);
            dynamic cityIdResponseData = JsonConvert.DeserializeObject(await cityIdResponse.Content.ReadAsStringAsync());

            string chatServer = cityIdResponseData.query.wikidesc.id;

            ChatRoomData.Add("name", username);
            ChatRoomData.Add("EIO", "1:2");
            ChatRoomData.Add("transport", "polling");
            ChatRoomData.Add("key", chatKey);
            ChatRoomData.Add("roomId", chatRoom);
            ChatRoomData.Add("serverId", chatServer);
            ChatRoomData.Add("wikiId", chatServer);

            loginHttpClient = new HttpClient(handler) {BaseAddress = new Uri($"http://{chatHost}/")};
            chatHttpClient = new HttpClient(handler) {BaseAddress = new Uri($"http://{chatHost}/")};
            pingHttpClient = new HttpClient(handler) {BaseAddress = new Uri($"http://{chatHost}/")};
            
            var sessionIdRequest = new HttpRequestMessage(HttpMethod.Get, GetQueryString());
            foreach (var pair in ChatHeaders) sessionIdRequest.Headers.Add(pair.Key, pair.Value);

            var sessionIdResponse = await loginHttpClient.SendAsync(sessionIdRequest);
            while(!sessionIdResponse.IsSuccessStatusCode) sessionIdResponse = await loginHttpClient.SendAsync(sessionIdRequest);

            var content = await sessionIdResponse.Content.ReadAsStringAsync();
            dynamic sessionIdResponseData = JsonConvert.DeserializeObject(content.Substring(content.IndexOf('{')));

            ChatRoomData.Add("sid", (string) sessionIdResponseData.sid);

            botName = ChatRoomData["name"];

            Tools.Log("Wikia","Fetched server info.");
        }

        static string EncodeToRetardedFormat(string text)
        {
            return text.Length + ":" + text;
        }

        static async Task PingOnce()
        {
            var body = "1:2";
            
            var pingRequest = new HttpRequestMessage(HttpMethod.Post, GetQueryString())
            {
                Content = new StringContent(body, Encoding.UTF8, "text/plain")
            };
            foreach (var pair in ChatHeaders) pingRequest.Headers.Add(pair.Key, pair.Value);

            await pingHttpClient.SendAsync(pingRequest);
        }

        public static async Task SendMessage(string message)
        {
            var cleanMessage = "";

            // Strip anything that isn't a printable ASCII character.
            // ======
            // Unfortunately, this is a necessary measure because the web chat
            // encodes (or rather, garbles) Unicode characters in some format that 
            // I couldn't manage to replicate. Not filtering results in Wikia immediately
            // breaking your connection (depending on what the problematic character is).

            foreach (char character in message)
            {
                if (character >= 32 && character <= 126)
                {
                    cleanMessage += character;
                }
                else
                {
                    cleanMessage += "?";
                }
            }

            cleanMessage = cleanMessage
                .Replace(Environment.NewLine, @"\\n")
                .Replace("\n", @"\\n")
                .Replace(@"""", @"\\\""");

            //Console.WriteLine("Pre-re-encoding: " + cleanMessage);
            // cleanMessage = Encoding.GetEncoding("iso-8859-9").GetString(Encoding.UTF8.GetBytes(cleanMessage));
            //Console.WriteLine("Post-re-encoding: " + cleanMessage);

            Tools.Log("Discord",$"{cleanMessage}");
            string requestBody = EncodeToRetardedFormat(@"42[""message"",""{\""id\"":null,\""cid\"":\""c2079\"",\""attrs\"":{\""msgType\"":\""chat\"",\""roomId\"":\""" + ChatRoomData["roomId"] +@"\"",\""name\"":\""" + botName + @"\"",\""text\"":\""" + cleanMessage + @"\"",\""avatarSrc\"":\""\"",\""timeStamp\"":\""\"",\""continued\"":false,\""temp\"":false}}""]");
            
            var request = new HttpRequestMessage(HttpMethod.Post, GetQueryString())
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "text/plain")
            };
            foreach (var pair in ChatHeaders) request.Headers.Add(pair.Key, pair.Value);

            await chatHttpClient.SendAsync(request);
        }

        static void PingCallback(object state)
        {
            PingOnce().Wait();
        }

        public static async Task ConnectToChat()
        {
            pingTimer = new Timer(PingCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));

            while(true)
            {
                var unixTime = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                ChatRoomData["t"] = $"{unixTime}-0";
                
                var request = new HttpRequestMessage(HttpMethod.Get, GetQueryString());
                foreach (var pair in ChatHeaders) request.Headers.Add(pair.Key, pair.Value);

                var response = await chatHttpClient.SendAsync(request);
                while (!response.IsSuccessStatusCode) response = await chatHttpClient.SendAsync(request);

                var responseString = await response.Content.ReadAsStringAsync();
                
                if (responseString.Contains("Session ID unknown"))
                {
                    Tools.Log("Wikia","Server returned 'session ID unknown'. Reconnecting.");

                    //new Thread(() => { Restart(); }).Start();
                    WikiaDiscordBridge.Restart();

                    break;
                } if (responseString.Length > 20)
                {

                    if (responseString.Contains("[\"message\""))
                    {
                        while (responseString[0] != '[')
                        {
                            responseString = responseString.Substring(1);
                        }

                        while (responseString[responseString.Length-1] != ']')
                        {
                            responseString = responseString.Substring(0, responseString.Length - 1);
                        }

                        dynamic responseObject = JsonConvert.DeserializeObject(responseString);
                        dynamic responseDataObject = JsonConvert.DeserializeObject(responseObject[1].data.Value);

                        await ChatEvent(responseObject, responseDataObject);
                    }                    
                }
            }
        }

        static async Task ChatEvent(dynamic responseObject, dynamic dataObject)
        {
            if (((string)dataObject["attrs"]["name"]).ToLower() != botName.ToLower())
            {
                var name = (string) dataObject["attrs"]["name"];

                string text = "";
                if (dataObject["attrs"]["text"] != null)
                {
                    text = ParseClientSideMessageMarkup((string)dataObject["attrs"]["text"]);
                }

                switch ((string)responseObject[1]["event"])
                {
                    case "chat:add":
                        Tools.Log("Wikia",$"{name}: {text}");
                        await DiscordSession.SendMessage($"**{name}**: {text}");
                        break;

                    case "join":
                        Tools.Log("Wikia",$"{name} has joined the chat.");
                        await DiscordSession.SendMessage($"**{name}** has joined the chat.");
                        break;

                    case "logout":
                        Tools.Log("Wikia",$"{name} has left the chat.");
                        await DiscordSession.SendMessage($"**{name}** has left the chat.");
                        break;

                    case "part":
                        Tools.Log("Wikia",$"{name} has left the chat.");
                        await DiscordSession.SendMessage($"**{name}** has left the chat.");
                        break;
                }
            }
        }

        static string ParseClientSideMessageMarkup(string message)
        {
            string processedMessage = message;

            if (message.StartsWith("/me"))
            {
                processedMessage = "*" + processedMessage.Substring(4) + "*";
            }

            if (Regex.IsMatch(message, @"\[\[.+\]\]"))
            {
                processedMessage = Regex.Replace(processedMessage, @"\[\[(.+?)\]\]", delegate (Match match)
                {
                    string resourceName = match.Groups[1].Value;
                    resourceName = resourceName.Replace(" ", "_");
                    resourceName = Uri.EscapeUriString(resourceName);

                    return $"http://{wikiaName}.wikia.com/wiki/{resourceName}";
                });
            }

            return processedMessage;
        }

        static string GetQueryString()
        {
            var requestStringBuilder = new StringBuilder("socket.io/?");
            foreach (var pair in ChatRoomData) requestStringBuilder.Append($"{pair.Key}={Uri.EscapeDataString(pair.Value)}&");
            return requestStringBuilder.ToString().TrimEnd('&');
        }
    }
}
