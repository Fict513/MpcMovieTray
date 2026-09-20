using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace MpcMovieDisplay {
    static class Program {
        public static string MutexName { get { return "Local\\MpcMovieDisplay-"+WindowsIdentity.GetCurrent().User.Value; } }
        [STAThread] static void Main(string[] args) {
            if(args.Length>0 && args[0]=="--guard") { Guard(args); return; }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            // Catch everything on the UI thread so failures report a full stack
            // trace instead of the generic .NET crash dialog.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException+=delegate(object sender,ThreadExceptionEventArgs te) { ReportFatal(te.Exception); };
            AppDomain.CurrentDomain.UnhandledException+=delegate(object sender,UnhandledExceptionEventArgs ue) { ReportFatal(ue.ExceptionObject as Exception); };
            if(args.Length>0 && args[0]=="--uitest") {
                try { UiTest(); } catch(Exception e) { ReportFatal(e); }
                return;
            }
            bool locked=false;
            using(Mutex mutex=new Mutex(false,MutexName)) {
                try {
                    try { locked=mutex.WaitOne(0); } catch(AbandonedMutexException) { locked=true; }
                    if(!locked) { MessageBox.Show("MPC Movie Tray, the old script, or a recovery helper is already running. Use the existing tray icon. If recovering, close MPC and wait a few seconds.","MPC Movie Tray"); return; }
                    if(!Environment.Is64BitProcess) throw new Exception("This application needs 64-bit Windows.");
                    Application.Run(new TrayContext());
                } catch(Exception e) {
                    ReportFatal(e);
                } finally { Native.Close(); if(locked) mutex.ReleaseMutex(); }
            }
        }
        static bool reported;
        static void ReportFatal(Exception e) {
            string detail = e==null ? "Unknown error (no exception object)." : e.ToString();
            try { Store.Log("FATAL: "+detail); } catch { }
            if(reported) return;
            reported=true;
            try {
                MessageBox.Show(
                    "MPC Movie Tray hit an unexpected error. Press Ctrl+C to copy this text."
                    +"\n\nIt is also written to:\n"+Store.FilePath("activity.log")
                    +"\n\nAny saved recovery data is retained."
                    +"\n\n"+detail,
                    "MPC Movie Tray",MessageBoxButtons.OK,MessageBoxIcon.Error);
            } catch { }
        }
        // UI test mode: exercises the settings window and the 15-second confirm
        // dialog with fake data. Makes NO NVIDIA or display API calls, takes no
        // single-instance lock and starts no recovery helper. Nothing is saved.
        static void UiTest() {
            SettingsForm ui=null;
            bool movieActive=false, automatic=true, fallback=false, startup=false;
            int hdrState=1; // 0 = On, 1 = Off, 2 = Unknown
            SettingsCallbacks cb=new SettingsCallbacks();
            cb.Movie=delegate { if(Dialogs.Confirm("4K30 - RGB Full - 10-bit")) movieActive=true; };
            cb.Desktop=delegate { if(Dialogs.Confirm(fallback?"4K60 - YCbCr 4:2:0 - 8-bit":"4K60 - RGB Full - 8-bit")) movieActive=false; };
            cb.RestoreDesktop=cb.Desktop;
            cb.ToggleAutomatic=delegate { automatic=!automatic; };
            cb.ToggleFallback=delegate { fallback=!fallback; };
            cb.ToggleStartup=delegate { startup=!startup; if(ui!=null && !ui.IsDisposed) ui.SetStartup(startup); };
            cb.OpenHdr=delegate { hdrState=(hdrState+1)%3; }; // cycles On / Off / Unknown
            cb.ChooseDisplay=delegate { MessageBox.Show("UI test mode: no display is selected or changed.","MPC Movie Tray"); };
            cb.OpenLog=delegate { MessageBox.Show("UI test mode: the activity log is not used.","MPC Movie Tray"); };
            cb.OpenReadme=delegate { MessageBox.Show("UI test mode: the README is not opened.","MPC Movie Tray"); };
            cb.TestAgain=delegate { MessageBox.Show("UI test mode: nothing is stored.","MPC Movie Tray"); };
            ui=new SettingsForm(cb);
            ui.SetStartup(startup);
            System.Windows.Forms.Timer feed=new System.Windows.Forms.Timer();
            feed.Interval=500;
            feed.Tick+=delegate {
                if(ui==null || ui.IsDisposed) return;
                StatusView v=new StatusView();
                v.DisplayName="\\\\.\\DISPLAY2   (UI TEST - no hardware)";
                v.Width=3840; v.Height=2160;
                v.Freq=movieActive?30:60;
                v.Format=(!movieActive && fallback)?"YCbCr 4:2:0":"RGB";
                v.Range=(!movieActive && fallback)?"Limited":"Full";
                v.Bits=movieActive?"10":"8";
                v.Hdr=hdrState==0?"On":hdrState==1?"Off":"Unknown (advanced colour on)";
                v.Control=automatic?"Automatic":"Manual";
                v.Mpc=movieActive?1:0;
                v.MatchesMovie=movieActive; v.MatchesDesktop=!movieActive;
                v.Automatic=automatic; v.Fallback=fallback;
                ui.SetStatus(v);
            };
            feed.Start();
            Application.Run(ui);
            feed.Stop(); feed.Dispose();
        }
        static void Guard(string[] args) {
            try {
                int pid=int.Parse(args[1]); long ticks=long.Parse(args[2]); string token=args[3];
                if(token.Length!=32 || !System.Text.RegularExpressions.Regex.IsMatch(token,"^[a-f0-9]+$")) return;
                Process owner=null;
                try {
                    Process p=Process.GetProcessById(pid);
                    if(p.StartTime.ToUniversalTime().Ticks==ticks) { IntPtr h=p.Handle; owner=p; }
                    else p.Dispose();
                } catch { }
                Directory.CreateDirectory(Store.Folder);
                File.WriteAllText(Store.FilePath(token+".ready"),"ready");
                if(owner!=null) using(owner) {
                    while(!owner.WaitForExit(1000)) {
                        // UI confirmation normally reverts in 15 seconds. This is the
                        // independent last resort if the app hangs with a changed mode.
                        Snapshot pending=null;
                        try { pending=Store.Read<Snapshot>("transition.json"); } catch { continue; }
                        if(pending!=null && pending.DeadlineUtcTicks>0 && DateTime.UtcNow.Ticks>pending.DeadlineUtcTicks) {
                            Store.Log("Unresponsive display test: terminating our owner process for rollback.");
                            try { owner.Kill(); owner.WaitForExit(); } catch { }
                            break;
                        }
                    }
                }
                bool locked=false;
                using(Mutex mutex=new Mutex(false,MutexName)) {
                    try {
                        try { locked=mutex.WaitOne(10000); } catch(AbandonedMutexException) { locked=true; }
                        if(!locked) return; // A new instance owns recovery now.
                        Snapshot pending=Store.Read<Snapshot>("transition.json");
                        if(pending!=null) {
                            RetryRestore(pending);
                            Store.Delete("transition.json");
                        }
                        Snapshot session=Store.Read<Snapshot>("session.json");
                        if(session!=null) {
                            if(session.WaitForPlayer) {
                                int empty=0;
                                while(empty<2) { int n=Players.Count(); empty=n==0?empty+1:0; if(empty<2) Thread.Sleep(1000); }
                            }
                            RetryRestore(session);
                            Store.Delete("session.json");
                        }
                    } finally { Native.Close(); if(locked) mutex.ReleaseMutex(); }
                }
                Store.Delete(token+".ready");
            } catch(Exception e) { Store.Log("Recovery pending: "+e); }
        }
        static void RetryRestore(Snapshot s) {
            for(int i=0;i<3;i++) {
                try { Profiles.Restore(s); Store.Log("Recovery restored saved output."); return; }
                catch { if(i==2) throw; Thread.Sleep(2000); }
            }
        }
    }
}
