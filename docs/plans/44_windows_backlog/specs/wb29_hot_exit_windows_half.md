# WB-29 — Unsaved notes survive closing, quitting and a crash on Windows, and come back unsaved when the vault reopens

Issues: #1525 (Windows half; closes in whichever of WB-29 and plan 43
PR 29's Mac half merges last — both hosts merged and field-passed, WD20;
earlier PRs write `Refs #1525`). Size L, two PRs: PR 1 (core: two
reason fragments and three accessors, §7; lanes Rust and Windows) and
PR 2 (the Windows host; lane windows). AT: NVDA full pass on PR 2.
Wave: W11, slot 2.

**Rebased on WB-29a (2026-11-02).** The shared design is
`wb29a_hot_exit_shared_design_and_core_store.md` (design #1732, core store
#1734, `114f737d`); its sections are cited here as WB-29a §N and its
"Delivery record, the core store PR" wins where it differs from its own
design text. This spec is the Windows host half of that design. It
replaces the spec of 2026-10-21, whose design was written before the
store existed: per-path keys, `discard_buffer`, a path-wide `InFlight`,
re-seating a moved note and a Discard All that proceeds unconfirmed are
all gone, superseded by WB-29a's claims, releases, epoch and restore
table. The owner's call of 2026-10-26 (WD20: core owns the store; a weak
root gets no hot exit) stands.

## Goal

Closing the main window or quitting Slate on Windows with unsaved edits
never asks and never writes the notes. Each dirty tab claims its own
entry in core's hot-exit store (outside the vault, keyed by the vault's
lasting identity) and writes its buffer there as you type, so a crash
or a kill keeps it too. Application close and a vault switch wait at
most 4 s for every dirty tab to be kept and every store call the vault
owes to resolve, and fall back to today's prompt, saying why, when that
cannot land, rather than discard the only copy. When the vault reopens,
each kept buffer comes back in its own tab, dirty, classified against
the disk by core (WB-29a §3), injected into the tabs the layout restore
constructs, and the launch landing happens once. Close Vault still asks;
its Discard All, and the fallback prompt's, completes only once the
store's discard is durable, and the vault stays open while it is not
(WB-29a §5; owner call D-1 asks whether a store that never answers
should offer "close and keep the copies"). Core owns every sentence. A vault with no
key (a weak, sentinel or remote root; WB-29a §7) keeps today's prompt
with core's reason.

## Design items closed by the rebase

The spec of 2026-10-21 carried two open items from Codex round 12. Both
close on WB-29a's design:

1. **[high] A weak root's admission check restored into a replacement.**
   Closed by policy (a), WB-29a §7: a weak (`persistent: false`),
   sentinel or remote root has no `HotExitVaultKey`
   (`hot_exit_key()` is `Unavailable { reason }`), so the host never
   claims, writes or reads for it, and nothing can be restored into a
   replacement. Core's facts prove it (`no_hot_exit_key_without_a_lasting_local_identity`,
   `the_key_includes_the_roots_creation_time`); this chunk's fact
   `AVaultWithNoKeyKeepsThePromptAndKeepsNothing` proves the host side.
   Current state's old sentence ("within a session the weak identity is
   sound … which is why the stores compare it there") is gone: since
   WB-28 the stores reach the vault through the session's held root and
   compare no identity.
2. **[medium] One vault's stalled write blocked another vault's
   recovery.** Closed by the store's shape: every call takes the vault's
   key, the store locks per vault (WB-29a §8), and the host keeps
   nothing process-wide that one vault's work could hold. In-flight
   bookkeeping is per `(vault key, entry id)`; there is no path-wide
   `InFlight` and no process-wide `DiscardsSettled`. A stalled payload
   write holds no lock at all (WB-29a §2). Fact:
   `AStalledWriteInOneVaultNeverHoldsAnother`.

## Current state on main (114f737d)

Paths under `apps/slate-windows/src/SlateWindows/` unless stated.

- **The core store and its session reads are on main, unused.** The FFI
  (`crates/slate-uniffi/src/lib.rs`): the `HotExitStore` object
  (`:9099-9173`: `Open(deviceRoot)`, `CurrentEpoch`, `ClaimEntry`,
  `WriteBuffer`, `ReleaseEntry`, `DiscardVault`, `ReadBuffers`), its
  records and enums (`:8846-9093`), `hot_exit_refusal_reason` and
  `hot_exit_reason_timed_out` (`:9177`, `:9185`), and on `VaultSession`
  `SaveTextChecked` (`:1382-1396`), `HotExitKey` (`:1400-1411`),
  `NoteFileIdentity` (`:1415-1417`) and `ClassifyHotExitEntries`
  (`:1420-1428`). The seven events (`UnsavedNotesRestored`,
  `RestoredNoteChangedOnDisk`, `RestoredNoteMissingOnDisk`,
  `RestoredNoteExistsOnDisk`, `RestoredNoteRecovered`,
  `UnsavedNotesNotKept`, `UnsavedNotesNotDiscarded`) are in core's
  corpus and designated, not constructed, for Windows in
  `a11y_trigger_designations.json`. Nothing under `apps/slate-windows`
  references any of it.
