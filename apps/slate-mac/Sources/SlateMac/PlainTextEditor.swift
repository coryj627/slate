// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

/// A plain-text `NSTextView` wrapper for STRUCTURED text fields (the
/// properties-source YAML editor) with the same input hygiene as the
/// main note editor.
///
/// Why not SwiftUI `TextEditor`: a bare `TextEditor` inherits the
/// system substitution defaults — smart quotes/dashes ON for most
/// users — so typing `"` into a YAML value silently lands a curly
/// `"`, changing or breaking the value's delimiters. That is the
/// exact corruption class `NoteEditorView` disables for the note
/// buffer; a structured-text field must not be the one undefended
/// surface. SwiftUI exposes no per-view substitution controls on
/// macOS, so the defence needs the AppKit view.
///
/// Deliberately minimal next to `NoteEditorView`: no highlight
/// engine, no dirty tracking, no caret park — a short-lived draft
/// buffer with a two-way `Binding<String>`.
struct PlainTextEditor: NSViewRepresentable {
    @Binding var text: String
    let accessibilityLabel: String

    /// In-app editor text zoom factor (#848) — the NoteEditorView
    /// threading pattern: the host passes `AppState.editorTextScale`
    /// and a zoom change re-runs `updateNSView` with the new value.
    var textScale: Double = 1.0

    /// System Text Size dependency — read in `updateNSView` so the
    /// font re-derives live (WCAG 1.4.4; the NoteEditorView pattern).
    @Environment(\.dynamicTypeSize) private var dynamicTypeSize

    func makeCoordinator() -> Coordinator {
        Coordinator(text: $text)
    }

    func makeNSView(context: Context) -> NSScrollView {
        let scrollView = NSScrollView()
        scrollView.hasVerticalScroller = true
        scrollView.autohidesScrollers = true
        scrollView.borderType = .bezelBorder
        scrollView.drawsBackground = true

        let textView = NSTextView(frame: .zero)
        textView.autoresizingMask = [.width]
        textView.isVerticallyResizable = true
        textView.isHorizontallyResizable = false
        textView.textContainer?.widthTracksTextView = true
        textView.textContainer?.containerSize = NSSize(
            width: 0, height: CGFloat.greatestFiniteMagnitude)

        // The NoteEditorView hygiene block: no prose substitutions in
        // structured text (each of these defaults to the user's
        // system-wide setting otherwise).
        textView.isEditable = true
        textView.isSelectable = true
        textView.isRichText = false
        textView.allowsUndo = true
        textView.isAutomaticQuoteSubstitutionEnabled = false
        textView.isAutomaticDashSubstitutionEnabled = false
        textView.isAutomaticTextReplacementEnabled = false
        textView.isAutomaticSpellingCorrectionEnabled = false
        textView.isAutomaticLinkDetectionEnabled = false
        // #855: deliberately NOT wired to the app-level spell-check
        // opt-in (`AppState.editorSpellCheckEnabled`). This surface
        // edits STRUCTURED text (YAML frontmatter) — keys and values
        // are identifiers, not prose, so live spell checking stays
        // always-off here regardless of the note-editor pref.
        textView.isContinuousSpellCheckingEnabled = false
        textView.smartInsertDeleteEnabled = false
        textView.usesFontPanel = false
        textView.usesRuler = false
        textView.usesFindBar = false

        textView.font = Tokens.Typography.monospacedBodyNSFont(scale: textScale)
        // Explicit dynamic color — the nil default reads dim-on-dark
        // (the #226/#302 NSTextView gotcha).
        textView.textColor = NSColor.textColor
        textView.textContainerInset = NSSize(width: 4, height: 6)

        textView.setAccessibilityLabel(accessibilityLabel)
        textView.setAccessibilityRole(.textArea)

        textView.delegate = context.coordinator
        context.coordinator.textView = textView
        textView.string = text

        scrollView.documentView = textView
        return scrollView
    }

    func updateNSView(_ scrollView: NSScrollView, context: Context) {
        guard let textView = scrollView.documentView as? NSTextView else { return }
        context.coordinator.text = $text
        textView.setAccessibilityLabel(accessibilityLabel)

        // Live Text Size tracking (the NoteEditorView pattern);
        // `textScale` (#848) is the second observed input.
        _ = dynamicTypeSize
        let baseFont = Tokens.Typography.monospacedBodyNSFont(scale: textScale)
        if textView.font?.pointSize != baseFont.pointSize {
            textView.font = baseFont
        }

        // External draft swap (mode re-entry, cross-note reset). Skip
        // during user typing — the delegate already synced the
        // binding, and restamping would reset the caret.
        if textView.string != text {
            let previousRange = textView.selectedRange()
            textView.string = text
            let clamped = min(previousRange.location, text.utf16.count)
            textView.setSelectedRange(NSRange(location: clamped, length: 0))
        }
    }

    @MainActor
    final class Coordinator: NSObject, NSTextViewDelegate {
        var text: Binding<String>
        weak var textView: NSTextView?

        init(text: Binding<String>) {
            self.text = text
        }

        func textDidChange(_ notification: Notification) {
            guard let textView else { return }
            text.wrappedValue = textView.string
        }
    }
}
