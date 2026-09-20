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
            bool locked=false;
            using(Mutex mutex=new Mutex(false,MutexName)) {
                try {
                    try { locked=mutex.WaitOne(0); } catch(AbandonedMutexException) { locked=true; }
                    if(!locked) { MessageBox.Show("MPC Movie Tray, the old script, or a recovery helper is already running. Use the existing tray icon. If recovering, close MPC and wait a few seconds.","MPC Movie Tray"); return; }
                    if(!Environment.Is64BitProcess) throw new Exception("This application needs 64-bit Windows.");
                    Application.Run(new TrayContext());
                } catch(Exception e) {
                    Store.Log("Fatal: "+e);
                    MessageBox.Show(e.Message+"\n\nAny saved recovery data will be retained.","MPC Movie Tray",MessageBoxButtons.OK,MessageBoxIcon.Error);
                } finally { Native.Close(); if(locked) mutex.ReleaseMutex(); }
            }
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
