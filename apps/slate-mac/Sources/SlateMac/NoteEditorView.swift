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
    /// False renders the buffer read-only (U1-3 unfocused panes): full
    /// editor visuals — font, highlighting, selection-for-copy — but no
    /// text input, no caret parking, and no first-responder claim on
    /// mount. The binding's setter never fires (AppKit blocks edits at
    /// the `isEditable` gate), so no dirty-tracking path exists.
    var isEditable: Bool = true
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
    /// Continuous caret report (U3-2): fires with the caret's RAW UTF-16
    /// location on every selection change (and once on attach/restamp, so
    /// a fresh mount is never unreported — Codoki #515), letting AppState
    /// park the caret for the reading-mode round trip without reaching
    /// into AppKit at toggle time. The handoff is a plain Int — the
    /// UTF-8 conversion happens ONCE at toggle time in AppState, never
    /// here: a per-keystroke rope build over the whole document is the
    /// exact O(n)-per-keystroke class #404 eliminated. Nil disables.
    /// The value lands in a plain stored var (never a `@Published`) —
    /// selection churn must not invalidate any view.
    var onCaretUTF16Change: ((Int) -> Void)? = nil

    /// System Reduce Motion preference (WCAG 2.3.1). When `true`, the
    /// editor's scroll-routing paths jump instantly instead of using
    /// `NSTextView`'s default animated scroll — vestibular-sensitive
    /// users avoid the spring-loaded re-position when activating an
    /// outline row, scrolling to a line, or landing on a template
    /// cursor offset. Read from `@Environment` so SwiftUI re-pushes
    /// the value through `updateNSView` whenever the system pref
    /// changes mid-session.
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    /// System Text Size dependency (WCAG 1.4.4) — read in
    /// `updateNSView` so the editor font re-derives when the user
    /// changes System Settings ▸ Accessibility ▸ Display ▸ Text Size,
    /// exactly as the SwiftUI surfaces rescale automatically.
    @Environment(\.dynamicTypeSize) private var dynamicTypeSize

    func makeCoordinator() -> Coordinator {
        Coordinator(
            text: $text,
            onSave: onSave,
            previewEmbedAtCursor: previewEmbedAtCursor,
            onCaretUTF16Change: onCaretUTF16Change
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
        textView.isEditable = isEditable
        textView.isSelectable = true
        textView.isRichText = false
        textView.allowsUndo = isEditable
        textView.isAutomaticQuoteSubstitutionEnabled = false
        textView.isAutomaticDashSubstitutionEnabled = false
        textView.isAutomaticTextReplacementEnabled = false
        textView.isAutomaticSpellingCorrectionEnabled = false
        textView.isAutomaticLinkDetectionEnabled = false
        textView.smartInsertDeleteEnabled = false
        textView.usesFontPanel = false
        textView.usesRuler = false
        // Body-text-style size, NOT `NSFont.systemFontSize` — the
        // latter is a fixed 13pt constant that ignores the macOS
        // Text Size setting, which left the WRITING surface pinned
        // while the reading surface (Dynamic-Type-backed
        // Tokens.Typography) scaled. `updateNSView` re-derives on
        // Dynamic Type changes so the scaling is live (WCAG 1.4.4).
        textView.font = Tokens.Typography.monospacedBodyNSFont()
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
        // ⌘S is owned by File ▸ Save (SlateMacApp's CommandGroup) —
        // menu-bar-homed so it works regardless of which pane has
        // focus (the #422 lesson; it previously rode a toolbar
        // button's keyboardShortcut, dead with sidebar focus). The
        // `onSave` argument stays here so future per-view shortcuts
        // (autosave on focus loss) have a single place to call into.

        textView.setAccessibilityLabel(accessibilityLabel)
        textView.setAccessibilityRole(.textArea)

        // Initial buffer sync. Subsequent updates come through
        // `updateNSView`. (The storage delegate isn't attached until
        // `attach` below, so this initial set fires no dirty-tracking
        // callback — but route it through the suppression path anyway so
        // the contract "every programmatic `string =` resets dirt" holds
        // regardless of attach ordering.)
        context.coordinator.withSuppressedDirtyTracking {
            if textView.string != text {
                textView.string = text
            }
            // NSTextView leaves the insertion point at the END of the
            // buffer after a `string =` swap. Pin it to the start so
            // VoiceOver enters the editor at the top of the note (and a
            // sighted caret starts at line 1) instead of the bottom.
            // `updateNSView`'s external-swap path sets the selection
            // explicitly (clamped to the prior location); the initial
            // mount — which is what runs on every file selection, since
            // the editor is torn down and rebuilt — needs its own reset
            // because there's no prior selection to preserve. A pending
            // {{cursor}} park, if any, arrives later via
            // `cursorByteOffsetRequest` and overrides this.
            textView.setSelectedRange(NSRange(location: 0, length: 0))
            // Setting the selection doesn't move the viewport. Scroll the
            // top into view so the caret is actually visible on entry. A
            // freshly-built NSTextView is already scrolled to the top, so
            // this is a no-op in the common case — but it's cheap insurance,
            // and scrolling to offset 0 never animates, so there's no
            // Reduce Motion (WCAG 2.3.1) concern.
            textView.scrollRangeToVisible(NSRange(location: 0, length: 0))
        }
        // Seed the coordinator with the initial Reduce Motion value
        // — updateNSView will refresh on changes, but the first
        // scroll-request fired before SwiftUI runs its first update
        // pass still needs a defined value.
        context.coordinator.reduceMotion = reduceMotion
        context.coordinator.attach(textView: textView)
        context.coordinator.scheduleHighlight(debounced: false)
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
        context.coordinator.onCaretUTF16Change = onCaretUTF16Change
        textView.setAccessibilityLabel(accessibilityLabel)

        // Live Text Size tracking (WCAG 1.4.4): reading
        // `dynamicTypeSize` registers it as a dependency, so this
        // method re-runs when the system setting changes (the
        // reduce-motion pattern above); the font re-derives at the
        // new body-style size. Point-size compare keeps the common
        // keystroke path allocation-free.
        _ = dynamicTypeSize
        let baseFont = Tokens.Typography.monospacedBodyNSFont()
        if textView.font?.pointSize != baseFont.pointSize {
            textView.font = baseFont
            context.coordinator.scheduleHighlight(debounced: false)
        }

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
            // Suppress dirty tracking around the swap — `string =` fires
            // `didProcessEditing` spanning the whole new buffer, which would
            // otherwise set `dirtyRange` to the entire 2 MB reload and
            // degrade the next keystroke to a whole-document recompute
            // (red-team C3). The swap is repainted whole-doc just below.
            context.coordinator.withSuppressedDirtyTracking {
                textView.string = text
            }
            // Re-establish the dynamic body colour on the freshly-swapped
            // storage. The highlight overlay is now display-only
            // (temporary attributes), so it no longer re-stamps the
            // storage foreground the way the old pass did — without this,
            // a buffer rebuilt by `string =` could drop back to the nil
            // `textColor` that reads dim-on-dark (#226/#302).
            textView.textColor = NSColor.textColor
            let clampedLocation = min(previousRange.location, text.utf16.count)
            textView.setSelectedRange(NSRange(location: clampedLocation, length: 0))
            context.coordinator.scheduleHighlight(debounced: false)
        }
        // U3-2 (Codoki #515): after any restamp/update pass, re-report the
        // caret so the parked value can never lag a programmatic content
        // swap (cheap: a raw Int handoff).
        context.coordinator.reportCaretLocation()
    }

    @MainActor
    final class Coordinator: NSObject, NSTextViewDelegate, NSTextStorageDelegate {
        @Binding var text: String
        var onSave: () -> Void
        var headings: [Heading] = []
        var accessibilityLabel: String = ""
        var previewEmbedAtCursor: ((String, Int) -> Void)?
        /// Continuous caret reporter (U3-2) — see the view property.
        var onCaretUTF16Change: ((Int) -> Void)?
        /// Mirror of `@Environment(\.accessibilityReduceMotion)` from
        /// the SwiftUI parent. Refreshed by `updateNSView` so the
        /// scroll-routing methods always see the current value (WCAG
        /// 2.3.1, audit #301).
        var reduceMotion: Bool = false
        private weak var textView: NSTextView?
        private var subscriptions: Set<AnyCancellable> = []
        /// In-flight debounced re-highlight (#376). Each
        /// `scheduleHighlight` cancels the previous one, so a burst of
        /// keystrokes collapses into a single span computation. Held so
        /// `deinit` can cancel a pass that outlived the view — and so
        /// tests can `await` a deterministic completion.
        private(set) var highlightTask: Task<Void, Never>?

        /// Accumulated edited region since the last successful highlight
        /// apply, in **current-buffer UTF-16** coords — the `dirty` range
        /// the #379 ranged highlighter scopes the recompute to. Maintained
        /// by `textStorage(_:didProcessEditing:…)` via `shiftAndUnion`
        /// (conservative: never under-covers). `nil` = nothing dirty → a
        /// debounced pass falls through to whole-document. Reset to `nil`
        /// only on a successful whole-doc apply or a buffer swap; a windowed
        /// apply *subtracts* its applied range (so a net-zero edit burst's
        /// surplus dirt survives — red-team C4).
        private var dirtyRange: NSRange?

        /// Re-entrancy guard: `NSTextStorageDelegate.didProcessEditing`
        /// fires for **programmatic** `string =` swaps too, indistinguishable
        /// from a keystroke. Set around every programmatic mutation so a
        /// 2 MB reload doesn't blow `dirtyRange` up to the whole buffer and
        /// silently degrade the next keystroke to whole-document (red-team
        /// C3).
        private var suppressDirtyTracking = false

        /// Stateful mirror of the live text store (#404). Fed the same edit
        /// deltas the storage receives (in `textStorage(_:didProcessEditing:…)`)
        /// and rebuilt wholesale on every programmatic swap
        /// (`withSuppressedDirtyTracking`) / re-bind (`attach`). The keystroke
        /// highlight path calls `highlightInRange` on it instead of
        /// re-marshalling the whole document over FFI per pass, and gets
        /// O(log n) UTF-16 ↔ byte conversions off the live rope. A length-based
        /// drift guard in `scheduleHighlight` re-syncs it from the text store if
        /// a delta is ever missed, so a desync self-heals rather than silently
        /// mis-colouring.
        private var documentBuffer: DocumentBuffer?

        init(
            text: Binding<String>,
            onSave: @escaping () -> Void,
            previewEmbedAtCursor: ((String, Int) -> Void)?,
            onCaretUTF16Change: ((Int) -> Void)? = nil
        ) {
            self._text = text
            self.onSave = onSave
            self.previewEmbedAtCursor = previewEmbedAtCursor
            self.onCaretUTF16Change = onCaretUTF16Change
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
            // Re-point the text-storage delegate to the (possibly new)
            // view's storage so dirty-range tracking follows the live
            // buffer (audit #233 re-bind path). Clear the old one first so a
            // stray edit on a detached view can't extend `dirtyRange` for a
            // buffer this coordinator no longer shows (red-team M4). Reset
            // the dirty range — the new buffer is highlighted whole-document
            // by the `scheduleHighlight(debounced: false)` that follows.
            self.textView?.textStorage?.delegate = nil
            self.textView = textView
            textView.textStorage?.delegate = self
            dirtyRange = nil
            // (Re)build the mirror buffer from the (possibly new) view's text
            // (#404). The new buffer is highlighted whole-document by the
            // `scheduleHighlight(debounced: false)` that follows every attach.
            documentBuffer = DocumentBuffer(text: textView.string)

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
            // notification handlers. MUST be per-name removals: a
            // blanket `removeObserver(self)` also tears down the
            // NSText.didChangeNotification subscription AppKit
            // registered on this coordinator when it became the
            // text view's delegate (`textView.delegate =` in
            // `makeNSView` runs before `attach`). With that
            // subscription gone, `textDidChange` never fires, the
            // buffer→`updateEditorText` sync dies, `hasUnsavedChanges`
            // stays false, and ⌘S — gated on the dirty flag — is
            // silently inert: the #409 data-loss bug.
            // #416: register on the centers these notifications are
            // ACTUALLY posted to. accessibilityDisplayOptionsDidChange
            // is posted to NSWorkspace.shared.notificationCenter (per
            // the SDK header), and accent-color changes arrive as
            // NSColor.systemColorsDidChangeNotification on the default
            // center — the previous default-center registration of the
            // workspace name (plus the distributed-only
            // "AppleAquaColorVariantChanged" name) never fired, so
            // mid-session Increase Contrast / accent toggles didn't
            // repaint until the next edit.
            NSWorkspace.shared.notificationCenter.removeObserver(
                self,
                name: NSWorkspace.accessibilityDisplayOptionsDidChangeNotification,
                object: nil
            )
            NotificationCenter.default.removeObserver(
                self,
                name: NSColor.systemColorsDidChangeNotification,
                object: nil
            )
            NSWorkspace.shared.notificationCenter.addObserver(
                self,
                selector: #selector(systemColorPreferencesChanged),
                name: NSWorkspace.accessibilityDisplayOptionsDidChangeNotification,
                object: nil
            )
            NotificationCenter.default.addObserver(
                self,
                selector: #selector(systemColorPreferencesChanged),
                name: NSColor.systemColorsDidChangeNotification,
                object: nil
            )

            // U3-2 (Codoki #515): a freshly attached editor reports its
            // caret immediately — the {0,0} mount reset happens before the
            // delegate wiring, so without this a mount followed directly by
            // a reading-mode toggle would park a stale location.
            reportCaretLocation()
        }

        @objc private func systemColorPreferencesChanged() {
            // Colours (palette + embed underline) are resolved per
            // appearance / Increase Contrast at apply time, so an
            // immediate re-highlight repaints with the new values
            // without waiting for an edit. Spans are unchanged, but
            // recomputing them off-main is cheap enough not to special-
            // case (the ranged/colour-only refresh is #379).
            scheduleHighlight(debounced: false)
        }

        deinit {
            NotificationCenter.default.removeObserver(self)
            // #416: the contrast observer lives on the workspace
            // center; the default-center blanket removal above does
            // not touch it.
            NSWorkspace.shared.notificationCenter.removeObserver(self)
            highlightTask?.cancel()
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

        /// Debounce interval before a keystroke-triggered re-highlight
        /// runs. ~40 ms coalesces a burst of typing into one span
        /// computation while staying well under the threshold where a
        /// pause reads as lag. The search debouncer in `AppState` uses
        /// 150 ms; highlighting is cheaper and wants to feel more
        /// immediate, so it's shorter (#376).
        private static let highlightDebounceNanos: UInt64 = 40_000_000

        /// `NSTextStorageDelegate` — accumulate the edited region so the
        /// debounced pass can recompute only a window around it (#379).
        /// Fires for **every** storage mutation, including programmatic
        /// `string =` swaps; `suppressDirtyTracking` filters those out so a
        /// reload doesn't grow `dirtyRange` to the whole buffer (C3).
        func textStorage(
            _ textStorage: NSTextStorage,
            didProcessEditing editedMask: NSTextStorageEditActions,
            range editedRange: NSRange,
            changeInLength delta: Int
        ) {
            guard editedMask.contains(.editedCharacters), !suppressDirtyTracking else { return }
            dirtyRange = Self.shiftAndUnion(dirtyRange, editedRange: editedRange, delta: delta)
            // Feed the same delta to the mirror buffer (#404). `editedRange` is
            // post-edit UTF-16 coords, so the replacement text is the storage's
            // current substring over it; the replaced (pre-edit) length is
            // `editedRange.length - delta` (delta = new − old, always ≥ 0).
            let inserted = (textStorage.string as NSString).substring(with: editedRange)
            documentBuffer?.applyEdit(
                startUtf16: UInt32(clamping: editedRange.location),
                oldLenUtf16: UInt32(clamping: editedRange.length - delta),
                newText: inserted)
        }

        /// Run a programmatic buffer mutation (a `textView.string = …` swap)
        /// with dirty tracking suppressed, so the synchronous
        /// `didProcessEditing` it fires isn't mistaken for a keystroke and
        /// doesn't balloon `dirtyRange` to the whole buffer (red-team C3).
        /// Resets the dirty range — a swap is always followed by a
        /// whole-document `scheduleHighlight(debounced: false)`.
        func withSuppressedDirtyTracking(_ body: () -> Void) {
            suppressDirtyTracking = true
            body()
            suppressDirtyTracking = false
            dirtyRange = nil
            // A programmatic swap is a whole-document replacement, not a delta,
            // and the suppressed `didProcessEditing` fed none — rebuild the
            // mirror buffer from the swapped-in text so it stays in lockstep
            // (#404; the silent-post-reload desync the red-team flagged).
            if let textView { documentBuffer?.reset(text: textView.string) }
        }

        /// Fold a new edit into the accumulated dirty range, keeping it in
        /// the **post-edit** buffer's UTF-16 coords: bounds at/after the
        /// edit shift by `delta`, then union with `editedRange`.
        /// Inversion-safe — a large delete can drive a shifted bound past
        /// another, which must clamp to a non-negative length rather than
        /// wrap `NSRange.length` (red-team H1). Conservative: over-covers,
        /// never under-covers.
        static func shiftAndUnion(_ prior: NSRange?, editedRange: NSRange, delta: Int) -> NSRange {
            guard let prior else { return editedRange }
            let editStart = editedRange.location
            func shift(_ p: Int) -> Int { p >= editStart ? max(editStart, p + delta) : p }
            let lo = min(shift(prior.location), editedRange.location)
            let hi = max(shift(prior.location + prior.length), editedRange.location + editedRange.length)
            return NSRange(location: max(0, lo), length: max(0, hi - lo))
        }

        /// Remove the applied window from the dirty range after a windowed
        /// apply, returning the residual dirt outside `applied` (a net-zero
        /// edit burst can grow `dirty` past what the pass consumed — red-team
        /// C4), or `nil` when `applied` covers it. A two-sided residual
        /// collapses to the bounding range (conservative).
        static func subtract(applied: NSRange, from dirty: NSRange?) -> NSRange? {
            guard let dirty else { return nil }
            let dLo = dirty.location, dHi = dirty.location + dirty.length
            let aLo = applied.location, aHi = applied.location + applied.length
            if aLo <= dLo && aHi >= dHi { return nil }
            let hasLeft = dLo < aLo, hasRight = dHi > aHi
            if hasLeft && hasRight { return dirty }
            if hasLeft { return NSRange(location: dLo, length: min(dHi, aLo) - dLo) }
            return NSRange(location: max(dLo, aHi), length: dHi - max(dLo, aHi))
        }

        /// Schedule a re-highlight of the current buffer (#376/#379).
        ///
        /// Two paths:
        /// - **`debounced: false`** (initial load, external buffer swap,
        ///   appearance change) → recompute the **whole document** and reset
        ///   `dirtyRange`. These aren't typing and must repaint everything.
        /// - **`debounced: true`** (the typing path) → after a ~40 ms
        ///   debounce, recompute only a **window** around `dirtyRange` via
        ///   `editorHighlightSpansInRange` (#379), and apply it windowed.
        ///   With no accumulated dirt it falls through to whole-document.
        ///
        /// Each call cancels the in-flight task (a burst collapses into one
        /// run). Compute (spans + UTF-16↔byte conversions + embed scan) runs
        /// off-main via `Task.detached`; only the AppKit apply returns to
        /// main, guarded by `textView.string == snapshot` so a result against
        /// a since-edited buffer is dropped rather than smeared over the
        /// wrong offsets. Mirrors `AppState.runSearch`'s detached shape.
        ///
        /// `dirtyRange` is cleared only on a **successful** apply (whole-doc
        /// → `nil`; window → `subtract` its applied range) so a cancelled or
        /// stale pass never loses an edit's recolor.
        func scheduleHighlight(debounced: Bool) {
            highlightTask?.cancel()
            guard let textView else { return }
            let snapshot = textView.string
            // Tier-1 drift guard (#404): the mirror buffer must match the live
            // text store. A length mismatch means a delta was missed (a
            // coalesced edit, an IME edge, or a `string =` swap that bypassed
            // `reset`) — rebuild the buffer from the live text and repaint
            // whole-document this pass, so a desync self-heals instead of
            // silently smearing colour onto the wrong offsets.
            if let buffer = documentBuffer,
                Int(buffer.lenUtf16()) != (snapshot as NSString).length
            {
                buffer.reset(text: snapshot)
                dirtyRange = nil
            }
            // Window only on the debounced typing path with accumulated
            // dirt; otherwise recompute whole-document.
            let dirty: NSRange? = debounced ? dirtyRange : nil
            // Capture `buffer` + `snapshot` as a consistent pair. The only
            // mutations of this buffer object — the drift-guard `reset` just
            // above (→ buffer == snapshot) and a reset/attach during a
            // `string =` swap — are both caught by the `textView.string ==
            // snapshot` guard below, which drops a result computed against a
            // mid-flight-reset buffer. That guard is the sole barrier (#404).
            let buffer = documentBuffer
            highlightTask = Task { @MainActor [weak self] in
                if debounced {
                    try? await Task.sleep(nanoseconds: Self.highlightDebounceNanos)
                }
                if Task.isCancelled { return }
                let prepared = await Task.detached(priority: .userInitiated) {
                    Self.computeHighlight(buffer: buffer, snapshot: snapshot, dirty: dirty)
                }.value
                guard let self, !Task.isCancelled else { return }
                guard self.textView?.string == snapshot else { return }
                self.applyHighlight(prepared)
                self.dirtyRange =
                    dirty == nil
                    ? nil
                    : Self.subtract(applied: prepared.appliedUTF16, from: self.dirtyRange)
            }
        }

        /// A computed highlight ready to stamp: the UTF-16 foreground spans,
        /// the embed underline spans, and the UTF-16 range the foreground
        /// spans authoritatively cover (`appliedUTF16` == the whole document
        /// on a whole-doc or fallback pass).
        struct PreparedHighlight {
            let mapped: [(range: NSRange, kind: EditorSpanKind)]
            let embeds: [EditorEmbedSpan]
            let appliedUTF16: NSRange
            let isWholeDocument: Bool
        }

        /// Off-main span computation. `dirty == nil` (immediate pass)
        /// recomputes the whole document; otherwise it windows the recompute
        /// to `dirty` via `editorHighlightSpansInRange` (#379) and reports
        /// the `applied_range` it covered — the Rust fallback signals itself
        /// by covering the whole document. The embed scan stays whole-doc
        /// (cheap regex, correct offsets); `applyHighlight` re-stamps embed
        /// underlines only within the cleared window.
        nonisolated static func computeHighlight(
            buffer: DocumentBuffer?, snapshot: String, dirty: NSRange?
        ) -> PreparedHighlight {
            let fullLength = (snapshot as NSString).length
            let fullRange = NSRange(location: 0, length: fullLength)
            let embeds = findEditorEmbedSpans(in: snapshot)
            // The windowed path maps the buffer's byte-offset spans against
            // `snapshot`, so it runs only when there is accumulated dirt, a live
            // buffer, and the two lengths agree (≡ the buffer mirrors the text
            // the spans get applied to). Otherwise — initial load, swap,
            // appearance refresh, or a transient/real buffer drift — recompute
            // whole-document from `snapshot`, the reliable source of truth.
            guard let dirty, let buffer, Int(buffer.lenUtf16()) == fullLength else {
                let spans = editorHighlightSpans(text: snapshot)
                return PreparedHighlight(
                    mapped: EditorSpanMapping.utf16Spans(from: spans, in: snapshot),
                    embeds: embeds, appliedUTF16: fullRange, isWholeDocument: true)
            }
            // Stateful windowed highlight: no whole-string FFI marshal, and the
            // dirty/applied conversions are O(log n) off the live rope (the four
            // per-pass `TextBuffer::from_str` rebuilds are gone).
            let ranged = buffer.highlightInRange(
                dirtyStartUtf16: UInt32(clamping: dirty.location),
                dirtyEndUtf16: UInt32(clamping: dirty.location + dirty.length))
            let aLo = Int(buffer.byteToUtf16(byte: ranged.appliedStart))
            let aHi = Int(buffer.byteToUtf16(byte: ranged.appliedEnd))
            let appliedUTF16 = NSRange(location: aLo, length: max(0, aHi - aLo))
            let whole = appliedUTF16.location == 0 && appliedUTF16.length == fullLength
            return PreparedHighlight(
                mapped: EditorSpanMapping.utf16Spans(from: ranged.spans, in: snapshot),
                embeds: embeds, appliedUTF16: appliedUTF16, isWholeDocument: whole)
        }

        /// Apply a prepared highlight to the live text view as
        /// `NSLayoutManager` temporary attributes. Main-actor only —
        /// every call touches AppKit.
        ///
        /// Temporary attributes are a **display overlay**: they don't
        /// mutate `NSTextStorage`, so there's no undo entry, no
        /// `textDidChange` re-entrancy, and no per-keystroke
        /// storage-edit/relayout cycle (#376). The storage keeps a single
        /// `NSColor.textColor` foreground run (stamped in `attach`), which
        /// shows through wherever no temporary foreground is set, so
        /// un-classified prose stays in the dynamic body colour the
        /// #226/#302 dark-mode fixes earned.
        ///
        /// #379: a windowed pass clears + re-stamps only `appliedUTF16`,
        /// **extended to cover any temporary-attribute run straddling its
        /// edges** — because temp attributes shift-track storage edits, a
        /// stale colour/underline can stretch past the (blank-bounded)
        /// window edge, and a remove confined to `appliedUTF16` would strand
        /// its tail (red-team C1/C2). The current parse's spans are all ⊆
        /// `appliedUTF16`, so clearing the extension without re-adding there
        /// is correct, not under-colour. A whole-doc / fallback pass clears
        /// the full range, as before.
        ///
        /// VoiceOver doesn't read colour/underline attributes; the highlight
        /// is purely a sighted-user affordance and AT users hear the raw
        /// text — so windowing changes only *which ranges repaint when*, not
        /// the AX tree.
        private func applyHighlight(_ prepared: PreparedHighlight) {
            guard let textView, let layoutManager = textView.layoutManager else { return }

            let fullRange = NSRange(location: 0, length: (textView.string as NSString).length)
            let increaseContrast = NSWorkspace.shared
                .accessibilityDisplayShouldIncreaseContrast
            let underlineColor = embedUnderlineColor

            let removeRange =
                prepared.isWholeDocument
                ? fullRange
                : Self.extendToCoverTemporaryRuns(
                    NSIntersectionRange(prepared.appliedUTF16, fullRange),
                    layoutManager: layoutManager, within: fullRange)

            layoutManager.removeTemporaryAttribute(.foregroundColor, forCharacterRange: removeRange)
            layoutManager.removeTemporaryAttribute(.underlineStyle, forCharacterRange: removeRange)
            layoutManager.removeTemporaryAttribute(.underlineColor, forCharacterRange: removeRange)

            // 1. Foreground per canonical kind. `nil` kinds (emphasis /
            //    strong / strikethrough, plus the never-emitted link /
            //    image / blockQuote) are skipped so they stay in body
            //    colour. `code(token:)` spans nest inside their `codeFence`
            //    — both resolve to `codeColor`, so the overlap paints the
            //    same colour. Spans are ⊆ `removeRange`; clamp to fullRange
            //    defensively.
            for (range, kind) in prepared.mapped {
                let clamped = NSIntersectionRange(range, fullRange)
                guard clamped.length > 0,
                    let color = EditorSyntaxPalette.color(
                        for: kind, increaseContrast: increaseContrast
                    )
                else { continue }
                layoutManager.addTemporaryAttribute(
                    .foregroundColor, value: color, forCharacterRange: clamped
                )
            }

            // 2. Embed underline (audit #207, #230). Re-stamp only within
            //    the cleared window — embeds outside it kept their
            //    shift-tracked underline (their text is unchanged). On a
            //    whole-doc pass `removeRange == fullRange`, so all embeds
            //    re-stamp.
            for embed in prepared.embeds {
                let clamped = NSIntersectionRange(embed.range, removeRange)
                guard clamped.length > 0 else { continue }
                layoutManager.addTemporaryAttribute(
                    .underlineStyle,
                    value: NSUnderlineStyle.single.rawValue,
                    forCharacterRange: clamped
                )
                layoutManager.addTemporaryAttribute(
                    .underlineColor, value: underlineColor, forCharacterRange: clamped
                )
            }
        }

        /// Widen `range` to fully include any temporary `.foregroundColor` /
        /// `.underlineStyle` run that straddles either edge, so a windowed
        /// remove clears a stale run that shift-tracked partly past the
        /// window rather than stranding its tail (#379 / red-team C1-C2).
        /// Runs are span-sized, so the widening is small and local.
        static func extendToCoverTemporaryRuns(
            _ range: NSRange, layoutManager: NSLayoutManager, within full: NSRange
        ) -> NSRange {
            guard full.length > 0 else { return range }
            var lo = range.location
            var hi = range.location + range.length
            let attrs: [NSAttributedString.Key] = [.foregroundColor, .underlineStyle]
            // Low edge: probe the char just before `lo`; a temp run there
            // may extend into `range` — pull `lo` back to the run's start.
            if lo > 0 && lo <= full.length {
                for attr in attrs {
                    var eff = NSRange(location: 0, length: 0)
                    if layoutManager.temporaryAttribute(
                        attr, atCharacterIndex: lo - 1, longestEffectiveRange: &eff, in: full) != nil
                    {
                        lo = min(lo, eff.location)
                    }
                }
            }
            // High edge: probe at `hi` (first char outside `range`); a temp
            // run there may have started inside — push `hi` to the run's end.
            if hi >= 0 && hi < full.length {
                for attr in attrs {
                    var eff = NSRange(location: 0, length: 0)
                    if layoutManager.temporaryAttribute(
                        attr, atCharacterIndex: hi, longestEffectiveRange: &eff, in: full) != nil
                    {
                        hi = max(hi, eff.location + eff.length)
                    }
                }
            }
            lo = max(0, lo)
            hi = min(full.length, hi)
            return NSRange(location: lo, length: max(0, hi - lo))
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
            // Recompute embeds from the LIVE buffer rather than reusing
            // the debounced highlight pass's result, so Cmd+E is correct
            // even immediately after an edit (before the ~40 ms pass
            // lands). The scan is a cheap regex over the current text, and
            // Cmd+E is a discrete keypress — no per-keystroke cost here.
            let embeds = findEditorEmbedSpans(in: textView.string)
            if let span = embedSpanContaining(cursor: cursor, in: embeds) {
                // 1-based source line for the popover header's spatial-
                // bearing cue (audit #209), via the rope `TextBuffer`
                // (#378) — was a hand-rolled O(n) `\n` walk.
                let line = EditorTextConversions.lineForUTF16Offset(
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

        /// U3-2: report the caret's raw UTF-16 location on every selection
        /// change — a plain Int handoff into a non-published AppState var
        /// (no conversion, no publish; see the view property's perf note).
        func textViewDidChangeSelection(_ notification: Notification) {
            reportCaretLocation()
        }

        /// One-shot form, also called from `attach` and the restamp path in
        /// `updateNSView`: a freshly mounted or re-stamped editor reports
        /// its caret even if no selection event ever fires before the user
        /// toggles to reading mode (Codoki #515).
        func reportCaretLocation() {
            guard let textView, let report = onCaretUTF16Change else { return }
            report(textView.selectedRange().location)
        }

        func textDidChange(_ notification: Notification) {
            guard let textView = notification.object as? NSTextView else {
                return
            }
            // Route the new buffer back through the SwiftUI binding,
            // which goes through `AppState.updateEditorText` and
            // recomputes `hasUnsavedChanges`.
            text = textView.string
            // Re-highlight on the debounced background path (#376) so the
            // keystroke itself never pays for span computation or an
            // attribute sweep — the old synchronous re-highlight here was
            // the ~182 ms/keystroke stall at 2 MB.
            scheduleHighlight(debounced: true)
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

        /// #431: position-based scroll with an honest announcement
        /// contract. The old shape searched the buffer for the
        /// heading's RENDERED text — which silently fails for
        /// headings with inline markup (`## A **bold** heading`
        /// renders as "A bold heading" and never matches the raw
        /// bytes) — while the activation announcement claimed
        /// success unconditionally. Now: the backend's byte offset
        /// is the primary target (exact, per-occurrence,
        /// markup-proof), the text search remains only as a
        /// fallback for offsets gone stale against an unsaved edit,
        /// and the announcement is posted HERE — success or honest
        /// failure — not at the request site.
        @discardableResult
        func scrollToHeadingAnchor(_ anchor: String) -> Bool {
            guard let textView else { return false }
            guard let heading = headings.first(where: { $0.anchorId == anchor })
            else {
                // Red-team F3: with the request-site announcement
                // gone, a silent bail here would make the activation
                // fully mute. Shouldn't occur (sidebar + coordinator
                // share one headings source), but if it does, say so.
                postAccessibilityAnnouncement(
                    "Could not find that heading.",
                    priority: .medium
                )
                return false
            }
            let ns = textView.string as NSString

            // Primary: backend byte offset → UTF-16 location.
            // Validate it actually lands on the heading's first
            // character (its level-marker `#` or the heading text
            // for setext) — an unsaved edit shifts offsets, and a
            // blind scroll to a stale position would be a quieter
            // version of the same lie #431 is about.
            let location = EditorTextConversions.utf16LocationForByteOffset(
                Int(heading.byteOffset),
                in: textView.string
            )
            var range = NSRange(location: NSNotFound, length: 0)
            if location < ns.length {
                let lineRange = ns.lineRange(for: NSRange(location: location, length: 0))
                let line = ns.substring(with: lineRange)
                let trimmed = line.trimmingCharacters(in: .whitespaces)
                // Red-team F2: require the heading's OWN level marker
                // ("## " for level 2), not any "#" — a stale offset
                // landing on a different heading must not announce
                // the requested one.
                let atxMarker = String(repeating: "#", count: Int(heading.level)) + " "
                // Red-team F1: setext headings put the TEXT on the
                // offset line with an ===/--- underline on the next —
                // accept when the underline is there, so markup-bearing
                // setext headings scroll too.
                var isSetext = false
                let nextLineStart = lineRange.location + lineRange.length
                if nextLineStart < ns.length {
                    let nextRange = ns.lineRange(
                        for: NSRange(location: nextLineStart, length: 0))
                    let next = ns.substring(with: nextRange)
                        .trimmingCharacters(in: .whitespacesAndNewlines)
                    isSetext = !next.isEmpty
                        && next.allSatisfy { $0 == "=" || $0 == "-" }
                }
                if line.contains(heading.text) || trimmed.hasPrefix(atxMarker) || isSetext {
                    range = NSRange(location: location, length: 0)
                }
            }
            // Fallback: rendered-text search (pre-#431 behavior) for
            // stale offsets. Still misses inline-markup headings —
            // but only in the stale-offset window before the next
            // save refreshes them.
            if range.location == NSNotFound {
                let candidate = ns.range(of: heading.text)
                if candidate.location != NSNotFound {
                    range = NSRange(location: candidate.location, length: 0)
                }
            }
            guard range.location != NSNotFound else {
                postAccessibilityAnnouncement(
                    "Could not scroll to \(heading.text).",
                    priority: .medium
                )
                return false
            }
            scrollRangeToVisibleRespectingReduceMotion(range)
            textView.setSelectedRange(range)
            postAccessibilityAnnouncement(
                "Scrolled to \(heading.text).",
                priority: .medium
            )
            return true
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
            // UTF-8 byte offset → UTF-16 location via the rope
            // `TextBuffer` (#378); the Rust side clamps past-EOF to the
            // buffer end (preserving the old "park at the end" fallback)
            // and snaps a mid-scalar offset to a char boundary — was a
            // hand-rolled `utf8.index`/`samePosition`/`utf16.distance`
            // walk.
            let location = EditorTextConversions.utf16LocationForByteOffset(
                offset,
                in: textView.string
            )
            let range = NSRange(location: location, length: 0)
            scrollRangeToVisibleRespectingReduceMotion(range)
            textView.setSelectedRange(range)
            // #421 (F-H1): focus follows the parked caret. The
            // create-from-template flow left keyboard focus on the
            // window — a caret position the user can't type at is
            // half a feature. Red-team F1: the replay-on-subscribe
            // delivery runs synchronously inside makeNSView, where
            // textView.window is still nil — a bare `window?` grab
            // silently no-ops in exactly the template-create path.
            // Defer one main-queue tick when the view isn't in a
            // window yet; by then SwiftUI's commit has inserted it.
            if let window = textView.window {
                window.makeFirstResponder(textView)
            } else {
                DispatchQueue.main.async { [weak textView] in
                    guard let textView else { return }
                    textView.window?.makeFirstResponder(textView)
                }
            }
        }

        private func scrollToLine(_ line: Int) {
            guard let textView else { return }
            // 1-based line → UTF-16 location of its first character via
            // the rope `TextBuffer` (#378); a line past EOF parks at the
            // buffer end — was an O(n) `source.indices` newline walk.
            let location = EditorTextConversions.utf16LocationForLine(
                line,
                in: textView.string
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
