using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace vPad.Ui
{
    /// <summary>
    /// The tab strip is vSuite's TabStrip (as vXplorer's): the tabs share the room with arrows when they don't fit, and dragging one slides the
    /// others out of its way. This hears what the strip does: a click shows the file, its close button / middle-click closes it, a new place
    /// along the strip, a tab dropped on another vPad window (it moves there - an emptied window closes), or pulled out into the open (a
    /// new window there with it). The window's only tab moves the window (the strip does that).
    /// </summary>
    public sealed partial class MainWindow
    {
        void HookStrip()
        {
            _strip.Picked += t => { var d = t.Tag as DocTab; if (d != null) Activate(d); };
            _strip.CloseClicked += t => { var d = t.Tag as DocTab; if (d != null) { var unused = CloseTab(d); } };
            _strip.MenuRequested += t => { var d = t.Tag as DocTab; if (d != null) TabMenu(d); };
            _strip.Reordered += (t, from, to) =>
            {
                var d = t.Tag as DocTab;
                if (d == null || !_tabs.Remove(d)) return;
                _tabs.Insert(Math.Max(0, Math.Min(to, _tabs.Count)), d);
            };
            _strip.MovedTo += (t, other, index) =>
            {
                var d = t.Tag as DocTab;
                MainWindow target = All.FirstOrDefault(w => w._strip == other);
                if (d == null || target == null || target == this) return;
                Detach(d);
                target.Adopt(d, index);
                target.BringUp();
                if (_tabs.Count == 0) { _quietClose = true; Close(); }
            };
            _strip.TornOut += (t, screen) =>
            {
                var d = t.Tag as DocTab;
                if (d == null) return;
                // the new window's tab lands under the pointer, held where it was grabbed
                DpiScale dpi = VisualTreeHelper.GetDpi(this);
                double stripX = _strip.TranslatePoint(new Point(0, 0), this).X;
                MoveToNewWindow(d, new Point(screen.X / dpi.DpiScaleX - (stripX + _strip.GrabOffset), screen.Y / dpi.DpiScaleY - 20));
            };
        }
    }
}
