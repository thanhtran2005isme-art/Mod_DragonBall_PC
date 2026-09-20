using System;

namespace AssemblyCSharp.Functions
{
	public static class KOLTracker
	{
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
			try
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
			catch
			{
				// KOL la tinh nang phu: loi parser khong duoc phep chan flow NPC.
			}
		}

		public static void ObserveNpcSay(string chat)
		{
			try
			{
				bool kolContext = ContainsKol(chat);
				if (kolContext)
					Arm();

				long now = GClass203.smethod_18();
				if (kolContext || now <= armedUntil)
					TryUpdateProgress(chat);
			}
			catch
			{
				// Fail-safe: khong de tracker anh huong dialog game.
			}
		}

		private static bool IsNumberPart(char c)
		{
			return char.IsDigit(c) || c == '.' || c == ',' || char.IsWhiteSpace(c);
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

			int bestCurrent = -1;
			int bestTotal = -1;

			for (int slash = 0; slash < text.Length; slash++)
			{
				if (text[slash] != '/')
					continue;

				int leftEnd = slash - 1;
				while (leftEnd >= 0 && char.IsWhiteSpace(text[leftEnd]))
					leftEnd--;
				if (leftEnd < 0 || !char.IsDigit(text[leftEnd]))
					continue;

				int leftStart = leftEnd;
				while (leftStart > 0 && IsNumberPart(text[leftStart - 1]))
					leftStart--;

				int rightStart = slash + 1;
				while (rightStart < text.Length && char.IsWhiteSpace(text[rightStart]))
					rightStart++;
				if (rightStart >= text.Length || !char.IsDigit(text[rightStart]))
					continue;

				int rightEnd = rightStart;
				while (rightEnd + 1 < text.Length && IsNumberPart(text[rightEnd + 1]))
					rightEnd++;

				int current;
				int total;
				if (!TryParseCount(text.Substring(leftStart, leftEnd - leftStart + 1), out current))
					continue;
				if (!TryParseCount(text.Substring(rightStart, rightEnd - rightStart + 1), out total))
					continue;
				if (total <= 0 || current < 0 || current > total)
					continue;

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
			try
			{
				if (!HasProgress || Total <= 0)
					return string.Empty;

				double percent = Current * 100.0 / Total;
				string text = "KOL: " + Current + "/" + Total + " (" + percent.ToString("0.##") + "%)";
				if (Completed)
					text += " - HOAN THANH";
				return text;
			}
			catch
			{
				return string.Empty;
			}
		}
	}
}
