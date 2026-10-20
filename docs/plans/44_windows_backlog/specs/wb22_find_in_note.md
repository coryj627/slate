# WB-22 — Ctrl+F finds in the open note: a landmark find bar beside the editor with next, previous, wrap, case, whole word, replace and a spoken count

Issues: Closes #1112. Size L. Lanes: windows + FlaUI. AT: NVDA, Narrator. Wave: W8. Depends on: WB-21b (the `MainWindow.xaml` Editor menu, `§6`'s order), WB-17 and WB-21 (`ChordTable.cs`, `chords.json`). Parallel with: WB-23, WB-24. Decision: WD12 (pending; recommended: disposition 1 via `SharedCommandChords`, replace in, regex out, Ctrl+G / Shift+Ctrl+G as next and previous, find-in-selection deferred; under disposition 2 the `slate.bases.quickFilter` row becomes `win: null` and the Bases menu item loses its gesture text; under "regex yes" a `SearchMode` toggle and its facts are added as a recorded Windows-only divergence).

## Goal

Mac has had find-in-note since #874 (AppKit's stock find bar); Windows
has nothing — Ctrl+F in a note is dead. After this chunk a Windows
reader presses Ctrl+F in a note and lands in a named "Find in note"
field beside the editor; typing highlights matches and the bar speaks
"N of M"; Enter / Shift+Enter and Ctrl+G / Shift+Ctrl+G step with
wrap-around; case and whole-word toggles and Replace / Replace All are
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
  (`e.ContinueRouting = FilterRequested is null`). No window-level
  Ctrl+F; no `Ctrl+G` row anywhere in the table. The vault search chord
  is `Ctrl+Shift+F` (`:794-796`, `slate.view.toggleSearch`), resolved to
  `host.ToggleSearchCommand` (`SlateCommandRegistrar.cs:456`,
  `VaultLifecycleViewModel.cs:280`).
