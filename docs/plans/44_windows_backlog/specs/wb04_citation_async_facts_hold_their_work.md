# WB-4 — The citation interleaving facts hold the work they reason about instead of betting on the scheduler

Issues: #1316 #1285 #1315. Size S–M. Lanes: windows. AT: — (test-only). Wave: W2, slot 1. Depends on: nothing. Parallel with: WB-5, WB-6. Decision: none.

## Goal

`CitationAsyncInterleavingTests` passes 50 consecutive isolated runs on Windows and asserts nothing about what "has not landed yet" without first holding that work on a seam. #1285's fact and its sibling carry 21cfc8e's hold; #1315's three facts get theirs, one of them through a new `FilesCitingViewModel` seam; and the three issues close in one PR. No product behaviour changes.

## Current state on main

**Plan vs code:** plan §2.2 says 21cfc8e's hold is "unheld" at `:411` and `:445`. The hold is indeed absent, but c247b865 (#1328) — the same merge that fixed #1320 — rewrote both facts into a different shape: they now run under `StaThread.RunPumped` with a serialized dispatcher context (`Assert.True(PanelWorkScheduler.CurrentContextIsUiDispatcher(), …)`, `CitationAsyncInterleavingTests.cs:413-416` and `:447-450`) and quiesce through `PumpedDispatcher.PumpUntilDrained(QuiesceAsync(workspace))` (`:431`, `:460`), with a 60 s bound. Under that context the publish is posted to the body's own dispatcher and cannot run until the body pumps (`:422-423`), so the ordering 21cfc8e fixed by a hold is fixed here by the context. `git log -S RunPumped` on the file names c247b865 as the commit. 21cfc8e (one commit on 023f658, `origin/claude/laughing-meitner-86n5do`) makes both facts `async Task` and will not apply; it conflicts textually and semantically.

- `:411` `ADeferredSummaryDoesNotAnswerForADifferentNote`, `:445` `ADeferredSummaryStillAnswersForTheNoteItWasAskedAbout`: pumped, no `InterleaveForTests` hold, no 50× Windows evidence recorded anywhere (`docs/plans/40_nvda_matrix_remediation_contracts.md:689` records the 5-of-5 failure on `main` 7cfd0a37; that is a frozen approval record and stays).
- #1315's three, as the issue describes them: `:372` `TheCitationsLeafDoesNotClaimEmptinessWhileLoading` (sync; the comment at `:389-390` claims the post "cannot run until the test yields"); `:512` `ASecondReloadIsRefusedWhileTheFirstIsStillRunning` (`:523-524` the same claim; the window is one statement at `:526`); `:325` `TheFilesCitingSheetAppearsWithItsCountAlreadyKnown` (`:337` `Assert.Null(workspace.FilesCiting)` right after `OpenFilesCiting`).
- Seams: `Panels/CitationsPanelViewModel.cs:116` `internal Action? InterleaveForTests`, invoked between the read and the publish at `:201` and `:209`; `WorkspaceViewModel.Citations.cs:188` `SeedInterleaveForTests`, invoked first in `ReadAndSeedSources` (`:192`); `Panels/FilesCitingViewModel.cs:79-100` `Load()` runs `ListFilesCiting` (`:88`) and `Post(() => Publish(…))` (`:98`) with no seam; `WorkspaceViewModel.Citations.cs:485-500` `OpenFilesCiting` constructs the sheet and calls `sheet.Load()` at `:499` — the sheet is unreachable before that.
- The house hold pattern: `HistoryAsyncInterleavingTests.cs:98-110` (`ManualResetEventSlim`, `gate.Wait(TimeSpan.FromSeconds(10))` in the hook, `Set`, clear the hook, drain). The mutation rule: `docs/plans/23_citation_surfaces_contracts.md:282`.
- Mutation targets exist: `CitationsPanelViewModel.cs:77` (`NotifyStateChanged()` re-raise in the `IsLoading` setter); `WorkspaceViewModel.Citations.cs:382` (`!_reloadInFlight` in `CanExecute`), `:384-424` the flag.
- The "known failures" notes #1316 asks to clear live in merged W7-7 PR bodies, not in the tree; nothing in `apps/` or `docs/` lists the fact as a known failure except the frozen record above.

## Reference

None (Windows-only test infrastructure). The behavioural specs are #1316's "What changed in 21cfc8e" and #1315's three fixes; the pattern is `HistoryAsyncInterleavingTests.cs:98-110`.

## Design

