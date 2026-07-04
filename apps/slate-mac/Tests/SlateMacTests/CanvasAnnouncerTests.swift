// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #518 acceptance: grammar strings per verbosity × event (table-driven
/// from t0 §1.2/§1.3), coalescing, priorities, Where-am-I assembly, and
/// the DoD §H funnel guard (no direct announcements under Canvas/).
@MainActor
final class CanvasAnnouncerTests: XCTestCase {
    private var posted: [(text: String, priority: NSAccessibilityPriorityLevel)] = []

    private func makeAnnouncer(
        verbosity: CanvasVerbosity, window: TimeInterval = 60
    ) -> CanvasAnnouncer {
        posted = []
        // A long window + flushForTests() keeps coalescing deterministic.
        return CanvasAnnouncer(verbosity: verbosity, coalesceWindow: window) {
            self.posted.append(($0, $1))
        }
    }

    private static let research = CanvasCardRef(kind: "text", title: "Research")

    func testMovedToMatrixPerVerbosity() {
        // t0 §1.2: terse | standard | verbose rows.
        let cases: [(CanvasVerbosity, String)] = [
            (.terse, "Research"),
            (.standard, "Text card \"Research\", 2 of 5 in Q3"),
            (
                .verbose,
                "Text card \"Research\", 2 of 5 in Q3, 3 connections, red, marked"
            ),
        ]
        for (verbosity, expected) in cases {
            let announcer = makeAnnouncer(verbosity: verbosity)
            announcer.announce(
                .movedTo(
                    card: Self.research, ordinal: 2, total: 5, container: "Q3",
                    connectionCount: 3, colorName: "red", marked: true))
            announcer.flushForTests()
            XCTAssertEqual(posted.map(\.text), [expected], "\(verbosity)")
            XCTAssertEqual(posted.first?.priority, .medium)
        }

        // Root-level container phrases as "canvas"; group cards phrase
        // as Group "label".
        let announcer = makeAnnouncer(verbosity: .standard)
        announcer.announce(
            .movedTo(
                card: CanvasCardRef(kind: "group", title: "Q3"), ordinal: 1, total: 3,
                container: nil, connectionCount: 0, colorName: nil, marked: false))
        announcer.flushForTests()
        XCTAssertEqual(posted.map(\.text), ["Group \"Q3\", 1 of 3 in canvas"])
    }

    func testGroupAndConnectionPhrases() {
        let announcer = makeAnnouncer(verbosity: .standard)
        announcer.announce(.groupEntered(label: "Q3", cardCount: 4))
        announcer.flushForTests()
        announcer.announce(.groupLeft(label: "Q3"))
        announcer.flushForTests()
        announcer.announce(
            .connectionTraversed(
                direction: .outgoing, other: CanvasCardRef(kind: "text", title: "Ideas"),
                label: "supports", towardOther: true))
        announcer.flushForTests()
        announcer.announce(
            .connectionTraversed(
                direction: .outgoing, other: Self.research, label: nil, towardOther: false))
        announcer.flushForTests()
        announcer.announce(
            .connectionTraversed(
                direction: .undirected, other: Self.research, label: nil, towardOther: true))
        announcer.flushForTests()

        XCTAssertEqual(
            posted.map(\.text),
            [
                "Entering group \"Q3\", 4 cards",
                "Leaving group \"Q3\"",
                "Connects to Text card \"Ideas\", labelled \"supports\"",
                "Connected from Text card \"Research\"",
                "Linked with Text card \"Research\"",
            ])
    }

    func testDestructiveConfirmationCarriesUndoHintAtStandardPlus() {
        for (verbosity, expected) in [
            (CanvasVerbosity.terse, "Deleted 3 cards"),
            (.standard, "Deleted 3 cards — ⌘Z to undo"),
            (.verbose, "Deleted 3 cards — ⌘Z to undo"),
        ] {
            let announcer = makeAnnouncer(verbosity: verbosity)
            announcer.announce(.destructiveConfirmation("Deleted 3 cards"))
            XCTAssertEqual(posted.map(\.text), [expected], "\(verbosity)")
        }
    }

