using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZunuConsole.Core;

namespace ZunuConsole.UI
{
    // IMGUI overlay for the desktop / PC mirror. Does not inject a 3D VR wrist menu.
    internal sealed class ConsoleOverlay : IDisposable
    {
        private readonly ZunuPlugin plugin;
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, GUIStyle> panels = new Dictionary<string, GUIStyle>();
        private readonly string[] tabs = { "Console", "Commands", "Settings", "Diagnostics", "Cloud" };
        private Rect window = new Rect(80, 70, 900, 560);
        private Vector2 consoleScroll, categoryScroll, commandScroll, settingsScroll, cloudScroll;
        private string input = "", commandSearch = "", settingSearch = "", category = "All";
        private string cloudUrlInput = "", cloudTokenInput = "";
        private bool initializedCloudInput;
        private int selectedTab, historyPosition = -1;
        private int lastLineCount = -1;
        private float lastSample;
        private readonly Queue<float> fpsHistory = new Queue<float>();
        private bool focusRequested, searchFocusRequested;
        private bool layoutReady, resizeActive, resizePending, resizeFinished, collapsed;
        private float pendingWidth, pendingHeight;
        private Vector2 resizeStartMouse, resizeStartSize;
        private float layoutScreenW, layoutScreenH;
        private float rememberedWidth, rememberedHeight, rememberedX, rememberedY;
        private string rememberedDock;
        private const int ResizeControlHash = 0x5A4E550;

        private Color background, surface, surface2, border, text, muted, accent;
        private float scaleFactor = 1f;
        private int font;

        public ConsoleOverlay(ZunuPlugin owner) { plugin = owner; }
        private SettingStore S { get { return plugin.Settings; } }
        public void FocusInput() { selectedTab = 0; focusRequested = true; }
        public void Prefill(string textToEnter) { input = textToEnter ?? ""; FocusInput(); }
        public void SetSearch(string value) { commandSearch = value ?? ""; selectedTab = 1; }
        public bool SelectTab(string value)
        {
            int i = Array.FindIndex(tabs, x => x.Equals(value, StringComparison.OrdinalIgnoreCase));
            if (i < 0) return false;
            selectedTab = i;
            return true;
        }

        // Always-visible recovery launcher: fixes the previous hidden/offscreen first-run experience.
        public void DrawLauncher()
        {
            if (!S.Bool("ui.showLauncher")) return;
            ApplyPalette();
            font = S.Int("ui.fontSize");
            GUI.depth = -950;
            var bounds = new Rect(18, Mathf.Max(10, Screen.height - 60), 163, 38);
            DrawRounded(bounds, Alpha(background, .96f), 10);
            DrawOutline(bounds, border);
            Text(new Rect(bounds.x + 12, bounds.y + 5, 139, 26), "ZUNU  /  OPEN  [F8]", accent, 12, true);
            if (GUI.Button(bounds, GUIContent.none, GUIStyle.none)) plugin.ToggleConsole();
        }

        private void ReadGeometry(float sw, float sh, bool reload)
        {
            float w = Mathf.Min(S.Number("ui.width"), Mathf.Max(280, sw - 16));
            float h = Mathf.Min(S.Number("ui.height"), Mathf.Max(200, sh - 16));
            string dock = S.Choice("ui.dock");
            float x = S.Number("ui.windowX"), y = S.Number("ui.windowY");
            if (dock == "center") { x = (sw - w) * .5f; y = (sh - h) * .5f; }
            else if (dock == "top-left") { x = 12; y = 12; }
            else if (dock == "top-right") { x = sw - w - 12; y = 12; }
            else if (dock == "bottom-left") { x = 12; y = sh - h - 12; }
            else if (dock == "bottom-right") { x = sw - w - 12; y = sh - h - 12; }
            if (reload) window = new Rect(x, y, w, collapsed ? 72 : h);
            else
            {
                window.width = w;
                if (!collapsed) window.height = h;
            }
            window.x = Mathf.Clamp(window.x, 0, Mathf.Max(0, sw - window.width));
            window.y = Mathf.Clamp(window.y, 0, Mathf.Max(0, sh - window.height));
            RememberGeometry();
            layoutScreenW = sw;
            layoutScreenH = sh;
            layoutReady = true;
        }

        private void RememberGeometry()
        {
            rememberedWidth = S.Number("ui.width");
            rememberedHeight = S.Number("ui.height");
            rememberedX = S.Number("ui.windowX");
            rememberedY = S.Number("ui.windowY");
            rememberedDock = S.Choice("ui.dock");
        }

        private bool GeometryChangedExternally()
        {
            return rememberedWidth != S.Number("ui.width") || rememberedHeight != S.Number("ui.height")
                || rememberedX != S.Number("ui.windowX") || rememberedY != S.Number("ui.windowY")
                || rememberedDock != S.Choice("ui.dock");
        }

        private void SaveWindowGeometry(bool sizeChanged)
        {
            string ignored;
            S.Set("ui.dock", "free", out ignored);
            S.Set("ui.windowX", Mathf.Clamp(Mathf.Round(window.x), 0, 3000).ToString(System.Globalization.CultureInfo.InvariantCulture), out ignored);
            S.Set("ui.windowY", Mathf.Clamp(Mathf.Round(window.y), 0, 2000).ToString(System.Globalization.CultureInfo.InvariantCulture), out ignored);
            if (sizeChanged)
            {
                // Retain a valid desktop size; compact mirrors still render with clipping-aware bounds.
                S.Set("ui.width", Mathf.Clamp(Mathf.Round(window.width), 620, 1400).ToString(System.Globalization.CultureInfo.InvariantCulture), out ignored);
                if (!collapsed) S.Set("ui.height", Mathf.Clamp(Mathf.Round(window.height), 390, 950).ToString(System.Globalization.CultureInfo.InvariantCulture), out ignored);
            }
            RememberGeometry();
        }

