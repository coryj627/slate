# CB-4 — No test fixture tears down under work it released: #1673, its sibling fixtures, and the census's first rule

Issues: #1673. Size M. Lanes: windows (the unit facts; both model shards run, because one park type is the Connections model's). AT: — (tests only). Wave: C1, slot 3. Depends on: nothing (CB-0 merges first). Parallel with: CB-1, CB-2 (no shared files). Decision: none. Interlock: this PR creates `Censuses/TestWaitCensus.cs`; CB-6, then CB-7, append rules 1, 2 and 4 at the end of it. Later chunks that edit these fixture files (CB-6, CB-26) rebase over it.

## Goal

A fixture that lets held work go waits for that work before its session goes, and never disposes a gate a worker may still reach. #1673's fixture and every sibling do so, each with a fact that fails on today's teardown. A census keeps the form from coming back.

## Current state on main

Paths: `T/` = `apps/slate-windows/tests/SlateWindows.Tests/`, `S/` = `apps/slate-windows/src/SlateWindows/`, `RCVM` = `S/Reading/ReadingContentViewModel.cs`. Every line was opened at c2377210. Nothing was built or run.

**The product already has every drain, so no product file changes.** `RCVM:717-726` `WhenRefreshWorkDrained()` ends when every tracked fetch (`:606`) and diagram stage (`:2035`) has ended; tests see internals (`S/AssemblyInfo.cs:7`). The property header is a `PanelWorkScheduler` with `WhenAllWorkDrained()` (`S/Panels/PanelWorkScheduler.cs:564`). The palette lane has `WhenIdle()` (`S/CommandPaletteWorkLane.cs:69-83`). `PumpedDispatcher.PumpUntilDrained(Task)` (`T/PumpedDispatcher.cs:127-131`) pumps until a task ends and observes its fault. The two right shapes for a gate are `T/Support/HeldWork.cs` (a `TaskCompletionSource`, nothing to dispose) and "the events are left to the collector" (`T/GraphDiagramTests.Lineage.cs:48-51`, `T/GraphDocumentTests.RequestLineage.cs:94-97`).

**The seven fixtures.**

| # | Fixture and `Dispose` | Today's teardown | What a late worker hits |
|---|---|---|---|
| 1 | `T/ReadingFocusTests.cs` `Host`, `:4665-4730` | The loop `:4680-4695` waits for a gate a fetch has reached (`:4689`) and only sets one no fetch has reached (`:4693`). Then the model (`:4696`), the session (`:4721`), and every gate is disposed (`:4723-4726`). | A fetch has "reached" its gate only once the pool thread runs the seam to `GateForTheFetch()` (`:4406`, `:4499-4507`). A later one waits on a disposed gate (`:4407`); the throw lands in the product's terminal catch (`RCVM:689-704`), which logs a failure and posts to a dead dispatcher. |
| 2 | `T/ReadingDiagramTests.Stage.cs` `StageHarness`, `:137-146` | `Gate.Set()`, close the window, `Tab.Dispose()`, `Session.Dispose()`. No wait. | A released stage checks its token (`RCVM:2060`), then calls `_session.GetDiagramBlocks` (`:2073`). One past the check when the tab is disposed calls a disposed session. |
| 3 | `T/ReadingFindSurfaceTests.cs` `Harness`, `:122-131` | The same, with `Find.Detach()`. | The same. |
| 4 | `T/GraphTableTests.Landing.cs` `ParkedFetch`, `:51-56` | Sets `_release`, then disposes it and `_reached`. The hook stays on `document.FetchGateForTests` (`:36-43`). Eight facts use it. | `_release.Wait` (`:41`) on a disposed event throws into the product's fetch. |
| 5 | `T/FinalMergeDeltaTests.cs` `ParkedLoads`, `:454-464` | Sets both releases, clears the workspace's seam, disposes four events. Tabs built during the park keep the hook (`S/WorkspaceViewModel.Layout.cs:409`). | A tab's second cache load waits on a disposed `ReleaseSecond` (`:429`). |
| 6 | `T/SaveChainAdmissionTests.cs` `Peers`, `:1517-1528` | Sets `_saveRelease` and `_readRelease`, clears the hooks, `H.Dispose()` (workspace, then session, `:1367-1378`), disposes four events. | The workspace joins the save worker (`S/WorkspaceViewModel.cs:3829`). Nothing joins the header read: it copies its hook (`NotePropertiesViewModel.cs:223`), runs it (`:228`), then calls `_session.ReadNoteParts` (`:229`). |
| 7 | `T/CommandPaletteTests.Shell.cs` `PaletteShellHost`, `:391-433` | Sets every lane gate (`:393-396`), closes the shell (`:419`), disposes the session (`:422`) and the vault (`:423`), then the gates (`:428-431`). | A parked lane item, and the recents write queued behind it, run after the session and the vault folder are gone. |