    func testCoalescingCollapsesRapidNavigationFinalStateWins() {
        let announcer = makeAnnouncer(verbosity: .terse)
        // A held arrow: five rapid moves — exactly one post, the LAST.
        for title in ["A", "B", "C", "D", "E"] {
            announcer.announce(
                .movedTo(
                    card: CanvasCardRef(kind: "text", title: title), ordinal: 1, total: 5,
                    container: nil, connectionCount: 0, colorName: nil, marked: false))
        }
        XCTAssertTrue(posted.isEmpty, "still inside the coalescing window")
        announcer.flushForTests()
        XCTAssertEqual(posted.map(\.text), ["E"])

        // Confirmations are immediate (never debounced).
        announcer.announce(.confirmation("Created text card below \"E\""))
        XCTAssertEqual(posted.count, 2)
    }

    func testErrorsAreAssertiveAndSupersedePendingNavigation() {
        let announcer = makeAnnouncer(verbosity: .terse)
        announcer.announce(
            .movedTo(
                card: Self.research, ordinal: 1, total: 1, container: nil,
                connectionCount: 0, colorName: nil, marked: false))
        announcer.announce(.error("Save conflict: the file changed on disk."))
        XCTAssertEqual(posted.count, 1, "pending navigation dropped, error posted")
        XCTAssertEqual(posted.first?.priority, .high)
        announcer.flushForTests()
        XCTAssertEqual(posted.count, 1, "stale navigation never resurfaces")
    }

    func testWhereAmIIsAlwaysVerboseGrade() {
        let announcer = makeAnnouncer(verbosity: .terse)
        let ctx = CanvasWhereAmI(
            nodeId: "n1", title: "Research", kind: "text",
            groupPath: ["Quarter", "Q3"], ordinalN: 2, totalM: 5,
            connectionCount: 3, inCount: 1, outCount: 2, colorName: "red")
        let text = announcer.whereAmIText(
            ctx, marked: true, activeMode: "Move mode", filterSummary: "3 of 40 shown")
        XCTAssertEqual(
            text,
            "Text card \"Research\", in Quarter › Q3, 2 of 5, "
                + "3 connections (1 in, 2 out), red, marked, Move mode, 3 of 40 shown")

        // Root-level, minimal context.
        let bare = announcer.whereAmIText(
            CanvasWhereAmI(
                nodeId: "n2", title: "Loose", kind: "text", groupPath: [],
                ordinalN: 1, totalM: 1, connectionCount: 1, inCount: 1, outCount: 0,
                colorName: nil),
            marked: false, activeMode: nil, filterSummary: nil)
        XCTAssertEqual(
            bare, "Text card \"Loose\", at canvas level, 1 of 1, 1 connection (1 in, 0 out)")
    }

    func testCreatedAndUndoBuilders() {
        XCTAssertEqual(
            CanvasAnnouncer.createdText(
                card: CanvasCardRef(kind: "text", title: "New idea"),
                relative: .below(anchorTitle: "Research")),
            "Created text card \"New idea\" below \"Research\"")
        XCTAssertEqual(
            CanvasAnnouncer.relativePhrase(.atOrigin), "at the canvas origin")
        XCTAssertEqual(CanvasAnnouncer.undidText(actionName: "move 'Research'"), "Undid: move 'Research'")
    }

    /// DoD §H guard: no canvas source calls postAccessibilityAnnouncement
    /// directly — everything routes through the announcer. Source-scan
    /// lint (the announcer itself is the single allowed caller).
    func testNoDirectAnnouncementsUnderCanvas() throws {
        let testsDir = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        let canvasDir =
            testsDir
            .deletingLastPathComponent()  // Tests/SlateMacTests → Tests
            .deletingLastPathComponent()  // → package root
            .appendingPathComponent("Sources/SlateMac/Canvas")
        let files = try FileManager.default.contentsOfDirectory(
            at: canvasDir, includingPropertiesForKeys: nil)
        var offenders: [String] = []
        for file in files where file.pathExtension == "swift" {
            if file.lastPathComponent == "CanvasAnnouncer.swift" { continue }
            let source = try String(contentsOf: file, encoding: .utf8)
            if source.contains("postAccessibilityAnnouncement") {
                offenders.append(file.lastPathComponent)
            }
        }
        XCTAssertEqual(
            offenders, [],
            "canvas code must announce through CanvasAnnouncer (DoD §H), not directly")
    }
}
