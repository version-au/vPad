using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using vPad.Core;

namespace vPad.Ui
{
    /// <summary>
    /// A vPad window: the suite's borderless window (the vShell theme, glass at the theme's opacity) with the vPad icon (its menu), the
    /// side panel button and the tabs along the top, the side panel (Files / Servers / Search), the text, and a status line. Tabs move
    /// between windows by dragging (MainWindow.TabDrag.cs); opening and saving is in MainWindow.Files.cs. vPad runs until its last
    /// window closes; that window's files open again next time.
    /// </summary>
    public sealed partial class MainWindow : Window, IThemedWindow
    {
        readonly AppSettings _s;
        readonly VShellThemeSource _themeSource;
        readonly Action _themeChanged, _updateChanged;
        IntPtr _hwnd;
        Theme _theme;
        bool _blur;
        WindowIcons _icons;

        readonly Grid _root = new Grid();
        readonly Image _appMark = new Image { Width = 18, Height = 18 };
        Button _appBtn, _panelBtn, _newBtn, _updateBtn, _pinBtn, _maxBtn;
        readonly TabStrip _strip = new TabStrip { TabHeight = 34, Reserve = 60 };
        readonly StackPanel _mdSwitch = new StackPanel { Orientation = Orientation.Horizontal };
        readonly Border _mdBox = new Border();
        SidePanel _side;
        readonly ColumnDefinition _sideCol = new ColumnDefinition(), _splitCol = new ColumnDefinition();
        GridSplitter _splitter;
        readonly Grid _editorHost = new Grid();
        readonly FindBar _find = new FindBar();
        readonly TextBlock _empty = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 14, TextAlignment = TextAlignment.Center };
        // the status line
        readonly TextBlock _pos = new TextBlock(), _note = new TextBlock();
        Button _indentBtn, _encBtn, _eolBtn, _langBtn;

        readonly List<DocTab> _tabs = new List<DocTab>();
        DocTab _active;

        public Theme CurrentTheme { get { return _theme; } }
        public bool Blurred { get { return _blur; } }
        public DocTab Active { get { return _active; } }

        static int _count;

