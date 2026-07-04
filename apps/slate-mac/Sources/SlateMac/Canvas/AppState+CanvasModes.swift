// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Move & resize modes (Milestone T, #521), on the #364
/// CanvasModeController (t0 §2 M1–M7).
///
/// The t4 pipeline exception: while a mode is active the UI holds the
/// hypothetical geometry (`CanvasDocument.transientRects`), querying
/// `canvas_check_overlap` per step for onset/offset warnings; Return
/// commits ONE `canvas_apply` capturing start→end (a single undo
/// step, never per-nudge entries); Esc discards the transient with no
/// backend call. Marked sets move as a rigid unit.
extension AppState {
    /// Grid steps mirror the backend constants (#517 exports them;
    /// values pinned by the cross-checking test).
    static let canvasGridStep: Double = 20
    static let canvasGridStepLarge: Double = 100
    static let canvasMinCardSize: Double = 40

    /// `Int(Double)` traps on NaN/Inf/≥2^63 — reachable via hostile
    /// .canvas geometry the parser tolerates (red-team #521 finding 3).
    static func canvasSafeInt(_ value: Double) -> Int {
        guard value.isFinite else { return 0 }
        return Int(min(max(value, -9e15), 9e15))
    }

    struct CanvasTransientState {
        /// Moving/resizing ids in reading order (rigid unit for moves).
        var ids: [String]
        /// Original geometry, restored on cancel.
        var originals: [String: CanvasRect]
        /// Hypothetical geometry (what commit writes).
        var rects: [String: CanvasRect]
        var isResize: Bool
        var wasOverlapping: Bool
    }

    // MARK: Entry

    /// ⌃⌘G "grab": move the selection (or the marked set, rigidly).
    func canvasEnterMoveMode() {
        guard let doc = activeCanvasDocument else { return }
        let moving = canvasMovingSet(in: doc)
        guard !moving.isEmpty else {
            canvasAnnouncer.announce(.status("Nothing selected."))
            return
        }
        var originals: [String: CanvasRect] = [:]
        for id in moving {
            guard let node = doc.scene.nodes.first(where: { $0.nodeId == id }) else { continue }
            originals[id] = CanvasRect(x: node.x, y: node.y, width: node.width, height: node.height)
        }
        let object =
            moving.count == 1
            ? "\"\(doc.outline.first { $0.nodeId == moving.first }?.title ?? "card")\""
            : "\(moving.count) cards"
        let controller = canvasModeController(for: doc)
        let entered = controller.enter(
            .init(
                name: "Move mode",
                object: object,
                exits: "Arrows to move, Shift for big steps, Return to place, Escape to cancel.",
                onCommit: { [weak self, weak doc] in
                    guard let self, let doc else { return nil }
                    return self.canvasCommitTransient(doc: doc, verb: "move", object: object)
                },
                onCancel: { [weak self, weak doc] in
                    guard let self, let doc else { return "Move cancelled." }
                    self.canvasDiscardTransient(doc: doc)
                    return moving.count == 1
                        ? "Move cancelled — card returned."
                        : "Move cancelled — cards returned."
                }))
        guard entered else { return }
        canvasTransient = CanvasTransientState(
            ids: moving, originals: originals, rects: originals,
            isResize: false,
            wasOverlapping: canvasEntryOverlap(doc: doc, ids: moving, rects: originals))
        doc.transientRects = originals
    }

    /// ⌃⌘R: resize the selected card (single card only).
    func canvasEnterResizeMode() {
        guard let doc = activeCanvasDocument,
            let selected = doc.selection.selected,
            let node = doc.scene.nodes.first(where: { $0.nodeId == selected })
        else {
            canvasAnnouncer.announce(.status("Nothing selected."))
            return
        }
        let rect = CanvasRect(x: node.x, y: node.y, width: node.width, height: node.height)
        let title = doc.outline.first { $0.nodeId == selected }?.title ?? "card"
        let controller = canvasModeController(for: doc)
        let entered = controller.enter(
            .init(
                name: "Resize mode",
                object: "\"\(title)\"",
                exits:
                    "Left and Right arrows change width, Up and Down change height, Return to apply, Escape to cancel.",
                onCommit: { [weak self, weak doc] in
                    guard let self, let doc else { return nil }
                    return self.canvasCommitTransient(
                        doc: doc, verb: "resize", object: "\"\(title)\"")
                },
                onCancel: { [weak self, weak doc] in
                    guard let self, let doc else { return "Resize cancelled." }
                    self.canvasDiscardTransient(doc: doc)
                    return "Resize cancelled — size restored."
                }))
        guard entered else { return }
        canvasTransient = CanvasTransientState(
            ids: [selected], originals: [selected: rect], rects: [selected: rect],
            isResize: true,
            wasOverlapping: canvasEntryOverlap(
                doc: doc, ids: [selected], rects: [selected: rect]))
        doc.transientRects = [selected: rect]
    }

