// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Split view shown once a vault is open.
///
/// Three-column layout: file list (#11) | note content (#45) | outline (#46).
/// `NavigationSplitView` with the `(sidebar, content, detail)` initializer
/// gives us system-styled column dividers, keyboard navigation between
/// columns (Cmd+1/2/3 via the System menu), and per-column collapse/
/// resize behaviour for free on macOS.
struct MainSplitView: View {
    @EnvironmentObject private var appState: AppState

    /// WCAG 2.3.1: the search-overlay transition swaps its slide for a
    /// plain crossfade under Reduce Motion. SwiftUI does NOT substitute
    /// custom transitions automatically — the environment flag must be
    /// consulted explicitly (runbook §3b).
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    /// Focus-return target for the two `.alert` modifiers below.
    /// Each alert button action assigns this so AppKit /
    /// VoiceOver routes keyboard + AX focus back to the editor
    /// after the alert dismisses — WCAG 2.4.3 (Focus Order) +
    /// 2.1.2 (No Keyboard Trap). The actual NSTextView focus
    /// transition is handled by AppKit's responder chain; the
    /// SwiftUI `@AccessibilityFocusState` here exists so the
    /// alert-dismiss path has an explicit "this is the
    /// destination" signal the linter and AX engine can verify.
    @AccessibilityFocusState private var alertFocusReturn: AlertFocusTarget?

    /// Single-case enum for the focus-state binding. SwiftUI's
    /// AccessibilityFocusState needs a typed value to bind
    /// `.accessibilityFocused(_:equals:)` against.
    enum AlertFocusTarget: Hashable {
        case editor
    }

    var body: some View {
        // Staged composition (U1-2): the previous single modifier
        // chain exceeded the type-checker's budget as alerts grew.
        // Each stage is a separately-inferred expression; behavior is
        // identical (modifier order preserved: core → alerts → sheets).
        splitViewWithSheets
    }

    private var splitViewCore: some View {
        // Each column gets its own .accessibilityLabel so VoiceOver
        // announces meaningful names when the user navigates between
        // panes (Cmd+Opt+arrow on macOS) instead of falling back to the
        // mangled NSHostingView type names that the Accessibility
        // Inspector flagged with "Element has no description".
        NavigationSplitView {
            FileTreeSidebar()
                .accessibilityLabel("Files sidebar")
        } content: {
            // U1-4 (#456): the center column is the workspace region.
            // With one group and ≤ 1 tab (the U1-4 mirror), WorkspaceView
            // delegates to the same NoteContentView subtree as before —
            // the reparent is structural, not behavioral.
            WorkspaceView()
                .accessibilityLabel("Note content pane")
                .accessibilityFocused($alertFocusReturn, equals: .editor)
        } detail: {
            // The right pane (U4-1, #470): the active leaf's content + the
            // trailing icon rail, replacing the old segmented-picker detail
            // column. Extracted as its own view (like the column it replaced)
            // because MainSplitView.body sits at the type-checker's practical
            // limit, and the workspace close-gate alerts below must live in
            // THIS struct next to the @AccessibilityFocusState they assign
            // (for both the AX behavior and the a11y-check gate).
            if appState.isRightPaneVisible {
                RightPaneView(workspace: appState.workspace)
            } else {
                // Custom collapse (#882, split-views.md:44): NavigationSplitView
                // can't hide the DETAIL column via columnVisibility, so the
                // right pane hides by forcing the detail column to zero width.
                // Reveal via View ▸ Show Right Pane (⌥⌘I). AX-hidden so a
                // collapsed pane is absent from VoiceOver's pane navigation.
                Color.clear
                    .frame(maxWidth: 0, maxHeight: .infinity)
                    .navigationSplitViewColumnWidth(0)
                    .accessibilityHidden(true)
            }
        }
        .navigationTitle(vaultTitle)
        .toolbar { mainToolbar }
        // Toolbar command glyphs render monochrome (U5-1, DoD §B rendering-mode
        // consistency): flat single-weight icons, the command-bar convention.
        // The environment set here propagates into the toolbar items' glyphs.
        // No `glassEffect` on the toolbar — the native `.toolbar` already adopts
        // Liquid Glass on macOS 26 and the system material below 26; only the
        // rendering mode is ours to pin.
        .slateSymbolSurface(.toolbar)
        .overlay(alignment: .top) {
            if appState.isSearchOpen {
                SearchOverlay()
                    // Reduce Motion: crossfade only — SwiftUI has NO
                    // automatic fallback for custom transitions (the
                    // previous comment claimed one; no such behavior
                    // exists). The slide is currently inert anyway
                    // (`isSearchOpen` flips without `withAnimation`),
                    // but the gate keeps a future animated toggle from
                    // shipping a Reduce-Motion violation (WCAG 2.3.1).
                    .transition(
                        reduceMotion
                            ? .opacity
                            : .move(edge: .top).combined(with: .opacity))
            }
        }
        // WriteConflict resolution alert (#64). Three buttons:
        //
        //  - Keep mine — default-styled (visually primary) so the
        //    sighted user can hit Return when they're confident
        //    their version is the one to ship. VoiceOver users
        //    arrow to it explicitly.
        //  - Reload from disk — `.destructive` because it drops
        //    the in-editor buffer.
        //  - Cancel — `.cancel` so Escape (and accidental keyboard
        //    dismissal) leaves the unsaved buffer intact and the
        //    user can think about it. Per issue #64: "accidental
        //    Return doesn't lose data" — Cancel as the cancel-role
        //    means the platform routes keyboard dismissal here.
    }

