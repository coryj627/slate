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

    /// Interim text-card detail (t2 R14): read-only content panel.
    @State private var detail: (title: String, text: String)?

    init(document: CanvasDocument, tabID: TabID) {
        self.document = document
        self.selection = document.selection
        self.tabID = tabID
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
        .sheet(isPresented: detailPresented) {
            detailPanel
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
        .accessibilityAction(named: "Open") { activate(row) }
        .accessibilityRotorEntry(id: row.nodeId, in: rotorSpace)
        .accessibilityFocused($focusedRow, equals: row.nodeId)
        .onTapGesture(count: 2) { activate(row) }
        .contextMenu {
            Button("Open") { activate(row) }
        }
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
        .onTapGesture(count: 2) { jump(to: neighbor.otherNode) }
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

    private var detailPresented: Binding<Bool> {
        Binding(
            get: { detail != nil },
            set: { if !$0 { detail = nil } }
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

    private func activate(_ row: CanvasOutlineRow) {
        document.lastActivatedNode = row.nodeId
        switch row.kind {
        case "text":
            guard let session = appState.currentSession, let handle = document.handle,
                let text = try? session.canvasNodeText(handle: handle, nodeId: row.nodeId)
            else { return }
            detail = (title: row.title, text: text ?? "")
        case "file":
            let target = document.target(of: row.nodeId)
            if target.lowercased().hasSuffix(".md") || target.lowercased().hasSuffix(".markdown") {
                appState.openFile(target, target: .currentTab)
            } else {
                appState.canvasAnnouncer.announce(
                    .status("Opening this file kind from the canvas arrives with canvas actions."))
            }
        case "image":
            appState.canvasAnnouncer.announce(
                .status("Opening media from the canvas arrives with canvas actions."))
        case "link":
            let target = document.target(of: row.nodeId)
            if let url = URL(string: target), appState.externalOpener(url) {
                appState.canvasAnnouncer.announce(.status("Opened \(row.title) in your browser."))
            } else {
                appState.canvasAnnouncer.announce(.error("The link could not be opened."))
            }
        default:
            break
        }
    }

    // MARK: Interim text detail (t2 R14 — #368 replaces with the editor)

    private var detailPanel: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
            Text(detail?.title ?? "")
                .font(Tokens.Typography.body.weight(.semibold))
            ScrollView {
                Text(detail?.text ?? "")
                    .font(Tokens.Typography.body)
                    .textSelection(.enabled)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            HStack {
                Spacer()
                Button("Close") { closeDetail() }
                    .keyboardShortcut(.cancelAction)
            }
        }
        .padding(Tokens.Spacing.lg)
        .frame(minWidth: 360, minHeight: 240)
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Card text: \(detail?.title ?? ""). Read-only until canvas editing arrives.")
    }

    private func closeDetail() {
        detail = nil
        // WCAG 2.4.3: focus returns to the card row that opened it.
        if let last = document.lastActivatedNode {
            DispatchQueue.main.async { focusedRow = last }
        }
    }
}
