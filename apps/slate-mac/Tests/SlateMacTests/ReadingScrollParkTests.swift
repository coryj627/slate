// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Reading-scroll preservation (#856): the per-tab park in
/// `WorkspaceState` — the reading-mode sibling of the U3-2 caret park.
/// Transient by contract: never persisted, cleared exactly where
/// `viewModes` entries die, invalidated by a note switch.
@MainActor
final class ReadingScrollParkTests: XCTestCase {

    // MARK: Round trip

    /// The headline contract: editing → reading (scroll to block N) →
    /// editing → reading restores N. Mode flips must NOT clear the
    /// park — only tab close / reset / note switch do.
    func testReadingScrollSurvivesModeRoundTrip() {
        let ws = WorkspaceState()
        let tab = ws.openTab(.markdown(path: "long.md"))

        ws.setViewMode(.reading, for: tab)
        // The mounted ReadingView reports the topmost block continuously.
        ws.parkReadingScroll(blockIndex: 42, path: "long.md", for: tab)

        // Toggle back to editing (reading view unmounts)…
        ws.setViewMode(.editing, for: tab)
        // …and return to reading: the remount restores block 42.
        ws.setViewMode(.reading, for: tab)
        XCTAssertEqual(
            ws.parkedReadingScroll(for: tab, path: "long.md"), 42,
            "the reading offset survives the editing round trip")
    }

    func testLatestParkWins() {
        let ws = WorkspaceState()
        let tab = ws.openTab(.markdown(path: "a.md"))
        ws.parkReadingScroll(blockIndex: 3, path: "a.md", for: tab)
        ws.parkReadingScroll(blockIndex: 9, path: "a.md", for: tab)
        XCTAssertEqual(ws.parkedReadingScroll(for: tab, path: "a.md"), 9)
    }

    func testParksAreIndependentPerTab() {
        let ws = WorkspaceState()
        let a = ws.openTab(.markdown(path: "a.md"))
        let b = ws.openTab(.markdown(path: "b.md"))
        ws.parkReadingScroll(blockIndex: 5, path: "a.md", for: a)
        ws.parkReadingScroll(blockIndex: 11, path: "b.md", for: b)
        XCTAssertEqual(ws.parkedReadingScroll(for: a, path: "a.md"), 5)
        XCTAssertEqual(ws.parkedReadingScroll(for: b, path: "b.md"), 11)
    }

    // MARK: Note switch invalidation

    /// #856's "clear on note switch": the park is keyed to the note
    /// path, so the same tab showing a different note restores nothing.
    func testParkIsInvalidatedByPathChange() {
        let ws = WorkspaceState()
        let tab = ws.openTab(.markdown(path: "a.md"))
        ws.parkReadingScroll(blockIndex: 7, path: "a.md", for: tab)
        XCTAssertNil(
            ws.parkedReadingScroll(for: tab, path: "b.md"),
            "another note's offset must never restore")
        XCTAssertNil(
            ws.parkedReadingScroll(for: tab, path: nil),
            "no selection → no restore")
    }

    /// Replacing the active tab's item (single-click open into the
    /// same tab) purges the park outright — the documents-purge rule.
    func testReplaceActiveItemPurgesPark() {
        let ws = WorkspaceState()
        let tab = ws.openTab(.markdown(path: "a.md"))
        ws.parkReadingScroll(blockIndex: 7, path: "a.md", for: tab)
        ws.replaceActiveItem(.markdown(path: "b.md"))
        XCTAssertNil(
            ws.parkedReadingScroll(for: tab, path: "a.md"),
            "the replaced item's park is dropped with its parked document")
    }

    // MARK: Lifecycle clearing (the viewModes rules)

    func testCloseClearsPark() {
        let ws = WorkspaceState()
        let a = ws.openTab(.markdown(path: "a.md"))
        let b = ws.openTab(.markdown(path: "b.md"))
        ws.parkReadingScroll(blockIndex: 4, path: "a.md", for: a)
        ws.parkReadingScroll(blockIndex: 6, path: "b.md", for: b)
        _ = ws.close(a)
        XCTAssertNil(ws.parkedReadingScroll(for: a, path: "a.md"))
        XCTAssertEqual(
            ws.parkedReadingScroll(for: b, path: "b.md"), 6,
            "other tabs' parks are untouched")
    }

    func testResetClearsParks() {
        let ws = WorkspaceState()
        let tab = ws.openTab(.markdown(path: "a.md"))
        ws.parkReadingScroll(blockIndex: 4, path: "a.md", for: tab)
        ws.reset()
        XCTAssertTrue(ws.readingScrollParks.isEmpty)
    }

    /// Session restore starts with no parks — the park is transient
    /// and deliberately not part of the workspace.json schema (a stale
    /// block index against a since-edited file would restore garbage).
    func testAdoptStartsWithNoParks() {
        let ws = WorkspaceState()
        let tab = ws.openTab(.markdown(path: "a.md"))
        ws.parkReadingScroll(blockIndex: 4, path: "a.md", for: tab)

        var restored = WorkspaceModel()
        _ = restored.openTab(.markdown(path: "a.md"))
        ws.adopt(restored)
        XCTAssertTrue(ws.readingScrollParks.isEmpty)
    }

    /// Red-team: a rename while the note is parked in reading mode must
    /// rebind the park's path — retarget used to orphan it (old path
    /// never matches again; the offset silently lost).
    func testRetargetRebindsParkPath() {
        let ws = WorkspaceState()
        let tab = ws.openTab(.markdown(path: "old.md"))
        ws.parkReadingScroll(blockIndex: 7, path: "old.md", for: tab)

        let changed = ws.retarget(old: "old.md", new: "new.md")
        XCTAssertEqual(changed, [tab])
        XCTAssertEqual(ws.readingScrollParks[tab]?.path, "new.md")
        XCTAssertEqual(ws.readingScrollParks[tab]?.blockIndex, 7)
    }
}
