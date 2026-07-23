// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Rope-backed editor text buffer (`05` §7.1).
//!
//! The locked editor model is a [rope](https://en.wikipedia.org/wiki/Rope_(data_structure))
//! plus a persistent operation log. This module lands the rope half: a
//! thin wrapper over [`ropey::Rope`] that exposes the offset / line
//! conversions the editor needs in **O(log n)**, replacing the
//! hand-rolled O(n) `String` walks the Mac app carried (`scrollToLine`,
//! `placeCursorAtByteOffset`, `oneBasedLineForUTF16Offset`).
//!
//! ## Coordinates
//!
//! Three coordinate spaces meet at the editor boundary:
//! - **UTF-8 byte offsets** — the canonical space, shared with
//!   [`crate::editor_spans`], headings, tasks, and the op log.
//! - **UTF-16 code units** — what AppKit's `NSTextView` /
//!   `NSLayoutManager` are indexed in.
//! - **1-based lines** — what the outline / task / "jump to line"
//!   affordances speak.
//!
//! `ropey` 2.0 indexes natively in bytes and carries a UTF-16 code-unit
//! metric (the reason it's picked over `crop`); byte ↔ UTF-16 conversion
//! is a direct O(log n) metric (`byte_to_utf16_idx` / `utf16_to_byte_idx`).
//! Lines are **0-based** inside `ropey` and counted with `LineType::LF`
//! (LF-only, per `05` §7.1); this type presents **1-based** lines at its
//! surface to match the host contract.
//!
//! ## Defensive clamping
//!
//! Every conversion clamps its input to the valid range and snaps to a
//! char boundary, so a caller passing an out-of-range or mid-scalar
//! offset gets a saturated, in-bounds result rather than a panic — the
//! same robustness the prior Swift walks hand-coded, now in one place.

use std::ops::Range;

use ropey::{LineType, Rope};

/// A rope-backed document buffer with O(log n) offset / line
/// conversions. Cheap to clone (ropey shares structure copy-on-write).
#[derive(Debug, Clone, Default)]
pub struct TextBuffer {
    rope: Rope,
}

impl TextBuffer {
    /// Build a buffer from a string slice.
    ///
    /// Named `from_str` to mirror [`ropey::Rope::from_str`]; this is the
    /// inherent constructor, not the `std::str::FromStr` trait (which
    /// would force a fallible `Result` this infallible build doesn't
    /// need).
    #[allow(clippy::should_implement_trait)]
    pub fn from_str(text: &str) -> Self {
        Self {
            rope: Rope::from_str(text),
        }
    }

    /// An empty buffer.
    pub fn new() -> Self {
        Self::default()
    }

    // --- Totals ------------------------------------------------------

    /// Length in UTF-8 bytes.
    pub fn len_bytes(&self) -> usize {
        self.rope.len()
    }

    /// Length in UTF-16 code units (what `NSTextStorage.length` reports).
    pub fn len_utf16(&self) -> usize {
        self.rope.len_utf16()
    }

    /// Number of lines, counting a trailing newline as opening one more
    /// (empty) line — `ropey`'s convention, so an empty buffer is 1 line
    /// and `"a\n"` is 2. LF-only (`LineType::LF`).
    pub fn len_lines(&self) -> usize {
        self.rope.len_lines(LineType::LF)
    }

    /// Canonical BLAKE3 content hash of the rope's UTF-8 bytes.
    ///
    /// This is byte-for-byte identical to [`crate::content_hash`] without
    /// materialising the rope as one contiguous `String`. The editor drift
    /// guard uses it on an idle cadence and before saves, so keeping the hash
    /// allocation-light avoids copying multi-megabyte documents merely to
    /// verify integrity.
    pub fn content_hash(&self) -> String {
        let mut hasher = blake3::Hasher::new();
        for chunk in self.rope.chunks() {
            hasher.update(chunk.as_bytes());
        }
        hasher.finalize().to_hex().to_string()
    }

    // --- byte <-> char (the bridge the others compose through) -------

    /// UTF-8 byte offset → char index. A mid-scalar byte snaps to the
    /// char it falls inside; past-the-end clamps to the char count.
    pub fn byte_to_char(&self, byte: usize) -> usize {
        self.rope.byte_to_char_idx(byte.min(self.rope.len()))
    }

