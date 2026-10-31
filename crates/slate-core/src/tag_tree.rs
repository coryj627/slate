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

use crate::sidebar_filter::count_noun;

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
pub(crate) fn summary(tag_count: u64, untagged_count: u32) -> String {
    let tags = count_noun(tag_count, "tag", "tags");
    if untagged_count == 0 {
        format!("{tags}.")
    } else {
        let untagged = count_noun(untagged_count as u64, "untagged note", "untagged notes");
        format!("{tags}, {untagged}.")
    }
}

/// WB-25 (#1290): one flattened pre-order row of the tag tree — what the
/// paged walk carries and the FFI's `TagTreeEntry` is built from. `depth`
/// is 0 for a root; a row's parent is the nearest earlier row one level up.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TagTreePageEntry {
    pub segment: String,
    pub full: String,
    pub file_count: u32,
    pub direct_count: u32,
    pub depth: u32,
}

/// WB-25 (#1290): one page of the flattened tag tree. `next_cursor` is
/// opaque: the host passes it back unread. `total_entries` counts every
/// row of the whole tree (real tags and implied ancestors); the untagged
/// count and the summary ride every page.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TagTreePage {
    pub entries: Vec<TagTreePageEntry>,
    pub next_cursor: Option<String>,
    pub total_entries: u32,
    pub untagged_count: u32,
    pub audio_summary: String,
}

/// The tree's rows in pre-order: one flattening, shared by the unpaged
/// tree's bindings and the paged walk's oracle.
pub fn flatten_preorder(tree: &TagTree) -> Vec<TagTreePageEntry> {
    fn walk(node: &TagTreeNode, depth: u32, out: &mut Vec<TagTreePageEntry>) {
        out.push(TagTreePageEntry {
            segment: node.segment.clone(),
            full: node.full.clone(),
            file_count: node.file_count,
            direct_count: node.direct_count,
            depth,
        });
        for child in &node.children {
            walk(child, depth + 1, out);
        }
    }
    let mut out = Vec::new();
    for root in &tree.roots {
        walk(root, 0, &mut out);
    }
    out
}

/// WB-25 (#1290): the order [`build_tag_tree`] produces, over node KEYS (a
/// node's segment path joined with `/`): the segment sequences compared
/// lexicographically, each segment in byte order, a proper prefix first.
/// It splits exactly as `build_tag_tree` does, so it is that tree's
/// pre-order, and it never equates two different keys (the split gives the
/// key back): a character below `/` (U+0001, U+0000) can neither collide
/// with a separator nor sort across one.
pub fn cmp_segments(a: &str, b: &str) -> std::cmp::Ordering {
    a.split('/').cmp(b.split('/'))
}

/// A node's `full` from its key, `materialize`'s rule: leading empty
/// segments add no separator, so a node's `full` is its key with its
/// leading `/`s trimmed. Not an identity — `/a`, `//a` and `a` all read
/// `a` — which is why order, cursor and counts use the key.
fn full_of_key(key: &str) -> &str {
    key.trim_start_matches('/')
}

fn page_entry(key: &str, direct_count: u32, file_count: u32) -> TagTreePageEntry {
    TagTreePageEntry {
        segment: key.rsplit('/').next().unwrap_or_default().to_string(),
        full: full_of_key(key).to_string(),
        file_count,
        direct_count,
        depth: key.split('/').count() as u32 - 1,
    }
}

/// How many leading segments two keys' `split('/')` sequences share.
fn common_segments(a: &str, b: &str) -> usize {
    a.split('/')
        .zip(b.split('/'))
        .take_while(|(x, y)| x == y)
        .count()
}

/// Every key of the tree in pre-order from after `cursor` (from the start
/// when `None`): before each real tag, the proper prefixes of its
/// `split('/')` sequence not yet emitted (its implied ancestors), shallowest
/// first, then the tag. `groups` holds the distinct real tags with their
/// direct counts, sorted by [`cmp_segments`]. `visit` sees each key with its
/// direct count (0 for an implied ancestor) and answers whether to go on;
/// `check` runs once per real tag, emitted or not.
///
/// Linear in the tags' bytes (codex round 1): everything up to the last
/// emitted key (or the cursor) is already out, so a tag's unseen prefixes
/// are exactly those deeper than the segments it shares with that key —
/// sliced from the tag at its separators, never re-joined or compared.
fn walk_keys_after<E>(
    groups: &[(String, u32)],
    cursor: Option<&str>,
    check: &mut impl FnMut() -> Result<(), E>,
    mut visit: impl FnMut(&str, u32) -> Result<bool, E>,
) -> Result<(), E> {
    let start = cursor.map_or(0, |cursor| {
        groups.partition_point(|(key, _)| cmp_segments(key, cursor) != std::cmp::Ordering::Greater)
    });
    let mut last: Option<&str> = cursor;
    for (key, direct) in &groups[start..] {
        check()?;
        let seen = last.map_or(0, |last| common_segments(key, last));
        for (depth, (separator, _)) in key.match_indices('/').enumerate() {
            if depth + 1 > seen && !visit(&key[..separator], 0)? {
                return Ok(());
            }
        }
        if !visit(key, *direct)? {
            return Ok(());
        }
        last = Some(key);
    }
    Ok(())
}

