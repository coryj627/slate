// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Unchecked only at the closure boundary: production captures one bound
/// VaultSession and tests capture immutable/thread-safe fixtures. The serial
/// worker is the sole caller while the owning bind generation is live.
struct FileTreeFetchOperation: @unchecked Sendable {
    let call: (String, String?, CancelToken) throws -> DirListing
}

/// A fully projected, organized level. File row observables are constructed
/// before this value crosses back to MainActor and are not mutated until the
/// owning view model publishes them.
struct FileTreePreparedLevel: @unchecked Sendable {
    let nodes: [TreeNode]
    let presentation: SidebarLevelPresentation
    let fileStatesByPath: [String: FileTreeFileState]
    let directoriesByPath: [String: TreeNode]
    let directoriesByID: [NodeID: TreeNode]
    /// Directory ids in provider order. Dictionary indexes remain O(1), while
    /// bulk commands never inherit hash-table iteration nondeterminism.
    let directoryOrder: [NodeID]
    let directoryOrdinalByID: [NodeID: Int]
    let isComplete: Bool
    let incompleteMessage: String?
}

struct FileTreeSummaryOverlay: @unchecked Sendable {
    let summariesByPath: [String: FileSummary]
}

/// Frozen worker input. The only unchecked member is the immutable civil-date
/// resolver existential; production's resolver is stateless, and test
/// resolvers supplied to async paths must be thread-safe.
struct FileTreeOrganizationInput: @unchecked Sendable {
    let prefs: SidebarOrganizationPrefs
    let pins: SidebarPins
    let now: Date
    let calendar: Calendar
    let locale: Locale
    let civilDateResolver: any SidebarCivilDateResolving

    @MainActor
    init(_ context: FileTreeViewModel.OrganizationContext) {
        prefs = context.prefs
        pins = context.pins
        now = context.now
        calendar = context.calendar
        locale = context.locale
        civilDateResolver = context.civilDateResolver
    }
}

struct FileTreeOrganizedFiles: @unchecked Sendable {
    let orderedPaths: [String]
    let presentation: SidebarLevelPresentation
}

/// One pure organization implementation shared by the production worker and
/// the deterministic legacy test seam.
enum FileTreeLevelOrganization {
    static func organize(
        files: [FileSummary],
        parentPath: String,
        depth: Int,
        input: FileTreeOrganizationInput,
        isComplete: Bool
    ) -> FileTreeOrganizedFiles {
        organizeCancellable(
            files: files,
            parentPath: parentPath,
            depth: depth,
            input: input,
            isComplete: isComplete,
            isCancelled: { false })!
    }

    static func organizeCancellable(
        files: [FileSummary],
        parentPath: String,
        depth: Int,
        input: FileTreeOrganizationInput,
        isComplete: Bool,
        isCancelled: () -> Bool
    ) -> FileTreeOrganizedFiles? {
        let pinnedPaths = input.pins.paths(forFolder: parentPath)
        guard !files.isEmpty else {
            return isCancelled() ? nil : FileTreeOrganizedFiles(
                orderedPaths: [],
                presentation: SidebarLevelPresentation(
                    stalePinnedPaths: isComplete ? pinnedPaths : []))
        }
        var organizerFiles: [SidebarOrganizerFile] = []
        organizerFiles.reserveCapacity(files.count)
        for (offset, file) in files.enumerated() {
            if offset.isMultiple(of: 256), isCancelled() { return nil }
            organizerFiles.append(
                SidebarOrganizerFile(
                    path: file.path,
                    name: file.name,
                    displayName: file.displayName,
                    createdDate: file.createdDate,
                    createdMs: file.createdMs,
                    mtimeMs: file.mtimeMs))
        }
        guard let organized = SidebarLevelOrganizer.organizeCancellable(
            files: organizerFiles,
            choice: input.prefs.effectiveChoice(forFolder: parentPath),
            pinnedPaths: pinnedPaths,
            now: input.now,
            calendar: input.calendar,
            locale: input.locale,
            civilDateResolver: input.civilDateResolver,
            isCancelled: isCancelled)
        else { return nil }

        var headers: [NodeID: SidebarTreeHeaderRow] = [:]
        if organized.pinnedCount > 0,
            let firstPinnedPath = organized.orderedPaths.first
        {
            headers[.file(path: firstPinnedPath)] = SidebarTreeHeaderRow(
                kind: .pinned,
                key: "pinned",
                label: "Pinned",
                fileCount: organized.pinnedCount,
                depth: depth)
        }
        for (offset, group) in organized.groups.enumerated() {
            if offset.isMultiple(of: 256), isCancelled() { return nil }
            headers[.file(path: group.firstPath)] = SidebarTreeHeaderRow(
                kind: .group,
                key: group.key,
                label: group.label,
                fileCount: group.fileCount,
                depth: depth)
        }
        guard !isCancelled() else { return nil }
        return FileTreeOrganizedFiles(
            orderedPaths: organized.orderedPaths,
            presentation: SidebarLevelPresentation(
                headersBefore: headers,
                pinnedIDs: Set(
                    organized.orderedPaths.prefix(organized.pinnedCount).map {
                        .file(path: $0)
                    }),
                stalePinnedPaths: isComplete ? organized.stalePinnedPaths : []))
    }
}

