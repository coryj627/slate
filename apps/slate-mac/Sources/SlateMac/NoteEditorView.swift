// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Combine
import SwiftUI

/// Editable Markdown source pane (issue #63).
///
/// Wraps `NSTextView` inside an `NSScrollView` and bridges it to
/// SwiftUI via `NSViewRepresentable`. The two-way text binding comes
/// from `AppState.noteTextBinding()` so the dirty-state bookkeeping
/// (`hasUnsavedChanges`, `savedBaselineText`) happens in one place.
///
/// Accessibility note. `NSTextView` ships rich native VoiceOver
/// behavior — character-by-character read, word/line nav, the text
/// rotor, selection announcements. Per issue #63 we keep those
/// untouched ("no custom AX overrides unless a tester reports a
/// missing affordance"). The trade-off: the in-pane heading rotor
/// that `NoteContentView`'s read-only shape supported (one
/// `.accessibilityAddTraits(.isHeader)` per heading) doesn't carry
/// into the editor — `NSTextView` exposes the buffer as one text
/// element. Heading navigation is still available through the
/// **Outline sidebar** (cmd+3 to focus the column), which lists the
/// headings as a navigable list and scrolls the editor when
/// activated via `scrollAnchorRequest`.
///
/// Scrolling.
/// - `scrollAnchorRequest` → look up the heading anchor in the
///   current heading list, find the first occurrence of the heading
///   text in the buffer, and `scrollRangeToVisible`.
/// - `lineScrollRequest` → convert 1-based line number to a
///   character range via newline count and `scrollRangeToVisible`.
struct NoteEditorView: NSViewRepresentable {
    @Binding var text: String
    /// Headings parsed for the current note. Provided up-front so
    /// `scrollAnchorRequest` can map anchor → buffer offset without
    /// taking the SQLite mutex.
    let headings: [Heading]
    /// VoiceOver label for the editor element. The wrapper attaches
    /// it to the `NSTextView` via the AX accessibility-label API so
    /// the editor announces its purpose (e.g. "Editor for foo.md")
    /// rather than the generic "edit text" fallback.
    let accessibilityLabel: String
    /// Cmd+S handler. Invoked by the `NSTextView` subclass when the
    /// user presses ⌘S inside the editor. Routes to
    /// `AppState.saveCurrentNote()` at the call site.
    let onSave: () -> Void
    /// Stream of scroll-to-anchor requests (heading anchor_id).
    /// Mirrors the publisher `NoteContentView` consumed before.
    let scrollAnchorRequest: AnyPublisher<String, Never>
    /// Stream of scroll-to-line requests (1-based line number).
    let lineScrollRequest: AnyPublisher<Int, Never>
    /// Stream of "park the cursor at this UTF-8 byte offset"
    /// requests. Fed by the create-from-template flow when the
    /// rendered template carried a `{{cursor}}` marker (Milestone
    /// H). The coordinator converts the byte offset to UTF-16
    /// before talking to `NSTextView`.
    let cursorByteOffsetRequest: AnyPublisher<Int, Never>
    /// Cmd+E handler (#188): receives the `target` of the embed
    /// the cursor is currently inside, plus the 1-based source
    /// line number for the popover header's spatial-bearing cue
    /// (audit #209). Hooked at the SwiftUI layer to
    /// `AppState.requestEmbedPreview(target:sourceLine:)` so the
    /// popover opens via `pendingEmbedPreview`. Nil disables the
    /// shortcut — used by the previewless contexts that include
    /// the editor (none today, kept for future surfaces).
    let previewEmbedAtCursor: ((String, Int) -> Void)?

    /// System Reduce Motion preference (WCAG 2.3.1). When `true`, the
    /// editor's scroll-routing paths jump instantly instead of using
    /// `NSTextView`'s default animated scroll — vestibular-sensitive
    /// users avoid the spring-loaded re-position when activating an
    /// outline row, scrolling to a line, or landing on a template
    /// cursor offset. Read from `@Environment` so SwiftUI re-pushes
    /// the value through `updateNSView` whenever the system pref
    /// changes mid-session.
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    func makeCoordinator() -> Coordinator {
        Coordinator(
            text: $text,
            onSave: onSave,
            previewEmbedAtCursor: previewEmbedAtCursor
        )
    }

