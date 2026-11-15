using System;

namespace vPad.Core
{
    // The saved connections are vTerm's, read by vSuite.Core's VTermConnections (VTermConnection) - shared with vShell since 1.0.27.

    /// <summary>A file or folder on a server.</summary>
    public sealed class RemoteEntry
    {
        public string Name;
        public string Path;
        public bool IsFolder;
        public long Size;
    }

    /// <summary>A path on a server, written for people: "docker-vm:/opt/stacks/compose.yaml".</summary>
    public static class RemotePath
    {
        public static string Join(string folder, string name) { return (folder ?? "/").TrimEnd('/') + "/" + name; }
        public static string Parent(string path)
        {
            if (string.IsNullOrEmpty(path) || path == "/") return "/";
            string p = path.TrimEnd('/');
            int i = p.LastIndexOf('/');
            return i <= 0 ? "/" : p.Substring(0, i);
        }
        public static string Name(string path) { string p = (path ?? "").TrimEnd('/'); return p.Substring(p.LastIndexOf('/') + 1); }
    }
}