    private var splitViewWithAlerts: some View {
        splitViewCore
        .alert(
            "File Changed Externally",
            isPresented: writeConflictPresented,
            presenting: appState.currentSaveConflict
        ) { conflict in
            Button("Keep Mine") {
                appState.resolveSaveConflictKeepMine()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                "Save your version, overwriting the external change."
            )
            Button("Reload from Disk", role: .destructive) {
                appState.resolveSaveConflictReloadFromDisk()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                "Discard your unsaved edits and reload the on-disk version."
            )
            Button("Cancel", role: .cancel) {
                appState.resolveSaveConflictCancel()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                "Close this dialog. Your unsaved changes stay in the editor."
            )
        } message: { conflict in
            Text(
                "\(filename(of: conflict.path)) was modified outside the editor since you opened it. Choose how to resolve the conflict."
            )
        }
        // O-5 (#543): the O-2 compaction-error channel's Mac half —
        // non-blocking, once per (path, session) (AppState gates), the
        // core's message verbatim.
        .alert(
            "History Compaction Failed",
            isPresented: compactionFailurePresented,
            presenting: appState.compactionFailure
        ) { _ in
            Button("OK", role: .cancel) {
                appState.compactionFailure = nil
            }
            // #881 suppression (alerts.md:36 — a macOS alert may carry a
            // suppression control). SwiftUI's `.alert` exposes no NSAlert
            // suppression-checkbox API, so the affordance is a button with the
            // standard macOS suppression semantics: it persists the opt-out
            // (PreferencesStore, app-level like editorSpellCheck) and dismisses.
            // This alert isn't actionable (alerts.md:27), so once suppressed
            // the failure routes to the polite, non-interrupting AX
            // announcement in AppState.handleVaultEvent — o_spec §O-2's "never
            // silent" contract stays intact.
            Button("Don't Show Again") {
                appState.suppressCompactionFailureAlert()
                appState.compactionFailure = nil
            }
        } message: { failure in
            Text(failure.message)
        }
        // Save-changes prompt for close-vault / file-switch while
        // dirty (#63 + #64). Save is the default action; Cancel is
        // the cancel-role so platform keyboard dismissal leaves
        // the dirty buffer intact rather than dropping it.
        .alert(
            "Save changes?",
            isPresented: pendingNavigationPresented,
            presenting: appState.pendingNavigation
        ) { _ in
            Button("Save") {
                appState.resolvePendingNavigationSave()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                "Save the current note, then continue with the requested navigation."
            )
            Button("Discard", role: .destructive) {
                appState.resolvePendingNavigationDiscard()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                "Throw away unsaved changes and continue."
            )
            Button("Cancel", role: .cancel) {
                appState.resolvePendingNavigationCancel()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                "Stay on the current note. Unsaved changes remain in the editor."
            )
        } message: { _ in
            let name = filename(
                of: appState.loadedFilePath ?? ""
            )
            Text(
                "Save changes to \(name)?"
            )
        }
        // Tab-close gate (U1-2, #454). Same three-button shape as the
        // navigation gate above so VoiceOver users learn one mental model;
        // scoped to the tab being closed. Lives inline (not in a modifier)
        // so the focus-return assignment sits next to the
        // @AccessibilityFocusState it targets — required for the AX
        // behavior and verified by the a11y-check gate.
        .alert(
            "Save changes?",
            isPresented: pendingTabClosePresented,
            presenting: appState.pendingTabClose
        ) { _ in
            Button("Save") {
                appState.resolveTabCloseSave()
                alertFocusReturn = .editor
            }
            .accessibilityHint("Save the note, then close its tab.")
            Button("Discard", role: .destructive) {
                appState.resolveTabCloseDiscard()
                alertFocusReturn = .editor
            }
            .accessibilityHint("Throw away unsaved changes and close the tab.")
            Button("Cancel", role: .cancel) {
                appState.resolveTabCloseCancel()
                alertFocusReturn = .editor
            }
            .accessibilityHint("Keep the tab open. Unsaved changes remain.")
        } message: { tabID in
            let name = appState.workspace.model.tab(tabID)
                .map { filename(of: appState.workspace.tabPath($0)) } ?? "this note"
            Text("Save changes to \(name) before closing its tab?")
        }
        // Vault-close gate with multiple dirty tabs (U1-2). The single-
        // dirty-tab case routes through the navigation gate, unchanged
        // from pre-tab behavior.
        .alert(
            "Save changes to \(appState.pendingVaultClose ?? 0) notes?",
            isPresented: pendingVaultClosePresented,
            presenting: appState.pendingVaultClose
        ) { _ in
            Button("Save All") {
                appState.resolveVaultCloseSaveAll()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                "Save every tab with unsaved changes, then close the vault.")
            Button("Discard All", role: .destructive) {
                appState.resolveVaultCloseDiscardAll()
                alertFocusReturn = .editor
            }
            .accessibilityHint("Throw away all unsaved changes and close the vault.")
            Button("Cancel", role: .cancel) {
                appState.resolveVaultCloseCancel()
                alertFocusReturn = .editor
            }
            .accessibilityHint("Keep the vault open. Unsaved changes remain.")
        } message: { count in
            Text(
                "\(count) open tabs have unsaved changes. Save them all before closing the vault?"
            )
        }
        // Property-edit conflict alert. Mirrors the editor-save
        // conflict alert above but scopes the message + actions to
        // a single key edit (Milestone I / #168). Same three-button
        // shape so VoiceOver users learn one mental model.
        .alert(
            "Property Edit Blocked",
            isPresented: propertyEditConflictPresented,
            presenting: appState.currentPropertyEditConflict
        ) { conflict in
            Button("Keep Mine") {
                appState.resolvePropertyEditConflictKeepMine()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                "Re-apply your property edit, overwriting the external change."
            )
            Button("Reload from Disk", role: .destructive) {
                appState.resolvePropertyEditConflictReloadFromDisk()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                "Discard your property edit and reload the note from disk."
            )
            Button("Cancel", role: .cancel) {
                appState.resolvePropertyEditConflictCancel()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                "Close this dialog. The properties panel stays as it was."
            )
        } message: { conflict in
            Text(
                "\(filename(of: conflict.path)) was modified outside the editor while you were editing the `\(conflict.key)` property. Choose how to resolve."
            )
        }
        // Non-empty-folder delete confirmation (#860). Files and empty
        // folders keep the no-confirm Finder-parity path; a folder with
        // children is the heavier loss-of-context event, so it prompts.
        // Two-button destructive shape (alerts.md): the destructive verb
        // is explicit ("Move to Trash", never "OK"), Cancel is the
        // cancel-role so Escape/accidental dismissal deletes nothing.
        .alert(
            "Delete folder \u{201C}\(appState.pendingFolderDelete?.name ?? "")\u{201D}?",
            isPresented: pendingFolderDeletePresented,
            presenting: appState.pendingFolderDelete
        ) { _ in
            // Focus returns to the TREE, not the editor: this alert is
            // always staged from a tree operation, and the ⌘⌥←-style
            // focusTreeRegion bridge is the established route (red-team;
            // the shared .editor default fits the other alerts, which
            // ARE editor-scoped).
            Button("Move to Trash", role: .destructive) {
                appState.confirmPendingFolderDelete()
                appState.workspace.focusTreeRegion()
            }
            .accessibilityHint(
                "Move the folder and everything inside it to the Trash."
            )
            Button("Cancel", role: .cancel) {
                appState.cancelPendingFolderDelete()
                appState.workspace.focusTreeRegion()
            }
            .accessibilityHint(
                "Keep the folder. Nothing is deleted."
            )
        } message: { pending in
            Text(
                "Moves the folder and its \(pending.itemCount) \(pending.itemCount == 1 ? "item" : "items") to the Trash."
            )
        }
    }

