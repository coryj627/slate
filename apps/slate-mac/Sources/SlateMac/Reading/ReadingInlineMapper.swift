// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Accessibility
import Foundation
import SwiftUI

/// The reading view's inline pipeline (U3-1, #465): one paragraph-family
/// source slice in, one styled `AttributedString` out.
///
/// Steps (spec §U3-1):
///  1. **Pre-process** — wikilinks / embeds / tags / citations are replaced
///     by markdown links carrying the `ReadingLinkRouter` schemes. WHERE
///     those constructs are is decided by `editorHighlightSpans` (the
///     canonical Rust span classifier — escapes, code spans, and balanced-
///     bracket rules live there once; this file never re-derives syntax,
///     only splits a confirmed span's interior).
///  2. **Parse** — `AttributedString(markdown:)` with
///     `.inlineOnlyPreservingWhitespace`, so authored bold / italic /
///     inline-code / external links render and newlines survive.
///  3. **Style** — every link run gets `Tokens.ColorRole.accentText` +
///     underline (link affordance not conveyed by color alone — WCAG 1.4.1).
///     Citation runs additionally carry their **speech text** (the Milestone
///     L contract, same `RenderedCitation.speechText` source CitationsPanel
///     uses) via `accessibilityTextCustom` — the public per-range AX-text
///     attribute; there is no public per-run label *substitution* API, so
///     activation (the CitationPopover) remains the full speech surface.
///
/// Pure and synchronous — everything arrives via parameters, so tests pin
/// the run model (`MappedRun`) without rendering.
enum ReadingInlineMapper {

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
    static func map(slice: String, citations: [RenderedCitation] = []) -> Mapped {
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

        style(&attributed, runs: runs)
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
                let url = ReadingLinkRouter.encodedURL(
                    scheme: ReadingLinkRouter.wikiScheme, target: parts.target)
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
            let name = (ReadingLinkRouter.baseTarget(of: parts.target) as NSString)
                .lastPathComponent
            let display = nonEmpty(
                parts.alias ?? name, fallback: nonEmpty(name, fallback: parts.target))
            guard
                let url = ReadingLinkRouter.encodedURL(
                    scheme: ReadingLinkRouter.embedScheme, target: parts.target)
            else { return nil }
            return MappedRun(
                kind: .embed, display: display, target: parts.target, url: url,
                axLabel: display)
        case .tag:
            guard spanText.hasPrefix("#"), spanText.count > 1 else { return nil }
            let name = String(spanText.dropFirst())
            guard
                let url = ReadingLinkRouter.encodedURL(
                    scheme: ReadingLinkRouter.tagScheme, target: name)
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
                let url = ReadingLinkRouter.encodedURL(
                    scheme: ReadingLinkRouter.citeScheme, target: spanText)
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
        if let pipe = inner.firstIndex(of: "|") {
            let target = String(inner[inner.startIndex..<pipe])
            let alias = String(inner[inner.index(after: pipe)...])
            guard !target.isEmpty else { return nil }
            return (target, alias.isEmpty ? nil : alias)
        }
        return (String(inner), nil)
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

    private static func style(_ attributed: inout AttributedString, runs: [MappedRun]) {
        // Snapshot the link runs before mutating: rewriting/removing `.link`
        // re-segments the run collection, so never mutate while iterating it.
        // Attribute-only mutation moves no characters, so the captured ranges
        // stay valid throughout.
        let linkRuns: [(range: Range<AttributedString.Index>, link: URL)] =
            attributed.runs.compactMap { run in
                run.link.map { (run.range, $0) }
            }
        for (range, link) in linkRuns {
            if case .discard = ReadingLinkRouter.disposition(for: link) {
                if let rewritten = internalMarkdownDestination(link) {
                    // Scheme-less markdown destination: Slate semantics are
                    // vault-rooted/basename (never source-relative) — the
                    // exact string `links.rs` records as `target_raw`. Route
                    // it like a wikilink, so activation resolves through the
                    // note's own link records and announces when unresolved.
                    attributed[range][
                        AttributeScopes.FoundationAttributes.LinkAttribute.self
                    ] = rewritten
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
            attributed[range][
                AttributeScopes.SwiftUIAttributes.ForegroundColorAttribute.self
            ] = Tokens.ColorRole.accentText
            attributed[range][
                AttributeScopes.SwiftUIAttributes.UnderlineStyleAttribute.self
            ] = Text.LineStyle(pattern: .solid)
            if link.scheme?.lowercased() == ReadingLinkRouter.citeScheme,
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
        return ReadingLinkRouter.encodedURL(
            scheme: ReadingLinkRouter.wikiScheme, target: raw)
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