    func makeNSView(context: Context) -> NSScrollView {
        // Custom subclass for the Cmd+E embed-preview intercept
        // (#188). `SlateEditorTextView.performKeyEquivalent(with:)`
        // routes the shortcut to the coordinator; everything else
        // falls through to NSTextView's native handling so the
        // rich VoiceOver / responder behaviour is untouched.
        let scrollView = NSScrollView()
        scrollView.hasVerticalScroller = true
        scrollView.hasHorizontalScroller = false
        scrollView.autohidesScrollers = true
        let contentSize = scrollView.contentSize
        let textView = SlateEditorTextView(
            frame: NSRect(origin: .zero, size: contentSize)
        )
        textView.coordinator = context.coordinator
        textView.autoresizingMask = [.width]
        textView.minSize = NSSize(width: 0, height: 0)
        textView.maxSize = NSSize(
            width: CGFloat.greatestFiniteMagnitude,
            height: CGFloat.greatestFiniteMagnitude
        )
        textView.isVerticallyResizable = true
        textView.isHorizontallyResizable = false
        textView.textContainer?.containerSize = NSSize(
            width: contentSize.width,
            height: CGFloat.greatestFiniteMagnitude
        )
        textView.textContainer?.widthTracksTextView = true
        scrollView.documentView = textView

        // Editor configuration. The defaults make NSTextView
        // attempt to be a word-processor (smart quotes, dashes,
        // automatic capitalization). For Markdown source those
        // substitutions are wrong: a typed `"foo"` should stay as
        // ASCII double-quotes, and `--` should stay as two ASCII
        // hyphens.
        textView.isEditable = true
        textView.isSelectable = true
        textView.isRichText = false
        textView.allowsUndo = true
        textView.isAutomaticQuoteSubstitutionEnabled = false
        textView.isAutomaticDashSubstitutionEnabled = false
        textView.isAutomaticTextReplacementEnabled = false
        textView.isAutomaticSpellingCorrectionEnabled = false
        textView.isAutomaticLinkDetectionEnabled = false
        textView.smartInsertDeleteEnabled = false
        textView.usesFontPanel = false
        textView.usesRuler = false
        textView.font = NSFont.monospacedSystemFont(
            ofSize: NSFont.systemFontSize,
            weight: .regular
        )
        textView.textContainerInset = NSSize(width: 16, height: 16)
        // Soft-wrap. NSTextView defaults vary by SDK; pin
        // explicitly so a vault opened on one macOS version doesn't
        // surprise the user with horizontal scroll on the next.
        textView.isHorizontallyResizable = false
        textView.textContainer?.widthTracksTextView = true
        textView.textContainer?.containerSize = NSSize(
            width: CGFloat.greatestFiniteMagnitude,
            height: CGFloat.greatestFiniteMagnitude
        )
        textView.maxSize = NSSize(
            width: CGFloat.greatestFiniteMagnitude,
            height: CGFloat.greatestFiniteMagnitude
        )
        textView.delegate = context.coordinator
        // Hook Cmd+S without subclassing NSTextView: route through
        // the coordinator's command-selector responder. AppKit's
        // standard responder chain invokes
        // `noResponderFor:eventSelector:` on the `NSTextView` for
        // unhandled key equivalents, which falls through to our
        // window-level handler. Cleaner approach for V1.F: install
        // the save shortcut at the SwiftUI level via a hidden
        // button on the toolbar (see #F4 acceptance + MainSplitView).
        // The `onSave` argument stays here so future per-view
        // shortcuts (autosave on focus loss) have a single place
        // to call into.

        textView.setAccessibilityLabel(accessibilityLabel)
        textView.setAccessibilityRole(.textArea)

        // Initial buffer sync. Subsequent updates come through
        // `updateNSView`.
        if textView.string != text {
            textView.string = text
        }
        // Seed the coordinator with the initial Reduce Motion value
        // — updateNSView will refresh on changes, but the first
        // scroll-request fired before SwiftUI runs its first update
        // pass still needs a defined value.
        context.coordinator.reduceMotion = reduceMotion
        context.coordinator.attach(textView: textView)
        context.coordinator.applyEmbedHighlighting()
        context.coordinator.subscribe(
            scrollAnchorRequest: scrollAnchorRequest,
            lineScrollRequest: lineScrollRequest,
            cursorByteOffsetRequest: cursorByteOffsetRequest
        )
        return scrollView
    }

