# WB-29a — One hot-exit design for both hosts, and the core store it fixes

Issues: Refs #1525 (both PRs of this chunk write `Refs`; #1525 closes in
whichever of WB-29 and plan 43 PR 29's mac half merges last, §5). Size L
(design first). Lanes: this design PR is docs only; the core-store PR
that follows is rust, windows and swift (compile: the FFI gains an
object, records and two `A11yEvent` variants, and both corpus mirrors
move). AT: none for the design or the store (nothing a reader hears
changes until a host adopts it; WB-29 carries the NVDA pass). Wave: W10
(this design), W11 (the store, after WB-28 — merged 2026-11-01 as
#1729 and #1731). Decision: WD20, answered 2026-10-26 (plan §3.1): one
design for both hosts; core owns the store; a vault whose root identity
is weak (FAT32, exFAT) gets no hot exit; WB-29 and plan 43 PR 29 start
only after this chunk's store is on `main` with green CI.

This document is the design WD20 asks for. It is written to WB-29's list
"What the shared design must have fixed before this starts"
(`specs/wb29_hot_exit_windows_half.md`, items 1–8) and answers each item
in the section with the same number. Where WB-29's spec and this design
differ, this design wins, and WB-29 rebases its Design section on it
before code (its line citations are stale since WB-9..WB-28; §Current
state below has today's).

## Goal

Closing the window or quitting Slate with unsaved notes never asks and
never writes the notes into the vault. Every unsaved buffer is kept, as
you type, in a store outside the vault, so a crash or a kill keeps it
too. When the vault opens again, each buffer comes back in its tab,
still unsaved; a note that changed on disk meanwhile comes back unsaved
and stale, and the existing save-time conflict decides at the next save.
Close Vault still asks, and its Discard All discards. A vault whose root
identity is weak gets no hot exit: closing falls back to today's prompt,
with core's sentence saying why.

## Current state on main (ed172e07)

- **No hot exit exists anywhere.** No recovery, autosave or hot-exit
  code is in `crates/`; the Mac's "recovery" words are in-memory draft
  registries (`AppState.swift:22210`, `:23183-23188`). Core has no store
  under a device-local root; every durable writer it has works relative
  to the vault's held root (`vault/fs.rs:1472-1486` stage + `sync_data`,
  `:1000-1313` publish per platform, `:1110-1132` best-effort
  `sync_parent`).
- **The hosts' device-local roots.** Windows: `SlateDeviceState.Root`
  (`SlateDeviceState.cs:18-34`, `%LOCALAPPDATA%\Slate` or
  `SLATE_LOCAL_APP_DATA`), used by seven stores. Mac: Application
  Support (`RecentVaultsStore.swift:145` and two more).
- **The vault key the hosts already use.** WB-28 PR 2's
  `FileRecentsStore` keys a persistent identity by the lossless text
  `{volume:x16}-{id_high:x16}{id_low:x16}` (`FileRecentsStore.cs:85`) and
  keeps a weak one on the path key; core's `VaultRootIdentity { volume,
  id_high, id_low, persistent }` (`slate-uniffi/src/lib.rs:629-635`,
  `vault/fs_root.rs:61-67`) documents that a weak identity is never a
  lasting key and that no access decision compares identities.
- **Dirty buffers are per tab, mirrored per path.** Windows: each
  `WorkspaceTabViewModel` (`WorkspaceViewModel.cs:142`) owns a buffer
  session (`:155`, `:1899`); `IsDirty` (`:532-549`) bumps
  `ContentGeneration` (`:281-291`); same-path Markdown tabs are kept in
  step edit by edit (`MirrorSamePathDocumentState :3880-3904`,
  `ApplyPeerDocumentEvent :826-873`), and a discard disposes only its
  own tab (`Layout.cs:492-493`). Mac: one buffer per path in effect —
  `mirrorEdit` "the duplicate IS the same buffer"
  (`WorkspaceState.swift:356-363`).
- **Tabs have stable ids in the saved layout.** Windows persists each
  tab's `Guid` (`WorkspaceTabState(tabId, …)`, `workspace.json` under
  `.slate`, `WorkspacePersistence.cs:87`, `:110`, `:180`); Mac's
  `WorkspaceStore(rootIdentity:)` restores tabs by id
  (`AppState.swift:4314-4338`).
- **The saves.** `save_text(path, contents, expected_content_hash)` is a
  compare-and-swap across processes (`session.rs:4195-4221`, conflict
  `:4577`); the hash is blake3 hex (`vault/fs.rs:1772-1773`,
  `editor_text_content_hash`, uniffi `:7482`). Windows serialises every
  file's saves through `WorkspaceSaveCoordinator` (`Enqueue :94-131`,
  `Admit :133-170`, per-path key `:68-75`); a teardown joins its workers
  with no bound (`CloseAndJoinWorkers :297-318`).
- **The open sequence.** Windows: session (`VaultLifecycleViewModel.cs:591`),
  null-identity refusal (`:604-616`), `VaultOpened` (`:646`), scan
  (`:657-675`), `InitializeWorkspace` (`:692`, `:1522-1597`) constructing
  the `WorkspaceViewModel` (`:1530`), whose constructor restores the
  layout (`WorkspaceViewModel.cs:2284`, `:2493`; `RestoreNode`,
  `Persistence.cs:53-107`, makes new tab instances), then the first tree
  publication (`:694`) and `WorkspaceReady` (`:1631`), whose handler lands
  focus once (`MainWindow.xaml.cs:486-495`). Mac: `restoreWorkspaceLayout`
  (`AppState.swift:10827`) runs **before** the scan task (`:10834-10838`).
- **Closing.** Windows asks through one prompt (`MainWindow.xaml.cs:365-381`,
  `VaultCloseDecision { SaveAll, Discard, Cancel }`,
  `VaultLifecycleViewModel.cs:16-21`) on Close Vault, application close
  (`PrepareForApplicationClose :811-825`) and vault switch (`:543`); a
  dirty tab's close, pane close and document replacement ask per tab
  (`Layout.cs:460-511`, `:546-668`, `:274-337`). Mac asks on Close Vault,
  vault switch and quit (`applicationShouldTerminate`,
  `SlateMacApp.swift:43-68`; `admitTermination`, `AppState.swift:11927-11949`).
- **Spoken copy.** `A11yEvent` (`a11y.rs:621`), `priority()`
  (`:1873-1920`), `render()` (`:1923`), the corpus (`:4054`), the
  committed corpus fixture (`tests/fixtures/a11y/corpus.json`, pinned at
  `:7888+`), the uniffi mirror (`lib.rs:9167-9171`), and the two host
  corpus mirrors (`A11yCorpusCensusTests.swift`, `A11yCorpusCensus.cs`),
  each pinned in order by a Rust tripwire (`:14799`, `:14910`).

## Design

### 1. The store, its key and its caps

**A process-lifetime core object.** `HotExitStore` is a uniffi
`#[uniffi::Object]` (like `CommandRegistry`, `lib.rs:8788`, one instance
for the app's lifetime), constructed by
`HotExitStore::open(device_root: String) -> Result<Arc<HotExitStore>, HotExitError>`.
The host passes its device-local root (Windows
`SlateDeviceState.Root`, Mac Application Support/Slate); the store keeps
everything under `<device_root>/hot-exit/v1/`. It never touches a vault.

