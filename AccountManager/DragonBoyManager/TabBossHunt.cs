using System;
using System.Collections.Generic;
using System.Drawing;
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
        private readonly Label labelBoss = new Label();
        private readonly Label labelStartZone = new Label();
        private readonly Label labelConnected = new Label();
        private readonly Label labelState = new Label();
        private readonly Label labelResult = new Label();
        private readonly DataGridView grid = new DataGridView();
        private readonly Timer timer = new Timer();

        public TabBossHunt()
        {
            instance = this;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(64, 64, 64);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            BuildUi();
            BossHuntCoordinator.Instance.Changed += Coordinator_Changed;
            timer.Interval = 1000;
            timer.Tick += delegate
            {
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

            grid.Columns[0].HeaderText = "ID";
            grid.Columns[1].HeaderText = vi ? "Tài khoản" : "Account";
            grid.Columns[2].HeaderText = "Gen";
            grid.Columns[3].HeaderText = "Worker";
            grid.Columns[4].HeaderText = "Map";
            grid.Columns[5].HeaderText = vi ? "Khu" : "Zone";
            grid.Columns[6].HeaderText = vi ? "Đã dò" : "Scanned";
            grid.Columns[7].HeaderText = vi ? "Nhịp cuối" : "Last signal";
            grid.Columns[8].HeaderText = vi ? "Trạng thái" : "Status";

            RefreshConnectedCount();
            ApplySnapshot(BossHuntCoordinator.Instance.GetSnapshot());
        }

        private void BuildUi()
        {
            labelBoss.SetBounds(14, 18, 86, 24);
            comboBoss.SetBounds(100, 15, 175, 26);
            comboBoss.DropDownStyle = ComboBoxStyle.DropDown;
            comboBoss.Items.AddRange(new object[]
            {
                "Super Broly", "Black Goku", "Super Black Goku", "Fide Vàng", "Fide Đại Ca",
                "Cooler", "Xên hoàn thiện", "Siêu bọ hung", "Xên bọ hung", "Xên con",
                "Android 13", "Android 14", "Android 15", "Android 19", "Dr.Kôrê",
                "Bojack", "Cumber", "Tiểu đội trưởng", "Số 1", "Số 2", "Số 3", "Số 4"
            });
            comboBoss.Text = "Super Broly";
            comboBoss.TextChanged += delegate { ApplySnapshot(BossHuntCoordinator.Instance.GetSnapshot()); };

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

            labelConnected.SetBounds(14, 52, 245, 24);
            labelState.SetBounds(270, 52, 450, 24);
            labelState.Font = new Font(labelState.Font, FontStyle.Bold);

            grid.SetBounds(14, 82, 710, 205);
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
            grid.ScrollBars = ScrollBars.Both;

            grid.Columns.Add(new DataGridViewTextBoxColumn { Width = 45 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Width = 120 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Width = 45 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Width = 65 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Width = 150 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Width = 45 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Width = 190 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Width = 115 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Width = 260 });

            GroupBox resultBox = new GroupBox();
            resultBox.Text = "Boss";
            resultBox.ForeColor = Color.White;
            resultBox.SetBounds(14, 295, 710, 95);
            labelResult.Dock = DockStyle.Fill;
            labelResult.Padding = new Padding(10, 6, 10, 6);
            labelResult.AutoEllipsis = true;
            resultBox.Controls.Add(labelResult);

            Controls.Add(labelBoss);
            Controls.Add(comboBoss);
            Controls.Add(labelStartZone);
            Controls.Add(numericStartZone);
            Controls.Add(buttonStart);
            Controls.Add(buttonStop);
            Controls.Add(labelConnected);
            Controls.Add(labelState);
            Controls.Add(grid);
            Controls.Add(resultBox);
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            BossHuntSnapshot snapshot = BossHuntCoordinator.Instance.GetSnapshot();
            if (snapshot.State == BossHuntState.Scanning || snapshot.State == BossHuntState.Rallying || snapshot.State == BossHuntState.Fighting)
                return;

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

                grid.Rows.Add(
                    worker.AccountId,
                    worker.Username,
                    worker.AssignmentGeneration > 0 ? "G" + worker.AssignmentGeneration : "-",
                    workerText,
                    mapText,
                    worker.Zone < 0 ? "-" : worker.Zone.ToString(),
                    GetScannedText(worker),
                    GetSignalText(worker),
                    status);
            }

            labelState.Text = GetStateText(snapshot);
            UpdateBossInfo(snapshot);
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
                    "Boss: " + (string.IsNullOrEmpty(selectedBoss) ? "-" : selectedBoss) + Environment.NewLine +
                    (vi ? "Manager chưa nhận được thông báo spawn/death của boss này." : "Manager has not received spawn/death data for this boss.");
                return;
            }

            string state = boss.Alive ? (vi ? "ĐANG SỐNG" : "ALIVE") : (vi ? "ĐÃ CHẾT" : "DEAD");
            string spawn = boss.SpawnedAtUtc == DateTime.MinValue
                ? "-"
                : boss.SpawnedAtUtc.ToLocalTime().ToString("dd/MM HH:mm:ss");
            string death = boss.DiedAtUtc == DateTime.MinValue
                ? "-"
                : boss.DiedAtUtc.ToLocalTime().ToString("dd/MM HH:mm:ss");
            string map = boss.MapId < 0
                ? "-"
                : (string.IsNullOrEmpty(boss.MapName) ? "#" + boss.MapId : boss.MapName + " (#" + boss.MapId + ")");
            string sources = GetSourcesText(boss.SourceAccounts);
            string killer = string.IsNullOrEmpty(boss.Killer) ? (vi ? "Không rõ" : "Unknown") : boss.Killer;

            labelResult.Text =
                "Boss: " + boss.BossName + " | " + state + Environment.NewLine +
                (vi ? "Xuất hiện: " : "Spawn: ") + spawn + " | " + map + (boss.Zone >= 0 ? " K" + boss.Zone : "") +
                " | " + (vi ? "Nguồn: " : "Sources: ") + sources + Environment.NewLine +
                (vi ? "Chết: " : "Death: ") + death + " | " + (vi ? "Người hạ: " : "Killer: ") + killer;
        }

        private string GetStateText(BossHuntSnapshot snapshot)
        {
            string boss = string.IsNullOrEmpty(snapshot.BossName) ? "-" : snapshot.BossName;
            string suffix = snapshot.SessionId > 0
                ? " | S#" + snapshot.SessionId + " G" + snapshot.AssignmentGeneration
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

        private static string GetScannedText(BossHuntWorkerSnapshot worker)
        {
            if (worker.ScannedZones == null || worker.ScannedZones.Count == 0)
                return "-";

            int start = Math.Max(0, worker.ScannedZones.Count - 6);
            List<string> values = new List<string>();
            for (int i = start; i < worker.ScannedZones.Count; i++)
                values.Add(worker.ScannedZones[i]);
            return string.Join(", ", values.ToArray());
        }

        private static string GetSignalText(BossHuntWorkerSnapshot worker)
        {
            string eventAge = FormatAge(worker.LastEventUtc);
            string heartbeatAge = FormatAge(worker.LastHeartbeatUtc);
            return "E:" + eventAge + " H:" + heartbeatAge;
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
            return ((int)(seconds / 60)) + "m";
        }

        private static string GetSourcesText(List<int> sources)
        {
            if (sources == null || sources.Count == 0)
                return "-";
            List<string> values = new List<string>();
            for (int i = 0; i < sources.Count; i++)
                values.Add("#" + sources[i]);
            return string.Join(",", values.ToArray());
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