    func updateNSView(_ scrollView: NSScrollView, context: Context) {
        guard let textView = scrollView.documentView as? NSTextView else {
            return
        }
        // Cache the heading map on the coordinator so a scroll
        // request fired between updates uses the current snapshot.
        context.coordinator.headings = headings
        context.coordinator.accessibilityLabel = accessibilityLabel
        // Push Reduce Motion through so scroll routing fired between
        // updates uses the current preference value — SwiftUI re-runs
        // updateNSView when @Environment(\.accessibilityReduceMotion)
        // flips, so this stays in sync mid-session.
        context.coordinator.reduceMotion = reduceMotion
        textView.setAccessibilityLabel(accessibilityLabel)

        // External buffer change (e.g. file reload after a
        // WriteConflict "Reload from disk"). Only restamp when the
        // bound text genuinely differs from what the view shows —
        // restamping during a normal keystroke would clobber the
        // user's typing and reset the cursor to the start.
        if textView.string != text {
            // Preserve the user's selection across out-of-band
            // updates that don't change the length wildly (e.g. a
            // same-file reload with identical content). For a real
            // content swap the selection is set to the start, which
            // matches AppKit's behavior on reopen.
            let previousRange = textView.selectedRange()
            textView.string = text
            let clampedLocation = min(previousRange.location, text.utf16.count)
            textView.setSelectedRange(NSRange(location: clampedLocation, length: 0))
            context.coordinator.applyEmbedHighlighting()
        }
    }

    @MainActor
    final class Coordinator: NSObject, NSTextViewDelegate {
        @Binding var text: String
        var onSave: () -> Void
        var headings: [Heading] = []
        var accessibilityLabel: String = ""
        var previewEmbedAtCursor: ((String, Int) -> Void)?
        /// Mirror of `@Environment(\.accessibilityReduceMotion)` from
        /// the SwiftUI parent. Refreshed by `updateNSView` so the
        /// scroll-routing methods always see the current value (WCAG
        /// 2.3.1, audit #301).
        var reduceMotion: Bool = false
        private weak var textView: NSTextView?
        private var subscriptions: Set<AnyCancellable> = []
        /// Cached embed spans for the current buffer state. Updated
        /// by `applyEmbedHighlighting` on every text change so
        /// `openEmbedPreviewAtCursor` can answer "what embed is the
        /// cursor inside" in O(spans) without re-running the regex.
        private(set) var embedSpans: [EditorEmbedSpan] = []

        init(
            text: Binding<String>,
            onSave: @escaping () -> Void,
            previewEmbedAtCursor: ((String, Int) -> Void)?
        ) {
            self._text = text
            self.onSave = onSave
            self.previewEmbedAtCursor = previewEmbedAtCursor
        }