- The collision rule: `ChordTableTests.cs:328-360`
  `CommandRows_HaveNoActiveChordCollision` — two command rows may share
  a chord only when `SharedCommandChords` (`:283`) carries the ordinal
  id pair with a reason (the scope-keyed carve-out the issue asked for
  already exists; its "disposition 1" is one dictionary entry).
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
  mac; not shipped', so no Mac probe gates the chunk."
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
WholeWord). `Next`, `Previous`, `Replace` and `ReplaceAll` first compare
the stamps with the live document version and the current options
revision; when either differs they flush the debounce and recompute
synchronously before acting (no refusal is needed for navigation —
recomputation is the correct answer; with zero matches after it the
count line is spoken and nothing moves). `Replace` additionally
re-verifies the current segment: `strategy.FindNext(document, segment.StartOffset, segment.Length)`
must return a result at exactly that offset and length, else the
segment is dropped, the set recomputed, and "Match changed; nothing
replaced." spoken — the host never rewrites text the pattern no longer
matches. `Replace` replaces through the document (one undo step), then
steps; `ReplaceAll` runs inside one `BeginUpdate`/`EndUpdate` over the
freshly recomputed set so Ctrl+Z undoes it whole. Opening the bar stores
the caret and selection as `TextAnchor`s (`TextDocument.CreateAnchor`,
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
Canvas tab → their own grid/canvas filters already handle Ctrl+F before
it bubbles (`AccessibleDataGrid.cs:217`), so the global arm does nothing
when the surface claimed the key; a reading-mode note, a non-note tab,
no tab, or no workspace at all (welcome view) → `ToggleSearchCommand.Execute(null)`
(mac's fall-through; the overlay's own no-vault behaviour is unchanged;
reading-mode find stays out of scope like mac's). Enter / Shift+Enter in the query
→ Next / Previous; Ctrl+G / Shift+Ctrl+G anywhere in the tab →
Next / Previous (chord-table rows `slate.editor.findNext` /
`slate.editor.findPrevious` with `mac: null` — Windows-only rows, the
parity matrix's Windows-only arm). Escape in the bar → Close (clears
highlights, restores the caret and selection the open captured, focuses
the editor) — recorded in the Escape chain beside
`windows.editor.closePopover` (`ChordTable.cs:1855-1859`).

**Chord table and menu.** `Reg(Ids.EditorFindInNote /* slate.editor.findInNote */, "Find…", CommandSection.Editor, "Find and highlight text within the current note.", "⌘F", "Ctrl+F", ChordScope.Global)`;
`SharedCommandChords["slate.bases.quickFilter | slate.editor.findInNote"] = "Ctrl+F. Disjoint by DELIVERY: the grid's KeyGesture handles it on a Base before it bubbles; the window binding acts only on a note in editor mode (WD12 disposition 1)."`;
Editor menu item "_Find…" with `InputGestureText="{cmd:ChordText slate.editor.findInNote}"`
and `AcceleratorKey` bound like `:437-441`. Registrar:
`[Ids.EditorFindInNote] = host => host.FindInNoteCommand` (the lifecycle host's, never `Workspace?.`).
`chords.json` regenerated; `deliveryEvidence.commands` gains the id →
`"editorHost"` (or a new `findInNote` group naming the view model and
the FlaUI journey).

**Speech.** The count is a host-composed §W-C golden family
`FindInNotePhrase` (new, `Panels/`-style static class): "{N} of {M}",
"No matches", "Wrapped to the top" / "Wrapped to the end" appended once
on a wrap, "Replaced {M} matches." after Replace All, and the pattern
error verbatim. Spoken through `HostComposed` at Medium, coalesced to
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
- `ADocumentEditWhileOpenRecountsOnTheDebounce`.
- `AnEmptyQueryClearsHighlightsAndCountsNothing`.
- `CloseRestoresTheCaretAndSelectionTheOpenCaptured`.

`FindInNotePhraseTests` (the `TaskStatusPhraseTests` shape): every
string pinned verbatim.

Hosted (`W2EditorInteractionTests` / a new `FindInNoteBarTests` with the
`CommandPaletteTests.ShellKeys` shell): 
- `CtrlFInANoteFocusesTheQueryAndSelectsAll`; `CtrlFOnABaseTabReachesTheQuickFilterNotTheBar`; `CtrlFInReadingModeOrWithNoTabOpensVaultSearch` (the fall-through); `CtrlFOnTheWelcomeViewWithNoWorkspaceReachesVaultSearch` (hosted shell, `Workspace == null`: `ToggleSearchCommand` executes; nothing throws). Mutation: remove the fall-through arm, or resolve the command through `Workspace?.` → the overlay stays closed.
- `AnEditBeforeTheDebounceNeverReplacesStaleText` — type a query, edit the document inside the 150 ms window so the current hit no longer matches, press Replace: the document is unchanged, "Match changed; nothing replaced." spoken, the count recomputed. Mutation: skip the `FindNext` re-verification → the edited text is overwritten.
- `AnOptionChangeBeforeReplaceRecountsFirst` — toggle Match case then Replace All within the debounce: only the case-sensitive matches are replaced (the set was recomputed synchronously). Mutation: act on the stamped set → the wrong matches change.
- `CloseAfterAShortenedDocumentRestoresAClampedCaret` — open at offset 40, delete the tail so the document is 10 chars, Escape: caret at 10, no exception. Mutation: restore the raw captured offset → `ArgumentOutOfRangeException`.
- `EnterAndCtrlGStepNextAndShiftStepsPrevious`.
- `EscapeClosesClearsHighlightsAndReturnsFocusToTheEditor` (the renderer is unregistered — assert `BackgroundRenderers` count).
- `TheBarIsASiblingOfTheEditorPeerNotAChild` — the editor's `GetChildrenCore` is unchanged (`AvalonDocumentBufferCensus` stays green) and the bar's peer hoists to the tab content.
- `TypingAnnouncesOnlyTheFinalCount` (coalescing).
- `ChordTableTests`: `CommandRows_HaveNoActiveChordCollision` passes with the pair; `EveryScopeIsEitherScrapedFromProductionOrDispositioned` (`:1105`) — the Global arm's both-direction check sees the new `KeyBinding`; `ChordsJson_IsExactlyTheTablesProjection` regenerated.
- `ThemeTokenContrastTests`: the match brush against both editor backgrounds.
- `WcMatrixEvidenceCensus`: the new ids registered; the new row's evidence executable.

FlaUI (`ShellAccessibilityTests.FindInNote.cs`):
`FindInNote_TypingAnnouncesTheCountAndEnterMovesTheCaret` — opens a
fixture note, Ctrl+F, types, reads `FindInNoteCount`'s Name, Enter, and
asserts the editor's Text pattern selection moved; `AssertAxeClean(process, "find-in-note")`
with text typed (the #1106 lesson). Journey naming follows
`Palette_TypingAnnouncesOnlyTheFinalCountAndSelection` (`ShellAccessibilityTests.Palette.cs:43`).

## Docs and registers

- `chords.json` regenerated (`SLATE_CHORDS_UPDATE=1`); `generate-parity-matrix.py`:
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
- Help: the Windows keyboard page gains Ctrl+F, Ctrl+G, Shift+Ctrl+G,
  Escape for the bar. `SlateTextEditor.cs:538` gets the rationale
  comment the issue asks for.

## Verification

```powershell
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb22'; ./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0 before reading counts
$env:SLATE_CHORDS_UPDATE='1'; dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~ChordTableTests.ChordsJson_IsExactlyTheTablesProjection"; Remove-Item Env:SLATE_CHORDS_UPDATE
$filter = "FullyQualifiedName~FindInNoteViewModelTests|FullyQualifiedName~FindInNotePhraseTests|FullyQualifiedName~FindInNoteBarTests|FullyQualifiedName~ChordTableTests|FullyQualifiedName~CommandRegistrationTests|FullyQualifiedName~WcMatrixEvidenceCensus|FullyQualifiedName~ThemeTokenContrastTests|FullyQualifiedName~AvalonDocumentBufferCensus|FullyQualifiedName~A11yCorpusCensus"
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --no-build --configuration Release --list-tests --filter $filter | Select-String "FindInNoteViewModelTests|FindInNotePhraseTests|FindInNoteBarTests" | Measure-Object | ForEach-Object { if ($_.Count -lt 20) { throw "new facts not discovered: $($_.Count)" } }
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --no-build --configuration Release --filter $filter
# desktop free, NVDA closed (the FlaUI facts live in the partial class ShellAccessibilityTests, so select by method name):
dotnet test apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj --no-build --configuration Release --filter "FullyQualifiedName~FindInNote_TypingAnnouncesTheCountAndEnterMovesTheCaret"
python -I scripts/generate-parity-matrix.py; python -I scripts/generate-parity-matrix.py --check
python -I scripts/a11y_trigger_ledger.py --write
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
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $files
```

CI: the Windows lane and the FlaUI shell gate. AT: NVDA full pass and
Narrator — Ctrl+F in a note: "Find in note, edit"; typing "the": "3 of
12"; Enter: "4 of 12", the editor selection read; Enter at the last
hit: "1 of 12, wrapped to the top"; Shift+Enter; Tab to "Match case,
check box, not checked"; Replace All: "Replaced 12 matches."; Escape:
the editor's filename line and the caret where it was; Ctrl+F on the
welcome/no tab: the search overlay's own line; Ctrl+F on a Base: "Quick
filter, edit". Recorded in the PR body; draft until then.

## Acceptance

- [ ] WD12 recorded in the PR body (disposition 1; replace in, regex out; Ctrl+G rows; find-in-selection deferred).
- [ ] Every fact above fails first, then passes; mutations run locally; the FlaUI journey green on the hosted lane.
- [ ] `GetChildrenCore` unchanged and its census green; the bar is a sibling.
- [ ] `chords.json`, `parity_matrix.md`, `w_c_matrix.md`, `w_c_automation_ids.json`, the designations and the ledger regenerated; `--check` scripts green.
- [ ] NVDA and Narrator passes recorded.
- [ ] PR body: `Closes #1112`; "not delivered: regex (parity), find-in-selection (unverified on mac), reading-mode find (mac defers it)".

## Brief

Read `SlateTextEditor.cs:215-245` and `:525-545`,
`WorkspaceTemplates.xaml:331-345`, `:480-500`, `:585-600`,
`Bases/BaseSurfaceView.cs:50-200`, `Grids/AccessibleDataGrid.cs:200-225`,
`Commands/ChordTable.cs:617-690`, `:794-796`, `:915-921`, `:1850-1890`,
`Commands/SlateCommandRegistrar.cs:40-70` and `:413-460`,
`VaultLifecycleViewModel.cs:270-290`, `ChordTableTests.cs:283-360` and
`:1105-1180`, `AutomationLandmark.cs`, `Censuses/WcMatrixEvidenceCensus.cs:36-75`,
`TaskStatusPhraseTests.cs`, `ShellAccessibilityTests.Palette.cs:43`,
`ShellAccessibilityTests.cs:69` and `:471` (`AssertAxeClean`), and mac's
`NoteEditorView.swift:240-250`, `:1426-1432`, `AppState.swift:9490-9535`,
`SlateCommands.swift:1895-1906`. Decision WD12 is pending (recommended:
disposition 1 — one `SharedCommandChords` entry for
`slate.bases.quickFilter | slate.editor.findInNote`; replace in; regex
out; Ctrl+G / Shift+Ctrl+G as `slate.editor.findNext` /
`slate.editor.findPrevious`, Windows-only rows; find-in-selection
deferred and recorded in the parity matrix as "unverified on mac; not
shipped"; under disposition 2 `slate.bases.quickFilter` becomes
`win: null` and the Bases menu loses its gesture text; under "regex yes"
a `SearchMode` toggle and facts are added as a recorded divergence).
Build: `FindInNoteViewModel` per Markdown tab (Query, MatchCase,
WholeWord, ReplaceText, IsOpen, CurrentIndex, TotalCount, Error,
FocusRequested; Next, Previous, Replace, ReplaceAll, Close) over
`SearchStrategyFactory.Create(pattern, ignoreCase, matchWholeWords, SearchMode.Normal)`
with a 150 ms debounce, manual wrap, result sets stamped with
`TextDocument.Version` and an options revision so every action
recomputes synchronously when stale and `Replace` re-verifies its
segment with `FindNext` before writing, `ReplaceAll` in one
`BeginUpdate`/`EndUpdate`, caret and selection kept as `TextAnchor`s and
restored clamped on Close; `FindMatchBackgroundRenderer : IBackgroundRenderer`
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
and to the registrar as `host => host.FindInNoteCommand`, routing a
Markdown tab in editor mode to `Workspace.OpenFindInNote()` and
everything else — reading mode, non-note tab, no tab, no workspace —
to `ToggleSearchCommand`; chord rows `Reg(Ids.EditorFindInNote, "Find…", CommandSection.Editor, "Find and highlight text within the current note.", "⌘F", "Ctrl+F", ChordScope.Global)`
plus the two Ctrl+G rows; an Editor-menu item "_Find…" (Windows has no
Edit menu) with `InputGestureText="{cmd:ChordText slate.editor.findInNote}"`;
`Panels/FindInNotePhrase` ("{N} of {M}", "No matches", "Wrapped to the
top", "Wrapped to the end", "Replaced {M} matches.", "Match changed;
nothing replaced.") spoken as HostComposed Medium after the debounce;
the rationale comment at `SlateTextEditor.cs:538`. Facts, failing
first — `FindInNoteViewModelTests`: `TypingFindsAllMatchesAndReportsOneOfM`,
`NextWrapsToTheTopAndSaysSo`, `PreviousWrapsToTheEndAndSaysSo`,
`MatchCaseAndWholeWordNarrowTheMatches`, `ReplaceReplacesTheCurrentHitAndSteps`,
`ReplaceAllIsOneUndoStep`, `ADocumentEditWhileOpenRecountsOnTheDebounce`,
`AnEmptyQueryClearsHighlightsAndCountsNothing`,
`CloseRestoresTheCaretAndSelectionTheOpenCaptured`;
`FindInNotePhraseTests` pinning every string verbatim;
`FindInNoteBarTests` (hosted shell): `CtrlFInANoteFocusesTheQueryAndSelectsAll`,
`CtrlFOnABaseTabReachesTheQuickFilterNotTheBar`,
`CtrlFInReadingModeOrWithNoTabOpensVaultSearch`,
`CtrlFOnTheWelcomeViewWithNoWorkspaceReachesVaultSearch`,
`EnterAndCtrlGStepNextAndShiftStepsPrevious`,
`EscapeClosesClearsHighlightsAndReturnsFocusToTheEditor`,
`TheBarIsASiblingOfTheEditorPeerNotAChild`, `TypingAnnouncesOnlyTheFinalCount`,
`AnEditBeforeTheDebounceNeverReplacesStaleText`,
`AnOptionChangeBeforeReplaceRecountsFirst`,
`CloseAfterAShortenedDocumentRestoresAClampedCaret`;
`ThemeTokenContrastTests` for the match brush; `ChordTableTests`
(`CommandRows_HaveNoActiveChordCollision` with the pair,
`EveryScopeIsEitherScrapedFromProductionOrDispositioned`,
`ChordsJson_IsExactlyTheTablesProjection` regenerated);
`WcMatrixEvidenceCensus` with the eleven ids registered; FlaUI
`ShellAccessibilityTests.FindInNote_TypingAnnouncesTheCountAndEnterMovesTheCaret`
with `AssertAxeClean(process, "find-in-note")` after text is typed.
Mutations to run locally: drop the fall-through arm; resolve the command
through `Workspace?.`; skip the `FindNext` re-verification; act on the
stamped set after an option change; restore the raw offset on Close;
remove the `SharedCommandChords` pair. Registers: `apps/slate-windows/chords.json`
(regenerated; `deliveryEvidence.commands` gains the three ids),
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
lane and the FlaUI gate are green, `dotnet format --include` over the
sixteen files is clean, and the NVDA and Narrator passes (Ctrl+F: "Find
in note, edit"; typing: "3 of 12"; Enter: "4 of 12"; Enter at the last
hit: "1 of 12, wrapped to the top"; Match case check box; Replace All:
"Replaced 12 matches."; Escape returns to the editor; Ctrl+F on the
welcome view opens the search overlay; Ctrl+F on a Base: "Quick filter,
edit") are in the PR body — draft until then. Not delivered: regex
(parity), find-in-selection (unverified on mac), reading-mode find (mac
defers it). PR body: `Closes #1112`. Commit: `feat(windows): ctrl+f
finds in the open note with a landmark find bar, replace and a spoken
count`.
