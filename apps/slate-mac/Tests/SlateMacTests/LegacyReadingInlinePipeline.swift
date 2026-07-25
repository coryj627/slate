// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Accessibility
import Foundation
import SwiftUI

@testable import SlateMac

// FROZEN SNAPSHOT — the reading inline pipeline as it stood at 02f9153,
// immediately before #967 replaced it with the canonical core API.
//
// Extracted verbatim from
//   apps/slate-mac/Sources/SlateMac/Reading/ReadingInlineMapper.swift
//   apps/slate-mac/Sources/SlateMac/Reading/ReadingBlockSource.swift
//   apps/slate-mac/Sources/SlateMac/Reading/ReadingLinkRouter.swift
// with only these edits: symbols renamed into the `Legacy*` namespace, and
// the router reduced to the pieces the RENDER path used (schemes, codec,
// grammar, record sets, candidate keys, dispositions). Interaction wiring —
// `route`, `live(appState:)` — is deliberately absent: this snapshot exists
// to render, not to navigate.
//
// It is SELF-CONTAINED on purpose. Three of the router helpers it needs
// (`LinkRecordSets`, `candidateKeys`, `baseTarget`) were deleted by #967, and
// `disposition` changed its grammar type, so leaning on production symbols
// would either not compile or silently re-point the "old" side of the
// differential at the new implementation — which would make the census
// compare the new code against itself.
//
// Do not "fix" anything here. Its value is being wrong in exactly the ways
// the shipped code used to be wrong.

/// The retired router surface, render-path only.
enum LegacyRouter {

    static let wikiScheme = "slate-wiki"
    static let embedScheme = "slate-embed"
    static let tagScheme = "slate-tag"
    static let citeScheme = "slate-cite"
    /// Codex round 2: internal MARKDOWN destinations rewritten by the
    /// mapper ride their own scheme so the routed value retains its
    /// source grammar — `^` is an anchor marker in wikilink grammar but
    /// a legal path character in a markdown destination, and one shared
    /// scheme made `[[note^block]]` able to activate a sibling
    /// `[m](note^block)` record.
    static let wikiMarkdownScheme = "slate-wikimd"


    enum WikiTargetGrammar: Equatable {
        case wikilink
        case markdownDestination
    }

    /// Does a record's authoring kind (`links_db.rs`: "wikilink" /
    /// "markdown") match the grammar that routed the activation? Codex
    /// round 3: matching on `targetRaw` alone let an UNSAVED
    /// `[[note^block]]` activate a saved `[m](note^block)` record
    /// through the verbatim arm — a cross-grammar record hit is always
    /// the wrong record.
    static func recordKindMatches(
        _ kind: String, grammar: WikiTargetGrammar
    ) -> Bool {
        switch grammar {
        case .wikilink: return kind == "wikilink"
        case .markdownDestination: return kind == "markdown"
        }
    }

    /// Kind-partitioned record sets for the styling classifier (Codex
    /// round 3 — one flat set let a run of one grammar classify
    /// against the other grammar's records). The EMPTY value is the
    /// honest "no records for this note" classification (every run
    /// unresolved), used while `currentOutgoingLinks` still belongs to
    /// a previous note.
    struct LinkRecordSets: Equatable {
        var knownWikilink: Set<String> = []
        var unresolvedWikilink: Set<String> = []
        var knownMarkdown: Set<String> = []
        var unresolvedMarkdown: Set<String> = []

        init() {}

        init(records: [OutgoingLink]) {
            for record in records where !record.isEmbed && !record.isExternal {
                switch record.kind {
                case "wikilink":
                    knownWikilink.insert(record.targetRaw)
                    if record.isUnresolved {
                        unresolvedWikilink.insert(record.targetRaw)
                    }
                case "markdown":
                    knownMarkdown.insert(record.targetRaw)
                    if record.isUnresolved {
                        unresolvedMarkdown.insert(record.targetRaw)
                    }
                default:
                    break
                }
            }
        }

        func known(for grammar: WikiTargetGrammar) -> Set<String> {
            grammar == .wikilink ? knownWikilink : knownMarkdown
        }

        func unresolved(for grammar: WikiTargetGrammar) -> Set<String> {
            grammar == .wikilink ? unresolvedWikilink : unresolvedMarkdown
        }
    }