1. **Port, do not cherry-pick, 21cfc8e — with an observed hold.** One helper in the class, **new** `HeldWork` (`IDisposable`): wraps a `ManualResetEventSlim`, exposes `Action Hook` = `() => { if (!_gate.Wait(TimeSpan.FromSeconds(10))) Volatile.Write(ref _timedOut, 1); }`, `Release()` (sets the gate and nulls the hook it was installed on), `AssertHeldUntilReleased()` (fails the fact with "the hold timed out before the fact released it" when `_timedOut` is set), and `Dispose()` that releases in all paths. The hook cannot throw on the worker (`CitationsPanelViewModel.cs:201` sits inside a `try` whose `catch` at `:209` invokes the hook again and publishes the error, so an exception there would be swallowed into a publish); it records, and the fact observes. Inside each pumped body (`:413`, `:447`): `using var held = new HeldWork(); workspace.Citations.InterleaveForTests = held.Hook;` before `OpenPath("cited.md")`; in a `try { … } finally { held.Release(); }` the fact does its work — after `OpenPath("other.md")` (the wrong-note fact) or after the `Assert.Null` (the still-answers fact) it calls `held.Release()`, pumps `PumpUntilDrained`, then `held.AssertHeldUntilReleased()` *before* the final assertions, so a hold that silently expired on a loaded runner is a failure, not a pass that raced. The hold runs on the pool thread the leaf's worker uses, so it is independent of the pumped context. Replace the `:422-423` comment with 21cfc8e's ("Deterministic: a held load cannot publish") and carry its doc-comment paragraph (`:409-417` in the 21cfc8e diff) onto `:396-410`.
2. **#1315 fact 1** (`:372-394`): `using var held = new HeldWork(); leaf.InterleaveForTests = held.Hook;` before `NoteChanged`, assert `IsLoading`, `!ShowEmptyState`, `lastToldShowEmpty == false` inside `try`, `held.Release()` in `finally`, `await leaf.DrainForTests()`, `held.AssertHeldUntilReleased()`; the fact becomes `async Task`; the `:389-390` comment is replaced by "a held publish cannot land".
3. **Fact 2** (`:512-534`): the same shape over `workspace.SeedInterleaveForTests` before `ReloadBibliography()`, assert `CanExecute` is false, release in `finally`, then the existing `SeedWorkForTests` loop, then `AssertHeldUntilReleased()`; fix `:523-524`.
4. **Fact 3** (`:325-352`): **new** `internal Action? InterleaveForTests { get; set; }` on `FilesCitingViewModel`, invoked between `:88` and `:98` inside `Load()`'s worker (both the success path and the `catch`); **new** `internal Action? FilesCitingInterleaveForTests { get; set; }` on `WorkspaceViewModel` (the Citations partial), copied onto the sheet in `OpenFilesCiting` before `sheet.Load()` (`:498-499`). The fact holds it through `HeldWork`, asserts `FilesCiting` is null, releases in `finally`, drains, asserts the hold held.
   The same `HeldWork` contract (observed timeout, release in `finally`) is the house pattern from here on; WB-7 copies it, not the bare `Wait`.
5. **50× confirmation** with #1316's loop (below), recorded in the PR body with the machine and the build. Comment #1285 and #1316 with the result; delete `claude/laughing-meitner-86n5do` and `claude/1285-deferred-summary-wip` after merge (owner step; the WIP branch's Wine harness is reference only).
6. Deliberately not changed: product code apart from the two seams; the pumped shape of the two facts; the `docs/plans/40` record at `:689`.

## Tests

The five facts above keep their names. Each is mutation-verified per `23_citation_surfaces_contracts.md:282`:

