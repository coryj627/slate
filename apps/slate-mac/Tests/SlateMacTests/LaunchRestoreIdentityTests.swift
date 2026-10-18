// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #1442: launch-restore never silently opens a folder that is not the
/// remembered vault. An entry recorded since #1442 carries the folder's
/// identity and is compared with what is at its path now; an older entry has
/// none, and only a folder holding nothing but `.slate` is refused for it.
/// Either way the "Vault Not Found" alert says what was found, and nothing
/// deletes the folder.
///
/// Every fixture lives in a private temp directory; nothing is trashed
/// (#1455).
@MainActor
final class LaunchRestoreIdentityTests: XCTestCase {

    private var tempDir: URL!
    private var storeFile: URL!
    private var suiteNames: [String] = []

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-launch-identity-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: false)
        storeFile = tempDir.appendingPathComponent("recent-vaults.json")
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        for name in suiteNames {
            UserDefaults.standard.removePersistentDomain(forName: name)
        }
    }

    /// A fresh app over the shared recents file: each call is one launch.
    private func launch(seed: [RecentVault] = []) throws -> AppState {
        let store = RecentVaultsStore(fileURL: storeFile)
        if !seed.isEmpty { try store.save(seed) }
        let name = "slate-launch-identity-\(UUID().uuidString)"
        suiteNames.append(name)
        return AppState(
            recentsStore: store, externalOpener: { _ in true },
            preferencesStore: PreferencesStore(defaults: UserDefaults(suiteName: name)!))
    }

    private func makeFolder(_ name: String, note: Bool) throws -> URL {
        let url = tempDir.appendingPathComponent(name, isDirectory: true)
        try FileManager.default.createDirectory(at: url, withIntermediateDirectories: false)
        if note {
            try Data("# Note\n".utf8).write(to: url.appendingPathComponent("note.md"))
        }
        return url
    }

    /// What a save that recreates a renamed vault's old path leaves there.
    private func makeGhost(_ name: String) throws -> URL {
        let url = try makeFolder(name, note: false)
        let slate = url.appendingPathComponent(".slate", isDirectory: true)
        try FileManager.default.createDirectory(at: slate, withIntermediateDirectories: false)
        try Data("{}".utf8).write(to: slate.appendingPathComponent("workspace.json"))
        return url
    }

    /// Opens `vault` in one launch and closes it, so the recents file holds
    /// the entry that open recorded.
    private func openAndClose(_ vault: URL) async throws -> RecentVault {
        let state = try launch()
        state.openVault(at: vault)
        XCTAssertTrue(state.isVaultOpen, "premise: \(vault.lastPathComponent) opened")
        await state.scanTask?.value
        state.closeVault()
        return try XCTUnwrap(state.recentVaults.first)
    }

    private func exists(_ url: URL) -> Bool {
        FileManager.default.fileExists(atPath: url.path)
    }

    // MARK: The issue's facts

    func testAnOldEntryWhoseFolderHoldsOnlyDotSlateIsNotRestored() throws {
        let ghost = try makeGhost("Notes")
        let entry = RecentVault(path: ghost.path, displayName: "Notes", lastOpenedMs: 1)
        let state = try launch(seed: [entry])
        XCTAssertFalse(state.willRestoreVaultOnLaunch, "Welcome keeps its announcement")

        state.restoreMostRecentVaultOnLaunch(optionHeld: false)

        XCTAssertFalse(state.isVaultOpen, "a ghost was restored as the vault")
        XCTAssertEqual(
            state.missingRecentVaultAlert,
            AppState.MissingRecentVaultAlert(entry: entry, mismatch: .onlySlateFolder))
        XCTAssertTrue(exists(ghost.appendingPathComponent(".slate/workspace.json")), "untouched")
    }

    func testARememberedPathNowHoldingAnotherFolderIsNotRestored() async throws {
        let vault = try makeFolder("Notes", note: true)
        let recorded = try await openAndClose(vault)
        XCTAssertNotNil(recorded.rootIdentity, "the open recorded which folder it was")
        try FileManager.default.moveItem(at: vault, to: tempDir.appendingPathComponent("Notes 2026"))
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: false)
        try Data("# Other\n".utf8).write(to: vault.appendingPathComponent("other.md"))

        let relaunched = try launch()
        relaunched.restoreMostRecentVaultOnLaunch(optionHeld: false)

        XCTAssertFalse(relaunched.isVaultOpen, "another folder was restored as the vault")
        XCTAssertEqual(
            relaunched.missingRecentVaultAlert,
            AppState.MissingRecentVaultAlert(entry: recorded, mismatch: .differentFolder))
        XCTAssertFalse(
            exists(vault.appendingPathComponent(".slate")), "nothing opened or wrote there")
    }

    func testARememberedPathNowHoldingAGhostIsNotRestored() async throws {
        let vault = try makeFolder("Notes", note: true)
        let recorded = try await openAndClose(vault)
        try FileManager.default.moveItem(at: vault, to: tempDir.appendingPathComponent("Notes 2026"))
        _ = try makeGhost("Notes")

        let relaunched = try launch()
        relaunched.restoreMostRecentVaultOnLaunch(optionHeld: false)

        XCTAssertFalse(relaunched.isVaultOpen)
        XCTAssertEqual(relaunched.missingRecentVaultMismatch, .onlySlateFolder)
        XCTAssertEqual(relaunched.missingRecentVault, recorded)
    }

    func testAnOldEntryWithoutAnIdentityStillRestoresARealVaultAndRecordsOne() async throws {
        let vault = try makeFolder("Real", note: true)
        let entry = RecentVault(path: vault.path, displayName: "Real", lastOpenedMs: 1)
        let state = try launch(seed: [entry])

        state.restoreMostRecentVaultOnLaunch(optionHeld: false)

        XCTAssertTrue(state.isVaultOpen)
        XCTAssertEqual(state.currentVaultURL?.path, vault.path)
        XCTAssertNil(state.missingRecentVault)
        await state.scanTask?.value
        let identity = try XCTUnwrap(AppState.vaultRootIdentity(of: vault))
        let recorded = try XCTUnwrap(state.recentVaults.first?.rootIdentity, "recorded going forward")
        XCTAssertEqual(recorded.device, identity.device)
        XCTAssertEqual(recorded.inode, identity.inode)
    }

    // MARK: The same folder still restores

    func testTheSameFolderRestoresOnTheNextLaunch() async throws {
        let vault = try makeFolder("Kept", note: true)
        _ = try await openAndClose(vault)

        let relaunched = try launch()
        relaunched.restoreMostRecentVaultOnLaunch(optionHeld: false)

        XCTAssertTrue(relaunched.isVaultOpen)
        XCTAssertNil(relaunched.missingRecentVault)
        await relaunched.scanTask?.value
    }

    /// A vault the user opened while it was empty holds only `.slate`
    /// afterwards; its recorded identity proves it is that folder.
    func testAnEmptyVaultWithARecordedIdentityStillRestores() async throws {
        let vault = try makeFolder("Empty", note: false)
        _ = try await openAndClose(vault)
        XCTAssertTrue(RecentVault.holdsOnlySlateFolder(vault), "premise: looks like a ghost")

        let relaunched = try launch()
        relaunched.restoreMostRecentVaultOnLaunch(optionHeld: false)

        XCTAssertTrue(relaunched.isVaultOpen)
        await relaunched.scanTask?.value
    }

    // MARK: The alert's choices

    func testOpenAnywayOpensTheFolderAndRemembersItForTheNextLaunch() async throws {
        let vault = try makeFolder("Notes", note: true)
        _ = try await openAndClose(vault)
        try FileManager.default.moveItem(at: vault, to: tempDir.appendingPathComponent("aside"))
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: false)
        let relaunched = try launch()
        relaunched.restoreMostRecentVaultOnLaunch(optionHeld: false)
        let alert = try XCTUnwrap(relaunched.missingRecentVaultAlert)

        // What the alert's Open Anyway button runs.
        relaunched.missingRecentVault = nil
        relaunched.openRecent(alert.entry)

        XCTAssertTrue(relaunched.isVaultOpen)
        XCTAssertEqual(relaunched.currentVaultURL?.path, vault.path)
        await relaunched.scanTask?.value
        relaunched.closeVault()
        let third = try launch()
        third.restoreMostRecentVaultOnLaunch(optionHeld: false)
        XCTAssertTrue(third.isVaultOpen, "the folder opened anyway is the one remembered now")
        await third.scanTask?.value
    }

    func testRemovingTheEntryNeverTouchesTheFolder() throws {
        let ghost = try makeGhost("Notes")
        let entry = RecentVault(path: ghost.path, displayName: "Notes", lastOpenedMs: 1)
        let state = try launch(seed: [entry])
        state.restoreMostRecentVaultOnLaunch(optionHeld: false)

        // What the alert's Remove from Recent Vaults button runs.
        state.removeRecent(path: entry.path)
        state.missingRecentVault = nil

        XCTAssertTrue(state.recentVaults.isEmpty)
        XCTAssertNil(state.missingRecentVaultMismatch, "the finding goes with the entry")
        XCTAssertTrue(exists(ghost.appendingPathComponent(".slate/workspace.json")))
    }

    // MARK: What the alert says

    func testTheAlertSaysWhatWasFound() {
        let entry = RecentVault(path: "/Users/someone/Notes", displayName: "Notes", lastOpenedMs: 1)
        XCTAssertEqual(
            WelcomeView.missingVaultMessage(for: entry, mismatch: nil),
            "Notes is no longer at /Users/someone/Notes. Remove it from your recent vaults?",
            "the original sentence is unchanged when nothing is there")
        XCTAssertEqual(
            WelcomeView.missingVaultMessage(for: entry, mismatch: .onlySlateFolder),
            "Slate didn’t reopen Notes. The folder at /Users/someone/Notes holds only "
                + "Slate’s .slate settings folder, which can be left behind when a vault is "
                + "renamed or moved. Remove it from your recent vaults?")
        XCTAssertEqual(
            WelcomeView.missingVaultMessage(for: entry, mismatch: .differentFolder),
            "Slate didn’t reopen Notes, because it can’t confirm that the folder at "
                + "/Users/someone/Notes is the one it last opened. The folder may have been "
                + "replaced, or its drive reconnected. Remove it from your recent vaults?")
    }

    // MARK: The decision and its rule

    func testTheLaunchDecisionRefusesWhatTheMismatchNames() {
        let entry = RecentVault(path: "/tmp/alpha", displayName: "alpha", lastOpenedMs: 2)
        XCTAssertEqual(
            AppState.launchDestination(
                recents: [entry], optionHeld: false, vaultExists: { _ in true },
                mismatch: { _ in .onlySlateFolder }),
            .notRestored(entry, .onlySlateFolder))
        XCTAssertEqual(
            AppState.launchDestination(
                recents: [entry], optionHeld: false, vaultExists: { _ in true },
                mismatch: { _ in .differentFolder }),
            .notRestored(entry, .differentFolder))
        var consulted = false
        XCTAssertEqual(
            AppState.launchDestination(
                recents: [entry], optionHeld: false, vaultExists: { _ in false },
                mismatch: { _ in
                    consulted = true
                    return .differentFolder
                }),
            .notFound(entry), "nothing at the path is the original not-found flow")
        XCTAssertFalse(consulted, "a missing path is never inspected")
    }

    func testTheSameFolderRuleIsTheJournals() {
        typealias Identity = RecentVault.RootIdentity
        let recorded = Identity(device: 1, inode: 10, birth: 100, volume: "V")
        XCTAssertTrue(recorded.identifiesSameFolder(as: recorded))
        XCTAssertTrue(
            recorded.identifiesSameFolder(
                as: Identity(device: 1, inode: 10, birth: nil, volume: nil)),
            "same device and inode, birth unknown: the same folder")
        XCTAssertFalse(
            recorded.identifiesSameFolder(
                as: Identity(device: 1, inode: 10, birth: 101, volume: "V")),
            "a replacement that reuses the inode")
        XCTAssertFalse(
            recorded.identifiesSameFolder(
                as: Identity(device: 1, inode: 11, birth: 100, volume: "V")),
            "another inode")
        XCTAssertTrue(
            recorded.identifiesSameFolder(
                as: Identity(device: 2, inode: 10, birth: 100, volume: "V")),
            "a volume mounted again under another device number")
        XCTAssertFalse(
            recorded.identifiesSameFolder(
                as: Identity(device: 2, inode: 10, birth: 100, volume: nil)),
            "another device and no volume to prove it is the same")
        XCTAssertFalse(
            recorded.identifiesSameFolder(
                as: Identity(device: 2, inode: 10, birth: 100, volume: "W")),
            "another volume")
    }

    func testOnlyAFolderHoldingNothingButARealDotSlateIsAGhost() throws {
        let ghost = try makeGhost("ghost")
        XCTAssertTrue(RecentVault.holdsOnlySlateFolder(ghost))
        try Data().write(to: ghost.appendingPathComponent(".DS_Store"))
        XCTAssertTrue(RecentVault.holdsOnlySlateFolder(ghost), "Finder's .DS_Store is not content")

        let withNote = try makeGhost("with-note")
        try Data("# n\n".utf8).write(to: withNote.appendingPathComponent("n.md"))
        XCTAssertFalse(RecentVault.holdsOnlySlateFolder(withNote))

        XCTAssertFalse(RecentVault.holdsOnlySlateFolder(try makeFolder("empty", note: false)))

        let slateFile = try makeFolder("slate-file", note: false)
        try Data().write(to: slateFile.appendingPathComponent(".slate"))
        XCTAssertFalse(RecentVault.holdsOnlySlateFolder(slateFile))

        let slateLink = try makeFolder("slate-link", note: false)
        try FileManager.default.createSymbolicLink(
            at: slateLink.appendingPathComponent(".slate"),
            withDestinationURL: ghost.appendingPathComponent(".slate"))
        XCTAssertFalse(RecentVault.holdsOnlySlateFolder(slateLink), "a link is the user's own")
    }

    // MARK: The recents file

    func testAnEntryWithoutAnIdentityKeepsTheOldShapeAndOldFilesStillRead() throws {
        let old = #"[{"displayName":"Old","lastOpenedMs":7,"path":"/tmp/old"}]"#
        try Data(old.utf8).write(to: storeFile)
        let store = RecentVaultsStore(fileURL: storeFile)
        XCTAssertEqual(
            store.load(), [RecentVault(path: "/tmp/old", displayName: "Old", lastOpenedMs: 7)])

        try store.save(store.load())
        let saved = try String(contentsOf: storeFile, encoding: .utf8)
        XCTAssertFalse(saved.contains("rootIdentity"), saved)

        let identity = RecentVault.RootIdentity(
            device: 16_777_233, inode: 9_007_199_254_740_993, birth: 1_760_000_000_123_456_789,
            volume: "0A1B2C3D-0000-4000-8000-000000000000")
        let entry = RecentVault(
            path: "/tmp/new", displayName: "New", lastOpenedMs: 8, rootIdentity: identity)
        try store.save([entry])
        XCTAssertEqual(store.load(), [entry], "every field round-trips exactly")
    }
}
