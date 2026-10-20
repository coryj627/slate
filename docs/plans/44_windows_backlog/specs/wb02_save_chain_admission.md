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
  while a save admitted under the path's folded key (`PathKey`, `:74-75`)
  has not completed; dispatcher-only. `:97-131` `Enqueue(keys, start)`
  runs `start` on the dispatcher once every earlier save under any of the
  keys has completed — this is the join primitive the chunk reuses.
  `KeysFor(tab, path)` (`:68-69`) is the tab-plus-path key set a save uses.
- `WorkspaceViewModel.cs:3003-3030` `KeepsBufferOnReseat(tab, stored)`:
  the one save-aware rule (`tab.HasPendingSaves || _saves.HasAdmittedSaveFor(tab.Path) || _saves.HasAdmittedSaveFor(stored)`),
  used by the Created-event re-seat (`:2966`) and the rescan's
  (`WorkspaceViewModel.Rescan.cs:396`). `_saves` is the workspace's
  coordinator (`:2000-2001`); `SavesForTests` exposes it (`:2842`).
- **#1322 site 1:** `WorkspaceViewModel.History.cs:255-267`
  `ReloadOpenTabFromDisk(path)`: `FindPathBackedTab(path)` and, when
  `IsDirty` is false, `tab.ReplaceItem(tab.Item)` — no admitted-save
  check. Its caller is the restore continuation at `:228`, after
  `_session.RestoreVersion(...)` ran on the history worker (`:176-183`,
  a CAS write with `request.ExpectedContentHash`). So the restore's own
  write also runs beside an in-flight save; WD2's "wait" has to cover the
  write, not only the reload. `ReloadOpenTabFromDiskForTests` (`:245`) is
  the existing seam.
- **#1322 site 2:** `WorkspaceViewModel.Templates.cs:267-277`: after the
  create, every clean same-path Markdown tab whose text differs from
  `rendered.Body` (or is missing from disk) gets `ReplaceItem` — no
  admitted-save check.
- **#1325:** `WorkspaceViewModel.Properties.cs:38-50` the note-scoped
  write lease (`_propertyWritePaths`, `TryAcquirePropertyWriteLease`,
  `WithLeaseRelease`); dispatch sites `:205-215` (add), `:244-250`
  (retry add), `:412-421` and `:507-515` (row set/delete). Each calls
  `tab.WriteProperty(expectedHash, write, completion)`
  (`WorkspaceTabViewModel.Properties.cs:146-149`), which runs the core
  write on `Task.Run` with a hash check — never through `_saves`.
  Within one tab the race is closed by the dirty refusal pinned at
  `PropertiesLandingTests.cs:448`
  `ADayPickedWhileItsNoteSavesWritesNothingUntilTheSavePublishes`; a
  peer tab's save is not.
- Test seams: `WorkspaceViewModel.cs:880` `SaveWriteHookForTests` (runs
  on the save worker before the core write), `:885`
  `SaveWrittenHookForTests`; used by `RescanTests.FollowUpB.cs:83-127`
  `ARescanNeverReloadsOverAnAdmittedSave` and `:130-176`
  `ARescansReseatKeepsATabWithAnAdmittedSave`, the twin shape for the
  new facts.

## Reference

No mac twin (mac has no off-dispatcher save chain). Contract 38 D-10
(`docs/plans/38_notification_dispatcher_contracts.md`): a reload must
never retire an admitted save's publication. Contract 40 R-9's
`KeepsBufferOnReseat` doc (`WorkspaceViewModel.cs:2978-3002`) is the rule
being extended.

## Design

