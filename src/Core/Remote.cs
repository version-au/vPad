using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace vPad.Core
{
    /// <summary>
    /// Files on servers over SFTP (SSH.NET), signing in the way vTerm's saved connection says: its saved password (or asked), its key file
    /// (+ passphrase), or for "SSH agent" your default keys in ~/.ssh. One connection per server, kept open while vPad runs (and opened
    /// again if it dropped). The server's key must match the one vTerm or vPad trusted before (asked the first time; a change is warned
    /// about). Everything here blocks: call it off the UI thread.
    /// </summary>
    public static class Remote
    {
        /// <summary>Asks on the UI thread: (title, question) -> yes.</summary>
        public static Func<string, string, bool> AskYesNo;
        /// <summary>Asks for a password on the UI thread: (title, label) -> the password, or null.</summary>
        public static Func<string, string, string> AskPassword;
        /// <summary>The keys vPad trusted (AppSettings.HostKeys) and saving them.</summary>
        public static Dictionary<string, string> TrustedKeys = new Dictionary<string, string>();
        public static Action KeysChanged;

        static readonly Dictionary<string, SftpClient> _open = new Dictionary<string, SftpClient>();
        static readonly Dictionary<string, string> _typed = new Dictionary<string, string>();   // passwords typed this run
        static readonly object _lock = new object();

        /// <summary>Connected now (for the side panel's "connected").</summary>
        public static bool IsOpen(string id)
        {
            lock (_lock) { SftpClient c; return _open.TryGetValue(id, out c) && c.IsConnected; }
        }

        static void AddPassword(List<AuthenticationMethod> methods, string user, string pw)
        {
            methods.Add(new PasswordAuthenticationMethod(user, pw));
            var ki = new KeyboardInteractiveAuthenticationMethod(user);
            ki.AuthenticationPrompt += (s, e) => { foreach (AuthenticationPrompt p in e.Prompts) p.Response = pw; };
            methods.Add(ki);
        }

        /// <summary>How to sign in. typedPassword: the key was turned down - a password typed this run (as ssh asks for one then).</summary>
        static ConnectionInfo Info(VTermConnection c, string typedPassword = null)
        {
            string user = string.IsNullOrWhiteSpace(c.User) ? Environment.UserName : c.User.Trim();
            var methods = new List<AuthenticationMethod>();
            string secret = Secret.Unprotect(c.Secret ?? "");
            if (typedPassword != null)
            {
                AddPassword(methods, user, typedPassword);
                return new ConnectionInfo(c.Host.Trim(), c.Port, user, methods.ToArray()) { Timeout = TimeSpan.FromSeconds(12) };
            }
            if (c.Auth == "Password")
            {
                if (secret.Length == 0) lock (_lock) _typed.TryGetValue(c.Id, out secret);
                if (string.IsNullOrEmpty(secret) && AskPassword != null)
                {
                    secret = AskPassword("vPad - " + c.Shown, "Password for " + c.Target) ?? "";
                    if (secret.Length > 0) lock (_lock) _typed[c.Id] = secret;
                }
                if (string.IsNullOrEmpty(secret)) throw new InvalidOperationException("No password - save one with the connection in vTerm, or type it when asked.");
                AddPassword(methods, user, secret);
            }
            else
            {
                var keys = new List<IPrivateKeySource>();
                var files = new List<string>();
                string ssh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
                if (c.Auth == "Key" && !string.IsNullOrWhiteSpace(c.KeyFile)) files.Add(Environment.ExpandEnvironmentVariables(c.KeyFile));
                else foreach (string n in new[] { "id_ed25519", "id_ecdsa", "id_rsa" }) files.Add(Path.Combine(ssh, n));
                string firstErr = null;
                foreach (string f in files)
                {
                    if (!File.Exists(f)) continue;
                    try { keys.Add(secret.Length > 0 ? new PrivateKeyFile(f, secret) : new PrivateKeyFile(f)); }
                    catch (Exception ex) { if (firstErr == null) firstErr = Path.GetFileName(f) + ": " + ex.Message; }
                }
                if (keys.Count == 0 && secret.Length == 0)
                    throw new InvalidOperationException(firstErr != null ? "The key couldn't be read (" + firstErr + ") - is its passphrase saved in vTerm?" :
                                                        c.Auth == "Key" ? "The key file isn't there: " + c.KeyFile :
                                                        "No key in your .ssh folder - vPad needs a password or a key file (keys only in the SSH agent can't be used).");
                if (keys.Count > 0) methods.Add(new PrivateKeyAuthenticationMethod(user, keys.ToArray()));
                // the saved secret is also tried as the password, as ssh does when the server turns the key down
                if (secret.Length > 0) AddPassword(methods, user, secret);
            }
            return new ConnectionInfo(c.Host.Trim(), c.Port, user, methods.ToArray()) { Timeout = TimeSpan.FromSeconds(12) };
        }

        static void CheckHostKey(VTermConnection c, HostKeyEventArgs e)
        {
            string fp = "";
            try { fp = e.FingerPrintSHA256 ?? ""; } catch { }
            if (fp.Length == 0) { e.CanTrust = true; return; }
            string mine;
            lock (_lock) TrustedKeys.TryGetValue(c.Id, out mine);
            if (string.Equals(c.HostKey, fp, StringComparison.Ordinal) || string.Equals(mine, fp, StringComparison.Ordinal)) { e.CanTrust = true; return; }
            string known = !string.IsNullOrEmpty(mine) ? mine : c.HostKey;
            string q = string.IsNullOrEmpty(known)
                ? "First connection to " + c.Target + ".\n\nIts key fingerprint is SHA256:" + fp + "\n\nTrust this server?"
                : "WARNING: the key of " + c.Target + " has CHANGED since it was last trusted.\n\nWas: SHA256:" + known + "\nNow: SHA256:" + fp +
                  "\n\nThis can mean the server was reinstalled - or that someone is in between. Trust the new key?";
            bool yes = AskYesNo != null && AskYesNo("vPad - " + c.Shown, q);
            e.CanTrust = yes;
            if (yes)
            {
                lock (_lock) TrustedKeys[c.Id] = fp;
                if (KeysChanged != null) KeysChanged();
            }
        }

        /// <summary>The open connection to this server (connecting, or connecting again).</summary>
        static SftpClient Client(VTermConnection c)
        {
            SftpClient client;
            lock (_lock)
            {
                if (_open.TryGetValue(c.Id, out client) && client.IsConnected) return client;
                if (client != null) { _open.Remove(c.Id); try { client.Dispose(); } catch { } }
            }
            if (!string.IsNullOrWhiteSpace(c.Jump)) throw new InvalidOperationException("vPad can't reach a server through a jump host (" + c.Jump + ").");
            string typed;
            lock (_lock) _typed.TryGetValue(c.Id, out typed);
            if (c.Auth == "Password") typed = null;   // Info finds it
            client = Connect(c, typed);
            Log.Write("SFTP: connected to " + c.Shown);
            lock (_lock) _open[c.Id] = client;
            return client;
        }

        /// <summary>Connect and sign in. A key the server turns down (and no saved password): the password is asked, as ssh does.</summary>
        static SftpClient Connect(VTermConnection c, string typed)
        {
            var client = new SftpClient(Info(c, typed));
            VTermConnection conn = c;
            client.HostKeyReceived += (s, e) => CheckHostKey(conn, e);
            client.KeepAliveInterval = TimeSpan.FromSeconds(30);
            try { client.Connect(); return client; }
            catch (SshAuthenticationException)
            {
                client.Dispose();
                lock (_lock) _typed.Remove(c.Id);   // a typed password that was wrong: asked again next time
                if (typed == null && c.Auth != "Password" && AskPassword != null)
                {
                    string pw = AskPassword("vPad - " + c.Shown, "The server didn't take the key. Password for " + c.Target);
                    if (!string.IsNullOrEmpty(pw))
                    {
                        lock (_lock) _typed[c.Id] = pw;
                        return Connect(c, pw);
                    }
                }
                throw new InvalidOperationException("The server didn't accept the sign-in for " + c.Target + ".");
            }
            catch (SshConnectionException ex) when (ex.Message.IndexOf("Key exchange negotiation failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                    ex.Message.IndexOf("host key", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                client.Dispose();
                throw new InvalidOperationException("Not connected - the server's key wasn't trusted.");
            }
            catch { client.Dispose(); throw; }
        }

        /// <summary>Run something on the server's connection; if the connection had dropped, once more on a new one.</summary>
        static T With<T>(VTermConnection c, Func<SftpClient, T> f)
        {
            SftpClient client = Client(c);
            try { return f(client); }
            catch (SshConnectionException) { }
            catch (ObjectDisposedException) { }
            lock (_lock) { _open.Remove(c.Id); }
            try { client.Dispose(); } catch { }
            return f(Client(c));
        }

        /// <summary>Where the server's files start: vTerm's SFTP folder for it, or the home folder.</summary>
        public static string Home(VTermConnection c)
        {
            return With(c, cl => !string.IsNullOrWhiteSpace(c.SftpPath) ? c.SftpPath.Trim() : cl.WorkingDirectory);
        }

        /// <summary>A folder's folders and files (folders first, by name; "." and ".." left out).</summary>
        public static List<RemoteEntry> List(VTermConnection c, string folder)
        {
            return With(c, cl =>
            {
                var l = new List<RemoteEntry>();
                foreach (ISftpFile f in cl.ListDirectory(folder))
                {
                    if (f.Name == "." || f.Name == "..") continue;
                    bool dir = f.IsDirectory;
                    if (f.IsSymbolicLink) { try { dir = cl.GetAttributes(f.FullName).IsDirectory; } catch { } }
                    l.Add(new RemoteEntry { Name = f.Name, Path = f.FullName, IsFolder = dir, Size = f.Length });
                }
                return l.OrderBy(e => e.IsFolder ? 0 : 1).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
            });
        }

        /// <summary>A file's bytes.</summary>
        public static byte[] Read(VTermConnection c, string path)
        {
            return With(c, cl =>
            {
                long size = cl.GetAttributes(path).Size;
                if (size > 64L * 1024 * 1024) throw new InvalidOperationException("The file is over 64 MB - too big to edit here.");
                return cl.ReadAllBytes(path);
            });
        }

        /// <summary>Write a file's bytes over it (in place, so its owner and permissions stay as they were).</summary>
        public static void Write(VTermConnection c, string path, byte[] bytes)
        {
            With(c, cl =>
            {
                try
                {
                    using (var s = cl.Open(path, FileMode.Create, FileAccess.Write)) s.Write(bytes, 0, bytes.Length);
                }
                catch (SftpPermissionDeniedException)
                {
                    throw new InvalidOperationException("The server won't let " + (string.IsNullOrWhiteSpace(c.User) ? "you" : c.User) +
                                                        " write " + path + " (it belongs to another user, often root). Save a copy, or change it in vTerm with sudo.");
                }
                return true;
            });
        }

        public static void CloseAll()
        {
            lock (_lock)
            {
                foreach (SftpClient c in _open.Values) { try { c.Disconnect(); c.Dispose(); } catch { } }
                _open.Clear();
            }
        }
    }
}
