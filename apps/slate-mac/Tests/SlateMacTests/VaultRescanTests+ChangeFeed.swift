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
/// the count lands on the Refresh; the watcher facts at the end lift it.
extension VaultRescanTests {
    static let uncountedNoChanges =
        "Files refreshed. No changes found; changes from other Slate sessions were not counted."

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

    // MARK: - The open

    /// Design §4.2, §6: the open registers this session's cursor after its
    /// scan and before its listing, so the open scan's own files (which
    /// `VaultScanFinished` spoke) are not in the first window, and the first
    /// Refresh's window is exact and counted, not a gap.
    func testTheOpenRegistersTheChangeFeedAfterItsScan() async throws {
        let h = try prepare(["alpha.md": "# Alpha\n", "beta.md": "# Beta\n"])
        let heads = Locked<[UInt64?]>([])
        h.state.changeFeedRegisterObserver = { head in heads.mutate { $0.append(head) } }
        await h.openVault()
        XCTAssertEqual(heads.value.count, 1, "the open registered once")
        XCTAssertNotNil(heads.value.first ?? nil, "and it succeeded")
        let windows = h.recordWindows()

        try await h.refresh()

        XCTAssertEqual(windows.statuses, [.exact])
        XCTAssertEqual(windows.all.first?.recordCount, 0, "the open scan's files are not in it")
        XCTAssertEqual(windows.all.first?.counted, true)
        XCTAssertEqual(h.spoken.lines, [Self.noChanges])
    }

    // MARK: - Spec fact 2: the gap and the oversized window

    /// Spec fact 2 (Windows' `ATrimmedFeedFallsBackToTheFullReSyncWithTheNot
    /// CountedSentence`, AR-76): rows trimmed past this session's cursor, as
    /// the 100,000-row cap trims them, make the window a gap. The run reads
    /// no record, takes PR 19's full re-sync (another session's note reaches
    /// Quick Open and the tree all the same), and says changes from other
    /// sessions were not counted, with this scan's own counts. Its
    /// acknowledgment heals the feed: the next Refresh is exact and counted.
    /// A foreground run in a gap speaks the uncounted sentence only when its
    /// own scan found something.
    func testATrimmedFeedFallsBackToTheFullReSyncWithTheNotCountedSentence() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        let tree = try await h.mountTree()
        defer { h.unmount(tree) }
        let calls = h.recordCoreCalls()
        let windows = h.recordWindows()
        let other = try h.anotherSession()
        _ = try other.createExclusive(path: "new.md", content: "# New\n")
        try h.trimTheFeedPastTheCursor()

        try await h.refresh()

        XCTAssertEqual(windows.statuses, [.gap(reason: .trimmed)])
        XCTAssertEqual(calls.count("page"), 0, "a gap reads no record")
        XCTAssertEqual(h.spoken.lines, [Self.uncountedNoChanges])
        XCTAssertTrue(h.state.files.contains { $0.path == "new.md" }, "the full re-sync showed it")
        XCTAssertTrue(tree.model.rootLevel.contains { $0.path == "new.md" })

        h.spoken.lines.removeAll()
        windows.all.removeAll()
        _ = try other.saveText(
            path: "new.md", contents: "# New, again\n",
            expectedContentHash: try h.indexedHash("new.md"))
        try await h.refresh()
        XCTAssertEqual(windows.statuses, [.exact], "the acknowledgment healed the feed")
        XCTAssertEqual(h.spoken.lines, [Self.oneNew], "and the next Refresh counts exactly")

        // A foreground run in a gap: its own scan's counts, uncounted.
        h.spoken.lines.removeAll()
        h.clock.now += AppState.foregroundRescanCooldown + 1
        _ = try other.createExclusive(path: "quiet.md", content: "# Quiet\n")
        try h.trimTheFeedPastTheCursor()
        await h.state.requestRescan(.foreground)?.value
        XCTAssertEqual(h.spoken.lines, [], "its own scan found nothing: silent")
        XCTAssertTrue(h.state.files.contains { $0.path == "quiet.md" })

