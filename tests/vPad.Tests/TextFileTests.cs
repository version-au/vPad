using System;
using System.IO;
using System.Text;
using vPad.Core;
using vPad.Ui;
using Xunit;

namespace vPad.Tests
{
    /// <summary>Reading a file and saving it again must give back the same bytes - its encoding, BOM and line endings kept.</summary>
    public class TextFileTests
    {
        static byte[] Bytes(params byte[] b) { return b; }

        [Fact]
        public void Utf8WithoutBomRoundTrips()
        {
            byte[] b = Encoding.UTF8.GetBytes("héllo\nwörld\n");
            TextFormat f;
            string text = TextFile.Decode(b, out f);
            Assert.Equal("UTF-8", f.Encoding);
            Assert.False(f.Bom);
            Assert.Equal("LF", f.LineEnding);
            Assert.Equal(b, TextFile.Encode(text, f));
        }

        [Fact]
        public void Utf8BomIsKept()
        {
            byte[] b = new UTF8Encoding(true).GetPreamble();
            byte[] all = new byte[b.Length + 6];
            Buffer.BlockCopy(b, 0, all, 0, b.Length);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes("a\r\nb\r\n"), 0, all, b.Length, 6);
            TextFormat f;
            string text = TextFile.Decode(all, out f);
            Assert.True(f.Bom);
            Assert.Equal("CRLF", f.LineEnding);
            Assert.Equal("a\r\nb\r\n", text);
            Assert.Equal(all, TextFile.Encode(text, f));
        }

        [Fact]
        public void Utf16LittleEndianRoundTrips()
        {
            byte[] b = new UnicodeEncoding(false, true).GetPreamble();
            byte[] body = new UnicodeEncoding(false, false).GetBytes("x\r\ny");
            byte[] all = new byte[b.Length + body.Length];
            Buffer.BlockCopy(b, 0, all, 0, b.Length);
            Buffer.BlockCopy(body, 0, all, b.Length, body.Length);
            TextFormat f;
            string text = TextFile.Decode(all, out f);
            Assert.Equal("UTF-16 LE", f.Encoding);
            Assert.Equal(all, TextFile.Encode(text, f));
        }

        [Fact]
        public void NotUtf8IsAnsiAndStaysAnsi()
        {
            // "café" in Windows-1252: é = 0xE9, not valid UTF-8 on its own
            byte[] b = Bytes(0x63, 0x61, 0x66, 0xE9, 0x0D, 0x0A);
            TextFormat f;
            string text = TextFile.Decode(b, out f);
            Assert.Equal("ANSI", f.Encoding);
            Assert.Equal(b, TextFile.Encode(text, f));
        }

        [Fact]
        public void AnsiSaysWhatItCantHold()
        {
            var ansi = new TextFormat { Encoding = "ANSI" };
            Assert.Null(TextFile.Lost("plain text", ansi));
            Assert.Equal("😀", TextFile.Lost("fine until 😀 here", ansi));     // in no Windows code page
            Assert.Null(TextFile.Lost("fine until 😀 here", new TextFormat()));  // UTF-8 holds everything
        }

        [Fact]
        public void MixedLineEndingsAreMadeTheFilesOwn()
        {
            var f = new TextFormat { LineEnding = "LF" };
            Assert.Equal("a\nb\nc\n", Encoding.UTF8.GetString(TextFile.Encode("a\r\nb\nc\r", f)));
            f.LineEnding = "CRLF";
            Assert.Equal("a\r\nb\r\nc\r\n", Encoding.UTF8.GetString(TextFile.Encode("a\r\nb\nc\r", f)));
        }

        [Theory]
        [InlineData("a\r\nb\r\nc\n", "CRLF")]
        [InlineData("a\nb\nc\r\n", "LF")]
        [InlineData("one line", "CRLF")]
        public void LineEndingIsWhatMostLinesUse(string text, string expected)
        {
            Assert.Equal(expected, TextFile.LineEndingOf(text));
        }

        [Fact]
        public void BinaryIsSpotted()
        {
            Assert.True(TextFile.LooksBinary(Bytes(0x4D, 0x5A, 0x90, 0x00, 0x03)));
            Assert.False(TextFile.LooksBinary(Encoding.UTF8.GetBytes("plain text")));
            Assert.False(TextFile.LooksBinary(new UnicodeEncoding(false, true).GetPreamble()));   // UTF-16 has NULs but is text
        }

