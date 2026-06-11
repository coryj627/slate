// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// #410 — the Milestone K sidebar panels' show/hide contract,
/// exercised through a real AppState with a real vault so the data
/// path and the panel conditions stay coupled.
///
/// Rendering specifics (MathCAT speech labels, code preambles,
/// diagram descriptions) are covered by the dedicated view tests
/// (`MathViewTests` / `CodeBlockViewTests` / `MermaidViewTests`);
/// CI's a11y-check lints the panel chrome. What needs pinning here:
/// the panels surface exactly when the selected note has blocks of
/// their kind — the wiring #410 exists to add — and disappear from
/// the AX tree otherwise (EmbedsPanel contract).
@MainActor
final class ContentBlockPanelsTests: XCTestCase {
    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-content-panels-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    private func makeLoadedState(noteBody: String) async throws -> AppState {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data(noteBody.utf8).write(to: vault.appendingPathComponent("note.md"))
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json")
        )
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "note.md"
        await state.noteLoadTask?.value
        await state.linksLoadTask?.value
        // Content pipelines load behind the links chain; await each.
        await state.mathBlocksLoadTask?.value
        await state.codeBlocksLoadTask?.value
        await state.diagramBlocksLoadTask?.value
        return state
    }

    func testPanelsPopulateForNoteWithAllThreeBlockKinds() async throws {
        let body = """
            # Mixed

            $$\\sum_{i=0}^{n} i^2$$

            ```rust
            fn main() {}
            ```

            ```mermaid
            flowchart TD
              A --> B
            ```
            """
        let state = try await makeLoadedState(noteBody: body)

        XCTAssertEqual(
            state.currentNoteMathBlocks.count, 1,
            "math pipeline must surface the display-math block"
        )
        // The code PIPELINE ingests every fence (rust + mermaid);
        // the code PANEL filters diagram fences so VO users don't
        // hear the same mermaid content in two panels.
        XCTAssertEqual(state.currentNoteCodeBlocks.count, 2)
        let panelBlocks = CodeBlocksPanel.panelBlocks(state.currentNoteCodeBlocks)
        XCTAssertEqual(panelBlocks.count, 1, "Code panel must hide mermaid fences")
        XCTAssertEqual(panelBlocks[0].language, "rust")
        XCTAssertEqual(
            state.currentNoteDiagramBlocks.count, 1,
            "diagram pipeline must surface the mermaid fence"
        )
        // The exact condition each panel renders on — non-empty
        // blocks with a selected note. This is the #410 wiring
        // contract: data present ⇒ panel visible.
        XCTAssertNotNil(state.selectedFilePath)
    }

    /// Red-team MEDIUM-1 on #410: the pipeline tests above pass even
    /// when no view consumes the panels (they pin inputs, not the
    /// consumer). This pins the WIRING: `FileListSidebar`'s panel
    /// stack must instantiate all three #410 panels, after
    /// `EmbedsPanel` and before `TasksPanel`. Same source-structural
    /// technique as `CloseVaultSheetParityTests` (walk up from
    /// #filePath to the app sources). It fails outright if the
    /// panels are ever dropped from the stack again — exactly the
    /// regression the red-team audit caught pre-push.
    func testSidebarSourceWiresAllThreePanelsIntoTheStack() throws {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        var sidebar: URL?
        for _ in 0..<8 {
            let candidate = cursor.appendingPathComponent(
                "Sources/SlateMac/FileListSidebar.swift"
            )
            if FileManager.default.fileExists(atPath: candidate.path) {
                sidebar = candidate
                break
            }
            cursor = cursor.deletingLastPathComponent()
        }
        let url = try XCTUnwrap(
            sidebar,
            "Could not locate Sources/SlateMac/FileListSidebar.swift from \(#filePath)"
        )
        let source = try String(contentsOf: url, encoding: .utf8)

        let embeds = try XCTUnwrap(source.range(of: "EmbedsPanel()"))
        let tasks = try XCTUnwrap(source.range(of: "TasksPanel()"))
        for panel in ["MathBlocksPanel()", "CodeBlocksPanel()", "DiagramsPanel()"] {
            let r = try XCTUnwrap(
                source.range(of: panel),
                "\(panel) must be instantiated in FileListSidebar — the #410 wiring is missing"
            )
            XCTAssertTrue(
                r.lowerBound > embeds.upperBound && r.upperBound < tasks.lowerBound,
                "\(panel) must sit between EmbedsPanel and TasksPanel in the per-note stack"
            )
        }
    }

    func testPanelsStayHiddenForPlainProseNote() async throws {
        let state = try await makeLoadedState(noteBody: "# Plain\n\nJust prose.\n")
        XCTAssertTrue(state.currentNoteMathBlocks.isEmpty)
        XCTAssertTrue(state.currentNoteCodeBlocks.isEmpty)
        XCTAssertTrue(state.currentNoteDiagramBlocks.isEmpty)
    }
}
