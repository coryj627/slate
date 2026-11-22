# CB-45: Reads that do not wait behind a scan (design)

Issue: #1718 (Refs; it stays open until the last phase below merges). Size S (documents only). Lanes: none (the PR adds this one file, which no lane lists). AT: none. Wave: C11, slot 3. Decision: CD34 (the owner, 2026-11-16): a read-only second connection in WAL mode, phased from Quick Open, built in plan 47, no interim change. Review: a red team on the spec, then a red team on this document, then Codex adversarial-review rounds until it approves (plan §4 rule 9); each is recorded at the end. The owner reads this design before CB-45b starts.

Written 2026-11-21 against `main` at 02fbd37f (after #2036, CB-33) by the chunk's first principal, and finished on 2026-11-22 at 3f2fd733 by its second, with the lead's rulings on the eight calls. Step 0 ran at 02fbd37f in the chunk's worktree with a Cargo target directory of its own, as three temporary test modules under `crates/slate-core/src/session/tests/` built into core's release-profile test binary, and removed with `git checkout` afterwards. The probe sources, the build scripts and the three output logs are kept with the chunk's working notes; no probe code is in the repository. Nothing in this chunk changes product code. A statement made by reading says so.

`S` is `crates/slate-core/src/session.rs`; `D` is `crates/slate-core/src/db.rs`; `W` is `apps/slate-windows/src/SlateWindows`; `M` is `apps/slate-mac/Sources/SlateMac`.

Two words are kept apart throughout. **A reader** is the person using Slate. **The read connection** is the second SQLite connection this design adds, and **the read seat** is the session field that holds it.

## In brief

- A session serves every read and write of its hosts through one connection behind one mutex, and a scan holds that mutex from its first step to its last. (The op-log compactor has a connection of its own; no host read uses it.) On a vault of 10,000 notes a rescan in which nothing changed held it for 0.5 to 1.1 s, and a rescan in which every note changed held it for 4.5 to 10.7 s. Quick Open's rank, a tree page, a tag tree page and a search, issued at the start of the scan, each returned when the scan did.
- The cache is already in WAL mode, on every open. Nothing has to migrate.
- A second connection opened read-only on the same cache answered Quick Open's read in 39 to 55 ms (medians) at every point of a held scan, the same as with no scan running. It saw the index as of the last commit: the pre-scan rows until the scan committed, the new rows from then on, even while the scan still held the mutex.
- The design is one read-only connection per session, opened the first time a read asks for it, behind a mutex of its own, through `db.rs`'s one configure function. A read on it is one read transaction that ends before the call returns. Where the cache is not in WAL, or the read connection cannot open or fails, the read runs on the main connection and gets today's answer.
- Three phases build it: CB-45b moves Quick Open's rank; CB-45c moves the files tree, the directory pages and the tag tree; CB-45d moves search and the sidebar filter, and closes #1718. Reads that write, the graph, Bases, the change feed and every write stay on the main connection, with the reason for each.
- The price is in "What a read on the read connection can see". During a rescan Quick Open lists the vault as of the last commit, which until the scan commits is the index as it stood before the scan, and speaks that count. When the scan's changes arrive the host replaces the rows silently, as it does today for a Quick Open left open across a rescan: and the corrected count is not spoken. When the scan was started by a change made outside Slate, its run speaks no sentence while Quick Open is open, so the reader hears the pre-scan count and then nothing more, unless the scan removed the row they had selected, which is then named (next bullet). During a Slate-owned write a typed query speaks the count from before the write and then the write's corrected count. The owner accepted the counts on 2026-11-22 (Call 2, "Accept, record it (Recommended)"; Call 10, "Accept (Recommended)"); CB-45b records them as an AR row in doc 40 and hears them in its NVDA spot check.
- A re-rank that cannot keep the reader's selected row moves the selection to the first row without naming it, today and more often with the read connection. The owner's answer to Call 9 (2026-11-22, "Speak new selection (Recommended)"): CB-45b makes Quick Open speak the new selection with the sentence an arrow move speaks, and the re-rank after a Slate-owned write keeps a surviving selection (the lead's ruling under that answer). CB-45b records it as an addendum after R-9.
- The later phases bring the same kind of price for the tree, the tag tree, search and the filter. Each of them owes its own reading of it and its own owner call; the owner's answer above covers Quick Open only.

## The lead's rulings (2026-11-22), binding on the build

The spec put eight calls to the lead. The rulings, with the owner's answer to Call 2, are below, and then the owner's answers to two more calls that the reviews of this document raised (rulings 9 and 10; "Calls for the lead, as ruled" gives each call in full).

1. **Three phases.** CB-45b is Quick Open. CB-45c is the tree, the directory pages and the tag tree. CB-45d is search and the sidebar filter, and closes #1718. The note panels and the other plain reads do not move (section 6's last row).
2. **The owner's answer, verbatim: "Accept, record it (Recommended)".** During a rescan a typed Quick Open query speaks the count from before the scan, and the later re-rank is silent. CB-45b appends an AR row to doc 40 that quotes the answer (its text is in section 5), and CB-45b's NVDA spot check records that sequence on base and on head. The answer is for Quick Open; CB-45c and CB-45d bring their own cases to the owner (section 5).
3. **No FFI change in CB-45b.** It changes no FFI signature and does not edit `crates/slate-uniffi/src/lib.rs`.
4. **A fallback's wait for the main connection's mutex becomes cancellable,** in the same function, shipping with the read connection. This is a departure from CD34's "No interim" (section 4, step 5).
5. **The read connection opens on first use.** Section 1 and the risks say what that leaves unrun on macOS in CB-45b.
6. **The Windows UI-thread find is filed** as #2039 (labels bug, windows). It names the three session calls that run on the UI thread and freeze the window for the rest of a scan, says that CB-45c frees `ShowTags` and `CopyWikilink`, and that `RefreshExternalStaleness`, which calls `NoteTasks`, needs a change of its own. Codex's fifth round on this document made the second conditional: CB-45c moves `wikilink_for_path` only if the link it copies names the same note after a scan (CB-45c's row).
7. **The #1954 line and the two #1955 lines are posted** (#1954, comment 7075132728; #1955, comment 7075132994).
8. **This document lands at** `docs/plans/47_windows_core_closeout/specs/cb45_reads_do_not_wait_behind_a_scan.md`, with no probe code.
9. **The owner's answer to Call 9, verbatim: "Speak new selection (Recommended)".** When a Quick Open re-rank cannot keep the selected path, it speaks the new selection with the existing `RowSelected` sentence. The owner answered the call as first put, for R-9's silent re-rank during a rescan. The lead ruled the same day that the widened option follows from that answer: the re-rank after a Slate-owned write keeps a surviving selection too, and speaks the new selection when it cannot keep it. CB-45b builds both and records them in an addendum after R-9 (section 5).
10. **The owner's answer to Call 10, verbatim: "Accept (Recommended)".** During a Slate-owned write, a typed query speaks the count from before the write and then the write's corrected count. It is a sentence in Call 2's AR row.

The lead also accepted two placements on 2026-11-22: CB-45b, not this documents-only PR, appends the AR row and the addendum; and CB-45b's overlap with CB-43 over `windows.yml` and `Workflows.Tests.ps1` is a note in "For the plan". The owner answered Call 2 after midnight Eastern time, so its date is 2026-11-22.

## Re-read at 3f2fd733 (2026-11-22)

`main` moved from 02fbd37f to 3f2fd733 by one commit, #2038, which changed documents under `docs/plans/` only. No code line cited here moved; the citations into `S`, `D`, `W` and `M` were read at 02fbd37f and hold at 3f2fd733. Two kinds of citation did move:

- `reading/07_core.md` gained one line above its #1718 section (a correction to #1917's), so every reading line cited here is one later than at 02fbd37f. The drift table gives the lines at 3f2fd733.
- The plan grew by its C9 and C10 record. The plan lines cited below ("For the plan" and the drift table) are read at 3f2fd733.

## Drift check (the plan, the reading and the issue)

Code lines at 02fbd37f, which hold at 3f2fd733; reading and plan lines at 3f2fd733.

| Claim | Where | What the tree says |
|---|---|---|
| One `Mutex<Connection>` serves every read and write | #1718; `reading/07_core.md:189` (cites `S:1293`) | Holds. `S:1298-1300`. The reading's line numbers predate CB-31 to CB-33: `S:1293` is now `S:1300`, `:3612` is `:3619`, `:6702` is `:6950`, `:10860` is `:11382` |
| `scan_session` holds the mutex for the whole scan | #1718; the doc sentence at `S:3401-3404` | Holds. Taken at `S:3630`, dropped at `S:3737` (or `S:3686` on a cancel after the commit, or on the error return at `S:3670`) |
| The scan is one IMMEDIATE transaction | `reading/07_core.md:190` | Holds. `db::begin_fenced` at `S:11450`, commit at `S:11722`. `count_files` (`S:11440`) walks the vault under the mutex before the transaction opens |
| Quick Open's rank copies rows out under the lock and scores after it | #1718 | Holds. `S:6950-6996`; the lock is taken at `S:6964` and the wait cannot be cancelled; the token is checked before and after the wait |
| "`db.rs` falls back when WAL is unavailable" | #1718 | Wrong, as the reading already said (`reading/07_core.md:193`). `apply_pragmas` (`D:578-611`) sets `journal_mode=WAL` on every open and never reads back what SQLite answered. There is no fallback code |
| A second connection per session already exists | `reading/07_core.md:192` | Holds. `worker_conn` (`S:1305`), opened on the compactor's first job by `open_worker_connection` (`S:1628-1636`) through `db::open_connection` |
| "The process-wide rank lane holds later keystrokes" | #1718 | Holds, and the lane is Windows host code only: `QuickSwitcherRankCoordinator.Shared`, a `SemaphoreSlim(1,1)` (`W/QuickSwitcherRankCoordinator.cs:14-45`). Core and the FFI have no lane |
| CB-45b is L, its lanes are rust, windows, swift, and it changes `crates/slate-uniffi/src/lib.rs` | plan §5 row CB-45b (`:656`); §6 (`:719-722`, the shared-file row `:832`); §11 (`:1108`) | The design below changes no FFI signature and does not edit that file (ruling 3), and the reading sized "Quick Open first" M (`reading/07_core.md:330`). See "For the plan" |
| A VoiceOver check of Quick Open during a rescan is owed once CB-45b lands | plan §8 (`:909-910`); `cb00` spec `:176` | The Mac's Quick Open does not call the indexed rank. It ranks a host-held list with the free `switcherRankTop` (`M/QuickSwitcherModel.swift:171-189`) and never takes the session's connection (contract note AR-63, `docs/plans/40_nvda_matrix_remediation_contracts.md:101`). CB-45b changes nothing a VoiceOver user meets. See "For the plan" |
| Every connection core opens goes through one configure function, and a text census and a fact hold that | CB-33's §12 line (plan `:1208`); #2036 | Holds. `D:299-301`, `D:307-400`, `D:408-423`; `db/connection_census.rs`; `session::tests::two_sessions::every_connection_core_opens_is_configured` |

## Step 0: measured on main (02fbd37f), 2026-11-21

### How it was measured

Three probe test modules, each run alone (`--exact --ignored --nocapture --test-threads=1`) from a release-profile build of core's test binary (the test seams are compiled in). Each makes a real vault on NTFS under the user's temp folder: 10,000 notes in 100 folders, with front matter, tags, links, embeds and one dangling link in ten. It opens the vault with `VaultSession::from_filesystem`, so the cache opens through the held `.slate` as a host's does, and drives scans on one thread and reads on others. A scan is traced and parked through core's own test seam (`scan_point_test_hook`, `S:11315-11348`), which fires on the scan's thread with the mutex held.

The limits of what follows:

- One desktop with an NVMe drive, one run of each case unless a median or a range is given. The figures size the wait. They are not a benchmark.
- SQLite 3.54.1 (rusqlite 0.41, bundled).
- The notes are synthetic: about 300 bytes each in the first vault, about 4 KB each in the second.
- **The second connection in the probes is not the one the design builds.** It was opened in the test module with `SQLITE_OPEN_READONLY` on the session's anchored cache path and core's SQL functions registered. It did not pass through `configure_connection` (so 16 cached statements and no planner stability setting), it kept rusqlite's 5 s busy timeout, and its reads ran in autocommit with no explicit transaction. Its Quick Open read is a copy of the statement and the ranker call in `switcher_rank_top_indexed`. Its other reads are `list_files_impl`, `list_dir_children_impl` (the whole-level listing, not the paged one), a tag tree built from the two statements `tag_tree` runs, and `search_db::full_text_search`. They size the reads a later phase moves. They are not that phase's functions.

### How long a scan holds the connection

| Scan of 10,000 notes | Small notes | 4 KB notes |
|---|---|---|
| First scan, empty cache | 4.5 to 4.7 s (2 runs) | 11.4 s |
| Rescan, nothing changed | 0.48 to 0.72 s (5 runs) | 0.81 to 0.83 s (3 runs) |
| Rescan, 1 note changed | 0.54 to 0.66 s (2 runs) | not run |
| Rescan, 100 notes changed | 0.68 to 0.80 s (3 runs) | 1.0 s |
| Rescan, every note changed | 4.45 to 5.67 s (8 runs) | 10.7 s |

Inside one scan of the small vault with no op-log history: the first file was reached 10 to 19 ms in (an upper bound on the pre-count walk, `count_files`, which runs under the mutex before the transaction opens); the commit took 6 to 164 ms (726 ms for the first scan of the 4 KB vault); the maintenance after the commit took 0 to 6 ms, because that vault had no history for it to work on.

The third probe gave the small vault a history: 1,000 of its notes saved through Slate three times each, so 1,000 op logs and 3,000 events. (A save through the session took 38 ms on average.) Then the maintenance has work, and a second connection polling about once a millisecond sees the cache change more than once in a scan:

| Scan of the vault with history | Whole scan | Of which the commit | Of which the maintenance | Times a second connection saw the cache change |
|---|---|---|---|---|
| Rescan, nothing changed (3 runs) | 1.03 to 1.09 s | 116 to 178 ms | 107 to 118 ms | 1, 2 and 1 |
| Rescan, nothing changed, 1,000 events past the retention window | 0.97 s | 147 ms | 109 ms | 3 |
| Rescan, 100 notes changed outside Slate | 1.19 s | 270 ms | 111 ms | 1 |
| Rescan after a parser bump (the events are rebuilt from the logs) | 1.32 s | 115 ms | 472 ms | 3 |
| First scan after the cache was deleted, the logs kept | 5.8 s | 208 ms | 679 ms | 4 |

So a vault with history holds the connection longer than the first table shows (about a second for an unchanged rescan here), and all of it is wait for a read today.

### What each read waits today

Each read was issued through the session when the scan reached its first file, and timed until it returned.

| Read (through the session) | Idle, median of 15 | Behind an unchanged rescan | Behind a rescan of 100 changed | Behind a rescan of every note |
|---|---|---|---|---|
| Quick Open's rank (`switcher_rank_top_indexed`, a query every note matches) | 44 ms | 507 ms | 720 ms | 5,063 ms |
| One page of `list_files` | 4.2 ms | 481 ms | 680 ms | 5,019 ms |
| One directory page | 3.2 ms | 478 ms | 684 ms | 5,023 ms |
| One tag tree page | 7.1 ms | 475 ms | 694 ms | 5,033 ms |
| `full_text_search`, vault scope | 0.6 ms | 481 ms | 695 ms | 5,033 ms |

The three scans took 476, 686 and 5,025 ms. Every read returned within 40 ms of the scan's end. With 4 KB notes the same reads returned after 0.81 to 0.85 s, 1.0 s and 10.7 s.

What starts the scan a reader waits behind, on Windows (by reading): Refresh, the vault watcher after a change made outside Slate, and the window coming back to the foreground after two seconds away. Quick Open cannot be opened on Windows during the first scan: the switcher is built after the open scan returns (`W/VaultLifecycleViewModel.cs:735-752`). So on Windows the wait is a rescan's. On this machine that is half a second to a second per 10,000 notes when little changed, and the length of a first scan when most of the vault changed under Slate (a sync client's first pull, a branch switch in a vault kept in git).

### The journal mode, and whether anything migrates

- The main connection reads `journal_mode = wal`, `page_size = 4096`, `wal_autocheckpoint = 1000`, `busy_timeout = 5000`, `synchronous = 1` (NORMAL), `locking_mode = normal`.
- `apply_pragmas` sets WAL on every open (`D:590-599`), and has since the oldest commit that holds `db.rs` (9a82334f, 2026-05-22). WAL is a property of the database file, so every cache a build of Slate has opened is in WAL already. **No migration is needed, and the design adds none.**
- `apply_pragmas` does not read the mode back. SQLite answers the old mode when it cannot switch (an in-memory database answers `memory`, which the existing fact `pragmas_are_applied`, `D:1699-1725`, tolerates). On Windows and on macOS the VFS in use has shared memory, so the switch succeeds; no path to a session that is open and not in WAL was found. The design still reads the mode on the read connection, because the read is one pragma and the next section shows what a second connection does without WAL.
- A database reached through an SMB path to this machine (`\\localhost\C$\...`) also answered `wal`, and a read-only connection read it while a write transaction was open. This is weak evidence for a vault on a share: a UNC path puts SQLite into a locking mode that the session's synthetic cache path never uses (by reading `sqlite3.c`). FAT32 and exFAT were not measured here (a VHD needs elevation); CB-45b's weak-volume fact reads the mode there.

### What a second connection does without WAL

A copy of the 10,000-note cache was put in rollback-journal mode (`journal_mode=DELETE`), with a writer whose page cache is the session's (4,096 pages) and a second connection with no busy timeout.

- The second connection read normally while the writer's open transaction grew through 14 steps of about 250 pages each. At the fifteenth it was refused with "database is locked", and stayed refused until the writer committed. (By reading SQLite: that is where the writer's page cache fills and spills to the file, which takes the exclusive lock.)
- A second connection holding a read transaction made the writer's commit fail with "database is locked" after its busy timeout.

