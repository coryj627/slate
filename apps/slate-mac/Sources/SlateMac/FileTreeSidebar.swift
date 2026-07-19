// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI
import UniformTypeIdentifiers

/// User-facing message for a shared Sidebar dispatch failure. UniFFI's typed
/// command error carries the actionable reason; unrelated errors retain their
/// localized description. Keeping this as a property lets every renderer make
/// exactly one announcement call without hiding dispatch behind another
/// executor.
extension Error {
    var sidebarActionAnnouncement: String {
        guard let commandError = self as? CommandError,
            case let .ActionFailed(message) = commandError
        else {
            return localizedDescription
        }
        return message
    }
}

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

/// Mutable presentation state for one materialized file row.
///
/// Tree structure and row order stay in `FileTreeViewModel`'s published arrays;
/// derived metadata changes do not. A targeted save updates this keyed object,
/// so SwiftUI invalidates only the observing row instead of rebuilding and
/// equality-comparing a 50k-entry structural array on the main actor.
@MainActor
final class FileTreeFileState: ObservableObject, Equatable {
    @Published private(set) var summary: FileSummary

    init(summary: FileSummary) {
        self.summary = summary
    }

    @discardableResult
    func replace(with summary: FileSummary) -> Bool {
        guard self.summary != summary else { return false }
        self.summary = summary
        return true
    }

    nonisolated static func == (lhs: FileTreeFileState, rhs: FileTreeFileState) -> Bool {
        lhs === rhs
    }
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
        /// A file carries a stable keyed summary object. Rich metadata updates
        /// publish through that object without mutating the structural tree;
        /// identity/rename still use `path`/`name` on `TreeNode`.
        case file(FileTreeFileState)
    }

    var id: NodeID { nodeID }

    var isDirectory: Bool {
        if case .directory = kind { return true }
        return false
    }

    /// Indexed metadata owned by the live file-row state. Path extensions are
    /// intentionally not consulted; the backend summary is authoritative.
    @MainActor var isMarkdown: Bool {
        if case let .file(state) = kind { return state.summary.isMarkdown }
        return false
    }

    /// Total immediate children (dirs + files) — the "N items" count a folder
    /// row announces. Directories only; 0 for files.
    var itemCount: Int {
        if case let .directory(dirs, files) = kind { return dirs + files }
        return 0
    }
}

/// Visual-only content for a folder List row. Interaction and accessibility
/// remain on `FileTreeSidebar.folderRow`; extracting the label makes the
/// active/inactive native-selection palette render-testable.
struct SidebarFolderRowContent: View {
    let node: TreeNode
    let isExpanded: Bool
    let isSelected: Bool
    let selectionIsActive: Bool
    var isDropTargeted = false

    @Environment(\.layoutDirection) private var layoutDirection

    private var primaryText: Color {
        isSelected
            ? Color(nsColor: SidebarSelectionColors.text(active: selectionIsActive))
            : Tokens.ColorRole.textPrimary
    }

    private var secondaryText: Color {
        isSelected
            ? Color(nsColor: SidebarSelectionColors.text(active: selectionIsActive))
            : Tokens.ColorRole.textSecondary
    }

    var body: some View {
        HStack(spacing: Tokens.Spacing.xs) {
            Color.clear
                .frame(width: FileTreeSidebar.indentWidth(for: node.depth), height: 0)
                .accessibilityHidden(true)
            SlateSymbol.disclosure.decorative
                .rotationEffect(
                    .degrees(
                        isExpanded
                            ? (layoutDirection == .rightToLeft ? -90 : 90)
                            : 0))
                .font(Tokens.Typography.caption)
                .foregroundStyle(secondaryText)
            (isExpanded ? SlateSymbol.folderOpen : SlateSymbol.folder).decorative
                .foregroundStyle(secondaryText)
            Text(node.name)
                .font(Tokens.Typography.body)
                .foregroundStyle(primaryText)
                .lineLimit(2)
            Spacer(minLength: 0)
        }
        .overlay {
            if isDropTargeted {
                RoundedRectangle(cornerRadius: Tokens.Radius.control)
                    .strokeBorder(
                        Color(
                            nsColor: SidebarSelectionColors.dropIndicator(
                                selected: isSelected,
                                active: selectionIsActive)),
                        lineWidth: 2)
                    .accessibilityHidden(true)
            }
        }
    }
}

/// The only observer of a file's mutable summary. Keeping this subscription at
/// the cell boundary is what makes an O(changed) metadata refresh remain
/// O(changed) through SwiftUI rather than invalidating the whole tree view.
struct SidebarObservedFileRowContent: View {
    @ObservedObject var fileState: FileTreeFileState
    let preferences: SidebarRowPreferencesSnapshot
    var isPinned = false
    let now: Date
    let depth: Int
    let isSelected: Bool
    let selectionIsActive: Bool

    var body: some View {
        SidebarFileRow(
            model: SidebarRowModel(
                summary: fileState.summary,
                preferences: preferences,
                isPinned: isPinned,
                now: now),
            depth: depth,
            isSelected: isSelected,
            selectionIsActive: selectionIsActive)
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

    // MARK: - Organization (FL-06, #658/#659)

    /// Everything level organization consumes: effective sort/grouping
    /// preferences, pins, and an injected clock/calendar/locale so bucket
    /// boundaries never read wall time inside the model.
    struct OrganizationContext {
        var prefs = SidebarOrganizationPrefs()
        var pins = SidebarPins()
        var now = Date()
        var calendar = Calendar.current
        var locale = Locale.current
        var civilDateResolver: any SidebarCivilDateResolving =
            SidebarProductionCivilDateResolver()
    }

    /// Header/pin lookups for the rendered rows, merged across levels.
    /// Published separately from the structural arrays so a label-only change
    /// (a day rollover) still re-renders without republishing every level.
    @Published private(set) var treePresentation = SidebarTreePresentation()

    private var organization = OrganizationContext()
    /// Stale-pin report seam: fired whenever an organized level carries
    /// authored pin entries that no longer resolve to rows. The subscriber
    /// (AppState's once-per-session prune) owns dedup and persistence.
    var onStalePins: ((_ folder: String, _ stale: [String]) -> Void)?
    private var presentationByParent: [NodeID: SidebarLevelPresentation] = [:]
    private var parentPathByKey: [NodeID: String] = [:]
    private var parentKeyByPath: [String: NodeID] = [:]
    /// Whether a stored level's listing was complete (no next page). Stale
    /// pins are only ever reported from complete levels.
    private var completeLevelByKey: [NodeID: Bool] = [:]
    private(set) var levelReorganizeCountForTesting = 0

    /// Adopt new organization inputs. Cached levels re-sort only when the
    /// preferences, pins, calendar/locale, or the local civil day actually
    /// changed — the relative-date clock tick is a no-op here.
    func applyOrganization(_ context: OrganizationContext) {
        let previous = organization
        organization = context
        let inputsChanged =
            previous.prefs != context.prefs
            || previous.pins != context.pins
            || previous.calendar.identifier != context.calendar.identifier
            || previous.calendar.timeZone != context.calendar.timeZone
            || previous.locale != context.locale
        let dayChanged = !context.calendar.isDate(
            previous.now, inSameDayAs: context.now)
        guard inputsChanged || dayChanged else { return }
        reorganizeCachedLevels()
    }

    /// The synthetic header rendered immediately above this file row, if any.
    func headerRow(before id: NodeID) -> SidebarTreeHeaderRow? {
        treePresentation.headersBefore[id]
    }

    func isPinnedRow(_ id: NodeID) -> Bool {
        treePresentation.pinnedIDs.contains(id)
    }

    /// Authored pin entries that no longer resolve to a row in the folder's
    /// materialized level. AppState consumes this for the lazy once-per-
    /// session prune (fl3 spec §FL3-2.3).
    func stalePins(forFolder folder: String) -> [String] {
        for (key, path) in parentPathByKey where path == folder {
            return presentationByParent[key]?.stalePinnedPaths ?? []
        }
        return []
    }

    /// Sort the file portion of a fetched level and derive its header/pin
    /// presentation. Directories keep their backend order and always precede
    /// files (fl3 spec §FL3-1.1).
    private func organizedPresentation(
        level: [TreeNode], parentPath: String
    ) -> (nodes: [TreeNode], presentation: SidebarLevelPresentation) {
        let fileNodes = level.filter { !$0.isDirectory }
        let pinnedPaths = organization.pins.paths(forFolder: parentPath)
        if fileNodes.isEmpty {
            // Nothing to sort; every authored pin for this folder is stale.
            return (level, SidebarLevelPresentation(stalePinnedPaths: pinnedPaths))
        }

        let files = fileNodes.map { node -> SidebarOrganizerFile in
            let summary: FileSummary
            if case let .file(state) = node.kind {
                summary = state.summary
            } else {
                summary = FileSummary(
                    path: node.path, name: node.name, mtimeMs: 0, sizeBytes: 0,
                    isMarkdown: false, displayName: nil, createdDate: nil,
                    createdMs: nil, wordCount: nil, preview: nil, taskTotal: 0,
                    taskOpen: 0)
            }
            return SidebarOrganizerFile(
                path: summary.path,
                name: summary.name,
                displayName: summary.displayName,
                createdDate: summary.createdDate,
                createdMs: summary.createdMs,
                mtimeMs: summary.mtimeMs)
        }

        let organized = SidebarLevelOrganizer.organize(
            files: files,
            choice: organization.prefs.effectiveChoice(forFolder: parentPath),
            pinnedPaths: pinnedPaths,
            now: organization.now,
            calendar: organization.calendar,
            locale: organization.locale,
            civilDateResolver: organization.civilDateResolver)

        var nodeByPath: [String: TreeNode] = [:]
        nodeByPath.reserveCapacity(fileNodes.count)
        for node in fileNodes { nodeByPath[node.path] = node }
        let orderedFiles = organized.orderedPaths.compactMap { nodeByPath[$0] }
        let depth = fileNodes[0].depth

        var headers: [NodeID: SidebarTreeHeaderRow] = [:]
        if organized.pinnedCount > 0, let firstPinned = orderedFiles.first {
            headers[firstPinned.nodeID] = SidebarTreeHeaderRow(
                kind: .pinned,
                key: "pinned",
                label: "Pinned",
                fileCount: organized.pinnedCount,
                depth: depth)
        }
        for group in organized.groups {
            headers[.file(path: group.firstPath)] = SidebarTreeHeaderRow(
                kind: .group,
                key: group.key,
                label: group.label,
                fileCount: group.fileCount,
                depth: depth)
        }

        let presentation = SidebarLevelPresentation(
            headersBefore: headers,
            pinnedIDs: Set(orderedFiles.prefix(organized.pinnedCount).map(\.nodeID)),
            stalePinnedPaths: organized.stalePinnedPaths)
        let dirs = level.filter(\.isDirectory)
        return (dirs + orderedFiles, presentation)
    }

    /// Re-run organization over every cached level, publishing only levels
    /// whose order or presentation actually changed.
    private func reorganizeCachedLevels() {
        reorganizeLevel(key: Self.rootFetchKey)
        for key in children.keys {
            reorganizeLevel(key: key)
        }
        rebuildMergedPresentation()
    }

    private func reorganizeLevel(key: NodeID) {
        let current: [TreeNode]
        if key == Self.rootFetchKey {
            current = rootLevel
        } else {
            guard let level = children[key] else { return }
            current = level
        }
        guard !current.isEmpty, let parentPath = parentPathByKey[key] else { return }
        let reorganizedResult = organizedPresentation(
            level: current, parentPath: parentPath)
        let organized = reorganizedResult.nodes
        var presentation = reorganizedResult.presentation
        if completeLevelByKey[key] != true {
            presentation.stalePinnedPaths = []
        }
        let orderChanged = organized.map(\.nodeID) != current.map(\.nodeID)
        let presentationChanged = presentationByParent[key] != presentation
        guard orderChanged || presentationChanged else { return }
        levelReorganizeCountForTesting += 1
        presentationByParent[key] = presentation
        reportStalePins(presentation, folder: parentPath)
        guard orderChanged else { return }
        // Membership is unchanged, so the path/state indexes stay valid.
        if key == Self.rootFetchKey {
            rootLevel = organized
        } else {
            children[key] = organized
        }
    }

    private func reportStalePins(
        _ presentation: SidebarLevelPresentation, folder: String
    ) {
        guard !presentation.stalePinnedPaths.isEmpty else { return }
        onStalePins?(folder, presentation.stalePinnedPaths)
    }

    private func rebuildMergedPresentation() {
        var merged = SidebarTreePresentation()
        for presentation in presentationByParent.values {
            merged.headersBefore.merge(presentation.headersBefore) { _, new in new }
            merged.pinnedIDs.formUnion(presentation.pinnedIDs)
        }
        if merged != treePresentation {
            treePresentation = merged
        }
    }

    /// Fields whose change can move a row under some active sort or grouping.
    private static func organizationKeyChanged(
        _ old: FileSummary, _ new: FileSummary
    ) -> Bool {
        old.displayName != new.displayName
            || old.name != new.name
            || old.createdDate != new.createdDate
            || old.createdMs != new.createdMs
            || old.mtimeMs != new.mtimeMs
    }

    /// The session the tree reads from. Swapped by `bind(to:)` when the vault
    /// changes; nil clears the tree.
    private var session: VaultSession?
    /// Exact identity captured when the tree binds. Selection publication uses
    /// this token rather than consulting AppState later, so a stale tree can
    /// never stamp rows with a replacement vault's session.
    private(set) var sessionIdentity: ObjectIdentifier?

    /// Level fetcher: `parentPath` → one level's listing. Production routes to
    /// `session.listDirChildren`; tests inject a spy to assert *which* levels
    /// are fetched (the lazy-fetch guarantee) and to stand up large synthetic
    /// fixtures without an FFI round-trip. `nil` until `bind(to:)`.
    private var fetcher: ((String, String?) throws -> DirListing)?

    /// Sentinel thrown by the single-shot test wrapper for a continuation
    /// cursor it cannot serve; the drain treats it as "incomplete level".
    private struct LevelPageUnavailable: Error {}

    /// Total-files safety bound for one level's drain. Levels beyond it are
    /// stored partial (organization still applies to what is materialized,
    /// stale pruning stays suppressed).
    nonisolated static let levelTotalSafetyCap = 50_000

    /// Ownership token per level for the asynchronous continuation drain
    /// (round-14 finding 1): a drain publishes only if its token is still
    /// current for that level and the session identity is unchanged.
    /// Revocation is LEVEL-SCOPED (round-16): each drop path clears exactly
    /// the affected levels' tokens (nil-clears per level; bulk child clears
    /// purge child tokens; a root refetch overwrites the root token), so a
    /// targeted invalidation of one folder never strands another folder's
    /// running drain as a silent permanent partial.
    private var levelDrainTokens: [NodeID: UUID] = [:]
    /// Newest summaries for paths NOT yet materialized while a continuation
    /// drain is in flight (round-19 finding 2), KEYED BY DRAIN TOKEN
    /// (round-20 finding 2): only the owning drain may consume its entries,
    /// and every revocation path drops that token's map — a replacement
    /// drain (whose pages already reflect the save) can never consume a
    /// stale predecessor's buffer.
    private var pendingSummaryOverlay: [UUID: [String: FileSummary]] = [:]
    private static let pendingSummaryOverlayCap = 4096
    private(set) var levelDrainTasksForTesting: [NodeID: Task<Void, Never>] = [:]


    /// Round-13 finding 1 + round-14 finding 1: the FIRST page fetches
    /// synchronously (the shipped U2 behavior — content appears immediately
    /// and every synchronous test seam holds), while any REMAINING pages
    /// drain off the main actor and merge in one publish when they land.
    /// Until the drain completes the level is stored partial, so stale-pin
    /// pruning stays suppressed (round-5 finding 2).
    private func scheduleContinuationDrain(
        parentKey: NodeID,
        parentPath: String,
        depth: Int,
        firstPage: DirListing
    ) {
        guard let fetcher else { return }
        let token = UUID()
        levelDrainTokens[parentKey] = token
        let capturedSession = sessionIdentity
        let startCursor = firstPage.files.nextCursor
        let baseCount = firstPage.files.items.count
        let task = Task { [weak self] in
            let outcome: Result<(files: [FileSummary], isComplete: Bool), Error> =
                await Task.detached(priority: .userInitiated) {
                    var files: [FileSummary] = []
                    var cursor = startCursor
                    var isComplete = true
                    do {
                        while let next = cursor {
                            let page = try fetcher(parentPath, next)
                            files.append(contentsOf: page.files.items)
                            cursor = page.files.nextCursor
                            if cursor != nil,
                                baseCount + files.count
                                    >= FileTreeViewModel.levelTotalSafetyCap
                            {
                                isComplete = false
                                break
                            }
                        }
                        return .success((files, isComplete))
                    } catch is LevelPageUnavailable {
                        return .success(([], false))
                    } catch {
                        return .failure(error)
                    }
                }.value
            guard let self,
                self.levelDrainTokens[parentKey] == token,
                self.sessionIdentity == capturedSession
            else { return }
            self.levelDrainTokens[parentKey] = nil
            self.levelDrainTasksForTesting[parentKey] = nil
            defer {
                // This drain's leftovers can never be consumed again.
                self.pendingSummaryOverlay[token] = nil
            }
            guard case .success(let drained) = outcome else {
                // Round-15 finding 2: a failed continuation keeps the
                // already-published partial page but surfaces the existing
                // inline error + Retry instead of a silent permanent
                // partial; Retry refetches the whole level.
                if case .failure(let error) = outcome {
                    self.fetchState[parentKey] = .failed(
                        message: Self.message(for: error))
                }
                return
            }
            // Round-16: preserve LIVE metadata for already-materialized
            // rows — a save that updated a first-page row's title/mtime while
            // the drain was in flight must not be overwritten by the stale
            // page snapshot. Reusing the state object also keeps SwiftUI row
            // identity.
            var level: [TreeNode] = []
            level.reserveCapacity(
                firstPage.dirs.count + baseCount + drained.files.count)
            for dir in firstPage.dirs {
                level.append(
                    TreeNode(
                        nodeID: .dir(dir.id),
                        path: dir.path,
                        name: dir.name,
                        depth: depth,
                        kind: .directory(
                            childDirCount: Int(dir.childDirCount),
                            childFileCount: Int(dir.childFileCount))))
            }
            for summary in firstPage.files.items + drained.files {
                let state: FileTreeFileState
                if let existing = self.fileStateByPath[summary.path] {
                    state = existing
                } else if let buffered =
                    self.pendingSummaryOverlay[token]?
                        .removeValue(forKey: summary.path)
                {
                    // A save landed for this later-page file mid-drain: the
                    // buffered newest summary wins over the page snapshot.
                    state = FileTreeFileState(summary: buffered)
                } else {
                    state = FileTreeFileState(summary: summary)
                }
                level.append(
                    TreeNode(
                        nodeID: .file(path: summary.path),
                        path: summary.path,
                        name: summary.name,
                        depth: depth,
                        kind: .file(state)))
            }
            if parentKey == Self.rootFetchKey {
                self.replaceRootLevel(
                    with: level, isCompleteLevel: drained.isComplete)
            } else {
                self.replaceChildLevel(
                    level, for: parentKey, parentPath: parentPath,
                    isCompleteLevel: drained.isComplete)
            }
        }
        levelDrainTasksForTesting[parentKey] = task
    }

    /// Exact path → the one observable owned by its materialized row. Rich
    /// metadata refreshes use this index and never mutate a published level.
    private var fileStateByPath: [String: FileTreeFileState] = [:]
    private(set) var summaryReplacementLookupCountForTesting = 0

    private func clearMaterializedLevels() {
        rootLevel = []
        children = [:]
        fileStateByPath = [:]
        parentKeyByPath = [:]
        presentationByParent = [:]
        parentPathByKey = [:]
        completeLevelByKey = [:]
        levelDrainTokens = [:]
        levelDrainTasksForTesting = [:]
        pendingSummaryOverlay = [:]
        if treePresentation != SidebarTreePresentation() {
            treePresentation = SidebarTreePresentation()
        }
    }

    private func replaceRootLevel(with level: [TreeNode], isCompleteLevel: Bool = true) {
        removeFileStates(in: rootLevel)
        let organizedResult = organizedPresentation(
            level: level, parentPath: "")
        let organized = organizedResult.nodes
        var presentation = organizedResult.presentation
        // Round-5 finding 2: a paginated level omits real files, so absent
        // pinned paths are unknowable, not stale — never offer them for
        // pruning from a partial listing.
        if !isCompleteLevel {
            presentation.stalePinnedPaths = []
        }
        completeLevelByKey[Self.rootFetchKey] = isCompleteLevel
        rootLevel = organized
        presentationByParent[Self.rootFetchKey] = presentation
        parentPathByKey[Self.rootFetchKey] = ""
        indexFileStates(in: organized, parentKey: Self.rootFetchKey)
        rebuildMergedPresentation()
        reportStalePins(presentation, folder: "")
    }

    private func replaceChildLevel(
        _ level: [TreeNode]?, for parent: NodeID, parentPath: String?,
        isCompleteLevel: Bool = true
    ) {
        if let oldLevel = children[parent] {
            removeFileStates(in: oldLevel)
        }
        guard let level, let parentPath else {
            children[parent] = level
            presentationByParent[parent] = nil
            parentPathByKey[parent] = nil
            completeLevelByKey[parent] = nil
            if let revoked = levelDrainTokens[parent] {
                pendingSummaryOverlay[revoked] = nil
            }
            levelDrainTokens[parent] = nil
            levelDrainTasksForTesting[parent] = nil
            rebuildMergedPresentation()
            return
        }
        let organizedResult = organizedPresentation(
            level: level, parentPath: parentPath)
        let organized = organizedResult.nodes
        var presentation = organizedResult.presentation
        if !isCompleteLevel {
            presentation.stalePinnedPaths = []
        }
        completeLevelByKey[parent] = isCompleteLevel
        children[parent] = organized
        presentationByParent[parent] = presentation
        parentPathByKey[parent] = parentPath
        indexFileStates(in: organized, parentKey: parent)
        rebuildMergedPresentation()
        reportStalePins(presentation, folder: parentPath)
    }

    private func removeFileStates(in level: [TreeNode]) {
        for node in level {
            guard case let .file(state) = node.kind,
                fileStateByPath[node.path] === state
            else { continue }
            fileStateByPath[node.path] = nil
            parentKeyByPath[node.path] = nil
        }
    }

    private func indexFileStates(in level: [TreeNode], parentKey: NodeID) {
        for node in level {
            guard case let .file(state) = node.kind else { continue }
            fileStateByPath[node.path] = state
            parentKeyByPath[node.path] = parentKey
        }
    }

    private func clearChildLevels() {
        for level in children.values {
            removeFileStates(in: level)
        }
        children = [:]
        for (key, revoked) in levelDrainTokens where key != Self.rootFetchKey {
            pendingSummaryOverlay[revoked] = nil
        }
        levelDrainTokens = levelDrainTokens.filter {
            $0.key == Self.rootFetchKey
        }
        levelDrainTasksForTesting = levelDrainTasksForTesting.filter {
            $0.key == Self.rootFetchKey
        }
    }

    /// Test seam: bind to an explicit fetcher instead of a live session. The
    /// tree behaves identically (root loads immediately); only the source of
    /// `DirListing`s differs. `session` stays nil, so no FFI is touched.
    func bindForTesting(
        fetcher: @escaping (String) throws -> DirListing,
        restoringExpandedDirPaths: [String] = []
    ) {
        bindForTesting(
            pagedFetcher: { parentPath, cursor in
                guard cursor == nil else { throw LevelPageUnavailable() }
                return try fetcher(parentPath)
            },
            restoringExpandedDirPaths: restoringExpandedDirPaths)
    }

    func bindForTesting(
        pagedFetcher: @escaping (String, String?) throws -> DirListing,
        restoringExpandedDirPaths: [String] = []
    ) {
        self.session = nil
        sessionIdentity = nil
        self.fetcher = pagedFetcher
        clearMaterializedLevels()
        expanded = []
        pendingExpandedPaths = Set(restoringExpandedDirPaths)
        expansionRecency = restoringExpandedDirPaths
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
    /// follow-up, not a correctness gap for realistic vaults. `nonisolated`
    /// (immutable constant, the `rootFetchKey` precedent) so the detached
    /// duplicate task (#853) can share the same level-listing bound.
    nonisolated static let levelPageLimit: UInt32 = 5000

    enum FetchState: Equatable {
        case loading
        /// Fetch failed; `message` is a specific, user-facing reason.
        case failed(message: String)
    }

    // MARK: - Vault binding

    /// Point the tree at a session (or nil to clear). Resets all cached state
    /// and loads the root level. `FileTreeSidebar` calls this only on vault
    /// change, so re-entrancy on the same session isn't a concern here.
    ///
    /// #873: `restoringExpandedDirPaths` rehydrates the persisted expansion
    /// state instead of resetting to []. The whole set is adopted up front;
    /// the post-fetch cascade in `loadRoot`/`loadChildren` then materializes
    /// exactly the reachable expanded chains (an id under a collapsed parent
    /// stays dormant, exactly like an in-session collapse — re-expanding the
    /// parent reveals it expanded). Ids that no longer exist match no node
    /// and cost nothing; the persistence cap bounds any accumulation.
    func bind(to session: VaultSession?, restoringExpandedDirPaths: [String] = []) {
        self.session = session
        sessionIdentity = session.map(ObjectIdentifier.init)
        clearMaterializedLevels()
        expanded = []
        pendingExpandedPaths = Set(restoringExpandedDirPaths)
        expansionRecency = restoringExpandedDirPaths
        fetchState = [:]
        guard let session else {
            fetcher = nil
            expanded = []
            pendingExpandedPaths = []
            expansionRecency = []
            return
        }
        // Route level fetches through the session. `Paging` with a nil cursor
        // and a generous limit takes the whole level in one call (see
        // `levelPageLimit`).
        fetcher = { parentPath, cursor in
            try session.listDirChildren(
                parentPath: parentPath,
                paging: Paging(cursor: cursor, limit: Self.levelPageLimit))
        }
        loadRoot()
    }

    // MARK: - Level fetching

    /// (Re)fetch the root level. Sets `.loading` on the root sentinel while in
    /// flight so the view can show a spinner if the root is slow.
    func loadRoot() {
        guard let fetcher else { return }
        // A fresh fetch of this level supersedes any prior in-flight drain
        // for it (level-scoped revocation, round-16): without this, a
        // complete new first page would leave the old token current and let
        // a stale continuation publish over the reload.
        if let revoked = levelDrainTokens[Self.rootFetchKey] {
            pendingSummaryOverlay[revoked] = nil
        }
        levelDrainTokens[Self.rootFetchKey] = nil
        levelDrainTasksForTesting[Self.rootFetchKey] = nil
        fetchState[Self.rootFetchKey] = .loading
        do {
            let firstPage = try fetcher("", nil)
            let isComplete = firstPage.files.nextCursor == nil
            replaceRootLevel(
                with: Self.nodes(from: firstPage, depth: 0),
                isCompleteLevel: isComplete)
            if !isComplete {
                scheduleContinuationDrain(
                    parentKey: Self.rootFetchKey, parentPath: "", depth: 0,
                    firstPage: firstPage)
            }
            fetchState[Self.rootFetchKey] = nil
            adoptPendingExpansions(in: rootLevel)
            materializeExpandedChildren(in: rootLevel)
        } catch {
            replaceRootLevel(with: [])
            fetchState[Self.rootFetchKey] = .failed(message: Self.message(for: error))
        }
    }

    /// Fetch a directory's children into the cache. No-op if already cached
    /// (call `treeInvalidation` first to force a refetch). Sets `.loading`
    /// while in flight and clears it (or records `.failed`) on completion.
    func loadChildren(of node: TreeNode) {
        guard case .directory = node.kind else { return }
        guard let fetcher else { return }
        if children[node.nodeID] != nil {
            // Already cached — unless a continuation failure left the level
            // partial with an inline error, in which case Retry refetches
            // the whole level from page one (round-15 finding 2).
            guard case .failed = fetchState[node.nodeID] else { return }
            replaceChildLevel(nil, for: node.nodeID, parentPath: nil)
        }
        if let revoked = levelDrainTokens[node.nodeID] {
            pendingSummaryOverlay[revoked] = nil
        }
        levelDrainTokens[node.nodeID] = nil
        levelDrainTasksForTesting[node.nodeID] = nil
        fetchState[node.nodeID] = .loading
        do {
            let firstPage = try fetcher(node.path, nil)
            let isComplete = firstPage.files.nextCursor == nil
            let level = Self.nodes(from: firstPage, depth: node.depth + 1)
            replaceChildLevel(
                level, for: node.nodeID, parentPath: node.path,
                isCompleteLevel: isComplete)
            if !isComplete {
                scheduleContinuationDrain(
                    parentKey: node.nodeID, parentPath: node.path,
                    depth: node.depth + 1, firstPage: firstPage)
            }
            fetchState[node.nodeID] = nil
            adoptPendingExpansions(in: level)
            materializeExpandedChildren(in: level)
        } catch {
            fetchState[node.nodeID] = .failed(message: Self.message(for: error))
        }
    }

    /// After a level lands, fetch the children of any of its directories
    /// that are already in `expanded` but not yet materialized (#873). This
    /// is what makes the restored (and the in-session collapsed-then-
    /// re-expanded) expansion state lazy-safe: a persisted chain
    /// root → A → A/B materializes exactly A then A/B, one level per fetch,
    /// while an id whose parent stays collapsed costs nothing. Recursion
    /// depth is bounded by the expanded set (≤ the persistence cap) and
    /// paths form a tree, so no cycles. Cached levels no-op in
    /// `loadChildren`, so re-walks (rescans, re-expands) don't refetch.
    private func materializeExpandedChildren(in level: [TreeNode]) {
        for child in level
        where child.isDirectory
            && expanded.contains(child.nodeID)
            && children[child.nodeID] == nil
        {
            loadChildren(of: child)
        }
    }

    /// Persisted-expansion staging (#873, Codex round 2): paths, not
    /// rowids. `expanded` holds ONLY materialized nodes; anything not
    /// yet (or no longer) materialized lives here as a vault-relative
    /// dir path. Landing levels promote pending paths into `expanded`;
    /// invalidation DEMOTES cached expanded subtrees back to paths.
    /// This kills the rowid-reuse hazard outright (SQLite INTEGER
    /// PRIMARY KEY reuses ids after delete — probe-proven): a deleted
    /// folder's entry survives only as a path that nothing ever
    /// re-materializes, and a recycled id can't inherit expansion.
    var pendingExpandedPaths: Set<String> = []

    /// Expansion RECENCY ledger (Codex round 3): ordered oldest→newest,
    /// deduped; the persisted array preserves this order so the 500-cap
    /// evicts the OLDEST expansions (a lexicographic sort evicted the
    /// newest — the exact keep-newest regression the id era already
    /// fixed once). Expand appends/moves-to-end; collapse removes;
    /// demotion keeps position (still logically expanded).
    private(set) var expansionRecency: [String] = []

    /// The persistence payload: the recency ledger filtered to paths
    /// that are still live (materialized-expanded or pending).
    var expandedDirPaths: [String] {
        let materialized = Set(expanded.compactMap { id -> String? in
            guard case .dir = id else { return nil }
            return node(for: id)?.path
        })
        let live = materialized.union(pendingExpandedPaths)
        return expansionRecency.filter { live.contains($0) }
    }

    private func recencyTouch(_ path: String) {
        expansionRecency.removeAll { $0 == path }
        expansionRecency.append(path)
    }

    /// Promote pending paths that just materialized in `level`.
    private func adoptPendingExpansions(in level: [TreeNode]) {
        guard !pendingExpandedPaths.isEmpty else { return }
        for row in level where row.isDirectory {
            if pendingExpandedPaths.remove(row.path) != nil {
                expanded.insert(row.nodeID)
            }
        }
    }

    /// Rewrite expansion bookkeeping after a rename/move: the exact old
    /// path and every descendant prefix follow the entity to its new
    /// path, in BOTH the pending set and the recency ledger — without
    /// this, a renamed expanded folder collapsed and its old path
    /// lingered as a persisted tombstone (Codex round 4).
    func remapExpansion(fromPrefix old: String, to new: String) {
        func remap(_ path: String) -> String? {
            if path == old { return new }
            if path.hasPrefix(old + "/") { return new + path.dropFirst(old.count) }
            return nil
        }
        pendingExpandedPaths = Set(pendingExpandedPaths.map { remap($0) ?? $0 })
        expansionRecency = expansionRecency.map { remap($0) ?? $0 }
        // Ordering (Codex round 5): the mutation's invalidation has
        // ALREADY reloaded the level synchronously — the new row landed
        // before this remap, so its adoption pass found nothing. Re-run
        // adoption over every loaded level so the remapped path
        // promotes (and its children fetch) immediately.
        reAdoptPendingExpansions()
    }

    /// Batch form: index every authoritative standing prefix once and transform
    /// the union of pending/recency paths once, independent of change count.
    func remapExpansions(
        using index: FileTreeSidebar.SelectionModel.KnownMoveIndex,
        componentVisits: inout Int
    ) {
        let paths = pendingExpandedPaths.union(expansionRecency)
        var mapped: [String: String] = [:]
        mapped.reserveCapacity(paths.count)
        for path in paths {
            mapped[path] = index.remappedPath(
                path, componentVisits: &componentVisits) ?? path
        }
        pendingExpandedPaths = Set(pendingExpandedPaths.map { mapped[$0] ?? $0 })
        var seen = Set<String>()
        expansionRecency = expansionRecency.compactMap { path in
            let next = mapped[path] ?? path
            return seen.insert(next).inserted ? next : nil
        }
        reAdoptPendingExpansions()
    }

    /// Re-run pending-path adoption + expanded-child materialization over
    /// all currently loaded levels. Values are snapshotted first —
    /// materialization appends NEW cache entries mid-sweep.
    private func reAdoptPendingExpansions() {
        guard !pendingExpandedPaths.isEmpty else { return }
        adoptPendingExpansions(in: rootLevel)
        materializeExpandedChildren(in: rootLevel)
        for level in Array(children.values) {
            adoptPendingExpansions(in: level)
            materializeExpandedChildren(in: level)
        }
    }

    /// Drop expansion bookkeeping for a deleted subtree — tombstones
    /// would otherwise sit in the pending set consuming cap slots.
    func removeExpansion(underPrefix prefix: String) {
        func hit(_ path: String) -> Bool {
            path == prefix || path.hasPrefix(prefix + "/")
        }
        pendingExpandedPaths = pendingExpandedPaths.filter { !hit($0) }
        expansionRecency.removeAll(where: hit)
    }

    /// Batch form: exact files remove only themselves; directories cover their
    /// component-bounded descendants. Each expansion path is looked up once.
    func removeExpansions(
        using index: FileTreeSidebar.SelectionModel.KnownRemovalIndex,
        componentVisits: inout Int
    ) {
        let paths = pendingExpandedPaths.union(expansionRecency)
        var removed = Set<String>()
        for path in paths where index.covers(
            path, componentVisits: &componentVisits) {
            removed.insert(path)
        }
        pendingExpandedPaths.subtract(removed)
        expansionRecency.removeAll { removed.contains($0) }
    }

    /// Drop the cached child levels (and fetch state) of every dir under
    /// `rows`, recursively. Partner of `demoteExpandedSubtree` on the
    /// targeted-invalidation path: without it, a recycled id could serve
    /// the DELETED folder's stale cached children on next expand.
    private func dropDescendantCaches(rows: [TreeNode]) {
        for row in rows where row.isDirectory {
            if let kids = children[row.nodeID] {
                dropDescendantCaches(rows: kids)
            }
            replaceChildLevel(nil, for: row.nodeID, parentPath: nil)
            fetchState[row.nodeID] = nil
        }
    }

    /// Demote every cached, expanded dir under `rows` (recursively) to a
    /// pending path — called before a cache drop so ids never outlive
    /// their materialization (the reuse guard), while surviving folders
    /// re-promote when the refetched level lands.
    private func demoteExpandedSubtree(rows: [TreeNode]) {
        for row in rows where row.isDirectory {
            if expanded.remove(row.nodeID) != nil {
                pendingExpandedPaths.insert(row.path)
            }
            if let kids = children[row.nodeID] {
                demoteExpandedSubtree(rows: kids)
            }
        }
    }

    // MARK: - Expand / collapse

    /// Expand a directory: mark it disclosed and fetch its children if this is
    /// the first expand (or it was invalidated). Cached levels re-expand
    /// without a fetch.
    func expand(_ node: TreeNode) {
        guard case .directory = node.kind else { return }
        expanded.insert(node.nodeID)
        recencyTouch(node.path)
        loadChildren(of: node)  // no-op when cached
    }

    /// Collapse a directory. The cached level is retained (re-expand is
    /// instant); `treeInvalidation` is what drops it.
    func collapse(_ node: TreeNode) {
        guard case .directory = node.kind else { return }
        expanded.remove(node.nodeID)
        expansionRecency.removeAll { $0 == node.path }
    }

    /// Toggle disclosure for a directory row (Space, Return when no selected
    /// file needs opening, or the disclosure action).
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
            // Demote BEFORE the drop (reuse guard — see pendingExpandedPaths):
            // surviving folders re-promote as their refetched levels land,
            // and materializeExpandedChildren then refetches each promoted
            // level in turn — level by level, lazily. (An id-keyed refetch
            // loop here would resolve RECYCLED ids to unrelated new folders
            // and eagerly fetch their children — Codex round 3.)
            demoteExpandedSubtree(rows: rootLevel)
            clearChildLevels()
            fetchState = fetchState.filter { $0.key == Self.rootFetchKey }
            loadRoot()
            return
        }
        // A single level changed. Demote the DESCENDANT expansion to paths
        // first (nested reuse guard — the parent itself stays expanded;
        // Codex round 3: without this, a recycled child id inherited the
        // deleted sibling's expansion), then drop the cache and refetch iff
        // the parent is disclosed.
        let oldRows = children[parent] ?? []
        demoteExpandedSubtree(rows: oldRows)
        dropDescendantCaches(rows: oldRows)
        replaceChildLevel(nil, for: parent, parentPath: nil)
        fetchState[parent] = nil
        if expanded.contains(parent), let node = node(for: parent) {
            loadChildren(of: node)
        }
    }

