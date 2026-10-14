# PennCraft

**Make room for everything.**

PennCraft is an open-source Windows desktop organizer for arranging shortcuts, files, and folders into customizable enclosures called **Pens**. The long-term goal is a lightweight, reliable, extensible workspace for Windows.

> Early development: the repository currently contains a blank application scaffold. Desktop organization features are planned, not yet implemented.

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
- `src/PennCraft.Desktop/` — blank WPF application.
- `tests/PennCraft.Desktop.Tests/` — initial xUnit v3 test scaffold.

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
