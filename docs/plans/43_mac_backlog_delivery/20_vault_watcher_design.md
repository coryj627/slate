# PR 20 — A real vault watcher in core: design

Status: design, 2026-10-16, with the owner's decisions of 2026-10-17
in §11. PR 20.0 (§7, #1429), PR 20a (§7, the core watcher) and PR 20b
(§7, the Windows host) are delivered; PR 20c, the mac host, is not. Issue #1267 (OD-1's
follow-up; AR-3, AR-7). Spec:
`specs/pr20_vault_watcher.md`. Baseline: `main` at 13624900; every
file:line reference below was read at that commit (several mac lines
have moved since the program's baseline 709ba8a2, and the current
lines are given). PR 19 is not implemented, so the mac half is designed
against `specs/pr19_mac_rescan_baseline.md`, not its code. The two
spikes behind §3 lived in a scratch directory and were not committed.

## 1. Summary

1. **Backend.** `notify` pinned at `=9.0.0-rc.5`: FSEvents on macOS,
   ReadDirectoryChangesW on Windows, one recursive watch on the vault
   root, filtered by the scanner's own hidden-path rule, debounced in
   core (1 s quiet, 10 s ceiling, 5 s spacing), overflow taken from
   notify's `Flag::Rescan`. 8.2.0, the latest stable, loses Windows
   overflows silently.
2. **Granularity.** A burst triggers the existing full incremental
   rescan, the R-9 run, with a new `Watcher` reason. Slate's own writes
   are dropped by a provider-owned echo ledger. **Prerequisite (PR
   20.0):** the scan's unresolved-link pass costs unresolved links ×
   files on every scan (4.6 s for a no-change rescan of 10k notes with
   20,000 links to notes not yet written) and must be bounded first.
3. **Threading.** notify's thread only fills a bounded burst set; a
   session-owned coalescer thread, like the compaction worker, calls
   the host once per burst through a new default-no-op
   `VaultEventListener::on_external_change()`, with no core lock held.
4. **PR 22.** FSEvents follows the pathname (measured), so the watcher
   verifies the root's identity at arm time and before every callback
   and ends itself on a mismatch. Its events are hints that only
   request a rescan; they never carry content. PR 22 replaces the
   anchor with the held descriptor.
5. **Announcements.** A watcher run speaks on the foreground policy,
   at most one watcher sentence per 60 s, none while a modal surface
   is open, and no watcher run while the app is inactive (the
   foreground rescan then speaks for that time, as today).
6. **Fallback.** Refresh and the foreground rescan never turn off.
   `start_watching()` returning false, an overflow, or an ended watch
   changes nothing the user hears or sees.