        /// Re-bind point. Called from `makeNSView` on initial setup
        /// and re-callable for SwiftUI lifecycle paths that hand the
        /// same coordinator a different `NSTextView` instance (audit
        /// #233). Resets the new view's typing attributes to a known
        /// state and strips any storage-level `.foregroundColor`
        /// that might be left over from a prior bind, so the dynamic
        /// `NSColor.textColor` AppKit relies on for dark-mode
        /// rendering isn't shadowed (the failure pattern from
        /// #226). Observer registration is idempotent — removing
        /// first lets repeated `attach` calls not double-fire.
        func attach(textView: NSTextView) {
            self.textView = textView

            // Reset typing attributes + any inherited foreground
            // color so the view starts in a known state. Uses
            // textView.font as the source of truth so wrappers that
            // installed a custom font keep it.
            let font = textView.font ?? NSFont.monospacedSystemFont(
                ofSize: NSFont.systemFontSize,
                weight: .regular
            )
            textView.typingAttributes = [
                .font: font,
                .foregroundColor: NSColor.textColor,
            ]
            if let storage = textView.textStorage {
                let fullRange = NSRange(location: 0, length: storage.length)
                storage.removeAttribute(.foregroundColor, range: fullRange)
            }
            // Explicit textColor: AppKit's NSTextView leaves `textColor` as nil after
            // the standard init when the storage has no `.foregroundColor` attribute
            // (#226 strips that attribute so the dynamic color resolves correctly).
            // With both nil, the live rendering falls back to a dimmer color than
            // `NSColor.textColor` resolves to — empirically a near-secondary
            // brightness against `textBackgroundColor` in dark mode, which reads as
            // grayish-on-dark instead of the WCAG-compliant pure-white contrast.
            // Stamping `textColor` directly forces the dynamic system text color and
            // re-runs at every `attach` so an appearance change picks up the fresh
            // value.
            textView.textColor = NSColor.textColor

            // Re-run the highlight pass when the user toggles Increase
            // Contrast or changes accent color so the embed underline
            // adapts. Audit #230: `NSColor.controlAccentColor` is
            // ~3:1 against `textBackgroundColor` on the default Blue
            // accent (borderline WCAG 1.4.11) and worse on Graphite —
            // when Increase Contrast is on we swap to `labelColor` for
            // a guaranteed-pass underline.
            //
            // Remove first so repeated attach calls don't stack
            // notification handlers.
            NotificationCenter.default.removeObserver(self)
            NotificationCenter.default.addObserver(
                self,
                selector: #selector(systemColorPreferencesChanged),
                name: NSWorkspace.accessibilityDisplayOptionsDidChangeNotification,
                object: nil
            )
            NotificationCenter.default.addObserver(
                self,
                selector: #selector(systemColorPreferencesChanged),
                name: NSNotification.Name("AppleAquaColorVariantChanged"),
                object: nil
            )
        }

        @objc private func systemColorPreferencesChanged() {
            applyEmbedHighlighting()
        }

        deinit {
            NotificationCenter.default.removeObserver(self)
        }

        /// Underline color for the embed highlight, picked per the
        /// system Increase Contrast setting. The accent color reads
        /// fine for users on default colour pairings but drops below
        /// 3:1 against `textBackgroundColor` on the Graphite accent +
        /// dark mode; under Increase Contrast we swap to `labelColor`
        /// which is contractually contrast-compliant against the
        /// matched background. Audit #230.
        var embedUnderlineColor: NSColor {
            Self.embedUnderlineColor(
                increaseContrast: NSWorkspace.shared
                    .accessibilityDisplayShouldIncreaseContrast
            )
        }

        /// Pure helper extracted for testability — the instance form
        /// reads `NSWorkspace.shared`, which can't be flipped from a
        /// test. This form takes the toggle directly so a unit test
        /// can verify both branches without mocking AppKit.
        static func embedUnderlineColor(increaseContrast: Bool) -> NSColor {
            increaseContrast ? NSColor.labelColor : NSColor.controlAccentColor
        }