**The vault key is plain data the session hands out.**
`VaultSession::hot_exit_key() -> HotExitKeyResult` returns
`HotExitKeyResult::Key { key: HotExitVaultKey }` for a persistent root
identity, where `HotExitVaultKey { volume, id_high, id_low }` is the
identity itself (no hashing in the record; the store hashes it into a
directory name, below), or `HotExitKeyResult::Unavailable { reason }` for
a weak or absent identity (§7). The host reads it once at open and
passes the record to every store call; no store call takes or borrows a
`VaultSession` (§8).

**On disk.** One directory per vault:
`hot-exit/v1/<hex(SHA-256("{volume:x16}-{id_high:x16}{id_low:x16}"))>/`
(the recents store's lossless text, so two spellings of one folder are
one directory and the high bits count). Inside:

- `epoch` — the vault's discard epoch, a decimal `u64`, written by
  temp + `sync_data` + rename (§6);
- `entries/<entry-id>.json` — one file per buffer (below);
- `tombstones/<entry-id>.json` — a per-buffer discard floor (§6).

**The key within a vault is per tab (the peer rule).** An entry's id is
the host's stable tab id (Windows the tab `Guid`, Mac its tab id), sent
as an opaque string of at most 64 characters from `[0-9A-Za-z_-]`; the
store refuses any other spelling (`HotExitError::InvalidEntryId`). Two
tabs on one note are two entries, so one tab's discard never removes its
peer's newer generation. A restore maps an entry back to the tab with
the same id in the saved layout; an entry whose id no restored tab has
opens as a new tab (§3).

