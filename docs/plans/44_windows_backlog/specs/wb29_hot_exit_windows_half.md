# WB-29 — Unsaved notes survive closing, quitting and a crash on Windows, and come back unsaved when the vault reopens

Issues: #1525 (Windows half; closes in whichever of WB-29 and plan 43
PR 29's Mac half merges last — both hosts merged and field-passed, WD20;
earlier PRs write `Refs #1525`). Size L. Lanes: windows (the store is
core's, from WB-29a; this chunk adds no core module). AT: NVDA full pass.
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
most 5 s for every dirty tab to be kept and every discard to be
confirmed, and fall back to today's prompt, saying why, when that
cannot land, rather than discard the only copy. When the vault reopens,
each kept buffer comes back in its own tab, dirty, classified against
the disk by core (WB-29a §3), injected into the tabs the layout restore
constructs, and the launch landing happens once. Close Vault still asks;
its Discard All, and the fallback prompt's, completes only once the
store's discard is durable. Core owns every sentence. A vault with no
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
  `WorkspaceTabState` (`:70-84`, each loading its note synchronously,
  `:234`), attaches documents (`:85`) and restores the active tab
  (`:91`).
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

Windows only, consuming WB-29a's store and session reads as they ship.
Every rule below is WB-29a's, made concrete for this host; where this
host adds a choice, the text says so.

### 1. The store host: one per process, joined by nothing

A new `HotExitStoreHost` (`HotExitStoreHost.cs`, `internal sealed`) owns
the one `HotExitStore` and every store call in flight.
`HotExitStoreHost.Shared` is installed at launch by
`InstallProductionLocation()`, beside the journal's (`App.xaml.cs:138`):
it creates `SlateDeviceState.Root` (`Directory.CreateDirectory`, a
failure logged and otherwise ignored) and opens
`HotExitStore.Open(root)`. **If `Open` fails** (it refuses a missing
root), the host is installed with no store and the `HotExitError`'s
`reason`, core's sentence, as its reason, logged once (`HotExitStoreUnavailable`, new
`HostDiagnosticEvent`); every vault then behaves as one with no key
(§6). `VaultLifecycleViewModel` passes the host to each
`WorkspaceViewModel` it builds (a new constructor parameter, `null`
meaning `Shared`); facts pass their own over a temp root.

