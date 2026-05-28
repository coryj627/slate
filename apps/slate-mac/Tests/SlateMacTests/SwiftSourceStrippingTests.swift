// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Self-tests for `SwiftSourceStripping` (#333). The stripper backs
/// structural source-scraping tests (the Settings-scene check today,
/// and the brace-counter in #343 if it adopts it). A regression in
/// the state machine would silently re-open the false-pass class
/// the helper exists to close, so these pin every branch.
final class SwiftSourceStrippingTests: XCTestCase {

    private func strip(_ s: String) -> String {
        SwiftSourceStripping.strippingCommentsAndStrings(s)
    }

    // MARK: - The motivating false-pass cases (#333)

    func testLineCommentContentIsBlanked() {
        // The exact false-pass from the issue: a commented-out
        // mention of the scene must NOT survive stripping.
        let src = "// TODO: bring back the Settings { } scene later\n"
        let out = strip(src)
        XCTAssertFalse(out.contains("Settings"), "comment text must be blanked")
        XCTAssertFalse(out.contains("{"), "brace inside comment must be blanked")
        // Newline preserved so line structure is intact.
        XCTAssertTrue(out.hasSuffix("\n"))
    }

    func testStringLiteralContentIsBlanked() {
        let src = #"let label = "Settings { ... }""#
        let out = strip(src)
        XCTAssertFalse(out.contains("Settings"), "string content must be blanked")
        XCTAssertTrue(out.contains("let label ="), "code outside the string survives")
    }

    func testRealSceneDeclarationSurvives() {
        // The true-positive: actual scene code must be untouched so
        // the grep still finds it.
        let src = """
            var body: some Scene {
                Settings {
                    PreferencesView()
                }
            }
            """
        let out = strip(src)
        XCTAssertTrue(out.contains("Settings {"), "real scene declaration must survive")
    }

    // MARK: - Block comments + nesting

    func testBlockCommentIsBlanked() {
        let src = "before /* Settings { } */ after"
        let out = strip(src)
        // Structural assertions rather than hand-counting spaces:
        // the real code survives, the comment content is gone, and
        // length is preserved.
        XCTAssertTrue(out.hasPrefix("before "))
        XCTAssertTrue(out.hasSuffix(" after"))
        XCTAssertFalse(out.contains("Settings"))
        XCTAssertFalse(out.contains("{"))
        XCTAssertEqual(out.count, src.count)
    }

    func testNestedBlockCommentFullyConsumed() {
        // Swift block comments nest — a naive `/* … */` scan that
        // stops at the FIRST `*/` would leave `tail` exposed and
        // `Settings {` would survive. The depth counter handles it.
        let src = "head /* outer /* inner */ still comment */ Settings { tail"
        let out = strip(src)
        XCTAssertTrue(out.hasPrefix("head "))
        XCTAssertTrue(
            out.contains("Settings { tail"),
            "code after the FULLY-closed nested comment must survive"
        )
        // The comment body (including the inner `Settings`-free text)
        // is blanked; only the trailing real code remains.
        XCTAssertFalse(
            out.contains("outer"),
            "nested comment content must be blanked"
        )
    }

    func testUnterminatedBlockCommentBlanksToEOF() {
        // Defensive: a malformed unterminated block comment shouldn't
        // crash; it blanks to end of input.
        let src = "code /* never closed Settings {"
        let out = strip(src)
        XCTAssertTrue(out.hasPrefix("code "))
        XCTAssertFalse(out.contains("Settings"))
    }

    // MARK: - String escape handling

    func testEscapedQuoteDoesNotCloseStringEarly() {
        // `"a \" Settings { b"` is ONE string. A scan that closed at
        // the escaped quote would treat ` Settings { b` as code and
        // false-pass. The `\"` escape keeps the string open.
        let src = #"x = "a \" Settings { b" ; y"#
        let out = strip(src)
        XCTAssertFalse(out.contains("Settings"), "escaped-quote string content must stay blanked")
        XCTAssertTrue(out.contains("x ="))
        XCTAssertTrue(out.contains("; y") || out.contains(";  y"), "code after the string survives")
    }

    func testEscapedBackslashBeforeQuoteClosesString() {
        // `"path\\"` — the `\\` is an escaped backslash, so the
        // following `"` DOES close the string. Then `code` is real.
        let src = #"p = "path\\" ; Settings {"#
        let out = strip(src)
        XCTAssertTrue(
            out.contains("Settings {"),
            "after a string ending in an escaped backslash, following code must survive"
        )
    }

    // MARK: - Length + structure preservation

    func testOutputLengthMatchesInput() {
        // Offsets must be stable so a line-number-reporting caller
        // sees identical positions.
        let src = """
            line one // comment
            "a string"
            real code {
            """
        let out = strip(src)
        XCTAssertEqual(out.count, src.count, "stripper must preserve total length")
    }

    func testNewlinesPreservedAcrossAllStates() {
        let src = "a // c1\n/* b1\nb2 */\n\"s1\ns2\"\nz"
        let out = strip(src)
        XCTAssertEqual(
            out.filter { $0 == "\n" }.count,
            src.filter { $0 == "\n" }.count,
            "newline count must be identical in every state"
        )
    }

    // MARK: - No-op on clean code

    func testPlainCodeUnchanged() {
        let src = "let x = 42\nstruct Foo { let y: Int }\n"
        XCTAssertEqual(strip(src), src, "code with no comments/strings is returned verbatim")
    }

    func testEmptyInput() {
        XCTAssertEqual(strip(""), "")
    }

    func testTrailingSlashAtEOFIsNotAComment() {
        // A lone `/` at end of input must not be misread as the
        // start of a comment (no next char to confirm `//` or `/*`).
        let src = "a /"
        XCTAssertEqual(strip(src), "a /")
    }
}
