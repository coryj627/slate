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

1. **Port, do not cherry-pick, 21cfc8e.** Inside each pumped body (`:413`, `:447`): `using var held = new ManualResetEventSlim(false); workspace.Citations.InterleaveForTests = () => held.Wait(TimeSpan.FromSeconds(10));` before `OpenPath("cited.md")`; after `OpenPath("other.md")` (the wrong-note fact) or after the `Assert.Null` (the still-answers fact): `held.Set(); workspace.Citations.InterleaveForTests = null;` then the existing `PumpUntilDrained`. The hold runs on the pool thread the leaf's worker uses, so it is independent of the pumped context and holds even if a later change moves the publish off the dispatcher. Replace the `:422-423` comment with 21cfc8e's ("Deterministic: a held load cannot publish") and carry its doc-comment paragraph (`:409-417` in the 21cfc8e diff) onto `:396-410`.
2. **#1315 fact 1** (`:372-394`): hold `leaf.InterleaveForTests` before `NoteChanged`, assert `IsLoading`, `!ShowEmptyState`, `lastToldShowEmpty == false`, release, clear, `await leaf.DrainForTests()`; the fact becomes `async Task`; the `:389-390` comment is replaced by "a held publish cannot land".
3. **Fact 2** (`:512-534`): hold `workspace.SeedInterleaveForTests` before `ReloadBibliography()`, assert `CanExecute` is false, release, clear, then the existing `SeedWorkForTests` loop; fix `:523-524`.
4. **Fact 3** (`:325-352`): **new** `internal Action? InterleaveForTests { get; set; }` on `FilesCitingViewModel`, invoked between `:88` and `:98` inside `Load()`'s worker (both the success path and the `catch`); **new** `internal Action? FilesCitingInterleaveForTests { get; set; }` on `WorkspaceViewModel` (the Citations partial), copied onto the sheet in `OpenFilesCiting` before `sheet.Load()` (`:498-499`). The fact holds it, asserts `FilesCiting` is null, releases, clears, drains.
5. **50× confirmation** with #1316's loop (below), recorded in the PR body with the machine and the build. Comment #1285 and #1316 with the result; delete `claude/laughing-meitner-86n5do` and `claude/1285-deferred-summary-wip` after merge (owner step; the WIP branch's Wine harness is reference only).
6. Deliberately not changed: product code apart from the two seams; the pumped shape of the two facts; the `docs/plans/40` record at `:689`.

## Tests

The five facts above keep their names. Each is mutation-verified per `23_citation_surfaces_contracts.md:282`:

- `ADeferredSummaryDoesNotAnswerForADifferentNote` — restore the identity-less parked handler (`WorkspaceViewModel.Citations.cs` park at the `OpenCitationSummary` defer; see #1314's lines `:281-286`, `:386-402`): fails with other.md's counts.
- `ADeferredSummaryStillAnswersForTheNoteItWasAskedAbout` — a parked press that never delivers: fails at `Assert.NotNull`.
- `TheCitationsLeafDoesNotClaimEmptinessWhileLoading` — remove `NotifyStateChanged()` at `CitationsPanelViewModel.cs:77`: fails at `lastToldShowEmpty`.
- `ASecondReloadIsRefusedWhileTheFirstIsStillRunning` — drop `!_reloadInFlight` at `WorkspaceViewModel.Citations.cs:382`: fails at `CanExecute`.
- `TheFilesCitingSheetAppearsWithItsCountAlreadyKnown` — show the sheet before its lookup lands (set `FilesCiting = sheet` in `OpenFilesCiting` instead of waiting for `Loaded`): fails at `Assert.Null`.
- Negative: with each hold's `Set()` removed the fact times out at the 10 s hook wait, not hangs (`WorkspaceViewModel.Dispose` does not drain citation work; #1316).

No FlaUI journey; no census.

## Docs and registers

None. `docs/plans/40:689` stays as written (#1316). Comments on #1285 and #1316 with the 50× line.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb4'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release   # Error(s)=0
$fails = 0
foreach ($i in 1..50) {
  dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~CitationAsyncInterleavingTests" | Out-Null
  if ($LASTEXITCODE -ne 0) { $fails++; "run $i failed" }
}
"failures: $fails of 50"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include apps/slate-windows/tests/SlateWindows.Tests/CitationAsyncInterleavingTests.cs apps/slate-windows/src/SlateWindows/Panels/FilesCitingViewModel.cs apps/slate-windows/src/SlateWindows/WorkspaceViewModel.Citations.cs
```

Run the loop once more with the machine loaded (a parallel `cargo build` is enough). CI lanes: `windows.yml`. AT pass: none.

## Acceptance

- [ ] `failures: 0 of 50` quiet and loaded, recorded in the PR body.
- [ ] Five mutations each kill their fact; the two seams exist and are `internal`.
- [ ] No `cannot run until` scheduling claim remains in the file (`grep -n "cannot run until" CitationAsyncInterleavingTests.cs` is empty).
- [ ] `Closes #1316`, `Closes #1285`, `Closes #1315`; comments on #1285 and #1316; the two branches deleted after merge.
- [ ] Not delivered: nothing.

## Brief

Read `CitationAsyncInterleavingTests.cs:320-400`, `:405-470`, `:505-535`, `Panels/FilesCitingViewModel.cs:70-110`, `Panels/CitationsPanelViewModel.cs:110-215`, `WorkspaceViewModel.Citations.cs:183-196` and `:485-510`, `HistoryAsyncInterleavingTests.cs:95-110`, and `git show 21cfc8e`. Port the hold into the two pumped facts (not a cherry-pick: `main`'s facts are `RunPumped` since c247b865), hold #1315's three facts on `leaf.InterleaveForTests`, `workspace.SeedInterleaveForTests` and the new `FilesCitingViewModel.InterleaveForTests` copied by `OpenFilesCiting`, verify the five mutations, run the 50× loop twice, and record it. Done when the loop is clean and the lane is green. Commit: `test(windows): the citation interleaving facts hold their work on a seam and pass fifty isolated runs`.
