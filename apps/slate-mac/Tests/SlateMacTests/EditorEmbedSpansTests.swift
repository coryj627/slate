import XCTest

@testable import SlateMac

/// Tests for the editor's embed-span finder + cursor-containment
/// lookup. Pure-logic tests; the actual NSTextView integration
/// (highlighting application, Cmd+E intercept) is exercised by
/// `EditorEmbedSpansIntegrationTests` against a live coordinator.
final class EditorEmbedSpansTests: XCTestCase {

    // MARK: - findEditorEmbedSpans

    func testNoEmbedsReturnsEmpty() {
        XCTAssertTrue(findEditorEmbedSpans(in: "plain text no embeds").isEmpty)
    }

    func testSingleWikilinkEmbedIsFound() {
        let text = "before ![[target]] after"
        let spans = findEditorEmbedSpans(in: text)
        XCTAssertEqual(spans.count, 1)
        XCTAssertEqual(spans[0].target, "target")
        // Span covers `![[target]]` = 11 chars starting at offset 7.
        XCTAssertEqual(spans[0].range.location, 7)
        XCTAssertEqual(spans[0].range.length, 11)
    }

    func testMultipleEmbedsAreFoundInOrder() {
        let text = "![[one]] middle ![[two]] end"
        let spans = findEditorEmbedSpans(in: text)
        XCTAssertEqual(spans.map(\.target), ["one", "two"])
    }

    func testEmbedWithHeadingAnchorPreservesTarget() {
        let text = "see ![[note#Section A]] inline"
        let spans = findEditorEmbedSpans(in: text)
        XCTAssertEqual(spans.count, 1)
        XCTAssertEqual(spans[0].target, "note#Section A")
    }

    func testEmbedWithBlockAnchorPreservesTarget() {
        let text = "see ![[note^my-block]] inline"
        let spans = findEditorEmbedSpans(in: text)
        XCTAssertEqual(spans.count, 1)
        XCTAssertEqual(spans[0].target, "note^my-block")
    }

    func testUnclosedEmbedDoesNotMatch() {
        // `![[target` on its own — no closing `]]` — should not
        // produce a span. The regex bails on the newline.
        let text = "see ![[unclosed\nnext line"
        XCTAssertTrue(findEditorEmbedSpans(in: text).isEmpty)
    }

    func testEmbedAcrossNewlineDoesNotMatch() {
        // The regex's character class excludes newlines, so a
        // `![[target\n]]` shape doesn't match.
        let text = "![[target\n]]"
        XCTAssertTrue(findEditorEmbedSpans(in: text).isEmpty)
    }

    func testWhitespaceOnlyTargetIsDropped() {
        let text = "![[   ]]"
        XCTAssertTrue(findEditorEmbedSpans(in: text).isEmpty)
    }

    func testPlainWikilinkWithoutBangIsNotAnEmbed() {
        // Wikilinks without the leading `!` are navigation, not
        // embeds. The span finder must NOT match them.
        let text = "see [[target]] for context"
        XCTAssertTrue(findEditorEmbedSpans(in: text).isEmpty)
    }

    // MARK: - embedSpanContaining

    func testCursorAtStartOfEmbedReturnsSpan() {
        let spans = findEditorEmbedSpans(in: "x ![[a]] y")
        // span starts at offset 2.
        let found = embedSpanContaining(cursor: 2, in: spans)
        XCTAssertEqual(found?.target, "a")
    }

    func testCursorInMiddleOfEmbedReturnsSpan() {
        let spans = findEditorEmbedSpans(in: "x ![[abc]] y")
        // span is `![[abc]]` at offset 2, length 8 → range 2..<10.
        let found = embedSpanContaining(cursor: 5, in: spans)
        XCTAssertEqual(found?.target, "abc")
    }

    func testCursorAtRightEdgeOfEmbedReturnsSpan() {
        let spans = findEditorEmbedSpans(in: "x ![[abc]] y")
        // Right edge inclusive — cursor at offset 10 is one past
        // the closing `]]` and should still resolve to the embed
        // (matches the spec's "backspace at right edge" intent).
        let found = embedSpanContaining(cursor: 10, in: spans)
        XCTAssertEqual(found?.target, "abc")
    }

    func testCursorOutsideAnyEmbedReturnsNil() {
        let spans = findEditorEmbedSpans(in: "x ![[a]] y")
        XCTAssertNil(embedSpanContaining(cursor: 0, in: spans))
        XCTAssertNil(embedSpanContaining(cursor: 9, in: spans))
        XCTAssertNil(embedSpanContaining(cursor: 100, in: spans))
    }
}
