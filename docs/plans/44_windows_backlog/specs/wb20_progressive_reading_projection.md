# WB-20 — The reading view shows text, code, math and embeds first; diagrams arrive in place, silently, without moving the reader

Issues: Closes #1317. Size M–L. Lanes: windows + FlaUI (shell gate). AT: NVDA (full pass). Wave: W7. Depends on: WB-11 (first on `Reading/ReadingSurface.cs` and `ReadingDocumentBuilder.cs`; WB-21b follows, §6). Parallel with: WB-19, WB-21. Decision: WD11 (pending; recommended "the first partial projection is applied; a late diagram is silent; its placeholder is named 'Diagram, rendering'").

## Goal

Today the reading view is all or nothing: the fetch renders every Mermaid
diagram before it publishes anything, and the build decodes each SVG on
the dispatcher, so a note with one diagram shows "Loading reading view…"
until a cold render finishes — seconds, during which a screen-reader user
has nothing to read (and #1304's journeys waited 10–30 s for a math
element parked behind a diagram). After this chunk the projection publishes
in two stages: text, code, math and embeds first, each diagram as a named
placeholder; then the rendered diagrams replace their placeholders in
place, decoded off the dispatcher, keeping the caret, the focused link and
a held landing. A late diagram says nothing; a failed one names its
failure on the placeholder and the rest of the note stays readable.

## Current state on main

Verified in the worktree; `report_rel.md`'s lines hold.

- `Reading/ReadingContentViewModel.cs:912-968` the fetch, on the worker:
  code, then math, then `FetchStage(cancel, "diagrams")` (`:920`) and
  `session.GetDiagramBlocks(path)` (`:931`; core renders Mermaid inside
  it — `session.rs:9637` → `diagram.rs:389` `render_mermaid_with_validation`),
  then blocks (`:937`), inlines (`:939-941`), embeds (`:942-944`), one
  `FetchResult` (`:956-968`) whose digest includes the diagram artifacts
  (`:951-954`). `DiagramFaultForTests` (`:926-929`). `FetchStage` is
  `:1156`. `Publish` (`:1517`) builds on the dispatcher —
  `ReadingDocumentBuilder.Build` at `:1609` (whole) and `:1757` (streamed
  chunks; `BlocksAppended` event `:225`); `FinishPublish` (`:1762-1786`)
  marks "the projection is complete: the refresh has landed (R-10)" and
  sets `_projectionComplete`.
- `Reading/ReadingDocumentBuilder.cs:893-961` the diagram arm: failure
  headers "Diagram dialect not supported" / "Diagram could not be rendered"
  with the reason (`:899-912`), the SVG budget (`:918-927`,
  `MaximumRenderedDiagramSvgBytes` `:998`), `DiagramRenderProbeForTests`
  (`:930`), `EditorInteractionCoordinator.DecodeImage(svg, "image/svg+xml")`
  (`:931`, on the dispatcher), the `ReadingDiagramElement(description,
  matched.Source)` (`:942`), `ReadingSemantics.MarkDiagramBlock(paragraph,
  landing)` (`:961`; `ReadingSemantics.cs:228-234`).
- `Reading/ReadingDiagramElement.cs:21` a focusable `ContentControl` named
  by `AutomationProperties.SetName(this, description)` (`:27`); its peer
  (`:87-100`) is a Group with localized type "diagram".
- `Reading/ReadingSurface.cs:312-330` installs the "Loading reading view…"
  placeholder (AutomationId `ReadingLoadingNotice`) and parks the caret;
  `ApplyModel` (`:378-390`) merges a built document once per identity and
  sets `_lastMerged`; `ApplyBuiltDocument` (`:401-`) reclaims focus and
  preserves the caret by symbol offset (`:413-420`). A landing is held
  while `!ShowsAppliedProjection || _model is { RefreshInFlight: true }`
  (`:506-517`); `ShowsAppliedProjection => _lastMerged is not null`
  (`:531`, "never the block count: the placeholder is a block").
- `EditorLandingSlot.cs:102-111` `Held` (let go when `!IsLive()`),
  `:216-222` `ScopeChanged`; `HeldEditorLanding` (`:354`, `IsLive :405`,
  `StillWhereAsked :409`).
