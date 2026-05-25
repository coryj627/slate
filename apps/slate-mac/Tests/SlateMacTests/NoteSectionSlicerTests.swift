// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Direct coverage for the ATX-heading slicer that powers the
/// per-heading anchors in `NoteContentView`. The scanner uses
/// pulldown_cmark on the Rust side, so the Swift parser must agree
/// with CommonMark §4.2 for the canonical edge cases (trailing
/// closers, extra spacing, leading-space indent) or else
/// `sliceIntoSections` falls back to a single un-anchored section
/// and `OutlineSidebar` clicks scroll nowhere.
final class NoteSectionSlicerTests: XCTestCase {

    private func h(_ level: UInt8, _ text: String, ordinal: UInt32, anchorId: String? = nil) -> Heading {
        Heading(
            level: level,
            text: text,
            ordinal: ordinal,
            anchorId: anchorId ?? text.lowercased().replacingOccurrences(of: " ", with: "-")
        )
    }

    // MARK: - parseAtxHeading

    func testParseAtxBasic() {
        XCTAssertEqual(parseAtxHeading("# Hello")?.level, 1)
        XCTAssertEqual(parseAtxHeading("# Hello")?.text, "Hello")

        XCTAssertEqual(parseAtxHeading("###### deep")?.level, 6)
        XCTAssertEqual(parseAtxHeading("###### deep")?.text, "deep")
    }

    func testParseAtxRejectsSevenOrMoreHashes() {
        XCTAssertNil(parseAtxHeading("####### too-deep"))
    }

    func testParseAtxRequiresSpaceAfterHashes() {
        // No space: `##title` isn't an ATX heading in CommonMark.
        XCTAssertNil(parseAtxHeading("##title"))
    }

    func testParseAtxStripsTrailingClosers() {
        XCTAssertEqual(parseAtxHeading("# Title ##")?.text, "Title")
        XCTAssertEqual(parseAtxHeading("## Title ###")?.text, "Title")
        XCTAssertEqual(parseAtxHeading("### Title #")?.text, "Title")
        // Trailing whitespace after closers is allowed.
        XCTAssertEqual(parseAtxHeading("## Title ##   ")?.text, "Title")
    }

    func testParseAtxKeepsHashesNotPrecededByWhitespace() {
        // `Title#` — closer must be preceded by whitespace, so this `#`
        // is part of the text.
        XCTAssertEqual(parseAtxHeading("## Title#")?.text, "Title#")
    }

    func testParseAtxStripsExtraSpacesAfterMarker() {
        XCTAssertEqual(parseAtxHeading("##   Title")?.text, "Title")
        XCTAssertEqual(parseAtxHeading("##\tTitle")?.text, "Title")
    }

    func testParseAtxAllowsZeroToThreeLeadingSpaces() {
        XCTAssertEqual(parseAtxHeading(" # Title")?.text, "Title")
        XCTAssertEqual(parseAtxHeading("  ## Title")?.text, "Title")
        XCTAssertEqual(parseAtxHeading("   ### Title")?.text, "Title")
        // 4 leading spaces is a code block per CommonMark, not a heading.
        XCTAssertNil(parseAtxHeading("    # Title"))
    }

    func testParseAtxEmptyHeadingIsValid() {
        // pulldown_cmark emits text="" for `#` with no content, so the
        // slicer must too — otherwise the empty heading falls through
        // as a paragraph and breaks the index alignment.
        XCTAssertEqual(parseAtxHeading("#")?.text, "")
        XCTAssertEqual(parseAtxHeading("#")?.level, 1)
        XCTAssertEqual(parseAtxHeading("## ##")?.text, "")
    }

    func testParseAtxRejectsNonHeadingLines() {
        XCTAssertNil(parseAtxHeading(""))
        XCTAssertNil(parseAtxHeading("not a heading"))
        XCTAssertNil(parseAtxHeading("hello # world"))
    }

    // MARK: - sliceIntoSections

