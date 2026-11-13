# CB-2 — The model names a stalled case, a slow shard outlives 45 minutes, and a failed attempt keeps its dumps

Issues: #1624. Size S. Lanes: windows (the unit facts, one census, both model shards), runner (the Pester facts; `windows-runner-tests.yml:19` already lists `windows.yml`). AT: — (tests and CI only). Wave: C1, slot 2. Depends on: nothing (CB-0 merges first). Parallel with: CB-1, CB-4 (no shared files). Decision: none. Interlock: CB-2 edits only the `windows-model-shard` job of `windows.yml` (`:897-1070`), before CB-3 edits the app job; it adds one `Describe` to `Workflows.Tests.ps1`, which CB-3, CB-5 and CB-44 rebase over. `Support/StaThread.cs` and `self-hosted-windows-runner.md` are CB-1's: CB-2 edits neither (the stall dump below does not go through `StaThread`).

## Goal

A model case that stops advancing is named, with its phase, in the job log within about five minutes, and the run ends there with a dump of every thread. A shard that is slow but advancing is no longer ended at 45 minutes. "Re-run failed jobs" cannot replace a failed attempt's dumps.

## Current state on main

`T/` = `apps/slate-windows/tests/SlateWindows.Tests/`. Checked at c2377210.

- **#1624 was not a hang.** Run 40119788856, shard 0, home pool. The progress file read 4,477 of 6,194 cases done, `"activeOrdinal":8955`, `"phase":"arrangement"`, `"elapsedMilliseconds":2700938`. That figure is the family clock (`T/ModelTestRun.cs:60`) read when the checkpoint is published (`:232-234`), so case 8955 entered its arrangement 45 min 0.9 s into Routes. The run was advancing at 0.60 s a case, a projected 62 minutes, on a host under an all-cores stress loop (the issue's comment). Routes is one fact (`T/ConnectionsLeafTests.Model.cs:2217`, loop `:2231-2233`), and blame's timer resets only between tests (`docs/runbooks/ci-cost-and-testing-handoff.md:452-454`).
- **No stall rule.** `PublishProgress` (`ModelTestRun.cs:221-230`) records a transition and, when a report directory is set, starts the flusher (`:228-229`). `FlushProgress` (`:240-251`) writes the latest checkpoint if it changed; nothing counts ticks that saw no change. `Checkpoint` is `:350-351`, `WriteCheckpoint` `:260-292`. The report JSON is written by `Dispose` (`:294-345`), which a process ended by blame never reaches.
- **The longest bounded wait inside a case is 30 s** (`T/PumpedDispatcher.cs:51`, `Model.cs:1952-1961`); the others are 10 s (`Model.cs:1440`, `:1668-1674`). Only the synchronous FFI calls have no bound (from the reading, not re-derived).
- **The model step:** `windows.yml:1046-1058`, `--blame-hang-timeout 45m --blame-hang-dump-type mini` (`:1053`), results and hang dumps in `${{ runner.temp }}\slate-model-results` (`:1052`, `:1057`), no `DOTNET_Dbg*` variable. The job's limit is 90 minutes (`:908`).
- **The upload:** `:1060-1070`, name `windows-model-${{ github.run_id }}-${{ matrix.shard }}` (`:1066`), `overwrite: true`, the whole folder, 30 days. A rerun replaced attempt 1's artifact, dumps included. The aggregator downloads `windows-model-${{ github.run_id }}-*` (`:1099-1103`); the verifier reads `*.json` under it (`scripts/verify_windows_model_shards.py:132`).
- **The app job already does both things.** Its results are named by run and attempt (`:813-823`, 7 days; pinned at `ci/windows-runner/tests/Workflows.Tests.ps1:192-202`). Its Test step arms the runtime's dump (`:764-769`; pinned by `T/Censuses/TestHostCrashDumpCensus.cs:21-42`). That census records that `Environment.FailFast` under those variables writes a full dump (`:44-52`, measured locally) and that a mini dump lacks the managed frames (`:28-31`). Its no-secret guard covers the `windows` job only (`:88-99`); the artifact is public. None of its seven vectors (`:133-142`) appears in the model job today.
- **`Workflows.Tests.ps1` pins the model shards' routing only** (`:95-100`).
- **Timings.** A home shard took 17 min 9 s alone and 26 min 21 s beside the app job, with test steps of 11 and 19 minutes (`docs/runbooks/self-hosted-windows-runner.md:1991-1994`): about 7 minutes are outside the test step. The pilot already gives hosted shards 70 minutes (`windows-ci-pilot.yml:333`, handoff `:451-456`).
- **Job logs are kept 90 days** (the repository's retention API, read 2026-11-13).

## Reference

Reading `reading/01_test_stability.md` (#1624, and PR A). #1641 and #1892, the app job's twins. Plan 45 §4 rule 2.

## Design

1. **A stall rule on the flusher's tick** (`ModelTestRun.cs`). The constructor gains `int? stallTicks = null` and `Action<string>? onStall = null`. `FlushProgress`, under its gate, counts consecutive ticks that read the same published checkpoint. On the `StallTicks`-th, once, unless the phase is `complete` or a debugger is attached, it writes a checkpoint built from the published one (`phase` `"stalled"`, new fields `stalledPhase` and `stalledMilliseconds`, `elapsedMilliseconds` read now) and calls `onStall` with one sentence: family, shard, ordinal, cell, phase, ticks, completed of selected. With no active case it says so. Ticks, not wall time: a suspended VM (#1843's kind) is never counted, and a starved pool makes the rule slower, never early.
2. **Five minutes.** `StallBound` is 5 minutes; `StallTicks` defaults to `ceil(StallBound / progressInterval)`, 300 at one second, and to "never" for an infinite interval. That is ten times the longest bounded wait in a case and five hundred times a case on #1624's loaded host. No unit fact runs a family that long.
3. **The default `onStall` ends the process:** `ModelTestRun.EndTheProcess(sentence)` writes it to standard error, then `Environment.FailFast(sentence)`. A stalled thread is in synchronous code and cannot be unwound, and the fail-fast makes the runtime write the dump (step 5). The three model facts are not edited.
4. **`--blame-hang-timeout 70m`**; the job stays at 90. This is the plan's call; the reading suggested 80. Seventy covers #1624's projected 62 minutes and is the pilot's figure for slow runners. It leaves 20 minutes for the 7 outside the test step, the ReRoot family, blame's own dump and the uploads, so the timer, not the job limit, ends a run and its evidence is uploaded. Eighty leaves 10, and a run that slow would be cancelled with nothing kept. The cost: blame is now the backstop for a hang outside any case or a failed watchdog, and only that can bill 70 minutes where it billed 45.
5. **The model step arms the runtime's dump** as the app step does: `DOTNET_DbgEnableMiniDump: '1'`, `DOTNET_DbgMiniDumpType: '4'`, `DOTNET_DbgMiniDumpName: '${{ runner.temp }}\slate-model-results\crash-%e-%p.dmp'`, `DOTNET_CreateDumpDiagnostics: '1'`.
6. **A step prints the checkpoints:** `Show where each model family stopped`, `if: ${{ always() }}`, after the test step. It writes each `*.progress.txt` to the log and an `::error::` line for a stalled one. The log names the case whatever the test adapter printed.
7. **Two artifacts.** The report keeps its name, `overwrite: true` and 30 days; its `path` gains an exclusion for `**\*.dmp`. A new step, `Upload a failed attempt's model evidence`, `if: ${{ failure() || cancelled() }}`, uploads the whole folder as `slate-windows-model-evidence-${{ github.run_id }}-${{ matrix.shard }}-${{ github.run_attempt }}`, `if-no-files-found: warn`, `retention-days: 7`. The name does not match the aggregator's pattern. Seven days because it can hold a full dump, as the app job's does (CD2's reasoning; if the owner answers CD2 with thirty, this follows).
8. **The no-secret guard covers both jobs:** `TheJobThatPublishesTheTestProcessDumpHoldsNoSecret` becomes a theory over `windows` and `windows-model-shard`.
9. **Not changed:** the job limit, the model facts, the verifier, `StaThread.cs`, `windows-ci-pilot.yml` (the handoff keeps its 45-minute watchdog for comparison, `:368`), and `--blame-hang-dump-type mini`. That flag is a find: the census says a mini dump lacks managed frames, but the flag is also on the app job (`:777`) and the gate (`:1163`), and nothing here produces a blame dump to check. Count it as deferred to CB-46.

## Tests

Failing first. `T/ModelTestRunTests.cs`, hand-driven as `:167-209` is (`progressInterval: Timeout.InfiniteTimeSpan`, the body calls `FlushProgress`, `onStall` records):

- `ACaseThatStopsAdvancingIsReportedAsStalledWithItsCellAndPhase`: `stallTicks: 3`, three ticks in `arrangement`. One call; the sentence has the family, ordinal, cell and phase; the file reads `stalled` with `stalledPhase` `arrangement`. Two more ticks add no call.
- `ARunThatKeepsAdvancingIsNeverReportedAsStalled`: `stallTicks: 2`, a transition before every tick, then `Complete` and five more ticks. No call.
- `AStallBetweenCasesSaysNoCaseIsActive`.
- `TheDefaultStallBoundIsFiveMinutesOfTicks`: 300 at one second, 15,000 at 20 ms, never at an infinite interval.

`Workflows.Tests.ps1`, new `Describe 'windows.yml model evidence'` (red on `main`: `45m`, no evidence step):

- `It 'ends a model fact at 70 minutes inside the 90-minute job and dumps a test process that fails fast'`: the `windows-model-shard` block has the job line `timeout-minutes: 90`; its one `Test (the Connections model)` step has the whole line `--blame-hang-timeout 70m --blame-hang-dump-type mini` and step 5's four lines, verbatim.
- `It 'keeps a failed attempt''s evidence under a name with the attempt, apart from the report the aggregator reads'`: the report step still has its name then `overwrite: true`, no `run_attempt`, and the `.dmp` exclusion; the evidence step has its `if:`, its name, the folder path and `retention-days: 7`; that name does not start with `windows-model-`; the `windows-model` job still has `pattern: windows-model-${{ github.run_id }}-*`; the print step has `if: ${{ always() }}`.

The census theory has no red phase; its teeth are `TheSecretGuardSeesEachKindOfSecret` (`:103-116`). `EndTheProcess` cannot run inside a fact; the runs below prove it.

## Docs and registers

- `docs/runbooks/windows-model-ci.md`, "Evidence and performance" (`:81-101`): the stall rule; that `elapsedMilliseconds` is when the phase was entered; the two timers; the two artifacts and their days.
- `docs/runbooks/ci-cost-and-testing-handoff.md:449-456`: one sentence, that `windows.yml`'s model step now uses 70 minutes on every pool and the pilot is unchanged.
- `Workflows.Tests.ps1`'s header (`:4-10`) names the new guard. A correcting comment on #1624 at close. No contract row, no matrix.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-cb2'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
$unit = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
dotnet test $unit -c Release --no-build --filter "FullyQualifiedName~ModelTestRunTests|FullyQualifiedName~TestHostCrashDumpCensus"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <the .cs files from git diff --name-only>
Import-Module Pester -RequiredVersion 5.9.1 -Force
./ci/windows-runner/tests/Invoke-Tests.ps1
```

- **Local stall, not committed.** Plant `Thread.Sleep(Timeout.Infinite)` at the top of the composed case body (`T/ConnectionsLeafTests.Model.Composed.cs:1043`). Set the shard variables of `windows-model-ci.md:56-61` and the `DOTNET_Dbg*` variables with a local folder; run `--filter "FullyQualifiedName~TheModelOfRuleDDerivesEveryComposedRoute"`. Expect the process to end after about five minutes, a `stalled` progress file and a `crash-*.dmp`. Quote what `dotnet test` printed.
- **The PR's own run:** both shards and `app model (windows x64)` green, no evidence artifact. Quote the run id.
- **A scratch branch** with the park committed, run by `gh workflow run windows.yml --ref <branch>`. Both shards go red about five minutes into the test step and the print step names the case. Then `gh run rerun <id> --failed`; `gh api repos/{owner}/{repo}/actions/runs/<id>/artifacts` lists the attempt-1 and attempt-2 evidence side by side. Open one dump (`dotnet-dump analyze`, `clrstack -all`) and quote the parked frame. Delete the branch.

## Acceptance

- [ ] The four `ModelTestRunTests` facts and the two Pester facts were seen failing, then pass; the census theory passes for both jobs.
- [ ] The PR's run is green on both shards and the aggregator; its run id is in the PR body.
- [ ] The scratch run shows the stalled case named in the log, both attempts' evidence artifacts and one readable dump; the branch is deleted.
- [ ] The PR body records where a failed model attempt's evidence lives and for how long (rule 6): the attempt-named artifact, 7 days; the job log with the stalled case, 90 days; the report artifact, 30 days, replaced by a rerun.
- [ ] The PR body has "Found and fixed here" (each fix with its failing test, or "none") and ends with the count: found on the way, fixed here, deferred, filed. The blame dump type is counted as deferred.
- [ ] `Closes #1624`, with the correcting comment posted. `not delivered: a replay of run 40119788856` (its host load cannot be reproduced; the correction is from the progress file's own numbers).
- [ ] No red team: tests and CI config only (rule 9).

## Brief

Read `T/ModelTestRun.cs:52-124`, `:216-351`; `T/ModelTestRunTests.cs:27-36`, `:164-260`; `.github/workflows/windows.yml:897-912`, `:1044-1109`, and the app twins `:763-777`, `:813-823`; `ci/windows-runner/tests/Workflows.Tests.ps1:16-47`, `:192-222`; `T/Censuses/TestHostCrashDumpCensus.cs`; `docs/runbooks/windows-model-ci.md:74-106`. Build, tests first. In `ModelTestRun`: the stall counter in `FlushProgress` (Design 1), `StallBound` and `StallTicks` (Design 2), `EndTheProcess` as the default `onStall` (Design 3). In `windows.yml`'s model job only: `70m`, the four dump variables, the print step, `.dmp` excluded from the report upload, and the attempt-named evidence upload (Design 4 to 7). Pin it in `Describe 'windows.yml model evidence'` and make the census's no-secret fact a theory over both jobs. Update the two runbook passages. Do not edit `StaThread.cs`, `self-hosted-windows-runner.md`, the model facts, the verifier or the pilot. Prove the fail-fast locally with a planted park, then on a dispatched scratch branch with a failed rerun. PR body: the run ids, the evidence lines (rule 6), "Found and fixed here", `not delivered: a replay of run 40119788856`, the count, `Closes #1624`. Commit: `test(windows): the connections model names a stalled case and a failed attempt keeps its dumps`.
