using System;
using System.Drawing;

namespace KeyboardCat
{
    internal enum Coat { SilverShaded, WhiteLonghair }

    internal sealed class BodyBuild
    {
        public readonly string Name;
        public readonly float TorsoWidth,TorsoHeight,FaceWidth,FaceHeight;
        BodyBuild(string name,float torsoWidth,float torsoHeight,float faceWidth,float faceHeight)
        {Name=name;TorsoWidth=torsoWidth;TorsoHeight=torsoHeight;FaceWidth=faceWidth;FaceHeight=faceHeight;}

        // Keep the paws in the same rig. The torso grows from the seat and the face around the eye line.
        public static readonly BodyBuild[] All={
            new BodyBuild("匀称",1,1,1,1),new BodyBuild("娇小",.80f,.83f,.85f,.87f),
            new BodyBuild("圆润",1.24f,.90f,1.095f,.93f),new BodyBuild("修长",.82f,1.14f,.90f,1),
            new BodyBuild("壮实",1.19f,1.09f,1.035f,.985f)
        };
        public override string ToString() {return Name;}
    }

    internal sealed class Cat
    {
        public readonly string Id, Name;
        public readonly Coat Coat;
        public readonly float WidthScale, HeightScale;
        public readonly Color Fur, Shade, Ink, Mark, Cream, Ear, Iris, IrisLight, Nose, Whisker;

        public readonly int ShapeIndex, Pattern, Eyes, BuildIndex;
        public BodyBuild Build {get {return BodyBuild.All[BuildIndex];}}
        public const int Black=11, White=12;
        // Channel shifts in units of 1/256 of the local fur pigment. Props and clothes have no pigment.
        public static readonly int[][] Dyes={
            new[]{0,0,0},new[]{0,75,152},new[]{0,0,0},new[]{-3,-2,-3},new[]{0,0,0},new[]{0,0,0},
            new[]{0,0,0},new[]{0,0,0},new[]{0,0,0},new[]{0,0,0},new[]{0,0,0},
            new[]{262,257,241},new[]{-8,-10,-13}
        };
        static readonly int[] probe={256,0,0};
        public Cat PigmentProbe() {return new Cat(Id,Name,ShapeIndex,Pattern,Eyes,BuildIndex,probe);}
        public Cat WithBuild(int index)
        {return index==BuildIndex?this:new Cat(Id,Name,ShapeIndex,Pattern,Eyes,index);}
        static Color Dye(int hex,int pigment,int[] dye)
        {
            Color c=Rgb(hex);
            return Color.FromArgb(Channel(c.R,pigment,dye[0]),Channel(c.G,pigment,dye[1]),Channel(c.B,pigment,dye[2]));
        }
        static int Channel(int baseValue,int pigment,int shift)
        {return Math.Max(0,Math.Min(255,baseValue-(int)Math.Round(pigment*shift/256.0)));}
        public static readonly string[] Shapes={"圆脸短毛","蓬松长毛"};
        public static readonly string[] Patterns={"原色","橘虎斑","奶牛花","三花","可可重点色","蓝灰绒","银色虎斑","海豹双色","棕色经典斑","狸花鱼骨纹","奶油渐层","墨黑纯色","雪白纯色"};
        public static readonly string[] EyeNames={"翡翠绿","湖水蓝","琥珀金","葡萄紫","鸳鸯瞳"};
        static readonly int[,] eyeTones={{0x91b18b,0xdbe7bd},{0x89b1c8,0xd3e9f1},{0xd59b57,0xffdf95},{0x9d85b8,0xe9d5f0}};
        public Cat(string id,string name,int shape,int pattern,int eyes,int build=0)
            : this(id,name,shape,pattern,eyes,build,Dyes[pattern]) {}
        Cat(string id,string name,int shape,int pattern,int eyes,int build,int[] dye)
        {
            BuildIndex=build;
            Id=id; Name=name; ShapeIndex=shape; Pattern=pattern; Eyes=eyes;
            Coat=shape==1?Coat.WhiteLonghair:Coat.SilverShaded;
            WidthScale=Longhair?1.02f:.90f; HeightScale=Longhair?1:.86f;
            Fur=Dye(Longhair?0xf9f5ec:0xe6e5df,173,dye); Shade=Dye(Longhair?0xdfd9d0:0xbfc2c3,153,dye);
            Ink=Dye(Longhair?0x7a7373:0x68676a,69,dye); Mark=Dye(Longhair?0xbab3aa:0x717679,83,dye);
            Cream=Dye(Longhair?0xfffcf6:0xfcf8ee,168,dye); Ear=Rgb(Longhair?0xe0bbb3:0xc3a99e);
            Iris=EyeTone(eyes,false,0); IrisLight=EyeTone(eyes,false,1);
            Nose=Rgb(Longhair?0xd79d92:0xd39583); Whisker=Rgb(pattern==Black?0xb4b1b3:Longhair?0xada49d:0x9c9a95);
        }

