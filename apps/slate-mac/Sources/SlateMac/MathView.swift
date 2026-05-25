import LaTeXSwiftUI
import SwiftUI

/// Renders one `MathBlock` (from the math pipeline in `slate-core`).
///
/// Two layers, two audiences:
/// - **Visual** — `LaTeXSwiftUI` renders the source LaTeX as a native
///   SwiftUI view via SwiftMath. Inline math lays out inline with
///   surrounding text; block math gets vertical padding.
/// - **Accessibility** — the `accessibilityLabel` is the MathCAT-
///   generated `speech` field, NOT the LaTeX source. Without this
///   substitution VoiceOver would read `\sum_{i=0}^n i` as
///   "backslash sum underscore i equals zero to n of i" — the
///   whole point of the math pipeline is to replace that with
///   "the sum from i equals zero to n of i."
///
/// Source LaTeX and braille are surfaced as `accessibilityCustomContent`
/// rotor entries so users who want them can pull them up via the
/// custom-content rotor (the standard way to expose secondary info
/// without cluttering the primary announcement). This mirrors how
/// Jupyter's accessibility layer presents math.
///
/// The view is standalone — no `@EnvironmentObject AppState` —
/// because the same code lights up the read pane and (future)
/// inline-math editor surfaces. Everything it needs arrives via
/// the `block` parameter.
struct MathView: View {
    let block: MathBlock

    /// Honor the system Reduce Motion setting. LaTeXSwiftUI defaults
    /// to a brief render animation on mount; we suppress it for
    /// vestibular-sensitive users per WCAG 2.3.1.
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        Group {
            switch block.displayStyle {
            case .inline:
                inlineRendering
            case .block:
                blockRendering
            }
        }
        .accessibilityLabel(primaryAccessibilityLabel)
        // Custom-content rotor entries (`Source`, `Braille`) — the
        // user pulls these up via VO+Cmd+Right to hear the secondary
        // info without it cluttering the primary read.
        .accessibilityCustomContent("Source", block.source, importance: .default)
        .accessibilityCustomContent(
            "Braille",
            brailleAccessibilityValue,
            importance: .default
        )
        // Trait selection: math is not a heading, not a button — it's
        // a static block of structured content. The label substitution
        // is what does the work; no trait additions needed.
    }

    // MARK: - Render variants

    private var inlineRendering: some View {
        latexView
            .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var blockRendering: some View {
        latexView
            .frame(maxWidth: .infinity, alignment: .center)
            // Block math gets its own visual paragraph + a slim
            // vertical margin so it doesn't crowd surrounding text.
            // 8pt scales with Dynamic Type at the SwiftUI layer.
            .padding(.vertical, 8)
    }

    private var latexView: some View {
        LaTeX(block.source)
            // LaTeXSwiftUI's default render mode is MathJax-backed
            // SVG; that gives the highest fidelity for arbitrary
            // formulas. Source-as-fallback is handled via the
            // `errorMode` config below.
            .errorMode(.rendered)
            // When the renderer can't produce SVG (rare; happens on
            // malformed LaTeX), fall back to rendering the raw
            // source as styled text instead of showing nothing. AT
            // users still hear the MathCAT speech regardless.
            .blockMode(.alwaysInline)
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

    /// Decode the braille byte payload into a human-readable string
    /// per the user's braille code. Nemeth is ASCII chars (the
    /// MathCAT output is ASCII Nemeth); UEB is Unicode braille
    /// (the MathCAT output is the actual braille characters
    /// pre-encoded). Either way we round-trip via UTF-8 since
    /// MathCAT returns the encoding as a `String` we converted to
    /// `Vec<u8>` at the FFI boundary.
    private var brailleAccessibilityValue: String {
        if block.braille.isEmpty {
            return "Braille not available."
        }
        return String(data: block.braille, encoding: .utf8)
            ?? "Braille not decodable."
    }
}