7. **Tests.** Core facts on inotify (rust lane) and
   ReadDirectoryChangesW (the Windows lane's `cargo test -p
   slate-core`); FSEvents through the mac host's integration fact;
   coalescing, echo, overflow and root facts on a fake clock; host
   twins on both hosts, a FlaUI journey on Windows.

Plan (§7): PR 20.0 (core, the link pass, S–M), PR 20a (core watcher and
FFI, L), then PR 20b (Windows, M) and PR 20c (mac, M, after PR 19) in
parallel.

## 2. What stands on main

- `VaultProvider::watch` (`crates/slate-core/src/vault/provider.rs:
  221-224`) promises a refresh-on-foreground fallback when it returns
  `Ok(None)`; `FileEventSink` and `FileEvent` are at `:309-323`, the
  opaque `WatchHandle` at `:325-341`. None of them crosses the FFI.
- `FsVaultProvider::watch` returns `Ok(None)` (`vault/fs.rs:975-980`),
  pinned by `watch_returns_none_in_milestone_a` (`:1860-1870`).
  **Nothing calls `watch`**: the only callers are test wrappers that
  delegate to an inner provider (`session/tests/*.rs`).
- Locked 05: §2.4 picks `notify` for desktop (`:115`); §4 principle 5
  is host-registered callbacks and no long-lived Rust threads calling
  the host independently (`:170`); §4.4's normative paragraph keeps
  file-change events to Slate's own writes and calls the watcher a stub
  (`:338-347`).
- Listener dispatch: `register_event_listener` and the
  `listener_snapshot` seat (`session.rs:4547-4580`),
  `notify_file_change` (`:4582`), `notify_index_phase` (`:4607`). The
  compaction worker is a session-owned thread that calls `on_error`
  through that registry (`:1554`) and is joined in `Drop`
  (`:1816-1833`).
- The rescan: `rescan_with_progress` (`session.rs:3139`) →
  `scan_session` (`:3181`, connection mutex taken at `:3192` for the
  whole run) → `scan_vault` (`:9965`): one IMMEDIATE transaction
  (`:10033`, `db.rs:485`), the hidden-name skip (`:10105`), the
  fast path in `index_file` (`:10514-10616`: a stat, a prior-row read
  and one UPDATE per unchanged file), one completeness predicate for
  both prunes (`:10194`), the unresolved-link pass (`:10243`), the
  canvas pass (`:10284`), the commit (`:10307`).
- Root identity: `VaultRootIdentity` and `observe_root_identity`
  (`session.rs:1031-1051`, Unix only; Windows returns `None`,
  `:1053-1060`), `pin_root_identity` (`:1062-1089`);
  `from_filesystem_with_actor` pins, opens, re-verifies and then drops
  the descriptor (`:3012-3091`).
- FFI: `rescan` (`crates/slate-uniffi/src/lib.rs:1327`),
  `register_event_listener` (`:1347`), the foreign
  `VaultEventListener` and its adapter (`:5364-5397`, whose doc says
  "The filesystem watcher is a stub", `:5372`).
- Windows: the lattice and blockers (`VaultLifecycleViewModel.Rescan.
  cs:81-161`), the follow-up loop (`:163-198`), the run and its one
  sentence (`:271-348`), blocked follow-ups (`:905-922`);
  `ForegroundRescanRoute.cs:32-76`, wired at `MainWindow.xaml.cs:
  143-147`; the open (`VaultLifecycleViewModel.cs:480-545`: listener,
  `ScanInitialWithProgress`, `InitializeWorkspace`,
  `StartSyncMarkerWatch`); `UiVaultEventListener` (`:2014-2035`).
- Mac: no rescan yet (PR 19). `VaultEventAdapter`
  (`AppState+History.swift:82-115`), `registerVaultEventListener`
  (`:733-766`), the open scan in a detached task under one token
  (`AppState.swift:11718-11745`), `didBecomeActiveNotification`
  (`SlateMacApp.swift:1083-1088`).
- Copy: `RescanReason { Explicit, Foreground }` (`a11y.rs:589-600`),
  `VaultRescanFinished` / `VaultRescanIncomplete` (`:718-731`,
  rendered at `:1999-2011`, golden at `:6678`); corpus rows
  (`tests/fixtures/a11y/corpus.json:203-213`); the mac census
  (`A11yCorpusCensusTests.swift:92-96`).
- Precedents: core's bibliography debouncer
  (`citations/bibliography.rs:256-305`: 500 ms trailing, no ceiling,
  never wired); both hosts' sync-marker watchers
  (`SyncMarkerWatcher.swift:5-55`, `SyncMarkerWatcher.cs:65`, `:125`:
  a 2.5 s debounce, a 10 s ceiling after #638 found that a pure
  trailing debounce starves under continuous churn, and arm-then-probe
  to close the setup race).

## 3. Evidence

### 3.1 The notify versions

From crates.io and the published crates' source, read 2026-10-16.

| Version | Date | Relevant facts |
|---|---|---|
| 8.2.0 (latest stable) | 2025-08-03 | Windows completion routine ignores the byte count, so an overflowed buffer is parsed as empty and lost; `ERROR_NOTIFY_ENUM_DIR` ends the watch with a log line (`windows.rs` `handle_event`). FSEvents callback panics on unknown flag bits and on non-UTF-8 paths inside an `extern "C"` callback, which aborts the process. `stop()` spins until `CFRunLoopIsWaiting`. Calls the root-only `FSEventsPurgeEventsForDeviceUpToEventId`. No `WatchRoot`. windows-sys 0.60 (the workspace has 0.61). |
| 9.0.0-rc.5 | 2026-08-30 | Windows overflow surfaces as `Flag::Rescan` and the watch re-arms (#964, `completion_rescan_event`); a deleted watched directory emits `Remove(Folder)`. FSEvents callback catches panics and keeps raw bytes (#790, rc.2); unknown flags tolerated (`from_bits_truncate`); the run loop stops on a flag instead of spinning, and the purge call is gone (#795, rc.2); `WatchRoot` set, so a renamed or replaced root reports "root changed"; events keep the watched path's spelling (rc.4). windows-sys 0.61. MSRV 1.88 (the workspace pins 1.97.1). |

New crates for slate-core with rc.5: `notify` (CC0-1.0),
`notify-types` (MIT OR Apache-2.0), `objc2-core-foundation` and
`objc2-core-services` (macOS; Zlib OR Apache-2.0 OR MIT),
`xxhash-rust` (BSL-1.0), and on Linux `inotify`/`inotify-sys` (ISC) and
`mio` (MIT). `walkdir`, `same-file`, `winapi-util`, `bitflags`,
`windows-sys` 0.61 and `log` are already locked. All are permissive.

The notify wiki's Event Guide (via the context7 docs tool) records the
platform rules this design relies on: on FSEvents a removed watched
object keeps its path watched and dropped events surface as a
`Rescan` meta-event; on Windows data and metadata writes are both
`Modify(Any)`, and a renamed watched directory keeps being watched
with events prefixed by the old path. Microsoft's
ReadDirectoryChangesW reference says an overflowed buffer completes
successfully with zero bytes and its contents discarded, that
`ERROR_NOTIFY_ENUM_DIR` means the caller must enumerate, that the
buffer may not exceed 64 KB over the network (notify uses 16 KiB,
`windows.rs:42`), and that an unsupported redirector fails with
`ERROR_INVALID_FUNCTION`.

### 3.2 FSEvents spike (macOS)

A scratch crate on notify 9.0.0-rc.5, run on the Mac Studio (M5 Max,
macOS 27.0.1), watching a temp directory under `/var/folders` (a
symlinked path).

| Action | What arrived |
|---|---|
| Arm | Two historical events for the root itself. Every path carried the watched spelling `/var/...`, never `/private/var/...`. |
| Create `a.md` | Create(File), Modify(Metadata(Extended)), Modify(Data(Content)). |
| Rewrite `a.md` | Create(File) again, Metadata(Any), Metadata(Extended), Data(Content): flags are cumulative per path. |
| Atomic save as `FsVaultProvider` does it: write `.slate/tmp/.tmpXYZ`, rename onto `a.md` | Four events for the temp path and six for `a.md`. Slate's own save is visible. |
| `mkdir Folder`, write `Folder/b.md` | Events for both paths. |
| Rename `Folder` to `Renamed` | Events for `Folder` and `Renamed` only, none for `Renamed/b.md`. |
| Create `.hidden.md` | Delivered: hidden-path filtering is the consumer's. |
| Delete `a.md` | Modify(Name(Any)) and Remove(File). |
| Set `Renamed/b.md`'s mtime | Modify(Metadata(Any)) only. |
| Rename the watched root away | Remove(Any), info "root changed". |
| Create a new directory at the watched path | Remove(Any) "root changed", Create(Folder). |
| Write into that swapped-in directory | Delivered. |
| Write into the original, moved directory | Nothing. |
| Block the handler 15 s while writing 50,000 files | 1,444 events in total; one was `Other` with `need_rescan() == true`, info "rescan: user dropped", path = the watched root. |
| Any event | `process_id` unset: notify does not ask FSEvents to mark own-process events. |

### 3.3 ReadDirectoryChangesW

No Windows machine was used in this pass; the facts come from notify
9.0.0-rc.5's `windows.rs` and the documents in §3.1. notify opens one
directory handle with `FILE_LIST_DIRECTORY`, all three share modes and
`FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OVERLAPPED`, watches the
subtree, and filters on file and directory names, attributes, size,
last write, creation and security (`windows.rs:330`, `:452-486`).
The handle follows the directory object, not the path. The Windows
facts in §4.7 run on the Windows lane's `cargo test -p slate-core`
(`.github/workflows/windows.yml:319-321`).

### 3.4 What a rescan costs today

A scratch binary depending on slate-core by path, release profile
without LTO, the Mac Studio above, about 1 KiB notes with frontmatter
across 50 directories (the scan bench's layout), median of seven
no-change rescans on an open session.

| Fixture | Cold scan | No-change rescan | One external change | All `list_files(OpenableDocuments)` pages |
|---|---|---|---|---|
| 10k notes, no links | 0.61 s | 66 ms | 71 ms | 17 ms |
| 10k notes, 7 resolvable links each | 20.0 s | 87 ms | 90 ms | 21 ms |
| 5k notes, 2 unresolvable links each | 1.76 s | 1.46 s | — | — |
| 10k notes, 2 unresolvable links each | 5.28 s | 4.63 s | 4.52 s | 19 ms |
| 50k notes, no links | 3.83 s | 385 ms | 383 ms | 142 ms |

`save_text` took 18 to 20 ms at 10k and 34 ms at 50k, and a rescan
right after Slate's own save is a full rescan unless the echo is
filtered (75 to 91 ms at 10k, 409 ms at 50k). The May baseline in
`BENCHMARKS.md:141` (open plus no-change scan, 53 ms at 10k, 269 ms at
50k) is the same order for link-free vaults.

**The unresolved-link pass dominates any vault with links to notes not
written yet.** The pass (`links_db.rs:236-282`, called on every scan at
`session.rs:10243`) re-resolves every unresolved link, and
`resolve_link` offers every indexed path to the selector for each link
(`link_resolver.rs:283-285`), so its cost is unresolved links × files:
doubling both took 1.46 s to 4.63 s above. A `sample` of the link-heavy
50k cold scan put 2,304 of 2,316 samples under
`re_resolve_unresolved_links_under` → `resolve_link` →
`LinkCandidateSelector::offer` / `basename_matches`; the run was
stopped after six minutes. The same per-link scan runs for every link
of every changed file (`replace_links_for_file`, `links_db.rs:129`)
and of every save (`session.rs:4811`). Today's Windows Refresh and
foreground rescans already pay this on such vaults; a watcher would pay
it once per burst. The 10k cold scan with resolvable links (20 s on
this build without LTO) is also above the 15 s first-open gate (05
§9.5), because on a cold scan every link is unresolved until the pass;
the scan bench's notes carry no links (`benches/common/mod.rs:140-180`),
so no bench measures it.

## 4. Decisions

### 4.1 Backend, scope, filter, debounce, overflow

**Chosen.**

- `notify = "=9.0.0-rc.5"` with default features (`macos_fsevent`)
  and nothing else, through `notify::recommended_watcher`: FSEvents on
  macOS, ReadDirectoryChangesW on Windows, inotify on Linux (the rust CI
  lane and `slate-cli` builds; Slate has no Linux host, and the CLI
  never starts a watch). The exact pin moves to 9.0.0 in a one-line
  follow-up when it ships. The workspace already pins pre-releases
  where the stable line does not fit (`ropey 2.0.0-beta.1`, `mathcat
  0.7.6-beta.4`).
- One recursive watch on the provider's root, the exact path the
  session opened (`FsVaultProvider.root`, `vault/fs.rs:24-27`; on
  Windows its extended-length form, `:54-57`), with
  `Config::default().with_follow_symlinks(false)` because the scanner
  never follows links (`session.rs:10164-10172`).
- The provider's event handler turns each event path into a
  vault-relative path by stripping the exact watched path (notify keeps
  that spelling) and joining components with `/`, then drops it when
  it is outside the root, is the root itself (a root change is handled
  by §4.4), or has any component starting with `.`. That is the
  scanner's rule (`session.rs:10105`) and covers `.slate/` (cache,
  WAL, `tmp/`, `oplog/`, prefs), `.obsidian/`, `.git/`, `.DS_Store`
  and editor swap files. A path that is not valid UTF-8 counts as an
  unnamed change: it cannot be echo-tested, so its burst calls back.
- Kinds are informational. FSEvents flags are cumulative per path
  (§3.2 rows 3 and 8) and Windows does not distinguish data from
  metadata writes, so the coalescer keeps a set of touched paths and
  never branches on a kind.
- Debouncing lives in core, the same on both platforms: a burst closes
  1 s after its last event (quiet window) or 10 s after its first (the
  ceiling #638 added to the sync-marker watchers); its callback waits
  until at least 5 s after this session's last scan ended (the
  foreground cooldown's figure), stretched to twice that scan's
  duration, so watcher scans hold the writer lock at most a third of
  the time on a slow vault.
- The overflow signal is `Event::need_rescan()` (notify-types
  `Flag::Rescan`): FSEvents `MustScanSubDirs` with `UserDropped` or
  `KernelDropped` (reproduced in §3.2), ReadDirectoryChangesW
  `ERROR_NOTIFY_ENUM_DIR` or a zero-byte success (rc.5). A backend
  `Err`, for example a failed re-arm, is treated the same way once, and
  so is a burst that exceeds the coalescer's own bound of 4,096 touched
  paths.

**Rejected.**

- notify 8.2.0: §3.1. Its Windows overflow is invisible, which breaks
  the fallback contract at its root: nothing would know to rescan.
- `notify-debouncer-full` (0.7.0, 0.8.0-rc.2): walks the whole watched
  tree at watch start to build a file-ID map (`file_id_map.rs`
  `add_path`), `.slate/` included; stitches renames, which AR-8 forbids
  relying on; brings its own thread and timing; has no spacing and no
  echo filter.
- `notify-debouncer-mini`: a per-path timeout, no ceiling, no overflow
  escalation, no echo filter.
- `macos_kqueue`: one descriptor per watched file or directory; a
  50k-note vault would exhaust `RLIMIT_NOFILE`. FSEvents is one stream.
- `PollWatcher`: a timed O(vault) stat walk, which is what the
  foreground rescan already does, at worse latency.
- Host-owned watchers (`FSEventStream` in Swift, `FileSystemWatcher` in
  C#): two implementations of filtering, coalescing and overflow, and
  the spec puts the watcher in core so both hosts gain it.
- FSEvents latency (`with_fsevent_latency`) as the debounce: macOS only,
  with no ceiling and no spacing.

### 4.2 Reconciliation granularity and burst coalescing

**Chosen: a burst requests the existing full incremental rescan.** The
host runs the same R-9 run as Refresh with a new reason,
`RescanReason::Watcher`, and the foreground speech policy (§4.5). Core
does not reconcile per path in PR 20.

How one sync burst becomes one run and at most one sentence:

1. Core closes the burst on the quiet window or the ceiling and calls
   back once (§4.1, §4.3). Bursts that close while a callback is
   waiting out the spacing merge into it.
2. A callback that arrives while a rescan runs joins the host's
   pending-reason lattice and is served by one follow-up run. The join
   is Explicit > Foreground > Watcher: Explicit dominates, Foreground
   with Watcher is Foreground, Watcher with Watcher stays Watcher.
3. A Watcher request is never dropped. While the initial open scan, an
   import or a Trash operation runs, before the workspace exists, or
   while the app is inactive, it is recorded and served when that ends.
   The foreground cooldown does not apply to it; core already spaced it.
4. A `git pull` or an Obsidian Sync batch is one burst, one run, at most
   one sentence. A long sync gets at most one run every 5 s (further
   apart on a slow vault) and at most one sentence a minute (§4.5).

**Slate's own writes are filtered by an echo ledger.** §3.2 shows that a
save reaches the watcher; without a filter every autosave would cost a
full rescan and a host re-sync. `FsVaultProvider` records each mutation
it completes (`write_file`, `write_file_if_absent`, `delete`,
`create_dir`, `create_dir_if_absent`, `rename_if_identity`, `rename`;
`vault/fs.rs:687-771`) as an expected post-state: present with the
size, mtime, ctime and kind it stats right after the write, or absent.
The ledger keeps at most 4,096 entries for 60 s. At burst close the
coalescer asks `provider.is_own_echo(path)` for each touched path and
drops those whose current state matches the newest entry; a burst left
empty calls back nothing.

- Recording at the provider makes the ledger complete by construction:
  every Slate write into the vault goes through those seven methods,
  imports and attachments included (both hosts import through
  `create_exclusive_bytes`). A miss (a spelling the ledger did not
  record, a host-side write) costs one silent rescan, never a wrong
  result.
- The test runs at burst close, not on arrival, so a ledger entry
  written microseconds after the kernel's event is always present.
- The test is "this session wrote it", not "the index already has it".
  Another session's change (`slate-cli`, another window) is already in
  the shared index but not on this host's surfaces, and only the
  re-sync shows it (AR-30: shown, not counted). An index-equality
  filter would hide it.

**Prerequisite, PR 20.0: bound the unresolved-link pass.** With §3.4's
numbers a watcher on a 10k-note vault with 20,000 unwritten-note links
would hold the writer lock 4.6 s per burst. PR 20.0, a small core PR
before PR 20a, gives `InMemoryVaultIndex` a lookup by lowercased final
path component (and by full path), built once per snapshot, and makes
`resolve_link` offer only the candidates `candidate_probe` names
(`link_resolver.rs:384-399`, `:443-455`) instead of every indexed path.
That probe was added for streamed resolution (#1279). Resolution
depends only on the indexed paths and the link's own source
(`link_resolver.rs:50-57`, `:253-297`), and the selector keeps the
first of equal-ranked candidates (`:402-430`), so the winner is
unchanged as long as the lookup keeps each key's candidates in index
order; #1279's equivalence fact pins that. The unresolved-link pass,
every changed file's links and every save's links then cost per link
rather than per link × file. This speeds up today's Refresh,
foreground rescans, saves and the link-heavy cold scan as well as
watcher runs. Skipping the pass when a scan added no path was
considered and left out: with the lookup it saves little, and a pass
that failed half-way would need a durable marker to be owed again.

**Rejected.**

- A bounded rescan of the touched paths (a scoped walk with scoped
  prunes). R-9's single completeness predicate (`session.rs:10194`),
  AR-26 (a Windows NotFound is not evidence of absence) and the
  canvas-after-prune order were hardened for one vault walk over thirty
  review rounds (contract 40 AR-18). A scoped walk would need its own
  evidence of absence: FSEvents reports a directory rename only at the
  directory (§3.2), and `MustScanSubDirs` names a directory, not files.
  The host re-sync stays the full R-9 re-sync either way until PR 21's
  feed gives per-path effects. And the walk is cheap once PR 20.0 lands
  (66 ms at 10k, 385 ms at 50k; one changed file costs the same).
  Revisit only if a measured watcher run, after PR 20.0, says
  otherwise.
- Forcing touched paths through the slow path to close AR-7. Event
  kinds cannot tell a content write from a metadata change (§3.2,
  §3.1), so the only safe trigger would be any event, and an iCloud or
  OneDrive eviction or pin is such an event: forcing the read would
  download every evicted file it names. On macOS the scan's ctime check
  already catches the mtime-preserving writers (`BENCHMARKS.md:150`).
  AR-7 stays as accepted (Q3).
- Core running the rescan itself from the coalescer. It would bypass
  the host's blockers (`Rescan.cs:128-142`), its one token per run and
  close drain (`:200-251`, `:804-853`), and race the host's own Refresh
  into two runs and two sentences.
- Per-path events for external changes through `on_file_change`. Locked
  05 `:338-342` and R-9 keep that channel to Slate-owned writes, and
  contract 40 round 3, finding 3 already rejected streaming external
  changes through it.

### 4.3 Threading

**Chosen: three threads, one direction.**

1. **notify's backend thread** (the CFRunLoop thread notify names
   "notify-rs fsevents loop" on macOS; notify's ReadDirectoryChangesW
   thread running completion routines on Windows) runs the provider's
   event handler: translate, filter, `sink.on_event`. The sink only
   takes the coalescer's burst mutex to insert into the bounded set and
   signal a condvar. It never touches SQLite, never waits on the
   session's connection mutex (a scan holds it for its whole run,
   `session.rs:3192`), never calls the host. §3.2's last row is the
   reason: blocking this thread is what makes FSEvents drop events.
2. **The session's coalescer thread** ("slate-watch"), owned like the
   compaction worker (`session.rs:1554`, joined in `Drop`
   `:1816-1833`), holds `Arc`s to the provider, the listener registry
   and its own state, never to the session. It sleeps on the condvar
   with a deadline (no polling), closes bursts, waits out the spacing,
   runs the echo test through `provider.stat` (no session lock), checks
   the root anchor (§4.4), then takes the listener snapshot (the #802
   seat, `:4572`) and calls `on_external_change()` once per listener
   with no core lock held. It learns this session's scan start, end and
   outcome from `scan_session`, and it drops a closed, uncalled burst
   whose last event came before the start of a scan that then
   committed, because that scan's walk saw the change. A Refresh
   therefore leaves no redundant watcher run behind it.
3. **The host's rescan worker** runs the Watcher-reason rescan with its
   own token, exactly as for Refresh.

The callback's host contract is the other listener methods': cheap,
non-blocking, marshal to the UI thread, never call the session
synchronously (`lib.rs:5364-5371`). Principle 5 holds the way the
compaction worker's `on_error` holds it: the only Rust-to-host calls go
through host-registered listeners, and notify's thread never reaches
the host.

Lifecycle: `VaultSession::start_watching() -> bool` arms through
`provider.watch(sink)`, is idempotent, and returns false when the
provider returns `Ok(None)` or an error or the root check fails at arm
time. `stop_watching()` disarms. `Drop` stops the watch first (dropping
notify's watcher stops its thread), then signals and joins the
coalescer. Hosts call `start_watching()` on their open worker right
before the initial scan, the arm-then-probe rule of #638
(`SyncMarkerWatcher.swift:43-51`): a change during the open scan is
either seen by its walk or arrives as a burst. They call
`stop_watching()` first when closing.

**Rejected.** Calling the host from notify's thread (no coalescing,
host code on a CFRunLoop thread, one call per event); host polling on a
timer (latency, a timer on both hosts; principle 5 prefers callbacks);
an async runtime (no `async` across the FFI, principle 2); reusing the
bibliography debouncer (trailing only, no ceiling, pulses without
paths).

### 4.4 The root, and PR 22

**Evidence.** FSEvents follows the pathname: writes into a directory
swapped in at the watched path were delivered, writes into the
original, moved directory were not, and both the rename and the swap
reported "root changed" (§3.2). ReadDirectoryChangesW follows the
directory handle: a renamed watched directory keeps being watched under
the old path's prefix, and a deleted one ends the watch with
`Remove(Folder)` (§3.1, §3.3).

**Chosen.** The watcher's events are hints that only ask the host to
rescan, and the rescan reads the vault through the session's provider
(by pathname today, by the held descriptor after PR 22). The watcher
therefore cannot carry another directory's content into the index; the
worst a swapped path can do through it is trigger a rescan, which a
Refresh does today. It still refuses to trigger rescans for a root that
is no longer the session's:

1. **Arm, then verify.** Right after arming, the provider observes the
   root path's identity and the session compares it with its anchor,
   `root_identity` (`session.rs:2980`). Windows has no core anchor
   today (`:1053-1060`), so there the watch handle keeps its own
   volume serial and file index, read with `GetFileInformationByHandle`
   (windows-sys, already a dependency). A mismatch disarms the watch,
   and `start_watching()` returns false.
2. **Verify per burst.** Before calling back, the coalescer asks the
   handle whether the root path still names the anchored directory. A
   mismatch, a failed observation or a root-changed event ends the
   watch without a callback; the baseline carries on.
3. **No canonicalization at event time.** Relative paths come from
   stripping the exact watched path (§4.1).

**The seam for PR 22.** `WatchHandle` owns a small `RootAnchor` (today
the root path and its identity at arm time). PR 22 replaces it with the
session's held descriptor, so verification becomes `fstat` of the
descriptor against `stat` of the path. PR 22 also decides whether to
re-arm a stream at the descriptor's current path after a legitimate
rename (`fcntl(F_GETPATH)` on macOS, `GetFinalPathNameByHandleW` on
Windows) or keep "end the watch". PR 22 must keep this section's facts
green.

**Residual (Windows).** notify opens its own handle by path. A root
swapped A→B→A between the arm and the verification leaves a handle on B
that passes verification, because the path names A again. Its events
only trigger rescans of A through the provider, and the watcher is
blind to A until the next open; the baseline covers A. notify's
path-based API cannot close this, before or after PR 22.

**Rejected.** Watching the descriptor (FSEvents has no descriptor API;
kqueue on the root descriptor watches one directory, not the tree);
re-resolving the root per event (a cost per event, still path-based);
trusting "root changed" alone (a swap and restore inside one window may
not show it, and Windows does not report root renames).

### 4.5 Announcements

**Chosen.**

- A Watcher run speaks exactly like a foreground rescan:
  `VaultRescanIncomplete(errors)` when the scan was incomplete or a
  re-sync step failed, otherwise `VaultRescanFinished(reason: Watcher,
  changed, removed)` only when changed + removed > 0, otherwise
  nothing. Core renders the same two sentences ("Files refreshed. …",
  `a11y.rs:1999-2011`); `RescanReason::Watcher` (`:595-600`) keeps the
  run's reason honest without changing the text.
- It never floods a screen reader:
  1. Nothing speaks during a burst; a burst is one callback and one run.
  2. At most one watcher sentence per 60 s per vault. A Watcher run
     whose sentence would fall inside that window posts nothing, and its
     counts are not carried. The window is a core constant read through
     the FFI (`watcher_sentence_spacing_ms()`), so both hosts use one
     number.
  3. No watcher sentence while a modal surface is open (the foreground
     route's predicate).
  4. No Watcher run while the app is inactive: the request waits for
     the next activation and joins the foreground route's request (one
     run, Foreground reason). If watcher runs indexed changes in the
     background, the foreground rescan on return would find nothing and
     stay silent, and the user would lose today's "what changed while you
     were away" sentence.
- Nothing else speaks: progress never does for a non-explicit run
  (`Rescan.cs:278-288`), the re-sync is silent, and the keys never move
  (R-9).
- AR-30 holds: a burst caused by `slate-cli` or another window is shown
  by the re-sync but not counted, so it is silent.

**Rejected.** Speaking every watcher run on the foreground policy with
no window (a script appending to a note every second, or a long sync,
would speak every 5 to 10 s); a trailing summary when a sync settles
(needs a timer and an accumulator on both hosts, generation-guarded and
drained on close, and a writer that never stops never settles; Q2); a
watcher-specific sentence (a corpus wording change, which the golden
at `a11y.rs:6678` makes a product decision; Q4); silence for every
watcher run (#1267 asks for the foreground policy).

### 4.6 The fallback contract

**Chosen.**

- **The baseline never turns off.** Refresh and the foreground rescan
  run exactly as today on both hosts, whether or not the watcher is
  live; the watcher only adds Watcher-reason runs. FSEvents on network
  volumes reports only this Mac's changes, ReadDirectoryChangesW needs
  server support on SMB, and events can be lost.
- `start_watching()` returns false when the provider returns `Ok(None)`
  (host providers, a future mobile provider), an error (an inotify
  watch limit, an unsupported redirector) or the arm-time root check
  fails. The host logs it as a diagnostic.
- An overflow makes one catch-up callback and one full rescan, the same
  run as a burst. The echo filter is skipped for that burst, since its
  path set is incomplete.
- A watch that ends (root changed or swapped, a backend error after its
  catch-up) is not re-armed in PR 20. The baseline continues until the
  next open.
- **The user is told nothing extra**: no sentence, status text or
  setting about the watcher. Diagnostics go to the host log; core's
  `log::warn!` lines carry no paths (the #507 privacy rule).

**Rejected.** Turning the foreground rescan off while the watcher is
live (it is the backstop for lost events, network volumes and the
inactive period); re-arming with backoff (the failure classes that end
a watch are not transient, so a re-arm loop is a state machine for no
case we can name); telling the user ("live updates unavailable": there
is nothing the user can do, and the spec says nothing extra); declining
to watch network volumes (local changes there are still reported).

### 4.7 Tests

Where each backend runs: the rust lane is Linux (inotify,
`.github/workflows/rust.yml:152-276`); the Windows lane runs `cargo test
-p slate-core` on Windows (ReadDirectoryChangesW); the mac lane runs
only `swift test`, so FSEvents is exercised by the mac host's
integration fact through the FFI (Q7).

**Core, real backend** (`vault/watch.rs`, `session/tests/watch.rs`):

- `an_external_create_modify_and_delete_reach_the_sink`: the paths
  arrive, kind-agnostic, within 10 s.
- `the_slate_cache_and_hidden_paths_never_reach_the_sink` (`.slate/`,
  `.obsidian/`, `.hidden.md`).
- `watch_returns_a_live_handle_on_a_local_vault`, which replaces
  `watch_returns_none_in_milestone_a` (`vault/fs.rs:1860`) with the
  owner's recorded call (the TGC-17 rule: rewrite a frozen fact first).
- `dropping_the_handle_stops_delivery`.
- `slate_own_writes_are_echoes_and_never_call_back`: save, create,
  rename, delete and Trash through a session with the watcher on.
- `an_external_write_after_a_slate_write_still_calls_back`.
- `a_swapped_root_ends_the_watch_without_a_callback` and
  `a_root_change_event_ends_the_watch` (Unix: rename the root away, put
  a directory at its path, write into it).
- `an_external_change_emits_no_file_change_event` (locked 05
  `:338-342`).
- `the_callback_runs_on_the_coalescer_thread_with_no_session_lock_held`.
- `dropping_the_session_joins_the_coalescer_and_stops_the_watch`, within
  a bound, mid-burst.

**Core, fake clock and fake event source** (deterministic):

- `a_burst_coalesces_into_one_callback`.
- `a_continuous_stream_closes_at_the_ceiling`.
- `callbacks_wait_for_the_spacing_and_the_duty_factor`.
- `a_burst_during_a_scan_is_delivered_after_the_spacing`.
- `a_burst_seen_by_a_later_scan_is_dropped`.
- `an_overflow_calls_back_once_and_skips_the_echo_filter`.
- `a_backend_error_calls_back_once_then_ends_the_watch`.
- `the_touched_path_bound_overflows_into_one_callback`.
- `start_watching_is_idempotent_and_false_for_an_unavailable_provider`.
- `the_test_switch_keeps_the_watcher_off` (below).

**Core copy.** The corpus gains Watcher rows; the golden and both
hosts' corpus censuses update.

**Windows** (`RescanTests`, `ForegroundRescanRouteTests`, FlaUI):

- `AWatcherDrivenChangeReachesTheTreeWithoutARefresh`: the real
  listener path into a Watcher run; the file is in the tree and Quick
  Open and no Refresh ran.
- `AWatcherRunIsSilentWhenNothingChanged`,
  `AWatcherRunSpeaksWhenSomethingChanged`,
  `AnIncompleteWatcherRunSpeaksTheIncompleteSentence`.
- `ASecondWatcherSentenceInsideTheWindowIsSilent`,
  `AWatcherSentenceIsNotPostedWhileAModalSurfaceIsOpen`.
- `WatcherJoinsTheLattice` (all three joins).
- `AWatcherRequestWhileBlockedRunsWhenTheBlockersSettle` (initial open
  scan, import, Trash), `AWatcherRequestBeforeTheWorkspaceRunsAfterTheOpen`.
- `AWatcherRequestWhileInactiveRunsOnActivation`,
  `AWatcherRequestIsNotSubjectToTheForegroundCooldown`.
- `AnOverflowSignalRunsOneRescan`,
  `CloseStopsTheWatchAndDropsALateSignal`.
- `TheBaselineIsUnchangedWhileTheWatcherIsLive`,
  `AnUnavailableWatcherLeavesTheBaselineUnchanged`.
- FlaUI `ExternalFiles_AppearWithoutRefresh`, the real watcher end to
  end.

**Mac** (`VaultRescanTests`, PR 19's class): the same facts under
Swift names (`testAWatcherDrivenChangeReachesTheTreeWithoutARefresh`
and so on), where the first runs the real watcher, so it is the FSEvents
witness, plus `testSlateOwnSaveDoesNotStartAWatcherRun` on real FSEvents.

**The test switch.** Once the watcher is live, every existing fact that
changes the vault from outside and then asserts a Refresh's counts
becomes timing-dependent: the watcher may index the file first, and the
Refresh then says "No changes." (`ExternalFiles_AppearAfterRefresh`,
PR 19's `testAnExplicitRefreshIndexesAFileDroppedIntoTheVaultAndSpeaksTheCount`).
Core's `start_watching()` therefore honours
`SLATE_TEST_DISABLE_VAULT_WATCHER` (the `SLATE_TEST_FAULT_*`
precedent, `session.rs:7952`). Both hosts' unit harnesses and the
existing FlaUI Refresh journey set it; the watcher facts unset it.

## 5. Core and FFI sketch

```rust
// vault/provider.rs (additive)
pub trait VaultProvider: Send + Sync {
    // ... existing methods ...
    /// Whether `relative`'s current state is the one this provider's own
    /// latest mutation of it left behind (the watcher's echo test).
    fn is_own_echo(&self, _relative: &str) -> bool { false }
}

pub enum FileEvent {
    Created { relative: String },     // kinds are informational
    Modified { relative: String },
    Deleted { relative: String },
    Renamed { from: String, to: String },
    /// The platform dropped events under `relative` (None: anywhere).
    Rescan { relative: Option<String> },
    /// The watch ended: `RootChanged` refuses a catch-up, `Failed`
    /// makes one.
    Ended { cause: WatchEnd },
}

pub struct WatchHandle { /* notify watcher, RootAnchor */ }

