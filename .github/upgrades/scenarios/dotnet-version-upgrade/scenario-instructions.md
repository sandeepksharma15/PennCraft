# .NET Version Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: net11.0

## Upgrade Options

| Option | Selected | Why |
|--------|----------|-----|
| Upgrade Strategy | All-at-Once | 2 projects, all on modern .NET, simple TFM bump |

## Strategy
**Selected**: All-at-Once
**Rationale**: 2 projects, both already on net10.0-windows; straightforward TFM bump with no incompatible packages; incremental approach adds overhead without benefit.

### Execution Constraints
- Single atomic upgrade — all projects updated together in one pass
- Operation sequence: update TFMs → restore → build and fix all compilation errors
- Validate full solution build (0 errors) before completing the task
- Run test suite after build succeeds
- One bounded fix pass, not a retry loop

## Source Control
- **Source Branch**: master
- **Working Branch**: upgrade-dotnet-11
- **Commit Strategy**: After Each Task
- **Branch Sync**: Auto (Merge)
