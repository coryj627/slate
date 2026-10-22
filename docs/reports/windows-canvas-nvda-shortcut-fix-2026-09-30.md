# Canvas New Card and the NVDA launch shortcut

The unchanged Windows Canvas authoring journey failed because Ctrl+Alt+N was globally reserved by the installed NVDA shortcut. Slate now advertises **Ctrl+Alt+T** for a new text card. Ctrl+Alt+N remains a Canvas-only compatibility alias when Windows delivers it to Slate. Mac Option+Command+N and Windows Connected Card Ctrl+Alt+Shift+N remain unchanged.

## Diagnosis before the change

The isolated `CanvasAuthoring_NewCanvasCardEditorAndUndo_AreReachable` fact failed at its first physical Ctrl+Alt+N press: no text-card row appeared. The test ran from 2026-10-01 01:38:40.919Z to 01:38:58.299Z, with a failing duration of 17.401 seconds and total test-run time 18.047 seconds. Its assertions and production binaries were unchanged.

A separate read-only native focus trace observed Slate PID 37296 in the foreground at 01:38:42.319Z, then Explorer at 01:38:46.147Z, well before fixture cleanup. The exact public desktop shortcut `C:\Users\Public\Desktop\NVDA.lnk` declares `Alt+Ctrl+N` and targets `C:\Program Files\NVDA\nvda_slave.exe` with `launchNVDA -r`. NVDA PID 12792 started at 01:38:46.450Z, 303 milliseconds after that foreground transition. Its executable reports product version 2026.2 and file version 2026.2.0.57664. This combination identifies a launch/restart collision rather than evidence for a speculative Canvas focus repair.