    private var splitViewWithSheets: some View {
        splitViewWithAlerts
        .sheet(isPresented: $appState.isTemplatePickerOpen) {
            TemplatePicker()
                .environmentObject(appState)
        }
        .sheet(isPresented: templateFlowSheetPresented) {
            TemplatePromptSheet()
                .environmentObject(appState)
        }
        .sheet(isPresented: $appState.isTasksReviewOpen) {
            TasksReviewView()
                .environmentObject(appState)
        }
        .sheet(isPresented: $appState.isAddPropertySheetOpen) {
            AddPropertySheet()
                .environmentObject(appState)
        }
        .sheet(isPresented: $appState.isBulkRenameSheetOpen) {
            BulkRenameSheet()
                .environmentObject(appState)
        }
        // Command palette (Milestone Q #313). Triggered by ⌘⇧P from
        // the SlateMacApp's CommandGroup; closes via Esc or another
        // ⌘⇧P press (handled inside the sheet).
        .sheet(isPresented: $appState.isCommandPaletteOpen) {
            CommandPaletteView()
                .environmentObject(appState)
        }
        .sheet(
            isPresented: Binding(
                get: { appState.activeBaseQueryBuilder != nil },
                set: { presented in
                    if !presented {
                        appState.activeBaseQueryBuilder = nil
                    }
                }
            )
        ) {
            if let model = appState.activeBaseQueryBuilder {
                BaseQueryBuilderSheet(model: model)
                    .environmentObject(appState)
            }
        }
        // Quick switcher (#495). Triggered by ⌘O from the SlateMacApp
        // CommandGroup (#863 moved it from ⌘T); closes via Esc or on
        // opening a file (handled inside the sheet).
        .sheet(isPresented: $appState.isQuickSwitcherOpen) {
            QuickSwitcherView()
                .environmentObject(appState)
        }
        // Citation expand — Reading-mode fallback ONLY (#878). A
        // `CitationsPanel` row now anchors its own `.popover` (the
        // popovers.md:21 fix); this detached presentation survives solely
        // for the inline Reading-mode citation click, whose NSTextView glyph
        // has no SwiftUI anchor a popover could point at. The
        // `expandedCitationRowAnchored` gate keeps it from double-presenting
        // over a panel row's popover. Dismissal routes focus back to the
        // editor (WCAG 2.4.3 + 2.1.2) — the anchorless surface has no row to
        // return to, unlike the anchored panel popover.
        .sheet(
            isPresented: Binding(
                get: {
                    appState.expandedCitation != nil
                        && !appState.expandedCitationRowAnchored
                },
                set: { presented in
                    if !presented {
                        appState.expandedCitation = nil
                        alertFocusReturn = .editor
                    }
                }
            )
        ) {
            if let citation = appState.expandedCitation {
                CitationPopover(
                    citation: citation,
                    onClose: {
                        appState.expandedCitation = nil
                        alertFocusReturn = .editor
                    }
                )
                .environmentObject(appState)
            }
        }
        // Files-citing sheet (#280 right-click action). Populated by
        // `requestFilesCiting` and shown when non-nil. Empty list
        // shows a friendly empty state rather than a blank sheet.
        .sheet(
            isPresented: Binding(
                get: { appState.filesCitingResult != nil },
                set: { presented in
                    if !presented {
                        appState.filesCitingResult = nil
                    }
                }
            )
        ) {
            FilesCitingSheet(
                paths: appState.filesCitingResult ?? [],
                onClose: { appState.filesCitingResult = nil }
            )
        }
        // Citation summary sheet (#282). Cmd+Shift+J opens, the body
        // reads from `currentNoteCitations`. Esc / Done closes via
        // the binding setter.
        .sheet(isPresented: $appState.isCitationSummaryOpen) {
            CitationSummarySheet()
                .environmentObject(appState)
        }
        // Move-to-folder picker (U2-5, #463). Presented when a rename/move
        // command sets `pendingMove`; the sheet's own commit/cancel clears it.
        .sheet(
            isPresented: Binding(
                get: { appState.pendingMove != nil },
                set: { presented in
                    if !presented { appState.pendingMove = nil }
                }
            )
        ) {
            if let move = appState.pendingMove {
                MoveToFolderSheet(move: move)
                    .environmentObject(appState)
            }
        }
        // Link-rewrite partial-failure alert (U2-5, #463). A move/rename stood
        // but some notes' links to it couldn't be updated — list exactly which,
        // never silent (spec §U2-5).
        .alert(
            structuralFailureTitle,
            isPresented: Binding(
                get: { appState.structuralFailureReport != nil },
                set: { presented in
                    if !presented { appState.structuralFailureReport = nil }
                }
            ),
            presenting: appState.structuralFailureReport
        ) { _ in
            Button("OK", role: .cancel) {
                appState.structuralFailureReport = nil
                alertFocusReturn = .editor
            }
        } message: { report in
            Text(structuralFailureMessage(report))
        }
        .onAppear {
            postAccessibilityAnnouncement(
                "Vault \(vaultTitle) opened. Scanning files for the sidebar."
            )
        }
    }

