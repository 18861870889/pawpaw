using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace KeyboardCat
{
    internal sealed partial class Painter
    {
        static GraphicsPath RestingHead(bool longhair)
        {
            if (longhair)
                return Shape(99,63,
                    99,48,94,31,98,17, 99,13,104,16,109,19, 117,24,123,31,129,36,
                    136,33,143,33,149,33, 147,31,145,30,144,29,
                    151,29,155,32,159,32, 166,30,172,33,177,35,
                    184,27,192,19,199,15, 203,13,205,16,205,23, 205,35,202,51,201,60,
                    207,68,213,80,213,89, 213,92,210,91,207,89,
                    208,96,211,100,213,102, 209,105,203,103,198,100,
                    199,107,201,111,203,114, 197,114,193,111,188,110,
                    181,120,167,125,153,126, 137,127,124,122,116,115,
                    111,117,106,118,101,117, 103,113,105,110,106,106,
                    101,109,95,109,91,107, 94,103,98,98,98,95,
                    94,97,90,98,87,96, 91,91,94,86,95,81, 93,82,92,82,91,82,
                    92,75,95,68,99,63);
            return Shape(101,61,
                100,54,96,47,93,39, 91,34,94,32,100,33,
                109,33,118,37,124,42, 139,37,162,37,177,41,
                184,36,196,32,204,32, 210,32,211,35,208,40,
                204,47,202,55,202,62, 210,71,212,88,208,99,
                205,110,194,117,182,122, 173,126,162,128,151,128,
                139,128,127,126,116,121, 103,116,94,109,92,98,
                89,85,92,71,101,61);
        }

        // Points above the diagonal from the outer cheek to the crown belong to the ear.
        // The points on that joint stay fixed when the tip swivels.
        static Matrix EarBend(bool longhair,bool left,float amount)
        {
            float outer=left?(longhair?99:101):(longhair?201:202);
            float baseY=left?(longhair?63:61):(longhair?60:62);
            float inner=left?(longhair?129:124):177;
            float innerY=left?(longhair?36:42):(longhair?35:41);
            float slope=(innerY-baseY)/(inner-outer),intercept=baseY-slope*outer;
            float dx=(left?-.47f:.47f)*amount,dy=.27f*amount;
            return new Matrix(1+dx*slope,dy*slope,-dx,1-dy,dx*intercept,dy*intercept);
        }

        static GraphicsPath HeadShape(Cat cat,float left,float right)
        {
            GraphicsPath rest=RestingHead(cat.Longhair);
            if(left==0 && right==0) return rest;
            using(rest) {
                PointF[] points=rest.PathPoints;
                for(int side=0;side<2;side++) {
                    bool isLeft=side==0;
                    PointF[] shifted=rest.PathPoints;
                    using(Matrix bend=EarBend(cat.Longhair,isLeft,isLeft?left:right)) bend.TransformPoints(shifted);
                    int start=isLeft?1:(cat.Longhair?22:13);
                    const int count=9;
                    // Only the three Bezier segments between the cheek and forehead move.
                    for(int i=start;i<start+count;i++) points[i]=shifted[i];
                }
                return new GraphicsPath(points,rest.PathTypes);
            }
        }

        static void DrawHead(Graphics g, Cat cat, float lookX, float lookY, float open,float smile=0,float leftEar=0,float rightEar=0)
        {
            DrawHeadBase(g,cat,leftEar,rightEar);
            DrawGaze(g,cat,lookX,lookY,open,smile);
            DrawMuzzle(g,cat,cat.Longhair ? 97 : 102);
        }

        static void DrawHeadBase(Graphics g,Cat cat,float leftEar=0,float rightEar=0)
        {
            using (GraphicsPath face = HeadShape(cat,leftEar,rightEar))
            {
                Color crown = cat.Longhair ? cat.Fur : Tint(cat.Shade,cat.Fur,.32f);
                Gradient(g,face,new PointF(152,28),new PointF(151,129),
                    crown,cat.Fur,cat.Cream);
                GraphicsState clip = g.Save();
                g.SetClip(face,CombineMode.Intersect);
                if (cat.Longhair) WhiteFace(g,cat); else SilverFace(g,cat);
                FaceMarkings(g,cat);
                g.Restore(clip);
                Edge(g,face,cat);
            }

            if(leftEar==0 && rightEar==0) {
                if(cat.Longhair) TallEars(g,cat);else SmallEars(g,cat);
            } else {
                for(int side=0;side<2;side++) {
                    bool left=side==0;
                    GraphicsState ear=g.Save();
                    g.SetClip(new RectangleF(left?50:151,-30,101,115),CombineMode.Intersect);
                    using(Matrix bend=EarBend(cat.Longhair,left,left?leftEar:rightEar)) g.MultiplyTransform(bend);
                    if(cat.Longhair) TallEars(g,cat);else SmallEars(g,cat);
                    g.Restore(ear);
                }
            }
            DrawCheeks(g,cat);
        }

        static void DrawGaze(Graphics g,Cat cat,float lookX,float lookY,float open,float smile)
        {
            float eyeY = cat.EyeLine;
            DrawEye(g,cat,128,eyeY+1,-4,lookX,lookY,open,smile);
            DrawEye(g,cat,174,eyeY-1,4,lookX,lookY,open,smile);
        }

        static void DrawCheeks(Graphics g, Cat cat)
        {
            float y = cat.Longhair ? 94 : 99;
            Color blush = Color.FromArgb(167,242,171,165);
            SoftOval(g,blush,99,y-5,24,12);
            SoftOval(g,blush,180,y-6,24,12);
            Color line = Color.FromArgb(129,211,140,139);
            Curve(g,line,.9f,106,y+2,107,y+1,108,y,108.6f,y-1);
            Curve(g,line,.9f,111,y+2,112,y+1,113,y,113.6f,y-1);
            Curve(g,line,.9f,188,y+1,189,y,190,y-1,190.6f,y-2);
            Curve(g,line,.9f,193,y+1,194,y,195,y-1,195.6f,y-2);
        }

        static void SilverFace(Graphics g, Cat cat)
        {
            SoftOval(g,Color.FromArgb(174,cat.Cream),111,54,81,76);
            SoftOval(g,Color.FromArgb(90,cat.Shade),79,61,45,47);
            SoftOval(g,Color.FromArgb(90,cat.Shade),181,60,45,47);
            Color pencil = Tint(cat.Shade,cat.Fur,.61f);
            Patch(g,pencil,129,40, 131,42,136,48,137,53, 134,51,130,46,128,44, 127,41,128,40,129,40);
            Patch(g,pencil,145,39, 148,41,150,48,151,54, 147,50,145,45,143,42, 143,40,144,39,145,39);
            Patch(g,pencil,170,40, 166,45,164,49,163,53, 166,51,170,46,172,42, 173,40,171,39,170,40);
            Curve(g,Tint(cat.Fur,cat.Shade,.47f),1.0f,111,74,115,70,120,67,123,67);
            Curve(g,Tint(cat.Fur,cat.Shade,.47f),1.0f,180,65,183,65,187,66,190,70);
            Curve(g,Color.FromArgb(182,cat.Cream),1.5f,99,85,102,77,105,72,109,69);
            Curve(g,Color.FromArgb(178,cat.Cream),1.5f,194,69,199,72,202,77,203,81);
            Curve(g,Color.FromArgb(120,cat.Shade),.85f,106,108,110,114,116,117,123,118);
        }

        static void WhiteFace(Graphics g, Cat cat)
        {
            SoftOval(g,Color.FromArgb(150,cat.Shade),84,53,47,62);
            SoftOval(g,Color.FromArgb(154,cat.Shade),173,52,49,61);
            SoftOval(g,Color.FromArgb(215,cat.Cream),108,37,88,85);
            Color strand = Tint(cat.Fur,cat.Shade,.71f);
            Curve(g,strand,1.15f,132,38,137,40,141,45,143,50);
            Curve(g,Tint(cat.Fur,cat.Shade,.53f),.95f,158,35,155,42,155,46,154,49);
            Curve(g,strand,.9f,180,45,185,47,191,52,193,57);
            Curve(g,strand,1.15f,108,79,106,86,101,90,96,92);
            Curve(g,cat.Cream,1.65f,104,86,101,92,98,93,95,94);
            Curve(g,strand,1.0f,112,90,111,96,105,100,100,101);
            Curve(g,strand,.95f,192,89,198,94,205,96,209,96);
            Curve(g,cat.Cream,1.6f,191,95,196,99,200,101,203,101);
        }

        static void SmallEars(Graphics g, Cat cat)
        {
            using (GraphicsPath left = Shape(97,37,
                104,36,112,39,119,43, 113,45,106,48,103,53,
                102,47,100,41,97,37))
                Gradient(g,left,new PointF(98,36),new PointF(112,54),cat.Ear,cat.Fur);
            using (GraphicsPath right = Shape(183,42,
                189,39,199,35,205,36, 201,41,199,46,197,52,
                193,48,188,45,183,42))
                Gradient(g,right,new PointF(201,35),new PointF(190,54),cat.Ear,cat.Fur);
            Color rim = Tint(cat.Ear,cat.Ink,.28f);
            Curve(g,rim,.9f,99,37,107,38,111,40,116,42);
            Curve(g,rim,.9f,187,42,193,38,199,37,203,36);
            Curve(g,cat.Cream,1.2f,102,41,106,42,108,45,108,47);
            Curve(g,cat.Cream,.95f,105,40,108,41,111,42,112,44);
            Curve(g,cat.Cream,1.1f,200,39,197,42,195,45,194,47);
        }

        static void TallEars(Graphics g, Cat cat)
        {
            using (GraphicsPath left = Shape(101,21,
                108,22,119,32,123,37, 116,39,109,45,105,51,
                105,40,101,30,101,21))
                Gradient(g,left,new PointF(103,16),new PointF(111,52),cat.Ear,Tint(cat.Fur,cat.Ear,.18f));
            using (GraphicsPath right = Shape(181,37,
                185,31,195,21,201,20, 202,29,198,43,197,50,
                193,44,188,40,181,37))
                Gradient(g,right,new PointF(200,16),new PointF(190,51),cat.Ear,Tint(cat.Fur,cat.Ear,.18f));
            Color thread = cat.Cream;
            Curve(g,thread,1.15f,101,23,107,27,112,31,115,36);
            Curve(g,thread,1.05f,100,28,107,30,111,34,114,39);
            Curve(g,thread,1.25f,101,34,107,35,109,38,110,43);
            Curve(g,thread,.8f,98,20,97,17,97,16,96,14);
            Curve(g,thread,1.15f,201,24,196,27,191,32,188,36);
            Curve(g,thread,1.1f,201,29,195,31,191,35,189,39);
            Curve(g,thread,1.15f,201,35,196,36,193,40,192,43);
            Curve(g,thread,.75f,203,20,204,17,205,16,205,14);
        }

        static void DrawEye(Graphics g, Cat cat, float x, float y, float angle,
                            float lookX, float lookY, float open,float smile)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(x,y);
            g.RotateTransform(angle);
            float radius = cat.Longhair ? 15.4f : 15.0f;
            float height = (cat.Longhair ? 15.3f : 14.4f) * open;
            Color liner = C(0x4c4946);
            if (height < 1.4f)
            {
                float arch=Motion.Mix(2.7f,-8.4f,smile);
                Curve(g,liner,1.8f,-radius,0,-6,arch,6,arch,radius,-.4f);
                g.Restore(state);
                return;
            }

            using (GraphicsPath eye = Shape(-radius,0,
                -radius,-height*.60f,-8,-height,0,-height,
                8,-height,radius,-height*.68f,radius,0,
                radius-1,height*.65f,7,height+.3f,0,height,
                -7,height,-radius,height*.67f,-radius,0))
            {
                Gradient(g,eye,new PointF(0,-height-1),new PointF(0,height+1),
                    Tint(Cat.EyeTone(cat.Eyes,x>151,0),liner,.26f),Cat.EyeTone(cat.Eyes,x>151,0),Cat.EyeTone(cat.Eyes,x>151,1));
                GraphicsState iris = g.Save();
                g.SetClip(eye,CombineMode.Intersect);
                float px = lookX * 4.7f, py = lookY * 3.4f;
                float pupil = cat.Longhair ? 9.0f : 8.3f;
                Oval(g,C(0x30313b),px-pupil,py-10.8f,pupil*2,21.6f);
                Oval(g,Color.FromArgb(255,255,254,244),px-6.0f,py-8.0f,6.1f,5.6f);
                Oval(g,Color.FromArgb(188,255,250,235),px+2.6f,py+3.6f,2.5f,2.45f);
                Curve(g,Color.FromArgb(172,255,255,239),.85f,-5,height*.79f,-2,height*.9f,2,height*.91f,5,height*.81f);
                g.Restore(iris);
                Stroke(g,eye,liner,1.45f);
            }
            Curve(g,liner,1.18f,-radius+.3f,-.6f,-radius+1,-height*.66f,-6,-height-.2f,-.6f,-height);
            g.Restore(state);
        }

        static void DrawMuzzle(Graphics g, Cat cat, float noseY)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(0,noseY-97);
            SoftOval(g,cat.Cream,124,91,30,20);
            SoftOval(g,cat.Cream,148,91,30,20);
            Color mouth = C(cat.Pattern==Cat.Black?0xb2a1a0:0x796b65);
            using (GraphicsPath nose = Shape(145.2f,94,
                147,92.7f,149.2f,93.4f,151,93.7f, 153,93.1f,156,92.9f,157.3f,94.1f,
                157.6f,96,155.5f,97.9f,152.4f,100, 151.8f,100.7f,150.8f,100.8f,150.3f,100.1f,
                147.8f,97.6f,145.1f,96.1f,145.2f,94))
            {
                Gradient(g,nose,new PointF(152,93),new PointF(152,101),Tint(cat.Nose,cat.Cream,.25f),cat.Nose);
                Stroke(g,nose,mouth,.9f);
            }
            Oval(g,mouth,145.9f,94.5f,2.0f,1.4f);
            Oval(g,mouth,154.9f,94.4f,1.95f,1.4f);
            Curve(g,mouth,1.0f,151.5f,100.6f,151.5f,103,149,105.2f,146.1f,105.1f);
            Curve(g,mouth,1.0f,151.5f,100.6f,151.6f,103.3f,154.2f,105.1f,157.0f,104.8f);
            Color freckles = Tint(cat.Whisker,cat.Cream,.28f);
            Oval(g,freckles,134.3f,99.3f,1.6f,1.2f); Oval(g,freckles,138.5f,101.2f,1.35f,1.15f);
            Oval(g,freckles,165.0f,100.4f,1.5f,1.1f); Oval(g,freckles,168.3f,98.6f,1.5f,1.15f);
            Color whisker = cat.Whisker;
            Curve(g,whisker,.78f,126,100,116,97,104,95,95,96);
            Curve(g,whisker,.78f,126,103,115,102,104,104,95,107);
            Curve(g,whisker,.66f,128,106,120,108,112,111,106,115);
            Curve(g,whisker,.78f,176,99,187,96,199,94,207,95);
            Curve(g,whisker,.78f,176,102,187,101,198,102,207,105);
            Curve(g,whisker,.66f,174,105,184,107,191,110,198,114);
            g.Restore(state);
        }

        public static Bitmap Portrait(Cat cat, int size, Headwear hat = Headwear.Bare)
        {
            Bitmap image = new Bitmap(size,size,PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(image))
            {
                Setup(g);
                g.Clear(Color.Transparent);
                float span = cat.Longhair ? 151 : 146;
                float cy = cat.Longhair ? 67 : (hat == Headwear.Bare ? 79 : 71);
                g.TranslateTransform(size*.5f,size*.5f);
                g.ScaleTransform(size/span*Cat.HeadWidth/Cat.HeadHeight,size/span);
                g.TranslateTransform(-Cat.HeadX,-cy);
                DrawHead(g,cat,0,0,1);
                DrawHeadwear(g,cat,hat);
            }
            return image;
        }
    }
}
