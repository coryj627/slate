// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Per-vault workspace-layout persistence (U1-6, #458):
/// `<vault>/.slate/workspace.json`, schema v1.
///
/// Follows the `PrefsJsonStore` discipline: bounded read (256 KiB — a
/// layout is a few KiB; a larger file is corrupt or hostile), atomic write
/// (temp + rename in the same directory), decode failures degrade to a
/// fresh default — never a crash, never a half-restore.
///
/// Forward compatibility: tabs whose `item.kind` is unknown (the reserved
/// "graph" discriminator from Milestone P, or future kinds) are
/// DROPPED on decode rather than failing the workspace; an unknown
/// top-level `version` yields nil (fresh default). Unsaved buffers are
/// deliberately NOT persisted — the dirty-close and vault-close gates
/// already guarantee nothing is silently lost, and persisting stale dirty
/// text would resurrect edits the user resolved differently.
struct WorkspaceStore {
    static let maxFileBytes = 256 * 1024
    static let schemaVersion = 1

    let vaultRoot: URL

    var fileURL: URL {
        vaultRoot.appendingPathComponent(".slate/workspace.json")
    }

    // MARK: Codable schema (versioned, additive-only)

    struct Snapshot: Codable, Equatable {
        var version: Int
        var activeGroup: UUID
        var root: Node
        /// Per-tab view state (U3 extends this with `mode`).
        var activeLeaf: String?
    }

    indirect enum Node: Codable, Equatable {
        case group(id: UUID, activeTab: UUID?, tabs: [Tab])
        case split(axis: String, weights: [Double], children: [Node])

        private enum CodingKeys: String, CodingKey {
            case kind, id, activeTab, tabs, axis, weights, children
        }

        init(from decoder: Decoder) throws {
            let c = try decoder.container(keyedBy: CodingKeys.self)
            switch try c.decode(String.self, forKey: .kind) {
            case "group":
                self = .group(
                    id: try c.decode(UUID.self, forKey: .id),
                    activeTab: try c.decodeIfPresent(UUID.self, forKey: .activeTab),
                    tabs: try c.decode([FailableTab].self, forKey: .tabs)
                        .compactMap(\.tab))
            case "split":
                self = .split(
                    axis: try c.decode(String.self, forKey: .axis),
                    weights: try c.decode([Double].self, forKey: .weights),
                    children: try c.decode([Node].self, forKey: .children))
            case let other:
                throw DecodingError.dataCorruptedError(
                    forKey: .kind, in: c, debugDescription: "unknown node kind '\(other)'")
            }
        }

        func encode(to encoder: Encoder) throws {
            var c = encoder.container(keyedBy: CodingKeys.self)
            switch self {
            case .group(let id, let activeTab, let tabs):
                try c.encode("group", forKey: .kind)
                try c.encode(id, forKey: .id)
                try c.encodeIfPresent(activeTab, forKey: .activeTab)
                try c.encode(tabs, forKey: .tabs)
            case .split(let axis, let weights, let children):
                try c.encode("split", forKey: .kind)
                try c.encode(axis, forKey: .axis)
                try c.encode(weights, forKey: .weights)
                try c.encode(children, forKey: .children)
            }
        }
    }

    struct Tab: Codable, Equatable {
        var id: UUID
        var item: Item
        /// Per-tab view mode (U3-2): `"reading"` or absent. Absent (or any
        /// unknown string) restores as editing — backward compatible with
        /// pre-U3 snapshots, forward compatible for older builds (unknown
        /// keys are ignored by Codable).
        var mode: String?
        /// Per-tab properties-widget collapse (U3-3): `true` or absent.
        /// Absent restores as expanded (the default) — same compatibility
        /// rules as `mode`.
        var propsCollapsed: Bool?
        /// Canvas tabs only (#369): the active sub-surface ("table" or
        /// "visual") or absent. Absent (or any unknown string) restores
        /// as the outline — the structured-first default. Additive and
        /// optional, so pre-T snapshots decode unchanged and older
        /// builds ignore the key.
        var activeCanvasSurface: String?
    }

    struct Item: Codable, Equatable {
        var kind: String
        var path: String
        var id: String? = nil
        var name: String? = nil
    }

    /// Unknown tab kinds decode to nil instead of failing the whole
    /// snapshot (forward compatibility with N/P tab types).
    private struct FailableTab: Codable {
        let tab: Tab?
        init(from decoder: Decoder) throws {
            let decoded = try? Tab(from: decoder)
            let known = ["markdown", "canvas", "base", "savedQuery", "dashboard"]
            self.tab = (decoded.map { known.contains($0.item.kind) } == true) ? decoded : nil
        }
        func encode(to encoder: Encoder) throws {
            try tab?.encode(to: encoder)
        }
    }

