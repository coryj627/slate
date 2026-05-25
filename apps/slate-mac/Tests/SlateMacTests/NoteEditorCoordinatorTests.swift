import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// Coordinator-layer tests for `NoteEditorView`. The pure-logic span
/// finder is covered by `EditorEmbedSpansTests`; here we drive a
/// live `NSTextView` + storage through the coordinator so the
/// attribute-layer interactions (highlighting + foreground-color
/// preservation) are exercised end-to-end.
@MainActor
final class NoteEditorCoordinatorTests: XCTestCase {

    /// Regression for [#226](https://github.com/coryj627/slate/issues/226):
    /// `applyEmbedHighlighting` used to strip every `.foregroundColor`
    /// attribute on every pass — a relic of the pre-#207 highlight
    /// that applied a foreground color. After the highlight switched
    /// to underline-only the strip stayed behind, blowing away the
    /// dynamic `NSColor.textColor` AppKit stamps onto typed text and
    /// rendering the editor as black-on-dark in dark mode.
    func testApplyEmbedHighlightingPreservesForegroundColor() {
        let textView = NSTextView(frame: NSRect(x: 0, y: 0, width: 400, height: 200))
        textView.string = "hello ![[target]] world"
        guard let storage = textView.textStorage else {
            return XCTFail("expected text storage on NSTextView")
        }
        let fullRange = NSRange(location: 0, length: (textView.string as NSString).length)
        // Stamp the dynamic system text color the way AppKit's typing-
        // attributes path would on a fresh editor.
        storage.addAttribute(.foregroundColor, value: NSColor.textColor, range: fullRange)

        let text: String = textView.string
        let binding = Binding<String>(get: { text }, set: { _ in })
        let coordinator = NoteEditorView.Coordinator(
            text: binding,
            onSave: {},
            previewEmbedAtCursor: nil
        )
        coordinator.attach(textView: textView)
        coordinator.applyEmbedHighlighting()

        // Foreground color must still be present everywhere — applying
        // the embed highlight is a *side affordance*, not a re-style.
        for offset in 0..<fullRange.length {
            let attrs = storage.attributes(at: offset, effectiveRange: nil)
            XCTAssertEqual(
                attrs[.foregroundColor] as? NSColor,
                NSColor.textColor,
                "applyEmbedHighlighting must not strip the dynamic textColor at offset \(offset)"
            )
        }
    }

    /// And while we're here, verify the underline does land on the
    /// embed span — proves the rest of `applyEmbedHighlighting` still
    /// works after the foreground-strip removal.
    func testApplyEmbedHighlightingAppliesUnderlineToEmbedSpan() {
        let textView = NSTextView(frame: NSRect(x: 0, y: 0, width: 400, height: 200))
        textView.string = "before ![[target]] after"
        guard let storage = textView.textStorage else {
            return XCTFail("expected text storage on NSTextView")
        }

        let text: String = textView.string
        let binding = Binding<String>(get: { text }, set: { _ in })
        let coordinator = NoteEditorView.Coordinator(
            text: binding,
            onSave: {},
            previewEmbedAtCursor: nil
        )
        coordinator.attach(textView: textView)
        coordinator.applyEmbedHighlighting()

        // The `![[target]]` span starts at offset 7 (after "before ").
        let underline = storage.attribute(.underlineStyle, at: 8, effectiveRange: nil)
        XCTAssertEqual(underline as? Int, NSUnderlineStyle.single.rawValue)
        // …and not outside the span.
        let outside = storage.attribute(.underlineStyle, at: 1, effectiveRange: nil)
        XCTAssertNil(outside, "underline must not bleed past the embed span")
    }
}
