# WB-2 — A History restore, a template reload and a property write join an admitted save of the same note instead of racing it

Issues: Closes #1322, Closes #1325. Size S–M. Lanes: windows. AT: none (no speech changes). Wave: W1. Depends on: nothing. Parallel with: WB-1, WB-3. Decision: WD2 (pending; recommended "wait — join the admitted save, then act — and yes, #1325 ships"; under "refuse": the three sites speak "Wait for the current save to finish." and do nothing; under "no" to #1325: its site and facts are dropped).

## Goal

Since #1280 a save's write runs off the dispatcher, and #1309 made the two
re-seats save-aware. Three paths still act on a note while its save is
admitted: the History restore's reload, a template's same-path reload, and
a property write from a peer tab. After this chunk each of them waits for
the admitted save to finish and then acts on the saved bytes: no reload
lands over an edit made during the write, no save publication is retired
by a reload's epoch bump ("Saved" is always spoken), and a property commit
in a split-pane peer never produces a conflict prompt for work the reader
did in the same window a moment apart.

## Current state on main

Verified against the worktree (the plan's and `report_rel.md`'s lines
hold).

- `WorkspaceSaveCoordinator.cs:83-87` `HasAdmittedSaveFor(path)`: true
  while the ticket recorded as the tail of the path's folded key
  (`PathKey`, `:74-75`) has not completed; dispatcher-only. `:94-131`
  `Enqueue(keys, start)` makes each new ticket the tail of every key it
  names (`:112-115`) and runs `start` on the dispatcher once every earlier
  save under any of the keys has completed — this is the join primitive
  the chunk reuses. `KeysFor(tab, path)` (`:68-69`) is the tab-plus-path
  key set a save uses. So while an operation admitted under the path
  runs, `HasAdmittedSaveFor(path)` is true because of that operation's
  own ticket, or of a save queued behind it.
- `WorkspaceViewModel.cs:3003-3034` `KeepsBufferOnReseat(tab, stored)`:
  the one save-aware rule (`tab.HasPendingSaves || _saves.HasAdmittedSaveFor(tab.Path) || _saves.HasAdmittedSaveFor(stored)`),
  used by the Created-event re-seat (`:2966`) and the rescan's
  (`WorkspaceViewModel.Rescan.cs:396`). `_saves` is the workspace's
  coordinator (`:2000-2001`); `SavesForTests` exposes it (`:2842`).
- The tab's save queue (`WorkspaceTabViewModel`, in `WorkspaceViewModel.cs`):
  `:126` `_queuedSave`, the admitted, not-yet-started save that later
  requests join while its `ItemEpoch` equals the tab's `_itemEpoch`
  (`SaveAsync`, `:769-807`); `:849-869` `SaveBatch`, whose `ItemEpoch`
  is init-only (`:853`); `:810` `HasPendingSaves` (the latest admitted
  save has not completed). `StartSave` retires a batch whose `ItemEpoch`
  is not the tab's `_itemEpoch` silently (`:914-918`, `ticket.Complete(false)`,
  nothing spoken) and publishes a clean tab's save with no write
  (`:919-925`). `ReplaceItem` (`:516-612`) bumps `_itemEpoch` (`:528`),
  so a same-item reload of a tab with a queued save retires that save.
- **#1322 site 1:** `WorkspaceViewModel.History.cs:255-269`
  `ReloadOpenTabFromDisk(path)`: `FindPathBackedTab(path)` and, when
  `IsDirty` is false, `tab.ReplaceItem(tab.Item)` — no admitted-save
  check. Its caller is the restore continuation at `:228`, after
  `_session.RestoreVersion(...)` ran on the history worker (`:177-198`,
  a CAS write with `request.ExpectedContentHash`). So the restore's own
  write also runs beside an in-flight save; WD2's "wait" has to cover the
  write, not only the reload. `ReloadOpenTabFromDiskForTests` (`:245-246`)
  is the existing seam and the only other caller. The stage-time rule
  (`RequestRestoreVersion`, `:125-169`) refuses a dirty tab with the
  D-14 line "Save the note before restoring a version. The editor has
  unsaved changes in {file}." (`:131-141`) and takes the expected hash
  from the tab's `SavedContentHash` (`:148-150`).