        [Theory]
        [InlineData("UTF-8 with BOM", "UTF-8", true)]
        [InlineData("UTF-8", "UTF-8", false)]
        [InlineData("UTF-16 BE", "UTF-16 BE", true)]
        [InlineData("ANSI", "ANSI", false)]
        public void EncodingChoices(string shown, string encoding, bool bom)
        {
            var f = new TextFormat();
            f.SetEncoding(shown);
            Assert.Equal(encoding, f.Encoding);
            Assert.Equal(bom, f.Bom);
            Assert.Equal(shown, f.EncodingText);
        }

        [Fact]
        public void WriteReplacesTheFileWhole()
        {
            string dir = Path.Combine(Path.GetTempPath(), "vpad-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string p = Path.Combine(dir, "a.txt");
                TextFile.Write(p, Encoding.UTF8.GetBytes("first"));
                TextFile.Write(p, Encoding.UTF8.GetBytes("second"));
                Assert.Equal("second", File.ReadAllText(p));
                Assert.Single(Directory.GetFiles(dir));        // no temp file left beside it
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void AReadOnlyFileIsNotWritten()
        {
            string dir = Path.Combine(Path.GetTempPath(), "vpad-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string p = Path.Combine(dir, "ro.txt");
            try
            {
                File.WriteAllText(p, "keep");
                File.SetAttributes(p, FileAttributes.ReadOnly);
                Assert.Throws<UnauthorizedAccessException>(() => TextFile.Write(p, Encoding.UTF8.GetBytes("changed")));
                Assert.Equal("keep", File.ReadAllText(p));
            }
            finally { File.SetAttributes(p, FileAttributes.Normal); Directory.Delete(dir, true); }
        }
    }

    public class LanguageTests
    {
        [Theory]
        [InlineData(@"C:\x\docker-compose.yml", "YAML")]
        [InlineData("/opt/stacks/compose.yaml", "YAML")]
        [InlineData(@"C:\x\README.md", "Markdown")]
        [InlineData("/etc/nginx/conf.d/jellyfin.conf", "nginx")]
        [InlineData("/etc/nginx/nginx.conf", "nginx")]
        [InlineData(@"C:\x\app.conf", "INI / config")]
        [InlineData(@"C:\x\Dockerfile", "Dockerfile")]
        [InlineData(@"C:\x\.env", "INI / config")]
        [InlineData("/root/.bashrc", "Shell")]
        [InlineData(@"C:\x\backup.ps1", "PowerShell")]
        [InlineData(@"C:\x\notes.txt", "Plain text")]
        [InlineData(@"C:\x\noextension", "Plain text")]
        public void FromTheFileName(string path, string language)
        {
            Assert.Equal(language, Languages.For(path));
        }

        [Theory]
        [InlineData("#!/bin/bash", "Shell")]
        [InlineData("#!/usr/bin/env python3", "Python")]
        [InlineData("#!/usr/bin/env pwsh", "PowerShell")]
        [InlineData("# just a comment", null)]
        public void FromTheFirstLine(string line, string language)
        {
            Assert.Equal(language, Languages.FromFirstLine(line));
        }

        [Fact]
        public void EveryLanguageHasItsColours()
        {
            foreach (var kv in Languages.All)
                if (kv.Value.Length > 0)
                    Assert.NotNull(typeof(Languages).Assembly.GetManifestResourceStream("vPad.Syntax." + kv.Value + ".xshd"));
        }
    }

    public class StartRequestTests
    {
        [Fact]
        public void FilesAndALine()
        {
            StartRequest r = StartRequest.Parse(new[] { "--line", "12", @"C:\a.txt", @"C:\b.md", "--new-window" });
            Assert.Equal(12, r.Line);
            Assert.True(r.NewWindow);
            Assert.Equal(new[] { @"C:\a.txt", @"C:\b.md" }, r.Files.ToArray());
        }

        [Fact]
        public void HandOffRoundTrips()
        {
            StartRequest r = StartRequest.FromHandOff(StartRequest.ToHandOff(new[] { "--line", "3", @"C:\x y\z.yml" }));
            Assert.Equal(3, r.Line);
            Assert.Equal(@"C:\x y\z.yml", r.Files[0]);
        }

        [Fact]
        public void NotAHandOff()
        {
            Assert.Null(StartRequest.FromHandOff("something else"));
        }
    }
}