enum FileTreeLevelOutcome: @unchecked Sendable {
    case success(FileTreePreparedLevel)
    case failure(String)
    case cancelled
}

struct FileTreeReorganizedLevel: @unchecked Sendable {
    /// Present only when publication needs a new order. An unchanged
    /// worker-built array is destroyed on the worker queue, never MainActor.
    let nodes: [TreeNode]?
    let presentation: SidebarLevelPresentation
    let orderChanged: Bool
    let presentationChanged: Bool
    let rowVisits: Int
}

enum FileTreeReorganizationOutcome: @unchecked Sendable {
    case success(FileTreeReorganizedLevel)
    case cancelled
}

/// Obsolete observable rows and indexes stay strongly owned until this value
/// reaches the cleanup queue. Replacing a 50k level therefore does not make
/// MainActor run the final release chain for every row and hash bucket.
struct FileTreeRetiredStorage: @unchecked Sendable {
    let levels: [[TreeNode]]
    let fileIndexes: [[String: FileTreeFileState]]
    let directoryPathIndexes: [[String: TreeNode]]
    let directoryIDIndexes: [[NodeID: TreeNode]]
    let directoryOrders: [[NodeID]]
    let directoryOrdinalIndexes: [[NodeID: Int]]
    let presentations: [SidebarLevelPresentation]
    fileprivate var probes: [FileTreeRetirementSentinel] = []

    init(
        levels: [[TreeNode]],
        fileIndexes: [[String: FileTreeFileState]],
        directoryPathIndexes: [[String: TreeNode]],
        directoryIDIndexes: [[NodeID: TreeNode]],
        directoryOrders: [[NodeID]] = [],
        directoryOrdinalIndexes: [[NodeID: Int]] = [],
        presentations: [SidebarLevelPresentation] = []
    ) {
        self.levels = levels
        self.fileIndexes = fileIndexes
        self.directoryPathIndexes = directoryPathIndexes
        self.directoryIDIndexes = directoryIDIndexes
        self.directoryOrders = directoryOrders
        self.directoryOrdinalIndexes = directoryOrdinalIndexes
        self.presentations = presentations
    }

    var isEmpty: Bool {
        levels.allSatisfy(\.isEmpty)
            && fileIndexes.allSatisfy(\.isEmpty)
            && directoryPathIndexes.allSatisfy(\.isEmpty)
            && directoryIDIndexes.allSatisfy(\.isEmpty)
            && directoryOrders.allSatisfy(\.isEmpty)
            && directoryOrdinalIndexes.allSatisfy(\.isEmpty)
            && presentations.allSatisfy {
                $0.headersBefore.isEmpty
                    && $0.pinnedIDs.isEmpty
                    && $0.stalePinnedPaths.isEmpty
            }
    }
}

fileprivate final class FileTreeRetirementSentinel: @unchecked Sendable {
    private let hook: FileTreeRetirementHook

    init(hook: FileTreeRetirementHook) { self.hook = hook }

    deinit { hook.call(Thread.isMainThread) }
}

/// Owns the sole retirement copy until MainActor has completed the turn that
/// handed it off. Only then may the utility queue release the storage graph.
private final class FileTreeRetirementHandoff: @unchecked Sendable {
    private let barrier = DispatchSemaphore(value: 0)
    private var storage: FileTreeRetiredStorage?

    init(storage: FileTreeRetiredStorage) { self.storage = storage }

    func permitRelease() { barrier.signal() }

