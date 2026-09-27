using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace DragonBoyManager
{
    public sealed class TabBossHunt : UserControl
    {
        public static TabBossHunt instance;

        private readonly ComboBox comboBoss = new ComboBox();
        private readonly NumericUpDown numericStartZone = new NumericUpDown();
        private readonly Button buttonStart = new Button();
        private readonly Button buttonStop = new Button();
        private readonly Button buttonLog = new Button();
        private readonly Label labelBoss = new Label();
        private readonly Label labelStartZone = new Label();
        private readonly Label labelConnected = new Label();
        private readonly Label labelState = new Label();
        private readonly Label labelResult = new Label();
        private readonly DataGridView grid = new DataGridView();
        private readonly TextBox timeline = new TextBox();
        private readonly Timer timer = new Timer();
        private int catalogRevision = -1;

        public TabBossHunt()
        {
            instance = this;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(64, 64, 64);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            BuildUi();
            RefreshBossCatalog();
            BossHuntCoordinator.Instance.Changed += Coordinator_Changed;
            timer.Interval = 500;
            timer.Tick += delegate
            {
                if (catalogRevision != BossHuntCatalog.Revision)
                    RefreshBossCatalog();
                RefreshConnectedCount();
                ApplySnapshot(BossHuntCoordinator.Instance.GetSnapshot());
            };
            timer.Start();
            ApplySnapshot(BossHuntCoordinator.Instance.GetSnapshot());
            RefreshConnectedCount();
            loadLanguage();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Stop();
                BossHuntCoordinator.Instance.Changed -= Coordinator_Changed;
            }
            base.Dispose(disposing);
        }

        public void loadLanguage()
        {
            bool vi = MainController.language == 0;
            labelBoss.Text = vi ? "Boss cần săn:" : "Target boss:";
            labelStartZone.Text = vi ? "Khu bắt đầu:" : "Start zone:";
            buttonStart.Text = vi ? "BẮT ĐẦU DÒ" : "START SCAN";
            buttonStop.Text = vi ? "DỪNG" : "STOP";
            buttonLog.Text = vi ? "LOG" : "LOG";

            string[] headers = vi
                ? new string[] { "ID", "Tài khoản", "Worker", "Map / Khu", "Được giao", "Đã dò", "Vòng", "Hiệu suất", "HP", "Trạng thái" }
                : new string[] { "ID", "Account", "Worker", "Map / Zone", "Assigned", "Scanned", "Cycle", "Performance", "HP", "Status" };
            for (int i = 0; i < headers.Length && i < grid.Columns.Count; i++)
                grid.Columns[i].HeaderText = headers[i];

            RefreshConnectedCount();
            ApplySnapshot(BossHuntCoordinator.Instance.GetSnapshot());
        }

        private void BuildUi()
        {
            labelBoss.SetBounds(14, 18, 86, 24);
            comboBoss.SetBounds(100, 15, 175, 26);
            comboBoss.DropDownStyle = ComboBoxStyle.DropDown;
            comboBoss.Text = "Super Broly";
            comboBoss.TextChanged += delegate { ApplySnapshot(BossHuntCoordinator.Instance.GetSnapshot()); };
            comboBoss.DropDown += delegate { RefreshBossCatalog(); };

            labelStartZone.SetBounds(285, 18, 85, 24);
            numericStartZone.SetBounds(370, 15, 55, 26);
            numericStartZone.Minimum = 0;
            numericStartZone.Maximum = 99;
            numericStartZone.Value = 0;

            buttonStart.SetBounds(440, 13, 115, 30);
            buttonStart.Click += buttonStart_Click;
            buttonStop.SetBounds(565, 13, 80, 30);
            buttonStop.Click += delegate
            {
                BossHuntCoordinator.Instance.Stop(MainController.language == 0 ? "Người dùng dừng" : "Stopped by user");
            };

            buttonLog.SetBounds(650, 13, 74, 30);
            buttonLog.Click += delegate
            {
                try
                {
                    BossHuntLogViewer viewer = new BossHuntLogViewer();
                    viewer.Show(this);
                }
                catch
                {
                }
            };

            labelConnected.SetBounds(14, 52, 245, 24);
            labelState.SetBounds(270, 52, 450, 24);
            labelState.Font = new Font(labelState.Font, FontStyle.Bold);

            grid.SetBounds(14, 82, 710, 135);
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.AutoGenerateColumns = false;
            grid.BackgroundColor = Color.FromArgb(45, 45, 45);
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.MultiSelect = false;
            grid.ReadOnly = true;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.DefaultCellStyle.BackColor = Color.FromArgb(55, 55, 55);
            grid.DefaultCellStyle.ForeColor = Color.White;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(90, 90, 90);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.RowTemplate.Height = 28;
            grid.ScrollBars = ScrollBars.Vertical;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            float[] fillWeights = new float[] { 5F, 14F, 9F, 18F, 14F, 14F, 6F, 11F, 7F, 20F };
            int[] minimumWidths = new int[] { 32, 72, 52, 92, 72, 72, 38, 68, 44, 100 };
            for (int i = 0; i < fillWeights.Length; i++)
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    FillWeight = fillWeights[i],
                    MinimumWidth = minimumWidths[i]
                });
            }

            GroupBox resultBox = new GroupBox();
            resultBox.Text = "Boss";
            resultBox.ForeColor = Color.White;
            resultBox.SetBounds(14, 223, 710, 82);
            labelResult.Dock = DockStyle.Fill;
            labelResult.Padding = new Padding(8, 4, 8, 4);
            labelResult.AutoEllipsis = true;
            labelResult.Font = new Font("Segoe UI", 8.25F, FontStyle.Regular);
            resultBox.Controls.Add(labelResult);

            GroupBox timelineBox = new GroupBox();
            timelineBox.Text = "Timeline";
            timelineBox.ForeColor = Color.White;
            timelineBox.SetBounds(14, 311, 710, 74);
            timeline.Dock = DockStyle.Fill;
            timeline.Multiline = true;
            timeline.ReadOnly = true;
            timeline.ScrollBars = ScrollBars.Vertical;
            timeline.WordWrap = false;
            timeline.BackColor = Color.FromArgb(45, 45, 45);
            timeline.ForeColor = Color.White;
            timeline.BorderStyle = BorderStyle.None;
            timeline.Font = new Font("Consolas", 8F, FontStyle.Regular);
            timelineBox.Controls.Add(timeline);

            Controls.Add(labelBoss);
            Controls.Add(comboBoss);
            Controls.Add(labelStartZone);
            Controls.Add(numericStartZone);
            Controls.Add(buttonStart);
            Controls.Add(buttonStop);
            Controls.Add(buttonLog);
            Controls.Add(labelConnected);
            Controls.Add(labelState);
            Controls.Add(grid);
            Controls.Add(resultBox);
            Controls.Add(timelineBox);
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            BossHuntSnapshot snapshot = BossHuntCoordinator.Instance.GetSnapshot();
            if (snapshot.State == BossHuntState.Scanning || snapshot.State == BossHuntState.Rallying || snapshot.State == BossHuntState.Fighting)
                return;

            BossHuntCatalog.RememberBoss(comboBoss.Text);
            RefreshBossCatalog();

            string error;
            if (!BossHuntCoordinator.Instance.Start(comboBoss.Text, (int)numericStartZone.Value, out error))
                MessageBox.Show(error, MainController.language == 0 ? "Săn Boss" : "Boss Hunt", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Coordinator_Changed(BossHuntSnapshot snapshot)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action<BossHuntSnapshot>(ApplySnapshot), snapshot);
                }
                catch
                {
                }
                return;
            }
            ApplySnapshot(snapshot);
        }

        private void ApplySnapshot(BossHuntSnapshot snapshot)
        {
            if (snapshot == null || IsDisposed)
                return;

            bool running = snapshot.State == BossHuntState.Scanning || snapshot.State == BossHuntState.Rallying || snapshot.State == BossHuntState.Fighting;
            buttonStart.Enabled = !running;
            buttonStop.Enabled = running;
            comboBoss.Enabled = !running;
            numericStartZone.Enabled = !running;

            grid.Rows.Clear();
            for (int i = 0; i < snapshot.Workers.Count; i++)
            {
                BossHuntWorkerSnapshot worker = snapshot.Workers[i];
                string mapText = worker.MapId < 0
                    ? "-"
                    : (string.IsNullOrEmpty(worker.MapName) ? ("#" + worker.MapId) : worker.MapName + " (#" + worker.MapId + ")");
                string workerText = worker.WorkerIndex < 0
                    ? "-"
                    : "W" + (worker.WorkerIndex + 1) + "/" + Math.Max(1, worker.WorkerCount);
                string status = worker.Status ?? "";
                if (!string.IsNullOrEmpty(worker.DuplicateWarning))
                    status = "⚠ " + worker.DuplicateWarning + " | " + status;
                if (!string.IsNullOrEmpty(worker.LastAction) && status.IndexOf(worker.LastAction, StringComparison.OrdinalIgnoreCase) < 0)
                    status += " | " + worker.LastAction;

                string mapZoneText = mapText;
                if (worker.Zone >= 0)
                    mapZoneText += " / K" + worker.Zone;

                int rowIndex = grid.Rows.Add(
                    worker.AccountId,
                    worker.Username,
                    (worker.AssignmentGeneration > 0 ? "G" + worker.AssignmentGeneration + " " : "") + workerText,
                    mapZoneText,
                    string.IsNullOrEmpty(worker.AssignedZones) ? "-" : worker.AssignedZones,
                    GetScannedText(worker),
                    worker.ScanCycle > 0 ? worker.ScanCycle.ToString() : "-",
                    GetPerformanceText(worker),
                    worker.TargetHp >= 0 ? FormatNumber(worker.TargetHp) : "-",
                    status);

                string detail =
                    "ID: " + worker.AccountId + Environment.NewLine +
                    "Account: " + (worker.Username ?? "") + Environment.NewLine +
                    "Generation: " + worker.AssignmentGeneration + Environment.NewLine +
                    "Worker: " + workerText + Environment.NewLine +
                    "Map: " + mapText + Environment.NewLine +
                    "Zone: " + (worker.Zone < 0 ? "-" : "K" + worker.Zone) + Environment.NewLine +
                    "Assigned: " + (string.IsNullOrEmpty(worker.AssignedZones) ? "-" : worker.AssignedZones) + Environment.NewLine +
                    "Scanned: " + GetScannedText(worker) + Environment.NewLine +
                    "Cycle: " + (worker.ScanCycle > 0 ? worker.ScanCycle.ToString() : "-") + Environment.NewLine +
                    "Performance: " + GetPerformanceText(worker) + Environment.NewLine +
                    "Entity: " + (worker.EntityCount >= 0 ? worker.EntityCount.ToString() : "-") + Environment.NewLine +
                    "Boss entities: " + (worker.BossCount >= 0 ? worker.BossCount.ToString() : "-") + Environment.NewLine +
                    "Target HP: " + (worker.TargetHp >= 0 ? FormatNumber(worker.TargetHp) : "-") + Environment.NewLine +
                    "Zone age: " + GetZoneAge(worker) + Environment.NewLine +
                    "Zone failures: " + GetZoneFailureText(worker) + Environment.NewLine +
                    "Last signal: " + GetSignalText(worker) + Environment.NewLine +
                    "Status: " + status;

                DataGridViewRow row = grid.Rows[rowIndex];
                for (int cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
                    row.Cells[cellIndex].ToolTipText = detail;
            }

            labelState.Text = GetStateText(snapshot);
            UpdateBossInfo(snapshot);
            UpdateTimeline(snapshot);
        }

        private void UpdateBossInfo(BossHuntSnapshot snapshot)
        {
            string selectedBoss = snapshot.State == BossHuntState.Idle || snapshot.State == BossHuntState.Stopped
                ? comboBoss.Text
                : snapshot.BossName;
            if (string.IsNullOrEmpty(selectedBoss))
                selectedBoss = comboBoss.Text;

            BossHuntBossSnapshot boss = snapshot.TargetBoss;
            if (boss == null || !BossNamesCompatible(boss.BossName, selectedBoss))
                boss = BossHuntCoordinator.Instance.GetBossInfo(selectedBoss);

            bool vi = MainController.language == 0;
            if (boss == null)
            {
                labelResult.Text =
                    "Boss: " + (string.IsNullOrEmpty(selectedBoss) ? "-" : selectedBoss) + " | " + (vi ? "UNKNOWN" : "UNKNOWN") + Environment.NewLine +
                    (vi ? "Chưa có announcement hợp lệ trong Manager." : "No valid announcement in Manager.") + Environment.NewLine +
                    (vi ? "Coverage: " : "Coverage: ") + snapshot.UniqueCoverageCount + "/" + (snapshot.CoverageTotalZones > 0 ? snapshot.CoverageTotalZones.ToString() : "?");
                return;
            }

            string presence = GetPresenceText(boss.Presence, vi);
            string spawn = FormatExactTime(boss.SpawnedAtUtc);
            string death = FormatExactTime(boss.DiedAtUtc);
            string map = boss.MapId < 0
                ? "-"
                : (string.IsNullOrEmpty(boss.MapName) ? "#" + boss.MapId : boss.MapName + " (#" + boss.MapId + ")");
            string sources = GetSourcesText(snapshot, boss.SourceAccounts);
            string killer = string.IsNullOrEmpty(boss.Killer) ? (vi ? "Không rõ" : "Unknown") : boss.Killer;
            if (!string.IsNullOrEmpty(boss.Killer) && boss.KillerId >= 0)
                killer += " (#" + boss.KillerId + ")";
            string age = GetBossAgeText(boss);
            string life = GetBossLifetimeText(boss);
            string finder = GetFinderText(snapshot);
            string raw = !string.IsNullOrEmpty(boss.RawDeath) && boss.Presence == BossPresenceState.Dead ? boss.RawDeath : boss.RawSpawn;
            raw = Truncate(raw, 118);

            labelResult.Text =
                "Boss: " + boss.BossName + " | " + presence + " | " + (vi ? "Tuổi cache: " : "Cache age: ") + age +
                " | Coverage " + snapshot.UniqueCoverageCount + "/" + (snapshot.CoverageTotalZones > 0 ? snapshot.CoverageTotalZones.ToString() : "?") + Environment.NewLine +
                (vi ? "Xuất hiện: " : "Spawn: ") + spawn + " | " + map + (boss.Zone >= 0 ? " K" + boss.Zone : "") +
                " | " + (vi ? "Nguồn: " : "Source: ") + sources + Environment.NewLine +
                (vi ? "Chết: " : "Death: ") + death + " | " + (vi ? "Sống: " : "Lifetime: ") + life +
                " | " + (vi ? "Người hạ: " : "Killer: ") + killer + " | Finder: " + finder + Environment.NewLine +
                "RAW: " + (string.IsNullOrEmpty(raw) ? "-" : raw);
        }

        private void UpdateTimeline(BossHuntSnapshot snapshot)
        {
            if (snapshot.RecentTimeline == null || snapshot.RecentTimeline.Count == 0)
            {
                timeline.Text = MainController.language == 0 ? "Chưa có sự kiện trong phiên." : "No session events yet.";
                return;
            }

            StringBuilder builder = new StringBuilder();
            int start = Math.Max(0, snapshot.RecentTimeline.Count - 12);
            for (int i = start; i < snapshot.RecentTimeline.Count; i++)
            {
                BossHuntTimelineEntry item = snapshot.RecentTimeline[i];
                builder.Append(item.AtUtc == DateTime.MinValue ? "--:--:--.---" : item.AtUtc.ToLocalTime().ToString("HH:mm:ss.fff"));
                builder.Append(" ");
                if (item.AccountId > 0)
                {
                    builder.Append("[#");
                    builder.Append(item.AccountId);
                    builder.Append("] ");
                }
                builder.Append(item.EventName);
                if (!string.IsNullOrEmpty(item.Detail))
                {
                    builder.Append(" | ");
                    builder.Append(Truncate(item.Detail, 150));
                }
                if (i < snapshot.RecentTimeline.Count - 1)
                    builder.Append(Environment.NewLine);
            }
            timeline.Text = builder.ToString();
            timeline.SelectionStart = timeline.TextLength;
            timeline.ScrollToCaret();
        }

        private string GetStateText(BossHuntSnapshot snapshot)
        {
            string boss = string.IsNullOrEmpty(snapshot.BossName) ? "-" : snapshot.BossName;
            string coverage = " | Coverage " + snapshot.UniqueCoverageCount + "/" +
                              (snapshot.CoverageTotalZones > 0 ? snapshot.CoverageTotalZones.ToString() : "?");
            string suffix = snapshot.SessionId > 0
                ? " | S#" + snapshot.SessionId + " G" + snapshot.AssignmentGeneration + coverage
                : "";

            if (MainController.language == 0)
            {
                switch (snapshot.State)
                {
                    case BossHuntState.Scanning: return "Trạng thái: Đang dò " + boss + suffix;
                    case BossHuntState.Rallying: return "Trạng thái: Đã thấy boss - đang tập trung" + suffix;
                    case BossHuntState.Fighting: return "Trạng thái: Đang đánh " + boss + suffix;
                    case BossHuntState.Stopped: return "Trạng thái: Đã dừng" + suffix;
                    default: return "Trạng thái: Chưa chạy";
                }
            }

            switch (snapshot.State)
            {
                case BossHuntState.Scanning: return "Status: Scanning " + boss + suffix;
                case BossHuntState.Rallying: return "Status: Boss found - rallying" + suffix;
                case BossHuntState.Fighting: return "Status: Fighting " + boss + suffix;
                case BossHuntState.Stopped: return "Status: Stopped" + suffix;
                default: return "Status: Idle";
            }
        }

        private void RefreshBossCatalog()
        {
            string current = comboBoss.Text;
            List<string> names = BossHuntCatalog.GetBossNames();
            comboBoss.BeginUpdate();
            try
            {
                comboBoss.Items.Clear();
                for (int i = 0; i < names.Count; i++)
                    comboBoss.Items.Add(names[i]);
            }
            finally
            {
                comboBoss.EndUpdate();
            }
            comboBoss.Text = current;
            catalogRevision = BossHuntCatalog.Revision;
        }

        private static string GetPerformanceText(BossHuntWorkerSnapshot worker)
        {
            double minutes = 0.0;
            if (worker.ScanStartedAtUtc != DateTime.MinValue)
                minutes = DateTime.UtcNow.Subtract(worker.ScanStartedAtUtc).TotalMinutes;
            double perMinute = minutes > 0.05 ? worker.ZoneClearCount / minutes : 0.0;
            return worker.ZoneClearCount + " khu | " + perMinute.ToString("0.0") + "/m | F" +
                   worker.FailureCount + " | TO" + worker.TimeoutCount;
        }

        private static string GetScannedText(BossHuntWorkerSnapshot worker)
        {
            if (worker.ScannedZones == null || worker.ScannedZones.Count == 0)
                return "-";

            int start = Math.Max(0, worker.ScannedZones.Count - 8);
            List<string> values = new List<string>();
            for (int i = start; i < worker.ScannedZones.Count; i++)
            {
                string token = worker.ScannedZones[i] ?? "";
                int index = token.LastIndexOf(":K", StringComparison.Ordinal);
                values.Add(index >= 0 ? "K" + token.Substring(index + 2) : token);
            }
            return string.Join(",", values.ToArray());
        }

        private static string GetSignalText(BossHuntWorkerSnapshot worker)
        {
            return "E:" + FormatAge(worker.LastEventUtc) + " H:" + FormatAge(worker.LastHeartbeatUtc);
        }

        private static string GetZoneAge(BossHuntWorkerSnapshot worker)
        {
            if (worker.ZoneEnteredAtUtc == DateTime.MinValue)
                return "-";
            double seconds = DateTime.UtcNow.Subtract(worker.ZoneEnteredAtUtc).TotalSeconds;
            if (seconds < 0)
                seconds = 0;
            return seconds.ToString("0.0") + "s";
        }

        private static string GetZoneFailureText(BossHuntWorkerSnapshot worker)
        {
            if (worker.ZoneFailureCount <= 0)
                return "0";
            if (string.IsNullOrEmpty(worker.LastZoneFailure))
                return worker.ZoneFailureCount.ToString();
            return worker.ZoneFailureCount + " | " + worker.LastZoneFailure;
        }

        private static string FormatAge(DateTime utc)
        {
            if (utc == DateTime.MinValue)
                return "-";
            double seconds = DateTime.UtcNow.Subtract(utc).TotalSeconds;
            if (seconds < 0)
                seconds = 0;
            if (seconds < 60)
                return ((int)seconds) + "s";
            if (seconds < 3600)
                return ((int)(seconds / 60)) + "m";
            return ((int)(seconds / 3600)) + "h";
        }

        private static string FormatExactTime(DateTime utc)
        {
            if (utc == DateTime.MinValue)
                return "-";
            return utc.ToLocalTime().ToString("dd/MM HH:mm:ss.fff");
        }

        private static string GetBossAgeText(BossHuntBossSnapshot boss)
        {
            if (boss == null || boss.SpawnedAtUtc == DateTime.MinValue)
                return "-";
            return FormatDuration(DateTime.UtcNow.Subtract(boss.SpawnedAtUtc));
        }

        private static string GetBossLifetimeText(BossHuntBossSnapshot boss)
        {
            if (boss == null || boss.SpawnedAtUtc == DateTime.MinValue)
                return "-";
            DateTime end = boss.DiedAtUtc != DateTime.MinValue ? boss.DiedAtUtc : DateTime.UtcNow;
            TimeSpan span = end.Subtract(boss.SpawnedAtUtc);
            if (span.TotalSeconds < 0)
                return "-";
            return FormatDuration(span);
        }

        private static string FormatDuration(TimeSpan span)
        {
            if (span.TotalSeconds < 0)
                return "-";
            if (span.TotalHours >= 1)
                return ((int)span.TotalHours).ToString("00") + ":" + span.Minutes.ToString("00") + ":" + span.Seconds.ToString("00");
            return span.Minutes.ToString("00") + ":" + span.Seconds.ToString("00");
        }

        private static string GetPresenceText(BossPresenceState presence, bool vi)
        {
            switch (presence)
            {
                case BossPresenceState.Alive: return vi ? "ĐANG SỐNG" : "ALIVE";
                case BossPresenceState.Dead: return vi ? "ĐÃ CHẾT" : "DEAD";
                case BossPresenceState.Stale: return vi ? "STALE - VỊ TRÍ CŨ" : "STALE";
                default: return "UNKNOWN";
            }
        }

        private static string GetSourcesText(BossHuntSnapshot snapshot, List<int> sources)
        {
            if (sources == null || sources.Count == 0)
                return "-";
            List<string> values = new List<string>();
            for (int i = 0; i < sources.Count; i++)
            {
                int id = sources[i];
                string username = FindUsername(snapshot, id);
                values.Add(string.IsNullOrEmpty(username) ? ("#" + id) : (username + "(#" + id + ")"));
            }
            return string.Join(",", values.ToArray());
        }

        private static string GetFinderText(BossHuntSnapshot snapshot)
        {
            if (snapshot == null || snapshot.FinderAccountId <= 0)
                return "-";
            string username = snapshot.FinderUsername;
            return string.IsNullOrEmpty(username)
                ? "#" + snapshot.FinderAccountId
                : username + " (#" + snapshot.FinderAccountId + ")";
        }

        private static string FindUsername(BossHuntSnapshot snapshot, int accountId)
        {
            if (snapshot == null || snapshot.Workers == null)
                return "";
            for (int i = 0; i < snapshot.Workers.Count; i++)
            {
                if (snapshot.Workers[i].AccountId == accountId)
                    return snapshot.Workers[i].Username ?? "";
            }
            return "";
        }

        private static string FormatNumber(int value)
        {
            return value.ToString("N0");
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value ?? "";
            return value.Substring(0, maxLength - 3) + "...";
        }

        private static bool BossNamesCompatible(string value, string target)
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

        private static string NormalizeBossName(string value)
        {
            return (value ?? "").Trim().Trim('[', ']', ':', '-', '.', ' ');
        }

        private void RefreshConnectedCount()
        {
            int count = BossHuntCoordinator.Instance.GetConnectedCount();
            labelConnected.Text = MainController.language == 0
                ? "Tài khoản đang kết nối: " + count
                : "Connected accounts: " + count;
        }
    }
}
