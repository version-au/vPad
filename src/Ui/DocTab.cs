using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using vPad.Core;

namespace vPad.Ui
{
    /// <summary>
    /// One open file (or a new, unsaved one): its editor (AvalonEdit), where it lives (a path on this PC, or a server from vTerm's
    /// connections and a path there), how it's stored (TextFormat), its language, and its tab button. For Markdown the editor sits beside a
    /// live preview (Text / Side by side / Preview). A tab moves between vPad windows as it is - the editor goes with it.
    /// </summary>
    public sealed class DocTab
    {
        static int _untitled;

        public readonly TextEditor Editor;
        /// <summary>What the window shows for this tab: the editor, or the editor and the Markdown preview.</summary>
        public readonly Grid Content = new Grid();
        /// <summary>Its tab in the strip (vSuite's StripTab - it moves with the file from window to window).</summary>
        public readonly StripTab Tab;

        /// <summary>A file on this PC (null for a server's file or a new one).</summary>
        public string Path { get; private set; }
        /// <summary>A server's file: the connection and its path there.</summary>
        public VTermConnection Server { get; private set; }
        public string RemotePath { get; private set; }
        public TextFormat Format = new TextFormat();
        public string Language { get; private set; } = "Plain text";
        /// <summary>When the file on disk was last written, as vPad last read / saved it (to notice changes made elsewhere).</summary>
        public DateTime DiskTime;
        public bool Busy;

        string _newName = "Untitled";
        readonly Border _chip = new Border { Padding = new Thickness(6, 1, 6, 1), CornerRadius = new CornerRadius(4) };
        readonly TextBlock _chipText = new TextBlock { FontSize = 11 };
        MarkdownView _preview;
        GridSplitter _split;

        /// <summary>The title or saved state changed (the window redraws the tab and its own title).</summary>
        public event Action Changed;
        /// <summary>The caret moved / the text changed (the status line).</summary>
        public event Action CaretMoved;

        public DocTab(AppSettings s)
        {
            Editor = new TextEditor
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 6, 0, 0),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FocusVisualStyle = null
            };
            Editor.SetResourceReference(Control.ForegroundProperty, "vx.Fg");
            Editor.Options.EnableHyperlinks = true;
            Editor.Options.RequireControlModifierForHyperlinkClick = true;
            Editor.Options.EnableEmailHyperlinks = false;
            Editor.Options.AllowScrollBelowDocument = true;
            Editor.Options.EnableRectangularSelection = true;
            Editor.Options.CutCopyWholeLine = true;
            Editor.TextArea.Caret.PositionChanged += (o, e) => { if (CaretMoved != null) CaretMoved(); };
            Editor.Document.UndoStack.PropertyChanged += (o, e) => { if (e.PropertyName == "IsOriginalFile") Redraw(); };
            Editor.TextChanged += (o, e) => { if (_preview != null && _preview.IsVisible) _preview.Soon(Editor.Text, Folder); if (CaretMoved != null) CaretMoved(); };
            Apply(s);

            Content.ColumnDefinitions.Add(new ColumnDefinition());
            Content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
            Content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
            Content.Children.Add(Editor);