    /// Targeted root-level mutation refresh. It refetches only the root and
    /// retains unaffected expanded descendant caches. Directory ids whose path
    /// changed or disappeared are demoted and cleared after the refetch, so an
    /// SQLite id reused by a new root folder cannot inherit stale children.
    func rootLevelInvalidation() {
        let oldRoot = rootLevel
        loadRoot()
        let newPathByID = Dictionary(uniqueKeysWithValues: rootLevel.compactMap {
            node -> (NodeID, String)? in
            node.isDirectory ? (node.nodeID, node.path) : nil
        })
        for old in oldRoot where old.isDirectory
            && newPathByID[old.nodeID] != old.path
        {
            if expanded.remove(old.nodeID) != nil {
                pendingExpandedPaths.insert(old.path)
            }
            let oldChildren = children[old.nodeID] ?? []
            demoteExpandedSubtree(rows: oldChildren)
            dropDescendantCaches(rows: oldChildren)
            replaceChildLevel(nil, for: old.nodeID, parentPath: nil)
            fetchState[old.nodeID] = nil
        }
    }

    /// Explicit whole-tree reconcile used only for authoritative scans or
    /// reports whose touched levels are unknown.
    func authoritativeTreeInvalidation() {
        treeInvalidation(parent: nil)
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

    /// Replace enriched metadata in already-materialized file rows without an
    /// FFI call, sibling-level refetch, or whole-tree scan. Every summary does
    /// one path-index lookup and preserves the row's identity and depth.
    @discardableResult
    func replaceFileSummaries(_ summaries: [FileSummary]) -> Int {
        summaryReplacementLookupCountForTesting = 0
        guard !summaries.isEmpty else { return 0 }
        var replacementCount = 0
        var affectedParents: Set<NodeID> = []

        for summary in summaries {
            summaryReplacementLookupCountForTesting += 1
            guard let state = fileStateByPath[summary.path] else {
                // Round-19 finding 2: while a drain is in flight this path
                // may be a later-page row that just hasn't landed — buffer
                // the newest summary under every ACTIVE drain token so the
                // owning landing overlays it (round-20 finding 2).
                for token in levelDrainTokens.values {
                    if pendingSummaryOverlay[token, default: [:]].count
                        < Self.pendingSummaryOverlayCap
                    {
                        pendingSummaryOverlay[token, default: [:]][summary.path] =
                            summary
                    }
                }
                continue
            }
            let previous = state.summary
            if state.replace(with: summary) {
                replacementCount += 1
                // A save can move a row under the active sort (mtime, an
                // authored title or created date). Re-sort only the levels
                // that contain a key-relevant change; the reorganize itself
                // publishes nothing when the order is already correct.
                if Self.organizationKeyChanged(previous, summary),
                    let parent = parentKeyByPath[summary.path]
                {
                    affectedParents.insert(parent)
                }
            }
        }
        if !affectedParents.isEmpty {
            for parent in affectedParents {
                reorganizeLevel(key: parent)
            }
            rebuildMergedPresentation()
        }
        return replacementCount
    }

    @discardableResult
    func replaceFileSummary(_ summary: FileSummary) -> Bool {
        replaceFileSummaries([summary]) == 1
    }

    /// Current rich summary for a materialized file. This reads the same keyed
    /// state the row observes and never scans a level.
    func fileSummary(forPath path: String) -> FileSummary? {
        fileStateByPath[path]?.summary
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

    /// FL3-4.1: collapse every materialized folder except the ancestor
    /// chain of `path` (the current selection stays revealed, so
    /// VoiceOver focus cannot land on a vanished row).
    func collapseAllPreservingAncestors(ofPath path: String?) {
        var keep: Set<String> = []
        if let path {
            let components = path.split(separator: "/").map(String.init)
            var prefix = ""
            for component in components.dropLast() {
                prefix = prefix.isEmpty ? component : "\(prefix)/\(component)"
                keep.insert(prefix)
            }
        }
        let materialized =
            (rootLevel + children.values.flatMap { $0 }).filter(\.isDirectory)
        for node in materialized where !keep.contains(node.path) {
            collapse(node)
        }
    }

    /// FL3-4.1: expand every already-materialized folder. Expanding an
    /// unfetched folder fetches its level — exactly one level deeper than
    /// what was loaded — and the newly fetched levels' own folders are NOT
    /// expanded, so a 10k-vault full expansion cannot cascade.
    func expandLoadedLevels() {
        let materialized =
            (rootLevel + children.values.flatMap { $0 }).filter(\.isDirectory)
        for node in materialized {
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
                    kind: .file(FileTreeFileState(summary: file))
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
    /// Immutable device-local row presentation supplied by the assembly layer.
    /// A default preserves existing previews/tests and the shipped U2 density.
    let rowPreferences: SidebarRowPreferencesSnapshot
    /// FL-09 (#663): the top-pinned filter's state machine, observed
    /// directly (it is its own ObservableObject, not derived AppState
    /// state) so activation/results/errors re-render this view.
    @ObservedObject var filterModel: SidebarFilterModel

    @MainActor
    init(
        rowPreferences: SidebarRowPreferencesSnapshot = .defaults,
        filterModel: SidebarFilterModel? = nil
    ) {
        self.rowPreferences = rowPreferences
        self.filterModel = filterModel ?? SidebarFilterModel()
    }

    /// The tree model. `@StateObject` so it survives view-body churn; rebound
    /// to the live session on vault change.
    @StateObject private var tree = FileTreeViewModel()
    @State private var didAnnounceCount = false
    /// One sidebar-scoped clock shared by every visible relative-date row.
    /// Absolute dates never start the task; no row owns a timer.
    @State private var sidebarNow = Date()
    nonisolated static let relativeDateRefreshInterval: Duration = .seconds(60)
    nonisolated static let recoveryActionMinimumHeight =
        Tokens.Spacing.lg + Tokens.Spacing.xs
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
    /// FL-03: semantic focus, the selected identities + path snapshots, and the
    /// independent fixed range anchor now live in one pure value model. The
    /// native `List` binding remains a local mirror because its mutation edge is
    /// the post-update point where #448-safe open/AppState effects run.
    @State private var selectionModel = SidebarSelectionModel<RowID>()
    /// FL-09 (#663): filter-surface focus/selection. The overlay list is
    /// its own focus model (one result list, one focus model — spec
    /// rule 2); the tree's selection state above stays untouched while
    /// the overlay is shown, which is what "prior expansion/selection
    /// intact" on Esc means.
    @FocusState private var filterFieldFocused: Bool
    @FocusState private var filterResultsFocused: Bool
    @State private var filterListSelection: String?
    /// #852: one-shot suppression consumed by `.onChange(of: listSelection)`. A
    /// ⌘/⇧ multi-select click moves the focus (`listSelection`) but must NOT run
    /// the open path — the gesture decides open/no-open itself (a live ⌘ during
    /// the onChange would otherwise mis-route the focus move to a new tab). Same
    /// one-shot shape as `suppressOpenForPostMutationFocus`.
    @State private var suppressOpenForSelectionChange = false
    /// One-shot gate armed when `selectedFilePath` mirrors a programmatic open
    /// onto the List. The originating surface owns its announcement, so the
    /// ensuing `listSelection` edge must not repeat it.
    @State private var selectionAnnouncementGate = SelectionAnnouncementGate()
    /// Distinguishes an App/semantic-model carrier write from a fresh native
    /// List user edge so selection revisions advance exactly once.
    @State private var selectionRevisionGate = SelectionRevisionGate()
    /// A destination level can briefly disappear while a batch Move refetches.
    /// Keep one restoration intent; a newer user focus edge supersedes it.
    @State private var pendingBatchFocus: PendingBatchFocus?
    /// Finder import selects its provider-ordered materialized results as one
    /// atomic union only after every row is present and all admission guards
    /// still match.
    @State private var pendingImportSelection: PendingImportSelection?
    /// Single create/rename/move focus waits for its refetched row; Delete owns
    /// a complete pre-invalidation fallback row and therefore does not depend
    /// on a cache that the mutation is about to drop.
    @State private var pendingPostMutationFocus: PendingPostMutationFocus?
    /// Keyboard focus on the file tree — gates the #418 selection announcements
    /// to list-driven changes only.
    @FocusState private var fileTreeFocused: Bool

    /// Native List selection changes carrier when the window/app becomes
    /// inactive even if this control remains the first responder.
    @Environment(\.controlActiveState) private var controlActiveState

    private var nativeSelectionIsActive: Bool {
        Self.selectionIsActive(
            treeFocused: fileTreeFocused,
            controlActiveState: controlActiveState)
    }

    /// Set for exactly one `listSelection` change when post-DELETE focus moves
    /// the highlight to a sibling: the deleted node's tab is now in the missing-
    /// file error state (U2-5), and re-selecting a sibling FILE must NOT open it
    /// (that would replace the error tab). Cleared the moment it's consumed.
    @State private var suppressOpenForPostMutationFocus = false

    // MARK: Type-select state (#850)

    /// The accumulating type-select prefix. Typing appends; ~1s of quiet
    /// (`typeSelectResetTask`) clears it, per the Finder/NSOutlineView staple.
    @State private var typeSelectBuffer = ""
    @State private var typeSelectResetTask: Task<Void, Never>?

    // MARK: Drag-and-drop feedback state (#851)

    /// Folder rows currently targeted by a live drag (per-row `isTargeted`
    /// mirror). Drives the selection-token drop wash and the spring-load
    /// timers.
    @State private var dropTargetedNodes: Set<NodeID> = []
    /// The root (list background) drop target's `isTargeted` mirror.
    @State private var rootDropTargeted = false
    /// Folders this drag session spring-opened (hover ≥ `springLoadDelay` on
    /// a collapsed folder). Re-collapsed at session end unless the drop
    /// landed inside them (`springFoldersToRecollapse`).
    @State private var springOpenedDirs: Set<NodeID> = []
    /// Pending spring-load timers, one per hovered collapsed folder.
    @State private var springLoadTasks: [NodeID: Task<Void, Never>] = [:]
    /// Fires when every drop target has gone quiet: SwiftUI has no drag-
    /// session-cancelled callback, so a short "nothing targeted anymore"
    /// grace window is how a drag that leaves the tree (or is cancelled)
    /// gets its spring-opened folders re-collapsed. Row-to-row moves flicker
    /// targets off for a frame or two — far under the window — so they
    /// don't end the session.
    @State private var dragSessionEndTask: Task<Void, Never>?
    /// One-shot drag settlement. Public import admission flips structural busy
    /// synchronously; the later busy observer must not settle the same accepted
    /// drag a second time with a nil destination.
    @State private var dragSessionHasSettled = true

    /// The `List` row id / selection key. A real tree node (`.node`) or a
    /// per-level loading/error placeholder derived from its parent level id so
    /// it's stable across renders. Only `.node(.file(_))` selections ever drive
    /// `selectedFilePath`.
    enum RowID: Hashable {
        case node(NodeID)
        case loading(parent: NodeID)
        case error(parent: NodeID)
        /// A nonselectable FL-06 section header (Pinned or a date bucket),
        /// stable per (containing folder, bucket key) across renders.
        case header(parentPath: String, key: String)
    }

    // MARK: - Multi-select model (#852)

    typealias SelectionModel = SidebarSelectionModel<RowID>
    typealias SelectionRow = SelectionModel.VisibleRow
    typealias SelectionClick = SelectionModel.PointerClick
    typealias SelectionOutcome = SelectionModel.PointerOutcome

    /// The only semantic-selection write seam. The mutation and the immutable
    /// AppState snapshot publish happen synchronously as one main-actor edge.
    static func mutateSelectionAndPublish(
        model: inout SelectionModel,
        capturedSessionIdentity: ObjectIdentifier?,
        visibleRows: [SelectionRow],
        appState: AppState,
        mutation: (inout SelectionModel) -> Void
    ) {
        mutation(&model)
        guard let capturedSessionIdentity else { return }
        let snapshot = SidebarSelectionSnapshot.capture(
            sessionIdentity: capturedSessionIdentity,
            model: model,
            visibleRows: visibleRows)
        _ = appState.publishSidebarSelectionSnapshot(snapshot)
    }

    private func mutateSelectionAndPublish(
        visibleRows: [SelectionRow]? = nil,
        _ mutation: (inout SelectionModel) -> Void
    ) {
        Self.mutateSelectionAndPublish(
            model: &selectionModel,
            capturedSessionIdentity: tree.sessionIdentity,
            visibleRows: visibleRows ?? visibleSelectionRows,
            appState: appState,
            mutation: mutation)
    }

    /// Pure target capture for both contextual surfaces. A row already in the
    /// published selection retains that complete frozen batch; right-clicking
    /// or invoking VoiceOver on any other row creates a one-row snapshot
    /// without changing the tree's visible or semantic selection.
    static func sidebarRowActionProjection(
        surface: SidebarActionSurface,
        row: SidebarSelectionItem,
        publishedSnapshot: SidebarSelectionSnapshot,
        structuralMutationDisabledReason: String?,
        actionDisabledReasons: [String: String]
    ) -> (
        targetSnapshot: SidebarSelectionSnapshot,
        evaluations: [SidebarActionEvaluation],
        openEvaluation: SidebarActionEvaluation?
    ) {
        let targetSnapshot: SidebarSelectionSnapshot
        if publishedSnapshot.items.contains(where: {
            $0.path == row.path && $0.isDirectory == row.isDirectory
        }) {
            targetSnapshot = publishedSnapshot
        } else {
            let parent = (row.path as NSString).deletingLastPathComponent
            targetSnapshot = SidebarSelectionSnapshot(
                sessionIdentity: publishedSnapshot.sessionIdentity,
                items: [row],
                focusedPath: row.path,
                creationParent: row.isDirectory
                    ? row.path
                    : (parent == "." ? "" : parent),
                selectionRevision: publishedSnapshot.selectionRevision)
        }
        let evaluations = SidebarActionCatalog.project(
            surface: surface,
            snapshot: targetSnapshot,
            structuralMutationDisabledReason: structuralMutationDisabledReason,
            actionDisabledReasons: actionDisabledReasons)
        let openEvaluation = SidebarActionCatalog.evaluation(
            for: SlateCommandID.sidebarOpen,
            snapshot: targetSnapshot,
            structuralMutationDisabledReason: structuralMutationDisabledReason,
            actionDisabledReasons: actionDisabledReasons)
        return (
            targetSnapshot: targetSnapshot,
            evaluations: evaluations,
            openEvaluation: openEvaluation)
    }

    enum ReturnOpenDisposition: Equatable {
        case folderDisclosure
        case openSelection
    }

    /// Bare Return is an Open command only when the frozen semantic selection
    /// contains a file. Folder-only or empty captures fall through to the
    /// focused folder's existing disclosure toggle; mixed captures still try
    /// Open so the exact capability rejection is announced and consumed.
    static func returnOpenDisposition(
        for snapshot: SidebarSelectionSnapshot?
    ) -> ReturnOpenDisposition {
        snapshot?.items.contains(where: { !$0.isDirectory }) == true
            ? .openSelection : .folderDisclosure
    }

    struct FileRowOpenAccessibilityPresentation: Equatable {
        let intent: SidebarActionInvocationIntent?
        let hint: String
        let exposesButtonTrait: Bool
        let exposesDefaultAction: Bool
    }

    /// Describe the exact frozen Open target. A file row can represent the
    /// complete selected batch, so singular copy is truthful only for one item.
    static func fileRowAvailableOpenHint(
        targetCount: Int,
        idleGuidance: String,
        structuralDisabledReason: String?
    ) -> String {
        let primaryAction = targetCount == 1
            ? "Opens the note."
            : "Opens the selected files."
        return rowAccessibilityHint(
            primaryAction: primaryAction,
            idleHint: "\(primaryAction) \(idleGuidance)",
            structuralDisabledReason: structuralDisabledReason)
    }

    /// One retained Open evaluation owns the file row's role, default action,
    /// and hint. Unavailable Open never advertises an affirmative action; its
    /// exact current reason becomes the complete hint.
    static func fileRowOpenAccessibilityPresentation(
        openEvaluation: SidebarActionEvaluation?,
        availableHint: String,
        unavailableHint: String
    ) -> FileRowOpenAccessibilityPresentation {
        guard let intent = openEvaluation?.intent else {
            let reason = openEvaluation?.disabledReason
                ?? AppState.sidebarSelectionChangedReason
            return FileRowOpenAccessibilityPresentation(
                intent: nil,
                hint: unavailableHint.isEmpty
                    ? reason : "\(reason) \(unavailableHint)",
                exposesButtonTrait: false,
                exposesDefaultAction: false)
        }
        return FileRowOpenAccessibilityPresentation(
            intent: intent,
            hint: availableHint,
            exposesButtonTrait: true,
            exposesDefaultAction: true)
    }

    /// The modifiers themselves are conditional: a blocked Open cannot leave
    /// behind a dead default action or a misleading button role in VoiceOver.
    private struct FileRowOpenAccessibilityModifier: ViewModifier {
        let presentation: FileRowOpenAccessibilityPresentation
        let dispatch: (SidebarActionInvocationIntent) -> Void

        @ViewBuilder
        func body(content: Content) -> some View {
            if presentation.exposesButtonTrait,
                presentation.exposesDefaultAction,
                let openIntent = presentation.intent
            {
                content
                    .accessibilityAddTraits(.isButton)
                    .accessibilityAction(.default) {
                        dispatch(openIntent)
                    }
            } else {
                content
            }
        }
    }

    /// Keyboard-only adapter over the same frozen projection used by menus and
    /// accessibility surfaces. The caller owns revalidation errors so each
    /// event path can announce once and decide whether to consume the key.
    @discardableResult
    static func invokeSidebarKeyboardAction(
        id: String,
        projection: [SidebarActionEvaluation],
        dispatch: (SidebarActionInvocationIntent) throws -> Void
    ) throws -> Bool {
        guard let evaluation = projection.first(where: { $0.id == id }) else {
            return false
        }
        guard let intent = evaluation.intent else {
            throw CommandError.ActionFailed(
                message: evaluation.disabledReason
                    ?? AppState.sidebarSelectionChangedReason)
        }
        try dispatch(intent)
        return true
    }

    enum SelectionKeyAction: Equatable {
        case extend(SelectionModel.ArrowDirection)
        case selectAll
        case openSelected
    }

    struct KeyboardSelectionOutcome: Equatable {
        let model: SelectionModel
        let handled: Bool
        let changed: Bool
        let listSelection: RowID?
        let shouldMirrorListSelection: Bool
        let visibleSelectedCount: Int
    }

    static func selectionKeyAction(
        key: KeyEquivalent,
        modifiers: EventModifiers,
        fileTreeFocused: Bool,
        isRenaming: Bool
    ) -> SelectionKeyAction? {
        guard treeKeyInterceptionActive(
            fileTreeFocused: fileTreeFocused,
            isRenaming: isRenaming)
        else { return nil }
        let meaningfulModifiers = modifiers.subtracting(.capsLock)
        if meaningfulModifiers == [.shift] {
            if key == .upArrow { return .extend(.up) }
            if key == .downArrow { return .extend(.down) }
        }
        if meaningfulModifiers == [.command], key == "a" { return .selectAll }
        if meaningfulModifiers.isEmpty, key == .return { return .openSelected }
        return nil
    }

    static func keyboardSelectionOutcome(
        action: SelectionKeyAction,
        model: SelectionModel,
        currentListSelection: RowID?,
        visibleRows: [SelectionRow]
    ) -> KeyboardSelectionOutcome {
        var next = model
        let transition: SelectionModel.Transition
        switch action {
        case let .extend(direction):
            transition = next.extendSelection(direction, visibleRows: visibleRows)
        case .selectAll:
            transition = next.selectAll(visibleRows: visibleRows)
        case .openSelected:
            return KeyboardSelectionOutcome(
                model: model, handled: false, changed: false,
                listSelection: currentListSelection,
                shouldMirrorListSelection: false,
                visibleSelectedCount: model.selectedVisibleRows(in: visibleRows).count)
        }
        return KeyboardSelectionOutcome(
            model: next,
            handled: transition.handled,
            changed: transition.changed,
            listSelection: next.focused,
            shouldMirrorListSelection: transition.focusChanged
                && currentListSelection != next.focused,
            visibleSelectedCount: next.selectedVisibleRows(in: visibleRows).count)
    }

    /// One-shot announcement suppression with explicit consume semantics.
    /// The view stores this value in `@State`, arms it immediately before a
    /// programmatic `listSelection` assignment, and consumes it at the start of
    /// the resulting `.onChange` edge. Repeated arms are intentionally
    /// idempotent because SwiftUI may coalesce programmatic writes; consuming
    /// always clears the gate so later user input cannot inherit suppression.
    /// Keeping this as a value lets tests exercise that lifecycle without
    /// parsing `.onChange` source text.
    struct SelectionAnnouncementGate: Equatable {
        private(set) var isArmed = false

        mutating func arm() {
            isArmed = true
        }

        mutating func consume() -> Bool {
            defer { isArmed = false }
            return isArmed
        }
    }

    /// One expected programmatic/semantic carrier value. A same-value write may
    /// produce no SwiftUI edge; a later different user value consumes the stale
    /// gate without being misclassified as programmatic.
    struct SelectionRevisionGate: Equatable {
        private var isArmed = false
        private var expected: RowID?

        mutating func arm(for selection: RowID?) {
            isArmed = true
            expected = selection
        }

        mutating func consume(if selection: RowID?) -> Bool {
            guard isArmed else { return false }
            defer {
                isArmed = false
                expected = nil
            }
            return expected == selection
        }
    }

    /// Complete state transition for a programmatic selected-path mirror. The
    /// batch selection and anchor always collapse, even when the List already
    /// carries the same focus and therefore emits no selection-change edge.
    struct ProgrammaticSelectionOutcome: Equatable {
        let selection: Set<RowID>
        let anchor: RowID?
        let listSelection: RowID?
        let shouldMirrorListSelection: Bool
        let shouldSuppressAnnouncement: Bool
    }

    static func programmaticSelectionOutcome(
        currentListSelection: RowID?,
        mirroredSelection: RowID?
    ) -> ProgrammaticSelectionOutcome {
        let shouldMirror = currentListSelection != mirroredSelection
        return ProgrammaticSelectionOutcome(
            selection: mirroredSelection.map { [$0] } ?? [],
            anchor: mirroredSelection,
            listSelection: mirroredSelection,
            shouldMirrorListSelection: shouldMirror,
            shouldSuppressAnnouncement: shouldMirror)
    }

    /// Keep the row's still-available primary activation first while scoping
    /// the transient busy reason to file-management actions. The shared reason
    /// is appended exactly once so VoiceOver does not imply the whole row is
    /// disabled or repeat the same status in one hint.
    static func rowAccessibilityHint(
        primaryAction: String,
        idleHint: String,
        structuralDisabledReason: String?
    ) -> String {
        guard let reason = structuralDisabledReason else { return idleHint }
        return "\(primaryAction) File changes are unavailable. \(reason)"
    }

    /// Decode a `SelectionClick` from a live event's modifier flags. ⇧ wins over
    /// ⌘ (a ⇧⌘-click range-selects rather than toggling) — a deliberate
    /// simplification of Finder's "add range" so the two multi gestures stay
    /// distinct and testable. Static + pure (the `deleteCommandAllowed` pattern).
    static func selectionClick(from event: NSEvent?) -> SelectionClick {
        selectionClick(from: event?.modifierFlags ?? [])
    }

    /// AppKit row-bridge form. The down-event modifiers are retained by the
    /// click/drag state machine so a key change during the gesture cannot alter
    /// which selection transition lands on mouse-up.
    static func selectionClick(from flags: NSEvent.ModifierFlags) -> SelectionClick {
        if flags.contains(.shift) { return .range }
        if flags.contains(.command) { return .toggle }
        return .plain
    }

    /// Pure (#852): fold one pointer click into `(selection, anchor, focus)`.
    /// `order` is the flattened VISIBLE selectable-row order (used only for
    /// ⇧-range). Semantics mirror Finder/NSTableView:
    ///   - `.plain`  → selection = `[clicked]`; anchor = clicked; focus = clicked.
    ///   - `.toggle` → clicked ∈ selection ? remove : add. Anchor becomes clicked
    ///                 (a ⌘-click re-pivots the range). Focus = clicked when it
    ///                 stays selected, else the LAST still-selected row in visible
    ///                 order (nil once the set empties) — a sensible single-item
    ///                 command target after a removal.
    ///   - `.range`  → the inclusive span between the anchor and clicked over
    ///                 `order`; the anchor is UNCHANGED so successive ⇧-clicks
    ///                 grow/shrink from one pivot; focus = clicked. With no usable
    ///                 anchor (nil or off-list — e.g. the first gesture is a
    ///                 ⇧-click) it degrades to a plain selection of clicked and
    ///                 adopts it as the anchor.
    /// Static so it's regression-locked without a `List` (the `moveOutcome`
    /// pattern).
    static func applySelectionClick(
        order: [RowID], current: Set<RowID>, anchor: RowID?,
        clicked: RowID, click: SelectionClick
    ) -> SelectionOutcome {
        SelectionModel.pointerOutcome(
            order: order, current: current, anchor: anchor, clicked: clicked, click: click)
    }

    static func openSelectedPaths(
        from model: SelectionModel,
        visibleRows: [SelectionRow]
    ) -> [String] {
        openSelectionBatch(from: model, visibleRows: visibleRows).paths
    }

    typealias OpenSelectionBatch = SidebarOpenSelectionBatch

    static func openSelectionBatch(
        from model: SelectionModel,
        visibleRows: [SelectionRow]
    ) -> OpenSelectionBatch {
        let rows = model.selectedVisibleRows(in: visibleRows)
        guard rows.allSatisfy({ !$0.isDirectory }) else {
            return OpenSelectionBatch(paths: [], focusedPath: nil)
        }
        let focusedPath = rows.first { $0.identity == model.focused }?.path
        return OpenSelectionBatch(paths: rows.map(\.path), focusedPath: focusedPath)
    }

    typealias OpenSelectionRequest = SidebarOpenSelectionRequest
    typealias OpenSelectionDisposition = SidebarOpenSelectionDisposition

    static func openSelectionDisposition(
        batch: OpenSelectionBatch,
        sessionIdentity: ObjectIdentifier?
    ) -> OpenSelectionDisposition {
        guard !batch.paths.isEmpty, let sessionIdentity else { return .none }
        if batch.paths.count >= 10 {
            return .confirm(
                OpenSelectionRequest(sessionIdentity: sessionIdentity, batch: batch))
        }
        return .direct(batch)
    }

    static func resolvedOpenPaths(
        _ request: OpenSelectionRequest,
        confirmed: Bool,
        currentSessionIdentity: ObjectIdentifier?
    ) -> [String] {
        guard confirmed, request.sessionIdentity == currentSessionIdentity else { return [] }
        return request.batch.executionPaths
    }

    struct DragPayloadItem: Codable, Equatable {
        let path: String
        let isDirectory: Bool
    }

    private struct DecodedDragPayload {
        let items: [DragPayloadItem]
        let preferredFocusPath: String?
        let token: UUID?
    }

    private struct DragPayloadEnvelope: Codable {
        let version: Int
        let items: [DragPayloadItem]
        /// Optional keeps v1 payloads emitted before C2 backward-compatible.
        let preferredFocusPath: String?
        /// Present only on process-registered v2 payloads. The token is an
        /// opaque capability; private drop dispatch never trusts JSON alone.
        let token: UUID?
    }

    private struct RegisteredDragPayload {
        let payload: Data
        let originVaultIdentity: String?
        let originSession: WeakDragOriginSession?
        let registeredAt: Date
    }

    private final class WeakDragOriginSession {
        weak var value: AnyObject?

        init(_ value: AnyObject) {
            self.value = value
        }
    }

    /// AppKit pasteboard items can't express NSItemProvider's `.ownProcess`
    /// visibility, so a custom UTI is not proof that Slate emitted the bytes.
    /// Keep a bounded process-local capability registry and require an exact,
    /// one-shot match before any private payload can reach a move funnel.
    @MainActor
    private static var registeredDragPayloads: [UUID: RegisteredDragPayload] = [:]
    private static let registeredDragPayloadLifetime: TimeInterval = 5 * 60
    private static let registeredDragPayloadLimit = 256

    static func encodeDragPayload(
        _ items: [DragPayloadItem],
        preferredFocusPath: String? = nil
    ) -> Data? {
        guard dragPayloadItemsAreValid(items),
            preferredFocusPath.map({ focus in items.contains { $0.path == focus } }) ?? true
        else { return nil }
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.sortedKeys]
        return try? encoder.encode(
            DragPayloadEnvelope(
                version: 1,
                items: items,
                preferredFocusPath: preferredFocusPath,
                token: nil))
    }

    static func decodeDragPayload(_ data: Data) -> [DragPayloadItem]? {
        decodedDragPayload(data)?.items
    }

    private static func decodedDragPayload(_ data: Data) -> DecodedDragPayload? {
        guard let envelope = try? JSONDecoder().decode(DragPayloadEnvelope.self, from: data),
            (envelope.version == 1 && envelope.token == nil)
                || (envelope.version == 2 && envelope.token != nil),
            dragPayloadItemsAreValid(envelope.items),
            envelope.preferredFocusPath.map({ focus in
                envelope.items.contains { $0.path == focus }
            }) ?? true
        else { return nil }
        return DecodedDragPayload(
            items: envelope.items,
            preferredFocusPath: envelope.preferredFocusPath,
            token: envelope.token)
    }

    @MainActor
    private static func registerDragPayload(
        _ items: [DragPayloadItem],
        preferredFocusPath: String?,
        originVaultURL: URL?,
        originSession: AnyObject?
    ) -> Data? {
        guard dragPayloadItemsAreValid(items),
            preferredFocusPath.map({ focus in items.contains { $0.path == focus } }) ?? true
        else { return nil }

        let token = UUID()
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.sortedKeys]
        guard let payload = try? encoder.encode(
            DragPayloadEnvelope(
                version: 2,
                items: items,
                preferredFocusPath: preferredFocusPath,
                token: token))
        else { return nil }
        registeredDragPayloads[token] = RegisteredDragPayload(
            payload: payload,
            originVaultIdentity: originVaultURL.map(vaultIdentity(for:)),
            originSession: originSession.map(WeakDragOriginSession.init),
            registeredAt: Date())
        reapRegisteredDragPayloads()
        return payload
    }

    @MainActor
    private static func consumeRegisteredDragPayload(
        _ data: Data,
        currentVaultURL: URL?,
        currentSession: AnyObject
    ) -> DecodedDragPayload? {
        reapRegisteredDragPayloads()
        guard let decoded = decodedDragPayload(data),
            let token = decoded.token,
            let registered = registeredDragPayloads.removeValue(forKey: token),
            registered.payload == data,
            let originVaultIdentity = registered.originVaultIdentity,
            let currentVaultURL,
            originVaultIdentity == vaultIdentity(for: currentVaultURL),
            let originSession = registered.originSession,
            originSession.value === currentSession
        else { return nil }
        return decoded
    }

    @MainActor
    private static func reapRegisteredDragPayloads(now: Date = Date()) {
        let oldestAllowed = now.addingTimeInterval(-registeredDragPayloadLifetime)
        registeredDragPayloads = registeredDragPayloads.filter {
            $0.value.registeredAt >= oldestAllowed
        }
        let overflow = registeredDragPayloads.count - registeredDragPayloadLimit
        guard overflow > 0 else { return }
        let oldestTokens = registeredDragPayloads
            .sorted { $0.value.registeredAt < $1.value.registeredAt }
            .prefix(overflow)
            .map(\.key)
        for token in oldestTokens {
            registeredDragPayloads.removeValue(forKey: token)
        }
    }

    private static func vaultIdentity(for url: URL) -> String {
        url.standardizedFileURL.resolvingSymlinksInPath().path
    }

    private static func inferredVaultURL(
        from originFileURL: URL?,
        relativePath: String?
    ) -> URL? {
        guard var candidate = originFileURL?.standardizedFileURL,
            let relativePath,
            !relativePath.isEmpty
        else { return nil }
        let components = relativePath.split(separator: "/").map(String.init)
        for component in components.reversed() {
            guard candidate.lastPathComponent == component else { return nil }
            candidate.deleteLastPathComponent()
        }
        return candidate
    }

    private static func dragPayloadItemsAreValid(_ items: [DragPayloadItem]) -> Bool {
        guard !items.isEmpty else { return false }
        var paths = Set<String>()
        for item in items {
            let components = item.path.split(
                separator: "/", omittingEmptySubsequences: false)
            guard !item.path.isEmpty,
                !item.path.hasPrefix("/"),
                !item.path.hasSuffix("/"),
                !item.path.contains("\0"),
                components.allSatisfy({ !$0.isEmpty && $0 != "." && $0 != ".." }),
                paths.insert(item.path).inserted
            else { return false }
        }
        return true
    }

    static func dragItems(
        for origin: SelectionRow,
        from model: SelectionModel,
        visibleRows: [SelectionRow]
    ) -> [DragPayloadItem] {
        let rows: [SelectionRow]
        if model.isSelected(origin.identity, currentPath: origin.path) {
            rows = model.selectedVisibleRows(in: visibleRows)
        } else {
            rows = [origin]
        }
        return rows.map {
            DragPayloadItem(path: $0.path, isDirectory: $0.isDirectory)
        }
    }

    static func dragPreviewCount(
        for origin: SelectionRow,
        from model: SelectionModel,
        visibleRows: [SelectionRow]
    ) -> Int {
        dragItems(for: origin, from: model, visibleRows: visibleRows).count
    }

    /// Pure (#852, Codex finding 4): the selected rows in LIVE visible-row order,
    /// PRE-deduplication, pruned to (a) rows still present in the visible tree
    /// and (b) rows whose CURRENT path still matches the `snapshot` taken at
    /// selection time. (b) drops a REUSED SQLite dir id now pointing at an
    /// UNRELATED folder after a rescan — that folder was never selected, so it
    /// must not paint the fill / carry `.isSelected` / become a batch target.
    /// This is the "what's VISUALLY selected" set: multi-select MODE
    /// (`hasMultiSelection`) and the "N items selected" announcement use its
    /// COUNT (a folder + a child inside it are TWO visible selected rows →
    /// multi, Codex finding 3).
    static func prunedSelection(
        visibleOrder: [(rowID: RowID, path: String, isDirectory: Bool)],
        selection: Set<RowID>,
        snapshot: [RowID: String]
    ) -> [AppState.TreeSelection] {
        let model = SelectionModel(
            selected: selection,
            selectionPathSnapshots: snapshot)
        return model.selectedVisibleRows(in: visibleOrder.map {
            SelectionRow(
                identity: $0.rowID, path: $0.path,
                isDirectory: $0.isDirectory, isMarkdown: false)
        }).map {
            AppState.TreeSelection(path: $0.path, isDirectory: $0.isDirectory)
        }
    }

    /// Pure (#852, Codex finding 4): the batch OPERATION targets —
    /// `prunedSelection` deduplicated to top-level entries (a folder + a
    /// descendant collapse to just the folder). Deterministic order. The
    /// "Move N items" menu label uses its COUNT — distinct from the pre-dedup
    /// mode count above (Codex finding 3: a folder+child reads "2 items
    /// selected" but "Move 1 Item").
    static func batchTargets(
        visibleOrder: [(rowID: RowID, path: String, isDirectory: Bool)],
        selection: Set<RowID>,
        snapshot: [RowID: String]
    ) -> [AppState.TreeSelection] {
        let model = SelectionModel(
            selected: selection,
            selectionPathSnapshots: snapshot)
        return model.topLevelOperationRows(in: visibleOrder.map {
            SelectionRow(
                identity: $0.rowID, path: $0.path,
                isDirectory: $0.isDirectory, isMarkdown: false)
        }).map {
            AppState.TreeSelection(path: $0.path, isDirectory: $0.isDirectory)
        }
    }

    /// Pure (#852, Codex finding 4): reconcile a selection after a tree change —
    /// drop any selected RowID whose id now resolves to a DIFFERENT path than its
    /// `snapshot` (a reused dir id repointed at an unrelated folder). An id that
    /// doesn't resolve at all (a level mid-refetch) is KEPT (transient), so a
    /// benign rescan never clears a still-valid selection. Returns survivors +
    /// the pruned snapshot; the caller re-anchors if the anchor was dropped.
    static func reconcileSelection(
        selection: Set<RowID>, snapshot: [RowID: String], resolve: (RowID) -> String?
    ) -> (selection: Set<RowID>, snapshot: [RowID: String]) {
        var model = SelectionModel(
            selected: selection,
            selectionPathSnapshots: snapshot)
        model.reconcile(visibleRows: [], resolveCurrentPath: resolve)
        return (model.selected, model.selectionPathSnapshots)
    }

    /// Pure (#852, Codex round-5): whether a range ANCHOR survives a tree-change
    /// reconcile. Mirrors the per-row rule: it survives when there's no snapshot
    /// to check, or it can't currently be resolved (transient, mid-refetch), or
    /// it still resolves to its snapshot path. It is CLEARED only on a CONFIRMED
    /// mismatch — a reused id now resolving to a DIFFERENT path than the one the
    /// anchor was set at. Static → directly unit-tested.
    static func anchorSurvivesReconcile(snapshot: String?, resolved: String?) -> Bool {
        SelectionModel.snapshotSurvives(snapshot: snapshot, resolved: resolved)
    }

    struct BatchMoveFocusPlan: Equatable {
        let path: String?
        let shouldRevealMovedAncestry: Bool
    }

    struct PendingPostMutationFocus: Equatable {
        let targetPath: String?
        let capturedDeleteRow: SelectionRow?
        let suppressOpen: Bool
        /// True after the immutable Delete fallback has already been installed
        /// and published while its invalidated level is unavailable. Later
        /// loading/error edges must guard that snapshot, not publish it again.
        let installedDeleteFallback: Bool
        /// Ownership prevents a late tree edge from carrying a fallback across
        /// either a newer structural landing or a replaced vault session.
        let ownerSessionIdentity: ObjectIdentifier?
        let ownerMutationToken: Int?

        init(
            targetPath: String?,
            capturedDeleteRow: SelectionRow?,
            suppressOpen: Bool,
            installedDeleteFallback: Bool = false,
            ownerSessionIdentity: ObjectIdentifier? = nil,
            ownerMutationToken: Int? = nil
        ) {
            self.targetPath = targetPath
            self.capturedDeleteRow = capturedDeleteRow
            self.suppressOpen = suppressOpen
            self.installedDeleteFallback = installedDeleteFallback
            self.ownerSessionIdentity = ownerSessionIdentity
            self.ownerMutationToken = ownerMutationToken
        }
    }

    enum PendingPostMutationRestoreOutcome: Equatable {
        case wait
        case installed(SelectionRow)
        case clearedGuard
        case cancelAndReconcile
    }

    static func postMutationFocusRow(
        for pending: PendingPostMutationFocus,
        visibleRows: [SelectionRow]
    ) -> SelectionRow? {
        if let capturedDeleteRow = pending.capturedDeleteRow {
            return capturedDeleteRow
        }
        guard let targetPath = pending.targetPath else { return nil }
        return visibleRows.first(where: { $0.path == targetPath })
    }

    struct PendingBatchFocus: Equatable {
        let plan: BatchMoveFocusPlan
        let expectedFocusedPath: String?
        let deferredModel: SelectionModel?

        init(
            plan: BatchMoveFocusPlan,
            expectedFocusedPath: String?,
            deferredModel: SelectionModel? = nil
        ) {
            self.plan = plan
            self.expectedFocusedPath = expectedFocusedPath
            self.deferredModel = deferredModel
        }
    }

    struct PendingImportSelection: Equatable {
        let paths: [String]
        let expectedSelectionRevision: UInt64
        let deferredModel: SelectionModel
        let ownerSessionIdentity: ObjectIdentifier
        let ownerStructuralMutationToken: Int
        let treeMutationToken: Int
    }

    enum PendingBatchFocusDisposition: Equatable {
        case wait
        case restore
        case cancel
    }

    static func pendingBatchFocusDisposition(
        _ pending: PendingBatchFocus,
        currentFocusedPath: String?,
        targetIsMaterialized: Bool
    ) -> PendingBatchFocusDisposition {
        guard currentFocusedPath == pending.expectedFocusedPath else {
            return .cancel
        }
        return targetIsMaterialized ? .restore : .wait
    }

    static func deferredBatchSelectionModel(
        from model: SelectionModel,
        using index: SelectionModel.KnownMoveIndex,
        componentVisits: inout Int
    ) -> SelectionModel {
        var result = model
        result.remapKnownMoves(
            using: index,
            identityForRemappedPath: remappedSelectionIdentity,
            componentVisits: &componentVisits)
        return result
    }

    /// Production selection transition for a batch move. An unavailable final
    /// row returns a fully prepared pending value without assigning `model` or
    /// publishing an AppState snapshot. A materialized row installs remap +
    /// final focus through one publication edge.
    static func applyOrDeferBatchMoveSelection(
        plan: BatchMoveFocusPlan?,
        moveIndex: SelectionModel.KnownMoveIndex?,
        targetRow: SelectionRow?,
        expectedFocusedPath: String?,
        model: inout SelectionModel,
        capturedSessionIdentity: ObjectIdentifier?,
        visibleRows: [SelectionRow],
        appState: AppState,
        componentVisits: inout Int
    ) -> PendingBatchFocus? {
        let deferredModel: SelectionModel
        if let moveIndex {
            deferredModel = deferredBatchSelectionModel(
                from: model,
                using: moveIndex,
                componentVisits: &componentVisits)
        } else {
            deferredModel = model
        }
        if let plan, plan.path != nil, targetRow == nil {
            return PendingBatchFocus(
                plan: plan,
                expectedFocusedPath: expectedFocusedPath,
                deferredModel: deferredModel)
        }
        mutateSelectionAndPublish(
            model: &model,
            capturedSessionIdentity: capturedSessionIdentity,
            visibleRows: visibleRows,
            appState: appState
        ) {
            $0 = deferredModel
            if let targetRow {
                $0.focusAfterStructuralMutation(targetRow)
            }
        }
        return nil
    }

    /// Install a previously deferred batch move after its final focus row has
    /// materialized. The complete remap and final focus publish exactly once.
    @discardableResult
    static func installPendingBatchFocus(
        _ pending: PendingBatchFocus,
        targetRow: SelectionRow,
        model: inout SelectionModel,
        capturedSessionIdentity: ObjectIdentifier?,
        visibleRows: [SelectionRow],
        appState: AppState
    ) -> Bool {
        mutateSelectionAndPublish(
            model: &model,
            capturedSessionIdentity: capturedSessionIdentity,
            visibleRows: visibleRows,
            appState: appState
        ) {
            if let deferredModel = pending.deferredModel {
                $0 = deferredModel
            }
            $0.focusAfterStructuralMutation(targetRow)
        }
        return true
    }

    /// Install a create/rename/move destination or a Delete fallback through
    /// the same production publication seam used by the SwiftUI restore path.
    /// A captured Delete row is appended only for this immutable publication
    /// when its invalidated child level is temporarily unavailable.
    static func installPendingPostMutationFocus(
        _ pending: PendingPostMutationFocus,
        model: inout SelectionModel,
        capturedSessionIdentity: ObjectIdentifier?,
        visibleRows: [SelectionRow],
        appState: AppState
    ) -> SelectionRow? {
        guard let targetRow = postMutationFocusRow(
            for: pending,
            visibleRows: visibleRows)
        else { return nil }
        var publicationRows = visibleRows
        if !publicationRows.contains(targetRow) {
            publicationRows.append(targetRow)
        }
        mutateSelectionAndPublish(
            model: &model,
            capturedSessionIdentity: capturedSessionIdentity,
            visibleRows: publicationRows,
            appState: appState
        ) {
            $0.focusAfterStructuralMutation(targetRow)
        }
        return targetRow
    }

    /// Retain only the already-published Delete fallback whose exact row is
    /// still missing from the invalidated level. Create/rename/move completes
    /// immediately once installed, as does a Delete whose fallback never left
    /// the visible tree (for example, the parent folder).
    static func pendingPostMutationFocusAfterInstall(
        _ pending: PendingPostMutationFocus,
        installedRow: SelectionRow,
        visibleRows: [SelectionRow]
    ) -> PendingPostMutationFocus? {
        guard pending.capturedDeleteRow == installedRow,
            !visibleRows.contains(installedRow)
        else { return nil }
        return PendingPostMutationFocus(
            targetPath: pending.targetPath,
            capturedDeleteRow: pending.capturedDeleteRow,
            suppressOpen: pending.suppressOpen,
            installedDeleteFallback: true,
            ownerSessionIdentity: pending.ownerSessionIdentity,
            ownerMutationToken: pending.ownerMutationToken)
    }

    /// Production transition for `.onChange(of: tree.visibleRows)`. An already
    /// published Delete fallback owns loading/error edges without mutating the
    /// model or AppState. Its exact rematerialization only clears the guard. A
    /// not-yet-installed create/rename/move uses the normal one-shot installer.
    static func restorePendingPostMutationSelection(
        pending: inout PendingPostMutationFocus?,
        model: inout SelectionModel,
        capturedSessionIdentity: ObjectIdentifier?,
        visibleRows: [SelectionRow],
        appState: AppState,
        currentSessionIdentity: ObjectIdentifier? = nil,
        currentMutationToken: Int? = nil,
        resolveCurrentRow: (RowID) -> SelectionRow?
    ) -> PendingPostMutationRestoreOutcome {
        guard let current = pending else { return .cancelAndReconcile }
        if let ownerSessionIdentity = current.ownerSessionIdentity,
            currentSessionIdentity != ownerSessionIdentity
        {
            pending = nil
            return .cancelAndReconcile
        }
        if let ownerMutationToken = current.ownerMutationToken,
            currentMutationToken != ownerMutationToken
        {
            pending = nil
            return .cancelAndReconcile
        }
        if current.installedDeleteFallback {
            guard let fallback = current.capturedDeleteRow else {
                pending = nil
                return .cancelAndReconcile
            }
            // A real user focus/selection edge supersedes this old structural
            // landing. Do not let a failed-refetch guard suppress unrelated
            // reconciliation after the semantic model has moved on.
            guard model.focused == fallback.identity,
                model.isSelected(fallback.identity, currentPath: fallback.path)
            else {
                pending = nil
                return .cancelAndReconcile
            }
            if visibleRows.contains(fallback) {
                pending = nil
                return .clearedGuard
            }
            // The old captured row has been replaced at the same path (new dir
            // id or refreshed metadata). It is not the immutable row we
            // published, so release to reconciliation rather than letting the
            // stale selection remain command-authoritative.
            if visibleRows.contains(where: { $0.path == fallback.path }) {
                pending = nil
                return .cancelAndReconcile
            }
            // The identity exists but is not the exact visible fallback: the
            // user may have collapsed its ancestor, or a stable dir id may now
            // resolve to another path. Release to generic reconciliation now;
            // waiting is reserved for a genuinely unmaterialized refetch.
            if resolveCurrentRow(fallback.identity) != nil {
                pending = nil
                return .cancelAndReconcile
            }
            return .wait
        }
        guard let installedRow = installPendingPostMutationFocus(
            current,
            model: &model,
            capturedSessionIdentity: capturedSessionIdentity,
            visibleRows: visibleRows,
            appState: appState)
        else { return .wait }
        pending = pendingPostMutationFocusAfterInstall(
            current,
            installedRow: installedRow,
            visibleRows: visibleRows)
        return .installed(installedRow)
    }

    static func cancelPendingPostMutationFocus(
        _ pending: inout PendingPostMutationFocus?
    ) {
        pending = nil
    }

    /// Publish the legacy single-item command mirror without erasing an
    /// installed Delete fallback solely because its invalidated row cannot yet
    /// be resolved. The pending guard already proved session, mutation, and
    /// semantic ownership; every non-guard/cancel path uses the caller's normal
    /// fail-closed resolution result.
    static func publishGuardAwareTreeSelectionMirror(
        pending: PendingPostMutationFocus?,
        model: SelectionModel,
        currentSessionIdentity: ObjectIdentifier?,
        currentMutationToken: Int?,
        resolvedSelection: AppState.TreeSelection?,
        appState: AppState
    ) {
        let guardedFallback: AppState.TreeSelection? = {
            guard let pending,
                pending.installedDeleteFallback,
                let fallback = pending.capturedDeleteRow,
                model.focused == fallback.identity,
                model.isSelected(fallback.identity, currentPath: fallback.path)
            else { return nil }
            if let ownerSessionIdentity = pending.ownerSessionIdentity,
                currentSessionIdentity != ownerSessionIdentity
            {
                return nil
            }
            if let ownerMutationToken = pending.ownerMutationToken,
                currentMutationToken != ownerMutationToken
            {
                return nil
            }
            return AppState.TreeSelection(
                path: fallback.path,
                isDirectory: fallback.isDirectory)
        }()
        let mirrored = guardedFallback ?? resolvedSelection
        if appState.treeSelectedNode != mirrored {
            appState.treeSelectedNode = mirrored
        }
    }

    /// The user's live keyboard locus wins at landing. A captured submission
    /// path is only a fallback when the live row was moved or disappeared.
    static func batchMoveFocusPlan(
        liveFocusPath: String?,
        liveFocusIsResolvable: Bool,
        preferredFocusPath: String?,
        firstStandingPath: String?,
        using index: SelectionModel.KnownMoveIndex,
        componentVisits: inout Int
    ) -> BatchMoveFocusPlan {
        if let liveFocusPath, liveFocusIsResolvable {
            if let remapped = index.remappedPath(
                liveFocusPath, componentVisits: &componentVisits) {
                return BatchMoveFocusPlan(
                    path: remapped, shouldRevealMovedAncestry: true)
            }
            return BatchMoveFocusPlan(
                path: liveFocusPath, shouldRevealMovedAncestry: false)
        }
        if let preferredFocusPath {
            let remapped = index.remappedPath(
                preferredFocusPath, componentVisits: &componentVisits)
            return BatchMoveFocusPlan(
                path: remapped ?? preferredFocusPath,
                shouldRevealMovedAncestry: remapped != nil)
        }
        return BatchMoveFocusPlan(
            path: firstStandingPath,
            shouldRevealMovedAncestry: firstStandingPath != nil)
    }

    /// Pure (#852, Codex round-4 finding 2 + round-5): remap a selection across a
    /// KNOWN in-app rename/move (`oldPath` → `newPath`), which deliberately
    /// PRESERVES a directory's id while changing its path. For each entry whose
    /// snapshot path is `oldPath` or a descendant of it, rewrite the snapshot to
    /// the corresponding new path — and, for FILE entries (whose RowID encodes
    /// the path), rewrite the RowID too; DIR entries keep their id-based RowID.
    /// `focus` and the range `anchor` are remapped alongside — the anchor via its
    /// OWN `anchorSnapshot`, because it may be a DESELECTED row (a ⌘-remove) that
    /// isn't in `snapshot`, and a deselected DESCENDANT anchor under a moved
    /// folder must still follow the move. Static → directly unit-tested.
    static func remapSelectionForMove(
        selection: Set<RowID>, snapshot: [RowID: String],
        focus: RowID?, anchor: RowID?, anchorSnapshot: String?,
        oldPath: String, newPath: String
    ) -> (
        selection: Set<RowID>, snapshot: [RowID: String],
        focus: RowID?, anchor: RowID?, anchorSnapshot: String?
    ) {
        var model = SelectionModel(
            focused: focus,
            selected: selection,
            selectionPathSnapshots: snapshot,
            rangeAnchor: anchor,
            rangeAnchorPathSnapshot: anchorSnapshot ?? anchor.flatMap { snapshot[$0] })
        model.remapKnownMoves(
            [SelectionModel.KnownMove(oldPath: oldPath, newPath: newPath)],
            identityForRemappedPath: remappedSelectionIdentity)
        return (
            model.selected,
            model.selectionPathSnapshots,
            model.focused,
            model.rangeAnchor,
            model.rangeAnchorPathSnapshot)
    }

    private static func remappedSelectionIdentity(_ identity: RowID, path: String) -> RowID {
        if case .node(.file) = identity { return .node(.file(path: path)) }
        return identity
    }

    var body: some View {
        VStack(spacing: 0) {
            if let progress = appState.importBatchProgress {
                SidebarImportProgressStrip(
                    progress: progress,
                    onCancel: {
                        _ = appState.requestImportBatchCancellation()
                    })
            } else {
                // Import owns the only visible progress/cancellation surface
                // for its complete lifetime, including mandatory final scan.
                progressBar
                structuralMutationProgress
            }
            batchTrashQuarantineRecovery
            if let notice = appState.sidebarVaultPrefsNotice {
                sidebarPreferencesNotice(notice.localizedDescription)
            } else if appState.sidebarOrganizationJournalRecoveryPending {
                // Round-26: unsaved structural transforms with a readable
                // file — same banner and Retry, dedicated message.
                sidebarPreferencesNotice(
                    "Some sidebar organization changes aren't saved yet. "
                        + "Retry to save them.")
            }
            sidebarSectionsMount
            Group {
                if filterModel.isActive {
                    // FL-09 (#663): the flat filter-result overlay
                    // replaces every tree branch, including scanning /
                    // error / empty — one result list, one focus model.
                    filterResultsList
                } else if appState.currentImportBatchOwner != nil {
                    treeList
                } else if appState.isScanning && appState.files.isEmpty {
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

            // FL5-2 (#665): the Tags section renders below the tree
            // (final order: Shortcuts, Recents, tree, Tags) and hides
            // with the rest while the filter overlay owns the surface.
            if !filterModel.isActive {
                SidebarTagTreeView()
            }

            // U4-3 (#472): the bottom-left utility bar — Settings, Help, and
            // the vault switcher — pinned at the sidebar's bottom edge. Last
            // child of the column (U3-3 moved Properties into the in-note
            // widget). The bar draws its own separator above.
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
        .background {
            TreeOpenSelectedKeyMonitor(
                enabled: fileTreeFocused,
                isRenaming: appState.renamingNode != nil,
                openSelected: { _ = requestOpenSelected() })
                .frame(width: 0, height: 0)
        }
        .onAppear {
            // Bind the tree to whatever session is already open when the
            // sidebar mounts (re-entering a vault view with files loaded).
            // #873: rehydrate the persisted expansion state instead of
            // resetting — `restoreWorkspaceLayout` filled the mirror before
            // any view update could run this.
            // FL-06: adopt organization BEFORE the bind so the first level
            // fetch already stores sorted levels; the stale-pin seam defers
            // one turn so a bind-time prune cannot publish mid-update.
            tree.onStalePins = { folder, stale in
                Task { @MainActor in
                    appState.pruneStaleSidebarPins(forFolder: folder, stale: stale)
                }
            }
            tree.applyOrganization(currentOrganizationContext())
            tree.bind(
                to: appState.currentSession,
                restoringExpandedDirPaths: appState.treeExpandedDirPaths)
            mirrorProgrammaticSelection(rowID(forPath: appState.selectedFilePath))
        }
        .onChange(of: appState.currentVaultURL) {
            // Each new vault gets a fresh tree and its own count announcement.
            didAnnounceCount = false
            typeSelectBuffer = ""  // #850: a prefix never spans vaults
            pendingBatchFocus = nil
            Self.cancelPendingPostMutationFocus(&pendingPostMutationFocus)
            tree.applyOrganization(currentOrganizationContext())
            tree.bind(
                to: appState.currentSession,
                restoringExpandedDirPaths: appState.treeExpandedDirPaths)
        }
        // FL-06: preference/pin changes re-sort cached levels in place; the
        // clock tick re-buckets only when the local civil day changes.
        .onChange(of: appState.sidebarOrganization) { _, _ in
            tree.applyOrganization(currentOrganizationContext())
        }
        .onChange(of: sidebarNow) { _, _ in
            tree.applyOrganization(currentOrganizationContext())
        }
        // #873: mirror every expansion change (user toggles, restores,
        // move-reveals, spring-loads) into AppState, whose debounced
        // workspace-save path persists it. Post-update mutation point (#448).
        .onChange(of: tree.expanded) { _, _ in
            let paths = tree.expandedDirPaths
            if appState.treeExpandedDirPaths != paths {
                appState.treeExpandedDirPaths = paths
            }
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
                if appState.consumeImportOwnedScanCompletion() != nil {
                    // Aggregate import publishes its own authoritative root
                    // mutation after all copy/move work. Acknowledge this scan
                    // edge without issuing a duplicate generic invalidation.
                } else {
                    tree.authoritativeTreeInvalidation()
                }
                // #852 (Codex round-4 finding 1): the reused-id reconcile does NOT
                // run here — `treeInvalidation` schedules an ASYNC refetch, so ids
                // wouldn't yet resolve to the new reality. It runs on
                // `.onChange(of: tree.visibleRows)` (below), which fires once the
                // refetch lands and resolution is fresh.
            }
        }
        .task(id: sidebarClockKey) {
            // The shared sidebar clock: relative row dates need periodic
            // refresh, and FL-06 date buckets need to observe local-midnight
            // rollover. Same-day ticks are no-ops in the view model's
            // organization gate, so the grouped case costs one comparison.
            guard rowPreferences.dateFormat == .relative
                || sidebarGroupingIsActive
            else { return }
            sidebarNow = Date()
            while !Task.isCancelled {
                do {
                    try await Task.sleep(for: Self.relativeDateRefreshInterval)
                } catch {
                    return
                }
                sidebarNow = Date()
            }
        }
    }

    /// One identity for the clock task: restarts when either consumer's need
    /// appears or disappears.
    private var sidebarClockKey: String {
        "\(rowPreferences.dateFormat.rawValue)-\(sidebarGroupingIsActive)"
    }

    private var sidebarGroupingIsActive: Bool {
        let prefs = appState.sidebarOrganization.prefs
        return prefs.vaultChoice.grouping == .dateBuckets
            || prefs.folderOverrides.values.contains { $0.grouping == .dateBuckets }
    }

    /// The complete FL-06 organization context handed to the view model.
    private func currentOrganizationContext() -> FileTreeViewModel.OrganizationContext {
        FileTreeViewModel.OrganizationContext(
            prefs: appState.sidebarOrganization.prefs,
            pins: appState.sidebarOrganization.pins,
            now: sidebarNow,
            calendar: .current,
            locale: .current)
    }

    // MARK: - States

    /// Persistent, non-modal recovery notice for unsafe vault-authored sidebar
    /// preferences. It stays visible while defaults are in use, uses a warning
    /// symbol plus text (never color alone), and shares the exact message spoken
    /// by the vault-open announcement.
    /// FL-07: hoisted so the sidebar column's ViewBuilder stays within
    /// the type-checker's budget.
    private var sidebarSectionsMount: some View {
        VStack(spacing: 0) {
            // FL-09 (#663): the filter field is the topmost sidebar
            // control. It never unmounts — it hosts the rollover
            // observers and must keep focus through activation.
            SidebarFilterField(
                model: filterModel,
                isFocused: $filterFieldFocused,
                moveFocusToResults: { focusFilterResultsAtFirstRow() })
            // A committed query overlays sections and tree with ONE
            // result list (locked decision 7). The views unmount; the
            // backing models (tree expansion, selection, shortcut and
            // recents state) are untouched, so Esc restores the exact
            // prior view.
            if !filterModel.isActive {
                SidebarSectionsView(
                    activateShortcut: { appState.activateSidebarShortcut($0) },
                    openRecent: { appState.openFile($0, target: .currentTab) })
            }
        }
        .onChange(of: appState.sidebarFilterFocusRequest) { _, _ in
            filterFieldFocused = true
        }
        .onChange(of: filterModel.isActive) { _, active in
            if active {
                publishFilterSelectionSnapshot()
            } else {
                filterListSelection = nil
                republishTreeSelectionSnapshot()
            }
        }
        // Rename/move/trash/duplicate completions re-run the committed
        // query so the flat list can't keep a renamed or deleted row
        // (review round). Announcement changes are the one funnel every
        // structural completion already passes through.
        .onChange(of: appState.lastMutationAnnouncement) { _, _ in
            filterModel.refreshAfterStructuralMutation()
            // FL5-2 rule 1: the Tags section refreshes on the same
            // funnel while expanded (no-op otherwise).
            appState.refreshSidebarTagTree()
        }
        // Scan completion re-derives file_tags wholesale; a tree
        // fetched mid-scan would otherwise stay stale (review round).
        .onChange(of: appState.isScanning) { _, scanning in
            if !scanning {
                appState.refreshSidebarTagTree()
            }
        }
        .sheet(
            item: Binding(
                get: { appState.sidebarTagEditorRequest },
                set: { appState.sidebarTagEditorRequest = $0 })
        ) { request in
            SidebarTagEditor(request: request)
                .environmentObject(appState)
        }
        .onChange(of: filterFieldFocused) { _, _ in
            syncSidebarRegionFocus()
        }
        .onChange(of: filterResultsFocused) { _, _ in
            syncSidebarRegionFocus()
        }
        // FL-07: one-shot reveal requests (shortcut containers, history
        // navigation). Ancestors expand through the existing seam; the
        // selection applies programmatically, so the history dedupe
        // (the navigated entry equals the ring's cursor) prevents
        // re-pushing. Mounted here, off the List's modifier chain,
        // to stay inside the type-checker's budget.
        .onChange(of: appState.sidebarRevealRequest) { _, request in
            guard let request else { return }
            tree.ensureAncestorsExpanded(forPath: request.path)
            if let rowID = rowID(
                forRevealPath: request.path,
                isDirectory: request.isDirectory)
            {
                mirrorProgrammaticSelection(rowID)
            }
        }
        // FL3-4.1: Expand Loaded — materialized folders expand,
        // fetching at most one level deeper.
        .onChange(of: appState.sidebarExpandLoadedRequest) { _, _ in
            tree.expandLoadedLevels()
        }
        // FL3-4.1: Collapse All against the LIVE tree, preserving the
        // selected row's ancestors.
        .onChange(of: appState.sidebarCollapseAllRequest) { _, _ in
            let anchor = selectionModel.focused
                .flatMap { selectionRow(for: $0)?.path }
                ?? appState.selectedFilePath
            tree.collapseAllPreservingAncestors(ofPath: anchor)
        }
        // FL3-3.2: ⌃1–⌃9 activate shortcuts 1–9 while the sidebar has
        // key focus (⌘1–9 belong to tabs; palette commands work
        // anywhere).
        .background {
            SidebarShortcutChordMonitor(
                enabled: fileTreeFocused,
                isRenaming: appState.renamingNode != nil,
                activateSlot: { slot in
                    _ = try? appState.dispatchSidebarAction(
                        id: SlateCommandID.sidebarOpenShortcut(slot))
                })
                .frame(width: 0, height: 0)
        }
    }

    // MARK: - Filter results overlay (FL-09, #663)

    /// ↓ from the field: enter the flat list at row 1. Selection is
    /// activation in this list (the tree's shipped FL1 behavior), so
    /// entering also opens row 1's note.
    private func focusFilterResultsAtFirstRow() {
        guard let first = filterModel.results?.files.first?.path else { return }
        filterListSelection = first
        filterResultsFocused = true
    }

    /// Selection-snapshot ownership for the overlay (review round,
    /// high): a single-item snapshot for the selected result row, or an
    /// empty one while nothing is selected — never the hidden tree
    /// selection. Published through the same admission funnel the tree
    /// uses.
    private func publishFilterSelectionSnapshot() {
        guard let sessionIdentity = tree.sessionIdentity else { return }
        let selectedSummary = filterListSelection.flatMap { path in
            filterModel.results?.files.first(where: { $0.path == path })
        }
        let items = selectedSummary.map {
            [SidebarSelectionItem(
                path: $0.path, isDirectory: false, isMarkdown: $0.isMarkdown)]
        } ?? []
        let parent = (selectedSummary?.path as NSString?)?
            .deletingLastPathComponent ?? ""
        _ = appState.publishSidebarSelectionSnapshot(SidebarSelectionSnapshot(
            sessionIdentity: sessionIdentity,
            items: items,
            focusedPath: selectedSummary?.path,
            creationParent: parent == "." ? "" : parent))
    }

    /// Exit the overlay: hand snapshot ownership back to the tree's
    /// selection model (its own edges republish only on CHANGE, so a
    /// silent handback would leave the last filter row as the frozen
    /// menu target while the tree highlights something else).
    private func republishTreeSelectionSnapshot() {
        mutateSelectionAndPublish { _ in }
    }

    /// Mirror of `workspace.noteTreeFocusChanged` covering every
    /// sidebar-owned focus carrier (review round: ⌥⌘F from the editor
    /// must register the sidebar region or ⌘⌥→ round-trips break).
    private func syncSidebarRegionFocus() {
        appState.workspace.noteTreeFocusChanged(
            fileTreeFocused || filterFieldFocused || filterResultsFocused)
    }

    /// The flat paged result list of shared FL1 rows (locked decision
    /// 7). One `List`, path-keyed selection; Esc moves focus back to the
    /// field; the tree beneath was never torn down.
    private var filterResultsList: some View {
        Group {
            if let page = filterModel.results, !page.files.isEmpty {
                List(selection: $filterListSelection) {
                    ForEach(page.files, id: \.path) { summary in
                        filterResultRow(summary)
                    }
                    if page.nextCursor != nil {
                        Button {
                            filterModel.loadNextPage()
                        } label: {
                            Text("Load More Results")
                                .font(Tokens.Typography.body)
                                .foregroundStyle(Tokens.ColorRole.accentText)
                                .frame(maxWidth: .infinity, alignment: .leading)
                        }
                        .buttonStyle(.borderless)
                        .selectionDisabled()
                        .accessibilityHint(
                            "Appends the next page of matching files to the list.")
                    }
                }
                .listStyle(.sidebar)
                .focused($filterResultsFocused)
                .onExitCommand {
                    filterResultsFocused = false
                    filterFieldFocused = true
                }
                .onChange(of: filterListSelection) { _, selected in
                    // Review round (high): while the overlay is active it
                    // OWNS the published selection — File menu, palette,
                    // and keyboard projections must target the visible
                    // row, never the hidden tree multi-selection (a
                    // wrong-target trash is a data-loss path).
                    publishFilterSelectionSnapshot()
                    guard let selected else { return }
                    appState.openFile(selected, target: .currentTab)
                }
                .accessibilityLabel("Filter results")
            } else {
                VStack(spacing: Tokens.Spacing.sm) {
                    Text("No results.")
                        .font(Tokens.Typography.body)
                        .foregroundStyle(Tokens.ColorRole.textSecondary)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .accessibilityElement(children: .combine)
            }
        }
    }

    /// One filter-result row: the shared FL1 row component plus the
    /// containing-folder subtitle, carrying the exact single-file
    /// context menu tree rows use (spec rule 5 — same component, all
    /// FL2 verbs).
    @ViewBuilder
    private func filterResultRow(_ summary: FileSummary) -> some View {
        if let rename = appState.renamingNode,
            rename.path == summary.path, !rename.isDirectory
        {
            // Review round: the shared menu exposes Rename, so the
            // overlay must render the SAME visible editor the tree row
            // swaps to — otherwise the rename owner strands invisibly.
            renameEditor(rename, isDirectory: false)
        } else {
            filterResultFileRow(summary)
        }
    }

    private func filterResultFileRow(_ summary: FileSummary) -> some View {
        let item = SidebarSelectionItem(
            path: summary.path,
            isDirectory: false,
            isMarkdown: summary.isMarkdown)
        let parent = (summary.path as NSString).deletingLastPathComponent
        let subtitle = parent.isEmpty ? "Vault root" : parent
        let selected = filterListSelection == summary.path
        return SidebarFileRow(
            model: SidebarRowModel(
                summary: summary,
                preferences: rowPreferences,
                isPinned: false,
                pathSubtitle: subtitle,
                now: sidebarNow),
            depth: 0,
            isSelected: selected,
            selectionIsActive: nativeSelectionIsActive)
            .tag(summary.path)
            // Complex-gesture disclosure (WCAG 3.3.2), the tree-row
            // idiom: say what activation does and where the rest lives.
            .accessibilityHint(
                "Opens the file. Other available actions are in the context menu.")
            .contextMenu {
                if let publishedSnapshot = appState.sidebarSelectionSnapshot {
                    // The overlay owns selection while active: target THIS
                    // row, never the tree's (hidden) multi-selection —
                    // so the single-row snapshot is built directly.
                    let rowSnapshot = SidebarSelectionSnapshot(
                        sessionIdentity: publishedSnapshot.sessionIdentity,
                        items: [item],
                        focusedPath: summary.path,
                        creationParent: parent.isEmpty ? "" : parent,
                        selectionRevision: publishedSnapshot.selectionRevision)
                    let projection = Self.sidebarRowActionProjection(
                        surface: .contextMenu,
                        row: item,
                        publishedSnapshot: rowSnapshot,
                        structuralMutationDisabledReason:
                            appState.structuralMutationDisabledReason,
                        actionDisabledReasons: sidebarRowActionDisabledReasons(
                            for: item))
                    singleFileContextMenuGroups(
                        projection: projection, path: summary.path)
                }
            }
    }

    private func sidebarPreferencesNotice(_ message: String) -> some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.xs) {
            HStack(alignment: .top, spacing: Tokens.Spacing.sm) {
                SlateSymbol.warning.decorative
                    .foregroundStyle(Tokens.ColorRole.warningText)
                Text(message)
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.warningText)
                    .fixedSize(horizontal: false, vertical: true)
            }
            Button {
                _ = appState.retrySidebarVaultPreferences()
            } label: {
                SlateSymbol.refresh.label("Retry")
            }
            .buttonStyle(.borderless)
            .controlSize(.small)
            .frame(minHeight: Self.recoveryActionMinimumHeight)
            .contentShape(Rectangle())
            .disabled(appState.isRetryingSidebarVaultPreferences)
            .accessibilityLabel("Retry sidebar settings")
            .accessibilityHint("Reads .slate/sidebar.json again after you repair it.")
        }
        .padding(.horizontal, Tokens.Spacing.md)
        .padding(.vertical, Tokens.Spacing.sm)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Tokens.ColorRole.surfaceSecondary)
        .overlay(alignment: .bottom) {
            Rectangle()
                .fill(Tokens.ColorRole.separator)
                .frame(height: 1)
                .accessibilityHidden(true)
        }
        .accessibilityElement(children: .contain)
    }

    private var scanningState: some View {
        // Linear (like the `scanStrip` that takes over once rows
        // appear), NOT circular: progress-indicators.md — "don't
        // switch between circular and bar styles" mid-operation. The
        // empty-vault phase and the strip are one scan.
        VStack(spacing: Tokens.Spacing.md) {
            ProgressView()
                .progressViewStyle(.linear)
                .frame(maxWidth: 220)
            Text("Scanning vault…")
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Scanning vault. The file list will appear when the scan finishes.")
    }

    private func errorState(_ message: String) -> some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
            Text("Could not load files")
                .font(Tokens.Typography.sectionHeader)
                .accessibilityAddTraits(.isHeader)
            Text(message)
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
        }
        .padding(Tokens.Spacing.lg)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    private var emptyState: some View {
        // Empty states offer the primary action (DoD §A "tighten every
        // empty state"): without the button, the only path to a first
        // note is ⌘N / the palette — invisible from the empty sidebar.
        let disabledReason = appState.structuralMutationDisabledReason
        return VStack(spacing: Tokens.Spacing.sm) {
            Text("No Markdown files in this vault.")
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            Button("New Note") {
                appState.requestCreateNote(in: "")
            }
            .disabled(disabledReason != nil)
            .accessibilityHint(
                disabledReason
                    ?? "Creates your first note at the vault root. Command-N.")
            .help(
                disabledReason
                    ?? "Creates your first note at the vault root. Command-N.")
            if let disabledReason {
                Text(disabledReason)
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .multilineTextAlignment(.center)
                    .accessibilityLabel(disabledReason)
            }
        }
        .padding(Tokens.Spacing.lg)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
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
        // FL-06 §FL3-1.6: the container names any non-default organization so
        // a VoiceOver user entering the list hears the active order. The
        // summary follows the selected container's EFFECTIVE choice — the
        // same source as the sort-menu radio state — so a per-folder
        // override is reported truthfully (red-team finding 4).
        .accessibilityLabel(
            Self.treeAccessibilitySummary(
                for: appState.sidebarOrganizationMenuTargetChoice
            ) ?? "Files")
        // Tree folder glyphs (disclosure + open/closed folder) render
        // hierarchical so open and closed folders read as one family (U5-1,
        // DoD §B rendering-mode consistency). Rendering-mode only — the sidebar
        // List already sits on the system sidebar material, so no `glassEffect`
        // here (the spec's material list is toolbar/rail/tab strip; the tree is
        // not a custom-backgrounded chrome container).
        .slateSymbolSurface(.tree)
        .focused($fileTreeFocused)
        // U4-4 review: mirror REAL tree focus into the region bookkeeping —
        // Tab/click into the tree must make the next ⌘⌥→ "return to editor"
        // per spec, not an interior editor move. Post-update (#448-safe).
        .onChange(of: fileTreeFocused) { _, _ in
            syncSidebarRegionFocus()
        }
        // If another structural operation begins while a drag is hovering or
        // decoding, immediately remove every acceptance mirror, cancel spring
        // timers, and restore spring-opened folders. The decoded payload will
        // independently reject through AppState's request admission.
        .onChange(of: appState.isMutatingStructure) { _, busy in
            if busy {
                endDragSession(dropDestination: nil)
            }
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
        .onKeyPress(keys: [.upArrow, .downArrow]) { press in
            guard
                let action = Self.selectionKeyAction(
                    key: press.key,
                    modifiers: press.modifiers,
                    fileTreeFocused: fileTreeFocused,
                    isRenaming: appState.renamingNode != nil),
                handleSelectionKeyAction(action, proxy: proxy)
            else { return .ignored }
            return .handled
        }
        .onKeyPress("a", phases: .down) { press in
            guard
                let action = Self.selectionKeyAction(
                    key: press.key,
                    modifiers: press.modifiers,
                    fileTreeFocused: fileTreeFocused,
                    isRenaming: appState.renamingNode != nil),
                handleSelectionKeyAction(action, proxy: proxy)
            else { return .ignored }
            return .handled
        }
        // ⌘⌫ deletes the selected node, but ONLY while the tree has keyboard
        // focus (spec §U2-5 "tree-focused only"). Delivered here rather than as
        // a menu-bar chord so it can't fire while a property field or the editor
        // is focused. A folder-or-file selection is required; placeholders and
        // no-selection no-op.
        //
        // The chord is ⌘⌫ — Finder's Move-to-Trash. `.onDeleteCommand` alone
        // also fires for a BARE ⌫, which must never mutate the vault (HIG:
        // Finder ignores it; an unmodified slip-key must not trash the
        // selection — especially since the highlight may sit on a row the
        // user last clicked minutes ago). The gate inspects the live key
        // event; non-key deliveries (a VoiceOver/AX delete action, a menu
        // `delete:`) carry no matching keyDown and pass through.
        .onDeleteCommand {
            guard fileTreeFocused, selectedTreeNode != nil,
                Self.deleteCommandAllowed(event: NSApp.currentEvent)
            else { return }
            // #852: the WHOLE multi-selection when ≥2 rows are selected, else
            // the single focused node — both through the #860 delete funnel.
            requestDeleteFromKeyboard()
        }
        // Explicit ⌘⌫ delivery: SwiftUI routes a COMMAND-modified ⌫ through
        // the key-press path, not (reliably) the delete-command path — with
        // only the `.onDeleteCommand` above, ⌘⌫ can be a dead chord while
        // bare ⌫ (the one that must NOT delete) is the live one. `.handled`
        // consumes the event so the two paths can't double-fire.
        .onKeyPress(keys: [.delete]) { press in
            guard
                Self.treeKeyInterceptionActive(
                    fileTreeFocused: fileTreeFocused,
                    isRenaming: appState.renamingNode != nil),
                Self.deleteKeyModifiersAllowed(press.modifiers),
                selectedTreeNode != nil
            else { return .ignored }
            // #852/#860: batch when multi-selected, else single — same funnel.
            requestDeleteFromKeyboard()
            return .handled
        }
        // Return explicitly opens every visible, path-valid selected file;
        // ⌘↓ reaches the same request through the tree-scoped key-down monitor
        // (SwiftUI erases that chord's modifiers before onMoveCommand). When
        // there is no selected file, Return keeps the selected folder's
        // disclosure behavior. Space always remains folder disclosure.
        .onKeyPress(keys: [.space, .return], phases: .down) { press in
            guard
                Self.treeKeyInterceptionActive(
                    fileTreeFocused: fileTreeFocused,
                    isRenaming: appState.renamingNode != nil)
            else { return .ignored }
            if press.key == .return,
                let action = Self.selectionKeyAction(
                    key: press.key,
                    modifiers: press.modifiers,
                    fileTreeFocused: fileTreeFocused,
                    isRenaming: appState.renamingNode != nil)
            {
                switch Self.returnOpenDisposition(
                    for: appState.sidebarSelectionSnapshot)
                {
                case .openSelection:
                    if handleSelectionKeyAction(action, proxy: proxy) {
                        return .handled
                    }
                case .folderDisclosure:
                    break
                }
            }
            guard press.modifiers.subtracting(.capsLock).isEmpty,
                let node = selectedTreeNode, node.isDirectory
            else { return .ignored }
            tree.toggle(node)
            return .handled
        }
        // F2 begins inline rename of the selected node (#850) — the cross-app
        // rename fallback the app's own grid already honors. Return is owned by
        // explicit open-selected / folder disclosure above. Gated exactly like
        // every other tree key path: never while the RenameField is up
        // (focus-WITHIN — see `treeKeyInterceptionActive`), never with
        // modifiers down.
        .onKeyPress(keys: [Self.f2Key]) { press in
            guard
                Self.treeKeyInterceptionActive(
                    fileTreeFocused: fileTreeFocused,
                    isRenaming: appState.renamingNode != nil),
                Self.typeSelectModifiersAllowed(press.modifiers),
                selectedTreeNode != nil
            else { return .ignored }
            let projection = appState.sidebarActionProjection(surface: .keyboard)
            do {
                _ = try Self.invokeSidebarKeyboardAction(
                    id: SlateCommandID.renameEntry,
                    projection: projection,
                    dispatch: { intent in
                        _ = try appState.dispatchSidebarAction(intent)
                    })
            } catch {
                appState.postMutationAnnouncement(error.sidebarActionAnnouncement)
            }
            return .handled
        }
        // Type-select (#850): printable characters accumulate into a prefix
        // buffer (~1s quiet resets it) that jumps the selection to the next
        // VISIBLE row — folder or file — whose name matches, wrapping
        // around (the Finder/NSOutlineView staple; on a 10k vault arrowing
        // was the only in-tree option). Space is deliberately NOT here — it
        // belongs to folder disclosure above. Gated like every tree key
        // path: a modifier chord or an active rename must fall through
        // untouched (a List-level onKeyPress sees keys BEFORE the rename
        // field editor does).
        .onKeyPress(characters: Self.typeSelectCharacters, phases: .down) { press in
            guard
                Self.treeKeyInterceptionActive(
                    fileTreeFocused: fileTreeFocused,
                    isRenaming: appState.renamingNode != nil),
                Self.typeSelectModifiersAllowed(press.modifiers)
            else { return .ignored }
            handleTypeSelect(press.characters, proxy: proxy)
            return .handled
        }
        // Root drop target: a node dropped on the tree background (not on a
        // folder row) moves to the vault root. Folder rows have their own
        // `.onDrop` that wins when the drop lands on them. #851: targeting
        // is mirrored into `rootDropTargeted` for the drop-destination ring
        // below and for the drag-session bookkeeping.
        .onDrop(
            of: [Self.nodeUTType, Self.fileURLUTType], isTargeted: rootDropBinding,
            perform: { providers in handleDrop(providers, into: "") })
        // #851: HIG drag-and-drop "highlight the destination" for the root
        // target — a selection-token ring hugging the tree, visible while
        // the drag would drop at the vault root. Decorative drag feedback
        // (hit-testing off; the drag itself is the interaction).
        .overlay {
            if Self.dropTargetIsActive(
                rootDropTargeted,
                busy: appState.structuralMutationDisabledReason != nil)
            {
                RoundedRectangle(cornerRadius: Tokens.Radius.control)
                    .strokeBorder(Tokens.ColorRole.selection, lineWidth: 2)
                    .padding(1)
                    .allowsHitTesting(false)
                    .accessibilityHidden(true)
            }
        }
        // Complex-gesture disclosure (WCAG 3.3.2) for the container-level
        // drop target above.
        .accessibilityHint(
            "Drop files or folders here to copy external items into the vault or move vault items to the root.")
        // Seed the mirror if a selection already exists when the sidebar mounts
        // (e.g. re-entering the vault view with a file open).
        .onAppear { mirrorProgrammaticSelection(rowID(forPath: appState.selectedFilePath)) }
        // U2-5: react to structural mutations. AppState (the mutation funnel)
        // publishes `treeMutation`; the view-owned VM refreshes the affected
        // levels here, then U2-6 moves the selection. Token-edge-triggered so
        // two equal mutations still fire.
        .onChange(of: appState.treeMutation?.token) { _, _ in
            handleTreeMutation(proxy: proxy)
        }
        // Non-structural writes (body/frontmatter/task edits) leave tree shape
        // intact but can change every rich-row field. Apply the complete
        // coalesced burst atomically so SwiftUI cannot drop an intermediate
        // per-file notification.
        .onReceive(appState.sidebarFileSummaryUpdates) { summaries in
            tree.replaceFileSummaries(summaries)
        }
        // #852 (Codex round-4 finding 1): reconcile the selection against reused
        // SQLite directory ids whenever the visible rows settle (an external
        // replace / rescan refetch). This fires AFTER the refetch has landed —
        // unlike the scan-finish path, where ids wouldn't yet resolve to the new
        // reality — so a repointed id resolves to its NEW path here and is
        // dropped (with the native focus re-anchored). A KNOWN in-app rename/move
        // already remapped its snapshots in `handleTreeMutation`, so its entries
        // resolve to a matching path and survive this reconcile untouched.
        .onChange(of: tree.visibleRows) { _, _ in
            if restorePendingImportSelection(proxy: proxy)
                || restorePendingBatchFocus(proxy: proxy)
                || restorePendingPostMutationFocus(proxy: proxy)
            {
                mirrorTreeSelectionToAppState(listSelection)
                return
            }
            reconcileSelectionAfterTreeChange()
            // Re-sync the single-item command mirror against the refreshed paths
            // (a folder rename keeps its dir id → the focus `listSelection` never
            // changed → `.onChange(of: listSelection)` never fired → the mirror
            // would keep the OLD path). Idempotent: a no-op when unchanged.
            mirrorTreeSelectionToAppState(listSelection)
        }
        // Vault switch: the row highlight must not survive into the next
        // vault — AppState clears its `treeSelectedNode` mirror on the
        // lifecycle paths (Codex review: stale mirrors let Copy Path /
        // Reveal resolve a vault-A node against vault B's root); the
        // view-side highlight resets with it.
        .onChange(of: appState.currentVaultURL) { _, _ in
            Self.cancelPendingPostMutationFocus(&pendingPostMutationFocus)
            mutateSelectionAndPublish { $0.reveal(nil) }
            selectionRevisionGate.arm(for: nil)
            listSelection = nil
        }
        .alert(
            activeOpenSelection?.title ?? "Open Selected Files?",
            isPresented: activeOpenSelectionPresented,
            presenting: activeOpenSelection
        ) { request in
            Button("Open") {
                _ = appState.confirmOpenSelection(id: request.id)
                fileTreeFocused = true
            }
            .keyboardShortcut(.defaultAction)
            Button("Cancel", role: .cancel) {
                _ = appState.cancelOpenSelection(id: request.id)
                fileTreeFocused = true
            }
        } message: { request in
            Text(request.message)
        }
        // User-driven selection: push it onto AppState here, outside the list's
        // update transaction, so handleSelectionChange runs in a well-defined
        // context. Only *file* rows drive `selectedFilePath`; selecting a
        // folder (or a placeholder) leaves the current note selection intact
        // (folders don't open). The guard prevents a write-back loop with the
        // mirror `.onChange` below.
        .onChange(of: listSelection) { _, newSelection in
            let announcementIsSuppressed = selectionAnnouncementGate.consume()
            let revisionIsNeutral = selectionRevisionGate.consume(if: newSelection)
            // #852: a ⌘/⇧ multi-select gesture manages the set + open itself
            // (`applyMultiSelectClick`) — consume its one-shot suppression and
            // don't re-open or collapse here.
            if suppressOpenForSelectionChange {
                suppressOpenForSelectionChange = false
                // A post-Delete carrier uses this same early-return branch to
                // preserve the missing-file tab. Consume its companion flag so
                // it cannot suppress a later genuine selection.
                suppressOpenForPostMutationFocus = false
                mirrorTreeSelectionToAppState(selectionModel.focused)
                announceFocusedFileSelection(
                    selectionModel.focused,
                    suppressed: announcementIsSuppressed)
                return
            }
            // FL3-4.2: record the selection for Back/Forward (dupes and
            // history-navigation arrivals collapse inside the recorder).
            recordHistoryForUserSelection(newSelection)
            // #852: any OTHER focus change (keyboard arrow, type-select,
            // programmatic open, plain click) is SINGLE — collapse any lingering
            // batch set to this one row so batch state never outlives it.
            mutateSelectionAndPublish {
                let row = newSelection.flatMap { selectionRow(for: $0) }
                if revisionIsNeutral {
                    $0.reveal(row)
                } else {
                    $0.revealFromUserIntent(row)
                }
            }
            // Mirror the selection (file OR folder) to AppState so the file-
            // management commands know their target. Placeholders clear it.
            mirrorTreeSelectionToAppState(selectionModel.focused)
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
                appState.openFile(
                    path,
                    target: .newTab,
                    advancesSidebarSelectionRevision: false)
                if case let .node(.file(selected)) = listSelection,
                    selected != appState.selectedFilePath {
                    revertCommandClickSelection(
                        to: appState.selectedFilePath.map { .node(.file(path: $0)) })
                }
                return
            }
            if !BaseExactIdentity.matches(appState.selectedFilePath, path) {
                appState.openFile(
                    path,
                    target: .currentTab,
                    advancesSidebarSelectionRevision: false)
            }
            announceFocusedFileSelection(
                newSelection,
                suppressed: announcementIsSuppressed)
        }
        // Programmatic selection changes (search-open, template create, dirty-
        // gate rollback) mirror onto the List but keep their originating
        // announcement. User-focused row speech is driven above by the actual
        // `listSelection` edge, including folder → already-open-file paths that
        // don't change `selectedFilePath` at all.
        .onChange(of: appState.selectedFilePath) { _, newPath in
            // Mirror programmatic selection changes back onto the list
            // highlight (search-open, template-create, dirty-gate rollback).
            // Guarded so it doesn't fight the user-driven write above.
            let outcome = Self.programmaticSelectionOutcome(
                currentListSelection: listSelection,
                mirroredSelection: rowID(forPath: newPath))
            // #852 (Codex finding 2a): a programmatic / single open collapses the
            // batch set to the single focus UNCONDITIONALLY — even when
            // `listSelection` is ALREADY the mirrored row (a same-value
            // assignment below wouldn't fire `.onChange(of: listSelection)`, so
            // the collapse there is skipped and a stale {A,B} would let a later
            // ⌘⌫ trash rows the user can't see selected). This handler only fires
            // when `selectedFilePath` actually changes (a real open), so it never
            // clobbers an in-progress ⌘/⇧ multi-select (those suppress the open).
            mutateSelectionAndPublish {
                $0.reveal(outcome.listSelection.flatMap { selectionRow(for: $0) })
            }
            if outcome.shouldMirrorListSelection {
                if outcome.shouldSuppressAnnouncement {
                    selectionAnnouncementGate.arm()
                }
                selectionRevisionGate.arm(for: outcome.listSelection)
                listSelection = outcome.listSelection
            }
        }
        // Search, graph, link, template, and command navigation does not pass
        // through the List's pointer/key transition. Preserve that newer user
        // agency as its own monotone edge, including a same-file re-open that
        // produces no `selectedFilePath` change at all. The path mirror above
        // remains neutral and owns only the visible single-row projection.
        .onChange(of: appState.sidebarSelectionIntentRevision) { _, _ in
            mutateSelectionAndPublish {
                $0.noteExternalNavigationIntent()
            }
        }
        }  // ScrollViewReader
    }

