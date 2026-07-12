// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

/// Editor text zoom (#848): the discrete ladder stepping, the
/// clamped ends, persistence through `PreferencesStore` (app-level
/// UserDefaults — NOT the CLI-shared vault prefs.json), the corrupt-
/// value load guard, and the `monospacedBodyNSFont(scale:)` seam the
/// editor/code surfaces render through.
@MainActor
final class EditorTextZoomTests: XCTestCase {

    private var defaults: UserDefaults!
    private var suiteName: String!
    private var tempDir: URL!

    override func setUpWithError() throws {
        suiteName = "slate.prefs.zoom.tests.\(UUID().uuidString)"
        defaults = UserDefaults(suiteName: suiteName)
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("editor-zoom-\(UUID().uuidString)")
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

    // MARK: Ladder stepping

    func testZoomStepsUpAndDownTheLadder() {
        let state = makeAppState()
        XCTAssertEqual(state.editorTextScale, 1.0, "base size by default")

        state.editorZoomIn()
        XCTAssertEqual(state.editorTextScale, 1.1)
        state.editorZoomIn()
        XCTAssertEqual(state.editorTextScale, 1.25)
        state.editorZoomOut()
        XCTAssertEqual(state.editorTextScale, 1.1)
        state.editorZoomOut()
        XCTAssertEqual(state.editorTextScale, 1.0)
        state.editorZoomOut()
        XCTAssertEqual(state.editorTextScale, 0.9, "one rung below base exists")
    }

    func testActualSizeResetsToBase() {
        let state = makeAppState()
        state.editorZoomIn()
        state.editorZoomIn()
        state.editorActualSize()
        XCTAssertEqual(state.editorTextScale, 1.0)
    }

    func testZoomClampsAtLadderEnds() {
        let state = makeAppState()
        for _ in 0..<10 { state.editorZoomIn() }
        XCTAssertEqual(
            state.editorTextScale, AppState.editorTextScaleSteps.last,
            "zoom in pins at the top rung")
        for _ in 0..<20 { state.editorZoomOut() }
        XCTAssertEqual(
            state.editorTextScale, AppState.editorTextScaleSteps.first,
            "zoom out pins at the bottom rung")
    }

    // MARK: Persistence

    func testZoomPersistsAcrossAppStates() {
        let first = makeAppState()
        first.editorZoomIn()
        XCTAssertEqual(first.editorTextScale, 1.1)

        let second = makeAppState()
        XCTAssertEqual(
            second.editorTextScale, 1.1,
            "the scale round-trips through PreferencesStore")
    }

    func testActualSizePersistsTheReset() {
        let first = makeAppState()
        first.editorZoomIn()
        first.editorActualSize()
        let second = makeAppState()
        XCTAssertEqual(second.editorTextScale, 1.0)
    }

    // MARK: Load guard

    func testLoadEditorTextScaleDefaultsAndClampsCorruptValues() {
        let store = PreferencesStore(defaults: defaults)
        XCTAssertEqual(store.loadEditorTextScale(), 1.0, "absent key → base")

        defaults.set(-3.0, forKey: PreferencesStore.editorTextScaleKey)
        XCTAssertEqual(store.loadEditorTextScale(), 1.0, "non-positive → base")

        defaults.set(0.0, forKey: PreferencesStore.editorTextScaleKey)
        XCTAssertEqual(store.loadEditorTextScale(), 1.0, "zero → base")

        defaults.set(100.0, forKey: PreferencesStore.editorTextScaleKey)
        XCTAssertEqual(
            store.loadEditorTextScale(), 3.0,
            "absurd hand-edited values clamp into the sane band")

        defaults.set("garbage", forKey: PreferencesStore.editorTextScaleKey)
        XCTAssertEqual(store.loadEditorTextScale(), 1.0, "non-numeric → base")
    }

    func testCorruptPersistedScaleNormalizesToLadderAtLoad() {
        // Codex review: normalization happens at LOAD (nearest rung),
        // so runtime state is always on-ladder and stepping can never
        // move against the pressed direction.
        defaults.set(2.9, forKey: PreferencesStore.editorTextScaleKey)
        let state = makeAppState()
        XCTAssertEqual(state.editorTextScale, 1.6, "2.9 snaps to the max rung at load")
        state.editorZoomOut()
        XCTAssertEqual(state.editorTextScale, 1.4, "then steps one rung down")
    }

    // MARK: Font seam

    func testMonospacedBodyNSFontScaleMultipliesPointSize() {
        let base = Tokens.Typography.monospacedBodyNSFont().pointSize
        let zoomed = Tokens.Typography.monospacedBodyNSFont(scale: 1.25).pointSize
        XCTAssertEqual(zoomed, base * 1.25, accuracy: 0.001)
        XCTAssertEqual(
            Tokens.Typography.monospacedBodyNSFont(scale: 1.0).pointSize, base,
            "scale 1.0 is exactly the base size")
    }

    func testAttributedCodeStringUsesScaledFont() throws {
        let block = CodeBlock(
            source: "let x = 1\n",
            language: "swift",
            tokens: [],
            semanticSpans: [],
            line: 1,
            byteOffset: 0)
        let attributed = attributedString(for: block, scale: 1.4)
        let font = try XCTUnwrap(
            attributed.attribute(.font, at: 0, effectiveRange: nil) as? NSFont)
        XCTAssertEqual(
            font.pointSize,
            Tokens.Typography.monospacedBodyNSFont(scale: 1.4).pointSize,
            accuracy: 0.001)
    }
    // MARK: - Codex review (chords PR): directional stepping + load normalization

    /// Build state with a pre-seeded (possibly corrupt) stored scale —
    /// the defaults suite is what `makeAppState`'s PreferencesStore reads.
    private func makeZoomState(stored: Double) -> AppState {
        defaults.set(stored, forKey: PreferencesStore.editorTextScaleKey)
        return makeAppState()
    }

    /// Stored 0.5 (below the ladder) must land on the MIN rung at load;
    /// Zoom Out from there stays pinned — never moves up.
    func testCorruptLowValueNormalizesAndZoomOutStaysPinned() {
        XCTAssertEqual(AppState.nearestEditorTextRung(to: 0.5), 0.9)
        let state = makeZoomState(stored: 0.5)
        XCTAssertEqual(state.editorTextScale, 0.9)
        state.editorZoomOut()
        XCTAssertEqual(state.editorTextScale, 0.9, "Zoom Out must never raise the scale")
    }

    /// Stored 3.0 (above the ladder) lands on MAX; Zoom In stays pinned.
    func testCorruptHighValueNormalizesAndZoomInStaysPinned() {
        XCTAssertEqual(AppState.nearestEditorTextRung(to: 3.0), 1.6)
        let state = makeZoomState(stored: 3.0)
        XCTAssertEqual(state.editorTextScale, 1.6)
        state.editorZoomIn()
        XCTAssertEqual(state.editorTextScale, 1.6, "Zoom In must never lower the scale")
    }

    /// Off-ladder mid value: normalized to the nearest rung at load
    /// (1.3 → 1.25), then steps are plain adjacent rungs — never a
    /// direction reversal.
    func testOffLadderMidValueNormalizesThenStepsAdjacent() {
        let state = makeZoomState(stored: 1.3)
        XCTAssertEqual(state.editorTextScale, 1.25, "1.3 loads as the nearest rung")
        state.editorZoomIn()
        XCTAssertEqual(state.editorTextScale, 1.4)
        let state2 = makeZoomState(stored: 1.3)
        state2.editorZoomOut()
        XCTAssertEqual(state2.editorTextScale, 1.1)
    }
}
