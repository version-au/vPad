using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace vPad.Ui
{
    /// <summary>
    /// Find and replace, floating over the top right of the text (Ctrl+F / Ctrl+H): every match lit up, "3 of 12", Enter / F3 for the
    /// next, Shift+Enter / Shift+F3 the one before, match case / whole word / regular expression, Replace and Replace all (one undo).
    /// Works on the window's active tab (Target).
    /// </summary>
    public sealed class FindBar : Border
    {
        readonly TextBox _find = new TextBox { MinWidth = 210 };
        readonly TextBox _replace = new TextBox { MinWidth = 210 };
        readonly TextBlock _count = new TextBlock { Width = 70, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
        readonly ToggleButton _case = Toggle("Aa", "Match case"), _word = Toggle("ab", "Whole words"), _regex = Toggle(".*", "Regular expression");
        readonly ToggleButton _more;
        readonly StackPanel _replaceRow;
        readonly Highlighter _marks = new Highlighter();
        TextEditor _target;
        List<Match> _found = new List<Match>();
        string _error;

        public FindBar()
        {
            Visibility = Visibility.Collapsed;
            HorizontalAlignment = HorizontalAlignment.Right;
            VerticalAlignment = VerticalAlignment.Top;
            Margin = new Thickness(0, 8, 22, 0);
            Padding = new Thickness(8);
            CornerRadius = new CornerRadius(8);
            BorderThickness = new Thickness(1);
            SetResourceReference(BackgroundProperty, "vx.Popup");
            SetResourceReference(BorderBrushProperty, "vx.Line");
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.35 };

            var rows = new StackPanel();
            var top = new StackPanel { Orientation = Orientation.Horizontal };
            _more = Toggle("", "Replace (Ctrl+H)");
            _more.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            _more.FontSize = 9;
            _more.Checked += (o, e) => { _replaceRow.Visibility = Visibility.Visible; };
            _more.Unchecked += (o, e) => { _replaceRow.Visibility = Visibility.Collapsed; };
            top.Children.Add(_more);
            top.Children.Add(Boxed(_find, "Find"));
            top.Children.Add(_case);
            top.Children.Add(_word);
            top.Children.Add(_regex);
            top.Children.Add(_count);
            top.Children.Add(Icon("", "Previous (Shift+Enter)", () => Next(false)));
            top.Children.Add(Icon("", "Next (Enter)", () => Next(true)));
            top.Children.Add(Icon("", "Close (Esc)", Hide));
            rows.Children.Add(top);
            _replaceRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
            _replaceRow.Children.Add(new Border { Width = 26 });
            _replaceRow.Children.Add(Boxed(_replace, "Replace with"));
            _replaceRow.Children.Add(Word("Replace", ReplaceOne));
            _replaceRow.Children.Add(Word("Replace all", ReplaceAll));
            rows.Children.Add(_replaceRow);
            Child = rows;

            _find.TextChanged += (o, e) => Search(true);
            foreach (ToggleButton t in new[] { _case, _word, _regex }) { t.Checked += (o, e) => Search(true); t.Unchecked += (o, e) => Search(true); }
            PreviewKeyDown += OnKey;
        }

        static ToggleButton Toggle(string text, string tip)
        {
            var t = new ToggleButton { Content = text, ToolTip = tip, Width = 26, Height = 26, Margin = new Thickness(2, 0, 0, 0), Focusable = false, FontSize = 11.5 };
            t.Template = ToggleTemplate();
            t.SetResourceReference(Control.ForegroundProperty, "vx.Fg");
            return t;
        }

        static ControlTemplate _toggleTemplate;
        static ControlTemplate ToggleTemplate()
        {
            if (_toggleTemplate != null) return _toggleTemplate;
            var t = new ControlTemplate(typeof(ToggleButton));
            var bd = new FrameworkElementFactory(typeof(Border), "Bd");
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            bd.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(cp);
            t.VisualTree = bd;
            var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            over.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("vx.Hover"), "Bd"));
            var on = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
            on.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("vx.Selected"), "Bd"));
            t.Triggers.Add(over);
            t.Triggers.Add(on);
            _toggleTemplate = t;
            return t;
        }

        static UIElement Boxed(TextBox box, string hint)
        {
            box.Height = 28;
            box.VerticalContentAlignment = VerticalAlignment.Center;
            box.Padding = new Thickness(6, 0, 6, 0);
            box.BorderThickness = new Thickness(1);
            box.SetResourceReference(Control.BackgroundProperty, "vx.Ctrl");
            box.SetResourceReference(Control.BorderBrushProperty, "vx.CtrlBorder");
            box.SetResourceReference(Control.ForegroundProperty, "vx.Fg");
            box.SetResourceReference(TextBox.CaretBrushProperty, "vx.Fg");
            box.SetResourceReference(TextBox.SelectionBrushProperty, "vx.Accent");
            box.ToolTip = hint;
            var g = new Grid { Margin = new Thickness(4, 0, 2, 0) };
            g.Children.Add(box);
            var ph = new TextBlock { Text = hint, IsHitTestVisible = false, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
            ph.SetResourceReference(TextBlock.ForegroundProperty, "vx.FgDim");
            box.TextChanged += (o, e) => ph.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            g.Children.Add(ph);
            return g;
        }

        static Button Icon(string glyph, string tip, Action click)
        {
            var b = new Button { Content = glyph, ToolTip = tip, Width = 26, Height = 26, FontSize = 10, Margin = new Thickness(1, 0, 0, 0) };
            b.SetResourceReference(StyleProperty, "vx.IconButton");
            b.Click += (o, e) => click();
            return b;
        }

        static Button Word(string text, Action click)
        {
            var b = new Button { Content = text, Height = 28, Margin = new Thickness(6, 0, 0, 0) };
            b.SetResourceReference(StyleProperty, "vx.TextButton");
            b.Click += (o, e) => click();
            return b;
        }

        // =================================================================
        // Showing it
        // =================================================================
        /// <summary>The editor it searches (the window's active tab; null = none).</summary>
        public TextEditor Target
        {
            get { return _target; }
            set
            {
                if (_target == value) return;
                if (_target != null) { _target.TextArea.TextView.BackgroundRenderers.Remove(_marks); _target.TextChanged -= TargetChanged; _target.TextArea.TextView.Redraw(); }
                _target = value;
                if (_target != null && IsVisible) Attach();
                if (IsVisible) Search(false);
            }
        }

        void Attach()
        {
            if (_target == null || _target.TextArea.TextView.BackgroundRenderers.Contains(_marks)) return;
            _target.TextArea.TextView.BackgroundRenderers.Add(_marks);
            _target.TextChanged += TargetChanged;
        }

        /// <summary>
        /// The text changed: look again. In a big file (over a million characters) not on every key - the whole text is copied and
        /// searched each time, which made typing with the bar open crawl - but once the typing pauses; anything that uses the
        /// matches meanwhile (Next, Replace) looks first (Fresh).
        /// </summary>
        void TargetChanged(object sender, EventArgs e)
        {
            if (!IsVisible || _target == null) return;
            if (_target.Document.TextLength < 1000000) { Search(false); return; }
            _stale = true;
            _marks.Matches = null;      // the old places are wrong now: nothing lit up rather than the wrong text
            if (_later == null)
            {
                _later = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                _later.Tick += (o, a) => Fresh();
            }
            _later.Stop();
            _later.Start();
        }

        System.Windows.Threading.DispatcherTimer _later;
        bool _stale;

        /// <summary>The matches are for the text as it is now.</summary>
        void Fresh()
        {
            if (_later != null) _later.Stop();
            if (!_stale) return;
            _stale = false;
            if (IsVisible) Search(false);
        }

        /// <summary>Ctrl+F (replace = Ctrl+H): open with the selected word, the find box ready.</summary>
        public void Open(bool replace)
        {
            Visibility = Visibility.Visible;
            Attach();
            _more.IsChecked = replace || _more.IsChecked == true;
            if (_target != null && !_target.TextArea.Selection.IsEmpty && !_target.TextArea.Selection.IsMultiline)
                _find.Text = _target.SelectedText;
            Search(false);
            if (replace && _find.Text.Length > 0) { _replace.Focus(); _replace.SelectAll(); }
            else { _find.Focus(); _find.SelectAll(); }
        }

        public void Hide()
        {
            Visibility = Visibility.Collapsed;
            _marks.Matches = null;
            if (_target != null)
            {
                _target.TextArea.TextView.BackgroundRenderers.Remove(_marks);
                _target.TextChanged -= TargetChanged;
                _target.TextArea.TextView.Redraw();
                _target.TextArea.Focus();
            }
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            if (e.Key == Key.Escape) { e.Handled = true; Hide(); }
            else if (e.Key == Key.Enter && Keyboard.FocusedElement == _replace) { e.Handled = true; if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) ReplaceAll(); else ReplaceOne(); }
            else if (e.Key == Key.Enter || e.Key == Key.F3) { e.Handled = true; Next(!shift); }
        }

        // =================================================================
        // Finding
        // =================================================================
        Regex Pattern()
        {
            string text = _find.Text;
            if (text.Length == 0) return null;
            string p = _regex.IsChecked == true ? text : Regex.Escape(text);
            if (_word.IsChecked == true) p = @"\b" + p + @"\b";
            var o = RegexOptions.Multiline;
            if (_case.IsChecked != true) o |= RegexOptions.IgnoreCase;
            try { _error = null; return new Regex(p, o, TimeSpan.FromSeconds(1)); }
            catch (ArgumentException) { _error = "Not a valid expression"; return null; }
        }

        /// <summary>Find every match; jump = select the first one after the caret (typing in the find box).</summary>
        void Search(bool jump)
        {
            _stale = false;
            if (_later != null) _later.Stop();
            _found = new List<Match>();
            if (_target != null)
            {
                Regex r = Pattern();
                if (r != null)
                {
                    try
                    {
                        foreach (Match m in r.Matches(_target.Text))
                        {
                            if (m.Length == 0) continue;
                            _found.Add(m);
                            if (_found.Count >= 10000) break;
                        }
                    }
                    catch (RegexMatchTimeoutException) { _error = "Took too long"; }
                }
            }
            _marks.Matches = _found;
            if (_target != null) _target.TextArea.TextView.Redraw();
            if (jump && _found.Count > 0 && _target != null)
            {
                int from = _target.SelectionStart;
                Match m = _found.Find(x => x.Index >= from) ?? _found[0];
                Select(m);
            }
            ShowCount();
        }

        void ShowCount()
        {
            if (_error != null) { _count.Text = _error; return; }
            if (_find.Text.Length == 0) { _count.Text = ""; return; }
            if (_found.Count == 0) { _count.Text = "No results"; return; }
            int at = _target == null ? -1 : _found.FindIndex(m => m.Index == _target.SelectionStart && m.Length == _target.SelectionLength);
            _count.Text = (at >= 0 ? (at + 1) + " of " : "") + _found.Count + (at >= 0 ? "" : _found.Count == 1 ? " found" : " found");
        }

        void Select(Match m)
        {
            _target.Select(m.Index, m.Length);
            TextLocation loc = _target.Document.GetLocation(m.Index);
            _target.ScrollTo(loc.Line, loc.Column);
            ShowCount();
        }

        /// <summary>The next (or previous) match from the caret, round to the start / end.</summary>
        public void Next(bool forward)
        {
            if (!IsVisible) { Open(false); return; }
            Fresh();
            if (_target == null || _found.Count == 0) return;
            Match pick;
            if (forward)
            {
                int from = _target.SelectionStart + Math.Max(1, _target.SelectionLength);
                pick = _found.Find(m => m.Index >= from) ?? _found[0];
            }
            else
            {
                int from = _target.SelectionStart;
                pick = _found.FindLast(m => m.Index < from) ?? _found[_found.Count - 1];
            }
            Select(pick);
        }

        // =================================================================
        // Replacing
        // =================================================================
        string ReplacementFor(Match m)
        {
            return _regex.IsChecked == true ? m.Result(_replace.Text) : _replace.Text;
        }

        void ReplaceOne()
        {
            if (_target == null || _target.IsReadOnly) return;
            Fresh();
            Match cur = _found.Find(m => m.Index == _target.SelectionStart && m.Length == _target.SelectionLength);
            if (cur == null) { Next(true); return; }
            _target.Document.Replace(cur.Index, cur.Length, ReplacementFor(cur));
            _target.SelectionLength = 0;
            Search(false);
            Next(true);
        }

        void ReplaceAll()
        {
            Fresh(); if (_target == null || _target.IsReadOnly || _found.Count == 0) return;
            // every match, not just the 10,000 the bar lights up
            Regex r = Pattern();
            if (r == null) return;
            var all = new List<Match>();
            try
            {
                foreach (Match m in r.Matches(_target.Text)) if (m.Length > 0) all.Add(m);
            }
            catch (RegexMatchTimeoutException) { _count.Text = "Took too long - nothing was replaced"; return; }
            if (all.Count == 0) return;
            int n = all.Count;
            _target.Document.BeginUpdate();
            try
            {
                for (int i = all.Count - 1; i >= 0; i--) _target.Document.Replace(all[i].Index, all[i].Length, ReplacementFor(all[i]));
            }
            finally { _target.Document.EndUpdate(); }
            Search(false);
            _count.Text = "Replaced " + n;
        }

        /// <summary>Lights up every match in the visible lines.</summary>
        sealed class Highlighter : IBackgroundRenderer
        {
            public List<Match> Matches;
            public KnownLayer Layer { get { return KnownLayer.Selection; } }

            public void Draw(TextView view, DrawingContext dc)
            {
                if (Matches == null || Matches.Count == 0 || !view.VisualLinesValid) return;
                var lines = view.VisualLines;
                if (lines.Count == 0) return;
                int start = lines[0].FirstDocumentLine.Offset, end = lines[lines.Count - 1].LastDocumentLine.EndOffset;
                var fill = view.TryFindResource("vx.Secondary") as SolidColorBrush;
                Color c = fill != null ? fill.Color : Colors.Orange;
                var brush = new SolidColorBrush(Color.FromArgb(0x48, c.R, c.G, c.B));
                brush.Freeze();
                foreach (Match m in Matches)
                {
                    if (m.Index + m.Length < start) continue;
                    if (m.Index > end) break;
                    var b = new BackgroundGeometryBuilder { CornerRadius = 2, AlignToWholePixels = true };
                    b.AddSegment(view, new TextSegment { StartOffset = m.Index, Length = m.Length });
                    Geometry g = b.CreateGeometry();
                    if (g != null) dc.DrawGeometry(brush, null, g);
                }
            }
        }
    }
}