    func releaseOnRetirementQueue() {
        barrier.wait()
        storage = nil
    }
}

/// Deterministic preparation checkpoint used by cancellation regressions. The
/// unchecked boundary is limited to this test seam; production never supplies
/// a hook.
enum FileTreePreparationEvent: Equatable, Sendable {
    case initial(parentPath: String)
    case overlay(parentPath: String)
    case completed(parentPath: String)
}

struct FileTreePreparationHook: @unchecked Sendable {
    let call: (FileTreePreparationEvent, CancelToken) -> Void
}

struct FileTreeRetirementHook: @unchecked Sendable {
    let call: (Bool) -> Void
}

/// Serial non-main owner for page-one fetch, continuation draining, projection,
/// and organization. Native calls can block, so a dedicated serial queue owns
/// them instead of monopolizing Swift's cooperative executor. A queued request
/// whose owner was revoked is rejected before its first FFI call.
final class FileTreeLevelWorker: @unchecked Sendable {
    private let queue = DispatchQueue(
        label: "com.coryjoseph.slate.file-tree-level-worker",
        qos: .userInitiated)
    private let retirementQueue = DispatchQueue(
        label: "com.coryjoseph.slate.file-tree-retirement",
        qos: .utility)
    private let preparationHook: FileTreePreparationHook?
    private let retirementHook: FileTreeRetirementHook?

    init(
        preparationHook: FileTreePreparationHook? = nil,
        retirementHook: FileTreeRetirementHook? = nil
    ) {
        self.preparationHook = preparationHook
        self.retirementHook = retirementHook
    }

    @MainActor
    func retire(_ storage: FileTreeRetiredStorage) {
        guard !storage.isEmpty else { return }
        var owned = storage
        if let retirementHook {
            owned.probes.append(FileTreeRetirementSentinel(hook: retirementHook))
        }
        let handoff = FileTreeRetirementHandoff(storage: owned)
        retirementQueue.async { handoff.releaseOnRetirementQueue() }
        // This block cannot run until the current MainActor stack (including
        // every caller-local copy) has unwound.
        DispatchQueue.main.async { handoff.permitRelease() }
    }

    @MainActor
    func retire(_ reorganization: FileTreeReorganizedLevel) {
        retire(
            FileTreeRetiredStorage(
                levels: reorganization.nodes.map { [$0] } ?? [],
                fileIndexes: [],
                directoryPathIndexes: [],
                directoryIDIndexes: [],
                presentations: [reorganization.presentation]))
    }

    @MainActor
    func retire(_ prepared: FileTreePreparedLevel) {
        retire(
            FileTreeRetiredStorage(
                levels: [prepared.nodes],
                fileIndexes: [prepared.fileStatesByPath],
                directoryPathIndexes: [prepared.directoriesByPath],
                directoryIDIndexes: [prepared.directoriesByID],
                directoryOrders: [prepared.directoryOrder],
                directoryOrdinalIndexes: [prepared.directoryOrdinalByID],
                presentations: [prepared.presentation]))
    }

    @MainActor
    func retire(_ outcome: FileTreeLevelOutcome) {
        guard case .success(let prepared) = outcome else { return }
        retire(prepared)
    }

    /// Reorder an already-published level after a live save changes its active
    /// organization key. Immutable TreeNode storage crosses the boundary, and
    /// file summaries are read through FileTreeFileState's locked mirror.
    func reorganize(
        nodes: [TreeNode],
        oldPresentation: SidebarLevelPresentation,
        parentPath: String,
        depth: Int,
        organization: FileTreeOrganizationInput,
        isComplete: Bool,
        nativeCancel: CancelToken
    ) async -> FileTreeReorganizationOutcome {
        await withTaskCancellationHandler {
            await withCheckedContinuation { continuation in
                queue.async {
                    continuation.resume(
                        returning: Self.performReorganization(
                            nodes: nodes,
                            oldPresentation: oldPresentation,
                            parentPath: parentPath,
                            depth: depth,
                            organization: organization,
                            isComplete: isComplete,
                            nativeCancel: nativeCancel))
                }
            }
        } onCancel: {
            nativeCancel.cancel()
        }
    }

