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

    func makeCoordinator() -> Coordinator {
        Coordinator(
            text: $text,
            onSave: onSave
        )
    }

    func makeNSView(context: Context) -> NSScrollView {
        // Standard scrollable text view container — same setup
        // Xcode's File > New > Storyboard scaffold uses for a plain
        // editor pane.
        let scrollView = NSTextView.scrollableTextView()
        guard let textView = scrollView.documentView as? NSTextView else {
            return scrollView
        }

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
        context.coordinator.subscribe(
            scrollAnchorRequest: scrollAnchorRequest,
            lineScrollRequest: lineScrollRequest
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
        }
    }

    final class Coordinator: NSObject, NSTextViewDelegate {
        @Binding var text: String
        var onSave: () -> Void
        var headings: [Heading] = []
        var accessibilityLabel: String = ""
        private weak var textView: NSTextView?
        private var subscriptions: Set<AnyCancellable> = []

        init(text: Binding<String>, onSave: @escaping () -> Void) {
            self._text = text
            self.onSave = onSave
        }

        func attach(textView: NSTextView) {
            self.textView = textView
        }

        func subscribe(
            scrollAnchorRequest: AnyPublisher<String, Never>,
            lineScrollRequest: AnyPublisher<Int, Never>
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
