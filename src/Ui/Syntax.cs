using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using vPad.Core;

namespace vPad.Ui
{
    /// <summary>
    /// The syntax colours: each language's definition (src\Syntax\*.xshd, built in) loaded once, and its named colours (Comment, String,
    /// Number, Keyword, Key …) painted from the vShell theme - keys and keywords in the Main Accent, variables and attributes in the
    /// Secondary Accent, comments and punctuation in dimmed text, strings / numbers / types in green / orange / yellow that suit a dark or
    /// light theme. A theme change repaints them (Paint) and the editors redraw.
    /// </summary>
    public static class Syntax
    {
        static readonly Dictionary<string, IHighlightingDefinition> _defs = new Dictionary<string, IHighlightingDefinition>();
        static Theme _theme;

        /// <summary>The definition for a shown language (null = plain text).</summary>
        public static IHighlightingDefinition For(string language)
        {
            string file = Languages.FileFor(language);
            if (file.Length == 0) return null;
            IHighlightingDefinition d;
            if (_defs.TryGetValue(file, out d)) return d;
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("vPad.Syntax." + file + ".xshd"))
                using (var r = XmlReader.Create(s))
                    d = HighlightingLoader.Load(r, HighlightingManager.Instance);
            }
            catch (Exception ex) { Log.Write("Syntax " + file + ": " + ex.Message); d = null; }
            _defs[file] = d;
            if (d != null && _theme != null) PaintOne(d, _theme);
            return d;
        }

        /// <summary>The theme changed: every loaded definition takes its colours.</summary>
        public static void Paint(Theme t)
        {
            _theme = t;
            foreach (IHighlightingDefinition d in _defs.Values) if (d != null) PaintOne(d, t);
        }

        static void PaintOne(IHighlightingDefinition d, Theme t)
        {
            bool dark = t.IsDark;
            Color fg = t.Fg;
            Color green = dark ? Rgb(0xA6, 0xE3, 0xA1) : Rgb(0x2E, 0x8B, 0x2E);
            Color orange = dark ? Rgb(0xFA, 0xB3, 0x87) : Rgb(0xC8, 0x5A, 0x0C);
            Color yellow = dark ? Rgb(0xF9, 0xE2, 0xAF) : Rgb(0x9A, 0x6B, 0x00);
            Color accent = Readable(t.Accent, t, dark), secondary = Readable(t.Secondary, t, dark);
            Set(d, "Comment", Mix(fg, t.Bg, 0.55));
            Set(d, "String", green);
            Set(d, "Code", green);
            Set(d, "Number", orange);
            Set(d, "Type", yellow);
            Set(d, "Keyword", accent);
            Set(d, "Key", accent);
            Set(d, "Heading", accent);
            Set(d, "Link", accent);
            Set(d, "Tag", accent);
            Set(d, "Variable", secondary);
            Set(d, "Attr", secondary);
            Set(d, "Punct", Mix(fg, t.Bg, 0.7));
        }

        static void Set(IHighlightingDefinition d, string name, Color c)
        {
            HighlightingColor hc = d.GetNamedColor(name);
            if (hc == null) return;
            hc.Foreground = new SimpleHighlightingBrush(c);
        }

        /// <summary>An accent that's too close to the background (a pale accent on a light theme) is darkened / lightened to read.</summary>
        static Color Readable(Color c, Theme t, bool dark)
        {
            double l = Lum(c), bg = Lum(t.Bg);
            if (Math.Abs(l - bg) >= 0.3) return c;
            return dark ? Mix(c, Colors.White, 0.6) : Mix(c, Colors.Black, 0.6);
        }

        static Color Mix(Color a, Color b, double f)   // f of a, the rest b (alpha ignored)
        {
            return Color.FromRgb((byte)(a.R * f + b.R * (1 - f)), (byte)(a.G * f + b.G * (1 - f)), (byte)(a.B * f + b.B * (1 - f)));
        }

        static double Lum(Color c) { return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0; }
        static Color Rgb(byte r, byte g, byte b) { return Color.FromRgb(r, g, b); }
    }
}
