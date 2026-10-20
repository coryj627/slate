# WB-31 — Two sessions in one process: the shared page cache is reproduced or refuted on Windows, and the remedy waits for the answer

Issues: Refs #1484 (closes by hand after a macOS measurement confirms
the remedy, or the owner withdraws the macOS observation — WD21; §5
closure matrix). Size M (diagnose first). Lanes: rust. AT: none. Wave:
any free slot. Depends on: nothing. Parallel with: anything (touches
`db.rs`, `benches/` and a new test module only). Decision: WD21
(recommended and consumed: conditional remedy; a Windows bench that does
not reproduce closes nothing).

## Goal

A bench anyone can run in one process opens two identical 10k-note
vaults and measures the older session's no-change rescan with the newer
one open and closed, reading SQLite's own page-cache counters per
connection, so the mechanism #1484 suspects — one process-wide page-cache
LRU from `SQLITE_ENABLE_MEMORY_MANAGEMENT` — is proven or refuted with
numbers. If proven, the idle session releases its cache
(`sqlite3_db_release_memory` on idle), which keeps the bundled build and
the one-session case untouched. If refuted, the issue stays open with the
measurements and the diagnosis continues, because the two-session
slowdown is the defect, not the hypothesis.

## Current state on main

Verified against the worktree; the plan's lines hold.

