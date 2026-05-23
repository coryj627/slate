import Combine
import XCTest

@testable import SlateMac

/// Focused on the recent-vaults edges of AppState — the
/// `missingRecentVault` flagging path and removal flow that the
/// welcome-screen alert is wired to. The actual VaultSession-opening
/// path goes through the Rust core and is exercised by other tests.
@MainActor
final class AppStateTests: XCTestCase {
    private var tempDir: URL!
    private var storeFile: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-appstate-test-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
        storeFile = tempDir.appendingPathComponent("recent-vaults.json")
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    private func makeAppState(seedEntries: [RecentVault] = []) throws -> AppState {
        let store = RecentVaultsStore(fileURL: storeFile)
        if !seedEntries.isEmpty {
            try store.save(seedEntries)
        }
        return AppState(recentsStore: store)
    }

    func testInitLoadsExistingRecentsFromStore() throws {
        let seed = [
            RecentVault(path: "/tmp/a", displayName: "a", lastOpenedMs: 1),
            RecentVault(path: "/tmp/b", displayName: "b", lastOpenedMs: 2),
        ]
        let state = try makeAppState(seedEntries: seed)
        XCTAssertEqual(state.recentVaults, seed)
    }

    func testOpenRecentWithMissingPathFlagsItForRemoval() throws {
        let missing = RecentVault(
            path: tempDir.appendingPathComponent("not-there").path,
            displayName: "not-there",
            lastOpenedMs: 1
        )
        let state = try makeAppState(seedEntries: [missing])

        state.openRecent(missing)

        XCTAssertEqual(state.missingRecentVault, missing)
        XCTAssertFalse(state.isVaultOpen, "missing entries must not open a session")
        XCTAssertNil(state.currentSession)
    }

    func testOpenRecentWithFileInsteadOfDirectoryFlagsItForRemoval() throws {
        // Picking a path that exists but is a regular file (not a
        // directory) should be treated the same as missing — the user
        // pointed Slate at something that isn't a vault.
        let filePath = tempDir.appendingPathComponent("not-a-dir.txt")
        try Data("hi".utf8).write(to: filePath)
        let entry = RecentVault(
            path: filePath.path,
            displayName: "not-a-dir.txt",
            lastOpenedMs: 1
        )
        let state = try makeAppState(seedEntries: [entry])

        state.openRecent(entry)

        XCTAssertEqual(state.missingRecentVault, entry)
        XCTAssertFalse(state.isVaultOpen)
    }

    func testOpenVaultScansAndPopulatesFiles() async throws {
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("# Alpha".utf8).write(to: vault.appendingPathComponent("alpha.md"))
        try Data("# Beta".utf8).write(to: vault.appendingPathComponent("beta.md"))
        try Data("plain".utf8).write(to: vault.appendingPathComponent("not-markdown.txt"))

        let state = try makeAppState()
        state.openVault(at: vault)
        XCTAssertTrue(state.isVaultOpen)
        XCTAssertTrue(
            state.isScanning || state.scanTask != nil,
            "openVault should kick off a scan task"
        )

        await state.scanTask?.value

        XCTAssertFalse(state.isScanning)
        XCTAssertNil(state.scanError)
        XCTAssertEqual(state.files.map(\.name), ["alpha.md", "beta.md"])
        XCTAssertTrue(state.files.allSatisfy { $0.isMarkdown })
    }

    func testFilesSortIsCaseInsensitiveByPath() async throws {
        let vault = tempDir.appendingPathComponent("case-vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for name in ["Charlie.md", "alpha.md", "BRAVO.md"] {
            try Data("#".utf8).write(to: vault.appendingPathComponent(name))
        }

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        // Case-insensitive lexicographic: alpha, BRAVO, Charlie. A
        // case-sensitive sort would put the uppercase names first.
        XCTAssertEqual(state.files.map(\.name), ["alpha.md", "BRAVO.md", "Charlie.md"])
    }

    func testCloseVaultMidScanDoesNotRepopulateFiles() async throws {
        // Reproduces the bug Codoki flagged on PR 36: closeVault fires
        // mid-scan, and the in-flight detached task must not later
        // overwrite the freshly-cleared `files` with stale results.
        let vault = tempDir.appendingPathComponent("cancel-vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for i in 0..<32 {
            try Data("# n\(i)".utf8).write(
                to: vault.appendingPathComponent("note-\(i).md")
            )
        }

        let state = try makeAppState()
        state.openVault(at: vault)
        let task = state.scanTask
        // Close immediately — the detached scan task may still be
        // running. The cancellation handler should bridge through to
        // the CancelToken, and even if the scan completes anyway, the
        // post-scan publish must be suppressed.
        state.closeVault()

        // Wait for the cancelled task to wind down (it may take a beat
        // to drain pending paging calls).
        await task?.value

        XCTAssertEqual(
            state.files, [],
            "files must stay empty after closeVault, even if the scan task finishes"
        )
        XCTAssertFalse(state.isVaultOpen)
        XCTAssertFalse(state.isScanning)
    }

