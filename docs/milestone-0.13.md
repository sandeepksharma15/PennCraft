# Milestone 0.13 — Fold and unfold

Double-click the header, including its title, to fold a Pen or Folder Portal into a compact title bar. Double-click again to expand. The hamburger menu also offers **Fold Pen / Unfold Pen**; use **Rename Pen** to edit the title. Header buttons and the title editor keep their own interactions.

Folded state survives hide/show and restart. Settings retain the expanded height rather than the temporary 36-DIP header height. Moving a folded Pen uses the existing screen grid. Resize grips return when expanded. Folding is disabled during geometry, layout and item operations. A failed settings write restores the previous fold state.

Automatic layout keeps folded containers folded, assigns the common width, and reserves compact header space rounded up to its grid. The expanded height stays unchanged. Undo is available while the participants retain their fold states from that arrangement; arrange again after changing fold states. Expanding in place can overlap a neighboring Pen; run automatic layout again to make space. Existing settings default to expanded. No automatic hover/inactivity folding or animation is included.

## Validation

Automated Windows tests cover Pens and Portals, expanded-size preservation, movement, hide/show, restart, automatic layout, busy-operation guards and settings-write failures.

Manual checks:
- Double-click blank header space and the title to fold/unfold; confirm buttons still open their menu or close.
- Move a folded header, expand, and confirm size, items, scroll position and folder connection.
- Rename through the menu while folded; verify Enter/Escape.
- Hide/show, restart, and try automatic layout and Undo with mixed folded/expanded containers.
- Check Win+D, application-window ordering and mixed-DPI monitors. CI windows do not establish interactive shell acceptance.
