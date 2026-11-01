# Milestone 0.2 — Pen foundation

## Use

Run `dotnet run --project src/PennCraft.Desktop` on Windows 11 with .NET 10. One dark, rounded, translucent **Programs** Pen starts at 600 × 300 DIPs.

- Drag the compact header. Resize any edge or corner; minimum 240 × 120 DIPs.
- Double-click the header, or hover and choose **☰ → Rename Pen**. Enter commits; Escape cancels; leaving the editor commits. Titles are trimmed and limited to 80 characters; an empty title becomes Programs.
- Hover the header to reveal ☰ and ×. The menu offers background opacity presets (35%, 55%, 78%, 100%). Text and controls stay opaque.
- × and Alt+F4 hide the Pen. The notification-area **PennCraft** icon offers Show Pen, Hide Pen, Reset position and show, and Exit PennCraft. Double-click the tray icon to show. Windows may place the icon in its overflow menu.
- Settings live in `%LOCALAPPDATA%\PennCraft\pen.json`: title, DIP coordinates, dimensions, background opacity and user visibility. Geometry saves after a short debounce; explicit visibility changes and exit save immediately. A malformed file falls back to defaults; failed reads/writes are logged. Reset position recovers an off-screen Pen.
- Exit through the tray to stop the process, preserving visible/hidden intent for the next launch.

The body is intentionally empty. No icons, file operations, folder portals or startup registration are included. Desktop input outside the Pen remains Explorer's responsibility; the Pen body consumes clicks and rejects file drops.

## Desktop integration and limits

An independent non-topmost tool window is reasserted at HWND_BOTTOM every 250 ms without activation. Unlike 0.1, application focus no longer hides it. Native visibility/minimization caused by Win+D is repaired without overwriting user intent. Missing Explorer hides the Pen until shell rediscovery. Explorer handle/process changes and TaskbarCreated are observed. No Explorer mutation, injection, registry changes, undocumented shell messages or global input hooks.

This is a best-effort independent window, not a supported Windows desktop embedding contract. Z-order relative to wallpaper/icons and Win+D differs between Windows builds and wallpaper utilities. A 250 ms recovery delay is expected. Native API success and CI cannot establish correct compositing on a user's desktop. Explicit title editing temporarily enables activation for keyboard input; ordinary header/body interaction does not activate the Pen.

Mixed DPI, monitor removal, virtual desktops, accessibility/high contrast, and multi-instance coordination remain limited. Coordinates are persisted in WPF DIPs; automatic monitor recovery is deferred, with a tray reset provided. Run one process per user. The translucent surface does not provide blur. Background color is currently fixed dark; opacity is configurable.

Diagnostics remain in `%LOCALAPPDATA%\PennCraft\desktop.log`, with bounded rotation. Persistence errors and shell transitions are recorded; no desktop filenames are collected.

Native references: [SetWindowPos](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowpos), [ShowWindow](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-showwindow).

## Verification

Windows CI restores, builds Release with warnings as errors, and runs xUnit tests for all resize directions, minimum-size anchoring, normalization, settings round-trip, malformed settings, failed writes, visibility policy, and real HWND styles/focus. On runners with a shell, the native test also simulates an externally hidden window and verifies restoration and persistent explicit hiding.

Interactive acceptance remains pending. Record Windows build and monitor/DPI configuration; CI alone is insufficient:

| Action | Expected |
| --- | --- |
| Launch with apps open; click desktop; repeat Win+D | Pen returns on desktop without focus theft |
| Focus, restore and maximize apps | Pen remains behind them, visible in uncovered desktop areas |
| Drag header; resize all eight directions, including to minimum | Smooth capture, fixed opposite edge, no desktop icon movement |
| Hover header/body; double-click title; Enter/Escape/click away | Controls only on header hover; centered title; commit/cancel work |
| Change opacity; hide via ×; restart; show via tray | Saved title/geometry/opacity/visibility restored; hidden Pen stays hidden |
| Exit with Pen visible; restart | Pen returns visible |
| Desktop click/double-click/context menu/selection/file drag outside Pen | Normal Explorer behavior, no file changes |
| Restart Explorer after saving other work | Process survives; Pen returns after rediscovery; tray remains available |
| Move between DPI settings; unplug monitor; use tray reset | Record DPI limits; reset makes title accessible |
| Lock/unlock; switch virtual desktops; toggle desktop icons | Record any unsupported compositing or visibility behavior |

The screenshots supplied in the design discussion informed the dark enclosure, small rounded corners, centered title and header-only controls. Icon layout shown there is outside this milestone.