- **`HotExitStore.Open` requires an existing absolute device root**
  (WB-29a's delivery record): the host's app-data folder, whose own
  durability is the host's. **`SlateDeviceState.Root`
  (`SlateDeviceState.cs:24`) is a path only; nothing creates it**: it
  exists in practice only because `HostLog.RedirectNativeStderrToAppLog`
  creates `Root\logs` (`HostLog.cs:133`, called from `App.xaml.cs:46`),
  which is skipped under `SLATE_LOG_DIR` and swallows its errors.
- **Closing asks today.** `MainWindow.xaml.cs` `Window_Closing`
  (`:2153-2162`) calls `PrepareForApplicationClose()` and cancels on
  false; `Window_Closed` (`:2164-2190`) ends with `_viewModel.Dispose()`
  (`:2189`). `VaultLifecycleViewModel.cs`: `PrepareForApplicationClose`
  `:811-825`; `CloseVault` `:772-809` (`TryCloseWorkspace` `:779`,
  `CloseSession` `:795`); `OpenVaultAsync` `:524-770` (the switch's
  `TryCloseWorkspace` `:543`, `CloseSession` `:562`); `TeardownRoute`
  `:44-55` (`Close`, `Switch`, `ApplicationClose`); `WorkspaceTeardown`
  `:26-40` (`Refused`, `Closed`, `ClosedAllSaved`,
  `ClosedChangesDiscarded`); `TryCloseWorkspace()` `:1766-1793` (no
  route parameter); `AnnounceTeardown` `:1847-1865` (speaks for `Close`
  only); `TryCloseWorkspaceCore()` `:1885-1994`, its round loop
  `:1925-1988` (settle `:1929-1936`; nothing dirty → `Closed` `:1949-1952`;
  `DirtyTabsForPrompt()` `:1955-1956`; `_confirmUnsavedClose()` `:1957`;
  Save All `:1963-1976`; the Discard check `!SavesIdle ||
  !DirtyTabsStill(asked)` `:1981-1986`; `ClosedChangesDiscarded` `:1987`);
  `CloseSession()` `:1224-1426` (`workspace.Dispose()` `:1402`,
  `_session?.Dispose()` `:1423`); `ReportTerminalStatus` `:2303-2308`.
- **The workspace persists its layout at disposal, with the live tab
  ids** (`WorkspaceViewModel.cs:3476`, before the tabs are disposed at
  `:3477-3479`), so the next open's layout names the tabs whose entries
  the store holds.
- **Tabs have stable ids.** `WorkspaceTabViewModel.Id` (`Guid`,
  `WorkspaceViewModel.cs:266`) is restored from `WorkspaceTabState.Id`
  (`:228`; `WorkspacePersistence.cs:47-52`, written `:535`, read
  `:352-361`, a duplicate id rejecting the snapshot `:286-288`). New tabs
  take `Guid.NewGuid()` (`Layout.cs:363`, `:824`); `ReplaceTabItem`
  (`Layout.cs:319-337`) keeps the id; `ReopenClosedTab` (`Layout.cs:851`)
  makes a new one. A Guid's `"D"` form (36 characters, hex and hyphens)
  is a valid store entry id (`hot_exit.rs:1024-1035`, at most 64).
- **Several tabs can hold one dirty file.** A new tab on a file another
  tab has open, and a duplicated tab, copy that tab's dirty buffer
  (`Layout.cs:375-378`, `:333-336`, `:837`); peers mirror each other's
  edits.
- **Dirty state.** `IsDirty` (`WorkspaceViewModel.cs:532-549`, private
  set) is also written directly, around the setter, by `Load` (`:2004`),
  `ReplaceItem` (`:669`), `MirrorDocumentStateFrom` (`:799`) and
  `ApplyPeerDocumentEvent` (`:850`). Every one of them, and the setter,
  bumps `ContentGeneration` (`:289`; `BumpContentGeneration` `:291`,
  called at `:539`, `:575`, `:631`, `:792`, `:837`, `:1711`, `:1913`,
  `:1932`, `:2018`), so a generation bump is the one signal that sees
  every dirtiness and text change.
- **The baseline.** `SavedContentHash` (`:279`, field `_contentHash`
  `:158`): set at load from the decoded text (`:2002-2003`), cleared by
  `ReplaceItem` (`:647`), set to a landed save's `NewContentHash`
  (`:1312`) and by the task splice (`:1681`). `IsExternallyStale`
  (`:1705-1713`, re-derived `:1722-1740`).
- **The save path.** `SaveAsync` (`:901-939`) enqueues on the
  coordinator; `StartSave` (`:1046-1143`) builds the `SaveRequest` with
  `ExpectedContentHash: _contentHash` (`:1097-1106`) and runs
  `WriteForSave` on a tracked worker (`:1110`, `TrackWorker` `:1119`);
  `WriteForSave` (`:1160-1191`) calls `CreateExclusiveReporting` for a
  null hash (`:1176`) and `SaveText(path, text, hash)` otherwise
  (`:1188`). `PublishSave` `:1204-1287`; `AdoptLandedWrite` `:1307-1333`.
  Seams: `SaveWriteHookForTests` (`:1017`, invoked `:1162`),
  `SaveWrittenHookForTests` (`:1022`).
- **A dirty tab's own Discard.** `AdmitDirtyTab` (`Layout.cs:702-757`,
  Discard accepted `:737-741`); a dirty navigation re-points the tab at
  `:286`; `CloseTab` (`:460-511`) disposes at `:493`; `CloseActivePane`
  (`:546-668`) approves Discard itself (`:607-617`) and disposes the
  pane's tabs at `:651-654`.
- **Save workers are joined; nothing else is.** `WorkspaceSaveCoordinator`:
  `TrackWorker` `:258-275`, `CloseAndJoinWorkers` `:297-318` (unbounded
  `worker.Wait()`, `BeforeJoinForTests` `:305`), `SettlePumping`
  `:280-291`, `PumpedWait.Until` `:405-422`. A binding call keeps its
  object alive until it returns (`CallWithPointer` in the generated
  binding), which is why a recovery call must not borrow the session
  (WB-29a §8: the store's calls take a key, never a session).
- **The open sequence.** `OpenVaultAsync`: the session `:591`, WB-28's
  root-identity refusal `:604-616`; the open scan inside
  `_runSessionLoad` (`:657-673`, `ScanInitialWithProgress` `:668`);
  `InitializeWorkspace` (`:1522-1597`, called `:692`) constructs the
  `WorkspaceViewModel` (`:1530-1545`); `PublishWorkspace` (`:1599-1632`)
  raises `WorkspaceReady` (`:1631`), on which `MainWindow` lands focus
  once (`ViewModel_WorkspaceReady` `:486-495`). The constructor
  (`WorkspaceViewModel.cs:2268-2281`) calls `Restore(_persistence.Load())`
  (`:2493`) and `SyncPanels()` (`:2494`); `RestoreNode`
  (`WorkspaceViewModel.Persistence.cs:53-107`) builds a new tab per
  `WorkspaceTabState` (`:70-84`, each loading its note synchronously in
  its constructor, `WorkspaceViewModel.cs:234`), attaches documents
  (`:85`) and restores the active tab (`:91`).
- **The process-lifetime store precedent.** `SidebarWriteJournal.Shared`
  (`:297`), installed by `InstallProductionLocation()` (`:314-321`,
  called `App.xaml.cs:138`), waits at most `DurableWaitBudget` (5 s,
  `:269`) without pumping and logs on a miss
  (`FilesSidebarViewModel.WriteJournal.cs:99-102`); `App.OnExit`
  (`App.xaml.cs:151-160`) waits for it at most 2 s. `HostDiagnosticEvent`
  (`HostLog.cs:17-95`) has no hot-exit member; `HostLog.ObserverForTests`
  `:221`.
- **Announcements.** The lifecycle's `_announce` is `_announcer.Post`
  (`MainWindow.xaml.cs:111`, `:125`), which renders a core `A11yEvent`
  through `SlateUniffiMethods.A11yRender`
  (`AccessibilityNotificationDispatcher.Post`, `:236-240`). Tests inject
  `announce:` (`PumpedSaveReentrancyTests.Host`, `:1090-1175`).
- **The field-pass launcher** (`scripts/windows/launch-for-field-pass.ps1`)
  refuses a reused `-Root` holding anything outside its `$deviceStores`
  list (`:87-89`), which does not name `hot-exit` yet.

## Reference

- WB-29a's spec (the store, its key, claims, the restore table, the open
  sequence, the copy, the interlocks, releases and the epoch, the weak
  root, and the calls that borrow no session; its delivery record).
- #1525's text (RD1): explicit save stands (#877), Close Vault still
  asks, storage keyed by vault identity outside the vault, written as
  you type, compared with disk on reopen, core-owned copy.
- Mac twin: plan 43 PR 29's host half (the same store and FFI).
- Contract 38 D-10 (`AnnounceTeardown`): only the explicit Close speaks
  its close line; its pumped-wait invariant (a caller re-reads what it
  acts on after any pumped frame).

