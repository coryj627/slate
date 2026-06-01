// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Stateful editor document buffer (#404 Slice A).
//!
//! [`DocBufferState`] holds the editor's text as a [`TextBuffer`] rope across
//! edits, so the per-keystroke highlight path feeds **edit deltas** instead of
//! re-marshalling the whole document over FFI, and gets O(log n) UTF-16 ↔ byte
//! conversions instead of rebuilding a rope per call (the stateless `text_*`
//! FFI wrappers each did a `TextBuffer::from_str`). The FFI `DocumentBuffer`
//! (in `slate-uniffi`) wraps this in a `Mutex` and clones the rope — O(1),
//! `ropey` shares chunks via `Arc` — for the off-main highlight pass.
//!
//! Slice A still parses the block structure whole-document inside
//! [`highlight_spans_in_range`]; Slice B (#404) will maintain that structure
//! incrementally on [`DocBufferState::apply_edit`] so the keystroke path is
//! truly O(edit).

use crate::editor_spans::{highlight_spans_in_range_with, RangedHighlight, StructureSnapshot};
use crate::text_buffer::TextBuffer;
use std::sync::Arc;

/// Owned editor-buffer state. Plain Rust (no uniffi) so the edit / convert /
/// highlight logic is unit-testable without the binding layer. `Clone` is
/// O(1): [`TextBuffer`] wraps a `ropey::Rope` whose chunks are `Arc`-shared —
/// which is what lets the FFI layer snapshot the buffer for an off-main
/// highlight without copying the document.
#[derive(Debug, Clone, Default)]
pub struct DocBufferState {
    buffer: TextBuffer,
    /// The document's block structure (#404 Slice B), maintained incrementally
    /// on each [`apply_edit`](Self::apply_edit) so the keystroke highlight path
    /// never re-parses the whole document. `Arc`-shared so a highlight snapshot
    /// clones it in O(1) (the buffer's `Clone` is what the FFI layer uses to
    /// hand the off-main pass a consistent rope + structure pair).
    structure: Arc<StructureSnapshot>,
}

impl DocBufferState {
    /// Build from the full document text (initial load).
    pub fn new(text: &str) -> Self {
        Self {
            buffer: TextBuffer::from_str(text),
            structure: Arc::new(StructureSnapshot::from_source(text)),
        }
    }

    /// Replace the whole document — initial load, external reload, or any
    /// programmatic `string =` swap. The host calls this (not [`apply_edit`])
    /// whenever it can't express the change as a single delta, keeping the
    /// buffer in lockstep with its text store.
    ///
    /// [`apply_edit`]: Self::apply_edit
    pub fn reset(&mut self, text: &str) {
        self.buffer = TextBuffer::from_str(text);
        self.structure = Arc::new(StructureSnapshot::from_source(text));
    }

    /// The document length in UTF-16 code units. The host compares this to its
    /// text store's length as a cheap drift guard (#404): a mismatch means a
    /// delta was missed and the host must [`reset`] + fall back to a
    /// whole-document highlight that pass.
    ///
    /// [`reset`]: Self::reset
    pub fn len_utf16(&self) -> usize {
        self.buffer.len_utf16()
    }

    /// Convert a whole-document UTF-8 byte offset to a UTF-16 code-unit offset
    /// on the live rope (O(log n)) — the host maps an `applied_range` back to
    /// the UTF-16 offsets its text view uses.
    pub fn byte_to_utf16(&self, byte: usize) -> usize {
        self.buffer.byte_to_utf16(byte)
    }

    /// Apply one edit delta expressed in UTF-16, matching AppKit's
    /// `editedRange` + `changeInLength`: replace `old_len_utf16` UTF-16 units
    /// at `start_utf16` with `new_text`. The UTF-16 bounds are resolved to
    /// bytes against the **pre-edit** rope (before the mutation), so the
    /// removal and insertion anchor at the same position.
    pub fn apply_edit(&mut self, start_utf16: usize, old_len_utf16: usize, new_text: &str) {
        let start_byte = self.buffer.utf16_to_byte(start_utf16);
        let end_byte = self.buffer.utf16_to_byte(start_utf16 + old_len_utf16);
        self.buffer.replace(start_byte..end_byte, new_text);
        // Maintain the cached block structure incrementally (#404 Slice B): the
        // prefix above `start_byte` is unchanged, so re-parse only the suffix
        // from a clean break. A `debug_assert!` cross-checks it against a
        // from-scratch parse on every edit, turning every test that drives the
        // buffer into a structure-soundness check.
        let new_source = self.buffer.to_string();
        let updated = self.structure.updated(&new_source, start_byte);
        debug_assert_eq!(
            updated,
            StructureSnapshot::from_source(&new_source),
            "#404: incremental structure diverged from from_source (edit_start={start_byte})"
        );
        self.structure = Arc::new(updated);
    }

