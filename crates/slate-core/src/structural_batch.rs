// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! One-request structural Move and Trash contracts (FL2-2a).

use std::cmp::Ordering;
use std::collections::BTreeMap;

pub const MAX_STRUCTURAL_BATCH_ITEMS: usize = 10_000;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct StructuralBatchItem {
    pub path: String,
    pub is_directory: bool,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BatchMoveRequest {
    pub items: Vec<StructuralBatchItem>,
    pub new_parent: String,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BatchTrashRequest {
    pub items: Vec<StructuralBatchItem>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BatchPathChange {
    pub old_path: String,
    pub new_path: String,
    pub is_directory: bool,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BatchSkippedItem {
    pub item: StructuralBatchItem,
    pub reason: BatchSkipReason,
    pub detail: String,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord)]
pub enum BatchSkipReason {
    Duplicate,
    CoveredBySelectedFolder,
    AlreadyInDestination,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BatchItemFailure {
    /// `None` for request-wide failures which have no honest path/kind.
    pub item: Option<StructuralBatchItem>,
    pub stage: BatchFailureStage,
    pub message: String,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BatchFailureStage {
    Preflight,
    Move,
    Index,
    LinkRewrite,
    LinkRewriteRestore,
    Journal,
    Rollback,
    Trash,
    Reconciliation,
    RecoveryBarrier,
}

#[derive(Debug, Clone, PartialEq, Eq, Default)]
pub struct StructuralBatchEnvelope {
    pub planned: Vec<StructuralBatchItem>,
    pub skipped: Vec<BatchSkippedItem>,
    pub preflight_failures: Vec<BatchItemFailure>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BatchMoveState {
    Rejected,
    NoOp,
    Succeeded,
    RolledBack,
    RollbackIncomplete,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BatchMoveReport {
    pub envelope: StructuralBatchEnvelope,
    pub state: BatchMoveState,
    pub op_id: Option<i64>,
    pub standing: Vec<BatchPathChange>,
    pub rolled_back: Vec<BatchPathChange>,
    pub failure: Option<BatchItemFailure>,
    pub rollback_failures: Vec<BatchItemFailure>,
    pub rewritten: Vec<crate::structural::RewriteOutcome>,
    pub rewrite_failures: Vec<crate::structural::RewriteFailure>,
    pub requires_rescan: bool,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BatchTrashState {
    Rejected,
    NoOp,
    Succeeded,
    Partial,
    Failed,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BatchTrashRemainder {
    pub item: StructuralBatchItem,
    pub failure: BatchItemFailure,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BatchTrashReport {
    pub envelope: StructuralBatchEnvelope,
    pub state: BatchTrashState,
    pub op_id: Option<i64>,
    pub trashed: Vec<StructuralBatchItem>,
    pub untrashed: Vec<BatchTrashRemainder>,
    pub bookkeeping_failures: Vec<BatchItemFailure>,
    pub requires_rescan: bool,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct NormalizedBatchItems {
    pub items: Vec<StructuralBatchItem>,
    pub skipped: Vec<BatchSkippedItem>,
    pub failures: Vec<BatchItemFailure>,
}

#[derive(Debug, Clone)]
pub(crate) struct PlannedBatchMove {
    pub item: StructuralBatchItem,
    pub destination: String,
    pub moved_files: Vec<(String, String)>,
}

impl PlannedBatchMove {
    pub(crate) fn change(&self) -> BatchPathChange {
        BatchPathChange {
            old_path: self.item.path.clone(),
            new_path: self.destination.clone(),
            is_directory: self.item.is_directory,
        }
    }
}

#[derive(Debug, Clone)]
pub(crate) struct PlannedBatchTrash {
    pub item: StructuralBatchItem,
    pub deleted_files: Vec<String>,
    pub oplog_bindings: Vec<crate::structural::DeletedOplogBinding>,
}

pub(crate) fn deterministic_item_cmp(
    left: &StructuralBatchItem,
    right: &StructuralBatchItem,
) -> Ordering {
    left.path
        .cmp(&right.path)
        .then_with(|| left.is_directory.cmp(&right.is_directory))
}

/// Sort, deduplicate, reject contradictory kind hints, and remove descendants
/// covered by a selected directory. Raw byte ordering can place siblings such
/// as `a.md` before `a/child.md`, so the pruning pass retains every selected
/// directory and checks only the candidate's component-boundary ancestors.
/// Complexity is O(K log K + C log K), where C is the total component count.
pub(crate) fn normalize_batch_items(items: Vec<StructuralBatchItem>) -> NormalizedBatchItems {
    let mut by_path: BTreeMap<String, (usize, usize)> = BTreeMap::new();
    for item in items {
        let counts = by_path.entry(item.path).or_default();
        if item.is_directory {
            counts.1 += 1;
        } else {
            counts.0 += 1;
        }
    }

    let mut unique = Vec::new();
    let mut skipped = Vec::new();
    let mut failures = Vec::new();
    for (path, (file_count, directory_count)) in by_path {
        if file_count > 0 && directory_count > 0 {
            failures.push(BatchItemFailure {
                item: Some(StructuralBatchItem {
                    path,
                    is_directory: false,
                }),
                stage: BatchFailureStage::Preflight,
                message: "the same path was supplied with conflicting kind hints".into(),
            });
            continue;
        }
        let (count, is_directory) = if directory_count > 0 {
            (directory_count, true)
        } else {
            (file_count, false)
        };
        let item = StructuralBatchItem { path, is_directory };
        unique.push(item.clone());
        for _ in 1..count {
            skipped.push(BatchSkippedItem {
                item: item.clone(),
                reason: BatchSkipReason::Duplicate,
                detail: "duplicate selection".into(),
            });
        }
    }
    unique.sort_by(deterministic_item_cmp);

    let mut top_level = Vec::with_capacity(unique.len());
    let mut selected_directories = std::collections::BTreeSet::new();
    for item in unique {
        let covered = item
            .path
            .match_indices('/')
            .any(|(boundary, _)| selected_directories.contains(&item.path[..boundary]));
        if covered {
            skipped.push(BatchSkippedItem {
                item,
                reason: BatchSkipReason::CoveredBySelectedFolder,
                detail: "covered by a selected folder".into(),
            });
            continue;
        }
        if item.is_directory {
            selected_directories.insert(item.path.clone());
        }
        top_level.push(item);
    }
    skipped.sort_by(|left, right| {
        deterministic_item_cmp(&left.item, &right.item).then_with(|| left.reason.cmp(&right.reason))
    });
    NormalizedBatchItems {
        items: top_level,
        skipped,
        failures,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn item(path: &str, is_directory: bool) -> StructuralBatchItem {
        StructuralBatchItem {
            path: path.to_string(),
            is_directory,
        }
    }

    #[test]
    fn prune_top_level_is_component_safe_deduplicated_and_deterministic() {
        let normalized = normalize_batch_items(vec![
            item("z.md", false),
            item("a/child.md", false),
            item("ab/kept.md", false),
            item("a", true),
            item("z.md", false),
            item("a/nested", true),
        ]);

        assert_eq!(normalized.failures, Vec::new());
        assert_eq!(
            normalized.items,
            vec![
                item("a", true),
                item("ab/kept.md", false),
                item("z.md", false)
            ]
        );
        assert_eq!(
            normalized
                .skipped
                .iter()
                .map(|skip| (&skip.item, skip.reason))
                .collect::<Vec<_>>(),
            vec![
                (
                    &item("a/child.md", false),
                    BatchSkipReason::CoveredBySelectedFolder
                ),
                (
                    &item("a/nested", true),
                    BatchSkipReason::CoveredBySelectedFolder
                ),
                (&item("z.md", false), BatchSkipReason::Duplicate),
            ]
        );
    }

    #[test]
    fn prune_keeps_folder_coverage_across_raw_order_siblings() {
        let normalized = normalize_batch_items(vec![
            item("a/child.md", false),
            item("a.md", false),
            item("a", true),
            item("a-foo.md", false),
        ]);

        assert_eq!(
            normalized.items,
            vec![
                item("a", true),
                item("a-foo.md", false),
                item("a.md", false)
            ]
        );
        assert_eq!(normalized.skipped.len(), 1);
        assert_eq!(normalized.skipped[0].item, item("a/child.md", false));
        assert_eq!(
            normalized.skipped[0].reason,
            BatchSkipReason::CoveredBySelectedFolder
        );
    }

    #[test]
    fn permuted_input_produces_identical_plan_and_result_order() {
        let first = normalize_batch_items(vec![
            item("b.md", false),
            item("folder/child.md", false),
            item("folder", true),
            item("a.md", false),
        ]);
        let second = normalize_batch_items(vec![
            item("a.md", false),
            item("folder", true),
            item("b.md", false),
            item("folder/child.md", false),
        ]);

        assert_eq!(first, second);
    }

    #[test]
    fn conflicting_duplicate_kind_is_preflight_failure() {
        let normalized = normalize_batch_items(vec![item("same", false), item("same", true)]);

        assert!(normalized.items.is_empty());
        assert_eq!(normalized.failures.len(), 1);
        assert_eq!(normalized.failures[0].item, Some(item("same", false)));
        assert_eq!(normalized.failures[0].stage, BatchFailureStage::Preflight);
        assert!(normalized.failures[0].message.contains("conflicting kind"));
    }
}
