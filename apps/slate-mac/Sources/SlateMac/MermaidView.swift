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
        // Own the AT surface authoritatively. SVGKit / SwiftDraw
        // emit inner image nodes whose accessibility tree (auto-
        // generated IDs, glyph descriptions) is noise — VO would
        // hear "image" or random SVG group labels before our
        // structured description if we didn't take charge.
        .accessibilityElement(children: .ignore)
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
        // exposes a hint of the source for hover + Voice Control
        // "show numbers" coverage.
        .help(block.source)
    }

    // MARK: - Render variants

    @ViewBuilder
    private var renderedSvg: some View {
        if let svgData = block.svg, !svgData.isEmpty, let nsImage = decodeSvg(svgData) {
            Image(nsImage: nsImage)
                .resizable()
                .aspectRatio(contentMode: .fit)
                .frame(maxWidth: .infinity, maxHeight: 600)
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
    private func decodeSvg(_ data: Data) -> NSImage? {
        NSImage(data)
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
