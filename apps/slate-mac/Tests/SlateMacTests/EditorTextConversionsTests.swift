// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Tests for `EditorTextConversions` (#378) — the rope-backed
/// offset/line conversions that replaced the editor's three O(n) Swift
/// walks. These exercise the full Swift → FFI → `slate-core` rope path
/// against multibyte / astral / multiline text, the exact surface the
/// hand-rolled walks got subtly wrong.
final class EditorTextConversionsTests: XCTestCase {

    /// "a\n中\nx" — three lines; 中 is 3 UTF-8 bytes / 1 UTF-16 unit, so
    /// UTF-16 offsets are a=0, \n=1, 中=2, \n=3, x=4.
    private let multiline = "a\n中\nx"

    func testLineForUTF16Offset() {
        XCTAssertEqual(EditorTextConversions.lineForUTF16Offset(0, in: multiline), 1)  // a
        XCTAssertEqual(EditorTextConversions.lineForUTF16Offset(2, in: multiline), 2)  // 中
        XCTAssertEqual(EditorTextConversions.lineForUTF16Offset(4, in: multiline), 3)  // x
    }

    func testUtf16LocationForLine() {
        XCTAssertEqual(EditorTextConversions.utf16LocationForLine(1, in: multiline), 0)
        XCTAssertEqual(EditorTextConversions.utf16LocationForLine(2, in: multiline), 2)
        XCTAssertEqual(EditorTextConversions.utf16LocationForLine(3, in: multiline), 4)
        // A line past EOF parks at the buffer end (NSString length 5).
        XCTAssertEqual(
            EditorTextConversions.utf16LocationForLine(99, in: multiline),
            (multiline as NSString).length
        )
    }

    func testUtf16LocationForByteOffset() {
        // "x😀y": 😀 is 4 UTF-8 bytes / 2 UTF-16 units. Byte offsets:
        // x=0, 😀=1..5, y=5.
        let text = "x😀y"
        XCTAssertEqual(EditorTextConversions.utf16LocationForByteOffset(0, in: text), 0)  // x
        XCTAssertEqual(EditorTextConversions.utf16LocationForByteOffset(1, in: text), 1)  // 😀 start
        XCTAssertEqual(EditorTextConversions.utf16LocationForByteOffset(5, in: text), 3)  // y (after 😀's 2 cu)
        XCTAssertEqual(EditorTextConversions.utf16LocationForByteOffset(6, in: text), 4)  // end
    }

    func testAsciiOffsetsCoincide() {
        let text = "hello world"
        for off in [0, 5, 6, 11] {
            XCTAssertEqual(EditorTextConversions.utf16LocationForByteOffset(off, in: text), off)
        }
    }

    func testClampingNegativeAndOverflowInputs() {
        let text = "abc"
        XCTAssertEqual(EditorTextConversions.utf16LocationForByteOffset(-5, in: text), 0)
        XCTAssertEqual(EditorTextConversions.utf16LocationForByteOffset(999, in: text), 3)
        XCTAssertEqual(EditorTextConversions.lineForUTF16Offset(-1, in: text), 1)
        XCTAssertEqual(EditorTextConversions.utf16LocationForLine(0, in: text), 0)  // < 1 → line 1
    }

    func testEmptyText() {
        XCTAssertEqual(EditorTextConversions.lineForUTF16Offset(0, in: ""), 1)
        XCTAssertEqual(EditorTextConversions.utf16LocationForLine(1, in: ""), 0)
        XCTAssertEqual(EditorTextConversions.utf16LocationForByteOffset(0, in: ""), 0)
    }
}
