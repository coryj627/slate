// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// End-to-end regression tests for the typing → dirty-flag → Cmd+S
/// chain ([#409](https://github.com/coryj627/slate/issues/409)).
///
/// The 2026-06-10 VoiceOver feature test surfaced a silent data-loss
/// path: real keystrokes landed in the NSTextView buffer, but Cmd+S
/// and the toolbar Save button did nothing — no disk write, no
/// announcement, no error. The Save button (which owns the ⌘S
/// keyboard shortcut) is `.disabled(!hasUnsavedChanges)`, so if the
/// NSTextView → `updateEditorText` sync chain breaks, save is
/// silently inert and switching notes discards the edit.
///
/// These tests drive the REAL production chain — `SlateEditorTextView`
/// + `NoteEditorView.Coordinator` wired exactly as `makeNSView` wires
/// them, bound to a real `AppState` via `noteTextBinding()` — and
/// then assert the two things the VO test observed failing:
/// `hasUnsavedChanges` flips on typing, and `saveCurrentNote()`
/// persists the typed text to disk.
@MainActor
final class EditorTypingSaveFlowTests: XCTestCase {
    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-typing-save-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    /// Build an AppState with an open vault and a loaded note —
    /// same fixture shape as `AppStateTests.makeAppStateWithLoadedNote`.
    private func makeAppStateWithLoadedNote(
        initialContent: String = "# Hello\n\nSome body.\n"
    ) async throws -> (AppState, URL, String) {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        let notePath = "note.md"
        try Data(initialContent.utf8).write(to: vault.appendingPathComponent(notePath))

        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json")
        )
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = notePath
        await state.noteLoadTask?.value
        await state.linksLoadTask?.value
        return (state, vault, notePath)
    }

    /// Wire a live `SlateEditorTextView` + `Coordinator` to the
    /// AppState exactly the way `NoteEditorView.makeNSView` +
    /// `NoteContentView.contentState` do in production: the text
    /// binding is `appState.noteTextBinding()`, the view's delegate
    /// is the coordinator, the initial buffer sync goes through
    /// `withSuppressedDirtyTracking`, then `attach`.
    private func makeProductionEditorChain(
        state: AppState
    ) -> (NoteEditorView.Coordinator, SlateEditorTextView) {
        let textView = SlateEditorTextView(
            frame: NSRect(x: 0, y: 0, width: 600, height: 400)
        )
        let coordinator = NoteEditorView.Coordinator(
            text: state.noteTextBinding(),
            onSave: { [weak state] in state?.saveCurrentNote() },
            previewEmbedAtCursor: nil
        )
        textView.coordinator = coordinator
        textView.isEditable = true
        textView.isRichText = false
        textView.allowsUndo = true
        textView.delegate = coordinator
        coordinator.withSuppressedDirtyTracking {
            let bound = state.currentNoteText ?? ""
            if textView.string != bound {
                textView.string = bound
            }
        }
        coordinator.attach(textView: textView)
        return (coordinator, textView)
    }

    /// Typing into the editor must flip `hasUnsavedChanges` — the
    /// flag that enables the Save toolbar button and its ⌘S
    /// shortcut. If this fails, save is silently unreachable and
    /// any edit is data-loss-on-navigation (#409).
    func testTypingFlipsHasUnsavedChanges() async throws {
        let (state, _, _) = try await makeAppStateWithLoadedNote()
        let (_, textView) = makeProductionEditorChain(state: state)

        // Park the caret at end-of-document (the VO test's ⌘↓),
        // then type through the same NSTextView entry point real
        // keystrokes use.
        let end = (textView.string as NSString).length
        textView.setSelectedRange(NSRange(location: end, length: 0))
        textView.insertText(
            "SENTINEL-409", replacementRange: NSRange(location: end, length: 0)
        )

        XCTAssertTrue(
            textView.string.contains("SENTINEL-409"),
            "typed text must land in the NSTextView buffer"
        )
        XCTAssertEqual(
            state.currentNoteText, textView.string,
            "NSTextView buffer and AppState.currentNoteText must stay in sync on typing"
        )
        XCTAssertTrue(
            state.hasUnsavedChanges,
            "typing must mark the note dirty — otherwise ⌘S is disabled and the edit is silently lost (#409)"
        )
    }

    /// The full loss scenario from the VO test: type, save, then
    /// assert the sentinel is actually on disk.
    func testTypedTextSurvivesSaveToDisk() async throws {
        let (state, vault, notePath) = try await makeAppStateWithLoadedNote()
        let (_, textView) = makeProductionEditorChain(state: state)

        let end = (textView.string as NSString).length
        textView.setSelectedRange(NSRange(location: end, length: 0))
        textView.insertText(
            "SENTINEL-409-DISK", replacementRange: NSRange(location: end, length: 0)
        )

        await state.saveCurrentNote()?.value

        let onDisk = try String(
            contentsOf: vault.appendingPathComponent(notePath), encoding: .utf8
        )
        XCTAssertTrue(
            onDisk.contains("SENTINEL-409-DISK"),
            "saved file must contain the typed text — silent no-op save is the #409 data-loss path"
        )
        XCTAssertFalse(state.hasUnsavedChanges, "save must clear the dirty flag")
    }

    /// Switching notes after typing must NOT silently discard the
    /// edit: with the dirty flag set, navigation goes through the
    /// save-changes prompt (`pendingNavigation`), never a silent
    /// buffer swap. This is the exact loss sequence from the VO
    /// test report (F-F1).
    func testSwitchAwayAfterTypingPromptsInsteadOfDiscarding() async throws {
        // Second file present before the vault opens so the scan
        // indexes both.
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("# Hello\n\nSome body.\n".utf8).write(
            to: vault.appendingPathComponent("note.md")
        )
        try Data("# Other\n".utf8).write(to: vault.appendingPathComponent("other.md"))
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json")
        )
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "note.md"
        await state.noteLoadTask?.value
        await state.linksLoadTask?.value
        let (_, textView) = makeProductionEditorChain(state: state)

        let end = (textView.string as NSString).length
        textView.setSelectedRange(NSRange(location: end, length: 0))
        textView.insertText(
            "SENTINEL-409-NAV", replacementRange: NSRange(location: end, length: 0)
        )
        XCTAssertTrue(state.hasUnsavedChanges)

        // Sidebar selection change while dirty — must park in
        // pendingNavigation, not load the other note.
        state.selectedFilePath = "other.md"

        XCTAssertNotNil(
            state.pendingNavigation,
            "navigating away from a dirty buffer must raise the save-changes prompt, not silently discard (#409)"
        )
    }
}
