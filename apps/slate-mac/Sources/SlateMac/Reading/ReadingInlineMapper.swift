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
        let spans = editorHighlightSpans(text: slice)
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
    /// may also classify as a wikilink) and (b) spans overlapping inline-code
    /// or fence spans — a construct rendered *as code* must stay literal
    /// (turning it into a link inside backticks would print raw brackets).
    private static func mappableSpans(from spans: [EditorSpan]) -> [EditorSpan] {
        var codeRanges: [(Int, Int)] = []
        for span in spans {
            switch span.kind {
            case .inlineCode, .codeFence, .code:
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
            guard let parts = splitWikiBody(spanText, embed: true) else { return nil }
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

    // MARK: - Styling

    private static func style(_ attributed: inout AttributedString, runs: [MappedRun]) {
        for run in attributed.runs {
            guard let link = run.link else { continue }
            // Every link — slate-scheme or external — gets the accent +
            // underline treatment: same affordance for every activatable run,
            // and underline keeps the cue non-color-only (WCAG 1.4.1).
            // `accentText` on `surface` is an existing gated pairing in
            // `Tokens.contrastPairings`. Explicit attribute keys: the
            // dynamic-member spellings are ambiguous between the SwiftUI and
            // AppKit attribute scopes.
            attributed[run.range][
                AttributeScopes.SwiftUIAttributes.ForegroundColorAttribute.self
            ] = Tokens.ColorRole.accentText
            attributed[run.range][
                AttributeScopes.SwiftUIAttributes.UnderlineStyleAttribute.self
            ] = Text.LineStyle(pattern: .solid)
            if link.scheme?.lowercased() == ReadingLinkRouter.citeScheme,
                let mapped = runs.first(where: { $0.url == link })
            {
                attributed[run.range][
                    AttributeScopes.AccessibilityAttributes.TextCustomAttribute.self
                ] = [mapped.axLabel]
            }
        }
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
