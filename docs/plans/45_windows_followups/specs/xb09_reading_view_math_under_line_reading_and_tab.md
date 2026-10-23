# XB-9 — Reading a note line by line speaks a math block's speech, and the source is heard on request only

Issues: #1595 (closes here); #1591 (`Refs`; its Windows half is delivered here, the mac half closes it from plan 43's queue). Size M. Lanes: windows (unit lane and the FlaUI shell gate's reading rows), rust + swift (compile) if the source sentence is added to core's `a11y.rs` (Design 4). AT: NVDA full pass, Narrator spot. Wave: step 0 (the spike) in X3 slot 3; the chunk in X4 slot 2. Depends on: XD2 (settled by the spike's record). Parallel with: XB-8, XB-10a. Interlock: plan 44's WB-9, WB-11, WB-20, WB-21b, WB-22 edit `Reading/ReadingDocumentBuilder.cs` and `ReadingSurface.cs`; XB-9 lands before WB-9 (plan 45 §6). Codex adversarial review per plan 44 §4 rule 5 (size M).

## Goal

In the Windows reading view, Down and Up through a note speak each math block as its MathCAT speech, whether WPFMath drew it, could not draw it, or core fell back; focus returning to the view with the caret on a math block speaks the same; Tab onto a math element speaks its speech and role and nothing more; the authored LaTeX is spoken only when the reader asks for it, by a chord the help line names. The raw-LaTeX criterion of #1561/#1562 then holds on every reading path.

## Current state on main