    /// ⌃⌘R while resize mode is active commits it (a quick
    /// grab-adjust-done loop); otherwise enters resize mode.
    func canvasCommitOrEnterResize() {
        guard let doc = activeCanvasDocument else { return }
        let controller = canvasModeController(for: doc)
        if controller.active?.name == "Resize mode" {
            _ = controller.commit()
        } else {
            canvasEnterResizeMode()
        }
    }

    /// Entry-time overlap state, so the first "Overlapping another
    /// card" is a real ONSET, not a restatement of the status quo
    /// (red-team #521 finding 4).
    private func canvasEntryOverlap(
        doc: CanvasDocument, ids: [String], rects: [String: CanvasRect]
    ) -> Bool {
        guard let session = currentSession, let handle = doc.handle else { return false }
        return ids.contains { id in
            guard let rect = rects[id] else { return false }
            let hits =
                (try? session.canvasCheckOverlap(handle: handle, rect: rect, exclude: ids)) ?? []
            return !hits.isEmpty
        }
    }

    /// True when an arrow press belongs to an active spatial mode.
    var canvasModeConsumesArrows: Bool {
        guard let doc = activeCanvasDocument else { return false }
        return canvasModeControllers[doc.path]?.active != nil && canvasTransient != nil
    }

    // MARK: Steps

    /// One arrow step in the active mode. Move: rigid translation of
    /// every transient rect. Resize: ←→ width, ↑↓ height, minimum
    /// enforced with announcement.
    func canvasModeStep(dx: Double, dy: Double, large: Bool) {
        guard let doc = activeCanvasDocument, var transient = canvasTransient else { return }
        let step = large ? Self.canvasGridStepLarge : Self.canvasGridStep

        if transient.isResize {
            guard let id = transient.ids.first, var rect = transient.rects[id] else { return }
            let newWidth = rect.width + dx * step
            let newHeight = rect.height + dy * step
            if newWidth < Self.canvasMinCardSize || newHeight < Self.canvasMinCardSize {
                canvasAnnouncer.announce(.status("Minimum size."))
                return
            }
            rect = CanvasRect(x: rect.x, y: rect.y, width: newWidth, height: newHeight)
            transient.rects[id] = rect
            canvasTransient = transient
            doc.transientRects = transient.rects
            canvasAnnounceTransient(doc: doc, transient: &transient, describe: {
                "\(Self.canvasSafeInt(rect.width)) by \(Self.canvasSafeInt(rect.height))"
            })
        } else {
            for (id, rect) in transient.rects {
                transient.rects[id] = CanvasRect(
                    x: rect.x + dx * step, y: rect.y + dy * step,
                    width: rect.width, height: rect.height)
            }
            canvasTransient = transient
            doc.transientRects = transient.rects
            let snapshot = transient
            var mutable = transient
            canvasAnnounceTransient(
                doc: doc, transient: &mutable,
                describe: { self.canvasRelativeDescription(doc: doc, transient: snapshot) })
        }
    }

    /// Resize presets (M6-friendly: palette commands, no arrows needed).
    func canvasResizeDefaultSize() {
        canvasApplyResizePreset(width: 260, height: 140, label: "default size")
    }

    func canvasResizeFitContent() {
        guard let doc = activeCanvasDocument, let transient = canvasTransient,
            transient.isResize, let id = transient.ids.first
        else { return }
        // Approximation: default width; height from the text length
        // (the real editor's metrics land with the Wave-4 editor).
        let fetched = try? currentSession?.canvasNodeText(handle: doc.handle ?? 0, nodeId: id)
        let text: String = (fetched ?? nil) ?? ""
        let lines = max(1, text.count / 32 + text.filter { $0 == "\n" }.count)
        let height = min(600, max(Double(lines) * 24 + 40, Self.canvasMinCardSize))
        canvasApplyResizePreset(width: 260, height: height, label: "fit to content")
    }

