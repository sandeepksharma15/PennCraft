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

## Milestone 0.4

Implemented actual same-volume moves between the user desktop and dedicated Pen storage, with conflict refusal, flushed intent/settings snapshots and identity-based recovery. Milestone 0.3 was manually accepted by the user; 0.4 requires fresh interactive validation. See [usage and manual checklist](milestone-0.4.md).

Public desktop moves, cross-volume transfers, folder portals, automatic path tracking and multiple Pens remain future work.
