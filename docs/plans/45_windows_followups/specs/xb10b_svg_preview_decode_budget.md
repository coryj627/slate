# XB-10b — An SVG preview decode has the same budget and the same sentence

Issues: #1581. Size M. Lanes: windows. AT: NVDA spot (the failed-state sentence in the editor embed and the reading view). Wave: X5, slot 1. Depends on: XB-10a merged (`render_time_exceeded_message` over the FFI); XD3. Parallel with: XB-12, XB-13. Interlock: plan 44's WB-11/WB-20 touch `ReadingDocumentBuilder.cs`'s embed and diagram paths; whichever lands second rebases. Codex adversarial review per plan 44 §4 rule 5 (size M).

## Goal

An SVG preview decode on Windows stops being waited on after 5 s; the preview shows core's "Image not shown: decoding took longer than 5 seconds." where today it either hangs the app or says "Could not decode image"; the abandoned decode finishes in the background; at most two abandoned decodes run at once, and a new decode is refused with the same sentence while that cap holds. The guard's cost weights are a filed follow-up, not this chunk.

## Current state on main

- `apps/slate-windows/src/SlateWindows/EditorInteractions.cs` (`EditorInteractionCoordinator`): `MaxPreviewImageBytes` 8 MiB (`:3070`), `MaxPreviewImageDimension` 1,120 (`:3071`); `DecodeImage(byte[] bytes, string? mime = null)` (`:3073`) and the budgeted overload (`:3082`): size gate (`:3089`), SVG sniff (`:3094-3096`), → `DecodeSvgImage` (`:3098`). **#1582's thread**: `SvgDecodeStackBytes` 256 MiB (`:3107`); `DecodeSvgImage` (`:3122-3175`) starts `new Thread(..., SvgDecodeStackBytes) { IsBackground = true, Name = "Slate SVG decode" }` (`:3131-3152`) and waits with Win32 `WaitForSingleObject(finished, Infinite)` (`:3160-3161`; comment `:3153-3159`: `Join`/`WaitOne` would pump on an STA), falling back to `Join` and logging `SvgDecodeWaitFailed` if the wait result is not `WaitObject0` (`:3162-3166`), rethrowing through `ExceptionDispatchInfo` (`:3167`), then `DecodeRasterImage` on the PNG (`:3174`). Test seam `[ThreadStatic] SvgDecodeWaitForTests` (`:3109-3120`); P/Invoke `NativeMethods` (`:3258-3266`). The thread body `RenderSvgToPng` (`:3181-3256`): UTF-8, `IsSafeSvg` (`:3192` → `SvgPreviewGuard.Check`, `:3295-3296`), `ParseSecureSvg` (`:3268-3281`, `SecureStatic`, external resources disabled), `SKSvg` (`:3203-3205`), scale (`:3219-3221`), reservation callback (`:3226-3231`), PNG; any exception but OOM/SO/AV → null (`:3249-3255`).
- **The guard** `SvgPreviewGuard.cs` (836 lines): `SvgRefusal` (`:11-47`), `MaxRenderDepth` 256 (`:73`), `MaxRenderedElements` 200,000 (`:78`), `MaxUrlReferencesPerValue` 8 (`:82`), `MaxValueNesting` 16 (`:86`), `Check(source, maxCharacters)` (`:100`). It bounds depth and multiplied reuse, not rules × elements or marker vertices × subtree (the issue's measurements: 150 KB stylesheet ≈ 40 s, 450 KB > 2.5 min, 400 KB custom properties ≈ 29 s, 220 KB marker ≈ 43 s).
- **Callers and copy** (all host-composed; no core sentence today): editor embed `BuildImageNode` (`:1826-1842`; failure text `:1835-1836` "Could not decode image. MIME: … The file may be corrupt or use an unsupported codec."; budget text `:1821-1823`; `IsWarning: decoded is null`); reading embed `AppendEmbedImage` (`ReadingDocumentBuilder.cs:1512` decode; copy `:1546-1553`, wording differs slightly from the editor's); reading diagram `:931` (`DecodeImage(svg, "image/svg+xml")`, host reasons `:913`, `:926`, `:935`). `a11y.rs` has no undecodable-image sentence (`EmbedPreviewUnavailable` reasons `:3369-3389` are about resolution, not decoding).
- **Tests.** `SvgPreviewGuardTests` (`:44`, `:96` Theory over `Refusals` `:104-232`); `SvgDecodeOutOfProcessTests` (`:66`, `:83`; `tools/SvgDecodeProbe`); `W2EditorInteractionRedTeamTests.EmbedPreview_ExpandsNestedContentAndExplainsCorruptImages` (`:55`, asserts "Could not decode image" `:76`), `SvgPreview_UsesSecureStaticPolicyAndRejectsExternalResources` (`:82`), `SvgPreview_ReadsTheDecodeOnlyOnceItEnds_EvenIfTheWaitFails` (`:119`, expects `SvgDecodeWaitFailed` `:143`); `ReadingEmbedTests.UndecodableImageEmbedShowsMacFailureString` (`:338`, copy `:366`). The 1,120 px bound appears only as literals in `RightPanePanelsBudgetTests.cs`.
- After XB-10a: `session.RenderTimeExceededMessage("image", 5)` is available over the FFI (a free function on the generated binding; confirm its C# name after regeneration).

## Reference

XD3; XB-10a's sentence; the editor and reading failure shapes already in place (`IsWarning`, the reading failure copy).

## Design

1. **A bounded wait.** `DecodeSvgImage`: `WaitForSingleObject(finished, SvgDecodeBudgetMilliseconds)` (5,000; `internal static int` so a fact can shorten it). `WaitObject0` → today's path. `WaitTimeout` → increment `s_abandonedDecodes` (`Interlocked`), log a new `HostDiagnosticEvent.SvgDecodeAbandoned` (bytes, elapsed), do not join, and return a failed result whose reason is core's sentence; the thread body's `finally` decrements the counter (it already runs to completion; add the guard). Any other wait result keeps the `SvgDecodeWaitFailed` + `Join` fallback (`:3162-3166`).
2. **A cap.** Before starting a decoder thread, if `s_abandonedDecodes >= MaxAbandonedSvgDecodes` (2), return the failure immediately with the same sentence and log `SvgDecodeRefusedAtCap`.
3. **A reason the callers can show.** `DecodeImage` gains an overload `DecodeImage(bytes, mime, out string? failureReason, …)` used by the three callers; `failureReason` is null on success and on the ordinary "could not decode" (callers keep their sentence for that), and core's `"image"` sentence on a timeout or a cap refusal. `BuildImageNode` (`:1826-1842`) shows `failureReason ?? <today's text>` with `IsWarning: true`; `AppendEmbedImage` (`:1546-1553`) likewise; the diagram path (`:913`, `:935`) shows it as the failure reason under the "Diagram could not be rendered" header.
4. **The sentence** comes from `session.RenderTimeExceededMessage("image", 5)` (XB-10a) via the host's existing session accessor at the call site, cached once per process (it is a constant string for a constant budget); the fallback if no session is reachable at that point (the editor coordinator may not hold one) is a `static` read through the generated free function, which needs no session.
5. **A slow-decoder seam.** Beside `SvgDecodeWaitForTests` (`:3109-3120`): `[ThreadStatic] internal static Func<byte[], byte[]?>? SvgDecodeBodyForTests` consulted by the thread body before `RenderSvgToPng`, so a fact can park the decode on a barrier; it runs on the decoder thread like the real body.
6. **Follow-up filed, not built:** the guard weights (XB-0 files it; this PR links it).
7. Not changed: `SvgPreviewGuard`'s limits, the 8 MiB cap, the 1,120 px size, `SecureStatic`, the out-of-process probe, PNG decoding.

## Tests

`tests/SlateWindows.Tests/W2EditorInteractionRedTeamTests.cs` (beside the three `SvgPreview_*` facts) and `ReadingEmbedTests.cs`:
- `SvgPreview_PastTheDecodeBudgetShowsCoresSentenceAndLeavesTheDecoderRunning` — body seam parks on a barrier, budget 100 ms: the node carries the `"image"` sentence and `IsWarning`; the call returned in < 500 ms; `SvgDecodeAbandoned` logged; release the barrier; the counter returns to 0 (poll). Mutation: `Infinite` wait → the fact's STA bound trips.
- `SvgPreview_RefusesANewDecodeWhileTwoAreAbandoned` — two parked decodes abandoned, a third returns the sentence with the body seam not called (count), `SvgDecodeRefusedAtCap` logged; release; a fourth decodes.
- `SvgPreview_AnAbandonedDecodeThatThrowsStillReleasesItsSlot` — the parked body throws after release; counter 0.
- `SvgPreview_AnOrdinaryDecodeFailureKeepsTheCorruptImageSentence` — `failureReason` null on a corrupt SVG; the existing "Could not decode image" text (keeps `:55`'s fact meaningful).
- `ReadingEmbedTests.AnSvgEmbedPastTheDecodeBudgetShowsCoresSentence` — the reading twin; `ReadingDiagramTests.ADiagramWhoseSvgDecodeOutlivesTheBudgetShowsCoresReason` — the diagram twin under the "Diagram could not be rendered" header.
- Existing: `SvgPreview_ReadsTheDecodeOnlyOnceItEnds_EvenIfTheWaitFails` (`:119`) stays (the non-timeout failure path); `SvgPreviewGuardTests`, `SvgDecodeOutOfProcessTests` unchanged.
- **NVDA spot:** a scratch vault with a crafted slow SVG (the issue's "many simple rules over many elements" shape at ~150 KB, which the guard admits) embedded in a note: in the editor, the embed card's warning text is heard as core's sentence; in the reading view, the embed paragraph reads it; phrases in the PR.

## Docs and registers

- Contract 38: the two new `HostDiagnosticEvent`s are log events, not announcements; if the designations JSON lists diagnostics, add them; the sentence is core's (XB-10a) and needs no new designation (it is text in range / card content, not an `A11yEvent`).
- `docs/plans/18_windows_port/w_c_matrix.md`: a row for the embed failed state's budget sentence if the matrix lists embed failure copy (grep "Could not decode").
- #1581: link the guard-weights follow-up.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-xb10b'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
$filter = 'FullyQualifiedName~W2EditorInteractionRedTeamTests.SvgPreview|FullyQualifiedName~W2EditorInteractionRedTeamTests.EmbedPreview|FullyQualifiedName~ReadingEmbedTests|FullyQualifiedName~ReadingDiagramTests|FullyQualifiedName~SvgPreviewGuardTests|FullyQualifiedName~SvgDecodeOutOfProcessTests'
$n = (dotnet test $tests -c Release --no-build --list-tests --filter $filter | Select-String '^\s{4}\S').Count; if ($n -lt 30) { throw "filter selects $n facts" }
dotnet test $tests -c Release --no-build --filter $filter       # new facts red first
dotnet test $tests -c Release --no-build --filter "FullyQualifiedName!~ConnectionsLeafTests.TheModelOf&kind!=benchmark"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include apps/slate-windows/src/SlateWindows/EditorInteractions.cs apps/slate-windows/src/SlateWindows/Reading/ReadingDocumentBuilder.cs apps/slate-windows/tests/SlateWindows.Tests/W2EditorInteractionRedTeamTests.cs apps/slate-windows/tests/SlateWindows.Tests/ReadingEmbedTests.cs apps/slate-windows/tests/SlateWindows.Tests/ReadingDiagramTests.cs
# NVDA spot with the crafted slow SVG (back up %LOCALAPPDATA%\Slate; nvda.log recipe)
```

CI lanes: `windows.yml`. AT: NVDA spot, phrases in the PR.

## Acceptance

- [ ] A decode past 5 s returns with core's sentence, the thread finishes in the background, the counter releases; the cap of two refuses new decodes with the sentence.
- [ ] The three callers show the sentence; ordinary decode failures keep their text.
- [ ] Six facts green (red first, mutations recorded); existing SVG suites green; NVDA phrases recorded.
- [ ] Guard-weights follow-up linked. `Closes #1581`. Not delivered: the guard weights (filed).

## Brief

Read `apps/slate-windows/src/SlateWindows/EditorInteractions.cs:1815-1845`, `:3065-3300`; `Reading/ReadingDocumentBuilder.cs:905-940`, `:1505-1560`; `tests/SlateWindows.Tests/W2EditorInteractionRedTeamTests.cs:50-150`; `ReadingEmbedTests.cs:330-370`; `ReadingDiagramTests.cs:328-380`; the generated binding for XB-10a's `render_time_exceeded_message`. Build per XD3: `WaitForSingleObject(finished, SvgDecodeBudgetMilliseconds)` (5,000, `internal static`), timeout → `s_abandonedDecodes++`, `SvgDecodeAbandoned` log, failure with core's `"image"` sentence, thread left to finish and decrement in its `finally`; a cap `MaxAbandonedSvgDecodes` 2 refusing new decodes (`SvgDecodeRefusedAtCap`); `DecodeImage(..., out string? failureReason)` used by `BuildImageNode`, `AppendEmbedImage` and the diagram path, showing the reason only for timeout/cap and keeping today's "Could not decode image" otherwise; a `[ThreadStatic] SvgDecodeBodyForTests` seam run on the decoder thread. Facts: the four `SvgPreview_*` facts, the reading embed twin, the diagram twin; keep `SvgPreview_ReadsTheDecodeOnlyOnceItEnds_EvenIfTheWaitFails`. NVDA spot with a crafted ~150 KB stylesheet-heavy SVG in the editor and the reading view. Link the guard-weights issue XB-0 filed. Done when the filter selects ≥30 facts and passes, the full unit run is green, `dotnet format --include` is clean over the five files, `windows.yml` green, phrases recorded. `Closes #1581`. Commit: `fix(windows): an svg preview decode stops being waited on after its budget and shows core's sentence, with abandoned decodes capped`.