        /// Recompute syntax + embed spans for the current buffer and
        /// apply attributes:
        ///
        /// 1. Re-stamp the entire range with `NSColor.textColor` as the
        ///    foreground so any previously-applied syntax colour
        ///    that's been edited past (e.g. the user deleted a `#`
        ///    that turned a heading back into prose) falls back to
        ///    the body colour. Same dynamic semantic colour as
        ///    `attach()` uses — preserves the #226/#302 dark-mode
        ///    contract.
        /// 2. Apply syntax foreground colours per kind from
        ///    `EditorSyntaxPalette` — each per-range stamp REPLACES
        ///    the body colour on that range. Semantic colours stay
        ///    dynamic per appearance + honor Increase Contrast via
        ///    the palette's IC branch.
        /// 3. Reset and reapply the embed underline (audit #207, #230)
        ///    to the `![[…]]` spans. This runs after the syntax
        ///    foreground pass so the embed's wikilink colour is in
        ///    place underneath the underline.
        ///
        /// Audit #207 + #214: the old embed highlight used
        /// `systemBlue` foreground which measured ~4.0:1 against
        /// the default editor background (fails WCAG 1.4.3). The
        /// underline-only approach for embeds stays — only the
        /// per-kind syntax colours from #296 use foreground, and
        /// the palette's IC branch collapses everything to
        /// `labelColor` for guaranteed contrast.
        ///
        /// NSTextStorage's attribute layer works even with
        /// `isRichText = false` — that flag restricts what the user
        /// can do via the font panel / paste-with-formatting, not
        /// what we set programmatically. VoiceOver doesn't emit
        /// colour attributes; the highlight is purely a sighted-
        /// user affordance, and AT users still hear the raw text.
        func applySyntaxHighlighting() {
            guard let textView, let storage = textView.textStorage else { return }
            let source = textView.string
            let nsSource = source as NSString
            let fullRange = NSRange(location: 0, length: nsSource.length)
            let syntaxSpans = findEditorSyntaxSpans(in: source)
            let embeds = findEditorEmbedSpans(in: source)
            embedSpans = embeds

            storage.beginEditing()
            defer { storage.endEditing() }
            // 1. Re-stamp body colour. Using addAttribute (not
            // setAttributes) so we don't blow away the font / typing
            // attributes already on the range.
            storage.addAttribute(.foregroundColor, value: NSColor.textColor, range: fullRange)
            // 2. Syntax foreground per kind.
            for span in syntaxSpans {
                let clamped = NSIntersectionRange(span.range, fullRange)
                guard clamped.length > 0 else { continue }
                let color = EditorSyntaxPalette.color(for: span.kind)
                storage.addAttribute(.foregroundColor, value: color, range: clamped)
            }
            // 3. Embed underline (#207, #230). Reset first so spans
            // typed past lose their underline.
            storage.removeAttribute(.underlineStyle, range: fullRange)
            storage.removeAttribute(.underlineColor, range: fullRange)
            let underlineAttrs: [NSAttributedString.Key: Any] = [
                .underlineStyle: NSUnderlineStyle.single.rawValue,
                .underlineColor: embedUnderlineColor,
            ]
            for embed in embeds {
                let clamped = NSIntersectionRange(embed.range, fullRange)
                if clamped.length > 0 {
                    storage.addAttributes(underlineAttrs, range: clamped)
                }
            }
        }

        /// Back-compat alias for the previous embed-only entry point
        /// — tests call this name; coordinator code paths inside
        /// this file have moved to `applySyntaxHighlighting`. Same
        /// behaviour either way; both run the full syntax + embed
        /// pass.
        func applyEmbedHighlighting() { applySyntaxHighlighting() }

        /// Cmd+E entry point — called by `SlateEditorTextView`'s
        /// `performKeyEquivalent(with:)` when the user hits the
        /// shortcut. Finds the embed span containing the cursor
        /// and invokes `previewEmbedAtCursor` with its target.
        /// When the cursor isn't inside any embed, posts a polite
        /// announcement so the user knows the shortcut fired but
        /// found nothing.
        ///
        /// Returns `true` when the shortcut was meaningful (cursor
        /// was inside an embed, callback fired). The subclass uses
        /// the return value to decide whether to swallow the event
        /// or let it fall through to NSTextView's default Cmd+E
        /// behaviour ("Use Selection For Find").
        @discardableResult
        func openEmbedPreviewAtCursor() -> Bool {
            guard let textView, let callback = previewEmbedAtCursor else {
                return false
            }
            let cursor = textView.selectedRange().location
            if let span = embedSpanContaining(cursor: cursor, in: embedSpans) {
                let line = oneBasedLineForUTF16Offset(
                    cursor,
                    in: textView.string
                )
                callback(span.target, line)
                return true
            }
            postAccessibilityAnnouncement(
                "No embed at cursor.",
                priority: .medium
            )
            return false
        }

        /// Convert a UTF-16 buffer offset to a 1-based line number
        /// for the popover header's spatial-bearing cue (audit
        /// #209). Counts `\n` from the start of the buffer up to
        /// the offset; clamps to last line on overshoot.
        private func oneBasedLineForUTF16Offset(_ offset: Int, in source: String) -> Int {
            let utf16 = source.utf16
            let safeOffset = min(max(offset, 0), utf16.count)
            var line = 1
            var idx = utf16.startIndex
            let end = utf16.index(utf16.startIndex, offsetBy: safeOffset)
            while idx < end {
                if utf16[idx] == UInt16(0x0A) {  // `\n`
                    line += 1
                }
                idx = utf16.index(after: idx)
            }
            return line
        }

