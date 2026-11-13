# CB-1 — A fact's thread leaves no WPF exit listener, and a timeout with no stack says whether the process stalled

Issues: #1826, #1843. Size M. Lanes: windows (`SlateWindows.Tests`, the app job). AT: — (test-only). Wave: C1, slot 1. Depends on: nothing (CB-0 merges first). Parallel with: CB-2, CB-4. Decision: CD1 and CD2. The owner answers both before C0 (plan §3); this spec encodes both as recommended. Interlock: none with CB-2, whose stall dump goes through the runtime's own dump path (`Environment.FailFast` and the dump variables) and does not touch `StaThread.cs`. CB-1 still leaves `Support/StaThread.cs:371-484` (the createdump path) alone. No `windows.yml` edit.

`T/` below is `apps/slate-windows/tests/SlateWindows.Tests/`.

## Goal

After the last test, the unit suite's process has a handful of `ProcessExit` handlers and exits in well under a second, so xUnit's 11-second exit limit no longer fails a run in which every test passed. A fact that times out without giving up its stack says in its message how long a heartbeat thread went without running during the body and how long the collector paused it, so the job log shows whether a stall of the whole process would explain the timeout after the dump has expired.

## Current state on main

Read at c2377210 (#1927), one commit after the reading. Nothing the reading cites has moved; `ExitWatch` is now in the tree.

- **The runner.** `T/Support/StaThread.cs:213-299` `Run<T>`. The `finally` at `:245-255` calls `ReleaseTextServices()` (`:176-202`) on the fact's own thread. It reads the dispatcher's private `ShutdownFinished` field (`:139-140`) and invokes the handlers whose owning type is in `TextServicesCleanup` (`:144-150`): `TextServicesHost`, the `TextServicesContext` and `WinEventHandler` shut-down listeners, `MediaContext`. None of the three kinds below is in it. The remarks at `:40-46` still say "Nothing runs on the fact's thread after its body"; #1875 made that untrue.
- **The census.** `T/Censuses/StaThreadCensus.cs:72-76` asserts `StaThread.TextServicesHookReadable` (`StaThread.cs:155-156`): three private members exist. The allow-list's type names are unchecked.
- **The instrument.** `T/Support/ExitWatch.cs`, the assembly fixture (`T/AssemblyInfo.cs:20`). `ReadProcessExit()` (`:128-129`) reads `AppDomain.ProcessExit`'s backing field; `NameOf` (`:147-148`) names a handler by its target's type. `Dispose` (`:151-172`) does nothing without a results folder (`:153-156`), so a local run needs `SLATE_STA_DUMP_DIR`. `WrapExitHandlers` (`:174-197`) rebuilds the list after the last test and logs the count by kind to `exit-watch.log`.
- **The measurement** (the issue, comments 8 and 9): 4,674 exit handlers on three CI runs, taking 5.6 s, 6.6 s and 10.5 s. By kind: 2,228 `MS.Internal.WeakEventTable+WeakEventTableShutDownListener`, 2,189 `MS.Internal.Data.DataBindEngine+DataBindEngineShutDownListener`, 252 `MS.Internal.PtsHost.PtsCache+PtsCacheShutDownListener`, 5 others. xUnit fails the run at 11 s: three times on `main` on 2026-11-11 and once on a PR the next day, all on Namespace.
- **What stays.** #1555's switch (`T/SlateWindows.Tests.csproj:50-59`, `T/WeakEventExitTests.cs`); `TextScaleTests.OnOwnThread` (`T/TextScaleTests.cs:1640-1655`); the app job's dump arming (`.github/workflows/windows.yml:732-747`, `:763-769`).
- **The local fail-fast** (`0xC0000602`) is not reproduced: a probe passed 15 of 15 with #1875's teardown on and 15 of 15 with it off (comment 1).
- **The timeout path.** `StaThread.cs:265-291`. `StackOf` (`:496-510`) waits 5 s (`StackBound`, `:488`) for a `Send` operation. With no stack, `DumpOnTimeout` (`:371-385`) runs createdump, which suspends every thread while it writes.
- **#1843's failure**, from the job's log as the reading quotes it: `bound 60 s … Its stack could not be taken: the body did not pump its dispatcher within 5 s. A full dump … (5082 MB private; … written in 61738ms)`. So the thread was in synchronous code, not in the settle loop, which pumps (`T/RightPaneRailTests.cs:242-248`). #1853 rewrote the fact the next day (`:30-46`; `RunSta` at `:269-270`). The dump expired: the app job's results keep 7 days (`windows.yml:815-823`).
- **The host was not idle.** `C:\slate-ci\logs\orchestrator-2026-11-05.log:183-186` (UTC−5): the Rust slot committed cache generation 112 at 02:03:31Z; the model shard's VM was provisioned at 02:03:43Z and claimed at 02:05:06Z. The fact's minute began about 02:03:51Z.
- **Retention, checked 2026-11-13.** Job logs: 90 days (the repository's setting, read with `gh api`). The host's orchestrator logs: 30 days (`docs/runbooks/self-hosted-windows-runner.md:1035`).

**Unverified, from memory of WPF's source:**

1. Each of the three listeners is also on its thread dispatcher's `ShutdownFinished` list (the base class subscribes one `HandleShutDown` to `ProcessExit`, `DomainUnload` and the dispatcher).
2. Running `HandleShutDown` unhooks it from all three.
3. Run on its own thread, each clean-up is cheap and harms no live window. `PtsCache`'s may call `GC.WaitForPendingFinalizers()`, a cost on every fact.

Design step 1 settles 1 and 2. The full local run settles 3.

## Reference

Issue #1826 comments 7 to 9 (the corrected diagnosis, the two fix options); #1875 (23632db9), the same mechanism for text services; the reading, `47_windows_core_closeout/reading/01_test_stability.md`; plan 47 §4 rules 2 and 6 to 9.

## Design

1. **The failing test, before any design is committed.** `T/StaThreadTests.cs`, `AFactsThreadLeavesNoWpfExitListenerBehind`. Count the `ProcessExit` handlers of each kind through `ExitWatch.ReadProcessExit()`. Run three `StaThread.Run` bodies; each makes a binding, a weak-event subscription and a laid-out `FlowDocument` in a shown window, and asserts as a premise that each kind's count grew on `ProcessExit` and that the kind is on its own dispatcher's `ShutdownFinished`. Then assert no kind's count is above where it started. The message gives, per kind, the count on `ShutdownFinished` before the teardown and on `ProcessExit` after, so one red run says which unverified statement is false. Red today: each count grows by three.
2. **The fix, if statements 1 and 2 hold.** A second set beside `TextServicesCleanup` with the three type names. `ReleaseTextServices` runs the text-services handlers first, as now, then the three kinds last, since the earlier teardown may touch a binding or a weak event and make a fresh table. It re-reads the list until none of the three is left, three passes at most, never running a handler twice, and skips a dispatcher that has already shut down (`OnOwnThread`'s). Window and layout handlers are still left alone.
3. **If a handler runs but stays on `ProcessExit`** (statement 2 false): the runner then removes it with the public `AppDomain.CurrentDomain.ProcessExit -= handler`, and the same for `DomainUnload`. A delegate equal by target and method is removed, so no further private member is needed.
4. **The fallback**, if the listeners are not on `ShutdownFinished`, or the full local run shows a focus regression that step 2 causes. `StaThread`'s teardown stays as it is. `ExitWatch.WrapExitHandlers` drops the three kinds from the list it rebuilds after the last test and logs `dropped N x <kind>`, with or without a results folder. The predicate is a constructor argument, so new `ExitWatchTests` facts drive it with their own kinds. The leak stays, and the PR says so under `not delivered`. Step 1's fact becomes read-only: after three such bodies, the real `ProcessExit` list holds handlers the predicate matches.
5. **The census.** `StaThreadCensus` gains `EveryCleanupTypeTheRunnerNamesIsAWpfType`: each name in both sets resolves in `WindowsBase`, `PresentationCore` or `PresentationFramework`, so a rename fails one census.
6. **The remarks** at `StaThread.cs:40-46` say what runs on the thread after its body and why windows are left alone.
7. **The stall sentence (#1843).** A new `T/Support/Heartbeat.cs`: one background thread at highest priority that takes a `Stopwatch` timestamp every 100 ms, over a pure type that records beats and answers the longest gap inside a window. `Run<T>` notes the timestamp and `GC.GetTotalPauseDuration()` before it starts the thread. On timeout with no stack (either arm of `StackOf`), and **before** `DumpOnTimeout`, whose suspension would read as a stall, it appends one of:
   - `The heartbeat thread missed 40.0 s in one gap during the body, which a stall of the whole process would explain.` (a gap of 5 s or more)
   - `The heartbeat thread kept running during the body: its longest gap was 0.1 s, so the process as a whole was not stalled.`

   then `Garbage collection paused the process for 0.3 s of the body; memory load was 71 %.` The reading comes through a seam, `LongestGapForTests`, reset in `StaThreadTests.Dispose` (`:32-37`). No sentence may contain "dump": `WithNoDumpFolderATimeoutWritesNoDump` (`T/StaThreadTests.cs:377-383`) asserts its absence. The sentences report what was measured, the heartbeat's gap and the collector's pause, and name a process-wide stall only as what a long gap would explain: one heartbeat cannot say how many other threads were blocked. A disk stall that blocks only the body's thread leaves the heartbeat running, which still rules out a frozen VM.
8. **The local fail-fast (CD1).** On a scratch commit that never reaches the PR: the issue's throwaway fact (a `ShownShell` with every sheet forced visible) and an environment switch that skips the per-thread teardown. On an interactive desktop, with the runtime's crash dump armed as the Test step arms it, run it twenty times with the teardown off and twenty with it on, recording each exit code. A crash only with it off shows the fix. No crash either way is `not delivered: the local fail-fast was not reproduced`. A crash with it on stops the close: post the dump's faulting thread on #1826.
9. **The twenty lane runs (CD1).** The first twenty app-job runs on `main` after the merge. Only runs on `main` count; the PR's own run and later PRs' runs are supporting evidence and go in the table marked as such. No rerun is bought for the count. Each run's `exit-watch.log`, read within its seven days, is a row in a table on #1826: run id, pool, handler count, seconds, and whether the run ended in `TestPipelineException`. So the PR body writes `Refs #1826`, and the issue is closed by hand, retitled, when the twentieth row is clean.
10. No change to the rail fact, to retention or to product code.

## Tests

- `AFactsThreadLeavesNoWpfExitListenerBehind` (step 1), red on the base commit.
- `ATimedOutBodyThatDoesNotPumpSaysWhetherTheProcessStalled`, on the body of the fact at `:389-412`: the seam reporting 40 s gives the stalled sentence, reporting none gives the other. Red today. A twin over `TimeOutASleepingBody` (`:323-328`) covers the no-dispatcher arm.
- `TheStallIsReadBeforeTheDumpSuspendsTheProcess`: the heartbeat seam and `RunDumperForTests` (`StaThread.cs:345`) record their order.
- `T/HeartbeatTests.cs`, on injected timestamps: the longest gap of a window; a gap that ended before the window is not counted; a gap still open at the read is; the thread is a background thread (a foreground one would hold the exit).
- The census fact, seen failing on a planted wrong name.

## Docs and registers

- `docs/runbooks/self-hosted-windows-runner.md`, "Observed timings" (`:1962`): the 2026-11-06 timeline, and rule 6's check that a single home-pool timeout is read against `C:\slate-ci\logs` before it is filed.
- A comment on #1843: the timeline and the three corrections (not the settle loop; the fact was rewritten; the host was not idle). A comment on #1826: the numbers before and after, and the probe's forty exit codes.
- No contract row, parity matrix or ledger line.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-cb1'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
# red on the base commit, green on the branch:
dotnet test $tests -c Release --no-build --filter "FullyQualifiedName~AFactsThreadLeavesNoWpfExitListenerBehind|FullyQualifiedName~SaysWhetherTheProcessStalled"
dotnet test $tests -c Release --no-build --filter "FullyQualifiedName~StaThread|FullyQualifiedName~ExitWatchTests|FullyQualifiedName~HeartbeatTests|FullyQualifiedName~WeakEventExitTests|FullyQualifiedName~TextScaleTests"
# the full run: interactive desktop, NVDA closed, hands off 35 to 45 minutes; base, then branch
$results = Join-Path $env:TEMP 'cb1-results'; New-Item -ItemType Directory -Force $results | Out-Null
$env:SLATE_STA_DUMP_DIR = $results
Start-Process pwsh -WindowStyle Hidden -ArgumentList '-NoProfile','-File','scripts/windows/foreground-sampler.ps1','-Out',"$results\foreground.log",'-StopFile',"$results\foreground.stop"
pwsh -NoProfile -File scripts/windows/guard-device-state.ps1 -- dotnet test $tests --configuration Release --no-build --filter "FullyQualifiedName!~ConnectionsLeafTests.TheModelOf&kind!=benchmark" --logger "trx;LogFileName=cb1.trx" --results-directory $results
New-Item -ItemType File -Force "$results\foreground.stop" | Out-Null
Select-String -Path "$results\exit-watch.log" 'exit handlers'
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <each .cs file the change touched>
```

The full run is the only way to see the foreground regression: CI's session has no foreground, and `StaThread.cs:40-46` records 6 to 9 `ReadingFocusTests` facts failing when a teardown went too far. Compare base and branch: totals, each failed fact, the foreground log, the duration (statement 3), the handler count and seconds. A focus fact that fails only on the branch sends the design to step 4.

CI lanes: `windows.yml`, the app job; quote `exit-watch.log` from its results artifact beside a run from before the change. AT: none.

## Acceptance

- [ ] `AFactsThreadLeavesNoWpfExitListenerBehind` was seen red on the base commit and is green. The PR body says which design shipped (step 2, 3 or 4) and what the red run showed about the unverified statements.
- [ ] The census fact fails on a planted wrong type name.
- [ ] The stall sentences were seen missing, then present; the reading precedes the dump; the heartbeat's thread is a background thread.
- [ ] The full local run on an interactive desktop, base and branch: totals, the focus facts, the foreground log, the duration. `exit-watch.log`'s handler count and seconds from both, and from a full CI run before and after.
- [ ] CD1: twenty local probe runs with the teardown off and twenty with it on, every exit code recorded; twenty rows from app-job runs on `main` on #1826 before it closes.
- [ ] Rule 6, in the PR body: a slow exit's evidence is `exit-watch.log`, with the TRX, `foreground.log` and any dump, in `slate-windows-app-results-<run>-<attempt>` for 7 days (`windows.yml:823`, unchanged by CD2); a timeout's stall sentence is in the job log for 90 days; the home host's side is `C:\slate-ci\logs` for 30 days.
- [ ] Rules 2 and 8: a "Found and fixed here" section (the stale remarks at `StaThread.cs:40-46` are its first line), and the body's last line is the count: found on the way, fixed here, deferred, filed.
- [ ] Rule 9: a fresh Claude red team with `docs/plans/24_red_team_protocol.md` before the PR opens, pointed at the private WPF members, the teardown's order and the foreground regression; findings and what was done with each are in the PR body.
- [ ] `Closes #1843`, with `not delivered: the cause of the 2026-11-06 timeout` (CD2). `Refs #1826` until step 9's table is complete; then it is closed by hand and retitled, with `not delivered: the local fail-fast was not reproduced` if step 8 ended that way. The dump arming from #1892 and #1902 stays on.
- [ ] The runbook entry and both issue comments are in. `windows.yml` is unchanged. `dotnet format --verify-no-changes` passes on the touched files; the new files carry the licence header.

## Brief

Branch `claude/cb1-exit-listeners` from `main`. Read `T/Support/StaThread.cs:122-202`, `:213-299`, `:317-385`, `:486-510`; `T/Support/ExitWatch.cs:128-197`; `T/StaThreadTests.cs:18-65`, `:323-328`, `:377-442`; `T/Censuses/StaThreadCensus.cs:68-76`; #1826 comments 7 to 9. First write `AFactsThreadLeavesNoWpfExitListenerBehind`: count the `ProcessExit` handlers (`ExitWatch.ReadProcessExit()`) whose target is a `WeakEventTable`, `DataBindEngine` or `PtsCache` shut-down listener (full names under "Current state"); run three `StaThread.Run` bodies that each make a binding, a weak-event subscription and a laid-out `FlowDocument` in a shown window, with a premise per kind that it is on `ProcessExit` and on the dispatcher's `ShutdownFinished`; assert no count grew, printing both lists by kind on failure. See it red. Then run the three kinds as a second set after the text-services handlers in `ReleaseTextServices`, re-reading until none is left (three passes at most). If a handler runs but stays on `ProcessExit`, remove it with `AppDomain.CurrentDomain.ProcessExit -= handler` (and `DomainUnload`). If the listeners are not on `ShutdownFinished`, or focus facts fail in the full local run that pass on the base, take Design step 4: `ExitWatch` drops the three kinds after the last test, and the PR says the leak stays. Add the census fact that every name in both sets resolves in WPF's assemblies; rewrite the remarks at `StaThread.cs:40-46`. For #1843 add `T/Support/Heartbeat.cs` (`git add`, then `python scripts/apply-license-header.py`) and the stall sentence as Design step 7 gives it: read before `DumpOnTimeout`, through the `LongestGapForTests` seam, no sentence containing "dump"; its facts first. Leave `StaThread.cs:371-484`, `windows.yml`, the rail fact, #1555's switch and `OnOwnThread` alone. Run the filtered facts and `dotnet format --include`; then the full suite on an interactive desktop, base and branch, as "Verification" gives it, and compare totals, focus facts, duration and `exit-watch.log`. On a scratch commit, run the issue's throwaway `ShownShell` fact twenty times with the teardown off and twenty with it on, dumps armed, exit codes recorded. Red team with `24_red_team_protocol.md` before the PR. PR body: `Closes #1843`, `Refs #1826` with no closing word near it, and each item under "Acceptance". After merge, tabulate the first twenty app-job runs on `main` on #1826, then close and retitle it. Commit: `test(windows): a fact's thread leaves no wpf exit listener, and a timeout with no stack says whether the process stalled`.