    /// Char index → UTF-8 byte offset. Clamps past-the-end.
    pub fn char_to_byte(&self, char_idx: usize) -> usize {
        self.rope
            .char_to_byte_idx(char_idx.min(self.rope.len_chars()))
    }

    // --- byte <-> UTF-16 --------------------------------------------

    /// UTF-8 byte offset → UTF-16 code-unit offset (for an `NSRange`).
    /// Direct byte↔UTF-16 metric in ropey 2.0; a mid-scalar byte snaps
    /// to the char it falls inside.
    pub fn byte_to_utf16(&self, byte: usize) -> usize {
        self.rope.byte_to_utf16_idx(byte.min(self.rope.len()))
    }

    /// UTF-16 code-unit offset → UTF-8 byte offset. A code unit landing
    /// inside a surrogate pair snaps to the char it belongs to.
    pub fn utf16_to_byte(&self, utf16: usize) -> usize {
        self.rope
            .utf16_to_byte_idx(utf16.min(self.rope.len_utf16()))
    }

    // --- byte <-> 1-based line --------------------------------------

    /// UTF-8 byte offset → **1-based** line number.
    pub fn byte_to_line(&self, byte: usize) -> usize {
        self.rope
            .byte_to_line_idx(byte.min(self.rope.len()), LineType::LF)
            + 1
    }

    /// **1-based** line number → UTF-8 byte offset of that line's first
    /// character. A line past the end clamps to the buffer length (so a
    /// "jump to line" past EOF parks the caret at the end, matching the
    /// prior Swift behaviour); a line `< 1` clamps to line 1.
    pub fn line_to_byte(&self, one_based_line: usize) -> usize {
        let zero_based = one_based_line
            .saturating_sub(1)
            .min(self.rope.len_lines(LineType::LF));
        self.rope.line_to_byte_idx(zero_based, LineType::LF)
    }

    /// UTF-16 code-unit offset → **1-based** line number. Composition of
    /// [`Self::utf16_to_byte`] and [`Self::byte_to_line`]; the one the
    /// Cmd+E line cue needs.
    pub fn utf16_to_line(&self, utf16: usize) -> usize {
        self.byte_to_line(self.utf16_to_byte(utf16))
    }

    // --- Edits (foundation for the stateful buffer; byte-indexed) ----

    /// Insert `text` at a UTF-8 byte offset (snapped to a char boundary).
    /// ropey 2.0 `insert` is byte-indexed and panics off a char boundary,
    /// so the offset is snapped first.
    pub fn insert(&mut self, byte: usize, text: &str) {
        let byte = self.byte_to_byte_boundary(byte);
        self.rope.insert(byte, text);
    }

    /// Delete the half-open UTF-8 byte range (each end snapped to a char
    /// boundary). A degenerate or inverted range is a no-op.
    pub fn delete(&mut self, byte_range: Range<usize>) {
        let start = self.byte_to_byte_boundary(byte_range.start);
        let end = self.byte_to_byte_boundary(byte_range.end);
        if start < end {
            self.rope.remove(start..end);
        }
    }

    /// Replace the half-open UTF-8 byte range with `text`. The range is
    /// snapped to char boundaries **once**, so the removal and the
    /// following insertion anchor at the same position even when `start`
    /// falls mid-scalar (re-snapping the raw start byte against the
    /// post-removal rope could otherwise resolve elsewhere).
    pub fn replace(&mut self, byte_range: Range<usize>, text: &str) {
        let start = self.byte_to_byte_boundary(byte_range.start);
        let end = self.byte_to_byte_boundary(byte_range.end);
        if start < end {
            self.rope.remove(start..end);
        }
        self.rope.insert(start, text);
    }

    // --- Rope-native line navigation (#407) --------------------------
    //
    // These mirror the `\n`-counting line helpers in `crate::editor_spans`
    // (`line_start` / `line_end` / `line_is_blank` / `extend_*_to_blank`)
    // but walk the rope's line metric in O(log n) per step instead of
    // materialising the document and doing `str::rfind` / `find`. The
    // semantics are pinned byte-for-byte against the `&str` versions by
    // `rope_line_nav_matches_str_helpers` so the window the stateful buffer
    // walks is identical to the one the stateless oracle computes — #407
    // makes the keystroke highlight materialise only that window, never the
    // whole document.

