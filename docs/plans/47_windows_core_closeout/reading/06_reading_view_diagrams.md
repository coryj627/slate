# Plan 47 reading — cluster 06: the Windows reading view, diagrams and SVG

Read at the worktree `<worktree>`, `main` at 1ec9bf31. Issues read in full: #1708, #1775, #1635, #1610, #1631 (none has comments). Also read: #1595 with its spike record and field pass (closed), #1581 body and comments (closed), PR #1908's body, the XB-9, XB-10a and XB-10b specs, and the pinned sources of mermaid-rs-renderer 0.3.1 and pulldown-cmark 0.13.6 in the local cargo registry.

Nothing was built or run. Every statement about what NVDA or Narrator *says* is either quoted from a recorded pass (named) or marked **unverified**. Statements about WPF's stock peers (CheckBox, Separator) are from WPF's documented behaviour, not from this repo's code, and are marked.

Paths below are relative to the worktree. `RDB` = `apps/slate-windows/src/SlateWindows/Reading/ReadingDocumentBuilder.cs`.

---

## #1708 — line reading speaks a diagram as only "grouping"

- **Verdict:** stands. No merge has touched it (`git log --grep 1708` is empty; the last change to `ReadingDiagramElement.cs` is #1707, the PR whose pass found it).

- **Current state:**
  - The element: `Reading/ReadingDiagramElement.cs:21` (`ContentControl`, `Focusable` `:26`, Name = description `:27`, HelpText = the whole source `:28-32`, ToolTip = 3-line/120-char preview `:33-37`). Peer `:120-135`: `ControlType.Group` (`:127-128`), localized type `"diagram"` (`:130`). The drawn image is `ReadingDiagramImage`, which has no peer (`:108-111`).
  - **Rendered** (`RDB:1028-1036` → `ApplyRenderedShape` `RDB:1193-1211`): the paragraph's only inline is the element's `InlineUIContainer`; `:1199-1205` removes every other inline. The text range holds no text.
  - **Pending** (`RDB:1052-1066`): a monospace source run, then a zero-size element named `"Diagram, rendering"` (`PendingDiagramName`, `RDB:1070`) as the last inline (`:1060-1061`).
  - **Failed** (`ApplyFailureShape`, `RDB:1219-1257`): header run, line break, reason run, line break, source run, then the zero-size element carrying the description. All text is in range; the description is not.
  - **Unmatched, not pending** (`RDB:1052-1053`, `:1066`): the source alone, no element. Nothing to fix.
  - In-place completion: `ReadingSurface.CompleteDiagrams` (`Reading/ReadingSurface.cs:493-613`) finds the paragraph by `Inlines.LastInline is InlineUIContainer { Child: ReadingDiagramElement }` (`:506-509`) and takes `Inlines.FirstInline as Run` as the source (`:513`); caret and selection anchors are recorded and restored around the edit (`:615-664`).
  - Enter at the caret re-reads the description (`ReadingSurface.cs:1357-1369`). Ctrl+Alt+D lands with "{description}, diagram." (W-C matrix row, `docs/plans/18_windows_port/w_c_matrix.md:24`).
  - The gate the issue names exists: `tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.cs:1728`; its diagram entry is `:1874-1876` (embedded, no range text asserted), order kinds `:1942`.
  - What NVDA says today is the issue's own record (NVDA 2026.2, commit 8f1f307e) and the 2026-09-22 agent pass (`docs/plans/18_windows_port/reports/nvda_agent_matrix_pass_2026-09-22.md:77`, `:136`, `:213`: "In caret reading a diagram is just 'grouping'"). I did not re-run it.
  - The issue's claim "this predates WB-20" is right: the rendered shape has had no text since W3-3 (#1058).
  - A core description of a *rendered* diagram is always one line ("Flowchart with 2 steps." and four siblings, `crates/slate-core/src/diagram.rs:426-458`). Only the unknown-kind description is multi-line ("Mermaid diagram, source:\n" plus a 160-char dump, `:459-470`), and an unknown kind never renders, so it only ever sits on a failed diagram's element.

- **Cause:** WPF's text range holds an embedded object as a blank; NVDA speaks an embedded object's role and not its Name during line reading (the #1595 spike, finding 2: "b fails… whatever the control type"). The rendered paragraph has nothing else on its line.

- **Fix shape:** XB-9's shape a1h, carried to the diagram's three states. Files: `RDB`, `Reading/ReadingDiagramElement.cs`, `Reading/ReadingSurface.cs`, `Reading/ReadingTextIndex.cs`, tests, the gate, `w_c_matrix.md:24`.
  1. **Rendered:** a visually suppressed run of the description (the existing `SpeechRun`, `RDB:685-689`, transparent at 0.01–0.1 font size) before the container. `ApplyRenderedShape` inserts it after clearing the paragraph.
  2. **The element must leave the run room on its line.** XB-9's review found this the hard way (`ReadingMathElement.LineReserve`, `ReadingMathElement.cs:84-101`): WPF measures an embedded element against the whole line, so a wide one drops to a line of its own and that line reads as a bare role. A diagram image usually fills the measure. `ReadingDiagramElement` needs the same `LineReserve` and `MeasureOverride`; the image is `Stretch.Uniform`/`DownOnly`, so it just scales into the narrower width. This does **not** carry over unchanged: the diagram element has no such code today.
  3. **Pending:** the placeholder's name as a suppressed run immediately before the container, after the source. It must not be glued to the last source line ("B --> CDiagram, rendering"); either a separator or its own line. Where exactly, and what both readers then say, is for a short spike (see the owner decision).
  4. **Failed:** see the owner decision. My recommendation leaves its text alone.
  5. `CompleteDiagrams` keeps working only if the container stays the last inline and the source stays the first run. The anchor code (`ReadingSurface.cs:615-664`) needs a case for a pointer inside the new run.
  6. **Find and copy.** `ReadingTextIndex` treats a math block's hidden speech as not searchable and searches its source instead (`ReadingTextIndex.cs:63`, `:77`, `:219-222`, the owner's call of 2026-10-26). A diagram's hidden description needs the twin rule, or find will match and highlight invisible text. `ReadingSurface.CopyTextOf` (`ReadingSurface.cs:1566-1600`) substitutes a math paragraph's fenced source for its speech; without a twin, a copy across a diagram pastes the description.
  7. HelpText stays as it is (the issue's "named as today"). See the owner decision on that.

- **Failing test first:** `tests/SlateWindows.Tests/ReadingDiagramTests.cs`, `LineReadingTextOfARenderedDiagramIsItsDescription`: build "```mermaid flowchart LR A --> B B --> C```", await the stage, assert the diagram paragraph's `TextRange(ContentStart, ContentEnd).Text.TrimEnd(' ')` equals `AutomationProperties.GetName(element)` ("Flowchart with 2 steps."). Today it is "". Then: `ARenderedDiagramSharesItsLineWithItsDescription` (hosted at 420 px, the twin of `ReadingMathTests.ALongFormulaSharesItsLineWithItsSpeech` `:679`: the run's line start equals the container's); `LineReadingTextOfAPendingDiagramCarriesItsPlaceholderName` (stage gate held); `ACompletedDiagramSwapsThePlaceholderNameForItsDescription`; `FindSkipsADiagramsHiddenDescription`; `CopyingAcrossADiagramCopiesItsFencedSource` (if decided). The six completion facts in `ReadingDiagramTests.Stage.cs` (`:228`, `:260`, `:283`, `:312`, `:346`, `:420`) must stay green. Plus the census (cluster section).

- **Size:** M.
- **Lanes:** windows (unit lane and the FlaUI shell gate).
- **Screen-reader work:** NVDA full (Down, Up, caret re-entry, Tab, for rendered, pending and failed), Narrator spot. A half-day spike first for the pending and failed placements, recorded on #1708 as #1595's was.
- **Changes text or speech a Mac user would hear:** no. The issue asks that the Mac reading view be checked for the same gap; that needs a Mac, so it is a ledger line, not work here.
- **Owner decision needed:**
  1. *Does line reading speak the description of a diagram that failed to render?* Recommended: no. Its header, reason and source are already text in range, the element's role trails the last text line rather than standing alone (from the code; **unverified** in speech), and an unknown-kind description would repeat 160 characters of source that is already there.
  2. *Where does a pending diagram's "Diagram, rendering" sit?* Recommended: settle by the spike; the done-when only says "where the element sits".
  3. *Find and copy over the hidden description.* Recommended: math's rule for both (find skips it; a copy carries the fenced source).
  4. *Does #1591's rule ("the source is spoken on request, never on focus") extend to diagrams?* Tab onto a diagram still reads its whole source as HelpText (up to 64 KiB; `ReadingDiagramElement.cs:28-32`; "Tab also reads the whole source", 2026-09-22 pass `:77`). Fixing it needs a source chord and a core sentence, as math's did (an FFI addition). Recommended: not in this chunk; give it a decision row of its own, because nobody has filed it.
- **Shares files with:** #1775 only if #1775 changes `RDB` (my recommended shape does not). None of #1635, #1610, #1631.

---

## #1775 — the reading view shows a callout's `[!note]` marker as text

- **Verdict:** stands, and misdescribed in its title. It is not a Windows rendering defect: core has no callout in its reading model, so the Mac shows the same marker (from the code; **unverified** on a Mac).

- **Current state:**
  - Core: `ReadingBlockKind::BlockQuote { depth }` is the only quote kind (`crates/slate-core/src/reading.rs:97`). A quote's paragraph becomes a `BlockQuote` leaf (`:266-277`). Its inline content is `quote_content` (`:907-909`, `:1065-1083`), which strips `>` markers and nothing else, so the content is "[!note]\nThis is a note callout…".
  - The parser options (`reading.rs:66-68`) do not enable `ENABLE_GFM`. That would not be enough anyway: pulldown-cmark 0.13.6 knows five GitHub kinds only (`BlockQuoteKind`: Note, Tip, Important, Warning, Caution), not Obsidian's set, titles or fold markers.
  - The only callout knowledge in core is `dql_inline_fields_db.rs:357`, which sees `> [!` and marks a projection incomplete.
  - FFI: `ReadingBlockKind::BlockQuote { depth }` (`crates/slate-uniffi/src/lib.rs:7892-7916`); `ReadingBlockInlines` (`:8238-8249`). No callout field.
  - Windows: the `BlockQuote` case builds a padded paragraph with an accent bar and marks it as a quote (`RDB:357-368`). It does not read `depth`.
  - Mac: `quoteRow` (`apps/slate-mac/Sources/SlateMac/Reading/ReadingView.swift:446-447`, `:778-791`) renders the same inline content beside a rule, with the value "block quote, level N". No callout handling. Every "callout" hit in `apps/slate-mac` is the SwiftUI font `.callout`.
  - The locked architecture promises it: "Callouts: `> [!note]`, `> [!warning]`, etc." among the parser's Obsidian extensions (`docs/plans/05_locked_architecture_decisions.md:107`, `:590`).
  - The fixture the issue saw: `demo-vault/reference/Markdown features.md:9-21` (note, warning, tip, and a nested info/warning pair).

- **Cause:** the callout extension was never built for the reading model. Both hosts render what core gives them.

- **Fix shape:** two shapes; the owner picks.
  - **A (recommended) — core rewrites the marker to its label in the segment's content.** No FFI change. In `reading.rs`, the first leaf of a quote whose first line is `[!type]`, optionally `+`/`-`, optionally a title, gets content that opens with the label instead of the marker: the authored title when there is one, else the type with a capital ("Note", "Warning"), as a `Strong` run, then the body. The walk has to mark "first leaf of its quote" (a `[!x]` line in a later paragraph is not a callout), and a nested callout is its own quote, so the demo's nested pair works the same way. Both hosts show and speak "Note" with no host change. Windows needs only a test.
  - **B — a structured callout over the FFI** (kind, label, title, fold on the block, so a host can draw a callout box and give it a role or value). An FFI change, both bindings regenerated, the Mac switch kept compiling blind, Mac adoption on the ledger. Until the Mac adopts, it keeps showing the marker unless core also strips it, which then loses the type there.

- **Failing test first:** `crates/slate-core/src/reading.rs` tests (beside the quote facts near `:2621`): `a_callouts_marker_reads_as_its_label` — `reading_inline_segments` over "> [!note]\n> Body." gives content that starts "Note" and holds no `[!`; `a_callout_with_a_title_reads_the_title`; `a_nested_callout_is_labelled_at_its_own_depth`; `a_marker_in_a_later_paragraph_of_a_quote_stays_text`; `a_plain_quote_is_unchanged`. Windows: `tests/SlateWindows.Tests/ReadingViewTests.cs`, `ACalloutShowsItsLabelNotItsMarker` — the built document's text holds "Note" and no `[!note]`.

- **Size:** M under A (most of it is the core walk and its edge cases). L under B.
- **Lanes:** rust, windows (a `crates/` change starts it), swift compile.
- **Screen-reader work:** NVDA spot and Narrator spot (the demo vault's five callouts, Down through each). VoiceOver owed.
- **Changes text or speech a Mac user would hear:** yes under A — a callout's first line changes from the literal "[!note]" to "Note" (or the authored title) in the reading view, and wherever else the Mac shows `ReadingInlineSegment.content` (the print composer reads reading blocks; whether it uses this content is **unverified**). Under B: nothing until the Mac adopts.
- **Owner decision needed:** *A or B?* Recommended: A. It meets the issue's own bar ("a label rather than bracket syntax, on both platforms through core") with no FFI change and no Mac adoption, and it does not bring a callout box in under a defect's name; B is that feature and belongs with Milestone W's design work. *The label's wording:* recommended — the authored title alone when there is one, else the type name capitalised; the fold marker is ignored (the reading view never folds).
- **Shares files with:** none in this cluster under A. (`reading.rs` is not touched by the others.)

---

## #1635 — a slow SVG costs its 5 s budget at every projection; no memory, no per-projection deadline

- **Verdict:** partly fixed. #1908 (78b35504, closing #1634) took the reading view's SVG embed decode off the dispatcher, which is the issue's third proposal and removes its "each block also risks #1634". The two defects in its title remain, moved to the refresh worker.

- **Current state:**
  - Where SVG decodes run now: reading embeds in `ReadingContentViewModel.FetchEmbedResolutions` on the refresh's worker (`Reading/ReadingContentViewModel.cs:1110-1146`); diagrams on the diagram stage's worker (`:2086-2104` → `RDB:1154`); the editor's Ctrl+E preview and the embeds leaf on their workers (`EditorInteractions.cs:1538-1566`, `Panels/RightPanePanelsViewModel.cs:725`). Production passes the builder no diagram artifacts (`ReadingContentViewModel.cs:1694`), and `AppendEmbedImage` only places a worker's decode (`RDB:1787-1797`). I found no production path that still waits on an SVG decode on the dispatcher. The comment at `EditorInteractions.cs:3392-3399` ("the reading view decodes on the dispatcher") is now stale.
  - **No memory.** `DecodeSvgImage` (`EditorInteractions.cs:3306-3434`) keeps no record of a timeout. `FetchEmbedResolutions` decodes every rendered SVG embed again on every refresh; the artifact digest is computed after the fetch (`ReadingContentViewModel.cs:958-968`), so the memo saves the rebuild, not the decode. So with one 40 s SVG in a note: every refresh (a save, a task toggle, a dependency change) publishes 5 s late and starts another decoder thread that runs to its end. After two are abandoned the third refresh is refused at once with the busy sentence (`:3314-3320`), the digest changes (`:2363-2370`), and the card's text flips between "decoding took longer than 5 seconds" and "earlier images are still decoding…" from one refresh to the next.
  - **No per-projection deadline.** The decodes are serial (`:1043`, `:1126`). N distinct SVGs that each take 5.5 s cost N × 5 s before the reading view publishes; each abandoned decode ends 0.5 s later, so the cap of two never engages. The bounds that do apply are 128 resolved embeds (`:883`), the 16 MiB image pool (`:888`) and the 16 MP pixel pool (`RDB:1297`). Worst case from those numbers is minutes on the loading placeholder (arithmetic, **not measured**).
  - A successful slow decode is also repeated at every refresh (a 3 s SVG costs 3 s each time). The issue does not ask for a cache of successes.
  - Diagrams already have a memory of a kind: a timed-out decode is a non-transient `Failed` outcome (`RDB:1160-1166`) and is kept in `_landedDiagrams` (`ReadingContentViewModel.cs:2099-2103`), so the same artifact is not decoded again in that view.
  - **A pinned fact contradicts the proposal.** `ReadingSvgEmbedDecodeTests.ARefreshWhoseSvgDecodeNowSucceedsShowsTheImage` (`tests/SlateWindows.Tests/ReadingSvgEmbedDecodeTests.cs:85`, added by #1908) asserts that the same bytes, timed out once, decode and show on the next refresh. A memory that refuses a repeat at once makes that fact false as written.
  - Core's twin, as the issue says: a real timeout is cached (XB-10a's delivered-shape note) and a note's diagrams have a 20 s deadline (`diagram.rs`, the `"note"` form at `:266-269`).
  - Core's sentence forms today (`diagram.rs:263-283`): diagram, note, busy, image, image-busy. There is none for a note's images. The Mac has no consumer of `render_time_exceeded_message` (no `renderTimeExceededMessage` in `apps/slate-mac` outside the generated binding).

- **Cause:** XB-10b bounded one decode's wait and the thread count; nothing bounds the sum over a projection or across projections.

- **Fix shape:**
  1. `EditorInteractions.cs`, `DecodeSvgImage`: a process-wide, size-bounded memory of byte hashes whose decode was abandoned at its budget. A repeat returns null with the time-exceeded sentence at once, before taking a slot or starting a thread. It serves all three callers. A busy refusal is never remembered. Fix the stale comment.
  2. `ReadingContentViewModel.FetchEmbedResolutions`: a deadline over the projection's SVG decode waits. Each decode waits for the per-image budget or what is left of the projection's, whichever is shorter (core's rule); once it is spent, the remaining SVGs get a sentence without entering the decoder. `DecodeImage` needs a budget parameter for that (today only a thread-static test seam, `EditorInteractions.cs:3257-3265`, `:3400`).
  3. `crates/slate-core/src/diagram.rs`: a sixth form of `render_time_exceeded_message` for "this note's images used their decode budget", and its doc comment in `crates/slate-uniffi/src/lib.rs:8035`. No signature change, so no binding change in shape.
  4. Rewrite #1908's fact to the case that still retries (the busy refusal), and keep the digest's decode field.

- **Failing test first:** `tests/SlateWindows.Tests/ReadingSvgEmbedDecodeTests.cs`: `ASecondRefreshNeverReDecodesAnSvgThatRanPastItsBudget` — seam-parked decode, wait seam returns timeout, first refresh shows the sentence; `Refresh()` again; the decode-body seam's call count is still 1 and the sentence is unchanged. `SvgsPastTheProjectionsDecodeDeadlineNeverEnterTheDecoder` — three distinct SVGs, a 150 ms deadline override, each decode timing out at 100 ms: the third's body is never called and its card carries the note sentence. `tests/SlateWindows.Tests/W2EditorInteractionRedTeamTests.cs`: `SvgPreview_ARepeatOfATimedOutDecodeIsRefusedWithoutADecoderThread`, `SvgPreview_ABusyRefusalIsNotRemembered`, `SvgPreview_TheTimeoutMemoryIsBounded`. Core: extend `render_time_exceeded_message_has_its_four_forms` (`diagram.rs:1855`; despite its name it already pins five forms).

- **Size:** M.
- **Lanes:** windows, rust (the sentence), swift compile.
- **Screen-reader work:** NVDA spot — the new sentence read in range in a note of several slow SVGs, and a second refresh that lands without the 5 s wait.
- **Changes text or speech a Mac user would hear:** no. One new core string that only Windows asks for.
- **Owner decision needed:**
  1. *How long is a timed-out decode remembered, given #1908 pinned a retry?* Recommended: for the life of the process, keyed by a hash of the bytes, 256 entries, oldest dropped — core's twin does the same for the session. The cost: a decode that timed out only because the machine was busy stays refused until the file changes or the app restarts. The softer option is to forget an entry after some minutes, which needs a clock seam.
  2. *The projection's deadline.* Recommended: 20 s, core's per-note number.
  3. *The sentence.* Recommended: "Image not shown: this note's images used their 20-second decoding budget."
- **Shares files with:** #1610 (`EditorInteractions.cs`, `diagram.rs`, the SVG test files), #1631 (`diagram.rs`).

---

## #1610 — the SVG guard should weigh rules × elements and marker vertices × subtree

- **Verdict:** stands as written. It is a hardening, not a defect: it carries no `bug` label, and nothing is wrong today that the 5 s budget and the cap of two do not already bound.

- **Current state:**
  - `apps/slate-windows/src/SlateWindows/SvgPreviewGuard.cs` (836 lines, last changed by #1582): `MaxRenderDepth` 256 (`:73`), `MaxRenderedElements` 200,000 (`:78`), `MaxUrlReferencesPerValue` 8 (`:82`), `MaxValueNesting` 16 (`:86`), `Check` (`:100-132`). `Measure` (`:509` on) walks a reference graph for the longest chain and the count of element renders. The issue's line numbers are right.
  - It records a stylesheet rule only for the `url(#…)` references it holds (`ruleTargets`, `:144`, `:482-505`). It does not count rules, declarations or custom properties, and it does not read a polyline's `points` or a path's `d`. A marker is one edge however many vertices replay it. So the issue's claim holds.
  - A guard refusal has no sentence of its own: `RenderSvgToPng` returns null (`EditorInteractions.cs:3490-3493`) and the caller shows the host's "Could not decode image. MIME: … The file may be corrupt or …" (`EditorInteractions.cs:1898-1899`, `RDB:1847-1848`; the two wordings differ by one word).
  - Svg.Skia is pinned at 5.2.3 (`src/SlateWindows/SlateWindows.csproj:31`), the version #1581 measured.

- **Cause:** not a fault. The guard was written to bound recursion and multiplied reuse (#1582); cost products were left to the time budget (#1581) and filed here.

- **What the issue rests on:** four measurements from #1581's body, Svg.Skia 5.2.3 at 1,120 px: a stylesheet of many simple rules over many elements, about 150 KB, 40 s; the same at about 450 KB, over 2.5 min; many custom properties over many elements, about 400 KB, 29 s; a marker with a large subtree on a many-vertex polyline, about 220 KB, 43 s. One more point from #1908's pass: a marker of 1,500 children on a 15,000-vertex polyline, 163 KB, ran past 5 s. **None of these documents is in the repo** (no fixture or generator under `tests/` or `tools/` has a polyline or a marker), and the rule and element counts behind each row were never recorded. The limits cannot be set from the issue; they need a calibration run.

- **Fix shape:** `SvgPreviewGuard.cs`: count stylesheet rules (custom-property declarations with them) and elements, and for each element that takes a marker, its vertex count times the marker's subtree size; two new `SvgRefusal` values and two limits. `EditorInteractions.cs`: carry the refusal out of `RenderSvgToPng` so a cost refusal can give `failureReason` a core sentence. `diagram.rs`: that sentence, if it gets its own. Generators for the four document kinds in the test project, and the same documents through `tools/SvgDecodeProbe`.

- **Failing test first:** `tests/SlateWindows.Tests/SvgPreviewGuardTests.cs`: four new rows in the `RefusesEachDocumentPastACap` theory (`:96`), one per measured kind, each generated at the measured size and expected to be refused with the new refusal. They fail today because the guard returns `None`. `AcceptsTheShapesRealDocumentsUse` (`:44`) gains the largest SVG mermaid-rs-renderer emits under core's caps, which must still pass. Then `SvgPreview_ADocumentRefusedForItsCostShowsCoresSentenceAndStartsNoDecode` in `W2EditorInteractionRedTeamTests.cs`.

- **Size:** M. The code is a day or two; the calibration is the rest.
- **Lanes:** windows; rust and swift compile if it gets its own sentence.
- **Screen-reader work:** NVDA spot (the sentence a reader hears for these documents changes from the timeout sentence to the refusal's).
- **Changes text or speech a Mac user would hear:** no.
- **Owner decision needed:**
  1. *Do it here, or close as won't-do?* Recommended: do it, after #1635. What it buys once #1635 lands is not the 5 s (that is then paid once per process) but the abandoned decode: it cannot be cancelled, it holds a core for its full 40 s to 2.5 min, and while two are running every other SVG in the app, a mermaid diagram's included, is refused as busy.
  2. *The two limits.* They have to be the owner's numbers, chosen from a recorded table: decode time against each product at several sizes on this machine and on the CI runner, beside the same products for the largest real mermaid output (mermaid SVGs carry a stylesheet, and diagram decodes pass through this guard, `RDB:1154`). Recommended rule: the limit is the product at which a decode takes about 1 s on the slower machine, provided the largest real diagram sits at a tenth of it or less; if it does not, raise the limit and accept that fewer slow documents are caught.
  3. *The refusal's sentence.* Recommended: its own core form ("Image not shown: it would take too long to decode."), because "took longer than 5 seconds" is untrue of a decode that never ran — the call the owner already made twice (the busy forms on #1580 and #1581).
- **Shares files with:** #1635 (`EditorInteractions.cs`, `diagram.rs`, `W2EditorInteractionRedTeamTests.cs`), #1631 (`diagram.rs`).

---

## #1631 — one panicking diagram disables diagrams for the session

- **Verdict:** stands. The issue's reading of mermaid-rs-renderer 0.3.1 is correct; I checked it against the pinned source.

- **Current state:**
  - The pin: `crates/slate-core/Cargo.toml:74` (`version = "0.3"`, a caret range), `Cargo.lock:1368-1369` (0.3.1).
  - Core: `RENDERER_POISONED` (`crates/slate-core/src/diagram.rs:520`, comment `:516-519`). `try_render_mermaid` (`:604-653`): the short-circuit and its sentence (`:609-616`); the flag set when an upstream error's text contains "poison" (`:632-639`); the flag set on any panic (`:646-651`). The render thread also sets it when an abandoned render panics (`:771-776`).
  - Upstream 0.3.1 (`~/.cargo/registry/…/mermaid-rs-renderer-0.3.1/src`): `text_metrics.rs:12` is the crate's only `Mutex` and its only mutable global (the other statics are `Lazy<Regex>`). `:19` is `let mut guard = TEXT_MEASURER.lock().ok()?;` inside `measure_text_width`, so the lock is taken per measurement and a poisoned lock returns `None`. All eleven call sites fall back to an estimated width (`layout/text.rs:421-422`, `:448`; `layout/c4.rs:736-737`; `layout/pie.rs:194-199`; `render.rs:3099`, `:3212`, `:3656`, `:5307`, `:5546`, `:5655`, `:5660`, `:5668`). The word "poison" appears nowhere in the crate, so the `:632` branch cannot fire with 0.3.1.
  - So: a panic outside a measurement poisons nothing; a panic inside one makes every later label width an estimate, and renders still succeed. XB-10a's delivered-shape note already says the lock is per measurement.
  - Tests that pin the old rule: `poisoned_renderer_short_circuits_with_clear_message` (`:1288`), `poisoned_renderer_still_produces_structured_description` (`:1307`), `poison_error_in_library_response_sets_flag` (`:1333`, which asserts only that a string contains "poison"), and `an_abandoned_render_that_panics_still_releases_its_slot` (`:1833`, which ends by asserting the flag is set, `:1850`).
  - What a reader gets today: the panicking diagram's reason is `"mermaid renderer panicked: {panic:?}"` (`:648-650`); `panic` is a `Box<dyn Any + Send>`, whose `Debug` prints `Any { .. }`, so the sentence is "mermaid renderer panicked: Any { .. }" (from the code; **not run**). Every later diagram gets "mermaid renderer is unavailable for the rest of this session (a previous render caused a panic that poisoned the renderer's internal state)". Both hosts show the message verbatim: Windows as the failed state's reason (`RDB:1126-1130`), the Mac in `MermaidView.swift:46-47`. Those failed blocks are then cached per note for the session (`session.rs:10189` on).

- **Cause:** audit #246's premise ("all subsequent renders will fail") was true of an older renderer and is false of 0.3.1.

- **Fix shape:** `diagram.rs` only. Stop setting the flag on a panic (`:646-651`, `:771-776`). Keep the flag and the short-circuit for the one case that would justify them, an upstream error that names a poisoned lock (`:632-639`), so a later 0.3.x that surfaces `PoisonError` falls back to today's behaviour. Correct the comments at `:516-519`, `:600-603`, `:772-774`.

- **Failing test first:** `diagram.rs` tests, under `renderer_test_guard`: `a_render_after_a_panicked_render_still_renders` — a `render_seam` that panics; `render_diagram(&flowchart())` is `RenderFailed`; drop the seam; `render_diagram` of the same flowchart through the real renderer is `Ok` with an SVG. Today the second is the "unavailable" sentence. Then flip the last assertion of `an_abandoned_render_that_panics_still_releases_its_slot`, and replace the vacuous `:1333` fact with one that feeds `try_render_mermaid` a seam error whose text holds "poison" and asserts the flag and the sentence.
  - **What this cannot prove:** recovery from a *poisoned measurer*. `text_metrics` is a private module and its static is private, so no test in this repo can poison it, and I know of no input that panics inside a measurement (**unverified** that none exists). That half rests on reading the pinned source, as above. The kept "poison" branch and the dependency cooldown review are the guard for an upgrade.

- **Size:** S.
- **Lanes:** rust; windows and swift run because `crates/` changed, with no host change.
- **Screen-reader work:** none possible on Windows — no known input panics the renderer, and the seam is test-only. Write `not delivered` for both passes with that reason.
- **Changes text or speech a Mac user would hear:** yes. After a diagram panics, later diagrams render (or fail for their own reasons) instead of showing "mermaid renderer is unavailable for the rest of this session (…)". The panicking diagram's own sentence is unchanged unless the owner takes decision 2. One ledger line.
- **Owner decision needed:**
  1. *Drop the short-circuit or narrow it?* Recommended: narrow it to upstream's own "poison" error text, as the issue suggests.
  2. *Reword the panic sentence in the same PR?* "mermaid renderer panicked: Any { .. }" is a debug print read aloud, the same class as #1890. Recommended: yes — "Diagram not drawn: the renderer failed on this diagram. The source follows.", in the family of the budget sentences. The PR already owes a VoiceOver line, and no field pass can reach either sentence.
- **Shares files with:** #1635 and #1610 (`diagram.rs`).

---

## Cluster

### The sweep: every embedded element the Windows reading view builds

The builder creates a `UIElement` inside the document at exactly five kinds of site. I found them by reading `RDB` in full and searching `Reading/` for `InlineUIContainer`, `BlockUIContainer` and `new …Element`. Everything else in the reading view is a `TextElement`, in the text range.

"Range text" is from the code. "Line reading" is quoted from a recorded pass where one exists; otherwise it is a prediction and says so.

| # | Kind | Class and site | Peer, control type | Range text for its line | Line reading (NVDA Down) | In the class? |
|---|---|---|---|---|---|---|
| 1 | Math block, matched (drawn, coverage gap, core fallback) | `ReadingMathElement` (`ReadingMathElement.cs:24`) in an `InlineUIContainer`, `RDB:655-667` | `ReadingMathElementPeer` (`:208`), Group, "math"; no HelpText (`:232`), no children (`:238`) | the hidden speech run, then the object | "laplacian of f / grouping" — #1595's field pass, NVDA 2026.2 | Fixed by #1633 |
| 2 | Math block, no core artifact | same class, zero-size, `RDB:616-621` | same | the fenced source, then the object | "$$y^3 - 2$$ / grouping" — same pass | No: text precedes the role |
| 3 | Diagram, rendered | `ReadingDiagramElement` (`ReadingDiagramElement.cs:21`), `RDB:1033`, shape `:1193-1211` | `ReadingDiagramElementPeer` (`:120`), Group, "diagram"; HelpText = source | the object alone | "grouping" — #1708, NVDA 2026.2 | **Yes** |
| 4 | Diagram, pending | same class, zero-size, `RDB:1060-1061` | same; Name "Diagram, rendering" | the source lines, then the object | source lines, then "grouping" — #1708 | **Yes** (the name is never spoken) |
| 5 | Diagram, failed | same class, zero-size, `RDB:1042`, shape `:1219-1257` | same; Name = description | header, reason, source, then the object | all the text, then "an empty grouping" — #1708. Whether that role is a Down stop of its own is **unverified**; from the code it shares the last text line | Borderline: decision 1 on #1708 |
| 6 | Task checkbox | stock `CheckBox` in an `InlineUIContainer`, `RDB:1883-1902` | WPF's stock CheckBox peer (CheckBox, Toggle; matched as `ControlType.CheckBox` by the gate, `ShellAccessibilityTests.cs:1865-1867`); Name = the item's text (`:1901`) | the object, then the item's text | "task checkboxes with state" — W3-1 pass, NVDA 2026.1.1 (`reports/w3_1_nvda_field_verification.md:13`); the exact phrase was not recorded | No: the line has its text |
| 7 | Thematic break | stock `Separator` in a `BlockUIContainer`, `RDB:377` | WPF's stock Separator peer (Separator; **from WPF, not this repo**); no Name | the object alone | **Unverified.** Predicted "separator" or "blank". The W3-1 corpus had one and the pass recorded no finding | No name to lose; a role alone is the right speech for a rule. A pass settles it |
| 8 | Image in an embed card | `ReadingDiagramImage` in a `BlockUIContainer`, `RDB:1851-1867` | none (`ReadingDiagramElement.cs:108-111`) | the object alone, on its own block; the card's header line above it holds "Embedded image: …" and the Jump link (`RDB:1491-1505`) | **Unverified.** No peer, so no role; predicted "blank". #1908's pass only walked the failed case | Not "grouping". Possibly a silent Down stop |

Not embedded elements, checked and listed so the census can say so:

- The drawn image inside a diagram element (`ReadingDiagramElement.cs:59-68`): the element's content, no peer, not separately in range.
- Code block: a `Section` of a caption paragraph and a code paragraph, all text (`RDB:507-543`). "Copy code" is `CodeCopyHyperlink`, a `Hyperlink` whose peer reports Button (`CodeCopyHyperlink.cs:21-44`); in range.
- Embed cards for a note, a section, a block and a `.base`: a `Section` of text paragraphs (`RDB:1457-1557`, base body `:1400-1425`). "Jump to source" is a `Hyperlink` (`:1635-1668`), made one on 2026-07-30 because a Button there was this same class.
- Links, wikilinks, tags, citations and inline embeds: `Hyperlink` (`RDB:1976-1997`).
- Tables: a WPF `Table` in range (`RDB:1325-1362`). `ReadingTableGrid` is a separate window opened by Enter (`ReadingTableGrid.cs:31`), not an element in the document.
- Headings, lists, quotes, HTML, the loading placeholder and the notices: paragraphs.
- Audio, video, PDF: core's `EmbedResolution` has five cases (note, section, block, image, unresolved), so the reading view builds no media element. What core returns for `![[clip.mp3]]` is **unverified**.
- Inline math and Markdown images are text in the reading view.
- Dead code found on the way: `ReadingSemantics.MarkEmbed(BlockUIContainer)` (`ReadingSemantics.cs:127`) is never called, so the `BlockUIContainer … IsEmbed` arm of the landmark walk (`RDB:2094-2101`) never runs.

**The sweep's result:** the class has one live member, the diagram, in three states. Math is fixed. The checkbox is safe by construction. Two kinds (the rule and the embed image) put an object alone on a line with no name; neither can say "grouping", and what they do say needs one NVDA pass.

### XB-9's shape and whether it carries over

XB-9 chose **a1h**: the paragraph is a transparent run of the speech at a 0.1 font size (smaller for long speech) followed by the element; the element has no HelpText (the peer overrides WPF's ToolTip fallback) and no child peer; the source is spoken only by Ctrl+Shift+Enter through core's `MathSourceSpoken`. Its review added three things: the element leaves the run room on their shared line (`LineReserve`), a copy substitutes the fenced source, and the caret is anchored by its block. WB-22 later made find skip the hidden run.

| Kind | Carries over? |
|---|---|
| Diagram, rendered | The run and the shared line: yes, with new code (`LineReserve` on the diagram element). The HelpText half: not without an owner decision and an FFI addition. The find and copy rules: need twins |
| Diagram, pending | Not unchanged. Math had no state with visible text *and* a name to speak. Placement needs the spike |
| Diagram, failed | Not unchanged, and recommended not applied |
| Checkbox, rule, image | Not needed |

### The fact designs

1. **Per-state line facts** in `ReadingDiagramTests` (named under #1708): the paragraph's range text is the element's Name; the run and the container start the same visual line at 420 px; completion swaps the placeholder's text for the description.

2. **The census** — new `tests/SlateWindows.Tests/ReadingEmbeddedElementCensusTests.cs`, two facts.
   - `EveryEmbeddedElementsLineCarriesItsText` (hosted, on `ShownShell` with the surface's real Text provider, as `ReadingLineUnitTests.EveryCaretReadsTheOneLineItIsOn` `:84` does). One note holds every kind: drawn math, gap math, unmatched math, a rendered diagram, a failed one, a pending one (stage gate held), a task, a rule, a PNG embed and an SVG embed. The fact walks **every** `InlineUIContainer` and `BlockUIContainer` in the built document and looks its child's type up in a closed table. A child type not in the table fails with "a new embedded kind: add its row and its line fact". Per row:
     - child with a non-empty Name (math, diagram, checkbox): the Text pattern's Line at the container's start, trimmed of spaces and object characters, is not empty;
     - matched math, rendered and pending diagram: that line's text contains the Name;
     - `Separator`: its peer's control type is Separator and its Name is empty;
     - `ReadingDiagramImage`: it has no peer.
   - `EveryEmbeddedElementSiteIsInTheCensus` (a source census in the style of `ReadingDoctrineCensusTests`): the `new InlineUIContainer(` and `new BlockUIContainer(` sites in `Reading/*.cs`, comments stripped, are exactly the listed ones, so a new host site fails until it has a row.

3. **The gate**, `ReadingTextPattern_RangeFromChildResolvesEveryCustomPeer` (`ShellAccessibilityTests.cs:1728`): for the math and diagram entries, the Line that encloses the child's range reads the element's Name, cross-process. This is the update the issue's done-when asks for.

The census pins the range text, which is what the code controls. It cannot pin speech. The NVDA pass for #1708's PR should also Down over a rule and over a shown image once and record both phrases beside the census rows.

### Recommended cut into PRs

| PR | Issues | Size | Lanes | Order |
|---|---|---|---|---|
| A | #1631 | S | rust (windows, swift compile) | first of the `diagram.rs` chain |
| B | #1635 | M | windows, rust | after A |
| C | #1610 | M | windows, rust | after B; shares `EditorInteractions.cs` and `diagram.rs` with it; the calibration table is its step 0 |
| D | #1708 with the census | M | windows | parallel with A → B → C (it touches `Reading/RDB`, `ReadingSurface`, `ReadingDiagramElement`, `ReadingTextIndex` only); spike first |
| E | #1775, shape A | M | rust, windows | parallel with all of them (`reading.rs` only); reader-facing, so with the reader-facing sweeps |

Three separate PRs for A, B and C, not one: each closes its own issue, and A's change to what a Mac user hears should be revertible alone. A, B and C cannot share a wave (same files); D and E can run beside them.

Decision rows this cluster adds: #1708 ×4, #1775 ×2, #1635 ×3, #1610 ×3, #1631 ×2. Ledger lines ("Mac work owed"): #1631 (the session-long sentence goes; the panic sentence if reworded), #1775 under A (a callout's first line), and a check of the Mac reading view for #1708's gap.

### Risks

- **#1708's pending and failed states have no precedent.** Math had one shape; XB-9 still needed a spike, a review that found the shared-line defect, and a second pass. Budget the spike.
- **In-place completion.** `CompleteDiagrams` depends on which inline is first and last, and six facts pin the caret and selection across it. A new run in the paragraph is exactly what they guard.
- **A text change under the caret.** Completion will now change in-range text where it changed only a Name. `ALateDiagramIsSilent` (`ReadingDiagramTests.Stage.cs:506`) pins that Slate announces nothing; whether NVDA re-reads a line whose text changed under the caret is **unverified** and belongs in the pass.
- **#1635 reverses a fact #1908 added on 2026-11-11, the day before this baseline.** If the owner prefers the retry, the memory needs an expiry and a clock seam, and the size grows.
- **#1610's limits against real diagrams.** Mermaid output has a stylesheet and passes through the same guard. A limit set from the four slow documents alone could refuse a large real diagram.
- **#1631 rests on a source reading for the poisoned-measurer half,** and the manifest allows any 0.3.x.
- **#1775 can grow into the callout feature** (a box, an icon, a fold). Shape A is the fence against that; it does change what a Mac user hears, with no Mac here to listen.
- **Two predictions in the sweep are unverified** (the rule, the shown image). If the pass finds a silent or misleading stop, that is one more small sibling for this chunk.
