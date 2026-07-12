// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

/// Renders one `CodeBlock` from the code pipeline.
///
/// Two layers, two audiences:
/// - **Visual** — an `NSTextView` (read-only, selectable, monospaced)
///   showing the source with per-`TokenKind` foreground colors
///   drawn from `CodeTokenTheme`. Matches Xcode's default palette
///   closely enough that a developer scanning the read pane sees
///   familiar highlighting.
/// - **Accessibility** — the container carries a `"Code block,
///   <language>, N lines"` preamble so VoiceOver users know what
///   they're entering before they drill in. `accessibilityElement(
///   children: .contain)` lets VO walk the source itself once
///   inside, without flooding the rotor with per-token children
///   (which would be unusable on a 200-line block).
///
/// Standalone — no AppState. The same view lights up the read pane
/// today and (future) "open code block in its own pane" surfaces.
struct CodeBlockView: View {
    let block: CodeBlock

    /// In-app editor text zoom factor (#848). Threaded by hosts on
    /// the EDITING side (the code-blocks panel passes
    /// `AppState.editorTextScale`); the reading surface deliberately
    /// leaves the default 1.0 — reading mode tracks the system Text
    /// Size wholesale, zoom scales the editing surfaces only (the
    /// boundary documented on the Zoom In menu item). Keeps the view
    /// standalone: a plain value, no AppState dependency.
    var textScale: Double = 1.0