    /// The tree node (file or folder) currently selected — the single-item
    /// command target (⌘⌫ delete, move, Reveal, Copy Path, Rename). FAIL-CLOSED
    /// (#852, Codex round-4 finding 1): resolves through `pathValidatedNode`, so
    /// if `listSelection`'s id now points at a DIFFERENT path than the one it was
    /// selected at (a reused SQLite dir id repointed by an external replace), it
    /// resolves to NOTHING rather than the unrelated folder — a single-item
    /// delete/move can NEVER act on a reused id even if `listSelection` lags the
    /// reconcile.
    private var selectedTreeNode: TreeNode? {
        guard let focused = selectionModel.focused else { return nil }
        return pathValidatedNode(for: focused)
    }

    /// Resolve `rowID` to its live `TreeNode`, but only if its id still resolves
    /// to the path snapshotted by the selection model — the fail-closed guard
    /// against a reused directory id (#852, Codex round-4 finding 1). A row with
    /// no snapshot (a fresh, not-yet-snapshotted selection) resolves normally.
    private func pathValidatedNode(for rowID: RowID) -> TreeNode? {
        guard case let .node(id) = rowID, let node = tree.node(for: id) else { return nil }
        return selectionModel.isSelected(rowID, currentPath: node.path) ? node : nil
    }