- **The element.** `apps/slate-windows/src/SlateWindows/Reading/ReadingMathElement.cs` (88 lines): `ContentControl`, `Focusable` (`:27`), `SetName(speech)` (`:28`), `SetHelpText(source)` (`:33`, comment `:29-32`: "JAWS 2022+ skips HelpText when Name is set"), `ToolTip = source` (`:34`), `SetItemStatus(braille)` when present (`:40-43`); properties `Speech` `:48`, `MathMl` `:51`, `Braille` `:55`; no `Source` property. Peer `ReadingMathElementPeer : FrameworkElementAutomationPeer, IMathMlUiaSource` (`:68-69`): `ControlType.Group` (`:80-81`), localized type `"math"` (`:83`), control and content element (`:85-87`), `MathMl` (`:78`) served through `MathMlUiaProperty` (`MathMlUiaProperty.cs:45`, GUID `:47-48`).
- **The two shapes.** `ReadingDocumentBuilder.cs` `MathBlockElement` (`:512-608`; dispatch `:337-338`; doc `:498-511` "degrade to the source IN the text range — never silently absent"): match by display style and byte range (`:517-520`), live-source coherence (`:531-538`, `StripMathDelimiters` `:629-643`), speech (`:540-542`, fallback "Math expression."), `fallbackSource` (`:543`); **unmatched** (`:545-550`): `MonospaceParagraph(fallbackSource)` + `MarkMathBlock(…, fallbackSource)`, no element; render gate (`:566-574`, `MaximumRenderedFormulaSourceBytes` `:715` = 4 KiB, the per-note pool `:722`); **coverage gap / fallback** (`:575-594`): `MonospaceParagraph(fallbackSource)` with a zero-size `ReadingMathElement` inline (`:586-591`), `MarkMathBlock(…, speech)` (`:592`), comment `:578` "source stays readable in range"; **drawn** (`:596-607`): `ReadingMathElement { Content = visual }` in a centred `Paragraph(new InlineUIContainer(element))`. `TryRenderFormula` `:655-685` (WPFMath, `TexException` → null `:677`). Semantics: `ReadingSemantics.MarkMathBlock` `:213`, `IsMathBlock` `:219`, `MathSpeechOf` `:222`; the landmark `:1761-1766` uses `MathSpeechOf`.
- **Why line reading fails.** A drawn paragraph's only inline is an `InlineUIContainer`: WPF's text range blanks embedded objects, NVDA reports only the role ("grouping"). A coverage-gap paragraph's range is the source: NVDA reads the LaTeX with its `$$`. Tab focus reads Name, role and HelpText (the source) — NVDA does not skip HelpText (#1591's Windows comment).
- **Chords.** `ReadingNavigator.cs:85-86` Ctrl+Alt+M / +Shift (landmark `Math`), `Navigate` `:245-291` (miss `:277` → core `ReadingNavNoTarget`, "No next math." `a11y.rs:2571-2574`; landing `:289-290` → `ReadingNavLanded`, "{speech}, math." `:2575-2599`). Enter and Ctrl+Enter: `ReadingNavigator.cs:150-159` → `ReadingSurface.TryActivateAtCaret(brailleRequested)` (`:773`; math at `:807-838`: Ctrl+Enter announces `HostComposed(braille or "Braille not available.")` `:820-832`; Enter announces `ReadingNavLanded(Math, MathSpeechOf)` `:834-836`). `ChordTable.cs:1626` `("M", "math block")` → rows `windows.reading.nextM`/`previousM` (`:1630-1644`, `ChordScope.Reading`); no Reading row for Enter/Ctrl+Enter. The mac has "Source" in VoiceOver's More Content menu (`MathView.swift`).
- **Help line.** `Commands/NavigationHelp.cs:17-23` `Reading` names headings, links, lists, tables, "Tab moves through links and embedded controls. Enter activates." — no math; applied at `ReadingSurface.cs:65`; pinned by `NavigationHelpTests.cs:63` (ids at `:69`).
- **Tests.** `tests/SlateWindows.Tests/ReadingMathTests.cs`: `MathBlockRendersAFocusableElementSpeakingCanonicalSpeech` `:119`, `UnrenderableMathDegradesToSourceInRange` `:163`, `EnterAtTheCaretSpeaksTheMathBlock` `:218`, `CtrlEnterWithoutBrailleSaysSo` `:455`, `MathElementPeerServesTheConventionProperty` `:493`, and the prefs facts `:526-837`. Shell gate: `ReadingIdentityContract_ExeWindowAndSurfaceIds_AreStable` finds `ByLocalizedControlType("math")` (`ShellAccessibilityTests.cs:1479-1480`) and checks the MathML property (`:1492-1497`); `ReadingTextPattern_RangeFromChildResolvesEveryCustomPeer` (`:1720`, "math element" `:1810-1812`, order kinds `:1932`).
- **Registers.** `parity_matrix.md:234` (`math` leaf), `:271` (math artifact row), `:295` (Settings Math tab); `w_c_matrix.md:22` (Math blocks: Ctrl+Alt+M landing "{speech}, math.", Enter speaks, Ctrl+Enter braille). Contract 38's designations JSON and generated ledger (`scripts/a11y_trigger_ledger.py`) list every `A11yEvent` trigger.
- **Core.** `MathBlock` FFI record `slate-uniffi/src/lib.rs:7083-7092` (source, display_style, mathml, speech, braille, line, byte_offset); no status field; `get_math_blocks` `:2321`; host fetch `ReadingContentViewModel.cs:905-918`.

## Reference

XD2. Contract 39 (AT navigation) for the math landing phrases; `w_c_matrix.md:22`; the mac's custom-content rule in #1591 ("on request, never on focus"); AGENTS.md "Accessible output … is produced in `slate-core`".

## Design