    /// Binding driving the `TemplatePromptSheet` sheet. True
    /// whenever `pendingTemplateFlow` is in a non-idle state
    /// (either prompts or name step). The setter resets the flow
    /// when SwiftUI fires a dismissal from outside (e.g. a swipe-
    /// down on iPad — defensive against future platform parity).
    private var templateFlowSheetPresented: Binding<Bool> {
        Binding(
            get: { appState.pendingTemplateFlow != .idle },
            set: { presented in
                if !presented {
                    appState.cancelTemplateFlow()
                }
            }
        )
    }

    private var compactionFailurePresented: Binding<Bool> {
        Binding(
            get: { appState.compactionFailure != nil },
            set: { if !$0 { appState.compactionFailure = nil } }
        )
    }

    private var writeConflictPresented: Binding<Bool> {
        Binding(
            get: { appState.currentSaveConflict != nil },
            set: { presented in
                if !presented {
                    appState.currentSaveConflict = nil
                }
            }
        )
    }

    private var propertyEditConflictPresented: Binding<Bool> {
        Binding(
            get: { appState.currentPropertyEditConflict != nil },
            set: { presented in
                if !presented {
                    appState.currentPropertyEditConflict = nil
                }
            }
        )
    }

    /// #860: the non-empty-folder delete confirmation. Dismissal from
    /// outside (Escape) routes through the cancel resolver — nothing is
    /// ever deleted by a dismissal.
    private var pendingFolderDeletePresented: Binding<Bool> {
        Binding(
            get: { appState.pendingFolderDelete != nil },
            set: { presented in
                if !presented {
                    appState.cancelPendingFolderDelete()
                }
            }
        )
    }

