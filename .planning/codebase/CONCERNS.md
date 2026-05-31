# Codebase Concerns

**Analysis Date:** 2026-05-28

## Tech Debt

**AppState god-object:**
- Issue: `AppState.swift` is a 4,507-line `@MainActor` class with 97 `@Published` properties and 107 functions. It handles vault lifecycle, note loading, search, tasks, citations, embeds, math, code, diagrams, command palette, preferences, and accessibility announcements in a single file.
- Files: `apps/slate-mac/Sources/SlateMac/AppState.swift`
- Impact: High cognitive overhead for every change. Incremental feature work requires understanding all 97 published state interactions. Compilation times will grow. Test isolation is impossible without the full class.
- Fix approach: Extract domain-specific view models (e.g., `SearchViewModel`, `CitationsViewModel`, `TasksViewModel`) that `AppState` composes. Each would own its `@Published` properties and async tasks. This is a large refactor; do it domain by domain rather than all at once.

**`parse_workers` configuration field never honored:**
- Issue: `SessionConfig::parse_workers` exists and is documented as "0 means auto," but `scan_initial_with_progress` at `crates/slate-core/src/session.rs:554` passes it directly to the sequential `scan_vault` function, which ignores it. The comment at line 51 says "Not yet honored by the (sequential) Milestone A scanner."
- Files: `crates/slate-core/src/session.rs`
- Impact: Large vaults (>5,000 files) are scanned on a single thread. Initial indexing of a large vault will be noticeably slow. The config knob misleads future callers into thinking parallelism is enabled.
- Fix approach: Implement a parallel scan in `scan_vault` using rayon or a bounded thread pool; honor `parse_workers` count.

**Op-log compaction never runs:**
- Issue: `SessionConfig` has `oplog_compaction_threshold_entries`, `oplog_compaction_threshold_bytes`, and `oplog_retention_days` fields (defaulting to 10,000 entries / 5 MB / 90 days), but the compaction logic is documented as "V1.F feature; reserved" and is never called. Every save appends a full file snapshot (`WholeFileReplace`) to `<cache_dir>/oplog/<file_id>.oplog` with no size bound.
- Files: `crates/slate-core/src/oplog.rs`, `crates/slate-core/src/session.rs:58-63`
- Impact: Heavy note-editors will accumulate unbounded oplog files. A user who saves a 500 KB note 10,000 times will have a 5 GB oplog file. The disk impact grows silently — no UI warning exists.
- Fix approach: Implement compaction in `oplog.rs` triggered after `append_entry` when either threshold is exceeded. Compaction should drop entries older than `oplog_retention_days` and/or truncate to the most-recent N entries.

**File system watcher not implemented:**
- Issue: `VaultProvider::watch` at `crates/slate-core/src/vault/provider.rs:63` returns `Ok(None)` in all cases. The comment at line 159-160 says "Reserved for the real watcher implementation (V1.A ships with `watch` returning `Ok(None)`)." The bibliography module at `crates/slate-core/src/citations/bibliography.rs:12-18` explicitly notes the watcher hasn't landed.
- Files: `crates/slate-core/src/vault/provider.rs`, `crates/slate-core/src/vault/fs.rs`
- Impact: External edits to vault files (from another editor, git pull, sync service) are not reflected until the user manually triggers a rescan or restarts the app. The search index goes stale silently.
- Fix approach: Implement `FsVaultProvider::watch` using `notify` crate (kqueue on macOS/Linux, ReadDirectoryChangesW on Windows), dispatch incremental scan on change events.

