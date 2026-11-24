using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace KeyboardCat
{
    internal sealed class PetForm : Form
    {
        readonly string settingsFile;
        readonly Preferences settings;
        readonly Motion motion = new Motion();
        readonly Painter art = new Painter();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Timer timer = new Timer();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly ContextMenuStrip actions = new ContextMenuStrip();
        readonly List<Bitmap> portraits = new List<Bitmap>();
        readonly ToolStripMenuItem cats = new ToolStripMenuItem("换只猫咪");
        readonly ToolStripMenuItem sizes = new ToolStripMenuItem("桌宠大小");
        readonly ToolStripMenuItem dressUp = new ToolStripMenuItem("装扮工作室…");
        readonly ToolStripMenuItem looks=new ToolStripMenuItem("桌宠画风");
        readonly ToolStripMenuItem trickMenu=new ToolStripMenuItem("单词彩蛋");
        readonly ToolStripMenuItem wordTriggers=new ToolStripMenuItem("打字触发彩蛋");
        UnlockBadge unlockBadge;
        readonly ContextMenuStrip lockedActions=new ContextMenuStrip();
        WardrobeForm wardrobe;
        OpacityForm opacityPanel;
        double saveOpacityAt=double.PositiveInfinity;
        double retryInputAt=double.PositiveInfinity, retryInputDelay=2;
        bool showingHelp;
        readonly ToolStripMenuItem labels = new ToolStripMenuItem("显示按键气泡");
        readonly ToolStripMenuItem pause = new ToolStripMenuItem();
        LayerSurface surface,studioSurface,rendered;
        string dragScreen;
        float dragScale;
        Icon trayIcon;
        float scale = 1;
        double lastFrame,lastInput=-100;
        Point priorCursor, dragStart;
        PointF grab;
        bool loaded, listening, pressing, dragging, placing, disposed;
        PasswordFocus focusProbe;
        IntPtr inputChild;
        double nextFocusCheck;
        bool waitForKeyRelease;
        FieldPrivacy inputMode;
        IntPtr inputWindow,protectedWindow;
        double Now { get { return clock.Elapsed.TotalSeconds; } }

        public PetForm(string settingsFile)
        {
            this.settingsFile=settingsFile; settings=Preferences.Load(settingsFile);
            motion.Tricks.Enabled=settings.WordTriggers;
            Text=Program.DisplayName; FormBorderStyle=FormBorderStyle.None;
            AutoScaleMode=AutoScaleMode.None; StartPosition=FormStartPosition.Manual;
            ShowInTaskbar=false; TopMost=true; TabStop=false;
            SetStyle(ControlStyles.Selectable,false);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.StandardDoubleClick,true);
            BuildMenu();
            lockedActions.Items.Add(new ToolStripMenuItem("请点猫咪右上角的小锁解锁") {Enabled=false});
            lockedActions.Items.Add("退出桌宠",null,delegate {Close();});
            tray.ContextMenuStrip=settings.Locked?lockedActions:actions;
            tray.DoubleClick+=delegate { if(!settings.Locked) ToCorner(); };
            timer.Interval=35;
            timer.Tick+=delegate { Frame(); };
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams p=base.CreateParams;
                p.ExStyle |= Native.Layered | Native.ToolWindow | Native.NoActivate;
                if(settings!=null && settings.Locked) p.ExStyle|=Native.ClickThrough;
                return p;
            }
        }

        void PositionBadge()
        {
            if(unlockBadge!=null && !unlockBadge.IsDisposed) unlockBadge.Place(Bounds);
        }
        void SetLocked(bool locked)
        {
            if(disposed || !IsHandleCreated) return;
            if(locked) {
                if(wardrobe!=null) wardrobe.Close();
                if(opacityPanel!=null) opacityPanel.Close();
                EndDrag(); Capture=false;
                if(unlockBadge==null) unlockBadge=new UnlockBadge(delegate {SetLocked(false);});
                PositionBadge();
                if(!unlockBadge.Visible) unlockBadge.Show(this);
            }
            if(!Native.SetClickThrough(Handle,locked)) {
                if(!settings.Locked && unlockBadge!=null) unlockBadge.Hide();
                return;
            }
            settings.Locked=locked;
            if(!locked && unlockBadge!=null) unlockBadge.Hide();
            tray.ContextMenuStrip=locked?lockedActions:actions;
            Save();
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if(settings.Locked) SetLocked(true);
        }

        void RebuildCatMenu()
        {
            while(cats.DropDownItems.Count>0) {
                ToolStripItem old=cats.DropDownItems[0];
                old.Image=null;
                cats.DropDownItems.RemoveAt(0);
                old.Dispose();
            }
            foreach(Bitmap image in portraits) image.Dispose();
            portraits.Clear();
            foreach(Cat item in settings.Roles)
            {
                Cat cat=item;
                Bitmap portrait=Painter.Portrait(cat,44,settings.OutfitFor(cat).Hat); portraits.Add(portrait);
                ToolStripMenuItem choice=new ToolStripMenuItem(cat.Name.Replace("&","&&"),portrait);
                choice.Click+=delegate { SelectCat(cat); };
                cats.DropDownItems.Add(choice);
            }
        }

        void BuildMenu()
        {
            actions.ImageScalingSize=new Size(22,22);
            RebuildCatMenu();
            ToolStripDropDownMenu catMenu=(ToolStripDropDownMenu)cats.DropDown;
            catMenu.ShowCheckMargin=true; catMenu.ImageScalingSize=new Size(22,22);
            for(int i=0;i<Preferences.Scales.Length;i++)
            {
                int index=i;
                sizes.DropDownItems.Add(Preferences.SizeNames[i],null,delegate { ResizePet(index); });
            }
            labels.Click+=delegate { settings.Labels=!settings.Labels; motion.ClearBubbles(); Save(); Redraw(); };
            pause.Click+=delegate
            {
                settings.Paused=!settings.Paused;
                SetListening(!settings.Paused);
                Save(); Redraw();
            };
            foreach (WordTrick entry in WordTrick.All)
            {
                WordTrick trick=entry;
                ToolStripMenuItem item=new ToolStripMenuItem(trick.Label);
                item.ShortcutKeyDisplayString=ChineseWord.For(trick).Simplified+" / "+trick.Word+" / "+trick.Pinyin;
                item.ToolTipText="连续敲完拼音或英文即触发，也可以点击试演";
                item.Click+=delegate { BeginInvoke((MethodInvoker)delegate { PlayTrick(trick); }); };
                trickMenu.DropDownItems.Add(item);
            }
            trickMenu.DropDownItems.Add(new ToolStripSeparator());
            wordTriggers.Checked=settings.WordTriggers;
            wordTriggers.Click+=delegate
            {
                settings.WordTriggers=!settings.WordTriggers;
                motion.Tricks.Enabled=settings.WordTriggers;
                wordTriggers.Checked=settings.WordTriggers;
                Save();
            };
            trickMenu.DropDownItems.Add(wordTriggers);
            dressUp.Click+=delegate { BeginInvoke((MethodInvoker)OpenWardrobe); };
            looks.DropDownItems.Add("猫咪画报",null,delegate {SetLook(true);});
            looks.DropDownItems.Add("经典原画",null,delegate {SetLook(false);});
            actions.Items.Add(cats); actions.Items.Add(dressUp); actions.Items.Add(looks); actions.Items.Add(trickMenu); actions.Items.Add(sizes);
            actions.Items.Add("锁定桌宠（点击穿透）",null,delegate { BeginInvoke((MethodInvoker)delegate {SetLocked(true);}); });
            actions.Items.Add("桌宠透明度…",null,delegate { BeginInvoke((MethodInvoker)OpenOpacity); });
            actions.Items.Add(new ToolStripSeparator()); actions.Items.Add(labels); actions.Items.Add(pause);
            actions.Items.Add(new ToolStripSeparator());
            actions.Items.Add("回到屏幕右下角",null,delegate { ToCorner(); });
            ToolStripMenuItem help=new ToolStripMenuItem("帮助与反馈");
            help.DropDownItems.Add("使用说明…",null,delegate { BeginInvoke((MethodInvoker)ShowHelp); });
            help.DropDownItems.Add("反馈问题…",null,delegate {
                try { Process.Start(new ProcessStartInfo(Program.FeedbackUrl) {UseShellExecute=true}); }
                catch(Win32Exception) { MessageBox.Show("请在浏览器中打开 "+Program.FeedbackUrl,Program.DisplayName); }
            });
            actions.Items.Add(help);
            actions.Items.Add("退出桌宠",null,delegate { Close(); });
            actions.Opening+=delegate
            {
                for(int i=0;i<settings.Roles.Count;i++) ((ToolStripMenuItem)cats.DropDownItems[i]).Checked=settings.Roles[i]==settings.Cat;
                for(int i=0;i<sizes.DropDownItems.Count;i++) ((ToolStripMenuItem)sizes.DropDownItems[i]).Checked=i==settings.ScaleIndex;
                ((ToolStripMenuItem)looks.DropDownItems[0]).Checked=settings.PrintLook;
                ((ToolStripMenuItem)looks.DropDownItems[1]).Checked=!settings.PrintLook;
                labels.Checked=settings.Labels;
                pause.Text=settings.Paused?"恢复键鼠互动":"暂停键鼠互动";
                motion.ClearInput();
            };
            actions.Closed+=delegate { motion.ClearInput(); priorCursor=Cursor.Position; };
        }

        void ShowHelp()
        {
            if(showingHelp || disposed) return;
            showingHelp=true; motion.ClearInput();
            string state=settings.Paused?"已暂停":listening?"已连接":"连接中，会自动重试";
            try {
                MessageBox.Show(this,"键鼠互动　"+state+"\n\n"+
                    "拖动猫咪可移动位置，右键打开菜单，进入装扮工作室可以换猫、换衣服和桌搭。\n\n"+
                    "连续敲 xiaoyu 或 fish 看猫咪抱鱼，双击猫咪看它捧心。右键可锁定桌宠，点击会穿过猫咪。点右上角的小锁恢复控制。",
                    Program.DisplayName+" "+Program.Version,MessageBoxButtons.OK,MessageBoxIcon.Information);
            }
            finally { showingHelp=false; motion.ClearInput(); }
        }

        void PlayTrick(WordTrick trick)
        {
            if (disposed || !loaded || motion.Private) return;
            motion.Tricks.Play(trick,Now);
            timer.Interval=25;
            Redraw();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (!settings.Locked && e.Button==MouseButtons.Left && !dragging && !actions.Visible)
                PlayTrick(WordTrick.Love);
        }

        void OpenWardrobe()
        {
            if(disposed || !loaded || settings.Locked) return;
            if(wardrobe==null || wardrobe.IsDisposed)
            {
                wardrobe=new WardrobeForm(settings,motion,delegate { return rendered==null?null:rendered.Image; },SelectCat,Wear,SetDesk,ChangeRole,SetLook,PlayTrick);
                wardrobe.Activated+=delegate { motion.ClearInput(); };
                wardrobe.FormClosed+=delegate { wardrobe=null; ReleaseStudioSurface(); };
            }
            ShowSettingsPanel(wardrobe);
            Redraw();
        }

        void OpenOpacity()
        {
            if(disposed || !loaded || settings.Locked) return;
            if(opacityPanel==null || opacityPanel.IsDisposed)
            {
                opacityPanel=new OpacityForm(settings.Transparency,delegate(int percent)
                {
                    settings.Transparency=percent;
                    saveOpacityAt=Now+.35;
                    if(surface!=null) surface.Present(Handle,Location,percent);
                });
                opacityPanel.Activated+=delegate { motion.ClearInput(); };
                opacityPanel.FormClosed+=delegate { opacityPanel=null; };
            }
            ShowSettingsPanel(opacityPanel);
        }

        void ShowSettingsPanel(Form panel)
        {
            if(!panel.Visible)
            {
                PlacePanel(panel);
                panel.Show(this);
                PlacePanel(panel);
            }
            panel.BringToFront();
            panel.Activate();
        }

        void PlacePanel(Form panel)
        {
            Rectangle pet=Bounds,area=Screen.FromRectangle(pet).WorkingArea;
            int gap=Math.Max(8,panel.Width/50);
            int x=pet.Left-gap-panel.Width;
            if(x<area.Left && area.Right-pet.Right>pet.Left-area.Left) x=pet.Right+gap;
            panel.Location=KeepInside(new Point(x,pet.Bottom-panel.Height),panel.Size,area);
        }

        void SetLook(bool print)
        {
            if(settings.PrintLook==print) return;
            settings.PrintLook=print;
            AppearanceChanged();
        }

        void SetDesk(bool keyboard,int index)
        {
            if(keyboard) settings.Board=index; else settings.MouseStyle=index;
            AppearanceChanged();
        }

        void ChangeRole(Cat cat,bool remove)
        {
            if(remove) settings.RemoveRole(cat); else settings.PutRole(cat);
            RebuildCatMenu();
            SelectCat(remove?Cat.All[0]:cat);
        }

        void SelectCat(Cat cat)
        {
            if(settings.Cat==cat) return;
            settings.Cat=cat;
            motion.Reset();
            UpdateIcon();
            AppearanceChanged();
        }

        void Wear(Outfit outfit)
        {
            Outfit prior=settings.CurrentOutfit;
            if(prior.Clothes==outfit.Clothes && prior.Hat==outfit.Hat) return;
            settings.CurrentOutfit=outfit;
            if(prior.Hat!=outfit.Hat)
            {
                int index=settings.Roles.IndexOf(settings.Cat);
                Bitmap replacement=Painter.Portrait(settings.Cat,44,outfit.Hat);
                Bitmap old=portraits[index];
                cats.DropDownItems[index].Image=replacement;
                portraits[index]=replacement;
                old.Dispose();
                UpdateIcon();
            }
            AppearanceChanged();
        }

        void AppearanceChanged()
        {
            if(wardrobe!=null) wardrobe.SyncSelection();
            Save();
            Redraw();
        }

        void UpdateIcon()
        {
            using(Bitmap head=Painter.Portrait(settings.Cat,48,settings.CurrentOutfit.Hat))
            {
                IntPtr handle=head.GetHicon();
                try
                {
                    using(Icon borrowed=Icon.FromHandle(handle))
                    {
                        Icon next=(Icon)borrowed.Clone();
                        tray.Icon=next;
                        if(trayIcon!=null) trayIcon.Dispose();
                        trayIcon=next;
                    }
                }
                finally { Native.DestroyIcon(handle); }
            }
            tray.Text=Program.DisplayName+" · "+settings.Cat.Name+"（右键设置）";
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Screen screen=settings.Position.HasValue ? Screen.FromPoint(settings.Position.Value) : Screen.PrimaryScreen;
            Place(screen,settings.Position,null);
            priorCursor=Cursor.Position; lastFrame=Now;
            loaded=true; UpdateIcon(); tray.Visible=true;
            if(!settings.Paused) SetListening(true);
            Redraw(); timer.Start();
        }

        void SetListening(bool enable,bool announceFailure=true)
        {
            if(!enable) { retryInputAt=double.PositiveInfinity; retryInputDelay=2; }
            if(enable==listening) return;
            bool registered=Native.Subscribe(Handle,enable);
            if(enable && !registered) {
                retryInputAt=Now+retryInputDelay;
                retryInputDelay=Math.Min(30,retryInputDelay*2);
                if(announceFailure) tray.ShowBalloonTip(3500,Program.DisplayName,"键鼠暂时未连接，爪爪会自动重试。",ToolTipIcon.Info);
                return;
            }
            // Pausing stops routing input even if Windows already removed the registration.
            motion.Reset(); priorCursor=Cursor.Position;
            retryInputAt=double.PositiveInfinity; retryInputDelay=2;
            listening=enable;waitForKeyRelease=false;inputMode=FieldPrivacy.Unknown;
            inputWindow=protectedWindow=IntPtr.Zero;
            if(enable) {focusProbe=new PasswordFocus();nextFocusCheck=0;}
            else {if(focusProbe!=null) focusProbe.Dispose();focusProbe=null;motion.SetPrivacy(false,Now);}
        }

        static Point Middle(Rectangle r) { return new Point(r.Left+r.Width/2,r.Top+r.Height/2); }
        internal static Point KeepInside(Point p,Size size,Rectangle area)
        {
            return new Point(Math.Max(area.Left,Math.Min(p.X,area.Right-size.Width)),
                             Math.Max(area.Top,Math.Min(p.Y,area.Bottom-size.Height)));
        }
        float ScaleOn(Screen screen) { return Preferences.Scales[settings.ScaleIndex]*Native.DpiAt(Middle(screen.Bounds)); }

        // With no anchor, use the work area's lower-right corner. An anchor preserves the bottom center on resize.
        void Place(Screen screen,Point? topLeft,Point? bottomCenter,float? knownScale=null)
        {
            if(placing) return;
            placing=true;
            try
            {
                float nextScale=knownScale ?? ScaleOn(screen);
                Size size=new Size((int)Math.Ceiling(Painter.Width*nextScale),(int)Math.Ceiling(Painter.Height*nextScale));
                Rectangle area=screen.WorkingArea;
                int margin=(int)Math.Round(16*nextScale);
                Point p=topLeft ?? new Point(area.Right-size.Width-margin,area.Bottom-size.Height-margin);
                if(bottomCenter.HasValue) p=new Point(bottomCenter.Value.X-size.Width/2,bottomCenter.Value.Y-size.Height);
                p=KeepInside(p,size,area);
                if(surface==null || size!=surface.Size)
                {
                    LayerSurface next=new LayerSurface(size);
                    if(rendered==surface) rendered=null;
                    if(surface!=null) surface.Dispose();
                    surface=next;
                }
                scale=nextScale;
                Rectangle target=new Rectangle(p,size);
                if(Bounds!=target) Bounds=target;
                PositionBadge();
            }
            finally { placing=false; }
        }

        void ResizePet(int index)
        {
            Point anchor=new Point(Left+Width/2,Bottom);
            settings.ScaleIndex=index;
            Place(Screen.FromRectangle(Bounds),null,anchor);
            Save(); Redraw();
        }
        void ToCorner()
        {
            Place(Screen.FromPoint(Cursor.Position),null,null);
            Save(); Redraw();
        }
        void Refit()
        {
            if(!loaded || disposed || dragging || placing) return;
            Place(Screen.FromRectangle(Bounds),Location,null);
            Save(); Redraw();
        }
        void Save()
        {
            saveOpacityAt=double.PositiveInfinity;
            settings.Position=Location;
            settings.Save(settingsFile);
        }

        void Frame()
        {
            if(!loaded || disposed) return;
            double now=Now, elapsed=now-lastFrame;
            lastFrame=now;
            if(now>=saveOpacityAt) Save();
            if(!settings.Paused && !listening && now>=retryInputAt) SetListening(true,false);
            if(waitForKeyRelease && !Native.AnyKeyboardKeyHeld()) waitForKeyRelease=false;
            if(listening && now>=nextFocusCheck) {
                nextFocusCheck=now+.12;
                RefreshPrivacy(false,now);
            }
            if(listening && !motion.Private && !actions.Visible && !dragging && !SettingsFocused)
            {
                Point p=Cursor.Position;
                if(p!=priorCursor) { motion.Tricks.InterruptIdle(now);motion.LastMouse=now; priorCursor=p; }
                PointF eyes=settings.Cat.EyeCenter;
                motion.Step(now,elapsed,(p.X-Left)/scale-eyes.X,(p.Y-Top)/scale-eyes.Y,inputMode==FieldPrivacy.GameSurface);
            }
            bool allowIdle=!motion.Private && !pressing && !dragging && !actions.Visible && !SettingsFocused &&
                !motion.MouseHeld && motion.Left.Key==null && motion.Right.Key==null;
            // Received input already restarts the clock; some desktops generate unrelated activity continuously.
            double quiet=listening || settings.Paused ? double.PositiveInfinity : Native.QuietSeconds();
            motion.Tricks.UpdateIdle(now,quiet,allowIdle,settings.Cat.Longhair);
            bool busy=dragging || motion.Left.Key!=null || motion.Right.Key!=null || (motion.Cover(now)>0 && motion.Cover(now)<1) || motion.Tricks.Current(now)!=null || now-motion.LastMouse<1.35 || now-lastInput<.75;
            int interval=busy?25:66;
            if(timer.Interval!=interval) timer.Interval=interval;
            Redraw();
        }
        bool SettingsFocused {
            get {return showingHelp || (wardrobe!=null && (wardrobe.ContainsFocus || wardrobe.Editing)) ||
                        (opacityPanel!=null && opacityPanel.ContainsFocus);}
        }
        void ReleaseStudioSurface()
        {
            if(studioSurface==null) return;
            if(rendered==studioSurface) rendered=null;
            studioSurface.Dispose();studioSurface=null;
        }
        void Redraw()
        {
            if(surface==null || disposed) return;
            bool preview=wardrobe!=null && wardrobe.Visible;
            float renderScale=preview?Math.Max(scale,wardrobe.PreviewScale):scale;
            LayerSurface target=surface;
            if(renderScale>scale) {
                Size size=new Size((int)Math.Ceiling(Painter.Width*renderScale),(int)Math.Ceiling(Painter.Height*renderScale));
                if(studioSurface==null || studioSurface.Size!=size) {
                    ReleaseStudioSurface();studioSurface=new LayerSurface(size);
                }
                target=studioSurface;
            } else ReleaseStudioSurface();
            art.Render(target.Canvas,renderScale,settings.Cat,motion,Now,settings.CurrentOutfit,settings.Board,settings.MouseStyle,settings.PrintLook);
            target.Finish(settings.PrintLook,renderScale);
            if(target!=surface) surface.CopyFrom(target.Image);
            rendered=target;
            surface.Present(Handle,Location,settings.Transparency);
            if(preview) wardrobe.RefreshPreview();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if(settings.Locked || e.Button!=MouseButtons.Left) return;
            dragStart=PointToScreen(e.Location);
            grab=new PointF((dragStart.X-Left)/scale,(dragStart.Y-Top)/scale);
            pressing=true; dragging=false; dragScreen=null; Capture=true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if(settings.Locked || !pressing) return;
            Point p=PointToScreen(e.Location);
            if(!dragging)
            {
                Size threshold=SystemInformation.DragSize;
                if(Math.Abs(p.X-dragStart.X)<=threshold.Width/2 && Math.Abs(p.Y-dragStart.Y)<=threshold.Height/2) return;
                dragging=true; motion.ClearInput();
            }
            Screen screen=Screen.FromPoint(p);
            if(dragScreen!=screen.DeviceName) {dragScreen=screen.DeviceName;dragScale=ScaleOn(screen);}
            float priorScale=scale;
            Place(screen,new Point(p.X-(int)(grab.X*dragScale),p.Y-(int)(grab.Y*dragScale)),null,dragScale);
            if(priorScale!=scale) Redraw();
        }
        void EndDrag()
        {
            bool moved=dragging;
            pressing=dragging=false;
            if(moved) { priorCursor=Cursor.Position; Save(); }
        }
        protected override void OnMouseCaptureChanged(EventArgs e)
        { base.OnMouseCaptureChanged(e); if(!Capture) EndDrag(); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if(settings.Locked) return;
            if(e.Button==MouseButtons.Left)
            {
                EndDrag(); Capture=false;
            }
            else if(e.Button==MouseButtons.Right && !actions.Visible)
                actions.Show(PointToScreen(e.Location));
        }

        void RefreshPrivacy(bool forKey,double now)
        {
            IntPtr foreground=Native.GetForegroundWindow();
            FieldPrivacy status=SettingsFocused?FieldPrivacy.Ordinary:focusProbe.Probe(forKey);
            if(foreground==IntPtr.Zero || foreground!=Native.GetForegroundWindow()) status=FieldPrivacy.Unknown;
            ApplyPrivacy(status,now,foreground);
        }

        void ApplyPrivacy(FieldPrivacy status,double now,IntPtr foreground)
        {
            IntPtr child=PasswordFocus.FocusedChild(foreground);
            if(foreground!=inputWindow || child!=inputChild || status!=inputMode) motion.ClearInput();
            inputMode=status;inputWindow=foreground;inputChild=child;
            if(status==FieldPrivacy.Unknown) return;
            bool unconfirmedField=status==FieldPrivacy.ActivityOnly || status==FieldPrivacy.WeChatMain;
            if(unconfirmedField && motion.Private && (foreground==IntPtr.Zero || foreground==protectedWindow)) return;
            bool secure=status==FieldPrivacy.Password;
            if(motion.Private && !secure) waitForKeyRelease=Native.AnyKeyboardKeyHeld();
            protectedWindow=secure?foreground:IntPtr.Zero;
            motion.SetPrivacy(secure,now);
        }

        bool HandleKey(Native.RawPayload data,double now)
        {
            if(motion.Private) {motion.Tricks.InterruptIdle(now);return false;}
            bool release=(data.KeyFlags&1)!=0;
            if(inputMode==FieldPrivacy.Unknown) {motion.Tricks.InterruptIdle(now);return false;}
            if(waitForKeyRelease) {
                waitForKeyRelease=Native.AnyKeyboardKeyHeld();
                if(waitForKeyRelease) return false;
            }
            if(inputMode==FieldPrivacy.ActivityOnly) {
                if(release) return false;
                motion.TypingActivity(now);
                return true;
            }
            int key=Native.KeyCode(data);
            if(!release && motion.Tricks.Enabled) motion.Tricks.SetForeground(inputWindow);
            bool shortcut=!release && motion.Tricks.Enabled && Native.ShortcutHeld();
            bool syllableSeparator=!release && !shortcut && motion.Tricks.Enabled && inputMode!=FieldPrivacy.GameSurface &&
                ChineseInput.IsSyllableSeparator(key,inputWindow,inputChild);
            motion.KeyEvent(key,release,now,settings.Labels,shortcut,syllableSeparator);
            return true;
        }

        void HandleInput(Native.RawPacket packet)
        {
            double now=Now;
            if(packet.Header.Type==1) {
                RefreshPrivacy(true,now);
                if(!HandleKey(packet.Data,now)) return;
            } else {
                if(packet.Data.MouseButtons==0) return;
                motion.MouseButtons(packet.Data.MouseButtons,now);
            }
            lastInput=now;
            if(timer.Interval!=25) timer.Interval=25;
        }

        protected override void WndProc(ref Message m)
        {
            if(m.Msg==Native.WmMouseActivate) { m.Result=new IntPtr(3); return; } // MA_NOACTIVATE
            if((uint)m.Msg==Program.WakeMessage && Program.WakeMessage!=0 && loaded)
            {
                Refit();
                if(!settings.Locked && !actions.Visible) actions.Show(new Point(Left+Width/2,Top+Height/2));
                return;
            }
            if(m.Msg==Native.WmInput && listening && !dragging && !actions.Visible && !SettingsFocused)
            {
                Native.RawPacket packet;
                if(Native.ReadInput(m.LParam,out packet)) HandleInput(packet);
            }
            bool geometry=m.Msg==Native.WmDisplayChange || m.Msg==Native.WmDpiChanged ||
                (m.Msg==0x001a && m.WParam==new IntPtr(0x2f)); // SPI_SETWORKAREA
            base.WndProc(ref m);
            if(geometry && loaded && !placing && !disposed && IsHandleCreated) BeginInvoke((MethodInvoker)Refit);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if(e.Cancel) return;
            timer.Stop(); if(loaded) Save();
            if(wardrobe!=null) wardrobe.Close();
            if(opacityPanel!=null) opacityPanel.Close();
            if(listening && IsHandleCreated) { Native.Subscribe(Handle,false); listening=false; }
            tray.Visible=false;
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing && !disposed)
            {
                disposed=true;
                timer.Dispose(); tray.Visible=false; tray.Dispose();
                if(trayIcon!=null) trayIcon.Dispose();
                if(focusProbe!=null) focusProbe.Dispose();
                if(unlockBadge!=null) unlockBadge.Dispose();
                lockedActions.Dispose(); actions.Dispose();
                foreach(Bitmap p in portraits) p.Dispose();
                if(surface!=null) surface.Dispose();
                ReleaseStudioSurface();
                art.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