        /// <summary>A window. request = what vPad was started with (the first window); adopt = a tab dragged out of another window.</summary>
        public MainWindow(AppSettings s, VShellThemeSource themeSource, StartRequest request, DocTab adopt = null, Point? at = null)
        {
            _s = s;
            _themeSource = themeSource;
            bool first = ++_count == 1;
            Title = "vPad";
            Width = 1000; Height = 680; MinWidth = 460; MinHeight = 260;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 12.5;
            UseLayoutRounding = true;
            Background = Brushes.Transparent;
            Topmost = s.Topmost;
            AllowDrop = true;
            SetResourceReference(ForegroundProperty, "vx.Fg");
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 40,     // the caption row: grab anywhere that isn't a tab or a button
                ResizeBorderThickness = new Thickness(ResizeEdge),
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = new Thickness(0),
                UseAeroCaptionButtons = false
            });

            Build();
            UpdateMaximized();
            if (first) RestorePlacement();
            else if (at == null)
            {
                MainWindow front = Front;
                if (front != null && front.WindowState == WindowState.Normal) { WindowStartupLocation = WindowStartupLocation.Manual; Left = front.Left + 32; Top = front.Top + 32; Width = front.Width; Height = front.Height; }
                else WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            if (at != null) { WindowStartupLocation = WindowStartupLocation.Manual; Left = at.Value.X; Top = at.Value.Y; }

            SourceInitialized += (o, e) =>
            {
                _hwnd = new WindowInteropHelper(this).Handle;
                HwndSource.FromHwnd(_hwnd).AddHook(WndProc);
                ApplyTheme();
                UpdateMaximized();
            };
            StateChanged += (o, e) => UpdateMaximized();
            Loaded += (o, e) =>
            {
                if (adopt != null) Adopt(adopt, -1);
                else if (first) StartUp(request);
                else if (!_noNewFile) NewFile();
            };
            Activated += (o, e) => { Dispatcher.BeginInvoke(new Action(CheckDisk), System.Windows.Threading.DispatcherPriority.Background); };
            Closing += OnClosing;
            Drop += OnDrop;
            DragOver += (o, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
            PreviewMouseWheel += OnWheel;
            _themeChanged = () => Dispatcher.BeginInvoke(new Action(ApplyTheme));
            _themeSource.Changed += _themeChanged;
            _updateChanged = () => Dispatcher.BeginInvoke(new Action(Buttons));
            UpdateCenter.Changed += _updateChanged;
        }

        /// <summary>Every open vPad window.</summary>
        public static List<MainWindow> All { get { return Application.Current.Windows.OfType<MainWindow>().Where(w => w.IsLoaded && !w._closing).ToList(); } }

        /// <summary>The window used last.</summary>
        public static MainWindow Front
        {
            get { List<MainWindow> all = All; return all.FirstOrDefault(w => w.IsActive) ?? all.LastOrDefault(); }
        }

        // =================================================================
        // Building the window
        // =================================================================
        void Build()
        {
            _root.SetResourceReference(Panel.BackgroundProperty, "vx.Bg");
            _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            _root.RowDefinitions.Add(new RowDefinition());
            _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });

            // ---- the caption row ----
            var caption = new DockPanel { LastChildFill = true };
            var capLine = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = caption };
            capLine.SetResourceReference(Border.BorderBrushProperty, "vx.Line");
            _appBtn = CapButton(null, "vPad menu", ShowMenu, 34);
            _appBtn.Content = _appMark;
            _appBtn.Margin = new Thickness(6, 0, 0, 0);
            DockPanel.SetDock(_appBtn, Dock.Left);
            caption.Children.Add(_appBtn);
            _panelBtn = CapButton("", "Side panel (Ctrl+B)", TogglePanel, 34);
            DockPanel.SetDock(_panelBtn, Dock.Left);
            caption.Children.Add(_panelBtn);

            var right = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(right, Dock.Right);
            _mdBox.Child = _mdSwitch;
            _mdBox.Margin = new Thickness(0, 0, 10, 0);
            _mdBox.VerticalAlignment = VerticalAlignment.Center;
            _mdBox.Padding = new Thickness(2);
            _mdBox.CornerRadius = new CornerRadius(6);
            _mdBox.SetResourceReference(Border.BackgroundProperty, "vx.Ctrl");
            WindowChrome.SetIsHitTestVisibleInChrome(_mdBox, true);
            foreach (var kv in new[] { Tuple.Create("Text", "Text"), Tuple.Create("Side", "Side by side"), Tuple.Create("Preview", "Preview") })
            {
                string how = kv.Item1;
                var b = new Button { Content = kv.Item2, Height = 24, Padding = new Thickness(10, 0, 10, 0), FontSize = 12, Tag = how, ToolTip = "Markdown: " + kv.Item2.ToLowerInvariant() + " (Ctrl+Shift+M)" };
                b.SetResourceReference(StyleProperty, "vx.CommandButton");
                b.Click += (o, e) => SetMarkdownView(how);
                _mdSwitch.Children.Add(b);
            }
            right.Children.Add(_mdBox);
            _updateBtn = CapButton("", "", () => OpenOptions("Updates"), 40);
            _updateBtn.SetResourceReference(ForegroundProperty, "vx.Accent");
            right.Children.Add(_updateBtn);
            _pinBtn = CapButton("", "", TogglePin, 40);
            right.Children.Add(_pinBtn);
            right.Children.Add(CapButton("", "Minimise", () => WindowState = WindowState.Minimized, 44));
            _maxBtn = CapButton("", "Maximise", ToggleMax, 44);
            right.Children.Add(_maxBtn);
            // Windows 11's snap layouts menu when the pointer rests on it (only while Windows' setting is on - else the plain button)
            vSuite.Ui.SnapLayouts.Attach(this, _maxBtn);
            Button close = CapButton("", "Close", Close, 44);
            close.SetResourceReference(StyleProperty, "vx.CloseButton");
            close.Height = 40;
            right.Children.Add(close);
            caption.Children.Add(right);

            // the tabs (vSuite's strip, as vXplorer's) and +, then empty caption (it moves the window)
            _newBtn = CapButton("\uE710", "New file (Ctrl+N)", NewFile, 32);
            _newBtn.FontSize = 12;
            _strip.Margin = new Thickness(4, 0, 0, 0);
            _strip.AddButton(_newBtn);
            HookStrip();
            caption.Children.Add(_strip);
            Grid.SetRow(capLine, 0);
            _root.Children.Add(capLine);

            // ---- the side panel and the text ----
            var body = new Grid();
            _sideCol.Width = new GridLength(_s.PanelOpen ? _s.PanelWidth : 0);
            _splitCol.Width = new GridLength(_s.PanelOpen ? 5 : 0);
            body.ColumnDefinitions.Add(_sideCol);
            body.ColumnDefinitions.Add(_splitCol);
            body.ColumnDefinitions.Add(new ColumnDefinition());
            _side = new SidePanel(_s) { Host = this };
            _side.OpenLocal = (p, line) => { var unused = OpenLocal(p, line); };
            _side.OpenRemote = (c, p) => { var unused = OpenRemote(c, p, 0); };
            var sideBack = new Border { Child = _side, Visibility = _s.PanelOpen ? Visibility.Visible : Visibility.Collapsed };
            sideBack.SetResourceReference(Border.BackgroundProperty, "vx.Panel");
            body.Children.Add(sideBack);
            _splitter = new GridSplitter { HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, Visibility = sideBack.Visibility };
            var splitLine = new Border { Width = 1, HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false };
            splitLine.SetResourceReference(Border.BackgroundProperty, "vx.Line");
            Grid.SetColumn(splitLine, 1);
            Grid.SetColumn(_splitter, 1);
            body.Children.Add(splitLine);
            body.Children.Add(_splitter);
            _splitter.DragCompleted += (o, e) => { if (_sideCol.ActualWidth > 40) { _s.PanelWidth = _sideCol.ActualWidth; _s.SaveSoon(); } };
            Grid.SetColumn(_editorHost, 2);
            body.Children.Add(_editorHost);
            _empty.Text = "Open a file (Ctrl+O), drop one here, or start a new one (Ctrl+N)";
            _empty.SetResourceReference(TextBlock.ForegroundProperty, "vx.FgDim");
            _editorHost.Children.Add(_empty);
            Panel.SetZIndex(_find, 20);
            _editorHost.Children.Add(_find);
            _editorHost.Children.Add(new UpdateNotice());
            Grid.SetRow(body, 1);
            _root.Children.Add(body);

            // ---- the status line ----
            var status = new DockPanel { LastChildFill = false };
            var statusBack = new Border { Child = status, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(8, 0, 6, 0) };
            statusBack.SetResourceReference(Border.BorderBrushProperty, "vx.Line");
            statusBack.SetResourceReference(TextElement.ForegroundProperty, "vx.FgDim");
            _pos.VerticalAlignment = VerticalAlignment.Center;
            _pos.Margin = new Thickness(4, 0, 12, 0);
            _pos.FontSize = 12;
            status.Children.Add(_pos);
            _indentBtn = StatusButton("Tab size and spaces", IndentMenu);
            status.Children.Add(_indentBtn);
            _encBtn = StatusButton("How the file's text is stored - click to change", EncodingMenu);
            status.Children.Add(_encBtn);
            _eolBtn = StatusButton("Line endings - click to change", LineEndingMenu);
            status.Children.Add(_eolBtn);
            _langBtn = StatusButton("Syntax colours for - click to change", LanguageMenu);
            DockPanel.SetDock(_langBtn, Dock.Right);
            status.Children.Add(_langBtn);
            _note.VerticalAlignment = VerticalAlignment.Center;
            _note.FontSize = 12;
            _note.Margin = new Thickness(0, 0, 12, 0);
            DockPanel.SetDock(_note, Dock.Right);
            status.Children.Add(_note);
            Grid.SetRow(statusBack, 2);
            _root.Children.Add(statusBack);

            Content = _root;
            Buttons();
            ShowStatus();
        }

        static Button CapButton(string glyph, string tip, Action click, double width)
        {
            var b = new Button { Content = glyph, ToolTip = tip, Width = width, Height = 32, Focusable = false };
            b.SetResourceReference(StyleProperty, "vx.IconButton");
            b.FontSize = 10.5;
            b.Click += (o, e) => click();
            return b;
        }

        Button StatusButton(string tip, Action<Button> menu)
        {
            var b = new Button { Height = 22, Padding = new Thickness(8, 0, 8, 0), FontSize = 12, ToolTip = tip, Margin = new Thickness(0, 0, 2, 0) };
            b.SetResourceReference(StyleProperty, "vx.CommandButton");
            b.SetResourceReference(ForegroundProperty, "vx.FgDim");
            b.Height = 22;
            b.Click += (o, e) => menu(b);
            return b;
        }

        // =================================================================
        // Theme
        // =================================================================
        void ApplyTheme()
        {
            Theme t = Theme.From(_themeSource.Current);
            bool blur = _s.ThemeOpacity;
            _theme = t;
            _blur = blur;
            Looks.Theme = t;
            Looks.Blur = blur;
            t.Apply(Application.Current.Resources, blur);
            WindowEffects.Look(this, t, blur);
            if (_icons == null) _icons = new WindowIcons(this);
            _icons.Apply(t);
            try { RenderOptions.SetBitmapScalingMode(_appMark, BitmapScalingMode.HighQuality); _appMark.Source = AppIcon.Image(t, true); }
            catch (Exception ex) { Log.Write("Icon: " + ex.Message); }
            Syntax.Paint(t);
            foreach (DocTab d in _tabs) { d.ApplyColors(); d.Redraw(); }
            if (OptionsWindow.Current != null && OptionsWindow.Current.Owner == this) OptionsWindow.Current.ApplyTheme(t, blur);
            ShowMarkdownSwitch();
        }

        /// <summary>Options changed something: every window shows it.</summary>
        public static void SettingsChanged()
        {
            foreach (MainWindow w in All)
            {
                w.Topmost = w._s.Topmost;
                w.ApplyTheme();
                foreach (DocTab d in w._tabs) { d.Apply(w._s); d.ShowMarkdown(w._s.MarkdownView, w._s); }
                w.Buttons();
                w.ShowStatus();
            }
        }

        // =================================================================
        // Placement, maximise, the buttons on the right
        // =================================================================
        void RestorePlacement()
        {
            if (_s.Width >= MinWidth && _s.Height >= MinHeight)
            {
                var r = new Rect(_s.Left, _s.Top, _s.Width, _s.Height);
                var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
                if (screen.IntersectsWith(r))
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Left = r.Left; Top = r.Top; Width = r.Width; Height = r.Height;
                    if (_s.Maximized) WindowState = WindowState.Maximized;
                    return;
                }
            }
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        void Remember()
        {
            Rect r = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            if (r.Width < MinWidth || r.Height < MinHeight || double.IsInfinity(r.Width)) return;
            _s.Left = r.Left; _s.Top = r.Top; _s.Width = r.Width; _s.Height = r.Height;
            _s.Maximized = WindowState == WindowState.Maximized;
        }

        void ToggleMax() { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }

        void UpdateMaximized()
        {
            bool max = WindowState == WindowState.Maximized;
            _root.Margin = max ? new Thickness(WindowEffects.MaximizedOverhang(_hwnd)) : new Thickness(0);
            // the text's scrollbar stays clear of the right edge, where the window is resized from (that took the mouse there)
            _editorHost.Margin = max ? new Thickness(0) : new Thickness(0, 0, ResizeEdge, 0);
            Buttons();
        }

        /// <summary>How far in from the window's edges a press resizes it (WindowChrome's ResizeBorderThickness).</summary>
        const double ResizeEdge = 6;

        void Buttons()
        {
            bool upd = UpdateCenter.Available != null && !UpdateCenter.IsSkipped(UpdateCenter.Available) && !UpdateCenter.NoticeVisible;
            _updateBtn.Visibility = upd ? Visibility.Visible : Visibility.Collapsed;
            if (upd) _updateBtn.ToolTip = "vPad " + UpdateCenter.VersionText(UpdateCenter.Available) + " is out - see Options > Updates";
            _pinBtn.Content = Topmost ? "" : "";
            _pinBtn.ToolTip = Topmost ? "Kept on top - click to let other windows cover it" : "Keep on top of other windows";
            bool max = WindowState == WindowState.Maximized;
            _maxBtn.Content = max ? "" : "";
            _maxBtn.ToolTip = max ? "Restore" : "Maximise";
            _panelBtn.ToolTip = (_s.PanelOpen ? "Hide" : "Show") + " the side panel (Ctrl+B)";
        }

        void TogglePin()
        {
            _s.Topmost = !_s.Topmost;
            _s.SaveSoon();
            foreach (MainWindow w in All) { w.Topmost = _s.Topmost; w.Buttons(); }
        }

        void TogglePanel()
        {
            _s.PanelOpen = !_s.PanelOpen;
            _s.SaveSoon();
            foreach (MainWindow w in All) w.ShowPanel();
        }

        void ShowPanel()
        {
            bool open = _s.PanelOpen;
            _sideCol.Width = new GridLength(open ? _s.PanelWidth : 0);
            _splitCol.Width = new GridLength(open ? 5 : 0);
            ((Border)_side.Parent).Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            _splitter.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            Buttons();
        }

        /// <summary>Show the side panel on a tab (Ctrl+Shift+F = Search).</summary>
        void PanelTab(string tab)
        {
            if (!_s.PanelOpen) TogglePanel();
            _side.ShowTab(tab);
        }

        // =================================================================
        // The vPad menu (the icon at the top left)
        // =================================================================
        void ShowMenu()
        {
            var m = new ContextMenu { PlacementTarget = _appBtn, Placement = PlacementMode.Bottom };
            Add(m, "", "New file", "Ctrl+N", true, NewFile);
            Add(m, "", "New window", "Ctrl+Shift+N", true, () => OpenWindow(null));
            Add(m, "", "Open…", "Ctrl+O", true, OpenDialog);
            var recent = new MenuItem { Header = "Open recent" };
            recent.Icon = Glyph("");
            foreach (string p in _s.Recent.Take(12))
            {
                string path = p;
                var mi = new MenuItem { Header = p.Replace("_", "__") };
                mi.Click += (o, e) => { var unused = OpenLocal(path, 0); };
                recent.Items.Add(mi);
            }
            recent.IsEnabled = recent.Items.Count > 0;
            m.Items.Add(recent);
            m.Items.Add(new Separator());
            Add(m, "", "Save", "Ctrl+S", _active != null, () => { var unused = Save(_active); });
            Add(m, "", "Save as…", "Ctrl+Shift+S", _active != null, () => { var unused = SaveAs(_active); });
            Add(m, "", "Save all", null, _tabs.Any(t => t.Dirty), () => { var unused = SaveAll(); });
            m.Items.Add(new Separator());
            Add(m, "", "Find…", "Ctrl+F", _active != null, () => _find.Open(false));
            Add(m, "", "Replace…", "Ctrl+H", _active != null, () => _find.Open(true));
            Add(m, "", "Go to line…", "Ctrl+G", _active != null, GoToLine);
            Add(m, "", "Find in files…", "Ctrl+Shift+F", true, () => PanelTab("Search"));
            var wrap = Add(m, "", "Wrap long lines", "Alt+Z", true, ToggleWrap);
            wrap.IsCheckable = true;
            wrap.IsChecked = _s.WordWrap;
            m.Items.Add(new Separator());
            Add(m, "", "Options", null, true, () => OpenOptions(null));
            Add(m, "", "Check for updates", null, !UpdateCenter.Busy && !UpdateCenter.Installing, () => { OpenOptions("Updates"); UpdateCenter.CheckNow(true); });
            Add(m, "", "About vPad", null, true, () => OpenOptions("About"));
            m.Items.Add(new Separator());
            Add(m, "", "Close window", "Alt+F4", true, Close);
            m.IsOpen = true;
        }

        static TextBlock Glyph(string g)
        {
            var t = new TextBlock { Text = g };
            t.SetResourceReference(TextBlock.StyleProperty, "vx.MenuGlyph");
            return t;
        }

        static MenuItem Add(ItemsControl m, string glyph, string text, string keys, bool enabled, Action a)
        {
            var mi = new MenuItem { Header = text, IsEnabled = enabled, Icon = Glyph(glyph) };
            if (keys != null) mi.InputGestureText = keys;
            mi.Click += (o, e) => a();
            m.Items.Add(mi);
            return mi;
        }

        public void OpenOptions(string page)
        {
            OptionsWindow.Open(this, _s, _themeSource, _theme, _blur, page);
        }

        // =================================================================
        // Keys and the mouse wheel
        // =================================================================
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (_strip.Pressing && e.Key == Key.Escape) { e.Handled = true; _strip.CancelDrag(); return; }
            ModifierKeys mods = Keyboard.Modifiers;
            bool ctrl = (mods & ModifierKeys.Control) != 0, shift = (mods & ModifierKeys.Shift) != 0, alt = (mods & ModifierKeys.Alt) != 0;
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            Action run = null;
            if (ctrl && !alt)
            {
                switch (key)
                {
                    case Key.N: run = shift ? (Action)(() => OpenWindow(null)) : NewFile; break;
                    case Key.O: run = shift ? (Action)(() => { if (_active != null && _tabs.Count > 1) MoveToNewWindow(_active, null); }) : OpenDialog; break;
                    case Key.S: run = () => { var unused = shift ? SaveAs(_active) : Save(_active); }; break;
                    case Key.W: case Key.F4: run = () => { var unused = CloseTab(_active); }; break;
                    case Key.Tab: run = () => Cycle(shift ? -1 : 1); break;
                    case Key.PageDown: run = () => Cycle(1); break;
                    case Key.PageUp: run = () => Cycle(-1); break;
                    case Key.F: run = shift ? (Action)(() => PanelTab("Search")) : () => _find.Open(false); break;
                    case Key.H: run = () => _find.Open(true); break;
                    case Key.G: run = GoToLine; break;
                    case Key.B: run = TogglePanel; break;
                    case Key.T: if (shift) run = Reopen; break;
                    case Key.M: if (shift) run = () => CycleMarkdown(); break;
                    case Key.OemPlus: case Key.Add: run = () => Zoom(1); break;
                    case Key.OemMinus: case Key.Subtract: run = () => Zoom(-1); break;
                    case Key.D0: case Key.NumPad0: run = () => Zoom(0); break;
                }
            }
            else if (alt && !ctrl && key == Key.Z) run = ToggleWrap;
            else if (key == Key.F3 && !ctrl && !alt) run = () => _find.Next(!shift);
            if (run != null && (_active != null || key == Key.N || key == Key.O || key == Key.B || key == Key.T || key == Key.F))
            {
                e.Handled = true;
                run();
                return;
            }
            base.OnPreviewKeyDown(e);
        }

        void OnWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control || _active == null || !_active.Editor.IsMouseOver) return;
            e.Handled = true;
            Zoom(e.Delta > 0 ? 1 : -1);
        }

        /// <summary>Text size: bigger (1), smaller (-1) or back to 14 (0) - every window.</summary>
        void Zoom(int dir)
        {
            double size = dir == 0 ? 14 : _s.FontSize + dir * (_s.FontSize >= 20 ? 2 : 1);
            _s.FontSize = Math.Max(7, Math.Min(40, size));
            _s.SaveSoon();
            foreach (MainWindow w in All) foreach (DocTab d in w._tabs) d.Apply(_s);
            Note("Text size " + _s.FontSize);
        }

        void ToggleWrap()
        {
            _s.WordWrap = !_s.WordWrap;
            _s.SaveSoon();
            foreach (MainWindow w in All) foreach (DocTab d in w._tabs) d.Editor.WordWrap = _s.WordWrap;
            Note(_s.WordWrap ? "Long lines wrap" : "Long lines don't wrap");
        }

        void Cycle(int dir)
        {
            if (_tabs.Count < 2 || _active == null) return;
            int i = (_tabs.IndexOf(_active) + dir + _tabs.Count) % _tabs.Count;
            Activate(_tabs[i]);
        }

        void GoToLine()
        {
            if (_active == null) return;
            string v = FormWindow.Prompt(this, "Go to line", "Line (1 - " + _active.Editor.Document.LineCount + ")", _active.Line.ToString());
            int n;
            if (v != null && int.TryParse(v.Trim(), out n)) _active.GoToLine(n);
            _active.Editor.TextArea.Focus();
        }

        // =================================================================
        // Markdown
        // =================================================================
        void SetMarkdownView(string how)
        {
            _s.MarkdownView = how;
            _s.SaveSoon();
            foreach (MainWindow w in All) { foreach (DocTab d in w._tabs) d.ShowMarkdown(how, _s); w.ShowMarkdownSwitch(); }
        }

        void CycleMarkdown()
        {
            if (_active == null || !Languages.IsMarkdown(_active.Language)) return;
            SetMarkdownView(_s.MarkdownView == "Text" ? "Side" : _s.MarkdownView == "Side" ? "Preview" : "Text");
        }

        void ShowMarkdownSwitch()
        {
            bool md = _active != null && Languages.IsMarkdown(_active.Language);
            _mdBox.Visibility = md ? Visibility.Visible : Visibility.Collapsed;
            foreach (Button b in _mdSwitch.Children.OfType<Button>())
            {
                bool on = (string)b.Tag == _s.MarkdownView;
                if (on) { b.SetResourceReference(BackgroundProperty, "vx.Accent"); b.SetResourceReference(ForegroundProperty, "vx.OnAccent"); }
                else { b.ClearValue(BackgroundProperty); b.SetResourceReference(ForegroundProperty, "vx.FgDim"); }
            }
        }

        // =================================================================
        // The status line
        // =================================================================
        void ShowStatus()
        {
            bool any = _active != null;
            foreach (Button b in new[] { _indentBtn, _encBtn, _eolBtn, _langBtn }) b.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            if (!any) { _pos.Text = ""; return; }
            var caret = _active.Editor.TextArea.Caret;
            int sel = _active.Editor.SelectionLength;
            _pos.Text = "Line " + caret.Line + ", column " + caret.Column + (sel > 0 ? "  (" + sel + " selected)" : "");
            _indentBtn.Content = _s.TabSpaces ? "Spaces: " + _s.TabSize : "Tab size: " + _s.TabSize;
            _encBtn.Content = _active.Format.EncodingText;
            _eolBtn.Content = _active.Format.LineEnding;
            _langBtn.Content = _active.Language;
        }

        DateTime _noteUntil;

        /// <summary>A short message on the right of the status line ("Saved at 3:42 PM").</summary>
        void Note(string text, int seconds = 6)
        {
            _note.Text = text;
            _noteUntil = DateTime.Now.AddSeconds(seconds);
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds + 0.2) };
            t.Tick += (o, e) => { t.Stop(); if (DateTime.Now >= _noteUntil) _note.Text = ""; };
            t.Start();
        }

        ContextMenu MenuAt(Button b)
        {
            return new ContextMenu { PlacementTarget = b, Placement = PlacementMode.Top };
        }

        void IndentMenu(Button b)
        {
            ContextMenu m = MenuAt(b);
            var spaces = Add(m, "", "The Tab key types spaces", null, true, () => { _s.TabSpaces = !_s.TabSpaces; Saved(); });
            spaces.IsCheckable = true; spaces.IsChecked = _s.TabSpaces;
            m.Items.Add(new Separator());
            foreach (int n in new[] { 2, 4, 8 })
            {
                int size = n;
                var mi = Add(m, "", "Tab size " + n, null, true, () => { _s.TabSize = size; Saved(); });
                mi.IsCheckable = true; mi.IsChecked = _s.TabSize == n;
            }
            m.Items.Add(new Separator());
            Add(m, "", "Tabs to spaces in this file", null, _active != null, () => Reindent(true));
            Add(m, "", "Spaces to tabs in this file", null, _active != null, () => Reindent(false));
            m.IsOpen = true;
        }

        void Saved()
        {
            _s.Normalize();
            _s.SaveSoon();
            SettingsChanged();
        }

        /// <summary>Leading tabs to spaces (or spaces to tabs) on every line - one undo.</summary>
        void Reindent(bool toSpaces)
        {
            var doc = _active.Editor.Document;
            int size = _s.TabSize;
            doc.BeginUpdate();
            try
            {
                foreach (var line in doc.Lines.ToList())
                {
                    string text = doc.GetText(line);
                    int i = 0, col = 0;
                    while (i < text.Length && (text[i] == ' ' || text[i] == '\t')) { col = text[i] == '\t' ? (col / size + 1) * size : col + 1; i++; }
                    if (i == 0) continue;
                    string lead = toSpaces ? new string(' ', col) : new string('\t', col / size) + new string(' ', col % size);
                    if (lead != text.Substring(0, i)) doc.Replace(line.Offset, i, lead);
                }
            }
            finally { doc.EndUpdate(); }
        }

        void EncodingMenu(Button b)
        {
            if (_active == null) return;
            ContextMenu m = MenuAt(b);
            m.Items.Add(new MenuItem { Header = "Save with", IsEnabled = false });
            foreach (string enc in TextFormat.Encodings)
            {
                string e = enc;
                var mi = Add(m, "", enc, null, true, () =>
                {
                    _active.Format.SetEncoding(e);
                    MarkChanged(_active);
                    ShowStatus();
                });
                mi.IsCheckable = true; mi.IsChecked = _active.Format.EncodingText == enc;
            }
            if (!_active.IsNew)
            {
                m.Items.Add(new Separator());
                Add(m, "", "Read the file again", null, true, () => { var unused = Reload(_active, true); });
            }
            m.IsOpen = true;
        }

        void LineEndingMenu(Button b)
        {
            if (_active == null) return;
            ContextMenu m = MenuAt(b);
            foreach (var kv in new[] { Tuple.Create("CRLF", "Windows (CRLF)"), Tuple.Create("LF", "Linux and macOS (LF)") })
            {
                string le = kv.Item1;
                var mi = Add(m, "", kv.Item2, null, true, () =>
                {
                    if (_active.Format.LineEnding == le) return;
                    _active.Format.LineEnding = le;
                    MarkChanged(_active);
                    ShowStatus();
                });
                mi.IsCheckable = true; mi.IsChecked = _active.Format.LineEnding == le;
            }
            m.IsOpen = true;
        }

        /// <summary>Something about how it's saved changed (no text did): it needs saving.</summary>
        static void MarkChanged(DocTab d)
        {
            // an empty change the undo stack counts: the tab shows unsaved until it's saved
            var doc = d.Editor.Document;
            doc.UndoStack.Push(new NoTextChange());
            d.Redraw();
        }

        sealed class NoTextChange : ICSharpCode.AvalonEdit.Document.IUndoableOperation
        {
            public void Undo() { }
            public void Redo() { }
        }

        void LanguageMenu(Button b)
        {
            if (_active == null) return;
            ContextMenu m = MenuAt(b);
            foreach (var kv in Languages.All)
            {
                string lang = kv.Key;
                var mi = Add(m, "", lang, null, true, () => { _active.SetLanguage(lang); _active.ShowMarkdown(_s.MarkdownView, _s); ShowMarkdownSwitch(); ShowStatus(); });
                mi.IsCheckable = true; mi.IsChecked = _active.Language == lang;
            }
            m.IsOpen = true;
        }

        // =================================================================
        // Another start of vPad handing over what it was asked (Instance.HandOff)
        // =================================================================
        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x4A /* WM_COPYDATA */)
            {
                string test = TestHooks.Received(lParam);   // VPAD_TEST_HOOKS=1 only (MainWindow.TestHooks.cs)
                if (test != null)
                {
                    handled = true;
                    Dispatcher.BeginInvoke(new Action(() => RunTestCommand(test)));
                    return new IntPtr(1);
                }
                string text = null;
                try { text = Instance.Received(lParam); } catch { }
                if (text == null) return IntPtr.Zero;
                handled = true;
                string t = text;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MainWindow target = Front ?? this;
                    if (UpdateCenter.IsCommand(t))
                    {
                        if (string.Equals(t.Trim(), UpdateCenter.UpdateCommand, StringComparison.OrdinalIgnoreCase)) target.BringUp();
                        UpdateCenter.HandleCommand(t);
                        return;
                    }
                    StartRequest r = StartRequest.FromHandOff(t);
                    if (r == null) return;
                    if (r.NewWindow) { var w = OpenWindow(null, false); w.Run(r); }
                    else target.Run(r);
                }));
                return new IntPtr(1);
            }
            return IntPtr.Zero;
        }

        public void BringUp()
        {
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
        }

        /// <summary>A new window (with a new file unless newFile is false).</summary>
        MainWindow OpenWindow(DocTab adopt, bool newFile = true)
        {
            var w = new MainWindow(_s, _themeSource, null, adopt);
            if (adopt == null && !newFile) w._noNewFile = true;
            w.Show();
            w.Activate();
            return w;
        }

        bool _noNewFile;
    }
}