**Corrections to the reading** (`reading/01_test_stability.md`):

- `FinalMergeDeltaTests.cs:454-466` is `:454-464`; `CommandPaletteTests.Shell.cs:391-432` is `:391-433`. Every other cited line holds.
- "Whether a late fetch touches the session before its first token check is unverified": it does not. `Fetch` checks the token (`RCVM:905`) before its first session call (`:906`). #1's harm is the disposed gate, the false failure log and a pool thread that outlives its fact.
- #7 is not quite "no wait between". `Shell.Close()` can reach the palette's shutdown (`S/MainWindow.xaml.cs:2351`, `S/VaultLifecycleViewModel.cs:920-924`), which waits 5 s for the lane and then gives up (`S/CommandPaletteViewModel.cs:878`, `:500`). Unverified: that `Close()` on the never-shown shell raises `Closed`. The fixture names no wait of its own either way.
- The reading's fix for #1 keeps "dispose the gates last". Rule 3 forbids that. The gates go to the collector.

**The search, run again** (every `Dispose` body in both test projects that sets an event, completes a `TaskCompletionSource` or calls a release helper; then every one that disposes an event member). 16 release sites in `T/`, none in the gate project; the reading's line-window count was 13. The seven above are the only ones of the reading's shape. Two more have the event half only, and by plan rule 3 are this chunk's work:

- **#8, `T/PumpedSaveReentrancyTests.cs` `Host`, `:1490-1513`.** Releases through its own helpers (`:1376`, `:1226`), lets the lifecycle join the work, then disposes four events (`:1505-1509`). Not a live race by its own comment (`:1507`); the same form.
- **#9, `T/ConnectionsLeafTests.Receiver.cs` `ParkedFetch`, `:47-51`.** Disposes `Gate` and `Reached` and never sets them. A fact that fails before its own `Gate.Set()` (`:234`, `:261`) disposes a gate a worker is parked on or has yet to reach. The Connections model owns one too (`T/ConnectionsLeafTests.Model.cs:2099-2124`), after a drain.

Right already: the two collector parks, `HeldWork`, `T/PendingImport.cs:35-45`, `ModelCaseResources` (it drains through `Host.Settle`), `T/RescanTests.cs:2567-2595` and `T/WelcomeShell.cs:277-298` (the lifecycle owns the drain).

## Reference

`T/Censuses/StaThreadCensus.cs` (allow-list `:33-40`, stale-entry check `:62-65`, walk `:133-170`); `T/Censuses/ShellLaunchCensus.cs:38` (a floor, so a walk that missed files fails); `T/Censuses/AnnouncementNameCensus.cs:150-170` (a rule run on planted source, parsed with `CSharpSyntaxTree.ParseText`; `CSharpSource.LoadPath` reads files only). Plan 47 §4 rules 2, 3, 4, 6, 7, 8.

## Design

**1. A shared witness, `T/Support/TeardownWitness.cs` (new).** It lets a fact force the bad schedule with no sleep. It is built on `TaskCompletionSource`, so it has nothing to dispose.

- Seam side: `Hold()` blocks, 30 s at most, until the fixture is draining or its session is gone. It counts a late run when "gone" came first and returns that, so the seam can stop a late worker before it calls a disposed session.
- Fixture side: `DrainBeforeTheSessionGoes(Func<Task> work)` raises "draining", then calls `PumpedDispatcher.PumpUntilDrained(work())`. One call on purpose: deleting the drain deletes the signal, so a fixture that loses its drain goes red, not racy. `SessionGone()` follows the session's `Dispose` and records whether the drain had completed.
- Fact side: `WaitUntilEntered()`, `WaitUntilPassed()` (both bounded), `LateRuns`, `DrainedBeforeGone`.
- A fixture always calls the two fixture-side methods. `Hold()` is wired into a seam only when a fact arms it, so no other fact changes.