    static func encodedURL(scheme: String, target: String) -> URL? {
        let unreserved = CharacterSet.alphanumerics
            .union(CharacterSet(charactersIn: "-._~"))
        guard
            let encoded = target.addingPercentEncoding(
                withAllowedCharacters: unreserved)
        else { return nil }
        return URL(string: "\(scheme)://\(encoded)")
    }

    /// Reverse of `encodedURL`: strip `<scheme>://` and percent-decode.
    static func decodedTarget(from url: URL) -> String {
        let absolute = url.absoluteString
        guard let separator = absolute.range(of: "://") else { return "" }
        let encoded = String(absolute[separator.upperBound...])
        return encoded.removingPercentEncoding ?? ""
    }

    static func baseTarget(of target: String) -> String {
        // Trim mirrors links.rs (red-team: padded targets left styling
        // and activation disagreeing).
        let trimmed = target.trimmingCharacters(in: .whitespaces)
        if let hash = trimmed.firstIndex(of: "#") {
            return String(trimmed[trimmed.startIndex..<hash])
                .trimmingCharacters(in: .whitespaces)
        }
        if let caret = trimmed.firstIndex(of: "^") {
            return String(trimmed[trimmed.startIndex..<caret])
                .trimmingCharacters(in: .whitespaces)
        }
        return trimmed
    }

    /// Ordered record-match keys for one routed target, EXACT per
    /// grammar (Codex round 2 — a grammar-blind list let a wikilink
    /// activate a markdown sibling's record): wikilink grammar cuts the
    /// anchor at the first `#`, else the first `^` (legacy block ref) —
    /// `links.rs::split_wikilink_body`; markdown-destination grammar
    /// cuts ONLY at `#`, `^` is a legal path character
    /// (`links.rs::split_markdown_target`). The verbatim target closes
    /// each list as the pre-#509 defense (rows still carrying a
    /// fragment in `targetRaw`). One list for the live router's record
    /// match AND the mapper's styling classification — agreement by
    /// construction.
    static func candidateKeys(
        for target: String, grammar: WikiTargetGrammar
    ) -> [String] {
        let trimmed = target.trimmingCharacters(in: .whitespaces)
        var keys: [String] = []
        func push(_ key: String) {
            if !key.isEmpty, !keys.contains(key) { keys.append(key) }
        }
        switch grammar {
        case .wikilink:
            push(baseTarget(of: trimmed))
        case .markdownDestination:
            if let hash = trimmed.firstIndex(of: "#") {
                push(
                    String(trimmed[trimmed.startIndex..<hash])
                        .trimmingCharacters(in: .whitespaces))
            }
        }
        push(trimmed)
        return keys
    }

    enum Disposition: Equatable {
        case wiki(String, WikiTargetGrammar)
        case embed(String)
        case tag(String)
        case citation(String)
        /// Allowlisted external scheme — hand to the system.
        case external
        /// Everything else (`file:`, `javascript:`, unknown schemes, and any
        /// scheme-less URL the mapper chose not to rewrite) — dropped, never
        /// LaunchServices. The mapper strips the link affordance from these,
        /// so a discard here is defense in depth, not a reachable dead end.
        case discard
    }

    static func disposition(for url: URL) -> Disposition {
        guard let scheme = url.scheme?.lowercased() else { return .discard }
        switch scheme {
        case Self.wikiScheme:
            return .wiki(Self.decodedTarget(from: url), .wikilink)
        case Self.wikiMarkdownScheme:
            return .wiki(Self.decodedTarget(from: url), .markdownDestination)
        case Self.embedScheme: return .embed(Self.decodedTarget(from: url))
        case Self.tagScheme: return .tag(Self.decodedTarget(from: url))
        case Self.citeScheme: return .citation(Self.decodedTarget(from: url))
        case "http", "https", "mailto": return .external
        default: return .discard
        }
    }
}

/// The retired block-chrome strippers (`ReadingBlockSource` inline paths).
enum LegacyBlockSource {