So without WAL a second connection is refused part of the way through a large scan, and it can refuse the scan's commit. The read connection is used only when the cache reads back `wal`.

### What a read-only connection costs to open and to keep

| | Measured |
|---|---|
| Open (the anchored path, read-only, functions registered) | 0.22 to 1.81 ms (5 opens) |
| Open while a scan's write transaction is in flight | 1.3 ms |
| First rank on a fresh connection | 45 to 58 ms (an idle rank on a warm one: 52 ms median) |
| Memory after every read below had run on it | 2.1 MB of page cache (SQLite's default limit, 2 MB), 71 KB of schema, 96 KB of statements |
| A rescan of every note with a second connection ranking in a loop on another thread | 4.67 s; 84 ranks, no error, 54 ms median, 64 ms at most. Runs with no second connection ranged from 4.45 to 5.67 s, so no cost shows above the variation between runs |

Handles, by reading SQLite: on Windows a connection adds three (the database, `-wal`, and its own lock handle on `-shm`); the mapping of `-shm` is shared by every connection of the process on that path. On macOS and Linux it adds two descriptors (the database and `-wal`); the `-shm` descriptor is shared per file.

### The second connection while a scan is parked

The scan was a rescan of every note plus 500 new notes, parked in turn at its first file, at the end of the walk, just before its commit, and after its commit while it still held the mutex. At each stop the main connection's mutex was held (`try_lock` failed).

| Parked at | Quick Open's read on the second connection (median of 5) | Rows it saw | Other reads on it |
|---|---|---|---|
| The first file | 55 ms (5.7 ms to copy the rows, the rest scoring) | 10,001, the pre-scan index | `list_files` page 9.2 ms; a directory 15.5 ms; the root directory 13.3 ms; the whole tag tree 12.1 ms; search 1.1 ms |
| The end of the walk | 50 ms | 10,001 | 6.5, 11.6, 12.8, 10.1, 0.8 ms |
| Just before the commit | 39 ms | 10,001 | 6.1, 11.6, 12.3, 8.5, 0.9 ms |
| After the commit, mutex still held | 52 ms | 10,501, the scan's result | 6.3, 11.5, 10.9, 8.2, 1.9 ms |

The slowest single rank was 59 ms. After each read the connection was in autocommit with no statement in progress. Across all runs the longest of the other reads on a warm connection was 21.7 ms (the root directory), and the first directory listing on a cold one took 33 ms.

### What the scan's one long write transaction does to a second connection's snapshot

- A read that starts while the scan's transaction is open sees the last commit before the scan. It never sees a half-indexed vault, because the walk is one transaction.
- A read that starts after the scan's commit sees the scan's result, whether or not the scan still holds the mutex.
- A save on the main connection is visible to the next read on the second connection (10,000 rows before, 10,001 after).
- A read transaction left open on the second connection keeps its old rows after the commit (10,501 against 10,502 on a fresh read), and stops every checkpoint: a PASSIVE checkpoint moved 0 of 5,241 frames, and a TRUNCATE checkpoint returned busy. After the transaction ended a PASSIVE checkpoint moved all 5,264 frames.
- The second connection's `PRAGMA data_version` moves by one each time it looks and finds that another connection has committed since its last look. It moves over a rescan in which nothing changed too: the files' fast path writes nothing (`S:11981`), but the walk upserts every directory row (`S:11789-11804`), so the scan still commits a change.
- Within one scan a second connection can see more than one change: the scan's commit, and then each maintenance commit that writes. One to four were measured (the table of the vault with history above).

### Checkpoints and the `-wal` file

- Nothing in core runs or configures a checkpoint (by search). SQLite's automatic checkpoint runs after a commit that leaves 1,000 or more frames in the log, on the connection that committed (the main one, or the compactor's).
- The `-wal` file is as large as the largest transaction since it was last emptied, and it does not shrink while a connection is open: 22.4 MB after the first scan of the small vault (the database is 22.0 MB), 150.8 MB after the first scan of the 4 KB vault (the database is 149.7 MB). It stayed at 22.4 MB through every later scan, save and read until the probe itself truncated it. That is today's behaviour and the read connection does not change it.
- A read-only connection cannot complete a checkpoint: `PRAGMA wal_checkpoint` on it failed with "disk I/O error" (by reading SQLite: the checkpoint starts and fails at its first write to the database file). It never needs one, since the main connection's commits run them.
- A snapshot left open across scans makes the log grow. With no snapshot open, two rescans of every note left the log at 21.4 MB after each (the second reused the space of the first). With one read transaction held open on a second connection, the log was 21.5 MB after the first rescan and 43.0 MB after the second, and it stayed 43.0 MB after the transaction ended.

### Who closes last

- The session's connections closed first and a read-only connection closed last: its close succeeded and left `-wal` (43 MB in that run) and `-shm` on disk. The last connection to close checkpoints and removes them, and a read-only one cannot.
- The next open of that cache recovered the log and read every row (58 ms for the open and a first rank together).
- The read-only connection closed first and the session after it: both files were removed, as they are today.

So the order of closing decides whether a clean close leaves the log behind. Nothing is lost either way.

### Core's SQL functions and migration 042's indexes

A read-only connection with none of core's functions registered:

- counted `files` and `links` and ran Quick Open's read correctly (that statement names no function);
- failed to prepare a directory listing: `no such function: slate_tree_sort_key`.

So the schema loads without the functions, and a statement that names one does not prepare. Migration 042 rebuilt the seven expression indexes over `slate_tree_sort_key` in the session's own process, with the function `register_connection_functions` registers (`D:473-534`). The read connection runs in that process and registers the same functions through the same call, so its keys and the stored keys come from one function. The design registers them when the read connection opens, for Quick Open too, so no later phase can move a tree read onto a connection that lacks them.

