using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KeyboardCat
{
    internal sealed partial class Painter
    {
        static void DrawSweetCone(Graphics g,Cat cat,TrickPose p,PointF paw)
        {
            float w=p.Weight,side=cat.Longhair?-1:1;
            if(w<.01f) return;
            float swing=(float)Math.Sin((p.Seconds-.13f)*Math.PI/.405);
            GraphicsState saved=g.Save();
            g.TranslateTransform(paw.X+side*5,paw.Y-9);
            g.RotateTransform(side*(8+10*swing)); g.ScaleTransform(.8f+.2f*w,.8f+.2f*w);
            Color edge=Faded(C(0x925c50),w),cream=Faded(C(0xffeed5),w);
            using(GraphicsPath cone=new GraphicsPath()) {
                cone.AddPolygon(new[]{new PointF(-10,-2),new PointF(11,-2),new PointF(1,21)});
                Gradient(g,cone,new PointF(-9,0),new PointF(8,12),Faded(C(0xffcf82),w),Faded(C(0xd98c54),w));Stroke(g,cone,edge,1.3f);
            }
            Color waffle=Faded(C(0xba7951),w*.8f);
            Curve(g,waffle,.9f,-7,2,-4,5,1,10,5,13);
            Curve(g,waffle,.9f,6,3,4,6,0,11,-2,14);
            using(GraphicsPath swirl=Shape(-13,-4,-15,-9,-10,-11,-8,-12,-9,-16,-6,-19,-2,-20,3,-22,5,-25,4,-30,13,-25,9,-19,8,-17,14,-16,15,-11,12,-9,17,-5,12,-1,7,0,1,3,-8,2,-13,-4)) {
                Gradient(g,swirl,new PointF(0,-24),new PointF(0,1),cream,Faded(C(0xf5a5ae),w));Stroke(g,swirl,edge,1.25f);
            }
            Curve(g,Faded(C(0xfff8df),w),2.5f,-8,-12,-3,-10,5,-12,8,-15);
            Curve(g,Faded(C(0xd98493),w),1.1f,-5,-18,-1,-16,4,-17,6,-20);
            Oval(g,Faded(C(0xd95065),w),-10,-25,7,7);Oval(g,cream,-8.6f,-24,1.7f,1.7f);
            g.Restore(saved);
            float heart=Motion.Smooth((p.Seconds-2.0f)/.23f)*(1-Motion.Smooth((p.Seconds-2.39f)/.43f));
            if(heart>0) Heart(g,148-side*17,119-9*heart,.4f+.19f*heart,-side*8,heart*w);
            for(int i=0;i<2;i++) {
                float u=(p.Seconds*.73f+i*.5f)%1,dir=i==0?side:-side;
                Note(g,152+dir*(64+17*u),110-38*u,.52f+.16f*u,dir*(7+10*u),i==0?C(0xcf617c):C(0x659bb4),w*(float)Math.Sin(Math.PI*u));
            }
        }

        static void DrawRiceBowl(Graphics g,TrickPose p)
        {
            if(p.Weight<.01f) return;
            float w=p.Weight,bite=p.Beat;
            GraphicsState saved=g.Save();g.TranslateTransform(p.Prop.X,p.Prop.Y);
            Color rim=Faded(C(0x426f69),w),ivory=Faded(C(0xfff0d3),w);
            for(int i=0;i<5;i++) {
                float x=-16+7.2f*i,y=-9-(i==2?5:i%2*2);
                Oval(g,ivory,x-3,y,12,10);
            }
            Oval(g,Faded(C(0xe89864),w),-5,-13,5,4);Oval(g,Faded(C(0x78a878),w),5,-11,5,3);
            using(GraphicsPath bowl=Shape(-22,-4,-21,8,-14,15,0,16,12,16,20,10,22,-4,10,1,-10,1,-22,-4)) {
                Gradient(g,bowl,new PointF(0,-2),new PointF(3,16),Faded(C(0xaadfc7),w),Faded(C(0x61aba3),w));Stroke(g,bowl,rim,1.2f);
            }
            Curve(g,ivory,1.2f,-15,3,-9,8,8,9,15,3);
            using(GraphicsPath foot=RoundRect(-8,14,16,3.5f,1.4f)) Fill(g,foot,rim);
            for(int i=0;i<2;i++) {
                float u=(p.Seconds*.9f+i*.5f)%1;
                Color steam=Faded(C(0xe7d3bc),w*(float)Math.Sin(Math.PI*u)*.85f);
                float x=-10+13*i,y=-13-10*u;
                Curve(g,steam,1.25f,x,y,x-4,y-3,x+4,y-5,x+1,y-8);
            }
            g.Restore(saved);
            Color wood=Faded(C(0x997059),w);
            float x1=p.Spoon.X,y1=p.Spoon.Y,x2=p.SpoonGrip.X,y2=p.SpoonGrip.Y;
            Curve(g,wood,2.8f,x1,y1,x1+(x2-x1)*.35f,y1+1,x2,y2,x2+(x2-x1)*.19f,y2+1);
            Oval(g,wood,x1-5,y1-2.7f,10,5.4f);
            if(bite<.87f) {
                Oval(g,ivory,x1-3.9f,y1-4.3f,5.7f,3.5f);Oval(g,ivory,x1+.2f,y1-4,3.8f,2.6f);
            }
        }

        static void DrawTripleTap(Graphics g,Cat cat,TrickPose p)
        {
            if(p.Weight<.01f) return;
            GraphicsState glass=g.Save();p.GlassSpace(g,cat);
            float reveal=Motion.Smooth((p.Seconds-.14f)/.25f),fade=1-Motion.Smooth((p.Seconds-1.56f)/.39f);
            for(int i=0;i<TrickPose.GlassButtons.Length;i++) {
                float t=p.Seconds-TrickPose.TouchTimes[i];
                float glow=Motion.Smooth((t+.012f)/.045f);
                float pop=t>=0 && t<.23f?1+.25f*(float)Math.Sin(Math.PI*t/.23f):1;
                PointF center=TrickPose.GlassButtons[i];
                GraphicsState saved=g.Save();g.TranslateTransform(center.X,center.Y);
                float alpha=p.Weight*reveal*fade;
                Color color=i==0?C(0xd77791):i==1?C(0xd39942):C(0x5eaa99);
                if(t>=0 && t<.34f) {
                    float u=t/.34f,r=11+18*Motion.Smooth(u);
                    using(Pen ripple=new Pen(Faded(color,alpha*(1-u)),2.3f*(1-u)+.5f)) g.DrawEllipse(ripple,-r,-r,2*r,2*r);
                    for(int ray=0;ray<5;ray++) {
                        double angle=(ray*72+i*19)*Math.PI/180;float dx=(float)Math.Cos(angle),dy=(float)Math.Sin(angle);
                        using(Pen streak=new Pen(Faded(color,alpha*(1-u)),1.4f))
                            g.DrawLine(streak,dx*(r+2),dy*(r+2),dx*(r+5),dy*(r+5));
                    }
                }
                g.ScaleTransform(pop,pop);
                Color edge=Faded(color,alpha*(.41f+.59f*glow)),ink=Faded(glow>.5f?C(0xfff6e4):color,alpha*(.54f+.46f*glow));
                using(GraphicsPath disk=new GraphicsPath()) {
                    disk.AddEllipse(-9,-9,18,18);Fill(g,disk,Faded(glow>.5f?color:C(0xfff9ec),alpha*(.22f+.71f*glow)));Stroke(g,disk,edge,1.3f);
                }
                if(i==0) {
                    using(GraphicsPath thumb=new GraphicsPath()) {
                        thumb.AddPolygon(new[]{new PointF(-5,0),new PointF(-2,0),new PointF(0,-4.5f),new PointF(2,-5),new PointF(2,-1),new PointF(5,-1),new PointF(4,4),new PointF(-5,4)});
                        Fill(g,thumb,ink);
                    }
                } else if(i==1) {
                    using(GraphicsPath coin=new GraphicsPath()) {coin.AddEllipse(-4.8f,-4.8f,9.6f,9.6f);Stroke(g,coin,ink,1.3f);}
                    using(GraphicsPath hole=RoundRect(-1.6f,-1.6f,3.2f,3.2f,.4f)) Fill(g,hole,ink);
                } else {
                    PointF[] points=new PointF[10];
                    for(int k=0;k<points.Length;k++) {
                        double a=-Math.PI/2+k*Math.PI/5;float radius=k%2==0?5.8f:2.6f;
                        points[k]=new PointF(radius*(float)Math.Cos(a),radius*(float)Math.Sin(a));
                    }
                    using(GraphicsPath star=new GraphicsPath()) {star.AddPolygon(points);Fill(g,star,ink);}
                }
                g.Restore(saved);
            }
            g.Restore(glass);
        }
    }
}
