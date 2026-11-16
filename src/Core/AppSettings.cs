using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;

namespace vPad.Core
{
    /// <summary>A tab that was open when vPad closed (opened again next time): a file on this PC, or on a server (vTerm connection id + path).</summary>
    public sealed class OpenTab
    {
        public string Path { get; set; } = "";
        /// <summary>vTerm's connection id ("" = a file on this PC).</summary>
        public string Server { get; set; } = "";
        public int Line { get; set; } = 1;
        public bool Active { get; set; }
    }

    /// <summary>
    /// vPad's settings (%APPDATA%\vPad\settings.json, or VPAD_DATA_DIR): the text, the window, the side panel, what was open, updates.
    /// </summary>
    public sealed class AppSettings : IUpdateSettings
    {
        // ---- the text ----
        /// <summary>Font (empty = Consolas) and size.</summary>
        public string Font { get; set; } = "";
        public double FontSize { get; set; } = 14;
        public bool WordWrap { get; set; }
        public bool LineNumbers { get; set; } = true;
        public bool HighlightLine { get; set; } = true;
        /// <summary>Spaces a Tab makes (and a tab character's width).</summary>
        public int TabSize { get; set; } = 4;
        /// <summary>The Tab key types spaces (off: a tab character).</summary>
        public bool TabSpaces { get; set; } = true;
        /// <summary>A new file's line endings: "CRLF" (Windows) or "LF".</summary>
        public string NewLineEnding { get; set; } = "CRLF";
        /// <summary>How a Markdown file shows: "Text", "Side" (side by side) or "Preview".</summary>
        public string MarkdownView { get; set; } = "Side";

        // ---- the side panel ----
        public bool PanelOpen { get; set; } = true;
        public double PanelWidth { get; set; } = 260;
        /// <summary>"Files", "Servers" or "Search".</summary>
        public string PanelTab { get; set; } = "Files";
        /// <summary>The folder the Files tab shows ("" = the first file's folder).</summary>
        public string Folder { get; set; } = "";

        // ---- what was open ----
        /// <summary>Open the files that were open last time.</summary>
        public bool RestoreTabs { get; set; } = true;
        public List<OpenTab> Tabs { get; set; } = new List<OpenTab>();
        /// <summary>Files opened lately (newest first), for the menu.</summary>
        public List<string> Recent { get; set; } = new List<string>();
        /// <summary>Servers' keys this vPad trusted (connection id -> SHA256 fingerprint) - vTerm's own are trusted too.</summary>
        public Dictionary<string, string> HostKeys { get; set; } = new Dictionary<string, string>();

        // ---- the window ----
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool Maximized { get; set; }
        public bool Topmost { get; set; }
        /// <summary>See-through at vShell's opacity, like its bars (the wallpaper behind); off = solid.</summary>
        public bool ThemeOpacity { get; set; } = true;

        // ---- updates (Options > Updates; see Updater / UpdateCenter) ----
        public bool UpdateAuto { get; set; } = true;
        public bool UpdatePre { get; set; }
        public DateTime UpdateLastCheck { get; set; } = DateTime.MinValue;
        public string UpdateSkip { get; set; } = "";

        static string FilePath { get { return Path.Combine(Log.Folder, "settings.json"); } }

        /// <summary>Set by Load when settings.json was there but couldn't be read: what was done about it, for the log and a notice.</summary>
        public static string LoadNote;
        bool _noSave, _keepBak;

        static AppSettings Read(string path)
        {
            string text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("the file is empty");
            return JsonSerializer.Deserialize<AppSettings>(text) ?? throw new InvalidDataException("the file holds nothing");
        }

