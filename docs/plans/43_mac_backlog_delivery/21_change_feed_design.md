# PR 21 — A core change feed for exact cross-session rescan counts: design

Status: design for owner review, 2026-10-17. Nothing here is
implemented. Issue #1293 (contract 40 AR-30, AR-31). Spec:
`specs/pr21_change_feed.md`. Baseline: `main` at 71231296; every
file:line reference below was read at that commit (PRs 13, 16 and 17
merged since beed82e1, where this design started, and moved only the mac
lines cited in §6). This design builds on PR 20's design
(`20_vault_watcher_design.md`, PR #1420), which is under review with its
questions Q1 to Q7 open; §10 names each point that depends on one of its
answers. PR 22's design (#1441) is open too; §7 names its interlock. PR
19 is not implemented, so the mac half is designed against
`specs/pr19_mac_rescan_baseline.md`. The spikes behind §3 lived in a
scratch directory and were not committed.

## 1. Summary

1. **The table.** A core-owned `change_feed` table in
   `.slate/cache.sqlite` (migration 040): `seq` (AUTOINCREMENT, never
   reused), `kind` (created, modified, deleted, renamed), `entry` (file
   or directory), `path`, `previous_path`, `hash_before`, `hash_after`,
   `writer` (actor, process id and session nonce), `origin` (reconcile or
   Slate-owned write) and `written_ms`. Six persistent triggers on
   `files` and `dirs` write the rows, the way migration 006's FTS
   triggers already do, so every index mutation by any Slate connection
   writes its row inside the same statement and transaction, with no call
   at the twenty-odd mutation sites. A row costs about 2.5 to 4 µs and
   136 to 220 bytes, and no row is written while no session holds a
   cursor.
2. **Cursors and trimming.** A host registers a cursor after its open
   scan. The cursor's heartbeat moves whenever the session registers or
   acknowledges a window; a cursor silent for 24 hours is reaped. Each
   acknowledgment trims the rows below every live cursor, and a
   100,000-row cap bounds what a lagging cursor can hold. A missing
   cursor, a cursor below the trim floor or a reset cache is a gap: the
   host runs today's full re-sync (R-9) with its cursor re-created, and
   core renders a new "not counted" sentence.
3. **Consumption.** After its scan, a run opens a window (cursor, head].
   Core reads it in one read transaction and derives per-path records
   (removal-first pages of at most 10,000, cancellable, each carrying the
   content hash and the `list_files` summary as of the window's target)
   and exact counts: what the host must change to show the index, net,
   with this session's own announced writes (including its renames)
   already applied. Rename chains are composed into one record per moved
   file, carrying the hash its old tabs hold. Hosts apply the records
   through R-9's per-path primitives, keep comparing every open tab with
   the index on every run, and notify reading and history per path only.
   A window of more than 10,000 rows that is not all the session's own
   writes falls back to the full re-sync with the uncounted sentence.
4. **Crash consistency.** The feed row and the index mutation commit
   together because the trigger runs inside the mutating statement. A
   child process that aborts before its commit leaves neither row, one
   that aborts after leaves both (measured). A reader's own crash costs
   nothing: its cursor is reaped.
5. **The watcher.** PR 20's Watcher runs consume the feed like every
   other run; there is no second path. PR 20's echo ledger (filesystem
   events, this process) and the feed's `writer` and `origin` (index rows,
   any process) stay two mechanisms. A ledger miss now costs one scan and
   an empty window instead of a full re-sync.
6. **Facts.** Each of the spec's six is named in §4.9, in core and on
   both hosts.

- **AR-31.** Every surface applies the same window, and after the last
  publication the run reads the feed head once more. If another session
  committed meanwhile, a further pass applies the rest, at most three
  passes per run.
- **Announcements.** One core-rendered sentence per run, as today. The
  counts now include other sessions' changes; the foreground and watcher
  speech policies are otherwise unchanged.
- **Cost.** A no-change run reads its cursor and the head and skips the
  re-sync that today re-reads Quick Open's whole list and re-projects
  every reading model.

Plan (§7): PR 21a (core and FFI, L), then PR 21b (Windows, M–L) and
PR 21c (mac, M–L, after PR 19 and PR 20c).

## 2. What stands on main

- **The index records what is current, not who changed it.** `files`
  (`crates/slate-core/migrations/001_init.sql`) and `dirs` (`016_dirs.sql`)
  hold one row per path. Nothing in the cache records a change.
- **R-9's re-sync** (contract 40 `:23`): after its scan, a rescan
  re-synchronizes every surface from the index and retains nothing
  (OD-10 `:58`, AR-18's fallback, `:47`). Windows: the run and its one
  sentence (`VaultLifecycleViewModel.Rescan.cs:271-348`), the three
  branches (`:363-386`): the tree's silent refresh (`:408-434`), Quick
  Open's replacement with a journal of Slate-owned changes replayed onto
  it (`:436-480`, `QuickSwitcherViewModel.cs:329-380`), and the
  workspace (`:485-575`) with the dependents once
  (`WorkspaceViewModel.Rescan.cs:620-662`, whose comment says "the
  re-sync has no per-path delta"). The mac has no rescan yet (PR 19).
- **AR-30** (`:59`): a Refresh's counts are its own scan's; another
  session's changes are shown but not counted. Witnesses: core
  `another_sessions_writes_are_in_the_shared_index_but_not_in_this_scans_counts`
  (`session/tests/rescan.rs:627`) and Windows
  `AnotherSessionsChangesAreShownButNotCounted` (`RescanTests.cs:1727`,
  which asserts "Files refreshed. No changes." and a silent foreground
  run). **AR-31** (`:60`): a commit by another session during the
  re-sync can leave the tree and Quick Open one version apart until the
  next rescan.
- **The scan's counts.** `index_file` counts "new or changed" from its
  own prior-row read (`session.rs:10488`, fast path `:10609`, upserts
  `:10678` and `:10752`); `prune_unseen_files` counts removals
  (`:10403`). The FFI `ScanReport` carries both
  (`crates/slate-uniffi/src/lib.rs:6424`). The scan is one IMMEDIATE transaction
  (`session.rs:10033`, commit `:10307`).
- **File-change events** cover Slate's own write paths only (locked 05
  §4.4, `:338-347`): `notify_file_change` (`session.rs:4582`),
  `notify_moved` (`:4600`). The save seat notifies with the connection
  lock held (`:4376-4393`); structural operations notify after releasing
  it (`delete_file_checked`: `drop(conn)` at `:15857`, then the Modified
  or Deleted notify at `:15865-15867`).
- **`PRAGMA data_version` is already used as a drift guard**, never as
  attribution: `tasks_index_revision` (`session.rs:1876-1883`) and the
  directory pages' snapshot (`session/directory_page.rs:47-67`, data
  version plus `total_changes`, failing a page across any commit).
- **Persistent triggers on `files` are an existing pattern**: migration
  006 maintains FTS5 through `AFTER INSERT`, `AFTER UPDATE OF body_text`
  and `AFTER DELETE` triggers, scoped by column so the scan's fast path
  does no FTS work (`006_fts5.sql:16-28`, `:49-81`).
- **Every writer is fenced by schema version.** Every write transaction
  goes through `begin_fenced` (`db.rs:485`), which refuses a cache at
  another schema version (`SchemaVersionSkew`, `:46-54`); a build older
  than the cache refuses to open it (`UnsupportedVersion`). After a
  migration, only builds that know its schema can write.
- **Connections.** Each session has one connection behind a mutex
  (`session.rs:1194`), opened by `open_database` with WAL, NORMAL
  synchronous and in-memory temp storage (`db.rs:230`, `:346-378`). The
  compaction worker opens a second connection (`session.rs:1484`) that
  writes `oplog_events_stale` and `oplog_events` (`:1646`, `:1532`),
  never `files` or `dirs`. Every Slate connection registers Slate's SQL
  functions (`register_connection_functions`, `db.rs:277`), which
  migration 033's expression indexes already require.
- **Identity.** Sessions carry an op-log actor, `"local"` for the app and
  `"cli"` for `slate-cli` (`from_filesystem_with_actor`,
  `session.rs:2990-3012`), and a per-session nonce
  (`directory_cursor_nonce`, `:1197`, from `next_directory_cursor_nonce`,
  `:45`). The nonce's comment still mentions "scan-delta cursors"
  (`:1196`), a remnant of the design OD-10 dropped; nothing on `main`
  uses one.
- **Bounded, cancellable reads exist** for the re-sync: `list_files`
  pages of 1 to 10,000 (`MAX_LIST_FILES_PAGE_LIMIT`, `session.rs:9945`)
  and `indexed_content_hashes` in chunks of 1,024 (`:9962`, `:3152`).
- **The issue's option lettering** ("option D from PR 7's design
  review") is recorded only in #1293's body and in OD-10's "option B";
  no document lists options A to D. The design the review dropped is
  the W7-7 spec's §8.2 before OD-10, which §3.1 mines for lessons:
  `git show 3cc314f5:docs/plans/18_windows_port/specs/w7_7_nvda_matrix_remediation_spec.md`.

## 3. Evidence

### 3.1 Why PR 7's ledger failed, and what this design keeps from it

Codex PR 7 round 1 found that the retained-generation ledger could not
be correct across sessions: its supersede triggers were TEMP, so they saw
only their own connection's writes (contract 40 `:791`, OD-10). The
attribution layer of the earlier design passes (`PRAGMA data_version`, a
"shown" signal) went with it (AR-18). Thirty review rounds on that ledger
(the pre-OD-10 §8.2 e–h) still settled several rules that hold for any
delta, and this design keeps them:

