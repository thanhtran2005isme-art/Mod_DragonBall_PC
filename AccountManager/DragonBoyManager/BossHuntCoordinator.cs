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
        public bool ZoneCapacityReported;
        public int ReportedMaxZone = -1;
        public DateTime ScanStartedAtUtc = DateTime.MinValue;
        public int ZoneClearCount;
        public int FailureCount;
        public int TimeoutCount;
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
        public int KillerId = -1;
        public string RawSpawn = "";
        public string RawDeath = "";
        public string DeathEvidence = "";
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
        public int KillerId = -1;
        public int SourceAccountId = -1;
        public string RawMessage = "";
        public string DeathEvidence = "";
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
        public DateTime SessionStartedAtUtc = DateTime.MinValue;
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
        public int HealthyWorkerCount;
        public int SessionWorkerCount;
        public double WorstHeartbeatAgeSeconds = -1.0;
        public string StopReason = "";
        public string LastUnparsedDeath = "";
        public DateTime LastUnparsedDeathUtc = DateTime.MinValue;
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
        public string targetBossName;
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
        public int killerId = -1;
        public long observedAtTicks;
        public int entityCount;
        public int bossCount;
        public int targetHp = -1;
        public int scanCycle;
        public int totalZones;
        public int maxZone = -1;
        public string assignedZones;
        public string[] bossNames;
    }

    internal sealed class PendingZoneAssignment
    {
        public Account Account;
        public BossHuntPayload Payload;
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
        public const int CmdZoneAssignment = 105;

        public const int CmdZone = 110;
        public const int CmdFound = 111;
        public const int CmdDead = 112;
        public const int CmdReady = 113;
        public const int CmdFailed = 114;
        public const int CmdBossSpawn = 115;
        public const int CmdBossDeath = 116;
        public const int CmdTelemetry = 117;
        public const int CmdHeartbeat = 118;
        public const int CmdBossCatalog = 119;

        private const int HeartbeatTimeoutSeconds = 8;
        private const int BossLocationFreshMinutes = 60;

        private static readonly BossHuntCoordinator _instance = new BossHuntCoordinator();
        private readonly object _sync = new object();
        private readonly Dictionary<int, BossHuntWorkerSnapshot> _workers = new Dictionary<int, BossHuntWorkerSnapshot>();
        private readonly List<Account> _sessionAccounts = new List<Account>();
        private readonly List<int> _requestedAccountIds = new List<int>();
        private readonly Dictionary<string, BossHuntBossSnapshot> _bossRecords = new Dictionary<string, BossHuntBossSnapshot>(StringComparer.OrdinalIgnoreCase);
        private readonly List<BossHuntBossEventSnapshot> _bossEvents = new List<BossHuntBossEventSnapshot>();
        private readonly Dictionary<string, BossHuntZoneLedgerEntry> _zoneLedger = new Dictionary<string, BossHuntZoneLedgerEntry>();
        private readonly Dictionary<int, string> _activeZoneByAccount = new Dictionary<int, string>();
        private readonly List<BossHuntTimelineEntry> _timeline = new List<BossHuntTimelineEntry>();
        private readonly Timer _watchdog;

        private int _sessionSeed;
        private int _sessionId;
        private int _assignmentGeneration;
        private int _zonePlanIssuedGeneration;
        private DateTime _sessionStartedAtUtc = DateTime.MinValue;
        private string _bossName = "";
        private BossHuntBossSnapshot _sessionTargetBoss;
        private int _startZone;
        private BossHuntState _state = BossHuntState.Idle;
        private int _foundMapId = -1;
        private string _foundMapName = "";
        private int _foundZone = -1;
        private int _finderAccountId = -1;
        private string _stopReason = "";
        private string _lastUnparsedDeath = "";
        private DateTime _lastUnparsedDeathUtc = DateTime.MinValue;

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
            List<Account> reassign = null;
            int reassignSessionId = 0;
            int reassignGeneration = 0;
            string reassignBoss = "";
            int reassignStartZone = 0;
            BossHuntBossSnapshot reassignKnownBoss = null;
            BossHuntPayload rejoinStart = null;
            BossHuntPayload rejoinRally = null;
            bool shouldPublish = false;

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
                            killerId = boss.KillerId,
                            rawMessage = boss.RawDeath,
                            observedAtTicks = boss.DiedAtUtc.Ticks,
                            eventName = "DEATH_SYNC"
                        });
                    }
                }

                if (IsRunningState(_state) && _requestedAccountIds.Contains(account.ID))
                {
                    AddSessionAccountLocked(account);

                    if (_state == BossHuntState.Scanning)
                    {
                        reassign = GetHealthySessionAccountsLocked();
                        if (!ContainsAccountId(reassign, account.ID))
                            reassign.Add(account);
                        reassign.Sort(delegate(Account a, Account b) { return a.ID.CompareTo(b.ID); });

                        _assignmentGeneration++;
                        reassignSessionId = _sessionId;
                        reassignGeneration = _assignmentGeneration;
                        reassignBoss = _bossName;
                        reassignStartZone = _startZone;
                        PrepareAssignmentsLocked(reassign, true);
                        reassignKnownBoss = CloneBoss(_sessionTargetBoss ?? FindLatestBossLocked(_bossName, true));
                        AddTimelineLocked("WORKER_REJOIN", account.ID,
                            "phase=Scanning;generation=" + _assignmentGeneration + ";workers=" + reassign.Count);
                    }
                    else
                    {
                        BossHuntWorkerSnapshot worker = EnsureWorkerLocked(account);
                        NormalizeSessionWorkerIndexesLocked();
                        worker.Ready = false;
                        worker.Failed = false;
                        worker.Unresponsive = false;
                        worker.AssignmentGeneration = _assignmentGeneration;
                        worker.LastHeartbeatUtc = now;
                        worker.LastEventUtc = now;
                        worker.LastAction = "REJOIN";
                        worker.Status = MainController.language == 0 ? "Kết nối lại - đang tới boss" : "Rejoined - rallying";

                        BossHuntBossSnapshot target = _sessionTargetBoss ?? FindLatestBossLocked(_bossName, true);
                        rejoinStart = new BossHuntPayload
                        {
                            sessionId = _sessionId,
                            assignmentGeneration = _assignmentGeneration,
                            bossName = _bossName,
                            targetBossName = target == null ? _bossName : target.BossName,
                            startZone = _startZone,
                            workerIndex = worker.WorkerIndex,
                            workerCount = Math.Max(1, worker.WorkerCount),
                            accountId = account.ID,
                            mapId = target != null && target.MapId >= 0 ? target.MapId : _foundMapId,
                            mapName = target != null && target.MapId >= 0 ? target.MapName : _foundMapName,
                            zone = target != null && target.Zone >= 0 ? target.Zone : _foundZone,
                            observedAtTicks = target == null || target.SpawnedAtUtc == DateTime.MinValue ? 0L : target.SpawnedAtUtc.Ticks
                        };
                        rejoinRally = CreatePayload();
                        rejoinRally.targetBossName = rejoinStart.targetBossName;
                        AddTimelineLocked("WORKER_REJOIN", account.ID,
                            "phase=" + _state + ";map=" + rejoinRally.mapId + ";zone=" + rejoinRally.zone);
                    }
                    shouldPublish = true;
                }
            }

            for (int i = 0; i < syncPayloads.Count; i++)
                Send(account, CmdBossSync, syncPayloads[i]);
            for (int i = 0; i < invalidatePayloads.Count; i++)
                Send(account, CmdBossInvalidate, invalidatePayloads[i]);

            if (reassign != null && reassign.Count > 0)
                SendScanAssignments(reassign, reassignSessionId, reassignGeneration, reassignBoss, reassignStartZone, reassignKnownBoss);
            else if (rejoinStart != null && rejoinRally != null)
            {
                Send(account, CmdStartScan, rejoinStart);
                Send(account, CmdRally, rejoinRally);
            }

            if (shouldPublish)
                Publish();
        }

        public bool Start(string bossName, int startZone, out string error)
        {
            return Start(bossName, startZone, null, out error);
        }

        public bool Start(string bossName, int startZone, List<Account> requestedAccounts, out string error)
        {
            error = "";
            bossName = (bossName ?? "").Trim();
            if (bossName.Length == 0)
            {
                error = MainController.language == 0 ? "Hãy nhập hoặc chọn boss cần săn." : "Choose or enter a boss name.";
                return false;
            }

            List<Account> requested = new List<Account>();
            if (requestedAccounts == null)
                requested.AddRange(GetConnectedAccounts());
            else
            {
                for (int i = 0; i < requestedAccounts.Count; i++)
                {
                    Account item = requestedAccounts[i];
                    if (item != null && !ContainsAccountId(requested, item.ID))
                        requested.Add(item);
                }
            }

            List<Account> accounts = new List<Account>();
            for (int i = 0; i < requested.Count; i++)
            {
                if (IsConnected(requested[i]))
                    accounts.Add(requested[i]);
            }

            if (accounts.Count == 0)
            {
                error = MainController.language == 0
                    ? "Không có tài khoản được chọn nào đang kết nối."
                    : "No requested account is connected.";
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
                _zonePlanIssuedGeneration = 0;
                _sessionStartedAtUtc = DateTime.UtcNow;
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
                _lastUnparsedDeath = "";
                _lastUnparsedDeathUtc = DateTime.MinValue;
                _workers.Clear();
                _sessionAccounts.Clear();
                _requestedAccountIds.Clear();
                _zoneLedger.Clear();
                _activeZoneByAccount.Clear();
                _timeline.Clear();

                for (int i = 0; i < requested.Count; i++)
                {
                    if (!_requestedAccountIds.Contains(requested[i].ID))
                        _requestedAccountIds.Add(requested[i].ID);
                }

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
                        LastEventUtc = now,
                        ScanStartedAtUtc = now
                    };
                }
                _sessionAccounts.AddRange(accounts);
                AddTimelineLocked("SESSION_START", -1,
                    "boss=" + bossName + ";workers=" + accounts.Count + ";requested=" + _requestedAccountIds.Count +
                    ";generation=" + generation);
                knownBoss = CloneBoss(FindLatestBossLocked(bossName, true));
                _sessionTargetBoss = CloneBoss(knownBoss);
                if (_sessionTargetBoss != null)
                    AddTimelineLocked("TARGET_LOCK", -1, DescribeBossInstance(_sessionTargetBoss));
            }

            BossHuntDiagnostics.Log("MANAGER", "SESSION_START", sessionId, -1, bossName, BossHuntState.Scanning.ToString(),
                "generation=" + generation + ";workers=" + accounts.Count + ";requested=" + requested.Count +
                ";startZone=" + Math.Max(0, startZone));
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
            if (cmd == CmdBossCatalog)
            {
                HandleBossCatalog(account, payload);
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
                string concreteBossName = "";
                lock (_sync)
                {
                    valid = IsCurrentLocked(payload) && BossMatches(payload.bossName, _bossName);
                    if (valid)
                    {
                        TouchWorkerLocked(account, payload);
                        if (_sessionTargetBoss != null && !string.IsNullOrEmpty(_sessionTargetBoss.BossName))
                            concreteBossName = _sessionTargetBoss.BossName;
                    }
                }

                if (valid)
                {
                    if (!string.IsNullOrEmpty(concreteBossName))
                    {
                        payload.bossName = concreteBossName;
                        payload.targetBossName = concreteBossName;
                    }
                    payload.killer = "";
                    payload.killerId = -1;
                    payload.observedAtTicks = DateTime.UtcNow.Ticks;
                    payload.rawMessage = string.IsNullOrEmpty(payload.detail)
                        ? "fallback:CmdDead"
                        : "fallback:CmdDead;" + payload.detail;
                    HandleBossDeath(account, payload);
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
                        else if (detail == "WAITING_ASSIGNMENT")
                            worker.Status = MainController.language == 0 ? "Chờ Manager chia khu" : "Waiting for Manager zone plan";
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
                        knownBoss = CloneBoss(_sessionTargetBoss ?? FindLatestBossLocked(_bossName, true));
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
                    SessionStartedAtUtc = _sessionStartedAtUtc,
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
                    HealthyWorkerCount = GetHealthySessionAccountsLocked().Count,
                    SessionWorkerCount = _requestedAccountIds.Count > 0 ? _requestedAccountIds.Count : _sessionAccounts.Count,
                    WorstHeartbeatAgeSeconds = GetWorstHeartbeatAgeSecondsLocked(),
                    StopReason = _stopReason,
                    LastUnparsedDeath = _lastUnparsedDeath,
                    LastUnparsedDeathUtc = _lastUnparsedDeathUtc,
                    TargetBoss = CloneBoss(_sessionTargetBoss ?? FindLatestBossLocked(_bossName, false))
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

            BossHuntCatalog.RememberBoss(payload.bossName);
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
                bool sameLiveEvent = record.Alive &&
                                     record.MapId == payload.mapId &&
                                     record.Zone == payload.zone;
                bool newEvent = !olderThanKnownDeath && !sameLiveEvent;

                if (!olderThanKnownDeath && sameLiveEvent)
                {
                    if (record.SpawnedAtUtc == DateTime.MinValue || observedUtc < record.SpawnedAtUtc)
                        record.SpawnedAtUtc = observedUtc;
                    if (string.IsNullOrEmpty(record.RawSpawn) && !string.IsNullOrEmpty(payload.rawMessage))
                        record.RawSpawn = payload.rawMessage;
                    record.LastSourceAccountId = account.ID;
                    shouldBroadcast = DateTime.UtcNow.Subtract(record.SpawnedAtUtc).TotalMinutes <= BossLocationFreshMinutes;
                }
                else if (newEvent)
                {
                    record.BossName = NormalizeBossName(payload.bossName);
                    record.MapId = payload.mapId;
                    record.MapName = payload.mapName ?? "";
                    record.Zone = payload.zone;
                    record.Alive = true;
                    record.Presence = BossPresenceState.Alive;
                    record.SpawnedAtUtc = observedUtc;
                    record.DiedAtUtc = DateTime.MinValue;
                    record.Killer = "";
                    record.KillerId = -1;
                    record.RawSpawn = payload.rawMessage ?? "";
                    record.RawDeath = "";
                    record.DeathEvidence = "";
                    record.LastSourceAccountId = account.ID;
                    shouldBroadcast = DateTime.UtcNow.Subtract(record.SpawnedAtUtc).TotalMinutes <= BossLocationFreshMinutes;

                    AddBossEventLocked("SPAWN", record, record.SpawnedAtUtc, account.ID, "", payload.rawMessage);
                    if (_sessionId > 0 && BossMatches(record.BossName, _bossName))
                        AddTimelineLocked("BOSS_SPAWN", account.ID, record.MapName + " K" + record.Zone + " | " + (payload.rawMessage ?? ""));
                }

                if (IsRunningState(_state) && BossMatches(record.BossName, _bossName))
                {
                    if (_sessionTargetBoss == null)
                    {
                        _sessionTargetBoss = CloneBoss(record);
                        AddTimelineLocked("TARGET_LOCK", account.ID, DescribeBossInstance(_sessionTargetBoss));
                    }
                    else if (SameBossInstance(_sessionTargetBoss, record))
                    {
                        _sessionTargetBoss = CloneBoss(record);
                    }
                    else
                    {
                        AddTimelineLocked("BOSS_SPAWN_QUEUED", account.ID,
                            DescribeBossInstance(record) + " | current=" + DescribeBossInstance(_sessionTargetBoss));
                    }
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

            BossHuntCatalog.RememberBoss(payload.bossName);
            DateTime observedUtc = ReadObservedUtc(payload.observedAtTicks);
            bool matchesCurrent = false;
            string killer = (payload.killer ?? "").Trim();
            int killerId = payload.killerId;
            string incomingRaw = payload.rawMessage ?? "";
            string incomingEvidence = GetDeathEvidence(incomingRaw);
            BossHuntBossSnapshot updatedRecord = null;

            lock (_sync)
            {
                BossHuntBossSnapshot targetRecord = null;

                // Session target remains the strongest binding even after the session was already stopped
                // by another death signal. This lets announcement and combat evidence enrich one record.
                if (_sessionTargetBoss != null &&
                    DeathNameMatchesInstance(payload.bossName, _sessionTargetBoss.BossName) &&
                    DeathLocationMatchesInstance(payload, _sessionTargetBoss))
                {
                    targetRecord = FindRecordForInstanceLocked(_sessionTargetBoss);
                }

                if (targetRecord == null)
                    targetRecord = FindBestRecordForDeathLocked(payload.bossName, payload.mapId, payload.zone);

                if (targetRecord != null)
                {
                    bool newEvent = targetRecord.Alive || targetRecord.DiedAtUtc == DateTime.MinValue;
                    string oldKiller = targetRecord.Killer ?? "";
                    int oldKillerId = targetRecord.KillerId;
                    string oldRaw = targetRecord.RawDeath ?? "";
                    string oldEvidence = targetRecord.DeathEvidence ?? "";

                    targetRecord.Alive = false;
                    targetRecord.Presence = BossPresenceState.Dead;
                    if (targetRecord.DiedAtUtc == DateTime.MinValue || observedUtc < targetRecord.DiedAtUtc)
                        targetRecord.DiedAtUtc = observedUtc;

                    if (ShouldReplaceKiller(targetRecord.Killer, killer))
                        targetRecord.Killer = killer;
                    if (targetRecord.KillerId < 0 && killerId >= 0)
                        targetRecord.KillerId = killerId;
                    if (ShouldReplaceDeathRaw(targetRecord.RawDeath, incomingRaw))
                        targetRecord.RawDeath = incomingRaw;
                    targetRecord.DeathEvidence = MergeDeathEvidence(targetRecord.DeathEvidence, incomingEvidence);

                    targetRecord.LastSourceAccountId = account.ID;
                    AddSourceAccount(targetRecord, account.ID);

                    bool enriched =
                        !string.Equals(oldKiller, targetRecord.Killer ?? "", StringComparison.Ordinal) ||
                        oldKillerId != targetRecord.KillerId ||
                        !string.Equals(oldRaw, targetRecord.RawDeath ?? "", StringComparison.Ordinal) ||
                        !string.Equals(oldEvidence, targetRecord.DeathEvidence ?? "", StringComparison.Ordinal);

                    if (newEvent)
                    {
                        AddBossEventLocked("DEATH", targetRecord, targetRecord.DiedAtUtc, account.ID, targetRecord.Killer, targetRecord.RawDeath);
                        AddTimelineLocked("BOSS_DEATH", account.ID,
                            DescribeBossInstance(targetRecord) +
                            " | killer=" + targetRecord.Killer +
                            (targetRecord.KillerId >= 0 ? " (#" + targetRecord.KillerId + ")" : "") +
                            " | " + targetRecord.RawDeath);
                    }
                    else if (enriched)
                    {
                        AddBossEventLocked("DEATH_ENRICH", targetRecord, observedUtc, account.ID, targetRecord.Killer, incomingRaw);
                        AddTimelineLocked("BOSS_DEATH_ENRICH", account.ID,
                            DescribeBossInstance(targetRecord) +
                            " | evidence=" + targetRecord.DeathEvidence +
                            " | killer=" + targetRecord.Killer +
                            (targetRecord.KillerId >= 0 ? " (#" + targetRecord.KillerId + ")" : "") +
                            " | " + targetRecord.RawDeath);
                    }

                    updatedRecord = CloneBoss(targetRecord);

                    if (_sessionTargetBoss != null && SameBossInstance(_sessionTargetBoss, targetRecord))
                    {
                        _sessionTargetBoss = CloneBoss(targetRecord);
                        matchesCurrent = IsRunningState(_state);
                    }
                }
                else
                {
                    string key = NormalizeBossName(payload.bossName).ToLowerInvariant();
                    BossHuntBossSnapshot record = new BossHuntBossSnapshot
                    {
                        BossName = NormalizeBossName(payload.bossName),
                        Alive = false,
                        Presence = BossPresenceState.Dead,
                        DiedAtUtc = observedUtc,
                        Killer = killer,
                        KillerId = killerId,
                        RawDeath = incomingRaw,
                        DeathEvidence = incomingEvidence,
                        LastSourceAccountId = account.ID
                    };
                    AddSourceAccount(record, account.ID);
                    _bossRecords[key] = record;
                    AddBossEventLocked("DEATH", record, observedUtc, account.ID, killer, incomingRaw);
                    AddTimelineLocked("BOSS_DEATH_UNBOUND", account.ID,
                        record.BossName + " | killer=" + killer + " | " + incomingRaw);
                    updatedRecord = CloneBoss(record);
                }

                bool deathBelongsToCurrentSession =
                    IsRunningState(_state) &&
                    payload.sessionId == _sessionId &&
                    payload.assignmentGeneration == _assignmentGeneration &&
                    BossMatches(payload.bossName, _bossName);

                if (!matchesCurrent && _sessionTargetBoss == null && deathBelongsToCurrentSession && updatedRecord != null)
                {
                    _sessionTargetBoss = CloneBoss(updatedRecord);
                    matchesCurrent = true;
                    AddTimelineLocked("DEATH_BEFORE_TARGET_LOCK", account.ID,
                        "boss=" + updatedRecord.BossName + ";evidence=" + updatedRecord.DeathEvidence +
                        ";raw=" + (incomingRaw ?? ""));
                }

                if (matchesCurrent)
                {
                    _lastUnparsedDeath = "";
                    _lastUnparsedDeathUtc = DateTime.MinValue;
                }
            }

            string effectiveKiller = updatedRecord == null ? killer : (updatedRecord.Killer ?? "");
            int effectiveKillerId = updatedRecord == null ? killerId : updatedRecord.KillerId;
            string effectiveRaw = updatedRecord == null ? incomingRaw : (updatedRecord.RawDeath ?? "");
            string effectiveEvidence = updatedRecord == null ? incomingEvidence : (updatedRecord.DeathEvidence ?? "");

            BossHuntDiagnostics.Log("MANAGER", "BOSS_DEATH", 0, account.ID, payload.bossName, "GLOBAL",
                "killer=" + effectiveKiller + ";killerId=" + effectiveKillerId + ";evidence=" + effectiveEvidence + ";at=" + observedUtc.ToString("o") +
                ";map=" + payload.mapId + ";zone=" + payload.zone +
                ";currentTarget=" + matchesCurrent + ";raw=" + effectiveRaw);

            BossHuntPayload invalidate = new BossHuntPayload
            {
                bossName = updatedRecord == null ? payload.bossName : updatedRecord.BossName,
                observedAtTicks = updatedRecord != null && updatedRecord.DiedAtUtc != DateTime.MinValue
                    ? updatedRecord.DiedAtUtc.Ticks
                    : observedUtc.Ticks,
                killer = effectiveKiller,
                killerId = effectiveKillerId,
                rawMessage = effectiveRaw,
                eventName = "DEATH_SYNC"
            };
            Broadcast(GetConnectedAccounts(), CmdBossInvalidate, invalidate);

            if (matchesCurrent)
            {
                string reason = MainController.language == 0 ? "Boss mục tiêu đã chết" : "Target boss died";
                if (!string.IsNullOrEmpty(effectiveKiller))
                    reason += (MainController.language == 0 ? " - Người hạ: " : " - Killer: ") + effectiveKiller +
                              (effectiveKillerId >= 0 ? " (#" + effectiveKillerId + ")" : "");
                Stop(reason);
            }
            else
                Publish();
        }

        private void HandleBossCatalog(Account account, BossHuntPayload payload)
        {
            if (payload == null || payload.bossNames == null || payload.bossNames.Length == 0)
                return;

            BossHuntCatalog.RememberBosses(payload.bossNames);
            BossHuntDiagnostics.Log("MANAGER", "BOSS_CATALOG_SYNC", 0, account == null ? -1 : account.ID, "", "GLOBAL",
                "count=" + payload.bossNames.Length);
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
            bool stopForScanExhausted = false;
            string scanExhaustedReason = "";
            string staleBossName = "";
            List<PendingZoneAssignment> centralAssignments = null;
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

                if (eventName == "ZONE_CAPACITY")
                {
                    worker.ZoneCapacityReported = true;
                    worker.ReportedMaxZone = payload.maxZone;
                    worker.TotalZones = payload.totalZones;
                    worker.Status = MainController.language == 0 ? "Đã báo số khu - chờ chia" : "Zone capacity reported";
                    AddTimelineLocked("ZONE_CAPACITY", account.ID, "maxZone=" + payload.maxZone + ";count=" + payload.totalZones);
                    centralAssignments = BuildCentralZoneAssignmentsIfReadyLocked();
                    changed = true;
                }
                else if (eventName == "ZONE_PLAN")
                {
                    if (!string.IsNullOrEmpty(payload.assignedZones))
                        worker.AssignedZones = payload.assignedZones;
                    if (payload.totalZones > 0)
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
                    worker.FailureCount++;
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

                if (eventName == "ANNOUNCEMENT_RAW")
                {
                    string raw = payload.detail ?? "";
                    if (!string.IsNullOrEmpty(raw))
                        AddTimelineLocked("ANNOUNCEMENT_RAW", account.ID, raw);
                    changed = true;
                }
                else if (eventName == "DEATH_UNPARSED")
                {
                    _lastUnparsedDeath = payload.detail ?? "";
                    _lastUnparsedDeathUtc = DateTime.UtcNow;
                    worker.Status = MainController.language == 0
                        ? "Cảnh báo: có thông báo death chưa parse được"
                        : "Warning: unparsed death announcement";
                    AddTimelineLocked("DEATH_UNPARSED", account.ID, _lastUnparsedDeath);
                    BossHuntDiagnostics.Log("MANAGER", "DEATH_UNPARSED", _sessionId, account.ID, _bossName, _state.ToString(), _lastUnparsedDeath);
                    changed = true;
                }

                if (_state == BossHuntState.Scanning &&
                    (eventName == "ZONE_CLEAR" || eventName == "ZONE_FAILED") &&
                    AllHealthyWorkersReachedScanCycleLocked(3))
                {
                    int coveredZones = GetUniqueCoverageCountLocked();
                    int totalZones = GetCoverageTotalZonesLocked();
                    bool coverageComplete = totalZones > 0 && coveredZones >= totalZones;

                    if (coverageComplete)
                    {
                        BossHuntBossSnapshot latest = _sessionTargetBoss ?? FindLatestBossLocked(_bossName, true);
                        if (latest != null &&
                            latest.SpawnedAtUtc != DateTime.MinValue &&
                            DateTime.UtcNow.Subtract(latest.SpawnedAtUtc).TotalSeconds >= 45.0)
                        {
                            latest.Presence = BossPresenceState.Stale;
                            _sessionTargetBoss = CloneBoss(latest);
                            staleBossName = latest.BossName;
                            AddTimelineLocked("SCAN_EXHAUSTED", account.ID,
                                "boss=" + latest.BossName + ";cycle>=3;coverage=" +
                                coveredZones + "/" + totalZones);
                            stopForScanExhausted = true;
                            scanExhaustedReason = MainController.language == 0
                                ? "Đã phủ đủ toàn bộ khu nhưng không còn tìm thấy boss; chưa xác nhận được death/killer"
                                : "All zones covered but boss was not found; death/killer not confirmed";
                        }
                    }
                    else if (eventName == "ZONE_CLEAR")
                    {
                        worker.Status = (MainController.language == 0 ? "Đang quét tiếp - chưa phủ đủ khu" : "Scanning - coverage incomplete") +
                                        " | " + coveredZones + "/" + (totalZones > 0 ? totalZones.ToString() : "?");
                    }
                }
            }

            if (stopForScanExhausted)
            {
                if (!string.IsNullOrEmpty(staleBossName))
                {
                    BossHuntPayload invalidate = new BossHuntPayload
                    {
                        bossName = staleBossName,
                        eventName = "SCAN_EXHAUSTED"
                    };
                    Broadcast(GetConnectedAccounts(), CmdBossInvalidate, invalidate);
                }
                Stop(scanExhaustedReason);
                return;
            }

            if (centralAssignments != null)
            {
                for (int i = 0; i < centralAssignments.Count; i++)
                    Send(centralAssignments[i].Account, CmdZoneAssignment, centralAssignments[i].Payload);
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
                    worker.FailureCount++;
                    if (!string.IsNullOrEmpty(payload.detail) && payload.detail.IndexOf("TIMEOUT", StringComparison.OrdinalIgnoreCase) >= 0)
                        worker.TimeoutCount++;
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
                    worker.FailureCount++;
                    worker.TimeoutCount++;
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
                    targetBossName = knownBoss == null ? "" : knownBoss.BossName,
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
            _zonePlanIssuedGeneration = 0;
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
                worker.ZoneCapacityReported = false;
                worker.ReportedMaxZone = -1;
                if (worker.ScanStartedAtUtc == DateTime.MinValue)
                    worker.ScanStartedAtUtc = now;
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

        private static bool ContainsAccountId(List<Account> accounts, int accountId)
        {
            if (accounts == null)
                return false;
            for (int i = 0; i < accounts.Count; i++)
            {
                if (accounts[i] != null && accounts[i].ID == accountId)
                    return true;
            }
            return false;
        }

        private void AddSessionAccountLocked(Account account)
        {
            if (account == null || ContainsAccountId(_sessionAccounts, account.ID))
                return;
            _sessionAccounts.Add(account);
            _sessionAccounts.Sort(delegate(Account a, Account b) { return a.ID.CompareTo(b.ID); });
        }

        private BossHuntWorkerSnapshot EnsureWorkerLocked(Account account)
        {
            BossHuntWorkerSnapshot worker;
            if (!_workers.TryGetValue(account.ID, out worker))
            {
                worker = new BossHuntWorkerSnapshot
                {
                    AccountId = account.ID,
                    Username = account.Username ?? "",
                    ScanStartedAtUtc = DateTime.UtcNow
                };
                _workers[account.ID] = worker;
            }
            return worker;
        }

        private void NormalizeSessionWorkerIndexesLocked()
        {
            _sessionAccounts.Sort(delegate(Account a, Account b) { return a.ID.CompareTo(b.ID); });
            for (int i = 0; i < _sessionAccounts.Count; i++)
            {
                BossHuntWorkerSnapshot worker = EnsureWorkerLocked(_sessionAccounts[i]);
                worker.WorkerIndex = i;
                worker.WorkerCount = _sessionAccounts.Count;
            }
        }

        private double GetWorstHeartbeatAgeSecondsLocked()
        {
            double worst = -1.0;
            DateTime now = DateTime.UtcNow;
            foreach (BossHuntWorkerSnapshot worker in _workers.Values)
            {
                if (worker.LastHeartbeatUtc == DateTime.MinValue)
                    continue;
                double age = now.Subtract(worker.LastHeartbeatUtc).TotalSeconds;
                if (age < 0.0)
                    age = 0.0;
                if (age > worst)
                    worst = age;
            }
            return worst;
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
            worker.ZoneClearCount++;
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
                KillerId = boss == null ? -1 : boss.KillerId,
                SourceAccountId = sourceAccountId,
                RawMessage = raw ?? "",
                DeathEvidence = boss == null ? "" : boss.DeathEvidence
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

        private BossHuntBossSnapshot FindRecordForInstanceLocked(BossHuntBossSnapshot target)
        {
            if (target == null)
                return null;

            foreach (BossHuntBossSnapshot record in _bossRecords.Values)
            {
                if (SameBossInstance(target, record))
                    return record;
            }
            return null;
        }

        private BossHuntBossSnapshot FindBestRecordForDeathLocked(string deathBossName, int mapId, int zone)
        {
            BossHuntBossSnapshot bestAlive = null;
            BossHuntBossSnapshot bestDead = null;

            foreach (BossHuntBossSnapshot record in _bossRecords.Values)
            {
                if (!DeathNameMatchesInstance(deathBossName, record.BossName))
                    continue;
                if (mapId >= 0 && record.MapId >= 0 && mapId != record.MapId)
                    continue;
                if (zone >= 0 && record.Zone >= 0 && zone != record.Zone)
                    continue;

                if (record.Alive)
                {
                    if (bestAlive == null || record.SpawnedAtUtc > bestAlive.SpawnedAtUtc)
                        bestAlive = record;
                    continue;
                }

                DateTime candidate = record.DiedAtUtc != DateTime.MinValue ? record.DiedAtUtc : record.SpawnedAtUtc;
                DateTime bestTime = bestDead == null
                    ? DateTime.MinValue
                    : (bestDead.DiedAtUtc != DateTime.MinValue ? bestDead.DiedAtUtc : bestDead.SpawnedAtUtc);
                if (bestDead == null || candidate > bestTime)
                    bestDead = record;
            }

            return bestAlive ?? bestDead;
        }

        private static bool ShouldReplaceKiller(string current, string incoming)
        {
            current = (current ?? "").Trim();
            incoming = (incoming ?? "").Trim();
            if (incoming.Length == 0)
                return false;
            if (current.Length == 0)
                return true;

            bool currentIsIdFallback = current.StartsWith("#", StringComparison.Ordinal);
            bool incomingIsIdFallback = incoming.StartsWith("#", StringComparison.Ordinal);
            return currentIsIdFallback && !incomingIsIdFallback;
        }

        private static string GetDeathEvidence(string raw)
        {
            raw = (raw ?? "").Trim();
            if (raw.StartsWith("combat:", StringComparison.OrdinalIgnoreCase))
                return "COMBAT_-60";
            if (raw.StartsWith("fallback:", StringComparison.OrdinalIgnoreCase))
                return "FALLBACK";
            if (raw.Length > 0)
                return "ANNOUNCEMENT";
            return "UNKNOWN";
        }

        private static string MergeDeathEvidence(string current, string incoming)
        {
            current = (current ?? "").Trim();
            incoming = (incoming ?? "").Trim();
            if (incoming.Length == 0 || incoming == "UNKNOWN")
                return current.Length == 0 ? incoming : current;
            if (current.Length == 0 || current == "UNKNOWN")
                return incoming;

            string[] tokens = current.Split(',');
            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i].Trim().Equals(incoming, StringComparison.OrdinalIgnoreCase))
                    return current;
            }
            return current + "," + incoming;
        }

        private static bool ShouldReplaceDeathRaw(string current, string incoming)
        {
            current = current ?? "";
            incoming = incoming ?? "";
            if (incoming.Length == 0)
                return false;
            if (current.Length == 0)
                return true;

            bool currentSynthetic =
                current.StartsWith("combat:", StringComparison.OrdinalIgnoreCase) ||
                current.StartsWith("fallback:", StringComparison.OrdinalIgnoreCase);
            bool incomingSynthetic =
                incoming.StartsWith("combat:", StringComparison.OrdinalIgnoreCase) ||
                incoming.StartsWith("fallback:", StringComparison.OrdinalIgnoreCase);

            return currentSynthetic && !incomingSynthetic;
        }

        private static bool SameBossInstance(BossHuntBossSnapshot a, BossHuntBossSnapshot b)
        {
            if (a == null || b == null)
                return false;
            if (!NormalizeBossName(a.BossName).Equals(NormalizeBossName(b.BossName), StringComparison.OrdinalIgnoreCase))
                return false;
            if (a.MapId >= 0 && b.MapId >= 0 && a.MapId != b.MapId)
                return false;
            if (a.Zone >= 0 && b.Zone >= 0 && a.Zone != b.Zone)
                return false;

            if (a.SpawnedAtUtc != DateTime.MinValue && b.SpawnedAtUtc != DateTime.MinValue)
                return Math.Abs(a.SpawnedAtUtc.Subtract(b.SpawnedAtUtc).TotalSeconds) <= 10.0;

            return true;
        }

        private static bool DeathNameMatchesInstance(string deathName, string instanceName)
        {
            deathName = NormalizeBossName(deathName);
            instanceName = NormalizeBossName(instanceName);
            if (deathName.Length == 0 || instanceName.Length == 0)
                return false;
            if (deathName.Equals(instanceName, StringComparison.OrdinalIgnoreCase))
                return true;

            if (StartsWithBossBoundary(instanceName, deathName))
                return true;
            if (StartsWithBossBoundary(deathName, instanceName))
                return true;

            return false;
        }

        private static bool StartsWithBossBoundary(string value, string prefix)
        {
            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || value.Length <= prefix.Length)
                return false;
            char next = value[prefix.Length];
            return char.IsWhiteSpace(next) || next == '-' || next == '(' || next == '[';
        }

        private static bool DeathLocationMatchesInstance(BossHuntPayload payload, BossHuntBossSnapshot instance)
        {
            if (payload == null || instance == null)
                return false;
            if (payload.mapId >= 0 && instance.MapId >= 0 && payload.mapId != instance.MapId)
                return false;
            if (payload.zone >= 0 && instance.Zone >= 0 && payload.zone != instance.Zone)
                return false;
            return true;
        }

        private static string DescribeBossInstance(BossHuntBossSnapshot boss)
        {
            if (boss == null)
                return "-";
            return boss.BossName + " @ " + (boss.MapName ?? "") + " K" + boss.Zone +
                   (boss.SpawnedAtUtc == DateTime.MinValue ? "" : " | " + boss.SpawnedAtUtc.ToLocalTime().ToString("HH:mm:ss"));
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
                    if (!boss.Alive || boss.SpawnedAtUtc == DateTime.MinValue || boss.Presence == BossPresenceState.Stale)
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
                KillerId = source.KillerId,
                RawSpawn = source.RawSpawn,
                RawDeath = source.RawDeath,
                DeathEvidence = source.DeathEvidence,
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
                KillerId = source.KillerId,
                SourceAccountId = source.SourceAccountId,
                RawMessage = source.RawMessage,
                DeathEvidence = source.DeathEvidence
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
                ZoneCapacityReported = source.ZoneCapacityReported,
                ReportedMaxZone = source.ReportedMaxZone,
                ScanStartedAtUtc = source.ScanStartedAtUtc,
                ZoneClearCount = source.ZoneClearCount,
                FailureCount = source.FailureCount,
                TimeoutCount = source.TimeoutCount,
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

        private List<PendingZoneAssignment> BuildCentralZoneAssignmentsIfReadyLocked()
        {
            if (_state != BossHuntState.Scanning || _zonePlanIssuedGeneration == _assignmentGeneration)
                return null;

            List<Account> healthy = new List<Account>();
            int canonicalZoneCount = int.MaxValue;
            for (int i = 0; i < _sessionAccounts.Count; i++)
            {
                Account account = _sessionAccounts[i];
                BossHuntWorkerSnapshot worker;
                if (!IsConnected(account) || !_workers.TryGetValue(account.ID, out worker) || worker.Failed || worker.Unresponsive)
                    continue;
                if (!worker.ZoneCapacityReported)
                    return null;

                int reportedCount = worker.TotalZones;
                if (reportedCount <= 0 && worker.ReportedMaxZone >= 0)
                    reportedCount = worker.ReportedMaxZone + 1;
                if (reportedCount <= 0)
                    return null;

                healthy.Add(account);
                if (reportedCount < canonicalZoneCount)
                    canonicalZoneCount = reportedCount;
            }

            if (healthy.Count == 0 || canonicalZoneCount == int.MaxValue || canonicalZoneCount <= 0)
                return null;

            int effectiveStart = _startZone;
            if (effectiveStart < 0 || effectiveStart >= canonicalZoneCount)
                effectiveStart = 0;

            List<int> orderedZones = BuildOrderedZoneList(canonicalZoneCount, effectiveStart);
            List<PendingZoneAssignment> result = new List<PendingZoneAssignment>();

            for (int i = 0; i < healthy.Count; i++)
            {
                Account account = healthy[i];
                BossHuntWorkerSnapshot worker = _workers[account.ID];

                int baseSize = orderedZones.Count / healthy.Count;
                int remainder = orderedZones.Count % healthy.Count;
                int assignedCount = baseSize + (i < remainder ? 1 : 0);
                int offset = i * baseSize + Math.Min(i, remainder);

                string assignedZones = BuildAssignedZones(orderedZones, offset, assignedCount);

                worker.WorkerIndex = i;
                worker.WorkerCount = healthy.Count;
                worker.TotalZones = canonicalZoneCount;
                worker.AssignedZones = assignedZones;
                worker.Status = string.IsNullOrEmpty(worker.AssignedZones)
                    ? (MainController.language == 0 ? "Dự phòng - không có khu" : "Standby - no assigned zone")
                    : (MainController.language == 0 ? "Đã nhận phân khu" : "Zone plan received");

                BossHuntPayload assignment = new BossHuntPayload
                {
                    sessionId = _sessionId,
                    assignmentGeneration = _assignmentGeneration,
                    bossName = _bossName,
                    startZone = effectiveStart,
                    workerIndex = i,
                    workerCount = healthy.Count,
                    accountId = account.ID,
                    totalZones = canonicalZoneCount,
                    maxZone = canonicalZoneCount - 1,
                    assignedZones = assignedZones
                };

                result.Add(new PendingZoneAssignment
                {
                    Account = account,
                    Payload = assignment
                });

                AddTimelineLocked("ZONE_PLAN", account.ID,
                    "zones=" + canonicalZoneCount +
                    ";start=K" + effectiveStart +
                    ";share=" + assignedCount +
                    ";assigned=" + assignedZones);
            }

            _zonePlanIssuedGeneration = _assignmentGeneration;
            BossHuntDiagnostics.Log("MANAGER", "CENTRAL_ZONE_PLAN", _sessionId, -1, _bossName, _state.ToString(),
                "generation=" + _assignmentGeneration +
                ";workers=" + healthy.Count +
                ";zones=" + canonicalZoneCount +
                ";start=K" + effectiveStart +
                ";mode=BALANCED_CONTIGUOUS");

            return result;
        }

        private static List<int> BuildOrderedZoneList(int zoneCount, int startZone)
        {
            List<int> zones = new List<int>();
            if (zoneCount <= 0)
                return zones;

            if (startZone < 0 || startZone >= zoneCount)
                startZone = 0;

            for (int zone = startZone; zone < zoneCount; zone++)
                zones.Add(zone);
            for (int zone = 0; zone < startZone; zone++)
                zones.Add(zone);

            return zones;
        }

        private static string BuildAssignedZones(List<int> zones, int offset, int count)
        {
            if (zones == null || count <= 0 || offset < 0 || offset >= zones.Count)
                return "";

            StringBuilder builder = new StringBuilder();
            int end = Math.Min(zones.Count, offset + count);
            for (int i = offset; i < end; i++)
            {
                if (builder.Length > 0)
                    builder.Append(",");
                builder.Append("K");
                builder.Append(zones[i]);
            }
            return builder.ToString();
        }

        private bool AllHealthyWorkersReachedScanCycleLocked(int minimumCycle)
        {
            bool any = false;
            for (int i = 0; i < _sessionAccounts.Count; i++)
            {
                Account account = _sessionAccounts[i];
                BossHuntWorkerSnapshot worker;
                if (!IsConnected(account) || !_workers.TryGetValue(account.ID, out worker) ||
                    worker.Failed || worker.Unresponsive || worker.AssignmentGeneration != _assignmentGeneration)
                    continue;

                any = true;
                if (worker.ScanCycle < minimumCycle)
                    return false;
            }
            return any;
        }

        private static BossPresenceState GetPresence(BossHuntBossSnapshot boss)
        {
            if (boss == null)
                return BossPresenceState.Unknown;
            if (boss.Presence == BossPresenceState.Stale)
                return BossPresenceState.Stale;
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
