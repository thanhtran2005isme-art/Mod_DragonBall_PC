using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AssemblyCSharp.Functions
{
    public class GClass156
    {
        public static bool bool_0;

        public string string_0;

        public string string_1;

        public int int_0;

        public int int_1 = -1;

        public DateTime dateTime_0;

        public static List<GClass156> list_0 = new List<GClass156>();

        public static List<GClass156> list_1 = new List<GClass156>();

        public static int int_2 = ((GClass122.int_12 == 2) ? 7 : 10);

        public static int int_3 = 0;

        private static int int_4 = 5;

        private static int int_5 = ((GClass122.int_12 == 2) ? 40 : 35);

        private static int int_6 = 0;

        public static List<GClass156> list_2 = new List<GClass156>();

        public static List<GClass156> list_3 = new List<GClass156>();

        public static List<GClass156> list_4 = new List<GClass156>();

        public static List<GClass156> list_5 = new List<GClass156>();

        public static List<GClass156> list_6 = new List<GClass156>();

        public static List<GClass156> list_7 = new List<GClass156>();

        public static List<GClass156> list_8 = new List<GClass156>();

        public static List<GClass156> list_9 = new List<GClass156>();

        public static List<GClass156> list_10 = new List<GClass156>();

        public static List<GClass156> list_11 = new List<GClass156>();

        public static List<GClass156> list_12 = new List<GClass156>();

        public static List<GClass156> list_13 = new List<GClass156>();

        public static List<GClass156> list_14 = new List<GClass156>();

        public static List<GClass156> list_15 = new List<GClass156>();

        public static List<GClass156> list_16 = new List<GClass156>();

        public static List<GClass156> list_17 = new List<GClass156>();

        public static List<GClass156> list_18 = new List<GClass156>();

        public static List<GClass156> list_19 = new List<GClass156>();

        public static List<GClass156> list_20 = new List<GClass156>();

        public static string[] string_2 = new string[19]
        {
            "[Số 4]", "[Số 3]", "[Số 2]", "[Số 1]", "[Tiểu đội trưởng]", "[Fide Đại Ca]", "[Android 19][Dr.Kôrê]", "[Android 13][Android 14][Android 15]", "[Pic][Poc][Kingkong]", "[Xên bọ hung]",
            "[Xên hoàn thiện]", "[Xên con]", "[Siêu bọ hung]", "[Black Goku]", "[Cooler]", "[Super Broly]", "[Fide Vàng]", "[Biệt đội Bojack]", "[Cumber]"
        };

        public static string[] string_3 = new string[19]
        {
            "[Guldo]", "[Recoome]", "[Jeice]", "[Burter]", "[Captain Ginyu]", "[Frieza]", "[Android 19][Dr.Kôrê]", "[Android 13][Android 14][Android 15]", "[Pic][Poc][Kingkong]", "[Cell]",
            "[Perfect Cell]", "[Cell Jr]", "[Super Cell]", "[Black Goku]", "[Cooler]", "[Super Broly]", "[Golden Frieza]", "[Bojack]", "[Cumber]"
        };

        public static List<GClass156> list_21 = new List<GClass156>();

        private static readonly object bossLocationLock = new object();

        private static readonly Dictionary<string, GClass156> bossLocationCache = new Dictionary<string, GClass156>(StringComparer.OrdinalIgnoreCase);

        private const double BossLocationFreshnessMinutes = 60.0;

        private GClass156(string name, string map)
        {
            string_0 = name;
            string_1 = map;
            if (!(map == ((mResources.language != 0) ? "Spaceship station" : "Trạm tàu vũ trụ")))
                int_0 = smethod_6(map);
            else if (name.StartsWith("Số ") || name.StartsWith("Tiểu đội") || name.StartsWith("Captain Ginyu") || name.StartsWith("Guldo") || name.StartsWith("Recoome") || name.StartsWith("Jeice") || name.StartsWith("Burter"))
            {
                int_0 = 25;
            }
            else if (name.Contains("Bojack") || name.StartsWith("Bujin") || name.StartsWith("Bido") || name.StartsWith("Zangya") || name.StartsWith("Bido"))
            {
                int_0 = 24;
            }
            dateTime_0 = DateTime.Now;
        }

        public static void smethod_0(List<GClass156> list, string name, string map)
        {
            list.Add(new GClass156(name, map));
            if (list == list_7 && list.Count > 3)
                list.RemoveAt(0);
            if ((list == list_2 || list == list_3 || list == list_4 || list == list_5 || list == list_6) && list.Count > 2)
                list.RemoveAt(0);
            if (list == list_9 && list.Count > 3)
                list.RemoveAt(0);
            if (list == list_10 && list.Count > 3)
                list.RemoveAt(0);
            if (list == list_8 && list.Count > 2)
                list.RemoveAt(0);
            if (list == list_15 && list.Count > 6)
                list.RemoveAt(0);
            if (list == list_12 && list.Count > 3)
                list.RemoveAt(0);
            if (list == list_13 && list.Count > 7)
                list.RemoveAt(0);
            if (list == list_17 && list.Count > 5)
                list.RemoveAt(0);
            if (list == list_18 && list.Count > 5)
                list.RemoveAt(0);
            if (list == list_16 && list.Count > 2)
                list.RemoveAt(0);
            if (list == list_19 && list.Count > 6)
                list.RemoveAt(0);
        }

        public static void smethod_1(string name, string map)
        {
            switch (mResources.language)
            {
                case 1:
                    switch (name)
                    {
                        case "Super Black Goku 2":
                        case "Super Black Goku 0":
                        case "Super Black Goku 1":
                            smethod_0(list_15, name, map);
                            break;
                        case "Bujin":
                        case "Bojack":
                        case "Bido":
                        case "Zangya":
                        case "Kogu":
                        case "Super Bojack":
                            smethod_0(list_19, name, map);
                            break;
                        case "Cell":
                            smethod_0(list_11, name, map);
                            break;
                        case "Super Cell":
                            smethod_0(list_14, name, map);
                            break;
                        case "King kong":
                        case "Pic":
                        case "Poc":
                            smethod_0(list_10, name, map);
                            break;
                        case "Cumber":
                            smethod_0(list_20, name, map);
                            break;
                        case "Android 13":
                        case "Android 14":
                            smethod_0(list_9, name, map);
                            break;
                        case "Frieza 1":
                        case "Frieza 2":
                        case "Frieza 3":
                            smethod_0(list_7, name, map);
                            break;
                        case "Recoome":
                            smethod_0(list_3, name, map);
                            break;
                        case "Dr.Kôrê":
                            smethod_0(list_8, name, map);
                            break;
                        case "Burter":
                            smethod_0(list_5, name, map);
                            break;
                        case "Jeice":
                            smethod_0(list_4, name, map);
                            break;
                        case "Captain Ginyu":
                            smethod_0(list_6, name, map);
                            break;
                        case "Black Goku 0":
                        case "Black Goku 1":
                        case "Black Goku 2":
                            smethod_0(list_15, name, map);
                            break;
                        case "Guldo":
                            smethod_0(list_2, name, map);
                            break;
                        case "Android 15":
                            smethod_0(list_9, name, map);
                            break;
                        case "Android 19":
                            smethod_0(list_8, name, map);
                            break;
                        case "Perfect Cell":
                            smethod_0(list_12, name, map);
                            break;
                        default:
                            if (name.StartsWith("Super Broly"))
                                smethod_0(list_17, name, map);
                            else if (name.StartsWith("Cooler"))
                                smethod_0(list_16, name, map);
                            else if (map.StartsWith("Cell Jr"))
                                smethod_0(list_13, name, map);
                            else if (name.ToLower().StartsWith("gold"))
                                smethod_0(list_18, name, map);
                            break;
                    }
                    break;
                case 0:
                    switch (name)
                    {
                        case "Pic":
                        case "Poc":
                        case "King kong":
                            smethod_0(list_10, name, map);
                            break;
                        case "Android 13":
                            smethod_0(list_9, name, map);
                            break;
                        case "Cumber":
                            smethod_0(list_20, name, map);
                            break;
                        case "Dr.Kôrê":
                        case "Android 19":
                            smethod_0(list_8, name, map);
                            break;
                        case "Android 14":
                        case "Android 15":
                            smethod_0(list_9, name, map);
                            break;
                        case "Super Black Goku 0":
                        case "Super Black Goku 1":
                        case "Super Black Goku 2":
                            smethod_0(list_15, name, map);
                            break;
                        case "Xên bọ hung":
                            smethod_0(list_11, name, map);
                            break;
                        case "Siêu Bojack":
                        case "Bojack":
                        case "Zangya":
                        case "Bujin":
                        case "Kogu":
                        case "Bido":
                            smethod_0(list_19, name, map);
                            break;
                        case "Siêu bọ hung":
                            smethod_0(list_14, name, map);
                            break;
                        case "Black Goku 0":
                        case "Black Goku 1":
                        case "Black Goku 2":
                            smethod_0(list_15, name, map);
                            break;
                        case "Số 4":
                            smethod_0(list_2, name, map);
                            break;
                        case "Số 1":
                            smethod_0(list_5, name, map);
                            break;
                        case "Số 2":
                            smethod_0(list_4, name, map);
                            break;
                        case "Số 3":
                            smethod_0(list_3, name, map);
                            break;
                        case "Fide Đại Ca 1":
                        case "Fide Đại Ca 2":
                        case "Fide Đại Ca 3":
                            smethod_0(list_7, name, map);
                            break;
                        case "Xên hoàn thiện":
                            smethod_0(list_12, name, map);
                            break;
                        case "Tiểu đội trưởng":
                            smethod_0(list_6, name, map);
                            break;
                        default:
                            if (name.StartsWith("Super Broly"))
                                smethod_0(list_17, name, map);
                            else if (name.StartsWith("Fide Vàng"))
                                smethod_0(list_18, name, map);
                            else if (!name.StartsWith("Cooler"))
                                if (map.StartsWith("Xên con"))
                                    smethod_0(list_13, name, map);
                            else
                                smethod_0(list_16, name, map);
                            break;
                    }
                    break;
            }
        }

        public static void smethod_2(string chatVip)
        {
            string deadBossName;
            if (TryParseBossDeathAnnouncement(chatVip, out deadBossName))
            {
                InvalidateBossLocation(deadBossName);
                return;
            }

            string bossName;
            string mapName;
            int mapId;
            int zone;
            if (!TryParseBossAnnouncement(chatVip, out bossName, out mapName, out mapId, out zone))
                return;

            GClass156 item = new GClass156(bossName, mapName);
            item.int_1 = zone;
            CacheBossLocation(item);
            list_0.Add(item);
            smethod_1(bossName, mapName);
            if (list_0.Count > 5)
                list_0.RemoveAt(0);
        }

        public static bool TryGetLatestBossLocation(string targetBossName, out GClass156 boss)
        {
            boss = null;
            DateTime now = DateTime.Now;
            lock (bossLocationLock)
            {
                foreach (KeyValuePair<string, GClass156> pair in bossLocationCache)
                {
                    GClass156 item = pair.Value;
                    if (item == null || !BossLocationNameMatches(item.string_0, targetBossName))
                        continue;
                    if (now.Subtract(item.dateTime_0).TotalMinutes > BossLocationFreshnessMinutes)
                        continue;
                    if (boss == null || item.dateTime_0 > boss.dateTime_0)
                        boss = item;
                }
            }
            return boss != null;
        }

        public static List<GClass156> GetBossLocationSnapshot()
        {
            List<GClass156> result = new List<GClass156>();
            lock (bossLocationLock)
            {
                foreach (GClass156 item in bossLocationCache.Values)
                {
                    if (item == null)
                        continue;
                    GClass156 copy = new GClass156(item.string_0, item.string_1);
                    copy.int_0 = item.int_0;
                    copy.int_1 = item.int_1;
                    copy.dateTime_0 = item.dateTime_0;
                    result.Add(copy);
                }
            }
            return result;
        }

        public static void ApplyBossLocationSync(string bossName, string mapName, int mapId, int zone, long observedAtUtcTicks)
        {
            if (string.IsNullOrEmpty(bossName) || mapId < 0)
                return;

            GClass156 item = new GClass156(bossName.Trim(), mapName ?? "");
            item.int_0 = mapId;
            item.int_1 = zone;
            if (observedAtUtcTicks > 0L)
            {
                try
                {
                    item.dateTime_0 = new DateTime(observedAtUtcTicks, DateTimeKind.Utc).ToLocalTime();
                }
                catch
                {
                    item.dateTime_0 = DateTime.Now;
                }
            }
            CacheBossLocation(item);
        }

        public static void InvalidateBossLocation(string bossName)
        {
            string normalized = NormalizeBossLocationName(bossName);
            if (normalized.Length == 0)
                return;

            lock (bossLocationLock)
            {
                List<string> keys = new List<string>();
                foreach (KeyValuePair<string, GClass156> pair in bossLocationCache)
                {
                    GClass156 item = pair.Value;
                    if (item == null)
                        continue;
                    if (BossLocationNameMatches(item.string_0, normalized) || BossLocationNameMatches(normalized, item.string_0))
                        keys.Add(pair.Key);
                }
                for (int i = 0; i < keys.Count; i++)
                    bossLocationCache.Remove(keys[i]);
            }
        }

        private static void CacheBossLocation(GClass156 item)
        {
            if (item == null || item.int_0 < 0)
                return;
            string key = NormalizeBossLocationName(item.string_0);
            if (key.Length == 0)
                return;
            lock (bossLocationLock)
                bossLocationCache[key] = item;
        }

        private static string NormalizeBossLocationName(string value)
        {
            return (value ?? "").Trim().Trim('[', ']', ':', '-', '.', ' ');
        }

        private static bool BossLocationNameMatches(string actual, string target)
        {
            actual = NormalizeBossLocationName(actual);
            target = NormalizeBossLocationName(target);
            if (actual.Length == 0 || target.Length == 0)
                return false;
            if (actual.Equals(target, StringComparison.OrdinalIgnoreCase))
                return true;
            if (!actual.StartsWith(target, StringComparison.OrdinalIgnoreCase))
                return false;
            if (actual.Length == target.Length)
                return true;
            char next = actual[target.Length];
            return char.IsWhiteSpace(next) || next == '-' || next == '(' || next == '[';
        }

        public static bool TryParseBossDeathAnnouncement(string chatVip, out string bossName)
        {
            string killer;
            return TryParseBossDeathAnnouncement(chatVip, out bossName, out killer);
        }

        public static bool TryParseBossDeathAnnouncement(string chatVip, out string bossName, out string killer)
        {
            bossName = "";
            killer = "";
            if (string.IsNullOrEmpty(chatVip))
                return false;

            string text = chatVip.Trim();
            if (text.StartsWith("!", StringComparison.Ordinal))
                text = text.Substring(1).Trim();
            if (text.StartsWith("BOSS ", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(5).Trim();

            string lower = text.ToLowerInvariant();

            string[] directByMarkers = new string[]
            {
                " vừa bị tiêu diệt bởi ", " đã bị tiêu diệt bởi ", " bị tiêu diệt bởi ",
                " vừa bị hạ gục bởi ", " đã bị hạ gục bởi ", " bị hạ gục bởi ",
                " vừa bị đánh bại bởi ", " đã bị đánh bại bởi ", " bị đánh bại bởi ",
                " vừa bị hạ bởi ", " đã bị hạ bởi ", " bị hạ bởi ",
                " has been defeated by ", " was defeated by ",
                " has been killed by ", " was killed by ",
                " defeated by ", " killed by "
            };

            int directCut = -1;
            string directMarker = "";
            for (int i = 0; i < directByMarkers.Length; i++)
            {
                int index = lower.IndexOf(directByMarkers[i], StringComparison.Ordinal);
                if (index >= 0 && (directCut < 0 || index < directCut))
                {
                    directCut = index;
                    directMarker = directByMarkers[i];
                }
            }

            if (directCut >= 0)
            {
                bossName = NormalizeBossLocationName(text.Substring(0, directCut));
                killer = CleanKiller(text.Substring(directCut + directMarker.Length));
                return bossName.Length > 0;
            }

            string[] passiveMarkers = new string[]
            {
                " vừa bị ", " đã bị ", " bị "
            };
            for (int i = 0; i < passiveMarkers.Length; i++)
            {
                int index = lower.IndexOf(passiveMarkers[i], StringComparison.Ordinal);
                if (index <= 0)
                    continue;

                string remainder = text.Substring(index + passiveMarkers[i].Length).Trim();
                string parsedKiller;
                if (!TryParseDeathRemainder(remainder, out parsedKiller))
                    continue;

                bossName = NormalizeBossLocationName(text.Substring(0, index));
                killer = parsedKiller;
                return bossName.Length > 0;
            }

            string[] directDeathMarkers = new string[]
            {
                " vừa chết", " đã chết", " chết",
                " has been defeated", " was defeated",
                " has been killed", " was killed",
                " is dead"
            };
            int cut = -1;
            for (int i = 0; i < directDeathMarkers.Length; i++)
            {
                int index = lower.IndexOf(directDeathMarkers[i], StringComparison.Ordinal);
                if (index >= 0 && (cut < 0 || index < cut))
                    cut = index;
            }
            if (cut < 0)
                return false;

            bossName = NormalizeBossLocationName(text.Substring(0, cut));
            return bossName.Length > 0;
        }

        public static bool LooksLikeBossDeathAnnouncement(string chatVip)
        {
            if (string.IsNullOrEmpty(chatVip))
                return false;

            string lower = chatVip.ToLowerInvariant();
            return lower.IndexOf("tiêu diệt", StringComparison.Ordinal) >= 0 ||
                   lower.IndexOf("hạ gục", StringComparison.Ordinal) >= 0 ||
                   lower.IndexOf("đánh bại", StringComparison.Ordinal) >= 0 ||
                   lower.IndexOf(" bị hạ", StringComparison.Ordinal) >= 0 ||
                   lower.IndexOf(" đã chết", StringComparison.Ordinal) >= 0 ||
                   lower.IndexOf(" vừa chết", StringComparison.Ordinal) >= 0 ||
                   lower.IndexOf(" killed", StringComparison.Ordinal) >= 0 ||
                   lower.IndexOf(" defeated", StringComparison.Ordinal) >= 0 ||
                   lower.IndexOf(" is dead", StringComparison.Ordinal) >= 0;
        }

        private static bool TryParseDeathRemainder(string remainder, out string killer)
        {
            killer = "";
            if (string.IsNullOrEmpty(remainder))
                return false;

            string lower = remainder.ToLowerInvariant();
            string[] actions = new string[]
            {
                "tiêu diệt", "hạ gục", "đánh bại", "hạ", "giết",
                "killed", "defeated", "slain"
            };

            int actionIndex = -1;
            string action = "";
            for (int i = 0; i < actions.Length; i++)
            {
                int index = lower.IndexOf(actions[i], StringComparison.Ordinal);
                if (index >= 0 && (actionIndex < 0 || index < actionIndex))
                {
                    actionIndex = index;
                    action = actions[i];
                }
            }
            if (actionIndex < 0)
                return false;

            if (actionIndex > 0)
            {
                killer = CleanKiller(remainder.Substring(0, actionIndex));
                return true;
            }

            string afterAction = remainder.Substring(action.Length).Trim();
            string lowerAfter = afterAction.ToLowerInvariant();
            if (lowerAfter.StartsWith("bởi ", StringComparison.Ordinal))
                killer = CleanKiller(afterAction.Substring(4));
            else if (lowerAfter.StartsWith("by ", StringComparison.Ordinal))
                killer = CleanKiller(afterAction.Substring(3));
            return true;
        }

        private static string CleanKiller(string value)
        {
            string result = (value ?? "").Trim().Trim('.', '!', ':', '-', '[', ']', ' ');
            string lower = result.ToLowerInvariant();
            if (lower.StartsWith("người chơi ", StringComparison.Ordinal))
                result = result.Substring("người chơi ".Length).Trim();
            return result;
        }


        public static bool TryParseBossAnnouncement(string chatVip, out string bossName, out string mapName, out int mapId, out int zone)
        {
            bossName = "";
            mapName = "";
            mapId = -1;
            zone = -1;

            if (string.IsNullOrEmpty(chatVip) || !chatVip.StartsWith("BOSS", StringComparison.OrdinalIgnoreCase))
                return false;

            string text = chatVip.Trim();
            if (text.StartsWith("BOSS ", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(5);

            text = text.Replace(" vừa xuất hiện tại ", "|")
                .Replace(" appear at ", "|")
                .Replace(" khu vực ", "|")
                .Replace(" zone ", "|");

            string[] array = text.Split('|');
            if (array.Length < 2)
                return false;

            bossName = array[0].Trim();
            mapName = array[1].Trim();
            if (bossName.Length == 0 || mapName.Length == 0)
                return false;

            GClass156 parsed = new GClass156(bossName, mapName);
            mapId = parsed.int_0;

            if (array.Length >= 3)
            {
                int parsedZone;
                if (int.TryParse(array[2].Trim(), out parsedZone))
                    zone = parsedZone;
            }

            return mapId >= 0;
        }

        public static string smethod_3(GClass156 boss)
        {
            TimeSpan timeSpan = DateTime.Now.Subtract(boss.dateTime_0);
            string text = "[" + boss.string_0 + "] [" + boss.string_1 + "] ";
            int num = (int)Math.Floor((decimal)timeSpan.TotalHours);
            if (num > 0)
                text += $"{num}h";
            if (timeSpan.Minutes > 0)
                text += $"{timeSpan.Minutes}m";
            return text + $"{timeSpan.Seconds}s";
        }

        public static string smethod_4(GClass156 boss)
        {
            TimeSpan timeSpan = DateTime.Now.Subtract(boss.dateTime_0);
            string text = "";
            int num = (int)Math.Floor((decimal)timeSpan.TotalHours);
            if (num > 0)
                text += $"{num}h";
            if (timeSpan.Minutes > 0)
                text += $"{timeSpan.Minutes}m";
            return text + $"{timeSpan.Seconds}s";
        }

        public static void smethod_5(GClass122 g)
        {
            if (!bool_0 || GClass153.smethod_0().method_6())
                return;
            try
            {
                GUIStyle[] array = new GUIStyle[list_0.Count];
                GUIStyle[] array2 = new GUIStyle[list_0.Count];
                for (int i = 0; i < list_0.Count; i++)
                {
                    array[i] = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.UpperRight,
                        fontSize = ((GClass122.int_12 == 2) ? 12 : 9),
                        fontStyle = FontStyle.Bold,
                        richText = true
                    };
                    array2[i] = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.UpperRight,
                        fontSize = ((GClass122.int_12 == 2) ? 12 : 9),
                        fontStyle = FontStyle.Bold,
                        richText = true
                    };
                    array2[i].normal.textColor = Color.black;
                    array[i].normal.textColor = Color.yellow;
                    GClass156 gClass = list_0[i];
                    if (gClass == null)
                        continue;
                    if (GClass20.int_37 == gClass.int_0)
                    {
                        array[i].normal.textColor = Color.green;
                        for (int j = 0; j < GClass144.gclass88_5.method_2(); j++)
                        {
                            if (((GClass78)GClass144.gclass88_5.method_3(j)).string_3 == gClass.string_0)
                            {
                                array[i].normal.textColor = Color.red;
                                break;
                            }
                        }
                    }
                    int_6 = GClass118.smethod_2(GClass154.smethod_0().method_4(array[i], (GClass151.int_0 != 0) ? (smethod_3(gClass) + " [Goto]") : (smethod_3(gClass) + " [Đến]")), int_6);
                    int num = GClass73.int_10 - int_4;
                    int_3 = GClass118.smethod_2(GClass154.smethod_0().method_5(array[i], (GClass151.int_0 == 0) ? (smethod_3(gClass) + " [Đến]") : (smethod_3(gClass) + " [Goto]")), int_3);
                    int num2 = int_5 + ((GClass122.int_12 == 2) ? 7 : 10) * i + ((GClass122.int_12 == 2) ? 3 : 0);
                    g.method_20(13959423, 0.3f);
                    g.method_15(num - ((GClass122.int_12 == 2) ? 16 : 23), num2 + ((GClass122.int_12 == 2) ? 1 : 5), (GClass122.int_12 == 2) ? 17 : 23, (GClass122.int_12 == 2) ? 7 : 8);
                    g.method_19((GClass151.int_0 == 0) ? (smethod_3(gClass) + " [Đến]") : (smethod_3(gClass) + " [Goto]"), (float)(-int_4) - 0.5f, (float)num2 - 0.5f, array2[i]);
                    g.method_19((GClass151.int_0 != 0) ? (smethod_3(gClass) + " [Goto]") : (smethod_3(gClass) + " [Đến]"), (float)(-int_4) - 0.5f, (float)num2 + 0.5f, array2[i]);
                    g.method_19((GClass151.int_0 != 0) ? (smethod_3(gClass) + " [Goto]") : (smethod_3(gClass) + " [Đến]"), (float)(-int_4) + 0.5f, (float)num2 - 0.5f, array2[i]);
                    g.method_19((GClass151.int_0 != 0) ? (smethod_3(gClass) + " [Goto]") : (smethod_3(gClass) + " [Đến]"), (float)(-int_4) + 0.5f, (float)num2 + 0.5f, array2[i]);
                    g.method_19((GClass151.int_0 == 0) ? (smethod_3(gClass) + " [Đến]") : (smethod_3(gClass) + " [Goto]"), -int_4, (float)num2 - 0.5f, array2[i]);
                    g.method_19((GClass151.int_0 == 0) ? (smethod_3(gClass) + " [Đến]") : (smethod_3(gClass) + " [Goto]"), -int_4, (float)num2 + 0.5f, array2[i]);
                    g.method_19((GClass151.int_0 == 0) ? (smethod_3(gClass) + " [Đến]") : (smethod_3(gClass) + " [Goto]"), (float)(-int_4) - 0.5f, num2, array2[i]);
                    g.method_19((GClass151.int_0 == 0) ? (smethod_3(gClass) + " [Đến]") : (smethod_3(gClass) + " [Goto]"), (float)(-int_4) + 0.5f, num2, array2[i]);
                    g.method_19((GClass151.int_0 == 0) ? (smethod_3(gClass) + " [Đến]") : (smethod_3(gClass) + " [Goto]"), -int_4, num2, array[i]);
                }
            }
            catch (Exception ex)
            {
                GClass149.smethod_0("Data/Errors/paintListbossinformation.txt", ex.ToString());
            }
        }

        private static int smethod_6(string string_4)
        {
            if (string_4.ToLower().Contains((mResources.language != 0) ? "moori cliff" : "vách núi moori"))
                return 43;
            for (int i = 0; i < GClass20.string_2.Length; i++)
            {
                if (GClass20.string_2[i].Equals(string_4))
                    return i;
            }
            return -1;
        }

        public static void smethod_7()
        {
            if (!bool_0)
                return;
            for (int i = 0; i < list_0.Count; i++)
            {
                if (GClass73.smethod_24(GClass73.int_10 - int_4 - ((GClass122.int_12 == 2) ? 16 : 23), int_5 + ((GClass122.int_12 == 2) ? 7 : 10) * i + ((GClass122.int_12 == 2) ? 4 : 5), (GClass122.int_12 == 2) ? 17 : 23, (GClass122.int_12 == 2) ? 7 : 8) && !GClass73.bool_14 && GClass73.bool_15 && GClass20.int_37 != list_0[i].int_0)
                {
                    if (GClass148.smethod_0().bool_0)
                        Class21.smethod_0().method_9();
                    GClass109.smethod_1().method_35();
                    GClass78.smethod_1().gclass29_0 = null;
                    GClass73.smethod_49();
                    Class21.smethod_0().method_8(list_0[i].int_0);
                }
            }
        }
    }
}
