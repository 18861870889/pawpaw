using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KeyboardCat
{
    internal sealed partial class Painter
    {
        static void DrawTemper(Graphics g,Cat cat,float amount)
        {
            if(amount<=0) return;
            float y=cat.EyeLine-11, f=amount;
            Color brow=Faded(Tint(cat.Ink,C(0x573c37),.70f),f);
            Curve(g,brow,2.8f,113,y-5*f,120,y-6*f,128,y+3*f,140,y+5*f);
            Curve(g,brow,2.8f,162,y+5*f,174,y+3*f,181,y-6*f,189,y-7*f);
            SoftOval(g,Color.FromArgb((int)(95*f),238,119,94),100,y+18,22,8);
            SoftOval(g,Color.FromArgb((int)(95*f),238,119,94),182,y+18,22,8);
            GraphicsState mark=g.Save();
            g.TranslateTransform(204,cat.Longhair?62:67);g.ScaleTransform(.72f+.32f*f,.72f+.32f*f);
            Color red=Faded(C(0xc04e42),f);
            for(int i=0;i<4;i++) {
                Curve(g,red,2.4f,0,-8,-.2f,-3.5f,-1,-2.4f,-5,-2);
                g.RotateTransform(90);
            }
            g.Restore(mark);
        }

        static void DrawPrivacyArms(Graphics g,Cat cat,Garment garment,float cover)
        {
            float eyeY=Cat.HeadY+Cat.HeadDrop+(cat.EyeLine-Cat.HeadY)*Cat.HeadHeight;
            PointF left=Motion.Mix(new PointF(132,169),new PointF(122,eyeY+1),cover);
            PointF right=Motion.Mix(new PointF(176,171),new PointF(180,eyeY-1),cover);
            GarmentFit fit=new GarmentFit(cat,garment,0,left,right);
            for(int i=0;i<2;i++) {
                bool isLeft=i==0;PointF tip=isLeft?left:right;
                float turn=(isLeft?-158:158)*cover;
                float elbowX=(isLeft?-7:7)*cover,elbowY=16*cover;
                PointF before=TrickPose.TurnPaw(new PointF(tip.X-elbowX,tip.Y-elbowY),fit.Shoulder(isLeft),-turn);
                GraphicsState elbow=g.Save();g.TranslateTransform(elbowX,elbowY);
                DrawArm(g,cat,isLeft,before,fit,turn,0);g.Restore(elbow);
                GraphicsState paw=g.Save();g.TranslateTransform(tip.X,tip.Y);
                g.RotateTransform(isLeft?-17:17);
                float size=Motion.Smooth(cover)*1.02f;
                if(size>.03f) {
                    g.ScaleTransform(size,size);
                    using(GraphicsPath mitten=Shape(-17,2,-19,-6,-16,-13,-10,-13,-6,-16,-1,-17,3,-15,
                        9,-17,14,-13,15,-9,21,-5,20,4,16,9,11,15,1,17,-8,14,-14,12,-18,8,-17,2)) {
                        Gradient(g,mitten,new PointF(0,-16),new PointF(1,16),cat.Cream,cat.Fur,cat.Shade);
                        Stroke(g,mitten,cat.Ink,1.35f);
                    }
                    Color toe=Tint(cat.Ink,cat.Fur,.35f);
                    Curve(g,toe,1.1f,-8,-12,-7,-11,-6,-9,-6,-7);
                    Curve(g,toe,1.1f,3,-14,5,-12,5,-11,5,-8);
                }
                g.Restore(paw);
            }
        }
    }
}
