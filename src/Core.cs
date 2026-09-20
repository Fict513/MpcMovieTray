using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;

namespace MpcMovieDisplay {
    [DataContract] public class Settings {
        [DataMember] public string DisplayName="";
        [DataMember] public string Identity="";
        [DataMember] public bool Automatic=true;
        [DataMember] public bool Desktop420=false;
        // Movie-preset refresh rate. 24 = 24.000 Hz; 23 = 23.976 Hz as Windows
        // reports it. Editable in settings.json without rebuilding.
        [DataMember] public uint MovieHz=24;
        [DataMember] public List<string> Confirmed=new List<string>();
    }
    [DataContract] public class Snapshot {
        [DataMember] public string Name;
        [DataMember] public string Identity;
        [DataMember] public string Mode;
        [DataMember] public string Color;
        [DataMember] public bool WaitForPlayer;
        [DataMember] public long DeadlineUtcTicks;
    }
    public static class Store {
        public static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MpcMovieTray");
        public static string FilePath(string name) { return Path.Combine(Folder,name); }
        public static void Log(string message) {
            try { Directory.CreateDirectory(Folder); File.AppendAllText(FilePath("activity.log"),DateTime.Now.ToString("s")+" "+message+Environment.NewLine); } catch { }
        }
        public static T Read<T>(string file) where T:class {
            string path=FilePath(file);
            if(!File.Exists(path)) return null;
            using(FileStream s=File.OpenRead(path)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(s);
        }
        public static void Save<T>(string file,T value) {
            Directory.CreateDirectory(Folder);
            string path=FilePath(file),temp=path+".tmp";
            using(FileStream s=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {
                new DataContractJsonSerializer(typeof(T)).WriteObject(s,value); s.Flush(true);
            }
            if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
        }
        public static void Delete(string file) { if(File.Exists(FilePath(file))) File.Delete(FilePath(file)); }
        public static bool Exists(string file) { return File.Exists(FilePath(file)); }
    }
    public static class Players {
        public static bool IsMpc(string name) {
            return new[]{"mpc-hc","mpc-hc64","mpc-be","mpc-be64"}.Contains(name,StringComparer.OrdinalIgnoreCase);
        }
        // -1 means unknown, never "all players closed".
        public static int Count() {
            try {
                int session=Process.GetCurrentProcess().SessionId,count=0;
                foreach(Process p in Process.GetProcesses()) using(p) {
                    try { if(IsMpc(p.ProcessName) && p.SessionId==session && !p.HasExited) count++; }
                    catch(InvalidOperationException) { }
                    catch { return -1; }
                }
                return count;
            } catch { return -1; }
        }
    }
    public static class Policy {
        public static string Action(bool movieOwned,int players,int emptySamples) {
            if(players<0) return "Hold";
            if(players>0 && !movieOwned) return "Movie";
            if(players==0 && movieOwned && emptySamples>=2) return "Desktop";
            return "Hold";
        }
        public static bool Matches(Mode m,NvidiaColor c,bool movie,Settings s) {
            return m.Width==3840 && m.Height==2160 && m.Frequency==(movie?s.MovieHz:60u) &&
                c.Bpc==(movie?3u:2u) && c.Format==(!movie && s.Desktop420?3:0) &&
                c.DynamicRange==(!movie && s.Desktop420?1:0);
        }
    }
    public static class Profiles {
        public static Snapshot Capture(Settings s,bool wait) {
            Native.RequireIdentity(s.DisplayName,s.Identity);
            Mode m=Native.Current(s.DisplayName);
            if(m.DriverExtra!=0) throw new InvalidOperationException("This display uses unsupported private timing data.");
            NvidiaColor c=Native.GetColor(s.DisplayName);
            if(c.Policy>1) throw new InvalidOperationException("Cannot save an unknown NVIDIA colour policy.");
            return new Snapshot {Name=s.DisplayName,Identity=s.Identity,Mode=Native.PackMode(m),Color=Native.PackColor(c),WaitForPlayer=wait};
        }
        public static void Apply(Settings s,bool movie) {
            Native.RequireIdentity(s.DisplayName,s.Identity);
            Mode target=Native.FindResolution(s.DisplayName,movie?s.MovieHz:60u,3840,2160);
            NvidiaColor c=Native.GetColor(s.DisplayName);
            c.Format=(byte)(!movie && s.Desktop420?3:0);
            c.Colorimetry=255; // Driver decides signal colourimetry; do not force SDR primaries.
            c.DynamicRange=(byte)(!movie && s.Desktop420?1:0);
            c.Bpc=movie?3u:2u; c.Policy=0;
            if(!movie) Native.SetColor(s.DisplayName,c); // Release bandwidth before raising the rate.
            Native.Apply(s.DisplayName,target);
            Native.SetColor(s.DisplayName,c);
            Thread.Sleep(1200);
            Verify(s,movie);
        }
        public static void Verify(Settings s,bool movie) {
            if(!Policy.Matches(Native.Current(s.DisplayName),Native.GetColor(s.DisplayName),movie,s))
                throw new InvalidOperationException("The driver did not keep the requested resolution, refresh rate, colour format, range and bit depth.");
        }
        public static void Restore(Snapshot snap) {
            Native.Restore(snap.Name,snap.Identity,snap.Mode,snap.Color);
        }
        public static string Key(Settings s,bool movie) {
            return s.Identity+"|4K|"+(movie?s.MovieHz+"-RGB-Full-10":s.Desktop420?"60-YUV420-Limited-8":"60-RGB-Full-8");
        }
    }
}
