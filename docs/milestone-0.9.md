# Milestone 0.9 — Folder Portal file operations

## Use

Drag one file, shortcut or folder between a Portal and the user desktop, a stored Pen item and a Portal, or two Portals. A drop means **Move**; copy modifiers and multi-selection are deferred. Shortcut files move without moving their targets.

Desktop input is restricted to one immediate child of the current user's desktop directory; public-desktop items and arbitrary Explorer sources are not accepted. External-reference Pen items cannot be moved into a Portal: use their real folder in Explorer. Private in-process drag tokens remain the only internal payload; no external FileDrop data is exposed for outgoing drags.

Windows IFileOperation performs Portal moves on a dedicated STA with the container window as owner. Progress, permission, error and cancellation UI are left to Windows. Existing destination names are refused before starting. A race that introduces a conflict afterward is handled by Windows confirmation UI, never a silent overwrite flag. Moving a folder into itself or a descendant, system items and moving symbolic links/junctions are refused.

Right-click Portal items for the native Windows menu, including properties and delete. Rename uses the existing explicit rename dialog. No Pen membership removal command appears for Portal items. Windows owns the behavior of shell commands; installed extensions vary. Portal contents refresh after commands and normal polling continues.

## Persistence and interruption

A Portal remains a path-based live folder view. Only Pens have membership. All windows pause item commands and normal membership writes during a Portal move, including other Portals displaying either folder.

Before the Windows call, transfers.json.portal records the source, destination, owning container IDs and source identity. After a successful call, the new destination identity is flushed before committing target/source Pen membership. Preflight saves container settings before starting. No membership is removed merely because Windows reports cancellation.

Startup recovery runs before opening windows. Verified source present and destination absent clears an unperformed intent. Source absent and verified destination present completes membership idempotently. The original identity suffices for a same-volume move; a completed cross-volume move uses the recorded destination identity. An interruption after a cross-volume move but before recording its new identity cannot be inferred safely and pauses operations for review. Both copies present (including partial cross-volume copies), missing items, corrupt settings/journals or identity mismatches also pause operations and retain files and journal. No recovery code copies, deletes or replays moves.

If recovery pauses operations, inspect the recorded source/destination through Explorer and retain the journal for diagnosis. Do not reset by deleting Pen storage. Cross-volume moves are Windows copy/delete operations and cannot provide the atomic rename guarantee of Pen-to-Pen moves. Cloud and network providers may need manual intervention; unavailable paths can delay the worker. Folder links in parent paths follow Windows resolution. Cancellation and partial folders require checking both locations.

## Validation

Automated tests use disposable temporary directories for desktop/Portal, Pen/Portal and Portal/Portal moves, shortcut bytes, nested contents, conflicts, stale identity, cancellation, interruption, ambiguous partial copies and invalid recovery records. One integration test exercises the actual Windows shell move on an STA without conflicts. UI prompts and real providers remain manual acceptance.

Manual checklist:

- Move a disposable file, shortcut and folder in each supported direction; open the result and confirm only the intended location remains.
- Cancel a drag with Escape; release over an unrelated app; verify no move.
- Try an existing destination name, same-folder Portal, self/descendant folder and external Pen reference; confirm preservation.
- Test Windows cancellation/progress and a cross-volume move using disposable data.
- Verify native right-click Properties, rename and recycle/delete; check other Portals reflecting the same directory.
- Restart with visible/hidden Pens and Portals after transfers; confirm membership and location.
- Verify existing Pen/Desktop and Pen/Pen transfers still work.
- Record OneDrive, network, Explorer restart and mixed-DPI behavior separately from automated results.

References: [IFileOperation](https://learn.microsoft.com/windows/win32/api/shobjidl_core/nn-shobjidl_core-ifileoperation), [operation flags](https://learn.microsoft.com/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags).

Portal file operations cannot move PennCraft's managed storage/settings trees or their parent folders, or use a Portal linked inside those internal trees. Normal stored-item transfers continue through their owning Pen.
