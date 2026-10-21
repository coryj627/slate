# WB-20 — The reading view shows text, code, math and embeds first; diagrams arrive in place, silently, without moving the reader

Issues: Closes #1317. Size M–L. Lanes: windows + FlaUI (shell gate). AT: NVDA (full pass). Wave: W7. Depends on: WB-11 (first on `Reading/ReadingSurface.cs` and `ReadingDocumentBuilder.cs`; WB-21b follows, §6). Parallel with: WB-19, WB-21. Decision: WD11 (pending; recommended "the first partial projection is applied; a late diagram is silent; its placeholder is named 'Diagram, rendering'").

## Goal

Today the reading view is all or nothing: the fetch renders every Mermaid
diagram before it publishes anything, and the build decodes each SVG on
the dispatcher, so a note with one diagram shows "Loading reading view…"
until a cold render finishes — seconds, during which a screen-reader user
has nothing to read (and #1304's journeys waited 10–30 s for a math
element parked behind a diagram). After this chunk the projection publishes
in two stages: text, code, math and embeds first, each diagram as its
source text in the note plus a named placeholder element; then each diagram
completes its placeholder in place — the same focusable element, its
paragraph's text edited around it — decoded off the dispatcher, keeping the
caret, the selection, the focused link and a held landing. A late diagram
says nothing; a failed one puts its failure header, reason and source in
the note's text (where caret reading and the TextPattern reach them) and
the rest of the note stays readable.

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

- **Where the text lives (the constraint the shapes obey).** WPF's text
  range does not include what an `InlineUIContainer`'s child displays: a
  `TextBlock` inside the embedded element is absent from the parent
  `TextRange` and from the surface's TextPattern (confirmed by a read-only
  WPF probe in codex round 6; main's own comment: "WPF text ranges blank
  embedded objects", `ReadingDocumentBuilder.cs:583`). Main relies on this
  — the diagram failure shape keeps header, reason and source as paragraph
  `Run`s beside the zero-size element (`:965-989`), the unmatched shape is
  a source `Run` (`:871-879`), and the math coverage-gap shape is a source
  `Run` plus a zero-size focusable element (`:575-596`). So every text a
  reader must reach by caret or through the TextPattern — the pending
  source, the failure header,
  reason and source — is a `Run` in the diagram's `Paragraph`, never content
  of `ReadingDiagramElement`; and the focusable placeholder element itself
  is never swapped (round 5): it may hold keyboard focus, the caret or a
  selection anchor when the render lands.
