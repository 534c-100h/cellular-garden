using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CellularGarden {
    internal static class Program {
        internal const string SignalTitle="CellularGarden.Desktop.MessageWindow.v1";
        internal static readonly uint ShowMessage=Native.RegisterWindowMessage("CellularGarden.ShowController.v1");
        [STAThread] private static void Main(string[] args){
            if(args.Contains("--self-test")){try{SelfTests.Run();Environment.ExitCode=0;}catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt"),e.ToString());Environment.ExitCode=1;}return;}
            bool created;using(var mutex=new Mutex(true,"Local\\CellularGarden.Desktop.v1",out created)){
                if(!created){IntPtr other=Native.FindWindow(null,SignalTitle);if(other!=IntPtr.Zero)Native.PostMessage(other,ShowMessage,IntPtr.Zero,IntPtr.Zero);return;}
                try{Native.SetProcessDpiAwarenessContext(new IntPtr(-4));}catch{Native.SetProcessDPIAware();}
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException+=delegate(object sender,ThreadExceptionEventArgs e){Log(e.Exception);MessageBox.Show("细胞花园遇到错误："+e.Exception.Message,"细胞花园",MessageBoxButtons.OK,MessageBoxIcon.Warning);};
                try{Application.Run(new GardenContext(args.Contains("--preview"),args.Contains("--control")));}catch(Exception e){Log(e);MessageBox.Show(e.Message,"细胞花园无法启动");}
            }
        }
        internal static void Log(Exception e){try{File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"error.log"),DateTime.Now.ToString("s")+" "+e+Environment.NewLine);}catch{}}
    }
    internal sealed class MessageWindow:Form {
        private GardenContext app;
        internal MessageWindow(GardenContext context){app=context;Text=Program.SignalTitle;ShowInTaskbar=false;FormBorderStyle=FormBorderStyle.FixedToolWindow;Opacity=0;Bounds=new Rectangle(-10000,-10000,1,1);var h=Handle;}
        protected override void WndProc(ref Message m){if(m.Msg==Program.ShowMessage||m.Msg==0x312){if(m.Msg==0x312&&m.WParam.ToInt32()==2)app.Toggle();else app.OpenController();return;}base.WndProc(ref m);}
    }
    internal sealed class GardenContext:ApplicationContext {
        internal Settings Settings;
        internal Universe World;
        internal Palette Palette;
        internal WallpaperAppearance Appearance;
        internal event EventHandler AppearanceChanged;
        internal Icon AppIcon;
        internal bool Running;
        internal string LastError="";
        private bool dark,quitting,locked,suspended,previewOnly;
        private WallpaperForm wallpaper;
        private Controller controller;
        private DesktopHost host=new DesktopHost();
        private MessageWindow messages;
        private NotifyIcon tray;
        private ToolStripMenuItem playback;
        private System.Windows.Forms.Timer tick,maintenance;
        private Stopwatch clock=Stopwatch.StartNew();
        private double last,accumulator;
        private int maintenanceCount;
        private readonly RenewalClock renewal=new RenewalClock();
        internal Screen Screen {get{return Screen.AllScreens.FirstOrDefault(x=>x.DeviceName==Settings.Monitor)??Screen.PrimaryScreen;}}
        internal bool Attached {get{return wallpaper!=null&&host.Healthy(wallpaper);}}
        internal GardenContext(bool preview,bool control){
            previewOnly=preview;Settings=Settings.Load();World=new Universe(160,90);World.RuleIndex=Settings.RuleIndex;World.Wrap=Settings.Wrap;
            try{World.SetBounds(Settings.WorldBounds);World.Restore(Settings.Cells,Settings.Initial,Settings.Generation);}catch{SeedVisible();}Running=Settings.Running;
            dark=IsDark();Palette=new Palette(dark);Appearance=new WallpaperAppearance(Settings,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"wallpaper-image.png"));AppIcon=MakeIcon();messages=new MessageWindow(this);
            bool hotkey=Native.RegisterHotKey(messages.Handle,1,0x4003,(uint)Keys.G);Native.RegisterHotKey(messages.Handle,2,0x4003,(uint)Keys.Space);
            var menu=new ContextMenuStrip();menu.Items.Add("打开控制模式   Ctrl+Alt+G",null,delegate{OpenController();});playback=new ToolStripMenuItem("暂停演化",null,delegate{Toggle();});menu.Items.Add(playback);menu.Items.Add("重新连接桌面",null,delegate{Reattach();});menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出细胞花园",null,delegate{Shutdown();});
            tray=new NotifyIcon{Icon=AppIcon,Visible=true,Text="细胞花园 · 双击进入控制模式",ContextMenuStrip=menu};tray.DoubleClick+=delegate{OpenController();};
            if(!preview)Reattach();
            tick=new System.Windows.Forms.Timer{Interval=30};tick.Tick+=delegate{Tick();};tick.Start();maintenance=new System.Windows.Forms.Timer{Interval=2000};maintenance.Tick+=delegate{Maintain();};maintenance.Start();
            SystemEvents.SessionSwitch+=SessionChanged;SystemEvents.PowerModeChanged+=PowerChanged;
            if(preview||control||!Attached)OpenController();
            if(!hotkey)tray.ShowBalloonTip(4500,"细胞花园已启动","快捷键被占用；请双击托盘中的绿色细胞图标打开控制模式。",ToolTipIcon.Info);
            else if(!preview)tray.ShowBalloonTip(4000,"细胞花园已启动","壁纸自动演化中。双击绿色托盘图标或按 Ctrl+Alt+G 打开控制面板。",ToolTipIcon.Info);
            Changed();WriteStatus();
        }
        private Icon MakeIcon(){using(var bitmap=new Bitmap(32,32)){using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Transparent);using(var bg=new SolidBrush(Color.FromArgb(37,54,35)))g.FillEllipse(bg,0,0,32,32);using(var cell=new SolidBrush(Color.FromArgb(185,231,140))){g.FillRectangle(cell,14,5,6,6);g.FillRectangle(cell,21,12,6,6);g.FillRectangle(cell,7,19,6,6);g.FillRectangle(cell,14,19,6,6);g.FillRectangle(cell,21,19,6,6);}}IntPtr handle=bitmap.GetHicon();try{using(var borrowed=Icon.FromHandle(handle))return (Icon)borrowed.Clone();}finally{Native.DestroyIcon(handle);}}}
        internal void Reattach(){
            if(quitting)return;previewOnly=false;
            if(wallpaper==null||wallpaper.IsDisposed||!Native.IsWindow(wallpaper.Handle)){if(wallpaper!=null)wallpaper.Dispose();wallpaper=new WallpaperForm(this);}
            wallpaper.Bounds=Screen.Bounds;
            if(host.Attach(wallpaper,Screen.Bounds)){wallpaper.Show();host.Position(wallpaper,Screen.Bounds);}else{wallpaper.Hide();LastError=host.LastError;}
            Changed();
        }
        internal void OpenController(){if(quitting)return;if(controller==null||controller.IsDisposed){controller=new Controller(this);controller.Show();}else{controller.Show();controller.WindowState=FormWindowState.Normal;}controller.Activate();controller.BringToFront();ResetClock();WriteStatus();}
        internal void ControllerClosed(){controller=null;Save();ResetClock();WriteStatus();}
        internal void Toggle(){Running=!Running;ResetClock();Changed();Save();}
        internal void Step(){Running=false;World.Step();Changed();ResetClock();}
        internal void Pattern(int index){World.SetBounds(new Rectangle(0,0,160,90));World.Pattern(index);Running=false;Settings.Zoom=1;Settings.PanX=Settings.PanY=0;Changed();}
        private void SeedVisible(){
            Viewport view;
            if(controller!=null&&!controller.IsDisposed)view=controller.Canvas.View;
            else if(wallpaper!=null&&!wallpaper.IsDisposed&&wallpaper.Visible)view=wallpaper.Canvas.View;
            else view=new Viewport(Screen.Bounds.Size,Screen.Bounds.Size,Point.Empty,Settings.Zoom,Settings.PanX,Settings.PanY);
            World.Randomize(view.VisibleCells);
            renewal.Reset();
        }
        internal void RandomizeVisible(){SeedVisible();ResetClock();Changed();}
        internal void ResetClock(){last=clock.Elapsed.TotalMilliseconds;accumulator=0;}
        internal void Changed(){Redraw();if(controller!=null)controller.RefreshInfo();if(playback!=null)playback.Text=Running?"暂停演化   Ctrl+Alt+Space":"开始演化   Ctrl+Alt+Space";}
        internal void Redraw(){if(wallpaper!=null&&!wallpaper.IsDisposed)wallpaper.Canvas.Invalidate();if(controller!=null&&!controller.IsDisposed)controller.Canvas.Invalidate();}
        private void Tick(){
            double now=clock.Elapsed.TotalMilliseconds,delta=Math.Min(200,now-last);last=now;
            if(!Running||locked||suspended){accumulator=0;return;}accumulator+=delta;double interval=1000.0*Settings.StepInterval;bool updated=false;
            while(accumulator>=interval){World.Step();accumulator-=interval;updated=true;}
            if(renewal.Advance(delta/1000,controller==null,World.Repeating>0,Settings)){SeedVisible();updated=true;}
            if(updated){
                Redraw();if(controller!=null)controller.RefreshInfo();
            }
        }
        private void Maintain(){
            if(quitting)return;UpdateTheme();
            if(!previewOnly){if(!Attached)Reattach();else host.Position(wallpaper,Screen.Bounds);}
            suspended=Settings.PauseFullscreen&&controller==null&&IsFullscreenApp();
            if(++maintenanceCount%5==0)Save();WriteStatus();
        }
        private bool IsFullscreenApp(){IntPtr fg=Native.GetForegroundWindow();if(fg==IntPtr.Zero||fg==host.Parent||fg==host.IconLayer||fg==Native.FindWindow("Progman",null)||Native.FindWindowEx(fg,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero||(wallpaper!=null&&fg==wallpaper.Handle)||Native.IsIconic(fg))return false;Native.RECT r;if(!Native.GetWindowRect(fg,out r))return false;Rectangle screen=Screen.Bounds;return r.Left<=screen.Left&&r.Top<=screen.Top&&r.Right>=screen.Right&&r.Bottom>=screen.Bottom;}
        internal Palette ControlsPalette {get{return Appearance.ControlPalette(Palette);}}
        internal void AppearanceUpdated(){if(controller!=null)controller.ApplyTheme();if(AppearanceChanged!=null)AppearanceChanged(this,EventArgs.Empty);Redraw();Save();}
        internal void UpdateTheme(){bool next=IsDark();if(next==dark)return;dark=next;Palette=new Palette(dark);if(controller!=null)controller.ApplyTheme();if(AppearanceChanged!=null)AppearanceChanged(this,EventArgs.Empty);Redraw();}
        private bool IsDark(){if(Settings.Theme!=0)return Settings.Theme==2;try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")){return Convert.ToInt32(key==null?1:key.GetValue("SystemUsesLightTheme",1))==0;}}catch{return false;}}
        private void SessionChanged(object sender,SessionSwitchEventArgs e){if(messages.IsDisposed)return;messages.BeginInvoke((Action)delegate{locked=e.Reason==SessionSwitchReason.SessionLock;if(e.Reason==SessionSwitchReason.SessionUnlock){locked=false;Reattach();}ResetClock();});}
        private void PowerChanged(object sender,PowerModeChangedEventArgs e){if(messages.IsDisposed)return;messages.BeginInvoke((Action)delegate{if(e.Mode==PowerModes.Suspend)locked=true;if(e.Mode==PowerModes.Resume){locked=false;Reattach();}ResetClock();});}
        internal void Save(){try{Settings.RuleIndex=World.RuleIndex;Settings.Wrap=World.Wrap;Settings.Running=Running;Settings.Cells=(byte[])World.Cells.Clone();Settings.Initial=World.GetInitial();Settings.Generation=World.Generation;Settings.WorldWidth=World.Width;Settings.WorldHeight=World.Height;Settings.WorldLeft=World.Left;Settings.WorldTop=World.Top;Settings.Save();}catch(Exception e){LastError="偏好保存失败："+e.Message;Program.Log(e);}}
        private void WriteStatus(){try{string data="{\"attached\":"+Attached.ToString().ToLowerInvariant()+",\"running\":"+Running.ToString().ToLowerInvariant()+",\"controlMode\":"+(controller!=null).ToString().ToLowerInvariant()+",\"darkTheme\":"+dark.ToString().ToLowerInvariant()+",\"themePreference\":"+Settings.Theme+",\"generation\":"+World.Generation+",\"population\":"+World.Population+",\"suspended\":"+(locked||suspended).ToString().ToLowerInvariant()+",\"raisedDesktop\":"+host.Raised.ToString().ToLowerInvariant()+",\"wallpaperHandle\":"+(wallpaper==null?0:wallpaper.Handle.ToInt64())+",\"parentHandle\":"+host.Parent.ToInt64()+",\"updatedUtc\":\""+DateTime.UtcNow.ToString("o")+"\"}";File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"desktop-status.json"),data);}catch{}}
        internal void Shutdown(){if(quitting)return;quitting=true;Save();tick.Stop();maintenance.Stop();SystemEvents.SessionSwitch-=SessionChanged;SystemEvents.PowerModeChanged-=PowerChanged;Native.UnregisterHotKey(messages.Handle,1);Native.UnregisterHotKey(messages.Handle,2);if(controller!=null)controller.Close();if(wallpaper!=null)wallpaper.Dispose();tray.Visible=false;tray.Dispose();messages.Dispose();tick.Dispose();maintenance.Dispose();AppIcon.Dispose();Appearance.Dispose();ExitThread();}
    }
}