    private static func performReorganization(
        nodes: [TreeNode],
        oldPresentation: SidebarLevelPresentation,
        parentPath: String,
        depth: Int,
        organization: FileTreeOrganizationInput,
        isComplete: Bool,
        nativeCancel: CancelToken
    ) -> FileTreeReorganizationOutcome {
        guard !nativeCancel.isCancelled() else { return .cancelled }
        var directories: [TreeNode] = []
        var files: [FileSummary] = []
        var fileNodesByPath: [String: TreeNode] = [:]
        directories.reserveCapacity(nodes.count)
        files.reserveCapacity(nodes.count)
        fileNodesByPath.reserveCapacity(nodes.count)
        var rowVisits = 0
        for (offset, node) in nodes.enumerated() {
            if offset.isMultiple(of: 256), nativeCancel.isCancelled() {
                return .cancelled
            }
            rowVisits += 1
            switch node.kind {
            case .directory:
                directories.append(node)
            case let .file(state):
                files.append(state.snapshotForWorker())
                fileNodesByPath[node.path] = node
            }
        }
        guard let organized = FileTreeLevelOrganization.organizeCancellable(
            files: files,
            parentPath: parentPath,
            depth: depth,
            input: organization,
            isComplete: isComplete,
            isCancelled: nativeCancel.isCancelled)
        else { return .cancelled }
        var nextNodes = directories
        nextNodes.reserveCapacity(nodes.count)
        for (offset, path) in organized.orderedPaths.enumerated() {
            if offset.isMultiple(of: 256), nativeCancel.isCancelled() {
                return .cancelled
            }
            rowVisits += 1
            if let node = fileNodesByPath[path] { nextNodes.append(node) }
        }
        guard !nativeCancel.isCancelled() else { return .cancelled }
        var orderChanged = nextNodes.count != nodes.count
        if !orderChanged {
            for index in nextNodes.indices {
                if index.isMultiple(of: 256), nativeCancel.isCancelled() {
                    return .cancelled
                }
                if nextNodes[index].nodeID != nodes[index].nodeID {
                    orderChanged = true
                    break
                }
            }
        }
        return .success(
            FileTreeReorganizedLevel(
                nodes: orderChanged ? nextNodes : nil,
                presentation: organized.presentation,
                orderChanged: orderChanged,
                presentationChanged: oldPresentation != organized.presentation,
                rowVisits: rowVisits))
    }

    func load(
        fetch: FileTreeFetchOperation,
        parentPath: String,
        depth: Int,
        organization: FileTreeOrganizationInput,
        nativeCancel: CancelToken
    ) async -> FileTreeLevelOutcome {
        await withTaskCancellationHandler {
            await withCheckedContinuation { continuation in
                queue.async { [preparationHook] in
                    continuation.resume(
                        returning: Self.performLoad(
                            fetch: fetch,
                            parentPath: parentPath,
                            depth: depth,
                            organization: organization,
                            nativeCancel: nativeCancel,
                            preparationHook: preparationHook))
                }
            }
        } onCancel: {
            nativeCancel.cancel()
        }
    }

    private static func performLoad(
        fetch: FileTreeFetchOperation,
        parentPath: String,
        depth: Int,
        organization: FileTreeOrganizationInput,
        nativeCancel: CancelToken,
        preparationHook: FileTreePreparationHook?
    ) -> FileTreeLevelOutcome {
        guard !nativeCancel.isCancelled() else {
            return .cancelled
        }

        var dirs: [DirNodeSummary] = []
        var files: [FileSummary] = []
        var cursor: String?
        var fetchedPage = false
        var incompleteMessage: String?

        do {
            repeat {
                guard !nativeCancel.isCancelled() else {
                    return .cancelled
                }
                let page = try fetch.call(parentPath, cursor, nativeCancel)
                fetchedPage = true
                let remaining =
                    FileTreeViewModel.levelTotalSafetyCap - dirs.count - files.count
                guard remaining > 0 else {
                    incompleteMessage = FileTreeViewModel.levelSafetyCapMessage
                    break
                }

                let takenDirs = Array(page.dirs.prefix(remaining))
                dirs.append(contentsOf: takenDirs)
                let fileRoom = remaining - takenDirs.count
                let takenFiles = Array(page.files.items.prefix(fileRoom))
                files.append(contentsOf: takenFiles)
                let consumedWholePage =
                    takenDirs.count == page.dirs.count
                    && takenFiles.count == page.files.items.count
                cursor = page.files.nextCursor
                if !consumedWholePage
                    || (cursor != nil
                        && dirs.count + files.count
                            >= FileTreeViewModel.levelTotalSafetyCap)
                {
                    incompleteMessage = FileTreeViewModel.levelSafetyCapMessage
                    break
                }
            } while cursor != nil
        } catch is FileTreeLevelPageUnavailable where fetchedPage {
            incompleteMessage = FileTreeViewModel.incompleteLevelMessage
        } catch is CancellationError {
            return .cancelled
        } catch let error as VaultError {
            if case .Cancelled = error {
                return .cancelled
            }
            return .failure(FileTreeViewModel.message(for: error))
        } catch {
            return .failure(FileTreeViewModel.message(for: error))
        }

        guard let prepared = Self.prepare(
            dirs: dirs,
            files: files,
            depth: depth,
            parentPath: parentPath,
            organization: organization,
            isComplete: incompleteMessage == nil,
            incompleteMessage: incompleteMessage,
            nativeCancel: nativeCancel,
            preparationEvent: .initial(parentPath: parentPath),
            preparationHook: preparationHook)
        else { return .cancelled }
        // Test-only race seam after the last preparation cancellation check.
        // A real owner can be revoked in this same return-to-MainActor window.
        preparationHook?.call(.completed(parentPath: parentPath), nativeCancel)
        return .success(prepared)
    }

