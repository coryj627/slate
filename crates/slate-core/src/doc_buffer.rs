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
    /// The RAW-source block structure (#404 Slice B), maintained incrementally
    /// on each [`apply_edit`](Self::apply_edit) so the keystroke highlight path
    /// never re-parses the whole document. `Arc`-shared so a highlight snapshot
    /// clones it in O(1) (the buffer's `Clone` is what the FFI layer uses to
    /// hand the off-main pass a consistent rope + structure pair).
    structure: Arc<StructureSnapshot>,
    /// The frontmatter-STRIPPED body's block structure (#404 Task B), i.e.
    /// `from_source(&text[fm_end..])` with body-local offsets. The ranged
    /// highlight runs a second `window_diverges` on the body framing to catch
    /// the CRITICAL #2 un-pairing case; caching it keeps that off the
    /// per-keystroke O(document) path. Kept in lockstep with [`fm_end`] so
    /// `body_structure` is always the structure of `text[fm_end..]`.
    ///
    /// [`fm_end`]: Self::fm_end
    body_structure: Arc<StructureSnapshot>,
    /// Byte length of the leading YAML frontmatter block (0 when there is
    /// none) — the boundary [`body_structure`](Self::body_structure) is
    /// anchored at. Cached so `apply_edit` can decide, without re-deriving the
    /// old boundary, whether an edit is a pure body edit that leaves the
    /// frontmatter framing stable (the common keystroke) or touches it (rare).
    fm_end: usize,
}

impl DocBufferState {
    /// Build from the full document text (initial load).
    pub fn new(text: &str) -> Self {
        let (fm_end, body_structure) = Self::body_framing(text);
        Self {
            buffer: TextBuffer::from_str(text),
            structure: Arc::new(StructureSnapshot::from_source(text)),
            body_structure: Arc::new(body_structure),
            fm_end,
        }
    }

    /// Parse the frontmatter boundary `fm_end` and the body-framing structure
    /// (`from_source(&text[fm_end..])`, body-local offsets) from a full text.
    /// The from-scratch path shared by `new` / `reset` / the frontmatter-
    /// touched rebuild branch of [`apply_edit`](Self::apply_edit).
    fn body_framing(text: &str) -> (usize, StructureSnapshot) {
        let body = crate::frontmatter::body_after_frontmatter(text);
        let fm_end = text.len() - body.len();
        (fm_end, StructureSnapshot::from_source(body))
    }

