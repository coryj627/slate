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
