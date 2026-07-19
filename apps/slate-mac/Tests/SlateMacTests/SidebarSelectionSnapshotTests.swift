// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Combine
import XCTest

@testable import SlateMac

@MainActor
final class SidebarSelectionSnapshotTests: XCTestCase {
    private var root: URL!
    private var cancellables: Set<AnyCancellable> = []

    override func setUpWithError() throws {
        root = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-sidebar-snapshot-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        cancellables = []
        try? FileManager.default.removeItem(at: root)
    }

    private func state() -> AppState {
        AppState(
            recentsStore: RecentVaultsStore(fileURL: root.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
    }

    @discardableResult
    private func open(_ name: String, on state: AppState) throws -> URL {
        let url = root.appendingPathComponent(name)
        try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        state.openVault(at: url)
        XCTAssertNotNil(state.currentSession)
        return url
    }

    private func row(
        _ path: String, directory: Bool = false, markdown: Bool = true, id: Int64 = 1
    ) -> FileTreeSidebar.SelectionRow {
        .init(
            identity: directory ? .node(.dir(id)) : .node(.file(path: path)),
            path: path,
            isDirectory: directory,
            isMarkdown: markdown)
    }

    func testCaptureUsesVisibleOrderFocusAndCreationParent() {
        let owner = NSObject()
        let a = row("Folder/A.md")
        let b = row("Folder/B.canvas", markdown: false)
        let folder = row("Folder", directory: true, markdown: false)
        let model = FileTreeSidebar.SelectionModel(
            focused: b.identity,
            selected: [b.identity, a.identity],
            selectionPathSnapshots: [a.identity: a.path, b.identity: b.path],
            rangeAnchor: a.identity,
            rangeAnchorPathSnapshot: a.path)

        let snapshot = SidebarSelectionSnapshot.capture(
            sessionIdentity: ObjectIdentifier(owner),
            model: model,
            visibleRows: [folder, a, b])

        XCTAssertEqual(snapshot.items.map(\.path), ["Folder/A.md", "Folder/B.canvas"])
        XCTAssertEqual(snapshot.items.map(\.isMarkdown), [true, false])
        XCTAssertEqual(snapshot.focusedPath, "Folder/B.canvas")
        XCTAssertEqual(snapshot.creationParent, "Folder")
    }

    func testLifecycleSeedsEmptyRootRejectsStalePublisherAndClears() throws {
        let state = state()
        try open("A", on: state)
        let sessionA = try XCTUnwrap(state.currentSession)
        XCTAssertEqual(state.sidebarSelectionSnapshot?.sessionIdentity, ObjectIdentifier(sessionA))
        XCTAssertEqual(state.sidebarSelectionSnapshot?.items, [])
        XCTAssertEqual(state.sidebarSelectionSnapshot?.creationParent, "")

        let stale = SidebarSelectionSnapshot(
            sessionIdentity: ObjectIdentifier(sessionA),
            items: [.init(path: "A.md", isDirectory: false, isMarkdown: true)],
            focusedPath: "A.md",
            creationParent: "")
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(stale))

        try open("B", on: state)
        let sessionB = try XCTUnwrap(state.currentSession)
        XCTAssertEqual(state.sidebarSelectionSnapshot?.sessionIdentity, ObjectIdentifier(sessionB))
        XCTAssertEqual(state.sidebarSelectionSnapshot?.items, [])
        XCTAssertFalse(state.publishSidebarSelectionSnapshot(stale))
        XCTAssertEqual(state.sidebarSelectionSnapshot?.sessionIdentity, ObjectIdentifier(sessionB))

        XCTAssertTrue(state.closeVault())
        XCTAssertNil(state.sidebarSelectionSnapshot)
    }

    func testSameFocusCommandClickPublishesSynchronouslyWithoutListOnChange() throws {
        let state = state()
        try open("vault", on: state)
        let sessionID = ObjectIdentifier(try XCTUnwrap(state.currentSession))
        let a = row("A.md")
        let b = row("B.md")
        let rows = [a, b]
        var model = FileTreeSidebar.SelectionModel(
            focused: b.identity,
            selected: [a.identity, b.identity],
            selectionPathSnapshots: [a.identity: a.path, b.identity: b.path],
            rangeAnchor: a.identity,
            rangeAnchorPathSnapshot: a.path)

        FileTreeSidebar.mutateSelectionAndPublish(
            model: &model,
            capturedSessionIdentity: sessionID,
            visibleRows: rows,
            appState: state
        ) { $0.applyPointerClick(.toggle, row: a, visibleRows: rows) }

        XCTAssertEqual(model.focused, b.identity)
        XCTAssertEqual(state.sidebarSelectionSnapshot?.items.map(\.path), ["B.md"])
        XCTAssertEqual(state.sidebarSelectionSnapshot?.focusedPath, "B.md")
    }

    func testBatchPublishesOnceAfterCompleteRemapAndFinalFocus() throws {
        let state = state()
        try open("vault", on: state)
        let sessionID = ObjectIdentifier(try XCTUnwrap(state.currentSession))
        let a = row("A.md")
        let b = row("B.md")
        let movedA = row("Moved/A.md")
        let movedB = row("Moved/B.md")
        var model = FileTreeSidebar.SelectionModel(
            focused: b.identity,
            selected: [a.identity, b.identity],
            selectionPathSnapshots: [a.identity: a.path, b.identity: b.path],
            rangeAnchor: a.identity,
            rangeAnchorPathSnapshot: a.path)
        var emissions = 0
        state.$sidebarSelectionSnapshot.dropFirst().sink { _ in emissions += 1 }
            .store(in: &cancellables)

        FileTreeSidebar.mutateSelectionAndPublish(
            model: &model,
            capturedSessionIdentity: sessionID,
            visibleRows: [movedA, movedB],
            appState: state
        ) {
            $0.remapKnownMoves(
                [
                    .init(oldPath: "A.md", newPath: "Moved/A.md", isDirectory: false),
                    .init(oldPath: "B.md", newPath: "Moved/B.md", isDirectory: false),
                ],
                identityForRemappedPath: { _, path in .node(.file(path: path)) })
            $0.focusAfterStructuralMutation(movedB)
        }

        XCTAssertEqual(emissions, 1)
        XCTAssertEqual(state.sidebarSelectionSnapshot?.items.map(\.path), ["Moved/A.md", "Moved/B.md"])
        XCTAssertEqual(state.sidebarSelectionSnapshot?.focusedPath, "Moved/B.md")
    }

    func testPostMutationTypeSelectAndDisclosureEdgesPublishSynchronously() throws {
        let state = state()
        try open("vault", on: state)
        let sessionID = ObjectIdentifier(try XCTUnwrap(state.currentSession))
        let a = row("A.md")
        let folder = row("Folder", directory: true, markdown: false)
        let child = row("Folder/Child.md")
        let rows = [a, folder, child]
        var model = FileTreeSidebar.SelectionModel()

        func apply(_ mutation: (inout FileTreeSidebar.SelectionModel) -> Void) {
            FileTreeSidebar.mutateSelectionAndPublish(
                model: &model, capturedSessionIdentity: sessionID,
                visibleRows: rows, appState: state, mutation: mutation)
        }
        apply { $0.focusAfterStructuralMutation(a) }
        XCTAssertEqual(state.sidebarSelectionSnapshot?.items.map(\.path), ["A.md"])
        apply { $0.reveal(folder) }  // type-select collapses to one row
        XCTAssertEqual(state.sidebarSelectionSnapshot?.items.map(\.path), ["Folder"])
        XCTAssertEqual(state.sidebarSelectionSnapshot?.creationParent, "Folder")
        apply { $0.reveal(child) }  // left/right parent/child collapses to one row
        XCTAssertEqual(state.sidebarSelectionSnapshot?.items.map(\.path), ["Folder/Child.md"])
        XCTAssertEqual(state.sidebarSelectionSnapshot?.creationParent, "Folder")
    }

    func testActivationRejectsMissingAndTypeChangedItems() throws {
        let state = state()
        let vault = try open("vault", on: state)
        let url = vault.appendingPathComponent("Target.md")
        try "# target".write(to: url, atomically: true, encoding: .utf8)
        let snapshot = SidebarSelectionSnapshot(
            sessionIdentity: ObjectIdentifier(try XCTUnwrap(state.currentSession)),
            items: [.init(path: "Target.md", isDirectory: false, isMarkdown: true)],
            focusedPath: "Target.md",
            creationParent: "")
        let intent = try XCTUnwrap(
            SidebarActionCatalog.evaluation(
                for: SlateCommandID.duplicateEntry, snapshot: snapshot)?.intent)

        try FileManager.default.removeItem(at: url)
        XCTAssertThrowsError(try state.validateSidebarActionIntent(intent)) {
            XCTAssertEqual($0.localizedDescription, AppState.sidebarSelectionChangedReason)
        }
        try FileManager.default.createDirectory(at: url, withIntermediateDirectories: false)
        XCTAssertThrowsError(try state.validateSidebarActionIntent(intent)) {
            XCTAssertEqual($0.localizedDescription, AppState.sidebarSelectionChangedReason)
        }
    }

    func testActivationRejectsStaleSessionIntent() throws {
        let state = state()
        try open("A", on: state)
        let staleSnapshot = SidebarSelectionSnapshot(
            sessionIdentity: ObjectIdentifier(try XCTUnwrap(state.currentSession)),
            items: [.init(path: "A.md", isDirectory: false, isMarkdown: true)],
            focusedPath: "A.md",
            creationParent: "")
        let intent = try XCTUnwrap(
            SidebarActionCatalog.evaluation(
                for: SlateCommandID.sidebarOpen, snapshot: staleSnapshot)?.intent)
        try open("B", on: state)

        XCTAssertThrowsError(try state.validateSidebarActionIntent(intent)) {
            XCTAssertEqual($0.localizedDescription, AppState.sidebarSelectionStaleReason)
        }
    }

    func testActivationRejectsLiveAndDanglingSymlinkReplacement() throws {
        let state = state()
        let vault = try open("vault", on: state)
        let path = "Target.md"
        let target = vault.appendingPathComponent(path)
        let other = vault.appendingPathComponent("Other.md")
        try "# target".write(to: target, atomically: true, encoding: .utf8)
        try "# other".write(to: other, atomically: true, encoding: .utf8)
        let snapshot = SidebarSelectionSnapshot(
            sessionIdentity: ObjectIdentifier(try XCTUnwrap(state.currentSession)),
            items: [.init(path: path, isDirectory: false, isMarkdown: true)],
            focusedPath: path,
            creationParent: "")
        let intent = try XCTUnwrap(
            SidebarActionCatalog.evaluation(
                for: SlateCommandID.sidebarOpen, snapshot: snapshot)?.intent)

        try FileManager.default.removeItem(at: target)
        try FileManager.default.createSymbolicLink(at: target, withDestinationURL: other)
        XCTAssertThrowsError(try state.validateSidebarActionIntent(intent))
        try FileManager.default.removeItem(at: other)
        XCTAssertThrowsError(try state.validateSidebarActionIntent(intent))
    }

    func testGenericActivationValidationUsesOnlyAllItemLstatChecks() throws {
        let state = state()
        let vault = try open("many", on: state)
        let paths = (0..<64).map { "Note-\($0).md" }
        for path in paths {
            try "# note".write(
                to: vault.appendingPathComponent(path),
                atomically: true,
                encoding: .utf8)
        }
        let snapshot = SidebarSelectionSnapshot(
            sessionIdentity: ObjectIdentifier(try XCTUnwrap(state.currentSession)),
            items: paths.map {
                .init(path: $0, isDirectory: false, isMarkdown: true)
            },
            focusedPath: paths.last,
            creationParent: "")
        let openIntent = try XCTUnwrap(
            SidebarActionCatalog.evaluation(
                for: SlateCommandID.sidebarOpen, snapshot: snapshot)?.intent)

        _ = try state.validateSidebarActionIntent(openIntent)

        let wikilinkIntent = try XCTUnwrap(
            SidebarActionCatalog.evaluation(
                for: SlateCommandID.sidebarCopyWikilink,
                snapshot: SidebarSelectionSnapshot(
                    sessionIdentity: snapshot.sessionIdentity,
                    items: [snapshot.items[0]],
                    focusedPath: paths[0],
                    creationParent: ""))?.intent)
        _ = try state.validateSidebarActionIntent(wikilinkIntent)

        let source = try appSource("AppState.swift")
        let validation = try functionBody(named: "validateSidebarActionIntent", in: source)
        XCTAssertFalse(validation.contains("getFileSummary"))
        XCTAssertFalse(validation.contains("sidebarActionIndexedMarkdownLookup"))
    }

    func testActivationRechecksCurrentActionSpecificAvailability() throws {
        let state = state()
        let vault = try open("availability", on: state)
        try "# A".write(
            to: vault.appendingPathComponent("A.md"),
            atomically: true,
            encoding: .utf8)
        let snapshot = SidebarSelectionSnapshot(
            sessionIdentity: ObjectIdentifier(try XCTUnwrap(state.currentSession)),
            items: [.init(path: "A.md", isDirectory: false, isMarkdown: true)],
            focusedPath: "A.md",
            creationParent: "")
        let intent = try XCTUnwrap(
            SidebarActionCatalog.evaluation(
                for: SlateCommandID.renameEntry, snapshot: snapshot)?.intent)
        let loading = "Sidebar actions are still loading."
        state.sidebarActionAvailabilityReasonProvider = {
            $0 == SlateCommandID.renameEntry ? loading : nil
        }

        XCTAssertThrowsError(try state.validateSidebarActionIntent(intent)) {
            XCTAssertEqual($0.localizedDescription, loading)
        }
        XCTAssertNil(state.renamingNode, "availability rejection occurs before staging")
    }

    func testChildLevelInvalidationRetainsCompleteDeleteFallbackRow() throws {
        func file(_ path: String) -> FileSummary {
            FileSummary(
                path: path, name: (path as NSString).lastPathComponent,
                mtimeMs: 0, sizeBytes: 0, isMarkdown: true,
                displayName: nil, createdDate: nil, createdMs: nil,
                wordCount: nil, preview: nil, taskTotal: 0, taskOpen: 0)
        }
        func listing(dirs: [DirNodeSummary], files: [FileSummary]) -> DirListing {
            DirListing(
                dirs: dirs,
                files: FileSummaryPage(
                    items: files, nextCursor: nil,
                    totalFiltered: UInt64(files.count)))
        }
        let folder = DirNodeSummary(
            id: 1, path: "Folder", name: "Folder",
            childDirCount: 0, childFileCount: 2, hasFolderNote: false)
        var failChildRefetch = false
        let tree = FileTreeViewModel()
        tree.bindForTesting { parent in
            if parent.isEmpty { return listing(dirs: [folder], files: []) }
            if failChildRefetch { throw VaultError.Db(message: "refetch failed") }
            return listing(
                dirs: [], files: [file("Folder/A.md"), file("Folder/B.md")])
        }
        tree.expand(try XCTUnwrap(tree.rootLevel.first))
        let target = try XCTUnwrap(
            tree.deleteFocusTarget(
                deletedPath: "Folder/A.md", parentPath: "Folder"))
        let targetNode = try XCTUnwrap(tree.node(for: target))
        let captured = FileTreeSidebar.SelectionRow(
            identity: .node(targetNode.nodeID),
            path: targetNode.path,
            isDirectory: targetNode.isDirectory,
            isMarkdown: targetNode.isMarkdown)

        failChildRefetch = true
        tree.treeInvalidation(parent: .dir(1))
        XCTAssertNil(tree.node(for: target), "the real child cache was dropped before refetch failed")
        let plan = FileTreeSidebar.PendingPostMutationFocus(
            targetPath: nil,
            capturedDeleteRow: captured,
            suppressOpen: true)
        XCTAssertEqual(
            FileTreeSidebar.postMutationFocusRow(
                for: plan, visibleRows: []),
            captured,
            "delete focus uses the full pre-invalidation row, not a dead NodeID lookup")
    }

    func testDeleteFallbackGuardSurvivesLaterAbsentEdgesWithoutRepublishing() throws {
        let state = state()
        try open("delete-restore", on: state)
        let sessionID = ObjectIdentifier(try XCTUnwrap(state.currentSession))
        let deleted = row("Folder/A.md")
        let fallback = row("Folder/B.md", id: 2)
        var model = FileTreeSidebar.SelectionModel(
            focused: deleted.identity,
            selected: [deleted.identity],
            selectionPathSnapshots: [deleted.identity: deleted.path],
            rangeAnchor: deleted.identity,
            rangeAnchorPathSnapshot: deleted.path)
        var pending: FileTreeSidebar.PendingPostMutationFocus? =
            FileTreeSidebar.PendingPostMutationFocus(
            targetPath: nil,
            capturedDeleteRow: fallback,
            suppressOpen: true)
        var emissions = 0
        state.$sidebarSelectionSnapshot.dropFirst().sink { _ in emissions += 1 }
            .store(in: &cancellables)

        let installed = FileTreeSidebar.installPendingPostMutationFocus(
            try XCTUnwrap(pending),
            model: &model,
            capturedSessionIdentity: sessionID,
            visibleRows: [],
            appState: state)

        XCTAssertEqual(installed, fallback)
        XCTAssertEqual(emissions, 1)
        XCTAssertEqual(state.sidebarSelectionSnapshot?.items.map(\.path), [fallback.path])
        XCTAssertEqual(state.sidebarSelectionSnapshot?.focusedPath, fallback.path)

        pending = FileTreeSidebar.pendingPostMutationFocusAfterInstall(
            try XCTUnwrap(pending),
            installedRow: fallback,
            visibleRows: [])
        XCTAssertTrue(try XCTUnwrap(pending).installedDeleteFallback)

        for _ in 0..<2 { // loading edge, then failed-refetch/error edge
            XCTAssertEqual(
                FileTreeSidebar.restorePendingPostMutationSelection(
                    pending: &pending,
                    model: &model,
                    capturedSessionIdentity: sessionID,
                    visibleRows: [],
                    appState: state,
                    resolveCurrentRow: { _ in nil }),
                .wait)
            XCTAssertNotNil(pending)
            XCTAssertEqual(emissions, 1)
            XCTAssertEqual(state.sidebarSelectionSnapshot?.items.map(\.path), [fallback.path])
            XCTAssertEqual(state.sidebarSelectionSnapshot?.focusedPath, fallback.path)
        }

        XCTAssertEqual(
            FileTreeSidebar.restorePendingPostMutationSelection(
                pending: &pending,
                model: &model,
                capturedSessionIdentity: sessionID,
                visibleRows: [fallback],
                appState: state,
                resolveCurrentRow: { $0 == fallback.identity ? fallback : nil }),
            .clearedGuard)
        XCTAssertNil(pending, "exact fallback rematerialization clears the guard")
        XCTAssertEqual(emissions, 1, "guard completion does not republish unchanged selection")

        pending = FileTreeSidebar.pendingPostMutationFocusAfterInstall(
            FileTreeSidebar.PendingPostMutationFocus(
                targetPath: nil,
                capturedDeleteRow: fallback,
                suppressOpen: true),
            installedRow: fallback,
            visibleRows: [])
        FileTreeSidebar.cancelPendingPostMutationFocus(&pending)
        XCTAssertNil(pending, "a newer structural mutation cancels the older guard")

        let source = try appSource("FileTreeSidebar.swift")
        XCTAssertTrue(
            try functionBody(named: "restorePendingPostMutationFocus", in: source)
                .contains("Self.restorePendingPostMutationSelection"))
        XCTAssertTrue(
            try functionBody(named: "handleTreeMutation", in: source)
                .contains("Self.cancelPendingPostMutationFocus"))
    }

    func testDeleteFallbackGuardReleasesForSupersedingFocusHiddenOrReusedIdentity() throws {
        let state = state()
        try open("delete-guard-release", on: state)
        let sessionID = ObjectIdentifier(try XCTUnwrap(state.currentSession))
        let fallback = row("Folder/B.md")
        let other = row("Other.md", id: 2)

        func guarded(_ row: FileTreeSidebar.SelectionRow) ->
            FileTreeSidebar.PendingPostMutationFocus
        {
            FileTreeSidebar.PendingPostMutationFocus(
                targetPath: nil,
                capturedDeleteRow: row,
                suppressOpen: true,
                installedDeleteFallback: true)
        }

        var model = FileTreeSidebar.SelectionModel()
        model.reveal(fallback)
        model.reveal(other)
        var pending: FileTreeSidebar.PendingPostMutationFocus? = guarded(fallback)
        XCTAssertEqual(
            FileTreeSidebar.restorePendingPostMutationSelection(
                pending: &pending,
                model: &model,
                capturedSessionIdentity: sessionID,
                visibleRows: [other],
                appState: state,
                resolveCurrentRow: { _ in nil }),
            .cancelAndReconcile)
        XCTAssertNil(pending, "new semantic focus releases the obsolete guard")

        model.reveal(fallback)
        pending = guarded(fallback)
        XCTAssertEqual(
            FileTreeSidebar.restorePendingPostMutationSelection(
                pending: &pending,
                model: &model,
                capturedSessionIdentity: sessionID,
                visibleRows: [],
                appState: state,
                resolveCurrentRow: { $0 == fallback.identity ? fallback : nil }),
            .cancelAndReconcile)
        XCTAssertNil(pending, "materialized-but-hidden fallback releases to reconciliation")

        let directory = row("Folder", directory: true, markdown: false, id: 7)
        let reused = row("Elsewhere", directory: true, markdown: false, id: 7)
        model.reveal(directory)
        pending = guarded(directory)
        XCTAssertEqual(
            FileTreeSidebar.restorePendingPostMutationSelection(
                pending: &pending,
                model: &model,
                capturedSessionIdentity: sessionID,
                visibleRows: [reused],
                appState: state,
                resolveCurrentRow: { $0 == reused.identity ? reused : nil }),
            .cancelAndReconcile)
        XCTAssertNil(pending, "reused stable identity releases to path-mismatch reconciliation")
    }

    func testDeleteFallbackGuardReleasesSamePathReplacementAndChangedMetadata() throws {
        let state = state()
        try open("delete-guard-replacement", on: state)
        let sessionID = ObjectIdentifier(try XCTUnwrap(state.currentSession))

        let capturedDirectory = row("Folder", directory: true, markdown: false, id: 7)
        let replacementDirectory = row("Folder", directory: true, markdown: false, id: 8)
        var model = FileTreeSidebar.SelectionModel()
        model.reveal(capturedDirectory)
        var pending: FileTreeSidebar.PendingPostMutationFocus? = .init(
            targetPath: nil,
            capturedDeleteRow: capturedDirectory,
            suppressOpen: true,
            installedDeleteFallback: true)
        XCTAssertEqual(
            FileTreeSidebar.restorePendingPostMutationSelection(
                pending: &pending,
                model: &model,
                capturedSessionIdentity: sessionID,
                visibleRows: [replacementDirectory],
                appState: state,
                resolveCurrentRow: { _ in nil }),
            .cancelAndReconcile)
        XCTAssertNil(pending)
        FileTreeSidebar.mutateSelectionAndPublish(
            model: &model,
            capturedSessionIdentity: sessionID,
            visibleRows: [replacementDirectory],
            appState: state
        ) {
            $0.reconcile(
                visibleRows: [replacementDirectory],
                resolveCurrentPath: { _ in nil })
        }
        XCTAssertEqual(
            state.sidebarSelectionSnapshot?.items, [],
            "a same-path row with a new stable identity is not command-authoritative")

        let capturedMarkdown = row("Folder/Note.md", markdown: true)
        let refreshedNonMarkdown = row("Folder/Note.md", markdown: false)
        model.reveal(capturedMarkdown)
        pending = .init(
            targetPath: nil,
            capturedDeleteRow: capturedMarkdown,
            suppressOpen: true,
            installedDeleteFallback: true)
        XCTAssertEqual(
            FileTreeSidebar.restorePendingPostMutationSelection(
                pending: &pending,
                model: &model,
                capturedSessionIdentity: sessionID,
                visibleRows: [refreshedNonMarkdown],
                appState: state,
                resolveCurrentRow: { _ in refreshedNonMarkdown }),
            .cancelAndReconcile)
        XCTAssertNil(pending)
        FileTreeSidebar.mutateSelectionAndPublish(
            model: &model,
            capturedSessionIdentity: sessionID,
            visibleRows: [refreshedNonMarkdown],
            appState: state
        ) {
            $0.reconcile(
                visibleRows: [refreshedNonMarkdown],
                resolveCurrentPath: { _ in refreshedNonMarkdown.path })
        }
        XCTAssertEqual(state.sidebarSelectionSnapshot?.items.first?.isMarkdown, false)
        XCTAssertNil(
            SidebarActionCatalog.evaluation(
                for: SlateCommandID.sidebarCopyWikilink,
                snapshot: state.sidebarSelectionSnapshot)?.intent,
            "changed row metadata replaces the captured command capability")
    }

    func testDeleteFallbackGuardOwnershipRejectsNewMutationAndSessionReplacement() throws {
        let state = state()
        try open("delete-guard-owner", on: state)
        let currentSessionID = ObjectIdentifier(try XCTUnwrap(state.currentSession))
        let staleSessionOwner = NSObject()
        let fallback = row("Folder/B.md")
        var model = FileTreeSidebar.SelectionModel()
        model.reveal(fallback)

        var pending: FileTreeSidebar.PendingPostMutationFocus? = .init(
            targetPath: nil,
            capturedDeleteRow: fallback,
            suppressOpen: true,
            installedDeleteFallback: true,
            ownerSessionIdentity: currentSessionID,
            ownerMutationToken: 41)
        XCTAssertEqual(
            FileTreeSidebar.restorePendingPostMutationSelection(
                pending: &pending,
                model: &model,
                capturedSessionIdentity: currentSessionID,
                visibleRows: [],
                appState: state,
                currentSessionIdentity: currentSessionID,
                currentMutationToken: 42,
                resolveCurrentRow: { _ in nil }),
            .cancelAndReconcile)
        XCTAssertNil(pending, "a newer TreeMutation token cancels the prior guard")

        pending = .init(
            targetPath: nil,
            capturedDeleteRow: fallback,
            suppressOpen: true,
            installedDeleteFallback: true,
            ownerSessionIdentity: ObjectIdentifier(staleSessionOwner),
            ownerMutationToken: 42)
        XCTAssertEqual(
            FileTreeSidebar.restorePendingPostMutationSelection(
                pending: &pending,
                model: &model,
                capturedSessionIdentity: ObjectIdentifier(staleSessionOwner),
                visibleRows: [],
                appState: state,
                currentSessionIdentity: currentSessionID,
                currentMutationToken: 42,
                resolveCurrentRow: { _ in nil }),
            .cancelAndReconcile)
        XCTAssertNil(pending, "vault/session replacement cancels the prior guard")

        let source = try appSource("FileTreeSidebar.swift")
        XCTAssertGreaterThanOrEqual(
            source.components(
                separatedBy:
                    "Self.cancelPendingPostMutationFocus(&pendingPostMutationFocus)"
            ).count - 1,
            3,
            "new mutation plus both vault-change production paths cancel pending ownership")

        pending = .init(
            targetPath: nil,
            capturedDeleteRow: fallback,
            suppressOpen: true,
            installedDeleteFallback: true,
            ownerSessionIdentity: ObjectIdentifier(staleSessionOwner),
            ownerMutationToken: 42)
        state.treeSelectedNode = AppState.TreeSelection(
            path: fallback.path,
            isDirectory: fallback.isDirectory)
        FileTreeSidebar.publishGuardAwareTreeSelectionMirror(
            pending: pending,
            model: model,
            currentSessionIdentity: currentSessionID,
            currentMutationToken: 42,
            resolvedSelection: nil,
            appState: state)
        XCTAssertNil(
            state.treeSelectedNode,
            "a stale tree owner cannot restore its fallback after AppState replaced the session")

        let restoreBody = try functionBody(
            named: "restorePendingPostMutationFocus", in: source)
        let mirrorBody = try functionBody(
            named: "mirrorTreeSelectionToAppState", in: source)
        for body in [restoreBody, mirrorBody] {
            XCTAssertTrue(
                body.contains(
                    "currentSessionIdentity: appState.currentSession.map(ObjectIdentifier.init)"),
                "guard consumers must use AppState's authoritative current session")
        }
    }

    func testInstalledDeleteGuardPreservesLegacyMirrorAcrossCarrierWaitAndClear() throws {
        let state = state()
        try open("delete-guard-mirror", on: state)
        let sessionID = ObjectIdentifier(try XCTUnwrap(state.currentSession))
        let deleted = row("Folder/A.md")
        let fallback = row("Folder/B.md", id: 2)
        let expectedMirror = AppState.TreeSelection(
            path: fallback.path,
            isDirectory: fallback.isDirectory)
        var model = FileTreeSidebar.SelectionModel()
        model.reveal(deleted)
        let initial = FileTreeSidebar.PendingPostMutationFocus(
            targetPath: nil,
            capturedDeleteRow: fallback,
            suppressOpen: true,
            ownerSessionIdentity: sessionID,
            ownerMutationToken: 7)
        var emissions = 0
        state.$sidebarSelectionSnapshot.dropFirst().sink { _ in emissions += 1 }
            .store(in: &cancellables)
        let installed = try XCTUnwrap(
            FileTreeSidebar.installPendingPostMutationFocus(
                initial,
                model: &model,
                capturedSessionIdentity: sessionID,
                visibleRows: [],
                appState: state))
        var pending = FileTreeSidebar.pendingPostMutationFocusAfterInstall(
            initial,
            installedRow: installed,
            visibleRows: [])
        XCTAssertEqual(emissions, 1)
        XCTAssertEqual(state.treeSelectedNode, expectedMirror)

        FileTreeSidebar.publishGuardAwareTreeSelectionMirror(
            pending: pending,
            model: model,
            currentSessionIdentity: sessionID,
            currentMutationToken: 7,
            resolvedSelection: nil,
            appState: state)
        XCTAssertEqual(
            state.treeSelectedNode, expectedMirror,
            "the carrier callback preserves the captured fallback while unmaterialized")

        XCTAssertEqual(
            FileTreeSidebar.restorePendingPostMutationSelection(
                pending: &pending,
                model: &model,
                capturedSessionIdentity: sessionID,
                visibleRows: [],
                appState: state,
                currentSessionIdentity: sessionID,
                currentMutationToken: 7,
                resolveCurrentRow: { _ in nil }),
            .wait)
        FileTreeSidebar.publishGuardAwareTreeSelectionMirror(
            pending: pending,
            model: model,
            currentSessionIdentity: sessionID,
            currentMutationToken: 7,
            resolvedSelection: nil,
            appState: state)
        XCTAssertEqual(state.treeSelectedNode, expectedMirror)
        XCTAssertEqual(emissions, 1, "a guarded visibleRows wait does not republish")

        XCTAssertEqual(
            FileTreeSidebar.restorePendingPostMutationSelection(
                pending: &pending,
                model: &model,
                capturedSessionIdentity: sessionID,
                visibleRows: [fallback],
                appState: state,
                currentSessionIdentity: sessionID,
                currentMutationToken: 7,
                resolveCurrentRow: { _ in fallback }),
            .clearedGuard)
        FileTreeSidebar.publishGuardAwareTreeSelectionMirror(
            pending: pending,
            model: model,
            currentSessionIdentity: sessionID,
            currentMutationToken: 7,
            resolvedSelection: expectedMirror,
            appState: state)
        XCTAssertEqual(state.treeSelectedNode, expectedMirror)
        XCTAssertEqual(emissions, 1, "exact rematerialization clears without republishing")

        pending = FileTreeSidebar.pendingPostMutationFocusAfterInstall(
            initial,
            installedRow: fallback,
            visibleRows: [])
        model.reveal(row("Other.md", id: 3))
        XCTAssertEqual(
            FileTreeSidebar.restorePendingPostMutationSelection(
                pending: &pending,
                model: &model,
                capturedSessionIdentity: sessionID,
                visibleRows: [],
                appState: state,
                currentSessionIdentity: sessionID,
                currentMutationToken: 7,
                resolveCurrentRow: { _ in nil }),
            .cancelAndReconcile)
        FileTreeSidebar.publishGuardAwareTreeSelectionMirror(
            pending: pending,
            model: model,
            currentSessionIdentity: sessionID,
            currentMutationToken: 7,
            resolvedSelection: nil,
            appState: state)
        XCTAssertNil(
            state.treeSelectedNode,
            "cancel outcomes return to the normal fail-closed legacy mirror")

        let source = try appSource("FileTreeSidebar.swift")
        XCTAssertTrue(
            try functionBody(named: "mirrorTreeSelectionToAppState", in: source)
                .contains("Self.publishGuardAwareTreeSelectionMirror"))
    }

    func testDeferredBatchMovePublishesOnlyAfterRemapAndMaterializedFinalFocus() throws {
        let state = state()
        try open("deferred", on: state)
        let sessionID = ObjectIdentifier(try XCTUnwrap(state.currentSession))
        let a = row("A.md")
        let b = row("B.md")
        let movedA = row("Moved/A.md")
        let movedB = row("Moved/B.md")
        let original = FileTreeSidebar.SelectionModel(
            focused: b.identity,
            selected: [a.identity, b.identity],
            selectionPathSnapshots: [a.identity: a.path, b.identity: b.path],
            rangeAnchor: a.identity,
            rangeAnchorPathSnapshot: a.path)
        let index = FileTreeSidebar.SelectionModel.KnownMoveIndex([
            .init(oldPath: a.path, newPath: movedA.path, isDirectory: false),
            .init(oldPath: b.path, newPath: movedB.path, isDirectory: false),
        ])
        var emissions = 0
        state.$sidebarSelectionSnapshot.dropFirst().sink { _ in emissions += 1 }
            .store(in: &cancellables)
        var installed = original
        var visits = 0
        let plan = FileTreeSidebar.BatchMoveFocusPlan(
            path: movedB.path,
            shouldRevealMovedAncestry: true)
        let pending = FileTreeSidebar.applyOrDeferBatchMoveSelection(
            plan: plan,
            moveIndex: index,
            targetRow: nil,
            expectedFocusedPath: b.path,
            model: &installed,
            capturedSessionIdentity: sessionID,
            visibleRows: [],
            appState: state,
            componentVisits: &visits)

        XCTAssertNotNil(pending)
        XCTAssertEqual(installed, original, "staging does not assign the remapped model")
        XCTAssertEqual(emissions, 0, "unmaterialized remap stays pending and unpublished")

        FileTreeSidebar.installPendingBatchFocus(
            try XCTUnwrap(pending),
            targetRow: movedB,
            model: &installed,
            capturedSessionIdentity: sessionID,
            visibleRows: [movedA, movedB],
            appState: state)
        XCTAssertEqual(emissions, 1)
        XCTAssertEqual(state.sidebarSelectionSnapshot?.items.map(\.path), [movedA.path, movedB.path])
        XCTAssertEqual(state.sidebarSelectionSnapshot?.focusedPath, movedB.path)
    }

    func testPostMutationNativeCarrierCannotRepublishOrOpenDeleteFallback() throws {
        let source = try appSource("FileTreeSidebar.swift")
        let body = try functionBody(named: "applyPostMutationSelectionCarrier", in: source)
        let suppress = try XCTUnwrap(
            body.range(of: "suppressOpenForSelectionChange = true"))
        let assignment = try XCTUnwrap(
            body.range(of: "listSelection = targetRow.identity"))
        XCTAssertLessThan(suppress.lowerBound, assignment.lowerBound)
        XCTAssertTrue(
            source.contains(
                "if suppressOpenForSelectionChange {\n"
                    + "                suppressOpenForSelectionChange = false\n"
                    + "                // A post-Delete carrier uses this same early-return branch"))
        XCTAssertTrue(
            source.contains(
                "suppressOpenForPostMutationFocus = false\n"
                    + "                mirrorTreeSelectionToAppState(selectionModel.focused)"))
        XCTAssertTrue(
            try functionBody(named: "restorePendingPostMutationFocus", in: source)
                .contains("case .cancelAndReconcile:\n            return false"))
    }

    func testReachableOpenFailureAndBothCatchBranchesClearSnapshot() throws {
        let state = state()
        try open("vault", on: state)
        XCTAssertNotNil(state.sidebarSelectionSnapshot)
        let file = root.appendingPathComponent("not-a-vault")
        try "not a directory".write(to: file, atomically: true, encoding: .utf8)
        state.openVault(at: file)
        XCTAssertNil(state.currentSession)
        XCTAssertNil(state.sidebarSelectionSnapshot)

        let source = try appSource("AppState.swift")
        let openVaultBody = try functionBody(named: "openVault", in: source)
        let failureStart = try XCTUnwrap(
            openVaultBody.range(
                of: "} catch let error as VaultError {",
                options: .backwards))
        let failureTail = openVaultBody[failureStart.lowerBound...]
        let catches = failureTail.components(separatedBy: "} catch ").dropFirst()
        XCTAssertEqual(catches.count, 2)
        for catchBody in catches {
            XCTAssertTrue(catchBody.prefix(500).contains("sidebarSelectionSnapshot = nil"))
        }
    }

    func testRealSelectionMutationSitesUseImmediatePublicationHelper() throws {
        let source = try appSource("FileTreeSidebar.swift")
        for forbidden in [
            "selectionModel.reveal(",
            "selectionModel.applyPointerClick(",
            "selectionModel.reconcile(",
            "selectionModel.remapKnownMoves(",
            "selectionModel.removeKnownItems(",
            "selectionModel.focusAfterStructuralMutation(",
            "selectionModel = outcome.model",
        ] {
            XCTAssertFalse(source.contains(forbidden), "bypasses snapshot publication: \(forbidden)")
        }
        for function in [
            "handleTypeSelect", "handleMoveCommand", "installPendingPostMutationFocus",
            "applyMultiSelectClick", "applyOrDeferBatchMoveSelection",
            "installPendingBatchFocus",
            "revertCommandClickSelection",
        ] {
            XCTAssertTrue(
                try functionBody(named: function, in: source).contains("mutateSelectionAndPublish"),
                "\(function) must publish synchronously")
        }
    }

    private func appSource(_ name: String) throws -> String {
        let packageRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(
            contentsOf: packageRoot.appendingPathComponent("Sources/SlateMac/\(name)"),
            encoding: .utf8)
    }

    private func functionBody(named name: String, in source: String) throws -> Substring {
        let start = try XCTUnwrap(source.range(of: "func \(name)("))
        let tail = source[start.lowerBound...]
        let openBrace = try XCTUnwrap(tail.firstIndex(of: "{"))
        var depth = 0
        for index in tail.indices[openBrace...] {
            switch tail[index] {
            case "{": depth += 1
            case "}":
                depth -= 1
                if depth == 0 { return tail[...index] }
            default: break
            }
        }
        throw XCTSkip("unbalanced function body for \(name)")
    }
}