        public static Color EyeTone(int eyes,bool right,int stop)
        {
            int tone=eyes==4?(right?2:1):eyes;
            return Rgb(eyeTones[tone,stop]);
        }
        public bool IsCustom { get { return ValidCustomId(Id); } }
        public static bool ValidCustomId(string id)
        {
            Guid guid;
            return id!=null && id.StartsWith("user-",StringComparison.Ordinal) && Guid.TryParseExact(id.Substring(5),"N",out guid);
        }
        public static bool ValidName(string name)
        {
            return !String.IsNullOrWhiteSpace(name) && name.Length<=24 && !Array.Exists(name.ToCharArray(),Char.IsControl);
        }
        public string MenuName
        {
            get {return IsCustom && Array.Exists(All,c=>String.Equals(c.Name,Name,StringComparison.OrdinalIgnoreCase))?Name+"（自建）":Name;}
        }
        public override string ToString() { return MenuName; }

        public bool Longhair { get { return Coat == Coat.WhiteLonghair; } }

        // Both figures sit on the same baseline; the keyboard stays in screen coordinates.
        public const float SeatX = 153f, SeatY = 199f;
        public const float HeadX = 151f, HeadY = 90f, HeadDrop = 12f;
        public const float HeadWidth = 1.24f, HeadHeight = 1.12f;
        public float EyeLine { get { return Longhair ? 78 : 84; } }
        public PointF ToFigure(PointF scene)
        {
            return new PointF(SeatX + (scene.X - SeatX) / WidthScale,
                              SeatY + (scene.Y - SeatY) / HeightScale);
        }
        public PointF EyeCenter
        {
            get
            {
                float y = HeadY + HeadDrop + (EyeLine - HeadY) * HeadHeight;
                return new PointF(SeatX + (HeadX - SeatX) * WidthScale,
                                  SeatY + (y - SeatY) * HeightScale);
            }
        }

        public static Color Rgb(int hex) { return Color.FromArgb(unchecked((int)0xff000000) | hex); }

        public static readonly Cat[] All = {
            new Cat("british-shorthair","英国短毛猫",0,5,2,2),
            new Cat("ragdoll","布偶猫",1,7,1),
            new Cat("american-shorthair","美国短毛猫",0,6,0,0),
            new Cat("siamese","暹罗猫",0,4,1,3),
            new Cat("maine-coon","缅因猫",1,8,2,4),
            new Cat("chinese-domestic","中华田园猫",0,9,0,0),
            new Cat("persian","波斯猫",1,10,2,2),
            new Cat("black-cat","黑猫",0,Black,2,3),
            new Cat("white-cat","白猫",1,White,1,1),
            new Cat("orange-tabby","橘猫",0,1,2,2),
            new Cat("calico","三花猫",0,3,0,0)
        };

        public static Cat Find(string id)
        {
            return Array.Find(All, delegate(Cat cat) { return cat.Id == id; }) ?? All[0];
        }
    }
}
