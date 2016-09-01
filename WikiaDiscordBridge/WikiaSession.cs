using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RestSharp;
using Newtonsoft.Json;

using System.Threading;
using System.Text.RegularExpressions;

namespace WikiaDiscordBridge
{
    class WikiaSession
    {
        static System.Net.CookieContainer SharedCookieContainer = new System.Net.CookieContainer();

        static RestClient LoginRestClient = new RestClient($"http://{WikiaDiscordBridge.Config["wikia_name"]}.wikia.com");
        static RestClient ChatRestClient = new RestClient($"http://{WikiaDiscordBridge.Config["wikia_name"]}.wikia.com");
        static RestClient PingRestClient = new RestClient($"http://{WikiaDiscordBridge.Config["wikia_name"]}.wikia.com");

        static Dictionary<string, string> ChatRoomData = new Dictionary<string, string>();
        static Dictionary<string, string> ChatHeaders = new Dictionary<string, string>();
        static string ChatHost;

        static string BotName;


        static void Login()
        {
            LoginRestClient.CookieContainer = SharedCookieContainer;
            ChatRestClient.CookieContainer = SharedCookieContainer;
            PingRestClient.CookieContainer = SharedCookieContainer;

            var request = new RestRequest("/api.php", Method.POST);
            request.AddParameter("action", "login");
            request.AddParameter("lgname", WikiaDiscordBridge.Config["wikia_username"]);
            request.AddParameter("lgpassword", WikiaDiscordBridge.Config["wikia_password"]);
            request.AddParameter("format", "json");

            var response = LoginRestClient.Execute(request);
            dynamic responseData = JsonConvert.DeserializeObject(response.Content);

            request.AddParameter("lgtoken", responseData.login.token);

            var cookieResponse = LoginRestClient.Execute(request);

            Console.WriteLine("Login complete.");
        }

        public static void GetChatInfo()
        {
            Login();

            ChatHeaders.Add("User-Agent", "Wikia-Discord Bridge by OmegaVesko");
            ChatHeaders.Add("Content-Type", "application/octet-stream");
            ChatHeaders.Add("Accept", "*/*");
            ChatHeaders.Add("Pragma", "no-cache");
            ChatHeaders.Add("Cache-Control", "no-cache");

            var request = new RestRequest("/wikia.php", Method.GET);
            foreach (var pair in ChatHeaders) request.AddHeader(pair.Key, pair.Value);

            request.AddParameter("controller", "Chat");
            request.AddParameter("format", "json");

            var response = LoginRestClient.Execute(request);
            dynamic responseData = JsonConvert.DeserializeObject(response.Content);

            string chatKey = responseData.chatkey;
            string chatRoom = responseData.roomId;
            ChatHost = responseData.chatServerHost;
            string chatPort = responseData.chatServerPort;
            string chatMod = responseData.isModerator;

            var cityIdRequest = new RestRequest("/api.php", Method.GET);
            foreach (var pair in ChatHeaders) cityIdRequest.AddHeader(pair.Key, pair.Value);

            cityIdRequest.AddParameter("action", "query");
            cityIdRequest.AddParameter("meta", "siteinfo");
            cityIdRequest.AddParameter("siprop", "wikidesc");
            cityIdRequest.AddParameter("format", "json");

            var cityIdResponse = LoginRestClient.Execute(cityIdRequest);
            dynamic cityIdResponseData = JsonConvert.DeserializeObject(cityIdResponse.Content);

            string chatServer = cityIdResponseData.query.wikidesc.id;

            ChatRoomData.Add("name", WikiaDiscordBridge.Config["wikia_username"]);
            ChatRoomData.Add("EIO", "1:2");
            ChatRoomData.Add("transport", "polling");
            ChatRoomData.Add("key", chatKey);
            ChatRoomData.Add("roomId", chatRoom);
            ChatRoomData.Add("serverId", chatServer);
            ChatRoomData.Add("wikiId", chatServer);

            LoginRestClient.BaseUrl = new Uri($"http://{ChatHost}");
            ChatRestClient.BaseUrl = new Uri($"http://{ChatHost}");
            PingRestClient.BaseUrl = new Uri($"http://{ChatHost}");

            var sessionIdRequest = new RestRequest($"/socket.io/", Method.GET);
            foreach (var pair in ChatRoomData) sessionIdRequest.AddParameter(pair.Key, pair.Value);
            foreach (var pair in ChatHeaders) sessionIdRequest.AddHeader(pair.Key, pair.Value);

            var sessionIdResponse = LoginRestClient.Execute(sessionIdRequest);

            dynamic sessionIdResponseData = JsonConvert.DeserializeObject(sessionIdResponse.Content.Substring(5));

            ChatRoomData.Add("sid", (string) sessionIdResponseData.sid);

            BotName = ChatRoomData["name"];

            Console.WriteLine("Fetched server info.");
        }

