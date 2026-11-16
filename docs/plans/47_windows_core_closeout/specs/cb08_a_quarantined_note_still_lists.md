# CB-8 — A quarantined note still lists, so an unreadable note no longer fails its folder's listing

Issues: #1900, #1709, #1963 (taken at the lead's request on 2026-11-16). Size S. Lanes: rust, windows (the app job; a change under `crates/` runs the lane in full), swift (the lane's test suite and a11y-check ran on the PR; no Mac was available locally). AT: — (no sentence changes on Windows). Wave: C2, slot 3. Depends on: nothing. Parallel with: CB-3 then CB-5, CB-6. Decision: CD41, answered 2026-11-16 (plan §3.1): the row lists with size 0 and the modified time core holds, and a marked row is filed for Milestone W. Interlock: `crates/slate-core/src/session.rs` goes CB-8, CB-9b, CB-12, CB-31, CB-32 (if its one-time rebuild starts there), CB-33, then CB-45b and the phases after it (plan §6). CB-6 edits `RescanTests.cs`, so this chunk's Windows twin went in `RescanTests.ChangeFeed.cs`. Review: a red team, then Codex adversarial-review rounds until it approves (plan §4 rule 9; the chunk changes product code).

*2026-11-16, delivered:* by #1966 (70e97ba0), which closed #1900, #1709 and #1963. No step below is run again; plan 47 §12 has the record. Written the same day from `reading/03_save_chain_lifecycle.md:289-367` (#1900) and `reading/02_ci_and_runner.md:411-444` (#1709), with every line re-read at 5b060297. `session.rs` did not change between 5b060297 and #1966, which moved every `session.rs` line cited below by 7 to 27; the citations are as of 5b060297.

`T/` below is `apps/slate-windows/tests/SlateWindows.Tests/`; `S/` is `apps/slate-windows/src/SlateWindows/`.

## Goal

A row in `files` whose `size_bytes` is the sentinel -1 lists like any other row, with size 0. Before #1966, one such row failed the whole page it sat on, so a folder that held one unreadable note showed "Could not load files" for as long as the note stayed unreadable. The two stale comments #1709 names are corrected.

## State on main before #1966 (at 5b060297)

- **The sentinel has three writers in `session.rs`,** all of which also set `mtime_ms = -1` and `ctime_ms = -1`: the host's containment (`contain_unrepairable_pending_path_locked`, `:8124`, the write at `:8165-8168`; reached from `repair_or_contain_path`, `:8060`), the sweep's quarantine (`quarantine_unrepairable_path_locked`, `:8788`, the write at `:8919-8922`; its containable error classes at `:8742-8760`), and the repair's own stat poison (`reindex_path_locked`, the write at `:8979-8982`, fault seam `SLATE_TEST_FAULT_REINDEX` at `:8945`), which rolls back on a read failure.
- **Three migrations write `size_bytes = -1` on every row** and leave the times alone: `migrations/026_reindex_typed_property_lists.sql:9`, `034_task_checkbox_offsets.sql:6`, `038_property_key_identity.sql:5`. Each forces the scanner's slow path. The scan records a per-file read failure and continues (`session.rs:11267`), so a file it cannot read keeps its -1 row. Before #1966, a cache upgraded across any of the three, holding one such file, failed that folder's listing with no quarantine involved.
- **-1 is a contract, not a mistake.** 026's comment says it never matches a real stat, and the fast path at `session.rs:11707-11710` relies on it. The quarantine test is `mtime_ms = -1` with no task rows (`:8143`, `:8896`), not `size_bytes`. So no writer changed.
- **The decoder rejected it.** Before #1966, `decode_file_summary_query_row` (`session.rs:12455`) raised `IntegralValueOutOfRange(4, raw)` at `:12490-12493`, which became `VaultError::Db` with the text "sqlite error: Integer -1 out of range at index 4". Its callers are `sidebar_filter_impl` (`:12639`), `list_files_impl` (`:12775`), `get_file_summary_impl` (`:12815`) and `list_dir_children_impl` (`:13058`). The same decode serves `directory_page.rs:747-770` (the query at `:724`, the decode at `:761`), `properties_db.rs:303` and `:448`, and `change_feed.rs:1024` (`present_file_summaries`, called at `:917`).
- **Two readers did not reject it and were wrong another way.** Before #1966, `get_file_metadata_impl` (`session.rs:12290`) cast `size_bytes as u64`, so -1 became 2^64-1. The canvas pass (`reindex_all_canvases`, `:22035`) compared `as u64` against its limit, so a quarantined `.canvas` read as oversize and its card rows were purged.
- **Readers that already tolerated it:** the Bases engine (`bases/engine.rs:853`, `:937`, `:1721`, with `.max(0)`) and `change_feed.rs:461-463` (`to_u64` clamps to 0). The Bases SQL filter on `size_bytes` (`engine.rs:1273`) compares the raw value, so `file.size < N` matches a -1 row. That was left alone. #1966's body and #1964 name it.
- **The FFI did not change.** `FileSummary.size_bytes` and `FileMetadata.size_bytes` are `u64` (`crates/slate-uniffi/src/lib.rs:2996-3001`, `:3147`, conversion `:3171-3175`).
- **Windows.** Before #1966, the tree's listing calls failed on such a row at `S/FilesSidebarViewModel.TreeOperations.cs:307-311` and `:452-459`, and the failure was spoken as "Could not load files: " and the exception's message. That sentence is #1890's and was not edited. `MtimeMs` is read only to bucket the row by date (`S/FilesSidebarViewModel.cs:1380`) and to sort (`:1417-1418`), so no date is shown. That is true of the Files tree only. Core's graph table Modified cell renders -1 as "1969-12-31 23:59" on both hosts (`crates/slate-core/src/graph_queries.rs:751`, `modified_text`), which #1964 records.
- **Mac, by reading.** The same listing calls feed the tree's error state ("Could not load files", `FileTreeSidebar.swift:5419-5424`). `Sidebar/SidebarFileRow.swift:224-226` and `:239` build "Modified" and a date from `mtime_ms` with no guard, and that string is in the row's accessibility value. A quarantined row, once it lists, says a 1969 or 1970 date. A row that 026, 034 or 038 marked keeps its real time. Nine older migrations (012, 014, 015, 018, 019, 021, 024, 025, 031) write `mtime_ms = 0`, so a file unread since one of those already shows a 1970 date; that was existing behaviour and was not changed.
- **#1709.** Before #1966, `apps/slate-mac/Sources/SlateMac/SlateCommands.swift:281` and `apps/slate-mac/Tests/SlateMacTests/CommandRegistryTests.swift:351` carried comments that named a `setVerbosity` id that no longer exists; the real ids are at `SlateCommands.swift:283-285`. #1966 corrected both.

