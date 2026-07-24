// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Accessibility
import Foundation
import SwiftUI

/// The reading view's inline **applier** (U3-1 #465 · #967).
///
/// One core-computed `ReadingInlineSegment` in, one styled
/// `AttributedString` out. Everything semantic — which bytes are a
/// wikilink / embed / tag / citation, how a target splits, what a run's
/// display text is, whether a link resolves, what its accessible text is
/// — is decided once in `slate-core`
/// (`reading_inline_segments_source`) and consumed identically by every
/// host (program decisions 4/5; executable spec
/// `docs/plans/18_windows_port/specs/w3_inline_runs_spec.md`).
///
/// **What this file may contain, and nothing else:** attribute
/// application, URL construction from a run kind via the router's
/// schemes, and the presentation policy below. The selection sweep,
/// interior splitting, markdown splicing, `AttributedString(markdown:)`
/// re-parse, destination rewriting, unresolved classification, and
/// accessible-string composition that used to live here are **deleted**,
/// not left as a fallback — they were the drift pocket #967 exists to
/// retire.
///
/// **Presentation policy (unchanged behavior, moved intact):**
///  - Every activatable run gets `Tokens.ColorRole.accentText` **plus**
///    underline — the affordance is never conveyed by colour alone
///    (WCAG 1.4.1).
///  - A wikilink run core reports as unresolved renders in
///    `warningText`, keeping the underline: the state is visible BEFORE
///    activation (#849), and activation still announces "unresolved.
///    Cannot open."
///  - `run.axText` (citation speech text, or "Unresolved link") is
///    stamped via `accessibilityTextCustom` — the public per-range
///    AX-text attribute. The strings come from core; this file never
///    composes one.
enum ReadingInlineMapper {

    /// Apply one core segment's runs. Pure and synchronous — everything
    /// arrives as a parameter, so tests pin the attribute model without
    /// rendering.
    static func attributed(_ segment: ReadingInlineSegment) -> AttributedString {
        // Runs carry UTF-8 byte offsets into `content` and partition it
        // exactly (core census: no gaps, no overlaps, char-aligned), so
        // appending run by run reproduces `content` verbatim.
        let utf8 = Array(segment.content.utf8)
        var result = AttributedString()
        for run in segment.runs {
            let start = Int(run.start)
            let end = Int(run.end)
            guard start >= 0, end <= utf8.count, start < end else { continue }
            var piece = AttributedString(
                String(decoding: utf8[start..<end], as: UTF8.self))

            let intent = presentationIntent(run.styles)
            if !intent.isEmpty {
                piece[
                    AttributeScopes.FoundationAttributes
                        .InlinePresentationIntentAttribute.self
                ] = intent
            }

            if let url = activationURL(for: run.kind) {
                piece[
                    AttributeScopes.FoundationAttributes.LinkAttribute.self
                ] = url
                piece[
                    AttributeScopes.SwiftUIAttributes.ForegroundColorAttribute.self
                ] = isUnresolved(run.kind)
                    ? Tokens.ColorRole.warningText : Tokens.ColorRole.accentText
                piece[
                    AttributeScopes.SwiftUIAttributes.UnderlineStyleAttribute.self
                ] = Text.LineStyle(pattern: .solid)
            }

            if let axText = run.axText {
                piece[
                    AttributeScopes.AccessibilityAttributes.TextCustomAttribute.self
                ] = [axText]
            }

            result.append(piece)
        }
        return result
    }

    /// The routing URL one run activates with, or nil for a run that
    /// carries no affordance. `Text` runs include the labels of
    /// never-activatable destinations (`file:` / `javascript:` / unknown
    /// schemes, protocol-relative `//host`, fragment-only `#anchor`),
    /// which core already degraded so nothing renders as activatable and
    /// then dead-clicks.
    static func activationURL(for kind: ReadingInlineRunKind) -> URL? {
        switch kind {
        case .text:
            return nil
        case .externalLink(let url):
            return URL(string: url)
        case .wikilink(let target, _, _, let grammar, _):
            // The grammar rides its own scheme so the router applies the
            // right anchor-cut rules on activation — `^` is an anchor
            // marker in wikilink grammar but a path character in a
            // markdown destination (Codex round 2).
            let scheme =
                grammar == .wikilink
                ? ReadingLinkRouter.wikiScheme : ReadingLinkRouter.wikiMarkdownScheme
            return ReadingLinkRouter.encodedURL(scheme: scheme, target: target)
        case .embed(let key):
            return ReadingLinkRouter.encodedURL(
                scheme: ReadingLinkRouter.embedScheme, target: key)
        case .tag(let name):
            return ReadingLinkRouter.encodedURL(
                scheme: ReadingLinkRouter.tagScheme, target: name)
        case .citation(let raw, _):
            return ReadingLinkRouter.encodedURL(
                scheme: ReadingLinkRouter.citeScheme, target: raw)
        }
    }

    /// #849: does this run render in the unresolved treatment? Core
    /// decided it — the host never re-classifies.
    static func isUnresolved(_ kind: ReadingInlineRunKind) -> Bool {
        if case .wikilink(_, _, _, _, let resolved) = kind {
            return !resolved
        }
        return false
    }

    private static func presentationIntent(
        _ styles: [ReadingInlineStyle]
    ) -> InlinePresentationIntent {
        var intent: InlinePresentationIntent = []
        for style in styles {
            switch style {
            case .emphasis: intent.insert(.emphasized)
            case .strong: intent.insert(.stronglyEmphasized)
            case .strikethrough: intent.insert(.strikethrough)
            case .inlineCode: intent.insert(.code)
            }
        }
        return intent
    }
}
