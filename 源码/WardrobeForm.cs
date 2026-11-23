using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace KeyboardCat
{
    internal sealed class WardrobeForm : Form
    {
        internal static readonly Color Paper=CoverArt.Paper;
        readonly Preferences settings;
        readonly Motion motion;
        readonly WardrobePreview preview;
        readonly ComboBox roles;
        readonly Dictionary<Cat,Bitmap> rolePortraits=new Dictionary<Cat,Bitmap>();
        readonly Button edit,reset;
        readonly Label outfitLine,deskLine,scope;
        readonly ContextMenuStrip trickChoices=new ContextMenuStrip();
        public float PreviewScale {get {return preview.FrameScale;}}
        readonly StudioButton print,classic;
        readonly Panel[] groups=new Panel[4];
        readonly StudioButton[] tabs=new StudioButton[4];
        readonly List<WardrobeChoice>[] choices={new List<WardrobeChoice>(),new List<WardrobeChoice>(),new List<WardrobeChoice>(),new List<WardrobeChoice>()};
        readonly Font uiFont=new Font("Microsoft YaHei UI",9f),headingFont=new Font("Microsoft YaHei UI",15f,FontStyle.Bold);
        readonly Font mastheadFont=new Font("SimSun",24f,FontStyle.Bold);
        public bool Editing {get;private set;}
        bool syncing;

        public WardrobeForm(Preferences settings,Motion motion,Func<Bitmap> frame,Action<Cat> selectCat,Action<Outfit> wear,
                            Action<bool,int> setDesk,Action<Cat,bool> changeRole,Action<bool> setLook,Action<WordTrick> perform)
        {
            this.settings=settings;this.motion=motion;
            SuspendLayout();
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
            Font=uiFont;Text=Program.DisplayName+" · 装扮工作室";BackColor=Paper;ForeColor=CoverArt.Ink;
            FormBorderStyle=FormBorderStyle.FixedToolWindow;StartPosition=FormStartPosition.Manual;
            ShowInTaskbar=false;MaximizeBox=false;MinimizeBox=false;TopMost=true;AutoScroll=true;
            Size contentSize=new Size(782,520);ClientSize=contentSize;
            Panel page=new Panel {Location=Point.Empty,Size=contentSize,BackColor=Paper};
            Controls.Add(page);
            Control mark=new StudioMark {Bounds=new Rectangle(24,16,36,32)};
            page.Controls.Add(mark);
            page.Controls.Add(LabelAt(Program.DisplayName,66,6,281,48,mastheadFont));
            page.Controls.Add(LabelAt("桌宠画风",545,14,73,30,uiFont));
            print=new StudioButton("画报",StudioButton.Kind.Switch,622,14,65,31);
            classic=new StudioButton("原画",StudioButton.Kind.Switch,687,14,69,31);
            print.Click+=delegate {setLook(true);};classic.Click+=delegate {setLook(false);};
            page.Controls.AddRange(new Control[]{print,classic});
            Rule(page,24,59,732,CoverArt.Ink);

            preview=new WardrobePreview(settings,frame) {Bounds=new Rectangle(24,77,CoverArt.Width,CoverArt.Height)};
            page.Controls.Add(preview);
            outfitLine=CaptionBand(page,24,415,332);
            deskLine=LabelAt("",380,415,376,27,uiFont);page.Controls.Add(deskLine);
            page.Controls.Add(LabelAt("角色",24,448,46,20,uiFont));
            roles=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=21,DropDownWidth=218,BackColor=Color.White,
                ForeColor=CoverArt.Ink,AccessibleName="选择角色",Bounds=new Rectangle(24,476,136,28)};
            roles.DrawItem+=DrawRole;
            roles.SelectedIndexChanged+=delegate {if(!syncing && roles.SelectedItem is Cat) selectCat((Cat)roles.SelectedItem);};
            Button create=new StudioButton("新建角色…",StudioButton.Kind.Quiet,169,472,89,32);
            edit=new StudioButton("编辑角色…",StudioButton.Kind.Quiet,264,472,92,32);
            create.Click+=delegate {EditRole(false,changeRole);};edit.Click+=delegate {EditRole(true,changeRole);};
            page.Controls.AddRange(new Control[]{roles,create,edit});

            page.Controls.Add(LabelAt("装扮工作室",380,72,260,35,headingFont));
            scope=LabelAt("",537,77,219,27,uiFont);scope.TextAlign=ContentAlignment.MiddleRight;
            scope.ForeColor=Cat.Rgb(0x615e56);page.Controls.Add(scope);
            string[] headings={"衣服","头饰","键盘","鼠标"};
            Rule(page,380,145,376,Cat.Rgb(0xccc9bf));
            for(int i=0;i<4;i++) {
                int group=i;
                tabs[i]=new StudioButton(headings[i],StudioButton.Kind.Tab,380+i*94,113,94,33);
                tabs[i].AccessibleName=headings[i]+"分类";
                tabs[i].Click+=delegate {ShowGroup(group);};
                page.Controls.Add(tabs[i]);
                Panel grid=new Panel {Bounds=new Rectangle(380,158,378,257),BackColor=Paper,AccessibleName=headings[i]+"选项"};
                groups[i]=grid;page.Controls.Add(grid);
                for(int j=0;j<CoverArt.Counts[i];j++) {
                    int index=j;
                    WardrobeChoice choice=new WardrobeChoice(CoverArt.ItemName(i,j),CoverArt.Item(i,j,i==2?224:128,false),i,j) {
                        Bounds=new Rectangle(j%3*127,j/3*86,120,79)};
                    choice.Click+=delegate {
                        if(group==0) wear(new Outfit((Garment)index,settings.CurrentOutfit.Hat));
                        else if(group==1) wear(new Outfit(settings.CurrentOutfit.Clothes,(Headwear)index));
                        else setDesk(group==2,index);
                    };
                    choices[i].Add(choice);grid.Controls.Add(choice);
                }
            }
            Rule(page,380,448,376,Cat.Rgb(0xc1c0b7));
            WordTrick selectedTrick=WordTrick.Fish;
            Button play=new StudioButton("试演抱鱼",StudioButton.Kind.Play,380,471,103,33);
            Button chooseTrick=new StudioButton("▾",StudioButton.Kind.Switch,482,471,30,33);
            chooseTrick.AccessibleName="选择试演动作";
            play.Click+=delegate {perform(selectedTrick);};
            foreach(WordTrick entry in WordTrick.All) {
                WordTrick trick=entry;
                ToolStripMenuItem item=new ToolStripMenuItem(trick.Label);
                item.Tag=trick;item.ShortcutKeyDisplayString=trick.Word+" / "+trick.Pinyin;
                item.Click+=delegate {
                    selectedTrick=trick;play.Text="试演"+trick.ShortLabel;perform(trick);
                };
                trickChoices.Items.Add(item);
            }
            chooseTrick.Click+=delegate {
                foreach(ToolStripMenuItem item in trickChoices.Items) item.Checked=item.Tag==selectedTrick;
                trickChoices.Show(chooseTrick,new Point(0,chooseTrick.Height));
            };
            reset=new StudioButton("恢复原装",StudioButton.Kind.Quiet,519,471,91,33);
            reset.Click+=delegate {wear(new Outfit());};
            Button done=new StudioButton("完成",StudioButton.Kind.Primary,678,470,78,35);
            done.Click+=delegate {Close();};CancelButton=done;
            page.Controls.AddRange(new Control[]{play,chooseTrick,reset,done});
            ShowGroup(0);SyncSelection();ResumeLayout(true);
        }
        Label CaptionBand(Control parent,int x,int y,int width)
        {
            Label caption=LabelAt("",x,y,width,27,uiFont);
            caption.BackColor=CoverArt.Ink;caption.ForeColor=CoverArt.Paper;
            caption.Padding=new Padding(12,0,8,0);parent.Controls.Add(caption);return caption;
        }
        static Label LabelAt(string text,int x,int y,int w,int h,Font font)
        {
            return new Label {Text=text,UseMnemonic=false,Font=font,Bounds=new Rectangle(x,y,w,h),ForeColor=CoverArt.Ink,
                TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};
        }
        static void Rule(Control parent,int x,int y,int w,Color color)
        {parent.Controls.Add(new Panel {Bounds=new Rectangle(x,y,w,1),BackColor=color,TabStop=false});}
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Rectangle area=Screen.FromControl(this).WorkingArea;
            if(Width>area.Width || Height>area.Height) Size=new Size(Math.Min(Width,area.Width),Math.Min(Height,area.Height));
        }
        void DrawRole(object sender,DrawItemEventArgs e)
        {
            if(e.Index<0 || e.Index>=roles.Items.Count) return;
            Cat cat=(Cat)roles.Items[e.Index];Bitmap portrait;
            if(!rolePortraits.TryGetValue(cat,out portrait)) {
                portrait=Painter.Portrait(cat,64);rolePortraits.Add(cat,portrait);
            }
            bool selected=(e.State&DrawItemState.Selected)!=0 && (e.State&DrawItemState.ComboBoxEdit)==0;
            Color bg=selected?CoverArt.Ink:Color.White,fg=selected?CoverArt.Paper:CoverArt.Ink;
            using(SolidBrush brush=new SolidBrush(bg)) e.Graphics.FillRectangle(brush,e.Bounds);
            int size=e.Bounds.Height-2;
            e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(portrait,e.Bounds.X+2,e.Bounds.Y+1,size,size);
            Rectangle name=e.Bounds;name.X+=size+6;name.Width-=size+8;
            TextRenderer.DrawText(e.Graphics,cat.Name,Font,name,fg,TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding|
                TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
            if((e.State&DrawItemState.Focus)!=0) e.DrawFocusRectangle();
        }
        void ShowGroup(int active)
        {
            scope.Text=active<2?"穿搭随角色保存":"所有角色共用这套桌搭";
            for(int i=0;i<groups.Length;i++) {groups[i].Visible=i==active;tabs[i].Selected=i==active;}
        }
        void EditRole(bool existing,Action<Cat,bool> changeRole)
        {
            Editing=true;motion.ClearInput();
            try {
                using(CatEditor dialog=new CatEditor(settings,existing)) {
                    DialogResult result=dialog.ShowDialog(this);
                    if(result==DialogResult.OK) changeRole(dialog.ResultCat,false);
                    else if(result==DialogResult.Yes) changeRole(settings.Cat,true);
                }
            }
            finally {Editing=false;motion.ClearInput();}
        }
        public void SyncSelection()
        {
            syncing=true;
            bool changed=roles.Items.Count!=settings.Roles.Count;
            for(int i=0;!changed && i<roles.Items.Count;i++) changed=roles.Items[i]!=settings.Roles[i];
            if(changed) {
                roles.BeginUpdate();
                foreach(Bitmap image in rolePortraits.Values) image.Dispose();
                rolePortraits.Clear();roles.Items.Clear();roles.Items.AddRange(settings.Roles.ToArray());
                roles.EndUpdate();
            }
            roles.SelectedItem=settings.Cat;edit.Enabled=settings.Cat.IsCustom;
            int[] selected={(int)settings.CurrentOutfit.Clothes,(int)settings.CurrentOutfit.Hat,settings.Board,settings.MouseStyle};
            for(int i=0;i<choices.Length;i++) for(int j=0;j<choices[i].Count;j++) {
                choices[i][j].Checked=j==selected[i];choices[i][j].PrintLook=settings.PrintLook;
            }
            string clothes=CoverArt.ItemName(0,selected[0]),hat=CoverArt.ItemName(1,selected[1]);
            outfitLine.Text="穿搭   "+(selected[0]==0?(selected[1]==0?"原装毛色":hat):(selected[1]==0?clothes:clothes+"  ·  "+hat));
            deskLine.Text="桌面   "+CoverArt.ItemName(2,selected[2])+"  ·  "+CoverArt.ItemName(3,selected[3]);
            print.Selected=settings.PrintLook;classic.Selected=!settings.PrintLook;
            reset.Enabled=!settings.CurrentOutfit.IsPlain;syncing=false;preview.Invalidate();
        }
        public void RefreshPreview() {preview.Invalidate();}
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if(disposing) {foreach(Bitmap image in rolePortraits.Values) image.Dispose();rolePortraits.Clear();trickChoices.Dispose();uiFont.Dispose();headingFont.Dispose();mastheadFont.Dispose();}
        }
    }

    internal sealed class StudioMark : Control
    {
        protected override void OnPaint(PaintEventArgs e) {CoverArt.Mark(e.Graphics,0,0,Math.Min(Width/36f,Height/32f));}
    }

    internal sealed class StudioButton : Button
    {
        public enum Kind {Tab,Switch,Quiet,Play,Primary}
        readonly Kind kind;
        bool selected,hover,pressed;
        public bool Selected {
            get {return selected;}
            set {if(selected==value) return;selected=value;AccessibleDescription=value?"当前选中":null;Invalidate();}
        }
        public StudioButton(string label,Kind kind,int x,int y,int w,int h)
        {
            this.kind=kind;Text=label;Bounds=new Rectangle(x,y,w,h);BackColor=CoverArt.Paper;ForeColor=CoverArt.Ink;
            FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;Cursor=Cursors.Hand;
            SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);
        }
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);pressed=e.Button==MouseButtons.Left;Invalidate();}
        protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture){pressed=false;Invalidate();}}
        protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g=e.Graphics;float d=g.DpiX/96f;
            bool dark=kind==Kind.Primary || (kind==Kind.Switch && selected);
            Color text=Enabled?(dark?CoverArt.Paper:CoverArt.Ink):Cat.Rgb(0x847f74);
            Color fill=dark?CoverArt.Ink:(hover && Enabled?Cat.Rgb(0xeae6da):CoverArt.Paper);
            if(pressed && hover && Enabled) fill=dark?Cat.Rgb(0x404952):Cat.Rgb(0xdcd6c8);
            g.Clear(fill);
            if(kind==Kind.Switch || kind==Kind.Play) {
                using(Pen edge=new Pen(CoverArt.Ink,d)) g.DrawRectangle(edge,d/2,d/2,Width-d,Height-d);
            }
            if(kind==Kind.Tab) {
                using(SolidBrush line=new SolidBrush(selected?CoverArt.Accent:Cat.Rgb(0xccc9bf)))
                    g.FillRectangle(line,0,Height-(selected?3:1)*d,Width,(selected?3:1)*d);
                if(selected) text=CoverArt.Accent;
            }
            Rectangle label=ClientRectangle;if(pressed && hover) label.Offset(0,(int)Math.Ceiling(d));
            if(kind==Kind.Play) {
                float mid=Height/2f;using(SolidBrush ink=new SolidBrush(CoverArt.Accent))
                    g.FillPolygon(ink,new PointF[]{new PointF(12*d,mid-4*d),new PointF(12*d,mid+4*d),new PointF(19*d,mid)});
                label.X+=(int)(12*d);label.Width-=(int)(12*d);
            }
            TextRenderer.DrawText(g,Text,Font,label,text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
            if(Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(ClientRectangle,-4,-4),text,fill);
        }
    }

    internal sealed class WardrobeChoice : RadioButton
    {
        readonly Bitmap artwork,printed;
        readonly bool keyboard;
        bool printLook;
        public bool PrintLook {set {if(printLook!=value) {printLook=value;Invalidate();}}}
        readonly Color tint;
        bool hover;
        static readonly int[] tints={0xeee9dc,0xf2deda,0xe0e8e7,0xf4e7c4,0xe3e7d6,0xe7e4ed,0xeee3d8};
        public WardrobeChoice(string name,Bitmap artwork,int group,int index)
        {
            this.artwork=artwork;printed=(Bitmap)artwork.Clone();InkLook.Apply(printed,artwork.Width/64f);
            keyboard=group==2;tint=Cat.Rgb(tints[(index+(group>=2?group:0))%tints.Length]);
            Text=AccessibleName=name;AccessibleRole=AccessibleRole.RadioButton;Appearance=Appearance.Button;
            AutoSize=false;AutoCheck=false;Cursor=Cursors.Hand;
            SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);
        }
        protected override void OnCheckedChanged(EventArgs e)
        {base.OnCheckedChanged(e);TabStop=Checked;Invalidate();}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override bool ProcessDialogKey(Keys keyData)
        {
            Keys key=keyData&Keys.KeyCode;
            if(key==Keys.Left || key==Keys.Right || key==Keys.Up || key==Keys.Down) {
                int i=Parent.Controls.IndexOf(this),last=Parent.Controls.Count-1,target=i;
                if(key==Keys.Left && i%3>0) target=i-1;
                if(key==Keys.Right && i%3<2 && i<last) target=i+1;
                if(key==Keys.Up && i>=3) target=i-3;
                if(key==Keys.Down && i+3<=last) target=i+3;
                if(target==i) return true;
                WardrobeChoice next=(WardrobeChoice)Parent.Controls[target];
                next.Focus();next.PerformClick();return true;
            }
            return base.ProcessDialogKey(keyData);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g=e.Graphics;float d=g.DpiX/96f;
            g.Clear(tint);
            int labelHeight=(int)(23*d),artHeight=Height-labelHeight,side=Math.Min(Width,(int)(artHeight*.98f));
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            Bitmap image=printLook?printed:artwork;
            if(keyboard) {
                int w=Math.Min(Width-(int)(14*d),artHeight*2),h=w/2;
                g.DrawImage(image,new Rectangle((Width-w)/2,(artHeight-h)/2,w,h),0,image.Height/4,image.Width,image.Height/2,GraphicsUnit.Pixel);
            } else if(side>0) g.DrawImage(image,(Width-side)/2,(artHeight-side)/2+1,side,side);
            Rectangle box=new Rectangle(2,artHeight-1,Width-4,labelHeight);
            TextRenderer.DrawText(g,Text,Font,box,CoverArt.Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|
                TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix);
            if(Checked || hover) {
                float stroke=(Checked?2:1)*d;using(Pen p=new Pen(Checked?CoverArt.Accent:CoverArt.Ink,stroke))
                    g.DrawRectangle(p,stroke/2,stroke/2,Width-stroke,Height-stroke);
            }
            if(Checked) {
                float x=Width-12*d,y=11*d;
                g.SmoothingMode=SmoothingMode.AntiAlias;
                using(SolidBrush a=new SolidBrush(CoverArt.Accent)) g.FillEllipse(a,x-6*d,y-6*d,12*d,12*d);
                using(Pen p=new Pen(Color.White,1.4f*d)) g.DrawLines(p,new PointF[]{new PointF(x-3*d,y),new PointF(x-d,y+2*d),new PointF(x+3*d,y-2*d)});
            }
            if(Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(ClientRectangle,-4,-4));
        }
        protected override void Dispose(bool disposing) {if(disposing) {artwork.Dispose();printed.Dispose();}base.Dispose(disposing);}
    }

    internal sealed class WardrobePreview : Control
    {
        readonly Preferences settings;
        readonly Func<Bitmap> frame;
        readonly Font nameFont=new Font("SimSun",40f,FontStyle.Bold),compactName=new Font("SimSun",24f,FontStyle.Bold);
        Bitmap cover;
        Cat coverCat;
        float coverDpi;
        public float FrameScale {get {return Math.Min(Width/(float)Painter.Width,
            (Height-76*Height/(float)CoverArt.Height)/Painter.Height);}}

        public WardrobePreview(Preferences settings,Func<Bitmap> frame)
        {
            this.settings=settings;this.frame=frame;
            AccessibleName="当前穿搭动态封面";AccessibleRole=AccessibleRole.Graphic;TabStop=false;
            SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if(Width<12 || Height<12) return;
            float sx=Width/(float)CoverArt.Width,sy=Height/(float)CoverArt.Height;
            Graphics g=e.Graphics;
            if(cover==null || cover.Size!=Size || coverCat!=settings.Cat || coverDpi!=g.DpiX) {
                if(cover!=null) cover.Dispose();
                coverCat=settings.Cat;coverDpi=g.DpiX;
                cover=new Bitmap(Width,Height,PixelFormat.Format24bppRgb);cover.SetResolution(g.DpiX,g.DpiY);
                using(Graphics bg=Graphics.FromImage(cover)) {
                    var state=bg.Save();bg.ScaleTransform(sx,sy);CoverArt.Background(bg,CoverArt.Edition(coverCat));bg.Restore(state);

                }
            }
            g.DrawImageUnscaled(cover,0,0);
            var flags=TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis;
            bool shortName=System.Globalization.StringInfo.ParseCombiningCharacters(coverCat.Name).Length<=4;
            TextRenderer.DrawText(g,coverCat.Name,shortName?nameFont:compactName,
                new Rectangle((int)(16*sx),(int)(5*sy),(int)(247*sx),(int)(68*sy)),CoverArt.Ink,flags);
            TextRenderer.DrawText(g,Cat.Shapes[coverCat.ShapeIndex]+"  /  "+Cat.EyeNames[coverCat.Eyes],Font,
                new Rectangle((int)(19*sx),(int)(72*sy),(int)(265*sx),(int)(24*sy)),CoverArt.Ink,flags);
            Bitmap image=frame();
            if(image==null) return;
            float scale=FrameScale;
            Size size=new Size((int)Math.Round(Painter.Width*scale),(int)Math.Round(Painter.Height*scale));
            Rectangle box=new Rectangle((Width-size.Width)/2,Height-size.Height-(int)(4*sy),size.Width,size.Height);
            if(Math.Abs(image.Width-size.Width)<=1 && Math.Abs(image.Height-size.Height)<=1) g.DrawImageUnscaled(image,box.Location);
            else {
                g.InterpolationMode=InterpolationMode.HighQualityBilinear;g.PixelOffsetMode=PixelOffsetMode.Half;
                g.DrawImage(image,box,0,0,image.Width,image.Height,GraphicsUnit.Pixel);
            }
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing){if(cover!=null)cover.Dispose();nameFont.Dispose();compactName.Dispose();}
            base.Dispose(disposing);
        }
    }
}
