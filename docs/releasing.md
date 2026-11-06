# Releasing PennCraft

Version.props is the version source. Packages bundle Windows x64 .NET without trimming. Runtime notices and the MIT license are included. BUILDINFO.json records the exact source and runtime versions.

PR/master CI runs NuGet audit, build/tests, self-contained publish, isolated app/portable smoke tests, installer compilation, install/reinstall/uninstall checks and SHA-256 generation. Smoke mode bypasses normal startup and uses temporary desktop/settings roots.

Actions are pinned. Package building has read-only repository permissions. Only deliberate publication has contents-write permission. No long-lived token or signing certificate is required for this unsigned beta.

## Prepare

1. Update Version.props and create matching docs/releases/vVERSION.md. Keep numeric file/assembly versions consistent.
2. Update README download links, limitations and the beta guide. Review .NET updates: bundled runtimes need new releases to receive security fixes.
3. Use a feature branch, pass CI, review packages, and complete manual Windows 11 acceptance for claimed behavior.
4. Merge after checks pass and verify master CI.

## Publish

Creating **publish/vVERSION** from reviewed master is the deliberate publication trigger. Create publish/v0.1.0-beta.1 only when ready to publish that beta.

Alternatively, run **Publish beta** from master: publish=false builds/verifies; publish=true publishes. The workflow requires a beta/rc version, a matching publication branch when applicable, and a source commit on master.

It rebuilds/verifies, creates a draft prerelease, uploads installer/ZIP/checksums/BUILDINFO, compares GitHub asset digests and sizes, then publishes. It verifies the new tag against the source commit. Existing tags/releases are never overwritten.

If verification fails, inspect the unpublished draft. Never replace a public release's bits or move its tag. Review/remove only an unpublished failed draft before retrying; bump the beta version for changed public bits.

## Manual acceptance

Use an isolated Windows 11 account/VM with backed-up samples:
- Accept, decline and cancel first-run organization in separate fresh profiles.
- Check the tray mark on light/dark taskbars and at 100%, 150%, 200%.
- Exercise transfers, Portals, native menus, Win+D, Explorer restart, folding, saved layouts and mixed monitors.
- Check running-app update/uninstall prompts without interrupting an active move.
- Update an existing workspace and uninstall; verify settings/storage remain.

## Local packaging

From a Git checkout on Windows with .NET 10 SDK and Inno Setup 6:
```powershell
dotnet restore PennCraft.slnx
dotnet build PennCraft.slnx -c Release --no-restore
dotnet test PennCraft.slnx -c Release --no-build
./build/Build-Beta.ps1 -OutputDirectory C:/Temp/PennCraft-beta-output
```

Use an empty output directory. Test-Installer.ps1 runs only on disposable GitHub Actions runners because it installs/uninstalls software and checks a per-user settings sentinel.

The SVG and Windows renderer live in src/PennCraft.Desktop/Assets and build/New-AppIcon.ps1. Regenerate ICO/PNG together after changing the vector mark.

Sources: [.NET deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/), [WPF trimming limits](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities), [installer running-app guard](https://jrsoftware.org/ishelp/topic_setup_appmutex.htm).
