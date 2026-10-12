using System;
using System.Drawing;

namespace CellularGarden {
    // Camera coordinates are independent of the current simulation's storage bounds.
    internal sealed class Viewport {
        internal readonly float CellSize;
        internal readonly PointF Origin;
        internal readonly Rectangle VisibleCells;

        internal Viewport(Size screen, Size viewport, Point screenOffset, float zoom, float panX, float panY) {
            CellSize=Math.Max(2f,Math.Max(screen.Width/160f,screen.Height/90f)*zoom);
            Origin=new PointF((screen.Width-160*CellSize)/2+panX-screenOffset.X,
                              (screen.Height-90*CellSize)/2+panY-screenOffset.Y);
            int left=(int)Math.Floor(-Origin.X/CellSize);
            int top=(int)Math.Floor(-Origin.Y/CellSize);
            int right=(int)Math.Ceiling((viewport.Width-Origin.X)/CellSize);
            int bottom=(int)Math.Ceiling((viewport.Height-Origin.Y)/CellSize);
            VisibleCells=Rectangle.FromLTRB(left,top,right,bottom);
        }
        internal Point Cell(Point pixel) {
            return new Point((int)Math.Floor((pixel.X-Origin.X)/CellSize),
                             (int)Math.Floor((pixel.Y-Origin.Y)/CellSize));
        }
    }
}
