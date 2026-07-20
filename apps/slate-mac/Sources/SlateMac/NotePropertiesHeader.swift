// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// The in-note properties widget (U3-3, #467) — frontmatter as a pinned,
/// non-scrolling region at the top of the tab content, shown in BOTH modes
/// above the editor / reading view (Obsidian parity; the sidebar no longer
/// hosts properties).
///
/// The rows and sheets moved UNCHANGED from the retired `PropertiesPanel`:
/// same `PropertyEditorRow` bindings, same `AddPropertySheet` /
/// bulk-rename triggers, same conflict alerts routed via `MainSplitView`,
/// same draft discipline. What's new here is only the HOST:
///
/// - `DisclosureGroup` with per-tab expansion (`WorkspaceState.
///   propertiesCollapsed`, sparse — expanded is the default and the
///   absent-key state in `workspace.json`, exactly the `viewModes`
///   pattern).
/// - A contained AX region labeled "Properties, N properties" — VoiceOver
///   users hear the region BEFORE the editor in the reading order, which
///   is the plan's contract for where frontmatter lives.
/// - Dynamic-Type safety: the row list lives in a ScrollView capped via
///   `maxHeight` + `fixedSize` so small property sets hug their content
///   while a long list (or accessibility text sizes) scrolls INTERNALLY
///   instead of clipping rows or starving the editor (DoD Dynamic Type;
///   the cap approximates the spec's 40%-of-pane without a GeometryReader
///   in the pinned header path).
struct NotePropertiesHeader: View {
    private enum PropertyPublicationRecoveryResolution {
        case reapplyMine
        case useCurrentVersion
    }

    @EnvironmentObject private var appState: AppState
    /// Observed directly for per-tab expansion state (the U1
    /// WorkspaceTreeView lesson). The #868 bridge now ALSO forwards
    /// workspace publishes through `appState` coarsely; direct
    /// observation stays — it keeps this widget's dependency explicit
    /// rather than riding the whole-appState invalidation.
    @ObservedObject var workspace: WorkspaceState

    /// See the type doc — hugs small content, scrolls long lists.
    private static let rowAreaMaxHeight: CGFloat = 280

    // U3-4 (#468): show-source state is view-local — the DRAFT is
    // uncommitted user input. Its bytes live in AppState's recovery cache so
    // a quarantine-driven unmount cannot destroy the only copy.
    @State private var isSourceMode = false
    /// Fields-switch guard: a dirty draft prompts before leaving source
    /// mode (Apply / Discard / Cancel — never silent loss).
    @State private var pendingFieldsSwitch = false
    @State private var lastHandledSourceCommitRevision: UInt64 = 0
    @State private var pendingPublicationRecoveryResolution:
        PropertyPublicationRecoveryResolution?
    @State private var showingPublicationRecoveryConfirmation = false
    @State private var propertyRecoveryFocusContinuity =
        PropertyRecoveryFocusContinuity()
    /// Focus return for the guard alert (WCAG 2.4.3/2.1.2): every
    /// resolution lands VoiceOver and keyboard focus back on the header's
    /// show-source toggle — the control that owns the mode.
    @AccessibilityFocusState private var sourceToggleAccessibilityFocused: Bool
    @FocusState private var sourceToggleKeyboardFocused: Bool