WD2 recommends: "Wait (join the save's publication, then act), and yes:
both go through `WorkspaceSaveCoordinator.HasAdmittedSaveFor`." If the
owner answers "refuse", the three sites instead speak one HostComposed
line, "Wait for the current save to finish.", and do nothing (the
structural commands' rescan posture); the facts below keep their names
and assert the refusal instead of the deferred action. If the owner
answers "no" to #1325, its site and facts are dropped and the issue is
closed as "won't fix: nothing is lost today".

1. **New admission primitive (new):**
   `Task<bool> WorkspaceSaveCoordinator.Admit(string path, Func<SaveTicket, Task> operation)`
   — dispatcher-only. It ALWAYS enqueues a ticket under `PathKey(path)`
   through `Enqueue` (`:97-131`), idle chain included, so a save admitted
   after it waits for it exactly as it waits for an earlier save (the
   restore IS a save; the file's chain stays ordered in both directions).
   When the earlier saves have settled, `operation(ticket)` runs on the
   dispatcher; the ticket completes (`ticket.Complete(true)`, or
   `ticket.Fail`) only when the Task the operation returns completes —
   after the operation's dispatcher publication or its terminal failure,
   never when the call returns. The operation's worker Task is handed to
   `TrackWorker` (`:200-215`) so teardown joins it like a save's. A closed
   coordinator returns `false` and the operation never runs (the caller
   speaks nothing; the shell is closing).
2. **History restore** (`PerformRestore`, `History.cs:170-235`): the
   whole restore is the operation — the worker's `RestoreVersion`
   (`:176-183`) and the continuation (`:199-235`, `ReloadOpenTabFromDisk`
   and `NotePersisted`) — and the operation's Task completes at the end of
   that continuation. The `ExpectedContentHash` the request carries is
   re-read from the tab's `SavedContentHash` when the operation starts, so
   a restore admitted behind a save lands on the saved bytes instead of
   conflicting with them. `ReloadOpenTabFromDisk` itself gains the D-10
   guard: a tab with `HasPendingSaves` or `HasAdmittedSaveFor` is not
   replaced (defensive; the admission makes it unreachable).
3. **Template same-path reload** (`Templates.cs:267-277`): the sweep is
   itself an `Admit(path, …)` operation whose Task is already complete
   when it returns (the reload is synchronous on the dispatcher); it
   re-checks `IsDirty` and the text difference at run time (the save may
   have made them equal). With no admitted save it runs in the same
   dispatcher turn as today.
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
   refuse a second commit meanwhile. Not changed: the hash check, the
   conflict prompt, the lease, the one-tab dirty refusal pinned at
   `PropertiesLandingTests.cs:448`.

Not changed: `KeepsBufferOnReseat`, the rescan paths, mac, core, FFI.

## Tests

`SlateWindows.Tests`, new file `SaveChainAdmissionTests.cs` (class
`SaveChainAdmissionTests`; the harness of `RescanTests.FollowUpB.cs:83-127`
— file of the partial class `RescanTests`; park the save at
`SaveWriteHookForTests`). Every hold is observed: each
`ManualResetEventSlim.Wait(TimeSpan)` result is asserted (`Assert.True(parked.Wait(TimeSpan.FromSeconds(10)), "…never parked")`;
a timeout is the fact's failure, never a silent release) and every
gate is `Set()` in `finally`, the hook nulled there.

- `AHistoryRestoreStartedUnderAnAdmittedSaveWaitsThenRestoresOverTheSavedBytes`
  — restore admitted behind the parked save; after release the save
  publishes first (`save.Result` true, `IsDirty` false, the "Saved"
  family spoken once), then the restore writes and the tab shows the
  restored text; the edit made during the write is in history, not lost.
  Mutation: drop the `Admit` wrap → the restore runs first and the
  ordering assertions fail.
- `AHistoryRestoreNeverSilencesTheSaveItWaitedFor` — the
  `RestoredVersionFrom` line follows the save's publication line; no
  save publication was retired (`SettleSaves()` true; `ItemIdentity`
  unchanged until the restore's own reload).
- `ASaveStartedUnderAnAdmittedRestoreWaitsForTheRestoresPublication`
  — the reverse order: the restore's worker is parked (a new
  `HistoryRestoreHookForTests` on the worker, the `SaveWriteHookForTests`
  shape); a save requested meanwhile is admitted but does not write until
  the restore's continuation ran; the file then holds the restore's bytes
  under the save's edit. Mutation: complete the ticket when `Admit`'s
  operation returns instead of when its Task completes → the save
  overtakes the restore.
- `ATemplatesSamePathReloadWaitsForTheNotesAdmittedSave` — a clean
  same-path tab with a parked save keeps its buffer and `ItemIdentity`
  through the template create; after release it reloads once.
  Mutation: remove the admission → `ItemIdentity` changes under the save.
- `APropertyCommitInAPeerTabWaitsForTheOtherTabsSaveAndWritesOnce` (#1325)
  — split pane, tab B's save parked, tab A commits a property: no write
  until release; after it, one property write on top of the saved bytes,
  no `PropertyEditConflict`, no conflict dialog; the file holds both the
  edit and the property. Mutation: call `WriteProperty` directly → the
  conflict prompt fires.
- `ASaveStartedUnderAnAdmittedPropertyWriteWaitsForItsCompletion` (#1325)
  — the property write's worker is parked at `PropertyWriteFaultForTests`'s
  neighbour seam (a new `PropertyWriteHookForTests` before the core
  write); a save of the same note requested meanwhile writes only after
  the property completion ran; one write each, nothing lost.
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
  restore, the template reload and property writes; WB-2, #1322 #1325")
  under the plan-44 heading; never rewrite the frozen row.
- No census, designation or `chords.json` change; no new sentence unless
  WD2 is answered "refuse" (then the refusal line is a HostComposed
  residue designated in `a11y_trigger_designations.json`).

## Verification

```powershell
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb02'; ./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0 before reading counts
$filter = "FullyQualifiedName~SaveChainAdmissionTests|FullyQualifiedName~WorkspaceSaveCoordinatorTests|FullyQualifiedName~PropertiesLandingTests|FullyQualifiedName~RescanTests|FullyQualifiedName~SaveOffDispatcherTests"
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --no-build --configuration Release --list-tests --filter $filter | Select-String "SaveChainAdmissionTests|WorkspaceSaveCoordinatorTests" | Measure-Object | ForEach-Object { if ($_.Count -lt 10) { throw "new facts not discovered: $($_.Count)" } }
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --no-build --configuration Release --filter $filter
$files = @(
  'apps/slate-windows/src/SlateWindows/WorkspaceSaveCoordinator.cs',
  'apps/slate-windows/src/SlateWindows/WorkspaceViewModel.History.cs',
  'apps/slate-windows/src/SlateWindows/WorkspaceViewModel.Templates.cs',
  'apps/slate-windows/src/SlateWindows/WorkspaceViewModel.Properties.cs',
  'apps/slate-windows/src/SlateWindows/WorkspaceTabViewModel.Properties.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/SaveChainAdmissionTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/WorkspaceSaveCoordinatorTests.cs')
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $files
```

(`RescanTests` is the real class name; `RescanTests.FollowUpB` is only
a file suffix and would select nothing.) CI: the always-on Windows
lane. AT: none — no sentence changes under the recommended answer.

## Acceptance

- [ ] WD2 recorded in the PR body (wait vs refuse; #1325 in or out).
- [ ] The seven `SaveChainAdmissionTests` facts and the three coordinator facts fail first, then pass; mutations run locally per §4 rule 1; every hold's wait result asserted.
- [ ] `PropertiesLandingTests.cs:448` and the FollowUpB facts unchanged and green.
- [ ] Contract 38 D-10 note appended.
- [ ] PR body: `Closes #1322`, `Closes #1325` (or #1325 "not delivered: owner declined").

## Brief

Read `WorkspaceSaveCoordinator.cs:60-131` and `:200-215` (`Enqueue`,
`TrackWorker`, `SaveTicket` at `:275-305`), `WorkspaceViewModel.cs:2978-3030`
and `:870-990`, `WorkspaceViewModel.History.cs:170-267`,
`WorkspaceViewModel.Templates.cs:226-285`, `WorkspaceViewModel.Properties.cs:38-50`,
`:205-215`, `:244-250`, `:412-421`, `:507-515`,
`WorkspaceTabViewModel.Properties.cs:146-200`, and the twin facts at
`RescanTests.FollowUpB.cs:83-176` and `PropertiesLandingTests.cs:448-515`.
Decision WD2 is pending (recommended: wait — join the admitted save,
then act — and ship #1325; under "refuse", the three sites speak one
HostComposed line "Wait for the current save to finish." and do nothing,
the facts keep their names and assert the refusal, and the line is
designated as residue in `docs/plans/18_windows_port/a11y_trigger_designations.json`;
under "no" to #1325 its site and facts are dropped and the issue closes
as won't-fix). Build: `Task<bool> WorkspaceSaveCoordinator.Admit(string path, Func<SaveTicket, Task> operation)`
— always enqueues a ticket under the path key, runs the operation on the
dispatcher after earlier saves settle, completes the ticket only when the
operation's Task completes, tracks its worker for teardown; wrap the
History restore (write and continuation), the template same-path reload
and the four property dispatches in it, reading the expected hash when
the operation starts; add `HistoryRestoreHookForTests` and
`PropertyWriteHookForTests` worker seams; keep the lease, hash check and
the one-tab dirty refusal. Facts, failing first, in new
`SaveChainAdmissionTests.cs`: `AHistoryRestoreStartedUnderAnAdmittedSaveWaitsThenRestoresOverTheSavedBytes`
(save publishes first, restore lands after, edit kept),
`AHistoryRestoreNeverSilencesTheSaveItWaitedFor` (the Saved line spoken,
no publication retired), `ASaveStartedUnderAnAdmittedRestoreWaitsForTheRestoresPublication`
(reverse order), `ATemplatesSamePathReloadWaitsForTheNotesAdmittedSave`
(`ItemIdentity` kept until release, one reload after),
`APropertyCommitInAPeerTabWaitsForTheOtherTabsSaveAndWritesOnce` (no
conflict, one write each), `ASaveStartedUnderAnAdmittedPropertyWriteWaitsForItsCompletion`,
`TeardownJoinsAnAdmittedRestoreAndPropertyWrite`; and in new
`WorkspaceSaveCoordinatorTests.cs`: `AdmitAlwaysReservesThePathEvenOnAnIdleChain`,
`AdmitCompletesItsTicketOnlyWhenTheOperationsTaskCompletes`,
`AdmitOnAClosedCoordinatorReturnsFalseAndRunsNothing`. Every hold
asserts its `Wait` result and releases in `finally`. Mutations to run
locally: drop the `Admit` wrap; complete the ticket on return instead of
on Task completion; skip `TrackWorker`; call `WriteProperty` directly.
Registers: append the D-10 note to `docs/plans/38_notification_dispatcher_contracts.md`.
Done when the ten facts and `PropertiesLandingTests`, `RescanTests`,
`SaveOffDispatcherTests` pass locally (`dotnet test --filter` by class
name, discovered count checked), `dotnet format --include` over the seven
files is clean, and the Windows lane is green; no AT pass (no sentence
changes under the recommended answer). PR body: `Closes #1322`,
`Closes #1325`. Commit: `fix(windows): a history restore, a template
reload and a property write wait for an admitted save of the same note`.
