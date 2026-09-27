using System;
using System.Diagnostics;
using System.IO;

namespace AssemblyCSharp.Functions
{
    public static class BossHuntDiagnostics
    {
        private const long MaxLogBytes = 5L * 1024L * 1024L;
        private const int MaxArchives = 3;
        private static readonly object Sync = new object();
        private static bool CleanupDone;

        public static void Log(string source, string eventName, int sessionId, string bossName, string state, string detail)
        {
            try
            {
                string mapName = "";
                int mapId = -1;
                int zone = -1;
                try
                {
                    mapId = GClass20.int_37;
                    mapName = GClass20.string_1 ?? "";
                    zone = GClass20.int_39;
                }
                catch
                {
                }

                string line =
                    "source=" + Clean(source) +
                    "|event=" + Clean(eventName) +
                    "|session=" + sessionId +
                    "|account=" + GClass150.int_0 +
                    "|boss=" + Clean(bossName) +
                    "|state=" + Clean(state) +
                    "|mapId=" + mapId +
                    "|map=" + Clean(mapName) +
                    "|zone=" + zone +
                    "|detail=" + Clean(detail);

                string path = GetLogPath();
                lock (Sync)
                {
                    RotateIfNeeded(path);
                    GClass149.smethod_2(path, line);
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
                string root = AppDomain.CurrentDomain.BaseDirectory ?? "";
                string directory = Path.Combine(Path.Combine(root, "Data"), "Errors");
                Directory.CreateDirectory(directory);
                CleanupOldLogs(directory, "BossHuntProtocol.Game.pid");
                int pid = Process.GetCurrentProcess().Id;
                return Path.Combine(directory, "BossHuntProtocol.Game.pid" + pid + ".log");
            }
            catch
            {
                return Path.Combine(Path.GetTempPath(), "BossHuntProtocol.Game.fallback.log");
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
