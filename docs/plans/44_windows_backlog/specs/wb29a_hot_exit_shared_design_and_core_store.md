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
`claim_entry(key, epoch, entry_id, adopts: Option<incarnation>) -> Result<HotExitClaimOutcome, HotExitError>`.
A restored tab's first claim passes `adopts` = the incarnation of the
entry it was restored from (from the snapshot); every other claim passes
`None`.
Under the vault lock the store checks `epoch` against the vault's
current epoch (`Superseded { by: Epoch }` if older), writes
`claims/<id>` = a fresh random incarnation durably (§2, durability), and
returns `Claimed { claim: HotExitClaim { entry_id, incarnation, epoch, adopted } }`,
`adopted` being the `adopts` it was given.
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
   `Superseded { by: Newer { stored_generation } }`, returned only after
   the store syncs that stored entry and its directory again under the
   lock, a failure being an error: a newer write whose own durability
   step failed is never vouched for); the vault's caps
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

**A host never takes a claim back.** A tab whose write returns
`Superseded { by: Claim }` stops writing and stays not kept until it
next turns clean (its next dirtiness claims afresh); it never re-claims
to win the entry back. So two writers cannot alternate silently, and
the one that lost knows it at its next close.

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
temp files last modified more than an hour ago (a live write's staging
file is younger) and every entry whose claim file is missing or names an
older epoch (a release or a vault discard cut short after its claim or
epoch step), and nothing else; an entry whose claim names a newer
incarnation (a restored tab that claimed and has not written yet) is
kept and returned. A missing `epoch` file in an existing directory
is `Err(HotExitError::Damaged)` (fail closed: no claim, no write and no
restore for that vault until Close Vault's Discard All recreates it; the
entries stay on disk), so a lost epoch can never fall back to 0 and let
an older write in.

The host classifies the entries through one session read,
`VaultSession::classify_hot_exit_entries(entries) -> Vec<HotExitRestore>`,
reading each `path` beneath the held root (no index, no scan):

| The vault now | Decision | What the host does |
|---|---|---|
| a file at `path`, its file identity == `baseline_file_id` (or either is null) and its hash == `baseline_hash` | `Restore { path }` | the tab opens on `path`, buffer = `text`, dirty; its saves carry `baseline_file_id` as well as `baseline_hash` until one lands (below) |
| a file at `path`, same identity (or null), another hash | `RestoreStale { path }` | the same, externally stale; its next save passes `baseline_hash` and `save_text` returns `WriteConflict` (`session.rs:4577`), which blocks the save |
| a file at `path` whose identity differs from a non-null `baseline_file_id`, or `baseline_hash` null | `ExistsNow { path }` | a new unsaved note at `path`, dirty; its first save is create-exclusive, so it refuses rather than overwrite the file now there |
| no file at `path` | `Gone { path }` | a new unsaved note at `path`, dirty; its first save creates it exclusively |
| `path` no longer validates (outside the vault, under `.slate`) | `Recovered { path }` | a new unsaved note at `Recovered note.md` (then `Recovered note 2.md`, …) at the vault root, holding `text`; nothing is dropped unread |