    var body: some View {
        VStack(alignment: .trailing, spacing: 0) {
            // #854: visible copy affordance. Every mainstream rendered-
            // code surface shows a top-trailing Copy button; before
            // this, copy was context-menu-only (invisible to pointer
            // users). Always-visible (not hover-revealed): hover
            // reveal hides the affordance from touch/switch users and
            // from anyone who doesn't think to probe, for near-zero
            // chrome savings on a block that already owns its row.
            // IN layout, never an overlay (Codex review): an overlay
            // sat on top of the first code line and swallowed pointer
            // selection under its hit region. The context-menu path
            // below stays for keyboard / Voice Control parity.
            copyButton
            CodeBlockContent(block: block, textScale: textScale)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(.vertical, 4)
            // The container owns the AT preamble. VO reads
            // "Code block, rust, 5 lines" before the user enters.
            //
            // `children: .contain` (not `.combine` or `.ignore`)
            // is the right call: the wrapped NSTextView holds the
            // source; we want VO to step into it after the preamble
            // (rotor or arrow keys), not be locked out (`.ignore`)
            // and not have the entire content concatenated into the
            // label (`.combine`, which on long blocks would be
            // catastrophic).
            .accessibilityElement(children: .contain)
            .accessibilityLabel(preambleLabel)
            // Audit #252 M3: a code block can clip long lines (the
            // NSScrollView allows horizontal scroll). VO users
            // wouldn't otherwise know — autohidesScrollers means
            // the scrollbar doesn't announce. The hint covers it.
            // #854: hint updated — the primary copy path is now the
            // visible button; the context menu remains as the
            // secondary route.
            .accessibilityHint(
                "Long lines scroll horizontally. Use the Copy button or right-click to copy the source."
            )
            // Audit #252 L4: keyboard / Voice Control users can't
            // reach NSTextView's mouse-selection affordance. A
            // context menu with "Copy code" gives mouse,
            // keyboard, and Voice Control users a uniform path.
            // (#854 routes it through `copyCode()` so both paths
            // announce identically.)
            .contextMenu {
                Button("Copy code") { copyCode() }
            }
            // NOTE: `.textSelection(.enabled)` is NOT applied at
            // container level. SwiftUI's container-scoped text
            // selection breaks VO continuous-read at the leaf level
            // (see memory: feedback_swiftui_textselection_ax). The
            // wrapped NSTextView's `isSelectable = true` handles
            // selection at the proper scope.
    }

    /// VoiceOver preamble. Language defaults to "plain text" when
    /// the block is fenced without a tag or indented; line count
    /// uses 1-based counting (matches how a code editor numbers
    /// rows).
    var preambleLabel: String {
        let language = displayLanguage
        let lines = lineCount
        let suffix = lines == 1 ? "line" : "lines"
        return "Code block, \(language), \(lines) \(suffix)."
    }

    var displayLanguage: String {
        if let lang = block.language?.trimmingCharacters(in: .whitespacesAndNewlines),
            !lang.isEmpty
        {
            return lang
        }
        return "plain text"
    }

    /// Source line count. A trailing newline is treated as
    /// terminating the last line, not adding an empty one — matches
    /// what an editor would show ("N lines").
    var lineCount: Int {
        if block.source.isEmpty {
            return 0
        }
        var count = 0
        var sawAny = false
        for ch in block.source {
            sawAny = true
            if ch == "\n" {
                count += 1
            }
        }
        if !sawAny {
            return 0
        }
        // Trailing newline closes the previous line; no trailing
        // newline means the last line still counts.
        if !block.source.hasSuffix("\n") {
            count += 1
        }
        return count
    }

    /// #854: the visible copy affordance. Quiet chrome (caption font,
    /// secondary tint — the code content stays the star) but a full
    /// 28pt hit target. Shares `copyCodeToPasteboard()` with the
    /// context-menu path and announces the result so the action is
    /// never silent for AT users.
    private var copyButton: some View {
        Button {
            copyCode()
        } label: {
            Text("Copy")
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .frame(minWidth: 28, minHeight: 28)
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .padding(.trailing, 4)
        .accessibilityLabel("Copy code")
        .accessibilityHint("Copies the code block source as plain text.")
        .help("Copy code")
    }

    /// Copy + announce (#854). Internal (not private) so the test
    /// suite can drive the button's action directly.
    func copyCode() {
        copyCodeToPasteboard()
        postAccessibilityAnnouncement("Code copied.", priority: .medium)
    }

    /// Copy the raw (un-attributed) source to the pasteboard. Plain
    /// text on purpose — pasting code into another editor should
    /// not carry our highlight colours.
    private func copyCodeToPasteboard() {
        let pasteboard = NSPasteboard.general
        pasteboard.clearContents()
        pasteboard.setString(block.source, forType: .string)
    }
}

// MARK: - Token theme

/// Foreground colors per `TokenKind`. Sensible defaults inspired by
/// Xcode's default palette.
///
/// Contrast (audit #252 H1 + H2): the default palette uses
/// `NSColor.system*` semantic colors, which are tuned by Apple
/// against the standard label/background pairing — but several
/// (`controlAccentColor` on Graphite especially, `systemRed` /
/// `systemPurple` in Dark Mode) land in the 3.0–4.5:1 range against
/// `NSColor.textBackgroundColor`, below WCAG 1.4.3's 4.5:1 bar.
/// Under `accessibilityDisplayShouldIncreaseContrast` we collapse
/// the whole palette to `NSColor.labelColor` (Apple-guaranteed
/// contrast against the matched background) so highlighting stays
/// discriminable for low-vision users — same pattern audit #230
/// applied to the editor's embed underline.
enum CodeTokenTheme {
    static func color(for kind: TokenKind, increaseContrast: Bool) -> NSColor {
        if increaseContrast {
            // Single high-contrast color across all kinds. Tokens
            // are still semantically tagged (via the attribute
            // layer), just not colour-coded — which is correct
            // a11y behaviour: shape / position carry the structure,
            // not colour alone (WCAG 1.4.1).
            return NSColor.labelColor
        }
        switch kind {
        case .keyword:
            return NSColor.systemPurple
        case .string:
            return NSColor.systemRed
        case .number:
            return NSColor.systemBlue
        case .comment:
            // Comments are de-emphasized. `secondaryLabelColor` is
            // a system gray that's Apple-tuned for body text
            // against text-background pairing.
            return NSColor.secondaryLabelColor
        case .identifier:
            return NSColor.labelColor
        case .type:
            return NSColor.systemTeal
        case .function:
            // Function names get the system accent so they pop.
            return NSColor.controlAccentColor
        case .operator:
            return NSColor.labelColor
        case .punctuation:
            return NSColor.secondaryLabelColor
        case .other:
            return NSColor.labelColor
        }
    }
}

// MARK: - Content (NSTextView wrapped)

// Internal (not private) so the #416 contrast-leg regression test
// can drive the Coordinator directly.
struct CodeBlockContent: NSViewRepresentable {
    let block: CodeBlock

    /// In-app editor text zoom factor (#848) — see `CodeBlockView`.
    var textScale: Double = 1.0

    /// System Text Size dependency — read in `updateNSView` so the
    /// code font re-derives live when the setting changes (WCAG
    /// 1.4.4; the NoteEditorView pattern).
    @Environment(\.dynamicTypeSize) private var dynamicTypeSize

    func makeCoordinator() -> Coordinator { Coordinator() }

    final class Coordinator: NSObject {
        weak var textView: NSTextView?
        var block: CodeBlock?
        /// Mirrors the view's `textScale` so the out-of-band contrast
        /// re-render below rebuilds at the current zoom, not 1.0.
        var textScale: Double = 1.0

        @objc func systemContrastChanged() {
            guard let textView, let block else { return }
            applyAttributedString(to: textView, block: block, scale: textScale)
        }
    }

    func makeNSView(context: Context) -> NSScrollView {
        let scrollView = NSScrollView()
        scrollView.hasVerticalScroller = false
        scrollView.hasHorizontalScroller = true
        scrollView.autohidesScrollers = true
        scrollView.borderType = .noBorder

        let textView = NSTextView(frame: .zero)
        textView.isEditable = false
        textView.isSelectable = true
        // Audit #252 L1: a read-only view doesn't need rich-text
        // input. Attributed-string rendering still works with
        // `isRichText = false`; the flag governs USER input
        // (paste-as-rich-text, Format menu), not what the storage
        // can hold. Set to false so a Cmd+C copy lands as plain
        // UTF-8 instead of RTF.
        textView.isRichText = false
        textView.drawsBackground = true
        textView.backgroundColor = NSColor.textBackgroundColor
        textView.textColor = NSColor.labelColor
        // Audit #252 M2's intent, actually implemented: the previous
        // `NSFont.systemFontSize` claim ("the user's chosen system
        // font size") was false — that constant is a fixed 13pt and
        // never tracks System Settings ▸ Accessibility ▸ Display ▸
        // Text Size. The body-TEXT-STYLE size does; `updateNSView`
        // re-applies on Dynamic Type changes (WCAG 1.4.4).
        textView.font = Tokens.Typography.monospacedBodyNSFont(scale: textScale)
        textView.textContainerInset = NSSize(width: 8, height: 8)
        textView.usesFontPanel = false
        textView.usesRuler = false
        // The automatic-substitution flags are irrelevant on
        // `isEditable = false` (there's no typing surface to
        // substitute on), but we keep `smartInsertDeleteEnabled =
        // false` as defensive cover in case the view ever flips
        // editable for a "scratchpad" feature.
        textView.smartInsertDeleteEnabled = false

        // Audit #252 M1: do NOT add `setAccessibilityLabel("Code")`
        // here. With the container's accessibilityLabel providing
        // the preamble + children: .contain, the inner "Code"
        // label would announce noise on drill-in ("Code block,
        // rust, 5 lines… Code… fn main…"). Leaving the textview
        // label nil lets VO read straight into the source content
        // after the preamble — what the container's docstring
        // already promised.

        context.coordinator.textView = textView
        context.coordinator.block = block
        context.coordinator.textScale = textScale

        // Re-render token colors when the user toggles Increase
        // Contrast at runtime (audit #252 H1). #416: registered on
        // NSWorkspace.shared.notificationCenter — the center this
        // notification is actually posted to; the previous
        // default-center registration never fired.
        NSWorkspace.shared.notificationCenter.addObserver(
            context.coordinator,
            selector: #selector(Coordinator.systemContrastChanged),
            name: NSWorkspace.accessibilityDisplayOptionsDidChangeNotification,
            object: nil
        )

        applyAttributedString(to: textView, block: block, scale: textScale)
        scrollView.documentView = textView
        return scrollView
    }

    func updateNSView(_ scrollView: NSScrollView, context: Context) {
        guard let textView = scrollView.documentView as? NSTextView else { return }
        context.coordinator.block = block
        context.coordinator.textScale = textScale
        // Dynamic Type dependency: reading the environment value here
        // re-runs this method when the system Text Size changes, and
        // `applyAttributedString` re-derives the base font (WCAG
        // 1.4.4 — the same live-tracking hook NoteEditorView uses).
        // `textScale` (#848) is the second observed input — a zoom
        // change re-renders the host and re-runs this method.
        _ = dynamicTypeSize
        applyAttributedString(to: textView, block: block, scale: textScale)
    }

    static func dismantleNSView(_ scrollView: NSScrollView, coordinator: Coordinator) {
        NotificationCenter.default.removeObserver(coordinator)
        // #416: the contrast observer lives on the workspace center.
        NSWorkspace.shared.notificationCenter.removeObserver(coordinator)
    }
}

/// Build the `NSAttributedString` from the block's tokens and apply
/// it. The full source is written first so ranges uncovered by any
/// token still render with the default text color.
private func applyAttributedString(
    to textView: NSTextView, block: CodeBlock, scale: Double = 1.0
) {
    guard let storage = textView.textStorage else { return }
    let attributed = attributedString(
        for: block,
        increaseContrast: NSWorkspace.shared.accessibilityDisplayShouldIncreaseContrast,
        scale: scale
    )
    storage.beginEditing()
    storage.setAttributedString(attributed)
    storage.endEditing()
}

/// Pure helper exposed for tests. Builds the attributed string
/// independent of NSTextView so XCTest can assert on attributes
/// without needing a real view. The `increaseContrast` parameter
/// lets tests cover both palettes (audit #252 H1/H2) without
/// depending on the system's actual setting. `scale` is the in-app
/// editor text zoom factor (#848), 1.0 = base size.
func attributedString(
    for block: CodeBlock, increaseContrast: Bool = false, scale: Double = 1.0
) -> NSAttributedString {
    let baseAttributes: [NSAttributedString.Key: Any] = [
        // Body-text-style size (tracks the macOS Text Size setting),
        // matching the editor pane — see the makeNSView note above.
        .font: Tokens.Typography.monospacedBodyNSFont(scale: scale),
        .foregroundColor: NSColor.labelColor,
    ]
    let attributed = NSMutableAttributedString(
        string: block.source,
        attributes: baseAttributes
    )
    let nsSource = block.source as NSString
    let length = nsSource.length
    for token in block.tokens {
        guard
            let range = utf16Range(
                forUtf8Start: Int(token.startByte),
                end: Int(token.endByte),
                in: block.source,
                nsLength: length
            )
        else {
            continue
        }
        let color = CodeTokenTheme.color(for: token.kind, increaseContrast: increaseContrast)
        attributed.addAttribute(.foregroundColor, value: color, range: range)
    }
    return attributed
}

/// Convert a UTF-8 byte range into an NSRange over the NSString
/// (UTF-16) view of `source`. Returns `nil` if the bytes don't
/// land on a valid character boundary in source.
private func utf16Range(
    forUtf8Start startByte: Int,
    end endByte: Int,
    in source: String,
    nsLength: Int
) -> NSRange? {
    guard startByte >= 0, endByte >= startByte, endByte <= source.utf8.count else {
        return nil
    }
    let utf8 = source.utf8
    guard let startIdx = utf8.index(utf8.startIndex, offsetBy: startByte, limitedBy: utf8.endIndex),
        let endIdx = utf8.index(utf8.startIndex, offsetBy: endByte, limitedBy: utf8.endIndex)
    else {
        return nil
    }
    guard let strStart = startIdx.samePosition(in: source),
        let strEnd = endIdx.samePosition(in: source)
    else {
        return nil
    }
    let utf16Start = strStart.utf16Offset(in: source)
    let utf16End = strEnd.utf16Offset(in: source)
    let len = utf16End - utf16Start
    guard utf16Start >= 0, utf16Start + len <= nsLength else {
        return nil
    }
    return NSRange(location: utf16Start, length: len)
}
