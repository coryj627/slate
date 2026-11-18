# CB-12: A repair announces only what it changed

Issues: #1683. Size S. Lanes: rust (every lane that runs the core facts; a change under `crates/` runs the Windows lane in full), windows (the app job: one edited fact and one new fact), swift (compile only; no Mac source changes). AT: an NVDA spot check with two Slate windows on one vault (the PR opens as a draft and is marked ready when the phrases are in its body). Wave: C5, slot 2. Depends on: nothing; #1982 (c84a08ef) is already under it. Parallel with: CB-11 (`WorkspaceViewModel.cs`, `WorkspaceViewModel.Rescan.cs`, `WorkspaceViewModel.Citations.cs`, `CitationAsyncInterleavingTests.cs`) and CB-19 (a new class, `App.xaml.cs`, two `MainWindow` partial files, the gate). Decision: CD6, answered 2026-11-16 as recommended (plan §3.1): the change is made in core, so both hosts get it. Interlock: `crates/slate-core/src/session.rs` goes CB-8, CB-9b, CB-12, CB-31 (plan §6). Review: a red team, then Codex adversarial-review rounds until it approves (plan §4 rule 9; the chunk changes product code in plan 21's protocol).

*2026-11-17, the plan lead's calls on this spec's open questions:* (1) The `Modified` a repair sends after it re-read a file whose content was already indexed stays as it is. It goes on #1955 as one line with the two reasons in "What stays out". (2) The graph find (the not-found arm stages a graph removal for a path with no node) is taken here as found and fixed, with its own red fact. (3) The single-session case is in scope and is named in the PR body and in a comment on #1683. (4) The two `canonical_identity` facts' expected lists change in this PR, with the reason. (5) The title says what is true: "a repair that finds no index row and no file announces nothing".

Written 2026-11-17 from `reading/03_save_chain_lifecycle.md:169-236` (read at 1ec9bf31), the issue and the owner's comment on it, #1982's body, and issues #1977 and #1981. Every file and line below was re-read at 3ae84200 and is cited as of that commit, but for `canonical_identity.rs`, which #1985 lengthened and which is cited as of 0a417b84. `main` moved to 0a417b84 (#1985) while this spec was written; #1985 edits neither `session.rs` nor `RescanTests.ChangeFeed.cs`. It moved again to eea6e7a2 (#1987, documents only) before the spec's red team; every line cited below was checked again there. Unlike the reading, this spec rests on runs: step 0 measured the mechanism, the fix shape as a prototype, and both hosts' side of it. "Step 0, as run" holds the results, and each statement that is still by reading says so.

`C/` below is `crates/slate-core/src/`; `S/` is `apps/slate-windows/src/SlateWindows/`; `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`.

**No FFI change.** `crates/slate-uniffi/src/lib.rs` is not edited. `FileChangeEvent` (`:4144`), `FileChangeKind` (`:4155`), `VaultEventListener` (`:5796`) and the mapping (`:5752`) are untouched. The generated `slate_uniffi.cs` must have the same SHA-256 before and after, and the PR body quotes both. No `A11yEvent` is constructed, moved or removed, so contract 38's ledger, the corpus and the variant count do not move. No sentence is added or changed: one that was spoken stops being spoken.

**Files this chunk edits.** Product: `C/session.rs` only (the not-found arm of `reindex_path_locked`, `:9241-9256`, the tail at `:9272-9291`, the comment above the notify, and one `mod` line in `mod tests`). Tests: a new `C/session/tests/repair_announcement.rs`; `C/session/tests/canonical_identity.rs` (two expected lists and their doc comments, `:730-760` and `:895-918` as of 0a417b84); `T/RescanTests.ChangeFeed.cs` (one fact edited at `:871-896`, two added). Documents: `docs/plans/21_write_intent_protocol_invariants.md` (one dated section) and, only if step 0's register read names one, a dated note in `docs/plans/32_canonical_file_identity_contracts.md`.

**Overlap.**

- CB-11 edits no file above. It and this chunk both have facts about a rename in the `RescanTests` family, in different files.
- #1985 (CB-13) merged as 0a417b84 and is under this branch. It edited `C/session/tests/canonical_identity.rs`, `C/session/tests/common.rs` and `C/session/tests/fault_guard.rs`, and not `C/session.rs`; this chunk's two expected lists in `canonical_identity.rs` are cited as of that merge. This chunk adds no fault-seam variable, so the two lists of them (`common.rs:28`, `fault_guard.rs:54`) do not move.
- CB-9b (same wave, slot 1, after CB-11) stands in plan §6's `session.rs` row with this chunk: if its spec edits `session.rs`, it branches only after this chunk has merged.
- #1981 (a stopped rename's recovery loses the history binding and names no creation). Its ground is the same arm when a row DOES stand: the row is removed and the other path is indexed fresh. This chunk changes nothing there: a row that stands is still removed and still announced. A guard fact pins that (Tests, fact 9). This chunk does not move the row, carry `oplog_name` or change what the feed records.
- #1977 (an outside edit before a rename is dropped as the rename's own echo). Its ground is the watcher's echo ledger (`C/vault/fs.rs`, `C/vault/echo_ledger.rs`, `C/session/watch.rs`). This chunk edits none of them.
- #1982's bind step (`:8735-8764`, `:8894-9001`) is not edited. For a path that names nothing it answers `MarkerBinding::Own` (`:8901-8904`), so the not-found route is the one it always was (measured).

## Goal

A repair that finds no file at a path and no index row for it changes nothing outside `text_write_intents`, and so it says nothing: no `Deleted` event, no graph operation applied, no generation bump. It still clears the marker tokens it was run for. A repair that removes a row that stood still announces `Deleted`, still removes the row's graph node and still moves the generation, as it does today.

For a reader on Windows: after another Slate window renames a note this window has open, and the rename landed, no repair makes this window say "first.md is missing from disk. Open editor content was preserved." or mark the tab missing. The tab stays where it is until the next rescan moves it to the note's new path, as it does today. (A stopped rename's recovery still announces the row it removed, fact 9, and a full re-sync still marks a tab whose path the index no longer holds, `S/WorkspaceViewModel.Rescan.cs:405`; neither is this defect.)

## Current state on main

**The markers.** Every structural move plants a held write-intent marker on both paths before the kernel call and ages both in its index commit: `structural_move_file` (`C/session.rs:17959`), the plant at `:18012` (`plant_move_markers_locked`, `:8360`), the age at `:18112` in `finish_structural_move` (`:18092`) through `age_move_markers_in_tx` (`:8460`; "so the first task query after the move re-reads every touched path", `:8452-8453`). The same commit moves the `files` row (`:18029`, `:18059`). The index and `text_write_intents` are one database shared by every session on the vault.

**The sweep.** A task query sweeps aged markers before it reads: `tasks_for_file` (`:8003`, sweep `:8005`), `note_tasks` (`:8014`, sweep `:8016`), `tasks_in_vault` (`:10417`, sweep `:10425`, all paths), through `repair_abandoned_intents_locked` (`:8630`), the bind step, and `reindex_path_locked` (call `:8809`, body `:9168-9292`). A host can also ask for one path's repair: `reindex_path` (`:8038`, call `:8052`) and `repair_or_contain_path` (`:8073`, call `:8114`).

**The arm.** In `reindex_path_locked`, when `index_file` answers `NotFound` (`:9241`), the arm sets `converged_to_deletion`, stages `GraphOp::FileRemoved` for the path (`:9249-9254`) and runs `DELETE FROM files WHERE path = ?1` (`:9255`). After the commit (`:9272`) the function applies the graph (`:9273`), bumps the bases generation (`:9274`) and announces `Deleted` (`:9282-9290`). **None of the four asks whether a row stood.** The comment above the notify (`:9275-9281`) gives round 24's reason for the kind: a convergence to deletion announced as `Modified` left hosts showing the note as existing.