**The identity travels to the first save.** `save_text` gains an
optional `expected_file_identity`, checked beside `expected_content_hash`
in the same step (`session.rs:4536-4583`): a file at `path` whose
identity differs refuses the save with `WriteConflict`. A restored tab
passes its entry's `baseline_file_id` until a save lands, so a file put
at the path after the classification — a backup restore with the same
bytes — is refused too. The check shares the hash check's existing
window between check and publish; it adds no new race.

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
`written_at` order. **Peers whose texts differ** (a crash between two
same-path tabs' publications) are reconciled before mirroring can copy
one over the other: the entry with the latest `written_at` gives its
text to every tab on that path; each other distinct text opens as a new
unsaved note at `{stem} (recovered).md` (numbered when taken), created
exclusively, which adopts that entry's id; a layout tab whose entry so
moved takes a fresh id. No version is lost, and one tab's discard still
never removes another's text. Each restored tab then claims its entry (§2): the new
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
  takes the vault lock and retires everything of its own incarnation and
  of the incarnation it adopted, whatever has happened since: if
  `claims/<id>` holds `claim.incarnation` it deletes the claim (durably);
  if the stored entry's incarnation is `claim.incarnation` or
  `claim.adopted` it deletes the entry (durably). It returns `Released`.
  So releasing a restored tab before its first write retires the text it
  was restored from. Nothing of another incarnation is touched — a
  successor's published payload stays, and claiming without releasing
  leaves the adopted text until the tab's first write replaces it — so a release
  that lands after the tab's next claim still removes the discarded
  text, and a stale release can never delete a newer entry. Every write
  of the released incarnation that lands later finds no claim and is
  `Superseded`. Routes: a clean transition (a save landed,
  undo to the baseline, a reload) and a dirty tab's own Discard (closing
  it, closing its pane, replacing its document). The host awaits a
  release at most 5 s; one not confirmed by then stays pending and is
  part of the next close's flush (§5), so a release that never landed
  makes that close fall back to the prompt rather than exit. A crash
  while a release is unconfirmed can restore the discarded text once
  (an accepted risk: a crash and an unconfirmed release, §Deviations).
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
5. **Accepted risk: two Slate processes on one vault, editing the same
   restored tab id.** The later claim wins. A process that closed first,
   told its text was kept, loses it to the other process's later claim
   and write. It needs two processes on one vault with the same layout:
   Windows runs one instance (its single-instance mutex) and the Mac one
   app instance.
6. **Accepted risk: a crash while a tab's release is unconfirmed.** The
   discarded text can restore once; the reader discards it again. It
   needs a release that did not land within 5 s and a crash before the
   next close's flush.
7. **Accepted risk: an identity check sharing the hash check's window.**
   A replacement in the instant between `save_text`'s check and its
   publish passes both, exactly as a byte change does today.
8. **Accepted risk: a store that fills with entries for vaults never
   reopened.** They are never deleted unread; the store then refuses new
   writes and closing prompts. A later host command can offer to review
   or discard them.

## Delivery record, the core store PR (2026-11-02)

Where it differs from the design above, it wins.

- **The vault directory name is a blake3 hash of the key text**, not
  SHA-256: core already depends on blake3 and on no SHA-2 crate, and the
  name only has to be stable and collision-free.
- **One lock, not two.** Every call opens its own handle on the vault's
  `lock` file and takes an exclusive `File::lock` on it. The lock belongs
  to the handle (`LockFileEx`; `flock` on an open file description), so it
  serialises two threads of one process exactly as it serialises two
  processes, and the in-process mutex the design names is not needed.
  The store-wide vault cap uses an in-process mutex and a `store.lock`
  file the same way. A vault directory is built under a temp name and
  renamed into place, so it never exists without its epoch.
- **A vault discard leaves temp files alone.** A temp file may be a live
  write's staging file: that write meets the new epoch under the lock and
  removes it; a dead writer's is cleaned by a read once an hour old.
- **The identity check is a sibling export,** `save_text_checked`, rather
  than a new parameter on `save_text`: every existing call on both hosts
  keeps its signature, and `save_text` delegates with no identity. The
  check runs inside `save_text_locked`'s transaction, beside the hash.
- **A note's file identity** is the provider's new `file_identity`: on
  Windows the same capture the conditional rename uses; on Unix the
  entry's device, inode and birth time.
- **A lost epoch is re-anchored by Close Vault's Discard All** above any
  epoch it can have held (the highest a claim names, or the clock's
  milliseconds, which exceed every counted epoch).
- **The reason fragments are core constants**
  (`a11y::HOT_EXIT_REASON_*`), carried by `HotExitError` and
  `HotExitKeyResult` across the FFI, with `hot_exit_refusal_reason` and
  `hot_exit_reason_timed_out` for the host's status line.
