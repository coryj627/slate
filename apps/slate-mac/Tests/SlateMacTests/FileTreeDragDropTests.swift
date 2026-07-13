// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import UniformTypeIdentifiers
import XCTest

@testable import SlateMac

/// #870: file-URL drag flavors — drag notes OUT to Finder and accept file
/// drops IN (external ⇒ import, in-vault ⇒ move).
///
/// The NSItemProvider plumbing and the `.onDrop` wiring aren't drivable from
/// XCTest, so these tests pin the extracted, load-bearing seams:
///  - `makeDragProvider` registers BOTH the private type AND `public.file-url`,
///  - the pure `fileURLDropAction` import-vs-move decision (+ its no-op guards),
///  - `importEntry` copies an external file in (reusing the collision surface),
///  - and an in-vault file-URL drop resolves to a move that lands on disk.
@MainActor
final class FileTreeDragDropTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("dnd-fileurl-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    private func makeVault(files: [String]) async throws -> (AppState, URL) {
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for rel in files {
            let url = vault.appendingPathComponent(rel)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try "# \((rel as NSString).lastPathComponent)\n".write(
                to: url, atomically: true, encoding: .utf8)
        }
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return (state, vault)
    }

    private func exists(_ vault: URL, _ rel: String) -> Bool {
        FileManager.default.fileExists(atPath: vault.appendingPathComponent(rel).path)
    }

    // MARK: - Drag payload carries public.file-url (drag OUT)

    func testDragProviderCarriesBothPrivateTypeAndFileURL() {
        let fileURL = URL(fileURLWithPath: "/Vaults/demo/Notes/idea.md")
        let provider = FileTreeSidebar.makeDragProvider(
            nodePath: "Notes/idea.md", fileURL: fileURL)
        let ids = provider.registeredTypeIdentifiers
        XCTAssertTrue(
            ids.contains(FileTreeSidebar.nodeUTType),
            "the private own-process type is still present for precise intra-tree moves")
        XCTAssertTrue(
            ids.contains(UTType.fileURL.identifier),
            "public.file-url is carried so the item can be dragged OUT to Finder")
        XCTAssertEqual(
            provider.suggestedName, "idea.md", "the drop gets a sensible file name")
    }

    /// The file-URL flavor round-trips the real on-disk URL (what Finder reads
    /// to copy the referenced file).
    func testDragProviderFileURLLoadsBackTheURL() async throws {
        let fileURL = URL(fileURLWithPath: "/Vaults/demo/idea.md")
        let provider = FileTreeSidebar.makeDragProvider(nodePath: "idea.md", fileURL: fileURL)

        let loaded: URL = try await withCheckedThrowingContinuation { cont in
            _ = provider.loadObject(ofClass: URL.self) { url, err in
                if let url { cont.resume(returning: url) }
                else { cont.resume(throwing: err ?? URLError(.badURL)) }
            }
        }
        XCTAssertEqual(loaded.standardizedFileURL.path, fileURL.standardizedFileURL.path)
    }

    /// No vault URL (welcome screen edge) → the file-URL flavor is simply
    /// omitted; the private type still registers so nothing crashes.
    func testDragProviderWithoutFileURLStillRegistersPrivateType() {
        let provider = FileTreeSidebar.makeDragProvider(nodePath: "a.md", fileURL: nil)
        XCTAssertEqual(provider.registeredTypeIdentifiers, [FileTreeSidebar.nodeUTType])
    }

    // MARK: - Pure drop decision: import vs move

    func testExternalFileURLResolvesToImport() {
        let vault = URL(fileURLWithPath: "/Vaults/demo")
        let external = URL(fileURLWithPath: "/Users/me/Downloads/clip.md")
        let action = AppState.fileURLDropAction(
            url: external, vaultURL: vault, destinationFolder: "Notes", isDirectory: false)
        XCTAssertEqual(action, .importFile(url: external, into: "Notes"))
    }

    func testInVaultFileURLResolvesToMove() {
        let vault = URL(fileURLWithPath: "/Vaults/demo")
        let inside = vault.appendingPathComponent("a.md")
        let action = AppState.fileURLDropAction(
            url: inside, vaultURL: vault, destinationFolder: "dest", isDirectory: false)
        XCTAssertEqual(action, .move(path: "a.md", isDirectory: false, to: "dest"))
    }

    func testInVaultDropAlreadyInDestinationIsNoOp() {
        let vault = URL(fileURLWithPath: "/Vaults/demo")
        let inside = vault.appendingPathComponent("dest/a.md")
        // Already directly in "dest" → no-op (same guard as the private path).
        let action = AppState.fileURLDropAction(
            url: inside, vaultURL: vault, destinationFolder: "dest", isDirectory: false)
        XCTAssertEqual(action, .none)
    }

    func testFolderDropIntoOwnSubtreeIsNoOp() {
        let vault = URL(fileURLWithPath: "/Vaults/demo")
        let folder = vault.appendingPathComponent("parent")
        // Dropping "parent" into "parent/child" is a folder-into-own-subtree.
        let action = AppState.fileURLDropAction(
            url: folder, vaultURL: vault, destinationFolder: "parent/child", isDirectory: true)
        XCTAssertEqual(action, .none)
    }

    func testVaultRelativePathClassification() {
        let vault = URL(fileURLWithPath: "/Vaults/demo")
        XCTAssertEqual(
            AppState.vaultRelativePath(
                of: vault.appendingPathComponent("Notes/a.md"), vaultURL: vault),
            "Notes/a.md")
        XCTAssertNil(
            AppState.vaultRelativePath(
                of: URL(fileURLWithPath: "/elsewhere/a.md"), vaultURL: vault),
            "an external file is not vault-relative")
        XCTAssertNil(
            AppState.vaultRelativePath(of: vault, vaultURL: vault),
            "the vault root itself is not a movable entry")
        XCTAssertNil(
            AppState.vaultRelativePath(of: vault.appendingPathComponent("a.md"), vaultURL: nil),
            "no open vault → nothing is vault-relative")
    }

    /// #870 Codex round 1 (F3): containment is FILESYSTEM-aware — a file
    /// reached through a symlinked path still classifies as in-vault (→ an
    /// undoable move), not external (→ a duplicate import). Uses real files so
    /// symlink resolution has something to resolve.
    func testVaultRelativePathResolvesSymlinkedContainment() throws {
        let realVault = tempDir.appendingPathComponent("realvault")
        try FileManager.default.createDirectory(
            at: realVault, withIntermediateDirectories: true)
        try "# a\n".write(
            to: realVault.appendingPathComponent("a.md"),
            atomically: true, encoding: .utf8)
        let link = tempDir.appendingPathComponent("linkvault")
        try FileManager.default.createSymbolicLink(at: link, withDestinationURL: realVault)

        XCTAssertEqual(
            AppState.vaultRelativePath(
                of: link.appendingPathComponent("a.md"), vaultURL: realVault),
            "a.md",
            "a file reached via a symlink to the vault is in-vault, not external")
    }

    /// #870 Codex round 2 (F3): an EXTERNAL symlink FILE that points INTO the
    /// vault must classify as external (→ import a copy), NOT be dereferenced
    /// to its in-vault target (→ a move of the real note, breaking the link).
    /// Only the container is symlink-resolved; the dropped item's own final
    /// component is preserved.
    func testExternalSymlinkFileIsNotDereferencedToVaultTarget() throws {
        let realVault = tempDir.appendingPathComponent("realvault2")
        try FileManager.default.createDirectory(
            at: realVault, withIntermediateDirectories: true)
        try "# a\n".write(
            to: realVault.appendingPathComponent("a.md"),
            atomically: true, encoding: .utf8)
        // An external symlink FILE (outside the vault) pointing at vault/a.md.
        let externalLink = tempDir.appendingPathComponent("shortcut.md")
        try FileManager.default.createSymbolicLink(
            at: externalLink, withDestinationURL: realVault.appendingPathComponent("a.md"))

        XCTAssertNil(
            AppState.vaultRelativePath(of: externalLink, vaultURL: realVault),
            "an external symlink file is external (import), not its vault target")
    }

    /// #870 Codex round 3 (F3): dragging the CURRENT VAULT ROOT onto its own
    /// tree is a no-op, NOT an external import (both the root and an external
    /// URL map to a nil vault-relative path — `fileURLDropAction` must
    /// distinguish them and return `.none` for the root).
    func testDroppingVaultRootIsNoOpNotImport() throws {
        let vault = tempDir.appendingPathComponent("rootdrop")
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)

        XCTAssertEqual(
            AppState.fileURLDropAction(
                url: vault, vaultURL: vault, destinationFolder: "", isDirectory: true),
            .none,
            "the vault root dropped onto itself is a no-op, not a text import")
    }

    // MARK: - Import (external drop) end-to-end

    func testExternalFileDropImportsIntoVault() async throws {
        let (state, vault) = try await makeVault(files: ["a.md"])
        // A file OUTSIDE the vault, dropped onto the root.
        let external = tempDir.appendingPathComponent("outside.md")
        try "# outside\nbody\n".write(to: external, atomically: true, encoding: .utf8)

        let action = AppState.fileURLDropAction(
            url: external, vaultURL: vault, destinationFolder: "", isDirectory: false)
        XCTAssertEqual(action, .importFile(url: external, into: ""))

        await state.importEntry(externalURL: external, into: "")?.value

        XCTAssertTrue(exists(vault, "outside.md"), "the external file was copied in")
        XCTAssertEqual(
            try String(contentsOf: vault.appendingPathComponent("outside.md"), encoding: .utf8),
            "# outside\nbody\n", "content preserved")
        XCTAssertEqual(state.lastMutationAnnouncement, "Imported outside.md.")
        // A copy — the original stays put outside the vault.
        XCTAssertTrue(FileManager.default.fileExists(atPath: external.path))
    }

    /// An import that collides with an existing vault name reuses the SAME
    /// no-clobber collision surface as a colliding move (`lastError` +
    /// "Could not import …"), never silently overwriting.
    func testImportCollisionSurfacesTheSharedFailurePath() async throws {
        let (state, vault) = try await makeVault(files: ["dupe.md"])
        let original = try String(
            contentsOf: vault.appendingPathComponent("dupe.md"), encoding: .utf8)

        let external = tempDir.appendingPathComponent("dupe.md")
        try "DIFFERENT CONTENT\n".write(to: external, atomically: true, encoding: .utf8)

        await state.importEntry(externalURL: external, into: "")?.value

        XCTAssertNotNil(state.lastError, "a name collision surfaces an error")
        let announcement = try XCTUnwrap(state.lastMutationAnnouncement)
        XCTAssertTrue(
            announcement.hasPrefix("Could not import dupe.md: "),
            "failure form matches the shared 'Could not <verb> <name>: …' — got \(announcement)")
        XCTAssertEqual(
            try String(contentsOf: vault.appendingPathComponent("dupe.md"), encoding: .utf8),
            original, "the existing vault file is NOT clobbered")
    }

    // MARK: - In-vault file-URL drop → move end-to-end

    func testInVaultFileURLDropMovesOnDisk() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/x.md"])
        // A vault file dragged in from Finder arrives as a file URL.
        let inVaultURL = vault.appendingPathComponent("a.md")

        let action = AppState.fileURLDropAction(
            url: inVaultURL, vaultURL: vault, destinationFolder: "dest", isDirectory: false)
        guard case .move(let path, let isDir, let dest) = action else {
            return XCTFail("an in-vault file URL must resolve to a move, got \(action)")
        }
        await state.moveEntry(path: path, isDirectory: isDir, to: dest)?.value

        XCTAssertTrue(exists(vault, "dest/a.md"), "the in-vault drop moved the file")
        XCTAssertFalse(exists(vault, "a.md"))
        // And — being a move — it is undoable (#871 integration).
        XCTAssertEqual(state.structuralUndoStack.count, 1)
    }
}
