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
