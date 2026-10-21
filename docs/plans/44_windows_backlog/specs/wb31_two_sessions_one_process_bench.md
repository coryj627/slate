# WB-31 — Two sessions in one process: the shared page cache is reproduced or refuted on Windows, and the remedy waits for the answer

Issues: Refs #1484 (closes by hand after a macOS measurement confirms
the remedy, or the owner withdraws the macOS observation — WD21; §5
closure matrix). Size M (diagnose first). Lanes: rust. AT: none. Wave:
any free slot. Depends on: nothing. Parallel with: anything (touches
`db.rs`, `session.rs`'s config, `benches/` and new test modules; the
remedy branch also retypes `session.rs`'s `conn` field and rewrites its
seven `lock_cancellable(&self.conn, …)` lines, leaving the 98 direct
`self.conn.lock()` lines textually unchanged).
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
  can hold four connections.
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
  name.
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
  connection's only acquisition path nor specific to it. The session
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
   pagecache_overflow }` (new, `db.rs`).
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
     `process_cache_status_reads_the_process_counters`;
   - **the timing guard**, `perf_guard_two_sessions_do_not_evict_each_others_cache`
     — `#[ignore]` **until the remedy ships**, with the measured ratio in
     its doc (in the refuted branch it stays ignored, its doc holding the
     measurement); the remedy's commit removes the `#[ignore]` and the
     fact asserts the ratio older-with-newer / older-alone ≤ 1.25 and that
     A's `CACHE_USED` holds between rescans.

   A `perf_guard_` fact joins `windows.yml:337-341`'s `--skip` list and
   `rust.yml:158-159`'s isolated steps only when it is enabled (the remedy
   branch); while ignored it runs only by hand, as Verification does.
