using System;
using System.Diagnostics;
using System.IO;

namespace DragonBoyManager
{
    internal static class BossHuntDiagnostics
    {
        private static readonly object Sync = new object();

        public static void Log(string source, string eventName, int sessionId, int accountId, string bossName, string state, string detail)
        {
            try
            {
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

                string path = GetLogPath();
                lock (Sync)
                    File.AppendAllText(path, line);
            }
            catch
            {
            }
        }

        public static string GetLogPath()
        {
            try
            {
                string root = AppDomain.CurrentDomain.BaseDirectory ?? "";
                string directory = Path.Combine(root, "Data", "Errors");
                Directory.CreateDirectory(directory);
                int pid = Process.GetCurrentProcess().Id;
                return Path.Combine(directory, "BossHuntProtocol.Manager.pid" + pid + ".log");
            }
            catch
            {
                return Path.Combine("Data", "Errors", "BossHuntProtocol.Manager.fallback.log");
            }
        }

        private static string Clean(string value)
        {
            return (value ?? "").Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