    /// Byte offset of the start of the line containing `byte` (just past the
    /// previous `\n`, or 0). `byte` is clamped to the buffer length.
    pub fn line_start_byte(&self, byte: usize) -> usize {
        let byte = byte.min(self.rope.len());
        self.rope
            .line_to_byte_idx(self.rope.byte_to_line_idx(byte, LineType::LF), LineType::LF)
    }

    /// Byte offset just past the end of the line containing `byte` — the byte
    /// after the next `\n` at or after `byte`, or the buffer length. `byte`
    /// is clamped to the buffer length.
    pub fn line_end_byte(&self, byte: usize) -> usize {
        let byte = byte.min(self.rope.len());
        self.rope.line_to_byte_idx(
            self.rope.byte_to_line_idx(byte, LineType::LF) + 1,
            LineType::LF,
        )
    }

    /// True when the line starting at `line_start_byte` (a line start) is
    /// empty or made only of ASCII whitespace — a CommonMark block separator
    /// on every EOL flavour. Matches `editor_spans::line_is_blank`
    /// byte-for-byte (the parity test pins it): the byte set is exactly
    /// pulldown's blank-line alphabet (space / tab / `\x0B` / `\x0C` + EOL
    /// bytes), deliberately stricter than `trim().is_empty()` — a
    /// Unicode-whitespace-only line (NBSP, U+2028, …) is paragraph content
    /// to pulldown's block parse (#927).
    pub fn line_is_blank(&self, line_start_byte: usize) -> bool {
        let le = self.line_end_byte(line_start_byte);
        self.rope
            .slice(line_start_byte..le)
            .bytes()
            .all(|b| matches!(b, b' ' | b'\t' | 0x0B | 0x0C | b'\r' | b'\n'))
    }

    /// Walk `start` (a line start) up to a block boundary — the first line of
    /// the contiguous non-blank run, i.e. just after the nearest blank line
    /// above (or BOF). Mirrors `editor_spans::extend_up_to_blank`.
    pub fn extend_up_to_blank(&self, start: usize) -> usize {
        let mut ls = start;
        while ls > 0 {
            let prev = self.line_start_byte(ls - 1);
            if self.line_is_blank(prev) {
                break;
            }
            ls = prev;
        }
        ls
    }

    /// Walk `end` (a line end) down to a block boundary — the start of the
    /// nearest blank line below (or EOF). Mirrors
    /// `editor_spans::extend_down_to_blank`.
    pub fn extend_down_to_blank(&self, end: usize) -> usize {
        let mut le = end;
        while le < self.rope.len() {
            if self.line_is_blank(le) {
                break;
            }
            le = self.line_end_byte(le);
        }
        le
    }

    /// Materialise the half-open UTF-8 byte `range` as an owned `String`.
    /// The bounds are clamped to the buffer and snapped to char boundaries
    /// (so a mid-scalar bound widens to include the whole char). #407 uses
    /// this to pull out **only the highlight window**, never the whole
    /// document, on the per-keystroke path.
    pub fn byte_slice_to_string(&self, range: Range<usize>) -> String {
        let start = self.byte_to_byte_boundary(range.start);
        let end = self.byte_to_byte_boundary(range.end).max(start);
        self.rope.slice(start..end).to_string()
    }

    /// Snap a byte offset down to the nearest char boundary (the start of the
    /// char it falls in), clamping past-the-end to the buffer length first.
    /// Composes the rope's byte↔char metrics in O(log n). Clamping here keeps
    /// the byte-indexed `insert`/`remove`/`slice` callers panic-safe (ropey
    /// 2.0 asserts in-bounds, char-boundary indices).
    fn byte_to_byte_boundary(&self, byte: usize) -> usize {
        let byte = byte.min(self.rope.len());
        self.rope.char_to_byte_idx(self.rope.byte_to_char_idx(byte))
    }

    /// The raw byte at `byte_idx` (O(log n)). Used by the rope-native
    /// reconvergence scan (#407) to test a line's first byte for indentation
    /// without materialising the line. Panics if `byte_idx >= len_bytes()`,
    /// like `ropey::Rope::byte` — callers only read in-bounds line starts.
    pub fn byte(&self, byte_idx: usize) -> u8 {
        self.rope.byte(byte_idx)
    }
}

