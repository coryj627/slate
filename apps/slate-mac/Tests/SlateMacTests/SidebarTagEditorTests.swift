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

    func testCommitRunsTheBatchAndAnnouncesOnceWithSkipClause() throws {
        let state = try openVault(named: "commit", files: ["a.md", "b.md"])
        try publish(state, files: ["a.md", "b.md"])
        var invocations: [(paths: [String], tag: String)] = []
        state.sidebarTagAddRunner = { paths, tag in
            invocations.append((paths, tag))
            return self.report(
                changed: 1,
                skipped: [SkippedFile(path: "b.md", reason: "changed on disk since it was indexed.")],
                summary: "Tagged 1 files with #project.")
        }
        _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarAddTag)
        let request = try XCTUnwrap(state.sidebarTagEditorRequest)

        state.commitSidebarTagEdit(request: request, tag: "project")
        XCTAssertEqual(invocations.count, 1)
        XCTAssertEqual(invocations[0].paths, ["a.md", "b.md"])
        XCTAssertEqual(invocations[0].tag, "project")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Tagged 1 files with #project. 1 skipped.",
            "one consolidated announcement: core summary + skip clause")
        XCTAssertNil(
            state.sidebarTagEditorRequest, "commit dismisses the editor")
    }

    func testRemoveCommitPreservesTheHonestInlineRemainderSummary() throws {
        let state = try openVault(named: "remove", files: ["a.md"])
        try publish(state, files: ["a.md"])
        state.sidebarTagRemoveRunner = { _, _ in
            self.report(
                changed: 2, inline: 1,
                summary: "Removed #project from 2 files. 1 still have it inline.")
        }
        _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarRemoveTag)
        let request = try XCTUnwrap(state.sidebarTagEditorRequest)
        state.commitSidebarTagEdit(request: request, tag: "#Project")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Removed #project from 2 files. 1 still have it inline.",
            "the core summary passes through verbatim when nothing skipped")
    }

    func testCommitNormalizesTheTypedTagBeforeTheRunner() throws {
        let state = try openVault(named: "norm", files: ["a.md"])
        try publish(state, files: ["a.md"])
        var seen: [String] = []
        state.sidebarTagAddRunner = { _, tag in
            seen.append(tag)
            return self.report(changed: 1, summary: "Tagged 1 files with #x.")
        }
        _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarAddTag)
        let request = try XCTUnwrap(state.sidebarTagEditorRequest)
        state.commitSidebarTagEdit(request: request, tag: "  #x  ")
        XCTAssertEqual(seen, ["#x"], "trimmed; core owns hash/casefold normalization")
        // Empty input is inert — the editor stays up.
        state.sidebarTagEditorRequest = request
        state.commitSidebarTagEdit(request: request, tag: "   ")
        XCTAssertEqual(seen.count, 1)
        XCTAssertNotNil(state.sidebarTagEditorRequest)
    }

    func testSelectionTagsComeFromTheProviderSeam() throws {
        let state = try openVault(named: "picker", files: ["a.md"])
        state.sidebarSelectionTagsProvider = { paths in
            XCTAssertEqual(paths, ["a.md"])
            return [
                TagCount(tag: "alpha", fileCount: 2),
                TagCount(tag: "beta", fileCount: 1),
            ]
        }
        let counts = state.sidebarSelectionTags(for: ["a.md"])
        XCTAssertEqual(counts.map(\.tag), ["alpha", "beta"])
        XCTAssertEqual(counts.map(\.fileCount), [2, 1])
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
