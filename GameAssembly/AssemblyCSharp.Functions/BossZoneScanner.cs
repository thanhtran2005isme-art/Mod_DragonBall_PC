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
            public int maxZone = -1;
            public string assignedZones;
        }

        private sealed class PendingCommand
        {
            public int cmd;
            public BossHuntPayload payload;
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
        private readonly List<int> _assignedZones = new List<int>();

        private bool _active;
        private int _sessionId;
        private int _assignmentGeneration;
        private string _bossName = "";
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
                DrainAnnouncements();
                if (!_active)
                    return;

                long now = GClass203.smethod_18();
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

            GClass78 currentTarget = FindTargetBoss();
            if (currentTarget != null)
            {
                SetScanLocation(GClass20.int_37, GClass20.string_1, GClass20.int_39, _startedAt);
                return;
            }

            if (payload.mapId >= 0)
            {
                GClass156.ApplyBossLocationSync(_bossName, payload.mapName, payload.mapId, payload.zone, payload.observedAtTicks);
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

        private GClass78 FindTargetBoss()
        {
            for (int i = 0; i < GClass158.list_3.Count; i++)
            {
                GClass78 boss = GClass158.list_3[i];
                if (boss == null || boss.bool_53 || boss.bool_54 || boss.int_13 >= 0)
                    continue;

                string actualName;
                try
                {
                    actualName = GClass158.smethod_0().method_0(boss, false);
                }
                catch
                {
                    actualName = boss.string_3 ?? "";
                }

                if (BossNameMatches(actualName, _bossName))
                    return boss;
            }
            return null;
        }

        public void ObserveAnnouncement(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            BossHuntDiagnostics.Log("GAME_ANNOUNCEMENT", "RAW", _active ? _sessionId : 0,
                _active ? _bossName : "", _state.ToString(), message);

            if (_active)
            {
                bool relevantRaw =
                    message.StartsWith("BOSS ", StringComparison.OrdinalIgnoreCase) ||
                    GClass156.LooksLikeBossDeathAnnouncement(message) ||
                    (!string.IsNullOrEmpty(_bossName) &&
                     message.IndexOf(_bossName, StringComparison.OrdinalIgnoreCase) >= 0);
                if (relevantRaw)
                    SendTelemetry("ANNOUNCEMENT_RAW", message);
            }

            string deadBoss;
            string killer;
            if (GClass156.TryParseBossDeathAnnouncement(message, out deadBoss, out killer))
            {
                BossHuntDiagnostics.Log("GAME_ANNOUNCEMENT", "DEATH_PARSED", _active ? _sessionId : 0,
                    deadBoss, _state.ToString(), "killer=" + killer + ";raw=" + message);
                SendBossObservation(CmdBossDeath, deadBoss, -1, "", -1, killer, message, DateTime.UtcNow.Ticks);
            }
            else
            {
                if (GClass156.LooksLikeBossDeathAnnouncement(message))
                    BossHuntDiagnostics.Log("GAME_ANNOUNCEMENT", "DEATH_UNPARSED", _active ? _sessionId : 0,
                        _active ? _bossName : "", _state.ToString(), message);

                string spawnBoss;
                string mapName;
                int mapId;
                int zone;
                if (GClass156.TryParseBossAnnouncement(message, out spawnBoss, out mapName, out mapId, out zone))
                {
                    BossHuntDiagnostics.Log("GAME_ANNOUNCEMENT", "SPAWN_PARSED", _active ? _sessionId : 0,
                        spawnBoss, _state.ToString(), "map=" + mapId + ";zone=" + zone + ";raw=" + message);
                    SendBossObservation(CmdBossSpawn, spawnBoss, mapId, mapName, zone, "", message, DateTime.UtcNow.Ticks);
                }
            }

            if (!_active)
                return;

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

                if (!_active)
                    continue;

                if (DeathAnnouncementMatches(message, _bossName))
                {
                    Trace("ANNOUNCEMENT_DEATH", message);
                    ReportDead(message);
                    return;
                }

                if (_state == ScannerState.WaitingLocation || _state == ScannerState.RoutingToScanMap || _state == ScannerState.Scanning)
                {
                    string announcedBoss;
                    string announcedMap;
                    int announcedMapId;
                    int announcedZone;
                    if (GClass156.TryParseBossAnnouncement(message, out announcedBoss, out announcedMap, out announcedMapId, out announcedZone) &&
                        BossNameMatches(announcedBoss, _bossName))
                    {
                        Trace("ANNOUNCEMENT_SPAWN_LOCAL", "mapId=" + announcedMapId + ";map=" + announcedMap + ";zone=" + announcedZone + ";waitingManagerSync=true");
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
                Trace("TX_EVENT", "cmd=" + cmd + ";detail=" + (detail ?? ""));
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
            _lastFocusAt = 0L;
            _lastHeartbeatAt = 0L;
            _scanCycle = 0;
            _assignmentGeneration = 0;
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

        private static bool DeathAnnouncementMatches(string message, string target)
        {
            if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(target))
                return false;

            string text = message.Trim();
            if (text.StartsWith("!", StringComparison.Ordinal))
                text = text.Substring(1).Trim();
            if (text.StartsWith("BOSS ", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(5).Trim();

            string[] markers = new string[]
            {
                " vừa bị tiêu diệt", " đã bị tiêu diệt", " bị tiêu diệt",
                " vừa chết", " đã chết",
                " vừa bị hạ", " đã bị hạ", " bị hạ",
                " has been defeated", " was defeated",
                " has been killed", " was killed",
                " is dead", " defeated by", " killed by"
            };

            string lower = text.ToLowerInvariant();
            int cut = -1;
            for (int i = 0; i < markers.Length; i++)
            {
                int index = lower.IndexOf(markers[i], StringComparison.Ordinal);
                if (index >= 0 && (cut < 0 || index < cut))
                    cut = index;
            }
            if (cut < 0)
                return false;

            string announcedBoss = text.Substring(0, cut).Trim();
            return BossNameMatches(announcedBoss, target);
        }
    }
}
