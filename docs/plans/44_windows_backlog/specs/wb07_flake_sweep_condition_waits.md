# WB-7 — The Windows lane waits on conditions, not the clock: the eleven #1323 items, the highlight census, a cooperative STA timeout, and deterministic structural-batch rendezvous

Issues: #1306 #1321 #1294 #1323 (the Windows items; the mac and Rust items are plan 43 PRs 28 and 37, RD17). Size M–L. Lanes: windows, rust. AT: — (test-only). Wave: W3, slot 1. Depends on: nothing (WB-4 is a separate class; WB-1 makes the loops safe to run). Parallel with: WB-8, WB-9. Decision: none. Codex adversarial review per §4 rule 5 (size M or larger).

## Goal

A PR with no Windows change no longer turns the Windows lane red on a loaded runner. Every fact in the list below waits for the condition it asserts, with one generous outer bound; wall-clock budgets become ratios or are benchmark-tagged and kept out of the gate; the one fact that reads the machine clipboard reads a seam; a timed-out STA body can stop cooperatively so one slow walk is one failure, not 252; and the three `structural_batch` tests print what they were waiting on when they fail.

## Current state on main

Grouped by file. Where the issue and the code disagree, the code wins and it is said.

**`apps/slate-windows/tests/SlateWindows.Tests/Censuses/CanonicalHighlightCensus.cs`** (#1306) — `LoadedCoordinatorQuiescesAndRefreshesForEditScrollAndResize` `:143-199`: `RunDispatcherFor(TimeSpan.FromMilliseconds(100))` then `Assert.True(RefreshCountForCensus > …)` after the edit (`:184-185`), the scroll (`:191-192`) and the resize (`:197-198`); the two 160 ms negative waits at `:178-179` and `:200-201` are the quiescence checks the issue keeps.

**`apps/slate-windows/tests/SlateWindows.Tests/Support/StaThread.cs`** (#1321) — `Run<T>` `:108-160`: on `Join(limit)` failing it posts `BeginInvokeShutdown` (`:141`), waits `UnwindGrace` (10 s, `:66`), and otherwise records the thread in `s_wedged` (`:149-151`) and throws (`:153-154`); `Wedged()` `:169-185` fails every later fact while a wedged thread lives, with `what` = the timeout message only. No flag or token reaches the body. `RunPumped` `:102-103`. `ReadingViewTests.cs:681` `SyntheticAttributeWalkSurvivesHugeDocuments` is the non-pumping body that cascaded (#1304); the default bound is 60 s (`ReadingViewTests.cs:4244`).

**`crates/slate-core/src/session/tests/structural_batch.rs`** (#1294) — `LIVENESS = 30 s` (`:28`) already covers the liveness waits (`:138 :169 :286 :760 :1082 …`); the doc at `:20-27` deliberately keeps the *negative* probes short. Negative waits: `recv_timeout(200 ms)` `:1094`, `:1162`, `:1310` (`early_second`, in `structural_operations_in_different_vaults_remain_independent`, `:1188`), `recv_timeout(100 ms)` `:3299` (`structural_undo_waits_for_failed_recovery_barrier_then_fails_closed`, `:3232`); `thread::sleep(150 ms)` + `try_wait` `:1400`. *Issue vs code:* under load these spuriously **pass** (a slow runner only makes "still blocked" more convincing); they cannot be the timeouts CI saw. `successful_batch_finalizes_one_undo_row_and_no_inflight_residue` `:1683-1719` has no timing at all, and CI run 36334818219 captured no panic text (the issue). `report_fixture_panic` (`:30-37`) prints context only on panic. The fix is diagnostics first (what each `expect` was waiting on, the provider call log, the inflight dir), then the rendezvous.

**#1323's Windows items:**
- `GraphDocumentTests.RequestLineage.cs` — 37 `CurrentForTests!` dereferences (`:137`, `:143`, `:175`, `:206`, …); the probe facts `:877` `AProbeDuringThePresetsPairSpeaksTheHeadlineOverTheNewerGeneration`, `:908` `AProbeDuringTheActivationsPairSpeaksTheSummary`, `:934` `AProbeDuringANeedlesPairSpeaksTheCount`, `:473` `ASortDuringAPairOverAStaleSnapshotRefetchesAndAdoptsWithGridSortedThenTheCount` each `PumpUntil(() => document.SeqForTests > token.Seq)` and then read `document.CurrentForTests!` on the next statement (`:894-895`, `:921-922`, `:951-952`): the replacing pair's unparked fetch can publish and clear `CurrentForTests` between the two.
- `GraphTableTests.cs:897` `TenThousandRowsStayVirtualisedAndTheActionInventoryStaysThree`; the bound is a parameter (`:1075`), asserted `live <= bound` at `:1133-1136`; 87–89 against 85 under load.
- `GraphEndToEndTests.cs:942` `LargeGraphOpensLaysOutPansAndStepsUnderBudget`: `openMs < 500` `:952`, `tickMs < 100` `:969`, `rebuildMs < 500` `:988`, `hopMs < 100` `:1002`; `BENCH` lines at `:968`, `:1015`. Failed twice on Namespace runners for a mac-only merge (2026-10-16 comments).
- `CommandPaletteTests.cs:131` `RecomputeScalesLinearlyInRowCount`: already a ratio (`ratio <= 2.5`, `:162-166`) over 21 measured rounds; 22 s under load.
- `ReadingViewTests.cs:3430` `CodeCopyButtonCopiesTheSourceAndAnnounces` reads `System.Windows.Clipboard.GetText()` (`:3466`); the seam exists — `Reading/ReadingContentViewModel.cs:397` `internal Action<string>? ClipboardForTests`, used at `:379` and already by `ReadingViewTests.cs:3744`.
- `ReadingViewTests.cs:681` the 60 s walk — the cost is #1320 (closed by WB-0); the cascade is #1321 (here).
- `ReadingFocusTests.cs` `ReleaseProjection` `:4420-4430`: *issue vs code* — it already waits for the specific in-flight projection (`LetTheHeldFetchRunOut(HeldFetchEnd.Published)`, predicate `Published()` at `:4468`), the #1304 fix. The ten call sites (`:126 :228 :287 :311 :340 :787 :830 :837 :899 :970`) go through it. Re-verify each; expected outcome "already fixed", recorded in the PR body.
- `ShellAccessibilityTests.cs:11876-11889` `OpenCanvasFromTree`: waits 30 s for `FilesTree` then searches its `TreeItem` descendants **once** (`:11880-11886`); `WaitForTreeItems` (`:11891`) exists and is unused by it.
- `CommandPaletteTests.Invoking.cs:456` `TheUnsavedChangesPromptOnCloseHearsNothingFromThePalette`: `Assert.NotNull` on the captured prompt tuple after a 5 s wait (1 of 4,834 on a Namespace runner).
- The lane's filter: `windows.yml:528-534` (`--filter "FullyQualifiedName!~ConnectionsLeafTests.TheModelOf"`).

## Reference

None (test infrastructure). The house patterns: `PumpedDispatcher.PumpUntil(condition, bound)` (`PumpedDispatcher.cs`), the hold pattern of WB-4, `WaitForElement`/`WaitForTreeItems` in the gate, and the `structural_batch.rs:20-27` doctrine (liveness waits generous, negative probes explicit).

## Design

- **CanonicalHighlightCensus.** Add `PumpUntilRefreshed(coordinator, baseline)` = pump the dispatcher until `RefreshCountForCensus > baseline` with a 5 s bound and a message naming the step; use it at `:184`, `:191`, `:197`. Keep `:178-179` and `:200-201` verbatim.
- **StaThread.** New `[ThreadStatic] private static CancellationTokenSource? t_timeout` created per `Run` on the STA thread; **new** `internal static CancellationToken Current => t_timeout?.Token ?? CancellationToken.None` and `internal static bool TimedOut => Current.IsCancellationRequested`; at `:139` (before `BeginInvokeShutdown`) cancel it. `Run` gains an optional `string? factName = null` filled by `[CallerMemberName]` from `RunSta`/`RunPumped` callers; `s_wedged` stores it and `Wedged()`'s message becomes "failed because `<factName>` (thread N) is still unwinding after its timeout: <what>". Long-walk facts poll between phases: `SyntheticAttributeWalkSurvivesHugeDocuments` checks `StaThread.TimedOut` between its three queries and `HugeDocumentSyntheticQueriesHaveBoundedCost` likewise; the walk itself (`HeadingStyleText.cs`) is product code and is not changed. The timeout stays a failure; no bound is loosened.
- **structural_batch.rs.** (a) Diagnostics: every `expect` on a `recv_timeout(LIVENESS)` in the three named tests becomes `unwrap_or_else(|_| panic!("rendezvous `<name>` timed out after {LIVENESS:?}; provider calls: {:?}; inflight: {:?}", calls, inflight_listing))` through one helper `await_rendezvous(rx, "name", || context())`, so the next CI failure names the wait. (b) Rendezvous: the negative probes at `:1310` and `:3299` gain a positive witness before the short wait — the blocked thread signals "entered the wait" on a channel (the barrier fixtures already expose `entered_rx`/`release_tx`; add the same pair to the second undo and to the blocked undo) and the test waits `LIVENESS` for *that*, then asserts the result channel is still empty with the short window. `:1400`'s `sleep(150)` + `try_wait` becomes a `blocked` marker file the child writes when it reaches the open (beside `SLATE_STRUCTURAL_LOCK_CHILD_READY`, `:1390-1391`), waited with `LIVENESS`. `successful_batch_…` keeps no timing and gains the diagnostics only. Keep `:20-27`'s doctrine; update its text to say the negative probes now follow a positive witness.
- **GraphDocumentTests.RequestLineage.cs.** Capture the token inside the predicate: `GraphLoadToken? replacing = null; PumpUntil(() => document.CurrentForTests is { } t && t.Seq > preset.Seq && (replacing = t) is not null)`; or, more honestly, a `document.PublicationGateForTests` the `Park` fixture already models — extend `Park(document, n)` so the replacing fetch parks too, read `CurrentForTests` while parked, then release. Apply to the four facts and audit the remaining 33 `!` reads for the same shape.
- **GraphTableTests.** The bound becomes a function of the viewport: `realised rows + cacheBefore + cacheAfter + 2` read from the panel at assertion time (`:1133-1136` already prints them), not the constant 85; the assertion message unchanged in shape.
- **GraphEndToEndTests and CommandPaletteTests.RecomputeScalesLinearly.** `[Trait("kind", "benchmark")]`; the lane filter at `windows.yml:532` adds `&kind!=benchmark`; the facts keep their `BENCH` lines and run locally and in a nightly or by hand. `RecomputeScalesLinearly` is already a ratio; it is tagged for its runtime (22 s), not its shape.
- **ReadingViewTests.CodeCopyButton…** set `tab.Reading!.ClipboardForTests = text => copied = text` before the click (the `:3744` shape); assert `copied`; never touch the machine clipboard.
- **ReadingFocusTests.ReleaseProjection.** Verify each of the ten sites against `:4420-4430`; no change expected; record.
- **ShellAccessibilityTests.OpenCanvasFromTree.** Replace the single `FindAllDescendants` with `WaitForTreeItems(automation, filesTree, atLeast: 1)` then the `FirstOrDefault` by name inside a `SpinWait.SpinUntil` (30 s) with the same exception text.
- **CommandPaletteTests.Invoking.cs:456.** The 5 s wait becomes `PumpedDispatcher.PumpUntil(() => duringPrompt is not null, TimeSpan.FromSeconds(30))`; the assertion follows.
- Deliberately not changed: `PanelWorkScheduler`, `PumpedDispatcher`, product code, any positive budget's *meaning*.

## Tests

New facts (`Support/StaThreadTests.cs`, beside `StaThreadCensus`):
- `StaThreadTests.ABodyThatPollsTimedOutUnwindsWithinTheGraceAndLaterFactsPass` — a body spinning on `!StaThread.TimedOut` with a 100 ms bound exits; `Wedged()` is null afterwards. Mutation: never cancel the token.
- `StaThreadTests.ABodyThatIgnoresTimedOutStillWedgesAndFailsLaterFacts` — a body in a blocking `Thread.Sleep` loop wedges; `Wedged()` is non-null until it ends. Mutation: skip the `s_wedged.Add`.
- `StaThreadTests.TheCascadeMessageNamesTheRootFact` — `Wedged()` contains the root fact's `factName` and "still unwinding". Mutation: drop the name from the message.
- `CanonicalHighlightCensus` keeps its fact; mutation: make the coordinator never refresh after an edit — the fact fails at the 5 s bound naming "edit".
- `structural_batch.rs`: `rendezvous_diagnostics_name_the_wait_that_timed_out` — a fixture whose release is never sent panics with "rendezvous `second-entered` timed out" and the provider calls. Mutation: drop the helper's message.
- Each converted #1323 fact keeps its name; the mutation for each is the inverse of its fix (e.g. unpark the replacing fetch before the read → the capture-in-predicate still reads a token; the clipboard seam left null → the fact fails on `copied` null).
- FlaUI: `OpenCanvasFromTree`'s change is exercised by `CanvasSurfaces_TableGridSortSelectionAndActivation_AreClean` and `Canvas_ContextMenus_OpenTheCardMenuByKeyboard` (gate).
- Census: `StaThreadCensus` (`Censuses/StaThreadCensus.cs:58`) keeps pinning that helpers forward to `StaThread.Run`/`RunPumped`; it gains the rule that a `Run` with a custom `timeout` over 30 s names its `factName`.

## Docs and registers

- `structural_batch.rs:20-27` doctrine paragraph updated.
- `windows.yml:532` filter (the benchmark tag); a one-line note in `docs/plans/18_windows_port/w_c_matrix.md`'s gate notes is not needed (no §W-C row changes).
- Comment on #1323 listing the eleven items and their dispositions ("already fixed" for `ReleaseProjection`).
- No contract rows, designations or ledgers.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb7'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~StaThreadTests|FullyQualifiedName~CanonicalHighlightCensus|FullyQualifiedName~GraphDocumentTests.AProbe|FullyQualifiedName~GraphDocumentTests.ASortDuringAPair|FullyQualifiedName~TenThousandRows|FullyQualifiedName~CodeCopyButton|FullyQualifiedName~TheUnsavedChangesPromptOnClose|FullyQualifiedName~SyntheticAttributeWalk"
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter "kind=benchmark"   # still runs by hand
# the whole unit project three times under load (cargo build -p slate-core in parallel): zero failures
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <every touched .cs>
cargo test -p slate-core structural_batch -- --nocapture   # per-worktree target dir
cargo test -p slate-core
make ci
dotnet test apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj -c Release --no-build --filter "FullyQualifiedName~Canvas"   # NVDA closed, desktop free
```

CI lanes: `windows.yml` (unit, shards, gate), `rust.yml` (the Rust change). AT pass: none.

## Acceptance

- [ ] The three `StaThreadTests` facts and the Rust diagnostics fact fail first / under mutation.
- [ ] `grep -n "RunDispatcherFor(TimeSpan.FromMilliseconds(100))" CanonicalHighlightCensus.cs` is empty; `:178-179` and `:200-201` unchanged.
- [ ] No `CurrentForTests!` read follows a `PumpUntil` on the next statement in the four probe facts.
- [ ] `GraphEndToEndTests.LargeGraph…` and `RecomputeScalesLinearly…` carry `kind=benchmark` and the lane filter excludes them.
- [ ] `CodeCopyButton…` no longer calls `System.Windows.Clipboard`.
- [ ] `OpenCanvasFromTree` waits for the row; `TheUnsavedChangesPromptOnClose…` pumps on the condition.
- [ ] Three loaded full runs of the unit project with no failure, recorded in the PR body.
- [ ] `Closes #1306`, `Closes #1321`, `Closes #1294`. #1323: `Closes #1323` only if plan 43 PRs 28 and 37 have merged and the issue has been retitled to the Windows list (closure matrix); otherwise `Refs #1323` and the owner closes by hand after the retitle.
- [ ] Not delivered: the mac fact `LeafContextMatrixTests.testSwitchPaneFocusRebindsLeaves` and `many_unique_citations_stay_subquadratic` (PRs 28 and 37); `ReleaseProjection` if confirmed already fixed (say so).

## Brief

Read `CanonicalHighlightCensus.cs:143-205`, `Support/StaThread.cs:96-190`, `structural_batch.rs:20-37`, `:1290-1330`, `:1385-1405`, `:3285-3310`, `:1683-1719`, the eleven #1323 sites listed under "Current state", and `windows.yml:526-534`. Convert each wall-clock wait to a condition wait or a benchmark tag as the Design says, add the cooperative timeout to `StaThread` with the root-fact cascade message, add the rendezvous diagnostics and positive witnesses to the three Rust tests, and write the facts and mutations. Done when the new facts pass, three loaded full runs are clean, both lanes are green, and the #1323 comment lists every item's disposition. Commit: `test(windows): the lane waits on conditions, a timed-out STA body stops cooperatively, and structural_batch names the rendezvous it was waiting on`.
