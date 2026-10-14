# Development roadmap

Build in small, usable increments. Each phase includes tests, build verification, and Windows behavior checks.

| Phase | Deliverable |
| --- | --- |
| 0 — Feasibility | Validate desktop placement, z-order, hit testing, drag-and-drop, Explorer restart, DPI, and multi-monitor behavior |
| 1 — Pens | Create, move, resize, rename, delete, and display items |
| 2 — Persistence | Durable layouts and recovery across sessions and display changes |
| 3 — Portals | Live folder-backed containers and file interactions |
| 4 — Rules | Non-destructive automatic organization and rule previews |
| 5 — Interaction | Fold, scroll, tabs, context menus, and icon scaling |
| 6 — Workspaces | Pages, snapshots, layout backup/restore, monitor-aware profiles |
| 7 — Personalization | Styling, themes, animations, and settings import/export |
| 8 — Distribution | Installer, startup integration, diagnostics, accessibility, and performance checks |

## Next milestone: 0.1

Create a desktop-level titled surface that can be dragged and resized, leaves normal desktop interaction intact outside its bounds, and recovers from Explorer restart. Include diagnostic logging.

**Gate:** demonstrate this behavior on Windows 11 without replacing Explorer, injecting into Explorer, or moving user files. If the overlay cannot reliably coexist with the shell, reassess integration before proceeding.
