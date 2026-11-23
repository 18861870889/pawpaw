using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace KeyboardCat
{
    // Both native renderers use the same two ink samples for every 5-bit RGB colour.
    internal static class InkLook
    {
        static readonly int[] neutral={0x192333,0x26354a,0x657988,0xc8c8b7,0xfff3d9};
        static readonly int[] hues={0xc3433b,0xcd702d,0xdd9e25,0x67954e,0x388d88,0x486d9e,0x695ca6,0xb05887,0xc3433b};
        static readonly double[] angles={0,29,53,108,171,214,265,315,360};

        internal static readonly byte[] Inks=MakeTable();

        static double Limit(double v) {return Math.Max(0,Math.Min(v,1));}
        static double Blend(double a,double b,double t) {return a+(b-a)*t;}
        static int Channel(int rgb,int shift) {return (rgb>>shift)&255;}
        static byte[] MakeTable()
        {
            byte[] table=new byte[32768*6];
            for(int key=0;key<32768;key++) {
                double r=(key>>10)/31.0,g=((key>>5)&31)/31.0,b=(key&31)/31.0;
                double high=Math.Max(r,Math.Max(g,b)), low=Math.Min(r,Math.Min(g,b)), chroma=high-low;
                double y=.26*r+.60*g+.14*b;
                double h=chroma<.001?0:(high==r?(g-b)/chroma:high==g?2+(b-r)/chroma:4+(r-g)/chroma)*60;
                if(h<0) h+=360;
                int hue=0;
                while(hue<7 && h>angles[hue+1]) hue++;
                double hueMix=(h-angles[hue])/(angles[hue+1]-angles[hue]);
                int band=y<.38?0:y<.60?1:y<.82?2:3;
                double lo=band==0?0:band==1?.38:band==2?.60:.82;
                double hi=band==0?.38:band==1?.60:band==2?.82:.965;
                double mid=Limit((y-lo)/(hi-lo));
                double coloured=Limit((chroma-.023)/.23);
                double density=Limit((y-.19)/.34)*Limit((.94-y)/.25);
                for(int k=0;k<3;k++) {
                    int shift=16-8*k;
                    double gray=Blend(Channel(neutral[band],shift),Channel(neutral[band+1],shift),mid);
                    double ink=Blend(Channel(hues[hue],shift),Channel(hues[hue+1],shift),hueMix);
                    double colour=y<.57?Blend(Channel(neutral[0],shift),ink,Limit((y-.15)/.42)):
                        Blend(ink,Channel(neutral[4],shift),Limit((y-.57)/.42));
                    double flat=Blend(gray,colour,coloured);
                    table[key*6+k]=(byte)Math.Round(flat);
                    table[key*6+3+k]=(byte)Math.Round(Blend(flat,Channel(neutral[0],shift),density*.20));
                }
            }
            return table;
        }

        public static void Apply(Bitmap image,float scale)
        {
            Rectangle area=new Rectangle(Point.Empty,image.Size);
            BitmapData bits=image.LockBits(area,ImageLockMode.ReadWrite,PixelFormat.Format32bppPArgb);
            try {Apply(bits.Scan0,bits.Stride,image.Width,image.Height,scale);}
            finally {image.UnlockBits(bits);}
        }

        public static unsafe void Apply(IntPtr data,int stride,int width,int height,float scale)
        {
            int step=Math.Max(1,(int)Math.Round(256.0/scale));
            fixed(byte* inks=Inks) {
                for(int y=0,phaseY=0;y<height;y++,phaseY+=step) {
                    byte* p=(byte*)data+y*stride;
                    int row=(phaseY>>8)&3;
                    for(int x=0,phaseX=0;x<width;x++,phaseX+=step,p+=4) {
                        int a=p[3];
                        if(a==0) continue;
                        int r=p[2],g=p[1],b=p[0];
                        if(a!=255) {r=Math.Min(255,(r*255+a/2)/a);g=Math.Min(255,(g*255+a/2)/a);b=Math.Min(255,(b*255+a/2)/a);}
                        int i=(((r>>3)<<10)|((g>>3)<<5)|(b>>3))*6;
                        int column=(phaseX>>8)&3;
                        if((row==0 && column==0)||(row==2 && column==2)) i+=3;
                        if(a==255) {p[2]=inks[i];p[1]=inks[i+1];p[0]=inks[i+2];}
                        else {p[2]=(byte)((inks[i]*a+127)/255);p[1]=(byte)((inks[i+1]*a+127)/255);p[0]=(byte)((inks[i+2]*a+127)/255);}
                    }
                }
            }
        }
    }
}
