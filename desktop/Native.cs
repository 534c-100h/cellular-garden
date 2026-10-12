using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CellularGarden {
    internal static class Native {
        internal delegate bool EnumProc(IntPtr hwnd,IntPtr arg);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr FindWindow(string cls,string title);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback,IntPtr arg);
        [DllImport("user32.dll")] internal static extern IntPtr SendMessageTimeout(IntPtr hwnd,uint msg,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
        [DllImport("user32.dll",SetLastError=true)] internal static extern IntPtr SetParent(IntPtr hwnd,IntPtr parent);
        [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll",EntryPoint="GetWindowLongW")] internal static extern int GetWindowLong(IntPtr hwnd,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLongW",SetLastError=true)] internal static extern int SetWindowLong(IntPtr hwnd,int index,int value);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int w,int h,uint flags);
        [DllImport("user32.dll")] internal static extern bool SetLayeredWindowAttributes(IntPtr hwnd,uint key,byte alpha,uint flags);
        [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd,int id);
        [DllImport("user32.dll")] internal static extern bool ScreenToClient(IntPtr hwnd,ref Point point);
        [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] internal static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr hwnd,uint msg,IntPtr w,IntPtr l);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
        [StructLayout(LayoutKind.Sequential)] internal struct RECT {public int Left,Top,Right,Bottom;}
        internal const int GWL_STYLE=-16,GWL_EXSTYLE=-20,WS_CHILD=0x40000000,WS_POPUP=unchecked((int)0x80000000),WS_EX_LAYERED=0x80000,WS_EX_NOACTIVATE=0x08000000,WS_EX_TOOLWINDOW=0x80;
    }
    internal sealed class DesktopHost {
        internal IntPtr Parent, IconLayer;
        internal bool Raised;
        internal string LastError="";
        internal bool Discover(){
            Parent=IntPtr.Zero;IconLayer=IntPtr.Zero;Raised=false;
            IntPtr progman=Native.FindWindow("Progman",null),result;
            if(progman==IntPtr.Zero){LastError="找不到 Windows 桌面，稍后会自动重试。";return false;}
            Native.SendMessageTimeout(progman,0x052c,IntPtr.Zero,IntPtr.Zero,2,1000,out result);
            Native.EnumWindows(delegate(IntPtr top,IntPtr unused){
                IntPtr icons=Native.FindWindowEx(top,IntPtr.Zero,"SHELLDLL_DefView",null);
                if(icons==IntPtr.Zero)return true;
                IntPtr childWorker=Native.FindWindowEx(top,IntPtr.Zero,"WorkerW",null);
                if(childWorker!=IntPtr.Zero){Parent=top;IconLayer=icons;Raised=true;return false;}
                IntPtr worker=Native.FindWindowEx(IntPtr.Zero,top,"WorkerW",null);
                if(worker!=IntPtr.Zero){Parent=worker;return false;}
                return true;
            },IntPtr.Zero);
            if(Parent==IntPtr.Zero){LastError="当前桌面层暂时不可用。可使用预览，并通过托盘重新连接。";return false;}
            return true;
        }
        internal bool Attach(Form form,Rectangle bounds){
            if(!Discover())return false;
            IntPtr hwnd=form.Handle;
            Native.SetWindowLong(hwnd,Native.GWL_STYLE,(Native.GetWindowLong(hwnd,Native.GWL_STYLE)&~Native.WS_POPUP)|Native.WS_CHILD);
            int ex=Native.GetWindowLong(hwnd,Native.GWL_EXSTYLE)|Native.WS_EX_TOOLWINDOW|Native.WS_EX_NOACTIVATE;
            if(Raised)ex|=Native.WS_EX_LAYERED;
            Native.SetWindowLong(hwnd,Native.GWL_EXSTYLE,ex);
            Native.SetParent(hwnd,Parent);
            if(Native.GetParent(hwnd)!=Parent){LastError="无法连接到桌面层（"+Marshal.GetLastWin32Error()+"）。";return false;}
            if(Raised)Native.SetLayeredWindowAttributes(hwnd,0,255,2);
            Position(form,bounds);LastError="";return true;
        }
        internal void Position(Form form,Rectangle bounds){
            if(!Healthy(form))return;
            Point p=bounds.Location;Native.ScreenToClient(Parent,ref p);
            Native.SetWindowPos(form.Handle,Raised?IconLayer:IntPtr.Zero,p.X,p.Y,bounds.Width,bounds.Height,0x0010|0x0020|0x0040);
        }
        internal bool Healthy(Form form){return form!=null&&!form.IsDisposed&&Native.IsWindow(Parent)&&Native.GetParent(form.Handle)==Parent&&(!Raised||Native.IsWindow(IconLayer));}
    }
}