- Deltas cross the FFI in bounded, cancellable pages, never as complete
  lists (codex PR 0 rounds 5–6, 23; locked 05 §9.3.1, `:1490-1492`).
- A delta page is removal-first, so a case-only rename's removal marks the
  tab missing before the creation re-seats it (rounds 4, 8–9).
- Application is idempotent, so a failed page is retried, not repaired
  (round 7). Rows carry their own provenance (the hash before), because
  a removed row's prior hash is gone from the live index (round 15).
- Openability is core's classification, never a host extension list
  (round 26).
- Slate-owned writes that land while a delta is applied must not be
  reverted by it (rounds 23–26: the host journal and the "no core call on
  the dispatcher" rule).
- An external rename is Deleted + Created, and nothing is retargeted by
  content equality (round 3, AR-8).

What this design changes is where the delta lives: in the shared cache,
written by persistent triggers that run on every connection, instead of
in one connection's TEMP state.

### 3.2 Trigger semantics (spike)

A scratch crate on the workspace's `rusqlite =0.40.1` (bundled SQLite
3.53.2, `trusted_schema = 1`) applied the 39 real migrations from
`crates/slate-core/migrations/` to a file-backed cache with Slate's
pragmas, then the draft migration 040 of §5, and wrote through the
statement shapes core uses.

| Operation | Feed rows |
|---|---|
| Any write while `change_feed_cursors` is empty | 0 (the gate) |
| `index_file`'s upsert, same hash (conflict path) | 0: `AFTER INSERT` does not fire on a conflict, and the update trigger's `WHEN` is false |
| The same upsert with a new hash | 1 modified, both hashes |
| The upsert of a new path | 1 created |
| The fast path (`UPDATE files SET indexed_at_ms, ctime_ms, birthtime_ms`) and the scan's `dirs` upsert of 50 existing directories | 0 (no watched column in the `SET` list) |
| `UPDATE files SET path` | 1 renamed, `previous_path` set, both hashes |
| `DELETE FROM files` | 1 deleted, hash before |
| `dirs` insert, path update, delete | 1 created, renamed or deleted directory row each |
| A write while the session's origin is Write | `origin = 1`, the session's writer |
| A multi-row insert that fails on its second row, inside a transaction that then continues and commits | The failed statement's row is gone and the next row reuses its `seq`: no phantom, no gap |
| A transaction rolled back | No `seq` consumed |
| Every row deleted, then a write | `seq` continues past the old head (AUTOINCREMENT never reuses) |
| A raw connection without Slate's functions updates `content_hash` | Refused: "no such function: slate_feed_writer" |
| The same raw connection updates an unwatched column | Allowed |

### 3.3 Cost (spike)

Same crate, release, the Mac Studio (M5 Max, 18 cores, macOS 27.0.1).
The machine was shared with other agents' `xctest` runs and benches
(load average 10 to 33), so absolute times are inflated; each figure is
the median of 5 to 9 rounds with the variants interleaved, and the
relative costs are what the design uses. The statements are core's SQL
shapes with 1 KiB Markdown bodies and live FTS triggers, not a full
scan (no file system walk, no parsing).

| | No feed | Feed, no cursor (gate off) | Feed, a cursor (gate on) |
|---|---|---|---|
| Cold insert of 10,000 rows, one transaction (9 rounds) | 270 ms | 286 ms | 310 ms |
| 10,000 hash-only updates, one transaction (9 rounds) | 3.60 µs each | 4.14 µs each | 6.13 µs each |
| One save, an upsert in its own IMMEDIATE transaction (9 rounds) | 0.165 ms | 0.171 ms | 0.206 ms |
| Cold insert of 50,000 rows (5 rounds) | 1,043 ms | 1,146 ms | 1,187 ms |
| Fast path over 50,000 rows (5 rounds) | 163 ms | 190 ms | 165 ms |
| 50,000 hash-only updates (5 rounds) | 3.15 µs each | 3.74 µs each | 4.87 µs each |

An earlier 7-round run at 10,000 files gave 2.94, 3.52 and 5.52 µs per
hash-only update. So a feed row costs about 2.5 to 4 µs on this machine
under that load, and the gate's check 0.5 to 2.1 µs while no cursor
exists (the high end from the 50,000-row cold insert); the fast path
fires no trigger, and its differences are noise.

For scale, PR 20's spike measured on the same machine a 0.61 s cold scan
of 10k link-free notes, 3.83 s at 50k, and `save_text` at 18 to 34 ms
(§3.4 of its design): a feed row adds about 0.04 ms to a save (well under
1%), 1 to 3% to a cold first open (no cursor yet), and 4 to 7% to a cold
scan that another session is watching. A no-change rescan writes no row.

Storage: a created row is 136 bytes on disk (10,050 rows, measured with
`VACUUM`); rows carrying two hashes or two paths are about 200 to 220
bytes. The 100,000-row cap is therefore 14 to 22 MB in the worst case.

The window (per-path materialization of §4.3 into a TEMP table in one
read transaction, over a 50,000-file index, mixed rows from another
writer: 40% modified, 20% created, 20% deleted, 20% renamed):

| Rows in the window | Materialize and count | All pages at 10,000 | Trim the rows |
|---|---|---|---|
| 10 | 0.08 ms | 0.01 ms | 0.01 ms |
| 1,000 | 2.6 ms | 0.13 ms | 0.26 ms |
| 10,000 | 26 ms | 1.5 ms | 2.3 ms |
| 50,000 | 145 ms | 12 ms | 25 ms |

The counts came out exact in every case (for the 10-row window: 4
created, 4 modified, 4 removed, renames counting one each way).

The rejected alternative of §4.1, a per-session snapshot of `(path,
hash)` diffed against `files`: at 50,000 files, building the TEMP
snapshot took 29 ms, each diff 30 ms, and the snapshot held about 5 MB;
at 10,000 files, 4.5 ms, 3.1 ms and 1 MB.

### 3.4 Visibility, snapshots and crashes (spike)

| Step | Session A's `data_version` | Feed head |
|---|---|---|
| Another connection inserts into `oplog_events_stale` (what the compaction worker does) | 2 → 3 | 0 → 0 |
| Another connection modifies a file | 3 → 4 | 0 → 1 |
| A commits its own file change | 4 → 4 | 1 → 2 |

`data_version` moves for writes that change nothing a host shows, and
does not move for the session's own commits; the head moves exactly for
index changes, whoever made them.

- While A held a read transaction that had read the head and written a
  TEMP table, B committed a file change without waiting (0.0 ms, no
  `SQLITE_BUSY`). A still read the old head inside its transaction and
  the new one after it. Writing TEMP tables inside a read transaction
  takes no lock on the main database.
- A child process opened the cache, upserted a file inside an IMMEDIATE
  transaction and called `abort()` before `COMMIT`: afterwards neither
  the `files` row nor its feed row existed, and `integrity_check` was
  `ok`. Aborting right after `COMMIT` left both rows, with the child's
  writer id.

### 3.5 Every index mutation, and its origin

Production writes to `files` and `dirs` are all in `session.rs`, with
one exception in a test seam: `tasks_db.rs:189-197`
(`SLATE_TEST_TORN_NOTE_READ`) opens a raw connection and updates
`content_hash`, discarding the result. After migration 040 that statement
fails for want of `slate_feed_writer` (measured with a cursor present,
§3.2; the trigger's body is compiled with the statement, so very likely
without one too), silently, and the race the seam exists to create
(`session/tests/tasks.rs:2473`) would no longer happen. 21a registers
Slate's functions on that connection and makes the seam assert that its
write landed. The other raw-connection seams
(`session.rs:4063`, `:7824`, `tasks_db.rs:113`) touch no watched
column. Everything else is a test fixture. The triggers record each
production write; the origin column says whether the writing session
announced it with a file-change event.