        func subscribe(
            scrollAnchorRequest: AnyPublisher<String, Never>,
            lineScrollRequest: AnyPublisher<Int, Never>,
            cursorByteOffsetRequest: AnyPublisher<Int, Never>
        ) {
            // Cancel and re-subscribe on every makeNSView pass so a
            // recycled coordinator doesn't accumulate duplicate
            // subscriptions across SwiftUI rebuilds.
            subscriptions.removeAll()
            scrollAnchorRequest
                .sink { [weak self] anchor in
                    self?.scrollToHeadingAnchor(anchor)
                }
                .store(in: &subscriptions)
            lineScrollRequest
                .sink { [weak self] line in
                    self?.scrollToLine(line)
                }
                .store(in: &subscriptions)
            cursorByteOffsetRequest
                .sink { [weak self] offset in
                    self?.placeCursorAtByteOffset(offset)
                }
                .store(in: &subscriptions)
        }

        // MARK: - NSTextViewDelegate

        func textDidChange(_ notification: Notification) {
            guard let textView = notification.object as? NSTextView else {
                return
            }
            // Route the new buffer back through the SwiftUI binding,
            // which goes through `AppState.updateEditorText` and
            // recomputes `hasUnsavedChanges`.
            text = textView.string
            // Re-highlight embed spans for the new buffer state.
            // Cheap to redo from scratch — the regex scan over a
            // 100k-character buffer is sub-millisecond and AppKit's
            // editing transactions batch attribute updates without
            // forcing layout each iteration.
            applyEmbedHighlighting()
        }

        // MARK: - Scroll routing

        /// `NSTextView.scrollRangeToVisible(_:)` routes through the
        /// enclosing scroll view's animator proxy, which by default
        /// produces a brief spring animation on macOS. When the
        /// system Reduce Motion preference is on, vestibular-
        /// sensitive users need the scroll to land instantly (WCAG
        /// 2.3.1). Wrapping the call in an `NSAnimationContext`
        /// group with `duration = 0` forces the animator path to
        /// resolve immediately; under default prefs we pass through
        /// to the unwrapped call so the visual continuity of the
        /// animated scroll is preserved for non-AT users.
        ///
        /// Pure helper extracted for testability — the instance
        /// scroll methods read `self.reduceMotion`; the static form
        /// takes the toggle directly so a unit test can drive both
        /// branches without going through SwiftUI's
        /// `@Environment` plumbing.
        ///
        /// `@MainActor` because every caller path (NSTextView mutation,
        /// scroll-view animator, NSAnimationContext) is main-thread-only.
        /// Explicit annotation makes the contract visible at the call
        /// site instead of relying on the enclosing Coordinator's
        /// main-actor isolation by inheritance.
        @MainActor
        static func scrollRangeToVisible(
            _ range: NSRange,
            in textView: NSTextView,
            reduceMotion: Bool
        ) {
            if reduceMotion {
                NSAnimationContext.runAnimationGroup { ctx in
                    ctx.duration = 0
                    ctx.allowsImplicitAnimation = false
                    textView.scrollRangeToVisible(range)
                }
            } else {
                textView.scrollRangeToVisible(range)
            }
        }

        private func scrollRangeToVisibleRespectingReduceMotion(_ range: NSRange) {
            guard let textView else { return }
            Self.scrollRangeToVisible(range, in: textView, reduceMotion: reduceMotion)
        }

        private func scrollToHeadingAnchor(_ anchor: String) {
            guard let textView else { return }
            guard let heading = headings.first(where: { $0.anchorId == anchor })
            else { return }
            // First occurrence of the heading text within the
            // buffer. Markdown headings repeat the text as raw
            // characters ("# Heading"), so finding the heading
            // string is almost always the right scroll target.
            // The anchor-id dedup logic (`heading-2`, `heading-3`)
            // means duplicates differ by suffix, but the raw
            // heading text in the source repeats verbatim — picking
            // the first match here is approximate when a heading
            // text appears twice. Outline-sidebar testers can flag
            // a per-occurrence anchor mapping if it matters; for
            // V1.F the first-match heuristic matches what most
            // users would expect from VoiceOver-driven outline
            // navigation.
            let ns = textView.string as NSString
            let needle = heading.text as String
            let range = ns.range(of: needle)
            guard range.location != NSNotFound else { return }
            scrollRangeToVisibleRespectingReduceMotion(range)
            textView.setSelectedRange(NSRange(location: range.location, length: 0))
        }

