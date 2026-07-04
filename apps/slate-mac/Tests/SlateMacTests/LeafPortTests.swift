// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// U4-2 (#471): the seven sidebar-stack panels ported into the right-pane leaf
/// host, the stack retired, and per-leaf empty states.
///
/// The panels themselves keep their existing dedicated tests (OutlineSidebar,
/// Citations, Bibliography, ContentBlockPanels) — those stay green unchanged in
/// assertion content, which IS the "identical capability" acceptance. What this
/// file adds is the U4-2 delta: the leaf empty states (a leaf must never be a
/// blank rectangle — DoD §A), the retirement of the sidebar stack (Properties
/// alone remains, temporarily), and appearance snapshots for the two densest
/// leaves. As elsewhere in this module, rendered-AX-tree assertions have no
/// XCTest surface, so leaf-empty-state LABELS are pinned structurally (the same
/// technique `RightPaneViewTests` / `ContentBlockPanelsTests` use) and paired
/// with a real-view render smoke test in both appearances.
@MainActor
final class LeafPortTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-leaf-port-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    /// A fresh `AppState` with no vault open: `selectedFilePath == nil`, so
    /// every ported leaf takes its no-note empty-state branch.
    private func stateWithNoNote() -> AppState {
        AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json")),
            externalOpener: { _ in true })
    }

    // MARK: - Per-leaf empty states (labeled, never blank)

    /// Every ported leaf's no-note empty state carries a specific, labeled
    /// sentence — "Select a note to see its …" — so a selectable rail icon
    /// never opens onto a blank rectangle (DoD §A, superseding the stack-era
    /// self-hiding). Pinned in source (no rendered-AX-tree API); the render
    /// smoke test below proves the branch actually draws.
    func testPortedLeavesDeclareLabeledNoNoteEmptyStates() throws {
        let expected: [(file: String, sentence: String)] = [
            ("BacklinksPanel.swift", "Select a note to see its backlinks."),
            ("OutgoingLinksPanel.swift", "Select a note to see its outgoing links."),
            ("EmbedsPanel.swift", "Select a note to see its embeds."),
            ("ContentBlockPanels.swift", "Select a note to see its math."),
            ("ContentBlockPanels.swift", "Select a note to see its code blocks."),
            ("ContentBlockPanels.swift", "Select a note to see its diagrams."),
            ("TasksPanel.swift", "Select a note to see its tasks."),
        ]
        for (file, sentence) in expected {
            let source = try panelSource(file)
            XCTAssertTrue(
                source.contains(sentence),
                "\(file) must present the labeled empty state \"\(sentence)\" — a leaf can't be blank."
            )
        }
    }

    /// The three leaves that can be non-empty-but-selected (a note with no
    /// content of that kind) declare a DISTINCT empty state so "no embeds here"
    /// never reads as "no note selected". Embeds + the two content-block leaves
    /// that settle to zero blocks with no error.
    func testNoContentLeavesDeclareDistinctEmptyStates() throws {
        let expected: [(file: String, sentence: String)] = [
            ("EmbedsPanel.swift", "This note has no embeds."),
            ("ContentBlockPanels.swift", "This note has no math blocks."),
            ("ContentBlockPanels.swift", "This note has no code blocks."),
            ("ContentBlockPanels.swift", "This note has no diagrams."),
        ]
        for (file, sentence) in expected {
            let source = try panelSource(file)
            XCTAssertTrue(
                source.contains(sentence),
                "\(file) must present the note-with-no-content empty state \"\(sentence)\"."
            )
        }
    }

    /// The shared leaf empty-state chrome labels itself for VoiceOver (the
    /// `.combine`d element carries the sentence). Structural pin of the
    /// mechanism every ported leaf routes through.
    func testLeafEmptyStateChromeIsLabeled() throws {
        let source = try panelSource("LeafChrome.swift")
        XCTAssertTrue(
            source.contains("accessibilityElement(children: .combine)"),
            "LeafEmptyState must combine into one AX element")
        XCTAssertTrue(
            source.contains("accessibilityLabel(message)"),
            "LeafEmptyState must label itself with its message")
    }

    /// Each ported leaf actually RENDERS its no-note empty state in both
    /// appearances (catches a per-appearance crash / failed render that a
    /// source pin can't). One real view per leaf, no vault → the empty branch.
    func testPortedLeafEmptyStatesRenderInBothAppearances() {
        let state = stateWithNoNote()
        XCTAssertNil(state.selectedFilePath, "no vault ⇒ no selected note ⇒ empty-state branch")
        PresentationReady.assertRendersInBothAppearances(BacklinksPanel().environmentObject(state))
        PresentationReady.assertRendersInBothAppearances(
            OutgoingLinksPanel().environmentObject(state))
        PresentationReady.assertRendersInBothAppearances(EmbedsPanel().environmentObject(state))
        PresentationReady.assertRendersInBothAppearances(MathBlocksPanel().environmentObject(state))
        PresentationReady.assertRendersInBothAppearances(CodeBlocksPanel().environmentObject(state))
        PresentationReady.assertRendersInBothAppearances(DiagramsPanel().environmentObject(state))
        PresentationReady.assertRendersInBothAppearances(TasksPanel().environmentObject(state))
    }

    /// The leaf empty state's text-on-surface pairing clears the DoD §D APCA
    /// floor in both appearances — the empty state is secondary text on the
    /// surface token, same as the tree's rows.
    func testLeafEmptyStateClearsContrastFloor() {
        PresentationReady.assertContrastFloor([
            ("leaf empty state (textSecondary on surface)", .tokenTextSecondary, .tokenSurface)
        ])
    }

    // MARK: - The sidebar no longer hosts the stack

    /// The retired panel stack: `FileTreeSidebar` must no longer instantiate
    /// any of the seven ported panels (they live in the leaf host now). The
    /// tree, the scan strip, and the temporary Properties section are all that
    /// remain of its per-note surfaces.
    func testSidebarNoLongerHostsThePortedPanels() throws {
        let source = try panelSource("FileTreeSidebar.swift")
        for panel in [
            "BacklinksPanel()", "OutgoingLinksPanel()", "EmbedsPanel()",
            "MathBlocksPanel()", "CodeBlocksPanel()", "DiagramsPanel()", "TasksPanel()",
        ] {
            XCTAssertFalse(
                source.contains(panel),
                "\(panel) must NOT be in FileTreeSidebar after U4-2 — it moved to the leaf rail."
            )
        }
    }

    /// U3-3 (#467) completed the relocation: the sidebar hosts NO properties
    /// surface at all — the in-note widget (`NotePropertiesHeader`, mounted
    /// by `NoteContentView` above both mode surfaces) is the one home.
    func testSidebarNoLongerHostsProperties() throws {
        let source = try panelSource("FileTreeSidebar.swift")
        XCTAssertFalse(
            source.contains("PropertiesPanel()"),
            "the temporary sidebar Properties section is gone (U3-3)")
        let contentHost = try panelSource("NoteContentView.swift")
        XCTAssertTrue(
            contentHost.contains("NotePropertiesHeader(workspace:"),
            "NoteContentView mounts the in-note properties widget")
    }

    /// The leaf host, conversely, DOES instantiate all seven ported panels —
    /// the other half of the retirement (the stack's surfaces moved, they
    /// weren't dropped).
    func testLeafHostInstantiatesAllPortedPanels() throws {
        let source = try rightPaneSource()
        for panel in [
            "OutlineSidebar()", "BacklinksPanel()", "OutgoingLinksPanel()", "EmbedsPanel()",
            "MathBlocksPanel()", "CodeBlocksPanel()", "DiagramsPanel()", "TasksPanel()",
            "CitationsPanel()", "BibliographyPanel()",
        ] {
            XCTAssertTrue(
                source.contains(panel),
                "\(panel) must be wired into RightPaneView.leafContent — the leaf host owns it now."
            )
        }
    }

    // MARK: - Appearance snapshots for the two densest leaves

    /// The Tasks leaf — the densest interactive leaf (grouped Open/Done rows,
    /// per-row checkbox + metadata) — renders in both appearances against a
    /// real note with tasks. PresentationReady §E render smoke over the real
    /// leaf, populated (not the empty state).
    func testTasksLeafRendersPopulatedInBothAppearances() async throws {
        let state = try await loadedState(
            note: """
                # Tasks

                - [ ] Open task with a due date 📅 2026-08-01
                - [ ] Another open task ⏫
                - [x] A completed task
                """)
        XCTAssertFalse(state.currentNoteTasks.isEmpty, "the note must have tasks to snapshot dense state")
        PresentationReady.assertRendersInBothAppearances(TasksPanel().environmentObject(state))
    }

    /// The Bibliography leaf — the densest read-only leaf (segmented picker,
    /// search field, entry rows with multi-field AX) — renders in both
    /// appearances. No sources configured is fine: it exercises the segmented
    /// header + the "no sources configured" populated branch, which is still
    /// the dense chrome (picker + divider), not the empty-state placeholder.
    func testBibliographyLeafRendersInBothAppearances() async throws {
        let state = try await loadedState(note: "# Refs\n\nSee [@smith2020].\n")
        PresentationReady.assertRendersInBothAppearances(
            BibliographyPanel().environmentObject(state))
    }

    // MARK: - internals

    /// Open a one-note vault and select the note, awaiting the load + content
    /// pipelines so panels have real data to render.
    private func loadedState(note body: String) async throws -> AppState {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data(body.utf8).write(to: vault.appendingPathComponent("note.md"))
        let state = stateWithNoNote()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "note.md"
        await state.noteLoadTask?.value
        await state.linksLoadTask?.value
        await state.tasksLoadTask?.value
        await state.mathBlocksLoadTask?.value
        await state.codeBlocksLoadTask?.value
        await state.diagramBlocksLoadTask?.value
        return state
    }

    /// Read an app source by filename (top-level `Sources/SlateMac/…`), walking
    /// up from #filePath like the sibling source-structural tests.
    private func panelSource(_ filename: String) throws -> String {
        try source(at: "Sources/SlateMac/\(filename)")
    }

    private func rightPaneSource() throws -> String {
        try source(at: "Sources/SlateMac/Workspace/RightPaneView.swift")
    }

    private func source(at relativePath: String) throws -> String {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        for _ in 0..<8 {
            let candidate = cursor.appendingPathComponent(relativePath)
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
            cursor = cursor.deletingLastPathComponent()
        }
        XCTFail("Could not locate \(relativePath) from \(#filePath)")
        return ""
    }
}
