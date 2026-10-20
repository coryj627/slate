# WB-31 — Two sessions in one process: the shared page cache is reproduced or refuted on Windows, and the remedy waits for the answer

Issues: Refs #1484 (closes by hand after a macOS measurement confirms
the remedy, or the owner withdraws the macOS observation — WD21; §5
closure matrix). Size M (diagnose first). Lanes: rust. AT: none. Wave:
any free slot. Depends on: nothing. Parallel with: anything (touches
`db.rs`, `session.rs`'s config, `benches/` and new test modules only).
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
3. **The diagnosis facts** `session/tests/two_sessions.rs` (new):
   `db_cache_status_reads_this_connections_counters` (always on) and
   `perf_guard_two_sessions_do_not_evict_each_others_cache` — `#[ignore]`
   **until the remedy ships**, with the measured ratio in its doc; the
   remedy's commit removes the `#[ignore]` and the fact asserts the ratio
   older-with-newer / older-alone ≤ 1.25 and that A's `CACHE_USED` holds
   between rescans. The two timing facts carry the `perf_guard_` prefix
   and are added to `windows.yml:337-341`'s `--skip` list and to
   `rust.yml:158-159`'s isolated steps, as the existing guards are.
4. **The remedy, conditional (WD21), with its own scheduler.** If the
   counters prove the shared LRU: a process-wide `SessionRegistry` (new,
   `session/registry.rs`: `LazyLock<Mutex<Vec<Weak<SessionCacheHandle>>>>`,
   each entry a weak handle to a session's connections and an
   `AtomicI64` last-activity instant) that `open_in` registers in and
   `Drop` removes from; **activity** = every public `VaultSession` method
   that takes `self.conn` (recorded at the one lock-acquisition helper,
   `lock_cancellable(&self.conn, …)`) and every scan or rescan start; a
   single lazily started daemon thread `slate-cache-release` (new; parked
   on a condvar with a deadline, like the watcher's, cancelled and joined
   when the registry empties) wakes every `SessionConfig.idle_cache_release_ms`
   (new; default 30 000 desktop, 0 = never) and calls
   `sqlite3_db_release_memory` on the main and worker connections of every
   session idle longer than the interval **only while two or more sessions
   are registered** (a lone session is never released, so the one-session
   case is untouched by construction). Additive FFI
   `VaultSession::release_idle_cache()` (new) for a host that switches
   vaults. If refuted: no remedy; the PR ships the bench, the counters and
   the facts' measurements, and the issue comment says what the numbers
   rule out.
5. **Not chosen and why:** unsetting `ENABLE_MEMORY_MANAGEMENT` needs a
   libsqlite3-sys build-flag override in every consumer's build
   (`generate-bindings.ps1`, the Swift build) with no per-session control;
   sizing `cache_size` for a shared LRU penalises the one-session case.
   Both are recorded in the PR body as WD21's alternatives.

## Tests

| Fact | Asserts | Mutation |
|---|---|---|
| `benches/two_sessions_bench.rs` compiles under `cargo bench --workspace --no-run` | — | — |
| `two_sessions.rs`: `db_cache_status_reads_this_connections_counters` | after a scan, `used > 0` and `miss > 0`; after `release_idle_cache`, `used` drops to the pinned pages | call `sqlite3_release_memory(0)` instead |
| `perf_guard_two_sessions_do_not_evict_each_others_cache` (`#[ignore]` until the remedy; then enabled) | ratio ≤ 1.25; A's `used` holds between rescans | remove the release |
| `an_idle_session_releases_its_cache_and_rescans_correctly` | row counts and `ScanReport` equal before and after the release | — |
| `registry.rs`: `the_release_thread_starts_with_the_second_session_and_stops_with_the_last` | thread absent with one session; present with two; joined after both drop (observed through a test seam with an asserted `Wait(2 s)`, released in `finally`) | leak the thread |
| `a_lone_session_is_never_released` | one session idle past the interval: `used` unchanged | release regardless |
| `activity_resets_the_idle_clock` | a read at T resets; release only after T + interval | ignore reads |
| `perf_guard_the_only_session_in_a_process_is_not_slowed_by_the_release` | alone-with-registry / alone ≤ 1.10 | release on every tick |
| uniffi: `release_idle_cache_is_additive` (compile + call) | | |

Every wait in these facts is a bounded wait whose result is asserted,
released in `finally`.

## Docs and registers

- `BENCHMARKS.md`: a "Plan 44 WB-31 — two sessions in one process
  (#1484)" entry in the PR 20.0 entry's form (`:1233-1250`), with the
  per-connection counter table and the before/after if the remedy ships;
  amend `:1292-1296` with a pointer.
- `Cargo.toml` `[[bench]]` entry; `windows.yml:337-341` and
  `rust.yml:158-159`, `:269-290` for the two new guards.
- FFI doc for `release_idle_cache()` if it ships (`swift-tests.yml`
  compile).
- Issue: `Refs #1484` with the measurements; WB-0 files the mac follow-up;
  the owner closes by hand after it (WD21).

## Verification

PowerShell, repo root. Each `cargo test` line's `-- --list` form must
print ≥ 1 before the run.

```
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb31'
Select-String -Path "$env:USERPROFILE\.cargo\registry\src\*\libsqlite3-sys-0.38.1\build.rs" -Pattern 'ENABLE_MEMORY_MANAGEMENT'   # record the line
cargo bench -p slate-core --bench two_sessions_bench                       # release, LTO off: the issue's profile
cargo test -p slate-core --locked two_sessions                            # the always-on facts
cargo test -p slate-core --locked registry
# before the remedy (the guard is still ignored):
cargo test -p slate-core --locked --release -- --ignored --list perf_guard_two_sessions_do_not_evict_each_others_cache | Select-String ': test$' | Measure-Object | Select-Object -ExpandProperty Count   # 1
cargo test -p slate-core --locked --release -- --ignored perf_guard_two_sessions_do_not_evict_each_others_cache   # expected red: the ratio in its doc
# after the remedy (the ignore marker removed):
cargo test -p slate-core --locked --release perf_guard_two_sessions_do_not_evict_each_others_cache
cargo test -p slate-core --locked --release perf_guard_the_only_session_in_a_process_is_not_slowed_by_the_release
cargo test -p slate-core --locked --no-fail-fast
cargo fmt --all -- --check
cargo clippy --all-targets --workspace -- -D warnings
cargo test --workspace --locked
cargo bench --workspace --no-run
bash scripts/check-license-headers.sh
```

No C# changes (the FFI method is additive; bindings regenerate in CI).
Mutation run per row, locally. CI: `rust.yml` (bench-compile, tests, the
two guards isolated), `windows.yml` rust-tests (the guards skipped by
name); `swift-tests.yml` only if the FFI method ships. AT: none.

## Acceptance

- [ ] WD21 recorded; the bench exists, compiles in CI, and its table
      (Windows, this machine, per-connection counters) is in
      `BENCHMARKS.md`.
- [ ] The mechanism is proven or refuted by the counters named in
      Reference, stated in the PR body and on #1484.
- [ ] If proven: the registry, the release thread, `release_idle_cache`
      and the enabled guards are green; the lone-session facts green;
      WB-0 files the mac follow-up.
- [ ] If refuted: no remedy ships; the issue stays open with the numbers.
- [ ] `Refs #1484` only; never closed by this PR.

## Brief

Reproduce or refute #1484 on Windows with SQLite's per-connection
counters, then act only on proof. Pending decision: WD21 (recommended:
release an idle session's cache only if the bench proves the shared LRU;
else the issue stays open; alternatives are the build-flag override or a
`cache_size` rule, gated by the same bench). Read `db.rs:236-260`,
`:395-432`, `session.rs:198-210`, `:255-290`, `:1600-1610`, `:2624-2645`,
`session/watch.rs:14-22` (no tick exists), `benches/scan_bench.rs:25-100`,
`benches/common/mod.rs:95-130`, `BENCHMARKS.md:1233-1300`,
`link_pass.rs:370-470`, `rust.yml:150-165`, `:265-292`,
`windows.yml:335-341`, `Cargo.toml:22-30`. Build: the counter seams
`VaultSession::db_cache_status` (`CACHE_USED`, `CACHE_USED_SHARED`,
`CACHE_HIT`, `CACHE_MISS`, `CACHE_WRITE`, `CACHE_SPILL` per connection)
and `db::process_cache_status` (`MEMORY_USED`, `PAGECACHE_OVERFLOW`);
`benches/two_sessions_bench.rs` with its `[[bench]]` entry; then, only
if A's `CACHE_USED` falls and `CACHE_MISS` rises while B is open and the
process total plateaus near one cache, the remedy: `session/registry.rs`
(`SessionRegistry`, activity recorded at the connection-lock helper and
scan starts, the `slate-cache-release` thread parked on a condvar,
started with the second session and joined after the last, releasing
only idle sessions and only while two or more are registered),
`SessionConfig.idle_cache_release_ms`, and the additive FFI
`release_idle_cache()`. Facts, seen failing first:
`db_cache_status_reads_this_connections_counters`;
`perf_guard_two_sessions_do_not_evict_each_others_cache` (ignored until
the remedy, then enabled, ratio ≤ 1.25 and `used` held);
`an_idle_session_releases_its_cache_and_rescans_correctly`;
`the_release_thread_starts_with_the_second_session_and_stops_with_the_last`;
`a_lone_session_is_never_released`; `activity_resets_the_idle_clock`;
`perf_guard_the_only_session_in_a_process_is_not_slowed_by_the_release`;
`release_idle_cache_is_additive`. Registers: `BENCHMARKS.md` (the WB-31
entry and the `:1292-1296` pointer), `Cargo.toml`, `windows.yml:337-341`
and `rust.yml:158-159`/`:269-290` for the two guards. Not delivered: the
macOS measurement (WB-0's follow-up). Done when the bench's table is in
`BENCHMARKS.md`, the PowerShell gates and the rust and Windows lanes are
green, each mutation was run locally, and #1484 carries the Windows
verdict with `Refs`. Commit:
`perf(core): a two-session bench reads sqlite's per-connection cache counters and an idle session releases its page cache when the shared lru is proven`.
