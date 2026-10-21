# WB-22 — Ctrl+F finds in the open note: a landmark find bar beside the editor with next, previous, wrap, case, whole word, replace and a spoken count

Issues: Closes #1112. Size L. Lanes: windows + FlaUI. AT: NVDA, Narrator. Wave: W8. Depends on: WB-21b (the `MainWindow.xaml` Editor menu, `§6`'s order), WB-17 and WB-21 (`ChordTable.cs`, `chords.json`). Parallel with: WB-23, WB-24. Decision: WD12 (pending; recommended: disposition 1 via `SharedCommandChords` — one entry for the whole Ctrl+F command group, the sorted three-command key `slate.bases.quickFilter | slate.canvas.filterCards | slate.editor.findInNote`, replacing today's `slate.bases.quickFilter | slate.canvas.filterCards` entry — replace in, regex out, Ctrl+G / Shift+Ctrl+G as next and previous, find-in-selection deferred; under disposition 2 the `slate.bases.quickFilter` row becomes `win: null`, the Bases menu item loses its gesture text, and the then-stale `slate.bases.quickFilter | slate.canvas.filterCards` entry is replaced by `slate.canvas.filterCards | slate.editor.findInNote`; under "regex yes" a `SearchMode` toggle and its facts are added as a recorded Windows-only divergence).

## Goal

Mac has had find-in-note since #874 (AppKit's stock find bar); Windows
has nothing — Ctrl+F in a note is dead. After this chunk a Windows
reader presses Ctrl+F in a note and lands in a named "Find in note"
field beside the editor; typing highlights matches and the bar speaks
"N of M"; in the bar, Enter / Shift+Enter (in the query) and Ctrl+G /
Shift+Ctrl+G step with wrap-around; case and whole-word toggles and Replace / Replace All are
visible controls; Escape closes the bar, clears the highlights and
returns the caret where it was; Ctrl+F with no note editor mounted
falls through to the vault search overlay, as mac's never-inert rule
does. Parity rows `slate.editor.findInNote` and the §W-C row leave
"pending".

## Current state on main

Verified against the worktree. Two of the issue's architectural
arguments drifted and the plan's §2.2 records it; a third correction is
new here: **Windows has no Edit menu** — its top-level menus are File,
Workspace, Base, Editor, Canvas, Graph (`MainWindow.xaml:179`, `:282`,
`:365`, `:435`, `:575`, `:677`), so the "Edit menu item" lands in the
Editor menu.

- No find bar: zero hits for `SearchPanel`, `ISearchStrategy`,
  `IBackgroundRenderer` under `apps/slate-windows/src`. AvalonEdit
  `6.3.1.120` (`SlateWindows.csproj:30`).
