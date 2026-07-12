// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// Editor spell-check opt-in (#855): default OFF, toggle + announce,
/// persistence through `PreferencesStore` (app-level UserDefaults —
/// the editorTextScale pattern), and the editor-side paragraph-style /
/// overscroll configuration that shipped in the same NSTextView pass.
@MainActor
final class EditorSpellCheckTests: XCTestCase {

    private var defaults: UserDefaults!
    private var suiteName: String!
    private var tempDir: URL!

    override func setUpWithError() throws {
        suiteName = "slate.prefs.spellcheck.tests.\(UUID().uuidString)"
        defaults = UserDefaults(suiteName: suiteName)
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("editor-spellcheck-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        defaults.removePersistentDomain(forName: suiteName)
        defaults = nil
        suiteName = nil
        try? FileManager.default.removeItem(at: tempDir)
    }

    private func makeAppState() -> AppState {
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json"))
        return AppState(
            recentsStore: store,
            externalOpener: { _ in true },
            preferencesStore: PreferencesStore(defaults: defaults))
    }

    // MARK: Preference store

    func testSpellCheckDefaultsOff() {
        let store = PreferencesStore(defaults: defaults)
        XCTAssertFalse(
            store.loadEditorSpellCheck(),
            "spell check is opt-in — Markdown source squiggles everywhere")
    }

    func testSpellCheckSaveLoadRoundTrip() {
        let store = PreferencesStore(defaults: defaults)
        store.saveEditorSpellCheck(true)
        XCTAssertTrue(store.loadEditorSpellCheck())
        store.saveEditorSpellCheck(false)
        XCTAssertFalse(store.loadEditorSpellCheck())
    }

    // MARK: AppState toggle

    func testToggleFlipsPublishedValueAndPersists() {
        let state = makeAppState()
        XCTAssertFalse(state.editorSpellCheckEnabled, "default OFF")

        state.toggleEditorSpellCheck()
        XCTAssertTrue(state.editorSpellCheckEnabled)
        XCTAssertTrue(
            PreferencesStore(defaults: defaults).loadEditorSpellCheck(),
            "toggle persists immediately")

        state.toggleEditorSpellCheck()
        XCTAssertFalse(state.editorSpellCheckEnabled)
        XCTAssertFalse(PreferencesStore(defaults: defaults).loadEditorSpellCheck())
    }

    func testPersistedValueLoadsOnInit() {
        PreferencesStore(defaults: defaults).saveEditorSpellCheck(true)
        let state = makeAppState()
        XCTAssertTrue(
            state.editorSpellCheckEnabled,
            "a fresh AppState adopts the persisted opt-in")
    }

    // MARK: NSTextView configuration (#855's other two legs)

    /// The line-height style: one shared immutable paragraph style at
    /// the documented 1.2 multiple, installed by `attach` as BOTH the
    /// view default and a typing attribute (typed text inherits it —
    /// the keystroke path never restamps), plus a one-time full-range
    /// storage stamp so the current buffer lays out deterministically.
    func testAttachInstallsLineHeightParagraphStyle() {
        XCTAssertEqual(NoteEditorView.lineHeightMultiple, 1.2)
        XCTAssertEqual(
            NoteEditorView.editorParagraphStyle.lineHeightMultiple, 1.2)

        let textView = NSTextView(frame: NSRect(x: 0, y: 0, width: 400, height: 200))
        textView.string = "line one\nline two"
        let binding = Binding<String>(get: { "" }, set: { _ in })
        let coordinator = NoteEditorView.Coordinator(
            text: binding, onSave: {}, previewEmbedAtCursor: nil)
        coordinator.attach(textView: textView)

        XCTAssertEqual(
            (textView.defaultParagraphStyle)?.lineHeightMultiple, 1.2,
            "attach sets the view default")
        XCTAssertEqual(
            (textView.typingAttributes[.paragraphStyle] as? NSParagraphStyle)?
                .lineHeightMultiple,
            1.2,
            "typed text inherits the style — no per-keystroke restamp")
        let storage = textView.textStorage!
        for offset in [0, storage.length - 1] {
            XCTAssertEqual(
                (storage.attribute(.paragraphStyle, at: offset, effectiveRange: nil)
                    as? NSParagraphStyle)?.lineHeightMultiple,
                1.2,
                "existing buffer is stamped once, full range (offset \(offset))")
        }
    }

    /// Scroll-past-end: the symmetric container inset carries
    /// (top + bottom) / 2 and the `textContainerOrigin` override pins
    /// the top back to 16pt, leaving 120pt of bottom overscroll.
    /// Codex review: a host with NO app-pref route (nil coordinator —
    /// the canvas sheet now routes, but future hosts may not) must keep
    /// AppKit's native toggle behavior, never a silent no-op.
    func testNativeSpellToggleFallsBackToAppKitWhenUnrouted() {
        let textView = SlateEditorTextView(
            frame: NSRect(x: 0, y: 0, width: 400, height: 200))
        textView.isContinuousSpellCheckingEnabled = false
        textView.toggleContinuousSpellChecking(nil)
        XCTAssertTrue(textView.isContinuousSpellCheckingEnabled)
        textView.toggleContinuousSpellChecking(nil)
        XCTAssertFalse(textView.isContinuousSpellCheckingEnabled)
    }

    func testEditorTextViewPinsTopInsetForBottomOverscroll() {
        XCTAssertEqual(SlateEditorTextView.topInset, 16)

        // `bottomOverscroll` is now INSTANCE-configurable (red-team:
        // the canvas card editor passes 0); the main editor default
        // stays 120.
        let textView = SlateEditorTextView(
            frame: NSRect(x: 0, y: 0, width: 400, height: 200))
        XCTAssertEqual(textView.bottomOverscroll, 120, "main-editor default")
        textView.textContainerInset = NSSize(
            width: 16,
            height: (SlateEditorTextView.topInset + textView.bottomOverscroll) / 2)
        XCTAssertEqual(
            textView.textContainerOrigin.y, SlateEditorTextView.topInset,
            "the override pins the visual top inset")
        XCTAssertEqual(
            textView.textContainerInset.height * 2 - SlateEditorTextView.topInset,
            textView.bottomOverscroll,
            "the remainder of the symmetric inset is the bottom overscroll")

        // Compact-host opt-out: zero overscroll keeps symmetric 16pt
        // insets and an unpinned (default) origin path consistent.
        let compact = SlateEditorTextView(
            frame: NSRect(x: 0, y: 0, width: 400, height: 200))
        compact.bottomOverscroll = 0
        compact.textContainerInset = NSSize(
            width: 16, height: SlateEditorTextView.topInset / 2)
        XCTAssertEqual(compact.textContainerOrigin.y, SlateEditorTextView.topInset)
    }
}
