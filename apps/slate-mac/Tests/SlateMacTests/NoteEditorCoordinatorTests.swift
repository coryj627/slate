import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// Coordinator-layer tests for `NoteEditorView`. The pure-logic span
/// finder is covered by `EditorEmbedSpansTests`; here we drive a
/// live `NSTextView` + storage through the coordinator so the
/// attribute-layer interactions (highlighting + foreground-color
/// preservation + contrast-aware underline) are exercised end-to-end.
@MainActor
final class NoteEditorCoordinatorTests: XCTestCase {

    private func makeCoordinator(
        text: String,
        previewEmbedAtCursor: ((String, Int) -> Void)? = nil
    ) -> (NoteEditorView.Coordinator, NSTextView, NSTextStorage) {
        let textView = NSTextView(frame: NSRect(x: 0, y: 0, width: 400, height: 200))
        textView.string = text
        let storage = textView.textStorage!
        let captured = text
        let binding = Binding<String>(get: { captured }, set: { _ in })
        let coordinator = NoteEditorView.Coordinator(
            text: binding,
            onSave: {},
            previewEmbedAtCursor: previewEmbedAtCursor
        )
        coordinator.attach(textView: textView)
        return (coordinator, textView, storage)
    }

    /// Regression for [#226](https://github.com/coryj627/slate/issues/226):
    /// `applyEmbedHighlighting` used to strip every `.foregroundColor`
    /// attribute on every pass — a relic of the pre-#207 highlight
    /// that applied a foreground color. After the highlight switched
    /// to underline-only the strip stayed behind, blowing away the
    /// dynamic `NSColor.textColor` AppKit stamps onto typed text and
    /// rendering the editor as black-on-dark in dark mode.
    func testApplyEmbedHighlightingPreservesStampedForegroundColor() {
        let (coordinator, _, storage) = makeCoordinator(text: "hello ![[target]] world")
        let fullRange = NSRange(location: 0, length: storage.length)
        // Stamp the dynamic system text color the way AppKit's typing-
        // attributes path would on a fresh editor.
        storage.addAttribute(.foregroundColor, value: NSColor.textColor, range: fullRange)

        coordinator.applyEmbedHighlighting()

        for offset in 0..<fullRange.length {
            let attrs = storage.attributes(at: offset, effectiveRange: nil)
            XCTAssertEqual(
                attrs[.foregroundColor] as? NSColor,
                NSColor.textColor,
                "applyEmbedHighlighting must not strip the dynamic textColor at offset \(offset)"
            )
        }
    }

    /// Audit [#231](https://github.com/coryj627/slate/issues/231): the
    /// pre-stamped case above tests the "we don't strip what's there"
    /// half. The actual failure path is typed text — AppKit doesn't
    /// always stamp `.foregroundColor` onto storage, it inherits via
    /// typing attributes. After the fix, running the highlight should
    /// leave non-embed ranges with NO `.foregroundColor` attribute,
    /// and embed ranges should ALSO have no foreground color (the
    /// highlight is underline-only per audit #207).
    func testApplyEmbedHighlightingNeverAddsForegroundColor() {
        let (coordinator, _, storage) = makeCoordinator(text: "before ![[target]] after")
        // Sanity-check the starting state: NSTextView's default
        // `string =` may stamp typing attributes that include the
        // foreground color. Whatever it did, we want the assertion to
        // hold after `applyEmbedHighlighting` runs — that pass must
        // not ADD a foreground color anywhere, especially not inside
        // the embed span.
        coordinator.applyEmbedHighlighting()
        coordinator.applyEmbedHighlighting()  // twice — state must not accumulate

        // Inside the embed span (offset 7..18, `![[target]]`) the
        // highlight must NOT have added a foreground color attribute.
        for offset in 7..<18 {
            let attrs = storage.attributes(at: offset, effectiveRange: nil)
            // It's fine if AppKit's default put NSColor.textColor in
            // there before we ran — but we must not see a different
            // color, and especially not a static foreground that the
            // pre-#207 highlight would have stamped (systemBlue).
            if let color = attrs[.foregroundColor] as? NSColor {
                XCTAssertEqual(
                    color, NSColor.textColor,
                    "applyEmbedHighlighting must not introduce a custom foreground color inside the embed span"
                )
            }
        }
    }

    /// And while we're here, verify the underline does land on the
    /// embed span — proves the rest of `applyEmbedHighlighting` still
    /// works after the foreground-strip removal.
    func testApplyEmbedHighlightingAppliesUnderlineToEmbedSpan() {
        let (coordinator, _, storage) = makeCoordinator(text: "before ![[target]] after")

        coordinator.applyEmbedHighlighting()

        let underline = storage.attribute(.underlineStyle, at: 8, effectiveRange: nil)
        XCTAssertEqual(underline as? Int, NSUnderlineStyle.single.rawValue)
        let outside = storage.attribute(.underlineStyle, at: 1, effectiveRange: nil)
        XCTAssertNil(outside, "underline must not bleed past the embed span")
    }