    private func canvasApplyResizePreset(width: Double, height: Double, label: String) {
        guard let doc = activeCanvasDocument, var transient = canvasTransient,
            transient.isResize, let id = transient.ids.first,
            let rect = transient.rects[id]
        else { return }
        transient.rects[id] = CanvasRect(x: rect.x, y: rect.y, width: width, height: height)
        canvasTransient = transient
        doc.transientRects = transient.rects
        // Through the overlap tracker (red-team #521 finding 5): a
        // preset that lands on another card must warn like a step.
        var mutable = transient
        canvasAnnounceTransient(
            doc: doc, transient: &mutable,
            describe: {
                "Resized to \(label): \(Self.canvasSafeInt(width)) by \(Self.canvasSafeInt(height))"
            })
    }

    // MARK: Commit / cancel plumbing

    private func canvasCommitTransient(doc: CanvasDocument, verb: String, object: String)
        -> String?
    {
        guard let transient = canvasTransient else { return nil }
        var ops: [CanvasOp] = []
        for id in transient.ids {
            guard let rect = transient.rects[id],
                let original = transient.originals[id],
                rect != original
            else { continue }
            ops.append(
                .updateNodeGeometry(
                    id: id, x: rect.x, y: rect.y, width: rect.width, height: rect.height))
        }
        canvasTransient = nil
        doc.transientRects = nil
        guard !ops.isEmpty else {
            return "\(verb.capitalized) ended — nothing changed."
        }
        let ok = canvasApply(
            CanvasAction(name: "\(verb) \(object)", ops: ops), to: doc)
        guard ok else { return nil }  // conflict already announced
        return verb == "move" ? "Placed \(object)." : "Resized \(object)."
    }

    private func canvasDiscardTransient(doc: CanvasDocument) {
        canvasTransient = nil
        doc.transientRects = nil
    }

    // MARK: Narration

    /// Overlap onset/offset (G20: silent stacking is invisible to a
    /// non-visual author) + the coalesced relative description.
    private func canvasAnnounceTransient(
        doc: CanvasDocument, transient: inout CanvasTransientState,
        describe: () -> String
    ) {
        var text = describe()
        if let session = currentSession, let handle = doc.handle {
            let anyOverlap = transient.ids.contains { id in
                guard let rect = transient.rects[id] else { return false }
                let hits =
                    (try? session.canvasCheckOverlap(
                        handle: handle, rect: rect, exclude: transient.ids)) ?? []
                return !hits.isEmpty
            }
            if anyOverlap && !transient.wasOverlapping {
                text += ". Overlapping another card"
            } else if !anyOverlap && transient.wasOverlapping {
                text += ". Clear of overlaps"
            }
            transient.wasOverlapping = anyOverlap
            canvasTransient = transient
        }
        canvasAnnouncer.announce(.transientGeometry(text))
    }

    /// Relative description from the nearest non-moving neighbors:
    /// "Below \"Research\", right of \"Ideas\"" (t4 phrasing).
    func canvasRelativeDescription(doc: CanvasDocument, transient: CanvasTransientState)
        -> String
    {
        guard let primaryId = transient.ids.first,
            let rect = transient.rects[primaryId]
        else { return "" }
        let cx: Double = rect.x + rect.width / 2
        let cy: Double = rect.y + rect.height / 2
        typealias NeighborFix = (node: CanvasSceneNode, dx: Double, dy: Double)
        var neighbors: [NeighborFix] = []
        for node in doc.scene.nodes {
            if transient.ids.contains(node.nodeId) || node.kind == "group" { continue }
            let dx: Double = node.x + node.width / 2 - cx
            let dy: Double = node.y + node.height / 2 - cy
            neighbors.append((node: node, dx: dx, dy: dy))
        }
        neighbors.sort { lhs, rhs in
            let ld: Double = lhs.dx * lhs.dx + lhs.dy * lhs.dy
            let rd: Double = rhs.dx * rhs.dx + rhs.dy * rhs.dy
            return ld < rd
        }
        guard let nearest = neighbors.first else { return "Alone on the canvas" }

        func phrase(_ neighbor: NeighborFix) -> String {
            if abs(neighbor.dy) >= abs(neighbor.dx) {
                return neighbor.dy < 0 ? "Below \"\(neighbor.node.title)\"" : "Above \"\(neighbor.node.title)\""
            }
            return neighbor.dx < 0
                ? "Right of \"\(neighbor.node.title)\"" : "Left of \"\(neighbor.node.title)\""
        }

        var parts = [phrase(nearest)]
        // A second axis-distinct neighbor completes the fix.
        let vertical = abs(nearest.dy) >= abs(nearest.dx)
        if let second = neighbors.dropFirst().first(where: {
            (abs($0.dy) >= abs($0.dx)) != vertical
        }) {
            parts.append(phrase(second).prefix(1).lowercased() + phrase(second).dropFirst())
        }
        return parts.joined(separator: ", ")
    }
}
