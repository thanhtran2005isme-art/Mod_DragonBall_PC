using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DragonBoyManager
{
    internal sealed class BossHuntLogViewer : Form
    {
        private readonly TextBox textLog = new TextBox();
        private readonly Button buttonRefresh = new Button();
        private readonly Button buttonFolder = new Button();
        private readonly Button buttonCopy = new Button();
        private readonly Button buttonClear = new Button();
        private readonly TextBox filterSession = new TextBox();
        private readonly TextBox filterAccount = new TextBox();
        private readonly TextBox filterEvent = new TextBox();
        private readonly TextBox filterBoss = new TextBox();
        private readonly List<string> allLines = new List<string>();

        public BossHuntLogViewer()
        {
            Text = "Boss Hunt Logs";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(980, 620);
            MinimumSize = new Size(760, 460);

            buttonRefresh.Text = "LÀM MỚI";
            buttonRefresh.SetBounds(10, 10, 92, 30);
            buttonRefresh.Click += delegate { RefreshLogs(); };

            buttonFolder.Text = "MỞ THƯ MỤC";
            buttonFolder.SetBounds(108, 10, 112, 30);
            buttonFolder.Click += delegate
            {
                try
                {
                    string quote = ((char)34).ToString();
                    Process.Start("explorer.exe", quote + BossHuntDiagnostics.GetLogDirectory() + quote);
                }
                catch
                {
                }
            };

            buttonCopy.Text = "COPY";
            buttonCopy.SetBounds(226, 10, 70, 30);
            buttonCopy.Click += delegate
            {
                try
                {
                    if (!string.IsNullOrEmpty(textLog.Text))
                        Clipboard.SetText(textLog.Text);
                }
                catch
                {
                }
            };

            buttonClear.Text = "XÓA LỌC";
            buttonClear.SetBounds(302, 10, 80, 30);
            buttonClear.Click += delegate
            {
                filterSession.Text = "";
                filterAccount.Text = "";
                filterEvent.Text = "";
                filterBoss.Text = "";
                ApplyFilters();
            };

            Label labelSession = new Label();
            labelSession.Text = "Session";
            labelSession.SetBounds(394, 10, 48, 18);
            filterSession.SetBounds(442, 8, 58, 24);

            Label labelAccount = new Label();
            labelAccount.Text = "Acc";
            labelAccount.SetBounds(510, 10, 28, 18);
            filterAccount.SetBounds(540, 8, 58, 24);

            Label labelEvent = new Label();
            labelEvent.Text = "Event";
            labelEvent.SetBounds(608, 10, 38, 18);
            filterEvent.SetBounds(648, 8, 105, 24);

            Label labelBoss = new Label();
            labelBoss.Text = "Boss";
            labelBoss.SetBounds(762, 10, 34, 18);
            filterBoss.SetBounds(798, 8, 152, 24);

            filterSession.TextChanged += delegate { ApplyFilters(); };
            filterAccount.TextChanged += delegate { ApplyFilters(); };
            filterEvent.TextChanged += delegate { ApplyFilters(); };
            filterBoss.TextChanged += delegate { ApplyFilters(); };

            Label hint = new Label();
            hint.Text = "Lọc trực tiếp trên log đã nạp. Ví dụ Event: DEATH, Account: 3, Session: 12.";
            hint.SetBounds(10, 44, 940, 20);

            textLog.SetBounds(10, 68, 940, 502);
            textLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textLog.Multiline = true;
            textLog.ReadOnly = true;
            textLog.ScrollBars = ScrollBars.Both;
            textLog.WordWrap = false;
            textLog.Font = new Font("Consolas", 9F, FontStyle.Regular);

            Controls.Add(buttonRefresh);
            Controls.Add(buttonFolder);
            Controls.Add(buttonCopy);
            Controls.Add(buttonClear);
            Controls.Add(labelSession);
            Controls.Add(filterSession);
            Controls.Add(labelAccount);
            Controls.Add(filterAccount);
            Controls.Add(labelEvent);
            Controls.Add(filterEvent);
            Controls.Add(labelBoss);
            Controls.Add(filterBoss);
            Controls.Add(hint);
            Controls.Add(textLog);

            Shown += delegate { RefreshLogs(); };
        }

        private void RefreshLogs()
        {
            try
            {
                string directory = BossHuntDiagnostics.GetLogDirectory();
                string[] protocolFiles = Directory.GetFiles(directory, "BossHuntProtocol.*.log*");
                List<string> fileList = new List<string>();
                for (int i = 0; i < protocolFiles.Length; i++)
                    fileList.Add(protocolFiles[i]);

                string runtimePath = Path.Combine(directory, "ManagerRuntime.log");
                if (File.Exists(runtimePath))
                    fileList.Add(runtimePath);

                string[] files = fileList.ToArray();
                List<string> merged = new List<string>();

                for (int i = 0; i < files.Length; i++)
                {
                    string[] lines;
                    try
                    {
                        lines = File.ReadAllLines(files[i]);
                    }
                    catch
                    {
                        continue;
                    }

                    int start = Math.Max(0, lines.Length - 250);
                    for (int j = start; j < lines.Length; j++)
                        merged.Add(Decorate(files[i], lines[j]));
                }

                merged.Sort(StringComparer.Ordinal);
                allLines.Clear();
                int mergedStart = Math.Max(0, merged.Count - 3000);
                allLines.AddRange(merged.GetRange(mergedStart, merged.Count - mergedStart));
                ApplyFilters();
            }
            catch (Exception ex)
            {
                textLog.Text = ex.Message;
            }
        }

        private void ApplyFilters()
        {
            try
            {
                string session = (filterSession.Text ?? "").Trim();
                string account = (filterAccount.Text ?? "").Trim();
                string eventName = (filterEvent.Text ?? "").Trim();
                string boss = (filterBoss.Text ?? "").Trim();

                List<string> filtered = new List<string>();
                for (int i = 0; i < allLines.Count; i++)
                {
                    string line = allLines[i] ?? "";
                    if (session.Length > 0 && !ContainsIgnoreCase(line, "|session=" + session))
                        continue;
                    if (account.Length > 0 && !ContainsIgnoreCase(line, "|account=" + account))
                        continue;
                    if (eventName.Length > 0 && !ContainsIgnoreCase(line, "|event=" + eventName))
                        continue;
                    if (boss.Length > 0 && !ContainsIgnoreCase(line, "|boss=" + boss))
                        continue;
                    filtered.Add(line);
                }

                int start = Math.Max(0, filtered.Count - 1500);
                textLog.Lines = filtered.GetRange(start, filtered.Count - start).ToArray();
                textLog.SelectionStart = textLog.TextLength;
                textLog.ScrollToCaret();
            }
            catch (Exception ex)
            {
                textLog.Text = ex.Message;
            }
        }

        private static bool ContainsIgnoreCase(string value, string token)
        {
            return (value ?? "").IndexOf(token ?? "", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Decorate(string file, string line)
        {
            string name = Path.GetFileName(file);
            line = line ?? "";
            int close = line.IndexOf("] ", StringComparison.Ordinal);
            if (line.StartsWith("[", StringComparison.Ordinal) && close > 0)
                return line.Substring(0, close + 1) + " [" + name + "] " + line.Substring(close + 2);
            return "[" + name + "] " + line;
        }
    }
}
