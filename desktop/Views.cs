using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace CellularGarden {
    internal sealed class SpeedInput:NumericUpDown {
        protected override void UpdateEditText(){UserEdit=false;ChangingText=true;Text=Value.ToString("0.######");}
        internal void Commit(){ValidateEditText();}
    }
    internal sealed class Palette {
        internal Color Background,Panel,Surface,Text,Muted,Accent,Line,Alive,OnAccent;
        internal bool Dark;
        internal Palette(bool dark){
            Dark=dark;
            Background=ColorTranslator.FromHtml(dark?"#111a16":"#eff2e9");
            Panel=ColorTranslator.FromHtml(dark?"#1b2620":"#fafbf6");
            Surface=ColorTranslator.FromHtml(dark?"#29362d":"#e5eadf");
            Text=ColorTranslator.FromHtml(dark?"#e6ecdf":"#25382c");
            Muted=ColorTranslator.FromHtml(dark?"#a5b3a0":"#63735f");
            Accent=ColorTranslator.FromHtml(dark?"#b9e78c":"#426b35");
            Alive=ColorTranslator.FromHtml(dark?"#91b775":"#789465");
            Line=ColorTranslator.FromHtml(dark?"#3b493d":"#d3dccb");
            OnAccent=Background;
        }
        internal Color Trail(int age){float t=age/8f*(Dark?.28f:.24f);return Color.FromArgb((int)(Background.R+(Alive.R-Background.R)*t),(int)(Background.G+(Alive.G-Background.G)*t),(int)(Background.B+(Alive.B-Background.B)*t));}
    }
    internal sealed class GardenCanvas:Control {
        internal readonly GardenContext App;
        internal bool Interactive, Erase, MoveTool;
        private bool painting,panning;
        private Point previous, panStart;
        internal GardenCanvas(GardenContext app,bool interactive){App=app;Interactive=interactive;Dock=DockStyle.Fill;TabStop=interactive;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);Cursor=interactive?Cursors.Cross:Cursors.Default;AccessibleName="生命画布";}
        internal Viewport View {
            get {
                Rectangle screen=App.Screen.Bounds;
                Point location=PointToScreen(Point.Empty);
                return new Viewport(screen.Size,ClientSize,new Point(location.X-screen.X,location.Y-screen.Y),
                    App.Settings.Zoom,App.Settings.PanX,App.Settings.PanY);
            }
        }
        private Point Cell(Point p){Point cell=View.Cell(p);return new Point(cell.X-App.World.Left,cell.Y-App.World.Top);}
        protected override void OnPaint(PaintEventArgs e){
            Palette p=App.ControlsPalette;var g=e.Graphics;
            Rectangle screen=App.Screen.Bounds;Point location=PointToScreen(Point.Empty);
            App.Appearance.Render(g,ClientSize,screen.Size,new Point(location.X-screen.X,location.Y-screen.Y),View,App.World,App.Palette);
            if(Interactive){using(var f=new Font("Microsoft YaHei UI",10))using(var b=new SolidBrush(p.Muted)){string text="左键绘制 / 右键拖动画面 / 滚轮缩放     ·     Esc 返回壁纸     ·     Tab 收起面板";var sz=g.MeasureString(text,f);using(var bg=new SolidBrush(p.Panel))g.FillRectangle(bg,Math.Max(12,Width-sz.Width-36),Height-48,sz.Width+24,32);g.DrawString(text,f,b,Math.Max(24,Width-sz.Width-24),Height-41);}}
            base.OnPaint(e);
        }
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(!Interactive)return;Focus();Capture=true;panStart=e.Location;
            if(e.Button==MouseButtons.Right||e.Button==MouseButtons.Middle||MoveTool){panning=true;Cursor=Cursors.Hand;return;}
            if(e.Button!=MouseButtons.Left)return;App.Running=false;App.World.EnsureBounds(View.VisibleCells);painting=true;previous=Cell(e.Location);App.World.Paint(previous.X,previous.Y,Erase);App.World.Count();App.Changed();}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(panning){App.Settings.PanX=Math.Max(-4000,Math.Min(4000,App.Settings.PanX+e.X-panStart.X));App.Settings.PanY=Math.Max(-4000,Math.Min(4000,App.Settings.PanY+e.Y-panStart.Y));panStart=e.Location;App.Redraw();}else if(painting){Point p=Cell(e.Location);App.World.PaintLine(previous.X,previous.Y,p.X,p.Y,Erase);previous=p;App.Changed();}}
        protected override void OnMouseUp(MouseEventArgs e){Finish();base.OnMouseUp(e);}
        protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture)Finish();base.OnMouseCaptureChanged(e);}
        private void Finish(){bool wasPainting=painting;painting=false;panning=false;Capture=false;Cursor=MoveTool?Cursors.Hand:Cursors.Cross;if(wasPainting){App.World.Checkpoint();App.Changed();}}
        protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);if(!Interactive)return;App.Settings.Zoom=Math.Max(.5f,Math.Min(3f,App.Settings.Zoom+(e.Delta>0?.1f:-.1f)));App.Changed();}
    }
    internal sealed class WallpaperForm:Form {
        internal GardenCanvas Canvas;
        internal WallpaperForm(GardenContext app){Text="Cellular Garden · Desktop Surface";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Canvas=new GardenCanvas(app,false);Controls.Add(Canvas);}
        protected override bool ShowWithoutActivation{get{return true;}}
        protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=Native.WS_EX_TOOLWINDOW|Native.WS_EX_NOACTIVATE;return p;}}
        protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
    }
    internal sealed class HistoryView:Control {
        private GardenContext app;
        internal HistoryView(GardenContext context){app=context;Height=35;DoubleBuffered=true;}
        protected override void OnPaint(PaintEventArgs e){var values=app.World.History.ToArray();e.Graphics.Clear(app.ControlsPalette.Panel);if(values.Length<2)return;int max=Math.Max(1,values.Max());PointF[] pts=values.Select((x,i)=>new PointF(2+i*(Width-4f)/(values.Length-1),Height-3-x*(Height-6f)/max)).ToArray();e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var pen=new Pen(app.ControlsPalette.Accent,1.6f))e.Graphics.DrawLines(pen,pts);}
    }
    internal sealed class Controller:Form {
        private GardenContext app;
        internal GardenCanvas Canvas;
        private Panel dock;
        private FlowLayoutPanel content;
        private Label state,stats,ruleNote,zoomLabel;
        private Button play,draw,erase,move;
        private ComboBox theme,rule,monitor;
        private ComboBox speedUnit;
        private SpeedInput speedInput;
        private HistoryView chart;
        private bool syncing, dragging;
        private Point dragPoint;
        private float uiScale=1;
        private readonly ToolTip tip=new ToolTip();
        internal Controller(GardenContext context){
            app=context;Text="细胞花园 · 控制模式";AutoScaleMode=AutoScaleMode.None;Font=new Font("Microsoft YaHei UI",9);FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;KeyPreview=true;ShowInTaskbar=true;Icon=app.AppIcon;Bounds=app.Screen.WorkingArea;MinimumSize=new Size(700,600);
            using(var graphics=CreateGraphics())uiScale=Math.Max(1,graphics.DpiX/96f);
            Canvas=new GardenCanvas(app,true);Controls.Add(Canvas);
            dock=new Panel {Width=348,BorderStyle=BorderStyle.FixedSingle,Location=new Point(24,24),Anchor=AnchorStyles.Top|AnchorStyles.Left};Controls.Add(dock);dock.BringToFront();
            content=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(20,16,14,14)};dock.Controls.Add(content);
            var header=Label("细胞花园",19,false,32);header.Cursor=Cursors.SizeAll;header.MouseDown+=delegate(object s,MouseEventArgs e){dragging=true;dragPoint=Cursor.Position;header.Capture=true;};header.MouseMove+=delegate{if(!dragging)return;Point pos=Cursor.Position;dock.Left=Math.Max(0,Math.Min(ClientSize.Width-dock.Width,dock.Left+pos.X-dragPoint.X));dock.Top=Math.Max(0,Math.Min(ClientSize.Height-100,dock.Top+pos.Y-dragPoint.Y));dragPoint=pos;};header.MouseUp+=delegate{dragging=false;header.Capture=false;};header.MouseCaptureChanged+=delegate{if(!header.Capture)dragging=false;};
            Label("DESKTOP LAB  /  控制模式",8,true,22);
            state=Label("",9,true,28);
            FlowLayoutPanel transport=Row(42);play=Button(transport,"暂停演化",126,delegate{app.Toggle();});Button(transport,"下一代",76,delegate{app.Step();});Button(transport,"重置",58,delegate{app.World.Reset();app.Running=false;app.Changed();});
            var speedRow=Row(42);speedRow.Controls.Add(new Label {Text="速度",Width=36,Height=32,TextAlign=ContentAlignment.MiddleLeft});
            bool seconds=app.Settings.StepInterval>=1,speedSync=false;
            speedInput=new SpeedInput{Minimum=EvolutionSpeed.Minimum(seconds),Maximum=EvolutionSpeed.Maximum(seconds),DecimalPlaces=6,Increment=.1m,Value=EvolutionSpeed.DisplayValue(app.Settings.StepInterval,seconds),Width=132,AccessibleName="演化速度"};speedRow.Controls.Add(speedInput);
            speedUnit=new ComboBox{Width=98,DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,AccessibleName="速度单位"};speedUnit.Items.AddRange(new[]{"秒/代","代/秒"});speedUnit.SelectedIndex=seconds?0:1;speedRow.Controls.Add(speedUnit);
            tip.SetToolTip(speedInput,"直接输入数值，按 Enter 或离开输入框生效；支持小数。范围：每 60 秒一代至每秒 30 代。");
            speedInput.ValueChanged+=delegate{if(speedSync)return;app.Settings.StepSeconds=EvolutionSpeed.Interval(speedInput.Value,seconds);app.ResetClock();app.Save();};
            speedUnit.SelectedIndexChanged+=delegate{speedSync=true;seconds=speedUnit.SelectedIndex==0;speedInput.Minimum=EvolutionSpeed.Minimum(seconds);speedInput.Maximum=EvolutionSpeed.Maximum(seconds);speedInput.Value=EvolutionSpeed.DisplayValue(app.Settings.StepInterval,seconds);speedSync=false;};
            FormClosing+=delegate{speedInput.Commit();};
            stats=Label("",10,false,26);chart=new HistoryView(app){Width=286,Margin=new Padding(0,0,0,10)};content.Controls.Add(chart);
            var appearanceRow=Row(43);Button(appearanceRow,"外观个性化 · 纯色 / 图片",280,delegate{using(var dialog=new AppearanceDialog(app))dialog.ShowDialog(this);});
            Section("01   演化规则");rule=Combo(Rule.All.Select(x=>x.Name).ToArray(),app.Settings.RuleIndex);rule.SelectedIndexChanged+=delegate{if(syncing)return;app.World.RuleIndex=app.Settings.RuleIndex=rule.SelectedIndex;app.World.Checkpoint();app.Changed();};ruleNote=Label("",9,true,42);
            Check("边缘循环",app.Settings.Wrap,delegate(bool v){app.World.Wrap=app.Settings.Wrap=v;app.World.Checkpoint();app.Changed();});
            Section("02   播种图案");var patterns=Row(39);Button(patterns,"滑翔机",135,delegate{app.Pattern(0);});Button(patterns,"脉冲星",135,delegate{app.Pattern(1);});var patterns2=Row(39);Button(patterns2,"滑翔机枪",135,delegate{app.Pattern(2);});Button(patterns2,"R 五连块",135,delegate{app.Pattern(3);});var actions=Row(39);Button(actions,"随机播种",135,delegate{app.RandomizeVisible();});Button(actions,"清空画布",135,delegate{app.Running=false;app.World.Clear();app.Changed();});
            Section("03   画笔与视角");var tools=Row(39);draw=Button(tools,"＋ 画笔",86,delegate{Brush(0);});erase=Button(tools,"－ 橡皮",86,delegate{Brush(1);});move=Button(tools,"↔ 移动",86,delegate{Brush(2);});
            var zoom=Row(37);Button(zoom,"－",38,delegate{app.Settings.Zoom=Math.Max(.5f,app.Settings.Zoom-.1f);app.Changed();});zoomLabel=new Label {Width=65,Height=30,TextAlign=ContentAlignment.MiddleCenter};zoom.Controls.Add(zoomLabel);Button(zoom,"＋",38,delegate{app.Settings.Zoom=Math.Min(3f,app.Settings.Zoom+.1f);app.Changed();});Button(zoom,"视角归位",120,delegate{app.Settings.Zoom=1;app.Settings.PanX=app.Settings.PanY=0;app.Changed();});
            Check("生命余迹",app.Settings.Trails,delegate(bool v){app.Settings.Trails=v;app.Redraw();});Check("显示网格",app.Settings.Grid,delegate(bool v){app.Settings.Grid=v;app.Redraw();});
            Section("04   壁纸偏好");theme=Combo(new[]{"主题 · 跟随系统","主题 · 亮色","主题 · 暗色"},app.Settings.Theme);theme.AccessibleName="壁纸主题";theme.SelectedIndexChanged+=delegate{app.Settings.Theme=theme.SelectedIndex;app.UpdateTheme();};
            string[] screens=System.Windows.Forms.Screen.AllScreens.Select((x,i)=>"显示器 "+(i+1)+(x.Primary?" · 主屏":"")+"  "+x.Bounds.Width+" × "+x.Bounds.Height).ToArray();int screenIndex=Array.FindIndex(System.Windows.Forms.Screen.AllScreens,x=>x.DeviceName==app.Screen.DeviceName);monitor=Combo(screens,Math.Max(0,screenIndex));monitor.SelectedIndexChanged+=delegate{if(monitor.SelectedIndex<0)return;app.Settings.Monitor=System.Windows.Forms.Screen.AllScreens[monitor.SelectedIndex].DeviceName;app.Reattach();Bounds=app.Screen.WorkingArea;};
            Section("05   自动播种");
            var repeatDelay=Duration("持续时间",app.Settings.RepeatDelay,3600,"秒",delegate(int v){app.Settings.RepeatSeconds=v;});
            var repeat=Check("静止 / 重复后播种",app.Settings.RenewRepeating??app.Settings.Renew,delegate(bool v){app.Settings.RenewRepeating=v;repeatDelay.Enabled=v;});
            content.Controls.SetChildIndex(repeat,content.Controls.GetChildIndex(repeatDelay.Parent));repeatDelay.Enabled=repeat.Checked;
            var timedDelay=Duration("更新间隔",app.Settings.TimedDelay,1440,"分钟",delegate(int v){app.Settings.RenewMinutes=v;});
            var timed=Check("定时重新播种",app.Settings.RenewTimed??app.Settings.Renew,delegate(bool v){app.Settings.RenewTimed=v;timedDelay.Enabled=v;});
            content.Controls.SetChildIndex(timed,content.Controls.GetChildIndex(timedDelay.Parent));timedDelay.Enabled=timed.Checked;
            Label("仅在壁纸运行时计时；两项独立，任一满足即更新。\n暂停、全屏暂停和控制模式均不计时。",8,true,40);
            Check("全屏应用时暂停演化",app.Settings.PauseFullscreen,delegate(bool v){app.Settings.PauseFullscreen=v;});
            var bottom=Row(41);Button(bottom,"返回壁纸   Esc",174,delegate{Close();});Button(bottom,"退出程序",96,delegate{app.Shutdown();});
            Label("托盘双击 / Ctrl+Alt+G 再次打开\nSpace 播放暂停 · N 下一代 · R 重置",8,true,40);
            if(uiScale!=1)dock.Scale(new SizeF(uiScale,uiScale));
            rule.AccessibleName="演化规则";monitor.AccessibleName="壁纸显示器";
            Resize+=delegate{LayoutPanel();};FormClosed+=delegate{app.ControllerClosed();tip.Dispose();};LayoutPanel();ApplyTheme();RefreshInfo();
        }
        private void LayoutPanel(){dock.Height=Math.Max(300,ClientSize.Height-(int)(48*uiScale));dock.Left=Math.Max(0,Math.Min(dock.Left,ClientSize.Width-dock.Width));dock.Top=Math.Max(0,Math.Min(dock.Top,ClientSize.Height-dock.Height));}
        private Label Label(string text,float size,bool muted,int height){var l=new Label{Text=text,Font=new Font("Microsoft YaHei UI",size),Width=286,Height=height,Margin=new Padding(0,0,0,3),Tag=muted?"muted":"text"};content.Controls.Add(l);return l;}
        private void Section(string title){var l=Label(title,9,false,30);l.Padding=new Padding(0,8,0,0);}
        private FlowLayoutPanel Row(int height){var row=new FlowLayoutPanel{Width=290,Height=height,Margin=new Padding(0),Padding=new Padding(0),WrapContents=false};content.Controls.Add(row);return row;}
        private Button Button(FlowLayoutPanel row,string text,int width,Action action){var b=new Button{Text=text,Width=width,Height=32,Margin=new Padding(0,0,7,0),FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand,UseVisualStyleBackColor=false};b.FlatAppearance.BorderSize=1;b.Click+=delegate{action();};row.Controls.Add(b);return b;}
        private ComboBox Combo(string[] items,int selected){var c=new ComboBox{Width=282,DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,Margin=new Padding(0,0,0,8)};c.Items.AddRange(items);c.SelectedIndex=selected;content.Controls.Add(c);return c;}
        private CheckBox Check(string text,bool value,Action<bool> action){var c=new CheckBox{Text=text,Checked=value,AutoSize=false,Width=282,Height=26,Margin=new Padding(0,0,0,2),Cursor=Cursors.Hand};c.CheckedChanged+=delegate{action(c.Checked);};content.Controls.Add(c);return c;}
        private NumericUpDown Duration(string text,int value,int max,string unit,Action<int> action){var row=Row(37);row.Controls.Add(new Label{Text=text,Width=84,Height=30,TextAlign=ContentAlignment.MiddleLeft});var input=new NumericUpDown{Minimum=1,Maximum=max,Value=value,Width=112,AccessibleName=text+"（"+unit+"）"};input.ValueChanged+=delegate{action((int)input.Value);};row.Controls.Add(input);row.Controls.Add(new Label{Text=unit,Width=60,Height=30,TextAlign=ContentAlignment.MiddleLeft});return input;}
        private void Brush(int tool){Canvas.Erase=tool==1;Canvas.MoveTool=tool==2;Canvas.Cursor=tool==2?Cursors.Hand:Cursors.Cross;StyleBrush();Canvas.Focus();}
        private void StyleBrush(){draw.BackColor=!Canvas.Erase&&!Canvas.MoveTool?app.ControlsPalette.Accent:app.ControlsPalette.Surface;erase.BackColor=Canvas.Erase?app.ControlsPalette.Accent:app.ControlsPalette.Surface;move.BackColor=Canvas.MoveTool?app.ControlsPalette.Accent:app.ControlsPalette.Surface;draw.ForeColor=!Canvas.Erase&&!Canvas.MoveTool?app.ControlsPalette.OnAccent:app.ControlsPalette.Text;erase.ForeColor=Canvas.Erase?app.ControlsPalette.OnAccent:app.ControlsPalette.Text;move.ForeColor=Canvas.MoveTool?app.ControlsPalette.OnAccent:app.ControlsPalette.Text;}
        internal void ApplyTheme(){Apply(this);StyleBrush();RefreshInfo();Canvas.Invalidate();}
        private void Apply(Control c){c.BackColor=c is Button||c is ComboBox?app.ControlsPalette.Surface:app.ControlsPalette.Panel;c.ForeColor=(string)c.Tag=="muted"?app.ControlsPalette.Muted:app.ControlsPalette.Text;if(c is Button)((Button)c).FlatAppearance.BorderColor=app.ControlsPalette.Line;foreach(Control child in c.Controls)Apply(child);}
        internal void RefreshInfo(){
            if(IsDisposed)return;syncing=true;
            play.Text=app.Running?"Ⅱ  暂停演化":"▶  开始演化";play.BackColor=app.ControlsPalette.Accent;play.ForeColor=app.ControlsPalette.OnAccent;
            state.Text=app.Attached?"●  已连接桌面  /  "+(app.Running?"自动演化中":"已暂停"):"○  预览模式 · 桌面连接待重试";
            tip.SetToolTip(state,app.LastError);stats.Text="第 "+app.World.Generation.ToString("N0")+" 代    ·    "+app.World.Population.ToString("N0")+" 个细胞    ·    "+(100.0*app.World.Population/app.World.Cells.Length).ToString("F1")+"%";
            ruleNote.Text=Rule.All[app.World.RuleIndex].Code+"\n"+Rule.All[app.World.RuleIndex].Description;zoomLabel.Text=(app.Settings.Zoom*100).ToString("F0")+"%";chart.Invalidate();syncing=false;
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData){
            if(keyData==Keys.Escape){Close();return true;}
            if(speedInput.ContainsFocus&&keyData==Keys.Enter){speedInput.Commit();return true;}
            if(theme.Focused||rule.Focused||monitor.Focused||speedUnit.Focused||content.Controls.OfType<FlowLayoutPanel>().Any(row=>row.Controls.OfType<NumericUpDown>().Any(input=>input.ContainsFocus)))return base.ProcessCmdKey(ref msg,keyData);
            if(keyData==Keys.Tab){dock.Visible=!dock.Visible;Canvas.Focus();return true;}
            if(keyData==Keys.Space){app.Toggle();return true;}if(keyData==Keys.N){app.Step();return true;}if(keyData==Keys.R){app.Running=false;app.World.Reset();app.Changed();return true;}return base.ProcessCmdKey(ref msg,keyData);
        }
    }
}