    static func headingText(_ source: String) -> String {
        let lines = source.split(separator: "\n", omittingEmptySubsequences: false)
        guard let first = lines.first else { return source }
        let trimmedFirst = first.trimmingCharacters(in: .whitespaces)

        // ATX: 1–6 `#` then space(s). Also trim a trailing closing sequence
        // (` ###`) per CommonMark.
        var hashes = 0
        for ch in trimmedFirst {
            if ch == "#" { hashes += 1 } else { break }
        }
        if (1...6).contains(hashes) {
            var text = String(trimmedFirst.dropFirst(hashes))
            guard text.isEmpty || text.first == " " || text.first == "\t" else {
                // `#not-a-heading` — the classifier said Heading, so this is
                // a setext or unusual form; fall through to setext handling.
                return setextOrVerbatim(lines: lines, source: source)
            }
            text = text.trimmingCharacters(in: .whitespaces)
            while text.hasSuffix("#") { text = String(text.dropLast()) }
            return text.trimmingCharacters(in: .whitespaces)
        }
        return setextOrVerbatim(lines: lines, source: source)
    }

    /// Setext form: `Title\n====` / `Title\n----` → the first line.
    private static func setextOrVerbatim(
        lines: [Substring], source: String
    ) -> String {
        if lines.count >= 2 {
            let underline = lines[1].trimmingCharacters(in: .whitespaces)
            if !underline.isEmpty,
                underline.allSatisfy({ $0 == "=" }) || underline.allSatisfy({ $0 == "-" })
            {
                return lines[0].trimmingCharacters(in: .whitespaces)
            }
        }
        return source.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    // MARK: - List items

    struct ListItemParts: Equatable {
        /// The authored marker, verbatim (`-`, `*`, `+`, `3.`, `12)`). The
        /// renderer shows `•` for unordered markers but the ORDERED number is
        /// displayed verbatim — the source carries the real ordinal, so no
        /// re-derivation (and no wrong renumbering) is possible.
        var marker: String
        /// Inline content after the marker (and after the `[x]` checkbox for
        /// task items), continuation lines preserved verbatim.
        var content: String
        /// The task status char between `[` and `]`, when present.
        var taskChar: String?
    }

    /// Split a list-item slice into marker / optional task box / content.
    /// Returns nil when no marker is found (degrade: render slice verbatim).
    ///
    /// `stripTaskBox` gates the `[c]` removal and must be true ONLY when the
    /// Rust block kind already says this item IS a task — taskhood belongs to
    /// the classifier, not this splitter. A plain list item that merely looks
    /// boxy keeps its bracket text verbatim: `1. [v] Visible` (ordered items
    /// are never tasks) and `- [v]x` (no space after the box) both reach the
    /// plain-list renderer, and unconditional stripping lost that authored
    /// content (Codoki, #514).
    static func listItemParts(
        _ source: String, stripTaskBox: Bool = false
    ) -> ListItemParts? {
        let firstLineEnd = source.firstIndex(of: "\n") ?? source.endIndex
        let firstLine = source[source.startIndex..<firstLineEnd]
        let rest = firstLineEnd < source.endIndex
            ? String(source[source.index(after: firstLineEnd)...]) : ""

        var index = firstLine.startIndex
        // Leading indentation (nested items keep their indent in the slice).
        while index < firstLine.endIndex,
            firstLine[index] == " " || firstLine[index] == "\t"
        {
            index = firstLine.index(after: index)
        }
        guard index < firstLine.endIndex else { return nil }

        var marker = ""
        let ch = firstLine[index]
        if ch == "-" || ch == "*" || ch == "+" {
            marker = String(ch)
            index = firstLine.index(after: index)
        } else if ch.isNumber {
            var digitsEnd = index
            while digitsEnd < firstLine.endIndex, firstLine[digitsEnd].isNumber {
                digitsEnd = firstLine.index(after: digitsEnd)
            }
            guard digitsEnd < firstLine.endIndex,
                firstLine[digitsEnd] == "." || firstLine[digitsEnd] == ")"
            else { return nil }
            marker = String(firstLine[index...digitsEnd])
            index = firstLine.index(after: digitsEnd)
        } else {
            return nil
        }

        // Exactly the marker-terminating whitespace.
        while index < firstLine.endIndex,
            firstLine[index] == " " || firstLine[index] == "\t"
        {
            index = firstLine.index(after: index)
        }

        var content = String(firstLine[index...])
        var taskChar: String? = nil
        // Task box: `[c] ` — same shape the Rust tasks grammar recognizes.
        // Only split it off when the caller vouched (via `stripTaskBox`)
        // that the classifier marked this item a task.
        if stripTaskBox, content.hasPrefix("["), content.count >= 3 {
            let afterOpen = content.index(after: content.startIndex)
            let closeIndex = content.index(afterOpen, offsetBy: 1)
            if content[closeIndex] == "]" {
                taskChar = String(content[afterOpen])
                var remainder = String(content[content.index(after: closeIndex)...])
                if remainder.hasPrefix(" ") { remainder.removeFirst() }
                content = remainder
            }
        }

        if !rest.isEmpty {
            content += "\n" + rest
        }
        return ListItemParts(marker: marker, content: content, taskChar: taskChar)
    }

    // MARK: - Block quotes

    /// Strip up to `depth` `>` markers (each with one optional following
    /// space) from the start of every line.
    static func quoteContent(_ source: String, depth: UInt8) -> String {
        let lines = source.split(separator: "\n", omittingEmptySubsequences: false)
        let stripped = lines.map { line -> String in
            var view = Substring(line)
            for _ in 0..<max(1, Int(depth)) {
                let lead = view.drop(while: { $0 == " " || $0 == "\t" })
                guard lead.first == ">" else { break }
                view = lead.dropFirst()
                if view.first == " " { view = view.dropFirst() }
            }
            return String(view)
        }
        return stripped.joined(separator: "\n")
    }
}

/// The retired inline mapper.
enum LegacyInlineMapper {

