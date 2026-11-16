using System;
using System.IO;
using vPad.Core;
using Xunit;

namespace vPad.Tests
{
    public class SettingsFileTests
    {
        [Fact]
        public void An_unreadable_settings_file_is_kept_and_the_save_before_is_used()
        {
            string dir = Path.Combine(Path.GetTempPath(), "vPad-settings-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string before = Environment.GetEnvironmentVariable("VPAD_DATA_DIR");
            try
            {
                Environment.SetEnvironmentVariable("VPAD_DATA_DIR", dir);
                vSuite.App.Init("vPad", "VPAD", "https://example.invalid");
                string file = Path.Combine(dir, "settings.json");
                var s = AppSettings.Load();
                Assert.Null(AppSettings.LoadNote);                       // no file yet: just new settings
                s.Font = "One"; s.Save();
                s = AppSettings.Load(); s.Font = "Two"; s.Save();
                Assert.Equal("Two", AppSettings.Load().Font);

                File.WriteAllText(file, "{ \"Font\": \"Tw");             // cut short
                var r = AppSettings.Load();
                Assert.Equal("One", r.Font);                             // the save before
                Assert.NotNull(AppSettings.LoadNote);
                Assert.Single(Directory.GetFiles(dir, "settings.broken-*.json"));
                r.Save();                                                // the broken file doesn't go over the good .bak
                Assert.Contains("One", File.ReadAllText(file + ".bak"));
                Assert.Equal("One", AppSettings.Load().Font);

                File.WriteAllText(file, "");                             // empty, and no usable .bak: new settings, the file kept
                File.WriteAllText(file + ".bak", "nonsense");
                Assert.Equal("", AppSettings.Load().Font ?? "");
                Assert.NotEmpty(Directory.GetFiles(dir, "settings.broken-*.json"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("VPAD_DATA_DIR", before);
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }
}
