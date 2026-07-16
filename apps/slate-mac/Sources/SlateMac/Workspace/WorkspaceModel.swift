// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// The pure workspace model (Milestone U1-1, #453): a tree of split nodes →
/// tab groups → ordered tabs, each tab holding a typed `EditorItem`.
///
/// This file has no SwiftUI dependency by design. Every mutation is a value-
/// semantic operation on `WorkspaceModel`, and `validate()` checks the full
/// invariant set (I1–I7, `docs/plans/08_ui_parity/specs/u1_spec.md`) so the
/// census tests can assert them after every operation — in release mode, not
/// behind `debug_assert`-style guards (the project's #404 lesson).
///
/// Views never mutate the model directly; `WorkspaceState` (U1-2) is the
/// single mutation funnel.

// MARK: - Identity

struct TabID: Hashable, Codable {
    let raw: UUID
    init() { self.raw = UUID() }
    init(raw: UUID) { self.raw = raw }
}

struct GroupID: Hashable, Codable {
    let raw: UUID
    init() { self.raw = UUID() }
    init(raw: UUID) { self.raw = raw }
}

/// Component-safe longest-prefix lookup for physical batch path transforms.
/// Keys use byte-exact identities because `.base` paths must not collapse
/// canonically-equivalent UTF-8 spellings in Swift dictionaries.
struct VaultComponentPrefixIndex<Value> {
    struct Entry {
        let path: String
        let includesDescendants: Bool
        let value: Value
    }

    struct Match {
        let entry: Entry
        /// Components below `entry.path`; empty for an exact match.
        let relativeSuffix: String
    }

    private final class Node {
        var children: [String: Node] = [:]
        var exactEntry: Entry?
        var directoryEntry: Entry?
        var containsEntryInSubtree = false
    }

    private let root: Node

    init(_ entries: [Entry]) {
        let root = Node()
        for entry in entries {
            let components = Self.components(of: entry.path)
            var node = root
            node.containsEntryInSubtree = true
            for component in components {
                let key = Self.key(component)
                if let child = node.children[key] {
                    node = child
                } else {
                    let child = Node()
                    node.children[key] = child
                    node = child
                }
                node.containsEntryInSubtree = true
            }
            if entry.includesDescendants {
                if node.directoryEntry == nil { node.directoryEntry = entry }
            } else if node.exactEntry == nil {
                node.exactEntry = entry
            }
        }
        self.root = root
    }

    func longestMatch(for candidate: String) -> Match? {
        var ignored = 0
        return longestMatch(for: candidate, componentVisits: &ignored)
    }

    /// Test-visible deterministic work counter: exactly one visit per candidate
    /// component, regardless of how many indexed roots exist.
    func longestMatch(for candidate: String, componentVisits: inout Int) -> Match? {
        let components = Self.components(of: candidate)
        var node = root
        var deepestDirectory: (entry: Entry, matchedLength: Int)? =
            root.directoryEntry.map { ($0, 0) }

        for (offset, component) in components.enumerated() {
            componentVisits += 1
            guard let child = node.children[Self.key(component)] else { break }
            node = child
            if let directory = node.directoryEntry {
                deepestDirectory = (directory, offset + 1)
            }
            if offset == components.count - 1, let exact = node.exactEntry {
                return Match(entry: exact, relativeSuffix: "")
            }
        }
        if let deepestDirectory {
            return Match(
                entry: deepestDirectory.entry,
                relativeSuffix: components.dropFirst(deepestDirectory.matchedLength)
                    .joined(separator: "/"))
        }
        return nil
    }

    /// Whether the candidate is itself an indexed root or contains one below
    /// it. Destructive directory mutations use this inverse lookup so Trash of
    /// an ancestor cannot bypass an exact quarantined descendant. Traversal is
    /// O(candidate components), independent of the number of indexed roots.
    func containsEntry(atOrBelow candidate: String) -> Bool {
        var node = root
        for component in Self.components(of: candidate) {
            guard let child = node.children[Self.key(component)] else {
                return false
            }
            node = child
        }
        return node.containsEntryInSubtree
    }

    private static func components(of path: String) -> [String] {
        // Match Rust `Path::components()` for valid vault-relative inputs:
        // repeated/trailing separators and CurDir (`.`) components are
        // stripped, while byte-exact normal components remain untouched.
        // Admission rejects absolute and `..` paths before querying the trie.
        path.split(separator: "/", omittingEmptySubsequences: true)
            .filter { $0 != "." }
            .map(String.init)
    }

    private static func key(_ component: String) -> String {
        BaseExactIdentity.registryKey(prefix: "vault-component", value: component)
    }
}

// MARK: - Tab content

/// What a tab shows: a markdown note, canvas (Milestone T, #369), or
/// base (Milestone N, #702).
///
/// Milestones N (Bases) and P (Graph) add cases here plus a renderer in
/// `TabGroupView` — nothing else in the shell changes. The persistence
/// schema (U1-6) reserves the discriminators "base" and "graph": a
/// decoder seeing an unknown kind drops that tab rather than failing the
/// whole workspace, so sessions saved by newer builds downgrade
/// gracefully (tab dropped, never a crash).
enum EditorItem: Hashable, Codable {
    case markdown(path: String)
    case canvas(path: String)
    case base(path: String)
    case savedQuery(id: String, name: String)
    case dashboard(id: String, name: String)
    /// The global graph tab (Milestone P, P1-2 #554/#555). A SINGLETON
    /// with no path payload — one graph tab per workspace; `openTab`'s
    /// identity dedup activates the existing tab instead of duplicating.
    case graph

