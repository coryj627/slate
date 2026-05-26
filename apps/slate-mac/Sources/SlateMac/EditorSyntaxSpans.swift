// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// One classified syntax span discovered in the editor's text.
/// `range` is an NSRange over the buffer's UTF-16 view (the same
/// coordinate space `NSTextView` / `NSTextStorage` use), so the
/// result drops into attribute calls without further conversion.
struct EditorSyntaxSpan: Equatable {
    let range: NSRange
    let kind: SyntaxKind
}

/// Markdown syntax classifier for editor-source highlighting (#296).
///
/// Single-priority scheme: every span carries a `kind`; the apply
/// step in `NoteEditorView.Coordinator` stamps the matching color
/// from `EditorSyntaxPalette`. Overlaps are resolved by detection
/// order — code spans are detected FIRST so any markdown-shaped text
/// inside them (a `**` inside a code fence, a `[[wikilink]]` inside
/// inline code) is masked off before the later regex passes run.
///
/// The embed span shape (`![[…]]`) intentionally lives in
/// `EditorEmbedSpans.swift` instead of `wikilink` here — the embed
/// path also drives the Cmd+E preview popover and we want its
/// `.target` payload separate from purely visual classification.
/// The embed underline (audit #207, #230) is still applied on top of
/// the wikilink color from this pass; both attributes co-exist on
/// the embed range.
enum SyntaxKind: Hashable, CaseIterable {
    /// `---` lines and YAML content between them (only valid at the
    /// very start of the buffer).
    case frontmatter
    /// ATX headings — the `#` markers AND the heading text after them
    /// (one-shot color for the whole line so scanning the outline is
    /// fast). `# H1` through `###### H6`.
    case heading
    /// Setext heading underlines — `===` (h1) and `---` (h2)
    /// immediately under a non-empty line. The text line itself is
    /// not coloured here; the underline alone carries the cue.
    case setextUnderline
    /// Fenced code blocks — the ``` lines plus everything between
    /// them. Detected first so inline emphasis/wikilink/tag patterns
    /// inside the fence are masked.
    case codeBlock
    /// Inline code — `` `…` ``. Single-tick form only (the literal
    /// `` `` … `` `` double-tick form for embedded backticks is rare
    /// enough to defer).
    case inlineCode
    /// Bold (`**…**` / `__…__`), italic (`*…*` / `_…_`), and
    /// strikethrough (`~~…~~`) markers. Highlighted at marker level
    /// only — the text BETWEEN the markers stays in the body color
    /// so the highlight reads as "this is a markup boundary" rather
    /// than re-styling the prose itself.
    case emphasisMarker
    /// Bare-bracket wikilinks `[[…]]`. The embed `![[…]]` shape is
    /// included here — its colour is the same; the underline cue
    /// from #207 stays applied on top by `applyEmbedHighlighting`.
    case wikilink
    /// Inline tags — `#tag` not at the start of a line (otherwise
    /// `# heading` would match). The tag character and the
    /// alphanumeric/hyphen/underscore body get one colour.
    case tag
    /// Obsidian-style `%% … %%` comment blocks (inline + multi-line).
    /// Treated as de-emphasized so they fade visually like comments
    /// in code.
    case commentBlock
    /// Pandoc-style citations — `[@key]` (bracket form) and `@key`
    /// (bare form). De-emphasized like comments; the citation panel
    /// has the rich rendering, the editor source only needs a cue
    /// that this token is a citation reference.
    case citation
}

