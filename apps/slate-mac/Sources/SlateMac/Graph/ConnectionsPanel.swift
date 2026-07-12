// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// The Connections leaf (Milestone P, P1-1 #554): the active note's
/// local graph neighborhood as structured "Linked from" / "Links to"
/// lists — the local graph, projected accessibly. Depth 1 shows the
/// direct in/out split with snippets; depth 2–3 nest each row's own
/// neighbors recursively (already fetched, not re-queried).
///
/// Rendered as a `List` so arrow-key row navigation and ←/→
/// expand-collapse are native; Return / ⌘Return / ⌥↑ / ⌥↓ / ⌘[ are
/// handled explicitly (spec §P1-1 Interaction). All announcements route
/// through `appState.graphAnnouncer`.
struct ConnectionsPanel: View {
    @EnvironmentObject private var appState: AppState
    @State private var selection: ConnectionRow.ID?

    var body: some View {
        Group {
            if appState.connectionsEffectivePath == nil {
                LeafEmptyState(message: "Select a note to see its connections.")
            } else {
                loaded
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .navigationTitle("Connections")
        .onAppear { if appState.connectionsLeafActiveForView { appState.loadConnections() } }
        .onChange(of: appState.selectedFilePath) { _, _ in
            // Follow the selection only when not re-rooted; load only
            // when the leaf is the active one (the panel stays mounted).
            if appState.connectionsRootPath == nil, appState.connectionsLeafActiveForView {
                appState.loadConnections()
            }
        }
    }

    @ViewBuilder
    private var loaded: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
                .padding(.horizontal, Tokens.Spacing.sm)
                .padding(.top, Tokens.Spacing.xs)
            Divider()
            content
        }
        // ⌘[ back is panel-level, not List-level, so it works even when
        // focus is on the depth picker (review round 2 finding 4); it
        // falls through when there's no re-root to pop.
        .onKeyPress(keys: [KeyEquivalent("[")]) { press in
            guard press.modifiers.contains(.command) else { return .ignored }
            return appState.connectionsBack() ? .handled : .ignored
        }
    }

    // MARK: Header + depth control

