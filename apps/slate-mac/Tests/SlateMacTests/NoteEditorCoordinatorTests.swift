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

    /// Regression for [#226](https://github.com/coryj627/slate/issues/226)
    /// + #296, re-expressed for the #376 temporary-attribute model:
    /// the highlight overlay paints per-kind foreground as
    /// `NSLayoutManager` temporary attributes; ranges that aren't
    /// classified get NO temporary foreground, so the storage base
    /// (`NSColor.textColor`, stamped by `attach`) shows through. That's
    /// how un-classified prose keeps the white-on-dark contrast the
    /// #226/#302 fixes earned — the highlight no longer touches storage
    /// at all.
    ///
    /// Fixture: `hello ![[target]] world` — [0,6) is prose ("hello "),
    /// the embed `![[target]]` spans [6,17), and [17,23) is prose
    /// (" world"). The prose ranges must have no temporary foreground
    /// AND keep the storage base `textColor`; the embed span gets the
    /// wikilink colour (see the dedicated embed-highlight test below).
    func testHighlightLeavesProseInBodyColorOutsideSpans() async {
        let (coordinator, textView, storage) = makeCoordinator(text: "hello ![[target]] world")
        let layoutManager = textView.layoutManager!

        coordinator.scheduleHighlight(debounced: false)
        await coordinator.highlightTask?.value

        let proseOffsets: [Int] = Array(0..<6) + Array(17..<storage.length)
        for offset in proseOffsets {
            XCTAssertNil(
                layoutManager.temporaryAttribute(
                    .foregroundColor, atCharacterIndex: offset, effectiveRange: nil
                ),
                "prose at offset \(offset) must have no temporary foreground override"
            )
            XCTAssertEqual(
                storage.attribute(.foregroundColor, at: offset, effectiveRange: nil) as? NSColor,
                NSColor.textColor,
                "storage base colour at offset \(offset) must remain dynamic textColor"
            )
        }
    }

    /// Audit [#231](https://github.com/coryj627/slate/issues/231) +
    /// #296, for #376: post-highlight, classified ranges MUST carry the
    /// palette colour for their kind as a temporary attribute — not the
    /// body colour, and not a stale colour from a previous pass. The
    /// canonical `embed` span maps to the wikilink colour (`labelColor`
    /// under Increase Contrast — see `EditorSyntaxPaletteTests`). The
    /// previous test enforces the inverse for ranges OUTSIDE any span.
    ///
    /// Runs the pass twice to prove temporary attributes are reset +
    /// reapplied each time rather than accumulating.
    func testHighlightStampsWikilinkColorOnEmbedRange() async {
        let (coordinator, textView, _) = makeCoordinator(text: "before ![[target]] after")
        let layoutManager = textView.layoutManager!

        coordinator.scheduleHighlight(debounced: false)
        await coordinator.highlightTask?.value
        coordinator.scheduleHighlight(debounced: false)  // twice — must not accumulate
        await coordinator.highlightTask?.value

        // Embed span: offset 7..18 (`![[target]]`, all ASCII so byte ==
        // UTF-16). Read the palette via the same instance entry point
        // the highlighter uses (consults NSWorkspace's IC pref) so the
        // assertion holds regardless of the host's Increase Contrast.
        let expected = EditorSyntaxPalette.color(for: .embed)
        for offset in 7..<18 {
            XCTAssertEqual(
                layoutManager.temporaryAttribute(
                    .foregroundColor, atCharacterIndex: offset, effectiveRange: nil
                ) as? NSColor,
                expected,
                "embed span offset \(offset) must carry the wikilink palette colour"
            )
        }
    }

    /// The embed underline lands on the embed span as a temporary
    /// attribute and doesn't bleed past it.
    func testHighlightAppliesUnderlineToEmbedSpan() async {
        let (coordinator, textView, _) = makeCoordinator(text: "before ![[target]] after")
        let layoutManager = textView.layoutManager!

        coordinator.scheduleHighlight(debounced: false)
        await coordinator.highlightTask?.value

        let underline = layoutManager.temporaryAttribute(
            .underlineStyle, atCharacterIndex: 8, effectiveRange: nil
        )
        XCTAssertEqual(underline as? Int, NSUnderlineStyle.single.rawValue)
        let outside = layoutManager.temporaryAttribute(
            .underlineStyle, atCharacterIndex: 1, effectiveRange: nil
        )
        XCTAssertNil(outside, "underline must not bleed past the embed span")
    }

    /// Red-team #376 follow-up: Cmd+E must resolve the embed under the
    /// cursor against the LIVE buffer, not the debounced highlight pass's
    /// cached spans. Otherwise typing an embed and immediately hitting
    /// Cmd+E (before the ~40 ms pass lands) would miss it. Here no
    /// highlight pass is run at all — the embed is found purely from the
    /// current `textView.string`.
    func testCmdEResolvesEmbedFromLiveBufferWithoutHighlightPass() {
        var captured: String?
        let (coordinator, textView, _) = makeCoordinator(
            text: "no embeds yet",
            previewEmbedAtCursor: { target, _ in captured = target }
        )
        // Mutate the buffer directly and place the cursor inside a
        // freshly-"typed" embed, without scheduling/awaiting a highlight.
        textView.string = "see ![[FreshNote]] now"
        textView.setSelectedRange(NSRange(location: 8, length: 0))  // inside FreshNote

        let handled = coordinator.openEmbedPreviewAtCursor()

        XCTAssertTrue(
            handled,
            "Cmd+E must find the just-typed embed without waiting for the highlight pass"
        )
        XCTAssertEqual(captured, "FreshNote")
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
    /// going through SwiftUI's `@Environment` plumbing.
    ///
    /// We only assert end-state on the `reduceMotion = true` branch:
    /// that path wraps the scroll in `NSAnimationContext` with
    /// `duration = 0` and is guaranteed to land synchronously. The
    /// `reduceMotion = false` path uses AppKit's default animator
    /// proxy, which may defer the `visibleRect` update past the call
    /// boundary — asserting on its post-call state risks cross-machine
    /// flakiness (PR #306 Codoki review). For that branch we only
    /// assert the helper doesn't crash.
    @MainActor
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

        // reduceMotion = false — animated path. Just exercise the
        // call to confirm it doesn't trap on a nil container, a
        // detached scroll view, or any of the AppKit edge cases this
        // path touches. End-state is intentionally not asserted: the
        // animator proxy can defer the visibleRect update past the
        // call boundary, which would make a positional assertion
        // flake.
        NoteEditorView.Coordinator.scrollRangeToVisible(
            target, in: textView, reduceMotion: false
        )

        // Reset scroll position so the reduceMotion = true branch has
        // visible work to do.
        textView.scroll(.zero)
        XCTAssertEqual(textView.visibleRect.origin.y, 0)

        // reduceMotion = true — wrapped in NSAnimationContext with
        // duration = 0, guaranteed synchronous landing. End-state IS
        // asserted here: post-call the visible rect must reflect the
        // scroll-target offset (no run-loop spin required).
        NoteEditorView.Coordinator.scrollRangeToVisible(
            target, in: textView, reduceMotion: true
        )
        XCTAssertGreaterThan(
            textView.visibleRect.origin.y, 0,
            "reduceMotion = true: scroll must land instantly (NSAnimationContext duration = 0)"
        )
    }

    /// Audit [#230](https://github.com/coryj627/slate/issues/230)
    /// follow-on: changing the system display options must re-run the
    /// highlight pass so the underline color refreshes without a
    /// vault reload. Drive the notification directly and assert the
    /// pass ran.
    func testSystemColorPreferencesNotificationReappliesHighlight() async {
        let (coordinator, textView, _) = makeCoordinator(text: "edge ![[a]] case")
        let layoutManager = textView.layoutManager!
        coordinator.scheduleHighlight(debounced: false)
        await coordinator.highlightTask?.value
        // Clear the underline so we can detect the re-apply.
        layoutManager.removeTemporaryAttribute(
            .underlineStyle,
            forCharacterRange: NSRange(location: 0, length: (textView.string as NSString).length)
        )

        // The observer fires synchronously and schedules a fresh
        // (immediate) highlight; await the task it created. #416:
        // post on NSWorkspace.shared.notificationCenter — the center
        // AppKit actually delivers this notification on. (The old
        // version posted on NotificationCenter.default, which masked
        // the dead default-center registration this test now guards
        // against.)
        NSWorkspace.shared.notificationCenter.post(
            name: NSWorkspace.accessibilityDisplayOptionsDidChangeNotification,
            object: nil
        )
        await coordinator.highlightTask?.value

        // Embed span `![[a]]` starts at offset 5, length 6.
        let underline = layoutManager.temporaryAttribute(
            .underlineStyle, atCharacterIndex: 6, effectiveRange: nil
        )
        XCTAssertEqual(
            underline as? Int, NSUnderlineStyle.single.rawValue,
            "accessibility-options notification must re-run the highlight pass"
        )
    }

    /// #416 second leg: accent-color changes arrive as
    /// `NSColor.systemColorsDidChangeNotification` on the DEFAULT
    /// center — the highlight must re-run for those too (the old
    /// "AppleAquaColorVariantChanged" name was distributed-center
    /// only and never fired).
    func testSystemColorsDidChangeReappliesHighlight() async {
        let (coordinator, textView, _) = makeCoordinator(text: "edge ![[a]] case")
        let layoutManager = textView.layoutManager!
        coordinator.scheduleHighlight(debounced: false)
        await coordinator.highlightTask?.value
        layoutManager.removeTemporaryAttribute(
            .underlineStyle,
            forCharacterRange: NSRange(location: 0, length: (textView.string as NSString).length)
        )

        NotificationCenter.default.post(
            name: NSColor.systemColorsDidChangeNotification,
            object: nil
        )
        await coordinator.highlightTask?.value

        let underline = layoutManager.temporaryAttribute(
            .underlineStyle, atCharacterIndex: 6, effectiveRange: nil
        )
        XCTAssertEqual(
            underline as? Int, NSUnderlineStyle.single.rawValue,
            "system-colors notification must re-run the highlight pass"
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

    /// #431: outline activation must scroll by the backend's byte
    /// offset — the rendered-text search silently failed for
    /// inline-markup headings while the announcement claimed
    /// success. The coordinator now returns the truth.
    func testScrollToHeadingAnchorUsesByteOffsetForInlineMarkupHeading() {
        let text = "# Plain\n\nbody text here\n\n## A **bold** heading\n\ntail\n"
        let (coordinator, textView, _) = makeCoordinator(text: text)
        let ns = text as NSString
        let offset = ns.range(of: "## A **bold**").location  // ASCII → byte == utf16
        coordinator.headings = [
            Heading(level: 1, text: "Plain", ordinal: 0, anchorId: "plain", byteOffset: 0),
            Heading(
                level: 2,
                text: "A bold heading",  // rendered text ≠ raw buffer
                ordinal: 1,
                anchorId: "a-bold-heading",
                byteOffset: UInt32(offset)
            ),
        ]

        let ok = coordinator.scrollToHeadingAnchor("a-bold-heading")

        XCTAssertTrue(ok, "offset-based scroll must succeed where text search cannot")
        XCTAssertEqual(
            textView.selectedRange().location, offset,
            "caret must park at the heading's real position"
        )
    }

    /// Stale offset (unsaved edits shifted the buffer) falls back to
    /// the rendered-text search when that still matches.
    func testScrollToHeadingAnchorFallsBackToTextSearchOnStaleOffset() {
        let text = "intro\n\n# Findable\n\nbody\n"
        let (coordinator, textView, _) = makeCoordinator(text: text)
        let real = (text as NSString).range(of: "# Findable").location
        coordinator.headings = [
            // Offset points mid-body (stale), but the line there
            // doesn't carry the heading text → fallback kicks in.
            Heading(level: 1, text: "Findable", ordinal: 0, anchorId: "findable",
                    byteOffset: UInt32((text as NSString).length - 2))
        ]

        let ok = coordinator.scrollToHeadingAnchor("findable")

        XCTAssertTrue(ok)
        // Fallback finds the heading TEXT ("Findable"), which sits
        // inside the heading line — close enough to scroll/park.
        let parked = textView.selectedRange().location
        XCTAssertTrue(
            parked >= real && parked <= real + 2,
            "fallback must park at the heading text (got \(parked), heading at \(real))"
        )
    }

    /// Unknown anchor → false, no crash (the honest-failure path; the
    /// announcement itself is fire-and-forget).
    func testScrollToHeadingAnchorReturnsFalseForUnknownAnchor() {
        let (coordinator, _, _) = makeCoordinator(text: "# Only\n")
        coordinator.headings = []
        XCTAssertFalse(coordinator.scrollToHeadingAnchor("ghost"))
    }

    /// Audit [#233](https://github.com/coryj627/slate/issues/233):
    /// `attach` is a re-bind point — after it, the coordinator must
    /// target the NEW textView, and the appearance observer (deduped via
    /// `removeObserver` in `attach`) must re-highlight that new view, not
    /// the abandoned one. With the #376 overlay this is verified through
    /// each view's own layout-manager temporary attributes.
    func testRepeatedAttachRetargetsHighlightToNewTextView() async {
        let (coordinator, oldTextView, _) = makeCoordinator(text: "edge ![[a]] case")
        let oldLayoutManager = oldTextView.layoutManager!
        // Re-attach to a fresh textView (simulating SwiftUI handing
        // the same coordinator a new NSView).
        let newTextView = NSTextView(frame: NSRect(x: 0, y: 0, width: 400, height: 200))
        newTextView.string = "edge ![[a]] case"
        coordinator.attach(textView: newTextView)
        let newLayoutManager = newTextView.layoutManager!

        // Clear the new view's underline so we can detect the re-apply
        // the notification triggers.
        newLayoutManager.removeTemporaryAttribute(
            .underlineStyle,
            forCharacterRange: NSRange(location: 0, length: (newTextView.string as NSString).length)
        )
        // #416: post on the workspace center — where AppKit actually
        // delivers this notification.
        NSWorkspace.shared.notificationCenter.post(
            name: NSWorkspace.accessibilityDisplayOptionsDidChangeNotification,
            object: nil
        )
        await coordinator.highlightTask?.value

        // The originally-attached view MUST NOT have been highlighted
        // (the coordinator now points at newTextView, and the old view
        // was never highlighted — `attach` doesn't highlight).
        XCTAssertNil(
            oldLayoutManager.temporaryAttribute(.underlineStyle, atCharacterIndex: 6, effectiveRange: nil),
            "after re-attach, the observer must target the new textView only — the old one stays untouched"
        )
        XCTAssertEqual(
            newLayoutManager.temporaryAttribute(.underlineStyle, atCharacterIndex: 6, effectiveRange: nil) as? Int,
            NSUnderlineStyle.single.rawValue,
            "re-attached coordinator must apply highlight to the new textView"
        )
    }
}
