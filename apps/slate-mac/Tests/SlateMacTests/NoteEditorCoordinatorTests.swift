// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

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

    /// Audit [#301](https://github.com/coryj627/slate/issues/301):
    /// scroll routing must honor `accessibilityReduceMotion` (WCAG
    /// 2.3.1). The pure static helper handles both branches without
    /// going through SwiftUI's `@Environment` plumbing — verify it
    /// scrolls the target into view in both modes, and that the
    /// reduce-motion branch executes synchronously inside the
    /// `NSAnimationContext.runAnimationGroup` block (i.e. the helper
    /// itself doesn't error and the textView reflects the post-scroll
    /// state before the call returns).
    func testScrollRangeRespectsReduceMotion() {
        // 100 lines × 80 chars so scrollRangeToVisible has work to do
        // — without enough content, the textView's visible rect
        // already covers the target and the call is a no-op.
        let body = (0..<100).map { "line-\($0) " + String(repeating: "x", count: 70) }
            .joined(separator: "\n")
        let textView = NSTextView(frame: NSRect(x: 0, y: 0, width: 400, height: 200))
        textView.string = body
        let scrollView = NSScrollView(frame: NSRect(x: 0, y: 0, width: 400, height: 200))
        scrollView.documentView = textView
        // Force layout so visibleRect is meaningful.
        textView.layoutManager?.ensureLayout(for: textView.textContainer!)

        let targetLocation = body.utf16.count - 50  // near the end
        let target = NSRange(location: targetLocation, length: 0)

        // reduceMotion=false — default animated path.
        NoteEditorView.Coordinator.scrollRangeToVisible(
            target, in: textView, reduceMotion: false
        )
        // The call always advances the selection-target glyph into the
        // visible rect (animated or not — by the time the call returns,
        // the visible rect has been updated). We can't precisely assert
        // animation duration in a unit test, but we can assert the
        // helper doesn't throw and the view is no longer scrolled to
        // origin.
        let yAfterAnimated = textView.visibleRect.origin.y
        XCTAssertGreaterThan(
            yAfterAnimated, 0,
            "reduceMotion=false: scroll must advance the visible rect"
        )

        // Reset scroll position so the reduceMotion=true branch has
        // work to do.
        textView.scroll(.zero)
        XCTAssertEqual(textView.visibleRect.origin.y, 0)

        // reduceMotion=true — wrapped in NSAnimationContext with
        // duration=0. Same end state as the animated path; the
        // difference (no animation) is observable only via timing,
        // not via the final visible-rect query.
        NoteEditorView.Coordinator.scrollRangeToVisible(
            target, in: textView, reduceMotion: true
        )
        let yAfterReduced = textView.visibleRect.origin.y
        XCTAssertGreaterThan(
            yAfterReduced, 0,
            "reduceMotion=true: scroll must still advance the visible rect (instantly)"
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

    /// Audit [#233](https://github.com/coryj627/slate/issues/233) +
    /// follow-up: `attach(textView:)` is a re-bind point. A new
    /// textView handed to a recycled coordinator must start with a
    /// known *dynamic* text color so dark-mode renders white-on-dark
    /// instead of the dim-grayish-on-dark we saw shipping on `main`
    /// (root cause: `NSTextView.textColor` is `nil` after the
    /// standard init in some contexts, and the rendering fallback
    /// for nil-textColor + no-storage-foreground is darker than
    /// `NSColor.textColor` resolves to).
    ///
    /// Contract after `attach`:
    /// 1. `textView.textColor` is the semantic `NSColor.textColor`
    ///    (dynamic; resolves per appearance).
    /// 2. Storage foreground is the semantic `NSColor.textColor`
    ///    (the textColor setter stamps it onto the existing range —
    ///    that's how AppKit propagates the dynamic color into the
    ///    rendered storage). Importantly this replaces any STALE
    ///    color (e.g. a hardcoded `NSColor.red` from a hypothetical
    ///    earlier pass) — the semantic NSColor still resolves
    ///    dynamically per appearance, so the #226 dark-mode
    ///    invisible-text bug doesn't return.
    /// 3. Typing attributes carry textColor for newly typed chars.
    func testAttachStampsDynamicTextColor() {
        // Simulate a textView that was previously stamped with a
        // stale, hardcoded foreground color.
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

        // Contract 1: textView.textColor is NSColor.textColor.
        XCTAssertEqual(
            textView.textColor, NSColor.textColor,
            "attach must set textView.textColor to the dynamic system text color"
        )

        // Contract 2: storage foreground is NSColor.textColor (not the
        // stale red). The semantic color is dynamic, so dark-mode
        // resolution still works.
        let foreground = storage.attribute(
            .foregroundColor, at: 0, effectiveRange: nil
        ) as? NSColor
        XCTAssertEqual(
            foreground, NSColor.textColor,
            "attach must replace stale foreground colors with the dynamic system text color"
        )
        XCTAssertNotEqual(
            foreground, NSColor.red,
            "stale foreground (red) must not survive attach"
        )

        // Contract 3: typing attributes carry textColor.
        let typingColor = textView.typingAttributes[.foregroundColor] as? NSColor
        XCTAssertEqual(
            typingColor, NSColor.textColor,
            "attach must restore textColor in typing attributes for new typed text"
        )
    }

    /// Codoki review on [PR #303](https://github.com/coryj627/slate/pull/303)
    /// follow-up: locks in the editor's contract that `attach()`
    /// *unconditionally* normalizes per-range foreground colors to
    /// the semantic `NSColor.textColor`, not just the single stale-red
    /// case covered by `testAttachStampsDynamicTextColor`.
    ///
    /// The editor never adds intentional per-range `.foregroundColor`
    /// attributes — it only paints `.underlineStyle` + `.underlineColor`
    /// for `![[…]]` embed spans (audit #207 + #214). If a future change
    /// starts stamping per-range colors (e.g. a markdown syntax
    /// highlighter, [#296](https://github.com/coryj627/slate/issues/296)),
    /// the author needs to choose: either (a) accept that `attach`
    /// will overwrite their colors and re-apply after attach, or (b)
    /// teach `attach` a per-range-aware mode. This test fails loudly
    /// if someone adds per-range colors and expects them to survive
    /// across an `attach` cycle.
    ///
    /// Mixed-color fixture: red on `[0,5)`, blue on `[5,11)`. Both
    /// must be normalized to a single dynamic-system-color run after
    /// attach.
    func testAttachNormalizesAllPerRangeForegroundColors() {
        let textView = NSTextView(frame: NSRect(x: 0, y: 0, width: 400, height: 200))
        textView.string = "hello world"
        let storage = textView.textStorage!
        storage.addAttribute(
            .foregroundColor,
            value: NSColor.red,
            range: NSRange(location: 0, length: 5)
        )
        storage.addAttribute(
            .foregroundColor,
            value: NSColor.systemBlue,
            range: NSRange(location: 5, length: 6)
        )

        let binding = Binding<String>(get: { "hello world" }, set: { _ in })
        let coordinator = NoteEditorView.Coordinator(
            text: binding,
            onSave: {},
            previewEmbedAtCursor: nil
        )
        coordinator.attach(textView: textView)

        // Every offset must now report NSColor.textColor — both the
        // red range and the blue range got normalized.
        for offset in 0..<storage.length {
            let color = storage.attribute(
                .foregroundColor, at: offset, effectiveRange: nil
            ) as? NSColor
            XCTAssertEqual(
                color, NSColor.textColor,
                "offset \(offset) must be normalized to NSColor.textColor; got \(String(describing: color))"
            )
        }

        // And the storage's foreground attribute run is contiguous —
        // no surviving boundary between the old red/blue ranges.
        var effectiveRange = NSRange(location: 0, length: 0)
        _ = storage.attribute(
            .foregroundColor,
            at: 0,
            longestEffectiveRange: &effectiveRange,
            in: NSRange(location: 0, length: storage.length)
        )
        XCTAssertEqual(
            effectiveRange,
            NSRange(location: 0, length: storage.length),
            "the entire storage must be a single textColor run after attach (no leftover per-range boundaries)"
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