    /// One mapped construct, in source order. `axLabel` is the display text
    /// for wiki/embed/tag runs and the citation's speech text for citation
    /// runs — the run model IS the tested contract.
    struct MappedRun: Equatable {
        enum Kind: Equatable {
            case wiki
            case embed
            case tag
            case citation
        }

        let kind: Kind
        /// Visible text of the run.
        let display: String
        /// Decoded routing target (wiki target with anchor, embed cache-key
        /// form, tag name without `#`, citation raw text).
        let target: String
        let url: URL
        let axLabel: String
    }

    struct Mapped {
        var attributed: AttributedString
        var runs: [MappedRun]
    }

    /// Map one source slice. `citations` supplies the speech/visual text for
    /// citation runs (matched by `RenderedCitation.raw` — the record carries
    /// no byte offset); an unmatched citation degrades to its raw text.
    ///
    /// `unresolvedTargets` (#849) is the set of UNRESOLVED outgoing-link
    /// `targetRaw` values for the owning note (sourced the way
    /// `OutgoingLinksPanel` reads `isUnresolved` —
    /// `appState.currentOutgoingLinks`). Wiki-scheme runs whose target is a
    /// member render in `warningText` (underline kept — the affordance is
    /// never color-only) instead of accent, matching the editor's U5-3
    /// unresolved treatment, so a dangling `[[Missing Note]]` is
    /// distinguishable BEFORE activation. Membership uses the router's
    /// exact resolution key (see `isUnresolvedWikiLink`). Pure param —
    /// empty set (the default) styles everything as before.
    static func map(
        slice: String, citations: [RenderedCitation] = [],
        unresolvedTargets: Set<String> = [],
        recordSets: LegacyRouter.LinkRecordSets? = nil
    ) -> Mapped {
        // Two Rust authorities compose here: the highlight classifier for
        // Slate tokens, plus the CommonMark link/image spans it intentionally
        // omits — those arrive only as exclusion ranges (isMappableKind
        // ignores them), so Slate-token splicing stays out of markdown-link
        // syntax the native parse owns.
        let spans = editorHighlightSpans(text: slice) + markdownLinkSpans(text: slice)
        let mappable = mappableSpans(from: spans)

        let utf8 = Array(slice.utf8)
        func segment(_ start: Int, _ end: Int) -> String {
            guard start >= 0, end <= utf8.count, start < end else { return "" }
            return String(decoding: utf8[start..<end], as: UTF8.self)
        }

        var markdown = ""
        var runs: [MappedRun] = []
        var cursor = 0
        for span in mappable {
            let start = Int(span.startByte)
            let end = Int(span.endByte)
            guard start >= cursor, end <= utf8.count, start < end else { continue }
            markdown += segment(cursor, start)
            let text = segment(start, end)
            if let run = mapRun(kind: span.kind, spanText: text, citations: citations) {
                markdown += "[\(escapeMarkdownLabel(run.display))](\(run.url.absoluteString))"
                runs.append(run)
            } else {
                // Interior didn't parse (e.g. `[[]]`); keep the bytes.
                markdown += text
            }
            cursor = end
        }
        markdown += segment(cursor, utf8.count)

        var attributed: AttributedString
        do {
            attributed = try AttributedString(
                markdown: markdown,
                options: .init(interpretedSyntax: .inlineOnlyPreservingWhitespace))
        } catch {
            // Inline-only parsing accepts arbitrary text in practice; if it
            // ever throws, degrade to the verbatim slice — never lose content.
            attributed = AttributedString(slice)
        }

        style(
            &attributed, runs: runs, unresolvedTargets: unresolvedTargets,
            recordSets: recordSets)
        return Mapped(attributed: attributed, runs: runs)
    }