    static func == (lhs: EditorItem, rhs: EditorItem) -> Bool {
        switch (lhs, rhs) {
        case (.markdown(let lhs), .markdown(let rhs)),
            (.canvas(let lhs), .canvas(let rhs)),
            (.base(let lhs), .base(let rhs)):
            return BaseExactIdentity.matches(lhs, rhs)
        case (.savedQuery(let lhsID, let lhsName),
            .savedQuery(let rhsID, let rhsName)),
            (.dashboard(let lhsID, let lhsName),
                .dashboard(let rhsID, let rhsName)):
            return BaseExactIdentity.matches(lhsID, rhsID)
                && BaseExactIdentity.matches(lhsName, rhsName)
        case (.graph, .graph):
            return true
        default:
            return false
        }
    }

    func hash(into hasher: inout Hasher) {
        switch self {
        case .markdown(let path):
            hasher.combine(0)
            BaseExactIdentity.hash(path, into: &hasher)
        case .canvas(let path):
            hasher.combine(1)
            BaseExactIdentity.hash(path, into: &hasher)
        case .base(let path):
            hasher.combine(2)
            BaseExactIdentity.hash(path, into: &hasher)
        case .savedQuery(let id, let name):
            hasher.combine(3)
            BaseExactIdentity.hash(id, into: &hasher)
            BaseExactIdentity.hash(name, into: &hasher)
        case .dashboard(let id, let name):
            hasher.combine(4)
            BaseExactIdentity.hash(id, into: &hasher)
            BaseExactIdentity.hash(name, into: &hasher)
        case .graph:
            hasher.combine(5)
        }
    }

    /// The vault-relative file path behind this tab, regardless of kind.
    var path: String {
        switch self {
        case .markdown(let path), .canvas(let path), .base(let path):
            return path
        case .savedQuery(let id, _):
            return "saved-query:\(id)"
        case .dashboard(let id, _):
            return "dashboard:\(id)"
        case .graph:
            // Namespaced synthetic key (the savedQuery/dashboard
            // convention) so the path-keyed dedup in
            // `activeGroupTab(forPath:)` can't confuse the singleton
            // graph tab with a real vault file literally named "graph"
            // (review round 1 finding 5).
            return "graph:singleton"
        }
    }

    var title: String {
        switch self {
        case .savedQuery(_, let name), .dashboard(_, let name):
            return name
        case .graph:
            return "Graph"
        case .markdown(let path), .canvas(let path), .base(let path):
            let name = (path as NSString).lastPathComponent
            let withoutExtension = (name as NSString).deletingPathExtension
            return withoutExtension.isEmpty ? name : withoutExtension
        }
    }

    private enum CodingKeys: String, CodingKey { case kind, path, id, name }

    init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        let kind = try c.decode(String.self, forKey: .kind)
        switch kind {
        case "markdown":
            self = .markdown(path: try c.decode(String.self, forKey: .path))
        case "canvas":
            self = .canvas(path: try c.decode(String.self, forKey: .path))
        case "base":
            self = .base(path: try c.decode(String.self, forKey: .path))
        case "savedQuery":
            self = .savedQuery(
                id: try c.decodeIfPresent(String.self, forKey: .id)
                    ?? c.decode(String.self, forKey: .path),
                name: try c.decodeIfPresent(String.self, forKey: .name) ?? "Saved query")
        case "dashboard":
            self = .dashboard(
                id: try c.decodeIfPresent(String.self, forKey: .id)
                    ?? c.decode(String.self, forKey: .path),
                name: try c.decodeIfPresent(String.self, forKey: .name) ?? "Dashboard")
        case "graph":
            // Singleton — any `path` field is ignored.
            self = .graph
        default:
            throw DecodingError.dataCorruptedError(
                forKey: .kind, in: c,
                debugDescription: "unknown EditorItem kind '\(kind)'")
        }
    }

    func encode(to encoder: Encoder) throws {
        var c = encoder.container(keyedBy: CodingKeys.self)
        switch self {
        case .markdown(let path):
            try c.encode("markdown", forKey: .kind)
            try c.encode(path, forKey: .path)
        case .canvas(let path):
            try c.encode("canvas", forKey: .kind)
            try c.encode(path, forKey: .path)
        case .base(let path):
            try c.encode("base", forKey: .kind)
            try c.encode(path, forKey: .path)
        case .savedQuery(let id, let name):
            try c.encode("savedQuery", forKey: .kind)
            try c.encode(id, forKey: .path)
            try c.encode(id, forKey: .id)
            try c.encode(name, forKey: .name)
        case .dashboard(let id, let name):
            try c.encode("dashboard", forKey: .kind)
            try c.encode(id, forKey: .path)
            try c.encode(id, forKey: .id)
            try c.encode(name, forKey: .name)
        case .graph:
            try c.encode("graph", forKey: .kind)
            // Write the namespaced synthetic path so the Item schema
            // stays populated; older builds drop the unknown tab.
            try c.encode("graph:singleton", forKey: .path)
        }
    }
}

// MARK: - Tree nodes

struct WorkspaceTab: Identifiable, Hashable {
    let id: TabID
    var item: EditorItem
}

struct TabGroupNode: Hashable {
    let id: GroupID
    var tabs: [WorkspaceTab]
    /// Invariant I2: non-nil iff `tabs` is non-empty, and always a member.
    var activeTabID: TabID?

    var activeTab: WorkspaceTab? {
        guard let activeTabID else { return nil }
        return tabs.first { $0.id == activeTabID }
    }
}

struct SplitBranch: Hashable {
    enum Axis: Hashable, Codable {
        /// Children side-by-side (a vertical divider between them).
        case horizontal
        /// Children stacked top-to-bottom.
        case vertical
    }

