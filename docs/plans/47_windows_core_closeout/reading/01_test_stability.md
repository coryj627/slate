# Plan 47 reading — cluster 01: Windows test stability

Issues: #1624 #1625 #1673 #1788 #1826 #1843.
Read from the worktree `<worktree>` at `1ec9bf31` (#1926).
Paths below are relative to that worktree unless they start with a drive letter.
`T/` = `apps/slate-windows/tests/SlateWindows.Tests/`, `G/` = `apps/slate-windows/tests/SlateWindows.AccessibilityTests/`, `S/` = `apps/slate-windows/src/SlateWindows/`.

## How this was read, and what is not verified

- Every issue file was read in full. Every cited file was opened in the worktree. Nothing was built or run.
- Beyond the brief's `gh issue view`, I used read-only GETs: `gh api …/actions/runs/<id>/artifacts`, `…/attempts/1/jobs`, `…/jobs/<id>/logs` (piped to grep, nothing saved), and `gh issue view 1865`. I also read the home runner host's own orchestrator logs on this machine (`C:\slate-ci\logs\orchestrator-2026-10-24.log`, `…-2026-11-05.log`), read-only. Those two sources changed three verdicts, so they are cited where used.
- **The worktree is one commit behind `origin/main`.** `c2377210` (#1927, "the test process logs its own exit") landed after the baseline. It adds `T/Support/ExitWatch.cs`, `T/ExitWatchTests.cs` and five lines in `T/AssemblyInfo.cs`. It is #1826's instrument. I read it with `git show origin/main:…`; it is not in the worktree's files. #1826's spec has to be written against `c2377210` or later.
- Statements about WPF's dispatcher internals (how `DispatcherTimer` ticks are promoted, what a `ShutDownListener` unhooks when it runs) are from memory of WPF's source, not from this repository. Each is marked **unverified**.
- Statements taken from an issue's comments and not re-derived (xUnit's 11 s exit rule, the ExitWatch tables) are marked **from the issue**.

---

## #1624 — the connections routes model "hung for 45 minutes" on a home shard

- **Verdict:** misdescribed. It was not a hang. The Routes fact was still advancing cases when VSTest's blame collector ended it at 45 minutes, because the whole Routes model is one fact and blame's timer only resets between tests. What stands is a smaller defect: the model's watchdog measures the fact, not a case, and a rerun destroys the failed attempt's dumps.

- **Current state:**
  - The progress file's `elapsedMilliseconds` is the family clock, read when the checkpoint is *published*, not when it is written: `T/ModelTestRun.cs:60` (`_clock` starts at construction), `:232-234` (`Snapshot` stamps `_clock.Elapsed`), `:221-230` (`PublishProgress`), `:240-251` (the flusher writes the latest published checkpoint once a second). So the issue's own evidence, `"phase":"arrangement"`, `"activeOrdinal":8955`, `"elapsedMilliseconds":2700938`, says the cell *entered* its arrangement 45 min 0.9 s into the Routes run. A cell stalled for 45 minutes would carry the time it entered, near 0, or the job would have run 90 minutes.
  - The model step runs with `--blame-hang-timeout 45m` (`.github/workflows/windows.yml:1053`); the job's own limit is 90 minutes (`:908`). `ModelTestRun.cs` and that flag have not changed since `c247b865` (2026-10-07), so this is the code the failing run used.
  - The repo already records the same event on hosted runners: "Two of seven hosted runs … lost a still-progressing routes fact at 45 minutes, because VSTest's blame timer resets only between tests" (`docs/runbooks/ci-cost-and-testing-handoff.md:452-454`).
  - Home shards normally sit close to the limit: 38 min at first green, 17 min alone and 26 min beside the app job after #1350 (`docs/runbooks/self-hosted-windows-runner.md:1972`, `:1991-1996`). On 2026-11-06 shard 0 took 24 min 45 s on the home pool and shard 1 took 5 min on Namespace (API, run 42146706653). 4,477 of 6,194 cases in 45 minutes is 0.60 s a case, a projected 62 minutes.
  - The host was overloaded for the whole window, more than the issue's correction says. `C:\slate-ci\logs\orchestrator-2026-10-24.log:369` shows the job claimed at 18:15:35Z, and `:370-403` show the host's own GitHub API calls timing out at 30 s continuously from 18:33Z to past 18:56Z. The issue's "18:18–19:03Z" is exactly the Routes fact's 45 minutes.
  - **The dumps the issue says are kept are gone.** The model artifact is named without the attempt and uploaded with `overwrite: true` (`windows.yml:1060-1070`, name at `:1066`); the hang dumps land in the same folder (`--results-directory`, `:1052`). The rerun replaced it: the API lists `windows-model-40119788856-0` as created 2026-10-24T19:43:08Z, 42,421 bytes. No dump is that small.
  - The waits in the arrangement are bounded, as the issue says: `T/ConnectionsLeafTests.Model.cs:1440` (10 s), `:1668-1674` (10 s), `Host.Settle` at `T/ConnectionsLeafTests.cs:155-160` through `PumpedDispatcher.PumpUntilDrained` (`T/PumpedDispatcher.cs:127-131`, 10 s) and `Drain` (`:63-108`, 30 s, throws). `WaitForTheProbesDecision` is 30 s (`Model.cs:1952-1961`). The only unbounded calls are the synchronous FFI ones (`ScanInitial`, `RenameFolder`, `OpenFilesystem`).
  - **Unverified:** that no bounded wait expired on the way (each would cost 10–30 s and be recorded as a failed cell, not a hang). The first attempt's report JSON, which lists the slowest cases, was overwritten with the dumps.
  - `ad8382ff` (#1642, the fix for #1603) removed a real spin in `PanelWorkScheduler`'s drain under a saturated pool. It landed the day after this run. It may have been slowing these cases; **unverified**.

- **Cause:** verified by reading. One fact holds 6,194 cases; blame's 45-minute timer started when the fact did; a host under an all-cores stress loop ran the cases about 2.4 times slower than its loaded norm; the timer fired with 1,717 cases left.

- **Fix shape:**
  1. `T/ModelTestRun.cs`: a per-case stall watchdog on the flusher's thread. If the published checkpoint has not changed for a stall bound (suggest 5 minutes; the longest bounded wait in a case is 30 s), write a `"phase":"stalled"` checkpoint naming the cell and the phase it is in, and take a dump through `StaThread`'s createdump path (`T/Support/StaThread.cs:371-440`). This is the watchdog the issue wanted and the 45-minute timer never was.
  2. `windows.yml:1053`: raise the blame timeout on the model step to cover a slow but advancing run inside the 90-minute job (suggest 80m). With (1), a real hang is caught in 5 minutes instead of 45, so the cost of a hang falls.
  3. `windows.yml:1060-1070`: upload `*_hangdump.dmp`, the blame sequence file and any stall dump as a second artifact named with `github.run_attempt`. Its name must not match the aggregator's pattern `windows-model-${{ github.run_id }}-*` (`windows.yml:1099-1103`). The report artifact keeps its name and `overwrite: true`, which the aggregator and `scripts/verify_windows_model_shards.py` depend on.
  4. `docs/runbooks/ci-cost-and-testing-handoff.md:452-456`: the "unchanged 45-minute watchdog" paragraph changes with (2).
  5. A comment on the issue with the correction, then close.

- **Failing test first:** `T/ModelTestRunTests.cs`, beside `AStalledCaseIsVisibleThoughItsThreadWritesNothing` (`:215`). New fact, suggested name `ACaseThatStopsAdvancingIsReportedAsStalledWithItsCellAndPhase`: a case body parks on a gate; the fact drives the watchdog's check directly with an injected clock (no real waiting), and asserts the stall callback fired once with the active cell and phase, and that the checkpoint reads `stalled`. Its twin, `ARunThatKeepsAdvancingIsNeverReportedAsStalled`, publishes a new checkpoint before each check and asserts no callback. Red today: there is no stall check. The workflow half has no unit oracle; if `W1HardeningTests` or a census is to pin the flag and the artifact name, that pin is written first and fails on today's `45m`.

- **Size:** S.
- **Lanes:** windows (unit, and the two model shards on both pools).
- **Screen-reader work:** none.
- **Changes text or speech a Mac user would hear:** no.
- **Owner decision needed:** none. (The 80m figure is a cost call the plan can make: a hung Namespace shard could bill 80 minutes only if the new per-case watchdog also failed.)
- **Shares files with:** `windows.yml` with the CI chunk (#1647 #1699 #1903 #1614 #1616) and with any #1843 retention change. `T/ModelTestRun.cs` with nothing in this cluster. Not with #1625 unless #1625 takes option (a).

---

## #1625 — the connections model's host delivers no session events

- **Verdict:** stands, but it is not in this class. It is a deterministic gap in what the model covers, not a clock wait or a teardown race. It is a decision, then a small or a large piece of work depending on the answer.

- **Current state** (every claim checked; the issue's line numbers have drifted):
  - The model's `Host` registers no session listener: `T/ConnectionsLeafTests.cs:83-118`. Its drivers call the lifecycle's arms by hand instead: `RenameIn`, `RenameFolderIn`, `DeleteIn`, `DeleteFolderIn` at `T/ConnectionsLeafTests.Model.Composed.cs:887-924`, and the Routes drivers at `T/ConnectionsLeafTests.Model.cs:1856-1914`.
  - The two cells derive 2 loads: `RenamePinInDialog` at `Model.Composed.cs:338-346`, `RenamePinFolderInDialog` at `:347-351`.
  - Production's listener: registered at `S/VaultLifecycleViewModel.cs:675-692` (issue: ~497–513), handled at `:1152-1158`, applied in `S/VaultLifecycleViewModel.FileChanges.cs:28`; `InvalidatePath` at `:45-46`; the graph probe at `:91`.
  - Core: `note_tasks` at `crates/slate-core/src/session.rs:7753`; `age_move_markers_in_tx` at `:8178` (the comment the issue quotes is `:8166-8177`); `reindex_path_locked` at `:8681-8804` (issue: ~8331–8371), with the deletion arm at `:8748-8763`, `graph_apply` at `:8786` and the `Deleted` notification at `:8795-8803`. `graph_apply` (`:2124-2148`) applies any non-empty batch.
  - The Tasks panel's `NoteTasks(path)` call is `S/Panels/RightPanePanelsViewModel.cs:908`.
  - #1626 (`88634077`) already names this gap in the model's own comment (`T/ConnectionsLeafTests.Model.ReRoot.cs:151`).
  - **Unverified:** that production ends these cells with three loads and a missing-file line. The issue says "most likely"; no fact drives the real lifecycle through this route. Also unverified: that `apply_batch` moves the generation for a `FileRemoved` of a path with no node (the issue observed 3 → 4 in the fixture).

- **Cause:** by design. The model drives the workspace's rule with hand-called lifecycle arms and never hears core's own events, so an event core raises as a side effect of a query (the aged move marker's repair) is outside it.

- **Fix shape:** depends on the decision.
  - (a) The model host delivers session events: register a `VaultEventListener` in `Host`, marshal as `UiVaultEventListener` does, drop the hand-called arms, and re-derive every rename and delete route in both families (Routes has 12,388 driven cells). Files: `T/ConnectionsLeafTests.cs`, `.Model.cs`, `.Model.Composed.cs`, `.Model.ReRoot.cs`. The inventory digests (`scripts/verify_windows_model_shards.py`) change only if a route name or exclusion reason changes.
  - (b) Name the event as out of the model's scope in the two cells' derivation comments, and pin what production does with one fact through the real lifecycle listener (a rename of the pin inside the dirty-tab dialog: the loads issued, the lines spoken, the tab's state). Files: `Model.Composed.cs` (comments), one new fact beside the lifecycle's rescan facts.
  - (c) Core: a repair that leaves the graph unchanged does not move its generation. Files: `crates/slate-core/src/session.rs` (`reindex_path_locked`) or the graph index's `apply_batch`.

- **Failing test first:**
  - (a): the two cells' derivations change to three loads first; the model goes red on them until the host delivers events.
  - (b): the lifecycle fact is new behaviour pinned, not a fix; it is written to state what happens and has no red phase. Say so in the PR (`not delivered: a failing test, because nothing changes`).
  - (c): a Rust test in `session.rs`'s tests: move a note, run `note_tasks` on the old path, assert `graph_generation()` is unchanged and no reload-worthy op was applied. Red today if the issue's 3 → 4 holds.

- **Size:** (a) L. (b) S. (c) S–M.
- **Lanes:** (a), (b) windows. (c) rust, windows, swift (compile only).
- **Screen-reader work:** none. (A silent reload speaks nothing; if the production fact in (b) finds an extra spoken line, that changes and becomes a decision of its own.)
- **Changes text or speech a Mac user would hear:** no for (a) and (b). (c) is core, but it removes a silent reload; no sentence changes. **Unverified** for the Mac host's graph surfaces.
- **Owner decision needed:** yes. "Should the Connections model hear core's own file-change events, or is the event out of its scope?" Recommended answer: (b). The model was built to drive the lifecycle's arms by hand (its helpers say "as the lifecycle sees it"), (a) re-derives two families to cover one event, and one production-shaped fact pins the behaviour the model cannot see. Decline (c) unless that fact shows something a reader can perceive. The issue's last paragraph (a real modal pump would run the applies inside the dialog, so `DeletePinInBackDialog` would need re-deriving) goes the same way: out of scope under (b), in scope under (a).
- **Shares files with:** none in this cluster under (b). Under (a), the model files; still not `ModelTestRun.cs` (#1624).

---

## #1673 — ReadingFocusTests' fixture disposes while a scheduled fetch has not reached its gate

- **Verdict:** stands, exactly as filed. No merge has touched it (`git log --grep "#1673"` finds only `eb65cdee`, #1674, which filed it).

- **Current state:**
  - `Host.Dispose` is `T/ReadingFocusTests.cs:4665-4730`. The loop is `:4680-4695`: an awaited gate goes through `LetTheHeldFetchRunOut(HeldFetchEnd.Either)` (`:4689`); an unawaited one is only set (`:4693`). Then `_inFlight?.Dispose()` (`:4696`), later `Session?.Dispose()` (`:4721`), and every gate is disposed (`:4723-4726`).
  - A gate counts as awaited only once the pool thread has run the fixture's seam as far as `GateForTheFetch()` (`:4406`, `:4499-4507`). The seam is `FetchFaultForTests`, called first thing in `FetchGuarded` (`S/Reading/ReadingContentViewModel.cs:781-792`) from the refresh's `Task.Run` (`:606`).
  - The completion the issue asks the model to expose already exists: `internal Task WhenRefreshWorkDrained()` (`ReadingContentViewModel.cs:717-726`) over `TrackRefreshWork` (`:728-746`); the tests see internals (`S/AssemblyInfo.cs:7`). It also covers the diagram stage (`:2035`). **So the fix needs no product change.**
  - `ReadingContentViewModel.Dispose` (`:1947-1962`) retires the fetch (`RetireFetch`, `:749-754`), so a late fetch is cancelled; whether it touches the session before its first token check is **unverified**.
  - What a late fetch can hit today: a disposed gate (`gate.Wait` at `:4407` throws `ObjectDisposedException` into the product's terminal catch at `ReadingContentViewModel.cs:689-704`, which logs a failure and posts to a dispatcher whose thread has ended), or a disposed session.

- **Cause:** verified by reading. The cleanup treats "no fetch at this gate yet" as "no fetch", and waits for nothing.

- **Fix shape:** `T/ReadingFocusTests.cs` only. In `Dispose`: keep the loop (release every gate); dispose the in-flight model (retire); then `CleanUp(() => PumpedDispatcher.PumpUntilDrained(model.WhenRefreshWorkDrained()))` before the canvas, the workspace and `Session?.Dispose()`; dispose the gates last, as now. The drain observes faults, as `PumpUntilDrained` does (`T/PumpedDispatcher.cs:127-131`).

- **Failing test first:** in `T/ReadingFocusTests.cs`, beside the fixture's own facts (`TheReleaseWaitsForItsOwnProjectionNotAnotherPost`, near `:380-430`). Suggested name `DisposingTheFixtureWaitsForAFetchThatHasNotReachedItsGate`. The bad schedule is forced, not hoped for:
  - the fixture's seam gets a latch *before* `GateForTheFetch()` (so the gate stays unawaited);
  - the latch opens on either of two signals only the fixture's `Dispose` raises: "cleanup is now waiting for refresh work" (set just before the new drain) or "the session is gone" (set right after `Session.Dispose()`), bounded 30 s;
  - the seam counts a fetch that passes the latch after "the session is gone".
  The fact holds a fetch (`HoldNextFetch`), starts a refresh, disposes the host, waits (bounded) for the latch to have been passed, and asserts the count is 0 and that `WhenRefreshWorkDrained()` was complete before the session went. Today's `Dispose` never raises the first signal, so the fetch always runs after the session is gone: red every run. After the fix it always runs before: green every run. No sleep decides either outcome.

- **Size:** S alone; M with the class siblings named in the cluster section, which belong in the same PR.
- **Lanes:** windows (unit).
- **Screen-reader work:** none.
- **Changes text or speech a Mac user would hear:** no.
- **Owner decision needed:** none.
- **Shares files with:** `T/ReadingFocusTests.cs` with any reading-view chunk that adds facts there (#1775 is the likely one; **unverified**). None in this cluster.

---

## #1788 — a graph force change can miss its save window while the diagram settles

- **Verdict:** most likely already fixed on main by `df67ee39` (#1870, 2026-11-09), as a second sighting of #1865. The issue's own proposed cause (the Background save timer starved by the settle's Render cadence) is unproved and, read against the code, unlikely. It is a product defect that was real and is closed, not a test flake; the test's own reader triggered it. Not proved for this particular run.

- **Current state:**
  - The issue's product citations are all correct at the baseline: the 400 ms window (`S/Graph/GraphPreferencesViewModel.cs:62`), the timer at `DispatcherPriority.Background` (`:111`), `Shutdown` flushing (`:367-376`), the cadence timer at `DispatcherPriority.Render` (`S/Graph/GraphLayoutDriver.cs:237`).
  - Its test citations have drifted: the fact is `G/ShellAccessibilityTests.cs:10535`; `ExpectFile` is `:10594-10600` (issue: `:10644`); the repel step's call is `:10803-10805` (issue: `:10849`).
  - The route from the key to the save: `S/Graph/GraphInspectorViewModel.cs:219-229` calls `_preferences.SetForces` first (which runs `ScheduleSave`, `GraphPreferencesViewModel.cs:232-242`, `:327-338`), then speaks, then `ApplyForces` (`S/Graph/GraphDocumentViewModel.cs:809-825`), which restarts the settle (`:823`).
  - **The gate's own poll holds the file.** `ExpectFile` spins on `ReadFile()` (`ShellAccessibilityTests.cs:10583-10593`, `:10598`), which is `File.ReadAllText` (`:10587`): read sharing, no delete sharing. The store replaces `graph.json` by renaming a staged file over it (`S/Graph/GraphConfigStore.cs:108-134`). Windows refuses that rename while a reader without delete sharing has the file open.
  - Before #1870 the writer logged that failure and dropped the aggregate, so the file kept the old value for good. #1870's commit says so and names "the shell gate's read loop" as one such reader. The writer now retries after 25, 50, 100, 200, 400 and 800 ms (`S/Graph/GraphConfigWriter.cs:162-194`, `:261-269`).
  - #1865 (closed 2026-11-09) is the same fact, the same shape ("the control shows the new value; the file never carries it in 10 s"), one step earlier (the group's query). #1788's failure was on 2026-11-03, before the fix. A lost write explains "still `repel: 0.5` after 10 s"; a late write does not, since 10 s is 25 save windows.
  - Why starvation is unlikely (reasoned, **unverified**, WPF internals from memory): each settle step computes on the pool (`GraphLayoutDriver.cs:116-143`), and the next step is issued only by a 16 ms one-shot timer started in the apply (`:145-163`). So the dispatcher is idle while each step computes and between steps, and a Background operation runs in those gaps however slow a step is. Starvation would need the dispatcher never to go idle for 10 s.
  - The failing run's gate artifact still exists (`slate-windows-accessibility-c30e15cf…-1`, expires 2026-12-03). It holds the TRX and axe evidence (`windows.yml:1176-1184`); the app's log was written under the fact's temp root and is probably not in it (**unverified**).
  - The gate still reads without delete sharing here and in seven other polls (cluster section, shape 3).

- **Cause:** not proved for this run. Most probable: the gate's poll held `graph.json` when the store's rename ran, and the pre-#1870 writer dropped the save. The diagnosis step that would record it: a hosted fact that holds a settle un-converged and shows the save still lands (below). If it passes on main, starvation is ruled out by test and #1870 is the fix; if it fails, the issue's cause is real and the timer's priority is the fix.

- **Fix shape:**
  1. `G/ShellAccessibilityTests.cs:10583-10593`: `ReadFile` opens with `FileShare.ReadWrite | FileShare.Delete` (the gate already does this at `:2508`, `:2571`, `:6783`, `:12299`), so the journey stops perturbing the product it measures.
  2. A hosted fact (below). Only if it fails: `S/Graph/GraphPreferencesViewModel.cs:111`, the timer to `DispatcherPriority.Normal`.
  3. Close with a comment naming #1865, `df67ee39` and the fact's result.

- **Failing test first:** `T/GraphInspectorTests.cs` or `T/GraphPreferencesTests.cs`, suggested name `AForcesEditIsSavedWhileTheDiagramIsStillSettling`. Under `StaThread.RunPumped`, over a real diagram model with its real `GraphLayoutDriver` and a preferences object on the fact's dispatcher: slow each step's apply through a seam so every step outlasts the 16 ms cadence (the model has `InsideGateForTests`, used at `T/GraphDiagramTests.Lineage.cs:1545-1552`), change a force, then pump until the writer has the aggregate (`OutstandingForTests` or the store's file), and assert `Driver.IsSettling` is still true at that moment. The oracle is the order of two events, not a duration. **I expect it to pass on main** (unverified); then it is a pin, the PR says `not delivered: a failing test, because the cause was fixed in #1870, whose own tests failed first`, and the red-first evidence is #1870's `GraphConfigWriterTests` (a reader holding the file).

- **Size:** S.
- **Lanes:** windows (unit, shell gate).
- **Screen-reader work:** none.
- **Changes text or speech a Mac user would hear:** no.
- **Owner decision needed:** yes, small. "May #1788 close as a second sighting of #1865, on #1870's tests plus a new pin, when its own failing run cannot be replayed?" Recommended answer: yes. The brief's anti-goal is a fix nobody saw fail and then pass; #1870's was seen.
- **Shares files with:** `G/ShellAccessibilityTests.cs` with every chunk that adds or edits a gate journey (the dropdown axe sweep #1783/#1836 for one). It is a 12,000-line file; plan it as shared.

---

## #1826 — "a fact's thread can end the test host when text services tear down at its exit"

- **Verdict:** partly fixed. The diagnosis is done and the instrument is on `origin/main`; the fix is not. Two things remain: (1) the slow exit that fails CI runs after every test has passed, cause known, not fixed; (2) the original local fail-fast, never reproduced on current main, so neither shown fixed nor shown present.

- **Current state — what each merge did:**
  - `23632db9` (#1875): at the end of every `StaThread` body, on the fact's own thread, `ReleaseTextServices` unregisters each text store and runs four kinds of `ShutdownFinished` handler: `T/Support/StaThread.cs:144-150` (the allow-list), `:176-202`, called at `:245-255`. `T/Censuses/StaThreadCensus.cs:72-76` fails if WPF moves the private members. This is the issue's "tear text services down some other way". The whole suite then passed once in one process on the desktop that used to crash (6,634 of 6,634, **from the issue**).
  - `24d58989` (#1887): unrelated to this issue's mechanism. It gives `ShownShell` the dispatcher's synchronization context (`T/ShownShell.cs:46-56`).
  - `0033dc30` (#1892): the app job's Test step arms the runtime's crash dump (`windows.yml:764-769`), pinned by `T/Censuses/TestHostCrashDumpCensus.cs`.
  - `c9af6261` (#1902): Windows Error Reporting `LocalDumps` for the test executable (`windows.yml:725-745`; `:745` warns on a runner that cannot write HKLM, which is #1903's).
  - `c2377210` (#1927, **after the baseline**): `ExitWatch`, an assembly fixture that rewrites the process's `ProcessExit` handlers after the last test, times each, counts them by kind, and writes `exit-watch.log` into the uploaded results folder.
  - The corrected diagnosis (comment 7, **from the issue**): the CI failures are not crashes. xUnit's in-process runner fails a run whose process is still alive 11 s after its entry point returns. The dumps armed by #1892 and #1902 can never fire for it.
  - The measurement (comment 8, **from the issue**): 4,674 exit handlers taking 5.6–6.6 s of the 11 s on passing runs; 2,228 `WeakEventTable` listeners (5.2 s), 2,189 `DataBindEngine` listeners (1.2 s), 252 `PtsCache` listeners.
  - Verified in the tree: #1555's switch is set for the test process only (`T/SlateWindows.Tests.csproj:50-59`) and pinned by `T/WeakEventExitTests.cs`; fact threads end without a dispatcher shutdown by design (`StaThread.cs:40-46`); `TextScaleTests.OnOwnThread` shuts its dispatcher down itself because its facts close their windows first (`T/TextScaleTests.cs:1640-1655`).
  - Neither fix option from comment 8 exists: the allow-list at `StaThread.cs:144-150` has none of the three listener kinds, and `ExitWatch` only measures.
  - Frequency (**from the issue**): three times on `main` on 2026-11-11 and once on a PR on 2026-11-12, all on Namespace runners. It is the most frequent red in this cluster.

- **Cause:**
  - Slow exit: known. Every fact thread that touches bindings, weak events or paginated text leaves per-thread WPF listeners on `AppDomain.ProcessExit`; they are never removed because the thread's dispatcher never shuts down; at exit they run one after another.
  - Local fail-fast (`0xC0000602`): unknown. A diagnosis step would record: the issue's "throwaway fact" (a `ShownShell` with every sheet forced visible) run 20 times on an interactive desktop with `ReleaseTextServices` switched off by a test seam, then 20 times with it on. Crash with it off and none with it on is the evidence; no crash either way means it is not reproducible on current main.

- **Fix shape** (based on `c2377210` or later):
  1. Remove the leak at its source: extend #1875's per-thread teardown. Add `MS.Internal.WeakEventTable+WeakEventTableShutDownListener`, `MS.Internal.Data.DataBindEngine+DataBindEngineShutDownListener` and `MS.Internal.PtsHost.PtsCache+PtsCacheShutDownListener` to the allow-list at `StaThread.cs:144-150`, so each runs on its own thread at the end of its fact. **Unverified:** that these listeners are on the thread dispatcher's `ShutdownFinished` list (the existing `TextServicesContextShutDownListener` entry is the same WPF mechanism, which suggests so) and that running one also removes it from `ProcessExit`. The failing test below answers both.
  2. If (1) cannot remove them (the handlers do not unhook, or the focus facts regress): comment 8's option 2. `ExitWatch`, which already rewrites the handler list, drops the three kinds after the last test. Narrower; leaves the leak.
  3. `TextScaleTests.OnOwnThread` (`:1640-1655`) stays; it is a stronger teardown for facts that can afford it.
  4. The local fail-fast: the 20 + 20 run above, reported in the PR either way.
  5. Retitle the issue at close; its title describes the half that was not what CI saw.
  - Files: `T/Support/StaThread.cs`, `T/StaThreadTests.cs`, `T/Censuses/StaThreadCensus.cs`; under (2) also `T/Support/ExitWatch.cs`, `T/ExitWatchTests.cs`.

- **Failing test first:** `T/StaThreadTests.cs`, suggested name `AFactsThreadLeavesNoWpfExitListenerBehind`. Read the `ProcessExit` handlers (`ExitWatch.ReadProcessExit()`, `c2377210`), count those whose target is one of the three kinds, run three `StaThread.Run` bodies that each make a binding, a weak-event subscription and a laid-out `FlowDocument`, and assert the count has not grown. A premise assertion inside each body shows the listeners did register, so a zero is the runner's doing. Red today (the count grows with every body); deterministic, in-process, no timing. The suite-level evidence is a number, not an absence: `exit-watch.log` from a full CI run before and after (4,674 handlers and 5.6–6.6 s today).
  A second measurement belongs in the PR: a full local run on an interactive desktop, to show `ReadingFocusTests` keeps the foreground (`StaThread.cs:40-46` records 6–9 of 21 facts failing when a teardown went too far). CI cannot show that; its session has no foreground.

- **Size:** M.
- **Lanes:** windows (unit).
- **Screen-reader work:** none.
- **Changes text or speech a Mac user would hear:** no.
- **Owner decision needed:** yes. "May #1826 close on the slow-exit fix when the original local fail-fast was never reproduced on current main?" Recommended answer: yes, after the 20 + 20 attempt, with `not delivered: the local fail-fast was not reproduced` if that is how it ends. #1892 and #1902 stay armed and name the thread if it returns.
- **Shares files with:** #1843 (`T/Support/StaThread.cs`, `T/StaThreadTests.cs`). They should be one PR or strictly sequenced.

---

## #1843 — a right-pane rail fact timed out once on the home pool

- **Verdict:** stands, but its "done when" can no longer be met as written, and one statement in it is wrong. The fact that timed out was rewritten the next day; the dump has expired; and the host was not idle.

- **Current state:**
  - The failure message, from the job's log (job 127659883812, lines 447-455): `STA test body timed out. (thread sta-fact-797, bound 60 s; its timeout token was cancelled and its dispatcher was told to shut down and the body unwound.) Its stack could not be taken: the body did not pump its dispatcher within 5 s. A full dump of the test process is at …\sta-fact-797-20261106T020456.dmp (5082 MB private; … written in 61738ms).`
  - What that says, read against `T/Support/StaThread.cs:264-291` and `:496-510`: the dispatcher existed; a `Send`-priority operation did not run within 5 s (`StackBound`, `:488`), so the thread was inside synchronous code, either the body's own or one dispatcher operation its pump was running; after the dump it unwound within the 10 s grace. **So it was not stuck in the held arrow's settle loop.** `PumpHeldTicks` pumps (`T/RightPaneRailTests.cs:242-248`, through `PumpedDispatcher.PumpUntil`), and a pumping body gives up its stack. The issue's second "done when" looks in the wrong place.
  - The 2 min 13 s is 60 s (bound) + 5 s (stack) + 61.7 s (the dump, which suspends every thread) + about 6 s (unwind).
  - **The fact is not the one that failed.** At `28dae96e` it began `new Rail("tasksReview", history: true)`, whose `OpenHistory()` made 56 synchronous `SaveText` calls on the STA thread and then loaded two History pages and an inline diff (`git show 28dae96e:…/RightPaneRailTests.cs`, the `OpenHistory` helper; `RunSta` was at line 396). `e7ebfe30` (#1853, 2026-11-07) moved History out of the arrows' reach and the fact now starts at `outline` with no history (`T/RightPaneRailTests.cs:30-46`, `Rail` at `:188-203`, `RunSta` at `:269-270`). The 56-save shape survives in `T/HistoryPanelTests.cs:865` and `T/HistoryAsyncInterleavingTests.cs:119`.
  - **The dump is gone.** The app job's results are kept 7 days (`windows.yml:817-823`); the API no longer lists the attempt-1 artifact.
  - **The host was not idle.** `C:\slate-ci\logs\orchestrator-2026-11-05.log:183-186` (times are UTC−5): the Rust slot's VM committed its cache generation at 02:03:32Z ("commit rust generation 112 (trusted main)"), the model shard's VM was provisioned on the same slot at 02:03:43Z, handed off at 02:03:55Z and claimed at 02:05:06Z. The fact ran from about 02:03:51Z (the failure was reported at 02:06:04Z after 2 min 13 s) and its dump began at 02:04:56Z. The fact's whole minute is the other slot's cache commit and VM start. The issue's "nothing heavy was running on this desktop" leaves out the runner host's own turnover. A cache commit happens only on a trusted `main` push (`docs/runbooks/self-hosted-windows-runner.md:1953`), which this was.
  - Still true at the baseline: the rail's poll is a real `DispatcherTimer` at Background priority (`S/RailSettle.cs:39`), 50 ms in the facts (`RightPaneRailTests.cs:193`); the facts wait on its tick count with a 10 s bound, which is a condition wait, not a clock oracle.

- **Cause:** unknown, and no longer knowable from that run. The best-supported hypothesis, from the two logs: the app job's VM stalled on disk while the host committed slot 2's cache disk and started the next VM, and the fact was in its one long synchronous disk-bound stretch (the 56 saves) at the time. One instance; **unverified**. A diagnosis step would record: every home-pool STA timeout since 2026-10-11 set against the orchestrator's `commit … generation` and `provisioned` lines within the same two minutes.

- **Fix shape:**
  1. No change to the fact. Its heavy arrangement is already gone.
  2. Make the next such timeout name its kind. In `T/Support/StaThread.cs`, when the stack cannot be taken, add to the message: the longest gap a process-wide heartbeat thread saw during the body (a stalled process or VM shows as a gap on every thread), the GC pause time over the body (`GC.GetTotalPauseDuration()`), and the memory load. That tells "the machine stopped" from "this thread blocked" from the log alone, which lives longer than the 7-day artifact.
  3. A comment on the issue with the timeline above, and the correlation recorded in `docs/runbooks/self-hosted-windows-runner.md` under "Observed timings".
  4. Not in this cluster: whether the host's cache commit should yield to a running slot. That is the runner's code (`C:\slate-ci\bin`), and the CI chunk's to weigh.

- **Failing test first:** `T/StaThreadTests.cs`, suggested name `ATimedOutBodyThatDoesNotPumpSaysWhetherTheProcessStalled`. The existing wedge helpers there run a body that blocks without pumping past a short bound (`:325`, `:436`). The heartbeat's reading comes through a seam, so no real stall is needed: with the seam reporting a 40 s gap the message says the process stalled; with it reporting none the message says only this thread was blocked. Red today: the message has neither sentence.

- **Size:** S. It rides with #1826.
- **Lanes:** windows (unit).
- **Screen-reader work:** none.
- **Changes text or speech a Mac user would hear:** no.
- **Owner decision needed:** yes. (1) "May #1843 close with its cause not proved, on the grounds that the fact was replaced, the capture expired, and the harness now says more?" Recommended: yes. (2) "Should a timed-out fact's dump be kept 30 days instead of 7?" Recommended: no; a dump is about 5 GB, and the message change makes the 90-day log enough to triage. (GitHub's log retention of 90 days is the default; **unverified** for this repository.)
- **Shares files with:** #1826 (`T/Support/StaThread.cs`, `T/StaThreadTests.cs`). `windows.yml` only if retention changes, which I recommend against.

---

## Cluster section

### 1. What the six issues turned out to be

| Issue | Filed as | Is |
|---|---|---|
| #1624 | a 45-minute hang | one fact outlasting a per-fact timer on a loaded host; evidence lost to a rerun |
| #1625 | (grouped with the flakes) | a deterministic gap in the model; a decision |
| #1673 | a fixture teardown race | exactly that; the one clean instance of the brief's second shape |
| #1788 | a save starved by the settle | very probably a save lost to the gate's own file reader; fixed by #1870 |
| #1826 | a crash at thread exit | a slow process exit from leaked per-thread WPF listeners; fix not written |
| #1843 | a stuck settle loop | a synchronous stall during the runner host's slot turnover; fact since rewritten |

Only #1673 is the class as the brief words it. The others share a different thread: **the evidence did not survive long enough to be read.** #1624's dumps were overwritten by the rerun, #1843's expired in 7 days, #1788's app log was never uploaded, and #1826 took nine comments because the adapter hides the one sentence that named the cause. That is worth a line in the plan's rules: a flake's PR records where its evidence lives and for how long.

### 2. The sweep

Counts are `grep` over `*.cs` outside `bin` and `obj`.

| Primitive | Unit project (`T/`) | Gate project (`G/`) |
|---|---|---|
| `Thread.Sleep(` | 64 in 27 files | 47 in 14 files |
| `Task.Delay(` | 46 in 19 files | 0 |
| `SpinWait.SpinUntil(` | 123 in 37 files | 437 in 21 files |
| `PumpedDispatcher.PumpUntil(` | 390 in 76 files | 0 |
| `.Wait(TimeSpan…` | 340 in 73 files | 0 |
| elapsed-time comparisons | 36 lines; about 14 are assertions, the rest loop bounds | — |
| `using`-declared `ManualResetEventSlim` | 372 in 60 files | — |
| `new HeldWork(` (the house hold, `T/Support/HeldWork.cs`) | 12 in 2 files | — |

Most of the bounded waits are the right shape: a condition polled under a safety bound. The wrong shapes are four.

**Shape 1a — a fixed sleep, then an assertion.** About 30 of the 64 unit sleeps and about 35 of the 47 gate sleeps. By file, heaviest first: `G/ShellAccessibilityTests.ThemeReadability.cs` 14, `T/SyncMarkerWatcherTests.cs` 13, `T/StaThreadTests.cs` 9 (these test the timeout itself; mostly legitimate), `G/…Announcements.cs` 6, `T/RescanTests.cs` 6 (poll intervals), `G/ShellAccessibilityTests.cs` 5, `G/…Rescan.cs` 4.

**Shape 1b — a wall-clock upper bound as the oracle.** About 14 assertions in the unit project.

**Shape 2 — a fixture that releases a gate and tears down without waiting for the released work.** 13 fixture `Dispose` methods release a gate (a line-window heuristic, so a floor); 7 of them, #1673's included, wait for nothing or dispose the event they just set.

**Shape 3 (found on the way, the mirror of #1788) — a gate poll that holds a file the app replaces by rename.** 8 polls in the gate project read an app-written file with `File.ReadAllText`, which shares no delete. The polls tolerate their own `IOException`; nothing protects the app's rename. Their own comments say so ("the CI sharing violation this journey tripped on PR #1124", `G/ShellAccessibilityTests.cs:4304-4306`).

**The riskiest instances:**

1. `T/CanvasEndToEndTests.cs:316` `LargeCanvasOpensNavigatesAndWindowsUnderBudget`: one cold reading each for `open < 500 ms` (`:326`), first derivation `< 500 ms` (`:338`), pan `< 100 ms` (`:348`), navigator step `< 50 ms` (`:358`), in an ordinary fact. This is the exact shape #1915 fixed in `CanvasDocumentTests` after #1901 failed at 598 ms; `T/WallClockBudget.cs` exists for it and this fact was not moved to it.
2. `T/AccessibilityNotificationDispatcherTests.cs:1384` `TheLaunchWindowStartsAtTheFirstFrameNotAtConstruction`: `Thread.Sleep(1 s)` (`:1400`), then `window.Show()` must reach its first frame in under that second (`:1408-1410`), then `Thread.Sleep(100 ms)` and a strict inequality (`:1411-1412`).
3. `T/W1HardeningTests.cs:1411` `FiveThousandItemRefreshReturnsWithinTheUiDispatchBudget`: `stopwatch.Elapsed < 100 ms` around a sidebar's construction (`:1439-1441`).
4. `T/SyncMarkerWatcherTests.cs:77` and `:122`: a real `FileSystemWatcher` and a real debounce, then a fixed 1.5 s or 3 s sleep and `Assert.Equal(1, fires)`. A loaded host can split the burst (red) or deliver the second fire late (green for the wrong reason). Nine more `Thread.Sleep(Settle)` at `:194`–`:465`, and `elapsed < 5 s` at `:154-156`.
5. `G/ShellAccessibilityTests.ThemeReadability.cs:31`, `:36`, `:39`, `:62`, `:184`–`:372`: fourteen fixed sleeps of 300–800 ms, each followed by a pixel measurement of what should have painted by then.
6. `G/ShellAccessibilityTests.HotExit.cs:131`: `Thread.Sleep(3 s)` "for the writer's one-second debounce", then the process is killed and recovery asserted. And `:88`, a 2 s window for "no late duplicate".
7. `G/ShellAccessibilityTests.ReadingSelection.cs:51` and `:65`: 300 ms, then a pixel count.
8. `G/ShellAccessibilityTests.ShellRegions.cs:351-360` `AssertKeepsFocus`: 250 ms + 750 ms, then "focus has not moved"; and `G/ShellAccessibilityTests.cs:11595-11596`, `:11659-11660`, 2 s negative `SpinUntil`s. These can only pass wrongly, which is the quieter failure.
9. `T/Censuses/EventKindsCensus.cs:105` and `T/Censuses/CallbackConcurrencyCensus.cs:273`: `Thread.Sleep(400)`, then "no event arrived".
10. `T/GraphDocumentTests.cs:1346` and `:1386` (`teardown < 3 s`), `T/HeldVaultStoreFileTests.cs:68` (`< 2 s`), `T/W2EditorInteractionRedTeamTests.cs:181-183` (`< 2 s`), `T/RescanTests.cs:1600`, `:1641` (`< 10 s`): an elapsed ceiling as the proof that something did not wait.
11. Shape 2, the #1673 siblings: `T/ReadingDiagramTests.Stage.cs:137-146` and `T/ReadingFindSurfaceTests.cs:122-131` (set the gate, dispose the tab and the session, wait for nothing; the same model type as #1673, so `WhenRefreshWorkDrained` fits both); `T/GraphTableTests.Landing.cs:51-56`, `T/FinalMergeDeltaTests.cs:454-466`, `T/SaveChainAdmissionTests.cs:1517-1528` (set an event and dispose it in the next statement, while a pool thread may still be waking on it or not yet have reached it); `T/CommandPaletteTests.Shell.cs:391-432` (gates set, session disposed, no wait between). The tree already has the two right patterns: `HeldWork` (a `TaskCompletionSource`, nothing to dispose) and "the events are left to the collector" (`T/GraphDiagramTests.Lineage.cs:48-51`, `T/GraphDocumentTests.RequestLineage.cs:94-97`).
12. Shape 3: `G/ShellAccessibilityTests.cs:2982`, `:3206`, `:4309`, `:4683`, `:4704`, `:4924`, `:10587`, and `G/ShellAccessibilityTests.Settings.cs:194`. Whether core's note save retries a refused rename is **unverified**; the graph config and the vault stores now do (#1870, #1906).

Lower risk, same family: the "release after a delay, then show the blocking call returned after the release" facts (`T/RescanTests.Round3.cs:143`, `:226`; `T/RescanTests.Round4.cs:159`; `T/FinalMergeDeltaTests.cs:368`; `T/QuickSwitcherRankCoordinatorTests.cs:212`; `T/BasesExportTests.cs:330`). The delay only widens the window, so load does not break them unless the pool is so starved that the release misses the product's own 5 s fallback. `T/BasesExportTests.cs:85` waits a fixed 10 s for a negative on every run.

### 3. A census that keeps the class closed

Models already in the tree: `T/Censuses/StaThreadCensus.cs` (Roslyn over the unit project's sources, a named allow-list, and a check that fails on a stale allow-list entry) and `T/Censuses/ShellLaunchCensus.cs` (the unit project parsing the gate project's sources, with a floor count so a walk that missed files fails). Both use `T/Support/CSharpSource.cs` and `SourceText.RepoRoot()`.

A new `T/Censuses/TestWaitCensus.cs`, one fact per rule, over both test projects:

1. **No new fixed sleep before an assertion.** Every `Thread.Sleep` or awaited `Task.Delay` that is a statement of its own (not inside a `while`, a `SpinUntil` lambda or a seam's body, where it is a poll interval or a deliberate hold) must be in a pinned list of (file, member, count, one-word reason). A new one fails; a removed one must leave the list. The list starts at today's sites and the sweep PR shrinks it. This is a ratchet, as `StaThreadCensus.OwnTheirDispatcherLifecycle` is.
2. **An elapsed-time assertion goes through `WallClockBudget` or is tagged `[Trait("kind", "benchmark")]`** (the app job already filters `kind!=benchmark`, `windows.yml:774`). Detect `Assert.True`/`InRange` whose argument compares a `Stopwatch` reading with a constant.
3. **No test type disposes an event it sets in the same `Dispose`,** and a `Dispose` that sets a gate and disposes a session has a drain between them (`PumpUntilDrained`, `WhenAllWorkDrained`, `WhenRefreshWorkDrained`, `HeldWork`). The first half is exact; the second needs an allow-list for fixtures whose lifecycle owns the drain (`T/RescanTests.cs:2567`).
4. **In the gate project, a file under the vault or the device root is never read with `File.ReadAllText`/`ReadAllBytes` inside a poll.** One shared reader that opens with delete sharing; the census refuses the direct call inside a lambda or a loop.

Rule 1 will have false positives; the allow-list with a reason is the release valve, and the PR that adds an entry has to say why a condition could not be waited on.

### 4. Recommended cut into PRs

Three can run at once in the first wave; they share no files.

- **PR A — #1624.** `test(windows)` + `ci(windows)`: the model reports a stalled case, a slow shard is not killed at 45 minutes, a failed attempt keeps its dumps. Files: `T/ModelTestRun.cs`, `T/ModelTestRunTests.cs`, `windows.yml` (model step and its upload), the runbook paragraph. First because every later PR pays for model-shard reruns. It is the only PR here that edits `windows.yml`; sequence it against the CI chunk.
- **PR B — #1826 with #1843.** `test(windows)`: a fact's thread leaves no WPF exit listener, and a timeout that could not take a stack says whether the process stalled. Files: `T/Support/StaThread.cs`, `T/StaThreadTests.cs`, `T/Censuses/StaThreadCensus.cs`, perhaps `T/Support/ExitWatch.cs`. Together because both rewrite the same runner. Based on `c2377210`. This removes the most frequent red on `main`, so if only one PR can go first, it is this one.
- **PR C — #1673 and its six siblings, with census rule 3.** `test(windows)`: no fixture tears down under work it released. Files: `T/ReadingFocusTests.cs`, `T/ReadingDiagramTests.Stage.cs`, `T/ReadingFindSurfaceTests.cs`, `T/GraphTableTests.Landing.cs`, `T/FinalMergeDeltaTests.cs`, `T/SaveChainAdmissionTests.cs`, `T/CommandPaletteTests.Shell.cs`, new `T/Censuses/TestWaitCensus.cs`.
- **PR D — the clock sweep in the unit project, with census rules 1 and 2.** Instances 1–4, 9 and 10 above; the gate's sites pinned as they are. After PR C, because both edit `TestWaitCensus.cs`.
- **PR E — the gate half, closing #1788.** Instances 5–8 and shape 3, census rule 4, the shared reader in `ExpectFile`, and the settle-and-save pin. After PR D (it removes gate entries from the pinned list). Runs the shell gate lane. Touches `G/ShellAccessibilityTests*.cs`, which other chunks also touch; give it a slot of its own.
- **PR F — #1625, after the decision.** Under the recommended (b): comments in `T/ConnectionsLeafTests.Model.Composed.cs` and one lifecycle fact; S; any wave. Under (a) it is a large chunk of its own with a Codex review.

Per the brief's §9, all six are tests, CI config or comments, so each goes on its tests alone, except that PR B reaches into WPF's private members and deserves a red team for the foreground regression it could cause.

### 5. Risks

- **The baseline is stale by one commit that matters.** #1826's spec written against `1ec9bf31` would describe a tree without `ExitWatch`.
- **PR B's fix rests on WPF internals I did not verify.** If a listener run on its own thread does not unhook itself from `ProcessExit`, the fallback is to drop the listeners at the last test, which leaves the leak (and whatever part of the 5 GB the process holds mid-suite is theirs; **unverified**). The failing test settles which before any design is committed.
- **The foreground regression is invisible to CI.** Only a full local run on an interactive desktop shows `ReadingFocusTests` losing activation. PR B's verification needs one, and it takes about 35–45 minutes.
- **Three issues close on inference, not replay** (#1788, #1843, half of #1826). Each is a named owner decision above. If the owner says no to any, that issue stays open with a watch, and the plan's "nothing unowned" needs a line for it.
- **Evidence expires.** App results live 7 days, gate and model artifacts 30. A flake filed and read a week later has only its log. #1843's dump expired the day of this reading.
- **A census ratchet with 150 pinned sleeps is friction.** If the allow-list is easy to extend without a reason, it will be extended. The stale-entry check and a required reason are what make it hold.
- **The home pool makes its own weather.** #1624 and #1843 both happened while the host was busy with something the issue did not count: a stress loop in one, a slot's cache commit and VM start in the other. Neither is a test defect. Until the host's turnover is known to be harmless, a single home-pool timeout should be read against `C:\slate-ci\logs` before it is filed as a flake. That check takes two minutes and would have answered #1843 on the day.
- **`G/ShellAccessibilityTests.cs` and `windows.yml` are shared across clusters.** PR A and PR E are the two places this cluster can collide with others in a wave.
