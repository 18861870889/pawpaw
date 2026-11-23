// Build the two native platforms' shared print artwork from the Windows vector originals.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace KeyboardCat
{
    internal static class ExportStudio
    {
        public static void Main(string[] args)
        {
            string dir=args[0];
            File.WriteAllBytes(Path.Combine(dir,"PrintInks.rgb"),InkLook.Inks);
            int w=CoverArt.Width*2,h=CoverArt.Height*2;
            using(Bitmap atlas=new Bitmap(w*3,h*2,PixelFormat.Format32bppPArgb))
            using(Graphics a=Graphics.FromImage(atlas)) {
                for(int i=0;i<6;i++) using(Bitmap panel=new Bitmap(w,h))
                using(Graphics g=Graphics.FromImage(panel)) {
                    g.ScaleTransform(2,2);CoverArt.Background(g,i);a.DrawImageUnscaled(panel,i%3*w,i/3*h);
                }
                atlas.Save(Path.Combine(dir,"StudioCovers.png"),ImageFormat.Png);
            }
            const int side=224;
            using(Bitmap atlas=new Bitmap(7*side,8*side,PixelFormat.Format32bppPArgb))
            using(Graphics g=Graphics.FromImage(atlas)) {
                for(int look=0;look<2;look++) for(int group=0;group<4;group++) for(int item=0;item<CoverArt.Counts[group];item++)
                    using(Bitmap swatch=CoverArt.Item(group,item,side,look==0)) g.DrawImageUnscaled(swatch,item*side,(look*4+group)*side);
                atlas.Save(Path.Combine(dir,"StudioItems.png"),ImageFormat.Png);
            }
            using(Bitmap mark=new Bitmap(72,64,PixelFormat.Format32bppPArgb))
            using(Graphics g=Graphics.FromImage(mark)) {
                CoverArt.Mark(g,0,0,2);mark.Save(Path.Combine(dir,"StudioMark.png"),ImageFormat.Png);
            }
        }
    }
}