**2. The five fixtures that own a session: release, drain, then the session.** In each, the drain sits in a `try` whose `finally` still disposes the session and the vault.

- **#1.** Keep the loop. Keep every model `BindProjectionInFlight` creates in a list. After `_inFlight?.Dispose()` (the retire, `RCVM:1955`), drain each model's `WhenRefreshWorkDrained()`, then `PumpedDispatcher.Drain()`, before the canvas, the workspace and the session. Delete the gate disposal. The issue says "before it disposes the model"; the model's `Dispose` is the retire, so the order is release, retire, drain, session.
- **#2, #3.** Record every model `Tab.ReadingCreatedForTests` hands out (#3 sets that hook only when gated; set it always). `Tab.Dispose()` nulls `Tab.Reading` (`S/WorkspaceViewModel.cs:1457-1458`), so drain the recorded models between `Tab.Dispose()` and `Session.Dispose()`.
- **#6.** After the two releases, drain `Properties.WhenAllWorkDrained()`, then clear the hooks and dispose `H`. Delete the four event disposals.
- **#7.** Right after the gates are set, drain `Lane.WhenIdle()`, before anything closes. Delete the gate disposal.

**3. The four that own no session: release only.** #4, #5 and #9 become `Dispose() => <release>.Set()` (#5 sets both and still clears the seam), with the collector comment the lineage parks carry. #8 deletes its four dispose lines. The host declared before each park is disposed after it and owns the session.

**4. `T/Censuses/TestWaitCensus.cs` (new), `[Trait("census", "test-wait")]`.** One fact per rule, each with its own allow-list, over one shared walk, so later rules are independent facts.

- **Shared, at the top.** A lazy walk of both test projects (`bin` and `obj` skipped), parsed once. `TheWalkReadsBothTestProjects` pins a floor per project (419 and 33 files today; pin 400 and 30). Each rule is a pure function from parsed sources to violation strings, so its planted fact feeds it text.
- **Rule 3a, `NoFixtureDisposesAGateItReleasesOrWaitsOn`.** In a type's `Dispose` or `DisposeAsync`: no `Dispose()` (a call, a method group such as `CleanUp(gate.Dispose)`, or a `using`) on an event the type sets, releases or waits on. An event is a member, local, loop or pattern variable declared as `ManualResetEventSlim`, `ManualResetEvent`, `AutoResetEvent`, `SemaphoreSlim` or `CountdownEvent`, or an element of a collection of them. A set or dispose made through one of the type's own methods counts (one level). No allow-list. This is wider than the reading's words by the two cases the search found: a set through a helper (#8) and a gate disposed with no set (#9).
- **Rule 3b, `AFixtureThatReleasesAGateDrainsBeforeItsSessionGoes`.** In a `Dispose` that releases a gate (an event `Set` or `Release`, a `TaskCompletionSource` completion, a `HeldWork.Release`) and later tears a session down, a drain call stands between the first release and the teardown. Teardown is `Dispose()` on a `VaultSession`, a `VaultLifecycleViewModel`, or a test type whose own `Dispose` does either (by declared type; a nested type resolves inside its enclosing class first). A drain is a call named `PumpUntilDrained`, `WhenRefreshWorkDrained`, `WhenAllWorkDrained`, `WhenIdle` or `DrainBeforeTheSessionGoes`. `PumpedDispatcher.Drain()` is not one: it waits for no pool work. The one-level rule applies here too.
- **3b's allow-list** is `(File, Type, Reason, Dated)`: fixtures whose product lifecycle owns the drain. Expected at merge, by reading: `RescanTests.cs` `Host`, `PumpedSaveReentrancyTests.cs` `Host`, `ConnectionsLeafTests.Model.cs` `ModelCaseResources`. Confirm each by reading its teardown before writing its reason. An empty reason fails. An entry that matches no hit fails. Any other hit is a sibling: fix it.
- **`TheTeardownRulesSeeAPlantedFixture`,** a theory over planted source. Violations: sets and disposes one field; sets through its own `Release()`, then disposes; sets in one loop, disposes in another; disposes a gate its hook waits on; sets a gate, then disposes a session; the drain after the session; only `PumpedDispatcher.Drain()` between; the session inside a nested fixture. Clean: `Dispose() => _release.Set()`; a `TaskCompletionSource` release; release, `PumpUntilDrained`, session; a `using` event in a fact body.