        h.clock.now += AppState.foregroundRescanCooldown + 1
        _ = try other.createExclusive(path: "more.md", content: "# More\n")
        try h.trimTheFeedPastTheCursor()
        try h.write("dropped.md", "# Dropped\n")
        await h.state.requestRescan(.foreground)?.value
        XCTAssertEqual(
            h.spoken.lines,
            [
                "Files refreshed. 1 new or changed, 0 removed; changes from other Slate sessions "
                    + "were not counted."
            ])
    }

    /// Windows' `ATooLargeWindowFallsBackWithTheNotCountedSentence`: another
    /// session's scan indexes 10,001 notes, one row past the window's limit
    /// (the owner's decision Q5). The window is too large and reads nothing;
    /// the full re-sync shows every note; the sentence says other sessions'
    /// changes were not counted. The acknowledgment moves the cursor past
    /// them: the next Refresh is exact and says nothing changed.
    func testATooLargeWindowFallsBackWithTheNotCountedSentence() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        let calls = h.recordCoreCalls()
        let windows = h.recordWindows()
        let folder = h.vault.appendingPathComponent("bulk", isDirectory: true)
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        for index in 0..<10_001 {
            let url = folder.appendingPathComponent("note-\(index).md")
            XCTAssertTrue(FileManager.default.createFile(atPath: url.path, contents: Data("n\n".utf8)))
        }
        let other = try h.anotherSession()
        _ = try other.scanInitial(cancel: CancelToken())

        try await h.refresh()

        XCTAssertEqual(windows.statuses, [.tooLarge])
        XCTAssertEqual(calls.count("page"), 0, "an oversized window reads no record")
        XCTAssertEqual(h.spoken.lines, [Self.uncountedNoChanges])
        XCTAssertEqual(h.state.files.count, 10_002, "the full re-sync listed every note")

        h.spoken.lines.removeAll()
        windows.all.removeAll()
        try await h.refresh()
        XCTAssertEqual(windows.statuses, [.exact])
        XCTAssertEqual(h.spoken.lines, [Self.noChanges])
    }

    // MARK: - Spec fact 3: the window's paths, and nothing else

    /// Spec fact 3 (Windows' `ReadingAndHistoryAreNotifiedOnlyForTheWindows
    /// Paths`): the active note's leaves follow the window's paths. Another
    /// session's edit of an unrelated note refreshes the leaves other files
    /// feed (its backlinks, links and embeds) but not the note's own, and the
    /// history panel does not reload; an edit of the note itself reloads its
    /// tab and every leaf, history included; a window with nothing in it
    /// touches no leaf.
    func testReadingAndHistoryAreNotifiedOnlyForTheWindowsPaths() async throws {
        let h = try await open([
            "note.md": "note one\n", "unrelated.md": "unrelated one\n",
        ])
        _ = try await h.openNote("note.md")
        await h.state.historyLoadTask?.value
        let other = try h.anotherSession()

        let historyBefore = h.state.historyLoadSeq
        let linksBefore = h.state.linksLoadTask
        _ = try other.saveText(
            path: "unrelated.md", contents: "unrelated two\n",
            expectedContentHash: try h.indexedHash("unrelated.md"))
        try await h.refresh()
        await h.state.historyLoadTask?.value
        XCTAssertEqual(h.state.historyLoadSeq, historyBefore, "an unrelated note's history stays")
        XCTAssertNotEqual(h.state.linksLoadTask, linksBefore, "the leaves other files feed refresh")

        _ = try other.saveText(
            path: "note.md", contents: "note two\n",
            expectedContentHash: try h.indexedHash("note.md"))
        try await h.refresh()
        await h.state.historyLoadTask?.value
        XCTAssertEqual(h.state.currentNoteText, "note two\n")
        XCTAssertGreaterThan(h.state.historyLoadSeq, historyBefore, "its own history reloads")

        let historyAfter = h.state.historyLoadSeq
        let linksAfter = h.state.linksLoadTask
        h.clock.now += AppState.foregroundRescanCooldown + 1
        await h.state.requestRescan(.foreground)?.value
        XCTAssertEqual(h.state.historyLoadSeq, historyAfter)
        XCTAssertEqual(h.state.linksLoadTask, linksAfter, "an empty window touches no leaf")
    }

    /// Windows' `ANoChangeForegroundRunTouchesNothingButTheTabComparison`: a
    /// foreground run that finds nothing reads its window and the open tabs'
    /// hashes, reads the head, acknowledges, and touches nothing else — no
    /// listing, no page, no tree refetch, no leaf.
    func testANoChangeForegroundRunTouchesNothingButTheTabComparison() async throws {
        let h = try await open(["alpha.md": "# Alpha\n", "beta.md": "# Beta\n"])
        let tree = try await h.mountTree()
        defer { h.unmount(tree) }
        _ = try await h.openNote("alpha.md")
        let calls = h.recordCoreCalls()
        let links = h.state.linksLoadTask
        let refetches = tree.refetches.value

        await h.state.requestRescan(.foreground)?.value

        XCTAssertEqual(calls.operations, ["scan", "window", "hashes", "head", "acknowledge"])
        XCTAssertEqual(tree.refetches.value, refetches, "no tree refetch")
        XCTAssertEqual(h.state.linksLoadTask, links, "no leaf")
        XCTAssertEqual(h.spoken.lines, [])
    }

    /// Windows' `ANoChangeRefreshAddsOnlyTheRecoverySet` (the owner's
    /// decision Q8): an unchanged explicit Refresh adds only the recovery set
    /// to that run — Quick Open's whole list — and re-checks a surface only
    /// when it shows a failure: a tree with a level it could not list
    /// refetches, and a leaf showing a load error reloads, with the window
    /// empty.
    func testANoChangeRefreshAddsOnlyTheRecoverySet() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        let tree = try await h.mountTree()
        defer { h.unmount(tree) }
        _ = try await h.openNote("alpha.md")
        let calls = h.recordCoreCalls()
        let refetches = tree.refetches.value

        try await h.refresh()

        XCTAssertEqual(
            calls.operations.filter { $0 != "inventory" },
            ["scan", "window", "hashes", "head", "acknowledge"])
        XCTAssertGreaterThan(calls.count("inventory"), 0, "Quick Open's whole list reloads")
        XCTAssertEqual(tree.refetches.value, refetches, "a healthy tree is not refetched")
        XCTAssertEqual(h.spoken.lines, [Self.noChanges])

        // A tree that shows a failed level, and a leaf that shows a load
        // error: both re-checked by an unchanged Refresh.
        let failing = Locked(true)
        let probe = h.state.rescanRun.registerTreeRefresher(showsFailure: { failing.value }) {
            failing.mutate { $0 = false }
            return 0
        }
        defer { h.state.rescanRun.unregisterTreeRefresher(probe) }
        h.state.linksLoadError = "the links could not load"
        let links = h.state.linksLoadTask
        h.spoken.lines.removeAll()

        try await h.refresh()

        XCTAssertFalse(failing.value, "the failing tree was refetched")
        XCTAssertNotEqual(h.state.linksLoadTask, links, "the failed leaf reloaded")
        await h.state.linksLoadTask?.value
        XCTAssertNil(h.state.linksLoadError)
        XCTAssertEqual(h.spoken.lines, [Self.noChanges])
    }

    // MARK: - Spec fact 4: pages

    /// Spec fact 4 (Windows' `AWindowSpanningSeveralPagesAppliesEveryRecord`):
    /// at a page limit of two, another session's five changes come as seven
    /// records in four pages under the run's one token, renames first, and
    /// every one is applied: Quick Open's list, the tree, and the open tab on
    /// the renamed note.
    func testAWindowSpanningSeveralPagesAppliesEveryRecord() async throws {
        let h = try await open([
            "edit.md": "edit one\n", "drop.md": "# Drop\n", "move.md": "move me\n",
        ])
        let tree = try await h.mountTree()
        defer { h.unmount(tree) }
        let moveTab = try await h.openNote("move.md")
        h.state.rescanRun.changeFeedPageLimit = 2
        let calls = h.recordCoreCalls()

        let other = try h.anotherSession()
        _ = try other.createExclusive(path: "one.md", content: "# One\n")
        _ = try other.createExclusive(path: "two.md", content: "# Two\n")
        _ = try other.saveText(
            path: "edit.md", contents: "edit two\n",
            expectedContentHash: try h.indexedHash("edit.md"))
        try other.deleteFile(path: "drop.md")
        _ = try other.renameFile(path: "move.md", newName: "moved.md")

        try await h.refresh()

        XCTAssertEqual(h.spoken.lines, ["Files refreshed. 4 new or changed, 2 removed."])
        XCTAssertGreaterThanOrEqual(calls.count("page"), 4, "seven records in pages of two")
        XCTAssertEqual(Set(calls.tokens).count, 1, "one token for the whole run")
        let listed = Set(h.state.files.map(\.path))
        XCTAssertEqual(listed, ["edit.md", "moved.md", "one.md", "two.md"])
        let shown = Set(tree.model.rootLevel.map(\.path))
        XCTAssertEqual(shown, ["edit.md", "moved.md", "one.md", "two.md"])
        XCTAssertEqual(h.state.workspace.model.tab(moveTab)?.item, .markdown(path: "moved.md"))
        XCTAssertEqual(h.state.loadedFilePath, "moved.md")
    }

    // MARK: - AR-31: passes

    /// Windows' `ACommitDuringTheReSyncIsAppliedByAFurtherPass` (AR-31):
    /// another session commits while the run's tree publication is parked.
    /// The head the run reads after it says so, and a further window applies
    /// the commit: at the end the tree and Quick Open agree, and the
    /// Refresh counts both changes, once.
    func testACommitDuringTheReSyncIsAppliedByAFurtherPass() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        let gate = Gate()
        let tree = try await h.mountTree(parkingFirstRefetchAt: gate)
        defer { h.unmount(tree) }
        let windows = h.recordWindows()
        let other = try h.anotherSession()
        _ = try other.createExclusive(path: "one.md", content: "# One\n")

        let run = try XCTUnwrap(h.state.requestRescan(.explicit))
        // Bounded: a run that never reaches the tree's publication fails
        // here instead of waiting for ever.
        await h.until("the run's tree publication, parked", timeout: 15) {
            tree.refetches.value > 0
        }
        guard tree.refetches.value > 0 else { return }
        await gate.untilParked()
        _ = try other.createExclusive(path: "two.md", content: "# Two\n")
        await gate.release()
        await run.value

        XCTAssertEqual(windows.statuses, [.exact, .exact], "a further pass")
        XCTAssertEqual(windows.all.last?.fromSeq, windows.all.first?.toSeq)
        XCTAssertTrue(h.state.files.contains { $0.path == "two.md" })
        XCTAssertEqual(
            Set(h.state.files.map(\.path)), Set(tree.model.rootLevel.map(\.path)),
            "the tree and Quick Open agree")
        XCTAssertEqual(h.spoken.lines, ["Files refreshed. 2 new or changed, 0 removed."])

        h.spoken.lines.removeAll()
        try await h.refresh()
        XCTAssertEqual(h.spoken.lines, [Self.noChanges], "the last window was acknowledged")
    }

    // MARK: - The Slate-write journal and the tab comparison

    /// Windows' `ASlateWriteDuringTheWindowIsReReadNotReverted`: another
    /// session creates a note, and this session's user moves it to the Trash
    /// while the run's window is open. The window's record says the note is
    /// there, but the delete is journaled and re-read from the index after
    /// the records: Quick Open's list does not get it back.
    func testASlateWriteDuringTheWindowIsReReadNotReverted() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        let other = try h.anotherSession()
        _ = try other.createExclusive(path: "brief.md", content: "# Brief\n")
        let deleted = Locked(false)
        h.state.rescanRun.afterWindowOpenedHook = { [state = h.state] _ in
            guard !deleted.value else { return }
            deleted.mutate { $0 = true }
            await state.deleteEntry(path: "brief.md", isDirectory: false)?.value
        }

        try await h.refresh()

        XCTAssertTrue(deleted.value)
        XCTAssertFalse(
            h.state.files.contains { $0.path == "brief.md" },
            "the journaled delete wins over the window's older record")
        XCTAssertFalse(
            FileManager.default.fileExists(atPath: h.vault.appendingPathComponent("brief.md").path))
    }

    /// Windows' `AnOwnRenameAfterTheWindowOpensStillGetsTheOtherSessionsEdit`:
    /// another session edits the open note, and this session's user renames
    /// it after the run's window opened. The rename moves the tab (its event
    /// is journaled); the tab comparison then finds the edit under the new
    /// name and reloads the clean tab onto it.
    func testAnOwnRenameAfterTheWindowOpensStillGetsTheOtherSessionsEdit() async throws {
        let h = try await open(["draft.md": "draft one\n"])
        let tab = try await h.openNote("draft.md")
        let other = try h.anotherSession()
        _ = try other.saveText(
            path: "draft.md", contents: "draft two\n",
            expectedContentHash: try h.indexedHash("draft.md"))
        let renamed = Locked(false)
        h.state.rescanRun.afterWindowOpenedHook = { [state = h.state] _ in
            guard !renamed.value else { return }
            renamed.mutate { $0 = true }
            await state.renameEntry(path: "draft.md", isDirectory: false, to: "final.md")?.value
        }

        try await h.refresh()

        XCTAssertTrue(renamed.value)
        XCTAssertEqual(h.state.workspace.model.tab(tab)?.item, .markdown(path: "final.md"))
        XCTAssertEqual(h.state.currentNoteText, "draft two\n", "the moved tab has the edit")
        XCTAssertFalse(h.state.hasUnsavedChanges)
        XCTAssertEqual(Set(h.state.files.map(\.path)), ["final.md"])
    }

    /// Windows' `AnOwnRenameOfANoteAnotherSessionEditedReloadsTheMovedTab`
    /// (design §4.3's first row): another session edits a note and this
    /// session's user renames it before the next run. The window moves the
    /// edit to the new name — one modified, nothing removed — and the tab the
    /// rename moved reloads onto it.
    func testAnOwnRenameOfANoteAnotherSessionEditedReloadsTheMovedTab() async throws {
        let h = try await open(["draft.md": "draft one\n"])
        let tab = try await h.openNote("draft.md")
        let other = try h.anotherSession()
        _ = try other.saveText(
            path: "draft.md", contents: "draft two\n",
            expectedContentHash: try h.indexedHash("draft.md"))
        await h.state.renameEntry(path: "draft.md", isDirectory: false, to: "final.md")?.value
        h.spoken.lines.removeAll()

        try await h.refresh()

        XCTAssertEqual(h.spoken.lines, [Self.oneNew])
        XCTAssertEqual(h.state.workspace.model.tab(tab)?.item, .markdown(path: "final.md"))
        XCTAssertEqual(h.state.currentNoteText, "draft two\n")
    }

    /// Windows' `AnUnverifiedTabReloadIsRetriedByTheNextForegroundRun`: the
    /// disk moves past the index between the scan and the tab's read, so the
    /// clean tab cannot be vouched for; it keeps its text and the run counts
    /// one error. The next foreground run compares the tab again, whatever
    /// its window holds, and reloads it.
    func testAnUnverifiedTabReloadIsRetriedByTheNextForegroundRun() async throws {
        let h = try await open(["draft.md": "draft one\n"])
        _ = try await h.openNote("draft.md")
        let other = try h.anotherSession()
        _ = try other.saveText(
            path: "draft.md", contents: "draft two\n",
            expectedContentHash: try h.indexedHash("draft.md"))
        h.state.rescanRun.afterScanHook = { [h] in
            try? h.write("draft.md", "draft three\n")
        }

        try await h.refresh()

        XCTAssertEqual(h.spoken.lines, [Self.oneError])
        XCTAssertEqual(h.state.currentNoteText, "draft one\n", "unvouched bytes are never applied")

        h.state.rescanRun.afterScanHook = nil
        h.spoken.lines.removeAll()
        h.clock.now += AppState.foregroundRescanCooldown + 1
        await h.state.requestRescan(.foreground)?.value
        XCTAssertEqual(h.state.currentNoteText, "draft three\n", "the next run reloaded it")
        XCTAssertEqual(h.spoken.lines, [Self.oneNew], "its own scan found the change")
    }

    // MARK: - Renames another session made (the owner's decision Q3)

    /// Windows' `AForeignRenameRetargetsTheOpenTab` (Q3; AR-8 amended):
    /// another session renames two open notes. Each tab that held one moves
    /// to the new name as for this session's own rename — the clean active
    /// one with its text, the dirty parked one keeping its unsaved words — and
    /// each rename counts one new and one removed (Q4).
    func testAForeignRenameRetargetsTheOpenTab() async throws {
        let h = try await open(["dirty.md": "dirty one\n", "clean.md": "clean one\n"])
        let dirtyTab = try await h.openNote("dirty.md")
        h.state.updateEditorText("my unsaved words\n")
        let cleanTab = try await h.openNoteInNewTab("clean.md")
        let other = try h.anotherSession()
        _ = try other.renameFile(path: "clean.md", newName: "clean-renamed.md")
        _ = try other.renameFile(path: "dirty.md", newName: "dirty-renamed.md")

        try await h.refresh()

        XCTAssertEqual(h.spoken.lines, ["Files refreshed. 2 new or changed, 2 removed."])
        XCTAssertEqual(
            h.state.workspace.model.tab(cleanTab)?.item, .markdown(path: "clean-renamed.md"))
        XCTAssertEqual(h.state.loadedFilePath, "clean-renamed.md")
        XCTAssertEqual(h.state.currentNoteText, "clean one\n")
        XCTAssertNil(h.state.noteLoadError, "never marked missing")
        XCTAssertEqual(
            h.state.workspace.model.tab(dirtyTab)?.item, .markdown(path: "dirty-renamed.md"))
        let parked = try XCTUnwrap(h.state.workspace.document(for: dirtyTab))
        XCTAssertEqual(parked.text, "my unsaved words\n", "a dirty tab keeps its buffer")
        XCTAssertTrue(parked.hasUnsavedChanges)
        XCTAssertEqual(
            Set(h.state.files.map(\.path)), ["clean-renamed.md", "dirty-renamed.md"])
    }

    /// Windows' `AForeignRenameThenAnOwnRenameOfItsDestinationMovesTheTabThere`
    /// (design §4.3's table): another session renames the open note, and this
    /// session's user renames it again before the run. One renamed record,
    /// from where the tab is to where the file is, moves the tab there.
    func testAForeignRenameThenAnOwnRenameOfItsDestinationMovesTheTabThere() async throws {
        let h = try await open(["first.md": "first\n"])
        let tab = try await h.openNote("first.md")
        let other = try h.anotherSession()
        _ = try other.renameFile(path: "first.md", newName: "second.md")
        await h.state.renameEntry(path: "second.md", isDirectory: false, to: "third.md")?.value
        h.spoken.lines.removeAll()

        try await h.refresh()

        XCTAssertEqual(h.spoken.lines, ["Files refreshed. 1 new or changed, 1 removed."])
        XCTAssertEqual(h.state.workspace.model.tab(tab)?.item, .markdown(path: "third.md"))
        XCTAssertEqual(h.state.loadedFilePath, "third.md")
        XCTAssertEqual(h.state.currentNoteText, "first\n")
        XCTAssertEqual(Set(h.state.files.map(\.path)), ["third.md"])
    }

    /// Windows' `ANoteThatTookARenamedNotesPathKeepsItsTab` (design §4.3's
    /// table; the `from_hash` guard): another session renames a note, and
    /// this session's user renames another note into the freed name before
    /// the run. Only the tab that held the renamed note moves; the tab of the
    /// note now at that name stays with it.
    func testANoteThatTookARenamedNotesPathKeepsItsTab() async throws {
        let h = try await open(["old.md": "old note\n", "other.md": "other note\n"])
        let oldTab = try await h.openNote("old.md")
        let otherTab = try await h.openNoteInNewTab("other.md")
        let session = try h.anotherSession()
        _ = try session.renameFile(path: "old.md", newName: "moved.md")
        await h.state.renameEntry(path: "other.md", isDirectory: false, to: "old.md")?.value
        XCTAssertEqual(h.state.workspace.model.tab(otherTab)?.item, .markdown(path: "old.md"))
        h.spoken.lines.removeAll()

        try await h.refresh()

        XCTAssertEqual(h.spoken.lines, [Self.oneNew])
        XCTAssertEqual(h.state.workspace.model.tab(oldTab)?.item, .markdown(path: "moved.md"))
        XCTAssertEqual(h.state.workspace.document(for: oldTab)?.text, "old note\n")
        XCTAssertEqual(h.state.workspace.model.tab(otherTab)?.item, .markdown(path: "old.md"))
        XCTAssertEqual(h.state.loadedFilePath, "old.md")
        XCTAssertEqual(h.state.currentNoteText, "other note\n", "the note now at the name keeps its tab")
    }

    /// The owner's decision Q3 for a tab not loaded since it opened (a
    /// restored tab never shown, or one whose clean document was dropped): it
    /// holds no baseline of its own and shows whatever is at its path when it
    /// loads. So it follows another session's rename when nothing is at the
    /// old path at the window's target, and stays when something is there
    /// again, since it may be the note now at that path.
    func testATabNeverLoadedFollowsARenameOnlyWhenItsOldPathIsGone() async throws {
        let h = try await open([
            "gone.md": "gone note\n", "taken.md": "taken note\n", "front.md": "front\n",
        ])
        let goneTab = try await h.openNote("gone.md")
        let takenTab = try await h.openNoteInNewTab("taken.md")
        _ = try await h.openNoteInNewTab("front.md")
        h.state.workspace.invalidateParkedDocuments(tabIDs: [goneTab, takenTab])
        XCTAssertNil(h.state.workspace.document(for: goneTab), "never loaded, as after a restore")
        XCTAssertNil(h.state.workspace.document(for: takenTab))
        let other = try h.anotherSession()
        _ = try other.renameFile(path: "gone.md", newName: "gone-renamed.md")
        _ = try other.renameFile(path: "taken.md", newName: "taken-renamed.md")
        _ = try other.createExclusive(path: "taken.md", content: "a new note at the old name\n")

        try await h.refresh()

        XCTAssertEqual(h.spoken.lines, ["Files refreshed. 3 new or changed, 1 removed."])
        XCTAssertEqual(
            h.state.workspace.model.tab(goneTab)?.item, .markdown(path: "gone-renamed.md"),
            "nothing is at its old name: it follows the rename")
        XCTAssertEqual(
            h.state.workspace.model.tab(takenTab)?.item, .markdown(path: "taken.md"),
            "a note is at its old name again: it stays")
        h.state.selectTab(id: goneTab)
        await h.state.noteLoadTask?.value
        XCTAssertEqual(h.state.currentNoteText, "gone note\n", "and loads the renamed note")
    }

    // MARK: - Acknowledgment, cancellation and the retry set

    /// Windows' `ACancelledRunLeavesTheCursorWhereItWas`: a run cancelled after
    /// its window opened acknowledges nothing and says nothing; the next
    /// Refresh reads the same window again and counts it.
    func testACancelledRunLeavesTheCursorWhereItWas() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        let calls = h.recordCoreCalls()
        let windows = h.recordWindows()
        let other = try h.anotherSession()
        _ = try other.createExclusive(path: "new.md", content: "# New\n")
        h.state.rescanRun.afterWindowOpenedHook = { [state = h.state] _ in
            state.rescanRun.cancelToken?.cancel()
        }

        try await h.refresh()

        XCTAssertEqual(h.spoken.lines, [], "a cancelled run says nothing")
        XCTAssertEqual(calls.count("acknowledge"), 0, "and acknowledges nothing")

        h.state.rescanRun.afterWindowOpenedHook = nil
        try await h.refresh()
        XCTAssertEqual(windows.all.count, 2)
        XCTAssertEqual(windows.all.last?.fromSeq, windows.all.first?.fromSeq, "the same window")
        XCTAssertEqual(h.spoken.lines, [Self.oneNew])
    }

    /// Design §4.2: a failed acknowledgment is one error for its run, and the
    /// next run reads the window again, so its counts are spoken once, by the
    /// run that acknowledges them. A failed feed read is one error too, and
    /// the run acknowledges nothing: a window that cannot be read falls back
    /// to the full re-sync, and a head that cannot be read after an exact
    /// window's publication leaves that window to the next run. Either way
    /// the sentence is the incomplete one (design §4.7's first row), never
    /// the uncounted one, which is for a feed that answered with a gap or an
    /// oversized window.
    func testAFailedAcknowledgmentOrFeedReadIsOneErrorAndTheWindowIsReadAgain() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        let calls = h.recordCoreCalls()
        let windows = h.recordWindows()
        let other = try h.anotherSession()
        _ = try other.createExclusive(path: "new.md", content: "# New\n")
        let failing = Locked<Set<String>>(["acknowledge"])
        h.state.rescanRun.coreCallFault = { operation in failing.value.contains(operation) }

        try await h.refresh()
        XCTAssertEqual(h.spoken.lines, [Self.oneError])
        XCTAssertTrue(h.state.files.contains { $0.path == "new.md" }, "applied all the same")

        failing.mutate { $0 = ["window"] }
        h.spoken.lines.removeAll()
        try await h.refresh()
        XCTAssertEqual(h.spoken.lines, [Self.oneError], "a feed read that fails")
        XCTAssertTrue(h.state.files.contains { $0.path == "new.md" }, "the full re-sync showed it")

        // The head read fails after an exact window was applied.
        failing.mutate { $0 = ["head"] }
        h.spoken.lines.removeAll()
        windows.all.removeAll()
        let acknowledgments = calls.count("acknowledge")
        let heads = calls.count("head")
        try await h.refresh()
        XCTAssertEqual(windows.statuses, [.exact], "the window was read and applied")
        XCTAssertEqual(calls.count("head"), heads + 1, "the head was read once, and failed")
        XCTAssertEqual(
            calls.count("acknowledge"), acknowledgments, "a failed head read acknowledges nothing")
        XCTAssertEqual(h.spoken.lines, [Self.oneError], "incomplete, never uncounted")

        failing.mutate { $0 = [] }
        h.spoken.lines.removeAll()
        try await h.refresh()
        XCTAssertEqual(h.spoken.lines, [Self.oneNew], "counted once, by the run that acknowledged it")
    }

    /// Windows' `AFailedQuickOpenOrTreeOperationIsRetriedByTheNextRun` (the
    /// retry set): a Quick Open listing that fails is counted, and the next
    /// run — a foreground run that found nothing — lists it again; a tree
    /// refetch that fails is counted, and the next run refetches it.
    func testAFailedQuickOpenOrTreeOperationIsRetriedByTheNextRun() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        let calls = h.recordCoreCalls()
        let failInventory = Locked(true)
        h.state.rescanRun.coreCallFault = { operation in
            operation == "inventory" && failInventory.value
        }

        try await h.refresh()
        XCTAssertEqual(h.spoken.lines, [Self.oneError])

        failInventory.mutate { $0 = false }
        h.spoken.lines.removeAll()
        let listed = calls.count("inventory")
        h.clock.now += AppState.foregroundRescanCooldown + 1
        await h.state.requestRescan(.foreground)?.value
        XCTAssertGreaterThan(calls.count("inventory"), listed, "the retry set listed it again")
        XCTAssertEqual(h.spoken.lines, [])

        let failures = Locked<[UInt64]>([1])
        let refetches = Locked(0)
        let tree = h.state.rescanRun.registerTreeRefresher {
            refetches.mutate { $0 += 1 }
            var failed: UInt64 = 0
            failures.mutate { if !$0.isEmpty { failed = $0.removeFirst() } }
            return failed
        }
        defer { h.state.rescanRun.unregisterTreeRefresher(tree) }
        let other = try h.anotherSession()
        _ = try other.createExclusive(path: "new.md", content: "# New\n")
        h.clock.now += AppState.foregroundRescanCooldown + 1
        await h.state.requestRescan(.foreground)?.value
        XCTAssertEqual(refetches.value, 1, "the structural record refetched the tree")
        XCTAssertEqual(h.spoken.lines, [Self.oneError], "and its failure was counted")

        h.spoken.lines.removeAll()
        h.clock.now += AppState.foregroundRescanCooldown + 1
        await h.state.requestRescan(.foreground)?.value
        XCTAssertEqual(refetches.value, 2, "the retry set refetched it, with the window empty")
        XCTAssertEqual(h.spoken.lines, [])
    }

    // MARK: - Spec fact 6: the watcher

    /// Q2 through the watcher's real adapter: a Watcher run applies another
    /// session's change to Quick Open's list and the tree and says nothing,
    /// because its own scan found nothing — a `slate write` while Slate is
    /// active is not announced. Its window is acknowledged: a Refresh then
    /// counts nothing.
    func testAWatcherRunAppliesAnotherSessionsChangeSilently() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        let tree = try await h.mountTree()
        defer { h.unmount(tree) }
        let other = try h.anotherSession()
        _ = try other.createExclusive(path: "from-cli.md", content: "# From the CLI\n")

        await h.signalAndAwaitTheWatcherRun()

        XCTAssertEqual(h.runs.reasons, [.watcher])
        XCTAssertEqual(h.spoken.lines, [], "Q2: another session's change is not announced")
        XCTAssertTrue(h.state.files.contains { $0.path == "from-cli.md" })
        XCTAssertTrue(tree.model.rootLevel.contains { $0.path == "from-cli.md" })

        try await h.refresh()
        XCTAssertEqual(h.spoken.lines, [Self.noChanges])
    }

    /// Spec fact 6 (Windows' `AWatcherDrivenChangeFromAnotherSessionIsApplied
    /// WithoutARefresh`), on real FSEvents: another session's write is not in
    /// this session's echo ledger, so the watcher calls back; the Watcher
    /// run's scan finds the note already indexed, and its window applies the
    /// other session's row to Quick Open's list and the tree with no Refresh,
    /// silently (Q2). Bounded wait: 30 s, ending early.
    func testAWatcherDrivenChangeFromAnotherSessionIsAppliedWithoutARefresh() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"], watching: true)
        let tree = try await h.mountTree()
        defer { h.unmount(tree) }
        let other = try h.anotherSession()

        _ = try other.createExclusive(path: "from-other.md", content: "# From another window\n")
        await h.until("the watcher's run applying another session's note", timeout: 30) {
            h.state.files.contains { $0.path == "from-other.md" }
                && tree.model.rootLevel.contains { $0.path == "from-other.md" }
                && !h.state.isRescanning
        }
        await h.state.vaultRescanTask?.value

        XCTAssertFalse(h.runs.reasons.isEmpty, "no rescan ran")
        XCTAssertEqual(Set(h.runs.reasons), [.watcher], "Watcher runs only: no Refresh")
        XCTAssertEqual(h.spoken.lines, [], "Q2: a Watcher run never speaks another session's change")
    }
}