/// The rows of the whole tree (real tags plus implied ancestors): each tag
/// adds its segments past those it shares with the tag before it. `check`
/// runs once per tag.
pub(crate) fn total_entries<E>(
    groups: &[(String, u32)],
    check: &mut impl FnMut() -> Result<(), E>,
) -> Result<u32, E> {
    let mut total = 0usize;
    let mut last: Option<&str> = None;
    for (key, _) in groups {
        check()?;
        let segments = key.split('/').count();
        total += segments - last.map_or(0, |last| common_segments(key, last));
        last = Some(key);
    }
    Ok(u32::try_from(total).unwrap_or(u32::MAX))
}

/// `groups` sorted by [`cmp_segments`] (stable), `check` run before every
/// comparison: a merge sort, so a cancel lands between two comparisons of
/// a long-tag sort instead of after the whole sort (codex round 1).
pub(crate) fn sort_by_segments<E>(
    mut groups: Vec<(String, u32)>,
    check: &mut impl FnMut() -> Result<(), E>,
) -> Result<Vec<(String, u32)>, E> {
    if groups.len() < 2 {
        return Ok(groups);
    }
    let right = groups.split_off(groups.len() / 2);
    let left = sort_by_segments(groups, check)?;
    let right = sort_by_segments(right, check)?;
    let mut merged = Vec::with_capacity(left.len() + right.len());
    let mut left = left.into_iter().peekable();
    let mut right = right.into_iter().peekable();
    while let (Some(a), Some(b)) = (left.peek(), right.peek()) {
        check()?;
        let next = if cmp_segments(&b.0, &a.0) == std::cmp::Ordering::Less {
            right.next()
        } else {
            left.next()
        };
        merged.extend(next);
    }
    merged.extend(left);
    merged.extend(right);
    Ok(merged)
}

