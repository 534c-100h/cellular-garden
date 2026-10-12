using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CellularGarden {
    internal sealed class AppearanceDialog:Form {
        private readonly GardenContext app;
        private ComboBox mode;
        private Label description,imageName,backgroundLabel,themeLabel;
        private Button cell,background,reset,import;
        private PictureBox preview;
        private FlowLayoutPanel body;
        internal AppearanceDialog(GardenContext context){
            app=context;Text="外观个性化 · 细胞花园";Font=new Font("Microsoft YaHei UI",10);AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;ClientSize=new Size(460,550);Icon=app.AppIcon;
            body=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(24)};Controls.Add(body);
            AddLabel("让生命，显露你的风景。",16,42);
            themeLabel=AddLabel("",9,30);
            mode=new ComboBox{Width=394,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="外观模式",Margin=new Padding(0,0,0,14)};mode.Items.AddRange(new[]{"纯色模式","图片模式"});mode.SelectedIndex=app.Settings.AppearanceMode==1?1:0;body.Controls.Add(mode);
            description=AddLabel("",9,48);
            var colors=new FlowLayoutPanel{Width=400,Height=75,Margin=new Padding(0)};
            var a=new FlowLayoutPanel{Width=195,Height=75,FlowDirection=FlowDirection.TopDown,Margin=new Padding(0)};
            a.Controls.Add(new Label{Text="细胞颜色",Width=180,Height=26});cell=MakeButton("",180,delegate{ChooseColor(true);});a.Controls.Add(cell);colors.Controls.Add(a);
            var b=new FlowLayoutPanel{Width=195,Height=75,FlowDirection=FlowDirection.TopDown,Margin=new Padding(0)};
            backgroundLabel=new Label{Text="背景颜色",Width=185,Height=26};b.Controls.Add(backgroundLabel);background=MakeButton("",185,delegate{ChooseColor(false);});b.Controls.Add(background);colors.Controls.Add(b);body.Controls.Add(colors);
            import=MakeButton("导入图片…",394,ImportImage);body.Controls.Add(import);
            imageName=AddLabel("",9,38);preview=new PictureBox{Width=394,Height=132,SizeMode=PictureBoxSizeMode.Zoom,Margin=new Padding(0,0,0,12)};body.Controls.Add(preview);
            reset=MakeButton("",394,delegate{app.Appearance.ResetColors(app.Palette.Dark);app.AppearanceUpdated();});body.Controls.Add(reset);
            var close=MakeButton("完成",394,delegate{Close();});body.Controls.Add(close);CancelButton=close;
            mode.SelectedIndexChanged+=delegate{app.Settings.AppearanceMode=mode.SelectedIndex;app.AppearanceUpdated();};
            app.AppearanceChanged+=Changed;FormClosed+=delegate{app.AppearanceChanged-=Changed;if(preview.Image!=null)preview.Image.Dispose();app.Save();};
            using(var g=CreateGraphics()){float scale=Math.Max(1,g.DpiX/96f);if(scale!=1){body.Scale(new SizeF(scale,scale));ClientSize=new Size((int)(460*scale),(int)(550*scale));}}
            MaximumSize=new Size(Width,Math.Min(Height,Screen.FromControl(this).WorkingArea.Height-30));RefreshView();
        }
        private Label AddLabel(string text,float size,int height){var l=new Label{Text=text,Font=new Font("Microsoft YaHei UI",size),Width=394,Height=height,Margin=new Padding(0,0,0,4)};body.Controls.Add(l);return l;}
        private Button MakeButton(string text,int width,Action action){var b=new Button{Text=text,Width=width,Height=36,FlatStyle=FlatStyle.Flat,Margin=new Padding(0,0,0,10),Cursor=Cursors.Hand};b.Click+=delegate{action();};return b;}
        private void Changed(object sender,EventArgs e){if(!IsDisposed)RefreshView();}
        private void Apply(Control c){c.BackColor=c is Button||c is ComboBox?app.ControlsPalette.Surface:app.ControlsPalette.Panel;c.ForeColor=app.ControlsPalette.Text;foreach(Control child in c.Controls)Apply(child);}
        private void RefreshView(){
            Apply(this);bool picture=app.Settings.AppearanceMode==1;themeLabel.Text="当前编辑："+(app.Palette.Dark?"暗色主题":"亮色主题")+"  ·  配色分别记忆，即时生效";
            description.Text=picture?"图片固定铺满屏幕。活细胞显露原图，衰亡余迹逐渐变淡，空白位置保留低透明度的毛玻璃底图。":"自由搭配细胞与背景颜色。衰亡细胞会逐渐融入背景。";
            cell.Enabled=!picture;cell.Text=picture?"使用对应位置的图片":WallpaperAppearance.Hex(app.Appearance.CellColor(app.Palette))+"  选择…";
            if(!picture)Swatch(cell,app.Appearance.CellColor(app.Palette));
            backgroundLabel.Text=picture?"底色 · "+(app.Appearance.AutomaticBackground(app.Palette.Dark)?"自动推荐":"手动设置"):"背景颜色";
            background.Text=WallpaperAppearance.Hex(app.Appearance.Background(app.Palette))+"  选择…";Swatch(background,app.Appearance.Background(app.Palette));
            import.Visible=imageName.Visible=preview.Visible=picture;
            imageName.Text=app.Appearance.HasImage?"已导入："+(app.Settings.ImageName??"壁纸图片"):string.IsNullOrEmpty(app.Appearance.Error)?"尚未导入图片，请选择 PNG、JPG、BMP 或 GIF。":app.Appearance.Error;
            reset.Text=picture?"使用推荐底色（当前主题）":"恢复默认配色（当前主题）";
            if(preview.Image!=null){preview.Image.Dispose();preview.Image=null;}
            if(picture&&app.Appearance.HasImage){using(var img=Image.FromFile(app.Appearance.ImagePath)){
                var thumb=new Bitmap(Math.Max(1,preview.Width),Math.Max(1,preview.Height));
                using(var g=Graphics.FromImage(thumb)){g.Clear(app.ControlsPalette.Panel);float scale=Math.Min(thumb.Width/(float)img.Width,thumb.Height/(float)img.Height);float w=img.Width*scale,h=img.Height*scale;g.DrawImage(img,(thumb.Width-w)/2,(thumb.Height-h)/2,w,h);}
                preview.Image=thumb;
            }}
        }
        private void Swatch(Button b,Color c){b.BackColor=c;b.ForeColor=WallpaperAppearance.Luminance(c)>.5?Color.FromArgb(25,30,25):Color.White;b.FlatAppearance.BorderColor=app.ControlsPalette.Muted;}
        private void ChooseColor(bool chooseCell){
            using(var dialog=new ColorDialog{FullOpen=true,AnyColor=true,Color=chooseCell?app.Appearance.CellColor(app.Palette):app.Appearance.Background(app.Palette)}){
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                if(chooseCell)app.Appearance.SetCellColor(app.Palette.Dark,dialog.Color);else app.Appearance.SetBackground(app.Palette.Dark,dialog.Color);
                app.AppearanceUpdated();
            }
        }
        private void ImportImage(){
            using(var dialog=new OpenFileDialog{Title="选择壁纸底图",Filter="图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*",CheckFileExists=true,Multiselect=false}){
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                try{app.Appearance.Import(dialog.FileName);mode.SelectedIndex=1;app.AppearanceUpdated();}
                catch(Exception e){MessageBox.Show(this,"未能导入图片，原来的设置已保留。\n"+e.Message,"导入图片",MessageBoxButtons.OK,MessageBoxIcon.Information);}
            }
        }
    }
}
