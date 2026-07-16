// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
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

    /// The item actually owned by each visible Move sheet. AppState may publish
    /// a replacement request before AppKit finishes dismissing the old sheet;
    /// keeping the rendered item in `@State` prevents a rebuilt modifier from
    /// making that old dismissal act on the replacement UUID.
    @State private var presentedMove: AppState.PendingMove?
    @State private var presentedBatchMove: AppState.BatchMove?

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
        case tree
    }

    /// A sheet dismissal can originate outside `MoveToFolderSheet` (Escape,
    /// AppKit, or the host window closing). Read the owner from persistent view
    /// state at dismissal time: SwiftUI rebuilds this modifier as AppState
    /// publishes, so capturing the latest pending UUID in this closure would
    /// let sheet A's late dismissal erase replacement B.
    static func pendingMoveSheetBinding(
        appState: AppState,
        presented: Binding<AppState.PendingMove?>
    ) -> Binding<AppState.PendingMove?> {
        Binding(
            get: { presented.wrappedValue },
            set: { next in
                guard next == nil else {
                    presented.wrappedValue = next
                    return
                }
                guard let owner = presented.wrappedValue else { return }
                presented.wrappedValue = nil
                appState.cancelPendingMove(id: owner.id)
            })
    }

    /// Batch counterpart of `pendingMoveSheetBinding`; batch and single move
    /// presentations have independent UUID owners and must clear independently.
    static func pendingBatchMoveSheetBinding(
        appState: AppState,
        presented: Binding<AppState.BatchMove?>
    ) -> Binding<AppState.BatchMove?> {
        Binding(
            get: { presented.wrappedValue },
            set: { next in
                guard next == nil else {
                    presented.wrappedValue = next
                    return
                }
                guard let owner = presented.wrappedValue else { return }
                presented.wrappedValue = nil
                appState.cancelPendingBatchMove(id: owner.id)
            })
    }

    /// Adopt a request only while no sheet owns the presentation. Replacements
    /// wait behind the visible owner and are promoted from `onDismiss`, after
    /// AppKit has completed the old sheet's lifecycle.
    private func synchronizeMovePresentation(_ pending: AppState.PendingMove?) {
        guard let pending else {
            presentedMove = nil
            return
        }
        guard presentedMove == nil else { return }
        presentedMove = pending
    }

    private func synchronizeBatchMovePresentation(_ pending: AppState.BatchMove?) {
        guard let pending else {
            presentedBatchMove = nil
            return
        }
        guard presentedBatchMove == nil else { return }
        presentedBatchMove = pending
    }

    static func promotePendingMoveAfterDismissal(
        appState: AppState,
        presented: Binding<AppState.PendingMove?>
    ) {
        guard presented.wrappedValue == nil else { return }
        presented.wrappedValue = appState.pendingMove
    }

    static func promotePendingBatchMoveAfterDismissal(
        appState: AppState,
        presented: Binding<AppState.BatchMove?>
    ) {
        guard presented.wrappedValue == nil else { return }
        presented.wrappedValue = appState.pendingBatchMove
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
            FileTreeSidebar(rowPreferences: appState.sidebarPreferences.rowSnapshot)
                .accessibilityLabel("Files sidebar")
                .accessibilityFocused($alertFocusReturn, equals: .tree)
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
        // #880 (HIG toolbars.md:33 — editing apps with many items should
        // let people customize the toolbar): the customizable form. The
        // `id:` names this toolbar's persisted customization; every item
        // below carries its own STABLE `ToolbarItem(id:)` (see `mainToolbar`).
        // The customization editor is reached via `ToolbarCommands()` in
        // SlateMacApp's `.commands` (View ▸ Customize Toolbar… + the
        // Control-click menu). No `.toolbarRole(.editor)`: it would
        // de-emphasize the `vaultTitle` presentation this window relies on
        // (the requirement's "only if it does not disturb existing layout/AX"
        // guard), so the default role stays.
        .toolbar(id: "main") { mainToolbar }
        // Toolbar command glyphs render monochrome (U5-1, DoD §B rendering-mode
        // consistency): flat single-weight icons, the command-bar convention.
        // The environment set here propagates into the toolbar items' glyphs.
        // No `glassEffect` on the toolbar — the native `.toolbar` already adopts
        // Liquid Glass on macOS 26 and the system material below 26; only the
        // rendering mode is ours to pin.
        .slateSymbolSurface(.toolbar)
        .safeAreaInset(edge: .top, spacing: 0) {
            batchTrashQuarantineRecovery
        }
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

    /// Always-mounted recovery for an outcome-unknown Trash operation. The
    /// Files sidebar can be hidden, and the result alert can be dismissed, so
    /// this window-level action is the durable visible and VoiceOver route.
    @ViewBuilder private var batchTrashQuarantineRecovery: some View {
        if let notice = appState.batchTrashQuarantineNotice {
            let disabledReason = appState.structuralMutationDisabledReason
            HStack(alignment: .firstTextBaseline, spacing: Tokens.Spacing.sm) {
                SlateSymbol.warning.decorative
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                Text(notice)
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .fixedSize(horizontal: false, vertical: true)
                Spacer(minLength: 0)
                Button(AppState.BatchTrashCopy.checkAgainLabel) {
                    _ = appState.retryBatchTrashUnknownReconciliation()
                }
                .disabled(disabledReason != nil)
                .accessibilityHint(
                    disabledReason ?? AppState.BatchTrashCopy.checkAgainHint)
                .help(disabledReason ?? AppState.BatchTrashCopy.checkAgainHint)
            }
            .padding(.horizontal, Tokens.Spacing.md)
            .padding(.vertical, Tokens.Spacing.xs)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(Tokens.ColorRole.surfaceSecondary)
            .overlay(alignment: .bottom) {
                Rectangle()
                    .fill(Tokens.ColorRole.separator)
                    .frame(height: 1)
                    .accessibilityHidden(true)
            }
            .accessibilityElement(children: .contain)
        }
    }

    private var splitViewWithSystemAlerts: some View {
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
                appState.saveConflictKeepMineDisabledReason
                    ?? "Save your version, overwriting the external change."
            )
            .disabled(appState.saveConflictKeepMineDisabledReason != nil)
            .help(
                appState.saveConflictKeepMineDisabledReason
                    ?? "Save your version, overwriting the external change."
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
            let question =
                "\(filename(of: conflict.path)) was modified outside the editor since you opened it. Choose how to resolve the conflict."
            if let reason = appState.saveConflictKeepMineDisabledReason {
                Text("\(question)\n\n\(reason)")
            } else {
                Text(question)
            }
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
    }

    private var splitViewWithNavigationAlert: some View {
        splitViewWithSystemAlerts
        // Save-changes prompt for close-vault / file-switch while
        // dirty (#63 + #64). Save is the default action; Cancel is
        // the cancel-role so platform keyboard dismissal leaves
        // the dirty buffer intact rather than dropping it.
        .alert(
            "Save changes?",
            isPresented: pendingNavigationPresented,
            presenting: appState.pendingNavigation
        ) { _ in
            let saveDisabledReason = appState.pendingNavigationSaveDisabledReason
            let discardDisabledReason =
                appState.pendingNavigationDiscardDisabledReason
            Button("Save") {
                appState.resolvePendingNavigationSave()
                alertFocusReturn = .editor
            }
            .disabled(saveDisabledReason != nil)
            .accessibilityHint(
                saveDisabledReason
                    ?? "Save the current note, then continue with the requested navigation.")
            .help(
                saveDisabledReason
                    ?? "Save the current note, then continue with the requested navigation.")
            Button("Discard", role: .destructive) {
                appState.resolvePendingNavigationDiscard()
                alertFocusReturn = .editor
            }
            .disabled(discardDisabledReason != nil)
            .accessibilityHint(
                discardDisabledReason
                    ?? "Throw away unsaved changes and continue."
            )
            .help(
                discardDisabledReason
                    ?? "Throw away unsaved changes and continue.")
            Button("Cancel", role: .cancel) {
                appState.resolvePendingNavigationCancel()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                "Stay on the current note. Unsaved changes remain in the editor."
            )
        } message: { _ in
            pendingNavigationAlertMessage()
        }
    }

    private var splitViewWithTabCloseAlert: some View {
        splitViewWithNavigationAlert
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
            let saveDisabledReason = appState.pendingTabCloseSaveDisabledReason
            let discardDisabledReason =
                appState.pendingTabCloseDiscardDisabledReason
            Button("Save") {
                appState.resolveTabCloseSave()
                alertFocusReturn = .editor
            }
            .disabled(saveDisabledReason != nil)
            .accessibilityHint(
                saveDisabledReason ?? "Save the note, then close its tab.")
            .help(saveDisabledReason ?? "Save the note, then close its tab.")
            Button("Discard", role: .destructive) {
                appState.resolveTabCloseDiscard()
                alertFocusReturn = .editor
            }
            .disabled(discardDisabledReason != nil)
            .accessibilityHint(
                discardDisabledReason
                    ?? "Throw away unsaved changes and close the tab.")
            .help(
                discardDisabledReason
                    ?? "Throw away unsaved changes and close the tab.")
            Button("Cancel", role: .cancel) {
                appState.resolveTabCloseCancel()
                alertFocusReturn = .editor
            }
            .accessibilityHint("Keep the tab open. Unsaved changes remain.")
        } message: { tabID in
            tabCloseAlertMessage(for: tabID)
        }
    }

    private var splitViewWithVaultCloseAlert: some View {
        splitViewWithTabCloseAlert
        // Vault-close gate for multiple dirty tabs or any path-scoped recovery
        // registry. A registry-only draft may have no open tab, so the copy
        // names notes/changes rather than claiming every owner is a tab.
        .alert(
            "Save changes before closing the vault?",
            isPresented: pendingVaultClosePresented,
            presenting: appState.pendingVaultClose
        ) { _ in
            let saveDisabledReason = appState.pendingVaultCloseSaveAllDisabledReason
            let discardDisabledReason =
                appState.pendingVaultCloseDiscardAllDisabledReason
            Button("Save All") {
                appState.resolveVaultCloseSaveAll()
                alertFocusReturn = .editor
            }
            .disabled(saveDisabledReason != nil)
            .accessibilityHint(
                saveDisabledReason
                    ?? "Save every note with unsaved changes, then close the vault.")
            .help(
                saveDisabledReason
                    ?? "Save every note with unsaved changes, then close the vault.")
            Button("Discard All", role: .destructive) {
                appState.resolveVaultCloseDiscardAll()
                alertFocusReturn = .editor
            }
            .disabled(discardDisabledReason != nil)
            .accessibilityHint(
                discardDisabledReason
                    ?? "Throw away all unsaved changes and close the vault.")
            .help(
                discardDisabledReason
                    ?? "Throw away all unsaved changes and close the vault.")
            Button("Cancel", role: .cancel) {
                appState.resolveVaultCloseCancel()
                alertFocusReturn = .editor
            }
            .accessibilityHint("Keep the vault open. Unsaved changes remain.")
        } message: { count in
            vaultCloseAlertMessage(count: count)
        }
    }

    private var splitViewWithAlerts: some View {
        splitViewWithVaultCloseAlert
        // Property-edit conflict alert. Mirrors the editor-save
        // conflict alert above but scopes the message + actions to
        // a single key edit (Milestone I / #168). Same three-button
        // shape so VoiceOver users learn one mental model.
        .alert(
            "Property Edit Blocked",
            isPresented: propertyEditConflictPresented,
            presenting: appState.currentPropertyEditConflict
        ) { conflict in
            let keepMineDisabledReason =
                appState.propertyEditConflictKeepMineDisabledReason
            Button("Keep Mine") {
                appState.resolvePropertyEditConflictKeepMine()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                keepMineDisabledReason
                    ?? "Re-apply your property edit, overwriting the external change.")
            .disabled(keepMineDisabledReason != nil)
            .help(
                keepMineDisabledReason
                    ?? "Re-apply your property edit, overwriting the external change.")
            Button("Reload from Disk", role: .destructive) {
                appState.resolvePropertyEditConflictReloadFromDisk()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                "Discard this property edit and reload properties from disk. Markdown body edits are kept."
            )
            Button("Cancel", role: .cancel) {
                appState.resolvePropertyEditConflictCancel()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                "Close this dialog. The properties panel stays as it was."
            )
        } message: { conflict in
            propertyEditConflictMessage(conflict)
        }
        // Non-empty-folder delete confirmation (#860). Files and empty
        // folders keep the no-confirm Finder-parity path; a folder with
        // children is the heavier loss-of-context event, so it prompts.
        // Two-button destructive shape (alerts.md): the destructive verb
        // is explicit ("Move to Trash", never "OK"), Cancel is the
        // cancel-role so Escape/accidental dismissal deletes nothing.
        .alert(
            AppState.BatchTrashCopy.singleFolderConfirmationTitle(
                name: appState.pendingFolderDelete?.name ?? "folder"),
            isPresented: pendingFolderDeletePresented,
            presenting: appState.pendingFolderDelete
        ) { pending in
            // Focus returns to the TREE, not the editor: this alert is
            // always staged from a tree operation, and the ⌘⌥←-style
            // focusTreeRegion bridge is the established route (red-team;
            // the shared .editor default fits the other alerts, which
            // ARE editor-scoped).
            Button(AppState.BatchTrashCopy.actionLabel, role: .destructive) {
                if appState.confirmPendingFolderDelete(id: pending.id) {
                    appState.workspace.focusTreeRegion()
                }
            }
            .accessibilityHint(
                appState.structuralMutationDisabledReason
                    ?? AppState.BatchTrashCopy.singleFolderActionHint(
                        name: pending.name)
            )
            .disabled(appState.structuralMutationDisabledReason != nil)
            Button(AppState.BatchTrashCopy.cancelLabel, role: .cancel) {
                if appState.cancelPendingFolderDelete(id: pending.id) {
                    appState.workspace.focusTreeRegion()
                }
            }
            .accessibilityHint(
                AppState.BatchTrashCopy.singleFolderCancelHint(
                    name: pending.name)
            )
        } message: { pending in
            VStack(alignment: .leading) {
                Text(
                    AppState.BatchTrashCopy.singleFolderConfirmationMessage(
                        name: pending.name,
                        itemCount: pending.itemCount))
                if let reason = appState.structuralMutationDisabledReason {
                    Text(reason)
                }
            }
        }
        // #852: batch-delete confirmation — the batch analog of the #860 single
        // folder prompt. Staged only when the multi-selection includes a
        // non-empty folder (`requestBatchDelete`); an all-files batch trashes
        // straight through. Same two-button destructive shape (alerts.md):
        // explicit "Move to Trash", cancel-role Cancel so Escape trashes
        // nothing. Focus returns to the tree, like the single prompt.
        .alert(
            AppState.BatchTrashCopy.confirmationTitle(
                itemCount: appState.pendingBatchDelete?.itemCount ?? 0),
            isPresented: pendingBatchDeletePresented,
            presenting: appState.pendingBatchDelete
        ) { pending in
            Button(AppState.BatchTrashCopy.actionLabel, role: .destructive) {
                if appState.confirmPendingBatchDelete(id: pending.id) {
                    appState.workspace.focusTreeRegion()
                }
            }
            .accessibilityHint(
                appState.structuralMutationDisabledReason
                    ?? AppState.BatchTrashCopy.actionHint)
            .disabled(appState.structuralMutationDisabledReason != nil)
            Button(AppState.BatchTrashCopy.cancelLabel, role: .cancel) {
                if appState.handlePendingBatchDeleteKey(.cancel, id: pending.id) {
                    appState.workspace.focusTreeRegion()
                }
            }
            .accessibilityHint(AppState.BatchTrashCopy.cancelHint)
        } message: { pending in
            VStack(alignment: .leading) {
                Text(
                    AppState.BatchTrashCopy.confirmationMessage(
                        itemCount: pending.itemCount,
                        nonEmptyFolderCount: pending.nonEmptyFolderCount))
                if let reason = appState.structuralMutationDisabledReason {
                    Text(reason)
                }
            }
        }
        .background {
            TrashConfirmationReturnKeyMonitor(
                owner: activeTrashConfirmationOwner,
                onReturn: handleTrashConfirmationReturn)
                .frame(width: 0, height: 0)
        }
    }

    private func propertyEditConflictMessage(
        _ conflict: PropertyEditConflict
    ) -> Text {
        let name = filename(of: conflict.path)
        let question: String
        if case .setSource = conflict.action {
            question = name
                + " was modified outside the editor while you were editing "
                + "the properties source. Keep Mine replaces the note’s entire "
                + "YAML frontmatter; Reload keeps Markdown body edits and uses "
                + "the disk properties."
        } else {
            question = name
                + " was modified outside the editor while you were editing the `"
                + conflict.key
                + "` property. Choose how to resolve."
        }
        guard let reason = appState.propertyEditConflictKeepMineDisabledReason else {
            return Text(question)
        }
        return Text(question + "\n\n" + reason)
    }

    private var splitViewWithBatchAttention: some View {
        splitViewWithAlerts.alert(
            batchStructuralAttention.map {
                AppState.BatchStructuralCopy.attention(for: $0).title
            } ?? "Batch Operation",
            isPresented: batchStructuralAttentionPresented,
            presenting: batchStructuralAttention
        ) { result in
            let copy = AppState.BatchStructuralCopy.attention(for: result)
            if appState.batchTrashQuarantineNotice != nil {
                Button(AppState.BatchTrashCopy.checkAgainLabel) {
                    _ = appState.retryBatchTrashUnknownReconciliation()
                }
                .accessibilityHint(AppState.BatchTrashCopy.checkAgainHint)
            }
            if copy.hasDetails {
                Button(AppState.BatchTrashCopy.copyDetailsLabel) {
                    if BatchAttentionDismissal.resolve(
                        id: result.id,
                        dismiss: appState.copyAndDismissBatchStructuralDetails,
                        focus: appState.workspace.focusTreeRegion
                    ) {
                        alertFocusReturn = .tree
                    }
                }
                .accessibilityHint(AppState.BatchTrashCopy.copyDetailsHint)
            }
            Button(AppState.BatchTrashCopy.doneLabel, role: .cancel) {
                if BatchAttentionDismissal.resolve(
                    id: result.id,
                    dismiss: appState.dismissBatchStructuralResult,
                    focus: appState.workspace.focusTreeRegion
                ) {
                    alertFocusReturn = .tree
                }
            }
            .keyboardShortcut(.defaultAction)
            .accessibilityHint("Dismiss this report and return to the files sidebar.")
        } message: { result in
            Text(AppState.BatchStructuralCopy.attention(for: result).inlineMessage)
        }
    }

    private var splitViewWithSheets: some View {
        splitViewWithBatchAttention
        .onAppear {
            synchronizeMovePresentation(appState.pendingMove)
            synchronizeBatchMovePresentation(appState.pendingBatchMove)
        }
        .onChange(of: appState.pendingMove) { _, pending in
            synchronizeMovePresentation(pending)
        }
        .onChange(of: appState.pendingBatchMove) { _, pending in
            synchronizeBatchMovePresentation(pending)
        }
        .sheet(isPresented: $appState.isTemplatePickerOpen) {
            TemplatePicker()
                .environmentObject(appState)
        }
        .sheet(isPresented: templateFlowSheetPresented) {
            TemplatePromptSheet()
                .environmentObject(appState)
        }
        // #879: Tasks Review is no longer a sheet — it's the `Leaf.tasksReview`
        // right-pane leaf (mounted in `RightPaneView`), revealed by
        // `openTasksReview()`. The blocking modal a paginated vault browser
        // shouldn't be (sheets.md:35) is gone.
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
        // command sets `pendingMove`; explicit and system dismissal both clear
        // the exact captured request without erasing a newer presentation.
        .sheet(
            item: Self.pendingMoveSheetBinding(
                appState: appState,
                presented: $presentedMove),
            onDismiss: {
                alertFocusReturn = .tree
                Self.promotePendingMoveAfterDismissal(
                    appState: appState,
                    presented: $presentedMove)
            }
        ) {
            MoveToFolderSheet(move: $0)
                .environmentObject(appState)
        }
        // #852: the BATCH Move-to-folder picker — same sheet, batch initializer.
        // Presented when the file-tree's batch context menu sets
        // `pendingBatchMove`; commit routes the whole selection through
        // `batchMove` (one summary announcement). External dismissal clears
        // only the UUID that actually produced the presented sheet.
        .sheet(
            item: Self.pendingBatchMoveSheetBinding(
                appState: appState,
                presented: $presentedBatchMove),
            onDismiss: {
                alertFocusReturn = .tree
                Self.promotePendingBatchMoveAfterDismissal(
                    appState: appState,
                    presented: $presentedBatchMove)
            }
        ) {
            MoveToFolderSheet(batch: $0)
                .environmentObject(appState)
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
            let sidebarNotice = appState.sidebarVaultPrefsNotice
                .map { " \($0.localizedDescription)" } ?? ""
            postAccessibilityAnnouncement(
                "Vault \(vaultTitle) opened. Scanning files for the sidebar."
                    + sidebarNotice
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
            set: { _ in }
        )
    }

    private var propertyEditConflictPresented: Binding<Bool> {
        Binding(
            get: { appState.currentPropertyEditConflict != nil },
            set: { _ in }
        )
    }

    /// #860: the non-empty-folder delete confirmation. Dismissal from
    /// outside (Escape) routes through the cancel resolver — nothing is
    /// ever deleted by a dismissal.
    private var pendingFolderDeletePresented: Binding<Bool> {
        Binding(
            get: { appState.pendingFolderDelete != nil },
            set: { _ in }
        )
    }

    private var activeTrashConfirmationOwner: TrashConfirmationReturnKey.Owner? {
        if let pending = appState.pendingBatchDelete {
            return .batch(pending.id)
        }
        if let pending = appState.pendingFolderDelete {
            return .singleFolder(pending.id)
        }
        return nil
    }

    private func handleTrashConfirmationReturn(
        _ owner: TrashConfirmationReturnKey.Owner
    ) {
        switch owner {
        case .singleFolder(let id):
            _ = appState.handlePendingFolderDeleteReturnKey(id: id)
        case .batch(let id):
            _ = appState.handlePendingBatchDeleteKey(.returnKey, id: id)
        }
    }

    /// #852: the batch-delete confirmation. Dismissal from outside (Escape)
    /// routes through the cancel resolver — nothing is deleted by a dismissal.
    private var pendingBatchDeletePresented: Binding<Bool> {
        Binding(
            get: { appState.pendingBatchDelete != nil },
            set: { _ in }
        )
    }

    private var batchStructuralAttention: AppState.BatchStructuralResult? {
        guard case .result(let result)? = appState.activeBatchAlertPresentation else {
            return nil
        }
        return result
    }

    /// UUID-checked so a stale SwiftUI dismissal cannot clear a newer report.
    private var batchStructuralAttentionPresented: Binding<Bool> {
        Binding(
            get: { batchStructuralAttention != nil },
            set: { _ in }
        )
    }

    private var pendingNavigationPresented: Binding<Bool> {
        Binding(
            get: { appState.pendingNavigation != nil },
            set: { _ in }
        )
    }


    /// Toolbar contents, extracted from `body` for type-checker budget
    /// (U1-2) — the close-gate alerts must stay inline in `body` next to
    /// `alertFocusReturn`, so the toolbar is what moves. Content verbatim.
    ///
    /// #880 — customizable toolbar (`toolbar(id: "main")`). The item set is
    /// STATIC: every `ToolbarItem(id:)` is emitted on every render with a
    /// STABLE, unique id (persisted in the user's customization). Nothing
    /// is conditionally included/excluded at the ToolbarContent level —
    /// conditionally emitting a `ToolbarItem` corrupts the customization
    /// model (dropped/crashed layout). Per-state visibility uses the
    /// `.hidden(_:)` modifier instead (see `saveStatus`) — Apple's mechanism
    /// for conditional toolbar-item display: it drops an item from the LIVE
    /// toolbar while keeping its stable id available in the customization
    /// palette. Type is `CustomizableToolbarContent`, not `ToolbarContent`,
    /// for `toolbar(id:)`.
    ///
    /// Grouping (toolbars.md:69 — critical actions visually distinct):
    /// Save + its status form the `.primaryAction` group; the navigation /
    /// reference cluster is `.secondaryAction`. On macOS `.primaryAction`
    /// sits at the LEADING edge and `.secondaryAction` follows toward the
    /// trailing edge, so the leading→trailing order is preserved EXACTLY as
    /// the pre-#880 all-`.automatic` layout: saveStatus, save, search,
    /// template, tasksReview, citationSummary, bibliography. That order is
    /// also the DEFAULT customization set — nothing hidden by default.
    ///
    /// customizationBehavior: the Save group is `.disabled` (a critical
    /// action + its status must never be removed or moved — "Save must
    /// remain reachable"); the reference cluster keeps the default behavior
    /// so users may reorder/remove/re-add those five to taste.
    @ToolbarContentBuilder
    private var mainToolbar: some CustomizableToolbarContent {
            // Save status indicator: a Modified/Saved label that
            // VoiceOver reads when the toolbar focus lands on it.
            // Hidden when no note is loaded so the toolbar doesn't
            // claim "Saved" against an empty editor.
            //
            // #880: ALWAYS emitted (stable item set) so the customization
            // set never changes shape. The "hidden when no note is loaded"
            // behavior uses `.hidden(_:)` — Apple's mechanism for conditional
            // toolbar-item display: it removes the item from the LIVE toolbar
            // while its stable id stays available in the customization palette.
            // (An inner `if` over the CONTENT is NOT a documented equivalent —
            // it risks a reachable blank slot / empty AX stop.) Pinned with the
            // Save button (`.disabled` behavior) as the critical
            // `.primaryAction` group.
            ToolbarItem(id: "saveStatus", placement: .primaryAction) {
                Text(appState.activeNoteHasUnsavedChanges ? "Modified" : "Saved")
                    .font(.callout)
                    .foregroundStyle(.secondary)
                    .accessibilityLabel(
                        appState.activeNoteHasUnsavedChanges
                            ? "Modified. This note has unsaved changes."
                            : "Saved. This note matches the on-disk file."
                    )
            }
            .customizationBehavior(.disabled)
            .hidden(appState.loadedFilePath == nil)
            ToolbarItem(id: "save", placement: .primaryAction) {
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
                        || !appState.activeNoteHasUnsavedChanges
                        || appState.activeNoteSaveDisabledReason != nil
                )
                .accessibilityHint(
                    appState.activeNoteSaveDisabledReason
                        ?? "Save the current note to disk. Command-S."
                )
                .help(
                    appState.activeNoteSaveDisabledReason
                        ?? "Save the current note to disk")
            }
            // #880: Save is the critical action — pinned (not removable or
            // movable), keeping it and its status as the fixed leading group.
            .customizationBehavior(.disabled)
            ToolbarItem(id: "search", placement: .secondaryAction) {
                Button {
                    appState.toggleSearchOverlay()
                } label: {
                    SlateSymbol.search.label()
                }
                // #422 / #874: vault search lives on the menu bar
                // ("Search Vault…", ⇧⌘F since #874 — ⌘F is now
                // find-in-note). The toolbar registration proved dead
                // with sidebar focus (VO test); the menu equivalent is
                // reachable regardless of focus. Two registrations of the
                // same equivalent would be ambiguous, so the toolbar
                // button is click/AX-activate only.
                .accessibilityHint(
                    "Opens the search overlay. Shift-Command-F to toggle, Escape to close."
                )
            }
            ToolbarItem(id: "template", placement: .secondaryAction) {
                Button {
                    appState.openTemplatePicker()
                } label: {
                    SlateSymbol.newFromTemplate.label()
                }
                // ⇧⌘N lives on File ▸ New from Template… (#422 — see
                // the Save button above). Click/AX-activate only.
                .disabled(appState.isMutatingStructure)
                .accessibilityHint(
                    appState.structuralMutationDisabledReason
                        ?? "Opens the template picker. Command-Shift-N. Escape closes."
                )
                .help(
                    appState.structuralMutationDisabledReason
                        ?? "Opens the template picker. Command-Shift-N. Escape closes."
                )
            }
            ToolbarItem(id: "tasksReview", placement: .secondaryAction) {
                Button {
                    appState.openTasksReview()
                } label: {
                    SlateSymbol.tasksReview.label()
                }
                // ⌘R lives on View ▸ Show Tasks Review (#422 — see the
                // Save button above; #863 moved the chord off ⇧⌘T).
                // Click/AX-activate only. #879: reveals the Tasks Review
                // right-pane leaf (no longer a sheet — nothing to Escape).
                .disabled(appState.currentSession == nil)
                .accessibilityHint(
                    "Reveals the vault-wide Tasks Review panel in the right pane. Command-R."
                )
            }
            // Milestone L #282: Citation Summary. Cmd+Shift+J opens
            // the sheet showing N citations / M unique sources for
            // the current note. Disabled with no note selected.
            ToolbarItem(id: "citationSummary", placement: .secondaryAction) {
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
            ToolbarItem(id: "bibliography", placement: .secondaryAction) {
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
            set: { _ in }
        )
    }

    private var pendingVaultClosePresented: Binding<Bool> {
        Binding(
            get: { appState.pendingVaultClose != nil },
            set: { _ in }
        )
    }

    private func filename(of path: String) -> String {
        (path as NSString).lastPathComponent
    }

    private func tabCloseAlertMessage(for tabID: TabID) -> Text {
        let path = appState.workspace.model.tab(tabID).map {
            appState.workspace.tabPath($0)
        }
        let name = path.map(filename(of:)) ?? "this note"
        let question = "Save changes to " + name + " before closing its tab?"
        guard let reason = appState.pendingTabCloseSaveDisabledReason else {
            return Text(question)
        }
        return Text(question + "\n\n" + reason)
    }

    private func pendingNavigationAlertMessage() -> Text {
        let name = filename(of: appState.loadedFilePath ?? "")
        let question = "Save changes to " + name + "?"
        guard let reason = appState.pendingNavigationSaveDisabledReason else {
            return Text(question)
        }
        return Text(question + "\n\n" + reason)
    }

    private func vaultCloseAlertMessage(count: Int) -> Text {
        let subject = count == 1 ? "1 note has" : String(count) + " notes have"
        let question = subject
            + " unsaved changes. Save all changes before closing the vault?"
        guard let reason = appState.pendingVaultCloseSaveAllDisabledReason else {
            return Text(question)
        }
        return Text(question + "\n\n" + reason)
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

enum BatchAttentionDismissal {
    @discardableResult
    static func resolve(
        id: UUID,
        dismiss: (UUID) -> Bool,
        focus: () -> Void
    ) -> Bool {
        guard dismiss(id) else { return false }
        focus()
        return true
    }
}

/// Bare Return and keypad Enter are consumed while a destructive Trash
/// confirmation owns the window. SwiftUI alerts can otherwise promote their
/// first button implicitly; this routes the live event to an inert,
/// captured-UUID resolver before the alert sees it.
enum TrashConfirmationReturnKey {
    enum Owner: Equatable {
        case singleFolder(UUID)
        case batch(UUID)
    }

    @discardableResult
    static func route(
        keyCode: UInt16,
        modifierFlags: NSEvent.ModifierFlags,
        owner: Owner?,
        onReturn: (Owner) -> Void
    ) -> Bool {
        guard let owner, keyCode == 36 || keyCode == 76 else { return false }
        let meaningfulModifiers = modifierFlags
            .intersection(.deviceIndependentFlagsMask)
            .subtracting([.capsLock, .function, .numericPad])
        guard meaningfulModifiers.isEmpty else { return false }
        onReturn(owner)
        return true
    }
}

/// Zero-size window-owned bridge for the Return router. The owner value is
/// refreshed on every SwiftUI update, so the callback always carries the alert
/// instance that was mounted; AppState rejects a stale UUID.
struct TrashConfirmationReturnKeyMonitor: NSViewRepresentable {
    let owner: TrashConfirmationReturnKey.Owner?
    let onReturn: (TrashConfirmationReturnKey.Owner) -> Void

    func makeNSView(context: Context) -> MonitorView {
        let view = MonitorView()
        view.update(owner: owner, onReturn: onReturn)
        return view
    }

    func updateNSView(_ view: MonitorView, context: Context) {
        view.update(owner: owner, onReturn: onReturn)
    }

    static func dismantleNSView(_ view: MonitorView, coordinator: ()) {
        view.stopMonitoring()
    }

    final class MonitorView: NSView {
        private var monitor: Any?
        private var owner: TrashConfirmationReturnKey.Owner?
        private var onReturn: (TrashConfirmationReturnKey.Owner) -> Void = { _ in }

        override var intrinsicContentSize: NSSize { .zero }

        func update(
            owner: TrashConfirmationReturnKey.Owner?,
            onReturn: @escaping (TrashConfirmationReturnKey.Owner) -> Void
        ) {
            self.owner = owner
            self.onReturn = onReturn
        }

        override func viewDidMoveToWindow() {
            super.viewDidMoveToWindow()
            if window == nil {
                stopMonitoring()
            } else {
                startMonitoringIfNeeded()
            }
        }

        func stopMonitoring() {
            guard let monitor else { return }
            NSEvent.removeMonitor(monitor)
            self.monitor = nil
        }

        private func startMonitoringIfNeeded() {
            guard monitor == nil else { return }
            monitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) {
                [weak self] event in
                guard let self, let hostWindow = self.window,
                    NSApp.isActive,
                    let eventWindow = event.window,
                    NSApp.keyWindow === eventWindow,
                    eventWindow === hostWindow
                        || eventWindow.sheetParent === hostWindow
                        || hostWindow.attachedSheet === eventWindow
                else { return event }
                if let editor = eventWindow.firstResponder as? NSTextView,
                    editor.hasMarkedText()
                {
                    return event
                }
                let handled = TrashConfirmationReturnKey.route(
                    keyCode: event.keyCode,
                    modifierFlags: event.modifierFlags,
                    owner: self.owner,
                    onReturn: self.onReturn)
                return handled ? nil : event
            }
        }

        deinit {
            if let monitor {
                NSEvent.removeMonitor(monitor)
            }
        }
    }
}