    // MARK: - Span selection

    /// Keep the wikilink / embed / tag / citation spans, outermost-first, and
    /// drop (a) spans nested inside an already-kept span (an embed's interior
    /// may also classify as a wikilink), (b) spans overlapping inline-code
    /// or fence spans — a construct rendered *as code* must stay literal
    /// (turning it into a link inside backticks would print raw brackets) —
    /// and (c) spans overlapping a markdown link/image span: the markdown
    /// link IS the construct there (`[t](#intro)`'s destination classifies
    /// as a tag too); splicing inside it would corrupt the destination that
    /// `AttributedString(markdown:)` is about to parse natively.
    private static func mappableSpans(from spans: [EditorSpan]) -> [EditorSpan] {
        var codeRanges: [(Int, Int)] = []
        for span in spans {
            switch span.kind {
            case .inlineCode, .codeFence, .code, .link, .image:
                codeRanges.append((Int(span.startByte), Int(span.endByte)))
            default:
                break
            }
        }
        let candidates = spans.filter { isMappableKind($0.kind) }
            .sorted {
                $0.startByte != $1.startByte
                    ? $0.startByte < $1.startByte
                    : $0.endByte > $1.endByte  // outermost first at same start
            }
        var kept: [EditorSpan] = []
        var coveredEnd = 0
        for span in candidates {
            let start = Int(span.startByte)
            let end = Int(span.endByte)
            if start < coveredEnd { continue }  // nested in a kept span
            if codeRanges.contains(where: { start < $0.1 && end > $0.0 }) {
                continue  // rendered as code — stays literal
            }
            kept.append(span)
            coveredEnd = end
        }
        return kept
    }

    private static func isMappableKind(_ kind: EditorSpanKind) -> Bool {
        switch kind {
        case .wikilink, .embed, .tag, .citation:
            return true
        default:
            return false
        }
    }

    // MARK: - Run construction

    private static func mapRun(
        kind: EditorSpanKind, spanText: String, citations: [RenderedCitation]
    ) -> MappedRun? {
        switch kind {
        case .wikilink:
            guard let parts = splitWikiBody(spanText, embed: false) else { return nil }
            let display = parts.alias ?? parts.target
            guard
                let url = LegacyRouter.encodedURL(
                    scheme: LegacyRouter.wikiScheme, target: parts.target)
            else { return nil }
            return MappedRun(
                kind: .wiki, display: nonEmpty(display, fallback: parts.target),
                target: parts.target, url: url,
                axLabel: nonEmpty(display, fallback: parts.target))
        case .embed:
            guard let parts = embedParts(fromSpanText: spanText) else { return nil }
            // Alt-text contract (spec §U3-1): alias, else the target's NAME
            // (last path component, anchor stripped) — never empty, so image
            // embeds always carry a non-empty AX label.
            let name = (LegacyRouter.baseTarget(of: parts.target) as NSString)
                .lastPathComponent
            let display = nonEmpty(
                parts.alias ?? name, fallback: nonEmpty(name, fallback: parts.target))
            guard
                let url = LegacyRouter.encodedURL(
                    scheme: LegacyRouter.embedScheme, target: parts.target)
            else { return nil }
            return MappedRun(
                kind: .embed, display: display, target: parts.target, url: url,
                axLabel: display)
        case .tag:
            guard spanText.hasPrefix("#"), spanText.count > 1 else { return nil }
            let name = String(spanText.dropFirst())
            guard
                let url = LegacyRouter.encodedURL(
                    scheme: LegacyRouter.tagScheme, target: name)
            else { return nil }
            return MappedRun(
                kind: .tag, display: spanText, target: name, url: url,
                axLabel: spanText)
        case .citation:
            let match = citations.first { $0.raw == spanText }
            // Reading view shows the RENDERED form when the citation
            // pipeline has it; VoiceOver gets the speech text (Milestone L).
            let display = nonEmpty(match?.visualText ?? "", fallback: spanText)
            let speech = nonEmpty(match?.speechText ?? "", fallback: spanText)
            guard
                let url = LegacyRouter.encodedURL(
                    scheme: LegacyRouter.citeScheme, target: spanText)
            else { return nil }
            return MappedRun(
                kind: .citation, display: display, target: spanText, url: url,
                axLabel: speech)
        default:
            return nil
        }
    }

