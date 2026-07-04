// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// The accessible canvas outline (Milestone T, #362) — the primary
/// structured surface: every card and group in deterministic reading
/// order, with rotors, N-of-M positional context in AX values (t0
/// §1.2), connection rows under the selected card (direction phrases,
/// t0 §1.2), and announcement via the #518 funnel only.
///
/// - Selection drives the shared `CanvasSelection` (single source of
///   truth; surfaces never hold local selection).
/// - Activation opens the card: markdown file → note tab; link →
///   browser; text → interim read-only detail panel (replaced by the
///   real editor in Wave 4, t2 R14); media → deferred to Wave 4
///   actions, announced honestly.
/// - Returning from an opened card restores focus to its row
///   (WCAG 2.4.3).
/// - Virtualized: `List` materializes rows lazily; the 2,000-node
///   fixture stays responsive (§K — backend build cost is benchmarked
///   in BENCHMARKS.md; row construction here is O(visible)).
struct CanvasOutlineView: View {
    @EnvironmentObject var appState: AppState
    @ObservedObject var document: CanvasDocument
    @ObservedObject var selection: CanvasSelection
    let tabID: TabID

    @AccessibilityFocusState private var focusedRow: String?
    @Namespace private var rotorSpace

    /// Per-kind activation lives in the container (one semantic across
    /// outline and table; #368 replaces it with the real actions).
    let onActivate: (CanvasOutlineRow) -> Void

    init(
        document: CanvasDocument, tabID: TabID,
        onActivate: @escaping (CanvasOutlineRow) -> Void
    ) {
        self.document = document
        self.selection = document.selection
        self.tabID = tabID
        self.onActivate = onActivate
    }

    /// One outline line: a node row, or a connection row nested under
    /// the selected card.
    enum Line: Identifiable {
        case node(CanvasOutlineRow)
        case connection(parent: CanvasOutlineRow, neighbor: CanvasNeighbor, ordinal: Int, total: Int)

        var id: String {
            switch self {
            case .node(let row): return row.nodeId
            case .connection(let parent, let neighbor, _, _):
                return "\(parent.nodeId)→\(neighbor.edgeId)"
            }
        }
    }

    private var lines: [Line] {
        var out: [Line] = []
        for row in document.outline {
            out.append(.node(row))
            // Connection rows materialize under the SELECTED card only:
            // linear reading stays concise at 2,000 nodes; #364's
            // follow-connection commands are the canvas-wide traversal.
            if row.nodeId == selection.selected {
                let neighbors = document.neighbors(of: row.nodeId, session: appState.currentSession)
                for (i, neighbor) in neighbors.enumerated() {
                    out.append(
                        .connection(
                            parent: row, neighbor: neighbor,
                            ordinal: i + 1, total: neighbors.count))
                }
            }
        }
        return out
    }

    var body: some View {
        List(lines, selection: selectionBinding) { line in
            lineView(line)
                .id(line.id)
        }
        .accessibilityLabel("Canvas outline")
        // Navigator arrows (#364, rule R2: plain keys act only while a
        // canvas surface has focus; palette equivalents always exist —
        // VO Quick Nav users take those). ↑↓ stay native List moves.
        .onKeyPress(.leftArrow, phases: .down) { press in
            if appState.canvasModeConsumesArrows {
                appState.canvasModeStep(
                    dx: -1, dy: 0, large: press.modifiers.contains(.shift))
                return .handled
            }
            appState.canvasFollowConnection(forward: false)
            return .handled
        }
        .onKeyPress(.rightArrow, phases: .down) { press in
            if appState.canvasModeConsumesArrows {
                appState.canvasModeStep(
                    dx: 1, dy: 0, large: press.modifiers.contains(.shift))
                return .handled
            }
            appState.canvasFollowConnection(forward: true)
            return .handled
        }
        // ↑↓ belong to the List EXCEPT while a spatial mode holds the
        // arrows (#521): then they nudge/resize.
        .onKeyPress(.upArrow, phases: .down) { press in
            guard appState.canvasModeConsumesArrows else { return .ignored }
            appState.canvasModeStep(dx: 0, dy: -1, large: press.modifiers.contains(.shift))
            return .handled
        }
        .onKeyPress(.downArrow, phases: .down) { press in
            guard appState.canvasModeConsumesArrows else { return .ignored }
            appState.canvasModeStep(dx: 0, dy: 1, large: press.modifiers.contains(.shift))
            return .handled
        }
        .accessibilityRotor("Cards") {
            ForEach(document.outline.filter { $0.kind != "group" }, id: \.nodeId) { row in
                AccessibilityRotorEntry(Text(row.title), id: row.nodeId, in: rotorSpace)
            }
        }
        .accessibilityRotor("Groups") {
            ForEach(document.outline.filter { $0.kind == "group" }, id: \.nodeId) { row in
                AccessibilityRotorEntry(Text(row.title), id: row.nodeId, in: rotorSpace)
            }
        }
        .accessibilityRotor("Connections") {
            ForEach(connectionLines, id: \.id) { line in
                if case .connection(_, let neighbor, _, _) = line {
                    AccessibilityRotorEntry(
                        Text(connectionPhrase(neighbor)), id: line.id, in: rotorSpace)
                }
            }
        }
        .onAppear {
            // WCAG 2.4.3: coming back from an opened card lands on the
            // row that opened it, not the top.
            if let last = document.lastActivatedNode {
                DispatchQueue.main.async { focusedRow = last }
            }
        }
    }

