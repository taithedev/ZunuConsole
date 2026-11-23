# Changelog

## 1.2.0 — movable UI + optional Neon sync

- Drag, resize, minimize, close, center and persistent bounds with dynamic screen clamping.
- Added always-available bottom-left launcher for recovery from hidden windows.
- Default visible for new installations and optional cursor release while the UI is open.
- Added fifth Cloud tab and optional Neon-backed sync for a small approved preference whitelist and favorites.
- Added serverless HTTPS API, token-hash authentication, deployment instructions and server-side SQL schema.
- No raw PostgreSQL credentials are shipped to clients. Cloud must be deployed separately.


## 1.1.1 — ScreenCapture compilation fix

- Added the missing `UnityEngine.ScreenCaptureModule.dll` reference used by `ui.screenshot`.
- Added a build prerequisite check with a descriptive missing-DLL error.
- Qualified the screenshot API as `UnityEngine.ScreenCapture` and synchronized displayed version numbers.
- Added a regression test for the project screenshot module reference.

## 1.1.0 — source update

- Fixed unbounded texture/style cache growth in the IMGUI console.
- Quantized animated accent transparency to reduce transient UI allocations.
- Added Ctrl+K quick command search and Ctrl+1–4 tab switching.
- Automatically focus command input when showing the console via F8.
- Added automated source-invariant tests to GitHub Actions.
- Documented build, test and compatibility limitations more explicitly.

## 1.0.0 — initial source release

- 397 registered commands (116 direct actions + 281 settings operations).
- Four-tab OLED console, persistent preferences, console log, local workbench.
- Local Unity and PC game diagnostics.
