// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Tests for the pure logic inside `EmbedView` — the byte-range
/// splicer that interleaves nested embeds into a parent's body
/// text, and the embed-depth constant. SwiftUI view-rendering
/// snapshots need a framework we don't take a dependency on yet;
/// those tests would land alongside the first true snapshot
/// dependency.
@MainActor
final class EmbedViewTests: XCTestCase {

    // MARK: - splice

    func testSpliceWithNoNestedEmbedsReturnsOriginalText() {
        let text = "Just plain text with no embeds."
        let segments = splice(text: text, nested: [])
        XCTAssertEqual(segments.count, 1)
        guard case .text(let s) = segments[0] else {
            XCTFail("Expected .text, got \(segments[0])")
            return
        }
        XCTAssertEqual(s, text)
    }

    func testSpliceWithOneNestedEmbedPartitionsAroundIt() {
        // 0-based byte layout:
        // "alpha ![[beta]] gamma"
        //  0     6         16
        let text = "alpha ![[beta]] gamma"
        let nested = NestedEmbed(
            rawTarget: "beta",
            byteOffsetInParent: 6,
            byteEndInParent: 15,
            resolution: EmbedResolution.unresolved(
                reason: .targetNotFound(target: "beta")
            )
        )
        let segments = splice(text: text, nested: [nested])
        XCTAssertEqual(segments.count, 3, "expected text + embed + text, got \(segments)")
        guard case .text(let leading) = segments[0] else { XCTFail(); return }
        XCTAssertEqual(leading, "alpha ")
        guard case .embed(let ne) = segments[1] else { XCTFail(); return }
        XCTAssertEqual(ne.rawTarget, "beta")
        guard case .text(let trailing) = segments[2] else { XCTFail(); return }
        // `![[beta]]` is exactly `5 + "beta".utf8.count` = 9 bytes
        // (`![[` + target + `]]`); after the fix in audit #199 the
        // trailing text starts cleanly after the closing `]]`.
        XCTAssertEqual(trailing, " gamma")
    }

    func testSpliceWithMultipleNestedEmbedsKeepsThemInOffsetOrder() {
        // Offsets deliberately given out of order to confirm the
        // sort in splice() works.
        let text = "A ![[one]] B ![[two]] C"
        let a = NestedEmbed(
            rawTarget: "two",
            byteOffsetInParent: 13,
            byteEndInParent: 21,
            resolution: .unresolved(reason: .targetNotFound(target: "two"))
        )
        let b = NestedEmbed(
            rawTarget: "one",
            byteOffsetInParent: 2,
            byteEndInParent: 10,
            resolution: .unresolved(reason: .targetNotFound(target: "one"))
        )
        let segments = splice(text: text, nested: [a, b])
        // Filter to .embed segments and check order.
        let embedTargets: [String] = segments.compactMap {
            if case .embed(let ne) = $0 { return ne.rawTarget }
            return nil
        }
        XCTAssertEqual(embedTargets, ["one", "two"])
    }

    func testSpliceDropsEmbedsWithOutOfRangeOffsets() {
        // An offset past the text length is treated as invalid and
        // the embed is dropped silently — defensive against a
        // future backend bug that reports a stale offset.
        let text = "short"
        let bad = NestedEmbed(
            rawTarget: "x",
            byteOffsetInParent: 100,
            byteEndInParent: 101,
            resolution: .unresolved(reason: .targetNotFound(target: "x"))
        )
        let segments = splice(text: text, nested: [bad])
        XCTAssertEqual(segments.count, 1)
        guard case .text(let s) = segments[0] else { XCTFail(); return }
        XCTAssertEqual(s, text)
    }

    // MARK: - depth constant

    /// #419 (WCAG 1.1.1): the author's alt text IS the AT
    /// description for image embeds; the filename is only a
    /// fallback (and empty/whitespace alt falls back too — audit
    /// #198 contract).
    func testImageEmbedTitleUsesAltTextWhenPresent() {
        XCTAssertEqual(
            EmbedView.imageEmbedTitle(
                targetPath: "attachments/pie.svg",
                alt: "A simple line drawing of a pie"
            ),
            "Embedded image: A simple line drawing of a pie"
        )
    }

    func testImageEmbedTitleFallsBackToFilename() {
        XCTAssertEqual(
            EmbedView.imageEmbedTitle(targetPath: "attachments/pie.svg", alt: nil),
            "Embedded image: pie.svg"
        )
        XCTAssertEqual(
            EmbedView.imageEmbedTitle(targetPath: "a/b/cover.png", alt: "   "),
            "Embedded image: cover.png"
        )
        XCTAssertEqual(
            EmbedView.imageEmbedTitle(targetPath: "x.png", alt: ""),
            "Embedded image: x.png"
        )
    }

    func testEmbedDepthLimitMatchesBackendConstant() {
        // The Swift-side guard is defense-in-depth against a
        // misbehaving backend. Pin the constant so any future bump
        // on the Rust side is mirrored here intentionally (and the
        // test forces the consideration).
        XCTAssertEqual(EmbedView.embedDepthLimit, 3)
    }
}
