// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

@MainActor
final class SidebarActionInvocationTests: XCTestCase {
    private var root: URL!
    private enum ProbeError: Error { case formatterFailed }

    private final class RecordingAnnouncer: AnnouncementPosting, @unchecked Sendable {
        private(set) var posts: [(message: String, priority: AnnouncementPriority)] = []

        func post(_ message: String, priority: AnnouncementPriority) {
            posts.append((message, priority))
        }
    }

    override func setUpWithError() throws {
        root = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-sidebar-invocation-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: root)
    }

    private func openVault(
        named name: String,
        files: [String] = [],
        folders: [String] = [],
        announcer: AnnouncementPosting = AppKitAnnouncementPoster()
    ) throws -> AppState {
        let vault = root.appendingPathComponent(name)
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for folder in folders {
            try FileManager.default.createDirectory(
                at: vault.appendingPathComponent(folder),
                withIntermediateDirectories: true)
        }
        for path in files {
            let url = vault.appendingPathComponent(path)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(),
                withIntermediateDirectories: true)
            try "# \((path as NSString).lastPathComponent)".write(
                to: url, atomically: true, encoding: .utf8)
        }
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: root.appendingPathComponent("\(name)-recents.json")),
            externalOpener: { _ in true },
            announcer: announcer)
        state.openVault(at: vault)
        let session = try XCTUnwrap(state.currentSession)
        _ = try session.scanInitial(cancel: CancelToken())
        return state
    }

    private func snapshot(
        on state: AppState,
        _ items: [SidebarSelectionItem],
        focusedPath: String? = nil,
        creationParent: String = ""
    ) throws -> SidebarSelectionSnapshot {
        SidebarSelectionSnapshot(
            sessionIdentity: ObjectIdentifier(try XCTUnwrap(state.currentSession)),
            items: items,
            focusedPath: focusedPath,
            creationParent: creationParent)
    }

    private func item(
        _ path: String,
        directory: Bool = false,
        markdown: Bool = true
    ) -> SidebarSelectionItem {
        SidebarSelectionItem(
            path: path,
            isDirectory: directory,
            isMarkdown: !directory && markdown)
    }

    private func intent(
        _ id: String,
        snapshot: SidebarSelectionSnapshot
    ) throws -> SidebarActionInvocationIntent {
        try XCTUnwrap(
            SidebarActionCatalog.evaluation(for: id, snapshot: snapshot)?.intent)
    }

    private func assertRejected(
        expectedMessage: String? = nil,
        context: String? = nil,
        _ operation: () throws -> SidebarActionDispatchResult,
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        XCTAssertThrowsError(try operation(), file: file, line: line) { error in
            guard case CommandError.ActionFailed(message: let actualMessage) = error else {
                XCTFail(
                    "\(context.map { "\($0): " } ?? "")expected CommandError.ActionFailed, got \(error)",
                    file: file,
                    line: line)
                return
            }
            if let expectedMessage {
                XCTAssertEqual(
                    actualMessage,
                    expectedMessage,
                    context ?? "unexpected rejection message",
                    file: file,
                    line: line)
            }
        }
    }

    func test00SingleOpenUsesCurrentTabFromFrozenIntentAfterLiveSelectionChanges() throws {
        let state = try openVault(named: "open-single", files: ["A.md", "B.md"])
        let frozen = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        let replacement = try snapshot(on: state, [item("B.md")], focusedPath: "B.md")
        let frozenIntent = try intent(SlateCommandID.sidebarOpen, snapshot: frozen)
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(replacement))

        var preflightBatches: [SidebarOpenSelectionBatch] = []
        var opens: [(path: String, target: AppState.OpenTarget)] = []
        state.sidebarActionDispatchOverrides.openPreflight = {
            preflightBatches.append($0)
            return true
        }
        state.sidebarActionDispatchOverrides.openPath = { path, target in
            opens.append((path, target))
            return true
        }

        XCTAssertEqual(
            try state.dispatchSidebarAction(frozenIntent),
            .opened(["A.md"]))
        XCTAssertEqual(preflightBatches.map(\.paths), [["A.md"]])
        XCTAssertEqual(opens.map(\.path), ["A.md"])
        XCTAssertEqual(
            opens.map(\.target), [.currentTab],
            "a one-file catalog Open is the ordinary current-tab action")
    }

    func test01OpenUnderTenPreflightsThenOpensFocusedLast() throws {
        let names = ["A.md", "B.md", "C.md"]
        let state = try openVault(named: "open-direct", files: names)
        let selection = try snapshot(
            on: state, names.map { item($0) }, focusedPath: "B.md")
        var events: [String] = []
        state.sidebarActionDispatchOverrides.openPreflight = { batch in
            events.append("preflight:\(batch.paths.joined(separator: ",")):\(batch.focusedPath ?? "nil")")
            return true
        }
        state.sidebarActionDispatchOverrides.openPath = { path, target in
            events.append("open:\(path):\(target)")
            return true
        }

        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.sidebarOpen, snapshot: selection)),
            .opened(["A.md", "C.md", "B.md"]))
        XCTAssertEqual(
            events,
            [
                "preflight:A.md,B.md,C.md:B.md",
                "open:A.md:newTab", "open:C.md:newTab", "open:B.md:newTab",
            ],
            "multi-file catalog Open keeps one new-tab target per frozen path")
    }

    func test02TenFileOpenSharesAlertUUIDDoesNotPreopenAndConfirmsFrozenBatch() async throws {
        let names = (1...10).map { "Note-\($0).md" }
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "open-confirm", files: names + ["Other.md"], announcer: announcer)
        let selection = try snapshot(
            on: state, names.map { item($0) }, focusedPath: names[2])
        let openIntent = try intent(SlateCommandID.sidebarOpen, snapshot: selection)
        var preflights = 0
        var opened: [String] = []
        state.sidebarActionDispatchOverrides.openPreflight = { _ in
            preflights += 1
            return true
        }
        state.sidebarActionDispatchOverrides.openPath = { path, target in
            XCTAssertEqual(target, .newTab)
            opened.append(path)
            return true
        }

        let announcementBaseline = announcer.posts.count
        let result = try state.dispatchSidebarAction(openIntent)
        guard case .openConfirmation(let request) = result else {
            return XCTFail("10+ Open must stage the shared alert")
        }
        guard case .open(let active)? = state.activeBatchAlertPresentation else {
            return XCTFail("AppState must own the same alert request")
        }
        XCTAssertEqual(preflights, 1)
        XCTAssertEqual(opened, [], "staging performs no pre-open")
        XCTAssertEqual(
            announcer.posts.count, announcementBaseline,
            "synchronous alert staging never owns an accessibility announcement")
        XCTAssertEqual(active.id, request.id)
        XCTAssertEqual(active, request)
        XCTAssertEqual(request.intent, openIntent)
        XCTAssertNil(
            state.deferredBatchAlertPresentation,
            "the first Open confirmation owns the active slot without duplicating itself")

        let replacement = try snapshot(
            on: state, [item("Other.md")], focusedPath: "Other.md")
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(replacement))
        let executionPaths = names.filter { $0 != names[2] } + [names[2]]
        XCTAssertEqual(
            state.confirmOpenSelection(id: request.id),
            .opened(executionPaths),
            "confirmation consumes and opens the frozen batch, not the live replacement selection")
        XCTAssertEqual(preflights, 2, "confirmation rechecks current Open admission once")
        XCTAssertEqual(opened, executionPaths)
        XCTAssertEqual(announcer.posts.count, announcementBaseline)
        XCTAssertNil(state.deferredBatchAlertPresentation)

        for drift in ConfirmedOpenDrift.allCases {
            try await assertConfirmedOpenRejectsPostStagingDrift(drift)
        }
    }

    private enum ConfirmedOpenDrift: String, CaseIterable {
        case missing
        case typeChanged
        case liveSymlink
        case danglingSymlink
        case intermediateSymlink
        case propertyEditNavigation
    }

    private func assertConfirmedOpenRejectsPostStagingDrift(
        _ drift: ConfirmedOpenDrift
    ) async throws {
        let names = (1...10).map { "Folder/Note-\($0).md" }
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "confirm-drift-\(drift.rawValue)",
            files: names + ["Other.md"],
            folders: ["Folder"],
            announcer: announcer)
        let vault = try XCTUnwrap(state.currentVaultURL)
        let selection = try snapshot(
            on: state, names.map { item($0) }, focusedPath: names[2],
            creationParent: "Folder")
        let openIntent = try intent(SlateCommandID.sidebarOpen, snapshot: selection)
        var preflightCalls = 0
        if drift != .propertyEditNavigation {
            state.sidebarActionDispatchOverrides.openPreflight = { _ in
                preflightCalls += 1
                return true
            }
        }
        var opened: [String] = []
        state.sidebarActionDispatchOverrides.openPath = { path, target in
            XCTAssertEqual(target, .newTab)
            opened.append(path)
            return true
        }

        guard case .openConfirmation(let request) =
            try state.dispatchSidebarAction(openIntent)
        else {
            return XCTFail("\(drift.rawValue): staging must succeed before drift")
        }
        XCTAssertEqual(request.intent, openIntent)
        let announcementBaseline = announcer.posts.count
        let preflightBaseline = preflightCalls

        let replacement = try snapshot(
            on: state, [item("Other.md")], focusedPath: "Other.md")
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(replacement))

        var propertyTask: Task<Void, Never>?
        let expectedReason: String
        switch drift {
        case .missing:
            try FileManager.default.removeItem(
                at: vault.appendingPathComponent(names.last!))
            expectedReason = AppState.sidebarSelectionChangedReason
        case .typeChanged:
            let url = vault.appendingPathComponent(names.last!)
            try FileManager.default.removeItem(at: url)
            try FileManager.default.createDirectory(at: url, withIntermediateDirectories: false)
            expectedReason = AppState.sidebarSelectionChangedReason
        case .liveSymlink:
            let url = vault.appendingPathComponent(names.last!)
            try FileManager.default.removeItem(at: url)
            try FileManager.default.createSymbolicLink(
                at: url,
                withDestinationURL: vault.appendingPathComponent(names[0]))
            expectedReason = AppState.sidebarSelectionChangedReason
        case .danglingSymlink:
            let url = vault.appendingPathComponent(names.last!)
            try FileManager.default.removeItem(at: url)
            try FileManager.default.createSymbolicLink(
                at: url,
                withDestinationURL: vault.appendingPathComponent("MissingTarget.md"))
            expectedReason = AppState.sidebarSelectionChangedReason
        case .intermediateSymlink:
            let folder = vault.appendingPathComponent("Folder")
            let backing = vault.appendingPathComponent("Backing")
            try FileManager.default.moveItem(at: folder, to: backing)
            try FileManager.default.createSymbolicLink(
                at: folder, withDestinationURL: backing)
            expectedReason = AppState.sidebarSelectionChangedReason
        case .propertyEditNavigation:
            propertyTask = state.setProperty(
                path: names[0], key: "status", value: .text(value: "busy"))
            XCTAssertNotNil(propertyTask)
            XCTAssertEqual(
                state.propertyEditNavigationDisabledReason,
                AppState.propertyEditInProgressReason)
            expectedReason = AppState.propertyEditInProgressReason
        }

        let outcome = state.confirmOpenSelection(id: request.id)
        guard case .rejected(let message) = outcome else {
            propertyTask?.cancel()
            await propertyTask?.value
            return XCTFail("\(drift.rawValue): expected typed rejection, got \(outcome)")
        }
        XCTAssertEqual(message, expectedReason)
        XCTAssertEqual(opened, [], "\(drift.rawValue): no path opens after staging drift")
        XCTAssertEqual(
            preflightCalls, preflightBaseline,
            "\(drift.rawValue): item revalidation finishes before Open admission")
        XCTAssertEqual(announcer.posts.count, announcementBaseline + 1)
        XCTAssertEqual(announcer.posts.last?.message, expectedReason)
        XCTAssertEqual(announcer.posts.last?.priority, .medium)
        XCTAssertNil(state.activeBatchAlertPresentation)
        XCTAssertNil(state.deferredBatchAlertPresentation)

        propertyTask?.cancel()
        await propertyTask?.value
    }

    func test03CreationUsesCanonicalCapturedParentMatrixNotLegacyMirror() throws {
        let state = try openVault(
            named: "creation", files: ["Root.md", "Folder/Nested.md"],
            folders: ["Folder", "Elsewhere"])
        let cases: [(SidebarSelectionSnapshot, String)] = [
            (try snapshot(on: state, []), ""),
            (try snapshot(on: state, [item("Root.md")], focusedPath: "Root.md"), ""),
            (try snapshot(
                on: state, [item("Folder/Nested.md")], focusedPath: "Folder/Nested.md",
                creationParent: "Folder"), "Folder"),
            (try snapshot(
                on: state, [item("Folder", directory: true)], focusedPath: "Folder",
                creationParent: "Folder"), "Folder"),
        ]
        var noteParents: [String] = []
        var folderParents: [String] = []
        state.sidebarActionDispatchOverrides.createNote = {
            noteParents.append($0)
            return true
        }
        state.sidebarActionDispatchOverrides.createFolder = {
            folderParents.append($0)
            return true
        }
        for (selection, _) in cases {
            state.treeSelectedNode = .init(path: "Elsewhere", isDirectory: true)
            XCTAssertEqual(
                try state.dispatchSidebarAction(intent(SlateCommandID.newNote, snapshot: selection)),
                .completed(actionID: SlateCommandID.newNote))
            state.treeSelectedNode = .init(path: "Elsewhere", isDirectory: true)
            XCTAssertEqual(
                try state.dispatchSidebarAction(intent(SlateCommandID.newFolder, snapshot: selection)),
                .completed(actionID: SlateCommandID.newFolder))
        }
        XCTAssertEqual(noteParents, cases.map(\.1))
        XCTAssertEqual(folderParents, cases.map(\.1))

        let forged = try snapshot(
            on: state, [item("Folder/Nested.md")], focusedPath: "Folder/Nested.md",
            creationParent: "Elsewhere")
        let acceptedParentCount = noteParents.count + folderParents.count
        assertRejected(context: "forged New Note parent") {
            try state.dispatchSidebarAction(intent(SlateCommandID.newNote, snapshot: forged))
        }
        assertRejected(context: "forged New Folder parent") {
            try state.dispatchSidebarAction(intent(SlateCommandID.newFolder, snapshot: forged))
        }
        XCTAssertEqual(
            noteParents.count + folderParents.count, acceptedParentCount,
            "neither creation funnel sees a forged non-canonical parent")
    }

    func test04TemplateAcceptsOnlyCanonicalRootAndIDOverloadCapturesRoot() throws {
        let state = try openVault(named: "template", folders: ["Folder"])
        var parents: [String] = []
        state.sidebarActionDispatchOverrides.openTemplatePicker = {
            parents.append($0)
            return true
        }
        let rootIntent = try snapshot(on: state, [])
        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.newFromTemplate, snapshot: rootIntent)),
            .completed(actionID: SlateCommandID.newFromTemplate))

        let nonRoot = try snapshot(
            on: state, [item("Folder", directory: true)], focusedPath: "Folder",
            creationParent: "Folder")
        assertRejected {
            try state.dispatchSidebarAction(intent(SlateCommandID.newFromTemplate, snapshot: nonRoot))
        }
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(nonRoot))
        XCTAssertEqual(
            try state.dispatchSidebarAction(id: SlateCommandID.newFromTemplate),
            .completed(actionID: SlateCommandID.newFromTemplate))
        XCTAssertEqual(parents, ["", ""], "direct canonical and ID overload both target root")
    }

    func test05RenameUsesExactCapturedPathKindAndRejectsFalseAdmission() throws {
        let state = try openVault(named: "rename", files: ["A.md"], folders: ["Other"])
        let selection = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        state.treeSelectedNode = .init(path: "Other", isDirectory: true)
        XCTAssertEqual(
            try state.dispatchSidebarAction(intent(SlateCommandID.renameEntry, snapshot: selection)),
            .completed(actionID: SlateCommandID.renameEntry))
        XCTAssertEqual(state.renamingNode?.path, "A.md")
        XCTAssertEqual(state.renamingNode?.isDirectory, false)
        if let pending = state.renamingNode { XCTAssertTrue(state.cancelPendingRename(id: pending.id)) }

        var captured: AppState.TreeSelection?
        state.sidebarActionDispatchOverrides.rename = {
            captured = $0
            return false
        }
        assertRejected {
            try state.dispatchSidebarAction(intent(SlateCommandID.renameEntry, snapshot: selection))
        }
        XCTAssertEqual(captured, .init(path: "A.md", isDirectory: false))
        XCTAssertNil(state.renamingNode)
    }

    func test06MoveUsesExactSingleOrOrderedBatchFocusAndRejectsFalseAdmission() throws {
        let state = try openVault(
            named: "move", files: ["A.md", "B.md"], folders: ["Folder"])
        let single = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        XCTAssertEqual(
            try state.dispatchSidebarAction(intent(SlateCommandID.moveTo, snapshot: single)),
            .completed(actionID: SlateCommandID.moveTo))
        let pendingSingle = try XCTUnwrap(state.pendingMove)
        XCTAssertEqual(pendingSingle.path, "A.md")
        XCTAssertFalse(pendingSingle.isDirectory)
        XCTAssertTrue(state.cancelPendingMove(id: pendingSingle.id))

        let batch = try snapshot(
            on: state, [item("B.md"), item("Folder", directory: true), item("A.md")],
            focusedPath: "Folder")
        XCTAssertEqual(
            try state.dispatchSidebarAction(intent(SlateCommandID.moveTo, snapshot: batch)),
            .completed(actionID: SlateCommandID.moveTo))
        XCTAssertEqual(state.pendingBatchMove?.items.map(\.path), ["B.md", "Folder", "A.md"])
        XCTAssertEqual(state.pendingBatchMove?.preferredFocusPath, "Folder")
        if let pending = state.pendingBatchMove { XCTAssertTrue(state.cancelPendingBatchMove(id: pending.id)) }

        state.sidebarActionDispatchOverrides.moveSingle = { _ in false }
        assertRejected {
            try state.dispatchSidebarAction(intent(SlateCommandID.moveTo, snapshot: single))
        }
        state.sidebarActionDispatchOverrides.moveSingle = nil
        state.sidebarActionDispatchOverrides.moveBatch = { _, _ in false }
        assertRejected {
            try state.dispatchSidebarAction(intent(SlateCommandID.moveTo, snapshot: batch))
        }
        XCTAssertNil(state.pendingMove)
        XCTAssertNil(state.pendingBatchMove)
    }

    func test07DuplicateUsesExactFileAndNilAdmissionRejects() throws {
        let state = try openVault(named: "duplicate", files: ["Folder/A.md"], folders: ["Folder"])
        let selection = try snapshot(
            on: state, [item("Folder/A.md")], focusedPath: "Folder/A.md",
            creationParent: "Folder")
        var captured: [String] = []
        state.sidebarActionDispatchOverrides.duplicate = {
            captured.append($0)
            return true
        }
        XCTAssertEqual(
            try state.dispatchSidebarAction(intent(SlateCommandID.duplicateEntry, snapshot: selection)),
            .completed(actionID: SlateCommandID.duplicateEntry))
        state.sidebarActionDispatchOverrides.duplicate = {
            captured.append($0)
            return false
        }
        assertRejected {
            try state.dispatchSidebarAction(intent(SlateCommandID.duplicateEntry, snapshot: selection))
        }
        XCTAssertEqual(captured, ["Folder/A.md", "Folder/A.md"])
    }

    func test08RevealUsesCapturedPathThroughFallibleAdapter() throws {
        let state = try openVault(named: "reveal", files: ["Folder/A.md"], folders: ["Folder"])
        let vaultURL = try XCTUnwrap(state.currentVaultURL)
        let selection = try snapshot(
            on: state, [item("Folder/A.md")], focusedPath: "Folder/A.md",
            creationParent: "Folder")
        var requests: [(URL, String)] = []
        state.sidebarActionDispatchOverrides.reveal = { vaultURL, path in
            requests.append((vaultURL, path))
            return true
        }
        XCTAssertEqual(
            try state.dispatchSidebarAction(intent(SlateCommandID.revealInFinder, snapshot: selection)),
            .completed(actionID: SlateCommandID.revealInFinder))
        state.sidebarActionDispatchOverrides.reveal = { vaultURL, path in
            requests.append((vaultURL, path))
            return false
        }
        assertRejected {
            try state.dispatchSidebarAction(intent(SlateCommandID.revealInFinder, snapshot: selection))
        }
        XCTAssertEqual(requests.map(\.0), [vaultURL, vaultURL])
        XCTAssertEqual(requests.map(\.1), ["Folder/A.md", "Folder/A.md"])
    }

    func test09CopyPathReturnsPreparedVaultRelativeValueOnly() throws {
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "copy-path", files: ["Folder/A.md"], folders: ["Folder"],
            announcer: announcer)
        let selection = try snapshot(
            on: state, [item("Folder/A.md")], focusedPath: "Folder/A.md",
            creationParent: "Folder")
        let announcementBaseline = announcer.posts.count
        let result = try state.dispatchSidebarAction(
            intent(SlateCommandID.copyPath, snapshot: selection))
        XCTAssertEqual(result, .copyPrepared(.path("Folder/A.md")))
        XCTAssertFalse(String(describing: result).contains(root.path))
        XCTAssertEqual(
            announcer.posts.count, announcementBaseline,
            "prepared copy never announces before Task 3 performs the copy")

        let appState = try appSource("AppState.swift")
        let dispatch = try functionBody(
            named: "dispatchSidebarAction",
            signatureFragment: "SidebarActionInvocationIntent",
            in: appState)
        for forbidden in ["NSPasteboard", "postAccessibilityAnnouncement", "announcer.post"] {
            XCTAssertFalse(
                dispatch.contains(forbidden),
                "the synchronous dispatcher must only prepare copy: \(forbidden)")
        }
    }

    func test10WikilinkRenderIsPureAndInvocationFormatsExactlyOnce() throws {
        let state = try openVault(named: "wikilink", files: ["A.md"])
        let currentSession = try XCTUnwrap(state.currentSession)
        let selection = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        var requests: [(sessionIdentity: ObjectIdentifier, path: String)] = []
        state.sidebarActionDispatchOverrides.wikilink = { session, path in
            requests.append((ObjectIdentifier(session), path))
            return "[[Exact A]]"
        }

        for surface in [
            SidebarActionSurface.menuBar, .commandPalette, .contextMenu,
            .voiceOver, .toolbar, .keyboard,
        ] {
            _ = SidebarActionCatalog.project(surface: surface, snapshot: selection)
        }
        XCTAssertEqual(requests.count, 0, "render projection never invokes the formatter")
        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.sidebarCopyWikilink, snapshot: selection)),
            .copyPrepared(.wikilink("[[Exact A]]")))
        XCTAssertEqual(requests.count, 1, "invocation formats exactly once")
        XCTAssertEqual(requests.first?.sessionIdentity, ObjectIdentifier(currentSession))
        XCTAssertEqual(requests.first?.sessionIdentity, selection.sessionIdentity)
        XCTAssertEqual(requests.first?.path, "A.md")
    }

    func test11WikilinkNilAndThrowRejectAfterExactlyOneCallWithoutPreparedCopy() throws {
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "wikilink-failure", files: ["A.md"], announcer: announcer)
        let selection = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        let sessionIdentity = ObjectIdentifier(try XCTUnwrap(state.currentSession))
        let wikilinkIntent = try intent(
            SlateCommandID.sidebarCopyWikilink, snapshot: selection)
        var requests: [(ObjectIdentifier, String)] = []
        let announcementBaseline = announcer.posts.count
        state.sidebarActionDispatchOverrides.wikilink = { session, path in
            requests.append((ObjectIdentifier(session), path))
            return nil
        }
        assertRejected { try state.dispatchSidebarAction(wikilinkIntent) }
        XCTAssertEqual(requests.count, 1)
        XCTAssertEqual(requests.first?.0, sessionIdentity)
        XCTAssertEqual(requests.first?.1, "A.md")
        XCTAssertEqual(announcer.posts.count, announcementBaseline)

        requests = []
        state.sidebarActionDispatchOverrides.wikilink = { session, path in
            requests.append((ObjectIdentifier(session), path))
            throw ProbeError.formatterFailed
        }
        assertRejected { try state.dispatchSidebarAction(wikilinkIntent) }
        XCTAssertEqual(requests.count, 1)
        XCTAssertEqual(requests.first?.0, sessionIdentity)
        XCTAssertEqual(requests.first?.1, "A.md")
        XCTAssertEqual(
            announcer.posts.count, announcementBaseline,
            "synchronous formatter rejection is returned to its caller without announcing")
    }

    func test12TrashUsesExactSingleFileFolderOrOrderedBatchAndRejectsFalse() throws {
        let state = try openVault(
            named: "trash", files: ["A.md", "Folder/Child.md"], folders: ["Folder"])
        let file = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        let folder = try snapshot(
            on: state, [item("Folder", directory: true)], focusedPath: "Folder",
            creationParent: "Folder")
        let batch = try snapshot(
            on: state, [item("Folder", directory: true), item("A.md")],
            focusedPath: "A.md")
        let vault = try XCTUnwrap(state.currentVaultURL)
        var singles: [AppState.TreeSelection] = []
        var batches: [([AppState.TreeSelection], String?)] = []
        state.sidebarActionDispatchOverrides.trashSingle = {
            singles.append($0)
            return true
        }
        state.sidebarActionDispatchOverrides.trashBatch = { items, focus in
            batches.append((items, focus))
            return true
        }
        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.deleteEntry, snapshot: file)),
            .completed(actionID: SlateCommandID.deleteEntry))
        XCTAssertEqual(
            singles, [.init(path: "A.md", isDirectory: false)],
            "the file case remains observable through the narrow leaf seam")

        state.sidebarActionDispatchOverrides.trashSingle = nil
        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.deleteEntry, snapshot: folder)),
            .completed(actionID: SlateCommandID.deleteEntry))
        let pendingFolder = try XCTUnwrap(state.pendingFolderDelete)
        XCTAssertEqual(pendingFolder.path, "Folder")
        XCTAssertEqual(pendingFolder.itemCount, 1)
        XCTAssertEqual(state.pendingFolderDelete?.id, pendingFolder.id)
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Folder/Child.md").path),
            "default non-empty-folder admission only stages the owned confirmation")
        XCTAssertFalse(state.cancelPendingFolderDelete(id: UUID()))
        XCTAssertEqual(state.pendingFolderDelete?.id, pendingFolder.id)
        XCTAssertTrue(state.cancelPendingFolderDelete(id: pendingFolder.id))
        XCTAssertNil(state.pendingFolderDelete)
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Folder/Child.md").path),
            "cancelling the exact staged UUID preserves the subtree")

        XCTAssertEqual(
            try state.dispatchSidebarAction(intent(SlateCommandID.deleteEntry, snapshot: batch)),
            .completed(actionID: SlateCommandID.deleteEntry))
        XCTAssertEqual(batches.first?.0.map(\.path), ["Folder", "A.md"])
        XCTAssertEqual(batches.first?.1, "A.md")

        state.sidebarActionDispatchOverrides.trashSingle = { _ in false }
        assertRejected {
            try state.dispatchSidebarAction(intent(SlateCommandID.deleteEntry, snapshot: file))
        }
        state.sidebarActionDispatchOverrides.trashBatch = { _, _ in false }
        assertRejected {
            try state.dispatchSidebarAction(intent(SlateCommandID.deleteEntry, snapshot: batch))
        }
    }

    func test13AllItemPreflightRejectsLateInvalidOrStaleBeforeAnyEffect() throws {
        let victims = ["Missing.md", "Type.md", "LiveLink.md", "DeadLink.md", "Stale.md"]
        let state = try openVault(named: "preflight", files: ["A.md"] + victims)
        let vault = try XCTUnwrap(state.currentVaultURL)
        let originalA = try String(
            contentsOf: vault.appendingPathComponent("A.md"), encoding: .utf8)
        var effects = 0
        state.sidebarActionDispatchOverrides.openPreflight = { _ in effects += 1; return true }
        state.sidebarActionDispatchOverrides.openPath = { _, _ in effects += 1; return true }
        state.sidebarActionDispatchOverrides.moveBatch = { _, _ in effects += 1; return true }
        state.sidebarActionDispatchOverrides.trashBatch = { _, _ in effects += 1; return true }

        func capturedIntents(_ victim: String) throws -> [SidebarActionInvocationIntent] {
            let selection = try snapshot(
                on: state, [item("A.md"), item(victim)], focusedPath: victim)
            return try [
                intent(SlateCommandID.sidebarOpen, snapshot: selection),
                intent(SlateCommandID.moveTo, snapshot: selection),
                intent(SlateCommandID.deleteEntry, snapshot: selection),
            ]
        }
        func reject(_ intents: [SidebarActionInvocationIntent], _ label: String) {
            for captured in intents {
                assertRejected(context: "\(label): \(captured.actionID)") {
                    try state.dispatchSidebarAction(captured)
                }
            }
        }

        let missing = try capturedIntents("Missing.md")
        try FileManager.default.removeItem(at: vault.appendingPathComponent("Missing.md"))
        reject(missing, "later missing")

        let typeChanged = try capturedIntents("Type.md")
        let typeURL = vault.appendingPathComponent("Type.md")
        try FileManager.default.removeItem(at: typeURL)
        try FileManager.default.createDirectory(at: typeURL, withIntermediateDirectories: false)
        reject(typeChanged, "later type change")

        let liveLink = try capturedIntents("LiveLink.md")
        let liveURL = vault.appendingPathComponent("LiveLink.md")
        try FileManager.default.removeItem(at: liveURL)
        try FileManager.default.createSymbolicLink(
            at: liveURL, withDestinationURL: vault.appendingPathComponent("A.md"))
        reject(liveLink, "live symlink")

        let deadLink = try capturedIntents("DeadLink.md")
        let deadURL = vault.appendingPathComponent("DeadLink.md")
        let deadTarget = vault.appendingPathComponent("DeadTarget.md")
        try "# target".write(to: deadTarget, atomically: true, encoding: .utf8)
        try FileManager.default.removeItem(at: deadURL)
        try FileManager.default.createSymbolicLink(at: deadURL, withDestinationURL: deadTarget)
        try FileManager.default.removeItem(at: deadTarget)
        reject(deadLink, "dangling symlink")

        let stale = try capturedIntents("Stale.md")
        let replacementVault = root.appendingPathComponent("replacement-vault")
        try FileManager.default.createDirectory(
            at: replacementVault, withIntermediateDirectories: true)
        state.openVault(at: replacementVault)
        reject(stale, "stale session")
        XCTAssertEqual(effects, 0, "validation completes for every item before any funnel effect")
        XCTAssertNil(state.pendingMove)
        XCTAssertNil(state.pendingBatchMove)
        XCTAssertNil(state.pendingFolderDelete)
        XCTAssertNil(state.pendingBatchDelete)
        XCTAssertEqual(
            try String(contentsOf: vault.appendingPathComponent("A.md"), encoding: .utf8),
            originalA,
            "a rejected late item leaves earlier valid captures untouched")
    }

    func test14StructuralAndActionSpecificReasonsRejectBeforeStaging() throws {
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "reasons", files: ["A.md"], announcer: announcer)
        let selection = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        let renameIntent = try intent(SlateCommandID.renameEntry, snapshot: selection)
        var funnelCalls = 0
        state.sidebarActionDispatchOverrides.rename = { _ in funnelCalls += 1; return true }

        let busy = "Wait for the current file operation to finish."
        state.sidebarActionStructuralDisabledReasonOverride = busy
        let announcementBaseline = announcer.posts.count
        assertRejected(expectedMessage: busy) {
            try state.dispatchSidebarAction(renameIntent)
        }
        XCTAssertEqual(funnelCalls, 0)
        XCTAssertNil(state.renamingNode)

        state.sidebarActionStructuralDisabledReasonOverride = nil
        let loading = "Sidebar actions are still loading."
        state.sidebarActionAvailabilityReasonProvider = {
            $0 == SlateCommandID.renameEntry ? loading : nil
        }
        assertRejected(expectedMessage: loading) {
            try state.dispatchSidebarAction(renameIntent)
        }
        XCTAssertEqual(funnelCalls, 0)
        XCTAssertNil(state.renamingNode)
        XCTAssertEqual(
            announcer.posts.count, announcementBaseline,
            "synchronous disabled-state rejection does not self-announce")
    }

    func test15EveryFallibleFunnelRejectionNeverReturnsCompleted() throws {
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "fallible", files: ["A.md", "B.md"], folders: ["Folder"],
            announcer: announcer)
        let file = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        let batch = try snapshot(
            on: state, [item("A.md"), item("B.md")], focusedPath: "B.md")
        let empty = try snapshot(on: state, [])
        let cases: [(String, String, SidebarSelectionSnapshot, () -> Void)] = [
            ("open", SlateCommandID.sidebarOpen, file, {
                state.sidebarActionDispatchOverrides.openPreflight = { _ in false }
            }),
            ("new note", SlateCommandID.newNote, empty, {
                state.sidebarActionDispatchOverrides.createNote = { _ in false }
            }),
            ("new folder", SlateCommandID.newFolder, empty, {
                state.sidebarActionDispatchOverrides.createFolder = { _ in false }
            }),
            ("template", SlateCommandID.newFromTemplate, empty, {
                state.sidebarActionDispatchOverrides.openTemplatePicker = { _ in false }
            }),
            ("rename", SlateCommandID.renameEntry, file, {
                state.sidebarActionDispatchOverrides.rename = { _ in false }
            }),
            ("single move", SlateCommandID.moveTo, file, {
                state.sidebarActionDispatchOverrides.moveSingle = { _ in false }
            }),
            ("batch move", SlateCommandID.moveTo, batch, {
                state.sidebarActionDispatchOverrides.moveBatch = { _, _ in false }
            }),
            ("duplicate", SlateCommandID.duplicateEntry, file, {
                state.sidebarActionDispatchOverrides.duplicate = { _ in false }
            }),
            ("reveal", SlateCommandID.revealInFinder, file, {
                state.sidebarActionDispatchOverrides.reveal = { _, _ in false }
            }),
            ("wikilink", SlateCommandID.sidebarCopyWikilink, file, {
                state.sidebarActionDispatchOverrides.wikilink = { _, _ in nil }
            }),
            ("single trash", SlateCommandID.deleteEntry, file, {
                state.sidebarActionDispatchOverrides.trashSingle = { _ in false }
            }),
            ("batch trash", SlateCommandID.deleteEntry, batch, {
                state.sidebarActionDispatchOverrides.trashBatch = { _, _ in false }
            }),
        ]
        let announcementBaseline = announcer.posts.count
        for (name, id, selection, configure) in cases {
            state.sidebarActionDispatchOverrides = .init()
            configure()
            assertRejected(context: name) {
                try state.dispatchSidebarAction(intent(id, snapshot: selection))
            }
        }
        XCTAssertEqual(
            announcer.posts.count, announcementBaseline,
            "leaf admission failures stay synchronous and silent")
    }

    func test16IDOverloadCapturesSnapshotOnceAcrossAvailabilityReentrancy() throws {
        let state = try openVault(named: "id-freeze", files: ["A.md", "B.md"])
        let a = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        let b = try snapshot(on: state, [item("B.md")], focusedPath: "B.md")
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(a))
        var captured: [AppState.TreeSelection] = []
        state.sidebarActionDispatchOverrides.rename = { captured.append($0); return true }
        state.sidebarActionAvailabilityReasonProvider = { id in
            if id == SlateCommandID.renameEntry {
                _ = state.publishSidebarSelectionSnapshot(b)
            }
            return nil
        }

        XCTAssertEqual(
            try state.dispatchSidebarAction(id: SlateCommandID.renameEntry),
            .completed(actionID: SlateCommandID.renameEntry))
        XCTAssertEqual(captured, [.init(path: "A.md", isDirectory: false)])
        XCTAssertEqual(state.sidebarSelectionSnapshot, b)

        state.sidebarActionAvailabilityReasonProvider = { _ in nil }
        XCTAssertEqual(
            try state.dispatchSidebarAction(id: SlateCommandID.renameEntry),
            .completed(actionID: SlateCommandID.renameEntry))
        XCTAssertEqual(
            captured,
            [
                .init(path: "A.md", isDirectory: false),
                .init(path: "B.md", isDirectory: false),
            ],
            "after the reentrant callback is reset, a second invocation captures B")
    }

    func test17CatalogIsNoIOOpenTypesAreSharedAndDefaultsUseRealFunnels() throws {
        let catalog = try appSource("Sidebar/SidebarActionCatalog.swift")
        for forbidden in [
            "FileManager", "VaultSession", "wikilinkForPath", "NSWorkspace",
            "NSPasteboard", "Data(contentsOf", "String(contentsOf", "resourceValues",
            "getResourceValue", "checkResourceIsReachable", "fileExists",
            "attributesOfItem", "contentsOfDirectory",
        ] {
            XCTAssertFalse(catalog.contains(forbidden), "render-time I/O token: \(forbidden)")
        }

        let tree = try appSource("FileTreeSidebar.swift")
        let appState = try appSource("AppState.swift")
        XCTAssertFalse(tree.contains("struct OpenSelectionBatch"))
        XCTAssertFalse(tree.contains("struct OpenSelectionRequest"))
        XCTAssertFalse(tree.contains("enum OpenSelectionDisposition"))
        XCTAssertTrue(tree.contains("typealias OpenSelectionBatch = SidebarOpenSelectionBatch"))
        XCTAssertTrue(tree.contains("typealias OpenSelectionRequest = SidebarOpenSelectionRequest"))
        XCTAssertTrue(tree.contains("typealias OpenSelectionDisposition = SidebarOpenSelectionDisposition"))
        XCTAssertTrue(tree.contains("_ = appState.confirmOpenSelection(id: request.id)"))
        XCTAssertFalse(
            tree.contains("openCapturedPaths(appState.confirmOpenSelection"),
            "the alert must not receive raw paths or run a second Open executor")
        XCTAssertFalse(appState.contains("FileTreeSidebar.OpenSelection"))

        let dispatch = try functionBody(
            named: "dispatchSidebarAction",
            signatureFragment: "SidebarActionInvocationIntent",
            in: appState)
        for forbidden in [
            "postAccessibilityAnnouncement", "announcer.post",
            "activeBatchAlertPresentation =", "deferredBatchAlertPresentation =",
        ] {
            XCTAssertFalse(
                dispatch.contains(forbidden),
                "dispatcher bypasses shared presentation/announcement ownership: \(forbidden)")
        }
        for realFunnel in [
            "openFile", "enqueueOpenSelection", "activateFileViewerSelecting",
            "wikilinkForPath",
            "requestCreateNote", "requestCreateFolder", "openTemplatePicker",
            "requestRename", "requestPendingMove", "requestBatchMove",
            "requestDuplicateEntry", "requestDeleteEntry", "requestBatchDelete",
        ] {
            XCTAssertTrue(
                dispatch.contains(realFunnel),
                "default dispatcher path must call the real \(realFunnel) funnel")
        }

        XCTAssertEqual(
            appState.components(separatedBy: "SidebarActionInvocationIntent").count - 1,
            1,
            "the intent overload is the one executor; no parallel intent runner is allowed")
        let overrides = try typeBody(named: "SidebarActionDispatchOverrides", in: appState)
        for leafSeam in [
            "openPreflight", "openPath", "createNote", "createFolder",
            "openTemplatePicker", "rename", "moveSingle", "moveBatch",
            "duplicate", "reveal", "wikilink", "trashSingle", "trashBatch",
        ] {
            let declaration = try storedPropertyDeclaration(named: leafSeam, in: overrides)
            XCTAssertTrue(
                declaration.contains("?") || declaration.contains("Optional<"),
                "\(leafSeam) must remain an optional leaf seam over the real default")
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

    private enum SourceExtractionError: Error {
        case missing(String)
        case unbalanced(String)
    }

    private func functionBody(
        named name: String,
        signatureFragment: String,
        in source: String
    ) throws -> Substring {
        let functionPrefix = "func \(name)("
        var searchStart = source.startIndex
        while let start = source.range(
            of: functionPrefix,
            range: searchStart..<source.endIndex)
        {
            let tail = source[start.lowerBound...]
            guard let openBrace = tail.firstIndex(of: "{") else {
                throw SourceExtractionError.unbalanced(name)
            }
            if tail[..<openBrace].contains(signatureFragment) {
                return try bracedBody(
                    startingAt: openBrace,
                    in: tail,
                    description: "\(name)(\(signatureFragment))")
            }
            searchStart = start.upperBound
        }
        throw SourceExtractionError.missing("\(name)(\(signatureFragment))")
    }

    private func typeBody(named name: String, in source: String) throws -> Substring {
        guard let start = source.range(of: "struct \(name)") else {
            throw SourceExtractionError.missing("struct \(name)")
        }
        let tail = source[start.lowerBound...]
        guard let openBrace = tail.firstIndex(of: "{") else {
            throw SourceExtractionError.unbalanced("struct \(name)")
        }
        return try bracedBody(
            startingAt: openBrace, in: tail, description: "struct \(name)")
    }

    private func bracedBody(
        startingAt openBrace: String.Index,
        in source: Substring,
        description: String
    ) throws -> Substring {
        var depth = 0
        for index in source.indices[openBrace...] {
            switch source[index] {
            case "{": depth += 1
            case "}":
                depth -= 1
                if depth == 0 { return source[...index] }
            default: break
            }
        }
        throw SourceExtractionError.unbalanced(description)
    }

    private func storedPropertyDeclaration(
        named name: String,
        in typeBody: Substring
    ) throws -> String {
        let lines = typeBody.split(separator: "\n", omittingEmptySubsequences: false)
        guard let start = lines.firstIndex(where: { $0.contains("var \(name):") }) else {
            throw SourceExtractionError.missing("stored property \(name)")
        }
        var declaration = ""
        for line in lines[start...] {
            let trimmed = line.trimmingCharacters(in: .whitespaces)
            if !declaration.isEmpty,
                trimmed.hasPrefix("var ") || trimmed.hasPrefix("let ")
                    || trimmed.hasPrefix("func ") || trimmed.hasPrefix("init(")
            {
                break
            }
            declaration.append(contentsOf: line)
            declaration.append("\n")
        }
        return declaration
    }
}
