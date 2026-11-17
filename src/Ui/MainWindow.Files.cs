using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using vPad.Core;

namespace vPad.Ui
{
    /// <summary>Opening, saving and closing files; the tabs in this window.</summary>
    public sealed partial class MainWindow
    {
        /// <summary>What this window hooked onto a tab (taken off again when the tab moves to another window).</summary>
        sealed class Hooks
        {
            public Action Changed, Caret;
        }

        readonly Dictionary<DocTab, Hooks> _hooks = new Dictionary<DocTab, Hooks>();
        static readonly Stack<Tuple<string, string, string>> _closed = new Stack<Tuple<string, string, string>>();   // path, server id, remote path

        // =================================================================
        // Starting
        // =================================================================
        /// <summary>The first window: last time's files (Options), then the ones vPad was started with; a new file if nothing opened.</summary>
        async void StartUp(StartRequest r)
        {
            if (_s.RestoreTabs && _s.Tabs.Count > 0)
            {
                OpenTab active = _s.Tabs.FirstOrDefault(t => t.Active);
                // the files on this PC first, then what vPad was started with - and only then the servers' files: a server that's
                // off takes 12 seconds to say so, and the file you double-clicked used to wait behind every one of its tabs
                var remote = new List<OpenTab>();
                foreach (OpenTab t in _s.Tabs.ToList())
                {
                    if (t.Server.Length > 0) remote.Add(t);
                    else if (File.Exists(t.Path)) await OpenLocal(t.Path, t.Line, true);
                }
                if (active != null && active.Server.Length == 0)
                {
                    DocTab a = _tabs.FirstOrDefault(d => d.Is(active.Path, null, null));
                    if (a != null) Activate(a);
                }
                bool asked = r != null && r.Files.Count > 0;
                if (r != null) Run(r);
                if (_tabs.Count == 0 && remote.Count == 0) NewFile();

                var away = new HashSet<string>();
                foreach (OpenTab t in remote)
                {
                    if (away.Contains(t.Server)) continue;      // it didn't answer for its first tab: the others aren't waited for too
                    VTermConnection c = VTermConnections.Find(t.Server);
                    // only servers that sign in without asking (a saved password or a key) - no questions at start-up
                    if (c == null || !(c.Auth != "Password" || c.Secret.Length > 0)) continue;
                    DocTab front = _active;
                    int before = _tabs.Count;
                    await OpenRemote(c, t.Path, t.Line, true);
                    if (_tabs.Count == before) { if (!Remote.IsOpen(c)) away.Add(t.Server); continue; }
                    // it comes in behind what you're looking at - unless it was the one in front last time and you asked for nothing else
                    bool itsTurn = t == active && !asked && (front == null || front.IsNew);
                    if (!itsTurn && front != null && _tabs.Contains(front)) Activate(front);
                }
                if (_tabs.Count == 0) NewFile();
                return;
            }
            if (r != null) Run(r);
            if (_tabs.Count == 0) NewFile();
        }

        /// <summary>What a start of vPad asked for: open its files (the first at its line).</summary>
        public void Run(StartRequest r)
        {
            bool first = true;
            foreach (string f in r.Files)
            {
                if (Directory.Exists(f)) { _side.SetFolder(f); if (!_s.PanelOpen) TogglePanel(); _side.ShowTab("Files"); continue; }
                var unused = OpenLocal(f, first ? r.Line : 0);
                first = false;
            }
            BringUp();
        }

        // =================================================================
        // Tabs in this window
        // =================================================================
        /// <summary>Take a tab into this window (a new one, or one dragged in) at index (-1 = the end) and show it.</summary>
        void Attach(DocTab d, int index)
        {
            var h = new Hooks();
            h.Changed = () => { if (d == _active) ShowTitle(); };
            h.Caret = () => { if (d == _active) ShowStatus(); };
            d.Changed += h.Changed;
            d.CaretMoved += h.Caret;
            d.OpenFile = p => { var unused = OpenLocal(p, 0); };
            _hooks[d] = h;
            if (index < 0 || index > _tabs.Count) index = _tabs.Count;
            _tabs.Insert(index, d);
            _strip.Insert(d.Tab, index);
            d.Apply(_s);
            d.ApplyColors();
            d.ShowMarkdown(_s.MarkdownView, _s);
        }

