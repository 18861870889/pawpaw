using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KeyboardCat
{
    internal sealed partial class Painter
    {
        static Matrix TorsoMatrix()
        {
            const float width=.87f, height=.82f;
            return new Matrix(width,0,0,height,Cat.SeatX*(1-width),Cat.SeatY*(1-height));
        }

        // Cross sections in figure coordinates, measured once from the very outline used to draw each cat.
        sealed class TorsoProfile
        {
            const int first=122, last=197;
            readonly PointF[] limits=new PointF[last-first+1];

            public TorsoProfile(Cat cat)
            {
                using (GraphicsPath outline=BodyOutline(cat,0))
                using (Matrix torso=TorsoMatrix())
                {
                    outline.Flatten(torso,.16f);
                    PointF[] points=outline.PathPoints;
                    for (int row=first; row<=last; row++)
                    {
                        float left=float.MaxValue, right=float.MinValue;
                        for (int i=0; i<points.Length; i++)
                        {
                            PointF a=points[i], b=points[(i+1)%points.Length];
                            if (row<Math.Min(a.Y,b.Y) || row>=Math.Max(a.Y,b.Y)) continue;
                            float x=a.X+(row-a.Y)*(b.X-a.X)/(b.Y-a.Y);
                            left=Math.Min(left,x);
                            right=Math.Max(right,x);
                        }
                        limits[row-first]=new PointF(left,right);
                    }
                }
            }

            PointF Sample(float y)
            {
                float row=Motion.Limit(y-first,0,limits.Length-1);
                int lo=(int)row, hi=Math.Min(lo+1,limits.Length-1);
                return Motion.Mix(limits[lo],limits[hi],row-lo);
            }

            public PointF Sides(float y)
            {
                // Smooth isolated fur tufts under fabric, while preserving the width and asymmetric haunches.
                PointF ends=Motion.Mix(Sample(y-3.5f),Sample(y+3.5f),.5f);
                return Motion.Mix(Sample(y),ends,.5f);
            }
        }

        struct ForelegEdge
        {
            public readonly PointF A,B,C,D;
            public ForelegEdge(PointF a,PointF b,PointF c,PointF d) { A=a; B=b; C=c; D=d; }
            public void Trace(GraphicsPath path,bool reverse)
            {
                if (reverse) path.AddBezier(D,C,B,A);
                else path.AddBezier(A,B,C,D);
            }

            public PointF At(float t)
            {
                float r=1-t, a=r*r*r, b=3*r*r*t, c=3*r*t*t, d=t*t*t;
                return new PointF(a*A.X+b*B.X+c*C.X+d*D.X,a*A.Y+b*B.Y+c*C.Y+d*D.Y);
            }

            public ForelegEdge Prefix(float t)
            {
                PointF ab=Motion.Mix(A,B,t), bc=Motion.Mix(B,C,t), cd=Motion.Mix(C,D,t);
                PointF abc=Motion.Mix(ab,bc,t), bcd=Motion.Mix(bc,cd,t);
                return new ForelegEdge(A,ab,abc,Motion.Mix(abc,bcd,t));
            }

            public static ForelegEdge Center(ForelegEdge a,ForelegEdge b)
            {
                return new ForelegEdge(Motion.Mix(a.A,b.A,.5f),Motion.Mix(a.B,b.B,.5f),
                    Motion.Mix(a.C,b.C,.5f),Motion.Mix(a.D,b.D,.5f));
            }

            public ForelegEdge Allowance(ForelegEdge other,float root,float cuff)
            {
                return new ForelegEdge(Away(A,other.A,root),Away(B,other.B,root),
                    Away(C,other.C,cuff),Away(D,other.D,cuff));
            }

            public float CuffParameter(float relaxedLength,float extraPerUnit)
            {
                float length=0;
                PointF previous=A;
                for (int i=1;i<=12;i++)
                {
                    PointF p=At(i/12f); length+=Distance(previous,p); previous=p;
                }
                float wanted=Math.Min(length-8.5f,relaxedLength+Math.Max(0,length-36)*extraPerUnit);
                previous=A;
                float walked=0;
                for (int i=1;i<=12;i++)
                {
                    PointF p=At(i/12f); float step=Distance(previous,p);
                    if (walked+step>=wanted)
                        return ((i-1)+Motion.Limit((wanted-walked)/Math.Max(.001f,step),0,1))/12f;
                    walked+=step; previous=p;
                }
                return 1;
            }
        }

        struct Foreleg
        {
            public readonly ForelegEdge Outer, Inner;
            public readonly PointF Shoulder, Tip, Direction;
            readonly float radius;

            public Foreleg(Cat cat,bool left,PointF paw,GarmentFit fit)
            {
                Shoulder=fit.Shoulder(left);
                float sx=Shoulder.X, sy=Shoulder.Y;
                float dx=paw.X-sx, dy=paw.Y-sy;
                float cx=sx+dx*.11f+(left?-8:8), cy=sy+dy*.42f;
                float bx=paw.X-dx*.20f+(left?-3:3), by=paw.Y-dy*.24f;
                float length=Math.Max(1,Distance(paw,new PointF(bx,by)));
                Direction=new PointF((paw.X-bx)/length,(paw.Y-by)/length);
                Tip=new PointF(paw.X-Direction.X*4,paw.Y-Direction.Y*4);
                radius=cat.Longhair?10.7f:10f;
                float nx=-Direction.Y*radius, ny=Direction.X*radius;
                Outer=new ForelegEdge(new PointF(sx-9,sy),new PointF(cx-12,cy),
                    new PointF(bx+nx*.8f,by+ny*.8f),new PointF(Tip.X+nx,Tip.Y+ny));
                Inner=new ForelegEdge(new PointF(sx+9,sy+2),new PointF(cx+10,cy),
                    new PointF(bx-nx*.8f,by-ny*.8f),new PointF(Tip.X-nx,Tip.Y-ny));
            }

            public GraphicsPath Outline()
            {
                GraphicsPath path=new GraphicsPath();
                Outer.Trace(path,false);
                float dx=Direction.X*radius*1.35f, dy=Direction.Y*radius*1.35f;
                path.AddBezier(Outer.D,new PointF(Outer.D.X+dx,Outer.D.Y+dy),
                    new PointF(Inner.D.X+dx,Inner.D.Y+dy),Inner.D);
                Inner.Trace(path,true);
                return path;
            }
        }

        static float Distance(PointF a,PointF b)
        {
            float x=a.X-b.X, y=a.Y-b.Y;
            return (float)Math.Sqrt(x*x+y*y);
        }
        static PointF Away(PointF point,PointF from,float distance)
        {
            float multiplier=distance/Math.Max(.001f,Distance(point,from));
            return new PointF(point.X+(point.X-from.X)*multiplier,point.Y+(point.Y-from.Y)*multiplier);
        }

        static readonly TorsoProfile[] profiles={new TorsoProfile(Cat.All[0]),new TorsoProfile(Cat.All[1])};

        // u runs from the left seam (-1) to the right seam (+1); v runs from the back neck (0) to the hem (1).
        struct GarmentFit
        {
            public readonly Garment Kind;
            readonly Cat cat;
            readonly TorsoProfile profile;
            readonly float neck, sideHem, hemDrop, ease, breath;
            readonly PointF leftPull, rightPull;

            public GarmentFit(Cat cat, Garment kind, float breath, PointF leftPaw, PointF rightPaw)
            {
                this.cat=cat; Kind=kind; this.breath=breath;
                profile=profiles[cat.ShapeIndex];
                neck=cat.Longhair?127.5f:130f;
                sideHem=(kind==Garment.SailorShirt?183f:187.3f)+(cat.Longhair?1.1f:0);
                hemDrop=kind==Garment.StrawberryDress?6.2f:5.3f;
                ease=kind==Garment.Raincoat?2.3f:.95f;
                if (cat.Longhair) ease-=1.8f;
                leftPull=ShoulderTravel(leftPaw,132,169+breath);
                rightPull=ShoulderTravel(rightPaw,176,171+breath);
            }

            static PointF ShoulderTravel(PointF paw, float restX, float restY)
            {
                return new PointF(Motion.Limit((paw.X-restX)*.052f,-4.3f,4.3f),
                                  Motion.Limit((paw.Y-restY)*.035f,-1.4f,2.4f));
            }

            public PointF At(float u, float v)
            {
                float front=Math.Max(0,1-u*u);
                float y=Motion.Mix(neck,sideHem,v)+front*Motion.Mix(1.3f,hemDrop,v);
                PointF sides=profile.Sides(y);
                float allowance=ease;
                if (Kind==Garment.StrawberryDress) allowance+=4*Motion.Smooth((v-.55f)/.45f);
                if (Kind==Garment.Raincoat) allowance+=1.15f*Motion.Smooth(v);
                float x=Motion.Mix(sides.X-allowance,sides.Y+allowance,(u+1)*.5f);
                float weight=1-Motion.Smooth(v/.72f);
                PointF travel=Motion.Mix(leftPull,rightPull,Motion.Limit((u+1)*.5f,0,1));
                return new PointF(x+travel.X*weight,y+(breath*.6f+travel.Y)*weight);
            }

            public PointF Shoulder(bool left)
            {
                if (Kind==Garment.Plain)
                    return new PointF(left?125:178,(left?132:133)+breath);
                return At(left?-.71f:.64f,cat.Longhair?.10f:.07f);
            }

            public GraphicsPath Curve(float u,float v,params float[] controls)
            {
                GraphicsPath path=new GraphicsPath();
                PointF start=At(u,v);
                for (int i=0;i<controls.Length;i+=6)
                {
                    PointF end=At(controls[i+4],controls[i+5]);
                    path.AddBezier(start,At(controls[i],controls[i+1]),At(controls[i+2],controls[i+3]),end);
                    start=end;
                }
                return path;
            }

            void Side(GraphicsPath path,float u,float from,float to)
            {
                PointF[] points=new PointF[7];
                for (int i=0;i<points.Length;i++)
                    points[i]=At(u,Motion.Mix(from,to,i/(float)(points.Length-1)));
                path.AddCurve(points,.38f);
            }

            public GraphicsPath Hem(float v)
            {
                GraphicsPath hem=new GraphicsPath();
                PointF[] points=new PointF[13];
                for (int i=0;i<points.Length;i++)
                {
                    float u=-1+2*i/(float)(points.Length-1);
                    PointF p=At(u,v);
                    if (Kind==Garment.StrawberryDress)
                        p.Y+=(float)(1-Math.Cos(6*Math.PI*u))*.46f*(1-u*u);
                    points[i]=p;
                }
                hem.AddCurve(points,.4f);
                return hem;
            }

            public GraphicsPath Panel(bool envelope)
            {
                bool dress=Kind==Garment.StrawberryDress && !envelope;
                float start=dress?.45f:.015f;
                GraphicsPath panel=new GraphicsPath();
                Side(panel,-1,start,1);
                using (GraphicsPath hem=Hem(1)) panel.AddPath(hem,true);
                Side(panel,1,1,start);
                if (dress)
                {
                    // The bib joins the wraparound skirt at both underarms. The two scoops leave real armholes.
                    using (GraphicsPath top=Curve(1,start,
                        .78f,.49f,.61f,.34f,.68f,.16f, .74f,.06f,.77f,-.02f,.73f,-.045f,
                        .62f,-.07f,.50f,-.045f,.43f,-.015f, .40f,.10f,.43f,.20f,.37f,.28f,
                        .22f,.35f,-.20f,.35f,-.37f,.28f, -.43f,.20f,-.40f,.10f,-.43f,-.015f,
                        -.52f,-.05f,-.65f,-.07f,-.74f,-.04f, -.79f,-.01f,-.74f,.09f,-.67f,.17f,
                        -.61f,.34f,-.79f,.49f,-1,start))
                        panel.AddPath(top,true);
                }
                else if (!envelope)
                {
                    float opening=Kind==Garment.SailorShirt?.36f:.27f;
                    using (GraphicsPath top=Curve(1,start,
                        .83f,-.04f,.69f,-.06f,.55f,-.03f, .44f,.12f,.23f,opening-.05f,0,opening,
                        -.25f,opening-.05f,-.46f,.12f,-.55f,-.03f, -.70f,-.06f,-.84f,-.04f,-1,start))
                        panel.AddPath(top,true);
                }
                panel.CloseFigure();
                return panel;
            }

            public void ClipUnderlayer(Graphics g)
            {
                // Loose flank fur compresses under a top. The open neck, sleeve holes and hind feet still show fur.
                using (GraphicsPath envelope=Panel(true))
                using (Region visible=new Region(envelope))
                {
                    visible.Union(new RectangleF(50,55,211,neck+4-55));
                    visible.Union(new RectangleF(50,sideHem,211,65));
                    g.SetClip(visible,CombineMode.Intersect);
                }
            }
        }
    }
}
