using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KeyboardCat
{
    internal sealed partial class Painter
    {
        partial struct TrickPose
        {
            public float Smile {get {return Kind==TrickKind.Fish || Kind==TrickKind.RiceBowl || Kind==TrickKind.Love || Kind==TrickKind.Toast || Kind==TrickKind.Groom ? Weight : 0;}}

            public bool InFront(int hand) {return Weight>.01f && (FrontHand==hand || FrontHand==3);}
            public PointF ArmOffset(bool left) {return new PointF((left?-6:6)*Weight,18*Weight);}

            void AimFront(GarmentFit fit,bool left,ref PointF paw,ref float turn)
            {
                PointF shoulder=fit.Shoulder(left),offset=ArmOffset(left);
                float x=paw.X-offset.X-shoulder.X,y=paw.Y-offset.Y-shoulder.Y;
                turn=(float)(Math.Atan2(y,x)*180/Math.PI)-90;
                paw=new PointF(shoulder.X,shoulder.Y+(float)Math.Sqrt(x*x+y*y));
            }
            public GarmentFit FitArms(Cat cat,Garment clothes,float breath,ref PointF left,ref PointF right)
            {
                GarmentFit fit=new GarmentFit(cat,clothes,breath,left,right);
                if(FrontHand!=0) {
                    if(InFront(1)) AimFront(fit,true,ref left,ref LeftTurn);
                    if(InFront(2)) AimFront(fit,false,ref right,ref RightTurn);
                } else if(LeftTurn!=0 || RightTurn!=0) {
                    fit=new GarmentFit(cat,clothes,breath,TurnPaw(left,fit.Shoulder(true),LeftTurn),
                        TurnPaw(right,fit.Shoulder(false),RightTurn));
                }
                return fit;
            }

            public void CompanionAction(Cat cat,float breath,ref PointF left,ref PointF right)
            {
                float t=Seconds,w=Weight,side=cat.Longhair?-1:1;
                float beat=(float)Math.Sin(t*Math.PI*2);
                PointF target;
                switch(Kind) {
                    case TrickKind.Fortune:
                        float beckon=Accent(t,.83f,.23f,.26f)+Accent(t,1.58f,.27f,.29f);
                        float reel=Ease(t,.32f,.84f)*(1-Ease(t,1.02f,1.25f))+Ease(t,1.18f,1.61f)*(1-Ease(t,1.85f,2.20f));
                        target=new PointF(151+side*(71-21*beckon),108+23*beckon);
                        FrontHand=side<0?1:2;
                        if(side<0) {left=target;LeftPads=w*(1-beckon);} else {right=target;RightPads=w*(1-beckon);}
                        Prop=new PointF(151+side*(96-38*reel),134-26*(float)Math.Sin(Math.PI*reel)-5*reel);
                        Beat=reel;BodyX=side*(5-9*beckon)*w;HeadTurn=side*(4-9*beckon)*w;
                        Focus=.74f*w;GazeX=side*(1-.8f*reel);GazeY=.22f;Squint=.39f*beckon*w;
                        TailTurn=(-10-12*beckon)*w;EarLeft=-.52f;EarRight=.56f*beckon;
                        break;
                    case TrickKind.Toast:
                        float lift=Ease(t,.12f,.72f)*(1-Ease(t,2.30f,2.90f));
                        float clink=Accent(t,1.02f,.25f,.32f),sip=Accent(t,1.94f,.40f,.37f);
                        Prop=new PointF(151+side*(51+12*clink-28*sip),168-34*lift-6*clink);
                        target=new PointF(Prop.X+side*12,Prop.Y+3);FrontHand=side<0?1:2;
                        if(side<0) left=target;else right=target;
                        CupAngle=side*(7*clink-25*sip);BodyTurn=side*(3*clink-3*sip)*w;
                        HeadTurn=side*(4*lift+5*sip)*w;HeadY=(2*lift+6*sip)*w;HeadX=side*6*sip*w;
                        Squint=(.15f*lift+.81f*sip)*w;Focus=.78f*w;GazeX=side*(.88f-.56f*sip);
                        EarLeft=-.32f*lift;EarRight=-.46f*sip;TailTurn=(-7-5*clink)*w;
                        break;
                    case TrickKind.Basketball:
                        float cross=Ease(t,.98f,1.55f),gather=Ease(t,1.50f,1.84f);
                        float ballSide=side*(1-2*cross);
                        float bounce=(float)Math.Abs(Math.Cos(t*Math.PI/.48f));
                        float shoulder=Accent(t,1.93f,.18f,.17f)-Accent(t,2.24f,.18f,.18f)+
                            Accent(t,2.57f,.20f,.16f)-.7f*Accent(t,2.88f,.17f,.18f);
                        float finish=Ease(t,2.97f,3.37f);
                        Prop=new PointF(151+ballSide*(53-16*gather-8*finish),187-31*bounce*(1-gather)-32*gather-8*finish);
                        target=new PointF(Prop.X-ballSide*5,Math.Min(170,Prop.Y-11)+12*gather);
                        FrontHand=ballSide<0?1:2;
                        PointF free=new PointF(151-ballSide*(34+17*shoulder),154-22*shoulder-14*finish);
                        if(FrontHand==1){left=target;right=free;}else{right=target;left=free;}
                        if(gather>.99f) FrontHand=3;
                        Legs=w;LeftStep=.88f*Math.Max(0,shoulder)*w;RightStep=.88f*Math.Max(0,-shoulder)*w;
                        BodyX=(ballSide*(7+7*gather)+side*14*shoulder)*w;
                        BodyY=(3*(1-bounce)*(1-gather)-3*Math.Abs(shoulder))*w;
                        BodyTurn=(ballSide*(4+3*gather)+side*14*shoulder)*w;Wide=-(.12f+.08f*Math.Abs(shoulder))*gather*w;Tall=-.035f*Math.Abs(shoulder)*w;
                        HeadTurn=(-BodyTurn*.61f-side*3*shoulder)*w;HeadX=-ballSide*3*w;
                        HeadY=(4*(1-bounce)*(1-gather)+3*Math.Abs(shoulder))*w;
                        EarLeft=.59f*shoulder;EarRight=-.71f*shoulder;
                        TailTurn=(ballSide*12-side*11*shoulder)*w;Tail=7*shoulder*w;
                        Focus=.82f*w;GazeX=ballSide*(1-.8f*gather);GazeY=.78f*(1-gather)-.2f*finish;
                        break;
                    case TrickKind.Freeze:
                        float lockIn=1-Motion.Smooth((t-1.85f)/.50f);
                        float shake=Accent(t,2.17f,.25f,.42f)*(float)Math.Sin((t-1.85f)*25);
                        float snap=Accent(t,.30f,.21f,.17f);BodyTurn=(-6*snap+2*shake)*w;
                        left=new PointF(122,183);right=new PointF(182,187);
                        LeftTurn=(75+4*shake)*w;RightTurn=(-84+4*shake)*w;LeftPads=RightPads=w;
                        Still=w*lockIn;Focus=w;GazeY=-.45f;
                        BodyY=-3*w;HeadY=(cat.Longhair?6:-6)*w-breath*Still;
                        HeadX=2.4f*shake*w;EarLeft=-.95f;EarRight=-.82f;
                        TailTurn=-24*w;Tail=2*shake*w;
                        break;
                    case TrickKind.Groom:
                        float stroke=Motion.Smooth((t-.55f)/.36f)*(.5f-.5f*(float)Math.Cos((t-.55f)*6.4));
                        target=new PointF(151+side*(11+31*stroke),120-30*stroke);
                        FrontHand=side<0?1:2;
                        if(side<0) left=target;else right=target;
                        HeadTurn=side*(3+4*stroke)*w;HeadY=(2+3*stroke)*w;Squint=.91f*w;
                        EarLeft=side<0?.82f*stroke:.1f;EarRight=side>0?.82f*stroke:.1f;
                        TailTurn=(2-4*stroke)*w;
                        break;
                    case TrickKind.ChaseTail:
                        float pounce=Accent(t,1.12f,.34f,.36f)+.75f*Accent(t,2.04f,.30f,.35f);
                        float crouch=Accent(t,.62f,.36f,.22f);BodyX=(12*pounce-5*crouch)*w;BodyY=(6*crouch-7*pounce)*w;
                        right=new PointF(196+51*pounce,165-37*pounce);FrontHand=2;
                        BodyTurn=(3+3*pounce)*w;HeadTurn=(9+5*pounce)*w;HeadX=5*w;
                        Focus=w;GazeX=1;GazeY=-.15f;RightPads=.65f*pounce*w;
                        EarLeft=-.5f;EarRight=.72f;TailTurn=(-15-24*pounce)*w;Tail=8*beat*w;
                        break;
                    case TrickKind.Listen:
                        float turn=Motion.Smooth(t/.55f)-1.7f*Motion.Smooth((t-1.2f)/.55f);
                        HeadTurn=side*9*turn*w;HeadX=side*3*turn*w;HeadY=cat.Longhair?9*w:0;
                        Focus=w;GazeX=side*turn;GazeY=-.65f;
                        EarLeft=-side*.88f*turn;EarRight=-EarLeft;
                        TailTurn=(-8+4*turn)*w;
                        break;
                }
            }
        }

        struct CatGesture
        {
            public float EarLeft,EarRight,TailBend,TailTurn;
            static float Flick(double time,double period,double offset)
            {
                double phase=(time+offset)%period;
                if(phase>=.62) return 0;
                double beat=Math.Sin(phase*Math.PI/.62);
                return (float)(beat*beat*Math.Sin(phase*Math.PI*3/.62));
            }
            public static CatGesture Sample(Cat cat,double t,float l,float r,float temper,float cover,TrickPose action)
            {
                double phase=t+(cat.Longhair?2.3:0);
                float left=.77f*Flick(phase,9.2,2.1)-.45f*l+.72f*temper+.55f*cover;
                float right=.77f*Flick(phase,11.4,6.3)-.45f*r+.72f*temper+.55f*cover;
                float bend=(float)(3.3*Math.Sin(phase*1.8)+1.3*Math.Sin(phase*3.1)+temper*5*Math.Sin(t*12));
                float turn=(float)(3.2*Math.Sin(phase*1.35)-3.5*Math.Max(l,r)+temper*5*Math.Sin(t*9));
                return new CatGesture {
                    EarLeft=Motion.Limit(Motion.Mix(left,action.EarLeft,action.Weight),-1,1),
                    EarRight=Motion.Limit(Motion.Mix(right,action.EarRight,action.Weight),-1,1),
                    TailBend=bend*(1-action.Still)*(1-.90f*cover)+action.Tail,
                    TailTurn=Motion.Mix(turn,8,cover)*(1-action.Weight)+action.TailTurn
                };
            }
        }

        static void TurnTail(Graphics g,float angle)
        {
            g.TranslateTransform(190,180);g.RotateTransform(angle);g.TranslateTransform(-190,-180);
        }

        static void DrawMemeProp(Graphics g,Cat cat,TrickPose p)
        {
            if(p.Weight<.01f) return;
            float w=p.Weight,side=cat.Longhair?-1:1;
            if(p.Kind==TrickKind.Fortune) {
                float u=(p.Seconds*1.13f)%1;
                float x=p.Prop.X,y=p.Prop.Y;
                float alpha=w*(.30f+.70f*(float)Math.Sin(Math.PI*u));
                Color edge=Faded(C(0x98643e),alpha),gold=Faded(C(0xf2bf64),alpha);
                using(GraphicsPath coin=new GraphicsPath()) {
                    coin.AddEllipse(x-7,y-7,14,14);Fill(g,coin,gold);Stroke(g,coin,edge,1.25f);
                }
                using(GraphicsPath hole=RoundRect(x-2,y-2,4,4,.5f)) Fill(g,hole,edge);
                Curve(g,Faded(C(0xffe9a4),alpha),1.1f,x-4,y-2,x-3,y-4,x-1,y-5,x+1,y-5);
            } else if(p.Kind==TrickKind.Toast) {
                GraphicsState saved=g.Save();g.TranslateTransform(p.Prop.X,p.Prop.Y);g.RotateTransform(p.CupAngle);g.ScaleTransform(side*1.24f,1.24f);
                Color rim=Faded(C(0x536e6a),w),pale=Faded(C(0xbddccb),w);
                using(GraphicsPath handle=new GraphicsPath()) {handle.AddEllipse(7,-4,11,10);Stroke(g,handle,rim,3.6f);Stroke(g,handle,pale,1.7f);}
                using(GraphicsPath cup=Shape(-11,-6,-11,3,-9,9,-5,10,-1,12,5,12,8,8,10,5,11,-2,11,-6,6,-9,-7,-9,-11,-6)) {
                    Gradient(g,cup,new PointF(-9,-5),new PointF(8,10),Faded(C(0xf1f3da),w),pale);Stroke(g,cup,rim,1.15f);
                }
                Oval(g,Faded(C(0xb57e55),w),-9.5f,-8,19,4);
                float steam=(float)Math.Sin(p.Seconds*4)*1.4f;
                Curve(g,Faded(C(0x899c94),w*.8f),1.1f,-2,-11,-7+steam,-15,2+steam,-17,-1,-21);
                g.Restore(saved);
            } else if(p.Kind==TrickKind.Basketball) {
                GraphicsState ball=g.Save();
                g.TranslateTransform(p.Prop.X,p.Prop.Y);g.RotateTransform(27*(float)Math.Sin(p.Seconds*6));
                Color seam=Faded(C(0x794632),w);
                using(GraphicsPath circle=new GraphicsPath()) {
                    circle.AddEllipse(-12,-12,24,24);
                    Gradient(g,circle,new PointF(-7,-10),new PointF(8,12),Faded(C(0xffbf73),w),Faded(C(0xda7649),w));
                    Stroke(g,circle,seam,1.35f);
                }
                Curve(g,seam,1.1f,-11,-4,-5,-1,5,1,11,4);
                Curve(g,seam,1.1f,-4,11,-1,5,1,-5,4,-11);
                Curve(g,seam,1.0f,-9,7,-3,8,4,5,9,-7);
                Curve(g,seam,1.0f,-8,-8,-6,-2,-1,3,8,8);
                g.Restore(ball);
            } else if(p.Kind==TrickKind.Freeze) {
                float x=151+side*82,y=69;
                Color pause=Faded(C(0x6895a7),p.Still*.9f);
                using(GraphicsPath a=RoundRect(x-4,y,2.7f,7,1)) Fill(g,a,pause);
                using(GraphicsPath b=RoundRect(x+2,y,2.7f,7,1)) Fill(g,b,pause);
            }
        }

        static void ActionForeground(Graphics g,Cat cat,TrickPose p,PointF left,PointF right,GarmentFit fit)
        {
            if(p.Kind==TrickKind.None) return;
            bool leadLeft=cat.Longhair;
            PointF wave=TrickPose.TurnPaw(leadLeft?left:right,fit.Shoulder(leadLeft),leadLeft?p.LeftTurn:p.RightTurn);
            if(p.InFront(leadLeft?1:2)) {PointF offset=p.ArmOffset(leadLeft);wave=new PointF(wave.X+offset.X,wave.Y+offset.Y);}
            DrawTrickEffects(g,cat,p,wave);
            DrawMemeProp(g,cat,p);
            if(p.Kind==TrickKind.TripleTap) DrawTripleTap(g,cat,p);
            for(int i=0;i<2;i++) {
                if(!p.InFront(i+1)) continue;
                bool isLeft=i==0;PointF offset=p.ArmOffset(isLeft);
                PointF paw=isLeft?left:right;float turn=isLeft?p.LeftTurn:p.RightTurn;
                GraphicsState elbow=g.Save();g.TranslateTransform(offset.X,offset.Y);
                DrawArm(g,cat,isLeft,paw,fit,turn,isLeft?p.LeftPads:p.RightPads);
                if(p.Kind==TrickKind.TripleTap) ContactPaw(g,cat,TrickPose.TurnPaw(paw,fit.Shoulder(isLeft),turn),isLeft?p.PalmLeft:p.PalmRight);
                g.Restore(elbow);
            }
            if(p.Kind==TrickKind.SweetCone) DrawSweetCone(g,cat,p,wave);
            if(p.Kind==TrickKind.RiceBowl) DrawRiceBowl(g,p);
            if(p.Kind==TrickKind.Fish && p.Weight>.1f) {
                DrawGrippingTip(g,cat,true,left,fit,p.LeftTurn,p.Weight);
                DrawGrippingTip(g,cat,false,right,fit,p.RightTurn,p.Weight);
            }
        }
    }
}