/// One page of at most `limit` rows after `cursor`, each row's nested
/// count from `file_count(key)`; the cursor of the next page is the last
/// emitted row's key, `None` once the tree is exhausted.
pub(crate) fn page_after<E>(
    groups: &[(String, u32)],
    cursor: Option<&str>,
    limit: usize,
    check: &mut impl FnMut() -> Result<(), E>,
    mut file_count: impl FnMut(&str) -> Result<u32, E>,
) -> Result<(Vec<TagTreePageEntry>, Option<String>), E> {
    let mut entries = Vec::new();
    let mut last_key: Option<String> = None;
    let mut more = false;
    walk_keys_after(groups, cursor, check, |key, direct| {
        if entries.len() == limit {
            more = true;
            return Ok(false);
        }
        entries.push(page_entry(key, direct, file_count(key)?));
        last_key = Some(key.to_string());
        Ok(true)
    })?;
    Ok((entries, if more { last_key } else { None }))
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
        assert_eq!(tree.audio_summary, "1 tag.");
    }

    #[test]
    fn summary_pluralizes_each_count_independently() {
        // Exactly one is singular; zero and many stay plural, and the
        // two counts never borrow each other's agreement.
        assert_eq!(
            build_tag_tree(&rows(&[("only", 1)]), 1).audio_summary,
            "1 tag, 1 untagged note."
        );
        assert_eq!(
            build_tag_tree(&rows(&[("only", 1)]), 2).audio_summary,
            "1 tag, 2 untagged notes."
        );
        assert_eq!(
            build_tag_tree(&rows(&[("a", 1), ("b", 2)]), 1).audio_summary,
            "2 tags, 1 untagged note."
        );
        assert_eq!(
            build_tag_tree(&[], 1).audio_summary,
            "0 tags, 1 untagged note."
        );
    }

    #[test]
    fn empty_vault_summary() {
        let tree = build_tag_tree(&[], 0);
        assert!(tree.roots.is_empty());
        assert_eq!(tree.audio_summary, "0 tags.");
    }

    /// WB-25 (#1290): over the adversarial tag set, every node's key (its
    /// segment path joined with `/`, rebuilt from the flattened rows) sorted
    /// by `cmp_segments` is `flatten_preorder`'s order; every node's `full`
    /// is `full_of_key` of its key; and no two different keys compare equal.
    #[test]
    fn cmp_segments_is_build_tag_trees_preorder_and_never_equates_two_tags() {
        let tags = [
            "a",
            "a/",
            "a//b",
            "a/z",
            "a\u{0}b",
            "a\u{0}b/c",
            "a\u{1}z",
            "a\u{1}z/k",
            "a b",
            "a-b",
            "a.b/c",
            "a_b/child",
            "axb/child",
            "/a",
            "//a",
        ];
        let rows: Vec<(String, i64)> = tags
            .iter()
            .enumerate()
            .map(|(i, t)| (t.to_string(), i as i64))
            .collect();
        let flat = flatten_preorder(&build_tag_tree(&rows, 0));
        let mut stack: Vec<String> = Vec::new();
        let keys: Vec<String> = flat
            .iter()
            .map(|entry| {
                stack.truncate(entry.depth as usize);
                stack.push(entry.segment.clone());
                stack.join("/")
            })
            .collect();
        for (entry, key) in flat.iter().zip(&keys) {
            assert_eq!(entry.full, full_of_key(key), "the full of {key:?}");
        }
        let mut sorted = keys.clone();
        sorted.sort_by(|a, b| cmp_segments(a, b));
        assert_eq!(
            sorted, keys,
            "cmp_segments is not build_tag_tree's pre-order"
        );
        for x in &keys {
            for y in &keys {
                assert_eq!(
                    cmp_segments(x, y) == std::cmp::Ordering::Equal,
                    x == y,
                    "{x:?} vs {y:?}"
                );
            }
        }
        // The leading-empty-segment leaves read like the root `a`.
        let a_fulls = flat.iter().filter(|entry| entry.full == "a").count();
        assert_eq!(
            a_fulls, 3,
            "/a's leaf, //a's leaf and the root a all read a"
        );
    }

    /// The page assembler over groups walks exactly the flattened tree at
    /// every limit, with implied ancestors and a cursor that is a key.
    #[test]
    fn page_after_walks_the_flattened_tree_at_every_limit() {
        let tags = [
            "a",
            "a/z",
            "a-b",
            "p/q/r",
            "a_b/child",
            "axb/child",
            "/a",
            "//a",
        ];
        let rows: Vec<(String, i64)> = tags
            .iter()
            .enumerate()
            .map(|(i, t)| (t.to_string(), i as i64))
            .collect();
        let expected = flatten_preorder(&build_tag_tree(&rows, 0));
        let groups: Vec<(String, u32)> = tags.iter().map(|t| (t.to_string(), 1)).collect();
        let groups = sort_by_segments(groups, &mut no_cancel).unwrap();
        assert_eq!(
            total_entries(&groups, &mut no_cancel).unwrap() as usize,
            expected.len()
        );
        let nested = |key: &str| -> Result<u32, ()> {
            let below = format!("{key}/");
            Ok(tags
                .iter()
                .filter(|t| **t == key || t.starts_with(&below))
                .count() as u32)
        };
        for limit in 1..=4 {
            let mut walked = Vec::new();
            let mut cursor: Option<String> = None;
            for _ in 0..=expected.len() + 1 {
                let (entries, next) =
                    page_after(&groups, cursor.as_deref(), limit, &mut no_cancel, nested).unwrap();
                assert!(entries.len() <= limit);
                assert!(!entries.is_empty(), "an empty page at limit {limit}");
                walked.extend(entries);
                cursor = next;
                if cursor.is_none() {
                    break;
                }
            }
            assert!(cursor.is_none(), "the walk at limit {limit} never ended");
            assert_eq!(walked, expected, "limit {limit}");
        }
    }

    fn no_cancel() -> Result<(), ()> {
        Ok(())
    }

    /// A check that refuses from its `fail_at`th call on, counting calls.
    fn refusing_at(
        fail_at: usize,
        calls: &std::cell::Cell<usize>,
    ) -> impl FnMut() -> Result<(), ()> {
        move || {
            calls.set(calls.get() + 1);
            if calls.get() > fail_at {
                Err(())
            } else {
                Ok(())
            }
        }
    }

    /// WB-25 (codex round 1): the merge sort is `cmp_segments`'s order, and
    /// a cancel lands between comparisons — every call of the check, from
    /// the first to the last, stops the sort.
    #[test]
    fn sort_by_segments_orders_like_cmp_segments_and_stops_at_any_check() {
        let tags = [
            "p/q",
            "a",
            "a\u{1}z",
            "a/",
            "//a",
            "a//b",
            "a\u{0}b/c",
            "a b",
            "/a",
            "a/z",
            "a\u{0}b",
        ];
        let groups: Vec<(String, u32)> = tags.iter().map(|t| (t.to_string(), 1)).collect();
        let mut expected = groups.clone();
        expected.sort_by(|a, b| cmp_segments(&a.0, &b.0));
        assert_eq!(
            sort_by_segments(groups.clone(), &mut no_cancel),
            Ok(expected)
        );

        let calls = std::cell::Cell::new(0);
        sort_by_segments(groups.clone(), &mut refusing_at(usize::MAX, &calls)).unwrap();
        let comparisons = calls.get();
        assert!(comparisons >= tags.len() - 1, "{comparisons} checks");
        for fail_at in 0..comparisons {
            let calls = std::cell::Cell::new(0);
            let result = sort_by_segments(groups.clone(), &mut refusing_at(fail_at, &calls));
            assert_eq!(result, Err(()), "a refusal at check {fail_at} was ignored");
            assert_eq!(
                calls.get(),
                fail_at + 1,
                "the sort went on past check {fail_at}"
            );
        }
    }

    /// WB-25 (codex round 1): counting the tree and walking a page check once
    /// per tag, so a cancel stops them at the next tag — including the tags
    /// a resumed page skips past.
    #[test]
    fn counting_and_paging_stop_at_the_next_tag_once_refused() {
        let groups: Vec<(String, u32)> = ["a/b", "a/c", "d", "e/f/g"]
            .iter()
            .map(|t| (t.to_string(), 1))
            .collect();
        for fail_at in 0..groups.len() {
            let calls = std::cell::Cell::new(0);
            assert_eq!(
                total_entries(&groups, &mut refusing_at(fail_at, &calls)),
                Err(())
            );
            assert_eq!(calls.get(), fail_at + 1);
            let calls = std::cell::Cell::new(0);
            let page = page_after(
                &groups,
                None,
                100,
                &mut refusing_at(fail_at, &calls),
                |_| Ok(1),
            );
            assert_eq!(page, Err(()), "a refusal at tag {fail_at} was ignored");
        }
        // Resumed after `a/c`, the walk still checks before `d`.
        let calls = std::cell::Cell::new(0);
        let page = page_after(
            &groups,
            Some("a/c"),
            100,
            &mut refusing_at(0, &calls),
            |_| Ok(1),
        );
        assert_eq!(page, Err(()));
    }

    /// WB-25 (codex round 1): one legal tag of 100,001 segments. Counting
    /// it, a one-row page, and a page resumed from deep inside it are each
    /// linear in its bytes — the old walk re-joined every prefix and ran
    /// about ten billion byte operations per page.
    #[test]
    fn a_tag_of_a_hundred_thousand_segments_pages_in_linear_time() {
        let depth = 100_001;
        let deep = vec!["a"; depth].join("/");
        let groups = vec![(deep.clone(), 1)];
        assert_eq!(total_entries(&groups, &mut no_cancel), Ok(depth as u32));

        let (first, next) = page_after(&groups, None, 1, &mut no_cancel, |_| Ok(1)).unwrap();
        assert_eq!(first.len(), 1);
        assert_eq!((first[0].full.as_str(), first[0].depth), ("a", 0));
        assert_eq!(next.as_deref(), Some("a"));

        let cursor = vec!["a"; 50_000].join("/");
        let (resumed, next) =
            page_after(&groups, Some(&cursor), 2, &mut no_cancel, |_| Ok(1)).unwrap();
        assert_eq!(
            resumed.iter().map(|entry| entry.depth).collect::<Vec<_>>(),
            [50_000, 50_001]
        );
        assert_eq!(next.map(|key| key.len()), Some(50_002 * 2 - 1));

        let last_prefix = &deep[..deep.len() - 2];
        let (tail, next) =
            page_after(&groups, Some(last_prefix), 5, &mut no_cancel, |_| Ok(1)).unwrap();
        assert_eq!(tail.len(), 1);
        assert_eq!(tail[0].depth as usize, depth - 1);
        assert_eq!(tail[0].direct_count, 1);
        assert_eq!(next, None);
    }
}
