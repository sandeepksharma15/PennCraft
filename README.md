# PennCraft

**Make room for everything.**

PennCraft is an open-source Windows desktop organizer. Group files, shortcuts and folders into **Pens**, or show a live folder through a **Folder Portal**.

<img src="src/PennCraft.Desktop/Assets/PennCraft.png" alt="PennCraft icon" width="96"/>

## Beta downloads

[Download v0.1.0-beta.1](https://github.com/sandeepksharma15/PennCraft/releases/tag/v0.1.0-beta.1) · [Beta guide](docs/beta-guide.md) · [Report a problem](https://github.com/sandeepksharma15/PennCraft/issues/new/choose)

Windows 11 x64. Choose the per-user installer or extract the full portable ZIP. Both include .NET. This unsigned beta may trigger Windows publisher/reputation warnings. Verify official downloads and checksums, and back up important files before testing.

## Features

- Create, rename, move, resize, fold and hide Pens.
- Move items between desktop, Pens and live Folder Portals.
- Use native Windows item commands.
- Align containers with grid snapping and automatic layout.
- Choose item size and opacity.
- Save/restore named layouts with Undo.

First launch asks before organizing your desktop. Transfers move real files into hidden desktop storage; layouts capture presentation rather than backing up files. Uninstall retains settings and managed files. See the [guide](docs/beta-guide.md) for backup and recovery.

## Status

Shell placement, Explorer restarts, virtual desktops, mixed-DPI monitors and third-party providers need wider manual testing. Tabs, multi-selection, workspace pages, monitor profiles and automatic updates are deferred. ARM64 and Windows 10 are not validated. Automated CI/package checks are distinct from interactive Windows 11 acceptance.

## Build and contribute

C# · .NET 10 · WPF · xUnit v3. Requires Windows and .NET 10 SDK.

```powershell
dotnet restore PennCraft.slnx
dotnet build PennCraft.slnx -c Release
dotnet test PennCraft.slnx -c Release
dotnet run --project src/PennCraft.Desktop
```

[Contributing](CONTRIBUTING.md) · [Releasing](docs/releasing.md) · [Architecture](docs/architecture.md) · [Roadmap](docs/roadmap.md)

Licensed under [MIT](LICENSE).
