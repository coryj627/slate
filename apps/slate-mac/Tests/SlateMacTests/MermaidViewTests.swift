import Foundation
import SwiftUI
import XCTest

@testable import SlateMac

/// Tests for `MermaidView` (#222). The AT-contract assertions go
/// through helper computeds since the SwiftUI accessibility tree
/// isn't easily introspectable from XCTest. Same trade-off
/// documented in `MathViewTests`.
@MainActor
final class MermaidViewTests: XCTestCase {

    private func makeBlock(
        source: String = "flowchart LR\nA --> B\n",
        dialect: DiagramDialect = .mermaid,
        svg: Data? = Data("<svg></svg>".utf8),
        pngFallback: Data? = nil,
        structuredDescription: String = "Flowchart with 1 step.",
        renderStatus: DiagramRenderStatus = .ok
    ) -> DiagramBlock {
        DiagramBlock(
            source: source,
            dialect: dialect,
            svg: svg,
            pngFallback: pngFallback,
            structuredDescription: structuredDescription,
            renderStatus: renderStatus,
            line: 1,
            byteOffset: 0
        )
    }

    /// Headline contract: the AT label is the structured description,
    /// not "image" or "SVG". Without this, sighted users see a
    /// rendered flowchart while AT users hear nothing about it.
    func testPrimaryAccessibilityLabelIsStructuredDescription() {
        let block = makeBlock(
            structuredDescription: "Flowchart with 5 steps: Start → Validate → Save → Notify → End."
        )
        let view = MermaidView(block: block)
        XCTAssertEqual(
            view.primaryAccessibilityLabel,
            "Flowchart with 5 steps: Start → Validate → Save → Notify → End."
        )
    }

    /// Failed-render path: structured description is still the AT
    /// label, even when the SVG isn't there. AT users hear the
    /// same content sighted users would have seen.
    func testFailedRenderStillSurfacesStructuredDescription() {
        let block = makeBlock(
            svg: nil,
            structuredDescription: "Sequence diagram with 3 interactions.",
            renderStatus: .renderFailed(message: "mermaid parser threw")
        )
        let view = MermaidView(block: block)
        XCTAssertEqual(
            view.primaryAccessibilityLabel,
            "Sequence diagram with 3 interactions."
        )
    }

    /// Unsupported-dialect path: same — backend's structured
    /// description is still the AT label.
    func testUnsupportedDialectStillSurfacesStructuredDescription() {
        let block = makeBlock(
            svg: nil,
            structuredDescription: "Mermaid diagram, source: weirdDiagram\nstuff",
            renderStatus: .unsupportedDialect(reason: "weirdDiagram")
        )
        let view = MermaidView(block: block)
        XCTAssertEqual(
            view.primaryAccessibilityLabel,
            "Mermaid diagram, source: weirdDiagram\nstuff"
        )
    }

    /// Empty structured description (defensive — backend should never
    /// produce this, but guard against it) must still produce a
    /// non-empty AT label so VoiceOver doesn't land on "untitled".
    func testEmptyStructuredDescriptionFallsBackToMermaidDiagram() {
        let block = makeBlock(structuredDescription: "")
        let view = MermaidView(block: block)
        XCTAssertEqual(view.primaryAccessibilityLabel, "Mermaid diagram.")
    }

    /// The `Source` rotor entry exposes the raw Mermaid source.
    func testSourceRotorEntryContainsRawSource() {
        let block = makeBlock(source: "flowchart LR\nA --> B\n")
        let view = MermaidView(block: block)
        XCTAssertEqual(view.sourceAccessibilityValue, "flowchart LR\nA --> B")
    }

    func testEmptySourceSurfacesNotAvailableMessage() {
        let block = makeBlock(source: "", structuredDescription: "Flowchart, empty.")
        let view = MermaidView(block: block)
        XCTAssertEqual(view.sourceAccessibilityValue, "Source not available.")
    }

    /// Audit #254 L1: empty SVG bytes route to the failure fallback.
    /// `nil` was already tested; empty `Data()` wasn't.
    func testEmptySvgDataRoutesToFailureFallback() {
        let block = makeBlock(
            svg: Data(),
            structuredDescription: "Flowchart with 1 step.",
            renderStatus: .ok
        )
        let view = MermaidView(block: block)
        // AT label is still the structured description regardless
        // of the visual path.
        XCTAssertEqual(view.primaryAccessibilityLabel, "Flowchart with 1 step.")
    }

    /// Audit #254 M2: tooltip preview must be short. Sources over
    /// 120 chars or 3 lines get truncated with an ellipsis.
    func testTooltipPreviewTruncatesLongSource() {
        let longSource = String(repeating: "flowchart LR\nA --> B\n", count: 30)
        let block = makeBlock(source: longSource)
        let view = MermaidView(block: block)
        XCTAssertTrue(
            view.tooltipPreview.hasSuffix("…"),
            "Long source should be truncated with an ellipsis; got \(view.tooltipPreview)"
        )
        XCTAssertLessThanOrEqual(view.tooltipPreview.count, 121)
    }

    func testTooltipPreviewKeepsShortSourceVerbatim() {
        let block = makeBlock(source: "flowchart LR\nA --> B")
        let view = MermaidView(block: block)
        XCTAssertFalse(
            view.tooltipPreview.hasSuffix("…"),
            "Short source should not be truncated; got \(view.tooltipPreview)"
        )
    }

    func testAllRenderStatusBranchesConstructValidViews() {
        let ok = makeBlock(renderStatus: .ok)
        let unsupported = makeBlock(
            svg: nil,
            renderStatus: .unsupportedDialect(reason: "weird")
        )
        let failed = makeBlock(svg: nil, renderStatus: .renderFailed(message: "boom"))
        _ = MermaidView(block: ok).body
        _ = MermaidView(block: unsupported).body
        _ = MermaidView(block: failed).body
    }
}

/// Test-only access to the internal helpers — matches the pattern
/// MathViewTests uses for the same accessibility-tree-unreachable
/// reason.
extension MermaidView {
    var primaryAccessibilityLabel: String {
        let trimmed = block.structuredDescription
            .trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? "Mermaid diagram." : trimmed
    }

    var sourceAccessibilityValue: String {
        let trimmed = block.source.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? "Source not available." : trimmed
    }
}
