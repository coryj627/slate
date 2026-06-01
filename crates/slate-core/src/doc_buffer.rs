// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Stateful editor document buffer (#404, #407).
//!
//! [`DocBufferState`] holds the editor's text as a [`TextBuffer`] rope across
//! edits, so the per-keystroke highlight path feeds **edit deltas** instead of
//! re-marshalling the whole document over FFI, and gets O(log n) UTF-16 ↔ byte
//! conversions instead of rebuilding a rope per call (the stateless `text_*`
//! FFI wrappers each did a `TextBuffer::from_str`). The FFI `DocumentBuffer`
//! (in `slate-uniffi`) wraps this in a `Mutex` and clones the rope — O(1),
//! `ropey` shares chunks via `Arc` — for the off-main highlight pass.
//!
//! #404 made the cached block structure (raw + frontmatter-body) and #407 the
//! `%%`-comment index incremental on [`apply_edit`](DocBufferState::apply_edit),
//! and made both `apply_edit` and [`highlight_in_range`] **rope-native**: an
//! edit re-parses only a bounded chunk and the highlight materialises only the
//! window around the edit (via [`highlight_window`]). Neither stringifies the
//! whole document on the keystroke path, so it's truly O(window/edit) — the
//! whole-document `to_string()` survives only in debug asserts and the rare
//! window-cannot-isolate fallback.

