// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

/// GraphAnnouncer (P1-1 #554): the single announcement funnel for graph
/// surfaces. Mirrors `CanvasAnnouncerTests`.
@MainActor
final class GraphAnnouncerTests: XCTestCase {
    /// A recording announcer with a long coalesce window + `flushForTests`
    /// so timing is deterministic (the canvas test harness shape).
    private func makeAnnouncer() -> (GraphAnnouncer, () -> [(String, NSAccessibilityPriorityLevel)]) {
        var posts: [(String, NSAccessibilityPriorityLevel)] = []
        let announcer = GraphAnnouncer(
            verbosity: .standard, coalesceWindow: 60,
            post: { posts.append(($0, $1)) })
        return (announcer, { posts })
    }

    // MARK: Row copy (spec §P1-1, normative)

    func testRowCopyStandardVerbosity() {
        let (a, _) = makeAnnouncer()
        XCTAssertEqual(
            a.rowPhrase(GraphRowRef(label: "Alpha", linksIn: 3, linksOut: 1, isGhost: false, references: 0, isEmbed: false)),
            "Alpha, 3 links in, 1 links out")
        // Ghost phrasing uses references, not links-in/out.
        XCTAssertEqual(
            a.rowPhrase(GraphRowRef(label: "Missing Note", linksIn: 0, linksOut: 0, isGhost: true, references: 2, isEmbed: false)),
            "Missing Note, unresolved, 2 references")
        // The spec template keeps "references" plural regardless of
        // count (verbatim substitution, review round 1 finding 5).
        XCTAssertEqual(
            a.rowPhrase(GraphRowRef(label: "Ghost", linksIn: 0, linksOut: 0, isGhost: true, references: 1, isEmbed: false)),
            "Ghost, unresolved, 1 references")
        // Embed suffix.
        XCTAssertEqual(
            a.rowPhrase(GraphRowRef(label: "Pic", linksIn: 1, linksOut: 0, isGhost: false, references: 0, isEmbed: true)),
            "Pic, 1 links in, 0 links out, embed")
    }

    func testRowCopyTerseCollapsesToLabel() {
        let (a, _) = makeAnnouncer()
        a.verbosity = .terse
        XCTAssertEqual(
            a.rowPhrase(GraphRowRef(label: "Alpha", linksIn: 3, linksOut: 1, isGhost: false, references: 0, isEmbed: false)),
            "Alpha")
    }

    // MARK: Priority + coalescing

    func testRowFocusIsCoalescedFinalStateWins() {
        let (a, posts) = makeAnnouncer()
        a.announce(.rowFocused(GraphRowRef(label: "A", linksIn: 0, linksOut: 0, isGhost: false, references: 0, isEmbed: false)))
        a.announce(.rowFocused(GraphRowRef(label: "B", linksIn: 1, linksOut: 2, isGhost: false, references: 0, isEmbed: false)))
        XCTAssertTrue(posts().isEmpty, "coalesced navigation shouldn't post before flush")
        a.flushForTests()
        XCTAssertEqual(posts().count, 1, "only the final navigation state posts")
        XCTAssertEqual(posts().first?.0, "B, 1 links in, 2 links out")
        XCTAssertEqual(posts().first?.1, .medium)
    }

    func testErrorPostsAssertivelyAndFlushesPendingNavigation() {
        let (a, posts) = makeAnnouncer()
        a.announce(.rowFocused(GraphRowRef(label: "A", linksIn: 0, linksOut: 0, isGhost: false, references: 0, isEmbed: false)))
        a.announce(.error("Boom"))
        // The error supersedes the queued navigation line (canvas §1.5 rule).
        XCTAssertEqual(posts().count, 1)
        XCTAssertEqual(posts().first?.0, "Boom")
        XCTAssertEqual(posts().first?.1, .high)
        a.flushForTests()
        XCTAssertEqual(posts().count, 1, "the stale navigation line was dropped, not posted")
    }