    private var header: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.xs) {
            Text(headerTitle)
                .font(Tokens.Typography.sectionHeader)
                .accessibilityAddTraits(.isHeader)
            if let summary = currentSummary {
                Text(summary)
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
            }
            depthControl
        }
    }

    /// Title + summary track the payload's OWN path, so a note switch
    /// never shows the new header over the old note's summary
    /// (review round 1 finding 9).
    private var headerTitle: String {
        appState.connectionsEffectivePath.map { appState.filename(of: $0) } ?? "Connections"
    }

    private var currentSummary: String? {
        guard isPayloadCurrent else { return nil }
        return appState.connectionsNeighborhood?.audioSummary
    }

    private var isPayloadCurrent: Bool {
        appState.connectionsLoadedPath != nil
            && appState.connectionsLoadedPath == appState.connectionsEffectivePath
    }

    private var depthControl: some View {
        Picker(
            "Local graph depth",
            selection: Binding(
                get: { appState.connectionsDepth },
                set: { appState.setConnectionsDepth($0) })
        ) {
            Text("Links").tag(1)
            Text("2 links away").tag(2)
            Text("3 links away").tag(3)
        }
        .pickerStyle(.segmented)
        .accessibilityLabel("Local graph depth")
        .accessibilityHint("How many links away from this note to include.")
    }

    // MARK: Content

    @ViewBuilder
    private var content: some View {
        if !isPayloadCurrent {
            // The published payload is for a different note (mid-load
            // after a switch) or absent — show loading, never stale rows.
            loadingRow
        } else if let error = appState.connectionsError {
            Text(error)
                .font(Tokens.Typography.callout)
                .foregroundStyle(Tokens.ColorRole.warningText)
                .padding(Tokens.Spacing.sm)
                .frame(maxWidth: .infinity, alignment: .leading)
                .accessibilityLabel("Connections error: \(error)")
        } else if let model {
            connectionsList(model)
        } else {
            Text("This note has no connections.")
                .font(Tokens.Typography.callout)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .padding(Tokens.Spacing.sm)
                .frame(maxWidth: .infinity, alignment: .leading)
                .accessibilityLabel("This note has no connections.")
        }
    }

    private var loadingRow: some View {
        HStack(spacing: Tokens.Spacing.sm) {
            ProgressView().controlSize(.small)
            Text("Loading connections…")
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
        }
        .padding(Tokens.Spacing.sm)
        .frame(maxWidth: .infinity, alignment: .leading)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Loading connections.")
    }

    private func connectionsList(_ model: ConnectionsModel) -> some View {
        List(selection: $selection) {
            sectionView("Linked from", rows: model.incoming, empty: "Nothing links here.")
            sectionView("Links to", rows: model.outgoing, empty: "This note links to nothing.")
        }
        .listStyle(.inset)
        // Keyboard contract (spec §P1-1). Native ↑/↓ move the List
        // selection and ←/→ expand/collapse; these add the rest.
        .onKeyPress(keys: [.return]) { press in
            guard let id = selection, let row = model.row(id: id) else { return .ignored }
            if press.modifiers.contains(.command) {
                if let path = row.path { appState.openFile(path, target: .newTab) }
            } else {
                activate(row)
            }
            return .handled
        }
        .onKeyPress(keys: [.upArrow, .downArrow]) { press in
            // ⌥↑ / ⌥↓ jump between the two section anchors.
            guard press.modifiers.contains(.option) else { return .ignored }
            jumpSection(down: press.key == .downArrow, model: model)
            return .handled
        }
    }

    @ViewBuilder
    private func sectionView(_ title: String, rows: [ConnectionRow], empty: String) -> some View {
        Section {
            if rows.isEmpty {
                Text(empty)
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .accessibilityLabel(empty)
            } else {
                ForEach(rows) { row in
                    OutlineGroup(row, children: \.nestedOrNil) { node in
                        rowLabel(node)
                    }
                }
            }
        } header: {
            Text("\(title), \(rows.count) \(rows.count == 1 ? "note" : "notes")")
                .accessibilityAddTraits(.isHeader)
        }
    }

    // MARK: Row

    private func rowLabel(_ row: ConnectionRow) -> some View {
        Button {
            activate(row)
        } label: {
            VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
                HStack(spacing: Tokens.Spacing.xs) {
                    Text(row.label)
                        .font(Tokens.Typography.callout)
                        .foregroundStyle(
                            row.isGhost ? Tokens.ColorRole.warningText : Tokens.ColorRole.textPrimary
                        )
                        .strikethrough(row.isGhost, color: Tokens.ColorRole.warningText)
                    badge(row)
                }
                if let snippet = row.snippet, !snippet.isEmpty {
                    Text(snippet)
                        .font(Tokens.Typography.caption)
                        .foregroundStyle(Tokens.ColorRole.textSecondary)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            // macOS control-size floor (#866/#884; review round 1
            // finding 10): a 15pt callout line pads to ~19pt without
            // this — below the 20pt HIG minimum. 28 is the preferred
            // default target.
            .frame(minHeight: 28)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityLabel(appState.graphAnnouncer.rowPhrase(row.rowRef))
        .accessibilityHint(
            row.isGhost ? "Unresolved. Choose Create note to add it." : "Opens the note.")
        .contextMenu { rowMenu(row) }
    }

    @ViewBuilder
    private func rowMenu(_ row: ConnectionRow) -> some View {
        if row.isGhost {
            Button("Create note") { appState.createNoteFromGhost(targetRaw: row.targetRaw) }
        } else if let path = row.path {
            Button("Open") { appState.openFile(path, target: .currentTab) }
            Button("Open in New Tab") { appState.openFile(path, target: .newTab) }
            Button("Show connections") { appState.reRootConnections(on: path) }
            Button("Reveal in File Tree") { appState.revealInFileTree(path) }
        }
    }

    private func activate(_ row: ConnectionRow) {
        if row.isGhost {
            appState.createNoteFromGhost(targetRaw: row.targetRaw)
        } else if let path = row.path {
            appState.openFile(path, target: .currentTab)
        }
    }

    /// ⌥↑ / ⌥↓: move selection to the first row of the previous / next
    /// section (spec: jump between sections).
    private func jumpSection(down: Bool, model: ConnectionsModel) {
        let anchors = [model.incoming.first?.id, model.outgoing.first?.id].compactMap { $0 }
        guard !anchors.isEmpty else { return }
        if down {
            selection = anchors.count > 1 ? anchors[1] : anchors[0]
        } else {
            selection = anchors[0]
        }
    }

    @ViewBuilder
    private func badge(_ row: ConnectionRow) -> some View {
        if row.isGhost {
            badgeText("Unresolved").foregroundStyle(Tokens.ColorRole.warningText)
        } else if row.isEmbed {
            badgeText("Embed")
        } else if row.isAttachment {
            badgeText("Attachment")
        }
    }

    private func badgeText(_ text: String) -> some View {
        Text(text)
            .font(.caption2)
            .padding(.horizontal, Tokens.Spacing.xs)
            .padding(.vertical, 1)
            .overlay(
                RoundedRectangle(cornerRadius: Tokens.Radius.chip)
                    .stroke(Tokens.ColorRole.separator, lineWidth: 0.5)
            )
            .accessibilityHidden(true)  // role folded into the row label
    }

    // MARK: Model

    private var model: ConnectionsModel? {
        guard isPayloadCurrent, let hood = appState.connectionsNeighborhood else { return nil }
        return ConnectionsModel(
            hood: hood, bundle: appState.connectionsBundle,
            depth: AppState.clampConnectionsDepth(appState.connectionsDepth))
    }
}

// MARK: - View model

/// One connection row: a neighbor node plus how it relates (aggregated
/// edge kinds) and, at depth 1, its snippet. `nested` holds this node's
/// own neighbors for depths ≥ 2.
struct ConnectionRow: Identifiable {
    /// Per-OCCURRENCE identity: a direction-prefixed traversal path
    /// (e.g. "out/2/4"), NOT the bare node id. A reciprocal neighbor
    /// (both incoming and outgoing) or a depth-3 diamond would reuse a
    /// node id in one `List`/`OutlineGroup`, breaking selection; the
    /// path makes every rendered occurrence unique (review round 2
    /// finding 1).
    let id: String
    /// The underlying graph node id (shared across occurrences).
    let nodeId: UInt64
    let label: String
    let path: String?
    let targetRaw: String
    let isGhost: Bool
    let isAttachment: Bool
    /// Embed-only: reached solely by `![[…]]` edges, no plain link.
    let isEmbed: Bool
    let linksIn: UInt32
    let linksOut: UInt32
    let references: UInt32
    let snippet: String?
    var nested: [ConnectionRow] = []

    /// `nil` (not empty) when there are no children, so `OutlineGroup`
    /// renders a leaf with no disclosure chevron.
    var nestedOrNil: [ConnectionRow]? { nested.isEmpty ? nil : nested }

    var rowRef: GraphRowRef {
        GraphRowRef(
            label: label, linksIn: linksIn, linksOut: linksOut,
            isGhost: isGhost, references: references, isEmbed: isEmbed)
    }
}

/// Splits a `GraphNeighborhood` into first-hop incoming / outgoing rows
/// (by edge direction from the center), aggregating parallel/typed
/// edges per neighbor and nesting deeper hops recursively (cycle-safe).
struct ConnectionsModel {
    let incoming: [ConnectionRow]
    let outgoing: [ConnectionRow]

    private let byId: [String: ConnectionRow]

    init(hood: GraphNeighborhood, bundle: NoteLoadBundle?, depth: Int) {
        let nodesById = Dictionary(uniqueKeysWithValues: hood.nodes.map { ($0.id, $0) })
        let center = hood.centerId

        // Undirected adjacency: node id → set of (neighbor id, kind).
        // Aggregating here means a node reached by BOTH a link and an
        // embed edge is ONE neighbor whose kinds are merged — no
        // duplicate `Identifiable` ids (review round 1 finding 6).
        var adjacency: [UInt64: [UInt64: Set<GraphEdgeKind>]] = [:]
        // Direction of the center's own incident edges, for the split.
        var incomingIds: [UInt64: Set<GraphEdgeKind>] = [:]
        var outgoingIds: [UInt64: Set<GraphEdgeKind>] = [:]
        for edge in hood.edges {
            adjacency[edge.sourceId, default: [:]][edge.targetId, default: []].insert(edge.kind)
            adjacency[edge.targetId, default: [:]][edge.sourceId, default: []].insert(edge.kind)
            if edge.sourceId == center, edge.targetId != center {
                outgoingIds[edge.targetId, default: []].insert(edge.kind)
            }
            if edge.targetId == center, edge.sourceId != center {
                incomingIds[edge.sourceId, default: []].insert(edge.kind)
            }
            // A self-edge (center→center) is not a connection to another
            // note; deliberately omitted from both lists.
        }

        // Snippet overlay (depth 1).
        var inSnippet: [String: String] = [:]
        var outSnippet: [String: String] = [:]
        if let bundle {
            for b in bundle.backlinks.items where !b.snippet.isEmpty {
                inSnippet[b.sourcePath] = b.snippet
            }
            for o in bundle.outgoingLinks where !o.snippet.isEmpty {
                if let tp = o.targetPath { outSnippet[tp] = o.snippet }
            }
        }

        func leaf(_ id: UInt64, kinds: Set<GraphEdgeKind>, snippet: String?, idString: String)
            -> ConnectionRow?
        {
            guard let node = nodesById[id] else { return nil }
            let embedOnly = !kinds.contains(.link) && kinds.contains(.embed)
            return ConnectionRow(
                id: idString, nodeId: node.id, label: node.label, path: node.path,
                targetRaw: node.label, isGhost: node.kind == .ghost,
                isAttachment: node.kind == .attachment, isEmbed: embedOnly,
                linksIn: node.inLinks, linksOut: node.outLinks,
                references: node.inLinks + node.inEmbeds, snippet: snippet)
        }

        // Recursively attach a node's neighbors (undirected), excluding
        // ancestors on the current path (cycle guard) — so depth 3
        // actually renders the third hop (review round 1 finding 8).
        // Child ids extend the parent's path so a diamond descendant
        // gets a distinct id under each parent (round 2 finding 1).
        func children(
            of id: UInt64, ancestors: Set<UInt64>, remaining: Int, parentId: String
        ) -> [ConnectionRow] {
            guard remaining > 0 else { return [] }
            let nextAncestors = ancestors.union([id])
            var rows: [ConnectionRow] = []
            for (neighborId, kinds) in adjacency[id] ?? [:] where !nextAncestors.contains(neighborId) {
                let childId = "\(parentId)/\(neighborId)"
                guard var row = leaf(neighborId, kinds: kinds, snippet: nil, idString: childId)
                else { continue }
                row.nested = children(
                    of: neighborId, ancestors: nextAncestors, remaining: remaining - 1,
                    parentId: childId)
                rows.append(row)
            }
            return rows.sorted { $0.label.localizedStandardCompare($1.label) == .orderedAscending }
        }

        // depth counts hops from center: first-hop rows, then up to
        // depth-1 further levels of nesting.
        let nestDepth = max(0, depth - 1)
        let ancestors: Set<UInt64> = [center]
        func firstHop(
            _ ids: [UInt64: Set<GraphEdgeKind>], snippets: [String: String], prefix: String
        ) -> [ConnectionRow] {
            ids.compactMap { (id, kinds) -> ConnectionRow? in
                let rowId = "\(prefix)/\(id)"
                guard
                    var row = leaf(
                        id, kinds: kinds,
                        snippet: nodesById[id]?.path.flatMap { snippets[$0] }, idString: rowId)
                else { return nil }
                row.nested = children(
                    of: id, ancestors: ancestors, remaining: nestDepth, parentId: rowId)
                return row
            }
            .sorted { $0.label.localizedStandardCompare($1.label) == .orderedAscending }
        }

        self.incoming = firstHop(incomingIds, snippets: inSnippet, prefix: "in")
        self.outgoing = firstHop(outgoingIds, snippets: outSnippet, prefix: "out")

        // Flat id → row index for keyboard activation (every occurrence,
        // keyed by its unique path id).
        var index: [String: ConnectionRow] = [:]
        func collect(_ rows: [ConnectionRow]) {
            for r in rows {
                index[r.id] = r
                collect(r.nested)
            }
        }
        collect(self.incoming)
        collect(self.outgoing)
        self.byId = index
    }

    func row(id: String) -> ConnectionRow? { byId[id] }
}
