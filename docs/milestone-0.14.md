# Milestone 0.14 — Menus and item scaling

Open the hamburger menu or right-click empty Pen space. Both use the same menu:
- **Rename Pen** edits the centered title.
- **View** contains Fold/Unfold, Item size and Background opacity. Checked options show the current choices.
- **Layout** contains automatic arrangement, its Undo and Reset position and show.
- Storage/folder access, Hide and Delete remain available. Ordinary Pens also offer Add Recycle Bin.

When the Pen has keyboard focus, the Menu key or Shift+F10 opens item commands for a selected visible item, or the container menu when no item is selected or the Pen is folded. Item right-click commands remain Windows-managed. Commands recheck their availability when invoked.

## Item size

Choose **View → Item size → 75%, 100% (default), 125%, or 150%**. Icons, labels, tile spacing and selection backgrounds scale together. The grid reflows and scrolls within the current window. Header and menu sizing follow Windows display settings independently.

Each Pen and Folder Portal saves its own size, including when folded or hidden. Existing settings default to 100%. Changing item size preserves window geometry, membership and folder connections. Failed appearance writes restore the previous setting.

Automatic layout uses a common width large enough for six columns at the largest selected item size. Each expanded container gets at least three scaled rows; smaller items may fit more columns. If the screen cannot fit the result, nothing moves. Layout Undo is available while participants retain the fold states and item sizes used for that arrangement. Enlarging items does not automatically enlarge or rearrange a manually sized Pen.

Tabs are deferred. This milestone does not add continuous zoom, global scale changes or custom menu themes. Icons continue to use the existing Windows shell bitmap source; enlargement quality depends on that source.

## Validation

Windows tests cover legacy/invalid settings, scale persistence for Pens and Portals, unchanged membership/geometry, fold/hide/restart, actual grid wrapping and row capacity, mixed-size packing, insufficient space, menu checks, stale menu busy guards and failed-write rollback.

Manual checks:
- Open menus through the hamburger and empty-space right-click; confirm grouped commands and checked choices agree.
- Try every size in Pens and Folder Portals, including long labels and enough items to scroll.
- Open, right-click and drag scaled items between containers and the desktop.
- Restart with different sizes in hidden/folded containers; confirm restoration.
- Arrange mixed sizes, test insufficient space and Undo; change size and confirm old Undo is unavailable.
- Check keyboard menu access during explicit focus, and rendering/movement across monitors with different Windows scaling. Automated CI does not replace interactive shell acceptance.
