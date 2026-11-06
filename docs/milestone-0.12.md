# Milestone 0.12 — screen-grid movement and resize

Header movement and all eight resize grips now snap to a common grid anchored at each monitor's physical top-left. The grid is 12 physical pixels at 100% Windows scaling, 15 at 125%, 18 at 150%, and 24 at 200%. Custom scaling rounds the step to the nearest whole physical pixel. Resolution changes the number of cells, not the logical spacing.

Movement snaps the top-left corner. Resizing snaps only the dragged edges, preserving the opposite edges. Small pointer movements accumulate from the original gesture, rather than being rounded away or recalculated from an already-snapped border. Existing off-grid geometry is retained until a move, resize or automatic arrangement.

Hold **Alt** for smooth adjustment; releasing it returns to the grid. Press **Escape** to cancel a gesture and restore its original bounds. Releasing the mouse saves the final geometry. If cancellation cannot restore the native bounds, the previous saved geometry is retained through later metadata saves until a successful geometry operation or Reset position; restarting restores it. Normal metadata updates may still save during a gesture. Manual minimum sizes remain unchanged; a minimum-edge clamp uses the nearest valid grid line without moving the opposite edge.

The monitor under the pointer supplies the grid and display scale. Moving between differently scaled monitors preserves logical size and logical pointer offset as Windows processes the DPI transition. Resize minimums account for current and destination scaling. Mixed-DPI transitions remain manual acceptance.

Automatic layout uses this same monitor grid. Widths and heights round upward to preserve six columns/three rows, work-area boundaries round inward, and gaps use two grid steps. Non-grid screen/taskbar boundaries can leave slightly larger outer margins. Insufficient space still leaves the full layout unchanged. Undo restores exact previous bounds, including off-grid ones. Saved arrangements are not silently snapped at startup.

## Validation

Pure tests cover DPI steps, common anchors for differently positioned Pens, accumulated small movements, all edges/corners, opposite-edge/minimum constraints, negative monitor origins, smooth adjustment, cross-scale grab offsets and all automatic-layout edges. Existing native WPF layout/Undo and geometry persistence checks continue to run.

Manual checks:

- Move two differently positioned Pens onto matching grid lines, including slow one-pixel pointer movements.
- Resize every edge/corner; confirm the opposite edges stay fixed and matching borders align.
- Hold/release Alt mid-gesture, and press Escape during moves and resizes.
- Check header title editing and normal item dragging still work independently.
- Arrange visible Pens/Portals; check grid-aligned bounds, scrolling capacity, Undo and persistence.
- Test monitors to the left/above the primary monitor and mixed 100/125/150/200% scaling. Check pointer anchoring, minimum sizes and both monitor-crossing directions.
- Verify no movement starts during another item/layout operation, and a stationary pointer does not cause repeated placement or flicker.

No filesystem item moves are performed by geometry gestures.
