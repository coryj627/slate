// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Derivation of `oplog_events` rows from op-log entries (O-6 #544).
//!
//! One shared function feeds every producer — the append-time
//! population, the scan-time rebuild, and the census's reference —
//! so "rebuild ≡ append-time" is structural, not aspirational
//! (modulo the spec's pinned snapshot-boundary NULLs, see below).
//!
//! Row rules (o_spec §O-6, normative):
//! * `hash_before == hash_after` (anchors, `PathChanged` markers) →
//!   **no rows** — the "excludes touch-only events" requirement
//!   falling out of the hash rule.
//! * `CanvasApply` → no rows: the semantic record duplicates the
//!   transition its accompanying text save already produced.
//! * A content change → one class-1 row. `deleted_text` = the
//!   concatenated removed spans of the save's edit ops against the
//!   OLD content, capped at 4096 bytes on a char boundary; `''` for
//!   a pure insert (known-nothing-deleted, distinct from unknown).
//! * `NULL` when ops or old content weren't in hand — the documented
//!   sampling gaps: cold-cache saves at append time (the session
//!   computed no diff), and snapshot entries at rebuild time (ops
//!   aren't recorded in the log). The cadence-snapshot save IS
//!   sampled at append time because the session computes
//!   `diff_to_ops` before deciding the entry kind — callers pass
//!   those ops via `ops_in_hand`.
//! * Annotations → one row each: `SetProperty` → class 2 (key),
//!   `RemoveProperty` → class 3 (key), `ToggleTask` → class 4,
//!   `FrontmatterReplace` → class 5. `PathChanged` → no rows.

use std::borrow::Cow;

use crate::oplog::{EditOp, OpAnnotation, OpKind, OpLogEntry, decode_annotated, decode_edit_batch};

pub(crate) const EVENT_CONTENT_CHANGE: u8 = 1;
pub(crate) const EVENT_PROPERTY_SET: u8 = 2;
pub(crate) const EVENT_PROPERTY_REMOVE: u8 = 3;
pub(crate) const EVENT_TASK_TOGGLE: u8 = 4;
pub(crate) const EVENT_FM_REPLACE: u8 = 5;

/// The `deleted_text` sample cap (bytes; truncated on a char
/// boundary). A pattern deleted inside a single larger removal can
/// miss — the cap is a constant, revisited on tester evidence.
pub(crate) const DELETED_TEXT_CAP_BYTES: usize = 4096;

/// One derived `oplog_events` row (sans `file_id` — the caller binds
/// it).
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct DerivedEvent {
    pub ts_ms: i64,
    pub event_class: u8,
    pub property_key: Option<String>,
    pub deleted_text: Option<String>,
}