    /// Reproject a not-yet-published level when live save metadata arrived
    /// during its native read. Sorting, grouping, headers, and row-state
    /// construction remain off MainActor and use the newest overlay values.
    func reprepare(
        _ prepared: FileTreePreparedLevel,
        overlay: FileTreeSummaryOverlay,
        parentPath: String,
        depth: Int,
        organization: FileTreeOrganizationInput,
        nativeCancel: CancelToken
    ) async -> FileTreeLevelOutcome {
        await withTaskCancellationHandler {
            await withCheckedContinuation { continuation in
                queue.async { [preparationHook] in
                    continuation.resume(
                        returning: Self.performReprepare(
                            prepared,
                            overlay: overlay,
                            parentPath: parentPath,
                            depth: depth,
                            organization: organization,
                            nativeCancel: nativeCancel,
                            preparationHook: preparationHook))
                }
            }
        } onCancel: {
            nativeCancel.cancel()
        }
    }

    private static func performReprepare(
        _ prepared: FileTreePreparedLevel,
        overlay: FileTreeSummaryOverlay,
        parentPath: String,
        depth: Int,
        organization: FileTreeOrganizationInput,
        nativeCancel: CancelToken,
        preparationHook: FileTreePreparationHook?
    ) -> FileTreeLevelOutcome {
        guard !nativeCancel.isCancelled() else { return .cancelled }
        var dirs: [DirNodeSummary] = []
        var files: [FileSummary] = []
        dirs.reserveCapacity(prepared.nodes.count)
        files.reserveCapacity(prepared.fileStatesByPath.count)
        for (offset, node) in prepared.nodes.enumerated() {
            if offset.isMultiple(of: 256), nativeCancel.isCancelled() {
                return .cancelled
            }
            switch node.kind {
            case let .directory(childDirs, childFiles, hasFolderNote):
                guard case let .dir(id) = node.nodeID else { continue }
                dirs.append(
                    DirNodeSummary(
                        id: id,
                        path: node.path,
                        name: node.name,
                        childDirCount: UInt32(clamping: childDirs),
                        childFileCount: UInt32(clamping: childFiles),
                        hasFolderNote: hasFolderNote))
            case let .file(state):
                files.append(
                    overlay.summariesByPath[node.path] ?? state.summary)
            }
        }
        guard let next = Self.prepare(
            dirs: dirs,
            files: files,
            depth: depth,
            parentPath: parentPath,
            organization: organization,
            isComplete: prepared.isComplete,
            incompleteMessage: prepared.incompleteMessage,
            nativeCancel: nativeCancel,
            preparationEvent: .overlay(parentPath: parentPath),
            preparationHook: preparationHook)
        else { return .cancelled }
        return .success(next)
    }