    let axis: Axis
    /// Invariant I4: count ≥ 2; no child is a `.split` with the same axis.
    var children: [SplitNode]
    /// Invariant I5: parallel to `children`, each ≥ `WorkspaceModel.minWeight`,
    /// summing to 1.
    var weights: [Double]
}

indirect enum SplitNode: Hashable {
    case group(TabGroupNode)
    case split(SplitBranch)
}

// MARK: - Model

struct WorkspaceModel: Hashable {
    struct FileTabRetarget {
        let tabID: TabID
        let oldItem: EditorItem
        let newItem: EditorItem
        let oldPath: String
        let newPath: String
    }
    /// Weights are normalized fractions of the parent axis; no pane may fall
    /// below this fraction (keyboard/drag resize clamps here — a pane can
    /// never be resized to invisibility, which would strand focus).
    static let minWeight: Double = 0.15
    /// Keyboard resize step (⌘⌥+ / ⌘⌥-).
    static let resizeStep: Double = 0.05
    /// Global pane cap: at most ⌊1/minWeight⌋ = 6 groups in the workspace.
    /// `split` REJECTS at capacity. The cap is global (not per-axis) because
    /// structural collapses can MERGE same-axis branches: dissolving a
    /// cross-axis intermediary flattens its same-axis child into the parent,
    /// so a per-axis split-time check still admitted 7-child branches whose
    /// 0.15 floor is unsatisfiable (census-found, seeds 11 and 198). With ≤ 6
    /// groups total, every branch's floor is satisfiable unconditionally.
    static let maxGroups = Int((1.0 / minWeight).rounded(.down))

    private(set) var root: SplitNode
    /// Invariant I1: always names a group present in the tree.
    private(set) var activeGroupID: GroupID

    /// A fresh workspace: one empty group (the only state where a group may
    /// be empty — invariant I3).
    init() {
        let group = TabGroupNode(id: GroupID(), tabs: [], activeTabID: nil)
        self.root = .group(group)
        self.activeGroupID = group.id
    }

    /// Rebuild from persisted parts (U1-6). The caller must run `validate()`
    /// and fall back to `init()` on violations; this initializer does not
    /// normalize.
    init(root: SplitNode, activeGroupID: GroupID) {
        self.root = root
        self.activeGroupID = activeGroupID
    }

    // MARK: Queries

    var activeGroup: TabGroupNode {
        group(activeGroupID) ?? Self.firstGroup(in: root)
    }

    /// All groups in tree (reading) order: depth-first, children in order —
    /// which for our geometry is left→right, top→bottom.
    var groupsInOrder: [TabGroupNode] {
        Self.collectGroups(root)
    }

    var allTabs: [WorkspaceTab] {
        groupsInOrder.flatMap(\.tabs)
    }

    var isEmpty: Bool {
        allTabs.isEmpty
    }

    func group(_ id: GroupID) -> TabGroupNode? {
        groupsInOrder.first { $0.id == id }
    }

    func groupContaining(_ tab: TabID) -> TabGroupNode? {
        groupsInOrder.first { $0.tabs.contains { $0.id == tab } }
    }

    func tab(_ id: TabID) -> WorkspaceTab? {
        allTabs.first { $0.id == id }
    }

    /// 1-based spatial ordinal of a group ("Editor pane N of M"), reading
    /// order. Returns nil for unknown groups.
    func ordinal(of id: GroupID) -> Int? {
        groupsInOrder.firstIndex { $0.id == id }.map { $0 + 1 }
    }

    // MARK: Open / close / select

    /// Open `item` in `groupID` (default: active group). Dedup rule: if the
    /// group already has a tab for `item`, that tab is selected instead of
    /// opening a duplicate (a different group may hold the same item).
    /// `allowDuplicate` bypasses dedup for the EXPLICIT duplicate gesture
    /// (⌘T "Duplicate Tab") — navigation-opens keep the dedup behavior.
    /// (U1-2 tests caught the conflict: with unconditional dedup,
    /// duplicate-tab was a silent no-op.)
    @discardableResult
    mutating func openTab(
        _ item: EditorItem, in groupID: GroupID? = nil, activate: Bool = true,
        allowDuplicate: Bool = false
    ) -> TabID {
        let targetID = groupID.flatMap { group($0) != nil ? $0 : nil } ?? activeGroupID
        var opened: TabID!
        updateGroup(targetID) { group in
            if !allowDuplicate,
                let existing = group.tabs.first(where: { $0.item == item }) {
                opened = existing.id
                if activate { group.activeTabID = existing.id }
                return
            }
            let tab = WorkspaceTab(id: TabID(), item: item)
            opened = tab.id
            // Insert after the active tab (Obsidian behavior), else append.
            if let activeID = group.activeTabID,
                let idx = group.tabs.firstIndex(where: { $0.id == activeID }) {
                group.tabs.insert(tab, at: idx + 1)
            } else {
                group.tabs.append(tab)
            }
            if activate || group.activeTabID == nil { group.activeTabID = tab.id }
        }
        if activate { activeGroupID = targetID }
        return opened
    }

    /// Replace the active tab's item in place (single-click open into the
    /// current tab). Falls back to `openTab` when the group is empty. The
    /// dedup rule still applies: if the item is already open in the group,
    /// that tab is selected and the previously-active tab is left untouched.
    mutating func replaceActiveTabItem(_ item: EditorItem) {
        let groupID = activeGroupID
        var handled = false
        updateGroup(groupID) { group in
            if let existing = group.tabs.first(where: { $0.item == item }) {
                group.activeTabID = existing.id
                handled = true
                return
            }
            guard let activeID = group.activeTabID,
                let idx = group.tabs.firstIndex(where: { $0.id == activeID })
            else { return }
            group.tabs[idx].item = item
            handled = true
        }
        if !handled { openTab(item, in: groupID) }
    }

