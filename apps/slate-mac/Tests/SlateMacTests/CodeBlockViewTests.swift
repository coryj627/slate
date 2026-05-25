import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// Tests for `CodeBlockView` (#221). Focus on the AT-facing contract
/// and the attribute layer — both are testable without a UI harness.
/// The visual rendering details (scroll behavior, font metrics) are
/// validated by manual inspection during dev; this suite locks the
/// behaviours that would silently regress.
@MainActor
final class CodeBlockViewTests: XCTestCase {

    private func makeBlock(
        source: String,
        language: String? = nil,
        tokens: [SyntaxToken] = []
    ) -> CodeBlock {
        CodeBlock(
            source: source,
            language: language,
            tokens: tokens,
            semanticSpans: [],
            line: 1,
            byteOffset: 0
        )
    }

    // MARK: - AT preamble

    /// Headline contract: VoiceOver hears `"Code block, <language>,
    /// N lines"` before drilling in. Language is the first
    /// distinguishing word; line count tells the user whether to
    /// dive in or move on.
    func testPreambleIncludesLanguageAndLineCount() {
        let block = makeBlock(
            source: "fn one() {}\nfn two() {}\nfn three() {}\n",
            language: "rust"
        )
        let view = CodeBlockView(block: block)
        XCTAssertEqual(view.preambleLabel, "Code block, rust, 3 lines.")
    }

    func testPreambleSingleLineUsesSingular() {
        let block = makeBlock(source: "println!(\"hi\")", language: "rust")
        let view = CodeBlockView(block: block)
        XCTAssertEqual(view.preambleLabel, "Code block, rust, 1 line.")
    }

    /// Missing language tag → "plain text" so the AT label is always
    /// well-formed. Indented blocks and bare fences both go here.
    func testPreambleMissingLanguageReadsPlainText() {
        let block = makeBlock(source: "raw\nstuff\n", language: nil)
        let view = CodeBlockView(block: block)
        XCTAssertEqual(view.preambleLabel, "Code block, plain text, 2 lines.")
    }

    func testPreambleEmptyLanguageReadsPlainText() {
        let block = makeBlock(source: "raw\n", language: "")
        let view = CodeBlockView(block: block)
        XCTAssertEqual(view.preambleLabel, "Code block, plain text, 1 line.")
    }

    func testPreambleEmptySourceReadsZeroLines() {
        let block = makeBlock(source: "", language: "rust")
        let view = CodeBlockView(block: block)
        XCTAssertEqual(view.preambleLabel, "Code block, rust, 0 lines.")
    }

    func testPreambleSourceWithoutTrailingNewlineCountsLast() {
        let block = makeBlock(source: "a\nb\nc", language: "rust")
        let view = CodeBlockView(block: block)
        XCTAssertEqual(view.preambleLabel, "Code block, rust, 3 lines.")
    }

    // MARK: - Attributed string

    /// Every token in the input is materialized in the attributed
    /// string. Drops on this surface would mean missing highlighting
    /// for entire ranges — visually broken even if AT works.
    func testAttributedStringAppliesForegroundColorForEachToken() {
        let source = "fn foo() {}"
        let tokens: [SyntaxToken] = [
            SyntaxToken(startByte: 0, endByte: 2, kind: .keyword),     // "fn"
            SyntaxToken(startByte: 3, endByte: 6, kind: .function),    // "foo"
            SyntaxToken(startByte: 6, endByte: 7, kind: .punctuation), // "("
            SyntaxToken(startByte: 7, endByte: 8, kind: .punctuation), // ")"
        ]
        let block = makeBlock(source: source, language: "rust", tokens: tokens)
        let attributed = attributedString(for: block)

        XCTAssertEqual(attributed.string, source)

        let keywordColor = attributed.attribute(
            .foregroundColor,
            at: 0, // "f" of "fn"
            effectiveRange: nil
        ) as? NSColor
        XCTAssertEqual(keywordColor, CodeTokenTheme.color(for: .keyword))

        let functionColor = attributed.attribute(
            .foregroundColor,
            at: 3, // "f" of "foo"
            effectiveRange: nil
        ) as? NSColor
        XCTAssertEqual(functionColor, CodeTokenTheme.color(for: .function))
    }

    /// Ranges uncovered by any token still render — with the default
    /// label color rather than vanishing.
    func testUntokenizedRangesGetDefaultLabelColor() {
        let block = makeBlock(
            source: "abc",
            language: "rust",
            tokens: [] // No tokens at all.
        )
        let attributed = attributedString(for: block)
        let color = attributed.attribute(
            .foregroundColor,
            at: 0,
            effectiveRange: nil
        ) as? NSColor
        XCTAssertEqual(color, NSColor.labelColor)
    }

    /// Tokens whose byte ranges fall outside the source are dropped
    /// silently rather than panicking. The backend should never emit
    /// them, but the UI must be defensive.
    func testOutOfRangeTokensAreDropped() {
        let block = makeBlock(
            source: "ab",
            language: "rust",
            tokens: [
                SyntaxToken(startByte: 0, endByte: 2, kind: .keyword),
                SyntaxToken(startByte: 99, endByte: 100, kind: .string),
            ]
        )
        // Just constructing must not panic.
        let attributed = attributedString(for: block)
        XCTAssertEqual(attributed.string, "ab")
    }

    /// UTF-8 byte ranges from tree-sitter must map correctly into
    /// the NSString's UTF-16 view even when source contains multi-
    /// byte characters.
    func testTokenRangeRoundTripsThroughMultibyteSource() {
        // "café" — c, a, f, é. UTF-8: c(0..1), a(1..2), f(2..3),
        // é(3..5). UTF-16: c(0..1), a(1..2), f(2..3), é(3..4).
        let source = "café"
        let block = makeBlock(
            source: source,
            language: "rust",
            tokens: [
                SyntaxToken(startByte: 3, endByte: 5, kind: .string), // "é"
            ]
        )
        let attributed = attributedString(for: block)
        var effectiveRange = NSRange()
        let color = attributed.attribute(
            .foregroundColor,
            at: 3, // utf16 position of é
            effectiveRange: &effectiveRange
        ) as? NSColor
        XCTAssertEqual(color, CodeTokenTheme.color(for: .string))
        XCTAssertEqual(effectiveRange.length, 1)
    }
}
