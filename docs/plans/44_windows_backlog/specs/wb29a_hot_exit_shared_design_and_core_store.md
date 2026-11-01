# WB-29a — One hot-exit design for both hosts, and the core store it fixes

Issues: Refs #1525 (both PRs of this chunk write `Refs`; #1525 closes in
whichever of WB-29 and plan 43 PR 29's mac half merges last, §5). Size L
(design first). Lanes: this design PR is docs only; the core-store PR
that follows is rust, windows and swift (compile: the FFI gains an
object, records, three session methods and seven `A11yEvent`
variants, and both corpus mirrors move; core reads the Mac volume's
UUID and birth time). AT: none for the design or the store (nothing a reader hears
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

The store protocol below was rebuilt from first principles after Codex's
round 1 (§Review record): every writer owns its entry through a claim, a
newer claim or a discard invalidates every older writer, every check and
publish happens under one short lock, and nothing the reader has not
seen is deleted by the store on its own.

### 1. The store, its key and its caps

**A process-lifetime core object.** `HotExitStore` is a uniffi
`#[uniffi::Object]` (like `CommandRegistry`, `lib.rs:8788`, one instance
for the app's lifetime), constructed by
`HotExitStore::open(device_root: String) -> Result<Arc<HotExitStore>, HotExitError>`.
The host passes its device-local root (Windows `SlateDeviceState.Root`,
Mac Application Support/Slate); the store keeps everything under
`<device_root>/hot-exit/v1/`. It never touches a vault.

**The vault key is an opaque record core makes, only for a lasting
identity.** `VaultSession::hot_exit_key() -> HotExitKeyResult` returns
`Key { key: HotExitVaultKey }` or `Unavailable { reason }` (§7).
`HotExitVaultKey { text: String }` is plain data the host passes to the
store; the host never builds or reads it. Core makes `text` only when
the root's identity is lasting and local:

- **Windows:** `win:{volume:x16}-{id_high:x16}{id_low:x16}-{created:x16}`
  from the held root's `FILE_ID_INFO` and `FILE_BASIC_INFO.CreationTime`.
  `Unavailable` when the identity is weak (FAT, FAT32, exFAT), when the
  128-bit id is a sentinel (all zeros or all ones, which MS-FSCC says to
  ignore) or the volume serial is zero, or when the volume is remote
  (`FILE_REMOTE_DEVICE` in its characteristics). The creation time keeps
  a deleted root's reused file id from naming the new folder.
- **macOS:** `mac:{volume-uuid}-{inode:x16}-{birth:x16}` from the held
  root (`getattrlist` `ATTR_VOL_UUID`, `ATTR_CMN_CRTIME`). `st_dev`, which
  WB-28's Unix identity uses (`fs_root.rs:70-79`), is not: it is reused
  across removable media and can change on remount. `Unavailable` when
  the volume reports no UUID, is not local (`MNT_LOCAL` clear), or its
  file system (`f_fstypename`) is `msdos` or `exfat`.
- **Linux (no Slate host):** `Unavailable`; core facts reach the store
  with explicit keys (§Tests).

The store hashes `text` into a directory name:
`hot-exit/v1/vaults/<hex(SHA-256(text))>/`. Inside it:

- `epoch` — the vault's discard epoch, a decimal `u64`; created with the
  directory and never deleted afterwards;
- `claims/<entry-id>` — the current claim on an entry: a random 128-bit
  incarnation id and the epoch it was made in;
- `entries/<entry-id>.json` — the entry's latest durable text;
- `lock` — the vault's cross-process lock file (§8).

**The key within a vault is per tab (the peer rule).** An entry id is
the host's tab id, an opaque string of at most 64 characters from
`[0-9A-Za-z_-]`; anything else is refused (`HotExitError::InvalidEntryId`).
Two tabs on one note are two entries, so one tab's discard never removes
its peer's text. On the Mac, where same-path tabs share one buffer
(`WorkspaceState.swift:356-363`), both entries carry the same text.

**An entry** is UTF-8 JSON: `{ "schema": 1, "entry_id", "incarnation",
"generation" (u64), "path" (vault-relative, forward slashes), "text",
"baseline_hash" (the note's content hash when the buffer was loaded or
last saved; null for a note never saved), "baseline_file_id" (the note's
file identity then, from `VaultSession::note_file_identity(path)`; null
when the platform gives none), "written_at" (RFC 3339 UTC) }`.

**Caps.** Per entry: 8 MiB of text. Per vault: 256 entries and 64 MiB.
Per store: 32 vault directories (so at most 2 GiB in all), checked when
a vault's directory is created, under a store-wide lock held only for
that check and the creation. A write or a first claim over a cap returns
`Refused { refusal }` and changes nothing. **The store never deletes an
entry the reader has not acted on**: no retention, no pruning of unread
text. An entry leaves only through its tab's save or discard, Close
Vault's Discard All, or a restore that the next write supersedes. A full
store refuses new writes, so closing falls back to the prompt (§5).

### 2. Claims, writes and what an outcome means

**A tab owns its entry through a claim.** Before its first write, and
again after every clean transition, a tab calls
`claim_entry(key, epoch, entry_id) -> Result<HotExitClaimOutcome, HotExitError>`.
Under the vault lock the store checks `epoch` against the vault's
current epoch (`Superseded { by: Epoch }` if older), writes
`claims/<id>` = a fresh random incarnation durably (§2, durability), and
returns `Claimed { claim: HotExitClaim { entry_id, incarnation, epoch } }`.
A newer claim replaces an older one, so every writer holding the older
incarnation — another tab instance, another process, a write still in
flight — is invalidated at once. Generations count within one claim: the
host starts at 1 and raises it per write.

**A write publishes only under its own live claim.**
`write_buffer(key, claim, generation, entry: HotExitEntryWrite) -> Result<HotExitWriteOutcome, HotExitError>`:

1. **Outside the lock** it writes the entry to
   `entries/<id>.json.tmp-<random>` and makes it durable (below). A
   stalled payload write holds no lock and delays no other call.
2. **Under the vault lock** it checks, in order: the vault's epoch equals
   `claim.epoch` (else `Superseded { by: Epoch }`); `claims/<id>` holds
   `claim.incarnation` (else `Superseded { by: Claim }`); the stored entry,
   if it has this incarnation, has a lower generation (else
   `Superseded { by: Newer { stored_generation } }`); the vault's caps
   (else `Refused`). Then it renames the temp file over the entry and makes
   the rename durable, and returns `Durable { generation }`.
3. A failure anywhere is `Err(HotExitError::Io { … })` and removes the
   temp file. A failure after the rename (the durability step) is also an
   error: the host treats it as not kept, though the new or the old
   entry — both valid copies — may survive.

**A tab counts as kept only on its own durable text.** For a flush (§5)
a tab is kept when its latest issued generation returned `Durable`, or
when an earlier one returned `Newer { stored_generation }` at or above
the latest under the same claim. `Superseded { by: Claim }` and
`{ by: Epoch }` mean the store does not hold this tab's text: the tab is
not kept. Two processes restoring one tab id therefore never both count
as kept: the later claim wins, and the other learns it at its next
write.

**Durability, per platform; failures propagate.** A temp file is synced
before its rename; the rename and the directory are synced after.
Windows: `FlushFileBuffers` on the file, then `MoveFileExW` with
`MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH` (the documented way
to make a rename's metadata durable; NTFS journals it). macOS:
`fcntl(F_FULLFSYNC)` on the file, `rename`, then `F_FULLFSYNC` on the
directory (macOS `fsync` does not flush the drive cache). A directory the
store creates is made durable in its parent the same way. The `epoch` and
`claims/` files use the same sequence. Any failed sync is an error — the
store never reports `Durable` on a best-effort step (unlike the vault's
`sync_parent`, `vault/fs.rs:1110-1132`, which ignores errors). All I/O
goes through one internal seam (`HotExitFs`) so facts can fail any step
and see the order of calls (§Tests).

**Cadence (the host's).** A tab that turns dirty claims and writes at
once; while dirty, a write follows 1 s after the last edit. A tab that
turns clean releases its claim (§6).

### 3. The restore rule

`read_buffers(key) -> Result<HotExitSnapshot, HotExitError>` returns
`{ epoch, entries: Vec<HotExitEntry> }` under the vault lock. It deletes
temp files, an entry whose claim file is missing and whose incarnation
the claim does not name (a release or discard cut short after its claim
step), and nothing else. A missing `epoch` file in an existing directory
is `Err(HotExitError::Damaged)` (fail closed: no claim, no write and no
restore for that vault until Close Vault's Discard All recreates it; the
entries stay on disk), so a lost epoch can never fall back to 0 and let
an older write in.

The host classifies the entries through one session read,
`VaultSession::classify_hot_exit_entries(entries) -> Vec<HotExitRestore>`,
reading each `path` beneath the held root (no index, no scan):

| The vault now | Decision | What the host does |
|---|---|---|
| a file at `path`, its file identity == `baseline_file_id` (or either is null) and its hash == `baseline_hash` | `Restore { path }` | the tab opens on `path`, buffer = `text`, dirty |
| a file at `path`, same identity (or null), another hash | `RestoreStale { path }` | the same, externally stale; its next save passes `baseline_hash` and `save_text` returns `WriteConflict` (`session.rs:4577`), which blocks the save |
| a file at `path` whose identity differs from a non-null `baseline_file_id`, or `baseline_hash` null | `ExistsNow { path }` | a new unsaved note at `path`, dirty; its first save is create-exclusive, so it refuses rather than overwrite the file now there |
| no file at `path` | `Gone { path }` | a new unsaved note at `path`, dirty; its first save creates it exclusively |
| `path` no longer validates (outside the vault, under `.slate`) | `Recovered { path }` | a new unsaved note at `Recovered note.md` (then `Recovered note 2.md`, …) at the vault root, holding `text`; nothing is dropped unread |

A note moved or renamed while Slate was closed restores as `Gone` at its
old path, its text intact (§Deviations). An unrelated file put at a
note's old path is caught by its file identity where the platform gives
one; with no identity on either side, a replacement with identical bytes
restores as `Restore` (an accepted risk, §Deviations).

**Mapping to tabs.** The host restores its saved layout as today. For
each classified entry, the restored tab with the entry's id takes the
classified path (its item replaced when the layout's path differs —
`ReplaceTabItem` keeps a tab's id while it changes the path,
`Layout.cs:319-337`) and the entry's text, dirty; an entry with no such
tab opens as a new tab **with the entry's id** in the active group, in
`written_at` order. Each restored tab then claims its entry (§2): the new
incarnation invalidates any writer of the old one, and its first write
replaces the restored entry. So the entry restores at most once more
than it is written, and a later save or discard of the tab removes it.

### 4. The open sequence and the copy

**Order.** Session open → identity admission (WB-28) → `hot_exit_key()`
→ `read_buffers` and `classify_hot_exit_entries`, off the UI thread on
the open's load worker → the layout restore with the classified buffers
injected into the tabs it constructs → the workspace becomes ready once,
after the buffers are installed → `UnsavedNotesRestored`. Windows reads
after the open scan (`VaultLifecycleViewModel.cs:657-675`) and before
the `WorkspaceViewModel` construction (`:1530`), passing the set in, since
`RestoreNode` builds new tab instances; `WorkspaceReady` (`:1631`) is
raised after, so the launch landing (`MainWindow.xaml.cs:486-495`)
happens once. The Mac reads at `restoreWorkspaceLayout`
(`AppState.swift:10827`), before its scan task, and installs the dirty
buffers before `activateTab` (§Deviations).

**Copy (core).** New `A11yEvent` variants, rendered in core, in the
corpus and the committed fixture, and in both host corpus mirrors:

| Event | Priority | Sentence |
|---|---|---|
| `UnsavedNotesRestored { count }` | Medium | "Restored 1 unsaved note." / "Restored {count} unsaved notes." |
| `RestoredNoteChangedOnDisk { name }` | Medium | "{name} changed on disk after its unsaved edits were kept. The edits are restored; saving will stop and say so before anything is overwritten." |
| `RestoredNoteMissingOnDisk { name }` | Medium | "{name} is no longer on disk. Its unsaved edits are restored as a new note." |
| `RestoredNoteExistsOnDisk { name }` | Medium | "Another file is now at {name}. Its unsaved edits are restored as a new note, and saving will not overwrite that file." |
| `RestoredNoteRecovered { name }` | Medium | "An unsaved note whose place is no longer in the vault is restored as {name}." |
| `UnsavedNotesNotKept { reason }` | High | "Your unsaved notes cannot be kept for next time: {reason}." |
| `UnsavedNotesNotDiscarded` | High | "Slate could not yet discard the kept copies of your unsaved notes, so the vault stays open. Try again, or cancel." |

The reasons (`HotExitUnavailable`, `HotExitRefusal`, `HotExitError`)
render in core too: the weak-root sentence (§7), "this vault is on a
network drive", "a note is larger than the 8 MB the store keeps", "the
store for this vault is full", "the store holds notes for too many
vaults", "the store could not be written", "the store's records for this
vault are damaged", "saving them took longer than 5 seconds".

### 5. Interlocks: closing, switching, Close Vault

- **Application close and vault switch never ask once every dirty tab is
  kept.** The host issues each dirty tab's latest generation and every
  release still pending (§6), waits at most 5 s, and closes without a
  prompt when every tab is kept and every release confirmed. Otherwise it
  falls back to today's prompt, speaking `UnsavedNotesNotKept` with the
  first reason. The layout save on close runs as today. On the Mac the
  flush runs inside `admitTermination`'s `.terminateLater` window
  (`AppState.swift:11927-11949`), before its existing termination steps.
- **A Discard All completes only once its fence is durable.** Close
  Vault's Discard All, and the fallback prompt's Discard All on
  application close and vault switch, call `discard_vault` and wait for it
  at most 5 s. It never waits behind a stalled payload write (§2: payload
  I/O holds no lock). If it has not committed by then, or failed, the
  close does not proceed: the vault stays open and the prompt speaks
  `UnsavedNotesNotDiscarded` (Try again / Cancel). No worker is joined:
  the call keeps running and a later attempt sees its outcome.
- **Close Vault still asks.** Save All saves as today; each tab that
  turns clean releases its claim.
- **Explicit save stands (RD1, #877).** Hot exit never writes a note into
  the vault.

### 6. Discards: releases and the vault epoch

- **A tab's discard releases its claim.**
  `release_entry(key, claim) -> Result<HotExitReleaseOutcome, HotExitError>`
  takes the vault lock; if `claims/<id>` still holds `claim.incarnation`
  and the epoch matches, it deletes the claim file (durably), then the
  entry (durably), and returns `Released`; otherwise it returns
  `Superseded { by: Claim | Epoch }` and deletes nothing. Every write of
  that incarnation that lands later finds no claim and is `Superseded`.
  A stale release — delayed past a newer claim or a vault discard — can
  never delete a newer entry. Routes: a clean transition (a save landed,
  undo to the baseline, a reload) and a dirty tab's own Discard (closing
  it, closing its pane, replacing its document). The host awaits a
  release at most 5 s; one not confirmed by then stays pending and is
  part of the next close's flush (§5), so a release that never landed
  makes that close fall back to the prompt rather than exit.
- **A vault discard advances the epoch first.**
  `discard_vault(key) -> Result<u64, HotExitError>` takes the vault lock,
  writes `epoch + 1` durably, then deletes every claim and entry (durably)
  and returns the new epoch. A claim, write or release carrying an older
  epoch is `Superseded { by: Epoch }`. Cut short after the epoch, it
  leaves entries whose claims name the old epoch; `read_buffers` returns
  only entries whose claim is current, and deletes the rest.

### 7. A weak root: no hot exit

The owner's call (WD20, 2026-10-26) picks policy (a). `hot_exit_key()`
is `Unavailable { reason }` for every root §1 excludes: a weak, sentinel
or remote identity on Windows; a volume with no UUID, not local, or
`msdos`/`exfat` on the Mac; Linux. The host has no key, so it never
claims, writes or reads for that vault; application close and vault
switch fall back to today's prompt, speaking `UnsavedNotesNotKept`; a
crash keeps nothing, as on `main`.

R1 (no buffer restored into a folder other than the one whose session
wrote it) holds because only a local, non-sentinel, lasting identity
with the root's creation time makes a key. R2 holds because a weak
root's close promised nothing: it prompted. R3: store facts prove two
distinct keys never read each other's entries; core facts prove
`Unavailable` under WB-28's seams, under a seam returning a sentinel
`FILE_ID_INFO`, under a remote-volume seam, and under the Mac volume
seam. The residual — a tool rewriting a root's creation time — changes
the key and orphans its entries (never restores them elsewhere); the
entries stay on disk until a Close Vault Discard All on a vault with
that key, or until the reader deletes the store (§Deviations).

### 8. The store's calls borrow no vault session

`claim_entry`, `write_buffer`, `release_entry`, `discard_vault`,
`current_epoch` and `read_buffers` are methods of `HotExitStore`, whose
state is the device root and a per-vault mutex map. They take the
`HotExitVaultKey` record and a claim; none takes a `VaultSession`, so no
binding call keeps a session alive after `session.Dispose()`, and no
teardown joins a recovery call. The object is `Send + Sync`. Every check
and publish for one vault runs under its in-process mutex and its
directory's `lock` file (an advisory lock, `LockFileEx` on Windows,
`flock` on the Mac), held only for the check, the rename and their
syncs — never for a payload write — so two Slate processes on one vault
serialise, and calls for two vaults run in parallel. A call cut short by
process exit leaves the previous durable state (§2, §6).

`hot_exit_key`, `note_file_identity` and `classify_hot_exit_entries` are
session methods: the first and last run on the open sequence before the
workspace exists; `note_file_identity` runs when a note loads or a save
lands, on the save path that already holds the session. None is a
recovery write.

## Deviations and accepted risks, for the owner and Codex

1. **No re-seat of a moved note (WB-29 item 3).** A moved note restores
   as `Gone` at its old path, edits intact. Re-seating by content hash
   contradicts the rescan's pinned rule (`rescan.rs:193-207`) and can
   misplace edits; re-seating by file identity needs per-file ids the
   index does not keep. Codex round 1 accepted this.
2. **The Mac reads before its scan (WD20's order sentence).** Windows
   reads after its scan, as WD20 says. The Mac restores its layout before
   its scan (`AppState.swift:10827-10838`); the classification reads
   paths beneath the held root and no index, so the Mac reads there and
   installs the dirty buffers before `activateTab`. Codex round 1
   accepted this.
3. **Accepted risk: an identical-bytes replacement with no file
   identity.** Where neither the entry nor the file now at its path has a
   file identity, an unrelated file with exactly the baseline's bytes
   restores as `Restore`, and its first save overwrites it. Windows and
   the Mac give identities on every volume that has a key, so this needs
   a platform identity failure plus identical bytes.
4. **Accepted risk: a root whose creation time is rewritten.** Its key
   changes; its kept notes are orphaned, never restored elsewhere, and
   stay on disk.
5. **Accepted risk: a store that fills with entries for vaults never
   reopened.** They are never deleted unread; the store then refuses new
   writes and closing prompts. A later host command can offer to review
   or discard them.

## The core store PR (after this design merges)

New module `crates/slate-core/src/hot_exit.rs` (the store, its claims,
its locks, the `HotExitFs` seam) and its FFI in
`crates/slate-uniffi/src/lib.rs`: the object; the records
`HotExitVaultKey`, `HotExitKeyResult`, `HotExitClaim`,
`HotExitClaimOutcome`, `HotExitEntryWrite`, `HotExitEntry`,
`HotExitSnapshot`, `HotExitWriteOutcome`, `HotExitReleaseOutcome`,
`HotExitSupersession`, `HotExitRefusal`, `HotExitUnavailable`,
`HotExitRestore`; `HotExitError`; the session methods `hot_exit_key`,
`note_file_identity` and `classify_hot_exit_entries`; the seven
`A11yEvent` variants with their corpus rows; and the Mac volume UUID,
birth time and file-system read. Windows and Mac compile against the new
bindings (the corpus mirrors move); neither host calls the store until
WB-29 and PR 29.

## Tests (the core store PR)

Core facts in `crates/slate-core/src/hot_exit/tests.rs` unless noted.
Every fact asserts on the files on disk as well as through the API. The
`HotExitFs` seam records the order of every write, sync and rename and
can fail any one of them; lock facts run a second **OS process** (the
test binary re-invoked as a child) with barriers that hold one side
inside its critical section while the other tries to enter.

| Fact | Proves | Mutation it kills |
|---|---|---|
| `a_durable_write_is_read_back_after_the_store_reopens` | write → new store → read back | skip the rename |
| `durable_means_synced_then_renamed_then_synced` | the recorded order is file sync, rename, directory (or write-through) sync, for entries, claims and the epoch | drop any sync; sync after the rename only |
| `a_failed_sync_is_never_reported_durable` | each sync step failed in turn → `Err`, never `Durable`; before the rename the old entry stays | ignore a sync error |
| `two_tabs_on_one_note_are_two_entries_and_one_release_keeps_the_other` | the peer rule | key by path |
| `an_older_generation_never_replaces_a_newer_one` | `Newer { stored_generation }` under one claim; newer text on disk | `>=` / no check |
| `a_newer_claim_invalidates_every_older_writer` | claim A, claim B on one id, A's write → `Superseded { Claim }`, B's text on disk | no claim check |
| `two_processes_restoring_one_tab_never_both_count_as_kept` | child process claims and writes; the parent's write after it → `Claim`; and the reverse order | generation-only ordering |
| `a_release_invalidates_its_own_late_writes` | release, then a delayed write of that claim → `Superseded`, no entry on disk | release without deleting the claim |
| `a_stale_release_never_deletes_a_newer_entry` | release of claim A delayed past claim B's write → `Superseded`, B's entry stays; same past a vault discard and a new claim | release by id alone |
| `a_vault_discard_supersedes_every_earlier_claim_write_and_release` | each kind issued before `discard_vault`, landing after → `Epoch`, nothing on disk | skip the epoch check |
| `a_discard_cut_short_after_its_epoch_hides_every_older_entry` | a seam fault between the epoch and the deletes; the next read returns nothing and deletes the files | bump the epoch after the deletes |
| `a_stalled_payload_write_never_blocks_a_discard` | a seam holds a payload write; `discard_vault` completes; the held write then lands as `Superseded` | hold the lock during the payload write |
| `a_lost_epoch_fails_closed` | `epoch` deleted from an emptied vault directory → `Damaged` for claim, write and read; a write from before cannot land | reset to 0 |
| `a_write_or_claim_over_a_cap_is_refused_and_changes_nothing` | 8 MiB, 256 entries, 64 MiB, 32 vaults (the vault cap under racing first claims from two threads and from a child process) | check outside the store lock; write before the check |
| `nothing_unread_is_ever_deleted` | entries untouched for a year (injected clock) and entries whose path no longer validates are still returned by `read_buffers` | any retention or pruning |
| `a_cut_short_write_keeps_the_previous_entry` | a fault before the rename; previous entry stays; the next read deletes the temp | write in place |
| `two_vault_keys_never_read_each_others_entries` | R1 for distinct keys, including keys differing only in the creation time | drop part of the key |
| `an_invalid_entry_id_is_refused` | ids outside `[0-9A-Za-z_-]{1,64}`, nothing written | accept any id |
| `the_vault_lock_excludes_another_process` | the child holds the lock inside a publish (barrier); the parent's claim blocks until it is released | no file lock |
| session: `no_hot_exit_key_without_a_lasting_local_identity` | `Unavailable` under the WB-28 seams, a sentinel `FILE_ID_INFO` seam, a remote-volume seam (Windows), the volume seam (macOS), and on Linux | key a weak, sentinel or remote identity |
| session: `the_key_includes_the_roots_creation_time` | a root deleted and recreated with a reused id (seam) gets another key | omit the creation time |
| session: `the_mac_key_survives_a_remount_and_two_volumes_never_share_one` (macOS) | volume UUID, inode and birth time, never `st_dev` | key by `st_dev` |
| session: `classify_decides_each_row` | `Restore`, `RestoreStale`, `ExistsNow` (another file at the path; a null baseline over a file), `Gone` (including an empty note deleted beside another empty note — never re-seated), `Recovered` | ignore the file identity; re-seat by hash; restore over a file |
| a11y: the seven variants render, at their priorities, in the corpus and the fixture | the copy | — |

uniffi: `hot_exit_store_round_trips_through_the_binding` (open, claim,
write, read, release, discard through the exported object). Hosts: both
corpus mirrors gain the seven events (the Rust tripwires fail until they
do); no host behaviour changes. WB-29 and PR 29 add the host facts:
restore → edit → reopen gets the new text; restore → save → reopen gets
nothing; a restored entry with no layout tab keeps its id; a layout tab
whose path differs from its entry's takes the entry's path; two dirty
peers, one discards, reopen restores the other; a Discard All whose
`discard_vault` does not commit keeps the vault open.

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
- **Codex round 1 (2026-11-01), taken by rebuilding the store protocol:**
  per-entry claims with random incarnations replace host-seeded
  generations and per-buffer floors (a newer claim, a release or a vault
  discard invalidates every older writer, in any thread or process); the
  ack rule counts only a tab's own durable text; payload writes hold no
  lock, so a discard never waits behind one, and a Discard All completes
  only once its epoch is durable (else the vault stays open); no
  retention and no pruning of unread text, and `Invalid` entries restore
  as recovered notes; the key requires a local, non-sentinel identity and
  includes the root's creation time; each note's file identity is kept so
  a replacement at its path is caught; durability is specified per
  platform and every sync failure propagates; the store-wide vault cap is
  checked under a short store lock; a lost epoch fails closed; lock facts
  use a child process. Both Deviations accepted by Codex; three accepted
  risks recorded above.