// MARK: - Harness

extension VaultRescanTests {
    /// The change-feed windows the runs opened, in order.
    final class Windows {
        var all: [ChangeFeedWindow] = []
        var statuses: [ChangeFeedStatus] { all.map(\.status) }
    }

    /// A files tree a fact mounted: its model, its registration with the
    /// run, and how many refetches the run made.
    @MainActor
    struct MountedTree {
        let model: FileTreeViewModel
        let registration: UUID
        let refetches: Locked<Int>
    }
}

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
    /// tree, as the mounted sidebar registers it, with its failure probe. A
    /// gate parks its first refetch (the run's tree publication) until
    /// released.
    func mountTree(
        parkingFirstRefetchAt gate: VaultRescanTests.Gate? = nil
    ) async throws -> VaultRescanTests.MountedTree {
        let session = try XCTUnwrap(state.currentSession)
        let model = FileTreeViewModel()
        model.bind(to: session)
        _ = await model.settleLevelLoadsForTesting()
        let refetches = Locked(0)
        let registration = state.rescanRun.registerTreeRefresher(
            showsFailure: { model.showsFailedLevelForRescan }
        ) {
            refetches.mutate { $0 += 1 }
            if let gate { await gate.park() }
            return await model.refreshForRescan()
        }
        return VaultRescanTests.MountedTree(
            model: model, registration: registration, refetches: refetches)
    }

    func unmount(_ tree: VaultRescanTests.MountedTree) {
        state.rescanRun.unregisterTreeRefresher(tree.registration)
    }

    func recordWindows() -> VaultRescanTests.Windows {
        let windows = VaultRescanTests.Windows()
        state.rescanRun.windowObserver = { windows.all.append($0) }
        return windows
    }

    /// SQL against this vault's cache from ANOTHER process, the system's
    /// `sqlite3`, as another tool would run it: two copies of SQLite in one
    /// process must never share a database file, since closing one's file
    /// releases the other's POSIX locks. A fact uses it only for what time or
    /// the 100,000-row cap would otherwise take to produce.
    func cacheSQL(_ sql: String) throws {
        let tool = "/usr/bin/sqlite3"
        guard FileManager.default.isExecutableFile(atPath: tool) else {
            throw XCTSkip("No \(tool) to edit the cache from another process.")
        }
        let process = Process()
        process.executableURL = URL(fileURLWithPath: tool)
        process.arguments = [
            "-bail", "-cmd", ".timeout 10000",
            vault.appendingPathComponent(".slate/cache.sqlite").path, sql,
        ]
        let errors = Pipe()
        process.standardError = errors
        process.standardOutput = FileHandle.nullDevice
        try process.run()
        process.waitUntilExit()
        let message =
            String(data: errors.fileHandleForReading.readDataToEndOfFile(), encoding: .utf8) ?? ""
        XCTAssertEqual(process.terminationStatus, 0, "sqlite3: \(message)")
    }

    /// The feed's rows trimmed past this session's cursor, as the 100,000-row
    /// cap trims them while it lags: its next window is a gap (`Trimmed`).
    func trimTheFeedPastTheCursor() throws {
        try cacheSQL(
            """
            DELETE FROM change_feed;
            UPDATE change_feed_meta
               SET trimmed_through = (SELECT seq FROM sqlite_sequence WHERE name = 'change_feed')
             WHERE id = 1;
            """)
    }
}