    /// Replace the whole document — initial load, external reload, or any
    /// programmatic `string =` swap. The host calls this (not [`apply_edit`])
    /// whenever it can't express the change as a single delta, keeping the
    /// buffer in lockstep with its text store.
    ///
    /// [`apply_edit`]: Self::apply_edit
    pub fn reset(&mut self, text: &str) {
        let (fm_end, body_structure) = Self::body_framing(text);
        self.buffer = TextBuffer::from_str(text);
        self.structure = Arc::new(StructureSnapshot::from_source(text));
        self.body_structure = Arc::new(body_structure);
        self.fm_end = fm_end;
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
        // Maintain the cached RAW block structure incrementally (#404 Slice B):
        // the prefix above `start_byte` is unchanged, so re-parse only a bounded
        // chunk from a clean break to a reconvergence point. A `debug_assert!`
        // cross-checks it against a from-scratch parse on every edit, turning
        // every test that drives the buffer into a structure-soundness check.
        let new_source = self.buffer.to_string();
        let updated = self
            .structure
            .updated(&new_source, start_byte, end_byte, new_text.len());
        debug_assert_eq!(
            updated,
            StructureSnapshot::from_source(&new_source),
            "#404: incremental structure diverged from from_source (edit_start={start_byte})"
        );
        self.structure = Arc::new(updated);

        // Maintain the cached frontmatter-BODY structure (#404 Task B). The
        // body framing only moves when the frontmatter itself is touched, which
        // a keystroke in the body never does — so the common path is an
        // incremental `updated` on the body, body-local. `body_after_frontmatter`
        // is a cheap byte scan (no pulldown); compare its new boundary to the
        // cached old one to decide.
        let old_fm_end = self.fm_end;
        let new_fm_end =
            new_source.len() - crate::frontmatter::body_after_frontmatter(&new_source).len();
        if start_byte >= old_fm_end && new_fm_end == old_fm_end {
            // Pure body edit, frontmatter framing stable: the body prefix above
            // the edit is byte-identical, so the same clean-break / reconvergence
            // machinery applies in body-local coordinates.
            let new_body = &new_source[new_fm_end..];
            let body_updated = self.body_structure.updated(
                new_body,
                start_byte - new_fm_end,
                end_byte - old_fm_end,
                new_text.len(),
            );
            debug_assert_eq!(
                body_updated,
                StructureSnapshot::from_source(new_body),
                "#404 Task B: incremental body structure diverged from from_source \
                 (fm_end={new_fm_end} body_edit_start={})",
                start_byte - new_fm_end
            );
            self.body_structure = Arc::new(body_updated);
        } else {
            // The edit touched (or shifted) the frontmatter block — rare. Rebuild
            // the body framing from scratch; the raw structure above already
            // re-parsed through the change. `body_framing` re-derives the same
            // boundary as `new_fm_end`, so only the structure needs replacing.
            let (_fm_end, body_structure) = Self::body_framing(&new_source);
            debug_assert_eq!(_fm_end, new_fm_end);
            self.body_structure = Arc::new(body_structure);
        }
        // Keep the cached boundary in lockstep with the new body structure.
        self.fm_end = new_fm_end;
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
        // Both cached snapshots are passed in: the raw structure and the
        // frontmatter-body framing (#404 Task B), so neither the whole-document
        // nor the whole-body parse lands on the keystroke path.
        highlight_spans_in_range_with(
            &text,
            start_byte..end_byte,
            &self.structure,
            Some(&self.body_structure),
        )
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::editor_spans::{highlight_spans, EditorSpan};

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

    /// #404 Task B census: drive a SEQUENCE of edits across a frontmatter doc
    /// (carrying the incrementally-maintained `body_structure` forward, so a
    /// body-cache error compounds) and after every edit assert the cached
    /// buffer's `highlight_in_range` is byte-identical to a freshly-constructed
    /// buffer's (whose `body_structure` is `from_source(body)` this turn — the
    /// ground truth) AND equals the whole-document spans within its applied
    /// range. The `apply_edit` `debug_assert!` already guards the cached body
    /// against `from_source`; this validates it end-to-end through the ranged
    /// highlight (the CRITICAL #2 body-framing decision is what consumes it).
    #[test]
    fn cached_body_structure_tracks_fresh_over_a_sequence_with_frontmatter() {
        // Frontmatter + a body rich in the shapes the body-framing check cares
        // about (a fence the strip could un-pair, a blockquote/wikilink, prose).
        let mut reference = "---\ntitle: T\ntags: [a, b]\n---\n\n\
             # Heading\n\nProse with a [[Wikilink]] and `code`.\n\n\
             ```rust\nfn main() {}\n```\n\n\
             > A quote with [@cite] inside.\n\nClosing prose here.\n"
            .to_string();
        let mut buf = DocBufferState::new(&reference);

        // Body edits at varied offsets — insert, replace, delete — none touching
        // the frontmatter, so each takes the incremental body-cache path.
        let body_anchor = reference.find("Closing").unwrap();
        let edits: &[(usize, usize, &str)] = &[
            (body_anchor, 0, "X "),                               // insert mid-prose
            (reference.find("[[Wikilink]]").unwrap(), 0, "the "), // before a wikilink
            (reference.encode_utf16().count(), 0, "\n\nNew tail para.\n"), // append at EOF
            (reference.find("Heading").unwrap(), 7, "Title"),     // replace a heading word
        ];
        for &(start, old, new) in edits {
            // Apply to the reference string the same way the buffer applies it.
            reference = apply_to_string(&reference, start, old, new);
            buf.apply_edit(start, old, new);
            assert_eq!(buf.buffer.to_string(), reference, "buffer text drifted");

            // Sweep a dirty range across the whole document; the cached buffer
            // and a fresh buffer must agree, and both must match the whole-doc
            // slice for that applied range.
            let fresh = DocBufferState::new(&reference);
            let units = reference.encode_utf16().count();
            let whole = highlight_spans(&reference);
            for frac in [0.0, 0.25, 0.5, 0.75, 1.0] {
                let d = ((units as f64) * frac) as usize;
                let cached_ranged = buf.highlight_in_range(d, d);
                let fresh_ranged = fresh.highlight_in_range(d, d);
                assert_eq!(
                    cached_ranged, fresh_ranged,
                    "cached body structure diverged from fresh at d={d}"
                );
                // #379 invariant through the cached buffer.
                let (a, b) = (
                    cached_ranged.applied_range.start,
                    cached_ranged.applied_range.end,
                );
                let mut expected: Vec<_> = whole
                    .iter()
                    .filter(|s| (s.start_byte as usize) >= a && (s.end_byte as usize) <= b)
                    .cloned()
                    .collect();
                let mut got = cached_ranged.spans.clone();
                let key = |s: &EditorSpan| (s.start_byte, s.end_byte, format!("{:?}", s.kind));
                expected.sort_by_key(key);
                got.sort_by_key(key);
                assert_eq!(got, expected, "ranged != whole-doc slice at d={d}");
            }
        }
    }
}
