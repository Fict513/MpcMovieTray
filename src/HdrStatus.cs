using System;
using System.Runtime.InteropServices;
namespace MpcMovieDisplay {
    public static class HdrStatus {
        [DllImport("user32.dll")] static extern int GetDisplayConfigBufferSizes(uint flags,out uint paths,out uint modes);
        [DllImport("user32.dll")] static extern int QueryDisplayConfig(uint flags,ref uint paths,IntPtr pathData,ref uint modes,IntPtr modeData,IntPtr topology);
        [DllImport("user32.dll")] static extern int DisplayConfigGetDeviceInfo(IntPtr request);
        // SDK: PATH_INFO 72 bytes, MODE_INFO 64; source at 0, target at 20.
        static IntPtr Request(int kind,int size,IntPtr path,int adapterOffset,int idOffset) {
            IntPtr p=Marshal.AllocHGlobal(size);
            Marshal.Copy(new byte[size],0,p,size);
            Marshal.WriteInt32(p,0,kind); Marshal.WriteInt32(p,4,size);
            Marshal.WriteInt64(p,8,Marshal.ReadInt64(path,adapterOffset));
            Marshal.WriteInt32(p,16,Marshal.ReadInt32(path,idOffset));
            return p;
        }
        public static string DecodeModern(int mode) {
            return mode==2 ? "On" : mode==0 || mode==1 ? "Off" : "Unknown";
        }
        public static string DecodeLegacy(int flags) {
            // Legacy API reports advanced colour, which can also mean WCG/ACM.
            if((flags&2)==0) return "Off";
            return "Unknown (advanced colour on)";
        }
        public static string Read(string deviceName) {
            try {
                for(int attempt=0;attempt<3;attempt++) {
                    uint pc,mc;
                    if(GetDisplayConfigBufferSizes(2,out pc,out mc)!=0 || pc==0) return "Unknown";
                    IntPtr paths=Marshal.AllocHGlobal(checked((int)pc*72));
                    IntPtr modes=Marshal.AllocHGlobal(checked((int)mc*64));
                    try {
                        int r=QueryDisplayConfig(2,ref pc,paths,ref mc,modes,IntPtr.Zero);
                        if(r==122) continue;
                        if(r!=0) return "Unknown";
                        for(int i=0;i<pc;i++) {
                            IntPtr path=IntPtr.Add(paths,i*72);
                            IntPtr source=Request(1,84,path,0,8);
                            bool matches=false;
                            try {
                                matches=DisplayConfigGetDeviceInfo(source)==0 &&
                                    string.Equals(Marshal.PtrToStringUni(IntPtr.Add(source,20)),deviceName,StringComparison.OrdinalIgnoreCase);
                            } finally { Marshal.FreeHGlobal(source); }
                            if(!matches) continue;
                            IntPtr modern=Request(15,36,path,20,28);
                            try {
                                if(DisplayConfigGetDeviceInfo(modern)==0) return DecodeModern(Marshal.ReadInt32(modern,32));
                            } finally { Marshal.FreeHGlobal(modern); }
                            IntPtr legacy=Request(9,32,path,20,28);
                            try {
                                if(DisplayConfigGetDeviceInfo(legacy)==0) return DecodeLegacy(Marshal.ReadInt32(legacy,20));
                            } finally { Marshal.FreeHGlobal(legacy); }
                        }
                        return "Unknown";
                    } finally { Marshal.FreeHGlobal(paths); Marshal.FreeHGlobal(modes); }
                }
            } catch { }
            return "Unknown";
        }
    }
}