    /// Rewrite every tab whose item is `.markdown(from)` to `.markdown(to)` —
    /// the model half of `WorkspaceState.retarget(old:new:)` (U2-5, #463). A
    /// file's on-disk path changed (rename / move / an ancestor folder moved);
    /// every tab pointing at it must follow, preserving tab identity (TabID)
    /// and each group's active-tab pointer. Tab ORDER and focus are untouched —
    /// this is a pure path substitution, not an open/close.
    ///
    /// Returns the ids of the tabs whose item changed, so the caller can rebind
    /// the matching parked `NoteDocument`s (their `path` is a `let`).
    ///
    /// Dedup is deliberately NOT applied: retarget can transiently produce two
    /// tabs in one group with the same item only if `from` and `to` collide,
    /// which the backend's collision check (`DestinationExists`) already
    /// forbids — so the post-condition stays within the model's invariants
    /// (I1–I7 don't require intra-group item uniqueness; `openTab`'s dedup is a
    /// UX convenience, not an invariant).
    @discardableResult
    mutating func retargetItem(from: EditorItem, to: EditorItem) -> [TabID] {
        guard from != to else { return [] }
        var changed: [TabID] = []
        root = Self.mapAllGroups(root) { group in
            for idx in group.tabs.indices where group.tabs[idx].item == from {
                group.tabs[idx].item = to
                changed.append(group.tabs[idx].id)
            }
        }
        return changed
    }

    /// Transform every file-backed tab in one tree traversal. The closure is
    /// evaluated once per markdown/canvas/Base tab; identities, order, active
    /// pointers, group geometry, and non-file tabs are preserved.
    @discardableResult
    mutating func retargetFileBackedItems(
        _ transform: (EditorItem) -> EditorItem?
    ) -> [FileTabRetarget] {
        var changed: [FileTabRetarget] = []
        root = Self.mapAllGroups(root) { group in
            for index in group.tabs.indices {
                let oldItem = group.tabs[index].item
                switch oldItem {
                case .markdown, .canvas, .base:
                    break
                case .savedQuery, .dashboard, .graph:
                    continue
                }
                guard let newItem = transform(oldItem),
                    !Self.fileBackedItemsMatchExactly(oldItem, newItem)
                else { continue }
                switch newItem {
                case .markdown, .canvas, .base:
                    break
                case .savedQuery, .dashboard, .graph:
                    continue
                }
                group.tabs[index].item = newItem
                changed.append(
                    FileTabRetarget(
                        tabID: group.tabs[index].id,
                        oldItem: oldItem,
                        newItem: newItem,
                        oldPath: oldItem.path,
                        newPath: newItem.path))
            }
        }
        return changed
    }

    private static func fileBackedItemsMatchExactly(
        _ lhs: EditorItem, _ rhs: EditorItem
    ) -> Bool {
        switch (lhs, rhs) {
        case (.markdown(let lhsPath), .markdown(let rhsPath)),
            (.canvas(let lhsPath), .canvas(let rhsPath)),
            (.base(let lhsPath), .base(let rhsPath)):
            return BaseExactIdentity.matches(lhsPath, rhsPath)
        default:
            return false
        }
    }

    @discardableResult
    mutating func retargetSavedQuery(id: String, name: String) -> [TabID] {
        var changed: [TabID] = []
        root = Self.mapAllGroups(root) { group in
            for idx in group.tabs.indices {
                guard case .savedQuery(let currentID, let currentName) = group.tabs[idx].item,
                    BaseExactIdentity.matches(currentID, id),
                    !BaseExactIdentity.matches(currentName, name)
                else { continue }
                group.tabs[idx].item = .savedQuery(id: id, name: name)
                changed.append(group.tabs[idx].id)
            }
        }
        return changed
    }

    @discardableResult
    mutating func retargetDashboard(id: String, name: String) -> [TabID] {
        var changed: [TabID] = []
        root = Self.mapAllGroups(root) { group in
            for idx in group.tabs.indices {
                guard case .dashboard(let currentID, let currentName) = group.tabs[idx].item,
                    BaseExactIdentity.matches(currentID, id),
                    !BaseExactIdentity.matches(currentName, name)
                else { continue }
                group.tabs[idx].item = .dashboard(id: id, name: name)
                changed.append(group.tabs[idx].id)
            }
        }
        return changed
    }

    /// Apply `body` to every group in the tree (unlike `updateGroup`, which
    /// targets one id). Used by whole-tree substitutions like `retargetItem`.
    private static func mapAllGroups(
        _ node: SplitNode, _ body: (inout TabGroupNode) -> Void
    ) -> SplitNode {
        switch node {
        case .group(var group):
            body(&group)
            return .group(group)
        case .split(var branch):
            branch.children = branch.children.map { mapAllGroups($0, body) }
            return .split(branch)
        }
    }

    struct CloseOutcome: Equatable {
        /// The tab that holds focus after the close (nil = workspace empty).
        var focusedTab: TabID?
        /// Set when the close emptied a non-root group and it collapsed.
        var collapsedGroup: GroupID?
    }