    private var connectionLines: [Line] {
        lines.filter {
            if case .connection = $0 { return true }
            return false
        }
    }

    // MARK: Rows

    @ViewBuilder
    private func lineView(_ line: Line) -> some View {
        switch line {
        case .node(let row):
            nodeRow(row)
        case .connection(_, let neighbor, let ordinal, let total):
            connectionRow(neighbor, ordinal: ordinal, total: total)
        }
    }

    private func nodeRow(_ row: CanvasOutlineRow) -> some View {
        HStack(spacing: Tokens.Spacing.xs) {
            Text(row.title)
                .font(
                    row.kind == "group"
                        ? Tokens.Typography.body.weight(.semibold) : Tokens.Typography.body
                )
                .padding(.leading, CGFloat(row.depth) * Tokens.Spacing.md)
            Spacer()
            if row.connectionCount > 0 {
                Text("\(row.connectionCount)")
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .accessibilityHidden(true)
            }
        }
        .contentShape(Rectangle())
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(CanvasCardRef(kind: row.kind, title: row.title).phrase)
        .accessibilityValue(nodeValue(row))
        .accessibilityHint(activationHint(row))
        .accessibilityAddTraits(.isButton)
        .accessibilityAction(named: "Open") { onActivate(row) }
        .accessibilityAction(named: "Toggle Mark") {
            selecting(row) { appState.canvasToggleMark() }
        }
        .accessibilityAction(named: "Delete") {
            selecting(row) { appState.canvasDeleteSelection() }
        }
        .accessibilityRotorEntry(id: row.nodeId, in: rotorSpace)
        .accessibilityFocused($focusedRow, equals: row.nodeId)
        .onTapGesture(count: 2) { onActivate(row) }
        // t0 M6 visible-control rule (#368): every core verb on the
        // row's context menu — Switch Control / Voice Control never
        // depend on chords. Each verb selects the row first.
        .contextMenu {
            // Link cards say what Open does (t5: never a raw-URL blob,
            // and the browser hand-off is explicit).
            Button(row.kind == "link" ? "Open in Browser" : "Open") { onActivate(row) }
            if row.kind == "text" {
                Button("Edit Card Text…") {
                    selecting(row) { appState.canvasEditCard() }
                }
                Button("Convert to Note…") {
                    selecting(row) { appState.canvasPromptConvertToNote() }
                }
            }
            Button("Create Connected Card") {
                selecting(row) { appState.canvasCreateConnectedCard() }
            }
            Button("Duplicate") {
                selecting(row) { appState.canvasDuplicate() }
            }
            if row.kind == "group" {
                Button("Rename Group…") {
                    selecting(row) { appState.canvasPromptRenameGroup() }
                }
            }
            if row.kind == "file" || row.kind == "image" {
                Button("Locate File…") {
                    selecting(row) { appState.canvasOpenLocate() }
                }
            }
            Divider()
            Button("Toggle Mark") {
                selecting(row) { appState.canvasToggleMark() }
            }
            Button("Connect To…") {
                selecting(row) { appState.canvasOpenConnectPicker() }
            }
            Button("Set Color…") {
                selecting(row) { appState.canvasPromptSetColor() }
            }
            Divider()
            Button("Move into Group…") {
                selecting(row) { appState.canvasPromptMoveIntoGroup() }
            }
            if !row.groupPath.isEmpty {
                Button("Remove from Group") {
                    selecting(row) { appState.canvasRemoveFromGroup() }
                }
            }
            Divider()
            Button(row.kind == "group" ? "Ungroup" : "Delete", role: .destructive) {
                selecting(row) { appState.canvasDeleteSelection() }
            }
        }
    }

    /// Context-menu verbs act on the selection: select the row the
    /// menu was opened on, then run the selection-scoped verb.
    private func selecting(_ row: CanvasOutlineRow, then verb: () -> Void) {
        appState.canvasSelect(nodeId: row.nodeId, in: document, announce: false)
        verb()
    }

