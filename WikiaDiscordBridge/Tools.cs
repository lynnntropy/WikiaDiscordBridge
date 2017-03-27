using System;

namespace WikiaDiscordBridge
{
    static class Tools
    {
        private static string Prefix => useTimestamp ? $"[{DateTime.Now}]\t" : string.Empty;

        private static bool useTimestamp;

        public static void InitLogging(bool timestamp)
        {
            useTimestamp = timestamp;
        }

        public static void Log(string location, string message)
        {
            Console.WriteLine($"{Prefix}{location}\t{message}");
        }
    }
}
