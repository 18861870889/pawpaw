using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KeyboardCat
{
    internal static class CoverArt
    {
        public const int Width=332,Height=338;
        public static readonly Color Paper=Cat.Rgb(0xf7f3e9), Ink=Cat.Rgb(0x202b36), Accent=Cat.Rgb(0xab382a);
        // The unprinted paper, overprinted disc and second ink of each cover.
        static readonly int[,] covers={
            {0xf5c9a6,0xe9583e,0xaf4434},{0xdbe1ea,0x8c9ed5,0x53668e},
            {0xfae4a2,0xe9a02e,0x94652e},{0xe9d7d9,0xca848a,0x945a69},
            {0xdbe3ba,0x91a66b,0x536f59},{0xe8d4c3,0xbe9179,0x845b54}
        };
        public static int Edition(Cat c) {return c.Pattern==0?c.ShapeIndex:(c.Pattern+1)%covers.GetLength(0);}

        public static void Background(Graphics g,int edition)
        {
            Color stock=Cat.Rgb(covers[edition,0]),disc=Cat.Rgb(covers[edition,1]),shadow=Cat.Rgb(covers[edition,2]);
            g.SmoothingMode=SmoothingMode.AntiAlias;
            g.Clear(stock);
            using(SolidBrush sun=new SolidBrush(disc))
            using(SolidBrush dots=new SolidBrush(shadow))
            using(SolidBrush light=new SolidBrush(stock))
            using(Pen orbit=new Pen(shadow,1.1f)) {
                RectangleF circle=new RectangleF(54,86,262,262);
                g.DrawEllipse(orbit,72,77,265,265);
                g.FillEllipse(sun,circle);
                // A crescent of offset screen dots follows the overprinted edge.
                for(int row=0;row<60;row++) for(int col=0;col<46;col++) {
                    float y=91+row*4.55f,x=120+col*4.55f+(row%2)*2.27f;
                    float dx=x-185,dy=y-217;
                    if(dx*dx+dy*dy>128*128) continue;
                    float distance=(float)Math.Sqrt((x-154)*(x-154)+(y-193)*(y-193));
                    float weight=Math.Min(1,(distance-90)/52);
                    if(weight>.04f) {float d=.55f+weight*1.24f;g.FillEllipse(dots,x,y,d,d);}
                }
                // Slender four-point printer's stars, drawn with two overlapping inks.
                Star(g,sun,277,43,12,18); Star(g,dots,277,43,5,10);
                Star(g,light,83,192,7,12);
            }
        }
        static void Star(Graphics g,Brush brush,float x,float y,float w,float h)
        {
            g.FillPolygon(brush,new PointF[]{new PointF(x,y-h),new PointF(x+w*.18f,y-h*.18f),new PointF(x+w,y),
                new PointF(x+w*.18f,y+h*.18f),new PointF(x,y+h),new PointF(x-w*.18f,y+h*.18f),
                new PointF(x-w,y),new PointF(x-w*.18f,y-h*.18f)});
        }

        // A pair of printed glints opens with the first bite, then drifts off the face.
        public static void FishGlints(Graphics g,bool longhair,double seconds)
        {
            double t=(seconds-.18)/1.15;
            if(t<=0 || t>=1) return;
            float rise=(float)(1-(1-t)*(1-t));
            float strength=(float)Math.Min(1,Math.Min(t/.17,(1-t)/.34));
            float x=(longhair?239:229)+10*rise,y=(longhair?93:100)-12*rise;
            using(SolidBrush dark=new SolidBrush(Color.FromArgb((int)(230*strength),Cat.Rgb(0x744d67))))
            using(SolidBrush light=new SolidBrush(Color.FromArgb((int)(255*strength),Cat.Rgb(0xffdc83)))) {
                Star(g,dark,x+1.7f,y+1.4f,4.2f*strength,8.2f*strength);
                Star(g,light,x,y,4.2f*strength,8.2f*strength);
                Star(g,dark,x+13,y-9,2.4f*strength,4.6f*strength);
            }
        }

        public static string ItemName(int group,int index)
        {
            if(group==0) return Outfit.Name((Garment)index);
            if(group==1) return Outfit.Name((Headwear)index);
            return (group==2?Painter.BoardNames:Painter.MouseNames)[index];
        }
        public static readonly int[] Counts={Outfit.Garments.Length,Outfit.Hats.Length,Painter.BoardNames.Length,Painter.MouseNames.Length};
        public static Bitmap Item(int group,int index,int size,bool print=true)
        {
            Bitmap b=group==0?Painter.ItemIcon((Garment)index,Headwear.Bare,size):
                group==1?Painter.ItemIcon(Garment.Plain,(Headwear)index,size):Painter.DeskIcon(group==2,index,size);
            if(print) InkLook.Apply(b,size/64f);
            return b;
        }

        public static void Mark(Graphics g,float x,float y,float scale)
        {
            GraphicsState state=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(scale,scale);
            g.SmoothingMode=SmoothingMode.AntiAlias;
            using(SolidBrush ink=new SolidBrush(Ink))
            using(SolidBrush accent=new SolidBrush(Accent))
            using(SolidBrush paper=new SolidBrush(Paper)) {
                g.FillEllipse(accent,16,3,19,19);
                g.FillPolygon(ink,new PointF[]{new PointF(1,6),new PointF(8,11),new PointF(17,10),new PointF(25,4),
                    new PointF(26,20),new PointF(22,26),new PointF(8,27),new PointF(2,22)});
                g.FillEllipse(paper,7,15,3.2f,3.2f);g.FillEllipse(paper,17,14.5f,3.2f,3.2f);
            }
            g.Restore(state);
        }
    }
}
