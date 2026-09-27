using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DragonBoyManager
{
    internal static class BossHuntCatalog
    {
        private static readonly object Sync = new object();

        public static string GetCatalogPath()
        {
            string root = AppDomain.CurrentDomain.BaseDirectory ?? "";
            string data = Path.Combine(root, "Data");
            Directory.CreateDirectory(data);
            return Path.Combine(data, "BossHuntBosses.txt");
        }

        public static List<string> GetBossNames()
        {
            List<string> result = new List<string>();
            lock (Sync)
            {
                try
                {
                    string path = GetCatalogPath();
                    if (!File.Exists(path))
                        return result;

                    string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string value = (lines[i] ?? "").Trim();
                        if (value.Length == 0 || value.StartsWith("#", StringComparison.Ordinal))
                            continue;
                        AddUnique(result, value);
                    }
                }
                catch
                {
                }
            }
            return result;
        }

        public static void RememberBoss(string bossName)
        {
            bossName = (bossName ?? "").Trim();
            if (bossName.Length == 0)
                return;

            lock (Sync)
            {
                try
                {
                    string path = GetCatalogPath();
                    List<string> current = new List<string>();
                    if (File.Exists(path))
                    {
                        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            string value = (lines[i] ?? "").Trim();
                            if (value.Length == 0 || value.StartsWith("#", StringComparison.Ordinal))
                                continue;
                            AddUnique(current, value);
                        }
                    }

                    for (int i = 0; i < current.Count; i++)
                    {
                        if (current[i].Equals(bossName, StringComparison.OrdinalIgnoreCase))
                            return;
                    }

                    File.AppendAllText(path, bossName + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                }
            }
        }

        private static void AddUnique(List<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].Equals(value, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            values.Add(value);
        }
    }
}