- Contract 40 R-10 (`40_nvda_matrix_remediation_contracts.md:33`): the
  landing is fulfilled "when its content is applied"; AR-60 (#1279) is
  the refresh-cancellation record the diagram stage must honour.
- Facts today: `ReadingDiagramTests.cs:26` `DiagramRendersAFocusableElementSpeakingTheDescription`,
  `:73` `UnsupportedDiagramDegradesToSourceInRangeWithHonestHeader`,
  `:215` `DiagramFetchFailureDegradesToSourceInRange`, `:259`
  `ProjectionPoolBoundsAggregateDiagramDecoding`, `:328`
  `OkStatusWithUndecodableSvgFallsBackHonestly`;
  `ReadingFocusTests.cs:93` `TheEditorLandingIsHeldUntilTheContentArrives`,
  `:780` `AHeldLandingWaitsForTheRefreshInFlight`, `:892`
  `AHeldLandingWaitsThroughARetiredRefresh`, `:1286`
  `ALaterChunkFailingRefusesTheHeldLanding`. FlaUI:
  `ShellAccessibilityTests.cs:1381` `ReadingIdentityContract_ExeWindowAndSurfaceIds_AreStable`
  waits 300 s for the math/diagram elements (`:1483`), `:1720`
  `ReadingTextPattern_RangeFromChildResolvesEveryCustomPeer` (`:1813`,
  `:1832`).
- Registers: `w_c_matrix.md:23` (Diagram blocks row); `parity_matrix.md:236,
  272` (diagrams, implemented).

## Reference

Windows-only pipeline; the issue's "Wanted" and "Facts wanted" lists are
the spec. WD11 recommendation (quoted): "Yes, it is applied (the reader
can read); a late diagram arrives silently and its placeholder is named
'Diagram, rendering' until then." If the owner answers that the partial
projection is NOT applied, the held landing waits for the diagram stage
(`RefreshInFlight` stays true through it) and the FlaUI fact's "math
before diagram" assertion keeps, but the landing fact flips; if a late
diagram is to be announced, a core `A11yEvent` is needed (corpus, mirrors,
Swift compile) and the chunk grows by WB-8's shape.

## Design

- **Two stages, one generation.** `FetchResult` loses its diagram fields;
  stage 1 fetches code, math, blocks, inlines and embeds as today and
  publishes. The builder's diagram arm, given no artifact for a Mermaid
  fence, emits a placeholder `ReadingDiagramElement` named "Diagram,
  rendering" (WD11) whose content is the fence's source (the existing
  source-in-range shape), marked by `MarkDiagramBlock`, with a new
  `ReadingSemantics.MarkDiagramPending(paragraph, key)` so stage 2 can
  find it. Stage 2 runs on the same worker after stage 1 publishes:
  `GetDiagramBlocks`, then `DecodeImage` per SVG there (the result frozen
  — `Freezable.Freeze()` — before it crosses to the dispatcher), under the
  same per-diagram and projection-wide budgets (`:918-927`, `:259`'s pool
  fact).
