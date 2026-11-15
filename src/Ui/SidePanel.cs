using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using vPad.Core;

namespace vPad.Ui
{
    /// <summary>
    /// A row in the side panel's trees - the shape vSuite's TreeViewItem style expects (Name, Icon, IndentMargin, IsExpanded …).
    /// Folders load what's in them the first time they open.
    /// </summary>
    public sealed class Node : INotifyPropertyChanged
    {
        public string Name { get; set; }
        ImageSource _icon;
        public ImageSource Icon { get { return _icon; } set { _icon = value; Changed("Icon"); } }
        public Thickness IndentMargin { get; set; }
        public bool IsPlaceholder { get; set; }
        public bool IsDropTarget { get { return false; } }
        public ObservableCollection<Node> Children { get; } = new ObservableCollection<Node>();

        // what it is
        public string Path;                 // a file or folder on this PC
        public VTermConnection Server;     // on a server (or the server itself, RemotePath null)
        public string RemotePath;
        public bool IsFolder;
        public int Depth;
        /// <summary>Fills Children (the first time it opens). Null for files.</summary>
        public Func<Node, Task> Load;
        bool _loaded, _expanded, _selected;

        public bool IsExpanded
        {
            get { return _expanded; }
            set
            {
                if (_expanded == value) return;
                _expanded = value;
                Changed("IsExpanded");
                if (value && !_loaded && Load != null) { _loaded = true; var unused = Load(this); }
            }
        }

        public bool IsSelected { get { return _selected; } set { if (_selected == value) return; _selected = value; Changed("IsSelected"); } }

        /// <summary>Load it again next time it opens (Refresh).</summary>
        public void Reset()
        {
            _loaded = false;
            Children.Clear();
            if (Load != null) Children.Add(Placeholder("Loading…", Depth + 1));
            if (_expanded) { _loaded = true; var unused = Load(this); }
        }

        /// <summary>Its name (screen readers and UI Automation see this).</summary>
        public override string ToString() { return Name; }

        public static Node Placeholder(string text, int depth)
        {
            return new Node { Name = text, IsPlaceholder = true, IndentMargin = new Thickness(depth * 14, 0, 0, 0) };
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void Changed(string p) { if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(p)); }
    }

    /// <summary>
    /// The side panel (Ctrl+B): Files (a folder on this PC as a tree), Servers (vTerm's saved connections, each server's files over SFTP)
    /// and Search (text in the folder's files). A click on a file opens it (OpenLocal / OpenRemote).
    /// </summary>
    public sealed class SidePanel : DockPanel
    {
        readonly AppSettings _s;
        public Action<string, int> OpenLocal;
        public Action<VTermConnection, string> OpenRemote;
        /// <summary>The window, for questions.</summary>
        public Window Host;

        readonly Dictionary<string, Button> _tabButtons = new Dictionary<string, Button>();
        readonly Grid _pages = new Grid();
        readonly TreeView _files = new TreeView();
        readonly TreeView _servers = new TreeView();
        readonly TextBlock _folderText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        readonly TextBox _searchBox = new TextBox();
        readonly StackPanel _results = new StackPanel();
        readonly TextBlock _searchNote = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(4, 6, 4, 6) };
        FrameworkElement _filesPage, _serversPage, _searchPage;
        CancellationTokenSource _searching;

        public SidePanel(AppSettings s)
        {
            _s = s;
            LastChildFill = true;
            Margin = new Thickness(8, 8, 4, 0);

            // ---- the tabs ----
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            foreach (string t in new[] { "Files", "Servers", "Search" })
            {
                string name = t;
                var b = new Button { Content = t, Height = 30, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 4, 0), BorderThickness = new Thickness(0, 0, 0, 2) };
                b.Template = TabTemplate();
                b.Click += (o, e) => ShowTab(name);
                _tabButtons[t] = b;
                tabs.Children.Add(b);
            }
            SetDock(tabs, Dock.Top);
            Children.Add(tabs);
            Children.Add(_pages);