    /// Close a tab. Focus rule (normative, u1_spec): the tab to the right,
    /// else the left neighbor; if the group empties, collapse it and focus
    /// the nearest sibling group (previous in parent order, else next,
    /// descending into splits by first group). The last group in the tree is
    /// never collapsed — it becomes the empty root (I3).
    @discardableResult
    mutating func closeTab(_ id: TabID) -> CloseOutcome {
        guard let owner = groupContaining(id) else {
            return CloseOutcome(focusedTab: activeGroup.activeTabID, collapsedGroup: nil)
        }
        var outcome = CloseOutcome()
        var emptied = false
        // Capture the collapse-focus successor BEFORE mutating: the previous
        // group in reading order, else the next (the spec's "nearest sibling,
        // previous in parent order, else next, descending by first group" —
        // reading order realizes exactly that rule at any depth).
        let order = groupsInOrder.map(\.id)
        let ownerIndex = order.firstIndex(of: owner.id)
        let successorIfCollapsed: GroupID? = ownerIndex.flatMap { idx in
            if idx > 0 { return order[idx - 1] }
            if idx + 1 < order.count { return order[idx + 1] }
            return nil
        }
        updateGroup(owner.id) { group in
            guard let idx = group.tabs.firstIndex(where: { $0.id == id }) else { return }
            group.tabs.remove(at: idx)
            if group.tabs.isEmpty {
                group.activeTabID = nil
                emptied = true
            } else if group.activeTabID == id {
                let next = min(idx, group.tabs.count - 1)
                group.activeTabID = group.tabs[next].id
            }
        }
        if emptied {
            let collapsed = collapseGroupIfNonRoot(owner.id)
            outcome.collapsedGroup = collapsed ? owner.id : nil
            if collapsed, activeGroupID == owner.id {
                activeGroupID = successorIfCollapsed ?? Self.firstGroup(in: root).id
            }
        }
        // Defensive: if focus somehow points at a vanished group, repair it.
        if group(activeGroupID) == nil {
            activeGroupID = Self.firstGroup(in: root).id
        }
        outcome.focusedTab = activeGroup.activeTabID
        return outcome
    }

    mutating func selectTab(_ id: TabID) {
        guard let owner = groupContaining(id) else { return }
        updateGroup(owner.id) { $0.activeTabID = id }
        activeGroupID = owner.id
    }

    /// ⌘1…⌘9: 1-based ordinal within the active group; 9 always selects the
    /// last tab (macOS convention).
    mutating func selectTab(ordinal: Int) {
        updateGroup(activeGroupID) { group in
            guard !group.tabs.isEmpty else { return }
            let index: Int
            if ordinal >= 9 {
                index = group.tabs.count - 1
            } else {
                index = ordinal - 1
            }
            guard group.tabs.indices.contains(index) else { return }
            group.activeTabID = group.tabs[index].id
        }
    }

    mutating func selectNextTab() { cycleTab(+1) }
    mutating func selectPreviousTab() { cycleTab(-1) }

    private mutating func cycleTab(_ delta: Int) {
        updateGroup(activeGroupID) { group in
            guard group.tabs.count > 1,
                let activeID = group.activeTabID,
                let idx = group.tabs.firstIndex(where: { $0.id == activeID })
            else { return }
            let count = group.tabs.count
            let next = ((idx + delta) % count + count) % count
            group.activeTabID = group.tabs[next].id
        }
    }

    // MARK: Reorder / move

    /// Reorder within the owning group. `toIndex` is clamped.
    mutating func moveTab(_ id: TabID, toIndex: Int) {
        guard let owner = groupContaining(id) else { return }
        updateGroup(owner.id) { group in
            guard let from = group.tabs.firstIndex(where: { $0.id == id }) else { return }
            let tab = group.tabs.remove(at: from)
            let clamped = max(0, min(toIndex, group.tabs.count))
            group.tabs.insert(tab, at: clamped)
        }
    }

    /// Move a tab to another group (drag between panes / palette command).
    /// The source group collapses if it empties; the moved tab becomes the
    /// destination's active tab.
    mutating func moveTab(_ id: TabID, toGroup destination: GroupID, index: Int? = nil) {
        guard let source = groupContaining(id),
            group(destination) != nil,
            source.id != destination,
            let tab = tab(id)
        else { return }
        var sourceEmptied = false
        updateGroup(source.id) { group in
            group.tabs.removeAll { $0.id == id }
            if group.tabs.isEmpty {
                group.activeTabID = nil
                sourceEmptied = true
            } else if group.activeTabID == id {
                group.activeTabID = group.tabs.first?.id
            }
        }
        updateGroup(destination) { group in
            let at = max(0, min(index ?? group.tabs.count, group.tabs.count))
            group.tabs.insert(tab, at: at)
            group.activeTabID = id
        }
        if sourceEmptied {
            _ = collapseGroupIfNonRoot(source.id)
        }
        if group(activeGroupID) == nil { activeGroupID = destination }
    }

    // MARK: Split / collapse

    /// Split `groupID` along `axis`, creating a new group after it. The new
    /// group receives the active tab (moved when `moveActiveTab`, else a
    /// duplicate of its item — Obsidian's split-keeps-both behavior).
    ///
    /// Rejected (returns nil, model unchanged): unknown group, empty group
    /// (nothing to show in the new pane), `moveActiveTab` on a single-tab
    /// group (it would just relocate the pane and orphan the source), and
    /// any split once the workspace holds `maxGroups` panes.
    @discardableResult
    mutating func split(
        _ groupID: GroupID, axis: SplitBranch.Axis, moveActiveTab: Bool = false
    ) -> GroupID? {
        guard let source = group(groupID), let activeTab = source.activeTab else { return nil }
        if moveActiveTab && source.tabs.count == 1 { return nil }
        if groupsInOrder.count >= Self.maxGroups { return nil }

        let newTab = moveActiveTab
            ? activeTab
            : WorkspaceTab(id: TabID(), item: activeTab.item)
        let newGroup = TabGroupNode(id: GroupID(), tabs: [newTab], activeTabID: newTab.id)

        if moveActiveTab {
            updateGroup(groupID) { group in
                group.tabs.removeAll { $0.id == activeTab.id }
                // Non-empty by the single-tab rejection above.
                if group.activeTabID == activeTab.id {
                    group.activeTabID = group.tabs.first?.id
                }
            }
        }

        root = Self.insertSibling(
            into: root, after: groupID, newNode: .group(newGroup), axis: axis)
        activeGroupID = newGroup.id
        return newGroup.id
    }

