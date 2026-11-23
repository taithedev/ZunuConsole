using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ZunuConsole.Core
{
    internal enum SettingType { Number, Toggle, Choice }

    internal sealed class SettingSpec
    {
        public string Key, Description, Default;
        public SettingType Type;
        public float Minimum, Maximum, Step;
        public bool Integer;
        public string[] Choices;
        public string Category { get { return Key.Split('.')[0]; } }
    }

    internal sealed class SettingStore
    {
        private readonly Dictionary<string, SettingSpec> specs = new Dictionary<string, SettingSpec>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string file;
        public event Action<string> Changed;

        public SettingStore(string path)
        {
            file = path;
            RegisterCatalog();
            Load();
        }

        public IEnumerable<SettingSpec> All { get { return specs.Values.OrderBy(x => x.Key); } }
        public bool TryFind(string key, out SettingSpec spec) { return specs.TryGetValue(key, out spec); }
        public string Get(string key) { string v; return values.TryGetValue(key, out v) ? v : specs[key].Default; }
        public float Number(string key) { return float.Parse(Get(key), CultureInfo.InvariantCulture); }
        public int Int(string key) { return (int)Math.Round(Number(key)); }
        public bool Bool(string key) { return string.Equals(Get(key), "true", StringComparison.OrdinalIgnoreCase); }
        public string Choice(string key) { return Get(key); }

        public bool Set(string key, string raw, out string error)
        {
            error = null;
            SettingSpec s;
            if (!specs.TryGetValue(key, out s)) { error = "Unknown setting: " + key; return false; }
            string normalized;
            if (s.Type == SettingType.Toggle)
            {
                if (raw == "1" || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase)) normalized = "true";
                else if (raw == "0" || string.Equals(raw, "off", StringComparison.OrdinalIgnoreCase)) normalized = "false";
                else if (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase)) normalized = raw.ToLowerInvariant();
                else { error = "Expected true/false or on/off"; return false; }
            }
            else if (s.Type == SettingType.Choice)
            {
                normalized = s.Choices.FirstOrDefault(x => string.Equals(x, raw, StringComparison.OrdinalIgnoreCase));
                if (normalized == null) { error = "Choose: " + string.Join(", ", s.Choices); return false; }
            }
            else
            {
                float parsed;
                if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) || float.IsNaN(parsed) || float.IsInfinity(parsed))
                { error = "Expected a finite number"; return false; }
                if (parsed < s.Minimum || parsed > s.Maximum)
                { error = "Valid range: " + s.Minimum.ToString(CultureInfo.InvariantCulture) + " - " + s.Maximum.ToString(CultureInfo.InvariantCulture); return false; }
                if (s.Integer) parsed = (float)Math.Round(parsed);
                normalized = parsed.ToString(s.Integer ? "0" : "0.###", CultureInfo.InvariantCulture);
            }
            if (Get(s.Key) == normalized) return true;
            values[s.Key] = normalized;
            Save();
            if (Changed != null) Changed(s.Key);
            return true;
        }

        public bool Reset(string key, out string error)
        {
            SettingSpec s;
            if (!specs.TryGetValue(key, out s)) { error = "Unknown setting: " + key; return false; }
            return Set(key, s.Default, out error);
        }

        public void ResetAll()
        {
            var old = values.Keys.ToArray();
            values.Clear(); Save();
            foreach (var key in old) if (Changed != null) Changed(key);
        }

        private void AddNum(string key, float def, float min, float max, float step, string desc, bool integer = false)
        {
            Add(new SettingSpec { Key = key, Description = desc, Default = def.ToString(CultureInfo.InvariantCulture), Minimum = min, Maximum = max, Step = step, Integer = integer, Type = SettingType.Number });
        }
        private void AddBool(string key, bool def, string desc)
        {
            Add(new SettingSpec { Key = key, Description = desc, Default = def ? "true" : "false", Type = SettingType.Toggle });
        }
        private void AddChoice(string key, string def, string desc, params string[] options)
        {
            Add(new SettingSpec { Key = key, Description = desc, Default = def, Choices = options, Type = SettingType.Choice });
        }
        private void Add(SettingSpec spec) { specs.Add(spec.Key, spec); }

        private void RegisterCatalog()
        {
            // These options correspond to actual UI dimensions, colors, visibility or input behavior.
            AddNum("ui.scale", 1, .75f, 1.5f, .05f, "Scale the entire console");
            AddNum("ui.opacity", .98f, .5f, 1, .05f, "Window surface opacity");
            AddNum("ui.width", 900, 620, 1400, 20, "Window width", true);
            AddNum("ui.height", 560, 390, 950, 20, "Window height", true);
            AddNum("ui.rounding", 14, 0, 24, 1, "Corner radius", true);
            AddNum("ui.padding", 18, 8, 32, 1, "Window content padding", true);
            AddNum("ui.fontSize", 14, 10, 24, 1, "Normal text size", true);
            AddNum("ui.logFontSize", 13, 10, 24, 1, "Console output text size", true);
            AddNum("ui.rowHeight", 28, 20, 50, 2, "Command list row height", true);
            AddNum("ui.sidebarWidth", 174, 110, 280, 4, "Category sidebar width", true);
            AddNum("ui.maxLines", 800, 100, 5000, 100, "Maximum captured log entries", true);
            AddNum("ui.scrollSpeed", 23, 8, 70, 3, "Scroll-wheel sensitivity");
            AddNum("ui.accentR", .27f, 0, 1, .05f, "Custom accent red channel");
            AddNum("ui.accentG", .82f, 0, 1, .05f, "Custom accent green channel");
            AddNum("ui.accentB", .93f, 0, 1, .05f, "Custom accent blue channel");
            AddNum("ui.windowX", 80, 0, 3000, 20, "Free window X coordinate", true);
            AddNum("ui.windowY", 70, 0, 2000, 20, "Free window Y coordinate", true);
            AddNum("ui.headerHeight", 78, 54, 110, 2, "Title header height", true);
            AddNum("ui.inputHeight", 42, 28, 70, 2, "Command input height", true);
            AddNum("ui.suggestionsCount", 6, 2, 16, 1, "Maximum suggestions displayed", true);
            AddNum("ui.historyLimit", 250, 20, 2000, 20, "Maximum saved commands", true);
            AddNum("ui.shadowOffset", 9, 0, 25, 1, "Shadow drop distance", true);
            AddNum("ui.shadowAlpha", .4f, 0, .9f, .05f, "Window shadow strength");
            AddNum("ui.spacing", 10, 2, 24, 1, "Spacing between UI rows", true);
            AddNum("ui.borderThickness", 1, 1, 4, 1, "Panel outline width", true);
            AddNum("ui.hoverAlpha", .15f, .05f, .65f, .05f, "Highlight intensity");
            AddNum("ui.statusHeight", 31, 23, 55, 2, "Footer/status bar height", true);

            AddBool("ui.visible", false, "Start with console showing");
            AddBool("ui.showFps", true, "Show live FPS in header");
            AddBool("ui.showClock", true, "Show local clock");
            AddBool("ui.showVersion", true, "Show ZunuConsole version");
            AddBool("ui.showUptime", true, "Show session uptime");
            AddBool("ui.showUnity", true, "Show Unity version");
            AddBool("ui.showHints", true, "Show hints under input");
            AddBool("ui.showSuggestions", true, "Show command autocomplete");
            AddBool("ui.showSidebar", true, "Show category sidebar");
            AddBool("ui.showStatusBar", true, "Show bottom status bar");
            AddBool("ui.showHeader", true, "Show title header");
            AddBool("ui.showCommandCount", true, "Show registered command count");
            AddBool("ui.useTimestamps", true, "Print log timestamps");
            AddBool("ui.logColors", true, "Color log entries by severity");
            AddBool("ui.autoscroll", true, "Scroll to latest output");
            AddBool("ui.useShadows", true, "Draw window shadow");
            AddBool("ui.animations", true, "Animate focus accents");
            AddBool("ui.showSystemInfo", true, "Show system information in diagnostics");
            AddBool("ui.showInputBorder", true, "Outline the input area");
            AddBool("console.persistHistory", true, "Persist command history on disk");
            AddBool("console.captureUnityLogs", true, "Capture Unity logs in console");
            AddBool("console.includeStackTraces", false, "Include stack traces when exporting logs");
            AddBool("console.confirmClear", false, "Require confirmation before clearing output");
            AddBool("console.echoCommands", true, "Echo typed commands to console");
            AddBool("console.autoComplete", true, "Enable Tab completion");
            AddBool("console.historyArrows", true, "Enable Up/Down history navigation");
            AddBool("console.wrapLog", true, "Wrap long console output lines");
            AddBool("ui.showCategoryNames", true, "Display category labels in command browser");
            AddBool("ui.showToolbar", true, "Display top navigation tabs");

            AddChoice("ui.theme", "oled", "Background palette", "oled", "graphite", "midnight");
            AddChoice("ui.accent", "cyan", "Accent palette or custom RGB", "cyan", "white", "blue", "violet", "mint", "amber", "rose", "custom");
            AddChoice("ui.dock", "center", "Window docking", "free", "center", "top-left", "top-right", "bottom-left", "bottom-right");
            AddChoice("ui.timeFormat", "h24", "Clock format", "h12", "h24");
            AddChoice("ui.logLevel", "all", "Minimum console log level", "all", "info", "warn", "error");
            AddChoice("ui.density", "cozy", "Row density", "compact", "cozy", "spacious");
            AddChoice("console.searchMode", "contains", "Command browser matching", "contains", "prefix");
            AddChoice("console.completionMode", "first", "Autocomplete matching", "first", "longest");
            AddChoice("console.historyOrder", "newest", "History listing order", "newest", "oldest");
            AddChoice("ui.statsMode", "detailed", "Diagnostic panel detail", "simple", "detailed");
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllLines(file, values.OrderBy(x => x.Key).Select(x => x.Key + "=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(x.Value))).ToArray());
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning("ZunuConsole settings save failed: " + e.Message); }
        }
        public void Load()
        {
            values.Clear();
            if (!File.Exists(file)) return;
            try
            {
                foreach (var line in File.ReadAllLines(file))
                {
                    int pos = line.IndexOf('=');
                    if (pos < 1) continue;
                    string key = line.Substring(0, pos);
                    SettingSpec s;
                    if (!specs.TryGetValue(key, out s)) continue;
                    string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(pos + 1)));
                    string ignored;
                    // Validate without writing to disk for every entry.
                    if (Validate(s, decoded, out ignored)) values[key] = decoded;
                }
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning("ZunuConsole settings load failed: " + e.Message); }
        }
        private bool Validate(SettingSpec s, string value, out string error)
        {
            error = null;
            if (s.Type == SettingType.Toggle) return value == "true" || value == "false";
            if (s.Type == SettingType.Choice) return s.Choices.Any(c => c == value);
            float v;
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && !float.IsNaN(v) && !float.IsInfinity(v) && v >= s.Minimum && v <= s.Maximum;
        }
    }
}
