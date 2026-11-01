> Historical prototype: superseded by [Milestone 0.2](milestone-0.2.md). The behavior below describes 0.1 only.

# Milestone 0.1 — desktop Pen experiment

## Run and scope

On Windows 11 with .NET 10, run `dotnet run --project src/PennCraft.Desktop`, then click the desktop or use Win+D. One titled Pen starts at (100,100), 360 × 240 DIPs. Drag its title; resize any edge/corner (minimum 240 × 160). The × button exits. Restart the app to reset geometry. No persistence, file listing, drops, startup registration, or shell extension is included.

## Integration decision

Use an independent top-level WPF window, with WS_EX_TOOLWINDOW and WS_EX_NOACTIVATE and WM_MOUSEACTIVATE returning MA_NOACTIVATE. WindowChrome supplies native resize hit testing; a title Thumb supplies captured movement. Only the Pen rectangle intercepts input. It deliberately blocks desktop clicks beneath its opaque body. Outside it, Explorer receives input normally; desktop files are never read or modified. File drops are disabled; drops on the body are not forwarded to Explorer.

A 250 ms UI timer reads GetShellWindow and foreground window class/process. Show the Pen only when a live shell owns the foreground Progman/WorkerW desktop; otherwise hide it. Position it at HWND_BOTTOM with SWP_NOACTIVATE, never topmost. This conservative focus gate also hides the Pen over inactive desktop areas when another app is focused. A polling delay up to 250 ms is expected. No SetParent, Explorer injection, undocumented messages, global hooks, or Explorer window mutation.

Shell handle/process changes invalidate visibility and trigger rediscovery. TaskbarCreated also invalidates state. Loss of shell hides the independent Pen; the app and geometry survive for the next desktop focus. A failed SetWindowPos hides and retries. This is best-effort recovery, not a claim that every Explorer/Windows variant is supported.

Diagnostics: %LOCALAPPDATA%\\PennCraft\\desktop.log, UTC timestamps, OS/runtime, HWND/PID, visibility transitions, shell replacement and native placement failures. Rotates at approximately 1 MiB to .previous; log failures do not crash the UI. Logs do not contain desktop filenames.

API references: [window activation](https://learn.microsoft.com/windows/win32/inputdev/about-mouse-input), [SetWindowPos](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowpos).

## Verification boundary

Windows CI builds with warnings as errors, tests the visibility policy, and creates an isolated real HWND on an STA thread to check no-activate/tool-window styles, no topmost/app-window style, and foreground preservation. These tests do **not** demonstrate Explorer coexistence or restart recovery.

Manual Windows 11 acceptance remains **pending**. Record Windows build, monitor/DPI configuration, result and relevant log entries:

| Check | Expected |
| --- | --- |
| Launch while an app is focused; then desktop click / Win+D | No focus steal; Pen appears only with desktop focus |
| Open/focus/maximize an app; Alt+Tab; taskbar | Pen hides, never overlays active apps; absent from taskbar/Alt+Tab |
| Drag title rapidly; release outside Pen; resize all 8 directions | No jump/stuck capture; minimum size respected; desktop icons remain fixed |
| Click/double-click/right-click outside Pen; desktop selection rectangle | Normal Explorer behavior |
| File drag outside Pen and onto its body | Outside works normally; body rejects drops; no file changes |
| Task Manager → Restart Windows Explorer (save work first) | Pen hides while shell is unavailable; returns on desktop focus without process restart |
| Win+D repeatedly; minimize-all/restore; lock/unlock | No stray overlay or focus theft; document any shell-specific failure |
| Mixed DPI, secondary/negative-coordinate monitor, unplug monitor | Verify drag/resize and document inaccessible geometry; automatic monitor recovery is deferred |
| Virtual desktop switch; desktop icons hidden; accessibility/high contrast | Record limitations; no support claim until verified |
| Exit via ×; inspect diagnostics | Process exits, no Explorer alteration; bounded transition log |

If desktop z-order places the Pen behind icons/wallpaper, or Win+D prevents its return, this experiment fails the feasibility gate on that build. Reassess before expanding features. Accessibility/keyboard movement, durable layout, monitor recovery, and virtual-desktop semantics remain future work.