/// Derive the events of one entry.
///
/// `ops_in_hand` is the save's edit ops when the producer computed
/// them independently of the entry payload — the session's
/// cadence-snapshot case, where the ops informed the kind decision
/// but a snapshot got written. Ignored for `EditBatch` entries (the
/// payload is authoritative and identical). `old_contents` is the
/// document state immediately BEFORE this entry; pass `None` when it
/// isn't known.
pub(crate) fn derive_events(
    entry: &OpLogEntry,
    ops_in_hand: Option<&[EditOp]>,
    old_contents: Option<&str>,
) -> Vec<DerivedEvent> {
    // Touch-only (anchors, pure markers): no rows, regardless of kind
    // or annotations.
    if entry.content_hash_before == entry.content_hash_after {
        return Vec::new();
    }
    // Semantic canvas records duplicate their text save's transition.
    if entry.op_kind == OpKind::CanvasApply {
        return Vec::new();
    }

    let (inner_kind, inner_payload, annotations) = match entry.op_kind {
        OpKind::Annotated => match decode_annotated(&entry.payload_bytes) {
            Ok((kind, payload, anns)) => (kind, Some(payload), anns),
            // An undecodable wrapper still changed content — keep the
            // class-1 row, drop the annotation detail.
            Err(_) => (OpKind::Annotated, None, Vec::new()),
        },
        kind => (kind, Some(entry.payload_bytes.clone()), Vec::new()),
    };

    // Span source: a batch payload is authoritative; otherwise the
    // caller's ops (snapshot entries never record ops themselves).
    let ops: Option<Cow<'_, [EditOp]>> = match (inner_kind, &inner_payload) {
        (OpKind::EditBatch, Some(payload)) => decode_edit_batch(payload).ok().map(Cow::Owned),
        _ => ops_in_hand.map(Cow::Borrowed),
    };
    let deleted_text = match (ops, old_contents) {
        (Some(ops), Some(old)) => Some(removed_spans_capped(&ops, old)),
        _ => None,
    };

    let mut events = vec![DerivedEvent {
        ts_ms: entry.timestamp_ms,
        event_class: EVENT_CONTENT_CHANGE,
        property_key: None,
        deleted_text,
    }];
    for annotation in annotations {
        let (event_class, property_key) = match annotation {
            OpAnnotation::SetProperty { key, .. } => (EVENT_PROPERTY_SET, Some(key)),
            OpAnnotation::RemoveProperty { key } => (EVENT_PROPERTY_REMOVE, Some(key)),
            OpAnnotation::ToggleTask { .. } => (EVENT_TASK_TOGGLE, None),
            OpAnnotation::FrontmatterReplace => (EVENT_FM_REPLACE, None),
            OpAnnotation::PathChanged { .. } => continue,
        };
        events.push(DerivedEvent {
            ts_ms: entry.timestamp_ms,
            event_class,
            property_key,
            deleted_text: None,
        });
    }
    events
}

/// Walk a whole log in order, deriving EVERY entry's events with old
/// content reconstructed incrementally — the scan-time rebuild path.
/// Ops are never "in hand" here: snapshot entries contribute class-1
/// rows with NULL `deleted_text` (the spec's pinned snapshot-boundary
/// difference from append-time population). An unreplayable entry
/// poisons the running document until the next snapshot re-seeds it —
/// rows keep deriving throughout, only the poisoned span's samples
/// degrade to NULL (see the divergence note below).
pub(crate) fn derive_events_for_log(entries: &[OpLogEntry]) -> Vec<DerivedEvent> {
    let mut events = Vec::new();
    // ONE running replay buffer advanced entry by entry — a per-prefix
    // `reconstruct_at_tail` here is O(n²) on a compacted log (one
    // anchor + thousands of batches re-replayed each step; measured in
    // whole seconds per hot log — adversarial round 3). The census
    // cross-validates this walker against per-prefix reconstruction.
    //
    // Divergence handling (round 4): a failed advance POISONS the
    // buffer instead of stopping the walk. Every entry still derives
    // its rows — append-time populated them, and rebuild ≡ append is
    // the contract — but samples in the poisoned span degrade to NULL
    // (old content unknown) until the next snapshot re-seeds. This
    // matches `reconstruct_at_tail`'s semantics, where corruption
    // before the last anchor is inert: a corrupt old batch here costs
    // only that span's samples, never the later history. Span
    // extraction against a captured old is bounds-checked (`str::get`),
    // so a decodable-but-inapplicable batch cannot emit garbage.
    let mut buf: Option<crate::text_buffer::TextBuffer> = None;
    for entry in entries {
        // Materialize old content only for entries that can sample it
        // (a content change); touch-only entries skip the copy.
        let old: Option<String> = if entry.content_hash_before != entry.content_hash_after {
            buf.as_ref().map(std::string::ToString::to_string)
        } else {
            None
        };
        events.extend(derive_events(entry, None, old.as_deref()));
        if crate::oplog::replay_advance(&mut buf, entry).is_err() {
            buf = None; // resync at the next snapshot
        }
    }
    events
}

