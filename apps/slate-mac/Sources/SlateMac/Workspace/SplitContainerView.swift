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
                DividerHandle(
                    axis: axis,
                    leadingFraction: weights[index] / max(weights[index] + weights[index + 1], 1e-9),
                    onDrag: { delta in
                        dragDivider(at: index, delta: delta, contentLength: contentLength)
                    },
                    onEnd: { commitDrag(at: index) },
                    onAdjust: { step in
                        // VoiceOver adjustable action: shift a resize-step of
                        // the PAIR's span between the two neighbors — the
                        // same math as a drag of that distance. A discrete
                        // step commits immediately (there's no gesture end).
                        let pair = (dragWeights ?? branch.weights)[index]
                            + (dragWeights ?? branch.weights)[index + 1]
                        dragDivider(
                            at: index,
                            delta: CGFloat(step * WorkspaceModel.resizeStep * pair)
                                * contentLength,
                            contentLength: contentLength)
                        commitDrag(at: index)
                    })
            }
        }
    }

    /// Move the divider between children `index` and `index+1` by `delta`
    /// points: weight shifts between exactly those two children, both
    /// clamped to the model floor. Writes ONLY the local `dragWeights`
    /// override that drives on-screen layout; the model commit is
    /// deferred to `commitDrag` at gesture end (see its note).
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
    }

    /// Commit the live drag weights to the model ONCE — at gesture end or
    /// per VoiceOver step — then release the local override.
    ///
    /// #868 red-team: the previous code committed on EVERY pointer tick,
    /// publishing `WorkspaceState.model` per event. The new
    /// workspace→appState `objectWillChange` bridge amplifies each such
    /// publish into a whole-app invalidation plus a full `.commands`
    /// rebuild, so a single divider drag re-rendered the menu bar and
    /// every appState-observing view at pointer-event rate (jank on
    /// large vaults). On-screen tracking already rides `dragWeights`
    /// (`body` reads `dragWeights ?? branch.weights` — never the model
    /// mid-drag), so deferring the commit is visually transparent; the
    /// final model state and the `setWeights` `validate()` assert are
    /// unchanged.
    private func commitDrag(at index: Int) {
        defer { dragWeights = nil }
        guard let weights = dragWeights,
            let firstGroup = Self.firstGroupID(in: branch.children[index])
        else { return }
        appState.workspace.setWeights(weights, forSplitContaining: firstGroup)
    }

    private static func firstGroupID(in node: SplitNode) -> GroupID? {
        switch node {
        case .group(let group): return group.id
        case .split(let branch): return branch.children.first.flatMap(firstGroupID(in:))
        }
    }
}

/// The 1pt divider line inside an 8pt grab zone.
///
/// A REAL accessibility element, not a hidden one: VoiceOver reaches it as
/// an adjustable "Pane divider" — swipe up/down (the adjustable-element
/// gesture) resizes the adjacent panes in `WorkspaceModel.resizeStep`
/// increments, the same math as a pointer drag. The Grow/Shrink Pane
/// commands remain the keyboard path; this gives VO users the direct,
/// spatial equivalent of the drag.
///
/// Pointer feedback via `pointerStyle` (macOS 15 floor) — declarative, no
/// NSCursor push/pop stack to imbalance (Codoki #494).
private struct DividerHandle: View {
    let axis: SplitBranch.Axis
    /// The leading neighbor's share of the pair, for the AX value.
    let leadingFraction: Double
    let onDrag: (CGFloat) -> Void
    let onEnd: () -> Void
    /// VoiceOver adjustable action: +1 grows the leading pane one step,
    /// -1 shrinks it.
    let onAdjust: (Double) -> Void

    @State private var lastTranslation: CGFloat = 0

    var body: some View {
        Rectangle()
            .fill(Tokens.ColorRole.separator)
            .frame(
                width: axis == .horizontal ? 1 : nil,
                height: axis == .vertical ? 1 : nil)
            .contentShape(
                Rectangle().inset(by: -4))
            .pointerStyle(
                axis == .horizontal
                    ? .frameResize(position: .trailing, directions: [.inward, .outward])
                    : .frameResize(position: .bottom, directions: [.inward, .outward])
            )
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
            .accessibilityElement()
            .accessibilityLabel("Pane divider")
            .accessibilityValue(
                "\(Int((leadingFraction * 100).rounded())) percent to the "
                    + (axis == .horizontal ? "left pane" : "top pane"))
            .accessibilityHint(
                "Resizes the adjacent panes. Adjust up to grow the "
                    + (axis == .horizontal ? "left" : "top")
                    + " pane, down to shrink it.")
            .accessibilityAdjustableAction { direction in
                onAdjust(direction == .increment ? 1 : -1)
            }
    }
}
