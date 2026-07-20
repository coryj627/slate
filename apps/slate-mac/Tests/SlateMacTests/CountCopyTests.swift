// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// The Swift mirror of core's `count_noun` agreement rule: singular at
/// exactly one, plural everywhere else INCLUDING zero. Zero-is-plural
/// is the load-bearing half — it keeps Swift copy matching the core
/// summaries ("No results." / "0 tags.") rather than drifting to
/// "0 tag".
final class CountCopyTests: XCTestCase {
    func testSingularOnlyAtExactlyOne() {
        XCTAssertEqual(CountCopy.counted(1, "card", "cards"), "1 card")
        XCTAssertEqual(CountCopy.counted(2, "card", "cards"), "2 cards")
        XCTAssertEqual(CountCopy.counted(0, "card", "cards"), "0 cards")
    }

    func testBareNounAndVerbAgreement() {
        XCTAssertEqual(CountCopy.noun(1, "file", "files"), "file")
        XCTAssertEqual(CountCopy.noun(0, "file", "files"), "files")
        XCTAssertEqual(CountCopy.verb(1, "is", "are"), "is")
        XCTAssertEqual(CountCopy.verb(3, "is", "are"), "are")
    }

    /// Counts cross the FFI as unsigned; the helper is generic so call
    /// sites never cast, because a cast is where the next miss hides.
    func testAcceptsUnsignedFFICounts() {
        let total: UInt64 = 1
        let many: UInt32 = 7
        XCTAssertEqual(CountCopy.counted(total, "result", "results"), "1 result")
        XCTAssertEqual(CountCopy.counted(many, "result", "results"), "7 results")
    }

    /// The composed shape used by the "X of Y" summaries: the noun
    /// agrees with the TOTAL, the verb with the shown count.
    func testOfTotalTemplateAgreesNounWithTotalAndVerbWithSubject() {
        func summary(_ shown: Int, _ total: Int) -> String {
            "\(shown) of \(CountCopy.counted(total, "card", "cards")) "
                + CountCopy.verb(shown, "matches", "match")
        }
        XCTAssertEqual(summary(1, 1), "1 of 1 card matches")
        XCTAssertEqual(summary(1, 5), "1 of 5 cards matches")
        XCTAssertEqual(summary(3, 5), "3 of 5 cards match")
    }
}