- **Completion in place — the placeholder element is the final element.**
  The placeholder `ReadingDiagramElement` is focusable (`:23-27`) and may
  hold keyboard focus, the caret or a selection anchor when the render
  lands, so nothing is swapped: the same element and the same `Paragraph`
  stay in the document. New `ReadingDiagramElement.Complete(string
  description, ImageSource visual)` sets `Content = new ReadingDiagramImage`,
  updates `Description`, `AutomationProperties.Name` and the HelpText, and
  raises the peer's `RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, …)`;
  `ReadingDiagramElement.Fail(string header, string reason)` renders the
  existing failure shape (`:899-912`: header, reason, source-in-range)
  inside the same element. `ReadingSurface.CompleteDiagrams(IReadOnlyList<(string key,
  DiagramOutcome outcome)>)` (new) finds each pending element by its key
  (`ReadingSemantics.MarkDiagramPending`) and calls `Complete`/`Fail` —
  no block insertion or removal, so the caret, the focused element, the
  selection, the list peers' tables and WB-11's structural-children cache
  are untouched (`InvalidateStructuralPeers()` is NOT called: the block
  set did not change; only the element's name and content did). A
  `ReadingDiagramElement` that holds focus keeps it through `Complete`;
  the peer object is the same, so a UIA client's held reference stays
  valid. One `StructureChanged` is not raised either; the name
  property-change is the only event. The memo (`_memo`) gains the diagram
  digest when stage 2 lands so a later rebind memo-hits the complete
  projection.
- **Readiness (WD11).** Stage 1's `FinishPublish` marks the projection
  complete and `RefreshInFlight` false: a held landing seats on the partial
  projection (`:506`). Stage 2 is not a refresh: it does not bump the
  generation, does not set `IsLoading`, and a landing held or seated is
  untouched. It honours AR-60: it carries stage 1's generation and cancel
  token; a newer refresh retires it (`:1517-1530`'s rule), and a retired
  stage drops its result without touching the document.
- **Announcement.** None for a late diagram (WD11). The placeholder's
  name changes to the description when the image lands (property change
  only).
- Not changed: `MaximumRenderedBlocks`, the streamed-chunk path's shape
  (placeholders ride in chunks like any block), the diagram element's
  peer and Enter-speaks-the-block (`:168`), the embeds' decode budget.

## Tests

Facts fail first.

- `ReadingDiagramTests.TextAndMathAreReadableWhileADiagramRenderIsParked`
  — `DiagramFaultForTests` replaced by a `DiagramStageGateForTests`
  (a `ManualResetEventSlim`) held: after stage 1 the surface has the
  paragraphs and the math element, no `ReadingLoadingNotice`, the diagram
  paragraph is a group named "Diagram, rendering", `IsLoading` false.
  Mutation: restore the single-stage fetch → fails on the loading notice.
- `ReadingDiagramTests.TheDiagramCompletesItsPlaceholderWithoutMovingTheReader`
  — caret parked two paragraphs below, a link focused; release the gate
  (`DiagramStageGateForTests.Set()` in `finally`; the fact asserts the
  stage's `Wait(10 s)` returned true): the rendered image is inside the
  SAME `ReadingDiagramElement` instance at the same block index, the caret
  offset and `Keyboard.FocusedElement` are unchanged, `ApplyBuiltDocument`
  ran once (`ApplyProbeForTests`). Mutation: re-apply the whole document →
  the probe counts two → fails.
- `ReadingDiagramTests.ADiagramLandingWhileTheReaderIsOnThePlaceholderKeepsFocus`
  — Tab onto the placeholder element (it has focus) and hold a reference
  to its automation peer; release the gate: the same element still has
  focus, its `Name` is now the description, the held peer is the same
  object and a `NameProperty` property-change was raised. Mutation: swap
  the paragraph for a new one → focus drops to the surface → fails.
- `ReadingDiagramTests.ADiagramLandingUnderASelectionKeepsTheSelection`
  — a selection anchored before the placeholder and extended into it;
  release the gate: `Selection.Start`/`End` offsets unchanged, the
  selected text still spans the diagram block.
- `ReadingDiagramTests.AFailedDiagramRenderLeavesTheRestReadableAndNamesTheFailure`
  — a `RenderFailed` status: the placeholder becomes "Diagram could not
  be rendered" + reason + source; every other block intact.
- `ReadingDiagramTests.ALateDiagramIsSilent` — no `A11yEvent` across stage 2.
- `ReadingDiagramTests.ASupersededDiagramStageTouchesNothing` — refresh
  during the gate; the stale stage's result is dropped (AR-60).
- `ReadingDiagramTests.SvgIsDecodedOffTheDispatcher` —
  `DiagramRenderProbeForTests` records a thread that is not the
  dispatcher's. Mutation: decode in `Build` → fails.
- `ReadingFocusTests.AHeldLandingSeatsOnThePartialProjection` (WD11) —
  the landing seats before the gate is released; `:780`/`:892` keep passing.
- Existing `:26, :73, :215, :259, :328` adapted to the two-stage shape.
- FlaUI `ShellAccessibilityTests.ReadingDiagram_TextIsReadableBeforeTheDiagramRenders`
  — a note with a cold Mermaid fence: the math element and the first
  paragraph appear before any diagram image; the placeholder group is
  named "Diagram, rendering"; the image arrives within the existing 300 s.
  The `:1483`, `:1813`, `:1832` waits for TEXT elements shrink to the
  ordinary budget (the diagram wait keeps 300 s).

## Docs and registers

- Contract 40: append a record under R-10 — "a partial projection (text,
  code, math, embeds; diagrams pending) is applied; the diagram stage is
  not a refresh; a late diagram is silent; the placeholder is named
  'Diagram, rendering'" (WD11), and a note under AR-60 that the stage
  honours the cancellation.
- `w_c_matrix.md:23`: the Diagram blocks row gains the pending placeholder
  (name, type, source inside) and the in-place replacement.
- `parity_matrix.md:272`: note "Windows projects progressively (#1317);
  mac unverified".

## Verification

```powershell
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb20'; ./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
# RescanTests is ONE partial class (RescanTests.ReadingLanding.cs:27 declares `partial class RescanTests`); filter the class, not the file suffix
$filter = 'FullyQualifiedName~SlateWindows.Tests.ReadingDiagramTests|FullyQualifiedName~SlateWindows.Tests.ReadingFocusTests|FullyQualifiedName~SlateWindows.Tests.ReadingViewTests|FullyQualifiedName~SlateWindows.Tests.ReadingMathTests|FullyQualifiedName~SlateWindows.Tests.RescanTests|FullyQualifiedName~SlateWindows.Tests.ReadingDoctrineCensusTests'
dotnet test $tests --no-build --configuration Release --list-tests --filter $filter | Select-String 'TextAndMathAreReadableWhileADiagramRenderIsParked|TheDiagramCompletesItsPlaceholderWithoutMovingTheReader|ADiagramLandingWhileTheReaderIsOnThePlaceholderKeepsFocus|ADiagramLandingUnderASelectionKeepsTheSelection|AFailedDiagramRenderLeavesTheRestReadableAndNamesTheFailure|ALateDiagramIsSilent|ASupersededDiagramStageTouchesNothing|SvgIsDecodedOffTheDispatcher|AHeldLandingSeatsOnThePartialProjection'   # nine lines, else the filter is wrong
dotnet test $tests --no-build --configuration Release --list-tests --filter 'FullyQualifiedName~SlateWindows.Tests.RescanTests' | Select-String 'Landing'   # the reading-landing regressions of RescanTests.ReadingLanding.cs must be listed (nonzero)
dotnet test $tests --no-build --configuration Release --filter $filter
$at = 'apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj'
$atFilter = 'FullyQualifiedName~ShellAccessibilityTests.ReadingDiagram_TextIsReadableBeforeTheDiagramRenders|FullyQualifiedName~ShellAccessibilityTests.ReadingIdentityContract_ExeWindowAndSurfaceIds_AreStable|FullyQualifiedName~ShellAccessibilityTests.ReadingTextPattern_RangeFromChildResolvesEveryCustomPeer|FullyQualifiedName~ShellAccessibilityTests.ReadingView_IsTheEditorStop'
dotnet test $at --no-build --configuration Release --list-tests --filter $atFilter | Select-String 'ReadingDiagram_'   # one line
dotnet test $at --no-build --configuration Release --filter $atFilter   # NVDA closed
$cs = @(git diff --name-only origin/main -- '*.cs') + @(git ls-files --others --exclude-standard -- '*.cs')   # Reading/ReadingContentViewModel.cs, Reading/ReadingDocumentBuilder.cs, Reading/ReadingSurface.cs, Reading/ReadingSemantics.cs, Reading/ReadingDiagramElement.cs, ReadingDiagramTests.cs, ReadingFocusTests.cs, ShellAccessibilityTests.ReadingDiagram.cs (new)
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs
```

`ReadingSurface.cs` has 7 CRs — check before and after. CI: Windows lane
with the shell gate; codex review (size M–L, §4 rule 5). AT: NVDA full
pass (the PR stays a draft until recorded): open a note with a cold
diagram — the first paragraph is readable at once; arrowing reaches
"Diagram, rendering, group"; after the render, arrowing back reads the
description, and nothing was announced in between; a broken fence reads
"Diagram could not be rendered …"; Ctrl+Shift+E then F6 lands on the
surface without waiting for the diagram.

## Acceptance

- [ ] Nine new facts pass, seen failing first (every gate wait's result asserted, released in `finally`); adapted facts green; mutations run.
- [ ] New FlaUI journey green; the text waits shortened.
- [ ] Contract 40 R-10/AR-60 records appended; `w_c_matrix.md:23` and the parity note updated.
- [ ] Windows lane green; codex review done; format gate clean.
- [ ] NVDA pass recorded in the PR body.
- [ ] PR body: `Closes #1317`. Not delivered: a progressive MATH stage (math still fetches before stage 1 publishes; it is core-rendered and fast) and an announcement for late diagrams (WD11: silent).

## Brief

Read `Reading/ReadingContentViewModel.cs:52, 225, 912-968, 1156,
1517-1620, 1757-1790`, `Reading/ReadingDocumentBuilder.cs:893-1000`,
`Reading/ReadingSurface.cs:312-330, 378-420, 500-535, 700-720`,
`Reading/ReadingDiagramElement.cs:21-100`, `ReadingSemantics.cs:228-234`,
`EditorLandingSlot.cs:102-111, 354-410`, contract 40 R-10
(`docs/plans/40_nvda_matrix_remediation_contracts.md:33`) and the facts at
`ReadingDiagramTests.cs:26-382`, `ReadingFocusTests.cs:93, 780, 892, 1286`.
Pending decision WD11 (recommended: the partial projection is applied, a
late diagram is silent, the placeholder is named "Diagram, rendering";
under the other answers: a held landing waits through the diagram stage
with `RefreshInFlight` kept true, and an announced late diagram needs a
new core `A11yEvent` with corpus, mirrors and the Swift lane). Build: in
`ReadingContentViewModel`, split the fetch — stage 1 (code, math, blocks,
inlines, embeds; `FetchResult` loses its diagram fields) publishes through
`Publish`/`FinishPublish` as today with `RefreshInFlight` false
afterwards; stage 2 on the same worker, under stage 1's generation and
cancel token, calls `GetDiagramBlocks`, decodes each SVG there
(`DecodeImage`, result `Freeze()`d, under the existing budgets) and posts
`ReadingSurface.CompleteDiagrams(IReadOnlyList<(string key, DiagramOutcome outcome)>)`
(new), dropping its result if retired (AR-60). In
`ReadingDocumentBuilder`, a Mermaid fence with no artifact emits a
placeholder `ReadingDiagramElement("Diagram, rendering", source)` marked
by a new `ReadingSemantics.MarkDiagramPending(paragraph, key)`. In
`ReadingDiagramElement`, add `Complete(description, visual)` (content,
`Description`, Name, HelpText, a `NameProperty` property-change on the
same peer) and `Fail(header, reason)` (the `:899-912` failure shape);
the element and its paragraph are never swapped, so focus, caret,
selection and WB-11's caches survive untouched. Facts, written first and
seen failing (a `DiagramStageGateForTests` whose `Wait(10 s)` result is
asserted and which is `Set()` in `finally`):
`ReadingDiagramTests.TextAndMathAreReadableWhileADiagramRenderIsParked`
(gate held: paragraphs and math present, no `ReadingLoadingNotice`,
placeholder group named "Diagram, rendering", `IsLoading` false);
`ReadingDiagramTests.TheDiagramCompletesItsPlaceholderWithoutMovingTheReader`
(same element instance, same index, caret and focused link unchanged,
one `ApplyBuiltDocument`);
`ReadingDiagramTests.ADiagramLandingWhileTheReaderIsOnThePlaceholderKeepsFocus`
(focus on the placeholder stays, same peer object, Name changed with a
property-change event);
`ReadingDiagramTests.ADiagramLandingUnderASelectionKeepsTheSelection`
(selection offsets unchanged);
`ReadingDiagramTests.AFailedDiagramRenderLeavesTheRestReadableAndNamesTheFailure`
(failure header, reason and source in the placeholder, other blocks
intact); `ReadingDiagramTests.ALateDiagramIsSilent` (no `A11yEvent`);
`ReadingDiagramTests.ASupersededDiagramStageTouchesNothing` (a refresh
during the gate retires the stage); `ReadingDiagramTests.SvgIsDecodedOffTheDispatcher`
(`DiagramRenderProbeForTests` sees a non-dispatcher thread);
`ReadingFocusTests.AHeldLandingSeatsOnThePartialProjection` (seats before
the gate releases); FlaUI
`ShellAccessibilityTests.ReadingDiagram_TextIsReadableBeforeTheDiagramRenders`
(a cold Mermaid note: math and first paragraph appear before any diagram
image; placeholder named; image within 300 s); the existing
`ReadingDiagramTests.cs:26, 73, 215, 259, 328` adapted, and the text waits
at `ShellAccessibilityTests.cs:1483, 1813, 1832` shortened. Mutations:
single-stage fetch back; re-apply the whole document; swap the paragraph;
decode in `Build`. Registers: append the R-10 record and an AR-60 note to
`docs/plans/40_nvda_matrix_remediation_contracts.md`; amend
`docs/plans/18_windows_port/w_c_matrix.md:23`; note progressive
projection on `docs/plans/18_windows_port/parity_matrix.md:272`. Not
delivered: a progressive math stage; an announcement for late diagrams.
Done when the Windows lane with the shell gate is green, the codex
review (size M–L) is done, the format gate is clean with
`ReadingSurface.cs`'s CR count unchanged, the mutation run is recorded,
and the NVDA pass (text readable at once; "Diagram, rendering, group";
description read after the render with nothing announced in between; a
broken fence reads "Diagram could not be rendered …"; F6 lands without
waiting) is in the PR body with `Closes #1317`. Commit: `feat(windows):
the reading view projects text first and lets diagrams arrive in place`.
