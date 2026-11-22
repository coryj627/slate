# CB-33: No cached or hot statement with an equality on `files.path` re-prepares per binding

Issue: #1917 (closes). Size M. Lanes: rust (the change and its facts), windows (a change under `crates/` runs the lane in full; no twin fact, Call 6), swift (compile only; no Swift edit). AT: none (the plan row owes none; no sentence changes). Wave: C10, slot 2. Parallel with: CB-17b and CB-22, neither of which edits a file under `crates/`. Decision: none in the plan; the lead ruled on every call on 2026-11-21 (the next section) and records Call 1 as a plan call. Interlock: `session.rs` goes CB-32, then CB-33, then CB-45b (plan §6); `db.rs` goes CB-32, then CB-33, then CB-45b; under the design below CB-33 no longer edits `links_db.rs`, so that row of plan §6 loses it. Review: a red team on this spec (done, recorded at the end), then a red team on the diff and Codex adversarial-review rounds until it approves (plan §4 rule 9).

The plan row, verbatim: "No cached or hot statement with an equality on `files.path` re-prepares per binding".

Written 2026-11-21 from `reading/07_core.md:165-182`, its cluster table (`:294-316`) and risk line (`:371`), the plan's §2.2 line, the issue, commit dbf8480a (#1918) and a step 0 run. Every `path:line` below was re-read at 7a34cb6e (`main` on 2026-11-21, after #2032). Step 0 ran at 7a34cb6e in a probe worktree (a detached checkout, never committed) with a Cargo target directory of its own, as temporary session test modules and four trial patches to product code, all removed afterwards; the worktree was left clean. It ran in two passes: the first before the red team, the second after it, for the statement cache's capacity and for `oplog.untouched_for`. The red team ran its own module in the same worktree; where a number below is the red team's it says so. The probe sources, the scan scripts, the trial patches and one log per run are kept with the chunk's working notes. Statements still by reading say so, and a section near the end lists what was run, what was read and what is not yet built.

`S` is `crates/slate-core/src/session.rs`; `L` is `crates/slate-core/src/links_db.rs`; `E` is `crates/slate-core/src/bases/engine.rs`; `T` is `crates/slate-core/src/tags_db.rs`; `D` is `crates/slate-core/src/db.rs`.

## The lead's rulings (2026-11-21), binding on the build

The six calls are kept below as they were put. These are the answers, and the build follows them. The delivery worktree is cut after CB-32 merges, on the principal's word.

1. **Yes: the stability guarantee on every connection core opens, in one configure function in `db.rs`.** The lead records it as a plan call and tells the owner; the build does not wait on them. The cost it brings is closed as a class, by three things the build owes:
   - a census that refuses a product statement that binds a `GLOB` pattern, and refuses the two conditions under which any `LIKE` could use an index prefix (facts 16 to 18; design section 5 says what "a case-sensitive LIKE prefix" means in this codebase and why the guard sits where it does);
   - a fact that reads `SQLITE_STMTSTATUS_REPREPARE` across the whole class, not only the path statements, and shows none per binding where main re-prepares, seen red on main (fact 15);
   - the setting confirmed on every connection core opens, with a fact that walks them (fact 11; design section 6 lists every connection kind that exists today).
   The PR still closes #1917, and its body corrects the issue's stated cause with the step 0 numbers.
2. **Yes: capacity 32, with its fact (14).** The PR body records the memory cost.
3. **Yes: the index is named in the five temporal probes and both wrong-index defects are fixed here, red first.** The 20.2 s view over 10,001 rows goes under "Found and fixed here" with before and after numbers.
4. **Yes: the upstream report is drafted in the PR body under "For the owner".** No agent posts it.
5. **Yes: the helpers move.**
6. **Agreed: no Windows twin.** The reason is named under "not delivered".

Both deferred finds go on #1955 as proposed; the two lines are written ready to post in the #1955 section.

Three places where a ruling could not be built exactly as worded, with what this spec builds in its place (each is flagged again where it is designed). The lead accepted all three wordings on 2026-11-21, each as a wording change from its ruling:

- **"Refuses a product statement that binds a case-sensitive LIKE prefix."** No reading of a statement can tell whether its `LIKE` is case-sensitive or can use an index: that is a property of the connection (`PRAGMA case_sensitive_like`) and of the schema (the collation of the indexed column), not of the SQL text. Built: a fact that both of those conditions are absent, judged by SQLite, which closes the shape for every `LIKE` however its SQL is assembled. `GLOB` is refused by its text.
- **"A fact that walks every connection, test seams that ship included."** The four env-gated second connections in product code are locals that live for one or two statements inside a product function; no fact can hold one. Built: the fact walks every connection it can hold and opens one more the way the seams do (the same function on the same path), and the census holds that nothing opens a connection any other way.
- **"Every one of the 38 literals main re-prepares is reached."** Thirty-two are reached. Six are never in the cache on main or after (their sites prepare a statement per call), so main does not re-prepare them and no fact can see them red; they are named with that reason. Four of the 32 are red on main by being absent from the cache, not by a re-prepare count, because main runs them through `query_row`.

## Re-read at 46f50603 (2026-11-21, after CB-32 merged as #2033)

Every `path:line` the Design, Facts and Build order rest on was read again in the delivery worktree. The body of this spec keeps its 7a34cb6e numbers; this table is the map. CB-32 did not edit `bases/engine.rs`, `tags_db.rs`, `tasks_db.rs`, `properties_db.rs`, `dql_inline_fields_db.rs`, `search_db.rs`, `citations_db.rs`, `session/directory_page.rs`, `session/tests/scan.rs`, `oplog_temporal.rs` or `two_sessions.rs`, so every line cited in those holds.

