## Folder Portal operations

Milestone 0.9 adds single-item moves between Portals, Pens and the user desktop, plus native Portal item menus. See [scope and limitations](milestone-0.9.md).

# Features

Milestone 0.7 adds direct transfers of stored items and external references between Pens, with no-overwrite conflict checks and two-Pen recovery. See [usage and verification](milestone-0.7.md).

The broader features below remain **planned**.

| Area | Scope |
| --- | --- |
| Pens | Create, rename, drag, resize, delete, and display shortcuts/files |
| Layouts | Persist positions, membership, restore, snapshot, and backup |
| Portals | Display and interact with live directory contents |
| Rules | Classify by name, type, extension, or location; preview conflicts |
| Interaction | Fold, scroll, tabs, menus, scaling, and icon visibility |
| Workspaces | Multiple arrangements, desktop pages, monitor-aware layouts |
| Appearance | Colors, opacity, typography, themes, and animations |
| Operations | Startup integration, diagnostics, packaging, update strategy |

## Safety expectations

Visual grouping must not silently move or delete files. Explicit filesystem operations require clear intent. Explorer restarts and display changes should not destroy saved arrangements.

## One-time setup and virtual items

Fresh workspaces receive Documents, Programs and Folders Pens with the fixed first-run grouping. Existing workspaces are untouched. Recycle Bin references support opening, Windows menus and Pen transfers; desktop icon visibility is restored on removal or normal exit. See [Milestone 0.10](milestone-0.10.md).

## Automatic layout

Arrange visible Pens and Portals on the primary monitor with equal widths, content-aware heights, consistent margins and right-to-left columns. Keep hidden containers unchanged; refuse partial layouts when space is insufficient. Undo the previous arrangement in the current session. See [Milestone 0.11](milestone-0.11.md).
