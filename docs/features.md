# Features

Milestone 0.2 implements one movable/resizable Pen, editable title, translucent background, hover controls, tray visibility commands and persistent settings. Manual Windows 11 acceptance is pending. See [usage and verification](milestone-0.2.md).

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
