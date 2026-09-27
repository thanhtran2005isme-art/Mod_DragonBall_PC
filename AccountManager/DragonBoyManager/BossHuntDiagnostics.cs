using System;
using System.Diagnostics;
using System.IO;

namespace DragonBoyManager
{
    internal static class BossHuntDiagnostics
    {
        private const long MaxLogBytes = 5L * 1024L * 1024L;
        private const int MaxArchives = 3;
        private static readonly object Sync = new object();
        private static bool CleanupDone;

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
                {
                    RotateIfNeeded(path);
                    File.AppendAllText(path, line);
                }
            }
            catch
            {
            }
        }

        public static string GetLogPath()
        {
            try
            {
                string directory = GetLogDirectory();
                CleanupOldLogs(directory, "BossHuntProtocol.Manager.pid");
                int pid = Process.GetCurrentProcess().Id;
                return Path.Combine(directory, "BossHuntProtocol.Manager.pid" + pid + ".log");
            }
            catch
            {
                return Path.Combine(Path.GetTempPath(), "BossHuntProtocol.Manager.fallback.log");
            }
        }

        public static string GetLogDirectory()
        {
            try
            {
                string root = AppDomain.CurrentDomain.BaseDirectory ?? "";
                string directory = Path.Combine(root, "Data", "Errors");
                Directory.CreateDirectory(directory);
                return directory;
            }
            catch
            {
                return Path.GetTempPath();
            }
        }

        private static void RotateIfNeeded(string path)
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length < MaxLogBytes)
                    return;

                for (int i = MaxArchives; i >= 1; i--)
                {
                    string source = i == 1 ? path : path + "." + (i - 1);
                    string destination = path + "." + i;
                    if (File.Exists(destination))
                        File.Delete(destination);
                    if (File.Exists(source))
                        File.Move(source, destination);
                }
            }
            catch
            {
            }
        }

        private static void CleanupOldLogs(string directory, string prefix)
        {
            if (CleanupDone)
                return;
            CleanupDone = true;
            try
            {
                string[] files = Directory.GetFiles(directory, prefix + "*.log*");
                DateTime cutoff = DateTime.UtcNow.AddDays(-14.0);
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(files[i]) < cutoff)
                            File.Delete(files[i]);
                    }
                    catch
                    {
                    }
                }
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
