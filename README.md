# ZunuConsole

**Source version:** 1.1.0 (not a compiled or game-tested release)

An OLED-themed desktop command console for the **PC Mono / BepInEx 5** version of Gorilla Tag, built in C# with an extensible command registry.

**Output DLL:** `ZunuConsole.dll`  
**Open/close:** `F8` (legacy Unity keyboard input)  
**UI:** Unity IMGUI on the PC game window / desktop mirror (not a standalone Windows terminal, injected VR wrist menu, or Quest APK).

## Highlights

- Dark OLED/graphite/midnight palettes, 8 accents and custom RGB, draggable/dockable window, with bounded GPU texture caching.
- Console, Commands, Settings, and Diagnostics tabs.
- Live Unity log capture, severity filtering, copying and exporting logs.
- Search across commands and descriptions, command autocomplete, keyboard history and favorites. F8 opens/closes, Ctrl+K focuses command search, Ctrl+1–4 switches tabs.
- Persistent local settings, history, favorites and notes.
- Useful PC and Gorilla Tag local read-only scene diagnostics, local graphics and audio controls, timers, stopwatch, clipboard utilities and basic math.
- **397 individually registered commands**, with functional actions/query handlers. Generated settings commands use true value management; see [`docs/COMMANDS.md`](docs/COMMANDS.md).
- No Gorilla Tag internals, no anti-cheat bypasses, no public-lobby gameplay cheats.

## Build prerequisites

1. Visual Studio 2022 with **.NET desktop development** workload and the .NET SDK supporting **netstandard2.1**. Start from a C# Class Library, rather than the legacy .NET Framework-only template.
2. Your legitimately installed **PC Gorilla Tag** game and **BepInEx 5** (`Gorilla Tag/BepInEx/core/BepInEx.dll`).
3. Local game Unity DLLs in `Gorilla Tag/Gorilla Tag_Data/Managed/`. This repository **does not ship game DLLs**.

Open `ZunuConsole.sln`. Choose `Release / Any CPU`. If installed in a nonstandard folder, build with:

```powershell
msbuild ZunuConsole.csproj /p:Configuration=Release /p:GorillaTagDir="D:\SteamLibrary\steamapps\common\Gorilla Tag"
```

The DLL appears in `bin/Release/ZunuConsole.dll`.

**Install:** copy `ZunuConsole.dll` to `Gorilla Tag/BepInEx/plugins/`, then start the game and press F8 on your keyboard while the PC game window has focus. For allowed mod testing, use an approved modded environment, not public matchmaking.

## Commands (examples)

```
help
commands
search ui
ui.theme.set oled
ui.accent.set violet
ui.width.set 1000
ui.showFps.toggle
ui.dock.set top-right
app.fps
graphics.quality
logs.export
note.set greeting "Test lobby session"
timer.start practice 120
stopwatch.start
stopwatch.lap
favorite.add app.fps
```

Open the **Commands** tab (Ctrl+K) to browse every registered command. Type a command and press Enter, or press Tab for autocomplete. `settings.list` lists current settings.

## Layout

```
ZunuConsole.sln
ZunuConsole.csproj
src/
  ZunuPlugin.cs
  Core/
    CommandEngine.cs
    SettingStore.cs
    ConsoleHistory.cs
    ConsoleLog.cs
    Workbench.cs
  Commands/
    PreferenceCommands.cs
    GeneralCommands.cs
    WorkbenchCommands.cs
    GorillaTagCommands.cs
  UI/
    ConsoleOverlay.cs
docs/
  COMMANDS.md
  GITHUB.md
scripts/
  Install-Local.ps1
```

## Compatibility and limitations

- Targets BepInEx 5 on the **Mono-based PC game installation**. Game updates may affect compatibility, module names, or modding rules.
- F8 uses Unity's legacy input API; when the game runs with legacy input disabled, you may need to adapt the input hook for your particular version.
- Unity quality/target FPS changes are local; the VR runtime may override display frame timing.
- The overlay renders through Unity IMGUI on the PC window; **it is not a headset world-space console**.
- `ui.visible` is stored between launches. Use `F8` to toggle. Some settings affect interface usability; reset values through the settings file if needed.
- This is source code, not a verified compiled release: build it using your installed game references.

## Responsible use

Gorilla Tag community guides distinguish approved modded testing from prohibited public-lobby gameplay modifications. This project does not modify game rules or other players, and should be used only where mods are permitted.

## Quality and verification

Run `python scripts/validate_commands.py` and `python -m unittest discover -s tests -v` before committing. GitHub Actions runs both checks on every push and pull request. These tests validate source invariants and command registration counts; **they do not compile against Gorilla Tag**. No game assemblies, license files, player data or authentication tokens should be committed.