0. **Step 0, the spike (one day, throwaway, recorded on #1595 before any product code).** A scratch branch builds the fixture note (`crates/slate-core/tests/fixtures/math_golden/MathCAT field pass.md`) in the reading view under three candidate shapes for the drawn and the coverage-gap paragraphs, and records NVDA 2026.2 and Narrator speech for Down, Up, caret re-entry (close a menu with the caret on the block) and Tab, per block: (a) **speech in range**: the paragraph's inlines are a `Run(speech)` whose visual is suppressed (zero-opacity foreground and a 0.1 pt font, or `Visibility` on a wrapping `InlineUIContainer`/`Span` — try both) followed by the drawing `InlineUIContainer`; (b) **element-only for every shape**: the coverage-gap paragraph becomes the drawn shape with a monospace `TextBlock` of the source as the element's `Content`, so every math paragraph's range is empty and the element's Name carries the speech, combined with the peer's `ControlType` tried as `Group`, `Image` and `Text` (NVDA reads an embedded object's Name for some control types and only its role for others); (c) **caret-entry announcement**: unchanged shapes plus `ReadingNavLanded(Math, speech)` when the caret enters a math paragraph. The record is a table (shape × reader × action × spoken). The chosen shape is the one where Down speaks the speech once, with no LaTeX and no bare "grouping", under both readers, and the gate's `ByLocalizedControlType("math")` and MathML property still resolve. The spike's branch is deleted; only the table survives.
1. **The paragraphs.** `MathBlockElement` builds all three cases (drawn, coverage gap, core fallback) in the chosen shape so that the paragraph's text range reads as the speech (or is empty with a Name NVDA reads, per the spike). The unmatched case (`:545-550`, no core artifact at all) keeps the source in range — there is no speech to speak — and gains the element so Tab and the chord still work. The "source stays readable in range" comment (`:578`) and the doc (`:498-511`) are rewritten to say what is now true: the source is visible (the drawing, or the monospace content) and spoken on request.
2. **HelpText.** `ReadingMathElement.cs:29-34`: `SetHelpText` is removed; `ToolTip` keeps the source for sighted hover; the comment names #1591 and the rule "on request, never on focus". Braille stays on `ItemStatus`. A `Source` property is added (the chord reads it).
3. **The source on request.** `TryActivateAtCaret` gains a third mode: `Enter` speech (today), `Ctrl+Enter` braille (today), `Ctrl+Shift+Enter` source (new; the builder checks `ChordTable` and `MainWindow` input bindings for a conflict and picks the nearest free Reading-scope chord if there is one, recording the choice). `ReadingNavigator.cs:150-159` routes the new modifier.
4. **The sentence.** Per AGENTS.md, core owns it: `a11y.rs` gains `A11yEvent::MathSourceSpoken { source: Option<String> }` rendering "Source: {source}." and "Source not available." (the unmatched case has one; the core fallback has one; only a missing artifact has none), with corpus entries beside the math landing goldens (`:4861-4869`, `:7276-7281`). This is an FFI change: regenerate both bindings (`make regenerate-bindings` is not here; `generate-bindings.ps1` for C#, and `swift-tests.yml` proves the Swift side), keep the Swift `A11yEvent` switch compiling (an explicit no-op case for the new variant, or `default`, whichever the file uses), and add the trigger's designation to `a11y_trigger_designations.json` + `python -I scripts/a11y_trigger_ledger.py --write`. If the owner prefers no FFI change in this chunk, the fallback is `HostComposed` like braille (`ReadingSurface.cs:827-831`) with the same two sentences — say which shipped.
5. **The help line.** `NavigationHelp.Reading` adds "Next math: {Spoken("windows.reading.nextM")}. Enter speaks it, Ctrl+Enter its braille, Ctrl+Shift+Enter its source." (the chord names from the table, never literals where a row exists); `ChordTable.cs` gains the Reading row for the source chord (`windows.reading.mathSource`); `chords.json` regenerated (`ChordsJson_IsExactlyTheTablesProjection`, `SLATE_CHORDS_UPDATE=1`); `generate-parity-matrix.py` rerun (`windows.yml` checks drift).
6. Not changed: Ctrl+Alt+M, the landing phrases, the MathML UIA property, the render budgets, core's speech. Mac: nothing (its half of #1591 adopts the same rule from plan 43's queue; XB-0 files the twin only if the chord has no mac counterpart).

## Tests

`tests/SlateWindows.Tests/ReadingMathTests.cs` (hosted, STA):
- `LineReadingTextOfADrawnFormulaIsItsSpeech` — build a note with `\nabla^2 f`; the math paragraph's `TextRange(ContentStart, ContentEnd).Text` (or, under shape (b), the element's Name with an empty range and the pinned control type) equals the speech and contains no `\` or `$`. Mutation: the old shape → "" (drawn) or the source.
- `LineReadingTextOfACoverageGapFormulaIsItsSpeech` — `\mathbb{R}` (WPFMath gap): same assertion; the source is visible as the element's content, never in the range.
- `LineReadingTextOfACoreFallbackIsItsSpeech` — `{n \choose k}` (core fallback, speech "Math expression: …"): the range reads the fallback speech, not `$$…$$`.
- `AnUnmatchedMathBlockKeepsItsSourceInRangeAndCarriesAnElement` — no core artifact: source in range (unchanged) and an element present.
- `TabFocusOnAMathElementCarriesNoHelpText` — `AutomationProperties.GetHelpText(element)` is empty; `ToolTip` is the source.
- `CtrlShiftEnterSpeaksTheSourceOnRequest` — the announcer receives the source sentence (core variant or `HostComposed`), once; `CtrlShiftEnterWithoutASourceSaysSo`.
- `UnrenderableMathDegradesToSourceInRange` (`:163`) is rewritten to the new rule and renamed `UnrenderableMathSpeaksItsSpeechInRangeAndItsSourceOnRequest`.
- `NavigationHelpTests.ReadingPeerPublishesTheTableDerivedNavigationEntryPoints` (`:63`) gains `nextM` and `mathSource`.
- Core (Design 4): `a11y::tests` corpus entries for the new variant; `corpus_renders_the_shipped_strings` (`:6823`).
- Shell gate: `ReadingTextPattern_RangeFromChildResolvesEveryCustomPeer` (`:1720`) and `ReadingIdentityContract…` (`:1381`) re-run; a new gate row only if the range shape changed what `RangeFromChild` returns (then pin it).
- **Field passes** (plan 44 §4 rule 4; back up `%LOCALAPPDATA%\Slate`): NVDA full — the fixture's 16 blocks: Down from the top (every block speaks its speech, no LaTeX, no bare "grouping"), Up from the bottom, caret re-entry after Esc from a menu on blocks 1, 4 and 13, Tab onto each (speech + "math", no source), Ctrl+Shift+Enter on 3, 13 and the unmatched case; Narrator spot — Down through blocks 1–4 and Tab onto 3. The phrases go in the PR body and a `w_c_matrix.md` row.

## Docs and registers

- `docs/plans/18_windows_port/w_c_matrix.md:22`: the row gains line reading and the source chord.
- `docs/plans/18_windows_port/parity_matrix.md`: regenerated (the new chord/help entry).
- `apps/slate-windows/chords.json` regenerated; `ChordTable.cs` row.
- Contract 38: designation for `MathSourceSpoken` (Design 4) and the ledger regenerated; contract 39: a dated addendum under the math rows (line reading speaks the speech; source on request).
- `ReadingMathElement.cs` and `ReadingDocumentBuilder.cs` comments.
- #1595: the spike table (step 0) and, at close, the field-pass phrases.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-xb9'
# Design 4 (core sentence): cargo test -p slate-core a11y; then regenerate bindings
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
$filter = 'FullyQualifiedName~ReadingMathTests|FullyQualifiedName~NavigationHelpTests|FullyQualifiedName~ChordTableTests|FullyQualifiedName~A11yTriggerParityCensus|FullyQualifiedName~ReadingShellToggleTests'
$n = (dotnet test $tests -c Release --no-build --list-tests --filter $filter | Select-String '^\s{4}\S').Count; if ($n -lt 25) { throw "filter selects $n facts" }
dotnet test $tests -c Release --no-build --filter $filter       # new facts red first
dotnet test $tests -c Release --no-build --filter "FullyQualifiedName!~ConnectionsLeafTests.TheModelOf&kind!=benchmark"
$gate = 'apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj'
dotnet test $gate -c Release --no-build --filter "FullyQualifiedName~ReadingTextPattern_RangeFromChildResolvesEveryCustomPeer|FullyQualifiedName~ReadingIdentityContract_ExeWindowAndSurfaceIds_AreStable"   # NVDA closed
$env:SLATE_CHORDS_UPDATE='1'; dotnet test $tests -c Release --no-build --filter "FullyQualifiedName~ChordsJson_IsExactlyTheTablesProjection"; Remove-Item Env:SLATE_CHORDS_UPDATE
python -I scripts/generate-parity-matrix.py; python -I scripts/generate-parity-matrix.py --check
python -I scripts/a11y_trigger_ledger.py --write; python -I scripts/a11y_trigger_ledger.py --check
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <every touched .cs>
cargo fmt --all -- --check; cargo clippy --all-targets --workspace -- -D warnings; cargo test --workspace; cargo bench --workspace --no-run   # if core changed
# NVDA full pass and Narrator spot per Tests; nvda.log recipe
```