    private var pendingNavigationPresented: Binding<Bool> {
        Binding(
            get: { appState.pendingNavigation != nil },
            set: { presented in
                if !presented {
                    appState.pendingNavigation = nil
                }
            }
        )
    }


    /// Toolbar contents, extracted from `body` for type-checker budget
    /// (U1-2) — the close-gate alerts must stay inline in `body` next to
    /// `alertFocusReturn`, so the toolbar is what moves. Content verbatim.
    @ToolbarContentBuilder
    private var mainToolbar: some ToolbarContent {
            // Save status indicator: a Modified/Saved label that
            // VoiceOver reads when the toolbar focus lands on it.
            // Hidden when no note is loaded so the toolbar doesn't
            // claim "Saved" against an empty editor.
            if appState.loadedFilePath != nil {
                ToolbarItem(placement: .automatic) {
                    Text(appState.hasUnsavedChanges ? "Modified" : "Saved")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                        .accessibilityLabel(
                            appState.hasUnsavedChanges
                                ? "Modified. Unsaved changes in the editor."
                                : "Saved. Editor matches the on-disk file."
                        )
                }
            }
            ToolbarItem(placement: .automatic) {
                Button {
                    appState.saveCurrentNote()
                } label: {
                    SlateSymbol.save.label()
                }
                // ⌘S lives on File ▸ Save (menu-bar-homed like ⌘F —
                // the #422 lesson: a toolbar-button keyboardShortcut
                // is dead with sidebar focus). Click/AX-activate only.
                .disabled(
                    appState.loadedFilePath == nil
                        || appState.isSaving
                        || !appState.hasUnsavedChanges
                )
                .accessibilityHint(
                    "Save the current note to disk. Command-S."
                )
            }
            ToolbarItem(placement: .automatic) {
                Button {
                    appState.toggleSearchOverlay()
                } label: {
                    SlateSymbol.search.label()
                }
                // #422: ⌘F moved to the menu bar ("Search Vault…").
                // The toolbar registration proved dead with sidebar
                // focus (VO test); the menu equivalent works because
                // nothing claims bare ⌘F in the key-window sweep
                // (which AppKit runs BEFORE the menu — see the note
                // in SlateMacApp). Two registrations of the same
                // equivalent would be ambiguous, so the toolbar
                // button is click/AX-activate only.
                .accessibilityHint(
                    "Opens the search overlay. Command-F to toggle, Escape to close."
                )
            }
            ToolbarItem(placement: .automatic) {
                Button {
                    appState.openTemplatePicker()
                } label: {
                    SlateSymbol.newFromTemplate.label()
                }
                // ⇧⌘N lives on File ▸ New from Template… (#422 — see
                // the Save button above). Click/AX-activate only.
                .accessibilityHint(
                    "Opens the template picker. Command-Shift-N. Escape closes."
                )
            }
            ToolbarItem(placement: .automatic) {
                Button {
                    appState.openTasksReview()
                } label: {
                    SlateSymbol.tasksReview.label()
                }
                // ⌘R lives on View ▸ Show Tasks Review (#422 — see the
                // Save button above; #863 moved the chord off ⇧⌘T).
                // Click/AX-activate only.
                .disabled(appState.currentSession == nil)
                .accessibilityHint(
                    "Opens the vault-wide tasks review. Command-R. Escape closes."
                )
            }
            // Milestone L #282: Citation Summary. Cmd+Shift+J opens
            // the sheet showing N citations / M unique sources for
            // the current note. Disabled with no note selected.
            ToolbarItem(placement: .automatic) {
                Button {
                    appState.isCitationSummaryOpen = true
                } label: {
                    SlateSymbol.citationSummary.label()
                }
                // ⇧⌘J lives on View ▸ Citation Summary (#422 — see
                // the Save button above). Click/AX-activate only.
                .disabled(appState.selectedFilePath == nil)
                .accessibilityHint(
                    "Opens the citation summary for the current note. Command-Shift-J. Escape closes."
                )
            }
            // Milestone L #282: Jump to bibliography from the
            // currently-expanded citation. Command-J. Active only when
            // a citation popover is open.
            ToolbarItem(placement: .automatic) {
                Button {
                    appState.jumpToBibliographyFromExpandedCitation()
                } label: {
                    SlateSymbol.bibliography.label("Jump to Bibliography")
                }
                // ⌘J lives on View ▸ Jump to Bibliography (#422 — see
                // the Save button above). Click/AX-activate only.
                .disabled(appState.expandedCitation == nil)
                .accessibilityHint(
                    "Filters the Bibliography sidebar to the citation's key. Command-J."
                )
            }
            // U4-3 (#472): the "Close Vault" toolbar button was removed here.
            // Close Vault now lives in the bottom-left utility bar's vault-
            // switcher menu (`SidebarUtilityBar`), alongside the File menu and
            // the `slate.vault.close` palette command — all three route through
            // `closeVaultFromUserAction`. The toolbar was the wrong prominence
            // for a destructive-adjacent action (DoD §C action-hierarchy); its
            // command registration + menu path are unchanged.
    }

