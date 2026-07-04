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

    var body: some View {
        // Self-hiding: no loaded note (or an error tab) → no widget. The
        // mode surfaces render their own empty/error states full-height.
        if appState.loadedFilePath != nil, appState.noteLoadError == nil {
            VStack(spacing: 0) {
                DisclosureGroup(isExpanded: expansionBinding) {
                    rowArea
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
        }
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
                .font(.headline)
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
            // U3-4 (#468) mounts the show-source YAML toggle here — the
            // header is its specified home; no placeholder control ships
            // (a dead button would be an AX lie).
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
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .padding(.vertical, 2)
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
