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
		private static int queryKind;
		private static int queryParentMenuId = -1;
		private static bool queryHasParent;
		private static bool hasQuery;
		private static int candidateNpcId = -1;
		private static int candidateMenuId = -1;
		private static int candidateOptionId = -1;
		private static int candidateKind;
		private static int candidateParentMenuId = -1;
		private static bool candidateHasParent;
		private static bool hasCandidate;
		private static long candidateAt = -1L;
		private static long nextSyncAt = -1L;
		private const int BackgroundIdle = 0;
		private const int BackgroundWaitMenu = 1;
		private const int BackgroundWaitProgress = 2;
		private const int BackgroundWaitSubMenu = 3;

		private static int backgroundSyncState;
		private static long backgroundSyncExpiresAt = -1L;

		private static int debugOpenSent;
		private static int debugMenuResponse;
		private static int debugQuerySent;
		private static int debugProgressResponse;
		private static string debugLast = "INIT";

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
			// Menu game có thể chèn xuống dòng/khoảng trắng khi render.
			// Nút KOL thường được phân biệt đủ an toàn bởi "KOL" và không có "VIP".
			return ContainsKol(text);
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
				debugLast = "OBS:" + npcId + ":" + menuId + "/" + optionId;
				if (!string.IsNullOrEmpty(caption) && caption.IndexOf("VIP", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					debugLast = "OBS_VIP";
					return;
				}

				long now = GClass203.smethod_18();
				bool regularClaim = IsRegularKolClaim(caption);
				bool inKolContext = now <= armedUntil;
				if (!regularClaim && !inKolContext)
					return;

				if (regularClaim)
				{
					// Caption "Nhận quà KOL" đủ đặc hiệu để chốt query ngay.
					// Không phụ thuộc npcId của response progress vì một số server
					// trả dialog bằng id khác dù progress vẫn parse đúng.
					queryNpcId = npcId;
					queryMenuId = menuId;
					queryOptionId = optionId;
					queryKind = 22;
					hasQuery = true;
					hasCandidate = false;
					candidateAt = -1L;
					backgroundSyncState = BackgroundIdle;
					backgroundSyncExpiresAt = -1L;
					nextSyncAt = now + SyncIntervalMs;
					debugLast = "ARM";
					return;
				}

				// Fallback cho server/menu không có caption chuẩn: chỉ giữ candidate
				// và xác nhận khi response tiếp theo parse được progress KOL.
				candidateNpcId = npcId;
				candidateMenuId = menuId;
				candidateOptionId = optionId;
				candidateKind = 22;
				hasCandidate = true;
				candidateAt = now;
			}
			catch
			{
			}
		}

		public static void ObservePacket22(int npcId, int menuId, int optionId)
		{
			try
			{
				// Packet 22 do auto-sync gửi không được ghi đè query đã học.
				if (backgroundSyncState != BackgroundIdle)
				{
					debugLast = "AUTO22";
					return;
				}

				candidateNpcId = npcId;
				candidateMenuId = menuId;
				candidateOptionId = optionId;
				candidateKind = 22;
				hasCandidate = true;
				candidateAt = GClass203.smethod_18();
				debugLast = "P22:" + npcId + ":" + menuId + "/" + optionId;
			}
			catch
			{
			}
		}

		public static void ObservePacket32Select(short npcId, sbyte select)
		{
			try
			{
				// Auto-sync packet không được ghi đè query đã học.
				if (backgroundSyncState != BackgroundIdle)
				{
					debugLast = "AUTO32";
					return;
				}

				long now = GClass203.smethod_18();
				if (hasCandidate && candidateKind == 32 && candidateNpcId == npcId
					&& candidateAt >= 0L && now - candidateAt <= 15000L)
				{
					candidateParentMenuId = candidateMenuId;
					candidateHasParent = true;
				}
				else
				{
					candidateParentMenuId = -1;
					candidateHasParent = false;
				}

				candidateNpcId = npcId;
				candidateMenuId = select;
				candidateOptionId = 0;
				candidateKind = 32;
				hasCandidate = true;
				candidateAt = now;
				debugLast = candidateHasParent
					? ("P32:" + npcId + ":" + candidateParentMenuId + ">" + select)
					: ("P32:" + npcId + ":" + select);
			}
			catch
			{
			}
		}

		private static void ConfirmCandidate(int npcId)
		{
			try
			{
				if (!hasCandidate)
					return;
				long now = GClass203.smethod_18();
				if (candidateAt < 0L || now - candidateAt > 5000L)
				return;

				queryNpcId = candidateNpcId;
				queryMenuId = candidateMenuId;
				queryOptionId = candidateOptionId;
				queryKind = candidateKind;
				queryParentMenuId = candidateParentMenuId;
				queryHasParent = candidateHasParent;
				hasQuery = true;
				hasCandidate = false;
				candidateAt = -1L;
				candidateParentMenuId = -1;
				candidateHasParent = false;
				backgroundSyncState = BackgroundIdle;
				backgroundSyncExpiresAt = -1L;
				nextSyncAt = now + SyncIntervalMs;
				debugLast = "CONFIRM";
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

				long now = GClass203.smethod_18();

				// Khi đang chờ response của sync nền, chỉ chờ hoặc timeout.
				if (backgroundSyncState != BackgroundIdle)
				{
					if (now < backgroundSyncExpiresAt)
						return;
					debugLast = (backgroundSyncState == BackgroundWaitMenu) ? "TO_MENU"
						: ((backgroundSyncState == BackgroundWaitSubMenu) ? "TO_SUB" : "TO_PROG");
					backgroundSyncState = BackgroundIdle;
					backgroundSyncExpiresAt = -1L;
					nextSyncAt = now + SyncIntervalMs;
					return;
				}

				// Không chen sync nền khi người chơi đang thao tác menu/dialog.
				if (GClass73.gclass145_0 != null && GClass73.gclass145_0.bool_0)
				{
					debugLast = "B_MENU";
					return;
				}
				if (GClass96.gclass96_0 != null)
				{
					debugLast = "B_DLG";
					return;
				}

				if (nextSyncAt < 0L)
				{
					nextSyncAt = now + SyncIntervalMs;
					return;
				}
				if (now < nextSyncAt)
					return;

				if (queryKind == 32)
				{
					if (queryHasParent)
					{
						backgroundSyncState = BackgroundWaitMenu;
						backgroundSyncExpiresAt = now + SyncTimeoutMs;
						nextSyncAt = now + SyncIntervalMs;
						debugOpenSent++;
						debugLast = "OPEN32";
						GClass7.smethod_0().method_44();
						GClass7.smethod_0().method_60(queryNpcId);
						return;
					}

					backgroundSyncState = BackgroundWaitProgress;
					backgroundSyncExpiresAt = now + SyncTimeoutMs;
					nextSyncAt = now + SyncIntervalMs;
					debugQuerySent++;
					GClass7.smethod_0().method_59((short)queryNpcId, (sbyte)queryMenuId);
					debugLast = "Q32";
					return;
				}

				// Packet 22 cần ngữ cảnh mở NPC trước.
				backgroundSyncState = BackgroundWaitMenu;
				backgroundSyncExpiresAt = now + SyncTimeoutMs;
				nextSyncAt = now + SyncIntervalMs;
				debugOpenSent++;
				debugLast = "OPEN22";
				GClass7.smethod_0().method_44();
				GClass7.smethod_0().method_60(queryNpcId);
			}
			catch
			{
				backgroundSyncState = BackgroundIdle;
				backgroundSyncExpiresAt = -1L;
			}
		}

		// Gọi sau khi packet 33 OPEN_UI_MENU đã được đọc hết.
		// Trả true để controller ẩn menu của lần sync nền.
		public static bool ObserveNpcMenu()
		{
			if (queryKind == 32 && queryHasParent && backgroundSyncState == BackgroundWaitMenu)
			{
				long now32 = GClass203.smethod_18();
				debugMenuResponse++;
				backgroundSyncState = BackgroundWaitSubMenu;
				backgroundSyncExpiresAt = now32 + SyncTimeoutMs;
				debugQuerySent++;
				GClass7.smethod_0().method_59((short)queryNpcId, (sbyte)queryParentMenuId);
				debugLast = "Q32A";
				return true;
			}

			if (queryKind != 22 || backgroundSyncState != BackgroundWaitMenu)
				return false;

			long now = GClass203.smethod_18();
			debugMenuResponse++;
			debugLast = "MENU_OK";
			backgroundSyncState = BackgroundWaitProgress;
			backgroundSyncExpiresAt = now + SyncTimeoutMs;
			try
			{
				debugQuerySent++;
				debugLast = "QUERY";
				GClass7.smethod_0().method_61(queryNpcId, queryMenuId, queryOptionId);
			}
			catch
			{
				backgroundSyncState = BackgroundIdle;
				backgroundSyncExpiresAt = -1L;
				nextSyncAt = now + SyncIntervalMs;
			}
			return true;
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

				if (queryKind == 32 && queryHasParent && backgroundSyncState == BackgroundWaitMenu)
				{
					debugMenuResponse++;
					backgroundSyncState = BackgroundWaitSubMenu;
					backgroundSyncExpiresAt = now + SyncTimeoutMs;
					debugQuerySent++;
					GClass7.smethod_0().method_59((short)queryNpcId, (sbyte)queryParentMenuId);
					debugLast = "Q32A";
					return true;
				}

				if (queryKind == 32 && queryHasParent && backgroundSyncState == BackgroundWaitSubMenu)
				{
					debugMenuResponse++;
					backgroundSyncState = BackgroundWaitProgress;
					backgroundSyncExpiresAt = now + SyncTimeoutMs;
					debugQuerySent++;
					GClass7.smethod_0().method_59((short)queryNpcId, (sbyte)queryMenuId);
					debugLast = "Q32B";
					return true;
				}

				bool backgroundProgress = backgroundSyncState == BackgroundWaitProgress;
				bool freshCandidate = hasCandidate && candidateAt >= 0L && now - candidateAt <= 5000L;
				bool parsedProgress = false;
				if (kolContext || now <= armedUntil || backgroundProgress || freshCandidate)
				{
					parsedProgress = TryUpdateProgress(chat);
					if (parsedProgress)
					{
						if (backgroundProgress)
						{
							debugProgressResponse++;
							debugLast = "PROG_OK";
						}
						ConfirmCandidate(npcId);
					}
				}

				if (backgroundProgress && parsedProgress)
				{
					backgroundSyncState = BackgroundIdle;
					backgroundSyncExpiresAt = -1L;
					nextSyncAt = now + SyncIntervalMs;
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
				bool backgroundProgress = backgroundSyncState == BackgroundWaitProgress;
				bool freshCandidate = hasCandidate && candidateAt >= 0L && now - candidateAt <= 5000L;
				bool parsedProgress = false;
				if (kolContext || now <= armedUntil || backgroundProgress || freshCandidate)
				{
					parsedProgress = TryUpdateProgress(chat);
					if (parsedProgress)
					{
						if (backgroundProgress)
						{
							debugProgressResponse++;
							debugLast = "PROG_OK";
						}
						ConfirmCandidate(npcId);
					}
				}

				if (backgroundProgress && parsedProgress)
				{
					backgroundSyncState = BackgroundIdle;
					backgroundSyncExpiresAt = -1L;
					nextSyncAt = now + SyncIntervalMs;
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
				string state = (backgroundSyncState == BackgroundWaitMenu) ? "WM"
					: ((backgroundSyncState == BackgroundWaitSubMenu) ? "WS"
					: ((backgroundSyncState == BackgroundWaitProgress) ? "WP" : "I"));
				text += " | Q:" + (hasQuery ? "Y" : "N")
					+ " K:" + queryKind
					+ " N:" + queryNpcId + " M:" + (queryHasParent ? (queryParentMenuId + ">") : "") + queryMenuId + "/" + queryOptionId
					+ " S:" + state
					+ " A:" + debugOpenSent + "/" + debugMenuResponse + "/" + debugQuerySent + "/" + debugProgressResponse
					+ " " + debugLast;
				return text;
			}
			catch
			{
				return string.Empty;
			}
		}
	}
}