    // MARK: Focus

    mutating func focusGroup(_ id: GroupID) {
        guard group(id) != nil else { return }
        activeGroupID = id
    }

    enum Direction { case left, right, up, down }

    /// Spatial focus move (⌘⌥arrows). Geometry-based, not tree-order-based:
    /// each group's normalized rect is computed from the split weights (root
    /// = unit square), and the neighbor is the nearest group in `direction`
    /// whose perpendicular span overlaps the origin's. Ties: larger overlap,
    /// then top/left-most. Returns the focused group, or nil at an edge
    /// (focus unchanged — never lost).
    @discardableResult
    mutating func focusNeighbor(_ direction: Direction) -> GroupID? {
        let rects = groupRects()
        guard let origin = rects[activeGroupID] else { return nil }

        var best: (id: GroupID, distance: Double, overlap: Double, cross: Double)?
        for (id, rect) in rects where id != activeGroupID {
            let distance: Double
            let overlap: Double
            let cross: Double
            switch direction {
            case .left:
                distance = origin.minX - rect.maxX
                overlap = Self.overlap(origin.minY..<origin.maxY, rect.minY..<rect.maxY)
                cross = rect.minY
            case .right:
                distance = rect.minX - origin.maxX
                overlap = Self.overlap(origin.minY..<origin.maxY, rect.minY..<rect.maxY)
                cross = rect.minY
            case .up:
                distance = origin.minY - rect.maxY
                overlap = Self.overlap(origin.minX..<origin.maxX, rect.minX..<rect.maxX)
                cross = rect.minX
            case .down:
                distance = rect.minY - origin.maxY
                overlap = Self.overlap(origin.minX..<origin.maxX, rect.minX..<rect.maxX)
                cross = rect.minX
            }
            // Candidate must lie strictly in the direction (allow abutting
            // edges: distance ≥ -epsilon accounts for shared dividers) and
            // overlap our perpendicular span.
            guard distance >= -1e-9, overlap > 1e-9 else { continue }
            if let current = best {
                if distance < current.distance - 1e-9
                    || (abs(distance - current.distance) <= 1e-9
                        && (overlap > current.overlap + 1e-9
                            || (abs(overlap - current.overlap) <= 1e-9
                                && cross < current.cross))) {
                    best = (id, distance, overlap, cross)
                }
            } else {
                best = (id, distance, overlap, cross)
            }
        }
        guard let target = best?.id else { return nil }
        activeGroupID = target
        return target
    }

    // MARK: Resize

    /// Keyboard resize (⌘⌥+ / ⌘⌥-): grow or shrink the focused group's
    /// weight in its nearest split by `resizeStep`, redistributing the delta
    /// proportionally across siblings and clamping everyone to `minWeight`.
    /// No-op when the group is the root (nothing to resize against).
    mutating func setWeight(delta: Double, for groupID: GroupID) {
        root = Self.adjustWeight(in: root, around: groupID, delta: delta)
    }

    /// Drag resize: set the explicit weight split between children `index`
    /// and `index+1` of the split containing `groupID`'s nearest branch.
    /// Exposed for the divider drag gesture (U1-3); routes through the same
    /// clamping as keyboard resize.
    mutating func setWeights(_ weights: [Double], forSplitContaining groupID: GroupID) {
        root = Self.replaceWeights(in: root, around: groupID, weights: weights)
    }

    // MARK: Geometry (shared by focusNeighbor and the U1-3 renderer)

    struct NormalizedRect: Equatable {
        var minX: Double, minY: Double, maxX: Double, maxY: Double
        var width: Double { maxX - minX }
        var height: Double { maxY - minY }
    }

    /// Every group's rect in the unit square, per the split weights. This is
    /// the single source of truth the view's layout mirrors, which is what
    /// makes spatial focus censusable without rendering.
    func groupRects() -> [GroupID: NormalizedRect] {
        var out: [GroupID: NormalizedRect] = [:]
        Self.accumulateRects(
            root, into: &out,
            rect: NormalizedRect(minX: 0, minY: 0, maxX: 1, maxY: 1))
        return out
    }

    // MARK: Validation (the census contract)

    /// Returns human-readable violations; empty means valid. Census tests
    /// call this after every operation.
    func validate() -> [String] {
        var violations: [String] = []
        let groups = groupsInOrder

        // I1
        if !groups.contains(where: { $0.id == activeGroupID }) {
            violations.append("I1: activeGroupID not present in tree")
        }
        // I2
        for group in groups {
            if let active = group.activeTabID {
                if !group.tabs.contains(where: { $0.id == active }) {
                    violations.append("I2: group \(group.id.raw) activeTab not a member")
                }
            } else if !group.tabs.isEmpty {
                violations.append("I2: group \(group.id.raw) has tabs but nil activeTab")
            }
        }
        // I3
        let emptyGroups = groups.filter { $0.tabs.isEmpty }
        if !emptyGroups.isEmpty {
            let rootIsSoleEmptyGroup: Bool
            if case .group(let g) = root, groups.count == 1, g.tabs.isEmpty {
                rootIsSoleEmptyGroup = true
            } else {
                rootIsSoleEmptyGroup = false
            }
            if !rootIsSoleEmptyGroup {
                violations.append("I3: empty group(s) in a non-trivial tree")
            }
        }
        // I4 + I5
        Self.walkSplits(root) { branch in
            if branch.children.count < 2 {
                violations.append("I4: split with \(branch.children.count) child(ren)")
            }
            for child in branch.children {
                if case .split(let inner) = child, inner.axis == branch.axis {
                    violations.append("I4: same-axis nested split not flattened")
                }
            }
            if branch.weights.count != branch.children.count {
                violations.append("I5: weights/children count mismatch")
            } else {
                if branch.weights.contains(where: { $0 < Self.minWeight - 1e-9 }) {
                    violations.append("I5: weight below minimum")
                }
                let sum = branch.weights.reduce(0, +)
                if abs(sum - 1) > 1e-6 {
                    violations.append("I5: weights sum \(sum) ≠ 1")
                }
            }
        }
        // I6
        let tabIDs = groups.flatMap { $0.tabs.map(\.id) }
        if Set(tabIDs).count != tabIDs.count {
            violations.append("I6: duplicate TabID")
        }
        let groupIDs = groups.map(\.id)
        if Set(groupIDs).count != groupIDs.count {
            violations.append("I6: duplicate GroupID")
        }
        // I7
        if !tabIDs.isEmpty {
            let active = groups.first { $0.id == activeGroupID }
            if active?.activeTabID == nil {
                violations.append("I7: tabs exist but active group has no active tab")
            }
        }
        return violations
    }