/// Find every syntax span in `text`. The result is sorted by start
/// offset; overlapping ranges (e.g. an emphasis marker inside a
/// wikilink) are excluded — code spans are detected first, then the
/// remaining passes skip text that's already covered.
///
/// Regex-based. Markdown's actual grammar isn't regular (the
/// canonical reason is the nesting / fence-content interactions), so
/// this trades fidelity for simplicity; the rendered-document view
/// uses the backend's full parser. For editor highlighting the goal
/// is sighted-user scanning speed — perfect token boundaries aren't
/// required. Edge cases the regex misses (a `**` split across lines,
/// an unmatched ` ``` `) intentionally fall through to body color.
func findEditorSyntaxSpans(in text: String) -> [EditorSyntaxSpan] {
    let nsText = text as NSString
    let fullRange = NSRange(location: 0, length: nsText.length)
    var out: [EditorSyntaxSpan] = []
    // Coverage map — offsets already claimed by a higher-priority
    // span. Subsequent passes consult this so they don't re-classify
    // text inside a code block as a wikilink, etc.
    var covered = IndexSet()

    func append(_ range: NSRange, kind: SyntaxKind) {
        let intersected = NSIntersectionRange(range, fullRange)
        guard intersected.length > 0 else { return }
        let indexRange = intersected.location..<(intersected.location + intersected.length)
        // Skip ranges that overlap higher-priority coverage. Strict
        // check — any byte of overlap disqualifies the span; the
        // alternative is splitting/clipping which adds complexity
        // for negligible visual benefit.
        if covered.intersects(integersIn: indexRange) { return }
        out.append(EditorSyntaxSpan(range: intersected, kind: kind))
        covered.insert(integersIn: indexRange)
    }

    // 1. Frontmatter (must be at very start of buffer; `---` open + close).
    if let fm = findFrontmatter(in: text) {
        append(fm, kind: .frontmatter)
    }

    // 2. Fenced code blocks (``` open + close on their own line).
    for range in findFencedCodeBlocks(in: text) {
        append(range, kind: .codeBlock)
    }

    // 3. Comment blocks (`%% … %%`) — second-highest priority after code
    // so a `**bold**` inside a comment doesn't re-classify as emphasis.
    for range in findCommentBlocks(in: text) {
        append(range, kind: .commentBlock)
    }

    // 4. Inline code (`` `…` ``).
    for range in regexRanges(in: text, pattern: #"`[^`\n]+`"#) {
        append(range, kind: .inlineCode)
    }

    // 5. Wikilinks (`[[…]]` AND `![[…]]` — the embed `!` is included
    // so the leading `!` gets the same colour).
    for range in regexRanges(in: text, pattern: #"!?\[\[[^\]\n]+\]\]"#) {
        append(range, kind: .wikilink)
    }

    // 6. ATX headings (`#…` at start of line, 1–6 `#`).
    for range in regexRanges(
        in: text,
        pattern: #"(?m)^#{1,6}[ \t][^\n]*"#
    ) {
        append(range, kind: .heading)
    }

    // 7. Setext underlines — `===` or `---` on a line of their own,
    // right under a non-empty content line. NSRegularExpression's
    // variable-length-lookbehind support is patchy; do this as a
    // line-by-line scan instead. The frontmatter open/close (`---`
    // at file start) is already in `covered`; the `append` helper
    // rejects overlapping ranges so the frontmatter close doesn't
    // double-match as a setext underline.
    for range in findSetextUnderlines(in: text) {
        append(range, kind: .setextUnderline)
    }

    // 8. Pandoc citations — bracket form `[@…]` and bare `@key`.
    // Bracket-form first so the inner `@key` doesn't double-match.
    for range in regexRanges(in: text, pattern: #"\[@[\w:-]+(?:[ \t,;][^\]]*)?\]"#) {
        append(range, kind: .citation)
    }
    for range in regexRanges(in: text, pattern: #"(?<![\w@])@[\w:-]+"#) {
        append(range, kind: .citation)
    }

    // 9. Inline `#tag` — not at the start of a line (that's a heading).
    // Allows letters, digits, hyphens, underscores, and `/` for
    // nested tags. The lookbehind for non-`#`/non-word avoids
    // matching the middle of a word and avoids re-matching `##` from
    // a heading.
    for range in regexRanges(
        in: text,
        pattern: #"(?<![\w#])#[A-Za-z][\w/-]*"#
    ) {
        append(range, kind: .tag)
    }

    // 10. Emphasis markers — bold `**` / `__`, italic `*` / `_`, and
    // strikethrough `~~`. Only the marker pairs are spanned, not the
    // text between them; the body stays in textColor so prose doesn't
    // change appearance when the user adds emphasis syntax.
    //
    // Order: triple-marker patterns first (`***bold-italic***`),
    // then double, then single. Each pattern matches the OPENING and
    // CLOSING markers as separate spans so the user can see the
    // boundary clearly.
    for range in regexRanges(in: text, pattern: #"\*\*\*(?=\S)"#) { append(range, kind: .emphasisMarker) }
    for range in regexRanges(in: text, pattern: #"(?<=\S)\*\*\*"#) { append(range, kind: .emphasisMarker) }
    for range in regexRanges(in: text, pattern: #"\*\*(?=\S)"#) { append(range, kind: .emphasisMarker) }
    for range in regexRanges(in: text, pattern: #"(?<=\S)\*\*"#) { append(range, kind: .emphasisMarker) }
    for range in regexRanges(in: text, pattern: #"__(?=\S)"#) { append(range, kind: .emphasisMarker) }
    for range in regexRanges(in: text, pattern: #"(?<=\S)__"#) { append(range, kind: .emphasisMarker) }
    for range in regexRanges(in: text, pattern: #"~~(?=\S)"#) { append(range, kind: .emphasisMarker) }
    for range in regexRanges(in: text, pattern: #"(?<=\S)~~"#) { append(range, kind: .emphasisMarker) }
    // Single `*`/`_` for italic — bounded by non-word on the outside
    // and non-space on the inside, so `2*3` (math) and `snake_case`
    // (identifiers) don't trigger.
    for range in regexRanges(
        in: text,
        pattern: #"(?<![\w*])\*(?=\S)"#
    ) { append(range, kind: .emphasisMarker) }
    for range in regexRanges(
        in: text,
        pattern: #"(?<=\S)\*(?![\w*])"#
    ) { append(range, kind: .emphasisMarker) }
    for range in regexRanges(
        in: text,
        pattern: #"(?<![\w_])_(?=\S)"#
    ) { append(range, kind: .emphasisMarker) }
    for range in regexRanges(
        in: text,
        pattern: #"(?<=\S)_(?![\w_])"#
    ) { append(range, kind: .emphasisMarker) }

    // Sort by start offset so the apply pass stamps left-to-right
    // (NSAttributedString's `addAttributes(_:range:)` is order-
    // independent, but sorted output makes test fixtures readable
    // and benchmark output stable).
    out.sort { $0.range.location < $1.range.location }
    return out
}

// MARK: - Pass implementations

/// Frontmatter MUST start at offset 0 — Obsidian/Pandoc convention.
/// Opens with a `---` line, closes with another `---` line; the span
/// covers the open delimiter, content, and close delimiter (but
/// NOT the trailing newline after the close — that belongs to the
/// blank-line separator following the frontmatter block).
private func findFrontmatter(in text: String) -> NSRange? {
    let nsText = text as NSString
    // Bail fast if the buffer doesn't start with `---\n` or `---\r\n`.
    let leadPattern = #"^---\r?\n"#
    guard let leadRegex = try? NSRegularExpression(pattern: leadPattern, options: []),
        let leadMatch = leadRegex.firstMatch(
            in: text,
            options: [],
            range: NSRange(location: 0, length: nsText.length)
        ),
        leadMatch.range.location == 0
    else { return nil }
    // Find the closing `---` line after the open. The lookahead for
    // newline-or-EOF means the match doesn't consume the trailing
    // newline of the close line — keeps the span's right edge at
    // the final `-` so the blank line between frontmatter and body
    // doesn't pick up the frontmatter colour.
    let searchStart = leadMatch.range.length
    let searchRange = NSRange(location: searchStart, length: nsText.length - searchStart)
    let closePattern = #"(?m)^---[ \t]*(?=\r?\n|$)"#
    guard let closeRegex = try? NSRegularExpression(pattern: closePattern, options: []),
        let closeMatch = closeRegex.firstMatch(
            in: text,
            options: [],
            range: searchRange
        )
    else { return nil }
    let closeEnd = closeMatch.range.location + closeMatch.range.length
    return NSRange(location: 0, length: closeEnd)
}

/// Fenced code blocks — opens with ` ``` ` on its own line (optionally
/// followed by a language tag), closes with another ` ``` ` line.
/// Each pair becomes one span; unclosed fences extend to the end of
/// the buffer so the user sees they have an unterminated fence.
private func findFencedCodeBlocks(in text: String) -> [NSRange] {
    let nsText = text as NSString
    var out: [NSRange] = []
    let fencePattern = #"(?m)^```[^\n]*$"#
    guard let regex = try? NSRegularExpression(pattern: fencePattern, options: []) else {
        return out
    }
    let matches = regex.matches(
        in: text,
        options: [],
        range: NSRange(location: 0, length: nsText.length)
    )
    var i = 0
    while i < matches.count {
        let openRange = matches[i].range
        if i + 1 < matches.count {
            let closeRange = matches[i + 1].range
            let end = closeRange.location + closeRange.length
            out.append(NSRange(location: openRange.location, length: end - openRange.location))
            i += 2
        } else {
            // Unclosed fence — extend to end of buffer.
            out.append(NSRange(location: openRange.location, length: nsText.length - openRange.location))
            i += 1
        }
    }
    return out
}

/// Setext heading underlines — a line consisting of one or more `=`
/// (for h1) or `-` (for h2) characters, optionally followed by spaces,
/// immediately under a non-empty content line. Line-by-line scan
/// because NSRegularExpression's variable-length lookbehind is
/// patchy across SDKs and the line-walk is O(n) in characters
/// anyway.
private func findSetextUnderlines(in text: String) -> [NSRange] {
    var out: [NSRange] = []
    let nsText = text as NSString
    var lineStart = 0
    var previousLineWasNonEmpty = false
    while lineStart <= nsText.length {
        // Find end of current line.
        var lineEnd = lineStart
        while lineEnd < nsText.length {
            let ch = nsText.character(at: lineEnd)
            if ch == UInt16(0x0A) { break }  // \n
            lineEnd += 1
        }
        let lineRange = NSRange(location: lineStart, length: lineEnd - lineStart)
        let line = nsText.substring(with: lineRange)
        let trimmed = line.trimmingCharacters(in: .whitespaces)
        let isAllEquals = !trimmed.isEmpty && trimmed.allSatisfy { $0 == "=" }
        let isAllDashes = !trimmed.isEmpty && trimmed.allSatisfy { $0 == "-" }
        if (isAllEquals || isAllDashes) && previousLineWasNonEmpty {
            out.append(lineRange)
        }
        previousLineWasNonEmpty = !trimmed.isEmpty
        // Move past the newline (or past EOF).
        lineStart = (lineEnd < nsText.length) ? lineEnd + 1 : lineEnd + 1
    }
    return out
}

/// Obsidian-style `%% … %%` comment blocks. Inline form (`%% comment %%`
/// on one line) and multi-line form both supported. Unmatched `%%`
/// (open without close) falls through to body color.
private func findCommentBlocks(in text: String) -> [NSRange] {
    let nsText = text as NSString
    var out: [NSRange] = []
    let pattern = #"%%[\s\S]*?%%"#
    guard let regex = try? NSRegularExpression(pattern: pattern, options: []) else {
        return out
    }
    let matches = regex.matches(
        in: text,
        options: [],
        range: NSRange(location: 0, length: nsText.length)
    )
    for match in matches {
        out.append(match.range)
    }
    return out
}

/// Generic regex helper — compile + enumerate, return the FULL match
/// range for each hit. Returns empty on regex compile failure so a
/// malformed pattern doesn't crash the editor (caught in tests).
private func regexRanges(in text: String, pattern: String) -> [NSRange] {
    let nsText = text as NSString
    guard let regex = try? NSRegularExpression(pattern: pattern, options: []) else {
        return []
    }
    let matches = regex.matches(
        in: text,
        options: [],
        range: NSRange(location: 0, length: nsText.length)
    )
    return matches.map { $0.range }
}
