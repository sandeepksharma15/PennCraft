# Milestone 0.15 — Saved layouts

Use **Layout → Save current layout…** in any Pen, or **Save current layout…** in the tray menu. Give the snapshot a unique name. It includes every current Pen and Folder Portal, including hidden ones.

A snapshot captures position, expanded width/height, visible state, folded state and item size. It does not capture titles, opacity, item membership or folder connections. Current contents remain current: restoring never moves files or recreates containers.

Open **Saved layouts…** from a container's Layout menu or the tray:
- Select a snapshot and choose **Restore**.
- **Rename…** changes its name.
- **Delete…** removes only that snapshot after confirmation.

Names are trimmed, case-insensitively unique and limited to 80 characters. Up to 100 snapshots are supported. Saving does not overwrite an existing name. Catalog writes are atomic; malformed/unreadable catalogs are retained and reported.

## Restore and Undo

Deleted containers are skipped and reported. Containers created after the snapshot remain unchanged. If none of the saved containers exist, nothing changes.

This initial version requires the same monitor topology, work areas and display scaling as the snapshot. A mismatch leaves the current presentation unchanged and explains why. On a matching setup, off-screen rectangles are moved into the nearest work area that can contain them. If any participating rectangle cannot fit, the entire restore is rejected. Folded containers retain their expanded height; expanding can overlap neighbors, as with ordinary folding.

**Undo last layout restore** restores the presentation immediately before the latest successful restore. It is session-only and requires the same monitor setup and all participating containers to still exist. Automatic arrangement clears restore Undo; restoring a snapshot clears automatic-layout Undo. Manual presentation changes after restoration can be replaced by Undo. Undo also checks work-area fit and brings off-screen rectangles into a fitting work area. Membership, titles, opacity and folder connections remain current.

Restore reserves all containers during its settings transaction. An interrupted or partially written restore retains a presentation-only recovery record. Startup restores the previous presentation before opening windows, while preserving current metadata and contents. If recovery cannot complete, the record remains and layout/item operations are blocked until settings access is repaired and the app restarted. OS failures can prevent immediate visual rollback; the recovery record provides the restart path.

## Validation

Automated Windows tests cover catalog CRUD, duplicate/invalid names, corrupt and locked stores, matching/mismatched displays, negative/off-screen positions, missing/new containers, no-fit behavior, presentation-only merging, Undo, partial-write recovery, and actual Pen/Portal fold/visibility/size restoration.

Manual checks:
- Save a mixed workspace with hidden/folded Pens and Portals. Rearrange and resize, restore, then Undo.
- Change item membership, titles, opacity and a Portal's live folder contents; confirm restoration preserves those current values.
- Restart and restore a named layout; rename/delete snapshots and try duplicate names.
- Delete a saved container and create another, then restore; verify skip reporting and the new container is untouched.
- Change monitor setup, taskbar work area or scaling; verify restore refuses without moving containers.
- Check mixed-DPI placement, Win+D, application-window ordering and recovery after a settings-access failure. CI does not establish interactive Explorer acceptance.

Workspace pages, monitor-specific profiles and tabs remain deferred.
