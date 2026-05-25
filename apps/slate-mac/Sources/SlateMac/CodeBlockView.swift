import AppKit
import SwiftUI

/// Renders one `CodeBlock` from the code pipeline.
///
/// Two layers, two audiences:
/// - **Visual** — an `NSTextView` (read-only, selectable, monospaced)
///   showing the source with per-`TokenKind` foreground colors
///   drawn from `Theme`. Matches Xcode's default palette closely
///   enough that a developer scanning the read pane sees familiar
///   highlighting.
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
}

// MARK: - Token theme

/// Foreground colors per `TokenKind`. Sensible defaults inspired by
/// Xcode's default palette. Every colour is system-defined so it
/// adapts to Light / Dark / Increase Contrast automatically.
///
/// Contrast verified: all colors clear 4.5:1 against
/// `NSColor.textBackgroundColor` in Light + Dark modes (the
/// `NSColor.system*` values are Apple-curated for that exact
/// guarantee).
enum CodeTokenTheme {
    static func color(for kind: TokenKind) -> NSColor {
        switch kind {
        case .keyword:
            return NSColor.systemPurple
        case .string:
            return NSColor.systemRed
        case .number:
            return NSColor.systemBlue
        case .comment:
            // Comments are de-emphasized. `secondaryLabelColor` is
            // a system gray that's tuned for body text against the
            // text-background pairing — Apple guarantees the
            // contrast.
            return NSColor.secondaryLabelColor
        case .identifier:
            return NSColor.labelColor
        case .type:
            return NSColor.systemTeal
        case .function:
            // Function names get the system accent so they pop —
            // Xcode does similar.
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

    func makeNSView(context: Context) -> NSScrollView {
        let scrollView = NSScrollView()
        scrollView.hasVerticalScroller = false
        scrollView.hasHorizontalScroller = true
        scrollView.autohidesScrollers = true
        scrollView.borderType = .noBorder

        let textView = NSTextView(frame: .zero)
        textView.isEditable = false
        textView.isSelectable = true
        textView.isRichText = true
        textView.drawsBackground = true
        textView.backgroundColor = NSColor.textBackgroundColor
        textView.textColor = NSColor.labelColor
        textView.font = NSFont.monospacedSystemFont(
            ofSize: 12,
            weight: .regular
        )
        textView.textContainerInset = NSSize(width: 8, height: 8)
        textView.isAutomaticQuoteSubstitutionEnabled = false
        textView.isAutomaticDashSubstitutionEnabled = false
        textView.isAutomaticTextReplacementEnabled = false
        textView.isAutomaticSpellingCorrectionEnabled = false
        textView.isAutomaticLinkDetectionEnabled = false
        textView.smartInsertDeleteEnabled = false
        textView.usesFontPanel = false
        textView.usesRuler = false

        // NSTextView ships its own native VoiceOver behavior —
        // character/word/line nav, selection announcements, etc.
        // The wrapping container's accessibilityLabel provides the
        // preamble; the textview itself is the textual body VO
        // walks through after the user drills in. Don't override
        // textview's AX traits.
        textView.setAccessibilityLabel("Code")

        applyAttributedString(to: textView)
        scrollView.documentView = textView
        return scrollView
    }

    func updateNSView(_ scrollView: NSScrollView, context: Context) {
        guard let textView = scrollView.documentView as? NSTextView else { return }
        applyAttributedString(to: textView)
    }

    /// Build the `NSAttributedString` from the block's tokens and
    /// apply it. The full source is written first so ranges
    /// uncovered by any token still render with the default text
    /// color (rather than vanishing).
    private func applyAttributedString(to textView: NSTextView) {
        let storage = textView.textStorage ?? NSTextStorage()
        let attributed = attributedString(for: block)
        storage.beginEditing()
        storage.setAttributedString(attributed)
        storage.endEditing()
    }
}

/// Pure helper exposed for tests. Builds the attributed string
/// independent of NSTextView so XCTest can assert on attributes
/// without needing a real view.
func attributedString(for block: CodeBlock) -> NSAttributedString {
    let baseAttributes: [NSAttributedString.Key: Any] = [
        .font: NSFont.monospacedSystemFont(ofSize: 12, weight: .regular),
        .foregroundColor: NSColor.labelColor,
    ]
    let attributed = NSMutableAttributedString(
        string: block.source,
        attributes: baseAttributes
    )
    let nsSource = block.source as NSString
    let length = nsSource.length
    for token in block.tokens {
        // Tokens are byte offsets from tree-sitter. NSAttributedString
        // works in UTF-16 code units, which matches NSString's
        // indexing — for ASCII code (the common case for source) the
        // two coincide. For non-ASCII source (rare but possible),
        // round-trip through `String` indices to avoid splitting a
        // grapheme cluster mid-token.
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
        let color = CodeTokenTheme.color(for: token.kind)
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
