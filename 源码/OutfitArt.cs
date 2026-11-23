using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace KeyboardCat
{
    internal sealed partial class Painter
    {
        static readonly Color berryInk = C(0x92515c), berry = C(0xe37e8c), berryLight = C(0xf8b0b0);
        static readonly Color navy = C(0x47617b), sea = C(0x779bb7), sky = C(0xdfeef0);
        static readonly Color ochre = C(0xa06c31), honey = C(0xe9ae4b), butter = C(0xffdc84);
        static readonly Color ivory = C(0xfff5db), mint = C(0xb2d4a3), moss = C(0x597b61);

        static void Piece(Graphics g, Color top, Color bottom, Color outline, float weight,
                          float x, float y, params float[] curves)
        {
            using (GraphicsPath path = Shape(x,y,curves))
            {
                RectangleF bounds = path.GetBounds();
                Gradient(g,path,new PointF(bounds.Left,bounds.Top-.2f),
                    new PointF(bounds.Left+bounds.Width*.30f,bounds.Bottom+.2f),top,bottom);
                Stroke(g,path,outline,weight);
            }
        }

        static void Seam(Graphics g,GarmentFit fit,Color color,float width,float u,float v,params float[] controls)
        {
            using (GraphicsPath path=fit.Curve(u,v,controls)) Stroke(g,path,color,width);
        }

        static void SewnPiece(Graphics g,GarmentFit fit,Color top,Color bottom,Color outline,
            float u,float v,params float[] controls)
        {
            using (GraphicsPath path=fit.Curve(u,v,controls))
            {
                path.CloseFigure();
                RectangleF b=path.GetBounds();
                Gradient(g,path,new PointF(b.Left,b.Top-.2f),new PointF(b.Left+b.Width*.32f,b.Bottom+.2f),top,bottom);
                Stroke(g,path,outline,1.12f);
            }
        }

        static void Button(Graphics g,PointF p,Color edge,Color face,float radius)
        {
            Oval(g,edge,p.X-radius,p.Y-radius,radius*2,radius*2);
            float r=radius*.70f;
            Oval(g,face,p.X-r,p.Y-r-.22f,r*2,r*2);
            Oval(g,Color.FromArgb(210,255,249,222),p.X-r*.55f,p.Y-r*.60f,r*.62f,r*.62f);
        }

        static void DrawGarment(Graphics g,GarmentFit fit)
        {
            if (fit.Kind==Garment.Plain) return;
            Color edge,side,mid,light;
            switch(fit.Kind)
            {
                case Garment.StrawberryDress: edge=berryInk;side=C(0xc97180);mid=C(0xea929b);light=C(0xf8b2b1);break;
                case Garment.SailorShirt: edge=navy;side=C(0xafc4cd);mid=C(0xe0ebea);light=C(0xfff9ee);break;
                case Garment.ForestVest: edge=C(0x48675b);side=C(0x779774);mid=C(0xa2bb8b);light=C(0xc9d6a5);break;
                case Garment.MoonPajamas: edge=C(0x595472);side=C(0x817b9f);mid=C(0xa09abb);light=C(0xc6bad8);break;
                case Garment.KnitShirt: edge=C(0x916c57);side=C(0xcda57c);mid=C(0xf2d7ab);light=C(0xffedcd);break;
                default: edge=ochre;side=C(0xd49a46);mid=C(0xf6c76c);light=C(0xffdc89);break;
            }

            using (GraphicsPath hem=fit.Hem(1))
            using (GraphicsPath panel=fit.Panel(false))
            {
                GraphicsState shadow=g.Save();
                g.TranslateTransform(0,1.25f);
                Stroke(g,hem,Color.FromArgb(67,86,73,74),2.35f);
                g.Restore(shadow);
                // Anchor the light across the hips so a reaching shoulder cannot rotate the belly's shading.
                PointF left=fit.At(-1,.78f), right=fit.At(1,.78f);
                Gradient(g,panel,left,new PointF(right.X,left.Y),side,mid,light,mid,Tint(side,edge,.10f));
                Stroke(g,panel,edge,1.42f);
                GraphicsState clipped=g.Save();
                g.SetClip(panel,CombineMode.Intersect);
                Seam(g,fit,Color.FromArgb(96,edge),.85f,.86f,.46f, .83f,.62f,.88f,.82f,.86f,.98f);
                Seam(g,fit,Color.FromArgb(130,light),.85f,-.84f,.51f, -.87f,.65f,-.89f,.78f,-.87f,.95f);
                switch(fit.Kind) {
                    case Garment.StrawberryDress: DressDetails(g,fit);break;
                    case Garment.SailorShirt: SailorDetails(g,fit);break;
                    case Garment.Raincoat: RaincoatDetails(g,fit);break;
                    case Garment.ForestVest:
                        Seam(g,fit,edge,1.65f,0,.1f, .04f,.42f,-.03f,.77f,0,1);
                        for(int s=-1;s<=1;s+=2) {
                            SewnPiece(g,fit,light,mid,edge,s*.27f,.61f, s*.45f,.60f,s*.67f,.57f,s*.81f,.54f,
                                s*.80f,.75f,s*.75f,.85f,s*.55f,.88f, s*.39f,.88f,s*.28f,.86f,s*.27f,.61f);
                            Seam(g,fit,edge,1.15f,s*.29f,.66f,s*.46f,.68f,s*.64f,.63f,s*.78f,.59f);
                        }
                        Button(g,fit.At(0,.52f),edge,butter,2);Button(g,fit.At(0,.83f),edge,butter,2);break;
                    case Garment.MoonPajamas:
                        using(GraphicsPath piping=fit.Hem(.91f)) Stroke(g,piping,ivory,1.7f);
                        for(int i=0;i<5;i++) {
                            PointF star=fit.At(-.82f+i*.38f,i%2==0?.70f:.52f);
                            Star(g,star.X,star.Y,i%2==0?2.5f:1.7f,ivory);
                        }
                        PointF moon=fit.At(.14f,.73f);
                        using(GraphicsPath crescent=Shape(moon.X,moon.Y-5, moon.X-6,moon.Y-5,moon.X-7,moon.Y+3,moon.X-1,moon.Y+4,
                            moon.X+2,moon.Y+5,moon.X+4,moon.Y+3,moon.X+5,moon.Y+1,
                            moon.X,moon.Y+3,moon.X-3,moon.Y-1,moon.X,moon.Y-5)) Fill(g,crescent,butter);
                        break;
                    case Garment.KnitShirt:
                        for(int i=0;i<4;i++) using(GraphicsPath stripe=fit.Hem(.39f+i*.155f))
                            Stroke(g,stripe,i%2==0?C(0xb77c64):C(0xd4a182),2.8f);
                        using(GraphicsPath rib=fit.Hem(.97f)) Stroke(g,rib,edge,1.4f);
                        break;
                }
                g.Restore(clipped);
            }
        }

        static void Star(Graphics g,float x,float y,float r,Color color)
        {
            PointF[] points=new PointF[8];
            for(int i=0;i<8;i++) {
                double a=i*Math.PI/4;float d=i%2==0?r:r*.36f;
                points[i]=new PointF(x+d*(float)Math.Cos(a),y+d*(float)Math.Sin(a));
            }
            using(SolidBrush b=new SolidBrush(color)) g.FillPolygon(b,points);
        }

        static void DressDetails(Graphics g,GarmentFit fit)
        {
            using (GraphicsPath waist=fit.Curve(-1,.52f, -.64f,.61f,-.28f,.59f,0,.595f,
                        .33f,.60f,.68f,.59f,1,.51f))
                Stroke(g,waist,berryInk,1.12f);
            using (GraphicsPath hem=fit.Hem(.935f)) Stroke(g,hem,ivory,1.3f);
            Seam(g,fit,Color.FromArgb(130,berryInk),1.05f,-.67f,.60f, -.63f,.73f,-.72f,.81f,-.74f,.92f);
            Seam(g,fit,Color.FromArgb(153,ivory),1.0f,-.61f,.62f, -.57f,.72f,-.63f,.81f,-.65f,.90f);
            Seam(g,fit,Color.FromArgb(112,berryInk),.95f,.57f,.61f, .57f,.71f,.64f,.84f,.66f,.93f);
            Button(g,fit.At(-.50f,.29f),ochre,butter,2.5f);
            Button(g,fit.At(.50f,.29f),ochre,butter,2.5f);
            PointF chest=fit.At(0,.45f);
            Fruit(g,chest.X,chest.Y,.66f);
            for (int i=0;i<7;i++)
            {
                PointF seed=fit.At(-.80f+i*.255f,i%2==0?.79f:.71f);
                Oval(g,ivory,seed.X-.8f,seed.Y-1.2f,1.7f,2.4f);
            }
        }

        static void SailorDetails(Graphics g,GarmentFit fit)
        {
            using (GraphicsPath stripe=fit.Hem(.89f)) Stroke(g,stripe,sea,2.15f);
            using (GraphicsPath welt=fit.Hem(.958f)) Stroke(g,welt,navy,1.05f);
            for (int side=-1;side<=1;side+=2)
            {
                SewnPiece(g,fit,C(0x98b6c8),C(0x61859f),navy,side*.99f,.045f,
                    side*.93f,-.025f,side*.73f,-.03f,side*.56f,-.025f,
                    side*.42f,.15f,side*.18f,.31f,side*.015f,.365f,
                    side*.045f,.42f,side*.095f,.48f,side*.15f,.515f,
                    side*.40f,.47f,side*.80f,.32f,side*.97f,.18f,
                    side*.99f,.13f,side*.99f,.09f,side*.99f,.045f);
                Seam(g,fit,ivory,1.45f,side*.91f,.16f, side*.74f,.28f,side*.43f,.405f,side*.17f,.452f);
            }
            SewnPiece(g,fit,C(0xf09d8d),C(0xc96369),berryInk,.015f,.35f,
                .085f,.325f,.17f,.35f,.17f,.393f, .16f,.44f,.11f,.47f,.085f,.49f,
                .12f,.56f,.22f,.65f,.21f,.72f, .17f,.70f,.12f,.68f,.085f,.68f,
                .06f,.71f,.03f,.745f,.005f,.765f, -.032f,.66f,-.005f,.55f,.025f,.477f,
                -.055f,.43f,-.047f,.384f,.015f,.35f);
        }

        static void RaincoatDetails(Graphics g,GarmentFit fit)
        {
            Seam(g,fit,ochre,1.22f,.025f,.24f, .04f,.47f,.077f,.76f,.067f,.988f);
            Seam(g,fit,ivory,.9f,.082f,.31f, .10f,.53f,.13f,.75f,.12f,.953f);
            for (int side=-1;side<=1;side+=2)
            {
                SewnPiece(g,fit,C(0xffe6a3),C(0xedb451),ochre,side*.99f,.08f,
                    side*.90f,-.025f,side*.69f,-.045f,side*.55f,-.025f,
                    side*.39f,.12f,side*.17f,.24f,side*.005f,.27f,
                    side*.09f,.32f,side*.20f,.39f,side*.25f,.437f,
                    side*.50f,.385f,side*.79f,.29f,side*.99f,.17f,
                    side, .14f,side*.99f,.105f,side*.99f,.08f);
                SewnPiece(g,fit,C(0xf6c56e),C(0xe9ac51),ochre,side*.55f,.655f,
                    side*.66f,.656f,side*.76f,.633f,side*.87f,.60f,
                    side*.87f,.70f,side*.86f,.78f,side*.79f,.81f,
                    side*.69f,.86f,side*.61f,.825f,side*.56f,.775f,
                    side*.55f,.72f,side*.55f,.695f,side*.55f,.655f);
                Seam(g,fit,ivory,.8f,side*.57f,.68f, side*.67f,.68f,side*.76f,.651f,side*.84f,.634f);
            }
            for (int i=0;i<3;i++) Button(g,fit.At(.046f+i*.009f,.45f+i*.195f),ochre,ivory,2.05f);
            using (GraphicsPath hem=fit.Hem(.946f)) Stroke(g,hem,Color.FromArgb(186,ivory),.95f);
        }

        static void Fruit(Graphics g, float x, float y, float scale)
        {
            GraphicsState state=g.Save();
            g.TranslateTransform(x,y); g.ScaleTransform(scale,scale);
            Piece(g,C(0xf39693),C(0xd95c6e),berryInk,1.15f,0,-5,
                6,-10,12,-6,10,0, 9,6,3,11,.2f,12, -3,11,-9,6,-10,0,
                -12,-6,-6,-10,0,-5);
            Patch(g,moss,0,-5, -3,-9,-6,-10,-9,-10, -8,-7,-6,-5,-3,-4,
                -5,-3,-6,0,-7,1, -3,0,-1,-1,0,-3,
                2,-1,6,0,8,.5f, 7,-3,5,-5,3,-5, 6,-6,8,-8,8,-10,
                4,-9,2,-8,1,-7, 1,-9,1,-10,2,-12, 0,-12,-1,-9,0,-5);
            Oval(g,ivory,-5,-.3f,1.5f,2.3f); Oval(g,ivory,3,1,1.5f,2.4f); Oval(g,ivory,-1.5f,5,1.4f,2.2f);
            g.Restore(state);
        }

        static GraphicsPath CuffArc(PointF left,PointF right,PointF tangent,float depth,float shift)
        {
            left=new PointF(left.X+tangent.X*shift,left.Y+tangent.Y*shift);
            right=new PointF(right.X+tangent.X*shift,right.Y+tangent.Y*shift);
            PointF b=Motion.Mix(left,right,.31f), c=Motion.Mix(left,right,.69f);
            GraphicsPath path=new GraphicsPath();
            path.AddBezier(left,new PointF(b.X+tangent.X*depth,b.Y+tangent.Y*depth),
                new PointF(c.X+tangent.X*depth,c.Y+tangent.Y*depth),right);
            return path;
        }

        static void DrawSleeve(Graphics g,Cat cat,Garment garment,Foreleg leg,GraphicsPath fur=null)
        {
            bool sailor=garment==Garment.SailorShirt;
            bool pajamas=garment==Garment.MoonPajamas,knit=garment==Garment.KnitShirt;
            if (!sailor && garment!=Garment.Raincoat && !pajamas && !knit) return;
            ForelegEdge arm=ForelegEdge.Center(leg.Outer,leg.Inner);
            float length=(sailor?20.3f:26.5f)+(cat.Longhair?1.7f:0);
            float t=arm.CuffParameter(length,sailor?.15f:.22f);
            ForelegEdge left=leg.Outer.Prefix(t), right=leg.Inner.Prefix(t);
            float slack=(sailor?1.25f:1.85f)+(cat.Longhair?.3f:0);
            ForelegEdge a=left.Allowance(right,slack*.65f,slack);
            ForelegEdge b=right.Allowance(left,slack*.65f,slack);
            PointF ahead=arm.At(Math.Min(1,t+.013f)), behind=arm.At(Math.Max(0,t-.013f));
            float mag=Math.Max(.001f,Distance(ahead,behind));
            PointF tangent=new PointF((ahead.X-behind.X)/mag,(ahead.Y-behind.Y)/mag);
            Color edge=sailor?navy:pajamas?C(0x595472):knit?C(0x916c57):ochre;
            using (GraphicsPath cuff=CuffArc(a.D,b.D,tangent,2.6f,0))
            using (GraphicsPath sleeve=new GraphicsPath())
            {
                if (fur!=null)
                {
                    GraphicsState shade=g.Save();
                    g.SetClip(fur,CombineMode.Intersect);
                    using (GraphicsPath shadow=CuffArc(a.D,b.D,tangent,2.6f,1.65f))
                        Stroke(g,shadow,Color.FromArgb(69,100,79,64),2.35f);
                    g.Restore(shade);
                }

                a.Trace(sleeve,false); sleeve.AddPath(cuff,true); b.Trace(sleeve,true);
                if(pajamas)
                    Gradient(g,sleeve,a.D,b.D,C(0x8a82a9),C(0xc5bbd6),C(0xa198b9));
                else if(knit)
                    Gradient(g,sleeve,a.D,b.D,C(0xd3ac85),C(0xffe9c7),C(0xe9c99c));
                else if (sailor)
                    Gradient(g,sleeve,a.D,b.D,C(0xc3d4d9),C(0xfbf8ea),C(0xdce8e7));
                else
                    Gradient(g,sleeve,a.D,b.D,C(0xe4aa55),butter,C(0xf5bf60));
                Stroke(g,sleeve,edge,1.32f);
                GraphicsState trim=g.Save();
                g.SetClip(sleeve,CombineMode.Intersect);
                using (GraphicsPath band=CuffArc(Away(a.D,b.D,3),Away(b.D,a.D,3),tangent,2.6f,-3.65f))
                    Stroke(g,band,pajamas?ivory:knit?C(0xb77c64):sailor?sea:honey,sailor?1.9f:2.6f);
                using (GraphicsPath stitch=CuffArc(Away(a.D,b.D,3),Away(b.D,a.D,3),tangent,2.6f,-1.75f))
                    Stroke(g,stitch,ivory,.8f);
                g.Restore(trim);
            }
        }

        static void DrawHeadwear(Graphics g, Cat cat, Headwear hat)
        {
            if (hat == Headwear.Bare) return;
            GraphicsState state=g.Save();
            if (hat==Headwear.BerryBow || hat==Headwear.DaisyClip)
            {
                g.TranslateTransform(187,cat.Longhair?47:50);
                g.RotateTransform(14);
            }
            else
            {
                g.TranslateTransform(152,cat.Longhair?36:44);
                g.RotateTransform(hat==Headwear.SailorCap?-7:-3);
            }
            HatPiece(g,hat);
            g.Restore(state);
        }

        static void HatPiece(Graphics g, Headwear hat)
        {
            switch(hat)
            {
                case Headwear.DaisyClip:
                    Curve(g,moss,2.2f,-16,8,-6,5,4,0,12,-6);
                    for(int i=0;i<8;i++) {
                        double a=i*Math.PI/4;
                        float x=5.2f*(float)Math.Cos(a),y=5.2f*(float)Math.Sin(a);
                        Oval(g,C(0xd9cbb3),x-3.8f,y-3.7f,7.6f,7.4f);Oval(g,C(0xfff9dd),x-3.3f,y-3.5f,6.8f,6.7f);
                    }
                    Button(g,new PointF(0,0),ochre,butter,3.5f);break;
                case Headwear.Beret:
                    Piece(g,C(0xde9a97),C(0xb26c73),C(0x844e59),1.3f,-25,-4,
                        -34,-10,-26,-22,-8,-24, 10,-30,29,-24,31,-14,
                        33,-6,16,-1,5,0, -6,2,-20,1,-25,-4);
                    Piece(g,C(0xb86b74),C(0xa15a65),C(0x844e59),1.1f,-23,-5,
                        -13,-1,9,-3,23,-9, 24,-4,18,0,12,2, 0,6,-14,6,-21,2, -23,0,-23,-3,-23,-5);
                    Curve(g,C(0x844e59),3.4f,1,-24,2,-27,5,-29,6,-29);
                    Curve(g,C(0xf1b4ab),1,-20,-12,-15,-19,-8,-21,-4,-21);break;
                case Headwear.BerryBow:
                    Piece(g,berryLight,berry,berryInk,1.1f,-2,1,
                        -3,6,-7,10,-10,13, -7,13,-5,12,-3,12, -2,14,-1,15,0,16,
                        3,11,4,5,3,1, 2,0,-1,0,-2,1);
                    Piece(g,berryLight,berry,berryInk,1.35f,-1,-2,
                        -7,-7,-13,-12,-18,-11, -22,-9,-22,5,-18,9,
                        -12,11,-6,6,-1,3, 2,2,1,-1,-1,-2);
                    Piece(g,berryLight,berry,berryInk,1.35f,1,-2,
                        7,-7,14,-12,19,-10, 23,-7,23,5,19,8,
                        14,10,7,6,1,3, 0,2,0,-1,1,-2);
                    Curve(g,berryInk,.8f,-13,-3,-9,-4,-5,-1,-2,0);
                    Curve(g,berryInk,.8f,3,0,7,-2,10,-3,14,-3);
                    Oval(g,ivory,-17,1,2.2f,2.2f); Oval(g,ivory,15,1,2.2f,2.2f);
                    Fruit(g,0,0,.58f);
                    break;
                case Headwear.SailorCap:
                    Piece(g,C(0xfffbec),C(0xe0e8df),navy,1.4f,-25,-2,
                        -28,-7,-31,-13,-28,-16, -18,-14,-14,-23,-4,-24,
                        8,-26,16,-17,27,-18, 30,-15,27,-8,23,-3,
                        14,3,-12,5,-25,-2);
                    Curve(g,C(0xb9ccd0),1.05f,-3,-22,1,-20,4,-17,6,-13);
                    Piece(g,sky,C(0xfbfaec),navy,1.3f,-25,-7,
                        -13,-4,11,-5,26,-10, 27,-8,26,-3,24,0,
                        12,6,-12,8,-23,4, -25,1,-26,-4,-25,-7);
                    Curve(g,navy,2.2f,-23,.6f,-11,4,12,2.5f,24,-3);
                    Oval(g,ochre,-3,-6.5f,6.2f,6.2f);
                    Oval(g,butter,-1.9f,-5.6f,3.75f,3.75f);
                    break;
                case Headwear.FrogHat:
                    Piece(g,C(0xbddb9f),C(0x8bb78e),moss,1.35f,-24,-3,
                        -24,-15,-17,-21,-6,-21, 8,-23,21,-20,25,-7,
                        26,-2,25,0,23,2, 1,7,-15,5,-24,-3);
                    FrogEye(g,-13,-20); FrogEye(g,14,-20);
                    Piece(g,C(0xc9daa5),mint,moss,1.3f,-27,-4,
                        -16,0,13,-1,26,-5, 29,-4,34,0,35,3,
                        25,10,-17,12,-34,6, -35,3,-31,-2,-27,-4);
                    Curve(g,moss,1.2f,-4,-10,-1,-7,2,-7,5,-11);
                    Oval(g,C(0xe7aca1),-12,-10,5.2f,2.5f);
                    Oval(g,C(0xe7aca1),8,-11,5.2f,2.5f);
                    break;
            }
        }

        static void FrogEye(Graphics g, float x, float y)
        {
            Oval(g,moss,x-6.5f,y-6.4f,13,13);
            Oval(g,mint,x-5.4f,y-5.4f,10.8f,11.2f);
            Oval(g,ivory,x-3.4f,y-3.6f,6.8f,7.2f);
            Oval(g,C(0x4a574c),x-1.7f,y-2,3.4f,4.2f);
            Oval(g,ivory,x-1.1f,y-1.5f,1.15f,1.15f);
        }

        public static Bitmap ItemIcon(Garment garment, Headwear hat, int size)
        {
            Bitmap icon = new Bitmap(size,size,PixelFormat.Format32bppPArgb);
            using (Graphics g=Graphics.FromImage(icon))
            {
                Setup(g);
                g.Clear(Color.Transparent);
                g.TranslateTransform(size*.5f,size*.5f);
                if (garment != Garment.Plain)
                {
                    g.ScaleTransform(size/131f,size/131f); g.TranslateTransform(-156,-160);
                    Cat cat=Cat.All[0];
                    PointF left=new PointF(97,164), right=new PointF(210,165);
                    GarmentFit fit=new GarmentFit(cat,garment,0,left,right);
                    DrawGarment(g,fit);
                    if (garment!=Garment.StrawberryDress)
                    {
                        DrawSleeve(g,cat,garment,new Foreleg(cat,true,left,fit));
                        DrawSleeve(g,cat,garment,new Foreleg(cat,false,right,fit));
                    }
                }
                else if (hat != Headwear.Bare)
                {
                    float span=hat==Headwear.BerryBow?50:76;
                    g.ScaleTransform(size/span,size/span); g.TranslateTransform(0,hat==Headwear.BerryBow?-1:8);
                    HatPiece(g,hat);
                }
                else
                {
                    g.ScaleTransform(size/32f,size/32f);
                    using (Pen p=new Pen(C(0xa6aaa3),1.7f))
                    {
                        g.DrawEllipse(p,-10,-10,20,20);
                        g.DrawLine(p,-6.6f,6.6f,6.6f,-6.6f);
                    }
                }
            }
            return icon;
        }
    }
}