    /// Filter-count narration coalesces on its own class so per-keystroke
    /// filtering announces the resting count once, not every letter
    /// (review round 1 finding 7).
    func testFilterCountIsCoalescedFinalStateWins() {
        let (a, posts) = makeAnnouncer()
        a.announceFilterCount("40 of 247 shown", gate: { true })
        a.announceFilterCount("12 of 247 shown", gate: { true })
        a.announceFilterCount("3 of 247 shown", gate: { true })
        XCTAssertTrue(posts().isEmpty, "coalesced filter shouldn't post before flush")
        a.flushForTests()
        XCTAssertEqual(posts().count, 1, "only the final filter count posts")
        XCTAssertEqual(posts().first?.0, "3 of 247 shown")
        XCTAssertEqual(posts().first?.1, .medium)
    }

    /// The fire-time gate suppresses a queued count when relevance lapses
    /// (focus moved to another split pane before the debounce fired) —
    /// round 3 finding 2.
    func testFilterCountGateSuppressesWhenNoLongerRelevant() {
        let (a, posts) = makeAnnouncer()
        var relevant = true
        a.announceFilterCount("9 of 40 shown", gate: { relevant })
        relevant = false  // focus left the graph within the debounce window
        a.flushForTests()
        XCTAssertTrue(posts().isEmpty, "a count whose gate lapsed must not post")
    }

    /// `cancelPending` drops queued announcements without posting them —
    /// so a coalesced count scheduled while the graph was active can't
    /// fire after the view leaves or the vault closes (round 2 finding 8).
    func testCancelPendingDropsQueuedAnnouncements() {
        let (a, posts) = makeAnnouncer()
        a.announceFilterCount("7 of 20 shown", gate: { true })
        a.announce(.rowFocused(GraphRowRef(label: "A", linksIn: 0, linksOut: 0, isGhost: false, references: 0, isEmbed: false)))
        a.cancelPending()
        a.flushForTests()
        XCTAssertTrue(posts().isEmpty, "cancelled announcements must never post")
    }

    /// A filter count and a navigation line coalesce independently — one
    /// must not swallow the other (distinct event classes).
    func testFilterAndNavigationCoalesceIndependently() {
        let (a, posts) = makeAnnouncer()
        a.announceFilterCount("5 of 9 shown", gate: { true })
        a.announce(.rowFocused(GraphRowRef(label: "A", linksIn: 0, linksOut: 0, isGhost: false, references: 0, isEmbed: false)))
        a.flushForTests()
        XCTAssertEqual(Set(posts().map(\.0)), ["5 of 9 shown", "A, 0 links in, 0 links out"])
    }

    func testSummaryAndReRootPostImmediatelyAtMedium() {
        let (a, posts) = makeAnnouncer()
        a.announce(.summary("247 notes, 1,032 links."))
        // Re-root announces ONLY the label; the authoritative summary
        // follows from the load's own .summary (spec + review round 1
        // finding 5).
        a.announce(.reRooted(label: "Alpha"))
        let all = posts()
        XCTAssertEqual(all.count, 2)
        XCTAssertEqual(all[0].0, "247 notes, 1,032 links.")
        XCTAssertEqual(all[0].1, .medium)
        XCTAssertEqual(all[1].0, "Connections: Alpha")
    }

    // MARK: DoD §H — no direct announcements under Sources/SlateMac/Graph

    func testNoDirectAnnouncementsUnderGraph() throws {
        let graphDir = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // Tests/SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // package root
            .appendingPathComponent("Sources/SlateMac/Graph")
        let files = try FileManager.default.contentsOfDirectory(
            at: graphDir, includingPropertiesForKeys: nil)
        var offenders: [String] = []
        for file in files where file.pathExtension == "swift" {
            if file.lastPathComponent == "GraphAnnouncer.swift" { continue }
            let source = try String(contentsOf: file, encoding: .utf8)
            if source.contains("postAccessibilityAnnouncement") {
                offenders.append(file.lastPathComponent)
            }
        }
        XCTAssertEqual(
            offenders, [],
            "graph code must announce through GraphAnnouncer (DoD §H), not directly")
    }
}