    // MARK: - Private tree surgery

    private mutating func updateGroup(
        _ id: GroupID, _ body: (inout TabGroupNode) -> Void
    ) {
        root = Self.mapGroup(root, id: id, body)
    }

    private static func mapGroup(
        _ node: SplitNode, id: GroupID, _ body: (inout TabGroupNode) -> Void
    ) -> SplitNode {
        switch node {
        case .group(var group):
            guard group.id == id else { return node }
            body(&group)
            return .group(group)
        case .split(var branch):
            branch.children = branch.children.map { mapGroup($0, id: id, body) }
            return .split(branch)
        }
    }

    /// Remove an (empty) non-root group from the tree, renormalizing sibling
    /// weights and flattening single-child splits. Returns false when the
    /// group is the root (kept: I3's empty-workspace state).
    private mutating func collapseGroupIfNonRoot(_ id: GroupID) -> Bool {
        if case .group(let g) = root, g.id == id { return false }
        root = Self.normalize(Self.removeGroup(root, id: id) ?? root)
        return true
    }

    private static func removeGroup(_ node: SplitNode, id: GroupID) -> SplitNode? {
        switch node {
        case .group(let group):
            return group.id == id ? nil : node
        case .split(var branch):
            var kept: [SplitNode] = []
            var keptWeights: [Double] = []
            for (child, weight) in zip(branch.children, branch.weights) {
                if let survivor = removeGroup(child, id: id) {
                    kept.append(survivor)
                    keptWeights.append(weight)
                }
            }
            if kept.isEmpty { return nil }
            if kept.count == 1 { return kept[0] }
            let total = keptWeights.reduce(0, +)
            branch.children = kept
            branch.weights = total > 0
                ? keptWeights.map { $0 / total }
                : Array(repeating: 1.0 / Double(kept.count), count: kept.count)
            return .split(branch)
        }
    }

    /// Insert `newNode` as the sibling immediately after `after` along
    /// `axis`. If the parent split already runs along `axis`, the node joins
    /// it (n-ary, I4 flattening); otherwise the target group is wrapped in a
    /// new two-child split. New sibling takes half the target's weight.
    private static func insertSibling(
        into node: SplitNode, after target: GroupID, newNode: SplitNode,
        axis: SplitBranch.Axis
    ) -> SplitNode {
        switch node {
        case .group(let group):
            guard group.id == target else { return node }
            return .split(
                SplitBranch(axis: axis, children: [node, newNode], weights: [0.5, 0.5]))
        case .split(var branch):
            if branch.axis == axis,
                let idx = branch.children.firstIndex(where: {
                    if case .group(let g) = $0 { return g.id == target }
                    return false
                }) {
                let half = branch.weights[idx] / 2
                branch.weights[idx] = half
                branch.children.insert(newNode, at: idx + 1)
                branch.weights.insert(half, at: idx + 1)
                branch = clampWeights(branch)
                return .split(branch)
            }
            branch.children = branch.children.map {
                insertSibling(into: $0, after: target, newNode: newNode, axis: axis)
            }
            // A wrapped child may now be a same-axis split — flatten.
            return normalize(.split(branch))
        }
    }

    /// Flatten same-axis nesting and dissolve single-child splits.
    private static func normalize(_ node: SplitNode) -> SplitNode {
        switch node {
        case .group:
            return node
        case .split(var branch):
            var children: [SplitNode] = []
            var weights: [Double] = []
            for (child, weight) in zip(branch.children, branch.weights) {
                let normalized = normalize(child)
                if case .split(let inner) = normalized, inner.axis == branch.axis {
                    for (grandchild, innerWeight) in zip(inner.children, inner.weights) {
                        children.append(grandchild)
                        weights.append(weight * innerWeight)
                    }
                } else {
                    children.append(normalized)
                    weights.append(weight)
                }
            }
            if children.count == 1 { return children[0] }
            branch.children = children
            let total = weights.reduce(0, +)
            branch.weights = total > 0
                ? weights.map { $0 / total }
                : Array(repeating: 1.0 / Double(children.count), count: children.count)
            branch = clampWeights(branch)
            return .split(branch)
        }
    }