    // MARK: - Tap dispatch (#852)

    /// Flattened visible real rows are the single input to pointer/keyboard
    /// transitions, visual selection, later multi-open/drag, and operations.
    private var visibleSelectionRows: [SelectionRow] {
        tree.visibleRows.map {
            SelectionRow(
                identity: .node($0.nodeID),
                path: $0.path,
                isDirectory: $0.isDirectory,
                isMarkdown: $0.isMarkdown)
        }
    }

    @discardableResult
    private func handleSelectionKeyAction(
        _ action: SelectionKeyAction,
        proxy: ScrollViewProxy
    ) -> Bool {
        if action == .openSelected { return requestOpenSelected() }
        let outcome = Self.keyboardSelectionOutcome(
            action: action,
            model: selectionModel,
            currentListSelection: listSelection,
            visibleRows: visibleSelectionRows)
        guard outcome.handled else { return false }

        mutateSelectionAndPublish { $0 = outcome.model }
        if outcome.shouldMirrorListSelection {
            suppressOpenForSelectionChange = true
            selectionAnnouncementGate.arm()
            selectionRevisionGate.arm(for: outcome.listSelection)
            listSelection = outcome.listSelection
            if let focus = outcome.listSelection {
                proxy.scrollTo(focus, anchor: .center)
            }
        }
        if outcome.changed {
            let count = outcome.visibleSelectedCount
            let message = count == 0
                ? "No items selected"
                : "\(count) \(count == 1 ? "item" : "items") selected"
            postAccessibilityAnnouncement(message, priority: .medium)
        }
        return true
    }

