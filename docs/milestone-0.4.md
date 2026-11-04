> Multiple Pens and desktop creation are introduced in [Milestone 0.5](milestone-0.5.md). The move and recovery behavior below remains applicable to each Pen.

# Milestone 0.4 — actual desktop moves

## Behavior

Drag a file, shortcut or folder directly from your user desktop into the Pen to **move the actual item** into Pen storage. Its original desktop icon disappears because its path changed. Drag it out onto exposed desktop space to move it back. Hiding, closing or exiting the Pen never moves or deletes stored items.

Shortcuts move as shortcut files; their targets are not moved. A shortcut that depends on a relative target path may need repair after relocation. Double-click still uses the normal Windows association.

Existing 0.3 references are preserved without automatically moving anything. Re-drop an existing desktop reference to move that source into storage and replace its old path. Public/shared desktop items, files inside desktop subfolders, and items from other locations remain references. Dragging out a reference removes only its membership.

Same-name conflicts are refused in both directions. Neither item is overwritten, merged or renamed automatically. Rename one of the conflicting items and retry. A multi-selection is processed per item; the status reports the moved count and the first error, while successful moves remain committed. Escape and invalid drag-out targets leave the item in the Pen.

## Storage

The default single Pen uses **.PennCraft/Default/Items** inside the Windows user desktop directory. The .PennCraft root is marked Hidden, and is visible in Explorer when hidden items are shown. Use **☰ → Open Pen storage folder** to inspect it after the first move. An existing unowned .PennCraft directory is never taken over.

Keeping storage under the desktop keeps ordinary moves on the same filesystem, including redirected desktops. Windows performs a no-overwrite rename with no cross-volume copy/delete fallback. Unsupported filesystems, locked files, system items, symbolic links and junctions are refused. Folder moves rename the folder as a whole; contents are not enumerated or rewritten. Storage directories cannot be redirected by junctions/symbolic links.

Cloud-desktop providers can synchronize these moves and apply their own availability restrictions. Desktop redirection, network filesystems, offline/cloud placeholders and relative shortcuts require interactive validation. PennCraft never changes Explorer settings or desktop icon visibility through shell commands.

## Recovery

A versioned **moves.json** beside Items records the source, destination, direction and native file identity before any physical move. Both journal and settings snapshots are flushed before replacement. Membership changes only after a verified rename; the journal entry is acknowledged only after settings save succeeds.

On restart:

- Source present with the original identity, destination absent: discard the unexecuted intent after saving settings. Never replay a physical move automatically.
- Source absent, destination present with the recorded identity: reconcile membership with the completed move, then acknowledge it after settings save.
- Both present, both missing, unexpected identity, malformed journal or invalid paths: retain files and journal, show a recovery error and block physical moves. Do not guess which copy is authoritative.

If settings or final journal bookkeeping cannot be saved, further moves pause until restart/recovery. Close any older PennCraft version before upgrading; earlier versions do not participate in this lock. The application holds an exclusive per-user settings lock; another instance cannot start against the same settings directory. Exit is deferred while a drag or move operation is active.

For an unresolved recovery error, inspect Pen storage and the recorded source/destination paths before making changes. Keep a copy of the journal for diagnosis. Files can be accessed through Explorer even when the app is closed. Do not delete Pen storage to reset the app. Automatic reconciliation assumes no unrelated process is replacing/reusing file identities or editing the journal during recovery; external concurrent changes can require manual review. Hardware/filesystem failures can still exceed normal rename/flush guarantees.

## Validation

Windows CI tests real temporary-file moves, byte/identity preservation, shortcut-file and folder round trips, conflicts in both directions, locked sources, journal-write failure before rename, applied/unapplied intent recovery, interrupted move-out, settings compatibility, malformed journals, wrong identities and boundary validation. Tests never touch the user's actual desktop.

The user manually accepted Milestone 0.3. Milestone 0.4 needs fresh interactive acceptance:

| Action | Expected |
| --- | --- |
| Move a disposable desktop file, .lnk, and folder into the Pen | Original icons disappear; actual items and bytes exist in storage |
| Double-click after moving; restart hidden/visible Pens | Opening and membership work at the new paths |
| Drag a managed item onto exposed desktop | Item returns; storage copy disappears; membership removed |
| Place the same filename at the destination in either direction | Conflict message; both items retained |
| Hold an item open without delete sharing and try to move it | Error; source retained; restart reconciles pending intent |
| Cancel with Escape; drop onto taskbar, Pen or an app | No physical move |
| Hide/exit the Pen with stored files; launch a second instance | Files stay stored; second instance is refused |
| Re-drop a legacy desktop reference; add an external reference | Only the desktop item moves |
| Inspect redirected/OneDrive desktop, long paths, Explorer restart and Win+D | Record provider/shell limits; no file loss |

API references: [MoveFileEx](https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-movefileexw),
[file identities](https://learn.microsoft.com/windows/win32/api/fileapi/ns-fileapi-by_handle_file_information),
[FileStream.Flush](https://learn.microsoft.com/dotnet/api/system.io.filestream.flush).
