// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! FL5-1 (#664): deterministic nested tag trees over the `file_tags`
//! dimension.
//!
//! One query supplies `(tag_norm, file_id)` rows; assembly is pure and
//! in-memory. Intermediate `/` segments materialize as nodes even when
//! no file carries them exactly; every count is DISTINCT FILES, with
//! `file_count` using the settled nested-prefix semantics (a file
//! carrying both `a` and `a/b` counts once toward `a`). Siblings order
//! alphabetically by segment — `tag_norm` is already lowercase by
//! normalization (#564–#567), so byte order IS the casefolded order and
//! the tree is deterministic for any row order.

use std::collections::BTreeMap;

use crate::sidebar_filter::group_thousands;

/// One node of the nested tag tree. `full` is the normalized full tag
/// (`projects/reading`); display case recovery is deliberately absent
/// in v1 — `tag_norm` is lowercase by design and recovering authored
/// case would need a new column (deferred, spec FL5-1 rule 4).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TagTreeNode {
    pub segment: String,
    pub full: String,
    /// Distinct files with this tag OR any descendant (nested).
    pub file_count: u32,
    /// Distinct files with exactly this tag.
    pub direct_count: u32,
    pub children: Vec<TagTreeNode>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TagTree {
    pub roots: Vec<TagTreeNode>,
    /// Markdown files with zero `file_tags` rows.
    pub untagged_count: u32,
    pub audio_summary: String,
}

/// Intermediate assembly node: BTreeMap keys give the alphabetical
/// sibling order for free and deterministically.
#[derive(Default)]
struct Assembly {
    direct_files: Vec<i64>,
    children: BTreeMap<String, Assembly>,
}

impl Assembly {
    /// Materialize into the public node, computing nested distinct-file
    /// counts bottom-up. Returns the node plus the sorted-deduped file
    /// ids of its whole subtree so the parent can merge without
    /// re-walking.
    fn materialize(mut self, segment: &str, prefix: &str) -> (TagTreeNode, Vec<i64>) {
        let full = if prefix.is_empty() {
            segment.to_string()
        } else {
            format!("{prefix}/{segment}")
        };
        self.direct_files.sort_unstable();
        self.direct_files.dedup();
        let direct_count = self.direct_files.len() as u32;

        let mut subtree_files = self.direct_files;
        let mut children = Vec::with_capacity(self.children.len());
        for (child_segment, child) in self.children {
            let (node, child_files) = child.materialize(&child_segment, &full);
            children.push(node);
            subtree_files.extend(child_files);
        }
        subtree_files.sort_unstable();
        subtree_files.dedup();

        (
            TagTreeNode {
                segment: segment.to_string(),
                full,
                file_count: subtree_files.len() as u32,
                direct_count,
                children,
            },
            subtree_files,
        )
    }
}

/// Assemble the tree from raw `(tag_norm, file_id)` rows. `tag_count`
/// in the summary is the number of DISTINCT REAL tags (rows' tag_norm
/// values) — synthesized intermediate segments are navigation, not
/// tags, and are not announced as such.
pub(crate) fn build_tag_tree(rows: &[(String, i64)], untagged_count: u32) -> TagTree {
    let mut root = Assembly::default();
    let mut distinct_tags = std::collections::BTreeSet::new();
    for (tag, file_id) in rows {
        distinct_tags.insert(tag.as_str());
        let mut cursor = &mut root;
        for segment in tag.split('/') {
            cursor = cursor.children.entry(segment.to_string()).or_default();
        }
        cursor.direct_files.push(*file_id);
    }

    let mut roots = Vec::with_capacity(root.children.len());
    for (segment, child) in root.children {
        let (node, _) = child.materialize(&segment, "");
        roots.push(node);
    }

    let audio_summary = summary(distinct_tags.len() as u64, untagged_count);
    TagTree {
        roots,
        untagged_count,
        audio_summary,
    }
}

/// Normative, locale-neutral strings (FL5-1 rule 5): grouped decimals,
/// second clause omitted when nothing is untagged. The UI localizes
/// visual formatting; core summaries stay stable for announcements.
fn summary(tag_count: u64, untagged_count: u32) -> String {
    let tags = group_thousands(tag_count);
    if untagged_count == 0 {
        format!("{tags} tags.")
    } else {
        let untagged = group_thousands(untagged_count as u64);
        format!("{tags} tags, {untagged} untagged notes.")
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn rows(pairs: &[(&str, i64)]) -> Vec<(String, i64)> {
        pairs.iter().map(|(t, f)| (t.to_string(), *f)).collect()
    }

    #[test]
    fn intermediate_segments_materialize_with_zero_direct() {
        let tree = build_tag_tree(&rows(&[("a/b/c", 1)]), 0);
        assert_eq!(tree.roots.len(), 1);
        let a = &tree.roots[0];
        assert_eq!((a.full.as_str(), a.direct_count, a.file_count), ("a", 0, 1));
        let b = &a.children[0];
        assert_eq!(
            (b.full.as_str(), b.direct_count, b.file_count),
            ("a/b", 0, 1)
        );
        let c = &b.children[0];
        assert_eq!(
            (
                c.full.as_str(),
                c.segment.as_str(),
                c.direct_count,
                c.file_count
            ),
            ("a/b/c", "c", 1, 1)
        );
    }

    #[test]
    fn nested_count_dedups_a_file_carrying_parent_and_child() {
        let tree = build_tag_tree(&rows(&[("a", 7), ("a/b", 7), ("a/b", 8)]), 0);
        let a = &tree.roots[0];
        assert_eq!(a.direct_count, 1);
        assert_eq!(a.file_count, 2, "file 7 counts once for a");
        assert_eq!(a.children[0].file_count, 2);
    }

    #[test]
    fn duplicate_rows_do_not_inflate_direct_counts() {
        let tree = build_tag_tree(&rows(&[("t", 1), ("t", 1)]), 0);
        assert_eq!(tree.roots[0].direct_count, 1);
    }

    #[test]
    fn row_order_is_irrelevant() {
        let forward = build_tag_tree(&rows(&[("m", 1), ("a/x", 2), ("a", 3)]), 4);
        let shuffled = build_tag_tree(&rows(&[("a", 3), ("m", 1), ("a/x", 2)]), 4);
        assert_eq!(forward, shuffled);
    }

    #[test]
    fn summary_groups_thousands_and_counts_real_tags_only() {
        let many: Vec<(String, i64)> = (0..1200)
            .map(|i| (format!("bulk/t{i:04}"), i as i64))
            .collect();
        let tree = build_tag_tree(&many, 2500);
        // 1200 real tags; the synthesized `bulk` intermediate is not one.
        assert_eq!(tree.audio_summary, "1,200 tags, 2,500 untagged notes.");
        let tree = build_tag_tree(&rows(&[("only", 1)]), 0);
        assert_eq!(tree.audio_summary, "1 tags.");
    }

    #[test]
    fn empty_vault_summary() {
        let tree = build_tag_tree(&[], 0);
        assert!(tree.roots.is_empty());
        assert_eq!(tree.audio_summary, "0 tags.");
    }
}
