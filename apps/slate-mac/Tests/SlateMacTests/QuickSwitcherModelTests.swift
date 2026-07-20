// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

/// Tests for the quick switcher view-model (#495).
///
/// Mirrors `CommandPaletteViewTests`' model coverage: fuzzy scoring
/// (here with the name-over-path bias), ordering (recency-first empty
/// query, score-then-recency tie-break), snap-to-first, arrow-wrap
/// navigation, and the result-count announcement.
final class QuickSwitcherModelTests: XCTestCase {

    private func row(_ path: String, _ name: String) -> QuickSwitcherModel.FileRow {
        QuickSwitcherModel.FileRow(path: path, name: name)
    }

    private func rows(_ pairs: [(String, String)]) -> [QuickSwitcherModel.FileRow] {
        pairs.map { row($0.0, $0.1) }
    }

    // The displayName + score unit tests moved to Rust with the logic
    // itself (W0.5-2 #718): `slate_core::switcher::tests` carries the
    // same cases as goldens — extension stripping, the name-over-path
    // bias with the exact `NAME_MATCH_BONUS` pin, path-only matches,
    // case-insensitivity, and the non-match `None`. Ranking behavior
    // through the model is still exercised end-to-end below.

    // MARK: - Ordering: empty query = recency first

    @MainActor
    func testEmptyQueryOrdersRecentsFirstThenIncomingOrder() {
        let model = QuickSwitcherModel()
        model.load(
            files: rows([
                ("a.md", "a.md"), ("b.md", "b.md"), ("c.md", "c.md"), ("d.md", "d.md"),
            ]),
            recents: ["c.md", "a.md"])
        // Recents in recency order, then the rest in incoming order.
        XCTAssertEqual(
            model.displayOrder.map(\.path),
            ["c.md", "a.md", "b.md", "d.md"])
    }

    @MainActor
    func testEmptyQueryPrunesRecentsMissingFromFiles() {
        let model = QuickSwitcherModel()
        model.load(
            files: rows([("a.md", "a.md"), ("b.md", "b.md")]),
            // "gone.md" was deleted since last session.
            recents: ["gone.md", "b.md"])
        XCTAssertEqual(
            model.displayOrder.map(\.path), ["b.md", "a.md"],
            "a recent whose file no longer exists is pruned from the order")
    }

    // MARK: - Ordering: non-empty query = score, recency only breaks ties

    @MainActor
    func testNonEmptyQuerySortsByScoreThenRecencyTiebreak() {
        let model = QuickSwitcherModel()
        // Two files with an identical name → identical fuzzy score for
        // "note"; recency breaks the tie.
        model.load(
            files: rows([
                ("alpha/note.md", "note.md"),
                ("beta/note.md", "note.md"),
            ]),
            recents: ["beta/note.md"])
        model.query = "note"
        XCTAssertEqual(
            model.displayOrder.map(\.path),
            ["beta/note.md", "alpha/note.md"],
            "equal scores → the more recently opened file sorts first")
    }

    @MainActor
    func testRecencyDoesNotBeatAMateriallyBetterFuzzyScore() {
        let model = QuickSwitcherModel()
        // "meeting-notes.md" is a far weaker match for "notes" than
        // "notes.md", yet it was opened recently. Score must still win.
        model.load(
            files: rows([
                ("notes.md", "notes.md"),
                ("archive/meeting-notes.md", "meeting-notes.md"),
            ]),
            recents: ["archive/meeting-notes.md"])
        model.query = "notes"
        XCTAssertEqual(
            model.displayOrder.first?.path, "notes.md",
            "a materially better fuzzy match must outrank a recent-but-worse one")
    }

    @MainActor
    func testNonEmptyQueryExcludesNonMatches() {
        let model = QuickSwitcherModel()
        model.load(
            files: rows([("foo.md", "foo.md"), ("bar.md", "bar.md")]),
            recents: [])
        model.query = "foo"
        XCTAssertEqual(
            model.displayOrder.map(\.path), ["foo.md"],
            "non-matching files are excluded from a non-empty query")
    }

    // MARK: - Snap-to-first + arrow nav

    @MainActor
    func testLoadSelectsFirstRow() {
        let model = QuickSwitcherModel()
        model.load(files: rows([("a.md", "a.md"), ("b.md", "b.md")]), recents: [])
        XCTAssertEqual(model.selectedID, "a.md")
    }

    @MainActor
    func testQueryChangeSnapsSelectionToFirstMatch() {
        let model = QuickSwitcherModel()
        model.load(files: rows([("a.md", "a.md"), ("b.md", "b.md")]), recents: [])
        model.selectedID = "b.md"
        model.query = "a"
        model.handleQueryChange()
        XCTAssertEqual(model.selectedID, "a.md", "selection snaps to the first remaining match")
    }

    @MainActor
    func testQueryChangeWithNoMatchesNilsSelection() {
        let model = QuickSwitcherModel()
        model.load(files: rows([("a.md", "a.md")]), recents: [])
        model.query = "zzz"
        model.handleQueryChange()
        XCTAssertNil(model.selectedID)
    }

