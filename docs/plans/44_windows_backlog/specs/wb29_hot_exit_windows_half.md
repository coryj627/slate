# WB-29 — Unsaved notes survive closing, quitting and a crash on Windows, and come back unsaved when the vault reopens

Issues: #1525 (Windows half; closes in whichever of WB-29 and plan 43
PR 29's Mac half merges last — both hosts merged and field-passed, WD20;
earlier PRs write `Refs #1525`). Size L, two PRs: PR 1 (core: two
reason fragments, their accessors and one event, §7; lanes Rust, Windows
and Mac censuses) and PR 2 (the Windows host; lane windows). AT: NVDA
full pass on PR 2. Wave: W11, slot 2.

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
most 5 s for every dirty tab to be kept and every discard to be
confirmed, and fall back to today's prompt, saying why, when that
cannot land, rather than discard the only copy. When the vault reopens,
each kept buffer comes back in its own tab, dirty, classified against
the disk by core (WB-29a §3), injected into the tabs the layout restore
constructs, and the launch landing happens once. Close Vault still asks;
its Discard All, and the fallback prompt's, completes only once the
store's discard is durable, and a store that twice cannot discard lets
the vault close with the copies kept, never trapping the reader (D-1).
Core owns every sentence. A vault with no
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
the text says so. Review round 1 (a Claude red team and Codex) found
that a host built from per-event handlers loses or resurrects text in
the gaps between them, so the host is built on one model first (§0) and
every later section is that model applied.

### 0. The model: ownership and obligations

**A buffer's ownership.** A tab owns its recovery entry for one *dirty
episode*: from the moment its buffer becomes different from its
baseline until it is clean again, discarded, re-pointed or disposed. An
episode has an `OwnershipToken` (new: the tab instance, its
`ItemIdentity`, and a per-tab episode counter), and every store call the
writer makes for it carries that token. A result whose token is no
longer the tab's current one is *orphaned*.

**Obligations.** Every store call the host issues is an *obligation*,
recorded by the store host under the vault's key **before** the call is
issued, and resolved when its result returns: a claim, a write, a
release or a vault discard. Obligations live in the store host (process
lifetime), not in a tab or a workspace, so they outlive both.

**The rules every section applies.**

- **O1, nothing unaccounted.** A call is never issued without its
  obligation, and an obligation is never dropped unresolved; a teardown
  never forgets one, it settles it or reports it.
- **O2, orphans are retired.** A claim that resolves `Claimed` for an
  orphaned token is released at once (its own release obligation), so a
  claim issued for an episode that ended can never strand a restored
  entry or keep text the reader left. A write that resolves for an
  orphaned token counts for nothing.
- **O3, kept is per live claim.** An outcome counts toward "kept" only
  when its claim's incarnation is the tab's live claim and its token is
  current; a late `Durable` of an older claim never marks a newer
  episode kept, and a late `Superseded { Claim }` of an older claim never
  marks a newer one lost.
- **O4, settle before every successful close.** Every route — application
  close, switch, Close Vault — settles the vault's obligations before it
  returns any successful outcome (`Closed`, `ClosedAllSaved`,
  `ClosedChangesDiscarded`), the clean early return (`:1949-1952`)
  included (§4).
- **O5, a discard fences until it resolves.** While a vault discard is
  unresolved, the writer issues nothing for that vault, and no settle can
  report kept; when it resolves committed, every live dirty episode
  re-claims at the new epoch before writing again (§4).
- **O6, state is read after the mutation.** The writer never decides from
  inside a mutation: it re-evaluates a tab on a coalesced dispatcher post
  after the mutation that changed it has returned, and a settle drains
  those posts first (§3).

### 1. The store host: one per process, joined by nothing

A new `HotExitStoreHost` (`HotExitStoreHost.cs`, `internal sealed`) owns
the one `HotExitStore`, the obligation ledger (per vault key: the open
claims, writes, releases and discards, each with its token, kind and
state) and every store call in flight.

