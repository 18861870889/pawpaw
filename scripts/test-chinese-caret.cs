using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
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
        add("homophone","天气像小雨",5,0,null);
        add("middle","小鱼在游泳",2,0,"小鱼");
        add("away","小鱼在游泳",5,0,null);
        add("selected","鸡你太美",0,4,null);
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
                result=0;
                Console.WriteLine("Windows: native editable controls passed Chinese suffix, script, homophone, caret, selection and password checks.");
            } catch(Exception ex) {Console.Error.WriteLine(ex);}
            finally {dispatcher.BeginInvoke((Action)delegate {dispatcher.InvokeShutdown();});}
        });
        worker.IsBackground=true;worker.SetApartmentState(ApartmentState.MTA);worker.Start();
        DispatcherTimer timeout=new DispatcherTimer {Interval=TimeSpan.FromSeconds(20)};
        timeout.Tick+=delegate {Console.Error.WriteLine("Caret fixture timed out");dispatcher.InvokeShutdown();};
        timeout.Start();Dispatcher.Run();return result;
    }
}
