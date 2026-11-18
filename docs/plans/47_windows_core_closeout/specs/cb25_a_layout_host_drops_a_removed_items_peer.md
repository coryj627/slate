# CB-25: A layout host drops a removed item's raw-view peer

Issue: #1860. Size S. Lanes: windows (the app job). AT: none (raw view only; the control view does not change, measured). Wave: C6, slot 3. Depends on: nothing. Parallel with: CB-14 (`a11y.rs`, `crates/slate-uniffi/src/lib.rs`, the toggle and save routes, `WorkspaceViewModel*.cs`) and CB-30 (`crates/slate-core/src/math.rs`, the math golden, `ReadingMathTests.cs`). Decision: none (no row of plan §3 gates it). Interlock: none; plan §6 names neither file for another chunk, and no gate file is edited (Call 3). Review: a red team, then Codex adversarial-review rounds until it approves (plan §4 rule 9; the chunk changes product code).

Written 2026-11-18 from `reading/04_focus_and_peers.md:355-384` and `:654-655` (read at 1ec9bf31), the issue, PR #1861 (08ac3086) and a step 0 run. Every file and line below was re-read at eeb99032, `main` when this was written; nothing has merged since. Step 0 ran on eeb99032 in the chunk's worktree with its own `CARGO_TARGET_DIR`, a Release build (0 errors), and probe facts in an untracked file that was deleted after the runs; the raw logs, the probe sources, the prototype and the WPF 10 sources read are kept with this chunk's working notes (`notes-2.md`, the logs, the probe sources, `prototype-snippet.cs` and the WPF sources). The spec's red team (below) was folded in. Statements that are still by reading say so.