- **#1322 site 2:** `WorkspaceViewModel.Templates.cs:267-277`: after the
  create, every clean same-path Markdown tab whose text differs from
  `rendered.Body` (or is missing from disk) gets `ReplaceItem` — no
  admitted-save check.
- **#1325:** `WorkspaceViewModel.Properties.cs:38-50` the note-scoped
  write lease (`_propertyWritePaths`, `TryAcquirePropertyWriteLease`,
  `WithLeaseRelease`); dispatch sites `:205-215` (add), `:244-250`
  (retry add), `:412-421` and `:507-515` (row set/delete). Each calls
  `tab.WriteProperty(expectedHash, write, completion)`
  (`WorkspaceTabViewModel.Properties.cs:146-215`), which runs the core
  write on `Task.Run` with a hash check — never through `_saves`. Its
  publication re-baselines a clean tab in place (`:83-125`, no epoch
  change). Within one tab the race is closed by the dirty refusal pinned
  at `PropertiesLandingTests.cs:448`
  `ADayPickedWhileItsNoteSavesWritesNothingUntilTheSavePublishes`; a
  peer tab's save is not.
- Test seams: `WorkspaceViewModel.cs:880` `SaveWriteHookForTests` (runs
  on the save worker before the core write), `:885`
  `SaveWrittenHookForTests`; used by `RescanTests.FollowUpB.cs:83-121`
  `ARescanNeverReloadsOverAnAdmittedSave` (the reader takes the edit back
  while the write is parked, so the tab is clean with `HasPendingSaves`
  true) and `:130-172` `ARescansReseatKeepsATabWithAnAdmittedSave`, the
  twin shape for the new facts.

## Reference

No mac twin (mac has no off-dispatcher save chain). Contract 38 D-10
(`docs/plans/38_notification_dispatcher_contracts.md`): a reload must
never retire an admitted save's publication. Contract 40 R-9's
`KeepsBufferOnReseat` doc (`WorkspaceViewModel.cs:2978-3002`) is the rule
being extended.

## Design

