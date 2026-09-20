using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace MpcMovieDisplay {
    // Win32 DEVMODEW, display variant; dmBitsPerPel is NOT output bits/channel.
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    public struct Mode {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string DeviceName;
        public ushort SpecVersion, DriverVersion, Size, DriverExtra;
        public uint Fields;
        public int X, Y;
        public uint Orientation, FixedOutput;
        public short Color, Duplex, YResolution, TTOption, Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string FormName;
        public ushort LogPixels;
        public uint BitsPerPel, Width, Height, Flags, Frequency;
        public uint ICMMethod, ICMIntent, MediaType, DitherType, Reserved1, Reserved2;
        public uint PanningWidth, PanningHeight;
    }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    public struct Device {
        public uint Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string Description;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string Key;
    }
    // NV_COLOR_DATA_V4: nested data starts at byte 8, not byte 7.
    [StructLayout(LayoutKind.Explicit, Size=20)]
    public struct NvidiaColor {
        [FieldOffset(0)] public uint Version;
        [FieldOffset(4)] public ushort Size;
        [FieldOffset(6)] public byte Command;
        [FieldOffset(8)] public byte Format;
        [FieldOffset(9)] public byte Colorimetry;
        [FieldOffset(10)] public byte DynamicRange;
        [FieldOffset(12)] public uint Bpc;
        [FieldOffset(16)] public uint Policy;
    }
    public class Display {
        public string Name, Description, Identity;
        public Mode Current;
    }
    public static class Native {
        [DllImport("user32.dll", CharSet=CharSet.Unicode)]
        static extern bool EnumDisplayDevices(string name, uint index, ref Device device, uint flags);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)]
        static extern bool EnumDisplaySettings(string name, int index, ref Mode mode);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)]
        static extern int ChangeDisplaySettingsEx(string name, ref Mode mode, IntPtr window, uint flags, IntPtr param);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        static extern IntPtr LoadLibraryEx(string path, IntPtr reserved, uint flags);
        [DllImport("kernel32.dll", CharSet=CharSet.Ansi, ExactSpelling=true)]
        static extern IntPtr GetProcAddress(IntPtr library, string name);
        [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr library);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Query(uint id);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Simple();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet=CharSet.Ansi)]
        delegate int DisplayId([MarshalAs(UnmanagedType.LPStr)] string name, out uint id);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int ColorControl(uint id, ref NvidiaColor color);
        static IntPtr library;
        static Query query;
        static Simple unload;
        static DisplayId displayId;
        static ColorControl colorControl;
        static T Function<T>(uint id) where T : class {
            IntPtr p=query(id);
            if (p==IntPtr.Zero) throw new InvalidOperationException("NVIDIA API unavailable: "+id.ToString("X"));
            return Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
        }
        static void CheckNv(int status, string operation) {
            if(status!=0) throw new InvalidOperationException(operation+" failed (NVAPI "+status+").");
        }
        public static void Init() {
            if(library!=IntPtr.Zero) return;
            if(IntPtr.Size!=8) throw new InvalidOperationException("Use the 64-bit Windows application.");
            library=LoadLibraryEx(Path.Combine(Environment.SystemDirectory,"nvapi64.dll"),IntPtr.Zero,0x800);
            if(library==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(),"Could not load the installed NVIDIA driver.");
            IntPtr p=GetProcAddress(library,"nvapi_QueryInterface");
            if(p==IntPtr.Zero) throw new InvalidOperationException("nvapi_QueryInterface unavailable.");
            query=(Query)Marshal.GetDelegateForFunctionPointer(p,typeof(Query));
            CheckNv(Function<Simple>(0x0150e828)(),"NVIDIA initialization");
            unload=Function<Simple>(0xd22bdd7e);
            displayId=Function<DisplayId>(0xae457190);
            colorControl=Function<ColorControl>(0x92f9d80d);
        }
        public static void Close() {
            if(library==IntPtr.Zero) return;
            if(unload!=null) unload();
            FreeLibrary(library); library=IntPtr.Zero;
            query=null; unload=null; displayId=null; colorControl=null;
        }
        static Mode EmptyMode() {
            Mode m=new Mode(); m.Size=(ushort)Marshal.SizeOf(typeof(Mode)); return m;
        }
        static Device EmptyDevice() {
            Device d=new Device(); d.Size=(uint)Marshal.SizeOf(typeof(Device)); return d;
        }
        public static Mode Current(string name) {
            Mode m=EmptyMode();
            if(!EnumDisplaySettings(name,-1,ref m)) throw new InvalidOperationException("Cannot read display "+name);
            return m;
        }
        public static Display[] Displays() {
            List<Display> result=new List<Display>();
            for(uint i=0;;i++) {
                Device d=EmptyDevice();
                if(!EnumDisplayDevices(null,i,ref d,0)) break;
                if((d.Flags&1)==0 || (d.Flags&8)!=0) continue;
                string identity=d.Id, description=d.Description;
                int monitors=0;
                for(uint j=0;;j++) {
                    Device monitor=EmptyDevice();
                    if(!EnumDisplayDevices(d.Name,j,ref monitor,1)) break;
                    if((monitor.Flags&1)==0) continue;
                    monitors++; identity+="|"+monitor.Id; description+=" / "+monitor.Description;
                }
                // Clone configurations are ambiguous; do not target multiple physical monitors.
                if(monitors!=1) continue;
                result.Add(new Display {Name=d.Name,Description=description,Identity=identity,Current=Current(d.Name)});
            }
            return result.ToArray();
        }
        public static void RequireIdentity(string name,string identity) {
            foreach(Display d in Displays()) if(d.Name==name && d.Identity==identity) return;
            throw new InvalidOperationException("The saved monitor is disconnected or the display layout changed. Reconnect it before restoring.");
        }
        public static Mode Find30(string name) { return FindRate(name,30); }
        public static Mode FindRate(string name,uint frequency) { return FindResolution(name,frequency,0,0); }
        public static Mode FindResolution(string name,uint frequency,uint width,uint height) {
            Mode current=Current(name);
            for(int i=0;;i++) {
                Mode m=EmptyMode();
                if(!EnumDisplaySettings(name,i,ref m)) break;
                if(m.Width==(width==0 ? current.Width : width) && m.Height==(height==0 ? current.Height : height) && m.BitsPerPel==current.BitsPerPel &&
                   m.Orientation==current.Orientation && m.Frequency==frequency && (m.Flags&2)==0) {
                    m.X=current.X; m.Y=current.Y;
                    m.Fields|=0x20; // Preserve desktop position.
                    Test(name,m); return m;
                }
            }
            throw new InvalidOperationException("No supported progressive "+frequency+" Hz mode at the requested resolution on "+name+". No settings changed.");
        }
        public static void Test(string name,Mode mode) {
            int r=ChangeDisplaySettingsEx(name,ref mode,IntPtr.Zero,2,IntPtr.Zero);
            if(r!=0) throw new InvalidOperationException("Windows rejected this display mode (code "+r+").");
        }
        public static void Apply(string name,Mode mode) {
            Test(name,mode);
            // Temporary: do not write a new default refresh rate to the registry.
            int r=ChangeDisplaySettingsEx(name,ref mode,IntPtr.Zero,0,IntPtr.Zero);
            if(r!=0) throw new InvalidOperationException("Display mode change failed (code "+r+").");
            Thread.Sleep(1500);
            Mode actual=Current(name);
            if(actual.Width!=mode.Width || actual.Height!=mode.Height || actual.Frequency!=mode.Frequency ||
               actual.Orientation!=mode.Orientation || actual.Flags!=mode.Flags)
                throw new InvalidOperationException("The driver did not retain the requested display mode.");
        }
        static uint Id(string name) {
            Init(); uint id; CheckNv(displayId(name,out id),"Finding the NVIDIA display"); return id;
        }
        public static NvidiaColor GetColor(string name) {
            NvidiaColor c=new NvidiaColor(); c.Version=0x40014; c.Size=20; c.Command=1;
            CheckNv(colorControlForGet(name,ref c),"Reading NVIDIA output colour"); return c;
        }
        static int colorControlForGet(string name,ref NvidiaColor c) {
            uint id=Id(name); return colorControl(id,ref c);
        }
        public static void SetColor(string name,NvidiaColor c) {
            uint id=Id(name); c.Command=2;
            CheckNv(colorControl(id,ref c),"Setting NVIDIA output colour");
        }
        public static void Set10Bit(string name) {
            NvidiaColor c=GetColor(name);
            c.Bpc=3; // NV_BPC_10 is enum value 3, NOT 10.
            c.Policy=0; // User-selected output settings.
            SetColor(name,c); Thread.Sleep(1500);
            if(GetColor(name).Bpc!=3) throw new InvalidOperationException("The driver did not enable 10-bit output. The original settings will be restored.");
        }
        public static string PackMode(Mode value) { return Pack(value); }
        public static string PackColor(NvidiaColor value) { return Pack(value); }
        static string Pack<T>(T value) where T:struct {
            int n=Marshal.SizeOf(typeof(T)); IntPtr p=Marshal.AllocHGlobal(n);
            try { Marshal.StructureToPtr(value,p,false); byte[] b=new byte[n]; Marshal.Copy(p,b,0,n); return Convert.ToBase64String(b); }
            finally { Marshal.FreeHGlobal(p); }
        }
        public static T Unpack<T>(string encoded) where T:struct {
            byte[] b=Convert.FromBase64String(encoded); int n=Marshal.SizeOf(typeof(T));
            if(b.Length!=n) throw new InvalidOperationException("Invalid recovery data.");
            IntPtr p=Marshal.AllocHGlobal(n);
            try { Marshal.Copy(b,0,p,n); return (T)Marshal.PtrToStructure(p,typeof(T)); }
            finally { Marshal.FreeHGlobal(p); }
        }
        public static void Restore(string name,string identity,string modeData,string colorData) {
            RequireIdentity(name,identity);
            Mode mode=Unpack<Mode>(modeData);
            NvidiaColor original=Unpack<NvidiaColor>(colorData);
            if(mode.Size!=220 || mode.DriverExtra!=0 || original.Version!=0x40014 || original.Size!=20)
                throw new InvalidOperationException("Unsupported recovery data.");
            // Lower output bandwidth first when restoring e.g. 60 Hz / 8 bpc.
            // Some saved colour combinations only work AFTER the old mode is restored.
            try { NvidiaColor temporary=original; temporary.Policy=0; SetColor(name,temporary); } catch { }
            Apply(name,mode);
            SetColor(name,original); Thread.Sleep(1200);
            NvidiaColor actual=GetColor(name);
            if(Current(name).Frequency!=mode.Frequency) throw new InvalidOperationException("Colour change altered the restored refresh rate.");
            if(actual.Policy!=original.Policy || (original.Policy==0 &&
               (actual.Bpc!=original.Bpc || actual.Format!=original.Format ||
                actual.DynamicRange!=original.DynamicRange || actual.Colorimetry!=original.Colorimetry)))
                throw new InvalidOperationException("The NVIDIA driver did not restore all saved colour settings. Recovery data has been kept.");
        }
    }
}
