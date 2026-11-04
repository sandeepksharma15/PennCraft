# Milestone 0.3 — desktop items

## Use

Drag one or more files, shortcuts or folders from Explorer or the desktop into the Pen. PennCraft adds references to existing paths; original desktop icons remain visible. Duplicate paths are ignored case-insensitively. New drops must exist; saved missing items remain in the Pen so temporary unavailability does not erase membership.

Items use native Windows shell icons, including shortcut target icons. The grid wraps as the Pen changes width and scrolls vertically. Double-click an item to open it with its normal Windows association. Shortcut labels omit .lnk/.url; document labels keep extensions. Hover for the full path. An unavailable item shows an in-Pen error when opened and keeps its reference.

Drag an item out onto **exposed desktop space** to remove it from the Pen. Its original file/icon stays where it was. Escape, release over the Pen, taskbar, an application or a folder window keeps membership. This is a membership drag, not a filesystem transfer; PennCraft supplies no file-move payload. The pointer indicates whether the exposed desktop is a valid target.

## Persistence and boundaries

ItemPaths are stored in the existing `%LOCALAPPDATA%\PennCraft\pen.json` with title, geometry, opacity and visibility. Earlier settings without ItemPaths load with an empty grid. References save immediately after adding/removing; geometry retains the existing debounce and atomic replacement. Renaming or moving a source externally can leave a stale reference; automatic path tracking and file watching are deferred.

Inbound WPF FileDrop accepts Copy/Link effects and refuses Move-only drags. Copy/Link here means adding a reference; no physical file is copied. Virtual shell objects (This PC, Recycle Bin, Outlook attachments) without filesystem paths are not supported. Folders open in Explorer; their contents are not listed inside the Pen.

Outgoing desktop detection reads the window under the physical cursor, its root class and Explorer process identity. It recognizes Progman/WorkerW desktop roots and excludes folder windows and taskbars. It does not change Explorer windows, shell settings, desktop icon visibility or original files. Wallpaper utilities and future shell layouts may not be recognized; in that case the item is retained. External application/file-manager drops and item reordering are deferred.

Icon extraction runs serially on an STA worker, initializes COM, freezes the WPF bitmap and releases each native icon handle. A slow shell icon handler can delay icons without blocking the grid. There is no forced termination of shell handlers. Icons refresh on app restart or re-add; changes to source icons are not watched. A placeholder remains if extraction fails.

Desktop placement, Win+D, virtual desktop and mixed-DPI limitations from [the Pen foundation](milestone-0.2.md) still apply. Run one process per user.

## Verification

Windows CI builds Release with warnings as errors and runs existing Pen tests plus membership, deduplication, missing paths, safe incoming/outgoing effects, desktop target filtering, legacy settings, restart persistence and real native icon extraction tests. CI does not launch user files.

Interactive Windows acceptance remains pending:

| Action | Expected |
| --- | --- |
| Drop desktop .txt, .lnk, .url, a folder, and a multi-selection | References appear once; originals remain visible and unchanged |
| Resize narrow/wide and add enough items to overflow | Grid wraps and vertical scrolling exposes all items |
| Double-click document, app shortcut, URL shortcut and folder | Windows opens the corresponding item |
| Drag a tile onto exposed desktop | Only membership is removed; original icon remains |
| Escape a drag; release over Pen, taskbar or another app | Membership and source file stay unchanged |
| Add duplicates; restart visible and hidden Pens | Ordered membership survives; duplicates are not added |
| Temporarily rename/remove a source; restart and double-click it | Reference retained; unavailable/open error appears without a crash |
| Drop while holding Shift; drop virtual shell objects | Move-only/unsupported drops rejected; original files remain |
| Restart Explorer; repeat Win+D; use multiple DPI/negative-coordinate monitors | Record shell-specific behavior and any desktop target detection limits |

API references: [WPF drag and drop](https://learn.microsoft.com/dotnet/desktop/wpf/advanced/drag-and-drop),
[SHGetFileInfo](https://learn.microsoft.com/windows/win32/api/shellapi/nf-shellapi-shgetfileinfow),
[GetAncestor](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getancestor).
