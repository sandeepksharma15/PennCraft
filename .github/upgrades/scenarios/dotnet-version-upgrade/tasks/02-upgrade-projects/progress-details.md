# Progress Details — 02-upgrade-projects

## Changes Made

### Project files
- `src\PennCraft.Desktop\PennCraft.Desktop.csproj`: `net10.0-windows` → `net11.0-windows`
- `tests\PennCraft.Desktop.Tests\PennCraft.Desktop.Tests.csproj`: `net10.0-windows` → `net11.0-windows`

### WFO2000 — New Windows Forms analyzer error in .NET 11
Converted 5 getter properties that returned newly constructed instances to factory methods — the analyzer's recommended fix:
- `PenDeletionTests.cs`: `Store` property → `Store()` method (1 declaration, 7 callers)
- `PortalMoveTests.cs`: `PenStore`, `PortalStore` → methods (2 declarations, 6 callers)
- `PenTransferTests.cs`: `SourceStore`, `TargetStore` → methods (2 declarations, 25 callers)

### WPF DPI sub-pixel precision change in .NET 11
WPF .NET 11 returns `window.Height`/`window.Left` with higher DPI-aware precision
(e.g., `36.226...` instead of `36.0`). Fixed 7 assertions across 2 test files:
- `PenFoldingTests.cs`: 6 assertions → `Assert.Equal(..., precision: 0)`
- `PenViewTests.cs`: 1 assertion → `Assert.Equal(..., precision: 0)`

### Packages
No package changes required — all 17 NuGet packages confirmed compatible with net11.0.

### Behavioral notices (Api.0003) — advisory, no action needed
`JsonDocument`, `Environment.OSVersion`, `Environment.TickCount64`, `Uri.TryCreate` —
all reviewed; no code impact found. Existing code continues to function correctly.

## Build Result
```
Build succeeded.
  0 Warning(s)
  0 Error(s)
```

## Test Result
```
Test run summary: Passed!
  total: 282 | failed: 0 | succeeded: 282 | skipped: 0
  duration: 5s 567ms
```

## Self-Review
Scope matches plan exactly: both project TFMs updated, all source-incompatible issues
resolved (System.Drawing.Icon APIs compiled without change — assessment was conservative),
behavioral notices reviewed and confirmed advisory only. WFO2000 and DPI-precision test
failures fixed without suppressing warnings. Build: 0 errors, 0 warnings. All 282 tests pass. ✅