    // MARK: IO

    /// Nil on missing file, over-budget file, unknown version, or any
    /// decode failure — the caller starts fresh. Never throws.
    func load() -> Snapshot? {
        guard let handle = try? FileHandle(forReadingFrom: fileURL) else { return nil }
        defer { try? handle.close() }
        guard let data = try? handle.read(upToCount: Self.maxFileBytes + 1),
            data.count <= Self.maxFileBytes,
            let snapshot = try? JSONDecoder().decode(Snapshot.self, from: data),
            snapshot.version == Self.schemaVersion
        else { return nil }
        return snapshot
    }

    /// Atomic temp+rename in `.slate/` (same volume ⇒ atomic). Errors are
    /// reported to the caller for logging; layout persistence must never
    /// interrupt the user.
    func save(_ snapshot: Snapshot) throws {
        let dir = fileURL.deletingLastPathComponent()
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        let data = try encoder.encode(snapshot)
        let tmp = dir.appendingPathComponent("workspace.json.tmp-\(UUID().uuidString)")
        try data.write(to: tmp, options: [])
        _ = try FileManager.default.replaceItemAt(fileURL, withItemAt: tmp)
    }

    // MARK: Model ⇄ snapshot

    static func snapshot(
        of model: WorkspaceModel, activeLeaf: String? = nil,
        viewModes: [TabID: NoteViewMode] = [:],
        propertiesCollapsed: Set<TabID> = [],
        canvasSurfaces: [TabID: CanvasSurface] = [:]
    ) -> Snapshot {
        Snapshot(
            version: schemaVersion,
            activeGroup: model.activeGroupID.raw,
            root: node(
                of: modelRoot(model), viewModes: viewModes,
                propertiesCollapsed: propertiesCollapsed,
                canvasSurfaces: canvasSurfaces),
            activeLeaf: activeLeaf)
    }

    /// The per-tab collapsed set captured in a snapshot (U3-3). Sparse:
    /// only `true` entries are stored.
    static func propertiesCollapsed(from snapshot: Snapshot) -> Set<TabID> {
        var out: Set<TabID> = []
        collectCollapsed(snapshot.root, into: &out)
        return out
    }

    private static func collectCollapsed(_ node: Node, into out: inout Set<TabID>) {
        switch node {
        case .group(_, _, let tabs):
            for tab in tabs where tab.propsCollapsed == true {
                out.insert(TabID(raw: tab.id))
            }
        case .split(_, _, let children):
            for child in children { collectCollapsed(child, into: &out) }
        }
    }

    /// The per-tab canvas sub-surfaces captured in a snapshot (#369).
    /// Sparse: only non-outline entries are stored; unknown strings drop
    /// (restore falls back to the outline — structured-first default).
    static func canvasSurfaces(from snapshot: Snapshot) -> [TabID: CanvasSurface] {
        var out: [TabID: CanvasSurface] = [:]
        collectCanvasSurfaces(snapshot.root, into: &out)
        return out
    }

    private static func collectCanvasSurfaces(
        _ node: Node, into out: inout [TabID: CanvasSurface]
    ) {
        switch node {
        case .group(_, _, let tabs):
            for tab in tabs {
                if let raw = tab.activeCanvasSurface,
                    let surface = CanvasSurface(rawValue: raw),
                    surface != .outline
                {
                    out[TabID(raw: tab.id)] = surface
                }
            }
        case .split(_, _, let children):
            for child in children { collectCanvasSurfaces(child, into: &out) }
        }
    }

    /// The per-tab view modes captured in a snapshot (U3-2). Only non-default
    /// entries are stored, so the result is sparse; unknown strings are
    /// dropped (restore falls back to editing).
    static func viewModes(from snapshot: Snapshot) -> [TabID: NoteViewMode] {
        var out: [TabID: NoteViewMode] = [:]
        collectModes(snapshot.root, into: &out)
        return out
    }

    private static func collectModes(
        _ node: Node, into out: inout [TabID: NoteViewMode]
    ) {
        switch node {
        case .group(_, _, let tabs):
            for tab in tabs {
                if let raw = tab.mode, let mode = NoteViewMode(rawValue: raw),
                    mode != .editing
                {
                    out[TabID(raw: tab.id)] = mode
                }
            }
        case .split(_, _, let children):
            for child in children { collectModes(child, into: &out) }
        }
    }

