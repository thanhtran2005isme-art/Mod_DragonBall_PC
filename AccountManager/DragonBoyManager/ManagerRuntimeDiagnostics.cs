using System;
using System.IO;

namespace DragonBoyManager
{
    internal static class ManagerRuntimeDiagnostics
    {
        private static readonly object Sync = new object();

        public static void Log(string source, Exception ex)
        {
            Log(source, ex == null ? "" : ex.ToString());
        }

        public static void Log(string source, string detail)
        {
            try
            {
                string root = AppDomain.CurrentDomain.BaseDirectory ?? "";
                string directory = Path.Combine(Path.Combine(root, "Data"), "Errors");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "ManagerRuntime.log");
                string line =
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " +
                    "source=" + (source ?? "") + Environment.NewLine +
                    (detail ?? "") + Environment.NewLine +
                    "------------------------------------------------------------" + Environment.NewLine;
                lock (Sync)
                    File.AppendAllText(path, line);
            }
            catch
            {
            }
        }
    }
}
