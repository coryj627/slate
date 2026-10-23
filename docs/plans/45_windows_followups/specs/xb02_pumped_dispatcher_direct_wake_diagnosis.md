# XB-2 — The direct-wake fact says which path ended its drain, and insists on it

Issues: #1594. Size S–M (diagnose first). Lanes: windows (`SlateWindows.Tests`). AT: — (test-only). Wave: X2, slot 1. Depends on: nothing. Parallel with: XB-4b, XB-5. Decision: none.

## Goal

`PumpedDispatcherTests.ADrainOnADispatcherWithoutItsWindowEndsThroughTheDirectWake` fails only when the watchdog's direct wake is missing or failed, never because of when its own bookkeeping ran. The cause of run 39897042671's failure (`Expected: [True] Actual: []`, with the 300 ms drain's `TimeoutException` thrown as expected) is named in the PR from a reproduction, not from a guess.

## Current state on main

- **The fact.** `apps/slate-windows/tests/SlateWindows.Tests/PumpedDispatcherTests.cs:76-135`. It swaps the static seam `PumpedDispatcher.WakeThread` (`:82-91`) for a wrapper that calls the original `directWake(threadId)` (`:85`) **and then** appends the result to `wakes` under `lock (wakes)` (`:86-89`). On an STA thread it nulls WPF's `Dispatcher._window` (`:102`), expects `Drain(300 ms)` to throw `TimeoutException` (`:105`), restores the window (`:109`), and after `StaThread.Run` returns it restores the seam in `finally` (`:119`) and asserts `Assert.Equal([true], wakes)` (`:131-134`). A wedged thread (`"has not unwound"`, `:113-116`) takes the `timedOut` path (`:121-130`) and fails with the message about the missing wake.
- **The drain.** `tests/SlateWindows.Tests/PumpedDispatcher.cs:63-105` `Drain(TimeSpan bound)`: a `DispatcherFrame` (`:65`), a Background `BeginInvoke` that sets `ran` and `frame.Continue = false` (`:67-73`), a one-shot `Timer` watchdog on a pool thread (`:75-95`) whose callback returns early if `ran` (`:78`), else sets `frame.Continue = false` in a try/catch (`:82-90`) and then calls `_ = WakeThread(thread)` (`:91`); `Dispatcher.PushFrame(frame)` (`:97`) inside the Timer's `using`; `TimeoutException` if `!ran` (`:99-104`). `WakeThread` (`:111-112`) is `PostThreadMessageW(threadId, WM_NULL, 0, 0)`.
- **The failure's shape.** The drain threw (so `ran` was false and `PushFrame` returned), the thread was not wedged (the `timedOut` path was not taken), and `wakes` was empty at `:133`. With `_window` null, `frame.Continue = false` posts to nothing; the loop leaves `GetMessage` only on a thread message. So `PushFrame` returned because a WM_NULL arrived — and the only poster is `directWake` at `:85`, which the wrapper calls **before** it records (`:86-89`).
- **The race, as read.** The watchdog thread runs `:85` (posts WM_NULL), the STA thread wakes, `PushFrame` returns, `Drain` throws, the STA body ends, `StaThread.Run` joins it and returns, the test thread runs `finally` (`:119`) and takes `lock (wakes)` at `:131` — all before the watchdog thread reaches `:88`. The assertion then reads an empty list. Nothing in the fact waits for the wrapper to finish. A busy runner (Namespace, 4,927 tests in the run) widens that window; the fact is otherwise deterministic, which matches "first time in 15 runs".
- Harness: `Support/StaThread.cs:108-162` (`Run<T>` with `unwindGrace`), `:144` join, `:152-154` the wedged exception, `:169` `Wedged()`. Parallelization is off assembly-wide (`AssemblyInfo.cs:9`).

## Reference

The issue's "Next": "find which path ended that drain, and either make the fact insist on the direct wake deterministically, or make it accept the other path". Plan 44 WB-4's `HeldWork` rule (a wait's result is observed and asserted; releases in `finally`).

## Design

1. **Diagnosis, first commit (test-only, reverted in the same PR).** Insert `Thread.Sleep(50)` between `:85` and `:86` in the wrapper and run the fact ten times: under the race hypothesis it fails every time with `Actual: []`, the issue's exact shape. Also run the unmodified fact under load (a parallel `cargo build -p slate-core --release` in a scratch target, as WB-7's loaded runs do) to see it pass or fail by chance. Record both in the PR body. If the sleep does not reproduce the shape, the hypothesis is wrong: stop, and instead instrument the wrapper and the watchdog with a `[ThreadStatic]`-free trace (`ConcurrentQueue<string>` of "wake-posted", "frame-ended", "assert-read" with `Stopwatch` ticks) and re-run under load until the order that produced `[]` is seen; the fix then follows the observed order.
2. **The fix (under the confirmed hypothesis).** The wrapper records before it can be observed: a `ManualResetEventSlim recorded` is set after `wakes.Add(posted)`; the fact, after `StaThread.Run` returns and before the assertion, waits `recorded.Wait(TimeSpan.FromSeconds(5))` and asserts the wait succeeded with a message naming the watchdog ("the watchdog's wake callback did not finish recording within 5 s"). The post-then-record order stays (the recorded bool is the real `PostThreadMessageW` result). `finally` still restores the seam; the event is disposed after the assertion. The timed-out branch (`:121-130`) is unchanged.
3. **Insist, do not accept.** The fact's name and doc (`:67-75`) already say the direct wake is the only legitimate end; no "other path" is admitted. If the diagnosis finds a genuine second path (a stray thread message from a previous fact's unwedge at `:125`, say), the fix is to drain that message before the test's drain (`PeekMessage` with `PM_REMOVE` on WM_NULL at the body's start), not to accept it.
4. `PumpedDispatcher.cs` is not changed.