**Installation.** `HotExitStoreHost.Shared` starts as a host **with no
store** (every vault then behaves as §6: the prompt, and no store call),
so a shell built without installation — every `MainWindow` test that
asserts today's prompt (`CommandPaletteTests.ModalLoop.cs:110-151`,
`CommandPaletteTests.Invoking.cs:567`, and the like) — keeps today's
prompt; such a host never posts `UnsavedNotesNotKept` (it has no store
to have failed; its reason is null and §6's prompt opens silently, as on
`main`). `InstallProductionLocation()`, called beside the journal's
(`App.xaml.cs:138`), creates `SlateDeviceState.Root` if it is missing
and, **when this call created it**, makes its name durable in its parent
(`FlushFileBuffers` on the parent opened with `FILE_FLAG_BACKUP_SEMANTICS`
and write access, P/Invoke, new); then opens `HotExitStore.Open(root)`.
WB-29a's delivery record makes the app-data folder's durability the
host's: a store under a folder whose own name may vanish keeps nothing.
Any failure — the folder cannot be made, its parent cannot be synced,
`Open` refuses — installs a host with no store and core's reason
(`Open`'s `HotExitError.reason`, or core's store-unwritable sentence
through the new `hot_exit_reason_store_unwritable()`, §7), logged once
(`HotExitStoreUnavailable`, new `HostDiagnosticEvent`), so every close
keeps the prompt with that reason. Facts build their own host over a
temp root.

