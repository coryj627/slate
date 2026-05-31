// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Incremental 1-based line numbering for in-order extractors (#387).
//!
//! The block/span extractors (`code::extract_code_blocks`,
//! `diagram::extract_diagram_blocks`, `math::extract_math_blocks`) walk the
//! source forwards and record the source line of each item. They used to
//! call a `line_of_offset` that counted newlines from byte 0 every time,
//! making extraction **O(n × blocks)** — ~660 ms on a 2 MB note with ~5.7k
//! blocks, vs ~8 ms for the single pulldown pass it sat behind.
//!
//! Because those offsets arrive **non-decreasing**, [`LineTracker`] counts
//! each newline once over the whole source (O(n) total) by remembering how
//! far it has counted. It stays correct if an offset regresses (recounts
//! from the start) so a future out-of-order caller can't silently get a
//! wrong line — it just pays the old cost for that one call.

/// A forward-walking line counter. Feed it byte offsets in (ideally)
/// non-decreasing order; each [`Self::line_at`] returns the 1-based line of
/// that offset, advancing an internal cursor so the common monotonic case
/// is O(total source length) across all calls rather than O(offset) each.
pub(crate) struct LineTracker<'a> {
    bytes: &'a [u8],
    /// 1-based line number of `cursor`.
    line: u32,
    /// Byte offset up to which `line` has been counted.
    cursor: usize,
}

impl<'a> LineTracker<'a> {
    pub(crate) fn new(source: &'a str) -> Self {
        LineTracker {
            bytes: source.as_bytes(),
            line: 1,
            cursor: 0,
        }
    }

    /// The 1-based line containing byte `offset` (matching the old
    /// `1 + newlines_in(source[..offset])`). Clamps past-the-end offsets to
    /// the source length. If `offset` regresses below the cursor (not
    /// expected from the in-order extractors), it recounts from the start
    /// so the answer is still correct.
    pub(crate) fn line_at(&mut self, offset: usize) -> u32 {
        let offset = offset.min(self.bytes.len());
        if offset < self.cursor {
            self.cursor = 0;
            self.line = 1;
        }
        self.line += self.bytes[self.cursor..offset]
            .iter()
            .filter(|&&b| b == b'\n')
            .count() as u32;
        self.cursor = offset;
        self.line
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    /// The reference the tracker must match: 1 + newlines before `off`.
    fn line_of_offset(source: &str, off: usize) -> u32 {
        1 + source[..off.min(source.len())]
            .bytes()
            .filter(|&b| b == b'\n')
            .count() as u32
    }

    #[test]
    fn monotonic_calls_match_the_reference() {
        let src = "a\nbb\n\nccc\nlast line no newline";
        let mut t = LineTracker::new(src);
        // Every offset, in order, agrees with the from-scratch count.
        for off in 0..=src.len() {
            assert_eq!(t.line_at(off), line_of_offset(src, off), "offset {off}");
        }
    }

    #[test]
    fn repeated_and_past_end_offsets() {
        let src = "x\ny\nz\n";
        let mut t = LineTracker::new(src);
        assert_eq!(t.line_at(0), 1);
        assert_eq!(t.line_at(0), 1); // same offset again
        assert_eq!(t.line_at(2), 2); // start of "y"
        assert_eq!(t.line_at(2), 2); // repeat
        assert_eq!(t.line_at(999), line_of_offset(src, src.len())); // past end clamps
    }

    #[test]
    fn regressing_offset_recounts_correctly() {
        let src = "l1\nl2\nl3\nl4\n";
        let mut t = LineTracker::new(src);
        let mid = src.find("l3").unwrap();
        assert_eq!(t.line_at(mid), 3);
        // Jump backwards — must recount, not return the stale line.
        assert_eq!(t.line_at(0), 1);
        assert_eq!(t.line_at(src.find("l2").unwrap()), 2);
    }

    #[test]
    fn empty_and_single_line() {
        let mut t = LineTracker::new("");
        assert_eq!(t.line_at(0), 1);
        let mut t2 = LineTracker::new("no newlines here");
        assert_eq!(t2.line_at(5), 1);
        assert_eq!(t2.line_at(16), 1);
    }
}
