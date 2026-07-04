// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// U3-2 (#466): the reading/editing mode toggle — per-tab mode state,
/// sparse storage, tab-switch persistence, the caret round trip, verbatim
/// announcements, and the workspace.json schema (including backward
/// compatibility with pre-U3 snapshots).
@MainActor
final class ViewModeTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("view-mode-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    private func makeVaultState(files: [String]) async throws -> (AppState, URL) {
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for name in files {
            try "# \(name)\n".write(
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

    // MARK: - WorkspaceState mode storage

    func testViewModeDefaultsToEditingAndStoresSparsely() {
        let ws = WorkspaceState()
        let tab = ws.openTab(.markdown(path: "a.md"))
        XCTAssertEqual(ws.viewMode(for: tab), .editing)
        XCTAssertEqual(ws.activeViewMode, .editing)

        ws.setViewMode(.reading, for: tab)
        XCTAssertEqual(ws.viewMode(for: tab), .reading)
        XCTAssertEqual(ws.viewModes.count, 1)

        // Editing is the absent default — setting it back removes the entry.
        ws.setViewMode(.editing, for: tab)
        XCTAssertEqual(ws.viewMode(for: tab), .editing)
        XCTAssertTrue(ws.viewModes.isEmpty, "editing entries are never stored")
    }

    func testCloseAndResetClearModeEntries() {
        let ws = WorkspaceState()
        let a = ws.openTab(.markdown(path: "a.md"))
        let b = ws.openTab(.markdown(path: "b.md"))
        ws.setViewMode(.reading, for: a)
        ws.setViewMode(.reading, for: b)

        _ = ws.close(a)
        XCTAssertNil(ws.viewModes[a], "closed tab's mode entry is dropped")
        XCTAssertEqual(ws.viewModes[b], .reading)

        ws.reset()
        XCTAssertTrue(ws.viewModes.isEmpty)
    }

    func testAdoptRestoresModesAndDropsUnknownIDs() {
        let ws = WorkspaceState()
        var restored = WorkspaceModel()
        let tab = restored.openTab(.markdown(path: "a.md"))
        let stranger = TabID()
        ws.adopt(restored, viewModes: [tab: .reading, stranger: .reading])
        XCTAssertEqual(ws.viewModes, [tab: .reading])
    }

    // MARK: - Store schema

    func testSnapshotRoundTripsPerTabModes() {
        var model = WorkspaceModel()
        let a = model.openTab(.markdown(path: "a.md"))
        let b = model.openTab(.markdown(path: "b.md"))
        _ = b  // b stays editing — must not be written to the snapshot.

        let snapshot = WorkspaceStore.snapshot(of: model, viewModes: [a: .reading])
        let decoded = WorkspaceStore.viewModes(from: snapshot)
        XCTAssertEqual(decoded, [a: .reading])
    }

    func testPreU3SnapshotWithoutModeKeyDecodesToEditing() throws {
        // A verbatim v1 snapshot as U1-6 wrote it — no "mode" keys anywhere.
        let tabID = UUID()
        let groupID = UUID()
        let json = """
            {
              "version": 1,
              "activeGroup": "\(groupID.uuidString)",
              "activeLeaf": "outline",
              "root": {
                "kind": "group",
                "id": "\(groupID.uuidString)",
                "activeTab": "\(tabID.uuidString)",
                "tabs": [
                  { "id": "\(tabID.uuidString)",
                    "item": { "kind": "markdown", "path": "a.md" } }
                ]
              }
            }
            """
        let snapshot = try JSONDecoder().decode(
            WorkspaceStore.Snapshot.self, from: Data(json.utf8))
        XCTAssertTrue(
            WorkspaceStore.viewModes(from: snapshot).isEmpty,
            "absent mode keys restore as editing (empty sparse map)")
        XCTAssertNotNil(WorkspaceStore.model(from: snapshot))
    }

    func testUnknownModeStringRestoresAsEditing() throws {
        // A future build wrote a mode this one doesn't know — decode keeps
        // the tab, the unknown mode falls back to editing.
        let tabID = UUID()
        let groupID = UUID()
        let json = """
            {
              "version": 1,
              "activeGroup": "\(groupID.uuidString)",
              "root": {
                "kind": "group",
                "id": "\(groupID.uuidString)",
                "activeTab": "\(tabID.uuidString)",
                "tabs": [
                  { "id": "\(tabID.uuidString)", "mode": "annotating",
                    "item": { "kind": "markdown", "path": "a.md" } }
                ]
              }
            }
            """
        let snapshot = try JSONDecoder().decode(
            WorkspaceStore.Snapshot.self, from: Data(json.utf8))
        XCTAssertTrue(WorkspaceStore.viewModes(from: snapshot).isEmpty)
        XCTAssertNotNil(WorkspaceStore.model(from: snapshot))
    }

    // MARK: - Toggle end-to-end

    func testToggleFlipsAnnouncesAndPersistsPerTab() async throws {
        let (state, _) = try await makeVaultState(files: ["a.md", "b.md"])
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.activeViewMode, .editing)

        state.toggleViewMode()
        XCTAssertEqual(state.activeViewMode, .reading)
        XCTAssertEqual(state.lastViewModeAnnouncement, "Reading mode.")

        // Open b in a new tab (editing); a's mode must survive the switch.
        state.openFile("b.md", target: .newTab)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.activeViewMode, .editing, "new tab starts editing")

        state.selectPreviousTab()
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "a.md")
        XCTAssertEqual(state.activeViewMode, .reading, "mode is per-tab state")

        state.toggleViewMode()
        XCTAssertEqual(state.activeViewMode, .editing)
        XCTAssertEqual(state.lastViewModeAnnouncement, "Editing mode.")
    }

    func testToggleNoOpsWithoutARenderableNote() async throws {
        let (state, _) = try await makeVaultState(files: ["a.md"])
        // Nothing loaded yet.
        state.toggleViewMode()
        XCTAssertEqual(state.activeViewMode, .editing)
        XCTAssertNil(state.lastViewModeAnnouncement)
    }

    /// The caret round trip: the coordinator reports byte offsets
    /// continuously; switch-to-reading parks the live value; switch back
    /// delivers it through the one-shot cursor request (whose editor-side
    /// handler also scrolls + takes first responder — #421 F-H1).
    func testCaretRoundTripThroughReadingMode() async throws {
        let (state, _) = try await makeVaultState(files: ["a.md"])
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value

        // Multibyte guard: the coordinator reports RAW UTF-16; the byte
        // conversion happens once at capture, against the live buffer.
        // "# a.md\n" — caret after the 'é' we type next would differ; use
        // the loaded ASCII fixture text: UTF-16 location 7 == byte 7 here,
        // then assert a non-ASCII case explicitly below.
        state.noteEditorCaretDidMove(toUTF16: 7)
        state.toggleViewMode()
        XCTAssertEqual(state.activeViewMode, .reading)

        state.toggleViewMode()
        XCTAssertEqual(state.activeViewMode, .editing)
        XCTAssertEqual(
            state.cursorByteOffsetRequest.value, 7,
            "the parked caret is delivered on the editor remount")

        // Non-ASCII: buffer "hé!" — caret after "hé" is UTF-16 location 2
        // but UTF-8 byte offset 3. The parked value must be BYTES.
        state.updateEditorText("hé!")
        state.noteEditorCaretDidMove(toUTF16: 2)
        state.toggleViewMode()
        state.toggleViewMode()
        XCTAssertEqual(
            state.cursorByteOffsetRequest.value, 3,
            "UTF-16 caret location converts to a UTF-8 byte offset at capture")
    }

    /// Layout persistence: a reading-mode tab survives vault close/reopen
    /// through workspace.json (schema: per-tab "mode", absent = editing).
    func testVaultReopenRestoresReadingMode() async throws {
        let (state, vault) = try await makeVaultState(files: ["a.md", "b.md"])
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        state.openFile("b.md", target: .newTab)
        await state.noteLoadTask?.value
        // b (active) flips to reading; a stays editing.
        state.toggleViewMode()
        XCTAssertEqual(state.activeViewMode, .reading)
        state.closeVault()

        state.openVault(at: vault)
        await state.scanTask?.value
        await state.noteLoadTask?.value

        let tabs = state.workspace.model.allTabs
        XCTAssertEqual(tabs.count, 2, "layout restored")
        let modes = tabs.map { state.workspace.viewMode(for: $0.id) }
        XCTAssertEqual(
            modes.filter { $0 == .reading }.count, 1,
            "exactly the one reading-mode tab restored as reading")
        let readingTab = tabs.first {
            state.workspace.viewMode(for: $0.id) == .reading
        }
        XCTAssertEqual(
            readingTab?.item, .markdown(path: "b.md"),
            "the RIGHT tab restored as reading")
    }
}
