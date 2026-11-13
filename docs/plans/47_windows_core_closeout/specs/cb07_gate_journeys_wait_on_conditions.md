# CB-7 — The gate's journeys wait on conditions and never hold a file the app replaces, and #1788 closes

Issues: #1788. Size M. Lanes: windows (the app job for the census and the pin; the FlaUI shell gate for everything else). AT: — (test-only). Wave: C3, slot 1. Depends on: CB-4 and CB-6, both merged (`TestWaitCensus.cs` and rule 1's pinned list are theirs). Parallel with: CB-9a, CB-10 (no shared files). Decision: CD3. The owner answers it before C0 (plan §3); this spec encodes "yes". Closing needs both that yes and a green pin on `main`. If the answer is no, the diff does not change: the PR writes `Refs #1788`, the issue stays open with a comment naming the evidence the next failure will leave (Design 9), and CB-46's table carries it. Interlock: the gate's partial files are shared with CB-19, CB-24, CB-17b, CB-20, CB-36, CB-18, CB-39 and CB-40. CB-7 lands first and edits only the lines named here. It does not edit `windows.yml`. Review: its tests alone (rule 9). If the pin fails on `main` (Design 8), that step stops, the record goes on #1788, no product code changes, and the PR writes `Refs`.

`G/` is `apps/slate-windows/tests/SlateWindows.AccessibilityTests/`, `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`, `S/` is `apps/slate-windows/src/SlateWindows/`. `SAT.cs` is `G/ShellAccessibilityTests.cs`.

## Goal

A gate journey measures or asserts when the state it needs has arrived, not after a fixed time. A journey that checks "nothing moved" counts reads that saw it in place. No gate read can refuse the app's rename over a file, which is how the gate lost a save for the product it was measuring. `TestWaitCensus` keeps both closed. #1788 closes on #1870's fix, a pin, and a gate that leaves the app's log behind when a save never lands.

## Current state on main

Verified at c2377210. The reading's counts drifted; these replace them.

- **Sleeps.** 47 `Thread.Sleep` in 14 files. 18 are poll intervals inside a loop or a `SpinUntil` lambda (all five in `SAT.cs`: `:2170`, `:7417`, `:12320`, `:12418`, `:12465`; four of six in `Announcements.cs`). **29 are statements of their own**, not "about 35":

| Class | Sites | Count |
|---|---|---|
| Before a pixel measurement | `G/ShellAccessibilityTests.ThemeReadability.cs:31`, `:36`, `:39`, `:62`, `:184`, `:193`, `:211`, `:228`, `:299`, `:302`, `:307`, `:334`, `:364`, `:372`; `…ReadingSelection.cs:51`, `:65` | 16 |
| Before a kill | `…HotExit.cs:131` (3 s "for the writer's one-second debounce") | 1 |
| Before "focus has not moved" | `…ShellRegions.cs:356` (`AssertKeepsFocus`, `:353-360`); `…RegionStops.cs:549` (`AssertFocusStays`, `:547-565`) | 2 |
| A window for late events | `…Announcements.cs:281` (`AwaitHeard`); `…Palette.cs:225`; `…HotExit.cs:88`; `…RegionStops.cs:369`; `…Rescan.cs:693`, `:883` | 6 |
| After registering a listener, before clearing its queue | `…RegionStops.cs:359`; `…Rescan.cs:679`, `:873` | 3 |
| A deliberate hold | `…Announcements.cs:130` (the window stays untouched while launch lines queue) | 1 |

- **Negatives with no sleep.** `SAT.cs:11595-11597` and `:11659-11664` (`GraphConnections_LeafWalkDepthAndReRoot_AreClean`): `Assert.False(SpinUntil(moved, 2 s))`. A null `FocusedElement()` reads as "not moved", so a UIA fault passes.
- **Shape 3 is ten polls, not eight.** The reading missed `SAT.cs:6563` and `DeviceState.cs:97`; both reach the read from a `SpinUntil` lambda, the second and `:10587` through a local function.