use crate::editor_spans::{
    highlight_spans, highlight_window, CommentIndex, RangedHighlight, StructureSnapshot,
};
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
    /// The terminated `%%…%%` comment ranges (#407 Part 3), maintained
    /// incrementally on each [`apply_edit`](Self::apply_edit) so the windowed
    /// highlight's "window inside a comment whose delimiters are outside it"
    /// fallback test costs O(comments) instead of an O(document)
    /// `scan_comments`. `Arc`-shared so a highlight snapshot clones it in O(1)
    /// alongside the two structure snapshots and the rope.
    comment_index: Arc<CommentIndex>,
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
            comment_index: Arc::new(CommentIndex::from_source(text)),
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
        self.comment_index = Arc::new(CommentIndex::from_source(text));
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
        // Capture the deleted slice BEFORE mutating — the comment index's
        // re-scan-vs-shift decision (#407 Part 3) needs to know whether the
        // removed text contained a `%`.
        let deleted = self.buffer.byte_slice_to_string(start_byte..end_byte);
        self.buffer.replace(start_byte..end_byte, new_text);

        // Maintain the cached RAW block structure incrementally (#404 Slice B,
        // #407 rope-native): the prefix above `start_byte` is unchanged, so
        // re-parse only a bounded chunk from a clean break to a reconvergence
        // point — walked directly on the rope (`updated` over the buffer),
        // materialising ONLY that chunk, never the whole document (which would
        // `to_string()` O(n) on the main thread and jank typing). A `debug_assert!`
        // cross-checks it against a from-scratch parse on every edit, turning
        // every test that drives the buffer into a structure-soundness check.
        // The assert is debug-only — in release a divergence would silently
        // flow into `window_diverges`, so the differential census in
        // `editor_spans` (`incremental_structure_*`, 100k+ sequences + the
        // exhaustive single-edit and caveat suites) is the real correctness
        // guarantee. Do not weaken it.
        let updated = self
            .structure
            .updated(&self.buffer, start_byte, end_byte, new_text.len());
        debug_assert_eq!(
            updated,
            StructureSnapshot::from_source(&self.buffer.to_string()),
            "#404: incremental structure diverged from from_source (edit_start={start_byte})"
        );
        self.structure = Arc::new(updated);

        // Maintain the cached frontmatter-BODY structure (#404 Task B). The
        // body framing only moves when the frontmatter itself is touched, which
        // a keystroke in the body never does — so the common path is an
        // incremental `updated` on the body, body-local. The new boundary is
        // found by a bounded head scan on the rope (no whole-document
        // materialise — #407); compare it to the cached old one to decide.
        let old_fm_end = self.fm_end;
        let new_fm_end = rope_fm_end(&self.buffer);
        if start_byte >= old_fm_end && new_fm_end == old_fm_end {
            // Pure body edit, frontmatter framing stable: the body prefix above
            // the edit is byte-identical, so the same clean-break / reconvergence
            // machinery applies in body-local coordinates. The body is a rope
            // slice view (`RopeWindow`) anchored at `new_fm_end`, so only the
            // bounded re-parse chunk is materialised, not the whole body.
            let body = RopeWindow {
                buf: &self.buffer,
                base: new_fm_end,
            };
            let body_updated = self.body_structure.updated(
                &body,
                start_byte - new_fm_end,
                end_byte - old_fm_end,
                new_text.len(),
            );
            debug_assert_eq!(
                body_updated,
                StructureSnapshot::from_source(
                    &self
                        .buffer
                        .byte_slice_to_string(new_fm_end..self.buffer.len_bytes())
                ),
                "#404 Task B: incremental body structure diverged from from_source \
                 (fm_end={new_fm_end} body_edit_start={})",
                start_byte - new_fm_end
            );
            self.body_structure = Arc::new(body_updated);
        } else {
            // The edit touched (or shifted) the frontmatter block — rare. Rebuild
            // the body framing from scratch; the raw structure above already
            // re-parsed through the change. Materialise only the body tail
            // (`[new_fm_end..]`), not the whole document.
            let body = self
                .buffer
                .byte_slice_to_string(new_fm_end..self.buffer.len_bytes());
            self.body_structure = Arc::new(StructureSnapshot::from_source(&body));
        }
        // Keep the cached boundary in lockstep with the new body structure.
        self.fm_end = new_fm_end;

        // Maintain the comment index (#407 Part 3): shift cached ranges for a
        // non-`%` edit, re-scan otherwise. A `debug_assert!` cross-checks it
        // against a whole-document scan every edit; the comment-index census
        // is the real guarantee.
        let mut comment_index = (*self.comment_index).clone();
        // Reads the live rope for the 1-char halo (O(log n)) and, only on the
        // rare re-scan branch, materialises the whole document.
        comment_index.apply_edit(&self.buffer, start_byte, &deleted, new_text);
        debug_assert_eq!(
            comment_index,
            CommentIndex::from_source(&self.buffer.to_string()),
            "#407: comment index diverged from scan_comments (edit_start={start_byte})"
        );
        self.comment_index = Arc::new(comment_index);
    }

    /// Windowed highlight for a dirty range expressed in UTF-16 (#407
    /// window-native). Computes the highlight window by **rope-walking** (line
    /// snap + blank-line extension on the rope, O(window) — never the whole
    /// document), materialises **only that window**, and delegates to
    /// [`highlight_window`] with the two cached structure snapshots and the
    /// incremental comment index. The returned `applied_range` + spans are
    /// whole-document UTF-8 byte offsets (the host converts the bounds back via
    /// [`byte_to_utf16`] and maps the spans against its snapshot, exactly as the
    /// stateless `editor_highlight_spans_in_range` path does). On the rare
    /// `None` (the window can't be parsed in isolation) it materialises the
    /// whole document once and runs [`highlight_spans`] — the documented
    /// whole-document fallback.
    ///
    /// [`byte_to_utf16`]: Self::byte_to_utf16
    pub fn highlight_in_range(
        &self,
        dirty_start_utf16: usize,
        dirty_end_utf16: usize,
    ) -> RangedHighlight {
        // `utf16_to_byte` already lands on a char boundary, so the oracle's
        // floor/ceil snap is a no-op here. `dirty.end.max(dirty.start)` mirrors
        // the oracle's clamp of an inverted range.
        let d_start = self.buffer.utf16_to_byte(dirty_start_utf16);
        let d_end = self
            .buffer
            .utf16_to_byte(dirty_end_utf16.max(dirty_start_utf16));

        // Snap to whole lines, then extend to blank-line boundaries — walked on
        // the rope so the cost tracks the window (block) size, not the document.
        let win_start = self
            .buffer
            .extend_up_to_blank(self.buffer.line_start_byte(d_start));
        let win_end = self
            .buffer
            .extend_down_to_blank(self.buffer.line_end_byte(d_end));

        // Materialise ONLY the window.
        let window_text = self.buffer.byte_slice_to_string(win_start..win_end);
        let window_intersects_comment = self.comment_index.intersects(win_start, win_end);

        highlight_window(
            &window_text,
            win_start,
            self.fm_end,
            &self.structure,
            &self.body_structure,
            window_intersects_comment,
        )
        .unwrap_or_else(|| {
            // Whole-document fallback (rare): the window couldn't be parsed in
            // isolation. This is the only path that materialises the whole doc.
            let text = self.buffer.to_string();
            RangedHighlight {
                applied_range: 0..text.len(),
                spans: highlight_spans(&text),
            }
        })
    }
}

