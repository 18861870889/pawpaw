using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KeyboardCat
{
    internal sealed partial class Painter
    {
        partial struct TrickPose
        {
            public float Smile {get {return Kind==TrickKind.Fish || Kind==TrickKind.Love || Kind==TrickKind.Toast || Kind==TrickKind.Groom ? Weight : 0;}}

            public bool InFront(int hand) {return FrontHand==hand && Weight>.01f;}

            public GarmentFit FitArms(Cat cat,Garment clothes,float breath,ref PointF left,ref PointF right)
            {
                GarmentFit fit=new GarmentFit(cat,clothes,breath,left,right);
                if(FrontHand!=0) {
                    // Draft a hanging foreleg, then turn it at the shoulder towards the actual contact.
                    // The outline and cuff keep their winding even when a paw rises above the face.
                    bool useLeft=FrontHand==1;
                    PointF shoulder=fit.Shoulder(useLeft),target=useLeft?left:right;
                    FrontOffset=new PointF((useLeft?-6:6)*Weight,18*Weight);
                    float dx=target.X-FrontOffset.X-shoulder.X,dy=target.Y-FrontOffset.Y-shoulder.Y;
                    float degrees=(float)(Math.Atan2(dy,dx)*180/Math.PI)-90;
                    PointF hanging=new PointF(shoulder.X,shoulder.Y+(float)Math.Sqrt(dx*dx+dy*dy));
                    if(useLeft) {left=hanging;LeftTurn=degrees;}else {right=hanging;RightTurn=degrees;}
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
                        float beckon=.5f+.5f*(float)Math.Cos(t*8.4);
                        target=new PointF(151+side*(68-6*beckon),109+19*beckon);
                        FrontHand=side<0?1:2;
                        if(side<0) {left=target;LeftPads=w*(1-beckon);} else {right=target;RightPads=w*(1-beckon);}
                        HeadTurn=-side*4*w;HeadY=-2*w;Squint=.35f*w;
                        TailTurn=(-12-8*beckon)*w;Tail=5*beat*w;
                        EarLeft=-.45f;EarRight=.38f*beat;
                        break;
                    case TrickKind.Toast:
                        float lift=Motion.Smooth((t-.15f)/.8f)*(1-Motion.Smooth((t-2.2f)/.65f));
                        Prop=new PointF(151+side*(53-7*lift),169-26*lift);
                        target=new PointF(Prop.X-15*side,Prop.Y+4);
                        FrontHand=side<0?1:2;
                        if(side<0) left=target;else right=target;
                        HeadTurn=side*6*w;HeadY=4*lift*w;Squint=.97f*lift*w;
                        EarLeft=-.22f;EarRight=-.38f;TailTurn=(-6-4*lift)*w;
                        break;
                    case TrickKind.Basketball:
                        // Two dribbles, a crossover and shoulder accents, then catch the ball.
                        float cross=Motion.Smooth((t-1.12f)/.57f);
                        float catchBall=Motion.Smooth((t-2.94f)/.52f);
                        float bounce=(float)Math.Abs(Math.Cos(t*Math.PI/.55))*(1-catchBall);
                        float ballSide=side*(1-2*cross);
                        float groove=(float)Math.Sin(t*Math.PI/.33);
                        Prop=new PointF(151+ballSide*(51-27*catchBall),185-28*bounce-31*catchBall);
                        target=new PointF(Prop.X,Math.Min(169,Prop.Y-13));
                        FrontHand=ballSide<0?1:2;
                        if(FrontHand==1) {left=target;right=new PointF(177,158+groove*6);}
                        else {right=target;left=new PointF(124,158-groove*6);}
                        BodyX=(ballSide*4+groove*2)*w;BodyY=-4*groove*groove*w;
                        BodyTurn=groove*(3+3*cross)*w;HeadTurn=-groove*6*w;HeadX=-ballSide*3*w;
                        HeadY=(2*bounce-2)*w;EarLeft=.65f*groove;EarRight=-EarLeft;
                        TailTurn=(ballSide*9+groove*7)*w;Tail=5*groove*w;
                        Focus=.65f*w;GazeX=ballSide;GazeY=.65f;Squint=.15f*groove*groove*w;
                        break;
                    case TrickKind.Freeze:
                        float lockIn=1-Motion.Smooth((t-1.85f)/.50f);
                        float shake=(1-lockIn)*(float)Math.Sin((t-1.85f)*22);
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
                        float pounce=Motion.Smooth((t-.65f)/.35f)*(.5f+.5f*(float)Math.Sin((t-.8f)*7.7));
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
                float x=151+side*(77+12*u),y=141-56*(float)Math.Sin(Math.PI*u);
                float alpha=w*(float)Math.Sin(Math.PI*u);
                Color edge=Faded(C(0x98643e),alpha),gold=Faded(C(0xf2bf64),alpha);
                using(GraphicsPath coin=new GraphicsPath()) {
                    coin.AddEllipse(x-7,y-7,14,14);Fill(g,coin,gold);Stroke(g,coin,edge,1.25f);
                }
                using(GraphicsPath hole=RoundRect(x-2,y-2,4,4,.5f)) Fill(g,hole,edge);
                Curve(g,Faded(C(0xffe9a4),alpha),1.1f,x-4,y-2,x-3,y-4,x-1,y-5,x+1,y-5);
            } else if(p.Kind==TrickKind.Toast) {
                GraphicsState saved=g.Save();g.TranslateTransform(p.Prop.X,p.Prop.Y);g.ScaleTransform(-side*1.24f,1.24f);
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
            PointF wave=cat.Longhair?TrickPose.TurnPaw(left,fit.Shoulder(true),p.LeftTurn):
                TrickPose.TurnPaw(right,fit.Shoulder(false),p.RightTurn);
            DrawTrickEffects(g,cat,p,wave);
            DrawMemeProp(g,cat,p);
            if(p.InFront(1) || p.InFront(2)) {
                bool isLeft=p.FrontHand==1;
                GraphicsState elbow=g.Save();g.TranslateTransform(p.FrontOffset.X,p.FrontOffset.Y);
                DrawArm(g,cat,isLeft,isLeft?left:right,fit,isLeft?p.LeftTurn:p.RightTurn,isLeft?p.LeftPads:p.RightPads);
                g.Restore(elbow);
            }
            if(p.Kind==TrickKind.Fish && p.Weight>.1f) {
                DrawGrippingTip(g,cat,true,left,fit,p.LeftTurn,p.Weight);
                DrawGrippingTip(g,cat,false,right,fit,p.RightTurn,p.Weight);
            }
        }
    }
}
