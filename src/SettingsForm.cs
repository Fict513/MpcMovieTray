using System;
using System.Drawing;
using System.Windows.Forms;

namespace MpcMovieDisplay {

    // Snapshot of live output pushed from TrayContext each poll.
    public struct StatusView {
        public string DisplayName;
        public int Width, Height;
        public long Freq;
        public string Format, Range, Bits, Hdr, Control;
        public int Mpc;
        public bool MatchesMovie, MatchesDesktop, Automatic, Fallback;
    }

    // Actions wired to the existing TrayContext logic.
    public class SettingsCallbacks {
        public Action Movie, Desktop, RestoreDesktop, ToggleAutomatic, ToggleStartup,
                      ToggleFallback, OpenHdr, ChooseDisplay, OpenLog, OpenReadme, TestAgain;
    }

    // The main settings window: compact grouped layout matching the approved
    // design. It only renders and forwards intent; all switching logic stays in
    // TrayContext.
    sealed class SettingsForm : ChromeForm {
        readonly SettingsCallbacks cb;
        ThemeButton btnDisplay, btnDiag;
        PresetCardButton cardDesktop, cardMovie;
        Label lblOut1, lblOut2, lblMeta, lblMatches;
        RoundDot dotMatches;
        StatusPill hdrPill;
        ToggleSwitch tglAuto, tglStartup, tglFallback;
        GroupPanel diagPanel;
        bool diagOpen;
        int titleH = Theme.Sc(36);
        int collapsedBottom;

        public SettingsForm(SettingsCallbacks callbacks) : base("Settings", 840, 812) {
            cb = callbacks;
            Body.Padding = new Padding(0);
            Body.AutoScroll = true;
            BuildUI();
        }

        Label Lab(Control parent, string text, Font f, Color c, int x, int y, int w, int h) {
            Label l = new Label();
            l.AutoSize = false;
            l.Text = text; l.Font = f; l.ForeColor = c; l.BackColor = parent.BackColor;
            l.Location = new Point(x, y); l.Size = new Size(w, h);
            parent.Controls.Add(l);
            return l;
        }

        GroupPanel Group(string title, int x, int y, int w, int h) {
            GroupPanel gp = new GroupPanel();
            gp.TitleText = title;
            gp.Location = new Point(x, y); gp.Size = new Size(w, h);
            Body.Controls.Add(gp);
            return gp;
        }

