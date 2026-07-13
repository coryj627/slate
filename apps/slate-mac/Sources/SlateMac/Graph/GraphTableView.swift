// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// The Graph tab's modes (U3 toggle pattern: one coherent AX tree per
/// mode). Table is the accessible-first grid; Diagram is the visual
/// force-directed projection (P2-3 #559).
enum GraphTabMode: String, CaseIterable {
    case table
    case diagram

    var title: String {
        switch self {
        case .table: return "Table"
        case .diagram: return "Diagram"
        }
    }
}

/// The Graph tab body (Milestone P, P1-2 #555): hosts the mode seam and,
/// in Table mode, the whole-graph grid + filter bar. One coherent AX
/// tree per mode (U3 toggle pattern).
struct GraphContainerView: View {
    @EnvironmentObject private var appState: AppState
    let tabID: TabID
    @State private var mode: GraphTabMode = .table
    @State private var showInspector = false

    var body: some View {
        VStack(spacing: 0) {
            filterBar
            Divider()
            switch mode {
            case .table:
                GraphTableView(tabID: tabID)
            case .diagram:
                diagramBody
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        // The inspector (Filters / Groups / Display / Forces) is a trailing
        // panel available in both modes (spec §P2-4).
        .inspector(isPresented: $showInspector) {
            GraphInspectorView()
                .inspectorColumnWidth(min: 260, ideal: 300, max: 380)
        }
        .navigationTitle("Graph")
        // Lazy load ONLY when nothing is loaded or in flight (round 3
        // finding 1): opening/activating the tab already kicks off a load
        // via `activateGraphTab`, so an unconditional mount-time load would
        // start a second redundant snapshot fetch. This still covers
        // session restore, where the tab mounts with no activation load.
        .onAppear {
            // Restore the last-used projection mode from the loaded config
            // (P2-4 #560); `activateGraphTab` loaded it before this mount.
            mode = appState.graphConfig.mode
            if appState.graphTableSnapshot == nil && !appState.graphTableLoading {
                appState.loadGraphTable()
            }
            if mode == .diagram { appState.ensureGraphDiagram() }
        }
        // The mode toggle owns the diagram's lifecycle: build on entering
        // Diagram, tear down on returning to Table so the settle loop and
        // layout session don't linger. One coherent AX tree per mode (U3).
        // The chosen mode persists to graph.json (restored on next open).
        .onChange(of: mode) { _, newMode in
            appState.setGraphMode(newMode)
            switch newMode {
            case .diagram:
                appState.ensureGraphDiagram()
                appState.graphAnnouncer.announce(.status("Diagram mode."))
            case .table:
                appState.resetGraphDiagramState()
                appState.graphAnnouncer.announce(.status("Table mode."))
            }
        }
        // A backend-filter change rebuilds the diagram's layout too, so
        // both projections track the same node set.
        .onChange(of: appState.graphTableFilter) { _, _ in
            if mode == .diagram { appState.buildGraphDiagram() }
        }
        .onDisappear {
            if mode == .diagram { appState.resetGraphDiagramState() }
        }
    }

    // MARK: Diagram mode (spec §P2-3)

    @ViewBuilder
    private var diagramBody: some View {
        if let model = appState.graphDiagramModel {
            GraphDiagramView(model: model, tabID: tabID, onSwitchToTable: { mode = .table })
        } else if let error = appState.graphDiagramError {
            Text(error)
                .foregroundStyle(Tokens.ColorRole.warningText)
                .padding(Tokens.Spacing.md)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .accessibilityLabel("Graph diagram error: \(error)")
        } else {
            HStack(spacing: Tokens.Spacing.sm) {
                ProgressView().controlSize(.small)
                Text("Laying out graph…").foregroundStyle(Tokens.ColorRole.textSecondary)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .accessibilityElement(children: .combine)
            .accessibilityLabel("Laying out graph.")
        }
    }

    // MARK: Filter bar (spec §P1-2)

    private var filterBar: some View {
        HStack(spacing: Tokens.Spacing.sm) {
            Picker("View", selection: $mode) {
                ForEach(GraphTabMode.allCases, id: \.self) { m in
                    Text(m.title).tag(m)
                }
            }
            .pickerStyle(.segmented)
            .fixedSize()
            .accessibilityLabel("Graph view mode")
            .accessibilityHint("Switch between the accessible table and the visual diagram.")

            // The quick name filter lives in the bar for Table mode; in
            // Diagram mode it (and Groups) live in the P2-4 inspector,
            // which applies the SAME name predicate to both projections.
            if mode == .table {
                TextField(
                    "Filter notes", text: appStateTextFilterBinding
                )
                .textFieldStyle(.roundedBorder)
                .frame(maxWidth: 240)
                .accessibilityLabel("Filter graph by note name")
            }

            Toggle("Attachments", isOn: filterToggle(\.includeAttachments))
                .accessibilityHint("Include attachment nodes.")
            Toggle("Unresolved", isOn: filterToggle(\.includeGhosts))
                .accessibilityHint("Include unresolved link targets.")
            Toggle("Orphans only", isOn: filterToggle(\.orphansOnly))
                .accessibilityHint("Show only notes with no links in or out.")

            Spacer()

            Button {
                showInspector.toggle()
            } label: {
                SlateSymbol.graphInspector.label("Inspector")
            }
            .help("Show the graph inspector — filters, colour groups, display, and forces.")
            .accessibilityLabel("Toggle graph inspector")
        }
        .toggleStyle(.checkbox)
        .padding(.horizontal, Tokens.Spacing.sm)
        .padding(.vertical, Tokens.Spacing.xs)
    }

    private var appStateTextFilterBinding: Binding<String> {
        Binding(
            get: { appState.graphTableTextFilter },
            set: {
                appState.graphTableTextFilter = $0
                appState.scheduleGraphConfigSave()  // persist the name filter (P2-4)
            })
    }

    /// A toggle bound to one field of the backend `GraphFilter`; setting
    /// it re-fetches the snapshot.
    private func filterToggle(_ key: WritableKeyPath<GraphFilter, Bool>) -> Binding<Bool> {
        Binding(
            get: { appState.graphTableFilter[keyPath: key] },
            set: { newValue in
                var f = appState.graphTableFilter
                f[keyPath: key] = newValue
                appState.setGraphTableFilter(f)
            })
    }
}

/// The whole-graph grid (Table mode). Rows come from the cached
/// snapshot, are text-filtered client-side, and sorted by the grid via
/// per-column comparators (default: Links in, descending — hubs first).
struct GraphTableView: View {
    @EnvironmentObject private var appState: AppState
    /// The owning Graph tab, so row actions target ITS group — not
    /// whichever split pane happens to hold global focus (review round 1
    /// finding 2).
    let tabID: TabID
    @State private var selection: GraphTableRow.ID?
    @State private var sortState: DataGridSortState? = DataGridSortState(
        columnIndex: GraphTableColumn.linksIn.rawValue, ascending: false)

    var body: some View {
        Group {
            if appState.graphTableLoading && appState.graphTableSnapshot == nil {
                loading
            } else if let error = appState.graphTableError {
                errorView(error)
            } else {
                let rows = filteredRows
                if rows.isEmpty {
                    LeafEmptyState(message: "No notes match the current filters.")
                } else {
                    grid(rows)
                }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        // The CLIENT-side text needle re-filters the cached snapshot
        // synchronously, so announce its resting count here (coalesced
        // via the announcer's `.filter` class, gated on an active graph
        // tab). The BACKEND toggles (graphTableFilter) instead trigger an
        // async re-fetch, so their count is announced only after the
        // fresh snapshot publishes — in `loadGraphTable` — never against
        // the stale one (round 2 finding 7).
        .onChange(of: appState.graphTableTextFilter) { _, _ in announceCount() }
        // A generation bump can reassign backend node ids, so any stale
        // selection must be re-validated against the fresh row set (our
        // id is the stable path/ghost key) and dropped if gone (finding 3).
        .onChange(of: appState.graphTableSnapshot?.generation) { _, _ in
            if let sel = selection, !allRows.contains(where: { $0.id == sel }) {
                selection = nil
            }
        }
        // Leaving the tab (switch/close) cancels any queued filter/nav
        // announcement so a stale count can't fire after the view is gone
        // (round 2 finding 8; vault open/close is covered by
        // resetGraphTableState).
        .onDisappear { appState.graphAnnouncer.cancelPending() }
    }

    private var loading: some View {
        HStack(spacing: Tokens.Spacing.sm) {
            ProgressView().controlSize(.small)
            Text("Loading graph…").foregroundStyle(Tokens.ColorRole.textSecondary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Loading graph.")
    }

    private func errorView(_ error: String) -> some View {
        Text(error)
            .foregroundStyle(Tokens.ColorRole.warningText)
            .padding(Tokens.Spacing.md)
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .accessibilityLabel("Graph error: \(error)")
    }

    private var allRows: [GraphTableRow] {
        guard let snap = appState.graphTableSnapshot else { return [] }
        return snap.nodes.map { GraphTableRow(node: $0, folder: $0.path.map { appState.folder(of: $0) } ?? "") }
    }

    private var filteredRows: [GraphTableRow] {
        var rows = allRows
        // Preset kind filter (P1-3 #556): the "unresolved" preset narrows
        // to ghosts, which the backend GraphFilter can't express.
        if let kind = appState.graphTableKindFilter {
            rows = rows.filter { $0.kind == kind }
        }
        // The SAME name predicate the Diagram applies (P2-4 single source
        // of truth — asserted by the filter-equivalence test).
        let needle = appState.graphTableTextFilter
        return rows.filter { AppState.graphNameMatches($0.label, needle: needle) }
    }

    private func announceCount() {
        // Gate at SCHEDULE time (skip entirely if the graph isn't the
        // active surface) AND pass a FIRE-TIME gate so a coalesced count
        // is dropped if focus leaves the graph within the debounce window
        // — e.g. clicking into another split pane, which leaves this view
        // mounted so `onDisappear` never fires (round 2 finding 7, round
        // 3 finding 2).
        guard appState.graphTabActive else { return }
        let shown = filteredRows.count
        let total = allRows.count
        appState.graphAnnouncer.announceFilterCount(
            "\(shown) of \(total) shown",
            gate: { [weak appState] in appState?.graphTabActive == true })
    }

    private func grid(_ rows: [GraphTableRow]) -> some View {
        AccessibleDataGrid(
            columns: GraphTableColumn.columns,
            rows: rows,
            summary: appState.graphTableSnapshot?.audioSummary ?? "",
            accessibilityLabel: "Graph, data grid",
            selection: $selection,
            sortState: $sortState,
            sortsRowsLocally: true,
            onActivate: { row in activate(row) },
            onActivateModified: { row in activateInNewTab(row) },
            showsRowContextMenu: true,
            rowActions: rowActions,
            announce: { [weak appState] text in
                appState?.graphAnnouncer.announce(.status(text))
            })
    }

    private func activate(_ row: GraphTableRow) {
        focusOwningGroup()
        if row.isGhost {
            appState.createNoteFromGhost(targetRaw: row.label)
        } else if let path = row.path {
            appState.openFile(path, target: .currentTab)
        }
    }

    /// ⌘Return / ⌘-double-click: open the row's note in a NEW tab (a
    /// ghost still resolves to Create note). Distinct from plain
    /// activation, which opens in place (round 2 finding 4).
    private func activateInNewTab(_ row: GraphTableRow) {
        focusOwningGroup()
        if row.isGhost {
            appState.createNoteFromGhost(targetRaw: row.label)
        } else if let path = row.path {
            appState.openFile(path, target: .newTab)
        }
    }

    /// Make the Graph tab's own group active before an open/new-tab
    /// action, so `.currentTab`/`.newTab` (which resolve against the
    /// active group) land in THIS pane rather than whichever split pane
    /// holds global focus (finding 2). A no-op when the tab is already
    /// active; when it isn't, `activateTab` focuses its group.
    private func focusOwningGroup() {
        guard appState.workspace.model.activeGroup.activeTabID != tabID else { return }
        appState.activateTab(tabID)
    }

    /// The row-action availability policy (finding 4), extracted so the
    /// ghost/real split is unit-testable without a live AppState: the
    /// four navigation actions need a real file; "Create note" applies
    /// only to a ghost. No row ever exposes an action that would
    /// silently do nothing.
    static func rowActionEnabled(_ name: String, isGhost: Bool) -> Bool {
        name == "Create note" ? isGhost : !isGhost
    }

    private var rowActions: [AccessibleDataGrid<GraphTableRow>.RowAction] {
        func enabled(_ name: String) -> (GraphTableRow) -> Bool {
            { Self.rowActionEnabled(name, isGhost: $0.isGhost) }
        }
        return [
            .init("Open", isEnabled: enabled("Open")) { row in
                if let p = row.path { focusOwningGroup(); appState.openFile(p, target: .currentTab) }
            },
            .init("Open in New Tab", isEnabled: enabled("Open in New Tab")) { row in
                if let p = row.path { focusOwningGroup(); appState.openFile(p, target: .newTab) }
            },
            .init("Show connections", isEnabled: enabled("Show connections")) { row in
                // reRootConnections funnels through openFile(.currentTab),
                // so it too must target the graph's own pane (finding 2).
                if let p = row.path { focusOwningGroup(); appState.reRootConnections(on: p) }
            },
            .init("Reveal in File Tree", isEnabled: enabled("Reveal in File Tree")) { row in
                // revealInFileTree also opens the file (.currentTab).
                if let p = row.path { focusOwningGroup(); appState.revealInFileTree(p) }
            },
            .init("Create note", isEnabled: enabled("Create note")) { row in
                if row.isGhost { focusOwningGroup(); appState.createNoteFromGhost(targetRaw: row.label) }
            },
        ]
    }
}

// MARK: - Row model

/// One graph-table row: a node's nine columns (spec §P1-2).
struct GraphTableRow: Identifiable {
    /// STABLE identity: the vault path for real nodes, `"g:<encoded key>"`
    /// for ghosts — NOT the backend node id, which is only stable within
    /// one generation and is reassigned on a rebuild (a stale numeric
    /// selection could otherwise activate a different note — round 1
    /// finding 3, the P1-1 lesson).
    let id: String
    /// The backend node id — VOLATILE across rebuilds, so NEVER used for
    /// selection identity. Kept solely as the absolute last-resort sort
    /// tie-break, which makes ordering a strict total order WITHIN a
    /// snapshot without depending on `id` uniqueness (round 3 finding 3).
    let nodeID: UInt64
    let label: String
    let path: String?
    let kind: GraphNodeKind
    let linksIn: UInt32
    let linksOut: UInt32
    let embedsIn: UInt32
    let embedsOut: UInt32
    let component: UInt32
    let modifiedMs: Int64?
    let folder: String

    var isGhost: Bool { kind == .ghost }

    var kindLabel: String {
        switch kind {
        case .note: return "Note"
        case .attachment: return "Attachment"
        case .ghost: return "Unresolved"
        }
    }

    var modifiedText: String {
        guard let ms = modifiedMs else { return "" }
        return GraphTableRow.dateFormatter.string(
            from: Date(timeIntervalSince1970: Double(ms) / 1000))
    }

    init(node: GraphNode, folder: String) {
        // Stable, collision-proof identity in two DISJOINT namespaces so
        // a real vault file can never share an id with a ghost (round 2
        // finding 3): real nodes key on their unique path under "p:";
        // ghosts (no path) key on their normalized label under "g:".
        //
        // The ghost key is PERCENT-ENCODED (round 3 finding 1): the
        // backend keys ghosts on the raw UTF-8 bytes of the folded target
        // (no Unicode NFC), so two normalization-variant targets (e.g.
        // "café" composed vs decomposed) are DISTINCT backend nodes — but
        // their Swift `String` labels compare canonically EQUAL, which
        // would collapse two rows onto one id. Percent-encoding keys on
        // the UTF-8 bytes, so byte-distinct labels stay distinct ids while
        // ASCII labels remain legible ("missing note" → "missing%20note").
        if let path = node.path {
            self.id = "p:\(path)"
        } else {
            let folded = node.label.lowercased()
            let encoded =
                folded.addingPercentEncoding(withAllowedCharacters: .alphanumerics) ?? folded
            self.id = "g:\(encoded)"
        }
        self.nodeID = node.id
        self.label = node.label
        self.path = node.path
        self.kind = node.kind
        self.linksIn = node.inLinks
        self.linksOut = node.outLinks
        self.embedsIn = node.inEmbeds
        self.embedsOut = node.outEmbeds
        self.component = node.component
        self.modifiedMs = node.modifiedMs
        self.folder = folder
    }

    private static let dateFormatter: DateFormatter = {
        let f = DateFormatter()
        f.dateStyle = .medium
        f.timeStyle = .short
        return f
    }()
}

/// The nine sortable columns (spec §P1-2), in display order. `rawValue`
/// is the column index the grid's `DataGridSortState` uses.
enum GraphTableColumn: Int, CaseIterable {
    case note = 0
    case linksIn
    case linksOut
    case embedsIn
    case embedsOut
    case component
    case modified
    case folder
    case kind

    /// Label order with two tie-breaks: the stable string `id`, then the
    /// backend `nodeID`. Row ids are unique across the disjoint
    /// `p:`/`g:` namespaces, but the final `nodeID` break guarantees a
    /// STRICT TOTAL ORDER on distinct rows UNCONDITIONALLY — the sort's
    /// determinism does not depend on `id` uniqueness, so no two distinct
    /// rows can ever compare equal in both directions (round 1 finding 8,
    /// round 2 finding 9, round 3 finding 3). `nodeID` is unique within
    /// the snapshot being sorted, which is all a per-render sort needs.
    static func byLabel(_ a: GraphTableRow, _ b: GraphTableRow) -> Bool {
        switch a.label.localizedStandardCompare(b.label) {
        case .orderedAscending: return true
        case .orderedDescending: return false
        case .orderedSame:
            if a.id != b.id { return a.id < b.id }
            return a.nodeID < b.nodeID
        }
    }

    /// The directional comparator for a column — exposed (not just
    /// baked into the private `Column.directionalSort`) so it's unit
    /// testable.
    func directionalComparator(_ a: GraphTableRow, _ b: GraphTableRow, ascending: Bool) -> Bool {
        // Numeric primary key, label tie-break (label ALWAYS ascending
        // so ties are stable regardless of the primary direction).
        func numeric(_ lhs: UInt32, _ rhs: UInt32) -> Bool {
            if lhs != rhs { return ascending ? lhs < rhs : lhs > rhs }
            return Self.byLabel(a, b)
        }
        switch self {
        case .note:
            return ascending ? Self.byLabel(a, b) : Self.byLabel(b, a)
        case .linksIn: return numeric(a.linksIn, b.linksIn)
        case .linksOut: return numeric(a.linksOut, b.linksOut)
        case .embedsIn: return numeric(a.embedsIn, b.embedsIn)
        case .embedsOut: return numeric(a.embedsOut, b.embedsOut)
        case .component: return numeric(a.component, b.component)
        case .modified:
            let l = a.modifiedMs ?? .min
            let r = b.modifiedMs ?? .min
            if l != r { return ascending ? l < r : l > r }
            return Self.byLabel(a, b)
        case .folder:
            if a.folder != b.folder {
                let cmp = a.folder.localizedStandardCompare(b.folder) == .orderedAscending
                return ascending ? cmp : !cmp
            }
            return Self.byLabel(a, b)
        case .kind:
            if a.kindLabel != b.kindLabel {
                let cmp = a.kindLabel < b.kindLabel
                return ascending ? cmp : !cmp
            }
            return Self.byLabel(a, b)
        }
    }

    static var columns: [AccessibleDataGrid<GraphTableRow>.Column] {
        allCases.map { col in
            AccessibleDataGrid<GraphTableRow>.Column(
                col.header,
                cell: col.cell,
                directionalSort: { col.directionalComparator($0, $1, ascending: $2) })
        }
    }

    var header: String {
        switch self {
        case .note: return "Note"
        case .linksIn: return "Links in"
        case .linksOut: return "Links out"
        case .embedsIn: return "Embeds in"
        case .embedsOut: return "Embeds out"
        case .component: return "Component"
        case .modified: return "Modified"
        case .folder: return "Folder"
        case .kind: return "Kind"
        }
    }

    var cell: (GraphTableRow) -> String {
        switch self {
        case .note: return { $0.label }
        case .linksIn: return { String($0.linksIn) }
        case .linksOut: return { String($0.linksOut) }
        case .embedsIn: return { String($0.embedsIn) }
        case .embedsOut: return { String($0.embedsOut) }
        case .component: return { String($0.component) }
        case .modified: return { $0.modifiedText }
        case .folder: return { $0.folder }
        case .kind: return { $0.kindLabel }
        }
    }
}
