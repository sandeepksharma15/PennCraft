# Development roadmap

| Phase | Deliverable |
| --- | --- |
| 0 — Feasibility | Desktop placement, z-order, hit testing, drag, Explorer restart, DPI, multi-monitor validation |
| 1 — Pens | Create, move, resize, rename, delete, display items |
| 2 — Persistence | Durable layouts and display recovery |
| 3 — Portals | Live folder-backed containers |
| 4 — Rules | Non-destructive automatic organization |
| 5 — Interaction | Fold, scroll, tabs, menus, scaling |
| 6 — Workspaces | Pages, snapshots, monitor profiles |
| 7 — Personalization | Themes, animation, import/export |
| 8 — Distribution | Installer, startup, accessibility, performance |

## Milestone 0.7

Implemented direct single-item transfers between Pens with physical stored-item moves, reference-only membership transfers, opaque drag tokens and a workspace-level recovery journal. The user manually accepted 0.6; fresh 0.7 interactive acceptance remains pending. See [usage and manual checklist](milestone-0.7.md).

Portal file operations, multi-item operations, copy transfers, Pen deletion, undo UI and broader shell/provider validation remain future work.

## Milestone 0.8

Basic Folder Portals display live immediate folder contents, open items and retain the folder connection and window settings. File operations remain deferred. See [scope and manual checks](milestone-0.8.md).

## Milestone 0.9

Windows-managed single-item Portal moves and native item context menus are implemented. Milestone 0.8 was manually accepted; 0.9 requires fresh interactive acceptance. Multi-selection and copy modifiers remain deferred. See [usage and checks](milestone-0.9.md).

## Milestone 0.10

One-time fresh-workspace setup groups desktop PDFs, .lnk shortcuts and folders into Documents, Programs and Folders Pens. Recycle Bin can be added as a namespace reference with reversible desktop icon visibility. There is no recurring organization. See [scope and manual checks](milestone-0.10.md).

## Milestone 0.11

Arrange visible Pens and Portals on the primary monitor in equal-width columns from right to left, with content-based heights and minimum six-column/three-row capacity. Insufficient space leaves the layout unchanged. One-level session Undo and geometry-only recovery are included. See [usage and validation](milestone-0.11.md).

## Milestone 0.12

Screen-grid movement and resizing use a 12-pixel step at 100% display scaling, monitor-relative anchors, Alt smooth bypass and Escape cancellation. Automatic layout shares the grid while retaining its capacity and minimum-size guarantees. See [usage and validation](milestone-0.12.md).

## Milestone 0.13

Manual fold/unfold for Pens and Folder Portals, persisted state, compact screen-grid movement and automatic layout participation. Expanded height is retained. See [usage and checks](milestone-0.13.md).

## Milestone 0.14

Grouped container menus and persisted per-container item scaling (75%, 100%, 125%, 150%), with scale-aware automatic layout. Tabs are deferred by user preference. See [usage and checks](milestone-0.14.md).

## Milestone 0.15

Named saved layouts capture container presentation, with restore, rename/delete, session Undo and presentation-only startup recovery. Restore checks the monitor setup and preserves current contents and metadata. Tabs remain deferred. See [usage and checks](milestone-0.15.md).

## First public beta — 0.1.0-beta.1

Branding, version/About, opt-in first-run organization, error logging, unsigned self-contained Windows x64 installer/portable packages, checksums and verified prerelease automation. See [beta guide](beta-guide.md) and [release process](releasing.md). Tabs remain deferred.
