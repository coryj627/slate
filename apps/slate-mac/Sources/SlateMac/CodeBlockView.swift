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

    var body: some View {
        CodeBlockContent(block: block)
            .frame(maxWidth: .infinity, alignment: .leading)
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
            .accessibilityHint("Long lines scroll horizontally. Right-click to copy the source.")
            // Audit #252 L4: keyboard / Voice Control users can't
            // reach NSTextView's mouse-selection affordance. A
            // context menu with "Copy code" gives mouse,
            // keyboard, and Voice Control users a uniform path.
            .contextMenu {
                Button("Copy code") { copyCodeToPasteboard() }
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

private struct CodeBlockContent: NSViewRepresentable {
    let block: CodeBlock

    func makeCoordinator() -> Coordinator { Coordinator() }

    final class Coordinator: NSObject {
        weak var textView: NSTextView?
        var block: CodeBlock?

        @objc func systemContrastChanged() {
            guard let textView, let block else { return }
            applyAttributedString(to: textView, block: block)
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
        // Audit #252 M2: Dynamic Type / system text size. Pinning
        // to 12pt makes the code smaller than the editor pane and
        // ignores System Settings → Display → Text Size. Use the
        // user's chosen system font size (same as the editor pane,
        // see NoteEditorView).
        textView.font = NSFont.monospacedSystemFont(
            ofSize: NSFont.systemFontSize,
            weight: .regular
        )
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

        // Re-render token colors when the user toggles Increase
        // Contrast at runtime (audit #252 H1).
        NotificationCenter.default.addObserver(
            context.coordinator,
            selector: #selector(Coordinator.systemContrastChanged),
            name: NSWorkspace.accessibilityDisplayOptionsDidChangeNotification,
            object: nil
        )

        applyAttributedString(to: textView, block: block)
        scrollView.documentView = textView
        return scrollView
    }

    func updateNSView(_ scrollView: NSScrollView, context: Context) {
        guard let textView = scrollView.documentView as? NSTextView else { return }
        context.coordinator.block = block
        applyAttributedString(to: textView, block: block)
    }

    static func dismantleNSView(_ scrollView: NSScrollView, coordinator: Coordinator) {
        NotificationCenter.default.removeObserver(coordinator)
    }
}

/// Build the `NSAttributedString` from the block's tokens and apply
/// it. The full source is written first so ranges uncovered by any
/// token still render with the default text color.
private func applyAttributedString(to textView: NSTextView, block: CodeBlock) {
    guard let storage = textView.textStorage else { return }
    let attributed = attributedString(
        for: block,
        increaseContrast: NSWorkspace.shared.accessibilityDisplayShouldIncreaseContrast
    )
    storage.beginEditing()
    storage.setAttributedString(attributed)
    storage.endEditing()
}

/// Pure helper exposed for tests. Builds the attributed string
/// independent of NSTextView so XCTest can assert on attributes
/// without needing a real view. The `increaseContrast` parameter
/// lets tests cover both palettes (audit #252 H1/H2) without
/// depending on the system's actual setting.
func attributedString(for block: CodeBlock, increaseContrast: Bool = false) -> NSAttributedString {
    let baseAttributes: [NSAttributedString.Key: Any] = [
        .font: NSFont.monospacedSystemFont(ofSize: NSFont.systemFontSize, weight: .regular),
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
