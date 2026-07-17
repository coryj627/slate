// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

@MainActor
final class SidebarCopyActionTests: XCTestCase {
    private enum FormatterFailure: LocalizedError {
        case unavailable

        var errorDescription: String? { "Injected formatter failure." }
    }

    private final class RecordingAnnouncer: AnnouncementPosting, @unchecked Sendable {
        private(set) var posts: [(message: String, priority: AnnouncementPriority)] = []

        func post(_ message: String, priority: AnnouncementPriority) {
            posts.append((message, priority))
        }
    }

    private final class RecordingPasteboard: SidebarPasteboardWriting {
        var setStringSucceeds = true
        private(set) var clearCount = 0
        private(set) var setValues: [String] = []
        private(set) var value: String?

        init(value: String? = nil) {
            self.value = value
        }

        func clearContents() {
            clearCount += 1
            value = nil
        }

        func setString(_ value: String) -> Bool {
            setValues.append(value)
            guard setStringSucceeds else { return false }
            self.value = value
            return true
        }
    }

    /// Parks the detached formatter without capturing mutable test locals in
    /// its `@Sendable` closure. `NSCondition` owns both the entered signal and
    /// release state, so the supersession race is deterministic and warning-free.
    private final class BlockingWikilinkFormatter: @unchecked Sendable {
        private let condition = NSCondition()
        private var didEnter = false
        private var isReleased = false
        private var callCountStorage = 0
        private let result: String?

        init(result: String?) {
            self.result = result
        }

        func format() -> String? {
            condition.lock()
            didEnter = true
            callCountStorage += 1
            condition.broadcast()
            while !isReleased {
                condition.wait()
            }
            condition.unlock()
            return result
        }

        func waitUntilEntered() async -> Bool {
            await Task.detached { [self] in
                waitUntilEnteredBlocking()
            }.value
        }

        private func waitUntilEnteredBlocking() -> Bool {
            condition.lock()
            defer { condition.unlock() }
            let deadline = Date().addingTimeInterval(10)
            while !didEnter {
                guard condition.wait(until: deadline) else { return false }
            }
            return true
        }

        func release() {
            condition.lock()
            isReleased = true
            condition.broadcast()
            condition.unlock()
        }

        var callCount: Int {
            condition.lock()
            defer { condition.unlock() }
            return callCountStorage
        }
    }

    private var root: URL!

