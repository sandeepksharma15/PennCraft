# Milestone 0.7 — direct transfers between Pens

## Use

Drag one item from a Pen and release it over another visible Pen. The pointer indicates an acceptable Pen or desktop target.

- A stored file, shortcut or folder moves directly into the destination Pen's own storage. No desktop staging or copy/delete fallback is used.
- An external reference transfers membership only. Its original filesystem path remains unchanged.
- Escape, an invalid target, or dropping back onto the source Pen leaves membership unchanged.
- An existing destination filename or reference is refused. Both originals and their memberships are retained; resolve the conflict and retry.
- Dragging out onto exposed desktop space retains the existing Move to Desktop / remove-reference behavior.

Transfers are single-item. Shortcut files move as files; their targets are not moved. Hidden Pens must be shown before dropping onto them. Names, geometry, opacity and visibility remain independent of membership transfers.

## Transaction and recovery

The workspace owns transfers.json beside workspace.json. Before changing a file, PennCraft checks both Pens, verifies their storage/ownership, preflights both settings stores and flushes a transfer intent. It then performs the existing same-volume no-overwrite native rename, verifies the original item identity at the destination, saves destination and source membership, and clears the intent only after both snapshots are durable.

The source/destination windows pause item actions, reconciliation and ordinary settings writes during the transaction. Live title/geometry/visibility changes are preserved when committed membership is applied. All owned filesystem operations share the existing move/rename gate.

Startup reconciles a pending transfer **before** opening Pen windows or starting their individual watchers:

- Verified source present, destination absent: retain/restore source membership, remove the uncommitted destination entry and clear intent. Never replay a physical move automatically.
- Source absent, verified destination present: complete both membership snapshots idempotently.
- Reference transfer: complete the durable membership intent without touching the referenced file, even if interrupted before either settings update.
- Both copies present, both missing, identity mismatch, malformed journal/settings or invalid Pen IDs/paths: retain files and journal and pause item operations for review.

A prepared reference transfer is therefore a durable logical move that can finish on restart. A physical transfer that fails before rename remains at the source after recovery. A failure after rename or after saving only one Pen is reconciled to the verified destination. Settings failures after intent is recorded require restart/recovery before more item operations.

Drag data carries only a short-lived opaque token. Only the current drag in the same running workspace can resolve it; source IDs/paths never come from external serialized data. Accepted, expired, unknown or replayed tokens are rejected. No FileDrop payload is exposed to Explorer for internal transfers.

## Limits

The desktop filesystem, no-overwrite, identity, cloud-provider and link/junction restrictions from [Milestone 0.4](milestone-0.4.md) still apply. Copy modifiers, multi-selection, direct transfers to hidden Pens, automatic conflict renaming, cross-volume transfer and undo UI are deferred. A duplicate reference in the destination is treated as a conflict rather than silently removing the source entry.

An ambiguous transfer pauses filesystem-changing item operations while leaving item opening and storage-folder inspection available. Review the recorded paths and retain the journal for diagnosis; do not delete storage to reset the app. External applications/providers can change files while an operation is underway and may require manual review. Hardware/filesystem failure can exceed ordinary rename/flush guarantees.

## Validation

Windows CI tests actual temporary-file/folder transfers, shortcut bytes and native identity preservation, both settings snapshots, reference transfers, same-Pen/duplicate/name conflicts, source locks, preflight failures, every commit checkpoint, idempotent recovery, corrupt/out-of-bound journals, stale source identities, token expiration and absence of external file payloads. No tests operate on the user's desktop.

The user manually accepted 0.6. Fresh interactive 0.7 acceptance remains pending:

| Action | Expected |
| --- | --- |
| Drag a disposable stored file, shortcut and folder from Pen A to Pen B | Source tile disappears; destination tile appears; actual item exists only in B storage |
| Transfer an external reference | Membership moves; original file path/bytes unchanged |
| Drop onto a destination with the same filename or reference | Clear conflict status; source and destination preserved |
| Escape, drop back onto source, release over an app/taskbar | No transfer |
| Transfer back, then move out to desktop | Both directions and existing desktop behavior work |
| Rename/open/right-click the received item | Existing native menus and identity tracking work |
| Restart with visible/hidden Pens after transferring | Exactly one intended membership and correct storage restored |
| Try while an operation/recovery is pending | Transfer refused; recorded files preserved |
| Mixed DPI, Explorer restart, OneDrive desktop | Record provider and shell-specific limitations |

References: [WPF drag and drop](https://learn.microsoft.com/dotnet/desktop/wpf/advanced/drag-and-drop),
[MoveFileEx](https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-movefileexw).
