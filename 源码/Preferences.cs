using System;
using System.Drawing;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace KeyboardCat
{
    internal sealed class Preferences
    {
        public Cat Cat = Cat.All[0];
        public int ScaleIndex = 1;
        public int Board, MouseStyle;
        public readonly List<Cat> Roles=new List<Cat>(Cat.All);
        public const int MaxTransparency = 80;
        public int Transparency;
        public bool PrintLook=true;
        public bool Labels = true, Paused, Locked, WordTriggers = true;
        public Point? Position;
        const string OutfitPrefix = "outfit.";
        readonly Dictionary<string,Outfit> outfits=new Dictionary<string,Outfit>(StringComparer.Ordinal);

        public void PutRole(Cat cat)
        {
            int index=Roles.FindIndex(c=>c.Id==cat.Id);
            if(index<0) Roles.Add(cat); else Roles[index]=cat;
        }
        public void RemoveRole(Cat cat)
        {
            if(!cat.IsCustom) return;
            Roles.RemoveAll(c=>c.Id==cat.Id);
            outfits.Remove(cat.Id);
        }
        static Cat ReadRole(string id,string value)
        {
            if(!Cat.ValidCustomId(id)) return null;
            string[] parts=value.Split(',');
            int shape,pattern,eyes;
            if(parts.Length!=4 || !ReadIndex(parts[0],Cat.Shapes.Length,out shape) ||
                !ReadIndex(parts[1],Cat.Patterns.Length,out pattern) || !ReadIndex(parts[2],Cat.EyeNames.Length,out eyes)) return null;
            try {
                string name=new UTF8Encoding(false,true).GetString(Convert.FromBase64String(parts[3])).Trim();
                return Cat.ValidName(name)?new Cat(id,name,shape,pattern,eyes):null;
            }
            catch(FormatException) {return null;}
            catch(DecoderFallbackException) {return null;}
        }
        static bool ReadIndex(string text,int count,out int index)
        { return int.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out index) && index>=0 && index<count; }

        public Outfit OutfitFor(Cat cat)
        {
            Outfit result;
            return outfits.TryGetValue(cat.Id,out result) ? result : new Outfit();
        }
        public Outfit CurrentOutfit
        {
            get { return OutfitFor(Cat); }
            set { outfits[Cat.Id] = value; }
        }
        public static readonly float[] Scales = { .70f, .93f, 1.24f };
        public static readonly string[] SizeNames = { "小", "标准", "大" };
        public static string DefaultFile
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KeyboardCat","settings.ini"); }
        }

        public static Preferences Load(string path)
        {
            Preferences p=new Preferences();
            try
            {
                if(!File.Exists(path)) return p;
                int? x=null,y=null;
                string selected=null;
                Dictionary<string,int> builds=new Dictionary<string,int>(StringComparer.Ordinal);
                foreach(string line in File.ReadAllLines(path))
                {
                    int cut=line.IndexOf('='),number;
                    if(cut<1) continue;
                    string name=line.Substring(0,cut), value=line.Substring(cut+1).Trim();
                    if(name.StartsWith("build.",StringComparison.Ordinal)) {
                        if(ReadIndex(value,BodyBuild.All.Length,out number)) builds[name.Substring(6)]=number;
                        continue;
                    }
                    if(name.StartsWith(OutfitPrefix,StringComparison.Ordinal))
                    {
                        string id=name.Substring(OutfitPrefix.Length);
                        if(Cat.ValidCustomId(id) || Array.Exists(Cat.All,c=>c.Id==id)) p.outfits[id]=Outfit.Decode(value);
                        continue;
                    }
                    if(name.StartsWith("role.",StringComparison.Ordinal)) {
                        Cat cat=ReadRole(name.Substring(5),value);
                        if(cat!=null) p.PutRole(cat);
                        continue;
                    }
                    switch(name)
                    {
                        case "cat": selected=value; break;
                        case "look": if(value=="print" || value=="classic") p.PrintLook=value=="print"; break;
                        case "board": if(ReadIndex(value,Painter.BoardNames.Length,out number)) p.Board=number; break;
                        case "mouse": if(ReadIndex(value,Painter.MouseNames.Length,out number)) p.MouseStyle=number; break;
                        case "size":
                            if(int.TryParse(value,out number) && number>=0 && number<Scales.Length) p.ScaleIndex=number;
                            break;
                        case "transparency":
                            if(int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out number) && number>=0 && number<=MaxTransparency)
                                p.Transparency=number;
                            break;
                        case "labels": if(value=="0" || value=="1") p.Labels=value=="1"; break;
                        case "locked": if(value=="0" || value=="1") p.Locked=value=="1"; break;
                        case "paused": if(value=="0" || value=="1") p.Paused=value=="1"; break;
                        case "wordTriggers": if(value=="0" || value=="1") p.WordTriggers=value=="1"; break;
                        case "x": if(Coordinate(value,out number)) x=number; break;
                        case "y": if(Coordinate(value,out number)) y=number; break;
                    }
                }
                for(int i=0;i<p.Roles.Count;i++) {
                    int build;
                    if(builds.TryGetValue(p.Roles[i].Id,out build)) p.Roles[i]=p.Roles[i].WithBuild(build);
                }
                p.Cat=p.Roles.Find(c=>c.Id==selected) ?? Cat.All[0];
                if(x.HasValue && y.HasValue) p.Position=new Point(x.Value,y.Value);
            }
            catch(IOException) { }
            catch(UnauthorizedAccessException) { }
            return p;
        }
        static bool Coordinate(string text,out int n)
        {
            return int.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out n) && n>=-1000000 && n<=1000000;
        }

        public void Save(string path)
        {
            string tmp=path+".new";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                StringBuilder lines=new StringBuilder();
                lines.AppendLine("cat="+Cat.Id).AppendLine("size="+ScaleIndex)
                    .AppendLine("look="+(PrintLook?"print":"classic"))
                    .AppendLine("transparency="+Transparency.ToString(CultureInfo.InvariantCulture))
                    .AppendLine("labels="+(Labels?"1":"0")).AppendLine("paused="+(Paused?"1":"0"))
                    .AppendLine("locked="+(Locked?"1":"0")).AppendLine("wordTriggers="+(WordTriggers?"1":"0"));
                if(Position.HasValue) lines.AppendLine("x="+Position.Value.X.ToString(CultureInfo.InvariantCulture))
                    .AppendLine("y="+Position.Value.Y.ToString(CultureInfo.InvariantCulture));
                lines.AppendLine("board="+Board).AppendLine("mouse="+MouseStyle);
                foreach(Cat cat in Roles) {
                    if(cat.IsCustom) lines.AppendLine("role."+cat.Id+"="+cat.ShapeIndex+","+cat.Pattern+","+cat.Eyes+","+
                        Convert.ToBase64String(Encoding.UTF8.GetBytes(cat.Name)));
                    lines.AppendLine("build."+cat.Id+"="+cat.BuildIndex);
                    lines.AppendLine(OutfitPrefix+cat.Id+"="+OutfitFor(cat).Encode());
                }
                File.WriteAllText(tmp,lines.ToString(),new UTF8Encoding(false));
                if(File.Exists(path)) File.Replace(tmp,path,null); else File.Move(tmp,path);
            }
            catch(IOException) { }
            catch(UnauthorizedAccessException) { }
        }
    }
}
