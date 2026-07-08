// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// The workspace region — the center column of `MainSplitView` (U1-4 →
/// U1-3). Renders the `WorkspaceModel` tree recursively: groups become
/// panes (tab strip + content), splits become `SplitContainerView`s.
///
/// Document binding (the U1-2 parked-document architecture): the FOCUSED
/// group's pane hosts `NoteContentView` — AppState's single-note fields ARE
/// that document. Unfocused groups render their active tab's PARKED
/// `NoteDocument` read-only (full editor visuals, no editing path) and
/// activate on click, which routes the identity funnel (snapshot the
/// outgoing pane, restore the clicked one).
struct WorkspaceView: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        // `workspace` is a nested ObservableObject — AppState's own
        // publisher does NOT forward its changes, so the tree observes it
        // directly (a tab switch between two tabs of the same file changes
        // no AppState @Published field at all).
        WorkspaceTreeView(workspace: appState.workspace)
    }
}

private struct WorkspaceTreeView: View {
    @ObservedObject var workspace: WorkspaceState

    var body: some View {
        SplitNodeView(node: workspace.model.root)
    }
}

/// Recursive renderer for a `SplitNode`.
struct SplitNodeView: View {
    let node: SplitNode

    var body: some View {
        switch node {
        case .group(let group):
            TabGroupView(group: group)
        case .split(let branch):
            SplitContainerView(branch: branch)
        }
    }
}

/// One tab group's pane: the tab strip (U1-2), then the active tab's
/// content. The strip renders only when the group holds a tab: an empty
/// workspace shows the content pane's own empty state without a bar above
/// it.
///
/// AX: each pane is a contained, labeled region — "Editor pane N of M,
/// <title>" — so ⌘⌥arrow moves land somewhere VoiceOver can name (u1_spec
/// §U1-3; N is reading order from the model's geometry).
private struct TabGroupView: View {
    @EnvironmentObject private var appState: AppState
    let group: TabGroupNode

    var body: some View {
        let isFocused = appState.workspace.model.activeGroupID == group.id
        let hasSplits = appState.workspace.hasSplits

        VStack(spacing: 0) {
            if !group.tabs.isEmpty {
                TabBarView(group: group, isFocusedGroup: isFocused || !hasSplits)
                Divider()
            }
            paneContent(isFocused: isFocused, hasSplits: hasSplits)
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel(paneLabel)
    }

    @ViewBuilder
    private func paneContent(isFocused: Bool, hasSplits: Bool) -> some View {
        // Milestone T/N: route on the active tab's kind. Canvas/base
        // panes render their containers in BOTH focused and parked
        // positions because the document is shared per path.
        if case .base(let path) = group.activeTab?.item {
            BaseContainerView(document: appState.baseDocument(for: path))
        } else if case .canvas(let path) = group.activeTab?.item {
            CanvasContainerView(
                document: appState.canvasDocument(for: path),
                workspace: appState.workspace,
                tabID: group.activeTab?.id ?? TabID(raw: UUID()))
        } else if isFocused || !hasSplits {
            // The focused pane (or the only pane): the live document.
            NoteContentView(workspace: appState.workspace)
        } else {
            ParkedPaneView(group: group)
        }
    }

    private var paneLabel: String {
        let model = appState.workspace.model
        guard model.groupsInOrder.count > 1,
            let ordinal = model.ordinal(of: group.id)
        else { return "Note content pane" }
        let title = group.activeTab.map {
            (appState.workspace.tabPath($0) as NSString).lastPathComponent
        } ?? "empty"
        return "Editor pane \(ordinal) of \(model.groupsInOrder.count), \(title)"
    }
}

/// An unfocused pane's content: the active tab's parked document rendered
/// with full editor visuals but not editable. Clicking anywhere (or
/// AX-activating) focuses the pane, which restores its document into the
/// live fields through the identity funnel.
private struct ParkedPaneView: View {
    @EnvironmentObject private var appState: AppState
    let group: TabGroupNode

    var body: some View {
        if let tab = group.activeTab,
            let document = appState.workspace.document(for: tab.id) {
            ParkedDocumentView(document: document)
                // The NSTextView would swallow mouseDown before any outer
                // gesture; an unfocused pane's ONLY pointer affordance is
                // "click to focus", so the editor is click-transparent and
                // a clear overlay catches every click (focus-then-interact,
                // the Obsidian model). The overlay IS the pane's one
                // interactive element, so it carries button semantics for
                // VoiceOver (WCAG 4.1.2).
                .allowsHitTesting(false)
                .overlay {
                    Color.clear
                        .contentShape(Rectangle())
                        .onTapGesture {
                            appState.activateTab(tab.id)
                        }
                        .accessibilityLabel(
                            "Focus pane, \((document.path as NSString).lastPathComponent)")
                        .accessibilityAddTraits(.isButton)
                }
        } else {
            // A never-activated or empty parked pane (e.g. restored layout
            // whose tab hasn't been visited): neutral placeholder, click
            // focuses.
            VStack(spacing: Tokens.Spacing.sm) {
                Text("Click to focus this pane")
                    .font(Tokens.Typography.callout)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .contentShape(Rectangle())
            .onTapGesture {
                if let tab = group.activeTab {
                    appState.activateTab(tab.id)
                }
            }
            .accessibilityLabel("Focus this pane")
            .accessibilityAddTraits(.isButton)
        }
    }
}

/// Read-only rendering of a parked `NoteDocument` — observes the document
/// directly so same-path live edits (mirrored by `updateEditorText`)
/// repaint this pane immediately.
private struct ParkedDocumentView: View {
    @ObservedObject var document: NoteDocument

    var body: some View {
        NoteEditorView(
            text: .constant(document.text),
            headings: [],
            accessibilityLabel:
                "Editor for \((document.path as NSString).lastPathComponent), inactive pane",
            isEditable: false,
            onSave: {},
            scrollAnchorRequest: Empty().eraseToAnyPublisher(),
            lineScrollRequest: Empty().eraseToAnyPublisher(),
            cursorByteOffsetRequest: Empty().eraseToAnyPublisher(),
            previewEmbedAtCursor: nil
        )
    }
}

import Combine