impl std::fmt::Display for TextBuffer {
    /// Materialise the buffer's text. Used by edit-op tests and any
    /// caller that needs the whole document as a `String`.
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        for chunk in self.rope.chunks() {
            f.write_str(chunk)?;
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    // --- byte <-> utf16 round-trips ----------------------------------

    #[test]
    fn ascii_offsets_coincide() {
        let buf = TextBuffer::from_str("hello world");
        for off in [0usize, 5, 6, 11] {
            assert_eq!(buf.byte_to_utf16(off), off);
            assert_eq!(buf.utf16_to_byte(off), off);
        }
        assert_eq!(buf.len_bytes(), 11);
        assert_eq!(buf.len_utf16(), 11);
    }

    #[test]
    fn two_byte_scalar_shifts_utf16() {
        // "café": é is 2 UTF-8 bytes, 1 UTF-16 cu. 5 bytes / 4 utf16.
        let buf = TextBuffer::from_str("café");
        assert_eq!(buf.len_bytes(), 5);
        assert_eq!(buf.len_utf16(), 4);
        assert_eq!(buf.byte_to_utf16(3), 3); // before é
        assert_eq!(buf.byte_to_utf16(5), 4); // after é
        assert_eq!(buf.utf16_to_byte(4), 5);
    }

    #[test]
    fn three_byte_scalar_shifts_utf16() {
        // "a中b": 中 is 3 UTF-8 bytes, 1 UTF-16 cu.
        let buf = TextBuffer::from_str("a中b");
        assert_eq!(buf.len_bytes(), 5);
        assert_eq!(buf.len_utf16(), 3);
        assert_eq!(buf.byte_to_utf16(1), 1); // after a, before 中
        assert_eq!(buf.byte_to_utf16(4), 2); // after 中
        assert_eq!(buf.utf16_to_byte(2), 4);
    }

    #[test]
    fn astral_scalar_is_two_utf16_units() {
        // "x😀y": 😀 (U+1F600) is 4 UTF-8 bytes, a UTF-16 surrogate pair.
        let buf = TextBuffer::from_str("x😀y");
        assert_eq!(buf.len_bytes(), 6);
        assert_eq!(buf.len_utf16(), 4);
        assert_eq!(buf.byte_to_utf16(1), 1); // after x, before 😀
        assert_eq!(buf.byte_to_utf16(5), 3); // after 😀, before y
        assert_eq!(buf.utf16_to_byte(3), 5);
        // A code unit inside the surrogate pair snaps to the char it
        // belongs to (the 😀, byte 1) rather than panicking.
        assert_eq!(buf.utf16_to_byte(2), 1);
    }

    // --- 1-based line math -------------------------------------------

    #[test]
    fn lines_are_one_based() {
        let buf = TextBuffer::from_str("a\nb\nc");
        assert_eq!(buf.len_lines(), 3);
        assert_eq!(buf.byte_to_line(0), 1); // a
        assert_eq!(buf.byte_to_line(2), 2); // b
        assert_eq!(buf.byte_to_line(4), 3); // c
        assert_eq!(buf.line_to_byte(1), 0);
        assert_eq!(buf.line_to_byte(2), 2);
        assert_eq!(buf.line_to_byte(3), 4);
    }

    #[test]
    fn line_past_eof_clamps_to_end_and_line_under_one_clamps_to_start() {
        let buf = TextBuffer::from_str("a\nb\nc"); // 5 bytes, 3 lines
        assert_eq!(
            buf.line_to_byte(99),
            buf.len_bytes(),
            "overshoot parks at EOF"
        );
        assert_eq!(buf.line_to_byte(0), 0, "line 0 clamps to line 1");
    }

    #[test]
    fn trailing_newline_opens_an_empty_final_line() {
        // ropey (correctly) treats the position after a trailing newline
        // as line 3 of "a\nb\n" — the prior Swift `scrollToLine` had a
        // latent bug here (returned offset 0); the rope is the new truth.
        let buf = TextBuffer::from_str("a\nb\n");
        assert_eq!(buf.len_lines(), 3);
        assert_eq!(buf.line_to_byte(3), 4);
        assert_eq!(buf.byte_to_line(4), 3);
    }

    #[test]
    fn only_lf_breaks_lines_not_cr_or_other_unicode_breaks() {
        // Pins the `default-features = false` (LF-only) choice. The
        // Swift editor and the `\n`-based backend count only `\n`; a CRLF
        // file's `\r` and exotic Unicode breaks (VT, LS, …) are line
        // *content*, not breaks. If a future ropey upgrade re-enabled
        // `unicode_lines`, this test fails loudly.
        assert_eq!(
            TextBuffer::from_str("a\r\nb").len_lines(),
            2,
            "CRLF: one break"
        );
        assert_eq!(
            TextBuffer::from_str("a\rb").len_lines(),
            1,
            "bare CR is content"
        );
        assert_eq!(
            TextBuffer::from_str("a\u{0b}b").len_lines(),
            1,
            "vertical tab is content"
        );
        assert_eq!(
            TextBuffer::from_str("a\u{2028}b").len_lines(),
            1,
            "U+2028 is content"
        );
    }

    #[test]
    fn utf16_to_line_composes_across_multibyte_prefix() {
        // "a\n中\nx": x is on line 3; its UTF-16 offset is 4.
        let buf = TextBuffer::from_str("a\n中\nx");
        assert_eq!(buf.utf16_to_line(4), 3);
        assert_eq!(buf.utf16_to_line(0), 1); // a
    }

    // --- clamping / no-panic -----------------------------------------

    #[test]
    fn out_of_range_inputs_saturate_without_panicking() {
        let buf = TextBuffer::from_str("café"); // 5 bytes, 4 utf16
        assert_eq!(buf.byte_to_utf16(999), buf.len_utf16());
        assert_eq!(buf.utf16_to_byte(999), buf.len_bytes());
        assert_eq!(buf.byte_to_line(999), buf.byte_to_line(buf.len_bytes()));
    }

    #[test]
    fn mid_scalar_byte_snaps_to_its_char() {
        // Byte 1 falls inside 中 (bytes 1..4 of "a中b"); it snaps to the
        // char's start, so byte_to_utf16 yields the same as byte 1's char.
        let buf = TextBuffer::from_str("a中b");
        // byte 2 and 3 are mid-中 → snap to char 1 (中) → utf16 1.
        assert_eq!(buf.byte_to_utf16(2), 1);
        assert_eq!(buf.byte_to_utf16(3), 1);
    }

    // --- edits + conversions after an edit ---------------------------

    #[test]
    fn insert_delete_replace_mutate_text() {
        let mut buf = TextBuffer::from_str("hello world");
        buf.insert(5, ",");
        assert_eq!(buf.to_string(), "hello, world");
        buf.delete(0..5);
        assert_eq!(buf.to_string(), ", world");
        buf.replace(0..2, "OK");
        assert_eq!(buf.to_string(), "OKworld");
    }

    #[test]
    fn degenerate_and_inverted_deletes_are_no_ops() {
        let mut buf = TextBuffer::from_str("hello");
        // Bounds built from values so clippy doesn't const-fold (and
        // reject) the empty / reversed range literals these deliberately
        // are — both must be treated as no-ops by the `start < end` guard.
        let (a, b, c) = (3usize, 4usize, 2usize);
        buf.delete(a..a); // degenerate (start == end)
        assert_eq!(buf.to_string(), "hello");
        buf.delete(b..c); // inverted (start > end)
        assert_eq!(buf.to_string(), "hello");
    }

    #[test]
    fn replace_snaps_a_mid_scalar_start_consistently() {
        // byte 1 falls mid-中 (bytes 0..3); [1,3) snaps to [char 0, char 1)
        // = 中. Replacing it with "Y" must yield "Yab" — the removal and
        // the insertion must anchor at the SAME snapped start (regression
        // guard for the prior raw-byte re-snap).
        let mut buf = TextBuffer::from_str("中ab");
        buf.replace(1..3, "Y");
        assert_eq!(buf.to_string(), "Yab");
    }

    #[test]
    fn conversions_are_correct_after_an_astral_insert() {
        let mut buf = TextBuffer::from_str("ab");
        buf.insert(1, "😀"); // "a😀b"
        assert_eq!(buf.to_string(), "a😀b");
        assert_eq!(buf.len_utf16(), 4); // a + 2 + b
        assert_eq!(buf.byte_to_utf16(buf.len_bytes()), 4);
        assert_eq!(buf.byte_to_utf16(1), 1); // before 😀
        assert_eq!(buf.byte_to_utf16(5), 3); // after 😀 (4-byte), before b
    }

    #[test]
    fn empty_buffer_behaves() {
        let buf = TextBuffer::new();
        assert_eq!(buf.len_bytes(), 0);
        assert_eq!(buf.len_utf16(), 0);
        assert_eq!(buf.len_lines(), 1);
        assert_eq!(buf.byte_to_utf16(0), 0);
        assert_eq!(buf.utf16_to_byte(0), 0);
        assert_eq!(buf.byte_to_line(0), 1);
        assert_eq!(buf.line_to_byte(1), 0);
    }
}

#[cfg(test)]
mod proptests {
    use super::*;
    use proptest::prelude::*;

