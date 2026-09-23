# W7-7 NVDA matrix remediation — feature contracts & registers (#1244–#1257)

Scope: [W7-7 executable spec](18_windows_port/specs/w7_7_nvda_matrix_remediation_spec.md). This contract-only commit precedes every implementation PR and every review round, per [24_red_team_protocol.md](24_red_team_protocol.md). Contract numbering is per-wave (`R-n`); each PR cites its numbers in its review prompts and appends its rounds to its own section below. A **planned witness** is a test or journey the owning PR has yet to land: it becomes evidence only when that PR's round record names its mutation run. `W77RemediationDocsCensus` pins the spec-section ↔ contract mapping (each PR's design section cites exactly its own `R-n`). Paths are relative to `apps/slate-windows/src/SlateWindows` unless stated.

## Contracts

**R-1 — Announcements reach a listening client from the first frame (PR 1, #1244).** The one production raiser (`AccessibilityNotificationDispatcher.cs`, contract 38 D-2) raises through `AutomationInteropProvider.RaiseAutomationEvent(AutomationElementIdentifiers.NotificationEvent, provider, NotificationEventArgs)` with the provider obtained from the status element's peer via `AutomationPeer.ProviderFromPeer`, guarded only by `AutomationInteropProvider.ClientsAreListening`. Evidence: WPF's `RaiseNotificationEvent` is gated on `EventMap.HasRegisteredEvent(Notification)`, a process-static map populated only by `ElementProxy.AdviseEventAdded`; the 2026-09-22 record shows the map stays empty until a popup HwndSource appears. Planned witness: `AnnouncementSeamCensus` (the raise shape, no `RaiseNotificationEvent` in authored code) and the FlaUI journey `Announcements_ReachADesktopScopedListenerFromLaunch` (desktop-root subscription created before launch; scan lines and "Right pane hidden." received without a menu having opened; mutation-verified against the gated call).

**R-2 — Keyboard selection never takes focus out of the Files tree (PR 2, #1245).** A selection-driven open (tree arrows, filter results, dual pane) opens the note with `requestEditorFocus: false`; `FocusedRegion()` stays `Files`. Enter opens the selected row and moves focus into the note; Ctrl+Enter opens it in a new tab; Space toggles the focused row's batch check box and the row's `ItemStatus` reflects it; the check box is `Focusable="False"`. The three keys are routed from the focused row container, never from "the tree has focus": inside the inline rename box Enter commits and Escape cancels the rename, and the same Enter/Ctrl+Enter serve the filter-results and dual-pane lists. The three gestures are chord rows (`windows.filesTree.*`) and the tree's help is composed from them (contract 39 N-1/N-3). Evidence: `FilesSidebarViewModel.cs:555–586`, `WorkspaceViewModel.cs:2044` (`OpenPathCore(requestEditorFocus)`), `WorkspaceTemplates.xaml:31–36`. Planned witness: unit facts on the setter and the open commands; FlaUI `FilesTree_ArrowsKeepFocusEnterOpens`.

**R-3 — Tag activation composes core's grammar (PR 2, #1250).** The split is core's: `sidebar_filter::tag_filter_activation(tag)` answers `#tag` for a tag without whitespace and a tag scope (`filter_files`'s `scope_tag`) otherwise — the rule `AppState.swift:7120–7131` applies host-side — and `ActivateTag(tag)` applies that answer, never re-implementing it (protocol rule, `24_red_team_protocol.md:64`). The scope is complete filter state: `IsFilterActive` is true while a scope is set, the query runs with an empty text, the scope is visible and spoken through the filter status, `ClearFilterCommand` executes from `IsFilterActive` and clears text and scope atomically, typing keeps the scope, the request identity `(query, scopeTag)` keys the stale-result guard and the announcement de-duplication, and a zero-result summary names the scope. The field's help text names only prefixes `parse_sidebar_filter` accepts. Evidence: `crates/slate-core/src/sidebar_filter.rs:153–229`, `FilesSidebarViewModel.cs:834`, `FilesSidebarViewModel.Filter.cs:55–80`. Planned witness: the Rust facts for `tag_filter_activation`; `ActivateTag_ComposesCoreGrammar`; the whitespace-tag hosted fact over a temp vault through the real FFI; `ReadingTagSearchRerouteTests` flipped to `#atag`; the FlaUI tag-tree journey finds the fixture's tagged note.

**R-4 — Every reachable item is named; layout containers are not control elements (PR 3, #1246).** No UIA Name in the shell matches a dotted .NET type name or a record dump. Item containers the reader lands on carry `AutomationProperties.Name` from the item's speakable name; containers that only wrap the real stop answer `IsControlElementCore => false`. Every `AccessibleDataGrid.Bind` call passes `rowAutomationName`. Evidence: the ten sites in spec §4.1; `Grids/AccessibleDataGrid.cs:215–224, 468–480`. Planned witness: the name census inside `AssertAxeClean` (every journey), the XAML container census, `AccessibleDataGridTests` per caller.

**R-5 — Arrow keys stay in their region (PR 4, #1247).** `MainMenu` sets `KeyboardNavigation.DirectionalNavigation="None"` beside `Focusable="False"` and `TabNavigation="None"`; no focus fallback lands on a bare `Selector` when it has items (a shared helper focuses the current or first item); the right-pane boundary lands on the shown leaf's first focusable stop when it has one; radio groups (`Tasks Review` filters, the canvas and graph view switchers) cycle with arrows and check the radio that receives keyboard focus, and the surface follows (the filter, the projection, the view); `ContentPaneBorder` contains directional navigation. Evidence: `MainWindow.xaml:79–85`, `MainWindow.ShellRegions.cs:180–188`, `MainWindow.xaml.cs:641–685`, `MainWindow.Citations.cs:343, 363, 654`. Planned witness: `MenuBarCensus`, the fallback census, FlaUI `RegionStops_ArrowsStayInRegion` with a per-surface radio block (Tasks, canvas, graph: focused radio checked, surface changed, next F6 landing).

**R-6 — A sheet fences Tab (PR 5, #1248).** Every overlay with `FocusManager.IsFocusScope="True"` carries `SheetKeyboardFence`: Tab and Shift+Tab traverse inside the overlay's cycle and are handled there; `EditingCommands.TabForward`/`TabBackward` never leave the overlay, so no Tab typed inside a sheet changes the note behind it. Shell-command suppression while a sheet is up stays the shipped modal-owner swallow (contract 30 TR-7, #1118); the fence adds no second mechanism and the two coexist. Evidence: `MainWindow.xaml:3751–3754` and the fifteen sibling overlays; the CommandManager focus-scope re-targeting (dotnet/wpf) and AvalonEdit's unconditional `TabForward` binding. Planned witness: `SheetFenceCensus`; the WPF-hosted `TabInsideASheetNeverReachesTheEditor` (plus its coexistence assertions: a text-editing chord inside the sheet still works, a forbidden shell chord does not execute behind the scrim); FlaUI `Templates_PromptTabTraversalStaysInTheSheet`.

**R-7 — A failed save speaks a sentence, never a diagnostic (PR 6, #1249; amends contract 38 D-10).** A `VaultException.WriteConflict` posts `NoteSaveConflict(filename)` → "Save blocked. {filename} was modified externally. Your edits remain in the editor." and the inline status says the same; other failures keep `NoteSaveBlocked(filename, detail)`. No announcement or status text contains a content hash, an `@field=` token or a modification time. Evidence: `WorkspaceViewModel.cs:682–684`, `crates/slate-core/src/lib.rs:250–262`, `a11y.rs:1872–1877`. Planned witness: `RecoveryAnnouncementTests.ConflictingSaveSpeaksTheConflictSentence`; corpus rows; the trigger ledger.

**R-8 — A popover or sheet announces its outcome when it opens (PR 6, #1251).** Core variants `CitationPopoverShown`, `EmbedPreviewShown`, `EmbedPreviewUnavailable`, `CitationSummaryShown`, `CitationDetailsShown` (High) post at the moment the outcome exists (citation popover and both sheets at open; embed previews when the result lands). Every sentence is rendered by core: `CitationPopoverShown` carries the preview's raw speech (or its semantic fields) and `a11y.rs` applies the "Citation." prefix rule; the host never prefixes and the popover's UIA Name is the rendered text. Read-only preview text (`EditorEmbedPreview.cs`, the "Preview content" box) is caret-navigable (`IsReadOnlyCaretVisible = true`, non-focusable scroll host). Evidence: `EditorInteractions.cs:1286, 1385–1395, 1679–1683, 2772–2776, 3162–3171`, `WorkspaceViewModel.Citations.cs:255–314`, `Panels/AddPropertyViewModel.cs:57` (the pattern). Planned witness: `W2EditorInteractionTests` trigger facts; `A11yCorpusCensus`; the journey's two-line read.

**R-9 — A rescan is the reconciliation, and it says what it found (PR 7, #1252; amends contract 38 D-3).** `VaultLifecycleViewModel.RescanAsync` runs core's incremental scan on the open session; "modified" means the committed content hash differs (never `files_indexed`, which counts reads), a unique hash-matched removed + created pair is `Renamed`, and the scan emits a per-path `notify_file_change` for every created, modified, deleted or renamed path it commits, so the host's path-aware funnel (`HandleFileChange`) reconciles open tabs — a clean tab reloads through `WorkspaceViewModel.ReloadCleanTab`, a dirty tab keeps its buffer — reading dependencies and Quick Open; counts alone reconcile nothing open; a request during a run coalesces with Explicit dominating Foreground; then the sidebar refreshes, the Quick Open list is replaced, the graph notify is sent, and exactly one completion sentence is posted: `VaultRescanFinished(reason, indexed, removed)`, or `VaultRescanIncomplete(errors)` when the walk was partial (a partial scan never says "No changes"). A rescan never speaks `VaultScanStarted`/`VaultScanFinished` (reason-aware progress policy). Explicit Refresh always announces; the foreground rescan (window re-activated after ≥ 2 s away, ≥ 5 s since the last rescan, no modal, no scan/import/trash in flight) announces only when something changed or the scan was incomplete, and its unchanged run posts nothing at all. A file created, modified or deleted outside Slate is reflected after one rescan, in the tree, Quick Open and any clean open tab; a dirty open tab keeps its edits. `VaultScanFinished` speaks both counts ("{seen} files, {indexed} new or changed") on both hosts (OD-6). Evidence: `session.rs:2868`, `VaultLifecycleViewModel.cs:455–472, 729–797`, `FilesSidebarViewModel.TreeOperations.cs:56, 449–452`, `QuickSwitcherViewModel.cs:242–259`, `vault/fs.rs:973–978`. Planned witness: Rust rescan facts; Windows unit facts; FlaUI `ExternalFiles_AppearAfterRefresh`.

**R-10 — The reading surface is the editor stop (PR 8, #1253).** `FocusEditorPane` asks the visible `ReadingSurface` of a reading-mode tab to land focus before falling back — immediately when the current model's projection is applied (never on block count: the loading placeholder is a block), otherwise as a pending landing addressed to that model generation, fulfilled when its content is applied and withdrawn on rebind, focus departure or unload, so neither an empty surface nor the placeholder is ever focused and no late apply steals focus; `ToggleViewMode` requests editor focus in both directions; F6/Shift+F6, Quick Open and the palette's close fallback land there with a readable document. The W7-6 F6 ring spec's editor row records it. Evidence: `MainWindow.xaml.cs:1679–1742`, `MainWindow.ShellRegions.cs:133–145`, `Reading/ReadingSurface.cs:199–205, 424–440`. Planned witness: the WPF-hosted arm facts (content present; content arriving after the request), asserting document content after the handoff; FlaUI `ReadingView_IsTheEditorStop`.

**R-11 — The palette announces one selection per query change and renders at typing speed (PR 9, #1254).** The `ItemsSource` swap runs inside the selection-sync guard and the list is not synchronized with its current item, so the view never re-selects on the user's behalf; the view model's `SetSelection` is the only `PaletteCommandSelected` source (contract 28 P7/P10). Recompute cost scales linearly in row count; grouped rows virtualize. Evidence: `MainWindow.Palette.cs:206–255`, `CommandPaletteViewModel.cs:708–769, 878`. Planned witness: the view-hosted selection fact; `RecomputeScalesLinearlyInRowCount`; FlaUI `Palette_TypingAnnouncesOnlyTheFinalCountAndSelection`; the recorded before/after timings.

**R-12 — The board's arrows move the seat and its cards have a menu (PR 10, #1255, #1256; amends contract 34 D15 and lifts G2D-12).** On the Visual projection Down/Up run `SelectAdjacent` (reading order, End/Start of canvas at the bounds) and Right/Left follow connections, and after every Visual move or follow the presenter's reveal pans the renderer to contain the seat (the navigator's `FocusRow` path answers false for Visual); outline rows and the renderer carry a persistent `ContextMenu` from construction, mutated per request, keyboard requests targeting the focused row or the seated card — a menu action acts on that card and no other. Shift+F10 and the Applications key open the same menu and never the tab's. Evidence: `Canvas/CanvasSurfaceView.cs:403–411`, `Canvas/CanvasNavigator.cs:183–186, 1079–1127, 1603–1627, 1659–1668`, `Canvas/CanvasOutlineView.cs:354–358, 1032–1047`, `Grids/AccessibleDataGrid.cs:1289–1327`, `Graph/GraphDiagramView.cs:1364–1400`. Planned witness: `CanvasNavigatorTests` visual facts including the recorded reveal (arrows and the palette's `NextCard`/`PreviousCard` route); `CanvasContextMenuTests` renderer plan and keyboard-request facts (a fresh, non-first row's menu is the row's); the two FlaUI journeys — the reveal block starts from a destination outside the viewport and asserts the viewport changed and the destination is contained, a move and a follow separately — and Toggle Mark from the keyboard menu changed only the seated card / focused row.

**R-13 — The Connections leaf documents its activation truthfully (PR 11, #1257).** Row activation opens the note (Ctrl+Enter in a new tab) and only Show connections re-roots (contract 35 B-9, `w6_2_graph_spec.md:198`, mac parity); the checklist says so; the row hint composes both gestures from chord rows (contract 39 N-1/N-2). Evidence: `Graph/ConnectionsLeafViewModel.cs:1036–1044, 1100–1113`, `Graph/ConnectionsPhrase.cs:49`. Planned witness: the hint fact; `ChordSpeechAuditCensus`; `WcMatrixEvidenceCensus` over the corrected row.

## Accepted risks, divergences and owner decisions (off-limits for re-litigation)

- **OD-1 (2026-09-22):** no live watcher in this wave; Refresh and foreground rescans are the reconciliation. **AR-3:** changes made while Slate is in the foreground and no rescan runs stay invisible until the next Refresh or activation; the follow-up issue for a core watcher is filed from PR 7.
- **OD-2 (2026-09-22):** keyboard selection keeps opening the note (mac parity) with focus in the tree; Enter/Ctrl+Enter move focus; Space toggles the batch box. **AR-5:** a pointer single-click also keeps focus in the tree; mouse users click into the note.
- **OD-3 (2026-09-22):** the visual board carries the derived card menu (G2D-12 lifted).
- **OD-4 (2026-09-22):** eleven grouped feature PRs plus the docs PR.
- **OD-6 (2026-09-23):** `VaultScanFinished` gaining `files_seen` is a cross-host signature change: mac's post site passes its count and speaks the same copy (the mac lane builds and arbitrates); the spec's mac non-goal is carved out for this one variant. Because mac's helper builds reports with `filesSeen == filesIndexed` today, PR 7 ships a mac fact with distinct counts (9 seen, 2 indexed) over the event payload and the rendered sentence.
- **AR-7:** the rescan keeps the `(mtime, size)` short-circuit, so an external same-size edit that preserves mtime stays invisible until the file is next read; the delta is authoritative for every file the scan reads.
- **OD-5 (2026-09-23, defaults amendable at review):** board Right/Left follow connections; the conflict sentence reuses mac's wording minus the dialog clause; #1257 is a checklist correction plus a composed hint; the scan-finished copy states both counts; #1244 is fixed at the raise call, not with a transient window.
- **AR-1:** the FlaUI desktop-root subscription may not reproduce NVDA's advise gap; the journey ships only once it discriminates (spec §2.4), escalating to an `IUIAutomation6` handler group.
- **AR-2:** mac does not post the five new popover/sheet variants in this wave; the corpus mirrors them and the mac owner decides.
- **AR-4:** the palette ratio fact tolerates 2.5× on doubling; absolute timings are recorded, not asserted.
- **AR-6:** a keyboard request on a container with no current item still lands on the container when it has no items (an empty list's notice is the stop where one exists).

## Review record

Each PR appends its rounds here, in its own section, newest last: the prompt's invariant list, the findings with dispositions (taken / refuted with evidence / accepted risk), the fix commits, and the verdict.

### PR 0 — docs (this document and the spec)

**Round 1 (2026-09-23; codex `gpt-5.6-sol`, effort xhigh; base `origin/main` 937dd3dd, branch scope).** Prompt: challenge the plan per PR — is the diagnosed mechanism supported by the cited code, does the design regress focus routing / mac parity / contracts 28, 30, 34, 35, 38, 39 and the W7-6 F6 ring, which named tests could pass with the bug present, what an independent developer still needs; weighted PR 1, 2, 5, 7; exhaustive single pass; OD-1..5 and AR-1..6 off-limits. Verdict: `needs-attention` (12 high, 2 medium; "PR 1's ungated UIA mechanism is supported; no separate blocker for PRs 3, 9, 11"). Dispositions — all taken, none refuted, no new accepted risk:

1. PR 7 — foreground rescans cannot be silent through the progress path (`ScanAnnouncementGate.Started/Finished` are unconditional): reason-aware progress policy, exactly one completion sentence, whole-event-sequence assertions (spec §8.2–8.4; R-9).
2. PR 7 — count-only rescans cannot reconcile open workspace state, and `HandleIndexPhase` notifies only the graph: the rescan emits per-path `notify_file_change` through the `HandleFileChange` funnel; open-tab facts (§8.2 e; R-9).
3. PR 7 — a partial scan could be announced as a no-op: `complete` on the report and `VaultRescanIncomplete` (§8.3; R-9).
4. PR 2 — whitespace tag activation is inert under the current filter state (`IsFilterActive` and `ScheduleFilter` ignore a scope) and the split re-implemented core's rule host-side: core `tag_filter_activation`, the scope as filter state with visibility and clearing, a hosted fact through the real FFI (§3.3; R-3).
5. PR 2 — the key arm could steal Enter from the inline rename: routing from the focused row container, rename commit/cancel facts, Enter/Ctrl+Enter on all three lists (§3.2; R-2).
6. PR 5 — R-6 promised more than the fence delivers and the journey's opener was Bulk Rename: R-6 narrowed to Tab/Shift+Tab with the shipped TR-7 swallow named, coexistence assertions, real openers (§6; R-6).
7. PR 8 — focusing an empty reading surface recreates the "blank" regression: pending focus landing fulfilled on apply; content asserted after the handoff (§9; R-10).
8. PR 10 — Visual moves had no path to the pan (`FocusRow` answers false for Visual): a presenter reveal after every Visual move/follow; bounding-rectangle assertions (§11; R-12).
9. PR 10 — the board-menu witness could not detect wrong-card targeting: Toggle Mark from a non-first seated card / focused row changes only that one (§11.3; R-12).
10. Spec R-n citations shifted by one from PR 4 on, and PR 8 named contract 39 for the F6 ring: every mapping corrected, the W7-6 ring spec named, `W77RemediationDocsCensus` added to pin section ↔ contract.
11. PR 4 — the radio witness covered Tasks only: a per-surface block for Tasks, canvas and graph (§5.3; R-5).
12. PR 6 — `CitationPopoverShown { speech }` laundered host-composed text: core renders the sentence with the prefix rule, the host never prefixes, both corpus shapes and two mutations (§7.3; R-8).
13. (medium) Prospective tests read as existing witnesses: "Planned witness" labels and the preamble rule.
14. (medium) PR 7 both required and forbade a mac change: OD-6 and the §14 carve-out.

Fixes: the docs PR's second commit (3e5bd369).

**Round 2 (2026-09-23; same model, effort and base; branch scope).** Prompt: verify each round-1 disposition closes its finding; re-check the PR 7, 2, 8 and 10 redesigns against the code; enumerate anything new. Verdict: `needs-attention` — "the R-to-PR mappings are now correct; dispositions 3, 5, 6, 9, 11, 12, 13 substantively closed; 10 and 14 introduce sibling test gaps; 1, 2, 4, 7, 8 remain incomplete" (8 high, 1 medium). Dispositions — all taken, none refuted; AR-7 recorded:

1. PR 7 — `files_indexed` is not "changed" (a slow-path re-read of unchanged bytes counts) and Windows reports `ctime_ms = 0`: modified = committed content hash differs, `ScanDelta` path lists, the short-circuit kept as AR-7, facts for a touched-unchanged file and a `ctime = 0` provider (§8.2 e; R-9).
2. PR 7 — no producer for `Renamed`: unique hash-matched removed + created pair → `Renamed`, ambiguity → Deleted + Created, a real filesystem rename tested through the scan and listener (§8.2 f; R-9).
3. PR 7 — the funnel never reloads a clean tab (`InvalidateModifiedPath` marks staleness only): `WorkspaceViewModel.ReloadCleanTab` seam with caret/undo/dirty rules, exercised through the real rescan → event → tab path; `WorkspaceViewModel.cs` added to the touch list (§8.2 item 2; R-9).
4. PR 7 — coalescing could drop the Explicit reason: a pending-reason lattice (Explicit dominates Foreground) with both interleaving facts over parked scans (§8.2 item 2, §8.4; R-9).
5. OD-6 — mac could forward `filesIndexed` for both counts: a mac fact with distinct counts over payload and sentence (§8.3; OD-6).
6. PR 2 — the scope was partial state (`ClearFilterCommand` gated on text length; announcement key `(query, total)`; zero-result summary silent on the scope): Clear from `IsFilterActive` clearing both atomically, request identity `(query, scopeTag)`, the zero-result summary names the scope, three facts (§3.3; R-3).
7. PR 8 — block count accepted the loading placeholder and a pending request had no owner: readiness = the current model's applied projection; requests addressed to a generation and withdrawn on rebind / focus departure / unload; placeholder and no-steal facts; the journey asserts the target text and no loading notice (§9.2–9.3; R-10).
8. PR 10 — the reveal witnesses passed with a no-op renderer: an off-screen precondition with viewport-changed and destination-contained assertions, move and follow separately, `NextCard`/`PreviousCard` through the reveal, a hosted renderer fact where hostable (§11.3; R-12).
9. (medium) `W77RemediationDocsCensus` accepted contradictory duplicates: `TryAdd` rejection of duplicate `R-n` / PR sections / review records and exact key-set assertions.

Fixes: the docs PR's third commit. Round 3: pending.

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
