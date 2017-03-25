using System;

namespace WikiaDiscordBridge
{
    static class Tools
    {
        public static void Log(string location, string message)
        {
            Console.WriteLine($"[{DateTime.Now}]\t{location}\t{message}");
        }
    }
}
