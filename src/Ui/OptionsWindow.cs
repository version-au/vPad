using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using vPad.Core;

namespace vPad.Ui
{
    /// <summary>
    /// vPad's Options (the vPad menu): the suite's Options window (vSuite's OptionsWindowBase) with vPad's pages. Every change is saved and
    /// shown in all open vPad windows at once - there's no OK button. One Options window at a time.
    /// </summary>
    public sealed class OptionsWindow : OptionsWindowBase
    {
        public static OptionsWindow Current { get; private set; }

        readonly AppSettings _s;
        readonly VShellThemeSource _themeSource;

        public static void Open(Window owner, AppSettings s, VShellThemeSource themeSource, Theme theme, bool blur, string page)
        {
            if (Current == null)
            {
                Current = new OptionsWindow(s, themeSource, theme, blur) { Owner = owner };
                Current.Show();
            }
            else Current.Activate();
            if (page != null) Current.ShowPage(page);
        }

        OptionsWindow(AppSettings s, VShellThemeSource themeSource, Theme theme, bool blur) : base(theme, blur)
        {
            _s = s;
            _themeSource = themeSource;
            Closed += (o, e) => { if (Current == this) Current = null; };
            Finish();
        }

        protected override void BuildPages()
        {
            // ---- General ----
            StackPanel general = Page("General", "", "Opening and new files");
            StackPanel c = Card(general, "Starting");
            Switch(c, "Open the files that were open last time", "When vPad starts (a file it's asked to open goes beside them)", null, _s.RestoreTabs, v => _s.RestoreTabs = v);
            c = Card(general, "New files");
            Row(c, "Line endings", "For a new file - an opened file keeps its own",
                "Windows programs use CRLF; Linux and macOS (and most files on servers) use LF. Click the line endings in the status line to change a file's.",
                Segmented(Opt("Windows (CRLF)", _s.NewLineEnding == "CRLF", () => _s.NewLineEnding = "CRLF"), Opt("Linux (LF)", _s.NewLineEnding == "LF", () => _s.NewLineEnding = "LF")));
            c = Card(general, "Markdown");
            Row(c, "Markdown files show", "Also the switch at the top while one is open", null,
                Segmented(Opt("Text", _s.MarkdownView == "Text", () => _s.MarkdownView = "Text"),
                          Opt("Side by side", _s.MarkdownView == "Side", () => _s.MarkdownView = "Side"),
                          Opt("Preview", _s.MarkdownView == "Preview", () => _s.MarkdownView = "Preview")));

            // ---- Text ----
            StackPanel text = Page("Text", "", "The font, tabs and lines");
            c = Card(text, "Font");
            Row(c, "Font", "Empty = Consolas", null, TextField(_s.Font, v => _s.Font = (v ?? "").Trim(), 220));
            Row(c, "Text size", "Ctrl + the mouse wheel, or Ctrl + / Ctrl -, change it too", null, SliderBox(7, 40, _s.FontSize, 0.5, "", v => _s.FontSize = Math.Round(v * 2) / 2));
            c = Card(text, "Tabs");
            Row(c, "Tab size", "How wide a tab is, and how many spaces Tab types", null, SliderBox(1, 16, _s.TabSize, 1, "", v => _s.TabSize = (int)Math.Round(v)));
            Switch(c, "The Tab key types spaces", "Off: a tab character", null, _s.TabSpaces, v => _s.TabSpaces = v);
            c = Card(text, "Lines");
            Switch(c, "Line numbers", null, null, _s.LineNumbers, v => _s.LineNumbers = v);
            Switch(c, "Highlight the line the cursor is on", null, null, _s.HighlightLine, v => _s.HighlightLine = v);
            Switch(c, "Wrap long lines", "Also Alt+Z", null, _s.WordWrap, v => _s.WordWrap = v);

            // ---- Look ----
            StackPanel look = Page("Look", "", "The window");
            c = Card(look, "The window");
            Switch(c, "Use the theme's opacity", "See-through like vShell's bars - off: solid",
                   "On: the window shows your wallpaper through its colour at the opacity set in vShell (blurred with Blur or Frosted), like the bars and widgets. Off: solid colours.",
                   _s.ThemeOpacity, v => _s.ThemeOpacity = v);
            Switch(c, "Keep on top of other windows", "Also the pin button at the top", null, _s.Topmost, v => _s.Topmost = v);
            Note(look, "Colours, opacity, blur and corners come from your vShell theme - change them in vShell's settings. The syntax colours follow it too.");

            // ---- Updates ----
            UpdatesPage(_s);

            // ---- About ----
            StackPanel about = Page("About", "", null);
            c = Card(about, "vPad");
            Version ver = Assembly.GetExecutingAssembly().GetName().Version;
            var info = new StackPanel();
            info.Children.Add(new TextBlock { Text = "vPad " + ver.Major + "." + ver.Minor + "." + ver.Build, FontSize = 13.5, FontWeight = FontWeights.SemiBold });
            info.Children.Add(Dim("A text editor in your vShell theme: tabs, syntax colours, Markdown with a preview, and files on your servers with vTerm's saved connections.", 12, new Thickness(0, 2, 0, 0)));
            Place(c, info);
            Row(c, "Colours", _themeSource.Found ? "Your vShell theme - vPad follows it as you change it" : "vShell's default theme (vShell's settings weren't found)", null, null);
            Row(c, "Servers", VTermConnections.Exists ? "vTerm's saved connections (" + VTermConnections.All.Count + ")" : "vTerm isn't set up - its saved connections show in Servers once it is", null, null);
            var folder = TextButton("Open", () =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Log.Folder) { UseShellExecute = true }); } catch { }
            });
            Row(c, "Settings folder", Log.Folder, null, folder);
            Row(c, "Made with", "AvalonEdit (MIT), Markdig (BSD-2), SSH.NET (MIT)", null, null);
        }

        protected override void Saved()
        {
            _s.Normalize();
            _s.Save();
            MainWindow.SettingsChanged();
        }
    }
}