    @discardableResult
    private func requestOpenSelected() -> Bool {
        let projection = appState.sidebarActionProjection(surface: .keyboard)
        do {
            return try Self.invokeSidebarKeyboardAction(
                id: SlateCommandID.sidebarOpen,
                projection: projection,
                dispatch: { intent in
                    _ = try appState.dispatchSidebarAction(intent)
                })
        } catch {
            appState.postMutationAnnouncement(error.sidebarActionAnnouncement)
            return true
        }
    }

    private var activeOpenSelection: OpenSelectionRequest? {
        guard case .open(let request)? = appState.activeBatchAlertPresentation else {
            return nil
        }
        return request
    }

    private var activeOpenSelectionPresented: Binding<Bool> {
        Binding(
            get: { activeOpenSelection != nil },
            set: { _ in }
        )
    }

    /// The selected rows currently VISIBLE + path-valid, PRE-dedup — "what the
    /// user sees selected". Its COUNT drives multi-select MODE + the count
    /// announcement (#852, Codex findings 3 & 4).
    private var prunedSelectedNodes: [AppState.TreeSelection] {
        selectionModel.selectedVisibleRows(in: visibleSelectionRows).map {
            AppState.TreeSelection(path: $0.path, isDirectory: $0.isDirectory)
        }
    }

    /// Multi-select MODE = ≥2 VISUALLY selected rows (path-valid). A folder + a
    /// child inside it remain one captured batch action; core owns projection
    /// and returns `CoveredBySelectedFolder` in its typed skip ledger.
    private var hasMultiSelection: Bool { prunedSelectedNodes.count >= 2 }

    /// Complete stable visible capture submitted to core. Swift deliberately
    /// does not prune descendants or predicted no-ops from a batch request.
    private var selectedNodesForBatch: [AppState.TreeSelection] {
        prunedSelectedNodes
    }

    private var preferredBatchFocusPath: String? {
        guard let focused = selectionModel.focused else { return nil }
        return selectionModel.selectedVisibleRows(in: visibleSelectionRows)
            .first(where: { $0.identity == focused })?.path
    }

    private func selectionRow(for rowID: RowID) -> SelectionRow? {
        guard case let .node(nodeID) = rowID else { return nil }
        switch nodeID {
        case let .file(path):
            guard let node = tree.node(for: nodeID) else { return nil }
            return SelectionRow(
                identity: rowID, path: path, isDirectory: false,
                isMarkdown: node.isMarkdown)
        case .dir:
            guard let node = tree.node(for: nodeID) else { return nil }
            return SelectionRow(
                identity: rowID, path: node.path, isDirectory: true,
                isMarkdown: false)
        }
    }

    /// Keep the native List carrier synchronized after the pure model has
    /// unconditionally normalized a programmatic selection.
    private func mirrorProgrammaticSelection(_ rowID: RowID?) {
        mutateSelectionAndPublish {
            $0.reveal(rowID.flatMap { selectionRow(for: $0) })
        }
        selectionRevisionGate.arm(for: selectionModel.focused)
        listSelection = selectionModel.focused
    }

    /// A Command-click opened a background tab, so return focus to the active
    /// document. Update the semantic model and AppState snapshot before the
    /// native List carrier, including when the carrier assignment is same-value.
    private func revertCommandClickSelection(to rowID: RowID?) {
        selectionAnnouncementGate.arm()
        mutateSelectionAndPublish {
            $0.reveal(rowID.flatMap { selectionRow(for: $0) })
        }
        selectionRevisionGate.arm(for: selectionModel.focused)
        listSelection = selectionModel.focused
    }

    /// The CURRENT path a selected RowID resolves to: for a file the path IS the
    /// id (stable); for a dir, resolved live through the tree (its id can be
    /// reused). Nil for a placeholder or an unmaterialized level.
    private func resolvedPath(of rowID: RowID) -> String? {
        guard case let .node(id) = rowID else { return nil }
        if case let .file(path) = id { return path }
        return tree.node(for: id)?.path
    }

    /// The GENERIC reconcile for UNEXPLAINED tree changes (an external replace /
    /// rescan, driven by `.onChange(of: tree.visibleRows)` once the refetch has
    /// landed so ids resolve to the NEW reality). Drops any selected id now
    /// resolving to a DIFFERENT path than its snapshot (a reused dir id repointed
    /// at an unrelated folder). Known in-app rename/move is handled separately by
    /// `remapSelectionForKnownMove` BEFORE this ever sees a mismatch.
    ///
    /// #852 Codex round-4 finding 1: if the dropped id was the FOCUS, re-anchor
    /// `listSelection` to a surviving path-valid row (or clear it) and suppress
    /// the open — otherwise the native `List` keeps highlighting the reused
    /// folder and `selectedTreeNode` would resolve it (the fail-closed guard is
    /// the backstop; this is the primary fix).
    private func reconcileSelectionAfterTreeChange() {
        // The value model reconciles the independent anchor first, then selected
        // snapshots, and finally focus. Nil resolution remains transient.
        mutateSelectionAndPublish {
            $0.reconcile(
                visibleRows: visibleSelectionRows,
                resolveCurrentPath: { resolvedPath(of: $0) })
        }
        if listSelection != selectionModel.focused {
            suppressOpenForSelectionChange = true
            selectionRevisionGate.arm(for: selectionModel.focused)
            listSelection = selectionModel.focused
        }
    }

    /// Remap the selection across a KNOWN in-app rename/move (#852, Codex
    /// round-4 finding 2) — a directory keeps its id but changes its path, so the
    /// generic reconcile would misread it as a reused id and DROP it. Instead we
    /// rewrite the affected entries' snapshots (and file RowIDs) to the new
    /// paths, keeping them selected + anchored under the new path. The mirror +
    /// native focus are re-synced by `handleTreeMutation` explicitly (a dir's
    /// same-id focus is a same-value no-op that wouldn't fire `.onChange`).
    private func remapSelectionForKnownMove(oldPath: String, newPath: String) {
        // #852 (Codex round-5): a DESELECTED anchor (with an empty selection) can
        // still need remapping under a moved folder, so don't early-return on an
        // empty selection when an anchor is present.
        guard !selectionModel.selected.isEmpty || selectionModel.rangeAnchor != nil else { return }
        mutateSelectionAndPublish {
            $0.remapKnownMoves(
                [SelectionModel.KnownMove(oldPath: oldPath, newPath: newPath)],
                identityForRemappedPath: Self.remappedSelectionIdentity)
        }
        // A FILE rename/move re-keys the focus RowID (path-keyed) — reflect it,
        // suppressing the open so re-anchoring can't re-open. A folder keeps its
        // id, so this is a no-op (handled by applyPostMutationFocus + the mirror).
        if listSelection != selectionModel.focused {
            suppressOpenForSelectionChange = true
            selectionRevisionGate.arm(for: selectionModel.focused)
            listSelection = selectionModel.focused
        }
    }

    /// Whether `rowID` (currently at `currentPath`) is a selected batch member —
    /// drives the row's AX `.isSelected` trait so EVERY member of a multi-
    /// selection reads as selected to VoiceOver, not just the single keyboard-
    /// focus row (#852, Codex finding 1). Path-validated so a reused dir id
    /// pointing at an unrelated folder is NOT reported selected (Codex finding 4).
    private func isRowSelected(_ rowID: RowID, currentPath: String) -> Bool {
        selectionModel.isSelected(rowID, currentPath: currentPath)
    }

    /// Whether `rowID` should paint the custom multi-select fill: a selected
    /// batch member that is NOT the focus row. The focus keeps the native `List`
    /// selection highlight, so a SINGLE selection stays pixel-identical (its one
    /// member is the focus → no custom fill); only the EXTRA batch members get
    /// the selection-token wash, so every selected row is visibly selected
    /// (#852, Codex finding 1 — the safety fix: ⌘⌫ acts on rows the user can now
    /// see are selected). Path-validated like `isRowSelected` (Codex finding 4).
    private func isMultiSelectFill(_ rowID: RowID, currentPath: String) -> Bool {
        isRowSelected(rowID, currentPath: currentPath) && selectionModel.focused != rowID
    }

    /// A PLAIN row tap (no ⌘/⇧): collapse to a single selection of `row` and let
    /// the existing `.onChange(of: listSelection)` path open it (files) / mirror
    /// it (folders). Byte-identical to the pre-#852 single-select behavior, plus
    /// the `multiSelection`/anchor bookkeeping so batch state never lingers.
    private func applyPlainSelection(_ row: RowID) {
        // #852 (Codex finding 2b): if `row` is ALREADY the focus, `listSelection
        // = row` is a no-op that never fires `.onChange(of: listSelection)`, so a
        // plain click on the already-focused (but not-open, because a multi-
        // select had suppressed the open) row would NOT open it. Detect that and
        // open it explicitly, mirroring the collapse-open in
        // `applyMultiSelectClick`. (`openFile` guards `selectedFilePath != path`,
        // so re-clicking the already-open file stays a no-op — no double-open.)
        guard let selectedRow = selectionRow(for: row) else { return }
        let sameFocus = (selectionModel.focused == row)
        mutateSelectionAndPublish {
            $0.applyPointerClick(
                .plain,
                row: selectedRow,
                visibleRows: visibleSelectionRows)
        }
        selectionRevisionGate.arm(for: selectionModel.focused)
        listSelection = selectionModel.focused
        if sameFocus, case let .node(.file(path)) = row,
            !BaseExactIdentity.matches(appState.selectedFilePath, path)
        {
            appState.openFile(
                path,
                target: .currentTab,
                advancesSidebarSelectionRevision: false)
        }
    }

    /// Route the ⌘⌫ Move-to-Trash chord (#852): the WHOLE multi-selection when
    /// ≥2 rows are selected (a batch action, with its own #860-style
    /// confirmation via `requestBatchDelete`), else the single focused node
    /// through the existing per-item funnel. Move-to-Trash is one of the two
    /// batch actions the issue arms, so it acts on the Set — Reveal / Copy Path /
    /// Rename stay single-item (they read the focus mirror).
    private func requestDeleteFromKeyboard() {
        let projection = appState.sidebarActionProjection(surface: .keyboard)
        do {
            _ = try Self.invokeSidebarKeyboardAction(
                id: SlateCommandID.deleteEntry,
                projection: projection,
                dispatch: { intent in
                    _ = try appState.dispatchSidebarAction(intent)
                })
        } catch {
            appState.postMutationAnnouncement(error.sidebarActionAnnouncement)
        }
    }

    /// A ⌘/⇧ multi-select tap: fold it through the pure model, update the set +
    /// anchor + focus, and SUPPRESS the onChange open (the live ⌘ would otherwise
    /// mis-route the focus move to a new tab). A modifier gesture never opens a
    /// row, including when it collapses a batch selection back to one item;
    /// plain clicks, keyboard Return, and programmatic opens keep their existing
    /// behavior.
    private func applyMultiSelectClick(_ clicked: RowID, click: SelectionClick) {
        guard let clickedRow = selectionRow(for: clicked) else { return }
        mutateSelectionAndPublish {
            $0.applyPointerClick(
                click,
                row: clickedRow,
                visibleRows: visibleSelectionRows)
        }
        // Suppress the onChange-driven open for every modifier transition.
        // #852 red-team: ONLY arm the one-shot when the focus assignment will
        // ACTUALLY fire `.onChange`. A same-value assignment (e.g. ⌘-removing
        // an upper row keeps focus on the bottom-most still-selected row, or a
        // repeat ⇧-click of the range endpoint) is a no-op that never fires
        // onChange, so an unconditional flag would STRAND true — later
        // swallowing a legitimate single-click open AND leaving `multiSelection`
        // uncollapsed so a keyboard-nav'd ⌘⌫ trashes the wrong rows (data loss).
        // When focus is unchanged there is nothing to suppress: `multiSelection`
        // is already set directly above. Mirrors the
        // `suppressOpenForPostMutationFocus` guard.
        if listSelection != selectionModel.focused {
            suppressOpenForSelectionChange = true
        }
        selectionRevisionGate.arm(for: selectionModel.focused)
        listSelection = selectionModel.focused
        // #852 VoiceOver (point 8): the selection COUNT must be discoverable.
        // Multi (≥2) and emptied (0) cases need an explicit count announcement.
        // A modifier collapse to one is silent because it intentionally does
        // not open the row.
        // #852 (Codex finding 3): announce the VISUALLY-selected count (pruned,
        // path-valid, PRE-dedup) so it matches what the user sees highlighted —
        // a folder + a child read "2 items selected" (the op then targets [F],
        // labelled "Move 1 Item"). Both accurate.
        let visibleCount = prunedSelectedNodes.count
        if visibleCount >= 2 {
            postAccessibilityAnnouncement(
                "\(visibleCount) items selected", priority: .medium)
        } else if selectionModel.selected.isEmpty {
            postAccessibilityAnnouncement("No items selected", priority: .medium)
        }
    }

