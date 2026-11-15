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

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                    if (s != null) { s.Normalize(); return s; }
                }
            }
            catch (Exception ex) { Log.Write("Settings: " + ex.Message); }
            var fresh = new AppSettings();
            fresh.Normalize();
            return fresh;
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
            try
            {
                Directory.CreateDirectory(Log.Folder);
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
