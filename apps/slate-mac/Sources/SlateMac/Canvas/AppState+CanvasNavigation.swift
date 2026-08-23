// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// The canvas keyboard navigator (Milestone T, #364) — deliberately a
/// **command layer, not a fourth view** (t2 shared-architecture
/// decision): these commands are hosted by every canvas surface and
/// operate on the shared `CanvasSelection`. Plain-arrow bindings apply
/// only while a canvas surface has focus (program rule R2); every
/// movement here is also a `CommandSection.canvas` palette command, so
/// VoiceOver Quick Nav users always have a path.
extension AppState {
    /// The active tab's canvas document, when the active tab is a
    /// ready canvas.
    var activeCanvasDocument: CanvasDocument? {
        guard let tab = workspace.activeTab, case .canvas(let path) = tab.item else {
            return nil
        }
        let doc = canvasDocument(for: path)
        guard case .ready = doc.state else { return nil }
        return doc
    }

    /// Move selection to the next/previous card in reading order.
    func canvasSelectAdjacent(offset: Int) {
        guard let doc = activeCanvasDocument else { return }
        // #373: movement walks the FILTERED set while a filter is on
        // (a view, never a mutation — Esc restores the full canvas).
        let rows = doc.filteredOutline
        guard !rows.isEmpty else {
            if doc.filterActive {
                canvasAnnouncer.announce(.canvasStatus(note: .noCardsMatchFilter))
            }
            return
        }
        let order = rows.map(\.nodeId)
        let currentIndex = doc.selection.selected.flatMap { order.firstIndex(of: $0) }
        let target: Int
        if let currentIndex {
            target = max(0, min(order.count - 1, currentIndex + offset))
            if target == currentIndex {
                canvasAnnouncer.announce(
                    .canvasStatus(note: offset > 0 ? .endOfCanvas : .startOfCanvas))
                return
            }
        } else {
            target = offset > 0 ? 0 : order.count - 1
        }
        canvasSelect(nodeId: order[target], in: doc)
    }

    /// Enter the selected group (select its first child), or announce
    /// that the selection isn't a group.
    func canvasEnterGroup() {
        guard let doc = activeCanvasDocument,
            let selected = doc.selection.selected,
            let row = doc.outline.first(where: { $0.nodeId == selected })
        else { return }
        guard row.kind == "group" else {
            canvasAnnouncer.announce(.canvasStatus(note: .notAGroup))
            return
        }
        // First child = the next outline row one level deeper.
        guard let index = doc.outline.firstIndex(where: { $0.nodeId == selected }),
            index + 1 < doc.outline.count,
            doc.outline[index + 1].depth == row.depth + 1
        else {
            canvasAnnouncer.announce(.canvasStatus(note: .groupIsEmpty(label: row.title)))
            return
        }
        canvasSelect(nodeId: doc.outline[index + 1].nodeId, in: doc)
    }

    /// Exit to the containing group (select the group row), or announce
    /// canvas level.
    func canvasExitGroup() {
        guard let doc = activeCanvasDocument,
            let selected = doc.selection.selected,
            let row = doc.outline.first(where: { $0.nodeId == selected })
        else { return }
        guard row.depth > 0,
            let index = doc.outline.firstIndex(where: { $0.nodeId == selected }),
            let parent = doc.outline[..<index].last(where: { $0.depth == row.depth - 1 })
        else {
            canvasAnnouncer.announce(.canvasStatus(note: .atCanvasLevel))
            return
        }
        canvasSelect(nodeId: parent.nodeId, in: doc)
    }

    /// Follow the selected card's Nth connection (1-based) in the given
    /// direction sense: forward = connections leaving or linking this
    /// card; back = connections arriving. Direction respects
    /// `fromEnd`/`toEnd` (t0 §1.2 / #360 model data).
    func canvasFollowConnection(forward: Bool, ordinal: Int = 1) {
        guard let doc = activeCanvasDocument, let selected = doc.selection.selected else {
            return
        }
        let neighbors = doc.neighbors(of: selected, session: currentSession)
        let candidates = neighbors.filter { neighbor in
            switch neighbor.direction {
            case .outgoing: return forward
            case .incoming: return !forward
            case .bidirectional, .undirected: return true
            }
        }
        guard candidates.indices.contains(ordinal - 1) else {
            canvasAnnouncer.announce(
                .canvasStatus(
                    note: .noConnection(
                        forward: forward,
                        ordinal: candidates.isEmpty ? nil : UInt32(clamping: ordinal))))
            return
        }
        let neighbor = candidates[ordinal - 1]
        // Narrate the destination's REAL kind (Codoki #613: a group or
        // file target must not be introduced as a text card).
        let otherKind =
            doc.outline.first { $0.nodeId == neighbor.otherNode }?.kind ?? "text"
        canvasAnnouncer.announce(
            .canvasConnectionTraversed(
                direction: neighbor.direction,
                kindLabel: otherKind, title: neighbor.otherTitle,
                label: neighbor.label))
        canvasSelect(nodeId: neighbor.otherNode, in: doc, announce: false)
    }