| # | Poll | Fact | File | Writer, and what it does when the file is held |
|---|---|---|---|---|
| 1 | `SAT.cs:2982` | `TaskPanels_RowsToggleAndReviewCarriesTheMacShapes` | vault `todo.md` | core save; retries |
| 2 | `SAT.cs:3206` | `PropertiesHeader_RowsEditSheetsAndRenameCarryTheMacShapes` | the vault note (`notePath`) | core save; retries |
| 3 | `SAT.cs:4309` | `BasesSurfaces_GridBuilderAndLeaves_AreClean` | vault `alpha.md` | core save; retries |
| 4–6 | `SAT.cs:4683`, `:4704`, `:4924` | `HistorySurfaces_LeafDiffAndRestore_AreClean` | vault `alpha.md` | core save; retries |
| 7 | `SAT.cs:6563` | `Templates_PickerPromptsCreateAndCancel_AreClean` | vault `Journey Note.md` | core save; retries |
| 8 | `SAT.cs:10587` (`ReadFile` `:10583-10593`, polled by `ExpectFile` `:10594-10600`) | `GraphInspector_FiltersGroupsAndForces_AreClean` (`:10535`) | vault `.slate/graph.json` | `GraphConfigWriter`; retries since #1870 |
| 9 | `Settings.cs:194` (`AssertFirstPaint`, `:178`) | `ThemePreference_ChoosingLightStaysLightAcrossARestart` | device `preferences.json` | `AppPreferencesStore.Save`; **no retry** |
| 10 | `DeviceState.cs:97` (`RecordsVault`, polled at `:62`) | `ShellGate_WritesItsRecentsUnderTheRunsOwnRoot` | device `recent-vaults.json` | `RecentVaultsStore.Save`; **no retry** |

  Polls 1 to 8 catch `IOException` only; poll 9 catches nothing and ends silently at its deadline. A replace in flight can also surface as `UnauthorizedAccessException`, which fails the journey.