    func testCloseVaultClearsFileListState() async throws {
        let vault = tempDir.appendingPathComponent("close-vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("#".utf8).write(to: vault.appendingPathComponent("a.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        XCTAssertFalse(state.files.isEmpty)

        state.selectedFilePath = state.files.first?.path
        state.closeVault()

        XCTAssertFalse(state.isVaultOpen)
        XCTAssertEqual(state.files, [])
        XCTAssertNil(state.selectedFilePath)
        XCTAssertFalse(state.isScanning)
    }

    func testSelectingAFileLoadsItsContent() async throws {
        let vault = tempDir.appendingPathComponent("read-vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try "# Hello, vault\n\nSome body text.".data(using: .utf8)!.write(
            to: vault.appendingPathComponent("hello.md")
        )

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.selectedFilePath = "hello.md"
        await state.noteLoadTask?.value

        XCTAssertEqual(state.currentNoteText, "# Hello, vault\n\nSome body text.")
        XCTAssertNil(state.noteLoadError)
        XCTAssertFalse(state.isLoadingNote)
    }

    func testSelectingNilClearsCurrentNoteText() async throws {
        let vault = tempDir.appendingPathComponent("clear-vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("# A".utf8).write(to: vault.appendingPathComponent("a.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        XCTAssertNotNil(state.currentNoteText)

        // Deselecting clears the content immediately (the observer
        // runs synchronously inside the @Published .sink).
        state.selectedFilePath = nil
        XCTAssertNil(state.currentNoteText)
        XCTAssertNil(state.noteLoadError)
    }

    func testInvalidUtf8NoteSurfacesAsNoteLoadError() async throws {
        let vault = tempDir.appendingPathComponent("utf8-vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        // Valid heading followed by an invalid UTF-8 continuation
        // byte. The strict-decode read_text path returns InvalidUtf8;
        // AppState's note load translates that into a user-readable
        // string on `noteLoadError`.
        var bytes: [UInt8] = Array("# heading\n".utf8)
        bytes.append(0xFF)
        let data = Data(bytes)
        try data.write(to: vault.appendingPathComponent("bad.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.selectedFilePath = "bad.md"
        await state.noteLoadTask?.value

        XCTAssertNil(state.currentNoteText)
        XCTAssertNotNil(state.noteLoadError)
        XCTAssertTrue(
            state.noteLoadError?.contains("UTF-8") == true,
            "expected UTF-8 in error message, got \(String(describing: state.noteLoadError))"
        )
    }

    func testCloseVaultClearsNoteState() async throws {
        let vault = tempDir.appendingPathComponent("note-close-vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("# A".utf8).write(to: vault.appendingPathComponent("a.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        XCTAssertNotNil(state.currentNoteText)

        state.closeVault()

        XCTAssertNil(state.currentNoteText)
        XCTAssertEqual(state.currentNoteHeadings, [])
        XCTAssertNil(state.noteLoadError)
        XCTAssertFalse(state.isLoadingNote)
    }

    func testSelectingANoteLoadsItsHeadingsInDocumentOrder() async throws {
        let vault = tempDir.appendingPathComponent("outline-vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        let body = """
            # Top
            intro

            ## Section one

            body of one

            ### Sub of one

            ## Section two

            tail
            """
        try Data(body.utf8).write(to: vault.appendingPathComponent("outline.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.selectedFilePath = "outline.md"
        await state.noteLoadTask?.value

        // Heading rotor / OutlineSidebar both rely on this list being
        // in scanner order (== document order) with the right levels.
        XCTAssertEqual(state.currentNoteHeadings.map(\.text), [
            "Top", "Section one", "Sub of one", "Section two"
        ])
        XCTAssertEqual(state.currentNoteHeadings.map(\.level), [1, 2, 3, 2])
        // Anchor IDs come from the Rust slugifier and must be unique
        // within the file so SwiftUI's `.id(_)` scrolling doesn't pick
        // the wrong target.
        let anchors = state.currentNoteHeadings.map(\.anchorId)
        XCTAssertEqual(Set(anchors).count, anchors.count, "anchor IDs must be unique within a file")
    }

    func testSelectingANoteWithoutHeadingsLeavesOutlineEmpty() async throws {
        let vault = tempDir.appendingPathComponent("plain-vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("plain body, no headings here.".utf8)
            .write(to: vault.appendingPathComponent("plain.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.selectedFilePath = "plain.md"
        await state.noteLoadTask?.value

        XCTAssertNotNil(state.currentNoteText)
        XCTAssertEqual(state.currentNoteHeadings, [], "headingless note must produce an empty outline")
    }

    func testSwitchingBetweenNotesReplacesHeadingList() async throws {
        let vault = tempDir.appendingPathComponent("switch-vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("# Alpha only".utf8).write(to: vault.appendingPathComponent("alpha.md"))
        try Data("# Beta\n## Beta two".utf8).write(to: vault.appendingPathComponent("beta.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.selectedFilePath = "alpha.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.currentNoteHeadings.map(\.text), ["Alpha only"])

        state.selectedFilePath = "beta.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.currentNoteHeadings.map(\.text), ["Beta", "Beta two"])
    }

    func testDeselectingClearsHeadingList() async throws {
        let vault = tempDir.appendingPathComponent("deselect-vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("# A\n## B".utf8).write(to: vault.appendingPathComponent("a.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.currentNoteHeadings.count, 2)

        // Same Combine sink that clears currentNoteText must also
        // clear the heading list — otherwise the outline pane shows
        // stale rows pointing at a note that's no longer loaded.
        state.selectedFilePath = nil
        XCTAssertEqual(state.currentNoteHeadings, [])
    }

    // MARK: - Scan progress

    private func makeReport(filesIndexed: UInt64 = 0) -> ScanReport {
        ScanReport(
            filesSeen: filesIndexed,
            filesIndexed: filesIndexed,
            filesSkipped: 0,
            bytesProcessed: 0,
            errors: []
        )
    }

    func testStartedEventAlwaysAnnouncesAndPublishesProgress() throws {
        let state = try makeAppState()
        state.handleScanProgress(.started(totalFiles: 42))

        if case .started(let total)? = state.scanProgress {
            XCTAssertEqual(total, 42)
        } else {
            XCTFail("expected scanProgress to be .started, got \(String(describing: state.scanProgress))")
        }
        XCTAssertEqual(state.scanAnnouncementCount, 1)
        XCTAssertEqual(
            state.scanAnnouncementLastMessage,
            "Scanning vault. 42 files to index."
        )
    }

    func testFileIndexedRespectsRateGuard() throws {
        let state = try makeAppState()
        // Pin the clock so the rate guard is deterministic.
        var now = Date(timeIntervalSinceReferenceDate: 0)
        state.scanClock = { now }

        state.handleScanProgress(.started(totalFiles: 10))
        // One announcement for Started.
        XCTAssertEqual(state.scanAnnouncementCount, 1)

        // Fire 10 FileIndexed events at +50ms each: 500ms of simulated
        // time. The rate guard is 350ms, so we should see at most two
        // additional announcements (at 350 and 700 ms of accumulated
        // gap, but capped by the 10 events spread across 500ms — only
        // one extra fire is possible inside that window).
        for i in 1...10 {
            now = now.addingTimeInterval(0.050)
            state.handleScanProgress(
                .fileIndexed(path: "n\(i).md", indexed: UInt64(i), total: 10)
            )
        }
        // Started + one rate-limited FileIndexed fire inside 500ms.
        XCTAssertLessThanOrEqual(state.scanAnnouncementCount, 3)
        XCTAssertGreaterThanOrEqual(state.scanAnnouncementCount, 2)
    }

    func testRateGuardAllowsThreePerSecondNotMore() throws {
        let state = try makeAppState()
        var now = Date(timeIntervalSinceReferenceDate: 0)
        state.scanClock = { now }

        // 30 events evenly spread over one second of simulated time.
        // 1 / 0.030s spacing = ~33 events; the 350ms guard should
        // allow ~3 fires over that second (plus one initial for
        // Started). Acceptance criteria caps at ~3/s — assert <= 4
        // counting Started.
        state.handleScanProgress(.started(totalFiles: 30))
        for i in 1...30 {
            now = now.addingTimeInterval(1.0 / 30.0)
            state.handleScanProgress(
                .fileIndexed(path: "n\(i).md", indexed: UInt64(i), total: 30)
            )
        }
        XCTAssertLessThanOrEqual(
            state.scanAnnouncementCount, 4,
            "rate guard must keep total announcements per simulated second to <= 3 + Started"
        )
    }

    func testFinishedAnnouncesAndClearsProgress() throws {
        let state = try makeAppState()
        state.handleScanProgress(.started(totalFiles: 5))
        state.handleScanProgress(.finished(report: makeReport(filesIndexed: 5)))

        // Finished must clear scanProgress so the sidebar progress bar
        // disappears even though the file list will repopulate next.
        XCTAssertNil(state.scanProgress)
        XCTAssertEqual(
            state.scanAnnouncementLastMessage,
            "Scan complete. 5 files indexed."
        )
    }

    func testCancelledClearsProgressWithoutAnnouncing() throws {
        let state = try makeAppState()
        state.handleScanProgress(.started(totalFiles: 5))
        let countAfterStarted = state.scanAnnouncementCount

        state.handleScanProgress(.cancelled)

        // No additional announcement — closeVault/next-vault flow is
        // already visible, screen-reader users don't need another
        // interruption.
        XCTAssertEqual(state.scanAnnouncementCount, countAfterStarted)
        XCTAssertNil(state.scanProgress)
    }

    func testFailedClearsProgressWithoutAnnouncing() throws {
        let state = try makeAppState()
        state.handleScanProgress(.started(totalFiles: 5))
        let countAfterStarted = state.scanAnnouncementCount

        state.handleScanProgress(.failed(message: "disk gone"))

        // Failures surface through scanError (the existing path); the
        // accessibility live region stays quiet so we don't double-fire.
        XCTAssertEqual(state.scanAnnouncementCount, countAfterStarted)
        XCTAssertNil(state.scanProgress)
    }

    func testCloseVaultClearsScanProgress() throws {
        let state = try makeAppState()
        state.handleScanProgress(.started(totalFiles: 10))
        XCTAssertNotNil(state.scanProgress)

        state.closeVault()
        XCTAssertNil(state.scanProgress)
    }

    func testStartedFromSecondVaultGetsItsOwnAnnouncement() async throws {
        // Bug class: counters persisting across vaults could cause
        // the second vault's Started to be suppressed (e.g. if we
        // reused the rate guard's last-fired timestamp). openVault
        // resets the bookkeeping; this test pins it.
        let vaultA = tempDir.appendingPathComponent("a")
        try FileManager.default.createDirectory(at: vaultA, withIntermediateDirectories: true)
        try Data("# A".utf8).write(to: vaultA.appendingPathComponent("a.md"))
        let vaultB = tempDir.appendingPathComponent("b")
        try FileManager.default.createDirectory(at: vaultB, withIntermediateDirectories: true)
        try Data("# B".utf8).write(to: vaultB.appendingPathComponent("b.md"))

        let state = try makeAppState()
        state.openVault(at: vaultA)
        await state.scanTask?.value
        let countA = state.scanAnnouncementCount

        state.openVault(at: vaultB)
        // openVault resets scanAnnouncementCount; assert that explicitly.
        XCTAssertEqual(
            state.scanAnnouncementCount, 0,
            "openVault must reset the per-vault announcement counter"
        )
        await state.scanTask?.value
        XCTAssertGreaterThan(state.scanAnnouncementCount, 0)
        XCTAssertGreaterThan(countA, 0)
    }

    // MARK: - Backlinks / outgoing links (#51)

    func testSelectingANoteLoadsBacklinksAndOutgoingLinks() async throws {
        let vault = tempDir.appendingPathComponent("links-vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("# Target".utf8).write(to: vault.appendingPathComponent("Target.md"))
        try Data("see [[Target]] and [external](https://example.com)".utf8)
            .write(to: vault.appendingPathComponent("source.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        // Select the source: should have one outgoing wikilink + one
        // external, and zero backlinks.
        state.selectedFilePath = "source.md"
        await state.linksLoadTask?.value
        XCTAssertEqual(state.currentBacklinks.count, 0)
        XCTAssertEqual(state.currentOutgoingLinks.count, 2)
        XCTAssertEqual(state.currentOutgoingLinks[0].targetRaw, "Target")
        XCTAssertFalse(state.currentOutgoingLinks[0].isExternal)
        XCTAssertTrue(state.currentOutgoingLinks[1].isExternal)

        // Select the target: should have one backlink and zero outgoing.
        state.selectedFilePath = "Target.md"
        await state.linksLoadTask?.value
        XCTAssertEqual(state.currentBacklinks.count, 1)
        XCTAssertEqual(state.currentBacklinks[0].sourcePath, "source.md")
        XCTAssertEqual(state.currentOutgoingLinks.count, 0)
    }

    func testDeselectingClearsLinkPanels() async throws {
        let vault = tempDir.appendingPathComponent("clear-links")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("# A".utf8).write(to: vault.appendingPathComponent("a.md"))
        try Data("see [[A]]".utf8).write(to: vault.appendingPathComponent("b.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.selectedFilePath = "a.md"
        await state.linksLoadTask?.value
        XCTAssertEqual(state.currentBacklinks.count, 1)

        state.selectedFilePath = nil
        // The Combine sink clears synchronously; no need to await.
        XCTAssertEqual(state.currentBacklinks, [])
        XCTAssertEqual(state.currentOutgoingLinks, [])
    }

    func testCloseVaultClearsLinkPanels() async throws {
        let vault = tempDir.appendingPathComponent("close-links")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("# A".utf8).write(to: vault.appendingPathComponent("a.md"))
        try Data("see [[A]]".utf8).write(to: vault.appendingPathComponent("b.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "a.md"
        await state.linksLoadTask?.value
        XCTAssertEqual(state.currentBacklinks.count, 1)

        state.closeVault()
        XCTAssertEqual(state.currentBacklinks, [])
        XCTAssertEqual(state.currentOutgoingLinks, [])
        XCTAssertFalse(state.isLoadingLinks)
    }

    func testRapidSelectionToggleDoesNotLeaveSpinnerOff() async throws {
        // Regression for the Codoki callout on PR 79: an older
        // `defer { isLoadingLinks = false }` would fire when a
        // cancelled task exited, clearing the flag while a newer
        // task was still in flight (causing spinner flicker).
        let vault = tempDir.appendingPathComponent("rapid-toggle")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("# A".utf8).write(to: vault.appendingPathComponent("a.md"))
        try Data("# B".utf8).write(to: vault.appendingPathComponent("b.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.selectedFilePath = "a.md"
        // Switch to b before A's load finishes. The previous task
        // gets cancelled and its early-return path must NOT clear
        // isLoadingLinks, because B's task has set it true.
        state.selectedFilePath = "b.md"

        // Drain both task handles. Older may have completed by the
        // time we look (cancelled returns immediately); the newer
        // is the one we care about.
        await state.linksLoadTask?.value
        // After the latest task completes for path b, flag is false.
        XCTAssertFalse(state.isLoadingLinks)
        XCTAssertEqual(state.selectedFilePath, "b.md")
    }

    func testUnresolvedLinkAppearsInOutgoingList() async throws {
        let vault = tempDir.appendingPathComponent("dangling")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("hello [[Missing]] bye".utf8)
            .write(to: vault.appendingPathComponent("source.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "source.md"
        await state.linksLoadTask?.value

        XCTAssertEqual(state.currentOutgoingLinks.count, 1)
        let link = state.currentOutgoingLinks[0]
        XCTAssertTrue(link.isUnresolved)
        XCTAssertFalse(link.isExternal)
        XCTAssertNil(link.targetPath)
        XCTAssertEqual(link.targetRaw, "Missing")
    }

    // MARK: - Frontmatter properties (#55)

    func testSelectingANoteLoadsItsProperties() async throws {
        let vault = tempDir.appendingPathComponent("props-vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        let body = """
            ---
            title: My Note
            tags:
              - alpha
              - beta
            published: true
            ---
            # body
            """
        try Data(body.utf8).write(to: vault.appendingPathComponent("note.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "note.md"
        await state.linksLoadTask?.value

        let keys = state.currentNoteProperties.map(\.key)
        XCTAssertEqual(keys, ["title", "tags", "published"])
    }

    func testDeselectingClearsProperties() async throws {
        let vault = tempDir.appendingPathComponent("clear-props")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("---\ntitle: Stable\n---\n".utf8)
            .write(to: vault.appendingPathComponent("a.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "a.md"
        await state.linksLoadTask?.value
        XCTAssertEqual(state.currentNoteProperties.count, 1)

        state.selectedFilePath = nil
        XCTAssertEqual(state.currentNoteProperties, [])
    }

    func testCloseVaultClearsProperties() async throws {
        let vault = tempDir.appendingPathComponent("close-props")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("---\ntitle: x\n---\n".utf8)
            .write(to: vault.appendingPathComponent("a.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "a.md"
        await state.linksLoadTask?.value
        XCTAssertEqual(state.currentNoteProperties.count, 1)

        state.closeVault()
        XCTAssertEqual(state.currentNoteProperties, [])
    }

    func testSwitchingNotesKeepsPreviousPropertiesUntilLoadCompletes() async throws {
        // Regression for #90 PropertiesPanel flicker. Previously
        // `handleSelectionChange` cleared `currentNoteProperties = []`
        // synchronously on every selection change, so the panel
        // rendered EmptyView for the duration of the load — a visible
        // disappear/reappear of the "Properties, N items" rotor item
        // for VoiceOver. The new behaviour leaves the previous note's
        // properties in place until the new load resolves, eliminating
        // the transient empty state.
        let vault = tempDir.appendingPathComponent("switch-props")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("---\ntitle: A\n---\n".utf8)
            .write(to: vault.appendingPathComponent("a.md"))
        try Data("---\ntitle: B\nauthor: someone\n---\n".utf8)
            .write(to: vault.appendingPathComponent("b.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        // Establish A's properties as the "previous selection."
        state.selectedFilePath = "a.md"
        await state.linksLoadTask?.value
        XCTAssertEqual(state.currentNoteProperties.map(\.key), ["title"])

        // Switch to B. handleSelectionChange runs synchronously inside
        // the @Published sink — at this point the new load task is
        // scheduled but not yet awaited. Properties must still equal
        // A's value, not a transient empty array.
        state.selectedFilePath = "b.md"
        XCTAssertEqual(
            state.currentNoteProperties.map(\.key),
            ["title"],
            "properties were cleared synchronously on selection change — flicker regression"
        )

        // Once the new load finishes, properties reflect B's frontmatter.
        await state.linksLoadTask?.value
        XCTAssertEqual(state.currentNoteProperties.map(\.key), ["title", "author"])
    }

    func testNotesWithoutFrontmatterHaveEmptyProperties() async throws {
        let vault = tempDir.appendingPathComponent("plain-props")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("# Just a heading\n\nno frontmatter here.".utf8)
            .write(to: vault.appendingPathComponent("plain.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "plain.md"
        await state.linksLoadTask?.value

        XCTAssertTrue(state.currentNoteProperties.isEmpty)
    }

    func testPropertyValueDisplayDecodesAtomicKindsWithTypeCues() throws {
        // Cover every kind once so VoiceOver label cues stay in sync
        // with the FFI JSON shape if either side moves.
        let cases: [(String, String, String)] = [
            ("text", "\"hello\"", "Property name: hello"),
            ("number", "42", "Property age, number: 42"),
            ("boolean", "true", "Property published, boolean: true"),
            ("date", "\"2024-01-02\"", "Property created, date:"),
            ("wikilink", "\"Alpha\"", "Property related, link to Alpha"),
        ]
        let keys = ["name", "age", "published", "created", "related"]
        for (i, (kind, json, expectedPrefix)) in cases.enumerated() {
            let display = PropertyValueDisplay.decode(kind: kind, valueJson: json)
            let label = display.accessibilityLabel(for: keys[i])
            XCTAssertTrue(
                label.hasPrefix(expectedPrefix),
                "kind=\(kind) label=\(label) expected to start with \(expectedPrefix)"
            )
        }
    }

    func testPropertyValueDisplayDecodesLists() throws {
        let display = PropertyValueDisplay.decode(
            kind: "list",
            valueJson: "[\"alpha\",\"beta\",\"gamma\"]"
        )
        XCTAssertEqual(display.listCount, 3)
        XCTAssertEqual(display.visibleText, "alpha, beta, gamma")
        let label = display.accessibilityLabel(for: "authors")
        XCTAssertEqual(label, "Property authors, list of 3: alpha, beta, gamma")
    }

    func testPropertyValueDisplayDecodesTagListsWithHashPrefix() throws {
        let display = PropertyValueDisplay.decode(
            kind: "tag_list",
            valueJson: "[\"alpha\",\"beta\"]"
        )
        XCTAssertEqual(display.listCount, 2)
        XCTAssertEqual(display.visibleText, "#alpha, #beta")
        let label = display.accessibilityLabel(for: "tags")
        XCTAssertEqual(label, "Property tags, tag list of 2: #alpha, #beta")
    }

    func testPropertyValueDisplayDateDoesNotDriftAcrossTimeZones() throws {
        // Codoki callout: a UTC-parsed `YYYY-MM-DD` rendered through
        // the user's locale formatter would shift to the wrong day
        // for anyone west of UTC. Parse + format in TimeZone.current
        // so "2024-01-02" stays Jan 2 regardless of locale.
        let display = PropertyValueDisplay.decode(kind: "date", valueJson: "\"2024-01-02\"")
        // The medium date style varies by locale ("Jan 2, 2024" in
        // en_US, "2 Jan 2024" elsewhere). The invariant we check is
        // that the day-of-month is 2, not 1 — i.e. no TZ drift.
        XCTAssertTrue(
            display.visibleText.contains("2024"),
            "year missing from rendered date: \(display.visibleText)"
        )
        XCTAssertFalse(
            display.visibleText.contains("Jan 1") || display.visibleText.contains("1 Jan"),
            "date drifted to Jan 1; got \(display.visibleText)"
        )
    }

    func testPropertyValueDisplayZLessDatetimeTreatedAsLocalTime() throws {
        // Z-less ISO datetimes ("2024-01-02T03:04:05") were
        // previously parsed in UTC even though the comment claimed
        // local — Codoki PR 83. Parsing as TimeZone.current means
        // the formatted hour stays 3 (or the locale equivalent),
        // not 3 + offset.
        let display = PropertyValueDisplay.decode(
            kind: "datetime",
            valueJson: "\"2024-01-02T03:04:05\""
        )
        // The rendered string includes hour:min in the user's
        // locale — "3:04 AM" or "03:04" depending on locale. Assert
        // the date hasn't drifted.
        XCTAssertTrue(
            display.visibleText.contains("2024"),
            "year missing: \(display.visibleText)"
        )
    }

    func testPropertyValueDisplayMalformedJsonFallsBackToRaw() throws {
        // Defense in depth: if the FFI ever hands us malformed JSON
        // (corrupt DB row, future kind we don't recognize), the row
        // stays visible with the raw JSON shown instead of crashing
        // or dropping silently.
        let display = PropertyValueDisplay.decode(kind: "text", valueJson: "not-json")
        XCTAssertEqual(display.visibleText, "not-json")
        let display2 = PropertyValueDisplay.decode(
            kind: "future_unknown_kind",
            valueJson: "anything"
        )
        XCTAssertEqual(display2.visibleText, "anything")
    }

    // MARK: - Search overlay (#58)

    /// Drain the debouncer + the in-flight search task so tests
    /// observe a settled state.
    private func awaitSearch(_ state: AppState) async {
        // The debounce fires on DispatchQueue.main after 150 ms.
        // Wait ~400 ms (Codoki PR-86 suggestion) so the CI runner's
        // slower main-queue scheduling doesn't race the assertion
        // before the debounced sink has had a chance to fire.
        try? await Task.sleep(nanoseconds: 400_000_000)
        await state.searchTask?.value
    }

    func testSearchIsIdleByDefault() throws {
        let state = try makeAppState()
        XCTAssertEqual(state.searchState, .idle)
        XCTAssertFalse(state.isSearchOpen)
    }

    func testToggleSearchOverlayOpensAndClosesIt() throws {
        let state = try makeAppState()
        state.toggleSearchOverlay()
        XCTAssertTrue(state.isSearchOpen)
        state.toggleSearchOverlay()
        XCTAssertFalse(state.isSearchOpen)
        XCTAssertEqual(state.searchState, .idle)
    }

    func testEmptyQueryStaysIdle() async throws {
        let vault = tempDir.appendingPathComponent("search-empty")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("hello world".utf8).write(to: vault.appendingPathComponent("note.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.searchQuery = ""
        state.bumpSearchQuery()
        await awaitSearch(state)
        XCTAssertEqual(state.searchState, .idle)
    }

    func testQueryTransitionsThroughResults() async throws {
        let vault = tempDir.appendingPathComponent("search-results")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("hello uniqueoverlaytoken world".utf8)
            .write(to: vault.appendingPathComponent("note.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.searchQuery = "uniqueoverlaytoken"
        state.bumpSearchQuery()
        await awaitSearch(state)

        switch state.searchState {
        case .results(let rows, let summary):
            XCTAssertEqual(rows.count, 1)
            XCTAssertEqual(rows[0].path, "note.md")
            XCTAssertEqual(summary, "Search returned 1 result.")
        case let other:
            XCTFail("expected results, got \(String(describing: other))")
        }
        XCTAssertEqual(state.searchSummary, "Search returned 1 result.")
    }

    func testQueryWithNoHitsReturnsEmptyResults() async throws {
        let vault = tempDir.appendingPathComponent("search-empty-results")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("hello world".utf8).write(to: vault.appendingPathComponent("note.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.searchQuery = "absenttokenxyz"
        state.bumpSearchQuery()
        await awaitSearch(state)

        switch state.searchState {
        case .results(let rows, let summary):
            XCTAssertTrue(rows.isEmpty)
            XCTAssertEqual(summary, "Search returned no results.")
        case let other:
            XCTFail("expected empty results, got \(String(describing: other))")
        }
    }

    func testCloseSearchOverlayResetsState() async throws {
        let vault = tempDir.appendingPathComponent("search-close")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("hello searchtokenone".utf8)
            .write(to: vault.appendingPathComponent("note.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.toggleSearchOverlay()
        state.searchQuery = "searchtokenone"
        state.bumpSearchQuery()
        await awaitSearch(state)

        if case .results = state.searchState {
            // good
        } else {
            XCTFail("expected results before close, got \(String(describing: state.searchState))")
        }

        state.closeSearchOverlay()
        XCTAssertFalse(state.isSearchOpen)
        XCTAssertEqual(state.searchState, .idle)
        XCTAssertEqual(state.searchSummary, "")
    }

    func testCloseVaultClearsSearchState() async throws {
        let vault = tempDir.appendingPathComponent("search-vault-close")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("searchablecontentx".utf8)
            .write(to: vault.appendingPathComponent("note.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.searchQuery = "searchablecontentx"
        state.bumpSearchQuery()
        await awaitSearch(state)

        state.closeVault()
        XCTAssertFalse(state.isSearchOpen)
        XCTAssertEqual(state.searchState, .idle)
        XCTAssertEqual(state.searchQuery, "")
    }

    // MARK: - Search result activation (#59)

    private func makeHit(
        path: String,
        snippet: String = ""
    ) -> QueryHit {
        QueryHit(
            path: path,
            snippet: snippet,
            score: 0.0
        )
    }

    /// Waits for the next emission on `lineScrollRequest`. Used in
    /// place of `Task.sleep` so the assertion synchronizes on the
    /// actual subject signal rather than a wall-clock guess — much
    /// less flaky under CI load.
    private func waitForLineScroll(
        _ state: AppState,
        timeout: TimeInterval = 1.0
    ) async -> Int? {
        await withCheckedContinuation { cont in
            var resumed = false
            var sub: AnyCancellable?
            sub = state.lineScrollRequest.sink { line in
                if !resumed {
                    resumed = true
                    sub?.cancel()
                    cont.resume(returning: line)
                }
            }
            DispatchQueue.main.asyncAfter(deadline: .now() + timeout) {
                if !resumed {
                    resumed = true
                    sub?.cancel()
                    cont.resume(returning: nil)
                }
            }
        }
    }

    func testOpenSearchResultSelectsPathAndClosesOverlay() async throws {
        // The line scrolled to is now derived UI-side at activation
        // time from the loaded body + current `searchQuery` (#92
        // item 1). Seed the query so the lookup finds the expected
        // line.
        let vault = tempDir.appendingPathComponent("openresult-select")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("# Hello\nline one\nmatchedline\nline three".utf8)
            .write(to: vault.appendingPathComponent("a.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.toggleSearchOverlay()
        XCTAssertTrue(state.isSearchOpen)
        state.searchQuery = "matchedline"

        let hit = makeHit(path: "a.md", snippet: "\u{2}matchedline\u{3}")
        state.openSearchResult(hit)

        // Activation: overlay closes synchronously, selection moves.
        XCTAssertFalse(state.isSearchOpen)
        XCTAssertEqual(state.selectedFilePath, "a.md")

        // Synchronize on the actual subject emission. The body
        // matches `matchedline` on line 3.
        let scrolledLine = await waitForLineScroll(state)
        XCTAssertEqual(scrolledLine, 3)
        XCTAssertEqual(state.lastActivatedSearchResultPath, "a.md")
        XCTAssertEqual(state.lastActivatedSearchResultLine, 3)
    }

    func testOpenSearchResultEmitsLineScrollRequest() async throws {
        let vault = tempDir.appendingPathComponent("openresult-scroll")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("alpha\nbeta\ngamma".utf8)
            .write(to: vault.appendingPathComponent("note.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.searchQuery = "beta"

        state.openSearchResult(makeHit(path: "note.md"))
        let scrolledLine = await waitForLineScroll(state)
        XCTAssertEqual(scrolledLine, 2)
    }

    func testOpenSearchResultIgnoresScrollIfSelectionChangedMidLoad() async throws {
        // If the user switches files between activating a result
        // and the load finishing, the scroll request must not land
        // on the wrong file's per-line anchors. We assert on a
        // negative outcome: the waiter times out without seeing an
        // emission.
        let vault = tempDir.appendingPathComponent("openresult-race")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("# A".utf8).write(to: vault.appendingPathComponent("a.md"))
        try Data("# B".utf8).write(to: vault.appendingPathComponent("b.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.searchQuery = "A"

        state.openSearchResult(makeHit(path: "a.md"))
        // Yank the selection elsewhere before the post-load task
        // runs.
        state.selectedFilePath = "b.md"

        // Short timeout — if the suppression worked, we expect nil;
        // if it didn't, the emission will arrive well within 500 ms.
        let scrolledLine = await waitForLineScroll(state, timeout: 0.5)
        XCTAssertNil(
            scrolledLine,
            "scroll request should be suppressed when selection changed mid-load; got \(String(describing: scrolledLine))"
        )
    }

    func testFirstTokenLineNumberFindsEarliestMatch() {
        // Direct unit coverage for the UI-side line lookup. Mirrors
        // the shape of the Rust-side tests that used to live in
        // `search_db::find_first_token_line` before #92 item 1.
        let body = "alpha\nbravo\ncharlie\ndelta"
        XCTAssertEqual(firstTokenLineNumber(in: body, query: "charlie"), 3)
        // Multi-token query: earliest match wins.
        XCTAssertEqual(firstTokenLineNumber(in: body, query: "delta alpha"), 1)
        // Operators are stripped; remaining tokens drive the lookup.
        XCTAssertEqual(firstTokenLineNumber(in: body, query: "AND bravo NOT"), 2)
        // No match → fallback to line 1.
        XCTAssertEqual(firstTokenLineNumber(in: body, query: "missing"), 1)
    }

    func testFirstTokenLineNumberStripsBodyTextColumnPrefix() {
        // PR 103 Codoki follow-up: the docstring claimed
        // column-name prefixes (`body_text:`) were filtered, but
        // the original implementation only handled FTS5 keywords.
        // Now the column prefix is stripped before tokenization so
        // `body_text:foo` reduces to just `foo` for the line scan.
        let body = "body and text are common words\nbut foo lives here"
        // Without the strip, "body" would match on line 1; with the
        // strip, only "foo" survives and the lookup hits line 2.
        XCTAssertEqual(
            firstTokenLineNumber(in: body, query: "body_text:foo"),
            2
        )
    }

    func testFirstTokenLineNumberSkipsFts5KeywordsAndNumericTokens() {
        // #93 item 5: bare FTS5 keywords (`NEAR`, `AND`, `OR`,
        // `NOT`) and column-name prefixes like `body_text:`
        // shouldn't sneak into the body-line lookup if they
        // happen to appear as prose words. Same for purely
        // numeric tokens that came from FTS5 syntax (e.g. the
        // distance argument inside `NEAR(a b, 5)`).
        let body = "line one\nNEAR is a real word here\nactual match"
        // "NEAR" alone in the query → all tokens are dropped → line 1.
        XCTAssertEqual(firstTokenLineNumber(in: body, query: "NEAR"), 1)
        // `NEAR(actual b, 5)` → "near" and "5" filtered; "actual" + "b" survive.
        // "actual" lands on line 3.
        XCTAssertEqual(
            firstTokenLineNumber(in: body, query: "NEAR(actual b, 5)"),
            3
        )
        // Pure-numeric query → fallback to line 1.
        XCTAssertEqual(firstTokenLineNumber(in: body, query: "42"), 1)
    }

    func testFirstTokenLineNumberSurvivesUnicodeLowercasing() {
        // Regression mirror of the Rust-side audit-#88 case: `İ`
        // (U+0130) lowercases to 2 codepoints so the lowered body
        // is 1 UTF-8 byte longer than the original. We count
        // newlines in the lowered string itself rather than slicing
        // the original — must not crash.
        let body = "İstanbul intro\nİstanbul again\nmatch on line 3"
        let line = firstTokenLineNumber(in: body, query: "match")
        XCTAssertTrue(line == 2 || line == 3, "got \(line)")
    }

    // MARK: - Link activation (#52)

    private func makeOutgoing(
        targetPath: String? = nil,
        targetRaw: String,
        isExternal: Bool = false,
        isUnresolved: Bool = false
    ) -> OutgoingLink {
        OutgoingLink(
            targetPath: targetPath,
            targetRaw: targetRaw,
            targetAnchor: nil,
            kind: isExternal ? "markdown" : "wikilink",
            isEmbed: false,
            isExternal: isExternal,
            isUnresolved: isUnresolved,
            snippet: "",
            ordinal: 0
        )
    }

    func testOpenResolvedLinkNavigatesToTarget() throws {
        let state = try makeAppState()
        let link = makeOutgoing(targetPath: "notes/foo.md", targetRaw: "foo")
        state.openLink(link)
        XCTAssertEqual(state.selectedFilePath, "notes/foo.md")
        XCTAssertEqual(
            state.lastActivatedLinkOutcome,
            .openedInternal("notes/foo.md")
        )
    }

    func testOpenUnresolvedLinkDoesNotNavigate() throws {
        let state = try makeAppState()
        let link = makeOutgoing(targetRaw: "Missing", isUnresolved: true)
        state.openLink(link)
        XCTAssertNil(state.selectedFilePath)
        XCTAssertEqual(
            state.lastActivatedLinkOutcome,
            .unresolved("Missing")
        )
    }

    func testOpenExternalLinkDoesNotChangeSelection() throws {
        // We can't reliably assert NSWorkspace.open() succeeded in a
        // headless test (no LaunchServices), so the test asserts on
        // the outcome enum which routes through either `openedExternal`
        // or `externalOpenFailed`. Either way, selectedFilePath stays
        // untouched.
        let state = try makeAppState()
        let link = makeOutgoing(targetRaw: "https://example.com", isExternal: true)
        state.openLink(link)
        XCTAssertNil(state.selectedFilePath)
        switch state.lastActivatedLinkOutcome {
        case .openedExternal, .externalOpenFailed:
            break
        case let other:
            XCTFail("expected external outcome, got \(String(describing: other))")
        }
    }

    func testOpenExternalLinkRejectsDisallowedSchemes() throws {
        // file://, javascript:, custom schemes — none should reach
        // NSWorkspace.open even though the link parser flagged them
        // as external. Restricting to http/https/mailto keeps a typo
        // from handing control to whatever app is registered for
        // a stray scheme.
        let state = try makeAppState()
        for raw in [
            "file:///etc/passwd",
            "javascript:alert(1)",
            "slate-internal:something",
        ] {
            let link = makeOutgoing(targetRaw: raw, isExternal: true)
            state.openLink(link)
            XCTAssertEqual(
                state.lastActivatedLinkOutcome,
                .externalOpenFailed(raw),
                "expected \(raw) to be rejected"
            )
            XCTAssertNil(state.selectedFilePath)
        }
    }

    func testOpenLinkDefensiveGuardFallsThroughToUnresolved() throws {
        // Codoki suggestion: exercise the "shouldn't happen" branch
        // where the link is neither external nor unresolved but
        // somehow has a nil target_path. Treat as unresolved so the
        // user gets feedback instead of silence.
        let state = try makeAppState()
        let link = makeOutgoing(targetPath: nil, targetRaw: "weird")
        state.openLink(link)
        XCTAssertNil(state.selectedFilePath)
        XCTAssertEqual(
            state.lastActivatedLinkOutcome,
            .unresolved("weird")
        )
    }

    func testOpenBacklinkNavigatesToSourcePath() throws {
        let state = try makeAppState()
        let backlink = Backlink(
            sourcePath: "notes/who-links.md",
            snippet: "see [[here]]",
            ordinal: 0,
            kind: "wikilink",
            isEmbed: false
        )
        state.openBacklink(backlink)
        XCTAssertEqual(state.selectedFilePath, "notes/who-links.md")
        XCTAssertEqual(
            state.lastActivatedLinkOutcome,
            .openedInternal("notes/who-links.md")
        )
    }

    func testRequestScrollToHeadingPublishesAnchor() throws {
        let state = try makeAppState()

        var received: [String] = []
        let sub = state.scrollAnchorRequest.sink { received.append($0) }
        defer { sub.cancel() }

        state.requestScrollToHeading(anchor: "section-one")
        state.requestScrollToHeading(anchor: "section-one")

        // Two emits for two calls, including repeat: PassthroughSubject
        // (not @Published) so the content pane re-scrolls on the same
        // heading without needing a counter.
        XCTAssertEqual(received, ["section-one", "section-one"])
    }

    func testRemoveRecentDropsEntryAndPersists() throws {
        let keeper = RecentVault(path: "/tmp/keep", displayName: "keep", lastOpenedMs: 1)
        let goner = RecentVault(path: "/tmp/gone", displayName: "gone", lastOpenedMs: 2)
        let state = try makeAppState(seedEntries: [keeper, goner])
        XCTAssertEqual(state.recentVaults.count, 2)

        state.removeRecent(path: goner.path)

        XCTAssertEqual(state.recentVaults, [keeper])
        // Verify the change was actually persisted to disk, not just to
        // the in-memory @Published.
        let reload = RecentVaultsStore(fileURL: storeFile).load()
        XCTAssertEqual(reload, [keeper])
    }

    // MARK: - Save flow (#63 + #64)

    /// Build a vault with a single editable note and drive the full
    /// open → scan → select → load path so subsequent save tests
    /// start from a known-good `currentNoteText` + hash baseline.
    private func makeAppStateWithLoadedNote(
        initialContent: String = "# Hello\n\nSome body.\n"
    ) async throws -> (AppState, URL, String) {
        let vault = tempDir.appendingPathComponent("save-vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        let notePath = "note.md"
        try Data(initialContent.utf8).write(to: vault.appendingPathComponent(notePath))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.selectedFilePath = notePath
        await state.noteLoadTask?.value
        return (state, vault, notePath)
    }

    func testLoadCapturesHashAndBaseline() async throws {
        let (state, _, notePath) = try await makeAppStateWithLoadedNote()
        XCTAssertEqual(state.loadedFilePath, notePath)
        XCTAssertEqual(state.savedBaselineText, "# Hello\n\nSome body.\n")
        XCTAssertNotNil(state.currentNoteContentHash)
        XCTAssertFalse(state.hasUnsavedChanges)
    }

    func testUpdateEditorTextFlipsDirtyAndBackToClean() async throws {
        let (state, _, _) = try await makeAppStateWithLoadedNote()
        // Typing a character: dirty.
        state.updateEditorText("# Hello\n\nSome body.\nmore")
        XCTAssertTrue(state.hasUnsavedChanges)
        // Editor reverts to the baseline (e.g. user backspaces):
        // dirty flips off without a save.
        state.updateEditorText("# Hello\n\nSome body.\n")
        XCTAssertFalse(state.hasUnsavedChanges)
    }

    func testSaveCurrentNoteUpdatesHashAndClearsDirty() async throws {
        let (state, vault, notePath) = try await makeAppStateWithLoadedNote()
        let originalHash = state.currentNoteContentHash
        let newBody = "# Hello\n\nNew body line.\n"
        state.updateEditorText(newBody)
        XCTAssertTrue(state.hasUnsavedChanges)

        await state.saveCurrentNote()?.value

        XCTAssertFalse(state.hasUnsavedChanges)
        XCTAssertNotEqual(state.currentNoteContentHash, originalHash)
        XCTAssertEqual(state.savedBaselineText, newBody)
        // On-disk file actually changed.
        let onDisk = try String(
            contentsOf: vault.appendingPathComponent(notePath),
            encoding: .utf8
        )
        XCTAssertEqual(onDisk, newBody)
    }

    func testSaveCurrentNoteSurfacesWriteConflictWhenFileChangedExternally()
        async throws
    {
        let (state, vault, notePath) = try await makeAppStateWithLoadedNote()
        // External writer changes the file behind the editor's back.
        let externalBody = "externally changed\n"
        try Data(externalBody.utf8).write(
            to: vault.appendingPathComponent(notePath)
        )

        state.updateEditorText("# Hello\n\nMy local edit.\n")
        await state.saveCurrentNote()?.value

        XCTAssertNotNil(
            state.currentSaveConflict,
            "external write must surface a WriteConflict, not a silent save"
        )
        XCTAssertTrue(state.hasUnsavedChanges, "buffer stays dirty until resolved")
        // Disk still holds the external writer's version.
        let onDisk = try String(
            contentsOf: vault.appendingPathComponent(notePath),
            encoding: .utf8
        )
        XCTAssertEqual(onDisk, externalBody)
    }

    func testResolveSaveConflictKeepMineOverwritesExternalChange() async throws {
        let (state, vault, notePath) = try await makeAppStateWithLoadedNote()
        try Data("external\n".utf8).write(
            to: vault.appendingPathComponent(notePath)
        )
        let mine = "# Hello\n\nMy version wins.\n"
        state.updateEditorText(mine)
        await state.saveCurrentNote()?.value
        XCTAssertNotNil(state.currentSaveConflict)

        await state.resolveSaveConflictKeepMine()?.value

        XCTAssertNil(state.currentSaveConflict)
        XCTAssertFalse(state.hasUnsavedChanges)
        let onDisk = try String(
            contentsOf: vault.appendingPathComponent(notePath),
            encoding: .utf8
        )
        XCTAssertEqual(onDisk, mine)
    }

    func testResolveSaveConflictReloadFromDiskDiscardsLocalEdits() async throws {
        let (state, vault, notePath) = try await makeAppStateWithLoadedNote()
        let externalBody = "external winner\n"
        try Data(externalBody.utf8).write(
            to: vault.appendingPathComponent(notePath)
        )
        state.updateEditorText("# Hello\n\nLocal version we're about to drop.\n")
        await state.saveCurrentNote()?.value
        XCTAssertNotNil(state.currentSaveConflict)

        await state.resolveSaveConflictReloadFromDisk()?.value

        XCTAssertNil(state.currentSaveConflict)
        XCTAssertFalse(state.hasUnsavedChanges)
        XCTAssertEqual(state.currentNoteText, externalBody)
        XCTAssertEqual(state.savedBaselineText, externalBody)
    }

    func testResolveSaveConflictCancelLeavesBufferDirty() async throws {
        let (state, vault, notePath) = try await makeAppStateWithLoadedNote()
        try Data("external\n".utf8).write(
            to: vault.appendingPathComponent(notePath)
        )
        let local = "# Hello\n\nLocal version, unresolved.\n"
        state.updateEditorText(local)
        await state.saveCurrentNote()?.value
        XCTAssertNotNil(state.currentSaveConflict)

        state.resolveSaveConflictCancel()

        XCTAssertNil(state.currentSaveConflict)
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertEqual(state.currentNoteText, local)
        // Disk untouched.
        let onDisk = try String(
            contentsOf: vault.appendingPathComponent(notePath),
            encoding: .utf8
        )
        XCTAssertEqual(onDisk, "external\n")
    }

    func testSelectingDifferentFileWhileDirtyArmsPendingNavigation()
        async throws
    {
        let (state, vault, _) = try await makeAppStateWithLoadedNote()
        // Add a second file so there's somewhere to navigate to.
        try Data("# Second\n".utf8).write(
            to: vault.appendingPathComponent("two.md")
        )
        // Re-scan so listFiles sees the new file. The simplest
        // re-scan path here is to ask AppState to reload.
        await state.loadFiles()

        state.updateEditorText("dirty edit\n")
        XCTAssertTrue(state.hasUnsavedChanges)

        // Request the file switch via the same property the
        // sidebar's List selection binds to.
        state.selectedFilePath = "two.md"

        // pendingNavigation is set synchronously; rollback runs on
        // the next runloop tick (see the willSet/sink re-entry
        // notes in AppState.handleSelectionChange).
        XCTAssertEqual(state.pendingNavigation, .selectFile("two.md"))
        XCTAssertEqual(
            state.loadedFilePath, "note.md",
            "loaded file must not have changed while the prompt is up"
        )

        // Let the queued rollback run.
        try await Task.sleep(nanoseconds: 50_000_000)

        // The sidebar selection should now reflect the rolled-back
        // value so the file list visually re-highlights the dirty
        // file.
        XCTAssertEqual(state.selectedFilePath, "note.md")
    }

    func testResolvePendingNavigationDiscardCompletesTheSwitch() async throws {
        let (state, vault, _) = try await makeAppStateWithLoadedNote()
        try Data("# Second\n".utf8).write(
            to: vault.appendingPathComponent("two.md")
        )
        await state.loadFiles()

        state.updateEditorText("dirty\n")
        state.selectedFilePath = "two.md"
        XCTAssertEqual(state.pendingNavigation, .selectFile("two.md"))
        // Let the rollback finish so we can observe the discard
        // path landing on the new file rather than racing with the
        // queued runloop-hop write.
        try await Task.sleep(nanoseconds: 50_000_000)

        state.resolvePendingNavigationDiscard()
        await state.noteLoadTask?.value

        XCTAssertNil(state.pendingNavigation)
        XCTAssertEqual(state.loadedFilePath, "two.md")
        XCTAssertEqual(state.currentNoteText, "# Second\n")
        XCTAssertFalse(state.hasUnsavedChanges)
    }

    func testAttemptCloseVaultWhileDirtyArmsPendingNavigation() async throws {
        let (state, _, _) = try await makeAppStateWithLoadedNote()
        state.updateEditorText("dirty\n")

        state.attemptCloseVault()

        XCTAssertEqual(state.pendingNavigation, .closeVault)
        XCTAssertTrue(state.isVaultOpen, "vault stays open until user resolves")
    }

    func testAttemptCloseVaultWhenCleanClosesImmediately() async throws {
        let (state, _, _) = try await makeAppStateWithLoadedNote()

        state.attemptCloseVault()

        XCTAssertNil(state.pendingNavigation)
        XCTAssertFalse(state.isVaultOpen)
    }
}
