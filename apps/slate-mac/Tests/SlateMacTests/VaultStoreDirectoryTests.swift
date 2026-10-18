// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #1442: a per-vault store never creates the vault root or any folder above
/// it. It makes at most `.slate`, and only inside a root that still exists
/// and still has the identity the open admitted (`VaultStoreDirectory`).
///
/// Every fixture lives in a private temp directory; nothing is trashed
/// (#1455).
final class VaultStoreDirectoryTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-vault-store-dir-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: false)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    private func makeRoot(_ name: String) throws -> URL {
        let root = tempDir.appendingPathComponent(name, isDirectory: true)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: false)
        try Data("# Note\n".utf8).write(to: root.appendingPathComponent("note.md"))
        return root
    }

    private func exists(_ url: URL) -> Bool {
        FileManager.default.fileExists(atPath: url.path)
    }

    /// One of the four stores, saved the way its caller saves it.
    private struct StoreUnderTest {
        let name: String
        let file: String
        /// `PrefsJsonStore` and `GraphConfigStore` report through
        /// `PrefsJsonStoreError.writeFailed`, carrying the refusal's sentence.
        let wrapsRefusal: Bool
        let save: (URL, VaultStoreDirectory.RootIdentity?) throws -> Void

        func expectedError(
            _ refusal: VaultStoreDirectory.Refusal, root: URL
        ) -> Error {
            guard wrapsRefusal else { return refusal }
            return PrefsJsonStoreError.writeFailed(
                path: root.appendingPathComponent(".slate").appendingPathComponent(file).path,
                reason: refusal.localizedDescription)
        }
    }

    private static let stores: [StoreUnderTest] = [
        StoreUnderTest(name: "WorkspaceStore", file: "workspace.json", wrapsRefusal: false) {
            try WorkspaceStore(vaultRoot: $0, rootIdentity: $1)
                .save(WorkspaceStore.snapshot(of: WorkspaceModel()))
        },
        StoreUnderTest(name: "SearchRecentsStore", file: "search-recents.json", wrapsRefusal: false) {
            try SearchRecentsStore(vaultRoot: $0, rootIdentity: $1).save(["budget"])
        },
        StoreUnderTest(name: "PrefsJsonStore", file: "prefs.json", wrapsRefusal: true) {
            try PrefsJsonStore(vaultRoot: $0, rootIdentity: $1).writeBibliographyPrefs(
                BibliographyPrefs(sources: [], defaultStyle: "apa.csl", additionalStyles: []))
        },
        StoreUnderTest(name: "GraphConfigStore", file: "graph.json", wrapsRefusal: true) {
            try GraphConfigStore(vaultRoot: $0, rootIdentity: $1).write(.default)
        },
    ]

    private func assertRefused(
        _ store: StoreUnderTest, root: URL, identity: VaultStoreDirectory.RootIdentity?,
        with refusal: VaultStoreDirectory.Refusal, file: StaticString = #filePath,
        line: UInt = #line
    ) {
        XCTAssertThrowsError(try store.save(root, identity), store.name, file: file, line: line) {
            error in
            let expected = store.expectedError(refusal, root: root)
            XCTAssertEqual(
                String(describing: error), String(describing: expected),
                "\(store.name) must refuse with the helper's reason", file: file, line: line)
        }
    }

    // MARK: The issue's facts

    func testEveryStoreSavedAfterItsRootIsRenamedLeavesNothingAtTheOldPath() throws {
        for store in Self.stores {
            let root = try makeRoot("renamed-\(store.name)")
            let identity = try XCTUnwrap(AppState.vaultRootIdentity(of: root))
            let moved = tempDir.appendingPathComponent("moved-\(store.name)")
            try FileManager.default.moveItem(at: root, to: moved)

            assertRefused(
                store, root: root, identity: identity,
                with: .vaultRootMissing(path: root.path))
            XCTAssertFalse(
                exists(root), "\(store.name) recreated the renamed vault's old path")
            XCTAssertFalse(
                exists(moved.appendingPathComponent(".slate")),
                "\(store.name) wrote nowhere: the moved vault gained nothing either")
        }
    }

    func testEveryStoreRefusesARootReplacedByAnotherFolderAtTheSamePath() throws {
        for (index, store) in Self.stores.enumerated() {
            let root = try makeRoot("replaced-\(store.name)")
            let identity = try XCTUnwrap(AppState.vaultRootIdentity(of: root))
            try FileManager.default.moveItem(
                at: root, to: tempDir.appendingPathComponent("original-\(store.name)"))
            try FileManager.default.createDirectory(at: root, withIntermediateDirectories: false)
            // Half the newcomers already hold a `.slate` of their own: the
            // refusal must not depend on whether there is one to write into.
            let newcomerSlate = root.appendingPathComponent(".slate", isDirectory: true)
            if index.isMultiple(of: 2) {
                try FileManager.default.createDirectory(
                    at: newcomerSlate, withIntermediateDirectories: false)
            }

            assertRefused(
                store, root: root, identity: identity,
                with: .vaultRootReplaced(path: root.path))
            XCTAssertFalse(
                exists(newcomerSlate.appendingPathComponent(store.file)),
                "\(store.name) wrote into the folder that replaced the vault")
            XCTAssertEqual(
                exists(newcomerSlate), index.isMultiple(of: 2),
                "\(store.name) made a .slate inside the newcomer")
        }
    }

    func testEveryStoreStillSavesIntoItsRootWithOrWithoutDotSlate() throws {
        for store in Self.stores {
            let root = try makeRoot("normal-\(store.name)")
            let identity = AppState.vaultRootIdentity(of: root)
            let file = root.appendingPathComponent(".slate").appendingPathComponent(store.file)

            XCTAssertNoThrow(try store.save(root, identity), "\(store.name), .slate missing")
            XCTAssertTrue(exists(file), "\(store.name) made .slate and saved")
            XCTAssertNoThrow(try store.save(root, identity), "\(store.name), .slate present")
            XCTAssertTrue(exists(file))
        }
    }

    func testAStoreWithNoAdmittedIdentityRefusesAndMakesNothing() throws {
        for store in Self.stores {
            let root = try makeRoot("unverified-\(store.name)")

            assertRefused(
                store, root: root, identity: nil,
                with: .vaultRootUnverified(path: root.path))
            XCTAssertFalse(exists(root.appendingPathComponent(".slate")), store.name)
        }
    }

    // MARK: The helper

    func testPrepareMakesOnlyDotSlateInsideTheVerifiedRoot() throws {
        let root = try makeRoot("prepare")
        let slate = try VaultStoreDirectory.prepare(
            vaultRoot: root, expectedRootIdentity: AppState.vaultRootIdentity(of: root))

        XCTAssertEqual(slate.standardizedFileURL, root.appendingPathComponent(".slate").standardizedFileURL)
        XCTAssertEqual(
            try FileManager.default.contentsOfDirectory(atPath: root.path).sorted(),
            [".slate", "note.md"], "one folder made, nothing else")
        XCTAssertEqual(
            try FileManager.default.contentsOfDirectory(atPath: slate.path), [],
            "prepare writes no file")
    }

    func testPrepareRefusesADotSlateThatIsNotAFolder() throws {
        let root = try makeRoot("slate-file")
        let slate = root.appendingPathComponent(".slate")
        try Data().write(to: slate)

        XCTAssertThrowsError(
            try VaultStoreDirectory.prepare(
                vaultRoot: root, expectedRootIdentity: AppState.vaultRootIdentity(of: root))
        ) { error in
            XCTAssertEqual(
                error as? VaultStoreDirectory.Refusal,
                .slateDirectoryUnavailable(path: slate.path, reason: "it is not a folder"))
        }
    }

    func testAFileAtTheVaultsPathIsAMissingVaultNotAFolderToMake() throws {
        let root = try makeRoot("became-a-file")
        let identity = AppState.vaultRootIdentity(of: root)
        try FileManager.default.moveItem(at: root, to: tempDir.appendingPathComponent("aside"))
        try Data("not a folder".utf8).write(to: root)

        XCTAssertThrowsError(
            try VaultStoreDirectory.prepare(vaultRoot: root, expectedRootIdentity: identity)
        ) { error in
            XCTAssertEqual(
                error as? VaultStoreDirectory.Refusal, .vaultRootMissing(path: root.path))
        }
    }

    /// The log lines interpolate the error and the Settings error shows the
    /// wrapped reason: both read the same sentence.
    func testARefusalReadsAsOneSentenceInTheLogAndTheSettingsError() {
        let refusal = VaultStoreDirectory.Refusal.vaultRootMissing(path: "/Users/someone/Notes")
        XCTAssertEqual(
            "\(refusal)",
            "The vault folder is no longer at /Users/someone/Notes, so Slate didn’t "
                + "recreate it there.")
        XCTAssertEqual(refusal.localizedDescription, "\(refusal)")
        XCTAssertEqual(
            PrefsJsonStoreError.writeFailed(
                path: "/Users/someone/Notes/.slate/prefs.json",
                reason: refusal.localizedDescription
            ).localizedDescription,
            "Couldn't write preferences at /Users/someone/Notes/.slate/prefs.json: The "
                + "vault folder is no longer at /Users/someone/Notes, so Slate didn’t "
                + "recreate it there.")
    }
}