- `SlateTextEditor.cs:538-539` `GetChildrenCore()` returns
  `(GetPattern(PatternInterface.Text) as EditorSemanticTextProvider)?.Links.RootChildren()`
  — not `null` as the issue says; link peers already live under the
  Document peer. Sibling placement is still the safer shape (the
  Document's atomic-text pin, `w_c_matrix.md:17`). `OnPreviewKeyDown`
  handles Ctrl+E (`:221`) and Ctrl+Enter (`:230`); `ChordTableTests.cs:1161`
  `EditorChords()` scrapes that method for `ChordScope.Editor` rows.
- `WorkspaceTemplates.xaml:331-343` `WorkspaceTabContentTemplate`, a
  three-row Grid (Auto, *, Auto); Row 2 is taken by the status
  `TextBlock` (`:591-595`) — the find bar needs a new row (Row 2, status
  moving to Row 3), not the "empty slot" the issue describes. The editor
  popover overlay is `Grid.RowSpan="3"` (`:488`) and must span the new
  row too.
- Ctrl+F today: `ChordTable.cs:918-920` `Reg(Ids.BasesQuickFilter, "Bases: Quick Filter", …, win: "Ctrl+F", scope: ChordScope.Grid)`
  and `:1884-1887` the surface row `windows.grid.filter` (Ctrl+F,
  `ChordScope.Grid`, `AccessibleDataGrid.FilterCommand`'s gesture);
  `Grids/AccessibleDataGrid.cs:217` yields when no subscriber
  (`e.ContinueRouting = FilterRequested is null`). A second COMMAND row
  already ships Ctrl+F: `ChordTable.cs:1361-1364`
  `Reg(Ids.CanvasFilterCards, "Canvas: Filter Cards…", …, "⌘F", "Ctrl+F", ChordScope.Canvas)`,
  delivered by the canvas surface's tunnelling `OnPreviewKeyDown` →
  `CanvasNavigator.HandleKey` → `FilterCardsFromKey`
  (`Canvas/CanvasNavigator.cs:237`, `:1855-1874`; it stands aside only
  when the canvas TABLE's grid owns the keys, and that grid's
  `FilterCommand` is subscribed to the same canvas filter). No
  window-level Ctrl+F (`MainWindow.xaml:34` `Window.InputBindings` has
  none); no `Ctrl+G` row anywhere in the table. The vault search chord
  is `Ctrl+Shift+F` (`:794-796`, `slate.view.toggleSearch`), resolved to
  `host.ToggleSearchCommand` (`SlateCommandRegistrar.cs:456`,
  `VaultLifecycleViewModel.cs:280`).
- The collision rule: `ChordTableTests.cs:318-380`
  `CommandRows_HaveNoActiveChordCollision` groups every command row by
  its Windows chord and, for a group of two or more, looks up ONE key —
  every id in the group, ordinal-sorted, joined by " | " (`:339-345`) —
  in `SharedCommandChords` (`:283-316`), requires its reason, and
  requires the group's scopes to be distinct (`:351-353`); a recorded
  key whose rows no longer share one chord is stale and fails
  (`:359-369`). Ctrl+F's group today is the two command rows above,
  recorded as `slate.bases.quickFilter | slate.canvas.filterCards` (`:286-291`, the canvas-table
  reason). The scope-keyed carve-out the issue asked for exists, but a
  new Ctrl+F command joins that group: the test never looks up a
  base/editor pair, so disposition 1's "one dictionary entry" is the
  three-command key. Every one of the six entries on `main` is the key
  of a live group (the six shared chords in `chords.json`).
- Chord scopes: every row carries ONE `ChordScope` (`ChordTable.cs:22-98`).
  `TableCoversEveryDeclarativelyBoundChord_AndNothingStale`
  (`ChordTableTests.cs:383-470`) compares each scope's rows, command and
  surface alike, both ways with that scope's production scrape
  (`ScopedProductionChords()`, `:778-796`), and
  `EveryScopeIsEitherScrapedFromProductionOrDispositioned`
  (`:1105-1123`) requires a scrape for every scope but `None` and
  `Global` (`:1093-1099`). `ChordScope.Editor` is scraped from
  `SlateTextEditor.OnPreviewKeyDown` (`EditorChords()`, `:1161`), which
  reads only bare keys and Ctrl with no other modifier
  (`SlateTextEditor.cs:212-250`); its rows are `slate.editor.previewEmbed`
  (Ctrl+E), `slate.editor.activateAtCaret` (Ctrl+Enter) and the surface
  row `windows.editor.closePopover` (Escape, `ChordTable.cs:1855-1859`).
  The surface-row loop (`ChordTableTests.cs:371-379`) refuses two
  surface rows on one chord in one scope, so a find-bar Escape row in
  `ChordScope.Editor` fails beside `closePopover`. `KeyBindingChords`
  reads only `MainWindow.xaml` and `WorkspaceTemplates.xaml`
  (`:390-392`), so a `KeyBinding` in another XAML file is checked by
  nothing. No row ships Shift+Enter, Ctrl+G or Ctrl+Shift+G; Escape and
  Enter surface rows exist in seven and six other scopes (`chords.json`).
  The canonical modifier order is Ctrl, Alt, Shift (`Canonical`,
  `:1542-1562`), so Shift+Ctrl+G is the row string `Ctrl+Shift+G`. The
  precedent for a field's own scope is `ChordScope.SidebarFilter`
  (W7-7 PR 2, `f035ab26`): one enum value (`ChordTable.cs:94-97`), its
  row (`:1724-1731`), its scrape (`ChordTableTests.cs:795`) and its fact
  (`SidebarFilterClearIsASurfaceRowInItsOwnScope`, `:870`); the
  map-shaped scrape the bar follows is `GraphChords()` (`:887-934`:
  `Bind`'s three-argument `AddChord(Key.X, ModifierKeys.Y, …)` calls,
  read as a duplicate-free list).
- The precedent: `Bases/BaseSurfaceView.cs:56` `_filterDebounce`,
  `:101` `BaseCountReadout`, `:110` `BaseQuickFilter`, `:120-125` the
  150 ms `DispatcherTimer`, `:189` `_grid.FilterRequested += FocusQuickFilter`;
  Escape semantics in `25_bases_grid_contracts.md:55-68`.
- Landmark hosts: `AutomationLandmark.cs:33-43` `AutomationLandmarkGrid`
  / `Border` (`AutomationLandmarkPeer`), `:54` `AutomationNamedRowBorder`,
  `:158` `AutomationPresentationTextBlock`. Every AutomationId
  construction must be registered in `w_c_automation_ids.json` with a
  surface title that is a `w_c_matrix.md` row
  (`WcMatrixEvidenceCensus.cs:42-58`); every matrix row needs ten cells
  and executable evidence (`:62-75`).
- Mac: `NoteEditorView.swift:245-246` `usesFindBar = true`,
  `isIncrementalSearchingEnabled = true`; `:1426-1432` `showFindBar()`
  posts `NSTextFinder.Action.showFindInterface`; `AppState.swift:9493-9501`
  `requestFindInFocusedSurface()` tries the Bases filter, then Canvas,
  then `showFindInNote()`, which (`:9505-9535`) falls back through
  `requestSearchOverlay()` when no `NoteEditorView` is mounted;
  `SlateCommands.swift:436` id, `:1895-1906` label "Find…", section
  `.editor`, hotkey ⌘F, hint "Find and highlight text within the current
  note."; `SlateMacApp.swift:1043-1046` the one Edit-menu Button; no
  ⌘G item (`:810` binds ⌃⌘G to something else).
- Registers: `parity_matrix.md:121` the row pending;
  `generate-parity-matrix.py:94` routes the id to #1112.

## Reference

- WD12 (kept as written): "Disposition 1 via `SharedCommandChords`
  (scope-keyed: Bases quick filter in a Base, find-in-note in a note);
  replace yes (mac has it), regex no (mac has none; parity). Ship Ctrl+G
  / Shift+Ctrl+G as next and previous (AppKit's defaults …); defer
  find-in-selection, recorded in the parity matrix as 'unverified on
  mac; not shipped', so no Mac probe gates the chunk." WD12's text
  names two rows; `slate.canvas.filterCards` is the third command row on
  Ctrl+F, so the disposition's one entry covers all three.
- Mac behaviour: AppKit's stock bar — incremental, next/previous, live
  count, always-wrap, ignore case, whole word, Replace / Replace All; no
  regex; no announcement of the count (`NoteEditorView.swift:245-246`,
  issue table).
- Contract 28 P5 (`28_palette_contracts.md:79`): never a `Popup`.
  Contract 25 (`25_bases_grid_contracts.md:55-68`): the filter's Escape
  ladder. `w_c_matrix.md:17`: the editor Document's atomic text.
- Plan 43 §4 rule 4 (ids and labels byte-identical, P3) and program
  decision 12 (⌘→Ctrl), which pins ⌘F → Ctrl+F.

## Design

**View model.** New `FindInNoteViewModel` (per Markdown tab, owned by
`WorkspaceTabViewModel`, disposed with it): `Query`, `MatchCase`,
`WholeWord`, `ReplaceText`, `IsOpen`, `CurrentIndex`, `TotalCount`,
`Error` (a `SearchPatternException` message — never a `ToolTip`),
`FocusRequested` (the `BaseDocumentViewModel` shape), commands `Next`,
`Previous`, `Replace`, `ReplaceAll`, `Close`. The engine is
`ICSharpCode.AvalonEdit.Search.SearchStrategyFactory.Create(pattern, ignoreCase, matchWholeWords, SearchMode.Normal)`
over the tab's `TextDocument`; results are recomputed on a 150 ms
debounce after a query or option change and on every document change
while open; wrap-around is manual (`FindNext` from the caret, else from
0; `Previous` mirrors). `SearchMode.RegEx` is not offered (WD12).
Revealing a hit: `editor.Select(offset, length)` then `BringCaretToView()`.

**No action on stale results.** Every result set is stamped with the
`TextDocument.Version` (`ITextSourceVersion`) it was computed against
and a monotonically increasing options revision (query, MatchCase,
WholeWord). A recompute has one of two causes, and they differ in what
they may move: one caused by a query or option change reveals the first
hit at or after the caret (incremental search: it selects that hit and
makes it the tracked hit below); one caused by a document edit recounts
and re-highlights only — it never moves the caret or the selection,
keeps the tracked hit current when that hit still validates, and
otherwise clears it (`CurrentIndex` 0).

*The tracked hit.* Whenever a hit becomes current (a query or option
recompute, `Next`, `Previous`, the step after a `Replace`), the view
model keeps it as `TrackedHit` (new, a private record in
`FindInNoteViewModel`: `TextAnchor Start` with `MovementType =
AnchorMovementType.AfterInsertion`, `TextAnchor End` with `MovementType
= AnchorMovementType.BeforeInsertion`, both from
`TextDocument.CreateAnchor` with `SurviveDeletion = true` so `Offset`
never throws; `int Length`, the hit's length when it became current).
The anchors follow every edit, so the hit the reader heard is the hit
`Replace` acts on, wherever an edit elsewhere moved it. It
**validates** when both hold: `End.Offset - Start.Offset == Length`,
and the current strategy (the live query and options) returns from
`FindNext(document, Start.Offset, Length)` a result at exactly
`Start.Offset` with length `Length`. An insertion or deletion inside
the hit changes the span; a deletion across it collapses both anchors
to one offset; an edit that rewrites the hit's text, wholly or in its
middle (which can leave the span's length intact — a same-length
replacement strictly inside the hit moves neither anchor), and a query
or option change the hit no longer satisfies fail the `FindNext` check.
Neither check alone is enough, and the facts below pin both.

*Navigation and Replace All recompute; Replace validates first.* `Next`,
`Previous` and `ReplaceAll` compare the stamps with the live document
version and the options revision; when either differs they flush the
debounce and recompute synchronously before acting (no refusal is
needed for them — recomputation is the correct answer; with zero
matches after it the count line is spoken and nothing moves).
`ReplaceAll` runs inside one `BeginUpdate`/`EndUpdate` over the freshly
recomputed set so Ctrl+Z undoes it whole. `Replace` never takes its
target from a recomputation: it validates the tracked hit **before** any
recompute, and only then:
- valid → `document.Replace(Start.Offset, Length, ReplaceText)` (one
  undo step), then the set is recomputed and the next hit after the
  replacement becomes current (the step);
- invalid → nothing is written; the tracked hit is cleared; the set is
  recomputed for the count only (`TotalCount` the live count,
  `CurrentIndex` 0, caret and selection unmoved); and the one line
  spoken is "Match changed; nothing replaced." — whether another match
  survives elsewhere in the note or none remains (never the "{N} of {M}"
  of a different hit, never "No matches" in its place);
- no tracked hit at all (nothing current yet, or after a refusal) →
  `Replace` acts as `Next` — recompute if stale, reveal and speak the
  next hit — and writes nothing, so a reader always hears a hit before a
  `Replace` can rewrite it.

So the host never rewrites any text but the hit it last revealed, and
never text the pattern no longer matches. Opening the bar stores the
caret and selection as `TextAnchor`s (`TextDocument.CreateAnchor`,
`AnchorMovementType.AfterInsertion`, `SurviveDeletion = true`), so
Close restores positions that followed every intervening edit, clamped
to `document.TextLength`.

**Rendering.** `Reading/`-free: a ~50-line `FindMatchBackgroundRenderer : IBackgroundRenderer`
(`KnownLayer.Selection`, `BackgroundGeometryBuilder` over a
`TextSegmentCollection<TextSegment>`), registered on
`TextArea.TextView.BackgroundRenderers` while the bar is open and
removed on close and on tab dispose. The brush is a theme token
(`Slate.FindMatchBrush`, new, both themes; contrast fact beside
`ThemeTokenContrastTests`).

**View.** `FindInNoteBar` (new `UserControl`) at `Grid.Row="2"` of
`WorkspaceTabContentTemplate`, the status `TextBlock` moving to a new
Row 3 and the popover overlay to `RowSpan="4"`. Rooted at an
`AutomationLandmarkGrid` named "Find in note"; children with
AutomationIds `FindInNoteQuery` (Edit, Name "Find in note"; HelpText
names Enter/Shift+Enter, Ctrl+G, Escape), `FindInNoteCount` (Text, a
focus stop like `BaseCountReadout`), `FindInNotePrevious`,
`FindInNoteNext`, `FindInNoteMatchCase`, `FindInNoteWholeWord`
(CheckBoxes, visible — the divergence the issue records),
`FindInNoteReplaceText`, `FindInNoteReplace`, `FindInNoteReplaceAll`,
`FindInNoteClose`, `FindInNoteError` (Text, present only on a pattern
error). Not modal: no `ModalSurface` member, no `IsDialog`, no
`IsFocusScope` (so `SheetFenceCensus` is not involved), tab order
Query → Count → Previous → Next → Match case → Whole word → Replace
text → Replace → Replace All → Close, then back to the editor.

**Routing.** The global command is owned by the lifecycle host, not
the workspace, because the welcome view has no `Workspace` and Ctrl+F
must still reach vault search there: `VaultLifecycleViewModel.FindInNoteCommand`
(new; `VaultLifecycleViewModel` is the `ISlateCommandHost`,
`SlateCommandRegistrar.cs:63-70`, which already owns `ToggleSearchCommand`
at `VaultLifecycleViewModel.cs:280`). A window-level `KeyBinding`
Ctrl+F binds it (`ChordScope.Global`); the registrar maps
`[Ids.EditorFindInNote] = host => host.FindInNoteCommand`. Its Execute:
`Workspace?.ActiveGroup.ActiveTab` is a Markdown tab in editor mode →
`Workspace.OpenFindInNote()` (new; opens the bar, or, open already,
focuses the query and selects all — mac's `Reactivate`); a Base or
Canvas tab → their own filters handle Ctrl+F before it reaches the
window (a Base: the grid's `FilterCommand` gesture,
`Grids/AccessibleDataGrid.cs:106`, `:217`; a canvas: the surface's
tunnelling handler, `CanvasNavigator.cs:237` → `FilterCardsFromKey`,
`:1855-1874`, or on the canvas TABLE the grid's `FilterCommand`,
subscribed to the canvas filter), so the global arm never sees a key a
surface claimed. That holds only because the window's Ctrl+F is a
bubbling `KeyBinding` in `MainWindow`'s `Window.InputBindings`
(`MainWindow.xaml:34`) — never a case in `Window_PreviewKeyDown`,
which tunnels ahead of every surface (the `slate.file.cancelImport`
Escape entry, `ChordTableTests.cs:292-298`, relies on exactly that
order for the opposite purpose); a reading-mode note, a non-note tab,
no tab, or no workspace at all (welcome view) → `ToggleSearchCommand.Execute(null)`
(mac's fall-through; the overlay's own no-vault behaviour is unchanged;
reading-mode find stays out of scope like mac's).

**The bar's own keys** (new `ChordScope.FindInNote`). Every key the bar
handles is delivered by ONE route: `FindInNoteBar.Bind()` (new, in
`FindInNoteBar.xaml.cs`) builds a map of three-argument
`AddChord(Key.X, ModifierKeys.Y, Func<bool>)` calls — the
`CanvasNavigator` / `GraphNavigator` shape — and the bar's tunnelling
`OnPreviewKeyDown` override runs it, so the keys are live only while
keyboard focus is within an open bar (the bar is `Collapsed` while
closed and holds no focus) and an action that returns false leaves the
key to its target. The map:
- Enter → Next and Shift+Enter → Previous, acting only when the query
  field (`FindInNoteQuery`) holds the focus; elsewhere in the bar they
  return false, so a focused button's own Enter still clicks it (Enter
  on Replace All runs Replace All);
- Ctrl+G → Next and Ctrl+Shift+G → Previous, anywhere in the bar;
- Escape → Close, anywhere in the bar (clears highlights, restores the
  caret and selection the open captured, focuses the editor).

`FindInNoteBar.xaml` declares no `KeyBinding` or `InputBindings` (no
scrape reads that file's bindings), and `SlateTextEditor.OnPreviewKeyDown`
gains no arm. The rows, all in `ChordScope.FindInNote`: the command rows
`slate.editor.findNext` (`"Ctrl+G"`) and `slate.editor.findPrevious`
(`"Ctrl+Shift+G"`, the table's canonical order) with `mac: null` —
Windows-only, the parity matrix's Windows-only arm — and three surface
rows, `Chord("windows.findInNote.next", "Find in note: next match",
"Enter", ChordScope.FindInNote, …)`, `windows.findInNote.previous`
(`"Shift+Enter"`) and `windows.findInNote.close` (`"Escape"`), each
reason naming the command it runs and the route. `ChordScope.FindInNote`
gets an enum summary like `SidebarFilter`'s, and `ScopedProductionChords()`
gains `[ChordScope.FindInNote] = FindInNoteChords()` (new), a scrape of
`CSharpSource.Load("FindInNoteBar.xaml.cs").Method("Bind")` shaped like
`GraphChords()` (a duplicate-free list of at least the five chords). Why
a scope of its own, and why only in the bar:
- `ChordScope.Editor` cannot hold them. `windows.editor.closePopover`
  already ships Escape there and the surface-row loop refuses a second
  Escape row in one scope; Enter and Shift+Enter there would be claims
  the editor's scrape never finds (the editor types them); and that
  scrape cannot express Ctrl+Shift+G (it reads Ctrl with no other
  modifier), so the row would fail the both-way comparison.
- A row carries ONE scope and each scope is checked against ONE
  production route, so Ctrl+G cannot be live "anywhere in the tab": that
  needs either a second delivery in the editor (a second scope for one
  row) or a route above the editor, which would see the editor's own
  Enter and Escape first. The rows are Windows-only (`mac: null`), so no
  parity row constrains where they live, and from the editor the step is
  one key away: Ctrl+F re-focuses the open bar's query
  (`OpenFindInNote` focuses it and selects all), then Enter or Ctrl+G.
- The new scope shares chords only across scopes: Escape and Enter with
  other scopes' surface rows (distinct scopes, as the surface-row loop
  requires), nothing with any command row (no Ctrl+G or Ctrl+Shift+G row
  exists), so no `SharedCommandChords` entry is needed for them.

Precedence is the window's first, as for every surface: the window's
own tunnelling handler (`MainWindow.xaml.cs:858` — an import's Escape,
the modal sheets) runs before the bar's, and the bar's Escape never
reaches the editor's popover row, which lives in the editor's route.

**Chord table and menu.** `Reg(Ids.EditorFindInNote /* slate.editor.findInNote */, "Find…", CommandSection.Editor, "Find and highlight text within the current note.", "⌘F", "Ctrl+F", ChordScope.Global)`;
one `SharedCommandChords` entry for the whole Ctrl+F command group, which with the new row is three rows in three scopes (Grid, Canvas, Global) — under disposition 1 the existing `["slate.bases.quickFilter | slate.canvas.filterCards"]` entry is REPLACED (not kept beside it: no group's key is the pair any more, and the live-key check below fails a left-over) by `SharedCommandChords["slate.bases.quickFilter | slate.canvas.filterCards | slate.editor.findInNote"] = "Ctrl+F. Disjoint by DELIVERY: on a Base the grid's FilterCommand gesture handles it, and on a canvas the surface's tunnelling handler does — on the canvas TABLE the grid's FilterCommand, which is subscribed to the canvas filter, so the Grid and Canvas rows agree by construction and the navigator stands aside there — both before the key bubbles to the window; the window's KeyBinding (Global, bubbling, never in Window_PreviewKeyDown) acts only on a key no surface claimed: the find bar for a note in editor mode, vault search otherwise (WD12 disposition 1)."`; under disposition 2 `slate.bases.quickFilter` ships no Windows chord, the group is the canvas and editor rows, and the then-stale `["slate.bases.quickFilter | slate.canvas.filterCards"]` entry (the staleness loop fails it: its Bases row no longer shares the chord) is replaced by `SharedCommandChords["slate.canvas.filterCards | slate.editor.findInNote"] = "Ctrl+F. Disjoint by DELIVERY: on a canvas the surface's tunnelling handler (or, on the canvas TABLE, the grid's FilterCommand subscribed to the canvas filter) handles it before it bubbles; the window's KeyBinding acts only on a key no surface claimed (WD12 disposition 2)."`;
Editor menu item "_Find…" with `InputGestureText="{cmd:ChordText slate.editor.findInNote}"`
and `AcceleratorKey` bound like `:437-441`. Registrar:
`[Ids.EditorFindInNote] = host => host.FindInNoteCommand` (the lifecycle host's, never `Workspace?.`).
`chords.json` regenerated; `deliveryEvidence.commands` gains the id →
`"editorHost"` (or a new `findInNote` group naming the view model and
the FlaUI journey).

**Speech.** The count is a host-composed §W-C golden family
`FindInNotePhrase` (new, `Panels/`-style static class): "{N} of {M}",
"No matches", "Wrapped to the top" / "Wrapped to the end" appended once
on a wrap, "Replaced {M} matches." after Replace All, "Match changed;
nothing replaced." when `Replace` refuses a tracked hit that no longer
validates, and the pattern error verbatim. Spoken through `HostComposed` at Medium, coalesced to
the final count after the debounce (the palette's `Palette_TypingAnnouncesOnlyTheFinalCountAndSelection`
posture). Designated in `a11y_trigger_designations.json` as residue
with the reason "no mac twin: AppKit's find bar announces nothing";
the trigger ledger regenerated.

Not changed: `SlateTextEditor.GetChildrenCore`, the Document peer, the
Bases and Canvas filters, core, FFI, mac. Deliberately not shipped:
regex, find-in-selection (parity row "unverified on mac; not shipped"),
reading-mode find.

## Tests

`FindInNoteViewModelTests` (pure, over a `TextDocument`):
- `TypingFindsAllMatchesAndReportsOneOfM` — "a b a" query "a": `TotalCount` 2, `CurrentIndex` 1 at the first hit after the caret. Mutation: drop the debounce flush → stale count.
- `NextWrapsToTheTopAndSaysSo` / `PreviousWrapsToTheEndAndSaysSo`.
- `MatchCaseAndWholeWordNarrowTheMatches` (Theory).
- `ReplaceReplacesTheCurrentHitAndSteps`; `ReplaceAllIsOneUndoStep` (Ctrl+Z restores every hit).
- `ADocumentEditWhileOpenRecountsOnTheDebounce` — an edit elsewhere recounts once the debounce runs; the recount leaves the caret and selection where the edit put them, and the still-valid tracked hit stays current with its index recomputed; an edit that breaks the hit clears it (`CurrentIndex` 0), still without moving the caret. Mutation: let a document-edit recompute reveal the first hit after the caret → the caret moves.
- `ReplaceWithNoCurrentHitRevealsTheNextAndWritesNothing` — "a b a", query "a", the tracked hit cleared by a document-edit recount (the first "a" edited to "x", the debounce flushed); Replace with ReplaceText "q": the document is still "x b a", the surviving "a" (offset 4) is selected and "1 of 1" is spoken; a second Replace writes "x b q". Mutation: with no tracked hit, recompute and replace the first hit → the first press writes.
- `AnEmptyQueryClearsHighlightsAndCountsNothing`.
- `CloseRestoresTheCaretAndSelectionTheOpenCaptured`.

`FindInNotePhraseTests` (the `TaskStatusPhraseTests` shape): every
string pinned verbatim.

Hosted (`W2EditorInteractionTests` / a new `FindInNoteBarTests` with the
`CommandPaletteTests.ShellKeys` shell): 
- `CtrlFInANoteFocusesTheQueryAndSelectsAll`; `CtrlFOnABaseTabReachesTheQuickFilterNotTheBar`; `CtrlFInReadingModeOrWithNoTabOpensVaultSearch` (the fall-through); `CtrlFOnTheWelcomeViewWithNoWorkspaceReachesVaultSearch` (hosted shell, `Workspace == null`: `ToggleSearchCommand` executes; nothing throws). Mutation: remove the fall-through arm, or resolve the command through `Workspace?.` → the overlay stays closed.
- `CtrlFOnACanvasReachesTheCanvasFilterNotTheBar` — a canvas tab with the board holding the keys: Ctrl+F through the shell sets the canvas document's `FilterFocusRequest` (as `CanvasNavigatorTests.cs:6330` reads it), the find bar stays closed and `ToggleSearchCommand` did not execute — the third row of the shared Ctrl+F group, delivered as its recorded reason says. Mutation: deliver the window's Ctrl+F from `Window_PreviewKeyDown` instead of a bubbling `KeyBinding` → vault search opens and `FilterFocusRequest` stays null (`CtrlFOnABaseTabReachesTheQuickFilterNotTheBar` fails the same way).
- `AnEditBeforeTheDebounceNeverReplacesStaleText` — a Theory over three cases, each with the query typed and the first hit (offset 0) revealed as the current hit, then, inside the 150 ms window, the reader's own edit, then Replace with ReplaceText "q". *Another match survives*: query "a", "a b a", edit `document.Replace(0, 1, "x")` → "x b a"; after the press the document is exactly "x b a" (the surviving "a" at offset 4 untouched), `TotalCount` 1, `CurrentIndex` 0. *No match remains*: query "a", "a b c", the same edit → "x b c"; after the press the document is exactly "x b c", `TotalCount` 0. *The hit's middle changed*: query "abc", "abc d abc", edit `document.Replace(1, 1, "x")` (strictly inside the hit, so neither anchor moves and the span keeps its length) → "axc d abc"; after the press the document is exactly "axc d abc", `TotalCount` 1. In every case the one line the press speaks is "Match changed; nothing replaced." (never "1 of 1", never "No matches") and the caret and selection are where the edit left them. Mutations: recompute before validating and act on the recomputed current hit (the round-7 design) → the first case writes "x b q", the second speaks "No matches", the third writes "axc d q"; validate by span only, dropping the `FindNext` check → the third case writes "q d abc".
- `AnEditElsewhereBeforeTheDebounceStillReplacesTheTrackedHit` — "a b a", query "a", Next to the second "a" (offset 4, "2 of 2"); inside the 150 ms window insert "zz" at offset 0 ("zza b a"); Replace with ReplaceText "q": the document is exactly "zza b q" — the tracked hit, followed by its anchors to offset 6, is the one replaced, and no refusal is spoken. Mutation: validate at the offset captured when the hit became current instead of through the anchors → offset 4 now holds "b", the press refuses, and the fact fails.
- `AnOptionChangeBeforeReplaceRecountsFirst` — toggle Match case then Replace All within the debounce: only the case-sensitive matches are replaced (the set was recomputed synchronously). Mutation: act on the stamped set → the wrong matches change.
- `CloseAfterAShortenedDocumentRestoresAClampedCaret` — open at offset 40, delete the tail so the document is 10 chars, Escape: caret at 10, no exception. Mutation: restore the raw captured offset → `ArgumentOutOfRangeException`.
- `EnterAndCtrlGStepNextAndShiftStepsPrevious` (focus in the query: Enter and Ctrl+G step next, Shift+Enter and Ctrl+Shift+G step previous).
- `TheBarsKeysActOnlyWithFocusInTheOpenBar` — "a b a", query "a", the bar open: with the focus on `FindInNoteReplaceAll` and ReplaceText "q", Enter runs Replace All ("q b q"), not Next; Ctrl+Z restores "a b a"; with the focus moved to the editor and the bar still open, Enter types a newline into the document, Ctrl+G and Ctrl+Shift+G leave `CurrentIndex` and the editor selection unchanged, and Escape leaves the bar open (`IsOpen` true); with an editor popover open (Ctrl+E on an embed) and the focus in the editor, Escape closes the popover and the bar stays open; with the bar closed, Ctrl+G and Ctrl+Shift+G in the editor change nothing and open nothing. (The in-bar positives are `EnterAndCtrlGStepNextAndShiftStepsPrevious`'s and `EscapeClosesClearsHighlightsAndReturnsFocusToTheEditor`'s.) Mutations: run the bar's map from a route above the editor (the tab content root, or `Window_PreviewKeyDown`) → Enter in the editor steps the find instead of typing a newline; let the Enter action act whatever holds the focus → Enter on Replace All steps Next and writes nothing.
- `EscapeClosesClearsHighlightsAndReturnsFocusToTheEditor` (the renderer is unregistered — assert `BackgroundRenderers` count).
- `TheBarIsASiblingOfTheEditorPeerNotAChild` — the editor's `GetChildrenCore` is unchanged (`AvalonDocumentBufferCensus` stays green) and the bar's peer hoists to the tab content.
- `TypingAnnouncesOnlyTheFinalCount` (coalescing).
- `ChordTableTests`: `CommandRows_HaveNoActiveChordCollision` passes with the three-command key — Ctrl+F's group is now three command rows in three scopes (Grid, Canvas, Global), so the fact looks up exactly `slate.bases.quickFilter | slate.canvas.filterCards | slate.editor.findInNote` (under disposition 2, `slate.canvas.filterCards | slate.editor.findInNote`, with the base/canvas entry removed); it gains one assertion in its staleness loop: every recorded key is the key of a live group (`liveKeys.Contains(key)`, `liveKeys` collected in the group loop), so a superseded entry cannot stay beside its successor (it holds on `main`: the six entries are the six live groups' keys). Mutations: record only a base/editor pair → the group's lookup fails; keep the base/canvas pair beside the triple → the live-key assertion fails; remove the triple → the lookup fails. `EveryScopeIsEitherScrapedFromProductionOrDispositioned` (`:1105`) — the new `ChordScope.FindInNote` is scraped, not dispositioned; `TableCoversEveryDeclarativelyBoundChord_AndNothingStale` (`:383`) — the Global arm's both-direction check sees the new Ctrl+F `KeyBinding`, and the scoped comparison checks `ChordScope.FindInNote`'s five rows both ways against `FindInNoteChords()`; `ChordsJson_IsExactlyTheTablesProjection` regenerated.
- `FindInNoteKeysAreRowsInTheirOwnScope` (new, `ChordTableTests`, beside `SidebarFilterClearIsASurfaceRowInItsOwnScope`): the `ChordScope.FindInNote` rows are exactly `windows.findInNote.next` Enter, `windows.findInNote.previous` Shift+Enter, `windows.findInNote.close` Escape, `slate.editor.findNext` Ctrl+G and `slate.editor.findPrevious` Ctrl+Shift+G; the three `windows.findInNote.*` rows are surface rows (`IsCommandId` and `IsRegistered` false, `MacChord` null, a reason naming the command they run); the two `slate.editor.find*` rows carry no mac chord; no `ChordScope.Editor` row shares a chord with them except `windows.editor.closePopover`'s Escape, in its own scope; and `KeyBindingChords("FindInNoteBar.xaml")` is empty. Mutations: declare `windows.findInNote.close` in `ChordScope.Editor` → this fact and `CommandRows_HaveNoActiveChordCollision`'s surface loop fail (two Escape rows in `Editor`); drop the Ctrl+Shift+G `AddChord` from `Bind` → the scoped comparison fails (a row nothing delivers); deliver Ctrl+G through a `KeyBinding` in `FindInNoteBar.xaml` → the empty-bindings assertion fails; write the previous row as "Shift+Ctrl+G" → the scoped comparison fails (the scrape's canonical string is `Ctrl+Shift+G`).
- `ThemeTokenContrastTests`: the match brush against both editor backgrounds.
- `WcMatrixEvidenceCensus`: the new ids registered; the new row's evidence executable.

FlaUI (`ShellAccessibilityTests.FindInNote.cs`):
`FindInNote_TypingAnnouncesTheCountAndEnterMovesTheCaret` — opens a
fixture note, Ctrl+F, types, reads `FindInNoteCount`'s Name, Enter, and
asserts the editor's Text pattern selection moved; `AssertAxeClean(process, "find-in-note")`
with text typed (the #1106 lesson). Journey naming follows
`Palette_TypingAnnouncesOnlyTheFinalCountAndSelection` (`ShellAccessibilityTests.Palette.cs:43`).

## Docs and registers

- `chords.json` regenerated (`SLATE_CHORDS_UPDATE=1`): `slate.editor.findInNote` (Global), `slate.editor.findNext` and `slate.editor.findPrevious` in `commands` and the three `windows.findInNote.*` rows in `chordSurface`, all five bar rows with `"scope": "FindInNote"`; `generate-parity-matrix.py`:
  drop the `slate.editor.findInNote` override, add the two Windows-only
  ids to the Windows-only arm, add the "find-in-selection: unverified on
  mac; not shipped" note; regenerate `parity_matrix.md`; `--check` in CI.
- `w_c_matrix.md`: new row "Find in note (WB-22)" with ten cells (Group
  landmark; Edit, Text, Button, CheckBox children; Value/Invoke/Toggle;
  the focus route above; the count's notification contract; evidence
  names; Narrator/NVDA cells filled from the pass). `w_c_automation_ids.json`
  gains the eleven ids with that surface title.
- `a11y_trigger_designations.json`: the `FindInNotePhrase` residue
  entries; `python -I scripts/a11y_trigger_ledger.py --write` regenerates
  contract 38's ledger.
- Contract 28: append a note that the find bar is a non-modal landmark
  (P5 honoured, no `Popup`); contract 25: a note that the Bases filter
  keeps Ctrl+F on a Base (WD12).
- Help: the Windows keyboard page gains Ctrl+F, and for the bar Enter /
  Shift+Enter (in the query), Ctrl+G, Ctrl+Shift+G and Escape. `SlateTextEditor.cs:538` gets the rationale
  comment the issue asks for.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb22'
function Assert-Ok([string] $What) { if ($LASTEXITCODE -ne 0) { throw "$What exited $LASTEXITCODE" } }
./apps/slate-windows/generate-bindings.ps1 -Locked; Assert-Ok 'generate-bindings'
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release; Assert-Ok 'dotnet build'
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
$env:SLATE_CHORDS_UPDATE = '1'
dotnet test $tests --no-build --configuration Release --filter "FullyQualifiedName~SlateWindows.Tests.ChordTableTests.ChordsJson_IsExactlyTheTablesProjection"; $chords = $LASTEXITCODE
Remove-Item Env:SLATE_CHORDS_UPDATE
if ($chords -ne 0) { throw "chords.json regeneration exited $chords" }
$filter = "FullyQualifiedName~FindInNoteViewModelTests|FullyQualifiedName~FindInNotePhraseTests|FullyQualifiedName~FindInNoteBarTests|FullyQualifiedName~ChordTableTests|FullyQualifiedName~CommandRegistrationTests|FullyQualifiedName~WcMatrixEvidenceCensus|FullyQualifiedName~ThemeTokenContrastTests|FullyQualifiedName~AvalonDocumentBufferCensus|FullyQualifiedName~A11yCorpusCensus"
$names = @(dotnet test $tests --no-build --configuration Release --list-tests --filter $filter | Select-String '^\s{4}\S' | ForEach-Object { ($_.Line.Trim() -split '\(', 2)[0] }); Assert-Ok 'test discovery'
$required = @(
  'FindInNoteViewModelTests.TypingFindsAllMatchesAndReportsOneOfM',
  'FindInNoteViewModelTests.NextWrapsToTheTopAndSaysSo',
  'FindInNoteViewModelTests.PreviousWrapsToTheEndAndSaysSo',
  'FindInNoteViewModelTests.MatchCaseAndWholeWordNarrowTheMatches',
  'FindInNoteViewModelTests.ReplaceReplacesTheCurrentHitAndSteps',
  'FindInNoteViewModelTests.ReplaceAllIsOneUndoStep',
  'FindInNoteViewModelTests.ADocumentEditWhileOpenRecountsOnTheDebounce',
  'FindInNoteViewModelTests.ReplaceWithNoCurrentHitRevealsTheNextAndWritesNothing',
  'FindInNoteViewModelTests.AnEmptyQueryClearsHighlightsAndCountsNothing',
  'FindInNoteViewModelTests.CloseRestoresTheCaretAndSelectionTheOpenCaptured',
  'FindInNoteBarTests.CtrlFInANoteFocusesTheQueryAndSelectsAll',
  'FindInNoteBarTests.CtrlFOnABaseTabReachesTheQuickFilterNotTheBar',
  'FindInNoteBarTests.CtrlFOnACanvasReachesTheCanvasFilterNotTheBar',
  'FindInNoteBarTests.CtrlFInReadingModeOrWithNoTabOpensVaultSearch',
  'FindInNoteBarTests.CtrlFOnTheWelcomeViewWithNoWorkspaceReachesVaultSearch',
  'FindInNoteBarTests.AnEditBeforeTheDebounceNeverReplacesStaleText',
  'FindInNoteBarTests.AnEditElsewhereBeforeTheDebounceStillReplacesTheTrackedHit',
  'FindInNoteBarTests.AnOptionChangeBeforeReplaceRecountsFirst',
  'FindInNoteBarTests.CloseAfterAShortenedDocumentRestoresAClampedCaret',
  'FindInNoteBarTests.EnterAndCtrlGStepNextAndShiftStepsPrevious',
  'FindInNoteBarTests.TheBarsKeysActOnlyWithFocusInTheOpenBar',
  'FindInNoteBarTests.EscapeClosesClearsHighlightsAndReturnsFocusToTheEditor',
  'FindInNoteBarTests.TheBarIsASiblingOfTheEditorPeerNotAChild',
  'FindInNoteBarTests.TypingAnnouncesOnlyTheFinalCount',
  'ChordTableTests.CommandRows_HaveNoActiveChordCollision',
  'ChordTableTests.EveryScopeIsEitherScrapedFromProductionOrDispositioned',
  'ChordTableTests.TableCoversEveryDeclarativelyBoundChord_AndNothingStale',
  'ChordTableTests.FindInNoteKeysAreRowsInTheirOwnScope',
  'ChordTableTests.ChordsJson_IsExactlyTheTablesProjection')
$missing = @($required | Where-Object { $r = ".$_"; -not ($names | Where-Object { $_.EndsWith($r) }) })
if ($missing.Count -gt 0) { throw "the filter does not select: $($missing -join ', ')" }
if (-not ($names | Where-Object { $_ -match '\.FindInNotePhraseTests\.' })) { throw 'no FindInNotePhraseTests fact discovered' }
dotnet test $tests --no-build --configuration Release --filter $filter; Assert-Ok 'unit and hosted facts'
# desktop free, NVDA closed (the FlaUI facts live in the partial class ShellAccessibilityTests, so select by method name):
$at = 'apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj'
$atFilter = 'FullyQualifiedName~FindInNote_TypingAnnouncesTheCountAndEnterMovesTheCaret'
$atNames = @(dotnet test $at --no-build --configuration Release --list-tests --filter $atFilter | Select-String '^\s{4}\S'); Assert-Ok 'FlaUI discovery'
if ($atNames.Count -lt 1) { throw "the FlaUI filter selects nothing: $atFilter" }
dotnet test $at --no-build --configuration Release --filter $atFilter; Assert-Ok 'FlaUI journey'
python -I scripts/generate-parity-matrix.py; Assert-Ok 'parity matrix'
python -I scripts/generate-parity-matrix.py --check; Assert-Ok 'parity matrix check'
python -I scripts/a11y_trigger_ledger.py --write; Assert-Ok 'trigger ledger'
python -I scripts/a11y_trigger_ledger.py --check; Assert-Ok 'trigger ledger check'
$files = @(
  'apps/slate-windows/src/SlateWindows/FindInNoteViewModel.cs',
  'apps/slate-windows/src/SlateWindows/FindInNoteBar.xaml.cs',
  'apps/slate-windows/src/SlateWindows/FindMatchBackgroundRenderer.cs',
  'apps/slate-windows/src/SlateWindows/Panels/FindInNotePhrase.cs',
  'apps/slate-windows/src/SlateWindows/WorkspaceTabViewModel.cs',
  'apps/slate-windows/src/SlateWindows/WorkspaceViewModel.cs',
  'apps/slate-windows/src/SlateWindows/VaultLifecycleViewModel.cs',
  'apps/slate-windows/src/SlateWindows/SlateTextEditor.cs',
  'apps/slate-windows/src/SlateWindows/Commands/ChordTable.cs',
  'apps/slate-windows/src/SlateWindows/Commands/SlateCommandRegistrar.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/FindInNoteViewModelTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/FindInNotePhraseTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/FindInNoteBarTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/ChordTableTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/ThemeTokenContrastTests.cs',
  'apps/slate-windows/tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.FindInNote.cs')
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $files; Assert-Ok 'dotnet format'
```

CI: the Windows lane and the FlaUI shell gate. AT: NVDA full pass and
Narrator — Ctrl+F in a note: "Find in note, edit"; typing "the": "3 of
12"; Enter: "4 of 12", the editor selection read; Enter at the last
hit: "1 of 12, wrapped to the top"; Shift+Enter; Tab to "Match case,
check box, not checked"; Replace All: "Replaced 12 matches."; Escape:
the editor's filename line and the caret where it was; Ctrl+F on the
welcome/no tab: the search overlay's own line; Ctrl+F on a Base: "Quick
filter, edit"; Ctrl+F on a canvas: the canvas filter field's own name,
never the find bar. Recorded in the PR body; draft until then.

## Acceptance

- [ ] WD12 recorded in the PR body (disposition 1 — the three-command key; replace in, regex out; Ctrl+G rows; find-in-selection deferred).
- [ ] Ctrl+F's command group is recorded whole: under disposition 1 the one sorted key `slate.bases.quickFilter | slate.canvas.filterCards | slate.editor.findInNote`, its reason naming each row's delivery, replaces the `slate.bases.quickFilter | slate.canvas.filterCards` entry; under disposition 2 `slate.canvas.filterCards | slate.editor.findInNote` replaces it; `CommandRows_HaveNoActiveChordCollision`, with its new live-key assertion, is green; `CtrlFOnABaseTabReachesTheQuickFilterNotTheBar` and `CtrlFOnACanvasReachesTheCanvasFilterNotTheBar` show the delivery the reason claims, with the window's Ctrl+F a bubbling `KeyBinding`.
- [ ] Every fact above fails first, then passes; mutations run locally; the FlaUI journey green on the hosted lane.
- [ ] `Replace` acts only on the tracked hit it last revealed: every `AnEditBeforeTheDebounceNeverReplacesStaleText` case (another match survives, no match remains, the hit's middle changed) refuses with "Match changed; nothing replaced.", and `AnEditElsewhereBeforeTheDebounceStillReplacesTheTrackedHit` replaces the hit its anchors followed.
- [ ] The bar's keys live in `ChordScope.FindInNote` only: its five rows (the Enter, Shift+Enter and Escape surface rows; the Ctrl+G and Ctrl+Shift+G command rows) are delivered by `FindInNoteBar.Bind`'s map and scraped both ways by `FindInNoteChords()`; no `ChordScope.Editor` row changes and `SlateTextEditor.OnPreviewKeyDown` gains no arm; `FindInNoteKeysAreRowsInTheirOwnScope`, `TableCoversEveryDeclarativelyBoundChord_AndNothingStale`, `EveryScopeIsEitherScrapedFromProductionOrDispositioned` and `TheBarsKeysActOnlyWithFocusInTheOpenBar` are green.
- [ ] `GetChildrenCore` unchanged and its census green; the bar is a sibling.
- [ ] `chords.json`, `parity_matrix.md`, `w_c_matrix.md`, `w_c_automation_ids.json`, the designations and the ledger regenerated; `--check` scripts green.
- [ ] NVDA and Narrator passes recorded.
- [ ] PR body: `Closes #1112`; "not delivered: regex (parity), find-in-selection (unverified on mac), reading-mode find (mac defers it)".

## Brief

Read `SlateTextEditor.cs:215-245` and `:525-545`,
`WorkspaceTemplates.xaml:331-345`, `:480-500`, `:585-600`,
`Bases/BaseSurfaceView.cs:50-200`, `Grids/AccessibleDataGrid.cs:200-225`,
`Commands/ChordTable.cs:22-98`, `:617-690`, `:794-796`, `:915-921`, `:1361-1364`, `:1720-1732`, `:1850-1890`,
`Canvas/CanvasNavigator.cs:230-240`, `:1855-1874`, `MainWindow.xaml:30-60`,
`Commands/SlateCommandRegistrar.cs:40-70` and `:413-460`,
`VaultLifecycleViewModel.cs:270-290`, `ChordTableTests.cs:283-470`,
`:778-940`, `:1093-1206` and `:1542-1562`, `MainWindow.xaml.cs:858-990`, `AutomationLandmark.cs`, `Censuses/WcMatrixEvidenceCensus.cs:36-75`,
`TaskStatusPhraseTests.cs`, `ShellAccessibilityTests.Palette.cs:43`,
`ShellAccessibilityTests.cs:69` and `:471` (`AssertAxeClean`), and mac's
`NoteEditorView.swift:240-250`, `:1426-1432`, `AppState.swift:9490-9535`,
`SlateCommands.swift:1895-1906`. Decision WD12 is pending (recommended:
disposition 1 — one `SharedCommandChords` entry for the whole Ctrl+F
command group, the sorted three-command key `slate.bases.quickFilter | slate.canvas.filterCards | slate.editor.findInNote`
(Ctrl+F already belongs to `slate.canvas.filterCards`,
`ChordTable.cs:1361-1364`, and `CommandRows_HaveNoActiveChordCollision`
looks up one key holding every command on the chord,
`ChordTableTests.cs:339-345`), replacing the `slate.bases.quickFilter | slate.canvas.filterCards`
entry; replace in; regex out; Ctrl+G / Shift+Ctrl+G as `slate.editor.findNext` /
`slate.editor.findPrevious`, Windows-only rows live in the bar; find-in-selection
deferred and recorded in the parity matrix as "unverified on mac; not
shipped"; under disposition 2 `slate.bases.quickFilter` becomes
`win: null`, the Bases menu loses its gesture text, and the then-stale
`slate.bases.quickFilter | slate.canvas.filterCards` entry is replaced by
`slate.canvas.filterCards | slate.editor.findInNote`; under "regex yes"
a `SearchMode` toggle and facts are added as a recorded divergence).
Build: `FindInNoteViewModel` per Markdown tab (Query, MatchCase,
WholeWord, ReplaceText, IsOpen, CurrentIndex, TotalCount, Error,
FocusRequested; Next, Previous, Replace, ReplaceAll, Close) over
`SearchStrategyFactory.Create(pattern, ignoreCase, matchWholeWords, SearchMode.Normal)`
with a 150 ms debounce, manual wrap, result sets stamped with
`TextDocument.Version` and an options revision; a query or option
recompute reveals the first hit at or after the caret, a document-edit
recompute only recounts and re-highlights (never moves the caret or
selection; keeps the tracked hit current if it still validates, else
clears it). The current hit is kept as `TrackedHit` (new private
record: `TextAnchor Start` with `AnchorMovementType.AfterInsertion`,
`TextAnchor End` with `AnchorMovementType.BeforeInsertion`, both
`SurviveDeletion = true`, from `TextDocument.CreateAnchor`; `int
Length`), set whenever a hit becomes current (query or option
recompute, `Next`, `Previous`, the step after `Replace`); it validates
when `End.Offset - Start.Offset == Length` and the live strategy's
`FindNext(document, Start.Offset, Length)` returns exactly that offset
and length. `Next`, `Previous` and `ReplaceAll` flush and recompute
synchronously when either stamp is stale, then act (`ReplaceAll` in one
`BeginUpdate`/`EndUpdate` over the fresh set). `Replace` never picks its
target from a recomputation: it validates the tracked hit first —
valid → `document.Replace(Start.Offset, Length, ReplaceText)` (one undo
step), recompute, step to the next hit; invalid → write nothing, clear
the tracked hit, recompute for the count only (caret unmoved,
`CurrentIndex` 0) and speak only "Match changed; nothing replaced.",
whether or not another match survives; no tracked hit → act as `Next`
and write nothing. Caret and selection are kept as `TextAnchor`s from
the open and restored clamped on Close; `FindMatchBackgroundRenderer : IBackgroundRenderer`
(`KnownLayer.Selection`, theme token `Slate.FindMatchBrush` in both
themes) registered while open; `FindInNoteBar` at a new `Grid.Row="2"`
of `WorkspaceTabContentTemplate` (status to Row 3, popover overlay
`RowSpan="4"`), rooted at an `AutomationLandmarkGrid` named "Find in
note" with ids `FindInNoteQuery`, `FindInNoteCount`, `FindInNotePrevious`,
`FindInNoteNext`, `FindInNoteMatchCase`, `FindInNoteWholeWord`,
`FindInNoteReplaceText`, `FindInNoteReplace`, `FindInNoteReplaceAll`,
`FindInNoteClose`, `FindInNoteError`, not modal (no `ModalSurface`
member, no `IsDialog`, no focus scope); `VaultLifecycleViewModel.FindInNoteCommand`
(the `ISlateCommandHost`) bound to a window-level Ctrl+F `KeyBinding`
in `Window.InputBindings` (bubbling, so the Base grid's `FilterCommand`
and the canvas surface's tunnelling `FilterCardsFromKey` claim the key
first — never a case in `Window_PreviewKeyDown`) and to the registrar
as `host => host.FindInNoteCommand`, routing a
Markdown tab in editor mode to `Workspace.OpenFindInNote()` and
everything else — reading mode, non-note tab, no tab, no workspace —
to `ToggleSearchCommand`; chord rows `Reg(Ids.EditorFindInNote, "Find…", CommandSection.Editor, "Find and highlight text within the current note.", "⌘F", "Ctrl+F", ChordScope.Global)`
plus the bar's own key route — a new `ChordScope.FindInNote` (an enum
summary like `SidebarFilter`'s; never `ChordScope.Editor`, where
`windows.editor.closePopover` already ships Escape, the surface-row loop
`ChordTableTests.cs:371-379` refuses a second Escape row in one scope,
and the editor's scrape cannot express Ctrl+Shift+G) delivered by
`FindInNoteBar.Bind()` (new, three-argument `AddChord(Key.X,
ModifierKeys.Y, Func<bool>)` calls) from the bar's tunnelling
`OnPreviewKeyDown`, so it is live only with focus inside the open bar:
Enter → Next and Shift+Enter → Previous only while the query holds the
focus (a focused button keeps its own Enter), Ctrl+G → Next,
Ctrl+Shift+G → Previous and Escape → Close anywhere in the bar; no
`KeyBinding` in `FindInNoteBar.xaml`, no new arm in
`SlateTextEditor.OnPreviewKeyDown`; rows `slate.editor.findNext`
("Ctrl+G") and `slate.editor.findPrevious` ("Ctrl+Shift+G") with
`mac: null`, and surface rows `windows.findInNote.next` ("Enter"),
`windows.findInNote.previous` ("Shift+Enter") and
`windows.findInNote.close` ("Escape"), all in `ChordScope.FindInNote`;
`ScopedProductionChords()` gains `[ChordScope.FindInNote] =
FindInNoteChords()` (new; `CSharpSource.Load("FindInNoteBar.xaml.cs")
.Method("Bind")`, shaped like `GraphChords()`); the `SharedCommandChords` entry
`["slate.bases.quickFilter | slate.canvas.filterCards | slate.editor.findInNote"]`
with its delivery reason (a Base's grid gesture and a canvas's
tunnelling handler — the canvas table's grid `FilterCommand` subscribed
to the canvas filter — claim the key before the window's bubbling
binding, which acts only on what no surface claimed), replacing the
`slate.bases.quickFilter | slate.canvas.filterCards` entry; an Editor-menu item "_Find…" (Windows has no
Edit menu) with `InputGestureText="{cmd:ChordText slate.editor.findInNote}"`;
`Panels/FindInNotePhrase` ("{N} of {M}", "No matches", "Wrapped to the
top", "Wrapped to the end", "Replaced {M} matches.", "Match changed;
nothing replaced.") spoken as HostComposed Medium after the debounce;
the rationale comment at `SlateTextEditor.cs:538`. Facts, failing
first — `FindInNoteViewModelTests`: `TypingFindsAllMatchesAndReportsOneOfM`,
`NextWrapsToTheTopAndSaysSo`, `PreviousWrapsToTheEndAndSaysSo`,
`MatchCaseAndWholeWordNarrowTheMatches`, `ReplaceReplacesTheCurrentHitAndSteps`,
`ReplaceAllIsOneUndoStep`, `ADocumentEditWhileOpenRecountsOnTheDebounce`
(the recount moves neither caret nor selection; a still-valid tracked
hit stays current, a broken one is cleared),
`ReplaceWithNoCurrentHitRevealsTheNextAndWritesNothing` ("a b a" with
the tracked hit cleared by editing the first "a" to "x": Replace leaves
"x b a", selects offset 4 and speaks "1 of 1"; a second Replace writes
"x b q"), `AnEmptyQueryClearsHighlightsAndCountsNothing`,
`CloseRestoresTheCaretAndSelectionTheOpenCaptured`;
`FindInNotePhraseTests` pinning every string verbatim;
`FindInNoteBarTests` (hosted shell): `CtrlFInANoteFocusesTheQueryAndSelectsAll`,
`CtrlFOnABaseTabReachesTheQuickFilterNotTheBar`,
`CtrlFOnACanvasReachesTheCanvasFilterNotTheBar` (a canvas tab, the
board holding the keys: Ctrl+F sets the canvas's `FilterFocusRequest`,
the bar stays closed, vault search does not open),
`CtrlFInReadingModeOrWithNoTabOpensVaultSearch`,
`CtrlFOnTheWelcomeViewWithNoWorkspaceReachesVaultSearch`,
`EnterAndCtrlGStepNextAndShiftStepsPrevious` (focus in the query),
`TheBarsKeysActOnlyWithFocusInTheOpenBar` (Enter on a focused Replace
All runs Replace All; with the bar open and the focus in the editor,
Enter types a newline, Ctrl+G and Ctrl+Shift+G change nothing and
Escape leaves the bar open; with an editor popover open, Escape closes
the popover, not the bar; with the bar closed, Ctrl+G and Ctrl+Shift+G
do nothing),
`EscapeClosesClearsHighlightsAndReturnsFocusToTheEditor`,
`TheBarIsASiblingOfTheEditorPeerNotAChild`, `TypingAnnouncesOnlyTheFinalCount`,
`AnEditBeforeTheDebounceNeverReplacesStaleText` (a Theory: the first
hit current, an edit inside the 150 ms window, then Replace with "q" —
*another match survives*: query "a", "a b a", `document.Replace(0, 1,
"x")`, stays exactly "x b a", `TotalCount` 1; *no match remains*: query
"a", "a b c", the same edit, stays exactly "x b c", `TotalCount` 0;
*the hit's middle changed*: query "abc", "abc d abc",
`document.Replace(1, 1, "x")`, stays exactly "axc d abc"; in every case
the one line spoken is "Match changed; nothing replaced.", never
"1 of 1" or "No matches", and the caret is where the edit left it),
`AnEditElsewhereBeforeTheDebounceStillReplacesTheTrackedHit` ("a b a",
Next to offset 4, "zz" inserted at 0 inside the window, Replace with
"q" gives exactly "zza b q", no refusal spoken),
`AnOptionChangeBeforeReplaceRecountsFirst`,
`CloseAfterAShortenedDocumentRestoresAClampedCaret`;
`ThemeTokenContrastTests` for the match brush; `ChordTableTests`
(`CommandRows_HaveNoActiveChordCollision` with the three-command key and
a new staleness assertion that every recorded key is a live group's
key,
`EveryScopeIsEitherScrapedFromProductionOrDispositioned` (the new scope
scraped), `TableCoversEveryDeclarativelyBoundChord_AndNothingStale` (the
new scope's five rows both ways against `FindInNoteChords()`),
`FindInNoteKeysAreRowsInTheirOwnScope` (exactly the five rows; the
three `windows.findInNote.*` rows unregistered surface rows with no mac
chord; no `ChordScope.Editor` row shares their chords but
`windows.editor.closePopover`'s Escape, in its own scope;
`KeyBindingChords("FindInNoteBar.xaml")` empty),
`ChordsJson_IsExactlyTheTablesProjection` regenerated);
`WcMatrixEvidenceCensus` with the eleven ids registered; FlaUI
`ShellAccessibilityTests.FindInNote_TypingAnnouncesTheCountAndEnterMovesTheCaret`
with `AssertAxeClean(process, "find-in-note")` after text is typed.
Mutations to run locally: drop the fall-through arm; resolve the command
through `Workspace?.`; recompute before validating and act on the
recomputed current hit (fails every Theory case: "x b q", "No matches"
spoken, "axc d q"); validate by span only, without the `FindNext` check
(the middle-changed case writes "q d abc"); validate at the offset captured when the
hit became current instead of through the anchors (the edit-elsewhere
fact refuses); with no tracked hit, recompute and replace the first hit
(the no-current-hit fact writes on the first press); let a
document-edit recompute reveal a hit (the caret moves); act on the
stamped set after an option change; restore the raw offset on Close;
record only a base/editor pair (the group's lookup fails); keep the
base/canvas pair beside the triple (the live-key assertion fails);
remove the triple (the lookup fails); deliver the window's Ctrl+F from
`Window_PreviewKeyDown` (the Base and canvas facts fail); declare
`windows.findInNote.close` in `ChordScope.Editor` (the scope fact and
the surface loop fail); drop the Ctrl+Shift+G `AddChord` (the scoped
comparison fails); deliver Ctrl+G through a `KeyBinding` in
`FindInNoteBar.xaml` (the empty-bindings assertion fails); write the
previous row as "Shift+Ctrl+G" (the scoped comparison fails); run the
bar's map from a route above the editor (Enter in the editor steps the
find); let Enter act whatever holds the focus (Enter on Replace All
steps Next). Registers: `apps/slate-windows/chords.json`
(regenerated with the `FindInNote` scope's five rows and the Global
Ctrl+F row; `deliveryEvidence.commands` gains the three ids),
`scripts/generate-parity-matrix.py` (drop the override, add the two
Windows-only ids, the find-in-selection note) and
`docs/plans/18_windows_port/parity_matrix.md` regenerated with `--check`
green, a ten-cell row "Find in note (WB-22)" in
`docs/plans/18_windows_port/w_c_matrix.md`, the ids in
`docs/plans/18_windows_port/w_c_automation_ids.json`, the
`FindInNotePhrase` residue entries in
`docs/plans/18_windows_port/a11y_trigger_designations.json` and
`python -I scripts/a11y_trigger_ledger.py --write`, notes appended to
`docs/plans/28_palette_contracts.md` (non-modal landmark, P5 honoured)
and `docs/plans/25_bases_grid_contracts.md` (the Bases filter keeps
Ctrl+F on a Base), the Windows keyboard help page. Done when the Windows
lane and the FlaUI gate are green, the Verification block runs clean
in PowerShell (every command's `$LASTEXITCODE` checked; the discovery
step throws unless each of the twenty-nine named facts and at least one
`FindInNotePhraseTests` fact is selected, and the FlaUI filter selects
its journey), `dotnet format --include` over the sixteen files is clean,
and the NVDA and Narrator passes (Ctrl+F: "Find
in note, edit"; typing: "3 of 12"; Enter: "4 of 12"; Enter at the last
hit: "1 of 12, wrapped to the top"; Match case check box; Replace All:
"Replaced 12 matches."; Escape returns to the editor; Ctrl+F on the
welcome view opens the search overlay; Ctrl+F on a Base: "Quick filter,
edit"; Ctrl+F on a canvas: the canvas filter field, never the find bar)
are in the PR body — draft until then. Not delivered: regex
(parity), find-in-selection (unverified on mac), reading-mode find (mac
defers it). PR body: `Closes #1112`. Commit: `feat(windows): ctrl+f
finds in the open note with a landmark find bar, replace and a spoken
count`.
