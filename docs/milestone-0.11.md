# Milestone 0.11 — automatic Pen layout

Choose **Arrange Pens automatically** from the tray or any Pen/Portal hamburger menu. All user-visible Pens and Folder Portals are arranged on the primary monitor. Hidden containers remain hidden and keep their geometry.

## Layout

- Common width: at least 600 device-independent pixels, enlarged if the system scrollbar requires it to preserve six icon columns.
- Height: content rows at six columns, with at least three rows. Header, border, inset and measured status text are included.
- Outer margins and inter-Pen gaps: 24 device-independent pixels, scaled to the primary display.
- Start at the usable area's top-right, stack downward, then continue in a column to the left. Respect the taskbar and all work-area edges.
- Read current columns from right to left, and Pens top to bottom within each column; ties use stable workspace order. Repeating the command with unchanged contents does not reshuffle the layout.
- A tall container is capped to the usable height and scrolls.
- If all containers cannot fit at these minimums, explain the required/available columns and change no bounds. There is no partial placement, overlap or automatic shrinking below the minimum.

This is a one-command snapshot of current contents. Later content changes do not continuously resize or reposition containers. Manual drag/resize remains available; the six-column/three-row minimum applies to automatic layout only.

## Undo and persistence

**Undo last automatic layout** restores the previous geometry of the affected containers in the current app session. Titles, opacity, contents, Portal connections and visibility are preserved. Undo is unavailable if an affected container was deleted/hidden; show it again to restore availability where possible. A changed monitor/DPI/work-area configuration prevents Undo until another automatic arrangement is made. New containers are left alone by Undo. A successful second layout replaces the prior one-level Undo snapshot. There is no Redo or Undo persistence across restarts.

Bounds are captured and placed in physical pixels, with a monitor transition before final sizing. Live WPF geometry is saved afterward. A layout recovery record stores only geometry. Settings preflight precedes movement; interrupted/failed writes restore previous geometry before startup opens windows. Other saved metadata is merged unchanged. A failed recovery retains its record and pauses operations for review. Normal layout and Undo never move, copy or delete filesystem items.

## Validation

Tests cover column packing/alignment/margins, ordering, content-height boundaries, capping, insufficient space with no partial plan, offset work areas, DPI scales, metadata-preserving commit/Undo, locked settings, partial commits and interrupted/corrupt recovery. A serialized WPF test verifies actual six-column/three-row grid placement, viewport height and restoration of native bounds, using temporary files only.

Manual acceptance remains required:

1. Show several Pens and Portals with different item counts; hide another one.
2. Arrange from the tray and hamburger menus. Check equal widths, six columns, three-row minimum, consistent margins and right-to-left column wrapping.
3. Check a tall Portal uses scrolling, and hidden containers remain unchanged.
4. Choose Undo and verify previous bounds and current titles/items/connections.
5. Arrange again, restart and verify the saved primary-screen layout.
6. Use enough containers or a small screen to exceed capacity; confirm the complete layout stays unchanged.
7. Move visible containers from a differently scaled secondary monitor, arrange, then Undo. Check coordinates, clipping and taskbar margins.
8. Change monitor scaling/work area after arranging and confirm Undo safely declines.
9. Check normal item moves, Win+D, overlapping windows and Portal deletion still work.

References: [Windows DPI guidance](https://learn.microsoft.com/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows), [DPI and device-independent pixels](https://learn.microsoft.com/windows/win32/learnwin32/dpi-and-device-independent-pixels).
