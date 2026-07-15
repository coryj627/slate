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

    private func fileRow(_ path: String) -> FileTreeSidebar.RowID {
        .node(.file(path: path))
    }

    // MARK: - Versioned private batch payload

    func testPrivateDragPayloadRoundTripsOrderAndKindDeterministically() throws {
        let items = [
            FileTreeSidebar.DragPayloadItem(path: "folder", isDirectory: true),
            FileTreeSidebar.DragPayloadItem(path: "folder/note.md", isDirectory: false),
            FileTreeSidebar.DragPayloadItem(path: "other.md", isDirectory: false),
        ]
        let first = try XCTUnwrap(FileTreeSidebar.encodeDragPayload(items))
        let second = try XCTUnwrap(FileTreeSidebar.encodeDragPayload(items))

        XCTAssertEqual(first, second)
        XCTAssertEqual(FileTreeSidebar.decodeDragPayload(first), items)
    }

    func testPrivateDragPayloadRejectsEmptyMalformedUnsafeAndDuplicateBatches() {
        let invalidPayloads = [
            #"{"version":1,"items":[]}"#,
            #"{"version":2,"items":[{"path":"a.md","isDirectory":false}]}"#,
            #"{"version":1,"items":[{"path":"","isDirectory":false}]}"#,
            #"{"version":1,"items":[{"path":"/tmp/a.md","isDirectory":false}]}"#,
            #"{"version":1,"items":[{"path":"a/../b.md","isDirectory":false}]}"#,
            #"{"version":1,"items":[{"path":"a.md","isDirectory":false},{"path":"a.md","isDirectory":true}]}"#,
            "not-json",
            "legacy.md",
        ]

        for payload in invalidPayloads {
            XCTAssertNil(
                FileTreeSidebar.decodeDragPayload(Data(payload.utf8)),
                "must fail closed: \(payload)")
        }
        XCTAssertNil(FileTreeSidebar.encodeDragPayload([]))
    }

    func testPrivateDropFlavorWinsEvenWhenItsDataIsInvalid() {
        let provider = NSItemProvider()
        provider.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.nodeUTType,
            visibility: .ownProcess
        ) { completion in
            completion(Data("malformed".utf8), nil)
            return nil
        }
        provider.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.fileURLUTType,
            visibility: .all
        ) { completion in
            completion(URL(fileURLWithPath: "/tmp/fallback.md").dataRepresentation, nil)
            return nil
        }

        guard case .privatePayload = FileTreeSidebar.preferredDropProvider(in: [provider]) else {
            return XCTFail("private data must win; invalid private data must not become an import")
        }
    }

    func testTreeDropMoveIntentUsesSingleAndBatchFunnels() async throws {
        do {
            let (state, vault) = try await makeVault(files: ["a.md", "dest/keep.md"])
            await state.moveTreeSelection(
                [AppState.TreeSelection(path: "a.md", isDirectory: false)],
                to: "dest")?.value
            XCTAssertTrue(exists(vault, "dest/a.md"))
            XCTAssertEqual(state.lastMutationAnnouncement, "Moved a.md to dest.")
        }

        try FileManager.default.removeItem(at: tempDir.appendingPathComponent("vault"))
        let (state, vault) = try await makeVault(files: ["a.md", "b.md", "dest/keep.md"])
        await state.moveTreeSelection(
            [
                AppState.TreeSelection(path: "a.md", isDirectory: false),
                AppState.TreeSelection(path: "b.md", isDirectory: false),
            ],
            to: "dest")?.value
        XCTAssertTrue(exists(vault, "dest/a.md"))
        XCTAssertTrue(exists(vault, "dest/b.md"))
        XCTAssertEqual(state.lastMutationAnnouncement, "Moved 2 items to dest.")
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

    func testDragProviderPrivateFlavorCarriesSelfDescribingOrderedBatch() async throws {
        let items = [
            FileTreeSidebar.DragPayloadItem(path: "folder", isDirectory: true),
            FileTreeSidebar.DragPayloadItem(path: "other.md", isDirectory: false),
        ]
        let originURL = URL(fileURLWithPath: "/Vaults/demo/folder")
        let provider = FileTreeSidebar.makeDragProvider(
            items: items, originFileURL: originURL)

        let data: Data = try await withCheckedThrowingContinuation { continuation in
            provider.loadDataRepresentation(
                forTypeIdentifier: FileTreeSidebar.nodeUTType
            ) { data, error in
                if let data { continuation.resume(returning: data) }
                else { continuation.resume(throwing: error ?? URLError(.cannotDecodeRawData)) }
            }
        }

        XCTAssertEqual(FileTreeSidebar.decodeDragPayload(data), items)
        XCTAssertEqual(provider.suggestedName, "folder")
    }

    func testDragProviderProjectsSelectionButKeepsOriginPublicFileURL() async throws {
        let a = fileRow("a.md")
        let b = fileRow("nested/b.md")
        let rows = [
            FileTreeSidebar.SelectionRow(identity: a, path: "a.md", isDirectory: false),
            FileTreeSidebar.SelectionRow(
                identity: b, path: "nested/b.md", isDirectory: false),
        ]
        let model = FileTreeSidebar.SelectionModel(
            focused: b,
            selected: [b, a],
            selectionPathSnapshots: [a: "a.md", b: "nested/b.md"],
            rangeAnchor: a,
            rangeAnchorPathSnapshot: "a.md")
        let vaultURL = URL(fileURLWithPath: "/Vaults/demo")

        let provider = FileTreeSidebar.makeDragProvider(
            origin: rows[1], from: model, visibleRows: rows, vaultURL: vaultURL)
        let data: Data = try await withCheckedThrowingContinuation { continuation in
            provider.loadDataRepresentation(
                forTypeIdentifier: FileTreeSidebar.nodeUTType
            ) { data, error in
                if let data { continuation.resume(returning: data) }
                else { continuation.resume(throwing: error ?? URLError(.cannotDecodeRawData)) }
            }
        }

        XCTAssertEqual(
            FileTreeSidebar.decodeDragPayload(data)?.map(\.path), ["a.md", "nested/b.md"])
        XCTAssertEqual(provider.suggestedName, "b.md")
        let publicURL: URL = try await withCheckedThrowingContinuation { continuation in
            _ = provider.loadObject(ofClass: URL.self) { url, error in
                if let url { continuation.resume(returning: url) }
                else { continuation.resume(throwing: error ?? URLError(.badURL)) }
            }
        }
        XCTAssertEqual(publicURL.standardizedFileURL.path, "/Vaults/demo/nested/b.md")
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

    /// Codoki: the extracted `urlIsDirectory` seam classifies real directories
    /// vs files correctly (the drop router feeds this into `fileURLDropAction`).
    func testUrlIsDirectoryClassifiesDirectoriesAndFiles() throws {
        let dir = tempDir.appendingPathComponent("a-folder")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        let file = tempDir.appendingPathComponent("a-file.md")
        try "# hi\n".write(to: file, atomically: true, encoding: .utf8)

        XCTAssertTrue(AppState.urlIsDirectory(dir), "a real directory reads as a directory")
        XCTAssertFalse(AppState.urlIsDirectory(file), "a real file does not")
        XCTAssertFalse(
            AppState.urlIsDirectory(tempDir.appendingPathComponent("does-not-exist")),
            "an unreadable URL falls back to false (safe file default)")
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

    /// #910: a binary / non-UTF-8 external drop imports as a byte-for-byte
    /// copy (via `createExclusiveBytes`) instead of the pre-PR text-only
    /// clean failure — same "Imported <name>." announcement as the text path.
    func testBinaryExternalDropImportsByteForByte() async throws {
        let (state, vault) = try await makeVault(files: ["a.md"])
        // A payload no valid UTF-8 string can hold (lone 0xFF/0xFE + 0xC0/0xC1).
        let bytes: [UInt8] = [0xFF, 0xFE, 0x00, 0x01, 0x80, 0xC0, 0xC1]
        let external = tempDir.appendingPathComponent("photo.png")
        try Data(bytes).write(to: external)

        await state.importEntry(externalURL: external, into: "")?.value

        XCTAssertTrue(exists(vault, "photo.png"), "the binary file was copied in")
        XCTAssertEqual(
            try Data(contentsOf: vault.appendingPathComponent("photo.png")), Data(bytes),
            "bytes round-trip identically, including the non-UTF-8 bytes")
        XCTAssertEqual(state.lastMutationAnnouncement, "Imported photo.png.")
        XCTAssertNil(state.lastError, "a successful binary import surfaces no error")
    }

    /// #910 red-team Medium: an oversized external drop is refused GRACEFULLY
    /// (via the shared `FileTooLarge` failure path) instead of crashing when
    /// its >2 GiB `Data`/`String` would trap in the FFI's `Int32(count)`
    /// converter. The pre-read size guard trips first. Driven with a SPARSE
    /// file one byte past the refuse ceiling — `truncate` sets the logical
    /// size without writing gigabytes, so the guard sees the over-cap size and
    /// the bytes are never allocated (let alone lowered across the FFI).
    func testOversizedExternalDropIsRefusedGracefullyNotCrashed() async throws {
        let (state, vault) = try await makeVault(files: ["a.md"])
        let refuse = try XCTUnwrap(state.currentSession).largeFileRefuseBytes()
        let big = tempDir.appendingPathComponent("huge.bin")
        XCTAssertTrue(FileManager.default.createFile(atPath: big.path, contents: nil))
        let handle = try FileHandle(forWritingTo: big)
        try handle.truncate(atOffset: refuse + 1)
        try handle.close()

        await state.importEntry(externalURL: big, into: "")?.value

        XCTAssertFalse(exists(vault, "huge.bin"), "the oversized file was not imported")
        XCTAssertNotNil(state.lastError, "the refusal surfaced an error")
        let announcement = try XCTUnwrap(state.lastMutationAnnouncement)
        XCTAssertTrue(
            announcement.hasPrefix("Could not import huge.bin: "),
            "refusal routes through the shared 'Could not import …' path — got \(announcement)")
    }

    /// #910 (Codex follow-up): the byte-ceiling decision does NOT trust a
    /// missing or stale preflight — the actual read count is the definitive
    /// gate, so a nil-metadata source whose bytes exceed the ceiling is still
    /// refused (the exact crash path the earlier metadata-only guard missed).
    func testImportOverCeilingGatesOnActualBytesWhenMetadataMissingOrStale() {
        let cap: UInt64 = 10
        // Pre-read call (no bytes yet), metadata unavailable → proceed.
        XCTAssertNil(
            AppState.importOverCeiling(metadataSize: nil, readByteCount: nil, refuseBytes: cap))
        // Metadata unavailable (nil), but the bytes IN HAND exceed the cap →
        // REFUSE. This is the nil-preflight / package-source crash path.
        XCTAssertEqual(
            AppState.importOverCeiling(metadataSize: nil, readByteCount: 11, refuseBytes: cap), 11)
        // A file that grew past the cap after a passing/absent stat (TOCTOU) is
        // caught by the post-read count.
        XCTAssertEqual(
            AppState.importOverCeiling(metadataSize: nil, readByteCount: 5_000, refuseBytes: cap),
            5_000)
        // Within-limit under both signals → proceed (boundary: exactly at cap).
        XCTAssertNil(
            AppState.importOverCeiling(metadataSize: 10, readByteCount: 10, refuseBytes: cap))
        // A preflight over the cap fast-rejects before any read.
        XCTAssertEqual(
            AppState.importOverCeiling(metadataSize: 20, readByteCount: nil, refuseBytes: cap), 20)
    }

    /// #910: the bounded reader never loads more than `cap + 1` bytes, so a
    /// nil-metadata multi-GB source can't be fully read into memory (nor reach
    /// the FFI). A within-cap file is returned in full, byte-identical.
    func testReadImportBytesCapsAtCeilingPlusOne() throws {
        // A file well past the cap → the reader returns exactly cap + 1 bytes,
        // not the whole 60.
        let big = tempDir.appendingPathComponent("cap-big.bin")
        try Data(repeating: 0xAB, count: 60).write(to: big)
        XCTAssertEqual(
            try AppState.readImportBytes(from: big, cap: 10).count, 11,
            "reads at most cap + 1, never the whole oversized file")

        // A within-cap file (incl. non-UTF-8 bytes) is returned verbatim.
        let small = tempDir.appendingPathComponent("cap-small.bin")
        let payload = Data([0xFF, 0xFE, 0x00, 0x01, 0x80])
        try payload.write(to: small)
        XCTAssertEqual(try AppState.readImportBytes(from: small, cap: 10), payload)
    }

    /// #910 (Codex rounds 2–3): the effective transport ceiling clamps the
    /// engine threshold to `Int32.max - 4` — the 4 being the RustBuffer length
    /// prefix, so the OUTER FFI buffer conversion `Int32(payload.count + 4)` (not
    /// just the inner `Int32(value.count)`) cannot trap. Even a pathological
    /// >2 GiB `large_file_refuse_bytes` config cannot let a buffer whose
    /// serialized length exceeds `Int32.max` reach the FFI.
    func testTransportCeilingClampsBelowFfiInt32Limit() {
        let int32Max = UInt64(Int32.max)  // 2_147_483_647
        // (a) A >2 GiB config clamps to Int32.max - 4; Int32.max itself clamps
        //     to Int32.max - 4 (min with the strictly-smaller bound).
        XCTAssertEqual(
            AppState.importTransportCeiling(refuseBytes: int32Max + 1000), int32Max - 4)
        XCTAssertEqual(
            AppState.importTransportCeiling(refuseBytes: int32Max), int32Max - 4)
        // The serialized buffer (payload + 4-byte length prefix) fits in Int32,
        // so neither the inner nor the outer converter conversion can trap.
        let clamped = AppState.importTransportCeiling(refuseBytes: int32Max + 1000)
        XCTAssertLessThanOrEqual(
            clamped + 4, int32Max,
            "payload.count + 4 (the RustBuffer length) must be representable as Int32")
        // The ~50 MiB default is far below the limit → passes through unchanged.
        let fiftyMiB: UInt64 = 50 * 1024 * 1024
        XCTAssertEqual(AppState.importTransportCeiling(refuseBytes: fiftyMiB), fiftyMiB)
        // (c) The clamped ceiling is Int-safe, so the reader's `cap + 1`
        //     sentinel can never overflow Int.
        XCTAssertLessThan(
            AppState.importTransportCeiling(refuseBytes: int32Max + 1_000_000), UInt64(Int.max))

        // (b) Under the clamped ceiling, a buffer AT Int32.max — whose serialized
        //     length WOULD trap the FFI converter — is REFUSED by the definitive
        //     gate, so it never reaches `createExclusive*`. The largest ALLOWED
        //     payload is exactly the ceiling (serialized length == Int32.max);
        //     one byte more is refused.
        XCTAssertNil(
            AppState.importOverCeiling(
                metadataSize: nil, readByteCount: Int(int32Max - 4), refuseBytes: clamped),
            "a payload at the ceiling (serialized length == Int32.max) is allowed")
        XCTAssertEqual(
            AppState.importOverCeiling(
                metadataSize: nil, readByteCount: Int(int32Max - 3), refuseBytes: clamped),
            int32Max - 3,
            "one byte past the ceiling is refused before it can trap the converter")
        XCTAssertEqual(
            AppState.importOverCeiling(
                metadataSize: nil, readByteCount: Int(Int32.max), refuseBytes: clamped),
            int32Max,
            "an Int32.max-byte buffer is refused before it can trap the FFI converter")
    }

    /// #910 (Codex round 3): a ByInspection guard that `importEntry` threads the
    /// CLAMPED `importTransportCeiling(...)` result — never the raw
    /// `session.largeFileRefuseBytes()` — into ALL THREE size checks (preflight,
    /// bounded read, definitive gate). The pure-helper tests above only exercise
    /// pre-clamped values, so they would not catch a regression that passed the
    /// raw threshold to one of the three sites; this reads the source and fails
    /// if that happens.
    func testImportEntryThreadsClampedCeilingIntoAllThreeSizeChecks() throws {
        let appStateURL = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .appendingPathComponent("Sources/SlateMac/AppState.swift")
        let source = try String(contentsOf: appStateURL, encoding: .utf8)

        // Scope to importEntry's body (up to the first helper that follows it).
        guard let start = source.range(of: "func importEntry(externalURL"),
            let end = source.range(
                of: "nonisolated static func importTransportCeiling",
                range: start.upperBound..<source.endIndex)
        else {
            return XCTFail("could not locate importEntry in AppState.swift")
        }
        // Whitespace-normalize so the assertions survive line-wrapping.
        let flat = source[start.lowerBound..<end.lowerBound]
            .split(whereSeparator: { $0.isWhitespace }).joined(separator: " ")

        // The raw engine threshold is read exactly ONCE, and only to feed the
        // clamp — never handed to a size check directly.
        XCTAssertEqual(
            flat.components(separatedBy: "largeFileRefuseBytes()").count - 1, 1,
            "the raw threshold must be read once and immediately clamped")
        XCTAssertTrue(
            flat.contains("importTransportCeiling( refuseBytes: session.largeFileRefuseBytes())"),
            "the single raw-threshold read must feed importTransportCeiling")
        // All three size checks consume the CLAMPED ceiling.
        XCTAssertTrue(
            flat.contains("readImportBytes(from: externalURL, cap: ceiling)"),
            "the bounded read must cap at the clamped ceiling, not the raw threshold")
        XCTAssertEqual(
            flat.components(separatedBy: "refuseBytes: ceiling").count - 1, 2,
            "both the preflight and the definitive gate must pass the clamped ceiling")
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
