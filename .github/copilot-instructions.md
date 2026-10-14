# Copilot Instructions

> **Stack:** .NET 10 · C# latest · ASP.NET Core · Blazor · EF Core
> **Memory:** When `.github/copilot-memory.md` exists, read it before substantive work.

## 1. Core Behavior

* Follow the user's explicit requirements first, then existing project architecture and conventions, then these instructions.
* Before substantive changes, inspect relevant files, symbols, tests, configuration, and existing patterns. **Never invent** paths, APIs, namespaces, types, configuration keys, database fields, or dependencies.
* For non-trivial or ambiguous work, briefly state your understanding, assumptions, and any important questions before implementing.
* For clear, localized changes, proceed without unnecessary questions.
* If a materially better approach exists, briefly explain the trade-off before changing direction.
* Prefer the simplest solution that fits the existing architecture. Do not over-engineer.
* Preserve existing behavior, public contracts, and APIs unless a change is explicitly required.
* Keep changes focused. Do not perform unrelated refactoring or formatting cleanup.

## 2. Code & Architecture

* Follow `.editorconfig` and Roslyn analyzers as the authoritative source for style and formatting.
* Use modern C# features (primary constructors, collection expressions `[]`, pattern matching, record types) when they improve clarity.
* Prefer immutable types and `record` types for DTOs, messages, and value objects.
* Use async APIs end-to-end for I/O operations and propagate `CancellationToken` through method signatures. Never block on async code with `.Result` or `.Wait()`.
* Use dependency injection with explicit lifetime scoping (Transient/Scoped/Singleton).
* Use established project patterns before introducing new ones. Architecture patterns like Repository, Specification, Factory, Strategy, Builder, or MediatR are **not mandatory**—introduce them only when they provide a clear benefit.
* Do not add unnecessary abstractions, projects, NuGet packages, or infrastructure without justification.
* Prefer the newer C# extension member syntax (`extension<T>(...)`) for extension methods going forward when the language/features support it.
* Keep **Craft.Extensions** and **Craft.Utilities** as independent top-level libraries with no project dependency on each other, even though both are widely reused across the solution.
* Standardize tokenizer punctuation/operator token values to use `ExpressionOperators` constants for consistency when applicable.
* Ensure discoverable APIs across libraries, making functionality available automatically when a package reference is added to avoid remembering many namespaces.

## 3. Configuration & Security

* Use strongly typed configuration with `IOptions<T>`, `IOptionsSnapshot<T>`, or `IOptionsMonitor<T>` as appropriate.
* Validate configuration at application startup using `ValidateDataAnnotations()` or custom validation.
* Never hardcode or commit secrets, credentials, tokens, connection strings, or sensitive configuration.
* Never log secrets, tokens, passwords, or sensitive personal identifiable information (PII).
* Treat external and user input as untrusted; enforce server-side validation and authorization.

## 4. Logging & Errors

* Use structured logging with named parameters through `ILogger<T>` (e.g., `_logger.LogInformation("Processing order {OrderId}", orderId)`). Default provider: **Serilog**.
* Use explicit result abstractions (e.g., `Result<T>`) for expected domain/business rule failures.
* Use exceptions exclusively for unexpected or unrecoverable infrastructure/runtime failures. Never swallow exceptions silently.
* Do not expose internal exception stack traces or raw infrastructure error details through public API endpoints.

## 5. Blazor

* Use **MudBlazor** as the primary UI component library where adopted.
* Align with the .NET unified Blazor render modes (`@rendermode InteractiveServer`, `InteractiveWebAssembly`, or `InteractiveAuto`).
* Keep Razor components focused on UI presentation, user interaction, and layout state; delegate business logic to dedicated scoped services.
* Prefer CSS isolation (`.razor.css`) for component-specific styles over inline styling.
* Handle component disposal (`IAsyncDisposable` / `IDisposable`), cancellation tokens, event subscriptions, timers, and JS Interop carefully to prevent memory leaks and thread safety issues.

## 6. EF Core

* Follow the project's established data-access architecture.
* Use EF Core Migrations for schema modifications.
* Keep filtering, sorting, projection, and pagination database-side (`IQueryable`).
* Use `AsNoTracking()` or `AsNoTrackingWithIdentityResolution()` for read-only queries.
* Prefer LINQ projection (`Select`) over fetching full entities when only a subset of fields is required.
* Use bulk operations (`ExecuteUpdateAsync` / `ExecuteDeleteAsync`) for mass modifications where entity tracking is not needed.
* Avoid N+1 queries, unnecessary `.Include()` chains, and premature materialization (`.ToList()`, `.ToArray()`).
* Always pass `CancellationToken` to EF Core async operations.

## 7. Testing

> **Stack:** xUnit · Moq · FluentAssertions / Shouldly

* Add or update unit/integration tests when modifying behavior.
* Structure test cases using the **Arrange → Act → Assert** pattern.
* Follow standard naming conventions: `MethodName_StateUnderTest_ExpectedBehavior`.
* Use `[Theory]` with `[InlineData]` or `[MemberData]` for parameterized test coverage.
* Use `WebApplicationFactory<TEntryPoint>` for ASP.NET Core integration tests.
* Validate boundary conditions, edge cases, and failure modes—not just happy paths.

## 8. Validation

For substantive code changes perform the following steps:

1. Build the affected project/solution.
2. Run relevant tests.
3. Fix any compiler errors, warnings, or broken tests introduced by the changes.
4. Review the final diff to check for unintended or unrelated modifications.

Never claim a build or test run succeeded unless it was executed. State clearly if validation could not be performed in the current environment.

## 9. Project Memory

When `.github/copilot-memory.md` exists:

* Read it before starting substantive work.
* Update it when durable architectural decisions, recurring constraints, or project conventions are established.
* Do not record temporary debugging logs, task progress, secrets, or transient details.
* Keep the memory clean, concise, and structured.

## 10. Git & Commit Style

* **Default branches:** `master`, `dev`, plus short-lived feature branches.
* **Commit format:** Follow Conventional Commits (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`).
* Do not generate or execute Git commits unless explicitly requested by the user.

## 11. Task Completion Summary

At the end of non-trivial tasks, provide a brief summary covering:

* What was changed or implemented
* Key architectural or design decisions
* Validation/testing results
* Any known limitations, assumptions, or suggested follow-up tasks

**Guiding Principle:** Inspect first. Understand the existing architecture. Make the smallest correct change. Validate it. Do not invent what you have not verified.