    func testSliceTracksAbsoluteLineNumbers() {
        // Per-line scroll targets in NoteContentView need each
        // section to know where it starts in the source — bodyStart
        // for the body lines, startLine for the heading line itself.
        let text = """
            # Top
            intro line
            another intro line

            ## Section
            body of section
            """
        let headings = [
            Heading(level: 1, text: "Top", ordinal: 0, anchorId: "top"),
            Heading(level: 2, text: "Section", ordinal: 1, anchorId: "section"),
        ]
        let sections = sliceIntoSections(text: text, headings: headings)
        XCTAssertEqual(sections.count, 2)
        // First section: heading on line 1, body on lines 2–4.
        XCTAssertEqual(sections[0].startLineInFile, 1)
        XCTAssertEqual(sections[0].bodyStartLineInFile, 2)
        // Second section: heading on line 5, body on line 6.
        XCTAssertEqual(sections[1].startLineInFile, 5)
        XCTAssertEqual(sections[1].bodyStartLineInFile, 6)
    }

    func testSlicePreambleStartsAtLineOne() {
        let text = "preamble line one\npreamble line two\n# Real\nbody"
        let headings = [
            Heading(level: 1, text: "Real", ordinal: 0, anchorId: "real"),
        ]
        let sections = sliceIntoSections(text: text, headings: headings)
        XCTAssertEqual(sections.count, 2)
        XCTAssertNil(sections[0].heading)
        XCTAssertEqual(sections[0].startLineInFile, 1)
        XCTAssertEqual(sections[0].bodyStartLineInFile, 1)
        XCTAssertEqual(sections[1].startLineInFile, 3)
        XCTAssertEqual(sections[1].bodyStartLineInFile, 4)
    }

    func testSliceProducesOneSectionPerHeading() {
        let text = """
            # Top
            intro

            ## Section
            body
            """
        let headings = [h(1, "Top", ordinal: 0, anchorId: "top"),
                        h(2, "Section", ordinal: 1, anchorId: "section")]
        let sections = sliceIntoSections(text: text, headings: headings)
        XCTAssertEqual(sections.count, 2)
        XCTAssertEqual(sections[0].anchorId, "top")
        XCTAssertEqual(sections[0].body, "intro\n")
        XCTAssertEqual(sections[1].anchorId, "section")
        XCTAssertEqual(sections[1].body, "body")
    }

    func testSlicePreservesPreambleBeforeFirstHeading() {
        let text = """
            front matter
            another line

            # Real
            body
            """
        let headings = [h(1, "Real", ordinal: 0, anchorId: "real")]
        let sections = sliceIntoSections(text: text, headings: headings)
        XCTAssertEqual(sections.count, 2)
        XCTAssertNil(sections[0].heading)
        XCTAssertEqual(sections[0].anchorId, "__preamble")
        XCTAssertTrue(sections[0].body.contains("front matter"))
        XCTAssertEqual(sections[1].anchorId, "real")
    }

    func testSliceMatchesHeadingsWithTrailingClosers() {
        // This is the Codoki callout: scanner records "Title", source
        // has `# Title ##`. Slicer must recognize the line as the
        // canonical heading or it collapses the whole note into one
        // un-anchored section.
        let text = """
            # Title ##
            content
            ## Sub ###
            more
            """
        let headings = [h(1, "Title", ordinal: 0, anchorId: "title"),
                        h(2, "Sub", ordinal: 1, anchorId: "sub")]
        let sections = sliceIntoSections(text: text, headings: headings)
        XCTAssertEqual(sections.count, 2)
        XCTAssertEqual(sections[0].anchorId, "title")
        XCTAssertEqual(sections[1].anchorId, "sub")
    }

    func testSliceMatchesHeadingsWithExtraSpacing() {
        let text = """
            ##   Spacey
            text
            """
        let headings = [h(2, "Spacey", ordinal: 0, anchorId: "spacey")]
        let sections = sliceIntoSections(text: text, headings: headings)
        XCTAssertEqual(sections.count, 1)
        XCTAssertEqual(sections[0].anchorId, "spacey")
    }

    func testSliceMatchesHeadingsWithLeadingIndent() {
        let text = """
            preamble
              ## Indented
            body
            """
        let headings = [h(2, "Indented", ordinal: 0, anchorId: "indented")]
        let sections = sliceIntoSections(text: text, headings: headings)
        // Preamble + indented section.
        XCTAssertEqual(sections.count, 2)
        XCTAssertEqual(sections[1].anchorId, "indented")
    }