**Calls.** Each call (`Claim`, `Write`, `Release`, `DiscardVault`,
`Read`) registers its obligation, then runs the store method inside a
`Task.Run` body whose whole conversion to an outcome happens on the
pool thread (no `await` that could capture the dispatcher's context), so
a dispatcher blocked on its result can never deadlock it. The task never
faults: a store error becomes an outcome carrying core's reason.
`BeforeCallForTests` and `AfterCallForTests` (`Action<HotExitStoreCall>?`:
the kind, the key's text, the entry id, the incarnation and the
generation) run on the call's own task before and after the store.

**Nothing joins it.** A store call is never handed to `TrackWorker`;
`CloseAndJoinWorkers` joins save workers only, as on `main`; no disposal
waits on a store call; settle (§4) waits only within its budget. A call
cut short by process exit is a crash (WB-29a §2). The calls take the
vault's key, never the session (WB-29a §8).

### 2. Keys, the snapshot and entry ids

At open, on the load worker, after the open scan and before the
`WorkspaceViewModel` is constructed, the lifecycle first waits, within
5 s, for the vault key's unresolved obligations from an earlier session
of this process (a reopen's read must not race the last session's
releases), then asks `session.HotExitKey()`. With a key it calls
`Read(key)` and `session.ClassifyHotExitEntries(entries)` and hands the
constructor a `HotExitOpen` (the key, the snapshot's epoch, the
classified entries, and `ReadFailure: null`). A read that fails —
`Damaged` or any other error — hands a `HotExitOpen` with the key and
the failure's reason and no entries: the writer is inert for the
session (§6), so no claim can orphan an unread entry, and Close Vault's
Discard All still calls `discard_vault` with that key, which repairs a
damaged vault (WB-29a's delivery record). With `Unavailable { reason }`,
or a host with no store, it hands `HotExitOpen.None(reason)`.

**Entry ids are opaque strings, kept apart from the tab's `Guid`.** A tab
gains `RecoveryEntryId` (string): for a tab made in this process,
`Id.ToString("D")`; for a tab restored from an entry, the entry's
`entry_id` exactly as stored, whatever its form. Every claim, write and
release of that tab uses `RecoveryEntryId`, never a re-derived string,
so an entry restored under any id can be adopted and retired under the
same id. A layout tab matches an entry when `Id.ToString("D")` equals
the entry id ordinally (no Guid parsing, no case folding); an entry no
layout tab matches opens as a new tab whose `Id` is fresh and whose
`RecoveryEntryId` is the entry's. That tab keeps the entry's id for its
life (it is not persisted in the layout, so after a further crash its
entry reopens as a new tab beside the layout's tab — a duplicate tab,
never a loss, §Deviations).

### 3. Writing as you type

A new `HotExitWriter` (`WorkspaceViewModel.HotExit.cs`, new partial)
holds per tab: `RecoveryEntryId`, the current `OwnershipToken`, its live
claim (if any), the claim obligation in flight (if any), the next
generation, the last generation issued and the last one kept under the
live claim, whether that claim was lost, the incarnation a restored tab
adopts, and the buffer's baseline (§5).

- **Observation (O6).** Every `ContentGeneration` bump (the nine sites)
  and every direct write of `_isDirty` (the four in Current state)
  schedules one coalesced re-evaluation of the tab, posted to the
  dispatcher, so it runs after the mutation that bumped has returned and
  reads the tab's finished state (`IsDirty`, text, `SavedContentHash`,
  item). A settle runs every pending re-evaluation synchronously before
  it decides (§4).
- **A tab that turns dirty starts an episode and claims.** The
  re-evaluation that finds a clean tab dirty starts a new episode (a new
  token) and issues `Claim(key, epoch, RecoveryEntryId, adopts)` with its
  obligation. `Claimed` for the current token becomes the live claim and
  issues generation 1 at once; for an orphaned token it is released (O2).
  `Superseded { Epoch }` re-reads the epoch and claims once more; a
  second marks the episode not kept with core's reason. `Refused` marks
  it not kept with core's reason.
- **While dirty, a write follows 1 s after the last edit** (a dispatcher
  timer per tab), at most one in flight per tab; a tick while one is in
  flight marks the tab due and issues on that write's return. A write
  snapshots, on the dispatcher, the path, the text, the baseline (§5) and
  the next generation under the live claim.
- **Outcomes (O3).** `Durable { g }` for the live claim and current token
  records `g` kept. `Newer { stored }` records kept when `stored` is at
  least the latest issued. `Superseded { Claim }` for the live claim marks
  it lost: the episode stops writing and stays not kept, and never
  re-claims (WB-29a: a host never takes a claim back); core's reason for
  it is `HOT_EXIT_REASON_TAKEN_OVER` (§7). `Superseded { Epoch }` for the
  live claim re-reads the epoch and re-claims once (a committed discard
  elsewhere, O5); a second marks not kept. `Refused` and errors mark not
  kept with core's reason; the next edit tries again. An outcome for any
  other claim or token changes nothing.
- **An episode ends with a release.** The re-evaluation that finds a tab
  clean (a save published, undo to the baseline, a reload, a peer's
  save mirrored), and every route that removes a tab's buffer — a dirty
  tab's own Discard (§4), a re-pointing, a disposal outside a teardown —
  ends the episode: a live claim is released, and a claim still in
  flight is released when it resolves (O2). A restored tab's episode
  starts at install with its adopting claim (§5), so it always has a
  claim to release. Each release is an obligation (O1); an errored one
  is re-issued by the next settle.
- **A save and a recovery write never wait on each other.** This host
  does not route writes through WB-2's `Admit` (§Deviations 1). A save
  that publishes with the tab still dirty is followed at once by a write
  carrying the new baseline; one that publishes clean ends the episode.
  A crash between a save landing and that next write restores the
  buffer against its old baseline, which WB-29a §3 classifies
  `RestoreStale`, and the restored tab's first save is checked against
  that old baseline (§5), so it stops at the conflict and overwrites
  nothing.

### 4. Closing, switching and discarding

- **The teardown knows its route.** `TryCloseWorkspace` and
  `TryCloseWorkspaceCore` take a `TeardownRoute` (`:543` `Switch`, `:779`
  `Close`, `PrepareForApplicationClose` `ApplicationClose`).
- **Settle (O4).** `HotExitWriter.Settle(budget: 5 s)` (new, synchronous
  on the dispatcher, pumped through `PumpedWait.Until` over
  `Task.WhenAny(settled, Task.Delay(budget))`): it runs pending
  re-evaluations, stops the debounce timers, issues the latest
  generation for every dirty tab not kept at it (claiming first where it
  has no live claim), re-issues errored releases, waits for every
  obligation of the vault's key, and re-reads the tabs after each pumped
  frame, issuing again for anything a frame changed, within the same
  budget. It returns `Kept` when no discard is unresolved, every dirty
  tab is kept at its latest generation under its live claim, and every
  release is confirmed; otherwise `NotKept(reason)` — the first lost
  claim, refusal or error's reason, core's taken-over or
  discard-pending reason, or core's timed-out reason
  (`hot_exit_reason_timed_out`).
- **Application close and switch.** Where the round loop would open the
  prompt (`:1955-1957`) they call `Settle`. `Kept` continues the round
  loop (`continue`), so a save a pumped frame admitted is settled
  (`:1929-1936`) and its clean transition's release is settled too, and
  the next round reaches the clean return. `NotKept(reason)` posts
  `UnsavedNotesNotKept { reason }` (High, also the status line through
  `ReportTerminalStatus`) and opens today's prompt.
- **Every successful return settles first.** Before `Closed` and
  `ClosedAllSaved` (`:1949-1952`) on every route, and before
  `ClosedChangesDiscarded` (below), the teardown calls `Settle` once
  more. With nothing dirty, `NotKept` can only mean an unconfirmed
  release or discard: the route proceeds, logs
  `HotExitObligationUnsettled` (new `HostDiagnosticEvent`) and leaves the
  obligation running on the store host, so the entry may restore once,
  classified and never overwriting (WB-29a §6's accepted risk, here for
  a release the reader saw complete; §Deviations 6). This is the only
  path on which a successful close leaves an obligation unsettled.
- **Close Vault still asks**, flushes nothing before its prompt, and
  saves as today; its Save All's clean transitions release, and the
  settle above runs before it returns.
- **Every Discard All fences until its discard resolves (O5).** Close
  Vault's, and the fallback prompt's on application close and switch:
  once the Discard check (`:1981-1986`) passes, the writer stops every
  timer and fences the vault (no claim or write is issued for its key),
  issues `DiscardVault(key)` and waits for it at most 5 s **without
  pumping** (§1 makes that wait deadlock-free). Committed: every episode
  ends with no release needed (the epoch retired them all), the settle
  runs, and the route returns `ClosedChangesDiscarded`. Failed or not
  resolved in time: the route returns `Refused`, the vault stays open,
  `UnsavedNotesNotDiscarded` is posted and put in the status line, and
  **the writer stays fenced while that discard is unresolved**: no
  write is issued, and every settle reports `NotKept` with core's
  discard-pending reason. When the discard resolves committed, every
  dirty episode re-claims at the new epoch and writes, so edits made
  after the refusal are kept only under the new epoch and the late
  discard can delete nothing they need. When it resolves failed, the
  fence lifts and episodes continue under their claims.
- **A store that cannot discard (owner call D-1, pending; this default
  ships).** A Discard All whose `discard_vault` fails or misses while an
  earlier one on this session already failed or missed — the store has
  answered twice that it cannot discard — closes the vault anyway with
  `ClosedChangesDiscarded`, the kept copies left in the store, and posts
  `UnsavedNotesDiscardDeferred` (new core event, §7: "Slate could not
  discard the kept copies of your unsaved notes. They will come back
  unsaved the next time this vault opens, where you can discard them
  again."). Nothing is lost and nothing is written into the vault; the
  reader is never trapped in an open vault by a broken store.
- **A dirty tab's own Discard ends its episode.** A tab close, a pane
  close and a dirty navigation that `AdmitDirtyTab` or `CloseActivePane`
  accepted Discard for end the episode before the tab is disposed
  (`:493`, `:651-654`) or re-pointed (`:286`), so its claim is released
  (or released when it resolves, O2). The reader sees the tab gone at
  once; the release is an obligation the next teardown settles.
- **Disposal.** `WorkspaceViewModel.Dispose` calls `HotExitWriter.Detach()`
  first: timers stop, re-evaluations and late outcomes publish nothing
  to tabs, and every token is orphaned, so a claim resolving later is
  released by the store host (O2). It waits for nothing. A tab disposed
  by the workspace's own disposal releases nothing itself: the teardown
  already settled its buffer.

### 5. Restoring into the tabs the layout builds

The constructor stores the `HotExitOpen` and runs `Restore` as today;
`RestoreNode` consults it as it constructs each tab (WB-29a §3, "Mapping
to tabs"):

- **A layout tab whose id matches an entry** (§2) takes the entry's
  text, dirty, on the classified path (the state's item re-pointed first
  when the path differs, keeping the `Id`), before
  `AttachTabDocumentsIfNeeded` (`:85`) and `RestoreActive` (`:91`), so the
  published active tab is the dirty one.
- **The baseline is the entry's, not the disk's.** For `Restore` and
  `RestoreStale` the tab's `SavedContentHash` is set to the entry's
  `baseline_hash` (the load had set it to the disk's) and its
  `BaselineFileIdentity` (new tab field) to the entry's
  `baseline_file_id`; `RestoreStale` also sets `IsExternallyStale`. So the
  first save sends the old baseline and returns `WriteConflict` when the
  file changed since the buffer was kept. `ExistsNow`, `Gone` and
  `Recovered` make the tab a new unsaved note at the classified path with
  a null baseline, so its first save is create-exclusive and refuses a
  file now there.
- **The baseline travels with the buffer.** `BaselineFileIdentity` is
  part of the buffer's baseline: every path that copies a dirty buffer
  and its `_contentHash` from one tab to another (`AddTab` `:375-378`,
  `ReplaceTabItem` `:333-336`, `DuplicateActiveTab` `:837`,
  `MirrorDocumentStateFrom`) copies it too; `StartSave` captures it into
  the `SaveRequest` with the hash; `WriteForSave` calls
  `SaveTextChecked(path, text, hash, identity)` when both are non-null,
  and `SaveText` otherwise; a landed save clears it. A tab that loads a
  note itself starts with none (its saves are as on `main`).
- **An entry with no layout tab** opens as a new tab (§2's ids) in the
  active group, in `written_at` order, after `Restore` returns and before
  `SyncPanels()` (`:2494`).
- **Peers whose texts differ** are reconciled first (WB-29a §3): the
  latest `written_at` text goes to every tab on that path; each other
  distinct text opens as a new unsaved note at `{stem} (recovered).md`
  (numbered when taken) whose `RecoveryEntryId` is that entry's; a layout
  tab whose entry so moved takes a fresh `RecoveryEntryId`.
- **Each restored tab claims at install, adopting.** Once the workspace
  is constructed, every restored tab starts its episode with
  `Claim(key, epoch, RecoveryEntryId, adopts: entry.incarnation)`, so a
  save, an undo to clean or a Discard before any edit releases that claim
  and retires the restored text (WB-29a §6), and its first write replaces
  it.
- **The copy, once, after the landing.** After `WorkspaceReady`
  (`:1631`), the lifecycle posts `UnsavedNotesRestored { count }` once,
  then, per restored tab whose decision is not `Restore`, its notice.
  Nothing is spoken when nothing was restored.

### 6. A vault with no key, or a read that failed

`HotExitOpen.None(reason)` (no key, or a host with no store) and a
`HotExitOpen` with a read failure make the writer inert for the session:
no claim, no write. `Settle` returns `NotKept(reason)` at once with
nothing dirty to keep reported as `Kept` (a clean close is unaffected),
so application close and switch with dirty tabs keep today's prompt and
post `UnsavedNotesNotKept { reason }` — except a host with no store and
a null reason (an uninstalled `Shared`, §1), which opens the prompt
silently, as on `main`. With a read failure, Close Vault's Discard All
still calls `discard_vault` with the key (the repair); with no key it
calls nothing. A crash keeps nothing, as on `main`.

### 7. The core additions this host needs (WB-29 PR 1)

Core has no sentence for two of the host's reasons and no event for
D-1's default, and the spec's rule is that core owns the copy. So WB-29
lands as two PRs: **PR 1 (core)** adds, in `a11y.rs`, the reason
fragments `HOT_EXIT_REASON_TAKEN_OVER` ("another Slate window took over
one of these notes") and `HOT_EXIT_REASON_DISCARD_PENDING` ("an earlier
discard of the kept copies has not finished"), with FFI accessors
`hot_exit_reason_taken_over()`, `hot_exit_reason_discard_pending()` and
`hot_exit_reason_store_unwritable()` beside the existing
`hot_exit_reason_timed_out()`; and the event `UnsavedNotesDiscardDeferred`
(High) with its sentence, its corpus row, the committed fixture, both
host corpus mirrors and its designations; **PR 2 (the Windows host)** is
everything else here.

### Deliberately not changed

The Close Vault prompt; the save path except the restored baseline's
identity check; `WorkspacePersistence` (no `RecoveryEntryId` in the
layout, §Deviations 4); `WorkspaceSaveCoordinator` and
`CloseAndJoinWorkers` (save workers are still joined, unbounded, before
the session goes).

### Deviations, for the owner and Codex

1. **No `Admit` for recovery writes.** Claims and generations order one
   tab's writes in the store; the save path and the writer share no
   ticket. The crash window between a save and the next write restores
   stale and its first save conflicts (§3, §5); it never overwrites.
2. **A Discard All that cannot commit keeps the vault open** (WB-29a
   §5), with the writer fenced until the discard resolves; the old spec
   proceeded and logged.
3. **No re-seat.** A note moved while Slate was closed restores as `Gone`
   at its old path (WB-29a Deviation 1).
4. **`RecoveryEntryId` is not persisted.** An entry whose id is not a
   layout tab's `Id` (only an entry this host did not write, or a tab
   whose entry was reconciled away) keeps its id for the tab's life; a
   further crash reopens it as a new tab beside the layout's — a
   duplicate tab, never a loss.
5. **The fallback prompt is Close Vault's dialog** ("keep the vault
   open"), unchanged on application close; filed if the field pass finds
   it confusing.
6. **An unconfirmed release on a clean close** (WB-29a §6's accepted
   risk): a close with nothing dirty whose release is still unconfirmed
   after the settle's 5 s proceeds, logged, and the entry may restore
   once, classified, never overwriting. A dirty tab's Discard is shown
   done at once; its durability is the next teardown's settle.
7. **D-1 (owner call pending):** the default that a second failed or
   missed Discard All closes anyway with the kept copies left, announced
   (§4). The alternative — the vault stays open until the store
   discards — traps the reader whenever the store is broken.

## Tests

xUnit `Noun.VerbPhrase`, in `SlateWindows.Tests/HotExitTests.cs` (new),
each over its own temp vault and its own `HotExitStoreHost` on a temp
device root (the real core store), on `PumpedSaveReentrancyTests`'
`Host` shape. A store call is held through `BeforeCallForTests` (its
start observed by an asserted `Wait(2 s)`, released in `finally`).
"Reopening" opens the vault again over the same store host through a new
`VaultLifecycleViewModel`, as a relaunch would; "the crash shape"
disposes the lifecycle with no teardown route; a teardown that runs
with a call held is guarded by a 20 s watchdog every such fact asserts
never fired. Every `Wait` asserts its result.

| Fact | Asserts | Mutation |
|---|---|---|
| `ADirtyTabClaimsAndWritesAtOnce` | the first edit claims the tab's `RecoveryEntryId` and writes generation 1 without the debounce | write only on the debounce |
| `EditsWhileDirtyAreWrittenAfterTheDebounce` | later edits write one later generation 1 s after the last edit; never two writes of one tab in flight | issue on every bump |
| `TheWriterReadsATabAfterItsMutation` (Theory: a peer's save mirrored clean, a peer's edit mirrored dirty, a reload, opening into a dirty peer's path) | each ends or starts the episode it should: a mirrored clean transition releases, a mirrored edit claims and writes | decide at the bump, inside the mutation |
| `ACleanTabReleasesItsEntry` (Theory: save, undo to clean, reload) | the store holds nothing for the id; a held release does not block the dispatcher | keep it; or release on the dispatcher |
| `AClaimResolvingAfterItsEpisodeEndedIsReleased` (Theory: the tab saved clean, discarded, re-pointed, disposed while its claim is held) | the claim resolves, is released, and reopening restores nothing for the id | drop the orphaned claim |
| `AnOldClaimsLateOutcomeNeverCountsForANewOne` | C1's write held; clean, then a new edit claims C2; C1's late `Durable` is ignored, and a close settles only on C2's own write; C1's late `Superseded { Claim }` does not mark C2 lost | record any outcome as kept |
| `ASaveNeverWaitsOnARecoveryWrite` | a write held; Ctrl+S reaches `SaveWriteHookForTests` at once and lands; the late write is never restored | route writes through `Admit` |
| `AWriteAfterASaveCarriesItsBaseline` | a save lands with the tab still dirty; the next write carries the new hash | snapshot the baseline at the tick |
| `ClosingTheWindowWithDirtyTabsSettlesThenNeverAsks` | an edit after the last debounce and a second typed inside the settle's pumped wait; no prompt; reopening restores the final text dirty, the file unchanged | close without settling; or no re-read after the frame |
| `ASwitchSettlesThenNeverAsks` | the same on `OpenVaultAsync(B)` | — |
| `ACleanCloseSettlesItsReleases` (Theory: application close, switch, Close Vault; and Save All on the fallback prompt) | the last dirty tab saved, its release held; the route waits for it within 5 s and closes on its confirmation; reopening restores nothing | return the clean early `Closed` without settling |
| `AnUnconfirmedReleaseOnACleanCloseIsLogged` | the release held past 5 s: the route closes, `HotExitObligationUnsettled` logged once; released later, reopening restores nothing | wait without a budget; or refuse the close |
| `ASettleThatCannotKeepFallsBackToThePromptWithItsReason` (Theory: write error, cap refusal, lost claim, timeout) | the prompt opens; `UnsavedNotesNotKept` with core's reason, in the status line; Cancel keeps the window and the buffer | close on failure; or drop the reason |
| `CloseVaultStillAsksAndDiscardAllFencesTheStore` | Close Vault prompts with a write held; Discard All issues `discard_vault` after the fence and no claim or write after it; reopening restores nothing | skip the discard; or issue after the fence |
| `ARefusedDiscardKeepsTheWriterFencedUntilItResolves` | `discard_vault` held past 5 s: `Refused`, `UnsavedNotesNotDiscarded`; an edit after it issues no write and an application close settles `NotKept` with the discard-pending reason; the discard released and committed: the edit re-claims at the new epoch and is kept; reopening restores the edit | un-fence on the refusal (the late discard deletes the kept edit) |
| `ASecondFailedDiscardClosesWithTheCopiesKept` (D-1) | the store refuses `discard_vault` twice: the second Discard All closes, `UnsavedNotesDiscardDeferred` posted; reopening restores the copies dirty | keep the vault open forever |
| `ADiscardWaitNeverDeadlocksTheDispatcher` | `DiscardVault` completes while the dispatcher is blocked in its 5 s wait | an outcome conversion that resumes on the dispatcher |
| `ADiscardedTabsEntryIsNotRestored` (Theory: close the tab, close its pane, open another note into it, each answered Discard) | the episode ends before disposal or re-pointing; a settled close, then reopening, builds no tab for it | skip the release on that route |
| `TwoDirtyPeersAreTwoEntries` | two tabs on `a.md`, one discards; reopening restores the other's text | key entries by path |
| `ATeardownNeverWaitsOnARecoveryWrite` (Theory: three routes, the prompt answering Save All) | a write held through the teardown, which returns with it still held after the settle's budget; `CloseAndJoinWorkers` joined save workers only | hand the write to `TrackWorker` |
| `DisposalJoinsASaveWorkerButNotARecoveryWrite` | a held write and a parked save; disposal joins the save and returns with the write held; the crash shape then restores the write | route the write through the session |
| `AStalledWriteInOneVaultNeverHoldsAnother` | vault A's write held; switch to B; B's edits are kept and B's close never prompts, with A's still held | one process-wide gate |
| `AReopenWaitsForTheLastSessionsObligations` | Close Vault's Save All with its release held, then a reopen in the same process: the read waits for it and restores nothing | read without waiting |
| `ReopeningRestoresIntoTheTabsTheLayoutBuilds` | the tab instances in `Groups` are dirty with the text; the active tab is the restored one; one landing; `WorkspaceReady` after the install | install into pre-built tabs |
| `AnEntryWithNoLayoutTabOpensANewDirtyTab` (Theory: a Guid id, an upper-case Guid id, a non-Guid id) | a new tab in the active group whose claims use the entry's id exactly; discarding it retires the entry | parse and re-format the id |
| `EachRestoreDecisionShapesItsTab` (Theory: Restore, RestoreStale, ExistsNow, Gone, Recovered) | path, dirtiness, baseline and staleness per WB-29a §3 | restore every decision as `Restore` |
| `ARestoredStaleTabsFirstSaveConflicts` | the file changed after the buffer was kept: the first Ctrl+S returns the conflict and leaves the file untouched | keep the disk's hash as the baseline |
| `ARestoredTabsFirstSaveRefusesAReplacedFile` (Theory: the restored tab, its duplicate, a new tab opened on its path) | the file replaced after classification with the same bytes: the save is refused | check the identity on the restored tab only |
| `RestoreThenEditThenReopenGetsTheEdit` | a restored tab edited, the crash shape: reopening restores the edit | stop writing for restored tabs |
| `RestoreThenSaveThenReopenGetsNothing` | a restored tab saved with no edit: reopening restores nothing | claim only on a dirty transition |
| `RestoreThenDiscardThenReopenGetsNothing` | a restored tab discarded before any edit: reopening restores nothing | claim without `adopts` |
| `DivergentPeersAreReconciledIntoARecoveredNote` | two entries on one path with different texts: the latest goes to both tabs, the other opens at `{stem} (recovered).md`; an edit then mirrors only the newest | keep only the latest |
| `ARestoreSpeaksCoresSentencesOnceAfterTheLanding` | `UnsavedNotesRestored { count }` once, then each non-`Restore` notice, after `WorkspaceReady` | post per tab; or before `WorkspaceReady` |
| `AVaultWithNoKeyKeepsThePromptAndKeepsNothing` | WB-28's weak-root seam: no store call, application close prompts with core's weak-root reason, the crash shape restores nothing | write under a key derived from the weak identity |
| `ADamagedReadKeepsThePromptAndDiscardAllRepairs` | a damaged vault: no claim, the close prompts with core's damaged reason; Close Vault's Discard All repairs it and the next open writes | treat a damaged read as no key |
| `AStoreThatCannotOpenKeepsThePrompt` | `Open` refused: closes prompt with core's reason, `HotExitStoreUnavailable` logged once | treat no store as kept |
| `AnUninstalledHostKeepsTodaysPromptSilently` | a host never installed: the prompt opens and nothing is posted | post a reason with no store |
| `AFirstLaunchSyncsTheNewDeviceRootIntoItsParent` | a missing device root under a fixture folder: installation creates it and syncs the parent before `Open` (recorded order); a parent that cannot be synced installs no store | skip the parent sync |
| `A11yCorpusCensus`, `A11yTriggerParityCensus` (existing) | the eight events constructed on Windows | — |
| FlaUI `ShellAccessibilityTests.HotExit_ClosingAndReopeningKeepsAnUnsavedNote` (new partial) | type into `note.md`; `WM_CLOSE`: no dialog, the process exits within 15 s; relaunch on the same device root: the restored tab is dirty with the text; the desktop-scoped `DesktopNotificationListener` hears core's `UnsavedNotesRestored` for one note exactly once, after the `VaultOpened` line | prompt on close; drop the post; post before `WorkspaceReady` |

The existing `PumpedSaveReentrancyTests` pins and the shell tests that
assert today's prompt (§1) run unchanged.

## Docs and registers

- Contract 38 D-10 (PR 2): a dated amendment (application close and
  switch settle then never ask; a settle that cannot keep falls back to
  the prompt with core's reason; Close Vault still asks; every
  successful close settles the vault's obligations first; every Discard
  All waits at most 5 s for the store's fence and otherwise keeps the
  vault open with the writer fenced, closing with the copies kept after
  a second failure (D-1); a dirty tab's own Discard and a clean
  transition release; no teardown waits on a recovery write; a vault
  with no key keeps the prompt).
- PR 1: the new event's corpus row, fixture, golden row, both host
  corpus mirrors and its Windows and Mac designations; the ledger
  (`python -I scripts/a11y_trigger_ledger.py --write`, then `--check`).
- PR 2: the eight Windows designations become construction sites; the
  ledger again.
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
- [ ] PR 1 (core): the two reason fragments, the three accessors and
      `UnsavedNotesDiscardDeferred`, with core facts, corpus, fixture,
      golden table, both host mirrors, designations and ledger.
- [ ] PR 2: the `HotExitTests` facts, the censuses and the FlaUI journey
      pass, each seen failing first or by its mutation; the save-join
      pins and the prompt-asserting shell tests pass unchanged.
- [ ] Every store call is an obligation registered before it is issued;
      orphaned claims are released; outcomes count only for the live
      claim and current episode.
- [ ] Every successful close on every route settles the vault's
      obligations first; application close and switch close without a
      prompt only when every dirty tab is kept and nothing is unresolved;
      otherwise the prompt, with core's reason; Close Vault prompts.
- [ ] Every Discard All waits at most 5 s for `discard_vault`, keeps the
      vault open and the writer fenced while it is unresolved, and after
      a second failure closes with the copies kept (D-1); nothing a
      settled discard or release retired is restored.
- [ ] No teardown waits on a recovery write beyond the settle's budget;
      save workers are joined as on `main`; a save never waits on the
      store; the discard wait cannot deadlock the dispatcher.
- [ ] Restore injected into `RestoreNode`'s tabs by exact entry id,
      classified per WB-29a §3, the entry's baseline (hash and identity)
      carried by the buffer through every copy, restored tabs claiming
      at install, one landing, the copy once after `WorkspaceReady`.
- [ ] A vault with no key, a failed read, and a host with no store keep
      the prompt with core's reason (silently for an uninstalled host);
      a first launch syncs the new device root into its parent.
- [ ] Registers: contract 38 amendment, designations, ledger, matrices,
      the launcher's `hot-exit`; NVDA pass (six scenarios) recorded;
      `Refs`/`Closes #1525` per the closure rule.
- [ ] Not delivered: anything deferred, named.

## Brief

WB-29 is the Windows host of WB-29a's hot-exit store (#1734), in two
PRs: core's missing reason fragments and the D-1 event first, then the
host. The host rests on one model (§0): a tab owns its entry for one
dirty episode; every store call is an obligation the process-lifetime
`HotExitStoreHost` records before issuing; orphaned claims are released;
outcomes count only for the live claim; every successful close settles
the vault's obligations; an unresolved discard fences the writer; the
writer reads a tab only after the mutation that changed it. Build the
store host (installed at launch after creating and, when new, syncing
the device root; uninstalled it has no store and keeps today's prompt;
every call on a pool thread, joined by nothing) and the writer (claim on
dirty, write at once then 1 s after the last edit, one write in flight
per tab, release when the episode ends; entry ids opaque and exact).
Application close and switch settle (5 s, pumped, re-reading after each
frame) and close without a prompt only when everything is kept; else
`UnsavedNotesNotKept` and today's prompt. Close Vault asks as today.
Every Discard All fences, calls `discard_vault`, waits at most 5 s
without pumping, keeps the vault open while it is unresolved, and after
a second failure closes with the copies kept. Restore reads after the
reopen waited for the last session's obligations, injects into
`RestoreNode`'s tabs by exact id per WB-29a §3 with the entry's baseline
travelling with the buffer, claims each restored tab at install, and
speaks once after `WorkspaceReady`. Facts and mutations per the Tests
table; the field pass's launcher learns `hot-exit`.