| Site | Rows | Origin |
|---|---|---|
| `scan_vault` → `index_file` upserts (`:10126`, `:10678`, `:10752`), `upsert_dir` (`:10117`, `:10381`), `prune_unseen_files` (`:10439`), `prune_unseen_dirs` (`:10465`) | created, modified, deleted, directory rows | Reconcile |
| `ensure_open_base_indexed` → `index_file` (`:18391`) | created or modified | Reconcile (no event) |
| `open_canvas`'s first index of an unindexed board (`:20877`) | created | Reconcile (no event) |
| Open-time recovery: `rollback_structural_batch_inflight_locked` → `apply_batch_move_index_direction` (`:2866`, `:12808`) | renamed | Reconcile (no event; this session's host has not registered yet, and other open hosts apply and count it) |
| `save_text_locked` → `index_saved_file` (`:4318`, `:4756`): saves, property and task edits, canvas serialization, link rewrites, restores | created, modified | Write |
| `create_exclusive_binding` → `index_saved_file` (`:5861`) | created | Write |
| `reindex_path_locked` (`:8000`, `:8028`) | modified, deleted | Write (notifies `:8054`) |
| Structural moves and renames: `structural_move_file` (`:16549`, `:16579`) and `structural_move_folder` → `rename_prefix_in_index` (`:16474`, `:17689`, `:17764`, `:17822`) | renamed, directory rows | Write (inside `finish_structural_move`, `:16612`) |
| `create_folder` and `create_folder_exclusive` → `upsert_dir_row` (`:15271`, `:15311`, `:17854`) | directory created | Write (inside `with_structural_tx`, `:17384`; the host refreshes its own tree) |
| `delete_file_checked` (`:15830`), `delete_folder_checked` (`:15983`, `:15987`), `undo_structural` (`:16117`) | deleted | Write (inside `with_structural_tx*`, `:17372`, `:17384`) |
| `batch_move_with_faults` (`:13150`) and its `recover_batch_move_locked` (`:13674`), `undo_batch_move_checked` (`:14101`) | renamed | Write |
| `batch_trash_checked` → `reconcile_batch_trash_index_locked` (`:15106-15127`) | deleted | Write |
| `contain_unrepairable_pending_path_locked`, `quarantine_unrepairable_path_locked` (`:7164`, `:7827`, `:7927`) | none: they move `mtime_ms` or `index_epoch`, not a watched column | — |
| `refresh_text_derived_indexes_after_reclassification` (`:12212`), `reconcile_oplogs_inner` (`:3715`), `stamp_index_epoch` (`:10874`), migrations 012/014/015/018 | none | — |

## 4. Decisions

### 4.1 The `change_feed` table

**Chosen.**

- Migration 040 (§5) creates `change_feed`, `change_feed_cursors` and
  `change_feed_meta`, and six triggers: `AFTER INSERT`, `AFTER UPDATE OF
  path, content_hash` and `AFTER DELETE` on `files`; `AFTER INSERT`,
  `AFTER UPDATE OF path` and `AFTER DELETE` on `dirs`. Each update trigger
  fires only when a watched value really changed (`OLD.x IS NOT NEW.x`);
  each trigger is gated on `EXISTS (SELECT 1 FROM change_feed_cursors)`.
  There is no backfill. `CREATE … IF NOT EXISTS` keeps a replay safe (the
  migration 035 precedent).
- `seq INTEGER PRIMARY KEY AUTOINCREMENT`. SQLite allocates under its
  one-writer lock, in commit order, densely (a rolled-back statement or
  transaction leaves no gap, §3.2), and never reuses a value after
  deletion. The **head** is AUTOINCREMENT's high-water mark (the
  `sqlite_sequence` row for `change_feed`, 0 before the first row), not
  the largest surviving `seq`, so trimming every row never moves it back
  (§3.2: with every row deleted, the next `seq` still follows the old
  head). Rows with `seq ≤ h` are exactly the changes
  committed before the commit that made `h` the head, and the head is a
  cross-session version clock for `files` and `dirs`.
- `kind`: 1 created, 2 modified, 3 deleted, 4 renamed. `entry`: 0 file,
  1 directory. `path`, and for a rename `previous_path`. `hash_before`
  and `hash_after` are the `files.content_hash` values (text, as the
  table stores them); directories carry none.
