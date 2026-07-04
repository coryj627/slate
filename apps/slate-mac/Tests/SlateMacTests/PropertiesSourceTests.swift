// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// U3-4 (#468): the show-source YAML commit path — `applyPropertiesSource`
/// riding the property-edit machinery (`.setSource`), the malformed-YAML
/// inline error (non-destructive), the conflict flow, and the command
/// guard. View-local draft/toggle state is exercised through the AppState
/// seams the widget observes (request + committed tokens, error string).
@MainActor
final class PropertiesSourceTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("props-source-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    private let fmNote = "---\ntitle: Old\n---\nBody stays.\n"

    private func makeLoadedState() async throws -> (AppState, URL) {
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try fmNote.write(
            to: vault.appendingPathComponent("note.md"), atomically: true, encoding: .utf8)
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "note.md"
        await state.noteLoadTask?.value
        return (state, vault)
    }

    func testApplyRewritesFrontmatterAndRefreshes() async throws {
        let (state, vault) = try await makeLoadedState()

        state.applyPropertiesSource("title: New\nrating: 5\n")
        await state.propertyEditTask?.value

        let disk = try String(
            contentsOf: vault.appendingPathComponent("note.md"), encoding: .utf8)
        XCTAssertEqual(
            disk, "---\ntitle: New\nrating: 5\n---\nBody stays.\n",
            "the composed rewrite keeps the body byte-exactly")
        XCTAssertEqual(
            state.currentNoteFMSource, "title: New\nrating: 5\n",
            "fmSource refreshed from the one post-commit read")
        XCTAssertEqual(state.propertiesSourceCommitted, 1, "the widget's flip-back edge")
        XCTAssertNil(state.propertiesSourceError)
        XCTAssertNil(state.currentSaveConflict)
    }

    /// Non-destructive, specific (DoD §F): malformed YAML writes NOTHING,
    /// surfaces the Rust line/column message inline, and the committed
    /// token does not move (the widget keeps the draft + focus).
    func testMalformedYAMLSurfacesInlineAndWritesNothing() async throws {
        let (state, vault) = try await makeLoadedState()
        let before = try Data(contentsOf: vault.appendingPathComponent("note.md"))

        state.applyPropertiesSource("title: \"unterminated\n")
        await state.propertyEditTask?.value

        XCTAssertNotNil(state.propertiesSourceError, "inline error surfaced")
        XCTAssertEqual(state.propertiesSourceCommitted, 0, "no flip-back on failure")
        let after = try Data(contentsOf: vault.appendingPathComponent("note.md"))
        XCTAssertEqual(before, after, "disk bytes unchanged — nothing was written")
        XCTAssertEqual(state.currentNoteFMSource, "title: Old\n", "state untouched")
    }

    func testConflictRidesThePropertyEditFlow() async throws {
        let (state, vault) = try await makeLoadedState()
        // External write behind our back → the expected-hash check trips.
        try "---\ntitle: External\n---\nBody stays.\n".write(
            to: vault.appendingPathComponent("note.md"), atomically: true, encoding: .utf8)

        state.applyPropertiesSource("title: Mine\n")
        await state.propertyEditTask?.value

        guard let conflict = state.currentPropertyEditConflict else {
            return XCTFail("expected the property-edit conflict surface")
        }
        XCTAssertEqual(conflict.action, .setSource("title: Mine\n"))
        XCTAssertNil(state.propertiesSourceError, "conflict is not the inline-error path")

        // Keep mine: re-issues .setSource with the fresh on-disk hash.
        state.resolvePropertyEditConflictKeepMine()
        await state.propertyEditTask?.value
        let disk = try String(
            contentsOf: vault.appendingPathComponent("note.md"), encoding: .utf8)
        XCTAssertEqual(disk, "---\ntitle: Mine\n---\nBody stays.\n")
        XCTAssertEqual(state.propertiesSourceCommitted, 1)
    }

    /// The fm-only guarantee holds through the source path too: a dirty
    /// BODY buffer survives the fm rewrite, and the next body save
    /// composes the fresh fm with the dirty body.
    func testApplyWithDirtyBodyPreservesBufferAndComposesFresh() async throws {
        let (state, vault) = try await makeLoadedState()
        state.updateEditorText("dirty body\n")
        XCTAssertTrue(state.hasUnsavedChanges)

        state.applyPropertiesSource("title: New\n")
        await state.propertyEditTask?.value
        XCTAssertEqual(state.currentNoteText, "dirty body\n", "buffer untouched")
        XCTAssertTrue(state.hasUnsavedChanges)

        state.saveCurrentNote()
        await state.saveTask?.value
        let disk = try String(
            contentsOf: vault.appendingPathComponent("note.md"), encoding: .utf8)
        XCTAssertEqual(disk, "---\ntitle: New\n---\ndirty body\n")
        XCTAssertNil(state.currentSaveConflict, "hash handoff kept the chain")
    }

    func testToggleCommandGuardsAndBumps() async throws {
        let (state, _) = try await makeLoadedState()
        XCTAssertEqual(state.propertiesSourceToggleRequest, 0)
        state.togglePropertiesSourceCommand()
        XCTAssertEqual(state.propertiesSourceToggleRequest, 1)

        // Guard: no renderable note → no bump.
        state.closeVault()
        state.togglePropertiesSourceCommand()
        XCTAssertEqual(state.propertiesSourceToggleRequest, 1)
    }
}