- `ADeferredSummaryDoesNotAnswerForADifferentNote` — restore the identity-less parked handler (`WorkspaceViewModel.Citations.cs` park at the `OpenCitationSummary` defer; see #1314's lines `:281-286`, `:386-402`): fails with other.md's counts.
- `ADeferredSummaryStillAnswersForTheNoteItWasAskedAbout` — a parked press that never delivers: fails at `Assert.NotNull`.
- `TheCitationsLeafDoesNotClaimEmptinessWhileLoading` — remove `NotifyStateChanged()` at `CitationsPanelViewModel.cs:77`: fails at `lastToldShowEmpty`.
- `ASecondReloadIsRefusedWhileTheFirstIsStillRunning` — drop `!_reloadInFlight` at `WorkspaceViewModel.Citations.cs:382`: fails at `CanExecute`.
- `TheFilesCitingSheetAppearsWithItsCountAlreadyKnown` — show the sheet before its lookup lands (set `FilesCiting = sheet` in `OpenFilesCiting` instead of waiting for `Loaded`): fails at `Assert.Null`.
- `HeldWorkTests.AHoldThatExpiresFailsTheFactAndAHoldThatIsReleasedDoesNot` — a `HeldWork` whose hook waits past a 100 ms test bound reports `AssertHeldUntilReleased()` as a failure; one released first does not. Mutation: ignore `Wait`'s result.
- Negative: with any fact's `Release()` removed, the hook expires at 10 s and `AssertHeldUntilReleased()` fails the fact with the hold's message, not a hang (`WorkspaceViewModel.Dispose` does not drain citation work; #1316).

No FlaUI journey; no census.

## Docs and registers

None. `docs/plans/40:689` stays as written (#1316). Comments on #1285 and #1316 with the 50× line.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb4'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release   # Error(s)=0
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
$filter = 'FullyQualifiedName~CitationAsyncInterleavingTests|FullyQualifiedName~HeldWorkTests'
$n = (dotnet test $tests -c Release --no-build --list-tests --filter $filter | Select-String '^\s{4}\S').Count; if ($n -lt 16) { throw "filter selects $n facts" }   # 15 in the class + HeldWorkTests
$fails = 0
foreach ($i in 1..50) {
  dotnet test $tests -c Release --no-build --filter $filter | Out-Null
  if ($LASTEXITCODE -ne 0) { $fails++; "run $i failed" }
}
"failures: $fails of 50"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include apps/slate-windows/tests/SlateWindows.Tests/CitationAsyncInterleavingTests.cs apps/slate-windows/tests/SlateWindows.Tests/Support/HeldWork.cs apps/slate-windows/tests/SlateWindows.Tests/HeldWorkTests.cs apps/slate-windows/src/SlateWindows/Panels/FilesCitingViewModel.cs apps/slate-windows/src/SlateWindows/WorkspaceViewModel.Citations.cs
```

Run the loop once more with the machine loaded (a parallel `cargo build` is enough). CI lanes: `windows.yml`. AT pass: none.

## Acceptance

- [ ] `failures: 0 of 50` quiet and loaded, recorded in the PR body.
- [ ] Five mutations each kill their fact; `HeldWork` observes its timeout and releases in `finally`; the two seams exist and are `internal`.
- [ ] No `cannot run until` scheduling claim remains in the file (`grep -n "cannot run until" CitationAsyncInterleavingTests.cs` is empty).
- [ ] `Closes #1316`, `Closes #1285`, `Closes #1315`; comments on #1285 and #1316; the two branches deleted after merge.
- [ ] Not delivered: nothing.

## Brief

Read `apps/slate-windows/tests/SlateWindows.Tests/CitationAsyncInterleavingTests.cs:320-400`, `:405-470`, `:505-535`, `apps/slate-windows/src/SlateWindows/Panels/FilesCitingViewModel.cs:70-110`, `Panels/CitationsPanelViewModel.cs:110-215`, `WorkspaceViewModel.Citations.cs:183-196` and `:485-510`, `tests/SlateWindows.Tests/HistoryAsyncInterleavingTests.cs:95-110`, and `git show 21cfc8e` (fetch `origin/claude/laughing-meitner-86n5do`). Build: `tests/SlateWindows.Tests/Support/HeldWork.cs` — `IDisposable` with `Action Hook` (waits a `ManualResetEventSlim` 10 s and records a timeout instead of throwing, because `CitationsPanelViewModel.cs:201/:209` would swallow a throw into a publish), `Release()`, `AssertHeldUntilReleased()`, `Dispose()` releasing; `internal Action? InterleaveForTests` on `FilesCitingViewModel`, invoked between `ListFilesCiting` (`:88`) and `Post` (`:98`) on both paths; `internal Action? FilesCitingInterleaveForTests` on `WorkspaceViewModel`, copied onto each sheet in `OpenFilesCiting` before `sheet.Load()` (`:499`). Facts (all existing names kept; `main`'s two deferred-summary facts are `StaThread.RunPumped` since c247b865, so port the hold into the pumped bodies rather than cherry-picking): `ADeferredSummaryDoesNotAnswerForADifferentNote` (held cited.md load, move to other.md, release in `finally`, pump, hold held, no sheet, `Citations.Path == "other.md"`); `ADeferredSummaryStillAnswersForTheNoteItWasAskedAbout` (same hold; sheet appears with `Total == 2`); `TheCitationsLeafDoesNotClaimEmptinessWhileLoading` (hold on `leaf.InterleaveForTests`; `IsLoading`, `!ShowEmptyState`, last told `false`; now `async Task` with `DrainForTests`); `ASecondReloadIsRefusedWhileTheFirstIsStillRunning` (hold on `SeedInterleaveForTests`; `CanExecute` false while held); `TheFilesCitingSheetAppearsWithItsCountAlreadyKnown` (hold on the new seam; `FilesCiting` null while held, then named with its count); `HeldWorkTests.AHoldThatExpiresFailsTheFactAndAHoldThatIsReleasedDoesNot`. Mutations, each must fail its fact: the identity-less parked handler restored; a parked press that never delivers; `NotifyStateChanged()` removed at `CitationsPanelViewModel.cs:77`; `!_reloadInFlight` dropped at `WorkspaceViewModel.Citations.cs:382`; the sheet shown before `Loaded`; `Wait`'s result ignored in `HeldWork`. No scheduling-claim comment ("cannot run until") may remain. Registers: none (`docs/plans/40:689` is a frozen record and stays). No decisions pending; nothing deferred. Done when `failures: 0 of 50` quiet and again under load (a parallel `cargo build`) is recorded in the PR body, the six mutations are recorded, `dotnet format --verify-no-changes --include` is clean on the five touched files, and `windows.yml` is green; then `Closes #1316`, `Closes #1285`, `Closes #1315`, comment both #1285 and #1316 with the loop line, and ask the owner to delete `claude/laughing-meitner-86n5do` and `claude/1285-deferred-summary-wip`. Commit: `test(windows): the citation interleaving facts hold their work on a seam and pass fifty isolated runs`.
