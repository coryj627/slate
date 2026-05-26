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
}
