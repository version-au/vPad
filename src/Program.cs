using System;
using System.Reflection;
using System.Threading;
using System.Windows;
using vPad.Core;
using vPad.Ui;

namespace vPad
{
    static class Program
    {
        static Mutex _running;

        [STAThread]
        static int Main(string[] args)
        {
            // the shared library: which app this is (settings folder, log, test variables, releases), and its icon drawing
            vSuite.App.Init("vPad", "VPAD", "https://vsuite.vrsion.com");
            AppIcon.Drawer = Brand.Draw;

            if (args.Length == 2 && args[0] == "--make-icon")   // used once to make vpad.ico
            {
                AppIcon.Save(args[1]);
                return 0;
            }
            if (args.Length == 3 && args[0] == "--update-watchdog")   // the old exe after an update: start the new one, go back if it fails
                return Updater.RunWatchdog(args);

            // for the installer: "vPad is running" (AppMutex) - every vPad holds it, whatever its settings folder
            _running = new Mutex(false, "vPad.Running");
            // already running (same settings folder): it opens the files (or updates), and this one stops here
            string handOff = args.Length == 1 && UpdateCenter.IsCommand(args[0]) ? args[0] : StartRequest.ToHandOff(args);
            try { if (Instance.HandOff(handOff)) return 0; }
            catch (Exception ex) { Log.Write("Hand-off: " + ex.Message); }
            Log.Write("vPad " + Assembly.GetExecutingAssembly().GetName().Version + " starting");

            var app = new Application { ShutdownMode = ShutdownMode.OnLastWindowClose };
            app.DispatcherUnhandledException += (s, e) =>
            {
                Log.Write("Unhandled: " + e.Exception);
                Log.Flush();
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) => { Log.Write("Unhandled (" + (e.IsTerminating ? "ending" : "carrying on") + "): " + e.ExceptionObject); Log.Flush(); };
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) => { Log.Write("Unobserved task error: " + e.Exception); e.SetObserved(); };
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/vSuite.Core;component/Ui/Controls.xaml", UriKind.Absolute)
            });

            var settings = AppSettings.Load();
            var theme = new VShellThemeSource();
            Ask.FallbackTheme = () => Theme.From(theme.Current);

            // servers (vTerm's connections): questions asked on the UI thread, in the window in front
            Remote.TrustedKeys = new System.Collections.Generic.Dictionary<string, string>(settings.HostKeys);
            Remote.KeysChanged = () => app.Dispatcher.BeginInvoke(new Action(() => { settings.HostKeys = new System.Collections.Generic.Dictionary<string, string>(Remote.TrustedKeys); settings.SaveSoon(); }));
            Remote.AskYesNo = (title, q) => (bool)app.Dispatcher.Invoke(new Func<bool>(() => Ask.YesNo(MainWindow.Front, title, q, "Trust it", "Cancel", false)));
            Remote.AskPassword = (title, label) => (string)app.Dispatcher.Invoke(new Func<string>(() =>
            {
                var f = new System.Collections.Generic.List<FormField> { FormField.Password(label, "Not saved - asked again next time vPad starts. Save it with the connection in vTerm.") };
                return FormWindow.Ask(MainWindow.Front, title, f) ? f[0].Value : null;
            }));

            bool update = args.Length == 1 && UpdateCenter.HandleCommand(args[0]);   // vPad.exe --update (vShell's Settings): open as usual, update now
            StartRequest request = update ? null : StartRequest.Parse(args);
            var window = new MainWindow(settings, theme, request);
            window.ContentRendered += (s, e) => UpdateCenter.Start(settings);
            int code = app.Run(window);
            settings.Save();
            Remote.CloseAll();
            theme.Dispose();
            return code;
        }
    }
}
