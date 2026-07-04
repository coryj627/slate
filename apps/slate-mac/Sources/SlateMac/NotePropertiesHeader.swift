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
    @EnvironmentObject private var appState: AppState
    /// Observed directly for per-tab expansion state — nested
    /// ObservableObject changes are not forwarded through `appState`
    /// (the U1 WorkspaceTreeView lesson).
    @ObservedObject var workspace: WorkspaceState

    /// See the type doc — hugs small content, scrolls long lists.
    private static let rowAreaMaxHeight: CGFloat = 280

    // U3-4 (#468): show-source state is view-local — the DRAFT is
    // uncommitted user input and must never ride a `@Published`
    // (#448 discipline; commits go through `applyPropertiesSource`).
    @State private var isSourceMode = false
    @State private var sourceDraft = ""
    /// Fields-switch guard: a dirty draft prompts before leaving source
    /// mode (Apply / Discard / Cancel — never silent loss).
    @State private var pendingFieldsSwitch = false
    /// Focus return for the guard alert (WCAG 2.4.3/2.1.2): every
    /// resolution lands VoiceOver/keyboard focus back on the header's
    /// show-source toggle — the control that owns the mode.
    @AccessibilityFocusState private var sourceToggleFocused: Bool

    var body: some View {
        // Self-hiding: no loaded note (or an error tab) → no widget. The
        // mode surfaces render their own empty/error states full-height.
        if appState.loadedFilePath != nil, appState.noteLoadError == nil {
            VStack(spacing: 0) {
                DisclosureGroup(isExpanded: expansionBinding) {
                    if isSourceMode {
                        sourceEditor
                    } else {
                        rowArea
                    }
                } label: {
                    header
                }
                .padding(.horizontal, Tokens.Spacing.sm)
                .padding(.vertical, Tokens.Spacing.xxs)
                // Hidden trigger for Cmd+Shift+R — opens the bulk-rename
                // sheet (moved verbatim from the retired PropertiesPanel;
                // the visible button is the discoverable surface).
                .background(
                    Button("") {
                        appState.isBulkRenameSheetOpen = true
                    }
                    .keyboardShortcut(KeyEquivalent("r"), modifiers: [.command, .shift])
                    .opacity(0)
                    .accessibilityHidden(true)
                )
                Divider()
            }
            .accessibilityElement(children: .contain)
            .accessibilityLabel(regionLabel)
            // ⌘⇧D / palette request (the U4-4 token pattern — AppState
            // can't reach view state). Post-update mutation point (#448).
            .onChange(of: appState.propertiesSourceToggleRequest) {
                toggleSourceMode()
            }
            // Successful Apply → back to fields; the row list re-reads
            // disk state (the round-trip guarantee — no Swift YAML parse).
            .onChange(of: appState.propertiesSourceCommitted) {
                isSourceMode = false
                sourceDraft = ""
                pendingFieldsSwitch = false
            }
            // Cross-tab/reload leak guard: a draft belongs to ONE note.
            .onChange(of: appState.loadedFilePath) {
                isSourceMode = false
                sourceDraft = ""
                pendingFieldsSwitch = false
                appState.clearPropertiesSourceError()
            }
            .alert(
                "Apply property source changes?",
                isPresented: $pendingFieldsSwitch
            ) {
                Button("Apply") {
                    appState.applyPropertiesSource(sourceDraft)
                    sourceToggleFocused = true
                }
                Button("Discard", role: .destructive) {
                    isSourceMode = false
                    sourceDraft = ""
                    appState.clearPropertiesSourceError()
                    postAccessibilityAnnouncement("Source changes discarded.")
                    sourceToggleFocused = true
                }
                Button("Cancel", role: .cancel) {
                    sourceToggleFocused = true
                }
            } message: {
                Text("The YAML source has uncommitted edits.")
            }
        }
    }

    // MARK: - Source mode (U3-4, #468)

    private var draftIsDirty: Bool {
        isSourceMode && sourceDraft != appState.currentNoteFMSource
    }

    /// Fields ⇄ source. Entering source is always safe (rows commit
    /// per-key already); leaving with a dirty draft prompts.
    private func toggleSourceMode() {
        if isSourceMode {
            if draftIsDirty {
                pendingFieldsSwitch = true
            } else {
                isSourceMode = false
                sourceDraft = ""
                appState.clearPropertiesSourceError()
            }
        } else {
            sourceDraft = appState.currentNoteFMSource
            isSourceMode = true
        }
    }

    private var sourceEditor: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
            TextEditor(text: $sourceDraft)
                .font(Tokens.Typography.code)
                .frame(minHeight: 80, maxHeight: Self.rowAreaMaxHeight)
                .accessibilityLabel("Properties source, YAML")
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
                Button("Apply") { appState.applyPropertiesSource(sourceDraft) }
                    .keyboardShortcut(.return, modifiers: [.command])
                    .disabled(appState.isEditingProperty)
                    .accessibilityHint(
                        "Validates the YAML and rewrites the note's frontmatter. Command-Return.")
                Button("Cancel") {
                    isSourceMode = false
                    sourceDraft = ""
                    appState.clearPropertiesSourceError()
                    postAccessibilityAnnouncement("Source changes discarded.")
                }
                .keyboardShortcut(.cancelAction)
                Spacer()
            }
        }
        .padding(.vertical, Tokens.Spacing.xxs)
    }

    // MARK: - Pieces

    private var regionLabel: String {
        let count = appState.currentNoteProperties.count
        return "Properties, \(count) \(count == 1 ? "property" : "properties")"
    }

    private var header: some View {
        let count = appState.currentNoteProperties.count
        let suffix = count == 1 ? "item" : "items"
        return HStack {
            Text("Properties, \(count) \(suffix)")
                .font(Tokens.Typography.sectionHeader)
                .accessibilityAddTraits(.isHeader)
            Spacer()
            Button {
                appState.isAddPropertySheetOpen = true
            } label: {
                SlateSymbol.addProperty.decorative
            }
            .buttonStyle(.borderless)
            .help("Add property")
            .accessibilityLabel("Add property")
            .disabled(appState.loadedFilePath == nil)
            Button {
                appState.isBulkRenameSheetOpen = true
            } label: {
                SlateSymbol.bulkRename.decorative
            }
            .buttonStyle(.borderless)
            .help("Rename property across the vault")
            .accessibilityLabel("Rename property across the vault")
            .disabled(appState.currentSession == nil)
            Button {
                toggleSourceMode()
            } label: {
                SlateSymbol.showSource.decorative
            }
            .buttonStyle(.borderless)
            .help("Show source (⇧⌘D)")
            .accessibilityFocused($sourceToggleFocused)
            .accessibilityLabel("Show source")
            .accessibilityValue(isSourceMode ? "Showing source" : "Showing fields")
            .disabled(appState.loadedFilePath == nil)
        }
    }

    /// The moved row list — content semantics identical to the retired
    /// panel (empty-state string verbatim for WCAG 2.5.3 speech-control
    /// parity).
    private var rowArea: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
                if appState.currentNoteProperties.isEmpty {
                    Text("No properties yet. Add one to start.")
                        .font(Tokens.Typography.caption)
                        .foregroundStyle(Tokens.ColorRole.textSecondary)
                        .padding(.vertical, Tokens.Spacing.xxs)
                        .accessibilityLabel("No properties yet. Add one to start.")
                } else if let path = appState.loadedFilePath {
                    ForEach(Array(appState.currentNoteProperties.enumerated()), id: \.offset) {
                        _,
                        property in
                        PropertyEditorRow(
                            property: property,
                            path: path,
                            vaultRoot: appState.currentVaultURL
                        )
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
