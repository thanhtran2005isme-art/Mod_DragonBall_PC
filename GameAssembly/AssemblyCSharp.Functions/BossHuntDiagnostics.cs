using System;
using System.Diagnostics;
using System.IO;

namespace AssemblyCSharp.Functions
{
    public static class BossHuntDiagnostics
    {
        private static readonly object Sync = new object();

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

                lock (Sync)
                    GClass149.smethod_2(GetLogPath(), line);
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
                int pid = Process.GetCurrentProcess().Id;
                return Path.Combine(directory, "BossHuntProtocol.Game.pid" + pid + ".log");
            }
            catch
            {
                return Path.Combine(Path.GetTempPath(), "BossHuntProtocol.Game.fallback.log");
            }
        }

        private static string Clean(string value)
        {
            return (value ?? "").Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
