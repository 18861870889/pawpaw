using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace KeyboardCat
{
    internal sealed partial class Painter
    {
        static PointF Toward(PointF a, PointF b, float amount)
        {
            float dx=b.X-a.X, dy=b.Y-a.Y;
            float distance=(float)Math.Sqrt(dx*dx+dy*dy);
            float part = distance < .001f ? 0 : Math.Min(.42f,amount/distance);
            return new PointF(a.X+part*dx,a.Y+part*dy);
        }

        static GraphicsPath RoundedPoly(float radius, params PointF[] corners)
        {
            GraphicsPath path = new GraphicsPath();
            for (int i=0;i<corners.Length;i++)
            {
                PointF prev = corners[(i+corners.Length-1)%corners.Length], p=corners[i];
                PointF next = corners[(i+1)%corners.Length];
                PointF a=Toward(p,prev,radius), b=Toward(p,next,radius);
                path.AddBezier(a,Motion.Mix(a,p,.66f),Motion.Mix(b,p,.66f),b);
            }
            path.CloseFigure(); return path;
        }
        static GraphicsPath RoundRect(float x,float y,float w,float h,float r)
        {
            return RoundedPoly(r,new PointF(x,y),new PointF(x+w,y),new PointF(x+w,y+h),new PointF(x,y+h));
        }

        internal static readonly string[] BoardNames={"原味奶油","樱花汽水","薄荷抹茶","午夜星空"};
        internal static readonly string[] MouseNames={"奶白经典","草莓奶昔","青柠小叶","星夜无线"};
        // Bottom case, deck, accent, modifier, function, typing keys, legend and pressed highlight.
        static readonly int[,] boardColors={
            {0x34353e,0x54535a,0xf6b45f,0xdcd9cd,0xbacfc2,0xfcf4e4,0x5d595a,0xffb456},
            {0x925a70,0xb2768b,0xef94ae,0xf8c7d2,0xe9b1c7,0xffedf0,0x674555,0xea83aa},
            {0x506963,0x668373,0xe8bf75,0xb8d4b4,0xa8d4ca,0xe8f0d8,0x3a574b,0xf7c373},
            {0x2c304b,0x444563,0xa28fc9,0x6b708f,0x68628a,0x505773,0xffefd6,0xe2ad68}
        };

        void EnsureProps(float scale,int board,int mouse)
        {
            if(props!=null && propScale==scale && propKeyboard==board && propMouse==mouse) return;
            if(props!=null) props.Dispose();
            propScale=scale;propKeyboard=board;propMouse=mouse;
            props=new Bitmap((int)Math.Ceiling(Width*scale),(int)Math.Ceiling(Height*scale),PixelFormat.Format32bppPArgb);
            using(Graphics g=Graphics.FromImage(props))
            {
                Setup(g);g.Clear(Color.Transparent);g.ScaleTransform(scale,scale);
                DrawKeyboard(g,board);
                DrawMouse(g,mouse);
            }
        }

        void DrawKeyboard(Graphics g,int board)
        {
            Color bottomColor=C(boardColors[board,0]),deckColor=C(boardColors[board,1]);
            using(GraphicsPath bottom=RoundedPoly(4,new PointF(64,205),new PointF(294,209),new PointF(300,249),
                new PointF(295,253),new PointF(53,247),new PointF(52,243)))
            {
                Fill(g,bottom,bottomColor);Stroke(g,bottom,Color.FromArgb(115,255,242,220),2.8f);
                Stroke(g,bottom,board==0?C(0x323039):Tint(bottomColor,C(0x352c3a),.3f),1.35f);
            }
            using(GraphicsPath deck=RoundedPoly(4,new PointF(66,200),new PointF(292,204.5f),
                new PointF(299,247.5f),new PointF(53,242.5f)))
            {
                Fill(g,deck,deckColor);Stroke(g,deck,board==0?C(0x35343d):bottomColor,1.4f);
            }
            foreach(Cap cap in Keyboard.Caps) DrawCap(g,cap,0,board);
            Curve(g,board==0?C(0x858088):Tint(deckColor,Color.White,.42f),.8f,62,244.2f,111,245.3f,238,248.3f,282,249.5f);
            Oval(g,board==0?C(0xf4b560):C(boardColors[board,7]),285,248.1f,3,1.4f);
        }

        void DrawCap(Graphics g, Cap key, float down,int board=0)
        {
            float x=key.X+.055f, right=key.X+key.Width-.055f, y=key.Row+.055f, end=key.Row+.925f;
            PointF a=Keyboard.Project(right,end), b=Keyboard.Project(x,end);
            PointF c=Keyboard.Project(x,y), d=Keyboard.Project(right,y);
            Color top=C(boardColors[board,key.Tone==1?2:key.Tone==2?3:key.Tone==3?4:5]);
            Color side = Tint(top,C(0x6b6263),.42f);
            using (GraphicsPath bevel = RoundedPoly(1.5f,a,b,c,d)) Fill(g,bevel,side);
            a.Y+=.16f+down*1.0f; b.Y+=.16f+down*1.0f; c.Y-=1.22f-down; d.Y-=1.22f-down;
            if (down>0) top = Tint(top,C(boardColors[board,7]),down*.93f);
            using (GraphicsPath face=RoundedPoly(1.25f,a,b,c,d)) Fill(g,face,top);
            if (key.Legend == "Space")
            {
                PointF m=key.Center;
                Curve(g,Tint(top,C(0x65665d),.5f),.6f,m.X-5,m.Y-.4f+down,m.X-3,m.Y-1.3f+down,m.X+3,m.Y-1.3f+down,m.X+5,m.Y-.6f+down);
                return;
            }
            PointF origin=key.Center; origin.Y-=.25f-down;
            GraphicsState legend=g.Save();g.TranslateTransform(origin.X,origin.Y);g.RotateTransform(180);
            using (SolidBrush ink = new SolidBrush(board==3 && down>.5f?C(0x473951):C(boardColors[board,6])))
                g.DrawString(key.Legend,legendFont,ink,PointF.Empty,center);
            g.Restore(legend);
        }

        // Local coordinates are from the cat's seat: button 0 is its left button.
        static readonly PointF MouseCenter=new PointF(34,222);
        static readonly PointF[] MouseButtons={new PointF(-6,-11),new PointF(7,-11),new PointF(.5f,-9)};
        static readonly PointF[] MouseContacts=ProjectMouseButtons();
        const int MouseFlashFrames=8;

        static Matrix MousePlacement()
        {
            Matrix placement=new Matrix();
            placement.Translate(MouseCenter.X,MouseCenter.Y);
            placement.Rotate(195);
            return placement;
        }

        static PointF[] ProjectMouseButtons()
        {
            PointF[] contacts=(PointF[])MouseButtons.Clone();
            using(Matrix placement=MousePlacement()) placement.TransformPoints(contacts);
            return contacts;
        }

        static PointF MousePawTarget(int side,float pressure)
        {
            PointF contact=MouseContacts[side];
            contact.Y-=2.8f*(1-pressure);
            return contact;
        }

        static void DrawMouse(Graphics g,int style=0)
        {
            GraphicsState s=g.Save();
            using(Matrix placement=MousePlacement()) g.MultiplyTransform(placement);
            Color upper=C(new int[]{0xf9f5e6,0xffc6cf,0xd9edb7,0x7b81a7}[style]);
            Color lower=C(new int[]{0xc8d8ca,0xe795a5,0x99c8a6,0x484d72}[style]);
            using (GraphicsPath shell=Shape(-13,-6, -13,-16,-8,-22,0,-22,
                9,-22,14,-17,14,-6, 15,6,12,17,3,19,
                -6,21,-13,15,-14,5, -14,1,-14,-3,-13,-6))
            {
                using (LinearGradientBrush b=new LinearGradientBrush(new RectangleF(-14,-22,29,42),upper,lower,236))
                    g.FillPath(b,shell);
                Stroke(g,shell,Color.FromArgb(135,255,246,222),3.1f);
                Stroke(g,shell,C(0x54555a),1.4f);
            }
            Curve(g,C(0xacafa5),.8f,0,-21,.6f,-15,1,-8,1,-1);
            Curve(g,C(0xb4b7a9),.75f,-12,-1,-5,1,7,2,13,0);
            using (GraphicsPath wheel=RoundRect(-1.8f,-13,4.6f,8.4f,2))
            { Fill(g,wheel,style==1?C(0x79a47c):style==2?C(0x659b80):C(0xe4a154)); Stroke(g,wheel,C(0x907a64),.7f); }
            Curve(g,Color.FromArgb(200,255,255,240),1.25f,11,3,11,8,8,13,6,14);
            if(style==1) {
                for(int i=0;i<5;i++) Oval(g,C(0xffefcb),-8+i*3.5f,4+(i%2)*4,1.4f,2.5f);
                Patch(g,C(0x739d76),-4,-16, -5,-20,-2,-21,0,-18, 2,-22,6,-20,4,-17, 3,-15,0,-15,-4,-16);
            } else if(style==2) {
                Patch(g,C(0x599878),-5,12, -7,5,0,2,7,2, 7,8,2,12,-5,12);
                Curve(g,C(0xe4f0c5),.85f,-5,12,-2,9,1,7,5,5);
            } else if(style==3) {
                Oval(g,C(0xffdfa1),-6,3,10,10);Oval(g,Tint(upper,lower,.65f),-2.5f,1.6f,8.7f,9);
                Oval(g,C(0xffefd8),6,8,2,2);
            }
            g.Restore(s);
        }

        public static Bitmap DeskIcon(bool keyboard,int style,int size)
        {
            Bitmap icon=new Bitmap(size,size,PixelFormat.Format32bppPArgb);
            using(Painter painter=new Painter())
            using(Graphics g=Graphics.FromImage(icon)) {
                Setup(g);g.Clear(Color.Transparent);g.TranslateTransform(size*.5f,size*.5f);
                if(keyboard) {
                    float s=size/256f;g.ScaleTransform(s,s);g.TranslateTransform(-176,-224);
                    painter.DrawKeyboard(g,style);
                } else {
                    float s=size/56f;g.ScaleTransform(s,s);g.TranslateTransform(-MouseCenter.X,-MouseCenter.Y);
                    DrawMouse(g,style);
                }
            }
            return icon;
        }

        static void DrawMouseFeedback(Graphics g,int side,float progress)
        {
            if(progress<0 || progress>=1) return;
            PointF button=MouseButtons[side];
            GraphicsState state=g.Save();
            using(Matrix placement=MousePlacement()) g.MultiplyTransform(placement);
            g.TranslateTransform(button.X,button.Y);
            float front=-12-progress*4,spread=5+progress*3;
            using(Pen ink=new Pen(Color.FromArgb((int)(200*(1-progress)),236,163,76),1.7f)) {
                ink.StartCap=ink.EndCap=LineCap.Round;
                g.DrawLine(ink,0,front,0,front-4);
                if(side!=1) g.DrawLine(ink,-spread,front+3,-spread-3,front);
                if(side!=0) g.DrawLine(ink,spread,front+3,spread+3,front);
            }
            g.Restore(state);
        }

        void DrawBubble(Graphics g, KeyBubble bubble, double now)
        {
            float p=(float)((now-bubble.Start)/(bubble.IsWord?Motion.WordBubbleLifetime:Motion.BubbleLifetime));
            if (p<0 || p>=1) return;
            float alpha=1-Motion.Smooth((p-.55f)/.45f);
            float rise=1-(1-p)*(1-p);
            float w=Math.Max(20,11+bubble.Label.Length*7.2f), h=17.5f;
            float x=bubble.Origin.X+(bubble.Lane-1)*(5+5*rise);
            float y=bubble.Origin.Y-19-34*rise-bubble.Lane*4;
            if(bubble.IsWord) {
                w=Math.Max(40,g.MeasureString(bubble.Label,bubbleFont).Width+16); h=21;
                x=Math.Min(254,Width-10-w/2); y=47-5*rise;
            }
            float radius=bubble.IsWord?h/2:4;
            int a=(int)(255*alpha);
            GraphicsState state=g.Save();
            g.TranslateTransform(x,y);
            if(!bubble.IsWord) g.RotateTransform((bubble.Lane-1)*4);
            float zoom=.83f+.17f*Motion.Smooth(p/.1f); g.ScaleTransform(zoom,zoom);
            using (GraphicsPath basePath=RoundRect(-w/2,-h/2+1.6f,w,h,radius))
                Fill(g,basePath,Color.FromArgb(a,94,86,88));
            using (GraphicsPath face=RoundRect(-w/2,-h/2,w,h,radius))
            {
                Fill(g,face,bubble.IsWord?Color.FromArgb(a,255,224,155):Color.FromArgb(a,255,248,228));
                Stroke(g,face,Color.FromArgb(a,79,72,78),1.25f);
            }
            using (SolidBrush ink=new SolidBrush(Color.FromArgb(a,55,51,61)))
                g.DrawString(bubble.Label,bubbleFont,ink,new PointF(0,-.25f),center);
            g.Restore(state);
        }
    }
}