    /// Mirror the current row selection (file OR folder) into
    /// `appState.treeSelectedNode`, the single source the file-management
    /// commands (rename/move/delete/new) read. Placeholder rows or no selection
    /// clear it. Post-update mutation point (#448 discipline): this runs inside
    /// the `.onChange` closure, not during the list's update pass.
    private func mirrorTreeSelectionToAppState(_ selection: RowID?) {
        // FAIL-CLOSED (#852, Codex round-4 finding 1): resolve through
        // `pathValidatedNode` so a reused dir id never mirrors an UNRELATED
        // folder to AppState's single-item command target (Reveal / Copy Path /
        // ⌘⇧M Move read `treeSelectedNode`). The sole exception is an installed,
        // ownership-checked Delete fallback whose level is still refetching.
        let resolvedSelection = selection.flatMap { selection in
            pathValidatedNode(for: selection).map {
                AppState.TreeSelection(path: $0.path, isDirectory: $0.isDirectory)
            }
        }
        Self.publishGuardAwareTreeSelectionMirror(
            pending: pendingPostMutationFocus,
            model: selectionModel,
            currentSessionIdentity: appState.currentSession.map(ObjectIdentifier.init),
            currentMutationToken: appState.treeMutation?.token,
            resolvedSelection: resolvedSelection,
            appState: appState)
    }

    /// Refresh the tree after a structural mutation (U2-5) and move the
    /// post-mutation selection (U2-6). Invalidates exactly the affected levels,
    /// then re-anchors `listSelection` on the post-mutation target and scrolls
    /// it into view.
    private func handleTreeMutation(proxy: ScrollViewProxy) {
        guard let mutation = appState.treeMutation else { return }
        if let generation = mutation.importOwnedScanGeneration {
            appState.acknowledgeImportOwnedScanCompletion(generation)
        }
        // A newer structural landing supersedes any focus still waiting on the
        // previous mutation's destination refetch.
        pendingImportSelection = nil
        pendingBatchFocus = nil
        Self.cancelPendingPostMutationFocus(&pendingPostMutationFocus)
        let preMutationRows = visibleSelectionRows
        let liveFocusPath = selectionModel.focused.flatMap { identity -> String? in
            guard let snapshot = selectionModel.selectionPathSnapshots[identity],
                preMutationRows.contains(where: {
                    $0.identity == identity && $0.path == snapshot
                })
            else { return nil }
            return snapshot
        }
        var batchMoveIndex: SelectionModel.KnownMoveIndex?
        var batchRemovalIndex: SelectionModel.KnownRemovalIndex?
        var batchMoveFocus: BatchMoveFocusPlan?
        var componentVisits = 0
        switch mutation.kind {
        case .batchMove(let standing, _),
            .importBatch(_, let standing, _):
            let index = SelectionModel.KnownMoveIndex(standing.map {
                .init(
                    oldPath: $0.oldPath, newPath: $0.newPath,
                    isDirectory: $0.isDirectory)
            })
            batchMoveIndex = index
            batchMoveFocus = Self.batchMoveFocusPlan(
                liveFocusPath: liveFocusPath,
                liveFocusIsResolvable: liveFocusPath != nil,
                preferredFocusPath: mutation.preferredFocusPath,
                firstStandingPath: standing.first?.newPath,
                using: index,
                componentVisits: &componentVisits)
        case .batchTrash(let trashed):
            batchRemovalIndex = SelectionModel.KnownRemovalIndex(trashed.map {
                .init(path: $0.path, isDirectory: $0.isDirectory)
            })
        default:
            break
        }
        // For a DELETE, the focus target (next sibling / prev / parent) must be
        // read from the tree BEFORE the level is dropped — capture it first.
        var preInvalidationDeleteTarget: SelectionRow?
        if case let .delete(path, parent, _) = mutation.kind {
            preInvalidationDeleteTarget = tree.deleteFocusTarget(
                deletedPath: path, parentPath: parent
            ).flatMap { selectionRow(for: .node($0)) }
        }
        // Invalidate each dirtied level. A move dirties two (source +
        // destination); everything else dirties one. `nil` = the root level.
        if mutation.requiresRescan {
            tree.authoritativeTreeInvalidation()
        } else {
            for parent in mutation.affectedParents {
                if parent == nil {
                    tree.rootLevelInvalidation()
                } else if let parentID = parentNodeID(forPath: parent) {
                    tree.treeInvalidation(parent: parentID)
                }
            }
        }
        // Expansion follows the entity (Codex round 4): invalidation just
        // demoted the affected subtree to OLD paths — rewrite them for
        // rename/move so the reloaded level re-promotes at the NEW path;
        // drop them for delete so tombstones can't eat cap slots.
        switch mutation.kind {
        case let .rename(oldPath, newPath),
            let .move(oldPath, newPath, _, _):
            tree.remapExpansion(fromPrefix: oldPath, to: newPath)
        case let .delete(path, _, wasDirectory) where wasDirectory:
            tree.removeExpansion(underPrefix: path)
        case .batchMove, .importBatch:
            if let batchMoveIndex {
                tree.remapExpansions(
                    using: batchMoveIndex, componentVisits: &componentVisits)
            }
        case .batchTrash:
            if let batchRemovalIndex {
                tree.removeExpansions(
                    using: batchRemovalIndex, componentVisits: &componentVisits)
            }
        case .batchReconcile:
            break
        default:
            break
        }
        // Explicit mirror sync (Codex round 6): production rename/move
        // PRESERVES the dir id, so the reconciled `expanded` set can be
        // identical pre/post and `.onChange(of: tree.expanded)` never
        // fires — the path ledger changed even though the id set didn't.
        let paths = tree.expandedDirPaths
        if appState.treeExpandedDirPaths != paths {
            appState.treeExpandedDirPaths = paths
        }
        // #852 (Codex round-4 finding 2): this is a KNOWN in-app mutation, so we
        // hold the old→new mapping — REMAP the selection's snapshots (rename/move
        // preserve a dir's id but change its path) instead of letting the generic
        // reconcile misread the intentional path change as a reused id and drop
        // the still-selected folder. A delete/create doesn't repoint an existing
        // selection identity, so it needs no remap (a delete's focus move + the
        // onChange collapse handle it; the deleted rows fall out of the visible
        // tree and are pruned lazily).
        switch mutation.kind {
        case let .rename(oldPath, newPath), let .move(oldPath, newPath, _, _):
            remapSelectionForKnownMove(oldPath: oldPath, newPath: newPath)
        case .batchMove, .importBatch:
            // Remap + final focus publish as one atomic snapshot below.
            break
        case .batchTrash:
            if let batchRemovalIndex {
                mutateSelectionAndPublish(visibleRows: preMutationRows) {
                    $0.removeKnownItems(
                        using: batchRemovalIndex,
                        preferredFocusPath: mutation.preferredFocusPath,
                        visibleRows: preMutationRows,
                        componentVisits: &componentVisits)
                }
            }
        case .delete, .createFolder, .createNote, .batchReconcile:
            break
        }
        switch mutation.kind {
        case .batchMove:
            applyBatchMoveFocus(
                batchMoveFocus,
                moveIndex: batchMoveIndex,
                componentVisits: &componentVisits,
                proxy: proxy)
        case .importBatch(let materialized, _, _):
            applyImportSelection(
                materialized: materialized,
                moveIndex: batchMoveIndex,
                mutation: mutation,
                componentVisits: &componentVisits,
                proxy: proxy)
        case .batchTrash:
            applyBatchSelectionFocus(proxy: proxy)
        case .batchReconcile:
            break
        default:
            applyPostMutationFocus(
                mutation, deleteTarget: preInvalidationDeleteTarget, proxy: proxy)
        }
        // The AppState mirror is re-synced on `.onChange(of: tree.visibleRows)`
        // once the refetch lands (a folder rename/move preserves the dir id, so
        // `applyPostMutationFocus`'s `listSelection` assignment is a same-value
        // no-op that never fires `.onChange` — the mirror must be refreshed
        // against the NEW path, and node resolution is only reliable post-refetch).
    }

    private func applyImportSelection(
        materialized: [SidebarImportMaterializedResult],
        moveIndex: SelectionModel.KnownMoveIndex?,
        mutation: AppState.TreeMutation,
        componentVisits: inout Int,
        proxy: ScrollViewProxy
    ) {
        guard !materialized.isEmpty,
            let expectedRevision = mutation.selectionRevision,
            let ownerToken = mutation.structuralMutationToken,
            let sessionIdentity = tree.sessionIdentity,
            selectionModel.selectionRevision == expectedRevision,
            appState.currentSession.map(ObjectIdentifier.init) == sessionIdentity,
            appState.currentStructuralMutationGeneration == ownerToken
        else { return }

        var deferredModel = selectionModel
        if let moveIndex {
            deferredModel = Self.deferredBatchSelectionModel(
                from: deferredModel,
                using: moveIndex,
                componentVisits: &componentVisits)
        }
        let paths = materialized.map(\.path)
        for path in paths { tree.ensureAncestorsExpanded(forPath: path) }
        let pending = PendingImportSelection(
            paths: paths,
            expectedSelectionRevision: expectedRevision,
            deferredModel: deferredModel,
            ownerSessionIdentity: sessionIdentity,
            ownerStructuralMutationToken: ownerToken,
            treeMutationToken: mutation.token)
        guard let rows = materializedImportRows(for: paths) else {
            pendingImportSelection = pending
            return
        }
        installImportSelection(pending, rows: rows, proxy: proxy)
    }

    private func materializedImportRows(for paths: [String]) -> [SelectionRow]? {
        var rows: [SelectionRow] = []
        rows.reserveCapacity(paths.count)
        for path in paths {
            guard let target = tree.focusTarget(forPath: path),
                let row = selectionRow(for: .node(target))
            else { return nil }
            rows.append(row)
        }
        return rows
    }

    private func importSelectionGuardsMatch(
        _ pending: PendingImportSelection
    ) -> Bool {
        tree.sessionIdentity == pending.ownerSessionIdentity
            && appState.currentSession.map(ObjectIdentifier.init)
                == pending.ownerSessionIdentity
            && appState.currentStructuralMutationGeneration
                == pending.ownerStructuralMutationToken
            && appState.treeMutation?.token == pending.treeMutationToken
            && selectionModel.selectionRevision
                == pending.expectedSelectionRevision
    }

    private func installImportSelection(
        _ pending: PendingImportSelection,
        rows: [SelectionRow],
        proxy: ScrollViewProxy
    ) {
        guard importSelectionGuardsMatch(pending) else { return }
        Self.mutateSelectionAndPublish(
            model: &selectionModel,
            capturedSessionIdentity: pending.ownerSessionIdentity,
            visibleRows: visibleSelectionRows,
            appState: appState
        ) {
            $0 = pending.deferredModel
            _ = $0.selectImportedResults(
                rows,
                ifSelectionRevisionIs: pending.expectedSelectionRevision)
        }
        applyBatchSelectionFocus(proxy: proxy)
    }

    @discardableResult
    private func restorePendingImportSelection(proxy: ScrollViewProxy) -> Bool {
        guard let pending = pendingImportSelection else { return false }
        guard importSelectionGuardsMatch(pending) else {
            pendingImportSelection = nil
            return false
        }
        for path in pending.paths { tree.ensureAncestorsExpanded(forPath: path) }
        guard let rows = materializedImportRows(for: pending.paths) else {
            return true
        }
        pendingImportSelection = nil
        installImportSelection(pending, rows: rows, proxy: proxy)
        return true
    }

    private func applyBatchMoveFocus(
        _ plan: BatchMoveFocusPlan?,
        moveIndex: SelectionModel.KnownMoveIndex?,
        componentVisits: inout Int,
        proxy: ScrollViewProxy
    ) {
        let expectedFocusedPath = currentFocusedSnapshotPath
        let targetRow: SelectionRow?
        if let plan, let path = plan.path {
            if plan.shouldRevealMovedAncestry {
                tree.ensureAncestorsExpanded(forPath: path)
            }
            targetRow = tree.focusTarget(forPath: path).flatMap {
                selectionRow(for: .node($0))
            }
        } else {
            targetRow = nil
        }
        pendingBatchFocus = Self.applyOrDeferBatchMoveSelection(
            plan: plan,
            moveIndex: moveIndex,
            targetRow: targetRow,
            expectedFocusedPath: expectedFocusedPath,
            model: &selectionModel,
            capturedSessionIdentity: tree.sessionIdentity,
            visibleRows: visibleSelectionRows,
            appState: appState,
            componentVisits: &componentVisits)
        if pendingBatchFocus != nil {
            return
        }
        applyBatchSelectionFocus(proxy: proxy)
    }

    private var currentFocusedSnapshotPath: String? {
        selectionModel.focused.flatMap {
            selectionModel.selectionPathSnapshots[$0]
        }
    }

    @discardableResult
    private func restorePendingBatchFocus(proxy: ScrollViewProxy) -> Bool {
        guard let pending = pendingBatchFocus,
            let path = pending.plan.path
        else { return false }
        if pending.plan.shouldRevealMovedAncestry {
            tree.ensureAncestorsExpanded(forPath: path)
        }
        let target = tree.focusTarget(forPath: path)
        switch Self.pendingBatchFocusDisposition(
            pending,
            currentFocusedPath: currentFocusedSnapshotPath,
            targetIsMaterialized: target != nil
        ) {
        case .wait:
            return true
        case .cancel:
            pendingBatchFocus = nil
            return false
        case .restore:
            pendingBatchFocus = nil
            guard let target,
                let row = selectionRow(for: .node(target))
            else { return true }
            Self.installPendingBatchFocus(
                pending,
                targetRow: row,
                model: &selectionModel,
                capturedSessionIdentity: tree.sessionIdentity,
                visibleRows: visibleSelectionRows,
                appState: appState)
            applyBatchSelectionFocus(proxy: proxy)
            return true
        }
    }

    /// One native/SwiftUI focus edge for a complete batch transition. The
    /// selection model is already authoritative; this carrier update is
    /// suppression-armed so a fallback row is never auto-opened.
    private func applyBatchSelectionFocus(proxy: ScrollViewProxy) {
        let target = selectionModel.focused
        if listSelection != target {
            suppressOpenForSelectionChange = true
            selectionAnnouncementGate.arm()
            selectionRevisionGate.arm(for: target)
            listSelection = target
        }
        if let target {
            proxy.scrollTo(target, anchor: .center)
        }
    }

    /// Move the tree selection to the post-mutation target and scroll it into
    /// view (spec §U2-6). Focus rules:
    ///   - create-folder / create-note → the new row,
    ///   - rename → the renamed row (kept selected at its new path),
    ///   - move → the moved row at its new location (ancestors auto-expanded),
    ///   - delete → next sibling, else previous, else parent (never window-root).
    /// VoiceOver focus follows list selection (verified in the runbook pass).
    private func applyPostMutationFocus(
        _ mutation: AppState.TreeMutation,
        deleteTarget: SelectionRow?,
        proxy: ScrollViewProxy
    ) {
        let pending: PendingPostMutationFocus?
        switch mutation.kind {
        case let .createFolder(path), let .createNote(path):
            pending = PendingPostMutationFocus(
                targetPath: path,
                capturedDeleteRow: nil,
                suppressOpen: false,
                ownerSessionIdentity: tree.sessionIdentity,
                ownerMutationToken: mutation.token)
        case let .rename(_, newPath):
            pending = PendingPostMutationFocus(
                targetPath: newPath,
                capturedDeleteRow: nil,
                suppressOpen: false,
                ownerSessionIdentity: tree.sessionIdentity,
                ownerMutationToken: mutation.token)
        case let .move(_, newPath, _, _):
            // Reveal the moved node's new home, then resolve it.
            tree.ensureAncestorsExpanded(forPath: newPath)
            pending = PendingPostMutationFocus(
                targetPath: newPath,
                capturedDeleteRow: nil,
                suppressOpen: false,
                ownerSessionIdentity: tree.sessionIdentity,
                ownerMutationToken: mutation.token)
        case .delete:
            pending = deleteTarget.map {
                PendingPostMutationFocus(
                    targetPath: nil,
                    capturedDeleteRow: $0,
                    suppressOpen: true,
                    ownerSessionIdentity: tree.sessionIdentity,
                    ownerMutationToken: mutation.token)
            }
        case .batchMove, .batchTrash, .importBatch, .batchReconcile:
            pending = nil
        }
        guard let pending else {
            pendingPostMutationFocus = nil
            suppressOpenForPostMutationFocus = false
            return
        }
        if !applyPendingPostMutationFocus(pending, proxy: proxy) {
            pendingPostMutationFocus = pending
        }
    }

    /// Finish a create/rename/move only once its destination row is materialized.
    /// Delete is different: its deterministic fallback row was captured before
    /// invalidation, so a failed child refetch cannot erase the focus target.
    @discardableResult
    private func applyPendingPostMutationFocus(
        _ pending: PendingPostMutationFocus,
        proxy: ScrollViewProxy
    ) -> Bool {
        let visibleRows = visibleSelectionRows
        guard let targetRow = Self.installPendingPostMutationFocus(
            pending,
            model: &selectionModel,
            capturedSessionIdentity: tree.sessionIdentity,
            visibleRows: visibleRows,
            appState: appState)
        else { return false }

        pendingPostMutationFocus = Self.pendingPostMutationFocusAfterInstall(
            pending,
            installedRow: targetRow,
            visibleRows: visibleRows)
        applyPostMutationSelectionCarrier(
            targetRow,
            suppressOpen: pending.suppressOpen,
            proxy: proxy)
        return true
    }

    /// Update only the native List carrier after the semantic model and
    /// AppState snapshot were installed. The one-shot general suppression makes
    /// the carrier callback mirror/announce and return without a second publish.
    private func applyPostMutationSelectionCarrier(
        _ targetRow: SelectionRow,
        suppressOpen: Bool,
        proxy: ScrollViewProxy
    ) {
        // After a delete the tab flips to the missing-file error state (U2-5);
        // moving the tree highlight to the sibling must NOT open it. Other
        // structural changes already opened/retargeted the active item. Arm the
        // general semantic-carrier suppression before assigning the native List
        // binding so its callback cannot publish the same selection a second
        // time. The callback clears both one-shot flags before returning.
        suppressOpenForPostMutationFocus = suppressOpen
        if listSelection != targetRow.identity {
            suppressOpenForSelectionChange = true
            selectionRevisionGate.arm(for: targetRow.identity)
            listSelection = targetRow.identity
        } else {
            // Selection didn't change (already there) → the open-suppression
            // flag would leak to the next real selection. Clear it now.
            suppressOpenForPostMutationFocus = false
        }
        // Scroll it into view. Reduce Motion is respected by SwiftUI's implicit
        // handling; `scrollTo` itself is instantaneous here (no explicit
        // animation), matching the outline pane's behavior.
        proxy.scrollTo(targetRow.identity, anchor: .center)
    }

