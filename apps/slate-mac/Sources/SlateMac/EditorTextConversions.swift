// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Editor offset/line conversions, routed through the canonical rope
/// `TextBuffer` in `slate-core` over FFI (#378, `05` §7.1).
///
/// These replace three hand-rolled O(n) `String.Index` walks the editor
/// carried (`scrollToLine`, `placeCursorAtByteOffset`,
/// `oneBasedLineForUTF16Offset`), each of which re-derived
/// UTF-8↔UTF-16↔line arithmetic slightly differently and carried its own
/// clamping/edge-case bugs. There is now **one** definition, O(log n) in
/// the rope, sharing the backend's notion of "offset" and "line" (a line
/// break is `\n` only — the backend and `NSTextView` agree on that).
///
/// Pure and `NSWorkspace`-free (like `EditorSpanMapping`), so it's
/// unit-testable in isolation. Most run at human-action cadence
/// (jump-to-line, cursor placement); `byteOffsetForUTF16Location` is the
/// exception — the #379 ranged highlighter calls it on the debounced
/// keystroke path to map the editor's dirty UTF-16 range into bytes (one
/// rope build per debounced burst, not per keystroke).
///
/// Inputs are clamped to `UInt32` (negatives → 0); the Rust side
/// additionally saturates any offset to the buffer bounds, so callers
/// can pass loosely-bounded host offsets without a guard.
enum EditorTextConversions {

    /// UTF-16 offset into `text` → 1-based line number (the Cmd+E
    /// spatial-bearing cue, audit #209).
    static func lineForUTF16Offset(_ utf16Offset: Int, in text: String) -> Int {
        Int(textUtf16ToLine(text: text, utf16Offset: UInt32(clamping: utf16Offset)))
    }

    /// 1-based line number → UTF-16 location of that line's first
    /// character (the `NSRange.location` for a "jump to line" scroll). A
    /// line past EOF parks at the buffer end; `< 1` clamps to line 1.
    static func utf16LocationForLine(_ oneBasedLine: Int, in text: String) -> Int {
        Int(textLineToUtf16(text: text, oneBasedLine: UInt32(clamping: oneBasedLine)))
    }

    /// UTF-8 byte offset into `text` → UTF-16 location (for parking the
    /// caret at a template's `{{cursor}}`). Past EOF clamps to the end.
    static func utf16LocationForByteOffset(_ byteOffset: Int, in text: String) -> Int {
        Int(textByteToUtf16(text: text, byteOffset: UInt32(clamping: byteOffset)))
    }

    /// UTF-16 location into `text` → UTF-8 byte offset (the inverse of
    /// `utf16LocationForByteOffset`). The ranged highlighter (#379 PR 2)
    /// uses it to turn the editor's UTF-16 dirty range into the byte
    /// `dirty` range `editorHighlightSpansInRange` expects. Past EOF clamps
    /// to the byte length; a location on a surrogate-pair trailing half
    /// snaps to the character boundary (Rust `TextBuffer::utf16_to_byte`).
    static func byteOffsetForUTF16Location(_ utf16Location: Int, in text: String) -> Int {
        Int(textUtf16ToByte(text: text, utf16Offset: UInt32(clamping: utf16Location)))
    }
}
