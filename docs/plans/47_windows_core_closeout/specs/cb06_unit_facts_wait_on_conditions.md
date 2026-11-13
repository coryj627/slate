# CB-6 — The unit project's facts wait on conditions, and `TestWaitCensus` refuses a new clock wait

Issues: none (the class sweep; it closes nothing). Size M. Lanes: windows (the app job's unit facts). AT: — (tests only). Wave: C2, slot 2. Depends on: CB-4 merged (it creates `Censuses/TestWaitCensus.cs` with rule 3 and the shared walk). Parallel with: CB-3 then CB-5, CB-8 (no shared files). Decision: none. Interlock: `TestWaitCensus.cs` is shared in order CB-4, CB-6, CB-7; CB-7 deletes the gate entries this PR pins, in the shape chosen here. CB-1 and CB-4 (wave C1) edited `StaThread.cs`, `StaThreadTests.cs`, `SaveChainAdmissionTests.cs`, `FinalMergeDeltaTests.cs` and `ReadingFocusTests.cs`: re-check every line below against `main` at start. No product file and no workflow changes. Review: its tests alone (plan §4 rule 9).

`T/` = `apps/slate-windows/tests/SlateWindows.Tests/`, `G/` = `…/SlateWindows.AccessibilityTests/`, `S/` = `apps/slate-windows/src/SlateWindows/`. Lines are from c2377210.

## Goal

A unit fact goes red because the code is wrong, not because the runner was busy. Cold wall-clock budgets become warm medians. A fixed delay in front of an assertion becomes a wait on the state the assertion reads. An elapsed ceiling used as proof that a call did not wait is replaced by a witness that it did not. `TestWaitCensus` then pins what is left, by file, member, count and reason, so a new one fails the lane and a fixed one must leave the list.

## Current state on main

**The greps, run again in `T/`.** `Thread.Sleep(`: 66 in 29 files (the reading's 64 in 27, plus `ExitWatch.cs` and `ExitWatchTests.cs` from #1927). `Task.Delay(`: 46 in 19. Not in the reading: 43 calls to the fixed pumps `PumpFor(span)` and `RunDispatcherFor(span)` in four files (`HotExitTests.cs` 26, `AccessibilityNotificationDispatcherTests.cs` 9, `Censuses/AvalonDocumentBufferCensus.cs` 5, `Censuses/CanonicalHighlightCensus.cs` 3). A fixed pump is a fixed sleep on a dispatcher thread. In `G/`: 47 `Thread.Sleep(` in 14 files, no `Task.Delay`.

**What exists.** `T/WallClockBudget.cs:27-58` (`WarmMedian`, `WarmMedianAsync`: one untimed run, five timed, the median), used at `T/CanvasDocumentTests.cs:4898` and `:4956` (#1915). The lane filter `kind!=benchmark` (`.github/workflows/windows.yml:774`); two facts carry the trait (`GraphEndToEndTests.cs:944`, `CommandPaletteTests.cs:134`). `TestWaitCensus.cs` is not on c2377210. CB-4's spec gives it one lazy walk of both projects, a floor per project, and rules written as pure functions from parsed source to violation strings, each with a planted-source theory.

**The reading's instances, checked.** Every line claim holds.

| # | Site | Asserts today |
|---|---|---|
| 1 | `T/CanvasEndToEndTests.cs:316` `LargeCanvasOpensNavigatesAndWindowsUnderBudget` | One cold reading each: open `< 500 ms` (`:325-327`), first derivation `< 500 ms` (`:338`), pan average `< 100 ms` (`:348`), navigator average `< 50 ms` (`:358`); a `BENCH` line (`:361-364`). The name is cited in `chords.json:2790`, `BENCHMARKS.md:943` and contract 34, so it stays. |
| 2 | `T/AccessibilityNotificationDispatcherTests.cs:1384` `TheLaunchWindowStartsAtTheFirstFrameNotAtConstruction` | Sleeps 1 s (`:1400`), reads zero, shows, pumps to the first frame, then needs `launch.Elapsed() < 1 s` (`:1407-1410`); sleeps 100 ms and needs the clock to have moved (`:1411-1412`). The product clock is a `Stopwatch` started in `ContentRendered` (`S/AccessibilityNotificationDispatcher.cs:607-626`). |
| 3 | `T/W1HardeningTests.cs:1411` `FiveThousandItemRefreshReturnsWithinTheUiDispatchBudget` | `CreateSidebar` returned in `< 100 ms` (`:1425`, `:1437-1441`). The worker is still held then, and `:1442-1445` already assert the other thread, the empty tree and `IsRefreshingTree`. |
| 4 | `T/SyncMarkerWatcherTests.cs` | A real `FileSystemWatcher`, a real debounce. `:77`, `:122`: sleep, then exactly one fire. `:194`, `:249`, `:307`, `:358`: sleep, then take a baseline. `:391`, `:429`, `:465`: sleep, then no fire. `:155-157`: the fire took `< 5 s`. The watcher has no idle witness (its only seams are `S/SyncMarkerWatcher.cs:154-206`). |
| 9 | `T/Censuses/EventKindsCensus.cs:105`, `T/Censuses/CallbackConcurrencyCensus.cs:273` | Unregister, save, sleep 400 ms, then the sealed count is unchanged. A save's fan-out runs on the saving thread over a snapshot of the listeners (`crates/slate-core/src/session.rs:5262-5285`). |
| 10 | `T/GraphDocumentTests.cs:1346`, `:1386` | Teardown `< 3 s`; its own bound is 5 s (`S/WorkspaceViewModel.cs:3927`). The next line (`:1347`, `:1387`) reads `WhenAllWorkDrained().IsCompleted` before anything pumps. |
| 10 | `T/HeldVaultStoreFileTests.cs:68` | A held save `< 2 s`. `:66` already asserts the attempts equal `AnchoredVaultStore.BriefHeldFileBackoff` (10, 20, 40, 80 ms; `S/AnchoredVaultStore.cs:40-46`). |
| 10 | `T/W2EditorInteractionRedTeamTests.cs:181-183` | `DecodeImage` returned in `< 2 s`. `:185-187` already assert one abandoned slot and `finished == 0` while the decoder is held. |
| 10 | `T/RescanTests.cs:1600`, `:1641` | Rescan and close `< 10 s` with the leaf's untracking held for 60 s. `:1601` reads the ended work still listed; the close has no such read. |

**Found by this spec's greps, same class.** Ceilings: `T/ConnectionsLeafTests.Rescan.cs:201`, `:302` (teardown `< 5 s`, beside `observed()`); `T/PumpedDispatcherTests.cs:82` (`< 10 s` after `Assert.Throws<TimeoutException>`); `T/HotExitTests.cs:684` (the close blocked `< 1.5 s`); `T/HotExitTests.cs:1300` (a `DateTime` difference `< 3 s` against the 4 s `HotExitDiscardBudget`); `T/WeakEventExitTests.cs:43-45` (a child process exits in `< 1 s`; the reading comes from a helper). A positive wait written as a fixed pump: `Censuses/AvalonDocumentBufferCensus.cs:586` (250 ms, then `DriftRecoveryCount == recoveries + 1`). A delay after the witness it needs: `T/CitationAsyncInterleavingTests.cs:956` (20 ms after the drain completed).

## Reference

`T/Censuses/StaThreadCensus.cs:53-65` (an allow-list with a stale-entry check) and `:133-170` (the walk, and how a site's member is named). CB-4's spec, Design 4, for the file's shape; CB-7's spec, Design 6 and 7, for what it does with the gate entries. Plan 44 WB-7 for the doctrine: a negative probe follows a positive witness. `reading/01_test_stability.md`, cluster section.

## Design

**1. The line between fix and pin.** For each delay in front of an assertion, in this order:

- The assertion is positive (something must have happened): wait for that state with the fact's own pump or `SpinWait.SpinUntil`, bound 10 s or more. Fixed.
- The assertion is negative, and something in the fact's hands keeps arriving through the same channel (a second listener, a sentinel write, a poll's tick count): wait for that, then assert. Fixed.
- Otherwise it stays, pinned with one reason word. A site that could only be fixed with a new product seam stays pinned, and its entry names the seam; this PR changes no product file.

For each elapsed ceiling: name the state that shows the call did not wait, assert it, delete the ceiling. A ceiling with no such state in today's seams is pinned in rule 2's list.

**2. Reason words.** `late-arrival`: asserts that nothing more happens; no signal ends a wait for an event that must not come (CB-7's word). `deliberate-hold`: the delay is the scenario; it holds a body, a seam or a gap open. `timeout-under-test`: the fact must outlast a timeout or debounce it tests. `distinct-timestamp`: two writes need different clock ticks. `quiesce`: a counted drain-and-yield loop. `watchdog`: a harness timer that is the feature. `registration`: a listener's registration settles (gate only, CB-7's). `unswept`: a gate entry as it stands; refused in the unit list. `no-wait-witness` (rule 2 only): an elapsed ceiling kept because no seam observes what the call really waited; its entry names the seam that would. "Poll interval" is not a reason: a poll is exempt by position.

**3. The fixes.**

- **#1.** Each of the four readings goes through `WallClockBudget.WarmMedian`. Open: one sample opens a fresh document, stops the clock, checks 2,000 rows and shuts it down (the shape at `CanvasDocumentTests.cs:4898-4918`). Derivation: one `Derive`. Pan: ten hops, divided by ten. Navigator: fifty `NextCard` steps on one kept document, divided by fifty. The count assertions (`:324`, `:336-337`, `:359`) stay outside the samples. The `BENCH` line keeps its keys and prints the medians.
- **#2.** Start the fact's own `Stopwatch` before `ForProduction`. Keep one hold of 250 ms (pinned `deliberate-hold`). After the first frame read `first = launch.Elapsed()`, then `built = clock.Elapsed`, and assert `built - first >= hold / 2`: a clock started at construction gives a difference near zero, and load only widens it. Replace `:1411-1412` with `SpinWait.SpinUntil(() => launch.Elapsed() > first, 10 s)`.
- **#3.** Delete the stopwatch and `:1439-1441`. The witness is already there: the constructor returned while the worker was held, on another thread, with an empty tree.
- **#4.** Delete `:155-157`; the witness is the fire arriving inside the `Churn` scope, which a trailing-only debounce cannot produce. The nine settle sleeps stay, `late-arrival`: nothing the watcher exposes says a burst has ended.
- **#9.** Register a second recorder before the unregister. After the post-unregister save, save a sentinel note and wait (10 s) until the second recorder holds the sentinel's change; the first save's fan-out is complete by then. Then assert the sealed count.
- **#10.** `GraphDocumentTests`: delete both ceilings; `:1347` and `:1387` are the witness and stay ahead of `PumpedDispatcher.Drain()`. `HeldVaultStoreFileTests`: the ceiling stays, pinned in rule 2's list with the reason `no-wait-witness`. The configured schedule's sum would not observe what the save really waited, so it is no witness; a real one needs a delay seam in `AnchoredVaultStore`, which is one line on "Plan 47 deferred finds". `W2…`: delete `:181-183`. `RescanTests`: the seam counts its returns (`held.Wait(…)`, then `Interlocked.Increment(ref untracked)`); both facts assert `untracked == 0` straight after the call under test and drop the ceiling.
- **The found ones.** `ConnectionsLeafTests.Rescan.cs`: delete both ceilings; `observed()` read straight after `Dispose` is the witness. `PumpedDispatcherTests.cs:82`: delete; the `TimeoutException` and the runner's 30 s bound are the witnesses. `HotExitTests.cs:1300`: replace with `Assert.Equal(0, host.DiscardPrompts)`; a wait that ran out its budget asks the "not discarded" prompt (`S/VaultLifecycleViewModel.cs:1939-1954`). `HotExitTests.cs:684`: find what a cancelled open leaves that a timed-out one does not; if nothing does without a product seam, pin it in rule 2's list. `WeakEventExitTests.cs:43-45`: compare the two readings with each other (`with < without / 2`); keep the floor. `AvalonDocumentBufferCensus.cs:586`: pump until the count moves, bound 10 s. `CitationAsyncInterleavingTests.cs:956`: delete the delay; the completed drain is the witness.
- **Every other fixed pump, counted loop and settle sleep:** apply Design 1. By reading, most are `late-arrival` (a 750 ms pump, then an exact list) or `quiesce`.

**4. Rule 1, `NoNewFixedDelayStandsBeforeAnAssertion`.** Syntax only, over both projects.

A *delay* is `Thread.Sleep(…)`; a `Task.Delay(…)` that is awaited, blocked on (`.Wait()`, `.GetAwaiter().GetResult()`) or passed as a direct argument to any call but `Task.WhenAny`; or a call to a method named `PumpFor` or `RunDispatcherFor`.

A delay is a *site* when it is the whole of an expression statement and none of these holds:

- **Poll.** Between it and its enclosing function stands a `while`, a `do`, a `foreach`, or a `for` that can end early. A `for` cannot end early when its condition is one comparison of its own loop variable and its body has no `break`, `return`, `throw` or `goto`. So `for (…; turn < 20; …) { Drain(); Thread.Sleep(5); }` is a site: a fixed sleep written long.
- **Wait condition.** Its enclosing function is a lambda passed to a call named `SpinUntil`, `PumpUntil`, `RunUntil`, `WaitFor` or `WaitUntil`.
- **Hold.** Its enclosing function is a lambda or anonymous method that is the right side of an assignment or `+=` (a seam the product calls), or an argument to `Task.Run`, `Task.Factory.StartNew`, `new Thread(…)` or `ThreadPool.QueueUserWorkItem`.

A delay that is an expression body (`() => Thread.Sleep(600)`) is not a statement, so not a site. A lambda passed to anything else, a fact's `RunSta(() => { … })` included, is searched like a method body.

Sites are keyed `(file, member)`, the member named as `StaThreadCensus.cs:157-165` names it. Two pinned lists, unit and gate, of `(File, Member, Count, Reason)`. The fact fails on a key with more sites than its count or with no entry ("new: wait on the condition, or add an entry and say in the PR why none could be waited on"), on an entry with fewer sites than its count ("stale: lower or remove it"), on a reason outside Design 2, and on `unswept` in the unit list. A floor on calls seen to the four names: 140 in `T/`, 20 in `G/`.

Blind spots, written in the file's header: a `while` on a clock with no other exit; a pump helper with another name; FlaUI's `Wait.UntilInputIsProcessed` (76 calls in `G/`, CB-7's to weigh).

**5. Rule 2, `AnElapsedCeilingGoesThroughWallClockBudgetOrIsABenchmark`.** Per member, its lambdas and local functions included.

A *reading* is a member access named `Elapsed`, `ElapsedMilliseconds` or `ElapsedTicks`, a call to `Stopwatch.GetElapsedTime`, or `DateTime.UtcNow`, `DateTime.Now`, `DateTimeOffset.UtcNow`, `Environment.TickCount`, `Environment.TickCount64`. A local is *timed* when its initialiser or any assignment to it holds a reading or a timed local, to a fixed point. A local initialised from a `WallClockBudget.` call is not timed. Array elements and helper returns carry nothing.

A *ceiling* is `a < b`, `a <= b`, `b > a` or `b >= a` where `a` holds a reading or a timed local and `b` holds neither, inside the first argument of `Assert.True` or `Assert.False`; or an `Assert.InRange` whose first argument is timed. Not a ceiling: a floor (`elapsed >= x`; load only lengthens a reading), a comparison of two timed values (an order or a ratio), an assertion inside a loop that rule 1 calls a poll (the wait's bound), and anything in a member or class with `[Trait("kind", "benchmark")]`.

Its pinned list has the same shape and checks. Blind spots, in the header: a reading returned from a helper (`WeakEventExitTests`), and a fact that medians its own samples (`AvalonDocumentBufferCensus.cs:359-370`, already the warm-median shape).

**6. The lists' size.** By this spec's reading, rule 1 starts at 81 unit sites: 28 `Thread.Sleep`, 13 `Task.Delay`, 40 fixed pumps. The reading's "about 30" counted the first kind only. Five go by the fixes above (#2's second sleep, both of #9, `AvalonDocumentBufferCensus.cs:586`, `CitationAsyncInterleavingTests.cs:956`), so the target is 76 or fewer, each with a reason; every positive wait found under Design 1 lowers it. The gate starts at about 30 sites, all `unswept` (CB-7's spec works 29). Rule 2 starts at 18 unit ceilings and one gate ceiling (`G/FocusSettleWatchTests.cs:54`, pinned for CB-7); the unit target is one (`HeldVaultStoreFileTests.cs:68`, `no-wait-witness`), or two with `HotExitTests.cs:684`. The census's first run is the authority for every number here. The PR body gives its numbers at the first commit and at merge.

**7. Order inside the PR (plan §4 rule 4).** First commit: both rules, the planted theory, and the lists holding today's sites. Then one commit per file: the fix, and its entry lowered or deleted.

**8. Not changed.** Product code, `windows.yml`, the gate project's code. The reading's "release after a delay" facts (`RescanTests.Round3.cs:143`, `:226` and their kin): the delay runs on a pool thread and only widens a window, and the oracle is the order of two readings of one clock, so neither rule fires. `BasesExportTests.cs:85` (10 s for core's real quiet window, `late-arrival`; its sentinel could be the witness only by changing what the fact counts).

## Tests

**No red-first form, said plainly.** A wait that replaces a sleep fails first only as the flake. What can be shown instead, and is quoted in the PR body:

- **A ceiling's witness still catches the defect.** For each deleted ceiling, one local mutation, reverted, turns the fact red at the witness. #3: build the tree inline in the constructor; `:1442` fails. `GraphDocumentTests`: leave the queued apply unsettled at shutdown; `:1347` fails. `RescanTests`: make the drain wait for untracking; `untracked == 0` fails. `HotExitTests.cs:1300`: hold the discard past its budget; the result or the prompt count fails. #9: skip the unregister; the sealed count fails. #2: start the product clock at construction; the difference fails. If a mutation stays green, the witness is wrong: keep the ceiling, pin it, and say so.
- **#1** still fails under contract 34's TH-2 M1 (a 60 ms sleep in the navigator step).
- **Loaded runs.** The touched facts pass three times with `DOTNET_PROCESSOR_COUNT=1` (WB-7's recipe).

**Census facts, in `TestWaitCensus.cs`.** These are red first: written before the rules, they fail until the rules classify them.

- `TheClockRulesSeeAPlantedFact`, a theory over planted source.
  - Rule 1 sites: `Thread.Sleep(100); Assert.True(done);`; `await Task.Delay(50);` then an assertion; the same sleep inside `RunSta(() => { … })`; the counted `for`; `h.Context.Await(Task.Delay(TimeSpan.FromSeconds(10)), "quiet");`; `host.PumpFor(span);`.
  - Rule 1 clean: a sleep in `while (!done)`; in a `for` with `&& !done`; in a `for` with a `return`; in a `SpinWait.SpinUntil` lambda; in a lambda assigned to `x.BeforeForTests`; in `Task.Run(() => { Thread.Sleep(300); release.Set(); })`; `() => Thread.Sleep(600)`; `await Task.WhenAny(t, Task.Delay(5000))`; `return Task.Delay(Timeout.InfiniteTimeSpan, token);`; the call in a comment and in a string.
  - Rule 2 hits: `Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3))`; a local divided from a reading, then `Assert.True(ms < 100)`; `Assert.True(DateTime.UtcNow - began < limit)`; `Assert.InRange(clock.ElapsedMilliseconds, 0, 500)`.
  - Rule 2 clean: a median from `WallClockBudget.WarmMedian`; the first hit under the benchmark trait; a floor; two timed values compared; the ceiling inside a `while (true)` poll; a `while` condition.
- `ThePinnedListsAreExactAndGiveAReason`: the comparison is a pure function. Planted: one site over its count, one entry over its sites, an entry with no site, an empty reason, a reason outside the vocabulary, `unswept` in the unit list.
- The two rule facts over the tree.

## Docs and registers

`BENCHMARKS.md`, the paragraph at `:942-946`: one sentence, that since plan 47 CB-6 the fact asserts the median of five warm readings (`WallClockBudget`), so the table's recorded cold values are history. No contract row, chord, matrix or announcement changes. Evidence (plan §4 rule 6): this PR answers no recorded failing run; the app job's results keep 7 days (`windows.yml:814-823`), and the PR body says so.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-cb6'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
$t = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
dotnet test $t -c Release --no-build --filter "FullyQualifiedName~TestWaitCensus"
$f = (<each touched test class> | ForEach-Object { "FullyQualifiedName~$_" }) -join '|'
dotnet test $t -c Release --no-build --filter $f
$env:DOTNET_PROCESSOR_COUNT = '1'; 1..3 | ForEach-Object { dotnet test $t -c Release --no-build --filter $f }; Remove-Item Env:DOTNET_PROCESSOR_COUNT
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include (git diff --name-only origin/main -- '*.cs')
```

Each mutation is made, run with its one fact, and reverted. CI lanes: `windows.yml` (the app job). AT: none.

## Acceptance

- [ ] Rules 1 and 2 are in `TestWaitCensus.cs` beside rule 3, each a pure function whose planted cases answer as listed; the pinned-list fact rejects a new site, a stale entry and a missing or unknown reason.
- [ ] #1 reads four warm medians; #2 and #9 wait on conditions; #3, #4's ceiling, #10 and the found ceilings assert a witness and no elapsed time, except `HeldVaultStoreFileTests.cs:68` in #10, whose ceiling stays, pinned as `no-wait-witness` (Design #10).
- [ ] Each deleted ceiling's mutation was seen red at its witness, or the ceiling is pinned with the reason; the PR body lists them.
- [ ] Rule 1's unit list is 76 sites or fewer, every entry with a reason word from Design 2 and none `unswept`; the gate list holds the gate's sites as they stand. Rule 2's unit list holds `HeldVaultStoreFileTests.cs:68` (`no-wait-witness`) and at most `HotExitTests.cs:684`. The PR body gives the census's numbers at the first commit and at merge.
- [ ] The touched facts pass three loaded runs; the format gate passes; the app job is green on the PR.
- [ ] No file outside `T/` and `BENCHMARKS.md` changed.
- [ ] The PR body has "Found and fixed here" (each fix with its failing test, or "none"), `not delivered: a failing test for each sleep replaced by a wait (none exists but the flake)`, and ends with the count: found on the way, fixed here, deferred, filed.
- [ ] No closing keyword stands before an issue number; `gh pr view N --json closingIssuesReferences` is empty.

## Brief

Branch `claude/cb6-unit-waits` from `main`, after CB-4 has merged. Read plan 47 §4 and §10, the reading's cluster section, CB-4's spec Design 4 and CB-7's Design 6 and 7, then `T/WallClockBudget.cs`, `T/Censuses/TestWaitCensus.cs` as CB-4 left it, and `T/Censuses/StaThreadCensus.cs:53-65`, `:133-170`. Re-check every line in this spec against the tree; CB-1 and CB-4 moved some. First commit: the planted theory, then rules 1 and 2 exactly as Design 4 and 5 define a delay, a site, a reading and a ceiling, and the lists `(File, Member, Count, Reason)` filled from the census's own first run, the gate's all `unswept`. Record the starting numbers. Then one commit per file. Move `CanvasEndToEndTests.cs:316`'s four readings to `WarmMedian`, keeping its name and `BENCH` keys. Rework the launch-clock fact. Give both listener censuses a second recorder and a sentinel save. Delete each ceiling Design 3 names and assert its witness; for each, run its one mutation locally and quote the red line. For every other fixed pump, counted loop and settle sleep apply Design 1: wait on the state if the assertion is positive or a same-channel signal exists, otherwise pin with a reason word. Change no product file. Add the one sentence to `BENCHMARKS.md`. Run the touched facts three times with `DOTNET_PROCESSOR_COUNT=1`, then the format gate over the changed files. PR body: the starting and final counts for both rules and both projects, each fixed site with its witness and mutation result, each pinned unit entry's reason, "Found and fixed here", the `not delivered` line, the evidence line, and the count. Commit: `test(windows): the unit facts wait on conditions, and a census refuses a new fixed sleep or elapsed-time ceiling`.