At 02:04:23Z, a bounded native `RegisterHotKey` probe returned Win32 error 1409 for Ctrl+Alt+N. Ctrl+Alt+T registered and immediately unregistered successfully, with both errors zero. A reservation probe alone does not identify an owner; the shortcut metadata and process timing supply the additional evidence here. The probe sent no keys and changed no settings. [Microsoft documents hotkey registration failures when a shortcut is already registered](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey), and [NVDA documents its Ctrl+Alt+N desktop launch shortcut](https://download.nvaccess.org/documentation/userGuide.html#CreateDesktopShortcut).

## Change and scope

`ChordTable` owns the canonical T binding, spoken form, and explicit Mac divergence. Menu InputGestureText and UIA AcceleratorKey, palette hints, and the empty-canvas onboarding sentence continue to derive that row. The existing surface-interaction row pattern records the legacy N alias, with its reason, without adding a registry command or global key binding. Both exact Control+Alt arms in `CanvasNavigator` call the existing New Card mutation funnel. Connected Card retains its Shift+N arm.

Reading Ctrl+Alt+T remains next Table; Ctrl+Alt+Shift+T remains previous Table. `WorkspaceTemplates.xaml` mounts Reading and Canvas surfaces as siblings, with mutually exclusive Markdown-reading and Canvas visibility. The Canvas surface contains no Reading surface. Its tunnelling handler only receives keys from its own focused descendants. The card editor is a separate MainWindow overlay containing a plain TextBox and `SheetKeyboardFence`; its input route does not pass through the Canvas surface.

The authoring fact now requires a reserved legacy N shortcut: it holds a test-owned native reservation on a clean runner, or accepts only error 1409 for an existing owner on the live NVDA desktop. Other registration errors fail immediately. A test-owned reservation is released on the registering thread in `finally`; an existing owner's registration is never touched. The fact checks the advertised menu accelerator and onboarding, sends real T keystrokes, creates a card, opens its editor, types text, commits with Escape and undoes the edit. A T press while the draft has focus must preserve the draft, focus, and card count. The other three authoring journeys use the canonical T chord while preserving their mutation, editor, focus, and undo assertions.

Unit witnesses cover canonical T and legacy N delivery, the advertised Mac/Windows bindings and reasons, unchanged Reading and Connected Card bindings, absence of a global T claim, and existing bidirectional production-source/table drift checks. The projected `chords.json` preserves delivery evidence; the Windows specification records the divergence.

## Verification status

The coordinated Release build passed with zero warnings or errors. The authored projection fact passed (1/1, 0.705 seconds total), using `SLATE_CHORDS_UPDATE=1`, and preserved `deliveryEvidence`. `python scripts/generate-parity-matrix.py` and its `--check` both passed (183 command rows, 53 with chords, 16 leaves).

The frozen Release binaries passed **510/510** focused unit and closure checks in 1.0947 minutes: Canvas document 173, navigator 147, chord table 22, Reading 72, editor 5, command drift 4, registration 23, navigation help 10, sheet admission 44, and sheet fence 10. The sole excluded fact was `AnInVaultSymlinkPointingOutsideTheVaultIsRefused`, whose direct file-symlink creation requires unavailable local privilege. No other assertion or test was excluded; the junction, identity, containment and long-path witnesses ran.

The mandatory native authoring fact passed **1/1**, with a fact duration of 7.1574 seconds and total run time 7.7956 seconds, at 2026-10-01 02:35:50–02:35:57Z. It proved the advertised T menu accelerator/onboarding, real T card creation while N was reserved, editor text entry, editor T isolation, Escape commit, and Ctrl+Z undo. NVDA remained PID 12792 with start time 01:38:46.4500735Z and session 1 before and after. This is a green physical replacement for the earlier unchanged N-chord red witness, with no NVDA or shortcut reconfiguration. The remaining Canvas journeys and final shell suite are assigned to the parent verification run; they are not claimed here yet.

Commands, run sequentially from `apps/slate-windows` with native desktop/COM access and no intervening source/build change:

```powershell
dotnet test tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter '(FullyQualifiedName~ChordTableTests|FullyQualifiedName~CommandDriftTests|FullyQualifiedName~CommandRegistrationTests|FullyQualifiedName~NavigationHelpTests|FullyQualifiedName~CanvasNavigatorTests|FullyQualifiedName~CanvasDocumentTests|FullyQualifiedName~CanvasCardEditorTests|FullyQualifiedName~ReadingViewTests|FullyQualifiedName~SheetKeyboardFenceTests|FullyQualifiedName~SheetChordAdmissionTests)&FullyQualifiedName!~AnInVaultSymlinkPointingOutsideTheVaultIsRefused' --logger 'trx;LogFileName=canvas-chord-closure.trx' --results-directory <evidence-root>/canvas-chord-closure-results
$env:SLATE_REQUIRE_UI_AUTOMATION = '1'
$env:SLATE_ACCESSIBILITY_EVIDENCE_DIR = '<evidence-root>/canvas-authoring-canonical-evidence'
dotnet test tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj -c Release --no-build --filter FullyQualifiedName~CanvasAuthoring_NewCanvasCardEditorAndUndo_AreReachable --logger 'trx;LogFileName=canvas-authoring-canonical.trx' --results-directory <evidence-root>/canvas-authoring-canonical-results
```

Both invocations also used `--blame-hang-timeout 2m --blame-hang-dump-type mini`; neither needed a dump. `<evidence-root>` is the local implementation artifact directory listed below. Two independent source reviews found no actionable blocker in the scope, alias, reservation lifetime, cleanup, or physical witness.

No NVDA settings, executable, shortcut, or OS input configuration was changed. Physical and human NVDA acceptance, other assistive technologies, keyboard layouts, and Mac integration remain separate follow-up checks. A free chord observed on this desktop is not a universal global-availability guarantee.

## Separate announcement capture finding

The local FlaUI launch listener returned `Heard[]` for the exact `Announcements Vault 02afad11` fixture. A bounded, whitelisted NVDA log slice at 21:40:14–21:40:35 local time identifies four notifications in order and retains all four complete matching speech payloads: vault opened, scanning two files, scan complete, and empty editor pane. The first notification's bounded text contains its fixture prefix rather than its complete expected string. Notification kind was `Other`, processing was `All`, and activity IDs were `slate-accessibility-announcement.1` through `.4`. This establishes delivery at NVDA's notification/speech boundary for that fixture. It does not establish human audibility or resolve the separate FlaUI listener capture failure. No unrelated log content was retained.

Local diagnostic artifacts are under `C:\Users\cory\AppData\Local\Temp\slate-windows-implementation-2026-09-30`: `shell-canvas-isolated-results/canvas-authoring-isolated.trx`, `shell-canvas-focus.jsonl`, `shell-canvas-shortcut-readonly.json`, `shell-nvda-version-readonly.json`, `shell-nvda-process-readonly.json`, `shell-hotkey-availability-readonly.json`, `shell-nvda-02afad11-canonical-readonly.json`, `canvas-chord-closure-results/canvas-chord-closure.trx`, `canvas-authoring-canonical-results/canvas-authoring-canonical.trx`, `canvas-authoring-canonical-evidence`, and the before/after `shell-nvda-*-canonical-readonly.json` snapshots.
