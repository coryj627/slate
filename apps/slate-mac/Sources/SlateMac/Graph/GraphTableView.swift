// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// The Graph tab body (Milestone P, P1-2 #555): hosts the mode seam and,
/// in Table mode, the whole-graph grid + filter bar. One coherent AX
/// tree per mode (U3 toggle pattern).
struct GraphContainerView: View {
    @EnvironmentObject private var appState: AppState
    let tabID: TabID
    // The two modes are core's `GraphSurfaceMode` (W6-2 PR 0a; the local
    // enum it replaced is gone — `GraphAnnouncer.swift` carries the
    // picker titles and the persistence tags).
    @State private var mode: GraphSurfaceMode = .table
    @State private var showInspector = false
    /// Bumped on every USER mode switch so the newly-shown projection moves
    /// VoiceOver focus onto the shared-selected node — the row (Table) or
    /// the node's AX element (Diagram). WCAG 2.4.3 focus landing, P2-5 #561
    /// review finding 3. Stays 0 through the initial mount + the mode
    /// RESTORE (tab activation owns that focus), so a plain open never yanks
    /// focus off the picker.
    @State private var focusToken = 0
    /// Set in `onAppear` when restoring a persisted NON-default mode, so the
    /// restore-driven `onChange(mode)` doesn't count as a user switch and
    /// bump `focusToken` (P2-5 review round-2 finding 3). Consumed once.
    @State private var suppressFocusBumpOnce = false

