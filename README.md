# PennCraft

**Make room for everything.**

PennCraft is an open-source Windows desktop organizer for arranging shortcuts, files, and folders into customizable enclosures called **Pens**. The long-term goal is a lightweight, reliable, extensible workspace for Windows.

> Early development: Milestone 0.7 adds direct item transfers between Pens, with a workspace journal coordinating file location and both memberships. See [usage and Windows acceptance checks](docs/milestone-0.7.md).

## Planned capabilities

- Create, move, resize, and personalize Pens.
- Preserve layouts across sessions and display changes.
- Show live folder contents through Portals.
- Organize items automatically using configurable Rules.
- Fold Pens, navigate workspaces, and save layouts.

## Technology

C# · .NET 10 · WPF · Windows 11 · xUnit v3.

## Repository

- `docs/` — [architecture](docs/architecture.md), [features](docs/features.md), and [roadmap](docs/roadmap.md).
- `src/PennCraft.Desktop/` — WPF Pen UI, item membership and isolated Windows integration.
- `tests/PennCraft.Desktop.Tests/` — xUnit v3 membership, persistence, geometry and native Windows tests.

## Build

Requires Windows, .NET 10 SDK, and the Windows Desktop targeting pack.

```powershell
dotnet restore PennCraft.slnx
dotnet build PennCraft.slnx
dotnet test PennCraft.slnx
dotnet run --project src/PennCraft.Desktop
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Windows shell integration remains experimental; CI does not replace interactive desktop acceptance.

Licensed under [MIT](LICENSE).
