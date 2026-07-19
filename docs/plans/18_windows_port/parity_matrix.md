# Milestone W parity matrix (§W-F row-level checklist)

Generated 2026-07-19 at `98934d9` by `scripts/generate-parity-matrix.py` (W0-4, #716). **Re-runnable:** matrix drift = re-run, diff, re-triage (program §moving-target). Every row is burned down by its consuming W issue; §W-F gates close-out on zero unshipped/unwaived rows.

## Entry-criteria snapshot (w0_spec §W0-4 item 3)

Recorded 2026-07-19 (the W0 unpark owner call; program §Entry criteria gate snapshot and the GH milestone description carry the same record):

1. **Milestone T residual closed** — GH milestone 20 closed. ✔
2. **Milestone P shipped** with the graph's canonical accessible textual representation in Rust — GH milestone 16 closed. ✔
3. **Queue state (owner call)** — shipped at snapshot: the pre-W core program plus Milestones N (Bases), O (local history), P (graph), Q (commands), T (canvas), U (UI parity), and the FL files-sidebar program's shipped majority (GH milestone 31: 18 closed / 4 open). **Not shipped:** V, X, XD, E, PD (open), R and S (unstarted) — their rows drop below. The owner directed execution of the complete W0 set 2026-07-19; W1–W8 remain parked pending the full-milestone unpark.
4. **W0.5 canonicalization landed** — #717/#718/#719 closed. ✔
5. **W0-1 binding spike concluded** — #714 closed; `uniffi-bindgen-cs` per w0_spec §Decision. ✔

## §W-B keystroke budgets (w0_spec §W0-4 item 2)

Pinned from the then-current `BENCHMARKS.md` mac baselines — the #407 rope-native windowed-highlight rows (`doc_buffer_keystroke`, Apple M5 Pro reference box) and the #375 Swift end-to-end row for marshalling context — plus an explicit marshalling allowance:

| fixture | mac core p50 (#407/#404) | marshalling allowance | pinned Windows p50 budget |
|---|---|---|---|
| 100 KB | 86.7 µs (Slice B row; #407 improves it further) | +250 µs | **≤ 0.5 ms** |
| 1 MB | 80.7 µs | +250 µs | **≤ 0.5 ms** |
| 8 MB | 244.7 µs | +250 µs | **≤ 1.0 ms** |

**Allowance rationale (not "same as mac"):** the W0-1 spike measured the uniffi `apply_edit` round-trip at ~112 µs/edit in a **debug** build (raw P/Invoke 101 µs — the generator's own overhead is ~11 µs); release-build marshalling is strictly cheaper, so 250 µs is >2× the debug-measured whole-call cost. Budgets are rounded up to absorb CI-runner-class variance vs the mac reference box; W8-5 measures with BenchmarkDotNet on the pinned runner class and records actuals in `BENCHMARKS.md`. **Flatness gate:** p50(8 MB) ≤ 4× p50(1 MB) — the mac profile is ~3× (245 µs vs 81 µs); no size-correlated growth beyond it.

## Command inventory

181 stable command ids from the `SlateCommandID` catalog (drift-test-enforced; chords from the registration chord tables — blank chord = palette/menu-only or focus-scoped by design). Windows chord mapping is by platform convention (⌘→Ctrl, ⌥→Alt; decision 12), declared in one table in W5-1.

| command id | mac chord | consuming W issue | status |
|---|---|---|---|
| `slate.bases.builder.addCondition` | — | #738 (W4-6) | pending |
| `slate.bases.builder.addGroup` | — | #738 (W4-6) | pending |
| `slate.bases.builder.editCondition` | — | #738 (W4-6) | pending |
| `slate.bases.builder.removeCondition` | — | #738 (W4-6) | pending |
| `slate.bases.copyLink` | — | #738 (W4-6) | pending |
| `slate.bases.copyMarkdown` | — | #738 (W4-6) | pending |
| `slate.bases.editProperty` | — | #738 (W4-6) | pending |
| `slate.bases.editViewFilters` | — | #738 (W4-6) | pending |
| `slate.bases.exportCsv` | — | #738 (W4-6) | pending |
| `slate.bases.exportMarkdown` | — | #738 (W4-6) | pending |
| `slate.bases.newQuery` | — | #738 (W4-6) | pending |
| `slate.bases.nextView` | — | #738 (W4-6) | pending |
| `slate.bases.openRow` | — | #738 (W4-6) | pending |
| `slate.bases.openViewSwitcher` | — | #738 (W4-6) | pending |
| `slate.bases.previousView` | — | #738 (W4-6) | pending |
| `slate.bases.quickFilter` | — | #738 (W4-6) | pending |
| `slate.bases.refresh` | — | #738 (W4-6) | pending |
| `slate.bases.resultsPopover` | — | #738 (W4-6) | pending |
| `slate.bases.saveSortToView` | — | #738 (W4-6) | pending |
| `slate.bases.savedQuery.run.<dynamic>` | — | #738 (W4-6) | pending |
| `slate.bases.showBacklinks` | — | #738 (W4-6) | pending |
| `slate.bases.sortByColumn` | — | #738 (W4-6) | pending |
| `slate.bases.viewAsList` | — | #738 (W4-6) | pending |
| `slate.bases.viewAsTable` | — | #738 (W4-6) | pending |
| `slate.bases.whereAmI` | — | #738 (W4-6) | pending |
| `slate.canvas.actualSize` | — | #745 (W6-1) | pending |
| `slate.canvas.addLink` | — | #745 (W6-1) | pending |
| `slate.canvas.addMedia` | — | #745 (W6-1) | pending |
| `slate.canvas.addNote` | — | #745 (W6-1) | pending |
| `slate.canvas.alignWith` | — | #745 (W6-1) | pending |
| `slate.canvas.cancelMode` | — | #745 (W6-1) | pending |
| `slate.canvas.clearColor` | — | #745 (W6-1) | pending |
| `slate.canvas.clearFilter` | — | #745 (W6-1) | pending |
| `slate.canvas.clearMarks` | — | #745 (W6-1) | pending |
| `slate.canvas.commitMode` | — | #745 (W6-1) | pending |
| `slate.canvas.connectMode` | — | #745 (W6-1) | pending |
| `slate.canvas.connectTo` | — | #745 (W6-1) | pending |
| `slate.canvas.convertToNote` | — | #745 (W6-1) | pending |
| `slate.canvas.createConnectedCard` | — | #745 (W6-1) | pending |
| `slate.canvas.createConnectedCardDirectional` | — | #745 (W6-1) | pending |
| `slate.canvas.delete` | — | #745 (W6-1) | pending |
| `slate.canvas.deleteConnection` | — | #745 (W6-1) | pending |
| `slate.canvas.deleteMarked` | — | #745 (W6-1) | pending |
| `slate.canvas.duplicate` | — | #745 (W6-1) | pending |
| `slate.canvas.editCard` | — | #745 (W6-1) | pending |
| `slate.canvas.editConnection` | — | #745 (W6-1) | pending |
| `slate.canvas.enterGroup` | — | #745 (W6-1) | pending |
| `slate.canvas.exitGroup` | — | #745 (W6-1) | pending |
| `slate.canvas.filterCards` | — | #745 (W6-1) | pending |
| `slate.canvas.fitCanvas` | — | #745 (W6-1) | pending |
| `slate.canvas.followConnectionBack` | — | #745 (W6-1) | pending |
| `slate.canvas.followConnectionForward` | — | #745 (W6-1) | pending |
| `slate.canvas.groupMarked` | — | #745 (W6-1) | pending |
| `slate.canvas.locateFile` | — | #745 (W6-1) | pending |
| `slate.canvas.moveIntoGroup` | — | #745 (W6-1) | pending |
| `slate.canvas.moveMode` | — | #745 (W6-1) | pending |
| `slate.canvas.newCard` | — | #745 (W6-1) | pending |
| `slate.canvas.newGroup` | — | #745 (W6-1) | pending |
| `slate.canvas.nextCard` | — | #745 (W6-1) | pending |
| `slate.canvas.placeAbove` | — | #745 (W6-1) | pending |
| `slate.canvas.placeBelow` | — | #745 (W6-1) | pending |
| `slate.canvas.placeLeftOf` | — | #745 (W6-1) | pending |
| `slate.canvas.placeRightOf` | — | #745 (W6-1) | pending |
| `slate.canvas.previousCard` | — | #745 (W6-1) | pending |
| `slate.canvas.removeFromGroup` | — | #745 (W6-1) | pending |
| `slate.canvas.renameGroup` | — | #745 (W6-1) | pending |
| `slate.canvas.resizeDefaultSize` | — | #745 (W6-1) | pending |
| `slate.canvas.resizeFitContent` | — | #745 (W6-1) | pending |
| `slate.canvas.resizeMode` | — | #745 (W6-1) | pending |
| `slate.canvas.setColor` | — | #745 (W6-1) | pending |
| `slate.canvas.showMarks` | — | #745 (W6-1) | pending |
| `slate.canvas.showOutline` | — | #745 (W6-1) | pending |
| `slate.canvas.showTable` | — | #745 (W6-1) | pending |
| `slate.canvas.showVisual` | — | #745 (W6-1) | pending |
| `slate.canvas.toggleFollowSelection` | — | #745 (W6-1) | pending |
| `slate.canvas.toggleMark` | — | #745 (W6-1) | pending |
| `slate.canvas.tracePath` | — | #745 (W6-1) | pending |
| `slate.canvas.whereAmI` | — | #745 (W6-1) | pending |
| `slate.canvas.zoomIn` | — | #745 (W6-1) | pending |
| `slate.canvas.zoomOut` | — | #745 (W6-1) | pending |
| `slate.canvas.zoomToSelection` | — | #745 (W6-1) | pending |
| `slate.diagnostics.refreshSync` | — | #741 (W5-1) | pending |
| `slate.editor.actualSize` | — | #725 (W2-3) | pending |
| `slate.editor.addProperty` | — | #725 (W2-3) | pending |
| `slate.editor.bulkRenameProperties` | — | #725 (W2-3) | pending |
| `slate.editor.citationSummary` | — | #725 (W2-3) | pending |
| `slate.editor.findInNote` | — | #725 (W2-3) | pending |
| `slate.editor.save` | — | #725 (W2-3) | pending |
| `slate.editor.togglePropertiesSource` | — | #725 (W2-3) | pending |
| `slate.editor.toggleSpellCheck` | — | #725 (W2-3) | pending |
| `slate.editor.toggleViewMode` | — | #725 (W2-3) | pending |
| `slate.editor.zoomIn` | — | #725 (W2-3) | pending |
| `slate.editor.zoomOut` | — | #725 (W2-3) | pending |
| `slate.file.cancelImport` | — | #744 (W5-4) | pending |
| `slate.file.copyPath` | — | #744 (W5-4) | pending |
| `slate.file.delete` | — | #744 (W5-4) | pending |
| `slate.file.duplicate` | — | #744 (W5-4) | pending |
| `slate.file.importFilesAndFolders` | — | #744 (W5-4) | pending |
| `slate.file.moveTo` | ⇧⌘M | #744 (W5-4) | pending |
| `slate.file.newCanvas` | — | #744 (W5-4) | pending |
| `slate.file.newFolder` | — | #744 (W5-4) | pending |
| `slate.file.newFromTemplate` | ⇧⌘N | #744 (W5-4) | pending |
| `slate.file.newNote` | ⌘N | #744 (W5-4) | pending |
| `slate.file.printNote` | — | #744 (W5-4) | pending |
| `slate.file.rename` | ⌥⌘R | #744 (W5-4) | pending |
| `slate.file.revealInFinder` | — | #744 (W5-4) | pending |
| `slate.graph.actualSize` | — | #746 (W6-2) | pending |
| `slate.graph.connectionsDeeper` | — | #746 (W6-2) | pending |
| `slate.graph.connectionsShallower` | — | #746 (W6-2) | pending |
| `slate.graph.fitGraph` | — | #746 (W6-2) | pending |
| `slate.graph.mostLinked` | — | #746 (W6-2) | pending |
| `slate.graph.openTab` | — | #746 (W6-2) | pending |
| `slate.graph.orphans` | — | #746 (W6-2) | pending |
| `slate.graph.showConnections` | — | #746 (W6-2) | pending |
| `slate.graph.unresolved` | — | #746 (W6-2) | pending |
| `slate.graph.whereAmI` | — | #746 (W6-2) | pending |
| `slate.graph.zoomIn` | — | #746 (W6-2) | pending |
| `slate.graph.zoomOut` | — | #746 (W6-2) | pending |
| `slate.help.open` | — | #756 (W8-6) | pending |
| `slate.history.showPanel` | — | #739 (W4-7) | pending |
| `slate.navigation.jumpToBibliography` | — | #741 (W5-1) | pending |
| `slate.settings.open` | — | #751 (W8-1) | pending |
| `slate.sidebar.addShortcut` | — | #721 (W1-2) | pending |
| `slate.sidebar.addTag` | — | #721 (W1-2) | pending |
| `slate.sidebar.clearRecents` | — | #721 (W1-2) | pending |
| `slate.sidebar.collapseAll` | — | #721 (W1-2) | pending |
| `slate.sidebar.copyWikilink` | — | #721 (W1-2) | pending |
| `slate.sidebar.createFolderNote` | — | #721 (W1-2) | pending |
| `slate.sidebar.deleteFolderNote` | — | #721 (W1-2) | pending |
| `slate.sidebar.expandLoaded` | — | #721 (W1-2) | pending |
| `slate.sidebar.focusFilter` | ⌥⌘F | #721 (W1-2) | pending |
| `slate.sidebar.historyBack` | ⌃⌘[ | #721 (W1-2) | pending |
| `slate.sidebar.historyForward` | ⌃⌘] | #721 (W1-2) | pending |
| `slate.sidebar.open` | — | #721 (W1-2) | pending |
| `slate.sidebar.openFolderNote` | — | #721 (W1-2) | pending |
| `slate.sidebar.openShortcut1` | — | #721 (W1-2) | pending |
| `slate.sidebar.openShortcut2` | — | #721 (W1-2) | pending |
| `slate.sidebar.openShortcut3` | — | #721 (W1-2) | pending |
| `slate.sidebar.openShortcut4` | — | #721 (W1-2) | pending |
| `slate.sidebar.openShortcut5` | — | #721 (W1-2) | pending |
| `slate.sidebar.openShortcut6` | — | #721 (W1-2) | pending |
| `slate.sidebar.openShortcut7` | — | #721 (W1-2) | pending |
| `slate.sidebar.openShortcut8` | — | #721 (W1-2) | pending |
| `slate.sidebar.openShortcut9` | — | #721 (W1-2) | pending |
| `slate.sidebar.pinNote` | — | #721 (W1-2) | pending |
| `slate.sidebar.removeShortcut` | — | #721 (W1-2) | pending |
| `slate.sidebar.removeTag` | — | #721 (W1-2) | pending |
| `slate.sidebar.sortCreatedAsc` | — | #721 (W1-2) | pending |
| `slate.sidebar.sortCreatedDesc` | — | #721 (W1-2) | pending |
| `slate.sidebar.sortModifiedAsc` | — | #721 (W1-2) | pending |
| `slate.sidebar.sortModifiedDesc` | — | #721 (W1-2) | pending |
| `slate.sidebar.sortNameAsc` | — | #721 (W1-2) | pending |
| `slate.sidebar.sortNameDesc` | — | #721 (W1-2) | pending |
| `slate.sidebar.toggleDateGrouping` | — | #721 (W1-2) | pending |
| `slate.sidebar.unpinAllInFolder` | — | #721 (W1-2) | pending |
| `slate.sidebar.unpinNote` | — | #721 (W1-2) | pending |
| `slate.sidebar.useVaultDefaultSort` | — | #721 (W1-2) | pending |
| `slate.tasks.review` | — | #735 (W4-3) | pending |
| `slate.vault.close` | — | #720 (W1-1) | pending |
| `slate.vault.open` | — | #720 (W1-1) | pending |
| `slate.view.toggleRightPane` | — | #722 (W1-3) | pending |
| `slate.view.toggleSearch` | — | #722 (W1-3) | pending |
| `slate.workspace.closePane` | — | #722 (W1-3) | pending |
| `slate.workspace.closeTab` | — | #722 (W1-3) | pending |
| `slate.workspace.focusPaneAbove` | — | #722 (W1-3) | pending |
| `slate.workspace.focusPaneBelow` | — | #722 (W1-3) | pending |
| `slate.workspace.focusPaneLeft` | — | #722 (W1-3) | pending |
| `slate.workspace.focusPaneRight` | — | #722 (W1-3) | pending |
| `slate.workspace.growPane` | — | #722 (W1-3) | pending |
| `slate.workspace.moveTabLeft` | — | #722 (W1-3) | pending |
| `slate.workspace.moveTabRight` | — | #722 (W1-3) | pending |
| `slate.workspace.newTab` | — | #722 (W1-3) | pending |
| `slate.workspace.nextTab` | — | #722 (W1-3) | pending |
| `slate.workspace.openInNewTab` | — | #722 (W1-3) | pending |
| `slate.workspace.openInSplit` | — | #722 (W1-3) | pending |
| `slate.workspace.previousTab` | — | #722 (W1-3) | pending |
| `slate.workspace.quickOpen` | — | #722 (W1-3) | pending |
| `slate.workspace.reopenClosedTab` | — | #722 (W1-3) | pending |
| `slate.workspace.shrinkPane` | — | #722 (W1-3) | pending |
| `slate.workspace.splitDown` | — | #722 (W1-3) | pending |
| `slate.workspace.splitRight` | — | #722 (W1-3) | pending |

The palette surface itself (ranking via the W0.5-1 core engine, sections, recents, chord display) is **#741 (W5-1)**; the quick switcher is **#723 (W1-4)**.

## Leaf / panel / tab inventory

| surface | source | consuming W issue | status |
|---|---|---|---|
| App shell, window chrome, vault lifecycle | `SlateMacApp.swift` | #720 (W1-1) | pending |
| Files sidebar (tree CRUD, filter, tags, pins, shortcuts, folder notes) | `FileTreeSidebar.swift` + FL program | #721 (W1-2) | pending |
| Workspace: tabs, splits, leaves, persistence, focus routing | `Workspace/` | #722 (W1-3) | pending |
| Quick switcher | `QuickSwitcherModel.swift` (core ranking, W0.5-2) | #723 (W1-4) | pending |
| Editor (AvalonEdit ⇄ DocumentBuffer, spans, interactions) | `NoteEditorView.swift` | #724/#381/#725 (W2-1/2/3) | pending |
| Reading view (block model, mode toggle, heading/link AT nav) | `Reading/` | #728 (W3-1) | pending |
| Math rendering + speech/braille artifact | `MathBlockView` + core `math.rs` | #729 (W3-2) | pending |
| Diagrams (canonical Rust SVG + description) | core `diagram.rs` consumers | #730 (W3-3) | pending |
| Code blocks (canonical tokens + AT preamble) | `CodeBlockView.swift` | #731 (W3-4) | pending |
| Embeds across contexts | `EmbedsPanel.swift` + editor embeds | #732 (W3-5; XD rows dropped) | pending |
| Accessible grid substrate | `AccessibleDataGrid.swift` | #733 (W4-1) | pending |
| BacklinksPanel | `BacklinksPanel.swift` | #734 (W4-2) | pending |
| BibliographyPanel | `BibliographyPanel.swift` | #737 (W4-5) | pending |
| CitationsPanel | `CitationsPanel.swift` | #737 (W4-5) | pending |
| ContentBlockPanels | `ContentBlockPanels.swift` | #734 (W4-2) | pending |
| EmbedsPanel | `EmbedsPanel.swift` | #734 (W4-2) | pending |
| HistoryPanel | `HistoryPanel.swift` | #739 (W4-7) | pending |
| OutgoingLinksPanel | `OutgoingLinksPanel.swift` | #734 (W4-2) | pending |
| SyncDiagnosticsPanel | `SyncDiagnosticsPanel.swift` | #740 (W4-8) | pending |
| TasksPanel | `TasksPanel.swift` | #735 (W4-3) | pending |
| TasksReviewPanel | `TasksReviewPanel.swift` | #734 (W4-2) | pending |
| Properties (in-note header, panel, typed rows) | `Properties*` views | #736 (W4-4) | pending |
| Bases grid + builder (N shipped) | `Bases/` | #738 (W4-6) | pending |
| Command palette | `CommandPaletteModel.swift` (core ranking, W0.5-1) | #741 (W5-1) | pending |
| Search overlay | search UI over `full_text_search` | #742 (W5-2) | pending |
| Templates picker + prompt flow | template views | #743 (W5-3) | pending |
| File management + bulk rename | sidebar/file commands | #744 (W5-4) | pending |
| Accessible canvas (T parity) | `Canvas/` | #745 (W6-1) | pending |
| Graph view (P parity, canonical textual representation) | `Graph/` | #746 (W6-2) | pending |

## Settings surface

| tab | consuming W issue | status |
|---|---|---|
| General | #751 (W8-1) | pending |
| Sidebar | #751 (W8-1) | pending |
| Math | #751 (W8-1) | pending |
| Code | #751 (W8-1) | pending |
| Bibliography | #751 (W8-1) | pending |
| Canvas | #751 (W8-1) | pending |
| History | #751 (W8-1) | pending |
| Windows-only section (theme/contrast behavior, file associations) | #751 (W8-1, additive) | pending |

## Help-doc index

| doc | consuming W issue | status |
|---|---|---|
| `docs/help/bases.md` | #756 (W8-6; shared prose, per-platform chords per decision 20) | pending |
| `docs/help/canvas.md` | #756 (W8-6; shared prose, per-platform chords per decision 20) | pending |
| `docs/help/graph.md` | #756 (W8-6; shared prose, per-platform chords per decision 20) | pending |

## `slate.cli.v1` surface

Verbs (from `slate-cli --help`): `open`, `sync-check`, `tasks`, `render-template`, `history`, `search`, `query`, `write`, `read`, `list`, `links`, `properties`, `completions`.

| capability | consuming W issue | status |
|---|---|---|
| CLI builds + full test suite green on the Windows runner | #715 (W0-3) | **shipped** (windows.yml step) |
| Distribution/packaging beyond CI | reserved (W-E5, decision 19) | out of scope |

## File-type handlers

The SwiftPM mac app declares no `CFBundleDocumentTypes`; the shipped handler set is pinned from program decision 15.

| type | Windows behavior | consuming W issue | status |
|---|---|---|---|
| `.md` | association optional per user choice | #753 (W8-3) | pending |
| `.base` | registered | #753 (W8-3) | pending |
| `.canvas` | registered | #753 (W8-3) | pending |
| `.excalidraw` | dropped — XD unshipped at snapshot | — | dropped |

## Dropped feature-conditional rows (program §moving-target item 3)

| milestone | would-be consumer | one-line note |
|---|---|---|
| Milestone V — editor autocomplete | #726 (W2-4) | V unshipped at snapshot (GH milestone 29: 15 open) |
| Milestone X — LaTeX authoring aids | #727 (W2-5) | X unshipped at snapshot (GH milestone 30: 15 open) |
| Milestone XD — Excalidraw viewer | #732 (W3-5, XD rows only) | XD unshipped at snapshot (GH milestone 34: 13 open); non-XD embed rows stay |
| Milestone E — note export (HTML + DOCX) | W5/W8 rows per G1 | E unshipped at snapshot (GH milestone 36: 15 open) |
| Milestone PD — accessible image OCR | W3/W4 rows per G1 | PD unshipped at snapshot (GH milestone 35: 7 open) |
| Milestone R — themes | #752 (W8-2 consumes R's shared APCA spec) | R unstarted at snapshot (GH milestone 18 empty); W8-2 falls back to the Swift-test predecessor per its spec |
| Milestone S — explain-this-function | (no W issue — post-R/S mac feature) | S unstarted at snapshot (GH milestone 19 empty) |

## Foundation rows already shipped by W0

| capability | issue | status |
|---|---|---|
| `apps/slate-windows/` scaffold, windows.yml CI, hello-core app | #603 (W0-2) | **shipped** (#956) |
| Full-surface C# binding + §W-E censuses + §W-A harness skeleton + app log | #715 (W0-3) | **shipped** |
| Parity matrix + §W-B budgets + entry-criteria snapshot | #716 (W0-4) | **this document** |
