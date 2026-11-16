# CB-10 — A failed vault switch returns to the welcome view once

Issues: #1730. Size S. Lanes: windows (the app job; no gate journey is added). AT: an NVDA spot check (the PR opens as a draft and is marked ready when the spoken phrases are in its body). Wave: C3, slot 3. Depends on: nothing. Parallel with: CB-7 (the shell gate's journeys and `TestWaitCensus.cs`) and CB-9a (the save chain: `WorkspaceViewModel*.cs`, `Panels/TasksReviewViewModel.cs`, a new census). Decision: none (no row of plan §3 gates it). Interlock: this chunk edits `VaultLifecycleViewModel.cs`. Plan §6's shared-file row now names CB-10 first, for `OpenVaultAsync` only, then CB-9b, CB-15a to CB-15c and CB-17b "each only where its spec names one of them", and says CB-9a's spec edits none of that row's files; see "Overlap" below. Review: a red team, then Codex adversarial-review rounds until it approves (plan §4 rule 9; the chunk changes product code).

Written 2026-11-16 from `reading/03_save_chain_lifecycle.md:132-165` (read at 1ec9bf31). Every file and line below was re-read at 80925d72 and is cited as of that commit.

`S/` below is `apps/slate-windows/src/SlateWindows/`; `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`.

**Overlap.** CB-10 edits four files: `S/VaultLifecycleViewModel.cs` (lines 614-626, 757 and the two catch arms at 770-802, all inside `OpenVaultAsync`), `T/WelcomeOpenTests.cs`, `T/VaultOpenIdentityTests.cs` and `docs/plans/40_nvda_matrix_remediation_contracts.md` (one addendum). Contract 40 is shared with CB-9a at different rows: CB-10's addendum follows R-5 and CB-9a's follows R-9, and the second PR to merge rebases and keeps both (plan §6). None of the other three is a file the reading gives to CB-9a (`WorkspaceViewModel.Rescan.cs`, `WorkspaceViewModel.History.cs`, `WorkspaceViewModel.Bases.cs`, `Panels/TasksReviewViewModel.cs`, `T/SaveChainAdmissionTests.cs`, a new `T/Censuses/SaveChainAdmissionCensus.cs`; `reading/03_save_chain_lifecycle.md:72-90`, `:468-520`) or the spec gives to CB-7 (the gate project and `T/Censuses/TestWaitCensus.cs`). CB-9a's spec was being written when this one was, so this was first checked against the reading. That spec is now written: it does not edit `VaultLifecycleViewModel.cs`, and contract 40 is the one document the two share. CB-10 adds no `VaultSession` call, so CB-9a's census has no row for it.

**Open question for the lead (it does not block the code).** A first open, not a switch, that fails still returns once, and that return moves the keys to the Open Vault… button wherever the reader had them in the welcome view. A reader who pressed a recent vault's button and whose open fails at the session open is moved off the button they pressed. No fact pins that case (the pinned one is the scan failure, where the recent buttons are rebuilt under the keys anyway). It is not #1730's defect, and WD4's answer ("a failed open returns the keys to the welcome view") was built that way by WB-9 (`docs/plans/44_windows_backlog/specs/wb09_open_handoff_and_landing_lifecycle.md:78`). Options: (a) leave it, listen to it in this chunk's NVDA pass (step 6 below) and put one line on #1955 if the move cuts the error sentence or reads as wrong; (b) make the window's handler move the keys only when they are outside the welcome view (`MainWindow.xaml.cs:511-514`), which changes WD4's built behaviour and edits a `MainWindow` file no C3 chunk is slotted for. Recommended: (a). This spec is written for (a).

## Goal

A switch from an open vault to a vault that does not open hands the keys to the welcome view once: when the switch closes the open vault. The failure then says why and moves nothing, so a reader who has moved within the welcome view while the new vault was opening stays where they are. A failure after the new vault has shown still returns to the welcome view.

## Current state on main

**The open.** `S/VaultLifecycleViewModel.cs`, `OpenVaultAsync`, `:572-825`.

