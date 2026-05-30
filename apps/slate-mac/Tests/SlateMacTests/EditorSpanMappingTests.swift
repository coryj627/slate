// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Unit tests for `EditorSpanMapping` (#376) — the UTF-8-byte →
/// UTF-16-offset conversion that bridges the canonical Rust spans
/// (`editorHighlightSpans`, byte offsets) into the UTF-16 `NSRange`
/// space `NSTextView` / `NSLayoutManager` use.
///
/// These are the cases that matter: ASCII (offsets coincide), and each
/// multi-byte width where UTF-8 and UTF-16 diverge — 2-byte (é), 3-byte
/// (中), and astral 4-byte / surrogate-pair (😀) — plus the defensive
/// drops (mid-scalar boundary, degenerate range).
final class EditorSpanMappingTests: XCTestCase {

    // MARK: - utf16Offsets

    func testAsciiByteOffsetsEqualUtf16Offsets() {
        let text = "hello world"
        let map = EditorSpanMapping.utf16Offsets(forUtf8: [0, 5, 6, 11], in: text)
        XCTAssertEqual(map[0], 0)
        XCTAssertEqual(map[5], 5)
        XCTAssertEqual(map[6], 6)
        XCTAssertEqual(map[11], 11)
    }

    func testTwoByteScalarShiftsUtf16Offset() {
        // "café": c,a,f are 1 byte each; é (U+00E9) is 2 UTF-8 bytes but
        // 1 UTF-16 unit. 5 bytes / 4 UTF-16 total.
        let text = "café"
        XCTAssertEqual(text.utf8.count, 5)
        XCTAssertEqual(text.utf16.count, 4)
        let map = EditorSpanMapping.utf16Offsets(forUtf8: [0, 3, 5], in: text)
        XCTAssertEqual(map[0], 0)
        XCTAssertEqual(map[3], 3)  // after "caf"
        XCTAssertEqual(map[5], 4)  // after "é": byte 5 → UTF-16 4
    }

    func testThreeByteScalarShiftsUtf16Offset() {
        // "a中b": 中 (U+4E2D) is 3 UTF-8 bytes, 1 UTF-16 unit.
        let text = "a中b"
        XCTAssertEqual(text.utf8.count, 5)
        XCTAssertEqual(text.utf16.count, 3)
        let map = EditorSpanMapping.utf16Offsets(forUtf8: [1, 4, 5], in: text)
        XCTAssertEqual(map[1], 1)  // after "a"
        XCTAssertEqual(map[4], 2)  // after "中": bytes 1..4 → UTF-16 1..2
        XCTAssertEqual(map[5], 3)  // after "b"
    }

    func testAstralScalarIsTwoUtf16Units() {
        // "x😀y": 😀 (U+1F600) is 4 UTF-8 bytes and a UTF-16 surrogate
        // pair (2 units).
        let text = "x😀y"
        XCTAssertEqual(text.utf8.count, 6)
        XCTAssertEqual(text.utf16.count, 4)
        let map = EditorSpanMapping.utf16Offsets(forUtf8: [1, 5, 6], in: text)
        XCTAssertEqual(map[1], 1)  // after "x"
        XCTAssertEqual(map[5], 3)  // after "😀": bytes 1..5 → UTF-16 1..3
        XCTAssertEqual(map[6], 4)  // after "y"
    }

    func testOffsetZeroOnlyMappedWhenRequested() {
        XCTAssertNil(
            EditorSpanMapping.utf16Offsets(forUtf8: [3], in: "abcdef")[0],
            "0 not requested → absent from the map"
        )
        XCTAssertEqual(EditorSpanMapping.utf16Offsets(forUtf8: [0], in: "abc")[0], 0)
    }

    func testMidScalarOffsetIsAbsent() {
        // "中x": 中 occupies bytes 0..3, so byte offsets 1 and 2 fall
        // mid-scalar and must never be recorded (the caller treats
        // "absent" as "drop this span").
        let text = "中x"
        let map = EditorSpanMapping.utf16Offsets(forUtf8: [1, 2, 3, 4], in: text)
        XCTAssertNil(map[1], "byte 1 is mid-scalar")
        XCTAssertNil(map[2], "byte 2 is mid-scalar")
        XCTAssertEqual(map[3], 1, "byte 3 ends 中 → UTF-16 1")
        XCTAssertEqual(map[4], 2, "byte 4 ends x → UTF-16 2")
    }

    // MARK: - utf16Spans

    func testConvertsByteSpanToNSRangeAcrossMultiBytePrefix() {
        // "a中 [[L]]": a(0..1) 中(1..4) space(4..5) then "[[L]]"(5..10).
        // In UTF-16 the wikilink lands at 3..8.
        let text = "a中 [[L]]"
        let mapped = EditorSpanMapping.utf16Spans(
            from: [EditorSpan(startByte: 5, endByte: 10, kind: .wikilink)],
            in: text
        )
        XCTAssertEqual(mapped.count, 1)
        XCTAssertEqual(mapped[0].range, NSRange(location: 3, length: 5))
        XCTAssertEqual(
            (text as NSString).substring(with: mapped[0].range), "[[L]]",
            "converted NSRange must slice to the original token"
        )
    }

    func testDropsDegenerateAndMidScalarSpans() {
        let text = "中x"  // 4 bytes, 2 UTF-16
        let mapped = EditorSpanMapping.utf16Spans(
            from: [
                EditorSpan(startByte: 0, endByte: 0, kind: .tag),  // empty → drop
                EditorSpan(startByte: 1, endByte: 3, kind: .tag),  // start mid-scalar → drop
                EditorSpan(startByte: 0, endByte: 3, kind: .heading(level: 1)),  // valid → 中
            ],
            in: text
        )
        XCTAssertEqual(mapped.count, 1)
        XCTAssertEqual(mapped[0].range, NSRange(location: 0, length: 1))
    }

    func testEmptyInputYieldsNoSpans() {
        XCTAssertTrue(EditorSpanMapping.utf16Spans(from: [], in: "anything").isEmpty)
    }
}
