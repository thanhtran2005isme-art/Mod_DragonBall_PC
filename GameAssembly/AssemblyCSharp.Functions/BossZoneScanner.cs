using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace AssemblyCSharp.Functions
{
    public sealed class BossZoneScanner
    {
        private enum ScannerState
        {
            Idle,
            WaitingLocation,
            RoutingToScanMap,
            Scanning,
            Standby,
            Found,
            Rallying,
            Fighting
        }

        private sealed class BossHuntPayload
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

        private sealed class PendingCommand
        {
            public int cmd;
            public BossHuntPayload payload;
        }

        private sealed class PendingCombatDeath
        {
            public int attackerId;
            public string attackerName;
            public int targetId;
            public string targetName;
            public int mapId;
            public string mapName;
            public int zone;
            public long observedAtTicks;
        }

        private const int CmdStartScan = 100;
        private const int CmdStop = 101;
        private const int CmdRally = 102;
        private const int CmdBossSync = 103;
        private const int CmdBossInvalidate = 104;
        private const int CmdZoneAssignment = 105;

        private const int CmdZone = 110;
        private const int CmdFound = 111;
        private const int CmdDead = 112;
        private const int CmdReady = 113;
        private const int CmdFailed = 114;
        private const int CmdBossSpawn = 115;
        private const int CmdBossDeath = 116;
        private const int CmdTelemetry = 117;
        private const int CmdHeartbeat = 118;
        private const int CmdBossCatalog = 119;

        private const long BossCatalogSyncIntervalMs = 15000L;

        private const long HeartbeatIntervalMs = 2000L;
        private const long ScanRouteTimeoutMs = 90000L;
        private const long RouteRetryDelayMs = 1200L;
        private const long RouteStallTimeoutMs = 30000L;
        private const long RallyOverallTimeoutMs = 90000L;
        private const long ZoneListTimeoutMs = 10000L;
        private const int RallyZoneMaxAttempts = 3;
        private const long RallyTargetLoadTimeoutMs = 8000L;
        private const long FightingTargetLostGraceMs = 3000L;
        private const long EntityMinDwellMs = 2000L;
        private const long EntityStableWindowMs = 800L;
        private const long EntityMaxDwellMs = 5000L;
        private const long AnnouncedZoneMaxDwellMs = 7000L;

        private static readonly BossZoneScanner _instance = new BossZoneScanner();

        private readonly object _commandLock = new object();
        private readonly Queue<PendingCommand> _pendingCommands = new Queue<PendingCommand>();
        private readonly object _announcementLock = new object();
        private readonly Queue<string> _pendingAnnouncements = new Queue<string>();
        private readonly object _combatDeathLock = new object();
        private readonly Queue<PendingCombatDeath> _pendingCombatDeaths = new Queue<PendingCombatDeath>();
        private readonly List<int> _assignedZones = new List<int>();

        private bool _active;
        private int _sessionId;
        private int _assignmentGeneration;
        private string _bossName = "";
        private bool _sessionTargetLocked;
        private string _sessionTargetBossName = "";
        private int _sessionTargetMapId = -1;
        private int _sessionTargetZone = -1;
        private long _sessionTargetObservedAtTicks;
        private int _startZone;
        private int _workerIndex;
        private int _workerCount = 1;
        private int _desiredZone;
        private int _maxZone = 14;
        private int _arrivedZone = -1;
        private int _zoneAttempts;
        private int _scanMapId = -1;
        private string _scanMapName = "";
        private int _announcedZone = -1;
        private bool _usingAnnouncedZone;
        private int _targetMapId = -1;
        private string _targetMapName = "";
        private int _targetZone = -1;
        private bool _previousAutoBoss;
        private bool _readyReported;
        private int _rallyZoneAttempts;
        private long _rallyStartedAt;
        private long _rallyZoneArrivedAt;
        private long _targetMissingSince;
        private long _scanRouteStartedAt;
        private long _lastScanRouteCommandAt;
        private int _scanRouteLastMapId = -1;
        private long _scanRouteLastProgressAt;
        private int _rallyRouteLastMapId = -1;
        private long _rallyRouteLastProgressAt;
        private long _startedAt;
        private long _lastZoneCommandAt;
        private long _arrivedAt;
        private long _lastRouteCommandAt;
        private long _lastFocusAt;
        private long _lastZoneListRequestAt;
        private int[] _zoneListBaseline;
        private int _zoneListMapId = -1;
        private long _zoneListWaitStartedAt;
        private bool _zoneListFresh;
        private bool _zonePlanReady;
        private bool _zoneCapacityReported;
        private int _assignedZonePosition;
        private int _lastEntityCount = -1;
        private int _lastBossCount = -1;
        private long _lastEntityChangeAt;
        private long _lastHeartbeatAt;
        private long _lastCatalogSyncAt;
        private int _scanCycle;
        private ScannerState _state = ScannerState.Idle;

        public static BossZoneScanner Instance
        {
            get { return _instance; }
        }

        private BossZoneScanner()
        {
        }

        public void HandleManagerMessage(int cmd, byte[] data)
        {
            if (cmd != CmdStartScan && cmd != CmdStop && cmd != CmdRally && cmd != CmdBossSync && cmd != CmdBossInvalidate && cmd != CmdZoneAssignment)
                return;

            BossHuntPayload payload = Deserialize(data);
            if (payload == null)
                return;

            lock (_commandLock)
            {
                if (_pendingCommands.Count >= 32)
                    _pendingCommands.Dequeue();
                _pendingCommands.Enqueue(new PendingCommand
                {
                    cmd = cmd,
                    payload = payload
                });
            }
        }

        public void Update()
        {
            try
            {
                DrainManagerCommands();
                DrainCombatDeaths();
                DrainAnnouncements();

                long now = GClass203.smethod_18();
                SendBossCatalogSnapshotIfDue(now);
                if (!_active)
                    return;

                SendHeartbeatIfDue(now);
                if (_state == ScannerState.WaitingLocation)
                    UpdateWaitingLocation(now);
                else if (_state == ScannerState.RoutingToScanMap)
                    UpdateScanRoute(now);
                else if (_state == ScannerState.Scanning)
                    UpdateScanning(now);
                else if (_state == ScannerState.Found || _state == ScannerState.Fighting)
                    UpdatePinnedFight(now);
                else if (_state == ScannerState.Rallying)
                    UpdateRally(now);
            }
            catch (Exception ex)
            {
                GClass149.smethod_0("Data/Errors/BossZoneScanner.txt", ex.ToString());
            }
        }

        private void DrainManagerCommands()
        {
            while (true)
            {
                PendingCommand pending;
                lock (_commandLock)
                {
                    if (_pendingCommands.Count == 0)
                        return;
                    pending = _pendingCommands.Dequeue();
                }

                if (pending == null || pending.payload == null)
                    continue;

                if (pending.cmd == CmdBossSync)
                {
                    ApplyBossSync(pending.payload);
                    continue;
                }

                if (pending.cmd == CmdBossInvalidate)
                {
                    GClass156.InvalidateBossLocation(pending.payload.bossName);
                    continue;
                }

                if (pending.cmd == CmdZoneAssignment)
                {
                    if (_active && pending.payload.sessionId == _sessionId &&
                        pending.payload.assignmentGeneration == _assignmentGeneration)
                        ApplyZoneAssignment(pending.payload);
                    continue;
                }

                if (pending.cmd == CmdStartScan)
                {
                    if (_active && pending.payload.sessionId == _sessionId &&
                        pending.payload.assignmentGeneration < _assignmentGeneration)
                        continue;
                    StartScan(pending.payload);
                    continue;
                }

                if (!_active || pending.payload.sessionId != _sessionId)
                    continue;

                if (pending.cmd == CmdStop)
                {
                    if (pending.payload.assignmentGeneration >= _assignmentGeneration)
                    {
                        _assignmentGeneration = pending.payload.assignmentGeneration;
                        StopInternal();
                    }
                    continue;
                }

                if (pending.cmd == CmdRally && pending.payload.assignmentGeneration == _assignmentGeneration)
                    StartRally(pending.payload);
            }
        }

        private void StartScan(BossHuntPayload payload)
        {
            bool newSession = !_active || payload.sessionId != _sessionId;
            if (newSession)
                _previousAutoBoss = GClass158.smethod_0().bool_0;

            GClass158.smethod_0().bool_0 = false;

            _active = true;
            _sessionId = payload.sessionId;
            _assignmentGeneration = Math.Max(1, payload.assignmentGeneration);
            _bossName = (payload.bossName ?? "").Trim();
            _sessionTargetLocked = false;
            _sessionTargetBossName = "";
            _sessionTargetMapId = -1;
            _sessionTargetZone = -1;
            _sessionTargetObservedAtTicks = 0L;
            _startZone = Math.Max(0, payload.startZone);
            _workerIndex = Math.Max(0, payload.workerIndex);
            _workerCount = Math.Max(1, payload.workerCount);
            _maxZone = -1;
            _desiredZone = -1;
            _arrivedZone = -1;
            _zoneAttempts = 0;
            _scanMapId = -1;
            _scanMapName = "";
            _announcedZone = -1;
            _usingAnnouncedZone = false;
            _targetMapId = -1;
            _targetMapName = "";
            _targetZone = -1;
            _readyReported = false;
            _state = ScannerState.WaitingLocation;
            _startedAt = GClass203.smethod_18();
            _scanRouteStartedAt = 0L;
            _lastScanRouteCommandAt = 0L;
            _scanRouteLastMapId = GClass20.int_37;
            _scanRouteLastProgressAt = _startedAt;
            _rallyRouteLastMapId = GClass20.int_37;
            _rallyRouteLastProgressAt = _startedAt;
            _lastZoneCommandAt = 0L;
            _arrivedAt = 0L;
            _lastRouteCommandAt = 0L;
            _lastFocusAt = 0L;
            _lastZoneListRequestAt = 0L;
            _zoneListBaseline = null;
            _zoneListMapId = -1;
            _zoneListWaitStartedAt = 0L;
            _zoneListFresh = false;
            _zonePlanReady = false;
            _zoneCapacityReported = false;
            _assignedZones.Clear();
            _assignedZonePosition = 0;
            _lastEntityCount = -1;
            _lastBossCount = -1;
            _lastEntityChangeAt = 0L;
            _lastHeartbeatAt = 0L;
            _scanCycle = 1;
            _rallyStartedAt = 0L;
            _rallyZoneAttempts = 0;
            _rallyZoneArrivedAt = 0L;
            _targetMissingSince = 0L;
            ClearAnnouncements();
            Trace("START_SCAN", "generation=" + _assignmentGeneration + ";worker=" + _workerIndex + "/" + _workerCount + ";startZone=" + _startZone);

            string requestedTarget = (payload.targetBossName ?? "").Trim();
            if (requestedTarget.Length > 0)
            {
                LockSessionTarget(
                    requestedTarget,
                    payload.mapId,
                    payload.zone,
                    payload.observedAtTicks);
            }

            GClass78 currentTarget = FindTargetBoss();
            if (currentTarget != null)
            {
                string concreteTarget = ResolveBossEntityName(currentTarget);
                if (string.IsNullOrEmpty(concreteTarget))
                    concreteTarget = requestedTarget.Length > 0 ? requestedTarget : _bossName;
                LockSessionTarget(
                    concreteTarget,
                    GClass20.int_37,
                    GClass20.int_39,
                    payload.observedAtTicks);
                SetScanLocation(GClass20.int_37, GClass20.string_1, GClass20.int_39, _startedAt);
                return;
            }

            if (payload.mapId >= 0)
            {
                string targetName = requestedTarget.Length > 0 ? requestedTarget : _bossName;
                LockSessionTarget(
                    targetName,
                    payload.mapId,
                    payload.zone,
                    payload.observedAtTicks);
                GClass156.ApplyBossLocationSync(
                    targetName,
                    payload.mapName,
                    payload.mapId,
                    payload.zone,
                    payload.observedAtTicks);
                SetScanLocation(payload.mapId, payload.mapName, payload.zone, _startedAt);
                return;
            }

            SendEvent(CmdZone, "WAITING_LOCATION");
        }

        private void StartRally(BossHuntPayload payload)
        {
            _targetMapId = payload.mapId;
            _targetMapName = payload.mapName ?? "";
            _targetZone = payload.zone;
            _bossName = (payload.bossName ?? _bossName).Trim();

            string concreteTarget = string.IsNullOrEmpty(payload.targetBossName)
                ? _bossName
                : payload.targetBossName.Trim();
            LockSessionTarget(
                concreteTarget,
                payload.mapId,
                payload.zone,
                payload.observedAtTicks);
            if (payload.mapId >= 0)
            {
                GClass156.ApplyBossLocationSync(
                    concreteTarget,
                    payload.mapName,
                    payload.mapId,
                    payload.zone,
                    payload.observedAtTicks);
            }

            _state = ScannerState.Rallying;
            _readyReported = false;
            _rallyStartedAt = GClass203.smethod_18();
            _rallyZoneAttempts = 0;
            _rallyZoneArrivedAt = 0L;
            _targetMissingSince = 0L;
            _lastRouteCommandAt = 0L;
            _rallyRouteLastMapId = GClass20.int_37;
            _rallyRouteLastProgressAt = _rallyStartedAt;
            _lastZoneCommandAt = 0L;
            _lastFocusAt = 0L;
            Trace("START_RALLY", "targetMap=" + _targetMapId + ";targetZone=" + _targetZone);
        }

        private void UpdateWaitingLocation(long now)
        {
            GClass78 currentTarget = FindTargetBoss();
            if (currentTarget != null)
                SetScanLocation(GClass20.int_37, GClass20.string_1, GClass20.int_39, now);
        }

        private void UpdateScanRoute(long now)
        {
            if (_scanMapId < 0)
            {
                _state = ScannerState.WaitingLocation;
                SendEvent(CmdZone, "WAITING_LOCATION");
                return;
            }

            if (GClass20.int_37 == _scanMapId)
            {
                Trace("MAP_ARRIVED", "targetMap=" + _scanMapId);
                BeginScanningOnCurrentMap(now);
                return;
            }

            if (TrackRouteProgress(now, ref _scanRouteLastMapId, ref _scanRouteLastProgressAt))
            {
                Trace("XMAP_PROGRESS", "targetMap=" + _scanMapId);
                SendTelemetry("SCAN_ROUTE", "progress map=" + GClass20.int_37 + " -> " + _scanMapId);
            }

            if (_scanRouteStartedAt > 0L && now - _scanRouteStartedAt >= ScanRouteTimeoutMs)
            {
                ReportFailed("SCAN_ROUTE_TIMEOUT " + _scanMapName);
                return;
            }

            bool xmapRunning = GClass148.smethod_0().bool_0;
            if (xmapRunning && now - _scanRouteLastProgressAt < RouteStallTimeoutMs)
                return;

            if (xmapRunning)
            {
                Trace("XMAP_STALL_RESTART", "targetMap=" + _scanMapId);
                Class21.smethod_0().method_9();
                _lastScanRouteCommandAt = 0L;
                _scanRouteLastProgressAt = now;
            }

            if (now - _lastScanRouteCommandAt < RouteRetryDelayMs)
                return;

            Trace("XMAP_START", "targetMap=" + _scanMapId + ";targetName=" + _scanMapName);
            SendTelemetry("SCAN_ROUTE", "start -> " + _scanMapName + " (#" + _scanMapId + ")");
            Class21.smethod_0().method_8(_scanMapId);
            _lastScanRouteCommandAt = now;
            SendEvent(CmdZone, "ROUTING_MAP|" + _scanMapName);
        }

        private static bool TrackRouteProgress(long now, ref int lastMapId, ref long lastProgressAt)
        {
            int currentMapId = GClass20.int_37;
            if (lastMapId != currentMapId)
            {
                lastMapId = currentMapId;
                lastProgressAt = now;
                return true;
            }
            if (lastProgressAt <= 0L)
                lastProgressAt = now;
            return false;
        }

        private void LockSessionTarget(string bossName, int mapId, int zone, long observedAtTicks)
        {
            _sessionTargetLocked = true;
            _sessionTargetBossName = (bossName ?? "").Trim();
            _sessionTargetMapId = mapId;
            _sessionTargetZone = zone;
            _sessionTargetObservedAtTicks = observedAtTicks;
            Trace("SESSION_TARGET_LOCK",
                "boss=" + _sessionTargetBossName + ";map=" + mapId + ";zone=" + zone + ";ticks=" + observedAtTicks);
        }

        private bool SyncMatchesLockedTarget(BossHuntPayload payload)
        {
            if (!_sessionTargetLocked || payload == null)
                return true;

            if (_sessionTargetMapId >= 0 && payload.mapId != _sessionTargetMapId)
                return false;
            if (_sessionTargetZone >= 0 && payload.zone >= 0 && payload.zone != _sessionTargetZone)
                return false;

            string lockedName = (_sessionTargetBossName ?? "").Trim();
            string incomingName = (payload.bossName ?? "").Trim();
            if (lockedName.Length > 0 && incomingName.Length > 0)
            {
                bool sameConcreteName = lockedName.Equals(incomingName, StringComparison.OrdinalIgnoreCase);
                bool genericLock = lockedName.Equals(_bossName, StringComparison.OrdinalIgnoreCase);
                if (!sameConcreteName && !genericLock)
                    return false;
            }

            if (_sessionTargetObservedAtTicks > 0L && payload.observedAtTicks > 0L &&
                Math.Abs(_sessionTargetObservedAtTicks - payload.observedAtTicks) > TimeSpan.TicksPerSecond * 10L)
                return false;

            return true;
        }

        private void SetScanLocation(int mapId, string mapName, int zone, long now)
        {
            if (mapId < 0)
                return;

            _scanMapId = mapId;
            _scanMapName = mapName ?? "";
            _announcedZone = zone;
            _scanRouteStartedAt = now;
            _lastScanRouteCommandAt = 0L;
            _scanRouteLastMapId = GClass20.int_37;
            _scanRouteLastProgressAt = now;

            if (GClass20.int_37 == _scanMapId)
                BeginScanningOnCurrentMap(now);
            else
            {
                _state = ScannerState.RoutingToScanMap;
                UpdateScanRoute(now);
            }
        }

        private void BeginScanningOnCurrentMap(long now)
        {
            _state = ScannerState.Scanning;
            _maxZone = -1;
            _usingAnnouncedZone = false;
            _desiredZone = -1;
            _arrivedZone = -1;
            _zoneAttempts = 0;
            _startedAt = now;
            _lastZoneCommandAt = 0L;
            _arrivedAt = 0L;
            _lastZoneListRequestAt = 0L;
            _zoneListMapId = GClass20.int_37;
            _zoneListBaseline = GetCurrentZoneListReference();
            _zoneListWaitStartedAt = now;
            _zoneListFresh = false;
            _zonePlanReady = false;
            _zoneCapacityReported = false;
            _assignedZones.Clear();
            _assignedZonePosition = 0;
            _lastEntityCount = -1;
            _lastBossCount = -1;
            _lastEntityChangeAt = 0L;
            Trace("ZONE_LIST_WAIT", "mapId=" + _zoneListMapId);
            RequestZoneList(now);
            SendEvent(CmdZone, "ZONE_LIST_WAIT");
        }

        private int[] GetCurrentZoneListReference()
        {
            try
            {
                return GClass144.smethod_8().int_63;
            }
            catch
            {
                return null;
            }
        }

        private bool EnsureCentralZonePlan()
        {
            if (_zonePlanReady)
                return true;

            if (!_zoneCapacityReported)
            {
                int availableZoneCount = GetAvailableZoneCount();
                SendZoneCapacityTelemetry(Math.Max(0, availableZoneCount), _maxZone);
                _zoneCapacityReported = true;
                Trace("ZONE_CAPACITY", "maxZone=" + _maxZone + ";available=" + availableZoneCount);
                SendEvent(CmdZone, "WAITING_ASSIGNMENT");
            }
            return false;
        }

        private void ApplyZoneAssignment(BossHuntPayload payload)
        {
            _assignedZones.Clear();
            _assignedZonePosition = 0;
            _workerIndex = Math.Max(0, payload.workerIndex);
            _workerCount = Math.Max(1, payload.workerCount);
            _startZone = Math.Max(0, payload.startZone);

            string raw = payload.assignedZones ?? "";
            string[] parts = raw.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string token = parts[i].Trim();
                if (token.StartsWith("K", StringComparison.OrdinalIgnoreCase))
                    token = token.Substring(1);
                int zone;
                if (int.TryParse(token, out zone) && zone >= 0 && (_maxZone < 0 || zone <= _maxZone) && !_assignedZones.Contains(zone))
                    _assignedZones.Add(zone);
            }

            _zonePlanReady = true;
            _scanCycle = 1;
            if (_assignedZones.Count == 0)
            {
                _state = ScannerState.Standby;
                _desiredZone = -1;
                _usingAnnouncedZone = false;
                Trace("CENTRAL_ZONE_PLAN", "standby;total=" + payload.totalZones);
                SendEvent(CmdZone, "STANDBY|" + payload.totalZones);
                return;
            }

            int announcedIndex = _assignedZones.IndexOf(_announcedZone);
            _usingAnnouncedZone = announcedIndex >= 0;
            _assignedZonePosition = _usingAnnouncedZone ? announcedIndex : 0;
            _desiredZone = _assignedZones[_assignedZonePosition];
            _state = ScannerState.Scanning;
            _zoneAttempts = 0;
            _lastZoneCommandAt = 0L;
            Trace("CENTRAL_ZONE_PLAN", "assigned=" + BuildAssignedZonesText() + ";desired=" + _desiredZone + ";announcedOwned=" + _usingAnnouncedZone);
            SendZonePlanAppliedTelemetry(payload.totalZones);
            SendEvent(CmdZone, "SCANNING");
        }

        private bool TryResolveKnownBossLocation(out int mapId, out string mapName, out int zone)
        {
            mapId = -1;
            mapName = "";
            zone = -1;

            try
            {
                GClass156 item;
                if (!GClass156.TryGetLatestBossLocation(_bossName, out item) || item == null || item.int_0 < 0)
                    return false;

                mapId = item.int_0;
                mapName = item.string_1 ?? "";
                zone = item.int_1;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void AdvanceScanZone()
        {
            if (_usingAnnouncedZone)
            {
                _usingAnnouncedZone = false;
                int first = GetFirstAssignedZone();
                if (first == _desiredZone)
                    MoveToNextAssignedZone();
                else
                {
                    _desiredZone = first;
                    _zoneAttempts = 0;
                    _lastZoneCommandAt = 0L;
                }
                return;
            }

            MoveToNextAssignedZone();
        }

        private void UpdateScanning(long now)
        {
            GClass78 target = FindTargetBoss();
            if (target != null)
            {
                if (target.int_25 <= 0)
                {
                    ReportDead("Boss HP <= 0");
                    return;
                }

                _targetMapId = GClass20.int_37;
                _targetMapName = GClass20.string_1;
                _targetZone = GClass20.int_39;
                _state = ScannerState.Found;
                Trace("TARGET_FOUND", "hp=" + target.int_25);
                EnableAndFocus(target, now);
                SendEvent(CmdFound, "Tìm thấy boss");
                return;
            }

            int detectedMax = GetDetectedMaxZone();
            if (detectedMax < 0)
            {
                RequestZoneList(now);
                if (_zoneListWaitStartedAt > 0L && now - _zoneListWaitStartedAt >= ZoneListTimeoutMs)
                    ReportFailed("ZONE_LIST_TIMEOUT");
                return;
            }

            _maxZone = detectedMax;
            if (!EnsureCentralZonePlan())
                return;

            NormalizeDesiredZone();
            if (GClass20.int_39 == _desiredZone)
            {
                if (_arrivedZone != _desiredZone)
                {
                    _arrivedZone = _desiredZone;
                    _arrivedAt = now;
                    _zoneAttempts = 0;
                    BeginEntityObservation(now);
                    SendTelemetry("ZONE_ENTER", "announced=" + _usingAnnouncedZone);
                    SendEvent(CmdZone, "SCANNING");
                }
                else
                    UpdateEntityObservation(now);

                if (ShouldAdvanceFromCurrentZone(now))
                {
                    Trace("ZONE_DWELL_DONE", "elapsedMs=" + (now - _arrivedAt) + ";entities=" + _lastEntityCount + ";bosses=" + _lastBossCount);
                    SendTelemetry("ZONE_CLEAR", "dwellMs=" + (now - _arrivedAt));
                    AdvanceScanZone();
                    _arrivedZone = -1;
                    _arrivedAt = 0L;
                    _lastEntityCount = -1;
                    _lastBossCount = -1;
                    _lastEntityChangeAt = 0L;
                }
                return;
            }

            _arrivedZone = -1;
            if (now - _lastZoneCommandAt < 1200L)
                return;

            if (_zoneAttempts >= 2)
            {
                Trace("ZONE_CHANGE_FAILED", "targetZone=" + _desiredZone + ";attempts=" + _zoneAttempts);
                SendTelemetry("ZONE_FAILED", "attempts=" + _zoneAttempts);
                AdvanceScanZone();
                _zoneAttempts = 0;
                return;
            }

            Trace("ZONE_REQUEST", "targetZone=" + _desiredZone + ";attempt=" + (_zoneAttempts + 1));
            GClass7.smethod_0().method_42(_desiredZone, -1);
            _zoneAttempts++;
            _lastZoneCommandAt = now;
        }

        private void UpdateRally(long now)
        {
            if (_targetMapId < 0 || _targetZone < 0)
            {
                ReportFailed("RALLY_INVALID_TARGET");
                return;
            }

            if (_rallyStartedAt > 0L && now - _rallyStartedAt >= RallyOverallTimeoutMs)
            {
                ReportFailed("RALLY_TIMEOUT");
                return;
            }

            if (GClass20.int_37 != _targetMapId)
            {
                _rallyZoneArrivedAt = 0L;
                _rallyZoneAttempts = 0;
                if (TrackRouteProgress(now, ref _rallyRouteLastMapId, ref _rallyRouteLastProgressAt))
                {
                    Trace("RALLY_XMAP_PROGRESS", "targetMap=" + _targetMapId);
                    SendTelemetry("RALLY_ROUTE", "progress map=" + GClass20.int_37 + " -> " + _targetMapId);
                }

                bool xmapRunning = GClass148.smethod_0().bool_0;
                if (xmapRunning && now - _rallyRouteLastProgressAt < RouteStallTimeoutMs)
                    return;

                if (xmapRunning)
                {
                    Trace("RALLY_XMAP_STALL_RESTART", "targetMap=" + _targetMapId);
                    Class21.smethod_0().method_9();
                    _lastRouteCommandAt = 0L;
                    _rallyRouteLastProgressAt = now;
                }

                if (now - _lastRouteCommandAt >= RouteRetryDelayMs)
                {
                    Trace("RALLY_XMAP_START", "targetMap=" + _targetMapId);
                    SendTelemetry("RALLY_ROUTE", "start -> " + _targetMapName + " (#" + _targetMapId + ")");
                    Class21.smethod_0().method_8(_targetMapId);
                    _lastRouteCommandAt = now;
                }
                return;
            }

            if (GClass20.int_39 != _targetZone)
            {
                _rallyZoneArrivedAt = 0L;
                if (now - _lastZoneCommandAt < 1200L)
                    return;

                if (_rallyZoneAttempts >= RallyZoneMaxAttempts)
                {
                    ReportFailed("ZONE_FAILED K" + _targetZone);
                    return;
                }

                Trace("RALLY_ZONE_REQUEST", "targetZone=" + _targetZone + ";attempt=" + (_rallyZoneAttempts + 1));
                SendTelemetry("RALLY_ZONE", "K" + _targetZone + ";attempt=" + (_rallyZoneAttempts + 1));
                GClass7.smethod_0().method_42(_targetZone, -1);
                _rallyZoneAttempts++;
                _lastZoneCommandAt = now;
                return;
            }

            if (_rallyZoneArrivedAt <= 0L)
            {
                _rallyZoneArrivedAt = now;
                _rallyZoneAttempts = 0;
                SendTelemetry("RALLY_ZONE_ARRIVED", "K" + _targetZone);
            }

            GClass78 target = FindTargetBoss();
            if (target == null)
            {
                if (now - _rallyZoneArrivedAt >= RallyTargetLoadTimeoutMs)
                    ReportFailed("TARGET_NOT_FOUND");
                return;
            }
            if (target.int_25 <= 0)
            {
                ReportDead("Boss HP <= 0");
                return;
            }

            _targetMissingSince = 0L;
            _state = ScannerState.Fighting;
            Trace("RALLY_TARGET_FOUND", "hp=" + target.int_25);
            SendFightTelemetry(target);
            EnableAndFocus(target, now);
            if (!_readyReported)
            {
                _readyReported = true;
                Trace("READY", "targetZone=" + _targetZone);
                SendEvent(CmdReady, "Đã tới boss");
            }
        }

        private void UpdatePinnedFight(long now)
        {
            GClass78 target = FindTargetBoss();
            if (target == null)
            {
                if (_targetMissingSince <= 0L)
                {
                    _targetMissingSince = now;
                    Trace("TARGET_MISSING", "graceMs=" + FightingTargetLostGraceMs);
                }
                else if (now - _targetMissingSince >= FightingTargetLostGraceMs)
                    ReportFailed("TARGET_LOST");
                return;
            }

            _targetMissingSince = 0L;
            if (target.int_25 <= 0)
            {
                ReportDead("Boss HP <= 0");
                return;
            }
            EnableAndFocus(target, now);
        }

        private void EnableAndFocus(GClass78 target, long now)
        {
            GClass158.smethod_0().bool_0 = true;
            GClass78 me = GClass78.smethod_1();
            if (me == null)
                return;
            if (now - _lastFocusAt < 250L && me.gclass78_0 == target)
                return;

            me.gclass194_0 = null;
            me.gclass64_0 = null;
            me.gclass79_0 = null;
            me.gclass78_0 = target;
            GClass159.smethod_0().method_26(target.int_4, target.int_5);
            _lastFocusAt = now;
        }

        private static string ResolveBossEntityName(GClass78 boss)
        {
            if (boss == null)
                return "";
            try
            {
                return GClass158.smethod_0().method_0(boss, false) ?? "";
            }
            catch
            {
                return boss.string_3 ?? "";
            }
        }

        private GClass78 FindTargetBoss()
        {
            for (int i = 0; i < GClass158.list_3.Count; i++)
            {
                GClass78 boss = GClass158.list_3[i];
                if (boss == null || boss.bool_53 || boss.bool_54 || boss.int_13 >= 0)
                    continue;

                string actualName = ResolveBossEntityName(boss);

                string targetName = _sessionTargetLocked && !string.IsNullOrEmpty(_sessionTargetBossName)
                    ? _sessionTargetBossName
                    : _bossName;
                if (BossNameMatches(actualName, targetName))
                    return boss;
            }
            return null;
        }

        public void ObserveCombatCharacterDeath(
            int attackerId,
            string attackerName,
            int targetId,
            string targetName,
            int mapId,
            string mapName,
            int zone,
            long observedAtTicks)
        {
            if (targetId >= 0 || string.IsNullOrEmpty(targetName))
                return;

            PendingCombatDeath pending = new PendingCombatDeath
            {
                attackerId = attackerId,
                attackerName = attackerName ?? "",
                targetId = targetId,
                targetName = targetName ?? "",
                mapId = mapId,
                mapName = mapName ?? "",
                zone = zone,
                observedAtTicks = observedAtTicks > 0L ? observedAtTicks : DateTime.UtcNow.Ticks
            };

            BossHuntDiagnostics.Log("GAME_COMBAT", "COMBAT_DEATH_RX",
                _active ? _sessionId : 0,
                pending.targetName,
                _state.ToString(),
                "attackerId=" + pending.attackerId +
                ";attacker=" + pending.attackerName +
                ";targetId=" + pending.targetId +
                ";map=" + pending.mapId +
                ";zone=" + pending.zone);

            lock (_combatDeathLock)
            {
                if (_pendingCombatDeaths.Count >= 32)
                    _pendingCombatDeaths.Dequeue();
                _pendingCombatDeaths.Enqueue(pending);
            }
        }

        private void DrainCombatDeaths()
        {
            while (true)
            {
                PendingCombatDeath pending;
                lock (_combatDeathLock)
                {
                    if (_pendingCombatDeaths.Count == 0)
                        return;
                    pending = _pendingCombatDeaths.Dequeue();
                }

                if (pending == null || !_active || !_sessionTargetLocked)
                    continue;

                string lockedName = _sessionTargetBossName ?? "";
                if (!BossNameMatches(pending.targetName, lockedName))
                {
                    Trace("COMBAT_DEATH_IGNORE",
                        "reason=NAME;locked=" + lockedName + ";target=" + pending.targetName +
                        ";targetId=" + pending.targetId);
                    continue;
                }

                if (_sessionTargetMapId >= 0 && pending.mapId >= 0 && pending.mapId != _sessionTargetMapId)
                {
                    Trace("COMBAT_DEATH_IGNORE",
                        "reason=MAP;locked=" + _sessionTargetMapId + ";actual=" + pending.mapId +
                        ";target=" + pending.targetName);
                    continue;
                }

                if (_sessionTargetZone >= 0 && pending.zone >= 0 && pending.zone != _sessionTargetZone)
                {
                    Trace("COMBAT_DEATH_IGNORE",
                        "reason=ZONE;locked=" + _sessionTargetZone + ";actual=" + pending.zone +
                        ";target=" + pending.targetName);
                    continue;
                }

                string killer = string.IsNullOrEmpty(pending.attackerName)
                    ? ("#" + pending.attackerId)
                    : pending.attackerName;
                string raw =
                    "combat:-60;isDie=1" +
                    ";attackerId=" + pending.attackerId +
                    ";attacker=" + killer +
                    ";targetId=" + pending.targetId +
                    ";target=" + pending.targetName +
                    ";map=" + pending.mapId +
                    ";zone=" + pending.zone;

                Trace("COMBAT_DEATH_ACCEPT",
                    "target=" + pending.targetName +
                    ";targetId=" + pending.targetId +
                    ";killer=" + killer +
                    ";killerId=" + pending.attackerId);

                GClass156.InvalidateBossLocation(pending.targetName);
                SendCombatDeathObservation(pending, killer, raw);
                StopInternal();
                return;
            }
        }

        public void ObserveAnnouncement(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            BossHuntDiagnostics.Log("GAME_ANNOUNCEMENT", "RAW", _active ? _sessionId : 0,
                _active ? _bossName : "", _state.ToString(), message);

            lock (_announcementLock)
            {
                if (_pendingAnnouncements.Count >= 32)
                    _pendingAnnouncements.Dequeue();
                _pendingAnnouncements.Enqueue(message);
            }
        }

        private void DrainAnnouncements()
        {
            while (true)
            {
                string message;
                lock (_announcementLock)
                {
                    if (_pendingAnnouncements.Count == 0)
                        return;
                    message = _pendingAnnouncements.Dequeue();
                }

                string activeDeathTarget = _sessionTargetLocked && !string.IsNullOrEmpty(_sessionTargetBossName)
                    ? _sessionTargetBossName
                    : _bossName;
                bool looksLikeDeath = GClass156.LooksLikeBossDeathAnnouncement(message);
                bool mentionsActiveTarget = _active && MessageMentionsBossTarget(message, activeDeathTarget);

                if (_active)
                {
                    bool relevantRaw =
                        message.StartsWith("BOSS ", StringComparison.OrdinalIgnoreCase) ||
                        mentionsActiveTarget ||
                        (looksLikeDeath && mentionsActiveTarget);
                    if (relevantRaw)
                        SendTelemetry("ANNOUNCEMENT_RAW", message);
                }

                string deadBoss;
                string killer;
                if (GClass156.TryParseBossDeathAnnouncement(message, out deadBoss, out killer))
                {
                    BossHuntDiagnostics.Log("GAME_ANNOUNCEMENT", "DEATH_PARSED", _active ? _sessionId : 0,
                        deadBoss, _state.ToString(), "killer=" + killer + ";raw=" + message);

                    GClass156.InvalidateBossLocation(deadBoss);
                    SendBossObservation(CmdBossDeath, deadBoss, -1, "", -1, killer, message, DateTime.UtcNow.Ticks);

                    if (_active)
                    {
                        string deathTarget = _sessionTargetLocked && !string.IsNullOrEmpty(_sessionTargetBossName)
                            ? _sessionTargetBossName
                            : _bossName;
                        if (BossNameMatches(deadBoss, deathTarget))
                        {
                            Trace("ANNOUNCEMENT_DEATH", "boss=" + deadBoss + ";killer=" + killer + ";raw=" + message);
                            StopInternal();
                            return;
                        }
                    }
                    continue;
                }

                string targetedDeadBoss;
                string targetedKiller;
                if (_active &&
                    TryParseTargetedAdmirationDeath(message, activeDeathTarget, out targetedDeadBoss, out targetedKiller))
                {
                    BossHuntDiagnostics.Log("GAME_ANNOUNCEMENT", "DEATH_PARSED_TARGET_FALLBACK",
                        _sessionId, targetedDeadBoss, _state.ToString(),
                        "killer=" + targetedKiller + ";raw=" + message);

                    GClass156.InvalidateBossLocation(targetedDeadBoss);
                    SendBossObservation(
                        CmdBossDeath,
                        targetedDeadBoss,
                        -1,
                        "",
                        -1,
                        targetedKiller,
                        message,
                        DateTime.UtcNow.Ticks);

                    Trace("ANNOUNCEMENT_DEATH_TARGET_FALLBACK",
                        "boss=" + targetedDeadBoss + ";killer=" + targetedKiller + ";raw=" + message);
                    StopInternal();
                    return;
                }

                if (looksLikeDeath)
                {
                    if (_active && mentionsActiveTarget)
                    {
                        BossHuntDiagnostics.Log("GAME_ANNOUNCEMENT", "DEATH_UNPARSED", _sessionId,
                            activeDeathTarget ?? "", _state.ToString(), message);
                        SendTelemetry("DEATH_UNPARSED", message);
                    }
                    else
                    {
                        BossHuntDiagnostics.Log("GAME_ANNOUNCEMENT", "DEATH_UNPARSED_IGNORED",
                            _active ? _sessionId : 0,
                            _active ? activeDeathTarget : "",
                            _state.ToString(),
                            "reason=UNRELATED_TARGET;raw=" + message);
                    }
                }

                string spawnBoss;
                string mapName;
                int mapId;
                int zone;
                if (GClass156.TryParseBossAnnouncement(message, out spawnBoss, out mapName, out mapId, out zone))
                {
                    BossHuntDiagnostics.Log("GAME_ANNOUNCEMENT", "SPAWN_PARSED", _active ? _sessionId : 0,
                        spawnBoss, _state.ToString(), "map=" + mapId + ";zone=" + zone + ";raw=" + message);
                    SendBossObservation(CmdBossSpawn, spawnBoss, mapId, mapName, zone, "", message, DateTime.UtcNow.Ticks);

                    if (_active &&
                        (_state == ScannerState.WaitingLocation || _state == ScannerState.RoutingToScanMap || _state == ScannerState.Scanning) &&
                        BossNameMatches(spawnBoss, _bossName))
                    {
                        string lockedText = _sessionTargetLocked
                            ? _sessionTargetBossName + "@" + _sessionTargetMapId + "/K" + _sessionTargetZone
                            : "-";
                        Trace("ANNOUNCEMENT_SPAWN_LOCAL",
                            "mapId=" + mapId + ";map=" + mapName + ";zone=" + zone +
                            ";locked=" + lockedText + ";waitingManagerSync=true");
                    }
                }
            }
        }

        private void ClearAnnouncements()
        {
            lock (_announcementLock)
                _pendingAnnouncements.Clear();
        }

        private void BeginEntityObservation(long now)
        {
            _lastEntityCount = GetVisibleEntityCount();
            _lastBossCount = GetVisibleBossCount();
            _lastEntityChangeAt = now;
            Trace("ZONE_ARRIVED", "zone=" + _desiredZone + ";entities=" + _lastEntityCount + ";bosses=" + _lastBossCount + ";announced=" + _usingAnnouncedZone);
        }

        private void UpdateEntityObservation(long now)
        {
            int entityCount = GetVisibleEntityCount();
            int bossCount = GetVisibleBossCount();
            if (entityCount == _lastEntityCount && bossCount == _lastBossCount)
                return;

            _lastEntityCount = entityCount;
            _lastBossCount = bossCount;
            _lastEntityChangeAt = now;
            Trace("ENTITY_CHANGE", "entities=" + entityCount + ";bosses=" + bossCount);
            SendTelemetry("ENTITY_SNAPSHOT", "entities=" + entityCount + ";bosses=" + bossCount);
        }

        private bool ShouldAdvanceFromCurrentZone(long now)
        {
            long elapsed = now - _arrivedAt;
            if (elapsed < EntityMinDwellMs)
                return false;

            if (_lastEntityCount > 0 && now - _lastEntityChangeAt >= EntityStableWindowMs)
                return true;

            long maxDwell = _usingAnnouncedZone ? AnnouncedZoneMaxDwellMs : EntityMaxDwellMs;
            return elapsed >= maxDwell;
        }

        private static int GetVisibleEntityCount()
        {
            try
            {
                return GClass144.gclass88_5.method_2();
            }
            catch
            {
                return 0;
            }
        }

        private static int GetVisibleBossCount()
        {
            try
            {
                return GClass158.list_3.Count;
            }
            catch
            {
                return 0;
            }
        }

        private void Trace(string eventName, string detail)
        {
            BossHuntDiagnostics.Log("GAME", eventName, _sessionId, _bossName, _state.ToString(), detail);
        }

        private void MoveToNextAssignedZone()
        {
            if (_assignedZones.Count == 0)
            {
                _state = ScannerState.Standby;
                _desiredZone = -1;
                _usingAnnouncedZone = false;
                SendEvent(CmdZone, "STANDBY|0");
                return;
            }

            int currentIndex = _assignedZones.IndexOf(_desiredZone);
            if (currentIndex < 0)
                currentIndex = _assignedZonePosition;
            int nextIndex = currentIndex + 1;
            if (nextIndex >= _assignedZones.Count)
            {
                nextIndex = 0;
                _scanCycle++;
            }

            _assignedZonePosition = nextIndex;
            _desiredZone = _assignedZones[_assignedZonePosition];
            _zoneAttempts = 0;
            _lastZoneCommandAt = 0L;
        }

        private int GetEffectiveStartZone()
        {
            int effectiveStart = _startZone;
            if (effectiveStart < 0 || effectiveStart > _maxZone)
                effectiveStart = 0;
            return effectiveStart;
        }

        private int GetAvailableZoneCount()
        {
            if (_maxZone < 0)
                return 0;
            return _maxZone + 1;
        }

        private int GetFirstAssignedZone()
        {
            return _assignedZones.Count == 0 ? -1 : _assignedZones[0];
        }

        private bool IsZoneAssignedToWorker(int zone)
        {
            return zone >= 0 && _assignedZones.Contains(zone);
        }

        private void NormalizeDesiredZone()
        {
            if (_assignedZones.Count == 0)
            {
                _desiredZone = -1;
                return;
            }

            int index = _assignedZones.IndexOf(_desiredZone);
            if (index < 0)
            {
                _assignedZonePosition = 0;
                _desiredZone = _assignedZones[0];
            }
            else
                _assignedZonePosition = index;
        }

        private int GetDetectedMaxZone()
        {
            try
            {
                if (GClass20.int_37 != _zoneListMapId)
                    return -1;

                int[] current = GClass144.smethod_8().int_63;
                if (current == null || current.Length <= 0)
                    return -1;

                if (!_zoneListFresh)
                {
                    if (object.ReferenceEquals(current, _zoneListBaseline))
                        return -1;
                    _zoneListFresh = true;
                    Trace("ZONE_LIST_FRESH", "zones=" + current.Length + ";mapId=" + _zoneListMapId);
                }

                return current.Length - 1;
            }
            catch
            {
                return -1;
            }
        }

        private void RequestZoneList(long now)
        {
            if (now - _lastZoneListRequestAt < 1000L)
                return;
            try
            {
                Trace("ZONE_LIST_REQUEST", "mapId=" + _zoneListMapId);
                GClass7.smethod_0().method_58();
                _lastZoneListRequestAt = now;
            }
            catch
            {
            }
        }

        private void ReportDead(string detail)
        {
            if (!_active)
                return;
            Trace("DEAD", detail);
            SendEvent(CmdDead, detail);
            StopInternal();
        }

        private void ReportFailed(string detail)
        {
            if (!_active)
                return;
            Trace("FAILED", detail);
            SendEvent(CmdFailed, detail);
            StopInternal();
        }

        public void SendBossCatalogSnapshot()
        {
            try
            {
                List<string> names = new List<string>();

                try
                {
                    List<GClass156> cached = GClass156.GetBossLocationSnapshot();
                    for (int i = 0; i < cached.Count; i++)
                    {
                        GClass156 item = cached[i];
                        if (item != null)
                            AddUniqueBossName(names, item.string_0);
                    }
                }
                catch
                {
                }

                try
                {
                    for (int i = 0; i < GClass158.list_3.Count; i++)
                    {
                        GClass78 boss = GClass158.list_3[i];
                        if (boss == null)
                            continue;

                        string name;
                        try
                        {
                            name = GClass158.smethod_0().method_0(boss, false);
                        }
                        catch
                        {
                            name = boss.string_3 ?? "";
                        }
                        AddUniqueBossName(names, name);
                    }
                }
                catch
                {
                }

                if (!string.IsNullOrEmpty(_bossName))
                    AddUniqueBossName(names, _bossName);
                if (!string.IsNullOrEmpty(_sessionTargetBossName))
                    AddUniqueBossName(names, _sessionTargetBossName);

                if (names.Count == 0)
                    return;

                BossHuntPayload payload = new BossHuntPayload
                {
                    eventName = "BOSS_CATALOG_SYNC",
                    bossNames = names.ToArray()
                };
                GClass150.smethod_0().method_2(new vMessage
                {
                    cmd = CmdBossCatalog,
                    data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload))
                });

                BossHuntDiagnostics.Log("GAME", "BOSS_CATALOG_SYNC", _active ? _sessionId : 0,
                    _active ? _bossName : "", _state.ToString(), "count=" + names.Count);
            }
            catch
            {
            }
        }

        private void SendBossCatalogSnapshotIfDue(long now)
        {
            if (now - _lastCatalogSyncAt < BossCatalogSyncIntervalMs)
                return;
            _lastCatalogSyncAt = now;
            SendBossCatalogSnapshot();
        }

        private static void AddUniqueBossName(List<string> names, string value)
        {
            string normalized = (value ?? "").Trim().Trim('[', ']', ':', '-', '.', ' ');
            if (normalized.Length == 0)
                return;

            for (int i = 0; i < names.Count; i++)
            {
                if (names[i].Equals(normalized, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            names.Add(normalized);
        }

        public void SendKnownBossLocations()
        {
            try
            {
                List<GClass156> bosses = GClass156.GetBossLocationSnapshot();
                for (int i = 0; i < bosses.Count; i++)
                {
                    GClass156 boss = bosses[i];
                    if (boss == null || boss.int_0 < 0)
                        continue;
                    SendBossObservation(CmdBossSpawn, boss.string_0, boss.int_0, boss.string_1, boss.int_1, "", "CACHE_SYNC",
                        boss.dateTime_0.ToUniversalTime().Ticks);
                }
            }
            catch
            {
            }
        }

        private void ApplyBossSync(BossHuntPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.bossName) || payload.mapId < 0)
                return;

            GClass156.ApplyBossLocationSync(payload.bossName, payload.mapName, payload.mapId, payload.zone, payload.observedAtTicks);
            Trace("MANAGER_BOSS_SYNC", "boss=" + payload.bossName + ";map=" + payload.mapId + ";zone=" + payload.zone);

            if (!_active || !BossNameMatches(payload.bossName, _bossName))
                return;
            if (_state != ScannerState.WaitingLocation && _state != ScannerState.RoutingToScanMap && _state != ScannerState.Scanning)
                return;

            if (_sessionTargetLocked && !SyncMatchesLockedTarget(payload))
            {
                Trace("SESSION_TARGET_SYNC_IGNORED",
                    "locked=" + _sessionTargetBossName + "@" + _sessionTargetMapId + "/K" + _sessionTargetZone +
                    ";incoming=" + payload.bossName + "@" + payload.mapId + "/K" + payload.zone);
                return;
            }

            if (!_sessionTargetLocked)
                LockSessionTarget(payload.bossName, payload.mapId, payload.zone, payload.observedAtTicks);

            if (_scanMapId == payload.mapId && _announcedZone == payload.zone && _state != ScannerState.WaitingLocation)
                return;

            SetScanLocation(payload.mapId, payload.mapName, payload.zone, GClass203.smethod_18());
        }

        private void SendHeartbeatIfDue(long now)
        {
            if (!_active || now - _lastHeartbeatAt < HeartbeatIntervalMs)
                return;

            GClass78 target = FindTargetBoss();
            BossHuntPayload payload = CreateClientPayload();
            payload.eventName = "HEARTBEAT";
            payload.detail = _state.ToString();
            payload.entityCount = GetVisibleEntityCount();
            payload.bossCount = GetVisibleBossCount();
            payload.targetHp = target == null ? -1 : target.int_25;
            GClass150.smethod_0().method_2(new vMessage
            {
                cmd = CmdHeartbeat,
                data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload))
            });
            _lastHeartbeatAt = now;
        }

        private string BuildAssignedZonesText()
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < _assignedZones.Count; i++)
            {
                if (builder.Length > 0)
                    builder.Append(",");
                builder.Append("K");
                builder.Append(_assignedZones[i]);
            }
            return builder.ToString();
        }

        private void SendZonePlanAppliedTelemetry(int totalZones)
        {
            try
            {
                BossHuntPayload payload = CreateClientPayload();
                payload.eventName = "ZONE_PLAN";
                payload.totalZones = totalZones;
                payload.maxZone = _maxZone;
                payload.assignedZones = BuildAssignedZonesText();
                payload.detail = "assigned=" + payload.assignedZones + ";total=" + totalZones;
                GClass150.smethod_0().method_2(new vMessage
                {
                    cmd = CmdTelemetry,
                    data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload))
                });
            }
            catch
            {
            }
        }

        private void SendZoneCapacityTelemetry(int totalZones, int maxZone)
        {
            try
            {
                BossHuntPayload payload = CreateClientPayload();
                payload.eventName = "ZONE_CAPACITY";
                payload.totalZones = totalZones;
                payload.maxZone = maxZone;
                payload.detail = "maxZone=" + maxZone + ";total=" + totalZones;
                GClass150.smethod_0().method_2(new vMessage
                {
                    cmd = CmdTelemetry,
                    data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload))
                });
            }
            catch
            {
            }
        }

        private void SendFightTelemetry(GClass78 target)
        {
            try
            {
                BossHuntPayload payload = CreateClientPayload();
                payload.eventName = "FIGHTING";
                payload.targetHp = target == null ? -1 : target.int_25;
                payload.entityCount = GetVisibleEntityCount();
                payload.bossCount = GetVisibleBossCount();
                payload.detail = "hp=" + payload.targetHp;
                GClass150.smethod_0().method_2(new vMessage
                {
                    cmd = CmdTelemetry,
                    data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload))
                });
            }
            catch
            {
            }
        }

        private void SendTelemetry(string eventName, string detail)
        {
            try
            {
                BossHuntPayload payload = CreateClientPayload();
                payload.eventName = eventName ?? "";
                payload.detail = detail ?? "";
                payload.entityCount = _lastEntityCount >= 0 ? _lastEntityCount : GetVisibleEntityCount();
                payload.bossCount = _lastBossCount >= 0 ? _lastBossCount : GetVisibleBossCount();
                GClass78 target = FindTargetBoss();
                payload.targetHp = target == null ? -1 : target.int_25;
                payload.scanCycle = _scanCycle;
                GClass150.smethod_0().method_2(new vMessage
                {
                    cmd = CmdTelemetry,
                    data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload))
                });
            }
            catch
            {
            }
        }

        private void SendBossObservation(int cmd, string bossName, int mapId, string mapName, int zone, string killer, string rawMessage, long observedAtTicks)
        {
            try
            {
                BossHuntPayload payload = new BossHuntPayload
                {
                    sessionId = _active ? _sessionId : 0,
                    assignmentGeneration = _active ? _assignmentGeneration : 0,
                    bossName = bossName ?? "",
                    mapId = mapId,
                    mapName = mapName ?? "",
                    zone = zone,
                    accountId = GClass150.int_0,
                    killer = killer ?? "",
                    rawMessage = rawMessage ?? "",
                    observedAtTicks = observedAtTicks
                };
                GClass150.smethod_0().method_2(new vMessage
                {
                    cmd = cmd,
                    data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload))
                });
            }
            catch
            {
            }
        }
 
        private void SendCombatDeathObservation(PendingCombatDeath pending, string killer, string rawMessage)
        {
            try
            {
                BossHuntPayload payload = new BossHuntPayload
                {
                    sessionId = _sessionId,
                    assignmentGeneration = _assignmentGeneration,
                    bossName = pending.targetName ?? "",
                    targetBossName = _sessionTargetBossName ?? "",
                    mapId = pending.mapId,
                    mapName = pending.mapName ?? "",
                    zone = pending.zone,
                    accountId = GClass150.int_0,
                    killer = killer ?? "",
                    killerId = pending.attackerId,
                    rawMessage = rawMessage ?? "",
                    observedAtTicks = pending.observedAtTicks
                };
                GClass150.smethod_0().method_2(new vMessage
                {
                    cmd = CmdBossDeath,
                    data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload))
                });
            }
            catch
            {
            }
        }

        private BossHuntPayload CreateClientPayload()
        {
            return new BossHuntPayload
            {
                sessionId = _sessionId,
                assignmentGeneration = _assignmentGeneration,
                bossName = _bossName,
                startZone = _startZone,
                workerIndex = _workerIndex,
                workerCount = _workerCount,
                mapId = GClass20.int_37,
                mapName = GClass20.string_1,
                zone = GClass20.int_39,
                accountId = GClass150.int_0,
                scanCycle = _scanCycle
            };
        }

        private void SendEvent(int cmd, string detail)
        {
            try
            {
                BossHuntPayload payload = CreateClientPayload();
                GClass78 currentTarget = FindTargetBoss();
                payload.targetHp = currentTarget == null ? -1 : currentTarget.int_25;
                payload.detail = detail ?? "";

                if (cmd == CmdFound && currentTarget != null)
                {
                    string concreteName = ResolveBossEntityName(currentTarget);
                    if (string.IsNullOrEmpty(concreteName))
                    {
                        concreteName = _sessionTargetLocked && !string.IsNullOrEmpty(_sessionTargetBossName)
                            ? _sessionTargetBossName
                            : _bossName;
                    }

                    long foundObservedAtTicks = _sessionTargetObservedAtTicks > 0L
                        ? _sessionTargetObservedAtTicks
                        : DateTime.UtcNow.Ticks;
                    LockSessionTarget(concreteName, payload.mapId, payload.zone, foundObservedAtTicks);
                    payload.targetBossName = concreteName;
                    payload.observedAtTicks = foundObservedAtTicks;
                    Trace("TX_FOUND",
                        "target=" + concreteName +
                        ";map=" + payload.mapId +
                        ";zone=" + payload.zone +
                        ";hp=" + payload.targetHp);
                }
                else
                {
                    Trace("TX_EVENT", "cmd=" + cmd + ";detail=" + (detail ?? ""));
                }

                GClass150.smethod_0().method_2(new vMessage
                {
                    cmd = cmd,
                    data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload))
                });
            }
            catch
            {
            }
        }

        private void StopInternal()
        {
            string oldBoss = _bossName;
            Trace("STOP_LOCAL", "");
            _active = false;
            _state = ScannerState.Idle;

            try
            {
                GClass158.smethod_0().bool_0 = _previousAutoBoss;
                GClass78 me = GClass78.smethod_1();
                if (me != null && me.gclass78_0 != null)
                {
                    string currentName;
                    try
                    {
                        currentName = GClass158.smethod_0().method_0(me.gclass78_0, false);
                    }
                    catch
                    {
                        currentName = me.gclass78_0.string_3 ?? "";
                    }
                    if (BossNameMatches(currentName, oldBoss))
                        me.gclass78_0 = null;
                }
            }
            catch
            {
            }

            _scanMapId = -1;
            _scanMapName = "";
            _announcedZone = -1;
            _usingAnnouncedZone = false;
            _targetMapId = -1;
            _targetMapName = "";
            _targetZone = -1;
            _readyReported = false;
            _rallyStartedAt = 0L;
            _rallyZoneAttempts = 0;
            _rallyZoneArrivedAt = 0L;
            _targetMissingSince = 0L;
            _scanRouteStartedAt = 0L;
            _lastScanRouteCommandAt = 0L;
            _zoneListBaseline = null;
            _zoneListMapId = -1;
            _zoneListWaitStartedAt = 0L;
            _zoneListFresh = false;
            _zonePlanReady = false;
            _lastEntityCount = -1;
            _lastBossCount = -1;
            _lastEntityChangeAt = 0L;
            _scanRouteLastMapId = -1;
            _scanRouteLastProgressAt = 0L;
            _rallyRouteLastMapId = -1;
            _rallyRouteLastProgressAt = 0L;
            ClearAnnouncements();
            lock (_combatDeathLock)
                _pendingCombatDeaths.Clear();
            _lastFocusAt = 0L;
            _lastHeartbeatAt = 0L;
            _scanCycle = 0;
            _assignmentGeneration = 0;
            _sessionTargetLocked = false;
            _sessionTargetBossName = "";
            _sessionTargetMapId = -1;
            _sessionTargetZone = -1;
            _sessionTargetObservedAtTicks = 0L;
        }

        private static BossHuntPayload Deserialize(byte[] data)
        {
            if (data == null)
                return null;
            try
            {
                return JsonConvert.DeserializeObject<BossHuntPayload>(Encoding.UTF8.GetString(data));
            }
            catch
            {
                return null;
            }
        }

        private static bool TryParseTargetedAdmirationDeath(
            string message,
            string target,
            out string deadBoss,
            out string killer)
        {
            deadBoss = "";
            killer = "";
            message = (message ?? "").Trim();
            target = NormalizeBossName(target);
            if (message.Length == 0 || target.Length == 0)
                return false;

            string lower = message.ToLowerInvariant();
            string[] markers = new string[]
            {
                " tiêu diệt được ",
                " diệt được ",
                " hạ được ",
                " đánh bại được "
            };

            int markerIndex = -1;
            string marker = "";
            for (int i = 0; i < markers.Length; i++)
            {
                int index = lower.IndexOf(markers[i], StringComparison.Ordinal);
                if (index >= 0 && (markerIndex < 0 || index < markerIndex))
                {
                    markerIndex = index;
                    marker = markers[i];
                }
            }
            if (markerIndex < 0)
                return false;

            int victimStart = markerIndex + marker.Length;
            string remainder = message.Substring(victimStart).Trim();
            string remainderLower = remainder.ToLowerInvariant();
            int suffixIndex = remainderLower.IndexOf(" mọi người", StringComparison.Ordinal);
            if (suffixIndex < 0)
                return false;

            string victim = remainder.Substring(0, suffixIndex).Trim().Trim('.', '!', ':', '-', '[', ']', ' ');
            if (victim.StartsWith("BOSS ", StringComparison.OrdinalIgnoreCase))
                victim = victim.Substring(5).Trim();

            if (!BossNameMatches(victim, target))
                return false;

            deadBoss = NormalizeBossName(victim);
            killer = message.Substring(0, markerIndex).Trim().Trim('.', '!', ':', '-', '[', ']', ' ');
            string killerLower = killer.ToLowerInvariant();
            if (killerLower.StartsWith("người chơi ", StringComparison.Ordinal))
                killer = killer.Substring("người chơi ".Length).Trim();

            return deadBoss.Length > 0;
        }

        private static bool MessageMentionsBossTarget(string message, string target)
        {
            message = message ?? "";
            target = NormalizeBossName(target);
            if (message.Length == 0 || target.Length == 0)
                return false;

            int searchStart = 0;
            while (searchStart < message.Length)
            {
                int index = message.IndexOf(target, searchStart, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    return false;

                bool leftBoundary =
                    index == 0 ||
                    char.IsWhiteSpace(message[index - 1]) ||
                    message[index - 1] == '[' ||
                    message[index - 1] == ':' ||
                    message[index - 1] == '-';

                int end = index + target.Length;
                bool rightBoundary =
                    end >= message.Length ||
                    char.IsWhiteSpace(message[end]) ||
                    message[end] == ']' ||
                    message[end] == ':' ||
                    message[end] == '-' ||
                    message[end] == '.' ||
                    message[end] == ',' ||
                    message[end] == '!' ||
                    message[end] == '(';

                if (leftBoundary && rightBoundary)
                    return true;

                searchStart = index + 1;
            }

            return false;
        }

        private static bool BossNameMatches(string actual, string target)
        {
            actual = NormalizeBossName(actual);
            target = NormalizeBossName(target);
            if (actual.Length == 0 || target.Length == 0)
                return false;
            if (actual == target)
                return true;
            if (!actual.StartsWith(target, StringComparison.OrdinalIgnoreCase))
                return false;
            if (actual.Length == target.Length)
                return true;
            char next = actual[target.Length];
            return char.IsWhiteSpace(next) || next == '-' || next == '(' || next == '[';
        }

        private static string NormalizeBossName(string value)
        {
            return (value ?? "").Trim().Trim('[', ']', ':', '-', '.', ' ');
        }


    }
}