    private var pendingTabClosePresented: Binding<Bool> {
        Binding(
            get: { appState.pendingTabClose != nil },
            set: { presented in
                if !presented {
                    appState.pendingTabClose = nil
                }
            }
        )
    }

    private var pendingVaultClosePresented: Binding<Bool> {
        Binding(
            get: { appState.pendingVaultClose != nil },
            set: { presented in
                if !presented {
                    appState.pendingVaultClose = nil
                }
            }
        )
    }

    private func filename(of path: String) -> String {
        (path as NSString).lastPathComponent
    }

    private var vaultTitle: String {
        appState.currentVaultURL?.lastPathComponent ?? "Vault"
    }

    // MARK: - Structural-failure alert (U2-5, #463)

    /// Title for the link-rewrite partial-failure alert. Uses the report's verb
    /// + name so the user knows which mutation partially failed.
    private var structuralFailureTitle: String {
        guard let report = appState.structuralFailureReport else {
            return "Couldn't update some links"
        }
        return "Couldn't update links after \(report.verb) of \(report.name)"
    }

    /// Body listing exactly the notes whose links couldn't be updated (spec
    /// §U2-5: "a specific alert listing skipped files — never silent"). When the
    /// report carries no skipped files (a plain move/rename error surfaced via
    /// this path), fall back to `lastError`.
    private func structuralFailureMessage(_ report: AppState.StructuralFailureReport) -> String {
        guard !report.skipped.isEmpty else {
            return appState.lastError
                ?? "The \(report.verb) could not be completed."
        }
        let list = report.skipped.joined(separator: "\n• ")
        let count = report.skipped.count
        return "The \(report.verb) succeeded, but links in "
            + "\(count) \(count == 1 ? "note" : "notes") couldn't be updated "
            + "(they may have been edited externally):\n\n• \(list)"
    }
}
