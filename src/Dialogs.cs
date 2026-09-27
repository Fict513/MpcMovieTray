using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MpcMovieDisplay {
    static class Dialogs {

        // 15-second "Keep settings?" confirmation with countdown + Revert.
        public static bool Confirm(string profile) { return Confirm(profile,"Movie","Desktop"); }
        public static bool Confirm(string profile,string presetName,string previousName) {
            using(ChromeForm f = new ChromeForm("MPC Movie Tray", 480, 262)) {
                f.ShowInTaskbar = false;
                f.TopMost = true;
                f.Body.Padding = new Padding(0);
                f.Body.AutoScroll = false;
                int gx = Theme.Sc(18);
                int W = f.ClientSize.Width - gx * 2;

                Label heading = new Label();
                heading.AutoSize = false; heading.Text = "Keep the "+presetName+" preset?";
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
                desc.Text = "Testing this switch on the Panasonic TV. Check picture and colours \u2014 if you do nothing, "+previousName+" returns.";
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
                revert.Kind = 0; revert.Font = Theme.Font(14, true); revert.Text = "Revert now";
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
                note.Text = "Presets: 4K60 RGB Full 8-bit desktop / 4K RGB Full 10-bit movie.\r\nEach new preset is tested before automatic use.";
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

        // First-run setup: stepper wizard offered while no preset has ever been verified.
        // testMovie/testDesktop must run the existing first-use confirmation + 15 s rollback
        // and return true once that preset is verified; changeDisplay lets the user go back
        // and pick a different display. Nothing here duplicates the switch/rollback logic.
        public static void FirstRun(Func<string> displayLabel,Func<bool> changeDisplay,Func<bool> testMovie,Func<bool> testDesktop,Func<bool> movieDone,Func<bool> desktopDone) {
            using(ChromeForm f = new ChromeForm("MPC Movie Tray \u2014 Setup", 640, 540)) {
                f.ShowInTaskbar = true;
                f.Body.Padding = new Padding(0);
                f.Body.AutoScroll = false;
                int gx = Theme.Sc(18);
                int W = f.ClientSize.Width - gx * 2;

                Label stepper = new Label();
                stepper.AutoSize = false; stepper.Font = Theme.Font(12, true); stepper.ForeColor = Theme.TextSecondary;
                stepper.BackColor = f.Body.BackColor;
                stepper.Text = "1 Display \u2713  \u00B7  2 Test presets  \u00B7  3 Finish";
                stepper.Location = new Point(gx, Theme.Sc(6)); stepper.Size = new Size(W, Theme.Sc(20));
                f.Body.Controls.Add(stepper);

                Label dispRow = new Label();
                dispRow.AutoSize = false; dispRow.Font = Theme.Font(13, false); dispRow.ForeColor = Theme.TextPrimary;
                dispRow.BackColor = Theme.Sunken; dispRow.Padding = new Padding(Theme.Sc(11), Theme.Sc(8), Theme.Sc(11), Theme.Sc(8));
                dispRow.Text = displayLabel();
                dispRow.Location = new Point(gx, Theme.Sc(36)); dispRow.Size = new Size(W - Theme.Sc(90), Theme.Sc(34));
                f.Body.Controls.Add(dispRow);
                ThemeButton change = new ThemeButton();
                change.Kind = 0; change.Font = Theme.Font(12, true); change.Text = "Change";
                change.Location = new Point(gx + W - Theme.Sc(82), Theme.Sc(36)); change.Size = new Size(Theme.Sc(82), Theme.Sc(34));
                change.Click += delegate { if(changeDisplay()) { f.DialogResult = DialogResult.Retry; f.Close(); } };
                f.Body.Controls.Add(change);

                Label note = new Label();
                note.AutoSize = false; note.Font = Theme.Font(11, false); note.ForeColor = Theme.TextSecondary;
                note.BackColor = f.Body.BackColor;
                note.Text = "Each test switches the TV and reverts after 15 s unless you choose Keep.";
                note.Location = new Point(gx, Theme.Sc(80)); note.Size = new Size(W, Theme.Sc(18));
                f.Body.Controls.Add(note);

                ThemeButton finish = new ThemeButton();
                finish.Kind = 1; finish.Font = Theme.Font(14, true); finish.Text = "Finish";
                finish.Size = new Size(Theme.Sc(140), Theme.Sc(38));
                finish.Location = new Point(gx + W - Theme.Sc(140), Theme.Sc(486));
                finish.DialogResult = DialogResult.OK;
                finish.Enabled = movieDone() && desktopDone();
                f.Body.Controls.Add(finish);
                f.AcceptButton = finish;

                ThemeButton skip = new ThemeButton();
                skip.Kind = 0; skip.Font = Theme.Font(13, true); skip.Text = "Skip for now";
                skip.Size = new Size(Theme.Sc(120), Theme.Sc(38));
                skip.Location = new Point(gx, Theme.Sc(486));
                skip.DialogResult = DialogResult.Cancel;
                f.Body.Controls.Add(skip);

                Row(f.Body, gx, Theme.Sc(108), W, "Movie","4K \u00B7 RGB Full \u00B7 10-bit",testMovie,movieDone(),finish);
                Row(f.Body, gx, Theme.Sc(160), W, "Desktop","4K60 \u00B7 RGB Full \u00B7 8-bit",testDesktop,desktopDone(),finish);

                DialogResult r=f.ShowDialog();
                if(r==DialogResult.Retry) FirstRun(displayLabel,changeDisplay,testMovie,testDesktop,movieDone,desktopDone);
            }
        }
        static void Row(Control parent,int gx,int y,int W,string name,string spec,Func<bool> test,bool done,ThemeButton finish) {
            Label title = new Label();
            title.AutoSize = false; title.Font = Theme.Font(13, true); title.ForeColor = Theme.TextPrimary;
            title.BackColor = parent.BackColor; title.Text = name+"  \u00B7  "+spec;
            title.Location = new Point(gx, y); title.Size = new Size(W - Theme.Sc(120), Theme.Sc(20));
            parent.Controls.Add(title);
            Label state = new Label();
            state.AutoSize = false; state.Font = Theme.Font(12, true);
            state.Location = new Point(gx + W - Theme.Sc(112), y); state.Size = new Size(Theme.Sc(112), Theme.Sc(30));
            state.Text = "Verified"; state.ForeColor = Theme.Green; state.BackColor = parent.BackColor;
            ThemeButton test2 = new ThemeButton();
            test2.Kind = 0; test2.Font = Theme.Font(12, true); test2.Text = "Test now";
            test2.Location = state.Location; test2.Size = state.Size;
            state.Visible = done; test2.Visible = !done;
            parent.Controls.Add(state); parent.Controls.Add(test2);
            test2.Click += delegate { if(test()) { state.Visible = true; test2.Visible = false; finish.Enabled = true; } };
        }
    }
}
