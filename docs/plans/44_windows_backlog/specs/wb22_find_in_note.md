# WB-22 — Ctrl+F finds in the open note, in the editor and in the reading view: a landmark find bar with next, previous, wrap, case, whole word, editor-only replace and a spoken count, and Ctrl+G steps from the text

Issues: Closes #1112 (PR 2; PR 1 says `Refs #1112`). Size L, delivered as two PRs (PR 1: the editor, the window's Ctrl+G route and the bar's keys; PR 2: the reading view). Lanes: windows + FlaUI. AT: a step-0 key spike before any code (NVDA in focus and browse mode, Narrator with scan mode off and on, JAWS if installed with its virtual cursor on and off), then NVDA and Narrator passes in both modes. Wave: W8. Depends on: WB-21b (the `MainWindow.xaml` Editor menu, §6's order), WB-17 and WB-21 (`ChordTable.cs`, `chords.json`); PR 2 also on WB-11 and WB-20 (`Reading/ReadingSurface.cs` — WB-11's cached structural peers and `InvalidateStructuralPeers()`, WB-20's `CompleteDiagrams`; §6's reading-surface order). Parallel with: WB-23, WB-24. Decision: WD12 (in part answered 2026-10-21: editor and reading view; Ctrl+G from the text; results-list fallback), the rest pending (recommended: disposition 1 via `SharedCommandChords` — one entry for the whole Ctrl+F command group, the sorted three-command key `slate.bases.quickFilter | slate.canvas.filterCards | slate.editor.findInNote`, replacing today's `slate.bases.quickFilter | slate.canvas.filterCards` entry — replace in (editor mode only), regex out, find-in-selection deferred; under disposition 2 the `slate.bases.quickFilter` row becomes `win: null`, the Bases menu item loses its gesture text, and the then-stale `slate.bases.quickFilter | slate.canvas.filterCards` entry is replaced by `slate.canvas.filterCards | slate.editor.findInNote`; under "regex yes" a `SearchMode` toggle and its facts are added as a recorded Windows-only divergence; under "replace no" the three replace controls, `Replace`, `ReplaceAll` and their facts leave the chunk and the bar is the same in both modes).

**Owner's call of 2026-10-26** (plan §3.1, "Review calls" and WD12) outranks this spec where they differ: the rest of WD12 is answered as recommended; reading-view find matches a math block's source and marks the whole math element, never the hidden speech run #1633 puts in the reading text; the parity note cites the mac follow-up #1654.

## Goal

Mac has had find-in-note since #874 (AppKit's stock find bar); Windows
has nothing — Ctrl+F in a note is dead. After this chunk a Windows reader
presses Ctrl+F in a note, in the editor or in the reading view, and lands
in a named "Find in note" field beside the note; typing highlights the
matches on the surface that shows the note and the bar speaks "N of M";
Enter / Shift+Enter in the query, and Ctrl+G / Ctrl+Shift+G from the bar,
the editor text or the reading surface, step with wrap-around — a step
from the text moves that surface's caret and selection to the match,
brings it into view, speaks the count once and leaves focus where it is;
case and whole-word toggles are visible controls, and in the editor so
are Replace and Replace All; Escape in the bar closes it, clears the
highlights and returns the caret where it was (Escape in the text never
closes it); Ctrl+F with no note surface — no workspace (welcome view), no
tab, or a tab that is not a note and whose own surface claims no Ctrl+F —
falls through to the vault search overlay, as mac's never-inert rule
does. Reading-view highlights are drawn over the text, never in it: the
reading document's text, structure and UIA tree are untouched. Parity
rows `slate.editor.findInNote` and the §W-C row leave "pending";
reading-view find goes beyond mac and is recorded so, with the mac
follow-up WB-0 files.

## Current state on main

Verified against the worktree. Two of the issue's architectural
arguments drifted and the plan's §2.2 records it; a third correction is
new here: **Windows has no Edit menu** — its top-level menus are File,
Workspace, Base, Editor, Canvas, Graph (`MainWindow.xaml:179`, `:282`,
`:365`, `:435`, `:575`, `:677`), so the "Edit menu item" lands in the
Editor menu.

