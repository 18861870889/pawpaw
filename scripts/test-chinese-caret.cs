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
        MethodInfo probe=app.GetType("KeyboardCat.ChineseInput").GetMethod("MatchField",BindingFlags.Static|BindingFlags.NonPublic);
        Type wordType=app.GetType("KeyboardCat.ChineseWord");
        object fish=wordType.GetMethod("Named").Invoke(null,new object[]{"小鱼"});
        StackPanel panel=new StackPanel();
        Dictionary<string,string> expectations=new Dictionary<string,string>();
        Action<string,string,int,int,string> add=delegate(string id,string text,int caret,int selection,string wanted) {
            TextBox editor=new TextBox {Text=text};
            editor.Select(caret,selection);
            AutomationProperties.SetAutomationId(editor,id);panel.Children.Add(editor);expectations[id]=wanted;
        };
        string[] words={"你好","爱心","愛心","小鱼","小魚","晚安","跳舞","来财","來財","敬自己一杯","硬控","鸡你太美","雞你太美"};
        for(int i=0;i<words.Length;i++) add("word"+i,"今天"+words[i],2+words[i].Length,0,words[i]);
        add("punctuation","愿你来财！",5,0,"来财");
        add("longwithmark","敬自己一杯。",6,0,"敬自己一杯");
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
                    string actual=(string)probe.Invoke(null,new object[]{editor,fish});
                    if(actual!=example.Value) throw new Exception(example.Key+" expected "+(example.Value??"(no match)")+" got "+(actual??"(no match)"));
                }
                AutomationElement target=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"delayed"));
                Type clockType=app.GetType("KeyboardCat.ChineseConfirmation");
                object clock=Activator.CreateInstance(clockType,true);
                MethodInfo read=app.GetType("KeyboardCat.ChineseInput").GetMethod("ReadField",BindingFlags.Static|BindingFlags.NonPublic);
                clockType.GetMethod("Begin").Invoke(clock,new object[]{fish,0.0});
                int ticket=(int)clockType.GetProperty("Ticket").GetValue(clock,null);
                foreach(double t in new[]{.05,.20,.45}) {
                    if(t>.3) dispatcher.Invoke((Action)delegate {delayed.Text="小魚";delayed.Select(2,0);});
                    if(!(bool)clockType.GetMethod("Due").Invoke(clock,new object[]{t})) throw new Exception("Missing delayed commit retry");
                    clockType.GetMethod("StartAttempt").Invoke(clock,null);
                    bool fallback=(bool)clockType.GetProperty("LastAttempt").GetValue(clock,null);
                    string hit=(string)read.Invoke(null,new object[]{target,fish,fallback});
                    if(hit!=(t>.3?"小魚":null)) throw new Exception("Wrong result across IME composition and delayed commit");
                }
                clockType.GetMethod("Cancel").Invoke(clock,null);
                if((bool)clockType.GetMethod("Current").Invoke(clock,new object[]{ticket,.5})) throw new Exception("Stale candidate remained eligible");
                result=0;
                Console.WriteLine("Windows: native editable controls passed Chinese suffix, script, homophone, caret, selection, delayed composition and password checks.");
            } catch(Exception ex) {Console.Error.WriteLine(ex);}
            finally {dispatcher.BeginInvoke((Action)delegate {dispatcher.InvokeShutdown();});}
        });
        worker.IsBackground=true;worker.SetApartmentState(ApartmentState.MTA);worker.Start();
        DispatcherTimer timeout=new DispatcherTimer {Interval=TimeSpan.FromSeconds(20)};
        timeout.Tick+=delegate {Console.Error.WriteLine("Caret fixture timed out");dispatcher.InvokeShutdown();};
        timeout.Start();Dispatcher.Run();return result;
    }
}