    proptest! {
        /// byte ↔ UTF-16 must agree with a naive prefix count at every
        /// char boundary, and round-trip back. This is the exact surface
        /// the three hand-rolled Swift walks got subtly wrong.
        #[test]
        fn byte_utf16_roundtrips_against_naive(s in ".*") {
            let buf = TextBuffer::from_str(&s);
            let boundaries = s
                .char_indices()
                .map(|(b, _)| b)
                .chain(std::iter::once(s.len()));
            for byte in boundaries {
                let naive_utf16 = s[..byte].encode_utf16().count();
                prop_assert_eq!(buf.byte_to_utf16(byte), naive_utf16);
                prop_assert_eq!(buf.utf16_to_byte(naive_utf16), byte);
            }
        }

        /// 1-based line of a byte = 1 + the number of newlines before it.
        #[test]
        fn byte_to_line_matches_naive_newline_count(s in ".*") {
            let buf = TextBuffer::from_str(&s);
            for (byte, _) in s.char_indices() {
                let naive_line = s[..byte].matches('\n').count() + 1;
                prop_assert_eq!(buf.byte_to_line(byte), naive_line);
            }
        }

        /// #407: the rope-native line navigation must agree byte-for-byte with
        /// the `&str` line helpers the stateless highlight oracle uses, at every
        /// char boundary (and one-past-the-end). If these ever diverge, the
        /// stateful buffer would materialise a different window than the oracle
        /// computes — exactly the byte-clean equivalence the census protects.
        #[test]
        fn rope_line_nav_matches_str_helpers(s in "(\\PC|\n|\r| ){0,400}") {
            let buf = TextBuffer::from_str(&s);

            // Reference `&str` helpers, copied from `editor_spans` semantics.
            let line_start = |byte: usize| s[..byte].rfind('\n').map_or(0, |i| i + 1);
            let line_end = |byte: usize| {
                let byte = byte.min(s.len());
                s[byte..].find('\n').map_or(s.len(), |i| byte + i + 1)
            };
            let line_is_blank = |ls: usize| {
                s.as_bytes()[ls..line_end(ls)]
                    .iter()
                    .all(|&b| matches!(b, b' ' | b'\t' | 0x0B | 0x0C | b'\r' | b'\n'))
            };
            let extend_up = |start: usize| {
                let mut ls = start;
                while ls > 0 {
                    let prev = line_start(ls - 1);
                    if line_is_blank(prev) {
                        break;
                    }
                    ls = prev;
                }
                ls
            };
            let extend_down = |end: usize| {
                let mut le = end;
                while le < s.len() {
                    if line_is_blank(le) {
                        break;
                    }
                    le = line_end(le);
                }
                le
            };

            let boundaries = s
                .char_indices()
                .map(|(b, _)| b)
                .chain(std::iter::once(s.len()));
            for byte in boundaries {
                let ls = line_start(byte);
                let le = line_end(byte);
                prop_assert_eq!(buf.line_start_byte(byte), ls, "line_start @ {}", byte);
                prop_assert_eq!(buf.line_end_byte(byte), le, "line_end @ {}", byte);
                // Blank-ness is defined on line starts.
                prop_assert_eq!(
                    buf.line_is_blank(ls),
                    line_is_blank(ls),
                    "line_is_blank @ {}",
                    ls
                );
                // Window extension from this line start / line end.
                prop_assert_eq!(buf.extend_up_to_blank(ls), extend_up(ls), "extend_up @ {}", ls);
                prop_assert_eq!(
                    buf.extend_down_to_blank(le),
                    extend_down(le),
                    "extend_down @ {}",
                    le
                );
                // The materialised window equals the slice.
                let win_start = extend_up(ls);
                let win_end = extend_down(le);
                prop_assert_eq!(
                    buf.byte_slice_to_string(win_start..win_end),
                    s[win_start..win_end].to_string(),
                    "byte_slice @ {}..{}",
                    win_start,
                    win_end
                );
            }
        }
    }
}