        /// <summary>Let go of a tab (it's closing, or moving to another window).</summary>
        void Detach(DocTab d)
        {
            Hooks h;
            if (_hooks.TryGetValue(d, out h))
            {
                d.Changed -= h.Changed;
                d.CaretMoved -= h.Caret;

                _hooks.Remove(d);
            }
            int i = _tabs.IndexOf(d);
            _tabs.Remove(d);
            _strip.Remove(d.Tab);
            if (d.Content.Parent == _editorHost) _editorHost.Children.Remove(d.Content);
            if (_active == d)
            {
                _active = null;
                if (_tabs.Count > 0) Activate(_tabs[Math.Max(0, Math.Min(i, _tabs.Count - 1))]);   // the one that took its place, or the last
                else ShowNothing();
            }
        }

        /// <summary>A tab from another window comes in.</summary>
        public void Adopt(DocTab d, int index)
        {
            if (_tabs.Count == 1 && _tabs[0].IsNew && !_tabs[0].Dirty && _tabs[0].Editor.Document.TextLength == 0)
            {
                DocTab blank = _tabs[0];
                Detach(blank);
            }
            Attach(d, index);
            Activate(d);
        }

        void Activate(DocTab d)
        {
            if (d == null) return;
            if (_active != null && _active != d)
            {
                if (_active.Content.Parent == _editorHost) _editorHost.Children.Remove(_active.Content);
            }
            _active = d;
            _strip.Select(d.Tab);
            if (d.Content.Parent is Panel old && old != _editorHost) old.Children.Remove(d.Content);
            if (d.Content.Parent == null) _editorHost.Children.Insert(0, d.Content);
            _empty.Visibility = Visibility.Collapsed;
            _find.Target = d.Editor;

            ShowTitle();
            ShowStatus();
            ShowMarkdownSwitch();
            Dispatcher.BeginInvoke(new Action(() => { if (_active == d && IsActive) d.Editor.TextArea.Focus(); }), System.Windows.Threading.DispatcherPriority.Input);
        }

        void ShowNothing()
        {
            _empty.Visibility = Visibility.Visible;
            _find.Target = null;
            _find.Hide();
            ShowTitle();
            ShowStatus();
            ShowMarkdownSwitch();
        }

        void ShowTitle()
        {
            Title = _active == null ? "vPad" : (_active.Dirty ? "• " : "") + _active.Name + (_active.IsRemote ? " (" + _active.Server.Shown + ")" : "") + " - vPad";
        }

        /// <summary>A tab already open in any window for this file (it's brought forward), or null.</summary>
        static DocTab FindOpen(string path, VTermConnection server, string remotePath, out MainWindow where)
        {
            foreach (MainWindow w in All)
                foreach (DocTab d in w._tabs)
                    if (d.Is(path, server, remotePath)) { where = w; return d; }
            where = null;
            return null;
        }

        /// <summary>A new tab replaces the active one when that is an empty, untouched new file.</summary>
        void Place(DocTab d)
        {
            DocTab blank = _active != null && _active.IsNew && !_active.Dirty && _active.Editor.Document.TextLength == 0 ? _active : null;
            int at = _active != null ? _tabs.IndexOf(_active) + 1 : -1;
            Attach(d, at);
            Activate(d);
            if (blank != null) Detach(blank);
        }

        public void NewFile()
        {
            var d = new DocTab(_s);
            d.StartNew(_s);
            Attach(d, _active != null ? _tabs.IndexOf(_active) + 1 : -1);
            Activate(d);
        }

        // =================================================================
        // Opening
        // =================================================================
        void OpenDialog()
        {
            var d = new Microsoft.Win32.OpenFileDialog { Title = "Open in vPad", Multiselect = true, Filter = "All files|*.*|Text|*.txt;*.md;*.log|Config|*.yml;*.yaml;*.json;*.ini;*.conf;*.toml;*.env;*.xml" };
            string dir = _active != null && _active.Folder != null ? _active.Folder : _s.Folder;
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) d.InitialDirectory = dir;
            if (d.ShowDialog(this) != true) return;
            foreach (string f in d.FileNames) { var unused = OpenLocal(f, 0); }
        }

