# CB-29: The SVG guard refuses the documents that decode slowly, from recorded limits

Issue: #1610. Size M, growing toward L by Call 2. Lanes: windows (the app job; a change under `crates/` runs the lane in full), rust (one sentence form and its fact), swift (compile and the lane's suite; no Swift edit). AT: an NVDA spot check in the reading view, base and head. Wave: C8, slot 3. Parallel with: CB-31 (#1704, core links) and CB-15b (canvas, graph and search failure detail); neither edits a file this chunk edits. Decision: CD20, answered 2026-11-16 as recommended: do it, after CB-28; the owner picks the limits from the calibration table this PR records (a two-step row, plan §8); its own sentence, "Image not shown: it would take too long to decode." Interlock: `diagram.rs` and `EditorInteractions.cs` go CB-27, CB-28 (merged), CB-29, then CB-15c and CB-22 (plan §6). Review: a red team, then Codex adversarial-review rounds until it approves (plan §4 rule 9).

Written 2026-11-19 from `reading/06_reading_view_diagrams.md:121-160`, the issue and its owner comment, CB-28's spec (the parent, `specs/cb28_a_timed_out_svg_decode_is_remembered.md`) and a step 0 calibration run. Every `path:line` below was re-read at 16ad788d (`main` on 2026-11-19, after #2018). Step 0 ran at 16ad788d in this chunk's worktree with a Cargo target directory of its own, bindings regenerated with `-Locked`, a Release build (0 errors), and probe facts in an untracked file deleted before the branch is cut. The probe source, every generated document, the raw CSVs, the fits and the decision tables are kept with the chunk's working notes, and the evidence is committed under `docs/plans/47_windows_core_closeout/calibration/cb29/` (`calibration.md`, `decision.md`, `est.csv`). Statements still by reading say so.

`S/` is `apps/slate-windows/src/SlateWindows/`; `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`; `G` is `S/SvgPreviewGuard.cs`; `EI` is `S/EditorInteractions.cs`; `D` is `crates/slate-core/src/diagram.rs`; `RCVM` is `S/Reading/ReadingContentViewModel.cs`; `RDB` is `S/Reading/ReadingDocumentBuilder.cs`.

## Goal

