using System;
using System.IO;
using System.Text;
using UnityEngine;

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
		private static int queryMapId = -1;
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
		private static int localKillsSinceServerSync;
		private static int localKillsAccepted;
		private static int localKillsRejected;
		private const long KillTnConfirmWindowMs = 750L;
		private static bool pendingKillTn;
		private static long pendingKillTnAt = -1L;
		private static int pendingKillMobId = -1;
		private static int localKillTnTimeouts;
		private static string debugLast = "INIT";

		// Diagnostic protocol trace: chỉ quan sát packet/timeline, không đổi công thức +1 KOL hiện tại.
		private static readonly object protocolTraceLock = new object();
		private static readonly StringBuilder protocolTraceBuffer = new StringBuilder();
		private static bool protocolTraceInitialized;
		private static bool protocolTraceNeedsReset;
		private static long protocolTraceLastFlushAt = -1L;

		public static bool IsProtocolTraceEnabled()
		{
			return HasProgress && Total == 100000;
		}

		public static void TraceProtocol(string eventName, string details)
		{
			if (!IsProtocolTraceEnabled())
				return;

			try
			{
				long now = GClass203.smethod_18();
				int mapId = -1;
				int playerId = -1;
				try
				{
					mapId = GClass20.int_37;
					GClass78 me = GClass78.smethod_1();
					if (me != null)
						playerId = me.int_13;
				}
				catch
				{
				}

				lock (protocolTraceLock)
				{
					if (!protocolTraceInitialized)
					{
						protocolTraceInitialized = true;
						protocolTraceNeedsReset = true;
						protocolTraceLastFlushAt = now;
						protocolTraceBuffer.Append("# KOL protocol trace - diagnostic only; counting logic unchanged\r\n");
						protocolTraceBuffer.Append("# ms | map | me | event | details\r\n");
					}

					protocolTraceBuffer.Append(now)
						.Append(" | map=").Append(mapId)
						.Append(" | me=").Append(playerId)
						.Append(" | ").Append(eventName ?? string.Empty)
						.Append(" | ").Append(details ?? string.Empty)
						.Append("\r\n");
				}
			}
			catch
			{
			}
		}

		private static string GetProtocolTracePath()
		{
			string outputRoot = Path.GetDirectoryName(Application.dataPath);
			string dataDir = Path.Combine(outputRoot, "Data");
			string errorDir = Path.Combine(dataDir, "Errors");
			Directory.CreateDirectory(errorDir);
			return Path.Combine(errorDir, "KOLProtocol.log");
		}

		private static void FlushProtocolTraceIfDue()
		{
			if (!protocolTraceInitialized)
				return;

			try
			{
				long now = GClass203.smethod_18();
				lock (protocolTraceLock)
				{
					if (protocolTraceBuffer.Length == 0)
						return;
					if (protocolTraceLastFlushAt >= 0L
						&& now - protocolTraceLastFlushAt < 500L
						&& protocolTraceBuffer.Length < 4096)
						return;

					string path = GetProtocolTracePath();
					using (StreamWriter writer = new StreamWriter(path, !protocolTraceNeedsReset))
					{
						writer.Write(protocolTraceBuffer.ToString());
					}
					protocolTraceBuffer.Length = 0;
					protocolTraceNeedsReset = false;
					protocolTraceLastFlushAt = now;
				}
			}
			catch
			{
				// Diagnostic không được phép làm hỏng gameplay/KOL flow.
			}
		}

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
					queryMapId = GClass20.int_37;
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
				queryMapId = GClass20.int_37;
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
			FlushProtocolTraceIfDue();
			try
			{
				ExpirePendingKill(GClass203.smethod_18());
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

				// NPC KOL chỉ mở được ở map nơi query thật đã được học.
				// Ở map khác dùng bộ đếm local, tránh OPEN32 -> TO_MENU vô ích.
				if (queryMapId >= 0 && GClass20.int_37 != queryMapId)
				{
					nextSyncAt = now + SyncIntervalMs;
					debugLast = "LOCAL_MAP";
					return;
				}

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

			int previousCurrent = Current;
			int previousLocalKills = localKillsSinceServerSync;
			bool hadProgress = HasProgress;

			Current = bestCurrent;
			Total = bestTotal;
			HasProgress = true;
			localKillsSinceServerSync = 0;
			ClearPendingKill();
			LastUpdate = GClass203.smethod_18();
			nextSyncAt = LastUpdate + SyncIntervalMs;

			TraceProtocol("SERVER_SYNC",
				"old=" + (hadProgress ? previousCurrent.ToString() : "-")
				+ " server=" + bestCurrent
				+ " total=" + bestTotal
				+ " localSinceSync=" + previousLocalKills
				+ " correction=" + (hadProgress ? (bestCurrent - previousCurrent).ToString() : "-"));
			return true;
		}

		private static void ClearPendingKill()
		{
			pendingKillTn = false;
			pendingKillTnAt = -1L;
			pendingKillMobId = -1;
		}

		private static void ExpirePendingKill(long now)
		{
			if (!pendingKillTn)
				return;
			if (pendingKillTnAt >= 0L && now - pendingKillTnAt <= KillTnConfirmWindowMs)
				return;

			localKillsRejected++;
			localKillTnTimeouts++;
			debugLast = "TN_TIMEOUT:" + pendingKillMobId;
			TraceProtocol("KOL_TN_TIMEOUT",
				"mob=" + pendingKillMobId
				+ " age=" + ((pendingKillTnAt >= 0L) ? (now - pendingKillTnAt) : -1L));
			ClearPendingKill();
		}

		public static void ResetKillConfirmation()
		{
			ClearPendingKill();
		}

		public static void ObserveOwnTnSmGain(sbyte type, int amount)
		{
			try
			{
				long now = GClass203.smethod_18();
				ExpirePendingKill(now);
				TraceProtocol("SMTN_EVAL",
					"type=" + type
					+ " amount=" + amount
					+ " pending=" + (pendingKillTn ? 1 : 0)
					+ " mob=" + pendingKillMobId);
				if (!pendingKillTn || type != 2 || amount <= 0)
					return;

				int confirmedMobId = pendingKillMobId;
				ClearPendingKill();
				if (!HasProgress || Total != 100000 || Completed)
					return;

				if (Current < Total)
				{
					Current++;
					localKillsSinceServerSync++;
					localKillsAccepted++;
					LastUpdate = now;
					debugLast = "LOCAL+1_TN:" + confirmedMobId;
					TraceProtocol("KOL_LOCAL_PLUS",
						"mob=" + confirmedMobId
						+ " current=" + Current
						+ " amount=" + amount);
				}
			}
			catch
			{
			}
		}

		public static void ObserveMobDeath(bool matchedOwnOneHpKillShot, bool hasOwnDrop, bool hasForeignOwnedDrop, int mobId)
		{
			try
			{
				long now = GClass203.smethod_18();
				ExpirePendingKill(now);
				TraceProtocol("KOL_DEATH_EVAL",
					"mob=" + mobId
					+ " oneHpMatch=" + (matchedOwnOneHpKillShot ? 1 : 0)
					+ " ownDrop=" + (hasOwnDrop ? 1 : 0)
					+ " foreignDrop=" + (hasForeignOwnedDrop ? 1 : 0)
					+ " oldPending=" + (pendingKillTn ? pendingKillMobId.ToString() : "-"));

				// Local mirror chi ap dung cho nhiem vu 100.000 quai.
				if (!HasProgress || Total != 100000 || Completed)
					return;

				// Neu mot death moi den truoc goi -3 cua candidate cu thi bo candidate cu.
				// Tren flow killer binh thuong -12 va -3 duoc server gui sat nhau.
				if (pendingKillTn)
				{
					localKillsRejected++;
					debugLast = "TN_OVERLAP:" + pendingKillMobId;
					TraceProtocol("KOL_TN_OVERLAP",
						"oldMob=" + pendingKillMobId + " newMob=" + mobId);
					ClearPendingKill();
				}

				// Drop co owner nguoi khac la bang chung manh minh khong phai killer.
				if (hasForeignOwnedDrop && !hasOwnDrop)
				{
					localKillsRejected++;
					debugLast = "LOCAL_REJECT:" + mobId;
					TraceProtocol("KOL_FOREIGN_REJECT", "mob=" + mobId);
					return;
				}

				// GIỮ NGUYÊN logic hiện tại để đo sai lệch:
				// chỉ stage khi có own-drop hoặc own attack đã được đánh dấu lúc HP client = 1.
				// Diagnostic log sẽ dùng để xác minh packet nào thực sự chứng minh last-hit.
				if (!hasOwnDrop && !matchedOwnOneHpKillShot)
				{
					debugLast = "LOCAL_SKIP:" + mobId;
					TraceProtocol("KOL_LOCAL_SKIP", "mob=" + mobId);
					return;
				}

				// Chua +1 tai -12. Logic hiện tại vẫn chờ packet -3 type=2.
				// Lưu ý: +SM/TN có thể xuất hiện với mọi hit gây damage, nên đây chưa phải
				// bằng chứng last-hit độc lập; đang được giữ nguyên chỉ để diagnostic A/B.
				pendingKillTn = true;
				pendingKillTnAt = now;
				pendingKillMobId = mobId;
				debugLast = hasOwnDrop ? ("WAIT_TN_DROP:" + mobId) : ("WAIT_TN_1HP:" + mobId);
				TraceProtocol("KOL_STAGE",
					"mob=" + mobId
					+ " source=" + (hasOwnDrop ? "DROP" : "ONE_HP"));
			}
			catch
			{
			}
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
					+ " MAP:" + queryMapId
					+ " S:" + state
					+ " A:" + debugOpenSent + "/" + debugMenuResponse + "/" + debugQuerySent + "/" + debugProgressResponse
					+ " L:+" + localKillsSinceServerSync + "(" + localKillsAccepted + "/" + localKillsRejected + ")"
					+ " P:" + (pendingKillTn ? pendingKillMobId.ToString() : "-")
					+ " T:" + localKillTnTimeouts
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
