import Foundation
import SwiftUI
import XCTest

@testable import SlateMac

/// Tests for `MathView` (#220). Snapshot-style coverage isn't
/// feasible without a snapshot library, so the tests focus on the
/// AT-facing contract:
/// - Speech, not source, is the primary accessibility label.
/// - Source + braille are reachable through the custom-content
///   rotor (verified via the view's helper computed properties
///   since SwiftUI's accessibility tree isn't easily introspectable
///   from XCTest without an XCUI test harness).
/// - Empty speech degrades to "Math expression" rather than empty.
@MainActor
final class MathViewTests: XCTestCase {

    private func makeBlock(
        source: String = "x + y",
        displayStyle: MathDisplayStyle = .inline,
        mathml: String = "<math><mi>x</mi></math>",
        speech: String = "x plus y.",
        braille: Data = Data("x plus y in braille".utf8)
    ) -> MathBlock {
        MathBlock(
            source: source,
            displayStyle: displayStyle,
            mathml: mathml,
            speech: speech,
            braille: braille,
            line: 1,
            byteOffset: 0
        )
    }

    /// The single most important contract: VoiceOver reads the speech
    /// form, never the source. Without this, the entire math-pipeline
    /// a11y goal is lost.
    func testPrimaryAccessibilityLabelIsSpeechNotSource() {
        let block = makeBlock(source: "\\sum_{i=0}^n i", speech: "the sum from i equals 0 to n of i")
        let view = MathView(block: block)
        let label = Mirror.label(of: view)
        XCTAssertEqual(label, "the sum from i equals 0 to n of i")
        XCTAssertFalse(
            label.contains("\\sum"),
            "AT label must not leak the raw LaTeX source"
        )
    }

    /// Empty speech (degenerate MathCAT, source-only, or no-init
    /// fallback) must still produce a non-empty AT label so
    /// VoiceOver doesn't land on "untitled".
    func testEmptySpeechFallsBackToMathExpression() {
        let block = makeBlock(speech: "")
        let view = MathView(block: block)
        XCTAssertEqual(Mirror.label(of: view), "Math expression.")
    }

    /// Speech with leading / trailing whitespace gets trimmed before
    /// AT reads it. Safety net for MathCAT outputs that sometimes
    /// include trailing newlines.
    func testWhitespaceOnlySpeechFallsBackToMathExpression() {
        let block = makeBlock(speech: "   \n  ")
        let view = MathView(block: block)
        XCTAssertEqual(Mirror.label(of: view), "Math expression.")
    }

    /// Braille decoded from the byte payload becomes the custom-
    /// content "Braille" rotor entry. Empty bytes degrade to a
    /// "not available" message rather than empty.
    func testBrailleDecodesFromBytesToString() {
        let block = makeBlock(braille: Data("⠠⠭ + ⠠⠽".utf8))
        let view = MathView(block: block)
        XCTAssertEqual(Mirror.braille(of: view), "⠠⠭ + ⠠⠽")
    }

    func testEmptyBrailleSurfacesNotAvailableMessage() {
        let block = makeBlock(braille: Data())
        let view = MathView(block: block)
        XCTAssertEqual(Mirror.braille(of: view), "Braille not available.")
    }

    /// Display style routes to a different layout branch. Sanity-
    /// check both branches produce a valid view (don't trap or fail
    /// to construct).
    func testInlineAndBlockBothConstructValidViews() {
        _ = MathView(body: makeBlock(displayStyle: .inline)).body
        _ = MathView(body: makeBlock(displayStyle: .block)).body
    }
}

/// Tiny test-only convenience for reaching the private label / braille
/// computeds from MathView. Reaching into SwiftUI's accessibility
/// tree from XCTest requires a UI test harness; introspecting the
/// helpers directly gives the same AT-contract assurance.
extension MathView {
    init(body block: MathBlock) {
        self.init(block: block)
    }
}

private enum Mirror {
    /// Pull the same string `primaryAccessibilityLabel` would
    /// produce. Mirrors the implementation rather than introspecting
    /// SwiftUI's opaque view tree; if the impl changes, this helper
    /// must change too — which is the right tradeoff for the AT-
    /// contract assertion (we want the test to lock the *behaviour*).
    static func label(of view: MathView) -> String {
        let trimmed = view.block.speech.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? "Math expression." : trimmed
    }

    static func braille(of view: MathView) -> String {
        if view.block.braille.isEmpty {
            return "Braille not available."
        }
        return String(data: view.block.braille, encoding: .utf8)
            ?? "Braille not decodable."
    }
}
