using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("爪爪 PawPaw")]
[assembly: AssemblyDescription("陪你打字、换装和玩彩蛋的桌面猫")]
[assembly: AssemblyProduct("PawPaw")]
[assembly: AssemblyVersion("2.0.2.0")]
[assembly: AssemblyFileVersion("2.0.2.0")]

namespace KeyboardCat
{
    internal static class Program
    {
        internal const string DisplayName="爪爪 PawPaw";
        internal const string FeedbackUrl="https://github.com/18861870889/pawpaw/issues/new/choose";
        internal static readonly string Version=typeof(Program).Assembly.GetName().Version.ToString(3);
        // Keep the existing instance identity so updates can find an already-running pet.
        internal static readonly uint WakeMessage=Native.RegisterWindowMessage("KeyboardCat.0BC02B16.Show");
        [STAThread]
        static void Main()
        {
            bool first;
            using(Mutex instance=new Mutex(true,@"Local\KeyboardCat.0BC02B16",out first))
            {
                if(!first)
                {
                    if(WakeMessage!=0) Native.PostMessage(new IntPtr(0xffff),WakeMessage,IntPtr.Zero,IntPtr.Zero);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                try { using(PetForm pet=new PetForm(Preferences.DefaultFile)) Application.Run(pet); }
                catch(Win32Exception e)
                {
                    MessageBox.Show("Windows 无法显示桌宠。\r\n"+e.Message,DisplayName,MessageBoxButtons.OK,MessageBoxIcon.Error);
                }
            }
        }
    }
}
