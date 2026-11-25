using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KeyboardCat
{
    internal sealed partial class Painter
    {
        static void CoatPatch(Graphics g,int light,int dark,float x,float y,params float[] curves)
        {
            using(GraphicsPath p=Shape(x,y,curves))
            {
                RectangleF b=p.GetBounds();
                Gradient(g,p,new PointF(b.Left,b.Top),new PointF(b.Right,b.Bottom),C(light),C(dark));
            }
        }

        // The face is already clipped to the short- or long-haired silhouette.
        static void FaceMarkings(Graphics g,Cat cat)
        {
            switch(cat.Pattern)
            {
                case Cat.Black:
                    SoftOval(g,Color.FromArgb(130,C(0x797987)),100,32,93,68);
                    break;
                case Cat.White:
                    using(SolidBrush snow=new SolidBrush(C(0xf8f7f3))) g.FillRectangle(snow,55,0,200,145);
                    SoftOval(g,Color.FromArgb(138,C(0xcfd4dc)),80,66,42,57);
                    SoftOval(g,Color.FromArgb(126,C(0xdadce0)),183,61,39,61);
                    break;
                case 1:
                    CoatPatch(g,0xf5c481,0xd89151,84,4, 113,1,196,2,219,13, 225,38,216,70,210,87,
                        201,77,195,72,185,72, 174,72,161,67,151,57, 145,67,132,73,119,73,
                        105,74,97,83,89,92, 80,66,80,27,84,4);
                    Patch(g,C(0xb7794d),131,31, 137,36,143,46,144,56, 136,53,133,44,129,42, 126,37,127,34,131,31);
                    Patch(g,C(0xb7794d),150,27, 154,35,156,46,153,53, 146,47,145,36,146,29, 147,27,148,26,150,27);
                    Patch(g,C(0xb7794d),174,29, 174,42,165,51,159,55, 161,45,165,36,168,31, 170,28,172,28,174,29);
                    Curve(g,C(0xc48355),2.4f,92,79,100,76,105,77,110,79);
                    Curve(g,C(0xc48355),2.2f,205,78,199,74,193,74,187,76);
                    break;
                case 2:
                    CoatPatch(g,0x535463,0x79777d,82,8, 102,6,126,16,137,35, 145,48,148,55,144,67,
                        139,78,144,88,135,94, 125,101,110,97,104,88, 97,80,89,81,83,83, 78,63,76,28,82,8);
                    CoatPatch(g,0x555562,0x6b686d,179,36, 185,21,198,12,211,8, 218,29,210,52,202,59,
                        199,51,180,49,179,36);
                    Oval(g,C(0x656169),176,101,5.5f,4);
                    break;
                case 3:
                    CoatPatch(g,0xf0be83,0xd99466,82,8, 104,4,135,17,149,38, 154,47,151,57,144,60,
                        134,66,138,78,127,81, 117,87,102,76,92,87, 82,67,76,29,82,8);
                    CoatPatch(g,0x5b5862,0x898087,219,7, 223,34,216,73,208,98, 197,100,186,96,183,87,
                        180,79,171,78,165,71, 158,64,160,54,167,46, 178,38,180,20,196,12,
                        204,9,212,8,219,7);
                    Oval(g,C(0xddab77),101,98,10,6);
                    break;
                case 5:
                    using(SolidBrush blue=new SolidBrush(C(0x8998a8))) g.FillRectangle(blue,55,0,200,140);
                    SoftOval(g,C(0xb9c3cb),102,73,100,57);
                    break;
                case 6: case 8: case 9:
                    bool silver=cat.Pattern==6, classic=cat.Pattern==8;
                    Color baseFur=C(silver?0xb7bcc2:classic?0xb78d6c:0xb89978);
                    Color stripe=C(silver?0x555963:classic?0x684d45:0x715c4e);
                    using(SolidBrush coat=new SolidBrush(baseFur)) g.FillRectangle(coat,55,0,200,126);
                    SoftOval(g,Color.FromArgb(192,244,231,209),98,76,111,58);
                    for(int i=-2;i<=2;i++) {
                        float x=151+i*13;
                        Curve(g,stripe,classic?4.3f:2.7f,x+i*3,32,x+i*2,39,x-i*2,47,x-i*3,55-Math.Abs(i)*2);
                    }
                    for(int side=-1;side<=1;side+=2) {
                        for(int i=0;i<(classic?2:3);i++) {
                            float y=59+i*8;
                            Curve(g,stripe,classic?4.2f:2.3f,151+side*70,y-9,151+side*60,y-1,151+side*54,y+5,151+side*(40+i*2),y+6);
                        }
                        if(classic) Curve(g,stripe,2.4f,151+side*48,99,151+side*62,91,151+side*62,103,151+side*52,107);
                    }
                    break;
                case 7:
                    SoftOval(g,C(0x90796f),78,24,147,96);
                    CoatPatch(g,0xfaf4e8,0xf0e4d6,110,110, 127,99,145,78,150,61, 151,58,153,59,154,64,
                        158,80,176,102,194,110, 172,128,127,129,110,110);
                    break;
                case 10:
                    SoftOval(g,C(0xe0b585),79,8,146,69);
                    SoftOval(g,Color.FromArgb(155,255,243,220),100,54,104,67);
                    break;
                case 4:
                    SoftOval(g,C(0x886f67),87,42,129,76);
                    SoftOval(g,Color.FromArgb(187,118,90,86),109,49,85,64);
                    CoatPatch(g,0x675860,0xb49c91,88,8, 106,9,122,19,128,37, 124,44,111,54,100,63, 91,49,84,27,88,8);
                    CoatPatch(g,0x77646b,0xb59d90,210,7, 217,27,207,53,201,62, 194,53,178,46,176,37, 181,25,194,9,210,7);
                    break;
            }
        }

        static void BodyMarkings(Graphics g,Cat cat,GraphicsPath body)
        {
            int dark,light;
            switch(cat.Pattern) {
                case 1: dark=0xd39158;light=0xf5ce91;break;
                case 3: dark=0xdedbd4;light=0xfcf8ec;break;
                case Cat.White: dark=0xe0e3e5;light=0xfffdf6;break;
                case 4: dark=0xb59b90;light=0xf4e5cd;break;
                case 5: dark=0x8291a2;light=0xb6c4cf;break;
                case 6: dark=0x91999e;light=0xd9dddb;break;
                case 7: dark=0xc1afa5;light=0xfdf4e8;break;
                case 8: dark=0xa67f62;light=0xdebd94;break;
                case 9: dark=0x9b7d5f;light=0xd8b791;break;
                case 10: dark=0xe1c4a5;light=0xffedcf;break;
                default:return;
            }
            Gradient(g,body,new PointF(109,124),new PointF(216,183),C(dark),C(light),C(dark));
            GraphicsState saved=g.Save();
            g.SetClip(body,CombineMode.Intersect);
            SoftOval(g,Color.FromArgb(150,C(light)),115,139,76,69);
            if(cat.Pattern==3) {
                CoatPatch(g,0xeab87d,0xc88a55,85,129, 101,116,122,122,130,140, 139,154,131,169,117,177, 107,184,98,179,89,171, 82,159,79,142,85,129);
                CoatPatch(g,0x4e4d57,0x70646a,190,122, 213,119,229,140,227,163, 223,181,212,192,196,190, 205,177,197,163,188,157, 176,147,178,133,190,122);
            }
            if(cat.Pattern==1) for(int j=0;j<3;j++) {
                float y=141+j*9;
                Curve(g,C(0xb4794d),2.5f,96,y-4,103,y,109,y+1,117-j,y+2);
                Curve(g,C(0xb4794d),2.5f,219,y-6,211,y-1,206,y+1,199+j,y+2);
            }
            if(cat.Pattern==6 || cat.Pattern==8 || cat.Pattern==9)
                for(int i=0;i<3;i++) Curve(g,Color.FromArgb(130,C(dark)),2.5f,203,140+i*8,211,137+i*8,217,134+i*8,224,130+i*8);
            g.Restore(saved);
        }

        static void TailMarkings(Graphics g,Cat cat,GraphicsPath tail,float swish)
        {
            if(cat.Pattern==0) return;
            Color near,far;
            switch(cat.Pattern) {
                case Cat.Black: near=cat.Fur;far=cat.Shade;break;
                case Cat.White: near=C(0xf9f7f0);far=C(0xd9dce0);break;
                case 1: near=C(0xf3c58c);far=C(0xc58553);break;
                case 2: near=C(0x999399);far=C(0x575866);break;
                case 3: near=C(0xe1ac78);far=C(0x69616a);break;
                case 5: near=C(0xa7b2bc);far=C(0x758595);break;
                case 6: near=C(0xb3b6b9);far=C(0x66717c);break;
                case 8: case 9: near=C(0xc0a285);far=C(0x786052);break;
                case 10: near=C(0xf0dfc6);far=C(0xcda57c);break;
                default: near=C(0xc1aaa0);far=C(0x79656a);break;
            }
            Gradient(g,tail,new PointF(203,183),new PointF(291,150),near,far);
            GraphicsState clip=g.Save();
            g.SetClip(tail,CombineMode.Intersect);
            if(cat.Pattern==1 || cat.Pattern==3 || cat.Pattern==6 || cat.Pattern==8 || cat.Pattern==9)
            {
                Color line=cat.Pattern==1?C(0xad744d):C(0x6c636d);
                for(int i=0;i<3;i++) {
                    float x=241+i*15;
                    Curve(g,line,cat.Pattern==1?3.5f:6.2f,x-6,135+swish,x-2,150+swish,x+7,168,x+15,187);
                }
            }
            g.Restore(clip);
        }
    }
}