| What | In this spec | At 46f50603 |
|---|---|---|
| `D` `open_database` | `:252-272` | `:256-276` |
| `D` `open_connection`, and its two `Connection::open…` calls | `:282-296`; `:293`, `:295` | `:286-300`; `:297`, `:299` |
| `D` `open_in_memory`, and its `Connection::open_in_memory` | `:323-328`; `:324` | `:327-332`; `:328` |
| `D` `migrate` | not cited by line | `:594` |
| `S` up to `:13112` (`open_worker_connection` `:1628-1636`, `open_worker_connection_for_tests` `:2274`, the session's open `:2722` and `migrate` `:2732`, the seams `:4980` and `:9038`, `S:3036-3037`, `:7098-7101`, `:7411`, `:7914`, `:9545`, `:10269`, `:12008-12011`, `:12037-12041`, `:12234`, the comment `:12543-12545`, `:12547`, `get_file_summary_impl` `:13098-13112`) | as written | unchanged |
| `S` `collect_rewrite_candidates`, its statement | `:19089`; `:19131-19135` | `:19090`; `:19134-19138` |
| `S` `ensure_open_base_indexed`, its statement | `:19940`; `:19944-19947` | `:19946`; `:19950-19953` |
| `S` `DbTitleSource`, `title_for`, its SQL | `:22272`; `:22277-22296`; `:22280-22283` | `:22278`; `:22283-22302`; `:22286-22289` |
| `S` `canvas_outline` and `canvas_table_rows` statements | `:22612`; `:22665` | `:22618`; `:22671` |
| `S` the tests list: `mod save_link_lookup;`, CB-32's `mod final_sigma;`, `mod two_sessions;` | after `:23483` | `:23489`, `:23491`, `:23571` |
| `L` `graph_linkset_for`, `graph_inbound_rows` | `:436-440`; `:464-468` | `:439-443`; `:467-471` |
| `L` `outgoing_links_for`, `outgoing_links_bounded`, `outgoing_links_count` | `:719-725`; `:801-808`, `:810-817`; `:835-838`, `:840-843` | `:722-728`; `:806-813`, `:815-822`; `:840-843`, `:845-848` |
| `L` `backlinks_for`, its count; `unresolved_links` | `:868-878`, `:900`; `:938-948` | `:873-884` (two more columns), `:909`; `:948-959` (two more columns) |

What CB-32 added that this chunk's class reaches, measured on 46f50603 with step 0's probes (run once in the delivery worktree and removed before any commit):

- **Statements main re-prepares:** the literal sweep reads the same totals (276 distinct, 197 steady, 38 re-prepared, 24 templates, 17 that do not compile alone). The 38 are the same sites; two of them (`backlinks_for`, `unresolved_links`) carry CB-32's two extra columns in their text. No new member.
- **Connections:** none. Migration 042 is SQL in the migration list and runs on the session's connection; its index rebuild opens nothing. Product code still opens a connection at `D:297`, `D:299` and (test builds) `D:328` only, and no new test seam opens one.
- **The scan's cached statements:** still seventeen for a changed rich note with an unchanged neighbour, still fourteen rebuilt per boundary at a capacity of 16 (16.6 compiles a changed file) and none at 32 (14.3).
- **`oplog_events`:** migration 042 rebuilds seven fold indexes on `files`, `dirs` and `links` and touches neither `oplog_events` index; the five temporal probes are unchanged.

Nothing moves a ruling's premise.

## What delivery changed (2026-11-21, after the build and the red team on the diff)

Delivered as PR #2036, merged on 2026-11-21 as 02fbd37f (branch `claude/plan-47-cb33`; head a04e042d when opened, a1744a20 when merged). #1917 is closed. Count: found 12, fixed here 3, deferred 5 (five #1955 lines), filed 0. Reviews: the spec red team and the diff red team (both Opus, xhigh), Codex rounds 1 to 4 (approve, needs-attention twice on a guard added after round 1 and then removed, approve on the final tree), and the lead's merge judge (merge; one minor, three nits).

The build report, kept with the chunk's working notes outside the repository, has the runs. What differs from the text below:

- **Only macOS takes the anchored branch of the open, and no CI lane runs core's tests on macOS.** `anchored::VFS` is `Some("unix-posix")` under `target_os = "macos"` and `None` elsewhere (`db/anchored.rs:55-58`, `db/anchored_windows.rs:65`). The rust lane is Linux and takes the plain branch, as Windows does; the Mac lane builds `slate-uniffi` and runs the Swift tests. So fact 11 proves the plain branch wherever it runs and the anchored branch nowhere, and the mutation "configured in the plain branch only" passed every fact on Windows. The design gained two things that hold on every lane: `open_unconfigured` returns a type, `Unconfigured`, whose connection is private to a module of `db.rs` and which only `configure_connection` turns into a `Connection` (an open that goes round the configuration does not compile; the Mac lane compiles the anchored branch when it builds `slate-uniffi`), and a census, `the_open_has_one_exit_through_configure`, that reads `db.rs` as text.
- **`configure_connection` takes `Unconfigured` and returns `Connection`**, not `&Connection`. `open_connection` is `configure_connection(open_unconfigured(path)?)`.
- **Fifteen statements were rebuilt in the mixed rescan, not fourteen.** Step 0's counter missed the one that fires triggers.
- **The probes' SQL did not become product `const`s.** The facts capture the SQL the product ran.
- **`PrepareCounter` is `counting_statements`.**
- **Fact 3 has a scripted half**, `each_row_eval_probe_answers_for_its_own_note`: the generated census cannot hold the two deleted-content probes to their own note at its default scale.
- **The pattern census reads `GLOB` in either case and as the function `glob(pattern, text)`**, which SQLite plans as it plans the operator. The connection census also sees an open passed as a value and product code after a one-line test module.
- **Two mutations of the table do not exist as written.** "The cache shrunk to two statements inside fact 1" cannot bite (a view assembles its rows before it runs the filter, so the probe runs back to back); two others stand in for it. "`configure_connection` called inside one branch" no longer compiles.
- **The release numbers.** The `has_change_since` view over 10,001 files on a log of 20,000 events reads 8.2 to 9.7 s on main and 0.63 to 0.64 s after; 20.2 s was a debug build.
- **`open_database`'s debug line** reads "did not open or could not be configured".
- **No local whole Windows unit run.** The desktop was held by another team; the PR's Windows lane is the proof.

After the PR opened (#2036, at a04e042d, Codex round 1 "approve"):

- **The merge judge's minor: the texts name all five uses of a bound value the guarantee gives up,** not one: the `LIKE` and `GLOB` prefix search, a partial or expression index matched through a bound value, STAT4 probes, a bound LIMIT, and the small-integer selectivity guess. The doc on `configure_connection`, `BENCHMARKS.md` and the PR body's plan call say the same five, each with its guard or that it has none.
- **A guard for the second use was built and taken out again.** The judge offered it "if cheap". It was a reader of SQL text over the partial indexes' conditions and the product statements. Codex round 2 found it missed a signed literal; the class fix (a condition reader that failed closed, with SQLite judging 32 planted conditions) still missed a list of one constant, a double-quoted string and a grouped operand in round 3. A reader of SQL text cannot stand in for SQLite's parser, so the lead's stated fallback was taken: the case is recorded as unguarded for product statements, with a #1955 line whose remedy is SQLite as the judge end to end. What stands in its place parses nothing: `the_schemas_partial_indexes_are_the_ones_pinned` lists what SQLite itself calls partial and pins each name and SQL text, and `the_guarantee_gives_up_a_partial_index_whose_condition_is_bound` shows the cost on planted conditions. Codex round 4 approved that tree (a1744a20). No product code changed after a04e042d.
- **Found by SQLite on the way, and now in the kept fact's doc:** a parameter stands in for a literal only as one side of an operator; the members of a list are never proved by a bound value; the parser writes a list of one constant as an equality. So the schema's only literal a statement could bind today is `is_external = 0` on the two link fold indexes, and every product statement writes that 0 as a literal.
- **Three more #1955 lines** (posted): the unguarded case above; the connection census's skip past product code after a test module closed as `} // mod tests`; two cache facts tied to 32 slots.
- **`main` was merged in twice,** f547d8f5 (#2035) and 3c0dbe55 (#2037), with no conflict and no crate file.

## Goal

On every connection core opens, a statement taken from the statement cache is never compiled again because its bindings were cleared. The hot statements #1917 names are in that cache and run from it: per row of a bases view, per card of a canvas, per healed source. The statements a scan runs per file fit in the cache, so none is dropped and built again as the scan moves between changed and unchanged files. Two temporal probes stop walking every event in the window for every row. A fact fails if a hot statement is compiled per call again, and a census fails if product code opens a connection any other way.

Not in the goal, said here so the plan row is not read wider than the chunk: a statement whose site prepares it fresh on every call stays that way where it is a plain one-row lookup (about 1.6 µs of 14.7 a call), and the scan's slow path keeps the fourteen statements it has never cached (14.3 compiles a changed file at any capacity; a #1955 line carries them).

## Current state (the drift check, against 7a34cb6e)

The reading was written at 1ec9bf31. `S` has grown by about 600 lines since, and CB-31 moved one caller into `L`.

| Claim (issue, plan or reading) | At 7a34cb6e |
|---|---|
| Issue: `tags_db.rs` `load_dql_tags_for_path` is a cached join on `f.path = ?1`. Reading: `tags_db.rs:117-130`, statement `:121-127` | `T:118-131`; `prepare_cached` at `:122`, SQL `:123-127`. Holds |
| Issue: `bases/engine.rs` `SqlVaultLookup` holds four cached joins of that shape. Plan §2.2 and the reading: three of the four are not cached | Holds as the plan says. `oplog_has_change_since` `E:3807` (SQL `:3809-3810`), `oplog_has_property_change` `E:3816` (`:3823-3826`) and `oplog_deleted_content_matches` `E:3832` (`:3839-3841`) go through `exists_query` (`E:3694-3709`), which is `Connection::query_row` (`:3702`): a fresh prepare on every call. Only `oplog_deleted_content_matches_regex` (`E:3851`, statement `:3874-3880`) uses `prepare_cached`. The reading's lines were `:3804`, `:3813`, `:3831`, `:3693-3708` and `:3874-3880` |
| Issue: `session.rs` `DbTitleSource::title_for` is an uncached `query_row`. Reading: `session.rs:21674-21683`, `:21677` | `S:22272-22296`; SQL `:22280-22283`. Holds. Its four callers are `S:5791` (a `.canvas` saved as text), `S:22422` (the scan's canvas pass), `S:22530` (`open_canvas`) and `S:23075` (`canvas_apply`); the title is asked at `canvas/model.rs:763` |
| Issue: "The plain single-table lookup, `SELECT … FROM files WHERE path = ?1`, is not re-prepared under either version" | Holds on 3.54.1, measured: 0 of 10,000. The safe class is narrower than "single-table", though: see step 0 |
| Reading `:169`: #1918 rewrote the scan's probe (`session.rs:11475-11480`), cached the fast path's `UPDATE` (`:11504-11516`), and added the fact `scan.rs:1551` with the helper at `:1525` | Probe `S:12006-12013` (its comment `:12002-12005`); `UPDATE` `S:12036-12048`; the fact and the helper are still at `session/tests/scan.rs:1551` and `:1525-1548` |
| Reading's table: DQL tags are read at `bases/engine.rs:4007` and `bases/eval.rs:1766`; the temporal probes at `eval.rs:1858`, `:1879`, `:1900` | `E:4015-4017` (`dql_tags_for`); the `eval.rs` lines hold, and the regex probe is called at `eval.rs:1926` |
| Reading's table: `links_db.rs:427` `graph_linkset_for`, called from `session.rs:6577`, `:11145`, `:14113`, `:15074`, `:17641` | `L:431-453`, SQL `:436-440`. Callers: `L:581` (CB-31 moved the create's heal into `heal_rows_naming_first_indexed`, `L:568`), `S:11667` (the scan's tail), `S:14690`, `S:15651`, `S:18218` (the moves). Each runs only while the graph is live |
| Reading's table: outgoing links `links_db.rs:669`, `:751`, `:760`; counts `:785`, `:790`; read at `session.rs:6916`, `:7611`, `:7635-7638` | `L:719-725`, `:801-808`, `:810-817`; counts `L:835-838`, `:840-843`; read at `S:7164` (`outgoing_links`), `S:7859` (`note_load_bundle`), `S:7883-7886` (`note_link_panels`) |
| Reading's table: the open-base probe `session.rs:19355`; the linked-source clause `bases/engine.rs:983` | `S:19944-19947` (`ensure_open_base_indexed`, `S:19940`); `E:983-988`, unchanged |
| Reading, "same shape, not measured": `links_db.rs:455` `graph_inbound_rows` (from `session.rs:11455`), `links_db.rs:818` `backlinks_for`, `bases/engine.rs:2086` `load_backlinks`, `session.rs:12555` `get_file_summary_impl`, `bases/engine.rs:3926` `oplog_untouched_for` | `L:459-479` (SQL `:464-468`; nine callers, the scan's at `S:11987`); `L:855-923` (SQL `:868-878`); `E:2084-2091`; `S:13098-13112`; `E:3925-3936`. All five are measured below |
| Reading: the census goes in `crates/slate-core/tests/`, "beside the other source censuses" | That folder holds no source census. Core's source censuses are unit-test modules under `src/`: `vault/test_trash_census.rs` and `session/tests/fault_guard.rs:101-154`. A census that needs a migrated connection belongs with the session tests |
| Reading: move the helper to `session/tests/common.rs` | `common.rs` exists and every session test module imports it; the helper is private to `scan.rs` today |
| Brief: every connection's statement cache has rusqlite's default capacity | Holds. Nothing under `crates/` calls `set_prepared_statement_cache_capacity`; rusqlite 0.41.1's default is 16 (`lib.rs:171`). Measured on the session's connection: 40 statements offered, 16 kept |
| Plan §6: `session.rs` and `links_db.rs` go to CB-32 before CB-33 | CB-32 is not merged (checked 2026-11-21 after the red team: `origin/main` is still 7a34cb6e). Its branch was at 8da0171e when this spec was first written and is at ed7e4ce2 now, cut from 47bcf77e. What it moves is under "Overlap" |

Versions, from `Cargo.lock:1303-1304` and `:1925-1926` and from the probe: rusqlite 0.41.1, libsqlite3-sys 0.39.1, bundled SQLite 3.54.1 (`sqlite_source_id()` 2026-10-16 14:45:20 555e07ef…). The workspace turns on rusqlite's `bundled`, `functions` and `hooks` features (`Cargo.toml:27`), not `trace`, so compiles were counted through the authorizer.

## Step 0, as run on main (7a34cb6e), 2026-11-21

### How it was measured

The vault: 10,000 notes (the issue's size) and one canvas of 1,000 file cards, scanned through `scan_initial`. Each note has a title, a status, two tags, a link, an embed and an inline field; one in ten has a dangling link. Thirty-one notes were saved twice so the log holds events. The index held 10,001 files, 21,000 links, 30,000 properties and 62 `oplog_events` rows. Everything ran on the session's own connection (`session.conn`).

Three counters, none of them a timing:

- `SQLITE_STMTSTATUS_REPREPARE` and `SQLITE_STMTSTATUS_RUN` of every statement still prepared on the connection, read by the walk the #1918 helper uses (`sqlite3_next_stmt`). A statement found by the walk after its caller returned is in the statement cache. RUN says how often that same statement object ran.
- Compiles, counted by an authorizer callback. SQLite calls the authorizer while it compiles a statement (a prepare or a re-prepare) and never while it runs one. This is what counts a `query_row`, which leaves no statement to walk.
- `SQLITE_STMTSTATUS_VM_STEP` where the work of running mattered (the index a probe walks).

Each statement was run three ways, 10,000 calls with 10,000 different paths (1,000 for a write, inside a savepoint that was rolled back): taken from the cache each call (`prepare_cached`), prepared fresh each call (what `query_row`, `execute` and a one-shot `prepare` do), and prepared once and held. Microseconds are from a debug build on a desktop that also runs CI VMs. They are indicative; the counts are the evidence.

### The cause, as measured and as read

The issue says a cached join on `files.path = ?` is re-prepared "on each new binding". Measured, it is re-prepared on every use from the cache, new value or not, and the path has little to do with it:

- By the red team's reading of both sources, the cause is a change in 3.54. In 3.53.2 `whereLoopOutputAdjust` called `sqlite3ExprIsInteger` without the parse context (libsqlite3-sys 0.38.2 `sqlite3.c:171773`): no look at the value, no mark. 3.54.0 passes the context with a new flag and a new mask (`smimask`) whose stated purpose is that such a statement is re-prepared only when a rebind moves the value to or from 0 or 1 (`sqlite3.c:115823-115826`). Binding honours the mask (`:94997-95004`, `:95127-95144`); `sqlite3_clear_bindings` does not (`:93420-93421`). That is the whole difference between a held statement and one that went back to rusqlite's cache, and it reads as an inconsistency SQLite may one day remove (Call 4). The look at a bound LIMIT is older: 3.53.2 has it (0.38.2 `:151391`).

- A cached join bound to the same path on every call is re-prepared 9,999 times in 10,000 (variant M6).
- The same statement prepared once and held, and rebound with a new path each call, is not re-prepared (0 of 10,000, every join in the table below). The red team's run adds two cases where a held statement is: a `LIKE ?1` whose pattern changes (4 of 4 binds), and an integer equality whose value crosses between 0 or 1 and anything else (4 of 4). The "Held" column below is for text paths and a constant LIMIT.
- By reading: rusqlite's cache clears a statement's bindings when the statement goes back in (`rusqlite-0.41.1/src/cache.rs:159`, `stmt.clear_bindings()`), and `sqlite3_clear_bindings` expires any statement whose plan looked at a bound value (`sqlite3.c:93419-93421`, `if( p->expmask ){ p->expired = 1; }`). The next use re-prepares it.
- By reading: SQLite 3.54.1 marks a parameter that way in `sqlite3ExprIsInteger` (`sqlite3.c:115828-115867`, whose comment says the statement is "reprepared each time a new value is bound"). The planner calls it for an `= ?` or `IS ?` term that some candidate loop does not use as an index key, but only for a statement with more than one FROM source or with an ORDER BY (`whereLoopOutputAdjust`, `sqlite3.c:172751-172753` and `:172802`), and for a bound LIMIT (`:152150`). A parameter under a unary plus is not looked at (`:115836-115840`).

Measured, the class is this (10,000 calls each, re-prepares when taken from the cache):

| Shape | Example | Re-prepares |
|---|---|---:|
| One-row lookup on a unique key, nothing else in the FROM | `SELECT id FROM files WHERE path = ?1`, also with `ORDER BY id` (M1) | 0 |
| Single table, bare-parameter equality, no ORDER BY | `… FROM properties WHERE file_id = ?1` (M14) | 0 |
| Single table, bare-parameter equality, ORDER BY | `… FROM properties WHERE file_id = ?1 ORDER BY ordinal` (M13, the bases row's own statement) | 10,000 |
| Join, bare-parameter equality on any column, text or integer | on `files.path` (02 to 07, 45, 49 to 52), `files.name` (M2), `links.target_path` (S1, S2, M3), `files.id` (M12); a comma join (M7); the term in the ON clause (M8) | 9,999 |
| The same on a schema of two bare tables | `t JOIN files f … WHERE f.path = ?1` | 199 of 200 |
| A bound LIMIT, any shape | `… WHERE path > ?1 ORDER BY path LIMIT ?2` (M9), `… WHERE path = ?1 LIMIT ?2` (M10) | 10,000 |
| A nested id lookup | `… WHERE file_id = (SELECT id FROM files WHERE path = ?1)` (30, and every rewrite below) | 0 |
| The parameter under a unary plus, or a CAST | `files.path = +?1` (M4), `CAST(?1 AS TEXT)` (M5), `LIMIT +?2` (M11), `file_id = +?1 ORDER BY …` (M15) | 0 |
| An EXISTS in the WHERE clause of a single-table statement | `collect_rewrite_candidates` (41): the planner turns the EXISTS into a join | 9,999 |
| A correlated subquery in the projection, or a NOT EXISTS | 21, 44, 54 | 0 |
| Red team's run, one cached reuse each: a single table with `GROUP BY`, with `DISTINCT`, with `COUNT(DISTINCT …)` (`S:9545`), or with a window function's `ORDER BY`; a single-table `UPDATE` or `DELETE` whose `IN` subquery holds a join; a correlated scalar subquery with a join inside; the parameter on the left (`?1 = f.path`); `IS ?`; a compared rowid in a join | | re-prepared |
| Red team's run: an `IN` list of one or two, `BETWEEN`, the parameter inside an expression (`?1 \|\| ''`) or under `COLLATE` | | not re-prepared |

The rule under the table, by the red team's reading: the planner's check is skipped only for one FROM source with no ORDER BY, and GROUP BY and DISTINCT set that ORDER BY too. No short rule a person could apply to new SQL covers every row; SQLite is the only reliable judge of a given statement.

Two more facts shape the design. A re-prepare costs more than a fresh prepare: `oplog_has_change_since` costs 27.3 µs as written (a fresh prepare each call) and 34.9 µs when it is only moved into the cache, because every call re-prepares it. And a statement with a bound LIMIT compiles twice per call even when it is not cached (2,000 compiles per 1,000 calls): the prepare, then a re-prepare on the first bind.

### The inventory

Built from a scan of every string literal under `crates/` (`sqlscan.py` in the working notes), then read by hand for SQL assembled from pieces. `slate-cli` and `slate-uniffi` hold no SQL: 0 calls to `prepare`, `query_row` or `execute` in either. In product code, 56 distinct statements name `files` and compare its `path` with a bound parameter, at 101 call sites: the 53 the first pass tabled below, and three the red team's own search added, listed after the groups. (The scan also met `search_db.rs:173`, a `LIKE`, and `session/directory_page.rs:621`, which compares `path` with an expression; neither is an equality on a bound path.) Every one runs on the session's `conn`. By reading, the compactor's `worker_conn` (`S:1305`, opened at `S:1628-1636`) runs none of them: its job carries a file id.

Numbers are for 10,000 calls unless a row says otherwise. "Cached" is taken from the cache each call; "fresh" is a new prepare each call.

Joins, CTEs and the like (19 statements, 19 sites):

| # | Statement | Where, and how the site prepares it | Per user action | Cached: re-prepares | Held | µs cached / fresh |
|---|---|---|---|---:|---:|---:|
| 02 | `links JOIN files … WHERE files.path = ?1 ORDER BY links.ordinal` | `L:436-440` `graph_linkset_for`, `prepare_cached` | per source re-resolved or healed, while the graph is live: hot | 9,999 | 0 | 36.7 / 23.6 |
| 03 | the same join, eleven columns | `L:719-725` `outgoing_links_for`, `prepare_cached` | once per note bundle | 9,999 | 0 | 49.4 / 27.8 |
| 04, 05 | the same with `LIMIT ?2`, embeds only and all | `L:801-808`, `:810-817` `outgoing_links_bounded`, `prepare_cached` | once each per link panel load | 10,000 | 1 | 47.0 / 57.3 and 49.7 / 54.2 |
| 06, 07 | `SELECT COUNT(*) FROM links JOIN files … WHERE files.path = ?1`, embeds only and all | `L:835-838`, `:840-843` `outgoing_links_count`, `prepare_cached` | once each per link panel load | 9,999 | 0 | 30.7 / 21.4 and 27.4 / 19.9 |
| 08 | `links JOIN files … WHERE links.target_path = ?1 AND (?2 IS NULL OR files.path > ?2 OR (files.path = ?2 AND links.ordinal > ?3)) ORDER BY … LIMIT ?4` | `L:868-878` `backlinks_for`, `prepare_cached` | once per page | 10,000 | 1 | 52.7 / 53.0 (first page), 48.8 / 51.1 (a later page) |
| 09 | `links JOIN files … WHERE links.target_path IS NULL … (files.path = ?1 AND links.ordinal > ?2) … LIMIT ?3` | `L:938-948` `unresolved_links`, `prepare_cached` | once per page | 10,000 | 1 | 742.9 / 748.5 |
| 19 | `file_tags t JOIN files f … WHERE f.path IN (?, …)` | `S:7098-7101` `tags_for_files`, `prepare`, once per 500 paths | once per 500 paths | 0 of 10,000 (a list of 1), 0 of 20 (a list of 500) | | |
| 35 | `WITH candidates AS (… FROM files WHERE path = ?1), …`, twelve SELECTs | `S:13100-13109` `get_file_summary_impl`, `prepare_cached` | once per path a host asks; the Mac asks per journal path in a loop (`AppState+Rescan.swift:1188`, by reading) | 9,999 | 0 | 294.7 / 268.5 |
| 41 | `files f WHERE f.path = ?1 AND f.is_markdown = 1 AND EXISTS (… links …)` | `S:19131-19135` `collect_rewrite_candidates`, `prepare`, held across its loop | per moved file | 9,999 if it were cached | 0, which is how it runs | 33.0 / 23.2 |
| 42 | `files f JOIN file_meta fm … WHERE f.path = ?1` | `S:19944-19947` `ensure_open_base_indexed`, `query_row` | once per base opened | 9,999 if it were cached | 0 | 25.3 / 17.5 |
| 43 | `properties p JOIN files f … WHERE f.path = ?1 AND p.key = 'title' … LIMIT 1` | `S:22280-22283` `title_for`, `query_row` | per file card, on every scan and every canvas open: hot | 9,999 if it were cached | 0 | 34.2 / 22.8 |
| 45 | `dql_file_tags t JOIN files f … WHERE f.path = ?1 ORDER BY t.ordinal` | `T:123-127` `load_dql_tags_for_path`, `prepare_cached` | per row of a DQL query that reads tags: hot | 9,999 | 0 | 30.1 / 21.0 |
| 47 | `path IN (SELECT l.target_path FROM links l JOIN files src … WHERE src.path = ? …)` | `E:983-988`, a clause of the compiled bases query, which is prepared once per query (`E:846`, `:929`) | once per query | 9,999 if the whole query were cached | 0 | 43.8 / 29.3 |
| 49 | `oplog_events e JOIN files f … WHERE f.path = ?1 AND e.event_class = 1 AND e.ts_ms >= ?2 LIMIT 1` | `E:3809-3810` `oplog_has_change_since`, `query_row` | per row of a view that uses it outside the pushed-down position: hot | 9,999 if it were cached | 0 | 34.9 / 27.3 |
| 50 | the same join for classes 2, 3 and 5 with `e.property_key = ?2` | `E:3823-3826` `oplog_has_property_change`, `query_row` | per row: hot | 9,999 if it were cached | 0 | 40.5 / 27.0 |
| 51 | the same join with `e.deleted_text LIKE '%' \|\| ?3 \|\| '%'` | `E:3839-3841` `oplog_deleted_content_matches`, `query_row` | per row: hot | 9,999 if it were cached | 0 | 37.0 / 25.1 |
| 52 | the same join selecting `e.deleted_text` | `E:3877-3879` `oplog_deleted_content_matches_regex`, `prepare_cached` | per row: hot | 9,999 | 0 | 33.7 / 22.7 |

A single table with a correlated subquery (3 statements, 4 sites), none re-prepared: 21 `SELECT f.mtime_ms = -1 AND NOT EXISTS(…) FROM files f WHERE f.path = ?1` (`S:8158-8160`, `S:9117-9119`, `query_row`; 13.2 / 19.2 µs); 44 `SELECT f.id, EXISTS(…) FROM files f WHERE f.path = ?1` (`S:22463-22466` `open_canvas`, `query_row`; 13.3 / 17.6); 54 `oplog_untouched_for` (`E:3927-3932`, `query_row`, per row; 13.6 / 21.8).

The path inside a scalar or EXISTS subquery (8 statements, 18 sites), none re-prepared: 13 the intent marker's `INSERT OR REPLACE … COALESCE((SELECT index_epoch FROM files WHERE path = ?1), 0)` (`S:4939`, `:5062`, `:8399`, `:8453`, `:8497`, `:19310`, `execute`); 14 (`S:5044`); 15 `SELECT EXISTS(SELECT 1 FROM files WHERE path = ?1)` (`S:5188`, `:8932`, `:9257`, `:17787`); 23 `DELETE FROM tasks WHERE file_id = (SELECT id …)` (`S:8185`, `:9145`, and the test seam at `tasks_db.rs:209`); 24 (`S:8478`); 26 (`S:8951`); 30, #1918's probe (`S:12008-12011`, `prepare_cached`; 13.5 / 17.7); 32 (`S:12063`, `prepare_cached`).

One-row lookups and writes on `files` by path (23 statements, 57 sites), none re-prepared. Reads, 0 of 10,000 each: 01 `SELECT id FROM files WHERE path = ?1` (15 sites: `citations_db.rs:82`, `dql_inline_fields_db.rs:165`, `S:5739`, `:7397`, `:7551`, `:7902`, `:12150`, `:12234`, `:18111`, `:18885`, `:22514`, `tasks_db.rs:99`, `E:3969`, `:3984`, `:3999`; all `query_row` except `S:12234`, which is cached; 13.1 / 14.7 µs), 11 (`S:3191`, `:6137`, `:6909`, `:17327`, `:19059`), 12 (`S:3449` cached, `:5168`), 16 (`S:5688`), 17 (`S:6309`, `:6401`, `tasks_db.rs:181`), 18 (`S:7017`), 20 (`S:8094`, `:8148`, `:9060`), 28 (`S:9690`), 29 (`S:11960`, cached), 34 (`S:12522`), 37 (`S:16940`, `:18056`), 40 (`S:18368`), 48 (`E:1714`), 53 `oplog_created_since` (`E:3918-3919`, per row). Writes, 0 of 1,000 each: 22 (`S:8181`, `:9141`, `:9201`), 25 `DELETE FROM files WHERE path = ?1` (`S:8947`, `:9271`, `:16676`, `:17380`, `:17533`), 27 (`S:9041`), 31 the fast path's `UPDATE` (`S:12037-12041`, cached), 33 (`S:12335`), 36 (`S:14319`, `:14664`, `:15625`), 38 (`S:18099`), 39 (`S:18129`, `:19251`, `:19326`). Not run: 46 (`tasks_db.rs:202`), a plain `UPDATE` in a test seam that runs on a second connection only under `SLATE_TEST_TORN_NOTE_READ`.

Three more, found by the red team by reading, none measured as bare SQL:

- `S:3036-3037` `rollback_structural_batch_inflight_locked`: `SELECT EXISTS(SELECT 1 FROM {table} WHERE path = ?1), EXISTS(SELECT 1 FROM {table} WHERE path = ?2)`, where `{table}` is `files` for a file entry. A `format!` template, so the first pass's scan dropped it. The safe shape (two single-table EXISTS), `query_row`, once per entry of a batch being rolled back.
- `E:1268` (`path = ?`) and `E:1520` (`(path = ? OR substr(path, 1, length(?)) = ?)`): two clauses of the bases candidate query, `SELECT … FROM files WHERE … ORDER BY path ASC LIMIT ? OFFSET ?` (`E:834-846`). One table with an ORDER BY, so the equality is marked; and LIMIT and OFFSET are bound as text (`E:841-844`), so the limit is marked and never used by the plan. Each execution compiles the query twice (the prepare, then a re-prepare on the first bind). Once per execution, not per row. The red team's flows ran it with the same VM steps with the setting on and off.

So, of the 53 measured: eleven are cached today and re-prepared on every use (02 to 09, 35, 45, 52). Five more have the shape and are not cached, so each call pays a fresh prepare (42, 43, 49, 50, 51); caching any of them without changing it makes it slower. One has the shape and is held, so it is not re-prepared (41). One is a clause of a query prepared once (47). The other 35 are not re-prepared in any mode.

### The class beyond `files.path`

The probe then took every product SQL literal the scan found (276 distinct texts) from the cache twice on a real session's connection and asked SQLite whether the second use re-prepared it. Nothing in that check reads the SQL. Of the 276: 197 are steady, 38 are re-prepared on every use, 24 are `format!` templates and 17 are fragments or non-SQL strings that do not compile alone.

Sixteen of the 38 are the `files.path` statements above. The other 22 name no path: the four statements every bases row runs (`E:1792` properties, `E:1993` tags, `E:2011` outgoing links, all `WHERE file_id = ?1 … ORDER BY`, and `E:2087` backlinks, a join on `links.target_path`), `properties_db.rs:171`, `S:12547` (headings, under a comment at `S:12543-12545` that says caching it "avoids the prepare-step overhead"), `dql_inline_fields_db.rs:184`, `L:464` `graph_inbound_rows`, `S:7411`, `S:7914`, `S:9545`, `S:10269`, `S:22612`, `S:22665`, `tasks_db.rs:122` and `:215`, three in `search_db.rs` (`:173`, `:300`, `:355`) and three in `citations_db.rs` (`:111`, `:327`, `:389`). `S:9545` (`COUNT(DISTINCT …)` with an OR) fits none of the first pass's shapes; the red team's run explains it (to the planner a `COUNT(DISTINCT …)` is an ORDER BY). It is the case for letting SQLite, not a rule, say which statements are in the class.

### Through the real callers

Each hot path was driven once through the public session API on the same vault. A unit is a row, a card, a note or a page. Each cell is microseconds per unit, then SELECT compiles per unit. The counter counts SELECTs compiled, so a statement with a subquery counts once for each SELECT in it: `untouched_for` holds two, `get_file_summary` twelve.

| Hot path | Units | main |
|---|---:|---:|
| A bases view, each temporal operator in turn outside the pushed-down position (`… \|\| file.name == "zzz"`): `has_change_since`, `has_property_change`, `deleted_content_matches`, `deleted_content_matches_regex`, `created_since` | 10,001 rows | 187 to 193 µs, 5.01 |
| The same for `untouched_for` | 10,001 | 187 µs, 6.01 |
| A bases view, `has_change_since` pushed down (control) | 10,001 | 1 µs, 0.01 |
| DQL `TABLE file.tags` | 10,001 | 220 µs, 5.01 |
| DQL `TABLE file.outlinks` | 10,001 | 246 µs, 6.01 |
| DQL `TABLE file.inlinks` | 10,001 | 217 µs, 5.01 |
| DQL `TABLE file.name` (control: no path statement at all) | 10,001 | 177 µs, 4.01 |
| `open_canvas`, a board of file cards | 1,000 cards | 46 µs, 1.00 |
| A rescan with nothing changed: the canvas pass asks every card's title again | 1,000 cards | 1.02 per card |
| `outgoing_links` per note | 2,000 | 54 µs, 1.00 |
| `backlinks` per note, first page | 2,000 | 69 µs, 2.00 |
| `note_link_panels` per note (the Windows link panels, `RightPanePanelsViewModel.cs:465`) | 2,000 | 249 µs, 6.00 |
| `note_load_bundle` per note (the Connections leaf on both hosts) | 2,000 | 224 µs, 6.00 |
| `list_unresolved_links`, pages of 20 | 50 pages | 1,144 µs, 2.00 |
| `get_file_summary` per note | 2,000 | 281 µs, 12.00 |
| `get_file_metadata` per note (control: a plain lookup) | 2,000 | 79 µs, 3.00 |
| `tags_for_files`, every note | 20 chunks | 2,425 µs, 1.00 |
| A rescan with the graph live after 500 dangling links gained their targets | 500 healed | 4.05 per heal |
| A rescan with nothing changed (#1918's fact, control) | 10,001 files | 110 µs, 0.10 |
| A rescan after 1,000 notes changed (the slow path) | 1,000 | 1.03 |
| `save_text` per note | 200 | 16.00 (plain lookups) |

What the counters under those numbers say:

- Every bases row re-prepares four cached statements whatever the view asks: properties, tags, outgoing links and backlinks. The walk shows each with RUN 10,001 and REPREPARE 10,001. The fifth compile is the operator's own statement: a fresh prepare for the five that go through `query_row` (nothing in the cache), a re-prepare for the regex probe and for DQL's tag lookup (RUN 10,001, REPREPARE 10,000).
- The control view that reads no path-keyed statement still compiles 4.01 statements per row. Most of what a bases view pays is outside #1917's list.
- `note_link_panels` runs five statements, all five re-prepared per note, plus one plain `query_row`.
- The canvas pass is the hottest caller of the title lookup: it runs on every scan whether or not a board changed.
- The graph replay re-prepares `graph_linkset_for` and `graph_inbound_rows` once per healed source (RUN 500, REPREPARE 499 each).
- The scan's fast path is clean, as #1918 left it. Its slow path and a save compile only plain lookups.

### The remedies, measured

(a) The #1918 rewrite, a nested id lookup, cached. Re-prepares 0 of 10,000 for every statement tried. Microseconds per call, as written on main to rewritten: tags (45) 30.1 to 14.4; `has_change_since` (49) 27.3 to 17.7; `has_property_change` (50) 27.0 to 13.1; `deleted_content_matches` (51) 25.1 to 19.5; the regex probe (52) 33.7 to 18.9; the title lookup (43) 22.8 to 13.6; `graph_linkset_for` (02) 36.7 to 14.8; `outgoing_links_for` (03) 49.4 to 14.5; the counts (06, 07) 30.7 to 15.0 and 27.4 to 13.7; the open-base probe (42) 17.5 to 12.8. Results: each rewrite returned the same rows as the statement it replaces on 140 of 140 paths (135 notes with rows, the canvas, and four paths the index lacks: an unknown path, the empty string, a path in another case, a path with a trailing slash). On a path the index lacks the subquery is NULL, the equality is never true, and the statement returns what the join returned: no row, or 0 for a count.

Two limits of (a):

- **It does not stop a statement with a bound LIMIT.** `outgoing_links_bounded` rewritten that way is still re-prepared 10,000 of 10,000 times (41.7 µs). With `LIMIT +?2` as well it is 0 and 15.0 µs.
- **It moves two of the four temporal probes onto the wrong index.** `EXPLAIN QUERY PLAN` shows the same indexes before and after for every rewrite except `deleted_content_matches` and its regex twin, which go from `oplog_events_file_ts (file_id=? AND ts_ms>?)` to `oplog_events_class_ts (event_class=? AND ts_ms>?)`. With 62 events in the log nothing shows. With 20,000 content-change events in the window (50 on each of 400 notes), measured over 2,000 probes:

| Probe | Form | VM steps per probe | Index it walks |
|---|---|---:|---|
| `has_change_since` (49) | the join, as on main | 93,430 | `oplog_events_class_ts` |
| | nested id | 130,799 | `oplog_events_class_ts` |
| | nested id, `INDEXED BY oplog_events_file_ts` | 28 | `oplog_events_file_ts` |
| `has_property_change` (50) | the join, as on main | 102 | `oplog_events_file_ts` |
| | nested id, with or without the index named | 110 | `oplog_events_file_ts` |
| `deleted_content_matches` (51) | the join, as on main | 105 | `oplog_events_file_ts` |
| | nested id | 166,929 | `oplog_events_class_ts` |
| | nested id, `INDEXED BY oplog_events_file_ts` | 113 | `oplog_events_file_ts` |
| the regex probe (52) | the join, as on main | 108 | `oplog_events_file_ts` |
| | nested id | 166,956 | `oplog_events_class_ts` |
| | nested id, `INDEXED BY oplog_events_file_ts` | 116 | `oplog_events_file_ts` |

The first row is a defect on main, found here: `oplog.has_change_since` outside the pushed-down position walks every content-change event in the window for every row, on the index migration 029 built for the pushed-down form, though the migration's own comment gives `oplog_events_file_ts` to "the row-eval fallback's per-file EXISTS probes" (`029_oplog_events.sql:26-27`). Through the public API on that log, the view takes 20.2 s for 10,001 rows on main (2,023 µs a row, against 192 µs with a small log).

(b) Only moving an uncached statement into the cache. Slower than leaving it: 49 goes from 27.3 to 34.9 µs, 43 from 22.8 to 34.2, 42 from 17.5 to 25.3, each re-prepared 9,999 times in 10,000.

(c) `SQLITE_DBCONFIG_ENABLE_QPSG`, the query planner stability guarantee, for the record. With it on, the join in 49 taken from the cache is re-prepared 0 of 10,000 times and costs 18.3 µs. Switched on for the session's connection and driven through the callers with no other change: a bases view row drops from 192 to 158 µs and from 5.01 to 1.10 compiles (the uncached probe is what is left), DQL tags from 220 to 126 µs and to 0.00, `note_link_panels` from 249 to 102 µs, `get_file_summary` from 281 to 37 µs. What it switches off, by reading the places `sqlite3.c` tests the flag: sample-based estimates from a bound value (`:87237`; STAT4 is compiled into the bundled build, but Slate's product code never runs `ANALYZE` or `PRAGMA optimize` and a scanned cache holds no `sqlite_stat` table, measured; two tests do run `ANALYZE`, `session/tests/tasks.rs:2962` and `:3086`), the peek at a bound integer or LIMIT (`:115852`), a partial index proved usable by a bound value (`:119504`), and the LIKE and GLOB prefix optimisation for a bound pattern (`:167914`). Measured for the record: a `GLOB` with a bound prefix on `files.path` takes a range search with the setting off and a full index scan with it on, 26.0 µs against 640.4 µs a call on 10,001 files. No product statement has that shape: there is no `GLOB` with a bound pattern under `crates/`, and the seven `LIKE ?` sites compare against columns whose indexes are BINARY while LIKE is case-insensitive, so the optimisation never applies to them; measured for the path scope and the tag prefix, the plan is the same scan with the setting on and off, before and after the pattern is bound. Whether the setting changes any plan Slate runs is in its own section below; the first pass's comparison for it showed nothing and is withdrawn there.

(d) The unary plus, which the measurements added. `files.path = +?1` keeps the join, its plan and its rows, and is re-prepared 0 times: tags 14.2 µs, `has_change_since` 18.3, `backlinks_for` 52.7 to 17.6, `get_file_summary` 294.7 to 34.3, `graph_inbound_rows` 38.2 to 15.6, `load_backlinks` 31.5 to 15.3. It is the only statement-level remedy that fits a keyset cursor, a LIMIT, or an equality that names no path. Every bare equality in the statement needs its own plus: `has_property_change` with the plus on the path alone is still re-prepared 1,999 times in 2,000, because `e.property_key = ?2` is marked too.

### Through the real callers, with each remedy tried

Three trial patches (a fourth, T4, is in the capacity section), applied to product code in the probe worktree, run and reverted. T1: fifteen of the sixteen `files.path` statements, each as the appendix's table writes it (all but the open-base probe, which runs once per base), with the two uncached hot ones cached and the index named in the four temporal probes. It leaves `graph_inbound_rows` alone. T1+T2: T1 plus the unary plus on the seven other statements the measured paths run (`E:1792`, `:1993`, `:2011`, `:2087`, `properties_db.rs:171`, `S:12547`, `dql_inline_fields_db.rs:184`). T3, on main without T1 or T2: the stability guarantee switched on where core opens a connection (`D:282`), `exists_query` and `title_for` moved into the cache with their SQL unchanged, and the index named in the four probes.

| Hot path | Units | main | T1 | T1+T2 | T3 |
|---|---:|---:|---:|---:|---:|
| bases view, `has_change_since` outside the pushed-down position | 10,001 | 192 µs, 5.01 | 188 µs, 4.01 | 103 µs, 0.01 | 104 µs, 0.00 |
| bases view, `deleted_content_matches` likewise | 10,001 | 188 µs, 5.01 | 185 µs, 4.01 | 101 µs, 0.01 | 106 µs, 0.00 |
| bases view, `untouched_for` likewise | 10,001 | 187 µs, 6.01 | 187 µs, 4.01 | 103 µs, 0.01 | 102 µs, 0.00 |
| DQL `TABLE file.tags` | 10,001 | 220 µs, 5.01 | 208 µs, 4.01 | 123 µs, 0.01 | 130 µs, 0.00 |
| DQL `TABLE file.outlinks` | 10,001 | 246 µs, 6.01 | 262 µs, 6.01 | 142 µs, 1.01 | 128 µs, 1.00 |
| DQL `TABLE file.name` (control) | 10,001 | 177 µs, 4.01 | 180 µs, 4.01 | 96 µs, 0.01 | 96 µs, 0.00 |
| `open_canvas`, per file card | 1,000 | 46 µs, 1.00 | 43 µs, 0.01 | 45 µs, 0.01 | 39 µs, 0.00 |
| `outgoing_links` per note | 2,000 | 54 µs, 1.00 | 20 µs, 0.00 | 20 µs, 0.00 | 18 µs, 0.00 |
| `backlinks` per note | 2,000 | 69 µs, 2.00 | 40 µs, 1.00 | 39 µs, 1.00 | 39 µs, 1.00 |
| `note_link_panels` per note | 2,000 | 249 µs, 6.00 | 108 µs, 1.00 | 104 µs, 1.00 | 104 µs, 1.00 |
| `note_load_bundle` per note | 2,000 | 224 µs, 6.00 | 151 µs, 4.00 | 125 µs, 2.00 | 116 µs, 2.00 |
| `get_file_summary` per note | 2,000 | 281 µs, 12.00 | 42 µs, 0.01 | 44 µs, 0.01 | 39 µs, 0.01 |
| `get_file_metadata` per note | 2,000 | 79 µs, 3.00 | 83 µs, 3.00 | 55 µs, 1.00 | 57 µs, 1.00 |
| graph replay, per healed source | 500 | 4.05 | 1.06 | 1.06 | 0.06 |
| bases view, `has_change_since`, 20,000 events in the window | 10,001 | 2,023 µs, 5.51 | 191 µs, 4.41 | 101 µs, 0.01 | 196 µs (see below) |

The compiles that remain under T1+T2 and T3 are plain one-row lookups through `query_row` (`E:3999` in `outlinks_for`, `L:900` in `backlinks_for`, `S:12522` in `get_file_metadata_impl`), and under T1+T2 `graph_inbound_rows`, which T1 did not touch. T3's last cell was measured after the probe itself had switched the setting off again on that connection, so it shows the index name alone: 2,023 to 196 µs.

Read across: T1, which is every hot statement #1917 names, takes a bases view from 192 to 188 µs a row. The row pays for four statements the issue does not list. T1+T2 and T3 both reach about 103 µs and no compile per row.

### Whether the setting changes a plan (the red team's runs)

The first draft of this spec said the plan a first prepare picks is identical with the setting on and off for 235 of 235 product literals. That comparison cannot fail and is withdrawn. On a first prepare no bound value is in hand, so every place the setting gates takes the same branch whatever the setting says; the two plans are identical by construction. The plan that matters is the one a re-prepare picks with its values bound, because on main that is the plan every marked cached statement runs. The red team measured that, three ways:

- **Every literal with a parameter, values bound** (`rt-bound-plans.log`): `EXPLAIN QUERY PLAN` stepped with values bound, setting off, against the plan with the setting on, for the 183 product literals that take a parameter and 5 controls, under 11 value patterns each (NULL; the integers 0, 1, -1 and 7; a path; a path with a LIMIT of 2, 20 and 1000; a `LIKE` prefix; a `GLOB` pattern; a LIMIT of 0). One of the 188 picks a different plan with its value in hand: the planted `files.path GLOB ?1` control. So the comparison can see a difference, and no product literal has one.
- **The callers driven** (`rt-flows-5000.log`): 32 public-API flows, templates and run-time SQL included, on a 5,000-note vault with 50 folders, a hub with 3,000 backlinks, 20,000 tasks and 500 dangling links, with the setting off, on, and off again. Of 59 SQL texts run, 37 ran the same VM steps; 22 differ, each by 2 or 4 steps a run (a bound LIMIT coded as a register and not as a constant). The largest relative change is `outgoing_links_bounded` for embeds, 5,316 against 5,718 steps over 201 runs. Wall time, off / on / off: a bases view on `status == "open"` 419.7 / 239.1 / 416.0 ms; a bases view on `file.hasLink` to the hub 2,178 / 1,255 / 1,791 ms; DQL tags 1,202 / 695 / 1,114 ms; `note_link_panels` for 200 notes 74.7 / 38.3 / 79.8 ms; `get_file_summary` for 200 notes 87.9 / 29.7 / 84.4 ms.
- **The tests that pin plans** (`rt-qpsg-targeted.log`): with the setting on in `open_connection` and `open_in_memory`, 526 core tests passed (`db::tests`, `links_db::tests`, and the session modules `directory_page`, `scan`, `tasks`, `oplog_temporal`, `bases`, `links_embeds`, `two_sessions`, `graph`, `canvas`, `files`), among them the two that run `ANALYZE` and then pin a plan with bound values.

The first pass's own check agrees where it overlaps: for the path-scope `LIKE`, the tag-prefix `LIKE`, two bound-LIMIT link statements, a keyset page and the bases row's properties statement, the plan is the same before and after the values are bound, with the setting on and off.

So, on this schema and with no statistics, no product statement or template changes plan under the setting, by a comparison that could have shown otherwise.

### `oplog.untouched_for` walks the wrong index too (second pass, after the red team)

The red team found that `oplog_untouched_for` (`E:3925-3936`) has the defect the first pass found in `oplog_has_change_since`: its correlated `NOT EXISTS (… FROM oplog_events e WHERE e.file_id = f.id AND e.event_class = 1 AND e.ts_ms >= ?2)` takes the covering `oplog_events_class_ts`. Re-run here on a second vault: 1,000 notes whose `mtime_ms` was set sixty days back (the notes the operator is asked about), 20,000 content-change events in the window on 400 of them.

| Probe | Form | A note with 50 events: VM steps a call | A note with none | Index it walks |
|---|---|---:|---:|---|
| `untouched_for` | as on main | 49,907 | 100,029 | `oplog_events_class_ts` |
| `untouched_for` | `oplog_events e INDEXED BY oplog_events_file_ts` in the subquery | 34 | 32 | `oplog_events_file_ts` |
| `has_change_since` | as on main | 49,898 | 100,016 | `oplog_events_class_ts` |
| `has_change_since` | the join, the index named after the alias | 25 | 19 | `oplog_events_file_ts` |

These match the red team's figures (49,907 and 100,030; 34 and 32; 49,898 and 100,017; 25 and 19). Through the public API on main, 1,001 rows: the view `oplog.untouched_for("30d") || …` takes 1,761 µs a row and the `has_change_since` view 1,708, against 255 for `has_property_change`, which walks the right index.

### The statement cache's 16 slots (second pass, after the red team)

The red team found cached statements dropped and built again during a rescan. Measured here with a counter that is exact for it: a profile hook that reads each finished statement's RUN counter, where a statement object's first finished run reads 1, so every such event is one statement object built; re-prepares are counted beside it. Every run starts from an empty cache. The vault is the red team's: 1,000 notes in 50 folders, of which 300 are rewritten before each rescan (six in a row in each folder, then fourteen unchanged). 1,024 slots is the reference, where nothing is ever dropped.

**(a) At 16.** A changed note that carries headings, links, an embed, tags, a task, properties, an inline field, two block ids, two citations and a base fence runs fifteen cached statements. An unchanged note runs three, one of them shared. That is seventeen in all, one more than the cache holds. With any one of block ids, citations or the base fence alone the count is fifteen or sixteen and nothing is dropped (measured: 0 rebuilt for each of the four smaller mixes, and 0 on a 40-note vault whose notes ran sixteen). With all of them, fourteen statements are dropped and built again at each boundary between a run of changed files and a run of unchanged ones: 50 builds each over the rescan, 689 extra compiles, 16.6 compiles a changed file against 14.3.

The fourteen: `INSERT INTO` `links`, `headings`, `tasks`, `file_tags`, `dql_file_tags`, `dql_inline_fields`, `dql_inline_field_state`, `properties_list_values`, `blocks` and `bases_blocks`, and four that have an equality on `files.path`: `SELECT id FROM files WHERE path = ?1` (inventory row 01 at `S:12234`), `DELETE FROM file_meta WHERE file_id = (SELECT id FROM files WHERE path = ?1)` (row 32), and the two #1918 put in the cache, its probe (row 30) and the fast path's `UPDATE` (row 31). The red team's reading of its own counters was that every file builds them again; counted exactly, it is once per boundary, so 2.3 a changed file in this vault and at most fourteen a changed file when every changed file sits between unchanged ones.

**(b) At 32, 64 and 128.** Three rounds each, interleaved with 16 and the reference:

| Capacity | Compiles a changed file | Statements dropped and rebuilt | µs a changed file, three rounds |
|---:|---:|---:|---|
| 16 | 16.6 | 14 | 2,991; 3,198; 2,910 |
| 32 | 14.3 | 0 | 3,056; 2,919; 3,143 |
| 64 | 14.3 | 0 | 2,898; 3,255; 3,576 |
| 128 | 14.3 | 0 | 3,092; 2,861; 3,216 |
| 1,024 | 14.3 | 0 | 2,895; 3,568; 3,541 |

The time shows nothing: the spread between rounds at one capacity is larger than any difference between capacities. Five more rounds each at 16 and 64 without the hook gave medians of 2,803 and 2,567 µs with ranges that overlap (2,653 to 4,043; 2,314 to 5,379). By the counts, 689 compiles of about 10 µs are about 7 ms of a 900 ms rescan.

The 14.3 that no capacity removes is fourteen statements whose sites never cache them: eleven `DELETE FROM … WHERE file_id = ?1` (or `source_file_id`), `INSERT INTO file_citations`, `UPDATE files SET index_epoch = ?1 WHERE id = ?2` and `UPDATE index_epoch_clock …`. Each is compiled once per changed file. None names a path.

**(c) Memory.** `SQLITE_DBSTATUS_STMT_USED` on the session's connection after a slow rescan, a bases view with the six temporal operators, a DQL query, the link panels, bundle, metadata, outline, tasks and summary for 200 notes and twenty saves; then again after the red team's 32 flows as well:

| Capacity | After the scan, the views and the panels | After every flow | Cached statements then |
|---:|---:|---:|---:|
| 16 | 105,216 bytes | 205,440 bytes | 17 |
| 32 | 224,160 | 531,992 | 33 |
| 64 | 382,928 | 845,120 | 62 |
| 128 | 382,928 | 845,120 | 62 |

Everything driven uses 62 distinct cached statements, so 64 and 128 cost the same. Nine more statements on the connection are FTS5's own (14,280 bytes), at every capacity. The largest cached statements are the three CTEs, 34 to 39 KB each; the mean is 11 to 16 KB. Lookaside in use rose from 29 slots to 57 and 61 (its high-water mark is 123 at every capacity). For scale, the connection's page cache held 3.7 to 4.0 MB in the same runs.

**(d) The guards.** By reading: `session/tests/two_sessions.rs` reads the page cache's bytes per connection (`db_cache_status().main.used`, exact in two facts), the process's `memory_used` and `pagecache_overflow` as lower bounds on an 8 MiB pad, and a ratio of rescan times; `rust.yml:173-175` runs three perf guards by name. None reads statement memory, and none pins a ceiling on process memory. By running, with trial T4 (the setting and a capacity of 32 applied in `open_connection` and `open_in_memory`): the fourteen tests matching `two_sessions` passed, the page-cache facts and `perf_guard_two_sessions_do_not_evict_each_others_cache` among them, and so did `perf_guard_unresolved_link_rescan_within_twice_the_link_free_one`, `perf_guard_root_listing_under_100ms_on_10k_files` and `a_rescan_prepares_its_per_file_statements_once`. That was a debug build; the lane runs the perf guards in release.

**(e) Every other hot path.** With the setting on (so a re-prepare cannot hide a rebuild), each of these compiled exactly as often at 16 as at 1,024: the scan's fast path, `save_text` of 100 rich notes, a bases view with the six temporal operators over rich notes, a plain bases view, DQL with tags, outlinks and inlinks, DQL `TASK`, `note_link_panels` and `note_load_bundle` for 200 notes, a note opened (metadata, outline, tasks, link panels and summary; eleven cached statements a note), `open_canvas` on a board of 200 file cards, and the red team's 32 flows run once. A first scan, where every file is changed, runs fifteen and fits. Only the rescan that mixes changed rich notes with unchanged ones goes over.

## Design

The design is the connection setting (the first draft's "Design B"; the lead's ruling after the red team). The first draft's other design, statement by statement, is described under "Alternative, not taken" and its statement table is the appendix, so the lead can still rule for it (Call 1).

### 1. One function configures every connection core opens (`D`)

`open_connection` (`D:282-296`) is the one place product code opens a SQLite connection: the session's (`open_database`, `D:252-272`), the compactor's (`S:1628-1636`) and the env-gated second connections of four test seams all go through it, and nothing else under `crates/` calls `Connection::open…` outside test code (`D:293`, `D:295`, and `D:324` in the test-only `open_in_memory`). It becomes:

```rust
pub(crate) fn open_connection(path: &Path) -> rusqlite::Result<Connection> {
    let conn = open_unconfigured(path)?;
    configure_connection(&conn)?;
    Ok(conn)
}
```

`open_unconfigured` is today's body, both branches. `configure_connection(&Connection)` has no `cfg` in it and does two things: it switches on `SQLITE_DBCONFIG_ENABLE_QPSG` through `set_db_config`, and it sets the statement cache's capacity to 32 (Call 2). `open_in_memory` (`D:323-328`) calls it too.

The shape matters because `open_connection` has two returns today. A session's cache opens by its anchored path, which on macOS returns early through `Connection::open_with_flags_and_vfs` (`D:283-294`); Linux and Windows have no anchored VFS and take the last line (`D:295`). (Corrected after the diff red team: only macOS takes the anchored branch; `anchored::VFS` is `None` on Linux and on Windows, and no CI lane runs core's tests on macOS. See "What delivery changed".) A setting added after the last `Connection::open` only would leave every Mac session without it. With one exit and a helper that knows nothing of the branch, whichever branch opened the connection is configured. Trial T3 ran the wrapper with the setting; trial T4 ran this shape with both settings.

With the setting on, SQLite does not look at bound values while it plans, marks no parameter, and expires no statement when its bindings are cleared. No SQL changes for that: the joins stay joins, and the 38 literals, the templates, the SQL assembled at run time, and every statement written later are covered alike.

What it costs: Slate gives up plans chosen from bound values. Measured, that is no plan Slate has (the section above). By reading, the four things it switches off are: the prefix optimisation for a bound `GLOB` or case-sensitive `LIKE` on an indexed column (no product statement has one; one written later would scan, 26 against 640 µs a call on 10,001 files, and nothing would warn); the use of a bound LIMIT's value (2 to 4 VM steps a run in the red team's flows); per-value STAT4 estimates, should a later chunk ever gather statistics (STAT4 is compiled in; stat1 averages would still apply); and a partial index proved usable by a bound value (the four partial indexes' conditions are met today by literals or by an equality that needs no value). A test that pins a plan on a connection core opens pins the plan the app runs. A test that opens a raw `rusqlite::Connection` is planned without the setting, as today.

### 2. The two uncached hot statements go through the cache

- `exists_query` (`E:3694-3709`): `self.conn.prepare_cached(sql)` and then `query_row(...).optional()` on the cached statement, with the same error mapping. That caches `oplog_has_change_since`, `oplog_has_property_change`, `oplog_deleted_content_matches`, `oplog_created_since` and `oplog_untouched_for`.
- `title_for` (`S:22277-22296`): `prepare_cached`, then `query_row(...).optional()`, then the same `.ok().flatten()` and decoding as today. Its SQL does not change.

Under the setting a cached join is not re-prepared (0 of 10,000, 18.3 µs against 27.3 as written), so caching is enough.

### 3. Five temporal probes name their index (`E`)

The five SQL texts become `const`s beside `SqlVaultLookup`, so a fact reads the plan of the text the product runs:

- `oplog_has_change_since`: `SELECT 1 FROM oplog_events e INDEXED BY oplog_events_file_ts JOIN files f ON f.id = e.file_id WHERE f.path = ?1 AND e.event_class = 1 AND e.ts_ms >= ?2 LIMIT 1`
- `oplog_has_property_change`, `oplog_deleted_content_matches` and the regex probe: the same `oplog_events e INDEXED BY oplog_events_file_ts JOIN files f …`, the rest of each unchanged
- `oplog_untouched_for`: `SELECT 1 FROM files f WHERE f.path = ?1 AND f.mtime_ms < ?2 AND NOT EXISTS (SELECT 1 FROM oplog_events e INDEXED BY oplog_events_file_ts WHERE e.file_id = f.id AND e.event_class = 1 AND e.ts_ms >= ?2) LIMIT 1`

This fixes two defects on main (`has_change_since` and `untouched_for`, about 100,000 VM steps a row down to about 30 on a log of 20,000 events) and pins the other three where they already are. `oplog_created_since` reads no event and keeps its SQL. If a later migration drops or renames the index, the statement does not prepare and the view reports that in-band, as any probe error does today (`E:3705-3708`); the plan fact catches it first. By the red team's reading the name cannot be missing on a schema a session runs: migration 029 creates the index, every session migrates before it plans a probe, and CB-32's migration 042 leaves it alone.

### 4. The helpers move (tests)

`statements_reprepared_more_than` moves from `scan.rs:1525-1548` to `session/tests/common.rs`, beside a new `cached_statement(conn, sql) -> (run, reprepares)`. It matches a statement by its whole SQL text with whitespace flattened, against the `const` the product runs, and panics unless exactly one statement on the connection matches. A needle would not do: the probes share long stretches (two of them are identical up to `e.ts_ms >= ?2`), and two matches mean a duplicate was prepared while the cached one was out.

### 5. What the setting gives up is closed as a class (ruling 1)

The setting stops SQLite from using a bound value to pick a plan. In this codebase that could matter in one way only: an index prefix search for a bound pattern. So the build pins that no statement can have one.

What the shapes are, by SQLite's rules for the LIKE optimisation:

- **`GLOB` with a bound pattern** on a column with a BINARY index. Every text index in the schema is BINARY (`files.path` first among them), so any `col GLOB ?` on an indexed column is this shape. Measured: a range search with the setting off, a full index scan with it on, 26 against 640 µs a call on 10,001 files. Product code has none today (the only `GLOB` under `crates/` is in comments).
- **`LIKE` with a bound pattern** can use an index prefix in two cases only. Either `PRAGMA case_sensitive_like` is on, which makes `LIKE` case-sensitive and lets it use a BINARY index: nothing under `crates/` sets it, and if anything did, the path scope's `files.path LIKE ?4` (`search_db.rs:179`) would become this shape and would also change what it matches. Or the indexed column's collation is NOCASE, which lets the default case-insensitive `LIKE` use it: no index and no column in the 41 migrations is NOCASE (the two `COLLATE NOCASE` under `crates/` are `ORDER BY name COLLATE NOCASE` in two list queries, not an index). That is what "a case-sensitive LIKE prefix" means here: a `LIKE` whose case rule matches its index's collation. Today none can exist, which is why the seven `LIKE ?` sites plan the same with the setting on and off.

How each is guarded, and which judge was chosen:

- **`LIKE`: SQLite judges, at the level of the connection and the schema, not the statement** (fact 17). On every connection core opens, `SELECT 'a' LIKE 'A'` is 1, so `LIKE` is case-insensitive there. On a migrated cache, no key column of any index has the collation NOCASE, read from `PRAGMA index_list` and `PRAGMA index_xinfo` for every table. With both true no `LIKE` anywhere can take a prefix search, whether its SQL is a literal, a `format!` template or assembled at run time by the bases compiler. A text rule on statements cannot do this at all, so this is not a choice between two ways of reading SQL: it is the only place the property lives.
- **`GLOB`: a text rule** (fact 16). The census reads the code lines of product source (comment lines dropped, test code skipped as in fact 12) and the migration files, and refuses the word `GLOB` unless a quoted literal pattern follows it. Because it reads lines and not whole statements, it sees a `GLOB` in a literal, in a `format!` template and in a clause that is one piece of run-time SQL alike. A pattern that is a literal is allowed: the setting changes nothing for it.
- **Why not SQLite as judge for `GLOB` too.** It can be: prepare each product literal with the setting off, bind a prefix pattern, and compare its plan with the plan under the setting, as the red team's control did. That was weighed and not chosen as the guard, for three reasons. It needs the string-literal reader this design dropped. It sees only literals that compile alone, so the nineteen templates and all run-time SQL are outside it, and those are exactly where a `GLOB` would be easy to miss. And it needs a guess at which parameter is the pattern. The text rule reaches everything and guesses nothing. SQLite is still used as the judge of the reasoning, in the planted case (fact 18): the same `GLOB` and a NOCASE-indexed `LIKE` are shown to search on a raw connection and scan on a configured one, so if a later SQLite changes what the setting gives up, that fact moves.

The ceiling, written in the census's doc comment: it reads the spelling `GLOB` in upper case, as every SQL keyword in this codebase is written; a pattern operator assembled from pieces of a word is not seen; the index walk reads the main schema of a migrated cache, not a temp table made at run time; and it guards the prefix search only. The other three things the setting switches off are not guarded because nothing can lean on them unnoticed: a bound LIMIT's value changes 2 to 4 VM steps and no plan; per-value statistics need an `ANALYZE` product code never runs; and a partial index proved by a bound value would need a statement that today's four partial indexes do not have.

### 6. Every connection kind that exists today (ruling 1), by reading at 7a34cb6e

| Kind | Where it is opened | How fact 11 reaches it |
|---|---|---|
| The session's connection | `VaultSession::open`, `S:2722`, through `open_database` (`D:252-272`), which calls `open_connection` | `session.conn` |
| Migrations | none of their own: `db::migrate(&mut conn)` runs on the session's connection straight after it opens (`S:2732`), so migrations run configured | with the session's |
| The compactor's worker | `open_worker_connection`, `S:1628-1636`, through `open_connection` | `open_worker_connection_for_tests` (`S:2274`), then the worker slot |
| Four env-gated second connections in product code (test seams that ship) | `S:4980`, `S:9038`, `tasks_db.rs:113`, `tasks_db.rs:191`: each calls `db::open_connection` on the session connection's own path, runs one or two statements and drops it | not held by any fact, and cannot be: each is a local inside a product function. The fact opens a connection the way they do (`open_connection` on `conn.path()`) and reads that one; fact 12 holds that they have no other way to open |
| In-memory | `open_in_memory`, `D:323-328`, test builds only | called directly |
| Read-only | none exists: the only `OpenFlags` in product code is `OpenFlags::default()` at `D:293` | nothing to reach; fact 12 refuses `Connection::open_with_flags` outside `db.rs` |
| The CLI's and the FFI's | none: `slate-cli` and `slate-uniffi` go through `VaultSession` and open nothing (the CLI holds rusqlite only to match an error type) | fact 12 reads both crates |
| Raw connections that tests open for themselves | test code only | not core's; left unconfigured on purpose, and fact 10 uses one as its control |

### Left as they are, with the reason the issue asks for

- Every other statement keeps its SQL. The eleven that are cached and re-prepared today stop being re-prepared because of the setting, not because of an edit.
- The statements whose sites prepare them fresh and that are plain one-row lookups stay uncached (`E:1714`, `:3969`, `:3984`, `:3999`, `dql_inline_fields_db.rs:165`, `L:900`, `S:12522` and the rest of the first group): caching one saves about 1.6 µs of 14.7.
- `S:19131-19135` `collect_rewrite_candidates` (held across its loop), `S:19944-19947` (once per base opened), `S:7098-7101` and `S:3036-3037`: prepared once per action; under the setting none is compiled twice.
- The bases candidate query (`E:834-846`) compiles once per execution under the setting, where it compiles twice today.
- The scan's fourteen uncached slow-path statements: not in this chunk (a #1955 line).

## Alternative, not taken: statement by statement

The first draft's "Design A". Each cached statement that re-prepares is rewritten: a nested id lookup (#1918's remedy) where it needs only the file's id, and a unary plus on the parameter where it must keep its join, on every bound LIMIT, and on an equality that names no path. Its statement table is the appendix. Measured through the callers it reaches the same numbers as the setting once it covers the seven statements beside #1917's (T1+T2 against T3 above).

Why not:

- **It grows toward L.** 17 to 24 statement edits in four to six product files to cover the measured paths, and the census it needs has a string-literal reader, a planted fact and three pinned lists. The red team's view is that it should split, census first, under plan rule 4.
- **Every later statement must carry the idiom, and the idiom is not a rule a person can apply.** The class has shapes no short rule covers (`GROUP BY`, `DISTINCT`, `COUNT(DISTINCT …)`, a window's `ORDER BY`, a join inside a subquery, the parameter on the left), and a held statement still re-prepares on `LIKE ?` and on an integer crossing 0 or 1.
- **It leaves SQL it cannot judge.** Nineteen `format!` templates and the bases compiler's clauses are seen by no census of literals; fifteen literals outside the measured paths would ship as dated exceptions.
- **Its rewrites are where plans actually move.** The nested form takes `deleted_content_matches` and its regex twin onto the wrong index (105 to 166,929 VM steps) unless the index is named, and its `title_for` (`… LIMIT 1` with no ORDER BY) may return another row than the join when a note has two `title` rows of kind text (the red team's note 14; under A it would need `ORDER BY ordinal` or the plus form).

Calls that exist only under it, should the lead rule for A: how far it goes (the first draft recommended the 24: the sixteen `files.path` statements, `graph_inbound_rows` and the seven on the measured paths; with the sixteen alone a bases view does not change, 188 µs and 4.01 compiles a row); and the census's oracle (SQLite judging each literal taken from the cache twice, not a regex on `files.path` joins, which finds 16 of the 38; its reader must skip only an inline test module's braces and never an out-of-line `#[cfg(test)] mod tests;`, of which `hot_exit.rs:54-55` and `canvas/mod.rs:717-718` are two with product code after them).

## Facts

The guard has three parts: runtime facts, one per hot path (1 to 9) with the instrument's own planted case (10), and one across the whole class (15); the guards on the connection (11 to 14); and the guards on what the setting gives up (16 to 18). Every fact is written first and seen red on main's code. `C` is `session/tests/common.rs`. The runtime facts use a 40-note vault, as #1918's does, and the public session API. "In the cache, run from it, not re-prepared" means: `cached_statement` finds exactly one statement with the product's SQL after the call returns, its RUN counter is at least the number of rows (or cards, or notes) the path served, and its REPREPARE counter is at most 2 (#1918's allowance for a schema change or two). All three are needed. Presence alone passes for a statement that was dropped and prepared again for every row. The re-prepare count alone passes for `query_row`, which leaves nothing to count: this is the reading's vacuous pass, and on main it is the state of four of the seven hot statements (43, 49, 50, 51). RUN is what tells a statement that served every row from one the cache dropped and remade.

1. `a_bases_view_prepares_each_temporal_probe_once` (`oplog_temporal.rs`). For each of the six operators in a filter of the form `<operator> || file.name == "zzz"`: the probe's statement is in the cache, run from it, not re-prepared. Red on main: five are absent from the cache; the regex probe is there with 39 or 40 re-prepares. Mutations: `exists_query` back to `query_row` (absent); the setting removed from `configure_connection` (each join probe re-prepares once a row); the cache shrunk to two statements inside the fact (RUN falls to 1 or the statement is absent), which is the operand that shows RUN bites.
2. `a_temporal_probe_reads_only_its_own_notes_events` (`oplog_temporal.rs`). `EXPLAIN QUERY PLAN` of each of the five probe constants names `oplog_events_file_ts`. With 2,000 content-change events on other notes, a `has_change_since` probe for a note with none takes fewer than 200 VM steps, and so does an `untouched_for` probe for a note with none whose `mtime_ms` is before the cutoff (set back in the fact: a note modified inside the window never reaches the subquery). Red on main for both (the plans name `oplog_events_class_ts`; about 100,000 steps at 20,000 events). Mutations: `INDEXED BY` dropped from `has_change_since` (red on its plan and its bound); from `untouched_for` (the same).
3. `the_row_eval_probes_agree_with_the_reference` (`oplog_temporal.rs`). The existing census `census_temporal_operators_vs_reference` (`:1058`) checks each operator against a model in the pushed-down position. This runs the same generated probes wrapped as in fact 1, so the statements this chunk touches are checked against the same model. Green on main; it is the fact that holds their results. Mutation: one probe's file filter removed, so every note with any event matches.
4. `a_dql_query_prepares_its_tag_lookup_once` (`bases.rs`). `TABLE file.tags` over 40 notes: the tag statement is in the cache, run from it, not re-prepared. Red on main: 39 or 40 re-prepares. Mutation: the setting removed.
5. `a_bases_view_re_prepares_nothing_per_row` (`bases.rs`). A plain view over 40 notes: `statements_reprepared_more_than(&conn, 2)` is empty for the whole connection, #1918's own assertion, and each of the four row statements is in the cache with RUN at least 40. Red on main: the four row statements, 40 re-prepares each. What it does not see, written in its doc comment: a lookup that goes through `query_row` leaves nothing on the connection, so the plain per-row lookups this chunk leaves alone (one a row for `file.outlinks`, measured) pass beside it. Mutation: the setting removed.
6. `a_canvas_pass_prepares_its_title_lookup_once` (`canvas.rs`). A board of 40 file cards. After `open_canvas` the title statement is in the cache with RUN at least 40; after a rescan with nothing changed its RUN has grown by at least 40 more; at most 2 re-prepares. The counter is read after each phase, so the fact does not depend on the statement surviving anything between them, and its doc comment says its rescan must stay on the fast path. Red on main: absent. A card that names a missing note keeps its file-name title, asserted in the same fact. Mutation: `query_row` back.
7. `a_notes_link_reads_are_prepared_once` (`links_embeds.rs`). `note_link_panels` and `note_load_bundle` over 40 notes: no statement on the connection is re-prepared more than twice, and each of the five link statements is in the cache with RUN at least 40. Red on main: five statements at 39 or 40. Mutation: the setting removed.
8. `a_graph_replay_prepares_its_reads_once` (`graph.rs`). With the graph built, forty dangling links gain their targets and a rescan heals them: `graph_linkset_for`'s and `graph_inbound_rows`'s statements are in the cache, run 40 times each, not re-prepared; the graph is still live. Red on main: 39 re-prepares each.
9. `a_file_summary_is_prepared_once` (`files.rs`). Forty `get_file_summary` calls: at most 2 re-prepares. Red on main: 39.
10. `a_cached_join_is_re_prepared_only_without_the_setting` (`scan.rs`, beside #1918's fact). The planted case for the instrument and for the cause. A raw second connection to the same cache, opened without `configure_connection`, takes the tag join from its statement cache twice and steps it: REPREPARE reads 1. The session's connection does the same: 0. Red on main (the session's reads 1). It shows the counter can see the defect, and that the setting is what stops it. If a later SQLite removes the cause, the raw connection reads 0 and this fact says so on the bump, which is the signal that the setting has become a choice and not a need.
11. `every_connection_core_opens_is_configured` (`two_sessions.rs`, which already opens the compactor's connection). It walks the kinds in design section 6: the session's connection (on which the migrations ran), the compactor's (through `open_worker_connection_for_tests`, `S:2274`), a second connection opened the way the four seams open theirs (`open_connection` on the session connection's own `path()`), and `open_in_memory`. Each reports the setting on, keeps 32 of 40 statements offered to its cache (rusqlite has no getter for the capacity), and answers 1 to `SELECT 'a' LIKE 'A'` (fact 17's first half, read here because this is the fact that holds the connections). The seams' own connections cannot be held; that is said in the fact's doc comment, with fact 12 named as what covers them. And `configure_connection` on a raw in-memory connection takes the setting from off to on. Red on main. Which branch it proves depends on the lane: on Windows `open_unconfigured` takes its last line, so the Windows lane proves that one; the rust lane (Linux arm64, `rust.yml:82`) and the Mac lane open a session's cache through the anchored branch and prove that one. (Wrong: the rust lane is Linux and takes the plain branch, and the Mac lane does not run this fact.Corrected after the diff red team: only macOS takes the anchored branch; `anchored::VFS` is `None` on Linux and on Windows, and no CI lane runs core's tests on macOS. See "What delivery changed".) Mutations: the call removed from `open_connection` (red on every lane); the call moved inside one branch of `open_unconfigured` (red only on the lane that takes the other branch, which is why the helper is called once, after both).
12. `product_code_opens_a_connection_only_through_db` (new `crates/slate-core/src/db/connection_census.rs`, a test module in the style of `vault/test_trash_census.rs`). The source census. It reads every `.rs` file under the three crates' `src` trees and takes the code lines of product code (comment lines dropped; `session/tests/`, `*_tests.rs`, `test_*.rs` and each file's trailing `#[cfg(test)] mod … {` block skipped). A line that matches `Connection::(open\w*|from_handle\w*)\s*\(` is allowed only inside `open_unconfigured` and `open_in_memory` in `db.rs`. Today's sites are the three named in the design. Red on main only in that today's two calls sit in `open_connection` itself; what it guards afterwards is a connection opened anywhere else. Its ceiling, in its doc comment: it reads that spelling only, so a connection reached through an alias of the type is not seen.
13. `the_connection_census_sees_a_planted_open` (same file). A fixture source with `rusqlite::Connection::open(path)` in a product function is flagged; the same line in a comment, in a trailing test module, and inside a function named `open_unconfigured` in a file that is not `db.rs` are respectively ignored, ignored and flagged.
14. `a_rescan_of_changed_and_unchanged_notes_keeps_its_statements` (`scan.rs`). The capacity fact (Call 2). Forty notes that each carry headings, links, an embed, tags, a task, a list property, an inline field on its own line, two block ids, two citations and a base fence; every other note is rewritten; one rescan. Then: the seventeen statements named in step 0 are all on the connection (a cache of 16 cannot hold them), and #1918's probe and `INSERT INTO links` each have RUN equal to the times the rescan ran them (20, and the links inserted), so neither was dropped and rebuilt. Red on main. The fact first asserts that the rescan ran all seventeen, because a note that runs sixteen fits in 16 and proves nothing (measured: the first 40-note vault tried had its inline field on a task's continuation line, ran sixteen, and showed no rebuild at any capacity). Mutation: the capacity back to 16.

15. `no_statement_is_re_prepared_per_binding_on_any_driven_path` (new `session/tests/statement_class.rs`). The whole class, by SQLite's own counter. A new helper in `C`, `PrepareCounter`, sets a profile hook (`sqlite3_trace_v2`, `SQLITE_TRACE_PROFILE`, through `rusqlite::ffi` as the #1918 helper already does) that reads two counters of every statement as it finishes: `SQLITE_STMTSTATUS_REPREPARE` (read and reset) and `SQLITE_STMTSTATUS_RUN`, where a first finished run reads 1 and so marks a statement object newly built. It yields, per SQL text: runs, statement objects built, re-prepares. This is the step 0 second-pass instrument, and unlike a walk of the connection it loses nothing when the cache drops a statement and sees a statement no cache ever held.
    One 40-note vault of rich notes, a board of 40 file cards, a few saves for events, and the graph built. Each flow below runs under its own counter, at least 12 times with a different note, page or query each time, so "per binding" has twelve chances. Two assertions per flow. First, the class: every SQL text the flow ran 12 times or more has at most 2 re-prepares. Second, against the vacuous passes: each member the flow is listed for matches exactly one SQL text that ran (by a needle the fact asserts unique among the texts run), that text ran at least 12 times, and it ran on at most 2 statement objects. The last clause is presence in the cache and the RUN counter in one: a statement that `query_row` builds per call, or that the cache dropped, shows as many objects as runs.

    | Flow, through the public API | Members of the 38 it reaches |
    |---|---|
    | a plain bases view | `E:1792`, `E:1993`, `E:2011`, `E:2087` |
    | a bases view for each temporal operator outside the pushed-down position | `E:3809`, `E:3823`, `E:3839`, `E:3877` (and `untouched_for` and `created_since`, which are not among the 38) |
    | DQL `TABLE file.tags`; a DQL query that reads an inline field | `T:123`; `dql_inline_fields_db.rs:184` |
    | `note_link_panels`, `note_load_bundle`, `backlinks`, `list_unresolved_links` in pages of 2 | `L:719`, `L:801`, `L:810`, `L:835`, `L:840`, `L:868`, `L:938` |
    | `get_file_metadata`, `note_outline`, `note_tasks`, `tasks_for_file` | `properties_db.rs:171`, `S:12547`, `S:7914`, `tasks_db.rs:215`, `tasks_db.rs:122` |
    | `resolve_embed` of a heading embed | `S:7411` |
    | `tag_tree_page`, twelve calls | `S:9545` |
    | `open_canvas`, `canvas_outline`, `canvas_table_rows` | `S:22280`, `S:22612`, `S:22665` |
    | `full_text_search` over the vault and a folder; with a tag scope, with a query and with none | `search_db.rs:173`, `:300`, `:355` |
    | a rescan with the graph live after dangling links gain their targets | `L:436`, `L:464` |
    | `list_files`, `list_dir_children_page`, `filter_files`, `tasks_in_vault`, `files_with_property`, `files_with_property_key`, `get_file_summary` | none of the 38: these are the `format!` templates no reading of literals could judge (`S:12865`, `S:13052`, `S:13101`, `S:13291`, `properties_db.rs:270`, `:422`, `tasks_db.rs:365`, `directory_page.rs:621`), held by the first assertion |

    That is 32 of the 38. The six not reached, and why: `citations_db.rs:111`, `:327` and `:389`, `S:10269` (`rename_property_impl`) and `S:19132` (`collect_rewrite_candidates`) use `prepare`, and `S:19944` (`ensure_open_base_indexed`) uses `query_row`. None is ever in the statement cache, on main or after, so nothing clears its bindings and main does not re-prepare it per binding; the sweep listed them for what would happen if they were cached, and under the setting that cannot happen. (One of them, `:327`, has a `LIKE ?1` and so compiles twice per call on main, once per search; under the setting it compiles once. The fact does not hold that.)
    Red on main: 28 of the 32 show a re-prepare for every run but the first; the other four (`E:3809`, `E:3823`, `E:3839`, `S:22280`) show an object per run, because main runs them through `query_row`. Mutations: the setting removed from `configure_connection` (the first assertion lists the class); `exists_query` back to `query_row` (three members show an object per run); the capacity set to 4 inside the fact (members show more than 2 objects), which is the operand that shows the second assertion bites.
16. `no_product_statement_binds_a_glob_pattern` (`db/connection_census.rs`, beside fact 12, with the same reader of code lines). The text census of design section 5: the word `GLOB` in a code line of product source or in a migration file is refused unless a quoted literal follows it. Zero sites on main, so it is green on main; its planted case is fact 18.
17. `no_like_can_use_an_index_prefix` (`two_sessions.rs` for the connections, with fact 11; `db.rs`'s tests for the schema). SQLite as judge. On a migrated cache, for every table in `sqlite_master`, no key column of any index reported by `PRAGMA index_list` and `PRAGMA index_xinfo` has the collation NOCASE; and no code line of product source or migration names `case_sensitive_like`. With fact 11's `'a' LIKE 'A'` on every connection, no `LIKE` can take a prefix search. Green on main; its planted cases are in fact 18.
18. `the_guards_see_each_way_a_bound_prefix_search_could_come_back` (`db/connection_census.rs` and `db.rs`'s tests). The planted cases, each red when planted. For fact 16: a fixture line `WHERE path GLOB ?1` is flagged; `GLOB 'n*'` is not; the same in a comment is not; a `format!` fragment that ends `GLOB` is flagged. For fact 17: on a scratch in-memory connection, a table with a `TEXT COLLATE NOCASE` column and an index on it is flagged by the index walk; `PRAGMA case_sensitive_like = ON` makes the `LIKE` probe read 0. And SQLite as judge of the reasoning: `EXPLAIN QUERY PLAN` of `SELECT COUNT(*) FROM files WHERE path GLOB ?1`, stepped with a prefix bound, reads a SEARCH on a raw connection and a SCAN on a configured one, and the same for a `LIKE ?1` on the scratch NOCASE index. If a later SQLite changes what the setting gives up, this last part moves and says the guards need another look.

Existing facts that must stay green: `a_rescan_prepares_its_per_file_statements_once` (`scan.rs:1551`), all of `oplog_temporal.rs` (95 uses of the operators, the reference census among them), `tags_db.rs`'s own `load_dql_tags_for_path` tests (`:424-428`, the missing path among them), the link panel and canvas suites, every test that pins a plan (the red team's 526 passed with the setting on), and the two-sessions guards (run once with trial T4).

There is no census of SQL literals in this design. With the setting on, no literal can be re-prepared from the cache of a connection core opens, so such a census could not fail; what can regress is a connection without the setting, and facts 11 to 13 hold that. Fact 15 is not that census: it reads nothing from source, it drives the callers, and it is red on main.

**The Windows twin** (plan §7): none, Call 6.

## Files it edits

Product, three files: `D` (`open_connection`, `open_unconfigured`, `configure_connection`, `open_in_memory`), `E` (`exists_query`, the five probe constants), `S` (`title_for`; one `mod` line in the tests list for the new test module).

Tests: `C` (`statements_reprepared_more_than`, `cached_statement`, `PrepareCounter`), `session/tests/scan.rs` (the helper leaves; facts 10 and 14), `oplog_temporal.rs` (1 to 3), `bases.rs` (4, 5), `canvas.rs` (6), `links_embeds.rs` (7), `graph.rs` (8), `files.rs` (9), `two_sessions.rs` (11, and the connections half of 17), the new `session/tests/statement_class.rs` (15), the new `crates/slate-core/src/db/connection_census.rs` with its `mod` line in `D` (12, 13, 16 and the text half of 18), and `D`'s own tests (the schema half of 17 and of 18). Two new `.rs` files: `git add`, then the licence header script.

Documents: `BENCHMARKS.md` (a release-build before and after for a bases view, DQL tags, the link panels and `get_file_summary`, beside #1918's section; the statement memory at 16 and 32).

Not edited: `links_db.rs`, `tags_db.rs`, any file under `apps/`, `crates/slate-uniffi` or `crates/slate-cli`. No migration.

## Overlap

**CB-32** is not merged: `origin/main` was still 7a34cb6e when this was last checked (2026-11-21, after the red team), so no line in this spec has moved yet. Its branch is at ed7e4ce2 (it was 8da0171e when the first draft read it), cut from 47bcf77e. It merges first, and CB-33's worktree is cut after it. It adds no statement of this shape: its new SQL is migration 042 and test code. Where it meets this design:

- `D`: it adds a migration to the list (`:243`), edits the neighbourhood of `open_in_memory` (`:329-333`), `effective_name_key` and the tests. This chunk's edits are `D:282-296` and `D:323-328`, a few lines from its second hunk. Re-read after the merge.
- `S`: it rewrites the `stem_of` closure in `collect_rewrite_candidates` (`:19101-19112`; the two commits since 8da0171e changed that hunk again, with no SQL in it), a doc comment at `:13119`, six `to_lowercase` calls in the batch-move code and one line of the tests list. Nothing within a thousand lines of `title_for` (`S:22277-22296`), which it shifts by about five lines. Both chunks add one `mod` line to the tests list at the end of `S` (CB-32's `mod final_sigma;` after `:23483`; this chunk's `mod statement_class;`), on different lines.
- `E`: it does not edit `bases/engine.rs`.
- `L`: it changes the projection of `backlinks_for` (`L:868-869`) and `unresolved_links` (`L:938-939`) and reads two more columns in each. This chunk no longer edits `L`, so nothing meets; the appendix's statement table would, and says where.

Every `path:line` in this spec is re-read after CB-32 merges, before any code.

**CB-45b** (after CB-33). A reader connection opened through `open_connection` is configured with no further work, and the census holds any other way of opening one. Fact 11 must gain the reader, and CB-45's design (C11) should be told two things: a new connection kind is opened through `D:282` and added to fact 11; and each connection has its own statement cache, now up to 32 statements and, measured, up to about 0.5 MB when full.

**Same wave.** CB-17b and CB-22 edit no file here.

## Swift

Nothing crosses the FFI. By reading: `crates/slate-uniffi/src/lib.rs` holds no SQL and calls the session's public methods, whose signatures, return types, ordering and errors do not change. No binding is regenerated. The swift lane compiles; no Swift file is edited. The Mac calls `getFileSummary` in a loop over journal paths (`AppState+Rescan.swift:1188`) and `noteLoadBundle` for the Connections leaf; both get the same answers sooner.

## #1954 and #1955

**#1954 (Mac work owed): no line.** No sentence, announcement, label, ordering or failure changes on either host. No statement's columns, rows or ORDER BY change: the setting changes planning only, `INDEXED BY` cannot change a result, and no product statement's plan moved in the red team's comparison. There is nothing to hear and no VoiceOver pass to owe.

**#1955 (deferred finds): two lines, ready to post when the PR opens** (each is one comment line on the issue, in its form: the file, what is wrong, the failing test that would show it):

> `crates/slate-core/src/session.rs` (`index_file` and the functions it calls): fourteen statements are compiled once per changed file because their sites use `execute` or `prepare` and not the statement cache (eleven `DELETE FROM … WHERE file_id = ?1`, `INSERT INTO file_citations`, `UPDATE files SET index_epoch`, `UPDATE index_epoch_clock`), 14.3 compiles a changed file at any cache capacity on a rescan of 300 changed notes (CB-33 step 0). Caching them takes the scan's cached set from 17 to 31, so the capacity CB-33 set to 32 would want 64 with it (845 KB of statements when full, measured). Failing test: with CB-33's `PrepareCounter`, a rescan of 40 changed rich notes builds fewer than 3 statement objects a changed file.

> `crates/slate-core/src/links_db.rs` (`unresolved_links`): each page reads and sorts every unresolved link (`SEARCH links USING INDEX idx_links_target (target_path=?)`, then a temp B-tree for the ORDER BY), 1,144 µs a page of 20 at 1,000 unresolved links (CB-33 step 0, debug build). The keyset cursor does not bound the work; a fix needs an index that serves the order, so a migration. Failing test: `SQLITE_STMTSTATUS_VM_STEP` for one page of 20 stays under a bound that does not grow when the vault's unresolved links go from 100 to 1,000.

## Found on the way

1. **Two temporal probes walk the wrong index outside the pushed-down position**: `oplog.has_change_since` (`E:3809-3810`, found in the first pass) and `oplog.untouched_for` (`E:3927-3932`, found by the red team, confirmed here). About 100,000 VM steps a row on a log of 20,000 events; 20.2 s for a 10,001-row `has_change_since` view. Small (statements this chunk already edits, in a file it already edits). Fixed here, red first by fact 2; one line under "Found and fixed here" names both.
2. **The class is wider than `files.path`**: 22 more literals, four of them run by every bases row, and shapes no short rule covers. Fixed with the chunk by the setting.
3. **The comment at `S:12543-12545`** says caching the headings statement "avoids the prepare-step overhead". On 3.54 it does not. It is true again under the setting; no edit.
4. **`unresolved_links` pages cost the whole list**: deferred, a #1955 line (its fix needs an index, so a migration: not small).
5. **Corrections owed to #1917's text**, posted as a comment when the PR opens, since the issue says "A statement left as it is says why in the issue": the re-prepare happens on every use from the cache and not only on a new value; the cause is a 3.54 change in which `sqlite3_clear_bindings` ignores the mask that binding honours; three of the four temporal statements were never cached; caching one without the setting is slower; no short rule describes the class; a bound LIMIT has the same effect; the statements left alone and why.
6. **A bound LIMIT compiles twice when it is not cached**, the bases candidate query among them (its LIMIT and OFFSET are bound as text). Seventeen product lines bind a LIMIT. Gone under the setting.
7. **The statement cache is one slot short for a mixed rescan** (the red team's find, counted exactly here): 17 cached statements in a cache of 16. Small (one line in the function this chunk adds). Fixed here (ruling 2), red first by fact 14.
8. **The scan's slow path never caches fourteen of its per-file statements**: 14.3 compiles a changed file. None has an equality on a path; the fix touches the scan's write path and wants a larger cache with it. Deferred, a #1955 line.
9. **The cause looks like an inconsistency in SQLite 3.54** (the red team's reading). Call 4.

Nothing filed in step 0. The count for the PR: nine found; 1, 2 and 7 fixed here; 3 and 6 need no edit once 2 is fixed; 4 and 8 deferred to #1955; 5 is the PR body's correction and the comment on #1917; 9 is the owner's, drafted in the PR body.

## Calls for the lead

In order of weight.

1. **The design: the connection setting, or statement by statement.** Recommendation: the setting, as this spec now has it (the lead's ruling after the red team; the red team recommends it too). The numbers: the rewrite the issue asks for moves a bases view from 192 to 188 µs a row, because four other statements re-prepare beside it; the widest statement-by-statement trial and the setting both reach about 103 µs and no compile a row; the class is 38 literals, nineteen templates and shapes no rule covers. The red team's: no product literal changes plan under the setting across 183 literals and 11 value patterns each, while its planted `GLOB` does; 32 flows on a 5,000-note vault differ by 2 to 4 VM steps a run at most; 526 plan-pinning tests pass with it on; a bases view on `status == "open"` goes from 420 to 239 ms and back to 416 when it is switched off again. Cost: one setting that governs how every later statement is planned, so the lead should record it as a plan call; a bound `GLOB` or case-sensitive `LIKE` prefix written later would scan (26 against 640 µs a call on 10,001 files) and nothing would warn; per-value statistics, if ever gathered, would not help bound parameters; `db.rs` gains a chunk in its row. The red team's view is that the owner need not rule, since nothing anyone sees or hears changes. Cost of the alternative: 17 to 24 statement edits in four to six product files, an idiom no rule describes, a census with fifteen standing exceptions and a reader, and plans that move where it restructures.
2. **The statement cache's capacity: 32.** Recommendation: raise it from rusqlite's 16 to 32, in `configure_connection`, with fact 14. Why: one path goes over 16, a rescan that mixes changed rich notes with unchanged ones (17 cached statements), and at each boundary it drops and rebuilds fourteen statements, four of them with an equality on `files.path`, two of them #1918's own. That is the plan row's words by a route #1918's fact cannot see. 32 removes it (16.6 to 14.3 compiles a changed file) and leaves room: the next largest sets measured are 15 and 11. Cost: up to 327 KB more statement memory on a connection whose cache fills (205 KB at 16, 532 KB at 32, measured after every flow driven); no guard reads it and all passed at 32. What it does not buy: time. The rescan's time did not move at any capacity; by the counts it is about 7 ms in 900. The alternatives: leave it at 16, in which case the Goal's third sentence and fact 14 go and the #1955 line above carries the numbers; or 64, which costs 845 KB when full and buys nothing measured today, but is what the slow path would want if its fourteen uncached statements are ever cached (find 8).
3. **Name the index in the five temporal probes, and fix both defects here.** Recommendation: yes. It is small by AGENTS.md (statements and a file the chunk already edits), red first by fact 2. Cost: a statement that does not prepare if the index is ever renamed, caught by fact 2. The alternative, a unary plus on `event_class` to steer the planner, hides the intent.
4. **An upstream report.** The cause is a 3.54 change: `sqlite3_clear_bindings` expires a statement that binding the same value would not, because it ignores the small-integer mask (the red team's reading; `sqlite3.c:93420-93421` against `:94997-95004`). Recommendation: the PR carries a drafted report (a ten-line reproduction on two bare tables, the three line references, no mention of this project beyond what the owner chooses), and the owner decides whether to post it on the SQLite forum, as CB-30 did with MathCAT. No agent posts it. Checked without sending anything: sqlite.org's change log names 3.54.1 (2026-10-16) as the newest release and lists nothing about re-preparing, cleared bindings or the stability guarantee in 3.54.0 or 3.54.1; trunk was not read. Cost: none to the chunk. If SQLite changes it, fact 10 reads 0 on the raw connection at that bump and says the setting is no longer needed for this; the setting can stay.
5. **Where the helpers live.** Recommendation: `session/tests/common.rs`, as the reading says, with `cached_statement` matching whole SQL text. Cost: `scan.rs` loses 24 lines.
6. **A Windows twin fact.** Recommendation: none. No host can observe the change, the Windows suite does not exercise the temporal operators today, and the lane runs in full on any `crates/` change, so the existing Windows facts already run over the configured connection. Cost of adding one anyway: a `BasesRedTeamRegressionTests` fact that a view with `oplog.has_change_since(...) || …` lists a just-saved note, about 30 lines, green before and after.

## Verification

In the chunk's own worktree, PowerShell, `$env:CARGO_TARGET_DIR = 'C:/dev/slate/target-<slug>'`. The order, the red lines and the mutations are in "Build order"; the gates are:

- `cargo fmt --all -- --check`; `cargo clippy --workspace --all-targets -- -D warnings`.
- The touched facts: `cargo test -p slate-core --lib -- connection_census statement_class two_sessions oplog_temporal a_bases_view a_dql_query a_canvas_pass a_notes_link a_graph_replay a_file_summary a_rescan a_cached_join no_like`.
- Then the whole core suite once. With the setting on every connection core opens, that run is the check that no pinned plan moved outside the 526 the red team ran.
- The licence headers: the two new files are `git add`ed, then `python3 scripts/apply-license-header.py`.
- The three perf guards by name in release (`rust.yml:173-175`), since step 0 ran them in debug.
- A release-build before and after for `BENCHMARKS.md`: the four paths of step 0, the 10,001-row `has_change_since` and `untouched_for` views on a log of 20,000 events, and the statement memory at 16 and 32.
- Windows, since `crates/` changed: `./apps/slate-windows/generate-bindings.ps1 -Locked`, then `dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release`, and the lane on the PR. No binding changes; the Release DLL does.
- No lane proves the anchored branch by running fact 11: only macOS takes it, and no CI lane runs core's tests there. The type the open returns and the one-exit census hold it. (Corrected after the diff red team: only macOS takes the anchored branch; `anchored::VFS` is `None` on Linux and on Windows, and no CI lane runs core's tests on macOS. See "What delivery changed".)

## Acceptance

- Facts 1 to 9 green: on each hot path the named statements are in the cache, run from it, and not re-prepared. Fact 10 green: the raw connection re-prepares and the session's does not. Fact 15 green: nothing any driven path runs is re-prepared per binding, 32 of the 38 reached and six named.
- Facts 11 to 13 green: every connection core opens is configured, and product code opens one only through `db.rs`. Fact 14 green at a capacity of 32.
- Facts 16 to 18 green: no `GLOB` with a bound pattern, no way for a `LIKE` to take an index prefix, and each planted case red when planted.
- Fact 2 green for all five probes; both wrong-index views measured before and after.
- `a_rescan_prepares_its_per_file_statements_once`, the temporal suites, the plan-pinning tests and the two-sessions guards green.
- The PR body has what "The PR body" lists; #1917 closes on the merge; the two #1955 lines are posted.

## The PR body

Title: `perf(core): no statement taken from the cache is compiled again, and two temporal probes read only their own note's events`. Beyond what AGENTS.md asks of every PR body:

- `Closes #1917`, and no close, fix or resolve word before any other number.
- **The issue's cause, corrected** with step 0's numbers: a cached statement is re-prepared on every use from the cache, the same value or a new one (9,999 of 10,000 with one path bound every time; 0 when the statement is held), because rusqlite clears bindings when a statement goes back and SQLite 3.54 then expires any statement whose plan looked at a bound value; the class is 38 product literals and nineteen templates, of which sixteen name a path; three of the four temporal statements the issue lists were never cached, and caching one without the setting is slower (27.3 to 34.9 µs); the rewrite the issue proposes moves a bases view from 192 to 188 µs a row, and the setting moves it to about 103. Then the statements left as they are and why, which is the list the issue asks for.
- **Found and fixed here**, one line each: the two temporal probes on the wrong index, with before and after (on a log of 20,000 events, `oplog.has_change_since` outside the pushed-down position took 20.2 s for a 10,001-row view on main, 2,023 µs a row; step 0's trials gave 196 µs a row with the index named alone and 101 with the setting too, about 2.0 s and 1.0 s; the build measures both views again in release and quotes those); the statement cache one slot short for a mixed rescan (16.6 to 14.3 compiles a changed file).
- **The memory cost of capacity 32** (ruling 2): statement memory on a connection whose cache has filled went from 205 KB to 532 KB in step 0's widest run; the build quotes its own reading.
- **For the owner**: the drafted upstream report, below. No agent posts it.
- `not delivered: a Windows twin fact`: no host can observe the change; the Windows suite does not exercise the temporal operators, and its existing facts already run over the configured connection because the lane runs in full on a `crates/` change. `not delivered: a release-build run of the perf guards on macOS` if the Mac lane is the only place it runs.
- The two #1955 lines as posted, and the last line with the count: nine found, three fixed here, two deferred, none filed.

The draft for the owner, with nothing in it about this project:

> **3.54: `sqlite3_clear_bindings()` expires a statement that rebinding the same parameter does not**
>
> Since 3.54.0, `whereLoopOutputAdjust()` passes the parse context to `sqlite3ExprIsInteger()` with `bRSI=1`, so an `=` or `IS` term whose right side is a bound parameter, in a query with a join or an ORDER BY, marks that parameter in `Vdbe.expmask` and in the new `Vdbe.smimask`. The binding routines honour `smimask`: `vdbeUnbind()` and `sqlite3_bind_int64()` expire the statement only when the value moves to or from 0 or 1. `sqlite3_clear_bindings()` tests `expmask` alone (`if( p->expmask ){ p->expired = 1; }`), so it expires the statement every time. A wrapper that clears bindings between uses, as statement caches commonly do, therefore re-prepares such a statement on every use, whatever is bound.
>
> To reproduce on 3.54.1: `CREATE TABLE f(id INTEGER PRIMARY KEY, path TEXT NOT NULL UNIQUE); CREATE TABLE t(file_id INTEGER NOT NULL, x TEXT); CREATE INDEX t_file ON t(file_id);` Prepare `SELECT t.x FROM t JOIN f ON f.id = t.file_id WHERE f.path = ?1` once. Loop 200 times: bind a text value to `?1`, step to completion, reset, `sqlite3_clear_bindings()`. `sqlite3_stmt_status(stmt, SQLITE_STMTSTATUS_REPREPARE, 0)` reads 199. Without the `sqlite3_clear_bindings()` call it reads 0. With `SQLITE_DBCONFIG_ENABLE_QPSG` on, or with `f.path = +?1`, it reads 0.
>
> Is `sqlite3_clear_bindings()` meant to ignore `smimask`? If the small-integer rule is the intended behaviour for binding, it seems it should apply here too.

Two notes for the owner beside it: the 199 was measured through rusqlite's statement cache, which makes that call, on two bare tables; and that 3.53.2 reads 0 either way is the issue's measurement and the red team's reading of its source, not a run in step 0.

## Build order

For whoever builds it, after CB-32 merges and every `path:line` here is re-read. Four commits; the facts go in first and are seen red on main's code.

**Commit 1: the facts and their helpers, red on main.** `C`'s three helpers, facts 1 to 11, 14 and 15, and the censuses and their planted cases (12, 13, 16, 17, 18). Expected on main's product code:

| Fact | Expected on main |
|---|---|
| 1 | red: five probes "not in the statement cache"; the regex probe "re-prepared 39 (or 40) times" |
| 2 | red for `has_change_since` and `untouched_for`: the plan names `oplog_events_class_ts`, and the VM-step bound is passed by thousands. The other three probes are green on main; the fact needs the five constants, so on main it reads the SQL from the product functions' current text |
| 3 | green (it holds results, not counts) |
| 4 | red: the tag statement re-prepared 39 or 40 times |
| 5 | red: the four row statements, 40 re-prepares each |
| 6 | red: the title statement is not in the cache |
| 7 | red: five link statements re-prepared 39 or 40 times |
| 8 | red: two statements re-prepared 39 times |
| 9 | red: the summary statement re-prepared 39 times |
| 10 | red: the session's connection reads 1 where the fact wants 0 |
| 11 | red: the setting is off on every connection, and each keeps 16 of 40 |
| 12 | red only in that the two `Connection::open` calls sit in `open_connection`, which the census does not allow yet |
| 13, 16, 17, 18 | green (planted cases and guards that hold on main); 18's last part needs `configure_connection` and is written against a connection the fact configures itself until commit 2 |
| 14 | red: at most 16 of the seventeen statements are on the connection |
| 15 | red: 28 members re-prepared per run, four built per run |

A fact that needs a function commit 2 or 3 adds (`configure_connection`, the probe constants) is committed with a stand-in the commit that adds the function removes; the PR body quotes each red line from this commit.

**Commit 2: `configure_connection`, with the setting and the capacity.** `open_connection` gets its one exit; `open_in_memory` calls the helper. Turns green: 4, 5, 7, 8, 9, 10, 11, 12, 14, and the 28 re-preparing members of 15. Still red: 1 and 6 (not cached yet), 2, and four members of 15.

**Commit 3: the two statements cached and the five probes' index names.** `exists_query`, `title_for`, the five constants. Turns green: 1, 2, 6 and the rest of 15.

**Commit 4: what is left.** `BENCHMARKS.md`; the helper's removal from `scan.rs` if it did not move in commit 1; the licence headers.

**Gates, after commit 3 and again at the end:** `cargo fmt --all -- --check`; `cargo clippy --workspace --all-targets -- -D warnings`; the touched tests; the whole core suite once; `python3 scripts/apply-license-header.py`; `./apps/slate-windows/generate-bindings.ps1 -Locked` and `dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release`.

**Mutations, each applied, seen red, and reverted, after commit 3:**

| Mutation | Facts that must go red |
|---|---|
| The setting removed from `configure_connection` | 1 (the join probes), 4, 5, 7, 8, 9, 10, 11, 15 |
| The capacity line removed (16 again) | 11, 14 |
| `configure_connection` called inside one branch of `open_unconfigured` only | 11, on the lane that takes the other branch (Windows for the anchored branch's side, the rust and Mac lanes for the default's); say in the PR body which lane showed it |
| `exists_query` back to `query_row` | 1, 15 |
| `title_for` back to `query_row` | 6, 15 |
| `INDEXED BY` dropped from `has_change_since`; then from `untouched_for` | 2, each time |
| One probe's file filter removed | 3 |
| A `rusqlite::Connection::open` added to a product function | 12 |
| A `GLOB ?1` added to a product statement | 16 |
| The cache shrunk to two statements inside fact 1; to four inside fact 15 | 1; 15 (its second assertion) |

## Verified by running, by reading only, and not yet built

**By running, first pass** (probe facts on 7a34cb6e; logs with the working notes):

- The versions, the cache capacity of 16, no `sqlite_stat` table in a scanned cache, the setting off on the session's connection.
- For every statement in the first 53 of the inventory except 46, and with 47 run as a whole query and 19 as lists of 1, 3 and 500: re-prepares per 10,000 calls from the cache (1,000 for a write), re-prepares when held, presence in the cache, compiles per 1,000 calls cached and fresh, microseconds both ways.
- The first pass's rows of the shape table, the two-table schema, the same value bound every call.
- The literal sweep on main (276 distinct, 38 re-prepared) and with T1+T2 applied (17).
- Each nested rewrite and each plus form of the appendix: re-prepares, microseconds, `EXPLAIN QUERY PLAN` before and after, and the same rows on 140 of 140 paths, four of them absent from the index. The open-base probe's and `graph_inbound_rows`'s new forms were run this way only, as bare SQL.
- The temporal probes on a log of 20,862 events: VM steps and plans for the join and the nested form, each with and without the index named.
- The setting: one statement on and off; the bound `GLOB` on and off; six paths through the callers with it on.
- Every hot path through the public API on main, and again with trial patches T1, T1+T2 and T3 (each applied, run, saved as a diff and reverted).

**By running, second pass** (after the red team):

- `untouched_for` and `has_change_since` on a log of 20,000 events, each form, and both views through the public API on main.
- The slow-path rescan at capacities 16, 32, 64, 128 and 1,024: statement objects built and re-prepares per SQL text, which statements are rebuilt at 16, and the time; the five content mixes at 16; a 40-note vault in the capacity fact's shape.
- Statement memory at 16, 32, 64 and 128.
- Eleven other paths at 16 against 1,024, on main and with the setting on.
- Trial T4 (the setting and a capacity of 32 where core opens a connection, in the single-exit shape): the fourteen `two_sessions` tests, two more perf guards and #1918's fact, debug build.

**Run by the red team, not repeated here** (its logs are with the working notes): the value-bound plan comparison over 183 literals and 5 controls; the 32 flows on the 5,000-note vault with the setting off, on and off; the 526 core tests with the setting on; the twenty statement shapes, cached and held. Its `untouched_for` figures and its capacity find were repeated here and hold, with one correction: the rebuilds at 16 happen once per boundary between changed and unchanged files, not once per file.

**By reading only:**

- Why: `cache.rs:159`, `sqlite3.c:93419-93421`, `:115828-115867`, `:172751-172802`, `:152150`, and the red team's reading of 3.53.2's source for what changed. The measurements agree with the reading at every point tried, but the reading is what says these lines are the cause. That 3.53.2 does not do this is the issue's measurement and the red team's reading, not a run here.
- The three statements the red team added to the inventory, and that the bases candidate query compiles twice per execution.
- Which connection runs what: that `worker_conn` runs none of the inventory; that every product connection goes through `open_connection` (a search for `Connection::open` under `crates/`, and the red team's list of callers).
- That `open_connection`'s early return is taken on macOS alone (first written here as "macOS and Linux"; corrected after the diff red team) (the red team's reading of `anchored_windows.rs:65` and `S:2722`).
- Who calls each statement and how often per user action, apart from the paths driven: the moves' callers of `graph_linkset_for`, the save and apply callers of `title_for`, the hosts' call sites.
- That nothing crosses the FFI, and the Swift and C# call sites named.
- What the setting switches off, beyond what was measured; that STAT4 is compiled in (the red team's reading of the build script).
- The guards in `two_sessions.rs` and `rust.yml`: that none reads statement memory.
- Everything about CB-32: its diff was read at 8da0171e and again at ed7e4ce2, not built.
- sqlite.org's change log, read through a fetch that sent nothing about this project.

**By reading only, third pass** (the lead's rulings, spec edits only, nothing run):

- The connection kinds of design section 6: a search for every `open_connection`, `open_database`, `open_in_memory`, `OpenFlags` and `Connection::open` under `crates/` at 7a34cb6e, and the lines around `S:2722-2732`.
- That no index or column in the migrations is NOCASE, that nothing sets `case_sensitive_like`, and that `GLOB` appears only in comments: searches under `crates/`.
- SQLite's conditions for the LIKE optimisation: from its documentation as remembered and from `isLikeOrGlob` in the bundled source, not from a run of a NOCASE index here. Fact 18's last part is what will run it.
- Which public call reaches each of the 32 members in fact 15's table: from the call sites, not from a run. Step 0 drove about half of these flows; `resolve_embed` of a heading, the tag-scoped searches, `canvas_outline`, `canvas_table_rows`, `tasks_for_file` and a DQL inline field were not driven.

**Not yet built or run:**

- Facts 15 to 18, `PrepareCounter` as a helper (step 0's second pass ran the same hook as probe code), the `GLOB` census's regex over the tree, the index walk, and the expected red lines in "Build order": all are predictions.
- Whether twelve runs of each flow in fact 15 fit a 40-note vault for every member (the unresolved-links pages and the tag tree need enough rows to page).

- No fact in this spec exists. "Red on main" lines are predictions from the measured counters. Fact 14's vault in particular has not been run in its final shape: the 40-note vault tried ran sixteen cached statements, not seventeen, and showed nothing, which is why the fact asserts the seventeen first.
- `configure_connection`, `open_unconfigured` and the connection census as written here; T4 ran the same shape under other names. The census's regex has not been run over the tree in Rust; the three sites it must allow come from a search.
- The five probe constants with `INDEXED BY` inside the product (the forms were run as bare SQL; T1 and T3 ran four of them through the callers in the nested and the join form, not `untouched_for`'s).
- The whole core suite with the setting on (the red team ran 526 of its tests), the Windows lane, the swift lane, macOS, a release build, the perf guards in release.
- The anchored branch with the setting: every run here was on Windows, which does not take it.
- The drafted upstream report.
- Timings are debug-build and indicative. Two flows that write files (the slow rescan and `save_text`) varied by more than two times between runs for reasons outside the change; no conclusion rests on them.

## Red team record

2026-11-21. A fresh Opus agent at xhigh effort, by `docs/plans/24_red_team_protocol.md`, with the first draft of this spec, the probe worktree and its own uncommitted test module (removed afterwards; its module and one log per run are with the working notes). No blocker, four majors, eight minors, three notes. It recommends the setting, with four changes; all four are made.

| # | Severity | Finding | What changed here |
|---:|---|---|---|
| 1 | Major | The evidence that the setting changes no plan (235 of 235 first-prepare plans) cannot fail: a first prepare has no value in hand. The claim itself holds, by its own value-bound and through-the-caller runs | The 235 is withdrawn wherever it stood and the reason given; the red team's three comparisons replace it (step 0, the design, Call 1) |
| 2 | Major | `oplog.untouched_for` walks the wrong index too | Confirmed by a run here. The index is named in five probes, fact 2 covers the five, find 1 names both |
| 3 | Major | The scan's slow path exceeds the cache's 16 slots, so the first draft's Call 7 premise and its Goal did not hold | Measured in a second pass: seventeen statements, fourteen rebuilt per boundary (not per file, a correction to the report), the memory at four capacities, the guards run. Its own call (2), fact 14, a narrowed Goal, two #1955 lines |
| 4 | Major | Under the setting the literal census cannot fail | The census of literals, its reader, its planted fact and its lists are gone from the design. Kept: the instrument's planted case (fact 10) and the connections fact (11). Added: a source census that product code opens a connection only through `db.rs`, with its planted case (12, 13) |
| 5 | Minor | The cause is a 3.54 change SQLite may undo, and the spec had no call for it | The cause section says so; find 5's comment says so; Call 4; fact 10 turns on the bump |
| 6 | Minor | The class as stated misses shapes, and "held is never re-prepared" is not general | The shape table gains the red team's rows and the sentence under it; the held bullet is narrowed; it is a reason against the alternative |
| 7 | Minor | The inventory misses three statements | Added (56 statements, 101 sites), with the bases candidate query's text-bound LIMIT and OFFSET and what the setting does for it |
| 8 | Minor | Fact 5 said "compiles nothing per row" and counts only re-prepares | Renamed `a_bases_view_re_prepares_nothing_per_row`; its doc comment says what it cannot see |
| 9 | Minor | `cached_statement(conn, needle)` can find the wrong statement | It matches whole SQL text against the product's `const` and requires exactly one match |
| 10 | Minor | The literal census's reader skips product code after an out-of-line test module | Under the alternative only; recorded there |
| 11 | Minor | The setting must cover both of `open_connection`'s returns | One exit and a helper with no `cfg`; fact 11 says which lane proves which branch |
| 12 | Minor | "The plan a test pins is the plan the app runs" is too wide | Narrowed to tests on connections core opens |
| 13 | Note | The cost list lacked STAT4 and partial indexes, and two tests do run `ANALYZE` | Both in the design's cost paragraph and Call 1; "never runs ANALYZE" corrected to product code |
| 14 | Note | The alternative's nested `title_for` may pick another row | Recorded under the alternative; the design does not change that statement's SQL |
| 15 | Note | Fact 6 assumed the title statement survives the rescan in the cache | Fact 6 reads RUN after each phase and says its rescan stays on the fast path |

What it confirmed: the cause, by reading and by running; the shape table's rows it re-ran; the `has_change_since` defect and its fix; that the setting is reachable in rusqlite 0.41.1 with no feature gate, that only `sqlite3_db_config` touches it and that no product code switches it; that every product connection goes through `open_connection`; that `INDEXED BY oplog_events_file_ts` cannot be missing on a schema a session runs; that caching `exists_query` and `title_for` keeps their error handling and re-enters nothing; the alternative's unary plus and nested lookups as equivalent for absent paths, NULL and duplicates (but for `title_for`); the overlap with CB-32; the two host call sites; and the first draft's calls on the helpers and the Windows twin.

What it could not check, still open: the whole core suite under the setting, release timings, the Windows and swift lanes, macOS, SQLite's trunk, CB-32's uncommitted work and CB-45b's design.

## Appendix: the alternative's statement table

Kept so the lead can still rule for it (Call 1). It is the first draft's "Design A", unchanged but for this heading. Under it, "Common to both" meant the caching of `exists_query` and `title_for`, the index name in the temporal probes and the helpers; "(Call 2)" in its table is the first draft's question of how far, now under "Alternative, not taken"; and "the three groups above" are the inventory's three groups that are never re-prepared. Its line numbers in `L` meet CB-32's two projection changes (`L:868-869`, `L:938-939`).

A nested id lookup where the statement needs only the file's id (the #1918 remedy). The unary plus where the statement must keep its join because it returns or orders by `files.path`, on every bound LIMIT, and on an equality that names no path.

| Site | Today | Becomes |
|---|---|---|
| `T:123-127` tags | `FROM dql_file_tags t JOIN files f ON f.id = t.file_id WHERE f.path = ?1 ORDER BY t.ordinal ASC` | `SELECT tag_raw FROM dql_file_tags WHERE file_id = (SELECT id FROM files WHERE path = ?1) ORDER BY ordinal ASC` |
| `E:3809-3810` `has_change_since` | the join | `SELECT 1 FROM oplog_events INDEXED BY oplog_events_file_ts WHERE file_id = (SELECT id FROM files WHERE path = ?1) AND event_class = 1 AND ts_ms >= ?2 LIMIT 1` |
| `E:3823-3826` `has_property_change` | the join | `SELECT 1 FROM oplog_events INDEXED BY oplog_events_file_ts WHERE file_id = (SELECT id FROM files WHERE path = ?1) AND event_class IN (2, 3, 5) AND (property_key = ?2 OR event_class = 5) AND ts_ms >= ?3 LIMIT 1` |
| `E:3839-3841` `deleted_content_matches` | the join | `SELECT 1 FROM oplog_events INDEXED BY oplog_events_file_ts WHERE file_id = (SELECT id FROM files WHERE path = ?1) AND event_class = 1 AND ts_ms >= ?2 AND deleted_text LIKE '%' \|\| ?3 \|\| '%' ESCAPE '\' LIMIT 1` |
| `E:3877-3879` the regex probe | the join | `SELECT deleted_text FROM oplog_events INDEXED BY oplog_events_file_ts WHERE file_id = (SELECT id FROM files WHERE path = ?1) AND event_class = 1 AND ts_ms >= ?2 AND deleted_text IS NOT NULL` |
| `S:22280-22283` title | the join | `SELECT value_text FROM properties WHERE file_id = (SELECT id FROM files WHERE path = ?1) AND key = 'title' AND value_kind = 'text' LIMIT 1` |
| `L:436-440` `graph_linkset_for` | the join | the same columns `FROM links WHERE links.source_file_id = (SELECT id FROM files WHERE path = ?1) ORDER BY links.ordinal ASC` |
| `L:719-725` `outgoing_links_for` | the join | the same columns, the same `FROM links WHERE links.source_file_id = (SELECT id FROM files WHERE path = ?1) ORDER BY links.ordinal ASC` |
| `L:801-808`, `:810-817` `outgoing_links_bounded` | the join, `LIMIT ?2` | as the row above (the first with `AND links.is_embed != 0`), ending `LIMIT +?2` |
| `L:835-838`, `:840-843` `outgoing_links_count` | the join | `SELECT COUNT(*) FROM links WHERE links.source_file_id = (SELECT id FROM files WHERE path = ?1)`, the first with `AND links.is_embed != 0` |
| `L:868-878` `backlinks_for` | `links.target_path = ?1`, `files.path = ?2`, `LIMIT ?4` | `links.target_path = +?1`, `files.path = +?2`, `LIMIT +?4`; nothing else moves |
| `L:938-948` `unresolved_links` | `files.path = ?1`, `LIMIT ?3` | `files.path = +?1`, `LIMIT +?3` |
| `L:464-468` `graph_inbound_rows` | `links.target_path = ?1` | `links.target_path = +?1` |
| `S:13100-13109` `get_file_summary_impl` | `WHERE path = ?1` in the CTE (`:13104`) | `WHERE path = +?1` |
| `S:19944-19947` open-base probe | the join, `query_row` | `SELECT 1 FROM file_meta WHERE file_id = (SELECT id FROM files WHERE path = ?1)`, still `query_row` |
| the seven on the measured paths that name no path (Call 2): `E:1792`, `:1993`, `:2011`, `properties_db.rs:171`, `S:12547`, `dql_inline_fields_db.rs:184` | `file_id = ?1` or `source_file_id = ?1`, with an ORDER BY | `= +?1` |
| `E:2087` `load_backlinks` | `l.target_path = ?1` | `l.target_path = +?1` |

Left as they are, with the reason the issue asks for:

- The 35 statements that are not re-prepared in any mode (the three groups above and `tags_for_files`). Caching the per-row ones among them (`E:1714`, `:3969`, `:3984`, `:3999`, `dql_inline_fields_db.rs:165`) would save about 1.6 µs of 14.7 a call; they stay `query_row`.
- `S:19131-19135` `collect_rewrite_candidates`: prepared once and held across its loop, never taken from the cache, 0 re-prepares as it runs. It is the census's one standing exception of the sixteen.
- `E:983-988`, the linked-source clause: part of a query that is prepared once per execution and never cached.
- The fifteen literals the census finds outside the measured paths (`S:19131` among them) ship as dated exceptions, each with its reason, under plan rule 4. The nineteen SQL templates the census cannot compile alone are pinned by site.
