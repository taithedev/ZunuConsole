using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using ZunuConsole.Commands;
using ZunuConsole.Core;
using ZunuConsole.UI;
using ZunuConsole.Cloud;

namespace ZunuConsole
{
    [BepInPlugin("com.zuzify.zunuconsole", "ZunuConsole", "1.2.0")]
    public sealed class ZunuPlugin : BaseUnityPlugin
    {
        internal SettingStore Settings { get; private set; }
        internal ConsoleLog Log { get; private set; }
        internal ConsoleHistory History { get; private set; }
        internal CommandEngine Engine { get; private set; }
        internal LocalWorkbench Tools { get; private set; }
        internal ConsoleOverlay Overlay { get; private set; }
        internal ZunuCloudClient Cloud { get; private set; }
        private bool capturedCursor;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        internal float StartTime { get; private set; }
        internal float Fps { get; private set; }
        private bool warnedInput;
        internal void ReportException(Exception exception) { Logger.LogError(exception); }

        private void Awake()
        {
            StartTime = Time.realtimeSinceStartup;
            string folder = Path.Combine(Paths.ConfigPath, "ZunuConsole");
            Settings = new SettingStore(Path.Combine(Paths.ConfigPath, "ZunuConsole.settings"));
            Log = new ConsoleLog(Settings);
            History = new ConsoleHistory(Settings, Path.Combine(folder, "history.dat"));
            Tools = new LocalWorkbench(Path.Combine(folder, "workbench.dat"));
            Engine = new CommandEngine(this);
            PreferenceCommands.Register(this);
            GeneralCommands.Register(this);
            WorkbenchCommands.Register(this);
            GorillaTagCommands.Register(this);
            Cloud = new ZunuCloudClient(this);
            Overlay = new ConsoleOverlay(this);
            Application.logMessageReceived += OnUnityLog;
            UpdateCursor(Settings.Bool("ui.visible"));
            Log.Write("ZUNUCONSOLE / PC BUILD 1.2.0", LogSeverity.Success);
            Log.Write("Loaded " + Engine.Count + " commands. F8 toggles the OLED console.");
            Log.Write("Use help, commands, search <term>, or the Commands browser.");
            Logger.LogInfo("ZunuConsole loaded (" + Engine.Count + " commands)");
        }
        private void OnDestroy()
        {
            Application.logMessageReceived -= OnUnityLog;
            if (Overlay != null) Overlay.Dispose();
            if (Tools != null) Tools.Save();
            UpdateCursor(false);
        }
        internal void ToggleConsole()
        {
            string error;
            bool isShowing = !Settings.Bool("ui.visible");
            Settings.Set("ui.visible", isShowing ? "true" : "false", out error);
            UpdateCursor(isShowing);
            if (isShowing && Overlay != null) Overlay.FocusInput();
        }
        private void UpdateCursor(bool open)
        {
            if (Settings == null) return;
            if (open && Settings.Bool("ui.unlockCursor"))
            {
                if (!capturedCursor)
                {
                    previousCursorLock = Cursor.lockState;
                    previousCursorVisible = Cursor.visible;
                    capturedCursor = true;
                }
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (capturedCursor)
            {
                Cursor.lockState = previousCursorLock;
                Cursor.visible = previousCursorVisible;
                capturedCursor = false;
            }
        }
        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (Settings != null) UpdateCursor(Settings.Bool("ui.visible"));
            if (Cloud != null) Cloud.Tick();
            if (dt > .0001f) Fps = Fps < 1 ? 1f / dt : Fps * .9f + (1f / dt) * .1f;
            try { if (Input.GetKeyDown(KeyCode.F8)) ToggleConsole(); }
            catch (InvalidOperationException)
            {
                if (!warnedInput) { warnedInput = true; Log.Write("Legacy input unavailable; use F8 in the IMGUI window when input is supported.", LogSeverity.Warning); }
            }
        }
        private void OnGUI()
        {
            if (Event.current != null && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F8)
            {
                // Update() handles F8 when legacy keyboard input exists. Avoid a second toggle.
                if (warnedInput) { ToggleConsole(); Event.current.Use(); }
            }
            if (Settings != null && Overlay != null)
            {
                if (Settings.Bool("ui.visible")) Overlay.Draw();
                else Overlay.DrawLauncher();
            }
        }
        private void OnUnityLog(string message, string stackTrace, LogType kind)
        {
            if (Settings == null || !Settings.Bool("console.captureUnityLogs")) return;
            if (!string.IsNullOrEmpty(message) && message.StartsWith("ZunuConsole ", StringComparison.OrdinalIgnoreCase)) return;
            LogSeverity severity = kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert ? LogSeverity.Error : kind == LogType.Warning ? LogSeverity.Warning : LogSeverity.Info;
            Log.Write("[Unity] " + message, severity, stackTrace);
        }
    }
}
