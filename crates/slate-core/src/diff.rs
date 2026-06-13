// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Diff-on-save producer for the fine-grained op log (#378, `05` §7.1).
//!
//! [`diff_to_ops`] line-diffs the previous content against the new
//! content at save time and emits byte-range [`EditOp`]s
//! (Insert/Delete/Replace). The save path encodes them into one
//! [`crate::oplog::OpKind::EditBatch`] entry; [`crate::oplog::reconstruct_at_tail`]
//! replays them. This is the **producer** half — single-writer, run under
//! the session connection mutex, never on a keystroke path.
//!
//! **Line granularity** is deliberate: hunks are meaningful units of
//! change, the op count stays proportional to changed lines (not
//! characters), and — because a line break is `\n` only (matching the
//! rest of the `\n`-based backend) — every line boundary is a char
//! boundary, so the emitted byte ranges are multibyte-safe for free.
//! `\r` rides inside line content, so CRLF round-trips byte-for-byte. The
//! ops carry byte ranges regardless of diff granularity, so a future
//! switch to word/char granularity changes only this module.

use similar::{DiffOp, TextDiff};

use crate::oplog::EditOp;

/// Diff `old` → `new` into byte-range edit ops. Offsets are UTF-8 byte
/// offsets in **`old`-content space**; insert/replace text is sliced from
/// `new`. Identical inputs yield an empty `Vec`.
pub fn diff_to_ops(old: &str, new: &str) -> Vec<EditOp> {
    let diff = TextDiff::from_lines(old, new);
    // Line slices retain their trailing `\n`, so concatenating them
    // reproduces `old`/`new` exactly and the cumulative offsets are real
    // byte positions.
    let old_off = cumulative_byte_offsets(diff.iter_old_slices());
    let new_off = cumulative_byte_offsets(diff.iter_new_slices());

    let mut ops = Vec::new();
    // Running byte cursor in OLD-content space — where we are after the
    // ops emitted so far. An `Insert` consumes no old content, so it
    // anchors HERE; `Equal`/`Delete`/`Replace` advance the cursor past
    // their old range. Anchoring an Insert at `old_off[old_index]`
    // instead is WRONG: when `similar` emits an insertion that follows an
    // `Equal` run (a `D…E…I` cover), it reports `old_index` at the start
    // of that run, not its end, so the inserted text lands before the
    // surviving line and `reconstruct_at_tail` produces the wrong
    // document. (Red-team #378 — verified across 500k diff pairs.)
    let mut old_cursor = 0usize;
    for op in diff.ops() {
        match *op {
            DiffOp::Equal { old_index, len, .. } => old_cursor = old_off[old_index + len],
            DiffOp::Delete {
                old_index, old_len, ..
            } => {
                let end = old_off[old_index + old_len];
                ops.push(EditOp::Delete {
                    start: old_off[old_index],
                    end,
                });
                old_cursor = end;
            }
            DiffOp::Insert {
                new_index, new_len, ..
            } => ops.push(EditOp::Insert {
                pos: old_cursor,
                text: new[new_off[new_index]..new_off[new_index + new_len]].to_string(),
            }),
            DiffOp::Replace {
                old_index,
                old_len,
                new_index,
                new_len,
            } => {
                let end = old_off[old_index + old_len];
                ops.push(EditOp::Replace {
                    start: old_off[old_index],
                    end,
                    text: new[new_off[new_index]..new_off[new_index + new_len]].to_string(),
                });
                old_cursor = end;
            }
        }
    }
    ops
}

/// `offs[i]` = byte offset of line `i`'s start; `offs[lines.len()]` =
/// total byte length. Lets a line-index range map to a byte range.
/// Takes an iterator of line slices (`similar` 3.0 exposes these via
/// `iter_old_slices()` / `iter_new_slices()`; the old `old_slices()` /
/// `new_slices()` slice-returning methods were removed).
fn cumulative_byte_offsets<'a>(lines: impl Iterator<Item = &'a str>) -> Vec<usize> {
    let mut offs = vec![0usize];
    let mut acc = 0usize;
    for line in lines {
        acc += line.len();
        offs.push(acc);
    }
    offs
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn identical_yields_no_ops() {
        assert!(diff_to_ops("a\nb\nc\n", "a\nb\nc\n").is_empty());
        assert!(diff_to_ops("", "").is_empty());
    }

    #[test]
    fn empty_old_is_one_insert_of_everything() {
        let ops = diff_to_ops("", "hello\nworld\n");
        assert_eq!(
            ops,
            vec![EditOp::Insert {
                pos: 0,
                text: "hello\nworld\n".to_string()
            }]
        );
    }

    #[test]
    fn empty_new_is_one_delete_of_everything() {
        let ops = diff_to_ops("hello\nworld\n", "");
        assert_eq!(ops, vec![EditOp::Delete { start: 0, end: 12 }]);
    }

    #[test]
    fn single_line_change_is_one_replace_on_that_line() {
        // Toggle-task shape: flip one char on the middle line.
        let old = "- [ ] a\n- [ ] b\n- [ ] c\n";
        let new = "- [ ] a\n- [x] b\n- [ ] c\n";
        let ops = diff_to_ops(old, new);
        // Line 2 spans bytes 8..16 ("- [ ] b\n").
        assert_eq!(
            ops,
            vec![EditOp::Replace {
                start: 8,
                end: 16,
                text: "- [x] b\n".to_string()
            }]
        );
    }

    #[test]
    fn multibyte_lines_keep_byte_ranges_on_char_boundaries() {
        // 中 is 3 bytes; ranges must land on its boundaries.
        let old = "a\n中\nb\n";
        let new = "a\n中中\nb\n";
        let ops = diff_to_ops(old, new);
        // Line "中\n" is bytes 2..6; replaced with "中中\n".
        assert_eq!(
            ops,
            vec![EditOp::Replace {
                start: 2,
                end: 6,
                text: "中中\n".to_string()
            }]
        );
    }

    #[test]
    fn multi_hunk_edit_yields_document_ordered_disjoint_ops() {
        // Insert a line at top, change one in the middle, delete one near
        // the end — non-adjacent hunks in one diff.
        let old = "one\ntwo\nthree\nfour\nfive\n";
        let new = "ZERO\none\ntwo\nTHREE\nfour\n";
        let ops = diff_to_ops(old, new);
        // Document-ordered, disjoint, all in OLD-content byte space.
        assert!(ops.len() >= 2, "expected multiple hunks, got {ops:?}");
        let starts: Vec<usize> = ops
            .iter()
            .map(|o| match o {
                EditOp::Insert { pos, .. } => *pos,
                EditOp::Delete { start, .. } | EditOp::Replace { start, .. } => *start,
            })
            .collect();
        let mut sorted = starts.clone();
        sorted.sort_unstable();
        assert_eq!(starts, sorted, "ops must be document-ordered: {ops:?}");
    }
}
