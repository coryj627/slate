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
    /// Placement geometry, straight from core (`canvas_constants`,
    /// §W-G row H / contract 0b-4). A free FFI function with no
    /// handle (CD-17), so a mode can read `minCardSize` before any
    /// canvas is open. The three Swift mirrors that stood here are
    /// gone — `canvasMinCardSize` among them, which had no core
    /// counterpart at all until 0b-1 added `MIN_CARD_SIZE`.
    ///
    /// Computed rather than stored: the call builds a record out of
    /// module constants, and a lazily-initialized static of an FFI
    /// record type is a concurrency question this does not need to
    /// answer.
    static var canvasGeometry: CanvasConstants { canvasConstants() }

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
        guard admitCanvasMutation(for: doc) else { return }
        let moving = canvasMovingSet(in: doc)
        guard !moving.isEmpty else {
            canvasAnnouncer.announce(.canvasStatus(note: .nothingSelected))
            return
        }
        var originals: [String: CanvasRect] = [:]
        for id in moving {
            guard let node = doc.scene.nodes.first(where: { $0.nodeId == id }) else { continue }
            originals[id] = CanvasRect(x: node.x, y: node.y, width: node.width, height: node.height)
        }
        let primaryTitle =
            doc.outline.first { $0.nodeId == moving.first }?.title ?? "card"
        let object: CanvasModeObject =
            moving.count == 1
            ? .card(title: primaryTitle)
            : .cards(count: UInt32(clamping: moving.count))
        let controller = canvasModeController(for: doc)
        let entered = controller.enter(
            .init(
                mode: .move,
                object: object,
                onCommit: { [weak self, weak doc] in
                    guard let self, let doc else { return nil }
                    return self.canvasCommitTransient(doc: doc, verb: .move, object: object)
                },
                onCancel: { [weak self, weak doc] in
                    // The degenerate path (document gone before the
                    // restore could run) states no restoration.
                    guard let self, let doc else { return .unstated }
                    self.canvasDiscardTransient(doc: doc)
                    return .cardsReturned(count: UInt32(clamping: moving.count))
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
            admitCanvasMutation(for: doc),
            let selected = doc.selection.selected,
            let node = doc.scene.nodes.first(where: { $0.nodeId == selected })
        else {
            canvasAnnouncer.announce(.canvasStatus(note: .nothingSelected))
            return
        }
        let rect = CanvasRect(x: node.x, y: node.y, width: node.width, height: node.height)
        let title = doc.outline.first { $0.nodeId == selected }?.title ?? "card"
        let controller = canvasModeController(for: doc)
        let entered = controller.enter(
            .init(
                mode: .resize,
                object: .card(title: title),
                onCommit: { [weak self, weak doc] in
                    guard let self, let doc else { return nil }
                    return self.canvasCommitTransient(
                        doc: doc, verb: .resize, object: .card(title: title))
                },
                onCancel: { [weak self, weak doc] in
                    guard let self, let doc else { return .unstated }
                    self.canvasDiscardTransient(doc: doc)
                    return .sizeRestored
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
        guard admitCanvasMutation(for: doc) else { return }
        let controller = canvasModeController(for: doc)
        if controller.active?.mode == .resize {
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
        guard admitCanvasMutation(for: doc) else { return }
        let geometry = Self.canvasGeometry
        let step = large ? geometry.gridStepLarge : geometry.gridStep

        if transient.isResize {
            guard let id = transient.ids.first, var rect = transient.rects[id] else { return }
            let newWidth = rect.width + dx * step
            let newHeight = rect.height + dy * step
            // Reject-the-step, not clamp-to-min: neither dimension
            // moves when either would fall below the minimum. The
            // CONSTANT is core's; this rule is the host's and PR F
            // copies it (contracts doc, "Mac details recorded while
            // reading").
            if newWidth < geometry.minCardSize || newHeight < geometry.minCardSize {
                canvasAnnouncer.announce(.canvasResizeClamped)
                return
            }
            rect = CanvasRect(x: rect.x, y: rect.y, width: newWidth, height: newHeight)
            transient.rects[id] = rect
            canvasTransient = transient
            doc.transientRects = transient.rects
            canvasAnnounceTransient(doc: doc, transient: &transient, describe: { overlap in
                CanvasA11yEvent.canvasResizeGeometry(
                    preset: nil,
                    width: UInt32(clamping: Self.canvasSafeInt(rect.width)),
                    height: UInt32(clamping: Self.canvasSafeInt(rect.height)),
                    overlap: overlap)
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
                describe: { overlap in
                    guard
                        let descs = self.canvasRelativeDescription(
                            doc: doc, transient: snapshot)
                    else { return nil }
                    return CanvasA11yEvent.canvasMoveRelative(
                        descs: descs, overlap: overlap)
                })
        }
    }

    /// Resize presets (M6-friendly: palette commands, no arrows needed).
    func canvasResizeDefaultSize() {
        let geometry = Self.canvasGeometry
        canvasApplyResizePreset(
            width: geometry.defaultCardW, height: geometry.defaultCardH,
            preset: .defaultSize)
    }

    func canvasResizeFitContent() {
        guard let doc = activeCanvasDocument, let transient = canvasTransient,
            transient.isResize, let id = transient.ids.first
        else { return }
        guard admitCanvasMutation(for: doc) else { return }
        // Approximation: default width; height from the text length
        // (the real editor's metrics land with the Wave-4 editor). The
        // formula's own numbers — 32, 24, 40, the 600 cap — are D-5's
        // host-designated placeholder, identical on both hosts and NOT
        // core constants; only the width and the floor come from
        // `canvas_constants`.
        let geometry = Self.canvasGeometry
        let fetched = try? currentSession?.canvasNodeText(handle: doc.handle ?? 0, nodeId: id)
        let text: String = (fetched ?? nil) ?? ""
        let lines = max(1, text.count / 32 + text.filter { $0 == "\n" }.count)
        let height = min(600, max(Double(lines) * 24 + 40, geometry.minCardSize))
        canvasApplyResizePreset(
            width: geometry.defaultCardW, height: height, preset: .fitToContent)
    }

    private func canvasApplyResizePreset(
        width: Double, height: Double, preset: CanvasResizePreset
    ) {
        guard let doc = activeCanvasDocument, var transient = canvasTransient,
            transient.isResize, let id = transient.ids.first,
            let rect = transient.rects[id]
        else { return }
        guard admitCanvasMutation(for: doc) else { return }
        transient.rects[id] = CanvasRect(x: rect.x, y: rect.y, width: width, height: height)
        canvasTransient = transient
        doc.transientRects = transient.rects
        // Through the overlap tracker (red-team #521 finding 5): a
        // preset that lands on another card must warn like a step.
        var mutable = transient
        canvasAnnounceTransient(
            doc: doc, transient: &mutable,
            describe: { overlap in
                CanvasA11yEvent.canvasResizeGeometry(
                    preset: preset,
                    width: UInt32(clamping: Self.canvasSafeInt(width)),
                    height: UInt32(clamping: Self.canvasSafeInt(height)),
                    overlap: overlap)
            })
    }

    // MARK: Commit / cancel plumbing

    private func canvasCommitTransient(
        doc: CanvasDocument, verb: CanvasTransientVerb, object: CanvasModeObject
    ) -> CanvasA11yEvent? {
        guard admitCanvasMutation(for: doc) else { return nil }
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
            return .canvasModeEndedWithoutEffect(mode: Self.canvasMode(of: verb))
        }
        let ok = canvasApply(
            CanvasAction(
                name: "\(Self.canvasActionVerb(verb)) \(Self.canvasActionObject(object))",
                ops: ops),
            to: doc)
        guard ok else { return nil }  // conflict already announced
        return .canvasModeCommitted(verb: verb, object: object)
    }

    private func canvasDiscardTransient(doc: CanvasDocument) {
        canvasTransient = nil
        doc.transientRects = nil
    }

    // MARK: Narration

    /// Overlap onset/offset (G20: silent stacking is invisible to a
    /// non-visual author) + the coalesced relative description. The
    /// transition is a CLAUSE on the geometry event, never a second
    /// utterance (contracts doc CD-1), so it is computed here and
    /// handed to the event builder. A nil build means "nothing to say"
    /// — the degenerate no-primary-rect path, which was an empty
    /// string before.
    private func canvasAnnounceTransient(
        doc: CanvasDocument, transient: inout CanvasTransientState,
        describe: (CanvasOverlapTransition?) -> CanvasA11yEvent?
    ) {
        var overlap: CanvasOverlapTransition?
        if let session = currentSession, let handle = doc.handle {
            let anyOverlap = transient.ids.contains { id in
                guard let rect = transient.rects[id] else { return false }
                let hits =
                    (try? session.canvasCheckOverlap(
                        handle: handle, rect: rect, exclude: transient.ids)) ?? []
                return !hits.isEmpty
            }
            if anyOverlap && !transient.wasOverlapping {
                overlap = .onset
            } else if !anyOverlap && transient.wasOverlapping {
                overlap = .cleared
            }
            transient.wasOverlapping = anyOverlap
            canvasTransient = transient
        }
        guard let event = describe(overlap) else { return }
        canvasAnnouncer.announce(event)
    }

    /// Relative description from the nearest non-moving neighbours —
    /// core's `canvas_describe_relative` (§W-G row B, contract 0b-7).
    /// The nearest-neighbour walk that lived here is gone: core picks
    /// the neighbours AND phrases them (`Below "Research", right of
    /// "Ideas"`), and its `(squared distance, document index)` order
    /// pins the tie-break Swift's unstable `sort(by:)` left undefined
    /// (CD-19).
    ///
    /// `nil` means there is nothing to describe against at all, which
    /// stays silent; an EMPTY list is a real fix — core speaks
    /// `Alone on the canvas`.
    func canvasRelativeDescription(doc: CanvasDocument, transient: CanvasTransientState)
        -> [CanvasRelativeDesc]?
    {
        guard let primaryId = transient.ids.first,
            let rect = transient.rects[primaryId],
            let session = currentSession,
            let handle = doc.handle
        else { return nil }
        // The moving set is the exclusion list: a card is never
        // described relative to itself or to the rest of its rigid unit.
        return try? session.canvasDescribeRelative(
            handle: handle, rect: rect, exclude: transient.ids)
    }

    /// The mode a transient verb belongs to (`Move ended — nothing
    /// changed.` names the MODE, the commit names the VERB).
    private static func canvasMode(of verb: CanvasTransientVerb) -> CanvasMode {
        switch verb {
        case .move: return .move
        case .resize: return .resize
        }
    }

    /// Undo-stack action names are host data (t3), not announcements —
    /// they ride into `CanvasHistoryApplied.name` as a payload.
    private static func canvasActionVerb(_ verb: CanvasTransientVerb) -> String {
        switch verb {
        case .move: return "move"
        case .resize: return "resize"
        }
    }

    private static func canvasActionObject(_ object: CanvasModeObject) -> String {
        switch object {
        case .card(let title): return "\"\(title)\""
        case .cards(let count): return CountCopy.counted(count, "card", "cards")
        }
    }
}
