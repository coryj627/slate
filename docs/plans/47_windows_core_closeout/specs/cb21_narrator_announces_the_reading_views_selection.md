# CB-21: Narrator announces the reading view's selection on Ctrl+A

Issues: Closes #1330. Size S. Lanes: windows (the app job's unit facts; the gate job runs as for any change under `apps/slate-windows/src`, with no new journey: Call 2). AT: a Narrator check on base and head (owed by the row) and an NVDA spot check on head (Call 4). Wave: C13, slot 1. Decision: CD13, adopted as recommended on 2026-11-16 ("Yes, as CB-21, diagnosis first: whether Narrator wants TextPattern2's caret range or another event. If the diagnosis finds no route Narrator honours, close with the record."). Review: an Opus xhigh red team on this spec (done; "Red team record") and on the diff, then Codex adversarial-review rounds until it approves, then the lead's merge judge.

Plan row, verbatim: "Narrator announces the selection on Ctrl+A in the reading view; diagnosis first".

Every `path:line` is as of `main` at b19c94ae, read on 2026-11-25. `S/` is `apps/slate-windows/src/SlateWindows/`, `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`, `G/` is `apps/slate-windows/tests/SlateWindows.AccessibilityTests/`. The driver scripts, the spike branch and every recap screenshot, trace and level log are in the principal's working notes (`drv/`, `fx/`), not committed.

## The answer to CD13's question

**Narrator does not want TextPattern2's caret range, or any other event. It wants a text range's `GetChildren` to answer an array.** WPF's text range adaptor (`MS.Internal.Automation.TextRangeAdaptor`) answers `ITextRangeProvider.GetChildren` with null when the range holds no element that is an immediate automation child (read in WPF 10.0.15's IL by the red team). The reading view's decorator forwards that null (`S/Reading/HeadingStyleText.cs:642`). The proof is the intervention: the same build, with only `GetChildren` answering an empty array in place of null, makes Narrator say "body selected" on Ctrl+A and "body" on its read-selection command, in two launches, where four control launches said nothing; on Files' filter field the same one change turned silence into "body selected". A route Narrator honours exists, so this chunk fixes #1330 rather than closing with the record.

The answer makes a second defect reachable, which the design fixes on the branch: once Narrator gets an array it asks for the whole selection's bounding rectangles, and WPF walks every line of the range to answer (Design 2; the red team's blocker).

## Step 0, as run

All runs on this desktop, 2026-11-25, Windows 11 Pro 10.0.26300, Narrator with stock settings, started and stopped with Ctrl+Win+Enter only, the app launched by `scripts/windows/launch-for-field-pass.ps1 -Root` on a fresh device-state root each launch. Keys went by SendInput only while Slate held the foreground. Speech was read from Narrator's speech recap window (Narrator+Alt+X), screenshotted after each case (`drv/recap-*.png`). A WASAPI loopback level meter (20 ms RMS bins over the default output, `drv/level-*.csv`) cross-checked whether anything sounded after a key and for how long. It cannot tell what was said, it hears every sound on the machine, and loopback capture writes rows only while something is playing, so "nothing after the key" is read from rows that are absent (a stalled capture would look the same; the recap is the record of speech). UIA events were logged by an out-of-process client over the whole Slate window. The driver's console output (the UIA event times, selection reads, pattern and caret readouts) was not saved to files; those numbers are as printed during the runs, and the delivery pass re-measures them with saved transcripts. Fixture: `trailing.md` (`body`, a blank line, `#`), WB-11's note, and `select.md`.

**Base: `main` b19c94ae, Release.**

