# Milestone 0.8 — basic Folder Portals

Draw a rectangle on exposed desktop space, choose **Create a Folder Portal here**, and select a folder. Cancelling the picker creates nothing. The Portal displays immediate non-hidden, non-system children using native icons in the existing responsive, scrollable grid. Double-click opens a file with its registered application or a subfolder in Explorer. Right-click offers Open and Show in File Explorer only.

The linked folder, title, bounds, opacity and visibility are saved with the existing workspace. Closing hides the Portal; show it again from the tray. The header retains existing rename, move, resize and opacity controls. Open linked folder is available in its menu.

Contents are transient views, never persisted as Pen membership. No Pen storage is created for Portals. Incoming/outgoing drags, Pen transfers, rename and delete commands are disabled for Portal items in this milestone. Opening a program or file can of course change files through the launched application.

An asynchronous scan every two seconds reflects external additions, renames and removals. Only one scan per Portal runs at a time. Unavailable/inaccessible folders show a status and retain the connection, retrying automatically. Partial enumeration failures show unavailable rather than an incomplete list.

## Limits and validation

Polling may be delayed by slow network/cloud providers. Large directories are not virtualized; native icon loading and enumeration may take time. Folder connections are path-based: moving/renaming the linked folder does not retarget it automatically. No recursive browsing, provider hydration guarantees, file operation UI or folder retarget UI is included.

Automated tests cover immediate-only listing, external rename/deletion, file bytes, hidden filtering, unavailable/reappearing folders, invalid relative paths and settings round trips. Existing Windows CI also builds WPF and runs Pen regression tests.

Manual acceptance remains required: create/cancel through the desktop menu; verify icons, scrolling and double-click; external changes; unavailable/reconnected folders; restart with hidden/visible Portals; tray unhide; Win+D, Explorer restart and mixed-DPI positioning; and confirm drags into/out of Portals do nothing while ordinary Pens still transfer items.