**What follows from that, measured.**

- Another session's rename. B renames `first.md` to `second.md`. A then asks for the tasks of `first.md`. A's sweep reads `NotFound`, the DELETE removes 0 rows, and A's listener hears `Deleted first.md`. The three task queries behave alike.
- One session alone. A session that renames `first.md` to `second.md` and then asks for tasks hears `Deleted first.md` for its own old path.
- A folder. B renames a folder holding two notes: A hears one `Deleted` for each old path.
- A move to another folder: the same as a rename.
- The host routes. `reindex_path("never.md")` returns `Ok(())` and `repair_or_contain_path("never.md")` returns `Ok(Repaired)` for a path with no row and no file, and each sends `Deleted never.md`.
- The scope. A sweep repairs the old path only when the query covers it: `tasks_in_vault` (all paths) or a query for the old path itself (the scope filter, `:8653-8657`). A query for another note does not reach it.
- The graph. `FileRemoved` is staged whenever this session's graph index is live. Where the graph holds no node for the path (the session renamed the note itself, so its graph already moved the node; or it built its graph after the other session's rename), `GraphIndex` meets `debug_assert!(false, "removed file had no node")` (`C/graph.rs:1025`) and returns `ReplayMismatch`. A debug build panics there with the session's connection mutex and graph mutex poisoned. A release build drops the whole graph index (`graph_apply`, `C/session.rs:2151`; `graph_drop_locked`, `:2057`), moves the generation floor so every graph probe reads "changed", and rebuilds the index on the next graph query. **So in one session with a graph surface open, every rename of a note followed by a vault-wide task query, or by a task query for the old path, drops and rebuilds the whole graph index.**
- `index_file` stages before it reads (by reading, confirmed by the red team): it stats the path (`:11875`), stages `GraphOp::FileAdded` for a path with no row (`:11931-11939`) or `MetadataTouched` for one with a row (`:12030-12032`), and only then reads the file (`:12115`). A file that is gone between the stat and the read reaches the arm with `FileAdded` already staged and no row inserted. On main the arm's `FileRemoved` follows it in the same sink, and the replay cancels out.
- The generation. `bump_bases_generation` moves `interaction_generation` though nothing changed.
- Not affected: another session's DELETE of a note. `delete_file` clears its own marker with the row (`:17310-17316`), so no sweep runs and A hears nothing.

**The Windows host.** `S/VaultLifecycleViewModel.FileChanges.cs:45-47` hands a `Deleted` to `WorkspaceViewModel.InvalidatePath` (`S/WorkspaceViewModel.cs:3767`, `:3784-3818`). Each tab on the path is marked missing (`tab.InvalidatePath()`, `:864-873`: the save epoch moves, `IsMissingFromDisk = true`, the status "{path} no longer exists on disk. Unsaved editor content is preserved."), the closed-tab history and the Connections stack are pruned for the path (`:3796-3802`), and one High `HostComposed` is spoken: "{file name} is missing from disk. Open editor content was preserved." (`:3811-3817`). Core owns neither sentence. The same event also removes the path from Quick Open (`S/VaultLifecycleViewModel.FileChanges.cs:65`), notifies the reading models and the Bases surfaces (`:71-74`) and probes the graph (`:91`).

**What makes this window ask.** Any Slate-owned file event runs `InvalidateAllInteractionStates` (`S/VaultLifecycleViewModel.FileChanges.cs:64`), and a tab still at `first.md` re-queries its tasks. Measured in the harness: the query is `TasksForFile` at `S/EditorInteractions.cs:2532` (`LoadArtifactCacheAsync`, queued from `InvalidateExternalState`, `:1322`, `:1347`, `:2472`, `:2502`), on a pool thread. The own rename in the issue is incidental: an unrelated own create after the foreign rename gives the same sentence (measured).

**The fact that pins the defect.** `T/RescanTests.ChangeFeed.cs:871-896`, `AForeignRenameThenAnOwnRenameOfItsDestinationMovesTheTabThere`. Its comment (`:882-884`) says the sentence is spoken "as on main", and `h.Events.Clear()` (`:887`) throws it away before the rescan.

**How this window learns of the rename without the stray event.** The change feed. A's next rescan window holds `Renamed second.md` from `first.md` (measured: the window is the same three records whether or not a sweep ran first). `AForeignRenameRetargetsTheOpenTab` (`T/RescanTests.ChangeFeed.cs:841-864`) pins that the window moves the tab, clean or dirty, never marked missing. The watcher asks for that rescan about a second after the rename lands (`C/session/watch.rs:62`), at once when the window is active and on activation otherwise (`S/VaultLifecycleViewModel.Rescan.cs:217-224`). The feed window also invalidates this session's graph (`graph_invalidate_locked_conn`, `C/session.rs:2067-2075`: "the graph learns only this session's own commits").

**The Mac, by reading.** The one listener (`apps/slate-mac/Sources/SlateMac/AppState+History.swift:82`, hook `:767-779`) does these things with a `.deleted`: it joins the rescan journal while a rescan runs (`AppState+Rescan.swift:1158`), it returns at once from `handleSidebarFileChange` (`AppState.swift:15628`, which acts on `.modified` only), for an `.md` path it schedules a 75 ms template availability refresh (`AppState.swift:25297-25314`), and, as for every event, it runs the three graph refresh probes (`refreshConnectionsIfGraphChanged`, `refreshGraphTableIfGraphChanged`, `refreshGraphDiagramIfGraphChanged`, `AppState+History.swift:776-778`). Nothing on the Mac marks a tab missing or speaks from a `Deleted`. No Swift test pins a `Deleted` from a repair. No Swift edit is needed.

**Cause.** Measured. The not-found arm acts and announces as if it removed a row, without asking whether one stood.

## Design

**One question, asked first: does a row stand at this path?** The bind step already has this shape for the alias row (`:8944-8961`): it reads `row_under_alias` and stages the graph removal and deletes only then.

1. **`reindex_path_locked`, the not-found arm (`:9241-9256`).** Inside the same fenced transaction, before the arm stages anything (`index_file` may already have staged `FileAdded`, Current state):

   ```rust
   Err(VaultError::Io(ref io_err)) if io_err.kind() == std::io::ErrorKind::NotFound => {
       let row_stands: bool = tx.query_row(
           "SELECT EXISTS(SELECT 1 FROM files WHERE path = ?1)",
           rusqlite::params![path],
           |row| row.get(0),
       )?;
       if row_stands {
           outcome = RepairOutcome::RemovedRow;
           graph_sink.stage_with(|| { /* FileRemoved, as today */ })?;
           tx.execute("DELETE FROM files WHERE path = ?1", rusqlite::params![path])?;
       } else {
           outcome = RepairOutcome::NothingStood;
       }
   }
   ```

   `converged_to_deletion` becomes a three-valued local (`Reindexed`, `RemovedRow`, `NothingStood`; a private enum beside `MarkerBinding`, or two booleans if the implementer finds that plainer). The poison `UPDATE` at `:9212-9215` runs before the read and touches no row when none stands; it is left where it is.

