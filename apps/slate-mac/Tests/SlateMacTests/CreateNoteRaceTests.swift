// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #796 — the new-note and template-create flows must never truncate
/// an existing file on a name race (a file created externally after
/// the scan, invisible to the unique-name picker). Both flows route
/// through `create_exclusive`, the O-3 no-clobber primitive.
@MainActor
final class CreateNoteRaceTests: XCTestCase {
    private var tempDirs: [URL] = []

    private actor SuspensionGate {
        private var entered = false
        private var entranceWaiters: [CheckedContinuation<Void, Never>] = []
        private var releaseWaiter: CheckedContinuation<Void, Never>?

        func enter() async {
            entered = true
            for waiter in entranceWaiters { waiter.resume() }
            entranceWaiters = []
            await withCheckedContinuation { releaseWaiter = $0 }
        }

        func waitUntilEntered() async {
            guard !entered else { return }
            await withCheckedContinuation { entranceWaiters.append($0) }
        }

        func release() {
            releaseWaiter?.resume()
            releaseWaiter = nil
        }
    }

    func addTempDir(_ dir: URL) {
        tempDirs.append(dir)
    }

    override func tearDown() {
        for dir in tempDirs {
            try? FileManager.default.removeItem(at: dir)
        }
        tempDirs = []
        super.tearDown()
    }

    private func openVault(plant: (URL) throws -> Void = { _ in }) async throws
        -> (AppState, URL)
    {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("create-race-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: dir, withIntermediateDirectories: true)
        tempDirs.append(dir)
        try plant(dir)
        let state = AppState(
            recentsStore: nil,
            externalOpener: { _ in true },
            preferencesStore: PreferencesStore(
                defaults: UserDefaults(suiteName: UUID().uuidString)!)
        )
        state.openVault(at: dir)
        await state.scanTask?.value
        return (state, dir)
    }

    func testCreateNoteNeverTruncatesARacedExistingFile() async throws {
        let (state, dir) = try await openVault()

        // The race: the file appears on disk AFTER the scan, so the
        // unique-name picker (which only sees the index) chooses its
        // exact name.
        let raced = dir.appendingPathComponent("Untitled.md")
        try "precious contents\n".write(to: raced, atomically: true, encoding: .utf8)

        await state.createNote(in: "")?.value

        XCTAssertEqual(
            try String(contentsOf: raced, encoding: .utf8),
            "precious contents\n",
            "the raced file's bytes survive"
        )
        XCTAssertNotNil(state.lastError, "the collision surfaces as an error")
        XCTAssertTrue(
            state.lastError?.contains("already exists") == true,
            "the standard name-collision copy: \(state.lastError ?? "nil")"
        )
    }

    func testNewNoteRechecksOwnershipAfterRefreshBeforeLandingInSamePathNewVault()
        async throws
    {
        let (state, vaultA) = try await openVault()
        let vaultB = FileManager.default.temporaryDirectory
            .appendingPathComponent("create-race-vault-b-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: vaultB, withIntermediateDirectories: true)
        tempDirs.append(vaultB)
        try "# Existing in B\n".write(
            to: vaultB.appendingPathComponent("Untitled.md"),
            atomically: true,
            encoding: .utf8)

        let refresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await refresh.enter() }

        let oldCreate = try XCTUnwrap(state.requestCreateNote(in: ""))
        await refresh.waitUntilEntered()

        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vaultA.appendingPathComponent("Untitled.md").path),
            "vault A's native create finishes before the shared refresh")
        XCTAssertNil(
            state.lastMutationAnnouncement,
            "success must not be announced before refresh and ownership revalidation")

        state.openVault(at: vaultB)
        await state.scanTask?.value
        state.selectedFilePath = "Untitled.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.currentNoteText, "# Existing in B\n")

        await refresh.release()
        await oldCreate.value

        XCTAssertEqual(state.selectedFilePath, "Untitled.md")
        XCTAssertEqual(state.loadedFilePath, "Untitled.md")
        XCTAssertEqual(state.currentNoteText, "# Existing in B\n")
        XCTAssertNil(
            state.renamingNode,
            "vault A's stale create must not stage rename UI on vault B's same path")
        XCTAssertFalse(
            state.lastMutationAnnouncement?.contains("Created note") == true,
            "vault A's stale create must not announce success in vault B")
    }

    func testTemplateCreateNeverTruncatesARacedExistingFile() async throws {
        let (state, dir) = try await openVault { dir in
            let templates = dir.appendingPathComponent("Templates")
            try FileManager.default.createDirectory(
                at: templates, withIntermediateDirectories: true)
            try "template body\n".write(
                to: templates.appendingPathComponent("Note.md"),
                atomically: true, encoding: .utf8)
        }
        guard let session = state.currentSession else {
            return XCTFail("no session")
        }

        let raced = dir.appendingPathComponent("FromTemplate.md")
        try "precious contents\n".write(to: raced, atomically: true, encoding: .utf8)

        // Drive the same primitive the template flow calls (the flow's
        // rendering plumbing is covered elsewhere; the race contract is
        // the no-clobber write).
        XCTAssertThrowsError(
            try session.createExclusive(path: "FromTemplate.md", content: "template body\n")
        ) { error in
            guard case VaultError.DestinationExists = error else {
                return XCTFail("expected DestinationExists, got \(error)")
            }
        }
        XCTAssertEqual(
            try String(contentsOf: raced, encoding: .utf8),
            "precious contents\n"
        )
    }
}

extension CreateNoteRaceTests {
    /// The New-Canvas flow uses the exclusive write itself as its live
    /// collision probe. An externally-created first candidate survives and
    /// the flow advances to the next numbered name.
    @MainActor
    func testNewCanvasNeverTruncatesARacedExistingFile() async throws {
        let (state, dir) = try await openVaultForCanvasRace()

        let raced = dir.appendingPathComponent("Untitled Canvas.canvas")
        try #"{"nodes":[{"id":"keep"}]}"#.write(
            to: raced, atomically: true, encoding: .utf8)

        try await XCTUnwrap(state.canvasNewCanvasFile()).value
        XCTAssertEqual(
            try String(contentsOf: raced, encoding: .utf8),
            #"{"nodes":[{"id":"keep"}]}"#,
            "the raced canvas bytes survive"
        )
        XCTAssertEqual(
            try String(
                contentsOf: dir.appendingPathComponent("Untitled Canvas 2.canvas"),
                encoding: .utf8),
            "{}\n")
    }

    private func openVaultForCanvasRace() async throws -> (AppState, URL) {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("create-race-canvas-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: dir, withIntermediateDirectories: true)
        addTempDir(dir)
        let state = AppState(
            recentsStore: nil,
            externalOpener: { _ in true },
            preferencesStore: PreferencesStore(
                defaults: UserDefaults(suiteName: UUID().uuidString)!)
        )
        state.openVault(at: dir)
        await state.scanTask?.value
        return (state, dir)
    }
}
