using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Threading;

// Only this test process's own fixture window is queried.
class ChineseCaretTest
{
    [STAThread]
    static int Main(string[] args)
    {
        Assembly app=Assembly.LoadFrom(args[0]);
        Type occurrence=app.GetType("KeyboardCat.ExactWordOccurrence",true);
        MethodInfo probe=occurrence.GetMethod("TryRead");
        Func<AutomationElement,object> hit=delegate(AutomationElement editor) {
            object[] input={editor,null};
            probe.Invoke(null,input);return input[1];
        };
        Func<object,string> label=value=>value==null?null:(string)occurrence.GetField("Text").GetValue(value);
        StackPanel panel=new StackPanel();
        Dictionary<string,string> expectations=new Dictionary<string,string>();
        Action<string,string,int,int,string> add=delegate(string id,string text,int caret,int selection,string wanted) {
            TextBox editor=new TextBox {Text=text};
            editor.Select(caret,selection);
            AutomationProperties.SetAutomationId(editor,id);panel.Children.Add(editor);expectations[id]=wanted;
        };
        string[] words={"你好","爱心","愛心","小鱼","小魚","晚安","跳舞","来财","來財","敬自己一杯","硬控","鸡你太美","雞你太美","蜜雪冰城甜蜜蜜","干饭时间","乾飯時間","一键三连","一鍵三連"};
        for(int i=0;i<words.Length;i++) add("word"+i,"今天"+words[i],2+words[i].Length,0,words[i]);
        add("punctuation","愿你来财！",5,0,"来财");
        add("longwithmark","敬自己一杯。",6,0,"敬自己一杯");
        add("longest","来听蜜雪冰城甜蜜蜜！",10,0,"蜜雪冰城甜蜜蜜");
        add("candidate-space","你好 ",3,0,"你好");
        add("candidate-full-space","小魚　",3,0,"小魚");
        add("candidate-placeholder","来财\u200b",3,0,"来财");
        add("one-delimiter-only","你好  ",4,0,null);
        add("homophone","天气像小雨",5,0,null);
        add("middle","小鱼在游泳",2,0,"小鱼");
        add("away","小鱼在游泳",5,0,null);
        add("selected","鸡你太美",0,4,null);
        Run tail=new Run("小鱼");
        Paragraph paragraph=new Paragraph(new Run("今天"));
        paragraph.Inlines.Add(new Bold(tail));
        RichTextBox rich=new RichTextBox(new FlowDocument(paragraph));
        rich.CaretPosition=tail.ContentEnd;
        AutomationProperties.SetAutomationId(rich,"rich-tail");
        panel.Children.Add(rich);expectations["rich-tail"]="小鱼";
        TextBox delayed=new TextBox {Text="xiaoyu"};
        delayed.Select(6,0);AutomationProperties.SetAutomationId(delayed,"delayed");panel.Children.Add(delayed);
        PasswordBox password=new PasswordBox {Password="小鱼"};
        AutomationProperties.SetAutomationId(password,"secure");panel.Children.Add(password);expectations["secure"]=null;
        Window window=new Window {Content=panel,Width=220,Height=410,Opacity=0,ShowInTaskbar=false,ShowActivated=false,WindowStyle=WindowStyle.None};
        int result=1;
        window.Show();
        IntPtr handle=new WindowInteropHelper(window).Handle;
        Dispatcher dispatcher=window.Dispatcher;
        Thread worker=new Thread(delegate() {
            try {
                AutomationElement root=AutomationElement.FromHandle(handle);
                foreach(KeyValuePair<string,string> example in expectations) {
                    AutomationElement editor=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,example.Key));
                    if(editor==null) throw new Exception("Missing fixture "+example.Key);
                    string actual=label(hit(editor));
                    if(actual!=example.Value) throw new Exception(example.Key+" expected "+(example.Value??"(no match)")+" got "+(actual??"(no match)"));
                }
                AutomationElement target=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"delayed"));
                if(hit(target)!=null) throw new Exception("Uncommitted spelling matched a literal");
                dispatcher.Invoke((Action)delegate {delayed.Text="小魚";delayed.Select(2,0);});
                object first=hit(target),same=hit(target);
                if(label(first)!="小魚" || !(bool)occurrence.GetMethod("Same").Invoke(first,new[]{same})) throw new Exception("Repeated sample of one word");
                dispatcher.Invoke((Action)delegate {delayed.AppendText("小魚");delayed.Select(4,0);});
                object second=hit(target);
                if(label(second)!="小魚" || (bool)occurrence.GetMethod("Same").Invoke(second,new[]{first})) throw new Exception("Separate occurrences must each trigger");
                var changed=new AutoResetEvent(false);
                AutomationEventHandler notice=delegate {changed.Set();};
                Automation.AddAutomationEventHandler(TextPattern.TextChangedEvent,target,TreeScope.Element,notice);
                try {
                    dispatcher.Invoke((Action)delegate {delayed.AppendText("蜜雪冰城甜蜜蜜");delayed.Select(delayed.Text.Length,0);});
                    if(!changed.WaitOne(2500) || label(hit(target))!="蜜雪冰城甜蜜蜜")
                        throw new Exception("A native edit notification must reveal the newly committed phrase");
                } finally {
                    Automation.RemoveAutomationEventHandler(TextPattern.TextChangedEvent,target,notice);
                }
                result=0;
                Console.WriteLine("Windows: native editable controls passed Chinese suffix, script, homophone, caret, selection, distinct occurrences, native change notifications and password checks.");
            } catch(Exception ex) {Console.Error.WriteLine(ex);}
            finally {dispatcher.BeginInvoke((Action)delegate {dispatcher.InvokeShutdown();});}
        });
        worker.IsBackground=true;worker.SetApartmentState(ApartmentState.MTA);worker.Start();
        DispatcherTimer timeout=new DispatcherTimer {Interval=TimeSpan.FromSeconds(20)};
        timeout.Tick+=delegate {Console.Error.WriteLine("Caret fixture timed out");dispatcher.InvokeShutdown();};
        timeout.Start();Dispatcher.Run();return result;
    }
}
