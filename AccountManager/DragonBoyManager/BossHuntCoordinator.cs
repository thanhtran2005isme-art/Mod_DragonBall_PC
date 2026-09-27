using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace DragonBoyManager
{
    public enum BossHuntState
    {
        Idle,
        Scanning,
        Rallying,
        Fighting,
        Stopped
    }

    public enum BossPresenceState
    {
        Unknown,
        Alive,
        Dead,
        Stale
    }

    public sealed class BossHuntWorkerSnapshot
    {
        public int AccountId;
        public string Username = "";
        public int WorkerIndex = -1;
        public int WorkerCount;
        public int AssignmentGeneration;
        public int Zone = -1;
        public int MapId = -1;
        public string MapName = "";
        public string Status = "";
        public bool Ready;
        public bool Failed;
        public bool Unresponsive;
        public DateTime LastHeartbeatUtc = DateTime.MinValue;
        public DateTime LastEventUtc = DateTime.MinValue;
        public int ScanCycle;
        public string AssignedZones = "";
        public int TotalZones;
        public int EntityCount = -1;
        public int BossCount = -1;
        public int TargetHp = -1;
        public DateTime ZoneEnteredAtUtc = DateTime.MinValue;
        public int ZoneFailureCount;
        public string LastZoneFailure = "";
        public string LastAction = "";
        public string DuplicateWarning = "";
        public List<string> ScannedZones = new List<string>();
        public List<string> ZoneHistory = new List<string>();
    }

    public sealed class BossHuntBossSnapshot
    {
        public string BossName = "";
        public int MapId = -1;
        public string MapName = "";
        public int Zone = -1;
        public bool Alive;
        public BossPresenceState Presence = BossPresenceState.Unknown;
        public DateTime SpawnedAtUtc = DateTime.MinValue;
        public DateTime DiedAtUtc = DateTime.MinValue;
        public string Killer = "";
        public string RawSpawn = "";
        public string RawDeath = "";
        public int LastSourceAccountId = -1;
        public List<int> SourceAccounts = new List<int>();
    }

    public sealed class BossHuntBossEventSnapshot
    {
        public string EventType = "";
        public string BossName = "";
        public int MapId = -1;
        public string MapName = "";
        public int Zone = -1;
        public DateTime ObservedAtUtc = DateTime.MinValue;
        public string Killer = "";
        public int SourceAccountId = -1;
        public string RawMessage = "";
    }

    public sealed class BossHuntTimelineEntry
    {
        public DateTime AtUtc = DateTime.MinValue;
        public int AccountId = -1;
        public string EventName = "";
        public string Detail = "";
    }

    public sealed class BossHuntSnapshot
    {
        public int SessionId;
        public int AssignmentGeneration;
        public string BossName = "";
        public int StartZone;
        public BossHuntState State;
        public int FoundMapId = -1;
        public string FoundMapName = "";
        public int FoundZone = -1;
        public int FinderAccountId = -1;
        public string FinderUsername = "";
        public int UniqueCoverageCount;
        public int CoverageTotalZones;
        public string StopReason = "";
        public BossHuntBossSnapshot TargetBoss;
        public List<BossHuntWorkerSnapshot> Workers = new List<BossHuntWorkerSnapshot>();
        public List<BossHuntBossEventSnapshot> RecentBossEvents = new List<BossHuntBossEventSnapshot>();
        public List<BossHuntTimelineEntry> RecentTimeline = new List<BossHuntTimelineEntry>();
    }

    internal sealed class BossHuntPayload
    {
        public int sessionId;
        public int assignmentGeneration;
        public string bossName;
        public int startZone;
        public int workerIndex;
        public int workerCount;
        public int mapId;
        public string mapName;
        public int zone;
        public int accountId;
        public string detail;
        public string eventName;
        public string rawMessage;
        public string killer;
        public long observedAtTicks;
        public int entityCount;
        public int bossCount;
        public int targetHp = -1;
        public int scanCycle;
        public int totalZones;
        public string assignedZones;
    }

    internal sealed class BossHuntZoneLedgerEntry
    {
        public int Generation;
        public int MapId;
        public int Zone;
        public int ActiveAccountId = -1;
        public int LastScannedByAccountId = -1;
        public DateTime LastEnterUtc = DateTime.MinValue;
        public DateTime LastClearUtc = DateTime.MinValue;
    }

    public sealed class BossHuntCoordinator
    {
        public const int CmdStartScan = 100;
        public const int CmdStop = 101;
        public const int CmdRally = 102;
        public const int CmdBossSync = 103;
        public const int CmdBossInvalidate = 104;

        public const int CmdZone = 110;
        public const int CmdFound = 111;
        public const int CmdDead = 112;
        public const int CmdReady = 113;
        public const int CmdFailed = 114;
        public const int CmdBossSpawn = 115;
        public const int CmdBossDeath = 116;
        public const int CmdTelemetry = 117;
        public const int CmdHeartbeat = 118;

        private const int HeartbeatTimeoutSeconds = 8;
        private const int BossLocationFreshMinutes = 60;

        private static readonly BossHuntCoordinator _instance = new BossHuntCoordinator();
        private readonly object _sync = new object();
        private readonly Dictionary<int, BossHuntWorkerSnapshot> _workers = new Dictionary<int, BossHuntWorkerSnapshot>();
        private readonly List<Account> _sessionAccounts = new List<Account>();
        private readonly Dictionary<string, BossHuntBossSnapshot> _bossRecords = new Dictionary<string, BossHuntBossSnapshot>(StringComparer.OrdinalIgnoreCase);
        private readonly List<BossHuntBossEventSnapshot> _bossEvents = new List<BossHuntBossEventSnapshot>();
        private readonly Dictionary<string, BossHuntZoneLedgerEntry> _zoneLedger = new Dictionary<string, BossHuntZoneLedgerEntry>();
        private readonly Dictionary<int, string> _activeZoneByAccount = new Dictionary<int, string>();
        private readonly List<BossHuntTimelineEntry> _timeline = new List<BossHuntTimelineEntry>();
        private readonly Timer _watchdog;

        private int _sessionSeed;
        private int _sessionId;
        private int _assignmentGeneration;
        private string _bossName = "";
        private int _startZone;
        private BossHuntState _state = BossHuntState.Idle;
        private int _foundMapId = -1;
        private string _foundMapName = "";
        private int _foundZone = -1;
        private int _finderAccountId = -1;
        private string _stopReason = "";

        public static BossHuntCoordinator Instance
        {
            get { return _instance; }
        }

        public event Action<BossHuntSnapshot> Changed;

        private BossHuntCoordinator()
        {
            _watchdog = new Timer(WatchdogTick, null, 2000, 2000);
        }

        public int GetConnectedCount()
        {
            return GetConnectedAccounts().Count;
        }

        public BossHuntBossSnapshot GetBossInfo(string bossName)
        {
            lock (_sync)
            {
                BossHuntBossSnapshot boss = FindLatestBossLocked(bossName, false);
                return CloneBoss(boss);
            }
        }

        public void HandleConnected(Account account)
        {
            if (account == null)
                return;

            List<BossHuntPayload> syncPayloads = new List<BossHuntPayload>();
            List<BossHuntPayload> invalidatePayloads = new List<BossHuntPayload>();
            lock (_sync)
            {
                DateTime now = DateTime.UtcNow;
                foreach (BossHuntBossSnapshot boss in _bossRecords.Values)
                {
                    if (boss.Alive)
                    {
                        if (boss.MapId < 0 || boss.SpawnedAtUtc == DateTime.MinValue)
                            continue;
                        if (now.Subtract(boss.SpawnedAtUtc).TotalMinutes > BossLocationFreshMinutes)
                            continue;
                        syncPayloads.Add(CreateBossSyncPayload(boss));
                    }
                    else if (boss.DiedAtUtc != DateTime.MinValue)
                    {
                        invalidatePayloads.Add(new BossHuntPayload
                        {
                            bossName = boss.BossName,
                            killer = boss.Killer,
                            rawMessage = boss.RawDeath,
                            observedAtTicks = boss.DiedAtUtc.Ticks,
                            eventName = "DEATH_SYNC"
                        });
                    }
                }
            }

            for (int i = 0; i < syncPayloads.Count; i++)
                Send(account, CmdBossSync, syncPayloads[i]);
            for (int i = 0; i < invalidatePayloads.Count; i++)
                Send(account, CmdBossInvalidate, invalidatePayloads[i]);
        }

        public bool Start(string bossName, int startZone, out string error)
        {
            error = "";
            bossName = (bossName ?? "").Trim();
            if (bossName.Length == 0)
            {
                error = MainController.language == 0 ? "Hãy nhập hoặc chọn boss cần săn." : "Choose or enter a boss name.";
                return false;
            }

            List<Account> accounts = GetConnectedAccounts();
            if (accounts.Count == 0)
            {
                error = MainController.language == 0 ? "Không có tài khoản nào đang kết nối." : "No connected account.";
                return false;
            }

            accounts.Sort(delegate(Account a, Account b) { return a.ID.CompareTo(b.ID); });
            int sessionId;
            int generation;
            BossHuntBossSnapshot knownBoss;
            lock (_sync)
            {
                if (IsRunningState(_state))
                {
                    error = MainController.language == 0
                        ? "Đang có một phiên săn boss hoạt động. Hãy bấm DỪNG trước khi tạo phiên mới."
                        : "A boss hunt is already running. Stop it before starting another session.";
                    return false;
                }

                _sessionSeed++;
                if (_sessionSeed <= 0)
                    _sessionSeed = 1;
                _sessionId = _sessionSeed;
                _assignmentGeneration = 1;
                sessionId = _sessionId;
                generation = _assignmentGeneration;
                _bossName = bossName;
                _startZone = Math.Max(0, startZone);
                _state = BossHuntState.Scanning;
                _foundMapId = -1;
                _foundMapName = "";
                _foundZone = -1;
                _finderAccountId = -1;
                _stopReason = "";
                _workers.Clear();
                _sessionAccounts.Clear();
                _zoneLedger.Clear();
                _activeZoneByAccount.Clear();
                _timeline.Clear();

                DateTime now = DateTime.UtcNow;
                for (int i = 0; i < accounts.Count; i++)
                {
                    Account account = accounts[i];
                    _workers[account.ID] = new BossHuntWorkerSnapshot
                    {
                        AccountId = account.ID,
                        Username = account.Username ?? "",
                        WorkerIndex = i,
                        WorkerCount = accounts.Count,
                        AssignmentGeneration = generation,
                        Status = MainController.language == 0 ? "Chuẩn bị dò" : "Preparing",
                        LastHeartbeatUtc = now,
                        LastEventUtc = now
                    };
                }
                _sessionAccounts.AddRange(accounts);
                AddTimelineLocked("SESSION_START", -1, "boss=" + bossName + ";workers=" + accounts.Count + ";generation=" + generation);
                knownBoss = CloneBoss(FindLatestBossLocked(bossName, true));
            }

            BossHuntDiagnostics.Log("MANAGER", "SESSION_START", sessionId, -1, bossName, BossHuntState.Scanning.ToString(),
                "generation=" + generation + ";workers=" + accounts.Count + ";startZone=" + Math.Max(0, startZone));
            SendScanAssignments(accounts, sessionId, generation, bossName, Math.Max(0, startZone), knownBoss);
            Publish();
            return true;
        }

        public void Stop(string reason)
        {
            List<Account> targets;
            BossHuntPayload payload;
            lock (_sync)
            {
                if (_state == BossHuntState.Idle || _state == BossHuntState.Stopped)
                    return;

                _state = BossHuntState.Stopped;
                _stopReason = reason ?? "";
                targets = new List<Account>(_sessionAccounts);
                payload = CreatePayload();
                payload.detail = _stopReason;
                AddTimelineLocked("SESSION_STOP", -1, _stopReason);

                foreach (BossHuntWorkerSnapshot worker in _workers.Values)
                {
                    ClearActiveZoneLocked(worker.AccountId);
                    if (worker.Status != (MainController.language == 0 ? "Mất kết nối" : "Disconnected"))
                        worker.Status = MainController.language == 0 ? "Đã dừng" : "Stopped";
                }
            }

            BossHuntDiagnostics.Log("MANAGER", "SESSION_STOP", payload.sessionId, -1, payload.bossName, BossHuntState.Stopped.ToString(), payload.detail);
            Broadcast(targets, CmdStop, payload);
            Publish();
        }

        public void HandleClientMessage(Account account, int cmd, byte[] data)
        {
            if (account == null || data == null)
                return;

            BossHuntPayload payload;
            try
            {
                payload = JsonConvert.DeserializeObject<BossHuntPayload>(Encoding.UTF8.GetString(data));
            }
            catch
            {
                return;
            }
            if (payload == null)
                return;

            if (cmd == CmdBossSpawn)
            {
                HandleBossSpawn(account, payload);
                return;
            }
            if (cmd == CmdBossDeath)
            {
                HandleBossDeath(account, payload);
                return;
            }
            if (cmd == CmdHeartbeat)
            {
                HandleHeartbeat(account, payload);
                return;
            }
            if (cmd == CmdTelemetry)
            {
                HandleTelemetry(account, payload);
                return;
            }

            BossHuntDiagnostics.Log("MANAGER", "CLIENT_EVENT", payload.sessionId, account.ID, payload.bossName, _state.ToString(),
                "generation=" + payload.assignmentGeneration + ";cmd=" + cmd + ";detail=" + (payload.detail ?? "") +
                ";map=" + payload.mapId + ";zone=" + payload.zone);

            if (cmd == CmdFailed)
            {
                HandleFailed(account, payload);
                return;
            }

            if (cmd == CmdDead)
            {
                bool valid;
                lock (_sync)
                {
                    valid = IsCurrentLocked(payload) && BossMatches(payload.bossName, _bossName);
                    if (valid)
                        TouchWorkerLocked(account, payload);
                }

                if (valid)
                {
                    string reason = MainController.language == 0 ? "Boss mục tiêu đã chết" : "Target boss died";
                    if (!string.IsNullOrEmpty(payload.detail))
                        reason += ": " + payload.detail;
                    Stop(reason);
                }
                return;
            }

            if (cmd == CmdFound)
            {
                HandleFound(account, payload);
                return;
            }

            if (cmd == CmdZone)
            {
                lock (_sync)
                {
                    if (!IsCurrentLocked(payload) || _state != BossHuntState.Scanning)
                        return;

                    BossHuntWorkerSnapshot worker;
                    if (_workers.TryGetValue(account.ID, out worker))
                    {
                        TouchWorkerLocked(account, payload);
                        string detail = payload.detail ?? "";
                        if (detail == "WAITING_LOCATION")
                            worker.Status = MainController.language == 0 ? "Chờ vị trí boss" : "Waiting for boss location";
                        else if (detail == "ZONE_LIST_WAIT")
                            worker.Status = MainController.language == 0 ? "Đang tải danh sách khu" : "Loading zone list";
                        else if (detail.StartsWith("STANDBY|", StringComparison.Ordinal))
                        {
                            string zoneCount = detail.Substring("STANDBY|".Length);
                            worker.Status = (MainController.language == 0 ? "Dự phòng - " : "Standby - ") + zoneCount +
                                            (MainController.language == 0 ? " khu" : " zones");
                        }
                        else if (detail.StartsWith("ROUTING_MAP|", StringComparison.Ordinal))
                        {
                            string targetMap = detail.Substring("ROUTING_MAP|".Length);
                            worker.Status = (MainController.language == 0 ? "Đang tới " : "Routing to ") + targetMap;
                        }
                        else
                            worker.Status = (MainController.language == 0 ? "Đang dò K" : "Scanning K") + payload.zone;
                    }
                }
                Publish();
                return;
            }

            if (cmd == CmdReady)
            {
                lock (_sync)
                {
                    if (!IsCurrentLocked(payload) || (_state != BossHuntState.Rallying && _state != BossHuntState.Fighting))
                        return;

                    BossHuntWorkerSnapshot worker;
                    if (_workers.TryGetValue(account.ID, out worker))
                    {
                        TouchWorkerLocked(account, payload);
                        worker.Ready = true;
                        worker.Failed = false;
                        worker.Unresponsive = false;
                        worker.TargetHp = payload.targetHp;
                        worker.LastAction = "READY";
                        worker.Status = MainController.language == 0 ? "Đã tới - đang đánh" : "Ready - fighting";
                        AddTimelineLocked("READY", account.ID, "map=" + payload.mapId + ";zone=" + payload.zone + ";hp=" + payload.targetHp);
                    }

                    bool anyReady;
                    if (AllConnectedWorkersSettledLocked(out anyReady) && anyReady)
                        _state = BossHuntState.Fighting;
                }
                Publish();
            }
        }

        public void HandleDisconnected(Account account)
        {
            if (account == null)
                return;

            BossHuntDiagnostics.Log("MANAGER", "WORKER_DISCONNECTED", _sessionId, account.ID, _bossName, _state.ToString(), "");

            List<Account> reassign = null;
            int sessionId = 0;
            int generation = 0;
            string boss = "";
            int startZone = 0;
            BossHuntBossSnapshot knownBoss = null;
            string stopReason = null;

            lock (_sync)
            {
                BossHuntWorkerSnapshot worker;
                if (!_workers.TryGetValue(account.ID, out worker))
                    return;

                worker.Status = MainController.language == 0 ? "Mất kết nối" : "Disconnected";
                worker.LastAction = "DISCONNECTED";
                worker.Ready = false;
                worker.Failed = true;
                worker.Unresponsive = false;
                worker.LastEventUtc = DateTime.UtcNow;
                AddTimelineLocked("DISCONNECTED", account.ID, "");
                ClearActiveZoneLocked(account.ID);

                if (!IsRunningState(_state))
                    return;

                if (_state == BossHuntState.Scanning)
                {
                    reassign = GetHealthySessionAccountsLocked();
                    if (reassign.Count == 0)
                    {
                        reassign = null;
                        stopReason = MainController.language == 0 ? "Tất cả tài khoản đã mất kết nối" : "All accounts disconnected";
                    }
                    else
                    {
                        _assignmentGeneration++;
                        generation = _assignmentGeneration;
                        sessionId = _sessionId;
                        boss = _bossName;
                        startZone = _startZone;
                        PrepareAssignmentsLocked(reassign, true);
                        knownBoss = CloneBoss(FindLatestBossLocked(_bossName, true));
                    }
                }
                else
                {
                    bool anyReady;
                    bool settled = AllConnectedWorkersSettledLocked(out anyReady);
                    if (!HasUsableWorkerLocked())
                        stopReason = MainController.language == 0 ? "Không còn tài khoản hoạt động" : "No active worker remains";
                    else if (settled && anyReady)
                        _state = BossHuntState.Fighting;
                }
            }

            if (!string.IsNullOrEmpty(stopReason))
            {
                Stop(stopReason);
                return;
            }
            if (reassign != null && reassign.Count > 0)
                SendScanAssignments(reassign, sessionId, generation, boss, startZone, knownBoss);
            Publish();
        }

        public BossHuntSnapshot GetSnapshot()
        {
            lock (_sync)
            {
                BossHuntSnapshot snapshot = new BossHuntSnapshot
                {
                    SessionId = _sessionId,
                    AssignmentGeneration = _assignmentGeneration,
                    BossName = _bossName,
                    StartZone = _startZone,
                    State = _state,
                    FoundMapId = _foundMapId,
                    FoundMapName = _foundMapName,
                    FoundZone = _foundZone,
                    FinderAccountId = _finderAccountId,
                    FinderUsername = GetWorkerUsernameLocked(_finderAccountId),
                    UniqueCoverageCount = GetUniqueCoverageCountLocked(),
                    CoverageTotalZones = GetCoverageTotalZonesLocked(),
                    StopReason = _stopReason,
                    TargetBoss = CloneBoss(FindLatestBossLocked(_bossName, false))
                };

                foreach (BossHuntWorkerSnapshot worker in _workers.Values)
                    snapshot.Workers.Add(CloneWorker(worker));
                snapshot.Workers.Sort(delegate(BossHuntWorkerSnapshot a, BossHuntWorkerSnapshot b) { return a.AccountId.CompareTo(b.AccountId); });

                int start = Math.Max(0, _bossEvents.Count - 30);
                for (int i = start; i < _bossEvents.Count; i++)
                    snapshot.RecentBossEvents.Add(CloneBossEvent(_bossEvents[i]));

                int timelineStart = Math.Max(0, _timeline.Count - 80);
                for (int i = timelineStart; i < _timeline.Count; i++)
                    snapshot.RecentTimeline.Add(CloneTimeline(_timeline[i]));

                return snapshot;
            }
        }

        private void HandleBossSpawn(Account account, BossHuntPayload payload)
        {
            if (string.IsNullOrEmpty(payload.bossName) || payload.mapId < 0)
                return;

            DateTime observedUtc = ReadObservedUtc(payload.observedAtTicks);
            BossHuntBossSnapshot canonical;
            bool shouldBroadcast = false;

            lock (_sync)
            {
                string key = NormalizeBossName(payload.bossName).ToLowerInvariant();
                BossHuntBossSnapshot record;
                if (!_bossRecords.TryGetValue(key, out record))
                {
                    record = new BossHuntBossSnapshot { BossName = NormalizeBossName(payload.bossName) };
                    _bossRecords[key] = record;
                }

                AddSourceAccount(record, account.ID);

                bool olderThanKnownDeath = !record.Alive &&
                                           record.DiedAtUtc != DateTime.MinValue &&
                                           observedUtc <= record.DiedAtUtc;
                bool newer = !olderThanKnownDeath &&
                             (record.SpawnedAtUtc == DateTime.MinValue || observedUtc >= record.SpawnedAtUtc);
                bool newEvent = !olderThanKnownDeath &&
                                (!record.Alive || record.MapId != payload.mapId || record.Zone != payload.zone ||
                                 record.SpawnedAtUtc == DateTime.MinValue ||
                                 Math.Abs(observedUtc.Subtract(record.SpawnedAtUtc).TotalSeconds) > 5.0);

                if (newer)
                {
                    record.BossName = NormalizeBossName(payload.bossName);
                    record.MapId = payload.mapId;
                    record.MapName = payload.mapName ?? "";
                    record.Zone = payload.zone;
                    record.Alive = true;
                    record.SpawnedAtUtc = observedUtc;
                    record.DiedAtUtc = DateTime.MinValue;
                    record.Killer = "";
                    record.RawSpawn = payload.rawMessage ?? "";
                    record.RawDeath = "";
                    record.LastSourceAccountId = account.ID;
                    shouldBroadcast = DateTime.UtcNow.Subtract(record.SpawnedAtUtc).TotalMinutes <= BossLocationFreshMinutes;
                }

                if (newEvent)
                {
                    AddBossEventLocked("SPAWN", record, observedUtc, account.ID, "", payload.rawMessage);
                    if (_sessionId > 0 && BossMatches(record.BossName, _bossName))
                        AddTimelineLocked("BOSS_SPAWN", account.ID, record.MapName + " K" + record.Zone + " | " + (payload.rawMessage ?? ""));
                }

                canonical = CloneBoss(record);
            }

            BossHuntDiagnostics.Log("MANAGER", "BOSS_SPAWN", 0, account.ID, payload.bossName, "GLOBAL",
                "map=" + payload.mapId + ";zone=" + payload.zone + ";at=" + observedUtc.ToString("o"));

            if (shouldBroadcast && canonical != null)
                Broadcast(GetConnectedAccounts(), CmdBossSync, CreateBossSyncPayload(canonical));
            Publish();
        }

        private void HandleBossDeath(Account account, BossHuntPayload payload)
        {
            if (string.IsNullOrEmpty(payload.bossName))
                return;

            DateTime observedUtc = ReadObservedUtc(payload.observedAtTicks);
            bool matchesCurrent;
            string killer = payload.killer ?? "";

            lock (_sync)
            {
                bool updatedAny = false;
                foreach (BossHuntBossSnapshot record in _bossRecords.Values)
                {
                    if (!BossMatches(record.BossName, payload.bossName) && !BossMatches(payload.bossName, record.BossName))
                        continue;

                    bool newEvent = record.Alive || record.DiedAtUtc == DateTime.MinValue ||
                                    Math.Abs(observedUtc.Subtract(record.DiedAtUtc).TotalSeconds) > 5.0;
                    record.Alive = false;
                    record.DiedAtUtc = observedUtc;
                    record.Killer = killer;
                    record.RawDeath = payload.rawMessage ?? "";
                    record.LastSourceAccountId = account.ID;
                    AddSourceAccount(record, account.ID);
                    if (newEvent)
                    {
                        AddBossEventLocked("DEATH", record, observedUtc, account.ID, killer, payload.rawMessage);
                        if (_sessionId > 0 && BossMatches(record.BossName, _bossName))
                            AddTimelineLocked("BOSS_DEATH", account.ID, "killer=" + killer + " | " + (payload.rawMessage ?? ""));
                    }
                    updatedAny = true;
                }

                if (!updatedAny)
                {
                    string key = NormalizeBossName(payload.bossName).ToLowerInvariant();
                    BossHuntBossSnapshot record = new BossHuntBossSnapshot
                    {
                        BossName = NormalizeBossName(payload.bossName),
                        Alive = false,
                        DiedAtUtc = observedUtc,
                        Killer = killer,
                        RawDeath = payload.rawMessage ?? "",
                        LastSourceAccountId = account.ID
                    };
                    AddSourceAccount(record, account.ID);
                    _bossRecords[key] = record;
                    AddBossEventLocked("DEATH", record, observedUtc, account.ID, killer, payload.rawMessage);
                    if (_sessionId > 0 && BossMatches(record.BossName, _bossName))
                        AddTimelineLocked("BOSS_DEATH", account.ID, "killer=" + killer + " | " + (payload.rawMessage ?? ""));
                }

                matchesCurrent = IsRunningState(_state) && BossMatches(payload.bossName, _bossName);
            }

            BossHuntDiagnostics.Log("MANAGER", "BOSS_DEATH", 0, account.ID, payload.bossName, "GLOBAL",
                "killer=" + killer + ";at=" + observedUtc.ToString("o"));

            BossHuntPayload invalidate = new BossHuntPayload
            {
                bossName = payload.bossName,
                observedAtTicks = observedUtc.Ticks,
                killer = killer,
                rawMessage = payload.rawMessage ?? "",
                eventName = "DEATH_SYNC"
            };
            Broadcast(GetConnectedAccounts(), CmdBossInvalidate, invalidate);

            if (matchesCurrent)
            {
                string reason = MainController.language == 0 ? "Boss mục tiêu đã chết" : "Target boss died";
                if (!string.IsNullOrEmpty(killer))
                    reason += (MainController.language == 0 ? " - Người hạ: " : " - Killer: ") + killer;
                Stop(reason);
            }
            else
                Publish();
        }

        private void HandleHeartbeat(Account account, BossHuntPayload payload)
        {
            lock (_sync)
            {
                if (!IsCurrentLocked(payload))
                {
                    BossHuntDiagnostics.Log("MANAGER", "STALE_HEARTBEAT", payload.sessionId, account.ID, payload.bossName, _state.ToString(),
                        "generation=" + payload.assignmentGeneration + ";current=" + _assignmentGeneration);
                    return;
                }

                BossHuntWorkerSnapshot worker;
                if (!_workers.TryGetValue(account.ID, out worker))
                    return;

                worker.LastHeartbeatUtc = DateTime.UtcNow;
                worker.MapId = payload.mapId;
                worker.MapName = payload.mapName ?? "";
                worker.Zone = payload.zone;
                worker.ScanCycle = payload.scanCycle;
                worker.EntityCount = payload.entityCount;
                worker.BossCount = payload.bossCount;
                worker.TargetHp = payload.targetHp;
                worker.LastAction = string.IsNullOrEmpty(payload.detail) ? worker.LastAction : payload.detail;
            }
        }

        private void HandleTelemetry(Account account, BossHuntPayload payload)
        {
            bool changed = false;
            lock (_sync)
            {
                if (!IsCurrentLocked(payload))
                {
                    BossHuntDiagnostics.Log("MANAGER", "STALE_TELEMETRY", payload.sessionId, account.ID, payload.bossName, _state.ToString(),
                        "generation=" + payload.assignmentGeneration + ";event=" + (payload.eventName ?? ""));
                    return;
                }

                BossHuntWorkerSnapshot worker;
                if (!_workers.TryGetValue(account.ID, out worker))
                    return;

                TouchWorkerLocked(account, payload);
                worker.ScanCycle = payload.scanCycle;
                worker.EntityCount = payload.entityCount;
                worker.BossCount = payload.bossCount;
                if (payload.targetHp >= 0)
                    worker.TargetHp = payload.targetHp;
                string eventName = payload.eventName ?? "";
                worker.LastAction = eventName;

                if (eventName == "ZONE_PLAN")
                {
                    worker.AssignedZones = payload.assignedZones ?? "";
                    worker.TotalZones = payload.totalZones;
                    AddTimelineLocked("ZONE_PLAN", account.ID, "assigned=" + worker.AssignedZones + ";total=" + worker.TotalZones);
                    changed = true;
                }
                else if (eventName == "ZONE_ENTER")
                {
                    RecordZoneEnterLocked(worker, payload);
                    AddTimelineLocked("ZONE_ENTER", account.ID, "K" + payload.zone + ";cycle=" + payload.scanCycle +
                        ";entities=" + payload.entityCount + ";bosses=" + payload.bossCount);
                    changed = true;
                }
                else if (eventName == "ZONE_CLEAR")
                {
                    RecordZoneClearLocked(worker, payload);
                    AddTimelineLocked("ZONE_CLEAR", account.ID, "K" + payload.zone + ";cycle=" + payload.scanCycle +
                        ";entities=" + payload.entityCount + ";bosses=" + payload.bossCount);
                    changed = true;
                }
                else if (eventName == "ZONE_FAILED")
                {
                    worker.ZoneFailureCount++;
                    worker.LastZoneFailure = "K" + payload.zone + " " + (payload.detail ?? "");
                    AppendZoneHistory(worker, "G" + payload.assignmentGeneration + " K" + payload.zone + " FAIL");
                    AddTimelineLocked("ZONE_FAILED", account.ID, worker.LastZoneFailure);
                    changed = true;
                }
                else if (eventName == "ENTITY_SNAPSHOT")
                {
                    changed = true;
                }
                else if (eventName == "SCAN_ROUTE")
                {
                    worker.Status = (MainController.language == 0 ? "Đang tới map boss" : "Routing to boss map") +
                                    " | " + (payload.detail ?? "");
                    AddTimelineLocked("SCAN_ROUTE", account.ID, payload.detail ?? "");
                    changed = true;
                }
                else if (eventName == "RALLY_ROUTE")
                {
                    worker.Status = (MainController.language == 0 ? "Rally - đang tới map" : "Rally - routing map") +
                                    " | " + (payload.detail ?? "");
                    AddTimelineLocked("RALLY_ROUTE", account.ID, payload.detail ?? "");
                    changed = true;
                }
                else if (eventName == "RALLY_ZONE")
                {
                    worker.Status = (MainController.language == 0 ? "Rally - đang vào khu" : "Rally - entering zone") +
                                    " | " + (payload.detail ?? "");
                    AddTimelineLocked("RALLY_ZONE", account.ID, payload.detail ?? "");
                    changed = true;
                }
                else if (eventName == "RALLY_ZONE_ARRIVED")
                {
                    worker.Status = MainController.language == 0 ? "Rally - đã tới khu, chờ boss" : "Rally - zone reached, waiting target";
                    AddTimelineLocked("RALLY_ZONE_ARRIVED", account.ID, "K" + payload.zone);
                    changed = true;
                }
                else if (eventName == "FIGHTING")
                {
                    worker.Status = MainController.language == 0 ? "Đang đánh boss" : "Fighting boss";
                    AddTimelineLocked("FIGHTING", account.ID, "hp=" + payload.targetHp);
                    changed = true;
                }
            }

            if (changed)
                Publish();
        }

        private void HandleFailed(Account account, BossHuntPayload payload)
        {
            List<Account> reassign = null;
            int sessionId = 0;
            int generation = 0;
            string boss = "";
            int startZone = 0;
            BossHuntBossSnapshot knownBoss = null;
            string stopReason = null;

            lock (_sync)
            {
                if (!IsCurrentLocked(payload))
                    return;

                BossHuntWorkerSnapshot worker;
                if (_workers.TryGetValue(account.ID, out worker))
                {
                    TouchWorkerLocked(account, payload);
                    worker.Ready = false;
                    worker.Failed = true;
                    worker.Unresponsive = false;
                    worker.Status = (MainController.language == 0 ? "Lỗi: " : "Failed: ") +
                                    (string.IsNullOrEmpty(payload.detail) ? "UNKNOWN" : payload.detail);
                    worker.LastAction = "FAILED";
                    AddTimelineLocked("FAILED", account.ID, payload.detail ?? "UNKNOWN");
                    ClearActiveZoneLocked(account.ID);
                }

                if (_state == BossHuntState.Scanning)
                {
                    reassign = GetHealthySessionAccountsLocked();
                    if (reassign.Count == 0)
                    {
                        reassign = null;
                        stopReason = MainController.language == 0 ? "Không còn tài khoản dò khả dụng" : "No scanning worker remains";
                    }
                    else
                    {
                        _assignmentGeneration++;
                        generation = _assignmentGeneration;
                        sessionId = _sessionId;
                        boss = _bossName;
                        startZone = _startZone;
                        PrepareAssignmentsLocked(reassign, true);
                        knownBoss = CloneBoss(FindLatestBossLocked(_bossName, true));
                    }
                }
                else if (_state == BossHuntState.Rallying || _state == BossHuntState.Fighting)
                {
                    bool anyReady;
                    if (!HasUsableWorkerLocked())
                        stopReason = MainController.language == 0 ? "Không còn tài khoản nào có thể tới boss" : "No account can reach the boss";
                    else if (AllConnectedWorkersSettledLocked(out anyReady) && anyReady)
                        _state = BossHuntState.Fighting;
                }
            }

            if (!string.IsNullOrEmpty(stopReason))
            {
                Stop(stopReason);
                return;
            }
            if (reassign != null && reassign.Count > 0)
                SendScanAssignments(reassign, sessionId, generation, boss, startZone, knownBoss);
            Publish();
        }

        private void HandleFound(Account account, BossHuntPayload payload)
        {
            List<Account> targets;
            BossHuntPayload rally;
            lock (_sync)
            {
                if (!IsCurrentLocked(payload) || _state != BossHuntState.Scanning || !BossMatches(payload.bossName, _bossName))
                    return;

                TouchWorkerLocked(account, payload);
                ClearActiveZoneLocked(account.ID);
                _state = BossHuntState.Rallying;
                _foundMapId = payload.mapId;
                _foundMapName = payload.mapName ?? "";
                _foundZone = payload.zone;
                _finderAccountId = account.ID;

                BossHuntWorkerSnapshot finder;
                if (_workers.TryGetValue(account.ID, out finder))
                {
                    finder.Ready = false;
                    finder.Failed = false;
                    finder.Unresponsive = false;
                    finder.TargetHp = payload.targetHp;
                    finder.LastAction = "FOUND";
                    finder.Status = MainController.language == 0 ? "Đã tìm thấy boss" : "Boss found";
                    AddTimelineLocked("FOUND", account.ID, _foundMapName + " K" + _foundZone + ";hp=" + payload.targetHp);
                }

                targets = new List<Account>();
                for (int i = 0; i < _sessionAccounts.Count; i++)
                {
                    Account target = _sessionAccounts[i];
                    BossHuntWorkerSnapshot worker;
                    if (!IsConnected(target) || !_workers.TryGetValue(target.ID, out worker) || worker.Failed || worker.Unresponsive)
                        continue;
                    targets.Add(target);
                    if (target.ID != account.ID)
                        worker.Status = MainController.language == 0 ? "Đang tới boss" : "Rallying";
                }
                rally = CreatePayload();
                AddTimelineLocked("RALLY", account.ID, _foundMapName + " K" + _foundZone + ";targets=" + targets.Count);
            }

            BossHuntDiagnostics.Log("MANAGER", "RALLY_BROADCAST", rally.sessionId, account.ID, rally.bossName, BossHuntState.Rallying.ToString(),
                "generation=" + rally.assignmentGeneration + ";map=" + rally.mapId + ";zone=" + rally.zone + ";targets=" + targets.Count);
            Broadcast(targets, CmdRally, rally);
            Publish();
        }

        private void WatchdogTick(object state)
        {
            List<Account> reassign = null;
            List<Account> timedOut = new List<Account>();
            int sessionId = 0;
            int generation = 0;
            string boss = "";
            int startZone = 0;
            BossHuntBossSnapshot knownBoss = null;
            string stopReason = null;
            bool changed = false;

            lock (_sync)
            {
                if (!IsRunningState(_state))
                    return;

                DateTime now = DateTime.UtcNow;
                for (int i = 0; i < _sessionAccounts.Count; i++)
                {
                    Account account = _sessionAccounts[i];
                    BossHuntWorkerSnapshot worker;
                    if (!IsConnected(account) || !_workers.TryGetValue(account.ID, out worker) || worker.Failed || worker.Unresponsive)
                        continue;

                    if (worker.LastHeartbeatUtc == DateTime.MinValue)
                        worker.LastHeartbeatUtc = now;

                    if (now.Subtract(worker.LastHeartbeatUtc).TotalSeconds <= HeartbeatTimeoutSeconds)
                        continue;

                    worker.Unresponsive = true;
                    worker.Failed = true;
                    worker.Ready = false;
                    worker.Status = MainController.language == 0 ? "Không phản hồi >8s" : "No heartbeat >8s";
                    worker.LastEventUtc = now;
                    worker.LastAction = "WATCHDOG_TIMEOUT";
                    AddTimelineLocked("WATCHDOG_TIMEOUT", account.ID, ">8s");
                    ClearActiveZoneLocked(account.ID);
                    timedOut.Add(account);
                    changed = true;

                    BossHuntDiagnostics.Log("MANAGER", "WATCHDOG_TIMEOUT", _sessionId, account.ID, _bossName, _state.ToString(),
                        "generation=" + _assignmentGeneration);
                }

                if (timedOut.Count > 0 && _state == BossHuntState.Scanning)
                {
                    reassign = GetHealthySessionAccountsLocked();
                    if (reassign.Count == 0)
                    {
                        reassign = null;
                        stopReason = MainController.language == 0 ? "Không còn worker phản hồi" : "No responsive worker remains";
                    }
                    else
                    {
                        _assignmentGeneration++;
                        generation = _assignmentGeneration;
                        sessionId = _sessionId;
                        boss = _bossName;
                        startZone = _startZone;
                        PrepareAssignmentsLocked(reassign, true);
                        knownBoss = CloneBoss(FindLatestBossLocked(_bossName, true));
                    }
                }
                else if (timedOut.Count > 0 && (_state == BossHuntState.Rallying || _state == BossHuntState.Fighting))
                {
                    bool anyReady;
                    if (!HasUsableWorkerLocked())
                        stopReason = MainController.language == 0 ? "Không còn worker phản hồi" : "No responsive worker remains";
                    else if (AllConnectedWorkersSettledLocked(out anyReady) && anyReady)
                        _state = BossHuntState.Fighting;
                }
            }

            if (!string.IsNullOrEmpty(stopReason))
            {
                Stop(stopReason);
                return;
            }

            if (reassign != null && reassign.Count > 0)
            {
                BossHuntPayload stopPayload = new BossHuntPayload
                {
                    sessionId = sessionId,
                    assignmentGeneration = generation,
                    bossName = boss,
                    detail = "WATCHDOG_REASSIGN"
                };
                for (int i = 0; i < timedOut.Count; i++)
                    Send(timedOut[i], CmdStop, stopPayload);

                SendScanAssignments(reassign, sessionId, generation, boss, startZone, knownBoss);
            }

            if (changed || reassign != null)
                Publish();
        }

        private void SendScanAssignments(List<Account> accounts, int sessionId, int generation, string bossName, int startZone, BossHuntBossSnapshot knownBoss)
        {
            int count = accounts.Count;
            for (int i = 0; i < count; i++)
            {
                BossHuntPayload payload = new BossHuntPayload
                {
                    sessionId = sessionId,
                    assignmentGeneration = generation,
                    bossName = bossName,
                    startZone = startZone,
                    workerIndex = i,
                    workerCount = count,
                    accountId = accounts[i].ID
                };
                if (knownBoss != null && knownBoss.Alive && knownBoss.MapId >= 0)
                {
                    payload.mapId = knownBoss.MapId;
                    payload.mapName = knownBoss.MapName;
                    payload.zone = knownBoss.Zone;
                    payload.observedAtTicks = knownBoss.SpawnedAtUtc.Ticks;
                }
                else
                {
                    payload.mapId = -1;
                    payload.zone = -1;
                }

                BossHuntDiagnostics.Log("MANAGER", "ASSIGN", sessionId, accounts[i].ID, bossName, BossHuntState.Scanning.ToString(),
                    "generation=" + generation + ";worker=" + i + "/" + count + ";startZone=" + startZone +
                    ";canonicalMap=" + payload.mapId + ";canonicalZone=" + payload.zone);
                lock (_sync)
                    AddTimelineLocked("ASSIGN", accounts[i].ID, "G" + generation + " W" + (i + 1) + "/" + count);
                Send(accounts[i], CmdStartScan, payload);
            }
        }

        private void PrepareAssignmentsLocked(List<Account> accounts, bool reassign)
        {
            _sessionAccounts.Clear();
            _sessionAccounts.AddRange(accounts);
            _zoneLedger.Clear();
            _activeZoneByAccount.Clear();

            DateTime now = DateTime.UtcNow;
            for (int i = 0; i < accounts.Count; i++)
            {
                Account account = accounts[i];
                BossHuntWorkerSnapshot worker;
                if (!_workers.TryGetValue(account.ID, out worker))
                {
                    worker = new BossHuntWorkerSnapshot
                    {
                        AccountId = account.ID,
                        Username = account.Username ?? ""
                    };
                    _workers[account.ID] = worker;
                }

                worker.WorkerIndex = i;
                worker.WorkerCount = accounts.Count;
                worker.AssignmentGeneration = _assignmentGeneration;
                worker.Ready = false;
                worker.Failed = false;
                worker.Unresponsive = false;
                worker.Zone = -1;
                worker.ScanCycle = 0;
                worker.AssignedZones = "";
                worker.TotalZones = 0;
                worker.EntityCount = -1;
                worker.BossCount = -1;
                worker.TargetHp = -1;
                worker.ZoneEnteredAtUtc = DateTime.MinValue;
                worker.LastAction = "ASSIGNED";
                worker.DuplicateWarning = "";
                worker.LastHeartbeatUtc = now;
                worker.LastEventUtc = now;
                worker.Status = reassign
                    ? (MainController.language == 0 ? "Phân lại khu" : "Reassigning")
                    : (MainController.language == 0 ? "Chuẩn bị dò" : "Preparing");
            }
        }

        private List<Account> GetHealthySessionAccountsLocked()
        {
            List<Account> result = new List<Account>();
            for (int i = 0; i < _sessionAccounts.Count; i++)
            {
                Account account = _sessionAccounts[i];
                BossHuntWorkerSnapshot worker;
                if (!IsConnected(account) || !_workers.TryGetValue(account.ID, out worker))
                    continue;
                if (worker.Failed || worker.Unresponsive)
                    continue;
                result.Add(account);
            }
            result.Sort(delegate(Account a, Account b) { return a.ID.CompareTo(b.ID); });
            return result;
        }

        private bool HasUsableWorkerLocked()
        {
            for (int i = 0; i < _sessionAccounts.Count; i++)
            {
                Account account = _sessionAccounts[i];
                BossHuntWorkerSnapshot worker;
                if (IsConnected(account) && _workers.TryGetValue(account.ID, out worker) && !worker.Failed && !worker.Unresponsive)
                    return true;
            }
            return false;
        }

        private void TouchWorkerLocked(Account account, BossHuntPayload payload)
        {
            BossHuntWorkerSnapshot worker;
            if (!_workers.TryGetValue(account.ID, out worker))
                return;

            DateTime now = DateTime.UtcNow;
            worker.LastEventUtc = now;
            worker.LastHeartbeatUtc = now;
            worker.MapId = payload.mapId;
            worker.MapName = payload.mapName ?? "";
            worker.Zone = payload.zone;
            worker.ScanCycle = payload.scanCycle;
        }

        private void RecordZoneEnterLocked(BossHuntWorkerSnapshot worker, BossHuntPayload payload)
        {
            ClearActiveZoneLocked(worker.AccountId);

            string key = ZoneKey(payload.assignmentGeneration, payload.mapId, payload.zone);
            BossHuntZoneLedgerEntry entry;
            if (!_zoneLedger.TryGetValue(key, out entry))
            {
                entry = new BossHuntZoneLedgerEntry
                {
                    Generation = payload.assignmentGeneration,
                    MapId = payload.mapId,
                    Zone = payload.zone
                };
                _zoneLedger[key] = entry;
            }

            int conflictAccountId = -1;
            if (entry.ActiveAccountId > 0 && entry.ActiveAccountId != worker.AccountId)
                conflictAccountId = entry.ActiveAccountId;
            else if (entry.LastScannedByAccountId > 0 && entry.LastScannedByAccountId != worker.AccountId)
                conflictAccountId = entry.LastScannedByAccountId;

            if (conflictAccountId > 0)
            {
                worker.DuplicateWarning = "K" + payload.zone + " trùng acc #" + conflictAccountId;
                BossHuntWorkerSnapshot other;
                if (_workers.TryGetValue(conflictAccountId, out other))
                    other.DuplicateWarning = "K" + payload.zone + " trùng acc #" + worker.AccountId;

                BossHuntDiagnostics.Log("MANAGER", "DUPLICATE_ZONE", _sessionId, worker.AccountId, _bossName, _state.ToString(),
                    "generation=" + payload.assignmentGeneration + ";map=" + payload.mapId + ";zone=" + payload.zone +
                    ";other=" + conflictAccountId);
                AddTimelineLocked("DUPLICATE_ZONE", worker.AccountId, "K" + payload.zone + " vs #" + conflictAccountId);
            }

            entry.ActiveAccountId = worker.AccountId;
            entry.LastEnterUtc = DateTime.UtcNow;
            worker.ZoneEnteredAtUtc = entry.LastEnterUtc;
            _activeZoneByAccount[worker.AccountId] = key;
            AppendZoneHistory(worker, "G" + payload.assignmentGeneration + " K" + payload.zone + " ENTER");
        }

        private void RecordZoneClearLocked(BossHuntWorkerSnapshot worker, BossHuntPayload payload)
        {
            string key = ZoneKey(payload.assignmentGeneration, payload.mapId, payload.zone);
            BossHuntZoneLedgerEntry entry;
            if (!_zoneLedger.TryGetValue(key, out entry))
            {
                entry = new BossHuntZoneLedgerEntry
                {
                    Generation = payload.assignmentGeneration,
                    MapId = payload.mapId,
                    Zone = payload.zone
                };
                _zoneLedger[key] = entry;
            }

            if (entry.ActiveAccountId == worker.AccountId)
                entry.ActiveAccountId = -1;
            entry.LastScannedByAccountId = worker.AccountId;
            entry.LastClearUtc = DateTime.UtcNow;
            worker.ZoneEnteredAtUtc = DateTime.MinValue;
            _activeZoneByAccount.Remove(worker.AccountId);

            string token = "G" + payload.assignmentGeneration + ":M" + payload.mapId + ":K" + payload.zone;
            if (!worker.ScannedZones.Contains(token))
                worker.ScannedZones.Add(token);
            if (worker.ScannedZones.Count > 60)
                worker.ScannedZones.RemoveAt(0);

            AppendZoneHistory(worker, "G" + payload.assignmentGeneration + " K" + payload.zone + " CLEAR C" + payload.scanCycle);
        }

        private void ClearActiveZoneLocked(int accountId)
        {
            string key;
            if (!_activeZoneByAccount.TryGetValue(accountId, out key))
                return;

            BossHuntZoneLedgerEntry entry;
            if (_zoneLedger.TryGetValue(key, out entry) && entry.ActiveAccountId == accountId)
                entry.ActiveAccountId = -1;
            _activeZoneByAccount.Remove(accountId);
        }

        private static void AppendZoneHistory(BossHuntWorkerSnapshot worker, string value)
        {
            worker.ZoneHistory.Add(DateTime.Now.ToString("HH:mm:ss") + " " + value);
            if (worker.ZoneHistory.Count > 100)
                worker.ZoneHistory.RemoveAt(0);
        }

        private static string ZoneKey(int generation, int mapId, int zone)
        {
            return generation + "|" + mapId + "|" + zone;
        }

        private void AddBossEventLocked(string type, BossHuntBossSnapshot boss, DateTime atUtc, int sourceAccountId, string killer, string raw)
        {
            _bossEvents.Add(new BossHuntBossEventSnapshot
            {
                EventType = type,
                BossName = boss.BossName,
                MapId = boss.MapId,
                MapName = boss.MapName,
                Zone = boss.Zone,
                ObservedAtUtc = atUtc,
                Killer = killer ?? "",
                SourceAccountId = sourceAccountId,
                RawMessage = raw ?? ""
            });
            if (_bossEvents.Count > 200)
                _bossEvents.RemoveAt(0);
        }

        private static void AddSourceAccount(BossHuntBossSnapshot boss, int accountId)
        {
            if (accountId <= 0)
                return;
            if (!boss.SourceAccounts.Contains(accountId))
                boss.SourceAccounts.Add(accountId);
        }

        private BossHuntBossSnapshot FindLatestBossLocked(string bossName, bool aliveOnly)
        {
            if (string.IsNullOrEmpty(bossName))
                return null;

            BossHuntBossSnapshot best = null;
            DateTime now = DateTime.UtcNow;
            foreach (BossHuntBossSnapshot boss in _bossRecords.Values)
            {
                if (!BossMatches(boss.BossName, bossName))
                    continue;
                if (aliveOnly)
                {
                    if (!boss.Alive || boss.SpawnedAtUtc == DateTime.MinValue)
                        continue;
                    if (now.Subtract(boss.SpawnedAtUtc).TotalMinutes > BossLocationFreshMinutes)
                        continue;
                }

                DateTime candidateTime = boss.Alive ? boss.SpawnedAtUtc : boss.DiedAtUtc;
                DateTime bestTime = best == null ? DateTime.MinValue : (best.Alive ? best.SpawnedAtUtc : best.DiedAtUtc);
                if (best == null || candidateTime > bestTime)
                    best = boss;
            }
            return best;
        }

        private static BossHuntBossSnapshot CloneBoss(BossHuntBossSnapshot source)
        {
            if (source == null)
                return null;
            BossHuntBossSnapshot clone = new BossHuntBossSnapshot
            {
                BossName = source.BossName,
                MapId = source.MapId,
                MapName = source.MapName,
                Zone = source.Zone,
                Alive = source.Alive,
                Presence = GetPresence(source),
                SpawnedAtUtc = source.SpawnedAtUtc,
                DiedAtUtc = source.DiedAtUtc,
                Killer = source.Killer,
                RawSpawn = source.RawSpawn,
                RawDeath = source.RawDeath,
                LastSourceAccountId = source.LastSourceAccountId
            };
            clone.SourceAccounts.AddRange(source.SourceAccounts);
            return clone;
        }

        private static BossHuntBossEventSnapshot CloneBossEvent(BossHuntBossEventSnapshot source)
        {
            return new BossHuntBossEventSnapshot
            {
                EventType = source.EventType,
                BossName = source.BossName,
                MapId = source.MapId,
                MapName = source.MapName,
                Zone = source.Zone,
                ObservedAtUtc = source.ObservedAtUtc,
                Killer = source.Killer,
                SourceAccountId = source.SourceAccountId,
                RawMessage = source.RawMessage
            };
        }

        private static BossHuntWorkerSnapshot CloneWorker(BossHuntWorkerSnapshot source)
        {
            BossHuntWorkerSnapshot clone = new BossHuntWorkerSnapshot
            {
                AccountId = source.AccountId,
                Username = source.Username,
                WorkerIndex = source.WorkerIndex,
                WorkerCount = source.WorkerCount,
                AssignmentGeneration = source.AssignmentGeneration,
                Zone = source.Zone,
                MapId = source.MapId,
                MapName = source.MapName,
                Status = source.Status,
                Ready = source.Ready,
                Failed = source.Failed,
                Unresponsive = source.Unresponsive,
                LastHeartbeatUtc = source.LastHeartbeatUtc,
                LastEventUtc = source.LastEventUtc,
                ScanCycle = source.ScanCycle,
                AssignedZones = source.AssignedZones,
                TotalZones = source.TotalZones,
                EntityCount = source.EntityCount,
                BossCount = source.BossCount,
                TargetHp = source.TargetHp,
                ZoneEnteredAtUtc = source.ZoneEnteredAtUtc,
                ZoneFailureCount = source.ZoneFailureCount,
                LastZoneFailure = source.LastZoneFailure,
                LastAction = source.LastAction,
                DuplicateWarning = source.DuplicateWarning
            };
            clone.ScannedZones.AddRange(source.ScannedZones);
            clone.ZoneHistory.AddRange(source.ZoneHistory);
            return clone;
        }

        private static BossPresenceState GetPresence(BossHuntBossSnapshot boss)
        {
            if (boss == null)
                return BossPresenceState.Unknown;
            if (!boss.Alive)
                return BossPresenceState.Dead;
            if (boss.SpawnedAtUtc == DateTime.MinValue)
                return BossPresenceState.Unknown;
            if (DateTime.UtcNow.Subtract(boss.SpawnedAtUtc).TotalMinutes > BossLocationFreshMinutes)
                return BossPresenceState.Stale;
            return BossPresenceState.Alive;
        }

        private string GetWorkerUsernameLocked(int accountId)
        {
            BossHuntWorkerSnapshot worker;
            if (accountId > 0 && _workers.TryGetValue(accountId, out worker))
                return worker.Username ?? "";
            return "";
        }

        private int GetUniqueCoverageCountLocked()
        {
            int count = 0;
            foreach (BossHuntZoneLedgerEntry entry in _zoneLedger.Values)
            {
                if (entry.Generation == _assignmentGeneration && entry.LastClearUtc != DateTime.MinValue)
                    count++;
            }
            return count;
        }

        private int GetCoverageTotalZonesLocked()
        {
            int total = 0;
            foreach (BossHuntWorkerSnapshot worker in _workers.Values)
            {
                if (worker.AssignmentGeneration == _assignmentGeneration && worker.TotalZones > total)
                    total = worker.TotalZones;
            }
            return total;
        }

        private void AddTimelineLocked(string eventName, int accountId, string detail)
        {
            _timeline.Add(new BossHuntTimelineEntry
            {
                AtUtc = DateTime.UtcNow,
                AccountId = accountId,
                EventName = eventName ?? "",
                Detail = detail ?? ""
            });
            if (_timeline.Count > 300)
                _timeline.RemoveAt(0);
        }

        private static BossHuntTimelineEntry CloneTimeline(BossHuntTimelineEntry source)
        {
            return new BossHuntTimelineEntry
            {
                AtUtc = source.AtUtc,
                AccountId = source.AccountId,
                EventName = source.EventName,
                Detail = source.Detail
            };
        }

        private BossHuntPayload CreatePayload()
        {
            return new BossHuntPayload
            {
                sessionId = _sessionId,
                assignmentGeneration = _assignmentGeneration,
                bossName = _bossName,
                startZone = _startZone,
                mapId = _foundMapId,
                mapName = _foundMapName,
                zone = _foundZone,
                accountId = _finderAccountId
            };
        }

        private static BossHuntPayload CreateBossSyncPayload(BossHuntBossSnapshot boss)
        {
            return new BossHuntPayload
            {
                bossName = boss.BossName,
                mapId = boss.MapId,
                mapName = boss.MapName,
                zone = boss.Zone,
                observedAtTicks = boss.SpawnedAtUtc == DateTime.MinValue ? 0L : boss.SpawnedAtUtc.Ticks,
                rawMessage = boss.RawSpawn,
                eventName = "SPAWN_SYNC"
            };
        }

        private bool IsCurrentLocked(BossHuntPayload payload)
        {
            return payload.sessionId == _sessionId &&
                   payload.assignmentGeneration == _assignmentGeneration &&
                   _sessionId > 0 &&
                   IsRunningState(_state);
        }

        private bool AllConnectedWorkersSettledLocked(out bool anyReady)
        {
            bool anyConnected = false;
            anyReady = false;
            for (int i = 0; i < _sessionAccounts.Count; i++)
            {
                Account account = _sessionAccounts[i];
                if (!IsConnected(account))
                    continue;

                BossHuntWorkerSnapshot worker;
                if (!_workers.TryGetValue(account.ID, out worker))
                    return false;

                anyConnected = true;
                if (worker.Failed || worker.Unresponsive)
                    continue;
                if (!worker.Ready)
                    return false;
                anyReady = true;
            }
            return anyConnected;
        }

        private static bool IsRunningState(BossHuntState state)
        {
            return state == BossHuntState.Scanning || state == BossHuntState.Rallying || state == BossHuntState.Fighting;
        }

        private static string NormalizeBossName(string value)
        {
            return (value ?? "").Trim().Trim('[', ']', ':', '-', '.', ' ');
        }

        private static bool BossMatches(string value, string target)
        {
            value = NormalizeBossName(value);
            target = NormalizeBossName(target);
            if (value.Length == 0 || target.Length == 0)
                return false;
            if (value.Equals(target, StringComparison.OrdinalIgnoreCase))
                return true;
            if (!value.StartsWith(target, StringComparison.OrdinalIgnoreCase))
                return false;
            if (value.Length == target.Length)
                return true;
            char next = value[target.Length];
            return char.IsWhiteSpace(next) || next == '-' || next == '(' || next == '[';
        }

        private static DateTime ReadObservedUtc(long ticks)
        {
            if (ticks <= 0L)
                return DateTime.UtcNow;
            try
            {
                return new DateTime(ticks, DateTimeKind.Utc);
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }

        private static List<Account> GetConnectedAccounts()
        {
            List<Account> result = new List<Account>();
            if (TabData._instance == null)
                return result;
            List<Account> accounts = TabData._instance.GetAccounts();
            for (int i = 0; i < accounts.Count; i++)
            {
                if (IsConnected(accounts[i]))
                    result.Add(accounts[i]);
            }
            return result;
        }

        private static bool IsConnected(Account account)
        {
            if (account == null || account.workSocket == null)
                return false;
            string status = account.Status ?? "";
            if (status != "Đã kết nối" && status != "Connected")
                return false;
            try
            {
                return account.workSocket.Connected;
            }
            catch
            {
                return false;
            }
        }

        private void Broadcast(List<Account> accounts, int cmd, BossHuntPayload payload)
        {
            for (int i = 0; i < accounts.Count; i++)
            {
                if (IsConnected(accounts[i]))
                    Send(accounts[i], cmd, payload);
            }
        }

        private static void Send(Account account, int cmd, BossHuntPayload payload)
        {
            try
            {
                if (!IsConnected(account))
                    return;
                account.sendMessage(new SocketServer.vMessage
                {
                    cmd = cmd,
                    data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload))
                });
            }
            catch
            {
            }
        }

        private void Publish()
        {
            Action<BossHuntSnapshot> handler = Changed;
            if (handler != null)
                handler(GetSnapshot());
        }
    }
}
