using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace vPad.Ui
{
    /// <summary>
    /// What vPad was started with: files to open (`vPad.exe a.txt b.md`), the line to go to (`--line 12`, for the first file), and
    /// `--new-window`. A second start hands it to the running vPad as "vpad:" + JSON (Instance.HandOff / WM_COPYDATA).
    /// </summary>
    public sealed class StartRequest
    {
        public List<string> Files { get; set; } = new List<string>();
        public int Line { get; set; }
        public bool NewWindow { get; set; }

        const string Prefix = "vpad:";

        public static StartRequest Parse(string[] args)
        {
            var r = new StartRequest();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--line" && i + 1 < args.Length) { int n; if (int.TryParse(args[++i], out n)) r.Line = n; continue; }
                if (a == "--new-window") { r.NewWindow = true; continue; }
                if (a.StartsWith("--")) continue;
                if (a.Trim().Length == 0) continue;
                string p = a.Trim().Trim('"');
                try { p = Path.GetFullPath(p); } catch { }
                r.Files.Add(p);
            }
            return r;
        }

        /// <summary>For another start's hand-off (the files with full paths, so the running vPad finds them).</summary>
        public static string ToHandOff(string[] args)
        {
            return Prefix + JsonSerializer.Serialize(Parse(args));
        }

        public static StartRequest FromHandOff(string text)
        {
            if (text == null || !text.StartsWith(Prefix)) return null;
            try { return JsonSerializer.Deserialize<StartRequest>(text.Substring(Prefix.Length)); }
            catch (Exception ex) { Log.Write("Hand-off: " + ex.Message); return null; }
        }
    }
}