    /// A child-level invalidation may publish a loading/error row before the
    /// final destination. Treat the pending focus as owning reconciliation until
    /// the target materializes; a newer structural mutation clears it up-front.
    @discardableResult
    private func restorePendingPostMutationFocus(proxy: ScrollViewProxy) -> Bool {
        guard let pending = pendingPostMutationFocus else { return false }
        let suppressOpen = pending.suppressOpen
        let outcome = Self.restorePendingPostMutationSelection(
            pending: &pendingPostMutationFocus,
            model: &selectionModel,
            capturedSessionIdentity: tree.sessionIdentity,
            visibleRows: visibleSelectionRows,
            appState: appState,
            currentSessionIdentity: appState.currentSession.map(ObjectIdentifier.init),
            currentMutationToken: appState.treeMutation?.token,
            resolveCurrentRow: { selectionRow(for: $0) })
        switch outcome {
        case .installed(let installedRow):
            applyPostMutationSelectionCarrier(
                installedRow,
                suppressOpen: suppressOpen,
                proxy: proxy)
            return true
        case .wait, .clearedGuard:
            return true
        case .cancelAndReconcile:
            return false
        }
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
        case header(parentPath: String, header: SidebarTreeHeaderRow)

        var rowID: RowID {
            switch self {
            case let .node(node): return .node(node.nodeID)
            case let .loading(parent, _): return .loading(parent: parent)
            case let .error(parent, _, _, _):
                return .error(parent: parent ?? FileTreeViewModel.rootFetchKey)
            case let .header(parentPath, header):
                return .header(parentPath: parentPath, key: header.key)
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
            // FL-06: splice the Pinned/date-bucket header immediately above
            // the first file row of its run (nonselectable; see headerRow).
            if !node.isDirectory, let header = tree.headerRow(before: node.nodeID) {
                out.append(
                    .header(
                        parentPath: SidebarPins.folder(of: node.path),
                        header: header))
            }
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
        case let .header(_, header):
            sectionHeaderRow(header)
        }
    }

    /// One nonselectable FL-06 section header (Pinned or a date bucket).
    /// A real AX header (VO rotor-navigable), never a focus stop: it is
    /// absent from `visibleSelectionRows`, so arrow navigation skips it, and
    /// `selectionDisabled` blocks pointer selection.
    private func sectionHeaderRow(_ header: SidebarTreeHeaderRow) -> some View {
        HStack(spacing: Tokens.Spacing.xs) {
            Color.clear
                .frame(width: Self.indentWidth(for: header.depth), height: 0)
                .accessibilityHidden(true)
            Text(header.label)
                .font(Tokens.Typography.caption)
                .fontWeight(.semibold)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            Spacer(minLength: 0)
        }
        .padding(.top, Tokens.Spacing.xs)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(header.label)
        .accessibilityValue(Self.headerAccessibilityValue(for: header))
        .accessibilityAddTraits(.isHeader)
        .selectionDisabled(true)
    }

    /// Spoken count for a section header ("3 notes").
    static func headerAccessibilityValue(for header: SidebarTreeHeaderRow) -> String {
        header.fileCount == 1 ? "1 note" : "\(header.fileCount) notes"
    }

    /// Container summary for the tree list. Default organization stays
    /// silent; any non-default choice is named so a VO user hears the active
    /// order when entering the list (fl3 spec §FL3-1.6).
    static func treeAccessibilitySummary(
        for choice: SidebarOrganizationChoice
    ) -> String? {
        choice == .defaults ? nil : "Files. \(choice.sortAnnouncement)"
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

    private func sidebarSelectionItem(for node: TreeNode) -> SidebarSelectionItem {
        SidebarSelectionItem(
            path: node.path,
            isDirectory: node.isDirectory,
            isMarkdown: node.isMarkdown)
    }

    /// Contextual projections target a row-specific snapshot, so they cannot
    /// reuse AppState's published-selection projection wholesale. Merge the
    /// same action-specific reasons here, including Open's property-edit gate,
    /// without performing any render-time I/O.
    private var sidebarRowActionDisabledReasons: [String: String] {
        var reasons = appState.sidebarActionDisabledReasons
        if let propertyEditReason = appState.propertyEditNavigationDisabledReason {
            reasons[SlateCommandID.sidebarOpen] = propertyEditReason
        }
        return reasons
    }

    /// Row-targeted variant: the FL-06 pin/override reasons depend on the
    /// exact row a context menu or rotor targets, which may differ from the
    /// published selection the shared dictionary describes. The published
    /// selection's organization reasons are removed wholesale first — a
    /// reason keyed to the selected row must never leak onto a different
    /// right-clicked row and hide its applicable direction (round-3
    /// finding 2).
    private func sidebarRowActionDisabledReasons(
        for row: SidebarSelectionItem
    ) -> [String: String] {
        var reasons = sidebarRowActionDisabledReasons
        for id in SlateCommandID.sidebarOrganizationCommands {
            reasons[id] = nil
        }
        reasons.merge(
            appState.sidebarOrganizationActionReasons(target: row)
        ) { _, rowSpecific in rowSpecific }
        return reasons
    }

    /// The only context-menu/VoiceOver catalog renderer. Its projection has
    /// already omitted unavailable actions, so every emitted button owns one
    /// frozen intent and announces an activation-time rejection exactly once.
    @ViewBuilder
    private func sidebarCatalogActions(
        _ evaluations: [SidebarActionEvaluation],
        actionIDs: [String]? = nil
    ) -> some View {
        let orderedEvaluations = actionIDs?.compactMap { id in
            evaluations.first(where: { $0.id == id })
        } ?? evaluations
        ForEach(orderedEvaluations, id: \.id) { evaluation in
            Button(
                role: evaluation.definition.isDestructive ? .destructive : nil
            ) {
                guard let intent = evaluation.intent else { return }
                do {
                    _ = try appState.dispatchSidebarAction(intent)
                } catch {
                    appState.postMutationAnnouncement(error.sidebarActionAnnouncement)
                }
            } label: {
                evaluation.definition.symbol.label(evaluation.definition.label)
            }
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
        if let rename = renameOwner(for: node) {
            renameFieldRow(node, rename: rename)
        } else {
            let selected = isRowSelected(.node(node.nodeID), currentPath: node.path)
            let disabledReason = appState.structuralMutationDisabledReason
            let activate = {
                fileTreeFocused = true
                applyPlainSelection(.node(node.nodeID))
                tree.toggle(node)
            }
            SidebarFolderRowContent(
                node: node,
                isExpanded: isExpanded,
                isSelected: selected,
                selectionIsActive: nativeSelectionIsActive,
                isDropTargeted: Self.dropTargetIsActive(
                    dropTargetedNodes.contains(node.nodeID),
                    busy: appState.structuralMutationDisabledReason != nil))
            .contentShape(Rectangle())
            // A folder row SELECTS and toggles disclosure on activation
            // (pointer tap; Space, or Return when no selected file needs
            // opening, ride the List-level `.onKeyPress` above). Selecting
            // matters as much as toggling:
            // the tap must move `listSelection` onto the folder — exactly
            // as a file row's tap does — or the highlight (and therefore
            // the ⌘⌫ / Rename / Move target that `mirrorTreeSelectionToAppState`
            // derives from it) silently stays on the PREVIOUSLY selected
            // file. That stale-target trap is Finder-hostile: click a
            // folder, press ⌘⌫, and the file you clicked minutes ago is
            // what lands in the Trash.
            //
            // The row's AppKit gesture bridge handles the primary activation
            // and drag threshold without letting a possible drag collapse the
            // SwiftUI List selection. The button trait remains an honest role
            // for a folder that acts on activation. The named Expand/Collapse
            // rotor action gives VoiceOver users an explicit verb, and the AX
            // value states expanded/collapsed + item count + level.
            //
            // #852: a ⌘/⇧ tap is a MULTI-SELECT gesture, not an activation — it
            // toggles/ranges the folder in the batch set and does NOT disclose
            // (Finder doesn't expand folders you ⌘-click into a selection). A
            // plain tap is unchanged: select + toggle disclosure.
            .background {
                FileTreeRowDragSource(
                    makeDescriptor: { dragDescriptor(for: node) },
                    onClick: { modifiers in
                        let click = Self.selectionClick(from: modifiers)
                        if click == .plain {
                            activate()
                        } else {
                            fileTreeFocused = true
                            applyMultiSelectClick(.node(node.nodeID), click: click)
                        }
                    },
                    onDragEnded: { endDragSession(dropDestination: nil) })
            }
            .background {
                if isMultiSelectFill(.node(node.nodeID), currentPath: node.path) {
                    // #852 (Codex finding 1): paint the non-focus batch members
                    // so every selected folder reads as selected (the focus row
                    // keeps the native List highlight).
                    RoundedRectangle(cornerRadius: Tokens.Radius.control)
                        .fill(
                            Color(
                                nsColor: SidebarSelectionColors.background(
                                    active: nativeSelectionIsActive)))
                        .accessibilityHidden(true)
                }
            }
            .accessibilityElement(children: .combine)
            .accessibilityAddTraits(.isButton)
            // #852 (Codex finding 1): every batch member carries the selected
            // trait so VoiceOver announces it — not just the keyboard-focus row.
            .accessibilityAddTraits(
                selected ? .isSelected : [])
            .accessibilityLabel(node.name)
            .accessibilityValue(Self.folderAccessibilityValue(for: node, expanded: isExpanded))
            .accessibilityAction(.default) {
                activate()
            }
            .accessibilityAction(named: Text(isExpanded ? "Collapse" : "Expand")) {
                tree.toggle(node)
            }
            .accessibilityActions {
                if let publishedSnapshot = appState.sidebarSelectionSnapshot {
                    let projection = Self.sidebarRowActionProjection(
                        surface: .voiceOver,
                        row: sidebarSelectionItem(for: node),
                        publishedSnapshot: publishedSnapshot,
                        structuralMutationDisabledReason:
                            appState.structuralMutationDisabledReason,
                        actionDisabledReasons: sidebarRowActionDisabledReasons(
                            for: sidebarSelectionItem(for: node)))
                    sidebarCatalogActions(projection.evaluations)
                }
            }
            // Complex-gesture disclosure (WCAG 3.3.2): the row carries a
            // context menu, a drag source, and a drop target — say what each
            // does, not how to perform it.
            .accessibilityHint(
                Self.rowAccessibilityHint(
                    primaryAction: "Expands or collapses.",
                    idleHint: "Expands or collapses. Drag to move within Slate or copy to another app; drop items on it to copy external items in or move vault items inside. Other available actions are in the context menu.",
                    structuralDisabledReason: disabledReason))
            .help(disabledReason ?? node.path)
            .contextMenu {
                if let publishedSnapshot = appState.sidebarSelectionSnapshot {
                    let projection = Self.sidebarRowActionProjection(
                        surface: .contextMenu,
                        row: sidebarSelectionItem(for: node),
                        publishedSnapshot: publishedSnapshot,
                        structuralMutationDisabledReason:
                            appState.structuralMutationDisabledReason,
                        actionDisabledReasons: sidebarRowActionDisabledReasons(
                            for: sidebarSelectionItem(for: node)))
                    if projection.targetSnapshot.items.count == 1,
                        projection.targetSnapshot.items.first?.isDirectory == true
                    {
                        let creationIDs = [
                            SlateCommandID.newNote, SlateCommandID.newFolder,
                            SlateCommandID.newFromTemplate,
                        ]
                        let managementIDs = [
                            SlateCommandID.renameEntry, SlateCommandID.moveTo,
                        ]
                        let sortIDs = [
                            SlateCommandID.sidebarSortNameAsc,
                            SlateCommandID.sidebarSortNameDesc,
                            SlateCommandID.sidebarSortCreatedDesc,
                            SlateCommandID.sidebarSortCreatedAsc,
                            SlateCommandID.sidebarSortModifiedDesc,
                            SlateCommandID.sidebarSortModifiedAsc,
                            SlateCommandID.sidebarToggleDateGrouping,
                            SlateCommandID.sidebarUseVaultDefaultSort,
                        ]
                        let pinIDs = [SlateCommandID.sidebarUnpinAll]
                        let inspectionIDs = [
                            SlateCommandID.revealInFinder, SlateCommandID.copyPath,
                        ]
                        let trashIDs = [SlateCommandID.deleteEntry]
                        let availableIDs = Set(projection.evaluations.map(\.id))
                        let hasCreation = creationIDs.contains(where: availableIDs.contains)
                        let hasManagement = managementIDs.contains(where: availableIDs.contains)
                        let hasSort = sortIDs.contains(where: availableIDs.contains)
                        let hasPins = pinIDs.contains(where: availableIDs.contains)
                        let hasInspection = inspectionIDs.contains(where: availableIDs.contains)
                        let hasTrash = trashIDs.contains(where: availableIDs.contains)

                        if hasCreation {
                            Menu {
                                sidebarCatalogActions(
                                    projection.evaluations,
                                    actionIDs: [
                                        SlateCommandID.newNote, SlateCommandID.newFolder,
                                        SlateCommandID.newFromTemplate,
                                    ])
                            } label: {
                                SlateSymbol.newNote.label("New")
                            }
                        }
                        if hasManagement {
                            sidebarCatalogActions(
                                projection.evaluations,
                                actionIDs: [
                                    SlateCommandID.renameEntry, SlateCommandID.moveTo
                                ])
                        }
                        if (hasCreation || hasManagement) && (hasSort || hasPins) {
                            Divider()
                        }
                        if hasSort {
                            // FL-06 (#658): the per-folder Sort submenu — the
                            // same catalog radio set the View menu and toolbar
                            // project, targeting this exact folder.
                            Menu {
                                SidebarSortMenuItems(
                                    evaluations: projection.evaluations,
                                    effectiveChoice: appState.sidebarOrganization
                                        .prefs.effectiveChoice(forFolder: node.path)
                                        .normalized,
                                    dispatch: { intent in
                                        do {
                                            _ = try appState.dispatchSidebarAction(intent)
                                        } catch {
                                            appState.postMutationAnnouncement(
                                                error.sidebarActionAnnouncement)
                                        }
                                    })
                            } label: {
                                SlateSymbol.sortOrder.label("Sort")
                            }
                        }
                        if hasPins {
                            sidebarCatalogActions(
                                projection.evaluations,
                                actionIDs: [SlateCommandID.sidebarUnpinAll])
                        }
                        if (hasCreation || hasManagement || hasSort || hasPins)
                            && hasInspection
                        {
                            Divider()
                        }
                        if hasInspection {
                            sidebarCatalogActions(
                                projection.evaluations,
                                actionIDs: [
                                    SlateCommandID.revealInFinder,
                                    SlateCommandID.copyPath,
                                ])
                        }
                        if (hasCreation || hasManagement || hasSort || hasPins
                            || hasInspection) && hasTrash
                        {
                            Divider()
                        }
                        if hasTrash {
                            sidebarCatalogActions(
                                projection.evaluations,
                                actionIDs: [SlateCommandID.deleteEntry])
                        }
                    } else {
                        sidebarCatalogActions(projection.evaluations)
                    }
                }
            }
            // Drop target: dropping a file/folder onto this folder moves it
            // here. #851: per-row targeting drives the wash above and the
            // spring-load timer (hover ≥ 600ms on a collapsed folder
            // expands it so nested collapsed destinations are reachable).
            .onDrop(
                of: [Self.nodeUTType, Self.fileURLUTType],
                isTargeted: dropTargetBinding(for: node),
                perform: { providers in handleDrop(providers, into: node.path) })
        }
    }

    /// A file row presents FL-01's derived title/date/preview/count metadata at
    /// the configured density, indented to its depth. The extracted content
    /// view remains interaction-free; this assembly keeps every tree gesture.
    ///
    /// U2-5: swaps to an inline rename field when renaming; is a drag source and
    /// carries the file-management context menu alongside the open-in actions.
    @ViewBuilder
    private func fileRow(_ node: TreeNode) -> some View {
        if let rename = renameOwner(for: node) {
            renameFieldRow(node, rename: rename)
        } else if case let .file(fileState) = node.kind {
            let selected = isRowSelected(.node(node.nodeID), currentPath: node.path)
            let disabledReason = appState.structuralMutationDisabledReason
            let voiceOverProjection = appState.sidebarSelectionSnapshot.map {
                Self.sidebarRowActionProjection(
                    surface: .voiceOver,
                    row: sidebarSelectionItem(for: node),
                    publishedSnapshot: $0,
                    structuralMutationDisabledReason:
                        appState.structuralMutationDisabledReason,
                    actionDisabledReasons: sidebarRowActionDisabledReasons(
                        for: sidebarSelectionItem(for: node)))
            }
            let openPresentation = Self.fileRowOpenAccessibilityPresentation(
                openEvaluation: voiceOverProjection?.openEvaluation,
                availableHint: Self.fileRowAvailableOpenHint(
                    targetCount: voiceOverProjection?.targetSnapshot.items.count ?? 1,
                    idleGuidance: voiceOverProjection?.targetSnapshot.items.count == 1
                        ? "Drag to move it within Slate or copy it to another app. Open-in-new-tab, split, rename, duplicate, move, and delete actions are in the context menu."
                        : "Drag to move the selected files within Slate or copy them to another app. Other available actions are in the context menu.",
                    structuralDisabledReason: disabledReason),
                unavailableHint:
                    voiceOverProjection?.targetSnapshot.items.count == 1
                        ? "Drag to move it within Slate or copy it to another app. Other available actions are in the context menu."
                        : "Drag to move the selected items within Slate or copy them to another app. Other available actions are in the context menu.")
            let activate = {
                fileTreeFocused = true
                applyPlainSelection(.node(node.nodeID))
            }
            SidebarObservedFileRowContent(
                fileState: fileState,
                preferences: rowPreferences,
                isPinned: tree.isPinnedRow(node.nodeID),
                now: sidebarNow,
                depth: node.depth,
                isSelected: selected,
                selectionIsActive: nativeSelectionIsActive)
            .contentShape(Rectangle())
            // The AppKit gesture bridge delays the click decision until mouse
            // up, but begins a drag after the standard threshold. That keeps a
            // selected batch intact when dragging any member while preserving
            // normal click activation. A PLAIN click sets `listSelection`,
            // flowing through `.onChange(of: listSelection)` → openFile
            // (current tab). A file row that opens on activation has an honest
            // button role (its hint already says "Opens the note").
            //
            // #852: ⌘-click and ⇧-click are now MULTI-SELECT gestures (Finder /
            // Xcode parity — the affordance the whole PR adds), superseding
            // U1-5's ⌘-click-opens-a-new-tab on the tree. ⌘ toggles this row in
            // the batch set, ⇧ range-selects from the anchor; both SUPPRESS the
            // open while ≥2 rows are held (`applyMultiSelectClick`). New-tab open
            // stays reachable via the row context menu's "Open in New Tab" and
            // ⌘O quick-open — only the pointer shortcut's meaning changed. A
            // A modifier gesture never opens, even when it lands on one row;
            // plain click and Return keep their existing open behavior.
            .background {
                FileTreeRowDragSource(
                    makeDescriptor: { dragDescriptor(for: node) },
                    onClick: { modifiers in
                        let click = Self.selectionClick(from: modifiers)
                        if click == .plain {
                            activate()
                        } else {
                            fileTreeFocused = true
                            applyMultiSelectClick(.node(node.nodeID), click: click)
                        }
                    },
                    onDragEnded: { endDragSession(dropDestination: nil) })
            }
            // #852 (Codex finding 1): paint the non-focus batch members so every
            // selected file reads as selected (the focus keeps the native List
            // highlight, so single-select stays pixel-identical).
            .background {
                if isMultiSelectFill(.node(node.nodeID), currentPath: node.path) {
                    RoundedRectangle(cornerRadius: Tokens.Radius.control)
                        .fill(
                            Color(
                                nsColor: SidebarSelectionColors.background(
                                    active: nativeSelectionIsActive)))
                        .accessibilityHidden(true)
                }
            }
            // #852 (Codex finding 1): every batch member carries the selected
            // trait so VoiceOver announces it — not just the keyboard-focus row.
            .accessibilityAddTraits(
                isRowSelected(.node(node.nodeID), currentPath: node.path) ? .isSelected : [])
            .modifier(
                FileRowOpenAccessibilityModifier(
                    presentation: openPresentation,
                    dispatch: { openIntent in
                        do {
                            _ = try appState.dispatchSidebarAction(openIntent)
                        } catch {
                            appState.postMutationAnnouncement(
                                error.sidebarActionAnnouncement)
                        }
                    }))
            .accessibilityHint(openPresentation.hint)
            .help(disabledReason ?? node.path)
            .accessibilityActions {
                if let voiceOverProjection {
                    sidebarCatalogActions(voiceOverProjection.evaluations)
                }
            }
            .contextMenu {
                if let publishedSnapshot = appState.sidebarSelectionSnapshot {
                    let projection = Self.sidebarRowActionProjection(
                        surface: .contextMenu,
                        row: sidebarSelectionItem(for: node),
                        publishedSnapshot: publishedSnapshot,
                        structuralMutationDisabledReason:
                            appState.structuralMutationDisabledReason,
                        actionDisabledReasons: sidebarRowActionDisabledReasons(
                            for: sidebarSelectionItem(for: node)))
                    if projection.targetSnapshot.items.count == 1,
                        projection.targetSnapshot.items.first?.isDirectory == false
                    {
                        singleFileContextMenuGroups(
                            projection: projection, path: node.path)
                    } else {
                        sidebarCatalogActions(projection.evaluations)
                    }
                }
            }
        }
    }


    /// The single-FILE context-menu body (Open group, management, pins,
    /// reveal, copy, trash), shared verbatim between tree file rows and
    /// FL-09 filter-result rows — the spec's "same component" contract:
    /// every FL2 verb on a result row is this exact projection-driven
    /// menu, not a re-implementation.
    @ViewBuilder
    private func singleFileContextMenuGroups(
        projection: (
            targetSnapshot: SidebarSelectionSnapshot,
            evaluations: [SidebarActionEvaluation],
            openEvaluation: SidebarActionEvaluation?
        ),
        path: String
    ) -> some View {
        let managementIDs = [
            SlateCommandID.renameEntry,
            SlateCommandID.moveTo,
            SlateCommandID.duplicateEntry,
        ]
        let pinIDs = [
            SlateCommandID.sidebarPinNote,
            SlateCommandID.sidebarUnpinNote,
        ]
        let revealIDs = [SlateCommandID.revealInFinder]
        let copyIDs = [
            SlateCommandID.copyPath,
            SlateCommandID.sidebarCopyWikilink,
        ]
        let trashIDs = [SlateCommandID.deleteEntry]
        let availableIDs = Set(projection.evaluations.map(\.id))
        let hasCatalogOpen = availableIDs.contains(
            SlateCommandID.sidebarOpen)
        let hasManagement = managementIDs.contains(
            where: availableIDs.contains)
        let hasPins = pinIDs.contains(where: availableIDs.contains)
        let hasReveal = revealIDs.contains(where: availableIDs.contains)
        let hasCopy = copyIDs.contains(where: availableIDs.contains)
        let hasTrash = trashIDs.contains(where: availableIDs.contains)

        if hasCatalogOpen {
            Menu {
                sidebarCatalogActions(
                    projection.evaluations,
                    actionIDs: [SlateCommandID.sidebarOpen])
                Divider()
                Button {
                    appState.openFile(path, target: .newTab)
                } label: {
                    SlateSymbol.newTab.label("Open in New Tab")
                }
                Button {
                    appState.openFile(
                        path, target: .newSplit(.horizontal))
                } label: {
                    SlateSymbol.splitRight.label("Open in Split")
                }
            } label: {
                SlateSymbol.open.label("Open")
            }
        }
        if hasManagement {
            sidebarCatalogActions(
                projection.evaluations,
                actionIDs: [
                    SlateCommandID.renameEntry,
                    SlateCommandID.moveTo,
                    SlateCommandID.duplicateEntry,
                ])
        }
        if hasPins {
            // FL-06 (#659): the applicable pin direction only
            // — the row-targeted reasons omit the other one.
            Divider()
            sidebarCatalogActions(
                projection.evaluations,
                actionIDs: [
                    SlateCommandID.sidebarPinNote,
                    SlateCommandID.sidebarUnpinNote,
                ])
        }
        if availableIDs.contains(SlateCommandID.sidebarAddTag)
            || availableIDs.contains(SlateCommandID.sidebarRemoveTag)
        {
            // FL5-3b (#666): the tag editors ride the same canonical
            // renderer, one shared single-file menu for tree and
            // filter rows alike.
            Divider()
            sidebarCatalogActions(
                projection.evaluations,
                actionIDs: [
                    SlateCommandID.sidebarAddTag,
                    SlateCommandID.sidebarRemoveTag,
                ])
        }
        if hasReveal || hasCopy {
            Divider()
        }
        if hasReveal {
            sidebarCatalogActions(
                projection.evaluations,
                actionIDs: [SlateCommandID.revealInFinder])
        }
        if hasCopy {
            Menu {
                sidebarCatalogActions(
                    projection.evaluations,
                    actionIDs: [
                        SlateCommandID.copyPath,
                        SlateCommandID.sidebarCopyWikilink,
                    ])
            } label: {
                SlateSymbol.copyPath.label("Copy")
            }
        }
        if hasTrash {
            Divider()
            sidebarCatalogActions(
                projection.evaluations,
                actionIDs: [SlateCommandID.deleteEntry])
        }
    }

    // MARK: - Inline rename (U2-5)

    /// The exact captured owner rendered for this row. Its UUID rides every
    /// field callback so a stale Return/Escape cannot act on a replacement.
    private func renameOwner(for node: TreeNode) -> AppState.RenamingNode? {
        guard let rename = appState.renamingNode,
            rename.path == node.path,
            rename.isDirectory == node.isDirectory
        else { return nil }
        return rename
    }

    /// Enter inline-rename mode for `node` (context-menu / rotor entry point).
    private func beginRename(_ node: TreeNode) {
        appState.requestRename(path: node.path, isDirectory: node.isDirectory)
    }

    /// The row shown in place of the label while renaming: a focused TextField
    /// (Return commits, Esc cancels) plus an inline error below it when the last
    /// commit was rejected (collision / invalid name) — the field keeps focus so
    /// the user can correct without re-invoking (spec §U2-5).
    private func renameFieldRow(
        _ node: TreeNode, rename: AppState.RenamingNode
    ) -> some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
            HStack(alignment: .firstTextBaseline, spacing: Tokens.Spacing.xs) {
                indent(for: node.depth)
                if node.isDirectory {
                    SlateSymbol.folder.decorative.foregroundStyle(Tokens.ColorRole.textSecondary)
                }
                renameEditor(rename, isDirectory: node.isDirectory)
            }
        }
    }

    /// The ONE `RenameField` construction (FilenameAdvisory contract):
    /// tree rows and FL-09 filter-result rows swap in the same editor
    /// with the same commit/cancel/error wiring.
    private func renameEditor(
        _ rename: AppState.RenamingNode, isDirectory: Bool
    ) -> some View {
        RenameField(
            initialName: rename.name,
            isDirectory: isDirectory,
            error: appState.structuralRenameError,
            onCommit: { newName in
                appState.commitPendingRename(id: rename.id, to: newName)
            },
            onCancel: {
                appState.cancelPendingRename(id: rename.id)
            })
    }

    // MARK: - Drag & drop (U2-5)

    /// Custom UTType carrying Slate's tokenized in-process drag envelope. The
    /// identifier is only a transport hint: drop dispatch also requires a
    /// one-shot registry capability bound to the current vault/session, so
    /// another process can't masquerade as an intra-tree move with forged JSON.
    static let nodeUTType = "com.slate.tree-node-path"

    /// The public file-URL flavor (#870). Carried OUT on every drag so a tree
    /// item can be dragged to Finder / another app and reopened, and ACCEPTED
    /// on drops so external files import (and vault files dragged in from
    /// Finder move). `"public.file-url"` via the UTI constant.
    static let fileURLUTType = UTType.fileURL.identifier

    enum PreferredDropProvider {
        case privatePayload(NSItemProvider)
        case fileURL(NSItemProvider)
        case none
    }

    private final class DropProviderCallbackGate: @unchecked Sendable {
        private let lock = NSLock()
        private var didClaim = false

        func claim() -> Bool {
            lock.lock()
            defer { lock.unlock() }
            guard !didClaim else { return false }
            didClaim = true
            return true
        }
    }

    static func preferredDropProvider(
        in providers: [NSItemProvider]
    ) -> PreferredDropProvider {
        if let provider = providers.first(where: {
            $0.hasItemConformingToTypeIdentifier(Self.nodeUTType)
        }) {
            return .privatePayload(provider)
        }
        if let provider = providers.first(where: {
            $0.hasItemConformingToTypeIdentifier(Self.fileURLUTType)
        }) {
            return .fileURL(provider)
        }
        return .none
    }

    /// Synchronous boundary between pure provider classification and every
    /// stateful part of accepting a drop. Keeping this seam independent of the
    /// SwiftUI instance makes the return value and provider-load ordering
    /// behavior-testable without mounting a List.
    @MainActor
    static func performAdmittedDrop(
        _ provider: PreferredDropProvider,
        appState: AppState,
        onBusy: () -> Void = {},
        perform: (PreferredDropProvider) -> Bool
    ) -> Bool {
        guard case .none = provider else {
            guard appState.admitStructuralDropRequest() else {
                onBusy()
                return false
            }
            return perform(provider)
        }
        return false
    }

    /// Admit and start exactly one supported provider load while retaining the
    /// VaultSession that owned that admission. Provider callbacks can arrive
    /// long after a vault switch; the identity guard deliberately precedes
    /// private decoding and public URL classification so bytes admitted by
    /// vault A can never act in vault B.
    @MainActor
    static func loadAdmittedDropProvider(
        _ provider: PreferredDropProvider,
        appState: AppState,
        onBusy: @escaping @MainActor () -> Void = {},
        onAdmitted: @escaping @MainActor () -> Void = {},
        onPrivate: @escaping @MainActor ([DragPayloadItem], String?) -> Void,
        onFileURL: @escaping @MainActor (URL) -> Void,
        onStaleSession: @escaping @MainActor () -> Void = {}
    ) -> Bool {
        performAdmittedDrop(
            provider,
            appState: appState,
            onBusy: onBusy
        ) { admitted in
            guard let capturedSession = appState.currentSession else { return false }
            onAdmitted()
            switch admitted {
            case let .privatePayload(itemProvider):
                let callbackGate = DropProviderCallbackGate()
                itemProvider.loadDataRepresentation(
                    forTypeIdentifier: Self.nodeUTType
                ) { data, error in
                    guard callbackGate.claim() else { return }
                    Task { @MainActor in
                        guard appState.currentSession === capturedSession else {
                            onStaleSession()
                            return
                        }
                        guard let data else { return }
                        let payload = Self.consumeRegisteredDragPayload(
                                data,
                                currentVaultURL: appState.currentVaultURL,
                                currentSession: capturedSession)
                        guard error == nil, let payload else { return }
                        onPrivate(payload.items, payload.preferredFocusPath)
                    }
                }
                return true
            case let .fileURL(itemProvider):
                itemProvider.loadDataRepresentation(
                    forTypeIdentifier: Self.fileURLUTType
                ) { data, _ in
                    Task { @MainActor in
                        guard appState.currentSession === capturedSession else {
                            onStaleSession()
                            return
                        }
                        guard let data,
                            let url = URL(dataRepresentation: data, relativeTo: nil),
                            url.isFileURL
                        else { return }
                        onFileURL(url)
                    }
                }
                return true
            case .none:
                return false
            }
        }
    }

    enum PrivateDropDisposition: Equatable {
        case move
        case reject(String)
    }

    /// Reject private drops that are already known to be impossible or a
    /// no-op. Core still owns partial-batch projection and typed skips: a batch
    /// with at least one potentially movable member is submitted unchanged.
    static func privateDropDisposition(
        _ items: [DragPayloadItem],
        into destinationFolder: String
    ) -> PrivateDropDisposition {
        guard dragPayloadItemsAreValid(items) else {
            return .reject("Nothing moved. The selected items can’t be moved to this folder.")
        }

        func isSameParent(_ item: DragPayloadItem) -> Bool {
            let parent = item.path.lastIndex(of: "/").map {
                String(item.path[..<$0])
            } ?? ""
            return parent == destinationFolder
        }

        func isSelfOrSubtree(_ item: DragPayloadItem) -> Bool {
            item.isDirectory
                && (destinationFolder == item.path
                    || destinationFolder.hasPrefix(item.path + "/"))
        }

        let rejected = items.filter { isSameParent($0) || isSelfOrSubtree($0) }
        guard rejected.count == items.count else { return .move }
        guard items.count == 1, let item = items.first else {
            return .reject("Nothing moved. The selected items can’t be moved to this folder.")
        }
        if isSelfOrSubtree(item) {
            return .reject("Nothing moved. A folder can’t be moved into itself.")
        }
        return .reject("Nothing moved. The item is already in this folder.")
    }

    /// Behavior-testable landing for a decoded private payload. A rejection is
    /// announced after decode without calling the native move funnel or
    /// changing sidebar/AppState selection. Valid and partial batches retain
    /// their original order for the core-owned planner.
    @MainActor
    @discardableResult
    static func performDecodedPrivateDrop(
        _ items: [DragPayloadItem],
        preferredFocusPath: String?,
        into destinationFolder: String,
        appState: AppState
    ) -> Bool {
        switch privateDropDisposition(items, into: destinationFolder) {
        case .move:
            return appState.moveTreeSelection(
                items.map {
                    AppState.TreeSelection(
                        path: $0.path,
                        isDirectory: $0.isDirectory)
                },
                to: destinationFolder,
                preferredFocusPath: preferredFocusPath) != nil
        case .reject(let message):
            appState.postMutationAnnouncement(message)
            return false
        }
    }

    /// Build the AppKit row descriptor only once the pointer crosses the drag
    /// threshold. A selected origin carries every visible selected URL in a
    /// pile; an unselected origin carries itself. The SwiftUI preview is
    /// rendered once for the leader while secondary items use cheap symbols.
    private func dragDescriptor(for node: TreeNode) -> FileTreeRowDragDescriptor? {
        // #851: a NEW drag beginning with leftovers from a previous session
        // (a cancel the watchdog hasn't reaped yet) settles the old one
        // first — its spring-opened folders re-collapse (no drop landed).
        if !springOpenedDirs.isEmpty || !springLoadTasks.isEmpty {
            endDragSession(dropDestination: nil)
        }
        dragSessionHasSettled = false
        let origin = SelectionRow(
            identity: .node(node.nodeID),
            path: node.path,
            isDirectory: node.isDirectory,
            isMarkdown: node.isMarkdown)
        guard var descriptor = Self.makeRowDragDescriptor(
            origin: origin,
            from: selectionModel,
            visibleRows: visibleSelectionRows,
            vaultURL: appState.currentVaultURL,
            originSession: appState.currentSession)
        else { return nil }
        let renderer = ImageRenderer(content: dragPreview(for: node))
        renderer.scale = max(1, NSScreen.main?.backingScaleFactor ?? 2)
        descriptor.leaderImage = renderer.nsImage
        return descriptor
    }

    /// A selected-origin drag carries more than the origin row, so its visual
    /// preview must disclose the actual payload count before the user drops it.
    /// The badge is visual-only because the selected rows already expose their
    /// selected state to VoiceOver.
    private func dragPreview(for node: TreeNode) -> some View {
        let origin = SelectionRow(
            identity: .node(node.nodeID),
            path: node.path,
            isDirectory: node.isDirectory,
            isMarkdown: node.isMarkdown)
        let count = Self.dragPreviewCount(
            for: origin,
            from: selectionModel,
            visibleRows: visibleSelectionRows)

        return HStack(spacing: Tokens.Spacing.xs) {
            if node.isDirectory {
                SlateSymbol.folder.decorative
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
            }
            Text(node.name)
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textPrimary)
                .lineLimit(2)
            if count > 1 {
                Text("\(count)")
                    .font(Tokens.Typography.caption.weight(.semibold))
                    .foregroundStyle(Tokens.ColorRole.onAccentFill)
                    .padding(.horizontal, Tokens.Spacing.xs)
                    .padding(.vertical, Tokens.Spacing.xxs)
                    .background(Tokens.ColorRole.accentFill, in: Capsule())
                    .accessibilityHidden(true)
            }
        }
        .padding(.horizontal, Tokens.Spacing.sm)
        .padding(.vertical, Tokens.Spacing.xs)
        .background(
            Tokens.ColorRole.surface,
            in: RoundedRectangle(cornerRadius: Tokens.Radius.control))
        .overlay {
            RoundedRectangle(cornerRadius: Tokens.Radius.control)
                .stroke(Tokens.ColorRole.separator)
                .accessibilityHidden(true)
        }
    }

    /// Pure builder for the drag payload (#870), extracted so the flavors it
    /// registers are regression-locked without a live drag: the versioned
    /// private own-process batch (ordered vault-relative path + directory kind,
    /// preferred by `handleDrop`) PLUS the origin row's public file URL so that
    /// item can cross the process boundary to Finder / other apps. The vault
    /// item already exists on disk, so a plain file-url is sufficient — no
    /// `NSFilePromiseProvider` needed (#870).
    static func makeDragProvider(nodePath: String, fileURL: URL?) -> NSItemProvider {
        makeDragProvider(
            items: [DragPayloadItem(path: nodePath, isDirectory: false)],
            originFileURL: fileURL,
            preferredFocusPath: nodePath)
    }

