using System;
using System.Drawing;
using System.Windows.Forms;

namespace KeyboardCat
{
    internal sealed class CatEditor : Form
    {
        readonly Preferences settings;
        readonly string id;
        readonly Font uiFont=new Font("Microsoft YaHei UI",9);
        readonly TextBox nameBox;
        readonly ComboBox shape,build,pattern,eyes;
        readonly Painter painter=new Painter();
        readonly PictureBox portrait;
        readonly Button save;
        readonly Label message;
        internal Cat ResultCat {get; private set;}

        public CatEditor(Preferences settings,bool edit)
        {
            this.settings=settings;
            Cat seed=settings.Cat;
            id=edit?seed.Id:"user-"+Guid.NewGuid().ToString("N");
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
            Font=uiFont;ForeColor=CoverArt.Ink;Text=edit?"编辑角色":"新建角色";BackColor=WardrobeForm.Paper;
            FormBorderStyle=FormBorderStyle.FixedDialog;StartPosition=FormStartPosition.CenterParent;
            MinimizeBox=false;MaximizeBox=false;ShowInTaskbar=false;ClientSize=new Size(455,286);

            portrait=new PictureBox {Bounds=new Rectangle(12,27,166,179),BackColor=Cat.Rgb(0xece5d8),SizeMode=PictureBoxSizeMode.Zoom};
            nameBox=new TextBox {Text=edit?seed.Name:"",MaxLength=24,Bounds=new Rectangle(237,18,200,26),AccessibleName="名字",TabIndex=0};
            Controls.AddRange(new Control[]{portrait,Caption("名字",20),nameBox});
            shape=Picker("毛型",57,Cat.Shapes,seed.ShapeIndex);
            build=Picker("体型",94,Array.ConvertAll(BodyBuild.All,b=>b.Name),seed.BuildIndex);
            pattern=Picker("花色",131,Cat.Patterns,seed.Pattern);
            eyes=Picker("瞳色",168,Cat.EyeNames,seed.Eyes);
            message=new Label {Bounds=new Rectangle(188,202,250,28),ForeColor=Color.FromArgb(146,70,70)};
            save=new StudioButton("保存",StudioButton.Kind.Primary,354,242,83,30);
            Button cancel=new StudioButton("取消",StudioButton.Kind.Quiet,265,242,80,30) {DialogResult=DialogResult.Cancel};
            Controls.AddRange(new Control[]{message,save,cancel});
            if(edit) {
                Button remove=new StudioButton("删除角色…",StudioButton.Kind.Quiet,16,242,98,30);
                remove.Click+=delegate {
                    if(MessageBox.Show(this,"删除「"+seed.Name+"」和它保存的穿搭？","删除角色",
                        MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)==DialogResult.Yes)
                        DialogResult=DialogResult.Yes;
                };
                Controls.Add(remove);
            }
            AcceptButton=save;CancelButton=cancel;
            nameBox.TextChanged+=delegate {ValidateName();};
            shape.SelectedIndexChanged+=delegate {RefreshPortrait();};
            build.SelectedIndexChanged+=delegate {RefreshPortrait();};
            pattern.SelectedIndexChanged+=delegate {RefreshPortrait();};
            eyes.SelectedIndexChanged+=delegate {RefreshPortrait();};
            save.Click+=delegate {if(ValidateName()) {ResultCat=Candidate();DialogResult=DialogResult.OK;}};
            RefreshPortrait();ValidateName();ActiveControl=nameBox;
        }
        static Label Caption(string text,int y)
        {return new Label {Text=text,Bounds=new Rectangle(186,y,50,24),TextAlign=ContentAlignment.MiddleLeft};}
        ComboBox Picker(string title,int y,string[] values,int index)
        {
            ComboBox combo=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Bounds=new Rectangle(237,y,200,26),AccessibleName=title};
            combo.Items.AddRange(values);combo.SelectedIndex=index;
            Controls.Add(Caption(title,y));Controls.Add(combo);return combo;
        }
        Cat Candidate()
        {return new Cat(id,nameBox.Text.Trim(),shape.SelectedIndex,pattern.SelectedIndex,eyes.SelectedIndex,build.SelectedIndex);}
        bool ValidateName()
        {
            string name=nameBox.Text.Trim();
            bool sameName=settings.Cat.Id==id && String.Equals(settings.Cat.Name,name,StringComparison.OrdinalIgnoreCase);
            bool duplicate=!sameName && settings.Roles.Exists(c=>c.Id!=id && String.Equals(c.Name,name,StringComparison.OrdinalIgnoreCase));
            save.Enabled=Cat.ValidName(name) && !duplicate;
            message.ForeColor=name.Length==0?CoverArt.Ink:CoverArt.Accent;
            message.Text=name.Length==0?"给这只猫起个名字。":duplicate?"这个名字已经有猫在用啦。":(!save.Enabled && name.Length>0?"名字需要 1 至 24 个字符。":"");
            return save.Enabled;
        }
        void RefreshPortrait()
        {
            Image old=portrait.Image;Bitmap next=new Bitmap(Painter.Width*2,Painter.Height*2,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using(Graphics g=Graphics.FromImage(next)) painter.Render(g,2,Candidate(),new Motion(),1);
            if(settings.PrintLook) InkLook.Apply(next,2);
            portrait.Image=next;
            if(old!=null)old.Dispose();
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing && portrait!=null && portrait.Image!=null) {portrait.Image.Dispose();portrait.Image=null;}
            base.Dispose(disposing);if(disposing) {painter.Dispose();uiFont.Dispose();}
        }
    }
}
