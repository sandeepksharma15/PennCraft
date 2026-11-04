# Architecture

## Principles

- Independent open-source utility.
- Native Windows experience with minimal idle resource use.
- Keep Explorer and desktop files untouched by default.
- Favor documented Windows APIs; treat undocumented shell techniques as experiments.
- Separate UI, application behavior, domain concepts, and Windows integration.
- Add dependencies and assemblies only when useful; test behavior before abstraction.

## Initial technology

- C# / .NET 10 LTS; WPF for Windows desktop UI.
- Win32 interop and Shell COM only where necessary.
- xUnit v3 for automated tests.
- JSON persistence initially; consider SQLite only if requirements justify it.
- Standard .NET dependency injection and logging when services are introduced.

## Evolution

Start with `PennCraft.Desktop` and its test project. As real code warrants separation, introduce independently testable Core, Application, Windows integration, and Infrastructure projects. Keep domain and rules logic independent of WPF.

Vocabulary: **Pen** (container), **Portal** (folder-backed view), **Workspace** (collection of Pens), **Layout** (saved arrangement), **Rule** (automatic classification).

## Desktop integration risks

A desktop-level surface must coexist with Explorer icons and handle z-order, hit testing, file drag-and-drop, Explorer restarts, DPI, multiple monitors, and virtual desktops. Evaluate overlay placement experimentally before committing to it. WorkerW/Progman-based techniques are not stable documented contracts and must not be assumed reliable.

Keep visual organization separate from physical file moves. Persist changes safely and make recovery a first-class concern.

## Verification

Unit test pure behavior and persistence; integration-test Windows boundaries; manually exercise Explorer restart, display topology, DPI changes, drag-and-drop, and accessibility on supported Windows versions. Never infer shell reliability solely from mocks.

## Pen foundation

The WPF window owns presentation and UI event handling. PenSettings is immutable user intent; PenSettingsStore normalizes and atomically replaces a JSON snapshot. PenGeometry implements resize constraints independently of WPF. TrayCommands owns and disposes the notification icon and dispatches commands to WPF.

DesktopSurface isolates native HWND styles, shell rediscovery, visibility and bottom z-order. Temporary native visibility never changes persisted user intent. Explicit rename briefly permits keyboard activation; ordinary dragging and resizing retain no-activate behavior. All shell operations target the Pen HWND; Explorer is never reparented, injected into or modified. See [usage, tradeoffs and acceptance](milestone-0.2.md).

## Desktop items

PenMembership owns ordered, normalized, case-insensitive path references and never performs filesystem mutations. PenItem presents labels and frozen icons; the MainWindow item partial binds an ObservableCollection and orchestrates explicit actions. ItemPaths extends the existing JSON settings without a migration requirement.

Windows adapters own native shell icons, launching and read-only desktop hit testing. Icons use a serial STA worker and release HICON handles after creating frozen bitmaps. Incoming drags are reference-only Copy/Link operations. Outgoing drags carry a private membership format; desktop release removes the reference locally without handing files to Explorer. See [semantics and acceptance](milestone-0.3.md).

## Physical item moves

PenMoveService owns the desktop/storage boundary and startup reconciliation. NativeFileMove isolates native identity queries and same-volume no-overwrite rename. A private drag payload lets PennCraft own the journaled move; Explorer never receives a second file-move instruction. External/legacy references retain their earlier semantics.

Move intent is flushed before rename. The destination identity is verified before membership changes, settings are flushed, then the intent is acknowledged. Recovery is idempotent and never replays a physical move. Ambiguous/corrupt records are retained and block physical moves. AtomicJsonFile supplies complete, flushed snapshot replacement for settings and the journal. One application instance owns the per-user settings lock; UI actions serialize worker operations and prevent exit in flight. See [storage and recovery](milestone-0.4.md).

## Workspace and desktop creation

WorkspaceController owns all Pen windows, a single tray icon, the transient CreationPreview and DesktopCreationGesture. WorkspaceStore indexes stable Pen IDs; each window keeps its own PenSettingsStore and PenMoveService. Default paths are retained for migration, and new IDs are validated before constructing paths.

The observer runs on a dedicated message-pump thread and always calls the next input observer. DesktopIconSnapshot runs on a separate MTA thread. CreationGesturePolicy checks physical rectangles against icons and visible windows. No Explorer state is changed, and uncertain/stale geometry fails closed. CreationPreview writes no state; only its enabled creation action commits a new ID/settings pair. See [behavior and boundaries](milestone-0.5.md).

## Item context menus

ShellContextMenu owns shell PIDLs, IContextMenu interfaces, HMENU lifetime and temporary message forwarding on the owning STA. Native commands remain shell-owned; rename uses a host dialog plus the shared no-overwrite rename gate. MainWindow.ItemMenus routes item versus background input and the storage-aware membership actions.

TrackedItem extends settings with stable file identities. ItemTracking reconciles only the original parent folders and performs no filesystem mutations. Watchers debounce onto the WPF dispatcher; metadata scans run on workers. Snapshots are saved before shell changes so startup can reconcile interrupted membership updates. Unknown/incomplete/ambiguous identities are retained. Existing physical move journals take priority over shell actions. See [semantics and validation](milestone-0.6.md).

## Transfers between Pens

PenTransferCoordinator reserves the source and destination windows and routes only current in-process PenDragRegistry tokens. DesktopItemDrag retains desktop release semantics while allowing valid private Pen drops. MainWindow.Transfers merges committed membership into live window metadata and suspends watcher/settings races during a transfer.

PenTransferService owns a single workspace write-ahead intent, validates Pen IDs and storage boundaries, preflights both stores, renames without overwrite/copy fallback, verifies identity, saves both memberships and acknowledges last. Startup reconciliation runs before windows/watchers. Physical moves are not replayed; reference intents are completed logically. Ambiguous/corrupt records fail closed without filesystem mutation. See [transaction and recovery](milestone-0.7.md).
