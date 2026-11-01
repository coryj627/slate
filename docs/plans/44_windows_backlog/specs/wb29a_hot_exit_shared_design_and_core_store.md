# WB-29a — One hot-exit design for both hosts, and the core store it fixes

Issues: Refs #1525 (both PRs of this chunk write `Refs`; #1525 closes in
whichever of WB-29 and plan 43 PR 29's mac half merges last, §5). Size L
(design first). Lanes: this design PR is docs only; the core-store PR
that follows is rust, windows and swift (compile: the FFI gains an
object, records, two session methods and five `A11yEvent` variants,
and both corpus mirrors move; core reads the Mac volume's UUID). AT: none for the design or the store (nothing a reader hears
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
The host passes its device-local root (Windows `SlateDeviceState.Root`,
Mac Application Support/Slate); the store keeps everything under
`<device_root>/hot-exit/v1/`. It never touches a vault.

**The vault key is an opaque record core makes.**
`VaultSession::hot_exit_key() -> HotExitKeyResult` returns
`Key { key: HotExitVaultKey }` or `Unavailable { reason: HotExitUnavailable }`
(§7). `HotExitVaultKey { text: String }` is plain data the host passes to
every store call; the host never builds or reads it. Core makes `text`
from an identity that survives a remount and that two folders never
share:

- **Windows:** the persistent `FileIdInfo` root identity, as
  `win:{volume:x16}-{id_high:x16}{id_low:x16}` (the recents store's
  lossless text, `FileRecentsStore.cs:85`). A weak identity (FAT, FAT32,
  exFAT) is `Unavailable`.
- **macOS:** the root folder's volume UUID (`getattrlist`
  `ATTR_VOL_UUID` on the held root) and its inode, as
  `mac:{volume-uuid}-{inode:x16}`. `st_dev` is not used: it is reused
  across removable media and can change when a volume remounts, and
  WB-28's Unix identity (`fs_root.rs:70-79`, always `persistent: true`)
  is built from it. A volume that reports no UUID, or whose file system
  (`statfs` `f_fstypename`) is `msdos`, `exfat`, `smbfs`, `nfs`, `afpfs`
  or `webdav`, is `Unavailable` (their inode numbers are not lasting).
- **Linux (no Slate host):** `Unavailable`. Core facts reach the store
  with explicit keys (§Tests).

The store hashes `text` into a directory name, so two spellings of one
folder are one directory: `hot-exit/v1/<hex(SHA-256(text))>/`. Inside:

- `epoch` — the vault's discard epoch, a decimal `u64` (§6);
- `entries/<entry-id>.json` — one file per buffer;
- `tombstones/<entry-id>.json` — a per-buffer discard floor (§6);
- `lock` — the vault's cross-process lock file (§8).

The store's root also holds `procs/<process-id>.lock`, one per running
store, held locked for the process's life (§6).

**The key within a vault is per tab (the peer rule).** An entry's id is
the host's tab id, an opaque string of at most 64 characters from
`[0-9A-Za-z_-]`; any other spelling is refused
(`HotExitError::InvalidEntryId`). Two tabs on one note are two entries,
so one tab's discard never removes its peer's newer generation. On the
Mac, where same-path tabs share one buffer (`WorkspaceState.swift:356-363`),
both entries carry the same text; restoring either is right.

**An entry** is UTF-8 JSON:
`{ "schema": 1, "entry_id", "path" (vault-relative, forward slashes),
"text", "baseline_hash" (the file's content hash when the buffer was
loaded or last saved; null for a note never saved), "generation" (u64,
§2), "epoch" (u64), "written_at" (RFC 3339 UTC) }`.

**Caps and retention.** Per entry: `text` at most 8 MiB of UTF-8. Per
vault: at most 256 entries and 64 MiB of entry files. Whole store: 256
MiB. A write over a cap returns
`Refused { refusal: HotExitRefusal::OverCap }` and writes nothing; the
tab's existing entry stays (older, but a valid copy). Retention:
`HotExitStore::open` deletes a vault directory none of whose files
changed in 30 days, and `read_buffers` returns nothing older than that;
so a vault never reopened, or a stray entry no tab holds, cannot fill the
store for good.

### 2. Generations, cadence, durability and results

