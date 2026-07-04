// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// A node in the file tree: either a directory or a Markdown file.
///
/// Directories are keyed by `dirs.id` (the stable, rescan-surviving id U2-1
/// exposes on `DirNodeSummary`). Files are keyed by their **vault-relative
/// path**: the U2-1 `DirListing.files` element (`FileSummary`) carries no id
/// field, and a file's path is itself stable + unique within the vault
/// (U2-1 upserts by path), so it serves the same "stable node id, dir and file
/// never collide" role the spec's `.file(i64)` intended. Keying files by path
/// also matches the existing open seam (`AppState.selectedFilePath` is a path).
/// See the deviation note in the U2-4 PR.
enum NodeID: Hashable {
    case dir(Int64)
    case file(path: String)
}

/// One visible entry in the tree. Directories carry their immediate child
/// counts (so a *collapsed* folder can announce "N items" without a fetch)
/// and their depth (0-based from root); files carry the path the open flow
/// consumes. `depth` drives indentation and the "level N" AX suffix.
struct TreeNode: Identifiable, Equatable {
    let nodeID: NodeID
    /// Vault-relative path (forward slashes, no trailing `/`). For a file this
    /// is what `AppState.selectedFilePath` is set to on selection.
    let path: String
    /// Final path component — the row's visible label.
    let name: String
    /// 0 at the root level, +1 per nesting level. 1-based ("level N") in AX.
    let depth: Int
    let kind: Kind

    enum Kind: Equatable {
        /// A directory; `childDirCount`/`childFileCount` are its immediate
        /// (non-recursive) child counts, straight from `DirNodeSummary`.
        case directory(childDirCount: Int, childFileCount: Int)
        /// A file; `mtimeMs` rides along from the tree API's `FileSummary`
        /// so the row never scans `AppState.files` (O(n) per row × visible
        /// rows was a measurable render cost on 10k vaults).
        case file(mtimeMs: Int64)
    }

    var id: NodeID { nodeID }

    var isDirectory: Bool {
        if case .directory = kind { return true }
        return false
    }

    /// Total immediate children (dirs + files) — the "N items" count a folder
    /// row announces. Directories only; 0 for files.
    var itemCount: Int {
        if case let .directory(dirs, files) = kind { return dirs + files }
        return 0
    }
}

/// Per-vault model backing `FileTreeSidebar`: a lazily-materialized directory
/// tree over the U2-1 `list_dir_children` API.
///
/// **Laziness is the 10k budget.** We only ever fetch (and only ever hold in
/// `children`) the levels the user has expanded. The root level loads once on
/// vault open; each folder's children load on its *first* expand and are then
/// cached. Collapsing keeps the cached level (re-expanding is instant) unless
/// it was invalidated — see `treeInvalidation`. `visibleRows` flattens only the
/// expanded subtree into the row array the `List` renders, so a 10k-file vault
/// with a collapsed root is a single-level render.
///
/// **Fetch state per level.** A folder mid-fetch is `.loading` (the view shows
/// an inline spinner row); a failed fetch is `.failed(message)` (an inline
/// error row with Retry). Successful levels live in `children`.
@MainActor
final class FileTreeViewModel: ObservableObject {
    /// The root level (`list_dir_children("")`). Empty until the first load.
    @Published private(set) var rootLevel: [TreeNode] = []
    /// Cached children per expanded directory. A key present here means that
    /// level has been fetched at least once; absent means "never fetched".
    @Published private(set) var children: [NodeID: [TreeNode]] = [:]
    /// Which directories are currently expanded (disclosed).
    @Published private(set) var expanded: Set<NodeID> = []
    /// Per-directory fetch state for levels currently being fetched or that
    /// failed. Absent ⇒ either not fetching or already cached in `children`.
    /// The root level uses the `.dir(-1)` sentinel (no real dir has id -1).
    @Published private(set) var fetchState: [NodeID: FetchState] = [:]

    /// The session the tree reads from. Swapped by `bind(to:)` when the vault
    /// changes; nil clears the tree.
    private var session: VaultSession?

    /// Level fetcher: `parentPath` → one level's listing. Production routes to
    /// `session.listDirChildren`; tests inject a spy to assert *which* levels
    /// are fetched (the lazy-fetch guarantee) and to stand up large synthetic
    /// fixtures without an FFI round-trip. `nil` until `bind(to:)`.
    private var fetcher: ((String) throws -> DirListing)?

    /// Test seam: bind to an explicit fetcher instead of a live session. The
    /// tree behaves identically (root loads immediately); only the source of
    /// `DirListing`s differs. `session` stays nil, so no FFI is touched.
    func bindForTesting(fetcher: @escaping (String) throws -> DirListing) {
        self.session = nil
        self.fetcher = fetcher
        rootLevel = []
        children = [:]
        expanded = []
        fetchState = [:]
        loadRoot()
    }

    /// Sentinel node id standing in for the root level in `fetchState` (the
    /// root has no `DirNodeSummary`, so no real id). No dir row ever has id -1,
    /// so this can't alias a real level. `nonisolated` (it's an immutable
    /// constant) so the view's row-id builder can reference it off the main
    /// actor — Swift 6 forbids touching actor-isolated statics from a
    /// nonisolated autoclosure.
    nonisolated static let rootFetchKey = NodeID.dir(-1)

    /// Page size for a level fetch. Directory levels are small by construction
    /// (a folder rarely holds thousands of *immediate* children); this bound is
    /// generous and the API sorts + pages for us. The tree does not paginate
    /// within a level in U2-4 — a `next_cursor` beyond this bound is a recorded
    /// follow-up, not a correctness gap for realistic vaults.
    static let levelPageLimit: UInt32 = 5000

    enum FetchState: Equatable {
        case loading
        /// Fetch failed; `message` is a specific, user-facing reason.
        case failed(message: String)
    }

    // MARK: - Vault binding

    /// Point the tree at a session (or nil to clear). Resets all cached state
    /// and loads the root level. `FileTreeSidebar` calls this only on vault
    /// change, so re-entrancy on the same session isn't a concern here.
    func bind(to session: VaultSession?) {
        self.session = session
        rootLevel = []
        children = [:]
        expanded = []
        fetchState = [:]
        guard let session else {
            fetcher = nil
            return
        }
        // Route level fetches through the session. `Paging` with a nil cursor
        // and a generous limit takes the whole level in one call (see
        // `levelPageLimit`).
        fetcher = { parentPath in
            try session.listDirChildren(
                parentPath: parentPath,
                paging: Paging(cursor: nil, limit: Self.levelPageLimit))
        }
        loadRoot()
    }

    // MARK: - Level fetching

    /// (Re)fetch the root level. Sets `.loading` on the root sentinel while in
    /// flight so the view can show a spinner if the root is slow.
    func loadRoot() {
        guard let fetcher else { return }
        fetchState[Self.rootFetchKey] = .loading
        do {
            let listing = try fetcher("")
            rootLevel = Self.nodes(from: listing, depth: 0)
            fetchState[Self.rootFetchKey] = nil
        } catch {
            rootLevel = []
            fetchState[Self.rootFetchKey] = .failed(message: Self.message(for: error))
        }
    }

    /// Fetch a directory's children into the cache. No-op if already cached
    /// (call `treeInvalidation` first to force a refetch). Sets `.loading`
    /// while in flight and clears it (or records `.failed`) on completion.
    func loadChildren(of node: TreeNode) {
        guard case .directory = node.kind else { return }
        guard let fetcher else { return }
        if children[node.nodeID] != nil { return }  // already cached
        fetchState[node.nodeID] = .loading
        do {
            let listing = try fetcher(node.path)
            children[node.nodeID] = Self.nodes(from: listing, depth: node.depth + 1)
            fetchState[node.nodeID] = nil
        } catch {
            fetchState[node.nodeID] = .failed(message: Self.message(for: error))
        }
    }

    // MARK: - Expand / collapse

    /// Expand a directory: mark it disclosed and fetch its children if this is
    /// the first expand (or it was invalidated). Cached levels re-expand
    /// without a fetch.
    func expand(_ node: TreeNode) {
        guard case .directory = node.kind else { return }
        expanded.insert(node.nodeID)
        loadChildren(of: node)  // no-op when cached
    }

