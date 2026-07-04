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

    // MARK: - displayName

    func testDisplayNameStripsMarkdownExtension() {
        XCTAssertEqual(row("a/foo.md", "foo.md").displayName, "foo")
        XCTAssertEqual(row("a/foo.markdown", "foo.markdown").displayName, "foo")
    }

    func testDisplayNameKeepsInteriorDots() {
        // Only the final extension is stripped — a dotted stem survives.
        XCTAssertEqual(row("x/2026.01.notes.md", "2026.01.notes.md").displayName, "2026.01.notes")
    }

    func testDisplayNameLeavesNonMarkdownUntouched() {
        XCTAssertEqual(row("x/diagram.png", "diagram.png").displayName, "diagram.png")
    }

    // MARK: - Fuzzy scoring: name-over-path bias

    /// The load-bearing rule: `foo` ranks `foo.md` (a name hit) above a
    /// file whose only match is via its folder path.
    func testScoreBiasesNameOverPathMatch() {
        let name = QuickSwitcherModel.score(
            query: "foo", row: row("foo.md", "foo.md"))!
        let pathOnly = QuickSwitcherModel.score(
            query: "foo", row: row("notes/foo-archive/bar.md", "bar.md"))!
        XCTAssertGreaterThan(
            name, pathOnly,
            "a name match must outrank a path-only match for the same query")
    }

    /// The bias is exactly `nameMatchBonus` over the bare name score —
    /// pins the constant so a silent change to it fails here.
    func testScoreAddsNameBonusOnTopOfNameFuzzyScore() {
        let r = row("dir/foo.md", "foo.md")
        let bareName = CommandPaletteModel.fuzzyScore(query: "foo", target: "foo")!
        let score = QuickSwitcherModel.score(query: "foo", row: r)!
        // The name ("foo" after stripping) is a prefix hit; adding the
        // bonus is what the model does. The path also matches ("dir/foo.md"
        // contains "foo"), but the boosted name score wins the max.
        XCTAssertEqual(score, bareName + QuickSwitcherModel.nameMatchBonus)
    }

    // MARK: - Fuzzy scoring: path matching still works

    func testScoreMatchesViaPathWhenNameDoesNot() {
        // Query "dir" is nowhere in the name "bar.md" but is in the path.
        let score = QuickSwitcherModel.score(query: "dir", row: row("dir/bar.md", "bar.md"))
        XCTAssertNotNil(score, "a path-only subsequence match must still score")
    }

    func testScoreIsCaseInsensitive() {
        let lower = QuickSwitcherModel.score(query: "foo", row: row("A/Foo.md", "Foo.md"))
        let upper = QuickSwitcherModel.score(query: "FOO", row: row("A/Foo.md", "Foo.md"))
        XCTAssertNotNil(lower)
        XCTAssertEqual(lower, upper)
    }

    func testScoreReturnsNilForNonMatch() {
        XCTAssertNil(
            QuickSwitcherModel.score(query: "zzz", row: row("a/foo.md", "foo.md")),
            "a query matching neither name nor path must return nil")
    }

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

    @MainActor
    func testInitialAnnouncementReportsRecentCount() {
        let model = QuickSwitcherModel()
        model.load(files: rows([("a.md", "a.md"), ("b.md", "b.md")]), recents: [])
        model.announceInitialCount()
        XCTAssertEqual(model.resultAnnouncement, "2 recent files")
    }

    @MainActor
    func testAnnouncementSingularRecent() {
        let model = QuickSwitcherModel()
        model.load(files: rows([("a.md", "a.md")]), recents: [])
        model.announceInitialCount()
        XCTAssertEqual(model.resultAnnouncement, "1 recent file")
    }

    @MainActor
    func testAnnouncementReportsMatchCount() {
        let model = QuickSwitcherModel()
        model.load(files: rows([("foo.md", "foo.md"), ("food.md", "food.md")]), recents: [])
        model.query = "foo"
        model.handleQueryChange()
        XCTAssertEqual(model.resultAnnouncement, "2 files matching \"foo\"")
    }

    @MainActor
    func testAnnouncementSingularMatch() {
        let model = QuickSwitcherModel()
        model.load(files: rows([("foo.md", "foo.md"), ("bar.md", "bar.md")]), recents: [])
        model.query = "foo"
        model.handleQueryChange()
        XCTAssertEqual(model.resultAnnouncement, "1 file matching \"foo\"")
    }

    @MainActor
    func testAnnouncementNoMatches() {
        let model = QuickSwitcherModel()
        model.load(files: rows([("foo.md", "foo.md")]), recents: [])
        model.query = "zzz"
        model.handleQueryChange()
        XCTAssertEqual(model.resultAnnouncement, "No files matching \"zzz\"")
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
