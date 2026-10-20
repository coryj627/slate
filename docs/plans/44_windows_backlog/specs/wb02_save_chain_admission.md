# WB-2 — A History restore, a template reload and a property write join an admitted save of the same note instead of racing it

Issues: Closes #1322, Closes #1325. Size S–M. Lanes: windows. AT: none (no speech changes). Wave: W1. Depends on: nothing. Parallel with: WB-1, WB-3. Decision: WD2 (pending; recommended "wait, and yes").

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

1. **New join primitive (new):** `WorkspaceSaveCoordinator.WhenSettled(string path, Action act)`
   — dispatcher-only; if `HasAdmittedSaveFor(path)` is false, runs `act`
   now; otherwise `Enqueue([PathKey(path)], ticket => { act(); ticket.Complete(true); })`
   so `act` runs on the dispatcher after the admitted save's publication
   and before any later save on the file (the file's chain stays
   ordered). A closed coordinator drops `act`.
2. **History restore** (`PerformRestore`, `History.cs:170`): the whole
   restore — the worker's `RestoreVersion` and the continuation's
   `ReloadOpenTabFromDisk` — is wrapped in `_saves.WhenSettled(request.Path, …)`.
   The `ExpectedContentHash` the request carries is re-read from the
   tab's `SavedContentHash` inside the join when a save published
   meanwhile, so the restore's CAS lands on the saved bytes rather than
   conflicting with them. `ReloadOpenTabFromDisk` itself gains the
   D-10 guard: a tab with `HasPendingSaves` or `HasAdmittedSaveFor` is
   not replaced (defensive; the join makes it unreachable).
3. **Template same-path reload** (`Templates.cs:267-277`): the sweep
   skips a tab for which `_saves.HasAdmittedSaveFor(path)` holds and
   schedules the same reload through `WhenSettled(path, …)`, re-checking
   `IsDirty` and the text difference at run time (the save may have
   made them equal).
4. **Property writes** (`Properties.cs:205`, `:244`, `:412`, `:507`):
   `DispatchRowWrite` and the add/retry dispatchers acquire the lease as
   today, then call `WhenSettled(path, () => tab.WriteProperty(…))` with
   the expected hash read from the tab at run time (the owner header's
   snapshot hash after the save's publication rebuilt it — `AwaitRepublished`
   in the landing tests shows the rebuild). `row.WriteInFlight` stays true
   across the wait so the row's gates refuse a second commit meanwhile.
   Not changed: the hash check, the conflict prompt, the lease, the
   one-tab dirty refusal pinned at `PropertiesLandingTests.cs:448`.

Not changed: `KeepsBufferOnReseat`, the rescan paths, mac, core, FFI.

## Tests

`SlateWindows.Tests`, new file `SaveChainAdmissionTests.cs` (the
`RescanTests.FollowUpB` harness shape; park the save at
`SaveWriteHookForTests`):

- `AHistoryRestoreStartedUnderAnAdmittedSaveWaitsThenRestoresOverTheSavedBytes`
  — restore parked behind the save; after release the save publishes
  (`save.Result` true, `IsDirty` false, "Saved" family spoken once), then
  the restore writes and the tab shows the restored text; the edit made
  during the write is in history, not lost. Mutation: drop the
  `WhenSettled` wrap → the restore runs first and the fact's ordering
  assertions fail.
- `AHistoryRestoreNeverSilencesTheSaveItWaitedFor` — asserts the
  `RestoredVersionFrom` line follows the save's publication line, and
  that no save publication was retired (`SettleSaves()` true, the tab's
  `ItemIdentity` unchanged until the restore's own reload).
- `ATemplatesSamePathReloadWaitsForTheNotesAdmittedSave` — a clean
  same-path tab with a parked save keeps its buffer and document identity
  through the template create; after release it reloads once.
  Mutation: remove the `HasAdmittedSaveFor` skip → `ItemIdentity` changes
  under the save.
- `APropertyCommitInAPeerTabWaitsForTheOtherTabsSaveAndWritesOnce` (#1325)
  — split pane, tab B's save parked, tab A commits a property: no write
  until release; after it, one property write on top of the saved bytes,
  no `PropertyEditConflict`, no conflict dialog, file contains both the
  edit and the property. Mutation: call `WriteProperty` directly → the
  conflict prompt fires.
- `WorkspaceSaveCoordinatorTests.WhenSettledRunsNowWithNoAdmittedSaveAndAfterTheChainOtherwise`
  — unit fact over the coordinator alone.

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

```
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb02'; ./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --filter "FullyQualifiedName~SaveChainAdmissionTests|FullyQualifiedName~PropertiesLandingTests|FullyQualifiedName~RescanTests|FullyQualifiedName~SaveOffDispatcherTests|FullyQualifiedName~WorkspaceSaveCoordinatorTests"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include apps/slate-windows/src/SlateWindows/WorkspaceSaveCoordinator.cs apps/slate-windows/src/SlateWindows/WorkspaceViewModel.History.cs apps/slate-windows/src/SlateWindows/WorkspaceViewModel.Templates.cs apps/slate-windows/src/SlateWindows/WorkspaceViewModel.Properties.cs apps/slate-windows/tests/SlateWindows.Tests/SaveChainAdmissionTests.cs
```

CI: the always-on Windows lane. AT: none — no sentence changes under
the recommended answer.

## Acceptance

- [ ] WD2 recorded in the PR body (wait vs refuse; #1325 in or out).
- [ ] Five facts fail first, then pass; mutations run locally per §4 rule 1.
- [ ] `PropertiesLandingTests.cs:448` and the FollowUpB facts unchanged and green.
- [ ] Contract 38 D-10 note appended.
- [ ] PR body: `Closes #1322`, `Closes #1325` (or #1325 "not delivered: owner declined").

## Brief

Read `WorkspaceSaveCoordinator.cs:60-131`, `WorkspaceViewModel.cs:2978-3030`
and `:870-990`, `WorkspaceViewModel.History.cs:170-267`,
`WorkspaceViewModel.Templates.cs:226-285`, `WorkspaceViewModel.Properties.cs:38-50`
and the four dispatch sites, `WorkspaceTabViewModel.Properties.cs:146-200`,
and the twin facts at `RescanTests.FollowUpB.cs:83-176` and
`PropertiesLandingTests.cs:448-515`. Add `WhenSettled(path, act)` to the
coordinator; wrap the History restore (write and reload), the template
same-path reload and the four property dispatches in it; keep the lease,
hash check and the one-tab dirty refusal. Write the five facts above
failing first. Append the D-10 note. Done when the facts pass locally and
the Windows lane is green. Commit: `fix(windows): a history restore, a
template reload and a property write wait for an admitted save of the
same note`.
