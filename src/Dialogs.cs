using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MpcMovieDisplay {
    static class Dialogs {

        // 15-second "Keep these settings?" confirmation with countdown + Restore.
        public static bool Confirm(string profile) {
            using(ChromeForm f = new ChromeForm("Keep this display setting?", 480, 262)) {
                f.ShowInTaskbar = false;
                f.TopMost = true;
                f.Body.Padding = new Padding(0);
                f.Body.AutoScroll = false;
                int gx = Theme.Sc(18);
                int W = f.ClientSize.Width - gx * 2;

                Label heading = new Label();
                heading.AutoSize = false; heading.Text = "Keep these settings?";
                heading.Font = Theme.Font(16, true); heading.ForeColor = Color.FromArgb(242, 246, 251);
                heading.BackColor = f.Body.BackColor;
                heading.Location = new Point(gx, Theme.Sc(6)); heading.Size = new Size(W, Theme.Sc(24));
                f.Body.Controls.Add(heading);

                Label spec = new Label();
                spec.AutoSize = true; spec.Text = profile;
                spec.Font = Theme.Mono(13, true); spec.ForeColor = Theme.TextPrimary;
                spec.BackColor = Theme.Sunken;
                spec.Padding = new Padding(Theme.Sc(11), Theme.Sc(7), Theme.Sc(11), Theme.Sc(7));
                spec.Location = new Point(gx, Theme.Sc(34));
                f.Body.Controls.Add(spec);

                Label desc = new Label();
                desc.AutoSize = false;
                desc.Text = "Check the picture and colours on the Panasonic TV. If you do nothing, the previous output is restored automatically.";
                desc.Font = Theme.Font(12, false); desc.ForeColor = Theme.TextSecondary;
                desc.BackColor = f.Body.BackColor;
                desc.Location = new Point(gx, Theme.Sc(74)); desc.Size = new Size(W, Theme.Sc(36));
                f.Body.Controls.Add(desc);

                Label count = new Label();
                count.AutoSize = false; count.Font = Theme.Font(12, false); count.ForeColor = Theme.TextSecondary;
                count.BackColor = f.Body.BackColor;
                count.Location = new Point(gx, Theme.Sc(118)); count.Size = new Size(Theme.Sc(120), Theme.Sc(20));
                count.TextAlign = ContentAlignment.MiddleLeft;
                f.Body.Controls.Add(count);

                Panel track = new Panel();
                track.BackColor = Theme.Sunken;
                track.Location = new Point(gx + Theme.Sc(120), Theme.Sc(121)); track.Size = new Size(W - Theme.Sc(120), Theme.Sc(6));
                f.Body.Controls.Add(track);
                Panel fill = new Panel();
                fill.Location = new Point(0, 0); fill.Size = new Size(track.Width, track.Height);
                fill.Paint += delegate(object s, PaintEventArgs pe) {
                    Rectangle r = fill.ClientRectangle;
                    if(r.Width > 0 && r.Height > 0)
                        using(LinearGradientBrush b = new LinearGradientBrush(r, Theme.AccentA, Theme.AccentB, 0f))
                            pe.Graphics.FillRectangle(b, r);
                };
                track.Controls.Add(fill);

                ThemeButton keep = new ThemeButton();
                keep.Kind = 1; keep.Font = Theme.Font(14, true); keep.Text = "Keep settings";
                keep.Size = new Size((W - Theme.Sc(10)) / 2, Theme.Sc(40));
                keep.Location = new Point(gx, Theme.Sc(150));
                keep.DialogResult = DialogResult.OK;
                f.Body.Controls.Add(keep);

                ThemeButton revert = new ThemeButton();
                revert.Kind = 0; revert.Font = Theme.Font(14, true); revert.Text = "Restore now";
                revert.Glyph = Theme.GlyphRestore;
                revert.Size = new Size((W - Theme.Sc(10)) / 2, Theme.Sc(40));
                revert.Location = new Point(gx + (W - Theme.Sc(10)) / 2 + Theme.Sc(10), Theme.Sc(150));
                revert.DialogResult = DialogResult.Cancel;
                f.Body.Controls.Add(revert);

                f.AcceptButton = keep; f.CancelButton = revert;

                DateTime end = DateTime.UtcNow.AddSeconds(15);
                Timer timer = new Timer();
                timer.Interval = 200;
                timer.Tick += delegate {
                    double leftSec = (end - DateTime.UtcNow).TotalSeconds;
                    int left = (int)Math.Ceiling(leftSec);
                    if(left < 0) left = 0;
                    count.Text = "Reverting in " + left + " s";
                    int fw = (int)Math.Round(track.Width * Math.Max(0.0, leftSec) / 15.0);
                    if(fw < 0) fw = 0; if(fw > track.Width) fw = track.Width;
                    fill.Width = fw; fill.Invalidate();
                    if(leftSec <= 0) { timer.Stop(); f.DialogResult = DialogResult.Cancel; f.Close(); }
                };
                count.Text = "Reverting in 15 s";
                timer.Start();
                bool ok = false;
                try { ok = f.ShowDialog() == DialogResult.OK; }
                finally { timer.Stop(); timer.Dispose(); }
                return ok;
            }
        }

        // Styled display chooser. Returns the chosen display, or null if cancelled.
        public static Display ChooseDisplay() {
            Display[] choices = Native.Displays();
            if(choices.Length == 0)
                throw new Exception("No unambiguous display found. Use an extended desktop, not Duplicate or Surround.");
            using(ChromeForm f = new ChromeForm("Choose your movie display", 620, 232)) {
                f.Body.Padding = new Padding(0);
                f.Body.AutoScroll = false;
                int gx = Theme.Sc(18);
                int W = f.ClientSize.Width - gx * 2;

                Label text = new Label();
                text.AutoSize = false;
                text.Text = "Choose the Panasonic TV. Only this display will be changed.";
                text.Font = Theme.Font(13, false); text.ForeColor = Theme.TextPrimary;
                text.BackColor = f.Body.BackColor;
                text.Location = new Point(gx, Theme.Sc(8)); text.Size = new Size(W, Theme.Sc(24));
                f.Body.Controls.Add(text);

                ComboBox box = new ComboBox();
                box.DropDownStyle = ComboBoxStyle.DropDownList;
                box.FlatStyle = FlatStyle.Flat;
                box.BackColor = Theme.Sunken; box.ForeColor = Theme.TextPrimary;
                box.Font = Theme.Font(13, false);
                box.Location = new Point(gx, Theme.Sc(42)); box.Size = new Size(W, Theme.Sc(30));
                foreach(Display d in choices)
                    box.Items.Add(d.Name + "  |  " + d.Current.Width + " x " + d.Current.Height + "  |  " + d.Description);
                box.SelectedIndex = 0;
                f.Body.Controls.Add(box);

                Label note = new Label();
                note.AutoSize = false;
                note.Text = "Presets: 4K60 RGB Full 8-bit desktop / 4K30 RGB Full 10-bit movie.\r\nEach new preset is tested before automatic use.";
                note.Font = Theme.Font(11, false); note.ForeColor = Theme.TextSecondary;
                note.BackColor = f.Body.BackColor;
                note.Location = new Point(gx, Theme.Sc(84)); note.Size = new Size(W, Theme.Sc(40));
                f.Body.Controls.Add(note);

                ThemeButton select = new ThemeButton();
                select.Kind = 1; select.Font = Theme.Font(14, true); select.Text = "Use this display";
                select.Size = new Size(Theme.Sc(220), Theme.Sc(40));
                select.Location = new Point(gx + W - Theme.Sc(220), Theme.Sc(138));
                select.DialogResult = DialogResult.OK;
                f.Body.Controls.Add(select);
                f.AcceptButton = select;

                return f.ShowDialog() == DialogResult.OK ? choices[box.SelectedIndex] : null;
            }
        }
    }
}
