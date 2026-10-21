# WB-31 — Two sessions in one process: the shared page cache is reproduced or refuted on Windows, and the remedy waits for the answer

Issues: Refs #1484 (closes by hand after a macOS measurement confirms
the remedy, or the owner withdraws the macOS observation — WD21; §5
closure matrix). Size M (diagnose first). Lanes: rust. AT: none. Wave:
any free slot. Depends on: nothing. Parallel with: anything (touches
`db.rs`, `session.rs`'s config, the oplog compactor's connection
ownership — `compaction_worker_loop`'s local `worker_conn` becomes a
session-shared slot, `:1686`, `:2695-2722` — `benches/`, new test
modules and the `#[cfg(test)]` child-process harness `run_alone` /
`is_alone_child` in `session.rs`; the remedy branch also retypes `session.rs`'s `conn` field,
rewrites its seven `lock_cancellable(&self.conn, …)` lines and
`finish_structural_move`'s guard parameter (`:16965`), and wraps scans in
an activity guard, leaving the 98 direct `self.conn.lock()` lines
textually unchanged).
Decision: WD21 (pending; recommended "conditional: if the bench proves
the shared LRU, release the idle session's cache; if it refutes it, the
issue stays open with the measurements; a Windows bench that does not
reproduce closes nothing; #1484 closes only after a Mac measurement";
under the other answers — unset `ENABLE_MEMORY_MANAGEMENT`, or size
`cache_size` — the remedy commit is replaced by the build-flag override
in every consumer's build or by a per-session `cache_size` rule, and the
same before/after bench gates it).

## Goal

A bench anyone can run in one process opens two identical 10k-note
vaults and measures the older session's no-change rescan with the newer
one open and closed, reading SQLite's per-connection page-cache counters,
so the mechanism #1484 suspects — one process-wide page-cache LRU from
`SQLITE_ENABLE_MEMORY_MANAGEMENT` — is proven or refuted with numbers
that discriminate eviction from other causes. If proven, an idle session
releases its cache through a scheduler this chunk adds (none exists),
which keeps the bundled build and the one-session case untouched. If
refuted, the issue stays open with the measurements.

## Current state on main

Verified against the worktree; the plan's lines hold.

