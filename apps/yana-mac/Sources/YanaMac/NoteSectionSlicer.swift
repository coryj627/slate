import Foundation

/// One heading-bounded chunk of a note's source. `.heading` is nil for
/// the preamble block before the first `#`.
struct NoteSection: Equatable {
    var heading: Heading?
    var anchorId: String
    var body: String
}

/// Walk `text` line-by-line and split at ATX headings that line up
/// with the scanner-recorded `headings` (in document order). The
/// scanner uses pulldown_cmark on the Rust side; this helper mimics
/// the relevant subset of CommonMark §4.2 so the Swift-side splits
/// match the Rust-side text exactly.
///
/// Returns one `NoteSection` per heading (plus an optional preamble
/// section if there's content before the first `#`). Each section's
/// `anchorId` is reusable as a SwiftUI `.id(_)` for ScrollViewReader.
///
/// Lines that look like headings but don't match the scanner's text
/// (e.g. `#` inside a fenced code block, or a 7-`#` ATX line that
/// isn't actually a heading) are folded into the body of the prior
/// section — same fallback as a plain paragraph line.
func sliceIntoSections(text: String, headings: [Heading]) -> [NoteSection] {
    let lines = text.components(separatedBy: "\n")
    var sections: [NoteSection] = []
    var current = NoteSection(heading: nil, anchorId: "__preamble", body: "")
    var bodyBuf: [String] = []
    var headingIdx = 0

    for line in lines {
        if headingIdx < headings.count,
            let parsed = parseAtxHeading(line),
            parsed.level == headings[headingIdx].level,
            parsed.text == headings[headingIdx].text
        {
            current.body = bodyBuf.joined(separator: "\n")
            if current.heading != nil || !current.body.isEmpty {
                sections.append(current)
            }
            let h = headings[headingIdx]
            current = NoteSection(heading: h, anchorId: h.anchorId, body: "")
            bodyBuf = []
            headingIdx += 1
        } else {
            bodyBuf.append(line)
        }
    }
    current.body = bodyBuf.joined(separator: "\n")
    if current.heading != nil || !current.body.isEmpty {
        sections.append(current)
    }
    return sections
}

/// Parse `line` as an ATX heading per CommonMark §4.2.
///
/// Returns `(level, text)` if the line is a valid ATX heading, else
/// nil. Handles the subset that matters for matching pulldown_cmark's
/// output:
///   - 0–3 leading spaces before the opening `#`s (4+ spaces would
///     make it a code block, not a heading)
///   - 1–6 opening `#` characters
///   - Required space/tab between opening and content (or end-of-line
///     for an empty heading)
///   - Optional trailing closing sequence: whitespace + 1+ `#`s +
///     optional trailing whitespace
///   - Leading + trailing whitespace in the heading body is trimmed
///
/// Deliberately does NOT handle setext (`===` / `---`) headings,
/// escape sequences, or fenced-code-block tracking — the scanner
/// emits the canonical text, and this is just a positional match.
/// If we'd guess wrong about a closer, the worst case is one section
/// boundary is missed (the line gets folded into the prior body),
/// not a crash or wrong-anchor scroll.
func parseAtxHeading(_ line: String) -> (level: UInt8, text: String)? {
    var idx = line.startIndex
    var leadingSpaces = 0
    while idx < line.endIndex, line[idx] == " ", leadingSpaces < 3 {
        idx = line.index(after: idx)
        leadingSpaces += 1
    }

    var level: UInt8 = 0
    while idx < line.endIndex, line[idx] == "#" {
        level += 1
        idx = line.index(after: idx)
        if level > 6 { return nil }
    }
    if level == 0 { return nil }

    // Empty heading line: `#` followed by nothing is allowed in
    // CommonMark and produces an empty-text heading. The scanner
    // emits text="" for these.
    if idx == line.endIndex {
        return (level, "")
    }

    // Must be whitespace between opening sequence and content.
    let afterOpening = line[idx]
    if afterOpening != " " && afterOpening != "\t" {
        return nil
    }
    idx = line.index(after: idx)

    var body = String(line[idx...])

    // Trim trailing whitespace (closing sequence may be followed by
    // any number of trailing spaces).
    while let last = body.last, last == " " || last == "\t" {
        body.removeLast()
    }

    // Strip optional closing sequence: one-or-more trailing `#`s
    // that are preceded by whitespace (or are the entire body). A
    // run of `#`s NOT preceded by whitespace is part of the text
    // (e.g. `## Title#`).
    var bIdx = body.endIndex
    while bIdx > body.startIndex, body[body.index(before: bIdx)] == "#" {
        bIdx = body.index(before: bIdx)
    }
    let hashCount = body.distance(from: bIdx, to: body.endIndex)
    if hashCount > 0 {
        if bIdx == body.startIndex {
            // Entire body is `#`s — that's the closer, text is empty.
            body = ""
        } else {
            let beforeHashes = body.index(before: bIdx)
            if body[beforeHashes] == " " || body[beforeHashes] == "\t" {
                body = String(body[..<bIdx])
                while let last = body.last, last == " " || last == "\t" {
                    body.removeLast()
                }
            }
        }
    }

    // Trim leading whitespace.
    while let first = body.first, first == " " || first == "\t" {
        body.removeFirst()
    }

    return (level, body)
}
