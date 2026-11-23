using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace KeyboardCat
{
    internal enum FieldPrivacy { Unknown, Ordinary, Password, GameSurface, ActivityOnly, WeChatMain }

    // The worker receives probe requests only. Keystrokes never enter its queue.
    internal sealed class PasswordFocus : IDisposable
    {
        readonly object gate=new object();
        readonly Thread worker;
        int requested,completed;
        bool stopped;
        FieldPrivacy result;
        IntPtr window,answeredFocus;
        long answered;
        // Only the worker checks process identity. Cache the owner while the same window stays in front.
        IntPtr checkedWindow;
        uint checkedProcess;
        FieldPrivacy ownerSurface;
        string windowClass;
        static readonly string[] chatCaptions={"微信","WeChat","Weixin","微信电脑版"};
        public PasswordFocus()
        {
            worker=new Thread(Run) {IsBackground=true,Name="KeyboardCat field privacy"};
            worker.SetApartmentState(ApartmentState.MTA);worker.Start();
        }

        [StructLayout(LayoutKind.Sequential)]
        struct GuiThreadInfo {
            public int Size,Flags;
            public IntPtr Active,Focus,Capture,Menu,Move,Caret;
            public int Left,Top,Right,Bottom;
        }
        [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint thread,ref GuiThreadInfo info);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int length);
        [DllImport("user32.dll",EntryPoint="GetWindowLongW")] static extern int GetWindowLong(IntPtr window,int index);

        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
        [DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr window);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string kind,string caption);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window,uint flags);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern uint RealGetWindowClass(IntPtr window,StringBuilder name,uint length);
        static readonly string[] systemEdits={"Edit","RichEdit","RichEdit20A","RichEdit20W","RichEdit50A","RichEdit50W"};
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]
        static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr wParam,IntPtr lParam,uint flags,uint timeout,out UIntPtr result);

        static IntPtr FocusedChild(IntPtr foreground)
        {
            if(foreground==IntPtr.Zero) return IntPtr.Zero;
            GuiThreadInfo info=new GuiThreadInfo {Size=Marshal.SizeOf(typeof(GuiThreadInfo))};
            return GetGUIThreadInfo(0,ref info) && info.Focus!=IntPtr.Zero && GetAncestor(info.Focus,2)==foreground
                ?info.Focus:IntPtr.Zero;
        }

        static bool NativeEdit(IntPtr focus)
        {
            if(focus==IntPtr.Zero) return false;
            StringBuilder name=new StringBuilder(128);
            if(GetClassName(focus,name,name.Capacity)==0) return false;
            string kind=name.ToString();
            return kind.Equals("Edit",StringComparison.OrdinalIgnoreCase) || kind.StartsWith("RichEdit",StringComparison.OrdinalIgnoreCase) ||
                kind.IndexOf(".EDIT.",StringComparison.OrdinalIgnoreCase)>=0;
        }

        static FieldPrivacy NativeEditPrivacy(IntPtr focus)
        {
            if(!NativeEdit(focus)) return FieldPrivacy.ActivityOnly;
            if((GetWindowLong(focus,-16)&0x20)!=0) return FieldPrivacy.Password;
            StringBuilder name=new StringBuilder(128);
            if(RealGetWindowClass(focus,name,(uint)name.Capacity)==0) return FieldPrivacy.ActivityOnly;
            string kind=name.ToString();
            if(!Array.Exists(systemEdits,item=>String.Equals(item,kind,StringComparison.OrdinalIgnoreCase))) return FieldPrivacy.ActivityOnly;
            // EM_GETPASSWORDCHAR queries the mask only. A failed or slow control provides no assurance.
            UIntPtr mask;
            if(SendMessageTimeout(focus,0x00d2,IntPtr.Zero,IntPtr.Zero,0x0002,12,out mask)==IntPtr.Zero)
                return FieldPrivacy.ActivityOnly;
            return mask==UIntPtr.Zero?FieldPrivacy.Ordinary:FieldPrivacy.Password;
        }

        static FieldPrivacy IdentifyOwner(IntPtr window,uint processId,out string kind)
        {
            StringBuilder name=new StringBuilder(96);
            GetClassName(window,name,name.Capacity);
            kind=name.ToString();
            bool game=kind=="SDL_app";
            const string qtSuffix="QWindowIcon";
            bool qt=kind.StartsWith("Qt",StringComparison.Ordinal) && kind.EndsWith(qtSuffix,StringComparison.Ordinal) &&
                kind.Length>2+qtSuffix.Length;
            for(int i=2;qt && i<kind.Length-qtSuffix.Length;i++) qt=kind[i]>='0' && kind[i]<='9';
            if(!game && !qt && kind!="WeChatMainWndForPC") return FieldPrivacy.ActivityOnly;
            try {
                using(Process p=Process.GetProcessById((int)processId)) {
                    string process=p.ProcessName;
                    if(game && process.Equals("dota2",StringComparison.OrdinalIgnoreCase)) return FieldPrivacy.GameSurface;
                    if(!game && (process.Equals("Weixin",StringComparison.OrdinalIgnoreCase) ||
                        process.Equals("WeChat",StringComparison.OrdinalIgnoreCase))) return FieldPrivacy.WeChatMain;
                }
            } catch(ArgumentException) {}
            catch(InvalidOperationException) {}
            catch(System.ComponentModel.Win32Exception) {}
            return FieldPrivacy.ActivityOnly;
        }

        static bool IsChatMainWindow(IntPtr window,string kind)
        {
            const int mainStyle=0x00040000|0x00020000|0x00010000; // resizing, minimize and maximize boxes
            if(!IsWindowEnabled(window) || GetWindow(window,4)!=IntPtr.Zero ||
                (GetWindowLong(window,-16)&mainStyle)!=mainStyle) return false;
            // Match public app captions only. Separate conversations and dialogs can use the same Qt class.
            foreach(string caption in chatCaptions) {
                IntPtr match=IntPtr.Zero;
                while((match=FindWindowEx(IntPtr.Zero,match,kind,caption))!=IntPtr.Zero)
                    if(match==window) return true;
            }
            return false;
        }

        FieldPrivacy FocusedSurface(IntPtr foreground,IntPtr focus)
        {
            if(foreground==IntPtr.Zero || focus!=foreground) return FieldPrivacy.ActivityOnly;
            uint pid;
            if(GetWindowThreadProcessId(foreground,out pid)==0) return FieldPrivacy.ActivityOnly;
            if(foreground!=checkedWindow || pid!=checkedProcess) {
                checkedWindow=foreground;checkedProcess=pid;
                ownerSurface=IdentifyOwner(foreground,pid,out windowClass);
            }
            // WeChat may reuse an HWND when its view changes. Recheck the main-window traits each time.
            if(ownerSurface==FieldPrivacy.WeChatMain && !IsChatMainWindow(foreground,windowClass)) return FieldPrivacy.ActivityOnly;
            return ownerSurface;
        }

        static FieldPrivacy ElementPrivacy(AutomationElement el,IntPtr window,FieldPrivacy surface)
        {
            if(el==null) return FieldPrivacy.ActivityOnly;
            object value=el.GetCachedPropertyValue(AutomationElement.IsPasswordProperty,true);
            if(value is bool && (bool)value) return FieldPrivacy.Password;
            if(surface==FieldPrivacy.GameSurface || surface==FieldPrivacy.WeChatMain) {
                // Only the exact focused root can inherit a recognized surface. Children use their own flags.
                object handle=el.GetCachedPropertyValue(AutomationElement.NativeWindowHandleProperty,true);
                object type=el.GetCachedPropertyValue(AutomationElement.ControlTypeProperty,true);
                if(handle is int && (int)handle==unchecked((int)window.ToInt64()) &&
                    Object.Equals(type,ControlType.Window)) return surface;
            }
            return value is bool?FieldPrivacy.Ordinary:FieldPrivacy.ActivityOnly;
        }

        static FieldPrivacy ReadField(IntPtr window,FieldPrivacy surface)
        {
            try {
                CacheRequest cache=new CacheRequest {TreeScope=TreeScope.Element,AutomationElementMode=AutomationElementMode.None};
                cache.Add(AutomationElement.IsPasswordProperty);
                if(surface==FieldPrivacy.GameSurface || surface==FieldPrivacy.WeChatMain) {
                    cache.Add(AutomationElement.NativeWindowHandleProperty);
                    cache.Add(AutomationElement.ControlTypeProperty);
                }
                using(cache.Activate()) {
                    return ElementPrivacy(AutomationElement.FocusedElement,window,surface);
                }
            } catch(ElementNotAvailableException) {return FieldPrivacy.ActivityOnly;}
            catch(COMException ex) {return ex.ErrorCode==unchecked((int)0x80070005)?FieldPrivacy.Unknown:FieldPrivacy.ActivityOnly;}
            catch(InvalidOperationException) {return FieldPrivacy.ActivityOnly;}
            catch(UnauthorizedAccessException) {return FieldPrivacy.Unknown;}
            catch(TimeoutException) {return FieldPrivacy.ActivityOnly;}
            catch(NotSupportedException) {return FieldPrivacy.ActivityOnly;}
            catch(ArgumentException) {return FieldPrivacy.Unknown;}
            catch(System.ComponentModel.Win32Exception) {return FieldPrivacy.Unknown;}
        }

        void Run()
        {
            while(true) {
                int ticket;
                lock(gate) {
                    while(!stopped && requested==completed) Monitor.Wait(gate);
                    if(stopped) return;
                    ticket=requested;
                }
                IntPtr foreground=Native.GetForegroundWindow();
                IntPtr focus=FocusedChild(foreground);
                FieldPrivacy next=ReadField(foreground,FocusedSurface(foreground,focus));
                if(next!=FieldPrivacy.Password && focus!=IntPtr.Zero && focus==FocusedChild(foreground)) {
                    FieldPrivacy native=NativeEditPrivacy(focus);
                    if(native==FieldPrivacy.Password || (native==FieldPrivacy.Ordinary && next==FieldPrivacy.ActivityOnly))
                        next=native;
                }
                if(foreground==IntPtr.Zero || foreground!=Native.GetForegroundWindow() || focus!=FocusedChild(foreground))
                    next=FieldPrivacy.Unknown;
                lock(gate) {
                    result=next;window=foreground;answeredFocus=focus;completed=ticket;answered=Stopwatch.GetTimestamp();
                    Monitor.PulseAll(gate);
                }
            }
        }

        public FieldPrivacy Probe(bool forKey)
        {
            IntPtr foreground=Native.GetForegroundWindow();
            if(foreground==IntPtr.Zero) return FieldPrivacy.Unknown;
            IntPtr focus=FocusedChild(foreground);
            if(focus!=IntPtr.Zero && (GetWindowLong(focus,-16)&0x20)!=0 && NativeEdit(focus)) return FieldPrivacy.Password;
            lock(gate) {
                if(stopped) return FieldPrivacy.Unknown;
                int ticket=++requested;Monitor.PulseAll(gate);
                if(forKey) {
                    // A slow provider may drive a generic typing pose, never a named key or a word.
                    bool opaque=result==FieldPrivacy.ActivityOnly && window==foreground && answeredFocus==focus &&
                        Stopwatch.GetTimestamp()-answered<Stopwatch.Frequency/2;
                    long deadline=Stopwatch.GetTimestamp()+Stopwatch.Frequency*(opaque?6:25)/1000;
                    while(!stopped && completed<ticket) {
                        long left=deadline-Stopwatch.GetTimestamp();
                        if(left<=0) return focus!=IntPtr.Zero && foreground==Native.GetForegroundWindow() && focus==FocusedChild(foreground)
                            ?FieldPrivacy.ActivityOnly:FieldPrivacy.Unknown;
                        Monitor.Wait(gate,Math.Max(1,(int)(1000*left/Stopwatch.Frequency)));
                    }
                }
                if(foreground!=Native.GetForegroundWindow() || focus!=FocusedChild(foreground)) return FieldPrivacy.Unknown;
                if(window==foreground && answeredFocus==focus && Stopwatch.GetTimestamp()-answered<Stopwatch.Frequency/2) return result;
                return focus==IntPtr.Zero?FieldPrivacy.Unknown:FieldPrivacy.ActivityOnly;
            }
        }
        public void Dispose() {lock(gate){stopped=true;Monitor.PulseAll(gate);}}
    }
}