    override func setUpWithError() throws {
        root = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-sidebar-copy-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: root)
    }

    private func openVault(
        named name: String,
        files: [String],
        pasteboard: RecordingPasteboard,
        announcer: RecordingAnnouncer
    ) throws -> AppState {
        let vault = root.appendingPathComponent(name)
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
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
            announcer: announcer,
            sidebarPasteboard: pasteboard)
        state.openVault(at: vault)
        let session = try XCTUnwrap(state.currentSession)
        _ = try session.scanInitial(cancel: CancelToken())
        return state
    }

    private func item(
        _ path: String,
        markdown: Bool = true
    ) -> SidebarSelectionItem {
        SidebarSelectionItem(
            path: path,
            isDirectory: false,
            isMarkdown: markdown)
    }

    private func snapshot(
        on state: AppState,
        _ item: SidebarSelectionItem
    ) throws -> SidebarSelectionSnapshot {
        SidebarSelectionSnapshot(
            sessionIdentity: ObjectIdentifier(try XCTUnwrap(state.currentSession)),
            items: [item],
            focusedPath: item.path,
            creationParent: AppState.TreeMutation.parentPath(of: item.path) ?? "")
    }

    private func intent(
        _ id: String,
        snapshot: SidebarSelectionSnapshot
    ) throws -> SidebarActionInvocationIntent {
        try XCTUnwrap(
            SidebarActionCatalog.evaluation(for: id, snapshot: snapshot)?.intent)
    }

    private func assertActionFailure(
        _ expected: String,
        _ operation: () throws -> SidebarActionDispatchResult,
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        XCTAssertThrowsError(try operation(), file: file, line: line) { error in
            guard case CommandError.ActionFailed(message: let message) = error else {
                return XCTFail(
                    "expected CommandError.ActionFailed, got \(error)",
                    file: file,
                    line: line)
            }
            XCTAssertEqual(message, expected, file: file, line: line)
        }
    }

    private func copiedPosts(
        after baseline: Int,
        in announcer: RecordingAnnouncer
    ) -> [(message: String, priority: AnnouncementPriority)] {
        Array(announcer.posts.dropFirst(baseline))
    }

    func testCopyPathWritesVaultRelativePathOnceAndAnnouncesOnlyAfterSuccess() throws {
        let pasteboard = RecordingPasteboard(value: "old value")
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "copy-path",
            files: ["Folder/File.md"],
            pasteboard: pasteboard,
            announcer: announcer)
        let selection = try snapshot(on: state, item("Folder/File.md"))
        let baseline = announcer.posts.count

        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.copyPath, snapshot: selection)),
            .completed(actionID: SlateCommandID.copyPath))

        XCTAssertEqual(pasteboard.clearCount, 1)
        XCTAssertEqual(pasteboard.setValues, ["Folder/File.md"])
        XCTAssertEqual(pasteboard.value, "Folder/File.md")
        XCTAssertFalse(pasteboard.setValues[0].contains(root.path))
        let posts = copiedPosts(after: baseline, in: announcer)
        XCTAssertEqual(posts.map(\.message), ["Copied."])
        guard let priority = posts.first?.priority else {
            return XCTFail("successful copy must post one polite announcement")
        }
        guard case .medium = priority else {
            return XCTFail("Copied. must use polite medium priority")
        }
    }

    func testCopyWikilinkUsesLiveFFIForUniqueAndQualifiedTargets() async throws {
        let pasteboard = RecordingPasteboard()
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "copy-wikilink",
            files: [
                "Notes/Unique.md",
                "Notes/Target.md",
                "Other/Target.md",
            ],
            pasteboard: pasteboard,
            announcer: announcer)
        let baseline = announcer.posts.count

        let unique = try snapshot(on: state, item("Notes/Unique.md"))
        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.sidebarCopyWikilink, snapshot: unique)),
            .copyPending(actionID: SlateCommandID.sidebarCopyWikilink))
        let uniqueTask = try XCTUnwrap(
            state.pendingSidebarWikilinkCopyTaskForTesting)
        await uniqueTask.value

        let qualified = try snapshot(on: state, item("Notes/Target.md"))
        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.sidebarCopyWikilink, snapshot: qualified)),
            .copyPending(actionID: SlateCommandID.sidebarCopyWikilink))
        let qualifiedTask = try XCTUnwrap(
            state.pendingSidebarWikilinkCopyTaskForTesting)
        await qualifiedTask.value

        XCTAssertEqual(pasteboard.clearCount, 2)
        XCTAssertEqual(pasteboard.setValues, ["[[Unique]]", "[[Notes/Target]]"])
        XCTAssertEqual(pasteboard.value, "[[Notes/Target]]")
        let posts = copiedPosts(after: baseline, in: announcer)
        XCTAssertEqual(
            posts.map(\.message),
            ["Copied.", "Copied."],
            "each successful user invocation owns exactly one announcement")
        XCTAssertTrue(
            posts.allSatisfy {
                if case .medium = $0.priority { return true }
                return false
            },
            "successful async copies use polite medium-priority announcements")
        XCTAssertNil(state.sidebarActionBackgroundFailure)
    }

    func testMissingStaleAndNonMarkdownTargetsFailBeforePasteboardOrAnnouncement() throws {
        let pasteboard = RecordingPasteboard(value: "unchanged")
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "invalid-targets",
            files: ["Live.md", "Gone.md", "Asset.txt"],
            pasteboard: pasteboard,
            announcer: announcer)
        let baseline = announcer.posts.count

        let missing = try snapshot(on: state, item("Missing.md"))
        assertActionFailure(AppState.sidebarSelectionChangedReason) {
            try state.dispatchSidebarAction(
                intent(SlateCommandID.copyPath, snapshot: missing))
        }

        let stale = try snapshot(on: state, item("Gone.md"))
        let staleIntent = try intent(SlateCommandID.copyPath, snapshot: stale)
        try FileManager.default.removeItem(
            at: try XCTUnwrap(state.currentVaultURL)
                .appendingPathComponent("Gone.md"))
        assertActionFailure(AppState.sidebarSelectionChangedReason) {
            try state.dispatchSidebarAction(staleIntent)
        }

        let nonMarkdown = try snapshot(
            on: state,
            item("Asset.txt", markdown: false))
        let nonMarkdownEvaluation = try XCTUnwrap(
            SidebarActionCatalog.evaluation(
                for: SlateCommandID.sidebarCopyWikilink,
                snapshot: nonMarkdown))
        XCTAssertEqual(
            nonMarkdownEvaluation.disabledReason,
            "Select exactly one Markdown file to copy its wikilink.")
        XCTAssertNil(nonMarkdownEvaluation.intent)
        assertActionFailure("Select exactly one Markdown file to copy its wikilink.") {
            try state.dispatchSidebarAction(
                SidebarActionInvocationIntent(
                    actionID: SlateCommandID.sidebarCopyWikilink,
                    snapshot: nonMarkdown))
        }

        XCTAssertEqual(pasteboard.clearCount, 0)
        XCTAssertEqual(pasteboard.setValues, [])
        XCTAssertEqual(pasteboard.value, "unchanged")
        XCTAssertEqual(copiedPosts(after: baseline, in: announcer).map(\.message), [])
    }

    func testStaleSessionFailsBeforePasteboardOrAnnouncement() throws {
        let pasteboard = RecordingPasteboard(value: "unchanged")
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "stale-session",
            files: ["A.md"],
            pasteboard: pasteboard,
            announcer: announcer)
        let selection = try snapshot(on: state, item("A.md"))
        let frozen = try intent(SlateCommandID.copyPath, snapshot: selection)
        let replacement = root.appendingPathComponent("replacement")
        try FileManager.default.createDirectory(
            at: replacement,
            withIntermediateDirectories: true)
        state.openVault(at: replacement)
        let baseline = announcer.posts.count

        assertActionFailure(AppState.sidebarSelectionStaleReason) {
            try state.dispatchSidebarAction(frozen)
        }
        XCTAssertEqual(pasteboard.clearCount, 0)
        XCTAssertEqual(pasteboard.setValues, [])
        XCTAssertEqual(pasteboard.value, "unchanged")
        XCTAssertEqual(copiedPosts(after: baseline, in: announcer).map(\.message), [])
    }

    func testFormatterNilAndThrowUseOneDeterministicAccessibleFailure() async throws {
        let pasteboard = RecordingPasteboard(value: "unchanged")
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "formatter-failure",
            files: ["Bad#Name.md", "Good.md"],
            pasteboard: pasteboard,
            announcer: announcer)
        let baseline = announcer.posts.count

        let failure = "Could not create a wikilink for this file."
        let refused = try snapshot(on: state, item("Bad#Name.md"))
        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.sidebarCopyWikilink, snapshot: refused)),
            .copyPending(actionID: SlateCommandID.sidebarCopyWikilink))
        let refusedTask = try XCTUnwrap(
            state.pendingSidebarWikilinkCopyTaskForTesting)
        await refusedTask.value
        XCTAssertEqual(state.sidebarActionBackgroundFailure, failure)

        let sessionFailure = try snapshot(on: state, item("Good.md"))
        state.sidebarActionDispatchOverrides.wikilink = { _, _ in
            throw FormatterFailure.unavailable
        }
        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.sidebarCopyWikilink, snapshot: sessionFailure)),
            .copyPending(actionID: SlateCommandID.sidebarCopyWikilink))
        let thrownTask = try XCTUnwrap(
            state.pendingSidebarWikilinkCopyTaskForTesting)
        await thrownTask.value

        XCTAssertEqual(pasteboard.clearCount, 0)
        XCTAssertEqual(pasteboard.setValues, [])
        XCTAssertEqual(pasteboard.value, "unchanged")
        XCTAssertEqual(state.sidebarActionBackgroundFailure, failure)
        let posts = copiedPosts(after: baseline, in: announcer)
        XCTAssertEqual(posts.map(\.message), [failure, failure])
        XCTAssertTrue(
            posts.allSatisfy {
                if case .medium = $0.priority { return true }
                return false
            },
            "each detached formatter failure is announced exactly once")
    }

    func testPasteboardFailureIsFailClosedForBothCopyActions() async throws {
        let pasteboard = RecordingPasteboard(value: "old value")
        pasteboard.setStringSucceeds = false
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "pasteboard-failure",
            files: ["A.md"],
            pasteboard: pasteboard,
            announcer: announcer)
        let selection = try snapshot(on: state, item("A.md"))
        let baseline = announcer.posts.count

        assertActionFailure("Could not copy to the clipboard.") {
            try state.dispatchSidebarAction(
                intent(SlateCommandID.copyPath, snapshot: selection))
        }
        XCTAssertNil(pasteboard.value)

        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.sidebarCopyWikilink, snapshot: selection)),
            .copyPending(actionID: SlateCommandID.sidebarCopyWikilink))
        let wikilinkTask = try XCTUnwrap(
            state.pendingSidebarWikilinkCopyTaskForTesting)
        await wikilinkTask.value

        XCTAssertEqual(pasteboard.clearCount, 2)
        XCTAssertEqual(pasteboard.setValues, ["A.md", "[[A]]"])
        XCTAssertNil(pasteboard.value)
        let failure = "Could not copy to the clipboard."
        XCTAssertEqual(state.sidebarActionBackgroundFailure, failure)
        let posts = copiedPosts(after: baseline, in: announcer)
        XCTAssertEqual(posts.map(\.message), [failure])
        guard let priority = posts.first?.priority else {
            return XCTFail("async pasteboard failure must be announced once")
        }
        guard case .medium = priority else {
            return XCTFail("async pasteboard failure must use medium priority")
        }
    }

    func testCopyPathSupersedesBlockedWikilinkEvenWhenCopyPathPasteboardFails()
        async throws
    {
        let pasteboard = RecordingPasteboard(value: "old value")
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "copy-supersession",
            files: ["A.md"],
            pasteboard: pasteboard,
            announcer: announcer)
        let selection = try snapshot(on: state, item("A.md"))
        let gate = BlockingWikilinkFormatter(result: "[[A]]")
        state.sidebarActionDispatchOverrides.wikilink = { _, _ in
            gate.format()
        }
        let baseline = announcer.posts.count

        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.sidebarCopyWikilink, snapshot: selection)),
            .copyPending(actionID: SlateCommandID.sidebarCopyWikilink))
        let wikilinkTask = try XCTUnwrap(
            state.pendingSidebarWikilinkCopyTaskForTesting)
        defer { gate.release() }
        let formatterEntered = await gate.waitUntilEntered()
        XCTAssertTrue(formatterEntered, "the detached Wikilink formatter must start")
        XCTAssertEqual(gate.callCount, 1)
        XCTAssertEqual(pasteboard.clearCount, 0)
        XCTAssertEqual(pasteboard.setValues, [])

        pasteboard.setStringSucceeds = false
        assertActionFailure("Could not copy to the clipboard.") {
            try state.dispatchSidebarAction(
                intent(SlateCommandID.copyPath, snapshot: selection))
        }
        XCTAssertNil(state.pendingSidebarWikilinkCopyTaskForTesting)
        XCTAssertFalse(state.isCopyingSidebarWikilink)
        let clearCountAfterCopyPath = pasteboard.clearCount
        let valuesAfterCopyPath = pasteboard.setValues
        let postsAfterCopyPath = copiedPosts(after: baseline, in: announcer)

        gate.release()
        await wikilinkTask.value

        XCTAssertEqual(pasteboard.clearCount, clearCountAfterCopyPath)
        XCTAssertEqual(pasteboard.setValues, valuesAfterCopyPath)
        XCTAssertEqual(pasteboard.clearCount, 1)
        XCTAssertEqual(pasteboard.setValues, ["A.md"])
        XCTAssertNil(pasteboard.value)
        XCTAssertEqual(
            copiedPosts(after: baseline, in: announcer).map(\.message),
            postsAfterCopyPath.map(\.message))
        XCTAssertFalse(
            copiedPosts(after: baseline, in: announcer)
                .contains(where: { $0.message == "Copied." }),
            "superseded Wikilink A must not announce success after Copy Path B")
        XCTAssertNil(state.sidebarActionBackgroundFailure)
    }

    func testEveryExposingSurfaceSharesCopyIDsAndFrozenInvocationIdentity() throws {
        let pasteboard = RecordingPasteboard()
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "surface-identity",
            files: ["Folder/A.md"],
            pasteboard: pasteboard,
            announcer: announcer)
        let selection = try snapshot(on: state, item("Folder/A.md"))
        let exposedSurfaces: [SidebarActionSurface] = [
            .contextMenu, .voiceOver, .menuBar, .commandPalette,
        ]

        for id in [SlateCommandID.copyPath, SlateCommandID.sidebarCopyWikilink] {
            let evaluations = try exposedSurfaces.map { surface in
                try XCTUnwrap(
                    SidebarActionCatalog.project(surface: surface, snapshot: selection)
                        .first(where: { $0.id == id }),
                    "\(surface) must expose \(id)")
            }
            XCTAssertTrue(evaluations.allSatisfy { $0.id == id })
            let frozen = try XCTUnwrap(evaluations.first?.intent)
            XCTAssertTrue(
                evaluations.allSatisfy { $0.intent == frozen },
                "all exposing surfaces must dispatch the same action and snapshot")
        }

        for surface in [SidebarActionSurface.toolbar, .keyboard] {
            let ids = Set(
                SidebarActionCatalog.project(surface: surface, snapshot: selection)
                    .map(\.id))
            XCTAssertFalse(ids.contains(SlateCommandID.copyPath))
            XCTAssertFalse(ids.contains(SlateCommandID.sidebarCopyWikilink))
        }
    }

    func testFileRowsDoNotOwnAnAlternatePasteboardFunnel() throws {
        let packageRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let treeSource = try String(
            contentsOf: packageRoot.appendingPathComponent("Sources/SlateMac/FileTreeSidebar.swift"),
            encoding: .utf8)

        XCTAssertFalse(treeSource.contains("NSPasteboard"))
        XCTAssertFalse(treeSource.contains("SidebarPasteboardWriting"))
    }
}
