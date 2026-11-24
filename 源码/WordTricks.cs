using System;
using System.Collections.Generic;

namespace KeyboardCat
{
    internal enum TrickKind { None, Wave, Love, Fish, Sleep, Dance, Fortune, Toast, Freeze, Basketball, Groom, ChaseTail, Listen }

    internal sealed class WordTrick
    {
        public readonly TrickKind Kind;
        public readonly string Word, Pinyin, Label, ShortLabel;
        public bool IsIdle {get { return Pinyin==null; }}
        public readonly double Duration;

        WordTrick(TrickKind kind,string word,string pinyin,string label,string shortLabel,double duration)
        {
            Kind=kind; Word=word; Pinyin=pinyin; Label=label; ShortLabel=shortLabel; Duration=duration;
        }

        public static readonly WordTrick Wave=new WordTrick(TrickKind.Wave,"hello","nihao","挥爪打招呼","挥爪",2.2);
        public static readonly WordTrick Love=new WordTrick(TrickKind.Love,"love","aixin","双爪捧心","捧心",2.4);
        public static readonly WordTrick Fish=new WordTrick(TrickKind.Fish,"fish","xiaoyu","抱鱼啃两口","抱鱼",2.55);
        public static readonly WordTrick Sleep=new WordTrick(TrickKind.Sleep,"sleep","wanan","歪头打盹","打盹",3.25);
        public static readonly WordTrick Dance=new WordTrick(TrickKind.Dance,"dance","tiaowu","踩拍子跳舞","跳舞",2.7);
        public static readonly WordTrick Fortune=new WordTrick(TrickKind.Fortune,"laicai","fortune","来财 · 招财爪","招财",2.75);
        public static readonly WordTrick Toast=new WordTrick(TrickKind.Toast,"jingzijiyibei","cheers","敬自己一杯 · 举杯","举杯",3.1);
        public static readonly WordTrick Freeze=new WordTrick(TrickKind.Freeze,"yingkong","freeze","硬控 · 原地定格","定格",2.7);
        public static readonly WordTrick Basketball=new WordTrick(TrickKind.Basketball,"jinitaimei","jntm","鸡你太美 · 运球舞","运球舞",3.8);
        public static readonly WordTrick[] All={Wave,Love,Fish,Sleep,Dance,Fortune,Toast,Freeze,Basketball};
        public static readonly WordTrick[] Idle={
            new WordTrick(TrickKind.Groom,"groom",null,"抹抹脸","洗脸",3.0),
            new WordTrick(TrickKind.Listen,"listen",null,"侧耳听动静","侧耳",2.65),
            new WordTrick(TrickKind.ChaseTail,"tailchase",null,"回头扑尾巴","扑尾巴",3.05)
        };
    }

    internal sealed class TrickPlayer
    {
        sealed class Prefix
        {
            public readonly Prefix[] Next=new Prefix[26];
            public WordTrick Match;
            public string Label;
            public bool IsPinyin;
        }
        static readonly Prefix start=BuildMatcher();
        Prefix position=start, pinyinPosition=start;
        bool syllableStarted;
        IntPtr foreground;
        double lastLetter=double.NegativeInfinity;
        WordTrick active,pending;
        double nextIdle=double.NaN;
        int idleIndex;
        bool enabled=true;
        public double Started { get; private set; }
        public bool Enabled
        {
            get { return enabled; }
            set { enabled=value; BreakSequence(); }
        }

        // Build transitions using only the fixed vocabulary. A keypress advances one state.
        static Prefix BuildMatcher()
        {
            Dictionary<string,Prefix> states=new Dictionary<string,Prefix>(StringComparer.Ordinal);
            states.Add("",new Prefix());
            foreach (WordTrick trick in WordTrick.All)
            foreach (string word in new[]{trick.Word,trick.Pinyin})
            {
                for (int n=1;n<=word.Length;n++)
                {
                    string prefix=word.Substring(0,n);
                    if (!states.ContainsKey(prefix)) states.Add(prefix,new Prefix());
                }
                states[word].Match=trick;
                ChineseWord chinese=ChineseWord.For(trick);
                bool isPinyin=chinese.IsSpelling(word);
                states[word].IsPinyin=isPinyin;
                states[word].Label=isPinyin?chinese.Simplified:word;
            }
            foreach (KeyValuePair<string,Prefix> entry in states)
            for (int i=0;i<26;i++)
            {
                string suffix=entry.Key+(char)('a'+i);
                Prefix next;
                while (!states.TryGetValue(suffix,out next)) suffix=suffix.Substring(1);
                entry.Value.Next[i]=next;
            }
            return states[""];
        }

        public void BreakSequence()
        {
            position=pinyinPosition=start; syllableStarted=false;
            lastLetter=double.NegativeInfinity;
        }

        public void SetForeground(IntPtr window)
        {
            if (foreground==window) return;
            foreground=window;
            BreakSequence();
        }

        public string Observe(int key,double now,bool shortcut,bool syllableSeparator)
        {
            if (!enabled) return null;
            if (shortcut) { BreakSequence(); return null; }
            if (key==0x10 || key==0xa0 || key==0xa1 || key==0x14) return null; // Shift and Caps Lock
            if (key<'A' || key>'Z') {
                position=start;
                if (!syllableSeparator || !syllableStarted) BreakSequence();
                syllableStarted=false;
                return null;
            }
            double gap=now-lastLetter;
            if (gap>2.5) position=start;
            if (gap>8) pinyinPosition=start;
            lastLetter=now; syllableStarted=true;
            position=position.Next[key-'A'];
            pinyinPosition=pinyinPosition.Next[key-'A'];
            // Only registered pinyin aliases may span a syllable separator.
            Prefix hit=pinyinPosition.IsPinyin?pinyinPosition:position;
            if (hit.Match==null) return null;
            Play(hit.Match,now);
            return hit.Label;
        }

        public void Play(WordTrick trick,double now)
        {
            BreakSequence();
            InterruptIdle(now);
            if (Current(now)!=null) pending=trick;
            else {active=trick; Started=now;}
        }

        public void InterruptIdle(double now)
        {
            if(active!=null && active.IsIdle) active=null;
            nextIdle=now+12;
        }

        public void UpdateIdle(double now,double quietSeconds,bool allowed,bool longhair)
        {
            Current(now);
            if(!allowed) {InterruptIdle(now);return;}
            if(double.IsNaN(nextIdle)) nextIdle=now+12;
            if(quietSeconds<12) {
                if(active!=null && active.IsIdle) active=null;
                nextIdle=Math.Max(nextIdle,(now-Math.Max(0,quietSeconds))+12);
                return;
            }
            if(active!=null || now<nextIdle) return;
            int choice=(idleIndex+(longhair?1:0))%WordTrick.Idle.Length;
            active=WordTrick.Idle[choice]; Started=now;
            nextIdle=now+active.Duration+16+4*idleIndex;
            idleIndex=(idleIndex+1)%WordTrick.Idle.Length;
        }

        public WordTrick Current(double now)
        {
            if (active!=null && now>=Started+active.Duration) {
                Started+=active.Duration;
                active=pending; pending=null;
                if (active!=null && now>=Started+active.Duration) active=null;
            }
            return active!=null && now>=Started?active:null;
        }

        public void Reset()
        {
            active=pending=null; nextIdle=double.NaN; idleIndex=0;
            BreakSequence();
        }
    }
}
