// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! The two graph summaries' ONE formatter (W6-2 PR 0a, #746; contracts
//! doc `35_graph_contracts.md` 0a-7).
//!
//! P0-3's normative formats used to be composed twice over: once in
//! `session.rs` to fill `audio_summary`, and — had the a11y vocabulary
//! gained typed summary events — a second time in `a11y.rs`. Both now
//! call here, over the count records the session exports on
//! `GraphSnapshot::summary_counts` and `GraphNeighborhood::summary_counts`,
//! so a host that raises [`crate::a11y::GraphA11yEvent::GraphSnapshotSummary`]
//! from the record it was handed speaks the bytes the record's
//! `audio_summary` carries — asserted by
//! `summary_events_render_the_snapshot_fields_verbatim`.
//!
//! The counts' semantics are the session's, named in `graph.rs` on the
//! records themselves and asserted field by field in
//! `session/tests/graph.rs::summary_counts_are_the_index_counts`.
//! Grouped decimals (`1,032`) are these five counts' alone: every other
//! count the graph speaks renders bare (0a-16).

use crate::graph::{GraphNeighborhoodCounts, GraphSnapshotCounts, grouped_decimal};

/// P0-3: `"{n} notes, {e} links. {o} orphans, {g} unresolved targets."`
/// — the second sentence omitted when both of its counts are 0; `"
/// Filtered."` appended when the filter deviates from the defaults.
/// Nouns are fixed plurals (P1 review round 1 finding 5): `1 notes` is
/// the shipped string, pinned by `session/tests/graph.rs`.
pub fn snapshot_summary(counts: &GraphSnapshotCounts) -> String {
    let mut summary = format!(
        "{} notes, {} links.",
        grouped_decimal(counts.notes),
        grouped_decimal(counts.links)
    );
    if counts.orphans > 0 || counts.unresolved > 0 {
        summary.push_str(&format!(
            " {} orphans, {} unresolved targets.",
            grouped_decimal(counts.orphans),
            grouped_decimal(counts.unresolved)
        ));
    }
    if counts.filtered {
        summary.push_str(" Filtered.");
    }
    summary
}

/// P0-3: `"{label}: {in} links in, {out} links out. Showing {n} notes
/// within {d} links."` — the three counts grouped, the depth bare.
pub fn neighborhood_summary(counts: &GraphNeighborhoodCounts) -> String {
    format!(
        "{}: {} links in, {} links out. Showing {} notes within {} links.",
        counts.center_label,
        grouped_decimal(u64::from(counts.in_links)),
        grouped_decimal(u64::from(counts.out_links)),
        grouped_decimal(counts.note_count),
        counts.depth,
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn snapshot_summary_omits_the_second_sentence_only_when_both_counts_are_zero() {
        let base = GraphSnapshotCounts {
            notes: 1,
            links: 1,
            orphans: 0,
            unresolved: 0,
            filtered: false,
        };
        assert_eq!(snapshot_summary(&base), "1 notes, 1 links.");
        assert_eq!(
            snapshot_summary(&GraphSnapshotCounts { orphans: 1, ..base }),
            "1 notes, 1 links. 1 orphans, 0 unresolved targets."
        );
        assert_eq!(
            snapshot_summary(&GraphSnapshotCounts {
                unresolved: 2,
                filtered: true,
                ..base
            }),
            "1 notes, 1 links. 0 orphans, 2 unresolved targets. Filtered."
        );
    }

    #[test]
    fn summaries_group_their_counts_and_leave_the_depth_bare() {
        assert_eq!(
            snapshot_summary(&GraphSnapshotCounts {
                notes: 247,
                links: 1032,
                orphans: 12,
                unresolved: 3,
                filtered: false,
            }),
            "247 notes, 1,032 links. 12 orphans, 3 unresolved targets."
        );
        assert_eq!(
            neighborhood_summary(&GraphNeighborhoodCounts {
                center_label: "Hub".into(),
                in_links: 1032,
                out_links: 4,
                note_count: 1206,
                depth: 3,
            }),
            "Hub: 1,032 links in, 4 links out. Showing 1,206 notes within 3 links."
        );
    }
}
