# CB-1 — A timeout that could not take a stack says how long a heartbeat thread stopped and how long the collector paused

Issues: #1843. Size S. Lanes: windows (`SlateWindows.Tests`, the app job). AT: — (test-only). Wave: C1, slot 1. Depends on: nothing (CB-0 merges first). Parallel with: CB-2, CB-4. Decision: CD2. The owner answers it before C0 (plan §3); this spec encodes the recommendation: #1843 closes with its cause unproved, and a timed-out fact's dump stays 7 days. CD1 is overtaken by #1928. Interlock: none. `Support/StaThread.cs` is this chunk's alone; the createdump path (`StaThread.cs:431-544`) and `windows.yml` are not edited. Review: a red team, then Codex adversarial-review rounds until it approves (plan §4 rule 9; the chunk changes test logic).

`T/` below is `apps/slate-windows/tests/SlateWindows.Tests/`.

## Goal

A fact that times out without giving up its stack says in its message how long a heartbeat thread went without running during the body, and how long the garbage collector paused the process. The job log keeps that for 90 days, so it outlives the dump and still shows whether a stall of the whole process would explain the timeout.

## Current state on main

Read at f0bde81c (#1938); the line numbers in `windows.yml`, which changed since, were re-read on 2026-11-15 at 798cdaae.

- **What #1928 delivered** (662be6b1, 2026-11-13; #1826 was closed by it). `StaThread`'s teardown calls WPF's own `StopListening` on the thread's three shut-down listeners (`T/Support/StaThread.cs:156-161`, `:229-244`). Its two facts (`T/StaThreadTests.cs:76`, `:108`) and census (`T/Censuses/StaThreadCensus.cs:82-86`) are not touched here.
- **The timeout path.** `Run<T>` is `StaThread.cs:273-359`; the thread starts at `:322`, and the timeout branch is `:325-351`. The stack is taken at `:328`. The dump is taken at `:329-332`, only when no stack was. The message is built at `:337` and thrown at `:341` or `:349-350`, with the stack text last.
- **The stack.** `StackOf` (`:556-570`) answers at once when the body has no dispatcher (`:560`). Otherwise it waits 5 s (`StackBound`, `:548`) for a `Send` operation, then says the body did not pump (`:569`).
- **The dump.** `DumpOnTimeout` (`:431-445`) runs createdump through `WriteDump` (`:447-500`). Every thread of the process is suspended while it writes (`:391-396`). Its sentences begin with a space (`:443`, `:498-499`). Seams: `DumpDirectory` (`:380`), `RunDumperForTests` (`:405`).
- **The tests.** `StaThreadTests.Dispose` restores the seams (`T/StaThreadTests.cs:32-37`). `TimeOutASleepingBody` (`:474-479`) is the no-dispatcher arm; the body at `:540-563` is the did-not-pump arm; `WaitHereWhilePumping` (`:598-616`) gives up a stack. `WithNoDumpFolderATimeoutWritesNoDump` (`:528-534`) asserts at `:533` that the whole message does not contain "dump". A grep of `T/` finds no other assertion that constrains the new wording.
- **No heartbeat exists** in `T/`. The project targets `net10.0-windows` (`T/SlateWindows.Tests.csproj:4`), which has `GC.GetTotalPauseDuration()`.
- **Stale remarks.** `StaThread.cs:40-46` still says "Nothing runs on the fact's thread after its body"; #1875 and #1928 made that untrue.
- **#1843's failure.** Run 42146706653, attempt 1, `main` at 28dae96e, home pool, 1 failed of 6,289. The job's log (job 127659883812, line 450, 02:06:04Z): `bound 60 s … and the body unwound.) Its stack could not be taken: the body did not pump its dispatcher within 5 s. A full dump … sta-fact-797-20261106T020456.dmp (5082 MB private; … written in 61738ms)`. So the thread was in synchronous code, not in the settle loop, which pumps (`T/RightPaneRailTests.cs:242-248`).
- **The fact was rewritten** on 2026-11-07 by #1853 (e7ebfe30): `T/RightPaneRailTests.cs:30-46`, under a 60 s bound (`:269-270`). The dump expired: the app job's results keep 7 days (`.github/workflows/windows.yml:993-1001`).
- **The host was not idle.** `C:\slate-ci\logs\orchestrator-2026-11-05.log:183-186` (UTC−5): the Rust slot committed cache generation 112 at 02:03:31Z; the model shard's VM was provisioned at 02:03:43Z, handed off at 02:03:55Z and claimed at 02:05:06Z. The fact's minute began about 02:03:51Z; its dump began at 02:04:56Z.
- **Retention, checked 2026-11-14.** Job logs: 90 days (the repository's setting, read with `gh api`). The host's orchestrator logs: 30 days (`docs/runbooks/self-hosted-windows-runner.md:1035`).

## Reference

Issue #1843; the reading, `47_windows_core_closeout/reading/01_test_stability.md:190-218`; plan 47 §2.6, §3 (CD2) and §4 rules 2 and 6 to 9.

## Design

1. **The heartbeat.** A new `T/Support/Heartbeat.cs` holds two things. A pure type records beats (`Stopwatch` timestamps) in a ring of the last 10 minutes and answers the longest stretch inside a window with no beat in it. So a gap that ended before the window is not counted, a gap that straddles its start is clipped to it, and the gap still open at the read is counted. A window older than the ring is measured from the ring's oldest beat. Over it sits one thread for the process, started on the first `Run<T>`: a background thread at highest priority that beats, then sleeps 100 ms, in a `while`. It sets no apartment state, so `StaThreadCensus` (`:44-66`) does not see it.
2. **The window.** `Run<T>` notes a timestamp and `GC.GetTotalPauseDuration()` before `thread.Start()` (`StaThread.cs:322`).
3. **The reading.** On timeout with no stack (either arm of `StackOf`), `Run<T>` appends the sentences at `:329-332`, **before** `DumpOnTimeout`: the dump suspends every thread, the heartbeat included, and would read as a stall. A timeout that took a stack gets no sentence; its body was pumping.
4. **The sentences.** Each begins with a space, as the dump's do. One of:
   - ` The heartbeat thread missed 40.0 s in one gap during the body, which a stall of the whole process would explain.` (a gap of 5 s or more)
   - ` The heartbeat thread kept running during the body: its longest gap was 0.1 s, so the process as a whole was not stalled for longer than that.`

   then ` Garbage collection paused the process for 0.3 s of the body; memory load at the last collection was 71 %.` Numbers use the invariant culture.
5. **What the sentences may say.** Only what was measured: the heartbeat's longest gap and the collector's pause. A process-wide stall is named only as what a long gap would explain. One heartbeat cannot say how many other threads were blocked, so no sentence claims it. A collection stops the heartbeat too, which is why its pause is printed beside the gap. No sentence contains the word "dump" (`T/StaThreadTests.cs:533`).
6. **The seam.** `StaThread.LongestGapForTests`, a `Func<TimeSpan>?` that answers in place of the heartbeat. `StaThreadTests.Dispose` resets it.
7. **The remarks** at `StaThread.cs:40-46` say what now runs on the thread after its body.
8. **Not changed:** `StackOf`, the createdump path, the dump's retention, `windows.yml`, the rail fact, #1928's teardown and facts, product code.

## Tests

In `T/StaThreadTests.cs` unless named. Write them first.

- `ATimedOutBodyThatDoesNotPumpSaysWhatItsHeartbeatSaw`, on the body at `:540-563`: the seam answering 40 s gives the first sentence, answering 0.1 s gives the second, and both carry the collector's sentence. Red today.
- `ATimedOutBodyWithNoDispatcherSaysWhatItsHeartbeatSaw`: the same over `TimeOutASleepingBody`. Red today.
- `TheHeartbeatIsReadBeforeTheDumpIsTaken`: with a dump folder named, the gap seam and `RunDumperForTests` record their order. Red today.
- `ATimedOutBodyThatGaveItsStackSaysNothingOfItsHeartbeat`, on `WaitHereWhilePumping`. A guard; it passes today.
- `WithNoDumpFolderATimeoutWritesNoDump` is unchanged and stays green with the real heartbeat running.
- `T/HeartbeatTests.cs`, on injected timestamps: the longest gap of a window; a gap that ended before the window; a gap clipped at the window's start; the open gap at the read; a window older than the ring. On the real thread: it is a background thread, and it beats.

## Docs and registers

- `docs/runbooks/self-hosted-windows-runner.md`, "Observed timings" (`:1962`): the 2026-11-06 timeline with the four host-log lines quoted, since that log is deleted after 30 days; and rule 6's check, that a single home-pool timeout is read against `C:\slate-ci\logs` before it is filed.
- A comment on #1843: the timeline and the three corrections (not the settle loop; the fact was rewritten; the host was not idle).
- No contract row, parity matrix or ledger line.

## Verification

```powershell
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
# red with only the new facts applied, green on the branch:
dotnet test $tests -c Release --no-build --filter "FullyQualifiedName~SaysWhatItsHeartbeatSaw|FullyQualifiedName~TheHeartbeatIsReadBeforeTheDumpIsTaken"
dotnet test $tests -c Release --no-build --filter "FullyQualifiedName~StaThread|FullyQualifiedName~HeartbeatTests"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <each .cs file the change touched>
```

No full local run: the chunk tears nothing down, so it cannot move the foreground. The PR's app job is the full-suite check. AT: none.

## Acceptance

- [ ] The three red facts were seen failing, then passing; the PR body quotes both runs.
- [ ] The reading precedes the dump; a timeout that took a stack carries no heartbeat sentence; the heartbeat's thread is a background thread; no sentence contains "dump" or says how many threads were blocked.
- [ ] Rule 2: a "Found and fixed here" section. The stale remarks at `StaThread.cs:40-46` are its first line (a comment, so no test).
- [ ] Rule 6, in the PR body: a timeout's heartbeat and collector sentences are in the job log for 90 days; its dump and TRX are in `slate-windows-app-results-<run>-<attempt>` for 7 days (`windows.yml:1001`, unchanged by CD2); the home host's side is `C:\slate-ci\logs` for 30 days.
- [ ] Rule 7 with CD2: `Closes #1843`, with `not delivered: the cause of the 2026-11-06 timeout`.
- [ ] Rule 8: the body's last line is the count: found on the way, fixed here, deferred, filed.
- [ ] The PR's `not delivered` lines say that a highest-priority heartbeat cannot tell a starved fact thread from a healthy one: it detects a stall of the whole process or the whole VM, or a collector pause. The red team sizes a second beat at normal priority as a find.
- [ ] Rule 9: a red team (a fresh Claude agent with `24_red_team_protocol.md`, the diff and the spec), then Codex adversarial-review rounds until it approves; the PR body records both reviews.
- [ ] The runbook entry and the comment on #1843 are in. `windows.yml` is unchanged. `dotnet format --verify-no-changes` passes; both new files carry the licence header.

## Brief

Branch `claude/cb1-timeout-heartbeat` from `main`. Read `T/Support/StaThread.cs:273-359`, `:377-445`, `:546-570`; `T/StaThreadTests.cs:18-37`, `:303-334`, `:474-479`, `:528-616`; issue #1843 and the reading's section on it. Re-check each line here against the tree. Write the facts under "Tests" and see the three red ones fail. Add `T/Support/Heartbeat.cs` and `T/HeartbeatTests.cs` (`git add`, then `python scripts/apply-license-header.py`) as Design step 1 gives them. Wire `Run<T>` as steps 2 to 6 give it: the window noted before `thread.Start()`, the sentences appended before `DumpOnTimeout`, the gap read through `LongestGapForTests`, that seam reset in `StaThreadTests.Dispose`. No sentence contains "dump" or counts blocked threads. Rewrite the remarks at `StaThread.cs:40-46`. Leave everything under Design step 8 alone. Run the filtered facts and `dotnet format --include`. Add the runbook entry and the comment on #1843. Review by plan §4 rule 9: a red team, which also sizes a second beat at normal priority as a find, then Codex rounds until it approves. PR body: each item under "Acceptance". Commit: `test(windows): a timeout that could not take a stack says how long a heartbeat thread stopped and how long the collector paused`.
