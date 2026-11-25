using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KeyboardCat
{
    internal sealed partial class Painter
    {
        static TrickPose At(WordTrick rule,Cat cat,double seconds,out PointF left,out PointF right)
        {
            var player=new TrickPlayer();
            player.Play(rule,1);
            left=new PointF(132,169);right=new PointF(176,171);
            return TrickPose.Sample(player,cat,1+seconds,0,ref left,ref right);
        }
        static void Check(bool pass,string reason)
        {
            if(!pass) throw new InvalidOperationException(reason);
        }
        static void ScreenContacts(Cat cat)
        {
            PointF l,r;
            TrickPose crouch=At(WordTrick.TripleTap,cat,.4,out l,out r);
            TrickPose air=At(WordTrick.TripleTap,cat,1.08,out l,out r);
            TrickPose land=At(WordTrick.TripleTap,cat,1.77,out l,out r);
            Check(crouch.BodyY>6 && crouch.Tall<-.1 && air.Air>.94 && air.BodyY<-26 && land.BodyY>4 && land.Air==0,
                "The screen press must crouch, jump clear of the keyboard and land");
            using(var surface=new Bitmap(320,260)) using(Graphics g=Graphics.FromImage(surface)) {
                for(int i=0;i<TrickPose.TouchTimes.Length;i++) foreach(Garment outfit in Outfit.Garments) {
                    TrickPose p=At(WordTrick.TripleTap,cat,TrickPose.TouchTimes[i],out l,out r);
                    GarmentFit fit=p.FitArms(cat,outfit,0,ref l,ref r);
                    bool left=p.PalmLeft>p.PalmRight;
                    Check(p.Air>.68 && Math.Max(p.PalmLeft,p.PalmRight)>.99,"Each contact needs a forward-facing pad in flight");
                    PointF hand=TrickPose.TurnPaw(left?l:r,fit.Shoulder(left),left?p.LeftTurn:p.RightTurn);
                    PointF shift=p.ArmOffset(left);
                    PointF[] point={new PointF(hand.X+shift.X,hand.Y+shift.Y)};
                    GraphicsState saved=FigureSpace(g,cat);p.MoveBody(g);
                    using(Matrix matrix=g.Transform) matrix.TransformPoints(point);
                    PointF button=TrickPose.GlassButtons[i];
                    Check(Distance(point[0],button)<.08f,"A sleeved paw missed a glass button");
                    p.GlassSpace(g,cat);
                    PointF[] glass={button};using(Matrix matrix=g.Transform) matrix.TransformPoints(glass);
                    Check(Distance(glass[0],button)<.08f,"The glass drifted with the jumping cat");
                    g.Restore(saved);
                }
            }
        }
        static void DistinctPhrases(Cat cat)
        {
            PointF l,r;float min=100,max=-100,feet=0;int bites=0;bool high=false;
            for(int i=0;i<170;i++) {
                double t=i/50.0;
                TrickPose dance=At(WordTrick.Dance,cat,t,out l,out r);
                min=Math.Min(min,dance.BodyX);max=Math.Max(max,dance.BodyX);feet=Math.Max(feet,Math.Abs(dance.LeftStep-dance.RightStep));
                TrickPose rice=At(WordTrick.RiceBowl,cat,t,out l,out r);
                if(rice.Beat>.9f && !high) bites++;
                high=rice.Beat>.9f;
                if(high) Check(rice.Spoon.Y<rice.Prop.Y-22,"A bite must lift the spoon above the bowl");
            }
            Check(min<-20 && max>20 && feet>.95f && bites==2,"Side-step weight changes and two separate bites");
            PointF l2,r2;TrickPose cone=At(WordTrick.SweetCone,cat,1.39,out l,out r);
            At(WordTrick.Dance,cat,1.39,out l2,out r2);
            Check(Distance(l,l2)+Distance(r,r2)>26 && cone.Focus>.5f,"The cone routine must have its own arms and focus");
        }
        public static void Main()
        {
            Check(Cat.All.Length==11 && BodyBuild.All.Length==5,"Public presets and physiques");
            foreach(string id in new[]{"black-cat","white-cat","orange-tabby","calico"}) Check(Cat.Find(id).Id==id,"Missing "+id);
            foreach(Cat cat in new[]{Cat.All[0],Cat.All[1]}) {
                for(int i=0;i<BodyBuild.All.Length;i++) ScreenContacts(cat.WithBuild(i));
                DistinctPhrases(cat);
            }
            Console.WriteLine("Both coat lengths, all five physiques, all garments, three screen contacts, grounded steps and two bites passed.");
        }
    }
}
