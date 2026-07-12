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
