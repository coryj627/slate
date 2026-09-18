# W5 COMMANDS — manual AT checklist (#750)

Source: [wave spec](../specs/w5_spec.md) acceptance lines and the
[conformance matrix](../w_c_matrix.md) keyboard routes. Run on a disposable
fixture vault. Use [_at_pass_template.md](_at_pass_template.md) for a named,
dated record and transcript. NVDA and JAWS are independent passes with stock
settings; Narrator is W8-6 smoke scope. Automated twins do not prove speech.

**Tester:** Pending · **AT:** Pending (exact version per run) · **OS:** Pending (edition and build)
**Build:** Pending (branch and verified commit) · **Corpus:** Pending (fixture vault and notes)
**Method:** Pending (Speech Viewer / JAWS history, braille viewer where required)
**Run date:** Pending · **Evidence reference:** Pending

| # | Spec item | Check | How (the UIA route) | Observable outcome | Automated twin | Narrator | NVDA | JAWS |
|---|---|---|---|---|---|---|---|---|
| 1 | W5-1 | Palette filter and dispatch | Open the command palette; type a query; move selection; invoke; reopen and Escape. | Selected command and count are spoken; invocation runs the named command and Escape restores focus. | `CommandPaletteTests` | Pending | Pending | Pending |
| 2 | W5-2 | Search and activation | Open Search; type a query; walk note and content results; activate a result; reopen and Escape. | Result snippets and counts are readable; activation lands in the intended note and location. | `SearchOverlayViewModelTests` | Pending | Pending | Pending |
| 3 | W5-3 | Templates and prompt focus | Create from template; select a template; fill required prompts; cancel once, then complete. | Prompt names and validation are spoken; cancellation restores focus; completion opens the created note. | `TemplateFlowTests` | Pending | Pending | Pending |
| 4 | W5-4 | File verbs and cancellation | On a disposable fixture create, rename, duplicate, move and delete a note; cancel the move picker; undo a mutation. | Each action names its target and outcome; cancellation is reachable and undo restores the expected files. | `FileManagementTests` | Pending | Pending | Pending |
| 5 | W7-3 | Menu accelerators | Open each main menu and the editor context menu; read a chorded item with NVDA and independently JAWS. | AcceleratorKey contains the table's display chord and the reader pronounces it; record each reader's actual rendering. | `SpokenChords_MenusPaletteAndOverlays_MatchTheTable` | Pending | Pending | Pending |
| 6 | W7-3 | Palette spoken chords | Open the palette and walk chorded and chordless rows; filter once and walk again. | Each Name carries its own command label and table-derived spoken chord, once; chordless rows retain their label. | `SpokenChords_MenusPaletteAndOverlays_MatchTheTable`, `CommandPaletteTests` | Pending | Pending | Pending |
| 7 | W7-3 | Quick Open and reading help | Open Quick Open and request field help; verify new-tab and both split instructions. Open reading view and request its help; follow heading, link, list and table instructions. | Help reads table-derived chords and the stated action follows each chord; reading help does not add editor navigation chords. | `SpokenChords_MenusPaletteAndOverlays_MatchTheTable`, `NavigationHelpTests`, `ReadingViewTests` | Pending | Pending | Pending |
| 8 | W7-3 navigation map | Sidebar action catalogs | On a disposable file and folder open the row context menu with Shift+F10 or the Application key; walk the available actions, activate a safe rename/duplicate action, then undo. | Menu actions name the selected target and availability; invocation reaches that action on the intended file or folder. | `FileManagementTests`, `CommandDriftTests` | Pending | Pending | Pending |
| 9 | W7-3 navigation map | Canvas custom-action equivalents | On a disposable canvas open a card's context menu; Toggle Mark, Delete and undo. On a connection row choose Edit Connection, cancel once then commit; Delete Connection and undo. | Each action remains reachable from its target row, names the target/outcome, and undo restores the deleted card or connection. | `TheOutlineMenuEqualsThePlan`, `TheWorkspaceMarkCommandsReachTheDocument`, `AConnectionRowsVerbsActOnTheCapturedEdgeFromItsSeatedSource`, `DeleteCardClearsSelectionAnnouncesAndUndoes` | Pending | Pending | Pending |
| 10 | W7-3 navigation map | Graph node actions and pinning | On a graph diagram node open its context menu; walk the node actions, invoke Show connections, return, then Pin and Unpin the same node. | Menu actions operate on the named node; its pin state changes and the menu label switches between Pin and Unpin. | `GraphMenuTests`, `GraphDiagramTests`, `GraphSurfaces_DiagramPeersTiersAndZoom_AreClean` | Pending | Pending | Pending |

Residual: every unexecuted row remains Pending. Record findings and the fix commit on retest; never replace a human pass with the automated twin.
