# Milestone 0.10 — one-time desktop setup and Recycle Bin

## First-run organization

On a genuinely fresh profile, PennCraft automatically creates Documents, Programs and Folders Pens. It moves immediate desktop PDF files (case-insensitive .pdf) into Documents, .lnk shortcut files into Programs, and immediate desktop directories into Folders. Other extensions stay on the desktop. Hidden/system items and symbolic links/junctions are skipped. Folder contents move with their folder; no recursive classification is performed. Shortcut targets are never moved.

This is a one-time startup attempt, not a recurring rule engine. An existing workspace index (even empty), legacy default Pen settings, orphan Pen settings or existing managed desktop storage prevents automatic organization. Hidden Pens count as existing Pens. Upgrading an established installation never reorganizes it.

The inventory is collected before publishing the new workspace. All three Pen settings and the workspace index are saved before physical moves begin. Each move uses the existing same-volume, no-overwrite Pen journal; membership is saved before acknowledgement. Once the workspace is published, a later launch performs normal journal recovery without rescanning or automatically moving new desktop files. Partial setup is not silently retried. first-run-report.json records moved/skipped counts and paths when the attempt finishes. Locked/unavailable items stay on the desktop; unexpected settings failures preserve journals for recovery. Small displays may need the existing reset-position command.

## Recycle Bin

Drag the Windows Recycle Bin desktop icon into a Pen, or choose **Add Recycle Bin** in the Pen menu. It is a shell namespace reference, not a filesystem file or directory. The Pen shows its Windows icon; double-click opens Recycle Bin. Its native context menu includes Windows commands such as Empty Recycle Bin and Properties, with Windows confirmation behavior. PennCraft never empties it automatically.

After the reference is durably saved, PennCraft hides the original desktop icon using only the current user's Recycle Bin visibility values under Explorer HideDesktopIcons. Both existing values (including absence) are recorded in recycle-bin-visibility.json before any change. No shell namespace registrations, policy keys, other icons, Explorer process or file contents are modified.

Move the reference between Pens normally. Dragging it back to exposed desktop space or removing its final Pen reference restores the previous desktop setting. Multiple Pen references keep the original hidden until the last is removed. Normal app exit restores the setting; restart hides it again if a saved reference exists. A hidden Pen can be shown through the tray. Polling durable membership avoids hiding after a failed membership save.

Visibility restoration preserves an externally changed value when it no longer equals PennCraft's hidden value. Corrupt recovery records are retained and prevent further visibility changes. Registry access/policy restrictions may leave the original visible and show a status. Explorer refresh can lag; use desktop Refresh if necessary. This Explorer visibility setting is Windows-specific and needs interactive acceptance on supported Windows versions.

A crash can leave the Windows icon hidden until PennCraft starts again. If the app is unavailable, restore Recycle Bin through Windows **Settings > Personalization > Themes > Desktop icon settings**. Its contents are unaffected.

This milestone supports Recycle Bin specifically. Other virtual desktop objects and Portal file operations involving namespace objects remain unsupported. Outgoing drag data remains private; no virtual item is passed to a file-move API. Shell drag input is bounded, structurally validated and compared to a Windows-generated PIDL for the fixed allowlisted object.

## Validation

Automated tests use isolated temporary workspaces/desktops and fake icon settings; they do not organize the user's desktop, empty Recycle Bin, or alter desktop visibility. Windows integration checks obtain the native icon and validate the Windows-generated single-object shell drag payload. Tests cover fresh/legacy/empty/orphan state, fixed classification, exactly one organization attempt, preserved unrelated files, namespace persistence and reconciliation, malformed drag input, visibility baselines, crash restoration and external setting changes.

Manual checks:

- Use an isolated Windows user/profile or Windows Sandbox for first-run setup. Do not delete an established workspace or its storage to simulate a fresh install.
- Prepare disposable PDFs, shortcuts, folders, hidden items and unrelated files. Verify the three groups and file bytes; create a new PDF and restart to confirm no second organization.
- Upgrade an existing visible/hidden workspace and confirm no automatic moves.
- Drag Recycle Bin into a Pen, open it and inspect its native menu without emptying valuable contents.
- Transfer its reference between Pens; remove/drag it back; confirm desktop visibility restoration.
- Test normal exit/restart, multiple references, hidden Pens and policy restrictions.
- Record Explorer refresh, DPI and Windows-version behavior separately from automated evidence.

References: [CIDA shell drag format](https://learn.microsoft.com/windows/win32/api/shlobj_core/ns-shlobj_core-cida), [desktop icon settings](https://support.microsoft.com/windows/show-desktop-icons-in-windows-cf659abf-7ec4-4a16-9c2c-379f8a54aa7f).

## Offer organization after emptying the workspace

Closing a Pen hides it; it does not delete its settings. On restart, after item recovery finishes, PennCraft now offers organization when every saved Pen is empty and hidden. A visible Pen, any saved membership (including an unavailable reference or Recycle Bin), a Folder Portal, or pending move recovery suppresses the offer.

The prompt previews counts for PDFs, .lnk shortcuts and desktop folders. No is the default. Nothing moves unless Yes is chosen. Matching Documents, Programs and Folders Pens are reused and shown; missing categories get new Pens. Other existing empty Pens are retained. The preview is read-only and only its listed paths are submitted to the existing journaled incoming-move flow. There is still no recurring organization.

Declining is remembered across restarts while the workspace remains empty/hidden. Using a Pen again (showing it or adding an item) re-arms the offer for a later empty-and-hidden restart. A desktop with no matching items produces no prompt.

### Test the empty-workspace offer

1. Update to this change. Prepare disposable desktop PDFs, .lnk shortcuts and a normal folder.
2. Move all items out of every Pen; remove external/system references as well. Hiding an item is not removing its membership.
3. Close/hide all Pens. A saved Folder Portal suppresses this offer, even if hidden.
4. Exit PennCraft through its tray menu, then launch it again. Hiding Pens alone leaves the process running.
5. Verify the prompt's counts. Choose Yes and confirm matching Pens are reused/shown and items arrive there.
6. Repeat the empty/hidden setup and choose No. Confirm no moves, no deleted Pens and no repeated prompt on another restart.
7. Show a Pen, hide it again, exit and restart to verify the offer is re-armed.

Automated tests cover eligibility exclusions, persisted dismissal and older workspace settings, read-only preview counts, plus existing incoming move/recovery behavior. The prompt and tray interaction remain manual Windows acceptance.