/// Concatenate the ops' removed spans against `old`, capped at
/// [`DELETED_TEXT_CAP_BYTES`] on a char boundary. Returns `""` for a
/// pure insert. Out-of-range spans (foreign ops) contribute nothing.
fn removed_spans_capped(ops: &[EditOp], old: &str) -> String {
    let mut removed = String::new();
    for op in ops {
        let span = match op {
            EditOp::Delete { start, end } | EditOp::Replace { start, end, .. } => {
                old.get(*start..*end)
            }
            EditOp::Insert { .. } => None,
        };
        if let Some(span) = span {
            removed.push_str(span);
            if removed.len() >= DELETED_TEXT_CAP_BYTES {
                break;
            }
        }
    }
    if removed.len() <= DELETED_TEXT_CAP_BYTES {
        return removed;
    }
    let mut cut = DELETED_TEXT_CAP_BYTES;
    while !removed.is_char_boundary(cut) {
        cut -= 1;
    }
    removed.truncate(cut);
    removed
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::oplog::{encode_annotated, encode_edit_batch};
    use crate::vault::content_hash;

    fn entry(kind: OpKind, hb: &str, ha: &str, payload: Vec<u8>) -> OpLogEntry {
        OpLogEntry {
            timestamp_ms: 42,
            user_actor_id: "t".into(),
            op_kind: kind,
            content_hash_before: content_hash(hb.as_bytes()),
            content_hash_after: content_hash(ha.as_bytes()),
            payload_bytes: payload,
        }
    }

    #[test]
    fn every_class_and_the_exclusions() {
        // Batch with old content in hand: removed span captured.
        let old = "keep DELETE keep\n";
        let new = "keep keep\n";
        let ops = crate::diff::diff_to_ops(old, new);
        let batch = entry(OpKind::EditBatch, old, new, encode_edit_batch(&ops));
        let events = derive_events(&batch, None, Some(old));
        assert_eq!(events.len(), 1);
        assert_eq!(events[0].event_class, EVENT_CONTENT_CHANGE);
        assert!(
            events[0]
                .deleted_text
                .as_deref()
                .unwrap()
                .contains("DELETE"),
            "removed span captured: {:?}",
            events[0].deleted_text
        );

        // No old content: NULL sample even with the ops decodable.
        let events = derive_events(&batch, None, None);
        assert_eq!(events[0].deleted_text, None);

        // Annotated save: one class-1 + one row per annotation, in
        // annotation order; same-ms set+remove of one key → BOTH rows
        // (nothing swallowed).
        let annotated = entry(
            OpKind::Annotated,
            old,
            new,
            encode_annotated(
                OpKind::EditBatch,
                &encode_edit_batch(&ops),
                &[
                    OpAnnotation::SetProperty {
                        key: "status".into(),
                        value_json: "1".into(),
                    },
                    OpAnnotation::RemoveProperty {
                        key: "status".into(),
                    },
                    OpAnnotation::ToggleTask {
                        ordinal: 0,
                        new_status: 'x',
                    },
                    OpAnnotation::FrontmatterReplace,
                ],
            ),
        );
        let events = derive_events(&annotated, None, Some(old));
        let classes: Vec<u8> = events.iter().map(|e| e.event_class).collect();
        assert_eq!(classes, vec![1, 2, 3, 4, 5]);
        assert_eq!(events[1].property_key.as_deref(), Some("status"));
        assert_eq!(events[2].property_key.as_deref(), Some("status"));
        assert!(
            events[0].deleted_text.is_some(),
            "the wrapped batch payload still yields spans"
        );

        // Anchors and markers (hash_before == hash_after): no rows.
        let anchor = entry(OpKind::WholeFileReplace, new, new, new.as_bytes().to_vec());
        assert!(derive_events(&anchor, None, Some(new)).is_empty());
        let marker = entry(
            OpKind::Annotated,
            new,
            new,
            encode_annotated(
                OpKind::EditBatch,
                &encode_edit_batch(&[]),
                &[OpAnnotation::PathChanged {
                    from: "a.md".into(),
                    to: "b.md".into(),
                }],
            ),
        );
        assert!(derive_events(&marker, None, Some(new)).is_empty());

        // Canvas records: no rows.
        let canvas = entry(OpKind::CanvasApply, old, new, b"{}".to_vec());
        assert!(derive_events(&canvas, None, Some(old)).is_empty());
    }

    #[test]
    fn snapshot_sampling_matches_the_spec_matrix() {
        let old = "alpha REMOVED beta\n";
        let new = "alpha beta\n";
        let ops = crate::diff::diff_to_ops(old, new);
        let snapshot = entry(OpKind::WholeFileReplace, old, new, new.as_bytes().to_vec());

        // Cadence-snapshot at append time: ops in hand → sampled.
        let events = derive_events(&snapshot, Some(&ops), Some(old));
        assert_eq!(events.len(), 1);
        assert!(
            events[0]
                .deleted_text
                .as_deref()
                .unwrap()
                .contains("REMOVED")
        );

        // Cold-cache snapshot at append time: no ops → NULL, even
        // with old content available (the spec's cold-cache row).
        let events = derive_events(&snapshot, None, Some(old));
        assert_eq!(events[0].deleted_text, None);

        // Rebuild (via the log walker): snapshot boundary → NULL —
        // the pinned append/rebuild difference.
        let anchor = entry(OpKind::WholeFileReplace, "", old, old.as_bytes().to_vec());
        let walked = derive_events_for_log(&[anchor, snapshot.clone()]);
        assert_eq!(walked.len(), 2);
        assert_eq!(walked[1].deleted_text, None);

        // Pure insert with ops in hand: '' (known-nothing-deleted),
        // NOT NULL.
        let grown = format!("{new}gamma\n");
        let insert_ops = crate::diff::diff_to_ops(new, &grown);
        let batch = entry(
            OpKind::EditBatch,
            new,
            &grown,
            encode_edit_batch(&insert_ops),
        );
        let events = derive_events(&batch, None, Some(new));
        assert_eq!(events[0].deleted_text.as_deref(), Some(""));
    }

    #[test]
    fn cap_truncates_on_a_char_boundary() {
        // Removed span of multibyte chars crossing the cap.
        let old_body = "中".repeat(2000); // 3 bytes each = 6000 bytes
        let old = format!("{old_body}tail\n");
        let new = "tail\n".to_string();
        let ops = crate::diff::diff_to_ops(&old, &new);
        let batch = entry(OpKind::EditBatch, &old, &new, encode_edit_batch(&ops));
        let events = derive_events(&batch, None, Some(&old));
        let sample = events[0].deleted_text.as_deref().unwrap();
        assert!(sample.len() <= DELETED_TEXT_CAP_BYTES);
        assert!(sample.len() > DELETED_TEXT_CAP_BYTES - 4, "cap is tight");
        assert!(sample.chars().all(|c| c == '中'), "boundary-safe");
    }

    #[test]
    fn corruption_before_a_later_anchor_is_inert() {
        // Round 4: `reconstruct_at_tail` ignores corruption before the
        // last snapshot; the walker must too. A corrupt old batch
        // poisons SAMPLES until the next snapshot re-seeds — it must
        // not stop the walk, omit later history, or clear rows that
        // append-time populated.
        let v0 = "alpha beta\n";
        let v2 = "gamma REMOVED delta\n";
        let v3 = "gamma delta\n";
        let corrupt_batch = encode_edit_batch(&[crate::oplog::EditOp::Delete {
            start: 10_000,
            end: 10_005,
        }]);
        let entries = vec![
            entry(OpKind::WholeFileReplace, "", v0, v0.as_bytes().to_vec()),
            // Decodes fine, cannot apply (offsets past the end).
            entry(OpKind::EditBatch, v0, "poisoned", corrupt_batch),
            // A later valid anchor re-seeds the walk.
            entry(
                OpKind::WholeFileReplace,
                "poisoned",
                v2,
                v2.as_bytes().to_vec(),
            ),
            entry(
                OpKind::EditBatch,
                v2,
                v3,
                encode_edit_batch(&crate::diff::diff_to_ops(v2, v3)),
            ),
        ];
        let events = derive_events_for_log(&entries);
        assert_eq!(events.len(), 4, "every content change emits its row");
        // The corrupt entry's extraction is bounds-checked: out-of-
        // range spans contribute nothing, never garbage.
        assert_eq!(events[1].deleted_text.as_deref(), Some(""));
        // The post-resync batch samples fully — the later history is
        // NOT lost to the old corruption.
        assert!(
            events[3]
                .deleted_text
                .as_deref()
                .unwrap()
                .contains("REMOVED"),
            "resynced span: {:?}",
            events[3].deleted_text
        );
    }

    #[test]
    fn corruption_poisons_samples_until_resync_not_the_walk() {
        // No later anchor: rows keep deriving after the corruption,
        // but their samples are NULL — the running document is
        // unknown, and honesty beats guessing.
        let v0 = "keep WIPED keep\n";
        let v1 = "keep keep\n";
        let corrupt_batch = encode_edit_batch(&[crate::oplog::EditOp::Delete {
            start: 10_000,
            end: 10_005,
        }]);
        let entries = vec![
            entry(OpKind::WholeFileReplace, "", v0, v0.as_bytes().to_vec()),
            entry(
                OpKind::EditBatch,
                v0,
                v1,
                encode_edit_batch(&crate::diff::diff_to_ops(v0, v1)),
            ),
            entry(OpKind::EditBatch, v1, "poisoned", corrupt_batch),
            entry(
                OpKind::EditBatch,
                "poisoned",
                "later",
                encode_edit_batch(&crate::diff::diff_to_ops(v1, v0)),
            ),
        ];
        let events = derive_events_for_log(&entries);
        assert_eq!(events.len(), 4, "the walk continues past the corruption");
        assert!(events[1].deleted_text.as_deref().unwrap().contains("WIPED"));
        assert_eq!(
            events[3].deleted_text, None,
            "poisoned span degrades to NULL, never a wrong sample"
        );
    }

    #[test]
    fn log_walk_reconstructs_old_content_for_batch_spans() {
        // Rebuild path: the walker supplies old content incrementally,
        // so batch entries get real samples even without session state.
        let v0 = "alpha REMOVED beta\n";
        let v1 = "alpha beta\n";
        // A whole added LINE: the line-based differ emits a pure
        // insert (an intra-line edit would be a Replace whose removed
        // span is the old line — also correct, just not this case).
        let v2 = "alpha beta\ngamma\n";
        let entries = vec![
            entry(OpKind::WholeFileReplace, "", v0, v0.as_bytes().to_vec()),
            entry(
                OpKind::EditBatch,
                v0,
                v1,
                encode_edit_batch(&crate::diff::diff_to_ops(v0, v1)),
            ),
            entry(
                OpKind::EditBatch,
                v1,
                v2,
                encode_edit_batch(&crate::diff::diff_to_ops(v1, v2)),
            ),
        ];
        let events = derive_events_for_log(&entries);
        // Anchor row (NULL sample — nothing before it) + two batches.
        assert_eq!(events.len(), 3);
        assert_eq!(events[0].deleted_text, None);
        assert!(
            events[1]
                .deleted_text
                .as_deref()
                .unwrap()
                .contains("REMOVED"),
            "the walker reconstructed v0 for the span: {:?}",
            events[1].deleted_text
        );
        assert_eq!(events[2].deleted_text.as_deref(), Some(""));
    }
}
