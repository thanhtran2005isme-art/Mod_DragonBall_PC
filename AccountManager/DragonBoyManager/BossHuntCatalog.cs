using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DragonBoyManager
{
    internal static class BossHuntCatalog
    {
        private static readonly object Sync = new object();

        private static int revision;

        public static int Revision
        {
            get
            {
                lock (Sync)
                    return revision;
            }
        }

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
            RememberBosses(new string[] { bossName });
        }

        public static void RememberBosses(IEnumerable<string> bossNames)
        {
            if (bossNames == null)
                return;

            lock (Sync)
            {
                try
                {
                    string path = GetCatalogPath();
                    List<string> current = ReadCatalogUnsafe(path);
                    bool changed = false;

                    foreach (string raw in bossNames)
                    {
                        string value = CanonicalizeForCatalog(raw, current);
                        if (value.Length == 0)
                            continue;

                        int before = current.Count;
                        AddUnique(current, value);
                        if (current.Count != before)
                            changed = true;
                    }

                    if (!changed)
                        return;

                    current.Sort(StringComparer.CurrentCultureIgnoreCase);
                    StringBuilder builder = new StringBuilder();
                    builder.AppendLine("# Boss Hunt catalog - one boss name per line");
                    builder.AppendLine("# Tự đồng bộ từ các Game client; có thể thêm boss thủ công.");
                    for (int i = 0; i < current.Count; i++)
                        builder.AppendLine(current[i]);

                    File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
                    revision++;
                }
                catch
                {
                }
            }
        }

        private static List<string> ReadCatalogUnsafe(string path)
        {
            List<string> current = new List<string>();
            if (!File.Exists(path))
                return current;

            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                string value = (lines[i] ?? "").Trim();
                if (value.Length == 0 || value.StartsWith("#", StringComparison.Ordinal))
                    continue;
                AddUnique(current, value);
            }
            return current;
        }

        private static string CanonicalizeForCatalog(string raw, List<string> current)
        {
            string value = (raw ?? "").Trim().Trim('[', ']', ':', '-', '.', ' ');
            if (value.Length == 0)
                return "";

            string best = "";
            for (int i = 0; i < current.Count; i++)
            {
                string candidate = current[i];
                if (value.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                    return candidate;
                if (!value.StartsWith(candidate, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (value.Length == candidate.Length)
                    return candidate;

                char next = value[candidate.Length];
                if ((char.IsWhiteSpace(next) || next == '-' || next == '(' || next == '[') &&
                    candidate.Length > best.Length)
                    best = candidate;
            }

            return best.Length > 0 ? best : value;
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
