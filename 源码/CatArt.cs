using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace KeyboardCat
{
    internal sealed partial class Painter : IDisposable
    {
        public const int Width = 320, Height = 260;
        Bitmap props;
        float propScale;
        int propKeyboard,propMouse;
        readonly Font legendFont = new Font("Segoe UI", 4.5f, FontStyle.Bold, GraphicsUnit.Pixel);
        readonly Font bubbleFont = new Font("Segoe UI", 12.5f, FontStyle.Bold, GraphicsUnit.Pixel);
        readonly StringFormat center = new StringFormat { Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

        static Color C(int hex) { return Cat.Rgb(hex); }
        static Color Tint(Color a, Color b, float t)
        {
            return Color.FromArgb((int)Motion.Mix(a.R,b.R,t),(int)Motion.Mix(a.G,b.G,t),(int)Motion.Mix(a.B,b.B,t));
        }
        static GraphicsPath Shape(float x, float y, params float[] curves)
        {
            GraphicsPath p = new GraphicsPath();
            for (int i = 0; i < curves.Length; i += 6)
            {
                p.AddBezier(x,y,curves[i],curves[i+1],curves[i+2],curves[i+3],curves[i+4],curves[i+5]);
                x = curves[i+4]; y = curves[i+5];
            }
            p.CloseFigure();
            return p;
        }
        static void Fill(Graphics g, GraphicsPath path, Color color)
        { using (SolidBrush b = new SolidBrush(color)) g.FillPath(b,path); }
        static void Stroke(Graphics g, GraphicsPath path, Color color, float width)
        {
            using (Pen p = new Pen(color, width))
            {
                p.LineJoin = LineJoin.Round; p.StartCap = p.EndCap = LineCap.Round;
                g.DrawPath(p,path);
            }
        }
        static void Oval(Graphics g, Color color, float x, float y, float w, float h)
        { using (SolidBrush b = new SolidBrush(color)) g.FillEllipse(b,x,y,w,h); }
        static void Curve(Graphics g, Color color, float width, float x1,float y1,float x2,float y2,float x3,float y3,float x4,float y4)
        {
            using (Pen p = new Pen(color,width))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                g.DrawBezier(p,x1,y1,x2,y2,x3,y3,x4,y4);
            }
        }
        static void Patch(Graphics g, Color color, float x, float y, params float[] knots)
        { using (GraphicsPath p = Shape(x,y,knots)) Fill(g,p,color); }
        static void Edge(Graphics g, GraphicsPath p, Cat cat)
        {
            GraphicsState outside = g.Save(); g.SetClip(p,CombineMode.Exclude);
            Stroke(g,p,Color.FromArgb(125,255,239,223),3.6f); g.Restore(outside);
            Stroke(g,p,cat.Ink,cat.Longhair ? 1.58f : 1.75f);
        }
        static void Setup(Graphics g)
        {
            g.PageUnit = GraphicsUnit.Pixel;
            g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.CompositingMode = CompositingMode.SourceOver;
        }

        static void Gradient(Graphics g, GraphicsPath path, PointF start, PointF end, params Color[] colors)
        {
            using (LinearGradientBrush brush = new LinearGradientBrush(start,end,colors[0],colors[colors.Length-1]))
            {
                float[] stops = new float[colors.Length];
                for (int i = 0; i < stops.Length; i++) stops[i] = (float)i / (stops.Length-1);
                brush.InterpolationColors = new ColorBlend { Colors = colors, Positions = stops };
                brush.WrapMode = WrapMode.TileFlipXY;
                g.FillPath(brush,path);
            }
        }

        static void SoftOval(Graphics g, Color color, float x, float y, float width, float height)
        {
            using (GraphicsPath oval = new GraphicsPath())
            {
                oval.AddEllipse(x,y,width,height);
                using (PathGradientBrush brush = new PathGradientBrush(oval))
                {
                    brush.CenterColor = color;
                    brush.SurroundColors = new Color[] { Color.FromArgb(0,color) };
                    g.FillPath(brush,oval);
                }
            }
        }

        static GraphicsState FigureSpace(Graphics g, Cat cat)
        {
            GraphicsState saved = g.Save();
            g.TranslateTransform(Cat.SeatX,Cat.SeatY);
            g.ScaleTransform(cat.WidthScale,cat.HeightScale);
            g.TranslateTransform(-Cat.SeatX,-Cat.SeatY);
            return saved;
        }

        public void Render(Graphics g, float scale, Cat cat, Motion motion, double now, Outfit outfit = default(Outfit), int keyboard = 0, int mouseStyle = 0, bool printLook = false)
        {
            GraphicsState saved = g.Save();
            g.ResetTransform();
            Setup(g);
            g.Clear(Color.Transparent);
            EnsureProps(scale,keyboard,mouseStyle);
            g.ScaleTransform(scale,scale);
            float breath = (float)Math.Sin(now * 2.05) * (cat.Longhair ? 1.0f : .62f);
            bool activityOnly=motion.IsTypingActivity(now);
            float l=activityOnly?motion.TypingReach(now,true):motion.Left.Reach(now);
            float r=activityOnly?motion.TypingReach(now,false):motion.Right.Reach(now);
            float cover=motion.Cover(now),temper=Math.Max(motion.Left.Temper(now),motion.Right.Temper(now));
            float typing = Math.Max(l,r);
            float lookX = motion.LookX * (1 - typing * .5f);
            float lookY = Motion.Mix(motion.LookY,.83f,typing * .68f);
            if(cover>0) {lookX*=1-cover;lookY*=1-cover;}

            float click = (float)Math.Max(0,1 - (now - motion.LastClick) / .2);
            float push = Math.Max(click,motion.MouseHeld ? .8f : 0);
            PointF mouse = cat.ToFigure(MousePawTarget(motion.MouseSide,push));
            PointF left = Motion.Mix(new PointF(132,169 + breath),mouse,motion.MouseReach);
            PointF right = new PointF(176,171 + breath);
            if(activityOnly) {
                left=Motion.Mix(left,cat.ToFigure(Keyboard.Project(10.1f,2.65f)),l);
                right=Motion.Mix(right,cat.ToFigure(Keyboard.Project(5.2f,2.65f)),r);
            } else {
                if(motion.Left.Key!=null) left=Motion.Mix(left,cat.ToFigure(motion.Left.Key.Cap.Center),l);
                if(motion.Right.Key!=null) right=Motion.Mix(right,cat.ToFigure(motion.Right.Key.Cap.Center),r);
            }
            TrickPose trick=TrickPose.Sample(motion.Tricks,cat,now,breath,ref left,ref right);
            CatGesture gesture=CatGesture.Sample(cat,now,l,r,temper,cover,trick);
            lookX=Motion.Mix(lookX,trick.GazeX,trick.Focus);lookY=Motion.Mix(lookY,trick.GazeY,trick.Focus);
            GarmentFit fit=trick.FitArms(cat,outfit.Clothes,breath,ref left,ref right);
            GraphicsState figure=FigureSpace(g,cat);
            trick.MoveBody(g);
            using (Matrix torso=TorsoMatrix())
            {
                GraphicsState tail=g.Save();
                TurnTail(g,gesture.TailTurn);g.MultiplyTransform(torso);
                DrawTail(g,cat,gesture.TailBend);
                g.Restore(tail);

                GraphicsState fur=g.Save();
                if (fit.Kind!=Garment.Plain) fit.ClipUnderlayer(g);
                g.MultiplyTransform(torso);
                DrawBody(g,cat,breath);
                if (cat.Longhair) DrawRuff(g,cat,breath);
                g.Restore(fur);
            }
            DrawGarment(g,fit);
            g.Restore(figure);

            GraphicsState screen = g.Save();
            g.ResetTransform();
            g.DrawImageUnscaled(props,0,0);
            g.Restore(screen);
            float leftPress=Motion.Smooth((l-.58f)/.42f),rightPress=Motion.Smooth((r-.58f)/.42f);
            if(motion.Left.Key!=null && leftPress>0) DrawCap(g,motion.Left.Key.Cap,leftPress,keyboard);
            if(motion.Right.Key!=null && rightPress>0) DrawCap(g,motion.Right.Key.Cap,rightPress,keyboard);
            DrawMouseFeedback(g,motion.MouseSide,(float)((now-motion.LastClick)/.38));

            figure = FigureSpace(g,cat);
            trick.MoveBody(g);
            if(cover<.01f) {
                if(!trick.InFront(1)) DrawArm(g,cat,true,left,fit,trick.LeftTurn,trick.LeftPads);
                if(!trick.InFront(2)) DrawArm(g,cat,false,right,fit,trick.RightTurn,trick.RightPads);
            }
            if (trick.Kind==TrickKind.Love)
                Heart(g,152,159+trick.Beat,1.03f+.07f*trick.Beat,0,trick.Weight);

            GraphicsState head=g.Save();
            float lean = cat.Longhair ? 3.3f : 1.75f;
            g.TranslateTransform(Cat.HeadX + lookX*lean + trick.HeadX,Cat.HeadY + Cat.HeadDrop + breath + lookY*1.7f + typing + trick.HeadY);
            g.RotateTransform(lookX*lean+trick.HeadTurn);
            g.ScaleTransform(Cat.HeadWidth,Cat.HeadHeight);
            g.TranslateTransform(-Cat.HeadX,-Cat.HeadY);
            float smile=trick.Smile;
            float blink=Motion.Mix(Motion.Blink(now),1,trick.Still);
            DrawHead(g,cat,lookX,lookY,blink*(1-trick.Squint)*(1-cover)*(1-.27f*temper),smile,gesture.EarLeft,gesture.EarRight);
            DrawTemper(g,cat,temper);
            DrawHeadwear(g,cat,outfit.Hat);
            g.Restore(head);
            if(cover>=.01f) DrawPrivacyArms(g,cat,outfit.Clothes,cover);
            ActionForeground(g,cat,trick,left,right,fit);
            g.Restore(figure);
            if(printLook && trick.Kind==TrickKind.Fish) CoverArt.FishGlints(g,cat.Longhair,trick.Seconds);
            foreach (KeyBubble bubble in motion.Bubbles) DrawBubble(g,bubble,now);
            if(motion.MatchedWord.HasValue) DrawBubble(g,motion.MatchedWord.Value,now);
            g.Restore(saved);
        }

        static void DrawTail(Graphics g, Cat cat, float swish)
        {
            if (cat.Longhair)
            {
                using (GraphicsPath plume = Shape(188,164,
                    209,174,220,161,234,151, 246,141,261,133+swish,275,133+swish,
                    284,132+swish,290,134+swish,295,138+swish, 292,137+swish,288,137+swish,285,138+swish,
                    294,137+swish,302,142+swish,303,148+swish, 304,155+swish,298,163+swish,288,169+swish,
                    290,166+swish,291,163+swish,291,161+swish, 282,174+swish,274,181,260,186,
                    262,183,264,181,264,179, 249,192,232,197,216,192, 204,190,194,184,187,180,
                    185,175,185,170,188,164))
                {
                    Gradient(g,plume,new PointF(240,132),new PointF(245,195),
                        cat.Cream,cat.Fur,cat.Shade);
                    TailMarkings(g,cat,plume,swish);
                    Edge(g,plume,cat);
                }
                Color thread = Tint(cat.Shade,cat.Mark,.25f);
                Curve(g,thread,1.2f,219,183,239,182,249,152,272,146+swish);
                Curve(g,cat.Cream,1.8f,225,177,245,168,246,151,268,142+swish);
                Curve(g,thread,.85f,237,185,260,179,266,157+swish,286,151+swish);
                Curve(g,cat.Cream,1.35f,254,178,271,170,274,159+swish,293,152+swish);
                return;
            }

            float w = swish * .55f;
            using (GraphicsPath tail = Shape(191,166,
                212,174,229,178,242,171, 254,166,260,153+w,272,150+w,
                283,146+w,290,149+w,290,155+w, 290,161+w,282,165+w,276,170+w,
                262,184,254,190,237,192, 217,194,201,189,189,182,
                186,177,185,170,191,166))
            {
                Gradient(g,tail,new PointF(204,174),new PointF(288,163),
                    cat.Fur,cat.Shade,C(0xa5a7a6),C(0x636465));
                GraphicsState clip = g.Save();
                g.SetClip(tail,CombineMode.Intersect);
                Curve(g,Color.FromArgb(46,116,119,119),3.8f,243,161,247,166,254,174,256,182);
                Curve(g,Color.FromArgb(48,108,110,110),3.5f,256,154+w,261,158+w,267,166+w,268,174+w);
                g.Restore(clip);
                TailMarkings(g,cat,tail,swish);
                Edge(g,tail,cat);
            }
            Curve(g,Color.FromArgb(142,228,228,220),1.1f,225,181,237,181,244,177,250,172);
        }

        static GraphicsPath BodyOutline(Cat cat, float breath)
        {
            if (cat.Longhair)
                return Shape(126,93+breath,
                    146,89,166,90,182,99, 198,108,210,125,217,144,
                    214,142,211,140,209,140, 216,151,221,160,222,171,
                    219,168,216,166,215,166, 221,182,219,190,212,194,
                    209,202,194,205,186,198, 171,203,144,202,127,198,
                    118,205,98,202,98,192, 94,188,93,182,95,176,
                    93,177,91,179,90,181, 88,165,94,152,99,143,
                    97,144,96,145,92,147, 96,129,107,108,126,93+breath);
            return Shape(127,104+breath,
                    142,100,165,99,180,105, 198,115,210,135,216,159,
                    222,178,219,188,212,193, 210,203,195,204,187,198,
                    169,202,143,202,128,198, 119,206,101,201,102,191,
                    95,182,98,165,103,148, 109,126,116,111,127,104+breath);
        }

        static void DrawBody(Graphics g, Cat cat, float breath)
        {
            using (GraphicsPath shape = BodyOutline(cat,breath))
            {
                Gradient(g,shape,new PointF(106,144),new PointF(218,171),
                    cat.Fur,cat.Cream,cat.Fur,cat.Shade);
                BodyMarkings(g,cat,shape);
                GraphicsState clip = g.Save();
                g.SetClip(shape,CombineMode.Intersect);
                SoftOval(g,Color.FromArgb(cat.Longhair ? 84 : 122,cat.Shade),160,113,85,96);
                if (!cat.Longhair)
                {
                    SoftOval(g,Color.FromArgb(125,cat.Cream),111,111,75,92);
                    Curve(g,Color.FromArgb(100,cat.Shade),1.1f,194,131,200,137,202,144,203,151);
                    Curve(g,Color.FromArgb(140,cat.Cream),1,193,134,197,140,199,147,200,152);
                }
                g.Restore(clip);
                Edge(g,shape,cat);
            }
            Color crease = Tint(cat.Shade,cat.Ink,.32f);
            Curve(g,crease,1.1f,114,172,105,179,104,190,111,194);
            Curve(g,crease,1.3f,201,166,187,172,181,187,188,194);
            Curve(g,crease,1,111,194,111,191,113,190,114,191);
            Curve(g,crease,1,117,196,117,193,119,192,120,193);
            Curve(g,crease,1,195,195,195,192,197,191,198,192);
            Curve(g,crease,1,201,195,201,192,203,191,204,192);
        }

        static void DrawRuff(Graphics g, Cat cat, float breath)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(0,breath * .6f);
            using (GraphicsPath ruff = Shape(109,97,
                129,90,165,90,185,100, 186,112,197,124,202,133,
                197,132,193,129,190,128, 190,137,195,143,198,147,
                189,144,184,140,179,136, 179,147,182,153,183,157,
                176,155,171,151,167,146, 166,156,160,164,154,171,
                154,166,152,160,149,157, 144,164,138,168,133,170,
                135,162,134,155,132,150, 126,155,121,157,118,159,
                120,151,120,145,119,141, 113,145,107,147,103,148,
                107,141,110,134,110,129, 106,131,102,133,98,134,
                102,124,109,113,109,97))
            {
                Gradient(g,ruff,new PointF(142,105),new PointF(159,168),
                    cat.Fur,cat.Cream,Tint(cat.Shade,cat.Cream,.37f));
            }
            Color lockShade = Tint(cat.Shade,cat.Fur,.30f);
            Curve(g,lockShade,1.1f,115,119,117,131,112,137,109,141);
            Curve(g,lockShade,1.25f,131,124,133,137,130,146,126,151);
            Curve(g,cat.Cream,1.5f,144,126,147,138,144,150,140,156);
            Curve(g,lockShade,1.05f,155,130,159,140,157,151,154,157);
            Curve(g,lockShade,.95f,176,121,175,132,180,142,185,146);
            g.Restore(state);
        }

        static void DrawGrippingTip(Graphics g,Cat cat,bool left,PointF paw,GarmentFit fit,float turn,float amount)
        {
            // Only the fingertips curl in front of the treat and chin. Their sleeves stay behind the head.
            Foreleg leg=new Foreleg(cat,left,paw,fit);
            PointF tip=TrickPose.TurnPaw(leg.Tip,leg.Shoulder,turn);
            float radius=11.5f*Motion.Smooth((amount-.1f)/.72f);
            using (GraphicsPath tips=new GraphicsPath())
            {
                tips.AddEllipse(tip.X-radius,tip.Y-radius*.75f,radius*2,radius*1.70f);
                GraphicsState state=g.Save();
                g.SetClip(tips,CombineMode.Intersect);
                DrawArm(g,cat,left,paw,fit,turn,0);
                g.Restore(state);
            }
        }

        static void DrawArm(Graphics g, Cat cat, bool left, PointF paw, GarmentFit fit,float turn,float pads)
        {
            Foreleg leg=new Foreleg(cat,left,paw,fit);
            GraphicsState rotated=null;
            if (turn!=0)
            {
                rotated=g.Save();
                g.TranslateTransform(leg.Shoulder.X,leg.Shoulder.Y);
                g.RotateTransform(turn);
                g.TranslateTransform(-leg.Shoulder.X,-leg.Shoulder.Y);
            }
            using (GraphicsPath arm=leg.Outline())
            {
                Gradient(g,arm,leg.Shoulder,leg.Tip,cat.Fur,cat.Cream);
                GraphicsState detail = g.Save();
                g.SetClip(arm,CombineMode.Intersect);
                g.TranslateTransform(leg.Tip.X,leg.Tip.Y);
                g.RotateTransform((float)(Math.Atan2(leg.Direction.Y,leg.Direction.X)*180/Math.PI)-90);
                Color toe = Faded(Tint(cat.Ink,cat.Fur,.47f),1-pads);
                if (cat.Longhair)
                {
                    Curve(g,cat.Shade,.8f,-5,-19,-7,-15,-6,-11,-5,-9);
                    Curve(g,cat.Shade,.75f,5,-12,3,-10,5,-7,4,-5);
                }
                Curve(g,toe,.9f,-3.6f,5.5f,-4,3.8f,-3.6f,2,-3,1.3f);
                Curve(g,toe,.9f,1.7f,5.8f,1.3f,3.9f,1.7f,2.9f,2.3f,2.2f);
                if (pads>0) PawPads(g,pads);
                g.Restore(detail);
                Stroke(g,arm,cat.Ink,1.55f);
                DrawSleeve(g,cat,fit.Kind,leg,arm);
            }
            if (rotated!=null) g.Restore(rotated);
        }

        public void Dispose()
        {
            if (props != null) props.Dispose();
            legendFont.Dispose(); bubbleFont.Dispose(); center.Dispose();
        }
    }
}