    /// t0 §1.2 standard context + §3 inspectability: position, marked
    /// state, and color live in the VALUE — pull-readable, never
    /// announcement-only.
    private func nodeValue(_ row: CanvasOutlineRow) -> String {
        var value = "\(row.ordinalN) of \(row.totalM) in \(row.groupPath.last ?? "canvas")"
        if let color = row.colorName { value += ", \(color)" }
        if selection.marked.contains(row.nodeId) { value += ", marked" }
        return value
    }

    private func connectionRow(_ neighbor: CanvasNeighbor, ordinal: Int, total: Int) -> some View {
        HStack(spacing: Tokens.Spacing.xs) {
            Text(connectionPhrase(neighbor))
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .padding(.leading, Tokens.Spacing.lg)
            Spacer()
        }
        .contentShape(Rectangle())
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(connectionPhrase(neighbor))
        .accessibilityValue("connection \(ordinal) of \(total)")
        .accessibilityHint("Opens the connected card's row.")
        .accessibilityAddTraits(.isButton)
        .accessibilityAction(named: "Jump to Card") { jump(to: neighbor.otherNode) }
        .accessibilityAction(named: "Edit Connection") {
            appState.canvasOpenConnectionEditor(edgeId: neighbor.edgeId)
        }
        .accessibilityAction(named: "Delete Connection") {
            appState.canvasDeleteConnection(edgeId: neighbor.edgeId)
        }
        .onTapGesture(count: 2) { jump(to: neighbor.otherNode) }
        .contextMenu {
            Button("Jump to Card") { jump(to: neighbor.otherNode) }
            Button("Edit Connection…") {
                appState.canvasOpenConnectionEditor(edgeId: neighbor.edgeId)
            }
            Button("Delete Connection") {
                appState.canvasDeleteConnection(edgeId: neighbor.edgeId)
            }
        }
    }

    /// t0 §1.2 direction phrases honoring fromEnd/toEnd.
    private func connectionPhrase(_ neighbor: CanvasNeighbor) -> String {
        let other = CanvasCardRef(
            kind: "text", title: neighbor.otherTitle)
        let phrase: String
        switch neighbor.direction {
        case .outgoing: phrase = "Connects to"
        case .incoming: phrase = "Connected from"
        case .bidirectional, .undirected: phrase = "Linked with"
        }
        var text = "\(phrase) \"\(other.title)\""
        if let label = neighbor.label { text += ", labelled \"\(label)\"" }
        return text
    }

    // MARK: Selection & activation

    private var selectionBinding: Binding<String?> {
        Binding(
            get: { selection.selected },
            set: { newValue in
                guard let id = newValue else {
                    selection.selected = nil
                    return
                }
                // Connection lines are navigational, not selectable
                // state: selecting one jumps to the other card.
                if let line = lines.first(where: { $0.id == id }),
                    case .connection(_, let neighbor, _, _) = line
                {
                    jump(to: neighbor.otherNode)
                    return
                }
                let previous = selection.selected
                selection.selected = id
                announceMove(to: id, from: previous)
            }
        )
    }

    private func announceMove(to id: String, from previous: String?) {
        guard let row = document.outline.first(where: { $0.nodeId == id }) else { return }
        // Group boundary narration (t0 §1.2): compare containers.
        let previousPath = previous
            .flatMap { prev in document.outline.first { $0.nodeId == prev } }?
            .groupPath ?? []
        if row.groupPath != previousPath {
            if let entered = row.groupPath.last, !previousPath.contains(entered) {
                let count = document.outline.first { $0.title == entered && $0.kind == "group" }
                    .map { Int($0.totalM) }
                appState.canvasAnnouncer.announce(
                    .groupEntered(label: entered, cardCount: count ?? 0))
            } else if let left = previousPath.last, !row.groupPath.contains(left) {
                appState.canvasAnnouncer.announce(.groupLeft(label: left))
            }
        }
        appState.canvasAnnouncer.announce(
            .movedTo(
                card: CanvasCardRef(kind: row.kind, title: row.title),
                ordinal: row.ordinalN, total: row.totalM,
                container: row.groupPath.last,
                connectionCount: row.connectionCount,
                colorName: row.colorName,
                marked: selection.marked.contains(row.nodeId)))
    }

    private func jump(to nodeId: String) {
        // Capture the origin BEFORE mutating selection — group
        // enter/leave narration compares the two paths (Codoki #610).
        let origin = selection.selected
        selection.selected = nodeId
        focusedRow = nodeId
        announceMove(to: nodeId, from: origin)
    }

    private func activationHint(_ row: CanvasOutlineRow) -> String {
        switch row.kind {
        case "group": return "Group. Cards inside follow in the outline."
        case "text": return "Opens the card text."
        case "file": return "Opens the note in this tab."
        case "image": return "Media cards open with canvas actions, arriving in a later milestone slice."
        case "link": return "Opens the link in your browser."
        default: return ""
        }
    }

}