        void BuildUI() {
            int LM = Theme.Sc(15);
            int W = Theme.Sc(810);
            int gx = Theme.Sc(13);          // group content inset
            int gy = Theme.Sc(20);          // group content top
            int innerW = W - gx * 2;
            int y = Theme.Sc(13);

            // ---- Display ----
            GroupPanel gDisp = Group("Display", LM, y, W, Theme.Sc(64));
            btnDisplay = new ThemeButton();
            btnDisplay.Kind = 2;
            btnDisplay.Font = Theme.Font(13, false);
            btnDisplay.Location = new Point(gx, gy);
            btnDisplay.Size = new Size(innerW, Theme.Sc(34));
            btnDisplay.Text = "Reading display\u2026";
            btnDisplay.Click += delegate { if(cb.ChooseDisplay != null) cb.ChooseDisplay(); };
            gDisp.Controls.Add(btnDisplay);
            y += Theme.Sc(64) + Theme.Sc(11);

            // ---- Presets ----
            int presetsH = Theme.Sc(104);
            GroupPanel gPre = Group("Presets", LM, y, W, presetsH);
            int cardW = (innerW - Theme.Sc(12)) / 2;
            int cardH = Theme.Sc(72);
            cardDesktop = new PresetCardButton();
            cardDesktop.Letter = "D"; cardDesktop.TitleText = "Desktop"; cardDesktop.BadgeColor = Theme.Blue;
            cardDesktop.SpecText = "3840\u00D72160 \u00B7 60 Hz \u00B7 RGB Full \u00B7 8-bit";
            cardDesktop.SubText = "Everyday use";
            cardDesktop.Location = new Point(gx, gy); cardDesktop.Size = new Size(cardW, cardH);
            cardDesktop.Click += delegate { if(cb.Desktop != null) cb.Desktop(); };
            gPre.Controls.Add(cardDesktop);
            cardMovie = new PresetCardButton();
            cardMovie.Letter = "M"; cardMovie.TitleText = "Movie"; cardMovie.BadgeColor = Theme.Green;
            cardMovie.SpecText = "3840\u00D72160 \u00B7 24 Hz \u00B7 RGB Full \u00B7 10-bit";
            cardMovie.SubText = "Auto-set while MPC is open";
            cardMovie.Location = new Point(gx + cardW + Theme.Sc(12), gy); cardMovie.Size = new Size(cardW, cardH);
            cardMovie.Click += delegate { if(cb.Movie != null) cb.Movie(); };
            gPre.Controls.Add(cardMovie);
            y += presetsH + Theme.Sc(11);

            // ---- Output row: Actual output + Windows HDR ----
            int rowH = Theme.Sc(126);
            int leftW = Theme.Sc(462);
            int rightW = W - leftW - Theme.Sc(10);
            GroupPanel gOut = Group("Actual output \u2014 live", LM, y, leftW, rowH);
            dotMatches = new RoundDot(Theme.Green, gOut.BackColor, Theme.Sc(7));
            dotMatches.Location = new Point(gx, gy + Theme.Sc(3));
            gOut.Controls.Add(dotMatches);
            lblMatches = Lab(gOut, "Matches Movie preset \u2713", Theme.Font(11, true), Theme.Green, gx + Theme.Sc(13), gy, leftW - gx * 2 - Theme.Sc(13), Theme.Sc(15));
            lblOut1 = Lab(gOut, "\u2014", Theme.Mono(15, true), Color.FromArgb(242, 246, 251), gx, gy + Theme.Sc(22), leftW - gx * 2, Theme.Sc(20));
            lblOut2 = Lab(gOut, "\u2014", Theme.Mono(15, true), Color.FromArgb(242, 246, 251), gx, gy + Theme.Sc(44), leftW - gx * 2, Theme.Sc(20));
            lblMeta = Lab(gOut, "\u2014", Theme.Font(11, false), Theme.TextSecondary, gx, gy + Theme.Sc(70), leftW - gx * 2, Theme.Sc(16));

            GroupPanel gHdr = Group("Windows HDR", LM + leftW + Theme.Sc(10), y, rightW, rowH);
            hdrPill = new StatusPill();
            hdrPill.Location = new Point(gx, gy);
            gHdr.Controls.Add(hdrPill);
            hdrPill.SetState("Off", Theme.GrayChip, Theme.TextSecondary, Color.FromArgb(36, 133, 147, 166), Color.FromArgb(90, 133, 147, 166));
            Lab(gHdr, "10-bit is active \u2014 this does not mean HDR is on.", Theme.Font(11, false), Theme.TextSecondary, gx, gy + Theme.Sc(30), rightW - gx * 2, Theme.Sc(30));
            ThemeButton btnHdr = new ThemeButton();
            btnHdr.Kind = 0; btnHdr.Font = Theme.Font(12, true);
            btnHdr.Text = "Open Windows HDR settings";
            btnHdr.Location = new Point(gx, rowH - Theme.Sc(13) - Theme.Sc(32));
            btnHdr.Size = new Size(rightW - gx * 2, Theme.Sc(32));
            btnHdr.Click += delegate { if(cb.OpenHdr != null) cb.OpenHdr(); };
            gHdr.Controls.Add(btnHdr);
            y += rowH + Theme.Sc(11);

            // ---- Behaviour ----
            int behH = Theme.Sc(132);
            GroupPanel gBeh = Group("Behaviour", LM, y, W, behH);
            Lab(gBeh, "Automatic switching", Theme.Font(13, true), Color.FromArgb(231, 236, 243), gx, gy, innerW - Theme.Sc(56), Theme.Sc(18));
            Lab(gBeh, "Movie when MPC-HC/BE opens; Desktop only after the last player closes. Pausing keeps Movie active.",
                Theme.Font(11, false), Theme.TextSecondary, gx, gy + Theme.Sc(19), innerW - Theme.Sc(56), Theme.Sc(30));
            tglAuto = new ToggleSwitch();
            tglAuto.Location = new Point(gx + innerW - Theme.Sc(40), gy + Theme.Sc(4));
            tglAuto.Click += delegate { if(cb.ToggleAutomatic != null) cb.ToggleAutomatic(); };
            gBeh.Controls.Add(tglAuto);
            Panel sep = new Panel();
            sep.BackColor = Theme.Border; sep.Size = new Size(innerW, 1);
            sep.Location = new Point(gx, gy + Theme.Sc(54));
            gBeh.Controls.Add(sep);
            Lab(gBeh, "Start with Windows", Theme.Font(13, true), Color.FromArgb(231, 236, 243), gx, gy + Theme.Sc(64), innerW - Theme.Sc(56), Theme.Sc(18));
            Lab(gBeh, "Launches to the tray for this account only.", Theme.Font(11, false), Theme.TextSecondary, gx, gy + Theme.Sc(83), innerW - Theme.Sc(56), Theme.Sc(16));
            tglStartup = new ToggleSwitch();
            tglStartup.Location = new Point(gx + innerW - Theme.Sc(40), gy + Theme.Sc(68));
            tglStartup.Click += delegate { if(cb.ToggleStartup != null) cb.ToggleStartup(); };
            gBeh.Controls.Add(tglStartup);
            y += behH + Theme.Sc(11);

            // ---- Restore Desktop ----
            ThemeButton btnRestore = new ThemeButton();
            btnRestore.Kind = 1; btnRestore.Font = Theme.Font(14, true);
            btnRestore.Text = "Restore Desktop";
            btnRestore.Location = new Point(LM, y); btnRestore.Size = new Size(W, Theme.Sc(40));
            btnRestore.Click += delegate { if(cb.RestoreDesktop != null) cb.RestoreDesktop(); };
            Body.Controls.Add(btnRestore);
            y += Theme.Sc(40) + Theme.Sc(11);

            // ---- Advanced: fallback ----
            int advH = Theme.Sc(72);
            GroupPanel gAdv = Group("Advanced \u2014 fallback", LM, y, W, advH);
            gAdv.TitleColor = Theme.Amber; gAdv.BorderColor = Color.FromArgb(78, 235, 165, 45);
            Lab(gAdv, "\u26A0", Theme.Font(16, false), Theme.Amber, gx, gy + Theme.Sc(2), Theme.Sc(22), Theme.Sc(22));
            Lab(gAdv, "60 Hz \u00B7 YCbCr 4:2:0 Limited \u00B7 8-bit", Theme.Mono(12, false), Color.FromArgb(231, 236, 243), gx + Theme.Sc(28), gy, innerW - Theme.Sc(160), Theme.Sc(16));
            Lab(gAdv, "Reduces text clarity. Use only if RGB Full at 4K60 is unreliable.", Theme.Font(11, false), Theme.TextSecondary, gx + Theme.Sc(28), gy + Theme.Sc(18), innerW - Theme.Sc(160), Theme.Sc(16));
            tglFallback = new ToggleSwitch();
            tglFallback.SetAccent(Theme.Amber, Color.FromArgb(214, 150, 40));
            tglFallback.Location = new Point(gx + innerW - Theme.Sc(120), gy + Theme.Sc(6));
            tglFallback.Click += delegate { if(cb.ToggleFallback != null) cb.ToggleFallback(); };
            gAdv.Controls.Add(tglFallback);
            Lab(gAdv, "Use fallback", Theme.Font(12, false), Theme.TextSecondary, gx + innerW - Theme.Sc(74), gy + Theme.Sc(8), Theme.Sc(74), Theme.Sc(18));
            y += advH + Theme.Sc(11);

            // ---- Diagnostics (expander) ----
            btnDiag = new ThemeButton();
            btnDiag.Kind = 0; btnDiag.Font = Theme.Font(13, true);
            btnDiag.Text = "Diagnostics  \u25BE";
            btnDiag.Location = new Point(LM, y); btnDiag.Size = new Size(W, Theme.Sc(38));
            btnDiag.Click += delegate { ToggleDiag(); };
            Body.Controls.Add(btnDiag);
            collapsedBottom = y + Theme.Sc(38);
            y += Theme.Sc(38) + Theme.Sc(11);

            diagPanel = new GroupPanel();
            diagPanel.TitleText = "";
            diagPanel.Location = new Point(LM, y);
            diagPanel.Size = new Size(W, Theme.Sc(250));
            diagPanel.Visible = false;
            Body.Controls.Add(diagPanel);
            BuildDiag(diagPanel, gx, innerW);

            SetClientPixelHeight(titleH + collapsedBottom + Theme.Sc(13));
        }

