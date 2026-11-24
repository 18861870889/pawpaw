using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace KeyboardCat
{
    internal sealed class ChineseWord
    {
        public readonly WordTrick Trick;
        public readonly string Simplified,Traditional;
        readonly string[] Spellings;
        public static readonly List<ChineseWord> All=Load();

        ChineseWord(string[] parts) {
            Trick=Array.Find(WordTrick.All,item=>item.Word==parts[0]);
            if(Trick==null) throw new InvalidDataException("Unknown trick in Chinese vocabulary");
            Simplified=parts[1];Traditional=parts[2];
            Spellings=new string[parts.Length-3];
            Array.Copy(parts,3,Spellings,0,Spellings.Length);
        }
        static List<ChineseWord> Load() {
            List<ChineseWord> words=new List<ChineseWord>();
            using(StreamReader reader=new StreamReader(typeof(ChineseWord).Assembly.GetManifestResourceStream("ChineseWords.tsv"))) {
                string line;
                while((line=reader.ReadLine())!=null)
                    if(line.Length>0) words.Add(new ChineseWord(line.Split('\t')));
            }
            return words;
        }
        public static ChineseWord For(WordTrick trick) {return All.Find(word=>word.Trick==trick);}
        public static readonly int MaxLength=All.Max(w=>Math.Max(w.Simplified.Length,w.Traditional.Length));
        public bool IsSpelling(string alias) {return Array.IndexOf(Spellings,alias)>=0;}
    }

    internal static class ChineseInput
    {
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
        [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint thread);

        // The layout is needed only to carry a partial spelling across a candidate selection.
        public static bool IsSyllableSeparator(int key,IntPtr window,IntPtr child)
        {
            if(key!=0x20 && key!=0xde && !(key>='1' && key<='9') && !(key>=0x61 && key<=0x69)) return false;
            uint pid;
            uint thread=GetWindowThreadProcessId(child!=IntPtr.Zero?child:window,out pid);
            return thread!=0 && (GetKeyboardLayout(thread).ToInt64()&0x3ff)==4;
        }
    }
}
