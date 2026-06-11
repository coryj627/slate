// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #423 (VO test F-G1): `[/]` and `[-]` must not both read as
/// "Open task" — cancelled-as-open actively misleads.
final class TaskStatusPhraseTests: XCTestCase {
    private func task(statusChar: String, completed: Bool) -> TaskItem {
        TaskItem(
            ordinal: 0,
            text: "t",
            statusChar: statusChar,
            completed: completed,
            dueMs: nil,
            scheduledMs: nil,
            priority: nil,
            recurrence: nil,
            line: 1,
            byteOffset: 0
        )
    }

    func testInProgressAndCancelledAreDistinct() {
        XCTAssertEqual(task(statusChar: "/", completed: false).statusPhrase, "In-progress task.")
        XCTAssertEqual(task(statusChar: "-", completed: false).statusPhrase, "Cancelled task.")
        XCTAssertEqual(task(statusChar: "/", completed: false).statusWord, "In progress")
        XCTAssertEqual(task(statusChar: "-", completed: false).statusWord, "Cancelled")
    }

    func testStandardCharsKeepBinaryPhrasing() {
        XCTAssertEqual(task(statusChar: " ", completed: false).statusPhrase, "Open task.")
        XCTAssertEqual(task(statusChar: "x", completed: true).statusPhrase, "Done task.")
        XCTAssertEqual(task(statusChar: " ", completed: false).statusWord, "Open")
        XCTAssertEqual(task(statusChar: "x", completed: true).statusWord, "Done")
    }

    func testUnknownCharsStayConservative() {
        XCTAssertEqual(task(statusChar: "?", completed: false).statusPhrase, "Open task.")
        XCTAssertEqual(task(statusChar: "?", completed: true).statusPhrase, "Done task.")
    }
}