`S/` below is `apps/slate-windows/src/SlateWindows/`; `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`; `G/` is `apps/slate-windows/tests/SlateWindows.AccessibilityTests/`; `wpfsrc\` is WPF 10's sources as step 0 read them (dotnet/wpf, branch release/10.0).

**Files this chunk edits.** Product: `S/AutomationLandmark.cs` (`LayoutItemsControl` only, `:272-312`). Tests: `T/LayoutItemsControlTests.cs`. No XAML file, no gate file, no `crates/` file, no binding.

**Overlap.** CB-14 and CB-30 edit none of these files.

**No sentence is added or changed.** No `A11yEvent` is constructed or moved (contract 38's ledger does not move), no chord or help line (the parity matrix does not move), no focus call (the focus manifest does not move). No register row names #1860 or the raw view of a layout host: contract 40's R-4 (`docs/plans/40_nvda_matrix_remediation_contracts.md:711`) is a delivery record, not a register row this changes.

## Goal

After an item leaves a `LayoutItemsControl`'s source, or is replaced or moved there, the host's peer lists exactly the current items' peers, in order, once the reset queued for the change has run (at `Loaded` priority, after any layout pending then), whether or not a UI Automation client has connected the window. A client that connects after the change sees no extra element where the item was. A retained item keeps its peer.

## Current state on main (eeb99032)

**The host and its one reset.** `LayoutItemsControl` (`S/AutomationLandmark.cs:272-312`) resets its peer's children in one place: its constructor subscribes to `ItemContainerGenerator.StatusChanged` and, on `ContainersGenerated`, queues `UIElementAutomationPeer.FromElement(this)?.ResetChildrenCache()` at `DispatcherPriority.Loaded` (`:293-302`, #1677 and #1678), whether or not a peer exists. It has no `OnItemsChanged` override. Its peer, `LayoutItemsControlAutomationPeer` (`:314-358`), asks each item peer to read its children again when its container changed (`GetChildrenCore`, `:345-357`, through `LayoutItemAutomationPeer.ReadAgainIfRewrapped`, `:387-405`, queued at `Send`, one at a time per item peer). The item peer remembers the container it read from only when that container was laid out (`:414-420`).

**What WPF does on a change** (`wpfsrc\`, by reading, and step 0's M2 by measurement). `ItemsControl` calls `OnItemsChanged` after the generator's change handler (`ItemsControl.cs:290-307`). By then the generator has told the panel, which removes or moves the container at once (`Panel.cs:668-707`, `:732-786`), or, for a Replace, the generator has relinked the old container to the new item and raised nothing (`ItemContainerGenerator.cs:2585-2626`). A Reset makes the panel regenerate, which raises `ContainersGenerated` (`Panel.cs:684-707`, `:788-792`; `ItemContainerGenerator.cs:1581-1589`), collapsed or not; a Remove, a Replace and a Move raise none. The host peer's children are the panel's children mapped to their items (`ItemsControlAutomationPeer.cs:127-158`; `IsVirtualized` is true, `:479-482`), cached until reset; a host that was never laid out has no panel and its peer lists no item peers (`:127-134`).

**#1860 reproduces, measured (step 0, M1).** Five rows A to E in a `LayoutItemsControl` over an `ObservableCollection` of records, each templated as a Button, in a borderless window shown with `ShowActivated = false` and in a host in no window (`BeginInit`, `EndInit`, then measure and arrange); the host's peer made with `CreatePeerForElement` and read once, as a client walk would; `ContextLayoutManager`'s automation list held 0 roots (no client had connected) in every run. After `rows.Remove(C)` the removed item's peer stays in the host peer's children: after 20 drains, after 50 more and an `ApplicationIdle` frame, after a public `GetChildren()`, and after `InvalidateMeasure` and `UpdateLayout` on the host. It leaves only at the next `ContainersGenerated` (an `Add`). The stale peer has no children (its own queued re-read cleared them) and an empty rectangle.

**Per operation, measured (M2).** Shown and not shown gave the same answers.

| Operation | `ContainersGenerated` | Main: stale after 20 drains | With the prototype |
|---|---|---|---|
| Add | yes (1) | no | no |
| Remove | none | **1 stale peer** | heals in 1 drain; 4 of 4 retained peers the same object |
| Replace | none (the old container is relinked) | **the old item's peer listed, the new one missing** | heals in 1 drain; 4 of 4 |
| Move | none | **the old order, for good** | heals in 1 drain only when Move is in the set; 5 of 5 |
| Clear (Reset) | yes, even to 0 rows | no | no |
| Clear then refill with equal records | yes (6 cycles) | no | no; WPF reuses the peer of an equal item, 5 of 5 |
| `ItemsSource` swapped | yes (1) | no | no; 2 of 2 by the same reuse |
| 100 removals in a burst | none | **100 stale peers** | heals in 1 drain |

**A client's view, measured (M3).** The client is `System.Windows.Automation` on a thread-pool thread in the test process, not out of process.

- The issue's schedule (the peer read in process, the removal, 20 drains, then the client connects): the raw view shows A, B, an empty `SlateLayoutItem` with an empty rectangle and no children, D, E; the control view is right (Buttons A, B, D, E; `FindAll` Button gives 4). The issue's "its control view is correct" is now measured.
- A client that connected before the removal, with or without a focus or structure handler: WPF drops the stale peer by itself at the next layout (1 drain). What decides it is not a listener but whether a client has connected the window's root: the root peer then sits in the layout manager's automation list, every measure marks the peer ancestors invalid, and after layout WPF's `UpdateSubtree` rebuilds those peers' children (`wpfsrc\UIElement.cs:556-558`, `:769-771`, `AutomationPeer.cs:2022-2113`, `ContextLayoutManager.cs:670-716`). With a structure handler the client got 2 `ChildRemoved` events, the same on main and with the prototype.

**Why neither the gate nor a screen reader shows it.** A FlaUI journey or a screen reader connects the window before anything changes, so in the launched app the stale peer is dropped at the next layout on `main` too. The product reads no layout host's children in process (its only `GetChildren()` calls are `S/Canvas/CanvasRendererView.cs:260-262` and `S/Reading/ReadingSurface.cs:1227`). Only an in-process read before any client connects shows #1860, which is why the facts below are hosted facts and why no other items host is swept here.

**The eleven sites** (the reading named eight; step 0 added the group members, the template prompts and the property rows, and mapped each user action to the collection call by reading):

| # | Site | Source and how it changes | Removes, replaces or moves an item? |
|---|---|---|---|
| 1 | `S/MainWindow.xaml:1095` recent vaults | `ObservableCollection<RecentVault>`, `ReplaceRecentVaults` clears and refills (`S/VaultLifecycleViewModel.cs:1293-1303`); `RemoveRecentVault` goes through it | no: Reset and Adds, reset today |
| 2 | `S/MainWindow.xaml:1860` Embeds | `ObservableCollection<EmbedRowViewModel>`, cleared on a note load and refilled | no |
| 3 | `S/MainWindow.xaml:2305` bibliography notices | a new `SiblingText[]` per update | no: an `ItemsSource` swap |
| 4 | `S/MainWindow.xaml:3259` recent searches | an `IReadOnlyList<string>` replaced | no: an `ItemsSource` swap |
| 5 | `S/MainWindow.xaml:4295` builder conditions | `ObservableCollection<BuilderConditionRow>`; the row's Remove button calls `Remove` (`S/MainWindow.Bases.cs:523-531`, `S/Bases/BaseQueryBuilderViewModel.cs:384`) | **Remove: stale on main** |
| 6 | `S/MainWindow.xaml:4341` group members | set once, one Add | no |
| 7 | `S/MainWindow.xaml:4640` template prompts | set once | no |
| 8 | `S/WorkspaceTemplates.xaml:534` property list editor | `RemoveItem` goes through `RebuildItems`, which clears and refills | no |
| 9 | `S/WorkspaceTemplates.xaml:795` property rows | cleared and refilled | no |
| 10 | `S/WorkspaceTemplates.xaml:1362` split host | `ObservableCollection<WorkspacePaneNodeViewModel>`: closing a pane's last tab or Close Pane removes its node (`S/WorkspaceViewModel.Layout.cs:566`, from `:551` and `:709`) and, for a two-pane split, replaces the split with the survivor (`:578`); Split across the other axis replaces the pane with a split (`:477`); Split along the same axis inserts (`:461`); the restore's prune clears and refills (`S/WorkspaceViewModel.Persistence.cs:122-131`) | **Remove and Replace: stale on main** |
| 11 | `S/Bases/BaseSurfaceView.cs:181` warning banners | a new array per render | no: an `ItemsSource` swap |

No site calls `Move` today.

**The #1861 trap, measured (M5).** #1861's first push ended the app in the gate after Split Right with "Recursive call to Automation Peer API is not valid", and the fix queued the item peer's reset. Step 0 tried to provoke a recursive call from the host's reset: a removal of a sibling, and of the read item itself, from inside an item peer's read and from inside WPF's layout-time `UpdateSubtree` walk with a structure handler, with and without a nested dispatcher frame, on main, with the queued prototype and with an inline-reset mutant. No run threw, and the queued prototype's state was current at the first drain (step 0's `probe-proto3-m5b.log`, in the working notes). By reading WPF 10, `AutomationPeer.ResetChildrenCache` is not guarded against re-entry (`AutomationPeer.cs:1446-1451`), and the host's `GetChildrenCore` makes no guarded call; #1861's crash was an item peer's reset re-entering its container peer's guarded `GetChildren()` (`ItemAutomationPeer.cs:182-194`). The exact #1861 nesting was not rebuilt in process.

**Facts that touch these hosts** (`LayoutItemsControlTests` 5, `CodeBuiltSiblingTests` 9, `TextScaleTests.ARecentSearchKeepsItsWholeQuery` and `TextScaleTests.TheWelcomeViewReachesItsLastRecentVault`, `WelcomeTabRingTests` 1, `ItemContainerNameCensus` 6): 23 of 23 passed on main and 23 of 23 with the prototype, under the desktop lock.

**Gate journeys.** No journey closes a split pane, removes a builder condition, removes a property list item, removes a recent vault or clears recent searches. `FluentShell_UiaPatternsKeyboardFocusAndAxe_AreClean` changes the split host after UI Automation reads: Split Right (`G/ShellAccessibilityTests.cs:744-774`, a new host with two Adds), Split Down (`:897-917`, a Replace at `S/WorkspaceViewModel.Layout.cs:477`, which reaches the new path) and Close Vault (`:922-929`, the recent vaults cleared and refilled). `PropertiesHeader_RowsEditSheetsAndRenameCarryTheMacShapes` reloads the property rows (`:3276-3317`). Both journeys passed on main and with the prototype, under the lock (FluentShell 1 min 8 s, PropertiesHeader 6 s). Whether the prototype's reset ran inside the app was not instrumented.

**Cost, measured.** 100 removals that leave five rows: 3.09 to 3.80 ms on main (never current), 3.32 to 3.64 ms with a one-pending flag (1 reset queued and run), 4.44 ms with no flag (100 queued and run). The existing `ContainersGenerated` path is not coalesced: a Clear and 5 Adds raise 6 cycles on main (M2), so by reading of `:296-300` it posts 6 operations.

**Cause.** A removal, replace or move raises no `ContainersGenerated`, so the host's one reset never runs, and nothing else invalidates the host peer's children until a client has connected the window.

## Design

1. **One queued reset for the host, shared by both triggers.** `LayoutItemsControl` gets a private `bool _peerResetQueued` and a private `QueuePeerReset()`: when the host has a peer (`UIElementAutomationPeer.FromElement(this)`, which returns a peer only if one was made) and no reset is queued, it sets the flag and queues, at `DispatcherPriority.Loaded`, an operation that clears the flag first and then calls `UIElementAutomationPeer.FromElement(this)?.ResetChildrenCache()`. The flag clears before the reset so that a change arriving while the reset runs queues a reset of its own; a clear after the reset (in a `finally`, say) would drop that change's reset. The constructor's `ContainersGenerated` handler calls `QueuePeerReset()` in place of its own `InvokeAsync` (Call 2).
2. **No peer, no post.** This departs from main's handler, which posts whether or not a peer exists, and follows the step 0 prototype, which returned when no peer existed. A host no client has read has nothing cached, and a peer made after the change reads the panel fresh on its first `GetChildren()`, so skipping the post loses nothing; it saves one no-op `Loaded` post per change on every unread host.
3. **`OnItemsChanged`.** `LayoutItemsControl` overrides `OnItemsChanged(NotifyCollectionChangedEventArgs e)`: it calls the base, then `QueuePeerReset()` when `e.Action` is `Remove`, `Replace` or `Move` (Call 1). `Add` and every Reset form are left to `ContainersGenerated`, which already resets for them on every host that has a panel, and a host with no panel lists no item peers (Call 4).
4. **Queued, not inline.** `OnItemsChanged` runs after the panel has applied the change, so an inline reset would read the right items too. It is queued because an inline reset rebuilds the host's children once per change in a burst, and because the rebuild asks each rewrapped item peer to re-read at `Send` priority, which would run before a layout that is still pending, when a new container has no template yet; at `Loaded` it runs after that layout, as the existing path's does. A queued operation's exception lands in its task (`Dispatcher.InvokeAsync`), not in the app's unhandled-exception path (recorded at `T/ReadingViewTests.cs:3150-3155`), as the existing path's does.
5. **The doc comments** on the constructor and on the new override say what each trigger covers and why a removal needs its own (#1860: a removal, replace or move generates no containers, and the host's children are otherwise rebuilt only once a client has connected the window), and why the flag clears first.

**Rejected.**

- *Inline reset in `OnItemsChanged`*: Design 4.
- *A host peer that never caches its children* (override `GetChildrenCore` to rebuild every time). WPF's `GetChildren()` returns its own cache unless it is invalidated; the cache is not the override's to bypass.
- *A gate fact for #1860.* It cannot be red (a connected client heals it on main).

## Tests

Failing facts first, in a tests-only commit that compiles against main: facts 1, 2 and 3. No fact adds a sleep, a `Task.Delay` or a timed pump: hosted facts use the class's `Drain()` (a `ContextIdle` frame that runs layout and everything queued at `Loaded` before it, `T/LayoutItemsControlTests.cs:251-259`) and `PumpedDispatcher.PumpUntilDrained(Task)` (`T/PumpedDispatcher.cs:127-136`), which `TestWaitCensus` does not count as fixed waits.

1. `AChangedSourceLeavesNoStalePeerOnTheHost`, a theory over: Remove, Replace, Move, a burst of 100 removals, Clear, Clear then refill with equal records, an `ItemsSource` swap, Add, and Remove from a host made `Collapsed` after its first read. Each arm builds the fact-5 shape on a host **in no window** (`BeginInit`, `EndInit`, measure, arrange, `UpdateLayout`; step 0 measured the same answers shown and not shown), so no client can connect it and the fact needs no desktop lock. It reads the host peer's children and each item peer's children once, makes the change, runs one `Drain()`, and asserts: the host peer's `GetChildren()` holds one `ItemAutomationPeer` for each item in `host.Items`, in order, each peer's `Item` the same object (by reference) as `host.Items` at its index, and nothing else; and each item kept by the change has the same peer object as before (`Assert.Same`; in the refill and swap arms the kept item is a new equal record, and WPF moves the reused peer's `Item` to it, `wpfsrc\ItemAutomationPeer.cs:688-703`). Red on main: Remove, Replace, Move, the burst and the collapsed Remove (by reading for the last: a removal generates nothing, `Panel.cs:732-735`; the PR body records its run on main). Green on main and kept as guards: Clear, refill, swap and Add.
2. `AClientThatConnectsAfterARemovalSeesNoExtraElement`: the issue's schedule. A borderless window shown with `ShowActivated = false`, a host of five rows with an `AutomationId`, its peer read in process; then a premise: the window's root is not connected (`ContextLayoutManager`'s automation list is empty, read by reflection as step 0's probe did, `Cb25ProbeTests.final.cs:55-62` in the working notes), so a client already connected cannot heal the host and make the fact pass on main. Then `Remove` of the middle row, one `Drain()`, and a `System.Windows.Automation` client on a thread-pool thread (`AutomationElement.FromHandle`, then `TreeWalker.RawViewWalker` from the host found by its `AutomationId`), waited with `PumpedDispatcher.PumpUntilDrained` while the STA pumps: four `SlateLayoutItem` children, each with one Button child, A, B, D, E, and no element with an empty rectangle; the control view holds Buttons A, B, D, E. Red on main (step 0: an empty `SlateLayoutItem` where C was). Takes the desktop lock when run locally. The issue's done-when.
3. `ABurstOfRemovalsQueuesOneReset`: on a host whose peer was read and on a twin `LayoutItemsControl` with the same rows and template whose peer was never made, count the operations posted at `DispatcherPriority.Loaded` through `Dispatcher.Hooks.OperationPosted` (precedent `T/MoveToFocusTests.cs:205-220`) while 100 rows are removed from each; the host's count minus the twin's is exactly one. Then a `Drain()`, and the same for a Clear and 5 Adds: exactly one. With Design 2 the twin posts none of the host's own, so the difference isolates them. Red on main: 0 for the removals and 0 for the Clear and Adds (main posts its six for the twin too), by reading of `:296-300` and M2's cycle counts; the PR body records the run.

Mutations, each alone, each rebuilt and the `SlateWindows.dll` timestamp checked: **A** (drop `Remove` from the set) turns fact 1's Remove, burst and collapsed arms, fact 2 and fact 3's burst half red; **B** (drop `Replace`) turns the Replace arm red; **C** (drop `Move`) turns the Move arm red; **D** (drop the flag) turns fact 3's burst half red; **E** (post whether or not a peer exists, main's shape) turns fact 3 red; **F** (reset inline in `OnItemsChanged`) turns fact 3's burst half red (an inline reset posts nothing at `Loaded`) and leaves facts 1 and 2 green, by M5's evidence; the PR body records both; **G** (clear the flag after the reset) is recorded as no fact turning red, by reading (nothing in the reset changes a collection today), and Design 1's comment carries the reason.

**The gate.** No gate file is edited (Call 3). The PR runs `FluentShell_UiaPatternsKeyboardFocusAndAxe_AreClean` (whose Split Down is a Replace in the split host after UI Automation reads, the app's exercise of the new path) and `PropertiesHeader_RowsEditSheetsAndRenameCarryTheMacShapes` once on the head under the lock, and the PR body quotes both.

**Existing facts that must stay green, unedited:** the five `LayoutItemsControlTests` facts, `CodeBuiltSiblingTests`, `TextScaleTests.ARecentSearchKeepsItsWholeQuery`, `TextScaleTests.TheWelcomeViewReachesItsLastRecentVault`, `WelcomeTabRingTests`, `ItemContainerNameCensus`, `EmbedPreviewTitleCensus` (it reads `AutomationLandmark.cs` by text, `T/Censuses/EmbedPreviewTitleCensus.cs:280`), `TestWaitCensus`, `StaThreadCensus`, and the two gate journeys above.

## Docs and registers

- No register row names #1860 (searched `docs/plans/*_contracts.md` for `LayoutItemsControl` and the issue numbers; contract 40's R-4 at `:711` is a delivery record). Search once more before the PR opens.
- Contract 38's ledger, the parity matrix and the focus manifest: no change; `python -I scripts/a11y_trigger_ledger.py --check`, `python3 scripts/generate-parity-matrix.py --check` and `FocusIntegrityManifestCensus` pass unedited.
- **#1954**: nothing (Windows only, no core change).
- **#1955**: nothing expected. The PR body names, as an observation, that no gate journey closes a split pane.

## Calls for the lead

1. **Move is in the set.** Recommended: yes. Step 0 measured a Move leaving the raw view in the old order for good; no site calls `Move` today, so it is a guard for the next one. Cost: one arm of fact 1 and mutation C. Alternative: leave Move out and name it in the PR body.
2. **One flag for both triggers, and no post without a peer.** Recommended: the existing `ContainersGenerated` reset goes through the same `QueuePeerReset()`, so a Clear and 5 Adds queue one reset, not six, and an unread host posts nothing. It changes #1677's path in timing and in posting only (the reset still runs at `Loaded` after the last change queued before it, and only a host with no peer stops posting); fact 5 and the recent-vault facts (`WelcomeTabRingTests`, `TextScaleTests.TheWelcomeViewReachesItsLastRecentVault`) pin #1677's behaviour. Cost: fact 3's second half and mutation E. Alternative: a flag and the peer check for the new trigger only, leaving the existing path as it is; fact 3 then counts the removal burst only.
3. **No gate edit.** Recommended. The gate cannot be red for #1860 (a connected client heals on main), a queued reset's exception lands in its task where no gate step sees it, and closing the pane Split Down made removes nothing that survives: the vertical host that loses the pane is rebound to the survivor's empty `Children` in the same operation (a Reset), and the only change on the new path is the root host's Replace, the same kind as Split Down's, which the journey already runs. Cost: none; the PR body names Split Down's Replace as the app's exercise. Alternative: a pure Remove in the app (Split Down again on the new pane, an `Insert` at `Layout.cs:461`; then Close Pane through `ClosePaneMenuItem`, `S/MainWindow.xaml:388`, which removes one of three panes at `:566` with no Replace and requests editor focus itself), measured on `main` first; it would edit `G/ShellAccessibilityTests.cs` and plan §6's gate row and §5's lane would gain CB-25.
4. **Reset is left out of the set.** Recommended: on every host with a panel a Reset regenerates and so already resets through `ContainersGenerated` (collapsed or not), and a host with no panel lists no item peers, so a Reset trigger covers nothing reachable and no fact could turn red without it. Cost: none. Alternative: keep Reset as a declared no-op guard, with a mutation recorded as turning nothing red by construction.

### Lead's rulings (2026-11-18)

- Lead's ruling on Call 1: accepted. Move is in the set.
- Lead's ruling on Call 2: accepted. The shared flag, with no post when there is no peer; the PR shows #1677's facts green on the change.
- Lead's ruling on Call 3: accepted. No gate edit; the PR says why a gate step cannot be red for #1860.
- Lead's ruling on Call 4: accepted. Reset stays out of the set, and a code comment in `OnItemsChanged` says why.
- The plan-record corrections (eleven sites, no gate journey closes a pane, #1861's lesson was an item peer's reset) go in the delivery record; the lead carries them into the docs PR.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-cb25'; $env:CARGO_BUILD_JOBS = '8'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
$t = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
$g = 'apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj'
# red on the tests-only commit, green on the fix (fact 2 under the desktop lock)
dotnet test $t -c Release --no-build --filter "FullyQualifiedName~LayoutItemsControlTests"
dotnet test $t -c Release --no-build --filter "FullyQualifiedName~CodeBuiltSiblingTests|FullyQualifiedName~TextScaleTests.ARecentSearchKeepsItsWholeQuery|FullyQualifiedName~TextScaleTests.TheWelcomeViewReachesItsLastRecentVault|FullyQualifiedName~WelcomeTabRingTests|FullyQualifiedName~ItemContainerNameCensus|FullyQualifiedName~EmbedPreviewTitleCensus|FullyQualifiedName~TestWaitCensus|FullyQualifiedName~StaThreadCensus|FullyQualifiedName~FocusIntegrityManifestCensus"
# under the desktop lock
dotnet test $g -c Release --no-build --filter "FullyQualifiedName~FluentShell_UiaPatternsKeyboardFocusAndAxe_AreClean|FullyQualifiedName~PropertiesHeader_RowsEditSheetsAndRenameCarryTheMacShapes"
python -I scripts/a11y_trigger_ledger.py --check
python scripts/check_windows_census_trait.py; python3 scripts/generate-parity-matrix.py --check
$cs = git diff --name-only origin/main -- '*.cs'
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs
```

Then the whole unit project once under the lock (35 to 45 minutes, background) before the push. CI: `windows.yml` on the PR, in full. The PR body quotes each command with its result.

## Acceptance

- [ ] Facts 1 (its Remove, Replace, Move, burst and collapsed arms), 2 and 3 were seen red on `main`'s product code and green with the fix (plan §4 rule 7); the guards in fact 1 are green on both; fact 2's premise held in both runs.
- [ ] Mutations A to G each recorded with the facts they turn red (or, for G, why none can).
- [ ] The product diff is `LayoutItemsControl` alone: the flag, `QueuePeerReset()` with its peer check, the constructor's handler calling it and the `OnItemsChanged` override for Remove, Replace and Move.
- [ ] No new fact holds a fixed sleep, delay or timed pump; `TestWaitCensus` passes.
- [ ] The two gate journeys ran on the head under the lock and passed; quoted.
- [ ] Plan §4 rule 9: a red team, then Codex rounds until approve; the PR body records both.
- [ ] `Closes #1860`, and `gh pr view N --json closingIssuesReferences` shows #1860 alone; no close, fix or resolve word before #1861, #1677 or any other number.
- [ ] `windows.yml` green on the pinned head; Codoki "Safe to merge" at 3/5 or higher after the last push. The PR is ready for review when opened (AT none).
- [ ] The PR body ends with the count: found on the way, fixed here, deferred, filed.

PR title: `fix(windows): a layout host drops a removed, replaced or moved item's peer`.

## Red team record (2026-11-18)

A fresh Opus agent at xhigh read the first draft against the tree at eeb99032, the step 0 output and the WPF 10 sources: 0 blockers, 5 medium, 13 low. It found the design right: `OnItemsChanged` runs after the panel has applied the change, so a reset queued there reads the right items, and the shared flag cannot skip a needed reset while it clears before the reset. Every finding was checked against the cited sources and taken.

- Medium: Reset in the set covers no reachable host (a Reset regenerates on every host with a panel, collapsed or not; a host with no panel lists no item peers), so Call 4 now leaves it out and mutation E is gone in that form. Fact 3's twin could not isolate the host's posts while the host posted without a peer; Design 2 now posts only with a peer (as the step 0 prototype did, now said), with a `Drain()` between fact 3's halves and mutation E for the peer check. Facts 1 and 2 could pass on main if a client connected the window; fact 1 runs on a host in no window and fact 2 uses an unactivated window and asserts the premise. The gate step removed nothing that survives and could not see a queued reset throw; Call 3 now recommends no gate edit, with a pure Remove journey as the alternative. The gate step was never run; it is gone.
- Low, all taken: Design 4 gives the real reasons against an inline reset and cites `ItemsControl.cs:290-307`; Design 1 says why the flag clears first (mutation G); the mutation predictions are complete; fact 3 is red on main and listed so; fact 1 compares peers with `Assert.Same` and items by reference; fact 2 names `PumpUntilDrained` and an `AutomationId`; the citations are corrected (`Drain()` at `:251-259`, Close Vault at `:922`, eight hosts in the reading, `S/Bases/`, `ContextLayoutManager.cs`, Close Pane and the restore's prune on site 10); the Goal's timing and the burst's wording are exact; the cause says why no gate or reader shows it.

## The diff's red team and the lead's second ruling (2026-11-18)

A fresh Opus agent at xhigh read the diff eeb99032..a21d2e9e through git: 0 blockers, 2 medium, 10 low.

- Medium: "no post without a peer" (Design 2, ruling 2) drops a reset `main` ran. `main` queued the `ContainersGenerated` reset unconditionally and looked the peer up when it ran, so an Add, then a peer made and read before the next layout, with the new item's peer reading its container before it has a template, was healed at `Loaded`; with Design 2 it is not. **Lead's ruling, 2026-11-18, replacing ruling 2 in part: option (a).** The `ContainersGenerated` trigger queues the reset whether or not a peer exists, as on `main`, still coalesced by the shared flag; the new Remove, Replace and Move trigger keeps the peer check. The red team's counter-fact joins the facts, red on the head before the change; fact 3's twin is a plain `ItemsControl`, with an arm for a `LayoutItemsControl` whose peer was never made, so mutation E still bites.
- Medium: fact 2 skips where a desktop UI Automation client's handlers are already advised into the test process, which every shared-process run here showed (a departure from the spec taken during the build, at the principal's instruction). Accepted by the lead on this condition: after the push the windows lane's TRX is read for fact 2, and if it skipped on CI the PR writes `not delivered` and adds a #1955 line for a CI-side witness. Facts 1 and 3, hosts in no window, carry the proof wherever it skips.
- Low, taken: the skip's wording names a desktop client's handlers; the WM_GETOBJECT route skips too; the Reset comment names non-virtualizing panels; the inline-reset reasons lead with the burst; fact 3 asserts the raw counts; mutation D measured `(100, 6)`, red in both halves, because the shared flag also coalesces the refill. Low, accepted: a post the dispatcher aborts leaves the flag set for that host's life; the reflection in `ConnectedRoots()` fails loudly if WPF renames its field.

## What delivery changed (2026-11-18)

- **Design 2 reversed in part** (the lead's ruling on the diff's red team, option (a)): the `ContainersGenerated` trigger posts whether or not the host's peer exists, as on `main`; only the new Remove, Replace and Move trigger checks for a peer. `QueuePeerReset(bool onlyWithAPeer)` carries the difference; the shared flag still coalesces both.
- **Facts added beyond the spec:** `AnItemAddedBeforeTheHostsPeerIsMadeShowsItsButtonAfterLayout`, the red team's counter-fact, in a shown window (a host in no window reads arrange-valid before any layout, which no app host meets, so the no-window shape could not be green on `main`): green on `main`, red on Design 2, green with (a). Fact 3 counts six posts with a plain `ItemsControl` twin and a never-peered layout host: `(1, 0, 0, 1, 0, 1)` on the head, `(0, 0, 0, 6, 0, 6)` on `main`.
- **Fact 2's premise** named only the connected-root list; on this desktop a UI Automation client's handlers in WPF's event map (and a WM_GETOBJECT) connect a new window's root first. Fact 2 and the counter-fact skip, naming the route, where either holds; locally they skipped in every shared process and ran alone. On CI (run 44314910379, head c132763d) both ran and passed (TRX), so no `not delivered` line or #1955 line.
- **Mutation D** turns both halves of fact 3 red, not only the burst half: the shared flag also coalesces the refill's generations.
- **The gate journeys** ran on b63a28f4 and again on the head c132763d under the lock: both passed.
- **Reviews:** spec red team (0/5/13), diff red team (0/2/10), Codex round 1 approve on c132763d; Codoki at its monthly limit, the lead's independent judge said Merge.
- **Spec claims that were wrong:** Design 2 (no post without a peer) dropped a reset `main` ran; fact 2's premise named one client route of two; mutation D's prediction (one half).