    private static func prepare(
        dirs: [DirNodeSummary],
        files: [FileSummary],
        depth: Int,
        parentPath: String,
        organization: FileTreeOrganizationInput,
        isComplete: Bool,
        incompleteMessage: String?,
        nativeCancel: CancelToken,
        preparationEvent: FileTreePreparationEvent?,
        preparationHook: FileTreePreparationHook?
    ) -> FileTreePreparedLevel? {
        var visibleFiles: [FileSummary] = []
        visibleFiles.reserveCapacity(files.count)
        for (offset, file) in files.enumerated() {
            if offset.isMultiple(of: 256), nativeCancel.isCancelled() {
                return nil
            }
            if !FileTreeViewModel.isRepresentedFolderNote(
                path: file.path, name: file.name)
            {
                visibleFiles.append(file)
            }
        }
        guard !nativeCancel.isCancelled() else { return nil }
        var signalledPreparation = false
        let preparationCancellationCheck = {
            if !signalledPreparation, let preparationEvent {
                signalledPreparation = true
                preparationHook?.call(preparationEvent, nativeCancel)
            }
            return nativeCancel.isCancelled()
        }
        guard let organized = FileTreeLevelOrganization.organizeCancellable(
            files: visibleFiles,
            parentPath: parentPath,
            depth: depth,
            input: organization,
            isComplete: isComplete,
            isCancelled: preparationCancellationCheck)
        else { return nil }
        var byPath: [String: FileSummary] = [:]
        byPath.reserveCapacity(visibleFiles.count)
        for (offset, file) in visibleFiles.enumerated() {
            if offset.isMultiple(of: 256), nativeCancel.isCancelled() {
                return nil
            }
            byPath[file.path] = file
        }
        var orderedFiles: [FileSummary] = []
        orderedFiles.reserveCapacity(organized.orderedPaths.count)
        for (offset, path) in organized.orderedPaths.enumerated() {
            if offset.isMultiple(of: 256), nativeCancel.isCancelled() {
                return nil
            }
            if let file = byPath[path] { orderedFiles.append(file) }
        }
        let presentation = organized.presentation

        var nodes: [TreeNode] = []
        nodes.reserveCapacity(dirs.count + orderedFiles.count)
        var directoriesByPath: [String: TreeNode] = [:]
        var directoriesByID: [NodeID: TreeNode] = [:]
        var directoryOrder: [NodeID] = []
        var directoryOrdinalByID: [NodeID: Int] = [:]
        directoriesByPath.reserveCapacity(dirs.count)
        directoriesByID.reserveCapacity(dirs.count)
        directoryOrder.reserveCapacity(dirs.count)
        directoryOrdinalByID.reserveCapacity(dirs.count)
        for (offset, dir) in dirs.enumerated() {
            if offset.isMultiple(of: 256), nativeCancel.isCancelled() {
                return nil
            }
            let node = TreeNode(
                    nodeID: .dir(dir.id),
                    path: dir.path,
                    name: dir.name,
                    depth: depth,
                    kind: .directory(
                        childDirCount: Int(dir.childDirCount),
                        childFileCount: Int(dir.childFileCount),
                        hasFolderNote: dir.hasFolderNote))
            nodes.append(node)
            directoriesByPath[node.path] = node
            directoriesByID[node.nodeID] = node
            directoryOrdinalByID[node.nodeID] = directoryOrder.count
            directoryOrder.append(node.nodeID)
        }
        var fileStatesByPath: [String: FileTreeFileState] = [:]
        fileStatesByPath.reserveCapacity(orderedFiles.count)
        for (offset, summary) in orderedFiles.enumerated() {
            if offset.isMultiple(of: 256), nativeCancel.isCancelled() {
                return nil
            }
            let state = FileTreeFileState(summary: summary)
            fileStatesByPath[summary.path] = state
            nodes.append(
                TreeNode(
                    nodeID: .file(path: summary.path),
                    path: summary.path,
                    name: summary.name,
                    depth: depth,
                    kind: .file(state)))
        }
        guard !nativeCancel.isCancelled() else { return nil }
        return FileTreePreparedLevel(
            nodes: nodes,
            presentation: presentation,
            fileStatesByPath: fileStatesByPath,
            directoriesByPath: directoriesByPath,
            directoriesByID: directoriesByID,
            directoryOrder: directoryOrder,
            directoryOrdinalByID: directoryOrdinalByID,
            isComplete: isComplete,
            incompleteMessage: incompleteMessage)
    }

}

/// Test-only single-page adapters throw this when a continuation is requested.
/// The production worker publishes the materialized prefix with an honest
/// incomplete-level notice instead of treating that adapter limitation as a
/// failed native read.
struct FileTreeLevelPageUnavailable: Error {}
