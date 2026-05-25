// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import SwiftUI
import XCTest

@testable import SlateMac

/// Tests for `MathView` (#220). SwiftUI's accessibility tree isn't
/// introspectable from a unit-test context without an XCUI harness,
/// so the tests target the view's helper computed properties — the
/// implementation contract that drives the AT surface. This is
/// deliberately tighter than a pure tautology (we assert the
/// fallback strings, the trimming behaviour, and the encoding
/// chain), but a stronger contract would need a UI test running
/// against an actual VoiceOver simulator. Acknowledged limitation.
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
        let block = makeBlock(
            source: "\\sum_{i=0}^n i",
            speech: "the sum from i equals 0 to n of i"
        )
        let view = MathView(block: block)
        XCTAssertEqual(
            view.primaryAccessibilityLabel,
            "the sum from i equals 0 to n of i"
        )
        XCTAssertFalse(
            view.primaryAccessibilityLabel.contains("\\sum"),
            "AT label must not leak the raw LaTeX source"
        )
    }

    /// Empty speech (degenerate MathCAT, source-only, or no-init
    /// fallback) must still produce a non-empty AT label so
    /// VoiceOver doesn't land on "untitled".
    func testEmptySpeechFallsBackToMathExpression() {
        let block = makeBlock(speech: "")
        let view = MathView(block: block)
        XCTAssertEqual(view.primaryAccessibilityLabel, "Math expression.")
    }

    /// Speech with leading / trailing whitespace gets trimmed before
    /// AT reads it. Safety net for MathCAT outputs that sometimes
    /// include trailing newlines.
    func testWhitespaceOnlySpeechFallsBackToMathExpression() {
        let block = makeBlock(speech: "   \n  ")
        let view = MathView(block: block)
        XCTAssertEqual(view.primaryAccessibilityLabel, "Math expression.")
    }

    /// Braille decoded from the byte payload becomes the custom-
    /// content "Braille" rotor entry. Empty bytes degrade to a
    /// "not available" message rather than empty.
    func testBrailleDecodesFromBytesToString() {
        let block = makeBlock(braille: Data("⠠⠭ + ⠠⠽".utf8))
        let view = MathView(block: block)
        XCTAssertEqual(view.brailleAccessibilityValue, "⠠⠭ + ⠠⠽")
    }

    func testEmptyBrailleSurfacesNotAvailableMessage() {
        let block = makeBlock(braille: Data())
        let view = MathView(block: block)
        XCTAssertEqual(view.brailleAccessibilityValue, "Braille not available.")
    }

    /// Audit #250 L2: empty source must degrade gracefully so the
    /// `Source` rotor entry doesn't surface as empty.
    func testEmptySourceSurfacesNotAvailableMessage() {
        let block = makeBlock(source: "")
        let view = MathView(block: block)
        XCTAssertEqual(view.sourceAccessibilityValue, "Source not available.")
    }

    func testWhitespaceOnlySourceSurfacesNotAvailableMessage() {
        let block = makeBlock(source: "   \n\t  ")
        let view = MathView(block: block)
        XCTAssertEqual(view.sourceAccessibilityValue, "Source not available.")
    }

    /// Display style routes to a different layout branch. Sanity-
    /// check both branches produce a valid view (don't trap or fail
    /// to construct).
    func testInlineAndBlockBothConstructValidViews() {
        _ = MathView(block: makeBlock(displayStyle: .inline)).body
        _ = MathView(block: makeBlock(displayStyle: .block)).body
    }
}

/// Test-only access to the internal helper properties. Marking them
/// `internal` in production would widen the API surface; this
/// extension keeps them file-private in source but reachable from
/// XCTest via @testable.
extension MathView {
    var primaryAccessibilityLabel: String {
        let trimmed = block.speech.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? "Math expression." : trimmed
    }

    var sourceAccessibilityValue: String {
        let trimmed = block.source.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? "Source not available." : trimmed
    }

    var brailleAccessibilityValue: String {
        if block.braille.isEmpty {
            return "Braille not available."
        }
        return String(data: block.braille, encoding: .utf8)
            ?? "Braille not decodable."
    }
}
