using System;

namespace AssemblyCSharp.Functions
{
	public static class KOLTracker
	{
		private const long SyncIntervalMs = 10000L;
		private const long SyncTimeoutMs = 3000L;

		private static long armedUntil;
		private static int queryNpcId = -1;
		private static int queryMenuId = -1;
		private static int queryOptionId = -1;
		private static bool hasQuery;
		private static int candidateNpcId = -1;
		private static int candidateMenuId = -1;
		private static int candidateOptionId = -1;
		private static bool hasCandidate;
		private static long candidateAt = -1L;
		private static long nextSyncAt = -1L;
		private static bool backgroundSyncPending;
		private static long backgroundSyncExpiresAt = -1L;

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

		private static bool IsRegularKolClaim(string text)
		{
			if (string.IsNullOrEmpty(text))
				return false;
			if (text.IndexOf("VIP", StringComparison.OrdinalIgnoreCase) >= 0)
				return false;
			return text.IndexOf("Nhận quà KOL", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Nhan qua KOL", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static void Arm()
		{
			armedUntil = GClass203.smethod_18() + 30000L;
		}

		private static string GetCurrentMenuCaption()
		{
			try
			{
				if (GClass73.gclass145_0 == null || GClass73.gclass145_0.gclass88_0 == null)
					return string.Empty;
				int index = GClass73.gclass145_0.int_0;
				if (index < 0 || index >= GClass73.gclass145_0.gclass88_0.method_2())
					return string.Empty;
				GClass87 item = (GClass87)GClass73.gclass145_0.gclass88_0.method_3(index);
				return (item == null || item.string_0 == null) ? string.Empty : item.string_0;
			}
			catch
			{
				return string.Empty;
			}
		}

		public static void ObserveMenuRequest(int npcId, int menuId, int optionId)
		{
			try
			{
				string caption = GetCurrentMenuCaption();
				if (!string.IsNullOrEmpty(caption) && caption.IndexOf("VIP", StringComparison.OrdinalIgnoreCase) >= 0)
					return;

				long now = GClass203.smethod_18();
				bool regularClaim = IsRegularKolClaim(caption);
				bool inKolContext = now <= armedUntil;
				if (!regularClaim && !inKolContext)
					return;

				// Chua tin request nay ngay. Giu lam candidate va chi xac nhan
				// sau khi response tiep theo thuc su parse duoc progress KOL.
				candidateNpcId = npcId;
				candidateMenuId = menuId;
				candidateOptionId = optionId;
				hasCandidate = true;
				candidateAt = now;
			}
			catch
			{
			}
		}

		private static void ConfirmCandidate(int npcId)
		{
			try
			{
				if (!hasCandidate || candidateNpcId != npcId)
					return;
				long now = GClass203.smethod_18();
				if (candidateAt < 0L || now - candidateAt > 5000L)
				return;

				queryNpcId = candidateNpcId;
				queryMenuId = candidateMenuId;
				queryOptionId = candidateOptionId;
				hasQuery = true;
				hasCandidate = false;
				candidateAt = -1L;
				backgroundSyncPending = false;
				backgroundSyncExpiresAt = -1L;
				nextSyncAt = now + SyncIntervalMs;
			}
			catch
			{
			}
		}

		public static void Update()
		{
			try
			{
				if (!hasQuery || !HasProgress || Completed)
					return;
				if (!GClass14.smethod_0().isConnected())
					return;
				if (!(GClass73.gclass131_0 is GClass144))
					return;
				if (GClass73.gclass145_0 != null && GClass73.gclass145_0.bool_0)
					return;
				if (GClass96.gclass96_0 != null)
					return;

				long now = GClass203.smethod_18();
				if (backgroundSyncPending)
				{
					if (now < backgroundSyncExpiresAt)
						return;
					backgroundSyncPending = false;
					backgroundSyncExpiresAt = -1L;
				}

				if (nextSyncAt < 0L)
				{
					nextSyncAt = now + SyncIntervalMs;
					return;
				}
				if (now < nextSyncAt)
					return;

				backgroundSyncPending = true;
				backgroundSyncExpiresAt = now + SyncTimeoutMs;
				nextSyncAt = now + SyncIntervalMs;
				GClass7.smethod_0().method_61(queryNpcId, queryMenuId, queryOptionId);
			}
			catch
			{
				backgroundSyncPending = false;
				backgroundSyncExpiresAt = -1L;
			}
		}

		public static bool ObserveNpcDialog(int npcId, string chat, string[] menu)
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
				bool sameBackgroundNpc = backgroundSyncPending && npcId == queryNpcId;
				if (kolContext || now <= armedUntil || sameBackgroundNpc)
				{
					if (TryUpdateProgress(chat))
						ConfirmCandidate(npcId);
				}

				if (sameBackgroundNpc)
				{
					backgroundSyncPending = false;
					backgroundSyncExpiresAt = -1L;
					return true;
				}
			}
			catch
			{
				// KOL la tinh nang phu: loi parser khong duoc phep chan flow NPC.
			}
			return false;
		}

		public static bool ObserveNpcSay(int npcId, string chat)
		{
			try
			{
				bool kolContext = ContainsKol(chat);
				if (kolContext)
					Arm();

				long now = GClass203.smethod_18();
				bool sameBackgroundNpc = backgroundSyncPending && npcId == queryNpcId;
				if (kolContext || now <= armedUntil || sameBackgroundNpc)
				{
					if (TryUpdateProgress(chat))
						ConfirmCandidate(npcId);
				}

				if (sameBackgroundNpc)
				{
					backgroundSyncPending = false;
					backgroundSyncExpiresAt = -1L;
					return true;
				}
			}
			catch
			{
				// Fail-safe: khong de tracker anh huong dialog game.
			}
			return false;
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
			nextSyncAt = LastUpdate + SyncIntervalMs;
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