### What a read-only connection may and may not do

| | Opened with `SQLITE_OPEN_READONLY` | Opened read-write with `PRAGMA query_only = 1` |
|---|---|---|
| A write to the cache | refused: "attempt to write a readonly database" | refused, same text |
| A TEMP table | allowed | refused |
| `PRAGMA wal_checkpoint` | fails ("disk I/O error") | runs |

### File locking and sharing of `-wal` and `-shm` (by reading SQLite 3.54.1 and the anchored hooks)

- **Windows.** Each connection opens its own handle on the database (a read-only connection: `GENERIC_READ`, `OPEN_EXISTING`), on `-wal` (always read-write and create, whatever the connection's mode) and on `-shm` (read-write, with a read-only retry), all shared for read and write and none for delete. The anchored hook passes SQLite's access, share mode and disposition through unchanged and refuses nothing about a read-only open (`db/anchored_windows.rs:329-351`). WAL's locks are byte-range locks on `-shm`, taken per connection, so two connections in one process exclude each other as two processes do. Every connection of a session uses the same synthetic path, so they share one shared-memory node. A folder with an open file inside cannot be renamed on Windows; that is true of the session's connection today.
- **macOS and Linux.** The hook resolves the database, `-wal`, `-shm` and `-journal` beneath the held `.slate` descriptor and passes SQLite's flags through with `O_NOFOLLOW | O_CLOEXEC` added (`db/anchored.rs:271-278`). A read-only open is one `openat` with `O_RDONLY`. SQLite's unix VFS keeps one lock record per file per process, so a second connection in the same process shares the first one's POSIX locks correctly and a close of one connection's descriptor is deferred while the other holds locks.
- A read-only connection still opens `-wal` and `-shm` read-write, because every WAL connection takes its read locks in `-shm`. It writes neither the database nor a frame.

### The anchored macOS open (by reading; no lane runs core's tests through it)

Only macOS takes the VFS branch of `open_unconfigured` (`anchored::VFS` is `Some("unix-posix")` there and `None` on Linux and Windows, `db/anchored.rs:55-58`, `db/anchored_windows.rs:65`), and no CI lane runs core's tests on macOS. What is known:

- `unix-posix` has shared-memory methods in SQLite 3.54.1, the same ones the default Linux VFS uses.
- A second WAL connection already runs through that branch on a reader's Mac whenever a session compacts an op log (the compactor's connection). No test runs it there.
- The Linux fact `sqlite_wal_two_connections_through_the_anchored_path` (`session/tests/descriptor_interior.rs:233-285`) opens three connections on an anchored path and asserts `wal`, through the default VFS.
- Nothing in the repository has ever opened a read-only connection, on any platform.
- The Mac lane's Swift tests open many sessions, but none of them would open the read connection: it opens on first use, and the Mac's Quick Open does not use the indexed rank.

So in CB-45b the read connection is opened on macOS by one Swift fact and by no product path. Mac readers first run it in CB-45c, when the tree's and the file inventory's reads move.

### Hot exit and vault switch (by reading)

- The hot-exit store uses no SQLite and no session connection (`hot_exit.rs:13-16`); the session's hot-exit calls read the provider only. The read connection does not meet it.
- A session's `Drop` (`S:1964-1990`) stops the watcher, deletes its change-feed cursor on the main connection, joins the compactor, and then its fields drop in declaration order: `conn` first, `worker_conn` next, the held `.slate` (`interior`) last, so every connection closes while its `-wal` still resolves. `Drop` has the session exclusively, and the FFI object's call counter defers it past any call in flight, so no read is running when it starts.
- On Windows, closing a vault drains the rescan's core calls before it disposes Quick Open (`W/VaultLifecycleViewModel.cs`, `CloseSession`), so the close path does not change.

### Not measured

- Anything on macOS.
- FAT32, exFAT and a real network share.
- A second process on the same cache while the second connection reads (the existing unix fact pins WAL snapshot isolation across processes).
- A vault larger than 10,000 notes.
- The Windows app: no windowed run was made. What the host does is from reading.
- The design's own code: the read connection as section 1 configures it, and the read function of section 4, do not exist yet.

## Design

### 1. One read-only connection per session

`VaultSession` gains one field, the read seat: a mutex over the read connection, or over the fact that there is none yet or none for this session. The read connection is a connection to the session's own cache, by the session's own anchored path (`interior.sqlite_path("cache.sqlite")`), opened with `SQLITE_OPEN_READONLY`.