    /// Clamp every weight to `minWeight` and renormalize. With n children the
    /// floor is only satisfiable when n·minWeight ≤ 1; beyond that (7+ panes
    /// at 0.15) weights fall back to equal shares — the pane count itself is
    /// bounded by usability, not the model.
    private static func clampWeights(_ branch: SplitBranch) -> SplitBranch {
        var branch = branch
        let n = branch.weights.count
        guard n > 0 else { return branch }
        if Double(n) * minWeight > 1 {
            branch.weights = Array(repeating: 1.0 / Double(n), count: n)
            return branch
        }
        // Waterfilling clamp: weights at/below the floor are pinned to it
        // PERMANENTLY (the pinned set only grows), and the remaining mass is
        // redistributed over the unpinned weights proportionally. Rescaling
        // can push further weights under the floor, so iterate; monotone
        // pinning guarantees convergence in ≤ n passes. (A non-sticky version
        // oscillates: the U1-1 census found exactly that on
        // splitH → splitH → splitH.)
        var weights = branch.weights
        let sum = weights.reduce(0, +)
        if sum > 0 { weights = weights.map { $0 / sum } }
        var pinned = Set<Int>()
        while true {
            let newlyBelow = weights.indices.filter {
                !pinned.contains($0) && weights[$0] < minWeight - 1e-12
            }
            if newlyBelow.isEmpty { break }
            pinned.formUnion(newlyBelow)
            if pinned.count == n { break }
            let free = weights.indices.filter { !pinned.contains($0) }
            let freeTarget = 1 - Double(pinned.count) * minWeight
            let freeTotal = free.reduce(0.0) { $0 + weights[$1] }
            for idx in pinned { weights[idx] = minWeight }
            if freeTotal > 0 {
                for idx in free { weights[idx] *= freeTarget / freeTotal }
            } else {
                for idx in free { weights[idx] = freeTarget / Double(free.count) }
            }
        }
        if pinned.count == n {
            // Only reachable when n·minWeight == 1 (satisfiability checked
            // above); equal shares are exactly the floor.
            weights = Array(repeating: 1.0 / Double(n), count: n)
        }
        branch.weights = weights
        return branch
    }

    private static func adjustWeight(
        in node: SplitNode, around target: GroupID, delta: Double
    ) -> SplitNode {
        switch node {
        case .group:
            return node
        case .split(var branch):
            if let idx = branch.children.firstIndex(where: { contains($0, group: target) }) {
                // The nearest enclosing split: adjust here (a group nested in
                // a cross-axis split resizes in its own split first).
                if case .group(let g) = branch.children[idx], g.id == target {
                    var weights = branch.weights
                    let others = weights.indices.filter { $0 != idx }
                    let d = min(
                        max(delta, minWeight - weights[idx]),
                        others.reduce(0.0) { $0 + weights[$1] }
                            - Double(others.count) * minWeight)
                    weights[idx] += d
                    let othersTotal = others.reduce(0.0) { $0 + weights[$1] }
                    if othersTotal > 0 {
                        for i in others {
                            weights[i] -= d * (weights[i] / othersTotal)
                        }
                    }
                    branch.weights = weights
                    branch = clampWeights(branch)
                    return .split(branch)
                }
                branch.children[idx] = adjustWeight(
                    in: branch.children[idx], around: target, delta: delta)
                return .split(branch)
            }
            return node
        }
    }

    private static func replaceWeights(
        in node: SplitNode, around target: GroupID, weights: [Double]
    ) -> SplitNode {
        switch node {
        case .group:
            return node
        case .split(var branch):
            let directChild = branch.children.contains { child in
                if case .group(let g) = child { return g.id == target }
                return false
            }
            if directChild {
                if weights.count == branch.weights.count {
                    branch.weights = weights
                    branch = clampWeights(branch)
                }
                return .split(branch)
            }
            branch.children = branch.children.map {
                replaceWeights(in: $0, around: target, weights: weights)
            }
            return .split(branch)
        }
    }

    private static func contains(_ node: SplitNode, group id: GroupID) -> Bool {
        switch node {
        case .group(let g): return g.id == id
        case .split(let branch): return branch.children.contains { contains($0, group: id) }
        }
    }

    private static func collectGroups(_ node: SplitNode) -> [TabGroupNode] {
        switch node {
        case .group(let g): return [g]
        case .split(let branch): return branch.children.flatMap(collectGroups)
        }
    }

    private static func firstGroup(in node: SplitNode) -> TabGroupNode {
        switch node {
        case .group(let g): return g
        case .split(let branch): return firstGroup(in: branch.children[0])
        }
    }

    private static func walkSplits(_ node: SplitNode, _ body: (SplitBranch) -> Void) {
        if case .split(let branch) = node {
            body(branch)
            branch.children.forEach { walkSplits($0, body) }
        }
    }

    private static func accumulateRects(
        _ node: SplitNode, into out: inout [GroupID: NormalizedRect],
        rect: NormalizedRect
    ) {
        switch node {
        case .group(let g):
            out[g.id] = rect
        case .split(let branch):
            var offset = 0.0
            for (child, weight) in zip(branch.children, branch.weights) {
                let childRect: NormalizedRect
                switch branch.axis {
                case .horizontal:
                    childRect = NormalizedRect(
                        minX: rect.minX + offset * rect.width,
                        minY: rect.minY,
                        maxX: rect.minX + (offset + weight) * rect.width,
                        maxY: rect.maxY)
                case .vertical:
                    childRect = NormalizedRect(
                        minX: rect.minX,
                        minY: rect.minY + offset * rect.height,
                        maxX: rect.maxX,
                        maxY: rect.minY + (offset + weight) * rect.height)
                }
                accumulateRects(child, into: &out, rect: childRect)
                offset += weight
            }
        }
    }

    private static func overlap(_ a: Range<Double>, _ b: Range<Double>) -> Double {
        max(0, min(a.upperBound, b.upperBound) - max(a.lowerBound, b.lowerBound))
    }
}
