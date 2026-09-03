// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

/// GraphAnnouncer (P1-1 #554; W6-2 PR 0a #746): the single announcement
/// funnel for graph surfaces. Since 0a the copy is core's — every string
/// below is pinned by the Rust golden (`corpus_renders_the_shipped_strings`)
/// and the corpus census; these tests own what a pure render cannot: the
/// coalescing classes, their independence, the fire-time gate, the cancel,
/// the High flush, and the funnel guard (contracts doc 0a-9).
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

    private func row(_ label: String, _ inLinks: UInt32 = 0, _ outLinks: UInt32 = 0) -> GraphRowCopy {
        GraphRowCopy(
            label: label, kind: .note, inLinks: inLinks, outLinks: outLinks, references: 0,
            embed: false)
    }

    // MARK: The window

    /// The production window is pinned as a number (contracts doc 0a-9);
    /// every behavioural test below runs on a 60 s window and flushes
    /// explicitly, so timing never decides an outcome.
    func testProductionCoalesceWindowIsTwoHundredMilliseconds() {
        XCTAssertEqual(GraphAnnouncer.defaultCoalesceWindow, 0.2, accuracy: 0.0001)
    }

    // MARK: Latest-wins within each of the four classes

    func testRowFocusIsCoalescedFinalStateWins() {
        let (a, posts) = makeAnnouncer()
        a.announce(.graphRow(verbosity: .standard, row: row("A")))
        a.announce(.graphRow(verbosity: .standard, row: row("B", 1, 2)))
        XCTAssertTrue(posts().isEmpty, "coalesced navigation shouldn't post before flush")
        a.flushForTests()
        XCTAssertEqual(posts().count, 1, "only the final navigation state posts")
        XCTAssertEqual(posts().first?.0, "B, 1 links in, 2 links out")
        XCTAssertEqual(posts().first?.1, .medium)
    }

    func testFilterCountIsCoalescedFinalStateWins() {
        let (a, posts) = makeAnnouncer()
        a.announceFilterCount(shown: 40, total: 247, gate: { true })
        a.announceFilterCount(shown: 12, total: 247, gate: { true })
        a.announceFilterCount(shown: 3, total: 247, gate: { true })
        XCTAssertTrue(posts().isEmpty, "coalesced filter shouldn't post before flush")
        a.flushForTests()
        XCTAssertEqual(posts().map(\.0), ["3 of 247 shown"])
    }

    func testForceValueIsCoalescedFinalStateWins() {
        let (a, posts) = makeAnnouncer()
        a.announce(.graphForceValue(control: .repel, percent: 55))
        a.announce(.graphForceValue(control: .repel, percent: 62))
        a.announce(.graphForceValue(control: .repel, percent: 70))
        a.flushForTests()
        XCTAssertEqual(posts().map(\.0), ["Repel force 70 percent"])
    }

    func testSettleIsCoalescedToOnePost() {
        let (a, posts) = makeAnnouncer()
        a.announce(.graphLayoutSettled)
        a.announce(.graphLayoutSettled)
        a.flushForTests()
        XCTAssertEqual(posts().map(\.0), ["Graph layout settled."])
    }

    // MARK: Independence across the classes

    /// One event of each class survives to the flush — no class swallows
    /// another (all six pairs, in one burst).
    func testTheFourClassesCoalesceIndependently() {
        let (a, posts) = makeAnnouncer()
        a.announceFilterCount(shown: 5, total: 9, gate: { true })
        a.announce(.graphRow(verbosity: .standard, row: row("A")))
        a.announce(.graphForceValue(control: .center, percent: 50))
        a.announce(.graphLayoutSettled)
        a.flushForTests()
        XCTAssertEqual(
            Set(posts().map(\.0)),
            ["5 of 9 shown", "A, 0 links in, 0 links out", "Center force 50 percent", "Graph layout settled."])
    }

    // MARK: The gate and the cancel

    /// The fire-time gate suppresses a queued count when relevance lapses
    /// (focus moved to another split pane before the debounce fired).
    func testFilterCountGateSuppressesWhenNoLongerRelevant() {
        let (a, posts) = makeAnnouncer()
        var relevant = true
        a.announceFilterCount(shown: 9, total: 40, gate: { relevant })
        relevant = false  // focus left the graph within the debounce window
        a.flushForTests()
        XCTAssertTrue(posts().isEmpty, "a count whose gate lapsed must not post")
    }

    func testCancelPendingDropsQueuedAnnouncements() {
        let (a, posts) = makeAnnouncer()
        a.announceFilterCount(shown: 7, total: 20, gate: { true })
        a.announce(.graphRow(verbosity: .standard, row: row("A")))
        a.announce(.graphForceValue(control: .link, percent: 10))
        a.announce(.graphLayoutSettled)
        a.cancelPending()
        a.flushForTests()
        XCTAssertTrue(posts().isEmpty, "cancelled announcements must never post")
    }

    // MARK: Priority and the High flush

    func testImmediateEventsPostAtMediumWithCoreCopy() {
        let (a, posts) = makeAnnouncer()
        a.announce(.graphSnapshotSummary(
            counts: GraphSnapshotCounts(notes: 247, links: 1032, orphans: 12, unresolved: 3, filtered: false)))
        a.announce(.graphReRooted(label: "Alpha"))
        let all = posts()
        XCTAssertEqual(all.map(\.0), ["247 notes, 1,032 links. 12 orphans, 3 unresolved targets.", "Connections: Alpha"])
        XCTAssertEqual(all.map(\.1), [.medium, .medium])
    }

    /// An announced High event posts assertively and drops all four
    /// pending classes (0a-9).
    func testBlockedPostsAssertivelyAndFlushesEveryPendingClass() {
        let (a, posts) = makeAnnouncer()
        a.announce(.graphRow(verbosity: .standard, row: row("A")))
        a.announceFilterCount(shown: 1, total: 2, gate: { true })
        a.announce(.graphForceValue(control: .repel, percent: 70))
        a.announce(.graphLayoutSettled)
        a.announce(.graphBlocked(reason: .loadFailed(message: "Boom")))
        XCTAssertEqual(posts().map(\.0), ["Couldn't load the graph: Boom"])
        XCTAssertEqual(posts().first?.1, .high)
        a.flushForTests()
        XCTAssertEqual(posts().count, 1, "the stale lines were dropped, not posted")
    }

    /// A RELAYED High event does the same — the relay keeps core's
    /// priority (0a-D2) and supersedes queued context like any High.
    func testRelayedHighEventFlushesPendingClasses() {
        let (a, posts) = makeAnnouncer()
        a.announce(.graphRow(verbosity: .standard, row: row("A")))
        a.announceFilterCount(shown: 1, total: 2, gate: { true })
        a.announce(.graphForceValue(control: .link, percent: 30))
        a.announce(.graphLayoutSettled)
        a.relay(.graph(event: .graphBlocked(reason: .connectionsLoadFailed(message: "io"))))
        XCTAssertEqual(posts().map(\.0), ["Couldn't load connections: io"])
        XCTAssertEqual(posts().first?.1, .high)
        a.flushForTests()
        XCTAssertEqual(posts().count, 1)
    }

    /// The relay passes a non-graph core event through with ITS rendered
    /// text and priority, uncoalesced.
    func testRelayKeepsCorePriorityAndPostsImmediately() {
        let (a, posts) = makeAnnouncer()
        a.relay(.filesRegionFocused)
        XCTAssertEqual(posts().map(\.0), ["Files."])
        XCTAssertEqual(posts().first?.1, .medium)
    }

    /// Verbosity is a parameter the host passes; terse collapses the row
    /// to its label (the matrix is pinned core-side).
    func testVerbosityIsPassedPerEvent() {
        let (a, posts) = makeAnnouncer()
        a.verbosity = .terse
        a.announce(.graphRow(verbosity: a.verbosity, row: row("Alpha", 3, 1)))
        a.flushForTests()
        XCTAssertEqual(posts().map(\.0), ["Alpha"])
    }

    /// An event that renders to nothing (a node with no visible
    /// neighbours) is not posted.
    func testEmptyRenderIsNotPosted() {
        let (a, posts) = makeAnnouncer()
        a.announce(.graphNeighborsContent(labels: []))
        a.flushForTests()
        XCTAssertTrue(posts().isEmpty)
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
            // Code, not commentary: a doc comment may NAME the primitive
            // it forbids (the announcer's own does), so comment lines are
            // dropped before the scan.
            let code = try String(contentsOf: file, encoding: .utf8)
                .split(separator: "\n", omittingEmptySubsequences: false)
                .filter { !$0.trimmingCharacters(in: .whitespaces).hasPrefix("//") }
                .joined(separator: "\n")
            if code.contains("postAccessibilityAnnouncement") || code.contains(".hostComposed(") {
                offenders.append(file.lastPathComponent)
            }
        }
        XCTAssertEqual(
            offenders, [],
            "graph code must announce through GraphAnnouncer (DoD §H) with typed events, never directly or as host-composed text")
    }
}
