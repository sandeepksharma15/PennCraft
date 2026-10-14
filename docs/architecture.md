# Architecture

## Principles

- Independent open-source utility; no Craft dependency.
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
