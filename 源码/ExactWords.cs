using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace KeyboardCat
{
    // Keep only the fixed catalog word and its provider-owned position, never the contents of an editor.
    internal sealed class ExactWordOccurrence
    {
        public readonly ChineseWord Rule;
        public readonly string Text;
        readonly TextPatternRange span;
        ExactWordOccurrence(ChineseWord rule,string text,TextPatternRange span) {Rule=rule;Text=text;this.span=span;}

        public bool Same(ExactWordOccurrence other)
        {
            return other!=null && Rule==other.Rule &&
                span.CompareEndpoints(TextPatternRangeEndpoint.Start,other.span,TextPatternRangeEndpoint.Start)==0;
        }

        public static bool Editable(AutomationElement field)
        {
            if(field==null || !Equals(field.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty,true),false)) return false;
            ControlType type=field.Current.ControlType;
            return type==ControlType.Edit || type==ControlType.Document || type==ControlType.ComboBox;
        }

        public static bool TryRead(AutomationElement field,out ExactWordOccurrence found)
        {
            found=null;
            object raw;
            if(!Editable(field) || !field.TryGetCurrentPattern(TextPattern.Pattern,out raw)) return false;
            TextPattern pattern=(TextPattern)raw;
            TextPatternRange[] selected=pattern.GetSelection();
            if(selected==null || selected.Length!=1) return false;
            TextPatternRange range=selected[0];
            var start=TextPatternRangeEndpoint.Start;var end=TextPatternRangeEndpoint.End;
            if(range.CompareEndpoints(start,range,end)!=0) return false;
            object readOnly=range.GetAttributeValue(TextPattern.IsReadOnlyAttribute);
            if(Equals(readOnly,true) || (field.Current.ControlType==ControlType.Document && !Equals(readOnly,false))) return false;
            TextPatternRange document=pattern.DocumentRange;
            if(range.CompareEndpoints(end,document,start)<0) return false;
            if(range.CompareEndpoints(end,document,end)>0) {
                // A DOM caret can sit just outside the last text node in a contenteditable.
                TextPatternRange previous=range.Clone();
                if(previous.MoveEndpointByUnit(start,TextUnit.Character,-1)!=-1 || previous.CompareEndpoints(start,document,end)>=0) return false;
                range.MoveEndpointByRange(end,document,end);range.MoveEndpointByRange(start,document,end);
            }
            range.MoveEndpointByUnit(start,TextUnit.Character,-ChineseWord.MaxLength-1);
            if(range.CompareEndpoints(start,document,start)<0) range.MoveEndpointByRange(start,document,start);
            foreach(ChineseWord rule in ChineseWord.All) {
                found=Find(range,rule,rule.Simplified);
                if(found==null && rule.Simplified!=rule.Traditional) found=Find(range,rule,rule.Traditional);
                if(found!=null) break;
            }
            return true;
        }

        static ExactWordOccurrence Find(TextPatternRange range,ChineseWord rule,string text)
        {
            TextPatternRange span=range.FindText(text,true,false);
            if(span==null) return null;
            var end=TextPatternRangeEndpoint.End;
            if(span.CompareEndpoints(end,range,end)!=0) {
                TextPatternRange mark=span.Clone();
                mark.MoveEndpointByRange(TextPatternRangeEndpoint.Start,span,end);
                if(mark.MoveEndpointByUnit(end,TextUnit.Character,1)!=1 || mark.CompareEndpoints(end,range,end)!=0) return null;
                bool delimiter=false;
                foreach(char c in " 　，。！？、；：,.!?;:\u200b") {
                    if(mark.FindText(c.ToString(),false,false)!=null) {delimiter=true;break;}
                }
                if(!delimiter) return null;
            }
            return new ExactWordOccurrence(rule,text,span);
        }
    }

    internal sealed class ExactWordMonitor : IDisposable
    {
        readonly object gate=new object();
        readonly Stopwatch time=Stopwatch.StartNew();
        readonly Action<ExactWordMonitor,int,WordEntry,ExactWordOccurrence> deliver;
        IntPtr window,child;
        WordEntry turn,requested;
        int revision,subscription,tries;
        long due;
        bool refresh,stopped,pinned;

        // Only this worker owns UI Automation elements, event subscriptions, and text ranges.
        AutomationElement field;
        AutomationEventHandler changed;
        ExactWordOccurrence previous;

        public ExactWordMonitor(Action<ExactWordMonitor,int,WordEntry,ExactWordOccurrence> deliver)
        {
            this.deliver=deliver;
            Thread worker=new Thread(Run){IsBackground=true,Name="PawPaw word confirmations"};
            worker.SetApartmentState(ApartmentState.MTA);worker.Start();
        }
        public void Follow(IntPtr target,IntPtr focus,WordEntry entry,bool update,bool input)
        {
            lock(gate) {
                if(stopped) return;
                if(window!=target || child!=focus) {
                    window=target;child=focus;revision++;requested=null;pinned=false;refresh=true;
                }
                turn=entry;
                if(target!=IntPtr.Zero && update) refresh=true;
                if(target!=IntPtr.Zero && input) {pinned=true;Schedule(entry);}
                if(refresh || input) Monitor.Pulse(gate);
            }
        }
        void Schedule(WordEntry entry)
        {
            requested=entry;tries=0;due=time.ElapsedMilliseconds+45;
        }
        public bool Current(int id) {lock(gate) return !stopped && revision==id;}
        public void Dispose() {lock(gate) {stopped=true;revision++;Monitor.Pulse(gate);}}

        void Bind(AutomationElement next,bool baseline=true)
        {
            if(field!=null && next!=null && Automation.Compare(field,next)) return;
            if(field==null && next==null) return;
            int id;
            lock(gate) {id=++subscription;revision++;}
            if(changed!=null) {
                try {Automation.RemoveAutomationEventHandler(TextPattern.TextChangedEvent,field,changed);}
                catch(Exception e) {if(!ProviderFailure(e)) throw;}
            }
            field=next;changed=null;previous=null;
            if(field==null) return;
            AutomationEventHandler handler=delegate {
                lock(gate) {if(!stopped && subscription==id && window!=IntPtr.Zero) {
                    // A selection notification can arrive after the next letter has started a new turn.
                    Schedule(pinned && requested!=null?requested:turn);Monitor.Pulse(gate);
                }}
            };
            try {
                Automation.AddAutomationEventHandler(TextPattern.TextChangedEvent,field,TreeScope.Element,handler);
                changed=handler;
            } catch(Exception e) {if(!ProviderFailure(e)) throw;}
            // Merely focusing an editor that already ends in a trigger must leave the cat alone.
            if(baseline) ExactWordOccurrence.TryRead(field,out previous);
        }
        static bool ProviderFailure(Exception e)
        {
            return e is ElementNotAvailableException || e is COMException || e is UnauthorizedAccessException ||
                e is InvalidOperationException || e is NotSupportedException || e is ArgumentException || e is TimeoutException ||
                e is System.ComponentModel.Win32Exception;
        }
        static bool NativeFocus(IntPtr target,IntPtr focus)
        {
            return target!=IntPtr.Zero && target==Native.GetForegroundWindow() && PasswordFocus.FocusedChild(target)==focus;
        }
        void Run()
        {
            try {
                for(;;) {
                    IntPtr target,focus;WordEntry sample=null;int id;bool checkFocus,confirmed;
                    lock(gate) {
                        while(!stopped && !refresh && (requested==null || due>time.ElapsedMilliseconds))
                            Monitor.Wait(gate,requested==null?Timeout.Infinite:(int)Math.Max(1,due-time.ElapsedMilliseconds));
                        if(stopped) return;
                        target=window;focus=child;id=revision;checkFocus=refresh;refresh=false;confirmed=pinned && requested!=null;
                        if(requested!=null && due<=time.ElapsedMilliseconds) {
                            sample=requested;
                            if(++tries==3) {requested=null;pinned=false;} else due=time.ElapsedMilliseconds+(tries==1?135:310);
                        }
                    }
                    try {
                        if(!NativeFocus(target,focus)) {Bind(null);continue;}
                        if(checkFocus || sample!=null) {
                            AutomationElement active=AutomationElement.FocusedElement;
                            Bind(ExactWordOccurrence.Editable(active)?active:null,!confirmed || field!=null);
                        }
                        if(sample==null || field==null || !Current(id)) continue;
                        ExactWordOccurrence hit;
                        if(!ExactWordOccurrence.TryRead(field,out hit)) continue;
                        bool fresh=hit!=null && !hit.Same(previous);
                        if(!NativeFocus(target,focus) || !Automation.Compare(field,AutomationElement.FocusedElement) || !Current(id)) continue;
                        previous=hit;
                        if(fresh) {
                            lock(gate) {if(requested==sample) pinned=false;}
                            deliver(this,id,sample,hit);
                        }
                    } catch(Exception e) {if(!ProviderFailure(e)) throw;}
                }
            } finally {
                try {Bind(null);} catch(Exception e) {if(!ProviderFailure(e)) throw;}
            }
        }
    }
}