- **Two stages, one generation.** `FetchResult` loses its diagram fields;
  stage 1 fetches code, math, blocks, inlines and embeds as today and
  publishes. It also carries the diagrams the last completed stage 2 landed
  for this path — `_landedDiagrams` (new, `IReadOnlyList<LandedDiagram>`,
  `internal sealed record LandedDiagram(DiagramBlock Artifact,
  DiagramOutcome Outcome)`, the visual already decoded and frozen; only
  `Rendered` and `Failed` outcomes are kept, since `Unmatched` has no
  artifact, so an unmatched fence is retried by the next refresh) — and the
  builder's diagram arm matches them by its existing rule (`:851-869`: byte
  containment plus LF-normalized interior equality). A matched fence is
  built complete, in exactly main's shape for that outcome and with no
  decode on the dispatcher, so a re-projection over unchanged fences (a task
  toggle, a dependency refresh, a rebind) never reverts a rendered diagram
  to its source, and `ApplyBuiltDocument`'s caret preservation by symbol
  offset (`ReadingSurface.cs:421-451`) stays exact, as on main. Only an
  unmatched fence is **pending**: the arm emits main's unmatched shape —
  `MonospaceParagraph(fallbackSource)` (`:1033-1043`, the source as one
  in-range `Run`), `MarkDiagramBlock(paragraph, fallbackSource)` as `:878`
  does — plus, exactly as the math coverage-gap shape (`:585-595`) adds its
  element, the placeholder `ReadingDiagramElement("Diagram, rendering",
  source)` (WD11) appended in its own `InlineUIContainer` with no content
  (zero-size, as the failure shape's element is), and a new
  `ReadingSemantics.MarkDiagramPending(Paragraph paragraph, string key)` /
  `PendingDiagramKeyOf(Paragraph paragraph)` (key: the fence's
  `ReadingBlock.ByteStart` in decimal, unique within one projection). The
  builder's diagram input becomes `IReadOnlyList<LandedDiagram>` plus a new
  `bool diagramsPending` argument that only a stage-1 build passes true;
  the existing `Build` overloads that take `DiagramBlock`s (tests only, e.g.
  `ReadingDiagramTests.cs:328`) resolve them through
  `ResolveDiagramOutcome` first and pass false, so an unmatched fence there
  keeps main's unmatched shape. Diagram fences reach the arm only as
  top-level blocks (`NonListBlock`, called only at `:213-215`), never inside
  a list or an embed card. Stage 2 runs on the same worker after stage 1's `FinishPublish`, only
  when a fence is pending: `GetDiagramBlocks`, then for each pending fence
  its outcome from a new `internal static DiagramOutcome
  ReadingDocumentBuilder.ResolveDiagramOutcome(DiagramBlock? matched,
  string fallbackSource, ref int remainingDiagramRenderBytes)` — today's
  status switch (`:894-938`) moved out of `DiagramBlockElement`, with
  `EditorInteractionCoordinator.DecodeImage` called there on the worker and
  the visual `Freeze()`d before it crosses to the dispatcher, under the same
  per-diagram cap and the projection-wide pool (`:918-927`, `:259`'s pool
  fact; stage 2 starts from what stage 1's build left of
  `ReadingListBuildContext.RemainingDiagramRenderBytes`). New `internal
  abstract record DiagramOutcome` with `Rendered(string Description, string
  Landing, ImageSource Visual)`, `Failed(string Description, string
  Landing, string Header, string Reason)` and `Unmatched()` (no artifact
  matches: the live buffer drifted from the saved file, or the stage's fetch
  degraded — `DiagramFaultForTests`, `ReadingContentViewModel.cs:841`, is
  now read in stage 2).
- **Shared shapes.** New `ReadingDocumentBuilder.ApplyRenderedShape(Paragraph,
  ReadingDiagramElement, DiagramOutcome.Rendered)` and
  `ApplyFailureShape(Paragraph, ReadingDiagramElement, DiagramOutcome.Failed,
  string source)` produce main's two shapes (`:940-962` and `:965-989`)
  around a given element; `DiagramBlockElement` calls them with a new
  paragraph and element, and the in-place completion calls them with the
  pending paragraph and its kept element, so a completed paragraph and a
  fresh complete build cannot drift.
- **Completion in place.** The model raises a new `event Action<long,
  IReadOnlyList<(string Key, DiagramOutcome Outcome)>>
  ReadingContentViewModel.DiagramsCompleted` (stage 1's generation, the
  outcomes) on the dispatcher and records `_landedDiagrams`; a surface
  bound to it whose `_lastMerged` is still the document stage 1 published
  calls new `internal void ReadingSurface.CompleteDiagrams(IReadOnlyList<(string
  Key, DiagramOutcome Outcome)> outcomes)`; any other surface ignores it,
  and the model's next build uses `_landedDiagrams`. For each outcome,
  `CompleteDiagrams` finds the pending paragraph P by `PendingDiagramKeyOf`
  and its last inline, the `InlineUIContainer` C holding the placeholder E,
  and inside one `BeginChange()`/`EndChange()` (`IsUndoEnabled` is already
  false, `ReadingSurface.cs:42`):
  1. *Anchors recorded.* For each of `CaretPosition`, `Selection.Start`
     and `Selection.End`: before or after P (left alone — the edit is
     confined to P and WPF's `TextPointer`s are live, so they stay on the
     same character; only their symbol offset from `ContentStart` shifts,
     by the edit's symbol delta, when they are after P), inside P's source
     text at character offset k, or at C's `ElementStart`/`ElementEnd`.
  2. *The shape.* `Rendered`: the source `Run` is removed and
     `ApplyRenderedShape(P, E, outcome)` sets main's rendered paragraph
     (`TextAlignment.Center`; padding, background and font family cleared)
     and `E.Complete(description, visual)`. `Failed`:
     `ApplyFailureShape(P, E, outcome, source)` builds main's failure shape
     around the kept C — `Run(header)` SemiBold, `LineBreak`, then
     `Run(reason)` and `LineBreak` when the reason is non-empty, then the
     source `Run` with the monospace font on the run and the paragraph's own
     font family cleared (`:968-985`; padding 8 and the raised background
     stay) — and
     `E.Complete(description, null)` (zero-size, named by the description,
     as `:987-988`). `Unmatched`: P already is main's unmatched shape plus
     C; C is removed when `E.IsKeyboardFocusWithin` is false (main's shape
     exactly, so `DiagramFetchFailureDegradesToSourceInRange` and
     `UnsavedDiagramEditsNeverShowTheStaleDiagram` keep asserting no
     element), otherwise kept, with E renamed to the source (the landing
     `:878` speaks) until the next projection rebuilds P. Every outcome
     re-applies `MarkDiagramBlock(P, landing)` (the source for `Unmatched`)
     and clears the pending mark.
  3. *Anchors restored.* Inside the source at k: `Rendered` → C's
     `ElementStart` for the caret and `Selection.Start`, C's `ElementEnd`
     for `Selection.End` (the diagram replaced the source, so a selection
     that reached into the source now covers the diagram); `Failed` and
     `Unmatched` → offset k of the source `Run`, the same character. At C's
     edges: the same edge (C is the same object), or the source `Run`'s end
     if C was removed. A collapsed selection is restored through
     `CaretPosition`, a range through `Selection.Select(start, end)`.
  4. *What else changes, and what does not.* `_landmarks` is re-collected
     (`ReadingDocumentBuilder.CollectLandmarks(Document)`,
     `_navigator?.SetLandmarks`) because each `ReadingLandmark` copies its
     text when collected (`ReadingDocumentBuilder.cs:1768-1773`) and the
     landing changed; the caret's Enter path reads
     `DiagramDescriptionOf(paragraph)` live (`ReadingSurface.cs:839-851`)
     and needs only the re-mark. WB-11's caches need nothing: P is a direct
     child of the document (diagram blocks never sit in a list —
     `ReadingDocumentBuilder.cs:213-215`), so no list or list-item peer
     reads its text; the surface peer's cached structural list holds E's
     peer, the same object; and WPF's own text-range children still hold
     the same C. If P is ever not a direct child of the document,
     `CompleteDiagrams` calls WB-11's `InvalidateStructuralPeers()` for that
     completion instead. No `StructureChanged` is raised; E's `NameProperty`
     change and whatever `TextPatternOnTextChanged` WPF's text adaptor
     raises for the Run edit are the only automation events, and neither is
     an `A11yEvent` (WD11: silent).

  `ReadingDiagramElement.Complete(string description, ImageSource? visual)`
  (new) sets `Description` (get-only today, `ReadingDiagramElement.cs:42`;
  it gains a private setter) and `AutomationProperties.Name`, sets `Content =
  new ReadingDiagramImage { … }` (the `:944-955` properties) when `visual` is
  non-null, leaves HelpText (the source) and the tooltip alone, and raises
  `RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, old,
  new)` on its peer when one exists. The element and its peer stay the same
  objects, so focus on E survives and a UIA client's held reference stays
  valid. The memo (`_memo`) gains the diagram digest when stage 2 lands so a
  later rebind memo-hits the complete projection.
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
- **Test seams (new).** On `ReadingContentViewModel`: `internal
  ManualResetEventSlim? DiagramStageGateForTests { get; set; }` (stage 2
  waits on it, 10 s, before `GetDiagramBlocks`), `internal bool
  DiagramStageGateTimedOutForTests { get; private set; }` (set when that
  wait returns false; every fact asserts it false), `internal int
  DiagramStageRunsForTests { get; private set; }`; on `ReadingSurface`:
  `internal static Action? ApplyProbeForTests` (invoked at the top of
  `ApplyBuiltDocument`).
- Not changed: `MaximumRenderedBlocks`, the streamed-chunk path's shape
  (placeholders ride in chunks like any block), the diagram element's
  peer and Enter-speaks-the-block (`:168`), the embeds' decode budget.

## Tests

Facts fail first. "The surface TextPattern" below is the in-process
provider every reader uses: `UIElementAutomationPeer.CreatePeerForElement(surface)
.GetPattern(PatternInterface.Text)` as `ITextProvider`, read with
`DocumentRange.GetText(-1)` (the `ReadingViewTests.cs:355-359` pattern) —
never only a child peer's `Name`. Every fact that holds the stage sets
`DiagramStageGateForTests` in `finally` and asserts
`DiagramStageGateTimedOutForTests` is false.

- `ReadingDiagramTests.TextAndMathAreReadableWhileADiagramRenderIsParked`
  — `DiagramStageGateForTests` held (a `ManualResetEventSlim`; the
  existing `DiagramFaultForTests` stays and is read in stage 2): after
  stage 1 the surface has the paragraphs and the math element, no
  `ReadingLoadingNotice`, `IsLoading` false; the surface TextPattern's text
  contains the fence's source; the diagram paragraph holds one
  `ReadingDiagramElement` named "Diagram, rendering" with no `Content`.
  Mutations: restore the single-stage fetch → fails on the loading
  notice; put the source in the element's `Content` instead of a `Run` →
  the TextPattern lacks it → fails.
- `ReadingDiagramTests.TheDiagramCompletesItsPlaceholderWithoutMovingTheReader`
  — caret parked on a known character two paragraphs below, a link
  focused; release the gate: the rendered image is inside the SAME
  `ReadingDiagramElement` instance at the same block index; the character
  after the caret and its offset from the diagram paragraph's `ElementEnd`
  are unchanged; `Keyboard.FocusedElement` is unchanged; `ApplyBuiltDocument`
  ran once (`ApplyProbeForTests`); the surface TextPattern no longer
  contains the source, and the diagram paragraph's text, inline kinds and
  marks equal those of a fresh single-stage `ReadingDocumentBuilder.Build`
  of the same artifact (the shared-shape differential). Mutations:
  re-apply the whole document → the probe counts two; leave the source
  `Run` after `Rendered` → the differential fails.
- `ReadingDiagramTests.ADiagramLandingWhileTheReaderIsOnThePlaceholderKeepsFocus`
  — Tab onto the placeholder element (it has focus) and hold a reference
  to its automation peer; release the gate: the same element still has
  focus, its `Name` is now the description, the held peer is the same
  object and a `NameProperty` property-change was raised. Mutation: swap
  the paragraph for a new one → focus drops to the surface → fails.
- `ReadingDiagramTests.ADiagramLandingUnderASelectionKeepsTheSelection`
  — a selection from the paragraph before the diagram to the paragraph
  after it; release the gate: `Selection.Start`'s offset is unchanged,
  `Selection.End` is on the same character, and `Selection.Text` starts
  and ends with the same text. Mutation: rebuild the diagram paragraph as
  a new `Paragraph` → the endpoints collapse → fails.
- `ReadingDiagramTests.AnchorsInsideThePendingSourceAreRemapped` — three
  cases on a held stage: (a) caret at source offset 5, `Rendered` → the
  caret is at the container's `ElementStart`; (b) a selection from the
  paragraph before into the source, `Rendered` → `Selection.End` is the
  container's `ElementEnd`, so the selection covers the diagram element
  (`Selection.Start.CompareTo(container.ElementStart) <= 0`);
  (c) caret at source offset 5 of an unsupported-dialect fence
  (`weirdDiagram\nstuff`, the `:73` fixture), `Failed` → the caret is at
  offset 5 of the failure shape's source `Run` and the character after it
  is unchanged. Mutation: skip the remap and let WPF collapse the pointers
  → (a)–(c) land at the paragraph's start → fails.
- `ReadingDiagramTests.AFailedDiagramRenderLeavesTheRestReadableAndNamesTheFailure`
  — the unsupported-dialect fence: through the surface TextPattern the
  text contains "Diagram dialect not supported", core's reason and
  `weirdDiagram`; the same element instance is named by core's
  description and has no `Content`; every other block is intact; the
  paragraph equals a fresh single-stage build (the differential).
  Mutation: render header, reason and source in a `TextBlock` inside the
  element → the TextPattern lacks them while the element's name still
  passes → fails.
- `ReadingDiagramTests.ANoMatchCompletionRestoresTheUnmatchedShapeUnlessThePlaceholderHasFocus`
  — `DiagramFaultForTests` throws `VaultException.Io`: unfocused, after
  stage 2 no `ReadingDiagramElement` remains (`:215`'s assertion), the
  TextPattern holds the source, one Diagram landmark; with the placeholder
  focused, it stays, keeps focus and is named the source. Mutation: always
  remove the container → the focused case loses focus → fails.
- `ReadingDiagramTests.AnUnchangedDiagramStaysCompleteAcrossARefresh` —
  after stage 2 lands, a task toggle in the same note re-projects: the
  stage-1 document already holds the rendered element with no pending mark
  and no source `Run` in the diagram paragraph, `DiagramStageRunsForTests`
  does not grow, and a caret after the diagram is on the same character
  after the re-projection. Mutation: drop the carried `_landedDiagrams` →
  the stage-1 document shows the source and the caret lands elsewhere →
  fails.
- `ReadingDiagramTests.ALateDiagramIsSilent` — no `A11yEvent` across stage 2.
- `ReadingDiagramTests.ASupersededDiagramStageTouchesNothing` — refresh
  during the gate; the stale stage's result is dropped (AR-60).
- `ReadingDiagramTests.SvgIsDecodedOffTheDispatcher` —
  `DiagramRenderProbeForTests` records a thread that is not the
  dispatcher's. Mutation: decode in `Build` → fails.
- `ReadingFocusTests.AHeldLandingSeatsOnThePartialProjection` (WD11) —
  the landing seats before the gate is released; `:780`/`:892` keep passing.
- Existing `ReadingDiagramTests` `:26, :73, :122, :215, :259, :328`
  adapted to the two-stage shape (`:122` and `:215` assert their final
  state after stage 2; `:259`'s pool spans both stages).
- FlaUI `ShellAccessibilityTests.ReadingDiagram_TextIsReadableBeforeTheDiagramRenders`
  — a note with a cold Mermaid fence and an unsupported-dialect fence
  (`weirdDiagram\nstuff`): the math element and the first paragraph appear
  before any diagram image; the placeholder group is named "Diagram,
  rendering"; the image arrives within the existing 300 s; then, through
  the cross-process TextPattern (`surface.Patterns.Text.Pattern.DocumentRange.GetText(-1)`),
  the text contains "Diagram dialect not supported" and `weirdDiagram`,
  no longer contains the Mermaid fence's source line, and still contains
  the paragraph after the diagram. The `:1483`, `:1813`, `:1832` waits for
  TEXT elements shrink to the ordinary budget (the diagram wait keeps
  300 s).

## Docs and registers

- Contract 40: append a record under R-10 — "a partial projection (text,
  code, math, embeds; diagrams pending) is applied; the diagram stage is
  not a refresh; a late diagram is silent; the placeholder is named
  'Diagram, rendering'" (WD11), and a note under AR-60 that the stage
  honours the cancellation.
- `w_c_matrix.md:23`: the Diagram blocks row gains the pending placeholder
  (name "Diagram, rendering", type "diagram", the source as text beside it)
  and the in-place completion (the same element; failure header, reason and
  source as text beside it, as today).
- `parity_matrix.md:272`: note "Windows projects progressively (#1317);
  mac unverified".

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb20'
function Assert-Ok([string] $What) { if ($LASTEXITCODE -ne 0) { throw "$What exited $LASTEXITCODE" } }
function Assert-Discovered([string[]] $Listed, [string[]] $Required) {
  $names = $Listed | ForEach-Object { ($_ -split '\(', 2)[0] }
  $missing = @($Required | Where-Object { $names -notcontains $_ })
  if ($missing.Count -gt 0) { throw "the filter does not select: $($missing -join ', ')" }
}
./apps/slate-windows/generate-bindings.ps1 -Locked; Assert-Ok 'generate-bindings'
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release; Assert-Ok 'dotnet build'   # and Error(s)=0
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
# RescanTests is ONE partial class (RescanTests.ReadingLanding.cs:27 declares `partial class RescanTests`); filter the class, not the file suffix
$filter = 'FullyQualifiedName~SlateWindows.Tests.ReadingDiagramTests|FullyQualifiedName~SlateWindows.Tests.ReadingFocusTests|FullyQualifiedName~SlateWindows.Tests.ReadingViewTests|FullyQualifiedName~SlateWindows.Tests.ReadingMathTests|FullyQualifiedName~SlateWindows.Tests.RescanTests|FullyQualifiedName~SlateWindows.Tests.ReadingDoctrineCensusTests'
$listed = @(dotnet test $tests --no-build --configuration Release --list-tests --filter $filter | Select-String '^\s{4}\S' | ForEach-Object { $_.Line.Trim() }); Assert-Ok 'unit discovery'
Assert-Discovered $listed @(
  'SlateWindows.Tests.ReadingDiagramTests.TextAndMathAreReadableWhileADiagramRenderIsParked',
  'SlateWindows.Tests.ReadingDiagramTests.TheDiagramCompletesItsPlaceholderWithoutMovingTheReader',
  'SlateWindows.Tests.ReadingDiagramTests.ADiagramLandingWhileTheReaderIsOnThePlaceholderKeepsFocus',
  'SlateWindows.Tests.ReadingDiagramTests.ADiagramLandingUnderASelectionKeepsTheSelection',
  'SlateWindows.Tests.ReadingDiagramTests.AnchorsInsideThePendingSourceAreRemapped',
  'SlateWindows.Tests.ReadingDiagramTests.AFailedDiagramRenderLeavesTheRestReadableAndNamesTheFailure',
  'SlateWindows.Tests.ReadingDiagramTests.ANoMatchCompletionRestoresTheUnmatchedShapeUnlessThePlaceholderHasFocus',
  'SlateWindows.Tests.ReadingDiagramTests.AnUnchangedDiagramStaysCompleteAcrossARefresh',
  'SlateWindows.Tests.ReadingDiagramTests.ALateDiagramIsSilent',
  'SlateWindows.Tests.ReadingDiagramTests.ASupersededDiagramStageTouchesNothing',
  'SlateWindows.Tests.ReadingDiagramTests.SvgIsDecodedOffTheDispatcher',
  'SlateWindows.Tests.ReadingFocusTests.AHeldLandingSeatsOnThePartialProjection',
  'SlateWindows.Tests.ReadingDiagramTests.DiagramFetchFailureDegradesToSourceInRange',
  'SlateWindows.Tests.ReadingDiagramTests.UnsavedDiagramEditsNeverShowTheStaleDiagram',
  'SlateWindows.Tests.RescanTests.AHeldReadingLandingSettlesWithTheRescansSilentReprojection',
  'SlateWindows.Tests.RescanTests.AFailedRescanReprojectionRefusesTheHeldLandingSilently',
  'SlateWindows.Tests.RescanTests.ACancelledRescanReprojectionStillSettlesTheHeldLanding',
  'SlateWindows.Tests.RescanTests.ARescanReloadOfTheReadingTabWithdrawsItsHeldLandingSilently')
dotnet test $tests --no-build --configuration Release --filter $filter; Assert-Ok 'unit tests'
$at = 'apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj'
$atFilter = 'FullyQualifiedName~ShellAccessibilityTests.ReadingDiagram_TextIsReadableBeforeTheDiagramRenders|FullyQualifiedName~ShellAccessibilityTests.ReadingIdentityContract_ExeWindowAndSurfaceIds_AreStable|FullyQualifiedName~ShellAccessibilityTests.ReadingTextPattern_RangeFromChildResolvesEveryCustomPeer|FullyQualifiedName~ShellAccessibilityTests.ReadingView_IsTheEditorStop'
$atListed = @(dotnet test $at --no-build --configuration Release --list-tests --filter $atFilter | Select-String '^\s{4}\S' | ForEach-Object { $_.Line.Trim() }); Assert-Ok 'FlaUI discovery'
Assert-Discovered $atListed @(
  'SlateWindows.AccessibilityTests.ShellAccessibilityTests.ReadingDiagram_TextIsReadableBeforeTheDiagramRenders',
  'SlateWindows.AccessibilityTests.ShellAccessibilityTests.ReadingIdentityContract_ExeWindowAndSurfaceIds_AreStable',
  'SlateWindows.AccessibilityTests.ShellAccessibilityTests.ReadingTextPattern_RangeFromChildResolvesEveryCustomPeer',
  'SlateWindows.AccessibilityTests.ShellAccessibilityTests.ReadingView_IsTheEditorStop')
dotnet test $at --no-build --configuration Release --filter $atFilter; Assert-Ok 'FlaUI journeys'   # NVDA closed
$cs = @(git diff --name-only origin/main -- '*.cs') + @(git ls-files --others --exclude-standard -- '*.cs')   # Reading/ReadingContentViewModel.cs, Reading/ReadingDocumentBuilder.cs, Reading/ReadingSurface.cs, Reading/ReadingSemantics.cs, Reading/ReadingDiagramElement.cs, ReadingDiagramTests.cs, ReadingFocusTests.cs, ShellAccessibilityTests.ReadingDiagram.cs (new)
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs; Assert-Ok 'dotnet format'
```

`ReadingSurface.cs` has 7 CRs — check before and after. CI: Windows lane
with the shell gate; codex review (size M–L, §4 rule 5). AT: NVDA full
pass (the PR stays a draft until recorded): open a note with a cold
diagram — the first paragraph is readable at once; arrowing over the
pending diagram reads its source lines and reaches "Diagram, rendering,
group"; after the render, arrowing back reads the description, and
nothing was announced in between; a broken fence reads "Diagram could not
be rendered …" (an unsupported one "Diagram dialect not supported …"),
then its reason and source, line by line; Ctrl+Shift+E then F6 lands on
the surface without waiting for the diagram.

## Acceptance

- [ ] Twelve new facts pass, seen failing first (every gate wait's result asserted, released in `finally`); the pending source and the failure header, reason and source asserted through the surface TextPattern, not only a peer's name; adapted facts green; mutations run.
- [ ] Both discovery checks find every required fact by fully qualified name (they throw otherwise) and every Verification command exits 0.
- [ ] New FlaUI journey green (its TextPattern assertions included); the text waits shortened.
- [ ] Contract 40 R-10/AR-60 records appended; `w_c_matrix.md:23` and the parity note updated.
- [ ] Windows lane green; codex review done; format gate clean.
- [ ] NVDA pass recorded in the PR body.
- [ ] PR body: `Closes #1317`. Not delivered: a progressive MATH stage (math still fetches before stage 1 publishes; it is core-rendered and fast) and an announcement for late diagrams (WD11: silent).

## Brief

Read `Reading/ReadingContentViewModel.cs:52, 225, 841, 912-968, 1156,
1517-1620, 1757-1790`, `Reading/ReadingDocumentBuilder.cs:23-70, 213-215,
575-596, 845-1043, 1768-1773`, `Reading/ReadingSurface.cs:42, 312-330,
357-367, 378-451, 500-535, 696-720, 839-851`,
`Reading/ReadingDiagramElement.cs:21-100`, `ReadingSemantics.cs:228-240`,
`EditorLandingSlot.cs:102-111, 354-410`, contract 40 R-10
(`docs/plans/40_nvda_matrix_remediation_contracts.md:33`), the facts at
`ReadingDiagramTests.cs:26-382`, `ReadingFocusTests.cs:93, 780, 892, 1286`,
and the TextPattern read at `ReadingViewTests.cs:355-359`. Pending decision
WD11 (recommended: the partial projection is applied, a late diagram is
silent, the placeholder is named "Diagram, rendering"; under the other
answers: a held landing waits through the diagram stage with
`RefreshInFlight` kept true, and an announced late diagram needs a new core
`A11yEvent` with corpus, mirrors and the Swift lane). The rule every shape
obeys: WPF's text range excludes what an `InlineUIContainer`'s child shows
(main's comment, `ReadingDocumentBuilder.cs:583`; codex's round-6 probe),
so the pending source and the failure header, reason and source are `Run`s
in the diagram's paragraph, never content of `ReadingDiagramElement`, and
the focusable placeholder element is never swapped. Build: in
`ReadingContentViewModel`, split the fetch — stage 1 (code, math, blocks,
inlines, embeds; `FetchResult` loses its diagram fields) publishes through
`Publish`/`FinishPublish` as today with `RefreshInFlight` false afterwards,
carrying `_landedDiagrams` (new, `IReadOnlyList<LandedDiagram>`, `record
LandedDiagram(DiagramBlock Artifact, DiagramOutcome Outcome)`) so a fence
matched by the builder's existing rule (`:851-869`) builds complete with no
dispatcher decode and the caret's symbol-offset preservation stays exact;
stage 2, only when a fence is pending, on the same worker after
`FinishPublish`, under stage 1's generation and cancel token, waits on
`DiagramStageGateForTests` (new `ManualResetEventSlim?`, 10 s, recording
`DiagramStageGateTimedOutForTests`), reads `DiagramFaultForTests`, calls
`GetDiagramBlocks`, resolves each pending fence through new `static
DiagramOutcome ReadingDocumentBuilder.ResolveDiagramOutcome(DiagramBlock?
matched, string fallbackSource, ref int remainingDiagramRenderBytes)`
(today's `:894-938` switch, `DecodeImage` on the worker, the visual
`Freeze()`d, the per-diagram cap and the pool stage 1 left), records
`_landedDiagrams`, and raises new `event Action<long, IReadOnlyList<(string
Key, DiagramOutcome Outcome)>> DiagramsCompleted`, dropping its result if
retired (AR-60). New `abstract record DiagramOutcome` with
`Rendered(Description, Landing, Visual)`, `Failed(Description, Landing,
Header, Reason)`, `Unmatched()`. In `ReadingDocumentBuilder` (diagram
input now `IReadOnlyList<LandedDiagram>` plus a new `bool diagramsPending`
that only stage 1 passes true; the test-only `DiagramBlock` overloads
resolve through `ResolveDiagramOutcome` and pass false, keeping main's
unmatched shape), an unmatched Mermaid fence in a stage-1 build emits the
pending shape: `MonospaceParagraph(fallbackSource)`
with `MarkDiagramBlock(p, fallbackSource)` (main's unmatched shape,
`:871-879`) plus, as the math coverage-gap shape adds its element
(`:585-595`), a zero-size `ReadingDiagramElement("Diagram, rendering",
source)` in its own `InlineUIContainer`, marked by new
`ReadingSemantics.MarkDiagramPending(Paragraph, string key)` /
`PendingDiagramKeyOf(Paragraph)` (key: the block's `ByteStart`); main's two
complete shapes move into new `ApplyRenderedShape(Paragraph,
ReadingDiagramElement, DiagramOutcome.Rendered)` and
`ApplyFailureShape(Paragraph, ReadingDiagramElement, DiagramOutcome.Failed,
string source)`, used by both the fresh build and the completion. In
`ReadingSurface`, new `CompleteDiagrams(IReadOnlyList<(string Key,
DiagramOutcome Outcome)>)`, run only when `_lastMerged` is still stage 1's
document, inside one `BeginChange()`/`EndChange()`: record the caret and
selection ends (before/after the paragraph: left alone, live pointers;
inside the source at offset k; at the container's edges); apply the shape
(`Rendered`: source `Run` removed, `ApplyRenderedShape`, `E.Complete(d,
visual)`; `Failed`: `ApplyFailureShape` around the kept container,
`E.Complete(d, null)`; `Unmatched`: the container removed unless the
element has keyboard focus, else kept and renamed to the source); restore
(source offset k → the container's `ElementStart` for the caret and
`Selection.Start`, `ElementEnd` for `Selection.End` when `Rendered`; offset
k of the source `Run` when `Failed`/`Unmatched`); re-mark
`MarkDiagramBlock` with the outcome's landing, clear the pending mark, and
re-collect `_landmarks` (they copy their text, `:1768-1773`); WB-11's
caches need nothing (the paragraph is a direct child of the document,
`:213-215`; the element's peer is the same object) — call
`InvalidateStructuralPeers()` only if the paragraph is not a direct child.
In `ReadingDiagramElement`, add `Complete(string description, ImageSource?
visual)` (`Description` gains a private setter; Name; `Content` = a
`ReadingDiagramImage` when `visual` is non-null; HelpText and tooltip
kept; a `NameProperty` property-change on the same peer). Seam:
`ReadingSurface.ApplyProbeForTests` (new `static Action?`, top of
`ApplyBuiltDocument`), `DiagramStageRunsForTests` (new). Facts, written
first and seen failing, reading text through the surface TextPattern
(`CreatePeerForElement(surface).GetPattern(PatternInterface.Text)`,
`DocumentRange.GetText(-1)`):
`ReadingDiagramTests.TextAndMathAreReadableWhileADiagramRenderIsParked`
(gate held: paragraphs and math present, no `ReadingLoadingNotice`,
`IsLoading` false, the TextPattern holds the source, the element named
"Diagram, rendering");
`ReadingDiagramTests.TheDiagramCompletesItsPlaceholderWithoutMovingTheReader`
(same element instance and index, the caret on the same character, the
focused link unchanged, one `ApplyBuiltDocument`, the source gone from the
TextPattern, the paragraph equal to a fresh single-stage build);
`ReadingDiagramTests.ADiagramLandingWhileTheReaderIsOnThePlaceholderKeepsFocus`
(focus on the placeholder stays, same peer object, Name changed with a
property-change event);
`ReadingDiagramTests.ADiagramLandingUnderASelectionKeepsTheSelection`
(a selection spanning the diagram keeps its start offset and its end
character); `ReadingDiagramTests.AnchorsInsideThePendingSourceAreRemapped`
(caret in the source → `ElementStart` on `Rendered`; a selection into the
source → ends at `ElementEnd`; caret in the source → the same source
offset on `Failed`);
`ReadingDiagramTests.AFailedDiagramRenderLeavesTheRestReadableAndNamesTheFailure`
(`weirdDiagram\nstuff`: the TextPattern holds "Diagram dialect not
supported", the reason and the source; the same element named by the
description; the paragraph equal to a fresh build);
`ReadingDiagramTests.ANoMatchCompletionRestoresTheUnmatchedShapeUnlessThePlaceholderHasFocus`;
`ReadingDiagramTests.AnUnchangedDiagramStaysCompleteAcrossARefresh` (a task
toggle re-projects with the rendered element already in stage 1, no new
stage-2 run, the caret on the same character);
`ReadingDiagramTests.ALateDiagramIsSilent` (no `A11yEvent`);
`ReadingDiagramTests.ASupersededDiagramStageTouchesNothing` (a refresh
during the gate retires the stage); `ReadingDiagramTests.SvgIsDecodedOffTheDispatcher`
(`DiagramRenderProbeForTests` sees a non-dispatcher thread);
`ReadingFocusTests.AHeldLandingSeatsOnThePartialProjection` (seats before
the gate releases); FlaUI
`ShellAccessibilityTests.ReadingDiagram_TextIsReadableBeforeTheDiagramRenders`
(a cold Mermaid fence and an unsupported one: math and first paragraph
appear before any diagram image; placeholder named; image within 300 s;
the cross-process TextPattern then holds the unsupported fence's header and
source, not the Mermaid source, and still the paragraph after it); the
existing `ReadingDiagramTests.cs:26, 73, 122, 215, 259, 328` adapted, and
the text waits at `ShellAccessibilityTests.cs:1483, 1813, 1832` shortened.
Mutations: single-stage fetch back; the source in the element's `Content`;
re-apply the whole document; the source `Run` left after `Rendered`; a new
paragraph; no anchor remap; failure text in a `TextBlock`; the container
always removed; the carried artifacts dropped; decode in `Build`. Local
loop, each native command followed by a throw on a nonzero
`$LASTEXITCODE`: bindings regenerated
(`$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb20'`), Release build,
`dotnet test --list-tests` over `FullyQualifiedName~SlateWindows.Tests.ReadingDiagramTests
| …ReadingFocusTests | …ReadingViewTests | …ReadingMathTests |
…RescanTests | …ReadingDoctrineCensusTests` throwing unless the twelve
facts, `DiagramFetchFailureDegradesToSourceInRange`,
`UnsavedDiagramEditsNeverShowTheStaleDiagram` and the four
`RescanTests.ReadingLanding.cs` landing facts are listed by fully qualified
name; the run; the same for the accessibility project's four reading
journeys (NVDA closed); the format gate over the touched `.cs` files.
Registers: append the R-10 record and an AR-60 note to
`docs/plans/40_nvda_matrix_remediation_contracts.md`; amend
`docs/plans/18_windows_port/w_c_matrix.md:23` (the pending placeholder with
its source as text beside it; in-place completion); note progressive
projection on `docs/plans/18_windows_port/parity_matrix.md:272`. Not
delivered: a progressive math stage; an announcement for late diagrams.
Done when the Windows lane with the shell gate is green, the codex
review (size M–L) is done, the format gate is clean with
`ReadingSurface.cs`'s CR count unchanged, the mutation run is recorded,
and the NVDA pass (text readable at once; the pending source read by
arrowing and "Diagram, rendering, group"; description read after the
render with nothing announced in between; a broken fence reads "Diagram
could not be rendered …" then its reason and source; F6 lands without
waiting) is in the PR body with `Closes #1317`. Commit: `feat(windows):
the reading view projects text first and lets diagrams arrive in place`.
