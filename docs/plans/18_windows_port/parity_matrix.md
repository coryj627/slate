# Milestone W parity matrix (§W-F row-level checklist)

Generated 2026-07-19 at `2ea76ff` by `scripts/generate-parity-matrix.py` (W0-4, #716). **Re-runnable:** matrix drift = re-run, diff, re-triage (program §moving-target). Every row is burned down by its consuming W issue; §W-F gates close-out on zero unshipped/unwaived rows.

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

181 stable command ids from the `SlateCommandID` catalog (drift-test-enforced), 52 carrying chords from the registration blocks and definition-table chord switches (blank chord = palette/menu-only or focus-scoped by design; the generator fails if a `hotkey:` literal goes unattributed). Spoken hotkeys derive from chords via the `HotkeySpoken` glyph walk (mirrored here); Windows chord mapping is by platform convention (⌘→Ctrl, ⌥→Alt; decision 12), declared in one table in W5-1 with spoken strings substituted per-platform through the canonical vocabulary.

| command id | capability (mac label) | mac chord | spoken hotkey | consuming W issue | status |
|---|---|---|---|---|---|
| `slate.bases.builder.addCondition` | Bases: Add Condition | — | — | #738 (W4-6) | pending |
| `slate.bases.builder.addGroup` | Bases: Add Group | — | — | #738 (W4-6) | pending |
| `slate.bases.builder.editCondition` | Bases: Edit Condition | — | — | #738 (W4-6) | pending |
| `slate.bases.builder.removeCondition` | Bases: Remove Condition | — | — | #738 (W4-6) | pending |
| `slate.bases.copyLink` | Bases: Copy Link | — | — | #738 (W4-6) | pending |
| `slate.bases.copyMarkdown` | Bases: Copy View as Markdown | — | — | #738 (W4-6) | pending |
| `slate.bases.editProperty` | Bases: Edit Property | — | — | #738 (W4-6) | pending |
| `slate.bases.editViewFilters` | Bases: Edit View Filters | — | — | #738 (W4-6) | pending |
| `slate.bases.exportCsv` | Bases: Export View as CSV | — | — | #738 (W4-6) | pending |
| `slate.bases.exportMarkdown` | Bases: Export View as Markdown Table | — | — | #738 (W4-6) | pending |
| `slate.bases.newQuery` | Bases: New Query | — | — | #738 (W4-6) | pending |
| `slate.bases.nextView` | Bases: Next View | — | — | #738 (W4-6) | pending |
| `slate.bases.openRow` | Bases: Open Row | — | — | #738 (W4-6) | pending |
| `slate.bases.openViewSwitcher` | Bases: Open View Switcher | — | — | #738 (W4-6) | pending |
| `slate.bases.previousView` | Bases: Previous View | — | — | #738 (W4-6) | pending |
| `slate.bases.quickFilter` | Bases: Quick Filter | — | — | #738 (W4-6) | pending |
| `slate.bases.refresh` | Bases: Refresh | — | — | #738 (W4-6) | pending |
| `slate.bases.resultsPopover` | Bases: Results | — | — | #738 (W4-6) | pending |
| `slate.bases.saveSortToView` | Bases: Save Sort to View | — | — | #738 (W4-6) | pending |
| `slate.bases.savedQuery.run.<dynamic>` | — | — | — | #738 (W4-6) | pending |
| `slate.bases.showBacklinks` | Bases: Show Backlinks | — | — | #738 (W4-6) | pending |
| `slate.bases.sortByColumn` | Bases: Sort by Column | — | — | #738 (W4-6) | pending |
| `slate.bases.viewAsList` | Bases: View as List | — | — | #738 (W4-6) | pending |
| `slate.bases.viewAsTable` | Bases: View as Table | — | — | #738 (W4-6) | pending |
| `slate.bases.whereAmI` | Bases: Where Am I? | — | — | #738 (W4-6) | pending |
| `slate.canvas.actualSize` | Canvas: Actual Size | ⌘0 | Command 0 | #745 (W6-1) | pending |
| `slate.canvas.addLink` | Canvas: Add Link Card… | — | — | #745 (W6-1) | pending |
| `slate.canvas.addMedia` | Canvas: Add Media… | — | — | #745 (W6-1) | pending |
| `slate.canvas.addNote` | Canvas: Add Note to Canvas… | — | — | #745 (W6-1) | pending |
| `slate.canvas.alignWith` | Canvas: Align With… | — | — | #745 (W6-1) | pending |
| `slate.canvas.cancelMode` | Canvas: Cancel Mode | — | — | #745 (W6-1) | pending |
| `slate.canvas.clearColor` | Canvas: Clear Color | — | — | #745 (W6-1) | pending |
| `slate.canvas.clearFilter` | Canvas: Clear Filter | — | — | #745 (W6-1) | pending |
| `slate.canvas.clearMarks` | Canvas: Clear All Marks | — | — | #745 (W6-1) | pending |
| `slate.canvas.commitMode` | Canvas: Commit Mode | — | — | #745 (W6-1) | pending |
| `slate.canvas.connectMode` | Canvas: Connect Mode | — | — | #745 (W6-1) | pending |
| `slate.canvas.connectTo` | Canvas: Connect To… | ⌃⌘C | Control Command C | #745 (W6-1) | pending |
| `slate.canvas.convertToNote` | Canvas: Convert Card to Note… | — | — | #745 (W6-1) | pending |
| `slate.canvas.createConnectedCard` | Canvas: Create Connected Card | ⌃⌥⌘N | Control Option Command N | #745 (W6-1) | pending |
| `slate.canvas.createConnectedCardDirectional` | Canvas: Create Connected Card (Choose Direction)… | — | — | #745 (W6-1) | pending |
| `slate.canvas.delete` | Canvas: Delete Selection | — | — | #745 (W6-1) | pending |
| `slate.canvas.deleteConnection` | Canvas: Delete Connection… | — | — | #745 (W6-1) | pending |
| `slate.canvas.deleteMarked` | Canvas: Delete Marked Cards | — | — | #745 (W6-1) | pending |
| `slate.canvas.duplicate` | Canvas: Duplicate | — | — | #745 (W6-1) | pending |
| `slate.canvas.editCard` | Canvas: Edit Card Text… | — | — | #745 (W6-1) | pending |
| `slate.canvas.editConnection` | Canvas: Edit Connection… | — | — | #745 (W6-1) | pending |
| `slate.canvas.enterGroup` | Canvas: Enter Group | — | — | #745 (W6-1) | pending |
| `slate.canvas.exitGroup` | Canvas: Exit Group | — | — | #745 (W6-1) | pending |
| `slate.canvas.filterCards` | Canvas: Filter Cards… | — | — | #745 (W6-1) | pending |
| `slate.canvas.fitCanvas` | Canvas: Fit Canvas | — | — | #745 (W6-1) | pending |
| `slate.canvas.followConnectionBack` | Canvas: Follow Connection Back | — | — | #745 (W6-1) | pending |
| `slate.canvas.followConnectionForward` | Canvas: Follow Connection Forward | — | — | #745 (W6-1) | pending |
| `slate.canvas.groupMarked` | Canvas: Group Marked Cards… | — | — | #745 (W6-1) | pending |
| `slate.canvas.locateFile` | Canvas: Locate File… | — | — | #745 (W6-1) | pending |
| `slate.canvas.moveIntoGroup` | Canvas: Move into Group… | — | — | #745 (W6-1) | pending |
| `slate.canvas.moveMode` | Canvas: Move Mode | ⌃⌘G | Control Command G | #745 (W6-1) | pending |
| `slate.canvas.newCard` | Canvas: New Card | ⌥⌘N | Option Command N | #745 (W6-1) | pending |
| `slate.canvas.newGroup` | Canvas: New Group… | — | — | #745 (W6-1) | pending |
| `slate.canvas.nextCard` | Canvas: Next Card | — | — | #745 (W6-1) | pending |
| `slate.canvas.placeAbove` | Canvas: Place Above… | — | — | #745 (W6-1) | pending |
| `slate.canvas.placeBelow` | Canvas: Place Below… | — | — | #745 (W6-1) | pending |
| `slate.canvas.placeLeftOf` | Canvas: Place Left Of… | — | — | #745 (W6-1) | pending |
| `slate.canvas.placeRightOf` | Canvas: Place Right Of… | — | — | #745 (W6-1) | pending |
| `slate.canvas.previousCard` | Canvas: Previous Card | — | — | #745 (W6-1) | pending |
| `slate.canvas.removeFromGroup` | Canvas: Remove from Group | — | — | #745 (W6-1) | pending |
| `slate.canvas.renameGroup` | Canvas: Rename Group… | — | — | #745 (W6-1) | pending |
| `slate.canvas.resizeDefaultSize` | Canvas: Resize to Default Size | — | — | #745 (W6-1) | pending |
| `slate.canvas.resizeFitContent` | Canvas: Resize to Fit Content | — | — | #745 (W6-1) | pending |
| `slate.canvas.resizeMode` | Canvas: Resize Mode | ⌃⌘R | Control Command R | #745 (W6-1) | pending |
| `slate.canvas.setColor` | Canvas: Set Color… | — | — | #745 (W6-1) | pending |
| `slate.canvas.showMarks` | Canvas: Show Marked Cards | — | — | #745 (W6-1) | pending |
| `slate.canvas.showOutline` | Canvas: Show Outline | — | — | #745 (W6-1) | pending |
| `slate.canvas.showTable` | Canvas: Show Table | — | — | #745 (W6-1) | pending |
| `slate.canvas.showVisual` | Canvas: Show Visual | — | — | #745 (W6-1) | pending |
| `slate.canvas.toggleFollowSelection` | Canvas: Toggle Viewport Follows Selection | — | — | #745 (W6-1) | pending |
| `slate.canvas.toggleMark` | Canvas: Toggle Mark | ⌃⌘M | Control Command M | #745 (W6-1) | pending |
| `slate.canvas.tracePath` | Canvas: Trace Path from Selected Card | — | — | #745 (W6-1) | pending |
| `slate.canvas.whereAmI` | Canvas: Where Am I? | ⌃⌘I | Control Command I | #745 (W6-1) | pending |
| `slate.canvas.zoomIn` | Canvas: Zoom In | ⌘= | Command Equals | #745 (W6-1) | pending |
| `slate.canvas.zoomOut` | Canvas: Zoom Out | ⌘- | Command Minus | #745 (W6-1) | pending |
| `slate.canvas.zoomToSelection` | Canvas: Zoom to Selection | — | — | #745 (W6-1) | pending |
| `slate.diagnostics.refreshSync` | Refresh Sync Diagnostics | — | — | #740 (W4-8) | pending |
| `slate.editor.actualSize` | Editor: Actual Size | — | — | #725 (W2-3) | pending |
| `slate.editor.addProperty` | Add Property… | — | — | #736 (W4-4) | pending |
| `slate.editor.bulkRenameProperties` | Bulk Rename Properties… | ⇧⌘R | Shift Command R | #736 (W4-4) | pending |
| `slate.editor.citationSummary` | Citation Summary | ⇧⌘J | Shift Command J | #737 (W4-5) | pending |
| `slate.editor.findInNote` | Find… | ⌘F | Command F | #742 (W5-2) | pending |
| `slate.editor.save` | Save | ⌘S | Command S | #724 (W2-1) | pending |
| `slate.editor.togglePropertiesSource` | Show Properties Source | ⇧⌘D | Shift Command D | #736 (W4-4) | pending |
| `slate.editor.toggleSpellCheck` | Check Spelling While Typing | — | — | #725 (W2-3) | pending |
| `slate.editor.toggleViewMode` | Toggle Reading Mode | ⇧⌘E | Shift Command E | #728 (W3-1) | pending |
| `slate.editor.zoomIn` | Editor: Zoom In | — | — | #725 (W2-3) | pending |
| `slate.editor.zoomOut` | Editor: Zoom Out | — | — | #725 (W2-3) | pending |
| `slate.file.cancelImport` | Cancel Import | ⌘. | Command Period | #721 (W1-2) | pending |
| `slate.file.copyPath` | Copy Path | — | — | #744 (W5-4) | pending |
| `slate.file.delete` | Move to Trash | — | — | #744 (W5-4) | pending |
| `slate.file.duplicate` | Duplicate | — | — | #744 (W5-4) | pending |
| `slate.file.importFilesAndFolders` | Import Files and Folders… | — | — | #721 (W1-2) | pending |
| `slate.file.moveTo` | Move To… | ⇧⌘M | Shift Command M | #744 (W5-4) | pending |
| `slate.file.newCanvas` | New Canvas | — | — | #745 (W6-1) | pending |
| `slate.file.newFolder` | New Folder | — | — | #744 (W5-4) | pending |
| `slate.file.newFromTemplate` | New Note from Template… | ⇧⌘N | Shift Command N | #743 (W5-3) | pending |
| `slate.file.newNote` | New Note | ⌘N | Command N | #744 (W5-4) | pending |
| `slate.file.printNote` | Print… | ⌘P | Command P | #728 (W3-1) | pending |
| `slate.file.rename` | Rename… | ⌥⌘R | Option Command R | #744 (W5-4) | pending |
| `slate.file.revealInFinder` | Reveal in Finder | — | — | #744 (W5-4) | pending |
| `slate.graph.actualSize` | Graph: Actual Size | ⌘0 | Command 0 | #746 (W6-2) | pending |
| `slate.graph.connectionsDeeper` | Connections: Deeper | — | — | #746 (W6-2) | pending |
| `slate.graph.connectionsShallower` | Connections: Shallower | — | — | #746 (W6-2) | pending |
| `slate.graph.fitGraph` | Graph: Fit Graph | ⌥⌘0 | Option Command 0 | #746 (W6-2) | pending |
| `slate.graph.mostLinked` | Graph: Most Linked Notes | — | — | #746 (W6-2) | pending |
| `slate.graph.openTab` | Open Graph | — | — | #746 (W6-2) | pending |
| `slate.graph.orphans` | Graph: Orphaned Notes | — | — | #746 (W6-2) | pending |
| `slate.graph.showConnections` | Show Connections | — | — | #746 (W6-2) | pending |
| `slate.graph.unresolved` | Graph: Unresolved Links | — | — | #746 (W6-2) | pending |
| `slate.graph.whereAmI` | Graph: Where Am I? | ⌃⌘I | Control Command I | #746 (W6-2) | pending |
| `slate.graph.zoomIn` | Graph: Zoom In | ⌘= | Command Equals | #746 (W6-2) | pending |
| `slate.graph.zoomOut` | Graph: Zoom Out | ⌘- | Command Minus | #746 (W6-2) | pending |
| `slate.help.open` | Help | — | — | #756 (W8-6) | pending |
| `slate.history.showPanel` | Show History Panel | — | — | #739 (W4-7) | pending |
| `slate.navigation.jumpToBibliography` | Jump to Bibliography | ⌘J | Command J | #737 (W4-5) | pending |
| `slate.settings.open` | Settings… | ⌘, | Command Comma | #751 (W8-1) | pending |
| `slate.sidebar.addShortcut` | Add to Shortcuts | — | — | #721 (W1-2) | pending |
| `slate.sidebar.addTag` | Add Tag… | — | — | #721 (W1-2) | pending |
| `slate.sidebar.clearRecents` | Clear Recents | — | — | #721 (W1-2) | pending |
| `slate.sidebar.collapseAll` | Collapse All Folders | — | — | #721 (W1-2) | pending |
| `slate.sidebar.copyWikilink` | Copy Wikilink | — | — | #721 (W1-2) | pending |
| `slate.sidebar.createFolderNote` | Create Folder Note | — | — | #721 (W1-2) | pending |
| `slate.sidebar.deleteFolderNote` | Delete Folder Note | — | — | #721 (W1-2) | pending |
| `slate.sidebar.expandLoaded` | Expand Loaded Folders | — | — | #721 (W1-2) | pending |
| `slate.sidebar.focusFilter` | Focus Sidebar Filter | ⌥⌘F | Option Command F | #721 (W1-2) | pending |
| `slate.sidebar.historyBack` | Back in Sidebar History | ⌃⌘[ | Control Command Left Bracket | #721 (W1-2) | pending |
| `slate.sidebar.historyForward` | Forward in Sidebar History | ⌃⌘] | Control Command Right Bracket | #721 (W1-2) | pending |
| `slate.sidebar.open` | Open | — | — | #721 (W1-2) | pending |
| `slate.sidebar.openFolderNote` | Open Folder Note | — | — | #721 (W1-2) | pending |
| `slate.sidebar.openShortcut1` | — | — | — | #721 (W1-2) | pending |
| `slate.sidebar.openShortcut2` | — | — | — | #721 (W1-2) | pending |
| `slate.sidebar.openShortcut3` | — | — | — | #721 (W1-2) | pending |
| `slate.sidebar.openShortcut4` | — | — | — | #721 (W1-2) | pending |
| `slate.sidebar.openShortcut5` | — | — | — | #721 (W1-2) | pending |
| `slate.sidebar.openShortcut6` | — | — | — | #721 (W1-2) | pending |
| `slate.sidebar.openShortcut7` | — | — | — | #721 (W1-2) | pending |
| `slate.sidebar.openShortcut8` | — | — | — | #721 (W1-2) | pending |
| `slate.sidebar.openShortcut9` | — | — | — | #721 (W1-2) | pending |
| `slate.sidebar.pinNote` | Pin to Top of Folder | — | — | #721 (W1-2) | pending |
| `slate.sidebar.removeShortcut` | Remove from Shortcuts | — | — | #721 (W1-2) | pending |
| `slate.sidebar.removeTag` | Remove Tag… | — | — | #721 (W1-2) | pending |
| `slate.sidebar.sortCreatedAsc` | Sort by Created (Oldest First) | — | — | #721 (W1-2) | pending |
| `slate.sidebar.sortCreatedDesc` | Sort by Created (Newest First) | — | — | #721 (W1-2) | pending |
| `slate.sidebar.sortModifiedAsc` | Sort by Modified (Oldest First) | — | — | #721 (W1-2) | pending |
| `slate.sidebar.sortModifiedDesc` | Sort by Modified (Newest First) | — | — | #721 (W1-2) | pending |
| `slate.sidebar.sortNameAsc` | Sort by Name (A to Z) | — | — | #721 (W1-2) | pending |
| `slate.sidebar.sortNameDesc` | Sort by Name (Z to A) | — | — | #721 (W1-2) | pending |
| `slate.sidebar.toggleDateGrouping` | Group by Date | — | — | #721 (W1-2) | pending |
| `slate.sidebar.unpinAllInFolder` | Unpin All in Folder | — | — | #721 (W1-2) | pending |
| `slate.sidebar.unpinNote` | Unpin | — | — | #721 (W1-2) | pending |
| `slate.sidebar.useVaultDefaultSort` | Use Vault Default Sort | — | — | #721 (W1-2) | pending |
| `slate.tasks.review` | Tasks Review | ⌘R | Command R | #735 (W4-3) | pending |
| `slate.vault.close` | Close Vault | — | — | #720 (W1-1) | pending |
| `slate.vault.open` | Open Vault… | ⇧⌘O | Shift Command O | #720 (W1-1) | pending |
| `slate.view.toggleRightPane` | Toggle Right Pane | ⌥⌘I | Option Command I | #722 (W1-3) | pending |
| `slate.view.toggleSearch` | Search Vault | ⇧⌘F | Shift Command F | #742 (W5-2) | pending |
| `slate.workspace.closePane` | Close Pane | — | — | #722 (W1-3) | pending |
| `slate.workspace.closeTab` | Close Tab | ⌘W | Command W | #722 (W1-3) | pending |
| `slate.workspace.focusPaneAbove` | Focus Pane Above | ⌥⌘↑ | Option Command Up Arrow | #722 (W1-3) | pending |
| `slate.workspace.focusPaneBelow` | Focus Pane Below | ⌥⌘↓ | Option Command Down Arrow | #722 (W1-3) | pending |
| `slate.workspace.focusPaneLeft` | Focus Pane Left | ⌥⌘← | Option Command Left Arrow | #722 (W1-3) | pending |
| `slate.workspace.focusPaneRight` | Focus Pane Right | ⌥⌘→ | Option Command Right Arrow | #722 (W1-3) | pending |
| `slate.workspace.growPane` | Grow Pane | ⌥⌘= | Option Command Equals | #722 (W1-3) | pending |
| `slate.workspace.moveTabLeft` | Move Tab Left | ⌃⌘← | Control Command Left Arrow | #722 (W1-3) | pending |
| `slate.workspace.moveTabRight` | Move Tab Right | ⌃⌘→ | Control Command Right Arrow | #722 (W1-3) | pending |
| `slate.workspace.newTab` | Duplicate Tab | ⌘T | Command T | #722 (W1-3) | pending |
| `slate.workspace.nextTab` | Show Next Tab | ⇧⌘] | Shift Command Right Bracket | #722 (W1-3) | pending |
| `slate.workspace.openInNewTab` | Open Selected File in New Tab | — | — | #722 (W1-3) | pending |
| `slate.workspace.openInSplit` | Open Selected File in Split | — | — | #722 (W1-3) | pending |
| `slate.workspace.previousTab` | Show Previous Tab | ⇧⌘[ | Shift Command Left Bracket | #722 (W1-3) | pending |
| `slate.workspace.quickOpen` | Quick Open… | ⌘O | Command O | #723 (W1-4) | pending |
| `slate.workspace.reopenClosedTab` | Reopen Closed Tab | ⇧⌘T | Shift Command T | #722 (W1-3) | pending |
| `slate.workspace.shrinkPane` | Shrink Pane | ⌥⌘- | Option Command Minus | #722 (W1-3) | pending |
| `slate.workspace.splitDown` | Split Down | ⌥⌘\\ | Option Command Backslash Backslash | #722 (W1-3) | pending |
| `slate.workspace.splitRight` | Split Right | ⌘\\ | Command Backslash Backslash | #722 (W1-3) | pending |

The palette surface itself (ranking via the W0.5-1 core engine, sections, recents, chord display) is **#741 (W5-1)**; the quick switcher is **#723 (W1-4)**.

## Leaf inventory (`enum Leaf`, the shipped right-pane registry)

| leaf | consuming W issue | status |
|---|---|---|
| `outline` | #734 (W4-2) | pending |
| `backlinks` | #734 (W4-2) | pending |
| `outgoingLinks` | #734 (W4-2) | pending |
| `connections` | #746 (W6-2) | pending |
| `embeds` | #734 (W4-2) | pending |
| `math` | #729 (W3-2) | pending |
| `code` | #731 (W3-4) | pending |
| `diagrams` | #730 (W3-3) | pending |
| `tasks` | #735 (W4-3) | pending |
| `tasksReview` | #735 (W4-3) | pending |
| `history` | #739 (W4-7) | pending |
| `citations` | #737 (W4-5) | pending |
| `bibliography` | #737 (W4-5) | pending |
| `queries` | #738 (W4-6) | pending |
| `basesDock` | #738 (W4-6) | pending |
| `syncDiagnostics` | #740 (W4-8) | pending |

## Primary surfaces

| surface | source | consuming W issue | status |
|---|---|---|---|
| App shell, window chrome, vault lifecycle | `SlateMacApp.swift` | #720 (W1-1) | pending |
| Files sidebar (tree CRUD, filter, tags, pins, shortcuts, folder notes) | `FileTreeSidebar.swift` + FL program | #721 (W1-2) | pending |
| Workspace: tabs, splits, leaves, persistence, focus routing | `Workspace/` | #722 (W1-3) | pending |
| Quick switcher | `QuickSwitcherModel.swift` (core ranking, W0.5-2) | #723 (W1-4) | pending |
| Editor host (AvalonEdit ⇄ DocumentBuffer, undo, save, IME) | `NoteEditorView.swift` | #724 (W2-1) | pending |
| Editor canonical spans | #381 span API consumers | #381 (W2-2) | pending |
| In-editor interactions (links, tags, citations, embeds, checkboxes) | `NoteEditorView.swift` | #725 (W2-3) | pending |
| Reading view (block model, mode toggle, heading/link AT nav, print) | `Reading/` | #728 (W3-1) | pending |
| Math rendering + canonical speech/braille artifact | core `math.rs` consumers | #729 (W3-2) | pending |
| Diagrams (canonical Rust SVG + description) | core `diagram.rs` consumers | #730 (W3-3) | pending |
| Code blocks (canonical tokens + AT preamble) | `CodeBlockView.swift` | #731 (W3-4) | pending |
| Embeds across contexts | editor/reading embeds | #732 (W3-5; XD rows dropped) | pending |
| Accessible grid substrate | `AccessibleDataGrid.swift` | #733 (W4-1) | pending |
| Properties (in-note header, panel, typed rows, add-property) | `Properties*` views | #736 (W4-4) | pending |
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
