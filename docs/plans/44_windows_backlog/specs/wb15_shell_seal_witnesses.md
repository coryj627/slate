# WB-15 — The shell seal's structural witnesses catch the bypasses codex named, the pointer harness models WPF, and two probes decide I3's reach

Issues: #1313 #1308 #1307. Size M. Lanes: windows. AT: — (no speech changes; a probe that finds a bypass and adds a guard changes nothing a reader hears). Wave: W5, slot 3. Depends on: nothing (the censuses are test-only; the guards, if any, touch pattern providers no other W5 chunk edits). Parallel with: WB-10, WB-13/WB-14. Decision: WD9 (pending). Codex adversarial review per §4 rule 5.

## Goal

Contract 28 I3 and contract 40 OD-12 are held by censuses that cannot be passed by a side effect inside the admitted observer's callee, a differently named landing request, a raw HWND hook, a `MessageBox.Show` method group, or a `MainWindow` input override; `PressPointer` and `Wheel` raise a bubbling twin only when the preview stayed unhandled, as WPF does, with witnesses on a tree row and a grid cell; and two probes — a shell-owned window and UIA pattern invocation under the seal — are answered with hosted facts and either a guard or an owner-recorded I3 exclusion (WD9).

## Current state on main

All paths under `apps/slate-windows/`.

- **`tests/SlateWindows.Tests/Censuses/ShellSealAdmissionCensus.cs`.** `PastHandledOffenders` (`:560-610`) rejects hooks whose expression names `InputManager`, `ThreadPreprocessMessage` or `ThreadFilterMessage` (`:600-604`); no `HwndSource.AddHook` inventory. `FocusObserverShapeOffenders` (`:741-790`) pins the departure observer's registrations (`:749-770`) and the exact normalised bodies of the callbacks in `FocusObserverCallbacks` — `OnGotKeyboardFocus` and `OnLostKeyboardFocus` only (`:772-786`); nothing inspects `Transition` or `OnTransition`. `DoubleClickOffenders` (`:796-808`) is the only override detector (`OnMouseDoubleClick`/`OnPreviewMouseDoubleClick`). `IsInputEvent` (`:548-555`) already decides "derives from `InputEventArgs` or is `AccessKeyPressedEventArgs`" and is reusable. The planted-mutation table is `EachDetectorCatchesItsBypass` (`:233-271`, `[InlineData]` rows at `:233-236`, the switch at `:266-271`).
- **`src/SlateWindows/EditorLandingSlot.cs`.** `OnGotKeyboardFocus` `:262-270` and `OnLostKeyboardFocus` `:272-280` delegate to `Transition` (`:282-293`), which calls each slot's `OnTransition` (`:234-260`): early returns for parking/restoring (`:245-248`), the within-target check (`:250-256`), else `_ = Withdraw()` (`:259`). A `Keyboard.ClearFocus()`, a command or an announcer call added at `:234-260` or `:282-293` leaves the pinned callbacks unchanged.
- **`tests/…/Censuses/EditorLandingEntryCensus.cs`.** `Permitted` (`:25-30`) names the two entries and the two AR-42 methods (`Canvas/CanvasSurfaceView.cs:FocusProjection`, `Graph/GraphSurfaceView.cs:RequestProjectionFocus`); `Creations` (`:102-103`) is name-only on `RequestFocusLanding`; the facts are `:33`, `:74`, `:91`. No census of `CanvasFocusRequest`/`GraphFocusRequest` construction or property writes, and nothing proves the AR-42 methods are called only from inside their surfaces.
- **`src/SlateWindows/ShellModalLoopMonitor.cs`.** `_source?.AddHook(Hook)` `:91` (`RemoveHook` `:75`, `:89`); `Hook` (`:95-…`) acts only on `WmEnable` (`:98-100`). Not inventoried by any census.
- **`tests/…/Censuses/ShellPromptOwnerCensus.cs`.** `Offenders` (`:159-226`) walks `InvocationExpressionSyntax` only (`:180`); `boxes >= 10` floor at `:79`; the planted table `:97-101`. A `Func<string, MessageBoxResult> prompt = MessageBox.Show; prompt("x")` resolves to the delegate's `Invoke` and is not counted.
- **`tests/SlateWindows.Tests/CommandPaletteTests.ShellKeys.cs`.** `PressPointer` (`:688-706`) raises the bubbling twin with `second.Handled = first.Handled` (`:702`); `Wheel` (`:712-731`) the same (`:725`); the key `Press` (`:803-815`) already does `if (!handled)` (`:809`). `AClickOnAListsEmptyAreaMovesNoKeysUnderTheSeal` (`:436-470`) relies on the synthetic twin and says so (`:424-434`): what it pins is the registration, not a live route.
- **`src/SlateWindows/Reading/ReadingTableGrid.cs`.** `Show` (`:28-61`) creates its own top-level `Window` (`:36-43`, `Owner = owner`) hosting an `AccessibleDataGrid`; `MainWindow.Seal.cs:120-124` installs the admission on `MainWindow`'s own preview handlers, so the table window is outside it.
- **Contracts.** I3 is `docs/plans/28_palette_contracts.md:878`; the accepted-risk register heading is `:325`; OD-8 is `docs/plans/40_nvda_matrix_remediation_contracts.md:68`, AR-48 `:70` (the four non-input paths the seal does not govern), AR-49 `:71` (pointer motion and popup-local input). No row mentions UIA patterns or shell-owned windows.

