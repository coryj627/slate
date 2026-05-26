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
}
