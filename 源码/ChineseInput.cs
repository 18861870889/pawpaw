using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace KeyboardCat
{
    internal sealed class ChineseWord
    {
        public readonly WordTrick Trick;
        public readonly string Simplified, Traditional;
        internal readonly string[] Spellings;
        public static readonly List<ChineseWord> All=Load();

        ChineseWord(string[] parts) {
            Trick=Array.Find(WordTrick.All,item=>item.Word==parts[0]);
            if(Trick==null) throw new InvalidDataException("Unknown trick in Chinese vocabulary");
            Simplified=parts[1]; Traditional=parts[2];
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
        public static ChineseWord Named(string text) {return All.Find(word=>word.Simplified==text || word.Traditional==text);}
    }

    // Only a prefix from the fixed pinyin dictionary survives a key event.
    internal sealed class ChineseSequence
    {
        string prefix="";
        double last;
        bool hasLetters;
        ChineseWord complete;
        public bool HasKnownSpelling {get {return complete!=null;}}
        public void Clear() {prefix="";complete=null;hasLetters=false;}
        public bool Letter(int key,double now) {
            if(now-last>8) Clear();
            last=now;hasLetters=true;
            string next=prefix+(char)(key-'A'+'a');
            complete=null;
            while(next.Length>0) {
                bool found=false;
                foreach(ChineseWord word in ChineseWord.All)
                    foreach(string spelling in word.Spellings) {
                        if(spelling==next) complete=word;
                        if(spelling.StartsWith(next,StringComparison.Ordinal)) found=true;
                    }
                if(found) break;
                next=next.Substring(1);
            }
            prefix=next;
            return complete!=null;
        }
        public void Untranslated(double now) {Clear();hasLetters=true;last=now;}
        public void Edit(double now) {
            bool recent=hasLetters && now-last<=8;
            Clear();hasLetters=recent;last=now;
        }
        public bool Select(double now,out ChineseWord expected) {
            expected=null;
            if(now-last>8) Clear();
            if(!hasLetters) {Clear();return false;}
            expected=complete;
            if(complete!=null) Clear();
            hasLetters=false;last=now;
            return true;
        }
    }

    // IMEs and accessibility providers can commit on different turns of the message loop.
    internal sealed class ChineseConfirmation
    {
        static readonly double[] delays={.04,.17,.40,.75};
        int attempt;
        double started=double.NegativeInfinity;
        public int Ticket {get; private set;}
        public ChineseWord Expected {get; private set;}
        public bool LastAttempt {get {return attempt==delays.Length;}}
        public void Cancel() {Ticket++;Expected=null;started=double.NegativeInfinity;attempt=0;}
        public void Begin(ChineseWord expected,double now) {Cancel();Expected=expected;started=now;}
        public bool Current(int ticket,double now) {return ticket==Ticket && now-started<=2.0;}
        public bool Due(double now) {
            if(double.IsNegativeInfinity(started)) return false;
            if(now-started>2.0 || LastAttempt) {Cancel();return false;}
            return now-started>=delays[attempt];
        }
        public void StartAttempt() {attempt++;}
    }

    internal static class ChineseInput
    {
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
        [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint thread);
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(System.Drawing.Point point);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,System.Text.StringBuilder name,int count);
        public static bool OnCandidateWindow() {
            IntPtr hit=WindowFromPoint(System.Windows.Forms.Cursor.Position);
            if(hit==IntPtr.Zero) return false;
            System.Text.StringBuilder name=new System.Text.StringBuilder(96);
            GetClassName(hit,name,name.Capacity);
            string kind=name.ToString();
            if(kind.IndexOf("candidate",StringComparison.OrdinalIgnoreCase)>=0) return true;
            uint pid;
            GetWindowThreadProcessId(hit,out pid);
            try {
                using(System.Diagnostics.Process process=System.Diagnostics.Process.GetProcessById((int)pid))
                    return process.ProcessName.Equals("TextInputHost",StringComparison.OrdinalIgnoreCase);
            } catch(ArgumentException) {} catch(InvalidOperationException) {} catch(System.ComponentModel.Win32Exception) {}
            return false;
        }
        public static bool IsChinese(IntPtr window,IntPtr child) {
            uint pid;
            uint thread=GetWindowThreadProcessId(child!=IntPtr.Zero?child:window,out pid);
            return thread!=0 && (GetKeyboardLayout(thread).ToInt64()&0x3ff)==4;
        }
        public static string AtCaret(IntPtr window,IntPtr child,ChineseWord expected,bool allowSpelling,bool chatRoot) {
            if(window==IntPtr.Zero || Native.GetForegroundWindow()!=window || PasswordFocus.FocusedChild(window)!=child) return null;
            try {
                AutomationElement field=AutomationElement.FocusedElement;
                if(!chatRoot) return ReadField(field,expected,allowSpelling);
                // This validated native WeChat window exposes no editor. Its document may be the conversation.
                if(field==null || child!=window || field.Current.NativeWindowHandle!=unchecked((int)window.ToInt64()) ||
                   !Object.Equals(field.Current.ControlType,ControlType.Window)) return null;
                object password=field.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty,true);
                return allowSpelling && expected!=null && !Object.Equals(password,true)?expected.Simplified:null;
            }
            catch(ElementNotAvailableException) {return null;}
            catch(COMException) {return null;}
            catch(UnauthorizedAccessException) {return null;}
            catch(InvalidOperationException) {return null;}
            catch(TimeoutException) {return null;}
            catch(NotSupportedException) {return null;}
        }
        internal static string MatchField(AutomationElement field,ChineseWord fallback) {
            return ReadField(field,fallback,true);
        }

        // FindText asks the provider about fixed strings only. Text outside that vocabulary never leaves the field.
        static string ReadField(AutomationElement field,ChineseWord expected,bool allowSpelling) {
            if(field==null) return null;
            string phonetic=null;
            try {
                object password=field.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty,true);
                if(password is bool && (bool)password) return null;
                phonetic=allowSpelling && expected!=null?expected.Simplified:null;
                object raw;
                if(!(password is bool) || !field.TryGetCurrentPattern(TextPattern.Pattern,out raw)) return phonetic;
                TextPattern pattern=(TextPattern)raw;
                TextPatternRange[] selected=pattern.GetSelection();
                if(selected==null || selected.Length==0) return phonetic;
                if(selected.Length!=1) return null;
                TextPatternRange range=selected[0];
                if(range.CompareEndpoints(TextPatternRangeEndpoint.Start,range,TextPatternRangeEndpoint.End)!=0) return null;
                TextPatternRange document=pattern.DocumentRange;
                if(range.CompareEndpoints(TextPatternRangeEndpoint.End,document,TextPatternRangeEndpoint.Start)<0) return null;
                if(range.CompareEndpoints(TextPatternRangeEndpoint.End,document,TextPatternRangeEndpoint.End)>0) {
                    // Chromium can place the DOM caret just outside the last text node in a contenteditable.
                    TextPatternRange previous=range.Clone();
                    if(previous.MoveEndpointByUnit(TextPatternRangeEndpoint.Start,TextUnit.Character,-1)!=-1 ||
                       previous.CompareEndpoints(TextPatternRangeEndpoint.Start,document,TextPatternRangeEndpoint.End)>=0) return null;
                    range.MoveEndpointByRange(TextPatternRangeEndpoint.End,document,TextPatternRangeEndpoint.End);
                    range.MoveEndpointByRange(TextPatternRangeEndpoint.Start,document,TextPatternRangeEndpoint.End);
                }
                int length=0;
                foreach(ChineseWord word in ChineseWord.All) length=Math.Max(length,Math.Max(word.Simplified.Length,word.Traditional.Length));
                range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start,TextUnit.Character,-length-1);
                if(range.CompareEndpoints(TextPatternRangeEndpoint.Start,document,TextPatternRangeEndpoint.Start)<0)
                    range.MoveEndpointByRange(TextPatternRangeEndpoint.Start,document,TextPatternRangeEndpoint.Start);
                string hit=FindWord(range,expected);
                if(hit!=null) return hit;
                foreach(ChineseWord word in ChineseWord.All) {
                    if(word==expected) continue;
                    hit=FindWord(range,word);
                    if(hit!=null) return hit;
                }
            } catch(ElementNotAvailableException) {} catch(InvalidOperationException) {} catch(COMException) {}
              catch(UnauthorizedAccessException) {} catch(NotSupportedException) {return phonetic;} catch(ArgumentException) {}
              catch(TimeoutException) {}
            return null;
        }
        static string FindWord(TextPatternRange range,ChineseWord rule) {
            if(rule==null) return null;
            if(AtEnd(range,rule.Simplified)) return rule.Simplified;
            return rule.Simplified!=rule.Traditional && AtEnd(range,rule.Traditional)?rule.Traditional:null;
        }
        static bool AtEnd(TextPatternRange range,string word) {
            TextPatternRange match=range.FindText(word,true,false);
            if(match==null) return false;
            if(match.CompareEndpoints(TextPatternRangeEndpoint.End,range,TextPatternRangeEndpoint.End)==0) return true;
            match.MoveEndpointByRange(TextPatternRangeEndpoint.Start,match,TextPatternRangeEndpoint.End);
            if(match.MoveEndpointByUnit(TextPatternRangeEndpoint.End,TextUnit.Character,1)!=1 ||
               match.CompareEndpoints(TextPatternRangeEndpoint.End,range,TextPatternRangeEndpoint.End)!=0) return false;
            foreach(char mark in " 　，。！？、；：,.!?;:\u200b")
                if(match.FindText(mark.ToString(),false,false)!=null) return true;
            return false;
        }
    }
}
