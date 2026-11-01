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

## Milestone 0.1

Implemented an independent, non-activating desktop-focus Pen prototype with movement, resizing, diagnostics and best-effort shell rediscovery. See [implementation and manual checklist](milestone-0.1.md).

**Feasibility gate remains pending:** demonstrate coexistence on Windows 11 without replacing/injecting into Explorer or moving user files. CI establishes build/test results only. Reassess integration if manual checks fail.