4. **The remedy, conditional (WD21), with its own scheduler.** If the
   counters prove the shared LRU:
   - **One way to the connection, recording activity.**
     `session/conn_slot.rs` (new): `pub(crate) struct ConnSlot { inner:
     Mutex<Connection>, last_activity_ms: AtomicI64 }` with
     `fn lock(&self) -> LockResult<MutexGuard<'_, Connection>>` (stamps
     activity, then `inner.lock()` — the same return type as
     `Mutex::lock`, so the 98 direct `self.conn.lock()` sites and the
     253 test sites compile unchanged and are all recorded),
     `fn lock_cancellable(&self, cancel: &CancelToken) ->
     Result<MutexGuard<'_, Connection>, VaultError>` (stamps activity,
     then the generic helper's loop), `pub(super) fn
     try_lock_without_activity(&self) -> Option<MutexGuard<'_,
     Connection>>` (the release path's only door: `None` while anyone
     holds the connection — a held connection is in use, so the release
     skips it rather than wait and then free a cache a long scan has
     just warmed) and `fn idle_since_ms(&self) -> i64`. `VaultSession.conn` becomes `ConnSlot`
     (`session.rs:1291`, `:2729`); `inner` is private to `conn_slot.rs`,
     so the type system is the census: nothing reaches the
     `Mutex<Connection>` except through these methods. Migration: the
     seven `lock_cancellable(&self.conn, cancel)` sites become
     `self.conn.lock_cancellable(cancel)` (the generic helper stays for
     `structural_operation`). Scan and rescan starts also stamp (the
     worker connection is the scan's own).
   - **The registry and its thread.** A process-wide `SessionRegistry`
     (new, `session/registry.rs`:
     `LazyLock<Mutex<Vec<Weak<SessionCacheHandle>>>>`, each entry a weak
     handle to a session's `ConnSlot` and its worker connection) that
     `open_in` registers in — only when `idle_cache_release_ms` is
     nonzero — and `Drop` removes from; a single lazily started daemon
     thread `slate-cache-release` (new; parked on a condvar with a
     deadline, like the watcher's, cancelled and joined when the registry
     empties) wakes every `SessionConfig.idle_cache_release_ms` (new;
     default 30 000 desktop, 0 = never; **0 under `cfg(test)`**, as
     `default_trash` gives core's test build its scratch bin, so only the
     facts that opt in register and the rest of the suite never shares
     the registry) and, through `try_lock_without_activity`, calls
     `sqlite3_db_release_memory` on the main and worker connections of
     every session idle longer than the interval **only while two or more
     sessions are registered** (a lone session is never released, so the
     one-session case is untouched by construction).
   - **Additive FFI** `VaultSession::release_idle_cache()` (new) for a
     host that switches vaults.

   If refuted: no remedy — no `ConnSlot`, no registry, no
   `release_idle_cache`; the PR ships the bench, the counters, the two
   always-on facts and the ignored guard's measurement, and the issue
   comment says what the numbers rule out.
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
| `two_sessions.rs`: `db_cache_status_reads_this_connections_counters` | session A after a scan: `used > 0`, `miss > 0`; a repeated identical query raises A's `hit`; a freshly opened session B in the same process reads `used` below A's (the counters are per connection); `worker` is `None` until the worker connection opens and `Some` after | read the process-wide `sqlite3_status64` for both connections (A and B then read equal) |
| `process_cache_status_reads_the_process_counters` | `memory_used > 0` with a session open, and it rises across A's first scan | return the main connection's `used` instead |
| `perf_guard_two_sessions_do_not_evict_each_others_cache` (`#[ignore]` until the remedy; then enabled) | ratio older-with-newer / older-alone ≤ 1.25; A's `used` holds between rescans | (remedy branch) remove the release |

**Remedy only (they ship with the remedy, and their Verification lines
run only when it did):**

| Fact | Asserts | Mutation |
|---|---|---|
| `two_sessions.rs`: `release_idle_cache_drops_this_connections_cache` | after A's scan, `release_idle_cache()` drops A's `used` to at most a tenth of its value, and B's `used` is unchanged | release through the process-wide `sqlite3_release_memory(i32::MAX)` (B's drops too) |
| `an_idle_session_releases_its_cache_and_rescans_correctly` | row counts and `ScanReport` equal before and after the release | release by closing and reopening the main connection without `register_connection_functions` (the rescan's `slate_tree_sort_key` queries then fail) |
| `registry.rs`: `the_release_thread_starts_with_the_second_session_and_stops_with_the_last` | with `idle_cache_release_ms` opted in: thread absent with one session; present with two; joined after both drop (observed through a test seam with an asserted `Wait(2 s)`, released in `finally`) | leak the thread |
| `a_lone_session_is_never_released` | one opted-in session idle past the interval: `used` unchanged | release regardless |
| `a_direct_lock_public_read_resets_the_idle_deadline` | two opted-in sessions, interval 200 ms: `idle_since_ms()` rises across a call to `indexed_content_hashes` (a direct-`self.conn.lock()` read on main, `session.rs:3347`); calling it and `change_feed_window` (`:3394`) every 50 ms for 600 ms keeps A's `used` unreleased; after the calls stop, A is released within an asserted 2 s wait | stamp activity only in `ConnSlot::lock_cancellable` (the round-5 design: direct-lock reads look idle) |
| `a_held_connection_is_never_released_under_its_user` | while a fact holds A's connection through `lock()` past the interval, the release skips A (`try_lock_without_activity` is `None`), and A's `used` is unchanged when the guard drops | make the release path block on `lock()` instead |
| `only_the_release_path_locks_without_recording_activity` (census) | in `src`, `try_lock_without_activity(` appears only in `session/conn_slot.rs` (its definition) and `session/registry.rs` (its one caller); every other path to the connection is a `ConnSlot` method that stamps activity, which the private `inner` field enforces at compile time | call `try_lock_without_activity` from a public read |
| `perf_guard_the_only_session_in_a_process_is_not_slowed_by_the_release` | alone-with-registry / alone ≤ 1.10 | release on every tick |
| uniffi: `release_idle_cache_is_additive` | the method exists on the binding and a call on an open session returns | drop the export |

The registry facts serialise on a `static REGISTRY_FACTS: Mutex<()>`
(new, in `registry.rs`'s tests), since the registry is process-wide.
Every wait in these facts is a bounded wait whose result is asserted,
released in `finally`.

## Docs and registers

- `BENCHMARKS.md`: a "Plan 44 WB-31 — two sessions in one process
  (#1484)" entry in the PR 20.0 entry's form (`:1233-1250`), with the
  per-connection counter table and the before/after if the remedy ships;
  amend `:1292-1296` with a pointer.
- `Cargo.toml` `[[bench]]` entry. Remedy branch only:
  `windows.yml:337-341`'s `--skip` list and `rust.yml:158-159`,
  `:269-290`'s isolated steps gain the two enabled guards; in the refuted
  branch the ignored guard touches neither file.
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
Assert-Facts 'slate-core' 'two_sessions' @('db_cache_status_reads_this_connections_counters', 'process_cache_status_reads_the_process_counters')
cargo test -p slate-core --locked two_sessions; Assert-Ok 'two_sessions'
$guard = 'perf_guard_two_sessions_do_not_evict_each_others_cache'
if (-not $remedy) {
    # the guard is still ignored: run it by hand, record its ratio and exit code as data (not a gate; a Windows run that does not reproduce closes nothing)
    Assert-Facts 'slate-core' $guard @($guard) -Ignored
    cargo test -p slate-core --locked --release $guard -- --ignored --nocapture
    "the ignored guard exited $LASTEXITCODE; record it and the printed ratio"
} else {
    $remedyFacts = @('release_idle_cache_drops_this_connections_cache', 'an_idle_session_releases_its_cache_and_rescans_correctly')
    Assert-Facts 'slate-core' 'two_sessions' $remedyFacts
    Assert-Facts 'slate-core' 'registry' @('the_release_thread_starts_with_the_second_session_and_stops_with_the_last', 'a_lone_session_is_never_released', 'a_direct_lock_public_read_resets_the_idle_deadline', 'a_held_connection_is_never_released_under_its_user', 'only_the_release_path_locks_without_recording_activity')
    cargo test -p slate-core --locked registry; Assert-Ok 'registry'
    foreach ($perf in $guard, 'perf_guard_the_only_session_in_a_process_is_not_slowed_by_the_release') {
        Assert-Facts 'slate-core' $perf @($perf)
        cargo test -p slate-core --locked --release $perf; Assert-Ok $perf
    }
    Assert-Facts 'slate-uniffi' 'release_idle_cache' @('release_idle_cache_is_additive')
    cargo test -p slate-uniffi --locked release_idle_cache; Assert-Ok 'uniffi release_idle_cache'
}
cargo test -p slate-core --locked --no-fail-fast; Assert-Ok 'the whole core suite'
cargo fmt --all -- --check; Assert-Ok 'fmt'
cargo clippy --all-targets --workspace -- -D warnings; Assert-Ok 'clippy'
cargo test --workspace --locked; Assert-Ok 'workspace tests'
cargo bench --workspace --no-run; Assert-Ok 'bench compile'
bash scripts/check-license-headers.sh; Assert-Ok 'licence headers'
```

The registry facts sit in `session/registry.rs`'s test module (the
`registry` filter); the two-session facts in `session/tests/two_sessions.rs`
(registered in `session.rs`'s test module list, `:22300-22305`). No C#
changes (the FFI method is additive; bindings regenerate in CI).
Mutation run per row, locally. CI: `rust.yml` (bench-compile, tests, and
— remedy branch — the two guards isolated), `windows.yml` rust-tests
(the guards skipped by name once enabled); `swift-tests.yml` only if the
FFI method ships. AT: none.

## Acceptance

- [ ] WD21 recorded; the bench exists, compiles in CI, and its table
      (Windows, this machine, per-connection counters) is in
      `BENCHMARKS.md`; the `build.rs` line is recorded.
- [ ] Both branches: `db_cache_status`, `process_cache_status` and their
      two always-on facts, which release nothing, discovered and green;
      the guard's measured ratio in its doc and the PR body.
- [ ] The mechanism is proven or refuted by the counters named in
      Reference, stated in the PR body and on #1484.
- [ ] If proven: `ConnSlot` is the only way to the session's connection
      (the seven `lock_cancellable` sites migrated, every direct
      `self.conn.lock()` now recorded), the registry, the release thread,
      `release_idle_cache`, the remedy-only facts and the enabled guards
      are green, the guards are in `windows.yml`'s skip list and
      `rust.yml`'s isolated steps; WB-0 files the mac follow-up.
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
`:2624-2645`, `:2729`, `:3334-3400` (three public reads that lock
`self.conn` directly), `:22300-22305` (the test module list),
`session/trash_cancellation.rs:16-37` (the generic `lock_cancellable`,
also used for `structural_operation`), `session/directory_page.rs:361`,
`session/watch.rs:14-22` (no tick exists), `vault/trash_handoff.rs:610-620`
(the `cfg(test)` default precedent), `benches/scan_bench.rs:25-100`,
`benches/common/mod.rs:95-130`, `BENCHMARKS.md:1233-1300`,
`link_pass.rs:370-470`, `rust.yml:150-165`, `:265-292`,
`windows.yml:335-341`, `Cargo.toml:22-30`. The connection census: 98
direct `.conn.lock()` sites (97 in `session.rs`, one in
`directory_page.rs`) and 7 `lock_cancellable(&self.conn, cancel)` sites.

Build, both branches: the counter seams `VaultSession::db_cache_status()
-> DbCacheStatus` (new; `CACHE_USED`, `CACHE_USED_SHARED`, `CACHE_HIT`,
`CACHE_MISS`, `CACHE_WRITE`, `CACHE_SPILL` for the main connection and
`worker: Option<…>`) and `db::process_cache_status() ->
ProcessCacheStatus` (new; `MEMORY_USED`, `PAGECACHE_OVERFLOW`);
`benches/two_sessions_bench.rs` with its `[[bench]]` entry; the always-on
facts and the ignored guard. Then, only if A's `CACHE_USED` falls and
`CACHE_MISS` rises while B is open and the process total plateaus near
one cache, the remedy: `session/conn_slot.rs`'s `ConnSlot` (new; private
`inner: Mutex<Connection>` and `last_activity_ms`; `lock()` with
`Mutex::lock`'s return type, stamping activity, so every direct site is
recorded unchanged; `lock_cancellable(cancel)`, to which the seven sites
migrate; `try_lock_without_activity()` for the release path alone,
`None` while the connection is held; `idle_since_ms()`), with
`VaultSession.conn: ConnSlot`; `session/registry.rs` (`SessionRegistry`
of weak handles, registering only sessions whose
`SessionConfig.idle_cache_release_ms` (new; 30 000 desktop, 0 = never, 0
under `cfg(test)`) is nonzero; the `slate-cache-release` thread parked on
a condvar, started with the second registered session and joined after
the last, releasing only idle, unheld sessions and only while two or
more are registered); and the additive FFI `release_idle_cache()`. If
refuted: none of the remedy ships.

Facts (mutation in brackets), seen failing first.
Always on, both branches:
- `db_cache_status_reads_this_connections_counters`: after a scan A's
  `used > 0` and `miss > 0`, a repeated query raises `hit`, a fresh B
  reads `used` below A's, `worker` is `None` then `Some` [read the
  process-wide status for both].
- `process_cache_status_reads_the_process_counters`: `memory_used > 0`
  and rising across A's first scan [return a connection's `used`].
- `perf_guard_two_sessions_do_not_evict_each_others_cache`: ignored with
  its measured ratio until the remedy; then ratio ≤ 1.25 and A's `used`
  held [remove the release].

Remedy only:
- `release_idle_cache_drops_this_connections_cache`: A's `used` drops to
  at most a tenth, B's unchanged [process-wide `sqlite3_release_memory`].
- `an_idle_session_releases_its_cache_and_rescans_correctly`: counts and
  `ScanReport` equal across the release [reopen without UDFs].
- `the_release_thread_starts_with_the_second_session_and_stops_with_the_last`:
  absent with one, present with two, joined after both drop (asserted
  `Wait(2 s)`, released in `finally`) [leak the thread].
- `a_lone_session_is_never_released` [release regardless].
- `a_direct_lock_public_read_resets_the_idle_deadline`:
  `indexed_content_hashes` (direct lock, `session.rs:3347`) raises
  `idle_since_ms()`; calls to it and `change_feed_window` every 50 ms keep
  A unreleased past a 200 ms interval; A is released within an asserted
  2 s once they stop [stamp only in `lock_cancellable`].
- `a_held_connection_is_never_released_under_its_user` [block on `lock()`
  in the release].
- `only_the_release_path_locks_without_recording_activity` (census):
  `try_lock_without_activity(` only in `conn_slot.rs` and `registry.rs`
  [call it from a public read].
- `perf_guard_the_only_session_in_a_process_is_not_slowed_by_the_release`:
  ≤ 1.10 [release every tick].
- uniffi `release_idle_cache_is_additive` [drop the export].

Registers: `BENCHMARKS.md` (the WB-31 entry with the counter table, and
the `:1292-1296` pointer), `Cargo.toml`; in the remedy branch only,
`windows.yml:337-341`'s skip list and `rust.yml:158-159`/`:269-290`'s
isolated steps for the two enabled guards, and the FFI doc. Not
delivered: the macOS measurement (WB-0's follow-up). Done when the
Verification block runs clean in PowerShell — the `build.rs` line
recorded, every required fact discovered by name, every command's
`$LASTEXITCODE` 0, the remedy-only lines run exactly when
`session/registry.rs` exists, the ignored guard's ratio recorded in the
refuted branch — the bench's table is in `BENCHMARKS.md`, the rust and
Windows lanes are green, each mutation was run locally, and #1484
carries the Windows verdict with `Refs`. Commit:
`perf(core): a two-session bench reads sqlite's per-connection cache counters and an idle session releases its page cache when the shared lru is proven`.
