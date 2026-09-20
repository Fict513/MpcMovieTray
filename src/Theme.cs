using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MpcMovieDisplay {

    // Central palette, DPI scaling and font cache. Colours are taken from the
    // approved design: dark blue-tinted surfaces with a cyan-to-pink brand
    // gradient derived from the HDR monitor icon.
    static class Theme {
        public static readonly Color WindowBg     = Color.FromArgb(26, 33, 44);   // #1A212C
        public static readonly Color TitleBg      = Color.FromArgb(23, 29, 38);   // #171D26
        public static readonly Color Surface      = Color.FromArgb(31, 40, 54);   // #1F2836
        public static readonly Color Sunken       = Color.FromArgb(15, 20, 28);   // #0F141C
        public static readonly Color CardBg       = Color.FromArgb(29, 38, 52);   // #1D2634
        public static readonly Color MenuBg       = Color.FromArgb(28, 35, 48);   // #1C2330
        public static readonly Color Border       = Color.FromArgb(28, 255, 255, 255);
        public static readonly Color BorderStrong = Color.FromArgb(34, 255, 255, 255);
        public static readonly Color TextPrimary  = Color.FromArgb(238, 242, 247);// #EEF2F7
        public static readonly Color TextBright   = Color.FromArgb(219, 226, 236);// #DBE2EC
        public static readonly Color TextSecondary= Color.FromArgb(143, 160, 179);// #8FA0B3
        public static readonly Color TextMuted    = Color.FromArgb(109, 122, 140);// #6D7A8C
        public static readonly Color TextLabel    = Color.FromArgb(147, 161, 179);// #93A1B3
        public static readonly Color AccentA      = Color.FromArgb(51, 184, 232); // #33B8E8
        public static readonly Color AccentB      = Color.FromArgb(233, 105, 177);// #E969B1
        public static readonly Color Green        = Color.FromArgb(79, 208, 138); // #4FD08A
        public static readonly Color Amber        = Color.FromArgb(235, 165, 45); // #EBA52D
        public static readonly Color Blue         = Color.FromArgb(90, 166, 245); // #5AA6F5
        public static readonly Color GrayChip     = Color.FromArgb(133, 147, 166);// #8593A6
        public static readonly Color DarkInk      = Color.FromArgb(15, 19, 26);   // text over accent
        public static readonly Color TrafficRed   = Color.FromArgb(255, 95, 87);
        public static readonly Color TrafficAmber = Color.FromArgb(254, 188, 46);
        public static readonly Color TrafficGreen = Color.FromArgb(40, 200, 64);

        public static readonly float Scale = ComputeScale();
        static float ComputeScale() {
            try { using(Graphics g = Graphics.FromHwnd(IntPtr.Zero)) { float s = g.DpiX / 96f; return s < 1f ? 1f : s; } }
            catch { return 1f; }
        }
        public static int Sc(int px) { return (int)Math.Round(px * Scale); }

        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        static Font Get(string family, int px, bool bold) {
            string key = family + "|" + px + "|" + bold;
            Font f;
            if(fonts.TryGetValue(key, out f)) return f;
            f = new Font(family, px * Scale, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            fonts[key] = f;
            return f;
        }
        public static Font Font(int px, bool bold) { return Get("Segoe UI", px, bold); }
        public static Font Mono(int px, bool bold) { return Get("Consolas", px, bold); }
        // Windows' built-in icon font (Windows 10/11). Glyph codes are MDL2.
        public static Font Icon(int px) { return Get("Segoe MDL2 Assets", px, false); }

        // Glyph codes used by the UI.
        public const string GlyphMonitor = ""; // TVMonitor
        public const string GlyphExternal = ""; // OpenInNewWindow
        public const string GlyphRestore = ""; // Refresh
        public const string GlyphDiag = ""; // Diagnostic
        public const string GlyphDown = ""; // ChevronDown
        public const string GlyphUp = ""; // ChevronUp

        public static GraphicsPath Round(Rectangle r, int radius) {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            if(d <= 0 || d > r.Width || d > r.Height) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
        public static LinearGradientBrush Accent(Rectangle r) {
            if(r.Width < 1) r.Width = 1;
            if(r.Height < 1) r.Height = 1;
            return new LinearGradientBrush(r, AccentA, AccentB, 18f);
        }
    }

    // Small filled circle used for macOS-style window controls and status dots.
    class RoundDot : Control {
        public Color Dot;
        public RoundDot(Color dot, Color back, int px) {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Dot = dot; BackColor = back; Size = new Size(px, px);
        }
        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            using(SolidBrush b = new SolidBrush(Dot)) g.FillEllipse(b, 0, 0, Width - 1, Height - 1);
        }
    }

    // Rounded pill: coloured dot + label, tinted background and border.
    class StatusPill : Control {
        string text = "";
        Color dot = Theme.GrayChip, fg = Theme.TextSecondary, fill = Theme.Surface, edge = Theme.Border;
        public StatusPill() {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = Theme.Sc(24);
        }
        public void SetState(string label, Color dotColor, Color textColor, Color bgColor, Color borderColor) {
            text = label; dot = dotColor; fg = textColor; fill = bgColor; edge = borderColor;
            int w = TextRenderer.MeasureText(text, Theme.Font(12, true)).Width;
            Size = new Size(Theme.Sc(30) + w, Theme.Sc(24));
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : BackColor);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using(GraphicsPath p = Theme.Round(r, Height / 2)) {
                using(SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                using(Pen pen = new Pen(edge)) g.DrawPath(pen, p);
            }
            int dotSize = Theme.Sc(7);
            using(SolidBrush b = new SolidBrush(dot))
                g.FillEllipse(b, Theme.Sc(11), (Height - dotSize) / 2, dotSize, dotSize);
            TextRenderer.DrawText(g, text, Theme.Font(12, true),
                new Rectangle(Theme.Sc(24), 0, Width - Theme.Sc(24), Height), fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }

    // Accessible toggle (CheckBox) painted as a capsule switch; accent when on.
    class ToggleSwitch : CheckBox {
        Color onColorA = Theme.AccentA, onColorB = Theme.AccentB;
        public ToggleSwitch() {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoCheck = false; // state is driven by the app, not by the click itself
            Text = ""; Size = new Size(Theme.Sc(40), Theme.Sc(22)); Cursor = Cursors.Hand;
        }
        public void SetAccent(Color a, Color b) { onColorA = a; onColorB = b; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : BackColor);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using(GraphicsPath p = Theme.Round(r, Height / 2)) {
                if(Checked) {
                    using(LinearGradientBrush b = new LinearGradientBrush(r, onColorA, onColorB, 12f)) g.FillPath(b, p);
                } else {
                    using(SolidBrush b = new SolidBrush(Color.FromArgb(43, 55, 72))) g.FillPath(b, p);
                    using(Pen pen = new Pen(Theme.BorderStrong)) g.DrawPath(pen, p);
                }
            }
            int knob = Height - Theme.Sc(6);
            int y = Theme.Sc(3);
            int x = Checked ? Width - knob - Theme.Sc(3) : Theme.Sc(3);
            using(SolidBrush b = new SolidBrush(Checked ? Color.White : Color.FromArgb(198, 207, 219)))
                g.FillEllipse(b, x, y, knob, knob);
        }
    }

    // Flat rounded button. Kind 0 = ghost (bordered), 1 = accent (gradient),
    // 2 = sunken field (left-aligned text + chevron, for the display selector).
    class ThemeButton : Button {
        public int Kind = 0;
        public string Glyph = null;       // leading MDL2 glyph
        public string TrailGlyph = null;  // right-aligned MDL2 glyph
        public ThemeButton() {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }
        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : BackColor);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fg;
            if(Kind == 2) {
                using(GraphicsPath p = Theme.Round(r, Theme.Sc(4))) {
                    using(SolidBrush b = new SolidBrush(Theme.Sunken)) g.FillPath(b, p);
                    using(Pen pen = new Pen(Theme.BorderStrong)) g.DrawPath(pen, p);
                }
                int pad = Theme.Sc(12);
                int tx = pad;
                if(!string.IsNullOrEmpty(Glyph)) {
                    Font icf2 = Theme.Icon(14);
                    int gw2 = TextRenderer.MeasureText(Glyph, icf2, new Size(200, 200), TextFormatFlags.NoPadding).Width;
                    TextRenderer.DrawText(g, Glyph, icf2, new Rectangle(pad, 0, gw2, Height), Theme.TextLabel,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    tx = pad + gw2 + Theme.Sc(9);
                }
                TextRenderer.DrawText(g, Text, Font,
                    new Rectangle(tx, 0, Width - tx - Theme.Sc(28), Height), Theme.TextPrimary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                using(Pen pen = new Pen(Theme.TextLabel, Theme.Scale * 1.3f)) {
                    int cx = Width - Theme.Sc(18), cy = Height / 2 - Theme.Sc(1), s = Theme.Sc(4);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.DrawLines(pen, new Point[] { new Point(cx - s, cy - s / 2), new Point(cx, cy + s / 2), new Point(cx + s, cy - s / 2) });
                }
                return;
            }
            using(GraphicsPath p = Theme.Round(r, Theme.Sc(5))) {
                if(Kind == 1) {
                    using(LinearGradientBrush b = Theme.Accent(r)) g.FillPath(b, p);
                    fg = Theme.DarkInk;
                } else {
                    using(LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(43, 54, 68), Color.FromArgb(35, 45, 58), 90f)) g.FillPath(b, p);
                    using(Pen pen = new Pen(Theme.BorderStrong)) g.DrawPath(pen, p);
                    fg = Enabled ? Theme.TextPrimary : Theme.TextMuted;
                }
            }
            Font icf = Theme.Icon(14);
            int gw = 0, gap = 0;
            if(!string.IsNullOrEmpty(Glyph)) {
                gw = TextRenderer.MeasureText(Glyph, icf, new Size(200, 200), TextFormatFlags.NoPadding).Width;
                gap = Theme.Sc(8);
            }
            int tw = TextRenderer.MeasureText(Text, Font, new Size(2000, 2000), TextFormatFlags.NoPadding).Width;
            int sx = (Width - (gw + gap + tw)) / 2;
            if(sx < Theme.Sc(8)) sx = Theme.Sc(8);
            if(gw > 0)
                TextRenderer.DrawText(g, Glyph, icf, new Rectangle(sx, 0, gw, Height), fg,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(sx + gw + gap, 0, Width - sx - gw - gap, Height), fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            if(!string.IsNullOrEmpty(TrailGlyph))
                TextRenderer.DrawText(g, TrailGlyph, icf, new Rectangle(0, 0, Width - Theme.Sc(12), Height), fg,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    // Dark checkbox with a tinted check, used for the Advanced fallback option.
    class ThemeCheckBox : CheckBox {
        public Color AccentColor = Theme.AccentA;
        public ThemeCheckBox() {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoCheck = false; AutoSize = false; Cursor = Cursors.Hand;
            Height = Theme.Sc(22);
        }
        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : BackColor);
            int box = Theme.Sc(16);
            int by = (Height - box) / 2;
            Rectangle br = new Rectangle(0, by, box, box);
            using(GraphicsPath p = Theme.Round(br, Theme.Sc(3))) {
                if(Checked) {
                    using(SolidBrush b = new SolidBrush(AccentColor)) g.FillPath(b, p);
                } else {
                    using(SolidBrush b = new SolidBrush(Theme.Sunken)) g.FillPath(b, p);
                    using(Pen pen = new Pen(Theme.BorderStrong)) g.DrawPath(pen, p);
                }
            }
            if(Checked) {
                using(Pen pen = new Pen(Theme.DarkInk, Theme.Scale * 1.8f)) {
                    g.DrawLines(pen, new Point[] {
                        new Point(br.X + box / 4, br.Y + box / 2),
                        new Point(br.X + box / 2 - Theme.Sc(1), br.Bottom - box / 4),
                        new Point(br.Right - box / 5, br.Y + box / 4)
                    });
                }
            }
            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(box + Theme.Sc(8), 0, Width - box - Theme.Sc(8), Height),
                Theme.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }

    // Preset card. Shows badge letter, title, spec line and a sub line;
    // the active card gets a gradient border and an ACTIVE pill.
    class PresetCardButton : Button {
        public string Letter = "D";
        public string TitleText = "Desktop";
        public string SpecText = "";
        public string SubText = "";
        public bool Active = false;
        public Color BadgeColor = Theme.Blue;
        public PresetCardButton() {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }
        public void SetActive(bool active) { if(Active != active) { Active = active; Invalidate(); } }
        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : BackColor);
            Rectangle outer = new Rectangle(0, 0, Width - 1, Height - 1);
            Rectangle inner = outer;
            if(Active) {
                using(GraphicsPath op = Theme.Round(outer, Theme.Sc(6)))
                using(LinearGradientBrush b = Theme.Accent(outer)) g.FillPath(b, op);
                int inset = Theme.Sc(2);
                inner = new Rectangle(inset, inset, Width - 1 - inset * 2, Height - 1 - inset * 2);
                using(GraphicsPath ip = Theme.Round(inner, Theme.Sc(5)))
                using(SolidBrush b = new SolidBrush(Theme.CardBg)) g.FillPath(b, ip);
            } else {
                using(GraphicsPath ip = Theme.Round(inner, Theme.Sc(5))) {
                    using(SolidBrush b = new SolidBrush(Theme.Surface)) g.FillPath(b, ip);
                    using(Pen pen = new Pen(Theme.BorderStrong)) g.DrawPath(pen, ip);
                }
            }
            int pad = Theme.Sc(11);
            int x = inner.X + pad;
            int y = inner.Y + pad;
            // Badge
            int badge = Theme.Sc(22);
            Rectangle br = new Rectangle(x, y, badge, badge);
            using(GraphicsPath bp = Theme.Round(br, Theme.Sc(5)))
            using(SolidBrush b = new SolidBrush(Color.FromArgb(40, BadgeColor.R, BadgeColor.G, BadgeColor.B))) g.FillPath(b, bp);
            TextRenderer.DrawText(g, Letter, Theme.Font(11, true), br, BadgeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            // Title
            TextRenderer.DrawText(g, TitleText, Theme.Font(14, true),
                new Rectangle(x + badge + Theme.Sc(8), y, inner.Width - badge - Theme.Sc(60), badge), Theme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            // ACTIVE pill
            if(Active) {
                string pill = "ACTIVE";
                Size ps = TextRenderer.MeasureText(pill, Theme.Font(9, true));
                Rectangle pr = new Rectangle(inner.Right - pad - ps.Width - Theme.Sc(16), y + Theme.Sc(1), ps.Width + Theme.Sc(16), Theme.Sc(18));
                using(GraphicsPath pp = Theme.Round(pr, Theme.Sc(9)))
                using(LinearGradientBrush b = Theme.Accent(pr)) g.FillPath(b, pp);
                TextRenderer.DrawText(g, pill, Theme.Font(9, true), pr, Theme.DarkInk,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            // Spec + sub
            int ty = y + badge + Theme.Sc(8);
            TextRenderer.DrawText(g, SpecText, Theme.Mono(12, false),
                new Rectangle(x, ty, inner.Width - pad * 2, Theme.Sc(16)), Theme.TextBright, TextFormatFlags.Left);
            TextRenderer.DrawText(g, SubText, Theme.Font(11, false),
                new Rectangle(x, ty + Theme.Sc(18), inner.Width - pad * 2, Theme.Sc(15)), Theme.TextMuted, TextFormatFlags.Left);
        }
    }

    // Classic-Windows grouped frame: rounded border with a legend on the edge.
    class GroupPanel : Panel {
        public string TitleText = "";
        public Color TitleColor = Theme.TextLabel;
        public Color BorderColor = Theme.Border;
        public GroupPanel() {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.WindowBg;
        }
        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            int top = Theme.Sc(8);
            Rectangle r = new Rectangle(0, top, Width - 1, Height - top - 1);
            using(GraphicsPath p = Theme.Round(r, Theme.Sc(5)))
            using(Pen pen = new Pen(BorderColor)) g.DrawPath(pen, p);
            if(!string.IsNullOrEmpty(TitleText)) {
                Font f = Theme.Font(11, true);
                Size ts = TextRenderer.MeasureText(g, TitleText, f);
                int lx = Theme.Sc(12);
                Rectangle clear = new Rectangle(lx - Theme.Sc(4), top - ts.Height / 2, ts.Width + Theme.Sc(8), ts.Height);
                using(SolidBrush b = new SolidBrush(BackColor)) g.FillRectangle(b, clear);
                TextRenderer.DrawText(g, TitleText, f, new Point(lx, top - ts.Height / 2), TitleColor);
            }
        }
    }

    // Borderless, rounded window with a macOS-style traffic-light title bar and
    // a scrollable body. Windows-app identity (icon + name) sits alongside.
    class ChromeForm : Form {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public Panel Body;
        Panel titleBar;
        int titleH;

        public ChromeForm(string title, int wPx, int hPx) {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.WindowBg;
            ForeColor = Theme.TextPrimary;
            Font = Theme.Font(12, false);
            KeyPreview = true;
            ClientSize = new Size(Theme.Sc(wPx), Theme.Sc(hPx));
            titleH = Theme.Sc(36);

            Body = new Panel();
            Body.Dock = DockStyle.Fill;
            Body.BackColor = Theme.WindowBg;
            Body.Padding = new Padding(Theme.Sc(15), Theme.Sc(13), Theme.Sc(15), Theme.Sc(13));
            Body.AutoScroll = true;

            titleBar = new Panel();
            titleBar.Dock = DockStyle.Top;
            titleBar.Height = titleH;
            titleBar.BackColor = Theme.TitleBg;
            titleBar.Paint += new PaintEventHandler(PaintTitle);
            titleBar.MouseDown += new MouseEventHandler(DragTitle);

            int cy = (titleH - Theme.Sc(12)) / 2;
            int lx = Theme.Sc(14);
            RoundDot red = new RoundDot(Theme.TrafficRed, Theme.TitleBg, Theme.Sc(12));
            red.Location = new Point(lx, cy); red.Cursor = Cursors.Hand;
            red.Click += delegate { this.Close(); };
            RoundDot amber = new RoundDot(Theme.TrafficAmber, Theme.TitleBg, Theme.Sc(12));
            amber.Location = new Point(lx + Theme.Sc(20), cy); amber.Cursor = Cursors.Hand;
            amber.Click += delegate { this.WindowState = FormWindowState.Minimized; };
            RoundDot green = new RoundDot(Theme.TrafficGreen, Theme.TitleBg, Theme.Sc(12));
            green.Location = new Point(lx + Theme.Sc(40), cy);

            Panel icon = new Panel();
            icon.Size = new Size(Theme.Sc(18), Theme.Sc(18));
            icon.Location = new Point(lx + Theme.Sc(62), (titleH - Theme.Sc(18)) / 2);
            icon.BackColor = Theme.TitleBg;
            icon.Paint += new PaintEventHandler(PaintIcon);
            icon.MouseDown += new MouseEventHandler(DragTitle);

            Label name = new Label();
            name.AutoSize = true;
            name.Text = "MPC Movie Tray";
            name.ForeColor = Theme.TextBright;
            name.BackColor = Theme.TitleBg;
            name.Font = Theme.Font(12, true);
            name.Location = new Point(lx + Theme.Sc(86), (titleH - name.PreferredHeight) / 2 + Theme.Sc(1));
            name.MouseDown += new MouseEventHandler(DragTitle);

            Label caption = new Label();
            caption.AutoSize = true;
            caption.Text = title;
            caption.ForeColor = Theme.TextMuted;
            caption.BackColor = Theme.TitleBg;
            caption.Font = Theme.Font(11, false);
            caption.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            titleBar.Controls.Add(red);
            titleBar.Controls.Add(amber);
            titleBar.Controls.Add(green);
            titleBar.Controls.Add(icon);
            titleBar.Controls.Add(name);
            titleBar.Controls.Add(caption);
            titleBar.Resize += delegate {
                caption.Location = new Point(titleBar.Width - caption.PreferredWidth - Theme.Sc(14), (titleH - caption.PreferredHeight) / 2);
            };

            Controls.Add(Body);
            Controls.Add(titleBar);
        }

        public void SetClientHeight(int hPx) {
            ClientSize = new Size(ClientSize.Width, Theme.Sc(hPx));
            ApplyRegion();
        }
        // Set client height directly in device pixels, clamped to the screen; the
        // body scrolls if content is taller than the clamp.
        public void SetClientPixelHeight(int px) {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int max = wa.Height - Theme.Sc(20);
            if(px > max) px = max;
            if(px < Theme.Sc(120)) px = Theme.Sc(120);
            ClientSize = new Size(ClientSize.Width, px);
            ApplyRegion();
            CenterToScreen();
        }

        void DragTitle(object sender, MouseEventArgs e) {
            if(e.Button == MouseButtons.Left) {
                ReleaseCapture();
                SendMessage(this.Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero);
            }
        }
        void PaintTitle(object sender, PaintEventArgs e) {
            using(Pen p = new Pen(Theme.Border))
                e.Graphics.DrawLine(p, 0, titleBar.Height - 1, titleBar.Width, titleBar.Height - 1);
        }
        void PaintIcon(object sender, PaintEventArgs e) {
            Panel icon = (Panel)sender;
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.TitleBg);
            Rectangle r = new Rectangle(0, 0, icon.Width - 1, icon.Height - 1);
            using(GraphicsPath p = Theme.Round(r, Theme.Sc(4)))
            using(LinearGradientBrush b = Theme.Accent(r)) g.FillPath(b, p);
            TextRenderer.DrawText(g, "HDR", Theme.Font(8, true), r, Theme.DarkInk,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        protected override CreateParams CreateParams {
            get { CreateParams cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } // drop shadow
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyRegion(); }
        void ApplyRegion() {
            using(GraphicsPath p = Theme.Round(new Rectangle(0, 0, Width, Height), Theme.Sc(8)))
                this.Region = new Region(p);
        }
    }

    // Dark colour table + renderer for the tray ContextMenuStrip.
    class DarkColorTable : ProfessionalColorTable {
        public override Color ToolStripDropDownBackground { get { return Theme.MenuBg; } }
        public override Color ImageMarginGradientBegin { get { return Theme.MenuBg; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.MenuBg; } }
        public override Color ImageMarginGradientEnd { get { return Theme.MenuBg; } }
        public override Color MenuBorder { get { return Theme.BorderStrong; } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color MenuItemSelected { get { return Color.FromArgb(40, 51, 66); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(40, 51, 66); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(40, 51, 66); } }
        public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(34, 44, 58); } }
        public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(34, 44, 58); } }
        public override Color CheckBackground { get { return Color.FromArgb(30, 51, 184, 232); } }
        public override Color CheckSelectedBackground { get { return Color.FromArgb(45, 51, 184, 232); } }
        public override Color CheckPressedBackground { get { return Color.FromArgb(45, 51, 184, 232); } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Color.Transparent; } }
    }

    class DarkMenuRenderer : ToolStripProfessionalRenderer {
        public DarkMenuRenderer() : base(new DarkColorTable()) { RoundedEdges = true; }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {
            e.TextColor = e.Item.Enabled ? Theme.TextBright : Theme.TextMuted;
            base.OnRenderItemText(e);
        }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) {
            e.ArrowColor = Theme.TextSecondary;
            base.OnRenderArrow(e);
        }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) {
            Rectangle r = e.ImageRectangle;
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using(Pen pen = new Pen(Theme.AccentA, Theme.Scale * 1.7f)) {
                int x = r.X + r.Width / 4, y = r.Y + r.Height / 2;
                g.DrawLines(pen, new Point[] {
                    new Point(x, y),
                    new Point(r.X + r.Width / 2 - Theme.Sc(1), r.Bottom - r.Height / 4),
                    new Point(r.Right - r.Width / 4, r.Y + r.Height / 4)
                });
            }
        }
    }
}
