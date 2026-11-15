using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using vPad.Core;

namespace vPad.Ui
{
    /// <summary>
    /// Tests only, as vXplorer's: with VPAD_TEST_HOOKS=1 a test drives a vPad window by WM_COPYDATA (kind TestKind, UTF-16 text) - the
    /// window needn't be in front, and nothing touches the real mouse or keyboard (moving them took over the PC of whoever was using it).
    /// Each command calls what its key or button calls. Without the variable these messages are ignored. scripts\test-send.ps1 sends one.
    /// </summary>
    static class TestHooks
    {
        public const int TestKind = 0x76580002;
        public static bool On { get { return App.TestHooks; } }

        [StructLayout(LayoutKind.Sequential)]
        struct COPYDATASTRUCT { public IntPtr dwData; public int cbData; public IntPtr lpData; }

        /// <summary>The command in a WM_COPYDATA, or null (not a test command, or test hooks off).</summary>
        public static string Received(IntPtr lParam)
        {
            if (!On || lParam == IntPtr.Zero) return null;
            var cds = Marshal.PtrToStructure<COPYDATASTRUCT>(lParam);
            if (cds.dwData.ToInt64() != TestKind || cds.lpData == IntPtr.Zero) return null;
            return Marshal.PtrToStringUni(cds.lpData, Math.Max(0, cds.cbData / 2)).TrimEnd('\0');
        }
    }

    public sealed partial class MainWindow
    {
        /// <summary>
        /// open &lt;path&gt; [line] | new | type &lt;text&gt; (at the caret; \n = a new line) | save | saveas &lt;path&gt; | close (the tab) | closewindow
        /// | tab &lt;name&gt; | line &lt;n&gt; | find &lt;text&gt; | replaceall &lt;find&gt;|&lt;with&gt; | markdown Text|Side|Preview | panel
        /// Files|Servers|Search | folder &lt;path&gt; | wrap | newwindow | moveout (the active tab into a new window) | options [page] | dump &lt;file&gt;
        /// (every window: its tabs - name, unsaved, server, language, encoding, line ending - the active one, the caret, the status line).
        /// Questions (save changes? …) answer themselves with VPAD_TEST_ASK=yes / no.
        /// </summary>
        void RunTestCommand(string command)
        {
            string verb = command.Split(' ')[0].ToLowerInvariant();
            string arg = command.Length > verb.Length ? command.Substring(verb.Length + 1) : "";
            try
            {
                switch (verb)
                {
                    case "open":
                    {
                        int line = 0, sp = arg.LastIndexOf(' ');
                        string path = arg;
                        if (sp > 0 && int.TryParse(arg.Substring(sp + 1), out line)) path = arg.Substring(0, sp); else line = 0;
                        var unused = OpenLocal(path, line);
                        break;
                    }
                    case "new": NewFile(); break;
                    case "type": if (_active != null) _active.Editor.Document.Insert(_active.Editor.CaretOffset, arg.Replace("\\n", "\n")); break;
                    case "save": { var unused = Save(_active); break; }
                    case "saveas": if (_active != null) { var unused = _active.SaveToAsync(arg); } break;
                    case "close": { var unused = CloseTab(_active); break; }
                    case "closewindow": Close(); break;
                    case "tab": { DocTab d = _tabs.FirstOrDefault(t => string.Equals(t.Name, arg, StringComparison.OrdinalIgnoreCase)); if (d != null) Activate(d); break; }
                    case "line": { int n; if (_active != null && int.TryParse(arg, out n)) _active.GoToLine(n); break; }
                    case "find": _find.Open(false); break;
                    case "replaceall":
                        if (_active != null && arg.Contains("|"))
                        {
                            string[] p = arg.Split(new[] { '|' }, 2);
                            _active.Editor.Document.Text = _active.Editor.Document.Text.Replace(p[0], p[1]);
                        }
                        break;
                    case "markdown": SetMarkdownView(arg); break;
                    case "panel": PanelTab(arg); break;
                    case "folder": _side.SetFolder(arg); break;
                    case "wrap": ToggleWrap(); break;
                    case "newwindow": OpenWindow(null); break;
                    case "moveout": if (_active != null && _tabs.Count > 1) MoveToNewWindow(_active, null); break;
                    case "options": OpenOptions(arg.Length > 0 ? arg : null); break;
                    case "dump": File.WriteAllText(arg, Dump()); break;
                }
            }
            catch (Exception ex) { Log.Write("Test command '" + command + "': " + ex); }
        }

        /// <summary>Every vPad window and its tabs, as text (the dump command).</summary>
        static string Dump()
        {
            var sb = new StringBuilder();
            sb.AppendLine("windows: " + All.Count);
            foreach (MainWindow w in All)
            {
                sb.AppendLine("window: " + w.Title + (w.IsActive ? " (in front)" : ""));
                foreach (DocTab d in w._tabs)
                    sb.AppendLine("  tab: " + d.Name + (d == w._active ? " [active]" : "") + (d.Dirty ? " [unsaved]" : "") +
                                  (d.IsRemote ? " [server " + d.Server.Shown + "]" : "") + " | " + d.Language + " | " + d.Format.EncodingText + " | " +
                                  d.Format.LineEnding + " | line " + d.Line + " | " + d.Editor.Document.TextLength + " chars");
                sb.AppendLine("  status: " + w._pos.Text + " / " + w._note.Text);
                sb.AppendLine("  markdown: " + w._s.MarkdownView + (w._mdBox.Visibility == System.Windows.Visibility.Visible ? " (switch shown)" : ""));
                sb.AppendLine("  panel: " + (w._s.PanelOpen ? w._s.PanelTab : "hidden") + "  folder: " + w._s.Folder);
            }
            return sb.ToString();
        }
    }
}
