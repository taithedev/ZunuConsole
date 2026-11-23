using System;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;
using ZunuConsole.Core;

namespace ZunuConsole.Commands
{
    internal static class GeneralCommands
    {
        private static void Add(ZunuPlugin p, string name, string description, string category, string usage, Action<CommandContext, string[]> action)
        { p.Engine.Register(name, description, category, usage, action); }
        public static void Register(ZunuPlugin p)
        {
            Add(p, "help", "Show command help or usage for a named command", "Core", "help [command]", (c,a) =>
            {
                if (a.Length == 0) { c.Say("Try commands, search <text>, help <name>, or settings.list. Open the Commands tab to browse everything."); return; }
                var cmd = p.Engine.Find(a[0]);
                if (cmd == null) { c.Error("Unknown command: " + a[0]); return; }
                c.Say(cmd.Name + " | " + cmd.Description + " | Usage: " + cmd.Usage);
            });
            Add(p, "commands", "Print the registered command count and browser hint", "Core", "commands", (c,a) => c.Say(p.Engine.Count + " commands loaded. Use Commands tab for searchable listing."));
            Add(p, "search", "Search registered command names and descriptions", "Core", "search <term>", (c,a) =>
            {
                string term = CommandEngine.ArgText(a);
                if (term.Length == 0) { c.Warn("Usage: search <term>"); return; }
                var found = p.Engine.Search(term).Take(35).ToArray();
                c.Say(found.Length == 0 ? "No matching commands." : string.Join(", ", found.Select(x => x.Name)));
            });
            Add(p, "echo", "Print text", "Core", "echo <text>", (c,a) => c.Say(CommandEngine.ArgText(a)));
            Add(p, "clear", "Clear console output", "Core", "clear [confirm]", (c,a) =>
            {
                if (p.Settings.Bool("console.confirmClear") && (a.Length == 0 || a[0] != "confirm")) { c.Warn("Type clear confirm to clear output."); return; }
                p.Log.Clear(); c.Success("Output cleared.");
            });
            Add(p, "about", "Show plugin information", "Core", "about", (c,a) => c.Say("ZunuConsole 1.2.0 | Local PC Gorilla Tag console | BepInEx 5 | F8 to toggle"));
            Add(p, "version", "Display plugin version", "Core", "version", (c,a) => c.Say("ZunuConsole v1.2.0"));
            Add(p, "count", "Display total registered commands", "Core", "count", (c,a) => c.Say(p.Engine.Count.ToString(CultureInfo.InvariantCulture) + " commands"));
            Add(p, "history", "Show recent commands", "Core", "history [count]", (c,a) =>
            {
                int n = 15;
                if (a.Length > 0 && (!int.TryParse(a[0], out n) || n < 1 || n > 100)) { c.Error("Count must be 1-100"); return; }
                var seq = p.History.Entries;
                var entries = p.Settings.Choice("console.historyOrder") == "newest" ? seq.Reverse() : seq;
                foreach (string item in entries.Take(n).ToArray()) c.Say(item);
            });
            Add(p, "history.clear", "Erase stored command history", "Core", "history.clear", (c,a) => { p.History.Clear(); c.Success("History cleared."); });
            Add(p, "history.save", "Write command history to disk", "Core", "history.save", (c,a) => { p.History.Save(); c.Success("History saved."); });
            Add(p, "history.load", "Reload command history from disk", "Core", "history.load", (c,a) => { p.History.Load(); c.Success("History reloaded."); });
            Add(p, "history.search", "Search previously used commands", "Core", "history.search <term>", (c,a) =>
            { string term = CommandEngine.ArgText(a); foreach (string h in p.History.Entries.Where(x => x.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0).TakeLastCompat(25)) c.Say(h); });
            Add(p, "settings.list", "List settings and values", "Core", "settings.list [prefix]", (c,a) =>
            { foreach (var s in p.Settings.All.Where(x => a.Length == 0 || x.Key.StartsWith(a[0], StringComparison.OrdinalIgnoreCase)).Take(70)) c.Say(s.Key + " = " + p.Settings.Get(s.Key)); });
            Add(p, "settings.search", "Search settings by description", "Core", "settings.search <term>", (c,a) =>
            { string term = CommandEngine.ArgText(a); foreach (var s in p.Settings.All.Where(x => x.Key.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 || x.Description.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0).Take(30)) c.Say(s.Key + " = " + p.Settings.Get(s.Key)); });
            Add(p, "settings.resetall", "Restore every console setting to defaults", "Core", "settings.resetall confirm", (c,a) =>
            { if (a.Length == 0 || a[0] != "confirm") { c.Warn("To reset all: settings.resetall confirm"); return; } p.Settings.ResetAll(); c.Success("All settings restored to defaults."); });
            Add(p, "settings.save", "Save settings file", "Core", "settings.save", (c,a) => { p.Settings.Save(); c.Success("Settings saved."); });
            Add(p, "settings.reload", "Reload saved settings", "Core", "settings.reload", (c,a) => { p.Settings.Load(); c.Success("Settings reloaded."); });
            Add(p, "settings.path", "Show local settings path", "Core", "settings.path", (c,a) => c.Say(Path.Combine(Paths.ConfigPath, "ZunuConsole.settings")));
            Add(p, "ui.show", "Open the console", "Interface", "ui.show", (c,a) => { string error; p.Settings.Set("ui.visible", "true", out error); });
            Add(p, "ui.hide", "Close the console", "Interface", "ui.hide", (c,a) => { string error; p.Settings.Set("ui.visible", "false", out error); });
            Add(p, "ui.toggle", "Toggle console visibility", "Interface", "ui.toggle", (c,a) => p.ToggleConsole());
            Add(p, "ui.center", "Dock the console in the center", "Interface", "ui.center", (c,a) => { string error; p.Settings.Set("ui.dock", "center", out error); c.Success("Window centered."); });
            Add(p, "ui.resetposition", "Reset free window placement", "Interface", "ui.resetposition", (c,a) =>
            { string error; p.Settings.Set("ui.windowX", "80", out error); p.Settings.Set("ui.windowY", "70", out error); p.Settings.Set("ui.dock", "free", out error); c.Success("Window position restored."); });
            Add(p, "ui.tab", "Switch console tab", "Interface", "ui.tab <console|commands|settings|diagnostics>", (c,a) =>
            { if (a.Length == 0 || !p.Overlay.SelectTab(a[0])) c.Warn("Usage: ui.tab <console|commands|settings|diagnostics>"); else c.Success("Tab selected."); });
            Add(p, "ui.focus", "Focus the command input", "Interface", "ui.focus", (c,a) => { p.Overlay.FocusInput(); c.Success("Input focused."); });
            Add(p, "ui.search", "Set command browser query", "Interface", "ui.search <query>", (c,a) => { p.Overlay.SetSearch(CommandEngine.ArgText(a)); c.Success("Browser search changed."); });
            Add(p, "ui.screenshot", "Capture an ordinary screenshot to local app folder", "Interface", "ui.screenshot", (c,a) =>
            { string file = Path.Combine(Paths.ConfigPath, "ZunuConsole", "screenshot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png"); Directory.CreateDirectory(Path.GetDirectoryName(file)); UnityEngine.ScreenCapture.CaptureScreenshot(file); c.Success("Screenshot requested: " + file); });
            Add(p, "ui.command", "Prefill the input with a command", "Interface", "ui.command <name>", (c,a) => { p.Overlay.Prefill(CommandEngine.ArgText(a)); c.Success("Input prefilled."); });

            Add(p, "unity.version", "Display running Unity version", "Diagnostics", "unity.version", (c,a) => c.Say(Application.unityVersion));
            Add(p, "app.version", "Display running game version", "Diagnostics", "app.version", (c,a) => c.Say(Application.version));
            Add(p, "app.name", "Display game product name", "Diagnostics", "app.name", (c,a) => c.Say(Application.productName));
            Add(p, "app.company", "Display game company name", "Diagnostics", "app.company", (c,a) => c.Say(Application.companyName));
            Add(p, "app.platform", "Display runtime platform", "Diagnostics", "app.platform", (c,a) => c.Say(Application.platform.ToString()));
            Add(p, "app.data", "Display persistent local data folder", "Diagnostics", "app.data", (c,a) => c.Say(Application.persistentDataPath));
            Add(p, "app.fps", "Display current sampled frames per second", "Diagnostics", "app.fps", (c,a) => c.Say(p.Fps.ToString("F1", CultureInfo.InvariantCulture) + " FPS (smoothed)"));
            Add(p, "app.targetfps", "Show Unity target frame rate", "Diagnostics", "app.targetfps", (c,a) => c.Say("Target FPS: " + Application.targetFrameRate));
            Add(p, "app.targetfps.set", "Set local target FPS (-1 for platform default)", "Diagnostics", "app.targetfps.set <-1|30..300>", (c,a) =>
            { int n; if (a.Length == 0 || !int.TryParse(a[0], out n) || !(n == -1 || n >= 30 && n <= 300)) { c.Warn("Valid values: -1 or 30..300"); return; } Application.targetFrameRate = n; c.Success("Target FPS set to " + n + ". VR runtime may override it."); });
            Add(p, "app.vsync", "Show local VSync setting", "Diagnostics", "app.vsync", (c,a) => c.Say("VSync count: " + QualitySettings.vSyncCount));
            Add(p, "app.vsync.set", "Set VSync count locally", "Diagnostics", "app.vsync.set <0..4>", (c,a) =>
            { int n; if (a.Length == 0 || !int.TryParse(a[0], out n) || n < 0 || n > 4) { c.Warn("Valid VSync: 0..4"); return; } QualitySettings.vSyncCount = n; c.Success("VSync set to " + n + ". VR runtime may override it."); });
            Add(p, "graphics.quality", "Show current Unity quality preset", "Diagnostics", "graphics.quality", (c,a) => c.Say("Quality: " + QualitySettings.names[QualitySettings.GetQualityLevel()]));
            Add(p, "graphics.list", "List quality presets", "Diagnostics", "graphics.list", (c,a) =>
            { for (int i = 0; i < QualitySettings.names.Length; i++) c.Say(i + ": " + QualitySettings.names[i]); });
            Add(p, "graphics.quality.set", "Change local Unity quality preset", "Diagnostics", "graphics.quality.set <index>", (c,a) =>
            { int n; if (a.Length == 0 || !int.TryParse(a[0], out n) || n < 0 || n >= QualitySettings.names.Length) { c.Warn("Use graphics.list for available indices."); return; } QualitySettings.SetQualityLevel(n, true); c.Success("Quality: " + QualitySettings.names[n]); });
            Add(p, "graphics.resolution", "Show render window resolution", "Diagnostics", "graphics.resolution", (c,a) => c.Say(Screen.width + "x" + Screen.height));
            Add(p, "graphics.device", "Show GPU model", "Diagnostics", "graphics.device", (c,a) => c.Say(SystemInfo.graphicsDeviceName));
            Add(p, "graphics.vendor", "Show GPU vendor", "Diagnostics", "graphics.vendor", (c,a) => c.Say(SystemInfo.graphicsDeviceVendor));
            Add(p, "graphics.memory", "Show GPU memory estimate", "Diagnostics", "graphics.memory", (c,a) => c.Say(SystemInfo.graphicsMemorySize + " MB GPU memory"));
            Add(p, "graphics.api", "Show graphics device backend", "Diagnostics", "graphics.api", (c,a) => c.Say(SystemInfo.graphicsDeviceType.ToString()));
            Add(p, "system.os", "Show OS name", "Diagnostics", "system.os", (c,a) => c.Say(SystemInfo.operatingSystem));
            Add(p, "system.cpu", "Show CPU model", "Diagnostics", "system.cpu", (c,a) => c.Say(SystemInfo.processorType));
            Add(p, "system.cores", "Show logical CPU count", "Diagnostics", "system.cores", (c,a) => c.Say(SystemInfo.processorCount.ToString()));
            Add(p, "system.memory", "Show system memory estimate", "Diagnostics", "system.memory", (c,a) => c.Say(SystemInfo.systemMemorySize + " MB RAM"));
            Add(p, "system.gc", "Show managed memory use", "Diagnostics", "system.gc", (c,a) => c.Say((GC.GetTotalMemory(false) / 1048576f).ToString("F1", CultureInfo.InvariantCulture) + " MB managed"));
            Add(p, "system.uptime", "Show elapsed realtime seconds", "Diagnostics", "system.uptime", (c,a) => c.Say(Time.realtimeSinceStartup.ToString("F1", CultureInfo.InvariantCulture) + " sec"));
            Add(p, "session.uptime", "Show ZunuConsole active duration", "Diagnostics", "session.uptime", (c,a) => c.Say((Time.realtimeSinceStartup - p.StartTime).ToString("F1", CultureInfo.InvariantCulture) + " sec"));
            Add(p, "logs.count", "Count local console output lines", "Diagnostics", "logs.count", (c,a) => c.Say(p.Log.Count.ToString()));
            Add(p, "logs.export", "Export local console output to a text file", "Diagnostics", "logs.export", (c,a) => c.Success("Saved: " + p.Log.ExportTo(Path.Combine(Paths.ConfigPath, "ZunuConsole", "logs"))));
            Add(p, "logs.copy", "Copy console output to clipboard", "Diagnostics", "logs.copy", (c,a) => { GUIUtility.systemCopyBuffer = p.Log.ExportText(); c.Success("Copied to clipboard."); });
            Add(p, "logs.test", "Write a test entry for each log severity", "Diagnostics", "logs.test", (c,a) =>
            { c.Say("Example info"); c.Success("Example success"); c.Warn("Example warning"); c.Error("Example error"); });
            Add(p, "audio.volume", "Show local game audio volume", "Audio", "audio.volume", (c,a) => c.Say("AudioListener volume: " + AudioListener.volume.ToString("F2", CultureInfo.InvariantCulture)));
            Add(p, "audio.volume.set", "Change local game audio volume", "Audio", "audio.volume.set <0..1>", (c,a) =>
            { float n; if (a.Length == 0 || !float.TryParse(a[0], NumberStyles.Float, CultureInfo.InvariantCulture, out n) || float.IsNaN(n) || n < 0 || n > 1) { c.Warn("Valid volume: 0..1"); return; } AudioListener.volume = n; c.Success("Local volume: " + n.ToString("F2")); });
            Add(p, "audio.mute", "Mute local game output", "Audio", "audio.mute", (c,a) => { AudioListener.pause = true; c.Success("Local game audio paused."); });
            Add(p, "audio.unmute", "Unmute local game output", "Audio", "audio.unmute", (c,a) => { AudioListener.pause = false; c.Success("Local game audio resumed."); });
            Add(p, "audio.status", "Show local audio pause and volume settings", "Audio", "audio.status", (c,a) => c.Say("Paused: " + AudioListener.pause + "; volume: " + AudioListener.volume));
        }
    }
}