Every call (`Claim`, `Write`, `Release`, `DiscardVault`, `Read`) runs
the store method on its own thread-pool task and returns a
`Task<…>` that never faults (a store error becomes an outcome carrying
core's reason). The host serializes nothing across tabs: the store's
per-vault lock and claims order them (WB-29a §2, §8). Its test seams,
`BeforeCallForTests` and `AfterCallForTests`
(`Action<HotExitStoreCall>?`; `HotExitStoreCall`: the kind, the key's
text, the entry id and the generation), run on the call's own task
before the store is called and after it returned, so a fact can hold a
call with its arguments fixed.

**Nothing joins it.** A store call is never handed to `TrackWorker`, so
`CloseAndJoinWorkers` joins save workers only, as on `main`; no teardown
and no disposal waits on a write; `App.OnExit` does not wait for the
host. A call cut short by process exit is a crash (WB-29a §2: the
previous durable state survives). Its calls take the vault's key, never
the session (WB-29a §8), so the session may be disposed with one in
flight.

### 2. Keys, epochs and entry ids

At open, on the load worker, after the open scan and before the
`WorkspaceViewModel` is constructed, the lifecycle asks
`session.HotExitKey()`. With a key it calls `Read(key)` (WB-29a §3:
`{ epoch, entries }`) and `session.ClassifyHotExitEntries(entries)`, and
hands the constructor a `HotExitOpen` (new record: the key, the
snapshot's epoch, and the classified entries). With `Unavailable {
reason }`, or a store host with no store, it hands `HotExitOpen.None`
with the reason. A `Damaged` read (WB-29a §3: a lost or backwards epoch,
an unreadable claim) is treated as no restore and no hot exit for this
session, with core's damaged reason, so closes keep the prompt; Close
Vault's Discard All still runs `discard_vault`, which repairs the vault
(WB-29a's delivery record).

A tab's entry id is its `Id.ToString("D")`. Ids already survive the
layout (`:3476`, `:228`), `ReplaceTabItem` keeps them, and every new tab
takes a fresh one, so one tab is one entry and peers on one file are
separate entries (WB-29a §1: a per-tab key).

### 3. Writing as you type

A new `HotExitWriter` (`WorkspaceViewModel.HotExit.cs`, new partial)
holds per tab: its live claim (if any), the next generation, the last
generation issued and the last one kept, whether its claim was lost,
the incarnation it adopts (a restored tab, §5), and its baseline file
identity. It listens to every `ContentGeneration` bump (all nine sites
and the four direct writes of `_isDirty`, Current state), on the
dispatcher.

- **A tab that turns dirty claims, then writes at once.** It calls
  `Claim(key, epoch, entryId, adopts)`. `Claimed` stores the claim and
  issues generation 1. `Superseded { Epoch }` (a Discard All elsewhere
  advanced it) re-reads the epoch once and claims again; a second
  supersession marks the tab not kept. `Refused` records core's reason.
- **While dirty, a write follows 1 s after the last edit** (a dispatcher
  timer per tab). A tab has at most one write in flight: a tick while
  one is in flight marks it due and issues on that write's return. The
  write snapshots, on the dispatcher, `(Path, text, SavedContentHash,
  baseline file identity)` and the next generation, so generations reach
  the store in order and each carries the baseline of the moment.
- **Outcomes, as WB-29a §2 defines them.** `Durable { g }` records `g`
  as kept. `Newer { stored }` records kept when `stored` is at least the
  latest issued. `Superseded { Claim }` marks the claim lost: the tab
  stops writing, is not kept, and never re-claims until it next turns
  clean (WB-29a: a host never takes a claim back). `Superseded { Epoch }`
  and `Refused` mark not kept with the reason. An error marks not kept
  with core's reason; the next edit tries again.
- **A tab that turns clean releases** (a save landed, undo to the
  baseline, a reload; and §4's own Discards): `Release(key, claim)`,
  awaited by nobody on the dispatcher. Until it is confirmed it is a
  **pending release** held by the store host per `(key, entry id)`, so
  it outlives the tab and the workspace; the next close's flush waits
  for it (§4). The tab's next dirtiness claims afresh (`adopts` None).
- **A save and a recovery write never wait on each other.** This host
  does not route writes through WB-2's `Admit` (the 2026-10-21 spec
  did): with per-claim generations a later write of the same tab always
  supersedes an earlier one in the store, so ordering against a save
  needs no shared ticket. When a save publishes and the tab is still
  dirty, the writer issues a write at once with the new
  `SavedContentHash`; when it publishes clean, the tab releases. The
  only window — a crash after a save landed and before that write — can
  restore the buffer against its old baseline, which WB-29a §3 then
  classifies `RestoreStale` (the saved file no longer has the old hash),
  so its next save stops at the conflict and overwrites nothing.
  Nothing a save does waits on the store, and nothing the writer does
  takes a save ticket.
- **The baseline file identity.** The writer reads
  `session.NoteFileIdentity(path)` when a tab loads a note and when a
  save lands (WB-29a §8: session reads on the save path, not recovery
  writes), off the dispatcher with the save worker where one runs, and
  carries it in every entry (`baseline_file_id`).

### 4. Closing, switching and discarding

- **The teardown knows its route.** `TryCloseWorkspace` and
  `TryCloseWorkspaceCore` take a `TeardownRoute` (the callers at `:543`,
  `:779` and `PrepareForApplicationClose` pass `Switch`, `Close` and
  `ApplicationClose`).
- **Application close and switch flush first, then never ask.** Where
  the round loop would open the prompt (`:1955-1957`), these two routes
  call `HotExitWriter.FlushLatest(budget: 5 s)` (new, synchronous on the
  dispatcher, pumped through `PumpedWait.Until` over `Task.WhenAny(flush,
  Task.Delay(budget))`). It stops every debounce timer, issues a write of
  the latest generation for every dirty tab not kept at it (claiming
  first where needed), waits for those and for every pending release of
  this vault's key, and — the pumped-wait invariant — re-reads the tabs
  after each frame and issues again for any generation an edit raised,
  within the same budget. It returns `Kept` when every dirty tab is kept
  at its latest generation and every pending release is confirmed; else
  `NotKept(reason)`: the first lost claim, refusal or error's reason, or
  core's timed-out reason (`hot_exit_reason_timed_out`). On `Kept` the
  teardown returns `Closed` without a prompt (the layout saves as
  today, at disposal). On `NotKept` it posts
  `UnsavedNotesNotKept { reason }` (High, core's sentence, also the
  status line through `ReportTerminalStatus`) and opens today's prompt;
  Save All saves as today.
- **Close Vault still asks**, flushes nothing, and saves as today; each
  tab its Save All makes clean releases.
- **Every Discard All waits for its fence.** Close Vault's, and the
  fallback prompt's on application close and switch: once the Discard
  check (`:1981-1986`) passes, the writer stops every timer and marks the
  workspace fenced (no claim or write is issued after it), then calls
  `DiscardVault(key)` and waits for it at most 5 s **without pumping**
  (the journal's precedent; the call never needs the dispatcher). A
  committed epoch returns `ClosedChangesDiscarded` as today. A failure
  or a miss returns `Refused`: the vault stays open, the writer is
  un-fenced (its timers re-armed), and `UnsavedNotesNotDiscarded` is
  posted (High) and put in the status line; the reader closes again to
  try again (WB-29a §5's "Try again / Cancel" is the next close, this
  host's choice: no second dialog). The discard keeps running on the
  host; a later attempt sees its outcome. No write in flight can bring
  a discarded buffer back: it carries the old epoch (WB-29a §6).
- **A dirty tab's own Discard releases.** A tab close, a pane close and
  a dirty navigation that `AdmitDirtyTab` or `CloseActivePane` accepted
  Discard for release the tab's claim before the tab is disposed
  (`:493`, `:651-654`) or re-pointed (`:286`), as a pending release
  (§3). Without it the entry, in no tab any more, would reopen as a new
  dirty tab. A tab disposed by the workspace's own disposal releases
  nothing: the teardown already decided its buffer.
- **Disposal detaches.** `WorkspaceViewModel.Dispose` calls
  `HotExitWriter.Detach()` first: timers stop and late outcomes publish
  nothing. It waits for nothing.

### 5. Restoring into the tabs the layout builds

The constructor stores the `HotExitOpen` and runs `Restore` as today;
`RestoreNode` consults it as it constructs each tab (WB-29a §3, "Mapping
to tabs"):

- **A layout tab whose id has an entry** takes the entry's text, dirty,
  on the classified path (its state's item re-pointed first when the
  path differs, keeping the id), before `AttachTabDocumentsIfNeeded`
  (`:85`) and `RestoreActive` (`:91`), so the published active tab is the
  dirty one. `Restore` and `RestoreStale` keep the file's baseline;
  `RestoreStale` also sets `IsExternallyStale`. `ExistsNow`, `Gone` and
  `Recovered` make the tab a new unsaved note at the classified path
  (null baseline), so its first save is create-exclusive and refuses a
  file now there.
- **An entry with no layout tab** opens as a new tab with the entry's id
  in the active group, in `written_at` order, after `Restore` returns
  and before `SyncPanels()` (`:2494`). An entry id that is not a Guid
  (never written by this host) opens under a fresh id.
- **Peers whose texts differ** are reconciled first (WB-29a §3): the
  latest `written_at` text goes to every tab on that path; each other
  distinct text opens as a new unsaved note at `{stem} (recovered).md`
  (numbered when taken), under the entry's id; a layout tab whose entry
  so moved takes a fresh id.
- **Each restored tab adopts.** Its first claim passes `adopts` = its
  entry's incarnation, so a release before its first write retires the
  restored text (WB-29a §6), and its first write replaces it.
- **The identity travels to the first save.** A restored tab keeps its
  entry's `baseline_file_id` and `WriteForSave` calls
  `SaveTextChecked(path, text, hash, identity)` while it has one (a
  non-null hash and identity), cleared when a save lands; a file put at
  the path after the classification — even with the same bytes — is
  refused with `WriteConflict`, the conflict path the save already has.
- **The copy, once, after the landing.** After `WorkspaceReady`
  (`:1631`), the lifecycle posts `UnsavedNotesRestored { count }` once,
  then, per restored tab whose decision is not `Restore`, its notice
  (`RestoredNoteChangedOnDisk`, `…MissingOnDisk`, `…ExistsOnDisk`,
  `…Recovered`, with the note's name). Nothing is spoken when nothing
  was restored.

### 6. A vault with no key

`HotExitOpen.None` (no key, a store host with no store, or a damaged
read) makes the writer inert for the session: no claim, write or read.
`FlushLatest` returns `NotKept(reason)` at once, so application close
and switch keep today's prompt with `UnsavedNotesNotKept { reason }`;
Close Vault is unchanged; a crash keeps nothing, as on `main`. No
host-side key is ever derived from a root identity.

### Deliberately not changed

The Close Vault prompt; the save path except `SaveTextChecked` for a
restored tab's first save; `WorkspacePersistence`;
`WorkspaceSaveCoordinator` and `CloseAndJoinWorkers` (save workers are
still joined, unbounded, before the session goes); no core change.

### Deviations from the 2026-10-21 spec, for the owner and Codex

1. **No `Admit` for recovery writes.** Claims and generations order a
   tab's writes in the store; the save path and the writer share no
   ticket (§3). The crash window between a save and the next write
   restores stale, never overwrites.
2. **A Discard All that cannot commit keeps the vault open** (WB-29a
   §5), instead of proceeding and logging `HotExitDiscardUnconfirmed`.
3. **No re-seat.** A note moved while Slate was closed restores as `Gone`
   at its old path (WB-29a Deviation 1); the fact
   `ABufferWhoseFileMovedReseatsOnItsNewPath` is replaced.
4. **Close Vault's prompt still says "keep the vault open"** when the
   fallback prompt opens on application close (`MainWindow.xaml.cs:365-381`
   titles it "Close Vault"); unchanged here, filed if the field pass
   finds it confusing.
5. **The app-data folder's own durability** is the host's (WB-29a's
   delivery record). The host creates it at launch and does not sync
   its parent; a power loss in the seconds after the very first launch
   created it could lose the store with it. Accepted: every later launch
   finds it on disk.

## Tests

xUnit `Noun.VerbPhrase`, in `SlateWindows.Tests/HotExitTests.cs` (new),
each over its own temp vault and its own `HotExitStoreHost` on a temp
device root (the real core store), built on `PumpedSaveReentrancyTests`'
`Host` shape. A store call is held through `BeforeCallForTests` (its
start observed by an asserted `Wait(2 s)`, released in `finally`).
"Reopening" opens the vault again over the same store host, through a
new `VaultLifecycleViewModel` after application close, as a relaunch
would. "The crash shape" disposes the lifecycle with no teardown route.
Every `Wait` asserts its result.

| Fact | Asserts | Mutation |
|---|---|---|
| `ADirtyTabClaimsAndWritesAtOnce` | the first edit claims the tab's id and writes generation 1 without waiting for the debounce; the store holds the text and baseline | write only on the debounce |
| `EditsWhileDirtyAreWrittenAfterTheDebounce` | further edits write one later generation 1 s after the last edit, never two writes of one tab in flight | issue on every bump |
| `EveryDirtinessChangeIsSeen` | a peer's mirrored edit and a reload that keeps the buffer dirty (the direct `_isDirty` writes) are written | listen to the `IsDirty` setter only |
| `ACleanTabReleasesItsEntry` | save, undo to clean, and a reload each release: the store holds nothing for the id; a release held by the seam does not block the dispatcher (a posted operation runs before the release) | keep it; or release on the dispatcher |
| `ASaveNeverWaitsOnARecoveryWrite` | a write held in flight; Ctrl+S reaches `SaveWriteHookForTests` at once and lands; the write released late is `Superseded` or `Newer`, never restored | route writes through `Admit` |
| `AWriteAfterASaveCarriesItsHash` | a save lands with the tab still dirty; the next write carries the new `SavedContentHash` | snapshot the baseline at the tick |
| `ClosingTheWindowWithDirtyTabsFlushesThenNeverAsks` | an edit after the last debounce and a second typed inside the flush's pumped wait; `PrepareForApplicationClose()` true, the prompt never invoked; reopening restores the final text dirty, file bytes unchanged | return `Closed` without flushing; or no re-read after the frame |
| `ASwitchFlushesThenNeverAsks` | the same on `OpenVaultAsync(B)` | — |
| `AFlushThatCannotLandFallsBackToThePromptWithItsReason` (Theory: write error, cap refusal, lost claim, timeout) | the prompt opens; `UnsavedNotesNotKept` posted with core's reason and in the status line; Cancel keeps the window and the buffer | return `Closed` on failure; or drop the reason |
| `APendingReleaseMustLandBeforeACloseWithoutAPrompt` | a clean transition's release held; application close waits for it within the budget and closes on its confirmation; held past 5 s, the prompt opens | flush ignores pending releases |
| `CloseVaultStillAsksAndDiscardAllFencesTheStore` | Close Vault prompts with a write held; Discard All issues `discard_vault` after the fence and no claim or write after it; the held write released late; reopening restores nothing and posts nothing | skip the discard; or issue a write after the fence |
| `ADiscardAllThatCannotCommitKeepsTheVaultOpen` (Theory: Close Vault, application close's fallback) | `discard_vault` held past 5 s: the teardown returns `Refused`, the vault stays open, `UnsavedNotesNotDiscarded` posted; released, a second close's Discard All completes | proceed on a miss |
| `ADiscardedTabsEntryIsNotRestored` (Theory: close the tab, close its pane, open another note into it, each answered Discard) | the release is issued before disposal or re-pointing; reopening builds no tab for it | skip the release on that route |
| `TwoDirtyPeersAreTwoEntries` | two tabs on `a.md`, one discards; reopening restores the other's text | key entries by path |
| `ATeardownNeverWaitsOnARecoveryWrite` (Theory: three routes) | a write held through the whole teardown, which returns with it still held (a watchdog of 20 s never fires); released afterwards, its return is observed; `CloseAndJoinWorkers` joined save workers only | hand the write to `TrackWorker` |
| `DisposalJoinsASaveWorkerButNotARecoveryWrite` | a held write and a parked save; disposal joins the save and returns with the write held; the write lands after the session is gone and reopening restores it (the crash shape) | route the write through the session |
| `AStalledWriteInOneVaultNeverHoldsAnother` | vault A's write held; switch to B; B's edits are kept and B's application close never prompts, with A's still held | one process-wide in-flight gate |
| `ReopeningRestoresIntoTheTabsTheLayoutBuilds` | the tab instances in `Groups` are dirty with the text; the active tab is the restored one; one landing; `WorkspaceReady` after the install | install into pre-built tabs |
| `AnEntryWithNoLayoutTabOpensANewDirtyTab` | a new tab with the entry's id in the active group | drop the entry |
| `EachRestoreDecisionShapesItsTab` (Theory: Restore, RestoreStale, ExistsNow, Gone, Recovered) | the tab's path, dirtiness, baseline and staleness per WB-29a §3; `ExistsNow`'s first save refuses the file now there | restore every decision as `Restore` |
| `DivergentPeersAreReconciledIntoARecoveredNote` | two entries on one path with different texts: the latest goes to both tabs, the other opens at `{stem} (recovered).md` | keep only the latest |
| `ARestoredTabsFirstSaveRefusesAReplacedFile` | the file replaced after classification with the same bytes; the first save is refused (`SaveTextChecked`) | save with `SaveText` |
| `ARestoredTabsReleaseRetiresTheRestoredText` | a restored tab answered Discard before its first write; reopening restores nothing | claim without `adopts` |
| `ARestoreSpeaksCoresSentencesOnceAfterTheLanding` | `UnsavedNotesRestored { count }` once, then each non-`Restore` notice, after `WorkspaceReady` | post per tab; or before `WorkspaceReady` |
| `AVaultWithNoKeyKeepsThePromptAndKeepsNothing` | with WB-28's `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` set to the fixture's folder: no claim or write, application close prompts with core's weak-root reason, the crash shape restores nothing | write under a key derived from the weak identity |
| `AStoreThatCannotOpenKeepsThePrompt` | a device root that cannot be created: the host has no store, closes prompt with core's reason, `HotExitStoreUnavailable` logged once | treat no store as kept |
| `A11yCorpusCensus` and `A11yTriggerParityCensus` (existing) | the seven events constructed on Windows; the designations replaced by construction rows | — |
| FlaUI `ShellAccessibilityTests.HotExit_ClosingAndReopeningKeepsAnUnsavedNote` (new partial `ShellAccessibilityTests.HotExit.cs`) | type into `note.md`; `WM_CLOSE`: no dialog, the process exits within 15 s; relaunch on the same device root: the restored tab is dirty with the text; the shell gate's desktop-scoped `DesktopNotificationListener`, registered before the relaunch, hears core's rendering of `UnsavedNotesRestored` for one note exactly once, after the `VaultOpened` line, raised as rendered | prompt on application close; drop the post; post before `WorkspaceReady` |

The existing `PumpedSaveReentrancyTests.DisposalJoinsASaveWorkerBeforeTheSessionGoes`
and `TheJoinRunsEveryTrackedWorkerToCompletion` run unchanged.

## Docs and registers

- Contract 38 D-10: a dated amendment (application close and switch
  flush then never ask; a flush that cannot land falls back to the
  prompt with core's reason; Close Vault still asks; every Discard All
  waits at most 5 s for the store's fence and otherwise keeps the vault
  open; a dirty tab's own Discard and a clean transition release; no
  teardown waits on a recovery write; a vault with no key keeps the
  prompt).
- `a11y_trigger_designations.json`: the seven Windows designations
  become construction sites (`python -I scripts/a11y_trigger_ledger.py
  --write`, then `--check`).
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
      merged before code.
- [ ] The `HotExitTests` facts, the censuses and the FlaUI journey pass,
      each seen failing first or by its mutation; the save-join pins pass
      unchanged.
- [ ] Application close and switch flush and never prompt when every
      dirty tab is kept and every release confirmed; otherwise the
      prompt, with core's reason; Close Vault prompts.
- [ ] Every Discard All waits at most 5 s for `discard_vault` and keeps
      the vault open when it cannot commit; every dirty tab's own Discard
      and every clean transition releases; nothing discarded is restored.
- [ ] No teardown waits on a recovery write; save workers are joined as
      on `main`; a save never waits on the store.
- [ ] Restore injected into `RestoreNode`'s tabs, classified per WB-29a
      §3, one landing, the copy once after `WorkspaceReady`.
- [ ] A vault with no key, and a store host with no store, keep the
      prompt with core's reason.
- [ ] Registers: contract 38 amendment, designations to construction
      rows, ledger, matrices, the launcher's `hot-exit`; NVDA pass (six
      scenarios) recorded; `Refs`/`Closes #1525` per the closure rule.
- [ ] Not delivered: anything deferred, named.

## Brief

WB-29 is the Windows host of WB-29a's hot-exit store (#1734). Build
`HotExitStoreHost` (one per process, installed at launch after creating
`SlateDeviceState.Root`; every call on its own thread-pool task; joined
by nothing) and `HotExitWriter` (per tab: claim on dirty, write at once
then 1 s after the last edit, one write in flight per tab, release on
clean, pending releases kept per vault key and entry id; a tab's entry
id is its layout `Id`; watch every `ContentGeneration` bump, not the
`IsDirty` setter). Application close and switch flush (5 s, pumped,
re-reading after each frame) and close without a prompt only when every
dirty tab is kept and every release confirmed; else
`UnsavedNotesNotKept` and today's prompt. Close Vault asks as today.
Every Discard All fences the writer, calls `discard_vault`, waits at
most 5 s without pumping, and on a miss keeps the vault open with
`UnsavedNotesNotDiscarded`. A dirty tab's own Discard releases before
disposal or re-pointing. Restore reads and classifies on the load worker
after the open scan, injects into `RestoreNode`'s tabs by id per WB-29a
§3 (peers reconciled, restored tabs adopt, first save through
`SaveTextChecked`), and speaks once after `WorkspaceReady`. No key or no
store: inert writer, prompt with core's reason. Facts and mutations per
the Tests table; the field pass's launcher learns `hot-exit`.