2. **The tail (`:9266-9291`).** The token clear and the commit are unchanged for all three outcomes: a path with no file and no row has converged, so its selected tokens go, as today. Then:

   - `Reindexed`: apply the graph sink, bump the generation and announce `Modified`, as today.
   - `RemovedRow`: apply the graph sink, bump the generation and announce `Deleted`, as today.
   - `NothingStood`: drop the graph sink **without applying it** and return `Ok(())`. No graph operation, no bump, no announcement. The sink is dropped, not applied, because `index_file` stages `FileAdded` for a path with no row before it reads (`:11931-11939`): when the file is gone between the stat and the read, that op is in the sink and no row was inserted, so applying it would give this session's graph a node for a note that does not exist. On main the arm's own `FileRemoved` cancelled it. `GraphOpSink` has no `Drop` behaviour (`C/graph.rs:172-211`), so dropping it discards the ops. The measured prototype did exactly this: its `NothingStood` branch returned after the commit and before `graph_apply`.

3. **The comment above the notify** is rewritten to say both rules: the kind matches the outcome (round 24), and an outcome that changed nothing is not announced (#1683, CD6), with the single-session and two-session cases named.

4. **Nothing else moves.** `repair_abandoned_intents_locked`, the bind step, `contain_repair_failure_locked`, `quarantine_unrepairable_path_locked`, `reindex_path`, `repair_or_contain_path` and their return values are not edited. The host routes return what they return today (`Ok(())`, `Ok(Repaired)`) for a path with no row and no file; they stop sending the event because they call the same function.

**What each caller of the arm gets after.**

| Sequence | main sends | This sends |
|---|---|---|
| B renames `first.md` to `second.md`; A asks for the tasks of `first.md` | `Deleted first.md` | nothing |
| B renames `first.md` to `second.md`; A's `tasks_in_vault` alone (no query for `first.md` before it) | `Deleted first.md`, `Modified second.md` | `Modified second.md` |
| one session renames, then `tasks_in_vault` | `Deleted first.md`, `Modified second.md` | `Modified second.md` |
| B renames a folder of two notes; A's `tasks_in_vault` | two `Deleted`, two `Modified` | two `Modified` |
| `reindex_path` or `repair_or_contain_path` on a path with no row and no file | `Deleted` | nothing |
| a row stands and the file is gone (round 24; a note deleted outside Slate under an abandoned marker) | `Deleted` | `Deleted` |
| a plain rename that stopped between the volume and the index (#1981's shape) | `Deleted A.md`, `Modified B.md` | the same |
| a case-only rename on a volume that keeps case (two real names) | `Deleted A.md`, `Modified a.md` | `Modified a.md` |

**What a host no longer gets, and why that is right.** A `Deleted` for a path this session's index has no row for told a host nothing it could act on truthfully. The path's row was moved or removed by a commit some session already made: a move, which announced it to that session's listeners, or a scan or another session's repair, which recorded it in the change feed (a scan sends no file event; the only `Deleted` sites are `:9282`, `:16543`, `:17347` and `:17499`). Every other session learns of it from its next change-feed window, which names a rename as a rename and a removal as a removal. On Windows the stray event was not rare: any own file event runs `InvalidateAllInteractionStates`, and a tab still at the old path re-queries it. Where the row was removed because the file was deleted outside Slate, the stray `Deleted` happened to be true; the fix moves that news to the next window, which applies another session's removal silently (R-9, `S/WorkspaceViewModel.cs:3809-3811`), as it does today when no sweep ran first.

The stray event also ran some refreshes by accident, and those now wait for the next window as they already do when no sweep runs first: on Windows, the old path left Quick Open, the reading models and the Bases surfaces were notified and the graph was probed; on the Mac, one template availability refresh and the three graph refresh probes ran. Each is a refresh with nothing new to show, or a removal the next window makes.

**The graph, after.** With no row standing the arm stages nothing and the sink is dropped, so a session that renamed a note itself keeps its graph index across the next task query. Where another session renamed the note and this session's graph was built before that, this session's graph keeps a node for the old path until its next change-feed window drops the index, which is how a session's graph learns of any other session's commit. On main the same node is removed by the stray operation and the destination is still absent, so the graph is as stale either way until that window.

**What stays out.**

- **`Modified` after a re-read that found the content already indexed** (measured: after any rename the sweep re-reads the destination, only `index_epoch` and `indexed_at_ms` change, and `Modified` is sent). The lead's call: not here, one line on #1955. Reasons: (a) "unchanged" cannot be read from the content hash, because a quarantined row keeps its hash while its task rows are gone, and its heal must be announced; (b) nothing false is heard from it: `Modified` asks a host to refresh, and a refresh of unchanged content says nothing.
- **A file that appears at a swept old path is announced as `Modified`, not as a creation.** That is #1981's class.
- **The scan, the delete routes and the quarantine.** By reading none announces a change it did not make: each delete follows a provider delete that succeeded, the quarantine announces only after it changed the row, and the scan sends no file event.
- **The Windows host.** No product file under `apps/` is edited. The sentence, its site and `InvalidatePath` stay.

### Step 0, as run (2026-11-17)

On main's product code at 3ae84200, NTFS, Windows 10.0.26300, with a temporary probe module of 28 facts and a temporary `eprintln!` in the arm. The raw logs are kept with this chunk's working notes; the PR body quotes the lines it relies on.

- **The mechanism holds.** `reindex_path_locked("first.md"): not-found arm, DELETE FROM files removed 0 row(s)`, then `A's listener heard: ["Deleted first.md"]`, for `tasks_for_file`, `note_tasks` and `tasks_in_vault`. The marker is aged by the move itself (`age_ms=15002`); no seam is needed.
- **The wider cases** in "What follows from that" above were each run.
- **Round 24's case** (`deleted_file_intents_converge_instead_of_poisoning_queries`, `C/session/tests/tasks.rs:426`, assert `:487-492`; `a_marker_under_a_path_that_names_nothing_is_settled_as_before`, `C/session/tests/canonical_identity.rs:1147`): the DELETE removes 1 row and `Deleted` is heard.
- **#1981's shape:** `A1981.md` removes 1 row; heard `["Deleted A1981.md", "Modified B1981.md"]`.
- **The feed:** A's window after B's rename is `Renamed second.md` from `first.md`, `Removed first.md`, `Present second.md`, with no query, after `tasks_for_file` and after `tasks_in_vault`.
- **The graph:** the three outcomes above, in a debug build and with `--config "profile.test.debug-assertions=false"`.
- **Windows:** the existing fact with the two assertions in place of the clear fails with `Assert.Empty() Failure: Collection was not empty … ["first.md is missing from disk. Open editor content"···]`. A listener that records stacks names `EditorInteractions.cs:2532`.
- **Two windows on one vault:** two instances started with different `SLATE_CENSUS_INSTANCE_ID` values and their own `SLATE_LOCAL_APP_DATA` roots both hold one vault. `scripts/windows/launch-for-field-pass.ps1` refuses a second instance (`:77-84`), so the pass starts the second by hand.
- **A notify-only gate** (announce `Deleted` only when the DELETE removed a row) turned no core lib fact red on NTFS: 2637 of 2638 finished, 0 failed; the one not finished is a long census fact that makes no task query and no rename. Two facts expect the zero-row `Deleted` on a volume that keeps case: `the_sweep_after_a_case_only_rename_reports_the_stored_spelling_once` (`canonical_identity.rs:900`, list `:913-917`) and `a_folders_case_only_rename_leaves_one_row_for_each_file_after_the_next_task_query` (`:736`, list `:751-760`). The first one's sequence was run in a folder switched to case-sensitive names: `["Deleted A.md", "Modified a.md"]` without the gate, `["Modified a.md"]` with it.

**The design as a prototype (2026-11-17, second round).** The arm was changed as Design 1 says, temporarily, and reverted; its `NothingStood` branch returned after the commit and before `graph_apply`, which is Design 2 as corrected by the spec's red team (the first draft of Design 2 applied the sink on that branch). The prototype diff is kept with the chunk's working notes (`p2/prototype-session.rs.diff`).

- **Core:** `cargo test -p slate-core --no-fail-fast`: lib `2624 passed; 0 failed; 14 ignored`, the 18 integration targets and the doc-tests ok. `cargo test -p slate-uniffi -p slate-cli`: ok. No fact turned red on NTFS.
- **The two case-keeping sequences,** run in a folder switched to case-sensitive names: `A.md` to `a.md` then `tasks_in_vault` gives `["Modified a.md"]` (main: `["Deleted A.md", "Modified a.md"]`); `rename_folder("Docs", "docs")` then `tasks_in_vault` gives the two `Modified` (main: two `Deleted` first). The Rust lane on Linux (`.github/workflows/rust.yml`, the nextest job) runs those branches; no lane runs the Rust facts on macOS.
- **The graph, one session:** after the own rename the graph is live with `[linker.md, second.md]`; `tasks_for_file("first.md")` hears nothing and moves neither the graph generation nor the interaction generation; `tasks_in_vault` hears `Modified second.md`. No panic in a debug build.
- **The graph, two sessions, A's graph built before B's rename:** after A's sweep of `first.md` nothing is heard and A's graph is unchanged (`[first.md, linker.md]`). A's next change-feed window drops the graph and the next graph query is right. That last step is the same on main.
- **Windows, against a Release DLL built from the prototype:** the generated `slate_uniffi.cs` has the same SHA-256 as before. 930 facts passed, 0 failed, over every unit class that mentions a missing tab (`RescanTests`, `TasksReviewTests`, `CanonicalIdentityTests`, `FileManagementTests`, `HotExitTests`, `ConnectionsLeafTests` without its `TheModelOf` facts, `FinalMergeDeltaTests`, `PumpedSaveReentrancyTests`, `RecoveryAnnouncementTests`, `SaveChainAdmissionTests`, `SharedNameTests`, `SiblingNamesTests`, `W1WorkspaceRedTeamTests`, `WorkspaceLayoutTests`) and the `TaskIndexRepair` facts. The existing fact with the two assertions is green. The unrelated-create variant is red on main and green on the prototype.
- **What the stray event destroyed, found by this round:** with a second tab still at `first.md`, a closed tab of `first.md` and a Connections back-stack entry for it, the stray `Deleted` on main removed both entries, and after the rescan Reopen Closed Tab had nothing to reopen, for a note that only moved. With the prototype both entries are kept and the rescan's feed window moves them to `second.md` (`WorkspaceViewModel.RetargetPathsForFeedRenames`, `S/WorkspaceViewModel.Rescan.cs:255-299`). So the fix removes no pruning those two needed; it removes a loss.
- **Two real windows on one vault, no screen reader:** the sentence was raised on main with the watcher on and no test seam, and was not raised with the prototype. The sequence is in "The NVDA spot check".

**Residuals the prototype showed, each stated in the PR body.**

- *An open base.* On main, a task query for the old path alone moved this session's generation, which by accident made an open base re-run its query and show another session's rename. With the fix that query moves nothing, so the base keeps its cached rows until the destination is re-read, this session writes, or a rescan runs (measured). Before any query both builds show the cached rows, so the staleness is not new; one accidental refresh goes. The class (a session's generation does not see another session's commits) goes on #1955.
- *A session with no change feed.* A session that built its graph before another session's rename and never reads a feed window keeps a node for the old path, where main removed it and left the note absent under either name. Both hosts register the feed at open, so neither host is that session.
- *The destination's re-read under a stale graph.* Where this session's graph was built before another session's rename, the sweep's re-read of the DESTINATION stages `LinksetChanged` for a node this graph does not have: a debug build panics at `graph.rs:841`, a release build drops the index and rebuilds it right. That is the ordinary re-read arm, it is the same on main, and it is not changed here. One line on #1955.

**Re-run on main at 0a417b84** (#1985 merged while this spec was written). #1985 does not edit `session.rs`. It adds 158 lines to `canonical_identity.rs`, so the two facts and the staging this spec cites are at new lines, given in Tests. The tests-only commit's red runs are made on 0a417b84 and are the re-run of step 0 there.

## Tests

Failing facts first, in one tests-only commit that compiles. No fact adds a sleep, a delay or a timed wait. Plan 21's rule applies: each new fact is bite-verified by switching its half of the fix off (the mutations below), and the PR body quotes each mutation with the facts it turns red.

**Core, a new module `C/session/tests/repair_announcement.rs`** (registered by one `mod` line in `session.rs`'s `mod tests`; `git add` it and run `python3 scripts/apply-license-header.py`). Two sessions are two `VaultSession::from_filesystem` values on one temp folder, as the probe did; a recording listener on A collects `kind path` strings.

1. `another_sessions_rename_is_not_announced_as_a_deletion`, one case for each of `tasks_for_file`, `note_tasks` and `tasks_in_vault`. A has `first.md` indexed. B renames it to `second.md`. A's query for `first.md` (or the vault) is heard as `[]` (or `["Modified second.md"]` for the vault query). Red today: `Deleted first.md` is in each list.
2. `a_sessions_own_rename_is_not_followed_by_a_deletion_of_the_old_path`. One session renames, then `tasks_for_file("first.md")`: heard after the rename's own `Renamed` is `[]`; then `tasks_in_vault`: `["Modified second.md"]`. Red today.
3. `another_sessions_folder_rename_and_move_announce_no_deletion`. B renames `Dir` (two notes) to `Ren` and moves a third note into `Sub`; A's `tasks_in_vault` hears exactly the three `Modified` events for the new paths. Red today (three `Deleted` as well).
4. `a_hosts_repair_of_a_path_with_no_row_and_no_file_announces_nothing`. `reindex_path("never.md")` is `Ok(())` and `repair_or_contain_path("never.md")` is `Ok(TaskIndexRepairOutcome::Repaired)`; heard `[]`. The same two calls for `first.md` after B's rename. Red today.
5. `a_repair_that_found_nothing_still_clears_its_marker`. After fact 1's query `text_write_intents` holds no row for `first.md`, and a second query hears nothing and runs no repair for it. Green today; it guards mutation E.
6. `a_repair_that_found_nothing_leaves_the_generation_alone`. `interaction_generation()` is equal before and after A's `tasks_for_file("first.md")`. Red today (it moves by one).
7. `a_rename_then_a_task_query_keeps_the_graph_index`. One session builds its graph (a graph query), renames `first.md` to `second.md`, and calls `tasks_for_file("first.md")`: the graph index is still live and its generation is the rename's own. Then `tasks_in_vault`: the index is still live, the node set holds `second.md` and not `first.md`, and the index deep-equals `graph_rebuild_reference()`. (The generation is not asserted after `tasks_in_vault`: its re-read of `second.md` follows the poison `UPDATE` at `:9212-9215`, so it stages `MetadataTouched` and the generation moves by one, measured: 3 after the rename, 3 after `tasks_for_file`, 4 after `tasks_in_vault`.) Red today two ways, and the PR body quotes both runs: in a debug build the fact panics at `graph.rs:1025`; with `cargo test --config "profile.test.debug-assertions=false"` it fails on "the graph index is live".
8. `another_sessions_rename_reaches_the_next_feed_window_whatever_was_swept`. A registers a cursor, B renames, A runs `tasks_for_file("first.md")`, and A's window still holds the rename record for `second.md` from `first.md`. Green today and after; it pins the route a host learns by.
9. Guards, green today and after, unedited where they exist: `deleted_file_intents_converge_instead_of_poisoning_queries` and `a_marker_under_a_path_that_names_nothing_is_settled_as_before` (a row stands, the file is gone: `Deleted`), and a new `a_stopped_renames_recovery_still_announces_the_row_it_removed` (#1981's shape, staged as `a_case_only_rename_the_volume_applied_and_the_index_never_recorded_leaves_one_row` stages it at `canonical_identity.rs:866-873`, with held markers planted on both names and a rename made outside the session, but for a plain rename `A.md` to `B.md`: heard `["Deleted A.md", "Modified B.md"]`).
10. `a_repair_that_removes_a_standing_row_still_removes_its_node_and_moves_the_generation`. One session with a live graph (a graph query first) holds `gone.md` and a note that links to it; the file is deleted outside Slate; a held marker for `gone.md` is planted and aged (`plant_marker`); `tasks_in_vault`. Heard `["Deleted gone.md"]`; the graph index is live, holds no node for `gone.md` (the linker's link is unresolved), and deep-equals `graph_rebuild_reference()`; `interaction_generation()` moved by one. Green today and after. It pins the `RemovedRow` branch's graph removal and bump, which this change restructures and which the round-24 guards do not read (they build no graph and read no generation).
11. `a_file_gone_between_the_stat_and_the_read_leaves_the_graph_as_it_was`. A test provider wraps the filesystem provider (the shape of `OneAliasProvider`, `canonical_identity.rs:950`) and answers `stat` for `ghost.md` as a small file while its `read_file` answers `NotFound`; no row stands at `ghost.md`. One session over that provider with a live graph; a held marker for `ghost.md` planted and aged; `tasks_in_vault`. Heard `[]`; the graph index is live, holds no node for `ghost.md`, and deep-equals `graph_rebuild_reference()`; no row stands at `ghost.md` and its marker is gone. Green today (main's `FileRemoved` cancels the staged `FileAdded`), red under mutation C. It pins Design 2's "drop the sink" on the no-row branch.
12. `a_case_only_rename_on_a_folder_that_keeps_case_reports_only_the_stored_spelling`. A vault in a folder that keeps case: on Windows a folder switched with `fsutil file setCaseSensitiveInfo <dir> enable` before any file is created (as step 0 did); where the switch is refused (no privilege, a volume that cannot), the fact prints one line saying so and returns. On Linux the temp folder keeps case as it is. One session renames `A.md` to `a.md` and calls `tasks_in_vault`: heard after the rename's own event is `["Modified a.md"]`. Red today (`["Deleted A.md", "Modified a.md"]`, measured in step 0), so the case-keeping change is seen red on this machine and not only on the Linux lane. If the implementer finds a helper that already makes such a folder (the red team named a `two_files_by_case_vault` pattern), it is used.

**Core, `C/session/tests/canonical_identity.rs`.** The two facts named in step 0 lose their case-keeping branch: on every volume the sweep after a case-only rename now reports the stored spelling's `Modified` and nothing for the old name. Their doc comments (`:730-734`, `:895-898`, "reported deleted first, as always") are rewritten to say so and to name #1683. On NTFS these two take their aliasing branch and are green before and after. Their changed branch is seen red twice: through fact 12 on this machine, and on the Linux lane by the tests-only commit pushed to a scratch branch (or the PR's first push), whose failing lines for both facts the PR body quotes.

**Mutations, each applied alone to the fixed `session.rs`:**

| | Mutation | Turns red |
|---|---|---|
| A | announce `Deleted` when nothing stood | facts 1 to 4 and 12 |
| B | take the no-row branch even when a row stands (remove nothing, announce nothing) | the two round-24 guards, facts 9 and 10 |
| C | apply the graph sink when nothing stood | fact 11 |
| C2 | stage `FileRemoved` and apply the sink when nothing stood (main's graph behaviour) | fact 7 |
| D | bump the generation when nothing stood | fact 6 |
| E | leave the tokens in place when nothing stood | fact 5 |
| F | announce `Modified` when nothing stood | facts 1 to 4 and 12 |
| G | skip the `FileRemoved` staging when a row stands | fact 10 |
| H | skip the generation bump when a row stands | fact 10 |

**Windows, `T/RescanTests.ChangeFeed.cs`.**

13. `AForeignRenameThenAnOwnRenameOfItsDestinationMovesTheTabThere` (`:871-896`): the comment at `:882-884` and `h.Events.Clear()` at `:887` are replaced by `Assert.Empty(h.Spoken)` and `Assert.False(tab.IsMissingFromDisk)`, with a comment that names #1683. The rest of the fact (the rescan moves the tab to `third.md`, not missing) stays. Red today, as measured.
14. A new fact beside it, `AForeignRenameThenAnUnrelatedOwnChangeSaysNothingOfTheOldPath`: seed `first.md`, open it, another session renames it to `second.md`, this session creates `unrelated.md`, drain and settle: nothing spoken, the tab at `first.md` and not missing; then a rescan moves the tab to `second.md`, not missing. Red today (the same sentence, measured).

15. A new fact, `AStrayEventNoLongerDropsAMovedNotesClosedTabAndConnectionsEntries`: the note is open in two tabs, one is closed (so the closed-tab history holds it); `first.md` is pinned in Connections and then another note is pinned, so `first.md` is on the back stack (pinning once makes it the pin, which `Connections.Prune` keeps, `S/WorkspaceViewModel.cs:3798-3802`; the probe did it this way, `p2/RescanTests.Cb12Probe.cs:112-113`); another session renames it; this session creates an unrelated note; drain and settle; then an explicit rescan. The closed-tab entry and the back-stack entry are at `second.md`, and Reopen Closed Tab opens `second.md` with its text, not missing. Red today, measured: after the own create both entries are gone and Reopen Closed Tab cannot run. This is the loss the stray event caused, and it goes in the PR body in those words.

All three use the harness's own condition waits (`Drain`, `Settle`); none adds a wait of its own, so `TestWaitCensus` has nothing new to pin. None reads a file the app writes.

**Existing facts that must stay green, unedited:** all of `RescanTests`, `TasksReviewTests`, the `TaskIndexRepair` facts, `CanonicalIdentityTests`, `FileManagementTests`, `HotExitTests`, and the censuses `A11yTriggerParityCensus`, `AnnouncementSeamCensus`, `SaveChainAdmissionCensus` and `TestWaitCensus`.

## The NVDA spot check

Run by plan 44 §4 rule 4: NVDA 2026.3.1, keys sent with `SendInput` and the speech read from `nvda.log`, as `docs/plans/18_windows_port/reports/nvda_agent_matrix_pass_2026-09-22.md` describes. `docs/runbooks/` has no NVDA runbook; that report is the recipe. Take the shared desktop lock. The owner is not needed: two instances hold one vault when each is started with its own `SLATE_CENSUS_INSTANCE_ID` and its own `SLATE_LOCAL_APP_DATA` root (measured). `scripts/windows/launch-for-field-pass.ps1` starts the first window (A, the one NVDA listens to) and refuses a second (`:77-84`), so the second (B) is started by hand: the same Release `SlateWindows.exe` with the vault's path as its argument and the two variables set. Say so in the PR body.

Run the whole sequence on a build of `main` first, as the base, then on the PR's head.

1. A vault with `first.md`, `other.md` and a third note. In A: open `first.md`, press Ctrl+T (Duplicate Tab), choose `other.md` in the Files tree, so A's tabs are `other | first` and `first.md` is in the background. (With one tab the sequence proves nothing: New note replaces the tab at `first.md`.) In B: select `first.md` in the Files tree.
2. In A press Refresh files and hear "Files refreshed. No changes.". For about five seconds after a scan nothing in A reconciles on its own (the foreground rescan's cooldown, `S/VaultLifecycleViewModel.Rescan.cs:64`, and the watcher's spacing, `C/session/watch.rs:69-71`), and that is the window the defect needs.
3. Go to B: F2, type `second`, Enter. Return to A.
4. In A: File actions, New note, within about five seconds of step 2's sentence.
5. Record everything NVDA says in A from step 4 on, and A's tab names.

Expected on the base: "first.md is missing from disk. Open editor content was preserved." Then, within seconds, the watcher's rescan moves the tab to `second`. Expected on the head: that sentence is not spoken; the tab is never "missing from disk"; it moves to `second` when the watcher's rescan runs, with nothing said (a watcher run applies another session's change silently, `AWatcherDrivenChangeFromAnotherSessionIsAppliedWithoutARefresh`).

If step 4 misses the window on the base, the sentence is not heard and the run looks like the head's: repeat it, up to three times. If it cannot be hit at SendInput's speed, run both builds with `SLATE_TEST_DISABLE_VAULT_WATCHER=1` set for A only: then only the return to A has to fall inside the five seconds, the base keeps the tab "first, missing from disk" until Refresh, and the head keeps it at `first`, not missing, until Refresh moves it and says "Files refreshed. 1 new or changed, 1 removed.". A pass that needed the seam is a weaker witness, and the PR body says which way each run was made. Step 0 raised the sentence on main both ways with a UIA listener and no screen reader, so the unseamed run is expected to work.

6. On both builds, fact 15's shape: in A, open `first.md` in two tabs (Ctrl+T) beside `other.md`, close one of the two `first` tabs (so the closed-tab history holds `first.md` and a tab is still at `first.md`), and press Refresh. In B rename `first.md` to `second`. In A, within the five seconds, File actions, New note; then Refresh, then Reopen Closed Tab. Record what is said. Expected on the base: the missing-from-disk sentence after New note, and after Refresh Reopen Closed Tab has nothing of `first` to reopen. Expected on the head: no missing sentence, and Reopen Closed Tab reopens the note as `second` with its text. With no tab left at `first.md` nothing re-queries the old path, so the two builds behave alike; a run without the second tab proves nothing and is not recorded as this step.

The PR body records the phrases for base and head, and `not delivered:` for any step that could not be driven, with the reason.

## Docs and registers

- **`docs/plans/21_write_intent_protocol_invariants.md`**: one dated section, placed after "The bind step (#1975, 2026-11-16)" (`:173-267`) and before the accepted-risk register (`:269`), as #1982 added its own (the document grows by dated sections and is not frozen). Title: "The not-found arm acts only on a row that stands (#1683, 2026-11-17)". It says: what the arm did (acted and announced whether or not a row stood); the rule now (a row that stands is removed, with its graph removal, the generation bump and `Deleted`; where none stands the arm clears the selected tokens and does nothing else); and a check against each invariant it touches, in the section's own words: INV3 (`:54-59`, "NotFound to deletion convergence (round 23)") still holds, because the marker is cleared in the same commit on both branches; "Repair converges to SCANNER semantics" (`:100-103`) holds more closely than before, because a scan of a path with no file and no row writes nothing and says nothing; the token-conditional clear (round 22) is untouched; the bind step's text at `:188-189` and `:194-195` ("a path that names nothing still converges to deletion", "as the not-found arm removes one") stays true. It also records round 24's rule in this document for the first time (the event's kind matches the outcome), since today that rule lives only in a code comment (`C/session.rs:9277-9281`) and a test (`C/session/tests/tasks.rs:469-472`), and names the facts of this spec. One rule is added to "Rules for future changes" (`:320` on): a repair's effects outside the index (an event, a graph operation, a generation bump) follow from a row it changed, never from the arm it took. The text, which the implementer copies and corrects only where the code as built differs (and says so in the PR body), is in "Doc 21's section, as it reads" below.
- **`docs/plans/32_canonical_file_identity_contracts.md`**: no change. Its notes for #1975 (`:154-181`, `:326-335`; the sentence quoted next is at `:334`) stay true: "A path that names nothing is not a failure: it converges to deletion as before" names a fact in which the row stands. No `*_contracts.md` row says the old names are reported deleted or mentions the sweep's `Deleted` (searched).
- **Comments that become untrue and are rewritten in this PR:** `C/session.rs:9243-9248` and `:9275-9281` (the arm and the notify); `C/session/tests/canonical_identity.rs:730-734` and `:895-898` (the two facts' doc comments); `T/RescanTests.ChangeFeed.cs:882-884`. `crates/slate-uniffi/src/lib.rs` says nothing of a repair's events and is not touched. The comment at `S/VaultLifecycleViewModel.ChangeFeed.cs:775-777` ("A deletion event is not followed: a scan reports another session's rename of that path as one too") is read by the implementer: if it describes the stray event and not the feed's records, its correction is one line on #1955, because this PR edits no product file under `apps/`.

- Contract 38's ledger: no site moves. `python -I scripts/a11y_trigger_ledger.py --check` must exit 0.
- The parity matrix: no change. `python3 scripts/generate-parity-matrix.py --check` must exit 0.
- **#1954**, one comment before the merge, under Behaviour: the chunk, #1683, the PR; "a repair that finds no file and no index row at a path no longer sends `Deleted` for it. On the Mac that event marked no tab and spoke nothing; it scheduled one template availability refresh and, during a rescan, one journal entry. Owed: nothing to hear. A check, when a Mac is at hand, that a note another session renamed is followed by the next rescan as before."
- **#1955**, one comment with the deferred lines: the `Modified` sibling with its two reasons; `launch-for-field-pass.ps1` cannot start a second instance on one vault; a session's generation does not see another session's commits, so an open base serves cached rows until this session re-reads, writes or rescans; the destination's re-read under a graph built before another session's rename meets `debug_assert!` at `graph.rs:841` (a release build drops and rebuilds the index, which is right); the Release `slate_uniffi.dll` is not byte-reproducible from the same sources (two builds, two hashes; the generated bindings are identical); and a sibling of the graph find by reading: `delete_file` stages `FileRemoved` without asking whether a row stood (`C/session.rs:17304-17310`), so a delete of a file with no row under a live graph meets `graph.rs:1025` (Slate's surfaces list indexed files, so this is hard to reach; the failing test: a session with a live graph deletes a file written outside it before any scan, and the graph stays live and equal to a rebuild).
- **#1683**, one comment before the PR opens: the widened scope (one session alone hears it; a folder gives one for each note; the host routes), the measured mechanism, and the graph find with what a user had: in a session with a graph open, the whole graph index was dropped and rebuilt after every rename that a vault-wide task query, or a task query for the old path, followed (and a debug build panicked there).
- Plan 47 §10's spec table and §12's delivery row are the lead's, in the wave's docs PR. This PR does not edit the plan.

### Doc 21's section, as it reads

Placed after "The bind step (#1975, 2026-11-16)" and its "Facts:" line, before "## Accepted-risk register":

> ## The not-found arm acts only on a row that stands (#1683, 2026-11-17)
>
> `reindex_path_locked` converges a path whose file is gone (INV3, round 23). Its not-found arm did four things without asking whether a `files` row stood at the path: it staged the graph's removal of the path, deleted the row, moved the session's generation and announced `Deleted`. After a move that landed (other than a case-only rename on a volume that answers both spellings, which the bind step handles), the sweep repairs both of the move's markers, and the old path then names nothing: no file, and no row, because the move's own commit moved the row. So the first task query that covered the old path after a rename, in the session that made it or in any other session on the vault, announced the old path as deleted, and a host could mark an open tab of the note missing (#1683). Where the session's graph held no node for the path, the staged removal met the graph's `debug_assert!` for a removed file with no node: a debug build panicked, and a release build dropped the whole graph index and rebuilt it on the next graph query.
>
> The arm now reads, in the same writer transaction and before it stages anything of its own, whether a `files` row stands at the path:
>
> - **A row stands.** As before: the graph's removal is staged, the row is deleted, and after the commit the graph ops are applied, the generation moves and `Deleted` is announced.
> - **No row stands.** The selected tokens, if any, are cleared in the same commit, and nothing else is done: the graph sink is dropped without being applied, the generation does not move, and no event is sent. The sink is dropped because `index_file` stages a new path's graph addition before it reads the file, so a file gone between the stat and the read leaves an addition for a row that was never inserted. The path had already converged, so the repair changed nothing outside `text_write_intents` and says nothing. A host learns of another session's move from its next change-feed window, which records it as a rename.
>
> Round 24's rule, recorded here for the first time: the event's kind follows the outcome. A repair that read the file announces `Modified`; a repair that removed a row announces `Deleted`, because a convergence to deletion announced as `Modified` left hosts showing the note as existing. A read that finds the content already indexed still announces `Modified`: the content hash cannot say "unchanged", because a quarantined row keeps its hash while its task rows are gone, and its heal must be announced.
>
> Checked against the invariants:
>
> - **INV2.** Unchanged. The marker is cleared by a repair that read the disk under the writer lock (the read answered not found), and on both branches only the tokens the sweep selected are cleared, in the repair's own commit (round 22). The host routes (`reindex_path`, `repair_or_contain_path`) select no tokens and clear none, as before.
> - **INV3.** Holds. Both branches end with the path converged and the selected tokens cleared. "NotFound to deletion convergence" means that no row stands at the path after the commit, whether the repair removed one or none was there.
> - **Scanner semantics.** Holds more closely than before: a scan that meets a path with no file and no row writes nothing and announces nothing, and now the repair does the same.
> - **The bind step.** Its text above stays true. A path that names nothing still converges to deletion, and the alias row it removes "as the not-found arm removes one" is a row that stands.
> - **INV1, INV4 and the durability rule.** Not touched. The arm plants nothing, contains nothing and precedes no filesystem mutation.
>
> Facts: `session/tests/repair_announcement.rs`, and the two facts that pin round 24 (`deleted_file_intents_converge_instead_of_poisoning_queries` in `session/tests/tasks.rs`, `a_marker_under_a_path_that_names_nothing_is_settled_as_before` in `session/tests/canonical_identity.rs`).

Appended to "Rules for future changes":

> - A repair's effects outside the index (an event, a graph operation, a generation bump) follow from a row it wrote or removed, never from the arm it took (#1683). A repair that finds no row and no file clears its selected tokens, changes nothing else and announces nothing.

**Corrections to the reading** (`reading/03_save_chain_lifecycle.md:169-236`). Every line number moved (`session.rs` by about 500, the Windows fact by 65); none of its claims is false. Four statements were incomplete:

- "The own rename is incidental; any task query on the old path triggers it" understates it: no second session is needed either.
- "Which of those three queries fires in the headless harness" is `EditorInteractions.cs:2532`.
- "announces `Deleted` only when its DELETE removed a row" is the right rule for the event, and the arm has two more effects with the same fault: the graph operation and the generation bump.
- "The pass needs two Slate sessions on one vault" and the open question whether the launcher can start two: it cannot, and two instances can be started by hand with two instance identities.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-cb12'; $env:CARGO_BUILD_JOBS = '8'
cargo fmt --all -- --check
cargo clippy --all-targets --workspace -- -D warnings
cargo test -p slate-core --lib repair_announcement          # red on the tests-only commit, green on the fix
cargo test --config "profile.test.debug-assertions=false" -p slate-core --lib a_rename_then_a_task_query_keeps_the_graph_index
cargo test -p slate-core --lib canonical_identity
cargo test -p slate-core
cargo test -p slate-uniffi -p slate-cli
cargo bench --workspace --no-run
& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh
./apps/slate-windows/generate-bindings.ps1 -Locked            # the generated slate_uniffi.cs has the same SHA-256 as before
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
$t = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
dotnet test $t -c Release --no-build --filter "FullyQualifiedName~AForeignRenameThenAnOwnRenameOfItsDestinationMovesTheTabThere|FullyQualifiedName~AForeignRenameThenAnUnrelatedOwnChangeSaysNothingOfTheOldPath|FullyQualifiedName~AStrayEventNoLongerDropsAMovedNotesClosedTabAndConnectionsEntries"
dotnet test $t -c Release --no-build --filter "(FullyQualifiedName~RescanTests|FullyQualifiedName~TasksReviewTests|FullyQualifiedName~TaskIndexRepair|FullyQualifiedName~CanonicalIdentityTests|FullyQualifiedName~FileManagementTests|FullyQualifiedName~HotExitTests|FullyQualifiedName~ConnectionsLeafTests|FullyQualifiedName~RecoveryAnnouncementTests|FullyQualifiedName~SaveChainAdmissionTests|FullyQualifiedName~W1WorkspaceRedTeamTests)&FullyQualifiedName!~TheModelOf"
dotnet test $t -c Release --no-build --filter "FullyQualifiedName~A11yTriggerParityCensus|FullyQualifiedName~AnnouncementSeamCensus|FullyQualifiedName~SaveChainAdmissionCensus|FullyQualifiedName~TestWaitCensus"
python -I scripts/a11y_trigger_ledger.py --check
python scripts/check_windows_census_trait.py; python scripts/check_windows_docs_trigger.py; python3 scripts/generate-parity-matrix.py --check
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include apps/slate-windows/tests/SlateWindows.Tests/RescanTests.ChangeFeed.cs
```

The two case-keeping branches are proved on the lane that keeps case (the PR body names the job and quotes its line for both facts), and locally by running the first fact's sequence in a folder switched to case-sensitive names, as step 0 did. Rebuild between a mutation and its run. CI: the Rust lanes and `windows.yml` in full (the change is under `crates/`); the swift lane compiles.

## Acceptance

- [ ] Facts 1 to 4, 6, 7 and 12 were seen red on main's `session.rs` and green with the fix; fact 7's two red runs (the debug panic and, with debug assertions off, the dropped index) are both quoted (plan §4 rule 7). Fact 12 ran on a case-keeping folder here (or its printed skip line is quoted and the Linux lane's red is the witness).
- [ ] Facts 5, 8, 9, 10 and 11 and the two round-24 facts are green before and after.
- [ ] Mutations A to H (with C2) each turn the named facts red; the results are in the PR body.
- [ ] The two `canonical_identity` facts' lists are changed with the reason, and the lane that keeps case is green on both.
- [ ] Windows facts 13, 14 and 15 were seen red on main's core and green with the fix; the named classes and censuses pass unedited.
- [ ] The product diff is `crates/slate-core/src/session.rs` only. The generated `slate_uniffi.cs` hash is unchanged. No file under `apps/slate-windows/src` or `apps/slate-mac` is edited. The ledger check exits 0.
- [ ] Doc 21 carries its dated section; #1954, #1955 and #1683 carry their comments.
- [ ] The PR opened as a draft. The NVDA spot check ran on a build of main and on the head with two windows on one vault; its phrases are in the PR body; the PR was marked ready only then. `not delivered: a VoiceOver or Mac run (no Mac; the swift lane compiles, and no Mac source changed)`.
- [ ] Plan §4 rule 9: a red team (a fresh Opus agent at xhigh with `24_red_team_protocol.md`, the diff, this spec and doc 21), then Codex adversarial-review rounds until it approves; the PR body records both with round counts.
- [ ] The PR body has "Found and fixed here" with the graph find in plain words (in a session with a graph open, the whole graph index was dropped and rebuilt after every rename that a vault-wide task query, or a task query for the old path, followed; a debug build panicked there) and its red fact, and ends with the count: found on the way, fixed here, deferred, filed (rule 8).
- [ ] `Closes #1683`, and `gh pr view N --json closingIssuesReferences` shows #1683 and nothing else. No close, fix or resolve word stands before #1981, #1977, #1982, #1954, #1955 or any other number in the body.
- [ ] Every check green on the head the merge is pinned to; Codoki reports Safe to merge at 3/5 or higher after the last push.

## Brief

Work in the worktree `C:\dev\slate\.claude\worktrees\cb12-repair-announces-what-it-changed` on `claude/cb12-repair-announces-what-it-changed` (at `main` eea6e7a2), with `CARGO_TARGET_DIR = C:\dev\slate\target-cb12` and `CARGO_BUILD_JOBS = 8`. Read `docs/plans/21_write_intent_protocol_invariants.md` first, then `C/session.rs:8630-8818` (the sweep), `:8894-9001` (the bind step, for its row-first shape) and `:9168-9292` (`reindex_path_locked`), and check this spec's "Current state" against them. The probe module, the prototype diff and the Windows probe facts from step 0 are kept with the chunk's working notes; start the facts from them. First commit, tests only: the new module `C/session/tests/repair_announcement.rs` with facts 1 to 12 (use `canonical_identity.rs`'s helpers where they fit: `make_vault`, `ChangeRecorder`, `plant_marker`, `vault_tasks`, and `OneAliasProvider`'s shape for fact 11's provider), the two edited `canonical_identity.rs` facts, and Windows facts 13 to 15 in `T/RescanTests.ChangeFeed.cs`. `git add` the new file and run `python3 scripts/apply-license-header.py`. Run them on main's product code and quote the red results: `Deleted` in each list for facts 1 to 4 and 12; the generation moved for fact 6; fact 7's debug panic at `graph.rs:1025` and, with `--config "profile.test.debug-assertions=false"`, the dropped index; the sentence for facts 13 and 14; the lost entries for fact 15; and the green runs of facts 5, 8 to 11. Second commit, the fix, in `reindex_path_locked` only: the row-first not-found arm, the three outcomes in the tail (the `NothingStood` branch drops the graph sink without applying it), the rewritten comments. Do not touch the sweep, the bind step, the quarantine, the host routes, `crates/slate-uniffi` or any product file under `apps/`. Then mutations A to H with C2, one at a time, each quoted with the facts it turns red. Then doc 21's dated section and its one new rule, from "Doc 21's section, as it reads". Run the Verification block; a local Windows filter that takes in `ConnectionsLeafTests` excludes its `TheModelOf` facts. Check each C# file's line endings before and after editing it, and run `dotnet format --verify-no-changes --include` on it. Do not push and do not open the PR: report to the principal with every command and its result, the red and green runs, the mutations, the generated bindings' SHA-256 before and after, anything found on the way with its size by `AGENTS.md`'s rule, and anything in this spec that the tree showed to be wrong. Commit messages follow `AGENTS.md` and end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. The PR title will be `fix(core): a repair that finds no index row and no file announces nothing`.

## The spec's red team (2026-11-17)

A fresh Opus agent at xhigh read this spec against the tree at eea6e7a2 and the step 0 logs: 0 blockers, 5 medium, 9 low. Every finding was checked and taken:

- Medium: Design 2 applied the graph sink on the no-row branch, but `index_file` stages `FileAdded` for a path with no row before it reads, so a file gone between the stat and the read would have left a node for a note that does not exist. The branch now drops the sink, as the prototype did; fact 11 and mutation C pin it, and the step 0 sentence that said the prototype matched the first draft is corrected.
- Medium: fact 7 asserted the graph generation after `tasks_in_vault`, which moves it by one through the destination's `MetadataTouched` (measured in the prototype logs). It now asserts the generation after the old path's query only, and a deep equality with a rebuild after the vault query.
- Medium: the case-keeping change was never seen red here. Fact 12 runs it on a folder switched to case-sensitive names, and the Linux lane's red run on the tests-only commit is quoted.
- Medium: no fact pinned the row-standing branch's graph removal and bump. Fact 10 and mutations G and H do.
- Medium: fact 15's Connections half pinned the note once, which makes it the pin, not a back-stack entry. It now pins a second note after it, as the probe did.
- Low: the host-effect inventory (Quick Open, the reading and Bases surfaces, the graph probe on Windows; the three graph refresh probes on the Mac), two untrue sentences in "What a host no longer gets", NVDA step 6 (it now runs on both builds with a second tab kept at the old path), the title and the Goal (narrowed to a rename that landed), doc 21's text (host routes select no tokens; the case-only alias case; "outside `text_write_intents`"), doc 32's line numbers, `delete_file`'s sibling staging (one line on #1955), the #1683 comment's scope, and one ambiguous table row.

## What delivery changed (2026-11-17, PR #1996)

The implementation followed this spec, with these departures:

- **Fact 11 asserts the graph generation and the silence.** As first written it was green on `main`, because `main`'s `FileAdded` then `FileRemoved` leave a graph equal to a rebuild. Codex's first round found that it could not tell `main` from the fix. It now asserts, first, that the graph's generation did not move, then the graph, the row, the marker and that nothing was announced; on `main` it fails at the generation (`left: 2 right: 1`). Mutations A, E and F also turn it red.
- **Facts 13 to 15 wait on the old path's task re-query** before they assert that nothing was said: `main`'s stray event comes from `TasksForFile` on a pool thread, so a fixed settle could let `main` pass on a loaded host. The wait reads `ArtifactCacheLoadCountForTests`, which moves only after `TasksForFile` returns, so it is also the premise that the re-query ran.
- **Names.** The enum is `ReindexOutcome`, not `RepairOutcome`, so it is not confused with the public `TaskIndexRepairOutcome`. Every path in the new module carries `1683`, because the fault seams' triggers match path substrings across the whole test process. Fact 12 uses `A1683.md` and `a1683.md`.
- **Helpers.** `canonical_identity.rs`'s recorder and marker helpers are private to that module, so the new module carries its own; moving them to `common.rs` was declined, so that `canonical_identity.rs` changes only in its two lists.
- **Doc 21.** "A repair that read the file announces `Modified`" reads "A repair that indexed the path again from disk announces `Modified`": the oversized branch indexes metadata without reading the body.
- **`S/VaultLifecycleViewModel.ChangeFeed.cs:775-777`** describes the feed's records (a rename's window carries a `Removed` record for the old path), not the stray event. It stays true, so #1955 has no line for it.
- **The NVDA pass** ran unseamed on the first try for each build. Window A started through the launcher, which takes `-Exe`. Step 6's tab order read `[first] [other]*`, which still leaves `first.md` in a background tab. The pass found #1995: a launcher root sits directly under `%TEMP%`, whose flush is refused, so such a window runs with no hot-exit store.
- **Reviews.** The diff's red team found 0 blockers, 1 medium and 10 low. Codex, at the configured effort high, found one medium in round 1 (fact 11) and approved in round 2.