**An entry** is UTF-8 JSON:
`{ "schema": 1, "entry_id", "path" (vault-relative, forward slashes),
"text", "baseline_hash" (the file's content hash when the buffer was
loaded or last saved; null for a note never saved), "generation" (u64,
the host's per-tab counter, strictly rising per edit), "epoch" (u64, the
discard epoch the host held), "written_at" (RFC 3339 UTC) }`.

**Caps.** Per entry: `text` at most 8 MiB of UTF-8. Per vault: at most
256 entries and 64 MiB of entry files. A write over a cap returns
`HotExitWriteOutcome::Refused { refusal: HotExitRefusal::OverCap, detail }`
— a typed refusal the host acts on exactly as on a failed flush (§5) —
and writes nothing; an existing entry for that tab is left as it was
(it is older but still a valid copy, and its generation stays, so a
later smaller write supersedes it normally).

### 2. Write cadence, durability and results

**The host writes on every dirtiness change and debounced while typing.**
A tab that turns dirty writes at once; while it stays dirty, a write
follows 1 s after the last edit (a host constant; WB-29 and PR 29 may
tune it, the store does not care). A tab that turns clean (a save landed,
an undo to the saved baseline, a reload) calls `discard_buffer` (§6).

**Every write is durable-or-error.**
`HotExitStore::write_buffer(key, entry: HotExitEntryWrite) -> Result<HotExitWriteOutcome, HotExitError>`
writes `entries/<id>.json.tmp-<random>` in the vault directory, calls
`sync_data` (`FlushFileBuffers` on Windows), renames it over
`entries/<id>.json` (POSIX `rename`; Windows `MoveFileExW` with
`MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH`), and syncs the
directory where the platform can (best effort, as `sync_parent`). It
returns only after the rename: `Durable { generation }`. Any I/O failure
is `Err(HotExitError::Io { detail })` with the temp file removed; a
caller treats it as "not kept" (§5). Nothing is fire-and-forget: the
host's write worker completes with the outcome.

**Out-of-order completions.** Writes for one vault are serialised inside
the store by a per-vault mutex. A write whose `generation` is not
greater than the stored entry's, or not greater than the entry's
tombstone floor, or whose `epoch` is below the vault's current epoch,
returns `Superseded` and writes nothing (§6). So a slow older write never
replaces a newer one, whichever thread finishes first.

### 3. The restore rule

`HotExitStore::read_buffers(key) -> Result<HotExitSnapshot, HotExitError>`
returns `{ epoch, entries: Vec<HotExitEntry> }`: every entry whose epoch
equals the vault's current epoch and whose generation is above its
tombstone floor (older files, left by a crash mid-discard, are deleted
here). It reads; it never deletes a current entry.

The host classifies each entry against the open vault through one
session read (the store borrows no session; the classification is a
session method because it reads the vault):
`VaultSession::classify_hot_exit_entries(entries) -> Vec<HotExitRestore>`,
one per entry, in order:

| The vault now | Decision | What the host does |
|---|---|---|
| the file at `path` has content hash == `baseline_hash` | `Restore { path }` | the tab opens on `path`, its buffer set to `text`, dirty |
| the file at `path` exists with another hash | `RestoreStale { path }` | the same, and the tab is externally stale; the save-time conflict decides at the next save (`save_text` with the entry's `baseline_hash` as the expected hash returns `WriteConflict`) |
| no file at `path`, and exactly one indexed file has content hash == `baseline_hash` | `Moved { from, to }` | the tab re-seats on `to`, dirty (the rescan's unique same-hash rename rule) |
| no file at `path`, none or several with that hash, or `baseline_hash` null | `Gone { path }` | a new unsaved note at `path`, dirty, holding `text` (its first save creates it, `CreateExclusiveReporting` on Windows) |

`path` is resolved with the vault's existing path validation; an entry
whose path no longer validates (it escaped the vault, it names `.slate`)
is reported as `Invalid` and never restored (the host leaves it; the
next `discard_vault` or the entry's own discard removes it).

**Mapping to tabs.** The host restores its saved layout as today, then
injects each classified entry into the restored tab with the same id;
an entry with no such tab opens as a new tab in the active group, in
`written_at` order. Two entries on one path (peers) restore as two dirty
tabs, each with its own text.

### 4. The open sequence and the copy

**Order (both hosts).** Session open → identity admission (WB-28) →
`hot_exit_key()` → the open scan → `read_buffers` and
`classify_hot_exit_entries` → the layout restore with the classified
buffers injected into the tabs it constructs → the workspace becomes
ready (Windows `WorkspaceReady`, raised once, after the buffers are
installed, so the launch landing happens once) → `UnsavedNotesRestored`
when any buffer was restored. On Windows the read moves before the
`WorkspaceViewModel` construction (`VaultLifecycleViewModel.cs:1530`)
and the restored set is passed in, because `RestoreNode` builds new tab
instances. On the Mac, PR 29 moves `restoreWorkspaceLayout`
(`AppState.swift:10827`) after the scan, or injects into it after the
scan before the editor shows; either way the buffers are installed
before the first editor focus.

**Copy (core).** Two new `A11yEvent` variants, both at
`A11yPriority::Medium`, rendered in core, in the corpus and the committed
fixture, and in both host corpus mirrors in the same PR (§Tests):

- `UnsavedNotesRestored { count }` — "Restored 1 unsaved note." /
  "Restored {count} unsaved notes.";
- `RestoredNoteChangedOnDisk { name }` — "{name} changed on disk while
  Slate was closed. Its unsaved edits are restored; saving will ask
  before overwriting." One per `RestoreStale` entry, after the count.

The store's refusals and errors carry core's sentences too
(`HotExitRefusal::render()`), so a host's status line never composes
them (AGENTS: accessible output is produced in core).

### 5. Interlocks: closing, switching, Close Vault

- **Application close and vault switch never ask once the latest
  generations are durable.** The host flushes every dirty tab's latest
  generation (`write_buffer`), waits for the outcomes at most 5 s, and
  closes without a prompt when every one is `Durable` (or `Superseded`
  by a newer durable generation of the same tab). If any write fails,
  is refused, or does not finish in 5 s, the host falls back to today's
  prompt, its status line saying why (the refusal's sentence, or "Your
  unsaved notes could not be kept for next time."). The layout save on
  close keeps running as today.
- **Close Vault still asks.** Its Save All saves as today, and each tab
  that turns clean discards its entry. Its Discard All discards: the host
  fences (§6) and calls `discard_vault`.
- **Explicit save stands (RD1, #877).** Hot exit never writes a note into
  the vault.

### 6. Every Discard fences pending writes; the store supersedes them

**Two mechanisms, both store-side, both durable before any delete.**

- **The vault's discard epoch.** `discard_vault(key) -> Result<u64, HotExitError>`
  takes the vault mutex, writes `epoch + 1` durably, then deletes every
  entry and tombstone, and returns the new epoch. Every `write_buffer`
  carries the epoch the host holds; under the same mutex a write whose
  epoch is below the current one returns `Superseded` and writes
  nothing. So a write issued before the discard — still in flight, or
  completing after it — can never leave or re-create a discarded buffer.
  The host's epoch comes from `read_buffers` at open and from each
  `discard_vault` it makes; a host whose write is `Superseded` by epoch
  refreshes its epoch with `current_epoch(key)` before the next write.
- **A per-buffer floor.** `discard_buffer(key, entry_id, through_generation) -> Result<(), HotExitError>`
  writes `tombstones/<id>.json` = `{ through_generation, process }`
  durably, then deletes `entries/<id>.json` if its generation is not
  above the floor. A later write for that id with a generation at or
  below the floor returns `Superseded`. `process` is a random 128-bit id
  minted when the store opens; `read_buffers` prunes tombstones written
  by another process (no write of a dead process can still land), and
  `discard_vault` clears them all.

**The routes that fence.** Close Vault's Discard All (`discard_vault`);
the fallback prompt's Discard All on application close and vault switch
(`discard_vault`, issued while an older `write_buffer` may still be in
flight — the epoch drops it); a clean transition (`discard_buffer` with
the generation that turned clean); and a dirty tab's own Discard —
closing it, closing its pane, or replacing its document (`discard_buffer`
with its latest generation). A host awaits its discards at most 5 s,
then proceeds and logs the unconfirmed discard (WB-29's
`HotExitDiscardUnconfirmed`); the floor or epoch still supersedes any
late write once the discard lands.

### 7. A weak root: no hot exit

The owner's call (WD20, 2026-10-26) picks policy (a).
`hot_exit_key()` returns `Unavailable { reason }` for a session whose
root identity is weak (`persistent: false`) or absent; the reason is a
core sentence, `a11y::HOT_EXIT_UNAVAILABLE_WEAK_ROOT`: "This folder's
file system cannot keep a lasting identity for the vault, so Slate
cannot keep your unsaved notes for next time." The host has no key, so
it never writes and never reads for that vault; application close and
vault switch fall back to today's prompt with that sentence in the
status line; a crash keeps nothing, as on `main`.

R1 (no buffer restored into a folder other than the one whose session
wrote it) holds by construction: only a persistent `FileIdInfo` identity
keys a directory, and two folders never share one (D1, WB-28). R2 (a
weak root renamed with Slate closed loses nothing the close promised to
keep) holds because the close promised nothing: it prompted. R3 (the
store's own facts prove R1 under a forced weak-index collision): the
store cannot be reached with a weak identity at all, and a store fact
proves that two distinct persistent keys never read each other's
entries, and a core fact proves `hot_exit_key()` is `Unavailable` under
the WB-28 seams `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` and
`SLATE_TEST_ROOT_FILE_INDEX_COLLIDES`.

### 8. The store's calls borrow no vault session

`write_buffer`, `discard_buffer`, `discard_vault`, `current_epoch` and
`read_buffers` are methods of `HotExitStore`, whose only state is the
device-local root, the per-vault mutex map and its process id. They take
the `HotExitVaultKey` record as an argument. None takes a `VaultSession`,
so no binding call keeps a session alive after `session.Dispose()`, and a
teardown never joins a recovery write. The object is `Send + Sync`;
concurrent calls for one vault serialise on its mutex, calls for two
vaults run in parallel. A call cut short by process exit leaves the
previous durable state: a write that did not reach its rename leaves the
old entry (and a stray temp file, deleted by the next `read_buffers`); a
discard that did not finish its deletes leaves files below the new
epoch or floor, which `read_buffers` ignores and deletes.

The one session method each host calls, `classify_hot_exit_entries`, runs
during the open, on the open sequence, before the workspace exists, and
is not a recovery write.

## The core store PR (after this design merges)

New module `crates/slate-core/src/hot_exit.rs` (the store) and
`hot_exit` FFI in `crates/slate-uniffi/src/lib.rs`: the object, the
records (`HotExitVaultKey`, `HotExitKeyResult`, `HotExitEntryWrite`,
`HotExitEntry`, `HotExitSnapshot`, `HotExitWriteOutcome`,
`HotExitRefusal`, `HotExitRestore`), `HotExitError`, the two session
methods, the two `A11yEvent` variants with their corpus rows, and the
weak-root constant. Windows and Mac compile against the new bindings
(the corpus mirrors move); neither host calls the store until WB-29 and
PR 29.

## Tests (the core store PR)

Core facts, `crates/slate-core/src/hot_exit/tests.rs` unless noted:

| Fact | Proves | Mutation it kills |
|---|---|---|
| `a_durable_write_is_read_back_after_the_store_reopens` | write → new store on the same root → `read_buffers` returns the entry | skip the rename |
| `two_tabs_on_one_note_are_two_entries_and_one_discard_keeps_the_other` | the peer rule: discard tab A, tab B's text restores | key by path |
| `an_older_generation_never_replaces_a_newer_one` | `Superseded` for a lower or equal generation, the newer text stays | compare `>=` / drop the check |
| `a_write_issued_before_a_vault_discard_is_superseded_even_if_it_lands_later` | the epoch: a write with the old epoch after `discard_vault` writes nothing | skip the epoch compare; bump the epoch after the deletes |
| `a_buffer_discard_supersedes_its_earlier_generations` | the floor: a late write at or below the floor writes nothing; a newer one writes | skip the floor |
| `tombstones_of_a_dead_process_are_pruned_at_read` | a floor written by another process id is gone after `read_buffers` | prune none / prune all |
| `a_write_over_a_cap_is_refused_and_leaves_the_older_entry` | 8 MiB, 256 entries and 64 MiB refusals; the older entry stays | write before the check |
| `a_cut_short_write_keeps_the_previous_entry` | a write killed after its temp file (a test seam before the rename) leaves the previous entry; the next read deletes the temp | write in place |
| `two_vault_keys_never_read_each_others_entries` | R1 for persistent keys, including two keys differing only in `id_high` | drop the high bits |
| `an_invalid_entry_id_is_refused` | ids outside `[0-9A-Za-z_-]{1,64}` refused, nothing written | accept any id |
| `concurrent_writes_and_a_discard_on_one_vault_serialise` | threads racing writes and `discard_vault` end with no entry from before the discard | no per-vault mutex |
| session: `a_weak_root_has_no_hot_exit_key` (Windows, WB-28 seams) | `Unavailable` with the constant sentence | key a weak identity |
| session: `classify_hot_exit_entries_decides_restore_stale_moved_gone_and_invalid` | each row of §3's table | treat a changed file as `Restore`; re-seat on an ambiguous hash |
| a11y: the two variants render, sit at `Medium`, and appear in the corpus and fixture | the copy | — |

uniffi: `hot_exit_store_round_trips_through_the_binding` (open, write,
read, discard through the exported object). Hosts: both corpus mirrors
gain the two events (the Rust tripwires fail until they do); no host
behaviour changes.

## Docs and registers

- This spec; plan 44 §3 row 29 and the WB-29a row point at it.
- WB-29's spec rebases on this design before its code (its own PR):
  its items 1–8 now cite this document, its Open design items 1 and 2
  close here (item 1: policy (a); item 2: in-flight and discard
  bookkeeping are per `HotExitVaultKey`, equal for two sessions on one
  vault, different between vaults), and its line citations update.
- Plan 43's PR 29 row (`43_mac_backlog_delivery/00_program.md:442`) and
  RD1 (`:565`) point at this design for the mac half; `45_windows_followups_plan.md:297`
  ("plan 43 PR 29's core store") reads "WB-29a's core store".

## Verification (the core store PR)

`make ci`; `cargo nextest` for `hot_exit` and the session facts on
Linux, macOS and Windows; `cargo test -p slate-uniffi hot_exit`;
`./apps/slate-windows/generate-bindings.ps1` and the Windows build and
`A11yCorpusCensus`; the Mac build and `A11yCorpusCensusTests`; the
mutation run per row above, locally.

## Acceptance

- This design merged after a fresh red team and Codex review.
- The core store, its FFI, its facts and the copy on `main` with green
  CI on every lane; both hosts compile; no host behaviour changed.
- WB-29 (Windows) and plan 43 PR 29 (Mac) can start.