- **Does core's note save retry a refused rename? Yes.** `crates/slate-core/src/vault/fs.rs:1309-1326` (`publish_staged_replacing`, called by the Windows save at `:1013`) retries os errors 5 and 32 ten times, waiting 5, 10, 20, 40, 80, then 100 ms four times: 555 ms at most. `S/Graph/GraphConfigWriter.cs:162-194`, `:261-269` waits 1.575 s (#1870). So for polls 1 to 8 the shared reader is a courtesy: it stops the gate pushing the product onto its retry path, and removes the small chance that a spinning poll meets all ten attempts. **For polls 9 and 10 it is a correctness fix.** `S/AppPreferencesStore.cs:152-173` and `S/RecentVaultsStore.cs:125-145` do one `File.Move(…, overwrite: true)`. `S/DevicePreference.cs:31-54` logs `AppPreferencesPersistFailed` and the choice is never written; `S/VaultLifecycleViewModel.cs:1215-1228` logs, speaks "Could not save recent vaults" and drops the record. That is #1788's shape, live.
- **Other reads.** 27 `File.ReadAllText`/`ReadAllBytes` calls in `G/`: the ten polls; three fixture reads from `AppContext.BaseDirectory` (`SAT.cs:1559`, `Palette.cs:466`, `SpokenChords.cs:26`); 14 single reads of a vault or device file (six in `HotExit.cs`, three in `Settings.cs`, two in `SheetFence.cs`, `SAT.cs:7004`, `:7188`, `:7448`), most of them while the app runs. Five `new FileStream` sites already share delete (`SAT.cs:2508`, `:2571`, `:6783`, `:6946`, `:12299`).
- **Signals the gate already has.** `FocusSettleWatch.Watch` (`G/FocusSettleWatch.cs:29-70`): counts reads, not wall time (#1611); used by `AssertFocusStays(automation, Func<bool?>, message)` (`FilesTree.cs:330-339`). Three equal reads 100 ms apart (`WaitForOneSelectedRow`, `Palette.cs:329-350`). A pixel predicate polled for 10 s (`WaitForGold`, `Settings.cs:79-88`). `SelectionText` (`FindInNote.cs:151`), `CapturePhysicalPixels` (`ReadingSelection.cs:112-123`), `ListenOnTheDesktop` (`Announcements.cs:223`).
- **Evidence.** `windows.yml:1174-1184` uploads `slate-windows-accessibility-<sha>-<attempt>` for 30 days: the TRX and `SLATE_ACCESSIBILITY_EVIDENCE_DIR`. `PreserveEvidence(testName, logDirectory)` (`SAT.cs:2550-2580`) copies the app's log there, and has two callers (`:3006`, `:6741`). The graph inspector fact has none and its `finally` (`:10862`) deletes the log, so #1788's app log was never uploaded. `RunFindInNoteJourney` (`FindInNote.cs:154-214`, 19 call sites, every pixel fact) has none either.
- **#1788.** Run 41697152538, first attempt, 2026-11-03, before #1870 (df67ee39, 2026-11-09). The repel step is `SAT.cs:10803-10805`. The issue's product lines still hold: `S/Graph/GraphPreferencesViewModel.cs:62` (400 ms), `:111` (`DispatcherPriority.Background`), `:232-242`, `:327-338`; `S/Graph/GraphInspectorViewModel.cs:219-229`; `S/Graph/GraphLayoutDriver.cs:237`. Each step computes off the dispatcher (`:116-143`), which is why starvation is unlikely. #1870's red-first facts: `T/GraphConfigWriterTests.cs:211`, `:239`, `:268`.
- **`T/Censuses/TestWaitCensus.cs` does not exist yet.** CB-4 creates it (rule 3); CB-6 adds rules 1 and 2 and pins the gate's sleeps as they are.

## Reference

`T/Censuses/ShellLaunchCensus.cs` (the unit project parsing `G/`, with a floor count); `StaThreadCensus.cs:33-65` (an allow-list that fails on a stale entry); `G/FocusSettleWatchTests.cs` (gate-project facts that need no window); plan 45 XB-1; `reading/01_test_stability.md`, #1788 and the cluster section.

## Design

1. **The shared reader.** New `G/GateFile.cs`:

   ```csharp
   internal static class GateFile
   {
       internal static FileStream Open(string path);      // FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete
       internal static string? TryReadText(string path);  // null: absent, or the open or the read was refused
       internal static byte[]? TryReadBytes(string path);
       internal static string ReadText(string path);      // a single read: retries a refusal for 5 s, then throws naming the path
   }
   ```

   `Try*` catch `IOException` (which covers not-found) and `UnauthorizedAccessException`. Text decodes as `File.ReadAllText` does (UTF-8, byte-order marks detected). `ReadSharedLog` (`SAT.cs:12288`) becomes a call to it.
2. **The ten polls.** Beside `DiagnosticFileHead` (`SAT.cs:2495`):

   ```csharp
   private static string ExpectFileText(
       string path, Func<string, bool> ready, string what, string testName, string logDirectory, TimeSpan timeout)
   ```

   It polls `GateFile.TryReadText` and returns the text that satisfied `ready`. On a timeout it calls `PreserveEvidence(testName, logDirectory)` and throws with `what`, `DiagnosticFileHead(path)` and the app log's last lines. Polls 2 to 7 and 9 become one call each, keeping their own timeout and message. Poll 1 keeps its miss branch (`:2994-3009`, it already preserves) and swaps only the read. Poll 8: `ReadFile` returns `GateFile.TryReadText(configPath) ?? string.Empty`, and `ExpectFile` preserves evidence on a miss. Poll 10: `RecordsVault` parses `GateFile.TryReadBytes`. The 14 single reads become `GateFile.ReadText` (`:7448`, a failure message, `TryReadText`).
3. **Pixels: a state witness, then a settled frame, then one measurement.** Polling the assertion itself is wrong here: `ThemedShell_ADisabledGridStaysOnItsSurface` guards an animation that ends in a bad colour, and its first frame is good. New, beside `CapturePhysicalPixels`:

   ```csharp
   private static void WaitForSettledPixels(AutomationElement surface, string what)
   ```

   It captures every 100 ms inside a loop until three captures in a row are equal, 10 s at most, and fails with `what` and the capture count. Per site:

   | Sleep | Witness, then |
   |---|---|
   | `ThemeReadability.cs:31` | deleted; `WaitForAppMenu` (`SAT.cs:12263`) is the wait |
   | `:36` | settled(`file`) |
   | `:39` | the logo's `ExpandCollapseState` is `Collapsed`; settled(`tab`) |
   | `:62`, `:211`, `:302`, `:334` | the element is already waited for; settled(it) |
   | `:184` | `AssertEventuallyFocused(editor, …)`; settled(`editor`) |
   | `:193` | `SelectionText(editor).Length > 0`; settled(`editor`) |
   | `:228` | a rail row other than `rows[1]` has keyboard focus; settled(that row) |
   | `:299` | `WaitForOneSelectedRow` names a row starting "Open Graph" |
   | `:307` | `grid.IsEnabled` is false; settled(`grid`) |
   | `:364` | a selected `ListItem` exists under the combo; settled(it) |
   | `:372` | the combo's state is `Collapsed` |
   | `ReadingSelection.cs:51`, `:65` | focus (`:45-49`) and selection (`:60-64`) are already waited for; settled(`reading`) |

4. **The kill.** `HotExit.cs:131`: snapshot the names and bytes of `*.json` under `ShellLaunch.Root\hot-exit` before `TypeIntoTheNote`; after it, wait (15 s) until an entry is new or changed, read through `GateFile`; then kill. Layout: `crates/slate-core/src/hot_exit.rs:70-81`.
5. **"Nothing moved" counts reads.** `AssertKeepsFocus` and `RegionStops`' `AssertFocusStays` both become a call to `FilesTree.cs:330`'s overload, with a read that answers true, false, or null on a transient UIA fault. `SAT.cs:11595` and `:11659` the same, the second's read also false when the heading changed. At `:11595` the comment says the shell posts a line. Trace `WorkspaceFocusBoundary.RightPaneEdge` (`S/WorkspaceViewModel.Layout.cs:1239`, `S/MainWindow.xaml.cs:1075`); if it does, hear core's rendering of it through `ListenOnTheDesktop` first, as the witness that the chord arrived. If not, the watch alone goes in and the PR says so.
6. **What stays pinned, and why.** A sleep is fixed here when the read after it can go red because the state had not arrived, or when a read-counting watch can replace it: 19 sites. Ten stay in rule 1's list, each with a reason word:
   - `late-arrival` (6): `Announcements.cs:281`, `Palette.cs:225`, `HotExit.cs:88`, `RegionStops.cs:369`, `Rescan.cs:693`, `:883`. Each asserts that nothing more is heard or moved; no signal ends a wait for an event that must not come. Two of them are today also the only wait for the expected event, so each gains a wait in front: `RegionStops.cs:369` waits until `focused` ends with the choice and `heard` holds the filter's line; `Rescan.cs:693` until `focusMoves` holds a `c.md` entry.
   - `registration` (3): `RegionStops.cs:359`, `Rescan.cs:679`, `:873`. Too short a settle fails red, never green.
   - `deliberate-hold` (1): `Announcements.cs:130`.
7. **Census.** In `T/Censuses/TestWaitCensus.cs`:
   - Rule 1: delete the 19 gate entries, give the ten their reason words, in the shape CB-6 chose. Its stale-entry check proves each deletion.
   - Rule 4, `TheGateReadsAppFilesThroughGateFileOnly`. The plan's wording ("inside a poll") cannot be checked from syntax: two of the ten polls reach the read through a local function. So the census checks the stricter, exact form, with no allow-list: in `G/`, a call to `File.ReadAllText`, `ReadAllBytes`, `ReadAllLines`, `ReadLines`, `OpenRead`, `OpenText` or `Open` (and the `Async` forms) is refused unless its path argument is built from `AppContext.BaseDirectory`; a `new FileStream(` outside `GateFile.cs` is refused unless it names `FileShare.Delete`. A floor on the files walked and on `GateFile.` calls seen, as `ShellLaunchCensus` has.
8. **The pin, and what closes #1788.** `AForcesEditIsSavedWhileTheDiagramIsStillSettling`, in `T/GraphDiagramTests.Lineage.cs` (it has `Host`, `SettledModel` and the seam's use at `:1545`). Real workspace, real `GraphLayoutDriver`, the preferences' real timer at its production priority and window; never `FireTickForTests`. `model.InsideGateForTests = () => Thread.Sleep(40)`, so every compute outlasts the 16 ms cadence. Change repel through `GraphInspectorViewModel.SetForces` (`:219`). Then `PumpedDispatcher.PumpUntil`, 10 s, on a predicate that restarts the settle whenever it converges (`if (!driver.IsSettling) driver.StartSettle();`) and is true when the preferences have no pending pair and no outstanding write. Assert `StepsForTests` advanced during the wait and the settle is still running; then, after `WhenWritesDrained`, one read of `graph.json` carries the new repel. The oracle is that the save lands while steps keep coming; no duration is asserted, and the file is not read while the writer runs. Expected green on `main`. Show it can fail: with `ScheduleSave()` removed at `GraphPreferencesViewModel.cs:241` it goes red; quote that in the PR. **If it is red on `main`**, the issue's cause may be real, and CD3 says what happens: the chunk stops there. Post the red run and the trace of which timer starved on #1788, write `Refs #1788` with `not delivered: the product fix waits for the owner (CD3)`, and change no product code. A red pin does not show that any one priority change is the remedy; that is decided after the owner reads the record.
9. **Evidence (rule 6).** Every fact this PR edits keeps its app log on any failure: `RunFindInNoteJourney` gains `catch { PreserveEvidence(name, logDirectory); throw; }`, and each other edited fact the same around its body (both log folders in the hot-exit facts). Download the failing run's artifact (`slate-windows-accessibility-c30e15cf…-1`, expires 2026-12-03) and say in the PR whether the app log is in it.
10. **Not fixed here; each is one line on "Plan 47 deferred finds"** (plan §4 rule 10; plan §9 already names both, so CB-46 owns them and no issue is filed). (a) The device stores lose a save when their file is held (check `WindowStateStore`, `FileRecentsStore`, `CommandPaletteRecentsStore` and `SidebarWriteJournal` for the same single `File.Move` first). Six product files, so not an inline fix. (b) The rest of the gate's facts still delete their app log on failure; a general fix is one launch-and-teardown seam over 56 launch sites, which needs a design.

## Tests

- `G/GateFileTests.cs`, no window. Red first: `AHeldGateReadDoesNotRefuseARenameOverTheFile` (hold `GateFile.Open`, then `File.Move(staged, path, overwrite: true)` succeeds; the control, a `File.OpenRead` holder, throws). `TryReadTextIsNullForAnAbsentOrUnsharedFile`. `ReadTextWaitsOutABriefHold`.
- `G/SettledPixelsTests.cs`, with the loop split from the capture so it runs on fed frames: three equal frames settle; frames that never repeat fail at the bound.
- `T/Censuses/TestWaitCensus.cs`: rule 4, and its planted case over source strings (a direct read in a lambda, in a loop and in a local function are each reported; a fixture read and a delete-sharing stream pass). Seen red before the reads move, when it lists the 24 reads.
- The pin (Design 8).
- The edited journeys are the rest. A wait that replaces a sleep has no red-first form; the PR says so per site.

## Docs and registers

None. No sentence, chord, contract row or matrix changes. `PreserveEvidence`'s summary gains one line naming the artifact and its 30 days.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-cb7'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
$unit = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
dotnet test $unit -c Release --no-build --filter 'FullyQualifiedName~TestWaitCensus|FullyQualifiedName~AForcesEditIsSavedWhileTheDiagramIsStillSettling'
$gate = 'apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj'
dotnet test $gate -c Release --no-build --filter 'FullyQualifiedName~GateFileTests|FullyQualifiedName~SettledPixelsTests'
$env:SLATE_ACCESSIBILITY_EVIDENCE_DIR = "$env:TEMP\cb7-evidence"
$names = 'ThemedShell_','ReadingView_ASelection','HotExit_','EditorPopover_','RegionStops_ArrowsStayInRegion','GraphConnections_','GraphInspector_','TaskPanels_','PropertiesHeader_','BasesSurfaces_','HistorySurfaces_','Templates_','ThemePreference_','ShellGate_','ExternalFiles_TheReadersRowRemoved'
dotnet test $gate -c Release --no-build --filter (($names | ForEach-Object { "FullyQualifiedName~$_" }) -join '|')   # NVDA closed, desktop free; run it twice
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include apps/slate-windows/tests/
```

**The PR's own gate run is the proof.** The local desktop is not the VM the flakes happen on. The PR body gives the gate job's run id and attempt on the final head and, from its TRX, each of these as Passed with its duration: the eight `ThemedShell_` facts with a sleep (`APalettesTextKeepsItsClearType` may read Skipped; say so), `ReadingView_ASelectionStandsOutAndKeepsItsText`, both `HotExit_` facts, `EditorPopover_F6AndShiftF6StayInsideIt`, `RegionStops_ArrowsStayInRegion`, `GraphConnections_LeafWalkDepthAndReRoot_AreClean`, `ExternalFiles_TheReadersRowRemovedUnderThemLandsTheKeysBeside`, and the eight facts of the polls table. A red attempt before the green one is named with its failing fact, its helper's message and its preserved log; it is not rerun away.

## Acceptance

- [ ] `GateFile` exists; the ten polls and the 14 single reads go through it; rule 4 is green with no allow-list and its planted case is red where it should be.
- [ ] 19 fixed sleeps are gone, each replaced as Design 3 to 5 give it; the ten that stay carry `late-arrival`, `registration` or `deliberate-hold` in rule 1's list, and its stale-entry check passes.
- [ ] The four "nothing moved" checks go through `FocusSettleWatch`; `SAT.cs:11595` hears the edge's line first, or the PR says no line exists.
- [ ] The pin is green on `main` and red under the quoted mutant; or it is red on `main`, the record is on #1788, no product code changed, and the PR says the fix waits for the owner (CD3).
- [ ] The gate is green on the PR, with the 23 facts named from its TRX; each ran twice locally.
- [ ] Rule 6: the PR says where a failed gate run's evidence lives (the artifact name, 30 days, `<testName>\slate-windows.log`), that every edited fact now leaves its app log there, and what the 2026-11-03 artifact held.
- [ ] Rule 7: the PR says #1788's run cannot be replayed, names #1865, df67ee39 and the #1870 facts that were seen failing then passing, cites CD3, and writes `not delivered: a failing test for #1788's own run` (unless the pin was red). `Closes #1788` only with the owner's yes to CD3 and a green pin on `main`; with a red pin, or without the yes, `Refs`, with the closing words kept away from the number.
- [ ] A "Found and fixed here" section (the two polls the reading missed are sweep siblings and are listed with the ten, not here), the two deferred finds as comments on "Plan 47 deferred finds", and the last line is the count: found on the way, fixed here, deferred, filed.

## Brief

Branch `claude/cb7-gate-waits` from `main`, after CB-4 and CB-6 have merged. Check "Current state" against the tree first; every line number is from c2377210. Read every site its tables name, then `G/FocusSettleWatch.cs`, `G/ShellAccessibilityTests.FilesTree.cs:315-339`, `…FindInNote.cs:151-214`, `SAT.cs:2495-2580`, `T/Censuses/TestWaitCensus.cs`, `ShellLaunchCensus.cs` and `T/GraphDiagramTests.Lineage.cs:20-70`, `:1537-1556`. In order: (1) `G/GateFile.cs` and `G/GateFileTests.cs`, the rename fact red against a `File.OpenRead` holder first (`git add` each new file, then `python scripts/apply-license-header.py`). (2) Census rule 4 in its exact form, with its planted case; see it list 24 reads. (3) `ExpectFileText`, the ten polls, the 14 single reads; rule 4 green. (4) `WaitForSettledPixels` and the 16 pixel sites by the table: a witness, a settled frame, one measurement; never poll the assertion. (5) `HotExit.cs:131` waits for a new or changed hot-exit entry. (6) The four negatives through `FilesTree.cs:330`'s `AssertFocusStays`; trace the right-pane edge's line for `SAT.cs:11595`. (7) An arrival wait in front of `RegionStops.cs:369` and `Rescan.cs:693`. (8) Rule 1's list: 19 entries out, ten reason words in. (9) The pin and its mutant; if it is red on `main`, stop that step, post the record on #1788 and leave the product alone (CD3). (10) `PreserveEvidence` in `RunFindInNoteJourney` and around each edited fact. Leave alone: `windows.yml`, the poll-interval sleeps, every journey not named here, and all product code. `dotnet format --include` after each C# edit. Run the gate filter twice locally with NVDA closed and the desktop free. Add Design 10's two finds as comments on "Plan 47 deferred finds"; file no issue. PR body: each item under "Acceptance", the gate run id with the 23 facts, `Closes #1788` if CD3 is yes and the pin is green on `main`, else `Refs #1788`. Commit: `test(windows): the shell gate waits on conditions and reads app files without blocking their replacement`.
