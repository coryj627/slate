// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #420 (VO test F-B2): outline rows must SPEAK their level. The
/// trait-only design (`.accessibilityHeading` + `.isHeader`) is not
/// voiced for Button rows on macOS — the level must be in the label
/// itself, in the documented "Level N heading: <text>" phrasing.
final class OutlineSidebarTests: XCTestCase {
    func testRowLabelCarriesDocumentedLevelPhrasing() {
        let heading = Heading(
            level: 3,
            text: "Second H3 with siblings",
            ordinal: 4,
            anchorId: "second-h3-with-siblings",
            byteOffset: 240
        )
        XCTAssertEqual(
            OutlineSidebar.rowAccessibilityLabel(for: heading),
            "Level 3 heading: Second H3 with siblings"
        )
    }

    func testRowLabelForTopLevelHeading() {
        let heading = Heading(level: 1, text: "Title", ordinal: 0, anchorId: "title", byteOffset: 0)
        XCTAssertEqual(
            OutlineSidebar.rowAccessibilityLabel(for: heading),
            "Level 1 heading: Title"
        )
    }
}