| Id | Case | UIA, as printed | Narrator said (recap) |
|---|---|---|---|
| B1 | Reading view, Ctrl+Home then Ctrl+A | `TextSelectionChanged` on `ReadingSurface` 72 ms after the key; `GetSelection()[0]` reads `body\r\n` | "a" (Narrator's own key echo), nothing more |
| B2 | Narrator+Shift+Down (read the selection) after B1 | none | nothing |
| B3 | Editor, Ctrl+Home then Ctrl+A | `TextSelectionChanged` twice on `MarkdownEditor` | "a", "body  # selected" |
| B4 | Narrator+Shift+Down after B3 | none | "body  #" |
| B6 | Patterns, native UIA (CUIAutomation) | reading view: Document, Text yes, TextPattern2 no, Value no, the selection's IsReadOnly attribute true. Editor: Document, Text yes, TextPattern2 no, Value yes, IsReadOnly NotSupported | |
| B7 | Win32 system caret (`GetGUIThreadInfo`) | reading view: a 1 px caret before Ctrl+A, none after it (WPF drops it once the selection is not empty). Editor: kept with a selection | |

The event and the selection were right on `main`, as WB-11 found, and Narrator announced select-all in the editor, which has no TextPattern2 either (B6). The base run on Files' filter field lost its recap when Narrator exited, so there is no Narrator measurement of a text field on `main` itself; the field rows below are on the spike's own text box.

**Spike: one build of the spike branch (`claude/cb-21-spike`, 002aefa1, local only, never pushed), each route switched on by an environment variable, one launch per route.** Every row raised `TextSelectionChanged` on the surface and left `GetSelection()` reading `body\r\n`. In three launches Narrator took Ctrl+Home as its own command ("Not on table", its key having been left as held after the previous recap): the "ActiveTextPositionChanged" and "later TextSelectionChanged" rows and the second of the two empty-children launches. Ctrl+A reached Slate in all three (the event was logged), so their outcomes stand, but their starting caret differed from the others'.

| Route | What the spike did | Narrator after Ctrl+A |
|---|---|---|
| none (control, four launches) | nothing | "a" only |
| no decorator | the peer served WPF's own provider | "a" only |
| read-only attribute | the decorator answered `IsReadOnly` (40015) false | "a" only |
| not read-only | `IsReadOnly = false`, edits blocked by handlers ("Reading view, editing," on entry) | "a" only |
| Edit control type | the peer's control type Edit | "a" only |
| ActiveTextPositionChanged | WPF's public `TextAutomationPeer.RaiseActiveTextPositionChangedEvent(start, end)` on each selection change (seen by the logger) | "a" only |
| a later TextSelectionChanged | a second event 300 ms after the first (seen twice) | "a" only |
| Value pattern | a read-only `IValueProvider` on the surface | "a" only |
| no structural children | the peer's heading, list and code peers removed | "a" only |
| system caret | a Win32 caret kept at the selection's end every 100 ms, as AvalonEdit keeps one (`GetGUIThreadInfo` shows it after Ctrl+A) | "a" only |
| TextPattern2 | TextPattern2 (10024) added to WPF's private pattern table with a COM-visible `ITextProvider2`; `IsTextPattern2Available` true, `GetCaretRange` answers active 1 | "a" only |
| TextPattern2 and system caret | both | "a" only |
| Notification | the surface raises a UIA notification "<selected text> selected" | "body  selected" (not taken: Call 6) |
| call trace | every call a client makes on the reading view's and the editor's text providers logged | see below |
| **empty children** | the decorator answers `GetChildren` with an empty array where WPF answers null | **"a", "body selected"; Narrator+Shift+Down: "body"** (two launches) |
| filter field, traced | the filter field as a `TextBox` subclass of the spike's, its provider through a tracing decorator, WPF's null kept | "a" only |
| filter field, empty children | the same, with an empty array for null | "a", "body selected"; Narrator+Shift+Down: "body" |

**The call trace.** On Ctrl+A Narrator makes 300 to 400 calls on each provider. It calls `GetChildren` on several ranges, not only on the selection: format-unit ranges of the selection's first line, its own earlier caret range, and clones of the selection; it also calls `GetChildren` during focus and caret handling, before Ctrl+A. On `main` each of those answers null. After some of them the reading view's handling carries on as the editor's does; after others (a clone of its earlier caret range at 14:57:11.188 and a format range of the selection at 11.193) Narrator abandons its pass, starts the same handling again, and ends at 11.241 with no speech. The editor's ranges answer arrays throughout (Slate's own `EditorHyperlinks.Children`, `S/EditorSemanticText.cs:362`, `S/EditorHyperlinks.cs:152-158`). With empty children Narrator reaches its speech; that run's sequence is not the editor's call for call (it expands to Page and asks for the whole selection's bounding rectangles, Design 2). The trace shows where Narrator meets the null; the intervention above is the proof that the null is the cause.

**What a UIA client sees of the null.** Through the classic client API it sees nothing: through CUIAutomation and CUIAutomation8, on `main` and with the fix alike, the selection's `GetChildren` returns an element array of length 0 and `IUIAutomationTextRange3.GetChildrenBuildCache` returns null (measured at 15:03, as printed). How Narrator meets the provider's own answer is not identified. UI Automation's remote operations run provider calls inside the provider's process and would see the null, and the trace's abandoned-and-repeated pass fits one that fails; this is not confirmed. So the claim here is only that the classic client API cannot see the null, which is why WB-11's FlaUI journey passed and why the facts sit at the provider (Call 2).

**Driving notes, for the next Narrator pass.** After the speech recap Narrator kept its own key as held, so the next chord reached it as a Narrator command ("Not a Narrator command", "Not on table"); a lone Insert tap clears it, and the driver now taps it after every recap. A left Alt was found held seven times across the session (three spike runs at 14:33 to 14:35 were invalid for it and were run again); each was released with one key-up. Three times it made Ctrl+Win+Enter miss; Narrator was stopped on the next try after the release. Once (14:50:19) the lock was released before that was noticed, with Narrator still on and no Slate open; it was taken back at 14:50:30 and Narrator stopped at 14:50:43. Narrator exited by itself once, after the base filter case. Terminal windows of another session took the foreground four times between cases. The owner's Settings window was never touched. Slate's app data and the clipboard were backed up before each pass and restored after. The spike's local commit was first made with hooks switched off by mistake and was redone at once with hooks on; nothing of the spike is pushed.

**The red team's measurement (no screen reader, under the lock, `main`'s Release build).** On WB-11's `big-list.md` (1,996 items) in the reading view, from another process: `GetBoundingRectangles` on the select-all range took 2,541 ms and then 1,764 ms for 11 rectangles; `GetText(-1)` on the same range took 4 ms. A plain WPF `RichTextBox` of the same shape grows faster than linearly (200 items 49 to 69 ms, 2,000 items 2.2 to 3.5 s, 8,000 items 41 to 45 s). WPF's IL: `TextAdaptor.GetBoundingRectangles(start, end, clipToView: true, transformToScreen: true)` visits every line of the range before it clips each to the visible rectangle, and never returns null.