    /// Collapse a directory. The cached level is retained (re-expand is
    /// instant); `treeInvalidation` is what drops it.
    func collapse(_ node: TreeNode) {
        guard case .directory = node.kind else { return }
        expanded.remove(node.nodeID)
    }

    /// Toggle disclosure for a directory row (Space/Return, disclosure action).
    func toggle(_ node: TreeNode) {
        guard case .directory = node.kind else { return }
        if expanded.contains(node.nodeID) {
            collapse(node)
        } else {
            expand(node)
        }
    }

    // MARK: - Invalidation seam (consumed by U2-5 + rescans)

    /// Drop a level's cached children and refetch it if it's currently
    /// expanded; otherwise leave it dropped so the *next* expand refetches.
    ///
    /// This is the refresh seam the rest of the milestone drives:
    ///   - **Rescans** (this PR): the sidebar calls `treeInvalidation(parent:
    ///     nil)` when a scan finishes, since a rescan can add/remove files and
    ///     folders anywhere.
    ///   - **U2-5 mutations** (next PR): `AppState` will call
    ///     `treeInvalidation(parent:)` with the affected parent(s) after every
    ///     create/rename/move/delete command, so only the touched levels
    ///     refetch — the rest of the tree (and its expansion state) is
    ///     untouched.
    ///
    /// `parent == nil` invalidates the root level (and, because a rescan can
    /// change anything, every cached child level — they refetch lazily on next
    /// expand, and expanded ones refetch now).
    func treeInvalidation(parent: NodeID?) {
        guard let parent else {
            // Root (and everything below) may have changed. Refetch root now;
            // drop all child caches so each refetches on its next expand, and
            // refetch the ones that are currently expanded immediately.
            let expandedChildKeys = children.keys.filter { expanded.contains($0) }
            children.removeAll()
            fetchState = fetchState.filter { $0.key == Self.rootFetchKey }
            loadRoot()
            for key in expandedChildKeys {
                if let node = node(for: key) {
                    loadChildren(of: node)
                }
            }
            return
        }
        // A single level changed. Drop its cache; refetch iff it's disclosed.
        children[parent] = nil
        fetchState[parent] = nil
        if expanded.contains(parent), let node = node(for: parent) {
            loadChildren(of: node)
        }
    }

    // MARK: - Flattening

    /// The rows the `List` renders: a pre-order walk of the expanded subtree.
    /// Only expanded levels contribute rows, so a collapsed vault materializes
    /// just the root level regardless of on-disk size (the 10k budget).
    var visibleRows: [TreeNode] {
        var rows: [TreeNode] = []
        rows.reserveCapacity(rootLevel.count)
        appendLevel(rootLevel, into: &rows)
        return rows
    }

    private func appendLevel(_ level: [TreeNode], into rows: inout [TreeNode]) {
        for node in level {
            rows.append(node)
            guard node.isDirectory, expanded.contains(node.nodeID) else { continue }
            if let kids = children[node.nodeID] {
                appendLevel(kids, into: &rows)
            }
        }
    }

    // MARK: - Row lookup

    /// Find the `TreeNode` for a node id across the root + all cached levels.
    /// Used by keyboard/selection handlers that hold an id but need the node.
    func node(for id: NodeID) -> TreeNode? {
        if let hit = rootLevel.first(where: { $0.nodeID == id }) { return hit }
        for level in children.values {
            if let hit = level.first(where: { $0.nodeID == id }) { return hit }
        }
        return nil
    }

    /// The parent node id of a node, if that node lives in a cached child
    /// level. Root-level nodes return nil (their parent is the vault root).
    func parent(of id: NodeID) -> NodeID? {
        for (parentID, level) in children where level.contains(where: { $0.nodeID == id }) {
            return parentID
        }
        return nil
    }

    // MARK: - Post-mutation focus (U2-6, #464)

    /// The `NodeID` of the materialized node at `path` (file OR folder), or nil
    /// if it isn't in the tree (its level isn't fetched/expanded). A file's
    /// NodeID *is* its path, so files always resolve once their level exists;
    /// folders resolve by matching the refetched level's `.dir(id)` rows.
    ///
    /// U2-6 create/rename/move focus: after `treeInvalidation` refetches the
    /// affected level, the new/renamed/moved row exists here.
    func focusTarget(forPath path: String) -> NodeID? {
        let fileID = NodeID.file(path: path)
        if rootLevel.contains(where: { $0.nodeID == fileID }) { return fileID }
        if let dir = rootLevel.first(where: { $0.path == path && $0.isDirectory }) {
            return dir.nodeID
        }
        for level in children.values {
            if level.contains(where: { $0.nodeID == fileID }) { return fileID }
            if let dir = level.first(where: { $0.path == path && $0.isDirectory }) {
                return dir.nodeID
            }
        }
        return nil
    }

    /// The post-DELETE focus target, computed on the CURRENT (pre-invalidation)
    /// tree — the deleted node's next sibling, else previous, else the parent
    /// folder; nil (⇒ no move / stay wherever the list lands, never the window
    /// root) when the deleted node was the only entry at the vault root.
    ///
    /// Pure over the tree's current state (the level still contains the doomed
    /// node) so it's unit-testable and must run BEFORE `treeInvalidation` drops
    /// the level. `parentPath` is "" for a root-level delete.
    func deleteFocusTarget(deletedPath: String, parentPath: String) -> NodeID? {
        // The visible level the deleted node lived in.
        let level: [TreeNode]
        let parentID: NodeID?
        if parentPath.isEmpty {
            level = rootLevel
            parentID = nil
        } else if let pid = dirNodeID(forPath: parentPath), let kids = children[pid] {
            level = kids
            parentID = pid
        } else {
            // Parent level isn't materialized — nothing to compute a sibling
            // against. Fall back to selecting the parent folder if we can find
            // it, else no move.
            return dirNodeID(forPath: parentPath)
        }
        guard let idx = level.firstIndex(where: { $0.path == deletedPath }) else {
            // Already gone from the level (double-fire) — target the parent.
            return parentID
        }
        // Next sibling (the element after the deleted one), else previous, else
        // the parent folder, else nil (only child at the root).
        if idx + 1 < level.count { return level[idx + 1].nodeID }
        if idx - 1 >= 0 { return level[idx - 1].nodeID }
        return parentID
    }

    /// The `.dir(id)` NodeID for a folder at `path` in the CURRENT tree, or nil.
    /// (Files are keyed by path; this is the folder-only lookup the focus
    /// helpers use.)
    func dirNodeID(forPath path: String) -> NodeID? {
        if let dir = rootLevel.first(where: { $0.path == path && $0.isDirectory }) {
            return dir.nodeID
        }
        for level in children.values {
            if let dir = level.first(where: { $0.path == path && $0.isDirectory }) {
                return dir.nodeID
            }
        }
        return nil
    }

    /// Expand the whole ancestor chain of `path` so a moved node is revealed at
    /// its new location (spec §U2-6 "auto-expand the destination ancestor
    /// chain"). Walks the path's directory prefixes shallow→deep, expanding +
    /// fetching each so the next level materializes before the deeper one is
    /// reached. No-op for a root-level path (no ancestors to expand).
    func ensureAncestorsExpanded(forPath path: String) {
        let components = path.split(separator: "/").map(String.init)
        guard components.count > 1 else { return }  // root-level: no ancestors
        var prefix = ""
        // Every component except the last is an ancestor DIRECTORY.
        for component in components.dropLast() {
            prefix = prefix.isEmpty ? component : "\(prefix)/\(component)"
            guard let node = dirNode(forPath: prefix) else {
                // The ancestor level isn't materialized yet — expanding its
                // parent (done on the previous iteration) fetched it, so a
                // retry after that fetch would find it. In the synchronous
                // fetch model the child level is already present; if not, we
                // stop (the node will still be selectable once visible).
                break
            }
            expand(node)
        }
    }

    /// The full `TreeNode` for a folder at `path` in the current tree, or nil.
    private func dirNode(forPath path: String) -> TreeNode? {
        if let dir = rootLevel.first(where: { $0.path == path && $0.isDirectory }) {
            return dir
        }
        for level in children.values {
            if let dir = level.first(where: { $0.path == path && $0.isDirectory }) {
                return dir
            }
        }
        return nil
    }

