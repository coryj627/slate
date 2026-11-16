# CB-8 — A quarantined note still lists, so an unreadable note no longer fails its folder's listing

Issues: #1900, #1709. Size S. Lanes: rust, windows (the app job; a change under `crates/` runs the lane in full), swift (compile only, plus a11y-check, which a `.swift` change starts). AT: — (no sentence changes on Windows). Wave: C2, slot 3. Depends on: nothing. Parallel with: CB-3 then CB-5, CB-6. Decision: CD41, answered 2026-11-16 (plan §3.1): the row lists with size 0 and the modified time core holds, and a marked row is filed for Milestone W. Interlock: `crates/slate-core/src/session.rs` goes CB-8, CB-9b, CB-12, CB-31, CB-33, CB-45b. CB-6 edits `RescanTests.cs`, so this chunk's Windows twin goes in `RescanTests.ChangeFeed.cs`. Review: a red team, then Codex adversarial-review rounds until it approves (plan §4 rule 9; the chunk changes product code).

*2026-11-16:* delivered by #1966; the five corrections its principal reported are applied below. Written 2026-11-16 from `reading/03_save_chain_lifecycle.md:289-367` (#1900) and `reading/02_ci_and_runner.md:411-444` (#1709), with every line re-read at 5b060297 (plan §2.6). `session.rs` has not changed since. The implementer re-reads each line before editing.

`T/` below is `apps/slate-windows/tests/SlateWindows.Tests/`; `S/` is `apps/slate-windows/src/SlateWindows/`.

## Goal

A row in `files` whose `size_bytes` is the sentinel -1 lists like any other row, with size 0. Today one such row fails the whole page it sits on, so a folder that holds one unreadable note shows "Could not load files" for as long as the note stays unreadable. The two stale comments #1709 names are corrected.

## Current state on main

- **The sentinel has three writers in `session.rs`,** all of which also set `mtime_ms = -1` and `ctime_ms = -1`: the host's containment (`contain_unrepairable_pending_path_locked`, `:8124`, the write at `:8165-8168`; reached from `repair_or_contain_path`, `:8060`), the sweep's quarantine (`quarantine_unrepairable_path_locked`, `:8788`, the write at `:8919-8922`; its containable error classes at `:8742-8760`), and the repair's own stat poison (`reindex_path_locked`, the write at `:8979-8982`, fault seam `SLATE_TEST_FAULT_REINDEX` at `:8944`), which rolls back on a read failure.
- **Three migrations write `size_bytes = -1` on every row** and leave the times alone: `migrations/026_reindex_typed_property_lists.sql:9`, `034_task_checkbox_offsets.sql:6`, `038_property_key_identity.sql:5`. Each forces the scanner's slow path. The scan records a per-file read failure and continues (`session.rs:11267`), so a file it cannot read keeps its -1 row. A cache upgraded across any of the three, holding one such file, fails that folder's listing with no quarantine involved.
- **-1 is a contract, not a mistake.** 026's comment says it never matches a real stat, and the fast path at `session.rs:11707-11710` relies on it. The quarantine test is `mtime_ms = -1` with no task rows (`:8143`, `:8896`), not `size_bytes`. So no writer changes.
- **The decoder rejects it.** `decode_file_summary_query_row` (`session.rs:12455`) raises `IntegralValueOutOfRange(4, raw)` at `:12490-12493`, which becomes `VaultError::Db` with the text "sqlite error: Integer -1 out of range at index 4". Its callers are `sidebar_filter_impl` (`:12639`), `list_files_impl` (`:12775`), `get_file_summary_impl` (`:12815`) and `list_dir_children_impl` (`:13058`). The same decode serves `directory_page.rs:747-770` (the query at `:724`, the decode at `:761`), `properties_db.rs:303` and `:448`, and `change_feed.rs:1024` (`present_file_summaries`, called at `:917`).
- **Two readers do not reject it and are wrong another way.** `get_file_metadata_impl` (`session.rs:12290`) casts `size_bytes as u64`, so -1 becomes 2^64-1. The canvas pass (`reindex_all_canvases`, `:22035`) compares `as u64` against its limit, so a quarantined `.canvas` reads as oversize and its card rows are purged.
- **Readers that already tolerate it:** the Bases engine (`bases/engine.rs:853`, `:937`, `:1721`, with `.max(0)`) and `change_feed.rs:461-463` (`to_u64` clamps to 0). The Bases SQL filter on `size_bytes` (`engine.rs:1273`) compares the raw value, so `file.size < N` matches a -1 row. That is left alone and named in the PR.
- **The FFI does not change.** `FileSummary.size_bytes` and `FileMetadata.size_bytes` are `u64` (`crates/slate-uniffi/src/lib.rs:2996-3001`, `:3147`, conversion `:3171-3175`).
- **Windows.** The tree's listing calls fail at `S/FilesSidebarViewModel.TreeOperations.cs:307-311` and `:452-459` and are spoken as "Could not load files: " and the exception's message. That sentence is #1890's and is not edited here. `MtimeMs` is read only to bucket the row by date (`S/FilesSidebarViewModel.cs:1380`) and to sort (`:1417-1418`); no date is shown.
- **Mac, by reading.** The same listing calls feed the tree's error state ("Could not load files", `FileTreeSidebar.swift:5419-5424`). `Sidebar/SidebarFileRow.swift:224-226` and `:239` build "Modified" and a date from `mtime_ms` with no guard, and that string is in the row's accessibility value. A quarantined row, once it lists, says a 1969 or 1970 date. A migration-route row keeps its real time.
- **#1709.** `apps/slate-mac/Sources/SlateMac/SlateCommands.swift:281` and `apps/slate-mac/Tests/SlateMacTests/CommandRegistryTests.swift:351` carry comments that name a `setVerbosity` id that no longer exists; the real ids are at `SlateCommands.swift:283-285`. The reading has the corrected text.

