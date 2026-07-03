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
/// The per-note panel stack, the scan progress strip, and the #418 selection-
/// announcement discipline are carried over unchanged from the flat list.
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

            // Per-note panels live below the tree inside the same sidebar
            // column. They self-hide when no note is selected (returning
            // EmptyView), so they don't push the tree around in the empty
            // case. Order matches the mental model of "what does this note say
            // about itself / link to / get linked from": properties → outgoing
            // → backlinks → tasks. Tasks lands last because users typically
            // scroll to it after reading the note's structural context; the
            // panel is dense (toggleable rows) and benefits from being below
            // the metadata sections that don't require interaction. (Outline
            // lives in the third NavigationSplit column, not here.)
            Divider()
            ScrollView {
                VStack(alignment: .leading, spacing: 0) {
                    PropertiesPanel()
                    OutgoingLinksPanel()
                    BacklinksPanel()
                    EmbedsPanel()
                    // Milestone K surfaces (#410): math / code / diagram
                    // pipelines for the selected note, after embeds (same
                    // "what does this note contain" family), before tasks
                    // (interaction-dense, kept last).
                    MathBlocksPanel()
                    CodeBlocksPanel()
                    DiagramsPanel()
                    TasksPanel()
                }
            }
            .frame(maxHeight: 400)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .navigationTitle("Files")
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
        List(selection: $listSelection) {
            ForEach(rows) { entry in
                rowView(for: entry)
                    .tag(entry.rowID)
            }
        }
        .listStyle(.sidebar)
        .focused($fileTreeFocused)
        // Keyboard disclosure: →/← move through the tree. On macOS a custom
        // flattened List doesn't get native outline arrow-disclosure, so we map
        // it explicitly (spec §U2-4):
        //   → : expand a collapsed folder, else (already expanded) move
        //       selection to its first child; a file has nothing to descend to.
        //   ← : collapse an expanded folder, else move selection to the parent.
        .onMoveCommand { direction in
            handleMoveCommand(direction)
        }
        // Seed the mirror if a selection already exists when the sidebar mounts
        // (e.g. re-entering the vault view with a file open).
        .onAppear { listSelection = rowID(forPath: appState.selectedFilePath) }
        // User-driven selection: push it onto AppState here, outside the list's
        // update transaction, so handleSelectionChange runs in a well-defined
        // context. Only *file* rows drive `selectedFilePath`; selecting a
        // folder (or a placeholder) leaves the current note selection intact
        // (folders don't open). The guard prevents a write-back loop with the
        // mirror `.onChange` below.
        .onChange(of: listSelection) { _, newSelection in
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
    }

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
    private func folderRow(_ node: TreeNode) -> some View {
        let isExpanded = tree.expanded.contains(node.nodeID)
        return HStack(spacing: Tokens.Spacing.xs) {
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
        .help(node.path)
    }

    /// A file row: name + relative modified time (the flat list's cell, carried
    /// over verbatim), indented to its depth.
    private func fileRow(_ node: TreeNode) -> some View {
        // Explicit `.primary` / `.secondary` so the text colors don't fall back
        // to whatever inherited container style happens to be in scope. Xcode's
        // Accessibility Inspector reported contrast failures on these rows with
        // foreground and background colors nearly identical (#100F16 vs
        // #101016) — most likely the inspector sampling antialiased edges on a
        // dark sidebar bg, but pinning the foreground style makes the intent
        // unambiguous either way.
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
            "Opens the note. Open-in-new-tab and split actions are in the context menu.")
        .help(node.path)
        // U1-5 (#457), ported through the U2-4 rename: open-in targets.
        // The context menu is the keyboard-discoverable path (VoiceOver
        // actions rotor); ⌘-click is the pointer shortcut for a new tab.
        .contextMenu {
            Button("Open") {
                appState.openFile(node.path, target: .currentTab)
            }
            Button("Open in New Tab") {
                appState.openFile(node.path, target: .newTab)
            }
            Button("Open in Split") {
                appState.openFile(node.path, target: .newSplit(.horizontal))
            }
        }
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
