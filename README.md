# PennCraft

**Make room for everything.**

PennCraft is an open-source Windows desktop organizer for arranging shortcuts, files, and folders into customizable enclosures called **Pens**. The long-term goal is a lightweight, reliable, extensible workspace for Windows.

> Early development: Milestone 0.1 provides one movable, resizable desktop-focus Pen with diagnostics and best-effort Explorer rediscovery. Windows 11 shell acceptance is pending; organization features remain planned. See the [prototype and manual verification checklist](docs/milestone-0.1.md).

## Planned capabilities

- Create, move, resize, and personalize Pens.
- Preserve layouts across sessions and display changes.
- Show live folder contents through Portals.
- Organize items automatically using configurable Rules.
- Fold Pens, navigate workspaces, and save layouts.

## Technology

C# · .NET 10 · WPF · Windows 11 · xUnit v3. No dependency on Craft.

## Repository

- `docs/` — [architecture](docs/architecture.md), [features](docs/features.md), and [roadmap](docs/roadmap.md).
- `src/PennCraft.Desktop/` — WPF Pen prototype and isolated Windows integration.
- `tests/PennCraft.Desktop.Tests/` — xUnit v3 policy and native HWND tests.

## Build

Requires Windows, .NET 10 SDK, and the Windows Desktop targeting pack.

```powershell
dotnet restore PennCraft.slnx
dotnet build PennCraft.slnx
dotnet test PennCraft.slnx
dotnet run --project src/PennCraft.Desktop
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). The first milestone is a desktop-integration feasibility prototype, not a production-ready shell extension.

Licensed under [MIT](LICENSE).
