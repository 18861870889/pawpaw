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
        public static bool IsChinese(IntPtr window) {
            uint pid;
            uint thread=GetWindowThreadProcessId(window,out pid);
            return thread!=0 && (GetKeyboardLayout(thread).ToInt64()&0x3ff)==4;
        }
        // The provider compares only catalog entries in a short range ending at the caret.
        // No control value, selected text or chat history is requested.
        public static string AtCaret(IntPtr window,IntPtr child,ChineseWord fallback) {
            if(window==IntPtr.Zero || Native.GetForegroundWindow()!=window || PasswordFocus.FocusedChild(window)!=child) return null;
            try { return MatchField(AutomationElement.FocusedElement,fallback); }
            catch(ElementNotAvailableException) {return null;}
            catch(COMException) {return null;}
            catch(UnauthorizedAccessException) {return null;}
            catch(InvalidOperationException) {return null;}
        }

        internal static string MatchField(AutomationElement field,ChineseWord fallback) {
            try {
                if(field==null) return null;
                object password=field.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty,true);
                if(password is bool && (bool)password) return null;
                string phonetic=fallback==null?null:fallback.Simplified;
                object raw;
                if(!(password is bool) || !field.TryGetCurrentPattern(TextPattern.Pattern,out raw)) return phonetic;
                TextPatternRange[] selected=((TextPattern)raw).GetSelection();
                if(selected==null || selected.Length!=1) return null;
                TextPatternRange range=selected[0];
                if(range.CompareEndpoints(TextPatternRangeEndpoint.Start,range,TextPatternRangeEndpoint.End)!=0) return null;
                int length=0;
                foreach(ChineseWord word in ChineseWord.All) length=Math.Max(length,Math.Max(word.Simplified.Length,word.Traditional.Length));
                range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start,TextUnit.Character,-1);
                // Chinese punctuation can accept a candidate and land after it in the same edit.
                foreach(char mark in "，。！？、；：,.!?;:") {
                    if(!EndsIn(range,mark.ToString())) continue;
                    range.MoveEndpointByUnit(TextPatternRangeEndpoint.End,TextUnit.Character,-1);
                    break;
                }
                range.MoveEndpointByRange(TextPatternRangeEndpoint.Start,range,TextPatternRangeEndpoint.End);
                range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start,TextUnit.Character,-length);
                foreach(ChineseWord word in ChineseWord.All) {
                    if(EndsIn(range,word.Simplified)) return word.Simplified;
                    if(word.Traditional!=word.Simplified && EndsIn(range,word.Traditional)) return word.Traditional;
                }
            } catch(ElementNotAvailableException) {} catch(InvalidOperationException) {} catch(COMException) {}
              catch(UnauthorizedAccessException) {} catch(NotSupportedException) {} catch(ArgumentException) {}
              catch(TimeoutException) {}
            return null;
        }
        static bool EndsIn(TextPatternRange range,string word) {
            TextPatternRange hit=range.FindText(word,true,false);
            return hit!=null && hit.CompareEndpoints(TextPatternRangeEndpoint.End,range,TextPatternRangeEndpoint.End)==0;
        }
    }
}