**5. What the census does not prove.** 3b sees that a drain is named, not that it is the right one: today's #1 passes 3b because two unrelated drains stand between (`:4704`, `:4716`); only 3a flags it. The forced facts prove each drain. The `using`-declared events in fact bodies (372 by the reading's count) are outside this rule.

**6. Order inside the PR (plan rule 4).** First commit: the witness and the census, with today's instances as dated exceptions (3a: #1 and #4 to #9; 3b: #2, #3, #6, #7 and the standing three). Then one commit per fixture: its failing fact, its fix, its exception deleted. At merge 3a has no exception and 3b only the standing ones.

## Tests

Each fact is seen failing on today's teardown first. None sleeps.

**Forced schedule (five).** The fact arms the witness in the fixture's own seam, gets a worker to the hold, disposes the fixture, waits for the worker to pass, and asserts `LateRuns == 0` and `DrainedBeforeGone`. Today there is no drain, so the hold opens only on "gone": red every run. After the fix it opens on "draining": green every run.

| Fact | Where the hold goes | A late run (red phase) |
|---|---|---|
| `ReadingFocusTests.DisposingTheFixtureWaitsForAFetchThatHasNotReachedItsGate` (beside `:383`) | In the `FetchFaultForTests` lambda (`:4404`), before `GateForTheFetch()`, so the gate stays unreached. Arm, then `BindProjectionInFlight`. | Returns a fault from the seam. |
| `ReadingDiagramTests.DisposingTheHarnessWaitsForAStageItReleased` | `DiagramFaultForTests` (`RCVM:865`, run at `:2069`: after the token check, before the session call). The fact sets `Gate` and waits until entered, so the stage is past its check. | Returns `new VaultException.Io(…)` (the stage's catch, `:2079`). |
| `ReadingFindSurfaceTests.DisposingTheHarnessWaitsForAStageItReleased` | The same seam. | The same. |
| `SaveChainAdmissionTests.DisposingThePeersWaitsForAHeaderReadItReleased` | In the `ParkHeaderRead` hook, after `_readRelease.Wait` (`:1506`). Park, `Properties.RefreshProperties()`, wait for `HeaderReadParked`. | Throws from the hook. |
| `CommandPaletteTests.DisposingTheShellHostWaitsForTheLaneItReleased` | In `ParkTheLane`'s item, after `gate.Wait` (`:316`). | Returns. Red can take 5 s: the shell's own lane wait runs first. |

**A worker that arrives after the park's `Dispose` (three).** No schedule is needed: the fact calls the installed hook itself after disposing the park. Today `Wait` on the disposed event throws `ObjectDisposedException`; after the fix the hook returns.

- `GraphTableTests.AFetchThatReachesTheParkAfterItsDisposalPassesThrough`: `document.FetchGateForTests!()` after `park.Dispose()`.
- `FinalMergeDeltaTests.ALoadThatReachesTheParkAfterItsDisposalPassesThrough`: read `host.Workspace.InteractionBackgroundFaultForTests` before `loads.Dispose()`; call it with `EditorInteractionWorkerKind.Artifact` after.
- `ConnectionsLeafTests.AHoldAfterTheParksDisposalPassesThrough`: `parked.Dispose()`, then `parked.Hold()`; `TimedOut` stays false. The name must not start with `TheModelOf` (the lane's filter, `windows.yml:774`).

**Cannot be forced.** A pool thread still waking inside `Wait` while the fixture disposes that event (#4, #5, #8, and the event half of #1, #6, #7). There is no seam inside `ManualResetEventSlim`, and whether the wake is harmless is the runtime's detail. No fact is written for it, and #8 has none. Rule 3a proves the form is gone, and its planted cases prove the rule sees it.

**The census.** The planted theory, the floor fact, and for each dated exception: delete it before its fix and see the tree fact name that fixture.

## Docs and registers

None. No contract row, announcement, chord or matrix changes. The two new `.cs` files get the licence header (`git add`, then `python scripts/apply-license-header.py`).

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-cb4'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
$t = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
dotnet test $t -c Release --no-build --filter "FullyQualifiedName~TestWaitCensus"
dotnet test $t -c Release --no-build --filter "FullyQualifiedName~DisposingThe|FullyQualifiedName~PassesThrough"
# every fact that uses a changed fixture (desktop free: ReadingFocusTests needs the foreground)
dotnet test $t -c Release --no-build --filter "FullyQualifiedName~ReadingFocusTests|FullyQualifiedName~ReadingDiagramTests|FullyQualifiedName~ReadingFindSurfaceTests|FullyQualifiedName~GraphTableTests|FullyQualifiedName~FinalMergeDeltaTests|FullyQualifiedName~PumpedSaveReentrancyTests|FullyQualifiedName~SaveChainAdmissionTests|FullyQualifiedName~CommandPaletteTests|(FullyQualifiedName~ConnectionsLeafTests&FullyQualifiedName!~ConnectionsLeafTests.TheModelOf)"
$cs = git diff --name-only origin/main -- '*.cs'
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs
```

The PR body quotes each new fact's red result and its green one. CI: `windows.yml` on the PR (the app job, and both model shards for #9's park). AT: none.

## Acceptance

- [ ] The five forced facts and the three pass-through facts were each seen failing on today's teardown, then pass.
- [ ] `TestWaitCensus` passes: the floor, 3a with no exception, 3b with only lifecycle-owned entries, each with a reason; every planted case answers as listed.
- [ ] Each dated exception was deleted in the commit that fixed its fixture, after the tree fact was seen naming that fixture without it.
- [ ] No product file changed. Every fact in the nine changed fixture files passes locally; the lane is green, model shards included; the run id is in the PR body.
- [ ] The PR body lists the nine fixtures by file with what each now waits for, and says which race no fact forces and why.
- [ ] Rule 6: the PR body says #1673 was found by review and never seen failing on a run, so its evidence is the forced fact's red result, quoted there.
- [ ] The PR body has "Found and fixed here" (#8, #9 and anything else, each with its failing test or "census only"; or "none") and ends with the count: found on the way, fixed here, deferred, filed (rule 8).
- [ ] `Closes #1673`. `not delivered: a fact for a worker still waking while its event is disposed` (no seam; rule 3a covers the form).
- [ ] No red team: tests only (rule 9).

## Brief

Tests only; no product change. Read the reading's #1673 section, cluster shape 2, instance 11 and census rule 3; then each fixture's `Dispose` at the lines in this spec's table; `RCVM:606-792`, `:899-906`, `:1947-1962`, `:2028-2114`; and the models (`T/Censuses/StaThreadCensus.cs`, `T/Support/HeldWork.cs`, `T/GraphDiagramTests.Lineage.cs:24-52`). Build in this order. (1) `T/Support/TeardownWitness.cs`: a `TaskCompletionSource` latch with `Hold()` for a seam, `DrainBeforeTheSessionGoes(Func<Task>)` (the signal and `PumpUntilDrained` in one call) and `SessionGone()` for a fixture, `LateRuns` and `DrainedBeforeGone` for a fact. (2) `T/Censuses/TestWaitCensus.cs`: a shared walk of both test projects with a floor, rules 3a and 3b as Design 4 words them, the planted theory, each rule a block of its own so CB-6 and CB-7 append at the end. Land it first, with today's nine instances as dated exceptions. (3) One commit per fixture: the failing fact, the fix, the exception deleted. The fixes are Design 2 (five fixtures drain between the release and the session) and Design 3 (four types only release, or stop disposing their events); the facts are the eight in Tests. Check CR counts before editing each file; run `dotnet format --verify-no-changes --include` on the changed files; run every class that uses a changed fixture with the desktop free. A census hit this spec does not list is fixed here (rule 3) and named under "Found and fixed here". PR body: the nine fixtures, each fact's red and green result, the race no fact forces, the count, `Closes #1673`. Commit: `test(windows): no fixture tears down under work it released, and a census holds the rule`.
