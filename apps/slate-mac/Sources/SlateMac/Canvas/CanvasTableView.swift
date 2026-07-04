// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// The canvas table surface (Milestone T, #363): every card as a flat,
/// sortable row on `AccessibleDataGrid` v2 (#519) — the fastest way to
/// scan a canvas by attribute rather than by position.
///
/// - Columns: Type · Title · Group · Target · Connections · Color
///   (named preset text — #370's color-as-text rule lands structurally
///   here; contrast verification finalizes in Wave 5).
/// - Selection syncs the shared `CanvasSelection` in both directions;
///   activation opens the card exactly like the outline (one
///   activation semantic per kind across surfaces).
/// - Sort announcements ride the #518 funnel via the grid's injectable
///   announce hook (DoD §H) — the grid itself stays canvas-agnostic.
struct CanvasTableView: View {
    @EnvironmentObject var appState: AppState
    @ObservedObject var document: CanvasDocument
    @ObservedObject var selection: CanvasSelection
    /// Activation handler shared with the outline (kind routing lives
    /// in one place — the container wires both surfaces to it).
    let onActivate: (String) -> Void

    init(document: CanvasDocument, onActivate: @escaping (String) -> Void) {
        self.document = document
        self.selection = document.selection
        self.onActivate = onActivate
    }

    /// One table row (Identifiable over the node id).
    struct TableRow: Identifiable {
        let id: String
        let kind: String
        let title: String
        let group: String
        let target: String
        let connections: UInt32
        let color: String
    }

    private var rows: [TableRow] {
        // #373: the filter narrows the table too (a view, not a
        // mutation); ids come from the same filtered outline set.
        let keep = document.filterActive ? Set(document.filteredOutline.map(\.nodeId)) : nil
        return document.tableRows.filter { keep?.contains($0.nodeId) ?? true }.map { row in
            TableRow(
                id: row.nodeId,
                kind: row.kind,
                title: row.title,
                group: row.groupPath.last ?? "",
                target: row.target,
                connections: row.connectionCount,
                color: row.colorName ?? "")
        }
    }

    var body: some View {
        AccessibleDataGrid(
            columns: [
                .init("Type", cell: { $0.kind.capitalized }, sort: { $0.kind < $1.kind }),
                .init("Title", cell: { $0.title }, sort: {
                    $0.title.localizedCaseInsensitiveCompare($1.title) == .orderedAscending
                }),
                .init("Group", cell: { $0.group }, sort: {
                    $0.group.localizedCaseInsensitiveCompare($1.group) == .orderedAscending
                }),
                .init("Target", cell: { $0.target }, sort: { $0.target < $1.target }),
                .init(
                    "Connections", cell: { String($0.connections) },
                    sort: { $0.connections < $1.connections }),
                .init("Color", cell: { $0.color }, sort: { $0.color < $1.color }),
            ],
            rows: rows,
            summary: summary,
            accessibilityLabel: "Canvas table",
            selection: selectionBinding,
            onActivate: { onActivate($0.id) },
            rowActions: [
                .init("Open") { [onActivate] row in onActivate(row.id) },
                .init("Toggle Mark") { [appState, document] row in
                    appState.canvasSelect(nodeId: row.id, in: document, announce: false)
                    appState.canvasToggleMark()
                },
                .init("Delete") { [appState, document] row in
                    appState.canvasSelect(nodeId: row.id, in: document, announce: false)
                    appState.canvasDeleteSelection()
                },
            ],
            announce: { [weak appState] text in
                appState?.canvasAnnouncer.announce(.status(text))
            }
        )
    }

    private var summary: String {
        // Straight off tableRows — `rows` would pay an extra O(n)
        // display-row map per render (Codoki #612).
        let cards = document.tableRows.filter { $0.kind != "group" }.count
        let groups = document.tableRows.count - cards
        return "Canvas table: \(cards) card\(cards == 1 ? "" : "s"), \(groups) group\(groups == 1 ? "" : "s")."
    }

    private var selectionBinding: Binding<String?> {
        Binding(
            get: { selection.selected },
            set: { newValue in
                guard selection.selected != newValue else { return }
                selection.selected = newValue
                guard let id = newValue,
                    let row = document.outline.first(where: { $0.nodeId == id })
                else { return }
                appState.canvasAnnouncer.announce(
                    .movedTo(
                        card: CanvasCardRef(kind: row.kind, title: row.title),
                        ordinal: row.ordinalN, total: row.totalM,
                        container: row.groupPath.last,
                        connectionCount: row.connectionCount,
                        colorName: row.colorName,
                        marked: selection.marked.contains(id)))
            }
        )
    }
}