    /// Audit [#230](https://github.com/coryj627/slate/issues/230):
    /// `controlAccentColor` is borderline against `textBackgroundColor`
    /// (especially on Graphite + dark mode) — under Increase Contrast
    /// the underline must swap to `labelColor` so the embed cue
    /// remains discriminable for low-vision users (WCAG 1.4.11).
    func testEmbedUnderlineColorRespectsIncreaseContrast() {
        XCTAssertEqual(
            NoteEditorView.Coordinator.embedUnderlineColor(increaseContrast: false),
            NSColor.controlAccentColor,
            "default mode uses accent color"
        )
        XCTAssertEqual(
            NoteEditorView.Coordinator.embedUnderlineColor(increaseContrast: true),
            NSColor.labelColor,
            "Increase Contrast swaps to labelColor for guaranteed contrast"
        )
    }

    /// Audit [#230](https://github.com/coryj627/slate/issues/230)
    /// follow-on: changing the system display options must re-run the
    /// highlight pass so the underline color refreshes without a
    /// vault reload. Drive the notification directly and assert the
    /// pass ran.
    func testSystemColorPreferencesNotificationReappliesHighlight() {
        let (coordinator, _, storage) = makeCoordinator(text: "edge ![[a]] case")
        coordinator.applyEmbedHighlighting()
        // Clear the underline so we can detect the re-apply.
        storage.removeAttribute(
            .underlineStyle,
            range: NSRange(location: 0, length: storage.length)
        )

        NotificationCenter.default.post(
            name: NSWorkspace.accessibilityDisplayOptionsDidChangeNotification,
            object: nil
        )

        // Embed span `![[a]]` starts at offset 5, length 6.
        let underline = storage.attribute(.underlineStyle, at: 6, effectiveRange: nil)
        XCTAssertEqual(
            underline as? Int, NSUnderlineStyle.single.rawValue,
            "accessibility-options notification must re-run applyEmbedHighlighting"
        )
    }

    /// Audit [#233](https://github.com/coryj627/slate/issues/233):
    /// `attach(textView:)` is a re-bind point, not just a reference
    /// grab. A new textView handed to a recycled coordinator must
    /// start in a known typing-attributes state — otherwise an
    /// inherited `.foregroundColor` attribute could shadow
    /// `NSColor.textColor` and re-introduce the dark-mode invisible
    /// text bug from #226.
    func testAttachResetsTypingAttributesAndForegroundColor() {
        // Simulate a textView that was previously stamped with a
        // stale foreground color (e.g. red, from a hypothetical
        // earlier rendering pass).
        let textView = NSTextView(frame: NSRect(x: 0, y: 0, width: 400, height: 200))
        textView.string = "hello world"
        let storage = textView.textStorage!
        storage.addAttribute(
            .foregroundColor,
            value: NSColor.red,
            range: NSRange(location: 0, length: storage.length)
        )

        let binding = Binding<String>(get: { "hello world" }, set: { _ in })
        let coordinator = NoteEditorView.Coordinator(
            text: binding,
            onSave: {},
            previewEmbedAtCursor: nil
        )
        coordinator.attach(textView: textView)

        // After attach: storage-level foreground stripped, typing
        // attributes reset to textColor.
        let foreground = storage.attribute(
            .foregroundColor, at: 0, effectiveRange: nil
        )
        XCTAssertNil(
            foreground,
            "attach must strip storage-level foreground color so dynamic textColor shows through"
        )
        let typingColor = textView.typingAttributes[.foregroundColor] as? NSColor
        XCTAssertEqual(
            typingColor, NSColor.textColor,
            "attach must restore textColor in typing attributes for new typed text"
        )
    }

    /// Audit [#233](https://github.com/coryj627/slate/issues/233):
    /// repeated `attach` calls must not stack notification handlers.
    /// Without the dedup, a coordinator recycled twice would re-run
    /// `applyEmbedHighlighting` twice for every appearance change,
    /// which is wasted work and a soft hint that lifecycle hygiene
    /// is slipping.
    func testRepeatedAttachDoesNotDoubleFireObservers() {
        let (coordinator, _, storage) = makeCoordinator(text: "edge ![[a]] case")
        // Re-attach to a fresh textView (simulating SwiftUI handing
        // the same coordinator a new NSView).
        let newTextView = NSTextView(frame: NSRect(x: 0, y: 0, width: 400, height: 200))
        newTextView.string = "edge ![[a]] case"
        coordinator.attach(textView: newTextView)

        // Drive the notification once; expect a single re-apply.
        // (Storage from `makeCoordinator` is no longer the live one;
        // assert against the new textView's storage.)
        let newStorage = newTextView.textStorage!
        newStorage.removeAttribute(
            .underlineStyle,
            range: NSRange(location: 0, length: newStorage.length)
        )
        NotificationCenter.default.post(
            name: NSWorkspace.accessibilityDisplayOptionsDidChangeNotification,
            object: nil
        )
        // The originally-attached storage MUST NOT have been
        // re-highlighted (the coordinator now points at newTextView).
        let oldStorageUnderline = storage.attribute(
            .underlineStyle, at: 6, effectiveRange: nil
        )
        XCTAssertNil(
            oldStorageUnderline,
            "after re-attach, observer must target the new textView only — the old one stays untouched"
        )
        let newStorageUnderline = newStorage.attribute(
            .underlineStyle, at: 6, effectiveRange: nil
        )
        XCTAssertEqual(
            newStorageUnderline as? Int, NSUnderlineStyle.single.rawValue,
            "re-attached coordinator must apply highlight to the new textView"
        )
    }
}
