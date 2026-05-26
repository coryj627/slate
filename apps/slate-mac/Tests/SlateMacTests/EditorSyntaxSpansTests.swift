// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Unit tests for `findEditorSyntaxSpans` (#296). One test per token
/// class plus a handful of interaction tests for the priority rules.
final class EditorSyntaxSpansTests: XCTestCase {

    // MARK: - Helpers

    private func spans(_ text: String, kind: SyntaxKind? = nil) -> [EditorSyntaxSpan] {
        let all = findEditorSyntaxSpans(in: text)
        if let kind { return all.filter { $0.kind == kind } }
        return all
    }

    private func substring(_ text: String, range: NSRange) -> String {
        (text as NSString).substring(with: range)
    }

    // MARK: - Frontmatter

    func testFrontmatterMatchesOpenContentAndClose() {
        let text = """
            ---
            tags: goal
            ---

            body
            """
        let frontmatter = spans(text, kind: .frontmatter)
        XCTAssertEqual(frontmatter.count, 1)
        XCTAssertEqual(substring(text, range: frontmatter[0].range), "---\ntags: goal\n---")
    }

    func testFrontmatterRequiresStartOfBuffer() {
        let text = "intro line\n---\nnot frontmatter\n---\n"
        XCTAssertTrue(spans(text, kind: .frontmatter).isEmpty)
    }

    // MARK: - Headings

    func testATXHeadingMatchesEachLevel() {
        let text = "# H1\n## H2\n### H3\n#### H4\n##### H5\n###### H6\n####### too deep"
        let headings = spans(text, kind: .heading)
        XCTAssertEqual(headings.count, 6, "ATX maxes out at six `#`")
        XCTAssertEqual(substring(text, range: headings[0].range), "# H1")
        XCTAssertEqual(substring(text, range: headings[5].range), "###### H6")
    }

    func testHeadingRequiresSpaceAfterHashes() {
        let text = "##NotHeading\n## yes heading"
        let headings = spans(text, kind: .heading)
        XCTAssertEqual(headings.count, 1)
        XCTAssertEqual(substring(text, range: headings[0].range), "## yes heading")
    }

    // MARK: - Setext underlines

    func testSetextUnderlineMatchesEqualsAndDashes() {
        let text = "Hello\n=====\n\nWorld\n-----\n"
        let underlines = spans(text, kind: .setextUnderline)
        XCTAssertEqual(underlines.count, 2)
        let texts = underlines.map { substring(text, range: $0.range).trimmingCharacters(in: .whitespacesAndNewlines) }
        XCTAssertEqual(texts.sorted(), ["-----", "====="].sorted())
    }

    // MARK: - Code blocks

    func testFencedCodeBlockSpansOpenContentAndClose() {
        let text = """
            before
            ```swift
            let x = 1
            ```
            after
            """
        let code = spans(text, kind: .codeBlock)
        XCTAssertEqual(code.count, 1)
        let body = substring(text, range: code[0].range)
        XCTAssertTrue(body.hasPrefix("```swift"))
        XCTAssertTrue(body.hasSuffix("```"))
        XCTAssertTrue(body.contains("let x = 1"))
    }

    func testFencedCodeBlockMasksInnerMarkdown() {
        // Inside the fence we have `**bold**` and `[[link]]` —
        // neither should be classified as emphasis/wikilink because
        // the code-block pass claims the range first.
        let text = """
            ```
            **not bold** and [[not a link]]
            ```
            """
        XCTAssertTrue(spans(text, kind: .emphasisMarker).isEmpty,
                      "code-block-masked emphasis must not appear")
        XCTAssertTrue(spans(text, kind: .wikilink).isEmpty,
                      "code-block-masked wikilink must not appear")
    }

    func testUnclosedFenceExtendsToEndOfBuffer() {
        let text = "```\nopen forever\nno close"
        let code = spans(text, kind: .codeBlock)
        XCTAssertEqual(code.count, 1)
        XCTAssertEqual(code[0].range.length, (text as NSString).length)
    }

    // MARK: - Inline code

    func testInlineCodeSpansBackticks() {
        let text = "use `foo()` and `bar` in text"
        let inline = spans(text, kind: .inlineCode)
        XCTAssertEqual(inline.count, 2)
        XCTAssertEqual(substring(text, range: inline[0].range), "`foo()`")
        XCTAssertEqual(substring(text, range: inline[1].range), "`bar`")
    }

    // MARK: - Wikilinks

