using System;
using System.Drawing;
using System.Windows.Forms;

namespace CellularGarden {
    internal sealed class InputShield {
        internal Rectangle Viewport, Panel;
        internal bool Allows(Point point){return Viewport.Contains(point)&&!Panel.Contains(point);}
        internal bool AllowsStroke(Point from,Point to){
            if(!Allows(from)||!Allows(to))return false;
            if(Panel.IsEmpty)return true;
            double enter=0,leave=1,dx=to.X-from.X,dy=to.Y-from.Y;
            return !(Clip(-dx,from.X-Panel.Left,ref enter,ref leave)&&
                Clip(dx,Panel.Right-from.X,ref enter,ref leave)&&
                Clip(-dy,from.Y-Panel.Top,ref enter,ref leave)&&
                Clip(dy,Panel.Bottom-from.Y,ref enter,ref leave));
        }
        private static bool Clip(double p,double q,ref double enter,ref double leave){
            if(p==0)return q>=0;double t=q/p;
            if(p<0){if(t>leave)return false;enter=Math.Max(enter,t);}
            else{if(t<enter)return false;leave=Math.Min(leave,t);}return true;
        }
        internal static Point ClampPanel(Point location,Size panel,Size viewport){return new Point(
            Math.Max(0,Math.Min(Math.Max(0,viewport.Width-panel.Width),location.X)),
            Math.Max(0,Math.Min(Math.Max(0,viewport.Height-panel.Height),location.Y)));}
    }

    internal sealed class SolidPanel:Panel {
        internal SolidPanel(){Cursor=Cursors.Default;SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
    }

    // A real hit-testable control: unlike a Label, this header owns its mouse capture.
    internal sealed class PanelDragHandle:Control {
        internal event Action DragStarted,DragFinished;
        internal event Action<Point> Dragged;
        private Point anchor;
        internal bool Dragging {get;private set;}
        internal PanelDragHandle(){Cursor=Cursors.SizeAll;TabStop=false;AccessibleName="拖动控制面板";SetStyle(ControlStyles.Selectable|ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
        protected override void OnPaint(PaintEventArgs e){
            e.Graphics.Clear(BackColor);
            using(var title=new Font("Microsoft YaHei UI",17))using(var note=new Font("Microsoft YaHei UI",8)){
                int inset=(int)(18*e.Graphics.DpiX/96f);
                TextRenderer.DrawText(e.Graphics,"细胞花园",title,new Point(inset,inset/2),ForeColor);
                TextRenderer.DrawText(e.Graphics,"⠿  拖动此处移动面板",note,new Point(inset,Height-(int)(24*e.Graphics.DpiY/96f)),ForeColor);
            }
            using(var pen=new Pen(Color.FromArgb(45,ForeColor)))e.Graphics.DrawLine(pen,0,Height-1,Width,Height-1);
        }
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();anchor=PointToScreen(e.Location);Dragging=true;Capture=true;if(DragStarted!=null)DragStarted();}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!Dragging)return;Point current=PointToScreen(e.Location);if(Dragged!=null)Dragged(new Point(current.X-anchor.X,current.Y-anchor.Y));}
        protected override void OnMouseUp(MouseEventArgs e){EndDrag();base.OnMouseUp(e);}
        protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture)EndDrag();base.OnMouseCaptureChanged(e);}
        internal void EndDrag(){if(!Dragging)return;Dragging=false;if(Capture)Capture=false;if(DragFinished!=null)DragFinished();}
    }
}