## Design

As #1966 delivered it.

1. **The decoder absorbs the sentinel.** In `decode_file_summary_query_row`, a negative `size_bytes` decodes as 0, through one helper, `reported_size_bytes`. One place, so every caller above lists the row. `mtime_ms` and `ctime_ms` pass through as core holds them (CD41).
2. **`get_file_metadata_impl` reports 0 for a negative size,** through the same helper, not a wrapped value.
3. **The canvas pass skips a row with a negative size.** It does not treat it as oversize and does not purge its card rows; the row is indexed again when the file reads.
4. **No writer, migration, FFI record or host source file changed,** except #1709's two comment edits. No `A11yEvent` is constructed, so contract 38's ledger does not move.
5. **#1709's two comments** now say the Windows chord table registers the commands "under the same three ids (contract 35 C-9, addendum 2026-10-30)". The test file's comment gives the contract as "C-9" alone. Comments only, so no failing test was possible. The swift lane's test suite ran on the PR (3,499 cases, run 43848194633).
6. **The Mac ledger line** (#1954, already listed under Speech or text): an unreadable note lists, and on the Mac its row says a 1969 or 1970 modified date until the file reads again. #1954 carries a comment that names #1966 for this line.
7. **Filed, not built (CD41):** #1964 in Milestone W, for a marked row, an optional modified time or an `unreadable` flag on `FileSummary`, with a sentence on both hosts. It is an FFI change with new speech, so it is High by `AGENTS.md`'s rule. #1964 also lists the other surfaces that read the sentinel.

## Tests

Each fact was seen red with the product change absent, then green. #1966's body quotes both runs. Before the change the three listing facts and the change-feed fact failed with `Db(Sqlite(IntegralValueOutOfRange(4, -1)))`, and the canvas fact found the board's rows purged.

- **Rust, the host's containment:** `tasks::a_contained_note_still_lists_in_its_folder` in `crates/slate-core/src/session/tests/tasks.rs`, beside and modelled on `repair_or_contain_releases_a_permanently_unreadable_path` (the same `fault_guard()` and `SLATE_TEST_FAULT_REINDEX` seam). After `Contained` the row holds -1 in all three stat columns. The note lists with size 0 and `mtime_ms == -1` (CD41). With the seam cleared, the next task query repairs the row, and it reports its real size and time.
- **Rust, the sweep's route:** `tasks::a_note_the_sweep_quarantined_still_lists_in_its_folder` in `crates/slate-core/src/session/tests/tasks.rs`, beside `unclearable_markers_quarantine_instead_of_poisoning_queries`. A stranded marker ages, the next task query's sweep quarantines the path, and the same listing assertions hold, with `mtime_ms == -1`.
- **Rust, the migration's route:** `directory_page::a_file_the_scan_after_a_migration_cannot_read_still_lists` in `crates/slate-core/src/session/tests/directory_page.rs`. It sets `size_bytes = -1` on every row as the migrations do, denies one file's reads through a new `UnreadableProvider` in `common.rs`, rescans, checks `error_count` is 1 and that the row lists with size 0 and the modified time it had, then asserts the row heals once reads are allowed. A -1 on one row whose file reads is healed by the next scan, so setting it by SQL alone would not be the route.
- **The shared helper.** The three listing facts share `assert_a_sentinel_size_lists_as_zero` in `crates/slate-core/src/session/tests/common.rs`, which asks nine readers. Seven must return the note with size 0 and its bystander beside it: `list_dir_files_page`, `list_dir_children_page`, `list_dir_children`, `list_files`, `filter_files`, `files_with_property` and `files_with_property_key`. `get_file_summary` and `get_file_metadata` are checked for the note only.
- **Rust, the canvas pass:** `canvas::a_board_whose_size_is_the_sentinel_keeps_its_rows_across_a_scan` in `crates/slate-core/src/session/tests/canvas.rs`. It sets the board's `size_bytes` to -1, denies the board's read through `UnreadableProvider` and runs a rescan; it does not call `reindex_all_canvases` directly. The scan counts one error and the board keeps its 9 node rows and 5 edge rows. Once the read is allowed, the next scan derives the rows again.
- **Rust, the change feed:** `change_feed::a_window_over_a_quarantined_path_opens` in `crates/slate-core/src/session/tests/change_feed.rs`. Another session edits a note and then contains it, and the first session's window still opens, with the record's summary at size 0. The quarantine itself writes no feed row (migration 040's triggers watch only `path` and `content_hash`), so the fact needs the other session's edit to put the path in a window.
- **Rust, #1963:** `a_generated_note_name_is_never_a_windows_reserved_device_name` in `crates/slate-core/tests/links_roundtrip.rs`. It is seeded and makes 300,000 draws from the `note_name()` strategy the file's three properties now share. None is one of the 22 stems `FsVaultProvider::write_file` refuses on Windows.
- **Windows twin, `T/RescanTests.ChangeFeed.cs`:** `AFolderHoldingAContainedNoteStillLoadsItsTree`, in two phases. The vault has a folder with two notes, and the fact expands it. Phase one contains one note through the session's `RepairOrContainPath` under the `SLATE_TEST_FAULT_REINDEX` env seam, awaits `Sidebar.RefreshAsync()`, and asserts that nothing was spoken, that the level is not shown as failed, and that the folder holds both notes. Phase two writes a third note into the folder, denies the contained note's read by an ACL for a whole explicit rescan, and asserts the one error sentence and the folder's three notes. It awaits the refresh and the rescan and has no sleep or delay; CB-6's census pins those by file and count.

## Docs and registers

- Plan 47 §12: the delivery record's row, in the wave's docs PR.
- #1954: one comment naming #1966 for the #1900 line. It is posted.
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

The commands as the implementer ran them; #1966's body has each result. The PR's own lanes ran and passed: rust, windows in full, swift (the Mac XCTest suite, 3,499 cases, run 43848194633) and a11y-check.

## Acceptance

- [ ] Each new fact was seen failing, then passing; the PR body quotes both runs.
- [ ] A negative size reaches no caller: the decoder, `get_file_metadata` and the canvas pass each have a fact.
- [ ] No writer, migration, FFI record, generated binding or host source file changed, except #1709's two comment edits. `git diff --stat` shows `session.rs`, the Rust tests (`links_roundtrip.rs` among them, for #1963), one Windows test file and the two Swift files.
- [ ] Rule 5: the body writes `not delivered: VoiceOver pass` with #1954, and says what the Mac row will say (CD41).
- [ ] The Milestone W issue for a marked row exists and the body names it. The body also names what was left alone: the Bases filter on a raw -1, and the "Could not load files" sentence (#1890).
- [ ] Rule 9: the red team's findings and the Codex round count with its verdict are in the body.
- [ ] Rule 8: the count line. `Closes #1900` and `Closes #1709`; `closingIssuesReferences` shows those two and #1963, which the lead added on 2026-11-16 as a find fixed here: `tests/links_roundtrip.rs` drew reserved device names such as `aux` for its notes.

## Brief

The brief as the implementer had it, kept for the record. #1963 was added to it at the lead's request on 2026-11-16: the property test in `crates/slate-core/tests/links_roundtrip.rs` drew reserved device names, and its generator was to leave them out, with a failing fact first.

Core, one function family in `crates/slate-core/src/session.rs`; no writer changes. Read this spec's "State on main" section, then `decode_file_summary_query_row` and its four callers, `get_file_metadata_impl`, and the size check in `reindex_all_canvases`. Write the Rust facts first (the contained route, the sweep's route, the migration's route, the canvas pass, the change-feed window) and see them red. Then: the decoder maps a negative `size_bytes` to 0; `get_file_metadata` reports 0; the canvas pass skips a negative row without purging. Add the Windows twin in `T/RescanTests.ChangeFeed.cs`, waiting on conditions only, red before the core change and green after (regenerate bindings into this worktree's own target first). Correct the two Mac comments for #1709. Run the four Rust gates, the licence check, the Windows build and the twin, and `dotnet format --include`. File the Milestone W issue for a marked row and comment on #1954. Review: a fresh Opus red team with `24_red_team_protocol.md`, then Codex rounds until it approves; record both in the PR body. Commit: `fix(core): a note the index could not read still lists, with size 0, and no longer fails its folder`.