    func testWikilinkMatchesBareAndEmbed() {
        let text = "see [[Goals]] and embed ![[Dashboard]] here"
        let links = spans(text, kind: .wikilink)
        XCTAssertEqual(links.count, 2)
        XCTAssertEqual(substring(text, range: links[0].range), "[[Goals]]")
        XCTAssertEqual(
            substring(text, range: links[1].range), "![[Dashboard]]",
            "embed `!` is included in the wikilink span"
        )
    }

    // MARK: - Tags

    func testTagMatchesInlineNotHeading() {
        let text = "## heading\nbody with #tag and #nested/tag-here"
        let tags = spans(text, kind: .tag)
        XCTAssertEqual(tags.count, 2)
        XCTAssertEqual(substring(text, range: tags[0].range), "#tag")
        XCTAssertEqual(substring(text, range: tags[1].range), "#nested/tag-here")
    }

    // MARK: - Comment blocks

    func testCommentBlockMatchesInlineAndMultiline() {
        let text = """
            before %% inline comment %% middle
            %%
            multi
            line
            %%
            after
            """
        let comments = spans(text, kind: .commentBlock)
        XCTAssertEqual(comments.count, 2)
        XCTAssertEqual(substring(text, range: comments[0].range), "%% inline comment %%")
        XCTAssertTrue(substring(text, range: comments[1].range).hasPrefix("%%"))
        XCTAssertTrue(substring(text, range: comments[1].range).hasSuffix("%%"))
    }

    // MARK: - Citations

    func testCitationBracketAndBareForms() {
        let text = "per [@smith2020] and also @jones2019 here"
        let cites = spans(text, kind: .citation)
        XCTAssertEqual(cites.count, 2)
        let strings = Set(cites.map { substring(text, range: $0.range) })
        XCTAssertEqual(strings, ["[@smith2020]", "@jones2019"])
    }

    func testCitationDoesNotMatchEmailAddress() {
        let text = "ping me at user@example.com"
        XCTAssertTrue(spans(text, kind: .citation).isEmpty)
    }

    // MARK: - Emphasis markers

    func testEmphasisMarkerSpansOnlyMarkersNotProse() {
        let text = "this is **bold** and *italic* and ~~strike~~"
        let markers = spans(text, kind: .emphasisMarker)
        // Expect: opening + closing for each of bold/italic/strike = 6
        XCTAssertEqual(markers.count, 6)
        // The prose between markers must NOT be classified — sanity:
        // no marker span covers "bold" / "italic" / "strike" text.
        for marker in markers {
            let s = substring(text, range: marker.range)
            XCTAssertFalse(s.contains("bold"), "marker span must not engulf the wrapped word")
            XCTAssertFalse(s.contains("italic"))
            XCTAssertFalse(s.contains("strike"))
        }
    }

    func testItalicMarkerDoesNotMatchSnakeCaseOrMath() {
        let text = "snake_case_var and 2*3 and not_emphasis"
        XCTAssertTrue(
            spans(text, kind: .emphasisMarker).isEmpty,
            "single `_`/`*` inside identifiers / math must not match"
        )
    }

    // MARK: - Priority / interaction

    func testCommentBlockMasksWikilinkInside() {
        // `[[link]]` inside `%% … %%` must not be classified as a
        // wikilink — the comment-block pass runs first and claims it.
        let text = "%% see [[secret]] for context %%"
        XCTAssertTrue(spans(text, kind: .wikilink).isEmpty)
        XCTAssertEqual(spans(text, kind: .commentBlock).count, 1)
    }

    func testFrontmatterMasksHashTagInside() {
        let text = "---\ntags: #project\n---\n"
        XCTAssertTrue(spans(text, kind: .tag).isEmpty,
                      "frontmatter content masks inline-tag pass")
        XCTAssertEqual(spans(text, kind: .frontmatter).count, 1)
    }

    // MARK: - Performance smoke test

    func testLargeBufferUnderFrameBudget() {
        // 1000 lines, ~50 chars each → ~50k chars. Issue #296 sets
        // a <1ms budget on 100k; this smoke test just confirms we
        // don't blow up to seconds (CI machines vary; absolute
        // timings are noisy). Acceptance: complete inside 50ms,
        // which is 5× the 100k budget at half the size — generous.
        var body = ""
        for i in 0..<1000 {
            body += "## Section \(i)\n\nsome **bold** and `code` and [[link\(i)]]\n\n"
        }
        let start = Date()
        _ = findEditorSyntaxSpans(in: body)
        let elapsed = Date().timeIntervalSince(start)
        XCTAssertLessThan(elapsed, 0.05, "1000-line buffer must classify in <50ms; got \(elapsed)s")
    }
}