        private void CenterWindow()
        {
            string ignored;
            S.Set("ui.dock", "center", out ignored);
            layoutReady = false;
        }

        private void ToggleCollapsed()
        {
            collapsed = !collapsed;
            window.height = collapsed ? 72 : Mathf.Min(S.Number("ui.height"), Mathf.Max(200, layoutScreenH - 16));
            window.y = Mathf.Clamp(window.y, 0, Mathf.Max(0, layoutScreenH - window.height));
        }

        public void Draw()
        {
            // Reclaim generated textures periodically instead of retaining infinitely many palette variants.
            if (textures.Count > 320)
            {
                foreach (var texture in textures.Values) if (texture != null) UnityEngine.Object.Destroy(texture);
                textures.Clear();
                panels.Clear();
            }
            HandleShortcuts();
            Matrix4x4 before = GUI.matrix;
            int priorDepth = GUI.depth;
            try
            {
                scaleFactor = Mathf.Clamp(S.Number("ui.scale"), .75f, 1.5f);
                GUI.matrix = Matrix4x4.Scale(new Vector3(scaleFactor, scaleFactor, 1));
                float sw = Mathf.Max(320, Screen.width / scaleFactor);
                float sh = Mathf.Max(240, Screen.height / scaleFactor);
                if (!layoutReady || GeometryChangedExternally() || Mathf.Abs(layoutScreenW - sw) > 2 || Mathf.Abs(layoutScreenH - sh) > 2)
                    ReadGeometry(sw, sh, true);

                window.width = Mathf.Min(window.width, sw - 16);
                window.height = Mathf.Min(window.height, sh - 16);
                window.x = Mathf.Clamp(window.x, 0, Mathf.Max(0, sw - window.width));
                window.y = Mathf.Clamp(window.y, 0, Mathf.Max(0, sh - window.height));
                ApplyPalette();
                font = S.Int("ui.fontSize");
                GUI.depth = -900;
                if (S.Bool("ui.useShadows"))
                    DrawRounded(new Rect(window.x + S.Number("ui.shadowOffset"), window.y + S.Number("ui.shadowOffset"), window.width, window.height),
                        new Color(0, 0, 0, S.Number("ui.shadowAlpha")), S.Int("ui.rounding"));
                Rect previous = window;
                resizePending = false;
                resizeFinished = false;
                window = GUI.Window(901120, window, DrawWindow, GUIContent.none, GUIStyle.none);
                if (resizePending)
                {
                    window.width = Mathf.Clamp(pendingWidth, Mathf.Min(620, sw - 16), Mathf.Min(1400, sw - window.x - 8));
                    if (!collapsed) window.height = Mathf.Clamp(pendingHeight, Mathf.Min(390, sh - 16), Mathf.Min(950, sh - window.y - 8));
                }
                window.x = Mathf.Clamp(window.x, 0, Mathf.Max(0, sw - window.width));
                window.y = Mathf.Clamp(window.y, 0, Mathf.Max(0, sh - window.height));
                bool moved = Mathf.Abs(window.x - previous.x) > .25f || Mathf.Abs(window.y - previous.y) > .25f;
                if (moved || resizeFinished) SaveWindowGeometry(resizeFinished);
            }
            finally
            {
                GUI.matrix = before;
                GUI.depth = priorDepth;
            }
        }