    var body: some View {
        let owner = appState.noteAuthoringOwner()
        // Self-hiding: no loaded note (or an error tab) → no widget. The
        // mode surfaces render their own empty/error states full-height.
        if appState.loadedFilePath != nil, appState.noteLoadError == nil {
            VStack(spacing: 0) {
                DisclosureGroup(isExpanded: expansionBinding) {
                    if isSourceMode {
                        sourceEditor(owner: owner)
                    } else {
                        rowArea(owner: owner)
                    }
                } label: {
                    header(owner: owner)
                }
                .padding(.horizontal, Tokens.Spacing.sm)
                .padding(.vertical, Tokens.Spacing.xxs)
                if let authoringDisabledReason {
                    VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
                        Text(authoringDisabledReason)
                            .font(Tokens.Typography.caption)
                            .foregroundStyle(Tokens.ColorRole.textSecondary)
                            .accessibilityLabel(authoringDisabledReason)
                            .help(authoringDisabledReason)
                        if let recoveryTitle =
                            appState.activePropertyPublicationRecoveryTitle,
                            let recoveryText =
                                appState.activePropertyPublicationRecoveryText
                        {
                            VStack(alignment: .leading, spacing: Tokens.Spacing.xs) {
                                Text(recoveryTitle)
                                    .font(Tokens.Typography.caption.weight(.semibold))
                                ScrollView(.vertical) {
                                    Text(verbatim: recoveryText)
                                        .font(Tokens.Typography.code)
                                        .textSelection(.enabled)
                                        .frame(
                                            maxWidth: .infinity,
                                            alignment: .leading)
                                }
                                .frame(minHeight: 44, maxHeight: 160)
                                .padding(Tokens.Spacing.sm)
                                .background(
                                    Tokens.ColorRole.surfaceSecondary,
                                    in: RoundedRectangle(
                                        cornerRadius: Tokens.Radius.small))
                                .accessibilityLabel(
                                    "Retained property update. \(recoveryText)")
                                .accessibilityHint(
                                    "Scrollable and selectable retained property text.")
                                LazyVGrid(
                                    columns: [
                                        GridItem(
                                            .adaptive(minimum: 145),
                                            spacing: Tokens.Spacing.sm)
                                    ],
                                    alignment: .leading,
                                    spacing: Tokens.Spacing.xs
                                ) {
                                    Button("Copy Retained Update") {
                                        appState.copyActivePropertyPublicationRecovery()
                                    }
                                    .accessibilityHint(
                                        "Copies the exact retained property update before resolution.")
                                    Button("Check Saved Version") {
                                        retryPropertyPublicationAndRestoreFocus()
                                    }
                                    .disabled(appState.isEditingProperty)
                                    .accessibilityHint(
                                        "Checks whether the retained update is still the current saved version.")
                                    Button("Reapply Mine…") {
                                        pendingPublicationRecoveryResolution = .reapplyMine
                                        showingPublicationRecoveryConfirmation = true
                                    }
                                    .disabled(appState.isEditingProperty)
                                    .accessibilityHint(
                                        "Confirms before reapplying the retained property update to the newest saved version.")
                                    Button("Use Current Version…") {
                                        pendingPublicationRecoveryResolution = .useCurrentVersion
                                        showingPublicationRecoveryConfirmation = true
                                    }
                                    .disabled(appState.isEditingProperty)
                                    .accessibilityHint(
                                        "Confirms before discarding the retained update and using the current saved properties.")
                                }
                            }
                            .accessibilityElement(children: .contain)
                        } else if appState.activePropertyPublicationUncertaintyReason != nil {
                            Button("Reload Properties") {
                                retryPropertyPublicationAndRestoreFocus()
                            }
                            .disabled(appState.isEditingProperty)
                            .accessibilityHint(
                                "Reload the saved frontmatter without discarding the Markdown body draft.")
                            .help("Reload the saved property update")
                        }
                    }
                    .padding(.horizontal, Tokens.Spacing.sm)
                }
                // ⇧⌘R (bulk rename) lives on Edit ▸ Bulk Rename
                // Properties… — menu-bar-homed like ⌘F (the #422
                // lesson). The former hidden opacity-0 trigger here
                // also vanished whenever this header wasn't mounted;
                // the menu item is reachable regardless of focus or
                // which note surface is showing. The visible header
                // button below remains the discoverable affordance.
                Divider()
            }
            .accessibilityElement(children: .contain)
            .accessibilityLabel(regionLabel)
            // ⌘⇧D / palette request (the U4-4 token pattern — AppState
            // can't reach view state). Post-update mutation point (#448).
            .onChange(of: appState.propertiesSourceToggleRequest) {
                toggleSourceMode(owner: owner)
            }
            // Successful Apply → back to fields; the row list re-reads
            // disk state (the round-trip guarantee — no Swift YAML parse).
            .onChange(of: appState.propertiesSourceCommitRevision) {
                guard let path = owner?.path else { return }
                let revision = appState.propertiesSourceCommitRevision(for: path)
                guard revision > lastHandledSourceCommitRevision else { return }
                lastHandledSourceCommitRevision = revision
                isSourceMode = false
                pendingFieldsSwitch = false
            }
            // Cross-tab/reload ownership: the mounted Binding belongs to one
            // note, but its bytes park by exact path before the new note takes
            // over. Returning to that tab restores source mode and the draft.
            .onChange(of: appState.loadedFilePath) { _, newPath in
                if let draftOwnerPath = appState.propertiesSourceDraftPath,
                    !BaseExactIdentity.matches(draftOwnerPath, newPath)
                {
                    appState.parkPropertiesSourceDraftForTransition(owner: owner)
                    isSourceMode = false
                    appState.clearMountedPropertiesSourceDraft(owner: owner)
                }
                if let newPath,
                    appState.restoreParkedPropertiesSourceDraft(
                        for: newPath, owner: owner)
                {
                    isSourceMode = true
                }
                pendingFieldsSwitch = false
                pendingPublicationRecoveryResolution = nil
                showingPublicationRecoveryConfirmation = false
                propertyRecoveryFocusContinuity.cancel()
                appState.clearPropertiesSourceError(owner: owner)
            }
            .onChange(of: appState.isEditingProperty) { _, isEditing in
                if propertyRecoveryFocusContinuity
                    .consumeCompletionFocusRequest(isEditing: isEditing)
                {
                    // The successful async path has just removed its recovery
                    // controls. Reassert both focus channels on the header
                    // button that survives that subtree transition.
                    restoreSourceToggleFocus()
                }
            }
            // #868: mirror the source-mode bool into appState so the
            // View ▸ Show/Hide Properties Source title tracks it. ONE
            // wire covers every mutation site (toggle, commit, note
            // switch, Discard, Cancel); post-update mutation point
            // (#448). The view stays the owner of the real state —
            // appState only reflects. `.onAppear` resyncs after mount
            // gaps (this widget self-hides while no note is loaded, a
            // stretch whose onChange events never fire;
            // `clearActiveNoteFields` resets the mirror on that edge,
            // and the setter's equality guard keeps the common
            // already-in-sync appear publish-free).
            .onChange(of: isSourceMode) {
                appState.notePropertiesSourceModeChanged(
                    isSourceMode, owner: owner)
            }
            .onAppear {
                if let path = owner?.path {
                    lastHandledSourceCommitRevision =
                        appState.propertiesSourceCommitRevision(for: path)
                }
                if BaseExactIdentity.matches(
                    appState.propertiesSourceDraftPath,
                    appState.loadedFilePath),
                    appState.propertiesSourceDraftPath != nil
                {
                    isSourceMode = true
                } else if let path = appState.loadedFilePath,
                    appState.restoreParkedPropertiesSourceDraft(
                        for: path, owner: owner)
                {
                    isSourceMode = true
                }
                appState.notePropertiesSourceModeChanged(
                    isSourceMode, owner: owner)
            }
            .alert(
                "Apply property source changes?",
                isPresented: $pendingFieldsSwitch
            ) {
                Button("Apply") {
                    appState.applyPropertiesSource(
                        appState.propertiesSourceDraft, owner: owner)
                    sourceToggleKeyboardFocused = true
                    sourceToggleAccessibilityFocused = true
                }
                .disabled(propertiesSourceApplyDisabledReason != nil)
                .accessibilityHint(
                    propertiesSourceApplyDisabledReason
                        ?? "Apply the uncommitted YAML source changes.")
                .help(
                    propertiesSourceApplyDisabledReason
                        ?? "Apply properties source changes")
                Button("Discard", role: .destructive) {
                    isSourceMode = false
                    appState.discardPropertiesSourceDraft(
                        for: appState.propertiesSourceDraftPath,
                        owner: owner)
                    appState.clearPropertiesSourceError(owner: owner)
                    postAccessibilityAnnouncement(.sourceChangesDiscarded)
                    sourceToggleKeyboardFocused = true
                    sourceToggleAccessibilityFocused = true
                }
                .disabled(draftDiscardDisabledReason != nil)
                .accessibilityHint(
                    draftDiscardDisabledReason
                        ?? "Discard the uncommitted YAML source changes.")
                .help(
                    draftDiscardDisabledReason
                        ?? "Discard properties source changes")
                Button("Cancel", role: .cancel) {
                    sourceToggleKeyboardFocused = true
                    sourceToggleAccessibilityFocused = true
                }
            } message: {
                Text(fieldsSwitchMessage)
            }
            .confirmationDialog(
                publicationRecoveryConfirmationTitle,
                isPresented: $showingPublicationRecoveryConfirmation,
                titleVisibility: .visible
            ) {
                switch pendingPublicationRecoveryResolution {
                case .reapplyMine:
                    Button("Reapply Mine", role: .destructive) {
                        trackPropertyRecoveryTask(
                            appState.reapplyActivePropertyPublication())
                        pendingPublicationRecoveryResolution = nil
                        sourceToggleKeyboardFocused = true
                        sourceToggleAccessibilityFocused = true
                    }
                case .useCurrentVersion:
                    Button("Use Current Version", role: .destructive) {
                        trackPropertyRecoveryTask(
                            appState
                                .useCurrentVersionForActivePropertyPublication())
                        pendingPublicationRecoveryResolution = nil
                        sourceToggleKeyboardFocused = true
                        sourceToggleAccessibilityFocused = true
                    }
                case nil:
                    EmptyView()
                }
                Button("Cancel", role: .cancel) {
                    pendingPublicationRecoveryResolution = nil
                    propertyRecoveryFocusContinuity.cancel()
                    sourceToggleKeyboardFocused = true
                    sourceToggleAccessibilityFocused = true
                }
            } message: {
                Text(publicationRecoveryConfirmationMessage)
            }
        }
    }

    private func retryPropertyPublicationAndRestoreFocus() {
        trackPropertyRecoveryTask(appState.retryActivePropertyPublication())
        restoreSourceToggleFocus()
    }

    private func trackPropertyRecoveryTask(
        _ task: Task<Void, Never>?
    ) {
        propertyRecoveryFocusContinuity.start(taskWasStarted: task != nil)
    }

    private func restoreSourceToggleFocus() {
        // Move focus before an asynchronous operation can remove the button
        // that launched it. Completion reasserts focus after the new subtree
        // has rendered, covering both sides of the transition.
        sourceToggleKeyboardFocused = true
        sourceToggleAccessibilityFocused = true
    }

    // MARK: - Source mode (U3-4, #468)

    private var draftIsDirty: Bool {
        isSourceMode
            && !(appState.loadedFilePath.map {
                appState.propertiesSourceDraftIsCommittedPendingVerification(
                    path: $0, draft: appState.propertiesSourceDraft)
            } ?? false)
            && !BaseExactIdentity.matches(
                appState.propertiesSourceDraft,
                appState.currentNoteFMSource)
    }

    private var authoringDisabledReason: String? {
        appState.activePropertyAuthoringDisabledReason
    }

    private var propertiesSourceApplyDisabledReason: String? {
        appState.activePropertiesSourceApplyDisabledReason
    }

    private var draftDiscardDisabledReason: String? {
        guard let path = appState.loadedFilePath else {
            return appState.activeDraftDiscardDisabledReason
        }
        return appState.propertiesSourceDraftDiscardDisabledReason(
            path: path, draft: appState.propertiesSourceDraft)
    }

    private var fieldsSwitchMessage: String {
        let reasons = [
            propertiesSourceApplyDisabledReason,
            draftDiscardDisabledReason,
        ].compactMap { $0 }
        let uniqueReasons = Array(Set(reasons)).sorted()
        guard !uniqueReasons.isEmpty else {
            return "The YAML source has uncommitted edits."
        }
        return "The YAML source has uncommitted edits. "
            + uniqueReasons.joined(separator: " ")
    }

    private var publicationRecoveryConfirmationTitle: String {
        switch pendingPublicationRecoveryResolution {
        case .reapplyMine:
            return "Reapply your retained property update?"
        case .useCurrentVersion:
            return "Use the current saved properties?"
        case nil:
            return "Resolve retained property update"
        }
    }

    private var publicationRecoveryConfirmationMessage: String {
        switch pendingPublicationRecoveryResolution {
        case .reapplyMine:
            if appState.activePropertyPublicationRecoveryReplacesAllProperties {
                return "Slate will replace all current saved properties with the retained properties source. Property changes made since this update was retained will be overwritten. The Markdown body remains unchanged. Copy the retained update first if you may need to compare versions."
            }
            return "Slate will apply the retained property update to the newest readable disk version. Other properties and the Markdown body remain unchanged."
        case .useCurrentVersion:
            return "Slate will load the current saved properties and permanently discard this retained property update. Any separate newer uncommitted draft remains available. Copy the retained update first if you may need it later."
        case nil:
            return "Choose how to resolve the retained property update."
        }
    }

    /// Fields ⇄ source. Entering source is always safe (rows commit
    /// per-key already); leaving with a dirty draft prompts.
    private func toggleSourceMode(owner: NoteAuthoringOwner?) {
        guard let owner, appState.ownsNoteAuthoring(owner) else { return }
        if isSourceMode, let draftDiscardDisabledReason {
            appState.postMutationAnnouncement(draftDiscardDisabledReason)
            return
        }
        if isSourceMode {
            if draftIsDirty {
                pendingFieldsSwitch = true
            } else {
                isSourceMode = false
                appState.discardPropertiesSourceDraft(
                    for: appState.propertiesSourceDraftPath,
                    owner: owner)
                appState.clearPropertiesSourceError(owner: owner)
            }
        } else {
            if !BaseExactIdentity.matches(
                appState.propertiesSourceDraftPath,
                appState.loadedFilePath)
            {
                appState.propertiesSourceDraft = appState.currentNoteFMSource
                appState.propertiesSourceDraftPath = appState.loadedFilePath
            }
            isSourceMode = true
        }
    }

    private func sourceEditor(owner: NoteAuthoringOwner?) -> some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
            // PlainTextEditor, not SwiftUI TextEditor: a bare
            // TextEditor inherits the system smart-quote/dash
            // substitutions, so typing `"` into a YAML value lands a
            // curly quote — the exact corruption the main editor's
            // hygiene block exists to prevent, on the one surface
            // that edits frontmatter as raw source.
            PlainTextEditor(
                text: Binding(
                    get: { appState.propertiesSourceDraft },
                    set: {
                        appState.updatePropertiesSourceDraft($0, owner: owner)
                    }),
                accessibilityLabel: "Properties source, YAML",
                isEditable: authoringDisabledReason == nil,
                readOnlyReason: authoringDisabledReason,
                // #848: the YAML source editor is an editing surface —
                // it zooms with the note editor.
                textScale: appState.editorTextScale
            )
            .frame(minHeight: 80, maxHeight: Self.rowAreaMaxHeight)
            if let error = appState.propertiesSourceError {
                // Inline, specific, non-destructive (DoD §F): the Rust
                // MalformedFrontmatter line/column message; the draft and
                // focus stay put, nothing was written.
                Text(error)
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.destructiveText)
                    .accessibilityLabel("Properties source error: \(error)")
            }
            HStack(spacing: Tokens.Spacing.sm) {
                Button("Apply") {
                    appState.applyPropertiesSource(
                        appState.propertiesSourceDraft, owner: owner)
                }
                    .keyboardShortcut(.return, modifiers: [.command])
                    .disabled(
                        appState.isEditingProperty
                            || propertiesSourceApplyDisabledReason != nil)
                    .accessibilityHint(
                        propertiesSourceApplyDisabledReason
                            ?? "Validates the YAML and rewrites the note's frontmatter. Command-Return.")
                    .help(
                        propertiesSourceApplyDisabledReason
                            ?? "Apply properties source changes")
                Button("Cancel") {
                    isSourceMode = false
                    appState.discardPropertiesSourceDraft(
                        for: appState.propertiesSourceDraftPath,
                        owner: owner)
                    appState.clearPropertiesSourceError(owner: owner)
                    postAccessibilityAnnouncement(.sourceChangesDiscarded)
                }
                .disabled(draftDiscardDisabledReason != nil)
                .accessibilityHint(
                    draftDiscardDisabledReason
                        ?? "Discard the uncommitted properties source changes.")
                .help(draftDiscardDisabledReason ?? "Discard properties source changes")
                .keyboardShortcut(.cancelAction)
                Spacer()
            }
        }
        .padding(.vertical, Tokens.Spacing.xxs)
    }

    // MARK: - Pieces

    private var regionLabel: String {
        let count = appState.currentNoteProperties.count
        return "Properties, \(CountCopy.counted(count, "property", "properties"))"
    }

    private func header(owner: NoteAuthoringOwner?) -> some View {
        let count = appState.currentNoteProperties.count
        let suffix = count == 1 ? "item" : "items"
        return HStack {
            Text("Properties, \(count) \(suffix)")
                .font(Tokens.Typography.sectionHeader)
                .accessibilityAddTraits(.isHeader)
            Spacer()
            // Header glyph buttons: pin the HIG macOS DEFAULT click
            // target (28×28pt; HIG minimum 20, WCAG 2.5.8 minimum 24
            // — 28 clears all three). A bare `.decorative` glyph in a
            // borderless button renders ~16pt with no padded hit area.
            Button {
                appState.requestAddPropertySheet()
            } label: {
                SlateSymbol.addProperty.decorative
                    .frame(minWidth: 28, minHeight: 28)
                    .contentShape(Rectangle())
            }
            .buttonStyle(.borderless)
            .help(authoringDisabledReason ?? "Add property")
            .accessibilityLabel("Add property")
            .accessibilityHint(authoringDisabledReason ?? "Add a property to this note.")
            .disabled(appState.loadedFilePath == nil || authoringDisabledReason != nil)
            Button {
                appState.isBulkRenameSheetOpen = true
            } label: {
                SlateSymbol.bulkRename.decorative
                    .frame(minWidth: 28, minHeight: 28)
                    .contentShape(Rectangle())
            }
            .buttonStyle(.borderless)
            .help("Rename property across the vault")
            .accessibilityLabel("Rename property across the vault")
            .disabled(appState.currentSession == nil)
            Button {
                toggleSourceMode(owner: owner)
            } label: {
                SlateSymbol.showSource.decorative
                    .frame(minWidth: 28, minHeight: 28)
                    .contentShape(Rectangle())
            }
            .buttonStyle(.borderless)
            .help(draftDiscardDisabledReason ?? "Show source (⇧⌘D)")
            .focused($sourceToggleKeyboardFocused)
            .accessibilityFocused($sourceToggleAccessibilityFocused)
            .accessibilityLabel("Show source")
            .accessibilityValue(isSourceMode ? "Showing source" : "Showing fields")
            .accessibilityHint(
                draftDiscardDisabledReason
                    ?? "Switch between property fields and YAML source.")
            .disabled(
                appState.loadedFilePath == nil
                    || (isSourceMode && draftDiscardDisabledReason != nil))
        }
    }

    /// The moved row list — content semantics identical to the retired
    /// panel (empty-state string verbatim for WCAG 2.5.3 speech-control
    /// parity).
    private func rowArea(owner: NoteAuthoringOwner?) -> some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
                if appState.currentNoteProperties.isEmpty {
                    Text("No properties yet. Add one to start.")
                        .font(Tokens.Typography.caption)
                        .foregroundStyle(Tokens.ColorRole.textSecondary)
                        .padding(.vertical, Tokens.Spacing.xxs)
                        .accessibilityLabel("No properties yet. Add one to start.")
                } else if let path = appState.loadedFilePath, let owner {
                    ForEach(
                        appState.currentNoteProperties,
                        id: \.exactPropertyRowCollectionID
                    ) { property in
                        PropertyEditorRow(
                            property: property,
                            path: path,
                            vaultRoot: appState.currentVaultURL,
                            owner: owner
                        )
                        .id(PropertyEditorRowIdentity(owner: owner, key: property.key))
                    }
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .frame(maxHeight: Self.rowAreaMaxHeight)
        .fixedSize(horizontal: false, vertical: true)
    }

    /// Per-tab expansion (default expanded; the collapsed set is the
    /// sparse persisted state). Toggling persists the layout immediately —
    /// same discipline as the mode flip.
    private var expansionBinding: Binding<Bool> {
        Binding(
            get: { [weak appState, weak workspace] in
                guard let workspace,
                    let tab = workspace.model.activeGroup.activeTabID
                else { return true }
                _ = appState  // silence unused-capture under -warnings-as-errors
                return workspace.isPropertiesExpanded(for: tab)
            },
            set: { [weak appState, weak workspace] expanded in
                guard let appState, let workspace,
                    let tab = workspace.model.activeGroup.activeTabID
                else { return }
                workspace.setPropertiesExpanded(expanded, for: tab)
                appState.saveWorkspaceLayout()
            }
        )
    }
}

/// Maintains the focus return across an asynchronous recovery transition.
/// The launching button can disappear before its task finishes, so the view
/// moves focus immediately and then consumes one post-completion request after
/// `isEditingProperty` returns to false. Cancellation/note switches prevent a
/// stale task from stealing focus in a different note.
struct PropertyRecoveryFocusContinuity {
    private(set) var awaitsAsyncCompletion = false

    mutating func start(taskWasStarted: Bool) {
        awaitsAsyncCompletion = taskWasStarted
    }

    mutating func cancel() {
        awaitsAsyncCompletion = false
    }

    mutating func consumeCompletionFocusRequest(isEditing: Bool) -> Bool {
        guard awaitsAsyncCompletion, !isEditing else { return false }
        awaitsAsyncCompletion = false
        return true
    }
}

private extension Property {
    var exactPropertyRowCollectionID: String {
        BaseExactIdentity.key(
            prefix: "note-property-row", components: [key])
    }
}
