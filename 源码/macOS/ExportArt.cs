// Build-time exporter for the original Windows artwork. The Mac app only reads the finished atlas.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace KeyboardCat
{
    internal sealed partial class Painter
    {
        const int Retina=2, ReachSteps=6, ClipFps=24;
        sealed class Atlas : IDisposable
        {
            public readonly Dictionary<string,int[]> Sprites=new Dictionary<string,int[]>();
            public readonly Dictionary<string,object> Clips=new Dictionary<string,object>();
            public readonly Dictionary<string,object> Map=new Dictionary<string,object>();
            public readonly List<object> Keys=new List<object>();
            readonly Dictionary<string,int> offsets=new Dictionary<string,int>();
            readonly FileStream output;
            readonly Bitmap canvas=new Bitmap(Width*Retina,Height*Retina,PixelFormat.Format32bppPArgb);
            readonly Graphics drawing;
            readonly string folder;
            public Atlas(string directory)
            {
                folder=directory; Directory.CreateDirectory(folder);
                drawing=Graphics.FromImage(canvas);
                output=File.Create(Path.Combine(folder,"Artwork.bin"));
            }
            unsafe Rectangle Bounds()
            {
                int x0=canvas.Width,y0=canvas.Height,x1=-1,y1=-1;
                BitmapData pixels=canvas.LockBits(new Rectangle(0,0,canvas.Width,canvas.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
                try
                {
                    for (int y=0;y<canvas.Height;y++)
                    {
                        byte* row=(byte*)pixels.Scan0+y*pixels.Stride;
                        int first=0,last=canvas.Width-1;
                        while (first<=last && row[first*4+3]==0) first++;
                        if (first>last) continue;
                        while (row[last*4+3]==0) last--;
                        x0=Math.Min(x0,first); x1=Math.Max(x1,last); y0=Math.Min(y0,y); y1=y;
                    }
                }
                finally { canvas.UnlockBits(pixels); }
                return x1<x0?Rectangle.Empty:Rectangle.FromLTRB(x0,y0,x1+1,y1+1);
            }
            readonly Dictionary<int,int[]> pigmentByOffset=new Dictionary<int,int[]>();
            void Draw(Action<Graphics> paint)
            {
                GraphicsState state=drawing.Save();
                Setup(drawing);drawing.Clear(Color.Transparent);drawing.ScaleTransform(Retina,Retina);
                paint(drawing);drawing.Restore(state);
            }
            int WriteUnique(byte[] data)
            {
                using(SHA256 sha=SHA256.Create()) {
                    string digest=Convert.ToBase64String(sha.ComputeHash(data));
                    int start;
                    if(!offsets.TryGetValue(digest,out start)) {
                        start=checked((int)output.Position);
                        output.Write(data,0,data.Length);offsets.Add(digest,start);
                    }
                    return start;
                }
            }
            int[] Store(Bitmap cut,Rectangle b)
            {
                using(MemoryStream stream=new MemoryStream()) {
                    cut.Save(stream,ImageFormat.Png);byte[] data=stream.ToArray();
                    return new[]{WriteUnique(data),data.Length,b.X,b.Y,b.Width,b.Height};
                }
            }
            public void Put(string name,Action<Graphics> paint,int square=0)
            {
                Draw(paint);
                Rectangle b=square>0?new Rectangle(0,0,square,square):Bounds();
                if(b.IsEmpty) return;
                using(Bitmap cut=canvas.Clone(b,PixelFormat.Format32bppPArgb)) Sprites.Add(name,Store(cut,b));
            }
            // A second pass changes only the red channel of fur. The premultiplied difference is the
            // amount of pigment visible through cuffs, props and antialiasing at each pixel.
            unsafe static byte[] Pigment(Bitmap neutral,Bitmap probe)
            {
                Rectangle rect=new Rectangle(0,0,neutral.Width,neutral.Height);
                BitmapData src=neutral.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
                BitmapData dye=probe.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
                byte[] weights=new byte[rect.Width*rect.Height];
                bool any=false;
                try {
                    for(int y=0;y<rect.Height;y++) {
                        byte* n=(byte*)src.Scan0+y*src.Stride;
                        byte* p=(byte*)dye.Scan0+y*dye.Stride;
                        int previous=0;
                        for(int x=0;x<rect.Width;x++,n+=4,p+=4) {
                            int amount=(Math.Max(0,n[2]-p[2])+1)/2;
                            weights[y*rect.Width+x]=unchecked((byte)(amount-previous));
                            previous=amount;any|=amount!=0;
                        }
                    }
                } finally {neutral.UnlockBits(src);probe.UnlockBits(dye);}
                if(!any) return null;
                using(MemoryStream output=new MemoryStream()) {
                    using(DeflateStream zip=new DeflateStream(output,CompressionLevel.Optimal,true))
                        zip.Write(weights,0,weights.Length);
                    return output.ToArray();
                }
            }
            public void PutFur(string name,Cat cat,Action<Graphics,Cat> paint)
            {
                Draw(g=>paint(g,cat));Rectangle b=Bounds();
                if(b.IsEmpty) return;
                using(Bitmap neutral=canvas.Clone(b,PixelFormat.Format32bppPArgb)) {
                    int[] entry=Store(neutral,b),mask;
                    if(!pigmentByOffset.TryGetValue(entry[0],out mask)) {
                        Draw(g=>paint(g,cat.PigmentProbe()));
                        using(Bitmap probe=canvas.Clone(b,PixelFormat.Format32bppPArgb)) {
                            byte[] pigment=Pigment(neutral,probe);
                            mask=pigment==null?null:new[]{WriteUnique(pigment),pigment.Length};
                        }
                        pigmentByOffset.Add(entry[0],mask);
                    }
                    Sprites.Add(name,mask==null?entry:new[]{entry[0],entry[1],b.X,b.Y,b.Width,b.Height,mask[0],mask[1]});
                }
            }
            public void Save()
            {
                var catalog=new { roles=Cat.All.Select(c=>new {id=c.Id,name=c.Name,shape=c.ShapeIndex,pattern=c.Pattern,eyes=c.Eyes,build=c.BuildIndex}).ToArray(),
                    builds=BodyBuild.All.Select(b=>new {name=b.Name,torsoWidth=b.TorsoWidth,torsoHeight=b.TorsoHeight,faceWidth=b.FaceWidth,faceHeight=b.FaceHeight}).ToArray(),
                    dyes=Cat.Dyes,shapes=Cat.Shapes,patterns=Cat.Patterns,eyes=Cat.EyeNames,clothes=Outfit.Garments.Select(Outfit.Name).ToArray(),
                    hats=Outfit.Hats.Select(Outfit.Name).ToArray(),boards=BoardNames,mice=MouseNames };
                var manifest=new { version=9,catalog=catalog,scale=Retina,steps=ReachSteps,fps=ClipFps,sprites=Sprites,clips=Clips,keys=Keys,keymap=Map,
                    tricks=WordTrick.All.Select(x=>new object[]{x.Word,x.Pinyin,x.Label,x.Duration,x.ShortLabel}).ToArray(),
                    idles=WordTrick.Idle.Select(x=>new object[]{x.Word,x.Duration}).ToArray() };
                JavaScriptSerializer json=new JavaScriptSerializer { MaxJsonLength=int.MaxValue };
                File.WriteAllText(Path.Combine(folder,"Artwork.json"),json.Serialize(manifest),new UTF8Encoding(false));
            }
            public void Dispose() { drawing.Dispose(); canvas.Dispose(); output.Dispose(); }
        }

        // Hardware key codes from HIToolbox/Events.h. Map the artwork's lower row to Control, Option and Command.
        void KeyboardAssets(Atlas a)
        {
            Cap[] prior=Keyboard.Caps.ToArray();
            Dictionary<int,string> relabel=new Dictionary<int,string>();
            foreach (var pair in new Dictionary<int,string>{{0x08,"Delete"},{0x0d,"Return"},{0x5b,"Opt"},{0xa4,"Cmd"},{0xa5,"Cmd"},{0xa3,"Opt"}})
                relabel[Array.IndexOf(prior,Keyboard.Find(pair.Key).Cap)]=pair.Value;
            for (int i=0;i<prior.Length;i++)
            {
                Cap old=prior[i]; string label;
                if (!relabel.TryGetValue(i,out label)) label=old.Legend;
                float x=old.X;
                if (old==Keyboard.Find(0xa3).Cap) x=Keyboard.Find(0x25).Cap.X;
                else if (old.Row==4 && (old.Legend=="←" || old.Legend=="↓" || old.Legend=="→")) x+=1;
                Keyboard.Caps[i]=new Cap(x,old.Row,old.Width,label,old.Tone);
                Cap cap=Keyboard.Caps[i]; PointF c=cap.Center;
                a.Keys.Add(new object[]{cap.Legend,c.X,c.Y,cap.OnLeft?0:1});

            }
            int[,] map={
                {0,'A'},{1,'S'},{2,'D'},{3,'F'},{4,'H'},{5,'G'},{6,'Z'},{7,'X'},{8,'C'},{9,'V'},
                {10,0xc0},{11,'B'},{12,'Q'},{13,'W'},{14,'E'},{15,'R'},{16,'Y'},{17,'T'},
                {18,'1'},{19,'2'},{20,'3'},{21,'4'},{22,'6'},{23,'5'},{24,0xbb},{25,'9'},{26,'7'},
                {27,0xbd},{28,'8'},{29,'0'},{30,0xdd},{31,'O'},{32,'U'},{33,0xdb},{34,'I'},{35,'P'},
                {36,0x0d},{37,'L'},{38,'J'},{39,0xde},{40,'K'},{41,0xba},{42,0xdc},{43,0xbc},{44,0xbf},
                {45,'N'},{46,'M'},{47,0xbe},{48,9},{49,0x20},{50,0xc0},{51,8},{53,0x1b},
                {54,0xa5},{55,0xa4},{56,0xa0},{57,0x14},{58,0x5b},{59,0xa2},{60,0xa1},{61,0xa3},{62,0xa2},{63,0xa2},
                {65,0xbe},{67,0x6a},{69,0x6b},{75,0x6f},{76,0x0d},{78,0x6d},{81,0xbb},
                {82,0x60},{83,0x61},{84,0x62},{85,0x63},{86,0x64},{87,0x65},{88,0x66},{89,0x67},{91,0x68},{92,0x69},
                {96,0x74},{97,0x75},{98,0x76},{99,0x72},{100,0x77},{101,0x78},{103,0x7a},{109,0x79},{111,0x7b},
                {115,0x24},{116,0x21},{117,0x2e},{118,0x73},{119,0x23},{120,0x71},{121,0x22},{122,0x70},
                {123,0x25},{124,0x27},{125,0x28},{126,0x26}
            };
            for (int i=0;i<map.GetLength(0);i++)
            {
                int mac=map[i,0],vk=map[i,1]; KeyHit hit=Keyboard.Find(vk);
                int idx=Array.IndexOf(prior,hit.Cap);
                string label=hit.Label==hit.Cap.Legend?Keyboard.Caps[idx].Legend:hit.Label;
                if(mac==63) label="fn";
                a.Map[mac.ToString()]=new object[]{idx,label,vk>='A' && vk<='Z'?vk:0};
            }
            for(int board=0;board<BoardNames.Length;board++) {
                a.Put("board/"+board,g=>DrawKeyboard(g,board));
                for(int i=0;i<Keyboard.Caps.Count;i++) {
                    Cap cap=Keyboard.Caps[i];
                    a.Put("press/"+board+"/"+i,g=>DrawCap(g,cap,1,board));
                }
            }
            for(int i=0;i<MouseNames.Length;i++) a.Put("mouse/"+i,g=>DrawMouse(g,i));
            for(int side=0;side<MouseButtons.Length;side++)
                for(int frame=0;frame<MouseFlashFrames;frame++)
                    a.Put("mouse/flash/"+side+"/"+frame,g=>DrawMouseFeedback(g,side,frame/(float)MouseFlashFrames));
        }

        static void Torso(Graphics g,Cat cat,GarmentFit fit,float breath)
        {
            GraphicsState fur=g.Save();
            if(fit.Kind!=Garment.Plain) fit.ClipUnderlayer(g);
            using(Matrix torso=TorsoMatrix()) g.MultiplyTransform(torso);
            DrawBody(g,cat,breath); if(cat.Longhair) DrawRuff(g,cat,breath);
            g.Restore(fur); DrawGarment(g,fit);
        }
        static void GarmentAssets(Atlas a,string sprite,GarmentFit fit)
        {
            // The outfit and its clipping mask are shared by every coat and body build.
            a.Put(sprite,g=>DrawGarment(g,fit));
            a.Put(sprite+"/cut",g=>{
                fit.ClipUnderlayer(g);
                g.FillRectangle(Brushes.White,60,65,190,148);
            });
        }
        static void TailLayer(Graphics g,Cat cat,float amount)
        {
            GraphicsState saved=g.Save();
            using(Matrix m=TorsoMatrix()) g.MultiplyTransform(m);
            DrawTail(g,cat,amount); g.Restore(saved);
        }
        static void FaceAssets(Atlas a,Cat cat,string prefix)
        {
            for(int i=1;i<=8;i++) a.Put(prefix+"temper/"+i,g=>DrawTemper(g,cat,i/8f));
            for(int pattern=0;pattern<Cat.Patterns.Length;pattern++)
            {
                Cat marked=new Cat("","",cat.ShapeIndex,pattern,0);
                PointF l=new PointF(132,169),r=new PointF(176,171);
                a.Put(prefix+"body/"+pattern,g=>Torso(g,marked,new GarmentFit(marked,Garment.Plain,0,l,r),0));
                for(int left=-3;left<=3;left++) for(int right=-3;right<=3;right++)
                    a.Put(prefix+"pattern/"+pattern+"/"+left+"_"+right,g=>DrawHeadBase(g,marked,left/3f,right/3f));
                for(int i=-12;i<=12;i++) a.Put(prefix+"tail/"+pattern+"/"+(i+12),g=>TailLayer(g,marked,i));
                a.Put(prefix+"muzzle/"+pattern,g=>DrawMuzzle(g,marked,marked.Longhair?97:102));
                for(int eye=0;eye<Cat.EyeNames.Length;eye++) {
                    Cat look=new Cat("","",cat.ShapeIndex,pattern,eye);
                    using(Bitmap portrait=Portrait(look,128))
                        a.Put(prefix+"portrait/"+pattern+"/"+eye,g=>{g.ResetTransform();g.DrawImageUnscaled(portrait,0,0);},128);
                }
            }
            for(int eye=0;eye<Cat.EyeNames.Length;eye++)
            {
                Cat look=new Cat("","",cat.ShapeIndex,0,eye);
                string eyePrefix=prefix+"eyes/"+eye+"/";
                for(int y=-2;y<=2;y++) for(int x=-2;x<=2;x++)
                    a.Put(eyePrefix+"head/"+(x+2)+"_"+(y+2),g=>DrawGaze(g,look,x*.5f,y*.5f,1,0));
                foreach(float open in new[]{.24f,.50f,.76f})
                    a.Put(eyePrefix+"lid/"+(int)Math.Round(open*100),g=>DrawGaze(g,look,0,0,open,0));
                a.Put(eyePrefix+"blink",g=>DrawGaze(g,look,0,0,0,0));
                a.Put(eyePrefix+"smile",g=>DrawGaze(g,look,0,0,0,1));
            }
            foreach(Headwear hat in Outfit.Hats)
                if(hat!=Headwear.Bare) a.Put(prefix+"hat/"+(int)hat,g=>DrawHeadwear(g,cat,hat));
        }
        static void ArmAssets(Atlas a,Cat cat,Garment garment,string prefix)
        {
            PointF left=new PointF(132,169),right=new PointF(176,171);
            GarmentFit rest=new GarmentFit(cat,garment,0,left,right);
            if(garment!=Garment.Plain) GarmentAssets(a,prefix+"body",rest);
            for(int i=1;i<=10;i++) a.PutFur(prefix+"privacy/"+i,cat,(g,fur)=>DrawPrivacyArms(g,fur,garment,i*.1f));
            for(int hand=0;hand<2;hand++)
            {
                bool isLeft=hand==0;
                PointF neutral=isLeft?left:right;
                string arm=prefix+"arm/"+hand+"/";
                a.PutFur(arm+"rest",cat,(g,fur)=>DrawArm(g,fur,isLeft,neutral,rest,0,0));
                Action<string,PointF> addReach=(target,contact)=>{
                    PointF goal=cat.ToFigure(contact);
                    for(int step=1;step<=ReachSteps;step++)
                    {
                        PointF paw=Motion.Mix(neutral,goal,step/(float)ReachSteps);
                        GarmentFit fit=new GarmentFit(cat,garment,0,isLeft?paw:left,isLeft?right:paw);
                        a.PutFur(arm+target+"/"+step,cat,(g,fur)=>DrawArm(g,fur,isLeft,paw,fit,0,0));
                    }
                };
                for(int target=0;target<Keyboard.Caps.Count;target++) {
                    Cap cap=Keyboard.Caps[target];
                    if(cap.Legend=="Space" || cap.OnLeft==isLeft) addReach(target.ToString(),cap.Center);
                }
                if(isLeft) {
                    for(int side=0;side<MouseButtons.Length;side++)
                        for(int depth=0;depth<=2;depth++)
                            addReach("mouse/"+side+"/"+depth,MousePawTarget(side,depth*.5f));
                }
            }
        }
        partial struct GarmentFit
        {
            // The torso moves less than the wrists. Share fits within half a design pixel per shoulder axis.
            public GarmentFit AtlasTorso(out int key,out float rasterBreath)
            {
                int lx=(int)Math.Round(leftPull.X),rx=(int)Math.Round(rightPull.X);
                int ly=(int)Math.Round(leftPull.Y),ry=(int)Math.Round(rightPull.Y);
                int breathing=(int)Math.Round(breath*2);
                rasterBreath=breathing*.5f;
                key=((((lx+4)*9+rx+4)*4+ly+1)*4+ry+1)*5+breathing+2;
                PointF left=new PointF(132+lx/.052f,169+rasterBreath+ly/.035f);
                PointF right=new PointF(176+rx/.052f,171+rasterBreath+ry/.035f);
                return new GarmentFit(cat,Kind,rasterBreath,left,right);
            }
        }

        static void ClipAssets(Atlas a,Cat cat,Garment garment,string prefix,WordTrick rule)
        {
            string clip=prefix+"clip/"+rule.Word;
            List<float[]> heads=new List<float[]>();
            Motion motion=new Motion(); motion.Tricks.Play(rule,1);
            int count=(int)Math.Ceiling(rule.Duration*ClipFps);
            for(int i=0;i<count;i++)
            {
                double time=1+i/(double)ClipFps;
                float breath=(float)Math.Sin(time*2.05)*(cat.Longhair?1:.62f);
                PointF l=new PointF(132,169+breath),r=new PointF(176,171+breath);
                TrickPose p=TrickPose.Sample(motion.Tricks,cat,time,breath,ref l,ref r);
                GarmentFit fit=p.FitArms(cat,garment,breath,ref l,ref r);
                int posture=0;
                if(garment!=Garment.Plain) {
                    float rasterBreath;
                    GarmentFit torso=fit.AtlasTorso(out posture,out rasterBreath);
                    string sprite=prefix+"posture/"+posture;
                    if(!a.Sprites.ContainsKey(sprite)) GarmentAssets(a,sprite,torso);
                }
                string frame=clip+"/"+i;
                a.PutFur(frame+"/back",cat,(g,fur)=>{
                    Footwork(g,fur,p);
                    if(!p.InFront(1)) DrawArm(g,fur,true,l,fit,p.LeftTurn,p.LeftPads);
                    if(!p.InFront(2)) DrawArm(g,fur,false,r,fit,p.RightTurn,p.RightPads);
                    if(p.Kind==TrickKind.Love) Heart(g,152,159+p.Beat,1.03f+.07f*p.Beat,0,p.Weight);
                });
                using(Matrix root=new Matrix()) {
                    root.Translate(Cat.SeatX,Cat.SeatY);root.Scale(cat.WidthScale,cat.HeightScale);
                    root.Translate(-Cat.SeatX,-Cat.SeatY);
                    root.Translate(Cat.SeatX+p.BodyX,Cat.SeatY+p.BodyY);
                    root.Rotate(p.BodyTurn);root.Scale(1+p.Wide,1+p.Tall);root.Translate(-Cat.SeatX,-Cat.SeatY);
                    float[] body=root.Elements;
                    root.Translate(Cat.HeadX+p.HeadX,Cat.HeadY+Cat.HeadDrop+breath+p.HeadY);
                    root.Rotate(p.HeadTurn);root.Scale(Cat.HeadWidth,Cat.HeadHeight);root.Translate(-Cat.HeadX,-Cat.HeadY);
                    float[] m=root.Elements;
                    heads.Add(new[]{m[0],m[1],m[2],m[3],m[4],m[5],1-p.Squint,p.Smile,
                        body[0],body[1],body[2],body[3],body[4],body[5],
                        p.Tail,p.TailTurn,p.Weight,p.EarLeft,p.EarRight,p.Still,p.Focus,p.GazeX,p.GazeY,p.Air,(float)posture});
                }
                a.PutFur(frame+"/front",cat,(g,fur)=>{
                    FigureSpace(g,cat); p.MoveBody(g);
                    ActionForeground(g,fur,p,l,r,fit);
                });
            }
            a.Clips.Add(clip,heads);
        }

        public void ExportMac(string folder)
        {
            using(Atlas a=new Atlas(folder))
            {
                KeyboardAssets(a);
                foreach(Cat cat in new[]{new Cat("short","短毛",0,0,0),new Cat("long","长毛",1,0,1)})
                {
                    string prefix=cat.Id+"/"; FaceAssets(a,cat,prefix);
                    foreach(Garment garment in Outfit.Garments)
                    {
                        string outfit=prefix+(int)garment+"/";
                        ArmAssets(a,cat,garment,outfit);
                        foreach(WordTrick rule in WordTrick.All.Concat(WordTrick.Idle))
                            ClipAssets(a,cat,garment,outfit,rule);
                    }
                    Console.WriteLine(cat.Name+" exported");
                }
                a.Save();
            }
        }
    }
    class ExportArt
    {
        static void Main(string[] args) {using(Painter p=new Painter()) p.ExportMac(args[0]);}
    }
}
