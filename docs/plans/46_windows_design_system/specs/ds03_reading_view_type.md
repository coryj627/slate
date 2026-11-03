# DS-3 — The reading view has the design's type scale and a 72ch measure

Issues: filed by DS-0. Size S–M. Lanes: windows. AT: NVDA spot. Wave 1, slot 4. Depends on: DS-1 (the font), DS-2 (`TypeRamp`). Decision: none. Interlock: plan 45's XB-9 is merged; math paragraph shapes in `ReadingDocumentBuilder.cs` aren't to change.

## Goal

The reading view is Open Sans 16/26, with headings at 32, 24, 20 and 18 and a measure of 72 characters, as the tokens say. A wide window has margins instead of a line a hundred and fifty characters long.

## Current state on main

- **Two documents:** the builder's at `ReadingDocumentBuilder.cs:208-215` and the surface's at `Reading/ReadingSurface.cs:81-86`, both `FontSize = 15`, `PagePadding = new Thickness(16)`, `ColumnWidth = double.PositiveInfinity`. Content arrives by merging blocks into the surface's document (`ReadingSurface.cs:78-80`), so what's on the builder's document doesn't reach the screen.
- **Headings:** `ReadingDocumentBuilder.cs:347-353`, 26, 21, 18, 16, semibold at `:354`, no line height.
- **No line height anywhere,** and no measure.
- **The preamble caption:** `ReadingDocumentBuilder.cs:810` `PreambleCaptionFontSize = 12`, asserted at `ReadingViewTests.cs:1552`.
- **Hidden speech runs:** `ReadingDocumentBuilder.cs:691-695`, `:700`. Their size is derived from their text, and they stay out of this.

## Reference

`design/windows/tokens/typography.css:34-40`: body 16/26, h1 32/40, h2 24/32, h3 20/28, h4 18/26, small 14/22, measure 72ch. `design/windows/readme.md:98`. `ui_kits/slate-windows/Document.jsx`, `ReaderView`.

## Design

1. **Keys.** `TypeRamp` gains `Slate.ReaderBody`, `ReaderH1` to `ReaderH4` and `ReaderSmall` (size and line height), so DS-5 scales them with everything else. They aren't ramp steps: the ramp is for chrome.
2. **Body size and line height.** The surface's document (`ReadingSurface.cs:81-88`) takes `FontSizeProperty` by resource reference, and so does the builder's (`ReadingDocumentBuilder.cs:208-216`), for facts that read it detached. The line height doesn't go on the document, for two reasons. `Block.LineHeight` inherits, so it would follow into the `TextBlock`s of cards and tables; and `BlockLineHeight` fixes every line's advance, so a math or diagram `InlineUIContainer` (`ReadingDocumentBuilder.cs:673`, `:1233`) would overlap what follows. Instead, an implicit `Paragraph` style in the document's `Resources` sets `LineHeight` 26 and keeps `LineStackingStrategy=MaxHeight`, the default, under which `LineHeight` is a floor: text lines are 26 and a taller inline grows its line. Open Sans at 16 is 21.8 natural, so body text is exactly 26.
3. **Headings.** `ReadingDocumentBuilder.cs:347-353` set `TextElement.FontSizeProperty` and `Block.LineHeightProperty` by resource reference. Open Sans's natural height passes the token at 32 and 24 (43.6, 32.7), so a heading with no `InlineUIContainer` takes `BlockLineHeight`; one holding inline math stays `MaxHeight`, so nothing overlaps. Levels 5 and 6 take H4, as `_ => 16` already folds them. Weight stays semibold. The design's "700 only for Reader H1" goes in with this: `FontWeights.Bold` at level 1.
4. **The preamble caption** becomes `ReaderSmall`, and `PreambleCaptionFontSize` becomes the key's name. Update the fact at `ReadingViewTests.cs:1552`.
5. **The measure.** `ColumnWidth` stays infinite (`ReadingDocumentBuilder.cs:212-214` gives the reason: multi-column reading order is a §W-C hazard). On `SizeChanged`, and when the body size changes, `ReadingSurface` sets `Document.PagePadding` to `Thickness(max(16, (ActualWidth - measure) / 2), 16, same, 16)`, where `measure` is 72 times the advance of `0` in the resolved Open Sans face at the body size. This keeps the scroll bar at the pane's edge, needs no rebuild, and can't move the caret. A rebuild is off the table because of the NVDA finding at `ReadingSurface.cs:78`.
6. **Blocks that aren't text.** Tables, code blocks and diagrams fill the measure. Check a wide table in the capture: `ReadingTableGrid.cs:41` opens its own 820-wide window, so it isn't affected.

## Tests

Failing first, in `ReadingViewTests.cs` or a small `ReadingTypeTests.cs`, hosted under `ThemeManager.DictionariesFor` so resources resolve:

- `TheBodyIsSixteenOnTwentySix`.
- `HeadingsFollowTheScale`: a theory over levels 1 to 6, with size, line height and weight.
- `AMathBlockDoesNotOverlapWhatFollows`: a tall block formula and the paragraph after it, hosted; the next paragraph's first line starts below the formula's rectangle.
- `ACardsTextKeepsItsOwnLineHeight`: a `TextBlock` in a `BlockUIContainer` reports no inherited 26.
- `AWideSurfaceHoldsTheMeasure`: at 1400 wide, a laid-out line's width is 72ch within one character.
- `ANarrowSurfaceKeepsItsPadding`: at 400 wide, padding is 16 and the text wraps.
- `AResizeKeepsTheCaret`: resizing leaves `CaretPosition`'s offset and the document's identity unchanged. This guards the rule at `ReadingSurface.cs:78-80`.

Launched app: `Markdown features`, `Linear algebra lecture 3` and `Code cookbook`, reading mode, at the default window and maximized.

## Docs and registers

None beyond `wpf-feasibility.md`'s type section if a number moves.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~Reading"`, then the shell gate's reading rows on the lane.

AT: NVDA, Down through a heading, a paragraph and a math block; Narrator heading navigation once. Line breaks move, so spoken lines change where they wrap and nowhere else. Record the phrases.

## Acceptance

- [ ] Sizes, line heights and weights match the tokens.
- [ ] A maximized window shows a 72-character line, centred, with the scroll bar at the edge.
- [ ] No fact had to accept a rebuilt document.

## Brief

Read `Reading/ReadingSurface.cs:70-96`, `ReadingDocumentBuilder.cs:204-216` and `:344-358`, `design/windows/tokens/typography.css:34-40`. Add the reader keys to `TypeRamp`, the implicit `Paragraph` style, the resource references on both documents and on headings, move the caption, add the `SizeChanged` padding. Facts first. Commit: `feat(windows): the reading view has the design's type scale and a 72ch measure`.