Before any decoder thread, slot or pixel, the guard estimates how long Svg.Skia would take to decode a document, from the four costs that make SVGs slow (a stylesheet's queries and the declarations it applies, `<use>` instances under a stylesheet, `var()` expansion, and markers), and refuses a document whose estimate passes the owner's limit. The reader hears "Image not shown: it would take too long to decode." at once, in the reading view, the Ctrl+E popover, the embeds leaf and a diagram's card, where today the same document runs out its 5-second budget and keeps a core busy for up to minutes after.

## Drift check (the reading against 16ad788d)

| Reading's claim (1ec9bf31) | At 16ad788d |
|---|---|
| `G` is 836 lines: `MaxRenderDepth` 256 `:73`, `MaxRenderedElements` 200,000 `:78`, `MaxUrlReferencesPerValue` 8 `:82`, `MaxValueNesting` 16 `:86`, `Check` `:100-132`, `Measure` from `:509` | Holds, line for line |
| It records a stylesheet rule only for its `url(#…)` targets (`ruleTargets` `:144`, `:482-505`); it counts no rules, declarations or custom properties, and reads no `points` or `d`; a marker is one edge however many vertices replay it | Holds (`:144`, `:484-504`). `SimpleSelectorKey` `:686`, `TakesStyleRules` `:677` |
| A guard refusal has no sentence: `RenderSvgToPng` returns null (`EI:3490-3493`) and the caller shows "Could not decode image. MIME: …" (`EI:1898-1899`, `RDB:1847-1848`) | `RenderSvgToPng` `EI:3772`, its guard call `EI:3783` (`IsSafeSvg`, `EI:3888-3889`); the sentence at `EI:1914` and `RDB:1847` |
| Svg.Skia 5.2.3 (`SlateWindows.csproj:31`) | Holds. Resolved with it: Svg.Custom, Svg.Model, Svg.SceneGraph 5.2.3, ExCSS 4.3.1, SkiaSharp 4.153.1 (the test build's `deps.json`) |
| #1581's four documents are not in the repository; no fixture or generator has a polyline or a marker | Holds. Step 0 generated all four (below) |
| (CB-28, merged as 9999cb68) | `DecodeSvgImage` `EI:3529`: the memory and the projection's deadline (`SvgDecodeRefusalBeforeDecoding`, `EI:3501-3527`), then the cap (`EI:3546-3552`), then the decoder thread (`EI:3591`). The reading view asks `SvgDecodeRefusalBeforeDecoding` when its pixel pool is spent (Codex round 2 of CB-28). The form `"note-images"` is in `D:281-283`; `lib.rs`'s doc comment points at core's list, so a new form needs no `lib.rs` edit |
| Mermaid output has a stylesheet and passes through this guard (`RDB:1154`), so a limit from the slow documents alone could refuse a large real diagram | The decode is `RDB:1154-1166`. **Not true of today's renderer:** mermaid-rs-renderer 0.3.1 writes no `<style>` for a rendered diagram (its only one is in the error diagram, `render.rs:2224`), and core sends it only five dialects (flowchart, sequence, class, state, ER; `D:489-506`). Step 0's largest outputs have no `<style>`, `<use>` or `var()` |

## Step 0, as run on main (16ad788d), 2026-11-19

Every decode went through the app's own path (`EditorInteractionCoordinator.DecodeImage`: the decoder thread with its 256 MB stack, the guard, Svg.Custom's parse with the app's `SecureStatic` options, Svg.Skia's picture, the raster at 1,120 px, the PNG and WPF's decode), one at a time, each with its own slots and a 130 s budget; points under 10 s are the median of three. Desktop: AMD Ryzen 7 9800X3D, 8 cores, 61.6 GiB, .NET 10. No home-pool job ran during any run (26 checks of `WINDOWS_HOME_STATE`). Two reference documents repeated through the runs varied 1.22x to 1.28x.

**The cost model, read from the cached assemblies' IL** (no sources or symbols are in the NuGet cache; a reflection lister of the worker's own, whose listings stay local in the principal's scratch folder; each claim cites type, method and IL offset in `calibration.md` §1):

1. **Stylesheet.** Svg.Custom runs every comma-separated selector branch as its own query over every element of the document (`ExCssQuery.QuerySelectorAll` walks every descendant), and applies every declaration of a rule to every element it matched (validate and store, `ApplyDeclaration`). A rule declaring a custom property or a geometry property (`cx, cy, d, height, r, rx, ry, transform-box, transform-origin, width, x, x1, x2, y, y1, y2`) is queried two or three times. Measured: 0.093 us per (selector branch x element), 1.03 us per matched paint declaration, about 4.5 us per rule.
2. **Uses under any stylesheet** (not in the issue). When a document has any `<style>`, Svg.Skia re-applies the whole stylesheet to every `<use>` instance it draws and drops the computed-style cache (`BeginUseInstanceStyleScope`). Measured: 0.2465 us x use renders x (all elements + 15 x rules), plus the instance's own subtree. A one-rule stylesheet over 10,000 uses decodes in 30 s; without the `<style>`, in 2 s.
3. **`var()` expansion.** Svg.Custom resolves `var()` with no memo, so a custom property that names another k times expands k^L, and gives up at the first reference past its depth limit (16 lookups). Measured: 0.19 us per resolved copy. A 746-byte document with three references per level at 14 levels decodes in 0.8 s.
4. **Markers.** Svg.Skia compiles the marker's whole subtree, references included, at every placement (`TryCompileDirectMarkerNode`, no instancing). Markers are placed by the attribute, the `marker` shorthand, a `style` declaration, a stylesheet rule and inheritance from an ancestor, and on rect, circle and ellipse as well as path, line, polyline and polygon. Measured: 2.548 us x [positions x (marker renders + 20) + marker renders] per marked element, times the element's own renders.

**#1581's four documents, reproduced** (each within a factor of two of the issue's size and time):

| #1581 row | Generated | Size | Decode |
|---|---|---|---|
| Stylesheet of many simple rules, about 150 KB, 40 s | 3,000 `*` rules over 13,000 `<g/>` | 97 KB | 40.5 s |
| The same, larger, about 450 KB, over 2.5 min | 15,000 `*` rules over 8,000 rects | 449 KB | 135.6 s |
| Many custom properties, about 400 KB, 29 s | `var()` doubling, 15 levels, 5,000 elements | 396 KB | 34.8 s |
| A marker on a many-vertex polyline, about 220 KB, 43 s | a 2,000-rect marker on a 10,000-vertex polyline | 168 KB | 50.0 s |

Size predicts little: at 150 KB a stylesheet document decodes anywhere from 0.2 s (class rules) to 40 s (universal rules over small elements).

**Real documents.** Core's largest mermaid outputs under its caps (64 KiB source, 5 s render, 2 MiB SVG), one per dialect, 0.6 to 2.1 MB, decode in 0.21 to 0.76 s, and that time is their element count's base cost. None has a `<style>`, a `<use>` or a `var()`; flowchart and ER arrowheads are polygons, and class, state and sequence edges carry a one-path marker. The repository's only SVG (`demo-vault/attachments/pie.svg`) decodes in 2 ms; the guard tests' 13 real shapes in 0.4 to 16 ms. No real-world export was downloaded (Call 8).

**The guard's own cost:** `Check` takes at most 184 ms on an 8 MiB document (150,092 rects), 65 ms on an 8 MiB stylesheet, 38 ms on an 8 MiB polyline, 54 ms on the largest mermaid output, and 17 ms or less on every other document measured.

## The calibration table (CD20's second step, for the owner)

The design (below) sums the four costs into one estimated decode time, with the rates measured on this desktop, and refuses past one limit. Each row counts the 174 decoded step 0 documents (`docs/plans/47_windows_core_closeout/calibration/cb29/est.csv`). **These are counts of generated documents chosen to bracket the 5 s budget, not frequencies of anything a reader has.** "Refused under 5 s" is an image a reader would lose that decodes today; "admitted past 5 s" is a document that still runs out its budget (then CB-28's memory refuses it for the rest of the process).

With the estimate as designed (the five corrections of Design 3 included):

| Limit (estimated, this desktop) | Refused, decodes under 5 s today (of which under 1 s) | Admitted, runs past 5 s (of which past 20 s) | Margin to the largest real document |
|---|---|---|---|
| 1 s | 34 (0) | 0 (0) | 13x |
| **2 s (recommended)** | **16 (0)** | **0 (0)** | **26x** |
| 3 s | 10 (0) | 1 (0) | 39x |
| 5 s | 2 (0) | 2 (0), 5.2 s and 5.3 s | 65x |
| 10 s | 0 (0) | 18 (0) | 131x |

The largest real estimate is the 1,093-message mermaid sequence diagram, 76 ms (all of it markers; it decodes in 434 ms). Every other real document estimates at 38 ms or less. #1581's four reproductions decode in 0.92x to 1.11x of their estimates and are refused at every limit. Actual over estimate, over documents where both pass 0.25 s: 0.34 to 3.18, geometric sd 1.36.

What each limit means for a reader:

- **1 s** refuses 18 more documents than 2 s, each of which decodes here in 1 to 5 s, and keeps out nothing more that runs past 5 s.
- **2 s** keeps out every document in the data that runs past the budget. The 16 it refuses that decode under 5 s here take 1.4 to 4.7 s, most of them 2.5 s or more, so on a machine twice as slow most would pass the 5 s budget anyway. A plotting-library-shaped scatter with one global rule sits at the line: 3,000 points estimate 2.31 s (refused) and decode in 1.75 s here; 1,000 points estimate 0.28 s (shown).
- **3 s** refuses 10 fast documents and admits one slow one (many small markers, 5.2 s). **5 s** refuses 2 and admits 2 (5.2 s and 5.3 s), each holding a core for its whole decode before CB-28's memory takes over.
- **10 s** refuses nothing that decodes under 5 s, and admits 18 that run past it (5.2 to 11.8 s).

The alternative of two per-product limits (a stylesheet limit and a marker limit, as CD20 and the issue framed it; uses and `var()` not guarded; computed with the first estimate's terms, before Design 3's corrections) refuses 29, 13 and 5 documents that decode under 5 s at 1, 2 and 5 s, and admits every `<use>` and `var()` document: 10 to 13 past 5 s, four of them past 30 s, among them #1581's custom-property row (34.8 s) and the scatter at 10,000 points (17.4 s). Call 1.

What the numbers do not settle: the rates are one fast desktop's (the CI runner was not measured, Call 7); per-kind error up to about 3x remains (many small markers, D_paths, under-estimated to 3.2x; custom properties on every element over-estimated to 3x); the real-world classes are the worker's guesses beyond mermaid, the pie and the test shapes (Call 8); a plain large document has no term (8 MiB of rects decodes in 2.4 s and estimates 0; the 8 MiB and 200,000-element caps bound it, and a base term would refuse it at 2 s).

## Current state on main (16ad788d), what the design changes

- **The guard** (`G`) refuses for markup, references, `url()` count, nesting, render depth, rendered elements and cycles, and measures nothing about cost beyond the render count. It runs on the decoder thread, inside `RenderSvgToPng` (`EI:3783`), after a slot is taken; a refusal returns null with no reason, and the caller says "Could not decode image. MIME: image/svg+xml. The file may be corrupt or use an unsupported codec." (`EI:1914`, `RDB:1847`).
- **The decode** (`EI:3529-3727`), since CB-28: the memory, the projection's deadline, the cap, the thread, the 5 s wait, the abandon.

## Design

1. **One estimate, in microseconds, summed in the guard.** `SvgPreviewGuard` gains an `SvgDecodeEstimate` (four terms and their sum) computed in its one read and `Measure`, with named rate constants (`SvgDecodeRates`, each with the calibration's figure and this spec's name in its doc), and one limit, `MaxEstimatedDecodeMilliseconds`. Built with **2,000 ms as a placeholder, named as the recommendation and not the owner's pick** until the owner answers (Call 4); the constant's doc and the PR body say so. A document past the limit is refused with a new `SvgRefusal.DecodeCost`. The terms, each counted the way the guard can in its pass:
   - **Stylesheet:** 0.0935 x (sum over rules of selector branches x passes x the branch's combinator weight) x all elements + 1.03 x matched declarations of every other kind + 0.5 x matched custom-property or `var()` declarations + 4.5 x rules. Rules are the qualified rules of the joined `<style>` text, recursed into `@media` (Svg.Custom recurses into `@media` only and skips other at-rules and rules with no declarations; counting those too errs wide and is harmless). Passes are 1 + [a `--name` declaration] + [a geometry declaration]. The combinator weight is Design 1b's. All elements is every element, the root included. Matched declarations are summed over **every** element, not only those `TakesStyleRules` admits: Svg.Custom's query walks every child and applies every declaration to every match, `desc`, `title`, `stop` and gradients included (the red team's M6).
   - **Uses under a stylesheet**, only when the document has a `<style>` element: 0.2465 x use renders x (all elements + 15 x rules) + use renders x the target's subtree renders (its bottom-up subtree size with references expanded, not `paths`) x (22.2 + 0.0666 x rules). Use renders are the sum of `Measure`'s render counts over `use` elements.
   - **`var()`:** 0.192 x copies. A custom property's expansion is the sum over the `var(--x)` occurrences in its value of x's expansion, a literal counting 1, resolved left to right; a reference that fails (past 16 lookups, or to an undefined name) costs nothing if it has no fallback, and costs its fallback's expansion one lookup deeper if it has one (Svg.Custom resolves a fallback at depth + 1 after any failure, depth failures included: the red team's M4, measured in the follow-up). A name declared more than once takes its largest. Each declaration that holds `var()` and is not itself a definition adds its value's copies times the elements it lands on (1 for a `style` attribute or a presentation attribute; the rule's matched elements for a rule). The expansion is computed with a memo per (name, depth) in a fixed table of 17 depths and saturating arithmetic, never by recursion, so the guard's own time stays linear in the declarations (the red team's M5: C_kary K=4 D=15, about 1.07e9 copies, must cost the guard microseconds).
   - **Markers:** 2.548 x sum over marked elements, times each element's own renders, of [positions x (marker renders + 20) + marker renders], with marker renders the largest over the element's slots. An element's start, mid and end markers come from its presentation attributes (`marker-start`, `marker-mid`, `marker-end`, `marker`), its `style`, the stylesheet rules filed under its keys (the property name read; a rule whose name cannot be read counts as `marker-mid`), and else its nearest ancestor's (inheritance); several candidates for one slot take the one with the most renders. A marker property whose value holds `var()` counts as the widest marker any custom property's value names (Svg.Custom resolves it; the red team's M7). Placed on path, line, polyline, polygon, rect, circle and ellipse. Vertices: `points` numbers / 2 (+1 for polygon), `d` per command (M, L, T: 2 numbers; H, V: 1; C: 6; S, Q: 4; A: 7 numbers counting 4 vertices, since Svg.Skia may cut an arc into several segments, wide and unmeasured; Z adds a vertex), line 2, rect, circle and ellipse 5. Positions: start 1, mid (vertices - 2), end 1, for the slots set. Marker renders: the marker's subtree with references expanded, counted bottom-up over `Measure`'s graph after its cycle check, **including the placements of any marked element inside it** (an inner marked element adds its own positions x its marker's renders), so a nested marker multiplies by the outer one's positions (the red team's M7).
   - **No base term** for a plain large document (the table's last caveat).
   - **The guard's own cost stays bounded.** Every new count is computed iteratively in the reader pass or in `Measure`'s order (bottom-up subtree and marker renders in reverse topological order), with saturating arithmetic; nothing recurses. Today's guard costs 184 ms at the 8 MiB cap; the new counts, the second pass for attribute keys and the `d` parsing add to it, so delivery measures `Check` again on step 0's 8 MiB documents and records it. Per projection the guard's total is bounded by the byte pools: 16 MiB per reading projection (`RCVM:888`) and per leaf batch (`S/Panels/RightPanePanelsViewModel.cs:50`), so at most two 8 MiB guard runs each.

1b. **Selectors whose cost is not linear** (the red team's B1, by reading the IL; measured in the follow-up). ExCSS's query composes each combinator step onto the next and removes duplicates only at the end. A descendant step costs the summed subtree sizes of the elements its left compound matched, duplicates included, so nested groups cost about n x depth per step and each further step multiplies again (`<style>g g g g g g rect{fill:red}</style>` over 254 nested `<g>` passes today's guard and estimates microseconds, and by arithmetic runs for hours). A general sibling step (`~`) costs the later siblings of every left match, n² over flat siblings; `:nth-*`, `:*-of-type` and `:only-of-type` recompute the parent's children per element, also n². The combinator weight of a branch is the product over its steps of: 1 for a child or adjacent-sibling step; for a descendant step, the summed subtree sizes of the elements filed under the left compound's key divided by all elements (at least 1); for a general-sibling step, the summed later-sibling counts of those elements divided by all elements; and, for an nth or of-type pseudo-class anywhere in the branch, the largest sibling list. Subtree sizes and sibling counts come from one bottom-up count over the parent edges the guard already records. The weights are wide by construction (filing by key over-counts matches); the follow-up's measurements decide their rate, and the spec's facts pin each shape.

2. **Filing a rule, so matched declarations are not counted wide.** Each selector branch is filed under its subject, the rightmost compound: its id, else its first class, else its type; an attribute-only subject under `[name=value]` for an exact match and `[name]` for any other operator; a pseudo-only or universal subject under `*` (errs wide, as today). Matched declarations are summed in `Measure`'s existing loop over elements (`G:568-599`), which already looks up the `*`, type, `#id` and `.class` keys; the sum covers every element, while the loop's reference edges keep their `TakesStyleRules` filter (a `desc` matched by a rule pays for the rule's declarations but draws nothing a rule's `url()` could reach). Attribute keys are known only once the stylesheet has been read, at the document's end, so when any rule is filed under an attribute key the guard reads the document a second time, collecting only the attributes those keys name (at most one more pass, about the first's cost, and only for documents with attribute selectors). Today's `url()` rule targets keep their wide filing for the reference graph (`AddRuleTarget`, `G:484-504`), which is a different question (which elements a rule's references may reach) and is not changed.

3. **The five corrections step 0 found in the first estimate** (`decision.md`, "A variant") are in points 1, 1b and 2: attribute keys, a combinator branch weighed (step 0 measured a flat descendant selector at twice the query cost, which Design 1b's weight gives; deeper nesting is Design 1b's), custom-property and `var()` declarations at 0.5 us, `var()` given up at its depth as Svg.Custom does (a fallback excepted), and the use instance's subtree. Without them the same 2 s limit refuses three documents that decode in under 1 s (attribute selectors estimated 14x high; two `var()` chains past the depth that Svg.Custom abandons at once: C_double D=16 E=1000 estimated 6.3 s, decoding in 0.03 s, and C_kary K=3 D=16 E=1 estimated 2.75 s, decoding in 0.01 s). Call 3.

4. **Where the guard runs: before the decoder slot.** `DecodeSvgImage` (`EI:3529`) runs the whole guard on the caller's worker after the memory and the deadline and before the cap: memory, deadline, guard, cap, thread. A `DecodeCost` refusal (and a `RenderedElements` refusal, Call 6) returns the new sentence with no slot, no thread, no pixels and no deadline charge (the deadline counts decode time, and nothing decodes); any other refusal returns null with no reason, as today, and now starts no thread either. `RenderSvgToPng` no longer calls the guard: its caller has. `SvgDecodeRefusalBeforeDecoding` (`EI:3501`), which the reading view asks once its pixel pool is spent, gains the cost refusal after the memory and the deadline, so a document refused for its cost says the cost sentence there too and not the pool notice. A cost refusal is not remembered (CB-28's memory holds timeouts only): the guard is deterministic, and running it again costs at most its own time (17 ms or less below 2 MB, 184 ms at 8 MiB) per decode request. The guard's time is not charged to the projection's deadline.
   - **The same exception filter, on the caller.** Today the UTF-8 decode (`throwOnInvalidBytes: true`) and the guard sit inside `RenderSvgToPng`'s catch (`EI:3778-3846`). On the caller they keep that filter: a non-fatal exception (an SVG with invalid UTF-8 that sniffs as `<svg`, or any defect in the new counting) gives null with no reason and a log line, never an exception into `FetchEmbedResolutions` (whose contract is "never a failed projection", `RCVM:991`), the leaf's batch (`BuildEmbedPreviewNode` sits outside its `try`, `S/Panels/RightPanePanelsViewModel.cs:727-731`) or `ResolveDiagramOutcome`. The string is decoded once on the caller and handed to the decoder thread (the red team's M3).
   - The test seam `SvgDecodeBodyForTests` (`EI:3563`) now sits behind the guard: a fact that feeds it bytes the guard refuses no longer reaches its body. One fact does: `SvgPreview_OnlyATimeoutAtTheFullBudgetIsRemembered`'s ordinary-failure arm feeds `<svg …><unclosed` to a body that returns null (`T/W2EditorInteractionRedTeamTests.cs:938-951`); it is rewritten to well-formed bytes of its own (the body still returns null), and the PR names it. The red team reread every other user of the seam: they feed 8x8 SVGs the guard passes.
   - Two existing facts keep passing but would test a guard refusal in place of a decode failure: `SvgPreview_AnOrdinaryDecodeFailureKeepsTheCorruptImageSentence` (`T/W2EditorInteractionRedTeamTests.cs:522-538`) and the second half of `TheEditorEmbedShowsCoresSentenceAsAWarning` (`:229-246`) decode `<svg …><unclosed` with the real body. Both get a well-formed SVG that Svg.Skia rejects (zero width and height, which `RenderSvgToPng`'s bounds check returns null for, `EI:3801-3808`), so they still pin a decode failure (the red team's L4).
   - **The out-of-process probe** (`apps/slate-windows/tools/SvgDecodeProbe/Program.cs:31-33`) prints "abandoned" for any non-null failure reason. With Call 6, a `RenderedElements` refusal carries the cost sentence, so `SvgDecodeOutOfProcessTests.RefusesAHostileDocumentWithoutEndingTheDecodingProcess("exponential use fan-out")` (`T/SvgDecodeOutOfProcessTests.cs:50-54`, `:73-74`), which expects "refused", would fail. The probe maps the cost sentence (`SvgDecodeCostRefusedMessage`) to "refused" (the red team's B2).

5. **The sentence.** A seventh form of `render_time_exceeded_message` in `D`: `"image-cost"` → "Image not shown: it would take too long to decode." (the `seconds` argument is not used; the function's doc says so). On Windows, `SvgDecodeCostRefusedMessage => RenderTimeExceededMessage("image-cost", SvgDecodeBudgetSeconds)`, read once, beside `EI:3276-3299`. No signature change and no `lib.rs` edit: its doc points at core's list since CB-28. The Mac has no consumer of `render_time_exceeded_message`.

6. **Where the reader meets it.** Every SVG decode goes through `DecodeSvgImage`: the reading view's embed card (`RCVM:1126`), the Ctrl+E popover and the embeds leaf (`BuildImageNode`, `EI:1903-1914`, which shows `failureReason` in place of "Could not decode image"), and a diagram's card (`RDB:1154-1166`: header "Diagram could not be rendered", reason the new sentence, not transient). By step 0 no real diagram comes within 26x of the recommended limit.

7. **Log.** A new `HostDiagnosticEvent.SvgDecodeRefusedForCost` with `estimateMs=…, term=…` (the largest term's name); numbers and a term name only, no document content (the privacy fact, `T/W1HardeningTests.cs:27-52`, iterates every member).

8. **Not changed.** The 5 s budget, the cap, CB-28's memory and deadline, the 8 MiB and 1,120 px caps, the pixel pools, the guard's existing refusals and constants, raster decodes, the diagram stage's landing rules. The two owner questions CB-28 left (#1955: a deadline for the diagram stage; a bounded cache of successful decodes) are not touched: a cost refusal needs neither a transient outcome nor a cache.

## Tests

**Red first, against an inert surface.** The new members (`SvgRefusal.DecodeCost`, the estimate type, the rates, the limit, the log event, the message property, the test generators) do not exist on `main`. The branch's first commit adds them inert: the refusal never returned, the estimate computed as zero, the message property returning the diagram form (core's form lands with the behaviour). The facts are seen red on that commit for the reasons below, then the behaviour lands. The core fact is red on `main` itself.

The generators are the step 0 probe's, moved into a test support class (`T/Support/SlowSvgDocuments.cs`): each kind parameterised as in `calibration.md` §2. No fact decodes a slow document: every slow-document fact asserts the guard's verdict or the decode path's refusal, which never starts a thread.

**Core, `D`, `mod tests`:**

1. `render_time_exceeded_message_has_its_forms` gains `render_time_exceeded_message("image-cost", 5)` == "Image not shown: it would take too long to decode.", and the same for another `seconds`. Red today: `"image-cost"` falls to the diagram form. Mutation: a changed literal.

**Windows, `T/SvgPreviewGuardTests.cs`:**

2. `RefusesEachSlowDocumentForItsCost`, a theory, every row sized to estimate past 10 s so it is refused at any limit the owner picks: #1581's four reproductions (A_star_g R=3000 E=13000, A_star_rect R=15000 E=8000, C_double D=15 E=5000, D_marker M=2000 V=10000); the scatter at 10,000 points with its rule; a one-rule stylesheet over 10,000 uses; markers inherited from a `<g>` (M=100 over 5,000 rects); one row per Design 1b shape at a size the follow-up measured past 10 s; a `var()` fallback chain past the depth limit (the follow-up's shape); `desc{…}` of 1,000 declarations over 10,000 `<desc/>`; a nested marker (the follow-up's shape); and `var()` with four references per level at 15 levels (C_kary K=4 D=15, about 1.07e9 copies), which must also return in under 100 ms of guard time (its own stopwatch; M5). Each `Check` returns `DecodeCost`. Red on the inert surface: `None`. Mutations: drop each term in turn (its rows fail); no inheritance (the inherited row fails); matched declarations over styled elements only (the `desc` row fails); `var()` fallbacks ignored; the nested marker counted once; the `var()` expansion by recursion (the K=4 row's time fails).
3. `EstimatesTrackTheCalibration`: for one document per term at a known size (A_class R=5000 E=6000; A_list R=2500 S=4 E=10000, where branches and rules differ; A_type R=1000 E=3000; A_use R=1 G=4 U=3000; C_double D=15 E=1000; D_marker M=500 V=1000), the estimate is within 10 % of the `est2Ms` value the committed calibration table records for it (Call 12), so a later change to the counting is seen against the calibration. Red on the inert surface (0). Mutations: a rate off by 2x; branches counted as rules (A_list fails). The "all elements replaced by styled elements" mutation is dropped here, since these documents differ by one element; fact 2's `desc` row bites it.
4. `AdmitsWhatDecodesQuickly`, a theory of shapes the corrections exist for, each with its own bound from the calibration (so the bound sits well below the limit and above the row's estimate): attribute selectors (A_attr R=300 E=3000), a `var()` chain past the depth limit with no fallback (C_double D=16 E=1000, decodes in 0.03 s, estimate about 0), the scatter at 500 points with its rule, 10,000 uses with no `<style>` (decodes in 2.0 s, no use term), and markers set only at a line's start and end (D_start M=500 V=1000, estimate under 200 ms, so the "every marked element is `marker-mid`" mutation, which gives it 1.33 s, still bites at the bound). Each returns `None`. Green on the inert surface (estimate 0); it pins the corrections. Mutations: attribute subjects filed under `*`; `var()` capped at 15 levels instead of given up past 16; the use term without its `<style>` condition; `marker-mid` assumed for every marked element.
5. `AcceptsTheShapesRealDocumentsUse` (`:44`) gains an assertion that each shape's estimate is under a tenth of the limit, and the repository's `pie.svg`. Green throughout.
6. `RealDiagramsStayFarBelowTheLimit`: renders through core (the bindings' diagram render) a sequence diagram of 300 messages, a class diagram of 150 classes and a state diagram of 150 states, sized to render in about a second each so core's 5 s per-diagram budget never cuts them on a slow job VM (the red team's M2: step 0's largest sat just under it), and asserts each SVG passes `Check` with an estimate under a thirtieth of the limit. The marker term, the only one real diagrams carry, grows linearly with the edges, so a thirtieth at about a third of step 0's largest keeps the largest at about a tenth; the committed calibration records the largest's 76 ms. Green throughout.
7. `RefusesEachDocumentPastACap` (`:96`) keeps its rows and their refusals: the existing caps are checked before the estimate, so a document past one still says which.

**Windows, the decode path, `T/W2EditorInteractionRedTeamTests.cs`** (through `WithSvgDecodeSeams` with their own slots):

8. `SvgPreview_ADocumentRefusedForItsCostSaysCoresSentenceAndStartsNoDecode`: a generated document past the limit, a body seam that counts calls: `DecodeImage` returns null with "Image not shown: it would take too long to decode."; the body was never called; `Live` and `Abandoned` stay 0; a second request does the same (not remembered: the memory's count is unchanged); `SvgDecodeRefusedForCost` was logged. Red on the inert surface: the guard passes it and the body runs. Mutations: remember the refusal (the second request reads the memory); run the guard after the slot (caught by fact 9, since `Live` is back to 0 before fact 8 asserts).
9. `SvgPreview_ACostRefusalIsSaidEvenWhileTwoDecodesAreAbandoned`: `slots.Abandoned = 2` (the cap); the same document still says the cost sentence, not the busy one. Red on the inert surface (busy). Mutation: the cap before the guard.
10. `SvgPreview_AStructuralRefusalStartsNoDecodeAndKeepsTheCorruptImageSentence`: a document with `<script>` and one with an external `href`: null, no reason, the body never called, `Live` 0. Red on the inert surface: the body runs (the guard was inside it). Mutation: structural refusals still go to the thread.
11. `SvgPreview_ARenderedElementsRefusalSaysTheCostSentence` (Call 6): a symbol used past 200,000 renders: the cost sentence. Red on the inert surface: no reason. Mutation: `RenderedElements` mapped to no sentence.
12. `SvgPreview_OnlyATimeoutAtTheFullBudgetIsRemembered` (existing, `:930`): its ordinary-failure arm's bytes become well-formed (Design 4); the fact keeps its assertions. `SvgPreview_AnOrdinaryDecodeFailureKeepsTheCorruptImageSentence` and `TheEditorEmbedShowsCoresSentenceAsAWarning` get a zero-size SVG (Design 4) and keep theirs.
12b. `SvgPreview_InvalidUtf8BytesGiveNullAndNoException`: bytes that sniff as `<svg` with an invalid UTF-8 sequence: null, no reason, no exception, no thread (Design 4's filter on the caller). Green on the inert surface (the decode still sits inside the thread's catch there); it pins that the filter moves with the decode. Mutation: drop the caller-side filter.

**Windows, the out-of-process probe, `T/SvgDecodeOutOfProcessTests.cs`:** `RefusesAHostileDocumentWithoutEndingTheDecodingProcess("exponential use fan-out")` keeps "refused" through the probe's new mapping (Design 4). Mutation: the probe without the mapping.

**Windows, the reading view, `T/ReadingSvgEmbedDecodeTests.cs`:**

13. `AnSvgRefusedForItsCostSaysSoInItsCard`: a note embedding one document past the limit and one small SVG: the first card says the cost sentence, the second shows its image; the decode body ran once (the small one). Red on the inert surface: the slow card runs its body. Mutation: the reading view's fetch bypasses the guard.
14. `AnSvgRefusedForItsCostAfterThePoolIsSpentStillSaysTheCostSentence`: the pixel pool spent by an earlier image, then the costly SVG: the cost sentence, not the pool notice (Design 4's change to `SvgDecodeRefusalBeforeDecoding`). Red on the inert surface (the pool notice). Mutation: the helper without the guard.

**Windows, diagrams, `T/ReadingDiagramTests.cs`:**

15. `ADiagramWhoseSvgIsRefusedForItsCostShowsCoresSentence`: a diagram artifact whose SVG is a generated document past the limit: the card's header is "Diagram could not be rendered", its reason the cost sentence, the outcome not transient, and the decode body never ran. Red on the inert surface. Mutation: the diagram path's outcome marked transient.

**What else runs.** `SvgPreviewGuardTests`, `W2EditorInteractionRedTeamTests`, `ReadingSvgEmbedDecodeTests`, `ReadingEmbedTests`, `ReadingDiagramTests`, `EmbedsLeafSvgDecodeDeadlineTests`, `UnhurriedSvgDecodesTests`, `SvgDecodeOutOfProcessTests`, `W1HardeningTests`, the censuses that read these files, then the whole unit run under the desktop lock before the push. Rust: the four gates.

## Files

- `crates/slate-core/src/diagram.rs`: the form, its doc line, fact 1.
- `S/SvgPreviewGuard.cs`: the estimate, the filing, the second pass, `DecodeCost`, the rates and the limit.
- `S/EditorInteractions.cs`: the guard before the slot, `RenderSvgToPng` without it, `SvgDecodeRefusalBeforeDecoding`, the message property.
- `S/HostLog.cs`: one `HostDiagnosticEvent` member.
- `S/Reading/ReadingEmbedArtifact.cs`: its `FailureReason` doc names the cost refusal (a comment).
- `S/Reading/ReadingDocumentBuilder.cs`: the comment at `:1158-1159` gains "or refused for its cost" (a comment; the red team's L6).
- `apps/slate-windows/tools/SvgDecodeProbe/Program.cs`: the cost sentence maps to "refused" (Design 4).
- `docs/plans/47_windows_core_closeout/calibration/cb29/`: `calibration.md`, `decision-tables.md`, `est.py` and `est.csv` from step 0, so fact 3 and the owner's table cite committed evidence (Call 12).
- Tests: `T/SvgPreviewGuardTests.cs`, `T/W2EditorInteractionRedTeamTests.cs`, `T/ReadingSvgEmbedDecodeTests.cs`, `T/ReadingDiagramTests.cs`, and the new `T/Support/SlowSvgDocuments.cs` (`git add`, then `python3 scripts/apply-license-header.py`).

Not edited: `crates/slate-uniffi/src/lib.rs` (Design 5; its doc comment at `:8072-8076` says `seconds` is the budget a sentence names, which the `"image-cost"` form does not use; that inexactness is accepted, because the comment points at core's list and an edit would change the function's checksum in both bindings for a doc word: the red team's L5), `RCVM`, any Mac source, `a11y.rs` and the corpus (no `A11yEvent`), contract 38's ledger (`--check` is run), any manifest or lockfile, `windows.yml`, `TestWaitCensus.cs`, the parity matrix, help documents.

## Overlap

- **This wave.** CB-31 edits `crates/slate-core/src/links_db.rs`, `session.rs` and their tests, and one Windows test file (`BasesRedTeamRegressionTests.cs`); CB-15b edits the Windows shell's canvas, graph and search sources and `ShellFailureDetailCensus`. None is in this chunk's list. `ShellFailureDetailCensus` keeps dated rows in `EI` (CB-15a's 61 rows); this chunk adds no read of an exception's `Message`, and if it moves a row's line in `EI`, the second of CB-15b and this chunk to merge reruns the census.
- **Next waves.** CB-15c and CB-22 follow in `EI` and `D`.

## The NVDA spot check

Base is this worktree's step 0 Release build, copied aside before the branch is built and launched with `-Exe`; head is the PR's Release build. Each launch goes through `scripts/windows/launch-for-field-pass.ps1` with `-Root` (#1995) and `-Vault`, under the desktop lock, with NVDA's version recorded and the app's log directory kept. Before the pass: no home-pool job running.

The vault: `slow.md` holding "Before.", four slow embeds, `![[sheet.svg]]` (#1581 row A's reproduction, 40 s), `![[props.svg]]` (row C's, 35 s), `![[marker.svg]]` (row D's, 50 s) and `![[scatter.svg]]` (the scatter of 10,000 points, 17 s), then `![[ok.svg]]` (the repository's pie), and "After."; and `diagram.md` with a small flowchart. The leaf decodes every embed of the active note on every activation (CB-28's finding), so each step waits for the leaf's batch to settle (its decode lines in the log stop) before reading mode. The pie comes after the slow ones and the diagram is opened within 30 s, so the pass hears what the slow documents cost the other images on base (the red team's L1).

1. Launch, Ctrl+O, "slow", Enter. Wait for the leaf. Ctrl+Shift+E, then Down through the cards.
   - Base, predicted: in the leaf, sheet and props run out their 5 s and are abandoned, which fills the cap of two, so marker, scatter and the pie are refused at once as busy (about 10 s in all). In the reading view, sheet and props are remembered ("Image not shown: decoding took longer than 5 seconds.") and marker, scatter and the pie say "Image not shown: earlier images are still decoding past their 5-second budget.", while the two abandoned decoders run on for up to 35 s more.
   - Head, predicted: the leaf and the reading view each in about a second; the four slow cards say "Image not shown: it would take too long to decode."; the pie shows; no decoder thread runs past the landing.
2. Within 30 s of step 1's landing: Ctrl+O, "diagram", Enter, Ctrl+Shift+E. Base, predicted: the flowchart's card says "Diagram could not be rendered" and the busy sentence, because the abandoned decoders still hold the cap. Head: the flowchart renders and its description is heard as today.

The PR records what NVDA says, the times from the log, and each step's wait for the leaf. The head pass may run on the placeholder limit: all four slow documents estimate past 10 s and are refused at any limit in the table.

## Docs and registers

- **Contract 38's ledger:** not regenerated; `python -I scripts/a11y_trigger_ledger.py --check` is run.
- **No contracts document records these sentences** (a search of `docs/plans/*_contracts.md` for "decoding took longer" and "too long to decode" finds nothing).
- **#1954 (Mac work owed):** no line for core's form (the Mac has no consumer of `render_time_exceeded_message`, so no Mac user hears anything different; plan §4 rule 5 does not apply). The Mac's embed view decodes SVGs with no budget and no guard (`EmbedView.swift:161`); CB-28 already put that on #1954, and this chunk's guard is that line's Windows remedy too: the line gains "and no cost guard (#1610)" if the lead wants it (Call 10).
- **#1955 (deferred finds):** whatever of Design 1b and the red team's M4, M6 and M7 the lead does not take here (Call 13), one line each with its failing test.
- **#1610:** closes on this PR, once the owner has picked the limit. Step 0's table goes in a comment on it now.
- **Plan 47 §12:** the delivery row, in the lead's docs PR, with two reading corrections from the drift table (mermaid output has no stylesheet; the guard's call site).
- **Found on the way, predicted:** the `var()` expansion is #1610's own third row and the use cost is scope taken under Call 2, so neither is a find (AGENTS.md: a defect in the PR's own feature is fixed on its branch). The red team's findings on the estimate (Design 1b, M4, M6, M7) are the feature's too. Found 1 (the probe tool's mapping, B2), fixed here 1, deferred 0 unless Call 13 defers, filed 0.

## Calls for the lead

1. **One estimate and one limit, or CD20's two limits.** CD20 says "the two limits" because the issue named two products. Step 0 shows four costs and a 30x spread between their rates, so a single count cannot serve, and two per-product limits leave `<use>` and `var()` unguarded (four documents past 30 s admitted, #1581's custom-property row among them).
   - **(a) Recommended:** one estimated decode time, the four terms summed in microseconds at this desktop's rates, one limit. The owner picks one number from the table. Cost: the owner is asked for one limit where CD20 said two (a departure the PR names); the rates are constants tied to one desktop.
   - (b) Two limits as CD20 framed them (a stylesheet estimate and a marker estimate), uses and `var()` not guarded, and one issue filed for them. Cost: the paragraph on two per-product limits under the calibration table.
2. **Scope: the two costs the issue misses.** The use-under-stylesheet cost is not in #1610; the `var()` expansion is the mechanism of #1610's own third row (the custom-property document), which the issue's "custom properties counted with the rules" does not capture. Recommended: both here, as terms of the one estimate. Cost: the `var()` graph is the largest new piece of the guard (about 100 to 150 lines), the use term about 20, and facts 2 to 4 carry their rows; the chunk grows from M toward L. Alternative: the two products only, and one issue for the rest.
3. **The five corrections** (Design 3). Recommended: take them. Cost: a second read of the document when a rule is filed under an attribute key, and a few comparisons per declaration. Without them the 2 s limit refuses three documents that decode in under 1 s.
4. **The limit (the owner's pick).** Recommended: **2 s** of estimated decode time at this desktop's rates: the largest round limit at which no document in the data that runs past the 5 s budget gets in, with the largest real document 26x below it and no document under 1 s refused. It refuses 16 generated documents that decode here in 1.4 to 4.7 s. 1 s refuses 18 more with no gain; 3 s and 5 s start to admit documents past the budget. The PR is built and tested with 2,000 ms as a named placeholder, and is not merge-ready until the owner answers.
5. **Where the guard runs** (Design 4). Recommended: before the decoder slot, after the memory and the deadline, the whole guard once, under the decode's own exception filter, `RenderSvgToPng` no longer running it. Cost: the guard's time on the caller's worker (184 ms at the 8 MiB cap today, under 20 ms below 2 MB; the new counts add to it and delivery re-measures), one existing fact's bytes change and two get a zero-size SVG (Design 4), and the probe tool's mapping. Alternative: the guard stays inside the thread and the refusal is carried out; then a cost refusal takes a slot, says the busy sentence while two decodes are abandoned, and is charged to the deadline.
6. **`RenderedElements` says the cost sentence too.** Today a document past 200,000 renders says "Could not decode image … may be corrupt", which is untrue; the cap bounds the same work. Recommended: yes (fact 11). Cost: one more sentence change on Windows, heard only on such documents. Alternative: leave it.
7. **The CI runner, and a second departure from CD20's recommendation.** The reading behind CD20 asked for the table "on this machine and on the CI runner" and a limit where a decode takes "about 1 s on the slower machine" (`reading/06_reading_view_diagrams.md:145`). This spec records one fast desktop and recommends 2 s estimated there. Plainly: that is a departure. Its arithmetic: the 2 s limit sits 2.5x under the 5 s budget, and the same 2.5x must absorb both a slower machine and the estimate's remaining per-kind error (under-estimates up to 3.2x for many small markers on many paths, D_paths), so it does not cover both at once; a document under-estimated 2x on a machine 1.5x slower than this one passes the budget while estimated under the limit (it then times out once and CB-28's memory refuses it for the rest of the process, which is today's behaviour for every slow document). Recommended: no runner measurement, the limit stated against this desktop, and the departure named in the PR. Alternative: a `workflow_dispatch` probe on the Namespace runner (one lane run, about 30 minutes) to record its rates beside these, and the owner picks with both columns.
8. **A real-world export.** No real export outside the repository was decoded: downloading one needs the owner's permission. The class most likely to meet the limit is a plotting library's scatter (one global rule plus a `<use>` per point), which by the worker's generated guess sits at the 2 s line near 3,000 points. Recommended: ask the owner, with the limit question, whether to fetch one real plotting export (or for the owner to supply one) before the limit is fixed. Cost: one owner step; nothing else waits on it.
9. **Fact 6's renders.** Recommended: render diagrams sized for about a second each (about 3 s in all), so core's per-diagram budget never cuts them on a slow job VM, and pin the largest's margin through the committed calibration (Call 12). Alternatives: commit step 0's three largest SVGs as fixtures (about 2.6 MB), or drop the fact.
10. **#1954.** Recommended: the CB-28 line about the Mac's `EmbedView.swift:161` gains "and no cost guard (#1610)". Cost: one edit to #1954's body by the lead. Alternative: nothing.
11. **Step 0's table on #1610 now**, before code. Recommended.
12. **The calibration evidence is committed.** Step 0's `calibration.md`, `decision-tables.md`, `est.py` and `est.csv` live in a scratch folder that a reboot can clear, and the owner's pick and fact 3 rest on them. Recommended: the PR commits them under `docs/plans/47_windows_core_closeout/calibration/cb29/` (about 200 KB of text; the generated SVGs are not committed, since the test generators rebuild them), and fact 3 cites the `est2Ms` column. Alternative: attach them to #1610. Cost: one more documents folder the plan does not list.
13. **Selectors whose cost is not linear, and three gaps the red team found** (Design 1b; `var()` fallbacks, M4; declarations on unstyled elements, M6; nested and `var()` markers, M7). None makes anything worse than `main`, and each leaves a slow document unguarded: the worst, deeply nested descendant selectors, by arithmetic holds a decoder for hours. Recommended: all four here, as Design 1, 1b and 2 now say, with their rates from the follow-up measurement and a row each in fact 2. Cost: more guard code (Design 1b's subtree and sibling counts, the fallback rule, the nested marker count) and about six more fact rows; the chunk is then L. Alternative: the estimate as first written, and one #1955 line or issue for these, with the spec's claims narrowed so nothing says the guard covers selectors in general.

### Lead rulings (2026-11-19)

Product code goes ahead on the 2,000 ms placeholder. The PR is not merge-ready until the owner picks the limit.

Put to the owner by the lead, answers to be relayed:
- Call 1: one estimated decode time and one limit, in place of CD20's two. The lead recommends it; the build uses one limit.
- Call 4: the limit; 2 s recommended, kept as a named placeholder.
- Call 8: whether to fetch, or for the owner to supply, a real plotting-library export before the limit is fixed.

Ruled by the lead:
- Call 2: taken here. The `<use>`-under-style cost and the `var()` expansion are terms of the estimate; the chunk grows, accepted.
- Call 3: taken here, all five corrections.
- Call 5: accepted. The guard runs before the slot, under the decode's exception filter. A cost refusal takes no slot, thread, pixels or deadline charge, and is not remembered.
- Call 6: accepted. `RenderedElements` refusals say the cost sentence.
- **Call 7: departure from the recommendation.** The CI-runner measurement is taken: one probe on Namespace, from a throwaway branch or draft PR, deleted afterwards. The table gains the runner's column, and its numbers go to the lead as soon as they exist, since the owner's pick may wait on them.
- Call 9: accepted. Fact 6 renders small diagrams.
- Call 10: accepted. The #1954 line on `EmbedView.swift:161` gains "and no cost guard (#1610)" (the lead edits #1954's body or tells the principal to).
- Call 11: accepted. Step 0's table goes on #1610 now, with no close word before any number.
- Call 12: accepted. The calibration evidence goes under `docs/plans/47_windows_core_closeout/calibration/cb29/` in the PR, checked first for local paths and user names.
- **Call 13: taken here, all four.** The chunk becomes L. Each cost gets a fact, red first, and the deeply nested descendant selector gets a fact that it is refused fast: a bounded guard time, asserted with the fact's own stopwatch, never a fixed delay. The follow-up's B1 rate goes to the lead when it lands.

Added by these rulings:

- Fact 2 gains the deep-descendant row's time bound: `Check` on `<style>g g g g g g rect{fill:red}</style>` over 254 nested `<g>` returns `DecodeCost` in under 100 ms of guard time on the test thread's stopwatch (no wait, no delay; the bound is the fact's assertion). Red on the inert surface: `None`.
- The table under "The calibration table" gains the Namespace runner's column once the probe has run; the probe's run id and the decode times of its reference documents go in the PR body.

The predicted count stays found 1, fixed here 1, deferred 0, filed 0.

## Verification

```powershell
$env:CARGO_TARGET_DIR = '<this worktree's own target directory>'
cargo test -p slate-core --lib diagram::tests::render_time_exceeded_message_has_its_forms
cargo fmt --all -- --check
cargo clippy --all-targets --workspace -- -D warnings
cargo nextest run --workspace   # make test-fast's sweep
cargo bench --workspace --no-run
& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh
python -I scripts/a11y_trigger_ledger.py --check
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SvgPreviewGuardTests|FullyQualifiedName~W2EditorInteractionRedTeamTests|FullyQualifiedName~ReadingSvgEmbedDecodeTests|FullyQualifiedName~ReadingEmbedTests|FullyQualifiedName~ReadingDiagramTests|FullyQualifiedName~EmbedsLeafSvgDecodeDeadlineTests|FullyQualifiedName~UnhurriedSvgDecodes|FullyQualifiedName~SvgDecodeOutOfProcessTests|FullyQualifiedName~W1HardeningTests"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <each edited .cs file>
python3 scripts/check_windows_census_trait.py
```

Then the whole unit run under the desktop lock. The PR's lanes: rust, windows in full, swift, a11y-check.

## Acceptance

- [ ] Step 0's table is in a comment on #1610 and in the PR body, with the owner's pick of the limit recorded (CD20's second step); until then the PR names its 2,000 ms as a placeholder and is not merge-ready.
- [ ] Fact 1 was seen red on `main`; facts 2, 3, 8 to 11 and 13 to 15 on the inert surface, each for its reason; 4 to 7 and 12 are named with their mutations; every mutation bites.
- [ ] A document refused for its cost takes no slot, starts no thread, reserves no pixels, charges no deadline, and is not remembered; it says exactly "Image not shown: it would take too long to decode." wherever an SVG is shown.
- [ ] The largest real diagrams stay under a tenth of the limit (fact 6).
- [ ] `Closes #1610`, and `gh pr view N --json closingIssuesReferences` shows only #1610; "close", "fix" and "resolve" stay away from #1581, #1635, #1908, #1954, #1955, #1949, #1950 and #2011.
- [ ] The body has the red team, the Codex rounds and verdict, the NVDA phrases on base and head, "Found and fixed here", the `not delivered` lines, and the count.

## Red team record

A fresh Opus agent at xhigh read the first draft against the tree at 16ad788d, step 0's record, the decision tables, `est.py` and `est.csv`, and the worker's IL listings (it ran the IL lister once more on the cached Svg.Custom): 2 blockers, 11 mediums, 11 lows. It found the drift table, the order of checks against every CB-28 fact, the seam users, the inert surface, the digest's stability and the calibration numbers (recomputed from `est.csv`) sound apart from the three it names. Every finding is taken:

- Blocker 1: selectors with combinators, general siblings and nth or of-type pseudo-classes cost more than linear time (by the IL), and the draft modelled them as two passes; step 0's descendant shape was one level deep. Design 1b weighs them, a follow-up measurement on `main` sets their rate, fact 2 gains a row per shape, and Call 13 puts the scope to the lead.
- Blocker 2: Call 6 would turn `SvgDecodeOutOfProcessTests`' hostile fan-out fact red through the probe tool's mapping. The tool maps the cost sentence to "refused"; Files and Design 4 name it.
- M1: fact 4's single bound failed two rows; each row has its own bound, with smaller documents. M2: fact 6's largest diagrams sat just under core's render budget; it renders smaller ones. M3: the UTF-8 decode and the guard keep the decode's exception filter on the caller; fact 12b. M4: a `var()` fallback re-descends after a failed reference; Design 1 counts it, the follow-up measures it, and presentation attributes count. M5: the guard's own time and memory are bounded (memo, saturation, no recursion) and re-measured; fact 2's K=4 row has a time bound. M6: matched declarations count every element. M7: a marker set through `var()` and a nested marker are counted. M8: three table figures corrected (5 s admits 5.2 and 5.3 s; 2 s refuses documents of 1.4 to 4.7 s; 10 s admits 5.2 to 11.8 s). M9: the calibration evidence is committed (Call 12). M10: fact 3 gains A_list and drops the mutation its documents could not see. M11: the second departure from CD20's recommendation (one machine, 2 s rather than about 1 s on the slower one) is named in Call 7 with its arithmetic.
- Lows: the NVDA predictions now follow the cap of two abandoned decodes, with the pie after the slow embeds and the diagram opened within 30 s (L1); fact 2's rows are each past 10 s, with a no-inheritance mutation (L2); fact 8's slot mutation is credited to fact 9 (L3); the two facts that decoded `<unclosed` with the real body get a zero-size SVG (L4); `lib.rs`'s doc inexactness is accepted and recorded (L5); the `RDB` comment gains its word (L6); the count follows AGENTS.md (L7); six wording errors fixed (L8); the guard's total per projection is bounded by the byte pools (L9); two formula terms clarified (L10); an arc counts four vertices (L11).

## Brief

Work in a worktree of its own on `claude/cb29-svg-cost-guard`, cut from `origin/main` at 16ad788d, with a `CARGO_TARGET_DIR` of its own. Copy step 0's Release build aside for the base NVDA pass. Post step 0's table on #1610. Commit the inert surface, then the facts (red on it), then the estimate, the filing, the guard before the slot and the sentence; make them green; run every mutation; the gates, the classes, and the whole unit run under the lock. Commit: `fix(windows): the svg guard refuses the documents that would take too long to decode`.

## The owner's answers (2026-11-20)

- **Call 1:** one estimated decode time and one limit, in place of CD20's two.
- **Call 4, the limit:** **3 s**, the owner's pick from the calibration table. The table now carries the CI runner's column. The lead recommended 3 s once the scatter numbers were in: at 2 s a matplotlib-shaped scatter would be refused from about 2,800 points, where it decodes in about 1.3 to 1.5 s on the calibration desktop. `MaxEstimatedDecodeMilliseconds` is 3,000 ms.
- **Call 8:** the owner approved the CI-runner probe run. No real plotting export was fetched.

## What delivery changed (2026-11-20 to 2026-11-21)

The branch was `claude/cb29-svg-cost-guard`, PR #2029, squash-merged on 2026-11-21 as e8956899 (pinned to head 137325cf). The judge's record is on the PR as comment 7058816967. Where this spec and the merge disagree, the merge stands, and the lines above are left as written.

**Step 0's follow-ups, both measured on `main`:**
- The spec red team read costs in the IL, and a measurement checked them. Descendant chains cost about depth^(K+1), general siblings n², and of-type pseudo-classes their sibling lists. nth-child is linear. Nested markers are not replayed. A var() fallback re-descends but is capped by the depth budget. Declarations on `desc`, `title` and `stop` cost at the custom-property rate. So Design 1b's weights became the measured counts.
- The CI runner (a Namespace EPYC-v4, run 44673476749, with the owner's approval) decodes about 1.13x the desktop's times overall, and 1.45x on stylesheets.

**The diff red team found 1 blocker, 3 majors and 6 minors:**
- the guard's own time was unbounded on the caller;
- CSS comment and escape tricks hid costs;
- `@keyframes` was read as rules;
- the owner's table was est2's and not the guard's.
All were fixed. The table is regenerated from the guard's own verdicts over 313 documents.

**Codex ran 8 rounds.** Two earlier launches returned no verdict; one was cut off by the provider's content filter on adversarial wording, and the focus was reworded in neutral terms.
- **Round 1:** four equivalent-input pairs, plus one from the aborted turn (a marker url in a var() fallback).
- **Round 2:** a step-budget bypass and per-instance branch queries.
- **Round 3:** a url() rescan, a use inside marker content, and per-instance declarations. The lead then had both classes closed structurally: every character a scan touches is charged, and one multiplier counts every cloned instance.
- **Round 4:** approve.
- **The merge judge** (Merge once the limit is recorded) asked for:
  - a limit pin;
  - cost refusals remembered, plus the guard's time charged to the note deadline;
  - the register's exporter routes measured.
  Patterns proved material. Each paint costs about 0.34 us per element of the document, so a fifth term was added. Clip paths were not material.
- **Rounds 5 to 7:** each found one false refusal or miss in the new pattern code. Fixed by, in turn: the full decoder cascade, var() scoped to the element, and fallbacks and unreadable paints read as the decoder reads them. Each was measured against the decoder case by case first; calibration.md section 9 has 150 measured cases.
- **The lead's ruling on currentColor changed** after measurement: the decoder draws it as the colour, so it clears a pattern.
- **Round 8:** on 7ae94ac1, which includes the owner's 3 s.

**The guard as delivered:**
- five terms: stylesheet, use instances, var(), markers and patterns;
- a step budget of 64,000,000, charged per character scanned;
- the guard runs on the caller before the decoder slot, and its time is charged to the note's deadline;
- cost refusals are remembered.

**Results:**
- Whole unit run on the final head: 8,161 passed, 0 failed.
- 134 live mutations bite.
- NVDA ran on 9c23729f; later stages change only the estimate.

**The count:** found 1 (the probe tool's mapping), fixed here 1, deferred 0, filed 0. Everything else the reviews found was in the PR's own feature.

**After the owner's pick:**
- **The lead's delta merge judge on 7ae94ac1: Merge after fixes, two majors.** Both were fixed red first in stage 11.
  - A declaration the decoder drops (an empty paint, an unreadable literal, an unclosed declaration) no longer wins the guard's cascade. A differential sweep of 558 rendered documents shows 55 false admits before and 0 after.
  - Custom-property scopes are linked rather than copied, charged per link, and capped at 20,000 entries. The judge's 738 KB document had allocated 1,754 MiB; it now allocates about 52 MiB.
  - A url() built through var() counts as a possible pattern or marker.
  - Cost refusals and timeouts are remembered apart.
  - The memory's hash is charged to the deadline.
- **Codex round 9: approve.**
- **The judge's re-review of c9f00133: Merge after fixes.** It verified every earlier finding by decoder pixels and withdrew its hsl row. Three new items were fixed red first in stage 12:
  - `url(#missing) inherit` keeps the parent's paint. A sweep of 14,688 rendered documents (the judge's 459 values written four ways at 8 templates) shows 10 hidden patterns before and 0 after.
  - Matching scopes are reused from the parent or a sibling, so root-rule definitions over 20,500 rects are admitted again.
  - An allocation fact now bites without linked scopes.
- **Codex round 10 on 1f31be9a: approve.**
- **The final head 1f31be9a:** the whole unit run passed 8,217 with 0 failed. All mutations bite except two equivalent ones (one rewritten so that it bites). guard-estimates.csv is unchanged since the 3 s pick.
- **Codex in all:** 10 rounds, ending with approves at rounds 4, 8, 9 and 10, plus two launches that returned no verdict.
- **The judge's third look at 1f31be9a: Merge after fixes.** Two neighbours of major A still hid a pattern: a reference written in another case (`URL(`), and escaped paints in the style attribute. On the lead's ruling, stage 13 closed the class by positive recognition. A winning paint clears the patterns below it only where the guard is sure of it: no backslash or comment, and a colour it parses, `none`, `currentColor`, or a reference to a unique id with nothing after it. Nit F was taken: 200,000 properties are refused at the cap before they are built (364.9 MiB, against about 2,089 MiB at 7ae94ac1). On the judge's 5,900 rendered documents, 12 patterns were hidden at 1f31be9a and none after.
- **Codex round 11 on 5ff69475: approve.**
- **The judge's fourth look at 5ff69475: Merge after fixes, G1 to G4.** Every hiding input was also hidden at 1f31be9a. G4 was a stage 13 regression on ordinary files: a comment anywhere in a stylesheet made every rule unsure. Stage 14 fixed all four on the lead's rulings, each measured against the decoder first:
  - G1: one rule keeps only its last declaration of each property.
  - G2: the guard is sure of a rule only where the decoder applies it: at the top level or in `@media` all, screen or bare, closed, with a selector list it parses, in an SVG-namespace style element with no type or text/css. Attributes and class tokens are read as the decoder reads them.
  - G3: a definition that is not plain may keep the parent's paint.
  - G4: comments are stripped as ExCSS strips them, and the stylesheet is read a second time with its comments and escapes marked.
  - Rules inside `@media` are read in document order (found on the way).
  On the judge's 7,508 rendered documents, 45 were hidden at 5ff69475 and none after, with 1,378 over-counts. Stage 14 changed only the steps column of guard-estimates.csv (177 of 313 rows, at most 15.5% of a row's steps); no estimate or verdict moved.
- **The owner ended the merge-judge loop after stage 14 (2026-11-20).**
- **Codex round 12 on 9f462970: needs-attention, one finding, a stage 14 regression.** A comment at the end of an `@media` header (`@media screen/* desktop */{...}`) makes the decoder drop the block, but the marked reading trimmed it and stayed sure. Codex's 6,000-rect document was admitted at 6.75 ms and decoded in 11.35 s. On the lead's ruling it was fixed in stage 14b, red first. The guard is now sure of an `@media` block only where its header, from the keyword to the block, holds no comment and no escape, and each rule takes the doubt of the marked reading. Ten rendered rows were added; four placements the decoder applies are over-counted. Codex's document decodes in 9.3 to 9.5 s here and is refused at an estimated 12,275 ms. The sweeps stay at 0 hidden, and the estimate table regenerates byte for byte. Four mutants were run; one is equivalent. Whole unit run on 8e68457a: 8,350 passed, 0 failed. The final head 137325cf only moves a doc comment.
- **Codex round 13 on 137325cf: approve,** with no material findings.
- **Codex in all:** 13 rounds, with approves at rounds 4, 8, 9, 10, 11 and 13, plus two launches that returned no verdict.
