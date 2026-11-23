using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace KeyboardCat
{
    internal sealed class OpacityForm : Form
    {
        readonly Font numberFont=new Font("Microsoft YaHei UI",22,FontStyle.Bold);
        readonly Font uiFont=new Font("Microsoft YaHei UI",9);
        public OpacityForm(int initial, Action<int> change)
        {
            SuspendLayout();
            AutoScaleDimensions=new SizeF(96,96);
            AutoScaleMode=AutoScaleMode.Dpi;
            Font=uiFont;ForeColor=CoverArt.Ink;
            Text="桌宠透明度";
            BackColor=WardrobeForm.Paper;
            FormBorderStyle=FormBorderStyle.FixedToolWindow;
            StartPosition=FormStartPosition.Manual;
            ShowInTaskbar=false;
            MaximizeBox=false;
            MinimizeBox=false;
            TopMost=true;
            ClientSize=new Size(306,170);

            Label title=new Label {Text="桌宠透明度",Bounds=new Rectangle(18,12,142,37),TextAlign=ContentAlignment.MiddleLeft};
            Label value=new Label {Text=initial+"%",Font=numberFont,ForeColor=CoverArt.Accent,
                Bounds=new Rectangle(161,3,128,48),TextAlign=ContentAlignment.MiddleRight};
            Panel line=new Panel {Bounds=new Rectangle(18,53,270,1),BackColor=CoverArt.Ink};
            TrackBar slider=new TrackBar {Minimum=0,Maximum=Preferences.MaxTransparency,Value=initial,
                SmallChange=1,LargeChange=10,TickStyle=TickStyle.None,AutoSize=false,
                Bounds=new Rectangle(13,65,280,29),AccessibleName="桌宠透明度",TabIndex=0};
            Label solid=new Label {Text="0%  不透明",Bounds=new Rectangle(18,97,120,23)};
            Label faint=new Label {Text="80%  更透明",Bounds=new Rectangle(166,97,122,23),TextAlign=ContentAlignment.TopRight};
            int[] levels={0,40,80};
            string[] names={"清晰","轻透","淡影"};
            StudioButton[] presets=new StudioButton[levels.Length];
            for(int i=0;i<levels.Length;i++) {
                int level=levels[i];StudioButton b=new StudioButton(names[i],StudioButton.Kind.Switch,18+i*90,127,90,28);
                b.AccessibleName=names[i]+" "+level+"%";b.TabIndex=i+1;b.Selected=level==initial;
                b.Click+=delegate {slider.Value=level;};Controls.Add(b);presets[i]=b;
            }
            slider.ValueChanged+=delegate {
                value.Text=slider.Value.ToString(CultureInfo.InvariantCulture)+"%";
                for(int i=0;i<levels.Length;i++) presets[i].Selected=levels[i]==slider.Value;
                change(slider.Value);
            };
            Controls.AddRange(new Control[]{title,value,line,slider,solid,faint});
            ActiveControl=slider;
            ResumeLayout(false);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);if(disposing) {numberFont.Dispose();uiFont.Dispose();}
        }

        protected override bool ProcessCmdKey(ref Message message, Keys key)
        {
            if(key!=Keys.Escape) return base.ProcessCmdKey(ref message,key);
            Close();
            return true;
        }
    }
}
