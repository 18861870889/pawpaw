using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KeyboardCat
{
    internal sealed partial class Painter
    {
        partial struct TrickPose
        {
            public TrickKind Kind;
            public float Weight, Seconds, Beat, BodyX, BodyY, BodyTurn, HeadX, HeadY, HeadTurn, Squint, Tail;
            public float LeftTurn, RightTurn, LeftPads, RightPads;
            public float EarLeft,EarRight,TailTurn,Still,Focus,GazeX,GazeY;
            public int FrontHand;
            public PointF Prop;
            public float Wide, Tall, Air, Legs, LeftStep, RightStep, PalmLeft, PalmRight, CupAngle;
            public PointF Spoon, SpoonGrip;

            public static TrickPose Sample(TrickPlayer player,Cat cat,double now,float breath,ref PointF left,ref PointF right)
            {
                TrickPose p=new TrickPose();
                WordTrick trick=player.Current(now);
                if (trick==null) return p;
                p.Kind=trick.Kind;
                float sec=(float)(now-player.Started);
                p.Seconds=sec;
                float remaining=(float)trick.Duration-sec;
                bool sleepy=trick.Kind==TrickKind.Sleep;
                float w=Motion.Smooth(Math.Min(sec/(sleepy?.46f:.26f),remaining/(sleepy?.55f:.36f)));
                p.Weight=w;
                int side=cat.Longhair?-1:1;
                PointF l=new PointF(132,169+breath), r=new PointF(176,171+breath);
                float beat;
                switch (p.Kind)
                {
                    case TrickKind.Wave:
                        float greeting=Ease(sec,.12f,.48f)*(1-Ease(sec,1.63f,2.06f));
                        beat=Accent(sec,.74f,.18f,.21f)+Accent(sec,1.35f,.20f,.23f);
                        if(cat.Longhair) {l=new PointF(123,191);p.LeftTurn=(81+42*beat)*w*greeting;p.LeftPads=greeting*w;}
                        else {r=new PointF(181,191);p.RightTurn=-(81+42*beat)*w*greeting;p.RightPads=greeting*w;}
                        float bow=Accent(sec,1.80f,.25f,.31f);
                        p.BodyX=-side*5*greeting*w;p.HeadY=(6*bow+(cat.Longhair?4:-2)*greeting)*w;
                        p.HeadTurn=-side*(5*greeting+3*beat)*w;p.Squint=.48f*bow*w;
                        p.EarLeft=-.55f*greeting;p.EarRight=.48f*beat;p.TailTurn=side*8*beat*w;
                        break;
                    case TrickKind.Love:
                        float gather=Ease(sec,.16f,.78f)*(1-Ease(sec,1.91f,2.35f));
                        beat=Accent(sec,1.05f,.14f,.24f)+.8f*Accent(sec,1.59f,.14f,.23f);
                        l=new PointF(112+10*gather,170);r=new PointF(193-12*gather,170);
                        p.LeftTurn=-(6+21*gather+4*beat)*w;p.RightTurn=(6+22*gather+4*beat)*w;
                        p.HeadTurn=side*(1.5f+5*gather)*w;p.HeadY=(5*gather-3*beat)*w;
                        p.BodyY=-3*beat*w;p.Wide=.025f*beat*w;p.Squint=(.75f*gather+.24f*beat)*w;
                        p.Beat=beat;p.EarLeft=-.35f*beat;p.EarRight=-.25f*gather;p.TailTurn=-11*gather*w;
                        break;
                    case TrickKind.Fish:
                        beat=Accent(sec,.87f,.26f,.24f)+Accent(sec,1.68f,.29f,.23f);
                        l=new PointF(117,156);r=new PointF(190,159);
                        p.LeftTurn=-(27+7*beat)*w;p.RightTurn=(30+8*beat)*w;
                        p.HeadY=(2+15*beat)*w;p.HeadX=side*3*beat*w;p.HeadTurn=side*(4-5*beat)*w;
                        p.Squint=.92f*beat*w;p.Beat=beat;p.Focus=.73f*w;p.GazeY=.76f;
                        p.TailTurn=side*(8*beat-3)*w;p.EarLeft=.44f*beat;p.EarRight=-.33f*beat;
                        break;
                    case TrickKind.Sleep:
                        float settle=Ease(sec,.34f,1.55f),nod=Accent(sec,.61f,.36f,.21f)+.68f*Accent(sec,1.15f,.28f,.22f);
                        beat=(float)Math.Sin(sec*2.8f);
                        l=new PointF(125,164);r=new PointF(182,164);
                        p.LeftTurn=-23*w;p.RightTurn=27*w;
                        p.BodyY=(2*settle+.75f*beat)*w;p.HeadY=(9*settle+6*nod+.9f*beat)*w;
                        p.HeadX=side*5*settle*w;p.HeadTurn=side*(13*settle+4*nod)*w;p.Squint=Math.Min(1,settle+.8f*nod)*w;
                        p.Tail=-2.4f*beat*w;p.EarLeft=.76f*settle;p.EarRight=.51f*settle;p.TailTurn=13*settle*w;
                        break;
                    case TrickKind.Dance: p.StepTouch(ref l,ref r);break;
                    case TrickKind.SweetCone: p.ConeDance(cat,ref l,ref r);break;
                    case TrickKind.RiceBowl: p.Dinner(cat,ref l,ref r);break;
                    case TrickKind.TripleTap: p.ScreenJump(cat,ref l,ref r);break;
                    default:
                        p.CompanionAction(cat,breath,ref l,ref r);break;
                }
                left=Motion.Mix(left,l,w); right=Motion.Mix(right,r,w);
                return p;
            }

            public void MoveBody(Graphics g)
            {
                if (BodyTurn==0 && BodyX==0 && BodyY==0 && Wide==0 && Tall==0) return;
                g.TranslateTransform(Cat.SeatX+BodyX,Cat.SeatY+BodyY);
                g.RotateTransform(BodyTurn);
                g.ScaleTransform(1+Wide,1+Tall);
                g.TranslateTransform(-Cat.SeatX,-Cat.SeatY);
            }

            public static PointF TurnPaw(PointF paw,PointF shoulder,float degrees)
            {
                if (degrees==0) return paw;
                double angle=degrees*Math.PI/180, c=Math.Cos(angle), s=Math.Sin(angle);
                float x=paw.X-shoulder.X, y=paw.Y-shoulder.Y;
                return new PointF(shoulder.X+(float)(x*c-y*s),shoulder.Y+(float)(x*s+y*c));
            }
        }

        static Color Faded(Color color,float opacity)
        { return Color.FromArgb((int)(255*Motion.Limit(opacity,0,1)),color); }

        static void PawPads(Graphics g,float opacity)
        {
            Color pink=Faded(C(0xe49a9f),opacity), light=Faded(C(0xffc4b5),opacity);
            using (GraphicsPath pad=Shape(0,-4.4f, -2.4f,-5.6f,-5.1f,-3.4f,-4.2f,-1.2f,
                -3.6f,1.7f,-1.8f,.9f,0,.35f, 1.6f,1,4.1f,1.6f,4.5f,-1.1f, 5.1f,-3.7f,2.2f,-5.5f,0,-4.4f))
                Fill(g,pad,pink);
            Oval(g,pink,-7.2f,1.15f,3.05f,3.65f); Oval(g,pink,-2.0f,3.45f,3.6f,4.0f);
            Oval(g,pink,3.6f,2.05f,3.25f,3.7f); Oval(g,light,-2.9f,-3.4f,2.05f,1.3f);
        }

        static void Heart(Graphics g,float x,float y,float scale,float turn,float amount)
        {
            if (amount<.015f || scale<=0) return;
            GraphicsState state=g.Save();
            g.TranslateTransform(x,y); g.RotateTransform(turn); g.ScaleTransform(scale,scale);
            using (GraphicsPath heart=Shape(0,11, -4.4f,7.7f,-13.3f,2.8f,-13.2f,-4.4f,
                -13.1f,-11.3f,-4.6f,-13,0,-7.2f, 4.8f,-13.3f,13.4f,-10.9f,13.3f,-4.3f,
                13.1f,2.4f,5.2f,7.6f,0,11))
            {
                Gradient(g,heart,new PointF(-5,-9),new PointF(3,10),Faded(C(0xffb6b1),amount),Faded(C(0xe77f8f),amount));
                Stroke(g,heart,Faded(C(0xa0566b),amount),1.22f);
            }
            Curve(g,Faded(C(0xffeee1),amount),1.4f,-9,-4.8f,-8.9f,-7,-7,-8.3f,-5.3f,-8.0f);
            g.Restore(state);
        }

        static void FishSnack(Graphics g,float x,float y,float turn,float amount)
        {
            if (amount<.015f) return;
            GraphicsState state=g.Save();
            g.TranslateTransform(x,y); g.RotateTransform(turn);
            float scale=.80f+.20f*amount; g.ScaleTransform(scale,scale);
            Color rim=Faded(C(0x956338),amount);
            using (GraphicsPath tail=Shape(-10,0, -16,-1.5f,-19,-6.2f,-24,-7.5f,
                -24,-3,-22,-1,-21,0, -22,2,-24,4.2f,-23,7.1f, -18,6.2f,-15,2.2f,-10,1))
            {
                Fill(g,tail,Faded(C(0xf1b15f),amount)); Stroke(g,tail,rim,1.2f);
            }
            using (GraphicsPath fish=Shape(-13,0, -8,-7,0,-10.6f,8,-8.3f,
                13,-7,17,-3.1f,18,0, 14,4.4f,9,7.6f,2.3f,8.2f, -5.3f,8.8f,-10.5f,4,-13,0))
            {
                Gradient(g,fish,new PointF(0,-8),new PointF(0,9),Faded(C(0xffdc88),amount),Faded(C(0xeea060),amount));
                Stroke(g,fish,rim,1.35f);
            }
            Curve(g,rim,1.0f,4.6f,-5.4f,2.2f,-2.5f,2.2f,2.4f,5.1f,5.0f);
            Curve(g,Faded(C(0xc38847),amount),.9f,-7,-2.3f,-4.8f,-.4f,-4.8f,.7f,-7,2.3f);
            Curve(g,Faded(C(0xc38847),amount),.9f,-1.7f,-2.7f,.3f,-1.1f,.1f,.5f,-1.7f,2);
            Oval(g,Faded(C(0x59494f),amount),9.0f,-3.8f,3.5f,3.5f);
            Oval(g,Faded(Color.White,amount),9.55f,-3.4f,1.1f,1.1f);
            g.Restore(state);
        }

        static void Note(Graphics g,float x,float y,float scale,float turn,Color fill,float amount)
        {
            GraphicsState saved=g.Save();
            g.TranslateTransform(x,y); g.RotateTransform(turn); g.ScaleTransform(scale,scale);
            Color ink=Faded(fill,amount);
            Oval(g,ink,-4.7f,2.5f,8.1f,5.9f);
            Curve(g,ink,1.9f,2.5f,5.4f,2.5f,1,2.7f,-4.4f,2.7f,-9);
            Curve(g,ink,2.3f,2.7f,-9,5,-7.5f,8.5f,-7.3f,8.1f,-3.6f);
            g.Restore(saved);
        }

        static void SleepMark(Graphics g,float x,float y,float scale,float amount)
        {
            GraphicsState saved=g.Save();
            g.TranslateTransform(x,y); g.RotateTransform(-9); g.ScaleTransform(scale,scale);
            using (GraphicsPath mark=new GraphicsPath())
            {
                mark.AddLines(new[]{new PointF(-3.7f,-4),new PointF(4,-4),new PointF(-3.7f,4),new PointF(4.8f,4)});
                Stroke(g,mark,Faded(C(0x738da3),amount),1.6f);
            }
            g.Restore(saved);
        }

        static void DrawTrickEffects(Graphics g,Cat cat,TrickPose p,PointF wavingPaw)
        {
            if (p.Weight<=.01f) return;
            int side=cat.Longhair?-1:1;
            switch (p.Kind)
            {
                case TrickKind.Wave:
                    float x=wavingPaw.X+side*16, y=wavingPaw.Y;
                    Color wave=Faded(C(0xcb9959),p.Weight*.83f);
                    Curve(g,wave,1.6f,x,y-9,x+side*4,y-7,x+side*5,y-3,x+side*4,y+.3f);
                    Curve(g,wave,1.3f,x+side*4,y-15,x+side*10,y-12,x+side*12,y-5,x+side*11,y-2);
                    break;
                case TrickKind.Love:
                    for (int i=0;i<3;i++)
                    {
                        float u=(p.Seconds*.56f+i/3f)%1, direction=i%2==0?side:-side;
                        float opacity=p.Weight*(float)Math.Sin(Math.PI*u);
                        Heart(g,152+direction*(74+16*u),118-47*u,.36f+.16f*u,direction*(-14+9*u),opacity);
                    }
                    break;
                case TrickKind.Fish:
                    FishSnack(g,151+2*p.Beat,151-7*p.Beat,-10+5*p.Beat,p.Weight);
                    for (int i=0;i<3;i++)
                    {
                        float u=(p.Seconds*1.12f+i*.31f)%1;
                        Color crumb=Faded(C(0xce9054),p.Weight*p.Beat*(float)Math.Sin(Math.PI*u)*.95f);
                        Oval(g,crumb,168+u*12,143-8*(float)Math.Sin(Math.PI*u)+7*u,2.2f-i*.30f,2.0f-i*.22f);
                    }
                    break;
                case TrickKind.Sleep:
                    for (int i=0;i<3;i++)
                    {
                        float u=(p.Seconds*.44f+i/3f)%1;
                        SleepMark(g,151+side*(84+23*u),85-48*u,.56f+.73f*u,
                            p.Weight*(float)Math.Sin(Math.PI*u));
                    }
                    break;
                case TrickKind.Dance:
                    for (int i=0;i<3;i++)
                    {
                        float u=(p.Seconds*.67f+i/3f)%1, direction=i%2==0?side:-side;
                        Note(g,152+direction*(81+13*u),113-64*u,.68f+.19f*u,direction*(7+13*u),
                            i%2==0?C(0x7095a3):C(0xc58793),p.Weight*(float)Math.Sin(Math.PI*u));
                    }
                    break;
            }
        }
    }
}
