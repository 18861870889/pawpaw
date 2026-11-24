using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KeyboardCat
{
    internal sealed partial class Painter
    {
        partial struct TrickPose
        {
            static float Ease(float t,float start,float end)
            {return Motion.Smooth((t-start)/(end-start));}

            static float Accent(float t,float peak,float attack,float release)
            {
                return t<peak?Ease(t,peak-attack,peak):1-Ease(t,peak,peak+release);
            }
            static float Track(float step,float[] values)
            {
                float pos=Motion.Limit(step,0,values.Length-1);
                int from=(int)pos,to=Math.Min(from+1,values.Length-1);
                return Motion.Mix(values[from],values[to],Motion.Smooth(pos-from));
            }

            // One phrase of step-touch: travel, bring the free foot in, reverse, then finish open.
            static readonly float[] danceTravel={0,-15,-28,-9,19,29,8,-11,0};
            static readonly float[] danceShoulder={0,-6,-9,3,8,5,-7,8,0};
            static readonly float[] danceLeftX={124,103,152,164,127,96,130,103,118};
            static readonly float[] danceLeftY={164,140,132,116,144,110,147,103,159};
            static readonly float[] danceRightX={178,168,209,175,142,137,209,200,182};
            static readonly float[] danceRightY={167,139,110,142,134,123,130,102,161};
            void StepTouch(ref PointF left,ref PointF right)
            {
                float count=(Seconds-.13f)/.405f,w=Weight;
                float down=(float)(.5-.5*Math.Cos(2*Math.PI*count));
                float swing=Track(count,danceShoulder);
                BodyX=Track(count,danceTravel)*w; BodyTurn=swing*w;
                BodyY=(4.4f*down-3.7f*(1-down))*w;
                Wide=.032f*down*w; Tall=-.052f*down*w;
                float hop=Accent(count,6.76f,.43f,.42f)*w;
                BodyY-=11*hop;Tall-=(.045f*hop);Air=.29f*hop;
                HeadTurn=-.66f*Track(count-.22f,danceShoulder)*w; HeadY=2.6f*down*w;
                FrontHand=3;
                left=new PointF(Track(count,danceLeftX),Track(count,danceLeftY));
                right=new PointF(Track(count-.10f,danceRightX),Track(count-.10f,danceRightY));
                Legs=w;
                LeftStep=(Accent(count,2,.66f,.64f)+Accent(count,5.8f,.72f,.62f))*w;
                RightStep=(Accent(count,3.1f,.62f,.69f)+Accent(count,6.9f,.62f,.66f))*w;
                EarLeft=-.52f+swing*.07f; EarRight=.25f-swing*.075f;
                TailTurn=-1.55f*Track(count-.28f,danceShoulder)*w; Tail=swing*.7f*w;
                Focus=.65f*w; GazeX=Motion.Limit(swing*.083f,-1,1); GazeY=-.16f; Squint=.18f*down*w;
            }

            static readonly float[] coneTravel={0,13,22,5,-16,-21,-4,10,0};
            static readonly float[] coneHeight={160,142,112,105,117,128,99,110,153};
            static readonly float[] coneReach={30,44,54,59,45,48,58,55,32};
            void ConeDance(Cat cat,ref PointF left,ref PointF right)
            {
                float t=Seconds,w=Weight,side=cat.Longhair?-1:1;
                float count=(t-.11f)/.405f, sway=Track(count,coneTravel);
                float beat=(float)(.5-.5*Math.Cos(2*Math.PI*count));
                float salute=Accent(t,3.04f,.21f,.32f);
                BodyX=side*sway*w; BodyY=(4*beat-4+4*salute)*w;
                BodyTurn=side*(sway*.35f-5*salute)*w; Tall=(-.032f*beat-.055f*salute)*w;
                HeadTurn=side*(-sway*.24f+9*salute)*w; HeadY=(2*beat+7*salute)*w;
                FrontHand=3; Legs=w;
                LeftStep=(Accent(count,2.05f,.7f,.69f)+Accent(count,5.8f,.75f,.68f))*w;
                RightStep=(Accent(count,3.08f,.68f,.66f)+Accent(count,6.95f,.68f,.62f))*w;
                PointF conePaw=new PointF(151+side*Track(count,coneReach),Track(count,coneHeight));
                float trace=Ease(t,1.05f,2.18f),a=trace*(float)(Math.PI*2);
                float curve=(float)Math.Sin(a);
                float heartY=(float)(13*Math.Cos(a)-5*Math.Cos(2*a)-2*Math.Cos(3*a)-Math.Cos(4*a));
                float showing=Ease(t,.79f,1.06f)*(1-Ease(t,2.2f,2.58f));
                PointF free=new PointF(151-side*(45-6*beat),151-14*beat);
                free=Motion.Mix(free,new PointF(148-side*13+side*24*curve*curve*curve,128-heartY),showing);
                left=side<0?conePaw:free;right=side<0?free:conePaw;
                Beat=trace; EarLeft=-.38f+.45f*beat; EarRight=.25f-.55f*beat;
                TailTurn=-side*Track(count-.28f,coneTravel)*.74f*w; Tail=side*(beat*9-4)*w;
                Focus=.6f*w;GazeX=side*(1-showing)-side*.35f*showing;GazeY=-.34f;Squint=.42f*salute*w;
            }

            internal static readonly PointF[] GlassButtons={new PointF(111,140),new PointF(159,123),new PointF(207,140)};
            internal static readonly float[] TouchTimes={.86f,1.12f,1.38f};

            PointF FromScreen(Cat cat,PointF position)
            {
                PointF point=cat.ToFigure(position);
                float x=point.X-Cat.SeatX-BodyX,y=point.Y-Cat.SeatY-BodyY;
                double a=BodyTurn*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a);
                return new PointF(Cat.SeatX+(float)(x*c+y*s)/(1+Wide),Cat.SeatY+(float)(-x*s+y*c)/(1+Tall));
            }

            public void GlassSpace(Graphics g,Cat cat)
            {
                // Cancel the cat's transform, preserving the caller's display scale.
                g.TranslateTransform(Cat.SeatX,Cat.SeatY);
                g.ScaleTransform(1/(1+Wide),1/(1+Tall));
                g.RotateTransform(-BodyTurn);
                g.TranslateTransform(-Cat.SeatX-BodyX,-Cat.SeatY-BodyY);
                g.TranslateTransform(Cat.SeatX,Cat.SeatY);
                g.ScaleTransform(1/cat.WidthScale,1/cat.HeightScale);
                g.TranslateTransform(-Cat.SeatX,-Cat.SeatY);
            }

            void ScreenJump(Cat cat,ref PointF left,ref PointF right)
            {
                float t=Seconds,w=Weight;
                float crouch=Accent(t,.40f,.32f,.16f),land=Accent(t,1.75f,.17f,.31f);
                float phase=Motion.Limit((t-.50f)/1.18f,0,1);
                Air=4*phase*(1-phase)*w; Legs=w;
                float travel=Ease(t,.48f,1.44f);
                BodyX=((travel-.5f)*17*Air-3*crouch)*w;
                BodyY=9*crouch+7*land-(cat.Longhair?31:37)*Air;
                // A little perspective leaves headroom for the ears of the taller breeds.
                Tall=-.13f*crouch-.085f*land-(cat.Longhair?.18f:.105f)*Air;
                Wide=.105f*crouch+.075f*land-.055f*Air;
                BodyTurn=(-4*crouch+(-4.4f+8*travel)*Air+2.5f*land)*w;
                HeadY=(6*crouch+3*land+3*Air)*w;HeadTurn=-BodyTurn*.67f;
                EarLeft=.92f*crouch-.56f*Air+.54f*land;EarRight=.8f*crouch-.65f*Air+.7f*land;
                TailTurn=(17*crouch-23*Air+13*land)*w;Tail=(-8*crouch+11*Air)*w;
                LeftStep=RightStep=.75f*Air;FrontHand=3;
                left=new PointF(123+5*crouch,163+8*crouch-20*Air);
                right=new PointF(180-5*crouch,165+9*crouch-20*Air);
                for(int i=0;i<GlassButtons.Length;i++) {
                    float contact=TouchTimes[i];
                    float reach=Accent(t,contact,.19f,.21f);
                    float palm=Accent(t,contact,.115f,.145f);
                    PointF target=FromScreen(cat,GlassButtons[i]);
                    if(i==0 || (i==1 && cat.Longhair)) {left=Motion.Mix(left,target,reach);PalmLeft=Math.Max(PalmLeft,palm*w);}
                    else {right=Motion.Mix(right,target,reach);PalmRight=Math.Max(PalmRight,palm*w);}
                }
                float excited=Accent(t,2.14f,.18f,.34f);
                left.Y-=12*excited;right.Y-=12*excited;
                Focus=w; GazeX=(-.83f+1.66f*Ease(t,.84f,1.4f))*Air;GazeY=-.5f*Air;
                Squint=(.39f*land+.26f*excited)*w;
            }

            void Dinner(Cat cat,ref PointF left,ref PointF right)
            {
                float t=Seconds,w=Weight,side=cat.Longhair?-1:1;
                float gather=Ease(t,.14f,.50f)*(1-Ease(t,2.68f,3.1f));
                float bite=Accent(t,1.06f,.40f,.30f)+Accent(t,2.04f,.40f,.32f);
                float scoop=Accent(t,.60f,.18f,.20f)+Accent(t,1.59f,.18f,.20f);
                float chew=Accent(t,1.39f,.15f,.14f)+Accent(t,2.37f,.14f,.16f);
                BodyTurn=-side*(2.5f*gather+2*scoop)*w; HeadTurn=side*(4*scoop-4*bite)*w;
                HeadX=side*(3*bite-3*scoop)*w;HeadY=(4*gather+6*scoop+5*bite+2*chew)*w;
                Prop=new PointF(151-side*5,164-10*gather-2*chew);
                Spoon=new PointF(Prop.X+side*(5+10*(float)Math.Sin(Math.PI*bite)),Prop.Y-6-21*bite+4*scoop);
                SpoonGrip=new PointF(Spoon.X+side*(20-5*bite),Spoon.Y+3+10*bite);
                PointF support=new PointF(Prop.X-side*16,Prop.Y+7);
                FrontHand=3;left=side<0?SpoonGrip:support;right=side<0?support:SpoonGrip;
                Beat=bite;Focus=.8f*w;GazeX=-side*.25f;GazeY=.83f-.65f*bite;
                Squint=(.65f*bite+.32f*chew)*w;EarLeft=.46f*chew-.31f*scoop;EarRight=-.39f*bite;
                TailTurn=side*(-6+10*bite)*w;Tail=4*chew*w;
            }
        }

        static void Footwork(Graphics g,Cat cat,TrickPose p)
        {
            if(p.Legs<=.01f) return;
            for(int i=0;i<2;i++) {
                float side=i==0?-1:1,lift=i==0?p.LeftStep:p.RightStep;
                float x=151+side*(29+19*lift-8*p.Air)-p.BodyX*(1-lift)*(1-p.Air)*.54f;
                float y=190+5*p.Legs-13*lift-3*p.Air;
                GraphicsState state=g.Save();g.TranslateTransform(x,y);g.RotateTransform(side*(8+19*lift));
                Color fur=Faded(cat.Fur,p.Legs),shade=Faded(cat.Shade,p.Legs),ink=Faded(cat.Ink,p.Legs*.85f);
                using(GraphicsPath foot=Shape(-12,1,-13,-4,-7,-8,0,-8,6,-8,12,-4,12,1,12,5,5,7,-1,6,-6,6,-12,5,-12,1)) {
                    Gradient(g,foot,new PointF(0,-6),new PointF(1,7),fur,shade);Stroke(g,foot,ink,1.15f);
                }
                Curve(g,ink,.85f,-3,5,-4,4,-4,3,-3,2);
                Curve(g,ink,.85f,3,5,2,4,2,3,3,2);g.Restore(state);
            }
        }

        static void ContactPaw(Graphics g,Cat cat,PointF point,float press)
        {
            if(press<=.006f) return;
            GraphicsState state=g.Save();g.TranslateTransform(point.X,point.Y);
            g.ScaleTransform(.55f+1.22f*press,.55f+1.08f*press);
            Color fur=Faded(cat.Cream,press),edge=Faded(cat.Ink,press);
            using(GraphicsPath pad=Shape(-12,1,-15,-3,-13,-8,-10,-9,-10,-14,-4,-15,-2,-11,1,-16,7,-15,8,-10,14,-12,17,-6,13,-2,16,7,8,14,0,14,-9,14,-14,8,-12,1)) {
                Gradient(g,pad,new PointF(0,-12),new PointF(0,13),fur,Faded(cat.Fur,press));Stroke(g,pad,edge,1.15f);
            }
            Color pink=Faded(C(0xe798a5),press);
            Oval(g,pink,-6,0,12,9);Oval(g,pink,-10,-6,4.6f,6);Oval(g,pink,-3,-10,4.8f,6.4f);Oval(g,pink,4,-9,4.8f,6.2f);Oval(g,pink,9,-5,3.8f,5);
            g.Restore(state);
        }
    }
}