        /// Park the caret at the byte offset (UTF-8) supplied by the
        /// create-from-template flow's `RenderedTemplate.cursor_byte_offset`.
        /// `NSTextView.selectedRange` is UTF-16-indexed, so we convert
        /// the byte offset to a UTF-16 distance from the start of the
        /// buffer before talking to AppKit.
        ///
        /// Defensively clamps to the buffer's byte length: a template
        /// whose `{{cursor}}` offset somehow landed past EOF (e.g. a
        /// shrinking edit between render and load) parks the caret at
        /// the end of the buffer rather than crashing.
        private func placeCursorAtByteOffset(_ offset: Int) {
            guard let textView else { return }
            let source = textView.string
            let bytes = source.utf8
            let safeByteOffset = max(0, min(offset, bytes.count))
            let byteIdx = bytes.index(bytes.startIndex, offsetBy: safeByteOffset)
            // A scalar boundary may not coincide with the byte
            // offset under pathological inputs (the render path's
            // contract is to land on a UTF-8 boundary, but be
            // generous on the consumer side). Fall back to the end
            // of the buffer if conversion fails.
            let strIdx = byteIdx.samePosition(in: source) ?? source.endIndex
            let utf16Idx = strIdx.samePosition(in: source.utf16) ?? source.utf16.endIndex
            let location = source.utf16.distance(
                from: source.utf16.startIndex,
                to: utf16Idx
            )
            let range = NSRange(location: location, length: 0)
            scrollRangeToVisibleRespectingReduceMotion(range)
            textView.setSelectedRange(range)
        }

        private func scrollToLine(_ line: Int) {
            guard let textView else { return }
            let target = max(1, line)
            let source = textView.string
            var lineNumber = 1
            var currentLineStart = source.startIndex
            // Walk newline-by-newline. UTF-8 string slicing on
            // `Swift.String` is O(n) in the worst case; for editor
            // buffers (typically < 100k characters) this is well
            // under a frame budget. Avoiding `components(separatedBy:)`
            // saves the per-line `String` allocations.
            for index in source.indices {
                if lineNumber == target {
                    currentLineStart = index
                    break
                }
                if source[index] == "\n" {
                    lineNumber += 1
                }
            }
            if lineNumber != target {
                // Buffer ended before reaching the requested line —
                // park the caret at the end so the user lands at
                // something coherent rather than nothing happening.
                currentLineStart = source.endIndex
            }
            let location = source.utf16.distance(
                from: source.utf16.startIndex,
                to: currentLineStart.samePosition(in: source.utf16) ?? source.utf16.endIndex
            )
            let range = NSRange(location: location, length: 0)
            scrollRangeToVisibleRespectingReduceMotion(range)
            textView.setSelectedRange(range)
        }
    }
}

/// `NSTextView` subclass that intercepts Cmd+E for the
/// embed-preview popover (#188). Routes the shortcut to the
/// coordinator; everything else (typing, selection, VoiceOver,
/// drag-and-drop, etc.) falls through to NSTextView's native
/// handling — the rich AT behaviour the editor inherits stays
/// untouched.
///
/// Held by the scroll view's `documentView`; the wrapper sets
/// `coordinator` after construction so the keyDown handler has
/// a route back to the SwiftUI layer.
final class SlateEditorTextView: NSTextView {
    weak var coordinator: NoteEditorView.Coordinator?

    override func performKeyEquivalent(with event: NSEvent) -> Bool {
        // Cmd+E: open the embed-preview popover for the embed
        // under the cursor. Always swallowed (returns `true`)
        // even when no embed is at the cursor — letting Cmd+E
        // fall through to NSTextView's default ("Use Selection
        // For Find") silently mutates the system find pasteboard
        // and the user has no idea why a later Cmd+F search has
        // mystery contents in the field (audit #208).
        if event.modifierFlags.intersection(.deviceIndependentFlagsMask) == .command,
            event.charactersIgnoringModifiers == "e"
        {
            coordinator?.openEmbedPreviewAtCursor()
            return true
        }
        return super.performKeyEquivalent(with: event)
    }
}
