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
            float swing=(float)Math.Sin(p.Seconds*(cat.Longhair?8.0:9.1));
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
            for(int i=0;i<2;i++) {
                float u=(p.Seconds*.73f+i*.5f)%1,dir=i==0?side:-side;
                Note(g,152+dir*(64+17*u),110-38*u,.52f+.16f*u,dir*(7+10*u),i==0?C(0xcf617c):C(0x659bb4),w*(float)Math.Sin(Math.PI*u));
            }
        }

        static void DrawRiceBowl(Graphics g,TrickPose p)
        {
            if(p.Weight<.01f) return;
            float w=p.Weight,bite=p.Beat;
            GraphicsState saved=g.Save();g.TranslateTransform(151,158-6*bite);
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
            float lift=(float)Math.Sin(p.Seconds*7);
            GraphicsState spoon=g.Save();g.TranslateTransform(16,-6-6*bite);g.RotateTransform(-28+15*lift);
            Curve(g,Faded(C(0x8c6758),w),2.8f,0,0,4,-4,11,-12,15,-17);
            Oval(g,Faded(C(0xf3bc77),w),-5,-2,9,5);g.Restore(spoon);
            for(int i=0;i<2;i++) {
                float u=(p.Seconds*.9f+i*.5f)%1;
                Color steam=Faded(C(0xe7d3bc),w*(float)Math.Sin(Math.PI*u)*.85f);
                float x=-10+13*i,y=-13-10*u;
                Curve(g,steam,1.25f,x,y,x-4,y-3,x+4,y-5,x+1,y-8);
            }
            g.Restore(saved);
        }

        static void DrawTripleTap(Graphics g,Cat cat,TrickPose p,PointF paw)
        {
            if(p.Weight<.01f) return;
            float side=cat.Longhair?-1:1;
            for(int i=0;i<3;i++) {
                float t=p.Seconds-.12f-i*.32f;
                if(t<=0) continue;
                float rise=Motion.Smooth(t/.19f),pop=1+.22f*(float)Math.Sin(Math.PI*Math.Min(1,t/.26f));
                float x=152+side*(53+16*i),y=103-13*i+(1-rise)*8;
                GraphicsState saved=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(pop,pop);g.RotateTransform(side*(i-1)*8);
                float alpha=p.Weight*rise;
                Color edge=Faded(C(0x796079),alpha),ink=Faded(C(0xfff5da),alpha);
                using(GraphicsPath disk=new GraphicsPath()) {
                    disk.AddEllipse(-8,-8,16,16);
                    Fill(g,disk,Faded(i==0?C(0xd98098):i==1?C(0xdeab61):C(0x84aca2),alpha));Stroke(g,disk,edge,1);
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
                if(t<.23f) {
                    float flash=p.Weight*(1-t/.23f);
                    Curve(g,Faded(C(0xdeab61),flash),1.4f,paw.X+side*8,paw.Y-2,paw.X+side*10,paw.Y-5,paw.X+side*13,paw.Y-8,paw.X+side*15,paw.Y-9);
                }
            }
        }
    }
}