- `:591-596` the switch's teardown. `AnnounceTeardown` speaks only for the Close route (`:2086-2089`), so a switch's close says nothing.
- `:613` `CloseSession()`. `:616` `bool returnedToWelcome = IsVaultOpen;`. `:617-626` the switch's own return: `IsVaultOpen = false`, the name and path cleared, `ReturnedToWelcome` raised at `:625`. This is the first return.
- `:628-633` the generation, `IsOpening`, `IsBusy`, `StatusText = "Opening {root}…"`.
- `:642` `await _openSession(root)`. The method leaves the UI thread here for the first time, so the first return's queued focus move runs before any failure is handled.
- `:655-667` the null-identity refusal (WB-28 PR 2). It already reads the flag: `if (!returnedToWelcome)` at `:661`. Its fact is `T/VaultOpenIdentityTests.cs:103-139`, `ASwitchRefusedForNoIdentityReturnsToWelcomeOnce`.
- `:697` the vault-opened line. `:696` `RecordRecentVault` (the store write; the buttons are not rebuilt yet). `:709-730` the scan.
- `:757` `IsVaultOpen = true`, the one place the new vault shows (#1684). `:758-766` `ReplaceRecentVaults`, `PublishWorkspace`, `StartSyncMarkerWatch`.
- `:770-788` `catch (VaultException)`: the sentence (`:777-779`), `CloseSession()`, `IsVaultOpen = false`, then `ReturnedToWelcome` raised **unconditionally** at `:782`, then `ReplaceRecentVaults(recorded)` when the open got as far as recording.
- `:789-802` `catch (Exception)`: the same shape, raised **unconditionally** at `:796`.

**The handler.** `S/MainWindow.xaml.cs:139` subscribes; `:511-514` is the whole handler: `_ = Dispatcher.InvokeAsync(() => OpenVaultButton.Focus());`. Each event queues one focus request for the Open Vault… button (`S/MainWindow.xaml:1081-1088`). `MainWindow` is the only subscriber.

**What "twice" is.** On a switch from vault A to a vault B whose open fails:

1. `:625` raises the first event. The welcome view shows (`IsWelcomeVisible => !IsVaultOpen`, `:363`). The queued request and the workspace's gone landing (`S/MainWindow.ShellRegions.cs:894-895`) both put the keys on the Open Vault… button: one focus change. The welcome view's buttons stay enabled through the open (`:276-280`, `AdmitOpenCommand` `:2571-2583`), so the reader can Tab to a recent vault's button or open the menu bar while B opens.
2. B's open throws. The catch arm posts the sentence and raises the second event at `:782` or `:796`. The handler queues a second request for the Open Vault… button.

If the keys are still on the Open Vault… button, the second request changes nothing: WPF raises no focus event for an element that already holds the keys (`WelcomeOpenTests.AFailedOpenLeavesTheKeysOnTheWelcomeView`, `T/WelcomeOpenTests.cs:264-284`, counts zero focus changes through that same request). If the reader has moved, the second request pulls the keys back to the Open Vault… button, and NVDA reads the button again, next to the error sentence.

**Cause.** Verified by reading. The flag at `:616` exists and one of the three failure exits reads it. The two catch arms do not.

**Which failures reach which arm.** Before `:757`: a `VaultException` from `_openSession` (core refuses the folder, WD24's identity refusal among them) or from the scan; any other exception from either. After `:757`: only a throw from `ReplaceRecentVaults`, `PublishWorkspace` (`:1664-1700`, which raises `WorkspaceReady` at `:1696`) or `StartSyncMarkerWatch` (`:1754`). The issue's "a scan failure once the workspace shows" cannot happen now: the scan ends before `:757`.

**What a reader hears today.** From the code; nobody has recorded it by ear. No pass report or issue comment holds a hearing of a failed switch.

- Keys not moved: the welcome landing once (the Open Vault… button, with its help text "Choose a folder to open as a Slate vault."), then "Could not open vault: …" or "Unexpected vault error: …" at High priority. The second event is silent.
- Keys moved within the welcome view, or into the menu bar: the same, and then the keys jump to the Open Vault… button and NVDA reads it a second time. Whether that readout cuts the error sentence short is not known.

**The routes into a switch.** All end in `OpenVaultAsync`: Ctrl+Shift+O (`S/MainWindow.xaml:57`), File ▸ Open Vault… (`:272-277`), both through `PickAndOpenVaultAsync` (`:945-965`); a second launch or a jump-list open (`S/MainWindow.xaml.cs:196-241`). The recent vaults' buttons are on the welcome view only, so `OpenRecentAsync` (`:967-1016`) never starts a switch.

## Design

1. **Both catch arms read the flag.** In `catch (VaultException)` and in `catch (Exception)`, the raise becomes what `:661-664` already is:

   ```csharp
   IsVaultOpen = false;
   if (!returnedToWelcome)
   {
       ReturnedToWelcome?.Invoke(this, EventArgs.Empty);
   }
   ```

   Nothing else in either arm moves: the sentence, `CloseSession()` and `ReplaceRecentVaults(recorded)` stay in their order.

2. **The flag is cleared where the new vault shows.** Right after `IsVaultOpen = true;` at `:757`:

   ```csharp
   // The new vault shows: a failure from here on returns to the welcome view again (#1730).
   returnedToWelcome = false;
   ```

   So a throw from the three calls after it still raises the event. That is a second event in a switch, and it is right: the workspace took the keys in between.

3. **The flag's comment** at `:614-615` is widened to say what it now means: the keys are with the welcome view for this attempt, because the switch's close handed them over and no vault has shown since, so no failure exit hands them over again.

That is the whole product change: two guards and one assignment, in one method of one file. No local function is needed; three guarded raises in one method read plainly.

**What a reader hears after.** The welcome landing once, then the error sentence. The keys stay where the reader has them: on the Open Vault… button if they did not move, on the recent vault's button or in the menu if they did.

**One case moves the keys anyway, as it does on main.** When a switch's open fails in the scan, the open had recorded B, and the catch arm publishes the recents (`ReplaceRecentVaults(recorded)`, `:783-786`, `:797-800`). That rebuilds every recent vault's button (`:1268-1278`). If the keys were on one, the welcome view's guard lands them on the view's first stop, the Open Vault… button (`S/MainWindow.ShellRegions.cs:922`; `docs/plans/18_windows_port/focus_integrity_manifest.json:81`, `guard: WelcomeRoot`). On main the event's request and the guard both aim at that button. After this chunk the guard does it alone. A fact pins that (Tests, fact 3). If that fact is red with the fix in, stop and tell the lead: the guard does not do alone what the manifest says, and the answer is not to put the raise back without saying so.

**No sentence is added, changed or moved.** `ReportTerminalStatus` (`:2591-2596`) is the one construction site and it is not touched, so contract 38's ledger and `a11y_trigger_designations.json` do not move. No chord, command or help line changes, so the parity matrix does not move.

**The other raise sites, and why they are unaffected.**

| Site | Why it stays |
|---|---|
| `:625`, the switch's close | The first return. Unchanged. |
| `:661-664`, the null-identity refusal | Already guarded. It returns before `:757`, so the new assignment cannot reach it. |
| `:864`, `CloseVault` | One event per close; no open follows it in the same call. |
| `:585-589`, a path `Path.GetFullPath` refuses | Returns before the teardown. Nothing closed, no event, as today. |
| `:592-595`, a refused teardown | Returns with vault A still open. No event, as today. |
| `:643-647`, an open overtaken by a newer generation | Returns with no event, as today; the newer call owns the view. |
| `OpenRecentAsync`'s two refusals (`:991`, `:1005`) | Run from the welcome view before `OpenVaultAsync`. No event, as today. |

A first open (no vault was open, so the flag starts false) that fails raises the event once, as today. That keeps `WelcomeOpenTests.AFailedOpenLeavesTheKeysOnTheWelcomeView` and `AFailedScanAfterARecentOpenLandsOnTheOpenButtonNeverTheWindow` as they are.

**Siblings (plan §4 rule 3).** The defect's shape is "a failure exit of `OpenVaultAsync` that hands the keys over without asking whether this attempt already has". There are three failure exits that raise; one was fixed by WB-28 PR 2, and this chunk fixes the other two. No other method raises `ReturnedToWelcome` more than once per call, and no other event of the lifecycle queues a focus move from a catch arm (`WorkspaceReady` is raised once, at `:1696`). The Mac has its own lifecycle and does not share this code. The case in the open question at the top is a different shape (one return, on a first open) and is not taken here.

**Rejected.** Making the window's handler skip the move when the keys are already in the welcome view. It would fix the same hearing from the view side, but it leaves the lifecycle raising a second event for any later subscriber, changes what a first failed open does (the open question), and edits a `MainWindow` file.

## Tests

Failing facts first. No fact adds a sleep, a `Task.Delay`, a timed pump or a gate of its own: the hosted facts wait through `WelcomeShell`'s condition waits (`WaitForTheOpenSessionPark`, `WaitForTheScanPark`, `WaitForTheOpenToEnd`, each a `PumpUntil` on a condition, `T/WelcomeShell.cs:233-254`), and the headless facts await the open's own task. `WelcomeShell` is not edited; its `Dispose` already releases its parks and pumps until the open has ended before the window closes (`:278-296`). At 80925d72 `TestWaitCensus` held rule 3 only. CB-6's rules 1 and 2 landed in #1971 (ecf7f551, 2026-11-16), and these facts give them nothing to pin.

**1. `T/WelcomeOpenTests.cs`, new fact `AFailedSwitchLeavesTheKeysWhereTheReaderMovedThem`** (the shipped window; placed after `ASwitchFromAnOpenVaultHoldsTheWelcomeAndNeverTheWindow`).

- `using var shell = new WelcomeShell();` `ReleaseParks()`, `OpenVaultCommand.Execute(null)`, `WaitForTheOpenToEnd()`; premise: `IsVaultOpen`.
- `ReArmParks()`; `PickAnswer = shell.StateFolder("not-a-folder", create: false)` (the real `OpenFilesystem` throws a `VaultException` on it, as in `AFailedOpenLeavesTheKeysOnTheWelcomeView`).
- `OpenVaultCommand.Execute(null)`; `WaitForTheOpenSessionPark()`; `Settle()`. Premise: the welcome view shows and `Keyboard.FocusedElement` is the Open Vault… button (the first return landed).
- The reader moves: `Button recent = shell.RecentButton(shell.VaultRoot);` (the first open published it), `Assert.True(recent.Focus(), …)`, `PumpedDispatcher.Drain()`, premise: it holds the keys. Then `List<IInputElement> changes = shell.RecordFocusChanges();`.
- `ReleaseOpenSession()`; `WaitForTheOpenToEnd()`.
- Asserts: `changes` is empty (the message lists them); `Keyboard.FocusedElement` is `recent`; `StatusText` starts with "Could not open vault"; `IsVaultOpen` and `IsOpening` are false; `shell.Posted` holds exactly one line that starts with "Could not open vault".
- **Red today:** `changes` is `[OpenVaultButton]`. The second event's request moved the keys.
- **Mutation A:** take the guard out of the `VaultException` arm. Red, as today.

**2. `T/VaultOpenIdentityTests.cs`, new theory `ASwitchWhoseOpenFailsReturnsToWelcomeOnce`**, beside `ASwitchRefusedForNoIdentityReturnsToWelcomeOnce`, headless, three arms (`[InlineData]` over a short arm name). The class summary gains one sentence for #1730. The lifecycle is built with a recents store in a temp file, `announce: announced.Add`, and two seams that pass through until a flag is set:

- `openSession: root => fault is not null && armed == "open" ? Task.FromException<VaultSession>(fault) : Task.Run(() => VaultSession.OpenFilesystem(root))` (the constructor's seam, `:221-226`).
- `sessionLoadWorker: work => Task.Run(() => armed == "scan" ? throw fault : work())` (`:214`, `:267`; the shape `WelcomeShell` uses).

Each arm opens `first` for real, asserts `IsVaultOpen`, starts counting `ReturnedToWelcome`, clears `announced`, arms the fault and awaits `OpenVaultAsync(second.Root)`.

| Arm | Fault | Where | Status sentence | Recents |
|---|---|---|---|---|
| a vault error at the open | `new VaultException.Io("injected open failure")` | `openSession` | `"Could not open vault: " + SlateUniffiMethods.VaultErrorDetail(fault)` | the store does not hold `second` |
| an unexpected error at the open | `new InvalidOperationException("injected open failure")` | `openSession` | `"Unexpected vault error: injected open failure"` | the store does not hold `second` |
| a vault error in the scan | `new VaultException.Io("injected scan failure")` | `sessionLoadWorker` | `"Could not open vault: " + SlateUniffiMethods.VaultErrorDetail(fault)` | `lifecycle.RecentVaults[0].Path` is `second.Root` (recorded, and published on failure) |

Every arm asserts: the count is 1; `IsVaultOpen` is false; `Workspace` is null; `StatusText` is the sentence; `announced` holds exactly one `A11yEvent.HostComposed`, with that `Text` and `Priority: A11yPriority.High`. The two arms that fail at the open also assert no `A11yEvent.VaultOpened`; the scan arm's open got as far as that line (`:697`), so it asserts exactly one.

- **Red today:** every arm counts 2.
- **Mutation A** (above) turns the first and third arms red. **Mutation B:** take the guard out of the `catch (Exception)` arm; the second arm counts 2.

**3. `T/WelcomeOpenTests.cs`, new fact `AFailedScanAfterASwitchLandsOnTheOpenButtonNeverTheWindow`** (a guard for Design's "one case moves the keys anyway"; it is expected green on main and must stay green).

- As fact 1 up to the second open, but `using var other = FixtureVault.Create(1, "welcome-switch-scan");`, `PickAnswer = other.Root`, `shell.ScanFault = new VaultException.Io("injected scan failure")`.
- At the open-session park the reader moves to `shell.RecentButton(shell.VaultRoot)`; record the focus changes; `ReleaseOpenSession()`, `WaitForTheScanPark()`, `ReleaseScan()`, `WaitForTheOpenToEnd()`.
- Asserts: `changes` is exactly `[OpenVaultButton]`; no entry is the window; `Keyboard.FocusedElement` is the Open Vault… button; `RecentVaults[0].Path` is `other.Root`.
- Run it on main first and quote the result. If it is not green on main, the recorded order differs from this spec's reading: write the fact to what main does, say so in the PR body, and keep "never the window, one move" as the assertion that must hold after the fix.

**4. `T/VaultOpenIdentityTests.cs`, new fact `ASwitchThatFailsAfterTheNewVaultShowedReturnsToWelcomeAgain`** (a guard for Design 2; green on main).

- Headless, default seams. Open `first`; start counting; then add a `WorkspaceReady` handler that throws `new InvalidOperationException("injected publish failure")` once (it removes itself or checks a flag first, so the dispose's path cannot throw); await `OpenVaultAsync(second.Root)`.
- Asserts: the count is 2 (the switch's close, then the failure after the vault showed); `IsVaultOpen` is false; `Workspace` is null; `StatusText` is "Unexpected vault error: injected publish failure".
- **Mutation C:** leave out `returnedToWelcome = false`. The count is 1.
- Not verified by a run: that `CloseSession` takes a half-published workspace down cleanly when `WorkspaceReady` throws (the comment at `:763-765` says the assembly is left for the catch arm). If it does not, throw from a one-shot `RecentVaultsChanged` handler instead: `ReplaceRecentVaults(recorded)` at `:760` raises it after `:757` and before anything is published. Say which seam the fact uses in the PR body.

**Existing facts that must stay green, unedited:** all of `WelcomeOpenTests` (the three named above and `ASwitchFromAnOpenVaultHoldsTheWelcomeAndNeverTheWindow`, which counts two focus changes on a switch that succeeds), all of `VaultOpenIdentityTests`, `MainWindowHandoffTests`, `RightPaneHideLandingTests`, and the censuses `FocusIntegrityManifestCensus`, `RegionGuardCensus`, `A11yTriggerParityCensus`, `AnnouncementSeamCensus` and `TestWaitCensus`.

## The NVDA spot check

Run by plan 44 §4 rule 4: NVDA, the app started through `scripts/windows/launch-for-field-pass.ps1`, keys sent with `SendInput` and the speech read from `nvda.log` as `docs/plans/18_windows_port/reports/nvda_agent_matrix_pass_2026-09-22.md` describes. The foreground is the agent's to take. Check that no home-pool CI job is running on this machine before the Release build.

The field cannot hold an open, so it cannot put the reader's keys elsewhere in the welcome view between the close and the failure: core refuses the folder within the same moment. The moved-keys case is fact 1's. The pass confirms what a reader hears on a failed switch, that the landing is read once, and that the error sentence is heard whole. Run it on the PR's head, and run steps 1 to 5 on a build of `main` as the base.

1. Make two folders and the refusal. The seam matches a substring of the vault's path, so B's name must not occur in A's path.

   ```powershell
   $a = Join-Path $env:TEMP 'cb10-vault-a'; $b = Join-Path $env:TEMP 'cb10-refused-b'
   New-Item -ItemType Directory -Force $a, $b | Out-Null
   Set-Content (Join-Path $a 'one.md') '# One'; Set-Content (Join-Path $b 'two.md') '# Two'
   $env:SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE = 'cb10-refused-b'
   pwsh -NoProfile -File scripts/windows/launch-for-field-pass.ps1 -Vault "$a|$b" -Seed
   ```

2. Wait for vault A's launch lines to end. Press Ctrl+Shift+O. In the folder picker, type B's full path into the Folder field and choose Select Folder. If the picker cannot be driven, start the switch with a second launch instead: run the Release `SlateWindows.exe` itself with B's path as its one argument (`& $exe $b`, where `$exe` is the path the launch script used). Slate runs one instance, so the second process hands its vault to the running window and exits, and the request ends in the same `OpenVaultAsync` (`S/MainWindow.xaml.cs:196-241`). The launch script cannot do this: it stops when any `SlateWindows` process is running (`scripts/windows/launch-for-field-pass.ps1:77-84`). That the hand-off works this way in a field pass was not tried when this spec was written. Say in the PR body which route was used.
3. Listen for, and copy from `nvda.log` in order:
   - the welcome landing: the Open Vault… button, read **once** (expected to include "Open Vault…", "button" and the help text "Choose a folder to open as a Slate vault."; record NVDA's exact words);
   - "Could not open vault: This folder's file system gives Slate no file identity for it, so Slate cannot keep the vault anchored to it." (core's sentence, `crates/slate-core/src/a11y.rs:3547`), heard whole.
4. Press NVDA+Tab. It reads the Open Vault… button: the keys are on the welcome view, not on the window.
5. Press Tab, then Shift+Tab. The keys move to the first recent vault's button and back; nothing is disabled or missing. Nothing else speaks.
6. For the open question at the top (head only): Tab to B's recent button and press Enter. Record what is spoken, where NVDA+Tab then says the keys are, and whether the error sentence was heard whole. This step records main's behaviour for a first open; it does not gate the PR.

Expected on head and on the base alike for steps 3 to 5: the button once, the sentence whole. A second readout of the button, or a clipped sentence, on head is a defect in this chunk. The same on the base and not on head is the issue heard, and the PR body says so. The PR body records both runs' phrases, and `not delivered: an NVDA hearing of the moved-keys case` with the reason above.

## Docs and registers

- `docs/plans/40_nvda_matrix_remediation_contracts.md`, R-5 (h), line 15: append one addendum after the last one there (the 2026-10-27 addendum for #1684). Text: "*Addendum 2026-11-16 (plan 47 CB-10, #1730):* a switch whose new vault does not open hands the keys to the welcome view once, at its close; the failure says why and moves nothing, so keys the reader moved within the welcome view stay where they are. A failure after the new vault has shown returns to the welcome view again. Facts: `WelcomeOpenTests.AFailedSwitchLeavesTheKeysWhereTheReaderMovedThem`, `VaultOpenIdentityTests.ASwitchWhoseOpenFailsReturnsToWelcomeOnce`." Use the PR's own date if it differs. The record is append-only: nothing earlier on the line is edited.
- `docs/plans/18_windows_port/focus_integrity_manifest.json`: no change. No focus site is added, removed or renamed; the one request site (`MainWindow.xaml.cs:513`) is untouched, and `ReplaceRecentVaults`' row (`:81`) still names its guard. `FocusIntegrityManifestCensus` must pass unedited.
- `docs/plans/38_notification_dispatcher_contracts.md` and `a11y_trigger_designations.json`: no change (no `A11yEvent` construction is added or moved). `python -I scripts/a11y_trigger_ledger.py --check` must exit 0.
- `docs/plans/18_windows_port/parity_matrix.md`: no change.
- `docs/plans/18_windows_port/w_c_matrix.md`: no change (its rows describe the welcome view's controls, not a failed switch).
- Plan 47 §10's spec table and §12's delivery row are the lead's, in the wave's docs PR. This PR does not edit the plan.
- The reading is not edited. Its three stale points are in "Corrections to the reading" below.

**Corrections to the reading** (`reading/03_save_chain_lifecycle.md:132-165`). Its line numbers for `VaultLifecycleViewModel.cs` and `MainWindow.xaml.cs` still hold at 80925d72. Three statements do not:

- "one focus move, and so one 'Open vault, button' readout, is removed" (`:160-161`). Only when the reader has moved. With the keys still on the button the second request is silent on main, so there the fix removes nothing a reader hears.
- The fact's `VaultException` arm "through the `SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE` seam" (`:153-155`). This spec throws from the `openSession` seam instead: the environment variable is process-wide and matches by substring, and the seam needs neither. The field pass still uses the variable.
- "the status sentence" as one sentence (`:157`). There are two: "Could not open vault: …" for a `VaultException` and "Unexpected vault error: …" for anything else.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-cb10-failed-switch'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
$t = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
# the new facts, red on the first commit (tests only), green on the second
dotnet test $t -c Release --no-build --filter "FullyQualifiedName~AFailedSwitchLeavesTheKeysWhereTheReaderMovedThem|FullyQualifiedName~ASwitchWhoseOpenFailsReturnsToWelcomeOnce|FullyQualifiedName~AFailedScanAfterASwitchLandsOnTheOpenButtonNeverTheWindow|FullyQualifiedName~ASwitchThatFailsAfterTheNewVaultShowedReturnsToWelcomeAgain"
# the classes and censuses around them
dotnet test $t -c Release --no-build --filter "FullyQualifiedName~WelcomeOpenTests|FullyQualifiedName~VaultOpenIdentityTests|FullyQualifiedName~MainWindowHandoffTests|FullyQualifiedName~RightPaneHideLandingTests"
dotnet test $t -c Release --no-build --filter "FullyQualifiedName~FocusIntegrityManifestCensus|FullyQualifiedName~RegionGuardCensus|FullyQualifiedName~A11yTriggerParityCensus|FullyQualifiedName~AnnouncementSeamCensus|FullyQualifiedName~TestWaitCensus"
python -I scripts/a11y_trigger_ledger.py --check
$cs = git diff --name-only origin/main -- '*.cs'
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs
```

Rebuild between a mutation and its run, and check the rebuilt `SlateWindows.dll`'s timestamp: a restored source with an old timestamp is not recompiled. The three mutations (A, B, C) are applied one at a time to the fixed tree and each is quoted with the fact it turns red. Then the whole unit project once with the desktop free. CI: `windows.yml` on the PR, in full (the change is under `apps/slate-windows/`). The PR body quotes each command with its result.

## Acceptance

- [ ] `AFailedSwitchLeavesTheKeysWhereTheReaderMovedThem` and all three arms of `ASwitchWhoseOpenFailsReturnsToWelcomeOnce` were seen red on `main`'s product code (the focus record `[OpenVaultButton]`; a count of 2), then green with the fix (plan §4 rule 7).
- [ ] `AFailedScanAfterASwitchLandsOnTheOpenButtonNeverTheWindow` and `ASwitchThatFailsAfterTheNewVaultShowedReturnsToWelcomeAgain` are green before and after, and the PR body says which seam the second one throws from.
- [ ] Mutations A, B and C each turn the named fact red; the results are in the PR body.
- [ ] The product diff is `VaultLifecycleViewModel.cs` only: two guards, one assignment, one comment. No sentence, announcement site, chord or focus site changed; the ledger check exits 0 and the manifest is unedited.
- [ ] Contract 40 R-5 (h) carries the addendum.
- [ ] No new fact holds a fixed sleep, delay or timed pump; `TestWaitCensus` passes; `WelcomeShell` is unedited.
- [ ] The PR opened as a draft. The NVDA spot check ran on the base and on the head through `launch-for-field-pass.ps1`; its phrases, the route used in step 2 and step 6's observation are in the PR body; the PR was marked ready only then. `not delivered: an NVDA hearing of the moved-keys case (the field cannot hold an open; the hosted fact shows the keys stay)`.
- [ ] Plan §4 rule 9: a red team (a fresh Claude agent with `24_red_team_protocol.md`, the diff and this spec), then Codex adversarial-review rounds until it approves; the PR body records both.
- [ ] The PR body has "Found and fixed here" (or "none") and ends with the count: found on the way, fixed here, deferred, filed (rule 8).
- [ ] `Closes #1730`, and `gh pr view N --json closingIssuesReferences` shows #1730 and nothing else. No close, fix or resolve word stands before #1319, #1684, #1955 or any other number in the body.
- [ ] `windows.yml` is green on the head the merge is pinned to; Codoki reports Safe to merge at 3/5 or higher, or its bypass is recorded.

## Brief

Branch `claude/cb10-failed-switch-returns-once` from `main`, in a fresh worktree with `CARGO_TARGET_DIR = C:\dev\slate\target-cb10-failed-switch`. Read `S/VaultLifecycleViewModel.cs:572-865` (the open, and `CloseVault`), `:1268-1278`, `:1664-1700`, `:2571-2596`; `S/MainWindow.xaml.cs:139`, `:196-241`, `:511-514`; `S/MainWindow.xaml:1024-1088`; `S/MainWindow.ShellRegions.cs:886-925`; `T/WelcomeShell.cs`; `T/WelcomeOpenTests.cs:230-362`; `T/VaultOpenIdentityTests.cs`; and check this spec's "Current state" against them before coding. First commit, tests only: in `T/WelcomeOpenTests.cs` the facts `AFailedSwitchLeavesTheKeysWhereTheReaderMovedThem` (open a vault; switch to a folder that is not there; at the open-session park move the keys to the first vault's recent button; release; zero focus changes and the keys still on that button; red today with `[OpenVaultButton]`) and `AFailedScanAfterASwitchLandsOnTheOpenButtonNeverTheWindow` (the same switch to a real vault with `ScanFault` set; exactly `[OpenVaultButton]`; green today), and in `T/VaultOpenIdentityTests.cs` the theory `ASwitchWhoseOpenFailsReturnsToWelcomeOnce` (three arms through the `openSession` and `sessionLoadWorker` seams: a `VaultException` at the open, an `InvalidOperationException` at the open, a `VaultException` in the scan; one `ReturnedToWelcome`, the status sentence, one High `HostComposed`; each arm counts 2 today) and the fact `ASwitchThatFailsAfterTheNewVaultShowedReturnsToWelcomeAgain` (a one-shot throwing `WorkspaceReady` handler on the second open; a count of 2; green today). Quote the red and green results. Second commit, the fix, in `OpenVaultAsync` only: wrap the raise in each catch arm (`:782`, `:796`) in `if (!returnedToWelcome)`, as `:661-664` already does; add `returnedToWelcome = false;` right after `IsVaultOpen = true;` at `:757`; widen the flag's comment at `:614-615`. Change no sentence, no handler and no test fixture. If `AFailedScanAfterASwitchLandsOnTheOpenButtonNeverTheWindow` turns red with the fix in, stop and tell the lead. Then apply mutations A (no guard in the `VaultException` arm), B (no guard in the `Exception` arm) and C (no reset at `:757`) one at a time, rebuilding and checking the DLL's timestamp each time, and quote the fact each turns red. Append the R-5 (h) addendum to `docs/plans/40_nvda_matrix_remediation_contracts.md` line 15. Check each C# file's CR count before and after editing it, and run `dotnet format --verify-no-changes --include` on the changed files. Run the Verification block. Open the PR as a draft; run the NVDA spot check (six steps, base and head, through `scripts/windows/launch-for-field-pass.ps1` with `SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE` set to the second folder's name) and put the phrases in the body; then mark it ready. Review by plan §4 rule 9: a red team, then Codex rounds until it approves. PR body: `Closes #1730` (and no closing word before any other issue number), the four facts with their red and green results, the three mutations, the addendum, the NVDA phrases and the route used, step 6's observation for the lead's open question, `not delivered: an NVDA hearing of the moved-keys case`, both reviews, "Found and fixed here", and the count. Commit and PR title: `fix(windows): a failed vault switch returns to the welcome view once and leaves the keys where the reader has them`.