// session.rs
impl VaultSession {
    pub fn start_watching(&self) -> bool;
    pub fn stop_watching(&self);
}
pub trait VaultEventListener: Send + Sync {
    // ... on_error, on_file_change, on_index_phase ...
    /// PR 20 (#1267): files changed outside this session; one call per
    /// coalesced burst from the watch coalescer, no session lock held.
    /// Carries no paths: request a Watcher rescan. Default no-op.
    fn on_external_change(&self) {}
}

// a11y.rs
pub enum RescanReason { Explicit, Foreground, Watcher }
```

FFI (`slate-uniffi`): `start_watching()`, `stop_watching()`,
`on_external_change()` on the foreign `VaultEventListener`, and
`watcher_sentence_spacing_ms()`. A foreign trait must implement every
method, so PR 20a adds a no-op `onExternalChange` / `OnExternalChange`
to the three implementations: `VaultEventAdapter`
(`AppState+History.swift:82`), `UiVaultEventListener`
(`VaultLifecycleViewModel.cs:2014`) and the test recorder
(`SlateWindows.Tests/Support/Recorders.cs`). Both bindings are
regenerated (program rule 7).

Constants, each pinned by a fact: quiet window 1 s, ceiling 10 s,
spacing 5 s, duty factor 2, touched-path bound 4,096, echo ledger 4,096
entries and 60 s, watcher sentence window 60 s. The two bounds keep the
watcher's memory fixed whatever a sync client does (locked 05 §4
principle 4).

## 6. Host designs

**Windows (PR 20b).**

- `JoinRescanReasons` takes the three-way join (`Rescan.cs:122-126`).
- `RequestRescan` records a Watcher request instead of refusing it
  while blocked or before the workspace exists (`:87-120`). The
  blocker-settled path (`:905-922`), the end of the open (after
  `InitializeWorkspace`, `VaultLifecycleViewModel.cs:542`) and
  activation (`MainWindow.xaml.cs:146`) serve it.
- `UiVaultEventListener` gains `OnExternalChange` →
  `_enqueueUi(() => HandleExternalChange(generation))`, which requests
  a Watcher rescan when the window is active and records it otherwise.
- The session load calls `StartWatching()` on the worker before
  `ScanInitialWithProgress` (`VaultLifecycleViewModel.cs:524-530`);
  `CloseSession` calls `StopWatching()` before closing the rescan
  admission.
- The sentence (`Rescan.cs:336-347`) adds §4.5's window and modal check
  for the Watcher reason.
- Facts per §4.7; the test switch in the harness; the FlaUI journey,
  which activates the main window first, since an inactive window
  defers the run (§4.5).

**Mac (PR 20c, after PR 19).**

- PR 19's `AppState+Rescan.swift` lattice takes the three-way join and
  the never-dropped rule.
- `VaultEventAdapter.onExternalChange()` hops to the main actor under
  the `currentSession === session` guard and requests a Watcher rescan,
  or records it while the app is inactive. PR 19's foreground route
  serves it on `didBecomeActive`.
- The open funnel calls `startWatching()` inside the detached task
  before `scanInitialWithProgress` (`AppState.swift:11726`); close calls
  `stopWatching()` before `unregisterVaultEventListener()`
  (`AppState.swift:11503`).
- The same sentence gate. A short VoiceOver check of one watcher
  sentence and of a burst is recorded in the PR (program rule 6).

Neither host's sync-marker watcher changes: it watches the hidden
marker paths (`.obsidian`, `.git`) that the vault watcher filters by
design.

## 7. Implementation plan

| PR | Scope | Size | Lanes | After |
|---|---|---|---|---|
| 20.0 | Core: link resolution looks candidates up by final component instead of offering every path, which bounds the unresolved-link pass (§4.2). **Delivered 2026-10-18 (#1429):** a snapshot keys each path by the hash of its lowercased path and of its lowercased final component, built on its first resolution; the pass snapshots `files` only when a link dangles; every resolution snapshot reads `ORDER BY path`. Before and after: `BENCHMARKS.md`, "Plan 43 PR 20.0" | S–M | rust, swift, windows | — |
| 20a | Core watcher, echo ledger, coalescer, root anchor, `on_external_change`, `start_watching` / `stop_watching`, `RescanReason::Watcher` and corpus rows, the test switch; bindings regenerated with no-op host adapters. **Delivered 2026-10-18:** `vault/watch.rs` (backend, filter, `RootAnchor`), `vault/echo_ledger.rs`, `session/watch.rs` (the coalescer and its "slate-watch" thread); the ledger also records the folders above each mutated path, because ReadDirectoryChangesW reports a folder's last-write change and a create or a move can make missing folders; a closed burst is dropped only by a scan that committed with a complete walk; callbacks are also spaced 5 s from the previous callback; facts in `vault/watch.rs`, `vault/echo_ledger.rs`, `vault/fs.rs` and `session/tests/watch.rs` | L (about 1,500 to 2,000 lines with facts) | rust, swift, windows, audit | 20.0 |
| 20b | Windows host: lattice, never-dropped Watcher requests, listener wiring, start and stop, the sentence gate, facts, FlaUI journey. **Delivered 2026-10-18:** `VaultLifecycleViewModel.Rescan.cs` (the three-way join, Watcher requests recorded before the workspace and while blocked, deferred while the window is away, the sentence gate), `VaultLifecycleViewModel.cs` (`OnExternalChange` → `HandleExternalChange`, `StartWatching` before the open scan, `StopWatching` first in `CloseSession`), `ForegroundRescanRoute.cs` (every activation serves a deferred request after its own decision); the activation join, a Watcher follow-up whose window went away deferred too, and a gated sentence that posts nothing on the status line either; #1498 settled on the Windows lane — a session's own cache writes arrive as `.slate`, a write through `SLATE~1` arrives as `SLATE~1` — and core reads an 8.3 alias by its long spelling; facts in `RescanTests.Watcher.cs`, `ForegroundRescanRouteTests.cs`, FlaUI `ExternalFiles_AppearWithoutRefresh`, and core's `vault/watch.rs` and `session/tests/watch.rs` | M (about 600 to 900) | windows | 20a |
| 20c | Mac host: the same on PR 19's rescan, plus a VoiceOver check | M (about 600 to 900) | swift, a11y, windows | 20a, PR 19 |

Facts by PR:

- **20.0.** `candidate_lookup_resolves_like_the_full_offer` (over the
  resolver's existing fixtures, case-variant duplicates included:
  #1279's equivalence); `a_created_file_still_resolves_a_previously_unresolved_link`;
  and an isolated perf guard like the root-listing guard
  (`rust.yml:268-276`): a 10k-note, 20,000-unresolved-link no-change
  rescan within twice the link-free one. `BENCHMARKS.md` records
  before and after. Delivered with these names in `link_resolver.rs`
  and `session/tests/link_pass.rs`, the guard as
  `perf_guard_unresolved_link_rescan_within_twice_the_link_free_one`
  in its own rust-lane step, plus
  `a_failed_snapshot_read_leaves_the_rescan_incomplete_and_resolves_nothing`
  and `every_resolution_snapshot_is_read_through_the_path_order_helper`.
- **20a.** §4.7's core facts.
- **20b and 20c.** §4.7's host facts.

Interlocks:

- **PR 19.** PR 20c builds on PR 19's lattice, foreground route and
  re-sync and cannot start before it merges. PR 20.0, 20a and 20b do not
  wait for PR 19. PR 19's facts that drop a file and press Refresh
  keep the watcher off through the test switch.
- **PR 21.** The change feed records the index mutations of any scan,
  so a Watcher run feeds it as Refresh does. PR 21 replaces the full
  re-sync for every reason with feed consumption and makes AR-30's
  counts exact, so a `slate-cli` write would then be counted. The
  watcher callback carries no paths and does not change; PR 21 takes
  the echo ledger as given and may later replace it with the feed's
  writer identity.
- **PR 22.** Replaces `RootAnchor` with the held descriptor (§4.4),
  decides the re-arm question, keeps §4.4's facts, and records the
  Windows arm-race residual. Its implementation follows PR 20a, since
  both touch root identity (program §9).
- In `00_program.md` §5, PR 20's row becomes 20.0, 20a, 20b and 20c as
  above; PRs 21 and 22 stay design-first.

## 8. Documents and registers the implementing PRs change

- Locked 05 §4.4 (`:338-347`), on the owner's call (Q5). Replace "The
  filesystem watcher is a stub, so external edits surface at the next
  scan, never as events" with: "External edits never surface as
  file-change events: the filesystem watcher only asks the host to
  reconcile (`on_external_change`), and the reconciliation is the
  rescan." §4.1's fallback promise (`:185-188`) stands.
- The doc comments at `lib.rs:5372-5373` and `session.rs:866-868`
  (20a).
- Contract 40: OD-1's follow-up delivered; AR-3 closed while a watch is
  live, with §9's residuals as new rows; AR-7 noted unchanged; R-9 gains
  the Watcher reason and the three-way join (20b and 20c).
- `w_c_matrix.md` and `parity_matrix.md`: the vault scan status, Files
  tree and Quick Open rows (20b, 20c). `docs/runbooks/
  voiceover-feature-test.md` §A gains a watcher step (20c). If PR 19
  adds a help entry for Refresh, one sentence says the list also
  updates on its own.

## 9. Residual risks (proposed contract 40 rows)

1. AR-3's remainder: with no watch (an unsupported provider, an inotify
   or redirector failure, a root renamed or swapped) and for remote
   changes on network volumes, changes wait for Refresh or the
   foreground rescan.
2. AR-7 unchanged: the watcher triggers the same incremental scan with
   its (mtime, size, ctime) short-circuit.
3. The watcher sentence window: later runs inside a 60 s window are
   silent. The tree and Quick Open are current, and Refresh speaks its
   own scan's counts.
4. The echo test compares (size, mtime in ms, ctime in ms, kind), and
   Windows has no portable ctime. An external write to a file in the
   same millisecond as Slate's own, at the same size, counts as an
   echo; the baseline's fast path would skip it too (AR-7's class).
5. The Windows arm race (§4.4).
6. A pre-release dependency until notify 9.0.0 ships.
7. FSEvents is exercised in CI only by the mac host's integration fact.
8. Metadata-only churn (an antivirus or indexer touching attributes,
   a cloud client evicting or pinning files; notify's Windows filter
   includes attributes and security, `windows.rs:457-463`) costs silent
   rescans, at most one every 5 s. A cost, not a correctness risk.

## 10. Open questions for the owner

- **Q1.** The unresolved-link cost (§3.4) is a defect in today's
  Refresh and foreground rescans, independent of the watcher. File it as
  its own issue and make PR 20.0 close it (recommended), or carry it
  under #1267? Nothing was filed in this pass.
- **Q2.** One leading watcher sentence per 60 s (recommended), or a
  trailing summary when a sync settles, at the price of a timer and an
  accumulator on both hosts?
- **Q3.** Keep AR-7 accepted (recommended), or ask for re-reads of
  watched paths behind a cloud-placeholder guard (a new `FileStat`
  field on both platforms)?
- **Q4.** Keep the shared "Files refreshed. …" sentence for watcher runs
  (recommended), or add a watcher-specific one?
- **Q5.** Approve §8's amendment of locked 05 §4.4.
- **Q6.** Pin `=9.0.0-rc.5` now (recommended), or wait for 9.0.0? 8.2.0
  is not an option (§3.1).
- **Q7.** Rely on the mac host's integration fact for FSEvents
  (recommended), or add a filtered `cargo test` of the watcher module
  to the mac lane at the cost of a test build there?

## 11. Owner decisions (2026-10-17)

The owner answered §10 in a decision interview on 2026-10-17. Every answer below is the recommendation except where marked.

- **Q1.** The unresolved-link cost is filed as #1429, and PR 20.0 closes it.
- **Q2.** One leading watcher sentence per 60 s; no trailing summary.
- **Q3.** AR-7 stays accepted; no forced re-reads of watched paths.
- **Q4.** Watcher runs speak the shared "Files refreshed. …" sentence; no watcher-specific copy.
- **Q5.** §8's amendment of locked 05 §4.4 is approved, together with PR 21's (#1444, Q6).
- **Q6.** `notify` is pinned at `=9.0.0-rc.5` now, moving to 9.0.0 when it ships.
- **Q7.** FSEvents is exercised in CI by the mac host's integration fact only; no core watcher test on the mac lane.
- **Plan.** §7's split is accepted and queued in order: 20.0, then 20a, then 20b and 20c (20c after PR 19, merged as #1447).
- **From PR 21's Q2 (not the recommendation there).** Other sessions' changes are spoken only on an explicit Refresh. So a burst caused by `slate-cli` stays silent on watcher runs, as §4.5 says.
