using System.Windows;
using System.Windows.Media;

namespace vPad.Ui
{
    /// <summary>
    /// vPad's icon drawing (AppIcon.Drawer, set in Program), on a 64 x 64 grid like vTerm's window and vXplorer's folder: a tall spiral
    /// notepad - its outline in the Main Accent filled with the theme's background, rings along the top in the text colour - holding two
    /// lines of text and vShell's three-dot mark as the third line (Main Accent, Secondary Accent, text). small: heavier lines, two rings,
    /// bigger dots for 16-24 px. `vPad.exe --make-icon vpad.ico` writes it in vShell's default colours for the exe's own icon.
    /// </summary>
    public static class Brand
    {
        public static Drawing Draw(Color main, Color secondary, Color text, Color background, bool small)
        {
            double stroke = small ? 5.5 : 4.2;
            var g = new DrawingGroup();
            // the pad
            var pen = new Pen(AppIcon.Brush(main), stroke) { LineJoin = PenLineJoin.Round };
            pen.Freeze();
            g.Children.Add(new GeometryDrawing(AppIcon.Brush(background), pen, new RectangleGeometry(new Rect(12.5, 12.5, 39, 45), 7, 7)));
            // the rings over its top edge
            var ring = new Pen(AppIcon.Brush(text), small ? 5 : 3.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            ring.Freeze();
            double[] rings = small ? new[] { 24.0, 40.0 } : new[] { 22.5, 32.0, 41.5 };
            foreach (double x in rings)
                g.Children.Add(new GeometryDrawing(null, ring, new LineGeometry(new Point(x, small ? 6.5 : 7.5), new Point(x, 16.5))));
            // two lines of text
            var line = new Pen(AppIcon.Brush(text), small ? 5.5 : 4.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            line.Freeze();
            double y1 = small ? 28 : 27.5, y2 = small ? 37.5 : 36;
            g.Children.Add(new GeometryDrawing(null, line, new LineGeometry(new Point(21, y1), new Point(43, y1))));
            g.Children.Add(new GeometryDrawing(null, line, new LineGeometry(new Point(21, y2), new Point(small ? 34 : 35.5, y2))));
            // the third line: vShell's three dots
            double r = small ? 3.6 : 3, y = small ? 47.5 : 46.5, x0 = 23, step = small ? 8.4 : 8;
            g.Children.Add(new GeometryDrawing(AppIcon.Brush(main), null, new EllipseGeometry(new Point(x0, y), r, r)));
            g.Children.Add(new GeometryDrawing(AppIcon.Brush(secondary), null, new EllipseGeometry(new Point(x0 + step, y), r, r)));
            g.Children.Add(new GeometryDrawing(AppIcon.Brush(text), null, new EllipseGeometry(new Point(x0 + step * 2, y), r, r)));
            g.Freeze();
            return g;
        }
    }
}