- No find bar: zero hits for `SearchPanel`, `ISearchStrategy`,
  `IBackgroundRenderer` under `apps/slate-windows/src`. AvalonEdit
  `6.3.1.120` (`SlateWindows.csproj:30`); its `ISearchStrategy.FindAll` /
  `FindNext` take any `ITextSource`, and `StringTextSource` is one (the
  package's `ICSharpCode.AvalonEdit.xml`).
- `SlateTextEditor.cs:538-539` `GetChildrenCore()` returns
  `(GetPattern(PatternInterface.Text) as EditorSemanticTextProvider)?.Links.RootChildren()`
  — not `null` as the issue says; link peers already live under the
  Document peer. Sibling placement is still the safer shape (the
  Document's atomic-text pin, `w_c_matrix.md:17`). `OnPreviewKeyDown`
  (`:212-247`) handles Ctrl+E (`:221`), Ctrl+Enter (`:228-237`) and
  Escape only while a popover is open (`:239-246`); `ChordTableTests.cs:1161`
  `EditorChords()` scrapes that method for `ChordScope.Editor` rows.
  `FocusInputOwner()` (`:96`) focuses the text area.
- `WorkspaceTemplates.xaml:331-343` `WorkspaceTabContentTemplate`
  (`DataType` `WorkspaceTabViewModel`), a three-row Grid (Auto, *, Auto);
  the editor `MarkdownEditor` (`:414-452`) and the `ReadingSurface`
  (`:456-462`) share Row 1, one of them collapsed by
  `IsEditorVisible` / `IsReadingVisible` (`WorkspaceViewModel.cs:252-253`,
  beside `IsMarkdown :248`, `IsReadingMode :251`, `Reading :256`; both
  raised on a mode change at `:311-313` and `:600-602`). Row 2 is taken by
  the status `TextBlock` (`:591-595`) — the find bar needs a new row
  (Row 2, status moving to Row 3), not the "empty slot" the issue
  describes. The editor popover overlay is `Grid.RowSpan="3"` (`:488`)
  and must span the new row too.
- Ctrl+F today: `ChordTable.cs:918-920` `Reg(Ids.BasesQuickFilter, "Bases: Quick Filter", …, win: "Ctrl+F", scope: ChordScope.Grid)`
  and `:1884-1887` the surface row `windows.grid.filter` (Ctrl+F,
  `ChordScope.Grid`, `AccessibleDataGrid.FilterCommand`'s gesture);
  `Grids/AccessibleDataGrid.cs:212-218` yields when no subscriber
  (`e.ContinueRouting = FilterRequested is null`). A second COMMAND row
  already ships Ctrl+F: `ChordTable.cs:1361-1364`
  `Reg(Ids.CanvasFilterCards, "Canvas: Filter Cards…", …, "⌘F", "Ctrl+F", ChordScope.Canvas)`,
  delivered by the canvas surface's tunnelling `OnPreviewKeyDown` →
  `CanvasNavigator.HandleKey` → `FilterCardsFromKey`
  (`Canvas/CanvasNavigator.cs:237`, `:1855-1874`; it stands aside only
  when the canvas TABLE's grid owns the keys, and that grid's
  `FilterCommand` is subscribed to the same canvas filter). No
  window-level Ctrl+F (`MainWindow.xaml:34-68` `Window.InputBindings` has
  none); no `Ctrl+G` or `Ctrl+Shift+G` row anywhere in the table. The vault search chord
  is `Ctrl+Shift+F` (`:794-796`, `slate.view.toggleSearch`), resolved to
  `host.ToggleSearchCommand` (`SlateCommandRegistrar.cs:456`,
  `VaultLifecycleViewModel.cs:280`). No surface binds Ctrl+F in the
  reading view or on a graph tab, so there it reaches the window.
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
- Chord scopes: every row carries ONE `ChordScope` (`ChordTable.cs:22-98`;
  `Global` is "`Window.InputBindings` or the shell's window-level
  `PreviewKeyDown`", `:27-29`), and `Reg` defaults a chorded row to
  `Global` (`:617-639`). `TableCoversEveryDeclarativelyBoundChord_AndNothingStale`
  (`ChordTableTests.cs:383-468`) requires every `KeyBinding` chord in
  `MainWindow.xaml` and `WorkspaceTemplates.xaml` to have a row
  (`:390-398`; `KeyBindingChords`, `:704-723`, reads the `Key` and
  `Modifiers` attributes only, so the bound command's form is free),
  compares each scope's rows both ways with that scope's production
  scrape (`ScopedProductionChords()`, `:778-796`), and requires every
  `Global` row to be delivered by a `MainWindow.xaml` `KeyBinding` unless
  it is on the imperative allow-list (`:431-446`, `ImperativeGlobalRows`
  `:617`). `EveryScopeIsEitherScrapedFromProductionOrDispositioned`
  (`:1104-1123`) requires a scrape for every scope but `None` and
  `Global` (`:1093-1099`). `ChordScope.Editor` is scraped from
  `SlateTextEditor.OnPreviewKeyDown` (`EditorChords()`, `:1161-1201`),
  which reads only bare keys and Ctrl with no other modifier; its rows
  are `slate.editor.previewEmbed` (Ctrl+E), `slate.editor.activateAtCaret`
  (Ctrl+Enter) and the surface row `windows.editor.closePopover`
  (Escape, `ChordTable.cs:1855-1859`). The surface-row loop
  (`ChordTableTests.cs:371-379`) refuses two surface rows on one chord in
  one scope, so a find-bar Escape row in `ChordScope.Editor` fails beside
  `closePopover`. No row ships Shift+Enter; Escape and Enter surface rows
  exist in seven and six other scopes (`chords.json`). The canonical
  modifier order is Ctrl, Alt, Shift (`Canonical`, `:1542-1562`), so
  Shift+Ctrl+G is the row string `Ctrl+Shift+G`. The precedent for a
  field's own scope is `ChordScope.SidebarFilter` (W7-7 PR 2,
  `f035ab26`): one enum value (`ChordTable.cs:94-97`), its row
  (`:1724-1731`), its scrape (`ChordTableTests.cs:795`) and its fact
  (`SidebarFilterClearIsASurfaceRowInItsOwnScope`, `:870`); the
  map-shaped scrape the bar follows is `GraphChords()` (`:887-921`:
  `Bind`'s three-argument `AddChord(Key.X, ModifierKeys.Y, …)` calls,
  read as a duplicate-free list).
- **Nothing consumes Ctrl+G or Ctrl+Shift+G before the window.** WPF
  raises KeyDown on the focused element and bubbles it; at each element
  on the route `UIElement.OnKeyDownThunk` runs `CommandManager.TranslateInput`
  — the element's own `InputBindings`, then class input bindings, then
  command bindings whose command's gestures match — before `OnKeyDown`
  (dotnet/wpf `main` fetched 2026-10-21: `Generated/UIElement.cs:1934-1947`,
  `CommandManager.cs:269-381`), and `KeyGesture.Matches` requires the exact
  modifier state (`KeyGesture.cs:174-182`), so Ctrl+Alt+G never matches
  Ctrl+G. On the route from either note surface:
  - `RichTextBox` and `TextBox` share WPF's text editor, which registers
    85 class gestures (the `Key…` constants in `TextEditor*.cs`), none on
    G; no command gesture in PresentationCore's or PresentationFramework's
    `Strings.resx` is on G; `TextEditorTyping.OnPreviewKeyDown` acts only
    on `Key.ImeProcessed` and `OnKeyDown` only on Shift and Ctrl keys (flow
    direction) (`TextEditorTyping.cs:233-345`); `TextBoxBase`'s key
    overrides only delegate to it (`TextBoxBase.cs:1066-1090`).
  - AvalonEdit (tag `v6.3.1`, the package's source): the text area's
    default handlers bind arrows, Home/End, PageUp/PageDown, Delete,
    Backspace, Enter, Tab, Undo/Redo and Copy/Cut/Paste
    (`CaretNavigationCommandHandler.cs:67-116`, `EditingCommandHandler.cs:55-90`,
    `TextAreaDefaultInputHandlers.cs:50-58`), and `AvalonEditCommands`
    carries three gestures — Insert, Ctrl+D, Ctrl+I (`AvalonEditCommands.cs:35`,
    `:45`, `:108`); the only stock stacked handler that binds find keys is
    `SearchPanel`'s, which nothing here installs; `TextArea.OnTextInput`
    ignores an empty `Text` (`TextArea.cs:863-869`), so a Ctrl+G nothing
    handles types nothing.
  - Slate: `SlateTextEditor.OnPreviewKeyDown` above; `ReadingNavigator.Bind`
    (`Reading/ReadingNavigator.cs:61-108`) maps Ctrl+Alt chords only
    (`AddChord` sets Control|Alt, `:110-115`; Ctrl+Alt+G and
    Ctrl+Alt+Shift+G are the diagram alias, `:94-95`), looks them up by
    the exact modifier state (`:180`), and its other arms are Enter and
    Space at the caret (`:150-167`); the only `Key.G` uses under
    `apps/slate-windows/src` are those and the canvas's Ctrl+Alt+G
    (`Canvas/CanvasNavigator.cs:253`); `Window_PreviewKeyDown`
    (`MainWindow.xaml.cs:858`) has no G arm, and the content `DockPanel`'s
    bubbling `TrashFallback_KeyDown` acts only on Escape
    (`MainWindow.Trash.cs:17-29`).
- **A `KeyBinding` whose command cannot execute still marks the key
  handled**, unless the command is routed and its CanExecute asks to
  continue the route: once a binding matched, `TranslateInput` sets
  `inputEventArgs.Handled = !continueRouting` whether or not the command
  ran (`CommandManager.cs:383-425`); for a plain `ICommand`
  `continueRouting` stays false (`:413-418`), for a `RoutedCommand` it is
  the CanExecute event's `ContinueRouting` (`RoutedCommand.cs:315-338`).
  So the shell's gated window bindings — Ctrl+J (`MainWindow.xaml:63-65`,
  enabled only while a citation is expanded) and Escape →
  `ClosePopoverCommand` (`:67`) — swallow the key even when they do
  nothing (harmless at the route's root, but not a pass-through; the repo
  already records it: "the Window's disabled Escape KeyBinding still
  consumes input", `MainWindow.Trash.cs:19-21`, which is why the trash
  fallback sits on the `DockPanel` below the window), and the
  pass-through precedent is `AccessibleDataGrid.FilterCommand`'s
  `ContinueRouting` (`Grids/AccessibleDataGrid.cs:212-218`). Window
  `CommandBinding`s are added in code beside `ApplicationCommands.Close`'s
  (`MainWindow.xaml.cs:86`). The focus walk that crosses content elements
  (a `Hyperlink` in the reading document, where `VisualTreeHelper.GetParent`
  throws) is `MainWindow.FindAncestorDataContext` (`MainWindow.xaml.cs:2268-2299`).
- **The reading surface** (`Reading/ReadingSurface.cs`; PR 2's base adds
  WB-11 and WB-20): `ReadingSurface : RichTextBox` (`:30`), read-only
  with a visible caret (`:34-35`), no undo (`:36-42`), document-enabled
  (`:44`), AutomationId `ReadingSurface` (`:63`). It keeps ONE
  `FlowDocument` for its whole life — replacing `Document` breaks the UIA
  binding (`:67-79`) — so content arrives by merging: `ApplyBuiltDocument`
  clears the blocks, moves the built ones in, re-collects landmarks,
  invalidates the peer's children and restores the caret by symbol offset
  (`:401-459`); `Model_BlocksAppended` appends a streamed chunk
  (`:357-367`); `ClearForModelSwitch` installs the "Loading reading view…"
  placeholder (`ReadingLoadingNotice`, `:306-334`); `ShowsAppliedProjection`
  (`:531`, private) is "the bound model's projection has been applied".
  `RequestFocusLanding` (`:496-523`) is the shell's way in (it holds the
  landing while the content is not ready). One surface serves every tab
  of its tab control, its `DataContext` naming the tab it shows
  (`MainWindow.xaml.cs:2261-2266`). `ReadingSurfacePeer` (`:949-1020`)
  returns `RichTextBoxAutomationPeer`'s children — read from the text
  container, never from visual children (`RichTextBoxAutomationPeer.cs:69-73`)
  — plus structural heading, code-block and list peers; WB-11 caches that
  child list per structural generation and drops it only through
  `InvalidateStructuralPeers()` (wb11 Design (4) and (6)); WB-20 completes
  a pending diagram in place through `CompleteDiagrams`, editing `Run`s
  inside the diagram's paragraph, keeping the placeholder element and its
  peer, and keeping the source, failure header and reason as in-range
  `Run`s (wb20 Design, "Where the text lives" and "Completion in place").
- **Text outside the range.** WPF's text range blanks embedded objects
  (`ReadingDocumentBuilder.cs:583`'s comment, confirmed by WB-20's
  probe): what a `ReadingDiagramElement`, a `ReadingMathElement`, a task
  `CheckBox` or an embed card's button displays is absent from the
  surface's `TextRange` and its TextPattern; link text, WB-11's list-marker
  runs, a diagram's source and failure text and the math coverage-gap
  source are `Run`s, in range.
- **What WPF gives a non-mutating highlight.** WPF's own caret is an
  `Adorner` over the text view's render scope (`CaretElement.cs:24`,
  `:43`), placed in `AdornerLayer.GetAdornerLayer(renderScope)`
  (`:855`); that call returns a `ScrollContentPresenter`'s own layer
  before any `AdornerDecorator`'s (`AdornerLayer.cs:307-324`), and the
  presenter clips its children (`ScrollContentPresenter.cs:38`, `:192`,
  `:501-504`). `UIElement.OnCreateAutomationPeer` returns null
  (`UIElement.cs:3957-3964`) and neither `FrameworkElement` nor `Adorner`
  overrides it, so an adorner has no peer. `TextPointer.GetCharacterRect`
  answers in the `RichTextBox`'s coordinates, transformed from the render
  scope (`TextPointer.cs:1242-1256`, `TextPointerBase.cs:859-899`).
  `ScrollViewer` scrolls a descendant's `BringIntoView(Rect)` into view
  (`ScrollViewer.cs:2448`, `:2916`). A `RichTextBox` paints an unfocused
  selection only with `IsInactiveSelectionHighlightEnabled`
  (`TextBoxBase.cs:879-882`), which the reading surface does not set.
  `ItemAutomationPeer` computes PositionInSet and SizeOfSet from its
  `ItemsControl` (`ItemAutomationPeer.cs:263-309`).
- Hosted key presses: `CommandPaletteTests.ShippedShellHost`
  (`CommandPaletteTests.ShellKeys.cs:512`, private) raises a real
  Preview/KeyDown pair on a target with exact modifiers and returns
  whether it was handled (`Press`, `:664-670`; `ThreadModifiers`, `:837`).
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
  and executable evidence (`:62-75`). A collapsed element is absent from
  the control view (the repo's measured precedent,
  `ShellAccessibilityTests.cs:6139-6143`, `:8929-8931`).
- Mac: `NoteEditorView.swift:245-246` `usesFindBar = true`,
  `isIncrementalSearchingEnabled = true`; `:1426-1432` `showFindBar()`
  posts `NSTextFinder.Action.showFindInterface`; `AppState.swift:9493-9501`
  `requestFindInFocusedSurface()` tries the Bases filter, then Canvas,
  then `showFindInNote()`, which (`:9505-9535`) falls back through
  `requestSearchOverlay()` when no `NoteEditorView` is mounted — mac has
  no find in its reading view; `SlateCommands.swift:436` id, `:1895-1906`
  label "Find…", section `.editor`, hotkey ⌘F, hint "Find and highlight
  text within the current note."; `SlateMacApp.swift:1043-1046` the one
  Edit-menu Button; no ⌘G item (`:810` binds ⌃⌘G to something else).
- Registers: `parity_matrix.md:121` the row pending;
  `generate-parity-matrix.py:94` routes the id to #1112.

## Reference

- WD12 as recommended (still pending for its other parts): disposition 1
  via one `SharedCommandChords` entry; replace yes (mac has it), regex no
  (mac has none; parity); find-in-selection deferred, recorded in the
  parity matrix as "unverified on mac; not shipped", so no Mac probe gates
  the chunk. WD12's text names two rows; `slate.canvas.filterCards` is the
  third command row on Ctrl+F, so the disposition's one entry covers all
  three.
- WD12 in part, the owner's answer of 2026-10-21 (plan §3.1): find in note
  works in the editor and in the reading view; Ctrl+F opens the bar in
  either mode and highlights the matches on the visible surface; Ctrl+G
  and Ctrl+Shift+G step from the bar or from the editor or reading surface
  that shows the highlights, leaving focus where the reader is, each step
  moving the caret or selection to the match and speaking the count; the
  keyboard approach is preferred, and if a spike shows the keys cannot
  reach Slate from those surfaces under NVDA, JAWS or Narrator, the
  accepted fallback is an accessible results list (context per match,
  arrows between them, Enter to land); reading-view find goes beyond mac
  and WB-0 files the mac follow-up.
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

**Delivery: a spike, then two PRs.** Step 0 (below) runs before any code
and its record goes in PR 1's body. PR 1 (`Refs #1112`): the view model,
the editor surface and its renderer, the bar in editor mode, Ctrl+F's
routing (a reading-mode note still falls through to vault search, as on
round 11's design, until PR 2, and a switch to reading mode with the bar
open closes the bar without moving focus — PR 2 replaces that with the
mode switch below), the window's Ctrl+G route serving the bar
and the editor, the bar's own keys, the chord rows, menu and registrar,
speech, and — if step 0 calls for it — the results list for the editor.
PR 2 (`Closes #1112`): the reading surface's additions, the reading index,
surface and adorner, the reading-mode bar, Ctrl+F opening the bar in
reading mode, Ctrl+G from the reading surface, mode switches, the reading
facts and journey, the results list's reading rows if it shipped, the AT
pass in both modes and the parity note. PR 2 starts after WB-11 and WB-20
are on `main` and PR 1 has merged. The split keeps PR 1 inside the editor
and the chord table, where the Replace safety facts need focused review,
and makes PR 2 the only part that touches `Reading/ReadingSurface.cs`,
which three earlier chunks reshape.

**Step 0 — the key spike, before any code.** Does Ctrl+G, and
Ctrl+Shift+G, reach Slate from each note surface under each screen reader
and mode? The probe is local and never committed: one line at the top of
`MainWindow.Window_PreviewKeyDown` (`MainWindow.xaml.cs:858` — the
window's tunnelling handler, which every key that reaches the app passes
first) writing, for G with Control held,
`HostLog.WriteUiAutomationDiagnostic(HostDiagnosticEvent.ReadingChordSeen, …)`
(the reading navigator's existing tap, `ReadingNavigator.cs:176-178`,
written under `SLATE_UIA_DIAGNOSTICS=1`) with the key, the modifiers and
`Keyboard.FocusedElement`'s type. The app is `main`'s build plus the
probe, launched through the isolating launcher (§4 rule 4); keys are real
keystrokes (`SendInput`) with the screen reader running. The record, a
table in PR 1's body, has a row per screen reader × surface × mode — NVDA:
the editor, the reading view in browse mode and in focus mode; Narrator:
both surfaces with scan mode off and on; JAWS if installed: both surfaces
with the virtual cursor on and off — and columns: Ctrl+G seen, Ctrl+Shift+G
seen, and how the reader leaves that mode on that surface (NVDA+Space,
Narrator's scan-mode toggle, JAWS's virtual-cursor toggle; an AT's
pass-key-through command is noted but is not "leaving"). The rule:
- every cell seen, or every unseen cell in a mode the reader can leave on
  that surface for one where the keys are seen → the keyboard design
  below ships alone, and the help names the leave-the-mode step for those
  cells;
- an unseen cell in a mode the reader cannot leave on that surface → the
  results list (below) ships too: **alongside** the text-surface keys
  when it is the only such cell, **instead of** them when there is more
  than one — then the help page and the query's HelpText name the list,
  not Ctrl+G, as the route from the text. The window route is built
  either way: it is the bar's only Ctrl+G, it serves every surface and
  mode the record shows delivering the keys (keyboard users without a
  screen reader among them), and where an AT consumes the key the key
  never reaches it. The PR body records which path shipped and why.

**View model.** New `FindInNoteViewModel` (per Markdown tab, owned by
`WorkspaceTabViewModel`, disposed with it): `Query`, `MatchCase`,
`WholeWord`, `ReplaceText`, `IsOpen`, `Mode` (new enum `FindInNoteMode {
Editor, Reading }`, following its tab's `IsReadingMode`, raised beside
`IsReadingVisible`), `CanReplace` (`Mode == FindInNoteMode.Editor`),
`CurrentIndex`, `TotalCount`, `Error` (a `SearchPatternException`
message — never a `ToolTip`), `FocusRequested` (the `BaseDocumentViewModel`
shape), commands `Next`, `Previous`, `Replace`, `ReplaceAll` (both
CanExecute only when `CanReplace` and a surface's `Document` is non-null),
`Close`, and `AttachSurface(IFindInNoteSurface? surface)` (new; the bar
calls it). The engine is
`ICSharpCode.AvalonEdit.Search.SearchStrategyFactory.Create(pattern, ignoreCase, matchWholeWords, SearchMode.Normal)`,
run with `FindAll` / `FindNext` over the attached surface's `ITextSource`,
so both modes share one engine, one option set and one whole-word rule;
results are recomputed on a 150 ms debounce after a query or option change
and after every surface text change while open; wrap-around is manual
(`FindNext` from the caret, else from 0; `Previous` mirrors). Revealing a
hit is `surface.Reveal(offset, length)`. `SearchMode.RegEx` is not offered
(WD12).

**No action on stale results.** Every result set is stamped with the
attached surface's `Stamp` (the editor's `TextDocument.Version`, an
`ITextSourceVersion` compared by reference; the reading surface's
`ContentRevision`, below) and a monotonically increasing options revision
(query, MatchCase, WholeWord). A recompute has one of two causes, and they
differ in what they may move: one caused by a query or option change
reveals the first hit at or after the caret (incremental search: it
selects that hit and makes it the current hit); one caused by a text
change recounts and re-highlights only — it never moves the caret or the
selection. What stays current after a text-change recount differs by mode:
in the editor, the tracked hit below stays current when it still
validates and is otherwise cleared (`CurrentIndex` 0); in the reading
view, where nothing is replaced, the current match is whichever match the
surface's selection still covers exactly, else none (`CurrentIndex` 0).

*The tracked hit (editor mode).* Whenever a hit becomes current (a query
or option recompute, `Next`, `Previous`, the step after a `Replace`), the
view model keeps it as `TrackedHit` (new, a private record in
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
Neither check alone is enough, and the facts below pin both. A mode switch
clears the tracked hit (its anchors belong to the editor's document).

*Navigation and Replace All recompute; Replace validates first.* `Next`,
`Previous` and `ReplaceAll` compare the stamps with the attached surface's
live stamp and the options revision; when either differs they flush the
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
never text the pattern no longer matches. Opening the bar takes the
surface's restore point (`CaptureRestorePoint()`, below) — in the editor
the caret and selection as `TextAnchor`s (`TextDocument.CreateAnchor`,
`AnchorMovementType.AfterInsertion`, `SurviveDeletion = true`), so Close
restores positions that followed every intervening edit, clamped to
`document.TextLength`; a mode switch takes it again on the new surface.

**The surfaces.** New `IFindInNoteSurface` (in new `FindInNoteSurface.cs`
with `FindInNoteMode`): `FindInNoteMode Mode { get; }`; `bool IsSearchable
{ get; }`; `ITextSource Text { get; }`; `object Stamp { get; }`; `(int
Offset, int Length) Selection { get; }` (in `Text`'s offsets);
`TextDocument? Document { get; }` (the editor's; null on the reading
surface, which is why Replace cannot execute there); `void Reveal(int
offset, int length)` (the match becomes the surface's selection and is
brought into view; keyboard focus untouched); `void Highlight(IReadOnlyList<(int
Offset, int Length)> matches, int current)`; `void ClearHighlights()`;
`object CaptureRestorePoint()`; `void Restore(object restorePoint)`; `void
ReturnFocus()`; `event Action? TextChanged`.
- `EditorFindSurface` (new, `EditorFindSurface.cs`), `EditorFindSurface(TextDocument
  document, SlateTextEditor? editor)` — headless when `editor` is null
  (the pure facts, which read its recorded selection and highlights):
  `Text` and `Document` are the document, `Stamp` its `Version`,
  `IsSearchable` true, `Reveal` is `editor.Select(offset, length)` then
  `editor.TextArea.Caret.BringCaretToView()`, `Highlight` fills the
  renderer's segments and registers it, `ClearHighlights` unregisters it,
  the restore point is the anchors above, `ReturnFocus` is
  `SlateTextEditor.FocusInputOwner()` (`SlateTextEditor.cs:96`), and
  `TextChanged` is `TextDocument.Changed`.
- `ReadingFindSurface` (new, `Reading/ReadingFindSurface.cs`),
  `ReadingFindSurface(ReadingSurface surface)`: `IsSearchable` is
  `surface.ShowsAppliedProjection` (never the "Loading reading view…"
  placeholder); `Text` is a `ReadingTextIndex` built for the surface's
  document and cached per `ContentRevision`, so a query change re-runs
  the strategy over the cached string and only a content change rebuilds
  it; `Stamp` is `ContentRevision`; `Selection` maps `surface.Selection`
  through `ReadingTextIndex.OffsetOf`; `Reveal` maps the match through
  `PointerAt`, calls `surface.Selection.Select(start, end)` and brings the
  match's first line into view with `DocumentRenderScope.BringIntoView(rect)`
  (the render scope sits inside the template's `ScrollViewer`, which scrolls
  it, `ScrollViewer.cs:2448`, `:2916`); `Highlight` hands the matches to a
  `FindMatchAdorner`; the restore point is `surface.Selection`'s live
  start and end pointers with the count of `Replaced` changes so far, and
  `Restore` re-selects them only when no `Replaced` change came since (a
  merge or a model switch moved every pointer; appends and in-place
  completions keep live pointers where they were, the rule WB-20's own
  anchors rely on), else leaves the caret where that change left it;
  `ReturnFocus` is `surface.RequestFocusLanding()` (`ReadingSurface.cs:496-523`,
  which holds the landing while the content is not ready); `TextChanged`
  is raised from `surface.ContentChanged`.
- `ReadingTextIndex` (new, `Reading/ReadingTextIndex.cs`): `static
  ReadingTextIndex Build(FlowDocument document)`; `ITextSource Text`
  (an AvalonEdit `StringTextSource` over the flattened text); `TextPointer
  PointerAt(int offset, LogicalDirection gravity)`; `int OffsetOf(TextPointer
  pointer)` (the flat offset at or before it); `int ParagraphOrdinalAt(int
  offset)` (1-based, for the results list). One forward walk from
  `ContentStart` with `GetNextContextPosition(LogicalDirection.Forward)`
  and `GetPointerContext`: a `Text` context appends `GetTextInRun(LogicalDirection.Forward)`
  and records the run's flat start and its pointer; the start or end of
  any element that is not an `Inline` (blocks, list items, table rows and
  cells) and a `LineBreak` append one "\n" (never two in a row, none at
  the start); an `EmbeddedElement` context appends U+FFFC; inline edges
  (`Run`, `Span`, `Bold`, `Hyperlink`, an `InlineUIContainer`'s own edges)
  append nothing, so a word split across formatting runs ("**fo**o") is
  one "foo". `PointerAt` binary-searches the run starts and returns
  `runStart.GetPositionAtOffset(offset - flatStart, gravity)` (within a
  text run one character is one symbol). So the search covers exactly the
  text in the reading surface's `TextRange` — what caret reading and the
  TextPattern reach — and never spans a block boundary or an embedded
  element; what an embedded element displays (a diagram element's
  description, a rendered math element, a task check box, an embed card's
  button) is neither searched nor counted, while a diagram's source and
  failure text, link text, list markers and the math coverage-gap source
  are. The walk is linear in the document, bounded by the projection's
  block cap (`MaximumRenderedBlocks`), runs on the dispatcher (text
  pointers are dispatcher-bound) and runs once per content change.
- `ReadingSurface` (PR 2, after WB-11 and WB-20) gains only: new enum
  `ReadingContentChange { Replaced, Appended, CompletedInPlace }`; new
  `internal event Action<ReadingContentChange>? ContentChanged` and `internal
  int ContentRevision { get; private set; }`, bumped and raised once at the
  end of each content path — `ApplyBuiltDocument` (`Replaced`),
  `ClearForModelSwitch` (`Replaced`), `Model_BlocksAppended` (`Appended`)
  and WB-20's `CompleteDiagrams` (`CompletedInPlace`, after its
  `EndChange()` and after any `InvalidateStructuralPeers()` it runs) — and
  never on the rebind fast path (`:265-288`, which changes no content);
  `ShowsAppliedProjection` (`:531`) becomes `internal`; new `internal
  FrameworkElement? DocumentRenderScope` — the text view's render scope,
  which `TextBoxBase` puts in the template's `PART_ContentHost`: that
  host's `Content` when it is a `ScrollViewer`, its `Child` when it is a
  `Decorator` (`TextBoxBase.cs:33`, `:1886-1920`; the reading surface's
  template, with `VerticalScrollBarVisibility="Auto"`, hosts a
  `ScrollViewer`, and `TheAdornerHasNoPeerAndLivesInTheSurfacesOwnLayer`
  fails if the layer found is not a `ScrollContentPresenter`'s). No peer, builder, merge, caret or
  landing logic changes, and nothing calls `InvalidateStructuralPeers()`
  that did not before.

**Rendering in the editor.** A ~50-line `FindMatchBackgroundRenderer :
IBackgroundRenderer` (`KnownLayer.Selection`, `BackgroundGeometryBuilder`
over a `TextSegmentCollection<TextSegment>`), registered on
`TextArea.TextView.BackgroundRenderers` while the bar is open on the
editor and removed on close, on detach and on tab dispose. The brush is a
theme token (`Slate.FindMatchBrush` over `Slate.FindMatchColor`, new,
both themes), drawn behind the text.

**Rendering in the reading view: drawn over the text, never in it.** New
`FindMatchAdorner : Adorner` (`Reading/FindMatchAdorner.cs`), constructed
over the surface's `DocumentRenderScope` and added to
`AdornerLayer.GetAdornerLayer(renderScope)` — the template
`ScrollContentPresenter`'s own layer, exactly where WPF puts its caret
(`CaretElement.cs:43`, `:855`; `AdornerLayer.cs:307-324`) — so the drawing
moves with the scrolled text and is clipped to the viewport by the
presenter (`ScrollContentPresenter.cs:501-504`). `IsHitTestVisible` and
`Focusable` are false and it does not override `OnCreateAutomationPeer`,
so it has no peer (`UIElement.cs:3957-3964`). `OnRender` draws only the
matches that intersect the viewport (the first and last visible
positions from `surface.GetPositionFromPoint(…, snapToText: true)`,
mapped through `OffsetOf` and binary-searched in the sorted matches): one
rectangle per line a match spans (lines split with
`TextPointer.GetLineStartPosition(1)`), from `GetCharacterRect` — which
answers in the `RichTextBox`'s coordinates (`TextPointerBase.cs:859-899`),
mapped into the render scope with `surface.TransformToDescendant(renderScope)`
— stroked as a 2 px ring in `Slate.FindMatchRingBrush` (over
`Slate.FindMatchRingColor`, new, both themes), the current match 3 px, so
the current match stays visible while focus is in the bar (the surface
paints no unfocused selection, `TextBoxBase.cs:879-882`). It re-renders
when its match list changes, on the surface's `ScrollViewer.ScrollChanged`
and on the render scope's `SizeChanged`. A ring, not a fill: an adorner
paints above the text, so a fill would cover the glyphs, while a ring
leaves the text's own contrast untouched and needs only its own pair
against the surface (the canvas selection-ring precedent in
`ThemeTokenContrastTests`).

Why not mark the matches in the document: `TextRange.ApplyPropertyValue`
(or wrapping matches in `Span`s) splits `Run`s and formatting elements at
the match edges, raises `TextChanged` and the TextPattern's text-changed
event that screen readers re-read, changes the element structure WB-11's
cached structural peers and names were built from (WB-11's only
invalidation entry, `InvalidateStructuralPeers()`, exists for block
changes, so every keystroke in the query would need a new invalidation
path), and edits the very `Run`s WB-20's in-place completion locates and
rewrites. The adorner and the selection change none of that: the
`FlowDocument`, its elements, its text and `ReadingSurfacePeer`'s children
(read from the text container, `RichTextBoxAutomationPeer.cs:69-73`) are
what they were before the bar opened. The current match is the
`RichTextBox` selection — a position, not content — so caret reading,
the TextPattern's selection and WB-11's selection-changed event carry it
to the screen reader.

**Re-running the search in the reading view.** The reading surface
re-runs the search when its projection publishes, streams or completes a
diagram in place: `ReadingFindSurface` handles `ContentChanged` — on
`Replaced` and `CompletedInPlace` it drops the drawn matches at once (a
merge moves every pointer to the deletion point, and a completion
rewrites the diagram paragraph's `Run`s), on `Appended` it keeps them
(appends only grow the tail) — and raises `TextChanged`, which schedules
the debounced recount (`Next` and `Previous` flush it when the stamp is
stale). The recount moves neither caret nor selection and is silent,
except that a search asked while the surface was not searchable (the
loading placeholder) is answered once, by the first recount over an
applied projection. While `IsSearchable` is false the count readout and a
step say "Waiting for the reading view." and nothing moves. A completion
WB-20 lands inside the current match's diagram source re-seats the
selection by its own rule (wb20 "Completion in place" step 3), so the
recount finds the selection covering no match and `CurrentIndex` is 0.

**Mode switches with the bar open.** The bar stays open and keeps its
query and options; the bar re-attaches (below): the old surface's
highlights are cleared (the editor's renderer unregistered, or the
adorner removed), the tracked hit is cleared, the restore point is taken
on the new surface, and the set is recounted over the new surface's text
— synchronously when it is searchable, else by the recount that follows
the first `ContentChanged` applying a projection (a reading surface
switched to for the first time shows the loading placeholder until its
projection publishes) — silently, with `CurrentIndex` the match the new
surface's selection covers exactly, else 0. (Only a query, an option
change or a step made while the surface was not searchable is "asked"
and answered when it becomes searchable; a mode switch asks nothing.) Neither surface's caret or
selection is moved by the switch (the reading surface's is wherever its
rebind left it). The reading text is the projection, not the Markdown
source, so the counts can differ between modes (markup is not in the
reading text).

**View.** `FindInNoteBar` (new `UserControl`, `DataContext` the tab's
`FindInNote`, collapsed unless `IsOpen`) at `Grid.Row="2"` of
`WorkspaceTabContentTemplate`, the status `TextBlock` moving to a new
Row 3 and the popover overlay to `RowSpan="4"`. Two new dependency
properties, `FindInNoteBar.Editor` and `FindInNoteBar.ReadingSurface`,
bound by `ElementName` to the template's `MarkdownEditor` and to the
reading surface (which gains `x:Name="NoteReadingSurface"`); the bar
calls `AttachSurface` whenever its `DataContext`, `IsOpen` or `Mode`
changes — `new EditorFindSurface(Editor.Document, Editor)` in editor
mode (the editor's `Document` is bound to the tab's `EditorDocument`,
`WorkspaceTemplates.xaml:418`), `new ReadingFindSurface(ReadingSurface)` in reading mode, null when
closed — and the view model detaches the previous surface (highlights
cleared, `TextChanged` unsubscribed); a tab switch, which rebinds the
shared template, detaches the outgoing tab's surface the same way. Rooted
at an `AutomationLandmarkGrid` named "Find in note"; children with
AutomationIds `FindInNoteQuery` (Edit, Name "Find in note"; HelpText
names Enter/Shift+Enter, Ctrl+G and Ctrl+Shift+G — from here or from the
note — and Escape), `FindInNoteCount` (Text, a focus stop like
`BaseCountReadout`), `FindInNotePrevious`, `FindInNoteNext`,
`FindInNoteMatchCase`, `FindInNoteWholeWord` (CheckBoxes, visible — the
divergence the issue records), `FindInNoteReplaceText`,
`FindInNoteReplace`, `FindInNoteReplaceAll`, `FindInNoteClose`,
`FindInNoteError` (Text, present only on a pattern error). Not modal: no
`ModalSurface` member, no `IsDialog`, no `IsFocusScope` (so
`SheetFenceCensus` is not involved). How UIA sees it:
- editor mode: the landmark's children are Query (Edit), Count (Text),
  Previous, Next (Buttons), Match case, Whole word (CheckBoxes), Replace
  text (Edit), Replace, Replace All, Close (Buttons); tab order Query →
  Count → Previous → Next → Match case → Whole word → Replace text →
  Replace → Replace All → Close, then back to the note;
- reading mode: `FindInNoteReplaceText`, `FindInNoteReplace` and
  `FindInNoteReplaceAll` are `Visibility.Collapsed` (bound to
  `CanReplace`), so they are absent from the control view and from the
  tab order; the landmark's children are Query, Count, Previous, Next,
  Match case, Whole word, Close; tab order Query → Count → Previous →
  Next → Match case → Whole word → Close, then back to the note;
- either mode: `FindInNoteError` only on a pattern error; the find
  highlights add no element anywhere in the tree.

**Routing Ctrl+F.** The global command is owned by the lifecycle host,
not the workspace, because the welcome view has no `Workspace` and Ctrl+F
must still reach vault search there: `VaultLifecycleViewModel.FindInNoteCommand`
(new; `VaultLifecycleViewModel` is the `ISlateCommandHost`,
`SlateCommandRegistrar.cs:63-70`, which already owns `ToggleSearchCommand`
at `VaultLifecycleViewModel.cs:280`). A window-level `KeyBinding` Ctrl+F
binds it (`ChordScope.Global`); the registrar maps
`[Ids.EditorFindInNote] = host => host.FindInNoteCommand`. Its Execute:
`Workspace?.ActiveGroup.ActiveTab` is a Markdown tab (`IsMarkdown`) in
either mode → `Workspace.OpenFindInNote()` (new; opens the bar, or, open
already, focuses the query and selects all — mac's `Reactivate`); a Base
or Canvas tab → their own filters handle Ctrl+F before it reaches the
window (a Base: the grid's `FilterCommand` gesture,
`Grids/AccessibleDataGrid.cs:106`, `:212-218`; a canvas: the surface's
tunnelling handler, `CanvasNavigator.cs:237` → `FilterCardsFromKey`,
`:1855-1874`, or on the canvas TABLE the grid's `FilterCommand`,
subscribed to the canvas filter), so the global arm never sees a key a
surface claimed. That holds only because the window's Ctrl+F is a
bubbling `KeyBinding` in `MainWindow`'s `Window.InputBindings`
(`MainWindow.xaml:34`) — never a case in `Window_PreviewKeyDown`, which
tunnels ahead of every surface (the `slate.file.cancelImport` Escape
entry, `ChordTableTests.cs:292-298`, relies on exactly that order for the
opposite purpose). Only with no note surface — no workspace at all
(welcome view), no tab, or a tab that is not a note whose surface claims
no Ctrl+F (a graph tab) — does it run `ToggleSearchCommand.Execute(null)`
(mac's fall-through; the overlay's own no-vault behaviour is unchanged).
PR 1 still sends a reading-mode note there; PR 2 opens the bar for it.

**Ctrl+G and Ctrl+Shift+G from the bar and from the text.** One route,
bubbling, at the window: two `KeyBinding`s in `MainWindow.xaml`'s
`Window.InputBindings`, `<KeyBinding Key="G" Modifiers="Control"
Command="{x:Static local:FindInNoteKeyRoute.FindNext}" />` and `<KeyBinding
Key="G" Modifiers="Control+Shift" Command="{x:Static local:FindInNoteKeyRoute.FindPrevious}"
/>`. New `internal static class FindInNoteKeyRoute` (`FindInNoteKeyRoute.cs`):
`internal static readonly RoutedUICommand FindNext` and `FindPrevious`
(no `InputGestures` — the table's rows and these two `KeyBinding`s are
the only place the chords are authored); `internal static void
Bind(UIElement window)` (called from `MainWindow`'s constructor beside
`MainWindow.xaml.cs:86`) adds a `CommandBinding` for each whose
CanExecute sets `e.CanExecute = SessionOwningFocus(Keyboard.FocusedElement
as DependencyObject) is { IsOpen: true }` and `e.ContinueRouting =
!e.CanExecute`, and whose Executed calls that session's `Next()` or
`Previous()`; `internal static FindInNoteViewModel? SessionOwningFocus(DependencyObject?
focused)` walks up from the focused element as `FindAncestorDataContext`
does (`MainWindow.xaml.cs:2268-2299`: a `FrameworkElement` by `Parent ??
TemplatedParent ?? VisualTreeHelper.GetParent`, a content element by
`Parent ?? LogicalTreeHelper.GetParent`) to the first `FindInNoteBar`
(→ its `DataContext as FindInNoteViewModel`), `SlateTextEditor` or
`Reading.ReadingSurface` (→ `(DataContext as WorkspaceTabViewModel)?.FindInNote`),
and answers null for anything else. Why this shape:
- *Nothing earlier takes the keys* (Current state): no WPF text-editor,
  AvalonEdit or Slate binding on the route is on G, and the reading
  navigator's Ctrl+Alt+G differs by modifiers, so a Ctrl+G pressed in the
  editor text, the reading surface, an element inside the reading
  document (a link, a task box) or any control in the bar bubbles to the
  window.
- *It passes through when it cannot act.* A plain `ICommand` binding
  would mark the key handled even when it cannot execute
  (`CommandManager.cs:425`); a routed command whose CanExecute sets
  `ContinueRouting` leaves it unhandled — `AccessibleDataGrid.FilterCommand`'s
  pattern — so with no open bar for the focused note the key leaves the
  window unhandled, types nothing (`TextArea.cs:863-869`; the reading
  surface is read-only) and changes nothing.
- *It acts on the note the reader is in.* "That tab's bar" is read from
  focus, not from the active tab: a step comes from the bar, the editor
  or the reading surface of the tab whose bar is open (two split panes
  each keep their own), and Ctrl+G from the Files tree, a tab header,
  the properties header or an editor popover passes through, as mac's
  ⌘G does where no text view is in the responder chain.
- *Focus stays where it is.* `Next()` and `Previous()` call
  `surface.Reveal`, which moves the surface's caret and selection to the
  match and brings it into view without focusing anything, and speak the
  "{N} of {M}" line once — the same call a step from the bar makes.
- *The chord rows stay one-scope and scraped.* The two rows are
  `ChordScope.Global`, delivered by `MainWindow.xaml` `KeyBinding`s, which
  `TableCoversEveryDeclarativelyBoundChord_AndNothingStale` checks both
  ways (`:390-398`, `:431-446`); no new scope or allow-list entry is
  needed, and no other command row ships Ctrl+G or Ctrl+Shift+G, so no
  `SharedCommandChords` entry either. This replaces round 11's design, in
  which Ctrl+G lived in the bar's own map and acted only with focus in
  the bar.

The palette reaches the same steps through the registrar:
`[Ids.EditorFindNext] = host => host.Workspace?.FindNextInNoteCommand` and
`[Ids.EditorFindPrevious] = host => host.Workspace?.FindPreviousInNoteCommand`
(new `RelayCommand`s on `WorkspaceViewModel`, CanExecute
`ActiveGroup.ActiveTab?.FindInNote is { IsOpen: true }`) — the palette
holds focus while it runs, so it acts on the active tab.

**The bar's own keys** (new `ChordScope.FindInNote`). `FindInNoteBar.Bind()`
(new, in `FindInNoteBar.xaml.cs`) builds a map of three-argument
`AddChord(Key.X, ModifierKeys.Y, Func<bool>)` calls — the
`CanvasNavigator` / `GraphNavigator` shape — and the bar's tunnelling
`OnPreviewKeyDown` override runs it, so the keys are live only while
keyboard focus is within an open bar (the bar is `Collapsed` while closed
and holds no focus) and an action that returns false leaves the key to
its target. The map:
- Enter → Next and Shift+Enter → Previous, acting only when the query
  field (`FindInNoteQuery`) holds the focus (and, when the results list
  ships, Enter on a focused row of it → `GoToResult`, below); elsewhere
  in the bar they return false, so a focused button's own Enter still
  clicks it (Enter on Replace All runs Replace All);
- Escape → Close, anywhere in the bar (clears the highlights, restores
  the restore point, and returns focus through the surface's
  `ReturnFocus()`). Escape in the note's text never reaches the bar (the
  bar is a sibling of the surfaces, not an ancestor), so it never closes
  it: in the editor it closes an open popover (`SlateTextEditor.cs:239-246`)
  or reaches the window's gated Escape binding, which does nothing.

`FindInNoteBar.xaml` declares no `KeyBinding` or `InputBindings` (no
scrape reads that file's bindings), and `SlateTextEditor.OnPreviewKeyDown`
gains no arm. The rows, all in `ChordScope.FindInNote`, are three
surface rows, `Chord("windows.findInNote.next", "Find in note: next match",
"Enter", ChordScope.FindInNote, …)`, `windows.findInNote.previous`
(`"Shift+Enter"`) and `windows.findInNote.close` (`"Escape"`), each reason
naming the command it runs and the route. `ChordScope.FindInNote` gets an
enum summary like `SidebarFilter`'s (the bar's own key handling; Ctrl+F,
Ctrl+G and Ctrl+Shift+G are `Global`), and `ScopedProductionChords()`
gains `[ChordScope.FindInNote] = FindInNoteChords()` (new), a scrape of
`CSharpSource.Load("FindInNoteBar.xaml.cs").Method("Bind")` shaped like
`GraphChords()` (a duplicate-free list of at least the three chords).
`ChordScope.Editor` cannot hold them: `windows.editor.closePopover`
already ships Escape there and the surface-row loop refuses a second
Escape row in one scope, and Enter and Shift+Enter there would be claims
the editor's scrape never finds (the editor types them). The new scope
shares chords only across scopes — Escape and Enter with other scopes'
surface rows (distinct scopes, as the surface-row loop requires) and
nothing with any command row — so no `SharedCommandChords` entry is
needed for them. Precedence is the window's first, as for every surface:
the window's own tunnelling handler (`MainWindow.xaml.cs:858` — an
import's Escape, the modal sheets) runs before the bar's, and the bar's
Escape, handled in the tunnelling phase, never reaches the window's
gated Escape binding or the editor's popover row.

**Chord table and menu.** `Reg(Ids.EditorFindInNote /* slate.editor.findInNote */, "Find…", CommandSection.Editor, "Find and highlight text within the current note.", "⌘F", "Ctrl+F", ChordScope.Global)`;
`Reg(Ids.EditorFindNext /* slate.editor.findNext, new */, "Find Next", CommandSection.Editor, "Move to the next match of the open find bar's query.", mac: null, win: "Ctrl+G")`
and `Reg(Ids.EditorFindPrevious /* slate.editor.findPrevious, new */, "Find Previous", CommandSection.Editor, "Move to the previous match of the open find bar's query.", mac: null, win: "Ctrl+Shift+G")`
(both `ChordScope.Global` by `Reg`'s default; Windows-only, the parity
matrix's Windows-only arm);
one `SharedCommandChords` entry for the whole Ctrl+F command group, which with the new row is three rows in three scopes (Grid, Canvas, Global) — under disposition 1 the existing `["slate.bases.quickFilter | slate.canvas.filterCards"]` entry is REPLACED (not kept beside it: no group's key is the pair any more, and the live-key check below fails a left-over) by `SharedCommandChords["slate.bases.quickFilter | slate.canvas.filterCards | slate.editor.findInNote"] = "Ctrl+F. Disjoint by DELIVERY: on a Base the grid's FilterCommand gesture handles it, and on a canvas the surface's tunnelling handler does — on the canvas TABLE the grid's FilterCommand, which is subscribed to the canvas filter, so the Grid and Canvas rows agree by construction and the navigator stands aside there — both before the key bubbles to the window; the window's KeyBinding (Global, bubbling, never in Window_PreviewKeyDown) acts only on a key no surface claimed: the find bar for a note in either mode, vault search otherwise (WD12 disposition 1)."`; under disposition 2 `slate.bases.quickFilter` ships no Windows chord, the group is the canvas and editor rows, and the then-stale `["slate.bases.quickFilter | slate.canvas.filterCards"]` entry (the staleness loop fails it: its Bases row no longer shares the chord) is replaced by `SharedCommandChords["slate.canvas.filterCards | slate.editor.findInNote"] = "Ctrl+F. Disjoint by DELIVERY: on a canvas the surface's tunnelling handler (or, on the canvas TABLE, the grid's FilterCommand subscribed to the canvas filter) handles it before it bubbles; the window's KeyBinding acts only on a key no surface claimed (WD12 disposition 2)."`;
Editor menu item "_Find…" with `InputGestureText="{cmd:ChordText slate.editor.findInNote}"`
and `AcceleratorKey` bound like `:437-441`. Registrar:
`[Ids.EditorFindInNote] = host => host.FindInNoteCommand` (the lifecycle
host's, never `Workspace?.`) and the two step entries above.
`chords.json` regenerated; `deliveryEvidence.commands` gains the three
ids → `"editorHost"` (or a new `findInNote` group naming the view model
and the FlaUI journeys).

**Speech.** The count is a host-composed §W-C golden family
`FindInNotePhrase` (new, `Panels/`-style static class): "{N} of {M}",
"No matches", "Wrapped to the top" / "Wrapped to the end" appended once
on a wrap, "Replaced {M} matches." after Replace All, "Match changed;
nothing replaced." when `Replace` refuses a tracked hit that no longer
validates, "Waiting for the reading view." while the reading surface
shows no applied projection, and the pattern error verbatim. Spoken
through `HostComposed` at Medium; typing is coalesced to the final count
after the debounce (the palette's `Palette_TypingAnnouncesOnlyTheFinalCountAndSelection`
posture); a step — from the bar, the editor text or the reading surface —
speaks its line once, at once; a text-change recount and a mode switch's
recount speak nothing. Designated in `a11y_trigger_designations.json` as
residue with the reason "no mac twin: AppKit's find bar announces
nothing"; the trigger ledger regenerated. Whatever a screen reader says
of the moved selection is its own reading, recorded in the AT pass.

**The results-list fallback** (built only when step 0 calls for it).
Placement: inside the find bar's landmark, a third row under the
controls, visible while the bar is open and the set holds at least one
match. A `ListBox` with AutomationId `FindInNoteResults` (the twelfth id)
and Name "Matches", bound to the view model's new
`ObservableCollection<FindInNoteResultRow> Results` (new `internal sealed
record FindInNoteResultRow(int Index, int Offset, int Length, string
Name)`), rebuilt on every recount; each `ListBoxItem`'s
`AutomationProperties.Name` is the row's Name: "line {N}: {context}" in
the editor (N the `TextDocument` line of the match's start) and
"paragraph {N}: {context}" in the reading view (N from
`ReadingTextIndex.ParagraphOrdinalAt` — the reading text reflows and has
no source lines, so a paragraph is the unit it has), the context being up
to 30 characters either side of the match within its line or paragraph,
whitespace runs collapsed, "…" where cut. At most eight rows tall with
its own scroll viewer, virtualized. UIA: a List named "Matches" whose
ListItems carry those names and native PositionInSet / SizeOfSet
(`ItemAutomationPeer.cs:263-309`). Keys: the arrows move between rows
(the `ListBox`'s own; selection follows focus) without moving the note's
caret or speaking anything of Slate's; Enter on a row calls new
`FindInNoteViewModel.GoToResult(FindInNoteResultRow row)`, which reveals
that match in the attached surface (its selection, brought into view),
makes it current and speaks "{N} of {M}" once, focus staying on the row.
Tab order: Query → Count → Matches → Previous → …. A `ListBoxItem` has no
Enter action of its own, so Enter on a row is delivered by the bar's own
Enter arm: `FindInNoteBar.Bind`'s Enter action runs `GoToResult` for the
focused row when a `FindInNoteResults` item holds the focus, `Next` when
the query does, and returns false anywhere else — one chord, one row
(`windows.findInNote.next`, whose reason then names both), so the list
adds no chord row and no `KeyBinding`.

Not changed: `SlateTextEditor.GetChildrenCore`, the Document peer, the
reading document's content and element structure, `ReadingSurfacePeer`
and WB-11's caches, WB-20's completion, the Bases and Canvas filters,
core, FFI, mac. Deliberately not shipped: regex, find-in-selection
(parity row "unverified on mac; not shipped"), and search inside the
reading view's embedded elements (outside the text range that caret
reading covers).

## Tests

Facts fail first. PR 1 ships the facts not marked PR 2; PR 2 adds its
own; the results-list facts ship only with the list.

`FindInNoteViewModelTests` (pure, over a `TextDocument` through a
headless `EditorFindSurface`) — PR 1:
- `TypingFindsAllMatchesAndReportsOneOfM` — "a b a" query "a": `TotalCount` 2, `CurrentIndex` 1 at the first hit after the caret. Mutation: drop the debounce flush → stale count.
- `NextWrapsToTheTopAndSaysSo` / `PreviousWrapsToTheEndAndSaysSo`.
- `MatchCaseAndWholeWordNarrowTheMatches` (Theory).
- `ReplaceReplacesTheCurrentHitAndSteps`; `ReplaceAllIsOneUndoStep` (Ctrl+Z restores every hit).
- `ADocumentEditWhileOpenRecountsOnTheDebounce` — an edit elsewhere recounts once the debounce runs; the recount leaves the caret and selection where the edit put them, and the still-valid tracked hit stays current with its index recomputed; an edit that breaks the hit clears it (`CurrentIndex` 0), still without moving the caret. Mutation: let a document-edit recompute reveal the first hit after the caret → the caret moves.
- `ReplaceWithNoCurrentHitRevealsTheNextAndWritesNothing` — "a b a", query "a", the tracked hit cleared by a document-edit recount (the first "a" edited to "x", the debounce flushed); Replace with ReplaceText "q": the document is still "x b a", the surviving "a" (offset 4) is selected and "1 of 1" is spoken; a second Replace writes "x b q". Mutation: with no tracked hit, recompute and replace the first hit → the first press writes.
- `AnEmptyQueryClearsHighlightsAndCountsNothing`.
- `CloseRestoresTheCaretAndSelectionTheOpenCaptured`.

`FindInNotePhraseTests` (the `TaskStatusPhraseTests` shape): every
string pinned verbatim, "Waiting for the reading view." included.

`FindInNoteBarTests` (hosted shell: `CommandPaletteTests.ShippedShellHost`
and `ThreadModifiers` become `internal` so this class reuses them; the
host gains a helper that opens a fixture note and an `IInputElement`
overload of `Press`; keyboard focus is set with `Keyboard.Focus` and
asserted before each press, so the route reads the element the press is
raised on) — PR 1. Round 11's `TheBarsKeysActOnlyWithFocusInTheOpenBar`
is retired: its Enter half is `TheBarsEnterIsTheQueryFieldsOnly`, its
Escape half `EscapeInTheEditorTextLeavesTheBarOpen`, and its Ctrl+G half
is inverted by `CtrlGFromTheEditorTextStepsAndKeepsFocusInTheEditor` and
`CtrlGWithTheBarClosedPassesThroughTheEditor`; round 11's
`CtrlFInReadingModeOrWithNoTabOpensVaultSearch` becomes
`CtrlFWithNoNoteSurfaceOpensVaultSearch`.
- `CtrlFInANoteFocusesTheQueryAndSelectsAll`; `CtrlFOnABaseTabReachesTheQuickFilterNotTheBar`; `CtrlFOnTheWelcomeViewWithNoWorkspaceReachesVaultSearch` (hosted shell, `Workspace == null`: `ToggleSearchCommand` executes; nothing throws).
- `CtrlFWithNoNoteSurfaceOpensVaultSearch` — no tab open, and a graph tab: Ctrl+F executes `ToggleSearchCommand` and the bar stays closed (PR 1 also covers a reading-mode note here; PR 2 removes that case). Mutation: remove the fall-through arm, or resolve the command through `Workspace?.` → the overlay stays closed.
- `CtrlFOnACanvasReachesTheCanvasFilterNotTheBar` — a canvas tab with the board holding the keys: Ctrl+F through the shell sets the canvas document's `FilterFocusRequest` (as `CanvasNavigatorTests.cs:6330` reads it), the find bar stays closed and `ToggleSearchCommand` did not execute — the third row of the shared Ctrl+F group, delivered as its recorded reason says. Mutation: deliver the window's Ctrl+F from `Window_PreviewKeyDown` instead of a bubbling `KeyBinding` → vault search opens and `FilterFocusRequest` stays null (`CtrlFOnABaseTabReachesTheQuickFilterNotTheBar` fails the same way).
- `AnEditBeforeTheDebounceNeverReplacesStaleText` — a Theory over three cases, each with the query typed and the first hit (offset 0) revealed as the current hit, then, inside the 150 ms window, the reader's own edit, then Replace with ReplaceText "q". *Another match survives*: query "a", "a b a", edit `document.Replace(0, 1, "x")` → "x b a"; after the press the document is exactly "x b a" (the surviving "a" at offset 4 untouched), `TotalCount` 1, `CurrentIndex` 0. *No match remains*: query "a", "a b c", the same edit → "x b c"; after the press the document is exactly "x b c", `TotalCount` 0. *The hit's middle changed*: query "abc", "abc d abc", edit `document.Replace(1, 1, "x")` (strictly inside the hit, so neither anchor moves and the span keeps its length) → "axc d abc"; after the press the document is exactly "axc d abc", `TotalCount` 1. In every case the one line the press speaks is "Match changed; nothing replaced." (never "1 of 1", never "No matches") and the caret and selection are where the edit left them. Mutations: recompute before validating and act on the recomputed current hit (the round-7 design) → the first case writes "x b q", the second speaks "No matches", the third writes "axc d q"; validate by span only, dropping the `FindNext` check → the third case writes "q d abc".
- `AnEditElsewhereBeforeTheDebounceStillReplacesTheTrackedHit` — "a b a", query "a", Next to the second "a" (offset 4, "2 of 2"); inside the 150 ms window insert "zz" at offset 0 ("zza b a"); Replace with ReplaceText "q": the document is exactly "zza b q" — the tracked hit, followed by its anchors to offset 6, is the one replaced, and no refusal is spoken. Mutation: validate at the offset captured when the hit became current instead of through the anchors → offset 4 now holds "b", the press refuses, and the fact fails.
- `AnOptionChangeBeforeReplaceRecountsFirst` — toggle Match case then Replace All within the debounce: only the case-sensitive matches are replaced (the set was recomputed synchronously). Mutation: act on the stamped set → the wrong matches change.
- `CloseAfterAShortenedDocumentRestoresAClampedCaret` — open at offset 40, delete the tail so the document is 10 chars, Escape: caret at 10, no exception. Mutation: restore the raw captured offset → `ArgumentOutOfRangeException`.
- `EnterAndCtrlGStepNextAndShiftStepsPrevious` — focus in the query: Enter and Ctrl+G step next, Shift+Enter and Ctrl+Shift+G step previous (Ctrl+G here through the window route). Mutation: drop the Ctrl+G `KeyBinding` → Ctrl+G from the query does nothing.
- `CtrlGFromTheEditorTextStepsAndKeepsFocusInTheEditor` — "a b a b a", query "a", the bar open at "1 of 3", keyboard focus moved to the editor's text area (`IsKeyboardFocused` asserted): `Press(textArea, Key.G, ModifierKeys.Control)` returns true; the editor's selection is offset 4, length 1; `CurrentIndex` 2; the heard list holds exactly one HostComposed "2 of 3"; the text area still has keyboard focus; the document text is unchanged; then Ctrl+Shift+G selects offset 0, "1 of 3" heard once. Mutations: deliver Ctrl+G only from the bar's map (round 11's design) → the press returns false and nothing moves; step by `OpenFindInNote` (focusing the query) → focus leaves the text area; announce in both `Reveal` and `Next` → two lines heard.
- `CtrlGWithTheBarClosedPassesThroughTheEditor` — the bar never opened, and opened then closed: Ctrl+G and Ctrl+Shift+G from the editor text each return false from `Press` (unhandled), the caret and selection are unchanged, nothing is heard, `IsOpen` stays false and vault search did not open. Mutations: bind the window `KeyBinding` to a plain command (`{Binding Workspace.FindNextInNoteCommand}`) → `Press` returns true (the command manager marks a matched key handled) → fails; drop `e.ContinueRouting = !e.CanExecute` → the same.
- `CtrlGFromOutsideTheNoteSurfacesPassesThrough` — the active tab's bar open, keyboard focus on a Files tree row, then on the tab's header item: Ctrl+G returns false, the note's selection and `CurrentIndex` are unchanged, nothing is heard. Mutation: CanExecute from "the active tab's bar is open" without the focus walk → the sidebar steps the note.
- `TheBarsEnterIsTheQueryFieldsOnly` — "a b a", query "a", the bar open: with the focus on `FindInNoteReplaceAll` and ReplaceText "q", Enter runs Replace All ("q b q"), not Next, and Ctrl+Z restores "a b a"; with the focus in the editor and the bar still open, Enter types a newline into the document and `CurrentIndex` is unchanged. Mutations: let the Enter action act whatever holds the focus → Enter on Replace All steps Next and writes nothing; run the bar's map from a route above the editor (the tab content root, or `Window_PreviewKeyDown`) → Enter in the editor steps the find instead of typing a newline.
- `EscapeInTheEditorTextLeavesTheBarOpen` — the bar open, focus in the editor: Escape leaves `IsOpen` true and the renderer registered; with an editor popover open (Ctrl+E on an embed) and the focus in the editor, Escape closes the popover and the bar stays open. Mutation: deliver Escape → Close from a route above the text (the tab content root or a window binding) → the bar closes.
- `EscapeClosesClearsHighlightsAndReturnsFocusToTheEditor` (in the bar; the renderer is unregistered — assert `BackgroundRenderers` count).
- `TheBarIsASiblingOfTheEditorPeerNotAChild` — the editor's `GetChildrenCore` is unchanged (`AvalonDocumentBufferCensus` stays green) and the bar's peer hoists to the tab content.
- `TypingAnnouncesOnlyTheFinalCount` (coalescing).

`FindInNoteBarTests` — PR 2 (the reading fixture: a note whose
projection holds "alpha" in a heading, a list item and a link, three
matches in all):
- `CtrlFInReadingModeOpensTheBarOverTheReadingView` — a reading-mode note, focus in the reading surface: Ctrl+F opens the bar with focus in the query and its text selected, `Mode` is `Reading`, and `ToggleSearchCommand` did not execute. Mutation: keep PR 1's reading-mode fall-through arm → vault search opens.
- `CtrlGFromTheReadingSurfaceMovesTheSelectionAndLeavesTheDocumentAlone` — query "alpha", the bar open at "1 of 3", focus on the reading surface (`IsKeyboardFocused` asserted): Ctrl+G returns true, `surface.Selection.Text` is "alpha" at the second match's pointers, `CurrentIndex` 2, exactly one "2 of 3" heard, the surface still focused; and across opening, highlighting and three steps (one wrapping): `new TextRange(Document.ContentStart, Document.ContentEnd).Text` is unchanged, the document's structural fingerprint (pre-order element types with each `Run`'s text) is unchanged, the surface's `TextChanged` fired zero times, `ReadingSurfacePeer.GetChildren()` returns the same peer instances in the same order, and `ContentRevision` is unchanged. Mutations: highlight with `TextRange.ApplyPropertyValue(TextElement.BackgroundProperty, …)` → `TextChanged` fires and the fingerprint changes (split runs); reveal by setting `CaretPosition` only → the selection is empty.
- `CtrlGFromALinkInTheReadingDocumentSteps` — keyboard focus on the `Hyperlink` holding the third "alpha": Ctrl+G through the `IInputElement` `Press` steps the surface's selection to the next match and keyboard focus stays on the link. Mutation: walk only visual parents in `SessionOwningFocus` → the walk throws on the content element (the crash `FindAncestorDataContext`'s comment records), or nothing steps.
- `CtrlGWithTheBarClosedPassesThroughTheReadingSurface` — the bar closed, focus on the reading surface: Ctrl+G and Ctrl+Shift+G return false, the selection is unchanged, nothing is heard, `IsOpen` false. Mutation: a plain-command binding → `Press` returns true.
- `EscapeInTheReadingSurfaceLeavesTheBarOpen` — the bar open, focus on the reading surface: Escape leaves `IsOpen` true and the adorner in place. Mutation: as the editor fact's.
- `AModeSwitchWithTheBarOpenKeepsTheQueryAndMovesTheHighlights` — editor mode, "alpha beta alpha" (the projection reads the same), query "alpha", Next to "2 of 2"; Ctrl+Shift+E with the bar open: `Query` unchanged and the editor's `BackgroundRenderers` no longer hold a `FindMatchBackgroundRenderer`; once the projection has applied and the debounce is flushed, the reading render scope's adorner layer holds one `FindMatchAdorner`, `TotalCount` 2 over the reading text, `CurrentIndex` the match the reading selection covers exactly (none at the surface's rebound caret → 0), the reading caret and selection where the rebind left them, the three replace controls collapsed, nothing spoken by the recount; Ctrl+Shift+E back: the adorner removed, the renderer registered, `CurrentIndex` 2 (the editor's selection still covers the second match). Mutations: leave the old surface's highlighter attached → the renderer stays registered in reading mode; recount without recomputing `CurrentIndex` → reading mode keeps 2 with no selection covering it.
- `TheReadingModeBarHidesReplace` — reading mode: `FindInNoteReplaceText`, `FindInNoteReplace` and `FindInNoteReplaceAll` are `Visibility.Collapsed`, `ReplaceCommand.CanExecute` and `ReplaceAllCommand.CanExecute` are false, and the landmark peer's children are exactly Query, Count, Previous, Next, Match case, Whole word, Close in that order; editor mode adds Replace text, Replace and Replace All before Close. Mutation: disable instead of collapse → the three stay among the peer's children.

`ReadingFindSurfaceTests` (new, PR 2; a `ReadingSurface` hosted in a
shown window, documents applied through `ApplyBuiltDocument`, WB-20's
completions through its gated harness):
- `TheIndexCoversTheSurfaceRangeAndNothingEmbedded` — a projection with a heading, an ordered list (WB-11's marker runs), a link, a word split across a bold run ("**fo**o"), a table, a task check box, a rendered math element and WB-20's diagram failure shape: the index text with whitespace and U+FFFC removed equals `new TextRange(ContentStart, ContentEnd).Text` with whitespace removed; query "foo" finds the split word once and `Reveal` selects text reading "foo"; the failure header (an in-range `Run`) is found; the diagram element's description and the math element's speech are not; a query spanning the end of one paragraph and the start of the next finds nothing. Mutations: append each embedded element's `AutomationProperties.Name` to the index → the description is found; drop the block separator → the cross-paragraph query matches.
- `ContentChangedFiresOnceForEachContentPath` — a Theory: a merge (`Replaced`), a model switch (`Replaced`), a streamed append (`Appended`), a WB-20 completion (`CompletedInPlace`): one `ContentChanged` with that kind and `ContentRevision` up by one; the rebind fast path raises none. Mutation: omit the raise from `Model_BlocksAppended` → the append case fails.
- `TheCurrentMatchIsTheSelectionAndIsBroughtIntoView` — a note long enough that the second match is below the viewport: Next makes `surface.Selection` exactly the match (text and start pointer), and after `UpdateLayout` the match's character rectangle lies inside the viewport; keyboard focus is unchanged. Mutations: move `CaretPosition` only → the selection is empty; drop the bring-into-view → the rectangle is outside the viewport.
- `TheAdornerHasNoPeerAndLivesInTheSurfacesOwnLayer` — with matches drawn: `UIElementAutomationPeer.CreatePeerForElement(adorner)` is null; the adorner is in `AdornerLayer.GetAdornerLayer(surface.DocumentRenderScope)`, whose visual parent is a `ScrollContentPresenter` inside the surface (not the window's `AdornerDecorator`); it is not hit-test visible and not focusable; `ClearHighlights` removes it. Mutations: adorn the `ReadingSurface` itself → the layer is the window's; give the adorner a `FrameworkElementAutomationPeer` → a peer exists.
- `TheLoadingPlaceholderIsNeverSearched` — the surface showing "Loading reading view…" after a model switch, the bar open, query "reading": "Waiting for the reading view." is spoken, `TotalCount` 0, nothing selected; when the projection applies, the count is spoken once. Mutation: search whatever the surface holds → "1 of 1" over the placeholder.
- `AReadingRepublishReRunsTheSearch` — query "alpha" over a projection with two; a merge of a projection with three: the drawn matches are dropped at once (the adorner holds none before the debounce runs), after the flush `TotalCount` is 3, nothing is spoken, and the caret is where `ApplyBuiltDocument` restored it. Mutation: no `ContentChanged` subscription → `TotalCount` stays 2.
- `AnInPlaceDiagramCompletionReRunsTheSearch` — WB-20's gated harness: stage 1 published with a pending diagram fence, query "rendered" counts 0 (the pending shape holds only the source), a match elsewhere drawn; `CompleteDiagrams` lands a `Failed` outcome ("Diagram could not be rendered" plus a reason): after the flush the header's match is counted, and the match outside the diagram paragraph was redrawn by the recount. Mutation: raise `ContentChanged` only from `ApplyBuiltDocument` → the count stays 0.

`FindInNoteResultsTests` (new; only when the list ships; PR 1 for the
editor, PR 2 adds its reading cases):
- `TheResultsListNamesEachMatchByLineAndContext` — editor "a\nxx alpha yy\nalpha", query "alpha": rows "line 2: xx alpha yy" and "line 3: alpha", the list named "Matches", PositionInSet 1 and 2, SizeOfSet 2; in reading mode the rows say "paragraph {N}: …". Mutation: name rows by context alone → the names fail.
- `EnterOnAResultLandsTheMatchAndSpeaksTheCountOnce` — focus on the second row, Enter: the attached surface's selection is the second match, "2 of 2" heard once, focus still on the row. Mutation: focus the surface on Enter → focus leaves the list.
- `ArrowsMoveBetweenResultsWithoutMovingTheSelection` — Down from row 1: the list's selection is row 2, the note's selection unchanged, nothing heard. Mutation: reveal on selection change → the note's selection moves.

`ChordTableTests` (PR 1):
- `CommandRows_HaveNoActiveChordCollision` passes with the three-command key — Ctrl+F's group is now three command rows in three scopes (Grid, Canvas, Global), so the fact looks up exactly `slate.bases.quickFilter | slate.canvas.filterCards | slate.editor.findInNote` (under disposition 2, `slate.canvas.filterCards | slate.editor.findInNote`, with the base/canvas entry removed); it gains one assertion in its staleness loop: every recorded key is the key of a live group (`liveKeys.Contains(key)`, `liveKeys` collected in the group loop), so a superseded entry cannot stay beside its successor (it holds on `main`: the six entries are the six live groups' keys). Mutations: record only a base/editor pair → the group's lookup fails; keep the base/canvas pair beside the triple → the live-key assertion fails; remove the triple → the lookup fails.
- `EveryScopeIsEitherScrapedFromProductionOrDispositioned` (`:1104`) — the new `ChordScope.FindInNote` is scraped, not dispositioned.
- `TableCoversEveryDeclarativelyBoundChord_AndNothingStale` (`:383`) — the Global arm sees the new Ctrl+F, Ctrl+G and Ctrl+Shift+G `KeyBinding`s both ways, and the scoped comparison checks `ChordScope.FindInNote`'s three rows both ways against `FindInNoteChords()`.
- `FindInNoteKeysAreRowsInTheirOwnScope` (new, beside `SidebarFilterClearIsASurfaceRowInItsOwnScope`): the `ChordScope.FindInNote` rows are exactly `windows.findInNote.next` Enter, `windows.findInNote.previous` Shift+Enter and `windows.findInNote.close` Escape, all surface rows (`IsCommandId` and `IsRegistered` false, `MacChord` null, a reason naming the command they run); `slate.editor.findNext` (Ctrl+G) and `slate.editor.findPrevious` (Ctrl+Shift+G) are registered command rows in `ChordScope.Global` with no mac chord; the `MainWindow.xaml` `KeyBinding`s for G bind `FindInNoteKeyRoute.FindNext` and `FindInNoteKeyRoute.FindPrevious` (their `Command` attributes name them — a `{Binding …}` would swallow the key when it cannot act); `FindInNoteKeyRoute.FindNext.InputGestures` and `FindPrevious.InputGestures` are empty; no `ChordScope.Editor` row shares a chord with the bar's rows but `windows.editor.closePopover`'s Escape, in its own scope; and `KeyBindingChords("FindInNoteBar.xaml")` is empty. Mutations: declare `windows.findInNote.close` in `ChordScope.Editor` → this fact and the surface loop fail (two Escape rows in `Editor`); drop the Ctrl+Shift+G `KeyBinding` → the Global reverse check fails (a Global row with no binding); bind the Ctrl+G `KeyBinding` to `{Binding Workspace.FindNextInNoteCommand}` → the routed-command assertion fails; give `FindNext` a `KeyGesture` → the gestures assertion fails; write the previous row as "Shift+Ctrl+G" → the forward check fails (the binding's canonical `Ctrl+Shift+G` has no row); deliver Escape through a `KeyBinding` in `FindInNoteBar.xaml` → the empty-bindings assertion fails.
- `ChordsJson_IsExactlyTheTablesProjection` regenerated.

`ThemeTokenContrastTests.EveryTextBearingPairClearsProjectApcaFloor`
gains `("find-match/text", "Slate.TextColor", "Slate.FindMatchColor")`
(PR 1: the editor's text over the fill) and `("find-ring/surface",
"Slate.FindMatchRingColor", "Slate.SurfaceColor")`, `("find-ring/raised",
"Slate.FindMatchRingColor", "Slate.RaisedSurfaceColor")` (PR 2: the ring
against the reading surface and its code blocks — the canvas-ring
precedent). Mutation: a pale ring colour → |Lc| under the floor.

`WcMatrixEvidenceCensus`: the new ids registered; the new row's evidence
executable. `CommandRegistrationTests` stays green with the three
registrar entries.

FlaUI (`ShellAccessibilityTests.FindInNote.cs`, journey naming after
`Palette_TypingAnnouncesOnlyTheFinalCountAndSelection`,
`ShellAccessibilityTests.Palette.cs:43`):
- PR 1 `FindInNote_TypingAnnouncesTheCountAndEnterMovesTheCaret` — opens a fixture note, Ctrl+F, types, reads `FindInNoteCount`'s Name, Enter, and asserts the editor's Text pattern selection moved; `AssertAxeClean(process, "find-in-note")` with text typed (the #1106 lesson).
- PR 1 `FindInNote_CtrlGFromTheEditorTextMovesTheSelectionAndKeepsFocus` — with the bar open, focus set on `MarkdownEditor`; Ctrl+G: the editor's Text pattern selection is the next match and the focused element is still the editor.
- PR 2 `FindInNote_ReadingView_CtrlGFromTheTextMovesTheSelectionAndReplaceIsAbsent` — a reading-mode note, Ctrl+F, type, focus set on `ReadingSurface`, Ctrl+G: the surface's Text pattern selection reads the query, the focused element is still `ReadingSurface`, `FindInNoteReplace` is absent from the control view; `AssertAxeClean(process, "find-in-note-reading")`.
- With the list: `FindInNote_ResultsListIsAListOfNamedMatches` — `FindInNoteResults` is a List named "Matches" whose ListItems carry "line {N}: …" names with PositionInSet and SizeOfSet.

## Docs and registers

- `chords.json` regenerated (`SLATE_CHORDS_UPDATE=1`): `slate.editor.findInNote`, `slate.editor.findNext` and `slate.editor.findPrevious` in `commands` with `"scope": "Global"`, and the three `windows.findInNote.*` rows in `chordSurface` with `"scope": "FindInNote"`; `deliveryEvidence.commands` gains the three ids.
- `generate-parity-matrix.py`: drop the `slate.editor.findInNote`
  override, add the two Windows-only ids to the Windows-only arm, add the
  "find-in-selection: unverified on mac; not shipped" note and (PR 2) the
  note "find in note also works in the reading view on Windows (mac has
  no reading-view find; follow-up 'mac: find in note in the reading view,
  with ⌘G from the reading text', filed by WB-0)"; regenerate
  `parity_matrix.md`; `--check` in CI.
- `w_c_matrix.md`: new row "Find in note (WB-22)" with ten cells (Group
  landmark; Edit, Text, Button, CheckBox children, the replace controls
  in editor mode only, a List of ListItems if the list ships;
  Value/Invoke/Toggle (Selection with the list); the focus route above,
  including Ctrl+G from the text with focus kept; the count's
  notification contract; the reading highlights as no element at all;
  evidence names; Narrator and NVDA cells for both modes, filled from the
  pass). `w_c_automation_ids.json` gains the eleven ids (twelve with
  `FindInNoteResults`) with that surface title.
- `a11y_trigger_designations.json`: the `FindInNotePhrase` residue
  entries; `python -I scripts/a11y_trigger_ledger.py --write` regenerates
  contract 38's ledger.
- Contract 28: append a note that the find bar is a non-modal landmark
  (P5 honoured, no `Popup`); contract 25: a note that the Bases filter
  keeps Ctrl+F on a Base (WD12).
- Help: the Windows keyboard page gains Ctrl+F (editor and reading
  view), Ctrl+G and Ctrl+Shift+G (from the bar, the editor text and the
  reading view; for the cells step 0 recorded as needing it, the step
  that leaves the AT's mode, or the results list where it replaced the
  keys), and for the bar Enter / Shift+Enter (in the query) and Escape.
  `SlateTextEditor.cs:538` gets the rationale comment the issue asks for.
- PR bodies: PR 1 carries step 0's record and the path it chose; PR 2
  restates the path.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb22'
function Assert-Ok([string] $What) { if ($LASTEXITCODE -ne 0) { throw "$What exited $LASTEXITCODE" } }
# What this head ships: PR 2 adds the reading index; the results list only when step 0 called for it.
$readingShipped = Test-Path 'apps/slate-windows/src/SlateWindows/Reading/ReadingTextIndex.cs'
$listShipped = [bool](Select-String -Path 'apps/slate-windows/src/SlateWindows/FindInNoteBar.xaml' -Pattern 'FindInNoteResults' -SimpleMatch -Quiet)
./apps/slate-windows/generate-bindings.ps1 -Locked; Assert-Ok 'generate-bindings'
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release; Assert-Ok 'dotnet build'
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
$env:SLATE_CHORDS_UPDATE = '1'
dotnet test $tests --no-build --configuration Release --filter "FullyQualifiedName~SlateWindows.Tests.ChordTableTests.ChordsJson_IsExactlyTheTablesProjection"; $chords = $LASTEXITCODE
Remove-Item Env:SLATE_CHORDS_UPDATE
if ($chords -ne 0) { throw "chords.json regeneration exited $chords" }
$filter = "FullyQualifiedName~FindInNoteViewModelTests|FullyQualifiedName~FindInNotePhraseTests|FullyQualifiedName~FindInNoteBarTests|FullyQualifiedName~ReadingFindSurfaceTests|FullyQualifiedName~FindInNoteResultsTests|FullyQualifiedName~ChordTableTests|FullyQualifiedName~CommandRegistrationTests|FullyQualifiedName~WcMatrixEvidenceCensus|FullyQualifiedName~ThemeTokenContrastTests|FullyQualifiedName~AvalonDocumentBufferCensus|FullyQualifiedName~A11yCorpusCensus|FullyQualifiedName~ReadingViewTests|FullyQualifiedName~ReadingDiagramTests|FullyQualifiedName~ReadingFocusTests"
$names = @(dotnet test $tests --no-build --configuration Release --list-tests --filter $filter | Select-String '^\s{4}\S' | ForEach-Object { ($_.Line.Trim() -split '\(', 2)[0] }); Assert-Ok 'test discovery'
$pr1 = @(
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
  'FindInNoteBarTests.CtrlFWithNoNoteSurfaceOpensVaultSearch',
  'FindInNoteBarTests.CtrlFOnTheWelcomeViewWithNoWorkspaceReachesVaultSearch',
  'FindInNoteBarTests.AnEditBeforeTheDebounceNeverReplacesStaleText',
  'FindInNoteBarTests.AnEditElsewhereBeforeTheDebounceStillReplacesTheTrackedHit',
  'FindInNoteBarTests.AnOptionChangeBeforeReplaceRecountsFirst',
  'FindInNoteBarTests.CloseAfterAShortenedDocumentRestoresAClampedCaret',
  'FindInNoteBarTests.EnterAndCtrlGStepNextAndShiftStepsPrevious',
  'FindInNoteBarTests.CtrlGFromTheEditorTextStepsAndKeepsFocusInTheEditor',
  'FindInNoteBarTests.CtrlGWithTheBarClosedPassesThroughTheEditor',
  'FindInNoteBarTests.CtrlGFromOutsideTheNoteSurfacesPassesThrough',
  'FindInNoteBarTests.TheBarsEnterIsTheQueryFieldsOnly',
  'FindInNoteBarTests.EscapeInTheEditorTextLeavesTheBarOpen',
  'FindInNoteBarTests.EscapeClosesClearsHighlightsAndReturnsFocusToTheEditor',
  'FindInNoteBarTests.TheBarIsASiblingOfTheEditorPeerNotAChild',
  'FindInNoteBarTests.TypingAnnouncesOnlyTheFinalCount',
  'ChordTableTests.CommandRows_HaveNoActiveChordCollision',
  'ChordTableTests.EveryScopeIsEitherScrapedFromProductionOrDispositioned',
  'ChordTableTests.TableCoversEveryDeclarativelyBoundChord_AndNothingStale',
  'ChordTableTests.FindInNoteKeysAreRowsInTheirOwnScope',
  'ChordTableTests.ChordsJson_IsExactlyTheTablesProjection',
  'ThemeTokenContrastTests.EveryTextBearingPairClearsProjectApcaFloor')
$pr2 = @(
  'FindInNoteBarTests.CtrlFInReadingModeOpensTheBarOverTheReadingView',
  'FindInNoteBarTests.CtrlGFromTheReadingSurfaceMovesTheSelectionAndLeavesTheDocumentAlone',
  'FindInNoteBarTests.CtrlGFromALinkInTheReadingDocumentSteps',
  'FindInNoteBarTests.CtrlGWithTheBarClosedPassesThroughTheReadingSurface',
  'FindInNoteBarTests.EscapeInTheReadingSurfaceLeavesTheBarOpen',
  'FindInNoteBarTests.AModeSwitchWithTheBarOpenKeepsTheQueryAndMovesTheHighlights',
  'FindInNoteBarTests.TheReadingModeBarHidesReplace',
  'ReadingFindSurfaceTests.TheIndexCoversTheSurfaceRangeAndNothingEmbedded',
  'ReadingFindSurfaceTests.ContentChangedFiresOnceForEachContentPath',
  'ReadingFindSurfaceTests.TheCurrentMatchIsTheSelectionAndIsBroughtIntoView',
  'ReadingFindSurfaceTests.TheAdornerHasNoPeerAndLivesInTheSurfacesOwnLayer',
  'ReadingFindSurfaceTests.TheLoadingPlaceholderIsNeverSearched',
  'ReadingFindSurfaceTests.AReadingRepublishReRunsTheSearch',
  'ReadingFindSurfaceTests.AnInPlaceDiagramCompletionReRunsTheSearch')
$listFacts = @(
  'FindInNoteResultsTests.TheResultsListNamesEachMatchByLineAndContext',
  'FindInNoteResultsTests.EnterOnAResultLandsTheMatchAndSpeaksTheCountOnce',
  'FindInNoteResultsTests.ArrowsMoveBetweenResultsWithoutMovingTheSelection')
$required = @($pr1) + @(if ($readingShipped) { $pr2 }) + @(if ($listShipped) { $listFacts })
$missing = @($required | Where-Object { $r = ".$_"; -not ($names | Where-Object { $_.EndsWith($r) }) })
if ($missing.Count -gt 0) { throw "the filter does not select: $($missing -join ', ')" }
if (-not ($names | Where-Object { $_ -match '\.FindInNotePhraseTests\.' })) { throw 'no FindInNotePhraseTests fact discovered' }
if ($readingShipped) { foreach ($class in 'ReadingViewTests', 'ReadingDiagramTests', 'ReadingFocusTests') { if (-not ($names | Where-Object { $_ -match "\.$class\." })) { throw "no $class fact discovered" } } }
dotnet test $tests --no-build --configuration Release --filter $filter; Assert-Ok 'unit and hosted facts'
# desktop free, NVDA closed (the FlaUI facts live in the partial class ShellAccessibilityTests, so select by method name):
$at = 'apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj'
$atFilter = 'FullyQualifiedName~FindInNote_'
$atNames = @(dotnet test $at --no-build --configuration Release --list-tests --filter $atFilter | Select-String '^\s{4}\S' | ForEach-Object { $_.Line.Trim() }); Assert-Ok 'FlaUI discovery'
$journeys = @('FindInNote_TypingAnnouncesTheCountAndEnterMovesTheCaret', 'FindInNote_CtrlGFromTheEditorTextMovesTheSelectionAndKeepsFocus') + @(if ($readingShipped) { 'FindInNote_ReadingView_CtrlGFromTheTextMovesTheSelectionAndReplaceIsAbsent' }) + @(if ($listShipped) { 'FindInNote_ResultsListIsAListOfNamedMatches' })
$atMissing = @($journeys | Where-Object { $j = ".$_"; -not ($atNames | Where-Object { $_.EndsWith($j) }) })
if ($atMissing.Count -gt 0) { throw "the FlaUI filter does not select: $($atMissing -join ', ')" }
dotnet test $at --no-build --configuration Release --filter $atFilter; Assert-Ok 'FlaUI journeys'
python -I scripts/generate-parity-matrix.py; Assert-Ok 'parity matrix'
python -I scripts/generate-parity-matrix.py --check; Assert-Ok 'parity matrix check'
python -I scripts/a11y_trigger_ledger.py --write; Assert-Ok 'trigger ledger'
python -I scripts/a11y_trigger_ledger.py --check; Assert-Ok 'trigger ledger check'
$pr1Files = @(
  'apps/slate-windows/src/SlateWindows/FindInNoteViewModel.cs',
  'apps/slate-windows/src/SlateWindows/FindInNoteSurface.cs',
  'apps/slate-windows/src/SlateWindows/EditorFindSurface.cs',
  'apps/slate-windows/src/SlateWindows/FindInNoteBar.xaml.cs',
  'apps/slate-windows/src/SlateWindows/FindInNoteKeyRoute.cs',
  'apps/slate-windows/src/SlateWindows/FindMatchBackgroundRenderer.cs',
  'apps/slate-windows/src/SlateWindows/Panels/FindInNotePhrase.cs',
  'apps/slate-windows/src/SlateWindows/WorkspaceViewModel.cs',
  'apps/slate-windows/src/SlateWindows/VaultLifecycleViewModel.cs',
  'apps/slate-windows/src/SlateWindows/MainWindow.xaml.cs',
  'apps/slate-windows/src/SlateWindows/SlateTextEditor.cs',
  'apps/slate-windows/src/SlateWindows/Commands/ChordTable.cs',
  'apps/slate-windows/src/SlateWindows/Commands/SlateCommandRegistrar.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/FindInNoteViewModelTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/FindInNotePhraseTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/FindInNoteBarTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/CommandPaletteTests.ShellKeys.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/ChordTableTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/ThemeTokenContrastTests.cs',
  'apps/slate-windows/tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.FindInNote.cs')
$pr2Files = @(
  'apps/slate-windows/src/SlateWindows/Reading/ReadingSurface.cs',
  'apps/slate-windows/src/SlateWindows/Reading/ReadingTextIndex.cs',
  'apps/slate-windows/src/SlateWindows/Reading/ReadingFindSurface.cs',
  'apps/slate-windows/src/SlateWindows/Reading/FindMatchAdorner.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/ReadingFindSurfaceTests.cs')
$files = @($pr1Files) + @(if ($readingShipped) { $pr2Files }) + @(if ($listShipped) { 'apps/slate-windows/tests/SlateWindows.Tests/FindInNoteResultsTests.cs' })
$absent = @($files | Where-Object { -not (Test-Path $_) })
if ($absent.Count -gt 0) { throw "files this head must carry are missing: $($absent -join ', ')" }
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $files; Assert-Ok 'dotnet format'
```

(`WorkspaceViewModel.cs` holds `WorkspaceTabViewModel`, `:94`; there is
no `WorkspaceTabViewModel.cs`.)

CI: the Windows lane and the FlaUI shell gate. Step 0's record is in PR
1's body before any code is reviewed. AT, NVDA full pass and Narrator, in
both modes (PR 1 the editor, PR 2 the reading view and both again), launched
per §4 rule 4 — editor: Ctrl+F in a note: "Find in note, edit"; typing
"the": "3 of 12"; Enter: "4 of 12", the editor selection read; Enter at
the last hit: "1 of 12, wrapped to the top"; Shift+Enter; Tab to "Match
case, check box, not checked"; Replace All: "Replaced 12 matches.";
Escape: the editor's filename line and the caret where it was; with the
bar open, focus in the text, Ctrl+G: "5 of 12" once, focus still in the
editor, the line read as the AT reads a selection; reading view (NVDA in
browse and focus mode, Narrator with scan mode off and on): Ctrl+F:
"Find in note, edit"; typing: the count; Tab never reaches a Replace
control; focus on the reading text, Ctrl+G and Ctrl+Shift+G: the count
once, focus still in the reading view, the selection read where the AT
reads it; Ctrl+Shift+E with the bar open: the bar stays, the count
follows the new surface on the next step; Escape in the reading text: the
bar stays open; Ctrl+F on the welcome view or with no tab: the search
overlay's own line; Ctrl+F on a Base: "Quick filter, edit"; Ctrl+F on a
canvas: the canvas filter field's own name, never the find bar. With the
list: arrows read "line 2: …, 2 of 5"-style item names and Enter lands
and speaks the count. Recorded in the PR bodies; each PR draft until its
pass is recorded.

## Acceptance

- [ ] Step 0's record (per screen reader, surface and mode: Ctrl+G and Ctrl+Shift+G seen or not, and how the reader leaves the mode) is in PR 1's body with the path it chose — the keyboard design alone, or the results list alongside or instead of the text-surface keys — and PR 2 restates it.
- [ ] WD12 recorded in the PR bodies: the 2026-10-21 answer (editor and reading view; Ctrl+G from the text; results-list fallback) as built, and for the pending parts disposition 1 — the three-command key; replace in (editor only); regex out; find-in-selection deferred.
- [ ] Ctrl+F's command group is recorded whole: under disposition 1 the one sorted key `slate.bases.quickFilter | slate.canvas.filterCards | slate.editor.findInNote`, its reason naming each row's delivery, replaces the `slate.bases.quickFilter | slate.canvas.filterCards` entry; under disposition 2 `slate.canvas.filterCards | slate.editor.findInNote` replaces it; `CommandRows_HaveNoActiveChordCollision`, with its new live-key assertion, is green; `CtrlFOnABaseTabReachesTheQuickFilterNotTheBar` and `CtrlFOnACanvasReachesTheCanvasFilterNotTheBar` show the delivery the reason claims, with the window's Ctrl+F a bubbling `KeyBinding`.
- [ ] Ctrl+G and Ctrl+Shift+G step from the bar, the editor text and the reading surface with focus kept and the count spoken once, through the two `MainWindow.xaml` `KeyBinding`s on `FindInNoteKeyRoute`'s routed commands, and pass through unhandled when the focused note's bar is closed or focus is outside a note surface (`CtrlGFromTheEditorTextStepsAndKeepsFocusInTheEditor`, `CtrlGFromTheReadingSurfaceMovesTheSelectionAndLeavesTheDocumentAlone`, `CtrlGFromALinkInTheReadingDocumentSteps`, `CtrlGWithTheBarClosedPassesThroughTheEditor`, `CtrlGWithTheBarClosedPassesThroughTheReadingSurface`, `CtrlGFromOutsideTheNoteSurfacesPassesThrough` green).
- [ ] The bar's own keys live in `ChordScope.FindInNote` only: its three surface rows (Enter, Shift+Enter, Escape) are delivered by `FindInNoteBar.Bind`'s map and scraped both ways by `FindInNoteChords()`; the two step rows are `Global`; no `ChordScope.Editor` row changes and `SlateTextEditor.OnPreviewKeyDown` gains no arm; Escape in the text never closes the bar; `FindInNoteKeysAreRowsInTheirOwnScope`, `TableCoversEveryDeclarativelyBoundChord_AndNothingStale`, `EveryScopeIsEitherScrapedFromProductionOrDispositioned`, `TheBarsEnterIsTheQueryFieldsOnly`, `EscapeInTheEditorTextLeavesTheBarOpen` and `EscapeInTheReadingSurfaceLeavesTheBarOpen` are green.
- [ ] `Replace` acts only on the tracked hit it last revealed: every `AnEditBeforeTheDebounceNeverReplacesStaleText` case (another match survives, no match remains, the hit's middle changed) refuses with "Match changed; nothing replaced.", and `AnEditElsewhereBeforeTheDebounceStillReplacesTheTrackedHit` replaces the hit its anchors followed; in reading mode the replace controls are collapsed and their commands cannot execute (`TheReadingModeBarHidesReplace`).
- [ ] The reading view's find changes nothing in the reading document: its text, element structure, `TextChanged` count, `ContentRevision` and `ReadingSurfacePeer`'s children are unchanged by highlighting and stepping, and the adorner has no peer (`CtrlGFromTheReadingSurfaceMovesTheSelectionAndLeavesTheDocumentAlone`, `TheAdornerHasNoPeerAndLivesInTheSurfacesOwnLayer`); it re-runs on a merge and on WB-20's in-place completion (`AReadingRepublishReRunsTheSearch`, `AnInPlaceDiagramCompletionReRunsTheSearch`) and never searches the loading placeholder; a mode switch keeps the query and moves the highlights (`AModeSwitchWithTheBarOpenKeepsTheQueryAndMovesTheHighlights`).
- [ ] Every fact above fails first, then passes; mutations run locally; the FlaUI journeys green on the hosted lane.
- [ ] `GetChildrenCore` unchanged and its census green; the bar is a sibling.
- [ ] `chords.json`, `parity_matrix.md` (with the reading-view note), `w_c_matrix.md`, `w_c_automation_ids.json`, the designations and the ledger regenerated; `--check` scripts green.
- [ ] NVDA and Narrator passes recorded in both modes.
- [ ] PR bodies: PR 1 `Refs #1112`, PR 2 `Closes #1112`; "not delivered: regex (parity), find-in-selection (unverified on mac), search inside the reading view's embedded elements (outside the text range caret reading covers)"; the mac follow-up "mac: find in note in the reading view, with ⌘G from the reading text" named as WB-0's.

## Brief

Read `SlateTextEditor.cs:96`, `:212-247` and `:525-545`,
`WorkspaceTemplates.xaml:331-345`, `:414-462`, `:480-500`, `:585-600`,
`WorkspaceViewModel.cs:94`, `:248-256`, `:311-313`, `:600-602`,
`Reading/ReadingSurface.cs:30-79`, `:233-334`, `:357-459`, `:496-531`,
`:686-724`, `:934-1020`, `Reading/ReadingNavigator.cs:61-204`,
`Bases/BaseSurfaceView.cs:50-200`, `Grids/AccessibleDataGrid.cs:85-110`
and `:200-225`, `Commands/ChordTable.cs:22-98`, `:617-690`, `:794-796`,
`:915-921`, `:1361-1364`, `:1720-1732`, `:1850-1890`,
`Canvas/CanvasNavigator.cs:230-255`, `:1855-1874`, `MainWindow.xaml:30-68`,
`MainWindow.xaml.cs:80-90`, `:858-990` and `:2255-2299`,
`MainWindow.Trash.cs:17-29`,
`Commands/SlateCommandRegistrar.cs:40-70` and `:413-460`,
`VaultLifecycleViewModel.cs:270-290`, `ChordTableTests.cs:283-470`,
`:617-640`, `:704-940`, `:1093-1206` and `:1542-1562`,
`CommandPaletteTests.ShellKeys.cs:512-600`, `:655-800` and `:837`,
`AutomationLandmark.cs`, `Censuses/WcMatrixEvidenceCensus.cs:36-75`,
`ThemeTokenContrastTests.cs:15-100`, `TaskStatusPhraseTests.cs`,
`ShellAccessibilityTests.Palette.cs:43`, `ShellAccessibilityTests.cs:69`
and `:471` (`AssertAxeClean`), the WB-11 spec's Design (4) and (6)
(`InvalidateStructuralPeers()`, the cached peer children) and the WB-20
spec's "Where the text lives" and "Completion in place"
(`CompleteDiagrams`, `DiagramsCompleted`), and mac's
`NoteEditorView.swift:240-250`, `:1426-1432`, `AppState.swift:9490-9535`,
`SlateCommands.swift:1895-1906`. WPF and AvalonEdit facts this rests on
(dotnet/wpf `main`, AvalonEdit tag `v6.3.1`): at each element on the
bubble route `CommandManager.TranslateInput` runs before `OnKeyDown`
(`Generated/UIElement.cs:1934-1947`), and once a binding matched it sets
`Handled = !continueRouting` whether or not the command ran
(`CommandManager.cs:383-425`), `continueRouting` being false for a plain
`ICommand` and the CanExecute event's `ContinueRouting` for a routed
command (`RoutedCommand.cs:315-338`) — so the shell's gated plain-command
window bindings (Ctrl+J, Escape → `ClosePopoverCommand`) swallow their
key even when disabled, as `MainWindow.Trash.cs:19-21` already records,
and `AccessibleDataGrid.FilterCommand`'s `ContinueRouting`
(`Grids/AccessibleDataGrid.cs:212-218`) is the pass-through precedent;
`KeyGesture.Matches` needs the
exact modifiers (`KeyGesture.cs:174-182`); WPF's text editor's 85 class
gestures, the stock command gestures, AvalonEdit's default handlers
(`CaretNavigationCommandHandler.cs:67-116`, `EditingCommandHandler.cs:55-90`,
`TextAreaDefaultInputHandlers.cs:50-58`) and `AvalonEditCommands`
(Insert, Ctrl+D, Ctrl+I) include nothing on G, and Slate's only G chords
are Ctrl+Alt+G (`ReadingNavigator.cs:94-95`, `CanvasNavigator.cs:253`);
`TextArea.OnTextInput` ignores empty text (`TextArea.cs:863-869`);
WPF's caret is an `Adorner` over the render scope in the
`ScrollContentPresenter`'s own layer (`CaretElement.cs:43`, `:855`;
`AdornerLayer.cs:307-324`), which clips it (`ScrollContentPresenter.cs:501-504`);
`UIElement.OnCreateAutomationPeer` returns null (`UIElement.cs:3957-3964`);
`RichTextBoxAutomationPeer`'s children come from the text container
(`RichTextBoxAutomationPeer.cs:69-73`); `GetCharacterRect` answers in the
`RichTextBox`'s coordinates (`TextPointerBase.cs:859-899`); a
`RichTextBox` paints an unfocused selection only with
`IsInactiveSelectionHighlightEnabled` (`TextBoxBase.cs:879-882`);
`ItemAutomationPeer` supplies PositionInSet and SizeOfSet
(`ItemAutomationPeer.cs:263-309`).
Decision WD12: in part answered 2026-10-21 — find in note works in the
editor and the reading view, Ctrl+G and Ctrl+Shift+G step from the bar
and from the text with focus kept and the count spoken, keyboard first
with an accessible results list as the accepted fallback — and the rest
pending (recommended: disposition 1 — one `SharedCommandChords` entry for
the whole Ctrl+F command group, the sorted three-command key
`slate.bases.quickFilter | slate.canvas.filterCards | slate.editor.findInNote`
(Ctrl+F already belongs to `slate.canvas.filterCards`,
`ChordTable.cs:1361-1364`, and `CommandRows_HaveNoActiveChordCollision`
looks up one key holding every command on the chord,
`ChordTableTests.cs:339-345`), replacing the `slate.bases.quickFilter | slate.canvas.filterCards`
entry; replace in, editor mode only; regex out; find-in-selection
deferred and recorded in the parity matrix as "unverified on mac; not
shipped"; under disposition 2 `slate.bases.quickFilter` becomes
`win: null`, the Bases menu loses its gesture text, and the then-stale
`slate.bases.quickFilter | slate.canvas.filterCards` entry is replaced by
`slate.canvas.filterCards | slate.editor.findInNote`; under "regex yes"
a `SearchMode` toggle and facts are added as a recorded divergence; under
"replace no" the three replace controls, `Replace`, `ReplaceAll` and
their facts leave the chunk).
Deliver as two PRs after a spike. **Step 0, before any code:** a local,
uncommitted probe line at the top of `MainWindow.Window_PreviewKeyDown`
(`MainWindow.xaml.cs:858`) logging G with Control through
`HostLog.WriteUiAutomationDiagnostic(HostDiagnosticEvent.ReadingChordSeen, …)`
with the modifiers and `Keyboard.FocusedElement`'s type, under
`SLATE_UIA_DIAGNOSTICS=1`, on `main`'s build launched through the
isolating launcher (§4 rule 4), keys sent as real keystrokes (`SendInput`);
record, per screen reader × surface × mode — NVDA: editor, reading view
in browse and in focus mode; Narrator: both surfaces, scan mode off and
on; JAWS if installed: both surfaces, virtual cursor on and off — whether
Ctrl+G and Ctrl+Shift+G were seen and how the reader leaves that mode
(an AT's pass-key-through is noted but is not leaving). Every cell seen,
or every unseen cell leavable for a mode where the keys are seen → the
keyboard design alone (the help names the leave step); an unseen cell in
a mode the reader cannot leave → the results list too, alongside the
text-surface keys if it is the only such cell, instead of them (the help
and HelpText name the list as the route from the text) if more; the
window route is built either way; PR 1's body carries the record and the
path, PR 2 restates it.
**PR 1** (`Refs #1112`) builds: `FindInNoteViewModel` per Markdown tab
(Query, MatchCase, WholeWord, ReplaceText, IsOpen, Mode — new enum
`FindInNoteMode { Editor, Reading }` following the tab's `IsReadingMode`
— CanReplace, CurrentIndex, TotalCount, Error, FocusRequested; Next,
Previous, Replace, ReplaceAll (only with CanReplace and a surface
`Document`), Close; `AttachSurface(IFindInNoteSurface? surface)`) over
`SearchStrategyFactory.Create(pattern, ignoreCase, matchWholeWords, SearchMode.Normal)`
run on the attached surface's `ITextSource`, with a 150 ms debounce,
manual wrap, result sets stamped with the surface's `Stamp` and an
options revision; a query or option recompute reveals the first hit at
or after the caret, a text-change recompute only recounts and
re-highlights (never moves the caret or selection); in editor mode the
current hit is kept as `TrackedHit` (new private record: `TextAnchor
Start` with `AnchorMovementType.AfterInsertion`, `TextAnchor End` with
`AnchorMovementType.BeforeInsertion`, both `SurviveDeletion = true`, from
`TextDocument.CreateAnchor`; `int Length`), set whenever a hit becomes
current, validating when `End.Offset - Start.Offset == Length` and the
live strategy's `FindNext(document, Start.Offset, Length)` returns
exactly that offset and length, kept current by a recount only while it
validates and cleared by a mode switch; in reading mode the current
match after a recount is the one the surface's selection covers exactly,
else none. `Next`, `Previous` and `ReplaceAll` flush and recompute
synchronously when either stamp is stale, then act (`ReplaceAll` in one
`BeginUpdate`/`EndUpdate` over the fresh set). `Replace` never picks its
target from a recomputation: it validates the tracked hit first — valid
→ `document.Replace(Start.Offset, Length, ReplaceText)` (one undo step),
recompute, step to the next hit; invalid → write nothing, clear the
tracked hit, recompute for the count only (caret unmoved, `CurrentIndex`
0) and speak only "Match changed; nothing replaced.", whether or not
another match survives; no tracked hit → act as `Next` and write
nothing. New `IFindInNoteSurface` (in `FindInNoteSurface.cs`): `Mode`,
`IsSearchable`, `ITextSource Text`, `object Stamp`, `(int Offset, int
Length) Selection`, `TextDocument? Document`, `Reveal(int offset, int
length)` (selection moved and brought into view, focus untouched),
`Highlight(IReadOnlyList<(int Offset, int Length)> matches, int current)`,
`ClearHighlights()`, `object CaptureRestorePoint()`, `Restore(object)`,
`ReturnFocus()`, `event Action? TextChanged`; new `EditorFindSurface(TextDocument
document, SlateTextEditor? editor)` (headless when `editor` is null;
`Stamp` the `TextDocument.Version`; `Reveal` = `editor.Select` then
`TextArea.Caret.BringCaretToView()`; restore point = caret and selection
`TextAnchor`s with `AfterInsertion` and `SurviveDeletion`, restored
clamped; `ReturnFocus` = `FocusInputOwner()`; `TextChanged` =
`TextDocument.Changed`); `FindMatchBackgroundRenderer : IBackgroundRenderer`
(`KnownLayer.Selection`, theme token `Slate.FindMatchBrush` over
`Slate.FindMatchColor` in both themes) registered while the bar is open
on the editor; `FindInNoteBar` at a new `Grid.Row="2"` of
`WorkspaceTabContentTemplate` (status to Row 3, popover overlay
`RowSpan="4"`), with new dependency properties `Editor` and
`ReadingSurface` bound by `ElementName` (the reading surface gains
`x:Name="NoteReadingSurface"`) and calling `AttachSurface` on every
`DataContext`, `IsOpen` or `Mode` change, rooted at an
`AutomationLandmarkGrid` named "Find in note" with ids `FindInNoteQuery`,
`FindInNoteCount`, `FindInNotePrevious`, `FindInNoteNext`,
`FindInNoteMatchCase`, `FindInNoteWholeWord`, `FindInNoteReplaceText`,
`FindInNoteReplace`, `FindInNoteReplaceAll`, `FindInNoteClose`,
`FindInNoteError` (the three replace controls `Collapsed` unless
`CanReplace`, so reading mode's control view is Query, Count, Previous,
Next, Match case, Whole word, Close), not modal (no `ModalSurface`
member, no `IsDialog`, no focus scope); `VaultLifecycleViewModel.FindInNoteCommand`
(the `ISlateCommandHost`) bound to a window-level Ctrl+F `KeyBinding` in
`Window.InputBindings` (bubbling, so the Base grid's `FilterCommand` and
the canvas surface's tunnelling `FilterCardsFromKey` claim the key first
— never a case in `Window_PreviewKeyDown`) and to the registrar as `host
=> host.FindInNoteCommand`, routing a Markdown tab to
`Workspace.OpenFindInNote()` (PR 1: editor mode only; in PR 1 a switch to
reading mode with the bar open closes it without moving focus) and
everything else — no workspace, no tab, a graph tab, and in PR 1 a
reading-mode note — to `ToggleSearchCommand`; the Ctrl+G route — two `KeyBinding`s in
`MainWindow.xaml`'s `Window.InputBindings` (`Key="G"`, `Modifiers="Control"`
and `"Control+Shift"`) on new `FindInNoteKeyRoute.FindNext` and
`FindPrevious` (`RoutedUICommand`s with no `InputGestures`, in new static
class `FindInNoteKeyRoute`), whose `CommandBinding`s (added by new
`FindInNoteKeyRoute.Bind(UIElement window)` from `MainWindow`'s
constructor beside `MainWindow.xaml.cs:86`) set `e.CanExecute =
SessionOwningFocus(Keyboard.FocusedElement as DependencyObject) is {
IsOpen: true }` and `e.ContinueRouting = !e.CanExecute` and execute that
session's `Next()` / `Previous()`; new `FindInNoteKeyRoute.SessionOwningFocus(DependencyObject?
focused)` walks up like `MainWindow.FindAncestorDataContext` (content
elements by their logical parent) to the first `FindInNoteBar` (its
`FindInNoteViewModel`), `SlateTextEditor` or `ReadingSurface` (its tab's
`FindInNote`), else null — so a step from the bar or the text acts on
that tab, keeps focus, speaks the count once, and with no open bar for
the focused note, or focus elsewhere, the key passes through unhandled;
registrar entries `[Ids.EditorFindNext] = host =>
host.Workspace?.FindNextInNoteCommand` and `[Ids.EditorFindPrevious] =
host => host.Workspace?.FindPreviousInNoteCommand` (new `RelayCommand`s,
CanExecute the active tab's bar open); chord rows `Reg(Ids.EditorFindInNote, "Find…", CommandSection.Editor, "Find and highlight text within the current note.", "⌘F", "Ctrl+F", ChordScope.Global)`,
`Reg(Ids.EditorFindNext, "Find Next", CommandSection.Editor, "Move to the next match of the open find bar's query.", mac: null, win: "Ctrl+G")`
and `Reg(Ids.EditorFindPrevious, "Find Previous", CommandSection.Editor, "Move to the previous match of the open find bar's query.", mac: null, win: "Ctrl+Shift+G")`
(Global, Windows-only); the bar's own key route — new
`ChordScope.FindInNote` (an enum summary like `SidebarFilter`'s; never
`ChordScope.Editor`, where `windows.editor.closePopover` already ships
Escape and the surface-row loop `ChordTableTests.cs:371-379` refuses a
second Escape row in one scope) delivered by `FindInNoteBar.Bind()` (new,
three-argument `AddChord(Key.X, ModifierKeys.Y, Func<bool>)` calls) from
the bar's tunnelling `OnPreviewKeyDown`, live only with focus inside the
open bar: Enter → Next and Shift+Enter → Previous only while the query
holds the focus (a focused button keeps its own Enter), Escape → Close
anywhere in the bar (Escape in the note's text never reaches it); surface
rows `windows.findInNote.next` ("Enter"), `windows.findInNote.previous`
("Shift+Enter") and `windows.findInNote.close` ("Escape"); no
`KeyBinding` in `FindInNoteBar.xaml`, no new arm in
`SlateTextEditor.OnPreviewKeyDown`; `ScopedProductionChords()` gains
`[ChordScope.FindInNote] = FindInNoteChords()` (new;
`CSharpSource.Load("FindInNoteBar.xaml.cs").Method("Bind")`, shaped like
`GraphChords()`); the `SharedCommandChords` entry
`["slate.bases.quickFilter | slate.canvas.filterCards | slate.editor.findInNote"]`
with its delivery reason (a Base's grid gesture and a canvas's
tunnelling handler — the canvas table's grid `FilterCommand` subscribed
to the canvas filter — claim the key before the window's bubbling
binding, which acts only on what no surface claimed: the find bar for a
note in either mode, vault search otherwise), replacing the
`slate.bases.quickFilter | slate.canvas.filterCards` entry; an Editor-menu
item "_Find…" (Windows has no Edit menu) with
`InputGestureText="{cmd:ChordText slate.editor.findInNote}"`;
`Panels/FindInNotePhrase` ("{N} of {M}", "No matches", "Wrapped to the
top", "Wrapped to the end", "Replaced {M} matches.", "Match changed;
nothing replaced.", "Waiting for the reading view.") spoken as
HostComposed Medium — typing coalesced after the debounce, each step once
at once, recounts and mode switches silent; the rationale comment at
`SlateTextEditor.cs:538`; `CommandPaletteTests.ShippedShellHost` and
`ThreadModifiers` made `internal`, the host given a fixture-note helper
and an `IInputElement` `Press` overload.
**PR 2** (`Closes #1112`; after WB-11 and WB-20 are on `main` and PR 1 has
merged) builds: in `ReadingSurface`, only new enum `ReadingContentChange
{ Replaced, Appended, CompletedInPlace }`, new `internal event
Action<ReadingContentChange>? ContentChanged` and `internal int
ContentRevision`, bumped and raised once at the end of `ApplyBuiltDocument`
(Replaced), `ClearForModelSwitch` (Replaced), `Model_BlocksAppended`
(Appended) and WB-20's `CompleteDiagrams` (CompletedInPlace, after its
`EndChange()` and any `InvalidateStructuralPeers()`), never on the rebind
fast path; `ShowsAppliedProjection` made `internal`; new `internal
FrameworkElement? DocumentRenderScope` (the `PART_ContentHost`
`ScrollViewer`'s `Content`, or a `Decorator` host's `Child`) — no peer,
builder, merge, caret or landing change; new `ReadingTextIndex` (`Build(FlowDocument)`, `ITextSource
Text` as a `StringTextSource`, `PointerAt(int, LogicalDirection)`,
`OffsetOf(TextPointer)`, `ParagraphOrdinalAt(int)`; one forward
context walk: text runs appended with their pointers, one "\n" at
non-inline element edges and `LineBreak`s, U+FFFC for an embedded
element, nothing at inline edges — so the search covers exactly the
surface's text range, never spans a block or an embedded element, and
never sees what an embedded element displays); new
`ReadingFindSurface(ReadingSurface)` (`IsSearchable` =
`ShowsAppliedProjection`, the index cached per `ContentRevision`, `Stamp`
= `ContentRevision`, `Reveal` = `Selection.Select` then
`DocumentRenderScope.BringIntoView(rect)`, restore point = the selection's
live pointers voided by a later `Replaced` change, `ReturnFocus` =
`RequestFocusLanding()`, drawn matches dropped at once on `Replaced` and
`CompletedInPlace`, kept on `Appended`, `TextChanged` raised for all
three, so the view model's debounced recount re-runs the search on every
publish, stream chunk and in-place completion, silently and without
moving the caret or selection; while `IsSearchable` is false the count
readout and any step say "Waiting for the reading view." and a query,
option change or step made then is answered once by the first recount
over an applied projection); new `FindMatchAdorner : Adorner` over `DocumentRenderScope` in
`AdornerLayer.GetAdornerLayer(renderScope)` (the scroll presenter's own,
clipped layer; no peer, not hit-test visible, not focusable), drawing
only the visible matches as 2 px rings (current 3 px) in new
`Slate.FindMatchRingBrush` over `Slate.FindMatchRingColor` (both themes),
one per line from `GetCharacterRect` mapped by
`surface.TransformToDescendant(renderScope)`, re-rendered on match
changes, `ScrollChanged` and `SizeChanged` — never
`TextRange.ApplyPropertyValue` or added `Span`s, which split runs, raise
text-changed events screen readers re-read, and change the structure
WB-11 caches and the runs WB-20 rewrites; the current match is the
`RichTextBox` selection; the reading-mode bar
(Ctrl+F opens it in reading mode; the replace controls collapsed); mode
switches with the bar open (query kept, old highlights cleared, tracked
hit cleared, restore point retaken, a silent recount — synchronous when
the new surface is searchable, else after its projection applies —
`CurrentIndex` from the new surface's selection, no caret moved); the
results list's reading rows if it shipped. **The results list** (only if
step 0 calls for it; PR 1 for the editor, PR 2 adds reading rows): a
`ListBox` `FindInNoteResults` named "Matches" in a third row of the bar,
visible while open with at least one match, over new
`ObservableCollection<FindInNoteResultRow> Results` (new record
`FindInNoteResultRow(int Index, int Offset, int Length, string Name)`),
items named "line {N}: {context}" (editor) or "paragraph {N}: {context}"
(reading), context up to 30 characters either side, whitespace
collapsed, "…" at cuts, at most eight rows tall, virtualized, native
PositionInSet/SizeOfSet; arrows move rows without moving the note's
selection; Enter on a row, delivered by the bar map's Enter arm (a
focused row → new `GoToResult(FindInNoteResultRow row)`, the query →
`Next`, else false; still one `windows.findInNote.next` row, its reason
naming both), reveals the match, makes it current and speaks "{N} of
{M}" once with focus kept on the row; tab order Query → Count → Matches
→ Previous → …; the twelfth id.
Facts, failing first — PR 1: `FindInNoteViewModelTests`:
`TypingFindsAllMatchesAndReportsOneOfM`, `NextWrapsToTheTopAndSaysSo`,
`PreviousWrapsToTheEndAndSaysSo`, `MatchCaseAndWholeWordNarrowTheMatches`,
`ReplaceReplacesTheCurrentHitAndSteps`, `ReplaceAllIsOneUndoStep`,
`ADocumentEditWhileOpenRecountsOnTheDebounce` (the recount moves neither
caret nor selection; a still-valid tracked hit stays current, a broken
one is cleared), `ReplaceWithNoCurrentHitRevealsTheNextAndWritesNothing`
("a b a" with the tracked hit cleared by editing the first "a" to "x":
Replace leaves "x b a", selects offset 4 and speaks "1 of 1"; a second
Replace writes "x b q"), `AnEmptyQueryClearsHighlightsAndCountsNothing`,
`CloseRestoresTheCaretAndSelectionTheOpenCaptured`;
`FindInNotePhraseTests` pinning every string verbatim;
`FindInNoteBarTests` (hosted shell, focus set and asserted before each
press): `CtrlFInANoteFocusesTheQueryAndSelectsAll`,
`CtrlFOnABaseTabReachesTheQuickFilterNotTheBar`,
`CtrlFOnACanvasReachesTheCanvasFilterNotTheBar` (the board holding the
keys: Ctrl+F sets the canvas's `FilterFocusRequest`, the bar stays
closed, vault search does not open),
`CtrlFWithNoNoteSurfaceOpensVaultSearch` (no tab and a graph tab — and a
reading-mode note in PR 1 only — execute `ToggleSearchCommand`),
`CtrlFOnTheWelcomeViewWithNoWorkspaceReachesVaultSearch`,
`AnEditBeforeTheDebounceNeverReplacesStaleText` (a Theory: the first hit
current, an edit inside the 150 ms window, then Replace with "q" —
*another match survives*: query "a", "a b a", `document.Replace(0, 1,
"x")`, stays exactly "x b a", `TotalCount` 1; *no match remains*: query
"a", "a b c", the same edit, stays exactly "x b c", `TotalCount` 0; *the
hit's middle changed*: query "abc", "abc d abc", `document.Replace(1, 1,
"x")`, stays exactly "axc d abc"; in every case the one line spoken is
"Match changed; nothing replaced.", never "1 of 1" or "No matches", and
the caret is where the edit left it),
`AnEditElsewhereBeforeTheDebounceStillReplacesTheTrackedHit` ("a b a",
Next to offset 4, "zz" inserted at 0 inside the window, Replace with "q"
gives exactly "zza b q", no refusal spoken),
`AnOptionChangeBeforeReplaceRecountsFirst`,
`CloseAfterAShortenedDocumentRestoresAClampedCaret`,
`EnterAndCtrlGStepNextAndShiftStepsPrevious` (focus in the query),
`CtrlGFromTheEditorTextStepsAndKeepsFocusInTheEditor` ("a b a b a",
"1 of 3", focus in the text area: Ctrl+G handled, selection offset 4,
`CurrentIndex` 2, exactly one "2 of 3" heard, focus still in the text
area, the text unchanged; Ctrl+Shift+G back to offset 0, "1 of 3" once),
`CtrlGWithTheBarClosedPassesThroughTheEditor` (never opened, and opened
then closed: both keys unhandled, nothing moved or heard, the bar
closed, no vault search), `CtrlGFromOutsideTheNoteSurfacesPassesThrough`
(the active tab's bar open, focus on a Files row, then the tab header:
unhandled, nothing moved or heard), `TheBarsEnterIsTheQueryFieldsOnly`
(Enter on a focused Replace All runs Replace All, "q b q", Ctrl+Z restores;
Enter in the editor types a newline), `EscapeInTheEditorTextLeavesTheBarOpen`
(the bar stays open; with a popover open, Escape closes the popover, not
the bar), `EscapeClosesClearsHighlightsAndReturnsFocusToTheEditor`,
`TheBarIsASiblingOfTheEditorPeerNotAChild`,
`TypingAnnouncesOnlyTheFinalCount`; `ChordTableTests`
(`CommandRows_HaveNoActiveChordCollision` with the three-command key and
a new staleness assertion that every recorded key is a live group's key,
`EveryScopeIsEitherScrapedFromProductionOrDispositioned` (the new scope
scraped), `TableCoversEveryDeclarativelyBoundChord_AndNothingStale` (the
Ctrl+F, Ctrl+G and Ctrl+Shift+G window bindings both ways; the new
scope's three rows both ways against `FindInNoteChords()`),
`FindInNoteKeysAreRowsInTheirOwnScope` (exactly the three surface rows
in the scope, unregistered with no mac chord; the two step rows
registered, `Global`, no mac chord; the G `KeyBinding`s bind
`FindInNoteKeyRoute.FindNext` / `FindPrevious`; those commands carry no
`InputGestures`; no `ChordScope.Editor` row shares the bar's chords but
`windows.editor.closePopover`'s Escape, in its own scope;
`KeyBindingChords("FindInNoteBar.xaml")` empty),
`ChordsJson_IsExactlyTheTablesProjection` regenerated);
`ThemeTokenContrastTests.EveryTextBearingPairClearsProjectApcaFloor`
with "find-match/text" (`Slate.TextColor` on `Slate.FindMatchColor`);
`WcMatrixEvidenceCensus` with the eleven ids registered; FlaUI
`ShellAccessibilityTests.FindInNote_TypingAnnouncesTheCountAndEnterMovesTheCaret`
with `AssertAxeClean(process, "find-in-note")` after text is typed and
`FindInNote_CtrlGFromTheEditorTextMovesTheSelectionAndKeepsFocus` (the
editor's Text pattern selection moves, the focused element stays the
editor). PR 2: `FindInNoteBarTests`:
`CtrlFInReadingModeOpensTheBarOverTheReadingView` (the bar opens, focus
in the query, `Mode` Reading, no vault search),
`CtrlGFromTheReadingSurfaceMovesTheSelectionAndLeavesTheDocumentAlone`
("alpha" in a heading, a list item and a link; focus on the surface:
Ctrl+G handled, `Selection.Text` "alpha" at the second match, "2 of 3"
once, focus kept; across opening, highlighting and three steps the
range text, the structural fingerprint, `ContentRevision` and the peer's
children instances are unchanged and `TextChanged` fired zero times),
`CtrlGFromALinkInTheReadingDocumentSteps` (focus on a `Hyperlink`: steps,
focus stays on the link), `CtrlGWithTheBarClosedPassesThroughTheReadingSurface`,
`EscapeInTheReadingSurfaceLeavesTheBarOpen`,
`AModeSwitchWithTheBarOpenKeepsTheQueryAndMovesTheHighlights` (Ctrl+Shift+E
with the bar open: query kept, renderer unregistered; once the projection
applies, one adorner present, `TotalCount` over the reading text, `CurrentIndex` 0 with no
reading selection covering a match, replace controls collapsed, silent;
back: adorner gone, renderer back, `CurrentIndex` 2),
`TheReadingModeBarHidesReplace` (three controls collapsed, both replace
commands unable to execute, the landmark's peer children exactly Query,
Count, Previous, Next, Match case, Whole word, Close);
`ReadingFindSurfaceTests` (new): `TheIndexCoversTheSurfaceRangeAndNothingEmbedded`
(the index equals the range text modulo whitespace and U+FFFC; a word
split across bold runs found once; the diagram failure header found; the
diagram description and math speech not; no cross-paragraph match),
`ContentChangedFiresOnceForEachContentPath` (merge and model switch
Replaced, append Appended, WB-20 completion CompletedInPlace, rebind
fast path none; `ContentRevision` up by one each),
`TheCurrentMatchIsTheSelectionAndIsBroughtIntoView` (the selection is
exactly the match and its rectangle lies in the viewport; focus
unchanged), `TheAdornerHasNoPeerAndLivesInTheSurfacesOwnLayer` (no
peer; in the render scope's layer, not the window's; not hit-test
visible or focusable; removed on clear),
`TheLoadingPlaceholderIsNeverSearched` (query "reading" over the
placeholder: "Waiting for the reading view.", `TotalCount` 0; the count
spoken once when the projection applies),
`AReadingRepublishReRunsTheSearch` (drawn matches dropped at once, then
`TotalCount` 3 after the flush, silent, caret where the merge put it),
`AnInPlaceDiagramCompletionReRunsTheSearch` (a `Failed` completion's
header counted after the flush);
`ThemeTokenContrastTests` gains "find-ring/surface" and
"find-ring/raised" (`Slate.FindMatchRingColor` on `Slate.SurfaceColor`
and `Slate.RaisedSurfaceColor`); FlaUI
`FindInNote_ReadingView_CtrlGFromTheTextMovesTheSelectionAndReplaceIsAbsent`
with `AssertAxeClean(process, "find-in-note-reading")`. With the list:
`FindInNoteResultsTests.TheResultsListNamesEachMatchByLineAndContext`
("line 2: xx alpha yy", "line 3: alpha"; List "Matches"; PositionInSet
1 and 2 of 2; reading rows "paragraph {N}: …"),
`EnterOnAResultLandsTheMatchAndSpeaksTheCountOnce` (the surface's
selection is the match, "2 of 2" once, focus on the row),
`ArrowsMoveBetweenResultsWithoutMovingTheSelection`; FlaUI
`FindInNote_ResultsListIsAListOfNamedMatches`.
Mutations to run locally: drop the fall-through arm; resolve the command
through `Workspace?.`; recompute before validating and act on the
recomputed current hit (fails every Theory case: "x b q", "No matches"
spoken, "axc d q"); validate by span only, without the `FindNext` check
(the middle-changed case writes "q d abc"); validate at the offset
captured when the hit became current instead of through the anchors
(the edit-elsewhere fact refuses); with no tracked hit, recompute and
replace the first hit (the no-current-hit fact writes on the first
press); let a text-change recompute reveal a hit (the caret moves); act
on the stamped set after an option change; restore the raw offset on
Close; record only a base/editor pair (the group's lookup fails); keep
the base/canvas pair beside the triple (the live-key assertion fails);
remove the triple (the lookup fails); deliver the window's Ctrl+F from
`Window_PreviewKeyDown` (the Base and canvas facts fail); deliver Ctrl+G
only from the bar's map (the editor-text and reading-surface facts fail);
bind the G `KeyBinding` to a plain command, or drop `ContinueRouting`
(the pass-through facts and the routed-command assertion fail);
CanExecute from the active tab without the focus walk (the
outside-the-surfaces fact fails); walk only visual parents (the link
fact fails); step by focusing the query (focus leaves the text);
announce in both `Reveal` and `Next` (two lines heard); give `FindNext`
a `KeyGesture` (the gestures assertion fails); declare
`windows.findInNote.close` in `ChordScope.Editor` (the scope fact and
the surface loop fail); drop the Ctrl+Shift+G `KeyBinding` (the Global
reverse check fails); write the previous row as "Shift+Ctrl+G" (the
forward check fails); deliver Escape through a `KeyBinding` in
`FindInNoteBar.xaml` (the empty-bindings assertion fails); run the bar's
map from a route above the editor (Enter in the editor steps the find;
Escape in the text closes the bar); let Enter act whatever holds the
focus (Enter on Replace All steps Next); highlight the reading view with
`TextRange.ApplyPropertyValue` (`TextChanged` fires, the fingerprint
changes); reveal by `CaretPosition` only (the selection is empty); adorn
the `ReadingSurface` itself (the layer is the window's); give the
adorner a peer; omit `ContentChanged` from `Model_BlocksAppended` (the
append case fails); no `ContentChanged` subscription, or a raise only
from `ApplyBuiltDocument` (the republish or completion fact fails);
search the placeholder ("1 of 1" over "Loading reading view…"); index
embedded elements' names, or drop the block separator (the index fact
fails); keep the old surface's highlighter on a mode switch, or keep
`CurrentIndex` across it (the mode-switch fact fails); disable instead
of collapse the replace controls (the peer children fail); and with the
list, name rows by context alone, focus the surface on Enter, reveal on
selection change; a pale ring colour (the contrast row fails).
Registers: `apps/slate-windows/chords.json` (regenerated with the
`FindInNote` scope's three surface rows and the three `Global` command
rows; `deliveryEvidence.commands` gains the three ids),
`scripts/generate-parity-matrix.py` (drop the override, add the two
Windows-only ids, the find-in-selection note and, in PR 2, the note that
Windows also finds in the reading view and that WB-0 files "mac: find in
note in the reading view, with ⌘G from the reading text") and
`docs/plans/18_windows_port/parity_matrix.md` regenerated with `--check`
green, a ten-cell row "Find in note (WB-22)" in
`docs/plans/18_windows_port/w_c_matrix.md` covering both modes, the ids
in `docs/plans/18_windows_port/w_c_automation_ids.json` (eleven, twelve
with `FindInNoteResults`), the `FindInNotePhrase` residue entries in
`docs/plans/18_windows_port/a11y_trigger_designations.json` and
`python -I scripts/a11y_trigger_ledger.py --write`, notes appended to
`docs/plans/28_palette_contracts.md` (non-modal landmark, P5 honoured)
and `docs/plans/25_bases_grid_contracts.md` (the Bases filter keeps
Ctrl+F on a Base), the Windows keyboard help page (Ctrl+F in both modes,
Ctrl+G and Ctrl+Shift+G from the bar and the text — or the list where it
replaced them — Enter / Shift+Enter in the query, Escape in the bar).
Done when the Windows lane and the FlaUI gate are green, the
Verification block runs clean in PowerShell (every command's
`$LASTEXITCODE` checked; `$readingShipped` and `$listShipped` read from
the tree; the discovery step throws unless each of PR 1's thirty-four
named facts — plus PR 2's fourteen when the reading index exists, plus
the list's three when it ships — and at least one `FindInNotePhraseTests`
fact are selected, and, in PR 2, at least one `ReadingViewTests`,
`ReadingDiagramTests` and `ReadingFocusTests` fact; the FlaUI step
throws unless every journey this head ships is selected), `dotnet format
--include` over the head's files is clean (it throws if one is missing),
step 0's record is in PR 1's body, and the NVDA and Narrator passes, both
modes (Ctrl+F: "Find in note, edit"; typing: "3 of 12"; Enter: "4 of 12";
Enter at the last hit: "1 of 12, wrapped to the top"; Match case check
box; Replace All: "Replaced 12 matches." in the editor, no Replace
control in the reading view; Ctrl+G from the text: the count once, focus
kept; Escape in the bar returns to the note, Escape in the text leaves
the bar open; Ctrl+F on the welcome view opens the search overlay;
Ctrl+F on a Base: "Quick filter, edit"; Ctrl+F on a canvas: the canvas
filter field, never the find bar), are in the PR bodies — each PR draft
until then. Not delivered: regex (parity), find-in-selection (unverified
on mac), search inside the reading view's embedded elements (outside the
text range caret reading covers). PR bodies: PR 1 `Refs #1112`, PR 2
`Closes #1112`. Commits: PR 1 `feat(windows): ctrl+f finds in the open
note with a landmark find bar, replace, a spoken count and ctrl+g from
the text`; PR 2 `feat(windows): find in note reaches the reading view —
matches drawn over the text, never in it, and ctrl+g from the reading
surface`.
