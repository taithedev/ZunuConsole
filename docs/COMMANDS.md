# ZunuConsole command reference

Total **409** registered commands: **293** automatically registered setting operations plus **116** hand-written core/diagnostic/tool/Gorilla Tag commands.

Every setting has a functional read/modify/reset action, not an empty placeholder. Commands that adjust settings are variants of shared underlying settings, not 1:1 gameplay mods.

## Built-in commands

### Core and diagnostics

- `help`
- `commands`
- `search`
- `echo`
- `clear`
- `about`
- `version`
- `count`
- `history`
- `history.clear`
- `history.save`
- `history.load`
- `history.search`
- `settings.list`
- `settings.search`
- `settings.resetall`
- `settings.save`
- `settings.reload`
- `settings.path`
- `ui.show`
- `ui.hide`
- `ui.toggle`
- `ui.center`
- `ui.resetposition`
- `ui.tab`
- `ui.focus`
- `ui.search`
- `ui.screenshot`
- `ui.command`
- `unity.version`
- `app.version`
- `app.name`
- `app.company`
- `app.platform`
- `app.data`
- `app.fps`
- `app.targetfps`
- `app.targetfps.set`
- `app.vsync`
- `app.vsync.set`
- `graphics.quality`
- `graphics.list`
- `graphics.quality.set`
- `graphics.resolution`
- `graphics.device`
- `graphics.vendor`
- `graphics.memory`
- `graphics.api`
- `system.os`
- `system.cpu`
- `system.cores`
- `system.memory`
- `system.gc`
- `system.uptime`
- `session.uptime`
- `logs.count`
- `logs.export`
- `logs.copy`
- `logs.test`
- `audio.volume`
- `audio.volume.set`
- `audio.mute`
- `audio.unmute`
- `audio.status`

### Gorilla Tag inspection

- `gtag.scene`
- `gtag.scene.loaded`
- `gtag.scene.roots`
- `gtag.scene.find`
- `gtag.camera`
- `gtag.camera.position`
- `gtag.camera.rotation`
- `gtag.camera.forward`
- `gtag.camera.fov`
- `gtag.camera.clip`
- `gtag.camera.viewport`
- `gtag.camera.depth`
- `gtag.physics.gravity`
- `gtag.physics.fixed`
- `gtag.performance.delta`
- `gtag.performance.smooth`
- `gtag.performance.timescale`
- `gtag.performance.frame`
- `gtag.performance.realtime`
- `gtag.world.objects`
- `gtag.world.colliders`
- `gtag.world.lights`
- `gtag.world.audiosources`
- `gtag.tags.find`

### Local tools

- `note.set`
- `note.get`
- `note.list`
- `note.remove`
- `note.clear`
- `favorite.add`
- `favorite.remove`
- `favorite.list`
- `favorite.clear`
- `timer.start`
- `timer.remaining`
- `timer.list`
- `timer.remove`
- `timer.clear`
- `stopwatch.start`
- `stopwatch.stop`
- `stopwatch.read`
- `stopwatch.lap`
- `stopwatch.reset`
- `clipboard.copy`
- `clipboard.read`
- `timestamp.now`
- `timestamp.utc`
- `random.number`
- `math.add`
- `math.subtract`
- `math.multiply`
- `math.divide`

## Setting command families

Numeric settings support `KEY`, `KEY.set <value>`, `KEY.reset`, `KEY.inc`, `KEY.dec`.

Toggle settings support `KEY`, `KEY.on`, `KEY.off`, `KEY.toggle`.

Choice settings support `KEY`, `KEY.set <option>`, `KEY.reset`.

### Numeric settings

- `ui.scale`
- `ui.opacity`
- `ui.width`
- `ui.height`
- `ui.rounding`
- `ui.padding`
- `ui.fontSize`
- `ui.logFontSize`
- `ui.rowHeight`
- `ui.sidebarWidth`
- `ui.maxLines`
- `ui.scrollSpeed`
- `ui.accentR`
- `ui.accentG`
- `ui.accentB`
- `ui.windowX`
- `ui.windowY`
- `ui.headerHeight`
- `ui.inputHeight`
- `ui.suggestionsCount`
- `ui.historyLimit`
- `ui.shadowOffset`
- `ui.shadowAlpha`
- `ui.spacing`
- `ui.borderThickness`
- `ui.hoverAlpha`
- `ui.statusHeight`

### Boolean settings

- `ui.visible`
- `ui.showFps`
- `ui.showClock`
- `ui.showVersion`
- `ui.showUptime`
- `ui.showUnity`
- `ui.showHints`
- `ui.showSuggestions`
- `ui.showSidebar`
- `ui.showStatusBar`
- `ui.showHeader`
- `ui.showCommandCount`
- `ui.useTimestamps`
- `ui.logColors`
- `ui.autoscroll`
- `ui.useShadows`
- `ui.animations`
- `ui.showSystemInfo`
- `ui.showInputBorder`
- `console.persistHistory`
- `console.captureUnityLogs`
- `console.includeStackTraces`
- `console.confirmClear`
- `console.echoCommands`
- `console.autoComplete`
- `console.historyArrows`
- `console.wrapLog`
- `ui.showCategoryNames`
- `ui.showToolbar`

### Choice settings

- `ui.theme`
- `ui.accent`
- `ui.dock`
- `ui.timeFormat`
- `ui.logLevel`
- `ui.density`
- `console.searchMode`
- `console.completionMode`
- `console.historyOrder`
- `ui.statsMode`

## Safety and scope

Gorilla Tag commands here are read-only Unity scene/device diagnostics. There are no remote-player, lobby-abuse, anti-cheat-evasion, or competitive gameplay modifications. The visual console is PC-side IMGUI, not a VR wrist UI.

The fifth Cloud tab is a visual interface. It is not an additional command category. Cloud sync is opt-in and does not upload command history, notes, game data, room codes, logs or credentials.
