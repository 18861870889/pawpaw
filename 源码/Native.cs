using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace KeyboardCat
{
    internal static class Native
    {
        public const int WmInput = 0x00ff, WmMouseActivate = 0x0021, WmDisplayChange = 0x007e, WmDpiChanged = 0x02e0;
        public const int ClickThrough=0x20;
        const int ExtendedStyle=-20;
        [DllImport("user32.dll",EntryPoint="GetWindowLongW")]
        static extern int GetWindowStyle(IntPtr window,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLongW")]
        static extern int SetWindowStyle(IntPtr window,int index,int style);
        public static bool SetClickThrough(IntPtr window,bool enabled)
        {
            int prior=GetWindowStyle(window,ExtendedStyle);
            int next=enabled?(prior|ClickThrough):(prior&~ClickThrough);
            if(prior!=next) SetWindowStyle(window,ExtendedStyle,next);
            return ((GetWindowStyle(window,ExtendedStyle)&ClickThrough)!=0)==enabled;
        }
        public const int Layered = 0x80000, ToolWindow = 0x80, NoActivate = 0x08000000;

        [StructLayout(LayoutKind.Sequential)]
        internal struct XY
        {
            public int X,Y;
            public XY(int x,int y) { X=x; Y=y; }
        }
        [StructLayout(LayoutKind.Sequential)]
        struct RawDevice { public ushort Page,Usage; public uint Flags; public IntPtr Target; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct RawHeader { public uint Type, Size; public IntPtr Device, WParam; }

        // The keyboard and mouse unions start after the pointer-sized RAWINPUTHEADER.
        [StructLayout(LayoutKind.Explicit,Size=24)]
        internal struct RawPayload
        {
            [FieldOffset(0)] public ushort MakeCode;
            [FieldOffset(2)] public ushort KeyFlags;
            [FieldOffset(4)] public ushort MouseButtons;
            [FieldOffset(6)] public ushort VKey;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct RawPacket { public RawHeader Header; public RawPayload Data; }
        static readonly uint rawHeaderSize=(uint)Marshal.SizeOf(typeof(RawHeader));
        static readonly uint rawPacketSize=(uint)Marshal.SizeOf(typeof(RawPacket));

        [DllImport("user32.dll",SetLastError=true)]
        static extern bool RegisterRawInputDevices([In] RawDevice[] devices,uint count,uint size);
        [DllImport("user32.dll",SetLastError=true)]
        static extern uint GetRawInputData(IntPtr input,uint command,out RawPacket packet,ref uint size,uint headerSize);

        public static bool Subscribe(IntPtr window,bool enable)
        {
            uint flags=enable?0x100u:1u; // INPUTSINK or REMOVE. Legacy input remains available to foreground apps.
            IntPtr target=enable?window:IntPtr.Zero;
            RawDevice[] devices={
                new RawDevice {Page=1,Usage=6,Flags=flags,Target=target},
                new RawDevice {Page=1,Usage=2,Flags=flags,Target=target}
            };
            return RegisterRawInputDevices(devices,2,(uint)Marshal.SizeOf(typeof(RawDevice)));
        }
        public static bool ReadInput(IntPtr handle,out RawPacket packet)
        {
            uint size=rawPacketSize;
            uint read=GetRawInputData(handle,0x10000003,out packet,ref size,rawHeaderSize);
            return read!=uint.MaxValue && read>=rawHeaderSize+16 && (packet.Header.Type==0 || packet.Header.Type==1);
        }
        public static int KeyCode(RawPayload data)
        {
            bool extended=(data.KeyFlags&2)!=0;
            if(data.VKey==0x10) return data.MakeCode==0x36?0xa1:0xa0;
            if(data.VKey==0x11) return extended?0xa3:0xa2;
            if(data.VKey==0x12) return extended?0xa5:0xa4;
            return data.VKey;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct LastInput { public uint Size,Tick; }
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LastInput info);
        internal static double QuietSeconds()
        {
            LastInput input=new LastInput {Size=(uint)Marshal.SizeOf(typeof(LastInput))};
            return GetLastInputInfo(ref input)?unchecked((uint)Environment.TickCount-input.Tick)/1000.0:double.PositiveInfinity;
        }

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int key);
        internal static bool AnyKeyboardKeyHeld()
        {
            for(int vk=8;vk<255;vk++) if(GetAsyncKeyState(vk)<0) return true;
            return false;
        }
        internal static bool ShortcutHeld()
        {
            // Include chords already held when input was resumed.
            return GetAsyncKeyState(0x11)<0 || GetAsyncKeyState(0x12)<0 ||
                GetAsyncKeyState(0x5b)<0 || GetAsyncKeyState(0x5c)<0;
        }

        [DllImport("user32.dll",CharSet=CharSet.Unicode)]
        internal static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll")]
        internal static extern bool PostMessage(IntPtr hwnd,uint msg,IntPtr w,IntPtr l);
        [DllImport("user32.dll")]
        internal static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromPoint(XY p,uint flags);
        [DllImport("shcore.dll")]
        static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint dx,out uint dy);
        public static float DpiAt(Point point)
        {
            try
            {
                uint x,y;
                IntPtr m=MonitorFromPoint(new XY(point.X,point.Y),2);
                if(m!=IntPtr.Zero && GetDpiForMonitor(m,0,out x,out y)==0 && x>=72 && x<=768) return x/96f;
            }
            catch(DllNotFoundException) { }
            catch(EntryPointNotFoundException) { }
            return 1;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DibInfo
        {
            public uint Size; public int Width,Height; public ushort Planes,Bits;
            public uint Compression,ImageSize; public int XMeters,YMeters; public uint Colors,Important;
        }
        [StructLayout(LayoutKind.Sequential,Pack=1)]
        internal struct Blend { public byte Operation,Flags,Alpha,Format; }

        [DllImport("gdi32.dll",SetLastError=true)]
        internal static extern IntPtr CreateCompatibleDC(IntPtr screen);
        [DllImport("gdi32.dll",SetLastError=true)]
        internal static extern IntPtr CreateDIBSection(IntPtr dc,ref DibInfo info,uint usage,out IntPtr pixels,IntPtr section,uint offset);
        [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc,IntPtr bitmap);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll",SetLastError=true)]
        internal static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr destDc,ref XY dest,ref XY size,
            IntPtr sourceDc,ref XY source,uint colorKey,ref Blend blend,uint flags);
    }

    internal sealed class LayerSurface : IDisposable
    {
        public readonly Size Size;
        public Graphics Canvas { get; private set; }
        Bitmap backing;
        IntPtr dc,bitmap,previous,pixels;

        public LayerSurface(Size size)
        {
            Size=size;
            try
            {
                dc=Native.CreateCompatibleDC(IntPtr.Zero);
                if(dc==IntPtr.Zero) throw new Win32Exception();
                Native.DibInfo info=new Native.DibInfo {
                    Size=(uint)Marshal.SizeOf(typeof(Native.DibInfo)),Width=size.Width,Height=-size.Height,Planes=1,Bits=32
                };
                bitmap=Native.CreateDIBSection(dc,ref info,0,out pixels,IntPtr.Zero,0);
                if(bitmap==IntPtr.Zero || pixels==IntPtr.Zero) throw new Win32Exception();
                previous=Native.SelectObject(dc,bitmap);
                if(previous==IntPtr.Zero || previous==new IntPtr(-1)) throw new Win32Exception();
                backing=new Bitmap(size.Width,size.Height,size.Width*4,PixelFormat.Format32bppPArgb,pixels);
                Canvas=Graphics.FromImage(backing);
            }
            catch { Dispose(); throw; }
        }

        public Bitmap Image {get { return backing; }}

        public void Finish(bool print,float scale)
        {
            Canvas.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);
            if(print) InkLook.Apply(pixels,Size.Width*4,Size.Width,Size.Height,scale);
        }

        public void CopyFrom(Bitmap image)
        {
            var state=Canvas.Save();
            Canvas.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            Canvas.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            Canvas.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
            Canvas.DrawImage(image,new Rectangle(Point.Empty,Size),0,0,image.Width,image.Height,GraphicsUnit.Pixel);
            Canvas.Restore(state);
            Canvas.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);
        }

        public void Present(IntPtr window,Point location,int transparency)
        {
            Native.XY dst=new Native.XY(location.X,location.Y), size=new Native.XY(Size.Width,Size.Height), src=new Native.XY(0,0);
            byte opacity=(byte)((255*(100-transparency)+50)/100);
            Native.Blend blend=new Native.Blend { Alpha=opacity,Format=1 };
            if(!Native.UpdateLayeredWindow(window,IntPtr.Zero,ref dst,ref size,dc,ref src,0,ref blend,2))
                throw new Win32Exception();
        }

        public void Dispose()
        {
            if(Canvas!=null) { Canvas.Dispose(); Canvas=null; }
            if(backing!=null) { backing.Dispose(); backing=null; }
            if(previous!=IntPtr.Zero && previous!=new IntPtr(-1)) { Native.SelectObject(dc,previous); previous=IntPtr.Zero; }
            if(bitmap!=IntPtr.Zero) { Native.DeleteObject(bitmap); bitmap=IntPtr.Zero; }
            if(dc!=IntPtr.Zero) { Native.DeleteDC(dc); dc=IntPtr.Zero; }
        }
    }
}