- `writer` comes from a connection function, `slate_feed_writer()`:
  `"<actor>:<pid>:<nonce>"`, for example `local:4242:9f3c2a7b11d04e55`
  (the op-log actor, the process id, the session's nonce). `origin` comes
  from `slate_feed_origin()`: 0 reconcile, 1 Slate-owned write.
  `register_connection_functions` (`db.rs:277`) registers both on every
  Slate connection with the defaults `"unattributed"` and reconcile, so
  the compaction worker, migrations and test connections write valid
  rows; `VaultSession::open` re-registers them on its own connection
  with its writer id and an origin cell. Both are registered
  `SQLITE_INNOCUOUS` (they have no side effects), so the triggers keep
  working if a later SQLite build turns `trusted_schema` off. A raw
  connection without them cannot change a watched column (§3.2); test
  seams that do must register Slate's functions, as migration 033 already
  requires for inserts.
- **The origin is decided inside the transaction.** The session's origin
  cell defaults to reconcile. A scope guard, taken only while the
  connection lock is held and dropped before it is released, sets Write
  around the Slate-owned write transactions of §3.5: `save_text_locked`,
  `create_exclusive_binding`, `finish_structural_move`,
  `with_structural_tx` and `with_structural_tx_cancellable` (which carry
  folder creation, the deletes and `undo_structural`), the batch move,
  batch trash and batch undo transactions, and `reindex_path_locked`.
  Everything else (the scan, the lazy first index of a base or board,
  open-time recovery) keeps reconcile. The default is the safe side: a
  Slate-owned write site that forgets its guard is applied again
  (idempotently) and counted by its own session's next run, a visible
  miscount that the census fact in §4.9 catches; it is never hidden from
  anyone.
- `written_ms` is the statement time (`unixepoch('subsec') * 1000`), used
  for diagnostics; trimming is by cursor, not by this clock.

**Write cost per mutation.** One row per changed path: about 2.5 to 4 µs
while a cursor exists, and 0.5 to 2.1 µs for the gate check while none
does (§3.3); nothing for the fast path, unchanged rescans or
same-content saves. A folder move of N files writes N file rows and one
row per moved directory, which is the granularity hosts need to retarget
tabs.

**Rejected.**

- Explicit feed writes at each mutation site, the issue's literal shape:
  about twenty sites across the scan, saves, creates, structural, batch,
  undo, repair and lazy-index paths (§3.5), each of which, and every
  future one, must remember the call. The triggers cover all of them in
  every process by construction, inherit SQLite's statement rollback
  (the scan keeps its transaction going after a failed file,
  `session.rs:10137-10152`), and follow the FTS precedent.
- TEMP (connection-local) triggers: PR 7's failure (OD-10). They cannot
  see another connection's commits.
- `PRAGMA data_version`: a per-connection counter that moves for the
  compaction worker's writes and not for the session's own (§3.4); it
  cannot say what changed or who changed it.
- A per-session snapshot of `(path, hash)` diffed against `files` at
  each run. It needs no shared state and is always exact, and §3.3 shows
  it is cheap (30 ms per run at 50,000 files, 5 MB per session). It was
  rejected because it has no rename identity (another window's rename
  stays Deleted + Created and its tab goes missing), no version clock
  for AR-31's check (it would need `data_version`, which compaction moves,
  and a diff per check), costs O(vault) on every run including every
  no-change foreground and watcher run, and cannot answer a count over a
  range, which a trailing summary (PR 20's Q2) would need. What it would
  have saved (the migration, the cursors, the gap and its sentence) is
  the price of this choice.
- Marking rows "announced" in `notify_file_change`: structural
  operations notify after releasing the connection lock (`:15857`, then
  `:15865-15867`), so a window opened in between would count the session's own
  write. The origin must be decided in the transaction.
- A connection function with side effects that tells the session which
  rows it wrote, made transactional with commit and rollback hooks: a
  statement rolled back inside a continuing transaction undoes the
  trigger's row but not the function's side effect (§3.2's row), so the
  session would believe in rows that never committed.
- The op-log actor alone as the writer: two windows are both `"local"`.
- Hash columns as 32-byte blobs: about 64 bytes saved per row, against a
  conversion on every write and read; the table keeps the text the index
  stores.

### 4.2 Cursors, heartbeat, trimming and gaps

**Chosen.**

- `change_feed_cursors(writer PRIMARY KEY, seq, heartbeat_ms,
  resync_pending)` holds one row per consuming session. A host calls
  `register_change_feed()` right after its open scan and before its
  initial reads (§6), which sets its cursor to the current head.
  Registering after the open scan keeps that scan's own rows out of the
  first window: the open scan already speaks `VaultScanFinished` for
  them. A change another session commits while
  the host is still loading its first view is in the first window, so it
  is applied again (idempotently) and counted by the first run.
  `slate-cli` never registers, so a command-line session writes rows only
  while some host is open, and costs nothing otherwise.
- The heartbeat is written by `register_change_feed` and by every
  acknowledgment, that is whenever the session finishes a run; there is
  no timer (locked 05 §4 principle 5). A cursor whose heartbeat is older
  than **24 hours** is reaped by the next registration or acknowledgment
  of any session. A session's own window never treats its own old
  heartbeat as a gap: only a missing cursor or rows actually trimmed
  below it are. A laptop asleep for a weekend with one Slate open
  therefore gets an exact window when it wakes, because nobody trimmed.
- `acknowledge_change_feed(window, cancel)` moves the cursor forward to
  the window's target, clears `resync_pending`, writes its own heartbeat
  first and then trims, in one fenced transaction: reap stale cursors;
  delete the rows at or below the lowest live cursor (all rows when none
  is live); if more than **100,000** rows remain, delete the oldest down
  to that cap; record the highest deleted `seq` in
  `change_feed_meta.trimmed_through`. The trim can delete a whole burst,
  so it honours the token (a cancelled acknowledgment changes nothing).
  The cap applies at acknowledgments; a single burst larger than it sits
  on disk until the next one, and its window is `TooLarge` without being
  read.
- The maximum age is the liveness window. A live cursor acknowledged its
  last window within 24 hours, so every row it still needs was written
  after that acknowledgment, less than 24 hours ago; rows older than
  every live cursor are already trimmed, and the cap bounds the rest.
- **A gap** is any of: no cursor row for this session (never registered,
  reaped, or a cache rebuilt or restored under a live session), a cursor
  below `trimmed_through`, a cursor above the head (a restored cache), or
  a cursor still marked `resync_pending` when a run opens its first
  window. The window then reports `Gap` with no records and re-creates
  the cursor at the head, marked (§4.3). The host runs today's full
  re-sync and acknowledges, which clears the mark; the next run's window
  is exact again. The sentence is the new "not counted" variant (§4.7),
  with this scan's own counts, which is AR-30's honest posture.
- **Migrations clear the cursors.** Applying any later migration deletes
  every cursor and every feed row and sets `trimmed_through` to the head,
  in the same transaction (`migrate`, `db.rs:410`): the schema change
  fences every older session out of writing (`begin_fenced`), so their
  cursors could never be acknowledged again, and the sessions that
  restart register at the head, which is at or above `trimmed_through`.
- **What is acknowledged.** A run acknowledges its last window when it
  completes, including when some re-sync operation failed: those failures
  are counted in the sentence (R-9, AR-66) and owned by the host's retry
  set (§4.3), which the next run retries. A cancelled run (a close or a
  vault switch) and a run whose feed reads failed acknowledge nothing, so
  the next run reads the same window again; application is idempotent.
  An acknowledgment that fails (a cache busy past the timeout, a schema
  skew) counts one error, so the run speaks the incomplete sentence and
  the next run reads the window again: its counts are spoken once, by the
  run that acknowledges them. `Drop` deletes the session's cursor row,
  best-effort.

**Rejected.**

- A timer heartbeat in core or in the hosts: principle 5, and a stale
  heartbeat costs only a gap.
- Process liveness by process id: ids are reused, and a vault on a
  network volume is opened from other machines.
- No cap: a lagging live cursor would let the table grow without bound.
- Trimming by age alone: it would trim rows a live session still needs.
- Acknowledging only fully successful runs: a persistent failure (AR-66's
  failed base) would block every acknowledgment until the window grew
  into a gap, and Refresh would never count exactly again.
- A gap that re-syncs silently and speaks the ordinary sentence: "No
  changes" would be false whenever another session changed something.

### 4.3 Host consumption: the window, its pages and its records

**Chosen.**

`change_feed_window(after, cancel)` opens a window from `after` (the
session's cursor when `None`) to the head, in one read transaction on
the session's connection (a snapshot, §3.4). It reads the cursor,
`trimmed_through` and the head first, then counts, in one streaming pass
over the range that holds nothing in memory, the rows that are not this
session's own writes (below). A range with none is an `Exact` window
with no records and no counts, however large: everything in it already
reached the host as its own events (a folder move of 50,000 files, say).
A range of more than **10,000 rows** (`head − from`; `seq` is dense)
with at least one such row is `TooLarge`, and nothing more is read.
Otherwise core reads the range's rows in `seq` order, derives the
records and the counts below, and materializes the records with their
summaries into a TEMP table, all in the same transaction. The
materialization is connection-local by design: one window per session,
replaced by the next, never visible to another session. It holds the
connection mutex for its duration (under 30 ms for 10,000 rows, §3.3),
and it is cancellable through SQLite's progress handler, as the directory
pages are.

**Per-path derivation.** A row is this session's **own write** when its
`writer` is this session and its `origin` is Write: the host applied it
from its file-change event. Core walks the rows in `seq` order and keeps
two states per path: what the host shows (absent, or present with the
hash its tabs hold as their baseline) and what the index holds (absent,
or present with a hash and, when the content arrived by renames the host
did not follow, the path and hash it had where the host last saw it).
At `from` the two agree, because the host has applied everything up to
its cursor (or, on a later pass, up to the previous target).

- An own write changes the shown state the way the host's own event
  handling does. An own create makes the path shown. An own modify
  updates the shown hash of a path that is shown, and leaves an unshown
  path unshown, because a save adds nothing to Quick Open or the tree
  (a save under a spelling another session's case-only rename stored,
  `session.rs:3983`, is such a save). An own delete makes the path
  absent. An own rename moves the source's shown state to the
  destination, as the host's retarget does: a moved tab keeps its buffer
  and its baseline. Each own write also sets the index state, and an own
  rename carries the content's origin, if it has one, to the destination.
- Any other row (another session's write, or any session's scan or
  repair) sets only the index state. A rename row carries the content to
  its destination with its origin: the source's origin if it has one,
  otherwise the source path and its shown hash, when the host shows the
  source.
- A path gets a record when a row that is not an own write touched it,
  or when its two states differ at the end.
- The record's **effect** converges the host to the index state at the
  window's target: `Removed` when absent, `Present` otherwise. A final
  path whose content carries an origin also yields a `Renamed` record
  from the origin to it, with the origin's shown hash (`from_hash`).
  Chains are composed, so another session's rename of X to Y followed by
  the user's own rename of Y to Z is one record from X, where the host's
  tab is, to Z, where the file is.
- The **count** (files only) compares the two states at the end:
  `Created` (shown absent, index present), `Modified` (both present,
  different hashes), `Removed` (shown present, index absent), else
  `Uncounted`. Directories are never counted, as the scan never counts
  them today.

| In the window | Records | Count |
|---|---|---|
| B edits X; then A's user renames X to Y | X `Removed`; Y `Present` with B's hash | 1 modified |
| B edits X; then A's user deletes X | X `Removed` | nothing |
| A creates X; then B deletes it | X `Removed` | 1 removed |
| B renames X to Y | `Renamed` X → Y; X `Removed`; Y `Present` | 1 new, 1 removed (Q4) |
| B renames X to Y; then A's user renames Y to Z | `Renamed` X → Z; X and Y `Removed`; Z `Present` | 1 new, 1 removed |
| B renames `readme.md` to `README.md` (case-insensitive volume); A's next save binds to `README.md` | `Renamed` `readme.md` → `README.md`; `readme.md` `Removed`; `README.md` `Present` | 1 new, 1 removed |
| B creates X; A's user opens and saves it before the run | X `Present` | 1 new |
| B renames X to Y; then A's user renames another note to X | `Renamed` X → Y with the old hash, which only the old note's tab matches; X and Y `Present` | 1 new |
| B edits X, then edits it back | X `Present` | nothing |
| A's scan finds X edited outside Slate | X `Present` | 1 modified |
| A saves X | nothing | nothing |

A path that only own writes touched, and whose states agree, gets no
record and no count. The counts are always taken from the session's
cursor, whatever `after` is, so a run's later pass reports the run's
cumulative counts: `changed` = Created + Modified, `removed` = Removed.
These are R-9's two numbers, extended to every writer except the host's
own announced writes. A run whose range from the cursor holds more than
10,000 rows, at least one of them not an own write, is uncounted: its
later window can still be `Exact` and carry records, but says
`counted = false`.

**The window record** (FFI, §5): an opaque `token` (session nonce, from,
to, materialization generation), `from_seq`, `to_seq`, `status`
(`Exact`, `TooLarge` or `Gap(reason)`), `counted`, `changed`, `removed`
and `record_count`. The run speaks its last window's counts when that
window says `counted`, and the uncounted sentence otherwise (§4.7).
`TooLarge` and `Gap` carry no records and no counts; the
host runs the full re-sync and speaks the uncounted sentence with its
scan's own counts (§4.7). On a gap, the run's first window also
re-creates the session's cursor at the head in a fenced write, marked
`resync_pending`, so the triggers stay on during the re-sync (a lone
host's gap would otherwise run with the gate off, and a write during it
would leave no row) and later passes have a cursor to count from. While
the mark is set, a run's first window keeps reporting `Gap`, so a
cancelled re-sync is never taken for a finished one; the acknowledgment
clears it.

**Pages.** `change_feed_page(token, cursor, limit, cancel)` returns at
most `limit` records (1 to 10,000, refused otherwise, like `list_files`)
and an opaque `next_cursor`, by keyset on (phase, key): first `Renamed`
in `seq` order, then `Removed` by path, then `Present` by path. Pages are
therefore removal-first across page boundaries. A page of a replaced
window's token is refused ("change feed window is stale"). Each record
carries:

| Field | Meaning |
|---|---|
| `path` | vault-relative |
| `entry` | `File` or `Directory` |
| `effect` | `Renamed`, `Removed` or `Present` |
| `renamed_from`, `from_hash` | `Renamed` only: the origin path and the hash the host's tabs there hold as their baseline |
| `counted` | `Created`, `Modified`, `Removed` or `Uncounted`, from the session's cursor as the window's counts are |
| `content_hash` | `Present` files: the hash at the window's target |
| `summary` | `Present` files: the `FileSummary` row `list_files` returns, as of the target (read in the materialization's transaction) |
| `openable` | core's openable-document classification (`OPENABLE_DOCUMENT_EXTENSIONS`, `session.rs:277`) |
| `seq` | the path's last row in the window, for diagnostics |

**How a host applies a window.** Through R-9's existing per-path
primitives, silently, every core call on the rescan worker with the run's
token, never on the UI thread:

- `Renamed`: open tabs at the origin whose saved baseline equals
  `from_hash` move to the destination, as an own Renamed event moves them
  (`VaultLifecycleViewModel.FileChanges.cs:41`); a dirty one keeps its
  buffer. A tab whose baseline differs stays where it is (a note the user
  has since put at that path, say), and the tab comparison below decides
  it. This replaces AR-8's missing tab for renames another Slate session
  made (Q3); renames a scan infers are still Deleted + Created, because
  the scan never writes a renamed row.
- `Removed` and `Present`: Quick Open drops the path, or upserts an
  openable file from `summary`, in one pass over a path index per page;
  the dependents are notified with the path.
- **Every open tab, every run.** Whatever the window holds, the run then
  compares every open tab with the index, as R-9's document re-sync does
  today: index hashes in chunks of 1,024, a clean tab whose baseline
  differs reloads only onto bytes that hash vouches for (R-9's tickets),
  a dirty one is marked stale, a tab with no index row and nothing on
  disk is marked missing, and a missing tab whose file is back under
  another spelling re-seats (PR 10's rule). It costs a few dozen hash
  reads (open tabs, not the vault), and it keeps tabs right whatever the
  order of events and records.
- Then, once per window that has records: the tree's silent refresh, the
  Bases and dashboard re-runs and the graph probe (when any file record
  exists). Reading models are notified with the window's paths and
  re-project only when their reverse-dependency filter matches one (or
  when their own tab reloaded); the history panel reloads only when a
  record names its note. This is the spec's "reading and history notified
  per path only".
- A window with no records touches nothing but the tab comparison.
- **The retry set.** Every re-sync operation that fails is counted in the
  run's sentence (R-9, AR-66) and puts its surface into the host's retry
  set: Quick Open and the tree are retried by their full reload (R-9's
  branches), a dependent by its reload; tabs need no entry, since every
  run compares them. The next run, whatever its reason, retries the set
  before it applies its window. An **explicit** Refresh also runs the
  **recovery set**: Quick Open's whole list is reloaded, as R-9 does
  today, and every surface showing a failure state (a board's load error,
  a STALE or Error graph, a Failed base) is re-checked ("a STALE or Error
  graph recovers on an unchanged Refresh"). So the run can acknowledge its
  window even when an operation failed: the failure is owned by the retry
  set, not by the feed, and Refresh keeps its meaning of "try again".
- **Slate writes during the run.** The host opens its Slate-write journal
  before it opens the run's first window and keeps it until the run ends:
  Quick Open's replacement journal (`QuickSwitcherViewModel.cs:329-380`,
  opened today before its reads at `VaultLifecycleViewModel.Rescan.cs:446`),
  extended to record the paths of every Slate-owned event. After each
  window's records, and once more when the run ends, every journaled path
  is re-read from the index for Quick Open's presence and summary,
  instead of replaying its event: an own write that committed after the
  window opened is newer than the window's records and wins over them,
  and one that committed before is excluded from the records (its event
  applied it) or inside the record's state. Tabs need no journal; the
  comparison covers them. One order is left: structural operations notify
  after releasing the connection lock (`session.rs:15857-15867`), so an
  own event can, rarely, arrive after the run's last re-read; §9 names
  what that leaves and the explicit Refresh's recovery set closes it. The
  tree refresh reads live (§4.6 covers it).

**Rejected.**

- A per-path baseline and target with no rename transfer, and own writes
  that always mark a path shown: another session's edit followed by this
  session's rename would leave the moved tab on the old bytes and count
  the rename as "1 removed"; a save under a spelling another session
  stored would hide that session's rename from the count; another
  session's rename followed by the user's rename of its destination
  would leave the tab marked missing.
- Applying records to tabs instead of comparing every open tab: an own
  event that arrives after the records (structural operations notify
  after releasing the lock) could leave a tab on the wrong bytes with no
  later record to correct it.
- Moving every tab at a renamed path: a note the user has since put at
  that path would be carried along and reload the renamed file's bytes;
  the `from_hash` guard moves only the tabs that held the renamed file.
- Counting a `TooLarge` range with one aggregate: it groups the whole
  range in SQLite's in-memory temp store (`temp_store = MEMORY`,
  `db.rs:372`) under the session mutex, and a 200,000-file checkout would
  group 200,000 rows (locked 05 §4 principle 4). A `(path, seq)` index to
  stream the count does not help: renames link paths, so a per-path
  stream cannot follow an own rename, and the index costs every write.
- Pages computed from the live tables with no materialization: pages
  would mix versions, and a trim between pages could drop rows silently.
- Raw feed rows across the FFI: both hosts would reimplement the
  per-path reduction, which is core's (rule 1 of the program).
- Including own writes in the records: their states already reached the
  host as events, and ordering events against records would need a feed
  `seq` on every `FileChangeEvent`, captured at about ten notify seats,
  several of which run after the connection lock is released. The
  journal re-read (for Quick Open) and the tab comparison give the same
  safety without changing the event.
- One long-lived read snapshot for every read of the re-sync: a
  dedicated connection held open across many FFI calls from several
  surfaces, holding back checkpoints for the whole run.
- An unbounded page, or a page limit above 10,000: locked 05 §9.3.1.

### 4.4 Crash consistency

**Chosen.** The trigger's insert is part of the mutating statement, so
the feed row and the index mutation commit or roll back together: a
statement rollback removes both (§3.2), a transaction rollback removes
both, and a process that dies before `COMMIT` leaves neither while one
that dies after leaves both (§3.4). The scan is one transaction, so a
crashed scan leaves no partial feed. What each party sees across a
crash:

| Who crashed | What the others see |
|---|---|
| A writer, before its commit | Nothing: neither row exists |
| A writer, after its commit | Both rows; its writer id stays on them |
| A writer between its file write and its index commit (`save_text` writes the file first) | No feed row yet. The durable write intent (migration 035) marks the path suspect; the next scan by any session re-reads it and writes a reconcile row that every session counts |
| A reader (a host) | Its cursor stays until reaped (24 h); until then it keeps the rows written after it from being trimmed, within the cap. The restarted host is a new session with a new writer id and registers at the head |
| The machine (power loss) | WAL with NORMAL synchronous may lose the last transactions; a feed row and its index row are in the same WAL frames, and a cursor's acknowledgment is a later transaction, so a surviving acknowledgment implies the rows it covered survived |

**Rejected.** A feed written after the commit by the session (a second
transaction): a crash between the two loses the row for every other
session. Synchronous FULL for the cache: the index is regenerable (locked
05 §9.2), and NORMAL already keeps a row and its mutation together.

### 4.5 The watcher, the echo ledger and `writer_id`

**Chosen.**

- PR 20's watcher stays exactly as designed: a burst requests a full
  incremental rescan with `RescanReason::Watcher`, and the host's run
  then consumes the feed like every other run. The issue's "scan of the
  touched paths" was settled by PR 20 §4.2 (a full incremental scan per
  burst); the feed makes the host's side O(changes) whatever the scan's
  scope.
- A change another Slate session makes on disk reaches this session's
  watcher (it is not in this process's echo ledger). The Watcher run's
  scan finds the file already indexed (the other session indexed it, so
  the fast path matches) and the window carries the other session's rows:
  applied and counted without a Refresh. An index-only change by another
  session (a repair) has no file event and waits for the next run.
- **Two mechanisms, one meeting point.** The echo ledger answers "did
  this process write this file?" at the filesystem layer, lock-free on
  the coalescer thread, before any index row exists. `writer` and
  `origin` answer "which session wrote this index row, and did it
  announce it?" for every process. They meet at a ledger miss: the
  Watcher run it causes finds only own writes in its window, so no record
  and no count, and the run is silent and cheap.
- **The fallbacks stay.** Refresh and the foreground rescan remain the
  baseline when the watcher is unavailable or overflows (PR 20 §4.6), and
  the full re-sync remains the baseline when the feed cannot answer (a
  gap or an oversized window, §4.2 and §4.3). Every reason runs the same
  scan and the same consumption; only the speech policy differs.

**Rejected.**

- Replacing the ledger with the feed: the coalescer would need the
  session's connection lock (PR 20 §4.3 keeps it lock-free; a scan holds
  the lock for its whole run), the save writes the file before it
  commits the row so a burst can close between the two, and the feed
  carries no stat to compare with the disk.
- Replacing `writer` with the ledger: the ledger is in memory and per
  process.
- Skipping the scan when every touched path has another session's row:
  the feed has no stat to prove the disk matches, a burst can mix
  external and Slate writes, and the scan is cheap after PR 20.0.

### 4.6 AR-31: one target per run

**Chosen.** Every surface of a run applies the same window (the records
are a snapshot at the window's target `T`). The tree refresh and the
dependents read the live index, so after the last publication of the
window the run reads `change_feed_head()` once (one row of
`sqlite_sequence`). If the head is still `T`, nothing committed since
the window opened, and every live read happened at `T` too: the tree and
Quick Open show the same version. If it moved, the run opens a further
window with `after = T` and applies it the same way (a further window
that is a gap or too large takes the full re-sync), at most **three**
passes per run. The same check follows the full re-sync of a gap or an
oversized window, so the fallback closes AR-31 as well. A head moved
only by this session's own writes gives a further window with no
records, which ends the run at once. A run that reaches the bound ends
at its last target; the surfaces are each at a committed version at or
after it, and the next run (which PR 20's watcher requests when the
writer touched files) converges them. That residual is far narrower
than AR-31 (§9).

**Rejected.** Leaving AR-31 as accepted (the check costs one read);
`data_version` as the check (compaction moves it, §3.4, so a busy
compaction would force passes for nothing); applying the tree's changes
from records instead of refreshing it, which would re-implement on both
hosts the tree's keys and landings (R-4, R-5) that its refresh already
honours.

### 4.7 Announcements

**Chosen.** One core-rendered sentence per run, after the re-sync, never
during it; nothing per path; the keys never move (R-9).

| Run | Window | Sentence |
|---|---|---|
| Any | The scan was incomplete, or a re-sync operation or a feed read failed | `VaultRescanIncomplete(errors)`, as today |
| Explicit | The last window counted | `VaultRescanFinished(Explicit, changed, removed)` with the window's counts |
| Explicit | A gap, too large, or uncounted | new `VaultRescanFinishedUncounted(Explicit, changed, removed)` with this scan's own counts |
| Foreground or Watcher | The last window counted | `VaultRescanFinished` only when `changed + removed > 0`; PR 20's watcher spacing and modal rules apply |
| Foreground or Watcher | A gap, too large, or uncounted | the uncounted sentence only when this scan's own counts are not zero; otherwise nothing |
| The open | — | `VaultScanFinished`, unchanged; registering the cursor speaks nothing |

Proposed copy for the new variant (Medium, beside its siblings at
`a11y.rs:1999-2011`; Q1): "Files refreshed. {changed} new or changed,
{removed} removed; changes from other Slate sessions were not counted."
and, for zero, "Files refreshed. No changes found; changes from other
Slate sessions were not counted." The existing sentences are unchanged;
only their counts now include other sessions' changes, so AR-30's
witness changes (Q7).

A Watcher run in a Slate process whose app is inactive never runs (PR 20
§4.5 item 4), so a background Slate process does not speak a sibling
session's writes; the mac app runs one session per process (one
`AppState`, `SlateMacApp.swift:255`).

**Rejected.** Attributing changes in the sentence ("1 changed here, 2 in
another session"): a copy change with no user need yet; one sentence per
path, or per page: a flood.

### 4.8 Cost for a large vault

At 50,000 notes:

- **Writes.** About 2.5 to 4 µs per changed path while a host is open
  (§3.3); nothing for unchanged rescans. A save gains about 0.04 ms (well
  under 1%); a cold first open 1 to 3% (no cursor yet); a cold scan
  another host watches 4 to 7%; a `git checkout` touching 20,000 files
  writes 20,000 rows (about 50 to 80 ms of trigger work inside a scan
  that reads and parses those files).
- **Storage.** Normally near zero: each acknowledgment trims below every
  live cursor. The worst case is the 100,000-row cap, 14 to 22 MB.
- **A no-change run** (most foreground and watcher runs): the scan, one
  window read with no records, the open tabs' hashes (a few dozen
  lookups), one acknowledgment. Today's re-sync on the same run re-reads
  every openable document for Quick Open (`list_files`, 142 ms for all
  pages at 50,000 in PR 20's spike), re-projects every reading model and
  re-runs every base.
- **A run with k changes**: materialization in 2.6 ms for 1,000 rows and
  26 ms for 10,000; pages of 10,000 in about 1.5 ms each; O(k)
  application on the host, the open tabs' hashes, plus one tree refresh
  and one Bases and graph pass when files changed. A range of the
  session's own writes alone costs one streaming count, however large.
  Hosts must apply a page in one pass over their
  structures (Quick Open's current `Applied` copies the whole list per
  change, `QuickSwitcherViewModel.cs:259-292`, which a 10,000-record page
  must not do).
- **Above 10,000 rows**: today's full re-sync and the uncounted sentence;
  the range is never read, so a burst of any size costs the window
  nothing (an aggregate over it measured 145 ms at 50,000 rows, §3.3,
  and grows with the burst).

### 4.9 Facts

**Core** (rust lane; `session/tests/change_feed.rs` unless stated):

- `two_sessions_count_and_apply_each_others_create_modify_and_delete`
  (spec fact 1): session B creates, modifies and deletes; A's window says
  2 new or changed and 1 removed and carries a `Present`/`Created`
  record with its summary and hash, a `Present`/`Modified` record and a
  `Removed` record; B's own window has no record and no count. The
  existing `another_sessions_writes_are_in_the_shared_index_but_not_in_this_scans_counts`
  (`rescan.rs:627`) stays: it pins the scan report, which is unchanged.
- `a_change_one_session_scanned_is_counted_by_both`.
- `every_slate_owned_write_is_excluded_from_its_own_window`: the census
  over save, create, create bytes, rename and move of a file and a
  folder, delete of a file and a folder, batch move, batch trash, undo,
  structural undo, version restore, deleted-file recovery, canvas apply,
  property and task edits; each counted once by another session's window.
- `slate_cli_writes_are_counted_by_a_registered_host` (slate-cli tests):
  `slate write --create` and `slate write` against a vault whose host
  session registered; and an external delete the CLI's `slate open` scan
  prunes, counted as removed.
- The §4.3 table, one fact per row, among them
  `another_sessions_edit_then_an_own_rename_moves_the_change_to_the_destination`
  (Y `Present` with B's hash, 1 modified, X not counted),
  `another_sessions_edit_then_an_own_delete_counts_nothing`,
  `an_own_save_under_another_sessions_spelling_keeps_its_rename_counted`,
  `another_sessions_rename_then_an_own_rename_is_one_renamed_record`,
  `a_renamed_record_carries_the_hash_only_the_old_tabs_hold`, and
  `an_own_folder_move_past_the_row_limit_is_an_empty_exact_window`.
- `no_feed_rows_without_a_cursor` and `register_moves_the_cursor_to_the_head`.
- `a_trimmed_window_is_a_gap_and_the_acknowledgment_heals_it` (spec fact
  2), `a_reaped_cursor_is_a_gap`,
  `a_gap_recreates_the_cursor_so_a_write_during_the_resync_has_a_row`
  (a lone host), `a_cancelled_resync_stays_a_gap`,
  `a_window_over_the_row_limit_is_too_large_and_reads_nothing`.
- `the_head_survives_trimming_every_row` and
  `a_lone_hosts_next_window_after_its_acknowledgment_is_exact`;
  `a_failed_acknowledgment_is_one_error_and_the_window_is_read_again`;
  `a_cancelled_acknowledgment_changes_nothing`.
- `pages_are_bounded_removal_first_and_resume_by_keyset` (spec fact 4):
  limits 0 and 10,001 refused; five records at limit 2 come in three
  pages, each once, renames in `seq` order, then removals, then the rest;
  `a_page_honours_its_cancel_token`; `a_stale_window_token_is_refused`.
- `the_feed_row_commits_with_the_index_mutation` (spec fact 5): a scan
  cancelled at its `commit` point (`scan_point`) leaves neither rows nor
  feed rows; a save whose index commit fails through the existing fault
  seams leaves no feed row; a file that fails inside a committed scan
  leaves no row for its rolled-back statement, and the row of any of its
  statements that did land matches the committed index
  (`session.rs:10146-10149`); and the §3.4 child-process abort before
  and after commit.
- `seq_is_dense_and_never_reused` (rollbacks, trims);
  `the_head_ignores_compaction_writes`;
  `the_window_is_one_snapshot` (another session commits during the
  materialization; the next window has it).
- `a_foreign_rename_is_a_renamed_record_counting_one_new_and_one_removed`;
  `a_path_changed_and_changed_back_is_an_uncounted_present_record`.
- `migration_040_creates_the_feed_and_replays_idempotently`;
  `a_later_migration_clears_every_cursor_and_row` (against a test-only
  041); `a_connection_without_slate_functions_cannot_change_a_watched_column`;
  the torn-read seam's write landing (`session/tests/tasks.rs:2473`
  asserts it).
- Copy: the corpus rows and golden for `VaultRescanFinishedUncounted`.

**Windows** (`RescanTests`, FlaUI):

- First, with the owner's recorded call (the TGC-17 rule):
  `AnotherSessionsChangesAreShownButNotCounted` becomes
  `AnotherSessionsChangesAreAppliedAndCountedExactly` (spec fact 1):
  "Files refreshed. 2 new or changed, 1 removed.", then a foreground run
  after another modify speaks "Files refreshed. 1 new or changed, 0
  removed."
- `ATrimmedFeedFallsBackToTheFullReSyncWithTheNotCountedSentence` (spec
  fact 2), `ATooLargeWindowFallsBackWithTheNotCountedSentence`.
- `ReadingAndHistoryAreNotifiedOnlyForTheWindowsPaths` (spec fact 3):
  a reading model embedding the changed note re-projects, an unrelated
  one does not, and an unrelated note's history panel does not reload;
  `ANoChangeForegroundRunTouchesNothingButTheTabComparison` and
  `ANoChangeRefreshAddsOnlyTheRecoverySet`.
- `AWindowSpanningSeveralPagesAppliesEveryRecord` (spec fact 4; limit 2,
  five changes).
- `ACommitDuringTheReSyncIsAppliedByAFurtherPass` (AR-31: another
  session commits while the tree publication is parked; at the end the
  tree and Quick Open agree).
- `ASlateWriteDuringTheWindowIsReReadNotReverted` and
  `AnOwnRenameAfterTheWindowOpensStillGetsTheOtherSessionsEdit` (the
  journal opened before the window, the tabs compared),
  `AnOwnRenameOfANoteAnotherSessionEditedReloadsTheMovedTab`,
  `AForeignRenameRetargetsTheOpenTab` (Q3),
  `AForeignRenameThenAnOwnRenameOfItsDestinationMovesTheTabThere`,
  `ANoteThatTookARenamedNotesPathKeepsItsTab`,
  `ACancelledRunLeavesTheCursorWhereItWas`.
- `AFailedQuickOpenOrTreeOperationIsRetriedByTheNextRun` (the retry set)
  and `AnUnverifiedTabReloadIsRetriedByTheNextForegroundRun` (the tab
  comparison).
- `AWatcherDrivenChangeFromAnotherSessionIsAppliedWithoutARefresh`
  (spec fact 6; PR 20b's listener path).
- FlaUI `AnotherSession_ChangesCountedOnRefresh` (a `slate-cli`
  subprocess), beside the existing `ExternalFiles_AppearAfterRefresh`.

**Mac** (`VaultRescanTests`, PR 19's class): the Swift twins of the
Windows facts (`testAnotherSessionsChangesAreAppliedAndCountedExactly`
and so on), the watcher one on real FSEvents, plus a VoiceOver check of
the counted cross-session sentence and the uncounted one.

The test switch. PR 20's `SLATE_TEST_DISABLE_VAULT_WATCHER` keeps the
watcher off in every fact that writes from another session and then
presses Refresh, so the count lands on the Refresh.

## 5. Core and FFI sketch

```sql
-- 040_change_feed.sql (draft; the spike ran this shape)
CREATE TABLE IF NOT EXISTS change_feed (
    seq           INTEGER PRIMARY KEY AUTOINCREMENT,
    kind          INTEGER NOT NULL,  -- 1 created, 2 modified, 3 deleted, 4 renamed
    entry         INTEGER NOT NULL,  -- 0 file, 1 directory
    path          TEXT NOT NULL,
    previous_path TEXT,
    hash_before   TEXT,
    hash_after    TEXT,
    writer        TEXT NOT NULL,     -- slate_feed_writer()
    origin        INTEGER NOT NULL,  -- slate_feed_origin(): 0 reconcile, 1 write
    written_ms    INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS change_feed_cursors (
    writer         TEXT PRIMARY KEY,
    seq            INTEGER NOT NULL,
    heartbeat_ms   INTEGER NOT NULL,
    resync_pending INTEGER NOT NULL DEFAULT 0
) WITHOUT ROWID;
-- The head is COALESCE((SELECT seq FROM sqlite_sequence
--                       WHERE name = 'change_feed'), 0).
CREATE TABLE IF NOT EXISTS change_feed_meta (
    id              INTEGER PRIMARY KEY CHECK (id = 1),
    trimmed_through INTEGER NOT NULL
);
INSERT OR IGNORE INTO change_feed_meta (id, trimmed_through) VALUES (1, 0);

CREATE TRIGGER IF NOT EXISTS files_au_feed AFTER UPDATE OF path, content_hash ON files
WHEN (OLD.path IS NOT NEW.path OR OLD.content_hash IS NOT NEW.content_hash)
    AND EXISTS (SELECT 1 FROM change_feed_cursors)
BEGIN
    INSERT INTO change_feed (kind, entry, path, previous_path, hash_before,
                             hash_after, writer, origin, written_ms)
    VALUES (CASE WHEN OLD.path IS NOT NEW.path THEN 4 ELSE 2 END, 0, NEW.path,
            CASE WHEN OLD.path IS NOT NEW.path THEN OLD.path END,
            OLD.content_hash, NEW.content_hash, slate_feed_writer(),
            slate_feed_origin(), CAST(unixepoch('subsec') * 1000 AS INTEGER));
END;
-- files_ai_feed (kind 1), files_ad_feed (kind 3) and the three dirs
-- triggers (AFTER UPDATE OF path only) follow the same shape.
```

```rust
// crates/slate-core/src/change_feed.rs (new) and session.rs
pub const MAX_CHANGE_FEED_PAGE_LIMIT: u32 = 10_000;
pub const CHANGE_FEED_WINDOW_ROW_LIMIT: u64 = 10_000;
pub const CHANGE_FEED_MAX_ROWS: u64 = 100_000;
pub const CHANGE_FEED_CURSOR_LIVENESS_MS: i64 = 24 * 60 * 60 * 1000;
pub const CHANGE_FEED_MAX_PASSES: u32 = 3;

pub enum ChangeFeedStatus { Exact, TooLarge, Gap(ChangeFeedGap) }
pub enum ChangeFeedGap { NoCursor, Trimmed, Reset, ResyncPending }
pub struct ChangeFeedWindow {
    pub token: String,
    pub from_seq: u64,
    pub to_seq: u64,
    pub status: ChangeFeedStatus,
    pub counted: bool,   // false on a gap, too large, or a run past the limit
    pub changed: u64,
    pub removed: u64,
    pub record_count: u64,
}
pub enum ChangeEntry { File, Directory }
pub enum ChangeEffect { Renamed, Removed, Present }
pub enum ChangeCount { Created, Modified, Removed, Uncounted }
pub struct ChangeRecord {
    pub path: String,
    pub entry: ChangeEntry,
    pub effect: ChangeEffect,
    pub renamed_from: Option<String>,
    pub from_hash: Option<String>,   // Renamed: the baseline the moving tabs hold
    pub counted: ChangeCount,
    pub content_hash: Option<String>,
    pub summary: Option<FileSummary>,
    pub openable: bool,
    pub seq: u64,
}
pub struct ChangeFeedPage { pub records: Vec<ChangeRecord>, pub next_cursor: Option<String> }

impl VaultSession {
    pub fn register_change_feed(&self) -> Result<u64, VaultError>;
    pub fn change_feed_window(&self, after: Option<u64>, cancel: &CancelToken)
        -> Result<ChangeFeedWindow, VaultError>;
    pub fn change_feed_page(&self, window: &str, cursor: Option<String>, limit: u32,
        cancel: &CancelToken) -> Result<ChangeFeedPage, VaultError>;
    pub fn change_feed_head(&self, cancel: &CancelToken) -> Result<u64, VaultError>;
    pub fn acknowledge_change_feed(&self, window: &str, cancel: &CancelToken)
        -> Result<(), VaultError>;
}

// a11y.rs
VaultRescanFinishedUncounted { reason: RescanReason, changed: u64, removed: u64 },
```

FFI (`slate-uniffi`): the five methods, the records and enums above, and
`max_change_feed_page_limit()` and `change_feed_max_passes()` as exported
constants, so both hosts use one number (the `max_indexed_hash_paths`
precedent, `lib.rs:6484`). `FileChangeEvent` does not change. Both
bindings are regenerated (program rule 7); until 21b and 21c land, no
host calls the new methods, so 21a changes no behaviour (the triggers are
gated off while no cursor exists). 21a also corrects the stale
"scan-delta cursors" comment (`session.rs:1196`) and the torn-read seam
(§3.5).

## 6. Host designs

**Windows (PR 21b).**

- The open: `RegisterChangeFeed()` between `ScanInitialWithProgress` and
  `LoadSwitcherFiles` inside the session load (`VaultLifecycleViewModel.cs:524-529`).
- The run (`VaultLifecycleViewModel.Rescan.cs:271-348`): the scan as
  today; the Slate-write journal opened (`BeginReplacement`, `:446`,
  moved ahead of the window and kept for the whole run); the retry set
  retried; `ChangeFeedWindow(null)` through the rescan seam with the
  run's token; `Exact` → the record path below; `Gap` or `TooLarge` →
  today's `ReSyncFromIndexAsync` unchanged (`:363-386`) as the fallback;
  the workspace's document re-sync over every open tab, as today
  (`ReSyncOpenDocumentsAsync`, `WorkspaceViewModel.Rescan.cs:173`); the
  journal re-read after each window and at the end; then the head check
  and up to two more passes (§4.6); the recovery set when the reason is
  Explicit; `AcknowledgeChangeFeed` unless cancelled or a feed read
  failed; the sentence per §4.7 (`:334-347`).
- The record path, page by page (limit 1,000): `Renamed` through the
  rename arm of `ApplyFileChangeEffects` in rescan mode (silent, no
  "missing from disk" line), moving only tabs whose baseline equals
  `from_hash`; `Removed` and `Present` through a new
  `QuickSwitcherViewModel.ApplyRecordsAsync`, which applies a page in one
  pass over a path index, and the dependents' per-path notifications;
  then, once, the tree's `RefreshForRescanAsync` and
  `ReSyncDependentsAsync` narrowed: reading models given the window's
  paths, `HistoryNoteChanged` (`:668`) answered from the records, Bases
  and graph when any file record exists. A branch that fails is counted,
  as today, and enters the retry set.
- Facts per §4.9; the AR-30 witness rewritten first; R-9's existing
  facts stay green, since the fallback is the old path.

**Mac (PR 21c, after PR 19 and PR 20c).**

- The open: `registerChangeFeed()` between `scanInitialWithProgress` and
  the `listFiles` loop in the detached open task
  (`AppState.swift:11815-11829`).
- PR 19's `rescanVault(reason:)` gains the same journal, retry set,
  window, pages, passes, acknowledgment and sentence rules. PR 19's full
  re-sync (tree, inventory, tabs by hash with PR 10's re-seat, Quick
  Open, leaves) becomes the fallback. The record path: the flat `files`
  inventory (which counts, Quick Open and create-name checks read)
  applies records by path; the tree applies `Present` summaries through
  `publishSidebarFileSummaries` (`AppState.swift:14732`) and refreshes
  silently for structural records; `Renamed` moves the tabs whose
  baseline equals `from_hash`; PR 19's tab step compares every open tab
  on every run; the leaves follow PR 19's steps narrowed to the window's
  paths; the Slate writes journaled since the run began are re-read after
  each window and at the end.
- Facts per §4.9 and a VoiceOver check (program rule 6).

## 7. Implementation plan

| PR | Scope | Size | Lanes | After |
|---|---|---|---|---|
| 21a | Core: migration 040, the feed functions and origin scopes, `change_feed.rs` (register, the window's state machine and materialization, pages, head, acknowledge, trim, reap, the gap's re-registration), the uncounted variant with its corpus rows and golden, the FFI records, methods and constants, both bindings regenerated, the torn-read seam, the census and the §4.9 core facts | L (about 2,000 to 2,600 lines with facts) | rust, swift, windows | 20a |
| 21b | Windows: open registration, the run-long journal, the retry set, the record path, passes, recovery set, acknowledgment, sentence, the rewritten AR-30 witness, facts, FlaUI journey | M–L (about 1,200 to 1,600) | windows | 21a, 20b |
| 21c | Mac: the same on PR 19's rescan, facts, VoiceOver check | M–L (about 1,000 to 1,400) | swift, a11y, windows | 21a, PR 19, 20c |

Facts by PR: 21a takes §4.9's core list; 21b the Windows list; 21c the
mac list.

Interlocks:

- **PR 19.** Its full re-sync is 21c's fallback, unchanged. Nothing in PR
  19 waits for PR 21; PR 19 should keep its re-sync steps behind separate
  functions so 21c can narrow each to a set of paths.
- **PR 20.0** (#1429). Every feed run starts with a scan, so the bounded
  link pass keeps runs cheap; no code dependency.
- **PR 20a.** 21a is written against 20a's `RescanReason::Watcher` (the
  new variant takes the reason) and its session and FFI listener changes,
  and merges after it. 21a can be developed alongside 20b and 20c.
- **PR 20b, PR 20c.** The watcher facts of 21b and 21c need their
  listener wiring and lattices; 21b after 20b, 21c after 20c.
- **PR 22** (design #1441). Its 22a opens the session's cache through
  `open_database_anchored` and a unix-VFS system-call hook on the held
  `.slate` descriptor (`FsRoot`, #1441 §4.1 and §6), and moves the
  compaction worker's connection the same way. The feed's functions are
  registered on whatever connection the open returns: the defaults by
  `register_connection_functions` inside both opens, the session's
  overrides right after. Whichever of 21a and 22a lands second rebases
  that one call site; nothing in the feed reads the root's pathname, and
  a session bound to its own `.slate` descriptor is what the writer id
  and the cursor row already assume (#1441 §7 calls the two independent).
  22a's identity-keyed structural-lock registry does not touch the feed.
- In `00_program.md` §5, PR 21's row becomes 21a, 21b and 21c as above.

## 8. Documents and registers the implementing PRs change

- Locked 05 §4.4 (`:338-347`), with the owner's call (Q6), beside PR 20's
  amendment: "Changes this session did not announce (another session's
  writes, and every session's scans) reach a host through the change
  feed, a paged, cancellable read it opens after a rescan; they never
  arrive as file-change events." §4.5 lists the three feed tables (21a).
- Contract 40: AR-30 retired (counts are exact across sessions, with
  §9's gap row); AR-31 narrowed to §9's continuous-writer row; AR-8
  amended for renames another Slate session made (if Q3 is yes); R-9
  amended: the re-sync applies the window's records, compares every open
  tab as today, and is the full re-sync only on a gap or an oversized
  window, plus the retry and recovery sets (21b, 21c).
- `w_c_matrix.md` and `parity_matrix.md`: the vault scan status, Files
  tree and Quick Open rows (21b, 21c). `docs/runbooks/
  voiceover-feature-test.md` §A: a cross-session count step (21c).

## 9. Residual risks (proposed contract 40 rows)

1. A gap (a cursor reaped after 24 hours without a run, the 100,000-row
   cap, a rebuilt or restored cache), or more than 10,000 rows since the
   last run that are not all this session's own writes, falls back to the
   full re-sync with the uncounted sentence; a foreground or watcher run
   then speaks only this scan's own changes.
2. A writer committing continuously faster than a pass: after three
   passes the run ends, and the tree and Quick Open can differ by the rows
   after its last target until the next run. Likewise, a change another
   session commits while a host loads its first view can leave the two
   apart until the first run, which counts it.
3. A Slate-owned write site added without its Write scope is applied
   again and counted by its own session's next run; the census fact
   covers the APIs it lists.
4. A raw SQLite connection without Slate's functions cannot change a
   watched column of `files` or `dirs` (measured). Tools and tests that
   write the cache directly must register them.
5. A crashed host's cursor keeps the rows written after it for up to 24
   hours, within the cap.
6. A late own structural event: the structural operations notify after
   releasing the connection lock (`session.rs:15857-15867`), so their
   event can, rarely, reach the host after the run whose window already
   accounted for the row, past its last journal re-read. Quick Open can
   then keep an entry for a path another session moved on, and a tab can
   follow the event to a path that is gone, where the next run's tab
   comparison marks it missing instead of retargeting it. The explicit
   Refresh's full Quick Open reload clears the entry; the tab stays a
   missing tab (AR-8's posture). Moving those notifications before the
   lock is released, as the save seat does (`:4376-4393`), would close
   it; that is left to 21a's review, since it touches about ten notify
   seats.
7. An index-only change by another session (a repair) has no file event
   for the watcher, and waits for the next run.
8. AR-7 unchanged: the feed records what the index commits, so an
   mtime-preserving same-size edit the scan skips is not in it either.
9. Feed rows carry vault paths inside the cache, as `files` already does;
   core's logs about the feed carry no paths (the #507 rule).

## 10. Open questions for the owner

- **Q1.** The uncounted wording (§4.7): approve "Files refreshed.
  {changed} new or changed, {removed} removed; changes from other Slate
  sessions were not counted." and "Files refreshed. No changes found;
  changes from other Slate sessions were not counted." (recommended), or
  give other words. The corpus golden makes it a product decision.
- **Q2.** Count other sessions' changes in foreground and watcher runs
  on the foreground policy (recommended): a `slate write` while Slate is
  active then speaks "Files refreshed. 1 new or changed, 0 removed.",
  within PR 20's one-sentence-a-minute window. This reverses PR 20 §4.5's
  note that a burst caused by `slate-cli` is silent. Depends on PR 20's
  Q2 and Q4.
- **Q3.** Retarget the open tabs that hold a file another Slate session
  renamed, as for this session's own renames (recommended; only tabs
  whose baseline matches the renamed file move), or keep AR-8's missing
  tab for every rename this session did not make?
- **Q4.** Count a known rename as "1 new or changed, 1 removed", as R-9
  counts an external rename today (recommended, no copy change), or add a
  "moved" count (a copy change)?
- **Q5.** The constants: cursor liveness 24 hours, row cap 100,000,
  window row limit 10,000 (a run with more rows since its last one is
  uncounted), at most three passes per run.
- **Q6.** Approve §8's amendment of locked 05 §4.4 and §4.5, together
  with PR 20's Q5 or alone.
- **Q7.** Record the call that retires AR-30, so 21b can rewrite
  `AnotherSessionsChangesAreShownButNotCounted` first (the TGC-17 rule).
- **Q8.** Keep the recovery set on explicit Refresh (recommended): Quick
  Open's whole list is reloaded and every surface showing a failure is
  re-checked even when the window is empty, so Refresh still means "try
  again" (tabs are compared on every run anyway).

Points that depend on PR 20's open questions:

- PR 20 **Q1** is answered by #1429 (PR 20.0); PR 21 assumes the bounded
  link pass for run cost only.
- PR 20 **Q2**: with the leading one-sentence-a-minute window (PR 20's
  recommendation), a silent watcher run's counts are dropped and its
  window acknowledged. If the owner picks a trailing summary, 21a adds a
  count over a longer range (the same derivation, from a second
  per-session mark that keeps those rows from being trimmed), still
  bounded by the 10,000-row limit.
- PR 20 **Q4**: the uncounted variant follows the same choice (one shared
  sentence, or a reason-specific one).
- PR 20 **Q5**: Q6 above lands with it or alone.
- PR 20 **Q7**: the mac watcher fact of 21c rides the same FSEvents
  witness.
- PR 20 **Q3** and **Q6**: no dependency.
- PR 20 decisions this design relies on: the echo ledger, one callback
  per burst, `RescanReason::Watcher`, no watcher run while the app is
  inactive, and the watcher test switch.

## 11. Owner decisions (2026-10-17)

The owner answered §10 in a decision interview on 2026-10-17. Every answer below is the recommendation except where marked.

- **Q1.** The uncounted wording is approved as proposed.
- **Q2. Not the recommendation.** Other sessions' changes are counted and spoken **only on an explicit Refresh**.
  - Foreground and watcher runs apply the window silently: the tree, Quick Open and tabs update.
  - Those runs speak only their own scan's changes, on the foreground policy, so a `slate write` while Slate is active is not announced.
  - PR 20 §4.5's silent CLI burst stands.
  - Consequence for 21a: the per-run counts derived from other writers are used for Refresh's sentence only. No second per-session mark is needed, because PR 20's Q2 kept the leading one-a-minute window.
- **Q3.** A tab holding a file another Slate session renamed is retargeted, as for this session's own renames. Only tabs whose baseline matches move, and a dirty tab keeps its buffer. AR-8 is amended for renames another Slate session made.
- **Q4.** A known rename counts as "1 new or changed, 1 removed"; no copy change.
- **Q5.** The constants are accepted: a cursor is live for 24 hours, the feed holds at most 100,000 rows, a window over 10,000 rows takes the uncounted full re-sync, and a run makes at most three passes.
- **Q6.** The locked 05 §4.4 and §4.5 amendment is approved, together with PR 20's (#1420, Q5).
- **Q7.** AR-30 is retired when 21a lands. 21b rewrites `AnotherSessionsChangesAreShownButNotCounted` first (TGC-17). Under Q2, the exact cross-session count is spoken on Refresh.
- **Q8.** Explicit Refresh keeps its recovery set: it reloads Quick Open's whole list and re-checks every failed surface, even with an empty window.
- **Plan.** §7's split is queued after 20a: 21a, then 21b and 21c.
