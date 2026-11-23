using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KeyboardCat
{
    // A separate top-level window stays clickable while the entire layered pet passes through.
    internal sealed class UnlockBadge : Form
    {
        readonly ToolTip hint = new ToolTip();
        readonly Action unlock;
        bool hover, armed;

        public UnlockBadge(Action unlock)
        {
            this.unlock=unlock;
            Text=AccessibleName="解锁桌宠";
            AccessibleRole=AccessibleRole.PushButton;
            FormBorderStyle=FormBorderStyle.None; StartPosition=FormStartPosition.Manual;
            ShowInTaskbar=false; TopMost=true; TabStop=false; AutoScaleMode=AutoScaleMode.None;
            Cursor=Cursors.Hand; BackColor=Color.FromArgb(51,56,65);
            SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);
            hint.SetToolTip(this,"解锁桌宠");
        }
        protected override bool ShowWithoutActivation { get {return true;} }
        protected override CreateParams CreateParams {
            get {CreateParams p=base.CreateParams;p.ExStyle|=Native.NoActivate|Native.ToolWindow;return p;}
        }
        public void Place(Rectangle pet)
        {
            Rectangle area=Screen.FromRectangle(pet).WorkingArea;
            int size=(int)Math.Round(28*Native.DpiAt(new Point(pet.Left+pet.Width/2,pet.Top+pet.Height/2)));
            Point pos=PetForm.KeepInside(new Point(pet.Right-size-4,pet.Top+4),new Size(size,size),area);
            if(Width!=size || Height!=size || Region==null) {
                Size=new Size(size,size);
                using(GraphicsPath circle=new GraphicsPath()) {
                    circle.AddEllipse(0,0,size,size);
                    Region old=Region;Region=new Region(circle);if(old!=null) old.Dispose();
                }
            }
            Location=pos;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            g.ScaleTransform(ClientSize.Width/28f,ClientSize.Height/28f);
            using(SolidBrush disc=new SolidBrush(hover?Color.FromArgb(88,73,79):BackColor))
            using(Pen rim=new Pen(Color.FromArgb(248,217,181),1.35f))
            using(Pen ink=new Pen(Color.FromArgb(255,241,216),1.8f)) {
                g.FillEllipse(disc,0,0,28,28);g.DrawEllipse(rim,1,1,26,26);
                g.DrawArc(ink,10,6.5f,8,10,180,180);
                g.DrawLine(ink,10,11.5f,10,14);g.DrawLine(ink,18,11.5f,18,14);
                g.DrawRectangle(ink,8,13,12,8);g.DrawLine(ink,14,16,14,18.5f);
            }
        }
        protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Invalidate();}
        protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;Invalidate();}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);armed=e.Button==MouseButtons.Left;}
        protected override void OnMouseUp(MouseEventArgs e){
            base.OnMouseUp(e);bool hit=armed && e.Button==MouseButtons.Left && Region.IsVisible(e.Location);
            armed=false;if(hit) unlock();
        }
        protected override void WndProc(ref Message m){
            if(m.Msg==Native.WmMouseActivate){m.Result=new IntPtr(3);return;}
            base.WndProc(ref m);
        }
        protected override void Dispose(bool disposing){if(disposing) hint.Dispose();base.Dispose(disposing);}
    }
}
