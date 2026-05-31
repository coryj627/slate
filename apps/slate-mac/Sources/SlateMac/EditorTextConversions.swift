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
/// unit-testable in isolation. These run at human-action cadence
/// (jump-to-line, cursor placement); the per-keystroke editor highlight
/// path uses the stateful `DocumentBuffer`'s own O(log n) conversions off
/// the live rope instead (#404), so no stateless rope rebuild happens while
/// typing.
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

}