## Design

Windows only, consuming WB-29a's store and session reads. Every rule is
WB-29a's, made concrete for this host; where this host adds a choice,
the text says so. Review rounds 1 and 2 (a Claude red team and Codex,
each round) showed that a host built from per-event handlers loses or
resurrects text in the gaps between them, so the host rests on one model
(§0) and every later section applies it.

### 0. The model: ownership, obligations and what "kept" means

**Episodes.** A tab owns its recovery entry for one *dirty episode*: from
the moment its buffer differs from its baseline until the reader ends it
— it turns clean (a save landed, undo to the baseline, a reload, a
peer's save mirrored), or the reader discards, re-points or closes it.
An episode has an `OwnershipToken` (new: the tab instance, its
`ItemIdentity`, and a per-tab episode counter). An episode ends in one of
two ways, and they are not the same:

- **Ended by the reader** (the list above): its entry must be retired —
  its live claim released, and, for a restored tab, the payload it
  inherited retired too.
- **Abandoned** (the workspace disposed without the reader ending it —
  the crash shape, a teardown that already settled, an open that failed
  after install): nothing is released; whatever the store holds stays,
  exactly as a crash leaves it.

**Obligations.** Every store call the host issues is an *obligation*,
recorded by the store host under the vault's key before the call is
issued and resolved when it returns: a claim, a write, a release, a
vault discard, and a *retirement* (below). Obligations live in the store
host (process lifetime), so they outlive tabs and workspaces.

**Kept.** A dirty episode is kept when the store's entry for it is, under
the episode's own live claim, the episode's latest *snapshot*: the tuple
(generation, path, baseline hash, baseline file identity). A change to
any part of the tuple — an edit, `RetargetPath` (a rename,
`WorkspaceViewModel.cs:736-749`), `InvalidatePath`, a landed save that
changes the baseline behind further edits (`AdoptLandedWrite`, `:1312`)
— makes the episode not kept until a write of the new snapshot returns.

**The rules.**

- **O1, nothing unaccounted.** No call is issued without its obligation;
  no obligation is dropped unresolved.
- **O2, one entry, one queue.** For each `(vault key, entry id)` the store
  host runs claims, releases and retirements strictly in order: a call
  for an entry id is issued only after every earlier one for that id has
  resolved (writes are ordered by the claim they carry and need no
  queue). So a late claim can never land after a newer episode's claim
  and displace it.
- **O3, kept is per live claim and snapshot.** An outcome counts only
  when its claim is the episode's live claim and its token is current; a
  late result of an older claim never marks a newer episode kept or lost.
- **O4, reader-ended episodes are retired; abandoned ones are not.** A
  claim that resolves `Claimed` for an episode the reader has since ended
  is released (behind O2's queue). A claim that resolves for an abandoned
  episode is left as it is. A restored tab's inherited payload is retired
  when the reader ends its first episode, whether or not its adopting
  claim succeeded (§5).
- **O5, every successful close is settled.** Every route returns a
  successful outcome only after a settle has found every obligation of the
  vault's key resolved and every dirty episode kept (§4).
- **O6, a discard fences until no discard is unresolved.** While any
  vault discard for the key is unresolved, the writer issues no claim or
  write for it, and no settle reports kept. A discard that resolves with
  anything but a confirmed commit is followed by a read of the vault's
  current epoch: if it advanced (the discard committed its epoch and then
  failed deleting), every claim of the old epoch is invalid, so every
  dirty episode re-claims at the new epoch and re-writes before it can be
  kept.
- **O7, state is read after the mutation.** The writer never decides
  inside a mutation: it re-evaluates a tab on a coalesced dispatcher post
  after the mutation that changed it has returned, and a settle drains
  those posts first.

### 1. The store host: one per process, joined by nothing

A new `HotExitStoreHost` (`HotExitStoreHost.cs`, `internal sealed`) owns
the one `HotExitStore`, the obligation ledger (per vault key: every
obligation with its token, kind and state, and O2's per-entry-id queues)
and every store call in flight.

**Installation.** `HotExitStoreHost.Shared` starts as a host **with no
store**, so a shell built without installation — the `MainWindow` tests
that assert today's prompt (`CommandPaletteTests.ModalLoop.cs:110-151`,
`CommandPaletteTests.Invoking.cs:567`, and the like) — keeps today's
prompt and posts nothing (§6). `InstallProductionLocation()`, called
beside the journal's (`App.xaml.cs:138`), creates `SlateDeviceState.Root`
if it is missing and then, **on every install, whoever created it** (the
log redirect at `App.xaml.cs:46` usually has), makes its name durable in
its parent (`FlushFileBuffers` on the parent opened with
`FILE_FLAG_BACKUP_SEMANTICS` and write access, P/Invoke, new); then opens
`HotExitStore.Open(root)`. WB-29a's delivery record makes the app-data
folder's durability the host's; above its parent (the profile's
`LocalAppData`, or a `SLATE_LOCAL_APP_DATA` override's own parent) is the
operator's. Any failure installs a host with no store and core's reason
(`Open`'s `HotExitError.reason`, or core's store-unwritable sentence
through the new `hot_exit_reason_store_unwritable()`, §7), logged once
(`HotExitStoreUnavailable`, new `HostDiagnosticEvent`).

**Calls.** Each call registers its obligation, waits its turn in O2's
queue where it has one, then runs the store method inside a `Task.Run`
body that converts the result to an outcome on the pool thread (no
`await` that could capture the dispatcher's context), so a dispatcher
blocked on a result can never deadlock it. The task never faults: a
store error becomes an outcome carrying core's reason. `BeforeCallForTests`
and `AfterCallForTests` (`Action<HotExitStoreCall>?`: the kind, the
key's text, the entry id, the incarnation and the generation) run on the
call's own task before and after the store.

**Nothing joins it.** A store call is never handed to `TrackWorker`;
`CloseAndJoinWorkers` joins save workers only, as on `main`; disposal
waits on nothing; a settle waits only within its budget. A call cut
short by process exit is a crash (WB-29a §2). The calls take the vault's
key, never the session (WB-29a §8).

### 2. Keys, the snapshot and entry ids

At open, on the load worker, after the open scan and before the
`WorkspaceViewModel` is constructed, the lifecycle asks
`session.HotExitKey()`. With a key, it waits up to 4 s for that key's
unresolved obligations from an earlier session of this process (a
reopen's read must not race the last session's releases); if any is
still unresolved it reads anyway, and the new session's writer starts
fenced (O6) until they resolve. Then it calls `Read(key)` and
`session.ClassifyHotExitEntries(entries)` and hands the constructor a
`HotExitOpen` (the key, the snapshot's epoch, the classified entries).
A read that returns `Damaged` hands the key, core's reason and no
entries: the writer is inert (§6), and Close Vault's Discard All still
calls `discard_vault` with that key, the repair WB-29a's delivery record
names. Any other read error hands no key and core's reason: the writer
is inert and no Discard All touches the store, so entries nobody has
read are never deleted on a transient error. `Unavailable { reason }`,
or a host with no store, hands `HotExitOpen.None(reason)`.

**Entry ids.** A tab gains `RecoveryEntryId` (string), the id every claim,
write, release and retirement of that tab uses, exactly as stored:

- a tab made in this process: `Id.ToString("D")`;
- a layout tab whose `Id.ToString("D")` equals an entry's id (ordinal):
  that id;
- an entry no layout tab matches whose id is a canonical lower-case
  `"D"` Guid no layout tab holds: a new tab whose `Id` is that Guid, so
  `RecoveryEntryId == Id` and the next layout names it;
- any other entry id (one this host never writes): a new tab with a
  fresh `Id` and `RecoveryEntryId` = the entry's id, kept for the tab's
  life (§Deviations 4).

### 3. Writing as you type

A new `HotExitWriter` (`WorkspaceViewModel.HotExit.cs`, new partial)
holds per tab: `RecoveryEntryId`, the current token, the live claim, the
next generation, the last snapshot issued and the last one kept under
the live claim, whether that claim was lost, and, for a restored tab, the
inherited incarnation still to retire.

- **Observation (O7).** Every `ContentGeneration` bump (the nine sites),
  every direct write of `_isDirty` (the four in Current state),
  `RetargetPath`, `InvalidatePath` and `AdoptLandedWrite` schedule one
  coalesced re-evaluation of the tab, posted to the dispatcher. It reads
  the finished state and compares the tab's snapshot with the last one
  issued.
- **The file identity is part of every baseline.** Every tab reads
  `session.NoteFileIdentity(path)` when it loads a note and when a save
  lands (WB-29a §8), and keeps it as `FileIdentity` beside
  `SavedContentHash`; every entry carries it as `baseline_file_id`, and
  every path that copies a buffer's baseline from one tab to another
  (`AddTab` `:375-378`, `ReplaceTabItem` `:333-336`, `DuplicateActiveTab`
  `:837`, `MirrorDocumentStateFrom`) copies it too.
- **An episode starts with a claim.** The re-evaluation that finds a
  clean tab dirty starts an episode and queues `Claim(key, epoch,
  RecoveryEntryId, adopts)` (O2). `Claimed` for the current token becomes
  the live claim and the snapshot is written at once. `Superseded { Epoch
  }` is handled once, by O6's single path: the writer reads the current
  epoch and re-claims; a second supersession marks the episode not kept.
  `Refused` marks it not kept with core's reason.
- **While dirty, a changed snapshot is written 1 s after the last change**
  (a dispatcher timer per tab), at most one write in flight per tab; a
  tick during one marks the tab due. Each write carries the live claim
  and the snapshot taken on the dispatcher.
- **Outcomes (O3).** `Durable { g }` marks that snapshot kept.
  `Newer { stored }` marks kept when `stored` is at least the latest
  issued generation and the snapshot is unchanged. `Superseded { Claim }`
  marks the live claim lost: the episode stops writing, is not kept, and
  never re-claims (WB-29a: a host never takes a claim back); core's reason
  is `HOT_EXIT_REASON_TAKEN_OVER` (§7). `Superseded { Epoch }` goes to
  O6's path. `Refused` and errors mark not kept with core's reason; the
  next change tries again.
- **The reader ends an episode → retire.** The live claim is released
  (O2's queue; a claim still in flight is released when it resolves, O4).
  Each release is an obligation; an errored one is re-queued by the next
  settle.
- **A save and a recovery write never wait on each other.** This host
  does not route writes through WB-2's `Admit` (§Deviations 1). A save
  that publishes with the tab still dirty changes its snapshot's baseline
  (O7 observes `AdoptLandedWrite`), so the next write carries the new
  baseline; one that publishes clean ends the episode. A crash between a
  save landing and that next write restores the buffer against its old
  baseline, which WB-29a §3 classifies `RestoreStale`, and the restored
  tab's first save is checked against that old baseline (§5): it stops
  at the conflict and overwrites nothing.

### 4. Closing, switching and discarding

- **The teardown knows its route.** `TryCloseWorkspace` and
  `TryCloseWorkspaceCore` take a `TeardownRoute` (`:543` `Switch`, `:779`
  `Close`, `PrepareForApplicationClose` `ApplicationClose`).
- **Settle (O5).** `HotExitWriter.Settle(budget: 4 s, mode)` (new,
  synchronous on the dispatcher, pumped through `PumpedWait.Until` over
  `Task.WhenAny(settled, Task.Delay(budget))`): it drains pending
  re-evaluations, stops the debounce timers, issues the latest snapshot
  of every dirty episode not kept at it (claiming first where it has
  none, unless the vault is fenced), re-queues errored releases and
  retirements, waits for every obligation of the vault's key, and
  re-reads the tabs after each pumped frame, issuing again for anything a
  frame changed, within the same budget. It returns `Kept` when no
  obligation of the key is unresolved, no discard is unresolved, and every
  dirty episode is kept; otherwise `NotKept(reason)`: the first lost
  claim, refusal or error's reason, core's taken-over or discard-pending
  reason, or core's timed-out reason (`hot_exit_reason_timed_out`). In
  `ReleaseOnly` mode (after a committed Discard All, below) it issues no
  claim or write and waits only for releases and retirements.
- **Application close and switch: a kept close is a new successful
  outcome.** Where the round loop would open the prompt (`:1955-1957`)
  they call `Settle`. On `Kept` the teardown re-checks, after the pumped
  frames, exactly as the Discard check does: `SavesIdle`, and the dirty
  set and each tab's `ItemIdentity` and `EditRevision` unchanged since
  the settle began. If they hold, it returns **`ClosedKept`** (new
  `WorkspaceTeardown` member: closed with the dirty buffers kept, not
  saved; silent on application close, and on a switch spoken as today's
  switch is); if not, it continues the round loop and settles again. On
  `NotKept(reason)` it posts `UnsavedNotesNotKept { reason }` (High, also
  the status line through `ReportTerminalStatus`) and opens today's
  prompt.
- **Every successful return is settled.** Before `Closed` and
  `ClosedAllSaved` (`:1949-1952`) on every route, the teardown settles
  too. With nothing dirty, `NotKept` means an obligation is unresolved —
  a release or retirement the reader's discard or save is owed, an
  orphaned claim's release, a discard: the route **does not close**
  (WB-29a §5–§6: an unconfirmed release falls back to the prompt). It
  returns `Refused`, posts `UnsavedNotesNotDiscarded` (High, core's
  sentence: the kept copies are not yet discarded, the vault stays open,
  try again or cancel) and puts it in the status line; closing again
  settles again.
- **Close Vault still asks**, settles nothing before its prompt, and
  saves as today; its Save All's clean transitions release, and the
  settle above runs before it returns.
- **Every Discard All fences (O6).** Close Vault's, and the fallback
  prompt's on application close and switch: once the Discard check
  (`:1981-1986`) passes, every dirty episode is marked ended by the
  reader with no release owed (the epoch retires them), the writer stops
  every timer and fences the vault, issues `DiscardVault(key)` and waits
  for it at most 4 s **without pumping** (§1 makes that wait
  deadlock-free; 4 s keeps it under Windows' 5 s hung-window threshold).
  Committed: it settles in `ReleaseOnly` mode and returns
  `ClosedChangesDiscarded`. Anything else: the route returns `Refused`,
  the vault stays open, `UnsavedNotesNotDiscarded` is posted and put in
  the status line, and the fence stays while any discard of the key is
  unresolved; when the last resolves, O6's epoch read decides — advanced:
  the reader's tabs are still dirty in memory, so each starts a new
  episode, re-claims at the new epoch and re-writes; not advanced: the
  ended episodes' claims are still live, so each dirty tab re-claims
  afresh (its old claim released behind O2) and re-writes. Either way
  nothing written before the fence counts as kept.
- **A store that cannot discard keeps the vault open** (WB-29a §5). The
  reader can Save All, or close again when the store answers. Whether an
  explicit "close and keep the copies" choice should exist for a store
  that never answers is owner call D-1 (§Deviations 6); it is not built.
- **A dirty tab's own Discard ends its episode** (a tab close, a pane
  close, a dirty navigation `AdmitDirtyTab` or `CloseActivePane` accepted
  Discard for) before the tab is disposed (`:493`, `:651-654`) or
  re-pointed (`:286`), so its claim is released and any inherited payload
  retired. The reader sees the tab gone at once; the release is an
  obligation the next close settles.
- **Disposal abandons.** `WorkspaceViewModel.Dispose` calls
  `HotExitWriter.Detach()` first: timers stop, re-evaluations and late
  outcomes publish nothing, and every live episode is abandoned (O4: no
  release). It waits for nothing.

### 5. Restoring into the tabs the layout builds

The constructor stores the `HotExitOpen` and runs `Restore` as today;
`RestoreNode` consults it as it constructs each tab (WB-29a §3):

- **A layout tab whose id matches an entry** (§2) takes the entry's text,
  dirty, on the classified path (the state's item re-pointed first when
  the path differs, keeping the `Id`), before `AttachTabDocumentsIfNeeded`
  (`:85`) and `RestoreActive` (`:91`), so the published active tab is
  the dirty one.
- **The baseline is the entry's, not the disk's.** For `Restore` and
  `RestoreStale` the tab's `SavedContentHash` is the entry's
  `baseline_hash` (the load had set the disk's) and its `FileIdentity`
  the entry's `baseline_file_id`; `RestoreStale` also sets
  `IsExternallyStale`. So the first save sends the old baseline and stops
  at `WriteConflict` when the file changed since the buffer was kept.
  `ExistsNow`, `Gone` and `Recovered` make the tab a new unsaved note at
  the classified path with a null baseline: its first save is
  create-exclusive and refuses a file now there.
- **A restored baseline's identity is enforced.** While a tab's baseline
  came from an entry (it and every copy of that buffer, through the copy
  paths in §3), `StartSave` captures `SavedContentHash` and `FileIdentity`
  into the `SaveRequest`, and `WriteForSave` calls
  `SaveTextChecked(path, text, hash, identity)` when both are non-null; a
  landed save ends the enforcement (the tab's own landed baseline is as
  on `main`). A tab that loaded its note itself saves as on `main`; its
  identity only travels into its entries.
- **An entry with no layout tab** opens as a new tab (§2's ids) in the
  active group, in `written_at` order, after `Restore` returns and before
  `SyncPanels()` (`:2494`).
- **Peers whose texts differ** are reconciled first (WB-29a §3): the
  latest `written_at` text goes to every tab on that path; each other
  distinct text opens as a new unsaved note at `{stem} (recovered).md`
  (numbered when taken) whose `Id` and `RecoveryEntryId` are that entry's
  (§2's canonical case) — the layout tab that held that id takes a fresh
  `Id` and `RecoveryEntryId`, since its own text is the latest's.
- **Each restored tab starts its episode at install, adopting, and owes a
  retirement.** Once the workspace is constructed, every restored tab
  queues `Claim(key, epoch, RecoveryEntryId, adopts: entry.incarnation)`
  and records an inherited retirement for that incarnation. When the
  reader ends the episode: with a live adopting claim, its release
  retires the inherited payload too (WB-29a §6); without one (the claim
  failed or was refused), the store host queues a *retirement* — an
  adopting claim followed by its release, retried by every settle until
  confirmed — so a failed claim never strands the restored text. The
  episode's first successful write replaces the inherited payload and
  discharges the retirement.
- **The copy, once, after the landing.** After `WorkspaceReady`
  (`:1631`), the lifecycle posts `UnsavedNotesRestored { count }` once,
  then each non-`Restore` decision's notice. Nothing is spoken when
  nothing was restored.

### 6. A vault with no key, or a read that failed

`HotExitOpen.None(reason)` (no key, or a host with no store) and a
`Damaged` or failed read make the writer inert for the session: no claim
and no write. `Settle` returns `Kept` with nothing dirty (a clean close
is unaffected) and `NotKept(reason)` otherwise, so application close and
switch with dirty tabs keep today's prompt and post `UnsavedNotesNotKept
{ reason }` — except an uninstalled host (no store and a null reason,
§1), which opens the prompt silently, as on `main`. After `Damaged`,
Close Vault's Discard All still calls `discard_vault` with the key (the
repair); otherwise it calls nothing. A crash keeps nothing, as on
`main`.

### 7. The core additions this host needs (WB-29 PR 1)

Core has no sentence for two of the host's reasons, and core owns the
copy. **PR 1 (core)** adds, in `a11y.rs`, the reason fragments
`HOT_EXIT_REASON_TAKEN_OVER` ("another Slate window took over one of
these notes") and `HOT_EXIT_REASON_DISCARD_PENDING` ("an earlier discard
of the kept copies has not finished"), and FFI accessors
`hot_exit_reason_taken_over()`, `hot_exit_reason_discard_pending()` and
`hot_exit_reason_store_unwritable()` beside `hot_exit_reason_timed_out()`,
with core facts that each renders inside `UnsavedNotesNotKept`. No new
event. **PR 2 (the Windows host)** is everything else here.

### Deliberately not changed

The Close Vault prompt; the save path except a restored baseline's
identity check and the identity read at load and landed save;
`WorkspacePersistence`; `WorkspaceSaveCoordinator` and
`CloseAndJoinWorkers` (save workers are still joined, unbounded, before
the session goes).

### Deviations, for the owner and Codex

1. **No `Admit` for recovery writes.** Claims, generations and the
   snapshot order one tab's writes in the store; the save path and the
   writer share no ticket. The crash window between a save and the next
   write restores stale and its first save conflicts (§3, §5).
2. **A Discard All that cannot commit keeps the vault open** (WB-29a §5),
   the writer fenced until the discard resolves; the old spec proceeded.
3. **No re-seat.** A note moved while Slate was closed restores as `Gone`
   at its old path (WB-29a Deviation 1).
4. **A non-canonical entry id** (none this host writes) keeps its id for
   the tab's life but is not in the layout; a further crash reopens it as
   a new tab beside the layout's — a duplicate tab, never a loss.
5. **The fallback prompt is Close Vault's dialog** ("keep the vault
   open"), unchanged on application close; filed if the field pass finds
   it confusing.
6. **D-1 (owner call, open; nothing built).** WB-29a §5 keeps the vault
   open while the store cannot discard, and §6 refuses a close while a
   release is unconfirmed. A store that never answers (writes denied, a
   full disk) therefore keeps the window open until the reader saves or
   the store recovers. The owner may want an explicit "close and keep
   the copies" choice — the copies then return unsaved at the next open;
   that would amend WB-29a §5–§6 and add a core sentence, and is not in
   this chunk.
7. **The crash shape in facts is disposal without a teardown,** which
   abandons episodes (O4) as a crash does; the journey's
   `HotExit_AKilledProcessKeepsAnUnsavedNote` kills the real process.

## Tests

xUnit `Noun.VerbPhrase`, in `SlateWindows.Tests/HotExitTests.cs` (new),
each over its own temp vault and its own `HotExitStoreHost` on a temp
device root (the real core store), on `PumpedSaveReentrancyTests`'
`Host` shape. A store call is held through `BeforeCallForTests` (its
start observed by an asserted `Wait(2 s)`, released in `finally`).
"Reopening" opens the vault again over the same store host through a new
`VaultLifecycleViewModel`; "the crash shape" disposes the lifecycle with
no teardown route; a teardown run with a call held is guarded by a 20 s
watchdog every such fact asserts never fired. Facts wait out the 1 s
debounce explicitly. Every `Wait` asserts its result. Every successful
route asserts its outcome (`PrepareForApplicationClose()` true, the
switch completed, `ClosedKept` / `Closed` / `ClosedAllSaved` /
`ClosedChangesDiscarded`) and a bounded number of settles.

| Fact | Asserts | Mutation |
|---|---|---|
| `EveryStoreCallIsRecordedBeforeItIsIssued` | at `BeforeCallForTests`, the ledger already holds the call's obligation | record on return |
| `ADirtyTabClaimsAndWritesAtOnce` | the first edit claims `RecoveryEntryId` and writes generation 1 without the debounce | write only on the debounce |
| `AChangedSnapshotIsWrittenAfterTheDebounce` (Theory: an edit, a rename while dirty, a save landing behind further edits) | one write of the new snapshot 1 s after the change; the entry's path, hash and identity match the tab's | watch the generation only |
| `TheWriterReadsATabAfterItsMutation` (Theory: a peer's save mirrored clean, a peer's edit mirrored dirty, a reload, opening into a dirty peer's path) | each ends or starts the episode it should | decide at the bump |
| `ACleanTabReleasesItsEntry` (Theory: save, undo to clean, reload) | the store holds nothing for the id; a held release does not block the dispatcher | keep it; or release on the dispatcher |
| `ALateClaimNeverDisplacesANewerEpisode` | C1 held before issue; clean, then a new edit; C2 waits behind C1 (O2); C1 resolves and is released; C2 claims and writes; a close is `ClosedKept`; the crash shape then restores C2's text | issue C2 without the queue |
| `AClaimForAReaderEndedEpisodeIsReleased` (Theory: saved clean, discarded, re-pointed while the claim is held) | the claim resolves, is released; reopening restores nothing | drop it |
| `AClaimForAnAbandonedEpisodeIsKept` | a restored tab's adopting claim held; the crash shape; released: reopening restores the text | release on disposal |
| `AnOldClaimsLateOutcomeNeverCountsForANewOne` | C1's write held; clean, then C2; C1's late `Durable` and `Superseded { Claim }` change nothing for C2 | record any outcome |
| `ASaveNeverWaitsOnARecoveryWrite` | a write held; Ctrl+S reaches `SaveWriteHookForTests` at once and lands | route writes through `Admit` |
| `AKeptCloseClosesWithoutAPrompt` (Theory: application close, switch) | an edit after the last debounce and one typed inside the settle's frame; outcome `ClosedKept`, the prompt never invoked; reopening restores the final text dirty, the file unchanged | `continue` on `Kept` (the rounds run out: `Refused`) |
| `AKeptCloseReSettlesWhenASaveSlipsIn` | a Ctrl+S admitted during the settle's frame: the round loop settles the save, then closes `Closed` | return `ClosedKept` without the re-check |
| `ASettleThatCannotKeepFallsBackToThePromptWithItsReason` (Theory: write error, cap refusal, lost claim, timeout) | the prompt opens; `UnsavedNotesNotKept` with core's reason, in the status line; Cancel keeps the window and the buffer | close on failure |
| `ACleanCloseSettlesItsReleases` (Theory: application close, switch, Close Vault, Save All on the fallback prompt) | the last dirty tab saved, its release held under 4 s; the route closes on its confirmation; reopening restores nothing | return the early `Closed` unsettled |
| `AnUnconfirmedReleaseRefusesTheClose` | a release held past 4 s: `Refused`, `UnsavedNotesNotDiscarded`; released, a second close succeeds and reopening restores nothing | close anyway |
| `CloseVaultStillAsksAndDiscardAllFencesTheStore` | Close Vault prompts with a write held; Discard All issues `discard_vault` after the fence, nothing after it; `ClosedChangesDiscarded`; reopening restores nothing | skip the discard; or claim in the settle after it |
| `ARefusedDiscardKeepsTheWriterFencedUntilItResolves` (Theory: resolves committed; resolves failed with the epoch advanced; resolves failed with it unchanged) | `discard_vault` held past 4 s: `Refused`; an edit issues nothing and a close settles `NotKept` (discard pending); after resolution every dirty tab re-claims and re-writes before a close is `ClosedKept`; reopening restores the edits | lift the fence on refusal; or trust the old claims after an advanced epoch |
| `TwoUnresolvedDiscardsKeepTheFence` | two refused Discard Alls held; the first commits: the fence stays until the second resolves | lift on the first |
| `ADiscardWaitNeverDeadlocksTheDispatcher` | `DiscardVault` completes while the dispatcher is blocked in its wait | resume on the dispatcher |
| `ADiscardedTabsEntryIsNotRestored` (Theory: close the tab, close its pane, open another note into it, each answered Discard) | the episode ends before disposal or re-pointing; reopening builds no tab for it | skip the release |
| `TwoDirtyPeersAreTwoEntries` | two tabs on `a.md`, one discards; reopening restores the other's | key by path |
| `ATeardownNeverWaitsOnARecoveryWrite` (Theory: three routes, Save All) | a write held through the teardown, which returns after the settle's budget with it still held | hand it to `TrackWorker` |
| `DisposalJoinsASaveWorkerButNotARecoveryWrite` | a held write and a parked save; disposal joins the save, returns with the write held | route the write through the session |
| `AStalledWriteInOneVaultNeverHoldsAnother` | A's write held; switch to B; B's edits kept, B's close `ClosedKept` | one process-wide gate |
| `AReopenWaitsForTheLastSessionsObligations` | Save All with its release held under 4 s, then a reopen: the read waits and restores nothing; held past 4 s: the new writer starts fenced | read without waiting |
| `ReopeningRestoresIntoTheTabsTheLayoutBuilds` | the tab instances in `Groups` are dirty with the text; the active tab is the restored one; one landing; `WorkspaceReady` after the install | install into pre-built tabs |
| `AnEntryWithNoLayoutTabOpensANewDirtyTab` (Theory: a canonical Guid id, an upper-case Guid id, a non-Guid id) | the canonical id becomes the tab's `Id` (a kept close then reopens one tab, not two); the others keep the entry's id for their claims; discarding retires each | parse and re-format; or a fresh `Id` for a canonical id |
| `EachRestoreDecisionShapesItsTab` (Theory: the five decisions) | path, dirtiness, baseline and staleness per WB-29a §3 | restore all as `Restore` |
| `ARestoredStaleTabsFirstSaveConflicts` | the file changed after the buffer was kept: the first Ctrl+S conflicts, the file untouched | keep the disk's hash |
| `AReplacedFileIsNeverOverwritten` (Theory: a restored tab, its duplicate, a new tab on its path, an ordinary tab's entry restored, a save that landed behind further edits) | the file replaced with the same bytes after the entry was kept: the save is refused | carry no identity in ordinary entries; or check only the restored tab |
| `RestoreThenEditThenReopenGetsTheEdit` | a restored tab edited, the debounce waited, the crash shape: reopening restores the edit | stop writing for restored tabs |
| `RestoreThenSaveThenReopenGetsNothing` | a restored tab saved with no edit: reopening restores nothing | claim only on a dirty transition |
| `RestoreThenDiscardThenReopenGetsNothing` (Theory: the adopting claim succeeded; it failed before publication; it failed after) | discarded before any edit: the retirement runs; reopening restores nothing | retire only through a successful claim |
| `DivergentPeersAreReconciledIntoARecoveredNote` | the latest text to both tabs, the other at `{stem} (recovered).md` with its entry's id; a kept close and reopen give the same tabs | keep only the latest; or a fresh id for the recovered note |
| `ARestoreSpeaksCoresSentencesOnceAfterTheLanding` | `UnsavedNotesRestored { count }` once, then each non-`Restore` notice, after `WorkspaceReady` | post per tab; or before |
| `AVaultWithNoKeyKeepsThePromptAndKeepsNothing` | WB-28's weak-root seam: no store call; the close prompts with core's weak-root reason | write under a weak key |
| `ADamagedReadKeepsThePromptAndDiscardAllRepairs` | no claim; the prompt with core's damaged reason; Close Vault's Discard All repairs it and the next open writes | treat damage as no key |
| `AFailedReadNeverDiscardsUnreadEntries` | a read error (not damage): Close Vault's Discard All calls nothing; the entries survive for the next open | repair on any read error |
| `AStoreThatCannotOpenKeepsThePrompt` | `Open` refused: the prompt with core's reason; `HotExitStoreUnavailable` logged once | treat no store as kept |
| `AnUninstalledHostKeepsTodaysPromptSilently` | a host never installed: the prompt opens, nothing posted | post a null reason |
| `InstallationSyncsTheDeviceRootIntoItsParent` (Theory: the root missing; the root already made by the log redirect) | the parent is synced before `Open` either way; a parent that cannot be synced installs no store | sync only a root this call made |
| `A11yCorpusCensus`, `A11yTriggerParityCensus` (existing) | the seven events constructed on Windows | — |
| FlaUI `ShellAccessibilityTests.HotExit_ClosingAndReopeningKeepsAnUnsavedNote` (new partial) | type; `WM_CLOSE`: no dialog, exit within 15 s; relaunch: the tab dirty with the text; the desktop-scoped listener hears `UnsavedNotesRestored` for one note once, after `VaultOpened` | prompt on close; drop or misplace the post |
| FlaUI `ShellAccessibilityTests.HotExit_AKilledProcessKeepsAnUnsavedNote` (new) | type, wait out the debounce, kill the process; relaunch: the tab dirty with the text | write only at close |

The existing `PumpedSaveReentrancyTests` pins and the shell tests that
assert today's prompt (§1) run unchanged.

## Docs and registers

- Contract 38 D-10 (PR 2): a dated amendment (application close and
  switch settle and close kept, `ClosedKept`, without a prompt; a settle
  that cannot keep falls back to the prompt with core's reason; Close
  Vault still asks; every successful close settles the vault's
  obligations first and a close with an obligation unresolved is
  refused; every Discard All waits at most 4 s for the store's fence and
  otherwise keeps the vault open with the writer fenced; a dirty tab's
  own Discard and a clean transition release; no teardown waits on a
  recovery write beyond the settle's budget; a vault with no key keeps
  the prompt).
- PR 2: the seven Windows designations become construction sites; the
  ledger (`python -I scripts/a11y_trigger_ledger.py --write`, then
  `--check`).
- `18_windows_port/w_c_matrix.md`: the Workspace tabs row gains the
  restore sentences; `python -I scripts/generate-parity-matrix.py
  --check`.
- `scripts/windows/launch-for-field-pass.ps1`: `'hot-exit'` joins
  `$deviceStores`, so the field pass's relaunch on one root is not
  refused (exit 2).
- Help docs: if `docs/help/` names the close prompt, update the sentence.
- PR body: `Refs #1525` (or `Closes #1525` if the Mac half is merged and
  field-passed), the NVDA phrases, "not delivered" items.

## Verification

PowerShell at the repo root, each command's `$LASTEXITCODE` checked:

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb18'
function Assert-Ok([string] $What) { if ($LASTEXITCODE -ne 0) { throw "$What exited $LASTEXITCODE" } }
./apps/slate-windows/generate-bindings.ps1; Assert-Ok 'generate-bindings'
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release; Assert-Ok 'dotnet build'
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
dotnet test $tests --no-build --configuration Release --filter 'FullyQualifiedName~SlateWindows.Tests.HotExitTests|FullyQualifiedName~Censuses|FullyQualifiedName~PumpedSaveReentrancyTests'; Assert-Ok 'hot-exit facts, censuses, save-join pins'
dotnet test $tests --no-build --configuration Release; Assert-Ok 'full Windows suite'
$at = 'apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj'
dotnet test $at --no-build --configuration Release --filter 'FullyQualifiedName~ShellAccessibilityTests.HotExit_ClosingAndReopeningKeepsAnUnsavedNote'; Assert-Ok 'FlaUI journey'
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes; Assert-Ok 'dotnet format'
python -I scripts/generate-parity-matrix.py --check; Assert-Ok 'parity matrix'
python -I scripts/a11y_trigger_ledger.py --check; Assert-Ok 'trigger ledger'
```

Every fact is seen failing first or by its mutation, run locally.

**NVDA field pass**, Release build, NVDA running, every launch through
WB-1's `scripts/windows/launch-for-field-pass.ps1 -Root <one existing
device root>` so the relaunch reads the store the first launch wrote:

```powershell
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release; if ($LASTEXITCODE -ne 0) { throw 'build failed' }
$root = Join-Path $env:TEMP ("wb29-field-pass-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$vault = Join-Path $root 'vault'; $weak = Join-Path $root 'wb29-weak-root'; $device = Join-Path $root 'device-state'
New-Item -ItemType Directory -Path $vault, $weak, $device | Out-Null
Set-Content -LiteralPath (Join-Path $vault 'note.md') -Value '# note'
Set-Content -LiteralPath (Join-Path $weak 'note.md') -Value '# weak note'
function Start-Slate([string]$Path) {
    pwsh -NoProfile -File scripts/windows/launch-for-field-pass.ps1 -Vault $Path -Root $device
    if ($LASTEXITCODE -ne 0) { throw "launch-for-field-pass.ps1 exited $LASTEXITCODE (2: it refused the root $device)" }
}
Start-Slate $vault   # scenario (1); each relaunch in (2)-(4) is this same line again
```

Scenario (5) denies writes to the store's folder with a checked deny
entry (.NET's `FileSystemAccessRule`, not `icacls /deny`, whose
`SYNCHRONIZE` would refuse reads too) and undoes it after:

```powershell
$hotExit = Join-Path $device 'hot-exit'
if (-not (Test-Path -LiteralPath $hotExit -PathType Container)) { throw "no hot-exit store at $hotExit (scenario (1) writes it)" }
$me = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
$deny = [System.Security.AccessControl.FileSystemAccessRule]::new($me, 'CreateFiles, CreateDirectories, Delete, DeleteSubdirectoriesAndFiles', 'ContainerInherit, ObjectInherit', 'None', 'Deny')
$acl = Get-Acl -LiteralPath $hotExit; $acl.AddAccessRule($deny); Set-Acl -LiteralPath $hotExit -AclObject $acl
$probe = Join-Path $hotExit 'wb29-deny-probe.tmp'
$denied = $false; try { [System.IO.File]::WriteAllText($probe, 'x') } catch [System.UnauthorizedAccessException] { $denied = $true }
if (-not $denied) { Remove-Item -LiteralPath $probe; throw 'the deny did not take' }
Start-Slate $vault   # scenario (5): type, Alt+F4 — the prompt, with core's reason spoken and in the status line
icacls $hotExit /remove:d "*$($me.Value)" /T /C; if ($LASTEXITCODE -ne 0) { throw 'icacls could not remove the deny' }
[System.IO.File]::WriteAllText($probe, 'x'); Remove-Item -LiteralPath $probe
$env:SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED = 'wb29-weak-root'
Start-Slate $weak   # scenario (6)
Remove-Item Env:SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED
```

The scenarios: (1) type in a note, Alt+F4 — no dialog, the window
closes; (2) relaunch — "Restored 1 unsaved note." after the
vault-opened sentence, the dirty marker in the tab title, the caret in
the restored note; (3) change the file outside Slate, relaunch — core's
changed-on-disk notice and, on Ctrl+S, the conflict sentence; (4) Close
Vault with a dirty tab — the prompt as today; (5) the store denied
writes: type, Alt+F4 — the prompt, with "Your unsaved notes cannot be
kept for next time: …" spoken and in the status line; (6) the weak
root: type, Alt+F4 — the prompt with core's weak-root reason. Recorded
in the PR body with the NVDA log excerpt per scenario; the PR stays a
draft until then.

## Acceptance

- [ ] This rebased design reviewed (Claude red team and Codex) and
      merged before code; D-1 recorded for the owner.
- [ ] PR 1 (core): the two reason fragments and the three accessors, with
      core facts that each renders inside `UnsavedNotesNotKept`.
- [ ] PR 2: the `HotExitTests` facts, the censuses and both FlaUI facts
      pass, each seen failing first or by its mutation; the save-join pins
      and the prompt-asserting shell tests pass unchanged.
- [ ] Every store call is an obligation recorded before it is issued; one
      entry id's claims, releases and retirements run in order;
      reader-ended episodes are retired and abandoned ones are not;
      outcomes count only for the live claim and the current snapshot.
- [ ] Application close and switch close kept (`ClosedKept`) without a
      prompt only when every dirty episode is kept and nothing is
      unresolved, re-checked after the settle's frames; otherwise the
      prompt, with core's reason; Close Vault prompts; every successful
      close is settled, and an unresolved obligation refuses the close.
- [ ] Every Discard All waits at most 4 s for `discard_vault`, keeps the
      vault open and the writer fenced while any discard is unresolved,
      and after it resolves re-establishes every dirty tab's claim at the
      current epoch before anything is kept again; nothing a confirmed
      discard, release or retirement retired is restored.
- [ ] No teardown waits on a recovery write beyond the settle's budget;
      save workers are joined as on `main`; a save never waits on the
      store; the discard wait cannot deadlock the dispatcher.
- [ ] Every entry carries the tab's file identity; a restored baseline's
      identity is enforced on its first save through every copy; restored
      tabs claim at install and owe a retirement; restore injected into
      `RestoreNode`'s tabs by exact entry id, a canonical id becoming the
      tab's `Id`; one landing; the copy once after `WorkspaceReady`.
- [ ] A vault with no key, a failed read, and a host with no store keep
      the prompt with core's reason (silently for an uninstalled host);
      only a damaged read lets Discard All repair; every install syncs
      the device root into its parent.
- [ ] Registers: contract 38 amendment, designations, ledger, matrices,
      the launcher's `hot-exit`; NVDA pass (six scenarios) recorded;
      `Refs`/`Closes #1525` per the closure rule.
- [ ] Not delivered: D-1's escape (owner call), and anything else
      deferred, named.

## Brief

WB-29 is the Windows host of WB-29a's hot-exit store (#1734), in two
PRs: core's two missing reason fragments and their accessors first,
then the host. The host rests on one model (§0): a tab owns its entry
for one dirty episode, ended by the reader (retire: release the claim
and any inherited payload) or abandoned by disposal (keep everything, as
a crash would); every store call is an obligation the process-lifetime
`HotExitStoreHost` records before issuing, one entry id's claims and
releases strictly in order; an outcome counts only for the live claim
and the current snapshot (generation, path, baseline hash, file
identity); every successful close is settled; an unresolved discard
fences the writer; the writer reads a tab only after the mutation that
changed it. Application close and switch settle (4 s, pumped, re-reading
after each frame) and return `ClosedKept` without a prompt when
everything is kept and a re-check finds nothing changed; else
`UnsavedNotesNotKept` and today's prompt; a close with an obligation
still unresolved is refused with `UnsavedNotesNotDiscarded`. Close Vault
asks as today. Every Discard All fences, calls `discard_vault`, waits at
most 4 s without pumping, and keeps the vault open while it is
unresolved. Restore reads after the reopen waited for the last
session's obligations, injects into `RestoreNode`'s tabs by exact id per
WB-29a §3 with the entry's baseline enforced through every copy, claims
each restored tab at install with a retirement owed, and speaks once
after `WorkspaceReady`. Facts and mutations per the Tests table; the
field pass's launcher learns `hot-exit`.