    /// Split a `[[…]]` / `![[…]]` span's interior into target + alias.
    /// The span BOUNDARY came from the Rust classifier; this only divides a
    /// confirmed interior: first `|` separates alias, anchors stay attached
    /// to the target (the router strips them where a base form is needed).
    private static func splitWikiBody(
        _ spanText: String, embed: Bool
    ) -> (target: String, alias: String?)? {
        var inner = Substring(spanText)
        if embed {
            guard inner.hasPrefix("!") else { return nil }
            inner = inner.dropFirst()
        }
        guard inner.hasPrefix("[["), inner.hasSuffix("]]"), inner.count >= 4 else {
            return nil
        }
        inner = inner.dropFirst(2).dropLast(2)
        guard !inner.isEmpty else { return nil }
        // Whitespace-trim mirrors links.rs (red-team probe: `[[ Missing ]]`
        // and `[[Missing #Anchor]]` styled as RESOLVED because the Swift
        // side kept padding the Rust resolver strips — styling and
        // activation disagreed).
        if let pipe = inner.firstIndex(of: "|") {
            let target = String(inner[inner.startIndex..<pipe])
                .trimmingCharacters(in: .whitespaces)
            let alias = String(inner[inner.index(after: pipe)...])
                .trimmingCharacters(in: .whitespaces)
            guard !target.isEmpty else { return nil }
            return (target, alias.isEmpty ? nil : alias)
        }
        return (String(inner).trimmingCharacters(in: .whitespaces), nil)
    }

    /// Divide a confirmed `.embed` span (`![[…]]`) interior into target +
    /// alias. The ONE home for embed body parsing: the inline embed run
    /// (`mapRun`'s `.embed` branch) and the block-level detector
    /// (`blockEmbedTarget(inSlice:)`) both go through here, so `target` — the
    /// cache-key form (anchors attached, e.g. `Note#Section`) that matches
    /// `AppState.embedTargetKey` — is derived identically on both paths.
    static func embedParts(
        fromSpanText spanText: String
    ) -> (target: String, alias: String?)? {
        splitWikiBody(spanText, embed: true)
    }

    // MARK: - Block-level embed detection

    /// If `slice` is a paragraph that IS a single wikilink embed — nothing but
    /// one `![[…]]` (surrounding whitespace allowed) — return its cache-key
    /// target (the form `AppState.embedTargetKey` composes and
    /// `currentNoteEmbedResolutions` is keyed on). Otherwise `nil`.
    ///
    /// Detection uses the SAME Rust span authority the inline pipeline
    /// consumes (`editorHighlightSpans` composed through `mappableSpans`) —
    /// never a `"![["` string-prefix check (the no-second-classifier
    /// invariant). "Block IS one embed" means: exactly one mappable span, of
    /// kind `.embed`, whose byte range covers every non-whitespace byte of the
    /// slice. A span suppressed by `mappableSpans` (an embed inside inline
    /// code / a fence) never survives to be counted, so those correctly fail
    /// detection and stay literal.
    ///
    /// Scope (pinned, #511): only WIKILINK embeds (`.embed` kind) expand in
    /// place. Markdown image embeds (`![alt](x.png)`) classify as `.image`,
    /// not `.embed`, so they never reach here and keep their current inline
    /// behavior — follow-up if in-place markdown-image rendering is wanted.
    static func blockEmbedTarget(inSlice slice: String) -> String? {
        let spans = editorHighlightSpans(text: slice) + markdownLinkSpans(text: slice)
        let mappable = mappableSpans(from: spans)
        // Exactly one mappable construct, and it must be an embed.
        guard mappable.count == 1, mappable[0].kind == .embed else { return nil }
        let span = mappable[0]

        let utf8 = Array(slice.utf8)
        let start = Int(span.startByte)
        let end = Int(span.endByte)
        guard start >= 0, end <= utf8.count, start < end else { return nil }

        // The span must cover every non-whitespace byte: any authored
        // character outside the embed (before, between, after) makes this a
        // mid-paragraph embed, which keeps the inline link-run path.
        for i in 0..<start where !isAsciiWhitespaceByte(utf8[i]) { return nil }
        for i in end..<utf8.count where !isAsciiWhitespaceByte(utf8[i]) { return nil }

        let spanText = String(decoding: utf8[start..<end], as: UTF8.self)
        guard let parts = embedParts(fromSpanText: spanText) else { return nil }
        return parts.target
    }

