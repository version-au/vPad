using System;
using System.Collections.Generic;
using System.IO;

namespace vPad.Core
{
    /// <summary>
    /// Which syntax colours a file gets, from its name (Languages.For), and the names shown in the status line. Each language's colours
    /// are a src\Syntax\*.xshd built into vPad.exe ("" = plain text).
    /// </summary>
    public static class Languages
    {
        /// <summary>Shown name -> definition file (without .xshd); "Plain text" has none.</summary>
        public static readonly List<KeyValuePair<string, string>> All = new List<KeyValuePair<string, string>>
        {
            Kv("Plain text", ""), Kv("Batch", "Batch"), Kv("C#", "CSharp"), Kv("CSS", "CSS"), Kv("Dockerfile", "Dockerfile"),
            Kv("HTML", "XML"), Kv("INI / config", "INI"), Kv("JavaScript", "JavaScript"), Kv("JSON", "JSON"), Kv("Markdown", "Markdown"),
            Kv("nginx", "Nginx"), Kv("PowerShell", "PowerShell"), Kv("Python", "Python"), Kv("Shell", "Shell"), Kv("SQL", "SQL"),
            Kv("TypeScript", "JavaScript"), Kv("XML", "XML"), Kv("YAML", "YAML")
        };

        static KeyValuePair<string, string> Kv(string a, string b) { return new KeyValuePair<string, string>(a, b); }

        static readonly Dictionary<string, string> ByExt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".yml", "YAML" }, { ".yaml", "YAML" },
            { ".json", "JSON" }, { ".jsonc", "JSON" }, { ".json5", "JSON" }, { ".code-workspace", "JSON" },
            { ".xml", "XML" }, { ".xaml", "XML" }, { ".csproj", "XML" }, { ".props", "XML" }, { ".targets", "XML" }, { ".config", "XML" },
            { ".svg", "XML" }, { ".xshd", "XML" }, { ".manifest", "XML" }, { ".resx", "XML" }, { ".plist", "XML" },
            { ".html", "HTML" }, { ".htm", "HTML" }, { ".xhtml", "HTML" },
            { ".ini", "INI / config" }, { ".cfg", "INI / config" }, { ".conf", "INI / config" }, { ".toml", "INI / config" },
            { ".env", "INI / config" }, { ".properties", "INI / config" }, { ".editorconfig", "INI / config" }, { ".inf", "INI / config" },
            { ".reg", "INI / config" }, { ".service", "INI / config" }, { ".desktop", "INI / config" }, { ".gitconfig", "INI / config" },
            { ".sh", "Shell" }, { ".bash", "Shell" }, { ".zsh", "Shell" }, { ".ksh", "Shell" }, { ".bashrc", "Shell" }, { ".profile", "Shell" },
            { ".ps1", "PowerShell" }, { ".psm1", "PowerShell" }, { ".psd1", "PowerShell" },
            { ".cmd", "Batch" }, { ".bat", "Batch" },
            { ".py", "Python" }, { ".pyw", "Python" },
            { ".cs", "C#" }, { ".csx", "C#" },
            { ".js", "JavaScript" }, { ".mjs", "JavaScript" }, { ".cjs", "JavaScript" }, { ".jsx", "JavaScript" },
            { ".ts", "TypeScript" }, { ".tsx", "TypeScript" }, { ".mts", "TypeScript" },
            { ".css", "CSS" }, { ".scss", "CSS" }, { ".less", "CSS" },
            { ".md", "Markdown" }, { ".markdown", "Markdown" }, { ".mdown", "Markdown" },
            { ".sql", "SQL" }, { ".dockerfile", "Dockerfile" }
        };

        /// <summary>The shown language for a file name (a full path or just the name; '/' paths from servers too).</summary>
        public static string For(string path)
        {
            if (string.IsNullOrEmpty(path)) return "Plain text";
            string p = path.Replace('\\', '/');
            string name = p.Substring(p.LastIndexOf('/') + 1);
            string lower = name.ToLowerInvariant();
            string folder = p.ToLowerInvariant();
            if (lower == "dockerfile" || lower.StartsWith("dockerfile.") || lower.EndsWith(".dockerfile")) return "Dockerfile";
            if (lower == "nginx.conf" || folder.Contains("/nginx/") || folder.Contains("/sites-available/") || folder.Contains("/sites-enabled/") ||
                (folder.Contains("/conf.d/") && lower.EndsWith(".conf"))) return "nginx";
            if (lower == ".env" || lower.StartsWith(".env.") || lower == ".gitconfig" || lower == ".editorconfig") return "INI / config";
            if (lower == ".bashrc" || lower == ".zshrc" || lower == ".profile" || lower == ".bash_profile" || lower == ".bash_aliases") return "Shell";
            if (lower == "makefile" || lower == ".gitignore" || lower == ".dockerignore" || lower == "hosts") return "Plain text";
            string ext = Path.GetExtension(name);
            string lang;
            return ext.Length > 0 && ByExt.TryGetValue(ext, out lang) ? lang : "Plain text";
        }

        /// <summary>The definition file for a shown language ("" = none).</summary>
        public static string FileFor(string shown)
        {
            foreach (var kv in All) if (kv.Key == shown) return kv.Value;
            return "";
        }

        /// <summary>A file with a shebang line ("#!/bin/bash", "#!/usr/bin/env python3"): its language, or null.</summary>
        public static string FromFirstLine(string firstLine)
        {
            if (string.IsNullOrEmpty(firstLine) || !firstLine.StartsWith("#!")) return null;
            string l = firstLine.ToLowerInvariant();
            if (l.Contains("python")) return "Python";
            if (l.Contains("pwsh") || l.Contains("powershell")) return "PowerShell";
            if (l.Contains("node")) return "JavaScript";
            if (l.Contains("sh")) return "Shell";
            return null;
        }

        /// <summary>The file is Markdown (the preview can show it).</summary>
        public static bool IsMarkdown(string shown) { return shown == "Markdown"; }
    }
}
