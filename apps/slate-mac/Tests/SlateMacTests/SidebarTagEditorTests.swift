// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// FL5-3b (#666): the Add Tag… / Remove Tag… flow — catalog dispatch
/// freezing the selection into an editor request, runner-seam commits,
/// ONE consolidated announcement (core summary + skip clause + honest
/// inline remainder), and the selection-tags choice list.
@MainActor
final class SidebarTagEditorTests: XCTestCase {
    private var root: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        root = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-tag-editor-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: root, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: root)
        try super.tearDownWithError()
    }

    private func openVault(
        named name: String, files: [String]
    ) throws -> AppState {
        let vault = root.appendingPathComponent(name)
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        for path in files {
            let url = vault.appendingPathComponent(path)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(),
                withIntermediateDirectories: true)
            try "# \(path)".write(to: url, atomically: true, encoding: .utf8)
        }
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: root.appendingPathComponent("\(name)-recents.json")))
        state.openVault(at: vault)
        _ = try XCTUnwrap(state.currentSession).scanInitial(
            cancel: CancelToken())
        return state
    }

    private func publish(_ state: AppState, files: [String], folders: [String] = []) throws {
        let items =
            files.map {
                SidebarSelectionItem(path: $0, isDirectory: false, isMarkdown: true)
            }
            + folders.map {
                SidebarSelectionItem(path: $0, isDirectory: true, isMarkdown: false)
            }
        _ = state.publishSidebarSelectionSnapshot(
            SidebarSelectionSnapshot(
                sessionIdentity: ObjectIdentifier(
                    try XCTUnwrap(state.currentSession)),
                items: items,
                focusedPath: items.last?.path,
                creationParent: ""))
    }

    private func report(
        changed: UInt32, skipped: [SkippedFile] = [], inline: UInt32 = 0,
        summary: String
    ) -> TagEditReport {
        TagEditReport(
            changed: changed, skipped: skipped, inlineRemainder: inline,
            audioSummary: summary)
    }

    func testDispatchFreezesFileSelectionIntoAnEditorRequest() throws {
        let state = try openVault(named: "freeze", files: ["a.md", "b.md"])
        try publish(state, files: ["a.md", "b.md"])
        _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarAddTag)
        let request = try XCTUnwrap(state.sidebarTagEditorRequest)
        XCTAssertEqual(request.kind, .add)
        XCTAssertEqual(request.paths, ["a.md", "b.md"])

        state.sidebarTagEditorRequest = nil
        _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarRemoveTag)
        XCTAssertEqual(state.sidebarTagEditorRequest?.kind, .remove)
    }

    func testCommitRunsTheBatchOffMainAndAnnouncesOnceWithSkipClause()
        async throws
    {
        let state = try openVault(named: "commit", files: ["a.md", "b.md"])
        try publish(state, files: ["a.md", "b.md"])
        var invocations: [(paths: [String], tag: String)] = []
        state.sidebarTagAddRunner = { _, paths, tag in
            await MainActor.run { invocations.append((paths, tag)) }
            return self.report(
                changed: 1,
                skipped: [SkippedFile(path: "b.md", reason: "changed on disk since it was indexed.")],
                summary: "Tagged 1 file with #project.")
        }
        _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarAddTag)
        let request = try XCTUnwrap(state.sidebarTagEditorRequest)

        state.commitSidebarTagEdit(request: request, tag: "project")
        XCTAssertNil(
            state.sidebarTagEditorRequest,
            "the sheet dismisses immediately; the batch continues off-main")
        await state.sidebarTagEditTaskForTesting?.value
        XCTAssertEqual(invocations.count, 1)
        XCTAssertEqual(invocations[0].paths, ["a.md", "b.md"])
        XCTAssertEqual(invocations[0].tag, "project")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Tagged 1 file with #project. 1 skipped.",
            "one consolidated announcement: core summary + skip clause")
    }

    func testStaleCompletionAfterVaultSwitchStaysSilent() async throws {
        let state = try openVault(named: "stale", files: ["a.md"])
        try publish(state, files: ["a.md"])
        let gate = AsyncGate()
        state.sidebarTagAddRunner = { _, _, _ in
            await gate.wait()
            return self.report(changed: 1, summary: "Tagged 1 file with #x.")
        }
        _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarAddTag)
        let request = try XCTUnwrap(state.sidebarTagEditorRequest)
        state.commitSidebarTagEdit(request: request, tag: "x")
        state.closeVault()
        let before = state.lastMutationAnnouncement
        await gate.open()
        await state.sidebarTagEditTaskForTesting?.value
        XCTAssertEqual(
            state.lastMutationAnnouncement, before,
            "a completion for the CLOSED session must not narrate into "
                + "the next vault")
    }

    func testRemoveCommitPreservesTheHonestInlineRemainderSummary() async throws {
        let state = try openVault(named: "remove", files: ["a.md"])
        try publish(state, files: ["a.md"])
        state.sidebarTagRemoveRunner = { _, _, _ in
            self.report(
                changed: 2, inline: 1,
                summary: "Removed #project from 2 files. 1 still has it inline.")
        }
        _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarRemoveTag)
        let request = try XCTUnwrap(state.sidebarTagEditorRequest)
        state.commitSidebarTagEdit(request: request, tag: "#Project")
        await state.sidebarTagEditTaskForTesting?.value
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Removed #project from 2 files. 1 still has it inline.",
            "the core summary passes through verbatim when nothing skipped")
    }

    func testCommitNormalizesTheTypedTagBeforeTheRunner() async throws {
        let state = try openVault(named: "norm", files: ["a.md"])
        try publish(state, files: ["a.md"])
        var seen: [String] = []
        state.sidebarTagAddRunner = { _, _, tag in
            await MainActor.run { seen.append(tag) }
            return self.report(changed: 1, summary: "Tagged 1 file with #x.")
        }
        _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarAddTag)
        let request = try XCTUnwrap(state.sidebarTagEditorRequest)
        state.commitSidebarTagEdit(request: request, tag: "  #x  ")
        await state.sidebarTagEditTaskForTesting?.value
        XCTAssertEqual(seen, ["#x"], "trimmed; core owns hash/casefold normalization")
        // Empty input is inert — the editor stays up.
        state.sidebarTagEditorRequest = request
        state.commitSidebarTagEdit(request: request, tag: "   ")
        XCTAssertEqual(seen.count, 1)
        XCTAssertNotNil(state.sidebarTagEditorRequest)
    }

    func testSelectionTagsComeFromTheRunnerSeam() async throws {
        let state = try openVault(named: "picker", files: ["a.md"])
        state.sidebarSelectionTagsRunner = { _, paths in
            XCTAssertEqual(paths, ["a.md"])
            return [
                TagCount(tag: "alpha", fileCount: 2),
                TagCount(tag: "beta", fileCount: 1),
            ]
        }
        let counts = await state.sidebarSelectionTags(for: ["a.md"])
        XCTAssertEqual(counts.map(\.tag), ["alpha", "beta"])
        XCTAssertEqual(counts.map(\.fileCount), [2, 1])
    }

    /// One-shot async gate for holding a runner mid-flight.
    private actor AsyncGate {
        private var opened = false
        private var waiters: [CheckedContinuation<Void, Never>] = []
        func wait() async {
            if opened { return }
            await withCheckedContinuation { waiters.append($0) }
        }
        func open() {
            opened = true
            for waiter in waiters { waiter.resume() }
            waiters.removeAll()
        }
    }

    func testEditorRequestDiesWithTheVault() throws {
        let state = try openVault(named: "close", files: ["a.md"])
        try publish(state, files: ["a.md"])
        _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarAddTag)
        XCTAssertNotNil(state.sidebarTagEditorRequest)
        state.closeVault()
        XCTAssertNil(state.sidebarTagEditorRequest)
        XCTAssertNil(state.sidebarTagTree)
    }
}