        private void HandleShortcuts()
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown || !e.control) return;
            if (e.keyCode == KeyCode.K)
            {
                selectedTab = 1;
                searchFocusRequested = true;
                e.Use();
            }
            else if (e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha5)
            {
                selectedTab = (int)e.keyCode - (int)KeyCode.Alpha1;
                e.Use();
            }
        }

        private void ApplyPalette()
        {
            string theme = S.Choice("ui.theme");
            background = theme == "graphite" ? C(18, 20, 24) : theme == "midnight" ? C(7, 13, 25) : C(5, 6, 8);
            surface = theme == "graphite" ? C(31, 34, 39) : theme == "midnight" ? C(15, 24, 41) : C(14, 17, 21);
            surface2 = theme == "graphite" ? C(41, 45, 50) : theme == "midnight" ? C(23, 34, 54) : C(21, 25, 30);
            border = theme == "midnight" ? C(40, 57, 79) : C(43, 48, 55);
            text = C(242, 247, 249);
            muted = C(138, 150, 163);
            switch (S.Choice("ui.accent"))
            {
                case "white": accent = C(238, 243, 248); break;
                case "blue": accent = C(94, 149, 255); break;
                case "violet": accent = C(179, 136, 255); break;
                case "mint": accent = C(108, 228, 175); break;
                case "amber": accent = C(255, 197, 108); break;
                case "rose": accent = C(255, 134, 178); break;
                case "custom": accent = new Color(S.Number("ui.accentR"), S.Number("ui.accentG"), S.Number("ui.accentB")); break;
                default: accent = C(91, 220, 237); break;
            }
        }
        private static Color C(byte r, byte g, byte b) { return new Color32(r, g, b, 255); }
        private Color Alpha(Color col, float alpha) { col.a = alpha; return col; }
        private void DrawWindow(int id)
        {
            float w = window.width, h = window.height;
            float padding = S.Int("ui.padding"), headerH = S.Bool("ui.showHeader") ? S.Int("ui.headerHeight") : 0;
            float toolbarH = S.Bool("ui.showToolbar") ? 39 : 0;
            float footerH = S.Bool("ui.showStatusBar") ? S.Int("ui.statusHeight") : 0;
            float contentTop = padding + headerH + toolbarH;
            float contentBottom = h - padding - footerH;
            if (collapsed)
            {
                DrawRounded(new Rect(0, 0, w, h), Alpha(background, S.Number("ui.opacity")), S.Int("ui.rounding"));
                DrawOutline(new Rect(0, 0, w, h), border);
                DrawHeader(w, 60, padding);
                GUI.DragWindow(new Rect(8, 4, Mathf.Max(40, w - 145), 48));
                return;
            }
            float contentH = Mathf.Max(90, contentBottom - contentTop);
            DrawRounded(new Rect(0, 0, w, h), Alpha(background, S.Number("ui.opacity")), S.Int("ui.rounding"));
            DrawOutline(new Rect(0, 0, w, h), border);
            if (S.Bool("ui.showHeader")) DrawHeader(w, headerH, padding);
            if (S.Bool("ui.showToolbar")) DrawToolbar(padding, padding + headerH, w - 2 * padding, toolbarH);
            var content = new Rect(padding, contentTop, w - 2 * padding, contentH);
            if (selectedTab == 0) DrawConsole(content);
            else if (selectedTab == 1) DrawCommands(content);
            else if (selectedTab == 2) DrawSettings(content);
            else if (selectedTab == 3) DrawDiagnostics(content);
            else DrawCloud(content);
            if (S.Bool("ui.showStatusBar")) DrawFooter(new Rect(padding, h - padding - footerH, w - 2 * padding, footerH));
            DrawResizeGrip(w, h);
            if (S.Bool("ui.showHeader")) GUI.DragWindow(new Rect(9, 4, Mathf.Max(40, w - 145), Math.Min(48, headerH - 5)));
            else GUI.DragWindow(new Rect(10, 4, Mathf.Max(40, w - 145), 13));
        }
        private void DrawHeader(float w, float h, float p)
        {
            DrawRounded(new Rect(p, p, 39, 39), Alpha(accent, .16f), 11);
            Text(new Rect(p, p + 7, 39, 22), "Z", accent, 19, true, TextAnchor.MiddleCenter);
            Text(new Rect(p + 52, p + 2, Mathf.Min(270, w - 185), 22), "ZUNU  /  CONSOLE", text, 20, true);
            string sub = "LOCAL MOD TOOLKIT";
            if (S.Bool("ui.showVersion")) sub += "  ·  v1.2.0";
            Text(new Rect(p + 52, p + 25, Mathf.Max(55, w - 195), 17), sub, muted, 10, true);
            if (Button(new Rect(w - p - 104, p + 5, 30, 27), "[]", false)) CenterWindow();
            if (Button(new Rect(w - p - 69, p + 5, 30, 27), collapsed ? "+" : "_", false)) ToggleCollapsed();
            if (Button(new Rect(w - p - 34, p + 5, 30, 27), "X", false)) plugin.ToggleConsole();
            float pos = w - p - 117;
            if (w > 900 && S.Bool("ui.showClock"))
            {
                var clock = DateTime.Now.ToString(S.Choice("ui.timeFormat") == "h12" ? "hh:mm tt" : "HH:mm");
                pos -= 83; Text(new Rect(pos, p + 1, 80, 20), clock, muted, 12, false, TextAnchor.MiddleRight);
            }
            if (w > 700 && S.Bool("ui.showFps"))
            {
                pos -= 91;
                DrawRounded(new Rect(pos, p, 80, 25), Alpha(accent, .10f), 9);
                Text(new Rect(pos + 5, p + 3, 70, 18), plugin.Fps.ToString("F0") + " FPS", accent, 12, true, TextAnchor.MiddleCenter);
            }
            float lineY = p + Math.Min(45, h - 12);
            if (S.Bool("ui.animations"))
            {
                // Quantize animation alpha to avoid allocating a texture for each tiny change.
                float pulseAlpha = Mathf.Round((.30f + .22f * (.5f + Mathf.Sin(Time.realtimeSinceStartup * 1.6f) * .5f)) * 16f) / 16f;
                Color pulse = Alpha(accent, pulseAlpha);
                DrawRounded(new Rect(p, lineY, Math.Max(1, w - 2*p), 1), pulse, 0);
            }
            else DrawRounded(new Rect(p, lineY, w - 2*p, 1), border, 0);
        }
        private void DrawToolbar(float x, float y, float w, float h)
        {
            float buttonW = Mathf.Min(143, (w - 24) / tabs.Length - 5);
            for (int i = 0; i < tabs.Length; i++)
            {
                float bx = x + i * (buttonW + 5);
                var area = new Rect(bx, y, buttonW, h - 4);
                if (Button(area, tabs[i], selectedTab == i)) selectedTab = i;
            }
        }
        private void DrawConsole(Rect area)
        {
            float inputH = S.Int("ui.inputHeight");
            float hintH = S.Bool("ui.showHints") ? 23 : 0;
            float outputH = Mathf.Max(75, area.height - inputH - hintH - 13);
            float sidebarW = S.Bool("ui.showSidebar") ? Mathf.Min(S.Int("ui.sidebarWidth"), area.width * .34f) : 0;
            if (sidebarW > 0) DrawSidebar(new Rect(area.x, area.y, sidebarW - 8, outputH));
            var output = new Rect(area.x + sidebarW, area.y, area.width - sidebarW, outputH);
            DrawRounded(output, surface, S.Int("ui.rounding") / 2);
            DrawOutput(new Rect(output.x + 12, output.y + 12, output.width - 24, output.height - 24));
            float inputY = area.y + outputH + 10;
            DrawInput(new Rect(area.x, inputY, area.width, inputH));
            if (hintH > 0) Text(new Rect(area.x + 5, inputY + inputH + 2, area.width - 10, hintH - 2), "ENTER run   /   TAB autocomplete   /   F8 show or hide   /   drag title, resize corner", muted, 11);
        }
        private void DrawSidebar(Rect area)
        {
            DrawRounded(area, surface, 10);
            Text(new Rect(area.x + 13, area.y + 12, area.width - 20, 23), "WORKSPACE", muted, 11, true);
            var all = new List<string> { "All", "Favorites" };
            all.AddRange(plugin.Engine.Commands.Select(x => x.Category).Distinct().OrderBy(x => x));
            Rect viewport = new Rect(area.x + 7, area.y + 39, area.width - 14, area.height - 46);
            float lineH = 32;
            categoryScroll = GUI.BeginScrollView(viewport, categoryScroll, new Rect(0, 0, viewport.width - 13, all.Count * lineH), false, false);
            for (int i = 0; i < all.Count; i++)
            {
                if (Button(new Rect(0, i * lineH, viewport.width - 14, lineH - 5), all[i], all[i] == category, TextAnchor.MiddleLeft))
                { category = all[i]; selectedTab = 1; }
            }
            GUI.EndScrollView();
        }
        private void DrawOutput(Rect view)
        {
            var lines = plugin.Log.Visible().ToArray();
            float lineH = S.Int("ui.logFontSize") + (S.Choice("ui.density") == "compact" ? 6 : S.Choice("ui.density") == "spacious" ? 13 : 9);
            var heights = new float[lines.Length];
            float total = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                float count = S.Bool("console.wrapLog") ? Mathf.Clamp(Mathf.Ceil((lines[i].Message.Length + (S.Bool("ui.useTimestamps") ? 11 : 0)) / Math.Max(20, (view.width - 20) / (S.Int("ui.logFontSize") * .56f))), 1, 7) : 1;
                heights[i] = lineH * count;
                total += heights[i];
            }
            if (S.Bool("ui.autoscroll") && lastLineCount != lines.Length)
            {
                consoleScroll.y = Mathf.Max(0, total - view.height + 8);
                lastLineCount = lines.Length;
            }
            float scrollSpeed = S.Number("ui.scrollSpeed");
            if (Event.current.type == EventType.ScrollWheel && view.Contains(Event.current.mousePosition))
            { consoleScroll.y = Mathf.Clamp(consoleScroll.y + Event.current.delta.y * scrollSpeed, 0, Math.Max(0, total - view.height)); Event.current.Use(); }
            consoleScroll = GUI.BeginScrollView(view, consoleScroll, new Rect(0, 0, Math.Max(1, view.width - 18), Mathf.Max(view.height, total + 8)), false, false);
            float y = 3;
            foreach (var entry in lines.Select((value, index) => new { value, index }))
            {
                float rh = heights[entry.index];
                if (y + rh > consoleScroll.y - 20 && y < consoleScroll.y + view.height + 20)
                {
                    string label = (S.Bool("ui.useTimestamps") ? "[" + entry.value.Timestamp.ToString("HH:mm:ss") + "] " : "") + entry.value.Message;
                    Color color = text;
                    if (S.Bool("ui.logColors"))
                    {
                        if (entry.value.Severity == LogSeverity.Error) color = C(255, 119, 141);
                        else if (entry.value.Severity == LogSeverity.Warning) color = C(255, 201, 123);
                        else if (entry.value.Severity == LogSeverity.Success) color = accent;
                    }
                    Text(new Rect(5, y, Math.Max(30, view.width - 25), rh), label, color, S.Int("ui.logFontSize"), false, TextAnchor.UpperLeft, S.Bool("console.wrapLog"));
                }
                y += rh;
            }
            GUI.EndScrollView();
        }
        private void DrawInput(Rect area)
        {
            DrawRounded(area, surface, 10);
            if (S.Bool("ui.showInputBorder")) DrawOutline(area, border);
            Text(new Rect(area.x + 11, area.y + 5, 18, area.height - 10), ">", accent, 18, true, TextAnchor.MiddleCenter);
            float buttonWidth = 65;
            var fieldRect = new Rect(area.x + 31, area.y + 7, area.width - 45 - buttonWidth, area.height - 14);
            GUI.SetNextControlName("ZunuCommandInput");
            input = GUI.TextField(fieldRect, input, Style(font, text, false, TextAnchor.MiddleLeft));
            if (focusRequested) { GUI.FocusControl("ZunuCommandInput"); focusRequested = false; }
            if (Button(new Rect(area.xMax - buttonWidth - 8, area.y + 6, buttonWidth, area.height - 12), "RUN", true)) Submit();
            if (GUI.GetNameOfFocusedControl() == "ZunuCommandInput") HandleKeys();
            if (S.Bool("ui.showSuggestions") && !string.IsNullOrWhiteSpace(input) && input.IndexOf(' ') < 0 && plugin.Engine.Find(input) == null)
            {
                var suggestions = plugin.Engine.Complete(input.Trim()).Take(S.Int("ui.suggestionsCount")).ToArray();
                if (suggestions.Length > 0)
                {
                    float height = suggestions.Length * 24 + 9;
                    Rect drop = new Rect(area.x, area.y - height - 3, area.width, height);
                    DrawRounded(drop, surface2, 9);
                    for (int i = 0; i < suggestions.Length; i++)
                    {
                        if (Button(new Rect(drop.x + 8, drop.y + 5 + i*24, drop.width - 16, 22), suggestions[i], false, TextAnchor.MiddleLeft)) Prefill(suggestions[i] + " ");
                    }
                }
            }
        }
        private void HandleKeys()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown) return;
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { Submit(); e.Use(); }
            else if (e.keyCode == KeyCode.Tab && S.Bool("console.autoComplete"))
            {
                if (!string.IsNullOrWhiteSpace(input) && input.IndexOf(' ') < 0)
                {
                    var options = plugin.Engine.Complete(input).ToArray();
                    if (options.Length > 0)
                    {
                        string completion = options[0];
                        if (S.Choice("console.completionMode") == "longest")
                        {
                            int n = 0;
                            while (options.All(s => s.Length > n && char.ToLowerInvariant(s[n]) == char.ToLowerInvariant(options[0][n]))) n++;
                            completion = options[0].Substring(0,n);
                        }
                        input = completion + (options.Length == 1 ? " " : "");
                    }
                }
                e.Use();
            }
            else if (S.Bool("console.historyArrows") && (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.DownArrow))
            {
                int count = plugin.History.Entries.Count;
                if (count > 0)
                {
                    if (e.keyCode == KeyCode.UpArrow) historyPosition = Mathf.Min(count - 1, historyPosition + 1);
                    else historyPosition = Mathf.Max(-1, historyPosition - 1);
                    input = historyPosition < 0 ? "" : plugin.History.Entries[count - historyPosition - 1];
                }
                e.Use();
            }
            else if (e.keyCode == KeyCode.Escape) { input = ""; e.Use(); }
        }
        private void Submit()
        {
            string command = input.Trim();
            input = "";
            historyPosition = -1;
            plugin.Engine.Run(command);
            GUI.FocusControl("ZunuCommandInput");
        }
        private void DrawCommands(Rect area)
        {
            DrawRounded(area, surface, 10);
            float rowH = S.Int("ui.rowHeight") + (S.Choice("ui.density") == "spacious" ? 7 : S.Choice("ui.density") == "compact" ? -3 : 0);
            Text(new Rect(area.x + 15, area.y + 9, 250, 25), "COMMAND LIBRARY", text, font + 2, true);
            Text(new Rect(area.x + 15, area.y + 31, 220, 17), plugin.Engine.Count + " registered commands", muted, 11);
            DrawRounded(new Rect(area.x + area.width * .43f, area.y + 10, area.width * .57f - 15, 36), surface2, 8);
            GUI.SetNextControlName("ZunuCommandSearch");
            commandSearch = GUI.TextField(new Rect(area.x + area.width * .43f + 10, area.y + 18, area.width * .57f - 37, 20), commandSearch, Style(font - 1, text));
            if (searchFocusRequested) { GUI.FocusControl("ZunuCommandSearch"); searchFocusRequested = false; }
            var list = plugin.Engine.Search(commandSearch)
                .Where(x => category == "All" || category == "Favorites" && plugin.Tools.Favorites.Contains(x.Name) || x.Category == category).ToArray();
            Rect view = new Rect(area.x + 12, area.y + 57, area.width - 24, area.height - 68);
            commandScroll = GUI.BeginScrollView(view, commandScroll, new Rect(0, 0, view.width - 20, list.Length * rowH + 4));
            for (int i = 0; i < list.Length; i++)
            {
                var command = list[i];
                float y = i * rowH;
                if (y + rowH < commandScroll.y || y > commandScroll.y + view.height) continue;
                Rect rr = new Rect(1, y + 1, view.width - 24, rowH - 3);
                if (i%2==0) DrawRounded(rr, Alpha(surface2, .55f), 5);
                if (Button(new Rect(5, y + 2, Math.Max(75, rr.width - 40), rowH - 6), "", false)) Prefill(command.Name + " ");
                Text(new Rect(13, y + 3, Math.Max(50, rr.width * .43f), rowH - 5), command.Name, accent, font - 1, true, TextAnchor.MiddleLeft);
                string desc = S.Bool("ui.showCategoryNames") ? "[" + command.Category + "] " + command.Description : command.Description;
                Text(new Rect(15 + rr.width * .42f, y + 3, Math.Max(30, rr.width * .56f - 20), rowH - 5), desc, muted, font - 2, false, TextAnchor.MiddleLeft);
                if (Button(new Rect(rr.xMax - 28, y + 3, 27, rowH - 6), plugin.Tools.Favorites.Contains(command.Name) ? "★" : "☆", false))
                {
                    if (!plugin.Tools.Favorites.Add(command.Name)) plugin.Tools.Favorites.Remove(command.Name);
                    plugin.Tools.Save();
                }
            }
            GUI.EndScrollView();
        }
        private void DrawSettings(Rect area)
        {
            DrawRounded(area, surface, 10);
            Text(new Rect(area.x + 15, area.y + 9, area.width * .5f, 26), "APPEARANCE / PREFERENCES", text, font + 1, true);
            DrawRounded(new Rect(area.x + area.width * .58f, area.y + 10, area.width * .42f - 15, 32), surface2, 7);
            settingSearch = GUI.TextField(new Rect(area.x + area.width * .58f + 10, area.y + 16, area.width * .42f - 33, 22), settingSearch, Style(font - 1, text));
            var specs = S.All.Where(s => s.Key.IndexOf(settingSearch, StringComparison.OrdinalIgnoreCase) >= 0 || s.Description.IndexOf(settingSearch, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            float rowH = Math.Max(43, S.Int("ui.rowHeight") + 17);
            Rect view = new Rect(area.x + 10, area.y + 48, area.width - 20, area.height - 60);
            settingsScroll = GUI.BeginScrollView(view, settingsScroll, new Rect(0, 0, view.width - 20, specs.Length * rowH));
            for (int i = 0; i < specs.Length; i++)
            {
                var spec = specs[i]; float y = i * rowH;
                if (y + rowH < settingsScroll.y || y > settingsScroll.y + view.height) continue;
                if (i%2==0) DrawRounded(new Rect(0, y, view.width - 25, rowH-2), Alpha(surface2,.6f), 7);
                Text(new Rect(12, y + 3, view.width * .5f, 20), spec.Key, text, font - 1, true);
                Text(new Rect(12, y + 22, view.width * .53f, 17), spec.Description, muted, font - 3);
                string current = S.Get(spec.Key);
                float controlsX = view.width * .57f;
                Text(new Rect(controlsX, y + 8, view.width * .19f, 25), current, accent, font - 1, true, TextAnchor.MiddleRight);
                if (spec.Type == SettingType.Toggle)
                {
                    if (Button(new Rect(view.width - 112, y + 6, 48, 29), "SW", false)) Change(spec.Key, S.Bool(spec.Key) ? "false":"true");
                }
                else if (spec.Type == SettingType.Number)
                {
                    if (Button(new Rect(view.width - 120, y + 6, 30, 29), "−", false)) Adjust(spec, -1);
                    if (Button(new Rect(view.width - 85, y + 6, 30, 29), "+", false)) Adjust(spec, +1);
                }
                else if (Button(new Rect(view.width - 112, y + 6, 48, 29), "NEXT", false))
                { int index = Array.IndexOf(spec.Choices, current); Change(spec.Key, spec.Choices[(index + 1) % spec.Choices.Length]); }
                if (Button(new Rect(view.width - 52, y + 6, 42, 29), "RST", false)) Change(spec.Key, spec.Default);
            }
            GUI.EndScrollView();
        }
        private void DrawDiagnostics(Rect area)
        {
            DrawRounded(area, surface, 10);
            float x = area.x + 19, y = area.y + 15, w = area.width - 38;
            Text(new Rect(x, y, w, 29), "LIVE DIAGNOSTICS", text, font + 3, true);
            y += 39;
            float cardW = (w - 20) / 3;
            Metric(new Rect(x, y, cardW, 77), "FRAME RATE", plugin.Fps.ToString("F0") + " FPS");
            Metric(new Rect(x + cardW + 10, y, cardW, 77), "MANAGED MEMORY", (GC.GetTotalMemory(false)/1048576f).ToString("F0") + " MB");
            Metric(new Rect(x + (cardW + 10)*2, y, cardW, 77), "REGISTERED", plugin.Engine.Count.ToString());
            y += 97;
            if (Time.realtimeSinceStartup - lastSample >= .25f)
            {
                lastSample = Time.realtimeSinceStartup;
                fpsHistory.Enqueue(plugin.Fps);
                while (fpsHistory.Count > 95) fpsHistory.Dequeue();
            }
            float graphH = S.Choice("ui.statsMode") == "simple" ? 85 : 133;
            DrawRounded(new Rect(x, y, w, graphH), surface2, 10);
            Text(new Rect(x + 15, y + 10, w - 30, 23), "FRAME-TIME TREND  /  LIVE FPS", muted, font - 2, true);
            var points = fpsHistory.ToArray();
            if (points.Length > 0)
            {
                float gw = w - 34;
                float barW = Math.Max(2, gw / points.Length - 1);
                float max = Math.Max(90f, points.Max());
                for (int i = 0; i < points.Length; i++)
                {
                    float bh = Mathf.Clamp(points[i] / max * (graphH - 50), 0, graphH - 50);
                    DrawRounded(new Rect(x + 16 + i * gw / points.Length, y + graphH - 13 - bh, barW, bh), Alpha(accent, .65f), 0);
                }
            }
            y += graphH + 13;
            if (S.Bool("ui.showSystemInfo"))
            {
                string[] info = {
                    "GAME  " + Application.productName + "  ·  " + Application.version,
                    "PLATFORM  " + Application.platform + "  ·  " + Screen.width + "×" + Screen.height,
                    "GPU  " + SystemInfo.graphicsDeviceName,
                    "CPU  " + SystemInfo.processorType,
                    "MEMORY  " + SystemInfo.systemMemorySize + " MB installed",
                    "SESSION  " + (Time.realtimeSinceStartup - plugin.StartTime).ToString("F0") + "s active"
                };
                if (S.Bool("ui.showUnity")) info = new[] { "UNITY  " + Application.unityVersion }.Concat(info).ToArray();
                foreach (string line in info)
                {
                    if (y + 24 > area.yMax - 4) break;
                    Text(new Rect(x + 2, y, w - 3, 24), line, muted, font - 2);
                    y += 25;
                }
            }
        }
        private void Metric(Rect rect, string name, string value)
        {
            DrawRounded(rect, surface2, 9);
            Text(new Rect(rect.x+12, rect.y+12, rect.width-24, 19), name, muted, 10, true);
            Text(new Rect(rect.x+12, rect.y+35, rect.width-24, 32), value, accent, font+7, true);
        }
        private void DrawFooter(Rect rect)
        {
            DrawRounded(new Rect(rect.x, rect.y + 3, rect.width, 1), border, 0);
            string status = "● LOCAL" + (plugin.Cloud != null ? "   /   CLOUD " + plugin.Cloud.Status : "");
            if (S.Bool("ui.showCommandCount")) status += "   ·   " + plugin.Engine.Count + " COMMANDS";
            if (S.Bool("ui.showUptime")) status += "   ·   UP " + TimeSpan.FromSeconds(Time.realtimeSinceStartup - plugin.StartTime).ToString(@"mm\:ss");
            Text(new Rect(rect.x + 5, rect.y + 10, rect.width - 150, rect.height - 10), status, accent, 10, true);
            if (Button(new Rect(rect.xMax - 119, rect.y + 7, 90, Math.Max(20,rect.height - 9)), "HIDE  X", false)) plugin.ToggleConsole();
        }

        private void DrawCloud(Rect area)
        {
            DrawRounded(area, surface, 11);
            if (!initializedCloudInput)
            {
                cloudUrlInput = plugin.Cloud.Endpoint == "Not configured" ? "" : plugin.Cloud.Endpoint;
                initializedCloudInput = true;
            }
            float panelWidth = Mathf.Max(260, area.width - 20);
            cloudScroll = GUI.BeginScrollView(new Rect(area.x + 4, area.y + 4, area.width - 8, area.height - 8),
                cloudScroll, new Rect(0, 0, panelWidth, 425));
            float x = 17, y = 12, width = panelWidth - 34;
            Text(new Rect(x, y, width, 30), "NEON CLOUD  /  PROFILE SYNC", text, font + 4, true);
            y += 39;
            DrawRounded(new Rect(x, y, width, 58), surface2, 9);
            Text(new Rect(x + 14, y + 7, width - 28, 20), "STATUS  /  " + plugin.Cloud.Status,
                plugin.Cloud.Status == "SYNCED" ? accent : muted, 13, true);
            Text(new Rect(x + 14, y + 29, width - 28, 18), "Last successful sync: " + plugin.Cloud.LastSync, muted, 11);
            y += 73;
            Text(new Rect(x, y, width, 18), "HTTPS API URL", muted, 11, true);
            y += 21;
            DrawRounded(new Rect(x, y, width, 35), surface2, 7);
            cloudUrlInput = GUI.TextField(new Rect(x + 10, y + 6, width - 20, 24),
                cloudUrlInput ?? "", 280, Style(font - 1, text));
            y += 46;
            Text(new Rect(x, y, width, 18), "PRIVATE ACCESS TOKEN (HIDDEN)", muted, 11, true);
            y += 21;
            DrawRounded(new Rect(x, y, width, 35), surface2, 7);
            cloudTokenInput = GUI.PasswordField(new Rect(x + 10, y + 6, width - 20, 24),
                cloudTokenInput ?? "", '*', 256, Style(font - 1, text));
            y += 48;
            float half = Mathf.Max(110, (width - 12) / 2);
            if (Button(new Rect(x, y, half, 35), "SAVE & CONNECT", true) && !plugin.Cloud.Busy)
            {
                string reason;
                if (plugin.Cloud.Configure(cloudUrlInput, cloudTokenInput, out reason))
                {
                    cloudTokenInput = "";
                    plugin.Cloud.Pull();
                }
                else plugin.Log.Write(reason, LogSeverity.Warning);
            }
            if (Button(new Rect(x + half + 12, y, half, 35), "DISCONNECT", false) && !plugin.Cloud.Busy)
            {
                cloudTokenInput = "";
                plugin.Cloud.Disconnect();
            }
            y += 47;
            if (Button(new Rect(x, y, half, 35), "PULL CLOUD", false) && !plugin.Cloud.Busy) plugin.Cloud.Pull();
            if (Button(new Rect(x + half + 12, y, half, 35), "PUSH CLOUD", false) && !plugin.Cloud.Busy) plugin.Cloud.Push();
            y += 48;
            Text(new Rect(x, y, width, 47),
                "Optional: syncs themes and favorite commands only. Needs your own deployed API and per-user token. Setup: docs/NEON.md.",
                muted, 11, false, TextAnchor.UpperLeft, true);
            y += 49;
            Text(new Rect(x, y, width, 24), "DO NOT ENTER NEON POSTGRES PASSWORDS HERE", accent, 11, true);
            GUI.EndScrollView();
        }

        private void DrawResizeGrip(float w, float h)
        {
            if (!S.Bool("ui.resizeEnabled") || collapsed) return;
            var grip = new Rect(w - 31, h - 31, 26, 26);
            DrawRounded(grip, Alpha(accent, .13f), 7);
            Text(new Rect(grip.x + 1, grip.y - 1, 24, 26), "\\", accent, 19, true, TextAnchor.MiddleCenter);
            Event e = Event.current;
            int control = GUIUtility.GetControlID(ResizeControlHash, FocusType.Passive, grip);
            switch (e.GetTypeForControl(control))
            {
                case EventType.MouseDown:
                    if (e.button != 0 || !grip.Contains(e.mousePosition)) break;
                    GUIUtility.hotControl = control;
                    resizeActive = true;
                    resizeStartMouse = new Vector2(window.x + e.mousePosition.x, window.y + e.mousePosition.y);
                    resizeStartSize = new Vector2(window.width, window.height);
                    e.Use();
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != control || !resizeActive) break;
                    Vector2 global = new Vector2(window.x + e.mousePosition.x, window.y + e.mousePosition.y);
                    pendingWidth = resizeStartSize.x + global.x - resizeStartMouse.x;
                    pendingHeight = resizeStartSize.y + global.y - resizeStartMouse.y;
                    resizePending = true;
                    e.Use();
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl != control || !resizeActive) break;
                    GUIUtility.hotControl = 0;
                    resizeActive = false;
                    resizeFinished = true;
                    e.Use();
                    break;
            }
        }

        private void Change(string key, string value) { string error; plugin.Settings.Set(key, value, out error); if (error != null) plugin.Log.Write(error, LogSeverity.Warning); }
        private void Adjust(SettingSpec spec, float dir)
        {
            float v = Mathf.Clamp(S.Number(spec.Key) + dir * spec.Step, spec.Minimum, spec.Maximum);
            Change(spec.Key, v.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        private bool Button(Rect area, string label, bool active, TextAnchor align = TextAnchor.MiddleCenter)
        {
            bool hover = area.Contains(Event.current.mousePosition);
            Color bg = active ? Alpha(accent, .19f) : hover ? Alpha(accent, S.Number("ui.hoverAlpha")) : Alpha(surface2, .85f);
            DrawRounded(area, bg, 7);
            var style = Style(Math.Max(10, font - 2), active ? accent : hover ? text : muted, true, align);
            if (align == TextAnchor.MiddleLeft) style.padding = new RectOffset(10, 3, 1, 1);
            return GUI.Button(area, label, style);
        }
        private GUIStyle Style(int sz, Color tint, bool bold = false, TextAnchor align = TextAnchor.MiddleLeft, bool wrap = false)
        {
            var s = new GUIStyle(GUIStyle.none);
            s.fontSize = Math.Max(9, sz); s.normal.textColor = tint; s.hover.textColor = tint; s.focused.textColor = tint;
            s.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            s.alignment = align;
            s.wordWrap = wrap;
            s.clipping = wrap ? TextClipping.Overflow : TextClipping.Clip;
            return s;
        }
        private void Text(Rect bounds, string message, Color tint, int sz, bool bold = false, TextAnchor align = TextAnchor.MiddleLeft, bool wrap = false)
        { GUI.Label(bounds, message ?? "", Style(sz, tint, bold, align, wrap)); }
        private void DrawOutline(Rect area, Color tint)
        {
            int thickness = S.Int("ui.borderThickness");
            DrawRounded(new Rect(area.x, area.y, area.width, thickness), tint, 0);
            DrawRounded(new Rect(area.x, area.yMax-thickness, area.width, thickness), tint, 0);
            DrawRounded(new Rect(area.x, area.y, thickness, area.height), tint, 0);
            DrawRounded(new Rect(area.xMax-thickness, area.y, thickness, area.height), tint, 0);
        }
        private void DrawRounded(Rect area, Color tint, int radius)
        {
            if (area.width <= 0 || area.height <= 0) return;
            int r = Mathf.Clamp(radius, 0, 21);
            Color32 color = tint;
            // Bucket alpha to 1/32 steps: smooth enough visually, bounded cache churn.
            color.a = (byte)Mathf.Min(255f, Mathf.Round(color.a / 8f) * 8f);
            string key = r + ":" + color.r + ":" + color.g + ":" + color.b + ":" + color.a;
            GUIStyle style;
            if (!panels.TryGetValue(key, out style))
            {
                Texture2D tex = MakeTexture(color, r);
                textures[key] = tex;
                style = new GUIStyle(GUIStyle.none);
                style.normal.background = tex;
                style.border = new RectOffset(r+1, r+1, r+1, r+1);
                panels[key] = style;
            }
            GUI.Box(area, GUIContent.none, style);
        }
        private static Texture2D MakeTexture(Color32 color, int r)
        {
            var tex = new Texture2D(64,64,TextureFormat.RGBA32,false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[64*64];
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float px = Math.Min(x, 63-x), py = Math.Min(y, 63-y);
                float dx = Math.Max(0, r - px - .5f), dy = Math.Max(0, r - py - .5f);
                float distance = Mathf.Sqrt(dx*dx + dy*dy);
                float alpha = r == 0 ? 1 : Mathf.Clamp01(r+.5f-distance);
                pixels[y*64+x] = new Color32(color.r,color.g,color.b,(byte)(color.a*alpha));
            }
            tex.SetPixels32(pixels); tex.Apply(false,true);
            return tex;
        }
        public void Dispose()
        {
            foreach(var texture in textures.Values) if(texture!=null) UnityEngine.Object.Destroy(texture);
            textures.Clear(); panels.Clear();
        }
    }
}
