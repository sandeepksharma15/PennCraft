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

## Milestone 0.2

Implemented the single Pen foundation: title editing, eight-direction resize, background opacity, hover controls, hide/show through the tray, and atomic JSON persistence. See [usage and manual checklist](milestone-0.2.md).

The independent HWND now maintains desktop-level visibility without a foreground-focus gate. Interactive acceptance of Win+D, Explorer coexistence and display configurations remains pending; CI establishes build/test results only. Icon management and portals remain future work.