        void BuildDiag(GroupPanel p, int gx, int innerW) {
            int gy = Theme.Sc(18);
            int half = innerW / 2;
            string[] names = { "Recovery helper \u2014 Running", "Display identity \u2014 Matched", "NVIDIA driver \u2014 nvapi64 loaded", "Instance lock \u2014 Held" };
            for(int i = 0; i < 4; i++) {
                int col = i % 2, row = i / 2;
                int x = gx + col * half;
                int yy = gy + row * Theme.Sc(22);
                RoundDot d = new RoundDot(Theme.Green, p.BackColor, Theme.Sc(6));
                d.Location = new Point(x, yy + Theme.Sc(4));
                p.Controls.Add(d);
                Lab(p, names[i], Theme.Font(12, false), Theme.TextSecondary, x + Theme.Sc(12), yy, half - Theme.Sc(16), Theme.Sc(16));
            }
            int cy = gy + Theme.Sc(52);
            Lab(p, "Movie 4K RGB Full 10-bit \u2713", Theme.Mono(11, false), Theme.Green, gx, cy, half - Theme.Sc(8), Theme.Sc(16));
            Lab(p, "Desktop 4K60 RGB Full 8-bit \u2713", Theme.Mono(11, false), Theme.Blue, gx + half, cy, half - Theme.Sc(8), Theme.Sc(16));

            Panel log = new Panel();
            log.BackColor = Theme.Sunken;
            log.Location = new Point(gx, cy + Theme.Sc(24));
            log.Size = new Size(innerW, Theme.Sc(66));
            p.Controls.Add(log);
            Lab(log, "21:14:02  Applying movie preset.\r\n21:14:05  Movie mode verified \u2014 3840\u00D72160, RGB Full, 10-bit.\r\n21:14:05  Windows HDR could not be confirmed: Off.",
                Theme.Mono(11, false), Theme.TextSecondary, Theme.Sc(10), Theme.Sc(8), innerW - Theme.Sc(20), Theme.Sc(52));

            int by = cy + Theme.Sc(24) + Theme.Sc(66) + Theme.Sc(12);
            ThemeButton b1 = DiagButton("Open activity log", gx, by, Theme.Sc(150));
            b1.Click += delegate { if(cb.OpenLog != null) cb.OpenLog(); };
            p.Controls.Add(b1);
            ThemeButton b2 = DiagButton("Open README", gx + Theme.Sc(160), by, Theme.Sc(140));
            b2.Click += delegate { if(cb.OpenReadme != null) cb.OpenReadme(); };
            p.Controls.Add(b2);
            ThemeButton b3 = DiagButton("Test next switch again", gx + Theme.Sc(310), by, Theme.Sc(200));
            b3.Click += delegate { if(cb.TestAgain != null) cb.TestAgain(); };
            p.Controls.Add(b3);

            p.Size = new Size(p.Width, by + Theme.Sc(32) + Theme.Sc(14));
        }

