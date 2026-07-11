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
    /// The New-Canvas flow shares the race shape (#796, round 2): its
    /// name probe reads the index-visible name, then an external
    /// create lands before the write. The bytes must survive.
    @MainActor
    func testNewCanvasNeverTruncatesARacedExistingFile() async throws {
        let (state, dir) = try await openVaultForCanvasRace()

        let raced = dir.appendingPathComponent("Untitled Canvas.canvas")
        try #"{"nodes":[{"id":"keep"}]}"#.write(
            to: raced, atomically: true, encoding: .utf8)

        state.canvasNewCanvasFile()
        // The create is synchronous on the main actor.
        XCTAssertEqual(
            try String(contentsOf: raced, encoding: .utf8),
            #"{"nodes":[{"id":"keep"}]}"#,
            "the raced canvas bytes survive"
        )
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