## Tests

- The fact itself, with the diagnosis mutation (step 1) recorded: fails 10/10 with the sleep, passes 10/10 with the fix and the sleep (the sleep is then removed).
- Keep `ADrainTheDispatcherNeverRunsFailsWithinItsBound` (`:49-65`) and the two `PumpUntil` facts (`:17`, `:32`) green.
- Loaded run of the unit project's `PumpedDispatcherTests` class 20 times (`for ($i=0;$i -lt 20;$i++) { dotnet test … --filter FullyQualifiedName~PumpedDispatcherTests }`) beside a cold release build.

## Docs and registers

None.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-xb2'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
# step 1 (with the Sleep(50) mutation applied): expect 10 failures of the issue's shape
1..10 | ForEach-Object { dotnet test $tests -c Release --no-build --filter "FullyQualifiedName~ADrainOnADispatcherWithoutItsWindowEndsThroughTheDirectWake" }
# step 2 (fix applied, mutation still in): expect 10 passes; then remove the mutation
1..10 | ForEach-Object { dotnet test $tests -c Release --no-build --filter "FullyQualifiedName~ADrainOnADispatcherWithoutItsWindowEndsThroughTheDirectWake" }
Start-Job { $env:CARGO_TARGET_DIR='C:\dev\slate\target-xb2-load'; cargo build -p slate-core --release } | Out-Null
1..20 | ForEach-Object { dotnet test $tests -c Release --no-build --filter "FullyQualifiedName~PumpedDispatcherTests" }
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include apps/slate-windows/tests/SlateWindows.Tests/PumpedDispatcherTests.cs
```

CI lanes: `windows.yml` (unit lane). AT: none.

## Acceptance

- [ ] The PR body names the path that ended the drain, with the reproduction (10/10 under the mutation) and the loaded-run counts.
- [ ] The fact waits for the wrapper's record before asserting, bounded, and still fails on a missing or failed wake.
- [ ] `PumpedDispatcher.cs` unchanged; the three sibling facts green; 20 loaded runs green.
- [ ] `Closes #1594`. Not delivered: nothing.

## Brief

Read `apps/slate-windows/tests/SlateWindows.Tests/PumpedDispatcherTests.cs:49-139` and `tests/SlateWindows.Tests/PumpedDispatcher.cs:60-118`, `Support/StaThread.cs:108-170`. Hypothesis to test first: the wrapper at `:83-91` posts (`directWake`, `:85`) before it records (`:88`), and the drain, the STA body, `StaThread.Run`, the `finally` (`:119`) and the assertion (`:131-134`) can all run before the watchdog thread records — so `wakes` reads `[]` although the direct wake happened. Prove it: a `Thread.Sleep(50)` between `:85` and `:86` must fail the fact 10/10 with `Expected: [True] Actual: []`. Fix: set a `ManualResetEventSlim` after `wakes.Add`; after `StaThread.Run` returns, `Assert.True(recorded.Wait(TimeSpan.FromSeconds(5)), "the watchdog's wake callback did not finish recording within 5 s")` before the `lock (wakes)` assertion; keep post-then-record so the bool is real; dispose the event after. With the fix the mutated fact passes 10/10; then remove the sleep. If the sleep does not reproduce, do not guess: trace the order (a `ConcurrentQueue<string>` with Stopwatch ticks at post, frame end and assert) under a parallel `cargo build --release` until the `[]` order is observed, and fix that order; never make the fact accept a second wake path — drain a stray WM_NULL at the body's start instead if one is found. Run `PumpedDispatcherTests` 20 times under load; `dotnet format --include` on the one file. PR body: the reproduction counts, run 39897042671, `Closes #1594`. Commit: `test(windows): the direct-wake fact waits for the watchdog to record its wake before reading it`.