    private static func modelRoot(_ model: WorkspaceModel) -> SplitNode {
        // `root` is private(set); the reconstruction initializer + groups
        // walk below only need the public surface. Mirror access is
        // test-proven (WorkspaceModelTests) and stable within the module.
        Mirror(reflecting: model).descendant("root") as! SplitNode
    }

    private static func node(
        of splitNode: SplitNode, viewModes: [TabID: NoteViewMode] = [:],
        propertiesCollapsed: Set<TabID> = [],
        canvasSurfaces: [TabID: CanvasSurface] = [:]
    ) -> Node {
        switch splitNode {
        case .group(let group):
            return .group(
                id: group.id.raw,
                activeTab: group.activeTabID?.raw,
                tabs: group.tabs.map { tab in
                    Tab(
                        id: tab.id.raw, item: item(of: tab.item),
                        // Sparse: only non-default state is written.
                        mode: viewModes[tab.id].map(\.rawValue),
                        propsCollapsed: propertiesCollapsed.contains(tab.id) ? true : nil,
                        activeCanvasSurface: canvasSurfaces[tab.id].map(\.rawValue))
                })
        case .split(let branch):
            return .split(
                axis: branch.axis == .horizontal ? "horizontal" : "vertical",
                weights: branch.weights,
                children: branch.children.map {
                    node(
                        of: $0, viewModes: viewModes,
                        propertiesCollapsed: propertiesCollapsed,
                        canvasSurfaces: canvasSurfaces)
                })
        }
    }

    private static func item(of editorItem: EditorItem) -> Item {
        switch editorItem {
        case .markdown(let path):
            return Item(kind: "markdown", path: path)
        case .canvas(let path):
            return Item(kind: "canvas", path: path)
        case .base(let path):
            return Item(kind: "base", path: path)
        case .savedQuery(let id, let name):
            return Item(kind: "savedQuery", path: id, id: id, name: name)
        case .dashboard(let id, let name):
            return Item(kind: "dashboard", path: id, id: id, name: name)
        }
    }

    /// Rebuild a model from a snapshot. Returns nil when the result would
    /// violate the invariants (never half-restore): the caller falls back
    /// to a fresh workspace. Tabs for `missingFiles` are KEPT — the tab
    /// renders a per-tab error state ("<name> was moved or deleted."),
    /// which is U1-6's graceful-degradation contract; only structurally
    /// invalid snapshots are rejected.
    static func model(from snapshot: Snapshot) -> WorkspaceModel? {
        guard let root = splitNode(from: snapshot.root) else { return nil }
        let model = WorkspaceModel(
            root: root, activeGroupID: GroupID(raw: snapshot.activeGroup))
        guard model.validate().isEmpty else { return nil }
        return model
    }

    private static func splitNode(from node: Node) -> SplitNode? {
        switch node {
        case .group(let id, let activeTab, let tabs):
            let workspaceTabs = tabs.map { tab -> WorkspaceTab in
                let item: EditorItem
                switch tab.item.kind {
                case "canvas":
                    item = .canvas(path: tab.item.path)
                case "base":
                    item = .base(path: tab.item.path)
                case "savedQuery":
                    item = .savedQuery(
                        id: tab.item.id ?? tab.item.path,
                        name: tab.item.name ?? "Saved query")
                case "dashboard":
                    item = .dashboard(
                        id: tab.item.id ?? tab.item.path,
                        name: tab.item.name ?? "Dashboard")
                default:
                    item = .markdown(path: tab.item.path)
                }
                return WorkspaceTab(id: TabID(raw: tab.id), item: item)
            }
            // Repair a dangling active pointer (dropped unknown-kind tab).
            let active: TabID? = {
                guard let activeTab else { return workspaceTabs.first?.id }
                let id = TabID(raw: activeTab)
                return workspaceTabs.contains { $0.id == id }
                    ? id : workspaceTabs.first?.id
            }()
            return .group(
                TabGroupNode(
                    id: GroupID(raw: id), tabs: workspaceTabs, activeTabID: active))
        case .split(let axis, let weights, let children):
            let mapped = children.compactMap(splitNode(from:))
            guard mapped.count == children.count, mapped.count >= 2,
                weights.count == mapped.count
            else { return nil }
            return .split(
                SplitBranch(
                    axis: axis == "vertical" ? .vertical : .horizontal,
                    children: mapped,
                    weights: weights))
        }
    }
}