CI lanes: `windows.yml` (unit, model shards, FlaUI), `rust.yml` + `swift-tests.yml` (compile) under Design 4. AT: NVDA full, Narrator spot; phrases in the PR.

## Acceptance

- [ ] Spike table on #1595 before product code; the chosen shape named with its reason.
- [ ] Down/Up/re-entry speak every block's speech; Tab speaks no source; the source chord speaks it; help line and chord table name the chords.
- [ ] The facts above green (red first, mutations recorded); the two gate rows green; `chords.json`, parity matrix, ledger regenerated and `--check` clean.
- [ ] NVDA full pass and Narrator spot recorded; `w_c_matrix.md` row.
- [ ] `Closes #1595`, `Refs #1591`. Not delivered: the mac half of #1591.

## Brief

Read `apps/slate-windows/src/SlateWindows/Reading/ReadingMathElement.cs` (all), `ReadingDocumentBuilder.cs:490-720`, `:1755-1770`, `ReadingSemantics.cs:205-225`, `ReadingSurface.cs:55-70`, `:770-840`, `ReadingNavigator.cs:80-160`, `:240-330`, `Commands/NavigationHelp.cs`, `Commands/ChordTable.cs:1595-1650`; `tests/SlateWindows.Tests/ReadingMathTests.cs:100-330`, `:450-520`, `NavigationHelpTests.cs:55-80`; `tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.cs:1470-1500`, `:1800-1820`, `:1925-1940`; `crates/slate-core/src/a11y.rs:100-115`, `:1410-1500`, `:2565-2600`, `:4855-4875`, `:7270-7285`; `docs/plans/18_windows_port/w_c_matrix.md:22`. Step 0: the spike on a scratch branch (shapes (a) speech-in-range with a suppressed Run, (b) element-only with `ControlType` Group/Image/Text, (c) caret-entry announcement), NVDA + Narrator, Down/Up/re-entry/Tab per block of the field-pass fixture; table on #1595; delete the branch. Build the chosen shape in `MathBlockElement` for drawn, coverage-gap and core-fallback cases (unmatched keeps the source in range and gains an element); remove `SetHelpText` (ToolTip stays), add `Source`; `Ctrl+Shift+Enter` (or the nearest free Reading chord) speaks the source through core's new `A11yEvent::MathSourceSpoken` ("Source: {source}." / "Source not available.") — bindings regenerated, the Swift switch kept compiling, the designation + ledger added — or `HostComposed` if the owner declines the FFI change; `NavigationHelp.Reading` names the math chords; `ChordTable` row + `chords.json`; parity matrix regenerated. Facts: the six new `ReadingMathTests` facts, the renamed `UnrenderableMath…` fact, the `NavigationHelpTests` ids, the core corpus entries, the two gate rows. NVDA full pass and Narrator spot as listed; `w_c_matrix.md` row; contract 39 addendum. Done when the filter selects ≥25 facts and passes, the full unit run and the gate are green, the four `--check`s are clean, `windows.yml` (+ `rust.yml`/`swift-tests.yml` if core changed) green, phrases recorded. `Closes #1595`, `Refs #1591`. Commit: `fix(windows): the reading view speaks a math block's speech when read by line, and its source only on request`.