    // MARK: - Keyboard disclosure decision (pure, unit-tested)

    /// What a →/← key does to the currently-selected node. Pure over the tree's
    /// current state so the mapping is regression-locked without a running
    /// `List`. The view's `.onMoveCommand` handler applies the result.
    enum MoveOutcome: Equatable {
        /// Expand the selected (collapsed) directory.
        case expand(NodeID)
        /// Collapse the selected (expanded) directory.
        case collapse(NodeID)
        /// Move the selection to this node id (first child, or parent).
        case select(NodeID)
        /// Nothing to do (e.g. → on a file, ← at the root).
        case none
    }

    /// Map a horizontal move on `selectedID` to an outcome (spec §U2-4):
    ///   → : collapsed dir ⇒ expand; expanded dir ⇒ select first child; file ⇒ none.
    ///   ← : expanded dir ⇒ collapse; otherwise ⇒ select parent (nil at root).
    func moveOutcome(for selectedID: NodeID, right: Bool) -> MoveOutcome {
        guard let node = node(for: selectedID) else { return .none }
        if right {
            guard node.isDirectory else { return .none }
            if expanded.contains(node.nodeID) {
                if let first = children[node.nodeID]?.first { return .select(first.nodeID) }
                return .none
            }
            return .expand(node.nodeID)
        }
        if node.isDirectory && expanded.contains(node.nodeID) {
            return .collapse(node.nodeID)
        }
        if let parentID = parent(of: node.nodeID) { return .select(parentID) }
        return .none
    }

    // MARK: - Helpers

    /// Build the `TreeNode`s for one level: dirs first (already sorted by the
    /// API), then files, at `depth`.
    static func nodes(from listing: DirListing, depth: Int) -> [TreeNode] {
        var out: [TreeNode] = []
        out.reserveCapacity(listing.dirs.count + listing.files.items.count)
        for dir in listing.dirs {
            out.append(
                TreeNode(
                    nodeID: .dir(dir.id),
                    path: dir.path,
                    name: dir.name,
                    depth: depth,
                    kind: .directory(
                        childDirCount: Int(dir.childDirCount),
                        childFileCount: Int(dir.childFileCount)
                    )
                ))
        }
        for file in listing.files.items {
            out.append(
                TreeNode(
                    nodeID: .file(path: file.path),
                    path: file.path,
                    name: file.name,
                    depth: depth,
                    kind: .file(mtimeMs: file.mtimeMs)
                ))
        }
        return out
    }

    /// A specific, user-facing message for a level-fetch failure. `VaultError`'s
    /// own `errorDescription` is a debug reflection (not user prose), so we
    /// front it with a plain sentence and append the reason where the variant
    /// carries a useful one.
    static func message(for error: Error) -> String {
        if let vaultError = error as? VaultError {
            switch vaultError {
            case let .Io(message), let .Db(message):
                return "Couldn't load this folder: \(message)"
            case let .InvalidPath(path, reason):
                return "Couldn't load \(path): \(reason)"
            case .Cancelled:
                return "Loading this folder was cancelled."
            default:
                return "Couldn't load this folder."
            }
        }
        return "Couldn't load this folder."
    }
}

/// Sidebar showing the open vault as a collapsible folder tree (Milestone U2-4,
/// #462), replacing the earlier flat file list.
///
/// SwiftUI's `List` is lazy under the hood (NSCollectionView on macOS), and we
/// feed it only the *visible* rows — the flattened expanded subtree — so a
/// vault with 10k+ files but a collapsed root renders one level. Selection is
/// single-row; a *file* row's selection drives `AppState.selectedFilePath`
/// (which the note-load cascade watches), preserving the flat list's open
/// semantics exactly. Folder rows disclose/collapse instead of opening.
///
/// The scan progress strip and the #418 selection-announcement discipline are
/// carried over unchanged from the flat list.
///
/// The per-note panel stack that once lived below the tree is GONE (U4-2,
/// #471): its seven panels (Outgoing links, Backlinks, Embeds, Math, Code,
/// Diagrams, Tasks) moved into the right-pane leaf rail. `PropertiesPanel`
/// alone remains here, in a TEMPORARY bottom `DisclosureGroup` with its
/// bindings unchanged — U3-3 relocates it to the in-note properties widget and
/// deletes this section. The header coordination note in u4_spec §U4-2 keeps
/// both milestones independently mergeable: whichever lands first, Properties
/// has a home and the stack is retired exactly once.
struct FileTreeSidebar: View {
    @EnvironmentObject private var appState: AppState
    /// The tree model. `@StateObject` so it survives view-body churn; rebound
    /// to the live session on vault change.
    @StateObject private var tree = FileTreeViewModel()
    @State private var didAnnounceCount = false
    /// Local mirror of the selected row that the `List`'s selection binds to —
    /// a `RowID` (which wraps a real `NodeID`), not a path, because rows are now
    /// dirs *and* files (plus synthetic loading/error placeholders). The list
    /// must NOT bind `selectedFilePath` directly: doing so writes that
    /// `@Published` from inside SwiftUI's update transaction, and its willSet
    /// cascade (handleSelectionChange → ~15 `@Published` writes) then trips
    /// "Publishing changes from within view updates … undefined behavior",
    /// which made the note load only ~50/50. We assign `selectedFilePath` from
    /// `.onChange` instead (a post-update, safe mutation point) and mirror
    /// programmatic changes back here to keep the highlight in sync. (#448
    /// discipline — the mechanism is identical; only the selection *key*
    /// generalized from a path string to a `RowID`.)
    @State private var listSelection: RowID?
    /// Keyboard focus on the file tree — gates the #418 selection announcements
    /// to list-driven changes only.
    @FocusState private var fileTreeFocused: Bool

    /// Set for exactly one `listSelection` change when post-DELETE focus moves
    /// the highlight to a sibling: the deleted node's tab is now in the missing-
    /// file error state (U2-5), and re-selecting a sibling FILE must NOT open it
    /// (that would replace the error tab). Cleared the moment it's consumed.
    @State private var suppressOpenForPostMutationFocus = false

    /// The `List` row id / selection key. A real tree node (`.node`) or a
    /// per-level loading/error placeholder derived from its parent level id so
    /// it's stable across renders. Only `.node(.file(_))` selections ever drive
    /// `selectedFilePath`.
    enum RowID: Hashable {
        case node(NodeID)
        case loading(parent: NodeID)
        case error(parent: NodeID)
    }

