using System;
using System.IO;
using System.Text;

namespace vPad.Core
{
    /// <summary>How a file's text is stored: its encoding (with or without a BOM) and line endings. Saving keeps them as they were.</summary>
    public sealed class TextFormat
    {
        /// <summary>"UTF-8", "UTF-16 LE", "UTF-16 BE" or "ANSI" (this PC's Windows code page).</summary>
        public string Encoding { get; set; } = "UTF-8";
        public bool Bom { get; set; }
        /// <summary>"CRLF" or "LF" (old Mac "CR" files are read and saved as CRLF).</summary>
        public string LineEnding { get; set; } = "CRLF";

        public string NewLine { get { return LineEnding == "LF" ? "\n" : "\r\n"; } }

        /// <summary>For the status line: "UTF-8", "UTF-8 with BOM", "UTF-16 LE" …</summary>
        public string EncodingText { get { return Encoding == "UTF-8" && Bom ? "UTF-8 with BOM" : Encoding; } }

        public TextFormat Copy() { return new TextFormat { Encoding = Encoding, Bom = Bom, LineEnding = LineEnding }; }

        /// <summary>The .NET encoding (with its BOM or not).</summary>
        public Encoding Get()
        {
            switch (Encoding)
            {
                case "UTF-16 LE": return new UnicodeEncoding(false, true);
                case "UTF-16 BE": return new UnicodeEncoding(true, true);
                case "ANSI": return TextFile.Ansi;
                default: return new UTF8Encoding(Bom);
            }
        }

        public static readonly string[] Encodings = { "UTF-8", "UTF-8 with BOM", "UTF-16 LE", "UTF-16 BE", "ANSI" };

        /// <summary>From the status line's choice ("UTF-8 with BOM" …).</summary>
        public void SetEncoding(string shown)
        {
            if (shown == "UTF-8 with BOM") { Encoding = "UTF-8"; Bom = true; }
            else if (shown == "UTF-8") { Encoding = "UTF-8"; Bom = false; }
            else { Encoding = shown; Bom = shown.StartsWith("UTF-16"); }
        }
    }

    /// <summary>Reading and writing text files: works out the encoding and line endings, and puts them back the same way.</summary>
    public static class TextFile
    {
        static Encoding _ansi;

        /// <summary>This PC's Windows code page (1252 for English Windows).</summary>
        public static Encoding Ansi
        {
            get
            {
                if (_ansi == null)
                {
                    try
                    {
                        System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                        _ansi = System.Text.Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
                    }
                    catch { _ansi = System.Text.Encoding.Latin1; }
                }
                return _ansi;
            }
        }

        /// <summary>The text of these bytes, and how they were stored.</summary>
        public static string Decode(byte[] b, out TextFormat format)
        {
            format = new TextFormat();
            string text;
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF)
            {
                format.Bom = true;
                text = new UTF8Encoding(false).GetString(b, 3, b.Length - 3);
            }
            else if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE)
            {
                format.Encoding = "UTF-16 LE"; format.Bom = true;
                text = new UnicodeEncoding(false, false).GetString(b, 2, b.Length - 2);
            }
            else if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF)
            {
                format.Encoding = "UTF-16 BE"; format.Bom = true;
                text = new UnicodeEncoding(true, false).GetString(b, 2, b.Length - 2);
            }
            else
            {
                try { text = new UTF8Encoding(false, true).GetString(b); }
                catch (DecoderFallbackException)
                {
                    format.Encoding = "ANSI";
                    text = Ansi.GetString(b);
                }
            }
            format.LineEnding = LineEndingOf(text);
            return text;
        }

        /// <summary>The line ending most lines use (none at all: CRLF).</summary>
        public static string LineEndingOf(string text)
        {
            int crlf = 0, lf = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '\n') continue;
                if (i > 0 && text[i - 1] == '\r') crlf++; else lf++;
                if (crlf + lf > 2000) break;
            }
            return lf > crlf ? "LF" : "CRLF";
        }

        /// <summary>The text as bytes in its format: every line ending made the format's, the BOM if it had one.</summary>
        public static byte[] Encode(string text, TextFormat format)
        {
            string nl = format.NewLine;
            var sb = new StringBuilder(text.Length + 64);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    sb.Append(nl);
                }
                else if (c == '\n') sb.Append(nl);
                else sb.Append(c);
            }
            Encoding enc = format.Get();
            byte[] body = enc.GetBytes(sb.ToString());
            byte[] pre = format.Bom ? enc.GetPreamble() : new byte[0];
            if (pre.Length == 0) return body;
            var all = new byte[pre.Length + body.Length];
            Buffer.BlockCopy(pre, 0, all, 0, pre.Length);
            Buffer.BlockCopy(body, 0, all, pre.Length, body.Length);
            return all;
        }

        /// <summary>
        /// The first character this format can't store (ANSI only holds its code page's - an arrow, an emoji or another alphabet
        /// would be saved as "?"). null = all of it fits.
        /// </summary>
        public static string Lost(string text, TextFormat format)
        {
            if (format.Encoding != "ANSI") return null;
            try
            {
                var strict = (Encoding)Ansi.Clone();
                strict.EncoderFallback = EncoderFallback.ExceptionFallback;
                strict.GetByteCount(text);
                return null;
            }
            catch (EncoderFallbackException ex)
            {
                return ex.CharUnknown != '\0' ? ex.CharUnknown.ToString() : new string(new[] { ex.CharUnknownHigh, ex.CharUnknownLow });
            }
        }

        /// <summary>Looks like a program or picture rather than text (NUL bytes near the start, and not UTF-16).</summary>
        public static bool LooksBinary(byte[] b)
        {
            if (b.Length >= 2 && ((b[0] == 0xFF && b[1] == 0xFE) || (b[0] == 0xFE && b[1] == 0xFF))) return false;
            int n = Math.Min(b.Length, 8000);
            for (int i = 0; i < n; i++) if (b[i] == 0) return true;
            return false;
        }

        /// <summary>Write the bytes safely: to a file beside it, then swapped in (a failed write leaves the old file whole).</summary>
        public static void Write(string path, byte[] bytes)
        {
            string tmp = path + ".vpad-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".tmp";
            bool keep = false;
            try
            {
                File.WriteAllBytes(tmp, bytes);     // a full disk or a share that's gone stops here: the old file isn't touched
                if (File.Exists(path))
                {
                    FileAttributes a = File.GetAttributes(path);
                    if ((a & FileAttributes.ReadOnly) != 0) throw new UnauthorizedAccessException("The file is read-only.");
                    try { File.Replace(tmp, path, null, true); }
                    catch (IOException)
                    {
                        // File.Replace can't work on some network shares: write straight over it. If that fails too the old file
                        // may be cut short, so the new text stays in the file beside it
                        try { File.WriteAllBytes(path, bytes); }
                        catch (IOException ex)
                        {
                            keep = true;
                            throw new IOException(ex.Message + " What you were saving is in " + Path.GetFileName(tmp) + ", in the same folder.", ex);
                        }
                    }
                }
                else File.Move(tmp, path);
            }
            finally { if (!keep) try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } }
        }
    }
}
