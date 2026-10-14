# Contributing

PennCraft is at the scaffold stage. Please discuss significant behavioral or architectural changes in an issue before implementing them.

- Keep changes small, focused, and independently testable.
- Preserve the `docs/`, `src/`, and `tests/` organization.
- Use idiomatic C#, nullable reference types, and minimal dependencies.
- Keep native shell operations isolated and document their failure modes.
- Run `dotnet build PennCraft.slnx` and `dotnet test PennCraft.slnx` on Windows before submitting a pull request.
- Include reproducible Windows validation steps for shell integration changes.

See [architecture](docs/architecture.md) and [roadmap](docs/roadmap.md).