    func testSliceFoldsUnmatchedHashLinesIntoPriorBody() {
        // A `#` line that doesn't match the next-expected scanner
        // heading (e.g. one inside a fenced code block, or a typo
        // that didn't get parsed by pulldown_cmark) gets folded into
        // the prior body so the remaining heading indices stay aligned.
        let text = """
            # Real
            ```
            # not a heading inside code
            ```
            ## Next
            tail
            """
        let headings = [h(1, "Real", ordinal: 0, anchorId: "real"),
                        h(2, "Next", ordinal: 1, anchorId: "next")]
        let sections = sliceIntoSections(text: text, headings: headings)
        XCTAssertEqual(sections.count, 2)
        XCTAssertEqual(sections[0].anchorId, "real")
        XCTAssertTrue(sections[0].body.contains("not a heading"))
        XCTAssertEqual(sections[1].anchorId, "next")
    }

    func testSliceEmptyHeadingsListReturnsSinglePreamble() {
        let text = "no headings here\njust paragraphs"
        let sections = sliceIntoSections(text: text, headings: [])
        XCTAssertEqual(sections.count, 1)
        XCTAssertNil(sections[0].heading)
        XCTAssertEqual(sections[0].body, text)
    }

    func testSliceEmptyTextReturnsNoSections() {
        let sections = sliceIntoSections(text: "", headings: [])
        XCTAssertEqual(sections, [])
    }

    func testSliceNormalizesCRLFLineEndings() {
        // Codoki callout on PR 70: a Windows-saved vault file with
        // CRLF endings would feed each line into the parser with a
        // trailing `\r`, so `# Title\r` wouldn't match the scanner's
        // canonical "Title" and the whole note would render as one
        // un-anchored block.
        let text = "# Title ##\r\ncontent\r\n## Sub\r\nmore"
        let headings = [h(1, "Title", ordinal: 0, anchorId: "title"),
                        h(2, "Sub", ordinal: 1, anchorId: "sub")]
        let sections = sliceIntoSections(text: text, headings: headings)
        XCTAssertEqual(sections.count, 2)
        XCTAssertEqual(sections[0].anchorId, "title")
        XCTAssertEqual(sections[1].anchorId, "sub")
        // Body should also be CR-free so it renders cleanly.
        XCTAssertFalse(sections[0].body.contains("\r"))
        XCTAssertFalse(sections[1].body.contains("\r"))
    }

    func testSliceNormalizesBareCRLineEndings() {
        // Legacy classic-Mac files use bare \r. CommonMark §2.3 treats
        // these the same as \n, and pulldown_cmark normalizes them on
        // the Rust side; the Swift slicer must too.
        let text = "# Title\rcontent\r## Sub\rmore"
        let headings = [h(1, "Title", ordinal: 0, anchorId: "title"),
                        h(2, "Sub", ordinal: 1, anchorId: "sub")]
        let sections = sliceIntoSections(text: text, headings: headings)
        XCTAssertEqual(sections.count, 2)
        XCTAssertEqual(sections[0].anchorId, "title")
        XCTAssertEqual(sections[1].anchorId, "sub")
        // Bodies must be CR-free too — otherwise the renderer would
        // display stray control characters where a Mac-Classic source
        // had paragraph breaks.
        XCTAssertFalse(sections[0].body.contains("\r"))
        XCTAssertFalse(sections[1].body.contains("\r"))
    }

    func testSliceNormalizesMixedLineEndings() {
        // Real-world Markdown can mix line endings when content is
        // pasted between platforms — a CRLF heading followed by an
        // LF heading must still produce two sections.
        let text = "# Alpha\r\nintro line\n## Beta\rbody\n### Gamma\r\ntail"
        let headings = [h(1, "Alpha", ordinal: 0, anchorId: "alpha"),
                        h(2, "Beta", ordinal: 1, anchorId: "beta"),
                        h(3, "Gamma", ordinal: 2, anchorId: "gamma")]
        let sections = sliceIntoSections(text: text, headings: headings)
        XCTAssertEqual(sections.map(\.anchorId), ["alpha", "beta", "gamma"])
        for section in sections {
            XCTAssertFalse(section.body.contains("\r"), "section \(section.anchorId) leaked \\r")
        }
    }
}