## Reference

Contract 28 I3 (`:878`): "while Sealed … no input the shell's window routes — a key, its release, text, a pointer press, a double-click, a wheel turn, a mnemonic … acts anywhere in the shell except Escape on an open palette (T9), and one check, the shell's seal admission, decides that for every route". Contract 40 OD-12 and the allowlist premise quoted in #1313: "a focus transition … only withdraws or notes the held landing". AR-48 (`40:70`) lists what the seal deliberately does not govern; WD9's two probes decide whether shell-owned windows and UIA patterns join I3 or AR-48. No mac twin (mac has no seal).

## Design

**Censuses (test-only, `ShellSealAdmissionCensus.cs`, `EditorLandingEntryCensus.cs`, `ShellPromptOwnerCensus.cs`):**
1. `FocusObserverCallbacks` gains `Transition` and `OnTransition` with their exact normalised bodies (the `:772-786` pattern), and a semantic check that `OnTransition`'s body invokes only `Withdraw`, `CanHoldKeys`, `IsWithin`, `IsTerminalSeatInto`, `held.Target` (an allowlist of callee symbols) — a `Keyboard.*`, `ICommand.Execute`, `_announce` or `A11yEvent` construction anywhere in the call graph two deep is an offender. Planted row: `"transition-side-effect"` inserting `Keyboard.ClearFocus();` at the top of `OnTransition`.
2. **New** `EditorLandingEntryCensus.EveryLandingRequestIsConstructedOrWrittenOnlyByTheEntryOrAnAr42Method`: semantic walk over `ObjectCreationExpressionSyntax` whose type is `CanvasFocusRequest`/`GraphFocusRequest`, and over assignments to properties of those types; each must sit in a `Permitted` member. **New** `TheAr42MethodsAreCalledOnlyFromInsideTheirSurfaces`: every caller of `FocusProjection` and `RequestProjectionFocus` (semantic `SymbolFinder`-free: resolve invocation symbols per tree) lives in the same surface folder (`Canvas/`, `Graph/`). Planted: a differently named `PublishLanding()` constructing a request; an outside-surface call.
3. **New** `TheOnlyHwndHookIsTheModalLoopMonitorsAndItReadsWmEnableOnly`: inventory `.AddHook(` registrations across the shell (exactly one, `ShellModalLoopMonitor.cs:91`) and pin `Hook`'s normalised body to the current WM_ENABLE-only text. Planted: a `WM_KEYDOWN` arm.
4. `ShellPromptOwnerCensus.Offenders` adds an `IOperation` pass: every `IMethodReferenceOperation`/`IDelegateCreationOperation` whose target is `System.Windows.MessageBox.Show` or a `NamedHelper` is an offender unless it is the typed owner-taking `DisplaySeam` assignment; counted into `boxes`. Planted: the ownerless method group from #1308.
5. **New** `ShellSealAdmissionCensus.MainWindowOverridesNoRoutedInputVirtual`: every `override` in any `MainWindow` partial whose single parameter type satisfies `IsInputEvent` is an offender (or carries the admission as its first statement — accept only the former; the window's routes are instance handlers by design). Planted: `OnPreviewKeyDown` and `OnPreviewMouseDown` overrides.
6. Remark in the census's class summary: past-handled class handlers on the *bubbling* mouse events are unreachable under the seal (WPF raises no bubbling twin for a handled preview, dotnet/wpf `MouseDevice.cs` ~1839/1854); only preview-side ones need the seal's class handlers (`MainWindow.Seal.cs`).

**Harness (`CommandPaletteTests.ShellKeys.cs`):** `PressPointer` and `Wheel` raise the twin only `if (!first.Handled)` (the `:809` shape); doc comments at `:684-687`, `:708-711` corrected. `AClickOnAListsEmptyAreaMovesNoKeysUnderTheSeal` is rewritten to assert the registration directly (the class handler is registered without `handledEventsToo`, read from `ShellSealAdmissionCensus`'s allow list) and that the unsealed press still moves the keys. **New** witnesses: `ASealedPressOnAFilesTreeRowLeavesFocusSelectionAndSpeechAlone` and `ASealedPressOnAGridCellLeavesFocusSelectionCurrentCellAndSpeechAlone` (under `ComponentDispatcher.PushModal`, then the same press acts once popped).

**Probes (hosted, WD9):**
- `ShellOwnedWindowProbe.AClickInAnOpenReadingTableUnderTheSeal…`: show `ReadingTableGrid.Show(source, shell)` from a shipped shell host, `PushModal`, press a cell (the corrected `PressPointer`), observe `Keyboard.FocusedElement`, `CurrentCell` and the announcement list. If anything moves, the fact's name ends `…IsRefusedByTheSeal` and the guard is: the table window's `PreviewMouseDown`/`PreviewKeyDown` consult `MainWindow.SealTakes` through its `Owner` (one static helper, `ShellSeal.TakesFor(Window owned, InputEventArgs e)`), recorded as I3's amendment "windows the shell owns". If nothing moves (the owned window is disabled by the owner's modal loop), the fact ends `…ActsLikeTheOwnersOwnInput` and I3's text gains the sentence "a window the shell owns is disabled with it".
- `UiaPatternProbe.*` (four facts): obtain the peer's `IInvokeProvider`, `ISelectionItemProvider`, `IExpandCollapseProvider`, `IValueProvider` via `UIElementAutomationPeer.CreatePeerForElement(element).GetPattern(…)` for a shell button, a Files tree row, a tree folder and the palette field, under `PushModal`, and call `Invoke()`, `Select()`, `Expand()`, `SetValue("x")`. If a call acts: per WD9 the providers of sealed surfaces refuse while the seal holds — a `ShellSeal.IsHolding` read in `SlateWindows`'s own peers (`CanvasRendererAutomationPeer`, the tree row and grid peers, the palette field's) before the action, hosted facts per pattern — unless the owner records an I3 exclusion in contract 28's accepted-risk register (`28:325`), in which case the facts pin the current behaviour and the register row names them. Either way the probes' results go into the PR body with the pattern-by-pattern table. *If the owner answers WD9 "no extension, record both"*: the guards are not built, both probes stay as pinning facts, I3 gains two AR rows, and #1307's item 3 closes on the record.

Deliberately not changed: the seal's input routes, `MainWindow.Seal.cs`, the modal-loop monitor's behaviour, contract 40's frozen records.

## Tests

Named above; each has a planted mutation in `EachDetectorCatchesItsBypass` (census rows) or an inverse (harness/witness):
- `TheFocusObserversTransitionCallGraphHasNoSideEffects` — fails on the planted `Keyboard.ClearFocus()`.
- `EveryLandingRequestIsConstructedOrWrittenOnlyByTheEntryOrAnAr42Method`, `TheAr42MethodsAreCalledOnlyFromInsideTheirSurfaces` — fail on the planted `PublishLanding()` and the outside-surface call.
- `TheOnlyHwndHookIsTheModalLoopMonitorsAndItReadsWmEnableOnly` — fails on the `WM_KEYDOWN` arm and on a second `AddHook`.
- `ShellPromptOwnerCensus.TheDetectorRejectsEveryPromptTheShellDoesNotOwn` gains the method-group row (expected 1).
- `MainWindowOverridesNoRoutedInputVirtual` — fails on the two planted overrides.
- `PressPointerRaisesNoTwinForAHandledPreview`, `WheelRaisesNoTwinForAHandledPreview` (harness self-facts) — fail when `:702`/`:725` are restored.
- The two sealed-press witnesses — fail if `SealTakes` is skipped for pointer presses.
- The probe facts — their verdict is the deliverable; mutation of a guard, if built: remove the `IsHolding` read, the pattern acts, the fact fails.

## Docs and registers

- `docs/plans/28_palette_contracts.md`: I3 (`:878`) gains the shell-owned-window sentence per the probe's verdict, appended, never rewritten; the accepted-risk register (`:325` section) gains rows only if WD9 is answered "exclude".
- `docs/plans/40_nvda_matrix_remediation_contracts.md`: a one-line note under AR-48 (`:70`) saying which probe moved what into I3 or stayed outside, appended.
- No designations, ledgers or matrices; no `w_c_matrix.md` row (no §W-C behaviour changes).
- Comments on #1313, #1308, #1307 with the probe table.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb15'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~ShellSealAdmissionCensus|FullyQualifiedName~EditorLandingEntryCensus|FullyQualifiedName~ShellPromptOwnerCensus|FullyQualifiedName~CommandPaletteTests.ShellKeys|FullyQualifiedName~ShellOwnedWindowProbe|FullyQualifiedName~UiaPatternProbe"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <every touched .cs>
```

Check CR counts before editing the census files (§4 rule 2). CI lanes: `windows.yml`. AT pass: none — no sentence changes. If a guard is built, a NVDA spot check that Invoke on a sealed tree row says nothing goes in the PR body (§4 rule 4's "if its implementation turns out to").

## Acceptance

- [ ] Five census facts and the method-group row, each killed by its planted mutation.
- [ ] `PressPointer`/`Wheel` corrected with self-facts; the empty-area fact rewritten; two sealed-press witnesses.
- [ ] Both probes run and their verdicts are in the PR body; the guard or the recorded exclusion exists per WD9.
- [ ] Contract 28 I3 appended, contract 40 AR-48 noted.
- [ ] `Closes #1313`, `Closes #1308`; `Closes #1307` only when item 3 has a guard or an owner-recorded exclusion (WD9), else `Refs #1307` with items 1 and 2 named as delivered.
- [ ] Not delivered: named per WD9's answer.

## Brief

Read `ShellSealAdmissionCensus.cs:233-271`, `:548-610`, `:741-808`; `EditorLandingSlot.cs:230-295`; `EditorLandingEntryCensus.cs:18-50`, `:98-112`; `ShellModalLoopMonitor.cs:70-105`; `ShellPromptOwnerCensus.cs:159-226`; `CommandPaletteTests.ShellKeys.cs:424-470`, `:684-731`, `:803-815`; `ReadingTableGrid.cs:28-61`; `MainWindow.Seal.cs:110-135`; contract 28 `:878` and contract 40 `:68-71`. Harden the four censuses with the planted rows, fix the two pointer helpers and add the witnesses, run the two WD9 probes and build the guard or write the exclusion the owner chose, append the contract text. Done when every planted bypass is caught, the probes' verdicts are recorded and the lane is green. Commit: `test(windows): the shell-seal censuses catch callee side effects, renamed landings, HWND hooks, prompt method groups and input overrides, and the pointer harness models WPF`.
