// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Tests for the citations UI helpers introduced in #279 — the
/// placeholder rendering used when no CSL style is configured yet
/// (the broader pipeline integration is exercised by Milestone L's
/// integration tests in #283).
final class CitationsPanelTests: XCTestCase {

    // MARK: - placeholderRendered

    func testPlaceholderForBracketedCitationUsesCitationPrefix() {
        let ref = CitationReference(
            raw: "[@smith2020]",
            citations: [
                CitedItem(
                    key: "smith2020",
                    locator: nil,
                    prefix: nil,
                    suffix: nil,
                    mode: .bracketed
                )
            ],
            byteOffset: 0,
            line: 1
        )
        let rendered = placeholderRendered(for: ref)
        XCTAssertEqual(rendered.speechText, "Citation: smith2020")
        XCTAssertEqual(rendered.raw, "[@smith2020]")
        XCTAssertNil(rendered.bibEntry)
        XCTAssertEqual(rendered.styleId, "")
    }

    func testPlaceholderForInTextCitationOmitsCitationPrefix() {
        let ref = CitationReference(
            raw: "@smith2020",
            citations: [
                CitedItem(
                    key: "smith2020",
                    locator: nil,
                    prefix: nil,
                    suffix: nil,
                    mode: .inText
                )
            ],
            byteOffset: 0,
            line: 1
        )
        let rendered = placeholderRendered(for: ref)
        // In-text speech drops the "Citation: " prefix per §6.5.
        XCTAssertEqual(rendered.speechText, "smith2020")
    }

    func testPlaceholderForMultiCitationJoinsKeys() {
        let ref = CitationReference(
            raw: "[@a; @b; @c]",
            citations: [
                CitedItem(
                    key: "a",
                    locator: nil,
                    prefix: nil,
                    suffix: nil,
                    mode: .bracketed
                ),
                CitedItem(
                    key: "b",
                    locator: nil,
                    prefix: nil,
                    suffix: nil,
                    mode: .bracketed
                ),
                CitedItem(
                    key: "c",
                    locator: nil,
                    prefix: nil,
                    suffix: nil,
                    mode: .bracketed
                ),
            ],
            byteOffset: 0,
            line: 1
        )
        let rendered = placeholderRendered(for: ref)
        XCTAssertEqual(rendered.speechText, "Citation: a, b, c")
    }

    // MARK: - #878: anchored popover, not a detached sheet

    /// The citation row presents its `CitationPopover` via an anchored
    /// `.popover(attachmentAnchor:arrowEdge:)` (popovers.md:21) rather than
    /// the MainSplitView sheet #878 replaced. Pinned structurally — a
    /// rendered NSPopover has no XCTest surface.
    func testCitationRowPresentsAnchoredPopover() throws {
        let source = try Self.source("Sources/SlateMac/CitationsPanel.swift")
        XCTAssertTrue(source.contains(".popover("))
        XCTAssertTrue(source.contains("attachmentAnchor: .rect(.bounds)"))
        XCTAssertTrue(source.contains("arrowEdge: .leading"))
        XCTAssertTrue(
            source.contains("appState.expandedCitationRowAnchored = true"),
            "a panel-row trigger marks the expansion row-anchored so the "
                + "MainSplitView fallback stays closed")
        XCTAssertTrue(source.contains("CitationPopover("))
    }

    /// The bibliography row also anchors its popover, keyed by the entry's
    /// unique key (#878).
    func testBibliographyRowPresentsAnchoredPopover() throws {
        let source = try Self.source("Sources/SlateMac/BibliographyPanel.swift")
        XCTAssertTrue(source.contains(".popover("))
        XCTAssertTrue(source.contains("attachmentAnchor: .rect(.bounds)"))
        XCTAssertTrue(source.contains("entryPopoverBinding(entry)"))
        XCTAssertTrue(source.contains("expandedBibEntry?.key == entry.key"))
    }

    /// MainSplitView no longer sheets the bibliography-entry expansion at
    /// all, and gates the surviving citation presentation (the anchorless
    /// Reading-mode fallback) on `!expandedCitationRowAnchored` (#878).
    func testMainSplitViewDropsBibSheetAndGatesCitationFallback() throws {
        let source = try Self.source("Sources/SlateMac/MainSplitView.swift")
        XCTAssertTrue(
            source.contains("!appState.expandedCitationRowAnchored"),
            "the detached citation presentation must be gated to the "
                + "Reading-mode (non-row-anchored) case")
        XCTAssertFalse(
            source.contains("appState.expandedBibEntry != nil"),
            "the bibliography-entry sheet must be gone — the row anchors its own popover")
    }

    /// The discriminator defaults to not-anchored (Reading-mode's expectation:
    /// an unset trigger presents the detached fallback), and Reading-mode's
    /// router explicitly sets it false (#878).
    @MainActor
    func testExpandedCitationRowAnchoredDefaultAndReadingModeIsFalse() throws {
        let appState = AppState()
        XCTAssertFalse(
            appState.expandedCitationRowAnchored,
            "no expansion in flight → not row-anchored")
        let router = try Self.source("Sources/SlateMac/Reading/ReadingLinkRouter.swift")
        XCTAssertTrue(
            router.contains("appState.expandedCitationRowAnchored = false"),
            "Reading mode's inline citation click has no anchor, so it routes "
                + "to the detached fallback")
    }

    private static func source(_ relativePath: String) throws -> String {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        for _ in 0..<8 {
            let candidate = cursor.appendingPathComponent(relativePath)
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
            cursor = cursor.deletingLastPathComponent()
        }
        throw CocoaError(.fileNoSuchFile)
    }
}
