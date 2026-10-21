# WB-29 — Unsaved notes survive closing, quitting and a crash on Windows, and come back unsaved when the vault reopens

Issues: #1525 (Windows half; closes in whichever of WB-29 and plan 43
PR 29's mac half merges last — both hosts merged and field-passed, WD20;
earlier PRs write `Refs #1525`). Size L (design first, in plan 43 PR 29's
design). Lanes: windows (the core store comes from PR 29; this chunk adds
no core module). AT: NVDA full pass. Wave: W11, slot 2, after WB-28 and
after PR 29's core store is on `main` with green CI; WB-2's
`WorkspaceSaveCoordinator.Admit` (W1) is on `main` long before. Parallel
with: WB-30. Decision: WD20 (pending; recommended "one design for both
hosts written as plan 43's PR 29 design, fixing the store schema, the
restore rule, the open-sequence ordering, and the durable key and
restore admission for a vault whose root identity is weak (WB-28's
`persistent: false`, FAT32 and exFAT); core owns the store; WB-29 starts
only after that implementation is on `main`"; under the other answer —
Windows writes its own store — this chunk grows a `HotExitStore.cs`
mirroring the design's schema, its persistent-identity key and its
weak-root policy, the FFI calls become local file IO, and
`parity_matrix.md` records the parity debt).

## Goal

