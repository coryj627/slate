// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Renders a `SplitBranch` (U1-3, #455): children separated by draggable
/// dividers, weights from the model, recursion via `SplitNodeView`.
///
/// Geometry is the model's: `WorkspaceModel.groupRects()` is the single
/// source of truth for spatial focus, and this container lays children out
/// with exactly the same weight math, so what the census proves about focus
/// geometry is what the user sees.
///
/// Dividers: 1pt visual line (`Tokens.ColorRole.separator`) inside an 8pt
/// grab zone. Drag routes through `WorkspaceState.setWeights` → the model's
/// clamping (min weight 0.15) — the keyboard resize commands (⌘⌥= / ⌘⌥-)
/// hit the same clamp, so drag and keyboard can never disagree about
/// bounds. Dividers are NOT focusable: keyboard resize is a pane-scoped
/// command, not a divider-scoped one (a focusable divider is a trap-prone
/// oddity for VoiceOver users — see u1_spec §U1-3).
struct SplitContainerView: View {
    @EnvironmentObject private var appState: AppState
    let branch: SplitBranch

    /// Weights during a live drag (nil when idle). Committed to the model
    /// continuously; kept locally too so the drag doesn't fight publisher
    /// latency.
    @State private var dragWeights: [Double]?

    var body: some View {
        GeometryReader { proxy in
            let weights = dragWeights ?? branch.weights
            let axis = branch.axis
            let totalLength = axis == .horizontal ? proxy.size.width : proxy.size.height
            // 1pt of visual divider per gap comes out of the panes evenly.
            let dividerCount = CGFloat(max(0, branch.children.count - 1))
            let contentLength = max(0, totalLength - dividerCount)

            layout(
                weights: weights, axis: axis, contentLength: contentLength,
                proxy: proxy)
        }
    }

    @ViewBuilder
    private func layout(
        weights: [Double], axis: SplitBranch.Axis, contentLength: CGFloat,
        proxy: GeometryProxy
    ) -> some View {
        let stack = children(weights: weights, axis: axis, contentLength: contentLength)
        if axis == .horizontal {
            HStack(spacing: 0) { stack }
        } else {
            VStack(spacing: 0) { stack }
        }
    }

    @ViewBuilder
    private func children(
        weights: [Double], axis: SplitBranch.Axis, contentLength: CGFloat
    ) -> some View {
        ForEach(Array(branch.children.enumerated()), id: \.offset) { index, child in
            let length = contentLength * CGFloat(weights[index])
            SplitNodeView(node: child)
                .frame(
                    width: axis == .horizontal ? length : nil,
                    height: axis == .vertical ? length : nil)
            if index < branch.children.count - 1 {
                DividerHandle(axis: axis) { delta in
                    dragDivider(at: index, delta: delta, contentLength: contentLength)
                } onEnd: {
                    dragWeights = nil
                }
            }
        }
    }

    /// Move the divider between children `index` and `index+1` by `delta`
    /// points: weight shifts between exactly those two children, both
    /// clamped to the model floor. Commit continuously so the model (and
    /// the census-checked invariants) always reflect what's on screen.
    private func dragDivider(at index: Int, delta: CGFloat, contentLength: CGFloat) {
        guard contentLength > 0 else { return }
        var weights = dragWeights ?? branch.weights
        guard weights.indices.contains(index + 1) else { return }
        let shift = Double(delta / contentLength)
        let pair = weights[index] + weights[index + 1]
        let minW = WorkspaceModel.minWeight
        let newLeft = min(max(weights[index] + shift, minW), pair - minW)
        weights[index] = newLeft
        weights[index + 1] = pair - newLeft
        dragWeights = weights
        if let firstGroup = Self.firstGroupID(in: branch.children[index]) {
            appState.workspace.setWeights(weights, forSplitContaining: firstGroup)
        }
    }

    private static func firstGroupID(in node: SplitNode) -> GroupID? {
        switch node {
        case .group(let group): return group.id
        case .split(let branch): return branch.children.first.flatMap(firstGroupID(in:))
        }
    }
}

/// The 1pt divider line inside an 8pt grab zone. Cursor feedback on hover;
/// hidden from accessibility (resize is command-driven for AT users —
/// `slate.workspace.growPane` / `shrinkPane`).
private struct DividerHandle: View {
    let axis: SplitBranch.Axis
    let onDrag: (CGFloat) -> Void
    let onEnd: () -> Void

    @State private var lastTranslation: CGFloat = 0

    var body: some View {
        Rectangle()
            .fill(Tokens.ColorRole.separator)
            .frame(
                width: axis == .horizontal ? 1 : nil,
                height: axis == .vertical ? 1 : nil)
            .contentShape(
                Rectangle().inset(by: -4))
            .onHover { hovering in
                if hovering {
                    (axis == .horizontal
                        ? NSCursor.resizeLeftRight : NSCursor.resizeUpDown).push()
                } else {
                    NSCursor.pop()
                }
            }
            .gesture(
                DragGesture(minimumDistance: 1)
                    .onChanged { value in
                        let translation =
                            axis == .horizontal
                            ? value.translation.width : value.translation.height
                        onDrag(translation - lastTranslation)
                        lastTranslation = translation
                    }
                    .onEnded { _ in
                        lastTranslation = 0
                        onEnd()
                    }
            )
            .accessibilityHidden(true)
    }
}
