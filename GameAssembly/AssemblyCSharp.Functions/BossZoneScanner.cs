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
            public string bossName;
            public int startZone;
            public int workerIndex;
            public int workerCount;
            public int mapId;
            public string mapName;
            public int zone;
            public int accountId;
            public string detail;
        }

        private sealed class PendingCommand
        {
            public int cmd;
            public BossHuntPayload payload;
        }

        private const int CmdStartScan = 100;
        private const int CmdStop = 101;
        private const int CmdRally = 102;

        private const int CmdZone = 110;
        private const int CmdFound = 111;
        private const int CmdDead = 112;
        private const int CmdReady = 113;
        private const int CmdFailed = 114;

        private const long ScanRouteTimeoutMs = 90000L;
        private const long RouteRetryDelayMs = 1200L;
        private const long RouteStallTimeoutMs = 30000L;
        private const long RallyOverallTimeoutMs = 90000L;
        private const long ZoneListTimeoutMs = 10000L;
        private const int RallyZoneMaxAttempts = 3;
        private const long RallyTargetLoadTimeoutMs = 8000L;
        private const long FightingTargetLostGraceMs = 3000L;

        private static readonly BossZoneScanner _instance = new BossZoneScanner();

        private readonly object _commandLock = new object();
        private readonly Queue<PendingCommand> _pendingCommands = new Queue<PendingCommand>();
        private readonly object _announcementLock = new object();
        private readonly Queue<string> _pendingAnnouncements = new Queue<string>();

        private bool _active;
        private int _sessionId;
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
            if (cmd != CmdStartScan && cmd != CmdStop && cmd != CmdRally)
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

                if (pending.cmd == CmdStartScan)
                {
                    StartScan(pending.payload);
                    continue;
                }

                if (!_active || pending.payload.sessionId != _sessionId)
                    continue;

                if (pending.cmd == CmdStop)
                {
                    StopInternal();
                    continue;
                }

                if (pending.cmd == CmdRally)
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
            _rallyStartedAt = 0L;
            _rallyZoneAttempts = 0;
            _rallyZoneArrivedAt = 0L;
            _targetMissingSince = 0L;
            ClearAnnouncements();

            GClass78 currentTarget = FindTargetBoss();
            if (currentTarget != null)
            {
                SetScanLocation(GClass20.int_37, GClass20.string_1, GClass20.int_39, _startedAt);
                return;
            }

            int knownMapId;
            string knownMapName;
            int knownZone;
            if (TryResolveKnownBossLocation(out knownMapId, out knownMapName, out knownZone))
            {
                SetScanLocation(knownMapId, knownMapName, knownZone, _startedAt);
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
        }

        private void UpdateWaitingLocation(long now)
        {
            GClass78 currentTarget = FindTargetBoss();
            if (currentTarget != null)
            {
                SetScanLocation(GClass20.int_37, GClass20.string_1, GClass20.int_39, now);
                return;
            }

            int knownMapId;
            string knownMapName;
            int knownZone;
            if (TryResolveKnownBossLocation(out knownMapId, out knownMapName, out knownZone))
                SetScanLocation(knownMapId, knownMapName, knownZone, now);
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
                BeginScanningOnCurrentMap(now);
                return;
            }

            TrackRouteProgress(now, ref _scanRouteLastMapId, ref _scanRouteLastProgressAt);

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
                Class21.smethod_0().method_9();
                _lastScanRouteCommandAt = 0L;
                _scanRouteLastProgressAt = now;
            }

            if (now - _lastScanRouteCommandAt < RouteRetryDelayMs)
                return;

            Class21.smethod_0().method_8(_scanMapId);
            _lastScanRouteCommandAt = now;
            SendEvent(CmdZone, "ROUTING_MAP|" + _scanMapName);
        }

        private static void TrackRouteProgress(long now, ref int lastMapId, ref long lastProgressAt)
        {
            int currentMapId = GClass20.int_37;
            if (lastMapId != currentMapId)
            {
                lastMapId = currentMapId;
                lastProgressAt = now;
            }
            else if (lastProgressAt <= 0L)
                lastProgressAt = now;
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

        private bool PrepareZonePlan()
        {
            int availableZoneCount = GetAvailableZoneCount();
            if (availableZoneCount <= 0 || _workerIndex >= availableZoneCount)
            {
                _state = ScannerState.Standby;
                _desiredZone = -1;
                _usingAnnouncedZone = false;
                SendEvent(CmdZone, "STANDBY|" + Math.Max(0, availableZoneCount));
                return false;
            }

            int first = GetFirstAssignedZone();
            if (first < 0)
            {
                _state = ScannerState.Standby;
                _desiredZone = -1;
                _usingAnnouncedZone = false;
                SendEvent(CmdZone, "STANDBY|" + Math.Max(0, availableZoneCount));
                return false;
            }

            _usingAnnouncedZone = IsZoneAssignedToWorker(_announcedZone);
            _desiredZone = _usingAnnouncedZone ? _announcedZone : first;
            _zonePlanReady = true;
            SendEvent(CmdZone, "SCANNING");
            return true;
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
            if (!_zonePlanReady && !PrepareZonePlan())
                return;

            NormalizeDesiredZone();
            if (GClass20.int_39 == _desiredZone)
            {
                if (_arrivedZone != _desiredZone)
                {
                    _arrivedZone = _desiredZone;
                    _arrivedAt = now;
                    _zoneAttempts = 0;
                    SendEvent(CmdZone, "SCANNING");
                }

                if (now - _arrivedAt >= 900L)
                {
                    AdvanceScanZone();
                    _arrivedZone = -1;
                    _arrivedAt = 0L;
                }
                return;
            }

            _arrivedZone = -1;
            if (now - _lastZoneCommandAt < 1200L)
                return;

            if (_zoneAttempts >= 2)
            {
                AdvanceScanZone();
                _zoneAttempts = 0;
                return;
            }

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
                TrackRouteProgress(now, ref _rallyRouteLastMapId, ref _rallyRouteLastProgressAt);

                bool xmapRunning = GClass148.smethod_0().bool_0;
                if (xmapRunning && now - _rallyRouteLastProgressAt < RouteStallTimeoutMs)
                    return;

                if (xmapRunning)
                {
                    Class21.smethod_0().method_9();
                    _lastRouteCommandAt = 0L;
                    _rallyRouteLastProgressAt = now;
                }

                if (now - _lastRouteCommandAt >= RouteRetryDelayMs)
                {
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

                GClass7.smethod_0().method_42(_targetZone, -1);
                _rallyZoneAttempts++;
                _lastZoneCommandAt = now;
                return;
            }

            if (_rallyZoneArrivedAt <= 0L)
            {
                _rallyZoneArrivedAt = now;
                _rallyZoneAttempts = 0;
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
            EnableAndFocus(target, now);
            if (!_readyReported)
            {
                _readyReported = true;
                SendEvent(CmdReady, "Đã tới boss");
            }
        }

        private void UpdatePinnedFight(long now)
        {
            GClass78 target = FindTargetBoss();
            if (target == null)
            {
                if (_targetMissingSince <= 0L)
                    _targetMissingSince = now;
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
            if (!_active || string.IsNullOrEmpty(message))
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
                        SetScanLocation(announcedMapId, announcedMap, announcedZone, GClass203.smethod_18());
                    }
                }
            }
        }

        private void ClearAnnouncements()
        {
            lock (_announcementLock)
                _pendingAnnouncements.Clear();
        }

        private void MoveToNextAssignedZone()
        {
            int first = GetFirstAssignedZone();
            if (first < 0)
            {
                _state = ScannerState.Standby;
                _desiredZone = -1;
                _usingAnnouncedZone = false;
                SendEvent(CmdZone, "STANDBY|" + Math.Max(0, GetAvailableZoneCount()));
                return;
            }

            int next = _desiredZone + _workerCount;
            _desiredZone = next > _maxZone ? first : next;
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
            int effectiveStart = GetEffectiveStartZone();
            int count = _maxZone - effectiveStart + 1;
            if (count <= 0)
            {
                effectiveStart = 0;
                count = _maxZone + 1;
            }
            return Math.Max(0, count);
        }

        private int GetFirstAssignedZone()
        {
            int count = GetAvailableZoneCount();
            if (count <= 0 || _workerIndex >= count)
                return -1;
            return GetEffectiveStartZone() + _workerIndex;
        }

        private bool IsZoneAssignedToWorker(int zone)
        {
            if (zone < 0 || _workerCount <= 0)
                return false;

            int count = GetAvailableZoneCount();
            if (count <= 0 || _workerIndex >= count)
                return false;

            int effectiveStart = GetEffectiveStartZone();
            int offset = zone - effectiveStart;
            if (offset < 0 || offset >= count)
                return false;

            return offset % _workerCount == _workerIndex;
        }

        private void NormalizeDesiredZone()
        {
            if (_maxZone < 0)
                return;
            if (_desiredZone < 0 || _desiredZone > _maxZone)
                _desiredZone = GetFirstAssignedZone();
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
            SendEvent(CmdDead, detail);
            StopInternal();
        }

        private void ReportFailed(string detail)
        {
            if (!_active)
                return;
            SendEvent(CmdFailed, detail);
            StopInternal();
        }

        private void SendEvent(int cmd, string detail)
        {
            try
            {
                BossHuntPayload payload = new BossHuntPayload
                {
                    sessionId = _sessionId,
                    bossName = _bossName,
                    startZone = _startZone,
                    workerIndex = _workerIndex,
                    workerCount = _workerCount,
                    mapId = GClass20.int_37,
                    mapName = GClass20.string_1,
                    zone = GClass20.int_39,
                    accountId = GClass150.int_0,
                    detail = detail ?? ""
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

        private void StopInternal()
        {
            string oldBoss = _bossName;
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
            _scanRouteLastMapId = -1;
            _scanRouteLastProgressAt = 0L;
            _rallyRouteLastMapId = -1;
            _rallyRouteLastProgressAt = 0L;
            ClearAnnouncements();
            _lastFocusAt = 0L;
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
            return char.IsWhiteSpace(next) || char.IsDigit(next) || next == '-' || next == '(' || next == '[';
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
