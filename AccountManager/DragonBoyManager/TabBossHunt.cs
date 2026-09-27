using System;
using System.Collections.Generic;
using System.Drawing;
using System.Media;
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
        private readonly CheckBox checkSelectedOnly = new CheckBox();
        private readonly CheckBox checkSound = new CheckBox();
        private readonly Label labelBoss = new Label();
        private readonly Label labelStartZone = new Label();
        private readonly Label labelConnected = new Label();
        private readonly Label labelHealth = new Label();
        private readonly Label labelState = new Label();
        private readonly Label labelResult = new Label();
        private readonly DataGridView grid = new DataGridView();
        private readonly System.Windows.Forms.TabControl detailTabs = new System.Windows.Forms.TabControl();
        private readonly TabPage tabBossInfo = new TabPage();
        private readonly TabPage tabEvents = new TabPage();
        private readonly TabPage tabTimeline = new TabPage();
        private readonly TextBox eventsText = new TextBox();
        private readonly TextBox timeline = new TextBox();
        private readonly Timer timer = new Timer();
        private int catalogRevision = -1;
        private int lastSoundSessionId = -1;
        private BossHuntState lastSoundState = BossHuntState.Idle;
        private long lastDeathSoundTicks;

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
            checkSelectedOnly.Text = vi ? "Chỉ acc đã chọn" : "Selected only";
            checkSound.Text = vi ? "Âm" : "Sound";
            tabBossInfo.Text = vi ? "BOSS" : "BOSS";
            tabEvents.Text = vi ? "SỰ KIỆN" : "EVENTS";
            tabTimeline.Text = vi ? "TIMELINE" : "TIMELINE";

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
            comboBoss.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            comboBoss.AutoCompleteSource = AutoCompleteSource.ListItems;
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

            checkSelectedOnly.SetBounds(14, 50, 130, 24);
            checkSelectedOnly.AutoSize = false;
            labelConnected.SetBounds(150, 52, 170, 24);
            labelHealth.SetBounds(325, 52, 230, 24);
            checkSound.SetBounds(565, 50, 70, 24);
            checkSound.Checked = true;

            labelState.SetBounds(14, 74, 710, 22);
            labelState.Font = new Font(labelState.Font, FontStyle.Bold);
            labelState.AutoEllipsis = true;

            grid.SetBounds(14, 98, 710, 119);
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
            grid.CellDoubleClick += grid_CellDoubleClick;

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

            detailTabs.SetBounds(14, 223, 710, 162);
            detailTabs.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            tabBossInfo.BackColor = Color.FromArgb(64, 64, 64);
            tabBossInfo.ForeColor = Color.White;
            labelResult.Dock = DockStyle.Fill;
            labelResult.Padding = new Padding(8, 4, 8, 4);
            labelResult.AutoEllipsis = true;
            labelResult.Font = new Font("Segoe UI", 8.25F, FontStyle.Regular);
            tabBossInfo.Controls.Add(labelResult);

            tabEvents.BackColor = Color.FromArgb(64, 64, 64);
            tabEvents.ForeColor = Color.White;
            eventsText.Dock = DockStyle.Fill;
            eventsText.Multiline = true;
            eventsText.ReadOnly = true;
            eventsText.ScrollBars = ScrollBars.Vertical;
            eventsText.WordWrap = false;
            eventsText.BackColor = Color.FromArgb(45, 45, 45);
            eventsText.ForeColor = Color.White;
            eventsText.BorderStyle = BorderStyle.None;
            eventsText.Font = new Font("Consolas", 8F, FontStyle.Regular);
            tabEvents.Controls.Add(eventsText);

            tabTimeline.BackColor = Color.FromArgb(64, 64, 64);
            tabTimeline.ForeColor = Color.White;
            timeline.Dock = DockStyle.Fill;
            timeline.Multiline = true;
            timeline.ReadOnly = true;
            timeline.ScrollBars = ScrollBars.Vertical;
            timeline.WordWrap = false;
            timeline.BackColor = Color.FromArgb(45, 45, 45);
            timeline.ForeColor = Color.White;
            timeline.BorderStyle = BorderStyle.None;
            timeline.Font = new Font("Consolas", 8F, FontStyle.Regular);
            tabTimeline.Controls.Add(timeline);

            detailTabs.TabPages.Add(tabBossInfo);
            detailTabs.TabPages.Add(tabEvents);
            detailTabs.TabPages.Add(tabTimeline);

            Controls.Add(labelBoss);
            Controls.Add(comboBoss);
            Controls.Add(labelStartZone);
            Controls.Add(numericStartZone);
            Controls.Add(buttonStart);
            Controls.Add(buttonStop);
            Controls.Add(buttonLog);
            Controls.Add(checkSelectedOnly);
            Controls.Add(checkSound);
            Controls.Add(labelConnected);
            Controls.Add(labelHealth);
            Controls.Add(labelState);
            Controls.Add(grid);
            Controls.Add(detailTabs);
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            BossHuntSnapshot snapshot = BossHuntCoordinator.Instance.GetSnapshot();
            if (snapshot.State == BossHuntState.Scanning || snapshot.State == BossHuntState.Rallying || snapshot.State == BossHuntState.Fighting)
                return;

            BossHuntCatalog.RememberBoss(comboBoss.Text);
            RefreshBossCatalog();

            List<Account> requestedAccounts = null;
            if (checkSelectedOnly.Checked)
            {
                requestedAccounts = TabData._instance == null ? new List<Account>() : TabData._instance.GetAccountsSelected();
                if (requestedAccounts.Count == 0)
                {
                    MessageBox.Show(
                        MainController.language == 0
                            ? "Hãy chọn ít nhất một tài khoản ở tab ACCOUNT trước khi bắt đầu."
                            : "Select at least one account in the ACCOUNT tab before starting.",
                        MainController.language == 0 ? "Săn Boss" : "Boss Hunt",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
            }

            string error;
            if (!BossHuntCoordinator.Instance.Start(comboBoss.Text, (int)numericStartZone.Value, requestedAccounts, out error))
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
            checkSelectedOnly.Enabled = !running;

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
                string heartbeat = GetHeartbeatStatus(worker);
                if (!string.IsNullOrEmpty(heartbeat))
                    status = heartbeat + (string.IsNullOrEmpty(status) ? "" : " | " + status);
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
            labelHealth.Text = GetHealthText(snapshot);
            UpdateBossInfo(snapshot);
            UpdateBossEvents(snapshot);
            UpdateTimeline(snapshot);
            UpdateSounds(snapshot);
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
                    "Boss: " + (string.IsNullOrEmpty(selectedBoss) ? "-" : selectedBoss) + " | UNKNOWN" + Environment.NewLine +
                    (vi ? "Chưa có dữ liệu spawn/death hợp lệ cho boss này." : "No valid spawn/death data for this boss.") + Environment.NewLine +
                    "Coverage: " + snapshot.UniqueCoverageCount + "/" +
                    (snapshot.CoverageTotalZones > 0 ? snapshot.CoverageTotalZones.ToString() : "?") + Environment.NewLine +
                    GetUnparsedWarning(snapshot, vi);
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
            raw = Truncate(raw, 150);
            string evidence = FormatEvidence(boss.DeathEvidence, vi);
            string targetLock = boss.BossName + " @ " + map + (boss.Zone >= 0 ? " / K" + boss.Zone : "");

            labelResult.Text =
                "Boss: " + boss.BossName + " | " + presence + " | " + (vi ? "Tuổi cache: " : "Cache age: ") + age +
                " | Coverage " + snapshot.UniqueCoverageCount + "/" +
                (snapshot.CoverageTotalZones > 0 ? snapshot.CoverageTotalZones.ToString() : "?") + Environment.NewLine +
                (vi ? "Target lock: " : "Target lock: ") + targetLock +
                " | " + (vi ? "Xuất hiện: " : "Spawn: ") + spawn + Environment.NewLine +
                (vi ? "Chết: " : "Death: ") + death + " | " + (vi ? "Sống: " : "Lifetime: ") + life +
                " | " + (vi ? "Người hạ: " : "Killer: ") + killer +
                " | " + (vi ? "Xác nhận: " : "Evidence: ") + evidence + Environment.NewLine +
                (vi ? "Nguồn: " : "Sources: ") + sources + " | Finder: " + finder + Environment.NewLine +
                GetUnparsedWarning(snapshot, vi) +
                (string.IsNullOrEmpty(raw) ? "" : (GetUnparsedWarning(snapshot, vi).Length > 0 ? Environment.NewLine : "") + "RAW: " + raw);
        }

        private void UpdateBossEvents(BossHuntSnapshot snapshot)
        {
            if (snapshot.RecentBossEvents == null || snapshot.RecentBossEvents.Count == 0)
            {
                eventsText.Text = MainController.language == 0 ? "Chưa có sự kiện boss." : "No boss events yet.";
                return;
            }

            string selectedBoss = snapshot.State == BossHuntState.Idle || snapshot.State == BossHuntState.Stopped
                ? comboBoss.Text
                : snapshot.BossName;

            StringBuilder builder = new StringBuilder();
            int start = Math.Max(0, snapshot.RecentBossEvents.Count - 30);
            for (int i = start; i < snapshot.RecentBossEvents.Count; i++)
            {
                BossHuntBossEventSnapshot item = snapshot.RecentBossEvents[i];
                if (!string.IsNullOrEmpty(selectedBoss) && !BossNamesCompatible(item.BossName, selectedBoss))
                    continue;
                builder.Append(item.ObservedAtUtc == DateTime.MinValue ? "--:--:--.---" : item.ObservedAtUtc.ToLocalTime().ToString("HH:mm:ss.fff"));
                builder.Append(" ");
                builder.Append(item.EventType ?? "");
                builder.Append(" | ");
                builder.Append(item.BossName ?? "");
                if (item.MapId >= 0)
                {
                    builder.Append(" | ");
                    builder.Append(string.IsNullOrEmpty(item.MapName) ? ("#" + item.MapId) : item.MapName);
                    if (item.Zone >= 0)
                    {
                        builder.Append(" K");
                        builder.Append(item.Zone);
                    }
                }
                if (!string.IsNullOrEmpty(item.Killer))
                {
                    builder.Append(" | killer=");
                    builder.Append(item.Killer);
                    if (item.KillerId >= 0)
                    {
                        builder.Append("(#");
                        builder.Append(item.KillerId);
                        builder.Append(")");
                    }
                }
                if (!string.IsNullOrEmpty(item.DeathEvidence))
                {
                    builder.Append(" | ");
                    builder.Append(FormatEvidence(item.DeathEvidence, MainController.language == 0));
                }
                if (item.SourceAccountId > 0)
                {
                    builder.Append(" | acc#");
                    builder.Append(item.SourceAccountId);
                }
                if (i < snapshot.RecentBossEvents.Count - 1)
                    builder.Append(Environment.NewLine);
            }

            eventsText.Text = builder.Length == 0
                ? (MainController.language == 0 ? "Chưa có sự kiện cho boss đang chọn." : "No events for the selected boss.")
                : builder.ToString();
            eventsText.SelectionStart = eventsText.TextLength;
            eventsText.ScrollToCaret();
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
            if (!string.IsNullOrEmpty(snapshot.MissingZones) &&
                snapshot.CoverageTotalZones > 0 &&
                snapshot.UniqueCoverageCount < snapshot.CoverageTotalZones)
            {
                coverage += (MainController.language == 0 ? " | Thiếu " : " | Missing ") + snapshot.MissingZones;
            }
            if (snapshot.MinimumHealthyScanCycle > 0)
                coverage += (MainController.language == 0 ? " | Vòng min " : " | Min cycle ") + snapshot.MinimumHealthyScanCycle;

            string elapsed = snapshot.SessionStartedAtUtc == DateTime.MinValue
                ? ""
                : " | " + FormatDuration(DateTime.UtcNow.Subtract(snapshot.SessionStartedAtUtc));
            string suffix = snapshot.SessionId > 0
                ? " | S#" + snapshot.SessionId + " G" + snapshot.AssignmentGeneration + elapsed + coverage
                : "";

            if (MainController.language == 0)
            {
                switch (snapshot.State)
                {
                    case BossHuntState.Scanning: return "Trạng thái: Đang dò " + boss + suffix;
                    case BossHuntState.Rallying: return "Trạng thái: Đã thấy boss - đang tập trung" + suffix;
                    case BossHuntState.Fighting: return "Trạng thái: Đang đánh " + boss + suffix;
                    case BossHuntState.Stopped:
                        return "Trạng thái: Đã dừng" + suffix +
                               (string.IsNullOrEmpty(snapshot.StopReason) ? "" : " | Lý do: " + snapshot.StopReason);
                    default: return "Trạng thái: Chưa chạy";
                }
            }

            switch (snapshot.State)
            {
                case BossHuntState.Scanning: return "Status: Scanning " + boss + suffix;
                case BossHuntState.Rallying: return "Status: Boss found - rallying" + suffix;
                case BossHuntState.Fighting: return "Status: Fighting " + boss + suffix;
                case BossHuntState.Stopped:
                    return "Status: Stopped" + suffix +
                           (string.IsNullOrEmpty(snapshot.StopReason) ? "" : " | Reason: " + snapshot.StopReason);
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
            string unit = MainController.language == 0 ? " khu" : " zones";
            return worker.ZoneClearCount + unit + " | " + perMinute.ToString("0.0") + "/m | F" +
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

        private static string GetHealthText(BossHuntSnapshot snapshot)
        {
            bool vi = MainController.language == 0;
            bool running = snapshot.State == BossHuntState.Scanning ||
                           snapshot.State == BossHuntState.Rallying ||
                           snapshot.State == BossHuntState.Fighting;
            string heartbeat = !running || snapshot.WorstHeartbeatAgeSeconds < 0.0
                ? "-"
                : snapshot.WorstHeartbeatAgeSeconds.ToString("0.0") + "s";
            return (vi ? "Worker khỏe: " : "Healthy: ") +
                   snapshot.HealthyWorkerCount + "/" + snapshot.SessionWorkerCount +
                   " | HB max: " + heartbeat;
        }

        private static string GetHeartbeatStatus(BossHuntWorkerSnapshot worker)
        {
            if (worker == null || worker.LastHeartbeatUtc == DateTime.MinValue)
                return "";
            double age = DateTime.UtcNow.Subtract(worker.LastHeartbeatUtc).TotalSeconds;
            if (age < 0.0)
                age = 0.0;
            if (worker.Unresponsive || age > 8.0)
                return "HB LOST " + age.ToString("0.0") + "s";
            if (age >= 5.0)
                return "HB CHẬM " + age.ToString("0.0") + "s";
            return "HB " + age.ToString("0.0") + "s";
        }

        private static string FormatEvidence(string evidence, bool vi)
        {
            evidence = (evidence ?? "").Trim();
            if (evidence.Length == 0)
                return vi ? "Chưa rõ" : "Unknown";
            return evidence
                .Replace("ANNOUNCEMENT", vi ? "Thông báo" : "Announcement")
                .Replace("COMBAT_-60", "Combat -60")
                .Replace("FALLBACK", vi ? "Fallback HP/event" : "Fallback HP/event")
                .Replace("UNKNOWN", vi ? "Chưa rõ" : "Unknown")
                .Replace(",", " + ");
        }

        private static string GetUnparsedWarning(BossHuntSnapshot snapshot, bool vi)
        {
            if (snapshot == null || string.IsNullOrEmpty(snapshot.LastUnparsedDeath))
                return "";
            string age = snapshot.LastUnparsedDeathUtc == DateTime.MinValue ? "-" : FormatAge(snapshot.LastUnparsedDeathUtc);
            return (vi ? "⚠ Death chưa parse (" : "⚠ Unparsed death (") + age + "): " +
                   Truncate(snapshot.LastUnparsedDeath, 125);
        }

        private void UpdateSounds(BossHuntSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            if (snapshot.SessionId != lastSoundSessionId)
            {
                lastSoundSessionId = snapshot.SessionId;
                lastSoundState = BossHuntState.Idle;
                lastDeathSoundTicks = 0L;
            }

            if (checkSound.Checked)
            {
                if (snapshot.State == BossHuntState.Rallying && lastSoundState == BossHuntState.Scanning)
                    SystemSounds.Asterisk.Play();

                BossHuntBossSnapshot boss = snapshot.TargetBoss;
                long deathTicks = boss == null || boss.DiedAtUtc == DateTime.MinValue ? 0L : boss.DiedAtUtc.Ticks;
                if (boss != null && boss.Presence == BossPresenceState.Dead && deathTicks > 0L && deathTicks != lastDeathSoundTicks)
                {
                    SystemSounds.Exclamation.Play();
                    lastDeathSoundTicks = deathTicks;
                }
            }

            lastSoundState = snapshot.State;
        }

        private void grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= grid.Rows.Count || TabData._instance == null)
                return;

            object value = grid.Rows[e.RowIndex].Cells[0].Value;
            int accountId;
            if (value == null || !int.TryParse(value.ToString(), out accountId))
                return;

            List<Account> accounts = TabData._instance.GetAccounts();
            for (int i = 0; i < accounts.Count; i++)
            {
                Account account = accounts[i];
                if (account == null || account.ID != accountId)
                    continue;

                IntPtr hWnd;
                if (MainController.ExistedWindow(account, out hWnd))
                {
                    MainController.ShowWindowAsync(hWnd, 9);
                    MainController.SetForegroundWindow(hWnd);
                }
                else
                {
                    MessageBox.Show(
                        MainController.language == 0 ? "Không tìm thấy cửa sổ game của tài khoản này." : "Game window was not found for this account.",
                        MainController.language == 0 ? "Săn Boss" : "Boss Hunt",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                return;
            }
        }

        private void RefreshConnectedCount()
        {
            int count = BossHuntCoordinator.Instance.GetConnectedCount();
            labelConnected.Text = MainController.language == 0
                ? "Kết nối: " + count
                : "Connected: " + count;
        }
    }
}
