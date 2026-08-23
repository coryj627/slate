// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// The connect flow (Milestone T, #523) — BOTH mechanisms from
/// interview decision 8:
///
/// 1. **Picker** (primary, ⌃⌘C): the reusable proximity-sorted card
///    picker → optional label step → one edge. Sides auto-default to
///    the nearest edges by geometry at confirm time; direction
///    defaults to a one-way arrow at the target (Obsidian parity).
/// 2. **Connect mode** (navigate-and-confirm): candidate stepping
///    reuses the navigator movements VERBATIM (no second traversal
///    grammar) — arrows move the real selection; Return connects the
///    remembered origin to wherever you are; Esc returns you to the
///    origin. Full t0 §2 semantics via the mode controller.
///
/// Existing-connection edit (label/direction) and delete live on the
/// #362 connection rows and the palette.
extension AppState {
    /// Create one connection origin→target with auto sides + default
    /// direction; announced per t0 §1.3.
    func canvasConnect(from originId: String, to targetId: String, label: String?) {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            originId != targetId,
            let origin = doc.scene.nodes.first(where: { $0.nodeId == originId }),
            let target = doc.scene.nodes.first(where: { $0.nodeId == targetId })
        else {
            canvasAnnouncer.announce(.canvasStatus(note: .pickDifferentTarget))
            return
        }
        // §W-G row C: the nearest-edges-by-centre rule is core's
        // (`canvas_auto_sides`, contract 0b-3), keyed by RECTS rather
        // than node ids (CD-16) and handle-free, so the same call
        // serves this site, create-connected-card's not-yet-real card,
        // and the renderer's per-endpoint auto arm.
        let sides = canvasAutoSides(
            from: CanvasRect(
                x: origin.x, y: origin.y, width: origin.width, height: origin.height),
            to: CanvasRect(
                x: target.x, y: target.y, width: target.width, height: target.height))
        let cleanLabel = (label?.isEmpty == true) ? nil : label
        let ok = canvasApply(
            CanvasAction(
                name: "connect \"\(origin.title)\" to \"\(target.title)\"",
                ops: [
                    .addEdge(
                        id: Self.newCanvasEntityID(),
                        fromNode: originId, fromSide: sides.from,
                        toNode: targetId, toSide: sides.to,
                        fromEnd: .none, toEnd: .arrow,
                        label: cleanLabel, color: nil)
                ]),
            to: doc)
        guard ok else { return }
        canvasAnnouncer.announce(
            .canvasConnected(
                fromTitle: origin.title, toTitle: target.title, label: cleanLabel))
    }

    /// ⌃⌘C: the picker flow. The pick routes to the optional label
    /// step (`CanvasPrompt.connectLabel`).
    func canvasOpenConnectPicker() {
        guard let doc = activeCanvasDocument else { return }
        guard admitCanvasMutation(for: doc) else { return }
        guard doc.selection.selected != nil else {
            canvasAnnouncer.announce(.canvasStatus(note: .nothingSelected))
            return
        }
        canvasCardPicker = CanvasCardPickerRequest(purpose: .connectTo)
    }

    // MARK: Connect mode (navigate-and-confirm)

    /// Enter connect mode: remember the origin, then navigate anywhere
    /// with the ordinary movements; Return connects, Esc goes home.
    func canvasEnterConnectMode() {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let origin = doc.selection.selected,
            let originRow = doc.outline.first(where: { $0.nodeId == origin })
        else {
            canvasAnnouncer.announce(.canvasStatus(note: .nothingSelected))
            return
        }
        let controller = canvasModeController(for: doc)
        let entered = controller.enter(
            .init(
                mode: .connect,
                object: .card(title: originRow.title),
                onCommit: { [weak self, weak doc] in
                    guard let self, let doc,
                        let target = doc.selection.selected, target != origin
                    else {
                        return .canvasModeEndedWithoutEffect(mode: .connect)
                    }
                    // The connect announces itself (§1.3).
                    self.canvasConnect(from: origin, to: target, label: nil)
                    return nil
                },
                onCancel: { [weak self, weak doc] in
                    guard let self, let doc else { return .unstated }
                    self.canvasSelect(nodeId: origin, in: doc, announce: false)
                    return .backAt(title: originRow.title)
                }))
        _ = entered
    }