**The generation is the store's ordering token, and the host seeds it.**
Every write carries a `generation` that the host raises by one per write
it issues for that tab. A tab's counter starts above anything the store
already knows for its id: `read_buffers` returns, per id it knows (entry
or tombstone), `next_generation = max(entry generation, tombstone floor) + 1`,
and the host starts that tab's counter there. Windows' in-memory
`ContentGeneration` (`WorkspaceViewModel.cs:281-291`, reset with each new
tab instance) is not this counter: WB-29 keeps a separate hot-exit
counter per tab, seeded at restore. A tab id the store does not know
starts at 1. So a restored tab's writes and discards are never below its
own history, in this process or after a crash.

**Cadence (the host's).** A tab that turns dirty writes at once; while
it stays dirty, a write follows 1 s after the last edit (a host
constant). A tab that turns clean calls `discard_buffer` (§6).

**Every write is durable-or-error.**
`write_buffer(key, entry: HotExitEntryWrite) -> Result<HotExitWriteOutcome, HotExitError>`
writes `entries/<id>.json.tmp-<random>` in the vault directory, calls
`sync_data` (`FlushFileBuffers` on Windows), renames it over the entry
(POSIX `rename`; Windows `MoveFileExW` with `MOVEFILE_REPLACE_EXISTING |
MOVEFILE_WRITE_THROUGH`), and syncs the directory where the platform can
(best effort, as `sync_parent`, `vault/fs.rs:1110-1132`). It returns
`Durable { generation }` only after the rename. An I/O failure is
`Err(HotExitError::Io { … })`, the temp file removed.

**A write that is not applied says why.**
`HotExitWriteOutcome::Superseded { by: HotExitSupersession }`:

- `Newer { stored_generation }` — the stored entry's generation is at
  least this one's (an older write finished after a newer one);
- `Floor { through_generation }` — the tab was discarded through this
  generation (§6);
- `Epoch { current }` — the vault was discarded after the host read its
  epoch (§6).

A flush (§5) counts a tab as kept only on `Durable { g }` or
`Newer { stored_generation: s }` with `g` or `s` at least the tab's
latest generation. `Floor` and `Epoch` mean the store holds nothing for
it; the host refreshes its epoch with `current_epoch(key)` on `Epoch`.

### 3. The restore rule

`read_buffers(key) -> Result<HotExitSnapshot, HotExitError>` returns
`{ epoch, entries: Vec<HotExitEntry>, next_generations: Vec<HotExitNextGeneration> }`:
every entry whose epoch equals the vault's current epoch and whose
generation is above its tombstone floor, and the per-id seeds (§2). Under
the vault lock it deletes temp files and files below the current epoch
or floor (left by a cut-short call); it never deletes a current entry.

The host classifies the entries through one session read:
`VaultSession::classify_hot_exit_entries(entries) -> Vec<HotExitRestore>`,
reading each `path` beneath the held root (no index, no scan):

| The vault now | Decision | What the host does |
|---|---|---|
| a file at `path` whose content hash == `baseline_hash` | `Restore { path }` | the tab opens on `path`, its buffer set to `text`, dirty |
| a file at `path` with another hash | `RestoreStale { path }` | the same, and the tab is externally stale; its next save passes `baseline_hash` and `save_text` returns `WriteConflict` (`session.rs:4577`), which blocks the save with the existing conflict sentence |
| no file at `path` | `Gone { path }` | a new unsaved note at `path`, dirty, holding `text`; its first save creates it exclusively |
| a file at `path` and `baseline_hash` null (the buffer was a new note) | `ExistsNow { path }` | a new unsaved note at `path`, dirty; its first save is create-exclusive, so it refuses rather than overwrite (`save_text(…, None)` would overwrite, `session.rs:4194-4196`) |
| `path` no longer validates (outside the vault, under `.slate`) | `Invalid` | never restored; the host discards the entry (`discard_buffer`) |

A note moved or renamed while Slate was closed restores as `Gone` at its
old path, its text intact; the reader saves it there or discards it. No
re-seat is made by content hash: a unique same-hash delete and create is
not a rename (`rescan.rs:193-207`), and re-seating on it could put the
edits onto an unrelated file (an empty note, a template). Re-seat by
file identity needs per-file ids the index does not keep (§Deviations).

**Mapping to tabs.** The host restores its saved layout as today, then,
for each classified entry: the restored tab with the entry's id takes the
classified path (its item is replaced when the layout's path differs —
`ReplaceTabItem` keeps a tab's id while it changes the path,
`Layout.cs:319-337`) and the entry's text, dirty; an entry with no such
tab opens as a new tab **with the entry's id** in the active group, in
`written_at` order. Every restored tab seeds its counter from
`next_generations` (§2). So a later save or discard of that tab
discards that entry, and nothing restores twice.

### 4. The open sequence and the copy

**Order.** Session open → identity admission (WB-28) → `hot_exit_key()`
→ `read_buffers` and `classify_hot_exit_entries` (off the UI thread, on
the open's load worker: the snapshot can be 64 MiB) → the layout restore
with the classified buffers injected into the tabs it constructs → the
workspace becomes ready once, after the buffers are installed →
`UnsavedNotesRestored`. Windows reads after the open scan
(`VaultLifecycleViewModel.cs:657-675`) and before the `WorkspaceViewModel`
construction (`:1530`), passing the restored set in, because `RestoreNode`
builds new tab instances; `WorkspaceReady` (`:1631`) is raised after the
buffers are installed, so the launch landing (`MainWindow.xaml.cs:486-495`)
happens once. The Mac reads at `restoreWorkspaceLayout`
(`AppState.swift:10827`), which runs before its scan task: the
classification reads paths, not the index, so it needs no scan, and the
buffers are installed before any editor shows (§Deviations).

**Copy (core).** New `A11yEvent` variants, rendered in core, in the
corpus and the committed fixture, and in both host corpus mirrors in the
store PR:

| Event | Priority | Sentence |
|---|---|---|
| `UnsavedNotesRestored { count }` | Medium | "Restored 1 unsaved note." / "Restored {count} unsaved notes." |
| `RestoredNoteChangedOnDisk { name }` | Medium | "{name} changed on disk after its unsaved edits were kept. The edits are restored; saving will stop and say so before anything is overwritten." |
| `RestoredNoteMissingOnDisk { name }` | Medium | "{name} is no longer on disk. Its unsaved edits are restored as a new note." |
| `RestoredNoteExistsOnDisk { name }` | Medium | "{name} now exists on disk. Its unsaved edits are restored as a new note, and saving will not overwrite the file." |
| `UnsavedNotesNotKept { reason }` | High | the status line when application close or a vault switch falls back to the prompt: "Your unsaved notes cannot be kept for next time: {reason}." |

`HotExitUnavailable` and `HotExitRefusal` render their `reason` in core:
the weak-root sentence (§7), "a note is larger than the 8 MB the store
keeps", "the store for this vault is full", "the store could not be
written" and "saving them took longer than 5 seconds". The host composes
none of them.

### 5. Interlocks: closing, switching, Close Vault

- **Application close and vault switch never ask once the latest
  generations are kept.** The host issues each dirty tab's latest
  generation, waits for the outcomes at most 5 s, and closes without a
  prompt when every tab is kept (§2's rule). Otherwise it falls back to
  today's prompt, the status line speaking `UnsavedNotesNotKept` with the
  first reason. The layout save on close keeps running as today. On the
  Mac the flush runs inside `admitTermination`'s `.terminateLater` window
  (`AppState.swift:11927-11949`), before its existing termination steps.
- **Close Vault still asks.** Save All saves as today, and each tab that
  turns clean discards its entry. Discard All fences (§6) and calls
  `discard_vault`, which also removes any entry no tab holds.
- **Explicit save stands (RD1, #877).** Hot exit never writes a note into
  the vault.

### 6. Every Discard fences pending writes; the store supersedes them

Both mechanisms are durable before any delete.

- **The vault's discard epoch.** `discard_vault(key) -> Result<u64, HotExitError>`
  takes the vault lock, writes `epoch + 1` (temp + `sync_data` + rename),
  then deletes every entry and tombstone, and returns the new epoch. A
  write whose epoch is below the current one returns
  `Superseded { by: Epoch }`. A missing `epoch` file reads as the highest
  epoch among the vault's files (never lower than a write the host was
  told is durable). The host's epoch comes from `read_buffers` and from
  each `discard_vault` it makes.
- **A per-buffer floor.** `discard_buffer(key, entry_id, through_generation)`
  writes `tombstones/<id>.json` = `{ through_generation, process }`
  durably, then deletes the entry if its generation is not above the
  floor. A later write for that id at or below the floor returns
  `Superseded { by: Floor }`. `process` is the random 128-bit id the
  store minted at open. `read_buffers` prunes a tombstone only when its
  process no longer runs: when `procs/<process>.lock` can be locked
  (each store holds its own lock file for its life, so a live process's
  floors are never pruned, even across a relaunch while an abandoned
  write still runs). `discard_vault` clears every tombstone.

**The routes that fence.** Close Vault's Discard All (`discard_vault`);
the fallback prompt's Discard All on application close and vault switch
(`discard_vault`, issued while an older write may still be in flight —
the epoch drops it); a clean transition (`discard_buffer` through the
generation that turned clean); a dirty tab's own Discard — closing it,
closing its pane, or replacing its document (`discard_buffer` through its
latest generation); and an `Invalid` entry at restore. A host awaits its
discards at most 5 s, then proceeds and logs the unconfirmed discard;
the floor or epoch still supersedes a late write once the discard lands.

### 7. A weak root: no hot exit

The owner's call (WD20, 2026-10-26) picks policy (a). `hot_exit_key()`
is `Unavailable { reason: WeakRoot }` for a root identity that is not
persistent on Windows, for the Mac volumes §1 names, and on Linux; the
reason renders "this folder's file system cannot keep a lasting identity
for the vault". The host has no key, so it never writes or reads for that
vault; application close and vault switch fall back to today's prompt,
speaking `UnsavedNotesNotKept`; a crash keeps nothing, as on `main`.

R1 (no buffer is restored into a folder other than the one whose session
wrote it) holds by construction: only an identity two folders never
share, and that survives a remount, makes a key. R2 holds because a weak
root's close promised nothing: it prompted. R3: store facts prove two
distinct keys never read each other's entries; core facts prove
`hot_exit_key()` is `Unavailable` under WB-28's seams
(`SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED`,
`SLATE_TEST_ROOT_FILE_INDEX_COLLIDES`) on Windows, and under a seam that
makes the Mac's volume report an excluded file system or no UUID.

### 8. The store's calls borrow no vault session

`write_buffer`, `discard_buffer`, `discard_vault`, `current_epoch` and
`read_buffers` are methods of `HotExitStore`, whose state is the device
root, a per-vault mutex map and its process id and lock. They take the
`HotExitVaultKey` record. None takes a `VaultSession`, so no binding call
keeps a session alive after `session.Dispose()`, and no teardown joins a
recovery write. The object is `Send + Sync`: calls for one vault
serialise on its in-process mutex and on the vault directory's `lock`
file (an advisory lock: `LockFileEx` on Windows, `flock` on the Mac),
held for each call, so two Slate processes on one vault serialise too;
calls for two vaults run in parallel. A call cut short by process exit
leaves the previous durable state: a write before its rename leaves the
old entry and a temp file; a discard after its epoch or floor and before
its deletes leaves files that `read_buffers` ignores and deletes.

`classify_hot_exit_entries` and `hot_exit_key` are session methods,
called on the open sequence before the workspace exists; neither is a
recovery write.

## Deviations from WB-29's list, for the owner and Codex

1. **No re-seat of a moved note (item 3).** A moved note restores as
   `Gone` at its old path, edits intact. Re-seating by content hash
   contradicts the rescan's pinned rule (`rescan.rs:193-207`) and can
   misplace edits; re-seating by file identity needs per-file ids the
   index does not keep. A later plan can add it.
2. **The Mac reads before its scan (WD20's order sentence).** WD20 says
   the recovery state is read after the open scan. Windows does exactly
   that. The Mac restores its layout before its scan
   (`AppState.swift:10827-10838`); since the classification reads paths
   beneath the held root and no index, the Mac reads there, before any
   editor shows, rather than injecting into tabs the reader may already
   have typed in.

## The core store PR (after this design merges)

New module `crates/slate-core/src/hot_exit.rs` (the store, its file
lock, the process lock) and its FFI in `crates/slate-uniffi/src/lib.rs`:
the object; the records `HotExitVaultKey`, `HotExitKeyResult`,
`HotExitUnavailable`, `HotExitEntryWrite`, `HotExitEntry`,
`HotExitNextGeneration`, `HotExitSnapshot`, `HotExitWriteOutcome`,
`HotExitSupersession`, `HotExitRefusal`, `HotExitRestore`; `HotExitError`;
the session methods `hot_exit_key` and `classify_hot_exit_entries`; the
five `A11yEvent` variants with their corpus rows; and the Mac volume UUID
and file-system read. Windows and Mac compile against the new bindings
(the corpus mirrors move); neither host calls the store until WB-29 and
PR 29.

## Tests (the core store PR)

Core facts, `crates/slate-core/src/hot_exit/tests.rs` unless noted. Each
asserts on the files on disk as well as through `read_buffers`, so a
read-side filter cannot hide a write that should not have happened.

| Fact | Proves | Mutation it kills |
|---|---|---|
| `a_durable_write_is_read_back_after_the_store_reopens` | write → new store on the root → the entry is read back | skip the rename |
| `two_tabs_on_one_note_are_two_entries_and_one_discard_keeps_the_other` | the peer rule | key by path |
| `an_older_generation_never_replaces_a_newer_one` | `Superseded { by: Newer }` with the stored generation; the newer text stays on disk | `>=` / no check |
| `a_superseded_write_says_why` | `Newer`, `Floor` and `Epoch` each returned for its own cause | one undifferentiated variant |
| `a_restored_tabs_counter_starts_above_its_history` | `next_generations` = max(entry, floor) + 1; restore → write gen `next` → `Durable`; restore → discard through `next` → nothing restores | seed from 0; ignore the floor |
| `a_write_issued_before_a_vault_discard_is_superseded_even_if_it_lands_later` | the epoch; no file written | skip the epoch compare |
| `a_discard_cut_short_after_its_epoch_hides_every_older_entry` | a seam between the epoch write and the deletes; the next read returns nothing and deletes the old files | bump the epoch after the deletes |
| `a_buffer_discard_supersedes_its_earlier_generations` | the floor | skip the floor |
| `a_live_processs_floor_is_never_pruned` | a second store on the same root does not prune the first's tombstones while the first is open; after the first drops, the next read prunes them | prune by process id alone |
| `a_lost_epoch_file_never_lowers_the_epoch` | the epoch file deleted: the read epoch is the highest among the files | reset to 0 |
| `a_write_over_a_cap_is_refused_and_leaves_the_older_entry` | the 8 MiB, 256-entry, 64 MiB and 256 MiB caps | write before the check |
| `a_vault_untouched_for_thirty_days_is_pruned_at_open` | retention, with an injected clock | no retention |
| `a_cut_short_write_keeps_the_previous_entry` | a seam before the rename; the previous entry stays; the next read deletes the temp file | write in place |
| `two_vault_keys_never_read_each_others_entries` | R1 for distinct keys | drop part of the key |
| `an_invalid_entry_id_is_refused` | ids outside `[0-9A-Za-z_-]{1,64}`, nothing written | accept any id |
| `two_processes_on_one_vault_serialise_on_its_lock` | two stores on one root racing writes and a `discard_vault` leave no pre-discard entry on disk | no file lock |
| session: `a_weak_root_has_no_hot_exit_key` (Windows, WB-28 seams; Mac, the volume seam; Linux) | `Unavailable { WeakRoot }` | key a weak identity |
| session: `the_mac_key_survives_a_remount_and_two_volumes_never_share_one` (macOS) | the key is the volume UUID and inode, not `st_dev` | key by `st_dev` |
| session: `classify_decides_restore_stale_gone_exists_now_and_invalid` | each row of §3's table, including an empty note deleted beside another empty note (`Gone`, never re-seated) and a null baseline over a file now on disk (`ExistsNow`) | treat a changed file as `Restore`; re-seat by hash; restore a null baseline over a file as `Restore` |
| a11y: the five variants render, sit at their priorities, and appear in the corpus and fixture | the copy | — |

uniffi: `hot_exit_store_round_trips_through_the_binding` (open, write,
read, discard through the exported object). Hosts: both corpus mirrors
gain the five events (the Rust tripwires fail until they do); no host
behaviour changes. WB-29 and PR 29 add the host facts: restore → edit →
reopen gets the new text; restore → save → reopen gets nothing; a
restored entry with no layout tab keeps its id; a layout tab whose path
differs from its entry's takes the entry's path; two dirty peers, one
discards, reopen restores the other.

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

## Review record

- **Fresh red team, round 1 (2026-11-01), taken:**
  - generations are seeded from the store (`next_generations`), not from a host's per-instance counter;
  - `Superseded` says why (`Newer`, `Floor`, `Epoch`), and only a newer durable generation counts as kept;
  - no re-seat by content hash (the rescan pins the opposite); a moved note restores as `Gone`;
  - a restored entry with no layout tab opens a tab with the entry's id, and a layout tab takes its entry's path;
  - the Mac key is the volume UUID and inode, `Unavailable` on removable and network file systems;
  - a null baseline over a file now on disk is `ExistsNow`, saved create-exclusive;
  - retention, a whole-store cap, and `Invalid` entries discarded at restore;
  - the copy corrected, with all five sentences in core;
  - cross-process: a per-vault file lock and per-process lock files guard tombstone pruning;
  - the Mac flush runs inside `.terminateLater`;
  - facts assert on disk; a crash seam between the epoch and the deletes.