            // ---- its tab: the file's dot, name, the server's name as a chip, unsaved dot / close ----
            _chip.Child = _chipText;
            _chip.SetResourceReference(Border.BackgroundProperty, "vx.Selected");
            Tab = new StripTab(this, "");
            Redraw();
        }

        // =================================================================
        // Names and state
        // =================================================================
        public bool IsRemote { get { return Server != null; } }
        public bool IsNew { get { return Path == null && Server == null; } }
        public bool Dirty { get { return !Editor.Document.UndoStack.IsOriginalFile; } }

        /// <summary>The file's name ("Untitled 2" for a new one).</summary>
        public string Name { get { return IsRemote ? RemotePath.Substring(RemotePath.LastIndexOf('/') + 1) : Path != null ? System.IO.Path.GetFileName(Path) : _newName; } }

        /// <summary>Where it is, in full (the tooltip, the window title).</summary>
        public string Where { get { return IsRemote ? Server.Shown + ":" + RemotePath : Path ?? _newName; } }

        /// <summary>The folder it's in (for the Markdown preview's pictures and links): a local folder, or null.</summary>
        public string Folder { get { return Path != null ? System.IO.Path.GetDirectoryName(Path) : null; } }

        /// <summary>The same file (same path, or same server and path).</summary>
        public bool Is(string path, VTermConnection server, string remotePath)
        {
            if (server != null) return Server != null && Server.Id == server.Id && RemotePath == remotePath;
            return Path != null && path != null && string.Equals(Path, path, StringComparison.OrdinalIgnoreCase);
        }

        public void Redraw()
        {
            if (Tab == null) return;
            Tab.Title = Name;
            Tab.ToolTip = Where;
            Tab.Icon = Icons.For(IsNew ? "new.txt" : Name);
            if (IsRemote) { _chipText.Text = Server.Shown; Tab.Extra = _chip; }
            else Tab.Extra = null;
            Tab.Dirty = Dirty;
            if (Changed != null) Changed();
        }

        // =================================================================
        // The editor's look and options (from Options)
        // =================================================================
        public void Apply(AppSettings s)
        {
            // Consolas by default: WPF draws the variable Cascadia fonts too heavy
            string font = string.IsNullOrWhiteSpace(s.Font) ? "Consolas, Courier New" : s.Font.Trim() + ", Consolas";
            Editor.FontFamily = new FontFamily(font);
            Editor.FontSize = s.FontSize;
            Editor.WordWrap = s.WordWrap;
            Editor.ShowLineNumbers = s.LineNumbers;
            Editor.Options.HighlightCurrentLine = s.HighlightLine;
            Editor.Options.IndentationSize = s.TabSize;
            Editor.Options.ConvertTabsToSpaces = s.TabSpaces;
            // no dotted line after the line numbers: a little room instead
            foreach (UIElement m in Editor.TextArea.LeftMargins)
            {
                var l = m as Line;
                if (l != null) { l.Visibility = Visibility.Hidden; l.Margin = new Thickness(6, 0, 4, 0); }
                var ln = m as ICSharpCode.AvalonEdit.Editing.LineNumberMargin;
                if (ln != null) { ln.SetResourceReference(Control.ForegroundProperty, "vx.FgDim"); ln.Opacity = 0.75; }
            }
            ApplyColors();
            if (_preview != null) _preview.Apply(s);
        }

        /// <summary>The theme's colours for the caret, selection and current line (the text's own colour is vx.Fg).</summary>
        public void ApplyColors()
        {
            var fg = Editor.TryFindResource("vx.Fg") as SolidColorBrush;
            var accent = Editor.TryFindResource("vx.Accent") as SolidColorBrush;
            Color f = fg != null ? fg.Color : Colors.White, a = accent != null ? accent.Color : Colors.SteelBlue;
            Editor.TextArea.Caret.CaretBrush = new SolidColorBrush(a);
            Editor.TextArea.SelectionBrush = Frozen(Color.FromArgb(0x55, a.R, a.G, a.B));
            Editor.TextArea.SelectionBorder = null;
            Editor.TextArea.SelectionForeground = null;
            Editor.TextArea.SelectionCornerRadius = 2;
            Editor.TextArea.TextView.CurrentLineBackground = Frozen(Color.FromArgb(0x0E, f.R, f.G, f.B));
            Editor.TextArea.TextView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);
            Editor.TextArea.TextView.LinkTextForegroundBrush = new SolidColorBrush(a);
            Editor.TextArea.TextView.Redraw();
        }

        static SolidColorBrush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

        // =================================================================
        // Language and the Markdown preview
        // =================================================================
        public void SetLanguage(string language)
        {
            Language = language ?? "Plain text";
            Editor.SyntaxHighlighting = Syntax.For(Language);
            if (CaretMoved != null) CaretMoved();
        }

        /// <summary>How a Markdown file shows ("Text", "Side", "Preview"); anything else shows the text alone.</summary>
        public void ShowMarkdown(string how, AppSettings s)
        {
            bool md = Languages.IsMarkdown(Language);
            if (!md) how = "Text";
            if (how != "Text" && _preview == null)
            {
                _preview = new MarkdownView(s) { OpenLink = OpenLink };
                Grid.SetColumn(_preview, 2);
                _split = new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, ResizeBehavior = GridResizeBehavior.PreviousAndNext };
                Grid.SetColumn(_split, 1);
                var line = new Border { Width = 1, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false };
                line.SetResourceReference(Border.BackgroundProperty, "vx.Line");
                Grid.SetColumn(line, 1);
                Content.Children.Add(line);
                Content.Children.Add(_split);
                Content.Children.Add(_preview);
            }
            ColumnDefinition c0 = Content.ColumnDefinitions[0], c1 = Content.ColumnDefinitions[1], c2 = Content.ColumnDefinitions[2];
            switch (how)
            {
                case "Side":
                    c0.Width = new GridLength(1, GridUnitType.Star); c1.Width = new GridLength(5); c2.Width = new GridLength(1, GridUnitType.Star);
                    Editor.Visibility = Visibility.Visible; _preview.Visibility = Visibility.Visible;
                    break;
                case "Preview":
                    c0.Width = new GridLength(0); c1.Width = new GridLength(0); c2.Width = new GridLength(1, GridUnitType.Star);
                    Editor.Visibility = Visibility.Collapsed; _preview.Visibility = Visibility.Visible;
                    break;
                default:
                    c0.Width = new GridLength(1, GridUnitType.Star); c1.Width = new GridLength(0); c2.Width = new GridLength(0);
                    Editor.Visibility = Visibility.Visible;
                    if (_preview != null) _preview.Visibility = Visibility.Collapsed;
                    break;
            }
            if (_preview != null) _split.Visibility = how == "Side" ? Visibility.Visible : Visibility.Collapsed;
            if (_preview != null && _preview.Visibility == Visibility.Visible) _preview.Now(Editor.Text, Folder);
        }

        /// <summary>A link in the preview: web pages in the browser; another file opened in vPad (OpenFile).</summary>
        public Action<string> OpenFile;

        void OpenLink(string href)
        {
            if (string.IsNullOrWhiteSpace(href) || href.StartsWith("#")) return;
            try
            {
                Uri u;
                if (Uri.TryCreate(href, UriKind.Absolute, out u) && (u.Scheme == "http" || u.Scheme == "https" || u.Scheme == "mailto"))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(u.AbsoluteUri) { UseShellExecute = true });
                    return;
                }
                if (Folder == null) return;
                string target = System.IO.Path.GetFullPath(System.IO.Path.Combine(Folder, Uri.UnescapeDataString(href.Split('#')[0])));
                if (File.Exists(target) && OpenFile != null) OpenFile(target);
            }
            catch (Exception ex) { Log.Write("Link: " + ex.Message); }
        }

        // =================================================================
        // Loading and saving
        // =================================================================
        /// <summary>Put text in (a file just read): nothing to undo, saved state.</summary>
        void SetText(string text, TextFormat f, string languageFrom)
        {
            Format = f;
            Editor.Document.Text = text;
            Editor.Document.UndoStack.ClearAll();
            Editor.Document.UndoStack.MarkAsOriginalFile();
            string lang = Languages.For(languageFrom);
            if (lang == "Plain text")
            {
                DocumentLine first = Editor.Document.LineCount > 0 ? Editor.Document.GetLineByNumber(1) : null;
                string byLine = first != null ? Languages.FromFirstLine(Editor.Document.GetText(first)) : null;
                if (byLine != null) lang = byLine;
            }
            SetLanguage(lang);
            Editor.ScrollToHome();
            Editor.TextArea.Caret.Offset = 0;
            Redraw();
        }

        /// <summary>A new, empty file.</summary>
        public void StartNew(AppSettings s)
        {
            _newName = "Untitled " + (++_untitled);
            SetText("", new TextFormat { LineEnding = s.NewLineEnding }, "");
        }

        /// <summary>Read a file on this PC (the caller has checked it's text). Throws when it can't be read.</summary>
        public void LoadLocal(string path, byte[] bytes)
        {
            TextFormat f;
            string text = TextFile.Decode(bytes, out f);
            Path = path;
            Server = null; RemotePath = null;
            try { DiskTime = File.GetLastWriteTimeUtc(path); } catch { }
            SetText(text, f, path);
        }

        /// <summary>A server's file, already read.</summary>
        public void LoadRemote(VTermConnection c, string remotePath, byte[] bytes)
        {
            TextFormat f;
            string text = TextFile.Decode(bytes, out f);
            Server = c; RemotePath = remotePath; Path = null;
            SetText(text, f, remotePath);
        }

        /// <summary>Read the file again from disk / the server (changes are lost - ask first).</summary>
        public async Task<string> ReloadAsync()
        {
            try
            {
                int caret = Editor.TextArea.Caret.Offset;
                double scroll = Editor.VerticalOffset;
                byte[] b;
                if (IsRemote) { VTermConnection c = Server; string p = RemotePath; b = await Task.Run(() => Remote.Read(c, p)); }
                else b = File.ReadAllBytes(Path);
                TextFormat f;
                string text = TextFile.Decode(b, out f);
                Format = f;
                Editor.Document.Text = text;
                Editor.Document.UndoStack.ClearAll();
                Editor.Document.UndoStack.MarkAsOriginalFile();
                if (!IsRemote) try { DiskTime = File.GetLastWriteTimeUtc(Path); } catch { }
                Editor.TextArea.Caret.Offset = Math.Min(caret, Editor.Document.TextLength);
                Editor.ScrollToVerticalOffset(scroll);
                Redraw();
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>Save where it is. Returns null, or what went wrong. (A new file needs SaveTo.)</summary>
        public async Task<string> SaveAsync()
        {
            if (IsNew) return "It has no file yet.";
            string lost = TextFile.Lost(Editor.Text, Format);
            if (lost != null) return "It's stored as ANSI, which can't hold \"" + lost + "\" - it would be saved as \"?\". Click ANSI on the status line, choose UTF-8, and save again.";
            byte[] bytes = TextFile.Encode(Editor.Text, Format);
            Busy = true;
            try
            {
                if (IsRemote)
                {
                    VTermConnection c = Server; string p = RemotePath;
                    await Task.Run(() => Remote.Write(c, p, bytes));
                }
                else
                {
                    string p = Path;
                    await Task.Run(() => TextFile.Write(p, bytes));
                    try { DiskTime = File.GetLastWriteTimeUtc(p); } catch { }
                }
                Editor.Document.UndoStack.MarkAsOriginalFile();
                Redraw();
                return null;
            }
            catch (Exception ex) { return ex.Message; }
            finally { Busy = false; }
        }

        /// <summary>Save as a file on this PC (Save as…; a server's file saved as a copy here).</summary>
        public async Task<string> SaveToAsync(string path)
        {
            string oldPath = Path; VTermConnection oldServer = Server; string oldRemote = RemotePath;
            Path = path; Server = null; RemotePath = null;
            string err = await SaveAsync();
            if (err != null) { Path = oldPath; Server = oldServer; RemotePath = oldRemote; Redraw(); return err; }
            if (Language == "Plain text" || Languages.For(path) != "Plain text") SetLanguage(Languages.For(path));
            Redraw();
            return null;
        }

        /// <summary>The line the caret is on (1-based).</summary>
        public int Line { get { return Editor.TextArea.Caret.Line; } }

        public void GoToLine(int line, int column = 1)
        {
            line = Math.Max(1, Math.Min(Editor.Document.LineCount, line));
            Editor.TextArea.Caret.Location = new TextLocation(line, Math.Max(1, column));
            Editor.TextArea.Caret.BringCaretToView();
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => Editor.ScrollTo(line, column)), DispatcherPriority.Loaded);
        }
    }
}
