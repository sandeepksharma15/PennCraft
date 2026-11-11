# 02-upgrade-projects: Upgrade all projects to net11.0-windows

Update both projects from `net10.0-windows` to `net11.0-windows`. This is the core
upgrade task covering TFM changes, any needed code fixes for the 18 source-incompatible
API issues detected in the assessment, and a review of the 15 behavioral-change notices
(Api.0003) for significant runtime risk.

Affected projects:
- `src\PennCraft.Desktop\PennCraft.Desktop.csproj` — WPF application; 3,156 issues
  detected (3,130 binary-incompatible requiring recompilation, 12 source-incompatible
  requiring code edits, 13 behavioral notices). Technologies: WPF (1,363 issues),
  Windows Forms (226), GDI+/System.Drawing (12), WinForms Legacy Controls (2).
- `tests\PennCraft.Desktop.Tests\PennCraft.Desktop.Tests.csproj` — xUnit v3 test project;
  673 issues (664 binary-incompatible, 6 source-incompatible, 2 behavioral notices).
  Technologies: WPF (160 issues), GDI+/System.Drawing (6).

All 17 NuGet packages are compatible with net11.0 — no package version changes are
needed. The large binary-incompatible count (3,794) reflects WPF/WinForms APIs whose
signatures changed between .NET 10 and .NET 11; these resolve automatically on
recompilation once the TFM is updated. The 18 source-incompatible issues and 15
behavioral notices require investigation and targeted code fixes.

Research starting points: check `assessment/projects/` for per-file issue detail; use
`query_dotnet_assessment` filtered to Api.0002 (source-incompatible) and Api.0003
(behavioral) to get file + line context for each fix.

**Done when**: Both project files declare `net11.0-windows`; solution builds with 0
errors and 0 warnings; all existing xUnit tests pass; source-incompatible API issues
resolved; behavioral-change notices reviewed and any with confirmed runtime impact
addressed.
