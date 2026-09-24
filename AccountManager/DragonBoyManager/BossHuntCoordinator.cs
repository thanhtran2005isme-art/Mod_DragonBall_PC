using System;
using System.Collections.Generic;
using System.Text;
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

    public sealed class BossHuntWorkerSnapshot
    {
        public int AccountId;
        public string Username;
        public int Zone = -1;
        public int MapId = -1;
        public string MapName = "";
        public string Status = "";
    }

    public sealed class BossHuntSnapshot
    {
        public int SessionId;
        public string BossName = "";
        public int StartZone;
        public BossHuntState State;
        public int FoundMapId = -1;
        public string FoundMapName = "";
        public int FoundZone = -1;
        public int FinderAccountId = -1;
        public string StopReason = "";
        public List<BossHuntWorkerSnapshot> Workers = new List<BossHuntWorkerSnapshot>();
    }

    internal sealed class BossHuntPayload
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

    public sealed class BossHuntCoordinator
    {
        public const int CmdStartScan = 100;
        public const int CmdStop = 101;
        public const int CmdRally = 102;
        public const int CmdZone = 110;
        public const int CmdFound = 111;
        public const int CmdDead = 112;
        public const int CmdReady = 113;

        private static readonly BossHuntCoordinator _instance = new BossHuntCoordinator();
        private readonly object _sync = new object();
        private readonly Dictionary<int, BossHuntWorkerSnapshot> _workers = new Dictionary<int, BossHuntWorkerSnapshot>();
        private readonly List<Account> _sessionAccounts = new List<Account>();

        private int _sessionSeed;
        private int _sessionId;
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
        }

        public int GetConnectedCount()
        {
            return GetConnectedAccounts().Count;
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
            lock (_sync)
            {
                if (_state == BossHuntState.Scanning || _state == BossHuntState.Rallying || _state == BossHuntState.Fighting)
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
                sessionId = _sessionId;
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
                for (int i = 0; i < accounts.Count; i++)
                {
                    Account account = accounts[i];
                    _sessionAccounts.Add(account);
                    _workers[account.ID] = new BossHuntWorkerSnapshot
                    {
                        AccountId = account.ID,
                        Username = account.Username ?? "",
                        Status = MainController.language == 0 ? "Chuẩn bị dò" : "Preparing"
                    };
                }
            }

            SendScanAssignments(accounts, sessionId, bossName, Math.Max(0, startZone));
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
                foreach (BossHuntWorkerSnapshot worker in _workers.Values)
                {
                    if (worker.Status != (MainController.language == 0 ? "Mất kết nối" : "Disconnected"))
                        worker.Status = MainController.language == 0 ? "Đã dừng" : "Stopped";
                }
            }

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

            if (cmd == CmdDead)
            {
                bool valid;
                lock (_sync)
                    valid = IsCurrentLocked(payload) && BossMatches(payload.bossName, _bossName);
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
                        worker.Zone = payload.zone;
                        worker.MapId = payload.mapId;
                        worker.MapName = payload.mapName ?? "";
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
                        worker.Zone = payload.zone;
                        worker.MapId = payload.mapId;
                        worker.MapName = payload.mapName ?? "";
                        worker.Status = MainController.language == 0 ? "Đã tới - đang đánh" : "Ready - fighting";
                    }
                    if (AllConnectedWorkersReadyLocked())
                        _state = BossHuntState.Fighting;
                }
                Publish();
            }
        }

        public void HandleDisconnected(Account account)
        {
            if (account == null)
                return;

            List<Account> reassign = null;
            int sessionId = 0;
            string boss = "";
            int startZone = 0;

            lock (_sync)
            {
                BossHuntWorkerSnapshot worker;
                if (!_workers.TryGetValue(account.ID, out worker))
                    return;

                worker.Status = MainController.language == 0 ? "Mất kết nối" : "Disconnected";

                int connectedCount = 0;
                for (int i = 0; i < _sessionAccounts.Count; i++)
                {
                    if (IsConnected(_sessionAccounts[i]))
                        connectedCount++;
                }

                if ((_state == BossHuntState.Scanning || _state == BossHuntState.Rallying || _state == BossHuntState.Fighting) && connectedCount == 0)
                {
                    _state = BossHuntState.Stopped;
                    _stopReason = MainController.language == 0 ? "Tất cả tài khoản đã mất kết nối" : "All accounts disconnected";
                }
                else if (_state == BossHuntState.Scanning)
                {
                    reassign = new List<Account>();
                    for (int i = 0; i < _sessionAccounts.Count; i++)
                    {
                        if (IsConnected(_sessionAccounts[i]))
                            reassign.Add(_sessionAccounts[i]);
                    }

                    if (reassign.Count > 0)
                    {
                        _sessionAccounts.Clear();
                        _sessionAccounts.AddRange(reassign);
                        sessionId = _sessionId;
                        boss = _bossName;
                        startZone = _startZone;
                        for (int i = 0; i < reassign.Count; i++)
                        {
                            BossHuntWorkerSnapshot activeWorker;
                            if (_workers.TryGetValue(reassign[i].ID, out activeWorker))
                                activeWorker.Status = MainController.language == 0 ? "Phân lại khu" : "Reassigning";
                        }
                    }
                }
            }

            if (reassign != null && reassign.Count > 0)
                SendScanAssignments(reassign, sessionId, boss, startZone);
            Publish();
        }

        public BossHuntSnapshot GetSnapshot()
        {
            lock (_sync)
            {
                BossHuntSnapshot snapshot = new BossHuntSnapshot
                {
                    SessionId = _sessionId,
                    BossName = _bossName,
                    StartZone = _startZone,
                    State = _state,
                    FoundMapId = _foundMapId,
                    FoundMapName = _foundMapName,
                    FoundZone = _foundZone,
                    FinderAccountId = _finderAccountId,
                    StopReason = _stopReason
                };
                foreach (BossHuntWorkerSnapshot worker in _workers.Values)
                {
                    snapshot.Workers.Add(new BossHuntWorkerSnapshot
                    {
                        AccountId = worker.AccountId,
                        Username = worker.Username,
                        Zone = worker.Zone,
                        MapId = worker.MapId,
                        MapName = worker.MapName,
                        Status = worker.Status
                    });
                }
                snapshot.Workers.Sort(delegate(BossHuntWorkerSnapshot a, BossHuntWorkerSnapshot b) { return a.AccountId.CompareTo(b.AccountId); });
                return snapshot;
            }
        }

        private void HandleFound(Account account, BossHuntPayload payload)
        {
            List<Account> targets;
            BossHuntPayload rally;
            lock (_sync)
            {
                if (!IsCurrentLocked(payload) || _state != BossHuntState.Scanning || !BossMatches(payload.bossName, _bossName))
                    return;

                _state = BossHuntState.Rallying;
                _foundMapId = payload.mapId;
                _foundMapName = payload.mapName ?? "";
                _foundZone = payload.zone;
                _finderAccountId = account.ID;
                BossHuntWorkerSnapshot finder;
                if (_workers.TryGetValue(account.ID, out finder))
                {
                    finder.Zone = payload.zone;
                    finder.MapId = payload.mapId;
                    finder.MapName = payload.mapName ?? "";
                    finder.Status = MainController.language == 0 ? "Đã tìm thấy boss" : "Boss found";
                }

                targets = new List<Account>(_sessionAccounts);
                for (int i = 0; i < targets.Count; i++)
                {
                    BossHuntWorkerSnapshot worker;
                    if (targets[i].ID != account.ID && _workers.TryGetValue(targets[i].ID, out worker) && IsConnected(targets[i]))
                        worker.Status = MainController.language == 0 ? "Đang tới boss" : "Rallying";
                }
                rally = CreatePayload();
            }

            Broadcast(targets, CmdRally, rally);
            Publish();
        }

        private void SendScanAssignments(List<Account> accounts, int sessionId, string bossName, int startZone)
        {
            int count = accounts.Count;
            for (int i = 0; i < count; i++)
            {
                Send(accounts[i], CmdStartScan, new BossHuntPayload
                {
                    sessionId = sessionId,
                    bossName = bossName,
                    startZone = startZone,
                    workerIndex = i,
                    workerCount = count,
                    accountId = accounts[i].ID
                });
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

        private BossHuntPayload CreatePayload()
        {
            return new BossHuntPayload
            {
                sessionId = _sessionId,
                bossName = _bossName,
                startZone = _startZone,
                mapId = _foundMapId,
                mapName = _foundMapName,
                zone = _foundZone,
                accountId = _finderAccountId
            };
        }

        private bool IsCurrentLocked(BossHuntPayload payload)
        {
            return payload.sessionId == _sessionId && _sessionId > 0 && _state != BossHuntState.Idle && _state != BossHuntState.Stopped;
        }

        private bool AllConnectedWorkersReadyLocked()
        {
            bool any = false;
            for (int i = 0; i < _sessionAccounts.Count; i++)
            {
                Account account = _sessionAccounts[i];
                if (!IsConnected(account))
                    continue;
                any = true;
                BossHuntWorkerSnapshot worker;
                if (!_workers.TryGetValue(account.ID, out worker))
                    return false;
                if (worker.Status != (MainController.language == 0 ? "Đã tới - đang đánh" : "Ready - fighting") &&
                    worker.Status != (MainController.language == 0 ? "Đã tìm thấy boss" : "Boss found"))
                    return false;
            }
            return any;
        }

        private static bool BossMatches(string value, string target)
        {
            value = (value ?? "").Trim();
            target = (target ?? "").Trim();
            if (value.Length == 0 || target.Length == 0)
                return false;
            return value.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   target.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
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

        private void Publish()
        {
            Action<BossHuntSnapshot> handler = Changed;
            if (handler != null)
                handler(GetSnapshot());
        }
    }
}
