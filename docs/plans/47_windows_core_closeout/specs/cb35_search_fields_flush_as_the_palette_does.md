# CB-35: every search field's list is laid out in the operation that publishes it, as the palette's is

Issues: Closes #1837. Size M as planned; L as measured (Call 2 proposes a split). Spec revised after its red team (2026-11-25). Lanes: windows (unit facts on the shown shell; no gate journey). AT: an NVDA spot check, timed (owed by the row): DS-10's probe on Quick Open and search, base and head. Wave: C13, slot 3. Decision: none (the row has no decision row). Review: an Opus xhigh red team on this spec and on the diff, then Codex adversarial-review rounds until it approves, then the lead's merge judge.

Plan row, verbatim: "Quick Open, search and the five other search fields flush as the palette does; a census holds every search field over a republishing list to it".

Every `path:line` is as of `main` at b19c94ae, read on 2026-11-25. `S/` is `apps/slate-windows/src/SlateWindows/`, `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`, `X` is `S/MainWindow.xaml`. The census worker's notes (`cb35/cb35-worker-notes.md`) and the step 0 probe (`Cb35Step0Probe.cs`, never committed) with its results (`cb35/step0/probe-results.txt`) are in the principal's working notes, not committed.

## What "as the palette does" is on `main`

`S/CommandPaletteResultsPresenter.cs`, after DS-10 (#1840) and CB-34 (#2058, fce6a6e1):

1. **The rows are laid out in the operation that publishes them.** `Refresh` swaps the view and ends in `_list.UpdateLayout()` (`:122`). TSF can be granted its lock between dispatcher operations, and `TextStore.GetTextExt` then runs whatever layout and automation events it finds pending, inside the lock; a UIA event raised there waits on a client reading the field, and the two wait until UIA gives up (about 20 s a time). The record is contract 28's DS-10 entry (`docs/plans/28_palette_contracts.md:911`).
2. **The selection is shown, then laid out.** `SyncSelection` sets the list's selection, scrolls it into view and ends in `UpdateLayout()` (`:177`), so no scroll is left deferred to the render pass.
3. **No letter search.** `IsTextSearchEnabled = false` (`:47`, CB-34): WPF's letter search matched the rows' type name, so a letter moved the keys and the selection with a "Selected:" line.

## Step 0, as run

**The class, re-derived from `main`.** A WPF text field that holds the keys while a list (ListBox, TreeView, DataGrid) changes its rows in response to the text or to a background publication. A sweep of all 41 `TextBox` sites (31 in XAML, 10 built in code) and the one `TextBoxBase` subclass found ten members besides the palette, not the plan's seven: the five `SearchGlyph` fields the plan counts, Quick Open, search, and three without the glyph (Move To's filter, the canvas card and file pickers' filter, the graph inspector's name field). The other 31 sites are not members, each with a reason (the census worker's notes, section 2).

**The probe.** A probe file on the shown shell (`ShownShell`, off screen), run twice under the desktop lock (17:08 to 18:02); the two runs agree except one count (M2's Searching op, 3 then 2). Each member was made to publish the way the app does. A publication in the keystroke's own operation was read as it returned; a posted one was read in `Dispatcher.Hooks.OperationCompleted` for the operation that set the publication's marker, with nothing pumped after it. "Pending" is the palette's walker (`T/CommandPaletteTests.View.cs:76`) over the list's subtree: elements whose measure or arrange is not valid. A collapsed root reads 0, so each count was taken with its root visible. Letters were typed through `TextCompositionManager.StartComposition`, as `ALetterTypedInTheListMovesNothing` does (`T/CommandPaletteTests.TypedName.cs:1202`).

| Member | Field (`X` or file) | List | Pending as the publishing operation ends | After one Background pump | Letter typed with the keys on row 3 |
|---|---|---|---|---|---|
| Palette (control) | `CommandPaletteSearchTextBox` `:3420` | `:3458` | 0 (keystroke and posted) | selection realized and in view | nothing moves; no line |
| M1 Quick Open | `QuickSwitcherSearchTextBox` `:3008` | `QuickSwitcherResultsList` `:3029` | 1 (the keystroke's clear); 1 (posted `ApplyRanked`); 2 (silent re-rank) | a re-rank keeping row 30 leaves it unrealized, after settling too | "q" moves keys and selection to row 0: "Selected: topic-00" |
| M2 Search | `SearchOverlaySearchTextBox` `:3134` | `SearchOverlayResults` `:3197` | 1 to 4 on the list and 2 to 5 on its grid (posted `Publish`); from Idle a real deferred scroll (`ItemsControl.OnBringItemIntoView` at Loaded, the list's generator not yet run); over results only `VirtualizingStackPanel`'s cleanup lambda, which is not a scroll (the red team's IL read) | the deferred scroll has run | "s" moves keys and selection: "Selected: topic-00.md" |
| M3 Files filter | `SidebarFilterTextBox` `:1275` | `FilterResultsList` `:1474` | 1 (posted `ApplyFilterOutcome`) | no selection is pushed | not measured |
| M4 Bibliography | `BibliographySearchBox` `:2264` | `BibliographyEntriesGrid` `:2362` | 3 (the keystroke's `Bind`) | current cell restored and in view | not measured (grid type-ahead) |
| M5 Bases quick filter | `BaseSurfaceView._quickFilter` (`Bases/BaseSurfaceView.cs:121`) | `BaseTabGrid`, `BaseTabList` | table 3, list 1 (posted) | the cleared filter leaves the list's selection unrealized | not measured |
| M6 Canvas filter | `CanvasSurfaceView._filterField` (`Canvas/CanvasSurfaceView.cs:162`) | `CanvasOutlineView`, `CanvasTableView` | outline 1, table 3, twice per keystroke (the setter's publication and the posted answer) | rows in view | not measured |
| M7 Graph filter | `GraphSurfaceView._filterField` (`Graph/GraphSurfaceView.cs:99`) | `GraphTableView` grid | 2 (posted) | the cleared filter's re-seated row 45 unrealized, after settling too | not measured |
| M8 Graph inspector name | `GraphInspectorView._nameQuery` (`Graph/GraphInspectorView.cs:76`) | the same grid | as M7 | as M7 | not measured |
| M9 Move To | `MoveToFilterTextBox` `:4801` | `MoveToList` `:4835` | 1 (opening, each folder page, each keystroke's `RebuildRows`) | after the arrows scrolled to row 20, a keystroke leaves row 0 selected out of view or unrealized: a publication never scrolls | "s" moves keys and selection: "Selected: dir-00" |
| M10 Canvas card and file pickers | `CanvasCardPickerFilterBox` `:4955` | the list at `:4960` (`CanvasCardPickerRows`) | 1 (the keystroke) | a filter that drops the selected row leaves no selection | "c" moves keys and selection, silently |

Every member's list leaves layout pending as its publication ends; the palette's leaves none. Every member `ListBox` has `IsTextSearchEnabled` on, WPF's default; the `TreeView` and `DataGrid` members have it off by default (measured by reflection) and use their own type-ahead.

**Measured beside the class.**
- **Quick Open never shows its selection.** Nothing scrolls it (the census found no `ScrollIntoView` for it). Down from the top: presses 1 to 8 stay in view, 9 and 10 are realized but out of view, 11 to 30 are not realized at all, each arrow leaving up to 14 elements pending. A sighted reader loses the highlight. (WPF keeps item peers for unrealized rows, so a screen reader still finds the selected item; what it lacks is a realized row on screen.)
- **The card and file pickers lose their selection when a filter drops the selected row.** Both view models set `SelectedRow` before they raise `Rows` (`Canvas/CanvasPromptViewModel.cs:931-941`, `Canvas/CanvasVaultFilePickerViewModel.cs:114-125`), and the list's two-way `SelectedItem` (`X:4961`) writes null back when its items change. Confirm then has nothing to pick.
- **The palette publishes at Background priority; every other posted member at Normal.** Recorded; the flush does not depend on it.
- **Search's first results leave a real scroll deferred**: `ListBox.ScrollIntoView` defers `OnBringItemIntoView` to Loaded while its generator has not generated (red team, IL). Over results the pending Loaded operation is `VirtualizingStackPanel`'s cleanup (`_bringIntoViewContainer = null`), not a scroll; the scroll itself sits in the layout queue.

**Not measured in step 0.** NVDA (the timed spot check is owed at delivery, Call 9); the letter row for M3 to M8; M3's automatic re-run after a tree publication (`FilesSidebarViewModel.TreeOperations.cs:676`), search's error state and tag chip, Move To's failed and truncated states; whether a stall trips on any member under NVDA (the walker sees layout validity, not pending automation events or TSF's lock).

## Drift check

| Claim | On `main` b19c94ae |
|---|---|
| "the five other search fields" (§5 `:644`), "five more search fields with the shape #1837 names" (§2.3 `:220-222`), §9 `:960`, §11 `:1081-1082` | holds for the `SearchGlyph` fields; wrong as a census of the class: eight members besides Quick Open and search (M3 to M10) |
| §6 `:835`, `FilesSidebarViewModel*.cs` lists CB-35 for "the filter field" | holds (`FilesSidebarViewModel.Filter.cs:184-282`, `:387-407`, `:516-562`) |
| §6 `:837`, the `MainWindow` files list CB-35 | holds |
| files CB-35 needs that no §6 row gives it | `QuickSwitcherViewModel.cs` (no row at all; CB-45b's too), `Search/SearchOverlayViewModel.cs`, `Grids/AccessibleDataGrid.cs`, `Bases/BaseSurfaceView.cs`, `Graph/GraphInspectorView.cs`, `Graph/GraphTableView.cs`, `Canvas/CanvasOutlineView.cs`, `Canvas/CanvasTableView.cs`, `Canvas/CanvasSurfaceView.cs`, `FileManagement/MoveToPickerViewModel.cs`, `Canvas/CanvasPromptViewModel.cs`, `Canvas/CanvasVaultFilePickerViewModel.cs` (Call 10) |
| reading 07 `:151`: presenter `:111`, `:166`; record `28_palette_contracts.md:855` | moved: `:122`, `:177`; `:911` |
| reading 07 `:152`: `ApplyRanked` `:523-551`, `Results` `:96`, list `X:2984-2994`, field `:2963` | moved: `:526-558`, `:99`, `X:3029-3039`, `:3008` |
| reading 07 `:153`: `PublishResults` `:847-853`, list `X:3152-3161`, field `:3089`, scroll `MainWindow.Search.cs:286-288` | moved: `:846-873`, `X:3197-3206`, `:3134`, `:287-295` |
| reading 07 `:156`: fix sites include "the Quick Open partial of MainWindow" | wrong: no such partial; Quick Open's code-behind is `S/MainWindow.xaml.cs:855-915` and `:2009-2060` |
| reading 07 `:156`: flush "again after the selection's scroll" in each surface | wrong for Quick Open: it has no scroll (measured above) |
| reading 07 `:336`: field lines `:1259`, `:2247`, palette `:3382` | moved: `:1275`, `:2264`, `:3420` |
| the CB-45 spec's `QuickSwitcherViewModel` lines (`:433-435`, `:527-545`, `:110-124`) and `TreeOperations.cs:640` | moved about three lines: `:436-438`, `:526-558`, `:113-127`, `:676` |

## What the hazard is, and what a settle has to reach

The red team read WPF 10.0.15's IL. `TextStore.ITextStoreACP.GetTextExt` begins with `UiScope.UpdateLayout()`, and `ContextLayoutManager.UpdateLayout` is dispatcher-wide: it loops while anything is dirty or post-layout events are queued, and fires the queued automation events inside that loop. So what TSF finds pending is the whole dispatcher's layout queue, not one list's subtree, and a settle has to run after the last change of its operation. Step 0's walker over the list's subtree is a partial stand-in; the facts below read the global queue.

One more boundary the palette never meets. The palette's text-change operation only schedules a rank; its rows are published, and laid out, in a later posted operation. Four list members publish inside the text change itself (M1's clear, M3's swap and empty-query clear, M9's `RebuildRows`, M10's `Filter`; M4 and M6 in CB-35b). For text that TSF delivers in an edit session (an IME, dictation, the emoji panel), the red team's IL read of `TextStore.GrantLockWorker` puts the change that drives the binding (`Selection.EndChange()`) after the sink's `OnLockGranted` and after `_lockFlags` is cleared, but still nested inside TSF's `RequestLock` call. Whether TSF counts that as inside its lock is not established. A settle there could fire automation events inside the lock, which is the stall itself. Step 0b settles it before the design does (Call 3).

## Design

1. **One helper, `PublishedList`** (new `S/PublishedList.cs`, static). `Settle(DispatcherObject anchor)` runs the dispatcher-wide `UpdateLayout`, guarded by `IsLoaded` on its anchor as `AccessibleDataGrid.ApplySort` guards its flush (`Grids/AccessibleDataGrid.cs:1144-1147`). `Show(ListBox list, object? row)` is the palette's order: settle, scroll the row into view, settle (red team M-C; the palette's `Refresh` lays out before `SyncSelection` scrolls, `CommandPaletteResultsPresenter.cs:122` then `:177`). The palette's presenter is not edited (CB-38's next).
2. **Every posted publication settles at the end of its posted delegate, not inside a helper it calls** (M-A):
   - **M1 Quick Open.** `QuickSwitcherViewModel` raises `RowsPublished` last in the delegate its rank posts (`:481-492`), after `ApplyRanked` (`:526-558`) and after the ranking-failure path (`:499-522`, which changes `ResultSummary`). CB-45b (C16) edits `ApplyRanked`, `ScheduleRefresh` and `ApplyFileChanges`, and must keep the raise last after its own selection lines; this spec names it for CB-45b.
   - **M2 Search.** `SearchOverlayViewModel` raises it last in the delegate the search posts (`:786-788`), after `PublishResults` and its `PublishSummary` (`:870-872`), on zero results as on results, and after `PublishError` (`:875-889`).
   - **M3 Files filter.** `FilesSidebarViewModel` raises it last in the posted apply (`:387-407`) after `ApplyFilterOutcome` and its status writes. Never from `ScheduleFilter`: the tree publication calls `ScheduleFilter(automatic: true)` in the middle of its own operation (`FilesSidebarViewModel.TreeOperations.cs:676`, before `:681-686`'s selection reconciliation), and a settle there lands on the Files tree's landing path (M-B).
   - **M9 Move To.** `MoveToPickerViewModel` raises it last in `RebuildRows` (after `SelectedRow`, `:349-352`) and last in `PublishFolders` (`:158-168`).
   - **M10 Pickers.** `ICanvasPickerSheet` gains the event (it has none today, `Canvas/CanvasVaultFilePickerViewModel.cs:15-33`), raised last in each `Filter`.
   - The view side handles each with `PublishedList.Settle`, or `Show` when the publication changed the selection (M-I: a background page, a silent re-rank or the automatic filter run settles without scrolling unless the selection moved, so a reader scrolling Move To during loading keeps the view).
3. **Text-change publications** (M1's clear, M3's swap and clear, M9, M10) settle at the true end of their setter, after its status writes (`:84`, `:213`, `:267` for M3). Step 0b (below) measured that plain typing reaches the setter outside any TSF lock and outside any dispatcher operation, and that text the emoji panel inserts reaches it after TSF's edit session has released the lock (`_lockFlags` 0), still nested in TSF's `RequestLock` from `GetMessage`; with no settle, the setter's new rows were then laid out about 2 ms later inside a TSF read lock (`GetTextExt` running `UpdateLayout`, DS-10's shape). A settle at the end of the setter lays them out before that. Moving the publication to a posted operation (the alternative) would race the TextView's own Normal-priority update, which is where that read lock was taken.
4. **The arrows show the selection on the route that runs.** The window's tunnelling handler takes Quick Open's and search's arrows first (`MainWindow.xaml.cs:1097`, `:1377-1384`, `:1705-1717`; `MainWindow.Search.cs:196-203`), so the field handlers the first draft named (`:2017-2026`) never see them (M-E). `Show` runs after the selection move on that route, for Quick Open (fixing "Quick Open never shows its selection"), search and Move To (`MainWindow.MoveTo.cs:136-157`). The field-level arrow branches that never run are deleted in this PR (dead code in files it changes).
5. **No letter search where the rows' text is a type or record name** (Call 4): `IsTextSearchEnabled="False"` on the lists at `X:3029`, `:3197`, `:4835`, `:4960`. The Files filter results and the Bases list keep theirs.
6. **The pickers keep their selection through a filter** (Call 6): both view models raise `Rows` before they set and raise `SelectedRow`.
7. Nothing else: no sentence, no `A11yEvent` construction site, no FFI, no core change. A letter on the four lists stops producing its "Selected: …" line (Call 4).

What a reader gets. No publication of a search field's list leaves layout pending for TSF to run inside its lock. (Other publications, a status line, a watcher's tree update, a timer, can still leave layout pending under any focused field; that residual is recorded, not fixed.) Quick Open, search and Move To show the row the arrows select. A letter typed with the keys on one of the four lists moves nothing. A picker keeps a selection while its filter narrows.

## Facts (each red on `main`'s product code first)

Home: `T/SearchFieldPublicationTests.cs`, the census in `T/Censuses/SearchFieldCensus.cs`, on the shown shell under the desktop lock. A shared reader `T/Support/PendingLayout.cs` gives two answers: the global one, `ContextLayoutManager.From(dispatcher)`'s `hasDirtiness` and `_firePostLayoutEvents` by reflection (a pinned premise fact fails loudly if WPF renames them), and the palette's subtree walker (`CommandPaletteTests.View.cs:76` then calls it).

| # | Fact | Pins | Red on `main` by |
|---|---|---|---|
| 1-5 | `{Member}LeavesNothingPendingWhenItPublishes` for M1, M2, M3, M9, M10 | every publishing operation of the member (each kind step 0 drove, plus M2's zero-result and error publications, M1's failed rank, M9's folder pages), read at the end of that operation with nothing pumped after it (`Dispatcher.Hooks.OperationCompleted`, as step 0 read): the global queue clean, and the list's subtree clean | step 0's counts |
| 6 | `{Member}ShowsTheRowTheArrowsSelect` for M1, M2, M9 | each arrow press through the window route: the selected row realized and inside the viewport, and the global queue clean at the end of the key's handling | M1 presses 9 and later; M1's 14 and M9's 15 pending per press |
| 7 | `QuickOpenKeepsAReRankedSelectionInView` | a silent re-rank keeping row 30: realized and in view | unrealized |
| 8 | `SearchsFirstResultsLeaveNoScrollDeferred` | from a fresh overlay whose list has never generated, the first results: the selected row realized right after the publication, as DS-10's fact checks (`CommandPaletteTests.View.cs:62-66`), and no `ItemsControl.OnBringItemIntoView` pending | the Loaded-deferred scroll |
| 9 | `MoveToKeepsTheViewWhileFoldersLoad` | a reader scrolled to row 20 while pages land: the view does not move; a keystroke's publication then shows its selection | `Show` on every page |
| 10 | `ALetterOnASearchListMovesNothing` (M1, M2, M9, M10) | keys and selection stay on row 3; no line | the letters measured |
| 11 | `APickerKeepsASelectionWhenItsFilterDropsTheRow` (both pickers) | the view model's choice survives a filter that drops the selected row, and Confirm picks it | null |
| 12 | `SearchFieldCensus` | keyed by type, not by tag: every `TextBoxBase` in the XAML files and in code, and every control template part that hosts one (the `DatePickerTextBox` at `WorkspaceTemplates.xaml:440` today; an editable `ComboBox`'s `PART_EditableTextBox` if one appears), is in one table: a member with its list and the names of its facts, or exempt with its reason. The definition the table applies: the field's text filters or selects a list, or the field holds the keys while that same list republishes in the background. A new site, a stale row or a named fact that does not exist fails it. M4 to M8 and the new M11 and M12 are rows pointing at CB-35b's issue (not dated, so a slipped CB-35b does not turn `main` red) | its red phase: a new unclassified field, seen red once and removed |

Mutations, each seen red and reverted: each member's raise moved back inside its helper (`PublishResults`, `ApplyFilterOutcome`): facts 1 to 5 red on the global answer while the subtree answer stays green, which is the reason for the global reader; each settle removed; `Show` without its first settle: fact 8 red; the arrow `Show` removed: fact 6 red; `Show` on background pages: fact 9 red; one list's text search back on: fact 10 red; the pickers' raise order reverted: fact 11 red; a census row deleted: fact 12 red.

The AGENTS.md republish rule: Quick Open's keys survive a silent re-rank (`T/QuickSwitcherRescanLandingTests.cs:27`) and the Files filter list keeps its keys through a tree publication (`T/FilesRegionLandingTests.cs:394`); both are run, and fact 9 adds Move To's.

## Files and overlap

PR 1 (non-test): `S/PublishedList.cs` (new), `S/MainWindow.xaml` (four `IsTextSearchEnabled`, one `x:Name` for the pickers' list), `S/MainWindow.xaml.cs` (Quick Open, the Files filter list and the pickers' handlers), `S/MainWindow.Search.cs`, `S/MainWindow.MoveTo.cs`, `S/QuickSwitcherViewModel.cs`, `S/Search/SearchOverlayViewModel.cs`, `S/FilesSidebarViewModel.Filter.cs`, `S/FileManagement/MoveToPickerViewModel.cs`, `S/Canvas/CanvasPromptViewModel.cs`, `S/Canvas/CanvasVaultFilePickerViewModel.cs`: eleven. Step 0b's answer B adds no file.

CB-35b (non-test): `S/Grids/AccessibleDataGrid.cs`, `S/Bases/BaseSurfaceView.cs`, `S/Graph/GraphTableView.cs`, `S/Graph/GraphInspectorView.cs`, `S/Canvas/CanvasOutlineView.cs`, `S/Canvas/CanvasTableView.cs`, `S/Canvas/CanvasSurfaceView.cs`, `S/MainWindow.Properties.cs` (Bulk Rename), `S/Panels/BibliographyViewModel.cs` if its filter moves under answer B.

- `MainWindow*`: one chunk a wave (plan `:722`); CB-35 is C13's.
- `FilesSidebarViewModel*.cs`: CB-35's row (§6 `:835`). CB-40 (the Files tree redesign) will meet `RowsPublished` in `FilesSidebarViewModel.Filter.cs`, as CB-45b meets it in Quick Open.
- `QuickSwitcherViewModel.cs`: CB-45b (C16).
- `Bases/BaseSurfaceView.cs` and `Graph/GraphInspectorView.cs` are in §6 `:836`'s row; CB-35b joins it.
- CB-36a edits only `Canvas/CanvasRendererView.cs` and `CanvasRendererPeers.cs` (its spec; §6 `:842`): no file shared with either PR.
- `CommandPaletteResultsPresenter.cs` (CB-38's next) is not edited; `T/CommandPaletteTests.View.cs` calls the lifted walker (one line).
- No ledger, parity matrix or census-path change. No Mac file.

## Screen-reader phrases owed

**NVDA, timed (the row's spot check), base and head.** DS-10's probe (contract 28): after a visit to Settings, open the surface and type a query, timing a UIA read of the field's Value from another process; the open's first publication is a probe point too (DS-10's own trigger). Quick Open and search, at least 34 runs a surface on head (DS-10's count; 0 of 20 would bound the stall rate only below about 14% at 95% confidence) and as many on base as the session allows; a read over 5 s is a stall. Then by ear, base and head:

| Case | Base | Head (expected) |
|---|---|---|
| Quick Open, Down nine times | the ninth row announced; its highlight off screen | the same speech; the row in view |
| Quick Open, the keys on a row, "q" | "Selected: …" for the first row (measured in the view model) | nothing |
| Search, the keys on a row, "s" | "Selected: …" (measured in the view model) | nothing |
| Move To, the keys on a row, "s" | "Selected: …" (measured in the view model) | nothing |
| A picker, a filter that drops the selected row, then Enter | nothing picked (by reading `Confirm`) | the chosen row picked |
| Typing in each field | as base | as base |

## Contract records

Contract 28's DS-10 entry gains one line naming CB-35's members and its global reader. Search and Quick Open have no contract that records their publication order; the PR says so and adds none.

## #1954 and #1955

- #1954: none.
- #1955 lines (or CB-35b's, if Call 2 is taken): the graph table's current row re-seated after a cleared filter is never scrolled into view (M7, measured); the Bases grid does not push its kept row back as the current cell (M5, measured); `FilterResultsList` and `FilesTree` use recycling virtualization, which `X:3454-3456` names as dotnet/wpf #8528's UIA crash class (an observation); WPF's type-name letter search on lists outside this class (CB-34's class across the app); the residual: any TSF field holding the keys while an unrelated list republishes (the rename boxes over the Files tree, Quick Open over a watcher-driven panel).

## Calls for the lead

1. **The members.** Twelve, not seven: the census and the red team found M8 (graph inspector name), M9 (Move To), M10 (the pickers), M11 (the graph inspector's group query, `Graph/GraphInspectorView.cs:535-545`, which republishes the same table through a new epoch) and M12 (Bulk Rename's key fields, `X:3733`, `:3757`, whose Enter rebinds the preview grid under the field). This grows the plan's first cut (§11 `:1081-1082`, "CB-35's five extra fields"). Recommended: all twelve in the census. Cost: five more members than the row counts.
2. **Split into CB-35 and CB-35b.** About twenty non-test files in all: L, not M. (A) PR 1: the helper, the census, the five list members (M1, M2, M3, M9, M10), Calls 3 to 6; CB-35b (a new issue, a later free slot): the grids and the canvas tree (M4 to M8, M11, M12), whose census rows point at CB-35b's issue. (B) One PR. Recommended: A. The two halves have different shapes (a view-model signal with a code-behind settle; a view class's end), and a settle in `AccessibleDataGrid.Bind` reaches every grid host (the reading view's tables, `ReadingTableGrid.cs:102`; bulk rename; the citations grids; dashboards), which wants its own review. Cost: a second PR and slot; the bibliography's search box, the first of these an NVDA user meets in the right pane, keeps its pending layout until CB-35b lands.
3. **Step 0b, run: settle in the setter.** A standalone WPF probe (WPF 10.0.15, en-US, under the lock, no screen reader; in the principal's working notes, not committed) read `TextStore._lockFlags` and the stack in the field's `TextChanged` and in its binding's setter. Plain typing (12 keystrokes): lock 0, no TSF frame, no dispatcher operation; a UIA reader's 320 Value reads were all under 2 ms. The emoji panel (2 inserts): the setter ran after the edit session, lock 0, nested in TSF's `RequestLock`; without a settle the rows were laid out 2 ms later inside a TSF read lock. Recommended: Design 3 as written (settle at the end of the setter). Not measured: a run with the settle in place, and whether an automation event raised with TSF's `RequestLock` on the stack can wait on a UIA client; the timed NVDA check (Call 9) covers typing, not the emoji panel. Cost: none beyond the design.
4. **No letter search on lists whose rows' text is a type or record name** (M1, M2, M9, M10). "As the palette does" includes it since CB-34, and step 0 measured the defect on these four. This departs from the plan lead's note that CB-35's Quick Open change "stays to the flush": the change is in `MainWindow.xaml`, not in `QuickSwitcherViewModel.cs`. Recommended: off on the four; kept on the Files filter results and the Bases list. Cost: a letter on those lists does nothing, where it used to jump with a "Selected:" line.
5. **Quick Open, search and Move To show the arrows' selection (found and measured for Quick Open).** Recommended: fixed here on the window's key route (Design 4), with the dead field-level arrow branches deleted. Also a departure from "stays to the flush", in MainWindow files. Cost: fact 6.
6. **The pickers' selection through a filter (found, measured).** Recommended: fixed here, Design 6. Cost: fact 11.
7. **Visibility flips** (search's Searching state, the Files tree/list swap) are in scope through Design 2 and 3, since they are changes of the same operations. Recommended: yes.
8. **The residual is recorded, not fixed** (#1955 line): any TSF field over an unrelated republishing list. Recommended: yes; a general fix would settle every dispatcher operation, which is a different design.
9. **The timed NVDA spot check**, at least 34 runs a surface on head, with base for comparison, plus the by-ear rows. Recommended: yes, about an hour of locked NVDA time. Cost: one long session.
10. **§6 rows.** `QuickSwitcherViewModel.cs`, `Search/SearchOverlayViewModel.cs`, `FileManagement/MoveToPickerViewModel.cs` and the two canvas picker view models have no CB-35 row; CB-35b joins `:836`'s row. Recommended: the lead's next docs PR. Cost: none here.

## Red team record

An Opus xhigh red team read this spec, the census notes, the step 0 probe and its results, main's code and WPF 10.0.15's IL (no window, no lock, no build). It found no blocker, 11 majors and 14 minors, all folded in:

- **M-A, call sites not last in their operation; facts that could not see it.** Raises moved to the end of each posted delegate (Design 2), the global reader (Facts), and the mutation that shows why.
- **M-B, a settle inside `ScheduleFilter` on every tree publication.** Never from `ScheduleFilter` (Design 2, M3).
- **M-C, `Show`'s order.** Settle, scroll, settle (Design 1); fact 8 from a never-generated list.
- **M-D, the "second deferred scroll" is a cleanup lambda.** Step 0's text corrected; fact 8 matches `OnBringItemIntoView` only, plus DS-10's realized check.
- **M-E, the arrows' route.** Design 4 on the window route; dead branches deleted.
- **M-F, no arrow leg.** Fact 6.
- **M-G, members missing; exemptions not reviewable.** M11 and M12 added; the census keyed by type with a stated definition; the residual recorded (Call 8); the census worker's exemptions are listed with reasons in its notes (section "Non-members") and become the census table.
- **M-H, overstated claim.** "What a reader gets" now says "no publication of a search field's list" and names the residual.
- **M-I, Move To's settle point and scrolling on background pages.** Raise after `SelectedRow`; background publications settle without scrolling (Design 2); fact 9. The cost of a settle per folder page on a 50,000-folder load is measured in the build.
- **M-J, settles inside text-change operations.** Step 0b measured it (Call 3); Design 3 settles in the setter.
- **M-K, template-hosted fields.** The census keyed by type, template parts included.
- **Minors.** The split's reason corrected (not CB-36's files; `Bind`'s reach) (1); §11's first cut named (2); the departures from "stays to the flush" named (3); the file lists made consistent, `MoveToPickerViewModel.cs` added, `ICanvasPickerSheet`'s new event named (4); Search's "skip while publishing" replaced by the key route (5); outer scroll viewers: `Show` runs only on a selection change, never on a keystroke that keeps the selection, and the build checks Move To and the pickers at 200% text size (6); N of at least 34 and the open's first publication (7); four unsupported claims corrected in step 0 and the phrase table (8); contract records (9); the republish rule's facts named (10); CB-40 named (11); CB-35b's own issue and undated census rows (12, 13); two more #1955 lines (14).

## What delivery changed

Merged as #2077 (squash 998db5dc, 2026-11-26), head c8dfd788. Closes #1837. PR 1 of 2: CB-35b is #2070.

- **The design held:** `PublishedList` settles at the end of each publishing operation, the view models raise the signal last, and the facts read WPF's whole layout queue. Mutation (a) proved the global reader's point: a raise left inside a helper was red on the whole queue while the list's subtree was clean.
- **Publishing paths the spec did not list,** found by the red team, Codex and the judge, each with a fact red first:
  - Quick Open's open and the reader's close;
  - Quick Open's refresh starts (a file change while open, a rescan, a feed page);
  - search's open, close, tag-scope change and Clear recent searches;
  - the Files filter's failure post and tag activation;
  - the canvas pickers' open (settled from an observer subscribed behind WPF's own binding listener);
  - the reader's cancels of Move To and the pickers.
- **Spec claims that were wrong:**
  - "Settle when the selection moved": a silent Quick Open re-rank first always showed its row, which pulled a reader's own scroll back. It now shows only when the kept row moved.
  - `Show` for publications: at twice the text size it scrolled Move To's sheet past its field, at the open (to 67, where main stays at 0) and at each keystroke. Publications now use `ShowInside`, which scrolls the list only; the arrows keep `Show`, so the sheet follows them as on main.
  - "The keystroke leaves nothing pending": the field's own relayout and its caret's scroll of the viewers around it stay pending after the setter returns, as in the palette's field. The facts allow exactly those.
  - The spec said four lists keep or lose letter search; the judge found the Files filter's list had it too. It is off on five lists.
- **Not taken, by design:** a close that a landing follows; a cancel under another open modal surface; the silent closes other surfaces make while clearing the way.
- **Census:** `EverySearchFieldIsClassified` covers twelve members by type, including grid text columns, editable ComboBoxes through any route, and XAML namespaces mapped by URI. Each member's facts must exercise it.
- **NVDA:** 0 stalls in 34 a surface on head and 0 in 68 on base. The positive control (the palette with DS-10's flushes removed) also gave 0 in 20, so the probe cannot see the stall class. The by-ear rows all behave as expected on head.
- **Reviews:** an Opus xhigh red team (3 majors, 11 minors); Codex rounds 1 and 2 needs-attention, round 3 approve; the merge judge 0/0/4/4, its delta taken. The whole unit project ran once on f8c4241e, and its one failure (FocusedRowCensus) was fixed in ae949271.
- **Count:** found 15 / fixed here 5 / deferred 10 / filed 1 (#2070). #2057 was commented for Quick Open and search.
- **Not delivered:**
  - IME composition, touch-keyboard autocorrect and dictation against a TSF write lock;
  - the probe's sensitivity;
  - the vault file picker by ear;
  - Ctrl+O in a fact;
  - the 50,000-folder settle cost.
