# CloudShelf repository instructions

Scope: this repository. Target: .NET 10, C#/.NET MAUI Android, EF Core/SQLite and xUnit v3.
Read `.github/copilot-memory.md` at the start of a session.

## Before and during work

- For substantial work, briefly state understanding and scope. Ask about material ambiguity;
  otherwise proceed within the user's authorization.
- Inspect actual files/types before editing. Follow `.editorconfig`; do not invent APIs.
- Prefer the smallest complete change and existing project boundaries. Use DI and explicit ownership.
- Keep UI-bound notifications/collections on the UI context. Await cancelled rendering before disposal.
- Use `string.Empty`, collection expressions and immutable DTOs where appropriate.
  Explain non-obvious reasons, avoid stale feature-spec headers, and document public contracts.
- Use structured ILogger messages (Serilog is configured at the composition root).
  Preserve useful errors; do not silently discard unexpected failures.
- Use short-lived DbContexts, checked-in migrations and the existing operation/file gates.
  Do not rewrite applied migrations or bypass identity boundaries.

## Architecture and tests

Follow [architecture](../Docs/Architecture.md) and [UI design](../Docs/UIDesign.md).
LibraryBrowser owns snapshot navigation; do not reintroduce a stale parent stack.
Keep sync/download presentation delegated and forward relevant state notifications.
Native Android APIs belong in Android-specific code.

Use meaningful xUnit behavior/failure regression tests. Mirror source responsibilities.
Run relevant compilation/tests and report actual executed counts/limitations.
See [development](../Docs/Development.md) for host scripts and source-only Android compilation.
Do not package APKs or run emulator/device validation unless the user explicitly authorizes it.
Feature development is paused until explicitly requested.

## Documentation and delivery

Maintain the five Markdown guides in `Docs/`; root README is the entry point.
Update the relevant guide instead of adding duplicate README stubs or step reports.
Current code and confirmed decisions supersede historical blueprints.
Keep release evidence separate from implementation claims; never invent a physical-device pass.

Use conventional commits on task branches. Review the diff, record source SHA/results/run links on
the PR, and merge validated work into `master` as requested. User instructions take precedence.
Preserve concise, useful decisions and corrections in copilot-memory.
