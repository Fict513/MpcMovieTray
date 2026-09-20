using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MpcMovieDisplay {
    sealed class TrayContext : ApplicationContext {
        Settings settings;
        NotifyIcon tray;
        ContextMenuStrip menu;
        ToolStripMenuItem automatic,statusLine,hdrLine,startup,desktop420;
        Timer poll,click;
        Icon movieIcon,desktopIcon,unknownIcon;
        Process guard;
        bool busy,closing,movieOwned,initial=true;
        int emptySamples;
        string lastHdr="",lastFault="";
        SettingsForm ui;
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);

        public TrayContext() {
            try { settings=Store.Read<Settings>("settings.json") ?? new Settings(); }
            catch(Exception e) { Store.Log("Settings reset after read error: "+e.Message);settings=new Settings(); }
            if(settings.Confirmed==null) settings.Confirmed=new System.Collections.Generic.List<string>();
            movieIcon=MakeIcon(Color.FromArgb(52,190,115),"M");
            desktopIcon=MakeIcon(Color.FromArgb(50,146,240),"D");
            unknownIcon=MakeIcon(Color.FromArgb(235,165,45),"?");
            menu=new ContextMenuStrip();
            menu.Renderer=new DarkMenuRenderer();
            menu.BackColor=Theme.MenuBg;
            menu.ForeColor=Theme.TextPrimary;
            menu.Font=Theme.Font(12,false);
            menu.ShowImageMargin=true;
            statusLine=Add("Reading display...",null); statusLine.Enabled=false;
            hdrLine=Add("Windows HDR: checking...",null); hdrLine.Enabled=false;
            menu.Items.Add(new ToolStripSeparator());
            Add("Movie: 4K"+settings.MovieHz+" / RGB Full / 10-bit",(s,e)=>Manual(true));
            Add("Desktop: 4K60 / 8-bit",(s,e)=>Manual(false));
            automatic=Add("Automatic while MPC is open",(s,e)=>SetAutomatic(!settings.Automatic));
            automatic.Checked=settings.Automatic;
            menu.Items.Add(new ToolStripSeparator());
            Add("Show live status / controls",(s,e)=>ShowPanel());
            Add("Choose TV / monitor...",(s,e)=>ChangeDisplay());
            desktop420=Add("Desktop fallback: YCbCr 4:2:0 Limited",(s,e)=>ChangeFallback());
            desktop420.Checked=settings.Desktop420;
            Add("Test next switch again (15-second rollback)",(s,e)=>{
                settings.Confirmed.Clear();Save();Toast("Preset tests reset","The next use of each preset will ask you to keep or revert it.");
            });
            Add("Open Windows HDR settings",(s,e)=>Open("ms-settings:display-hdr"));
            startup=Add("Start with Windows",(s,e)=>ToggleStartup());
            startup.Checked=StartupEnabled();
            Add("Open README",(s,e)=>Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"README.txt")));
            Add("Open activity log",(s,e)=>{Store.Log("Log opened.");Open(Store.FilePath("activity.log"));});
            menu.Items.Add(new ToolStripSeparator());
            Add("Exit (restore desktop)",(s,e)=>ExitToDesktop());
            tray=new NotifyIcon {Visible=true,Icon=unknownIcon,Text="MPC Movie Tray",ContextMenuStrip=menu};
            click=new Timer {Interval=SystemInformation.DoubleClickTime};
            click.Tick+=(s,e)=>{click.Stop();Manual(!IsMovie());};
            tray.MouseClick+=(s,e)=>{if(e.Button==MouseButtons.Left && !busy){click.Stop();click.Start();}};
            tray.BalloonTipClicked+=(s,e)=>ShowPanel();
            poll=new Timer {Interval=1000};
            poll.Tick+=(s,e)=>Tick(); poll.Start();
        }
        ToolStripMenuItem Add(string text,EventHandler action) {
            ToolStripMenuItem item=new ToolStripMenuItem(text);
            if(action!=null)item.Click+=(s,e)=>{try{action(s,e);}catch(Exception fault){Toast("Action failed",fault.Message,true);}};
            menu.Items.Add(item);return item;
        }
        static Icon MakeIcon(Color colour,string letter) {
            using(Bitmap b=new Bitmap(32,32)) {
                using(Graphics g=Graphics.FromImage(b)) {
                    g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using(Brush brush=new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(1,3,30,22),colour,
                        letter=="M"?Color.FromArgb(233,105,177):Color.FromArgb(130,160,190),0f)) {
                        g.FillRectangle(brush,1,3,30,22);
                        g.FillRectangle(brush,14,25,4,3);
                        g.FillRectangle(brush,8,28,16,3);
                    }
                    g.FillRectangle(Brushes.Black,4,6,24,16);
                    using(Font f=new Font("Segoe UI",16,FontStyle.Bold,GraphicsUnit.Pixel))
                    using(StringFormat sf=new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})
                        g.DrawString(letter,f,Brushes.White,new RectangleF(3,3,26,22),sf);
                }
                IntPtr h=b.GetHicon(); try {using(Icon temp=Icon.FromHandle(h))return (Icon)temp.Clone();}finally{DestroyIcon(h);}
            }
        }
        void Save() { Store.Save("settings.json",settings); }
        void Toast(string title,string text,bool error=false) {
            Store.Log(title+": "+text);
            tray.ShowBalloonTip(5000,title,text,error?ToolTipIcon.Warning:ToolTipIcon.Info);
        }
        void Open(string path) {
            try { Process.Start(new ProcessStartInfo(path){UseShellExecute=true}); }
            catch(Exception e) { Toast("Could not open",e.Message,true); }
        }
        void StartGuard() {
            string token=Guid.NewGuid().ToString("N");
            using(Process self=Process.GetCurrentProcess()) {
                guard=Process.Start(new ProcessStartInfo(Application.ExecutablePath,
                    "--guard "+self.Id+" "+self.StartTime.ToUniversalTime().Ticks+" "+token) {UseShellExecute=false,CreateNoWindow=true});
            }
            DateTime end=DateTime.UtcNow.AddSeconds(15);
            while(!Store.Exists(token+".ready")) {
                if(guard==null || guard.HasExited || DateTime.UtcNow>end) throw new Exception("The recovery helper could not start. No display changes were made.");
                System.Threading.Thread.Sleep(100);
            }
            Store.Delete(token+".ready");
        }
        bool Recover() {
            Snapshot pending=Store.Read<Snapshot>("transition.json");
            if(pending!=null) { Profiles.Restore(pending);Store.Delete("transition.json"); }
            Snapshot session=Store.Read<Snapshot>("session.json");
            if(session!=null) {
                while(session.WaitForPlayer && Players.Count()!=0) {
                    if(MessageBox.Show("A previous session needs recovery. Close all MPC instances, then click Retry. Cancel leaves recovery to the helper.","Pending recovery",MessageBoxButtons.RetryCancel,MessageBoxIcon.Information)==DialogResult.Cancel) return false;
                }
                Profiles.Restore(session);Store.Delete("session.json");
            }
            return true;
        }
        void Tick() {
            if(busy || closing)return;
            try {
                if(initial) {
                    initial=false;busy=true;
                    try {
                        StartGuard();
                        if(!Recover()) { Shutdown();return; }
                        if(string.IsNullOrEmpty(settings.DisplayName)) {
                            Display d=Dialogs.ChooseDisplay();
                            if(d==null){Shutdown();return;}
                            settings.DisplayName=d.Name;settings.Identity=d.Identity;Save();
                        }
                        Native.RequireIdentity(settings.DisplayName,settings.Identity);
                        Native.GetColor(settings.DisplayName);
                        Toast("MPC Movie Tray is ready","Click the icon to toggle. Right-click for Automatic mode, HDR status and settings.");
                    } finally {busy=false;}
                }
                if(guard==null || guard.HasExited) StartGuard();
                UpdateStatus();
                if(!settings.Automatic) return;
                int count=Players.Count();
                emptySamples=count==0?emptySamples+1:0;
                string action=Policy.Action(movieOwned,count,emptySamples);
                if(action=="Movie") Request(true);
                else if(action=="Desktop") Request(false);
            } catch(Exception e) {
                if(lastFault!=e.Message){lastFault=e.Message;Toast("Display control paused",e.Message,true);}
                settings.Automatic=false;automatic.Checked=false;
                statusLine.Text="Unavailable - choose/reconnect the TV";
                tray.Icon=unknownIcon;tray.Text="MPC Movie Tray: display unavailable";
                try{Save();}catch{}
            }
        }
        bool IsMovie() {
            try {return Policy.Matches(Native.Current(settings.DisplayName),Native.GetColor(settings.DisplayName),true,settings);}catch{return false;}
        }
        void UpdateStatus() {
            Native.RequireIdentity(settings.DisplayName,settings.Identity);
            Mode m=Native.Current(settings.DisplayName);NvidiaColor c=Native.GetColor(settings.DisplayName);
            string bits=c.Bpc==1?"6":c.Bpc==2?"8":c.Bpc==3?"10":c.Bpc==4?"12":c.Bpc==5?"16":"Auto";
            string format=c.Format==0?"RGB":c.Format==1?"YCbCr 4:2:2":c.Format==2?"YCbCr 4:4:4":c.Format==3?"YCbCr 4:2:0":"Auto";
            string range=c.DynamicRange==0?"Full":c.DynamicRange==1?"Limited":"Auto";
            string hdr=HdrStatus.Read(settings.DisplayName);
            bool movie=Policy.Matches(m,c,true,settings),desktop=Policy.Matches(m,c,false,settings);
            statusLine.Text=string.Format("{0} x {1} / {2} Hz / {3} / {4}-bit",m.Width,m.Height,m.Frequency,format,bits);
            hdrLine.Text="Windows HDR: "+hdr;
            string tip=string.Format("{0}Hz {1} {2}bit | HDR {3} | {4}",m.Frequency,format,bits,hdr,settings.Automatic?"Auto":"Manual");
            tray.Text=tip.Length>63?tip.Substring(0,63):tip;
            tray.Icon=movie?movieIcon:desktop?desktopIcon:unknownIcon;
            if(ui!=null && !ui.IsDisposed) {
                StatusView v=new StatusView();
                v.DisplayName=settings.DisplayName;
                v.Width=(int)m.Width; v.Height=(int)m.Height; v.Freq=m.Frequency;
                v.Format=format; v.Range=range; v.Bits=bits; v.Hdr=hdr;
                v.Control=settings.Automatic?"Automatic":"Manual";
                v.Mpc=Players.Count();
                v.MatchesMovie=movie; v.MatchesDesktop=desktop;
                v.Automatic=settings.Automatic; v.Fallback=settings.Desktop420;
                ui.SetStatus(v);
            }
            if(movie && hdr=="Off" && lastHdr!="Off") Toast("Movie mode: Windows HDR is off","10-bit output is active. Windows HDR is off; your player may enable it when playback begins.");
            if(movie && hdr.StartsWith("Unknown") && lastHdr!=hdr) Store.Log("Windows HDR could not be confirmed: "+hdr);
            lastHdr=hdr;lastFault="";
        }
        void SetAutomatic(bool enabled) {
            if(busy)return;
            settings.Automatic=enabled;automatic.Checked=enabled;emptySamples=0;
            Snapshot saved=Store.Read<Snapshot>("session.json");
            if(saved!=null){saved.WaitForPlayer=enabled;Store.Save("session.json",saved);}
            if(enabled) movieOwned=IsMovie();
            Save();Toast(enabled?"Automatic MPC control":"Manual control",enabled?"Movie mode while MPC is open; desktop after the last instance closes.":"Your selection stays in place until you change it or re-enable Automatic.");
        }
        void Manual(bool movie) {
            if(busy||closing)return;
            try {SetAutomatic(false);Request(movie);}catch(Exception e){Toast("Switch failed",e.Message,true);}
        }
        bool Request(bool movie) {
            if(busy)return false;
            busy=true;bool createdSession=false;
            try {
                if(guard==null||guard.HasExited) StartGuard();
                Native.RequireIdentity(settings.DisplayName,settings.Identity);
                string key=Profiles.Key(settings,movie);
                bool test=!settings.Confirmed.Contains(key);
                if(Policy.Matches(Native.Current(settings.DisplayName),Native.GetColor(settings.DisplayName),movie,settings) && !test) {
                    movieOwned=movie;if(!movie)Store.Delete("session.json");return true;
                }
                Snapshot before=Profiles.Capture(settings,settings.Automatic);
                if(movie&&!Store.Exists("session.json")){Store.Save("session.json",before);createdSession=true;}
                // Independent guard restores if this app hangs or is killed mid-switch.
                before.DeadlineUtcTicks=DateTime.UtcNow.AddSeconds(60).Ticks;
                Store.Save("transition.json",before);
                Store.Log("Applying "+(movie?"movie":"desktop")+" preset.");
                Profiles.Apply(settings,movie);
                bool keep=true;
                if(test) {
                    before.DeadlineUtcTicks=DateTime.UtcNow.AddSeconds(35).Ticks;
                    Store.Save("transition.json",before);
                    keep=Dialogs.Confirm(movie?"4K"+settings.MovieHz+" - RGB Full - 10-bit":settings.Desktop420?"4K60 - YCbCr 4:2:0 - 8-bit":"4K60 - RGB Full - 8-bit");
                }
                if(!keep) {
                    before.DeadlineUtcTicks=DateTime.UtcNow.AddSeconds(45).Ticks;Store.Save("transition.json",before);
                    Profiles.Restore(before);Store.Delete("transition.json");
                    if(createdSession)Store.Delete("session.json");
                    settings.Automatic=false;automatic.Checked=false;Save();
                    Toast("Previous setting restored","The test was cancelled or timed out. Automatic control is paused.");
                    return false;
                }
                Profiles.Verify(settings,movie);
                if(test){settings.Confirmed.Add(key);Save();}
                Store.Delete("transition.json");
                if(!movie)Store.Delete("session.json");
                movieOwned=movie;
                Toast(movie?"Movie mode verified":"Desktop mode verified",movie?"3840 x 2160, "+settings.MovieHz+" Hz, RGB Full, 10-bit.":"3840 x 2160, 60 Hz, "+(settings.Desktop420?"YCbCr 4:2:0 Limited":"RGB Full")+", 8-bit.");
                if(movie)lastHdr="";
                UpdateStatus();return true;
            } catch(Exception e) {
                Store.Log("Switch error: "+e);
                try {
                    Snapshot pending=Store.Read<Snapshot>("transition.json");
                    if(pending!=null){pending.DeadlineUtcTicks=DateTime.UtcNow.AddSeconds(45).Ticks;Store.Save("transition.json",pending);Profiles.Restore(pending);Store.Delete("transition.json");if(createdSession)Store.Delete("session.json");}
                } catch(Exception recovery) {Store.Log("Rollback pending: "+recovery);}
                settings.Automatic=false;automatic.Checked=false;try{Save();}catch{}
                MessageBox.Show(e.Message+"\n\nAutomatic control is paused. A rollback was attempted. If RGB at 4K60 is unavailable, select the explicit YCbCr 4:2:0 desktop fallback from the tray menu.","Preset could not be applied",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return false;
            } finally {busy=false;}
        }
        void ChangeFallback() {
            if(busy)return;
            SetAutomatic(false);settings.Desktop420=!settings.Desktop420;desktop420.Checked=settings.Desktop420;Save();
            Toast("Desktop preset updated",settings.Desktop420?"Fallback selected: 4K60 YCbCr 4:2:0 Limited 8-bit. Select Desktop to test it.":"Preferred desktop selected: 4K60 RGB Full 8-bit. Select Desktop to test it.");
        }
        void ChangeDisplay() {
            if(busy)return;
            try {
                if(Store.Exists("session.json") || Store.Exists("transition.json")) {
                    MessageBox.Show("Return the current display to Desktop before choosing another monitor.","Display still controlled");return;
                }
                busy=true;Display d=Dialogs.ChooseDisplay();if(d==null)return;
                settings.DisplayName=d.Name;settings.Identity=d.Identity;movieOwned=false;lastHdr="";Save();
            } catch(Exception e){Toast("Display selection failed",e.Message,true);}finally{busy=false;}
        }
        bool StartupEnabled() {
            using(RegistryKey key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                return key!=null && string.Equals(key.GetValue("MpcMovieTray") as string,"\""+Application.ExecutablePath+"\"",StringComparison.OrdinalIgnoreCase);
        }
        void ToggleStartup() {
            try {
                bool enable=!StartupEnabled();
                using(RegistryKey key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
                    if(enable)key.SetValue("MpcMovieTray","\""+Application.ExecutablePath+"\"");else key.DeleteValue("MpcMovieTray",false);
                }
                startup.Checked=enable;Toast("Windows startup",enable?"Enabled for your account. Keep this executable in this folder.":"Disabled.");
            }catch(Exception e){Toast("Startup setting failed",e.Message,true);}
        }
        void ShowPanel() {
            if(ui!=null && !ui.IsDisposed){ui.Show();ui.Activate();return;}
            SettingsCallbacks cb=new SettingsCallbacks();
            cb.Movie=delegate{Manual(true);};
            cb.Desktop=delegate{Manual(false);};
            cb.RestoreDesktop=delegate{Manual(false);};
            cb.ToggleAutomatic=delegate{SetAutomatic(!settings.Automatic);if(ui!=null && !ui.IsDisposed)ui.SetAutomatic(settings.Automatic);};
            cb.ToggleStartup=delegate{ToggleStartup();if(ui!=null && !ui.IsDisposed)ui.SetStartup(StartupEnabled());};
            cb.ToggleFallback=delegate{ChangeFallback();if(ui!=null && !ui.IsDisposed)ui.SetFallback(settings.Desktop420);};
            cb.OpenHdr=delegate{Open("ms-settings:display-hdr");};
            cb.ChooseDisplay=delegate{ChangeDisplay();};
            cb.OpenLog=delegate{Store.Log("Log opened.");Open(Store.FilePath("activity.log"));};
            cb.OpenReadme=delegate{Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"README.txt"));};
            cb.TestAgain=delegate{settings.Confirmed.Clear();Save();Toast("Preset tests reset","The next use of each preset will ask you to keep or revert it.");};
            ui=new SettingsForm(cb);
            ui.FormClosed+=delegate{ui=null;};
            ui.SetStartup(StartupEnabled());
            ui.SetMovieHz(settings.MovieHz);
            try{UpdateStatus();}catch{}
            ui.Show();ui.Activate();
        }
        void ExitToDesktop() {
            if(busy||closing)return;
            bool wasAutomatic=settings.Automatic;
            settings.Automatic=false;automatic.Checked=false;
            if(Request(false)){settings.Automatic=wasAutomatic;Save();Shutdown();}
        }
        void Shutdown() {
            closing=true;poll.Stop();click.Stop();tray.Visible=false;
            if(ui!=null)ui.Close();
            tray.Dispose();menu.Dispose();poll.Dispose();click.Dispose();movieIcon.Dispose();desktopIcon.Dispose();unknownIcon.Dispose();
            ExitThread();
        }
    }
}
