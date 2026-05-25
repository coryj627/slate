import AppKit
import SwiftDraw
import SwiftUI

/// Renders one `DiagramBlock` from the Mermaid pipeline.
///
/// Two layers, two audiences:
/// - **Visual** — when the backend produced an SVG and the render
///   status is `Ok`, we decode the SVG to an `NSImage` via SwiftDraw
///   and display it through `Image(nsImage:)`. Width is capped at
///   the parent's container so wide flowcharts don't blow the
///   layout.
/// - **Accessibility** — the container's `accessibilityLabel` is
///   ALWAYS the backend-generated `structuredDescription`, never
///   "image" or "SVG". On render failure the structured description
///   is still populated (the backend's contract — see `diagram.rs`),
///   so AT users always hear what the diagram is regardless of
///   whether the visual rendered.
///
/// Source Mermaid is surfaced as an `accessibilityCustomContent`
/// rotor entry so users who want the raw text can reach it without
/// it bloating the primary announcement. Same pattern MathView uses.
///
/// Standalone — no AppState. The same view lights up the read pane
/// today and (future) zoom / share surfaces.
struct MermaidView: View {
    let block: DiagramBlock

    /// Dynamic-Type-aware max height for the rendered SVG (audit
    /// #254 M1). The SVG glyphs themselves don't reflow at Dynamic
    /// Type, but scaling the container's height cap with the user's
    /// text size keeps the diagram large enough to read on the same
    /// terms as surrounding text.
    @ScaledMetric(relativeTo: .body) private var maxImageHeight: CGFloat = 600

    var body: some View {
        Group {
            switch block.renderStatus {
            case .ok:
                renderedSvg
            case .unsupportedDialect(let reason):
                failureFallback(reason: reason, kind: "unsupported")
            case .renderFailed(let message):
                failureFallback(reason: message, kind: "failed")
            }
        }
        // Audit #254 H1: the outer `.ignore` previously stranded the
        // failure-path source ScrollView from AT (visible to sighted
        // users, invisible to VO). Switched to `.contain` so the
        // outer container carries the label authoritatively but
        // children remain reachable. The SVG `Image` itself opts out
        // via `.accessibilityHidden(true)` (SwiftDraw's inner nodes
        // are noise); the failure-path Text remains reachable so VO
        // can read the source if the user drills in.
        .accessibilityElement(children: .contain)
        .accessibilityLabel(primaryAccessibilityLabel)
        // Source rotor entry — reachable through verbose AT mode or
        // VO+Shift+Down; matches MathView's pattern.
        .accessibilityCustomContent(
            "Source",
            sourceAccessibilityValue,
            importance: .default
        )
        // WCAG 2.5.3 (Label in Name): voice-control users see the
        // rendered diagram, not the structured description. Tooltip
        // exposes a SHORT preview of the source (audit #254 M2 —
        // full source as a tooltip becomes a wall of text that
        // obscures other UI under 1.4.13).
        .help(tooltipPreview)
    }

    // MARK: - Render variants

    @ViewBuilder
    private var renderedSvg: some View {
        if let svgData = block.svg, !svgData.isEmpty, let nsImage = decodeSvg(svgData) {
            Image(nsImage: nsImage)
                .resizable()
                .aspectRatio(contentMode: .fit)
                // Audit #254 M1: max-height scales with Dynamic
                // Type so diagrams stay legible at large text sizes.
                .frame(maxWidth: .infinity, maxHeight: maxImageHeight)
                // The container owns the AT label via
                // `.accessibilityLabel(structuredDescription)` +
                // `.accessibilityElement(children: .contain)`. Hide
                // the Image so VO doesn't announce "image" and the
                // static analyzer doesn't trip on a label-less
                // image (the structured description IS the label
                // at the container level).
                .accessibilityHidden(true)
        } else {
            // Backend said Ok but didn't produce SVG / SwiftDraw
            // couldn't decode it. Treat as a soft render failure so
            // the user gets the source fallback rather than an
            // empty box.
            failureFallback(reason: "diagram rendered but image could not be decoded", kind: "failed")
        }
    }

    /// Shared visual fallback for both UnsupportedDialect and
    /// RenderFailed. Renders a typed marker plus the source so the
    /// user can still read what was authored.
    private func failureFallback(reason: String, kind: String) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(failureHeaderText(kind: kind))
                .font(.callout.weight(.semibold))
                .foregroundStyle(.secondary)
            if !reason.isEmpty {
                Text(reason)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            // Preformatted source so the failed-render path still
            // gives the user something to read. Monospaced + selectable
            // matches what code-block fallback users expect.
            ScrollView(.horizontal, showsIndicators: true) {
                Text(block.source)
                    .font(.system(.body, design: .monospaced))
                    .textSelection(.enabled)
            }
        }
        .padding(8)
        .background(
            RoundedRectangle(cornerRadius: 4)
                .fill(Color.secondary.opacity(0.08))
        )
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func failureHeaderText(kind: String) -> String {
        switch kind {
        case "unsupported":
            return "Diagram dialect not supported"
        default:
            return "Diagram could not be rendered"
        }
    }

    // MARK: - Decoding

    /// SVG `Data` → `NSImage` via SwiftDraw. Returns nil on decode
    /// failure (covered by the soft-failure branch above).
    ///
    /// Important: the unlabeled `NSImage(data)` form is load-bearing.
    /// AppKit's stock initializer is `init?(data: Data)` — labeled.
    /// SwiftDraw's convenience init is `init?(_ data: Data)` —
    /// unlabeled. A future refactor that changes `data` → `data:
    /// data` would silently flip to AppKit and produce a blank
    /// (non-SVG) NSImage without test signal. Keep the call form
    /// unlabeled (audit #254 L3).
    private func decodeSvg(_ data: Data) -> NSImage? {
        NSImage(data)
    }

    // MARK: - Tooltip preview

    /// Truncated source preview for the `.help` tooltip. Full
    /// source is reachable via the `Source` rotor entry, so the
    /// tooltip can stay short. Limits to ~120 chars / 3 lines.
    /// Audit #254 M2 (WCAG 1.4.13 Content on Hover or Focus).
    var tooltipPreview: String {
        let maxChars = 120
        let lines = block.source.split(
            separator: "\n",
            omittingEmptySubsequences: false
        )
        let firstFew = lines.prefix(3).joined(separator: " ")
        if firstFew.count <= maxChars && lines.count <= 3 {
            return firstFew
        }
        let truncated = String(firstFew.prefix(maxChars))
        return truncated + "…"
    }

    // MARK: - Accessibility helpers

    /// Primary AT label. Always prefers the backend's structured
    /// description; degrades to a never-empty fallback so VoiceOver
    /// doesn't land on "untitled".
    private var primaryAccessibilityLabel: String {
        let trimmed = block.structuredDescription.trimmingCharacters(
            in: .whitespacesAndNewlines
        )
        if !trimmed.isEmpty {
            return trimmed
        }
        // Backend contract is to always populate structured
        // description; this is a defensive guard if the FFI ever
        // hands us an empty one.
        return "Mermaid diagram."
    }

    private var sourceAccessibilityValue: String {
        let trimmed = block.source.trimmingCharacters(in: .whitespacesAndNewlines)
        if trimmed.isEmpty {
            return "Source not available."
        }
        return trimmed
    }
}
