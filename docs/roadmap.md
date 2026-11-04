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