**Localization infrastructure absent (issue #264):**
- Issue: The entire UI uses string literals directly, with no `.xcstrings` catalogue or `String(localized:)` calls. The comment at `apps/slate-mac/Sources/SlateMac/SettingsView.swift:160-161` explicitly defers this to "i18n infrastructure" work.
- Files: All `.swift` files under `apps/slate-mac/Sources/SlateMac/`
- Impact: The app cannot be localized or internationalised without touching every display string. Accessibility labels are also hard-coded English.
- Fix approach: Introduce an `.xcstrings` file; migrate display strings to `String(localized:)` progressively, starting with error messages and accessibility labels.

**`large_file_warn_bytes` and `large_file_confirm_bytes` thresholds unused:**
- Issue: `SessionConfig` defines warn (5 MB) and confirm (10 MB) thresholds, but neither is read by any code path in `session.rs` or the Swift layer. Only `large_file_refuse_bytes` (50 MB) is enforced.
- Files: `crates/slate-core/src/session.rs:65-66`
- Impact: Users who open files between 5-50 MB get no warning and no confirmation prompt — they may experience UI lag or long indexing pauses without explanation.
- Fix approach: Wire the warn/confirm thresholds into `read_text` and the scanner; surface them to Swift via FFI so the UI can present a confirmation sheet.

**Outgoing links query is unbounded:**
- Issue: `crates/slate-core/src/links_db.rs:outgoing_links_for` has no `LIMIT` clause. A note with thousands of links returns all rows in a single query. In contrast, the `backlinks` query is paginated with `limit: 200` from the Swift call site.
- Files: `crates/slate-core/src/links_db.rs:170-199`
- Impact: A note intentionally using wikilinks as a map/index (common in Zettelkasten vaults) could have thousands of outgoing links, making the panel load slow and memory usage unpredictable.
- Fix approach: Add cursor-based pagination to `outgoing_links_for` matching the `backlinks_for` shape.

**`ResolvedBlock` dead-code accumulation:**
- Issue: `crates/slate-core/src/blocks_db.rs:103` has `#[allow(dead_code)]` on `ResolvedBlock` with fields `kind`, `line_start`, `line_end` that "aren't read by the resolver today but are kept so a future affordance has the metadata."
- Files: `crates/slate-core/src/blocks_db.rs`
- Impact: Minor; accumulating dead fields adds maintenance noise and could confuse future contributors about what the resolver actually uses.
- Fix approach: Remove unused fields now; add them back with a concrete feature PR.

**`tree_sitter_cache_size` configuration field never used:**
- Issue: `SessionConfig::tree_sitter_cache_size` (line 57 in `session.rs`) is populated in config but no LRU cache for tree-sitter parse trees exists in the codebase. Every code-block parse allocates fresh parser state.
- Files: `crates/slate-core/src/session.rs:57`, `crates/slate-core/src/code.rs`
- Impact: For large vaults or frequent file-switching, tree-sitter parsers are re-initialized repeatedly — an avoidable allocation on every `get_code_blocks` call.
- Fix approach: Implement an LRU-keyed parser pool (`file_id → parse tree`) bounded by the config field.

**Indentation inconsistency in AppState `handleSelectionChange`:**
- Issue: At `apps/slate-mac/Sources/SlateMac/AppState.swift:1280`, `currentNoteCitationRefs = []` is indented at the base column inside a block that indents four spaces. This suggests a copy-paste or merge error — the line is syntactically correct but visually misaligned with its siblings.
- Files: `apps/slate-mac/Sources/SlateMac/AppState.swift:1280`
- Impact: Low (compile-time only). However, it signals AppState's size makes manual review error-prone.
- Fix approach: Correct indentation. Low priority.

## Known Bugs

**Mermaid renderer permanent failure after one panic:**
- Symptoms: Once any Mermaid diagram causes a panic inside `mermaid-rs-renderer` (which holds a process-global `Mutex`), all subsequent diagram renders for the entire session surface as `RenderFailed` with the message "mermaid renderer is unavailable for the rest of this session." There is no recovery path short of restarting the app.
- Files: `crates/slate-core/src/diagram.rs:241-326`
- Trigger: Any malformed Mermaid input that triggers a panic in `mermaid-rs-renderer 0.2`.
- Workaround: App restart. The `RENDERER_POISONED` static flag is read at every render entry.

**Task due-date filter uses UTC midnight, not user's local midnight:**
- Symptoms: "Due today" and "Overdue" task filters change at UTC midnight regardless of the user's timezone. A user in UTC-8 will see today's tasks shift at 4 PM local (summer) or 5 PM local (winter).
- Files: `apps/slate-mac/Sources/SlateMac/AppState.swift:43-127` (the `TaskReviewFilter` doc comment acknowledges this explicitly)
- Trigger: Any timezone offset from UTC. Affects `dueToday`, `overdue`, `thisWeek` filters.
- Workaround: None. Documented as a V1 limitation.

**Oplog entries silently lost on append failure:**
- Symptoms: If `oplog::append_entry` fails (disk full, permissions error), the failure is printed to `stderr` via `eprintln!` and swallowed — the save still succeeds.
- Files: `crates/slate-core/src/session.rs:829-830`
- Trigger: Disk-full or permission error on the `.slate/oplog/` directory.
- Workaround: None. The `save_text` call returns `Ok(())` regardless. History is silently lost.

## Security Considerations

**Symlink escape: `O_NOFOLLOW` not enforced on non-Unix platforms:**
- Risk: The `open_nofollow` helper at `crates/slate-core/src/vault/fs.rs:347-359` uses `O_NOFOLLOW` via `libc` on Unix. The `#[cfg(not(unix))]` fallback uses plain `File::open`, which follows symlinks. If Slate ever ships to Windows, the TOCTOU symlink-escape defence (canonicalize → verify in-scope → open final component) would have a race window on the open step.
- Files: `crates/slate-core/src/vault/fs.rs:356-359`
- Current mitigation: Slate currently ships macOS/Linux only, so `O_NOFOLLOW` is always active.
- Recommendations: If Windows support lands, implement `openat2(RESOLVE_NO_SYMLINKS)` or equivalent Win32 path.

**`user_actor_id` permanently hardcoded to `"local"`:**
- Risk: The op-log's `actor_id` field is always `"local"`. In a multi-device sync scenario (V2), op-log entries from different devices would be indistinguishable.
- Files: `crates/slate-core/src/session.rs:119`
- Current mitigation: V1 is single-device only.
- Recommendations: Generate or persist a per-device UUID at vault-open time before V2 sync work begins.

**External URL launch is unvalidated:**
- Risk: `AppState.openLink(_:)` passes any `http://` or `https://` URL directly to `NSWorkspace.shared.open`. There is no allowlist or user confirmation prompt. A malicious note file could contain a link to a locally-served web exploit or a custom URL scheme.
- Files: `apps/slate-mac/Sources/SlateMac/AppState.swift:1471-1482`
- Current mitigation: The comment at line 1482 mentions `NSWorkspace.open` limitations, but no prompt is shown.
- Recommendations: Add a confirmation sheet for external link launches, or restrict to `http`/`https`/`mailto` only with scheme validation.

## Performance Bottlenecks

**Initial scan holds the SQLite mutex for entire duration:**
- Problem: `scan_initial_with_progress` at `crates/slate-core/src/session.rs:559` acquires `self.conn.lock()` and never releases it until the entire vault is scanned. Any concurrent Swift `Task` trying to call `get_file_metadata`, `list_files`, `outgoing_links`, etc. blocks for the full duration.
- Files: `crates/slate-core/src/session.rs:554-568`
- Cause: Single `Mutex<Connection>` architecture; scan commits per-file but holds the connection lock throughout.
- Improvement path: Release the lock between file batches, or switch to WAL mode's reader/writer separation. The current SQLite config already enables WAL (`crates/slate-core/src/db.rs:131`), but the single-connection mutex serializes all access regardless.

**MathCAT routes all renders through a single dedicated worker thread:**
- Problem: All `get_math_blocks` calls serialize through one channel to the MathCAT worker (`crates/slate-core/src/math.rs`). Multiple rapid file switches or a note with many math blocks queue behind each other on a single thread.
- Files: `crates/slate-core/src/math.rs:302-317`
- Cause: MathCAT's thread-local state forces single-threaded access; the worker design is correct but creates a serialization bottleneck.
- Improvement path: Investigate if MathCAT 0.8+ exposes re-entrant rendering. If not, multiple worker threads (each with their own initialized MathCAT context) would parallelize renders at the cost of more memory.

**FTS5 full-text search has no result count cap:**
- Problem: `search_db::full_text_search` at `crates/slate-core/src/search_db.rs:94` returns all matching rows. A broad query on a large vault (e.g., single letter) could return tens of thousands of hits with snippet computation.
- Files: `crates/slate-core/src/search_db.rs:94`
- Cause: No `LIMIT` clause on the FTS5 query.
- Improvement path: Add a configurable max-results cap (e.g., 500); surface a "too many results, refine your query" state to the UI.

## Fragile Areas

**Drift tests scrape Swift source via regex:**
- Files: `apps/slate-mac/Tests/SlateMacTests/CloseVaultSheetParityTests.swift`, `apps/slate-mac/Tests/SlateMacTests/SlateCommandsTests.swift`
- Why fragile: Both drift tests locate function bodies (e.g., `closeVault()`) in `AppState.swift` using brace-counting over comment/string-stripped source. The brace-counter was fixed in commit `d19cd93` but the approach remains inherently brittle. Renaming the function, adding a nested closure, or changing indentation can silently break the locator and cause the drift test to scan the wrong substring — or fail with a misleading diagnostic.
- Safe modification: When adding new sheet-state `@Published` vars to `AppState`, always add the corresponding `closeVault()` reset and run `CloseVaultSheetParityTests` locally before committing.
- Test coverage: Covered only by the drift test itself; no independent unit test exercises the brace extractor in isolation.

**`@unchecked Sendable` on FFI bridge types:**
- Files: `apps/slate-mac/Sources/SlateMac/ScanProgressAdapter.swift:24`, `apps/slate-mac/Sources/SlateMac/SlateCommands.swift:82`
- Why fragile: `ScanProgressAdapter` and `MenuCommandAction` use `@unchecked Sendable` because the underlying Rust `ScanProgressListener` and `CommandAction` traits are not verifiably Swift-`Sendable`. This suppresses Swift 6 concurrency checking for these types. Incorrect concurrent access would be a data race that the compiler cannot catch.
- Safe modification: Audit that all methods on these types are only called from expected actors. Document the invariant at the call site.
- Test coverage: No concurrency tests for these adapters.

**Mermaid renderer permanent-poison is unrecoverable:**
- Files: `crates/slate-core/src/diagram.rs:244`
- Why fragile: `RENDERER_POISONED` is a `static AtomicBool`. Once set, it can never be cleared within the process. A single panic in any diagram render permanently disables all future renders. This interacts with `catch_unwind` — if a future `mermaid-rs-renderer` version introduces a new panic path not currently caught, users lose diagrams for the session silently.
- Safe modification: Do not upgrade `mermaid-rs-renderer` without verifying panic behaviour on invalid input shapes.
- Test coverage: One test (`test_render_after_poison_returns_rend_failed`) covers the static flag, but no test verifies recovery.

**`DispatchQueue.main.sync` in `SlateCommands`:**
- Files: `apps/slate-mac/Sources/SlateMac/SlateCommands.swift:101`
- Why fragile: `DispatchQueue.main.sync` called from any context that is already on the main queue causes a deadlock. The existing call appears safe given current call sites, but it is an easy footgun if command actions are ever triggered from within a main-actor Task chain.
- Safe modification: Prefer `await MainActor.run {}` in async contexts to avoid the deadlock risk.
- Test coverage: Not tested.

## Scaling Limits

**SQLite single-connection mutex:**
- Current capacity: Adequate for vaults up to ~10,000 files based on bench results in `target/criterion/`.
- Limit: As vault size grows, the single `Mutex<Connection>` serializes all reads and writes. Any long-running operation (initial scan, search) blocks all other queries.
- Scaling path: Connection pool with WAL-mode read concurrency, or splitting the index into read and write connections.

**Op-log unbounded growth:**
- Current capacity: Disk capacity.
- Limit: Each save appends the entire file content. At 500 KB/save × 1,000 saves = 500 MB per heavily-edited file.
- Scaling path: Implement compaction (see Tech Debt section).

## Dependencies at Risk

**`mathcat 0.7.6-beta.4` — pre-release pin:**
- Risk: The project pins a beta version of MathCAT. Beta versions may have breaking API changes or be abandoned in favour of a different release line. The `include-zip` feature bundles rule files into the binary; if upstream restructures the rule-file layout, the bundle breaks silently.
- Files: `crates/slate-core/Cargo.toml:27`
- Impact: Breaking API change or abandoned beta would require a migration effort; rule-file restructuring would break speech/braille output at runtime.
- Migration plan: Monitor MathCAT 0.8 stable release; migrate when stable is published and the API surface is frozen.

**`tree-sitter-sequel 0.3` — third-party, not tree-sitter-org:**
- Risk: `tree-sitter-sequel` is maintained by `derekstride`, not the tree-sitter organization. The crate comment at `Cargo.toml:40-45` notes it was chosen because `tree-sitter-sql 0.0.2` pins tree-sitter 0.19 (incompatible with the 0.26 the rest of the grammars use). If `derekstride` abandons the crate, or if `tree-sitter-sql` adds a 0.26-compatible release, the project is stuck on an unmaintained grammar.
- Files: `crates/slate-core/Cargo.toml:40-45`
- Impact: SQL code blocks lose syntax highlighting and token classification.
- Migration plan: Watch `tree-sitter-sql` for a 0.26-compatible release; switch then.

**`mermaid-rs-renderer 0.2` — process-global mutable state:**
- Risk: `mermaid-rs-renderer` holds a process-global `Mutex` for its text measurer. This design means panics permanently poison rendering for the session. There is no API to reset the state. The library is not under the project's control.
- Files: `crates/slate-core/src/diagram.rs`, `crates/slate-core/Cargo.toml:55`
- Impact: Single diagram panic = no diagrams for the session.
- Migration plan: Evaluate headless Mermaid rendering via JS runtime (Deno/QuickJS) as a process-isolated fallback; or contribute an upstream reset API.

## Missing Critical Features

**No file-system change watcher:**
- Problem: External edits (from another editor, git, sync services like iCloud/Dropbox) are not reflected until the user manually re-scans. The FTS index, backlinks, and properties panel all show stale data after external edits.
- Blocks: Reliable "live" vault experience for users with multiple editors or automated tools modifying their vault.

**Search scopes `File` and `Tag` are stubs:**
- Problem: `SearchScope::File(_)` and `SearchScope::Tag(_)` at `crates/slate-core/src/search_db.rs:109-116` return `VaultError::Unsupported`. These are reserved variants with no implementation.
- Blocks: Find-in-current-file and tag-scoped search features.

**PlantUML / D2 / Graphviz diagram formats not supported:**
- Problem: `crates/slate-core/src/diagram.rs:20` documents "V1 supports Mermaid only." Users who use PlantUML (common in engineering teams) or D2 (modern diagram language) get `UnsupportedDialect` errors.
- Blocks: Engineering/technical users who depend on PlantUML or D2 diagrams in their vaults.

**Op-log provides history but no UI for it:**
- Problem: `read_oplog` is implemented and exposed via FFI, but no UI in the Swift layer reads or presents version history. The history data is being collected but has no consumer.
- Blocks: Undo-beyond-last-save, conflict resolution UI for multi-device sync.

## Test Coverage Gaps

**No integration tests for the file-watch path:**
- What's not tested: `VaultProvider::watch` returns `None` (no-op). The incremental-rescan code path that would be triggered by real file-change events has zero integration coverage.
- Files: `crates/slate-core/src/vault/provider.rs`, `crates/slate-core/src/vault/fs.rs`
- Risk: When the watcher is implemented, regressions in incremental indexing would not be caught.
- Priority: High — implement alongside the watcher.

**AppState is untestable in isolation for most of its methods:**
- What's not tested: The majority of `AppState`'s 107 functions interact with both the FFI layer and `@Published` state. Only a small subset is covered by `AppStateTests.swift`. Methods that dispatch off-actor work (e.g., `loadCurrentMathBlocks`, `loadCurrentCodeBlocks`, `loadCurrentDiagramBlocks`) have no test coverage.
- Files: `apps/slate-mac/Sources/SlateMac/AppState.swift`, `apps/slate-mac/Tests/SlateMacTests/AppStateTests.swift`
- Risk: Regressions in panel-loading state machines are only caught by manual testing.
- Priority: High — extract view models (see Tech Debt) to make these testable.

**Op-log compaction has no tests (because it doesn't exist yet):**
- What's not tested: There are no tests for oplog size bounds, compaction triggering, or retention enforcement.
- Files: `crates/slate-core/src/oplog.rs`
- Risk: When compaction is implemented, correctness of the compacted log (no entry corruption, correct entry ordering) is not verified.
- Priority: Medium — write tests as part of the compaction implementation.

**`@unchecked Sendable` adapters have no concurrency tests:**
- What's not tested: `ScanProgressAdapter` and `MenuCommandAction` suppress Swift 6 concurrency checking. There are no tests verifying these types are accessed only from their expected actors.
- Files: `apps/slate-mac/Sources/SlateMac/ScanProgressAdapter.swift`, `apps/slate-mac/Sources/SlateMac/SlateCommands.swift`
- Risk: Data races in the FFI bridge layer that the compiler cannot catch.
- Priority: Medium.

---

*Concerns audit: 2026-05-28*