        static string EncodeToRetardedFormat(string text)
        {
            return text.Length + ":" + text;
        }

        static void PingOnce()
        {
            var body = "1:2";

            var pingRequest = new RestRequest($"/socket.io/", Method.POST);
            pingRequest.AddParameter("text/plain;charset=UTF-8", body, ParameterType.RequestBody);
            foreach (var pair in ChatRoomData) pingRequest.AddParameter(pair.Key, pair.Value, ParameterType.QueryString);
            foreach (var pair in ChatHeaders) pingRequest.AddHeader(pair.Key, pair.Value);

            var response = PingRestClient.Execute(pingRequest);
        }

        public static void SendMessage(string message)
        {
            // Escape newlines, as this is the format Wikia expects to see.
            var cleanMessage = message
                .Replace(Environment.NewLine, @"\\n")
                .Replace("\n", @"\\n");

            // Remove emoji, as Wikia doesn't support them (usually, connection instantly breaks)
            cleanMessage = Regex.Replace(cleanMessage, @"\p{Cs}", "?");

            Console.WriteLine($"Sending: \"{cleanMessage}\"");
            string requestBody = EncodeToRetardedFormat(@"42[""message"",""{\""id\"":null,\""cid\"":\""c2079\"",\""attrs\"":{\""msgType\"":\""chat\"",\""roomId\"":\""" + ChatRoomData["roomId"] +@"\"",\""name\"":\""" + BotName + @"\"",\""text\"":\""" + cleanMessage + @"\"",\""avatarSrc\"":\""\"",\""timeStamp\"":\""\"",\""continued\"":false,\""temp\"":false}}""]");

            var request = new RestRequest($"/socket.io/", Method.POST);
            request.AddParameter("text/plain;charset=UTF-8", requestBody, ParameterType.RequestBody);
            foreach (var pair in ChatRoomData) request.AddParameter(pair.Key, pair.Value, ParameterType.QueryString);
            foreach (var pair in ChatHeaders) request.AddHeader(pair.Key, pair.Value);

            var response = ChatRestClient.Execute(request);
        }

        static void PingContinuously()
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                while (true)
                {
                    PingOnce();
                    Thread.Sleep(10000);
                }

            }).Start();
        }

        public static void ConnectToChat()
        {
            PingContinuously();

            while(true)
            {
                var unixTime = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                ChatRoomData["t"] = $"{unixTime}-0";

                var request = new RestRequest("/socket.io/");
                foreach (var pair in ChatRoomData) request.AddParameter(pair.Key, pair.Value);
                foreach (var pair in ChatHeaders) request.AddHeader(pair.Key, pair.Value);

                var response = ChatRestClient.Execute(request);
                
                if (response.Content.Contains("Session ID unknown"))
                {
                    Console.WriteLine("Error: Server returned 'session ID unknown'.");
                    Console.WriteLine($"Response content is:\n{response.Content}");

                    WikiaDiscordBridge.Restart();
                }
                else if (response.Content.Length > 20)
                {

                    var responseString = response.Content;

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

                        ChatEvent(responseObject, responseDataObject);
                    }                    
                }
            }
        }

        static void ChatEvent(dynamic responseObject, dynamic dataObject)
        {
            if (((string)dataObject["attrs"]["name"]).ToLower() != BotName.ToLower())
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
                        Console.WriteLine($"{name}: {text}");
                        DiscordSession.SendMessage($"**{name}**: {text}");
                        break;

                    case "join":
                        Console.WriteLine($"{name} has joined the chat.");
                        DiscordSession.SendMessage($"**{name}** has joined the chat.");
                        break;

                    case "logout":
                        Console.WriteLine($"{name} has left the chat.");
                        DiscordSession.SendMessage($"**{name}** has left the chat.");
                        break;

                    case "part":
                        Console.WriteLine($"{name} has left the chat.");
                        DiscordSession.SendMessage($"**{name}** has left the chat.");
                        break;
                }
            }
        }

        static string ParseClientSideMessageMarkup(string message)
        {
            if (message.StartsWith("/me"))
            {
                return "*" + message.Substring(4) + "*";
            }
            else
            {
                return message;
            }
        }
    }
}
