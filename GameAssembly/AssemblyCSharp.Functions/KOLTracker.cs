using System;
using System.Text.RegularExpressions;

namespace AssemblyCSharp.Functions
{
	public static class KOLTracker
	{
		private static readonly Regex progressRegex = new Regex(@"(?<!\d)(\d[\d\.,\s]*)\s*/\s*(\d[\d\.,\s]*)(?!\d)", RegexOptions.Compiled);

		private static long armedUntil;

		public static bool HasProgress { get; private set; }

		public static int Current { get; private set; }

		public static int Total { get; private set; }

		public static long LastUpdate { get; private set; }

		public static bool Completed
		{
			get
			{
				return HasProgress && Total > 0 && Current >= Total;
			}
		}

		private static bool ContainsKol(string text)
		{
			return !string.IsNullOrEmpty(text) && text.IndexOf("KOL", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static void Arm()
		{
			armedUntil = GClass203.smethod_18() + 30000L;
		}

		public static void ObserveNpcDialog(string chat, string[] menu)
		{
			bool kolContext = ContainsKol(chat);
			if (menu != null)
			{
				for (int i = 0; i < menu.Length; i++)
				{
					if (ContainsKol(menu[i]))
					{
						kolContext = true;
						break;
					}
				}
			}

			if (kolContext)
				Arm();

			long now = GClass203.smethod_18();
			if (kolContext || now <= armedUntil)
				TryUpdateProgress(chat);
		}

		public static void ObserveNpcSay(string chat)
		{
			bool kolContext = ContainsKol(chat);
			if (kolContext)
				Arm();

			long now = GClass203.smethod_18();
			if (kolContext || now <= armedUntil)
				TryUpdateProgress(chat);
		}

		private static bool TryParseCount(string value, out int result)
		{
			result = 0;
			if (string.IsNullOrEmpty(value))
				return false;

			long number = 0L;
			bool foundDigit = false;
			for (int i = 0; i < value.Length; i++)
			{
				char c = value[i];
				if (!char.IsDigit(c))
					continue;

				foundDigit = true;
				number = number * 10L + (c - '0');
				if (number > int.MaxValue)
					return false;
			}

			if (!foundDigit)
				return false;

			result = (int)number;
			return true;
		}

		private static bool TryUpdateProgress(string text)
		{
			if (string.IsNullOrEmpty(text))
				return false;

			MatchCollection matches = progressRegex.Matches(text);
			int bestCurrent = -1;
			int bestTotal = -1;
			for (int i = 0; i < matches.Count; i++)
			{
				int current;
				int total;
				if (!TryParseCount(matches[i].Groups[1].Value, out current) || !TryParseCount(matches[i].Groups[2].Value, out total))
					continue;
				if (total <= 0 || current < 0 || current > total)
					continue;

				// Neu dialog co nhieu phan so, uu tien muc tieu lon nhat
				// de tranh bat nham chi so trang/menu kieu 1/2.
				if (total > bestTotal)
				{
					bestCurrent = current;
					bestTotal = total;
				}
			}

			if (bestTotal <= 0)
				return false;

			Current = bestCurrent;
			Total = bestTotal;
			HasProgress = true;
			LastUpdate = GClass203.smethod_18();
			return true;
		}

		public static string GetHudText()
		{
			if (!HasProgress || Total <= 0)
				return string.Empty;

			double percent = Current * 100.0 / Total;
			string text = "KOL: " + Current + "/" + Total + " (" + percent.ToString("0.##") + "%)";
			if (Completed)
				text += " - HOAN THANH";
			return text;
		}
	}
}