    var body: some View {
        VStack(spacing: 0) {
            // Thin progress strip that mirrors the scanner's FileIndexed
            // events. The `@ViewBuilder` renders EmptyView when there's no
            // scanProgress, which collapses to no rendered output.
            progressBar
            Group {
                if appState.isScanning && appState.files.isEmpty {
                    scanningState
                } else if let error = appState.scanError {
                    errorState(error)
                } else if appState.files.isEmpty {
                    emptyState
                } else {
                    treeList
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)

            // TEMPORARY Properties home (U4-2, #471). The other seven per-note
            // panels moved into the right-pane leaf rail this milestone;
            // `PropertiesPanel` stays here in one `DisclosureGroup` — its own
            // bindings and self-hiding behavior unchanged — until U3-3 relocates
            // it to the in-note properties widget and deletes this section
            // (header coordination note, u4_spec §U4-2). Structure is exactly
            // what the panel stack used (divider + bounded ScrollView); with
            // only Properties inside it now, the section self-hides via
            // `PropertiesPanel`'s own `EmptyView` when no note is selected —
            // unchanged from the stack era, so the tree isn't pushed around in
            // the empty case.
            Divider()
            ScrollView {
                VStack(alignment: .leading, spacing: 0) {
                    PropertiesPanel()
                }
            }
            .frame(maxHeight: 300)

            // U4-3 (#472): the bottom-left utility bar — Settings, Help, and
            // the vault switcher — pinned at the sidebar's bottom edge. Last
            // child of the column, below the temporary Properties section
            // (which U3-3 deletes). The bar draws its own separator above.
            SidebarUtilityBar()
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .navigationTitle("Files")
        // U4-4 (#473): ⌘⌥← off the leftmost editor group routes focus into the
        // file tree (the westernmost terminal region). AppState can't assign
        // this view's `@FocusState` directly, so it bumps `treeFocusRequest`;
        // the `TreeFocusBridge` below OBSERVES `workspace` (this view observes
        // only `appState`, whose publisher doesn't forward the nested
        // `WorkspaceState` @Published — the same reason `RightPaneView` takes
        // `workspace` as an `@ObservedObject`) and mirrors the request into
        // `fileTreeFocused` on `.onChange` (a post-update mutation point, #448).
        .background(
            TreeFocusBridge(workspace: appState.workspace, focused: $fileTreeFocused))
        .onAppear {
            // Bind the tree to whatever session is already open when the
            // sidebar mounts (re-entering a vault view with files loaded).
            tree.bind(to: appState.currentSession)
            listSelection = rowID(forPath: appState.selectedFilePath)
        }
        .onChange(of: appState.currentVaultURL) {
            // Each new vault gets a fresh tree and its own count announcement.
            didAnnounceCount = false
            tree.bind(to: appState.currentSession)
        }
        .onChange(of: appState.isScanning) { _, scanning in
            // Announce once the scan finishes — at that point `files` has been
            // populated and N items is the count VoiceOver should hear.
            if !scanning && !didAnnounceCount && appState.scanError == nil {
                didAnnounceCount = true
                postAccessibilityAnnouncement(
                    "File list, \(appState.files.count) "
                        + (appState.files.count == 1 ? "item" : "items")
                )
            }
            // A finished (re)scan can add/remove files and folders anywhere in
            // the tree — invalidate so the root (and any expanded levels)
            // refetch. This is the "after rescans" arm of the treeInvalidation
            // seam; U2-5 wires the per-mutation arm from AppState.
            if !scanning && appState.currentSession != nil {
                tree.treeInvalidation(parent: nil)
            }
        }
    }

    // MARK: - States

    private var scanningState: some View {
        VStack(spacing: 12) {
            ProgressView()
            Text("Scanning vault…")
                .foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Scanning vault. The file list will appear when the scan finishes.")
    }

    private func errorState(_ message: String) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Could not load files")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)
            Text(message)
                .foregroundStyle(.secondary)
        }
        .padding()
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    private var emptyState: some View {
        VStack(spacing: 8) {
            Text("No Markdown files in this vault.")
                .foregroundStyle(.secondary)
        }
        .padding()
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityLabel("No Markdown files in this vault.")
    }

    // MARK: - Tree list

    private var treeList: some View {
        // Binds to the local `listSelection` (a RowID), not
        // `appState.selectedFilePath` directly — see the `listSelection` doc
        // comment for why (avoids the "publishing within view updates" UB that
        // made note loads flaky). The list highlight updates synchronously as
        // the user arrows/clicks; `selectedFilePath` (and its note-load
        // cascade) is assigned from the `.onChange` below, after the update
        // pass, and only for *file* rows.
        // ScrollViewReader wraps the List so U2-6 can scroll the post-mutation
        // focus target into view (`proxy.scrollTo(rowID)`). U2-5 already re-
        // anchors `listSelection`; the scroll is wired in U2-6.
        ScrollViewReader { proxy in
        List(selection: $listSelection) {
            ForEach(rows) { entry in
                rowView(for: entry)
                    .tag(entry.rowID)
                    .id(entry.rowID)
            }
        }
        .listStyle(.sidebar)
        .focused($fileTreeFocused)
        // U4-4 review: mirror REAL tree focus into the region bookkeeping —
        // Tab/click into the tree must make the next ⌘⌥→ "return to editor"
        // per spec, not an interior editor move. Post-update (#448-safe).
        .onChange(of: fileTreeFocused) { _, focused in
            appState.workspace.noteTreeFocusChanged(focused)
        }
        // Keyboard disclosure: →/← move through the tree. On macOS a custom
        // flattened List doesn't get native outline arrow-disclosure, so we map
        // it explicitly (spec §U2-4):
        //   → : expand a collapsed folder, else (already expanded) move
        //       selection to its first child; a file has nothing to descend to.
        //   ← : collapse an expanded folder, else move selection to the parent.
        .onMoveCommand { direction in
            handleMoveCommand(direction)
        }
        // ⌘⌫ deletes the selected node, but ONLY while the tree has keyboard
        // focus (spec §U2-5 "tree-focused only"). Delivered here rather than as
        // a menu-bar chord so it can't fire while a property field or the editor
        // is focused. A folder-or-file selection is required; placeholders and
        // no-selection no-op.
        .onDeleteCommand {
            guard fileTreeFocused, let node = selectedTreeNode else { return }
            appState.deleteEntry(path: node.path, isDirectory: node.isDirectory)
        }
        // Root drop target: a node dropped on the tree background (not on a
        // folder row) moves to the vault root. Folder rows have their own
        // `.onDrop` that wins when the drop lands on them.
        .onDrop(
            of: [Self.nodeUTType], isTargeted: nil,
            perform: { providers in handleDrop(providers, into: "") })
        // Complex-gesture disclosure (WCAG 3.3.2) for the container-level
        // drop target above.
        .accessibilityHint(
            "Dropping a dragged file or folder on empty space moves it to the vault root.")
        // Seed the mirror if a selection already exists when the sidebar mounts
        // (e.g. re-entering the vault view with a file open).
        .onAppear { listSelection = rowID(forPath: appState.selectedFilePath) }
        // U2-5: react to structural mutations. AppState (the mutation funnel)
        // publishes `treeMutation`; the view-owned VM refreshes the affected
        // levels here, then U2-6 moves the selection. Token-edge-triggered so
        // two equal mutations still fire.
        .onChange(of: appState.treeMutation?.token) { _, _ in
            handleTreeMutation(proxy: proxy)
        }
        // User-driven selection: push it onto AppState here, outside the list's
        // update transaction, so handleSelectionChange runs in a well-defined
        // context. Only *file* rows drive `selectedFilePath`; selecting a
        // folder (or a placeholder) leaves the current note selection intact
        // (folders don't open). The guard prevents a write-back loop with the
        // mirror `.onChange` below.
        .onChange(of: listSelection) { _, newSelection in
            // Mirror the selection (file OR folder) to AppState so the file-
            // management commands know their target. Placeholders clear it.
            mirrorTreeSelectionToAppState(newSelection)
            // Post-delete focus moved the highlight to a sibling — honor the
            // one-shot suppression so the deleted note's error tab isn't
            // replaced by opening the sibling. Consume the flag either way.
            if suppressOpenForPostMutationFocus {
                suppressOpenForPostMutationFocus = false
                return
            }
            // Only *file* rows drive opens; the path travels in the NodeID
            // itself, so no tree lookup is needed (robust even if the level
            // was refetched between select and this callback).
            guard case let .node(.file(path)) = newSelection else { return }
            // U1-5 (#457, ported through the U2-4 rename): ⌘-click opens in
            // a new tab; the highlight then reverts to the mirror (the
            // CURRENT tab did not change files — a new tab was created).
            if appState.openTargetFromCurrentEvent() == .newTab {
                appState.openFile(path, target: .newTab)
                if case let .node(.file(selected)) = listSelection,
                    selected != appState.selectedFilePath {
                    listSelection = appState.selectedFilePath.map { .node(.file(path: $0)) }
                }
                return
            }
            if appState.selectedFilePath != path {
                appState.openFile(path, target: .currentTab)
            }
        }
        // #418 (F-A1): keyboard selection in the list is silent — VO speaks
        // only side-effect live regions ("Outline, N headings.") and a blind
        // user can't tell which file is selected while arrowing. Announce the
        // selection, but ONLY when the list itself has keyboard focus:
        // programmatic selection changes (search-open's "Opened <file>, line
        // N", template create's "Created <file>…", the dirty-gate rollback)
        // carry their own announcements and must not double-speak. Same
        // "Selected:" phrasing as the command palette.
        .onChange(of: appState.selectedFilePath) { _, newPath in
            // Mirror programmatic selection changes back onto the list
            // highlight (search-open, template-create, dirty-gate rollback).
            // Guarded so it doesn't fight the user-driven write above.
            let mirrored = rowID(forPath: newPath)
            if listSelection != mirrored {
                listSelection = mirrored
            }
            guard fileTreeFocused, let newPath else { return }
            // Red-team note: the dirty-gate rollback re-sets the selection
            // asynchronously while the "Save changes?" alert presents —
            // announcing the rollback on top of the prompt is chatter the user
            // didn't ask for.
            guard appState.pendingNavigation == nil else { return }
            guard let file = appState.files.first(where: { $0.path == newPath }) else { return }
            postAccessibilityAnnouncement(
                "Selected: \(file.name)",
                priority: .medium
            )
        }
        }  // ScrollViewReader
    }

    /// The tree node (file or folder) currently selected, if the selection is a
    /// real node (not a loading/error placeholder). Command targets read this.
    private var selectedTreeNode: TreeNode? {
        guard case let .node(id) = listSelection else { return nil }
        return tree.node(for: id)
    }

    /// Mirror the current row selection (file OR folder) into
    /// `appState.treeSelectedNode`, the single source the file-management
    /// commands (rename/move/delete/new) read. Placeholder rows or no selection
    /// clear it. Post-update mutation point (#448 discipline): this runs inside
    /// the `.onChange` closure, not during the list's update pass.
    private func mirrorTreeSelectionToAppState(_ selection: RowID?) {
        guard case let .node(id) = selection, let node = tree.node(for: id) else {
            if appState.treeSelectedNode != nil { appState.treeSelectedNode = nil }
            return
        }
        let mirrored = AppState.TreeSelection(path: node.path, isDirectory: node.isDirectory)
        if appState.treeSelectedNode != mirrored {
            appState.treeSelectedNode = mirrored
        }
    }

    /// Refresh the tree after a structural mutation (U2-5) and move the
    /// post-mutation selection (U2-6). Invalidates exactly the affected levels,
    /// then re-anchors `listSelection` on the post-mutation target and scrolls
    /// it into view.
    private func handleTreeMutation(proxy: ScrollViewProxy) {
        guard let mutation = appState.treeMutation else { return }
        // For a DELETE, the focus target (next sibling / prev / parent) must be
        // read from the tree BEFORE the level is dropped — capture it first.
        var preInvalidationDeleteTarget: NodeID?
        if case let .delete(path, parent, _) = mutation.kind {
            preInvalidationDeleteTarget = tree.deleteFocusTarget(
                deletedPath: path, parentPath: parent)
        }
        // Invalidate each dirtied level. A move dirties two (source +
        // destination); everything else dirties one. `nil` = the root level.
        for parent in mutation.affectedParents {
            tree.treeInvalidation(parent: parentNodeID(forPath: parent))
        }
        applyPostMutationFocus(mutation, deleteTarget: preInvalidationDeleteTarget, proxy: proxy)
    }

    /// Move the tree selection to the post-mutation target and scroll it into
    /// view (spec §U2-6). Focus rules:
    ///   - create-folder / create-note → the new row,
    ///   - rename → the renamed row (kept selected at its new path),
    ///   - move → the moved row at its new location (ancestors auto-expanded),
    ///   - delete → next sibling, else previous, else parent (never window-root).
    /// VoiceOver focus follows list selection (verified in the runbook pass).
    private func applyPostMutationFocus(
        _ mutation: AppState.TreeMutation, deleteTarget: NodeID?, proxy: ScrollViewProxy
    ) {
        let target: NodeID?
        switch mutation.kind {
        case let .createFolder(path), let .createNote(path):
            target = tree.focusTarget(forPath: path)
        case let .rename(_, newPath):
            target = tree.focusTarget(forPath: newPath)
        case let .move(_, newPath, _, _):
            // Reveal the moved node's new home, then resolve it.
            tree.ensureAncestorsExpanded(forPath: newPath)
            target = tree.focusTarget(forPath: newPath)
        case .delete:
            target = deleteTarget
            // After a delete the tab flips to the missing-file error state
            // (U2-5); moving the tree highlight to the sibling must NOT open it
            // (that would replace the error tab the user just created by
            // deleting). Suppress the open for this one selection change.
            suppressOpenForPostMutationFocus = true
        }
        guard let target else {
            suppressOpenForPostMutationFocus = false
            return
        }
        // Re-anchor the highlight (VO focus follows it). #448 discipline: this
        // runs inside `.onChange`, a post-update mutation point — not the list's
        // update pass. For create-note / rename / move the file is already the
        // active note (created-open, or retargeted), so re-selecting it is a
        // no-op open; for delete the open is suppressed above.
        if listSelection != .node(target) {
            listSelection = .node(target)
        } else {
            // Selection didn't change (already there) → the open-suppression
            // flag would leak to the next real selection. Clear it now.
            suppressOpenForPostMutationFocus = false
        }
        // Scroll it into view. Reduce Motion is respected by SwiftUI's implicit
        // handling; `scrollTo` itself is instantaneous here (no explicit
        // animation), matching the outline pane's behavior.
        proxy.scrollTo(RowID.node(target), anchor: .center)
    }

    /// The `NodeID` of the directory at `parentPath` (nil ⇒ the root level's
    /// sentinel), so `treeInvalidation` can target the level that changed. A
    /// dir's NodeID is `.dir(id)`, keyed by the tree's own materialized rows; if
    /// the level isn't materialized yet, `nil` (root) is the safe fallback —
    /// invalidating root refetches everything, which is correct if we can't
    /// resolve a finer target.
    private func parentNodeID(forPath parentPath: String?) -> NodeID? {
        guard let parentPath else { return nil }  // root level
        // Find the dir node for this path across the materialized tree.
        if let node = tree.rootLevel.first(where: { $0.path == parentPath && $0.isDirectory }) {
            return node.nodeID
        }
        for level in tree.children.values {
            if let node = level.first(where: { $0.path == parentPath && $0.isDirectory }) {
                return node.nodeID
            }
        }
        // Not materialized (its parent isn't expanded) → nothing to refresh at
        // that level; return a sentinel that no-ops. Using the path-derived
        // parent chain isn't possible without the id, so fall back to root only
        // if the level genuinely can't be found AND it should be visible.
        return .dir(Self.unmaterializedSentinel)
    }

    /// Sentinel dir id for "a level that isn't materialized" — distinct from the
    /// VM's root sentinel (-1) and from any real dir id (which are ≥ 1).
    /// `treeInvalidation` on it is a harmless no-op (no cached level to drop).
    private static let unmaterializedSentinel: Int64 = -2

    /// A `List` row: a real tree node, or a synthetic per-level loading/error
    /// placeholder. `Identifiable` so `ForEach` can diff rows; placeholder ids
    /// derive from the parent level id so they're stable across renders.
    private enum RowEntry: Identifiable {
        case node(TreeNode)
        case loading(parent: NodeID, depth: Int)
        case error(parent: NodeID?, depth: Int, message: String, node: TreeNode?)

        var rowID: RowID {
            switch self {
            case let .node(node): return .node(node.nodeID)
            case let .loading(parent, _): return .loading(parent: parent)
            case let .error(parent, _, _, _):
                return .error(parent: parent ?? FileTreeViewModel.rootFetchKey)
            }
        }

        var id: RowID { rowID }
    }

    /// The flattened row list handed to the `List`, with loading/error
    /// placeholder rows spliced in immediately under any expanded folder whose
    /// level is mid-fetch or failed. Root-level fetch state renders as a
    /// top-of-list placeholder.
    private var rows: [RowEntry] {
        var out: [RowEntry] = []
        // Root-level loading/error (rare — root loads on open) surfaces first.
        switch tree.fetchState[FileTreeViewModel.rootFetchKey] {
        case .loading:
            out.append(.loading(parent: FileTreeViewModel.rootFetchKey, depth: 0))
        case let .failed(message):
            out.append(.error(parent: nil, depth: 0, message: message, node: nil))
        case nil:
            break
        }
        for node in tree.visibleRows {
            out.append(.node(node))
            guard node.isDirectory, tree.expanded.contains(node.nodeID) else { continue }
            switch tree.fetchState[node.nodeID] {
            case .loading:
                out.append(.loading(parent: node.nodeID, depth: node.depth + 1))
            case let .failed(message):
                out.append(
                    .error(parent: node.nodeID, depth: node.depth + 1, message: message, node: node))
            case nil:
                break
            }
        }
        return out
    }

    @ViewBuilder
    private func rowView(for entry: RowEntry) -> some View {
        switch entry {
        case let .node(node):
            row(for: node)
        case let .loading(_, depth):
            loadingRow(depth: depth)
        case let .error(_, depth, message, node):
            errorRow(depth: depth, message: message, node: node)
        }
    }

    // MARK: - Rows

    @ViewBuilder
    private func row(for node: TreeNode) -> some View {
        if node.isDirectory {
            folderRow(node)
        } else {
            fileRow(node)
        }
    }

    /// A folder row: disclosure chevron + folder glyph + name. Disclosure state
    /// and item count live in the AX value (macOS VoiceOver doesn't voice
    /// custom row traits — #420 lesson — so state is baked into label/value).
    ///
    /// U2-5: swaps to an inline rename field when `appState.renamingNode`
    /// matches; is a drag source and a drop target (folders + root accept
    /// moves), and carries the file-management context menu.
    @ViewBuilder
    private func folderRow(_ node: TreeNode) -> some View {
        let isExpanded = tree.expanded.contains(node.nodeID)
        if isRenaming(node) {
            renameFieldRow(node)
        } else {
            HStack(spacing: Tokens.Spacing.xs) {
                indent(for: node.depth)
                // Disclosure chevron — rotates with expansion. Decorative (routed
                // through SlateSymbol, no raw glyph): the AX value already states
                // expanded/collapsed.
                SlateSymbol.disclosure.decorative
                    .rotationEffect(.degrees(isExpanded ? 90 : 0))
                    .font(.caption)
                    .foregroundStyle(.secondary)
                // Folder glyph per expanded state; decorative since the row label
                // names the folder (SlateSymbol contract).
                (isExpanded ? SlateSymbol.folderOpen : SlateSymbol.folder).decorative
                    .foregroundStyle(.secondary)
                Text(node.name)
                    .foregroundStyle(.primary)
                    .lineLimit(2)
                Spacer(minLength: 0)
            }
            .contentShape(Rectangle())
            // A folder row toggles disclosure on activation (pointer tap, or
            // Space/Return/double-click when selected). The spec preferred an
            // "isButton-free plain row", but the a11y gate (WCAG 4.1.2) requires
            // any `.onTapGesture` target to carry `.isButton` so VoiceOver users
            // can discover it's actuatable — and for a folder that genuinely
            // toggles on activation, the button role is honest. We keep BOTH: the
            // button trait (discoverable activation) and the named Expand/Collapse
            // rotor action (VO users get an explicit verb), plus the AX value that
            // states expanded/collapsed + item count + level.
            .onTapGesture { tree.toggle(node) }
            .accessibilityElement(children: .combine)
            .accessibilityAddTraits(.isButton)
            .accessibilityLabel(node.name)
            .accessibilityValue(Self.folderAccessibilityValue(for: node, expanded: isExpanded))
            .accessibilityAction(named: Text(isExpanded ? "Collapse" : "Expand")) {
                tree.toggle(node)
            }
            // U2-5 file-management rotor actions so VoiceOver users reach
            // rename/move/delete/new without the pointer context menu.
            .accessibilityAction(named: Text("Rename")) { beginRename(node) }
            .accessibilityAction(named: Text("Move to Trash")) {
                appState.deleteEntry(path: node.path, isDirectory: true)
            }
            .accessibilityAction(named: Text("New Note in Folder")) {
                appState.createNote(in: node.path)
            }
            // Complex-gesture disclosure (WCAG 3.3.2): the row carries a
            // context menu, a drag source, and a drop target — say what each
            // does, not how to perform it.
            .accessibilityHint(
                "Expands or collapses. Drag to move the folder; drop items on it to move them inside. Rename, move, delete, and new-note actions are in the context menu.")
            .help(node.path)
            .contextMenu { fileManagementMenu(for: node) }
            // Drag source: a folder can be dragged onto another folder / root.
            .onDrag { dragItem(for: node) }
            // Drop target: dropping a file/folder onto this folder moves it here.
            .onDrop(
                of: [Self.nodeUTType], isTargeted: nil,
                perform: { providers in handleDrop(providers, into: node.path) })
        }
    }

    /// A file row: name + relative modified time (the flat list's cell, carried
    /// over verbatim), indented to its depth.
    ///
    /// U2-5: swaps to an inline rename field when renaming; is a drag source and
    /// carries the file-management context menu alongside the open-in actions.
    @ViewBuilder
    private func fileRow(_ node: TreeNode) -> some View {
        if isRenaming(node) {
            renameFieldRow(node)
        } else {
            // Explicit `.primary` / `.secondary` so the text colors don't fall
            // back to whatever inherited container style happens to be in scope.
            HStack(spacing: Tokens.Spacing.xs) {
                indent(for: node.depth)
                VStack(alignment: .leading, spacing: 2) {
                    Text(node.name)
                        .foregroundStyle(.primary)
                    Text("Modified \(relativeDate(for: mtime(of: node)))")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                Spacer(minLength: 0)
            }
            .accessibilityElement(children: .combine)
            .accessibilityLabel("\(node.name), modified \(relativeDate(for: mtime(of: node)))")
            .accessibilityHint(
                "Opens the note. Drag to move it into a folder. Open-in-new-tab, split, rename, move, and delete actions are in the context menu.")
            // U2-5 file-management rotor actions.
            .accessibilityAction(named: Text("Rename")) { beginRename(node) }
            .accessibilityAction(named: Text("Move to Trash")) {
                appState.deleteEntry(path: node.path, isDirectory: false)
            }
            .help(node.path)
            .contextMenu {
                // U1-5 (#457): open-in targets. The context menu is the
                // keyboard-discoverable path (VoiceOver actions rotor); ⌘-click
                // is the pointer shortcut for a new tab.
                Button("Open") {
                    appState.openFile(node.path, target: .currentTab)
                }
                Button("Open in New Tab") {
                    appState.openFile(node.path, target: .newTab)
                }
                Button("Open in Split") {
                    appState.openFile(node.path, target: .newSplit(.horizontal))
                }
                Divider()
                fileManagementMenu(for: node)
            }
            // Drag source: a file can be dragged onto a folder / root.
            .onDrag { dragItem(for: node) }
        }
    }

    // MARK: - Inline rename (U2-5)

    /// True when this node is the one currently in inline-rename mode.
    private func isRenaming(_ node: TreeNode) -> Bool {
        appState.renamingNode?.path == node.path
    }

    /// Enter inline-rename mode for `node` (context-menu / rotor entry point).
    private func beginRename(_ node: TreeNode) {
        appState.structuralRenameError = nil
        appState.renamingNode = AppState.RenamingNode(path: node.path, isDirectory: node.isDirectory)
    }

    /// The row shown in place of the label while renaming: a focused TextField
    /// (Return commits, Esc cancels) plus an inline error below it when the last
    /// commit was rejected (collision / invalid name) — the field keeps focus so
    /// the user can correct without re-invoking (spec §U2-5).
    private func renameFieldRow(_ node: TreeNode) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            HStack(spacing: Tokens.Spacing.xs) {
                indent(for: node.depth)
                if node.isDirectory {
                    SlateSymbol.folder.decorative.foregroundStyle(.secondary)
                }
                RenameField(
                    initialName: node.name,
                    isDirectory: node.isDirectory,
                    error: appState.structuralRenameError,
                    onCommit: { newName in
                        commitRename(node, to: newName)
                    },
                    onCancel: {
                        appState.renamingNode = nil
                        appState.structuralRenameError = nil
                    })
            }
            if let error = appState.structuralRenameError {
                Text(error)
                    .font(.caption)
                    .foregroundStyle(Tokens.ColorRole.destructiveText)
                    // WCAG 1.4.4: wrap, don't clip.
                    .lineLimit(3)
                    .padding(.leading, Self.indentWidth(for: node.depth))
                    .accessibilityLabel("Rename error. \(error)")
            }
        }
    }

    /// Commit an inline rename. Empty / unchanged names just cancel; otherwise
    /// route through the AppState wrapper (which surfaces collisions inline).
    private func commitRename(_ node: TreeNode, to newName: String) {
        let trimmed = newName.trimmingCharacters(in: .whitespaces)
        guard !trimmed.isEmpty, trimmed != node.name else {
            appState.renamingNode = nil
            appState.structuralRenameError = nil
            return
        }
        appState.renameEntry(path: node.path, isDirectory: node.isDirectory, to: trimmed)
    }

    // MARK: - Context menu (U2-5)

    /// The shared file-management actions for a node's context menu: New Note /
    /// New Folder (into a folder, or the node's parent for a file), Rename, Move
    /// to…, Move to Trash. Each routes through the AppState wrapper. SlateSymbol
    /// labels per the u2_spec table.
    @ViewBuilder
    private func fileManagementMenu(for node: TreeNode) -> some View {
        let creationParent = node.isDirectory ? node.path : (AppState.TreeMutation.parentPath(of: node.path) ?? "")
        Button {
            appState.createNote(in: creationParent)
        } label: {
            SlateSymbol.newNote.label("New Note")
        }
        Button {
            appState.newFolderInContext(parent: creationParent)
        } label: {
            SlateSymbol.newFolder.label("New Folder")
        }
        Divider()
        Button {
            beginRename(node)
        } label: {
            SlateSymbol.rename.label("Rename")
        }
        Button {
            appState.pendingMove = AppState.PendingMove(path: node.path, isDirectory: node.isDirectory)
        } label: {
            SlateSymbol.moveTo.label("Move to…")
        }
        Divider()
        Button(role: .destructive) {
            appState.deleteEntry(path: node.path, isDirectory: node.isDirectory)
        } label: {
            SlateSymbol.trash.label("Move to Trash")
        }
    }

    // MARK: - Drag & drop (U2-5)

    /// Custom UTType carrying a tree node's vault-relative path across a drag.
    /// A private app type so drops from Finder / other apps don't masquerade as
    /// intra-tree moves.
    static let nodeUTType = "com.slate.tree-node-path"

    /// Build the drag payload for `node`: an `NSItemProvider` carrying the
    /// node's path under the private UTType. Also records the source in AppState
    /// so the drop handler knows whether the dragged node is a directory.
    private func dragItem(for node: TreeNode) -> NSItemProvider {
        appState.dragSourceNode = AppState.TreeSelection(
            path: node.path, isDirectory: node.isDirectory)
        let provider = NSItemProvider()
        provider.registerDataRepresentation(
            forTypeIdentifier: Self.nodeUTType, visibility: .ownProcess
        ) { completion in
            completion(Data(node.path.utf8), nil)
            return nil
        }
        return provider
    }

    /// Handle a drop of a dragged tree node into `destinationFolder` ("" =
    /// root). Reads the dragged path, resolves whether it's a directory from the
    /// recorded source, and routes through the SAME `moveEntry` wrapper the
    /// commands use (spec §U2-5: "drop calls exactly `move_file`/`move_folder`
    /// — the commands are the source of truth"). Rejects a no-op (already in the
    /// destination) and a folder-into-own-subtree drop (the backend also
    /// guards, but rejecting early avoids a wasted round-trip + error alert).
    private func handleDrop(_ providers: [NSItemProvider], into destinationFolder: String) -> Bool {
        guard let provider = providers.first(where: { $0.hasItemConformingToTypeIdentifier(Self.nodeUTType) })
        else { return false }
        provider.loadDataRepresentation(forTypeIdentifier: Self.nodeUTType) { data, _ in
            guard let data, let path = String(data: data, encoding: .utf8) else { return }
            Task { @MainActor in
                let isDir = appState.dragSourceNode?.path == path
                    ? (appState.dragSourceNode?.isDirectory ?? false)
                    : false
                appState.dragSourceNode = nil
                // No-op: already directly in the destination folder.
                let currentParent = AppState.TreeMutation.parentPath(of: path) ?? ""
                if currentParent == destinationFolder { return }
                // Folder into its own subtree (or onto itself) — reject early.
                if isDir, AppState.pathIsWithin(destinationFolder, path: path, isDirectory: true) {
                    return
                }
                appState.moveEntry(path: path, isDirectory: isDir, to: destinationFolder)
            }
        }
        return true
    }

    /// Inline "Loading…" row shown under a folder whose children are being
    /// fetched. Labeled so VoiceOver announces the wait.
    private func loadingRow(depth: Int) -> some View {
        HStack(spacing: Tokens.Spacing.sm) {
            indent(for: depth)
            ProgressView()
                .controlSize(.small)
            Text("Loading…")
                .font(.caption)
                .foregroundStyle(.secondary)
            Spacer(minLength: 0)
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Loading folder contents.")
    }

    /// Inline error row shown under a folder whose children failed to load.
    /// Carries the specific message and a Retry button that refetches that
    /// level (a real folder via `treeInvalidation`; the root via `loadRoot`).
    private func errorRow(depth: Int, message: String, node: TreeNode?) -> some View {
        HStack(spacing: Tokens.Spacing.sm) {
            indent(for: depth)
            SlateSymbol.warning.image()
                .foregroundStyle(Tokens.ColorRole.destructiveText)
            Text(message)
                .font(.caption)
                .foregroundStyle(.primary)
                // WCAG 1.4.4: no lineLimit(1) — let Dynamic Type wrap.
                .lineLimit(3)
            Spacer(minLength: 0)
            Button("Retry") {
                if let node {
                    tree.treeInvalidation(parent: node.nodeID)
                } else {
                    tree.loadRoot()
                }
            }
            .buttonStyle(.borderless)
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Error loading folder. \(message)")
    }

    /// Fixed-width indent for a row at `depth`. `Tokens.Spacing.md` (12pt) per
    /// level, scaled by Dynamic Type so the hierarchy doesn't visually collapse
    /// at large text sizes (same technique as the outline pane).
    @ViewBuilder
    private func indent(for depth: Int) -> some View {
        if depth > 0 {
            Spacer()
                .frame(width: Self.indentWidth(for: depth))
        }
    }

    // MARK: - Progress strip (carried over from the flat list, verbatim)

    /// Determinate progress strip rendered above the tree while a scan is in
    /// flight. Returns nil between scans (or once the scan terminates) so it
    /// stays hidden by default.
    ///
    /// We render the bar for `FileIndexed` events; `Started` reports 0 indexed
    /// which gives an empty bar (still useful — it lets the user see the scan
    /// kicked off before the first file lands). `Finished` / `Cancelled` /
    /// `Failed` clear `scanProgress` on the AppState side so this returns nil
    /// and the strip disappears.
    @ViewBuilder private var progressBar: some View {
        switch appState.scanProgress {
        case .started(let total):
            scanStrip(
                label: total == 1
                    ? "Scanning vault — 1 file to index."
                    : "Scanning vault — \(total) files to index.",
                progress: total == 0 ? nil : 0,
                total: total
            )
        case .fileIndexed(_, let indexed, let total):
            scanStrip(
                label: total == 0
                    ? "Indexed \(indexed) files."
                    : "Indexed \(indexed) of \(total) files.",
                progress: total == 0 ? nil : Double(indexed) / Double(total),
                total: total
            )
        case .finished, .cancelled, .failed, .none:
            EmptyView()
        case .some:
            // Defensive: future enum variants stay hidden rather than showing a
            // stale strip.
            EmptyView()
        }
    }

    private func scanStrip(label: String, progress: Double?, total: UInt64) -> some View {
        // Indeterminate (`progress == nil`) when we don't yet know the
        // denominator — Started{totalFiles: 0} or a FileIndexed with total ==
        // 0. The label still tells the user what's happening.
        HStack(spacing: 8) {
            if let progress {
                ProgressView(value: progress)
                    .progressViewStyle(.linear)
            } else {
                ProgressView()
                    .progressViewStyle(.linear)
            }
            Text(label)
                .font(.caption)
                .foregroundStyle(.secondary)
                // WCAG 1.4.4: no lineLimit(1) — let Dynamic Type wrap.
                .lineLimit(2)
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 6)
        // Combine into one accessible element so VoiceOver reads "<label>"
        // instead of separately announcing the bar.
        .accessibilityElement(children: .combine)
        .accessibilityLabel(label)
        .accessibilityValue(
            progress.map { String(Int($0 * 100)) + " percent" } ?? "Scanning"
        )
    }

    // MARK: - Keyboard disclosure

    /// →/← handling for the tree (macOS custom-row outline navigation). The
    /// mapping decision is the VM's pure `moveOutcome`; this just applies it, so
    /// the semantics are unit-tested without a running List. Up/down are left to
    /// the List's native row navigation.
    private func handleMoveCommand(_ direction: MoveCommandDirection) {
        guard case let .node(selectedID) = listSelection else { return }
        let right: Bool
        switch direction {
        case .right: right = true
        case .left: right = false
        case .up, .down: return  // native List row navigation
        @unknown default: return
        }
        switch tree.moveOutcome(for: selectedID, right: right) {
        case let .expand(id):
            if let node = tree.node(for: id) { tree.expand(node) }
        case let .collapse(id):
            if let node = tree.node(for: id) { tree.collapse(node) }
        case let .select(id):
            listSelection = .node(id)
        case .none:
            break
        }
    }

    // MARK: - AX builders (unit-tested)

    /// The AX value for a folder row: disclosure state, immediate item count,
    /// and 1-based depth ("level N"). macOS VoiceOver doesn't voice custom-row
    /// traits (#420), and there is no outline-level API for custom SwiftUI rows
    /// on macOS, so depth is conveyed here in the value string — the same
    /// technique the outline pane uses for heading level.
    ///
    /// Static + pure so the phrasing is regression-locked by a unit test.
    static func folderAccessibilityValue(for node: TreeNode, expanded: Bool) -> String {
        let state = expanded ? "expanded" : "collapsed"
        let count = node.itemCount
        let items = count == 1 ? "1 item" : "\(count) items"
        // Depth is 0-based internally; VoiceOver parity wants 1-based levels.
        return "\(state), \(items), level \(node.depth + 1)"
    }

    // MARK: - Helpers

    /// Map a file path to its row `RowID`, if that file is currently in the
    /// materialized tree (root or a cached level). Used to mirror
    /// `selectedFilePath` onto the list highlight. A path not yet materialized
    /// (its folder isn't expanded) has no row to highlight — return nil; the
    /// note still loads via `selectedFilePath`.
    ///
    /// A file's `NodeID` *is* its path (see `NodeID`), so once we confirm the
    /// row exists we hand back `.node(.file(path:))` directly.
    private func rowID(forPath path: String?) -> RowID? {
        guard let path else { return nil }
        let fileID = NodeID.file(path: path)
        if tree.rootLevel.contains(where: { $0.nodeID == fileID }) {
            return .node(fileID)
        }
        for level in tree.children.values where level.contains(where: { $0.nodeID == fileID }) {
            return .node(fileID)
        }
        return nil
    }

    /// The mtime a file node carries from the tree API. Reading it off the
    /// node (not `AppState.files`) matters: a per-row linear scan of a 10k
    /// file list × ~50 visible rows was O(500k) string compares per render
    /// pass (principal review of the U2-4 implementation).
    private func mtime(of node: TreeNode) -> Int64 {
        if case .file(let mtimeMs) = node.kind { return mtimeMs }
        return 0
    }

    /// Indent width for a row at `depth`: `Tokens.Spacing.md` per level.
    /// Static + pure so tests can assert the exact geometry.
    static func indentWidth(for depth: Int) -> CGFloat {
        CGFloat(depth) * Tokens.Spacing.md
    }

    /// Cached so a vault of 10k rows doesn't allocate 10k formatters.
    /// RelativeDateTimeFormatter is thread-safe for `localizedString` reads, and
    /// we only mutate `unitsStyle` once at init.
    private static let relativeFormatter: RelativeDateTimeFormatter = {
        let formatter = RelativeDateTimeFormatter()
        formatter.unitsStyle = .full
        return formatter
    }()

    private func relativeDate(for mtimeMs: Int64) -> String {
        let date = Date(timeIntervalSince1970: TimeInterval(mtimeMs) / 1000)
        return Self.relativeFormatter.localizedString(for: date, relativeTo: Date())
    }
}

/// The inline rename TextField swapped into a tree row while renaming (U2-5).
///
/// - Auto-focuses on appear and selects the base name (a file's extension is
///   left out of the selection so Return-to-keep-extension is one keystroke —
///   spec §U2-5: "current name with extension excluded from the initial
///   selection for files").
/// - Return commits (`onCommit`); Esc cancels (`onCancel`). A focus loss also
///   commits (matches Finder), so clicking away doesn't silently lose the edit.
/// - Kept a separate `View` with its own `@State`/`@FocusState` so the field's
///   local text isn't a `@Published` on AppState mutated during a view update
///   (#448 discipline) — the commit is the only mutation point that reaches
///   AppState.
private struct RenameField: View {
    let initialName: String
    let isDirectory: Bool
    /// The last commit error (collision / invalid name), if any — drives the
    /// field's error styling. The message itself renders in the parent row.
    let error: String?
    let onCommit: (String) -> Void
    let onCancel: () -> Void

    @State private var text: String = ""
    @State private var didSeed = false
    @FocusState private var focused: Bool

    var body: some View {
        TextField("Name", text: $text)
            .textFieldStyle(.roundedBorder)
            .font(.body)
            .focused($focused)
            .accessibilityLabel(isDirectory ? "Folder name" : "File name")
            .accessibilityHint("Type a new name. Return renames; Escape cancels.")
            .onSubmit { onCommit(text) }
            .onExitCommand { onCancel() }
            .onChange(of: focused) { _, isFocused in
                // Commit on focus loss (click-away) — but not on the initial
                // pre-focus render, and not while an error keeps us focused.
                if !isFocused && didSeed && error == nil {
                    onCommit(text)
                }
            }
            .onAppear {
                if !didSeed {
                    text = initialName
                    didSeed = true
                }
                focused = true
                selectBaseName()
            }
    }

    /// Select the editable portion of the name: the whole thing for a folder,
    /// the stem (sans extension) for a file. Reaches into the AppKit field
    /// editor after focus lands — SwiftUI has no partial-selection API.
    private func selectBaseName() {
        DispatchQueue.main.async {
            guard let window = NSApp?.keyWindow,
                let editor = window.firstResponder as? NSTextView
            else { return }
            let ns = initialName as NSString
            let extLength = (ns.pathExtension as NSString).length
            let end: Int
            if !isDirectory, extLength > 0, ns.length > extLength + 1 {
                // Stop the selection just before the ".ext".
                end = ns.length - extLength - 1
            } else {
                end = ns.length
            }
            editor.setSelectedRange(NSRange(location: 0, length: end))
        }
    }
}

/// Bridges `WorkspaceState.treeFocusRequest` (bumped by ⌘⌥← off the leftmost
/// editor group, U4-4 #473) into the file tree's `@FocusState`.
///
/// Exists because `FileTreeSidebar` observes only `appState`, and AppState's
/// publisher does not forward the nested `WorkspaceState`'s `@Published`
/// changes — so an `.onChange(of: appState.workspace.treeFocusRequest)` on the
/// sidebar would never re-evaluate and never fire. This tiny view DOES observe
/// `workspace` (`@ObservedObject`), so its body re-evaluates when the request
/// bumps; the `.onChange` then mirrors it into the passed `FocusState` binding
/// (a post-update mutation point — never publishing inside the update
/// transaction, #448). Rendered in a `.background` so it adds no layout.
/// When the tree list isn't present (empty/scanning vault) the FocusState has
/// no target and the assignment is a harmless no-op — ⌘⌥→ still exits, so focus
/// is never trapped.
private struct TreeFocusBridge: View {
    @ObservedObject var workspace: WorkspaceState
    var focused: FocusState<Bool>.Binding

    var body: some View {
        Color.clear
            .frame(width: 0, height: 0)
            .accessibilityHidden(true)
            .onChange(of: workspace.treeFocusRequest) {
                focused.wrappedValue = true
            }
    }
}
