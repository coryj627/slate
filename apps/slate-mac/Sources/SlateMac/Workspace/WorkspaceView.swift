// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// The workspace region — the center column of `MainSplitView` (U1-4, #456).
///
/// Renders the `WorkspaceModel` tree recursively. In U1-4 the model is a
/// mirror constrained to one group and ≤ 1 tab, and the group body delegates
/// to `NoteContentView` unchanged — including its empty / loading / error /
/// populated states — so this reparent is strictly behavior-preserving (the
/// existing suite is the regression harness). The tab strip (U1-2) and
/// `SplitContainerView` (U1-3) mount into exactly these seams.
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

/// Recursive renderer for a `SplitNode`. `.split` rendering arrives with
/// U1-3; until then the model is single-group by construction (U1-4 mirror)
/// and the split branch renders its first child defensively rather than
/// crashing if reached.
private struct SplitNodeView: View {
    let node: SplitNode

    var body: some View {
        switch node {
        case .group(let group):
            TabGroupView(group: group)
        case .split(let branch):
            // U1-3 replaces this with SplitContainerView (dividers, weights,
            // focus routing). Unreachable in U1-4 (the mirror never splits),
            // and I4 guarantees ≥ 2 children — but a defensive path must not
            // be able to trap, so render the first child if any, else nothing.
            if let first = branch.children.first {
                SplitNodeView(node: first)
            }
        }
    }
}

/// One tab group's pane: the tab strip (U1-2), then the active tab's
/// content — today's `NoteContentView`, which owns all of the center
/// column's visual states. The strip renders only when the group holds a
/// tab: an empty workspace shows the content pane's own empty state without
/// a bar above it.
private struct TabGroupView: View {
    let group: TabGroupNode

    var body: some View {
        VStack(spacing: 0) {
            if !group.tabs.isEmpty {
                TabBarView(group: group)
                Divider()
            }
            NoteContentView()
        }
    }
}