## Drift check

| Claim (plan, reading, WB-11 record) | On `main` b19c94ae |
|---|---|
| "one Text provider per peer (`ReadingSurface.cs:1686`)" (reading 09) | moved: `S/Reading/ReadingSurface.cs:2224-2253` (`_innerText`, `_text`, `GetPattern`) |
| the journey `ReadingSelectAll_RaisesTextSelectionChangedAndReturnsTheNote` (`ShellAccessibilityTests.ReadingList.cs:124`) | holds: its summary at `G/ShellAccessibilityTests.ReadingList.cs:124`, the fact at `:129` |
| record `wb11_reading_list_at_pass_2026-10-28.md:43-50` | holds |
| "The UIA side is correct; Narrator still announces nothing" | the event and the selection hold (B1); `GetChildren` answers null, which the classic client API cannot see |
| "WPF's TextAdaptor implements ITextProvider only, with no TextPattern2" | holds; WPF's `PatternInterface` stops at `SynchronizedInput`, so the spike served TextPattern2 through `AutomationPeer`'s private pattern table |
| "the next step: whether Narrator wants TextPattern2's caret range or another event" (WB-11, CD13) | answered: neither |

## Design

1. **`GetChildren` never answers null.** `HeadingStyleTextRange.GetChildren` (`S/Reading/HeadingStyleText.cs:642`) answers WPF's array when WPF has one and an empty array when WPF answers null. A range holding an immediate automation child (a hyperlink, a code block's copy link, a diagram, a math element, a table) keeps WPF's children unchanged.
2. **`GetBoundingRectangles` asks WPF only for the part of the range on screen.** WPF clips every line to the visible rectangle anyway, after walking all of them (the red team's measurement). The decorator intersects the range with the provider's visible ranges first and asks WPF for that part: a range wholly outside the visible ranges answers an empty array without asking WPF, a degenerate range (a caret) is passed through as it is, and a range that overlaps the view is clamped to the visible span and then asked. UI Automation's contract is the rectangles of the range's visible lines, fully or partly visible; WPF's answer can also hold lines it has laid out outside the view (measured in the red run: the heading above the view came back 199 px above the window), so the decorator's answer is WPF's rectangles for the visible part, and fact 5 compares it with WPF's rectangles that meet the view, edge lines included. To do this a range needs its provider's visible ranges, so `HeadingStyleTextRange` takes its provider at construction, through `Wrap`, `Clone`, `FindAttribute` and `FindText`. This is fixed here because the PR's own change is what lets Narrator reach the call (AGENTS.md: a defect in the PR's own feature is fixed on its branch).
3. `GetSelection` and `GetVisibleRanges` already answer an array through `WrapAll` (`:88-100`). The provider's class summary, which says it "forwards everything", is corrected to name the two answers it now shapes.
4. Nothing else changes: no event, no pattern, no sentence, no `A11yEvent` construction site, no FFI. The words are Narrator's own ("selected"), as they are in the editor.

What a reader gets: in the reading view, Ctrl+A is announced by Narrator as it is in the editor ("body selected" on WB-11's note), Narrator's read-selection command reads the selection, and a select-all on a long note does not hold the window while Narrator measures it. Through the classic client API no answer changes, so NVDA's ordinary path sees the same arrays and the same rectangles; Call 4 checks NVDA by ear.

## Facts (each red on `main`'s product code first)

Home: a new file `T/ReadingTextRangeArrayTests.cs` (license header through the script), on the shown shell (`ShownShell`, off screen, never activated) under the desktop lock, with its own small copy of `ReadingLineUnitTests.OpenReading` (`T/ReadingLineUnitTests.cs:390-411`, private there).

| # | Fact | What it pins | Red on `main` by |
|---|---|---|---|
| 1 | `ASelectAllWithNoEmbeddedElementAnswersAnEmptyChildrenArray` | `ApplicationCommands.SelectAll` on `trailing.md`'s surface; premise: the selection's text is the note (`body\r\n`); then `GetSelection()[0].GetChildren()` is an empty array, not null | WPF's null passes through |
| 2 | `ACaretAndTheDocumentRangeAnswerAnEmptyChildrenArray` | the caret's range and `DocumentRange` on a note with no immediate automation child | the same |
| 3 | `ARangeHoldingALinkKeepsWpfsChildren` | on a note whose wikilink is an immediate automation child (not inside a table), a selection over the link answers WPF's children: the same count, and the same peer behind each, compared through the peer or its name | green on `main`; the over-fix guard (mutation 2) |
| 4 | `NoInterfaceMemberOfTheReadingTextPatternAnswersANullArray` | every array-returning member of `ITextProvider` and `ITextRangeProvider`, called through the decorator by reflection on a caret, a selection without children, a range wholly off screen and `DocumentRange`, on a note long enough to scroll; answers non-null | `GetChildren` |
| 5 | `TheSelectAllRectanglesAreWpfsOwnThatMeetTheView` | on a note long enough to scroll, scrolled to its middle, the select-all range's `GetBoundingRectangles` through the decorator equals WPF's own rectangles that meet the scroll viewport, rectangle for rectangle within 0.5 px, the partly visible lines at both edges included; the first paragraph (above the view) and the last (below it) answer empty arrays | red on `main`: WPF answers the heading above the view (measured); it also holds the clamp to WPF's answer (mutation 4) |
| 6 | `ASelectAllOnALongNoteAnswersItsRectanglesQuickly` | on a 2,000-item note (WB-11's `big-list.md` shape) in the reading view, the select-all range's `GetBoundingRectangles` through the decorator returns within 250 ms (`main` measured 1.8 to 2.5 s from another process) | the walk over every line |
| 7 | `TheAppsOwnTextRangeTypesAreCoveredByAnArrayFact` | a census over the app assembly: the types the app defines that implement `ITextRangeProvider` are exactly `HeadingStyleTextRange` and `EditorSemanticTextRange`; a third fails it until it joins a fact. Its name says "the app's own": every WPF text control still serves WPF's adaptor, which is the filed issue (Call 1), named in the fact's summary | green; its red phase is a temporary third type, seen red once and removed |

The editor's ranges are already pinned: `T/EditorSemanticTextRangeTests.cs:183` and `:363` assert `Assert.Empty(range.GetChildren())`, and `:493` the document range's children. Those tests gain one `GetBoundingRectangles` non-null assertion each in place of a new editor fact.

Mutations, each seen red and reverted: (1) `GetChildren`'s null-to-empty removed: facts 1, 2 and 4 red; (2) `GetChildren` always empty: fact 3 red; (3) the clamp removed (plain delegation): fact 6 red; (4) the clamp made one line short at either edge: fact 5 red.

The existing journey `G/ShellAccessibilityTests.ReadingList.cs:129` stays (Call 2).

## Files and overlap

- `S/Reading/HeadingStyleText.cs`: `GetChildren`, `GetBoundingRectangles`, the range's constructor and its construction sites, the class summary. §6's `Reading/` row (CB-28, CB-29, CB-15c, CB-20, CB-21, CB-20b, CB-26); none of the others is in C13.
- `T/ReadingTextRangeArrayTests.cs`: new. `T/EditorSemanticTextRangeTests.cs`: three assertions.
- `G/ShellAccessibilityTests.ReadingList.cs`: its summary comment at `:124-127` (Call 2). The lead allotted this partial file to CB-21; CB-36 uses `ShellAccessibilityTests.CanvasBoard.cs`.
- CB-35 (this wave, the same principal) owns the search fields, the Files filter among them, with a census over them (plan §5 `:644`, §6 `:835`). The text-field find (Call 1) touches those fields; CB-21 changes none of them.
- `docs/plans/18_windows_port/w_c_matrix.md` is not touched: censuses read it, and the reading-view row names no selection behaviour.
- No ledger, parity matrix or census-path change. No Mac file.

## Screen-reader phrases owed

**Narrator, base `main` and head**, transcripts saved beside the recaps this time. Fixtures: `trailing.md`, `select.md`, `big-list.md` (WB-11's shape), and `rows.md`, a note with an H1, a paragraph holding a wikilink, a two-item list, a two-column table and an H2. Every row is heard on base and head; a row that changes on head and is not a selection is a regression to explain.

| Case | Base | Head (expected) |
|---|---|---|
| `trailing.md`, Ctrl+Home then Ctrl+A | "a" (measured) | "a", "body selected" (measured on the spike) |
| `trailing.md`, Narrator+Shift+Down after it | nothing (measured) | "body" (measured on the spike) |
| `select.md`, Ctrl+A, then Narrator+Shift+Down | to record | the note's text and "selected"; then the text |
| `rows.md`, Down through every line from Ctrl+Home | to record | the same as base |
| `rows.md`, Shift+Down twice, then Shift+End | to record | each selection announced; otherwise as base |
| `rows.md`, Ctrl+A | to record | announced |
| `big-list.md`, Ctrl+A, then Narrator+Shift+Down, each timed from the key to the next speech | to record | announced, with no stall over 1 s |
| the editor, Ctrl+A (control) | "a", "body  # selected" (measured) | the same |

Not covered: scan mode. It is entered with Narrator+Space, and the brief forbids changing any Narrator setting; whether scan mode's state is a setting is not settled, so it is left out and named under `not delivered`.

**NVDA, head and base** (Call 4): the same `rows.md` and `big-list.md` rows, phrases from NVDA's log, `big-list.md`'s Ctrl+A timed.

## #1954 and #1955

- #1954 (Mac work owed): none. The Mac's reading view is not WPF; VoiceOver is not affected.
- #1955 (deferred finds): none. The text-field member of the class is High and is filed (Call 1).

## Accepted-risk register (for the reviews)

- **Every WPF text field still answers `GetChildren` with null** (text boxes, the palette's field, search and rename boxes, the read-only "Embedded content" box at `S/EditorEmbedPreview.cs:144`), and Narrator stays silent on Ctrl+A there. It is the same class, outside this row, sized High by AGENTS.md (an app-wide fix needs an owner decision, or touches every XAML file with a field), and filed (Call 1). A review that raises it is pointed here.
- **How Narrator sees the null is unidentified** (remote operations suspected, not confirmed). The fix does not depend on it: the intervention is the proof.

## Calls for the lead

1. **The text-field member of the class.** Every WPF text field in the app serves WPF's own adaptor, whose `GetChildren` answers null (the red team read it in the IL and probed a `TextBox`: its selection and document range both answer null). Narrator was measured on one field, through the spike's subclass: silent with null, "body selected" with an empty array. (A) CB-21 fixes the reading view, and the text fields are filed as an issue (labels bug, windows) with this measurement, naming the read-only embed preview box (`S/EditorEmbedPreview.cs:144`) as the field where select-all-to-copy matters most, and options B to D. (B) An app-wide hook at startup that replaces the Text pattern's wrapper (key 10014) in WPF's private `AutomationPeer.s_patternInfo` table, so every WPF text provider is served through a decorator that never answers a null array. Every text control in the app resolves through that key, a larger reach than the spike's added key 10024; it reads and writes a private WPF static by reflection, so it needs the owner's yes, a pinned fact that fails loudly if WPF's shape changes, and an NVDA check on fields. (C) A text box subclass in every field: every XAML file with a field. (D) Report WPF's null `GetChildren` to dotnet/wpf, the one fix that reaches every WPF field without reflection; posting publicly needs the owner's yes. Recommended: A, with the issue offering B and D to the owner. The row is the reading view and its size is S, and B and D need owner decisions, which AGENTS.md sizes as never small or medium. Cost: text fields stay silent for Narrator on Ctrl+A until the issue lands; CB-35 (the search fields) does not take it either.
2. **No new gate journey.** The classic client API cannot see the null (step 0), so a FlaUI journey cannot go red on `main`. The facts sit at the provider on the shown shell. Recommended: no new journey, and correct the existing journey's summary at `G/ShellAccessibilityTests.ReadingList.cs:124-127`, which says the event and the selection are "what Narrator needs", to name the provider facts. Cost: one comment in a file allotted to CB-21; the row's "(gate)" is met by the lane, not by a journey.
3. **The facts' reach.** Recommended as written: the reflection loop over the interface members, the census of the app's own range types with its temporary red phase, and the editor's existing tests extended rather than a new editor fact. Cost: small.
4. **An NVDA spot check.** The row owes only Narrator. The change alters no classic-API answer, so NVDA's ordinary path is unchanged; how NVDA meets the rectangles change on a long note (faster, the same rectangles) is unmeasured. Recommended: owe it: `rows.md` and `big-list.md` on base and head, about 20 minutes in one locked session. Cost: one NVDA session.
5. **The diagnosis on #1330 (not a choice: plan §10 requires it before product code).** Open only its timing and wording. Recommended: post it right after your ruling and before the branch's first product commit, saying what the intervention proved, that the classic client API cannot see the null, that Narrator's path is unidentified, and the spike's route table; no claim stronger than this spec's.
6. **The Notification route is recorded, not taken.** It works for Narrator ("body  selected"), but its sentence would be composed by the host, and AGENTS.md has spoken text produced in slate-core; NVDA's hearing of it was not measured. Recommended: record it in the PR body under the diagnosis. Cost: none.

## Red team record

An Opus xhigh red team read this spec, the spike's diff, the recaps, meters and traces, WPF 10.0.15's IL, and ran two windowed probes under the lock with no screen reader. It found 1 blocker, 3 majors and 14 minors. Every one is folded in:

- **Blocker: the fix lets Narrator ask for the whole selection's rectangles, which takes 1.8 to 2.5 s on a 2,000-item note.** Design 2 (the clamp), facts 5 and 6, mutations 3 and 4, the `big-list.md` phrase rows.
- **Major: no Narrator regression rows for the other speech `GetChildren` reaches (focus, caret, arrows, Shift selections, links, tables).** The phrase table's `rows.md` rows on base and head; scan mode named as not covered.
- **Major: "no UIA client can see the null" was more than was measured.** Narrowed to the classic client API; Narrator's path recorded as unidentified; NVDA kept owed (Call 4); #1330's comment limited (Call 5).
- **Major: the trace story placed the divergence at the selection's first `GetChildren`.** Rewritten from the trace: several ranges, some nulls survived, the abandon-and-repeat after others, the editor's arrays being Slate's own; the intervention named as the proof.
- **Minors.** The dead `GetBoundingRectangles` null operand replaced by the clamp (m1). The editor fact dropped for existing tests plus rectangles (m2). The census renamed to the app's own types with a planned red phase (m3). Fact 1's premise (m4). Fact 3's peer comparison and an immediate-child link (m5). Fact 4 on a scrolling note and the interface's members (m6). Row B5 removed: "new line" followed the driver's Escape, the field was the spike's subclass, the counts were session-wide, and `main` has no field measurement (m7). The meter's absent rows stated (m8). The three runs where Narrator took Ctrl+Home named (m9). The unsaved console numbers named and re-measured with saved transcripts on the delivery pass (m10). The Notification route's reason taken from AGENTS.md (m11). Call 1's measured scope, the embed box, CB-35's overlap, key 10014 against 10024, and the dotnet/wpf option (m12). Call 5 recast as timing and wording (m13). The wikilink row recast, the provider's summary corrected, the matrix left untouched (m14).


## Build notes (after the spec calls)

- The facts were written and run red on `main`'s product code under the lock (2026-11-25, two runs): facts 1, 2 and 4 red by WPF's null `GetChildren` (fact 4 by `GetChildren` alone: no other array member answered null); fact 5 red because WPF answers the heading above the view; fact 6 red at 5,043 to 5,499 ms in process; facts 3 and 7 green. The editor's existing tests do not gain rectangle assertions: their windowless host has no layout, so AvalonEdit's `GetBoundingRectangles` throws `VisualLinesInvalidException` there (measured); the census and the existing `GetChildren` assertions keep the editor's member of the class.

## What delivery changed

Merged as #2072 (squash b2ebcbb9, 2026-11-26), head 1f2245da. Closes #1330.

- The fix is the one the spec named: `GetChildren` answers an array where WPF answers null. Making it work found the cost the spec did not foresee. With an array, Narrator asks for the whole selection's rectangles, and WPF walks every line of the range (1.7 to 2.6 s on a 2,000-item note). `GetBoundingRectangles` now asks WPF only about the span its visible ranges cover, each end moved out by WPF's Line unit. The rectangles stay WPF's own.
- Right-to-left text: the first clamp, with no Line move, lost the top or bottom line for right-to-left, mixed and wrapped text (the sweep fact). In a view about one line tall over right-to-left text, WPF's visible ranges throw, so the provider orders the corners itself (Codex round 1).
- A note that begins with a table: WPF answers its document range no rectangle; the decorator answers the visible lines (Codex round 1).
- The merge judge's M1: the public `GetVisibleRanges` still passed WPF's throw on. It now answers from the same span. Making that fact green found two more things. The fallback hit-tested the whole surface, whose padding snaps to the document's start; it now hits the scroll viewport. And where the view sits between two paragraphs (where these throws come from), the fallback answers a collapsed range rather than the snapped, off-screen paragraph.
- Spec claims that were wrong:
  - "WPF answers rectangles outside the view" was a test helper's bug; the guard written for it was removed.
  - The editor rectangle assertions were dropped: the windowless editor host has no layout.
  - The timing bound is 500 ms a range, not 250 ms.
- No new gate journey (ruling 2). The existing journey's summary was corrected.
- Narrator, base and head: base says "a" on Ctrl+A. Head adds "<text> selected", and Narrator+Shift+Down reads the selection; on big-list.md, 0.97 s after the key. The NVDA spot check was identical on base and head.
- Filed: #2067 (every WPF text field answers `GetChildren` null; Narrator is silent on Ctrl+A in them), #2071 (a range across a table answers its first column's rectangles only, WPF's own traversal).
- Count: found 4 / fixed here 2 / deferred 0 / filed 2.
- Not delivered:
  - scan mode rows (Narrator+Space);
  - how Narrator meets the provider's null, which no classic UIA client can show;
  - a Narrator measurement of a text field on `main` (the text fields are #2067);
  - a bound on the rectangle walk for a table row whose first cell wraps to thousands of lines (the judge's nit 2).
