// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Tests for the Citation Summary helpers introduced in #282.
final class CitationSummaryTests: XCTestCase {

    // MARK: - extractCitationKey

    func testExtractCitationKeyHandlesBracketed() {
        XCTAssertEqual(extractCitationKey(from: "[@smith2020]"), "smith2020")
        XCTAssertEqual(extractCitationKey(from: "[@smith2020, p. 23]"), "smith2020")
        XCTAssertEqual(extractCitationKey(from: "[-@smith2020]"), "smith2020")
        XCTAssertEqual(extractCitationKey(from: "[see @smith2020]"), "smith2020")
    }

    func testExtractCitationKeyHandlesInText() {
        XCTAssertEqual(extractCitationKey(from: "@smith2020"), "smith2020")
        XCTAssertEqual(extractCitationKey(from: "@jones-etal-2019:abc.v2+rev"), "jones-etal-2019:abc.v2+rev")
    }

    func testExtractCitationKeyStripsTrailingDot() {
        // Sentence-ending dot doesn't belong in the key.
        XCTAssertEqual(extractCitationKey(from: "@smith2020."), "smith2020")
    }

    func testExtractCitationKeyEmptyWhenNoAt() {
        XCTAssertEqual(extractCitationKey(from: "just plain text"), "")
    }

    // Verify the unresolved-source counting collapses different
    // locator forms of the same key into one unique source —
    // Codoki PR #293.
    func testSameKeyWithDifferentLocatorsCollapsesToOneUniqueSource() {
        let a = extractCitationKey(from: "[@smith2020]")
        let b = extractCitationKey(from: "[@smith2020, p. 2]")
        let c = extractCitationKey(from: "[@smith2020, pp. 5-10]")
        XCTAssertEqual(a, b)
        XCTAssertEqual(b, c)
        let unique = Set([a, b, c])
        XCTAssertEqual(unique.count, 1)
    }
}
