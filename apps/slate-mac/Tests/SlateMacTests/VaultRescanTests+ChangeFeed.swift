// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

/// Plan 43 PR 21c (#1293; design
/// `docs/plans/43_mac_backlog_delivery/21_change_feed_design.md` §4.3–§4.7,
/// §6): the mac host of core's change feed. Another Slate session on the same
/// vault — another window, or `slate-cli` — is a second `VaultSession` with
/// its own connection to the shared cache, as a second process is (PR 21a's
/// facts make the same stand-in). The names are the Windows twins' (§4.9) in
/// Swift form. The vault watcher stays off for every fact that writes from
/// another session and then presses Refresh (the bootstrap's test switch), so
/// the count lands on the Refresh.
extension VaultRescanTests {
    // MARK: - Spec fact 1: AR-30 retired

    /// The owner's decision Q7 retired AR-30 (the TGC-17 rule: the witness
    /// is rewritten first). Mac never had a twin of Windows'
    /// `AnotherSessionsChangesAreShownButNotCounted`, so this one is written
    /// first instead, against the behaviour AR-30 described: another
    /// session's create, edit and delete were shown after a Refresh but not
    /// counted ("Files refreshed. No changes."). Now the Refresh applies them
    /// to Quick Open's list, the tree and a clean open tab, and counts them
    /// exactly. Its window is acknowledged, so the next Refresh counts
    /// nothing. Under the owner's decision Q2 a foreground run applies
    /// another session's change silently: it speaks only its own scan's.
    func testAnotherSessionsChangesAreAppliedAndCountedExactly() async throws {
        let h = try await open([
            "alpha.md": "# Alpha\n", "edit.md": "edit one\n", "gone.md": "# Gone\n",
        ])
        let tree = try await h.mountTree()
        defer { h.unmount(tree) }
        _ = try await h.openNote("edit.md")

        let other = try h.anotherSession()
        _ = try other.createExclusive(path: "new.md", content: "# New\n")
        _ = try other.saveText(
            path: "edit.md", contents: "edit two\n",
            expectedContentHash: try h.indexedHash("edit.md"))
        try other.deleteFile(path: "gone.md")

        try await h.refresh()

        XCTAssertEqual(
            h.spoken.lines, ["Files refreshed. 2 new or changed, 1 removed."],
            "another session's create, edit and delete are counted exactly")
        XCTAssertTrue(h.state.files.contains { $0.path == "new.md" })
        XCTAssertFalse(h.state.files.contains { $0.path == "gone.md" })
        XCTAssertTrue(tree.model.rootLevel.contains { $0.path == "new.md" })
        XCTAssertFalse(tree.model.rootLevel.contains { $0.path == "gone.md" })
        XCTAssertEqual(h.state.currentNoteText, "edit two\n", "the clean tab took the edit")
        XCTAssertEqual(h.state.currentNoteContentHash, try h.indexedHash("edit.md"))

        h.spoken.lines.removeAll()
        try await h.refresh()
        XCTAssertEqual(
            h.spoken.lines, ["Files refreshed. No changes."],
            "the window was acknowledged: nothing is counted twice")

        // Q2: a foreground run applies another session's changes silently.
        h.spoken.lines.removeAll()
        h.clock.now += AppState.foregroundRescanCooldown + 1
        _ = try other.saveText(
            path: "edit.md", contents: "edit three\n",
            expectedContentHash: try h.indexedHash("edit.md"))
        _ = try other.createExclusive(path: "later.md", content: "# Later\n")
        await h.state.requestRescan(.foreground)?.value

        XCTAssertEqual(h.spoken.lines, [], "Q2: a foreground run speaks only its own scan's changes")
        XCTAssertEqual(h.state.currentNoteText, "edit three\n")
        XCTAssertTrue(h.state.files.contains { $0.path == "later.md" })
        XCTAssertTrue(tree.model.rootLevel.contains { $0.path == "later.md" })

        h.spoken.lines.removeAll()
        try await h.refresh()
        XCTAssertEqual(
            h.spoken.lines, ["Files refreshed. No changes."],
            "the foreground run acknowledged its window too")
    }
}

// MARK: - Harness

extension VaultRescanTests.Harness {
    /// Another Slate session on this vault: its own `VaultSession` and its
    /// own connection to the shared cache, as another window or `slate-cli`
    /// has. It never registers a cursor unless asked (`slate-cli` never
    /// does); its writes reach this vault's open session only through the
    /// change feed. Released when the fact ends.
    func anotherSession(registered: Bool = false) throws -> VaultSession {
        let session = try VaultSession.openFilesystem(rootPath: vault.path)
        if registered { _ = try session.registerChangeFeed() }
        return session
    }

    /// A files tree bound to the open session and registered as the run's
    /// tree, as the mounted sidebar registers it.
    func mountTree() async throws -> VaultRescanTests.MountedTree {
        let session = try XCTUnwrap(state.currentSession)
        let model = FileTreeViewModel()
        model.bind(to: session)
        _ = await model.settleLevelLoadsForTesting()
        let registration = state.rescanRun.registerTreeRefresher { await model.refreshForRescan() }
        return VaultRescanTests.MountedTree(model: model, registration: registration)
    }

    func unmount(_ tree: VaultRescanTests.MountedTree) {
        state.rescanRun.unregisterTreeRefresher(tree.registration)
    }
}

extension VaultRescanTests {
    @MainActor
    struct MountedTree {
        let model: FileTreeViewModel
        let registration: UUID
    }
}
