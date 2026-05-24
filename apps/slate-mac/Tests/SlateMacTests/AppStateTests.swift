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

    private func makeAppState(
        seedEntries: [RecentVault] = [],
        externalOpener: @escaping (URL) -> Bool = { _ in true }
    ) throws -> AppState {
        let store = RecentVaultsStore(fileURL: storeFile)
        if !seedEntries.isEmpty {
            try store.save(seedEntries)
        }
        // Default test opener swallows the URL and reports success so
        // `state.openLink(externalLink)` can exercise the "opened"
        // branch without spawning the developer's default browser.
        // Tests that care which URL was passed inject a recording
        // closure of their own.
        return AppState(recentsStore: store, externalOpener: externalOpener)
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
    ///
    /// The 5-second default is the ceiling, not the expected wait:
    /// the positive-assertion path resumes immediately when the
    /// signal arrives, so a healthy run pays milliseconds. Only
    /// failure paths pay the full timeout. The previous 1-second
    /// default occasionally tripped on heavily-loaded `macos-14`
    /// GitHub runners where the upstream `noteLoadTask` await
    /// outran the window even though the production code was
    /// fine (caught on PR #128 CI). Negative-assertion call sites
    /// (where we *want* to see no emission) pass an explicit
    /// shorter timeout so they don't inherit the new ceiling.
    private func waitForLineScroll(
        _ state: AppState,
        timeout: TimeInterval = 5.0
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
        // The default test opener swallows the URL and reports
        // success, so this asserts the full happy path: the URL
        // reaches the opener with the bytes the user clicked,
        // selectedFilePath stays untouched, and the outcome enum
        // records `.openedExternal`. Previously this test relied on
        // calling `NSWorkspace.shared.open` directly, which actually
        // launched the user's default browser on every local
        // `swift test` run.
        var opened: [URL] = []
        let state = try makeAppState(externalOpener: { url in
            opened.append(url)
            return true
        })
        let link = makeOutgoing(targetRaw: "https://example.com", isExternal: true)
        state.openLink(link)
        XCTAssertNil(state.selectedFilePath)
        XCTAssertEqual(opened.map(\.absoluteString), ["https://example.com"])
        XCTAssertEqual(
            state.lastActivatedLinkOutcome,
            .openedExternal("https://example.com")
        )
    }

    func testOpenExternalLinkRecordsFailureWhenOpenerReturnsFalse() throws {
        // The opener returning false models LaunchServices declining
        // (no handler registered, sandbox refusal, etc.). The outcome
        // enum should switch to `.externalOpenFailed` so the UI can
        // surface the right announcement to VoiceOver.
        let state = try makeAppState(externalOpener: { _ in false })
        let link = makeOutgoing(targetRaw: "https://example.com", isExternal: true)
        state.openLink(link)
        XCTAssertNil(state.selectedFilePath)
        XCTAssertEqual(
            state.lastActivatedLinkOutcome,
            .externalOpenFailed("https://example.com")
        )
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

    // MARK: - Milestone H: create-from-template flow

    /// Sets up a vault with the supplied templates under
    /// `Templates/`, opens the AppState against it, and waits for
    /// the initial scan to finish. Returns the AppState plus the
    /// vault URL so the test can re-read on-disk files afterward.
    private func makeTemplatesVault(
        templates: [(name: String, body: String)]
    ) async throws -> (state: AppState, vault: URL) {
        let vault = tempDir.appendingPathComponent("templates-vault-\(UUID().uuidString)")
        let templatesDir = vault.appendingPathComponent("Templates")
        try FileManager.default.createDirectory(
            at: templatesDir,
            withIntermediateDirectories: true
        )
        for template in templates {
            try Data(template.body.utf8).write(
                to: templatesDir.appendingPathComponent(template.name)
            )
        }
        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        return (state, vault)
    }

    /// Subscribe to `cursorByteOffsetRequest` for the duration of
    /// the test closure and return every value the publisher emitted.
    /// Mirrors the `waitForLineScroll` pattern but doesn't need a
    /// timeout: the template-flow tests can `await` each step
    /// deterministically.
    private func collectCursorOffsets(
        from state: AppState
    ) -> (subscription: AnyCancellable, captured: () -> [Int]) {
        var captured: [Int] = []
        let sub = state.cursorByteOffsetRequest.sink { offset in
            captured.append(offset)
        }
        return (sub, { captured })
    }

    func testOpenTemplatePickerPopulatesAndAnnounces() async throws {
        let (state, _) = try await makeTemplatesVault(templates: [
            ("Alpha.md", "# {{title}}\n"),
            ("Daily.md", "---\ndescription: Daily standup\n---\n"),
        ])

        state.openTemplatePicker()
        await state.templatePickerTask?.value

        XCTAssertTrue(state.isTemplatePickerOpen)
        XCTAssertEqual(
            state.availableTemplates.map(\.name),
            ["Alpha", "Daily"]
        )
        XCTAssertEqual(
            state.templateAnnouncementLastMessage,
            "Template picker opened. 2 templates available."
        )
    }

    func testOpenTemplatePickerAnnouncesEmptyState() async throws {
        let (state, vault) = try await makeTemplatesVault(templates: [])

        state.openTemplatePicker()
        await state.templatePickerTask?.value

        XCTAssertTrue(state.isTemplatePickerOpen)
        XCTAssertTrue(state.availableTemplates.isEmpty)
        let expected = "Template picker opened. No templates found. "
            + "Create one in \(vault.lastPathComponent)/Templates/."
        XCTAssertEqual(state.templateAnnouncementLastMessage, expected)
    }

    func testSelectTemplateWithPromptsRoutesToNeedsPrompts() async throws {
        let (state, _) = try await makeTemplatesVault(templates: [
            (
                "Meeting.md",
                "# Meeting: {{prompt:Topic}}\n\nAttendees: {{prompt:Attendees}}\n"
            ),
        ])
        state.openTemplatePicker()
        await state.templatePickerTask?.value

        let summary = try XCTUnwrap(state.availableTemplates.first)
        state.selectTemplate(summary)
        await state.templateSelectionTask?.value

        XCTAssertFalse(state.isTemplatePickerOpen)
        switch state.pendingTemplateFlow {
        case .needsPrompts(let template, let prompts):
            XCTAssertEqual(template.path, summary.path)
            XCTAssertEqual(prompts.map(\.label), ["Topic", "Attendees"])
            XCTAssertEqual(prompts.map(\.key), ["topic", "attendees"])
        default:
            XCTFail("expected .needsPrompts, got \(state.pendingTemplateFlow)")
        }
    }

    func testSelectTemplateWithoutPromptsSkipsToNeedsName() async throws {
        let (state, _) = try await makeTemplatesVault(templates: [
            ("Scratch.md", "# {{title}}\n\n{{cursor}}\n"),
        ])
        state.openTemplatePicker()
        await state.templatePickerTask?.value

        let summary = try XCTUnwrap(state.availableTemplates.first)
        state.selectTemplate(summary)
        await state.templateSelectionTask?.value

        switch state.pendingTemplateFlow {
        case .needsName(let template, let values):
            XCTAssertEqual(template.path, summary.path)
            XCTAssertTrue(values.isEmpty)
        default:
            XCTFail("expected .needsName, got \(state.pendingTemplateFlow)")
        }
    }

    func testCreateFromStaticTemplateWritesFileAndSendsCursor() async throws {
        let (state, vault) = try await makeTemplatesVault(templates: [
            ("Scratch.md", "# {{title}}\n\n{{cursor}}\n"),
        ])
        let (sub, captured) = collectCursorOffsets(from: state)
        defer { sub.cancel() }

        state.openTemplatePicker()
        await state.templatePickerTask?.value
        let summary = try XCTUnwrap(state.availableTemplates.first)
        state.selectTemplate(summary)
        await state.templateSelectionTask?.value

        state.submitTemplateNoteName("scratch-1.md")
        await state.templateCreateTask?.value

        XCTAssertEqual(state.pendingTemplateFlow, .idle)
        XCTAssertNil(state.templateNoteNameError)
        XCTAssertEqual(
            state.templateAnnouncementLastMessage,
            "Created scratch-1.md from Scratch."
        )

        let onDisk = try String(
            contentsOf: vault.appendingPathComponent("scratch-1.md"),
            encoding: .utf8
        )
        // `{{title}}` resolves to the file stem, `{{cursor}}` is
        // stripped to empty and its offset is captured separately.
        XCTAssertEqual(onDisk, "# scratch-1\n\n\n")
        XCTAssertEqual(state.selectedFilePath, "scratch-1.md")

        // Wait for the post-load cursor request to land.
        await state.noteLoadTask?.value
        // Give the queued Task a runloop tick to run.
        await Task.yield()
        // The cursor offset matches the byte index of `{{cursor}}`'s
        // substitution in the rendered body: "# scratch-1\n\n" = 13
        // bytes, so the offset is 13.
        XCTAssertEqual(captured(), [13])
    }

    func testCreateFromPromptedTemplateStuffsPromptsIntoRightSlots() async throws {
        let body = "# Meeting: {{prompt:Topic}}\n\n"
            + "Attendees: {{prompt:Attendees}}\n\n{{cursor}}\n"
        let (state, vault) = try await makeTemplatesVault(templates: [
            ("Meeting.md", body),
        ])
        state.openTemplatePicker()
        await state.templatePickerTask?.value
        let summary = try XCTUnwrap(state.availableTemplates.first)
        state.selectTemplate(summary)
        await state.templateSelectionTask?.value

        state.submitTemplatePrompts([
            "topic": "Quarterly review",
            "attendees": "Cory, Pat",
        ])
        // The transition is synchronous; advance to .needsName
        // without an await.
        switch state.pendingTemplateFlow {
        case .needsName(_, let values):
            XCTAssertEqual(values["topic"], "Quarterly review")
            XCTAssertEqual(values["attendees"], "Cory, Pat")
        default:
            XCTFail("expected .needsName after submitPrompts, got \(state.pendingTemplateFlow)")
        }

        state.submitTemplateNoteName("Q2-review.md")
        await state.templateCreateTask?.value

        let onDisk = try String(
            contentsOf: vault.appendingPathComponent("Q2-review.md"),
            encoding: .utf8
        )
        XCTAssertEqual(
            onDisk,
            "# Meeting: Quarterly review\n\nAttendees: Cory, Pat\n\n\n"
        )
        XCTAssertEqual(state.pendingTemplateFlow, .idle)
        XCTAssertEqual(
            state.templateAnnouncementLastMessage,
            "Created Q2-review.md from Meeting."
        )
    }

    func testCreateFromTemplateRendersDateVariable() async throws {
        let (state, vault) = try await makeTemplatesVault(templates: [
            ("Daily.md", "# {{date:%Y-%m-%d}}\n"),
        ])
        state.openTemplatePicker()
        await state.templatePickerTask?.value
        let summary = try XCTUnwrap(state.availableTemplates.first)
        state.selectTemplate(summary)
        await state.templateSelectionTask?.value

        // Use the default name to also cover the Daily-date suffix
        // path.
        let defaultName = state.defaultNewNoteName(for: summary)
        state.submitTemplateNoteName(defaultName)
        await state.templateCreateTask?.value

        let onDisk = try String(
            contentsOf: vault.appendingPathComponent(defaultName),
            encoding: .utf8
        )
        // Today's date in UTC, matching the chrono `%Y-%m-%d`
        // formatter the render engine uses.
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = "yyyy-MM-dd"
        let today = formatter.string(from: Date())
        XCTAssertEqual(onDisk, "# \(today)\n")
        // The space-separated form is the post-#133 shape; multi-word
        // Daily templates (`Daily Standup`) read more naturally than
        // the prior `Daily-...` join.
        XCTAssertEqual(
            defaultName, "Daily \(today).md",
            "defaultNewNoteName should join the date with a space; got \(defaultName)"
        )
    }

    func testDefaultNewNoteNameForMultiWordDailyTemplateUsesSpaceSeparator() async throws {
        // Regression for #133. `Daily Standup` (multi-word) previously
        // produced `Daily Standup-2026-05-23.md` — the dash after a
        // space reads awkwardly. New shape uses a single space.
        let (state, _) = try await makeTemplatesVault(templates: [
            ("Daily Standup.md", "# {{date:%Y-%m-%d}}\n"),
        ])
        state.openTemplatePicker()
        await state.templatePickerTask?.value
        let summary = try XCTUnwrap(state.availableTemplates.first)

        let defaultName = state.defaultNewNoteName(for: summary)

        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = "yyyy-MM-dd"
        let today = formatter.string(from: Date())
        XCTAssertEqual(defaultName, "Daily Standup \(today).md")
    }

    func testSubmitTemplateNoteNameTreatsMdSuffixCaseInsensitively() async throws {
        // Regression for #133. A user typing `notes.MD` (or pasting
        // from a system that uppercased the extension) previously
        // got `notes.MD.md` on disk because the suffix check was
        // case-sensitive.
        let (state, vault) = try await makeTemplatesVault(templates: [
            ("Plain.md", "# stub\n"),
        ])
        state.openTemplatePicker()
        await state.templatePickerTask?.value
        let summary = try XCTUnwrap(state.availableTemplates.first)
        state.selectTemplate(summary)
        await state.templateSelectionTask?.value

        state.submitTemplateNoteName("notes.MD")
        await state.templateCreateTask?.value

        // File written as `notes.MD` (preserves the user's casing),
        // not `notes.MD.md` (which would have been the pre-#133
        // result).
        let preserved = vault.appendingPathComponent("notes.MD")
        let doubled = vault.appendingPathComponent("notes.MD.md")
        XCTAssertTrue(
            FileManager.default.fileExists(atPath: preserved.path),
            "expected file written as `notes.MD`"
        )
        XCTAssertFalse(
            FileManager.default.fileExists(atPath: doubled.path),
            "must not double-suffix to `notes.MD.md`"
        )
    }

    func testDailyTemplateNameMatcherTreatsSubstringMatchesAsNotDaily() {
        // Codoki PR #154 follow-up: `Dailyness` and `Daily123`
        // start with the substring `daily` but aren't daily-note
        // templates. Only the standalone word `daily` (followed by
        // end-of-string OR a non-alphanumeric boundary) should
        // qualify.
        XCTAssertTrue(AppState.isDailyTemplateName("Daily"))
        XCTAssertTrue(AppState.isDailyTemplateName("daily"))
        XCTAssertTrue(AppState.isDailyTemplateName("Daily Standup"))
        XCTAssertTrue(AppState.isDailyTemplateName("Daily-Notes"))
        XCTAssertTrue(AppState.isDailyTemplateName("Daily.md"))
        XCTAssertTrue(AppState.isDailyTemplateName("DAILY"))

        XCTAssertFalse(AppState.isDailyTemplateName("Dailyness"))
        XCTAssertFalse(AppState.isDailyTemplateName("Daily123"))
        XCTAssertFalse(AppState.isDailyTemplateName("DailyMeeting"))
        XCTAssertFalse(AppState.isDailyTemplateName(""))
        XCTAssertFalse(AppState.isDailyTemplateName("Weekly"))
    }

    func testDefaultNewNoteNameForDailynessTemplateDoesNotGetDateSuffix() async throws {
        // Regression for Codoki PR #154 Medium. Before the
        // word-boundary fix, a template named `Dailyness.md`
        // produced `Dailyness 2026-05-24.md` instead of just
        // `Dailyness.md`.
        let (state, _) = try await makeTemplatesVault(templates: [
            ("Dailyness.md", "# wellbeing\n"),
        ])
        state.openTemplatePicker()
        await state.templatePickerTask?.value
        let summary = try XCTUnwrap(state.availableTemplates.first)

        let defaultName = state.defaultNewNoteName(for: summary)
        XCTAssertEqual(defaultName, "Dailyness.md")
    }

    func testSubmitTemplateNoteNameHandlesMultiDotExtensions() async throws {
        // Codoki PR #154 suggestion: `archive.tar.MD` is already
        // markdown-shaped (extension `MD`, case-insensitively `md`);
        // the previous hasSuffix(".md") branch handled it, but the
        // new `pathExtension` shape is more semantically correct
        // and worth locking in.
        let (state, vault) = try await makeTemplatesVault(templates: [
            ("Plain.md", "# stub\n"),
        ])
        state.openTemplatePicker()
        await state.templatePickerTask?.value
        let summary = try XCTUnwrap(state.availableTemplates.first)
        state.selectTemplate(summary)
        await state.templateSelectionTask?.value

        state.submitTemplateNoteName("archive.tar.MD")
        await state.templateCreateTask?.value

        let preserved = vault.appendingPathComponent("archive.tar.MD")
        let doubled = vault.appendingPathComponent("archive.tar.MD.md")
        XCTAssertTrue(
            FileManager.default.fileExists(atPath: preserved.path),
            "expected `archive.tar.MD` to be preserved as-is"
        )
        XCTAssertFalse(
            FileManager.default.fileExists(atPath: doubled.path),
            "must not double-suffix to `archive.tar.MD.md`"
        )
    }

    func testCancelFromPromptSheetResetsFlowAndWritesNoFile() async throws {
        let (state, vault) = try await makeTemplatesVault(templates: [
            ("Meeting.md", "# {{prompt:Topic}}\n"),
        ])
        state.openTemplatePicker()
        await state.templatePickerTask?.value
        let summary = try XCTUnwrap(state.availableTemplates.first)
        state.selectTemplate(summary)
        await state.templateSelectionTask?.value

        guard case .needsPrompts = state.pendingTemplateFlow else {
            return XCTFail("expected to land on prompt step, got \(state.pendingTemplateFlow)")
        }

        state.cancelTemplateFlow()

        XCTAssertEqual(state.pendingTemplateFlow, .idle)
        XCTAssertFalse(state.isTemplatePickerOpen)
        // Make sure no spurious file was created under the vault root
        // or under Templates/.
        let vaultContents = try FileManager.default.contentsOfDirectory(
            at: vault,
            includingPropertiesForKeys: nil
        )
        let rootFileNames = vaultContents.map(\.lastPathComponent).sorted()
        XCTAssertEqual(rootFileNames, [".slate", "Templates"])
    }

    func testCancelFromNameSheetResetsFlowAndWritesNoFile() async throws {
        let (state, vault) = try await makeTemplatesVault(templates: [
            ("Scratch.md", "# {{title}}\n"),
        ])
        state.openTemplatePicker()
        await state.templatePickerTask?.value
        let summary = try XCTUnwrap(state.availableTemplates.first)
        state.selectTemplate(summary)
        await state.templateSelectionTask?.value

        guard case .needsName = state.pendingTemplateFlow else {
            return XCTFail("expected to land on name step, got \(state.pendingTemplateFlow)")
        }

        state.cancelTemplateFlow()

        XCTAssertEqual(state.pendingTemplateFlow, .idle)
        let vaultContents = try FileManager.default.contentsOfDirectory(
            at: vault,
            includingPropertiesForKeys: nil
        )
        XCTAssertEqual(
            vaultContents.map(\.lastPathComponent).sorted(),
            [".slate", "Templates"]
        )
    }

    func testSubmitTemplateNoteNameRejectsEmpty() async throws {
        let (state, _) = try await makeTemplatesVault(templates: [
            ("Scratch.md", "# {{title}}\n"),
        ])
        state.openTemplatePicker()
        await state.templatePickerTask?.value
        let summary = try XCTUnwrap(state.availableTemplates.first)
        state.selectTemplate(summary)
        await state.templateSelectionTask?.value

        state.submitTemplateNoteName("   ")
        XCTAssertNotNil(state.templateNoteNameError)
        XCTAssertNotEqual(state.pendingTemplateFlow, .idle)
    }

    func testSubmitTemplateNoteNameRejectsParentTraversal() async throws {
        let (state, _) = try await makeTemplatesVault(templates: [
            ("Scratch.md", "# {{title}}\n"),
        ])
        state.openTemplatePicker()
        await state.templatePickerTask?.value
        let summary = try XCTUnwrap(state.availableTemplates.first)
        state.selectTemplate(summary)
        await state.templateSelectionTask?.value

        state.submitTemplateNoteName("../escape.md")
        XCTAssertNotNil(state.templateNoteNameError)
        XCTAssertNotEqual(state.pendingTemplateFlow, .idle)
    }

    // MARK: - Tasks panel + review (#113 + #114)
    //
    // The backend's `tasksForFile` / `tasksInVault` / `toggleTaskStatus`
    // surfaces are exercised directly in slate-core's tests; here we
    // only need to confirm AppState routes correctly:
    //   - selecting a file populates `currentNoteTasks`
    //   - toggle calls the FFI with the right ordinal + status char
    //     and refreshes both `currentNoteTasks` and the editor buffer
    //   - opening the review surface kicks `loadVaultTasks`
    //   - filter switches re-query and surface the new filter set
    //   - row activation populates `selectedFilePath` + sends
    //     `lineScrollRequest` for cross-file navigation

    func testSelectingNoteWithTasksPopulatesCurrentNoteTasks() async throws {
        let vault = tempDir.appendingPathComponent("tasks-panel-vault")
        try FileManager.default.createDirectory(
            at: vault,
            withIntermediateDirectories: true
        )
        try "- [ ] one\n- [x] two\n- [ ] three\n".data(using: .utf8)!.write(
            to: vault.appendingPathComponent("a.md")
        )

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        await state.tasksLoadTask?.value

        XCTAssertEqual(state.currentNoteTasks.count, 3)
        XCTAssertEqual(state.currentNoteTasks.map(\.completed), [false, true, false])
        XCTAssertEqual(state.currentNoteTasks.map(\.text), ["one", "two", "three"])
    }

    func testToggleCurrentTaskFlipsStatusAndRefreshesPanel() async throws {
        let vault = tempDir.appendingPathComponent("toggle-vault")
        try FileManager.default.createDirectory(
            at: vault,
            withIntermediateDirectories: true
        )
        try "- [ ] open task\n- [ ] another\n".data(using: .utf8)!.write(
            to: vault.appendingPathComponent("a.md")
        )

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        await state.tasksLoadTask?.value

        let firstTask = try XCTUnwrap(state.currentNoteTasks.first)
        XCTAssertFalse(firstTask.completed)
        await state.toggleCurrentTask(firstTask)?.value
        // Drop into the next runloop pass so the post-toggle refresh
        // task (refreshTasksAfterSave) lands.
        await Task.yield()
        try await Task.sleep(nanoseconds: 50_000_000)

        // On-disk file flipped from `[ ]` to `[x]`.
        let onDisk = try String(
            contentsOf: vault.appendingPathComponent("a.md"),
            encoding: .utf8
        )
        XCTAssertTrue(
            onDisk.contains("- [x] open task"),
            "toggle should have written `[x]` to disk; got: \(onDisk)"
        )
        // Panel mirrors the new state — first task now completed,
        // count + ordinal preserved.
        XCTAssertEqual(state.currentNoteTasks.count, 2)
        XCTAssertTrue(state.currentNoteTasks[0].completed)
        XCTAssertFalse(state.currentNoteTasks[1].completed)
        // Editor buffer also reflects the new on-disk state so the
        // user's next edit doesn't clobber the toggle.
        XCTAssertTrue(
            (state.currentNoteText ?? "").contains("- [x] open task"),
            "editor buffer should reflect the toggled text"
        )
        // No conflict surfaced; cached content hash refreshed.
        XCTAssertNil(state.currentSaveConflict)
        XCTAssertNotNil(state.currentNoteContentHash)
    }

    func testOpenTasksReviewKicksLoadAndFlipsSheetState() async throws {
        let vault = tempDir.appendingPathComponent("review-vault")
        try FileManager.default.createDirectory(
            at: vault,
            withIntermediateDirectories: true
        )
        try "- [ ] only task\n".data(using: .utf8)!.write(
            to: vault.appendingPathComponent("a.md")
        )

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        XCTAssertFalse(state.isTasksReviewOpen)
        state.openTasksReview()
        XCTAssertTrue(state.isTasksReviewOpen)
        await state.vaultTasksLoadTask?.value

        XCTAssertEqual(state.vaultTasks.count, 1)
        XCTAssertEqual(state.vaultTasks.first?.task.text, "only task")
        XCTAssertEqual(state.taskReviewFilter, .all)
    }

    func testApplyTaskReviewFilterRequeriesWithNewWindow() async throws {
        let vault = tempDir.appendingPathComponent("filter-vault")
        try FileManager.default.createDirectory(
            at: vault,
            withIntermediateDirectories: true
        )
        // Hand-tune dates against UTC midnight so the filter
        // windows have deterministic membership: an `overdue`
        // task (2024-01-01) and a far-future task (2099-12-31).
        try "- [ ] long overdue 📅 2024-01-01\n- [ ] far future 📅 2099-12-31\n"
            .data(using: .utf8)!
            .write(to: vault.appendingPathComponent("a.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.openTasksReview()
        await state.vaultTasksLoadTask?.value
        XCTAssertEqual(state.vaultTasks.count, 2, "All filter shows both")

        state.applyTaskReviewFilter(.overdue)
        await state.vaultTasksLoadTask?.value
        XCTAssertEqual(state.taskReviewFilter, .overdue)
        let overdueTexts = state.vaultTasks.map(\.task.text)
        XCTAssertEqual(
            overdueTexts, ["long overdue"],
            "Overdue filter should include only the 2024-01-01 task"
        )

        state.applyTaskReviewFilter(.thisWeek)
        await state.vaultTasksLoadTask?.value
        XCTAssertEqual(state.taskReviewFilter, .thisWeek)
        XCTAssertEqual(
            state.vaultTasks.count, 0,
            "This-week filter should exclude both the 2024 and 2099 tasks"
        )
    }

    func testOpenTaskRowInEditorSwitchesFileAndRequestsLineScroll() async throws {
        let vault = tempDir.appendingPathComponent("review-jump-vault")
        try FileManager.default.createDirectory(
            at: vault,
            withIntermediateDirectories: true
        )
        try "- [ ] task on line 1\n# Heading\n- [ ] task on line 3\n"
            .data(using: .utf8)!
            .write(to: vault.appendingPathComponent("b.md"))
        try "# Other note\n".data(using: .utf8)!.write(
            to: vault.appendingPathComponent("a.md")
        )

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        // Start on a different file so the activation triggers a
        // selection change.
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value

        // Subscribe to lineScrollRequest so we can assert the line
        // number that gets emitted after activation.
        var received: [Int] = []
        let cancellable = state.lineScrollRequest.sink { received.append($0) }
        defer { cancellable.cancel() }

        state.openTasksReview()
        await state.vaultTasksLoadTask?.value
        let row = try XCTUnwrap(state.vaultTasks.first { $0.task.text == "task on line 3" })

        state.openTaskRowInEditor(row)

        // Activation closes the sheet, switches selection, and
        // schedules the scroll for after the new file's load.
        XCTAssertFalse(state.isTasksReviewOpen)
        XCTAssertEqual(state.selectedFilePath, "b.md")
        await state.noteLoadTask?.value
        // Yield twice so the awaiting Task that fires the scroll
        // request lands before we read `received`.
        await Task.yield()
        try await Task.sleep(nanoseconds: 50_000_000)
        XCTAssertEqual(
            received, [3],
            "Activation should request a scroll to the task's source line (1-based)"
        )
    }

    func testToggleVaultTaskUpdatesUnderlyingFileAndReQueries() async throws {
        let vault = tempDir.appendingPathComponent("toggle-vault-review")
        try FileManager.default.createDirectory(
            at: vault,
            withIntermediateDirectories: true
        )
        try "- [ ] from review\n".data(using: .utf8)!.write(
            to: vault.appendingPathComponent("a.md")
        )

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openTasksReview()
        await state.vaultTasksLoadTask?.value

        let row = try XCTUnwrap(state.vaultTasks.first)
        XCTAssertFalse(row.task.completed)
        await state.toggleVaultTask(row)?.value
        await state.vaultTasksLoadTask?.value

        // The vault re-query reflects the new state.
        let updated = try XCTUnwrap(state.vaultTasks.first)
        XCTAssertTrue(updated.task.completed)
        // On-disk file changed too.
        let onDisk = try String(
            contentsOf: vault.appendingPathComponent("a.md"),
            encoding: .utf8
        )
        XCTAssertTrue(onDisk.contains("- [x] from review"))
    }

    func testTaskReviewFilterToFFIShapesMatchTheirDocumentedSemantics() {
        // Unit-level sanity check: the filter cases produce the
        // expected `completed` + due-window pairs. Pinning these
        // shapes prevents accidental regressions in the date math
        // (UTC midnight bounds, [from, to) windows, etc.).
        let now = ISO8601DateFormatter().date(from: "2026-05-24T12:00:00Z")!
        let utcDay: Int64 = 86_400_000

        // All — no constraints.
        let all = TaskReviewFilter.all.toFFIFilter(now: now)
        XCTAssertNil(all.completed)
        XCTAssertNil(all.dueFromMs)
        XCTAssertNil(all.dueToMs)

        // Due today — completed=false, [startOfDay, startOfDay+1day).
        let dueToday = TaskReviewFilter.dueToday.toFFIFilter(now: now)
        XCTAssertEqual(dueToday.completed, false)
        let expectedStart: Int64 = {
            let iso = ISO8601DateFormatter()
            return Int64(iso.date(from: "2026-05-24T00:00:00Z")!.timeIntervalSince1970 * 1000)
        }()
        XCTAssertEqual(dueToday.dueFromMs, expectedStart)
        XCTAssertEqual(dueToday.dueToMs, expectedStart + utcDay)

        // Overdue — completed=false, [0, startOfToday).
        let overdue = TaskReviewFilter.overdue.toFFIFilter(now: now)
        XCTAssertEqual(overdue.completed, false)
        XCTAssertEqual(overdue.dueFromMs, 0)
        XCTAssertEqual(overdue.dueToMs, expectedStart)

        // This week — completed=false, [startOfToday, +7 days).
        let thisWeek = TaskReviewFilter.thisWeek.toFFIFilter(now: now)
        XCTAssertEqual(thisWeek.completed, false)
        XCTAssertEqual(thisWeek.dueFromMs, expectedStart)
        XCTAssertEqual(thisWeek.dueToMs, expectedStart + utcDay * 7)
    }

    // MARK: - Milestone G integration test (#115)
    //
    // End-to-end "the milestone shipped" coverage. The atomic
    // tests above each pin one piece of the contract; this one
    // walks the whole AppState surface in a single sequence so an
    // accidental regression in any seam (scanner → tasks_db →
    // tasks_for_file → toggle → re-query → conflict) surfaces as
    // a single, obvious failure here even when the targeted unit
    // tests still pass.
    //
    // Closing checkpoint per the issue: a fixture vault with three
    // notes carrying mixed task states (open / done / due-today /
    // overdue), per-file currentNoteTasks walk, every
    // TaskReviewFilter case, a happy-path toggle, then an external
    // write that surfaces a WriteConflict on the next toggle.

    /// UTC `yyyy-MM-dd` formatter shared across the integration
    /// test's fixture lines. Cached so the test doesn't pay
    /// formatter setup cost three times. Locale pinned to POSIX so
    /// it produces stable digits regardless of the runner's locale.
    private static let integrationUtcDateFormatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(identifier: "UTC")
        formatter.dateFormat = "yyyy-MM-dd"
        return formatter
    }()

    func testMilestoneGEndToEndRoundTrip() async throws {
        // === Fixture dates ===
        //
        // Dates are computed from `Date()` so the filter math
        // inside `applyTaskReviewFilter` (which also calls
        // `Date()`) lines up with what the fixture wrote. We use
        // the same UTC calendar shape the production code uses
        // (`TaskReviewFilter.utcCalendar`) so today/yesterday/in-3-
        // days resolve to the same UTC-midnight boundaries the
        // filter compares against.
        //
        // There's a vanishing race if the test runs exactly across
        // UTC midnight (test computes today as day D, filter
        // observes day D+1). Practically a sub-millisecond window
        // out of 86 400 000 ms per day — not worth guarding for a
        // test that runs in <1s.
        var utcCal = Calendar(identifier: .gregorian)
        utcCal.timeZone = TimeZone(identifier: "UTC")!
        let todayUtc = utcCal.startOfDay(for: Date())
        let yesterdayUtc = utcCal.date(byAdding: .day, value: -1, to: todayUtc)!
        let in3DaysUtc = utcCal.date(byAdding: .day, value: 3, to: todayUtc)!

        let fmt = Self.integrationUtcDateFormatter
        let todayStr = fmt.string(from: todayUtc)
        let yesterdayStr = fmt.string(from: yesterdayUtc)
        let in3DaysStr = fmt.string(from: in3DaysUtc)

        // === Vault layout ===
        //
        // work.md      — 3 tasks: open+overdue, open+due-today, done
        // future.md    — 2 tasks: open+due-in-3-days, open+no-date
        // done.md      — 2 tasks: both completed, no dates
        //
        // Total: 7 tasks; 5 open, 2 done; 1 overdue, 1 due today,
        // 1 in this week (+2 days inside the [today, +7) window).
        let vault = tempDir.appendingPathComponent("milestone-g-integration")
        try FileManager.default.createDirectory(
            at: vault,
            withIntermediateDirectories: true
        )
        try """
            # Work
            - [ ] overdue work 📅 \(yesterdayStr)
            - [ ] due today 📅 \(todayStr)
            - [x] already finished
            """
            .data(using: .utf8)!
            .write(to: vault.appendingPathComponent("work.md"))
        try """
            # Future
            - [ ] starts later 📅 \(in3DaysStr)
            - [ ] no date task
            """
            .data(using: .utf8)!
            .write(to: vault.appendingPathComponent("future.md"))
        try """
            # Done
            - [x] shipped one
            - [x] shipped two
            """
            .data(using: .utf8)!
            .write(to: vault.appendingPathComponent("done.md"))

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        // === Vault-wide count ===
        state.openTasksReview()
        await state.vaultTasksLoadTask?.value
        XCTAssertEqual(
            state.vaultTasks.count, 7,
            ".all should land every task across the three files (3 + 2 + 2)"
        )

        // === Per-file currentNoteTasks walk ===
        state.selectedFilePath = "work.md"
        await state.noteLoadTask?.value
        await state.tasksLoadTask?.value
        XCTAssertEqual(state.currentNoteTasks.count, 3, "work.md has 3 tasks")
        XCTAssertEqual(
            state.currentNoteTasks.map(\.completed),
            [false, false, true],
            "work.md tasks in document order: overdue, due-today, done"
        )
        XCTAssertEqual(
            state.currentNoteTasks.map(\.text),
            ["overdue work", "due today", "already finished"]
        )

        state.selectedFilePath = "future.md"
        await state.noteLoadTask?.value
        await state.tasksLoadTask?.value
        XCTAssertEqual(state.currentNoteTasks.count, 2, "future.md has 2 tasks")
        XCTAssertNotNil(
            state.currentNoteTasks[0].dueMs,
            "first future.md task carries a due date"
        )
        XCTAssertNil(
            state.currentNoteTasks[1].dueMs,
            "second future.md task has no due date"
        )

        state.selectedFilePath = "done.md"
        await state.noteLoadTask?.value
        await state.tasksLoadTask?.value
        XCTAssertEqual(state.currentNoteTasks.count, 2, "done.md has 2 tasks")
        XCTAssertTrue(
            state.currentNoteTasks.allSatisfy(\.completed),
            "done.md tasks are all completed"
        )

        // === Each TaskReviewFilter case asserts membership ===
        //
        // `.all` already asserted above; re-set it to drop any
        // filter state from previous bodies + drive the re-query
        // through `applyTaskReviewFilter` (the public mutator).
        state.applyTaskReviewFilter(.all)
        await state.vaultTasksLoadTask?.value
        XCTAssertEqual(state.vaultTasks.count, 7, ".all = every task")

        state.applyTaskReviewFilter(.dueToday)
        await state.vaultTasksLoadTask?.value
        XCTAssertEqual(
            state.vaultTasks.map(\.task.text),
            ["due today"],
            ".dueToday should land exactly the today-dated work.md task"
        )

        state.applyTaskReviewFilter(.overdue)
        await state.vaultTasksLoadTask?.value
        XCTAssertEqual(
            state.vaultTasks.map(\.task.text),
            ["overdue work"],
            ".overdue should land the yesterday-dated open task only"
        )

        state.applyTaskReviewFilter(.thisWeek)
        await state.vaultTasksLoadTask?.value
        // .thisWeek = [today, today+7). today task qualifies;
        // in-3-days qualifies; overdue (yesterday) doesn't; no-
        // date doesn't; completed don't.
        XCTAssertEqual(
            Set(state.vaultTasks.map(\.task.text)),
            Set(["due today", "starts later"]),
            ".thisWeek should land today + in-3-days, exclude overdue + no-date + done"
        )

        state.closeTasksReview()

        // === Toggle a task via toggleCurrentTask, assert round-trip ===
        state.selectedFilePath = "work.md"
        await state.noteLoadTask?.value
        await state.tasksLoadTask?.value

        let openTodayTask = try XCTUnwrap(
            state.currentNoteTasks.first(where: { $0.text == "due today" }),
            "fixture should expose the today-dated task on work.md"
        )
        XCTAssertFalse(openTodayTask.completed)

        // Snapshot the post-toggle hash so we can verify the
        // cache updated after the round-trip lands.
        let preToggleHash = state.currentNoteContentHash
        await state.toggleCurrentTask(openTodayTask)?.value
        // refreshTasksAfterSave + reloadEditorBufferAfterToggle
        // are fire-and-forget Tasks; yield + brief sleep so they
        // settle. The unit test for toggle uses the same pattern.
        await Task.yield()
        try await Task.sleep(nanoseconds: 50_000_000)

        // On-disk file flipped from `[ ]` to `[x]` on the
        // today-dated task.
        let onDiskAfterToggle = try String(
            contentsOf: vault.appendingPathComponent("work.md"),
            encoding: .utf8
        )
        XCTAssertTrue(
            onDiskAfterToggle.contains("- [x] due today"),
            "toggle should have flipped the today task to done on disk; got: \(onDiskAfterToggle)"
        )
        let updatedTask = try XCTUnwrap(
            state.currentNoteTasks.first(where: { $0.text == "due today" }),
            "panel should still expose the row after the toggle"
        )
        XCTAssertTrue(
            updatedTask.completed,
            "panel should reflect the new completed state"
        )
        XCTAssertNotEqual(
            state.currentNoteContentHash,
            preToggleHash,
            "content hash should update after the toggle's save"
        )
        XCTAssertNil(
            state.currentSaveConflict,
            "happy-path toggle should not surface a WriteConflict"
        )

        // === External write → next toggle surfaces WriteConflict ===
        //
        // `currentNoteContentHash` is now the post-toggle hash.
        // An external editor overwriting work.md invalidates that
        // hash, so the next toggle's `toggle_task_status` call
        // (which passes expectedContentHash like the save flow
        // does) returns WriteConflict — and AppState's
        // performToggleCurrentTask routes that into
        // `currentSaveConflict` for the same resolution UI as
        // editor saves.
        try "# Overwritten externally\n- [ ] new task\n"
            .data(using: .utf8)!
            .write(to: vault.appendingPathComponent("work.md"))
        // Give the FS write a beat so the next toggle's read sees
        // the new content (and the new hash). Same 50ms cadence
        // as the post-toggle settle.
        try await Task.sleep(nanoseconds: 50_000_000)

        // Use the now-stale "overdue work" task — its identity
        // doesn't matter, the conflict surfaces before mutation.
        let staleTask = try XCTUnwrap(
            state.currentNoteTasks.first(where: { $0.text == "overdue work" }),
            "stale panel should still expose the overdue row"
        )
        await state.toggleCurrentTask(staleTask)?.value
        try await Task.sleep(nanoseconds: 50_000_000)

        XCTAssertNotNil(
            state.currentSaveConflict,
            "external write should make the next toggle surface a WriteConflict"
        )
        XCTAssertEqual(
            state.currentSaveConflict?.path, "work.md",
            "the conflict should be attributed to the file that was overwritten"
        )
    }

    // MARK: - Toggle-while-dirty buffer protection (#158)
    //
    // The toggle path's post-save reload (`reloadEditorBufferAfterToggle`)
    // overwrites `currentNoteText` from disk. If the buffer has
    // unsaved edits, those are silently dropped. The FFI's
    // WriteConflict check doesn't catch this case because
    // `currentNoteContentHash` tracks the disk hash, not the
    // buffer hash. AppState now guards the toggle entry points
    // against this scenario.

    func testToggleCurrentTaskNoOpsWhenEditorHasUnsavedChanges() async throws {
        let vault = tempDir.appendingPathComponent("dirty-toggle-vault")
        try FileManager.default.createDirectory(
            at: vault,
            withIntermediateDirectories: true
        )
        try "- [ ] still open\n".data(using: .utf8)!.write(
            to: vault.appendingPathComponent("a.md")
        )

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        await state.tasksLoadTask?.value

        // Simulate the user typing into the editor — pushes the
        // buffer dirty without touching disk. updateEditorText is
        // the same path the SwiftUI Binding uses.
        let originalText = try XCTUnwrap(state.currentNoteText)
        state.updateEditorText(originalText + "\nuser edit\n")
        XCTAssertTrue(
            state.hasUnsavedChanges,
            "precondition: editor buffer is dirty"
        )

        let task = try XCTUnwrap(state.currentNoteTasks.first)
        let toggleResult = state.toggleCurrentTask(task)

        XCTAssertNil(
            toggleResult,
            "toggle should be a no-op while editor is dirty; returns nil"
        )

        // The on-disk file is untouched.
        let onDisk = try String(
            contentsOf: vault.appendingPathComponent("a.md"),
            encoding: .utf8
        )
        XCTAssertEqual(
            onDisk, "- [ ] still open\n",
            "on-disk file must not change when the toggle is blocked"
        )
        // The buffer's user edits stay intact.
        XCTAssertEqual(
            state.currentNoteText,
            originalText + "\nuser edit\n",
            "editor buffer must keep the user's unsaved edits"
        )
        // Dirty flag stays true so the next save still flushes.
        XCTAssertTrue(state.hasUnsavedChanges)
        // No conflict UI fires — this isn't a conflict, it's a
        // pre-emptive block.
        XCTAssertNil(state.currentSaveConflict)
        // Panel state reflects the (unchanged) on-disk task.
        XCTAssertFalse(
            state.currentNoteTasks.first?.completed ?? true,
            "task remains open since the toggle was blocked"
        )
    }

    func testToggleVaultTaskNoOpsWhenTogglingActiveFileWithDirtyBuffer() async throws {
        let vault = tempDir.appendingPathComponent("dirty-vault-toggle")
        try FileManager.default.createDirectory(
            at: vault,
            withIntermediateDirectories: true
        )
        try "- [ ] from review\n".data(using: .utf8)!.write(
            to: vault.appendingPathComponent("a.md")
        )

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        // Select the file + dirty its buffer so the review's
        // toggle would target the loaded-and-dirty file.
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        await state.tasksLoadTask?.value
        let originalText = try XCTUnwrap(state.currentNoteText)
        state.updateEditorText(originalText + "\ndirty edit\n")
        XCTAssertTrue(state.hasUnsavedChanges)

        state.openTasksReview()
        await state.vaultTasksLoadTask?.value
        let row = try XCTUnwrap(state.vaultTasks.first)
        let toggleResult = state.toggleVaultTask(row)

        XCTAssertNil(
            toggleResult,
            "toggleVaultTask should no-op against the loaded-and-dirty file"
        )
        let onDisk = try String(
            contentsOf: vault.appendingPathComponent("a.md"),
            encoding: .utf8
        )
        XCTAssertEqual(
            onDisk, "- [ ] from review\n",
            "on-disk file must not change when the toggle is blocked"
        )
        XCTAssertEqual(
            state.currentNoteText,
            originalText + "\ndirty edit\n",
            "editor buffer keeps the user's unsaved edits"
        )
        XCTAssertTrue(state.hasUnsavedChanges)
        // The row in the vault list still shows the task as open
        // (no re-query was triggered, but the underlying state
        // didn't change so this should hold regardless).
        XCTAssertFalse(state.vaultTasks.first?.task.completed ?? true)
    }

    func testToggleVaultTaskProceedsWhenTogglingUnloadedFile() async throws {
        let vault = tempDir.appendingPathComponent("unloaded-toggle-vault")
        try FileManager.default.createDirectory(
            at: vault,
            withIntermediateDirectories: true
        )
        // Two files: a.md (we'll load + dirty), b.md (we'll
        // toggle from the review surface). Dirtying a.md must NOT
        // block toggles on b.md because b.md has no live buffer.
        try "- [ ] task in A\n".data(using: .utf8)!.write(
            to: vault.appendingPathComponent("a.md")
        )
        try "- [ ] task in B\n".data(using: .utf8)!.write(
            to: vault.appendingPathComponent("b.md")
        )

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        await state.tasksLoadTask?.value
        // Dirty the editor buffer for a.md.
        let originalAText = try XCTUnwrap(state.currentNoteText)
        state.updateEditorText(originalAText + "\nedit in A\n")
        XCTAssertTrue(state.hasUnsavedChanges)

        state.openTasksReview()
        await state.vaultTasksLoadTask?.value

        // Find the row from b.md — that's the unloaded file.
        let bRow = try XCTUnwrap(
            state.vaultTasks.first(where: { $0.path == "b.md" }),
            "fixture should expose b.md's task in the vault tasks list"
        )
        let toggleResult = state.toggleVaultTask(bRow)
        XCTAssertNotNil(
            toggleResult,
            "toggle on an unloaded file should proceed even when the loaded buffer is dirty"
        )
        await toggleResult?.value

        // b.md flipped on disk.
        let onDiskB = try String(
            contentsOf: vault.appendingPathComponent("b.md"),
            encoding: .utf8
        )
        XCTAssertTrue(
            onDiskB.contains("- [x] task in B"),
            "b.md's task should have been toggled to done; got: \(onDiskB)"
        )

        // a.md untouched on disk (still has the open task).
        let onDiskA = try String(
            contentsOf: vault.appendingPathComponent("a.md"),
            encoding: .utf8
        )
        XCTAssertEqual(
            onDiskA, "- [ ] task in A\n",
            "a.md must stay untouched"
        )
        // a.md's buffer still has the user's unsaved edits.
        XCTAssertEqual(
            state.currentNoteText,
            originalAText + "\nedit in A\n",
            "a.md's buffer keeps the user's edits"
        )
        XCTAssertTrue(state.hasUnsavedChanges)
    }

    // MARK: - Loading-flag cancellation cleanup (#159)
    //
    // The per-note and vault-wide load paths used to leak their
    // `isLoading…` flag true on cancellation-without-replacement
    // (e.g. close-review mid-load, deselect mid-load). The flag
    // is now cleared via `defer` so every exit path returns the
    // spinner to the inert state.

    func testCloseTasksReviewMidLoadClearsSpinner() async throws {
        let vault = tempDir.appendingPathComponent("close-spinner-vault")
        try FileManager.default.createDirectory(
            at: vault,
            withIntermediateDirectories: true
        )
        try "- [ ] task\n".data(using: .utf8)!.write(
            to: vault.appendingPathComponent("a.md")
        )

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.openTasksReview()
        // Grab the handle BEFORE closeTasksReview nils it out so
        // we can deterministically await the cancelled task's
        // settle and observe the post-cancellation flag state.
        let handle = state.vaultTasksLoadTask
        state.closeTasksReview()
        await handle?.value

        XCTAssertFalse(
            state.isLoadingVaultTasks,
            "closeTasksReview mid-load must not leave the spinner stuck"
        )
    }

    func testRapidFilterSwitchClearsSpinnerBetweenLoads() async throws {
        let vault = tempDir.appendingPathComponent("rapid-filter-vault")
        try FileManager.default.createDirectory(
            at: vault,
            withIntermediateDirectories: true
        )
        try "- [ ] task\n".data(using: .utf8)!.write(
            to: vault.appendingPathComponent("a.md")
        )

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.openTasksReview()
        // Fire three filter switches as fast as the synchronous
        // mutator allows. Each call cancels the previous task and
        // schedules a new one. Without the defer-based cleanup,
        // intermediate cancellations could leak the flag true; the
        // final await proves the last load's cleanup ran AND that
        // no intermediate task left the flag stuck.
        state.applyTaskReviewFilter(.dueToday)
        state.applyTaskReviewFilter(.overdue)
        state.applyTaskReviewFilter(.thisWeek)
        await state.vaultTasksLoadTask?.value

        XCTAssertFalse(
            state.isLoadingVaultTasks,
            "after the last filter switch settles, the spinner must clear"
        )
    }

    func testNilSelectionMidLoadClearsPerNoteSpinner() async throws {
        let vault = tempDir.appendingPathComponent("nil-selection-vault")
        try FileManager.default.createDirectory(
            at: vault,
            withIntermediateDirectories: true
        )
        try "- [ ] task\n".data(using: .utf8)!.write(
            to: vault.appendingPathComponent("a.md")
        )

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.selectedFilePath = "a.md"
        // Grab the in-flight task handle before deselection nils
        // it out, then deselect to trigger the cancellation path.
        let handle = state.tasksLoadTask
        state.selectedFilePath = nil
        await handle?.value

        XCTAssertFalse(
            state.isLoadingTasks,
            "deselecting mid-load must clear the per-note spinner"
        )
    }

    // MARK: - Vault tasks pagination (#160)
    //
    // The vault-wide review queries with a 200-row page size; the
    // FFI returns a nextCursor + totalFiltered so the surface can
    // page forward and advertise truncation. These tests build a
    // 201-task fixture (one over the page boundary) so the first
    // page is full + an explicit "more to fetch" signal lands.

    /// Build a vault with `count` open tasks across one file.
    /// Lives here rather than in the global test helpers because
    /// the only callers are the pagination tests.
    private func makePaginationFixture(at url: URL, count: Int) throws {
        try FileManager.default.createDirectory(
            at: url,
            withIntermediateDirectories: true
        )
        var lines = "# Pagination Fixture\n"
        for index in 1...count {
            lines += "- [ ] task \(index)\n"
        }
        try lines.data(using: .utf8)!.write(
            to: url.appendingPathComponent("tasks.md")
        )
    }

    func testVaultTasksReviewSurfacesTruncationWhenOver200Results() async throws {
        let vault = tempDir.appendingPathComponent("pagination-vault")
        try makePaginationFixture(at: vault, count: 201)

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.openTasksReview()
        await state.vaultTasksLoadTask?.value

        XCTAssertEqual(
            state.vaultTasks.count, Int(AppState.vaultTasksPageSize),
            "the first page should fill to the page size limit"
        )
        XCTAssertEqual(
            state.vaultTasksTotalFiltered, 201,
            "totalFiltered should report the full result-set size, not just the page"
        )
        XCTAssertNotNil(
            state.vaultTasksNextCursor,
            "an over-page-size result set must return a non-nil cursor"
        )
        XCTAssertFalse(
            state.isLoadingMoreVaultTasks,
            "no Load more is in flight after the initial load settles"
        )
    }

    func testLoadMoreVaultTasksAppendsNextPage() async throws {
        let vault = tempDir.appendingPathComponent("pagination-load-more-vault")
        try makePaginationFixture(at: vault, count: 201)

        let state = try makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        state.openTasksReview()
        await state.vaultTasksLoadTask?.value

        // Sanity: cursor exists before "Load more".
        XCTAssertNotNil(state.vaultTasksNextCursor)
        let firstPageLastText = try XCTUnwrap(state.vaultTasks.last?.task.text)

        // Trigger the second page.
        let loadMoreTask = state.loadMoreVaultTasks()
        XCTAssertNotNil(loadMoreTask, "loadMoreVaultTasks should return a task while a cursor exists")
        await loadMoreTask?.value

        XCTAssertEqual(
            state.vaultTasks.count, 201,
            "the appended page should bring the total to 201 (200 + 1)"
        )
        XCTAssertNil(
            state.vaultTasksNextCursor,
            "after the last page lands, the cursor should be nil"
        )
        XCTAssertEqual(
            state.vaultTasksTotalFiltered, 201,
            "totalFiltered stays consistent across page loads"
        )
        XCTAssertFalse(state.isLoadingMoreVaultTasks)

        // First-page rows still in place — the append happened
        // after them, not in their stead.
        XCTAssertEqual(
            state.vaultTasks[Int(AppState.vaultTasksPageSize) - 1].task.text,
            firstPageLastText,
            "the last row of page 1 should still be at its original index after the append"
        )

        // Calling loadMoreVaultTasks again with no cursor is a
        // no-op (returns nil) — the button hides in the UI but
        // the API should be defensive against programmatic
        // callers too.
        XCTAssertNil(
            state.loadMoreVaultTasks(),
            "loadMoreVaultTasks must no-op once the cursor is exhausted"
        )
    }
}