    @MainActor
    func testSelectNextWrapsAtEnd() {
        let model = QuickSwitcherModel()
        model.load(
            files: rows([("a.md", "a.md"), ("b.md", "b.md"), ("c.md", "c.md")]),
            recents: [])
        XCTAssertEqual(model.selectedID, "a.md")
        model.selectNext(); XCTAssertEqual(model.selectedID, "b.md")
        model.selectNext(); XCTAssertEqual(model.selectedID, "c.md")
        model.selectNext(); XCTAssertEqual(model.selectedID, "a.md")
    }

    @MainActor
    func testSelectPreviousWrapsAtStart() {
        let model = QuickSwitcherModel()
        model.load(
            files: rows([("a.md", "a.md"), ("b.md", "b.md"), ("c.md", "c.md")]),
            recents: [])
        XCTAssertEqual(model.selectedID, "a.md")
        model.selectPrevious(); XCTAssertEqual(model.selectedID, "c.md")
    }

    @MainActor
    func testSelectedRowResolvesToDisplayedRow() {
        let model = QuickSwitcherModel()
        model.load(files: rows([("a.md", "a.md"), ("b.md", "b.md")]), recents: [])
        model.selectedID = "b.md"
        XCTAssertEqual(model.selectedRow?.path, "b.md")
    }

    // MARK: - Display cap

    @MainActor
    func testDisplayOrderCapsButAnnouncementReportsTotal() {
        let model = QuickSwitcherModel()
        let many = (0..<(QuickSwitcherModel.displayCap + 20)).map {
            ("note\($0).md", "note\($0).md")
        }
        model.load(files: rows(many), recents: [])
        model.query = "note"
        model.handleQueryChange()
        XCTAssertEqual(
            model.displayOrder.count, QuickSwitcherModel.displayCap,
            "rendered rows are capped")
        XCTAssertEqual(
            model.matches.count, QuickSwitcherModel.displayCap + 20,
            "matches (and thus the announcement) reflect the TOTAL")
    }

    // MARK: - Result-count announcement

    // #963: the model publishes typed events; core renders the text.
    // Each test asserts BOTH the event choice (the model's trigger
    // contract) and the exact rendered string (the shipped copy, now
    // pinned core-side by the §W-D corpus goldens).

    @MainActor
    func testInitialAnnouncementReportsRecentCount() {
        let model = QuickSwitcherModel()
        model.load(files: rows([("a.md", "a.md"), ("b.md", "b.md")]), recents: [])
        model.announceInitialCount()
        XCTAssertEqual(model.resultAnnouncement, .switcherRecentCount(count: 2))
        XCTAssertEqual(renderedText(model), "2 recent files")
    }

    @MainActor
    func testAnnouncementZeroRecents() {
        // The copy decision, locked (Codoki on #971): an empty recency
        // list announces "0 recent files" rather than staying silent.
        let model = QuickSwitcherModel()
        model.load(files: [], recents: [])
        model.announceInitialCount()
        XCTAssertEqual(model.resultAnnouncement, .switcherRecentCount(count: 0))
        XCTAssertEqual(renderedText(model), "0 recent files")
    }

    @MainActor
    func testAnnouncementSingularRecent() {
        let model = QuickSwitcherModel()
        model.load(files: rows([("a.md", "a.md")]), recents: [])
        model.announceInitialCount()
        XCTAssertEqual(model.resultAnnouncement, .switcherRecentCount(count: 1))
        XCTAssertEqual(renderedText(model), "1 recent file")
    }

    @MainActor
    func testAnnouncementReportsMatchCount() {
        let model = QuickSwitcherModel()
        model.load(files: rows([("foo.md", "foo.md"), ("food.md", "food.md")]), recents: [])
        model.query = "foo"
        model.handleQueryChange()
        XCTAssertEqual(model.resultAnnouncement, .switcherMatchCount(count: 2, query: "foo"))
        XCTAssertEqual(renderedText(model), "2 files matching \"foo\"")
    }

    @MainActor
    func testAnnouncementSingularMatch() {
        let model = QuickSwitcherModel()
        model.load(files: rows([("foo.md", "foo.md"), ("bar.md", "bar.md")]), recents: [])
        model.query = "foo"
        model.handleQueryChange()
        XCTAssertEqual(model.resultAnnouncement, .switcherMatchCount(count: 1, query: "foo"))
        XCTAssertEqual(renderedText(model), "1 file matching \"foo\"")
    }

    @MainActor
    func testAnnouncementNoMatches() {
        let model = QuickSwitcherModel()
        model.load(files: rows([("foo.md", "foo.md")]), recents: [])
        model.query = "zzz"
        model.handleQueryChange()
        XCTAssertEqual(model.resultAnnouncement, .switcherNoMatches(query: "zzz"))
        XCTAssertEqual(renderedText(model), "No files matching \"zzz\"")
    }

    /// Render the model's pending event through the canonical vocabulary
    /// — the same call the view's poster makes.
    @MainActor
    private func renderedText(_ model: QuickSwitcherModel) -> String? {
        model.resultAnnouncement.map { a11yRender(event: $0).text }
    }

    @MainActor
    func testClearAnnouncementResetsToNil() {
        let model = QuickSwitcherModel()
        model.load(files: rows([("a.md", "a.md")]), recents: [])
        model.announceInitialCount()
        XCTAssertNotNil(model.resultAnnouncement)
        model.clearAnnouncement()
        XCTAssertNil(model.resultAnnouncement)
    }
}