    /// Markdown/CommonMark whitespace: space, tab, newline, carriage return,
    /// form feed, vertical tab. Byte-level test — every one is single-byte in
    /// UTF-8, so scanning raw bytes can't split a multibyte scalar.
    private static func isAsciiWhitespaceByte(_ byte: UInt8) -> Bool {
        switch byte {
        case 0x20, 0x09, 0x0A, 0x0D, 0x0C, 0x0B: return true
        default: return false
        }
    }

    // MARK: - Styling

    /// #849: does this routing URL activate as a wiki link the router would
    /// announce as unresolved? Match keys come from
    /// `LegacyRouter.candidateKeys` — the SAME ordered list the live
    /// router matches records with — and are case-SENSITIVE exactly like
    /// the router's `targetRaw ==` compare, so styling and activation can
    /// never disagree about the same run. With `recordSets` supplied
    /// (the production path), a run whose target has NO record of ITS
    /// OWN GRAMMAR classifies unresolved: that run ACTIVATES as
    /// "unresolved. Cannot open." — live-buffer links the saved index
    /// hasn't seen — so accent styling would lie, and a record of the
    /// OTHER grammar must never vouch for it (Codex round 3: the sets
    /// are kind-partitioned exactly like the router's record match).
    static func isUnresolvedWikiLink(
        _ link: URL, unresolvedTargets: Set<String>,
        recordSets: LegacyRouter.LinkRecordSets? = nil
    ) -> Bool {
        guard
            case .wiki(let target, let grammar) =
                LegacyRouter.disposition(for: link)
        else { return false }
        let keys = LegacyRouter.candidateKeys(for: target, grammar: grammar)
        if let recordSets {
            // Full classification: the first key with a SAME-GRAMMAR
            // record decides.
            let known = recordSets.known(for: grammar)
            for key in keys where known.contains(key) {
                return recordSets.unresolved(for: grammar).contains(key)
            }
            return true
        }
        // Membership-only (legacy/test callers without record sets).
        return keys.contains { unresolvedTargets.contains($0) }
    }

