# 01-prerequisites: Verify SDK and global.json compatibility

Verify that the .NET 11 SDK is installed and that any `global.json` file in the
repository is compatible with the target SDK version. This is a lightweight gate that
prevents obscure SDK mismatch errors during the upgrade.

The repo contains 2 projects: `src\PennCraft.Desktop` (WPF app) and
`tests\PennCraft.Desktop.Tests` (xUnit v3 test project). Both currently target
`net10.0-windows`.

**Done when**: .NET 11 SDK is confirmed installed; `global.json` (if present) does
not pin a SDK version that is incompatible with net11.0; no blockers found.
