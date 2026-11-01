# Copilot Memory

> **Purpose:** Persistent cross-session knowledge base. The AI reads this file at session start and appends learnings during work.
> **Rule:** Only add entries that would save time or prevent mistakes in future sessions. Keep entries concise (1–2 lines each).

---

## Corrections

<!-- User corrections to AI behavior or output. Format: `- YYYY-MM-DD: <what was wrong> → <what is correct>` -->

## Preferences

- 2026-11-01: Sync All traverses the whole library and is cancellable per folder transaction. Folder navigation respects auto-sync and lists immediate children only; missing folders remove their descendants and linked history.

- 2026-11-01: CI, emulator/device validation, APK packaging and releases are paused. Do not run or enable them unless specifically requested; workflows are manual-only.

- 2026-10-24: Automatic library/folder-open sync must enumerate only direct children, including first load. Persist an enabled-by-default Auto-sync folders toggle; manual Refresh and full Sync remain available. Startup recovery must not launch an invisible full sync. Keep unexplored folders visible and Library headers compact.

- 2026-10-24: Approved portrait tablet UI implementation: slate/teal, bottom Library/Downloads/Settings tabs, compact resume card, consistent outline SVG icons and quiet reader. No permanent rail; use honest placeholders. Revisit API 36 portrait opt-out before targeting API 37.

- 2026-10-24: Keep product/developer/UI documentation consolidated as Markdown in root `Docs/`. Do not recreate `Documentation`, `UI Design`, per-step reports or scattered README stubs; root README is the entry point.

- 2026-10-24: For the reported Sync/folder-tap fixes, run builds and host tests, then merge; do not package APKs or run emulator/device tests unless explicitly requested.

- 2026-10-23: Step 6 is release qualification only; do not build new features. Personal OneDrive; Galaxy S23 Ultra phone and Galaxy Tab S8+ tablet; approximately 5,000 PDF/EPUBs, largest around 50 MB. Both devices run Android 16 / One UI 8+; performance targets remain pending.

- 2026-10-20: After completing, validating, and reviewing work, resolve applicable review findings and merge its PR into master; do not stop at an open PR.

<!-- Explicit user preferences for code style, tooling, or workflow beyond what .editorconfig covers. -->

## Architecture Decisions

- 2026-11-01: EPUB screen pages are measured per chapter; persisted CurrentPage remains the legacy chapter index. Save chapter identity plus TextOffset (with old scroll fallback), and never persist viewport-dependent screen-page numbers. Publisher scripts stay blocked; only native-evaluated pagination runs after load.

- 2026-10-22: Step 3 policy: after completed enumeration, delete confirmed-missing books/progress, linked notes and downloaded copies; retain standalone notes/notebooks/tags. Failed physical cleanup retains tombstone/cache records for retry.

- 2026-10-22: Cloud identity includes provider/account/drive/root. Use persisted snapshot.Id across UI, downloads, and readers; legacy account linking is explicit and retains IDs.

<!-- Non-obvious technical decisions made during development. Include brief rationale. -->

## Patterns & Conventions

- 2026-11-01: In xUnit tests, always pass `TestContext.Current.CancellationToken` to APIs that accept a `CancellationToken` (for example `Task.Run`, `WaitAsync`, repository/service async methods) to satisfy xUnit1051 and keep cancellation responsive.

<!-- Recurring patterns discovered in the codebase that new code should follow. -->

## Gotchas

- 2026-11-01: `App_AutoSyncOnFolderOpen` must be accepted by `SqliteStateStore.LegacyKeys`; omitting it makes every automatic folder sync/settings read fail while manual sync succeeds. Cover preferences with the real SQLite store, not only unrestricted fakes.

- 2026-10-24: Settings must retain the UI synchronization context across auth/config/cache/preferences and command awaits. Worker completions plus native CanExecuteChanged handlers can interrupt account loading after the connection tick and destabilize native controls; exercise asynchronous fakes, not only completed tasks.

- 2026-10-24: User confirmed Sync/folder-tap fixes work. OneDrive account chooser's cached tile failed, but entering the same username manually worked; cause unverified. Track this separately from save-to-Home navigation.

<!-- Pitfalls, quirks, or non-obvious behaviors encountered in this workspace. -->
- 2026-10-20: MAUI XAML binding warnings from page commands can be fixed by setting x:DataType on the ContentPage to the concrete view model type; this lets the source generator resolve BindingContext.* command properties without reflection fallback.

- 2026-10-22: Preserve UTF-8 in scripted edits and verify XAML glyphs/text against Git before committing.

- 2026-10-23: ReaderPage and App share a singleton ReaderLifecycleCoordinator. Await cancelled rendering before disposal; hold a BookFileAccess lease while a native reader or EPUB WebView owns downloaded content. EPUB display acknowledgment includes the actual scroll offset before the first progress save, and same-chapter reloads must notify ContentBaseUrl even when its value is unchanged.

- 2026-10-23: Keep iterations fast: batch fixes, run Fast checks, and request full-validation only once the final native/storage/toolchain batch is ready. Remove that label before further iterations; reuse successful APK jobs for device-only retries. Do not rerun full Android validation automatically after merging.
- 2026-10-23: Persist source SHA/run link/current check in the PR so a disconnected session can resume without restarting CI. Always diagnose the first explicit harness error; emulator shutdown noise is not its cause. A Bash helper ending with a false negative string test returns 1 unless its success path is explicit.


