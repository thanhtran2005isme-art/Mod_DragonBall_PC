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

        public BossHuntLogViewer()
        {
            Text = "Boss Hunt Logs";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(980, 620);
            MinimumSize = new Size(760, 460);

            buttonRefresh.Text = "LÀM MỚI";
            buttonRefresh.SetBounds(10, 10, 100, 30);
            buttonRefresh.Click += delegate { RefreshLogs(); };

            buttonFolder.Text = "MỞ THƯ MỤC";
            buttonFolder.SetBounds(120, 10, 120, 30);
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

            textLog.SetBounds(10, 50, 940, 520);
            textLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textLog.Multiline = true;
            textLog.ReadOnly = true;
            textLog.ScrollBars = ScrollBars.Both;
            textLog.WordWrap = false;
            textLog.Font = new Font("Consolas", 9F, FontStyle.Regular);

            Controls.Add(buttonRefresh);
            Controls.Add(buttonFolder);
            Controls.Add(textLog);

            Shown += delegate { RefreshLogs(); };
        }

        private void RefreshLogs()
        {
            try
            {
                string directory = BossHuntDiagnostics.GetLogDirectory();
                string[] files = Directory.GetFiles(directory, "BossHuntProtocol.*.log*");
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
                int mergedStart = Math.Max(0, merged.Count - 1500);
                textLog.Lines = merged.GetRange(mergedStart, merged.Count - mergedStart).ToArray();
                textLog.SelectionStart = textLog.TextLength;
                textLog.ScrollToCaret();
            }
            catch (Exception ex)
            {
                textLog.Text = ex.Message;
            }
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