    static func makeDragProvider(
        items: [DragPayloadItem],
        originFileURL: URL?,
        preferredFocusPath: String? = nil,
        originVaultURL: URL? = nil,
        originSession: AnyObject? = nil
    ) -> NSItemProvider {
        let provider = NSItemProvider()
        if let payload = registerDragPayload(
            items,
            preferredFocusPath: preferredFocusPath,
            originVaultURL: originVaultURL
                ?? inferredVaultURL(
                    from: originFileURL,
                    relativePath: preferredFocusPath),
            originSession: originSession)
        {
            provider.registerDataRepresentation(
                forTypeIdentifier: Self.nodeUTType, visibility: .ownProcess
            ) { completion in
                completion(payload, nil)
                return nil
            }
        }
        if let originFileURL {
            provider.suggestedName = originFileURL.lastPathComponent
            // Register the URL object itself before the file-URL data flavor.
            // `loadObject(ofClass: URL.self)` declares `public.url` as its
            // readable type; providing that exact object representation avoids
            // an intermittent NSItemProvider -1200 conformance-bridge failure.
            provider.registerObject(originFileURL as NSURL, visibility: .all)
            // Keep the explicit `.all` `public.file-url` flavor that Finder
            // reads to copy the referenced on-disk item during drag-out.
            provider.registerDataRepresentation(
                forTypeIdentifier: Self.fileURLUTType, visibility: .all
            ) { completion in
                completion(originFileURL.dataRepresentation, nil)
                return nil
            }
        }
        return provider
    }

    static func makeDragProvider(
        origin: SelectionRow,
        from model: SelectionModel,
        visibleRows: [SelectionRow],
        vaultURL: URL?
    ) -> NSItemProvider {
        makeDragProvider(
            items: dragItems(for: origin, from: model, visibleRows: visibleRows),
            originFileURL: vaultURL?.appendingPathComponent(origin.path),
            preferredFocusPath: origin.path,
            originVaultURL: vaultURL)
    }

    /// AppKit multi-item counterpart to `makeDragProvider`. It fails closed
    /// unless every selected row can publish a concrete vault URL and the
    /// leader-only private envelope can be encoded in the same visible order.
    static func makeRowDragDescriptor(
        origin: SelectionRow,
        from model: SelectionModel,
        visibleRows: [SelectionRow],
        vaultURL: URL?,
        originSession: AnyObject? = nil
    ) -> FileTreeRowDragDescriptor? {
        guard let vaultURL else { return nil }
        let items = dragItems(for: origin, from: model, visibleRows: visibleRows)
        guard let leaderIndex = items.firstIndex(where: {
            $0.path == origin.path && $0.isDirectory == origin.isDirectory
        }),
            let payload = registerDragPayload(
                items,
                preferredFocusPath: origin.path,
                originVaultURL: vaultURL,
                originSession: originSession)
        else { return nil }
        return FileTreeRowDragDescriptor(
            fileURLs: items.map { vaultURL.appendingPathComponent($0.path) },
            directoryFlags: items.map(\.isDirectory),
            leaderIndex: leaderIndex,
            privatePayload: payload)
    }

    /// Handle a drop into `destinationFolder` ("" = root). The private flavor
    /// wins and decodes one ordered, self-describing selection before sending
    /// one intent to AppState's single/batch operation funnel. Malformed private
    /// data fails closed instead of falling through as a file import. Public
    /// file URLs preserve the existing in-vault move / external import path.
    private func handleDrop(_ providers: [NSItemProvider], into destinationFolder: String) -> Bool {
        dragSessionHasSettled = false
        let preferred = Self.preferredDropProvider(in: providers)
        guard case .privatePayload = preferred else {
            let hasPublicProvider = providers.contains {
                $0.hasItemConformingToTypeIdentifier(Self.fileURLUTType)
            }
            guard hasPublicProvider else {
                endDragSession(dropDestination: nil)
                return false
            }
            guard let owner = appState.beginImportBatch(
                providers: providers,
                destinationFolder: destinationFolder,
                selectionRevision: selectionModel.selectionRevision)
            else {
                endDragSession(dropDestination: nil)
                return false
            }
            endDragSession(dropDestination: destinationFolder)
            _ = appState.startImportBatch(owner)
            return true
        }
        return Self.loadAdmittedDropProvider(
            preferred,
            appState: appState,
            onBusy: { endDragSession(dropDestination: nil) },
            onAdmitted: {
                // #851: the drag session ends HERE with a known destination —
                // spring-opened folders that the drop did NOT land in re-collapse;
                // the destination's own chain stays open (the user is about to see
                // the moved node inside it).
                endDragSession(dropDestination: destinationFolder)
            },
            onPrivate: { items, preferredFocusPath in
                Self.performDecodedPrivateDrop(
                    items,
                    preferredFocusPath: preferredFocusPath,
                    into: destinationFolder,
                    appState: appState)
            },
            onFileURL: { url in
                // #870: a file-URL drop from Finder / another app (or a vault file
                // dragged back in from Finder). Inside the vault ⇒ move; external ⇒
                // import — resolved by `AppState.fileURLDropAction`.
                handleFileURLDrop(url, into: destinationFolder)
            })
    }

    /// Dispatch a resolved file-URL drop (#870) through AppState's shared
    /// classification plus admission-aware move/import request funnels.
    @MainActor
    private func handleFileURLDrop(_ url: URL, into destinationFolder: String) {
        appState.handleFileURLDrop(url, into: destinationFolder)
    }

    // MARK: - Drop feedback + spring-loading (#851)

    /// Hover dwell on a COLLAPSED folder before it spring-opens mid-drag.
    /// 600ms — long enough that sweeping a drag across the tree doesn't
    /// rifle folders open, short enough to feel like Finder's spring.
    static let springLoadDelay: Duration = .milliseconds(600)

    /// Quiet window after the last drop target deactivates before the drag
    /// session is considered over (left the tree / cancelled). Row-to-row
    /// moves flicker targets off for at most a frame or two (~16–33ms), so
    /// 300ms cleanly separates "between rows" from "gone".
    static let dragSessionEndGrace: Duration = .milliseconds(300)

    /// Pure (#851): of the folders a drag spring-opened, the ones to
    /// re-collapse once the session ends. A folder stays open only when the
    /// drop landed in it or somewhere beneath it (`destinationFolder` is
    /// vault-relative, "" = root); a cancelled/exited drag
    /// (`destinationFolder == nil`) re-collapses everything. Static so the
    /// set semantics are regression-locked without a live drag (the
    /// `moveOutcome` pattern).
    static func springFoldersToRecollapse(
        openedPaths: [String], destinationFolder: String?
    ) -> [String] {
        guard let destination = destinationFolder else { return openedPaths }
        return openedPaths.filter { opened in
            !(destination == opened || destination.hasPrefix(opened + "/"))
        }
    }

    /// One policy for the visual target mirrors and spring-load timers. The
    /// extracted seam keeps row and root behavior identical.
    static func dropTargetIsActive(_ targeted: Bool, busy: Bool) -> Bool {
        targeted && !busy
    }

    /// Per-row `isTargeted` binding for a folder row's drop target: mirrors
    /// into `dropTargetedNodes` (the wash), arms/cancels the spring-load
    /// timer, and feeds the session-end watchdog. Binding setters run
    /// outside the view-update transaction (#448-safe).
    private func dropTargetBinding(for node: TreeNode) -> Binding<Bool> {
        Binding(
            get: {
                Self.dropTargetIsActive(
                    dropTargetedNodes.contains(node.nodeID),
                    busy: appState.structuralMutationDisabledReason != nil)
            },
            set: { targeted in
                if Self.dropTargetIsActive(
                    targeted,
                    busy: appState.structuralMutationDisabledReason != nil)
                {
                    dragSessionHasSettled = false
                    dropTargetedNodes.insert(node.nodeID)
                    dragSessionEndTask?.cancel()
                    dragSessionEndTask = nil
                    scheduleSpringLoad(for: node)
                } else {
                    dropTargetedNodes.remove(node.nodeID)
                    springLoadTasks[node.nodeID]?.cancel()
                    springLoadTasks[node.nodeID] = nil
                    scheduleDragSessionEndCheck()
                }
            })
    }

    /// The root drop target's `isTargeted` binding — same bookkeeping as the
    /// rows, driving the root ring instead of a row wash. The root never
    /// spring-loads (there is nothing to open).
    private var rootDropBinding: Binding<Bool> {
        Binding(
            get: {
                Self.dropTargetIsActive(
                    rootDropTargeted,
                    busy: appState.structuralMutationDisabledReason != nil)
            },
            set: { targeted in
                let active = Self.dropTargetIsActive(
                    targeted,
                    busy: appState.structuralMutationDisabledReason != nil)
                rootDropTargeted = active
                if active {
                    dragSessionHasSettled = false
                    dragSessionEndTask?.cancel()
                    dragSessionEndTask = nil
                } else {
                    scheduleDragSessionEndCheck()
                }
            })
    }

    /// Arm the spring-load timer for a hovered COLLAPSED folder: after
    /// `springLoadDelay` of continuous targeting it expands and is recorded
    /// in `springOpenedDirs` so the session end can restore it. Folders the
    /// user already expanded are never recorded — they were open before the
    /// drag and must stay open after it.
    private func scheduleSpringLoad(for node: TreeNode) {
        guard node.isDirectory, !tree.expanded.contains(node.nodeID) else { return }
        let id = node.nodeID
        springLoadTasks[id]?.cancel()
        springLoadTasks[id] = Task { @MainActor in
            try? await Task.sleep(for: Self.springLoadDelay)
            guard !Task.isCancelled,
                Self.dropTargetIsActive(
                    dropTargetedNodes.contains(id),
                    busy: appState.structuralMutationDisabledReason != nil),
                !tree.expanded.contains(id)
            else { return }
            tree.expand(node)
            springOpenedDirs.insert(id)
        }
    }

    /// Start (restarting) the session-end watchdog: if no target goes live
    /// again within `dragSessionEndGrace`, the drag left the tree or was
    /// cancelled — settle the session with no destination.
    private func scheduleDragSessionEndCheck() {
        dragSessionEndTask?.cancel()
        dragSessionEndTask = Task { @MainActor in
            try? await Task.sleep(for: Self.dragSessionEndGrace)
            guard !Task.isCancelled else { return }
            if dropTargetedNodes.isEmpty && !rootDropTargeted {
                endDragSession(dropDestination: nil)
            }
        }
    }

    /// Settle the drag session: cancel timers, clear the target mirrors, and
    /// re-collapse the spring-opened folders the drop did NOT land in
    /// (`springFoldersToRecollapse`; nil destination ⇒ all of them).
    private func endDragSession(dropDestination: String?) {
        guard !dragSessionHasSettled else { return }
        dragSessionHasSettled = true
        dragSessionEndTask?.cancel()
        dragSessionEndTask = nil
        for task in springLoadTasks.values { task.cancel() }
        springLoadTasks = [:]
        dropTargetedNodes = []
        rootDropTargeted = false
        guard !springOpenedDirs.isEmpty else { return }
        let opened: [(id: NodeID, path: String)] = springOpenedDirs.compactMap { id in
            tree.node(for: id).map { (id, $0.path) }
        }
        let recollapse = Set(
            Self.springFoldersToRecollapse(
                openedPaths: opened.map(\.path),
                destinationFolder: dropDestination))
        for entry in opened where recollapse.contains(entry.path) {
            if let node = tree.node(for: entry.id) {
                tree.collapse(node)
            }
        }
        springOpenedDirs = []
    }

    /// Inline "Loading…" row shown under a folder whose children are being
    /// fetched. Labeled so VoiceOver announces the wait.
    private func loadingRow(depth: Int) -> some View {
        HStack(spacing: Tokens.Spacing.sm) {
            indent(for: depth)
            ProgressView()
                .controlSize(.small)
            // Named object, not bare "Loading…" (progress-indicators.md:
            // "avoid vague terms like 'Loading'").
            Text("Loading folder…")
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
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
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textPrimary)
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

    // MARK: - Progress strips

    /// A compact, sidebar-local indicator for ordinary structural work. Large
    /// selection validation has an always-mounted window-level counterpart, so
    /// suppress that one state here to avoid duplicate progress presentations.
    @ViewBuilder private var structuralMutationProgress: some View {
        if !appState.isValidatingSidebarAction,
           let reason = appState.structuralMutationDisabledReason
        {
            HStack(spacing: Tokens.Spacing.sm) {
                ProgressView()
                    .controlSize(.small)
                    .accessibilityHidden(true)
                Text(reason)
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .fixedSize(horizontal: false, vertical: true)
                Spacer(minLength: 0)
            }
            .padding(.horizontal, Tokens.Spacing.md)
            .padding(.vertical, Tokens.Spacing.xs)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(Tokens.ColorRole.surfaceSecondary)
            .overlay(alignment: .bottom) {
                Rectangle()
                    .fill(Tokens.ColorRole.separator)
                    .frame(height: 1)
                    .accessibilityHidden(true)
            }
            .accessibilityElement(children: .combine)
            .accessibilityLabel(reason)
            .help(reason)
        }
    }

    /// Persistent recovery for a physical Trash outcome the one-shot result
    /// alert could not settle. Dismissing that alert never strands a document
    /// in an undiscoverable read-only state.
    @ViewBuilder private var batchTrashQuarantineRecovery: some View {
        if let notice = appState.batchTrashQuarantineNotice {
            HStack(alignment: .firstTextBaseline, spacing: Tokens.Spacing.sm) {
                SlateSymbol.warning.decorative
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                Text(notice)
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .fixedSize(horizontal: false, vertical: true)
                Spacer(minLength: 0)
                Button(AppState.BatchTrashCopy.checkAgainLabel) {
                    _ = appState.retryBatchTrashUnknownReconciliation()
                }
                .disabled(appState.isMutatingStructure)
                .accessibilityHint(AppState.BatchTrashCopy.checkAgainHint)
                .help(AppState.BatchTrashCopy.checkAgainHint)
            }
            .padding(.horizontal, Tokens.Spacing.md)
            .padding(.vertical, Tokens.Spacing.xs)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(Tokens.ColorRole.surfaceSecondary)
            .overlay(alignment: .bottom) {
                Rectangle()
                    .fill(Tokens.ColorRole.separator)
                    .frame(height: 1)
                    .accessibilityHidden(true)
            }
            .accessibilityElement(children: .contain)
        }
    }

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
        HStack(spacing: Tokens.Spacing.sm) {
            if let progress {
                ProgressView(value: progress)
                    .progressViewStyle(.linear)
            } else {
                ProgressView()
                    .progressViewStyle(.linear)
            }
            Text(label)
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                // WCAG 1.4.4: no lineLimit(1) — let Dynamic Type wrap.
                .lineLimit(2)
        }
        .padding(.horizontal, Tokens.Spacing.md)
        // 6pt: a deliberate half-step between xs(4) and sm(8) — a thin progress
        // strip wants a tighter vertical rhythm than a full sm row. No 6pt token
        // (the scale is 2/4/8/12…); kept literal per the u5_spec escape hatch.
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

    /// Whether a delete-command delivery may mutate the vault. The spec'd
    /// chord is ⌘⌫ (Finder's Move-to-Trash); a bare ⌫ must not delete —
    /// `.onDeleteCommand` can't see the key, so the gate inspects the live
    /// event. A keyDown without ⌘ is the bare slip-key: reject. Anything
    /// that isn't a keyDown (AX delete action, menu `delete:`, or no
    /// current event at all) passes — those deliveries are deliberate.
    ///
    /// Static + pure so the modifier semantics are regression-locked by a
    /// unit test without a running List (the `moveOutcome` pattern).
    static func deleteCommandAllowed(event: NSEvent?) -> Bool {
        guard let event, event.type == .keyDown else { return true }
        let modifiers = event.modifierFlags
            .intersection(.deviceIndependentFlagsMask)
            .subtracting(.capsLock)
        return modifiers == .command
    }

    /// SwiftUI companion to `deleteCommandAllowed(event:)`: only the Finder
    /// Move-to-Trash chord is consumed. Caps Lock is typing state and ignored;
    /// every other added modifier changes the command and falls through.
    static func deleteKeyModifiersAllowed(_ modifiers: EventModifiers) -> Bool {
        modifiers.subtracting(.capsLock) == .command
    }

    /// Whether the List-level `.onKeyPress` interceptors (keyboard selection,
    /// explicit open, ⌘⌫ delete, and folder disclosure) may fire. TWO
    /// conditions, and the second is load-bearing:
    /// `.focused($fileTreeFocused)` on the List
    /// has focus-WITHIN semantics — it stays TRUE while the inline
    /// RenameField (a descendant) is first responder, and a List-level
    /// `.onKeyPress` sees keys BEFORE the field editor does. Without the
    /// rename gate, typing a space into "Project Notes" toggles the
    /// folder under the rename row instead of inserting the space,
    /// Return can never commit the rename, and ⌘⌫ (delete-to-line-start
    /// muscle memory in any macOS text field) trashes the node being
    /// renamed (red-team probe on the HIG-audit pass, empirically
    /// reproduced in-harness).
    ///
    /// Static + pure so the two-flag semantics are regression-locked by
    /// a unit test (the `deleteCommandAllowed` pattern).
    /// Typing-state modifiers (Shift, Caps Lock) pass; chord modifiers
    /// (⌘⌥⌃) reject — shifted characters must type-select and caps lock
    /// must not kill F2/type-select (red-team probe), while real chords
    /// fall through to their owners. Static + pure (the
    /// `treeKeyInterceptionActive` pattern).
    static func typeSelectModifiersAllowed(_ modifiers: EventModifiers) -> Bool {
        modifiers.subtracting([.shift, .capsLock]).isEmpty
    }

    static func treeKeyInterceptionActive(
        fileTreeFocused: Bool, isRenaming: Bool
    ) -> Bool {
        fileTreeFocused && !isRenaming
    }

    // MARK: - Type-select + F2 (#850)

    /// F2 as a SwiftUI `KeyEquivalent`: AppKit delivers function keys as
    /// Unicode scalars in the F700 block (`NSF2FunctionKey` = U+F705), and
    /// `onKeyPress(keys:)` matches on exactly that character.
    static let f2Key = KeyEquivalent(Character(UnicodeScalar(UInt16(NSF2FunctionKey))!))

    /// Quiet time before the type-select prefix buffer resets (~the
    /// NSTableView/Finder cadence).
    static let typeSelectResetDelay: Duration = .seconds(1)

    /// The characters that feed type-select: letters, digits, punctuation,
    /// symbols. Deliberately NOT whitespace — Space belongs to folder
    /// disclosure (shipped semantics, unchanged) and control keys belong to
    /// navigation.
    static let typeSelectCharacters: CharacterSet = {
        var set = CharacterSet.alphanumerics
        set.formUnion(.punctuationCharacters)
        set.formUnion(.symbols)
        return set
    }()

    /// Case/diacritic fold for type-select matching ("é" matches "e",
    /// "READ" matches "rea"). Locale-independent so tests are deterministic
    /// across machines.
    static func typeSelectFold(_ s: String) -> String {
        s.folding(options: [.caseInsensitive, .diacriticInsensitive], locale: nil)
    }

    /// Pure (#850): the visible-row index a type-select buffer lands on, or
    /// nil when nothing matches. Semantics (Finder/NSOutlineView):
    ///   - a single-character buffer advances — the scan starts AFTER the
    ///     current selection, so pressing "r" repeatedly cycles through the
    ///     r-names;
    ///   - a multi-character (refining) buffer stays on the current row
    ///     while it still matches ("r" landed on readme.md; "re" must not
    ///     jump away), else scans onward;
    ///   - the scan wraps around the full visible list (folder AND file
    ///     names — `names` is the flattened visible rows in order).
    /// Static so the matcher is regression-locked without a running List
    /// (the `moveOutcome` pattern).
    static func typeSelectIndex(
        names: [String], prefix: String, selectedIndex: Int?
    ) -> Int? {
        guard !prefix.isEmpty, !names.isEmpty else { return nil }
        let foldedPrefix = typeSelectFold(prefix)
        func matches(_ index: Int) -> Bool {
            typeSelectFold(names[index]).hasPrefix(foldedPrefix)
        }
        if prefix.count > 1, let sel = selectedIndex, names.indices.contains(sel),
            matches(sel)
        {
            return sel
        }
        let anchor: Int
        if let sel = selectedIndex, names.indices.contains(sel) {
            anchor = (sel + 1) % names.count
        } else {
            anchor = 0
        }
        for offset in 0..<names.count {
            let index = (anchor + offset) % names.count
            if matches(index) { return index }
        }
        return nil
    }

    /// Apply one type-select keystroke: grow the buffer, re-arm the reset
    /// timer, and move the selection to the match among the VISIBLE rows.
    /// Landing on a file flows through the normal selection funnel
    /// (`.onChange(of: listSelection)` opens it, and the existing
    /// tree-focused announcement path speaks "Selected: <name>" exactly
    /// once — no separate announcement here, so no double-speak); landing
    /// on a folder selects it silently, exactly like arrowing onto it.
    /// Next buffer state for an incoming type-select character: repeating
    /// the SAME character cycles (buffer stays that single character, and
    /// the matcher's advance-from-selection walk lands the next match —
    /// the Finder/NSTableView idiom); any other character refines the
    /// prefix. Static + pure (Codex round 2: "bb" used to dead-end).
    static func nextTypeSelectBuffer(current: String, incoming: String) -> String {
        if !current.isEmpty,
            current.allSatisfy({ String($0).caseInsensitiveCompare(incoming) == .orderedSame }) {
            return incoming
        }
        return current + incoming
    }

    /// VoiceOver's keyboard-selection live region speaks the same semantic
    /// label and value as the row itself, including compact rows whose details
    /// are intentionally visual-only hidden.
    static func selectionAnnouncement(for model: SidebarRowModel) -> String {
        "Selected: \(model.accessibilityLabel). \(model.accessibilityValue)."
    }

    /// #418: native macOS List keyboard movement is silent in this custom
    /// tree. Speak the selected row's shared semantic model on the actual
    /// selection edge. Dirty-gate navigation and programmatic mirrors suppress
    /// this path because their dialog/originating command already speaks.
    private func announceFocusedFileSelection(
        _ selection: RowID?,
        suppressed: Bool
    ) {
        guard !suppressed, fileTreeFocused, appState.pendingNavigation == nil else {
            return
        }
        guard case let .node(id) = selection,
            case .file = id,
            let node = tree.node(for: id),
            case let .file(fileState) = node.kind
        else { return }
        let model = SidebarRowModel(
            summary: fileState.summary,
            preferences: rowPreferences,
            isPinned: tree.isPinnedRow(id),
            now: sidebarNow)
        postAccessibilityAnnouncement(
            Self.selectionAnnouncement(for: model),
            priority: .medium)
    }

    static func selectionIsActive(
        treeFocused: Bool,
        controlActiveState: ControlActiveState
    ) -> Bool {
        treeFocused && controlActiveState == .key
    }

    /// Finder-style type-select follows the visible primary label. Folder
    /// names are already their visible labels; file rows may use frontmatter
    /// title or the filename stem.
    static func typeSelectName(for row: TreeNode) -> String {
        if case let .file(fileState) = row.kind {
            return SidebarRowModel.displayName(for: fileState.summary)
        }
        return row.name
    }

    private func handleTypeSelect(_ characters: String, proxy: ScrollViewProxy) {
        typeSelectResetTask?.cancel()
        typeSelectBuffer = Self.nextTypeSelectBuffer(
            current: typeSelectBuffer, incoming: characters)
        typeSelectResetTask = Task { @MainActor in
            try? await Task.sleep(for: Self.typeSelectResetDelay)
            guard !Task.isCancelled else { return }
            typeSelectBuffer = ""
        }
        let rows = tree.visibleRows
        let selectedIndex: Int? = {
            guard case let .node(id) = listSelection else { return nil }
            return rows.firstIndex(where: { $0.nodeID == id })
        }()
        guard
            let index = Self.typeSelectIndex(
                names: rows.map(Self.typeSelectName(for:)), prefix: typeSelectBuffer,
                selectedIndex: selectedIndex)
        else { return }
        let target = rows[index].nodeID
        let moved = listSelection != .node(target)
        if moved {
            if let row = selectionRow(for: .node(target)) {
                mutateSelectionAndPublish { $0.revealFromUserIntent(row) }
            }
            selectionRevisionGate.arm(for: .node(target))
            listSelection = .node(target)
        }
        // Reveal + MATERIALIZE the landing (red-team probe: without the
        // scroll, a long jump leaves the viewport unmoved, AppKit never
        // applies the row selection, and the next native arrow snaps the
        // binding to row 0 — the applyPostMutationFocus lesson).
        proxy.scrollTo(RowID.node(target), anchor: .center)
        // Folder landings produce no selectedFilePath change, so the
        // tree-focused selection announcement never fires — speak them
        // here or type-select is indistinguishable from a dead buffer
        // for VoiceOver users (file landings speak via the existing
        // path; this branch must not double-speak them).
        // Only on a CHANGED landing — prefix refinement that stays on the
        // same row must not re-announce (Codex round 2: chatter).
        if moved, case .dir = target,
            let row = rows.first(where: { $0.nodeID == target }) {
            postAccessibilityAnnouncement("Selected: \(row.name), folder", priority: .medium)
        }
    }

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
            if let row = selectionRow(for: .node(id)) {
                mutateSelectionAndPublish { $0.revealFromUserIntent(row) }
            }
            selectionRevisionGate.arm(for: .node(id))
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

    /// FL-07: resolve a reveal target that may be a folder (shortcut
    /// containers, history entries). Searches materialized levels for the
    /// matching node of the requested kind.
    /// FL3-4.2: user-selection edge → history ring. Hoisted out of the
    /// List's modifier chain for the type-checker's budget.
    private func recordHistoryForUserSelection(_ newSelection: RowID?) {
        guard let newSelection,
            let row = selectionRow(for: newSelection)
        else { return }
        appState.recordSidebarSelectionForHistory(
            path: row.path, isDirectory: row.isDirectory)
    }

    private func rowID(forRevealPath path: String, isDirectory: Bool) -> RowID? {
        guard isDirectory else { return rowID(forPath: path) }
        if let dir = tree.rootLevel.first(where: {
            $0.path == path && $0.isDirectory
        }) {
            return .node(dir.nodeID)
        }
        for level in tree.children.values {
            if let dir = level.first(where: { $0.path == path && $0.isDirectory }) {
                return .node(dir.nodeID)
            }
        }
        return nil
    }

    /// Indent width for a row at `depth`: `Tokens.Spacing.md` per level.
    /// Static + pure so tests can assert the exact geometry.
    static func indentWidth(for depth: Int) -> CGFloat {
        CGFloat(depth) * Tokens.Spacing.md
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

    @State private var text: String
    @FocusState private var focused: Bool

    init(
        initialName: String,
        isDirectory: Bool,
        error: String?,
        onCommit: @escaping (String) -> Void,
        onCancel: @escaping () -> Void
    ) {
        self.initialName = initialName
        self.isDirectory = isDirectory
        self.error = error
        self.onCommit = onCommit
        self.onCancel = onCancel
        _text = State(initialValue: initialName)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
            TextField("Name", text: $text)
                .textFieldStyle(.roundedBorder)
                .font(Tokens.Typography.body)
                .focused($focused)
                .accessibilityLabel(isDirectory ? "Folder name" : "File name")
                .accessibilityHint("Type a new name. Return renames; Escape cancels.")
                .onSubmit { onCommit(text) }
                .onExitCommand { onCancel() }
                .onChange(of: focused) { _, isFocused in
                    // Commit on focus loss (click-away), unless a validation
                    // error deliberately keeps the edit active for correction.
                    if !isFocused && error == nil {
                        onCommit(text)
                    }
                }
                .onAppear {
                    focused = true
                    selectBaseName()
                }
            if let error = error {
                Text(error)
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.destructiveText)
                    // WCAG 1.4.4: wrap, don't clip.
                    .lineLimit(3)
                    .accessibilityLabel("Rename error. \(error)")
            }
            FilenameAdvisoryView(name: text)
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