## Design

1. **The decoder absorbs the sentinel.** In `decode_file_summary_query_row`, a negative `size_bytes` decodes as 0. One place, so every caller above lists the row. `mtime_ms` and `ctime_ms` pass through as core holds them (CD41).
2. **`get_file_metadata_impl` reports 0 for a negative size,** not a wrapped value.
3. **The canvas pass skips a row with a negative size.** It does not treat it as oversize and does not purge its card rows; the row is indexed again when the file reads.
4. **No writer, migration, FFI record or host file changes.** No `A11yEvent` is constructed, so contract 38's ledger does not move.
5. **#1709's two comments** are corrected to name the three ids at `SlateCommands.swift:283-285`. Comments only; the swift lane compiles them.
6. **The Mac ledger line** (#1954, already listed under Speech or text): an unreadable note lists, and on the Mac its row says a 1969 or 1970 modified date until the file reads again. This PR adds a comment on #1954 naming itself.
7. **Filed, not built (CD41):** one issue in Milestone W for a marked row, an optional modified time or an `unreadable` flag on `FileSummary`, with a sentence on both hosts. It is an FFI change with new speech, so it is High by `AGENTS.md`'s rule.

## Tests

Failing first, each seen red with the product change absent.

- **Rust, `crates/slate-core/src/session/tests/tasks.rs`,** beside and modelled on `repair_or_contain_releases_a_permanently_unreadable_path` (`:1043`; the same `fault_guard()` and `SLATE_TEST_FAULT_REINDEX` seam): `a_contained_note_still_lists_in_its_folder`. After `Contained`, `list_dir_files_page`, `list_dir_children_page`, `list_dir_children`, `list_files`, `filter_files`, `get_file_summary` and `get_file_metadata` each return the note with size 0, and the bystander note too. Today the first call fails with `VaultError::Db`.
- **Rust, the sweep's route:** the same assertions after the sweep's quarantine, modelled on `unclearable_markers_quarantine_instead_of_poisoning_queries` (`tasks.rs:717`).
- **Rust, the migration's route** (`tests/directory_page.rs` or `tests/scan.rs`): set `size_bytes = -1` on one row by SQL, leave its times real, and assert it lists with size 0 and its real modified time.
- **Rust, the canvas pass:** a `.canvas` row with `size_bytes = -1` keeps its card rows across `reindex_all_canvases`. Red today: the rows are purged.
- **Rust, the change feed** (`tests/change_feed.rs`): a window over a quarantined path opens. If the quarantine's own `Modified` row is filtered as the session's own write, say so in the PR and drop this fact.
- **Windows twin, `T/RescanTests.ChangeFeed.cs`:** `AFolderHoldingAContainedNoteStillLoadsItsTree`. Contain a note through the session's `RepairOrContainPath` under the env seam (the pattern is at `T/TasksReviewTests.cs:990-1009`), refresh the tree, and assert that no "Could not load files" was spoken and the note's node is under its folder. It waits on conditions, never a fixed sleep or delay: CB-6's census pins those by file and count. It reads no census-only path, so it carries no `tier` trait.

## Docs and registers

- Plan 47 §12: the delivery record's row, in the wave's docs PR.
- #1954: one comment naming this PR for the #1900 line.
- No contract register row; no parity-matrix change; no help document.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-cb8-quarantine'
cargo fmt --all -- --check
cargo clippy --all-targets --workspace -- -D warnings
cargo test -p slate-core
cargo bench --workspace --no-run
& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AFolderHoldingAContainedNoteStillLoadsItsTree"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <the changed .cs files>
```

The PR's own lanes: rust, windows in full, swift and a11y-check.

## Acceptance

- [ ] Each new fact was seen failing, then passing; the PR body quotes both runs.
- [ ] A negative size reaches no caller: the decoder, `get_file_metadata` and the canvas pass each have a fact.
- [ ] No writer, migration, FFI record, generated binding or host source file changed. `git diff --stat` shows `session.rs`, the Rust tests, one Windows test file and the two Swift files.
- [ ] Rule 5: the body writes `not delivered: VoiceOver pass` with #1954, and says what the Mac row will say (CD41).
- [ ] The Milestone W issue for a marked row exists and the body names it. The body also names what was left alone: the Bases filter on a raw -1, and the "Could not load files" sentence (#1890).
- [ ] Rule 9: the red team's findings and the Codex round count with its verdict are in the body.
- [ ] Rule 8: the count line. `Closes #1900` and `Closes #1709`; `closingIssuesReferences` shows those two and no other.

## Brief

Core, one function family in `crates/slate-core/src/session.rs`; no writer changes. Read this spec's "Current state", then `decode_file_summary_query_row` and its four callers, `get_file_metadata_impl`, and the size check in `reindex_all_canvases`. Write the Rust facts first (the contained route, the sweep's route, the migration's route, the canvas pass, the change-feed window) and see them red. Then: the decoder maps a negative `size_bytes` to 0; `get_file_metadata` reports 0; the canvas pass skips a negative row without purging. Add the Windows twin in `T/RescanTests.ChangeFeed.cs`, waiting on conditions only, red before the core change and green after (regenerate bindings into this worktree's own target first). Correct the two Mac comments for #1709. Run the four Rust gates, the licence check, the Windows build and the twin, and `dotnet format --include`. File the Milestone W issue for a marked row and comment on #1954. Review: a fresh Opus red team with `24_red_team_protocol.md`, then Codex rounds until it approves; record both in the PR body. Commit: `fix(core): a note the index could not read still lists, with size 0, and no longer fails its folder`.