Closing the main window or quitting Slate on Windows with unsaved edits
never asks and never writes the notes. Each unsaved buffer is written as
you type to the hot-exit store PR 29 ships (outside the vault, keyed by
the vault's persistent identity), so a crash or a kill keeps it too; a
close or a switch waits for a durable write of the latest edits and, if
that write cannot land, falls back to today's prompt rather than
discarding the only copy. When the vault reopens, the buffers come back
in their tabs, still marked unsaved, read after the open scan and
injected into the tabs the layout restore constructs, and the launch
landing happens once. A buffer whose file changed on disk meanwhile is
restored dirty and the existing save-time conflict machinery decides at
the next explicit save. Close Vault still asks. Core owns the copy
("Restored 2 unsaved notes"). A FAT32 or exFAT vault, whose weak
identity keys nothing durable, gets what the shared design's weak-root
policy names, and never another vault's buffers.

## Current state on main

Verified against the worktree; the plan's lines hold. **The shared
design does not exist yet:** `docs/plans/43_mac_backlog_delivery/` holds
designs 20, 21, 22 and the 24 trio only; `specs/` holds `pr22_…` only.
PR 29 is a row (`00_program.md:437`, "L (design first)"; RD1 as amended
`:557`). Nothing below can start until that design and its core-store PRs
are merged.

- **Closing asks today.** `MainWindow.xaml.cs:1986-1992` `Window_Closing`
  → `_viewModel.PrepareForApplicationClose()`; cancels on `false`.
  `VaultLifecycleViewModel.cs:637-650` `PrepareForApplicationClose`:
  refuses while busy, else `TryCloseWorkspace()` and
  `AnnounceTeardown(teardown, TeardownRoute.ApplicationClose)` (silent
  for that route, `:1433-1440`). `TryCloseWorkspace()` (`:1405-1420`,
  no route parameter today) → `TryCloseWorkspaceCore()` (`:1468-1575`,
  its doc `:1422-1467`): with `workspace.HasDirtyTabs`
  (`WorkspaceViewModel.Layout.cs:54`) it reads `DirtyTabsForPrompt()`
  (`WorkspaceViewModel.cs:2829`), calls `_confirmUnsavedClose()` (`:1538`;
  the `Func<VaultCloseDecision>` at `:78`, defaulted to `Cancel` `:213`),
  then `SaveAll()` (`:2753`) or, for Discard, requires `SavesIdle`
  (`:2816`) and `DirtyTabsStill(asked)` (`:2839`). The `TeardownRoute`
  enum is `:44-55` (`Close`, `Switch`, `ApplicationClose`). The same core
  serves the vault switch (`OpenVaultAsync`, `:454`), Close Vault
  (`CloseVault`, `:605`) and application close (`:644`).
- **The teardown waits on every admitted ticket.** Its round loop
  (`:1505-1569`) first pumps `SettleSaves()` (`WorkspaceViewModel.cs:2813`
  → `WorkspaceSaveCoordinator.SettlePumping`, `:211`, which pumps with
  no bound until the admitted count `_pending` is zero) whenever
  `SavesIdle` (`IsIdle`, `:42`) is false (`:1510-1516`), and accepts a
  Discard only with `SavesIdle` true (`:1562-1566`). After a successful
  teardown `WorkspaceViewModel.Dispose` (`:3089-3097`) calls
  `CloseAndJoinWorkers` (`WorkspaceSaveCoordinator.cs:228`), which joins
  every worker handed to `TrackWorker` (`:189-206`) with no bound; its
  test seam `BeforeJoinForTests` (`:62`, invoked at `:236`) lets a fact
  release a parked worker inside the join.
- **Admission.** `WorkspaceSaveCoordinator.HasAdmittedSaveFor`
  (`:83-87`) only reads whether a ticket under the file's chain key is
  incomplete; it reserves nothing, so a save admitted after a caller
  checked it can overtake whatever that caller starts. `Enqueue`
  (`:94-131`) chains a ticket behind every incomplete ticket under its
  keys. WB-2 (W1) adds `Task<bool> WorkspaceSaveCoordinator.Admit(string
  path, Func<SaveTicket, Task> operation)`: it ALWAYS enqueues a ticket
  under the file's chain key, idle chain included, runs the operation on
  the dispatcher once every earlier ticket has settled, and completes the
  ticket only when the operation's returned Task completes, so a save
  admitted after it waits for that Task; a closed coordinator returns
  `false` and never runs the operation. WB-2's rule: an admitted
  operation never asks `HasAdmittedSaveFor` about its own path — its own
  ticket makes it true.
- **Dirty state and edits.** `WorkspaceTabViewModel`
  (`WorkspaceViewModel.cs:94`, constructor `:138`) `IsDirty` `:435-451`
  (private set; bumps `ContentGeneration` `:239-241`, ends
  `IsTransient`); the buffer is `_editorSession` (`:430-432`
  `ReplaceAll`); `BumpContentGeneration` also at `:442`, `:478`. Save
  seams: `SaveWriteHookForTests` (`:880`, on the save worker before the
  core write) and `SaveWrittenHookForTests` (`:885`, after it landed,
  before its publication). `PumpedWait.Until(dispatcher, task)`
  (`WorkspaceSaveCoordinator.cs:336-353`) pumps until a task completes,
  with no timeout of its own.
- **The open sequence.** `VaultLifecycleViewModel.cs:1176-1192`
  `WorkspacePersistence(root).Load()` (`:1180`) then `new
  WorkspaceViewModel(session, root, …)` (`:1184`, constructor
  `WorkspaceViewModel.cs:2103`), whose body restores the layout at
  `:2309` `(_root, _activeGroup) = Restore(_persistence.Load());`.
  **`Restore` builds new instances:** `RestoreNode`
  (`WorkspaceViewModel.Persistence.cs:53-108`) creates a new
  `WorkspaceGroupViewModel` and, per `WorkspaceTabState`, a new
  `WorkspaceTabViewModel(_session, tabState, …)` (`:69-82`), attaches
  documents (`:83`), adds it to the group (`:84`) and restores the active
  tab (`:88-91`); it neither finds nor reuses pre-existing tabs.
  `WorkspaceReady?.Invoke` at `:1280`. `OpenRecentAsync` `:724`. The
  layout store is `WorkspacePersistence.cs` (`Load` `:98`, `Save`
  `:168-214`).
- **Stores outside the vault.** `%LOCALAPPDATA%\Slate`
  (`FileRecentsStore.cs:32-36`; WB-1's `SlateDeviceState.Root` honouring
  `SLATE_LOCAL_APP_DATA`); `SidebarWriteJournal.cs:301` is the journalled
  store precedent.
- **Root identity after WB-28.** A Windows session's `RootIdentity()` is
  `VaultRootIdentity { volume, id_high, id_low, persistent }`, never null
  for an open vault (WD24 as amended). `persistent: true` is the file
  system's own file id (`FileIdInfo`; NTFS, ReFS). `persistent: false` —
  FAT32 and exFAT, which answer no `FileIdInfo` — is the volume serial
  and the 64-bit file index, and WB-28 (design items 6 and 7) keys
  nothing durable by it: a FAT file index is derived from the containing
  directory's cluster and the entry's offset in it, so renaming or moving
  the folder with Slate closed can change it, and a later folder can take
  a freed slot's index — even a different folder created later at the
  same path. Within a session the weak identity is sound (the held root
  cannot be renamed or deleted), which is why the stores compare it
  there; across sessions it can neither follow the vault nor keep two
  vaults apart. WB-28's recents store therefore keys a weak root by its
  path. WB-28 PR 1's seam `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED`,
  set to a value the opened root's path contains, makes an NTFS folder
  open with a weak identity (its NTFS index, which survives a rename).
- **Interlocks.** `App.xaml.cs:151-159` `OnExit`; no `SessionEnding`
  handler. `IsExternallyStale` (`WorkspaceViewModel.cs:1827`) is the
  stale-tab flag the save path already honours. Re-seat by
  `capture_structural_identity` (`session.rs:16564`).

## Reference

- Plan 43 PR 29's design (to be written; WD20 fixes its contents).
- #1525's text (RD1): explicit save stands (#877), Close Vault still asks,
  storage keyed by vault identity outside the vault, written as you type
  (debounced), compared with disk on reopen, core-owned restore copy,
  interlocks (termination fence, layout save, layout restore, re-seat).
- Mac twin: PR 29's mac host half (the same store and FFI).
- Contract 38 D-10 (`AnnounceTeardown` doc `:1423-1432`): only the
  explicit Close speaks; application close is silent — unchanged here.
  Its pumped-wait invariant: a caller re-reads what it acts on after any
  pumped frame.
- WB-2's spec (`Admit`, item 1) and WB-28's spec (design items 6 and 7:
  the weak identity, and why it keys nothing durable).

## What the shared design must have fixed before this starts

1. The store: core-owned, its FFI (`write_buffer(path, text,
   baseline_hash)` returning a durable write result, `read_buffers()`,
   `discard_buffer(path)`, `discard_vault()` — names per the design),
   under the host-supplied device-local root, keyed by a persistent
   `VaultRootIdentity` (WB-28's lossless form; a weak one per item 7),
   bounded (per-buffer and per-vault caps, and what a write over the cap
   returns — a typed refusal the host can act on).
2. The write cadence (debounce; on every dirtiness change at least) and
   fsync policy; a write's result is durable-or-error, never fire-and-forget.
3. The restore rule: baseline hash equal → restore dirty; file changed →
   restore dirty and stale; file gone → a new unsaved note at the path;
   file moved (identity found elsewhere) → re-seat; a recovery entry with
   no tab in the saved layout → a new tab in the active group.
4. The open-sequence ordering (WD20's sentence) and the core copy:
   `A11yEvent::UnsavedNotesRestored { count }` and the per-buffer
   "changed on disk" notice (census rows, both corpus mirrors, §4 rule 6
   — PR 29's core PR adds them).
5. Interlocks: the layout save on close keeps running; Close Vault's
   prompt stays and, on Discard All, `discard_vault()` runs (Discard means
   discard).
6. **Every Discard route fences pending recovery writes before the
   durable discard.** The routes: Close Vault's Discard All; the Discard
   All of the prompt that application close and vault switch fall back to
   after a failed or timed-out flush (a timed-out flush can leave a
   `write_buffer` in flight while that prompt is open); and the
   per-buffer `discard_buffer` on a clean transition. The store's discard
   must supersede every write issued before it, including one that
   completes after it — for example a per-vault discard epoch that
   `write_buffer` carries and the store compares, the discard advancing
   it durably before it deletes, so an older epoch's write is dropped —
   and the design names the mechanism. Without it a late write leaves or
   re-creates a buffer the reader discarded, and the next open restores
   text the reader chose to throw away.
7. **A durable key and a restore-admission policy for a weak root
   (`persistent: false`).** A weak identity keys nothing durable (WB-28
   items 6 and 7): keyed by it, a buffer is orphaned when a rename or
   move made with Slate closed changes the folder's index, and is offered
   to a different vault that later takes a freed slot's index. The design
   chooses one of these and names it:
   - **(a) No hot exit on a weak root.** The store refuses `write_buffer`
     for a session whose identity is weak with a typed refusal like the
     cap's, carrying core's reason sentence, and `read_buffers()` returns
     nothing for it; application close and vault switch fall back to
     today's prompt with that reason in the status line, exactly as a
     failed flush does; a crash keeps nothing, as on `main`.
   - **(b) A path key plus a restore-admission check.** The entry is
     keyed by the root's path (the recents store's weak key, WB-28 item
     7), records the weak identity and each buffer's baseline, and an
     open admits it only through a check the design names (for example:
     the recorded weak identity equals the open root's and each buffer's
     baseline matches the file at its path). An entry no open admits is
     kept within the caps and handled as the design states (reported,
     offered, or pruned after a named retention) — never restored, never
     deleted unread.

   Either way: (R1) no buffer is ever restored into a folder other than
   the one whose session wrote it — not a different folder later at the
   same path, not one that took the same weak index; (R2) a weak root
   renamed or moved with Slate closed never loses a buffer the close
   promised to keep without the design saying what happens to it; (R3)
   the core store's own facts prove R1 for an index taken by another
   folder through a store seam that hands the store an explicit weak
   identity and root path, because index reuse cannot be produced on
   NTFS. A third policy needs the owner and re-derives this spec's two
   weak-root facts before code. WD24's opening rule is unchanged: a FAT32
   or exFAT vault keeps opening.

## Design

Windows only, consuming the merged core store and WB-2's `Admit`.

- **Writing as you type, reserved on the file's chain until it is
  durable.** `WorkspaceTabViewModel` raises a change signal at its
  `BumpContentGeneration` points (`:442`, `:478`); a new `HotExitWriter`
  (`WorkspaceViewModel.HotExit.cs`, new partial) debounces per tab on a
  dispatcher timer and, on the tick, admits the write as a WB-2
  operation: `_saves.Admit(tab.Path, ticket => WriteOperation(tab))`.
  The reservation holds both ways: a save admitted after the write waits
  for the write's durable completion (write first, save second), and a
  write admitted after a save starts only once that save has published
  (save first, write second). `WriteOperation` (new, private) runs on the
  dispatcher once the earlier tickets on the path have settled and
  re-reads the tab then, because it may start later than its tick: a
  disposed, clean or fenced tab writes nothing and its Task completes;
  otherwise it snapshots `(text, SavedContentHash, ContentGeneration)` at
  that moment — so a write admitted behind a save carries the hash that
  save published — calls `write_buffer` on a worker handed to
  `TrackWorker` (`:189-206`), and returns a Task it completes itself (a
  `TaskCompletionSource`) when the write's result has been published on
  the dispatcher: the durable generation recorded on success, the
  failure recorded on error. The ticket therefore completes after
  durable completion, never when the call returns. Two writes of one
  path never overlap: the second is admitted behind the first, so the
  store receives a path's generations in order. The writer never asks
  `HasAdmittedSaveFor` — inside its own operation the answer is always
  true (its own ticket), and outside one the Boolean reserves nothing. A
  clean transition (`IsDirty` false) discards through the fence below
  (`FenceAndDiscard(tab)`), never a bare `discard_buffer`. The writer
  records, per tab, the last generation durably written.
- **A teardown never waits on a recovery write without a bound.**
  `TryCloseWorkspace` and `TryCloseWorkspaceCore` gain a `TeardownRoute
  route` parameter (the three callers at `:454`, `:605`, `:644` pass
  `Switch`, `Close` and `ApplicationClose`). Immediately before the
  round loop (`:1505`), every route calls `HotExitWriter.Quiesce()`
  (new): it stops every debounce timer and suspends scheduling, so no
  write is admitted on a tick while the teardown runs, then calls
  `AbandonInFlight()` (new): every outstanding recovery operation's Task
  is completed by the writer — its ticket releases, so `SettleSaves()`
  and the Discard check's `SavesIdle` no longer wait on it — and is marked
  abandoned, so its late result records no durable generation and
  re-issues nothing; its worker stays tracked, so `CloseAndJoinWorkers`
  still joins it before the session goes. The writer remembers each
  abandoned worker per path and admits no new write for that path until
  the worker has finished: a tick defers its admission until then, and
  `FlushLatest` waits for it first, inside its own bound. A deferred
  admission re-checks, when the worker finishes, that the writer is not
  suspended and the tab is neither fenced nor clean; a flush that timed
  out admits nothing afterwards. So an abandoned write never lands after
  a newer generation, and a save never waits on an abandoned write (no
  ticket is held while the writer waits for one). Whatever an abandoned
  write lands later is superseded by the fenced discard of the clean
  transition or Discard All (item 6), or overwritten by the next
  generation's write, admitted only after it finished. Every `Refused`
  return after `Quiesce()` calls `HotExitWriter.Resume()` (new), which
  re-arms the timer of every dirty tab whose generation exceeds its last
  durable generation.
- **Closing never asks — once the latest edits are durable.** In the
  round loop, where the prompt would open, application close and the
  switch call `HotExitWriter.FlushLatest(timeout: 5 s)` (new;
  synchronous on the dispatcher: `PumpedWait.Until(dispatcher,
  Task.WhenAny(flushed, Task.Delay(timeout)))`, the facts asserting which
  one finished). It admits, as above, a write for every tab whose
  `ContentGeneration` exceeds its last durable generation, waits for
  their operations, and — the pumped-wait invariant — re-reads the tabs
  after the frame and admits again for any generation an edit bumped
  meanwhile, within the same bound. It returns `FlushResult { Flushed,
  Failed(reason) }` (new). On `Flushed` the teardown returns `Closed`
  with no prompt (the layout saves as today). On `Failed` — disk full,
  access denied, the store's cap refusal, the weak-root refusal under
  item 7 (a), a closed coordinator, a timeout (which first calls
  `AbandonInFlight()`) — the teardown falls back to today's prompt
  (`_confirmUnsavedClose`, Save All / Discard All / Cancel) with the
  reason in the status line (`ReportTerminalStatus`), so the only copy is
  never discarded silently. `TeardownRoute.Close` (Close Vault) keeps the
  prompt unconditionally and flushes nothing. **Every Discard All —
  Close Vault's and the fallback prompt's on application close and
  switch — runs `HotExitWriter.FenceAndDiscardVault()`** (new), and a
  clean transition runs `HotExitWriter.FenceAndDiscard(tab)` (new): each
  first fences its scope (stops the scope's debounce timers and marks it
  fenced, so no new `write_buffer` is issued for it), then calls the
  store's discard (`discard_vault()` / `discard_buffer(path)`) directly,
  not through `Admit`, which per the shared design's item 6 supersedes
  every write already in flight; a write that completes after the fence
  is ignored by the writer (no durable generation recorded, nothing
  re-issued). A tab's fence lasts until its next edit, whose writes are
  issued after the discard and so are newer than it; the vault fence
  lasts until the teardown completes. The fence is one of three locks on
  "nothing is issued after the discard" — with `Quiesce()`'s suspension
  (and, after the teardown, the closed coordinator) and the operation's
  start-time re-read of a clean or fenced tab — while a write already in
  flight is the store's to drop (item 6). A timed-out flush therefore
  cannot bring a discarded buffer back.
- **Weak roots (item 7).** The writer and the restore read
  `RootIdentity().Persistent` once per session. Under (a): the first
  write's typed refusal stops the writer issuing more for the session
  (it records the reason; no write is re-issued), `FlushLatest` returns
  `Failed(reason)` at once with core's sentence, so application close and
  switch keep today's prompt with that reason, and the restore reads
  nothing. Under (b): the host writes and restores exactly as for a
  persistent root; the key and the admission check are core's. No
  host-side key is ever derived from the weak identity.
- **Restoring, injected into the tabs the layout builds.**
  `VaultLifecycleViewModel` reads `session.ReadHotExitBuffers()` (the FFI
  name per the design) after the open scan and before `:1184`, and passes
  `IReadOnlyList<RestoredBuffer>` into the `WorkspaceViewModel`
  constructor as a new parameter. The constructor stores it and
  `Restore(_persistence.Load())` at `:2309` runs as today, but
  `RestoreNode` (`Persistence.cs:69-84`) consults it when it constructs
  each `WorkspaceTabViewModel`: a tab whose path (or identity, for a
  moved file) has a recovery entry is constructed with
  `RestoreUnsaved(text, baselineHash, changedOnDisk)` (new internal
  method on the tab) applied before `AttachTabDocumentsIfNeeded` (`:83`)
  and before `group.RestoreActive` (`:90`), so the activated, published
  tab is the dirty one. Entries no restored tab consumed are opened as
  new tabs in the active group after `Restore` returns and before
  `SyncPanels()` (`:2310`). `WorkspaceReady` (`:1280`) fires after; the
  host posts `UnsavedNotesRestored` once, after it. A restored tab takes
  `IsTransient = false` and `IsDirty = true` through the existing private
  setter; the single launch landing lands on the restored active tab.
- **Disk changed.** `changedOnDisk` sets `IsExternallyStale`, so the next
  `SaveAll`/Ctrl+S runs the existing conflict path; no new conflict code.
- Deliberately not changed: the Close Vault prompt; the save path;
  `WorkspacePersistence`; `WorkspaceSaveCoordinator` (WB-2's `Admit` is
  used as it ships); no new core module; no Windows-only store.

## Tests

xUnit `Noun.VerbPhrase`, in `SlateWindows.Tests/HotExitTests.cs` (new
class `HotExitTests`). Unless a row says otherwise the store is reached
through a host write seam over the FFI that can hold a `write_buffer` in
flight (its start observed by an asserted `Wait(2 s)`) and release it.

| Fact | Asserts | Mutation |
|---|---|---|
| `ATypedEditIsWrittenToTheStoreAfterTheDebounce` | after an edit and the timer, the store holds the text and baseline tagged with the generation | drop the write |
| `ACleanTabDiscardsItsBuffer` | save, or undo to clean: buffer gone | keep it |
| `ClosingTheWindowWithDirtyTabsFlushesThenNeverAsks` | an edit made after the last debounce, and a second edit the fact types from inside the flush's pumped wait (through the write seam's hold callback); `PrepareForApplicationClose()` true, `_confirmUnsavedClose` never called, the store holds the final generation, file bytes unchanged | return `Closed` without flushing; or no re-read after the pumped wait (the second edit's generation is not in the store) |
| `AFailedFlushFallsBackToThePromptAndDiscardsNothing` | the store's write seam returns `Failed` (disk full) → the prompt is invoked; Cancel keeps the window and the buffer in memory | return `Closed` on failure |
| `AFlushThatTimesOutFallsBackToThePrompt` | the write seam never completes; the flush's bounded wait reports the timeout (asserted), `AbandonInFlight()` released the ticket (`SavesIdle` true), the prompt follows; release in `finally` | treat timeout as flushed |
| `AWriteOverTheStoresCapFallsBackToThePrompt` | the cap refusal → prompt | return `Closed` on the refusal |
| `CloseVaultStillAsksAndDiscardAllDropsTheBuffers` | a write held in flight when Close Vault starts; the prompt is invoked while it is held (`Quiesce()` abandoned it); Discard All calls `discard_vault()` through `FenceAndDiscardVault()`; the fact releases the held write from `WorkspaceSaveCoordinator.BeforeJoinForTests`, inside the join, after `discard_vault()`; `read_buffers()` is then empty | skip the discard (the late write's buffer is restored); or a store whose `discard_vault()` does not supersede an older in-flight write (the late write re-creates the buffer) |
| `ATimedOutFlushThenDiscardAllLeavesNothingToRestore` (Theory: application close, vault switch) | an edit after the last debounce; the route's `FlushLatest` admits its write and the store's write seam holds that `write_buffer` in flight; the flush times out (asserted) and abandons it; the fallback prompt answers Discard All; `discard_vault()` ran after the fence, and no `write_buffer` is issued after it; the held write is released from `BeforeJoinForTests` and completes late (its completion observed through an asserted `Wait(2 s)`, released in `finally`); reopening the vault over the same store root reads no buffers, builds no dirty tab and posts no `UnsavedNotesRestored` | the fallback route's Discard All without `discard_vault()` (the late write's buffer is restored); or a store whose discard does not supersede an older in-flight write (the late write re-creates the buffer); or the timeout treated as `Flushed` (no prompt is invoked) |
| `ALateWriteAfterACleanTransitionDoesNotResurrectTheBuffer` | a `write_buffer` held in flight; the reader undoes the tab back to its saved text (clean without a save — a save would wait behind the write's ticket), so `FenceAndDiscard(tab)` runs while the write is held; the held write is released and completes late (asserted `Wait(2 s)`, released in `finally`); reopening restores nothing for that path and the tab opens clean | the clean transition without `discard_buffer` (the late write's buffer is restored); or a store whose `discard_buffer` does not supersede an older in-flight write (the late write re-creates the buffer) |
| `ASaveAdmittedWhileAWriteIsInFlightWaitsForItsDurableCompletion` (new) | write first, save second: a `write_buffer` held in flight; Ctrl+S on the tab; the save's `SaveWriteHookForTests` is not reached while the write is held (asserted `Wait(1 s)` false); the write is released (in `finally`); then the save writes and publishes; the recorded order is write durable → save write → save publication → `FenceAndDiscard(tab)`'s discard; reopening restores nothing | complete the operation's Task when `write_buffer` is called rather than when its result publishes (the save starts while the write is held); or the round-8 shape — a `HasAdmittedSaveFor` check and an unadmitted write (the save starts at once) |
| `AWriteAdmittedBehindASaveWaitsForItsPublicationAndCarriesItsHash` (new; replaces `AWriteNeverInterleavesWithAnAdmittedSave`) | save first, write second: a save parked at `SaveWriteHookForTests`; an edit and the tick admit a write; no `write_buffer` is called while the save is parked (asserted); the save is released (in `finally`) and publishes; the write is then issued with the tab's new `SavedContentHash` as its baseline | snapshot the baseline at the tick rather than when the operation starts (the old hash); or write without `Admit` (the write lands while the save is parked) |
| `TwoGenerationsOfOnePathNeverOverlap` (new) | generation N's write held in flight; an edit makes N + 1 and its tick admits a second write; the seam sees no second `write_buffer` while N is held (call count 1, asserted); N is released (in `finally`); N + 1 is issued and lands; the store holds N + 1's text and the recorded durable generation is N + 1 | issue on the tick without `Admit` (the seam completes N after N + 1 → the store holds N's text) |
| `AnAbandonedWriteNeverLandsAfterANewerGeneration` (new) | generation N's debounce write held; application close abandons it (`Quiesce()`) and its `FlushLatest` times out waiting for it (asserted); the prompt answers Cancel (`Refused`, `Resume()`); an edit makes N + 1, and while N's abandoned worker is held its tick admits nothing — no second `write_buffer` call and `SavesIdle` true, so a Ctrl+S meanwhile reaches `SaveWriteHookForTests` at once (asserted); N is released (in `finally`); then N + 1 is admitted and lands; the store holds N + 1's text | admit the next write without waiting for the abandoned worker (the seam completes N after N + 1 → the store holds N's text) |
| `ATeardownNeverWaitsOnARecoveryWrite` (new; Theory: Close Vault, application close, vault switch) | a debounce write held in flight when the route starts; a dispatcher timer the fact arms releases the hold after 3 s and records when; Close Vault's prompt (answered Cancel) is invoked before that release with `SavesIdle` true; application close's and switch's `FlushLatest` is entered before that release (the recorded order), admits its write only after the abandoned worker finished, and returns `Flushed` | no `Quiesce()` before the round loop (`SettleSaves()` pumps until the timer releases the write; the prompt or the flush comes after the release) |
| `ReopeningRestoresTheBuffersIntoTheTabsTheLayoutBuilds` | the tab instances in `Groups` after construction are dirty with the text; the active tab is the restored one; exactly one landing; `WorkspaceReady` after install (order recorded by a seam) | install into pre-built tabs |
| `ARecoveryEntryWithNoLayoutTabOpensANewDirtyTab` | a new tab in the active group, dirty | drop the entry |
| `ABufferWhoseFileChangedOnDiskRestoresDirtyAndStale` | `IsExternallyStale`; the next save takes the conflict path | clear the flag |
| `ABufferWhoseFileMovedReseatsOnItsNewPath` | identity match → tab on the new path | seat on the old path |
| `ARestoreSpeaksCoresSentenceOnce` | `UnsavedNotesRestored` posted once with the count | post per tab |
| `AWeakRootsBufferIsNeverRestoredIntoAnotherFolderAtItsPath` (new; item 7, R1) | through the real core store over a temp store root, with WB-28's `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` set in-process to the fixture's unique folder name `T` (cleared in `finally`): the vault `T\vault` opens with `RootIdentity().Persistent` false (asserted first); an edit to `Notes/a.md` reaches the store (b) or is refused with core's reason (a), observed; the session is disposed with no teardown route (the crash shape); `T\vault` is deleted and a different vault is created at the same path with its own `Notes/a.md` bytes; opening it builds no dirty tab, posts no `UnsavedNotesRestored`, and a tab on `Notes/a.md` shows the new vault's bytes | store a weak root's buffer under its path and admit it without the design's check (the buffer is restored into the other vault) |
| `AWeakRootRenamedWhileClosedIsNotRestoredByItsIndex` (new; item 7, R2) | the same seam and store; `T\a` opens weak (asserted); an edit to `Notes/a.md`; the crash shape; `T\a` is renamed to `T\b` with Slate closed (the NTFS index survives the rename, as a FAT one may not); opening `T\b` restores nothing; then `T\b` is renamed back to `T\a` and opened: under (b) the buffer is restored dirty with its text (the visit to `T\b` did not delete it); under (a) nothing is restored and the store holds nothing for either spelling | key a weak root's buffer by its weak identity (opening `T\b` restores it); or, under (b), prune an entry the open did not admit (renamed back, nothing restores) |
| `A11yCorpusCensus` (existing) | PR 29's events present | — |
| FlaUI `ShellAccessibilityTests.HotExit_ClosingAndReopeningKeepsAnUnsavedNote` (new partial file `ShellAccessibilityTests.HotExit.cs`) | type, close the window, relaunch, the tab is dirty with the text; NVDA log carries the restore sentence | — |
| existing open-order pins in `RescanTests` updated where the constructor gained a parameter | | |

Every `Wait(timeout)` in these facts asserts its boolean result and
releases its event in `finally`. A held write that a teardown's
`CloseAndJoinWorkers` would join is released from
`WorkspaceSaveCoordinator.BeforeJoinForTests`, never after the
teardown returns (the join has no bound).

## Docs and registers

- Contract 38 D-10: append a dated amendment (closing never asks on both
  hosts once the latest edits are durable; a failed flush falls back to
  the prompt; Close Vault still asks; every Discard, the fallback
  prompt's included, fences pending recovery writes before it discards;
  a recovery write is an admitted operation on its file's chain until it
  is durable, and a teardown abandons, never waits on, one still in
  flight; a weak root follows the design's item 7).
- `18_windows_port/w_c_matrix.md`: the Workspace tabs row's notification
  column gains the restore sentence; `18_windows_port/parity_matrix.md`:
  `python -I scripts/generate-parity-matrix.py --check`.
- `docs/plans/18_windows_port/a11y_trigger_designations.json` rows for the new events (PR 29
  adds the census rows); `python -I scripts/a11y_trigger_ledger.py --write`.
- Help docs: if `docs/help/` names the close prompt, update the sentence.
- PR body: `Refs #1525` (or `Closes #1525` if the mac half is already
  merged and field-passed), the NVDA phrases, "not delivered" items.

## Verification

PowerShell, repo root. Every filter is listed first and the block throws
unless each required fact is discovered under it; every command's
`$LASTEXITCODE` is checked (no output grepping for "Failed").

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb29'
function Assert-Ok([string] $What) { if ($LASTEXITCODE -ne 0) { throw "$What exited $LASTEXITCODE" } }
./apps/slate-windows/generate-bindings.ps1 -Locked; Assert-Ok 'generate-bindings'   # PR 29's FFI
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release; Assert-Ok 'dotnet build'
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
$filter = 'FullyQualifiedName~SlateWindows.Tests.HotExitTests|FullyQualifiedName~SlateWindows.Tests.RescanTests|FullyQualifiedName~SlateWindows.Tests.Censuses.A11yCorpusCensus'
$names = @(dotnet test $tests --no-build --configuration Release --list-tests --filter $filter | Select-String '^\s{4}\S' | ForEach-Object { ($_.Line.Trim() -split '\(', 2)[0] }); Assert-Ok 'test discovery'
$required = @('ATypedEditIsWrittenToTheStoreAfterTheDebounce', 'ACleanTabDiscardsItsBuffer', 'ClosingTheWindowWithDirtyTabsFlushesThenNeverAsks', 'AFailedFlushFallsBackToThePromptAndDiscardsNothing', 'AFlushThatTimesOutFallsBackToThePrompt', 'AWriteOverTheStoresCapFallsBackToThePrompt', 'CloseVaultStillAsksAndDiscardAllDropsTheBuffers', 'ATimedOutFlushThenDiscardAllLeavesNothingToRestore', 'ALateWriteAfterACleanTransitionDoesNotResurrectTheBuffer', 'ASaveAdmittedWhileAWriteIsInFlightWaitsForItsDurableCompletion', 'AWriteAdmittedBehindASaveWaitsForItsPublicationAndCarriesItsHash', 'TwoGenerationsOfOnePathNeverOverlap', 'AnAbandonedWriteNeverLandsAfterANewerGeneration', 'ATeardownNeverWaitsOnARecoveryWrite', 'ReopeningRestoresTheBuffersIntoTheTabsTheLayoutBuilds', 'ARecoveryEntryWithNoLayoutTabOpensANewDirtyTab', 'ABufferWhoseFileChangedOnDiskRestoresDirtyAndStale', 'ABufferWhoseFileMovedReseatsOnItsNewPath', 'ARestoreSpeaksCoresSentenceOnce', 'AWeakRootsBufferIsNeverRestoredIntoAnotherFolderAtItsPath', 'AWeakRootRenamedWhileClosedIsNotRestoredByItsIndex') | ForEach-Object { "SlateWindows.Tests.HotExitTests.$_" }
$missing = @($required | Where-Object { $names -notcontains $_ })
if ($missing.Count -gt 0) { throw "the filter does not select: $($missing -join ', ')" }
if (-not ($names | Where-Object { $_ -like 'SlateWindows.Tests.Censuses.A11yCorpusCensus.*' })) { throw 'no A11yCorpusCensus fact discovered' }
dotnet test $tests --no-build --configuration Release --filter $filter; Assert-Ok 'HotExitTests, RescanTests, A11yCorpusCensus'
# NVDA closed, desktop free (the FlaUI facts live in the partial class ShellAccessibilityTests, so select by method name):
$at = 'apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj'
$atFilter = 'FullyQualifiedName~ShellAccessibilityTests.HotExit_ClosingAndReopeningKeepsAnUnsavedNote'
$atNames = @(dotnet test $at --no-build --configuration Release --list-tests --filter $atFilter | Select-String '^\s{4}\S'); Assert-Ok 'FlaUI discovery'
if ($atNames.Count -lt 1) { throw "the FlaUI filter selects nothing: $atFilter" }
dotnet test $at --no-build --configuration Release --filter $atFilter; Assert-Ok 'FlaUI journey'
$cs = @('apps/slate-windows/src/SlateWindows/MainWindow.xaml.cs','apps/slate-windows/src/SlateWindows/VaultLifecycleViewModel.cs','apps/slate-windows/src/SlateWindows/WorkspaceViewModel.cs','apps/slate-windows/src/SlateWindows/WorkspaceViewModel.Persistence.cs','apps/slate-windows/src/SlateWindows/WorkspaceViewModel.HotExit.cs','apps/slate-windows/tests/SlateWindows.Tests/HotExitTests.cs','apps/slate-windows/tests/SlateWindows.Tests/RescanTests.cs','apps/slate-windows/tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.HotExit.cs')
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs; Assert-Ok 'dotnet format'
python -I scripts/generate-parity-matrix.py --check; Assert-Ok 'parity matrix check'
python -I scripts/a11y_trigger_ledger.py --write; Assert-Ok 'trigger ledger'
python -I scripts/a11y_trigger_ledger.py --check; Assert-Ok 'trigger ledger check'
```

No Rust changes here (the store is PR 29's), so no cargo gates beyond the
binding regeneration. Mutation run per row, locally (the weak-root
rows under the policy the design chose).

CI: `windows.yml` all lanes (FlaUI journey added). AT: NVDA full pass,
launched with `SLATE_LOCAL_APP_DATA` on a seeded root (§4 rule 4): (1)
type in a note, Alt+F4 — no dialog, the window closes; (2) relaunch and
open the vault — "Restored 1 unsaved note" (core's exact phrase) after
the vault-opened sentence, the dirty marker in the tab title, the caret
in the restored note; (3) change the file outside Slate first, then
reopen — the stale notice and, on Ctrl+S, the conflict sentence; (4)
Close Vault with a dirty tab — the prompt as today; (5) make the store
root read-only, type, Alt+F4 — the prompt with the reason in the status
line; (6) launched with `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` set
to a scratch vault's folder name (a weak root), type, Alt+F4 — under
item 7 (a) the prompt with core's weak-root reason in the status line,
under (b) no dialog and the relaunch restores as in (2). Recorded in the
PR body; the PR stays a draft until then.

## Acceptance

- [ ] WD20 recorded; PR 29's design and core store merged with green
      combined-head CI; WB-28 merged; WB-2's `Admit` on `main`.
- [ ] The twenty-one `HotExitTests` facts, an `A11yCorpusCensus` fact
      and the FlaUI journey pass, seen failing first.
- [ ] Application close and switch flush then never prompt; a failed or
      timed-out flush falls back to the prompt; Close Vault prompts;
      every Discard All (Close Vault's and the fallback prompt's) and every
      clean transition discards through the fence, so a write still in
      flight that completes after the discard restores nothing on reopen
      (timeout → Discard → late completion → reopen, on both routes).
- [ ] Every recovery write is a WB-2 `Admit` operation whose ticket
      completes at durable completion: a save admitted after it waits
      for it, it waits for a save admitted before it and writes that
      save's hash, two generations of one path never overlap, and the
      writer never consults `HasAdmittedSaveFor`. Every teardown route
      quiesces and abandons in-flight recovery writes before its round
      loop, so no route waits on one, and an abandoned write never lands
      after a newer generation.
- [ ] The shared design names how its discard supersedes an older
      in-flight write (its item 6) and its weak-root key and restore
      admission (its item 7: (a) or (b), with R1–R3); if it does not name
      both, this chunk does not start. The two weak-root facts pass under
      the policy it chose.
- [ ] Restore injected into `RestoreNode`'s tabs (the instances in
      `Groups` are the dirty ones); one landing; `WorkspaceReady` after.
- [ ] Registers: contract 38 amendment, matrices, ledger; NVDA pass
      (six scenarios) recorded; `Refs`/`Closes #1525` per the closure
      matrix.
- [ ] Not delivered: anything the design deferred (named).

## Brief

Give Windows hot exit over the core store PR 29 ships. Pending decision:
WD20 (recommended: one shared design that also fixes the durable key and
restore admission for a weak root, core-owned store, start only after
its implementation is on `main`; else a Windows-only `HotExitStore.cs`
mirroring the design's schema, key and weak-root policy, with a
parity-matrix debt). Read PR 29's design and core PR first, WB-2's spec
(`Admit`) and WB-28's design items 6 and 7 (the weak identity); then
`MainWindow.xaml.cs:1982-1992`, `VaultLifecycleViewModel.cs:44-55`
(`TeardownRoute`), `:76-82`, `:210-216`, `:436-460` (the switch's
teardown at `:454`), `:598-650` (Close Vault's at `:605`, application
close's at `:644`), `:1176-1192`, `:1272-1282`, `:1405-1575` (the round
loop `:1505-1569`, its settle `:1510-1516`, the Discard check
`:1562-1566`), `WorkspaceViewModel.cs:94`, `:138`, `:239-241`,
`:430-452`, `:880-885` (the save seams), `:1827`, `:2103`, `:2302-2316`,
`:2753`, `:2813-2840`, `:3089-3097` (`Dispose` joins the workers),
`WorkspaceViewModel.Persistence.cs:53-108`,
`WorkspaceViewModel.Layout.cs:54`, `WorkspaceSaveCoordinator.cs:39-62`
(`IsIdle`, `BeforeJoinForTests`), `:83-131` (`HasAdmittedSaveFor`
reserves nothing; `Enqueue`), `:189-240` (`TrackWorker`,
`SettlePumping` without a bound, `CloseAndJoinWorkers`), `:323-353`
(`PumpedWait.Until`, no timeout), `WorkspacePersistence.cs:98`,
`:168-214`, `FileRecentsStore.cs:22-37`. Before starting, confirm the
shared design fixed: the store (core-owned FFI `write_buffer(path, text,
baseline_hash)` with a durable result, `read_buffers()`,
`discard_buffer(path)`, `discard_vault()`, keyed by a persistent
`VaultRootIdentity`, capped with a typed refusal), the cadence and fsync
policy, the restore rule, the open-sequence ordering and core's
`UnsavedNotesRestored` copy, the interlocks, item 6 — its discard
supersedes every write issued before it, including one completing later
(e.g. a durable per-vault discard epoch `write_buffer` carries) — and
item 7: a weak root (`persistent: false`; a FAT index changes on a
rename or move made with Slate closed and a freed slot's index can be
taken by another folder, even one later at the same path) keys nothing
by its weak identity, and the design names either (a) no hot exit on a
weak root — `write_buffer` refused with a typed refusal carrying core's
reason, nothing read back, application close and switch falling back to
today's prompt with that reason — or (b) a path key plus a named
restore-admission check, an entry no open admits kept and handled as the
design states; with (R1) never restored into another folder, (R2) a
renamed weak root never losing a promised buffer without the design
saying what happens, (R3) index reuse proven in core's facts through a
store seam with an explicit weak identity. If either item is missing,
stop and raise it — this chunk does not start.

Build: `HotExitWriter` (`WorkspaceViewModel.HotExit.cs`, new partial):
per-tab debounce; on the tick `_saves.Admit(tab.Path, ticket =>
WriteOperation(tab))` (WB-2's `Task<bool> Admit(string path,
Func<SaveTicket, Task> operation)`), where the new private
`WriteOperation` re-reads the tab when it starts (disposed, clean or
fenced → nothing), snapshots `(text, SavedContentHash,
ContentGeneration)` then, calls `write_buffer` on a worker handed to
`TrackWorker`, and returns a `TaskCompletionSource` Task completed when
the result is published on the dispatcher (durable generation recorded,
or the failure) — so the ticket reserves the path until durable
completion both ways, and a path's writes never overlap; the writer
never calls `HasAdmittedSaveFor`. `TryCloseWorkspace(TeardownRoute
route)` and `TryCloseWorkspaceCore(TeardownRoute route)` gain the route
(callers `:454` Switch, `:605` Close, `:644` ApplicationClose); before
the round loop every route calls `HotExitWriter.Quiesce()` (new: stop
the timers, suspend scheduling, then `AbandonInFlight()` (new): complete
each outstanding operation's Task so its ticket releases, mark it
abandoned so its late result records nothing, keep its worker tracked
for the join, and admit no new write on that path until that worker has
finished — a tick defers, `FlushLatest` waits inside its bound, a
deferred admission re-checks suspension, fence and dirtiness, and a
timed-out flush admits nothing later — so no ticket is held while the
writer waits and a save never waits on an abandoned write), and every
later `Refused` return calls
`HotExitWriter.Resume()` (new: re-arm the timers of dirty tabs whose
generation exceeds the durable one). Where the prompt would open,
application close and switch call `FlushLatest(timeout: 5 s) ->
FlushResult { Flushed, Failed(reason) }` (new; `PumpedWait.Until` over
`Task.WhenAny(flushed, Task.Delay(timeout))`; admits a write per
outstanding generation, re-reads after the frame and admits again for
generations bumped meanwhile; a timeout calls `AbandonInFlight()`
first): `Closed` without a prompt on `Flushed`, today's prompt with the
reason on `Failed`; Close Vault's prompt unchanged and unflushed. The
discard fence: every Discard All (Close Vault's, and the prompt
application close and vault switch fall back to) runs
`HotExitWriter.FenceAndDiscardVault()` (new) and a clean transition runs
`HotExitWriter.FenceAndDiscard(tab)` (new): stop the scope's timers and
mark it fenced so no new `write_buffer` is issued, then call
`discard_vault()` / `discard_buffer(path)` directly (not admitted), and
ignore any write that completes after the fence. Weak roots: under item
7 (a) the first refusal stops the session's writes and `FlushLatest`
returns `Failed` with core's reason at once; under (b) nothing
host-side differs; no host key is ever derived from the weak identity.
The recovery read in `VaultLifecycleViewModel` before `:1184` passed
into the `WorkspaceViewModel` constructor; `RestoreNode` applying
`WorkspaceTabViewModel.RestoreUnsaved(text, baselineHash,
changedOnDisk)` (new) to the tabs it constructs before attach and
activation; leftover entries opened as new dirty tabs;
`UnsavedNotesRestored` posted once after `WorkspaceReady`.

Facts in `HotExitTests`, seen failing first (mutations in brackets):
`ATypedEditIsWrittenToTheStoreAfterTheDebounce` [drop the write];
`ACleanTabDiscardsItsBuffer` [keep it];
`ClosingTheWindowWithDirtyTabsFlushesThenNeverAsks` (an edit after the
last debounce and one typed inside the flush's pumped wait are durable
before close, no prompt) [close without flushing; no re-read after the
frame]; `AFailedFlushFallsBackToThePromptAndDiscardsNothing` [`Closed`
on failure]; `AFlushThatTimesOutFallsBackToThePrompt` (the timeout
asserted, the ticket released by `AbandonInFlight()`, release in
`finally`) [timeout as flushed]; `AWriteOverTheStoresCapFallsBackToThePrompt`
[`Closed` on the refusal]; `CloseVaultStillAsksAndDiscardAllDropsTheBuffers`
(a write held when Close Vault starts; the prompt opens while it is
held; Discard All through `FenceAndDiscardVault()`; the held write
released from `WorkspaceSaveCoordinator.BeforeJoinForTests` inside the
join leaves no buffer) [skip the discard; a store `discard_vault()` that
does not supersede the older write];
`ATimedOutFlushThenDiscardAllLeavesNothingToRestore` (a Theory over
application close and vault switch: the flush's own write held makes
`FlushLatest` time out, asserted, and is abandoned; the fallback
prompt's Discard All runs `discard_vault()` after the fence and no
`write_buffer` follows; the held write, released from
`BeforeJoinForTests`, completes late, asserted `Wait(2 s)` released in
`finally`; reopening reads no buffers, builds no dirty tab and speaks no
restore) [no `discard_vault()` on the fallback route; a store discard
that does not supersede the older write; the timeout treated as
`Flushed`];
`ALateWriteAfterACleanTransitionDoesNotResurrectTheBuffer` (a held write;
the reader undoes to the saved text — a save would wait behind the
write — so `FenceAndDiscard(tab)` runs while it is held; it completes
late; reopening restores nothing) [the clean transition without
`discard_buffer`; a store `discard_buffer` that does not supersede the
older write];
`ASaveAdmittedWhileAWriteIsInFlightWaitsForItsDurableCompletion` (write
first: Ctrl+S during a held write never reaches `SaveWriteHookForTests`
until the write is released; order write durable → save write → save
publication → discard) [complete the Task when `write_buffer` is called;
a `HasAdmittedSaveFor` check with an unadmitted write];
`AWriteAdmittedBehindASaveWaitsForItsPublicationAndCarriesItsHash` (save
first: no `write_buffer` while the save is parked; then the write
carries the published hash) [baseline snapshotted at the tick; write
without `Admit`]; `TwoGenerationsOfOnePathNeverOverlap` (N held, N + 1
admitted, one call until N is released; the store ends at N + 1) [issue
without `Admit`]; `AnAbandonedWriteNeverLandsAfterANewerGeneration` (the
teardown abandons N and its flush times out; Cancel; while N's worker is
held the N + 1 tick admits nothing and a Ctrl+S is not blocked; the
store ends at N + 1) [admit without waiting for the abandoned worker];
`ATeardownNeverWaitsOnARecoveryWrite` (a Theory over Close Vault,
application close and vault switch: with a debounce write held, Close
Vault's prompt opens with `SavesIdle` true, and the flush is entered,
before a 3 s timer releases it; the flush then returns `Flushed`) [no
`Quiesce()`];
`ReopeningRestoresTheBuffersIntoTheTabsTheLayoutBuilds` (the `Groups`
instances are dirty; one landing; `WorkspaceReady` after) [install into
pre-built tabs]; `ARecoveryEntryWithNoLayoutTabOpensANewDirtyTab` [drop
the entry]; `ABufferWhoseFileChangedOnDiskRestoresDirtyAndStale` [clear
the flag]; `ABufferWhoseFileMovedReseatsOnItsNewPath` [seat on the old
path]; `ARestoreSpeaksCoresSentenceOnce` [post per tab];
`AWeakRootsBufferIsNeverRestoredIntoAnotherFolderAtItsPath` (real core
store; `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` set in-process to the
fixture's unique folder name, cleared in `finally`; the vault opens weak,
asserted; an edit reaches the store (b) or is refused (a); the crash
shape; a different vault created at the same path restores nothing and
shows its own bytes) [store under the path without the design's check];
`AWeakRootRenamedWhileClosedIsNotRestoredByItsIndex` (the same seam; the
crash shape; renamed with Slate closed, the new name restores nothing;
renamed back, (b) restores the buffer dirty, (a) restores nothing and
the store holds nothing) [key by the weak identity; under (b) prune an
unadmitted entry]; the FlaUI journey
`ShellAccessibilityTests.HotExit_ClosingAndReopeningKeepsAnUnsavedNote`.
Every `Wait(timeout)` asserts its result and releases in `finally`; a
held write a teardown would join is released from `BeforeJoinForTests`.
Registers: contract 38 D-10 amendment (including the admitted recovery
write, the teardown's abandonment and the weak-root rule),
`w_c_matrix.md` Workspace tabs row, `parity_matrix.md` (`--check`),
`docs/plans/18_windows_port/a11y_trigger_designations.json` and the
ledger (`--write`, then `--check`). Not delivered: whatever the design
defers. Done when the Verification block runs clean in PowerShell (the
twenty-one `HotExitTests` facts and an `A11yCorpusCensus` fact
discovered by name, the FlaUI journey discovered, every `$LASTEXITCODE`
0), the Windows lanes are green, each mutation was run locally (the
weak-root rows under the chosen policy), the six-scenario NVDA pass
(launched with `SLATE_LOCAL_APP_DATA` on a seeded root: (1) type,
Alt+F4 — no dialog; (2) relaunch and open — "Restored 1 unsaved note"
after the vault-opened sentence, the dirty marker, the caret in the
note; (3) the file changed outside Slate first — the stale notice and,
on Ctrl+S, the conflict sentence; (4) Close Vault with a dirty tab — the
prompt; (5) a read-only store root — the prompt with the reason in the
status line; (6) a weak root through
`SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` — under (a) the prompt with
core's reason, under (b) as (1) and (2)) is in the PR body, and the
issue reference follows the closure matrix.
Commit:
`feat(windows): unsaved notes survive closing, quitting and a crash and come back unsaved when the vault reopens`.
