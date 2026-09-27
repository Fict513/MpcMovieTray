using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MpcMovieDisplay {
    enum TrayIconState { Movie, Desktop, TvConnected, LgConnected, Unknown }

    sealed class TrayIconSet : IDisposable {
        readonly Icon[] icons=new Icon[5];
        readonly IntPtr[] handles=new IntPtr[5];
        int size;
        bool light;

        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);

        public Icon this[TrayIconState state] { get { return icons[(int)state]; } }

        public void EnsureCurrent() {
            int requested=TrayIconRenderer.MapSize(System.Windows.Forms.SystemInformation.SmallIconSize.Width);
            bool requestedLight=TrayIconRenderer.IsLightTheme();
            if(requested==size && requestedLight==light && icons[0]!=null) return;
            DisposeIcons();
            size=requested; light=requestedLight;
            for(int i=0;i<icons.Length;i++) {
                using(Bitmap bitmap=TrayIconRenderer.Render((TrayIconState)i,size,light)) {
                    handles[i]=bitmap.GetHicon();
                    icons[i]=Icon.FromHandle(handles[i]);
                }
            }
        }

        void DisposeIcons() {
            for(int i=0;i<icons.Length;i++) {
                if(icons[i]!=null) { icons[i].Dispose();icons[i]=null; }
                if(handles[i]!=IntPtr.Zero) { DestroyIcon(handles[i]);handles[i]=IntPtr.Zero; }
            }
        }
        public void Dispose() { DisposeIcons(); }
    }

    static class TrayIconRenderer {
        static readonly Color[] DarkPalette = {
            Color.FromArgb(52,190,115), Color.FromArgb(50,146,240), Color.FromArgb(105,190,235),
            Color.FromArgb(216,150,66), Color.FromArgb(142,151,164)
        };
        static readonly Color[] LightPalette = {
            Color.FromArgb(26,142,80), Color.FromArgb(30,102,198), Color.FromArgb(36,136,181),
            Color.FromArgb(178,105,25), Color.FromArgb(89,98,111)
        };

        public static int MapSize(int systemSize) {
            if(systemSize<=16) return 16;
            if(systemSize<=20) return 20;
            if(systemSize<=24) return 24;
            return 32;
        }
        public static bool IsLightTheme() {
            try {
                using(RegistryKey key=Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) {
                    object value=key==null?null:key.GetValue("SystemUsesLightTheme");
                    return value is int && (int)value!=0;
                }
            } catch { return false; }
        }
        public static Bitmap Render(TrayIconState state,int size,bool light) {
            if(size!=16 && size!=20 && size!=24 && size!=32) throw new ArgumentOutOfRangeException("size");
            Bitmap bitmap=new Bitmap(size,size,PixelFormat.Format32bppArgb);
            using(Graphics graphics=Graphics.FromImage(bitmap)) {
                graphics.Clear(Color.Transparent);
                int unit=size/4;
                Color start=(light?LightPalette:DarkPalette)[(int)state];
                Color end=light?Color.FromArgb(255,Math.Min(255,start.R+42),Math.Min(255,start.G+42),Math.Min(255,start.B+42)):
                    Color.FromArgb(255,Math.Max(0,start.R-36),Math.Max(0,start.G-36),Math.Max(0,start.B-36));
                using(LinearGradientBrush gradient=new LinearGradientBrush(new Rectangle(0,0,size,1),start,end,0f)) {
                    // A single full-width gradient gives the monitor body and stand one continuous colour ramp.
                    graphics.FillRectangle(gradient,0,unit,size,unit*2);
                    graphics.FillRectangle(gradient,unit*2,unit*3,unit,unit);
                    graphics.FillRectangle(gradient,unit,unit*4-1,unit*2,1);
                }
                Color screen=light?Color.FromArgb(255,246,248,250):Color.FromArgb(255,18,23,29);
                using(Brush screenBrush=new SolidBrush(screen))
                    graphics.FillRectangle(screenBrush,unit/2,unit+unit/2,size-unit,unit);
                DrawGlyph(graphics,state,size,light,unit);
            }
            return bitmap;
        }
        static void DrawGlyph(Graphics graphics,TrayIconState state,int size,bool light,int unit) {
            Color glyph=light?Color.FromArgb(255,28,37,48):Color.FromArgb(255,244,247,250);
            int x=size/2-unit/2, y=unit+unit/2;
            using(Brush brush=new SolidBrush(glyph)) {
                if(state==TrayIconState.Movie) {
                    graphics.FillRectangle(brush,x,y,unit/2,unit);
                    graphics.FillRectangle(brush,x+unit/2,y+unit/4,unit/2,unit/2);
                } else if(state==TrayIconState.Desktop) {
                    graphics.FillRectangle(brush,x,y,unit,unit/4);
                    graphics.FillRectangle(brush,x,y+unit/4,unit/4,unit*3/4);
                } else if(state==TrayIconState.TvConnected) {
                    graphics.FillRectangle(brush,x,y+unit/4,unit,unit/2);
                    graphics.FillRectangle(brush,x+unit/4,y,unit/2,unit);
                } else if(state==TrayIconState.LgConnected) {
                    graphics.FillRectangle(brush,x,y,unit/4,unit);
                    graphics.FillRectangle(brush,x,y+unit*3/4,unit,unit/4);
                } else {
                    graphics.FillRectangle(brush,x+unit/4,y,unit/2,unit/4);
                    graphics.FillRectangle(brush,x+unit/2,y+unit/4,unit/4,unit/2);
                    graphics.FillRectangle(brush,x+unit/2,y+unit*3/4,unit/4,unit/4);
                }
            }
        }
        public static void Export(string folder) {
            Directory.CreateDirectory(folder);
            TrayIconState[] states=(TrayIconState[])Enum.GetValues(typeof(TrayIconState));
            int[] sizes={16,20,24,32};
            bool[] themes={false,true};
            foreach(bool light in themes)
                foreach(TrayIconState state in states)
                    foreach(int size in sizes)
                        using(Bitmap bitmap=Render(state,size,light))
                            bitmap.Save(Path.Combine(folder,state.ToString().ToLowerInvariant()+"-"+size+"-"+(light?"light":"dark")+".png"),ImageFormat.Png);
            using(Bitmap sheet=new Bitmap(32*5,32*2,PixelFormat.Format32bppArgb))
            using(Graphics graphics=Graphics.FromImage(sheet)) {
                graphics.Clear(Color.Transparent);
                for(int row=0;row<2;row++) {
                    bool light=row==1;
                    graphics.FillRectangle(new SolidBrush(light?Color.White:Color.FromArgb(24,29,36)),0,row*32,sheet.Width,32);
                    for(int column=0;column<states.Length;column++)
                        using(Bitmap icon=Render(states[column],32,light))
                            graphics.DrawImageUnscaled(icon,column*32,row*32);
                }
                sheet.Save(Path.Combine(folder,"icon-preview.png"),ImageFormat.Png);
            }
        }
        public static void SelfTest() {
            if(MapSize(16)!=16 || MapSize(17)!=20 || MapSize(21)!=24 || MapSize(25)!=32)
                throw new Exception("Tray icon size mapping failed.");
            foreach(bool light in new[]{false,true})
                foreach(TrayIconState state in (TrayIconState[])Enum.GetValues(typeof(TrayIconState)))
                    using(Bitmap bitmap=Render(state,32,light)) {
                        if(bitmap.GetPixel(0,0).A!=0) throw new Exception("Tray icon background is not transparent.");
                        if(bitmap.GetPixel(16,8).A!=255) throw new Exception("Tray icon body is missing.");
                        if(bitmap.GetPixel(16,31).A!=255) throw new Exception("Tray icon stand is missing.");
                        if(bitmap.GetPixel(16,16).A!=255) throw new Exception("Tray icon screen is missing.");
                        Color body=bitmap.GetPixel(2,8);
                        Color edge=bitmap.GetPixel(29,8);
                        if(body.ToArgb()==edge.ToArgb()) throw new Exception("Tray icon gradient is missing.");
                        if(light && body.R==DarkPalette[(int)state].R &&
                            body.G==DarkPalette[(int)state].G && body.B==DarkPalette[(int)state].B)
                            throw new Exception("Tray icon light palette is missing.");
                    }
        }
    }
}