    /// Trace the outgoing chain from the selected card (cycle-safe),
    /// announcing each hop, ending with the visited count (t3).
    func canvasTracePath() {
        guard let doc = activeCanvasDocument, let start = doc.selection.selected else { return }
        var visited: [String] = [start]
        var seen: Set<String> = [start]
        var current = start
        while true {
            let outgoing = doc.neighbors(of: current, session: currentSession)
                .filter { $0.direction == .outgoing || $0.direction == .bidirectional }
            guard let next = outgoing.first(where: { !seen.contains($0.otherNode) }) else {
                break
            }
            visited.append(next.otherNode)
            seen.insert(next.otherNode)
            current = next.otherNode
        }
        let titles = visited.compactMap { id in
            doc.outline.first { $0.nodeId == id }?.title
        }
        if visited.count == 1 {
            canvasAnnouncer.announce(
                .canvasStatus(note: .noOutgoingPath(title: titles.first ?? "")))
            return
        }
        canvasSelect(nodeId: current, in: doc, announce: false)
        // The event carries the TITLES only; core speaks their count as
        // the sentence's tail, so the list and the number it claims can
        // never disagree (contracts doc CD-13 — mac spoke
        // `visited.count` while listing `titles`).
        canvasAnnouncer.announce(.canvasTracePathEnd(titles: titles))
    }

    /// The one selection mutation used by every navigator movement:
    /// updates the shared selection and narrates through the funnel.
    func canvasSelect(nodeId: String, in doc: CanvasDocument, announce: Bool = true) {
        let previous = doc.selection.selected
        doc.selection.selected = nodeId
        guard announce,
            let row = doc.outline.first(where: { $0.nodeId == nodeId })
        else { return }
        let previousPath =
            previous
            .flatMap { prev in doc.outline.first { $0.nodeId == prev } }?.groupPath ?? []
        if row.groupPath != previousPath {
            if let entered = row.groupPath.last, !previousPath.contains(entered) {
                // CD-4: the ENTERED GROUP's own card count, which is
                // exactly `row.totalM` — the arrived-at row's container
                // size, straight from core. mac walked back to the
                // group's own outline row and spoke ITS `totalM`, i.e.
                // how many siblings the GROUP has, a different number.
                // There is no lookup left at all, so Codoki #613's
                // repeated-label miscount cannot recur either.
                canvasAnnouncer.announce(
                    .canvasGroupEntered(label: entered, count: row.totalM))
            } else if let left = previousPath.last, !row.groupPath.contains(left) {
                canvasAnnouncer.announce(.canvasGroupLeft(label: left))
            }
        }
        canvasAnnouncer.announce(
            .canvasMovedTo(
                verbosity: canvasAnnouncer.verbosity,
                kindLabel: row.kind, title: row.title,
                ordinalN: row.ordinalN, totalM: row.totalM,
                container: row.groupPath.last,
                connectionCount: row.connectionCount,
                colorName: row.colorName,
                marked: doc.selection.marked.contains(nodeId)))
    }

    // MARK: Viewport commands (#520)

    private func announceZoom(_ doc: CanvasDocument) {
        canvasAnnouncer.announce(
            .canvasZoom(context: nil, percent: UInt32(clamping: doc.viewport.zoomPercent)))
    }

    func canvasZoomIn() {
        guard let doc = activeCanvasDocument else { return }
        doc.viewport.zoom(by: CanvasViewport.zoomStep)
        announceZoom(doc)
    }

    func canvasZoomOut() {
        guard let doc = activeCanvasDocument else { return }
        doc.viewport.zoom(by: 1 / CanvasViewport.zoomStep)
        announceZoom(doc)
    }

    func canvasActualSize() {
        guard let doc = activeCanvasDocument else { return }
        doc.viewport.setScale(1.0)
        announceZoom(doc)
    }

    func canvasFitCanvas() {
        guard let doc = activeCanvasDocument, !doc.scene.nodes.isEmpty else { return }
        var rect = CGRect.null
        for node in doc.scene.nodes {
            rect = rect.union(
                CGRect(x: node.x, y: node.y, width: node.width, height: node.height))
        }
        doc.viewport.fit(rect: rect)
        canvasAnnouncer.announce(
            .canvasZoom(
                context: .fitCanvas, percent: UInt32(clamping: doc.viewport.zoomPercent)))
    }

    func canvasZoomToSelection() {
        guard let doc = activeCanvasDocument else { return }
        guard let selected = doc.selection.selected,
            let node = doc.scene.nodes.first(where: { $0.nodeId == selected })
        else {
            canvasAnnouncer.announce(.canvasStatus(note: .nothingSelected))
            return
        }
        doc.viewport.fit(
            rect: CGRect(x: node.x, y: node.y, width: node.width, height: node.height),
            padding: 120)
        canvasAnnouncer.announce(
            .canvasZoom(
                context: .zoomedToSelection,
                percent: UInt32(clamping: doc.viewport.zoomPercent)))
    }

    /// Viewport-follows-selection toggle (default ON; the auto-pan
    /// itself stays silent — t0 §1.5 no-doubling).
    func canvasToggleFollowSelection() {
        guard let doc = activeCanvasDocument else { return }
        doc.viewport.followSelection.toggle()
        canvasAnnouncer.announce(
            .canvasFollowSelectionToggled(following: doc.viewport.followSelection))
    }

    /// The per-document mode controller (t0 §2), created on first use.
    /// Focus departure and Esc route through the container.
    func canvasModeController(for doc: CanvasDocument) -> CanvasModeController {
        if let existing = canvasModeControllers[doc.path] {
            return existing
        }
        let controller = CanvasModeController { [weak self] event in
            self?.canvasAnnouncer.announce(event)
        }
        canvasModeControllers[doc.path] = controller
        return controller
    }
}
