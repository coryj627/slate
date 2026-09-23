# W7-7 NVDA matrix remediation — feature contracts & registers (#1244–#1257)

Scope: [W7-7 executable spec](18_windows_port/specs/w7_7_nvda_matrix_remediation_spec.md). This contract-only commit precedes every implementation PR and every review round, per [24_red_team_protocol.md](24_red_team_protocol.md). Contract numbering is per-wave (`R-n`); each PR cites its numbers in its review prompts and appends its rounds to its own section below. Paths are relative to `apps/slate-windows/src/SlateWindows` unless stated.

## Contracts

**R-1 — Announcements reach a listening client from the first frame (PR 1, #1244).** The one production raiser (`AccessibilityNotificationDispatcher.cs`, contract 38 D-2) raises through `AutomationInteropProvider.RaiseAutomationEvent(AutomationElementIdentifiers.NotificationEvent, provider, NotificationEventArgs)` with the provider obtained from the status element's peer via `AutomationPeer.ProviderFromPeer`, guarded only by `AutomationInteropProvider.ClientsAreListening`. Evidence: WPF's `RaiseNotificationEvent` is gated on `EventMap.HasRegisteredEvent(Notification)`, a process-static map populated only by `ElementProxy.AdviseEventAdded`; the 2026-09-22 record shows the map stays empty until a popup HwndSource appears. Witness: `AnnouncementSeamCensus` (the raise shape, no `RaiseNotificationEvent` in authored code) and the FlaUI journey `Announcements_ReachADesktopScopedListenerFromLaunch` (desktop-root subscription created before launch; scan lines and "Right pane hidden." received without a menu having opened; mutation-verified against the gated call).

**R-2 — Keyboard selection never takes focus out of the Files tree (PR 2, #1245).** A selection-driven open (tree arrows, filter results, dual pane) opens the note with `requestEditorFocus: false`; `FocusedRegion()` stays `Files`. Enter opens the selected row and moves focus into the note; Ctrl+Enter opens it in a new tab; Space toggles the focused row's batch check box and the row's `ItemStatus` reflects it; the check box is `Focusable="False"`. The three gestures are chord rows (`windows.filesTree.*`) and the tree's help is composed from them (contract 39 N-1/N-3). Evidence: `FilesSidebarViewModel.cs:555–586`, `WorkspaceViewModel.cs:2044` (`OpenPathCore(requestEditorFocus)`), `WorkspaceTemplates.xaml:31–36`. Witness: unit facts on the setter and the open commands; FlaUI `FilesTree_ArrowsKeepFocusEnterOpens`.

**R-3 — Tag activation composes core's grammar (PR 2, #1250).** `ActivateTag(tag)` writes `#tag` for a tag without whitespace and a tag scope (`filter_files`'s `scope_tag`) otherwise, exactly as `AppState.swift:7120–7131`; the field's help text names only prefixes `parse_sidebar_filter` accepts. Evidence: `crates/slate-core/src/sidebar_filter.rs:153–229`, `FilesSidebarViewModel.cs:834`. Witness: `ActivateTag_ComposesCoreGrammar`; `ReadingTagSearchRerouteTests` flipped to `#atag`; the FlaUI tag-tree journey finds the fixture's tagged note.

**R-4 — Every reachable item is named; layout containers are not control elements (PR 3, #1246).** No UIA Name in the shell matches a dotted .NET type name or a record dump. Item containers the reader lands on carry `AutomationProperties.Name` from the item's speakable name; containers that only wrap the real stop answer `IsControlElementCore => false`. Every `AccessibleDataGrid.Bind` call passes `rowAutomationName`. Evidence: the ten sites in spec §4.1; `Grids/AccessibleDataGrid.cs:215–224, 468–480`. Witness: the name census inside `AssertAxeClean` (every journey), the XAML container census, `AccessibleDataGridTests` per caller.

**R-5 — Arrow keys stay in their region (PR 4, #1247).** `MainMenu` sets `KeyboardNavigation.DirectionalNavigation="None"` beside `Focusable="False"` and `TabNavigation="None"`; no focus fallback lands on a bare `Selector` when it has items (a shared helper focuses the current or first item); the right-pane boundary lands on the shown leaf's first focusable stop when it has one; radio groups (`Tasks Review` filters, the canvas and graph view switchers) cycle with arrows and check the radio that receives keyboard focus; `ContentPaneBorder` contains directional navigation. Evidence: `MainWindow.xaml:79–85`, `MainWindow.ShellRegions.cs:180–188`, `MainWindow.xaml.cs:641–685`, `MainWindow.Citations.cs:343, 363, 654`. Witness: `MenuBarCensus`, the fallback census, FlaUI `RegionStops_ArrowsStayInRegion`.

**R-6 — A sheet fences the keyboard (PR 5, #1248).** Every overlay with `FocusManager.IsFocusScope="True"` carries `SheetKeyboardFence`: Tab and Shift+Tab traverse inside the overlay's cycle and are handled there; `EditingCommands.TabForward`/`TabBackward` never leave the overlay. No keystroke typed inside a sheet changes the note behind it. Evidence: `MainWindow.xaml:3751–3754` and the fifteen sibling overlays; the CommandManager focus-scope re-targeting (dotnet/wpf) and AvalonEdit's unconditional `TabForward` binding. Witness: `SheetFenceCensus`; the WPF-hosted `TabInsideASheetNeverReachesTheEditor`; FlaUI `Templates_PromptTabTraversalStaysInTheSheet`.

**R-7 — A failed save speaks a sentence, never a diagnostic (PR 6, #1249; amends contract 38 D-10).** A `VaultException.WriteConflict` posts `NoteSaveConflict(filename)` → "Save blocked. {filename} was modified externally. Your edits remain in the editor." and the inline status says the same; other failures keep `NoteSaveBlocked(filename, detail)`. No announcement or status text contains a content hash, an `@field=` token or a modification time. Evidence: `WorkspaceViewModel.cs:682–684`, `crates/slate-core/src/lib.rs:250–262`, `a11y.rs:1872–1877`. Witness: `RecoveryAnnouncementTests.ConflictingSaveSpeaksTheConflictSentence`; corpus rows; the trigger ledger.

**R-8 — A popover or sheet announces its outcome when it opens (PR 6, #1251).** Core variants `CitationPopoverShown`, `EmbedPreviewShown`, `EmbedPreviewUnavailable`, `CitationSummaryShown`, `CitationDetailsShown` (High) post at the moment the outcome exists (citation popover and both sheets at open; embed previews when the result lands). Read-only preview text (`EditorEmbedPreview.cs`, the "Preview content" box) is caret-navigable (`IsReadOnlyCaretVisible = true`, non-focusable scroll host). Evidence: `EditorInteractions.cs:1286, 1385–1395, 1679–1683, 2772–2776, 3162–3171`, `WorkspaceViewModel.Citations.cs:255–314`, `Panels/AddPropertyViewModel.cs:57` (the pattern). Witness: `W2EditorInteractionTests` trigger facts; `A11yCorpusCensus`; the journey's two-line read.

**R-9 — A rescan is the reconciliation, and it says what it found (PR 7, #1252; amends contract 38 D-3).** `VaultLifecycleViewModel.RescanAsync` runs core's incremental scan on the open session, then refreshes the sidebar, replaces the Quick Open list, sends the scan-finished notifies, and posts `VaultRescanFinished(reason, indexed, removed)`: always for Files Sidebar → Refresh, only when something changed for the foreground rescan (window re-activated after ≥ 2 s away, ≥ 5 s since the last rescan, no modal, no scan/import/trash in flight). A file created, modified or deleted outside Slate is reflected after one rescan. `VaultScanFinished` speaks both counts ("{seen} files, {indexed} new or changed"). Evidence: `session.rs:2868`, `VaultLifecycleViewModel.cs:455–472, 729–797`, `FilesSidebarViewModel.TreeOperations.cs:56, 449–452`, `QuickSwitcherViewModel.cs:242–259`, `vault/fs.rs:973–978`. Witness: Rust rescan facts; Windows unit facts; FlaUI `ExternalFiles_AppearAfterRefresh`.

**R-10 — The reading surface is the editor stop (PR 8, #1253).** `FocusEditorPane` focuses the visible `ReadingSurface` for a reading-mode tab before falling back; `ToggleViewMode` requests editor focus in both directions; F6/Shift+F6, Quick Open and the palette's close fallback land there. Evidence: `MainWindow.xaml.cs:1679–1742`, `MainWindow.ShellRegions.cs:133–145`, `Reading/ReadingSurface.cs:199–205, 426–440`. Witness: the WPF-hosted arm fact; FlaUI `ReadingView_IsTheEditorStop`.

**R-11 — The palette announces one selection per query change and renders at typing speed (PR 9, #1254).** The `ItemsSource` swap runs inside the selection-sync guard and the list is not synchronized with its current item, so the view never re-selects on the user's behalf; the view model's `SetSelection` is the only `PaletteCommandSelected` source (contract 28 P7/P10). Recompute cost scales linearly in row count; grouped rows virtualize. Evidence: `MainWindow.Palette.cs:206–255`, `CommandPaletteViewModel.cs:708–769, 878`. Witness: the view-hosted selection fact; `RecomputeScalesLinearlyInRowCount`; FlaUI `Palette_TypingAnnouncesOnlyTheFinalCountAndSelection`; the recorded before/after timings.

**R-12 — The board's arrows move the seat and its cards have a menu (PR 10, #1255, #1256; amends contract 34 D15 and lifts G2D-12).** On the Visual projection Down/Up run `SelectAdjacent` (reading order, End/Start of canvas at the bounds) and Right/Left follow connections; outline rows and the renderer carry a persistent `ContextMenu` from construction, mutated per request, keyboard requests targeting the focused row or the seated card. Shift+F10 and the Applications key open the same menu and never the tab's. Evidence: `Canvas/CanvasSurfaceView.cs:403–411`, `Canvas/CanvasNavigator.cs:183–186, 1079–1127, 1603–1627, 1659–1668`, `Canvas/CanvasOutlineView.cs:354–358, 1032–1047`, `Grids/AccessibleDataGrid.cs:1289–1327`, `Graph/GraphDiagramView.cs:1364–1400`. Witness: `CanvasNavigatorTests` visual facts; `CanvasContextMenuTests` renderer plan and keyboard-request facts; the two FlaUI journeys.

**R-13 — The Connections leaf documents its activation truthfully (PR 11, #1257).** Row activation opens the note (Ctrl+Enter in a new tab) and only Show connections re-roots (contract 35 B-9, `w6_2_graph_spec.md:198`, mac parity); the checklist says so; the row hint composes both gestures from chord rows (contract 39 N-1/N-2). Evidence: `Graph/ConnectionsLeafViewModel.cs:1036–1044, 1100–1113`, `Graph/ConnectionsPhrase.cs:49`. Witness: the hint fact; `ChordSpeechAuditCensus`; `WcMatrixEvidenceCensus` over the corrected row.

## Accepted risks, divergences and owner decisions (off-limits for re-litigation)

- **OD-1 (2026-09-22):** no live watcher in this wave; Refresh and foreground rescans are the reconciliation. **AR-3:** changes made while Slate is in the foreground and no rescan runs stay invisible until the next Refresh or activation; the follow-up issue for a core watcher is filed from PR 7.
- **OD-2 (2026-09-22):** keyboard selection keeps opening the note (mac parity) with focus in the tree; Enter/Ctrl+Enter move focus; Space toggles the batch box. **AR-5:** a pointer single-click also keeps focus in the tree; mouse users click into the note.
- **OD-3 (2026-09-22):** the visual board carries the derived card menu (G2D-12 lifted).
- **OD-4 (2026-09-22):** eleven grouped feature PRs plus the docs PR.
- **OD-5 (2026-09-23, defaults amendable at review):** board Right/Left follow connections; the conflict sentence reuses mac's wording minus the dialog clause; #1257 is a checklist correction plus a composed hint; the scan-finished copy states both counts; #1244 is fixed at the raise call, not with a transient window.
- **AR-1:** the FlaUI desktop-root subscription may not reproduce NVDA's advise gap; the journey ships only once it discriminates (spec §2.4), escalating to an `IUIAutomation6` handler group.
- **AR-2:** mac does not post the five new popover/sheet variants in this wave; the corpus mirrors them and the mac owner decides.
- **AR-4:** the palette ratio fact tolerates 2.5× on doubling; absolute timings are recorded, not asserted.
- **AR-6:** a keyboard request on a container with no current item still lands on the container when it has no items (an empty list's notice is the stop where one exists).

## Review record

Each PR appends its rounds here, in its own section, newest last: the prompt's invariant list, the findings with dispositions (taken / refuted with evidence / accepted risk), the fix commits, and the verdict.

### PR 0 — docs (this document and the spec)

(pending)

### PR 1 — #1244 announcements from launch

(pending)

### PR 2 — #1245 + #1250 Files sidebar

(pending)

### PR 3 — #1246 accessible names

(pending)

### PR 4 — #1247 arrows stay in the region

(pending)

### PR 5 — #1248 sheet keyboard fence

(pending)

### PR 6 — #1249 + #1251 failed-save sentence, popover outcomes

(pending)

### PR 7 — #1252 rescan on Refresh and foreground

(pending)

### PR 8 — #1253 reading surface is the editor stop

(pending)

### PR 9 — #1254 palette selection and speed

(pending)

### PR 10 — #1255 + #1256 canvas board arrows and menus

(pending)

### PR 11 — #1257 Connections leaf documentation

(pending)