    /// Windowed highlight for a dirty range expressed in UTF-16. Materialises
    /// the current text once and delegates to [`highlight_spans_in_range`]; the
    /// returned `applied_range` + spans are whole-document UTF-8 byte offsets
    /// (the host converts the bounds back via [`byte_to_utf16`] and maps the
    /// spans against its snapshot, exactly as the stateless
    /// `editor_highlight_spans_in_range` path does).
    ///
    /// [`byte_to_utf16`]: Self::byte_to_utf16
    pub fn highlight_in_range(
        &self,
        dirty_start_utf16: usize,
        dirty_end_utf16: usize,
    ) -> RangedHighlight {
        let start_byte = self.buffer.utf16_to_byte(dirty_start_utf16);
        let end_byte = self.buffer.utf16_to_byte(dirty_end_utf16);
        let text = self.buffer.to_string();
        highlight_spans_in_range_with(&text, start_byte..end_byte, &self.structure)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::editor_spans::highlight_spans;

    /// Apply a UTF-16 `(start, old_len, new)` delta to a `String` the way
    /// [`DocBufferState::apply_edit`] applies it to the rope — the independent
    /// reference the buffer must track byte-for-byte.
    fn apply_to_string(
        s: &str,
        start_utf16: usize,
        old_len_utf16: usize,
        new_text: &str,
    ) -> String {
        let units: Vec<u16> = s.encode_utf16().collect();
        let head = String::from_utf16(&units[..start_utf16]).unwrap();
        let tail = String::from_utf16(&units[start_utf16 + old_len_utf16..]).unwrap();
        format!("{head}{new_text}{tail}")
    }

    #[test]
    fn apply_edit_tracks_a_reference_string_through_a_sequence() {
        let mut reference = "Hello, world.\n\nSecond paragraph.".to_string();
        let mut buf = DocBufferState::new(&reference);

        // insert mid-line, delete a run, replace a run, insert at EOF.
        let edits: &[(usize, usize, &str)] = &[
            (5, 0, " there"),                          // "Hello there, world."
            (0, 5, "Hi"),                              // replace "Hello" -> "Hi"
            (reference_len(&reference).min(7), 1, ""), // delete one unit
        ];
        for &(start, old, new) in edits {
            reference = apply_to_string(&reference, start, old, new);
            buf.apply_edit(start, old, new);
            assert_eq!(buf.buffer.to_string(), reference);
            assert_eq!(buf.len_utf16(), reference.encode_utf16().count());
        }
    }

    fn reference_len(s: &str) -> usize {
        s.encode_utf16().count()
    }

    #[test]
    fn apply_edit_handles_multibyte_and_astral_units() {
        // "a😀b": 😀 is U+1F600 — 4 UTF-8 bytes, 2 UTF-16 units (a surrogate
        // pair). Inserting after it must land at UTF-16 index 3 (= byte 5).
        let mut buf = DocBufferState::new("a😀b");
        assert_eq!(buf.len_utf16(), 4);
        buf.apply_edit(3, 0, "Z"); // after the pair, before 'b'
        assert_eq!(buf.buffer.to_string(), "a😀Zb");
        // Delete the astral pair (2 UTF-16 units at index 1).
        buf.apply_edit(1, 2, "");
        assert_eq!(buf.buffer.to_string(), "aZb");
    }

    #[test]
    fn reset_replaces_the_whole_document() {
        let mut buf = DocBufferState::new("old contents");
        buf.apply_edit(0, 3, "");
        buf.reset("brand new contents\n\nwith paragraphs");
        assert_eq!(
            buf.buffer.to_string(),
            "brand new contents\n\nwith paragraphs"
        );
        assert_eq!(
            buf.len_utf16(),
            "brand new contents\n\nwith paragraphs"
                .encode_utf16()
                .count()
        );
    }

    #[test]
    fn highlight_in_range_matches_whole_doc_slice_after_edits() {
        // A doc with real structure the windowing must reproduce.
        let src = "# Title\n\nA paragraph with a [[wikilink]] in it.\n\n\
                   ```rust\nfn main() {}\n```\n\nClosing paragraph here.";
        let mut buf = DocBufferState::new(src);

        // Edit inside the closing paragraph (a context-independent block).
        let dirty_start = src.encode_utf16().count(); // EOF
        buf.apply_edit(dirty_start, 0, " More.");
        let text = buf.buffer.to_string();

        // Re-derive the dirty UTF-16 range and check the windowed result
        // equals the whole-doc spans sliced to the applied range.
        let ranged =
            buf.highlight_in_range(dirty_start, dirty_start + " More.".encode_utf16().count());
        let whole = highlight_spans(&text);
        let expected: Vec<_> = whole
            .iter()
            .filter(|s| {
                (s.start_byte as usize) < ranged.applied_range.end
                    && ranged.applied_range.start < (s.end_byte as usize)
            })
            .cloned()
            .collect();
        // The windowed spans cover the applied range; every windowed span is a
        // whole-doc span (the #379 invariant, exercised through the buffer).
        for s in &ranged.spans {
            assert!(
                whole.contains(s),
                "windowed span {s:?} absent from whole-doc spans"
            );
        }
        // And no whole-doc span strictly inside the applied range is missed.
        for s in &expected {
            if (s.start_byte as usize) >= ranged.applied_range.start
                && (s.end_byte as usize) <= ranged.applied_range.end
            {
                assert!(
                    ranged.spans.contains(s),
                    "whole-doc span {s:?} inside applied range missing from window"
                );
            }
        }
    }

    #[test]
    fn highlight_in_range_falls_back_inside_frontmatter() {
        let src = "---\ntitle: Hello\n---\n\nBody paragraph here.";
        let mut buf = DocBufferState::new(src);
        // Type inside the YAML frontmatter — any window touching it reclassifies
        // the boundary, so the core falls back to a whole-document parse.
        let dirty = "---\ntitle: Hel".encode_utf16().count();
        buf.apply_edit(dirty, 0, "X");
        let text = buf.buffer.to_string();
        let ranged = buf.highlight_in_range(dirty, dirty + 1);
        // Fallback signals as applied_range == whole document.
        assert_eq!(ranged.applied_range, 0..text.len());
        assert_eq!(ranged.spans, highlight_spans(&text));
    }
}