- **The pragma site.** `db.rs:398` `apply_pragmas(conn, cache_size_pages)`:
  WAL, `synchronous NORMAL`, `temp_store MEMORY`, `foreign_keys ON`, then
  `:429` `cache_size` in pages. Callers `open_database` (`:242`, used at
  `session.rs:2641` with `config.max_db_cache_pages`) and `open_in_memory`
  (`:307`). `SessionConfig.max_db_cache_pages` (`session.rs:203`,
  "Desktop default 4096 (~32 MB); mobile 512", default `:262`, test
  `:283`). A session opens a second, worker connection lazily
  (`open_worker_connection` `:1600`, used `:1752-1753`), so two sessions
  can hold four connections. It is the oplog compactor's, not the
  scan's: `compaction_worker_loop` (`:1671`, run on the
  `slate-oplog-compactor` thread spawned at `:2695-2722`) keeps it in a
  local `let mut worker_conn: Option<Connection> = None;` (`:1686`, "owned
  by the worker thread for its lifetime"), so nothing on the session can
  read its counters or release it today.
- **No housekeeping tick exists.** `session/watch.rs:14-22`: the
  coalescer's thread "sleeps on a condvar with a deadline (no polling)",
  is owned by the session, and exists only once watching starts; a bare
  session (the bench's, and every host session before `start_watching`)
  has no periodic thread at all. The only process-wide statics in
  `session.rs` are counters (`:49`) and the structural-lock registry.
- **The build.** `Cargo.toml:25-28`: rusqlite 0.40 `bundled` →
  libsqlite3-sys 0.38.1 (`Cargo.lock:1254`), bundled SQLite 3.53.x. The
  issue's claim that its `build.rs` passes `-DSQLITE_ENABLE_MEMORY_MANAGEMENT`
  is not re-read here: verify in
  `$env:USERPROFILE\.cargo\registry\src\*\libsqlite3-sys-0.38.1\build.rs`
  as the bench's first step and record the line. No `LIBSQLITE3_FLAGS` or
  `.cargo/config.toml` override exists. The repository configures no
  `SQLITE_CONFIG_PAGECACHE` buffer, so `SQLITE_STATUS_PAGECACHE_USED`
  (pages from that buffer) reads zero or flat regardless of the mechanism
  and proves nothing; `SQLITE_STATUS_PAGECACHE_OVERFLOW` (page allocations
  that fell through to the default allocator) and the per-connection
  `SQLITE_DBSTATUS_*` counters are the observations that can.
- **The evidence on record.** `BENCHMARKS.md:1292-1296` (under the PR 20.0
  entry, `:1233`): the 1.8× two-session note. The perf guard
  `link_pass.rs:450` `perf_guard_unresolved_link_rescan_within_twice_the_link_free_one`
  opens one session at a time because of it; `rust.yml:279-290` runs the
  two guards isolated (`:158-159`); `windows.yml:337-341` skips both by
  name. The bulk test invocations in CI: `rust.yml`'s two nextest sweeps
  (`:251-256`, censuses, `-E "test(census_) and not
  test(${ROOT_LISTING_PERF_GUARD}) and not
  test(${UNRESOLVED_LINK_PERF_GUARD})"`, and `:258-267`, the non-census
  suite, `-E "not test(census_) and not test(…) and not test(…)"`) each
  exclude the guards by the job-level names at `:158-159`, and the
  isolated steps (`:269-290`) select each by `test(=…)` with
  `--no-tests=fail --test-threads 1`, so a rename fails loudly;
  `rust.yml:294`'s `cargo test --workspace --doc` runs doctests only;
  `windows.yml:337-341` (`cargo test -p slate-core --profile ci --locked
  -- --skip census_ --skip …`) is the Windows lane's one slate-core bulk
  run and runs no guard in isolation (the Rust lanes cover them). A
  fact whose name or module path contains a bulk run's filter runs in
  that bulk run unless the run skips it.
- **Benches.** `crates/slate-core/benches/scan_bench.rs`:
  `bench_first_open_and_scan` (`:38`), `bench_reopen_with_cache` (`:73`),
  `benches/common/mod.rs` `generate_vault(file_count)` (`:95`).
  `rust.yml:304-392` `bench-compile` runs `cargo bench --no-run`.
- **How the session's connection is reached (census, non-test code).**
  `VaultSession.conn: Mutex<Connection>` (`session.rs:1291`, built at
  `:2729`). 98 sites lock it directly — 97 `self.conn.lock()` in
  `session.rs` (one split across two lines), among them public reads
  such as `indexed_content_hashes` (`:3334`, lock at `:3347`),
  `register_change_feed` (`:3372`, `:3373`) and `change_feed_window`
  (`:3388`, `:3394`), plus `session/directory_page.rs:361` — and 7 go
  through `lock_cancellable(&self.conn, cancel)` (`:6723`, `:6849`,
  `:6877`, `:13239`, `:14999`, `:16119`, `:16271`). `lock_cancellable`
  (`session/trash_cancellation.rs:16-37`) is generic over any
  `Mutex<T>` and also guards `structural_operation`, so it is neither the
  connection's only acquisition path nor specific to it. One function
  names the guard type: `finish_structural_move(&self, mut conn:
  std::sync::MutexGuard<'_, Connection>, …)` (`:16965`); no other site
  under `src` spells `MutexGuard<…Connection>`. The session
  tests lock `session.conn` 253 more times. `SessionConfig::new`
  (`:259`) has no `cfg(test)` defaults today; `default_trash`
  (`vault/trash_handoff.rs:610-620`) is the crate's precedent for a
  test-build default.
- **The counters** (libsqlite3-sys symbols on `Connection::handle()`,
  through `rusqlite::ffi`, as `db/anchored.rs` already does for the VFS):
  `sqlite3_db_status(db, SQLITE_DBSTATUS_CACHE_USED | CACHE_USED_SHARED |
  CACHE_HIT | CACHE_MISS | CACHE_WRITE | CACHE_SPILL, &cur, &hi, reset)`
  per connection; `sqlite3_status64(SQLITE_STATUS_MEMORY_USED |
  PAGECACHE_OVERFLOW, …)` process-wide; `sqlite3_db_release_memory(db)`.

## Reference

None: no host twin; the behavioural spec is #1484's text and WD21's text
(§3). The numbers on record are macOS (M5 Max: 173 ms against 96 ms for
the older session; the newer 88–120 ms; a third session makes the first
slow again). **What proves the shared LRU:** after B opens and scans,
A's next rescan shows `CACHE_MISS` rising by about the pages A's rescan
touches while A's `CACHE_USED` fell between the two rescans without A
running (eviction by another connection), and `PAGECACHE_OVERFLOW` /
`MEMORY_USED` for the process plateau near one session's `cache_size`
rather than two. **What refutes it:** A's `CACHE_USED` holds between
rescans and its `CACHE_MISS` does not rise while the slowdown persists —
then the cause is elsewhere (WAL checkpointing, the lock registry,
`PRAGMA cache_size` re-application) and the bench's table says so. The
remedy's acceptance: the older session's rescan back within noise of its
alone figure while the newer's is unchanged.

## Design

1. **The counter seam.** `VaultSession::db_cache_status(&self) ->
   DbCacheStatus` (new, `#[doc(hidden)] pub`, like the `perf_guard`
   seams): `{ used, used_shared, hit, miss, write, spill }` for the main
   connection, and `worker: Option<…>` for the worker connection when
   open; `pub fn process_cache_status() -> ProcessCacheStatus { memory_used,
   pagecache_overflow }` (new, `db.rs`). To read the worker's counters at
   all, the compactor's connection moves (both branches) out of
   `compaction_worker_loop`'s local (`session.rs:1686`) into a new session
   field `worker_conn: Arc<Mutex<Option<Connection>>>` that `open_in`
   creates before spawning `slate-oplog-compactor` (`:2695-2722`) and
   passes in as a new parameter. The compactor takes that lock per job
   (after `rx.recv()` returns, dropped before the next `recv`), never
   across the wait, and opens the connection into the `Option` on first
   need, as today. `db_cache_status` reads the main connection, drops
   it, then reads the worker slot; it never holds both. A `#[cfg(test)]`
   seam, `VaultSession::open_worker_connection_for_tests()` (new), opens
   the connection into that slot through `open_worker_connection`, as
   the compactor does, so facts need not provoke a compaction.
2. **The bench** `benches/two_sessions_bench.rs` (new; `[[bench]]` entry):
   two identical 10k vaults via `common::generate_vault`; open A, scan,
   rescan 8× (median); open B, scan; rescan A 8×; rescan B 8×; close B;
   rescan A 8×; open C; repeat. Per rescan, the per-connection deltas and
   the process counters; print a table; criterion groups carry the
   medians. First step of the run: the `build.rs` grep, recorded.
3. **The diagnosis facts** `session/tests/two_sessions.rs` (new), in two
   groups that never mix:
   - **always on, both WD21 branches** (they read counters and release
     nothing): `db_cache_status_reads_this_connections_counters` and
     `process_cache_status_reads_the_process_counters`. The second is a
     parent and a child, in the crate's child-process form
     (`real_trash_linux_child`, `vault/trash_handoff_tests.rs:1108-1125`
     and `:1143-1154`; that precedent is Linux-only, the form is not):
     process-wide counters move with every connection any parallel test
     opens or drops, and only the registry facts serialise, so the
     reading is proven in a process that holds nothing else (and, with
     `idle_cache_release_ms` 0 under `cfg(test)`, runs no release in the
     remedy branch either). The parent calls `run_alone(
     "session::tests::two_sessions::process_cache_status_child")` — the
     child-process harness every exact-cache fact of this chunk uses
     (new, `#[cfg(test)] pub(crate)` in `session.rs` beside its other
     test seams, so both `session::tests::two_sessions` and the remedy's
     `session::registry::tests` reach it): `pub(crate) fn run_alone(child:
     &str)` spawns `std::env::current_exe()` with `[child, "--exact",
     "--nocapture", "--test-threads=1"]` and the environment variable
     `SLATE_WB31_CHILD` set to `child`, waits, and asserts the exit status
     is success with the child's stdout and stderr in the failure
     message; `pub(crate) fn is_alone_child(child: &str) -> bool` is true
     only when `SLATE_WB31_CHILD` equals `child`. Every child's first line
     is `if !is_alone_child("<its own full path>") { return; }`, so the
     ordinary suite runs each child as a no-op and a child process runs
     exactly the one test it names, alone. The child
     (`process_cache_status_child`, new)
     opens two sessions A and B with `common::make_vault` and makes
     **controlled allocations one connection at a time**: under the
     connection's lock, `CREATE TEMP TABLE wb31_pad(x BLOB)` and a
     recursive CTE inserting 1 024 rows of `zeroblob(8192)` — at least
     8 MiB of temp pages, held in memory (`temp_store MEMORY`,
     `db.rs:424`), which no LRU evicts and whose growth evicts nothing.
     First B allocates while A is untouched, then A while B is untouched;
     each step must move the process reading by at least half of the
     allocating connection's own `used` growth while the other
     connection's `used` stays exactly equal. So the reading is
     process-wide: a per-connection reading of either connection is flat
     in one of the two steps. The same untouched-connection checks pin
     `db_cache_status` as per-connection (a process-wide reading reported
     as `used` moves with the other connection's allocation). They live
     in the child, not in the ordinary suite, because with
     `SQLITE_ENABLE_MEMORY_MANAGEMENT` every purgeable cache in the
     process shares one LRU (`pcache1`'s global group), so a parallel
     test's connection reaching its own `cache_size` can recycle another
     session's oldest page — an exact-equality check on `used` is only
     deterministic where nothing else runs;
   - **the timing guard**, `perf_guard_two_sessions_do_not_evict_each_others_cache`
     — `#[ignore]` **until the remedy ships**, with the measured ratio in
     its doc (in the refuted branch it stays ignored, its doc holding the
     measurement); the remedy's commit removes the `#[ignore]` and the
     fact asserts the ratio older-with-newer / older-alone ≤ 1.25 and that
     A's `CACHE_USED` holds between rescans.

   While ignored (the refuted branch) a `perf_guard_` fact runs only by
   hand, as Verification does, and touches neither workflow. Enabled (the
   remedy branch), this chunk's two guards —
   `session::tests::two_sessions::perf_guard_two_sessions_do_not_evict_each_others_cache`
   and
   `session::registry::tests::perf_guard_the_only_session_in_a_process_is_not_slowed_by_the_release`
   (in `session/registry.rs`'s test module) — time rescans and assert
   exact `CACHE_USED` readings, which the shared LRU makes deterministic
   only where nothing else runs, so each runs **only alone** and **every
   bulk invocation excludes it**. Their paths contain the `two_sessions`
   and `registry` filters, so a bare filter run would execute them beside
   the other facts; and `cargo test --workspace` would too. Locally: the
   `two_sessions` run, the `registry` run, the whole-core run and the
   workspace run each pass `-- --skip perf_guard_`, and each guard runs
   by name in its own `--release` invocation. In CI: `rust.yml`'s job
   `env:` (`:157-159`) gains `TWO_SESSIONS_PERF_GUARD:
   session::tests::two_sessions::perf_guard_two_sessions_do_not_evict_each_others_cache`
   and `ONLY_SESSION_RELEASE_PERF_GUARD:
   session::registry::tests::perf_guard_the_only_session_in_a_process_is_not_slowed_by_the_release`;
   both nextest sweeps' filtersets (`:254-256` and `:265-267`) append
   `and not test(${TWO_SESSIONS_PERF_GUARD}) and not
   test(${ONLY_SESSION_RELEASE_PERF_GUARD})`; two isolated steps in the
   `:269-290` form are added (`cargo nextest run --workspace
   --cargo-profile ci --locked --no-tests=fail --test-threads 1 -E
   "test(=${TWO_SESSIONS_PERF_GUARD})"`, and the same for
   `ONLY_SESSION_RELEASE_PERF_GUARD`); and `windows.yml:337-341` gains
   `--skip perf_guard_two_sessions_do_not_evict_each_others_cache` and
   `--skip perf_guard_the_only_session_in_a_process_is_not_slowed_by_the_release`.
   `rust.yml:294` (doctests) and `windows.yml:346` (`slate-cli`) run no
   slate-core unit test and need nothing.
4. **The remedy, conditional (WD21), with its own scheduler.** If the
   counters prove the shared LRU:
   - **One way to each connection, recording when its use ENDS.** An
     operation that holds a connection longer than the interval must
     not become releasable the moment it lets go: the idle interval
     starts when a use completes, not when it began.
     `session/conn_slot.rs` (new): `pub(crate) struct ConnSlot<C =
     Connection> { inner: Mutex<C>, last_activity_ms: AtomicI64, active:
     AtomicUsize }` and its guard `pub(crate) struct ConnGuard<'a, C =
     Connection> { guard: MutexGuard<'a, C>, slot: &'a ConnSlot<C> }`
     (new; `Deref`/`DerefMut` to `C`; its `Drop` stamps
     `last_activity_ms` before the inner guard unlocks, so a release
     that wins the lock next always sees the completion). Methods:
     `fn lock(&self) -> LockResult<ConnGuard<'_, C>>` (stamps, then
     `inner.lock()`, a poisoned lock's guard wrapped the same way — the
     `LockResult` shape `Mutex::lock` has, so the 98 direct
     `self.conn.lock()` sites and the 253 test sites compile unchanged
     and are all recorded at both ends; the one function that names the
     guard type, `finish_structural_move` at `:16965`, takes
     `ConnGuard<'_>`), `fn lock_cancellable(&self, cancel: &CancelToken)
     -> Result<ConnGuard<'_, C>, VaultError>` (stamps, then the generic
     helper's loop on `inner`, wrapped), `fn activity(&self) ->
     ActivityGuard<'_, C>` (new; increments `active` and stamps; its
     `Drop` stamps, then decrements — an operation that is in progress
     without holding the lock, such as a scan between its connection
     uses), `pub(super) fn try_lock_without_activity(&self) ->
     Option<MutexGuard<'_, C>>` (the release path's only door: `None`
     while anyone holds the connection — a held connection is in use,
     so the release skips it rather than wait and then free a cache a
     long scan has just warmed), `pub(super) fn cache_counters(&self)`
     (reads `sqlite3_db_status` under `inner` without stamping and
     returns numbers, never a guard, so `db_cache_status` observes a slot
     without making it look busy), `fn is_active(&self) -> bool` and
     `fn idle_since_ms(&self) -> i64` (the last stamp). Time is
     `conn_slot::now_ms() -> i64` (new): milliseconds since a
     process-wide `LazyLock<Instant>`, monotonic; under `cfg(test)`,
     `set_test_clock_ms(Option<i64>)` (new) pins it for the registry
     facts. `VaultSession.conn` becomes `ConnSlot` (`session.rs:1291`,
     `:2729`) and the shared worker slot from step 1 becomes
     `Arc<ConnSlot<Option<Connection>>>`; the compactor's per-job lock
     is therefore a `ConnGuard` too, so a long compaction pass is
     recorded at its end. `inner` is private to `conn_slot.rs`, so the
     type system is the census: nothing reaches either connection except
     through these methods. Migration: the seven
     `lock_cancellable(&self.conn, cancel)` sites become
     `self.conn.lock_cancellable(cancel)` (the generic helper stays for
     `structural_operation`). `scan_initial` and `rescan` hold
     `self.conn.activity()` for their whole run, so a scan whose
     non-database phase outlasts the interval is not released mid-scan,
     and its idle interval starts when it returns.
   - **The registry and its thread.** A process-wide `SessionRegistry`
     (new, `session/registry.rs`:
     `LazyLock<Mutex<Vec<Weak<SessionCacheHandle>>>>`, each entry a weak
     handle to a session's main `ConnSlot` and its worker
     `ConnSlot<Option<Connection>>`) that
     `open_in` registers in — only when `idle_cache_release_ms` is
     nonzero — and `Drop` removes from; a single lazily started daemon
     thread `slate-cache-release` (new; parked on a condvar with a
     deadline, like the watcher's, cancelled and joined when the registry
     empties) wakes every `SessionConfig.idle_cache_release_ms` (new;
     default 30 000 desktop, 0 = never; **0 under `cfg(test)`**, as
     `default_trash` gives core's test build its scratch bin, so only the
     facts that opt in register and the rest of the suite never shares
     the registry) and runs one `release_pass()` (new, `pub(super)` in
     `registry.rs`; the facts call it directly). A pass does nothing
     unless two or more sessions are registered (a lone session is never
     released, so the one-session case is untouched by construction);
     otherwise, for each main and worker slot, it takes
     `try_lock_without_activity` and, under that guard, re-reads
     `is_active()` and `idle_since_ms()`, and calls
     `sqlite3_db_release_memory` only when no activity guard is alive
     and `now_ms() - idle_since_ms() >= idle_cache_release_ms` — a slot
     is idle a full interval after its last use ENDED.
   - **Additive FFI** `VaultSession::release_idle_cache()` (new) for a
     host that switches vaults: it runs the pass's per-session step on
     this session's two slots without the idle and two-session tests
     (still skipping a held slot), through `registry.rs`, so
     `try_lock_without_activity` keeps its one file of callers.
   - **Its exact-cache facts run alone.** A remedy fact that asserts a
     connection's `used` stays exactly where it was — B's after A's
     release, A's under a held or active slot, a lone session's past the
     interval, A's while public reads keep it busy — shares the
     process-wide `pcache1` LRU with every core test running beside it
     (item 3's reason: a parallel test's connection reaching its own
     `cache_size` can recycle the page the fact counts), and only the
     registry facts serialise. So each such fact is a parent over a child
     through item 3's `run_alone`: `release_idle_cache_drops_this_connections_cache`,
     `a_lone_session_is_never_released`,
     `a_direct_lock_public_read_resets_the_idle_deadline`,
     `a_held_connection_is_never_released_under_its_user` and
     `a_long_operation_earns_a_full_idle_interval_after_it_releases_its_connection`
     each run `<the same path>_child` (new), which carries the fact's
     whole body — its assertions and its mutations unchanged — in a
     process holding nothing else (its own registry, release thread and
     pinned clock included). The remedy facts that assert no exact
     reading stay in the ordinary suite:
     `an_idle_session_releases_its_cache_and_rescans_correctly` (counts
     and `ScanReport`), `the_release_thread_starts_with_the_second_session_and_stops_with_the_last`
     (the thread's life) and the census. The two enabled `perf_guard_`
     facts run alone by name.

   If refuted: no remedy — no `ConnSlot`, no guards, no registry, no
   `release_idle_cache`; the PR ships the bench, the counters (with step
   1's shared worker slot as a plain `Arc<Mutex<Option<Connection>>>`),
   the two always-on facts and the ignored guard's measurement, and the
   issue comment says what the numbers rule out.
5. **Not chosen and why:** unsetting `ENABLE_MEMORY_MANAGEMENT` needs a
   libsqlite3-sys build-flag override in every consumer's build
   (`generate-bindings.ps1`, the Swift build) with no per-session control;
   sizing `cache_size` for a shared LRU penalises the one-session case.
   Both are recorded in the PR body as WD21's alternatives.

## Tests

**Always on (both WD21 branches; nothing here calls a release):**

| Fact | Asserts | Mutation |
|---|---|---|
| `benches/two_sessions_bench.rs` compiles under `cargo bench --workspace --no-run` | the bench target builds | drop the `[[bench]]` entry |
| `two_sessions.rs`: `db_cache_status_reads_this_connections_counters` | session A after a scan: `used > 0`, `miss > 0`; a repeated identical query raises A's `hit`; `worker` is `None` until the worker connection opens in the shared worker slot (through `open_worker_connection_for_tests()`) and `Some` after. Nothing here compares two connections' `used`: that check is the child's (next row), where no parallel test shares the LRU | report the main connection's counters as `worker` before it opens (`Some` too early); reading the process-wide `sqlite3_status64(SQLITE_STATUS_MEMORY_USED)` as `used` is killed by the child's untouched-connection checks |
| `process_cache_status_reads_the_process_counters` (parent) with `process_cache_status_child` (child, new) | the parent runs the child alone in its own process through `run_alone` and asserts exit 0. The child, with sessions A and B open and nothing else in the process: readings `a0`, `b0`, `p0`; B allocates the `wb31_pad` temp table (≥ 8 MiB) → `a1.used == a0.used`, `b1.used - b0.used ≥ 8 MiB`, and both `p1.memory_used - p0.memory_used` and `p1.pagecache_overflow - p0.pagecache_overflow` ≥ `(b1.used - b0.used) / 2`; then A allocates the same → `b2.used == b1.used`, `a2.used - a1.used ≥ 8 MiB`, and both process deltas ≥ `(a2.used - a1.used) / 2` | make `process_cache_status` a per-connection reading — `sqlite3_db_status(SQLITE_DBSTATUS_CACHE_USED)` of A's main connection (the round-7 mutation), or of B's — and the step in which that connection is untouched reads a flat process value; or read `SQLITE_STATUS_PAGECACHE_USED` (the configured-buffer counter, zero here) for `pagecache_overflow`, which stays flat in both steps |
| `perf_guard_two_sessions_do_not_evict_each_others_cache` (`two_sessions.rs`; `#[ignore]` until the remedy; then enabled, and run only alone: every bulk run skips `perf_guard_`) | ratio older-with-newer / older-alone ≤ 1.25; A's `used` holds between rescans | (remedy branch) remove the release |

**Remedy only (they ship with the remedy, and their Verification lines
run only when it did):**

| Fact | Asserts | Mutation |
|---|---|---|
| `two_sessions.rs`: `release_idle_cache_drops_this_connections_cache` (parent; runs `session::tests::two_sessions::release_idle_cache_drops_this_connections_cache_child` alone through `run_alone` and asserts exit 0) with `release_idle_cache_drops_this_connections_cache_child` (new; the body) | in the child, after A's scan, `release_idle_cache()` drops A's `used` to at most a tenth of its value, and B's `used` is unchanged | release through the process-wide `sqlite3_release_memory(i32::MAX)` (B's drops too) |
| `an_idle_session_releases_its_cache_and_rescans_correctly` | row counts and `ScanReport` equal before and after the release | release by closing and reopening the main connection without `register_connection_functions` (the rescan's `slate_tree_sort_key` queries then fail) |
| `registry.rs`: `the_release_thread_starts_with_the_second_session_and_stops_with_the_last` | with `idle_cache_release_ms` opted in: thread absent with one session; present with two; joined after both drop (observed through a test seam with an asserted `Wait(2 s)`, released in `finally`) | leak the thread |
| `a_lone_session_is_never_released` (parent, `run_alone`) with `a_lone_session_is_never_released_child` (new; the body) | in the child: one opted-in session idle past the interval: `used` unchanged | release regardless |
| `a_direct_lock_public_read_resets_the_idle_deadline` (parent, `run_alone`) with `a_direct_lock_public_read_resets_the_idle_deadline_child` (new; the body) | in the child: two opted-in sessions, interval 200 ms: `idle_since_ms()` rises across a call to `indexed_content_hashes` (a direct-`self.conn.lock()` read on main, `session.rs:3347`); calling it and `change_feed_window` (`:3394`) every 50 ms for 600 ms keeps A's `used` unreleased; after the calls stop, A is released within an asserted 2 s wait | stamp activity only in `ConnSlot::lock_cancellable` (the round-5 design: direct-lock reads look idle) |
| `a_held_connection_is_never_released_under_its_user` (parent, `run_alone`) with `a_held_connection_is_never_released_under_its_user_child` (new; the body) | in the child: while the fact holds A's connection through `lock()` past the interval, the release skips A (`try_lock_without_activity` is `None`), and A's `used` is unchanged when the guard drops | make the release path block on `lock()` instead |
| `a_long_operation_earns_a_full_idle_interval_after_it_releases_its_connection` (parent, `run_alone`) with `a_long_operation_earns_a_full_idle_interval_after_it_releases_its_connection_child` (new; the body) | in the child, deterministic, on the pinned clock (`set_test_clock_ms`, reset to `None` in a drop guard; no sleeps, no waits): two opted-in sessions A and B with `idle_cache_release_ms` = I = 1 000, both scanned at clock 0, A's compactor connection opened through `VaultSession::open_worker_connection_for_tests()` (step 1's seam). Three parts, each at a fresh base T (0, 10 I, 20 I), each re-warming the slot it tests with a query at T and recording its `used`. (a) Hold `A.conn.lock()` from T to T + 5 I, running a `release_pass()` at T + 3 I under the hold; after the guard drops at T + 5 I, A's main `used` still equals the recorded value; passes at T + 5 I and T + 6 I − 1 leave it unchanged, and a pass at T + 6 I drops it to at most a tenth. (b) The same through A's worker slot `lock()`, read through `db_cache_status().worker`. (c) Hold `A.conn.activity()` (no lock) from T to T + 5 I, the pass at T + 3 I under it: the same outcomes on A's main `used`. Counters are read only while the fact holds no guard (a read under its own guard would wait on itself), and reading them never moves a deadline (`cache_counters` does not stamp) | stamp only when a guard is taken (round 6's design) → (a) releases at T + 5 I, right after the drop; the worker slot's guard without the drop stamp → (b) releases at T + 5 I; `ActivityGuard` stamping only when created → (c) releases at T + 5 I; the pass ignoring `is_active()` → (c) releases at T + 3 I |
| `only_the_release_path_locks_without_recording_activity` (census) | in `src`, `try_lock_without_activity(` appears only in `session/conn_slot.rs` (its definition) and `session/registry.rs` (its callers: `release_pass` and `release_idle_cache`'s step); every other path to either connection is a `ConnSlot` method whose guard stamps when taken and when dropped, which the private `inner` field enforces at compile time; `cache_counters` returns numbers, never a guard | call `try_lock_without_activity` from a public read |
| `registry.rs`: `perf_guard_the_only_session_in_a_process_is_not_slowed_by_the_release` (run only alone: every bulk run skips `perf_guard_`) | alone-with-registry / alone ≤ 1.10 | release on every tick |
| uniffi: `release_idle_cache_is_additive` | the method exists on the binding and a call on an open session returns | drop the export |

Each parent's child is a no-op in the ordinary suite (`is_alone_child`
is false there) and runs its body only in the process `run_alone` starts
for it, where nothing else opens a connection — so every exact
`used` reading above is taken where no other connection can recycle its
pages, and each mutation still fails its child, which fails its parent
(the child's exit status is asserted). The registry facts that stay in
the ordinary suite serialise on a `static REGISTRY_FACTS: Mutex<()>`
(new, in `registry.rs`'s tests), since the registry and the test clock
are process-wide; a child has the process to itself and takes it too,
harmlessly. The release thread may run its own passes during the
clock-pinned child; a pass's decisions are a function of the pinned
clock, so the thread's passes agree with the fact's. Every wait in these
facts is a bounded wait whose result is asserted, released in `finally`.

## Docs and registers

- `BENCHMARKS.md`: a "Plan 44 WB-31 — two sessions in one process
  (#1484)" entry in the PR 20.0 entry's form (`:1233-1250`), with the
  per-connection counter table and the before/after if the remedy ships;
  amend `:1292-1296` with a pointer.
- `Cargo.toml` `[[bench]]` entry. Remedy branch only, for each of the
  two enabled guards: a `rust.yml` job `env:` name beside `:158-159`
  (`TWO_SESSIONS_PERF_GUARD`, `ONLY_SESSION_RELEASE_PERF_GUARD`), an
  `and not test(${…})` clause in both nextest sweeps (`:254-256`,
  `:265-267`), an isolated step in the `:269-290` form, and a `--skip`
  by name in `windows.yml:337-341`; in the refuted branch the ignored
  guard touches neither file.
- FFI doc for `release_idle_cache()` if it ships (`swift-tests.yml`
  compile).
- Issue: `Refs #1484` with the measurements; WB-0 files the mac follow-up;
  the owner closes by hand after it (WD21).

## Verification

PowerShell, repo root. Each filter is listed first and the block throws
unless every required fact is discovered under it; every command's
`$LASTEXITCODE` is checked (no output grepping for "Failed"). Whether the
remedy shipped is read from the tree (`session/registry.rs` exists only in
the remedy branch), so the remedy-only lines run exactly when it did.

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb31'
function Assert-Ok([string]$step) { if ($LASTEXITCODE -ne 0) { throw "$step failed ($LASTEXITCODE)" } }
function Assert-Facts([string]$crate, [string]$filter, [string[]]$facts, [switch]$Ignored) {
    $listed = if ($Ignored) { cargo test -p $crate --locked --release $filter -- --ignored --list } else { cargo test -p $crate --locked $filter -- --list }
    Assert-Ok "listing $crate $filter"
    foreach ($fact in $facts) {
        if (-not ($listed -match "(^|::)$([regex]::Escape($fact)): test$")) { throw "filter '$filter' does not select $fact" }
    }
}
$built = Select-String -Path "$env:USERPROFILE\.cargo\registry\src\*\libsqlite3-sys-0.38.1\build.rs" -Pattern 'ENABLE_MEMORY_MANAGEMENT'
if (-not $built) { throw 'libsqlite3-sys 0.38.1 build.rs does not name ENABLE_MEMORY_MANAGEMENT: record that instead' }
$built | ForEach-Object { "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }   # record the line in the PR body
cargo bench -p slate-core --bench two_sessions_bench; Assert-Ok 'the bench'   # release, LTO off: the issue's profile
$remedy = Test-Path 'crates/slate-core/src/session/registry.rs'

# always on, both branches:
Assert-Facts 'slate-core' 'two_sessions' @('db_cache_status_reads_this_connections_counters', 'process_cache_status_reads_the_process_counters', 'process_cache_status_child')
cargo test -p slate-core --locked two_sessions -- --skip perf_guard_; Assert-Ok 'two_sessions'   # each parent spawns its child alone in its own process (run_alone); unless SLATE_WB31_CHILD names it, a child is a no-op here; the enabled guard's path contains two_sessions, so the bulk run skips it and it runs alone below
$guard = 'perf_guard_two_sessions_do_not_evict_each_others_cache'
if (-not $remedy) {
    # the guard is still ignored: run it by hand, record its ratio and exit code as data (not a gate; a Windows run that does not reproduce closes nothing)
    Assert-Facts 'slate-core' $guard @($guard) -Ignored
    cargo test -p slate-core --locked --release $guard -- --ignored --nocapture
    "the ignored guard exited $LASTEXITCODE; record it and the printed ratio"
} else {
    # the exact-cache remedy facts are parents whose bodies run alone in a child process (design item 4); list both halves
    $remedyFacts = @('release_idle_cache_drops_this_connections_cache', 'release_idle_cache_drops_this_connections_cache_child', 'an_idle_session_releases_its_cache_and_rescans_correctly')
    Assert-Facts 'slate-core' 'two_sessions' $remedyFacts   # they ran in the two_sessions run above
    $alone = @('a_lone_session_is_never_released', 'a_direct_lock_public_read_resets_the_idle_deadline', 'a_held_connection_is_never_released_under_its_user', 'a_long_operation_earns_a_full_idle_interval_after_it_releases_its_connection')
    Assert-Facts 'slate-core' 'registry' (@('the_release_thread_starts_with_the_second_session_and_stops_with_the_last', 'only_the_release_path_locks_without_recording_activity') + $alone + @($alone | ForEach-Object { "${_}_child" }))
    cargo test -p slate-core --locked registry -- --skip perf_guard_; Assert-Ok 'registry'   # the second guard lives in registry.rs's tests: alone below, never in this bulk run
    $guards = [ordered]@{ 'TWO_SESSIONS_PERF_GUARD' = $guard; 'ONLY_SESSION_RELEASE_PERF_GUARD' = 'perf_guard_the_only_session_in_a_process_is_not_slowed_by_the_release' }
    foreach ($name in $guards.Keys) {
        $perf = $guards[$name]
        Assert-Facts 'slate-core' $perf @($perf)
        cargo test -p slate-core --locked --release $perf -- --test-threads=1; Assert-Ok $perf   # alone: the filter is the guard's own name
        # CI excludes it from every bulk run and isolates it (design item 3):
        $rust = Get-Content -Raw .github/workflows/rust.yml
        if (-not $rust.Contains("${name}: session::")) { throw "rust.yml defines no $name" }
        if (([regex]::Matches($rust, [regex]::Escape("and not test(`${$name})"))).Count -ne 2) { throw "rust.yml's two nextest sweeps do not both exclude $name" }
        if (([regex]::Matches($rust, [regex]::Escape("-E `"test(=`${$name})`""))).Count -ne 1) { throw "rust.yml has no isolated step for $name" }
        if (-not (Get-Content -Raw .github/workflows/windows.yml).Contains("--skip $perf")) { throw "windows.yml does not skip $perf" }
    }
    Assert-Facts 'slate-uniffi' 'release_idle_cache' @('release_idle_cache_is_additive')
    cargo test -p slate-uniffi --locked release_idle_cache; Assert-Ok 'uniffi release_idle_cache'
}
cargo test -p slate-core --locked --no-fail-fast -- --skip perf_guard_; Assert-Ok 'the whole core suite'   # every perf_guard_ runs alone (WB-31's by name above; the two existing ones in rust.yml's isolated steps; windows.yml:337-341 skips them by name), so the whole-suite run holds no timing or exact-cache reading
cargo fmt --all -- --check; Assert-Ok 'fmt'
cargo clippy --all-targets --workspace -- -D warnings; Assert-Ok 'clippy'
cargo test --workspace --locked -- --skip perf_guard_; Assert-Ok 'workspace tests'   # the workspace run re-runs every enabled guard otherwise; they ran alone above
cargo bench --workspace --no-run; Assert-Ok 'bench compile'
& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh; Assert-Ok 'licence headers'   # Git Bash by full path: a bare `bash` in PowerShell here resolves to WSL's C:\WINDOWS\system32\bash.exe
```

The registry facts sit in `session/registry.rs`'s test module (the
`registry` filter); the two-session facts in `session/tests/two_sessions.rs`
(registered in `session.rs`'s test module list, `:22300-22305`). Each
exact-cache parent and its `_child` sit in the same module, so one
filter discovers and runs both halves; the child runs its body only in
the process its parent starts, so the whole-suite run executes each
child as a no-op and each parent's body in a process of its own. No C#
changes (the FFI method is additive; bindings regenerate in CI).
Mutation run per row, locally. CI: `rust.yml` (bench-compile, tests, and
— remedy branch — the two guards excluded from both nextest sweeps by
name and run in their own isolated steps), `windows.yml` rust-tests
(the guards skipped by name once enabled); `swift-tests.yml` only if the
FFI method ships. AT: none.

## Acceptance

- [ ] WD21 recorded; the bench exists, compiles in CI, and its table
      (Windows, this machine, per-connection counters) is in
      `BENCHMARKS.md`; the `build.rs` line is recorded.
- [ ] Both branches: `db_cache_status`, `process_cache_status` and their
      always-on facts (the per-connection fact, and the process-counter
      parent with its child run alone in its own process), which release
      nothing, discovered and green, each seen failing under its
      per-connection or process-wide substitution;
      the compactor's connection in the session's shared worker slot,
      taken per job; the guard's measured ratio in its doc and the PR
      body.
- [ ] The mechanism is proven or refuted by the counters named in
      Reference, stated in the PR body and on #1484.
- [ ] If proven: `ConnSlot` is the only way to the session's main and
      worker connections (the seven `lock_cancellable` sites migrated,
      every direct `self.conn.lock()` and every compactor job now
      recorded when its guard is taken and when it drops, scans held
      under `activity()`), an operation longer than the interval is
      released no sooner than a full interval after it lets go (the
      clock-pinned fact), the registry, the release thread,
      `release_idle_cache`, the remedy-only facts and the enabled guards
      are green, the guards are in `windows.yml`'s skip list and
      `rust.yml`'s isolated steps; every remedy fact that asserts an
      exact cache reading (`release_idle_cache_drops_this_connections_cache`,
      `a_lone_session_is_never_released`,
      `a_direct_lock_public_read_resets_the_idle_deadline`,
      `a_held_connection_is_never_released_under_its_user`,
      `a_long_operation_earns_a_full_idle_interval_after_it_releases_its_connection`)
      runs its body in its `_child` alone through `run_alone`, both
      halves discovered by name; each enabled guard runs only alone —
      every local bulk run (the `two_sessions` and `registry` filters,
      the whole core suite, `cargo test --workspace`) passes `-- --skip
      perf_guard_`, `rust.yml`'s two nextest sweeps exclude each by its
      job `env:` name and an isolated step runs each, and
      `windows.yml:337-341` skips each by name (the Verification block's
      workflow checks pass); WB-0 files the mac follow-up.
- [ ] If refuted: no remedy ships (no `ConnSlot`, registry or FFI); the
      guard stays ignored with its measurement; the issue stays open with
      the numbers.
- [ ] `Refs #1484` only; never closed by this PR.

## Brief

Reproduce or refute #1484 on Windows with SQLite's per-connection
counters, then act only on proof. Pending decision: WD21 (recommended:
conditional — if the bench proves the shared LRU, release the idle
session's cache; if it refutes it, the issue stays open with the
measurements; a Windows bench that does not reproduce closes nothing;
#1484 closes only after a Mac measurement; under the other answers —
unset `ENABLE_MEMORY_MANAGEMENT`, or size `cache_size` — the remedy
commit is replaced by the build-flag override in every consumer's build
or by a per-session `cache_size` rule, gated by the same before/after
bench). Read `db.rs:236-260`, `:395-432`, `session.rs:198-210`,
`:255-290`, `:1288-1295` (`conn: Mutex<Connection>`), `:1600-1610`,
`:1665-1690` (`compaction_worker_loop` and its local `worker_conn`),
`:1745-1860` (the compactor's uses of it), `:2624-2645`, `:2695-2729`
(the compactor's spawn and the session's construction), `:3334-3400`
(three public reads that lock `self.conn` directly), `:16960-16970`
(`finish_structural_move`'s guard parameter), `:22300-22305` (the test
module list),
`session/trash_cancellation.rs:16-37` (the generic `lock_cancellable`,
also used for `structural_operation`), `session/directory_page.rs:361`,
`session/watch.rs:14-22` (no tick exists), `vault/trash_handoff.rs:610-620`
(the `cfg(test)` default precedent), `benches/scan_bench.rs:25-100`,
`benches/common/mod.rs:95-130`, `BENCHMARKS.md:1233-1300`,
`link_pass.rs:370-470`, `rust.yml:150-165`, `:265-292`,
`windows.yml:335-341`, `Cargo.toml:22-30`. The connection census: 98
direct `.conn.lock()` sites (97 in `session.rs`, one in
`directory_page.rs`) and 7 `lock_cancellable(&self.conn, cancel)` sites;
one function names the guard type, `finish_structural_move(&self, mut
conn: std::sync::MutexGuard<'_, Connection>, …)` (`:16965`). The worker
connection is the oplog compactor's, a local `Option<Connection>` of
`compaction_worker_loop` (`:1686`) on the `slate-oplog-compactor`
thread, which no session field reaches today.

Build, both branches: the counter seams `VaultSession::db_cache_status()
-> DbCacheStatus` (new; `CACHE_USED`, `CACHE_USED_SHARED`, `CACHE_HIT`,
`CACHE_MISS`, `CACHE_WRITE`, `CACHE_SPILL` for the main connection and
`worker: Option<…>`) and `db::process_cache_status() ->
ProcessCacheStatus` (new; `MEMORY_USED`, `PAGECACHE_OVERFLOW`); the
compactor's connection moved into a new session field `worker_conn:
Arc<Mutex<Option<Connection>>>` created before the compactor spawns and
passed to `compaction_worker_loop` as a new parameter, locked per job
(never across `rx.recv()`), opened into the `Option` on first need as
today, so `db_cache_status` can read it (main first, dropped, then the
worker; never both held), and the `#[cfg(test)]` seam
`VaultSession::open_worker_connection_for_tests()` (new; opens it into
the slot through `open_worker_connection`, as the compactor does);
`benches/two_sessions_bench.rs` with its
`[[bench]]` entry; the child-process harness `run_alone(child)` and
`is_alone_child(child)` (new, `#[cfg(test)] pub(crate)` in `session.rs`;
described with the facts below), through which every fact asserting an
exact cache or process reading runs its body alone; the always-on facts
and the ignored guard. Then, only
if A's `CACHE_USED` falls and `CACHE_MISS` rises while B is open and the
process total plateaus near one cache, the remedy, whose rule is that a
connection's idle interval starts when a use ENDS (otherwise an
operation longer than the interval is protected while it holds the
connection and released on the first tick after it lets go, discarding
the cache it just warmed): `session/conn_slot.rs`'s `ConnSlot<C =
Connection>` (new; private `inner: Mutex<C>`, `last_activity_ms:
AtomicI64`, `active: AtomicUsize`) and `ConnGuard<'a, C>` (new; derefs to
`C`; its `Drop` stamps before the inner guard unlocks); `lock() ->
LockResult<ConnGuard<'_, C>>`, stamping when taken and, through the
guard, when dropped, so every direct site is recorded at both ends
unchanged (`finish_structural_move` takes `ConnGuard<'_>`);
`lock_cancellable(cancel) -> Result<ConnGuard<'_, C>, VaultError>`, to
which the seven sites migrate; `activity() -> ActivityGuard<'_, C>`
(new; counts as in use and stamps when created and when dropped), held
by `scan_initial` and `rescan` for their whole run;
`try_lock_without_activity() -> Option<MutexGuard<'_, C>>` for the
release path alone, `None` while the connection is held;
`cache_counters()`, numbers read without stamping, for
`db_cache_status`; `is_active()`; `idle_since_ms()`; time from
`conn_slot::now_ms()` (new; monotonic ms since a process-wide
`LazyLock<Instant>`; `#[cfg(test)] set_test_clock_ms(Option<i64>)` pins
it). `VaultSession.conn: ConnSlot` and `worker_conn:
Arc<ConnSlot<Option<Connection>>>`, so every compactor job is a
`ConnGuard` too. `session/registry.rs` (`SessionRegistry` of weak
handles to both slots, registering only sessions whose
`SessionConfig.idle_cache_release_ms` (new; 30 000 desktop, 0 = never, 0
under `cfg(test)`) is nonzero; the `slate-cache-release` thread parked on
a condvar, started with the second registered session and joined after
the last, running `release_pass()` (new, `pub(super)`), which while two
or more sessions are registered takes each slot by
`try_lock_without_activity` and, re-reading under that guard, releases it
only when no activity guard is alive and `now_ms() - idle_since_ms() >=
idle_cache_release_ms`); and the additive FFI `release_idle_cache()`,
running the pass's per-session step on this session's slots (no idle or
two-session test, a held slot still skipped) through `registry.rs`. If
refuted: none of the remedy ships; the shared worker slot stays a plain
`Arc<Mutex<Option<Connection>>>`.

Facts (mutation in brackets), seen failing first.
Always on, both branches:
- `db_cache_status_reads_this_connections_counters`: after a scan A's
  `used > 0` and `miss > 0`, a repeated query raises `hit`; `worker` is
  `None` until `open_worker_connection_for_tests()` opens the worker
  connection in the shared slot and `Some` after [`worker` reported
  before it opens]. It compares no two connections: with
  `SQLITE_ENABLE_MEMORY_MANAGEMENT` all purgeable caches share one LRU,
  so a parallel test can evict a session's page, and the exact
  per-connection checks live in the isolated child below.
- The child-process harness (new, `#[cfg(test)] pub(crate)` in
  `session.rs`, reachable from `session::tests::two_sessions` and
  `session::registry::tests`): `run_alone(child: &str)` spawns
  `std::env::current_exe()` with `[child, "--exact", "--nocapture",
  "--test-threads=1"]` and `SLATE_WB31_CHILD` set to `child`, and
  asserts a successful exit with the child's stdout and stderr in the
  failure message (the `real_trash_linux_child` form,
  `vault/trash_handoff_tests.rs:1108-1154`); `is_alone_child(child: &str)
  -> bool` is true only when `SLATE_WB31_CHILD` equals `child`, and each
  child opens with `if !is_alone_child("<its own full path>") { return; }`,
  so the ordinary suite runs every child as a no-op.
- `process_cache_status_reads_the_process_counters` (parent) and
  `process_cache_status_child` (child, new): process-wide counters move
  with any parallel test's connections, so the parent calls
  `run_alone("session::tests::two_sessions::process_cache_status_child")`
  and asserts exit 0; the child opens A
  and B with `common::make_vault`, then B allocates — under B's
  connection lock, `CREATE TEMP TABLE wb31_pad(x BLOB)` and a recursive
  CTE inserting 1 024 rows of `zeroblob(8192)`, ≥ 8 MiB of in-memory
  temp pages (`temp_store MEMORY`, `db.rs:424`) that no LRU evicts and
  whose growth evicts nothing — while A is untouched: A's `used` exactly
  equal, B's up by ≥ 8 MiB, both `memory_used` and `pagecache_overflow`
  up by at least half of B's growth; then A allocates while B is
  untouched, symmetrically [a per-connection reading of A's main
  connection (the round-7 mutation) or of B's fails the step in which
  that connection is untouched; `PAGECACHE_USED` for
  `pagecache_overflow` stays flat; the process-wide `MEMORY_USED`
  reported as `db_cache_status`'s `used` moves the untouched
  connection's reading].
- `perf_guard_two_sessions_do_not_evict_each_others_cache`
  (`two_sessions.rs`): ignored with its measured ratio until the remedy;
  then ratio ≤ 1.25 and A's `used` held, run only alone — its path
  contains `two_sessions`, so every bulk run skips `perf_guard_` [remove
  the release].

Remedy only. Each fact below that asserts an exact `used` reading is a
parent over a child of the same path plus `_child` (new), run through
`run_alone`: the child carries the whole body, assertions and mutations
unchanged, in a process where no other test's connection can recycle
its pages through the shared `pcache1` LRU (only the registry facts
serialise, and other core tests open connections beside them). The
others stay in the ordinary suite.
- `release_idle_cache_drops_this_connections_cache` (parent) with
  `release_idle_cache_drops_this_connections_cache_child`: A's `used`
  drops to at most a tenth, B's unchanged [process-wide
  `sqlite3_release_memory`].
- `an_idle_session_releases_its_cache_and_rescans_correctly`: counts and
  `ScanReport` equal across the release [reopen without UDFs].
- `the_release_thread_starts_with_the_second_session_and_stops_with_the_last`:
  absent with one, present with two, joined after both drop (asserted
  `Wait(2 s)`, released in `finally`) [leak the thread].
- `a_lone_session_is_never_released` (parent) with
  `a_lone_session_is_never_released_child`: one opted-in session idle
  past the interval keeps its `used` [release regardless].
- `a_direct_lock_public_read_resets_the_idle_deadline` (parent) with
  `a_direct_lock_public_read_resets_the_idle_deadline_child`:
  `indexed_content_hashes` (direct lock, `session.rs:3347`) raises
  `idle_since_ms()`; calls to it and `change_feed_window` every 50 ms keep
  A unreleased past a 200 ms interval; A is released within an asserted
  2 s once they stop [stamp only in `lock_cancellable`].
- `a_held_connection_is_never_released_under_its_user` (parent) with
  `a_held_connection_is_never_released_under_its_user_child`: a pass while
  the child holds A's `lock()` skips A, whose `used` is unchanged when the
  guard drops [block on `lock()` in the release].
- `a_long_operation_earns_a_full_idle_interval_after_it_releases_its_connection`
  (parent) with
  `a_long_operation_earns_a_full_idle_interval_after_it_releases_its_connection_child`
  (the body; deterministic: the clock pinned by `set_test_clock_ms`,
  reset in a drop guard; no sleeps): two opted-in sessions with I = 1 000 scanned at
  clock 0, A's compactor connection opened by
  `VaultSession::open_worker_connection_for_tests()`; at bases T = 0,
  10 I, 20 I, re-warm the slot under test at T, record its `used`, and
  hold (a) `A.conn.lock()`, (b) A's worker slot `lock()`, (c)
  `A.conn.activity()` from T to T + 5 I with a `release_pass()` at
  T + 3 I under the hold; after the drop its `used` still equals the
  recorded value, passes at T + 5 I and T + 6 I − 1 leave it, and a pass
  at T + 6 I drops it to at most a tenth (counters read only while the
  fact holds no guard) [stamp only when taken → released at T + 5 I; the
  worker guard without its drop stamp → (b) at T + 5 I; `ActivityGuard`
  stamping only when created → (c) at T + 5 I; the pass ignoring
  `is_active()` → (c) at T + 3 I].
- `only_the_release_path_locks_without_recording_activity` (census):
  `try_lock_without_activity(` only in `conn_slot.rs` (definition) and
  `registry.rs` (`release_pass` and `release_idle_cache`'s step); every
  other door is a stamping guard; `cache_counters` returns numbers
  [call it from a public read].
- `perf_guard_the_only_session_in_a_process_is_not_slowed_by_the_release`
  (`registry.rs`'s tests, run only alone; the `registry` bulk run skips
  `perf_guard_`): ≤ 1.10 [release every tick].
- uniffi `release_idle_cache_is_additive` [drop the export].

Registers: `BENCHMARKS.md` (the WB-31 entry with the counter table, and
the `:1292-1296` pointer), `Cargo.toml`; in the remedy branch only, for
each enabled guard (`perf_guard_two_sessions_do_not_evict_each_others_cache`
in `session/tests/two_sessions.rs`,
`perf_guard_the_only_session_in_a_process_is_not_slowed_by_the_release`
in `session/registry.rs`'s tests): a `rust.yml` job `env:` name beside
`:158-159` (`TWO_SESSIONS_PERF_GUARD`, `ONLY_SESSION_RELEASE_PERF_GUARD`,
each the guard's full test path), an `and not test(${…})` clause in both
nextest sweeps (`:254-256`, `:265-267`), an isolated step in the
`:269-290` form (`--no-tests=fail --test-threads 1 -E "test(=${…})"`),
and a `--skip` by name in `windows.yml:337-341`'s slate-core run
(`rust.yml:294`'s doctests and `windows.yml:346`'s `slate-cli` run need
nothing); and the FFI doc. Not delivered: the macOS measurement (WB-0's
follow-up). Done when the Verification block runs clean in PowerShell —
the `build.rs` line recorded, every required fact discovered by name
(the process-counter child and, in the remedy branch, the five
exact-cache children included), every bulk run — `cargo test -p
slate-core --locked two_sessions`, (remedy) `… registry`, the whole core
suite and `cargo test --workspace --locked` — passing `-- --skip
perf_guard_`, because both guards' paths contain those filters and the
workspace run reruns every enabled guard (each guard runs alone instead:
WB-31's two by name with `--release`, the existing two in `rust.yml`'s
isolated steps), the block's workflow checks finding each enabled guard
excluded from both `rust.yml` sweeps, isolated once, and skipped in
`windows.yml`, the license-header check run through Git Bash by full
path (`& "$env:ProgramFiles\Git\bin\bash.exe"`; a bare `bash` here is
WSL), every command's
`$LASTEXITCODE` 0, the remedy-only lines run exactly when
`session/registry.rs` exists, the ignored guard's ratio recorded in the
refuted branch — the bench's table is in `BENCHMARKS.md`, the rust and
Windows lanes are green, each mutation was run locally (each mutation
of a child fact fails its child, and so its parent), and #1484
carries the Windows verdict with `Refs`. Commit:
`perf(core): a two-session bench reads sqlite's per-connection cache counters and an idle session releases its page cache when the shared lru is proven`.