- **The pragma site.** `db.rs:398` `apply_pragmas(conn, cache_size_pages)`:
  `journal_mode` WAL, `synchronous NORMAL`, `temp_store MEMORY`,
  `foreign_keys ON`, then `:429` `cache_size` in pages (positive). Callers
  `open_database` (`:242`, used at `session.rs:2641` with
  `config.max_db_cache_pages`) and `open_in_memory` (`:307`).
  `SessionConfig.max_db_cache_pages` (`session.rs:203`, "Desktop default
  4096 (~32 MB); mobile 512", default `:262`, test `:283`). A session
  opens a second, worker connection lazily (`open_worker_connection`
  `:1600`, used `:1752-1753`), so a process with two sessions can hold
  four connections.
- **The build.** `Cargo.toml:25-28`: rusqlite 0.40 `bundled` → libsqlite3-sys
  0.38.1 (`Cargo.lock:1254`), bundled SQLite 3.53.x. The issue's claim that
  its `build.rs` passes `-DSQLITE_ENABLE_MEMORY_MANAGEMENT` is from the
  issue, not re-read here: verify in
  `%USERPROFILE%\.cargo\registry\src\*\libsqlite3-sys-0.38.1\build.rs`
  (grep `ENABLE_MEMORY_MANAGEMENT`) as the bench's first step and record
  the line. No `LIBSQLITE3_FLAGS` or `.cargo/config.toml` override exists.
- **The evidence on record.** `BENCHMARKS.md:1292-1296` (under the PR 20.0
  entry, `:1233`): "With a second session open in the process, the older
  one's rescans lose their SQLite page cache and run about 1.8× slower
  (#1484), so a comparison of two open vaults would read 0.67 or 1.89
  depending on which opened first." The perf guard
  `link_pass.rs:450` `perf_guard_unresolved_link_rescan_within_twice_the_link_free_one`
  opens one session at a time because of it; `rust.yml:279-290` runs it
  isolated (`UNRESOLVED_LINK_PERF_GUARD`, `:159`); `windows.yml:337-341`
  skips both perf guards.
- **Benches.** `crates/slate-core/benches/scan_bench.rs`:
  `bench_first_open_and_scan` (`:38`), `bench_reopen_with_cache` (`:73`),
  with `benches/common/mod.rs` `generate_vault(file_count)` (`:95`),
  `generate_linked_vault` (`:332`). `rust.yml:304-392` `bench-compile`
  runs `cargo bench --no-run` (PR: LTO off; main: full profile). Makefile
  `ci` includes `bench-check` (`:74`).
- **The counters SQLite offers** (libsqlite3-sys symbols, callable on
  `Connection::handle()`): `sqlite3_db_status(db, SQLITE_DBSTATUS_CACHE_USED
  | CACHE_HIT | CACHE_MISS | CACHE_WRITE, …)` per connection;
  `sqlite3_status(SQLITE_STATUS_PAGECACHE_USED | PAGECACHE_OVERFLOW)`
  process-wide; `sqlite3_db_release_memory(db)` and
  `sqlite3_release_memory(n)` (the latter only with
  `ENABLE_MEMORY_MANAGEMENT`); `sqlite3_soft_heap_limit64`. rusqlite
  exposes none directly; `unsafe` FFI calls through
  `rusqlite::ffi` are the route (as `db/anchored.rs` already does for the
  VFS).

## Reference

None: no host twin; the behavioural spec is #1484's text and WD21's
text (§3). The numbers on record are macOS (M5 Max: 173 ms against 96 ms
for the older session; the newer 88–120 ms; a third session makes the
first slow again). A Windows bench reproducing a slowdown of the same
shape with the counters showing the older connection's `CACHE_MISS`
rising when the newer opens is the proof; the remedy's acceptance is the
older session's rescan back within noise of its alone figure while the
newer's is unchanged.

## Design

1. **The bench** `benches/two_sessions_bench.rs` (new; registered in
   `Cargo.toml` `[[bench]]`): generate two identical 10k vaults with
   `common::generate_vault`; open A, scan, rescan 8× (median); open B,
   scan; rescan A 8×; rescan B 8×; close B; rescan A 8×; open C; repeat.
   Per rescan record `SQLITE_DBSTATUS_CACHE_HIT`, `CACHE_MISS`,
   `CACHE_USED` for each session's main connection (through a new
   `#[doc(hidden)] pub fn db_cache_status(&self) -> CacheStatus`
   (new, `session.rs`; `cfg(any(test, feature = "bench-probes"))` or
   plain `#[doc(hidden)]` — pick the existing convention for
   `perf_guard` seams) and `SQLITE_STATUS_PAGECACHE_USED` process-wide.
   Print a table; criterion's groups carry the medians.
2. **The diagnosis fact** `session/tests/two_sessions.rs` (new, run only
   under `perf_guard_` naming so the lanes skip it like the others,
   `windows.yml:339-341`): `perf_guard_two_sessions_do_not_evict_each_others_cache`
   — asserts the ratio older-with-newer / older-alone ≤ 1.25 (the
   tolerance of the other guards) **only after the remedy ships**; before
   it, the fact is `#[ignore]` with the measured ratio in its doc, so the
   red/green of the remedy is visible.
3. **The remedy, conditional (WD21):** if the counters prove the shared
   LRU (A's `CACHE_MISS` rises when B opens; `PAGECACHE_USED` plateaus at
   one cache's worth), add `VaultSession::release_idle_cache()` (new):
   `sqlite3_db_release_memory` on the main and worker connections, called
   by the session when it has been idle for a configured interval
   (`SessionConfig.idle_cache_release_ms`, new, default 30 s desktop,
   `0` = never) from the existing housekeeping tick the watcher gate runs
   on (`session/watch.rs`'s timer), and exposed additively on the FFI as
   `release_idle_cache()` for a host that switches vaults. The one-session
   case is untouched: an idle session that is the only one releases its
   cache and re-reads on the next rescan, which the bench must show costs
   under the alone figure's noise for a warm OS file cache; if it does
   not, the release becomes conditional on a second open session in the
   process (a process-wide `AtomicUsize` session count, new).
   If refuted (A slows without B evicting its pages — e.g. WAL checkpoint
   contention, or the structural-lock registry), stop: the PR ships the
   bench, the counters and the facts' measurements, and the issue
   comment says what the numbers rule out.
4. **Not chosen and why:** unsetting `ENABLE_MEMORY_MANAGEMENT` needs a
   libsqlite3-sys build flag override for every consumer (hosts build
   core through their own toolchains: `generate-bindings.ps1`, the Swift
   build) — a build-system change with no per-session control; sizing
   `cache_size` for a shared LRU penalises the one-session case. Both are
   recorded in the PR body as the alternatives WD21 lists.

## Tests

| Fact | Asserts | Mutation |
|---|---|---|
| `benches/two_sessions_bench.rs` (compiles under `cargo bench --no-run`) | — | — |
| `session/tests/two_sessions.rs`: `db_cache_status_reads_this_connections_counters` | after a scan, `CACHE_USED > 0`; after `release_idle_cache`, `CACHE_USED` drops to the pinned pages | call `sqlite3_release_memory(0)` instead |
| `perf_guard_two_sessions_do_not_evict_each_others_cache` (ignored until the remedy) | ratio ≤ 1.25 | remove the release |
| `an_idle_session_releases_its_cache_and_rescans_correctly` | row counts and `ScanReport` equal before and after the release | — |
| `the_only_session_in_a_process_is_not_slowed_by_the_release` (perf_guard) | alone-with-release / alone ≤ 1.10 | release on every tick |
| uniffi: `release_idle_cache_is_additive` (compile + call) | | |

Rust snake_case; `perf_guard_` names so the lanes' skips apply (add the
new guard to `rust.yml:158-159`'s isolated steps if it should run there,
as the other two do).

## Docs and registers

- `BENCHMARKS.md`: a "Plan 44 WB-31 — two sessions in one process
  (#1484)" entry in the PR 20.0 entry's form (`:1233-1250`: what changed,
  machine, toolchain, profile, the table, the command), with the counters
  table and the before/after if the remedy ships; amend `:1292-1296`'s
  sentence with a pointer.
- `Cargo.toml` `[[bench]]` entry; `rust.yml` isolated step if added.
- FFI doc for `release_idle_cache()` if it ships (`swift-tests.yml`
  compile).
- Issue: `Refs #1484` with the measurements; WB-0 files the mac follow-up
  ("measure WB-31's remedy on the Mac Studio"); the owner closes by hand
  after it (WD21).

## Verification

```
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb31'
grep -n ENABLE_MEMORY_MANAGEMENT $env:USERPROFILE\.cargo\registry\src\*\libsqlite3-sys-0.38.1\build.rs   # record the line
cargo bench -p slate-core --bench two_sessions_bench                  # release, LTO off: the issue's profile
cargo test -p slate-core --locked two_sessions
cargo test -p slate-core --locked --release -- --ignored perf_guard_two_sessions   # after the remedy
cargo test -p slate-core --locked --no-fail-fast
make ci                                                              # bench-check compiles the new bench
```

CI: `rust.yml` (bench-compile, tests), `windows.yml` rust-tests;
`swift-tests.yml` only if the FFI method ships. AT: none.

## Acceptance

- [ ] The bench exists, compiles in CI, and its table (Windows, this
      machine) is in `BENCHMARKS.md` with the counters per connection.
- [ ] The mechanism is proven (counters) or refuted (counters), stated in
      the PR body and on #1484.
- [ ] If proven: `release_idle_cache` ships, the two-session guard is
      green, the one-session guard is green, and the macOS follow-up is
      filed by WB-0 for the close.
- [ ] If refuted: no remedy ships; the issue stays open with the numbers.
- [ ] `Refs #1484` only; the issue is never closed by this PR.

## Brief

Reproduce or refute #1484 on Windows with SQLite's own counters, then
act only on proof. Read `db.rs:236-260`, `:395-432`,
`session.rs:198-210`, `:255-290`, `:1600-1610`, `:2636-2645`,
`benches/scan_bench.rs:25-100`, `benches/common/mod.rs:95-130`,
`BENCHMARKS.md:1233-1300`, `link_pass.rs:370-470` (the perf-guard
shape), `rust.yml:150-165`, `:265-292`, `windows.yml:335-341`,
`Cargo.toml:22-30`. Check the libsqlite3-sys build flag, write the bench
and the counter seam, run it, and record the table. If the counters show
the shared LRU, add the idle release with its guards and the additive
FFI method; otherwise ship the bench and the numbers. Registers:
`BENCHMARKS.md`, the `[[bench]]` entry. Done when the bench's table is in
`BENCHMARKS.md`, the lanes are green, and #1484 carries the Windows
verdict with `Refs`. Commit:
`perf(core): a two-session bench reads sqlite's cache counters and an idle session releases its page cache when the shared lru is proven`.
