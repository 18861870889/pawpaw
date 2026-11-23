using System;

namespace KeyboardCat
{
    internal enum Garment { Plain, StrawberryDress, SailorShirt, Raincoat, ForestVest, MoonPajamas, KnitShirt }
    internal enum Headwear { Bare, BerryBow, SailorCap, FrogHat, Beret, DaisyClip }

    internal struct Outfit
    {
        public readonly Garment Clothes;
        public readonly Headwear Hat;
        public static readonly Garment[] Garments = (Garment[])Enum.GetValues(typeof(Garment));
        public static readonly Headwear[] Hats = (Headwear[])Enum.GetValues(typeof(Headwear));
        static readonly string[] garmentNames = { "不穿衣服", "草莓背带裙", "海盐水手服", "蜂蜜雨衣", "森林工装马甲", "星月睡衣", "奶油条纹衫" };
        static readonly string[] hatNames = { "不戴头饰", "莓果蝴蝶结", "水手帽", "青蛙帽", "画家贝雷帽", "小雏菊发夹" };

        public Outfit(Garment clothes, Headwear hat)
        {
            Clothes = clothes;
            Hat = hat;
        }

        public static string Name(Garment item) { return garmentNames[(int)item]; }
        public static string Name(Headwear item) { return hatNames[(int)item]; }
        public bool IsPlain { get { return Clothes == Garment.Plain && Hat == Headwear.Bare; } }

        public string Encode() { return Clothes.ToString() + "," + Hat.ToString(); }

        public static Outfit Decode(string value)
        {
            string[] parts = value.Split(',');
            Garment clothes;
            Headwear hat = Headwear.Bare;
            if (!Enum.TryParse(parts[0],true,out clothes) || !Enum.IsDefined(typeof(Garment),clothes))
                clothes = Garment.Plain;
            if (parts.Length > 1 && (!Enum.TryParse(parts[1],true,out hat) || !Enum.IsDefined(typeof(Headwear),hat)))
                hat = Headwear.Bare;
            return new Outfit(clothes,hat);
        }
    }
}
