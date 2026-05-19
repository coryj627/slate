import Combine
import XCTest

@testable import YanaMac

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
            .appendingPathComponent("yana-appstate-test-\(UUID().uuidString)")
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
        // pointed YANA at something that isn't a vault.
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
            "yana-internal:something",
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
}