            _filesPage = BuildFiles();
            _serversPage = BuildServers();
            _searchPage = BuildSearch();
            _pages.Children.Add(_filesPage);
            _pages.Children.Add(_serversPage);
            _pages.Children.Add(_searchPage);
            ShowTab(_s.PanelTab);
            if (_s.Folder.Length > 0 && Directory.Exists(_s.Folder)) SetFolder(_s.Folder);
            else ShowFolderHint();
        }

        static ControlTemplate _tabTemplate;
        static ControlTemplate TabTemplate()
        {
            if (_tabTemplate != null) return _tabTemplate;
            var t = new ControlTemplate(typeof(Button));
            var bd = new FrameworkElementFactory(typeof(Border), "Bd");
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(5, 5, 0, 0));
            bd.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            bd.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            bd.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            bd.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(cp);
            t.VisualTree = bd;
            _tabTemplate = t;
            return t;
        }

        public void ShowTab(string name)
        {
            if (!_tabButtons.ContainsKey(name)) name = "Files";
            _s.PanelTab = name;
            foreach (var kv in _tabButtons)
            {
                bool on = kv.Key == name;
                if (on)
                {
                    kv.Value.SetResourceReference(Control.BackgroundProperty, "vx.Hover");
                    kv.Value.SetResourceReference(Control.BorderBrushProperty, "vx.Accent");
                    kv.Value.SetResourceReference(Control.ForegroundProperty, "vx.Fg");
                }
                else
                {
                    kv.Value.Background = Brushes.Transparent;
                    kv.Value.BorderBrush = Brushes.Transparent;
                    kv.Value.SetResourceReference(Control.ForegroundProperty, "vx.FgDim");
                }
            }
            _filesPage.Visibility = name == "Files" ? Visibility.Visible : Visibility.Collapsed;
            _serversPage.Visibility = name == "Servers" ? Visibility.Visible : Visibility.Collapsed;
            _searchPage.Visibility = name == "Search" ? Visibility.Visible : Visibility.Collapsed;
            if (name == "Servers") LoadServers();
            if (name == "Search") Dispatcher.BeginInvoke(new Action(() => { _searchBox.Focus(); _searchBox.SelectAll(); }), System.Windows.Threading.DispatcherPriority.Input);
        }

        static ScrollViewer Scroll(UIElement content)
        {
            return new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 4, 0) };
        }

        static Button SmallButton(string glyph, string tip, Action click)
        {
            var b = new Button { Content = glyph, ToolTip = tip, Width = 30, Height = 30, FontSize = 12 };
            b.SetResourceReference(StyleProperty, "vx.IconButton");
            b.Click += (o, e) => click();
            return b;
        }

        // =================================================================
        // Files
        // =================================================================
        FrameworkElement BuildFiles()
        {
            var page = new DockPanel();
            var bar = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var refresh = SmallButton("", "Refresh", RefreshFolder);
            var up = SmallButton("", "Up a folder", () => { if (_s.Folder.Length > 0) { string p = Path.GetDirectoryName(_s.Folder.TrimEnd('\\')); if (p != null) SetFolder(p); } });
            SetDock(refresh, Dock.Right);
            SetDock(up, Dock.Right);
            bar.Children.Add(refresh);
            bar.Children.Add(up);
            var pick = new Button { Height = 30, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 0, 8, 0), ToolTip = "Show another folder" };
            pick.SetResourceReference(StyleProperty, "vx.CommandButton");
            var inner = new DockPanel();
            var glyph = new TextBlock { Text = "", Margin = new Thickness(0, 0, 8, 0) };
            glyph.SetResourceReference(TextBlock.StyleProperty, "vx.CommandGlyph");
            inner.Children.Add(glyph);
            inner.Children.Add(_folderText);
            pick.Content = inner;
            pick.Click += (o, e) => ChooseFolder();
            bar.Children.Add(pick);
            SetDock(bar, Dock.Top);
            page.Children.Add(bar);
            _files.PreviewMouseLeftButtonUp += (o, e) => Clicked(e, false);
            _files.MouseRightButtonUp += (o, e) => RightClicked(e);
            _files.KeyDown += (o, e) => { if (e.Key == Key.Enter) { var n = _files.SelectedItem as Node; if (n != null) Activate(n); } };
            page.Children.Add(Scroll(_files));
            return page;
        }

        void ShowFolderHint()
        {
            _folderText.Text = "Choose a folder…";
            _files.Items.Clear();
        }

        void ChooseFolder()
        {
            var d = new Microsoft.Win32.OpenFolderDialog { Title = "The folder to show in vPad's side panel" };
            if (_s.Folder.Length > 0 && Directory.Exists(_s.Folder)) d.InitialDirectory = _s.Folder;
            if (d.ShowDialog(Host) == true) SetFolder(d.FolderName);
        }

        /// <summary>Show this folder in Files.</summary>
        public void SetFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
            _s.Folder = folder;
            _s.SaveSoon();
            _folderText.Text = Path.GetFileName(folder.TrimEnd('\\')) is string n && n.Length > 0 ? n : folder;
            ((FrameworkElement)_folderText.Parent).ToolTip = folder;
            _files.Items.Clear();
            foreach (Node c in LocalChildren(folder, 0)) _files.Items.Add(c);
        }

        /// <summary>No folder chosen yet: the first file opened gives one.</summary>
        public void FolderFromFile(string file)
        {
            if (_s.Folder.Length > 0 && Directory.Exists(_s.Folder)) return;
            string d = Path.GetDirectoryName(file);
            if (d != null) SetFolder(d);
        }

        void RefreshFolder()
        {
            if (_s.Folder.Length == 0) return;
            var open = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Walk(_files.Items.OfType<Node>(), n => { if (n.IsExpanded && n.Path != null) open.Add(n.Path); });
            SetFolder(_s.Folder);
            Reopen(_files.Items.OfType<Node>(), open);
        }

        void Reopen(IEnumerable<Node> nodes, HashSet<string> open)
        {
            foreach (Node n in nodes.ToList())
                if (n.Path != null && open.Contains(n.Path))
                {
                    n.IsExpanded = true;
                    Dispatcher.BeginInvoke(new Action(() => Reopen(n.Children, open)), System.Windows.Threading.DispatcherPriority.Background);
                }
        }

        static void Walk(IEnumerable<Node> nodes, Action<Node> f)
        {
            foreach (Node n in nodes) { f(n); Walk(n.Children, f); }
        }

        static readonly HashSet<string> SkipFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".git", ".vs", "node_modules", "bin", "obj", "__pycache__", ".idea" };

        List<Node> LocalChildren(string folder, int depth)
        {
            var l = new List<Node>();
            try
            {
                var dirs = new DirectoryInfo(folder).EnumerateDirectories().Where(d => (d.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0 || d.Name.StartsWith("."))
                                                    .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase);
                foreach (DirectoryInfo d in dirs)
                {
                    var n = new Node { Name = d.Name, Path = d.FullName, IsFolder = true, Depth = depth, IndentMargin = new Thickness(depth * 14, 0, 0, 0), Icon = Icons.Folder };
                    n.Load = LoadLocal;
                    n.Children.Add(Node.Placeholder("Loading…", depth + 1));
                    l.Add(n);
                }
                var files = new DirectoryInfo(folder).EnumerateFiles().Where(f => (f.Attributes & FileAttributes.System) == 0).OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase);
                foreach (FileInfo f in files)
                    l.Add(new Node { Name = f.Name, Path = f.FullName, Depth = depth, IndentMargin = new Thickness(depth * 14 + 16, 0, 0, 0), Icon = Icons.For(f.Name) });
            }
            catch (Exception ex) { l.Add(Node.Placeholder("Can't open it: " + ex.Message, depth)); }
            return l;
        }

        Task LoadLocal(Node n)
        {
            n.Children.Clear();
            List<Node> kids = LocalChildren(n.Path, n.Depth + 1);
            if (kids.Count == 0) kids.Add(Node.Placeholder("Empty", n.Depth + 1));
            foreach (Node k in kids) n.Children.Add(k);
            return Task.CompletedTask;
        }

        // =================================================================
        // Servers
        // =================================================================
        DateTime _serversRead = DateTime.MinValue;

        FrameworkElement BuildServers()
        {
            var page = new DockPanel();
            var bar = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var refresh = SmallButton("", "Read vTerm's connections again, and refresh the open folders", () => { _serversRead = DateTime.MinValue; LoadServers(); });
            SetDock(refresh, Dock.Right);
            bar.Children.Add(refresh);
            var note = new TextBlock { Text = "Your connections from vTerm", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
            note.SetResourceReference(TextBlock.ForegroundProperty, "vx.FgDim");
            bar.Children.Add(note);
            SetDock(bar, Dock.Top);
            page.Children.Add(bar);
            _servers.PreviewMouseLeftButtonUp += (o, e) => Clicked(e, true);
            _servers.MouseRightButtonUp += (o, e) => RightClicked(e);
            page.Children.Add(Scroll(_servers));
            return page;
        }

        void LoadServers()
        {
            DateTime w = DateTime.MinValue;
            try { if (File.Exists(VTermConnections.SettingsFile)) w = File.GetLastWriteTimeUtc(VTermConnections.SettingsFile); } catch { }
            if (w == _serversRead && _servers.Items.Count > 0) return;
            _serversRead = w;
            _servers.Items.Clear();
            List<VTermConnection> all = VTermConnections.All;
            if (all.Count == 0)
            {
                _servers.Items.Add(Node.Placeholder(VTermConnections.Exists ? "No saved connections in vTerm yet." : "Save connections in vTerm to open their files here.", 0));
                return;
            }
            string folder = null;
            Node group = null;
            foreach (VTermConnection c in all)
            {
                if ((c.Folder ?? "") != folder)
                {
                    folder = c.Folder ?? "";
                    if (folder.Length > 0)
                    {
                        group = new Node { Name = folder, IsFolder = true, Depth = 0, IndentMargin = new Thickness(0), Icon = Icons.Group, IsExpanded = true };
                        _servers.Items.Add(group);
                    }
                    else group = null;
                }
                int depth = group != null ? 1 : 0;
                var n = new Node
                {
                    Name = c.Shown, Server = c, IsFolder = true, Depth = depth, IndentMargin = new Thickness(depth * 14, 0, 0, 0),
                    Icon = Remote.IsOpen(c.Id) ? Icons.ServerOn : Icons.Server, Load = LoadRemote
                };
                n.Children.Add(Node.Placeholder("Connecting…", depth + 1));
                if (group != null) group.Children.Add(n); else _servers.Items.Add(n);
            }
        }

        async Task LoadRemote(Node n)
        {
            VTermConnection c = n.Server;
            string folder = n.RemotePath;
            n.Children.Clear();
            n.Children.Add(Node.Placeholder(folder == null ? "Connecting…" : "Loading…", n.Depth + 1));
            try
            {
                var got = await Task.Run(() =>
                {
                    string at = folder ?? Remote.Home(c);
                    return Tuple.Create(at, Remote.List(c, at));
                });
                if (folder == null) n.RemotePath = got.Item1;
                List<RemoteEntry> entries = got.Item2;
                n.Children.Clear();
                if (folder == null)
                {
                    // the server itself: its start folder (home), with the whole server's folders a click away
                    n.Icon = Icons.ServerOn;
                    if (n.RemotePath != "/")
                    {
                        var top = new Node
                        {
                            Name = "/  (the whole server)", Server = c, RemotePath = "/", IsFolder = true, Depth = n.Depth + 1,
                            IndentMargin = new Thickness((n.Depth + 1) * 14, 0, 0, 0), Icon = Icons.Up, Load = LoadRemote
                        };
                        top.Children.Add(Node.Placeholder("Loading…", n.Depth + 2));
                        n.Children.Add(top);
                    }
                }
                foreach (RemoteEntry e in entries)
                {
                    var k = new Node
                    {
                        Name = e.Name, Server = c, RemotePath = e.Path, IsFolder = e.IsFolder, Depth = n.Depth + 1,
                        IndentMargin = new Thickness((n.Depth + 1) * 14 + (e.IsFolder ? 0 : 16), 0, 0, 0), Icon = e.IsFolder ? Icons.Folder : Icons.For(e.Name)
                    };
                    if (e.IsFolder) { k.Load = LoadRemote; k.Children.Add(Node.Placeholder("Loading…", n.Depth + 2)); }
                    n.Children.Add(k);
                }
                if (entries.Count == 0) n.Children.Add(Node.Placeholder("Empty", n.Depth + 1));
            }
            catch (Exception ex)
            {
                n.Children.Clear();
                n.Children.Add(Node.Placeholder(ex.Message, n.Depth + 1));
                Log.Write("Servers " + c.Shown + ": " + ex.Message);
            }
        }

        // =================================================================
        // Clicking
        // =================================================================
        static Node NodeAt(MouseEventArgs e)
        {
            var d = e.OriginalSource as DependencyObject;
            while (d != null && !(d is TreeViewItem)) d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            return d != null ? ((TreeViewItem)d).DataContext as Node : null;
        }

        void Clicked(MouseButtonEventArgs e, bool remote)
        {
            Node n = NodeAt(e);
            if (n == null || n.IsPlaceholder) return;
            if (e.OriginalSource is FrameworkElement fe && fe.TemplatedParent is System.Windows.Controls.Primitives.ToggleButton) return;   // the arrow
            Activate(n);
        }

        void Activate(Node n)
        {
            if (n.IsFolder) { n.IsExpanded = !n.IsExpanded; return; }
            if (n.Server != null) { if (OpenRemote != null) OpenRemote(n.Server, n.RemotePath); }
            else if (n.Path != null && OpenLocal != null) OpenLocal(n.Path, 0);
        }

        void RightClicked(MouseButtonEventArgs e)
        {
            Node n = NodeAt(e);
            if (n == null || n.IsPlaceholder) return;
            n.IsSelected = true;
            var m = new ContextMenu();
            if (!n.IsFolder) Item(m, "", "Open", () => Activate(n));
            if (n.Path != null)
            {
                if (n.IsFolder) Item(m, "", "Show this folder here", () => SetFolder(n.Path));
                Item(m, "", "Show in its folder", () =>
                {
                    try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + n.Path + "\""); } catch { }
                });
                Item(m, "", "Copy the path", () => { try { Clipboard.SetText(n.Path); } catch { } });
            }
            if (n.Server != null)
            {
                if (n.RemotePath != null) Item(m, "", "Copy the path", () => { try { Clipboard.SetText(n.RemotePath); } catch { } });
                if (n.IsFolder) Item(m, "", "Refresh", () => n.Reset());
            }
            m.PlacementTarget = (UIElement)e.Source;
            m.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            m.IsOpen = true;
            e.Handled = true;
        }

        static void Item(ContextMenu m, string glyph, string text, Action a)
        {
            var mi = new MenuItem { Header = text };
            var g = new TextBlock { Text = glyph };
            g.SetResourceReference(TextBlock.StyleProperty, "vx.MenuGlyph");
            mi.Icon = g;
            mi.Click += (o, e) => a();
            m.Items.Add(mi);
        }

        // =================================================================
        // Search: text in the folder's files
        // =================================================================
        FrameworkElement BuildSearch()
        {
            var page = new DockPanel();
            _searchBox.Height = 30;
            _searchBox.VerticalContentAlignment = VerticalAlignment.Center;
            _searchBox.Padding = new Thickness(6, 0, 6, 0);
            _searchBox.SetResourceReference(Control.BackgroundProperty, "vx.Ctrl");
            _searchBox.SetResourceReference(Control.BorderBrushProperty, "vx.CtrlBorder");
            _searchBox.SetResourceReference(Control.ForegroundProperty, "vx.Fg");
            _searchBox.SetResourceReference(TextBox.CaretBrushProperty, "vx.Fg");
            _searchBox.ToolTip = "Text to find in the files of the folder in Files - Enter";
            _searchBox.KeyDown += (o, e) => { if (e.Key == Key.Enter) { e.Handled = true; var unused = SearchAsync(_searchBox.Text); } };
            var box = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            box.Children.Add(_searchBox);
            var ph = new TextBlock { Text = "Find in the folder's files", IsHitTestVisible = false, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
            ph.SetResourceReference(TextBlock.ForegroundProperty, "vx.FgDim");
            _searchBox.TextChanged += (o, e) => ph.Visibility = _searchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            box.Children.Add(ph);
            SetDock(box, Dock.Top);
            page.Children.Add(box);
            _searchNote.SetResourceReference(TextBlock.ForegroundProperty, "vx.FgDim");
            SetDock(_searchNote, Dock.Top);
            page.Children.Add(_searchNote);
            page.Children.Add(Scroll(_results));
            return page;
        }

        async Task SearchAsync(string text)
        {
            if (_searching != null) _searching.Cancel();
            _results.Children.Clear();
            if (string.IsNullOrWhiteSpace(text)) { _searchNote.Text = ""; return; }
            string folder = _s.Folder;
            if (folder.Length == 0 || !Directory.Exists(folder)) { _searchNote.Text = "Choose a folder in Files first."; return; }
            var cts = new CancellationTokenSource();
            _searching = cts;
            _searchNote.Text = "Looking in " + folder + "…";
            var hits = new List<Tuple<string, int, string>>();
            int files = 0;
            await Task.Run(() =>
            {
                var stack = new Stack<string>();
                stack.Push(folder);
                while (stack.Count > 0 && !cts.IsCancellationRequested && hits.Count < 500)
                {
                    string d = stack.Pop();
                    try
                    {
                        foreach (string sub in Directory.EnumerateDirectories(d))
                            if (!SkipFolders.Contains(Path.GetFileName(sub))) stack.Push(sub);
                        foreach (string f in Directory.EnumerateFiles(d))
                        {
                            if (cts.IsCancellationRequested || hits.Count >= 500) break;
                            try
                            {
                                var fi = new FileInfo(f);
                                if (fi.Length > 2 * 1024 * 1024 || fi.Length == 0) continue;
                                byte[] b = File.ReadAllBytes(f);
                                if (TextFile.LooksBinary(b)) continue;
                                files++;
                                TextFormat fmt;
                                string all = TextFile.Decode(b, out fmt);
                                if (all.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0) continue;
                                string[] lines = all.Split('\n');
                                for (int i = 0; i < lines.Length && hits.Count < 500; i++)
                                    if (lines[i].IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                                        hits.Add(Tuple.Create(f, i + 1, lines[i].Trim().TrimEnd('\r')));
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            });
            if (cts.IsCancellationRequested) return;
            _searchNote.Text = hits.Count == 0 ? "Not found in " + files + " files." :
                               hits.Count + (hits.Count >= 500 ? "+" : "") + " lines in " + hits.Select(h => h.Item1).Distinct().Count() + " files (looked in " + files + ").";
            string last = null;
            foreach (var h in hits)
            {
                if (h.Item1 != last)
                {
                    last = h.Item1;
                    string rel = h.Item1.StartsWith(folder, StringComparison.OrdinalIgnoreCase) ? h.Item1.Substring(folder.Length).TrimStart('\\') : h.Item1;
                    var head = new TextBlock { Text = rel, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 8, 0, 2), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = h.Item1 };
                    _results.Children.Add(head);
                }
                var hit = h;
                var row = new Button { HorizontalContentAlignment = HorizontalAlignment.Left, Height = 24, Padding = new Thickness(6, 0, 6, 0), ToolTip = hit.Item3 };
                row.SetResourceReference(StyleProperty, "vx.CommandButton");
                var line = new StackPanel { Orientation = Orientation.Horizontal };
                var num = new TextBlock { Text = hit.Item2.ToString(), MinWidth = 30, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center };
                num.SetResourceReference(TextBlock.ForegroundProperty, "vx.FgDim");
                line.Children.Add(num);
                line.Children.Add(new TextBlock { Text = hit.Item3.Length > 120 ? hit.Item3.Substring(0, 120) : hit.Item3, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                row.Content = line;
                row.Click += (o, e) => { if (OpenLocal != null) OpenLocal(hit.Item1, hit.Item2); };
                _results.Children.Add(row);
            }
        }

        /// <summary>The Servers tab's connected / not marks follow (a file was opened or saved).</summary>
        public void ServersChanged()
        {
            foreach (Node n in _servers.Items.OfType<Node>().SelectMany(x => x.Server != null ? new[] { x } : x.Children.ToArray()))
                if (n.Server != null && n.RemotePath != null && Remote.IsOpen(n.Server.Id) && n.Icon != Icons.ServerOn) { n.Icon = Icons.ServerOn; }
        }
    }

    /// <summary>The side panel's little pictures: a coloured dot per kind of file, a folder, a server.</summary>
    public static class Icons
    {
        public static ImageSource Folder = Glyph("", Color.FromRgb(0xB4, 0xA9, 0xBE));
        public static ImageSource Group = Glyph("", Color.FromRgb(0x9A, 0x8F, 0xA3));
        public static ImageSource Server = Glyph("", Color.FromRgb(0x9A, 0x8F, 0xA3));
        public static ImageSource ServerOn = Glyph("", Color.FromRgb(0xA6, 0xE3, 0xA1));
        public static ImageSource Up = Glyph("", Color.FromRgb(0x9A, 0x8F, 0xA3));
        static readonly Dictionary<Color, ImageSource> _dots = new Dictionary<Color, ImageSource>();

        public static ImageSource For(string name)
        {
            string lang = Languages.For(name);
            Color c;
            switch (lang)
            {
                case "YAML": case "JSON": case "XML": case "HTML": c = Color.FromRgb(0x89, 0xB4, 0xFA); break;
                case "Markdown": c = Color.FromRgb(0xF5, 0xC2, 0xE7); break;
                case "Shell": case "PowerShell": case "Batch": case "Python": case "Dockerfile": c = Color.FromRgb(0xFA, 0xB3, 0x87); break;
                case "INI / config": case "nginx": c = Color.FromRgb(0xA6, 0xE3, 0xA1); break;
                case "C#": case "JavaScript": case "TypeScript": case "CSS": case "SQL": c = Color.FromRgb(0xF9, 0xE2, 0xAF); break;
                default: c = Color.FromRgb(0x9A, 0x8F, 0xA3); break;
            }
            ImageSource img;
            if (_dots.TryGetValue(c, out img)) return img;
            var g = new DrawingGroup();
            g.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
            var b = new SolidColorBrush(c); b.Freeze();
            g.Children.Add(new GeometryDrawing(b, null, new EllipseGeometry(new Point(8, 8), 3.5, 3.5)));
            g.Freeze();
            img = new DrawingImage(g);
            img.Freeze();
            _dots[c] = img;
            return img;
        }

        static ImageSource Glyph(string glyph, Color c)
        {
            var b = new SolidColorBrush(c); b.Freeze();
            var ft = new FormattedText(glyph, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                       new Typeface("Segoe Fluent Icons"), 13, b, 1.0);
            var g = new DrawingGroup();
            g.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
            g.Children.Add(new GeometryDrawing(b, null, ft.BuildGeometry(new Point((16 - ft.Width) / 2, (16 - ft.Height) / 2))));
            g.Freeze();
            var img = new DrawingImage(g);
            img.Freeze();
            return img;
        }
    }
}
