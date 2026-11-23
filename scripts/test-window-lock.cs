using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static class WindowLockTest
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string title);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point p);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window,uint flag);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    static readonly BindingFlags fields=BindingFlags.Instance|BindingFlags.NonPublic;
    static IntPtr Target(Point point){return GetAncestor(WindowFromPoint(point),2);}
    static void Pump(){for(int i=0;i<8;i++){Application.DoEvents();Thread.Sleep(20);}}
    static void Expect(bool ok,string what){if(!ok) throw new Exception(what);}
    static Form Open(Type pet,string path){
        Form f=(Form)Activator.CreateInstance(pet,new object[]{path});f.Show();Pump();return f;
    }

    [STAThread]
    static int Main(string[] args)
    {
        SetProcessDPIAware();
        Rectangle area=Screen.PrimaryScreen.WorkingArea;
        Point origin=new Point(area.Left+25,area.Top+25);
        if(args[0]=="--behind"){
            using(Form target=new Form()){
                target.StartPosition=FormStartPosition.Manual;target.FormBorderStyle=FormBorderStyle.None;
                target.Bounds=new Rectangle(origin,new Size(500,400));target.Text=args[1];
                target.ShowInTaskbar=false;target.TopMost=true;Application.Run(target);
            }
            return 0;
        }
        string path=Path.Combine(Path.GetTempPath(),"PawPaw-lock-"+Guid.NewGuid().ToString("N")+".ini");
        Process behind=null;Form f=null;IntPtr under=IntPtr.Zero;
        try {
            string title="PawPaw-input-"+Guid.NewGuid().ToString("N");
            var info=new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--behind "+title){UseShellExecute=false};
            behind=Process.Start(info);
            for(int i=0;i<60 && under==IntPtr.Zero;i++){Thread.Sleep(50);under=FindWindow(null,title);}
            Expect(under!=IntPtr.Zero,"Missing independent target");
            File.WriteAllText(path,"paused=1\ntransparency=75\nx="+origin.X+"\ny="+origin.Y+"\n");
            Type pet=Assembly.LoadFrom(args[0]).GetType("KeyboardCat.PetForm",true);
            f=Open(pet,path);
            Point body=Point.Empty;
            for(int x=f.Left+15;x<f.Right-30 && body.IsEmpty;x+=5)
                for(int y=f.Top+30;y<f.Bottom-10;y+=5)
                    if(Target(new Point(x,y))==f.Handle){body=new Point(x,y);break;}
            Expect(!body.IsEmpty,"Unlocked cat must win hit testing on an opaque pixel");
            pet.GetMethod("SetLocked",fields).Invoke(f,new object[]{true});Pump();
            Expect(Target(body)==under,"Locked cat must pass through to the independent process");
            Expect(File.ReadAllText(path).Contains("locked=1"),"Lock must persist");
            f.Close();f.Dispose();f=Open(pet,path);
            Expect(Target(body)==under,"Restored lock must still pass through");
            Form badge=(Form)pet.GetField("unlockBadge",fields).GetValue(f);
            Point center=new Point(badge.Left+badge.Width/2,badge.Top+badge.Height/2);
            Expect(badge.Visible && Target(center)==badge.Handle,"Only the small badge must be clickable");
            Point b=badge.PointToClient(center);
            IntPtr xy=new IntPtr((b.Y<<16)|b.X);
            SendMessage(badge.Handle,0x201,new IntPtr(1),xy);SendMessage(badge.Handle,0x202,IntPtr.Zero,xy);
            Pump();Expect(!badge.Visible && Target(body)==f.Handle,"Badge click must restore the pet");
            Expect(File.ReadAllText(path).Contains("locked=0"),"Unlock must persist");
            Console.WriteLine("Cross-process click-through, badge hit testing and restart passed.");
            return 0;
        } catch(Exception e){Console.Error.WriteLine(e);return 1;}
        finally {
            if(f!=null){f.Close();f.Dispose();}
            if(behind!=null){if(!behind.HasExited){PostMessage(under,0x10,IntPtr.Zero,IntPtr.Zero);if(!behind.WaitForExit(3000)) behind.Kill();}behind.Dispose();}
            if(File.Exists(path)) File.Delete(path);
        }
    }
}
