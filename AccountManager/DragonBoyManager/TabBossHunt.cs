using System;
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
            timer.Tick += delegate { RefreshConnectedCount(); };
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
            grid.Columns[2].HeaderText = "Map";
            grid.Columns[3].HeaderText = vi ? "Khu" : "Zone";
            grid.Columns[4].HeaderText = vi ? "Trạng thái" : "Status";
            RefreshConnectedCount();
            ApplySnapshot(BossHuntCoordinator.Instance.GetSnapshot());
        }

        private void BuildUi()
        {
            labelBoss.SetBounds(18, 18, 110, 24);
            comboBoss.SetBounds(125, 15, 220, 26);
            comboBoss.DropDownStyle = ComboBoxStyle.DropDown;
            comboBoss.Items.AddRange(new object[]
            {
                "Super Broly", "Black Goku", "Super Black Goku", "Fide Vàng", "Fide Đại Ca",
                "Cooler", "Xên hoàn thiện", "Siêu bọ hung", "Xên bọ hung", "Xên con",
                "Android 13", "Android 14", "Android 15", "Android 19", "Dr.Kôrê",
                "Bojack", "Cumber", "Tiểu đội trưởng", "Số 1", "Số 2", "Số 3", "Số 4"
            });
            comboBoss.Text = "Super Broly";

            labelStartZone.SetBounds(365, 18, 100, 24);
            numericStartZone.SetBounds(465, 15, 65, 26);
            numericStartZone.Minimum = 0;
            numericStartZone.Maximum = 99;
            numericStartZone.Value = 0;

            buttonStart.SetBounds(550, 13, 130, 30);
            buttonStart.Click += buttonStart_Click;
            buttonStop.SetBounds(690, 13, 90, 30);
            buttonStop.Click += delegate { BossHuntCoordinator.Instance.Stop(MainController.language == 0 ? "Người dùng dừng" : "Stopped by user"); };

            labelConnected.SetBounds(18, 52, 330, 24);
            labelState.SetBounds(365, 52, 600, 24);
            labelState.Font = new Font(labelState.Font, FontStyle.Bold);

            grid.SetBounds(18, 82, 700, 380);
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
            grid.Columns.Add(new DataGridViewTextBoxColumn { Width = 55 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Width = 180 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Width = 155 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Width = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Width = 225 });

            GroupBox resultBox = new GroupBox();
            resultBox.Text = "Boss";
            resultBox.ForeColor = Color.White;
            resultBox.SetBounds(735, 82, 245, 380);
            labelResult.Dock = DockStyle.Fill;
            labelResult.Padding = new Padding(12);
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
                string mapText = worker.MapId < 0 ? "-" : (string.IsNullOrEmpty(worker.MapName) ? ("#" + worker.MapId) : worker.MapName + " (#" + worker.MapId + ")");
                grid.Rows.Add(worker.AccountId, worker.Username, mapText, worker.Zone < 0 ? "-" : worker.Zone.ToString(), worker.Status);
            }

            labelState.Text = GetStateText(snapshot);
            if (snapshot.FoundMapId >= 0)
            {
                labelResult.Text =
                    "Boss: " + snapshot.BossName + Environment.NewLine +
                    "Map: " + (string.IsNullOrEmpty(snapshot.FoundMapName) ? "#" + snapshot.FoundMapId : snapshot.FoundMapName + " (#" + snapshot.FoundMapId + ")") + Environment.NewLine +
                    (MainController.language == 0 ? "Khu: " : "Zone: ") + snapshot.FoundZone + Environment.NewLine +
                    (MainController.language == 0 ? "Acc tìm thấy: " : "Found by: ") + snapshot.FinderAccountId + Environment.NewLine + Environment.NewLine +
                    (!string.IsNullOrEmpty(snapshot.StopReason) ? snapshot.StopReason : "");
            }
            else
            {
                labelResult.Text = "Boss: " + (string.IsNullOrEmpty(snapshot.BossName) ? "-" : snapshot.BossName) +
                    Environment.NewLine + Environment.NewLine +
                    (!string.IsNullOrEmpty(snapshot.StopReason) ? snapshot.StopReason : "");
            }
        }

        private string GetStateText(BossHuntSnapshot snapshot)
        {
            string boss = string.IsNullOrEmpty(snapshot.BossName) ? "-" : snapshot.BossName;
            if (MainController.language == 0)
            {
                switch (snapshot.State)
                {
                    case BossHuntState.Scanning: return "Trạng thái: Đang dò " + boss;
                    case BossHuntState.Rallying: return "Trạng thái: Đã thấy boss - đang tập trung tài khoản";
                    case BossHuntState.Fighting: return "Trạng thái: Đang đánh " + boss;
                    case BossHuntState.Stopped: return "Trạng thái: Đã dừng";
                    default: return "Trạng thái: Chưa chạy";
                }
            }

            switch (snapshot.State)
            {
                case BossHuntState.Scanning: return "Status: Scanning " + boss;
                case BossHuntState.Rallying: return "Status: Boss found - rallying accounts";
                case BossHuntState.Fighting: return "Status: Fighting " + boss;
                case BossHuntState.Stopped: return "Status: Stopped";
                default: return "Status: Idle";
            }
        }

        private void RefreshConnectedCount()
        {
            int count = BossHuntCoordinator.Instance.GetConnectedCount();
            labelConnected.Text = MainController.language == 0 ? "Tài khoản đang kết nối: " + count : "Connected accounts: " + count;
        }
    }
}