        /// <summary>
        /// The settings; new ones when there's no file yet. A file that's there but can't be read is never taken for "no settings":
        /// one that's only busy is tried again; a broken one is kept as settings.broken-&lt;time&gt;.json and the save before it
        /// (settings.json.bak) is used; and when not even a copy can be kept, nothing is saved over it this run.
        /// </summary>
        public static AppSettings Load()
        {
            bool there = false;
            string why = null;
            for (int tries = 0; tries < 5; tries++)
            {
                try
                {
                    there = File.Exists(FilePath);
                    if (!there) break;
                    AppSettings s = Read(FilePath);
                    s.Normalize();
                    return s;
                }
                catch (IOException ex) when (tries < 4 && !(ex is InvalidDataException)) { why = ex.Message; System.Threading.Thread.Sleep(200); }
                catch (Exception ex) { why = ex.Message; break; }
            }
            AppSettings use = null;
            if (there)
            {
                Log.Write("Settings: settings.json couldn't be read - " + why);
                bool kept = false;
                try
                {
                    string aside = Path.Combine(Log.Folder, "settings.broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
                    File.Copy(FilePath, aside, true);
                    kept = true;
                    Log.Write("Settings: it's kept as " + aside);
                }
                catch (Exception ex) { Log.Write("Settings: couldn't keep a copy of it - " + ex.Message); }
                try
                {
                    if (File.Exists(FilePath + ".bak")) { use = Read(FilePath + ".bak"); Log.Write("Settings: using settings.json.bak (the save before the last one)"); }
                }
                catch (Exception ex) { Log.Write("Settings: settings.json.bak can't be read either - " + ex.Message); }
                if (use == null) use = new AppSettings();
                use._noSave = !kept;      // no copy of the unreadable file: leave it alone
                use._keepBak = true;      // the first save mustn't put the broken file over the good .bak
                LoadNote = kept ? "settings.json couldn't be read; a copy is kept beside it (settings.broken-…json)."
                                : "settings.json couldn't be read or copied, so nothing is saved over it until " + App.Name + " starts again.";
            }
            if (use == null) use = new AppSettings();
            use.Normalize();
            return use;
        }

        /// <summary>Values from an old or hand-edited file put right.</summary>
        public void Normalize()
        {
            if (Font == null) Font = "";
            FontSize = Math.Max(7, Math.Min(40, FontSize));
            TabSize = Math.Max(1, Math.Min(16, TabSize));
            if (NewLineEnding != "LF") NewLineEnding = "CRLF";
            if (MarkdownView != "Text" && MarkdownView != "Preview") MarkdownView = "Side";
            PanelWidth = Math.Max(160, Math.Min(700, PanelWidth));
            if (PanelTab != "Servers" && PanelTab != "Search") PanelTab = "Files";
            if (Folder == null) Folder = "";
            if (Tabs == null) Tabs = new List<OpenTab>();
            Tabs.RemoveAll(t => t == null || string.IsNullOrWhiteSpace(t.Path));
            foreach (OpenTab t in Tabs) { if (t.Server == null) t.Server = ""; if (t.Line < 1) t.Line = 1; }
            if (Recent == null) Recent = new List<string>();
            Recent.RemoveAll(string.IsNullOrWhiteSpace);
            if (Recent.Count > 15) Recent.RemoveRange(15, Recent.Count - 15);
            if (HostKeys == null) HostKeys = new Dictionary<string, string>();
            if (UpdateSkip == null) UpdateSkip = "";
        }

        public void Save()
        {
            if (_soon != null) _soon.Stop();
            if (_noSave) return;
            try
            {
                Directory.CreateDirectory(Log.Folder);
                // the file as it is (read or written by us, so it's good) stays as settings.json.bak
                if (_keepBak) _keepBak = false;
                else try { if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".bak", true); } catch { }
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(tmp, FilePath, true);
            }
            catch (Exception ex) { Log.Write("Settings save: " + ex.Message); }
        }

        DispatcherTimer _soon;

        /// <summary>Save in a moment (several changes in a row are saved once). On the UI thread.</summary>
        public void SaveSoon()
        {
            if (_soon == null)
            {
                _soon = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                _soon.Tick += (o, e) => Save();
            }
            _soon.Stop();
            _soon.Start();
        }

        /// <summary>A file was opened: first in the recent list.</summary>
        public void AddRecent(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            Recent.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            Recent.Insert(0, path);
            if (Recent.Count > 15) Recent.RemoveRange(15, Recent.Count - 15);
        }
    }
}
