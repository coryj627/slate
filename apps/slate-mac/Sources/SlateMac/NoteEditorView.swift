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
    /// the cursor is currently inside. Hooked at the SwiftUI layer
    /// to `AppState.requestEmbedPreview(target:)` so the popover
    /// opens via `pendingEmbedPreview`. Nil disables the
    /// shortcut — used by the previewless contexts that include
    /// the editor (none today, kept for future surfaces).
    let previewEmbedAtCursor: ((String) -> Void)?

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

    final class Coordinator: NSObject, NSTextViewDelegate {
        @Binding var text: String
        var onSave: () -> Void
        var headings: [Heading] = []
        var accessibilityLabel: String = ""
        var previewEmbedAtCursor: ((String) -> Void)?
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
            previewEmbedAtCursor: ((String) -> Void)?
        ) {
            self._text = text
            self.onSave = onSave
            self.previewEmbedAtCursor = previewEmbedAtCursor
        }

        func attach(textView: NSTextView) {
            self.textView = textView
        }

        /// Recompute embed spans for the current buffer and apply
        /// highlight attributes (foreground color + underline) to
        /// each. Resets attributes on the full range first so
        /// spans the user typed past lose their highlight.
        ///
        /// NSTextStorage's attribute layer works even with
        /// `isRichText = false` — that flag restricts what the
        /// user can do via the font panel / paste-with-formatting,
        /// not what we set programmatically. VoiceOver doesn't
        /// emit colour attributes; the highlight is purely a
        /// sighted-user affordance, and AT users still hear the
        /// raw `![[…]]` text.
        func applyEmbedHighlighting() {
            guard let textView, let storage = textView.textStorage else { return }
            let source = textView.string
            let nsSource = source as NSString
            let fullRange = NSRange(location: 0, length: nsSource.length)
            let spans = findEditorEmbedSpans(in: source)
            embedSpans = spans
            storage.beginEditing()
            storage.removeAttribute(.foregroundColor, range: fullRange)
            storage.removeAttribute(.underlineStyle, range: fullRange)
            let attrs: [NSAttributedString.Key: Any] = [
                .foregroundColor: NSColor.systemBlue,
                .underlineStyle: NSUnderlineStyle.single.rawValue,
            ]
            for span in spans {
                let clamped = NSIntersectionRange(span.range, fullRange)
                if clamped.length > 0 {
                    storage.addAttributes(attrs, range: clamped)
                }
            }
            storage.endEditing()
        }

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
                callback(span.target)
                return true
            }
            postAccessibilityAnnouncement(
                "No embed at cursor.",
                priority: .medium
            )
            return false
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
            textView.scrollRangeToVisible(range)
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
            textView.scrollRangeToVisible(range)
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
            textView.scrollRangeToVisible(range)
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
        // under the cursor. Falls through to NSTextView's
        // default (which has its own Cmd+E meaning depending on
        // the responder chain) when no embed is at the cursor.
        if event.modifierFlags.intersection(.deviceIndependentFlagsMask) == .command,
            event.charactersIgnoringModifiers == "e"
        {
            if coordinator?.openEmbedPreviewAtCursor() == true {
                return true
            }
        }
        return super.performKeyEquivalent(with: event)
    }
}
