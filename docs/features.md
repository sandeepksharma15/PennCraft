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
