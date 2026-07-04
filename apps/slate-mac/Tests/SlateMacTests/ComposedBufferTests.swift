// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// U3-3 (#467/#469): the body-only buffer + composed save — the Swift half
/// of the U3-5 contract. The editor buffer is the BODY; frontmatter source
/// + the whole-file→body offsets ride AppState (and park on NoteDocument),
/// and every save reassembles through the one Rust composer.
@MainActor
final class ComposedBufferTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("composed-buffer-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    private let fmNote = "---\ntitle: Hi\ntags: [a]\n---\n# Head\n\n- [ ] a task\nBody line.\n"

    private func makeVaultState(files: [String: String]) async throws -> (AppState, URL) {
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for (name, content) in files {
            try content.write(
                to: vault.appendingPathComponent(name), atomically: true, encoding: .utf8)
        }
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return (state, vault)
    }

    private func load(_ state: AppState, _ path: String) async {
        state.selectedFilePath = path
        await state.noteLoadTask?.value
    }

    // MARK: - Load

    func testLoadSplitsBodyFromFrontmatter() async throws {
        let (state, _) = try await makeVaultState(files: ["note.md": fmNote])
        await load(state, "note.md")

        XCTAssertEqual(state.currentNoteText, "# Head\n\n- [ ] a task\nBody line.\n")
        XCTAssertEqual(state.currentNoteFMSource, "title: Hi\ntags: [a]\n")
        // "---\n" + fm(20) + "---\n" = 28 bytes, 4 newlines before the body.
        XCTAssertEqual(state.bodyByteOffset, 28)
        XCTAssertEqual(state.bodyLineOffset, 4)
        XCTAssertFalse(state.hasUnsavedChanges)
    }

    func testLoadWithoutFrontmatterIsZeroOffset() async throws {
        let (state, _) = try await makeVaultState(files: ["plain.md": "# Just body\n"])
        await load(state, "plain.md")
        XCTAssertEqual(state.currentNoteText, "# Just body\n")
        XCTAssertEqual(state.currentNoteFMSource, "")
        XCTAssertEqual(state.bodyByteOffset, 0)
        XCTAssertEqual(state.bodyLineOffset, 0)
    }

    func testHeadingsAreRebasedIntoBodySpace() async throws {
        let (state, _) = try await makeVaultState(files: ["note.md": fmNote])
        await load(state, "note.md")
        let head = state.currentNoteHeadings.first { $0.text == "Head" }
        XCTAssertEqual(
            head?.byteOffset, 0,
            "the first body byte is the heading — offsets are body-relative")
    }

    // MARK: - Save

    func testBodyEditSavesComposedBytes() async throws {
        let (state, vault) = try await makeVaultState(files: ["note.md": fmNote])
        await load(state, "note.md")
        state.updateEditorText("# Head\n\nEDITED body.\n")
        state.saveCurrentNote()
        await state.saveTask?.value

        let disk = try String(
            contentsOf: vault.appendingPathComponent("note.md"), encoding: .utf8)
        XCTAssertEqual(
            disk, "---\ntitle: Hi\ntags: [a]\n---\n# Head\n\nEDITED body.\n",
            "the composed save reassembles fm ⊕ edited body byte-exactly")
        XCTAssertFalse(state.hasUnsavedChanges)
        XCTAssertNil(state.saveError)
        XCTAssertNil(state.currentSaveConflict, "hash chain intact")
    }

    /// THE resurrection guard: a property edit while the body is dirty must
    /// refresh AppState's fmSource, so the LATER body save composes the
    /// fresh frontmatter — not the stale pre-edit fm.
    func testPropertyEditWhileBodyDirtyThenSaveComposesFreshFM() async throws {
        let (state, vault) = try await makeVaultState(files: ["note.md": fmNote])
        await load(state, "note.md")
        state.updateEditorText("# Head\n\ndirty body\n")
        XCTAssertTrue(state.hasUnsavedChanges)

        state.setProperty(
            path: "note.md", key: "title", value: .text(value: "New Title"))
        await state.propertyEditTask?.value
        XCTAssertTrue(
            state.currentNoteFMSource.contains("New Title"),
            "fm handoff after the property edit (got: \(state.currentNoteFMSource))")
        XCTAssertEqual(
            state.currentNoteText, "# Head\n\ndirty body\n",
            "the dirty buffer is never clobbered by an fm-only edit")
        XCTAssertTrue(state.hasUnsavedChanges)

        state.saveCurrentNote()
        await state.saveTask?.value
        let disk = try String(
            contentsOf: vault.appendingPathComponent("note.md"), encoding: .utf8)
        XCTAssertTrue(
            disk.contains("New Title"),
            "the body save composed the FRESH fm — no resurrection")
        XCTAssertTrue(disk.hasSuffix("# Head\n\ndirty body\n"))
        XCTAssertNil(state.currentSaveConflict, "the refreshed hash keeps the chain")
    }

    // MARK: - Offsets at the consumer boundaries

    func testTaskRowActivationSendsBodyLine() async throws {
        let (state, _) = try await makeVaultState(files: ["note.md": fmNote])
        await load(state, "note.md")
        await state.tasksLoadTask?.value
        guard let task = state.currentNoteTasks.first else {
            return XCTFail("fixture task not loaded")
        }
        // "- [ ] a task" is body line 3, file line 7 (4 fm lines ahead).
        XCTAssertEqual(Int(task.line), 7, "backend speaks whole-file lines")
        XCTAssertEqual(state.bodyLine(fromFileLine: Int(task.line)), 3)
        XCTAssertEqual(state.fileLine(fromBodyLine: 3), 7)
    }

    func testParkRestoreRoundTripsParts() async throws {
        let (state, _) = try await makeVaultState(
            files: ["note.md": fmNote, "other.md": "# Other\n"])
        await load(state, "note.md")
        state.updateEditorText("# Head\n\nparked dirty\n")

        state.openFile("other.md", target: .newTab)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.currentNoteFMSource, "", "other.md has no fm")
        XCTAssertEqual(state.bodyByteOffset, 0)

        state.selectPreviousTab()
        await state.noteLoadTask?.value
        XCTAssertEqual(state.currentNoteText, "# Head\n\nparked dirty\n")
        XCTAssertEqual(
            state.currentNoteFMSource, "title: Hi\ntags: [a]\n",
            "fm parts travel through the park/restore cycle")
        XCTAssertEqual(state.bodyByteOffset, 28)
        XCTAssertEqual(state.bodyLineOffset, 4)
        XCTAssertTrue(state.hasUnsavedChanges)
    }

    func testReadingContextTaskOffsetMatchesRows() async throws {
        let (state, _) = try await makeVaultState(files: ["note.md": fmNote])
        await load(state, "note.md")
        await state.tasksLoadTask?.value
        // The reading view receives body text + whole-file task records;
        // the context delta must reconcile them: body line 3 + offset 4 ==
        // file line 7 == the record.
        let body = state.currentNoteText ?? ""
        XCTAssertTrue(body.hasPrefix("# Head"))
        let record = state.currentNoteTasks.first
        XCTAssertEqual(Int(record?.line ?? 0), 3 + state.bodyLineOffset)
    }
}