    // MARK: Existing-connection edit / delete

    /// The selected card's connections, for the pick sheets.
    func canvasConnectionChoices() -> [(edgeId: String, label: String)] {
        guard let doc = activeCanvasDocument, let selected = doc.selection.selected else {
            return []
        }
        return doc.neighbors(of: selected, session: currentSession).map { neighbor in
            var text: String
            switch neighbor.direction {
            case .outgoing: text = "To \"\(neighbor.otherTitle)\""
            case .incoming: text = "From \"\(neighbor.otherTitle)\""
            case .bidirectional, .undirected: text = "With \"\(neighbor.otherTitle)\""
            }
            if let label = neighbor.label { text += ", labelled \"\(label)\"" }
            return (edgeId: neighbor.edgeId, label: text)
        }
    }

    /// Delete a connection of the selected card (row action + palette).
    func canvasDeleteConnection(edgeId: String) {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let selected = doc.selection.selected
        else { return }
        // CD-7: the sentence is built from the connection's STRUCTURE
        // — direction, the other card's title, the label — so core
        // lower-cases only the fixed direction word. mac lower-cased
        // the whole picker row, which mangled the author's title
        // (`Deleted connection to "ideas"` for a card called *Ideas*).
        //
        // Looked up BEFORE the delete, and required: connection rows
        // only ever materialise under the selected card, so an edge
        // that is not among its neighbours is not this card's to
        // delete — and the typed event cannot express the
        // `Deleted connection ` with a trailing space that the old
        // `?? ""` produced.
        guard
            let neighbor = doc.neighbors(of: selected, session: currentSession)
                .first(where: { $0.edgeId == edgeId })
        else { return }
        let ok = canvasApply(
            CanvasAction(
                name: "delete connection",
                ops: [.deleteEdge(id: edgeId)]),
            to: doc)
        guard ok else { return }
        canvasAnnouncer.announce(
            .canvasDeleted(
                target: .connection(
                    direction: neighbor.direction,
                    otherTitle: neighbor.otherTitle,
                    label: neighbor.label),
                verbosity: canvasAnnouncer.verbosity,
                undoChord: Self.canvasUndoChord))
    }

    /// Edit a connection's label and direction (sides stay as
    /// authored; the auto-side pin applies at creation).
    func canvasEditConnection(
        edgeId: String, label: String?, direction: CanvasConnectionDirectionChoice
    ) {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let edge = doc.scene.edges.first(where: { $0.edgeId == edgeId })
        else { return }
        let (fromEnd, toEnd): (CanvasEndStyle, CanvasEndStyle)
        switch direction {
        case .toTarget: (fromEnd, toEnd) = (.none, .arrow)
        case .toSource: (fromEnd, toEnd) = (.arrow, .none)
        case .both: (fromEnd, toEnd) = (.arrow, .arrow)
        case .none: (fromEnd, toEnd) = (.none, .none)
        }
        let cleanLabel = (label?.isEmpty == true) ? nil : label
        let ok = canvasApply(
            CanvasAction(
                name: "edit connection",
                ops: [
                    .updateEdge(
                        id: edgeId,
                        fromSide: edge.fromSide, toSide: edge.toSide,
                        fromEnd: fromEnd, toEnd: toEnd,
                        label: cleanLabel, color: edge.color)
                ]),
            to: doc)
        guard ok else { return }
        canvasAnnouncer.announce(.canvasConnectionUpdated(label: cleanLabel))
    }
}

/// Direction choices for the connection-edit sheet (#523): phrased by
/// meaning, never by arrowhead glyphs.
enum CanvasConnectionDirectionChoice: String, CaseIterable {
    case toTarget
    case toSource
    case both
    case none

    var title: String {
        switch self {
        case .toTarget: return "Points at the target"
        case .toSource: return "Points back at the source"
        case .both: return "Both directions"
        case .none: return "No direction"
        }
    }
}
