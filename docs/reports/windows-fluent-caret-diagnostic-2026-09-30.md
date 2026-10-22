# Bounded Windows FluentShell caret diagnostic

The earlier full-run caret failure remains unexplained. The unchanged isolated fact passed twice on final revision `8c8a4c55c6124402c31d1e0f252c997960d35146`, and a working passive observer captured the first physical traversal reaching the embed target. This establishes useful failure instrumentation and a passing composed input path; it does not classify or repair the earlier occurrence.

No tracked source, product binary, assertion, NVDA configuration, shortcut, OS setting or focus/input behavior was changed. Only a standalone observer was compiled under the task's Temp evidence directory. Desktop ownership was coordinated with `launch_capture_diagnostic` and released after both processes exited. The observed app's PID was 38768; NVDA remained PID 12792, started at 01:38:46.4500735 UTC.

## Revision and retained evidence

The recorded full-run failure on the earlier palette/shortcut revision was 02:43:16.822596–02:43:33.878779 UTC, duration 17.053989 seconds. It failed the first `PlaceCaretAtText(editor, "![[Folder/child]]")` at `RunShellAccessibilityGate` line 340. Its assertion records only the final mismatch, without actual selection offsets, continued native/editor focus, modifier state or range-generation evidence.

The earlier unchanged isolated pass was 02:49:50.485063–02:50:57.710929 UTC, duration 67.220405 seconds. Its existing native focus trace has 585 total records, of which 579 have the owned main HWND. All 579 show the owned foreground PID and a GUI focus HWND. It has no modifier or UIA selection fields, and therefore cannot explain the failed occurrence.

This follow-up used the frozen Release assemblies with `--no-build`:

| Run | Outcome | UTC interval | Fact duration |
| --- | --- | --- | --- |
| Final isolated, observer startup failed before UI calls | 1/1 passed | 03:40:58.202693–03:42:04.267490 | 66.089691 s |
| Final isolated with corrected passive observer | 1/1 passed | 03:43:31.801484–03:44:37.633488 | 65.856819 s |

The initial observer used the portable `System.Management.dll` reference, which throws `PlatformNotSupportedException`. The Temp project was corrected to reference its existing Windows runtime assembly; product binaries were not rebuilt. Product DLL modification times remained 03:08:03.470706 UTC for SlateWindows and 03:00:02.111423 UTC for the accessibility assembly, both before this diagnostic.

All artifacts are under `C:/Users/cory/AppData/Local/Temp/slate-windows-implementation-2026-09-30`:

- `fluent-caret-final-results/fluent-caret-final-isolated.trx`
- `fluent-caret-observed-results/fluent-caret-observed.trx`
- `fluent-caret-observed.jsonl` — numeric/native observations only, no document text.
- `fluent-caret-observed-summary.json` and `fluent-caret-initial-transitions.json`.
- `fluent-caret-observer/Program.cs`, project and compiled diagnostic.
- Corresponding `fluent-caret-final-evidence` and `fluent-caret-observed-evidence` directories.

## Observed first traversal

The observer accepts exactly one newly started process with the final production executable path and a command-line root matching the disposable `slate-shell-accessibility-{guid}/Accessible Vault` fixture; it rejects ambiguity. It reads only the `MarkdownEditor` named `note.md editor` in that owned window. It sends no input and calls no focus-setting, selection-setting or range `Select` operation. Native observations use foreground/GUI-thread handles and only the high bit of modifier key states; range clones are manipulated solely to measure offsets.

There were 210 successful UIA samples over the 30-second editor window, zero recorded COM/provider exceptions, and 214 total polling iterations. The first embed leg comprised 64 samples from 03:43:35.073790 to 03:43:43.913341 UTC:

- Every integer caret offset 0 through 33 was observed in order, with 34 transitions and no backwards transition.
- Selection start equaled selection end throughout that leg.
- The owned app stayed foreground and the editor remained focused throughout all 64 samples.
- No Control state was down after offset 0; Shift, Alt and Windows keys were never down during that leg.
- Document length remained 78; the fresh embed target remained at offset 33.
- The original and freshly found target endpoints agreed throughout all 210 successful samples, and editor RuntimeId stayed `7,38768,45258072`.

This directly validates the source fixture's LF/UTF-16 offset premise for this run. `NavigationKeys` already sends Home and Right as extended scan codes, and the real unchanged helper drives Ctrl+Home followed by 33 Rights. No deterministic count/encoding discrepancy was found. The initial caret was already at 0 and the observer did not sample Control during that Home leg, so position alone does not attest receipt of the idempotent Home chord.

Microsoft documents that [GetAsyncKeyState's high bit reports the current down state](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getasynckeystate); its low bit is unreliable and was ignored. False readings cannot exclude very brief events between polls, and zero can also indicate access/desktop restrictions. Later samples observe Control down, supplying a positive control for this observer's key-state reads. [GetGUIThreadInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getguithreadinfo) supplies out-of-context native thread focus; editor focus and numerical text selection were sampled separately. The numeric comparisons use the editor's own [UIA text ranges](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-usingtextrangeobjects).

## Exact reproduction command and remaining proof

The unchanged native fact was run with `SLATE_REQUIRE_UI_AUTOMATION=1` and its own evidence directory:

```powershell
dotnet test apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj -c Release --no-build --filter FullyQualifiedName~FluentShell_UiaPatternsKeyboardFocusAndAxe_AreClean --logger 'console;verbosity=normal' --logger 'trx;LogFileName=fluent-caret-observed.trx' --results-directory C:/Users/cory/AppData/Local/Temp/slate-windows-implementation-2026-09-30/fluent-caret-observed-results --blame-hang-timeout 2m --blame-hang-dump-type mini
```

The observer starts immediately before that fact, with its UTC lower process-start bound:

```powershell
dotnet C:/Users/cory/AppData/Local/Temp/slate-windows-implementation-2026-09-30/fluent-caret-observer/bin/Release/net10.0-windows/FluentCaretObserver.dll C:/Users/cory/AppData/Local/Temp/slate-windows-implementation-2026-09-30/fluent-caret-observed.jsonl 2026-10-01T03:43:25Z
```

For a future run, use fresh evidence paths and the current UTC bound. The current diagnostic waits up to 25 seconds for the exact fixture and samples its editor for 30 seconds; attaching to a later full-suite occurrence would require starting it just before this fact or extending only its process-discovery budget. The poller adds another UIA client and query load, which may affect scheduling. These observed passes therefore do not exclude a composition/timing problem in the full suite.

The missing evidence is one failed traversal paired with this numeric trace. A stalled/wrong offset with a focus or modifier transition would narrow input delivery; a backwards caret reset with stable focus would motivate the document/caret-restoration path; changed RuntimeId or original/fresh target disagreement/HRESULTs would motivate provider/document replacement. The current passing trace supports none of those causes for the failed run. If native event receipt must be attested, a separately scoped input-route diagnostic would be needed; asynchronous key-state polling is not proof that each SendInput event reached the editor.

Preserve the original full-run failure, unchanged assertions and isolated passes. No skip, blind retry, focus repair, range `Select` shortcut or speculative product patch is supported by this bounded result.