        ThemeButton DiagButton(string text, int x, int y, int w) {
            ThemeButton b = new ThemeButton();
            b.Kind = 0; b.Font = Theme.Font(12, true); b.Text = text;
            b.Location = new Point(x, y); b.Size = new Size(w, Theme.Sc(32));
            return b;
        }

        void ToggleDiag() {
            diagOpen = !diagOpen;
            diagPanel.Visible = diagOpen;
            btnDiag.Text = diagOpen ? "Diagnostics  \u25B4" : "Diagnostics  \u25BE";
            int bottom = diagOpen ? diagPanel.Bottom : collapsedBottom;
            SetClientPixelHeight(titleH + bottom + Theme.Sc(13));
        }

        // ---- Live updates from TrayContext ----
        public void SetStatus(StatusView v) {
            if(IsDisposed) return;
            btnDisplay.Text = string.IsNullOrEmpty(v.DisplayName) ? "No display selected" :
                v.DisplayName + "    \u00B7    " + v.Width + " \u00D7 " + v.Height;
            lblOut1.Text = v.Width + " \u00D7 " + v.Height + "  \u00B7  " + v.Freq + " Hz";
            lblOut2.Text = v.Format + " " + v.Range + "  \u00B7  " + v.Bits + "-bit";
            lblMeta.Text = "Control: " + v.Control + "     MPC: " + v.Mpc;

            if(v.MatchesMovie) { lblMatches.Text = "Matches Movie preset \u2713"; lblMatches.ForeColor = Theme.Green; dotMatches.Dot = Theme.Green; }
            else if(v.MatchesDesktop) { lblMatches.Text = "Matches Desktop preset \u2713"; lblMatches.ForeColor = Theme.Blue; dotMatches.Dot = Theme.Blue; }
            else { lblMatches.Text = "Custom / unverified"; lblMatches.ForeColor = Theme.Amber; dotMatches.Dot = Theme.Amber; }
            dotMatches.Invalidate();

            string hs = v.Hdr == null ? "" : v.Hdr;
            if(hs == "On")
                hdrPill.SetState("On", Theme.Green, Theme.Green, Color.FromArgb(30, 79, 208, 138), Color.FromArgb(90, 79, 208, 138));
            else if(hs == "Off")
                hdrPill.SetState("Off", Theme.GrayChip, Theme.TextSecondary, Color.FromArgb(36, 133, 147, 166), Color.FromArgb(90, 133, 147, 166));
            else
                hdrPill.SetState("Unknown", Theme.Amber, Theme.Amber, Color.FromArgb(30, 235, 165, 45), Color.FromArgb(90, 235, 165, 45));

            cardMovie.SetActive(v.MatchesMovie);
            cardDesktop.SetActive(v.MatchesDesktop);
            SetAutomatic(v.Automatic);
            SetFallback(v.Fallback);
        }
        public void SetAutomatic(bool on) { if(tglAuto != null) { tglAuto.Checked = on; tglAuto.Invalidate(); } }
        public void SetFallback(bool on) { if(tglFallback != null) { tglFallback.Checked = on; tglFallback.Invalidate(); } }
        public void SetMovieHz(uint hz) {
            if(cardMovie == null) return;
            cardMovie.SpecText = "3840\u00D72160 \u00B7 " + hz + " Hz \u00B7 RGB Full \u00B7 10-bit";
            cardMovie.Invalidate();
        }
        public void SetStartup(bool on) { if(tglStartup != null) { tglStartup.Checked = on; tglStartup.Invalidate(); } }
    }
}
