> Direct transfers between Pens are introduced in [Milestone 0.7](milestone-0.7.md). The context menu and reconciliation behavior below remains applicable.

# Milestone 0.6 — item context menus

## Use

Right-click an item to select it and open its classic Windows shell context menu. Applicable native commands (such as Open, Open with, Copy, Cut, Delete and Properties) depend on the file type, Windows version and installed shell handlers. Double-click still opens the item. Apps key or Shift+F10 opens the selected item's menu while the Pen has keyboard focus.

Two Pen commands are appended:

- **Move to Desktop** for an item physically stored in that Pen, using the existing journaled no-overwrite move service.
- **Remove from Pen** for an external reference, removing membership without deleting the original file.
- **Show in File Explorer** opens its containing folder and selects the actual file/shortcut.

The first two are alternatives, so each existing item receives one membership command plus Show in File Explorer. Right-clicking empty Pen space opens the Pen settings menu. Escape or clicking elsewhere dismisses menus. Selection is single-item in this milestone.

A shortcut's shell menu is requested for the shortcut file itself; filesystem actions do not resolve and operate on its target. Open follows the target normally.

## Rename and delete

Rename uses a Pen-owned name dialog because Explorer's inline editor is not available inside a WPF tile. Enter the full filename, including any extension; the stem is selected initially. It renames within the original parent directory, refuses invalid/reserved names and existing destinations, and shares the physical-move gate with other Pens.

Delete is invoked through the Windows shell handler. Windows owns its confirmation, cancellation, elevation and Recycle Bin behavior. Recycling is used where Windows supports it; Shift-modified actions, network locations and other unsupported providers can offer permanent deletion through Windows. PennCraft never substitutes Remove from Pen for Delete, and never implements its own permanent-delete fallback.

Commands are paused while an item move or unresolved recovery is pending. If settings cannot be saved or a stable item identity cannot be obtained, filesystem-changing shell invocation/rename is not started; Open by double-click and Show in File Explorer remain available.

## Membership reconciliation

Before invoking a native item command or renaming, PennCraft persists native file identities with membership. Target identity is checked again after menu selection; a replacement at the same path is refused rather than operated on. Non-recursive parent-folder watchers then reconcile relevant rename, deletion and move-out notifications. Existing watchers stay attached during same-folder renames; unrelated filenames do not trigger refreshes. A successful same-folder rename follows the original identity, refreshes the tile in its existing grid position and saves its new path. An original identity confirmed absent from a completely readable parent folder is removed from membership. Unrelated replacements at the old path are not adopted.

Startup performs the same reconciliation, including a rename/deletion completed after the last settings save. Older missing references with no recorded identity are retained. Inaccessible parents, incomplete metadata scans and ambiguous hard links are retained for later refresh; no filesystem object is moved/deleted by reconciliation. Files moved outside the original parent are removed from membership rather than searched for across drives.

Tracking metadata extends pen.json/Pens/{id}.json without moving existing storage. The existing physical move journal remains authoritative while its recovery is unresolved. Folder watching follows the immediate parent only, not folder contents. Watcher overflow triggers a complete parent scan; unavailable parents/providers can need a restart or a later item action to refresh.

## Windows integration and limits

ShellContextMenu owns PIDLs, shell COM interfaces, the native popup menu and temporary owner-window message forwarding for IContextMenu2/IContextMenu3. Query, display and invocation run on the WPF STA. Identity scans run off the dispatcher. The Pen temporarily permits activation for its explicit context menu/dialog, then restores its normal non-activating mode.

This is the classic shell menu, not the compact Windows 11 menu. Available extensions/verbs differ by machine. Explorer-specific or third-party handlers can depend on services a standalone host does not provide; those actions require manual verification. Shell handlers run under normal Windows behavior and can display their own dialogs or start asynchronous work. PennCraft does not suppress Windows security or deletion confirmations.

Missing items receive a small menu to remove the unavailable entry and, where possible, open its containing folder. No actual file is deleted by that fallback. Automatic icon refresh for same-path association/custom-icon edits, multi-selection, undo UI, cross-folder identity search, junction/symlink tracking and full filesystem-provider coverage remain outside this milestone.

## Validation

CI constructs and queries a real native menu for a Unicode-named temporary file without displaying it or invoking a file command. Other tests cover identity capture, rename/restart reconciliation, deletion, unrelated replacements, unavailable parents, ambiguous hard links, corrupt tracking metadata, filename validation, rename conflicts and shortcut-byte preservation, alongside the existing move/workspace tests. Tests never use the user's desktop or Recycle Bin.

The user manually accepted 0.5. Fresh interactive 0.6 acceptance remains pending:

| Action | Expected |
| --- | --- |
| Right-click file, folder and shortcut | Selected tile, applicable Windows commands and storage-aware Pen commands |
| Escape/click elsewhere; double-click afterward | Menu dismisses; opening and drag behavior still work |
| Move to Desktop; Remove from Pen on an external reference | Actual move versus membership-only removal; no overwrite |
| Show in File Explorer | Parent opens with the actual item selected |
| Rename a disposable item; restart | New name/icon/path retained; no stale tile |
| Rename to an existing name; cancel rename | Existing files preserved; cancelled operation unchanged |
| Delete a disposable local file; cancel a separate deletion | Windows behavior/Recycle Bin where supported; deleted entry removed, cancelled entry retained |
| Cut/paste outside the Pen; rename/delete through Explorer | Parent notifications update membership; unavailable/ambiguous cases retained |
| Shortcut Properties/Open with/third-party submenus | Record handler compatibility; shortcut targets not renamed/deleted |
| Empty-space right-click; keyboard menu key | Pen menu versus selected-item menu |
| Multiple Pens, mixed DPI, Explorer restart and lock/unlock | Existing desktop/creation/move behavior retained |

References: [IContextMenu](https://learn.microsoft.com/windows/win32/api/shobjidl_core/nn-shobjidl_core-icontextmenu),
[menu message forwarding](https://learn.microsoft.com/windows/win32/api/shobjidl_core/nf-shobjidl_core-icontextmenu3-handlemenumsg2),
[Explorer selection](https://learn.microsoft.com/windows/win32/api/shlobj_core/nf-shlobj_core-shopenfolderandselectitems).