    var body: some View {
        VStack(spacing: 0) {
            filterBar
            Divider()
            switch mode {
            case .table:
                GraphTableView(tabID: tabID, focusRequest: focusToken)
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
            // A non-default restore changes `mode`, which fires the
            // `onChange` below — mark it so it isn't treated as a user
            // switch that steals focus (review round-2 finding 3).
            if appState.graphConfig.mode != mode { suppressFocusBumpOnce = true }
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
            // Land VoiceOver focus on the shared-selected node in the new
            // projection (finding 3). The bump is a ONE-SHOT decided HERE,
            // at switch time: only on a genuine USER switch (not the restore,
            // round-2 finding 3) AND only when there's a node to land on RIGHT
            // NOW. Deciding at switch time — rather than a reactive
            // `selection != nil ? token : 0` — means a LATER selection change
            // can't retroactively unmask a stale token and steal focus from
            // another split (round-3 finding c).
            if suppressFocusBumpOnce {
                suppressFocusBumpOnce = false
            } else if appState.graphSelectedNodeKey != nil {
                focusToken += 1
            }
            switch newMode {
            case .diagram:
                appState.ensureGraphDiagram()
                appState.graphAnnouncer.announce(.graphMode(mode: .diagram))
            case .table:
                appState.resetGraphDiagramState()
                appState.graphAnnouncer.announce(.graphMode(mode: .table))
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
            GraphDiagramView(
                model: model, tabID: tabID, onSwitchToTable: { mode = .table },
                focusRequest: focusToken)
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
                ForEach(GraphSurfaceMode.allCases, id: \.self) { m in
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

/// The whole-graph grid (Table mode). Rows come from core, formatted,
/// filtered and ordered there for the accepted request (W6-2 PR 0b,
/// 0b-7 — the grid sorts and filters nothing itself), over core's
/// column model (default sort: Links in, descending — hubs first).
struct GraphTableView: View {
    @EnvironmentObject private var appState: AppState
    /// The owning Graph tab, so row actions target ITS group — not
    /// whichever split pane happens to hold global focus (review round 1
    /// finding 2).
    let tabID: TabID
    /// Bumped by the container when Table becomes the active mode so the
    /// grid takes first-responder on the selected row — the Diagram→Table
    /// focus landing (P2-5 #561 review finding 3). 0 on plain mount.
    var focusRequest: Int = 0
    /// The grid's sort state: reads the ACCEPTED sort, and a header
    /// interaction issues a REQUEST token (design A); the accepted sort
    /// changes only when its rows publish.
    private var sortState: Binding<DataGridSortState?> {
        Binding(
            get: {
                DataGridSortState(
                    columnIndex: GraphTableColumns.index(of: appState.graphTableSort.column),
                    ascending: appState.graphTableSort.ascending)
            },
            set: { state in
                guard let state, let column = GraphTableColumns.column(at: state.columnIndex) else { return }
                appState.setGraphTableSort(GraphTableSort(column: column, ascending: state.ascending))
            })
    }
    /// The token actually handed to the grid — captured ONCE at appear (this
    /// view is recreated per switch) and ONLY when the selected row is
    /// currently visible, so a selection that cleared between the switch-time
    /// bump and consumption doesn't leave the grid focused on nothing (P2-5
    /// review round-4 race). Not reactive to later selection changes.
    @State private var gridFocusRequest = 0

    /// The grid's selection is the SHARED `graphSelectedNodeKey` (P2-5
    /// #561): the Table row id IS that cross-projection key, so binding the
    /// grid straight to it makes a Table selection visible to the Diagram
    /// (and vice versa) with no translation.
    private var selection: Binding<GraphTableRow.ID?> {
        Binding(
            get: { appState.graphSelectedNodeKey },
            set: { appState.graphSelectedNodeKey = $0 })
    }

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
        // Capture the focus request ONCE, here, re-checking that the shared
        // selection is a CURRENTLY-visible row — closes the round-4 race
        // where the selection cleared after the switch-time bump. Only a
        // present, visible selection hands the grid a non-zero request.
        .onAppear {
            if focusRequest != 0,
                let key = appState.graphSelectedNodeKey,
                filteredRows.contains(where: { $0.id == key })
            {
                gridFocusRequest = focusRequest
            }
        }
        // A needle or kind change is a token change (design A): core
        // re-answers the rows, and the count is announced when they publish.
        .onChange(of: appState.graphTableTextFilter) { _, _ in appState.requestGraphTableRows() }
        .onChange(of: appState.graphTableKindFilter) { _, _ in appState.requestGraphTableRows() }
        // A generation bump can reassign backend node ids, so any stale
        // selection must be re-validated against the fresh row set (our
        // id is the stable path/ghost key) and dropped if gone (finding 3).
        .onChange(of: appState.graphTableSnapshot?.generation) { _, _ in
            if let snap = appState.graphTableSnapshot {
                appState.revalidateGraphSelection(against: snap)
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

    /// Core's rows for the accepted request: the needle and the preset's
    /// kind overlay are in the query (0b-2), so nothing is filtered here.
    private var filteredRows: [GraphTableRow] { appState.graphTableRows }

    private func grid(_ rows: [GraphTableRow]) -> some View {
        AccessibleDataGrid(
            columns: GraphTableColumns.columns(
                ghostCreationDisabledReason: appState.structuralMutationDisabledReason),
            rows: rows,
            summary: appState.graphTableSnapshot?.audioSummary ?? "",
            accessibilityLabel: "Graph, data grid",
            selection: selection,
            sortState: sortState,
            // Core orders the rows (0b-7); the grid never re-sorts them.
            sortsRowsLocally: false,
            onActivate: { row in activate(row) },
            onActivateModified: { row in activateInNewTab(row) },
            showsRowContextMenu: true,
            rowActions: rowActions,
            focusRequest: gridFocusRequest,
            // The grid's own events relay with THEIR priority (0a-D2).
            announce: { [weak appState] event in
                appState?.graphAnnouncer.relay(event)
            })
    }

    private func activate(_ row: GraphTableRow) {
        activate(row, fileTarget: .currentTab)
    }

    /// ⌘Return / ⌘-double-click: open the row's note in a NEW tab (a
    /// ghost still resolves to Create note). Distinct from plain
    /// activation, which opens in place (round 2 finding 4).
    private func activateInNewTab(_ row: GraphTableRow) {
        activate(row, fileTarget: .newTab)
    }

    /// Keep ordinary and modified activation on one availability gate so a
    /// busy ghost cannot enter the structural rejection funnel from either
    /// Return or double-click. Real notes remain available in both targets.
    private func activate(_ row: GraphTableRow, fileTarget: AppState.OpenTarget) {
        focusOwningGroup()
        if row.isGhost {
            guard appState.structuralMutationDisabledReason == nil else { return }
            appState.createNoteFromGhost(targetRaw: row.label)
        } else if let path = row.path {
            appState.openFile(path, target: fileTarget)
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

    /// The row-action availability policy, now delegating to the CANONICAL
    /// `GraphRowAction` set shared by every projection (P2-5 #561): the four
    /// navigation actions need a real file; "Create note" applies only to a
    /// ghost. Kept as a thin wrapper so the existing unit tests (which key
    /// off the label string) still pass.
    static func rowActionEnabled(_ name: String, isGhost: Bool) -> Bool {
        GraphRowAction.allCases.first { $0.title == name }?.applies(toGhost: isGhost) ?? false
    }

    /// The grid's row actions, built from the ONE canonical `GraphRowAction`
    /// set so the Table's action labels + availability can never drift from
    /// the Diagram's / Connections' (P2-5 #561, DoD §P-B parity). Every
    /// open/re-root path first activates the graph's own group so it lands
    /// in this pane (finding 2).
    private var rowActions: [AccessibleDataGrid<GraphTableRow>.RowAction] {
        GraphRowAction.allCases.map { action in
            .init(
                action.title,
                isVisible: { row in action.applies(toGhost: row.isGhost) },
                isEnabled: { row in
                    action.applies(toGhost: row.isGhost)
                        && (action != .createNote
                            || appState.structuralMutationDisabledReason == nil)
                },
                disabledReason: { row in
                    guard action == .createNote, row.isGhost else { return nil }
                    return appState.structuralMutationDisabledReason
                }
            ) { row in
                focusOwningGroup()
                Self.perform(action, row: row, appState: appState)
            }
        }
    }

    /// Run a canonical action against a table row — the single dispatch the
    /// grid actions and (via the same enum) every projection route through.
    static func perform(_ action: GraphRowAction, row: GraphTableRow, appState: AppState) {
        switch action {
        case .open:
            if let p = row.path { appState.openFile(p, target: .currentTab) }
        case .openInNewTab:
            if let p = row.path { appState.openFile(p, target: .newTab) }
        case .showConnections:
            if let p = row.path { appState.reRootConnections(on: p) }
        case .reveal:
            if let p = row.path { appState.revealInFileTree(p) }
        case .createNote:
            if row.isGhost { appState.createNoteFromGhost(targetRaw: row.label) }
        }
    }
}

// MARK: - Row model

// `GraphTableRow` is the generated record core formats and orders (W6-2
// PR 0b, 0b-7): the nine cells, the stable key, the node id. The Swift
// derivation — `init(node:folder:)`, the date formatter, `byLabel` and
// the comparator — is deleted; what remains is the sugar the grid
// and the actions read.

extension GraphTableRow: Identifiable {
    /// STABLE identity: core's `stable_key` — the vault path under `p:`
    /// for real nodes, the percent-encoded ghost key under `g:` — never the
    /// backend node id, which is reassigned on a rebuild.
    public var id: String { stableKey }

    var isGhost: Bool { kind == .ghost }
}

/// Core's column model (0b-7, design B): the ordered specs, fetched ONCE;
/// the grid is built from the vector, the sort state's index IS the
/// vector index, and a row's cell for index `i` is `cells[i]`. No column
/// is listed here.
enum GraphTableColumns {
    static let specs: [GraphTableColumnSpec] = graphTableColumns()

    static func index(of column: GraphTableColumn) -> Int {
        specs.firstIndex { $0.column == column } ?? 0
    }

    static func column(at index: Int) -> GraphTableColumn? {
        specs.indices.contains(index) ? specs[index].column : nil
    }

    static var columns: [AccessibleDataGrid<GraphTableRow>.Column] {
        columns(ghostCreationDisabledReason: nil)
    }

    static func columns(ghostCreationDisabledReason: String?)
        -> [AccessibleDataGrid<GraphTableRow>.Column]
    {
        specs.enumerated().map { index, spec in
            AccessibleDataGrid<GraphTableRow>.Column(
                spec.header,
                cell: { row in row.cells.indices.contains(index) ? row.cells[index] : "" },
                accessibilityHint: { row in
                    row.isGhost ? ghostCreationDisabledReason : nil
                })
        }
    }
}