/// #1442 through the app's own save call sites: each store is handed the
/// identity the open admitted.
@MainActor
final class VaultRootRenameAppStateTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-vault-rename-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: false)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    private func openVault(_ name: String) async throws -> (AppState, URL) {
        let vault = tempDir.appendingPathComponent(name, isDirectory: true)
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: false)
        try Data("# Note\n".utf8).write(to: vault.appendingPathComponent("note.md"))
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recent-vaults.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        XCTAssertTrue(state.isVaultOpen, "premise: the vault opened")
        await state.scanTask?.value
        return (state, vault)
    }

    /// Each store's save, through the call site the app uses for it.
    private func saveEveryStore(_ state: AppState, vault: URL) async {
        state.saveWorkspaceLayout()
        state.recordSearchRecent("budget")
        await state.applyBibliographyPrefs(
            BibliographyPrefs(sources: [], defaultStyle: "apa.csl", additionalStyles: []))
        state.setGraphMode(.diagram)
        await state.graphConfigSaveTasks[vault]?.value
    }

    private static let storeFiles = [
        "workspace.json", "search-recents.json", "prefs.json", "graph.json",
    ]

    func testRenamingTheOpenVaultThenSavingEveryStoreRecreatesNothingAtTheOldPath() async throws {
        let (state, vault) = try await openVault("open-then-renamed")
        let moved = tempDir.appendingPathComponent("renamed-away")
        try FileManager.default.moveItem(at: vault, to: moved)

        await saveEveryStore(state, vault: vault)

        XCTAssertFalse(
            FileManager.default.fileExists(atPath: vault.path),
            "a save recreated the renamed vault's old path")
        // Where the app already shows a store error, the refusal shows there.
        let shown = try XCTUnwrap(state.bibliographySettingsError)
        XCTAssertTrue(
            shown.contains("The vault folder is no longer at \(vault.path)"), shown)
    }

    func testAFolderPutWhereTheOpenVaultWasReceivesNoStoreWrite() async throws {
        let (state, vault) = try await openVault("open-then-replaced")
        try FileManager.default.moveItem(at: vault, to: tempDir.appendingPathComponent("away"))
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: false)

        await saveEveryStore(state, vault: vault)

        XCTAssertFalse(
            FileManager.default.fileExists(atPath: vault.appendingPathComponent(".slate").path),
            "a store wrote into the folder that replaced the open vault")
        let shown = try XCTUnwrap(state.bibliographySettingsError)
        XCTAssertTrue(shown.contains("is no longer the vault Slate opened"), shown)
    }

    /// The one deferred write: a graph save scheduled in vault A runs after
    /// the switch to B, so it must carry A's identity with A's root. Paired
    /// with B's (from the open), A's root would refuse and the edit would be
    /// lost.
    func testAGraphSaveScheduledBeforeASwitchStillLandsInItsOwnVault() async throws {
        let (state, vaultA) = try await openVault("graph-switch-a")
        state.setGraphMode(.diagram)
        let pending = try XCTUnwrap(state.graphConfigSaveTasks[vaultA], "premise: a save is pending")
        let vaultB = tempDir.appendingPathComponent("graph-switch-b", isDirectory: true)
        try FileManager.default.createDirectory(at: vaultB, withIntermediateDirectories: false)
        try Data("# B\n".utf8).write(to: vaultB.appendingPathComponent("b.md"))

        state.openVault(at: vaultB)
        XCTAssertEqual(state.currentVaultURL?.path, vaultB.path, "premise: switched before the save ran")
        await state.scanTask?.value
        await pending.value

        let saved = try GraphConfigStore(
            vaultRoot: vaultA, rootIdentity: AppState.vaultRootIdentity(of: vaultA)
        ).read()
        XCTAssertEqual(saved.mode, .diagram, "A's edit landed in A")
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vaultB.appendingPathComponent(".slate/graph.json").path),
            "B received nothing from A's save")
    }

    /// The regression guard for the call sites: an open vault that stays put
    /// still gets every store's file, so each site passes a real identity.
    func testAnOpenVaultThatStaysPutStillGetsEveryStoresFile() async throws {
        let (state, vault) = try await openVault("stays-put")

        await saveEveryStore(state, vault: vault)

        for file in Self.storeFiles {
            XCTAssertTrue(
                FileManager.default.fileExists(
                    atPath: vault.appendingPathComponent(".slate/\(file)").path),
                "\(file) was not saved")
        }
        XCTAssertNil(state.bibliographySettingsError)
    }
}
