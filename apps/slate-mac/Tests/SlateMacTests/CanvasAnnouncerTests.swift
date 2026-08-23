// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #518 acceptance for what the HOST still owns after W6-1 PR 0a:
/// coalescing (t0 §1.5), the error flush/supersede rule, the priority
/// relay, and the DoD §H funnel guard.
///
/// The GRAMMAR is pinned in `slate_core::a11y`, not here — this suite
/// used to hold a second, partial copy of the tables:
/// - `corpus_renders_the_shipped_strings` — every shipped canvas
///   string, complete (replaces `testGroupAndConnectionPhrases`; the
///   `towardOther` inversion it exercised is gone, CD-9).
/// - `canvas_verbosity_matrix_pins_every_level` — the t0 §1.2/§1.3
///   matrix (replaces `testMovedToMatrixPerVerbosity` and
///   `testDestructiveConfirmationCarriesUndoHintAtStandardPlus`).
/// - `canvas_where_am_i_is_always_verbose_grade` (replaces
///   `testWhereAmIIsAlwaysVerboseGrade`).
/// - `canvas_priorities_pin_the_error_tier` — the High membership.
///
/// The mac half of those pins is `A11yCorpusCensusTests`, which renders
/// all 165 canvas entries through this exact FFI path and asserts text,
/// priority AND event identity against the committed artifact.
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

    /// A terse move, which renders to the bare title — the cheapest
    /// event whose text names the step it came from.
    private func movedTo(_ title: String) -> CanvasA11yEvent {
        .canvasMovedTo(
            verbosity: .terse, kindLabel: "text", title: title,
            ordinalN: 1, totalM: 5, container: nil, connectionCount: 0,
            colorName: nil, marked: false)
    }

    func testCoalescingCollapsesRapidNavigationFinalStateWins() {
        let announcer = makeAnnouncer(verbosity: .terse)
        // A held arrow: five rapid moves — exactly one post, the LAST.
        for title in ["A", "B", "C", "D", "E"] {
            announcer.announce(movedTo(title))
        }
        XCTAssertTrue(posted.isEmpty, "still inside the coalescing window")
        announcer.flushForTests()
        XCTAssertEqual(posted.map(\.text), ["E"])
        XCTAssertEqual(posted.first?.priority, .medium)

        // Confirmations are immediate (never debounced).
        announcer.announce(
            .canvasCreated(
                kindLabel: "text", title: "New idea",
                relative: .below(anchorTitle: "E")))
        XCTAssertEqual(posted.count, 2)
        XCTAssertEqual(posted.last?.text, "Created text card \"New idea\" below \"E\"")
    }

    /// The two classes are independent: a filter burst must not cancel
    /// a pending navigation line (t0 §1.5, contracts doc 0a-8).
    func testNavigationAndFilterCoalesceIndependently() {
        let announcer = makeAnnouncer(verbosity: .standard)
        announcer.announce(movedTo("A"))
        announcer.announce(.canvasFilterCount(matched: 2))
        announcer.announce(.canvasFilterCount(matched: 3))
        XCTAssertTrue(posted.isEmpty)
        announcer.flushForTests()
        XCTAssertEqual(Set(posted.map(\.text)), ["A", "3 cards match."])
    }

    func testErrorsAreAssertiveAndSupersedePendingNavigation() {
        let announcer = makeAnnouncer(verbosity: .terse)
        announcer.announce(movedTo("Research"))
        announcer.announce(.canvasSaveConflict)
        XCTAssertEqual(posted.count, 1, "pending navigation dropped, error posted")
        XCTAssertEqual(posted.first?.priority, .high)
        XCTAssertEqual(
            posted.first?.text,
            "The canvas changed on disk. Reload it to continue — your action was "
                + "not applied.")
        announcer.flushForTests()
        XCTAssertEqual(posted.count, 1, "stale navigation never resurfaces")
    }

    /// The canvas table hands the shared data grid's OWN events to the
    /// funnel. Both the text and the priority must come from the
    /// render: unwrapping the text and re-wrapping it as a status
    /// silently demoted every assertive grid event to polite.
    func testRelayCarriesTheCorePriorityOfANonCanvasEvent() {
        let announcer = makeAnnouncer(verbosity: .standard)
        announcer.relay(.gridSorted(column: "Status", ascending: true))
        announcer.relay(.commandPaletteNeedsVault)
        XCTAssertEqual(posted.count, 2)
        XCTAssertEqual(posted[0].priority, .medium)
        XCTAssertEqual(posted[1].text, "Open a vault to use the command palette.")
        XCTAssertEqual(posted[1].priority, .high, "core's High must survive the relay")
    }

    /// DoD §H guard: no canvas source announces on its own — everything
    /// routes through the announcer. Source-scan lint (the announcer
    /// itself is the single allowed caller).
    ///
    /// WIDENED for W6-1 PR 0a: `postMutationAnnouncement` is scanned
    /// too. It lives in `AppState.swift`, so five canvas admission
    /// sites used to bypass this guard entirely while posting real
    /// announcements (contracts doc 0a-12). They construct
    /// `CanvasMutationRefused` events now; the guard makes the hole
    /// impossible to reopen.
    ///
    /// `AppKitAnnouncementPoster` is scanned for the same reason (round
    /// 1, M2): the announcer's default `post` closure had to move onto
    /// the poster layer to satisfy the residue census's
    /// string-primitive rule, which handed every other canvas file a
    /// bypass no test named — `AppKitAnnouncementPoster().post("prose",
    /// priority: .high)` would trip neither this guard's old list, nor
    /// the residue census (no `.hostComposed(`), nor the
    /// string-primitive test (a different symbol). Zero such call sites
    /// exist; this keeps it that way. `CanvasAnnouncer.swift` stays the
    /// one exempted file — it is the single legal posting seam — which
    /// is the same exemption the two names above already rely on.
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
            // Comment-only lines dropped, like the residue census does,
            // so prose NAMING the bypasses cannot trip the guard.
            let source = try String(contentsOf: file, encoding: .utf8)
                .split(separator: "\n", omittingEmptySubsequences: false)
                .filter { !$0.trimmingCharacters(in: .whitespaces).hasPrefix("//") }
                .joined(separator: "\n")
            for bypass in [
                "postAccessibilityAnnouncement", "postMutationAnnouncement",
                "AppKitAnnouncementPoster",
            ] {
                if source.contains(bypass) {
                    offenders.append("\(file.lastPathComponent): \(bypass)")
                }
            }
        }
        XCTAssertEqual(
            offenders, [],
            "canvas code must announce through CanvasAnnouncer (DoD §H), not directly")
    }
}