/// A [`DocText`](crate::editor_spans::DocText) view of the rope suffix
/// `buffer[base..]` with body-local offsets — the #407 rope-native equivalent
/// of `&source[fm_end..]`. Every body-local offset `o` maps to `base + o` on
/// the buffer. `base` (= `fm_end`) is **not** necessarily a line start: a
/// closing frontmatter delimiter with trailing whitespace (`--- \n`) leaves
/// `fm_end` right after `---`, mid-line — so `line_start` is **clamped at
/// `base`**, exactly as `(&source[base..]).line_start` stops at body byte 0
/// (its `rfind('\n')` only searches within the body). Used to maintain the
/// frontmatter-body structure incrementally without materialising the body.
struct RopeWindow<'a> {
    buf: &'a TextBuffer,
    base: usize,
}

impl crate::editor_spans::DocText for RopeWindow<'_> {
    fn len(&self) -> usize {
        self.buf.len_bytes() - self.base
    }
    fn line_start(&self, byte: usize) -> usize {
        // Clamp at `base`: a document line start below `base` means the
        // newline that would define it is outside the body, so the body-local
        // line start is 0 — matching `(&source[base..]).line_start`.
        self.buf.line_start_byte(self.base + byte).max(self.base) - self.base
    }
    fn line_end(&self, byte: usize) -> usize {
        // `line_end_byte` only scans forward, so it never crosses below `base`.
        self.buf.line_end_byte(self.base + byte) - self.base
    }
    fn line_is_blank(&self, line_start: usize) -> bool {
        // `TextBuffer::line_is_blank` slices from the GIVEN offset to its
        // line end (it doesn't re-derive the line start), so passing
        // `base + line_start` yields the body-local line `[line_start..le)` —
        // correct even when `base` is mid-line (`line_start == 0` ⇒ the body's
        // leading partial line).
        self.buf.line_is_blank(self.base + line_start)
    }
    fn byte_at(&self, i: usize) -> u8 {
        self.buf.byte(self.base + i)
    }
    fn slice_to_string(&self, start: usize, end: usize) -> String {
        self.buf
            .byte_slice_to_string(self.base + start..self.base + end)
    }
}