- **One read connection, not a pool.** At 10,000 notes the reads measured hold a warm second connection for 22 ms or less. Quick Open's copy is 4 to 8 ms; its scoring runs after the seat is released, as it runs after the lock today. A second read connection is a later phase's call, on that phase's own measurement.
- **Opened on first use, not at session open (ruling 5).** `slate-cli` and most of core's test sessions never rank; they pay nothing. A read that arrives during a scan opens it then (1.3 ms measured). What this leaves unrun on macOS in CB-45b: no Mac product path opens the read connection (the Mac's Quick Open does not call the indexed rank), and no CI lane runs core's suite on macOS, so the read connection's open through `unix-posix`, its two checks and one rank during a held scan run there in one Swift fact only (CB-45b's fact 14). The fallback paths, the release of the snapshot, the close order and every other core fact of CB-45b run on Linux and Windows only. An open at session open would have run the open and the two checks in each of the 107 Swift test files that open a session (by search: 100 call `.openVault(at:`, 14 call `VaultSession.openFilesystem(`), and put a connection that nothing reads into every Mac session. Mac readers first run the read connection in CB-45c.
- **`SQLITE_OPEN_READONLY`, not `query_only` on a read-write connection.** SQLite opens the database file itself read-only, so no statement and no pragma can make this connection write the cache; and it can still hold a TEMP table, which two later candidates (the change feed's window, a Bases full-text filter) would need if they ever moved. The cost is that it cannot checkpoint, so it must not be the last connection to close (section 7).
- **`open_reader` checks what it opened**: the connection must answer read-only for its main database (`is_readonly`) and `wal` for its journal mode. Either answer wrong means no read connection for this session. The first check is what holds the read-only flag on the macOS branch of the open, which no core test runs.
- **Configured as the main connection is, in the two settings that matter to a read**: `temp_store = MEMORY`, so a sort never spills note paths to the system temp folder, and a page cache of 512 pages (2 MB, what was measured) or the session's `max_db_cache_pages` if that is smaller. A mobile host would have to budget for it: 512 pages is the whole of the mobile configuration's cache today. No mobile host exists.
- **A busy timeout of 250 ms** (the figure `Drop` already uses for its one write). A WAL read connection waits for no writer in ordinary running. The timeout bounds a busy answer; it does not bound SQLite's own retry loop when another process is recovering the log (see Risks).

### 2. How it opens: through `db.rs`'s one configure function

- `open_connection` gains one parameter, the access a caller wants: read-write and create, as today, or read-only. It passes it to its single `open_unconfigured` call, and both of `open_unconfigured`'s existing open lines take the flags. `configure_connection` is unchanged and is still the only way out of `Unconfigured`.
- A new `db::open_reader(path)` calls `open_connection(path, read-only)`, registers core's functions (`register_connection_functions`), applies the settings of section 1, and makes the two checks.
- This is the one shape the text census passes without being edited (`db/connection_census.rs`): still exactly three `Connection::open*` lines, `open_unconfigured` still has one caller, `configure_connection` still has two. The case the census plants to trip itself is a function named `open_reader` that calls `open_unconfigured` itself; this design's `open_reader` calls `open_connection`. The census file is not edited.
- `open_connection` has 14 other call sites in 6 files, which take the new parameter: `D:262`; `S:1631, 4980, 9038`; `tasks_db.rs:113, 191`; `db/anchored.rs:676, 702` (unix-only tests); `db/anchored_windows.rs:720, 729, 747, 851, 877` (Windows-only tests); `session/tests/two_sessions.rs:522`.
- The connections fact, `every_connection_core_opens_is_configured` (`session/tests/two_sessions.rs:496-551`), gains a fifth row: the session's read connection, reached through a test seam beside the compactor's. Its doc comment's count moves from four kinds to five.
- `db_cache_status` gains the read connection's counters beside `main` and `worker`, and keeps its rule of holding one lock at a time. It takes its two locks today with `.expect` (`S:2256-2268`); the read seat's it takes as section 3 recovers a poisoned seat, so a panic in a read never makes the status call panic.

### 3. When there is no read connection, and when one fails

**None for this session.** The cache does not read back `wal`, or the opened connection is not read-only. Every read runs on the main connection for the life of the session.

**The open fails.** That read runs on the main connection. The next read tries the open again, no sooner than one second after the failed attempt, so a hold by an antivirus or a sync client during a busy moment costs one slow read and not a session.

**A read on it fails.** What counts as a failure is decided by where the error came from, after a cancelled run has been mapped to `Cancelled`:

- an error SQLite raised on the read connection (beginning or ending the read transaction, preparing or stepping a statement) that is `SQLITE_BUSY` or `SQLITE_LOCKED`: this read runs again on the main connection and the read connection is kept;
- `SQLITE_READONLY`, in any of its extended codes: a read sent a write to the read connection. That is a defect in the read, not in the connection, so the error passes through untouched, the read connection is kept, and nothing runs again on the main connection, where the write would succeed;
- any other error SQLite raised on the read connection: the read connection is closed, this read runs again on the main connection, and the open is tried again under the one-second rule;
- every other error is the read's own answer (a cancellation, an invalid argument, a refused cursor) and passes through untouched. The read connection is kept and nothing runs again.

Whether a read may run again on the main connection is stated per read. Quick Open's may: it has no side effect and no cursor. CB-45c states it for cursor pages.

**The seat's mutex is poisoned** (a panic inside a read). It is recovered, the connection inside is closed, and the open is tried again under the one-second rule. A rank never fails because an earlier one panicked.

**A reopened read connection is a new connection.** Its `PRAGMA data_version` starts again from its own first read, and a read-only connection's `total_changes` is always 0, so a value read on the old connection can equal one read on the new connection across real commits (the red team's experiment on SQLite 3.50.4: `(4, 0)` before a close and `(4, 0)` again after the reopen, with two more rows committed between). Each open of the read connection therefore takes a new generation number from the session. CB-45b compares nothing across reads; CB-45c binds the generation into every cursor its reads issue (its row below).

Each of these logs at `warn` with no path (the #507 privacy rule) once per change of state: the first failed open after a working one, the first working open after a failure, and each close for an error. The detail goes to `debug` every time. An open that is refused on every attempt is retried once a second for the session's life and logs once. None is an error a reader meets: the read is slower, as it is today.

### 4. How a read runs

One private function on the session takes the token and the read. No product read touches the read seat any other way. Two other callers reach the seat and run no read: `db_cache_status`, which reads its counters (section 2), and the test seams the facts use.

1. Check the token.
2. Wait for the seat's mutex with a cancellable wait like `lock_cancellable`'s (`session/trash_cancellation.rs:16-37`: `try_lock` every 20 ms, the token checked each time), so a keystroke that has been superseded stops waiting within about 20 ms.
3. Open the read connection if there is none yet and the rules of section 3 allow an attempt.
4. Register the progress handler (`with_sqlite_cancellation`: per connection, every 1,000 VM steps), begin a read transaction, run the read, end the transaction, clear the handler, release the seat. The function owns the handler; a read passed to it does not register one of its own, because a connection has one handler slot. Every read on the read connection is one snapshot, whether it is one statement or five.
5. With no read connection, or after a failure that section 3 sends to the main connection: release the seat first, then wait for the main connection's mutex, also cancellably, and run the read there under the progress handler registered on the main connection, as today's rank does (`S:6966`), so a read that falls back can still be cancelled inside its statement.

Two rules, each held by a fact:

- **The read connection has no transaction open and no statement in progress when the function returns**, on every path out, an error and a cancellation included. A snapshot left open is what stops checkpoints and grows the log (measured above).
- **The seat and the main mutex are never held together.** The function releases the seat before it waits for `conn`, and nothing takes the seat while holding `conn`. A read that falls back while a scan runs waits for the scan alone; the next read still gets the read connection.

Today the wait for the main mutex cannot be cancelled. In step 5 it can. That is the one change to a read that falls back, and it is a departure from CD34's "No interim" (ruling 4): CD34 ruled out a change to today's path on its own ahead of the design. This one ships in the same function and the same PR as the read connection, so no build of Slate has the cancellable wait without the read connection. It is half of CB-45b's fact 9.

### 5. What a read on the read connection can see

It sees a committed state of the cache: never a torn row, never half of a transaction. It can see two things a read waiting on the mutex cannot: the index as it stood before a scan, for as long as the scan's transaction is open, and the states between a change on disk and the commit that records it. The first changes what a screen-reader user hears. And it sees nothing of the session's in-memory state.

**The index as it stood before a scan, until the scan commits.** Quick Open during a rescan lists the vault as of the last commit. Until the scan's walk commits, that is the index from before the scan; from the commit on, through the maintenance that follows, it is the scan's result, as a read waiting on the mutex would see at the end. A note renamed outside Slate shows under its old name until the scan commits; choosing it then fails to open and says so, as choosing a row for a file deleted outside Slate does today before the next rescan. On Windows the run that owns the scan then re-ranks an open Quick Open (`W/VaultLifecycleViewModel.ChangeFeed.cs:213-227, 534-556`; contract R-9), so the rows correct themselves when the scan's changes arrive.

**The re-rank keeps the selection only when its path survived.** `ApplyRanked` (`W/QuickSwitcherViewModel.cs:527-545`) looks for the selected path among the new rows and keeps it there. When the scan removed or renamed that note, the selection goes to the first row, set past the property's setter (`:110-124`), so no `RowSelected` sentence is spoken. A reader who arrowed to a pre-scan row during the scan, and heard its name, and presses Enter after the re-rank opens the first row's note. Today that needs a Quick Open left open across a rescan with the selected note changed by it; with the read connection it covers every arrow move made while the scan runs, because today a query typed during the scan has no rows to arrow through until the scan ends. R-9 (b) promises "the keys kept on the reader's result" and nothing more.

**So CB-45b names a moved selection (Call 9, the owner's answer "Speak new selection (Recommended)", and the lead's ruling under it).** The rule, for every re-rank Quick Open runs that the reader's keystroke did not start (R-9's silent re-rank, and the re-rank after a Slate-owned write):

- The selected path is read before the rows change. When it is among the new rows, it stays selected and nothing more is said.
- When it is not, the first row is selected and named with the sentence an arrow move speaks (`RowSelected`, `:110-124`; no new wording). When no row is left, nothing is selected and nothing more is said, and Enter opens nothing.
- A re-rank that runs as a keystroke's (a new query, or the pending-keystroke exception below) is unchanged: the first row is selected and only the count is spoken, because the reader's selection belonged to the query they replaced.
- R-9's re-rank stays silent about its count. The re-rank after a Slate-owned write still speaks its count, and names a moved selection after it.

CB-45b records the rule in an addendum after R-9 in doc 40, which it appends with the AR row:

> *R-9 addendum (plan 47 CB-45b, #1718; the owner's answer of 2026-11-22 to CB-45's Call 9, "Speak new selection (Recommended)", and the plan lead's ruling of the same day for the re-rank after a Slate-owned write):* (b)'s keys kept on the reader's result say where they went when the result is gone. When Quick Open's silent re-rank cannot keep the selected path, it selects the first row and speaks it with the sentence an arrow move speaks; with no row left it selects and says nothing. The re-rank after a Slate-owned write keeps a surviving selection in the same way, speaks its count as before, and names a moved selection after the count. A keystroke's rank is unchanged. The frozen R-9 row is unchanged.

**Which count a screen-reader user hears changes.** No sentence is added and none is reworded. Apart from the named selection above, what changes is which count is spoken, and when. Quick Open speaks core's `QuickSwitcherCount` sentence (`crates/slate-core/src/a11y.rs:2474-2481`): `12 files matching "note"`, `No files matching "x"`, or, with no query, `5 recent files`.

- Today, a query typed during a rescan is answered when the scan ends, from the scan's result, and that count is spoken. The keystroke's rank is still pending when the run re-ranks, and a re-rank that supersedes a pending keystroke's rank runs as that keystroke's, count spoken (`W/QuickSwitcherViewModel.cs:433-435`, pinned by `QuickSwitcherViewModelTests.cs:261-318`).
- With the read connection, the same query is answered at once from the pre-scan index, and that count is spoken (`No files matching "x"` for a note the scan is about to add). Opening Quick Open during the scan is answered the same way: its recent-files count is checked against the pre-scan index (`:175-183`). When the scan's changes arrive, the keystroke's rank has long published, so the host's re-rank is silent by R-9's rule (`:409-454, 546-551`): the rows change and nothing is said. The exception above still holds for a keystroke whose rank has not yet published when the re-rank comes: one typed within the time a rank takes from keystroke to rows of the scan's end (its 60 ms debounce, `:67`, the wait for the rank lane, `:465-477`, the rank itself, about 50 ms measured, and the post back to the UI thread, `:478-489`). That keystroke hears the post-scan count, as today.
- That second sequence already exists. A reader who types a query a moment before a rescan starts hears the pre-scan count and then gets the silent replacement. The read connection makes it the case for a query typed, or a Quick Open opened, during the scan as well.

What follows the silent re-rank depends on what started the scan (`W/VaultLifecycleViewModel.Rescan.cs:530-541, 1132-1156`):

- **Refresh** always speaks its scan's sentence, and R-9 awaits the re-rank's publication before it, so the rows change first and the sentence follows. Refresh cannot be started while Quick Open is open: the menu and the workspace are disabled under Quick Open's scrim (`W/MainWindow.xaml:174-178, 1143-1150`). So this sequence is a Refresh started first and Quick Open opened while it runs.
- **A foreground run** (the window back after two seconds away) starts only when no modal surface is open (`W/ForegroundRescanRoute.cs:88-95`), so Quick Open is opened after it starts. It speaks its sentence only when it found changes.
- **A watcher run** (a change made outside Slate) speaks no sentence while a modal surface is open, and Quick Open is one (`W/ModalSurfaces.cs:38-40`, `W/MainWindow.xaml.cs:185-191`); otherwise at most one a minute (AR-75). So a watcher run that corrects an open Quick Open speaks no count and no sentence: on head the reader hears the pre-scan count and then silence, where today a query typed during that run hears the post-scan count when the run ends (the pending-keystroke exception above). A query whose rank published before a watcher run began is corrected in the same silence today. The one thing a watcher run's re-rank speaks on head is Call 9's: when it removed the selected row and another row is left, that row is named.

**The owner accepted the counts on 2026-11-22: "Accept, record it (Recommended)" for Call 2 and "Accept (Recommended)" for Call 10** (rulings 2 and 10). R-9 is frozen and does not change. CB-45b appends this row to `docs/plans/40_nvda_matrix_remediation_contracts.md`'s accepted risks, under the next free AR number when it lands (AR-83 is the last at 3f2fd733):

> **AR-nn (plan 47 CB-45b, #1718; the owner's answer of 2026-11-22 to CB-45's Call 2, "Accept, record it (Recommended)"):** during a rescan, Quick Open on Windows ranks against the index as of the last commit, through core's read connection (CB-45's design); until the scan commits, that is the index as it stood before the scan. Opening Quick Open or typing a query while the scan runs speaks that count at once. When the scan's changes arrive, R-9's re-rank replaces the rows silently and the corrected count is not spoken (a keystroke whose rank is still pending at that moment hears the post-scan count, as before). After a Refresh the scan's sentence follows; after a watcher run, which posts no sentence while Quick Open is open, the reader hears no further count or sentence (a removed selection is still named, by the addendum after R-9). The same sequence already held for a query typed just before a rescan. During a Slate-owned write, a query typed while the write holds the session is answered from the state before the write and speaks that count, and the write's own re-rank then speaks the corrected count, two counts where one was spoken before (the owner's answer of 2026-11-22 to CB-45's Call 10, "Accept (Recommended)"). The frozen R-9 row is unchanged.

The row and the R-9 addendum above are appended by CB-45b, with the behaviour it records, and not by this documents-only PR: doc 40 is one of the paths the Windows lane's census tier reads (`windows.yml`, `census-paths`), and this PR edits no file a lane lists.

**The later phases bring the same price to other surfaces.** The owner's answer covers Quick Open only. After CB-45c and CB-45d, a tree page, a tag tree page, a search and a sidebar filter run during a rescan answer from the index as of the last commit too. R-9's run re-syncs the tree, Quick Open, the reading view, history, Bases and the graph (`W/VaultLifecycleViewModel.ChangeFeed.cs:15-23`), each differently, by reading:

- **The tree and the tag tree** correct themselves silently, as Quick Open does: the run refreshes the sidebar silently (`ReSyncTreeAsync`, `:584`; `RefreshForRescanAsync`, `W/VaultLifecycleViewModel.Rescan.cs:637-639`).
- **The sidebar filter** corrects itself and can speak. The tree's publication re-runs an active filter (`ScheduleFilter(automatic: true)`, `W/FilesSidebarViewModel.TreeOperations.cs:640`), and the re-run speaks `FileListCount` again when its total differs from the count last spoken for that query and tag, unless a mutation's status is being reasserted (`W/FilesSidebarViewModel.Filter.cs:497-540`). So a filter typed during a rescan would speak the pre-scan count at once and, when the scan's changes arrive, the corrected count, where today it speaks once, after the scan.
- **Search results** are not re-synced: an open search's rows and its spoken count (`SearchResultsSummary`, `W/Search/SearchOverlayViewModel.cs:868-872`) stay as the pre-scan index answered until the reader runs the search again.

Each phase's spec reads its surfaces against this section on its own step 0, puts each spoken output that differs from today to the owner, appends an AR row for each one the owner accepts, and runs an NVDA spot check of it.

**The states between a change on disk and the commit that records it.** The mutex is held across these by the flows below. A read waiting on the mutex sees only the end; a read on the read connection can see each.

| Flow | What a read on the read connection can see |
|---|---|
| A scan (`S:3619-3741`): up to five commits | After a parser bump, a marker row is committed before the walk begins (`S:3646-3660`). `files`, `dirs`, tags and full text then change in the walk's one commit. The maintenance commits after it touch only op-log bindings and events |
| A save, a create, a tag or property edit, a task toggle | `files` and its derived rows change in one commit. Only the write-intent row is committed earlier |
| A single rename or move (`S:18163-18327`) | After the file has moved and before the index commit: the old path. After it: the final paths, while other notes' link text is still being rewritten one save at a time |
| A batch move (`S:14514-14893`) | The same two, for every item at once. If a later step fails, the batch reverses the index in another commit (`S:15189-15202`): a read can have returned destination paths that are then taken back |
| A delete of a file or a folder (`S:17290-17420`, `S:17442-17562`) | The item is sent to the system Trash inside the index transaction: until the commit, a row for a file that is gone |
| A batch trash (`S:16160-16626`) | Items already sent to the Trash stay listed until the one index commit that removes them all |

For Quick Open each of these is a row that names a path the vault no longer has, for at most the length of one operation. It is the same class as a file changed outside Slate between rescans, which Quick Open has always been able to list: choosing the row fails to open and says so. No state was found in which a row opens a different file than it names.

How long the row stays on Windows, by reading: a Slate-owned write that completes sends a file-change event, and the host re-ranks an open Quick Open on it (`W/VaultLifecycleViewModel.FileChanges.cs:65`, `W/QuickSwitcherViewModel.cs:255-267`); a rank that began before the commit cannot publish over one that began after it, because the host publishes only its newest rank generation (`:478-489`). One path sends no event: a batch move that is rolled back entirely publishes nothing (`S:15368-15388`), so rows naming its destinations stay in an open Quick Open until the next keystroke or the next rescan. It takes a database failure in the middle of a batch move while Quick Open is open on a matching query.

**The re-rank after a Slate-owned write is not silent.** Unlike R-9's, the event's re-rank clears the rows and the selection, ranks again, selects the first row past the setter, so without naming it, and speaks the count (`W/QuickSwitcherViewModel.cs:444-450, 541-551`). That is today's behaviour whenever a Slate-owned write lands while Quick Open is open (a background save of a note edited just before Quick Open opened, for one), and a selection that survived the write is dropped too; the read connection does not change it. What the read connection adds: a keystroke ranked while a write holds the main connection's mutex is answered at once from the state before the write and speaks that count, and the write's event then speaks the corrected count, where today the keystroke waits for the write and one count is spoken. The window is the write's hold on the mutex; step 0 measured a save through the session at 38 ms on average. The owner accepted the two counts (Call 10, in the AR row above). The dropped selection is answered by Call 9's rule above: the write's re-rank keeps a surviving selection and names a moved one.

**Nothing of the session's in-memory state.** Quick Open's read touches none. The graph index, the Bases generation and the change feed's origin cell are ordered with commits only by the main connection's mutex; reads that use them stay there (next section).

### 6. What does not move, and why

Every read and every write stays on the main connection unless a phase below names it. The ones a later reader of this design might expect to move:

| Stays on the main connection | Reason |
|---|---|
| Every write | One writer |
| `note_tasks`, `tasks_for_file`, `tasks_in_vault` | Each runs the abandoned-intent repair first, which writes (`S:7990-8003`, `S:10458-10466`); doc 21's rule is that a task read sweeps first and reads one snapshot. These are the only reads doc 21's containment (INV4) governs |
| `open_base`, `open_canvas` | Each may reindex its file (`S:19946-19992`, `S:22464-22544`) |
| `change_feed_window`, `change_feed_page`, `acknowledge_change_feed`, `register_change_feed` | The window is a TEMP table on the connection that made it, a gap writes, and the seat's state is ordered by the main mutex |
| Every graph query | The index is built from SQLite once and then kept by replaying commits while the main mutex is held (`S:2124-2154`); a build on another connection could interleave with a commit and its replay |
| `base_execute`, `base_export` | It samples the Bases generation and caches by it under the main mutex |
| The note panels, link lists, properties, history, citations, embeds, canvas, file metadata and content-hash reads | Not named by #1718. Most are plain reads and could move through the same function; no phase in this plan moves them (ruling 1) |
| A read whose answer outlives the display: copied, written into a note, or used to name a target | A display is replaced by the rescan's re-sync; a copied or written answer is not. Such a read moves only with a rule, proved on its phase's step 0, that its answer stays right whichever side of a pending commit it was formed on. Of the reads the phases name, only `wikilink_for_path` is one (CB-45c's row) |

### 7. Closing

- The read seat is declared before `conn` in `VaultSession`, so the read connection closes first, and the main connection or the compactor's still closes last, checkpoints, and removes `-wal` and `-shm`, as today.
- `Drop` has the session exclusively, so no read is in flight on the read connection when it closes.
- A vault switch drops the session; the read connection's handles go with it.

### 8. Checkpoints

No checkpoint policy changes. The main connection's commits run SQLite's automatic checkpoint as today. A read on the read connection lasts milliseconds; a checkpoint that meets one copies up to that read's mark and a later commit's checkpoint finishes. The first rule of section 4 is what keeps the log from being pinned.

### 9. Facts and guards the phases must meet

- `every_connection_core_opens_is_configured` gains the read connection (CB-45b).
- The connection census passes without an edit (CB-45b).
- `no_statement_is_re_prepared_per_binding_on_any_driven_path` (`session/tests/statement_class.rs:494`) counts statements on the main connection only, through `counting_statements` (`session/tests/common.rs:655-720`). Quick Open's statement is not one of its flows, so CB-45b does not touch it. The tag tree, the lists and search are, so CB-45c and CB-45d teach the helper to count on the read connection too, or those flows read zero.
- The two-sessions cache facts read the main connection's counters only and time rescans, which stay on the main connection; they do not move.
- A directory or tag tree cursor is bound to the connection that issued it: its snapshot is that connection's `data_version` and `total_changes`, with the session's nonce (`session/directory_page.rs:50-63, 86-131`). On the read connection `total_changes` is always 0 and `data_version` starts again at each open, so the snapshot alone cannot tell two opens of the read connection apart (section 3). CB-45c is where that matters; see its row.
- Three existing facts hold the main connection's mutex or commit on another connection and expect a tree or tag tree page to wait or be refused, so CB-45c changes what each means: `tag_tree_page_cancelled_while_waiting_for_a_writer_is_cancelled_not_stale` (`session/tests/files.rs:929-959`), `tag_tree_page_refuses_a_page_another_connection_committed_under` (`session/tests/files.rs:897`) and `in_flight_external_commit_fails_the_whole_page` (`session/tests/directory_page.rs:283`). Inside one read transaction `data_version` does not move (the red team's experiment), so the last two would fail as written once their pages run on the read connection.

## The phases

Each phase is one PR, with a spec, a red team, Codex and a merge judge like every other chunk. Each fact is seen red first: on main where main can show it, and otherwise by the named mutation of the finished code.

### CB-45b: Quick Open's rank does not wait behind a scan

Size M (the reading's own estimate for "Quick Open first", `reading/07_core.md:330`). Lanes: rust (the change and its facts), windows (the lane runs in full on a `crates/` change; one twin fact; Call 9's host change in `QuickSwitcherViewModel` and its facts; an NVDA spot check), swift (one fact; no product edit), and the runner lane (`windows-runner-tests.yml` runs when `windows.yml` or `ci/windows-runner/**` changes, `:15-19`; fact 12 edits both). No FFI signature changes and `crates/slate-uniffi/src/lib.rs` is not edited (ruling 3).

Builds sections 1 to 4, 7 and 9 in `db.rs` and `session.rs`; `switcher_rank_top_indexed` reads through the new function. On Windows it builds Call 9's rule (section 5) in `W/QuickSwitcherViewModel.cs`: `ApplyFileChanges`'s re-rank reads the selected path before it clears the rows, and `ApplyRanked` keeps a surviving path and names a moved selection for the two re-ranks the reader's keystroke did not start.

One test seam serves several facts: it reports which connection served each read (the read connection, or the main connection after a fallback, with the reason). Without it a fallback that answers correctly hides a read connection that misbehaves: a read transaction left open makes the next `BEGIN` fail, section 3 sends the rank to the main connection, and the main connection's answer is right (the red team's experiment).

Facts:

1. `a_quick_open_rank_answers_while_a_scan_holds_the_writer`: the session's first rank, so it opens the read connection while the scan is parked at its first file (`scan_point_test_hook`); the rank on another thread returns the pre-scan rows inside a deadline, before the scan is released. Red on main: the deadline passes.
2. The same rank with the scan parked at `"maintenance"`, after its commit and still holding the mutex, returns the scan's rows. Red on main the same way.
3. A rank after a save sees the save, served by the read connection: the test holds the main connection's mutex while it ranks, so no fallback can answer, and the seam reports the read connection. Mutation: the read transaction not ended, so the read connection stays on its old snapshot.
4. After a rank, after a rank cancelled inside its statement and after a rank cancelled between rows, the read connection is in autocommit with no statement in progress, and a TRUNCATE checkpoint from the main connection is not busy and empties the log. Mutation: the function returns without ending the transaction. (Autocommit alone does not prove release; the checkpoint does.)
5. A write through the read connection is refused with `SQLITE_READONLY`, the connection answers read-only, the error passes through, the read connection is kept and nothing runs on the main connection (the seam). Mutations: the open made with the default flags; `SQLITE_READONLY` classed as a connection failure.
6. The read connection joins the connections fact, and `db_cache_status` reports it, after a panic inside a read as well. On the read connection the fact also reads `temp_store` (MEMORY), the page cache's size and the busy timeout, and prepares a statement that names `slate_tree_sort_key`, so the registration of core's functions is held from CB-45b on. Mutations: the configuration removed from `open_connection` (the existing mutation of that fact); the function registration removed from `open_reader`.
7. A cache that does not read back `wal` gets no read connection for the session, and the rank answers through the main connection (a test seam stands in for the mode read; the reporting seam shows the main connection, with no open attempted on later ranks). Mutation: the answer ignored.
8. Failures, each through a seam: an open that fails costs one slow rank and the open is tried again after the spacing; a SQLite error during a read closes the read connection and the rank still answers, and the next open has a new generation; a busy answer costs one fallback, keeps the read connection, and does not hold the seat (a second rank answers from the read connection while the first waits on a held main mutex); a cancellation keeps the read connection; a poisoned seat recovers. Mutations: retire on a cancellation; hold the seat across the fallback; return the poison error.
9. Cancellation in every wait and in the fallback's statement: a rank waiting for the seat's mutex returns `Cancelled` inside a bound once its token is cancelled (mutation: a plain `lock()`); so does a rank waiting for the main mutex in the fallback; and a fallback rank cancelled inside its statement returns `Cancelled` (mutation: the fallback run without the progress handler). The second part needs a seam that forces the fallback, which main lacks; its red run on main is the same assertions without the seam call, where the rank waits on the main mutex and cannot be cancelled.
10. A session that ranked and is then dropped, its compactor never having opened a connection, leaves no `-wal` and no `-shm`. Mutation: the seat declared after `conn`.
11. On unix, the vault folder is renamed and the first rank after it still opens the read connection, through the held `.slate`. It runs on the Linux lane, through the default VFS, not through `unix-posix`.
12. In the weak-volume job: on FAT32 and on exFAT the fact asserts one of two outcomes. Either the cache reads `wal` and the rank answers while a scan is parked, or it does not and the session has no read connection and the rank answers after the scan; it prints which. It lives in `session/tests/held_root_windows.rs` beside the job's other session facts, gated as they are, and its name does not contain `weak_volume`: the job's provider step runs every ignored fact whose name matches `weak_volume` and fails when their count is not six, and its session step selects facts from the `held_root_windows` module by a list of names (`.github/workflows/windows.yml:654-693`). So the session step's list gains the name, and `ci/windows-runner/tests/Workflows.Tests.ps1` (`:158-185`), which pins all three of that step's facts by name, pins the fourth too. Red first: the `wal` branch is red on main where the volume reads `wal` (the deadline passes); the other branch is seen red only by fact 7's mutation.
13. Windows twin, `AQuickOpenRankAnswersWhileAScanHoldsTheWriter`: a real rescan held inside a `ScanProgressListener` callback (core calls it on the scan's thread with the mutex held and the transaction open; the listener's contract says not to block, and this test blocks on purpose); a `QuickSwitcherViewModel` on the same session publishes rows while it is held. Red on main: its deadline passes.
14. Swift twin, `testAQuickOpenRankAnswersWhileAScanHoldsTheWriter`: the same hold; `switcherRankTopIndexed` through the session returns while it is held. This is the fact that runs the read connection through `unix-posix`, and in CB-45b the only thing that does (ruling 5). Seen red the way AGENTS.md prescribes for a Windows test the machine cannot run, used here by analogy for the Mac lane: a throwaway draft PR with the rank routed back to the main connection, the Mac lane shown red on this fact, the probe deleted.
15. Windows, Call 9, on `QuickSwitcherViewModel` with a fake rank (no read connection needed, so each that changes behaviour is red on main's host code):
    - `ASilentReRankThatLosesTheSelectedRowSpeaksTheNewSelection`: row B selected; a silent re-rank whose rows lack B selects the first row and announces `RowSelected` for it, and no count. Red on main: nothing is announced.
    - `ASilentReRankThatLeavesNoRowsSelectsAndSaysNothing`: the same with no rows; nothing is selected and nothing is announced. Green on main, which already selects nothing and says nothing here (`W/QuickSwitcherViewModel.cs:541-551`); seen red by the mutation that keeps the removed selection when no row is left.
    - `AFileChangeReRankKeepsASurvivingSelection`: row C selected; `ApplyFileChanges` and a re-rank whose rows hold C; C stays selected, the count is announced, and no `RowSelected`. Red on main: the first row is selected.
    - `AFileChangeReRankThatLosesTheSelectedRowNamesTheNewOne`: the same with C gone; the count, then `RowSelected` for the first row. Red on main: the count alone.
    - `AKeystrokeRankStillSelectsTheFirstRowAndSpeaksOnlyTheCount`: a new query after a selection; the first row is selected and only the count is announced. Green on main and head; the mutation that names every moved selection turns it red.
    Mutations: the selected path read after the rows are cleared; the name spoken for a silent re-rank only.
16. Call 10's two counts, held on `QuickSwitcherViewModel` with a fake rank: a keystroke's rank publishes and its count is announced, then `ApplyFileChanges` re-ranks and a second count is announced. It pins accepted behaviour and is green on main's host code, as today's view model already speaks both when the keystroke's rank publishes before the event; it is seen red by the mutation that makes the event's re-rank silent. The core half, a rank during a held write answered from the read connection, is fact 3's shape.

Existing facts that must stay green unchanged: `switcher_rank_top_indexed_cancelled_returns_cancelled` (`session/tests/files.rs:979-1011`) and the four in `crates/slate-core/tests/switcher_indexed_exec.rs`.

NVDA spot check, on base and on head, the phrases recorded. A vault large enough to type during the scan (10,000 notes, most of them touched outside Slate, so the scan runs for seconds), with notes the query matches added, removed and renamed outside Slate. Section 5 says which run speaks what. Refresh cannot be started while Quick Open is open, so a Refresh sequence starts Refresh first and opens Quick Open while it runs; a sequence with Quick Open already open is started by a change outside Slate, which the watcher turns into a run. Each count to be checked is taken after the keystroke's rank has published, so the pending-keystroke exception does not mask it. Four sequences:

1. Refresh, then Quick Open opened and a query typed while the scan runs.
2. As 1, with an arrow move to a row the scan renames, made while the scan runs, then Enter after the scan ends (Call 9).
3. Quick Open open with a query whose count has been spoken, then a change outside Slate (a watcher run).
4. A change outside Slate, then Quick Open opened and a query typed while the watcher run's scan runs.

Expected, by reading:

- Sequence 1. Base: the post-scan count when the scan ends, then the Refresh sentence. Head, by the owner's answer: the pre-scan count (`N files matching "q"`) at once; when the scan ends the rows change with no count spoken, then the Refresh sentence.
- Sequence 2. Base: no row can be arrowed to during the scan. Head: the arrow move is spoken; when the scan ends and the renamed row is gone, the first row is named (the `RowSelected` sentence), then the Refresh sentence; Enter opens the note just named.
- Sequence 3. Base: the rows change and nothing is spoken, since a watcher run posts no sentence while Quick Open is open. Head: the same while the selected row survives; when the change outside Slate removes the selected row and another is left, that row is named (the `RowSelected` sentence). Run it both ways.
- Sequence 4. Base: the post-scan count when the run ends, and no sentence. Head: the pre-scan count at once, then nothing (a row selected meanwhile and removed by the run is named, as in sequence 3).

A Slate-owned write cannot be timed by hand against a keystroke (Call 10's window is a write's few tens of milliseconds), and Quick Open covers the editor, so the write's re-rank (Call 9's second branch) and Call 10's two counts are held by facts 15 and 16 on `QuickSwitcherViewModel` rather than by the spot check. The phrases go in CB-45b's PR beside the AR row and the addendum.

Registers: the AR row and the addendum after R-9 of section 5, appended to doc 40 (R-9 itself is frozen). No parity matrix change (no command, chord or setting is added). The trigger ledger changes only if the host change constructs `RowSelected` outside the `SelectedRow` setter; then the ledger is regenerated. A #1954 line: when the Mac adopts the indexed rank and re-ranks an open sheet (#1652), it names a moved selection the same way.

Files: `crates/slate-core/src/db.rs`, `session.rs`, the 14 call sites of section 2, `session/tests/` (the new facts, `held_root_windows.rs` and `two_sessions.rs`), `.github/workflows/windows.yml`, `ci/windows-runner/tests/Workflows.Tests.ps1`, `W/QuickSwitcherViewModel.cs`, the Windows test files for facts 13, 15 and 16, one Swift test file, doc 40.

Closes: Quick Open's wait on Windows. `Refs #1718`.

### CB-45c: the files tree, the directory pages and the tag tree do not wait behind a scan

Size M to L. Lanes: rust, windows, swift.

Moves: `list_dir_children_page`, `list_dir_files_page`, `list_dir_children`, `list_files`, `get_file_summary`, `tag_tree_page`, `tag_tree`, `list_tags`, `tags_for_files`, `wikilink_for_path`.

Its spec settles, on its own step 0:

- **The cursor rule.** A cursor carries the snapshot of the connection that issued it. A page walk must stay on one connection, and a cursor from the read connection offered to the main connection after a fallback must be refused as stale, never compared by accident. (In practice that case is refused already, because the main connection's `total_changes` is not zero; the rule makes it a guarantee.) A cursor from one open of the read connection offered to the next open must be refused too: section 3's generation number goes into the cursor's snapshot, because `data_version` and `total_changes` alone can match across a reopen. A fact walks a page, forces a reopen through a seam with commits between, and asks for the next page: refused. The read connection's view also changes at every commit by another connection, a scan's maintenance commits included (one to four changes in one scan were measured), so a walk that runs during a scan's tail can be refused more than once; the Windows tag tree restarts a refused walk at most three times (`W/FilesSidebarViewModel.cs:1497-1539`). Its step 0 uses a vault with op-log history.
- A refused cursor is the read's own answer under section 3: it keeps the read connection. A fact holds that.
- The three existing facts section 9 names are rewritten for a read connection whose snapshot is fixed for one read transaction.
- The helper for the statement-class fact counts on the read connection.
- **What a reader meets** (section 5's last paragraph): the tree and the tag tree answer from the index as of the last commit during a rescan and are corrected silently by R-9's re-sync. Its spec reads what each surface speaks during and after the scan, puts each stale spoken output to the owner, appends an AR row for each one the owner accepts, and records it in an NVDA spot check.
- This is the phase in which Mac readers first run the read connection (the tree and the file inventory). It owes Swift facts for those reads during a held scan, and the owner's Mac check is on #1954 (comment 7075132728). The VoiceOver check the plan lists for #1718 moves here (see "For the plan").
- **A cache replaced under a live session** (a risk below) reaches Mac and Linux readers from this phase on; its spec decides whether the read connection's open checks that it reached the main connection's file.
- **`wikilink_for_path` moves only if its link still names the same note after the scan.** Every other read this phase moves feeds a display that the rescan's re-sync replaces. This one feeds the clipboard: it builds an index of every path and returns the shortest link that resolves to the note (`S:7010-7036`), and the reader pastes that link into a note, where no re-sync reaches it. Answered from the pre-scan index during a rescan, it can be wrong after the commit. With `Z/Target.md` indexed and a sync client adding `A/Target.md`, the pre-scan answer is `[[Target]]`, which after the commit resolves by folder distance and then path to `A/Target.md` from a note at the root (`crates/slate-core/src/link_resolver.rs:305-336, 575-590`); today the call waits for the scan and returns `[[Z/Target]]`. The host copies what core returns (`W/FilesSidebarViewModel.cs:2167-2178`). (Codex's fifth round.) CB-45c's spec settles it on its step 0: either it proves a rule under which the link names the same note whichever side of a pending commit it was formed on, or `wikilink_for_path` stays on the main connection.
- On Windows, the UI-thread calls of #2039 that stop waiting behind a scan as a result: `ShowTags`, which calls `LoadTags`, and `CopyWikilink` if `wikilink_for_path` moves. If it stays, #2039's own change (the call taken off the UI thread) is what frees `CopyWikilink`. The third call, `RefreshExternalStaleness`, stays on the main connection with `note_tasks` and needs #2039's own change.

Closes: the tree's, the directory pages' and the tag tree's wait. `Refs #1718`.

### CB-45d: search and the sidebar filter do not wait behind a scan

Size M. Lanes: rust, windows, swift.

Moves: `full_text_search`, `filter_files`, `untagged_files`.

Its spec settles, on its own step 0:

- Whether a long search may hold the one read connection. `full_text_search` checks its token between rows only, and `filter_files` takes no token at all (`crates/slate-uniffi/src/lib.rs:958-965`). Either they run under the progress handler like the other reads, or search gets a read connection of its own. If `filter_files` gains a token across the FFI, CB-45d edits `crates/slate-uniffi/src/lib.rs` and takes a place in the plan's list of chunks that change it.
- The helper counts search's statements on the read connection.
- **What a reader meets** (section 5's last paragraph). A search run during a rescan keeps the pre-scan rows and its spoken count (`SearchResultsSummary`) until the reader runs it again, because R-9 does not re-sync search. A sidebar filter run during a rescan speaks the pre-scan count at once, and the tree's publication after the scan re-runs it and speaks the corrected count when the total changed (`FileListCount`). Its spec puts each to the owner (for search: accept it, or re-run an open search after a rescan; for the filter: accept the two counts, or hold the first), appends an AR row for each one accepted, and runs an NVDA spot check of both. The plan's row has no AT today; it gains one.

Closes: #1718 (`Closes #1718`), which names Quick Open, the tree and tag tree pages, and search.

## Risks

- **macOS is proved by one Swift fact in CB-45b, not by core's suite.** No lane runs core's tests through `unix-posix`, and the read connection opens on first use (ruling 5), so in CB-45b only fact 14 opens it on a Mac: the open, its two checks and one rank during a held scan. The fallback paths, the release of the snapshot and the close order are not run on macOS until a Mac reader's session runs them in CB-45c. In CB-45b no Mac product path opens the read connection, so a fault there reaches no reader; from CB-45c it does. If the Swift fact cannot be written to hold a scan, CB-45b says so and the owner's Mac run is the proof.
- **A snapshot left open pins the log.** One early return that skips the end of the read transaction would stop checkpoints for the session's life and grow `-wal` without bound. Section 4's single function and fact 4 are the guard.
- **Stale rows and a stale spoken count are new behaviour.** For Quick Open the counts are accepted by the owner (rulings 2 and 10), and a selection that gives way to the first row is named (ruling 9). For the tree, the tag tree, search and the filter nothing is accepted yet: CB-45c and CB-45d each bring their own case to the owner (section 5's last paragraph). Search is corrected by no re-sync today; the filter is re-run after a rescan and speaks a second count when its total changed. Section 5 lists the flows found by reading; the spec's red team added two rows to it. It rests on the Windows host re-ranking after a scan and after a Slate-owned write. The Mac's Quick Open does not re-rank an open sheet today (by reading); that matters only when the Mac adopts the indexed rank (#1652), and the #1954 comment says so.
- **CB-45c's cursor rule is the hardest part of the build** and is not designed here beyond naming it.
- **A wait that cannot be cancelled remains in one case.** If another process is recovering the log when a read begins, SQLite retries on its own for up to about ten seconds before it answers; neither the busy timeout nor the token reaches that loop. It needs a second Slate process (or `slate-cli`) opening the same cache at that moment after a crash.
- **Other software on `.slate`.** An antivirus or a sync client that holds `-wal` or `-shm` at the moment the read connection first opens makes that open fail; section 3 sends the read to the main connection and tries again a second later. The main connection has the same exposure today.
- **Version skew is unchanged, not solved.** A session left running while a newer Slate migrates the shared cache is fenced from writing (`begin_fenced`), not from reading; its reads, on either connection, would use its own functions against the newer indexes.
- **A cache replaced under a live session, on macOS and Linux.** The read connection opens `cache.sqlite` by name, later than the main connection. If the file is replaced by a rename while the session runs (a backup restored under a live session, the case `SchemaVersionSkew` names, `D:67-75`), the two connections can hold different files that share one `-wal` and `-shm` by name. The compactor's connection has the same exposure today, and Windows refuses the rename of an open file. In CB-45b only fact 14 opens the read connection on a Mac; from CB-45c a reader's session does, and CB-45c's spec decides the check (its row).
- **A third connection per session** costs about 2.3 MB and three handles on Windows while it is open. Sessions that never read through it never open it.
- **Scale.** Everything was measured at 10,000 notes. Quick Open's read copies every openable path under the seat's mutex: 4 to 8 ms at 10,000 notes, so by proportion 40 to 80 ms at 100,000 (not measured), still far below a scan.

## Alternatives not taken

The reading's options B (the scan yields the lock between batches), C (an in-memory list for Quick Open) and D (shrink the window) are in `reading/07_core.md:319-332`. The owner chose A in CD34. Within A:

- **A read connection opened at session open.** Ruled out by ruling 5 (section 1 says what that trades).
- **`PRAGMA query_only` on a read-write connection.** It could checkpoint at close, so the order of closing would not matter. It refuses TEMP tables, and a pragma is a weaker guarantee than a file opened read-only.
- **A read connection per surface.** Not needed at the measured hold times.
- **Reading the journal mode back on the main connection in `apply_pragmas`.** It would change every open. The read connection reads its own.
- **Closing the read connection for the session's life on any failure.** Simpler, but one antivirus hold during the first open would cost the session the whole benefit.
- **Speaking the corrected count when a silent re-rank's total differs from the count last spoken**, or holding the count until the scan commits. The owner accepted the silent correction instead (ruling 2).

## Found on the way

1. **Windows: three session calls run on the UI thread and wait behind a scan.** `ShowTags`'s setter calls `LoadTags()`, which calls `TagTreePage` inline (`W/FilesSidebarViewModel.cs:968-976, 1469-1483`); `CopyWikilink` calls `WikilinkForPath` inline (`:2158-2169`); `RefreshExternalStaleness` calls `NoteTasks(Path, 1)` inline (`W/WorkspaceViewModel.cs:2127-2145`). Read at the call sites; that each caller runs on the dispatcher is from what it touches. During a rescan each freezes the window for what is left of the scan. CB-45c frees the first, and the second if `wikilink_for_path` moves (CB-45c's row: a copied link must name the same note after the scan). The third is a task read, which writes and stays on the main connection. High: a host change across view models. Filed as #2039 (ruling 6).
2. **`IndexPhase::ScanFinished` is not sent when a scan fails or is cancelled before its commit** (`S:3662-3670` returns before `S:3688` and `S:3739`). No host builds a "scanning" flag on the pair today (Windows uses its own `_rescanActive`), so nothing is wrong yet. On #1955 (comment 7075132994, line 1).
3. **The `-wal` file stays as large as the largest scan** (150.8 MB beside a 149.7 MB cache above) until the last connection closes. `journal_size_limit` would bound it. On #1955 (comment 7075132994, line 2).
4. **`filter_files` takes no cancel token.** CB-45d meets it.
5. **Mac: Quick Open opened during the first scan shows "No files. This vault has no notes yet."** over an empty list and does not refill when the scan ends (`M/QuickSwitcherView.swift:95-106, 439-455`; by reading, not run). On #1954 (comment 7075132728).

## #1954 and #1955

Posted on 2026-11-22 (ruling 7):

- #1954 (Mac work owed), comment 7075132728: item 5 above; when the Mac adopts the indexed rank (#1652), its open sheet must re-rank after a rescan, or section 5's stale rows stay until the sheet is reopened; and CB-45c, the first phase in which a Mac reader's session opens the read connection, owes a check on a Mac.
- #1955 (deferred finds), comment 7075132994: items 2 and 3 above, each with its failing test.

## For the plan

Plan lines at 3f2fd733. The lead records these in the plan; this PR edits only this document.

- **CB-45b is M, not L** (§5 row `:656`; the reading sized "Quick Open first" M, `reading/07_core.md:330`), and it does not edit `crates/slate-uniffi/src/lib.rs` (ruling 3). It leaves the list of chunks that change that file: §6's paragraph (`:719-722`, "Seven chunks now change") and §11's risk (`:1108`, "Seven chunks now change the FFI") each become six, and the shared-file row (`:832`), which also names CB-32's merged doc comment, goes from eight names to seven. Its lanes become rust, windows, swift and the runner lane (fact 12 edits `windows.yml` and `Workflows.Tests.ps1`, which start `windows-runner-tests.yml`).
- **CB-45b's files.** Its `db.rs` edit is `open_connection`'s access parameter and `open_reader`; its `session.rs` edit is the seat, the read function and `switcher_rank_top_indexed`. It also edits `.github/workflows/windows.yml` (the weak-volume job's session step) and `ci/windows-runner/tests/Workflows.Tests.ps1` (the pin on that step's names), and appends one AR row to doc 40. So CB-45b joins the shared-file rows for `windows.yml` (`:819`), `Workflows.Tests.ps1` (`:820`) and doc 40 (`:824`). **That puts it beside CB-43 in wave C16** (`:713`), which edits the first two: the two run in turn under the `Workflows.Tests.ps1` row's rule (the second rebases and reruns the suite), or one moves a wave. By the owner's answer to Call 9, CB-45b also edits `W/QuickSwitcherViewModel.cs` (no shared-file row names it), adds Windows facts, and appends an addendum after R-9 to doc 40 beside the AR row. Its size stays M.
- **Two new rows in §5:** CB-45c (M to L; rust, windows, swift; an NVDA spot check of what its surfaces speak during a rescan, and the VoiceOver check below; `Refs #1718`) and CB-45d (M; rust, windows, swift; an NVDA spot check, since a search run during a rescan keeps a stale count and a filter run during one speaks the pre-scan count and then the corrected one; `Closes #1718`). Each brings its own owner call for what its surfaces speak during a rescan (section 5's last paragraph); the owner's answer to Call 2 covers Quick Open only. In §6 they take free slots from C17 on (`:725-729`), each after the one before it; C17's third slot and C18's second and third are free at 3f2fd733 (`:714-715`), and C17 holds CB-44, which edits `windows.yml` and `Workflows.Tests.ps1` (CB-45c is not expected to). Both edit `session.rs`, so its shared-file row (`:829`, "then CB-45b and the phases after it") names them. CB-45c also edits `session/directory_page.rs`, which joins §6's shared-file table. CB-45d joins the `lib.rs` row only if its spec gives `filter_files` a token across the FFI.
- **The ledger row for #1718** (`:679`) names CB-45, CB-45b, CB-45c and CB-45d, with CB-45d as the phase that closes.
- **The VoiceOver check** the plan lists for #1718 in §8 (`:909-910`, "VoiceOver on Quick Open during a rescan, once CB-45b lands") has nothing to hear after CB-45b: the Mac's Quick Open does not use the indexed rank. It moves to CB-45c (the tree during a rescan) and to #1652's follow-up (Quick Open, once the Mac adopts the indexed rank).
- **§11's PR count** (`:1074`, "53 PRs before CB-46's, and more for the phases CB-45's design cuts") gains two, and the CB-45b risk (`:1106`, "The number of phases is not known until the design is read") can say three phases.
- **The owner's answers** of 2026-11-22 join §3.1's answers beside CD34 (`:451`): Call 2, "Accept, record it (Recommended)"; Call 9, "Speak new selection (Recommended)", with the lead's ruling that the re-rank after a Slate-owned write keeps a surviving selection; Call 10, "Accept (Recommended)".
- **The issue Call 6 filed:** #2039.

## Calls for the lead, as ruled

Each call as the spec put it, with its ruling. Call 9 was raised by the red team on this document and widened after Codex's third round, which also raised Call 10.

1. **The phases.** Three implementation PRs: CB-45b (Quick Open), CB-45c (tree, directory pages, tag tree), CB-45d (search and the filter, which closes #1718). The note panels, link lists, properties, history and the other plain reads in section 6's last row do not move in this plan. Cost: two more chunks than the plan lists, M to L and M. **Ruled: yes.**
2. **During a rescan Quick Open answers at once from the pre-scan index, speaks that count, and the later correction is silent.** Offered: (a) accept it, recorded as the owner's call in an AR row of doc 40 (recommended: the sequence already exists for a query typed just before a rescan, and R-9 made the re-rank silent on purpose, so the scan's one sentence speaks for the run); (b) speak the corrected count when a silent re-rank's total differs from the count last spoken (a Windows host change in `QuickSwitcherViewModel`, an amendment to R-9's family, an NVDA pass); (c) hold the count until the scan commits (a screen-reader user keeps today's wait). **The owner's answer: "Accept, record it (Recommended)".** CB-45b records it in doc 40 and hears it in its NVDA spot check.
3. **No FFI change in CB-45b, and the plan's lists follow.** Cost: none. **Ruled: yes.**
4. **The fallback's wait for the main mutex becomes cancellable** (section 4, step 5), although CD34 says no interim change. It is the same function, a few lines and half of fact 9, and it ships with the read connection. **Ruled: yes, named as a departure from CD34's "No interim".**
5. **The read connection opens on first use, not at session open.** Cost: on macOS, CB-45b's read connection is run by one Swift fact only, where an open at session open would have run it in each of the Swift test files that open a session (107 by search; the spec said 35). **Ruled: yes; section 1 and the risks say what that leaves unrun on macOS in CB-45b.**
6. **File the Windows UI-thread find as an issue now** (labels bug, windows), naming the three calls and what CB-45c frees. **Ruled: yes. Filed as #2039.**
7. **The #1954 line and the two #1955 lines.** **Ruled: yes. Posted.**
8. **Where the design lives:** beside the other specs, with no probe code. **Ruled: yes.**
9. **A re-rank moves the Quick Open selection to the first row without naming it** (section 5). Two ways, both by reading: (i) during a rescan, R-9's silent re-rank keeps a selected row only when its note survived, and otherwise selects the first row in silence; today that needs a Quick Open left open across a rescan, and with the read connection it covers every arrow move made during the scan; (ii) after a Slate-owned write, the event's re-rank drops every selection, surviving or not, selects the first row, and speaks only the count; that is today's behaviour, unchanged by the read connection (added after Codex's third round). Either way, a reader who arrowed to a row and presses Enter after the re-rank opens a note they did not hear named. Three ways to rule:
   - (a) Accept both, as a sentence in Call 2's AR row. Cost: nothing built.
   - (b) When a re-rank does not keep the row the reader had selected, speak the new selection with the sentence an arrow move already speaks (`RowSelected`; no new wording), and let the event's re-rank keep a surviving selection as R-9's does. Recommended: the reader hears where the keys went before Enter acts on it, and today's cases are answered too. Cost: a branch in `ApplyRanked` and one in `ScheduleRefresh` in CB-45b, Windows facts, an addendum after R-9 (whose re-sync speaks nothing today), steps in the NVDA spot check, and the trigger ledger if the event is constructed outside the setter.
   - (c) Clear the selection when its row is gone, so Enter opens nothing until the reader arrows again. Cost: as (b), and an Enter that does nothing and says nothing.
   **The owner's answer (2026-11-22): "Speak new selection (Recommended)".** The owner answered the call as first put, for way (i). The lead ruled the same day that the widened (b) follows from that answer, so CB-45b builds both branches; the second is recorded as the lead's ruling under the owner's answer. Section 5 gives the rule and the addendum after R-9.
10. **During a Slate-owned write, a typed Quick Open query speaks two counts** (section 5, added after Codex's third round). A keystroke ranked while a write holds the main connection's mutex is answered from the state before the write and speaks that count; the write's event then re-ranks and speaks the corrected count. Today the keystroke waits for the write and one count is spoken. The window is the write's hold on the mutex (38 ms on average for a save in step 0). Two ways to rule:
   - (a) Accept it, as a sentence in Call 2's AR row. Recommended: the correction is spoken, which Call 2's rescan case does not even do, and the window is a write's few tens of milliseconds. Cost: nothing built; a sequence in the NVDA spot check if one can be timed, otherwise a Windows fact on the view model.
   - (b) Rank Quick Open on the main connection while a Slate-owned write is in flight. Cost: core would need to know a write is in flight, which the read seat does not, and the keystroke waits as today.
   **The owner's answer (2026-11-22): "Accept (Recommended)".** It is a sentence in Call 2's AR row (section 5), and fact 16 holds it.

## Verified by running, by reading only, and not verified

- **By running (a release-profile test binary, Windows, 02fbd37f):** every number in step 0; that a read-only connection opens on the anchored path, idle and during a write transaction; what it sees at each point of a held scan; what it refuses; the rollback-journal behaviour; the SMB loopback mode; who removes `-wal` and `-shm`. The spec's red team repeated the SQLite behaviours (the mode read, read-only semantics, snapshot pinning, close order, `data_version`) in Python on SQLite 3.50.4. The document's red team ran three more there: `data_version` across a close and reopen and inside one read transaction, the error a `BEGIN` gets after a read transaction left open, and the error a write gets on a read-only connection.
- **By reading only:** everything about the hosts, including what a screen reader is given to speak; the flows in section 5; the anchored hooks; SQLite's source for locks, sharing and handles; macOS. The code citations were read at 02fbd37f and hold at 3f2fd733, which changed documents only.
- **Not verified:** macOS in any form; FAT32 and exFAT; what NVDA actually speaks in section 5's sequences, the silent change of selection among them; what search and the filter speak during a rescan once they move; that every caller named in "Found on the way" runs on the dispatcher; a second process during a read; the design's own code, which does not exist yet.

## Red team record

**The spec (2026-11-21; Opus, xhigh; the repository read at 02fbd37f, SQLite's and rusqlite's source, and its own experiments in Python on SQLite 3.50.4).** No blocker, 4 majors, 27 minors. Its report is kept with the chunk's working notes. It found the SQLite mechanics, the census shape, the teardown order, the reasons for what stays on the main connection and every row of the drift check sound. What it changed:

- **M1, the spoken count.** The first draft said nothing a reader hears changes. On head a query typed during a rescan speaks the pre-scan count at once and the host's later re-rank is silent, where on main the post-scan count is spoken. Section 5 now says so, the NVDA spot check uses a rescan that changes the ranked set, and Call 2 offered three rulings.
- **M2, what counts as a failure.** The first draft closed the read connection on any error but a cancellation or a busy answer, which would have closed it on CB-45c's first refused cursor. Section 3 now decides by where the error came from, says per read whether it runs again on the main connection, and gives a retry rule in place of closing for the session's life (m14).
- **M3, the maintenance figure.** The interval the first draft called maintenance was the commit, and the probe vault had no op-log history. The figures are relabelled and a third probe measured a vault with history.
- **M4, section 5.** Two rows were missing (a delete of a file or folder; the single-commit flows that change the disk first), and a batch move that is rolled back entirely sends no event, so "healed by the event" was false there. The table, the heading and the healing paragraph are corrected.
- **Minors folded:** the ranges now cover every run (m1 to m4); the probe's second connection is stated to differ from the design's (m5); an unchanged rescan commits because of the directory upserts, not a stamp on every row (m6); handle counts (m7); why a read-only connection cannot checkpoint and what that trades against `query_only` (m8); the SMB result is weak evidence (m9); the compactor's connection already runs through `unix-posix` and the Mac lane's sessions never open the read connection (m10); `open_reader` checks that what it opened is read-only (m11); the two mutexes are never held together (m12); a poisoned seat recovers (m13); the function owns the progress handler (m15); `temp_store` and the page cache are set (m16); the busy timeout's limits (m17); the close-path claim is withdrawn (m18); the facts gained deadlines, mutations, names and four missing cases (m19); the file list (m20); the census is not edited (m21); "For the plan" names the ledger row, the PR count and the shared-file table (m22); the AR row for Call 2 (m23); section 6 says everything unnamed stays (m24); the lazy open is Call 5 (m25); "the reader" is the person and "the read connection" the connection (m26); wording (m27).
- **Not checked by the spec's red team:** anything by running Rust, C# or Swift; macOS; FAT32, exFAT and a real share; SQLite 3.54.1 by running; what NVDA speaks in M1's sequence (its conclusion rests on the view model's code).

**This document (2026-11-22; Opus, xhigh; the repository read at e7d4abe0, which is 3f2fd733 plus this file, and its own experiments in Python on SQLite 3.50.4).** No blocker, 3 majors, 21 minors. Its report is kept with the chunk's working notes. It found the census shape, the close order, the lock rule, `db_cache_status`'s one-lock rule, fact 13's hold, R-9's silent rule, the plan citations but two, the macOS statements and the issue and comments sound. What it changed:

- **M1, the selection.** The silent re-rank keeps the selection only when its path survived, and otherwise moves it to the first row without a word, so Enter can open a note the reader did not hear. Section 5 says so, the NVDA spot check gains a sequence for it, and Call 9 put it to the owner, whose answer makes CB-45b name a moved selection.
- **M2, the later phases.** CB-45c and CB-45d bring the same kind of spoken output to the tree, the tag tree, search and the filter, and R-9 does not re-sync search. The owner's answer covers Quick Open only. Section 5, the risks, both phases' rows and "For the plan" now say that each phase brings its own owner call and AR row, and CB-45d gains an NVDA spot check. (The red team also said the filter is not re-synced; Codex's first round showed that the tree's publication re-runs it, and section 5 is corrected.)
- **M3, a cursor across a reopen.** On a read-only connection `total_changes` is always 0 and `data_version` starts again at each open, so a cursor from a closed read connection can be accepted by the reopened one. Section 3 gives each open a generation number, section 9 says why, and CB-45c binds it into every cursor with a fact.
- **Minors folded:** 14 call sites, not 13 (m1); the reading's Size row is `:330` (m2); the `lib.rs` row goes from eight names to seven (m3); fact 12's module, name, workflow steps, Pester pin and red route (m4); the runner lane (m5); 107 Swift test files, not 35 (m6); the fallback registers the progress handler on the main connection, with a fact (m7); `SQLITE_READONLY` passes through (m8); a seam reports which connection served each read, so fact 3 and fact 7 cannot be answered by a fallback (m9); fact 6 checks the read connection's settings and function registration (m10); fact 1 is the session's first rank (m11); fact 9's red route and fact 14's analogy (m12); the NVDA check's order, trigger and vault (m13); the AR row says "as of the last commit" and names opening Quick Open (m14); three existing facts change meaning in CB-45c (m15); CB-45d may change the FFI (m16); `db_cache_status` and the seams reach the seat, and a poisoned seat does not make the status call panic (m17); section 5 names two things, not three (m18); the parser-bump marker commits before the walk (m19); logging once per change of state (m20); a cache replaced under a live session on macOS and Linux (m21).
- **Not checked by this red team:** anything by running Rust, C# or Swift; macOS; FAT32, exFAT and a real share; SQLite 3.54.1 by running; what NVDA speaks in M1's and M2's sequences, including whether a list's selection change is spoken while focus is in the search box; whether the filter's results re-run on any other event.

## Codex record

`codex-companion adversarial-review --base origin/main --scope branch`, with the focus naming eight invariants the design must uphold, the decided and off-limits register, and the severity bar.

- **Round 1 (2026-11-22, at 1e7429af): needs-attention, one finding (medium, called a blocker).** The design said the sidebar filter's results and spoken count stay stale after a rescan until the reader runs the filter again. That was wrong: the rescan's tree publication re-runs an active filter, which speaks its count again when the total changed (`W/FilesSidebarViewModel.TreeOperations.cs:640`, `W/FilesSidebarViewModel.Filter.cs:497-540`). Taken, checked against the code: section 5's last paragraph, CB-45d's row, the risks, "For the plan" and the red team record now describe the automatic re-run and its second count, and keep search as the surface that stays stale.
- **Round 2 (2026-11-22, at 16cfc0ea): needs-attention, two findings (medium, called blockers) and one minor; no core invariant broken.** (1) Section 5 said the scan's sentence follows the silent re-rank, but a watcher run posts no sentence while Quick Open, a modal surface, is open (`W/VaultLifecycleViewModel.Rescan.cs:1132-1156`, `W/MainWindow.xaml.cs:185-191`), so after a change made outside Slate the reader hears the pre-scan count and then nothing. (2) The NVDA check's first sequence started Refresh with Quick Open open, which the host does not allow (`W/MainWindow.xaml:174-178, 1143-1150`). (3) A re-rank that supersedes a still-pending keystroke rank speaks its count (`W/QuickSwitcherViewModel.cs:433-435`). All three taken, checked against the code, as a class: section 5 now says what follows the re-rank for Refresh, a foreground run and a watcher run, and names the pending-keystroke exception; the AR row records the watcher's silence and the exception under the owner's answer to Call 2; the In brief says it; and the NVDA check has four sequences, started the ways the host allows.
- **Round 3 (2026-11-22, at e2dd0c6c): needs-attention, one finding (medium) and one minor; the earlier corrections match the code, and no break of invariants 1 to 6.** (1) The re-rank after a Slate-owned write is not silent: it drops every selection, selects the first row without naming it and speaks the count (`W/QuickSwitcherViewModel.cs:255-267, 444-450, 541-551`), and with the read connection a keystroke ranked during the write speaks the pre-write count and then the event's count. (2) The pending-keystroke window is the whole time from keystroke to rows (a 60 ms debounce, the rank lane, the rank and the post to the UI thread), not the rank's 50 ms. Both taken, checked against the code. Section 5 has a paragraph for the write's re-rank and the corrected window; Call 9 now covers both ways a re-rank moves the selection without naming it (the write's way is today's behaviour); Call 10 puts the two counts to the owner; the NVDA check says which cases are held by view-model facts instead.
- **The owner's answers (2026-11-22), folded before round 4.** Call 9, "Speak new selection (Recommended)", with the lead's ruling for the write's re-rank: section 5 states the rule and drafts the addendum after R-9; CB-45b gains the host change, facts 15 and 16, and the expected phrases of sequence 2. Call 10, "Accept (Recommended)": a sentence in the AR row. The owner also chose "Run round 4 (Recommended)", so the rounds continue to approve.
- **Round 4 (2026-11-22, at 1330eaad): approve.** "No blocking design defect found. Call 9 is buildable, and fact 16's red route is sound." Two minors, both taken: (1) the watcher run's silence is qualified everywhere it is stated (the In brief, section 5, the AR row, NVDA sequences 3 and 4), since by Call 9 a removed selection is named even then; (2) fact 15's empty-rows case is green on main and is seen red by a named mutation.
- **Round 5 (2026-11-22, at 1d21d045): needs-attention, one finding (medium); both round-4 folds confirmed.** `wikilink_for_path`, which CB-45c moves, feeds the clipboard, not a display: answered from the pre-scan index, a bare link can resolve to a different note once the scan adds a namesake, and no re-sync repairs a pasted link (`S:7010-7036`, `crates/slate-core/src/link_resolver.rs:305-336, 575-590`). Taken, checked against the code, as a class: section 6 gains a row for every read whose answer outlives the display, CB-45c moves `wikilink_for_path` only with a proved rule or keeps it on the main connection, and the #2039 lines say `CopyWikilink` is freed by CB-45c only if it moves.