    private static func style(
        _ attributed: inout AttributedString, runs: [MappedRun],
        unresolvedTargets: Set<String> = [],
        recordSets: LegacyRouter.LinkRecordSets? = nil
    ) {
        // Snapshot the link runs before mutating: rewriting/removing `.link`
        // re-segments the run collection, so never mutate while iterating it.
        // Attribute-only mutation moves no characters, so the captured ranges
        // stay valid throughout.
        let linkRuns: [(range: Range<AttributedString.Index>, link: URL)] =
            attributed.runs.compactMap { run in
                run.link.map { (run.range, $0) }
            }
        for (range, link) in linkRuns {
            // The URL the run will ACTIVATE with — reassigned by the
            // internal-markdown rewrite below, so the #849 unresolved
            // check always sees the routed (slate-wiki) form.
            var effectiveLink = link
            if case .discard = LegacyRouter.disposition(for: link) {
                if let rewritten = internalMarkdownDestination(link) {
                    // Scheme-less markdown destination: Slate semantics are
                    // vault-rooted/basename (never source-relative) — the
                    // exact string `links.rs` records as `target_raw`. Route
                    // it like a wikilink, so activation resolves through the
                    // note's own link records and announces when unresolved.
                    attributed[range][
                        AttributeScopes.FoundationAttributes.LinkAttribute.self
                    ] = rewritten
                    effectiveLink = rewritten
                } else {
                    // file: / javascript: / unknown schemes, protocol-relative
                    // `//host`, fragment-only `#anchor`: never activatable
                    // (the router would drop the click silently). Remove the
                    // link attribute so there is no dead affordance — visually
                    // or to VoiceOver.
                    attributed[range][
                        AttributeScopes.FoundationAttributes.LinkAttribute.self
                    ] = nil
                    continue
                }
            }
            // Every surviving link — slate-scheme or external — gets the
            // accent + underline treatment: same affordance for every
            // activatable run, and underline keeps the cue non-color-only
            // (WCAG 1.4.1). `accentText` on `surface` is an existing gated
            // pairing in `Tokens.contrastPairings`. Explicit attribute keys:
            // the dynamic-member spellings are ambiguous between the SwiftUI
            // and AppKit attribute scopes.
            //
            // #849: a wiki run the router would refuse to open renders in
            // `warningText` (also a gated pairing on `surface` — the U5-3
            // editor treatment) so the unresolved STATE is visible before
            // activation, with an AX custom-text suffix for parity; the
            // underline stays — it marks "activatable" (activation still
            // announces "unresolved. Cannot open."), and dropping it would
            // make the state color-only.
            let unresolved = isUnresolvedWikiLink(
                effectiveLink, unresolvedTargets: unresolvedTargets,
                recordSets: recordSets)
            attributed[range][
                AttributeScopes.SwiftUIAttributes.ForegroundColorAttribute.self
            ] = unresolved ? Tokens.ColorRole.warningText : Tokens.ColorRole.accentText
            attributed[range][
                AttributeScopes.SwiftUIAttributes.UnderlineStyleAttribute.self
            ] = Text.LineStyle(pattern: .solid)
            if unresolved {
                attributed[range][
                    AttributeScopes.AccessibilityAttributes.TextCustomAttribute.self
                ] = ["Unresolved link"]
            }
            if link.scheme?.lowercased() == LegacyRouter.citeScheme,
                let mapped = runs.first(where: { $0.url == link })
            {
                attributed[range][
                    AttributeScopes.AccessibilityAttributes.TextCustomAttribute.self
                ] = [mapped.axLabel]
            }
        }
    }

    /// A scheme-less markdown destination the vault could resolve, rewritten
    /// to the wiki routing scheme — or nil when the URL is not an internal
    /// note reference. Mirrors `links.rs::looks_external`: anything with a
    /// scheme, a protocol-relative `//host`, or a fragment-only `#anchor` is
    /// NOT internal (fragment-only heading navigation inside the open note is
    /// out of reading-v1 scope, so those lose the affordance rather than
    /// dead-clicking).
    private static func internalMarkdownDestination(_ link: URL) -> URL? {
        guard link.scheme == nil else { return nil }
        // The authored destination text, VERBATIM — Slate never
        // percent-decodes markdown destinations (`target_raw` stores them
        // literally), so no decoding happens here either.
        let raw = link.absoluteString
        guard !raw.isEmpty, !raw.hasPrefix("//"), !raw.hasPrefix("#") else {
            return nil
        }
        // Grammar-retaining scheme (Codex round 2): the router must
        // apply MARKDOWN anchor-cut rules to this destination, never
        // the wikilink ones — `^` stays in the path here.
        return LegacyRouter.encodedURL(
            scheme: LegacyRouter.wikiMarkdownScheme, target: raw)
    }

    // MARK: - Small helpers

    /// Backslash-escape ASCII punctuation for a markdown link label so a
    /// display text containing `]`, `*`, `` ` ``, … can't break out of the
    /// label context. CommonMark treats a backslash before ASCII punctuation
    /// as an escape (and ONLY there — hence the punctuation check). Newlines
    /// can't appear inside an inline link label; degrade them to spaces.
    static func escapeMarkdownLabel(_ text: String) -> String {
        var out = ""
        out.reserveCapacity(text.count)
        for ch in text {
            if ch == "\n" || ch == "\r" {
                out.append(" ")
                continue
            }
            if ch.isASCII, Self.asciiPunctuation.contains(ch) {
                out.append("\\")
            }
            out.append(ch)
        }
        return out
    }

    private static let asciiPunctuation = Set("!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~")

    private static func nonEmpty(_ value: String, fallback: String) -> String {
        value.isEmpty ? fallback : value
    }
}