WD2 recommends: "Wait (join the save's publication, then act), and yes:
both go through `WorkspaceSaveCoordinator.HasAdmittedSaveFor`." Under
that answer the sites join through `Admit` (item 1); `HasAdmittedSaveFor`
stays the test of whether some save is admitted — the "refuse" answer's
request-time check and the unadmitted reload's guard — and an admitted
operation never asks it about the path it holds, because its own ticket
makes it true. If the owner answers "refuse", the three sites instead speak one HostComposed
line, "Wait for the current save to finish.", and do nothing (the
structural commands' rescan posture) when `HasAdmittedSaveFor(path)` is
true at request time; an operation that is not refused is still admitted
through `Admit`, so the reverse-order facts, the admitted reload and the
queued-save carry below hold under both answers, and the facts that start
an operation under an admitted save keep their names and assert the
refusal instead of the deferred action. If the owner answers "no" to
#1325, its site and facts are dropped and the issue is closed as "won't
fix: nothing is lost today".

1. **New admission primitive (new):**
   `Task<bool> WorkspaceSaveCoordinator.Admit(string path, Func<SaveTicket, Task> operation)`
   — dispatcher-only. It ALWAYS enqueues a ticket under `PathKey(path)`
   through `Enqueue` (`:94-131`), idle chain included, so a save admitted
   after it waits for it exactly as it waits for an earlier save (the
   restore IS a save; the file's chain stays ordered in both directions).
   When the earlier saves have settled, `operation(ticket)` runs on the
   dispatcher; the ticket completes (`ticket.Complete(true)`, or
   `ticket.Fail`) only when the Task the operation returns completes —
   after the operation's dispatcher publication or its terminal failure,
   never when the call returns. The operation's worker Task is handed to
   `TrackWorker` (`:189-206`) so teardown joins it like a save's. A closed
   coordinator returns `false` and the operation never runs (the caller
   speaks nothing; the shell is closing). Because `Admit` runs an
   operation only after every save admitted before it has completed, the
   saves still pending on the path while the operation runs are its own
   ticket and its queued successors — never an earlier save.
2. **History restore** (`PerformRestore`, `History.cs:171-237`): the
   whole restore is the operation — the worker's `RestoreVersion`
   (`:177-198`) and the continuation (`:199-235`) — and the operation's
   Task completes at the end of that continuation. When the operation
   starts it re-applies the stage-time rules, because it may now start
   later than the stage: a tab that is dirty by then (the reader's edit
   made while the save wrote stays an unsaved change behind the saved
   bytes once the save publishes) gets the existing D-14 line
   (`:131-141`) once and nothing is written; otherwise the
   `ExpectedContentHash` is re-read from the tab's `SavedContentHash`, so
   a restore admitted behind a save lands on the saved bytes instead of
   conflicting with them.

   The continuation reloads through a new admission-aware reload,
   `private void ReloadOpenTabFromDiskAdmitted(string path, WorkspaceSaveCoordinator.SaveTicket admittedBy)`
   (new, `History.cs`), never through the guarded `ReloadOpenTabFromDisk`.
   Inside the operation `HasAdmittedSaveFor(path)` is always true — the
   restore's own ticket is the path's tail until the operation's Task
   completes, and a save queued meanwhile becomes the tail — and a tab
   with a queued save reports `HasPendingSaves`; a guard on either would
   skip the mandatory reload in both queue directions (codex round 6).
   The ticket parameter is the proof of admission: only `admittedBy` and
   its queued successors can be pending (item 1), and neither may block
   the reload. The admitted reload finds the tab as today; a dirty tab is
   not reloaded (unchanged: the buffer outranks the restore); otherwise
   it calls `WorkspaceTabViewModel.ReloadKeepingQueuedSave()` (new,
   `internal void ReloadKeepingQueuedSave()`, `WorkspaceViewModel.cs`),
   then `AttachTabDocumentsIfNeeded` and `ReloadCanvasDocumentAt` as
   today. `ReloadKeepingQueuedSave` is `ReplaceItem(Item)` — a same-item
   reload — followed by re-stamping the tab's queued, not-yet-started
   save (`_queuedSave`, `:126`, when its `ItemEpoch` was the pre-reload
   `_itemEpoch`) to the new item epoch through
   `SaveBatch.Rebase(int itemEpoch)` (new; `SaveBatch.ItemEpoch`, `:853`,
   gains a private setter). While the operation runs, a save of a tab on
   the path can only be queued behind it, never writing, so `_queuedSave`
   is the whole of the tab's successor; without the re-stamp `StartSave`'s
   epoch check (`:914-918`) would retire it silently — "Saved" never
   spoken, the D-10 failure this chunk closes. The successor then starts
   against the reloaded document: clean, it publishes `NoteSaved` with
   no write (`:919-925`); edited after the reload, it writes on the
   restored hash. A tab dirtied while the restore wrote is not reloaded,
   so its successor's CAS from the pre-restore hash raises
   `NoteSaveConflict` and the restore's bytes are never silently
   overwritten.

   The guarded `ReloadOpenTabFromDisk(path)` stays for every unadmitted
   caller (after this chunk, only `ReloadOpenTabFromDiskForTests`,
   `:245-246`) and gains the D-10 guard: a tab with `HasPendingSaves` or
   `HasAdmittedSaveFor(path)` keeps its buffer and `ItemIdentity`.
3. **Template same-path reload** (`Templates.cs:267-277`): the sweep is
   itself an `Admit(path, …)` operation whose Task is already complete
   when it returns (the reload is synchronous on the dispatcher); it
   re-checks `IsDirty` and the text difference at run time (the save may
   have made them equal) and reloads each matching tab through
   `ReloadKeepingQueuedSave()` — never a bare `ReplaceItem`, and never
   under a `HasAdmittedSaveFor` or `HasPendingSaves` guard: the
   template's own ticket is the path's tail while the sweep runs, and a
   save a tab queued between the template's admission and the deferred
   sweep is its successor, whose publication a bare `ReplaceItem` would
   retire (the restore's round-6 defect, here too). With no admitted save
   it runs in the same dispatcher turn as today.
4. **Property writes** (`Properties.cs:205`, `:244`, `:412`, `:507`):
   `DispatchRowWrite` and the add/retry dispatchers acquire the lease as
   today, then `Admit(path, ticket => …)` an operation that calls
   `tab.WriteProperty(expectedHash, write, completion)` with the expected
   hash read from the tab when the operation starts (the owner header's
   snapshot hash after the save's publication rebuilt it — `AwaitRepublished`
   in the landing tests shows the rebuild) and completes its Task from
   the wrapped completion (`WithLeaseRelease`, `:46-50`), so a save
   admitted behind the property write waits for the write's publication.
   `row.WriteInFlight` stays true across the wait so the row's gates
   refuse a second commit meanwhile. The property publication re-baselines
   in place (no epoch change), so it needs no queued-save carry. Not
   changed: the hash check, the conflict prompt, the lease, the one-tab
   dirty refusal pinned at `PropertiesLandingTests.cs:448`.

Not changed: `KeepsBufferOnReseat`, `HasAdmittedSaveFor`, the rescan
paths, mac, core, FFI.

## Tests

`SlateWindows.Tests`, new file `SaveChainAdmissionTests.cs` (class
`SaveChainAdmissionTests`; the harness of `RescanTests.FollowUpB.cs:83-121`
— file of the partial class `RescanTests`; park the save at
`SaveWriteHookForTests`; "takes the edit back" means setting `Text` to
the loaded text while the write is parked, as that fact does). Every hold
is observed: each `ManualResetEventSlim.Wait(TimeSpan)` result is
asserted (`Assert.True(parked.Wait(TimeSpan.FromSeconds(10)), "…never parked")`;
a timeout is the fact's failure, never a silent release) and every gate
is `Set()` in `finally`, the hook nulled there. Every save a fact
observes is requested with `SaveAsync(announce: true)`, so its
publication is a `NoteSaved` the fact can count.

- `AHistoryRestoreStartedUnderAnAdmittedSaveWaitsThenRestoresOverTheSavedBytes`
  — queue direction 1, a restore behind a save: the save parks; the
  reader takes the edit back (the tab clean, so the stage-time refusal
  passes), confirms a restore (admitted behind the save), then types the
  edit again; after release the save publishes first (`save.Result`
  true, `NoteSaved` once) and adopts the retyped text as the baseline, so
  the tab is clean when the restore starts; the restore writes on the
  saved hash and its continuation — run while the restore's own ticket is
  still the path's tail — reloads the tab: `ItemIdentity` advances
  exactly once and `Text` is the restored version; the saved bytes are a
  history version. Mutations: drop the `Admit` wrap → the restore runs
  first and the ordering assertions fail; call the guarded
  `ReloadOpenTabFromDisk` from the continuation → the restore's own
  ticket keeps `HasAdmittedSaveFor` true, the reload is skipped and
  `Text` stays the saved text.
- `AHistoryRestoreNeverSilencesTheSaveItWaitedFor` — the
  `RestoredVersionFrom` line follows the save's `NoteSaved`; no save
  publication was retired (`SettleSaves()` true; `ItemIdentity`
  unchanged until the restore's own reload).
- `AHistoryRestoreThatFindsItsTabDirtyWhenItStartsRefusesAndWritesNothing`
  — the first fact without the retyping: once the save publishes, the
  reader's taken-back text is an unsaved change, so the restore's start
  speaks the D-14 line once, writes nothing (the file holds the saved
  bytes; no `RestoredVersionFrom`), and leaves the buffer and
  `ItemIdentity`; its ticket completes, so a save requested afterwards
  writes. Mutation: drop the start-time re-check → the restore writes
  under the dirty tab and `RestoredVersionFrom` is spoken while the
  editor shows the reader's text.
- `ASaveStartedUnderAnAdmittedRestoreWaitsForTheRestoresPublication`
  — queue direction 2, a save behind a restore: the restore's worker
  parks at a new `HistoryRestoreHookForTests` (`internal Action? HistoryRestoreHookForTests { get; set; }`
  on the workspace, run on the history worker before `RestoreVersion`,
  the `SaveWriteHookForTests` shape); the clean tab requests a save
  meanwhile (a queued successor): `save.IsCompleted` stays false while
  the restore is parked; after release the continuation reloads the tab
  despite the successor (`ItemIdentity` advances once, `Text` is the
  restored version), then the successor publishes `NoteSaved` once,
  after `RestoredVersionFrom`, with `save.Result` true; the file holds
  the restore's bytes. Mutations: complete the ticket when `Admit`'s
  operation returns instead of when its Task completes → `NoteSaved`
  comes before `RestoredVersionFrom`; guard the admitted reload with
  `HasPendingSaves || HasAdmittedSaveFor` → the successor suppresses the
  reload and `Text` stays the pre-restore text; drop the `Rebase` → the
  reload's epoch bump retires the successor (`save.Result` false, no
  `NoteSaved`).
- `ADirtySaveQueuedBehindARestoreReportsAConflictAndOverwritesNothing`
  — queue direction 2 with an edit: the restore's worker parked, the
  reader edits and requests a save; after release the dirty tab is not
  reloaded (the edit stays, `IsDirty` true), and the successor's CAS
  from the pre-restore hash speaks `NoteSaveConflict` once
  (`save.Result` false); the file holds the restore's bytes. Mutation:
  drop the `IsDirty` check in the admitted reload → the edit is replaced
  by the restored text.
- `TheUnadmittedReloadStillKeepsATabWithAnAdmittedSave` — the guard kept
  for unadmitted callers: the save parks, the reader takes the edit back
  (clean, `HasPendingSaves` true), `ReloadOpenTabFromDiskForTests(path)`
  leaves `ItemIdentity` and `Text` unchanged; after release the save
  publishes `NoteSaved` once. Mutation: drop the D-10 guard from
  `ReloadOpenTabFromDisk` → `ItemIdentity` changes and the save's
  publication is retired (no `NoteSaved`).
- `ATemplatesSamePathReloadWaitsForTheNotesAdmittedSave` — a clean
  same-path tab with a parked save keeps its buffer and `ItemIdentity`
  through the template create; a save that tab requests while the sweep
  waits is queued behind it; after release the tab reloads once and that
  queued save still publishes `NoteSaved` once. Mutations: remove the
  admission → `ItemIdentity` changes under the save; a bare `ReplaceItem`
  in the sweep → the queued save is retired (no `NoteSaved`).
- `APropertyCommitInAPeerTabWaitsForTheOtherTabsSaveAndWritesOnce` (#1325)
  — split pane, tab B's save parked, tab A commits a property: no write
  until release; after it, one property write on top of the saved bytes,
  no `PropertyEditConflict`, no conflict dialog; the file holds both the
  edit and the property. Mutation: call `WriteProperty` directly → the
  conflict prompt fires.
- `ASaveStartedUnderAnAdmittedPropertyWriteWaitsForItsCompletion` (#1325)
  — the property write's worker is parked at `PropertyWriteFaultForTests`'s
  neighbour seam (a new `internal Action? PropertyWriteHookForTests { get; set; }`
  on `WorkspaceTabViewModel`, run on the worker before the core write);
  a save of the same note requested meanwhile writes only after the
  property completion ran; one write each, nothing lost. Mutation:
  complete the ticket when the operation returns → the save writes
  before the property completion.
- `TeardownJoinsAnAdmittedRestoreAndPropertyWrite` — dispose the
  workspace with each operation parked: `Dispose` waits for the tracked
  worker (contract 35 A-1) and the session is not disposed under it.
  Mutation: skip `TrackWorker` for the operation → the join returns
  early and the fact's "worker still running at dispose" probe fires.
- `WorkspaceSaveCoordinatorTests` (new file, class
  `WorkspaceSaveCoordinatorTests`):
  `AdmitAlwaysReservesThePathEvenOnAnIdleChain` (a save enqueued right
  after an idle `Admit` waits for the operation's Task),
  `AdmitCompletesItsTicketOnlyWhenTheOperationsTaskCompletes`,
  `AdmitOnAClosedCoordinatorReturnsFalseAndRunsNothing`.

Existing facts that must keep passing: `PropertiesLandingTests.cs:448`,
`RescanTests.FollowUpB.cs:83`, `:130`, `SaveOffDispatcherTests`.

## Docs and registers

- Contract 38: append a D-10 note ("D-10 extended to the History
  restore, the template reload and property writes; an admitted
  operation's own reload runs inside its ticket and carries a queued
  save across its epoch bump; WB-2, #1322 #1325") under the plan-44
  heading; never rewrite the frozen row.
- No census, designation or `chords.json` change; no new sentence (the
  restore's start-time refusal reuses the existing D-14 line) unless WD2
  is answered "refuse" (then the refusal line is a HostComposed residue
  designated in `a11y_trigger_designations.json`).

## Verification

```powershell
$ErrorActionPreference = 'Stop'
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb02'
./apps/slate-windows/generate-bindings.ps1 -Locked   # throws on failure
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }
$required = @(
  'AHistoryRestoreStartedUnderAnAdmittedSaveWaitsThenRestoresOverTheSavedBytes',
  'AHistoryRestoreNeverSilencesTheSaveItWaitedFor',
  'AHistoryRestoreThatFindsItsTabDirtyWhenItStartsRefusesAndWritesNothing',
  'ASaveStartedUnderAnAdmittedRestoreWaitsForTheRestoresPublication',
  'ADirtySaveQueuedBehindARestoreReportsAConflictAndOverwritesNothing',
  'TheUnadmittedReloadStillKeepsATabWithAnAdmittedSave',
  'ATemplatesSamePathReloadWaitsForTheNotesAdmittedSave',
  'APropertyCommitInAPeerTabWaitsForTheOtherTabsSaveAndWritesOnce',
  'ASaveStartedUnderAnAdmittedPropertyWriteWaitsForItsCompletion',
  'TeardownJoinsAnAdmittedRestoreAndPropertyWrite',
  'AdmitAlwaysReservesThePathEvenOnAnIdleChain',
  'AdmitCompletesItsTicketOnlyWhenTheOperationsTaskCompletes',
  'AdmitOnAClosedCoordinatorReturnsFalseAndRunsNothing',
  'ADayPickedWhileItsNoteSavesWritesNothingUntilTheSavePublishes',
  'ARescanNeverReloadsOverAnAdmittedSave',
  'ARescansReseatKeepsATabWithAnAdmittedSave')
$filter = 'FullyQualifiedName~SaveChainAdmissionTests|FullyQualifiedName~WorkspaceSaveCoordinatorTests|FullyQualifiedName~PropertiesLandingTests|FullyQualifiedName~RescanTests|FullyQualifiedName~SaveOffDispatcherTests'
$results = Join-Path $env:TEMP 'wb02-results'
if (Test-Path $results) { Remove-Item $results -Recurse -Force }
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --no-build --configuration Release --filter $filter --logger 'trx;LogFileName=wb02.trx' --results-directory $results
if ($LASTEXITCODE -ne 0) { throw "dotnet test failed ($LASTEXITCODE)" }
[xml]$trx = Get-Content (Join-Path $results 'wb02.trx')
$passed = @($trx.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq 'Passed' } | ForEach-Object { $_.testName })
$missing = @($required | Where-Object { $fact = $_; -not ($passed | Where-Object { $_ -like "*.$fact" }) })
if ($missing.Count -gt 0) { throw "required facts not selected and passed: $($missing -join ', ')" }
$files = @(
  'apps/slate-windows/src/SlateWindows/WorkspaceSaveCoordinator.cs',
  'apps/slate-windows/src/SlateWindows/WorkspaceViewModel.cs',
  'apps/slate-windows/src/SlateWindows/WorkspaceViewModel.History.cs',
  'apps/slate-windows/src/SlateWindows/WorkspaceViewModel.Templates.cs',
  'apps/slate-windows/src/SlateWindows/WorkspaceViewModel.Properties.cs',
  'apps/slate-windows/src/SlateWindows/WorkspaceTabViewModel.Properties.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/SaveChainAdmissionTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/WorkspaceSaveCoordinatorTests.cs')
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $files
if ($LASTEXITCODE -ne 0) { throw "dotnet format reports changes ($LASTEXITCODE)" }
```

(`RescanTests` is the real class name; `RescanTests.FollowUpB` is only
a file suffix and would select nothing. The TRX check proves each named
fact was selected by the filter and passed; xUnit's `testName` is the
fully qualified `Namespace.Class.Method`. `WorkspaceViewModel.cs` holds
`WorkspaceTabViewModel`, where `ReloadKeepingQueuedSave` and
`SaveBatch.Rebase` land.) CI: the always-on Windows lane. AT: none — no
sentence changes under the recommended answer.

## Acceptance

- [ ] WD2 recorded in the PR body (wait vs refuse; #1325 in or out).
- [ ] The ten `SaveChainAdmissionTests` facts and the three coordinator facts fail first, then pass; mutations run locally per §4 rule 1 (both queue directions among them); every hold's wait result asserted.
- [ ] The restore's continuation reloads through `ReloadOpenTabFromDiskAdmitted`, and the guarded `ReloadOpenTabFromDisk` keeps its D-10 guard for unadmitted callers.
- [ ] `PropertiesLandingTests.cs:448` and the FollowUpB facts unchanged and green.
- [ ] Contract 38 D-10 note appended.
- [ ] PR body: `Closes #1322`, `Closes #1325` (or #1325 "not delivered: owner declined").

## Brief

Read `WorkspaceSaveCoordinator.cs:60-131` and `:189-206` (`Enqueue`,
`TrackWorker`, `SaveTicket` at `:275-314`), `WorkspaceViewModel.cs:120-130`,
`:516-612` (`ReplaceItem`), `:769-1006` (`SaveAsync`, `HasPendingSaves`,
`SaveBatch`, `StartSave`) and `:2978-3034`,
`WorkspaceViewModel.History.cs:113-269`,
`WorkspaceViewModel.Templates.cs:226-287`, `WorkspaceViewModel.Properties.cs:38-50`,
`:205-215`, `:244-250`, `:412-421`, `:507-515`,
`WorkspaceTabViewModel.Properties.cs:74-215`, and the twin facts at
`RescanTests.FollowUpB.cs:76-172` and `PropertiesLandingTests.cs:448-515`.
Decision WD2 is pending (recommended: wait — join the admitted save,
then act — and ship #1325; under "refuse", a site that finds
`HasAdmittedSaveFor(path)` true at request time speaks one HostComposed
line "Wait for the current save to finish." and does nothing, an
operation not refused is still admitted, the facts that start an
operation under a save keep their names and assert the refusal, and the
line is designated as residue in
`docs/plans/18_windows_port/a11y_trigger_designations.json`; under "no"
to #1325 its site and facts are dropped and the issue closes as
won't-fix). Build: `Task<bool> WorkspaceSaveCoordinator.Admit(string path, Func<SaveTicket, Task> operation)`
— always enqueues a ticket under the path key, runs the operation on the
dispatcher after every earlier save settles (so only its own ticket and
its queued successors can be pending while it runs), completes the
ticket only when the operation's Task completes, tracks its worker for
teardown. Wrap the History restore (write and continuation) in it; at the
operation's start re-apply the stage-time rules — a tab dirty by then
gets the existing D-14 line ("Save the note before restoring a version.
The editor has unsaved changes in {file}.") and nothing is written,
otherwise re-read the expected hash from `SavedContentHash`. The
continuation reloads through new
`private void ReloadOpenTabFromDiskAdmitted(string path, WorkspaceSaveCoordinator.SaveTicket admittedBy)`,
which consults neither `HasAdmittedSaveFor` (true because of its own
ticket or a queued successor) nor `HasPendingSaves`, skips a dirty tab,
and otherwise calls new `internal void WorkspaceTabViewModel.ReloadKeepingQueuedSave()`:
`ReplaceItem(Item)` then re-stamp the tab's queued, not-yet-started
`_queuedSave` to the new item epoch with new `SaveBatch.Rebase(int itemEpoch)`
(`ItemEpoch` gains a private setter), so `StartSave`'s epoch check never
retires the successor. The guarded `ReloadOpenTabFromDisk` keeps a D-10
guard (`HasPendingSaves || HasAdmittedSaveFor(path)` keeps the buffer)
for unadmitted callers (`ReloadOpenTabFromDiskForTests`). Wrap the
template same-path sweep in `Admit` and reload each matching clean tab
through `ReloadKeepingQueuedSave()`, never a bare `ReplaceItem`; wrap
the four property dispatches in `Admit`, reading the expected hash when
the operation starts; add the worker seams
`internal Action? HistoryRestoreHookForTests` (workspace, before
`RestoreVersion`) and `internal Action? PropertyWriteHookForTests`
(`WorkspaceTabViewModel`, before the core property write); keep the
lease, hash check and the one-tab dirty refusal. Facts, failing first,
in new `SaveChainAdmissionTests.cs` (every observed save requested with
`SaveAsync(announce: true)`):
`AHistoryRestoreStartedUnderAnAdmittedSaveWaitsThenRestoresOverTheSavedBytes`
(save parked, edit taken back, restore confirmed, edit retyped: the save
publishes first, then the restore's own reload runs under its live ticket
— `ItemIdentity` +1, `Text` restored),
`AHistoryRestoreNeverSilencesTheSaveItWaitedFor` (`NoteSaved` before
`RestoredVersionFrom`, no publication retired),
`AHistoryRestoreThatFindsItsTabDirtyWhenItStartsRefusesAndWritesNothing`
(no retyping: the D-14 line once, file holds the saved bytes, no
`RestoredVersionFrom`),
`ASaveStartedUnderAnAdmittedRestoreWaitsForTheRestoresPublication`
(restore parked at `HistoryRestoreHookForTests`, a clean tab's save
queued: the reload still runs, then `NoteSaved` once after
`RestoredVersionFrom`, `save.Result` true),
`ADirtySaveQueuedBehindARestoreReportsAConflictAndOverwritesNothing`
(edit stays, `NoteSaveConflict` once, file holds the restore's bytes),
`TheUnadmittedReloadStillKeepsATabWithAnAdmittedSave`
(`ReloadOpenTabFromDiskForTests` under a parked save keeps
`ItemIdentity`; the save's `NoteSaved` still spoken),
`ATemplatesSamePathReloadWaitsForTheNotesAdmittedSave` (`ItemIdentity`
kept until release, one reload after, a save queued meanwhile still
speaks `NoteSaved`),
`APropertyCommitInAPeerTabWaitsForTheOtherTabsSaveAndWritesOnce` (no
conflict, one write each), `ASaveStartedUnderAnAdmittedPropertyWriteWaitsForItsCompletion`,
`TeardownJoinsAnAdmittedRestoreAndPropertyWrite`; and in new
`WorkspaceSaveCoordinatorTests.cs`: `AdmitAlwaysReservesThePathEvenOnAnIdleChain`,
`AdmitCompletesItsTicketOnlyWhenTheOperationsTaskCompletes`,
`AdmitOnAClosedCoordinatorReturnsFalseAndRunsNothing`. Every hold
asserts its `Wait` result and releases in `finally`. Mutations to run
locally: drop the `Admit` wrap; complete the ticket on return instead of
on Task completion; call the guarded `ReloadOpenTabFromDisk` from the
restore's continuation (direction 1 fails); guard the admitted reload
with `HasPendingSaves || HasAdmittedSaveFor` (direction 2 fails); drop
`Rebase`; drop the start-time dirty re-check; drop the admitted reload's
`IsDirty` check; drop the unadmitted reload's D-10 guard; a bare
`ReplaceItem` in the template sweep; skip `TrackWorker`; call
`WriteProperty` directly. Registers: append the D-10 note to
`docs/plans/38_notification_dispatcher_contracts.md`. Done when the
thirteen new facts and `PropertiesLandingTests`, `RescanTests`,
`SaveOffDispatcherTests` pass locally (`dotnet test --filter` by class
name with a TRX log; the run throws on a nonzero exit code and on any
required fact missing from the passed results), `dotnet format
--include` over the eight files (`WorkspaceSaveCoordinator.cs`,
`WorkspaceViewModel.cs`, `.History.cs`, `.Templates.cs`,
`.Properties.cs`, `WorkspaceTabViewModel.Properties.cs` and the two new
test files) is clean, and the Windows lane is green; no AT pass (no
sentence changes under the recommended answer). PR body: `Closes #1322`,
`Closes #1325`. Commit: `fix(windows): a history restore, a template
reload and a property write wait for an admitted save of the same note`.