- **The seven events are designated, not constructed,** in
  `a11y_trigger_designations.json` for both hosts, and contract 38's
  ledger lists them (`scripts/a11y_trigger_ledger.py --write`): no host
  constructs them until WB-29 and plan 43 PR 29.
- **The macOS key reads** (`fgetattrlist`, `getattrlist` at the mount
  point, `fstatfs`) were written without a Mac to run them; the Mac lane's
  build and the macOS-only facts are their first run. Both ask for
  `ATTR_CMN_RETURNED_ATTRS` with `FSOPT_PACK_INVAL_ATTRS` and give no key
  unless the volume returned the creation time, the file id and the UUID,
  so a file system lacking one gives no key rather than a shifted one.
- **Discard All fences a vault that has no directory yet:** it creates the
  vault (under the store lock and the cap) and advances its epoch, so a
  first claim issued at epoch 0 before the discard meets the new epoch.
- **Windows syncs directories too:** `FlushFileBuffers` on the directory
  opened for writing (`FILE_FLAG_BACKUP_SEMANTICS`), so a release's and a
  discard's deletes are durable when the call returns, not only the
  write-through rename. The rename takes the verbatim (`\\?\`) form, so a
  store under a deep profile is not held to `MAX_PATH`.
- **Nothing unread is deleted on a guess:** a claim that exists but does not
  parse, or a missing claims directory, makes the read return `Damaged`
  and leaves every entry in place; Discard All still clears such a vault
  (an unreadable claim names no epoch when the epoch is re-anchored).
- **Dead temp files are cleaned:** claim and epoch temps under the vault
  lock (their publishers hold it, so any found is dead) and staging vault
  directories under the store lock; entry temps keep the hour, being staged
  outside the lock.
- **A note's identity carries no device:** `unix:{inode}-{birth}` (macOS
  renumbers `st_dev` on a remount; the key names the volume), and a Windows
  note that is itself a reparse point has none (`None`, not an error).
- **`save_text_checked` refuses an identity without a hash** rather than
  skipping the check.
- **A recovered name** avoids every kept entry's path, and its search is
  bounded: it stops at an answer that is not "no such file" and falls back
  to `Recovered note {entry id}.md`.
- **The device root is the host's, and must exist:** `open` refuses a
  device root that is relative or missing and creates nothing above
  `hot-exit`, because existence cannot prove a directory durable (another
  opener may have just made it, unsynced) and a parent above the host's
  app-data folder may not even be writable. The host's existing app-data
  folder (the one its settings already live in) is the durable anchor.
- **Durable includes every directory name below that anchor:** opening the
  store syncs `vaults`, `v1`, `hot-exit` and the device root on every open,
  whoever created them, and
  every claim syncs `vaults` before it publishes, so a vault directory a
  concurrent creator renamed into place but had not yet synced (or failed
  to sync) is made durable by the caller relying on it.
- **A claim from a later epoch than the vault's is damage, not age:** the
  read returns `Damaged` and deletes nothing, and Discard All moves past
  the highest epoch any claim names as well as the counter.
- **An entry's files are named by its id in hex,** not by the id: two
  valid ids may differ only by case (`TabA`, `taba`), and NTFS and APFS
  compare names without case, so the id as a name would let one tab's
  write replace another's unread text. The entry's JSON keeps the id itself.
- **No epoch follows `u64::MAX`:** an epoch or claim at the maximum makes
  Discard All return `Damaged` before it changes anything, rather than
  panicking across the FFI or wrapping below the claims it must pass.
- **A release always syncs both directories** before it returns, so a retry
  after a failed sync, finding the records already removed, still makes
  that removal durable before it reports the release done.
- **Recovered names compare without case** (NTFS and APFS defaults), and
  the fallback is unique among kept and chosen names.
- **Out of this PR:** on macOS and Linux the provider's `stat` and `read`
  (and so `classify_hot_exit_entries` and `note_file_identity`) resolve by
  pathname while writes use the held root, as before this PR; filed as
  #1733.
- **Two records fold into others:** `release_entry` returns `()`, since
  `HotExitReleaseOutcome` had the one variant `Released`; and
  `HotExitUnavailable` is `HotExitKeyResult::Unavailable { reason }`. Claim
  files are not capped apart from entries: one exists per open tab, and a
  read removes every claim of an older epoch.
- **Accepted, not built:** two byte-cloned volumes (a `dd` copy, a cloned
  VHD) share serial, id and creation time and so one key; and remote
  detection on Windows is `FileRemoteProtocolInfo` alone, so a redirector
  that refuses that call but reports a persistent `FileIdInfo` would get a
  key. Neither can be shown failing on the development machine.

## Amendment, WB-29 PR 1 (2026-11-02)

The owner's calls of 2026-11-02 (WB-29's spec,
`wb29_hot_exit_windows_half.md`, "Owner calls" and §7) amend this design
for both hosts. Where this section and the text above differ, this
section wins.

- **§1, a vault hold.** `HotExitStore::hold_vault(key)` takes an
  exclusive, non-blocking OS file lock on
  `<device root>/hot-exit/v1/holds/<key hash>.lock`, released by
  `release`, by drop, or by the OS at process exit; another holder is the
  new `HotExitError::HeldElsewhere`. A host reads, adopts in, writes to
  and discards a vault's store only while it owns the hold. The store's
  other calls do not check it: holding it is the host's contract, as the
  device root's durability is.
- **§2, a claim names the claim it replaces.** "A newer claim replaces an
  older one" becomes "a claim naming the current one replaces it":
  `claim_entry(key, epoch, entry_id, expected, incarnation)` applies only
  when the entry's current claim at `epoch` is `expected` (`None`: no
  current claim; an older epoch's claim counts as none, a later epoch's
  is `Damaged`), else `Superseded { Claim }` with nothing changed. The
  host makes `incarnation` first (`new_hot_exit_incarnation()`), so the
  same call retried after an error answers `Claimed` again, once the
  claim is re-synced. A claim with no current claim first removes any
  payload of that entry, durably, since only an older epoch's or a
  released claim can have left one. `HotExitClaim.adopted` is a list:
  `expected` and the stored payload's incarnation when an older claim
  wrote it.
- **§3, the snapshot and the identity.** `HotExitSnapshot.claims` lists
  every claim at the read's epoch, with or without a payload: an entry
  may sit under a claim of another incarnation (an adopter that never
  wrote), and a host adopts by expecting the observed claim. A note's
  identity comes from the read or the save that touched it:
  `read_text_identified`, `NotePartsBundle.file_identity`,
  `SaveReport.file_identity` and `PublishedUnindexed.file_identity`, each
  from one handle (the staged file's for a save, since the rename keeps
  it), and kept only when it agrees with `note_file_identity` for the
  entry at the path, which a checked save compares against: on Windows
  that gives none for a link or any other reparse point (OneDrive
  placeholders, compressed or deduplicated files), on Unix a link's own
  inode. So a baseline never carries an identity the check cannot
  produce, a link's read (the target's bytes) reports none, and a file
  put at the path between the handle and that check reports none.
  `None` where none can be had, never a failed read or save.
- **Refusals change nothing.** A claim with a stale epoch or an expected
  claim, on a vault that has no directory, is refused before the
  directory is created, so it spends no vault cap. Hold files (`holds/<key hash>.lock`, empty) are not removed:
  one per vault this device has held, an accepted, bounded leftover.
- **§5, the discard-pending prompt (owner call D-1).** A close the store
  cannot finish — a discard or a retirement not confirmed — opens a
  prompt whose message is `UnsavedNotesNotDiscarded` (now: closing anyway
  keeps any copies not yet discarded, and they come back unsaved at the
  next open) with Try again, Close and keep the copies, and Cancel
  (`hot_exit_discard_pending_choices()`). A Discard All that commits late
  never re-keeps the buffers it covered; the next close asks.
- **§6, `retire_entry`.** `retire_entry(key, entry_id, incarnations)`
  removes the entry's claim and payload where their incarnation is
  listed, needs no claim and checks no epoch; `release_entry(claim)` is
  `retire_entry` of the claim's incarnation and every one it adopted.
- **Deviation 5 is withdrawn.** Windows runs one instance per Windows
  session (`SingleInstanceCoordinator.cs:30-39`), not per account, so two
  sign-ins of one account share a store; the vault hold, not the
  single-instance mutex, keeps two processes apart, and a later claim
  never wins over a live one.
- **Copy.** New fragments `HOT_EXIT_REASON_TAKEN_OVER`,
  `HOT_EXIT_REASON_DISCARD_PENDING`, `HOT_EXIT_REASON_DISCARDED`,
  `HOT_EXIT_REASON_HELD_ELSEWHERE`, `HOT_EXIT_REASON_EARLIER_WORK_PENDING`
  with FFI accessors (and `hot_exit_reason_store_unwritable()`);
  `HOT_EXIT_REASON_TIMED_OUT` says 4 seconds, the host's settle budget.

## The core store PR (after this design merges)

New module `crates/slate-core/src/hot_exit.rs` (the store, its claims,
its locks, the `HotExitFs` seam) and its FFI in
`crates/slate-uniffi/src/lib.rs`: the object; the records
`HotExitVaultKey`, `HotExitKeyResult`, `HotExitClaim`,
`HotExitClaimOutcome`, `HotExitEntryWrite`, `HotExitEntry`,
`HotExitSnapshot`, `HotExitWriteOutcome`, `HotExitReleaseOutcome`,
`HotExitSupersession`, `HotExitRefusal`, `HotExitUnavailable`,
`HotExitRestore`; `HotExitError`; the session methods `hot_exit_key`,
`note_file_identity` and `classify_hot_exit_entries`; `save_text`'s
optional `expected_file_identity` (both hosts pass `None` until they adopt
hot exit); the seven
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
| `newer_is_never_vouched_for_without_durability` | hold write N; N+1 renames, its directory sync fails (`Err`); N then completes → `Err`, never `Newer`; with the sync healed, N → `Newer` after the store re-syncs | return `Newer` without the re-sync |
| `a_newer_claim_invalidates_every_older_writer` | claim A, claim B on one id, A's write → `Superseded { Claim }`, B's text on disk | no claim check |
| `two_processes_restoring_one_tab_never_both_count_as_kept` | child process claims and writes; the parent's write after it → `Claim`; and the reverse order | generation-only ordering |
| `a_release_invalidates_its_own_late_writes` | release, then a delayed write of that claim → `Superseded`, no entry on disk | release without deleting the claim |
| `a_release_retires_its_payload_after_the_next_claim` | durable C; next claim C2 starts a held payload write; release(C) lands → C's entry deleted; kill before C2 publishes → `read_buffers` returns nothing | release by the live claim only |
| `a_release_retires_the_text_its_claim_adopted` | restore entry A, claim B adopting A, release(B) before any write → A deleted; claim C, kill before C publishes → nothing restores; the same with release(B) landing after claim C | retire only the claim's own incarnation |
| `claiming_without_releasing_keeps_the_adopted_text` | restore A, claim B adopting A, kill before B writes → A restores | delete the adopted payload at the claim |
| `a_stale_adopters_release_keeps_a_successors_payload` | claim B adopting A, claim C, C publishes, release(B) lands → C's entry stays | delete by entry id |
| `a_stale_release_never_deletes_a_newer_entry` | release of claim A delayed past claim B's write → `Superseded`, B's entry stays; same past a vault discard and a new claim | release by id alone |
| `a_vault_discard_supersedes_every_earlier_claim_write_and_release` | each kind issued before `discard_vault`, landing after → `Epoch`, nothing on disk | skip the epoch check |
| `a_discard_cut_short_after_its_epoch_hides_every_older_entry` | a seam fault between the epoch and the deletes; the next read returns nothing and deletes the files | bump the epoch after the deletes |
| `a_stalled_payload_write_never_blocks_a_discard` | a seam holds a payload write; `discard_vault` completes; the held write then lands as `Superseded` | hold the lock during the payload write |
| `a_lost_epoch_fails_closed` | `epoch` deleted from an emptied vault directory → `Damaged` for claim, write and read; a write from before cannot land | reset to 0 |
| `a_write_or_claim_over_a_cap_is_refused_and_changes_nothing` | 8 MiB, 256 entries, 64 MiB, 32 vaults (the vault cap under racing first claims from two threads and from a child process) | check outside the store lock; write before the check |
| `nothing_unread_is_ever_deleted` | entries untouched for a year (injected clock) and entries whose path no longer validates are still returned by `read_buffers` | any retention or pruning |
| `a_cut_short_write_keeps_the_previous_entry` | a fault before the rename; previous entry stays; the next read deletes the temp once it is an hour old (injected clock), never a younger one | write in place; delete young temps |
| `two_vault_keys_never_read_each_others_entries` | R1 for distinct keys, including keys differing only in the creation time | drop part of the key |
| `an_invalid_entry_id_is_refused` | ids outside `[0-9A-Za-z_-]{1,64}`, nothing written | accept any id |
| `the_vault_lock_excludes_another_process` | the child holds the lock inside a publish (barrier); the parent's claim blocks until it is released | no file lock |
| session: `no_hot_exit_key_without_a_lasting_local_identity` | `Unavailable` under the WB-28 seams, a sentinel `FILE_ID_INFO` seam, a remote-volume seam (Windows), the volume seam (macOS), and on Linux | key a weak, sentinel or remote identity |
| session: `the_key_includes_the_roots_creation_time` | a root deleted and recreated with a reused id (seam) gets another key | omit the creation time |
| session: `the_mac_key_survives_a_remount_and_two_volumes_never_share_one` (macOS) | volume UUID, inode and birth time, never `st_dev` | key by `st_dev` |
| session: `the_first_save_refuses_a_file_put_at_the_path_after_classification` | classify `Restore`; replace the file with another holding the baseline bytes; `save_text` with the entry's identity → `WriteConflict`, the other file untouched | drop the identity from `save_text` |
| session: `classify_decides_each_row` | `Restore`, `RestoreStale`, `ExistsNow` (another file at the path; a null baseline over a file), `Gone` (including an empty note deleted beside another empty note — never re-seated), `Recovered` | ignore the file identity; re-seat by hash; restore over a file |
| a11y: the seven variants render, at their priorities, in the corpus and the fixture | the copy | — |

uniffi: `hot_exit_store_round_trips_through_the_binding` (open, claim,
write, read, release, discard through the exported object). Hosts: both
corpus mirrors gain the seven events (the Rust tripwires fail until they
do); no host behaviour changes. WB-29 and PR 29 add the host facts:
restore → edit → reopen gets the new text; restore → save → reopen gets
nothing; a restored entry with no layout tab keeps its id; a layout tab
whose path differs from its entry's takes the entry's path; two dirty
peers, one discards, reopen restores the other; two peers killed
between their publications restore the newest text to both tabs and the
older text as a recovered note, and an edit then mirrors only the
newest; a Discard All whose
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
- **Codex round 2 (2026-11-01), taken:** `Newer` is vouched for only
  after the stored entry is re-synced under the lock; a release retires
  its own incarnation's payload even after the next claim; the restored
  tab's saves carry the entry's file identity into `save_text`; divergent
  same-path entries reconcile at restore (the newest to the path's tabs,
  each other version as a recovered note); temp cleanup only past an
  hour. Accepted risks added: a crash during an unconfirmed release; the
  identity check sharing the hash check's window. Deviations 1 and 2
  accepted again.
- **Codex round 3 (2026-11-01), taken:** a restored tab's claim names
  the incarnation it adopts, and its release retires that payload as
  well as its own, never a successor's. No other blocker; Deviations 1
  and 2 accepted again.
- **Codex approved at round 4** (head `70c1752d`): no remaining blocker under D1–D9; both Deviations acceptable. The core store PR implements this design; its facts and mutation results are that PR's to show.
