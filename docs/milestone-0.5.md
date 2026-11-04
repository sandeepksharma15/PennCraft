# Milestone 0.5 — desktop creation and multiple Pens

## Use

Hold the left mouse button on empty desktop space and draw a rectangle. Release and leave the pointer still briefly while PennCraft checks the desktop. A translucent preview and menu appear:

- **Create a Pen here** saves a new Pen at that location, with the editable title New Pen.
- **Create a Folder Portal here** is visible but disabled, with a Coming later tooltip.

Click elsewhere or press Escape to dismiss the preview. No Pen, membership or item storage is created by previewing/cancelling alone. Rectangles must be at least the Pen minimum of 240 × 120 DIPs at the target monitor's scale. Reverse-direction drawing and negative monitor coordinates are accepted.

Creation is suppressed for modifiers, injected input, drags starting over icons, selected icons, rectangles touching icon bounds, application windows or existing Pens, changed icon layouts, and unavailable/stale shell geometry. Ordinary Explorer selection still receives the original mouse input. There is no custom rectangle during dragging: Explorer draws its selection rectangle, followed by PennCraft's preview only after release.

## Multiple Pens

The notification-area icon lists all Pens by their current titles, with Show, Hide and Reset position commands. It also offers New Pen, Show all Pens, Hide all Pens, Desktop rectangle creation (toggle), and Exit. Double-clicking the icon shows all Pens. Close still hides a Pen.

Each new Pen has an immutable GUID identity and independent settings, visibility, item membership, storage and move journal. Its display title can change without relocating files. Moving directly between Pens is deferred; move an item back to the desktop, then into another Pen.

The existing Pen remains Default. Its pen.json, .PennCraft/Default/Items and recovery journal are preserved in place. Workspace membership lives in workspace.json beside pen.json; new settings live in Pens/{id}.json and physical storage in desktop/.PennCraft/{id}/Items. Startup migration writes only the workspace index. A malformed/unsupported workspace index stops startup without replacing it.

Pens remain saved when hidden. There is no delete command in this milestone. Use the tray to restore them. New Pen in the tray provides a creation path when desktop gesture detection is unavailable.

## Windows integration and limits

A dedicated input-observer thread installs WH_MOUSE_LL and always returns CallNextHookEx. It does not capture, suppress, synthesize or forward modified input, and does not inject code into Explorer. A separate MTA worker reads desktop icon bounds and selection through UI Automation, verifies the native icon count and checks visible top-level window bounds. Slow/unavailable providers suppress creation while normal desktop input continues. No filenames, pointer history or keystrokes are logged.

Desktop discovery currently expects Explorer's Progman/WorkerW → SHELLDLL_DefView → SysListView32 layout and an automation provider exposing all visible icons. Other shell layouts, wallpaper utilities, hidden-icon states, remote sessions or provider failures may suppress the gesture. The tray creation command remains available. Windows can silently remove a slow input observer; restarting PennCraft reinstalls it. The tray toggle disables offers and automation polling until re-enabled; exit uninstalls the observer.

Coordinates are validated in physical pixels; the app configures per-monitor V2 awareness before WPF starts and places the preview/new window using native physical bounds. Mixed DPI, crossing monitors, virtual desktops, Explorer restart and display changes still require interactive acceptance. The popup may take keyboard focus after the explicit creation gesture. Temporary popups and unknown overlapping windows conservatively suppress creation.

Existing item move/recovery limits remain documented in [Milestone 0.4](milestone-0.4.md). Close older PennCraft versions before upgrading. Do not run versions that ignore the application lock concurrently.

## Validation

Windows CI builds and runs existing move/recovery tests plus rectangle direction/minimum/DPI rules, icon/app/Pen exclusion, snapshot stability, preview portal state, workspace migration, invalid IDs/indexes, independent settings and separate storage/journals. Automated tests do not send desktop input or create Pens on the user's real desktop.

The user manually accepted 0.4. Fresh 0.5 interactive acceptance remains pending:

| Action | Expected |
| --- | --- |
| Draw a sufficiently large empty-desktop rectangle in each direction | Translucent preview and two menu entries at the chosen area |
| Create a Pen; rename, move, resize and restart | Geometry/title persist; existing Pens retain their own state |
| Escape or click elsewhere; try an accidental tiny drag | Preview dismissed; no new saved Pen or storage |
| Start on an icon, select icons, use Ctrl/Shift, cross an app/Pen | Normal Explorer/app behavior; no creation offer |
| Hover the disabled portal item | Coming later tooltip; no portal created |
| Hide several Pens and inspect the tray | All Pens listed and individually restorable |
| Move same-named files into different Pens; restart | Separate storage/journals; existing conflict/recovery protection preserved |
| Toggle desktop creation off/on; exit | Offers disabled/enabled; observer removed on exit |
| Explorer restart, icon visibility toggle, mixed DPI/negative monitors | Record provider, scaling and shell-specific limitations |

References: [mouse observer callback](https://learn.microsoft.com/windows/win32/winmsg/lowlevelmouseproc),
[UI Automation threading](https://learn.microsoft.com/windows/win32/winauto/uiauto-threading),
[per-monitor DPI](https://learn.microsoft.com/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows).
