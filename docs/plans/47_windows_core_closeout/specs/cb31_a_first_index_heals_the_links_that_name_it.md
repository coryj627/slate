# CB-31: A file first indexed by a save, a repair or an open heals the dangling links that name it

Issue: #1704. Size M. Lanes: rust (the change and its facts), windows (a change under `crates/` runs the lane in full; one twin fact), swift (compile and the lane's suite; no Swift edit). AT: none (the plan row owes none; no sentence changes). Wave: C8, slot 1. Parallel with: CB-15b (canvas, graph and search failure detail) and CB-29 (the SVG cost guard); neither edits a file this chunk edits. Decision: none. Interlock: `session.rs` goes CB-8, CB-12, CB-9b (merged), CB-31, then CB-32, CB-33 and CB-45b (plan §6); `links_db.rs` goes CB-31 (lightly), then CB-32 and CB-33. Review: a red team, then Codex adversarial-review rounds until it approves (plan §4 rule 9).

Written 2026-11-19 from `reading/07_core.md:60-80` and `:255-270`, the issue, and a step 0 run. Every `path:line` below was re-read at 16ad788d (`main` on 2026-11-19, after #2018). Step 0 ran at 16ad788d in this chunk's worktree with a Cargo target directory of its own, as temporary facts in `crates/slate-core/src/session/tests/save_link_lookup.rs`, removed with `git checkout` afterwards. The probe source, its diff and one output file per probe are kept with the chunk's working notes. Statements still by reading say so.

`S` is `crates/slate-core/src/session.rs`; `L` is `crates/slate-core/src/links_db.rs`; `T` is `crates/slate-core/src/session/tests/save_link_lookup.rs`; `W` is `apps/slate-windows/`.

## Goal

When a path the index does not hold gets a `files` row (a path never seen, or one deleted and written again), by a save, a repair, an open base or an open canvas, the dangling link rows that could name it are healed in the same transaction, exactly as a create heals them today, and each healed source's linkset is replayed to the graph. A scan still heals once, at its tail, and runs no per-file heal. A save, repair or open of a path that already had a row runs no heal.

## Drift check (the reading against 16ad788d)

The reading was written at 1ec9bf31. `S` has grown by about 600 lines since (CB-8, CB-12, CB-9b, CB-13, CB-14).

| Reading's claim | At 16ad788d |
|---|---|
| Create: `create_exclusive_binding` `:6467`, index at `:6547`, heal `links_db::heal_after_create` `:6570`, graph replay `:6571-6582`, the cited comment `:6559-6569` | `:6728`; index at `:6808`; heal `:6831`; replay `:6832-6843`; comment `:6820-6830`. The heal is `L:540` `heal_after_create`, which calls `L:604` `re_resolve_unresolved_links_naming` (the reading named the inner function) behind the test seam `CREATE_HEAL_VAULT_WIDE` (`L:122`) |
| Save: `save_text_locked_checked` calls `index_saved_file` at `:5006`; `index_saved_file` `:5384`; `FileAdded` with `graph_inbound_rows` `:5401-5408` | Call at `:5267`, inside the change feed's write scope (`:5266`); `existed` is an exact-path `files` row check (`:5186-5192`); `index_saved_file` `:5645`; `FileAdded` `:5662-5669`; `graph_inbound_rows` `L:450-470` reads rows whose `target_path` already equals the path, not the NULL ones. Holds |
| Repair: `reindex_path_locked` `:8681` → `index_file` `:8740`; callers `reindex_path` `:7777`, `repair_or_contain_path` `:7806`, `repair_abandoned_intents_locked` `:8488` | `:9168` → `:9227`; callers `reindex_path` `:8038`, `repair_or_contain_path` `:8073`, and the sweep (`repair_abandoned_intents_locked`, defined at `:8630`, whose per-path call is `:8809`). #1683 (CB-12) added the `NothingStood` outcome (`:9258-9276`, `:9321-9328`) |
| Open base: `ensure_open_base_indexed` `:19351` → `index_file` `:19375` | `:19927` → `:19951`. Holds |
| Open canvas: `open_canvas` `:21842`, its own `INSERT INTO files` `:21866` | `:22436`; insert `:22460`; `FileAdded` staged `:22481-22487`. Holds |
| `index_file` `:11381`, `replacing_existing_file` `:11442`; the scan calls it at `:11019` and heals once at its tail `:11136` | `:11904`, `:11965`; the scan's call `:11542`; the tail heal `:11659` (`re_resolve_unresolved_links_under`). On a cold scan every internal link dangles until that tail, because the walk resolves against a snapshot of `files` taken before it starts (`:11453-11462`) |
| Production `INSERT INTO files` sites: `:5443`, `:11579`, `:11653`, `:21866` | `:5704`, `:12102`, `:12176`, `:22460`, and no others (every other match under `crates/slate-core/src` is inside a `#[cfg(test)] mod tests`, checked file by file) |
| Every `UPDATE files SET path` sits in a transaction that heals vault-wide | Holds: `:14306` (heals `:14325`), `:14651` (`:14671`), `:15612` (`:15632`), `:18086` and `:18116` (`structural_move_file`) and `:19238`, `:19313` (`rename_prefix_in_index`, from `structural_move_folder`), both moves ending in `finish_structural_move`, which heals at `:18199`. So the issue's list of four is complete |
| The test file and the create fact `a_create_heals_only_the_dangling_rows_that_could_name_it` `T:581` | Holds at `T:581`; the helpers `make_vault`, `outgoing_targets` `T:84`, `heal_rows_visited` `T:560`, `link_rows_where` `T:566` |
| "Mac work owed: none to verify by ear. The Mac runs a scan after every structural change, so it already heals on that scan" (`07_core.md:77`) | True of the Mac's two first-save routes (the saved-query export and Convert card to note each run a full scan right after, `apps/slate-mac/Sources/SlateMac/Bases/AppState+Bases.swift:908`, `apps/slate-mac/Sources/SlateMac/Canvas/AppState+CanvasExtras.swift:354`). Too broad as written: a Mac save that carries a content hash runs no scan (`AppState.swift:14990-14993`), and nothing follows the abandoned-intent sweep. #1954 already carries #1704's line ("links that used to stay unresolved now resolve"), which stays true |
| The issue: "The Windows shell never runs a scan after a structural change" | True of the moment after the change. Windows does rescan on Refresh, on the main window's return to the foreground (`W/src/SlateWindows/ForegroundRescanRoute.cs:71-96`, away at least 2 s, at least 5 s after the last scan, `VaultLifecycleViewModel.Rescan.cs:64`) and on the watcher's run for changes made outside the session. The watcher never answers Slate's own writes (`S:948-958`) |

## Step 0, as run on main (16ad788d), 2026-11-19

Each probe scans a vault in which `A.md` holds `[[Draft]] [[Board.canvas]] [[Table.base]]` (all dangling), builds the graph so its incremental hooks are live, runs one route, records the state, runs `rescan_with_progress` (control C2) and records again. Recorded per route: `outgoing_targets(A.md)`; the public `backlinks(new, first 50)` (the query behind both hosts' Backlinks panels: Windows `RightPanePanelsViewModel.cs:465`, Mac `AppState.swift:13242`); the new path's `files` rows; whether the graph equals a fresh rebuild, its ghosts and `A.md`'s edges; `graph_connections_tree` for both paths (the Connections leaf's query); `HEAL_ROWS_VISITED`.

| Probe | Route | After the route | After a rescan |
|---|---|---|---|
| C1 | `create_exclusive("Draft.md")` | resolves; Backlinks `["A.md"]`; ghost gone; visited 1 | unchanged |
| P1a | `save_text("Draft.md", …, None)`, no file on disk | **dangles**; Backlinks `[]`; ghost `draft` kept, edge `A.md -> Ghost`; Connections of `A.md`: `Draft`, kind Ghost, no path; visited 0 | heals |
| P1b | `Draft.md` on disk but not indexed, then `save_text(…, Some(disk hash))` | **dangles**, as P1a | heals |
| P1c | `export_saved_query_as_base(id, "Table.base")` (core writes through `save_text(path, text, None)`, `S:20212`) | **dangles**, as P1a | heals |
| P2a | `Draft.md` on disk, then `reindex_path("Draft.md")` | **dangles** | heals |
| P2b | the same, then `repair_or_contain_path("Draft.md")` (outcome Repaired) | **dangles** | heals |
| P2c | a save faulted after its write (`SLATE_TEST_FAULT_AFTER_WRITE`, `SavedButUnindexed`, no row), then `note_tasks` with the abandon threshold at zero: the abandoned-intent sweep first-indexes it | **dangles** | heals |
| P2d | `create_exclusive_reporting` faulted after its write (`PublishedUnindexed`; its heal rolled back with it), then `repair_or_contain_path`: the Windows repair coordinator's sequence | **dangles** | heals |
| P3 | `Table.base` on disk, then `open_base("Table.base")` | **dangles** | heals |
| P4 | `Board.canvas` on disk, then `open_canvas("Board.canvas")` | **dangles** | heals |
| C3 | P1a, then a hashed save of the source `A.md` | the source's save re-resolves its own links: resolves; visited 0 | not run |

In every red probe the new path has exactly one `files` row and the graph equals a fresh rebuild: the graph replays SQLite faithfully, and SQLite is what stays stale. The rescan's heal is vault-wide and does not touch `HEAL_ROWS_VISITED`.

P5, the scan's heal (release, 3 rounds, a fresh vault each): 2,000 notes in 20 folders, each linking the next, the first 200 also linking a target that never exists. Cold scan 597 to 657 ms (median 612); `HEAL_ROWS_VISITED` during the scan 0; the tail's vault-wide pass visits 2,200 rows (every internal link plus the 200 ghosts). Two thousand naming heals run after the scan over that cache cost 21 to 24 ms with nothing to fix and 43 to 45 ms with every internal link set dangling first (2,000 rows read), so a naming heal costs about 11 to 22 microseconds here. Debug numbers are about twice these.

Who reaches the bug today (by reading; the step 0 notes have file and line for each):

- **Windows, the saved-query export** to a new `.base` path (Queries dock, Export: `W/src/SlateWindows/MainWindow.Bases.cs:349-375`, `WorkspaceViewModel.Bases.cs:1760`). Nothing in the flow rescans. Until a Refresh, a return to the window, a move, a save of the linking note or a reopen, `A.md`'s Connections leaf shows a ghost, the export's Backlinks are empty, and the reading view draws the link as "Unresolved link" and refuses to follow it. This is the route a user hits.
- **Windows, the repair coordinator** (`TaskIndexRepairCoordinator.cs:176`, `:232`, run at a Tasks panel load): it first-indexes a new note whose create published but did not index (P2d). Reached only after an index failure.
- **Windows, the Bases view's Export** onto a vault file that is on disk but not indexed yet (`WorkspaceViewModel.Bases.cs:950`, P1b). Narrow.
- **The CLI's `slate write --create`** (`crates/slate-cli/src/commands/write.rs:134-142`) is a `save_text` of a new path. A running app hears it as an outside change, and the watcher's rescan heals it.
- **The Mac's** two first-save routes are each followed by a full scan, so the row dangles for one scan's length.
- Not reachable from normal use on either host, by reading: an editor save of a path the index lacks (a hashless save is a create), `reindex_path` (no host or CLI caller), and P3 and P4 (the tree, switcher, links and embeds all read the index).

## Design

1. **One helper in `L`, replacing `heal_after_create`.** `pub(crate) fn heal_rows_naming_first_indexed(tx: &Transaction, path: &str, graph_sink: &mut GraphOpSink) -> Result<(), VaultError>`: it runs `re_resolve_unresolved_links_naming(tx, path)` (or, under the renamed test seam, the vault-wide `re_resolve_unresolved_links`) and, when the graph sink is live, stages one `LinksetChanged` per healed source with `graph_linkset_for`: the create's `S:6831-6843`, lifted out unchanged. The seam `CREATE_HEAL_VAULT_WIDE` is renamed `FIRST_INDEX_HEAL_VAULT_WIDE` and its doc says any first index; its two uses in `T` (`:727-729`, `:863-869`) follow. A new `#[cfg(test)]` thread-local, `FIRST_INDEX_HEALS`, beside `HEAL_ROWS_VISITED`, counts the helper's calls on entry, so a fact can tell "no heal ran" from "a heal ran and read nothing" (the red team's B1 and B2: a heal that runs on a path whose naming rows already resolve reads 0 rows, because the heal's statement filters `target_path IS NULL`). The helper imports `GraphOp` and `GraphOpSink` from `crate::graph`. The docs that call this "a create's heal" are corrected: `HEAL_ROWS_VISITED` (`L:115-116`), `RESOLUTION_ROWS_VISITED` (`L:89-92`), `re_resolve_unresolved_links_naming` (`L:594`), `FoldedNameLookup` (`L:193-199`) and `LinkCandidates` (`L:130-133`).

2. **`index_file` says whether it wrote the path's first row.** Its return type becomes `Result<bool, VaultError>`: `false` from the fast path (`S:12041`, which needs an existing row), `!replacing_existing_file` from the oversized branch (`S:12154`) and from the normal end (`S:12274`). These are its only `Ok` returns, and every `Ok` after a missing row follows an upsert, so `true` always means a row was written (red team, checked). Its doc says what the `bool` means. The scan's call (`S:11542`, `if let Err(e) = index_file(…)`) compiles unchanged and ignores the value; the scan keeps healing once, at its tail. The reading suggested this shape (`07_core.md:72`); the alternative, a `SELECT EXISTS` before each caller's `index_file`, is Call 1.

3. **The four routes call the helper, each inside its own transaction, after the row exists:**
   - **Save and create.** `index_saved_file` (`S:5645`) calls the helper at its end, after `stamp_index_epoch` and before `Ok(file_id)`, when `!existed`. That serves the save (`S:5267`) and the create (`S:6808`); the create's own call and replay (`S:6820-6843`) are deleted and its comment moves onto the helper. The relative order the create had is kept: the file's own links resolve first, then the rows naming it heal, then the create's `bind_log` update. A heal failure is a failure of `index_saved_file`, so the save's transaction rolls back and the save returns `SavedButUnindexed` (`S:5278-5281`), exactly as the create already returns `PublishedUnindexed` (`a_create_whose_heal_fails_is_published_unindexed_and_commits_nothing`, `T:759`). The heal runs inside the save's change-feed write scope (`S:5266`); the feed's triggers are on `files` and `dirs` only (`migrations/040_change_feed.sql:82-131`), and the heal updates `links` alone, so the feed is unchanged.
   - **Repair.** `reindex_path_locked` (`S:9168`): the `Ok` arm of its `match index_file(…)` (`S:9240`) calls the helper when `index_file` returned `true`, before the intent tokens clear. The `NotFound` arms are unchanged. The repair's announcement is unchanged (Call 4).
   - **Open base.** `ensure_open_base_indexed` (`S:19927`) calls the helper after `index_file` when it returned `true`, before its commit. A base whose row exists without its `file_meta` row is re-indexed as today and heals nothing.
   - **Open canvas.** `open_canvas` (`S:22436`): the `None` arm calls the helper after its `INSERT` and its `FileAdded` (so the graph hears the node before the edges that heal onto it), before `replace_canvas_for_file`.
   - **When the heal fails** (only a SQLite-class error can, since the heal reads the database alone), each route fails as loudly as the create does today, with its whole transaction rolled back: the save returns `SavedButUnindexed` and the intent marker repairs it later; a repair returns the error from `reindex_path_locked` with its intent tokens still set (`repair_or_contain_path` contains only `Io`, `InvalidUtf8`, `FileTooLarge` and `InvalidPath`, `S:8118-8133`, so a heal failure is never contained, and the sweep's `contain_repair_failure_locked` lets the task query fail); `open_base_cancellable` fails with no handle (`S:19990`); `open_canvas` fails with no row and no handle. Failing a user's open on a database failure is intended: the same database failure fails the open's own index write today. Fact 6b pins one of them.

4. **What is not changed.** The scan (`index_file` never heals; the tail pass stays the scan's only heal, because a per-file heal during a walk would resolve against the files walked so far and the tail re-resolves only rows still NULL, so a link could keep a worse target). The same narrow effect exists, and is accepted as the create's already is, wherever several paths are first indexed one after another outside a scan: the abandoned-intent sweep's per-path loop (`S:8765-8811`) and a crashed folder move's recovery through held markers each heal against the paths repaired so far, so a bare `[[shared]]` could take the first of two equally named files to arrive, where a scan would pick by the resolver's tiebreak among both; a later scan does not revisit a row that resolves. The moves and renames (each already heals vault-wide). The resolver and the heal's keys (WB-19's `re_resolve_unresolved_links_naming` and its fold rules). Links that already resolve to another file are not re-resolved when a better candidate arrives: the create does not do that either, and the issue does not ask for it. No FFI change, no event, no sentence, no Swift edit.

5. **Cost.** One heal is two indexed reads through migration 041's partial indexes plus one lookup per row that could name the path: about 11 to 22 microseconds in release on step 0's vault (P5). Only a path's first index pays it.

`S` functions touched: `index_saved_file`, `create_exclusive_binding`, `reindex_path_locked`, `ensure_open_base_indexed`, `open_canvas` and `index_file` (its return value only). `scan_vault` is not edited.

## Tests

Every new core fact is in `T`, beside `a_create_heals_only_the_dangling_rows_that_could_name_it`. **Red first, against an inert surface:** the new counter `FIRST_INDEX_HEALS` and the renamed seam do not exist on `main`, so the branch's first commit adds them inert (the counter declared and read by no product code, the seam renamed) with no behaviour change; the facts are seen red on that commit, "main plus the inert surface", for the reasons below, then the behaviour lands. Facts 1 to 6b also fail on `main`'s behaviour for the same reason (step 0). No fact uses a fixed wait.

**The graph in facts 1 to 5** (the red team's M1): `graph_sink()` stages only when the graph exists (`S:2047-2050`), and `with_graph` builds a fresh graph from SQLite, so a graph check is vacuous unless the graph is built before the route. Each of facts 1 to 5 builds it first (`with_graph`), runs the route, asserts `graph_is_built()` before any further `with_graph`, then asserts the graph `deep_equals` `graph_rebuild_reference()` (as `session/tests/graph.rs:38-44` `assert_matches_rebuild` does), that it holds no ghost for the new path, and that `A.md`'s Connections leaf (`graph_connections_tree(A.md, 1, …)`) lists the target with its path.

1. `a_first_save_heals_the_dangling_rows_that_name_it`, two arms: `save_text("Draft.md", …, None)` with no file on disk (P1a), and `Draft.md` written by the provider after the scan, then `save_text(…, Some(disk hash))` (P1b). Each: `outgoing_targets(A.md)` is `[Some("Draft.md"), None, None]`; `backlinks("Draft.md")` lists `A.md`; `FIRST_INDEX_HEALS` is 1 and `HEAL_ROWS_VISITED` is 1; the graph as above. Red: the link dangles and both counters are 0 (step 0). Mutations: drop the call in `index_saved_file`; drop the helper's graph staging (the graph no longer equals its rebuild).
2. `a_saved_query_export_heals_the_links_that_name_it` (P1c, core's own route through `save_text`). Facts 2 to 5 make fact 1's assertions for their own target (links, Backlinks, both counters at 1, the graph). Red on `main`. Mutation: as 1.
3. `a_repair_that_first_indexes_a_file_heals_the_dangling_rows_that_name_it`, four arms: `reindex_path` (P2a), `repair_or_contain_path` (P2b), the abandoned-intent sweep through `note_tasks` (P2c, under `fault_guard()` with a distinctive path, because the fault seams match a path substring in every session of the test process, `session/tests/common.rs:19-23`) and a `PublishedUnindexed` create repaired by `repair_or_contain_path` (P2d). Red on `main` in every arm. Mutation: drop the call in `reindex_path_locked`.
4. `an_open_base_that_first_indexes_a_base_heals_the_dangling_rows_that_name_it` (P3). Red on `main`. Mutation: drop the call in `ensure_open_base_indexed`.
5. `an_open_canvas_that_first_indexes_a_canvas_heals_the_dangling_rows_that_name_it` (P4). Red on `main`. Mutations: drop the call in `open_canvas`; call it before the `FileAdded` stage (the graph's replay of an edge onto a node it does not hold yet no longer equals its rebuild, or `debug_assert!` fires, `graph.rs:841`, `:943`).
6. `a_first_save_whose_heal_fails_is_saved_but_unindexed_and_commits_nothing`, the save's twin of `T:759`: three notes link `[[fresh]]`; `LOOKUP_FAULT` on; `save_text("fresh.md", "# fresh\n", None)` returns `SavedButUnindexed` with the landed hash; the bytes are on disk; no `files` row for `fresh.md`; all three rows still dangle. Red: with no heal the save never meets the fault and commits (`LOOKUP_FAULT` gates only `for_each_folded_candidate`, `L:241`, and `# fresh` has no links of its own). Mutation: swallow the helper's error in `index_saved_file`.
6b. `an_open_canvas_whose_heal_fails_opens_nothing_and_commits_nothing` (Design 3's failure paragraph): `Board.canvas` on disk after the scan, a note linking `[[Board.canvas]]`; `LOOKUP_FAULT` on; `open_canvas("Board.canvas")` is `Err`; no `files` row for it; the row still dangles; no handle (a second `open_canvas` after the fault is off succeeds and heals). Red on `main`: the open succeeds. Mutation: swallow the helper's error in `open_canvas`.
7. `a_path_already_indexed_runs_no_heal`, two guards (the red team's B1: a qualified `[[missing/Draft]]` sits in the path index only, `L:575-579` and `db.rs:391-404`, so no heal of `Draft.md` would ever read it, and a counter of rows read could not see a wrongly placed heal). The vault holds an indexed `Draft.md`, `Table.base` and `Board.canvas`, and `A.md`'s `[[Draft]] [[Table.base]] [[Board.canvas]]` resolve; the fact then sets those three rows' `target_path` to NULL with one `UPDATE links` (rows only a heal or a rescan would touch; no trigger on `links` exists). Then, in turn, a second save of `Draft.md`, a `reindex_path` of it, an `open_base` of `Table.base` after its `file_meta` row is deleted (so `ensure_open_base_indexed` re-indexes an existing row), and an `open_canvas` of `Board.canvas` after its `file_meta` row is deleted. After each: `FIRST_INDEX_HEALS` is 0 and the three rows are still NULL. Positive control at the end: a direct `re_resolve_unresolved_links_naming` of each path heals its row, so the manufactured rows were healable. Green on `main`; it pins "first row only". Mutations: heal when `existed`; heal on `index_file`'s `Ok` whatever it returned; heal in `open_canvas`'s `Some` arm.
8. `a_cold_scan_runs_no_per_file_heal`: `a.md` holds `[[b]]` and `b.md` holds `[[a]]`, so whichever is walked second finds the first one's dangling row whatever the walk order; a cold `scan_initial` leaves `FIRST_INDEX_HEALS` and `HEAL_ROWS_VISITED` at 0, and both links resolve. Green on `main`; it pins point 4. Mutation: call the helper inside `index_file` whenever it writes a first row.
9. **A create heals once.** `a_create_heals_only_the_dangling_rows_that_could_name_it` gains one assertion, `FIRST_INDEX_HEALS` is 1. A second heal on the same route reads no rows (the first already resolved them), so only the call counter can see it (the red team's B2). `heal_after_create` itself is deleted, so a leftover call of it does not compile. `a_create_heals_exactly_what_the_vault_wide_heal_heals` and `a_create_whose_heal_fails_is_published_unindexed_and_commits_nothing` keep their meaning through the renamed seam. Mutation: call the helper twice in `index_saved_file`'s create path.

**The Windows twin** (plan §7, "Core: … the Windows twin fact"), Call 3: `W/tests/SlateWindows.Tests/BasesRedTeamRegressionTests.cs`, beside `TheSavedQueryExportCannotNameANote` (`:399-418`): `TheSavedQueryExportHealsTheLinksThatNameIt`. The fixture's shared vault is left as it is; the fact writes its own linking note first (`_session.CreateExclusive("Linker.md", "[[Exported.base]]\n")`, whose link dangles), then saves a query, calls `workspace.ExportSavedQueryAsBase(id, "Exported.base")` (the Windows route's own view-model method, no dialog), and asserts `_session.OutgoingLinks("Linker.md")` resolves to `Exported.base` and `_session.Backlinks("Exported.base", …)` lists `Linker.md`. Red on `main` (the same core call as P1c). It reads no census path, so it carries no census trait; it opens no window.

## Files

- `crates/slate-core/src/links_db.rs`: the helper in place of `heal_after_create`; the seam's name; the `FIRST_INDEX_HEALS` counter; the five docs listed in Design 1.
- `crates/slate-core/src/session.rs`: the six functions listed under Design, and `index_file`'s doc.
- `crates/slate-core/src/session/tests/save_link_lookup.rs`: facts 1 to 8 and 6b; fact 9's assertion; the seam's two uses.
- `apps/slate-windows/tests/SlateWindows.Tests/BasesRedTeamRegressionTests.cs`: the twin.

Not edited: `crates/slate-uniffi` (no FFI change), any Swift or C# product source, `a11y.rs` and the corpus, contract 38's ledger (no `A11yEvent` moves; `--check` is run), any manifest or lockfile, any workflow, the parity matrix, help documents. No contracts document has a row for #1704 (a search of `docs/plans/*_contracts.md`).

## Overlap

- **This wave.** CB-15b edits the Windows shell's canvas, graph and search sources and `ShellFailureDetailCensus`; CB-29 edits `SvgPreviewGuard.cs`, `EditorInteractions.cs`, `diagram.rs` and their tests. None of them edits a file in this chunk's list. The twin's test file is not one CB-15b's spec names (to be confirmed against CB-15b's spec when the lead shares it).
- **Next waves.** CB-32 (`session.rs` if its rebuild starts there, `links_db.rs`, `db.rs`) and CB-33 (`session.rs`, `links_db.rs`) follow; this chunk changes `index_file`'s return type, which CB-32's rebuild may call.

## Docs and registers

- **#1954 (Mac work owed):** no new line. Its #1704 line already reads "links that used to stay unresolved now resolve"; core's change gives the Mac the heal on its hashed saves and its sweep, which run no scan today. No Mac user hears anything different.
- **#1955 (deferred finds):** Call 4's line, if the lead takes it.
- **#1704:** closes on this PR. Step 0's table goes in a comment on it before code.
- **Plan 47 §12:** the delivery row, in the lead's docs PR, with the two reading corrections from the drift table (the tail heal's line and the Mac note).
- **Found on the way, predicted:** found 2 (Calls 4 and 5), fixed here 0, deferred 1 (Call 4), filed 1 (Call 5, if its fact is red), if both calls go as recommended.

## Calls for the lead

1. **How `index_file` reports a first row.**
   - **(a) Recommended:** its return type becomes `Result<bool, VaultError>` (Design 2). The fact that decides the heal is the one `index_file` already holds (`replacing_existing_file`), read in the same statement that wrote the row. Cost: three return points and two call sites in `S` (the scan's call compiles unchanged); CB-32 meets a changed signature if it calls `index_file`.
   - (b) Each caller runs `SELECT EXISTS(SELECT 1 FROM files WHERE path = ?1)` before `index_file`, in the same transaction. Leaves `index_file` alone; adds one query to each repair and open base, and a second place that encodes "first row".
2. **Where the helper lives.** Recommended: in `L`, beside the naming heal it wraps, so `S` gains one call per route and loses the create's twelve lines. Cost: `L` imports `GraphOpSink`. The alternative is a private function in `S` beside `index_saved_file`.
3. **The Windows twin.** Recommended: one fact, `TheSavedQueryExportHealsTheLinksThatNameIt`, for the one Windows route a user reaches. Cost: a Windows test edit, so a Windows build, the class run, `dotnet format --include` on the file, and the whole unit run under the desktop lock before the push (about 40 minutes). The repair route's twin (`TasksPanelTests`, through `EnvFaultLock`) is optional and not recommended: it needs an index failure to start, and core's P2d fact covers the call. No twin for P3 or P4: no Windows action reaches them.
4. **A repair that first-indexes a path announces Modified** (`S:9318-9320`, `ReindexOutcome::Reindexed => FileChangeKind::Modified`), where a save of a new path announces Created (`existed`, `S:5184-5192`). On Windows a Modified for a path the host never listed invalidates a tab at that path and does not reseat missing tabs, which a Created does (`W/src/SlateWindows/VaultLifecycleViewModel.FileChanges.cs:41-61`); on the Mac the template list refreshes on `.created` and returns on `.modified` (`apps/slate-mac/Sources/SlateMac/AppState.swift:25303-25309`; the red team read that one site, not the Mac's other handlers). Found by reading for this spec and confirmed by the red team's reading; not run. It is not this chunk's feature (the feature is the heal, not the event's kind). It is a core change both hosts would see, so by plan §4 rule 5 it is not taken inline. Recommended: one #1955 line naming the failing test (a repair of an unindexed path on disk announces `Created`, through the listener the repair-announcement tests use, `session/tests/repair_announcement.rs`). Cost: none here.
5. **An open reading view does not redraw a link that heals.** By reading, the Windows reading view refreshes on a vault event only for an embed dependency, a Created or Renamed while it has an unresolved embed, or a Markdown change while it shows a base embed (`W/src/SlateWindows/Reading/ReadingContentViewModel.cs:1409-1416`, `:1430-1451`); a wikilink's resolved state comes from the link records at projection time. So after any heal (the create's today, and every route this chunk adds), an open reading view of `A.md` keeps drawing `[[Draft]]` as "Unresolved link" until something else refreshes it; whether activation then refuses to follow it (`ReadingActivation.cs:101-110`) was not run. The fix is Windows host code in `Reading/`, another subsystem, which CB-15c and CB-20 own in later waves. Recommended: verify it with one failing Windows fact during delivery and, if it is red, file one Windows issue (it predates this chunk: the create route has it today); otherwise drop it. Cost: one fact run, one issue.
6. **Step 0's comment on #1704** goes up now, with the table above, before any code. Recommended.

### Lead rulings (2026-11-19)

1. (a): `index_file` returns `Result<bool, VaultError>`. The PR body names the signature change, and the lead's docs inputs gain a §6 line so CB-32 meets it knowingly.
2. Accepted: the helper lives in `links_db.rs`.
3. Accepted: one Windows twin, `TheSavedQueryExportHealsTheLinksThatNameIt`, then the class run, `dotnet format --include`, and the whole unit run under the desktop lock. Every other route gets a core fact, each seen red on `main`.
4. **Departure from the recommendation: fixed here, red first.** The fix stays in `session.rs`, which the PR already changes, so by `AGENTS.md` it is small, and it sits beside this chunk's feature (a first index is what the PR teaches core to recognise). So `reindex_path_locked` announces `Created` when `index_file` wrote the path's first row and `Modified` otherwise (the `RemovedRow` and `NothingStood` arms unchanged). Fact 10 (below) pins it in core. The Windows effect (a missing tab at the path reseats on `Created`, `VaultLifecycleViewModel.FileChanges.cs:58-61`) gets a fact if one is cheap, else it is read and the PR says so. The Mac's template-list refresh (`AppState.swift:25303-25309`) is checked by reading and gets a #1954 line. If anything VoiceOver hears changes, the PR body writes `not delivered: VoiceOver pass`. It goes under "Found and fixed here".
5. Accepted: one failing Windows fact during delivery. Red on head: file one Windows issue (labels `bug`, `windows`) and drop the fact from the PR. Green: drop the item and say so.
6. Accepted: step 0's table goes on #1704 now, with no close word before any issue number.

Added by ruling 4:

10. `a_repair_that_first_indexes_a_file_announces_it_created` (in `session/tests/repair_announcement.rs`, beside CB-12's facts, through the listener they use): `Draft.md` on disk after the scan, then `repair_or_contain_path("Draft.md")` and, in a second arm, `reindex_path`: the listener hears one `Created` for `Draft.md`; a repair of an indexed path still hears `Modified`. Red on `main`: it hears `Modified`. Mutation: announce `Modified` for every `Reindexed`. Every existing repair-announcement fact is reread for a first-index arm that pinned `Modified`; any that did is changed in the same commit and named in the PR.

The predicted count becomes found 2, fixed here 1 (ruling 4), deferred 0, filed 0 or 1 (ruling 5).

## Verification

```powershell
$env:CARGO_TARGET_DIR = '<this worktree's own target directory>'
cargo test -p slate-core --lib session::tests::save_link_lookup
cargo fmt --all -- --check
cargo clippy --all-targets --workspace -- -D warnings
cargo nextest run --workspace   # make test-fast's sweep; slate-cli's tests drive save_text too (write --create)
cargo bench --workspace --no-run
& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh
python -I scripts/a11y_trigger_ledger.py --check
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~BasesRedTeamRegressionTests"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include apps/slate-windows/tests/SlateWindows.Tests/BasesRedTeamRegressionTests.cs
python3 scripts/check_windows_census_trait.py
```

`make ci`'s Rust gates run in PowerShell as above (no make here); the workspace run mirrors `make test-fast`'s nextest filter (the Makefile's expressions are copied, with the two-sessions perf guard run alone in release as `make test` does). Then the whole Windows unit run under the desktop lock, if Call 3 is taken. The PR's lanes: rust, windows in full, swift, a11y-check.

## Acceptance

- [ ] Step 0's table is in a comment on #1704 and in the PR body.
- [ ] Facts 1 to 6b and the twin were seen red on `main` and on the inert surface, each for the reason given; 7 and 8 are named with their mutations; every mutation listed bites.
- [ ] A create calls the heal exactly once (`FIRST_INDEX_HEALS`, fact 9).
- [ ] A heal failure on each route fails that route with its transaction rolled back (Design 3; fact 6 and 6b seen).
- [ ] No scan heals per file; no path that already had a row heals.
- [ ] `Closes #1704`, and `gh pr view N --json closingIssuesReferences` shows only #1704; "close", "fix" and "resolve" stay away from #1426, #1954, #1955, #1683, #1295 and #1429.
- [ ] The body has the red team, the Codex rounds and verdict, "Found and fixed here" (none predicted), the `not delivered` lines (a Mac run), and the count.

## Red team record

A fresh Opus agent at xhigh read the first draft against the tree at 16ad788d, step 0's record and its probe outputs: 2 blockers, 2 mediums, 8 lows, and no design blocker. It found every first-row path covered, `index_file`'s return points complete, each placement and error mapping right, the change feed untouched, facts 1 to 6 red for the stated reasons, fact 8 biting whatever the walk order, and the counters read on the healing thread. Every finding is taken:

- Blocker 1: fact 7 was vacuous. A qualified `[[missing/Draft]]` sits in the path index only and no heal of `Draft.md` reads it, so none of fact 7's mutations could bite. Fact 7 now manufactures NULL rows that name the indexed paths and asserts a call counter, `FIRST_INDEX_HEALS` (Design 1), with a positive control.
- Blocker 2: fact 9's mutation (a double heal on a create) could not bite, because the second heal reads no rows. Fact 9 now asserts the call counter; `heal_after_create` is deleted, so a leftover call does not compile.
- Medium 1: the graph checks were vacuous unless the graph is built before the route. Facts 1 to 5 now build it first and check `graph_is_built()` and the rebuild; two staging mutations were added.
- Medium 2: the heal-failure outcome was stated only for the save and the create. Design 3 now states it for the repair, the open base and the open canvas, and fact 6b pins the canvas.
- Lows: the predicted count follows Call 5 (L1); the workspace test run mirrors `make test-fast` (L2); the Mac paths carry their folders (L3); the five docs are listed (L4); the twin writes its own linking note and leaves the shared vault alone (L5); the sweep's call line is `:8809` (L6); Design 4 names the sweep's and a recovery's first-come resolution as accepted (L7); the Goal says "does not hold" (L8).
- Calls 4 and 5: both real by the red team's reading. It added the Mac's template refresh to Call 4, and agreed that neither is this chunk's feature.

## Brief

Work in a worktree of its own on `claude/cb31-heal-on-first-index`, cut from `origin/main` at 16ad788d, with a `CARGO_TARGET_DIR` of its own. Post step 0's table on #1704. Commit the inert surface (the counter and the renamed seam), then the facts (red on it, the twin included), then the helper, `index_file`'s return value and the four calls; make them green; run every mutation; then the gates, the Windows class, and the whole unit run under the lock. Commit: `fix(core): a file first indexed by a save, a repair or an open heals the dangling links that name it`.

## What delivery changed (2026-11-19)

The branch is `claude/cb31-heal-on-first-index`, PR #2024. Where this spec and the merge disagree, the merge stands, and the lines above are left as written.

**The commits and the red runs.**
- 077a36d3 adds the inert surface (the counter and the renamed seam). 5aa52068 adds the facts, each red on it for this spec's reasons. 3b2514d4 is the behaviour.
- Ruling 4's Windows fact (`FinalMergeDeltaTests.ARepairThatFirstIndexesAMissingTabsFileReseatsIt`) is its own commit, d324964b. It was red against commit 2's core.
- 71d4f8ab updates one more existing fact that pinned `Modified` for a first-row repair: `canonical_identity::a_row_a_stopped_move_left_under_an_alias_goes_and_the_stored_spelling_gets_its_row`.
- Then come the diff red team's fixes (b36e16ab, 5af6b8f5, 665cec61) and the merge of `main` at 15cba369 (66ca3abf).

**Fact names.** Fact 10 is `repair_announcement.rs`'s Fact 13, because that file already had a Fact 12. The faulted arms use paths named for #1704, so no other fact's fault seam matches them.

**Design 3's "the repair's announcement is unchanged" was superseded by ruling 4.** A new `ReindexOutcome::FirstIndexed` announces `Created`. Doc 21's two recorded rules (`21_write_intent_protocol_invariants.md`, round 24's rule and the bind step's) gained a first-row arm in the same PR, as #1996 and #1982 had amended them. That was the diff red team's major.

**Mutation M6 is equivalent.** Facts 1 to 5's mutation "call the helper before the `FileAdded` stage" cannot bite. `row_target_key` (`graph.rs:733-741`) maps the healed row to its ghost, and `apply_file_added` then moves it onto the new node, so both orders end equal to a rebuild. The comment in `open_canvas` says so.

**The diff red team's minor 1 added three facts:** a repair, the sweep and an open base whose heal fails commit nothing. Each was red on `main`'s behaviour, each paired with a swallow mutation that bites.

**Ruling 5's probe was red on the branch.** An open reading view keeps drawing `[[Draft]]` unresolved after the create heals it, and activation announces `LinkUnresolved`. It was filed as #2023 (bug, windows) and is not in the PR.

**Found outside the diff, and commented on #1981 (comment 7025682510).** A stopped case-only move's recovery deletes the alias row and leaves the link rows that point at it.

**#1954 has two lines, both by reading** (comments 7027208954 and 7027505455):
- the Mac's template list refreshes on the first-indexing repair's `Created`;
- `handleSidebarFileChange` ignores `.created`, so that row's metadata is not refreshed.

**The merge judge's edits to the PR body** (no code):
- the two Windows effects a first-indexing repair's `Created` no longer triggers (the clean tab's staleness check and the History refresh), with why the impact is low;
- `open_base` named beside `open_canvas`: both heal without a file event, as on `main`;
- the §6 note on `index_file`'s return type lands in the wave C8 docs PR.

**Verification.**
- The whole Windows unit run at 71d4f8ab ran in two parts, because `ConnectionsLeafTests.TheModelOf*` hung the blame collector in a single run. Everything else passed 7,838 with 2 skipped; the three model facts alone passed 3 of 3.
- At the merged head, the nextest sweep passed 3,313 with 18 skipped, and every gate passed.
- On the PR, every check passed or skipped. The Windows app job is run 44651581502.

**Reviews.**
- Spec red team: 2 blockers, 2 medium, 8 low.
- Diff red team: 0 blockers, 1 major, 5 minors.
- Codex: round 1, "approve".
- The lead's independent merge judge: Merge.

**The count:** found 3, fixed here 1, deferred 1, filed 1.
