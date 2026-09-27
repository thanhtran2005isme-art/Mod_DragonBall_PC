using System;
using System.IO;

namespace DragonBoyManager
{
    internal static class BossHuntDiagnostics
    {
        private const string LogPath = "Data/Errors/BossHuntProtocol.log";
        private static readonly object Sync = new object();

        public static void Log(string source, string eventName, int sessionId, int accountId, string bossName, string state, string detail)
        {
            try
            {
                string directory = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                string line =
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " +
                    "source=" + Clean(source) +
                    "|event=" + Clean(eventName) +
                    "|session=" + sessionId +
                    "|account=" + accountId +
                    "|boss=" + Clean(bossName) +
                    "|state=" + Clean(state) +
                    "|detail=" + Clean(detail) +
                    Environment.NewLine;

                lock (Sync)
                    File.AppendAllText(LogPath, line);
            }
            catch
            {
            }
        }

        private static string Clean(string value)
        {
            return (value ?? "").Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