/// The frontmatter boundary `fm_end` for the buffer's current text, computed
/// without materialising the whole document (#407). `body_after_frontmatter`
/// only reads from byte 0 to the closing delimiter, so we materialise a
/// bounded head, run it there, and trust the result once it's provably stable
/// under any text below — i.e. once at least one body byte is materialised
/// (the closing `---<eol>` is fully inside the head), or there's no opening
/// delimiter at all, or we've reached EOF. The common keystroke is a no-
/// frontmatter doc (first line isn't `---` → returns 0 on a tiny head) or a
/// small-frontmatter doc (resolves in the first 8 KiB head).
fn rope_fm_end(buf: &TextBuffer) -> usize {
    let total = buf.len_bytes();
    let mut cap = 8192usize;
    loop {
        let end = cap.min(total);
        let head = buf.byte_slice_to_string(0..end);
        let body = crate::frontmatter::body_after_frontmatter(&head);
        let fm_end = head.len() - body.len();
        // Stable: a body byte is materialised, so the closing delimiter line +
        // its newline are fully within `head` — text below can't change this.
        if fm_end > 0 && fm_end < head.len() {
            return fm_end;
        }
        // No opening delimiter at all ⇒ 0 is final (an edit below byte 0 can't
        // create frontmatter at byte 0).
        if fm_end == 0 && !crate::frontmatter::starts_with_opening_delimiter(&head) {
            return 0;
        }
        // Either an unresolved opener (no close seen yet) or a close right at
        // the materialised end — grow, or accept the exact answer at EOF where
        // `head` is the whole document.
        if end == total {
            return fm_end;
        }
        cap = cap.saturating_mul(2);
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

    /// #407: `rope_fm_end` must equal `body_after_frontmatter` exactly,
    /// including the corner the differential census surfaced — a closing
    /// delimiter with TRAILING WHITESPACE (`--- \n`), which leaves `fm_end`
    /// **mid-line** (right after `---`, before the ` \n`). The body view the
    /// buffer maintains (`RopeWindow` anchored there) must not underflow and
    /// must match `&source[fm_end..]`.
    #[test]
    fn rope_fm_end_matches_body_after_frontmatter_incl_mid_line_boundary() {
        let cases = [
            "no frontmatter\n\nbody\n",
            "---\ntitle: x\n---\n\nbody\n",
            "---\ntitle: x\n--- \n\nbody\n", // trailing-space close → mid-line fm_end
            "--- \na: 1\n---\nbody\n",       // trailing-space OPEN delimiter
            "---\n\n---\nbody\n",            // empty frontmatter with blank line
            "---\nunclosed frontmatter\nbody body body\n", // opener, no close → 0
            "---xyz not an opener\nbody\n",  // `---` but not a delimiter line → 0
            "\u{FEFF}---\nbom: 1\n---\nbody\n", // BOM then frontmatter
        ];
        for src in cases {
            let buf = TextBuffer::from_str(src);
            let expected = src.len() - crate::frontmatter::body_after_frontmatter(src).len();
            assert_eq!(
                rope_fm_end(&buf),
                expected,
                "rope_fm_end mismatch on {src:?}"
            );
        }
    }

    /// #407: `rope_fm_end` resolves a frontmatter block whose closing delimiter
    /// lands far past the first 8 KiB head — the grow-and-retry path the small
    /// census docs never reach.
    #[test]
    fn rope_fm_end_grows_past_the_first_head_chunk() {
        // A frontmatter block padded well beyond 8 KiB before its close, then a
        // body. The first head (8 KiB) sees an unresolved opener; the loop must
        // grow until the close is materialised.
        let mut src = String::from("---\n");
        while src.len() < 20_000 {
            src.push_str("padding_key_value_pair: some reasonably long value here\n");
        }
        src.push_str("---\n\nthe body starts here\n");
        let buf = TextBuffer::from_str(&src);
        let expected = src.len() - crate::frontmatter::body_after_frontmatter(&src).len();
        assert_eq!(rope_fm_end(&buf), expected);
        assert!(
            expected > 8192,
            "the close must be past the first head chunk"
        );
    }

    /// #407: end-to-end — a doc whose closing frontmatter delimiter carries
    /// trailing whitespace (mid-line `fm_end`) must still produce windowed
    /// highlights byte-identical to the stateless oracle, through a body edit
    /// sequence (so the incremental `RopeWindow`-fed body structure is
    /// exercised). Regression for the differential-census failure.
    #[test]
    fn trailing_space_close_delimiter_windows_match_stateless() {
        use crate::editor_spans::highlight_spans_in_range;
        let mut text =
            "---\ntitle: t\n--- \n\n# Head\n\nProse [[L]] and `c`.\n\n```\nfn x(){}\n```\n\ntail prose\n"
                .to_string();
        let mut buf = DocBufferState::new(&text);
        let steps: &[(&str, usize, &str)] = &[
            ("tail prose", 0, "X "),
            ("# Head", 6, "\n\nnew para\n"),
            ("Prose", 0, "the "),
        ];
        for &(needle, off, ins) in steps {
            let at = text.find(needle).expect("anchor") + off;
            let start_u16 = text[..at].encode_utf16().count();
            buf.apply_edit(start_u16, 0, ins);
            text = format!("{}{}{}", &text[..at], ins, &text[at..]);
            for frac in [0usize, 25, 50, 75, 100] {
                let d = {
                    let mut b = (text.len() * frac / 100).min(text.len());
                    while b < text.len() && !text.is_char_boundary(b) {
                        b += 1;
                    }
                    b
                };
                let d_u16 = text[..d].encode_utf16().count();
                assert_eq!(
                    buf.highlight_in_range(d_u16, d_u16),
                    highlight_spans_in_range(&text, d..d),
                    "trailing-space close: cached != stateless at byte {d}\n text={text:?}"
                );
            }
        }
    }
}
