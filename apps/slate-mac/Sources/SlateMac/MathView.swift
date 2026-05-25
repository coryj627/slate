// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import LaTeXSwiftUI
import SwiftUI

/// Renders one `MathBlock` (from the math pipeline in `slate-core`).
///
/// Two layers, two audiences:
/// - **Visual** — `LaTeXSwiftUI` renders the source LaTeX as a native
///   SwiftUI view via SwiftMath. Inline math sizes to its own
///   content (no greedy width) so it flows inside the surrounding
///   sentence in callers that compose it that way; block math gets
///   vertical padding and centers.
/// - **Accessibility** — the `accessibilityLabel` is the MathCAT-
///   generated `speech` field, NOT the LaTeX source. Without this
///   substitution VoiceOver would read `\sum_{i=0}^n i` as
///   "backslash sum underscore i equals zero to n of i" — the
///   whole point of the math pipeline is to replace that with
///   "the sum from i equals zero to n of i."
///
/// `.accessibilityElement(children: .ignore)` is the critical wire
/// (audit #250 H1): LaTeXSwiftUI emits the rendered formula as
/// nested `Text` children that include the raw LaTeX source. Without
/// the `.ignore`, VoiceOver users can drill into those children and
/// hear the raw LaTeX *after* the speech label — which silently
/// undermines the math pipeline's whole purpose. We declare the
/// view as a single accessibility element and authoritatively own
/// the label.
///
/// Source LaTeX and braille are surfaced as `accessibilityCustomContent`
/// entries so users who want them can pull them up via the rotor
/// (or in macOS verbose mode, hear them announced after the label).
/// This mirrors how Jupyter's accessibility layer presents math.
///
/// The view is standalone — no `@EnvironmentObject AppState` —
/// because the same code lights up the read pane and (future)
/// inline-math editor surfaces. Everything it needs arrives via
/// the `block` parameter.
struct MathView: View {
    let block: MathBlock

    /// Dynamic-Type-aware vertical padding for block math (audit
    /// #250 M2). Raw `.padding(.vertical, 8)` is a fixed CGFloat
    /// that does NOT scale with system text size; @ScaledMetric
    /// re-scales the constant against the user's chosen size,
    /// matching the surrounding body text's growth.
    @ScaledMetric(relativeTo: .body) private var blockPadding: CGFloat = 8

    var body: some View {
        Group {
            switch block.displayStyle {
            case .inline:
                inlineRendering
            case .block:
                blockRendering
            }
        }
        // Audit #250 H1: own the accessibility surface authoritatively.
        // LaTeXSwiftUI's internal Text children include the raw LaTeX
        // source; without `.ignore`, VoiceOver can drill into them
        // and read the source after our speech label, exactly the
        // failure the math pipeline exists to prevent.
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(primaryAccessibilityLabel)
        // Custom-content entries (`Source`, `Braille`). macOS
        // announces these after the label in verbose mode or via
        // VO+Shift+Down (read-all). They are NOT reached via a
        // dedicated macOS rotor key — that's an iOS affordance.
        .accessibilityCustomContent("Source", sourceAccessibilityValue, importance: .default)
        .accessibilityCustomContent(
            "Braille",
            brailleAccessibilityValue,
            importance: .default
        )
        // Audit #250 L1 (WCAG 2.5.3): voice-control users see the
        // rendered LaTeX glyphs; the AT label is the speech form.
        // Expose the source as a tooltip so a Voice Control user
        // can reach it via "show numbers"/hover, and so a mouse
        // user can confirm what they're looking at.
        .help(block.source)
    }

    // MARK: - Render variants

    /// Inline math: size to content so the LaTeX view can flow
    /// inside a host's `HStack` / `Text` interpolation. The earlier
    /// shape used `.frame(maxWidth: .infinity, alignment: .leading)`
    /// which greedily consumed the row width and broke "Let $x = 1$
    /// and consider…" into three rows (audit #250 H3).
    private var inlineRendering: some View {
        latexView
    }

    private var blockRendering: some View {
        latexView
            .frame(maxWidth: .infinity, alignment: .center)
            .padding(.vertical, blockPadding)
    }

    private var latexView: some View {
        LaTeX(block.source)
            // SwiftMath-backed render; on parser failure render the
            // source as styled text instead of vanishing.
            .errorMode(.rendered)
            // LaTeXSwiftUI's default `renderingAnimation` is `.none`
            // already (verified against the dep), so no Reduce
            // Motion plumbing is needed today. If the upstream
            // ever introduces an animated mount, replace `.none`
            // here with `reduceMotion ? nil : .default` and add
            // back an `@Environment(\.accessibilityReduceMotion)`.
            .renderingAnimation(.none)
    }

    // MARK: - Accessibility helpers

    /// Primary AT label. Always prefers the MathCAT speech; degrades
    /// gracefully when MathCAT couldn't produce one so we never read
    /// an empty string (which would land as VoiceOver's "untitled").
    private var primaryAccessibilityLabel: String {
        let trimmed = block.speech.trimmingCharacters(in: .whitespacesAndNewlines)
        if trimmed.isEmpty {
            // Last-resort fallback — keeps the AT story intact even
            // when MathCAT init fails. The backend now emits a
            // typed message in this case (e.g. "Math expression too
            // large…"), but a fully-empty speech can still happen if
            // the source itself is empty. "Math expression" is the
            // shortest unambiguous label.
            return "Math expression."
        }
        return trimmed
    }

    /// Source value for the `Source` rotor entry. Empty source
    /// degrades to a "not available" message instead of an empty
    /// rotor entry (audit #250 L2).
    private var sourceAccessibilityValue: String {
        let trimmed = block.source.trimmingCharacters(in: .whitespacesAndNewlines)
        if trimmed.isEmpty {
            return "Source not available."
        }
        return trimmed
    }

    /// Decode the braille byte payload into a human-readable string
    /// per the user's braille code. Nemeth is ASCII Nemeth chars;
    /// UEB is Unicode braille — MathCAT emits the right encoding
    /// based on the user's preference, and we round-trip via UTF-8
    /// since the FFI carries the encoding as bytes.
    private var brailleAccessibilityValue: String {
        if block.braille.isEmpty {
            return "Braille not available."
        }
        return String(data: block.braille, encoding: .utf8)
            ?? "Braille not decodable."
    }
}