        /// <summary>Open a file on this PC (or bring its tab forward), at a line (0 = the top). quiet: no questions (start-up).</summary>
        public async Task OpenLocal(string path, int line, bool quiet = false)
        {
            MainWindow w;
            DocTab open = FindOpen(path, null, null, out w);
            if (open != null)
            {
                w.Activate(open);
                if (w != this) w.BringUp();
                if (line > 0) open.GoToLine(line);
                return;
            }
            byte[] bytes;
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists) { if (!quiet) Tell("There's no file " + path + "."); return; }
                if (fi.Length > 200L * 1024 * 1024) { if (!quiet) Tell(fi.Name + " is " + (fi.Length / 1024 / 1024) + " MB - too big for vPad."); return; }
                if (fi.Length > 20L * 1024 * 1024 && !quiet && !Ask.YesNo(this, "vPad", fi.Name + " is " + (fi.Length / 1024 / 1024) + " MB. Open it anyway? It may be slow.", "Open", "Cancel", false)) return;
                bytes = await Task.Run(() => File.ReadAllBytes(path));
            }
            catch (Exception ex) { if (!quiet) Tell("Couldn't open " + Path.GetFileName(path) + ": " + ex.Message); return; }
            if (TextFile.LooksBinary(bytes))
            {
                if (quiet) return;
                if (!Ask.YesNo(this, "vPad", Path.GetFileName(path) + " doesn't look like text (a program, picture or other file). Open it anyway?", "Open", "Cancel", false)) return;
            }
            // opened meanwhile (a double-click twice)?
            open = FindOpen(path, null, null, out w);
            if (open != null) { w.Activate(open); return; }
            var d = new DocTab(_s);
            d.LoadLocal(path, bytes);
            Place(d);
            if (line > 0) d.GoToLine(line);
            _s.AddRecent(path);
            _s.SaveSoon();
            _side.FolderFromFile(path);
        }

        /// <summary>Open a file on a server (vTerm's connection), at a line.</summary>
        public async Task OpenRemote(VTermConnection c, string path, int line, bool quiet = false)
        {
            MainWindow w;
            DocTab open = FindOpen(null, c, path, out w);
            if (open != null) { w.Activate(open); if (w != this) w.BringUp(); return; }
            Note("Opening " + RemotePath.Name(path) + " from " + c.Shown + "…", 30);
            byte[] bytes;
            try { bytes = await Task.Run(() => Remote.Read(c, path)); }
            catch (Exception ex)
            {
                Note("");
                if (!quiet) Tell("Couldn't open " + path + " on " + c.Shown + ": " + ex.Message);
                else Log.Write("Reopening " + c.Shown + ":" + path + ": " + ex.Message);
                return;
            }
            Note("");
            if (TextFile.LooksBinary(bytes) && (quiet || !Ask.YesNo(this, "vPad", RemotePath.Name(path) + " doesn't look like text. Open it anyway?", "Open", "Cancel", false))) return;
            open = FindOpen(null, c, path, out w);
            if (open != null) { w.Activate(open); return; }
            var d = new DocTab(_s);
            d.LoadRemote(c, path, bytes);
            Place(d);
            if (line > 1) d.GoToLine(line);
            _side.ServersChanged();
        }

        void OnDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null) return;
            e.Handled = true;
            foreach (string f in files)
            {
                if (Directory.Exists(f)) { _side.SetFolder(f); if (!_s.PanelOpen) TogglePanel(); _side.ShowTab("Files"); }
                else { var unused = OpenLocal(f, 0); }
            }
            Activate();
        }

        // =================================================================
        // Saving
        // =================================================================
        /// <summary>Save (a new file asks where). True when it's saved.</summary>
        async Task<bool> Save(DocTab d)
        {
            if (d == null) return false;
            if (d.IsNew) return await SaveAs(d);
            if (d.Busy) return false;
            Note(d.IsRemote ? "Saving to " + d.Server.Shown + "…" : "Saving…", 30);
            string err = await d.SaveAsync();
            if (err != null)
            {
                Note("");
                int pick = Ask.Show(this, "vPad", "Couldn't save " + d.Name + ":\n\n" + err, new[] { "Save as…", "OK" }, 0, 1, false, null, null);
                if (pick == 0) return await SaveAs(d);
                return false;
            }
            Note((d.IsRemote ? "Saved to " + d.Server.Shown : "Saved") + " at " + DateTime.Now.ToString("h:mm tt"));
            if (!d.IsRemote) { _s.AddRecent(d.Path); _s.SaveSoon(); }
            _side.ServersChanged();
            return true;
        }

        async Task<bool> SaveAs(DocTab d)
        {
            if (d == null) return false;
            var dlg = new Microsoft.Win32.SaveFileDialog { Title = "Save " + d.Name, FileName = d.Name, Filter = "All files|*.*", AddExtension = false, OverwritePrompt = true };
            string dir = d.Folder ?? (_s.Folder.Length > 0 ? _s.Folder : null);
            if (dir != null && Directory.Exists(dir)) dlg.InitialDirectory = dir;
            if (dlg.ShowDialog(this) != true) return false;
            if (d.IsNew && d.Language == "Plain text" && d.Format.LineEnding == _s.NewLineEnding) { }
            string err = await d.SaveToAsync(dlg.FileName);
            if (err != null) { Tell("Couldn't save " + Path.GetFileName(dlg.FileName) + ": " + err); return false; }
            d.ShowMarkdown(_s.MarkdownView, _s);
            ShowMarkdownSwitch();
            ShowStatus();
            Note("Saved at " + DateTime.Now.ToString("h:mm tt"));
            _s.AddRecent(dlg.FileName);
            _s.SaveSoon();
            _side.FolderFromFile(dlg.FileName);
            return true;
        }

        async Task SaveAll()
        {
            foreach (DocTab d in _tabs.Where(t => t.Dirty).ToList()) if (!await Save(d)) return;
        }

        /// <summary>Read it again. ask: when there are changes, ask first.</summary>
        async Task Reload(DocTab d, bool ask)
        {
            if (d == null || d.IsNew) return;
            if (ask && d.Dirty && !Ask.YesNo(this, "vPad", "Read " + d.Name + " again? Your changes to it will be lost.", "Read it again", "Cancel", true)) return;
            string err = await d.ReloadAsync();
            if (err != null) Tell("Couldn't read " + d.Name + ": " + err);
            else { Note("Read again"); ShowStatus(); }
        }

        /// <summary>The window came forward: files changed by another program are read again (asked first if they've changes here too).</summary>
        void CheckDisk()
        {
            foreach (DocTab d in _tabs.ToList())
            {
                if (d.IsNew || d.IsRemote || d.Busy) continue;
                DateTime t;
                try { if (!File.Exists(d.Path)) continue; t = File.GetLastWriteTimeUtc(d.Path); }
                catch { continue; }
                if (t == d.DiskTime) continue;
                d.DiskTime = t;
                if (!d.Dirty) { var unused = Reload(d, false); Note(d.Name + " was changed outside vPad - read again"); }
                else if (Ask.YesNo(this, "vPad", d.Name + " was changed by another program, and has changes here too.\n\nRead it again (your changes are lost), or keep yours (saving overwrites the other changes)?",
                                   "Read it again", "Keep mine", true)) { var unused = Reload(d, false); }
            }
        }

        // =================================================================
        // Closing
        // =================================================================
        /// <summary>Close a tab (asked about changes first). True when it closed.</summary>
        async Task<bool> CloseTab(DocTab d)
        {
            if (d == null) return false;
            if (d.Dirty)
            {
                Activate(d);
                int pick = Ask.Show(this, "vPad", "Save the changes to " + d.Name + "?", new[] { "Save", "Don't save", "Cancel" }, 0, 2, false, null, null);
                if (pick == 2) return false;
                if (pick == 0 && !await Save(d)) return false;
            }
            if (!d.IsNew) _closed.Push(Tuple.Create(d.Path, d.IsRemote ? d.Server.Id : null, d.RemotePath));
            Detach(d);
            return true;
        }

        /// <summary>Ctrl+Shift+T: the tab closed last opens again.</summary>
        void Reopen()
        {
            while (_closed.Count > 0)
            {
                var c = _closed.Pop();
                if (c.Item2 != null) { VTermConnection s = VTermConnections.Find(c.Item2); if (s != null) { var unused = OpenRemote(s, c.Item3, 0); return; } }
                else if (File.Exists(c.Item1)) { var unused = OpenLocal(c.Item1, 0); return; }
            }
        }

        void TabMenu(DocTab d)
        {
            Activate(d);
            var m = new ContextMenu { PlacementTarget = _strip, Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint };
            Add(m, "", "Close", "Ctrl+W", true, () => { var unused = CloseTab(d); });
            Add(m, "", "Close the others", null, _tabs.Count > 1, async () => { foreach (DocTab t in _tabs.Where(x => x != d).ToList()) if (!await CloseTab(t)) return; });
            Add(m, "", "Close the ones to the right", null, _tabs.IndexOf(d) < _tabs.Count - 1, async () => { foreach (DocTab t in _tabs.Skip(_tabs.IndexOf(d) + 1).ToList()) if (!await CloseTab(t)) return; });
            m.Items.Add(new Separator());
            Add(m, "", "Move to a new window", "Ctrl+Shift+O", _tabs.Count > 1, () => MoveToNewWindow(d, null));
            foreach (MainWindow w in All.Where(x => x != this))
            {
                MainWindow target = w;
                string name = w._active != null ? w._active.Name : "empty";
                Add(m, "", "Move to the window with " + name, null, true, () => { Detach(d); target.Adopt(d, -1); target.BringUp(); if (_tabs.Count == 0) Close(); });
            }
            m.Items.Add(new Separator());
            if (!d.IsNew)
            {
                Add(m, "", "Copy the path", null, true, () => { try { Clipboard.SetText(d.IsRemote ? d.RemotePath : d.Path); } catch { } });
                if (!d.IsRemote)
                    Add(m, "", "Show in its folder", null, true, () => { try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + d.Path + "\""); } catch { } });
                Add(m, "", "Read it again", null, true, () => { var unused = Reload(d, true); });
            }
            // after Activate's own "focus the editor" (queued at Input) - opened before it, the menu lost the focus to the editor and closed
            Dispatcher.BeginInvoke(new Action(() => { if (IsVisible) m.IsOpen = true; }), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        /// <summary>A tab into a window of its own (at a screen point in DIPs, or beside this one).</summary>
        void MoveToNewWindow(DocTab d, Point? at)
        {
            Detach(d);
            var w = new MainWindow(_s, _themeSource, null, d, at);
            // the size of this window (as it is when not maximised)
            Rect size = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            w.Width = size.Width;
            w.Height = size.Height;
            if (at == null) { w.Left = Left + 40; w.Top = Top + 40; }
            w.Show();
            w.Activate();
            if (_tabs.Count == 0) { _quietClose = true; Close(); }
        }

        bool _closing, _quietClose, _closeOk;

        void OnClosing(object sender, CancelEventArgs e)
        {
            if (!_closeOk && !_quietClose)
            {
                List<DocTab> dirty = _tabs.Where(t => t.Dirty).ToList();
                if (dirty.Count > 0)
                {
                    e.Cancel = true;
                    string q = dirty.Count == 1 ? "Save the changes to " + dirty[0].Name + "?" :
                               "Save the changes to these " + dirty.Count + " files?\n\n" + string.Join("\n", dirty.Select(t => "• " + t.Name));
                    int pick = Ask.Show(this, "vPad", q, new[] { dirty.Count == 1 ? "Save" : "Save all", "Don't save", "Cancel" }, 0, 2, false, null, null);
                    if (pick == 2) return;
                    Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        if (pick == 0) foreach (DocTab d in dirty) if (!await Save(d)) return;
                        _closeOk = true;
                        Close();
                    }));
                    return;
                }
            }
            _closing = true;
            // the last window: its files open again next time
            if (!All.Any(w => w != this))
            {
                _s.Tabs = _tabs.Where(t => !t.IsNew).Select(t => new OpenTab
                {
                    Path = t.IsRemote ? t.RemotePath : t.Path, Server = t.IsRemote ? t.Server.Id : "", Line = t.Line, Active = t == _active
                }).ToList();
                Remember();
            }
            else if (_s.Width == 0) Remember();
            _s.HostKeys = new Dictionary<string, string>(Remote.TrustedKeys);
            _s.Save();
            _themeSource.Changed -= _themeChanged;
            UpdateCenter.Changed -= _updateChanged;
        }

        void Tell(string text) { Ask.YesNo(this, "vPad", text, "OK", null, false); }
    }
}
