// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Op-log compaction + retention execution (O-2 #540).
//!
//! # Fold semantics (normative — positional, never timestamp-filtered)
//!
//! Wall-clock timestamps are not monotonic across entries (clock steps,
//! NTP); filtering by timestamp over an append-ordered log can
//! double-apply or orphan entries. Compaction is therefore always a
//! **prefix fold at a position boundary P**: entries `[0..=P]` are
//! replaced by one synthesized anchor; entries `[P+1..]` are retained
//! **verbatim, by position**. The replay chain stays valid by
//! construction: the anchor's content is `reconstruct(entries[0..=P])`
//! and its `hash_after` is entry P's `hash_after` (`hash_before ==
//! hash_after`, timestamp = entry P's timestamp). Annotations and
//! semantic records on folded entries are discarded — they described
//! history that no longer exists at save-point granularity.
//!
//! Choosing P — two triggers, evaluated when compaction runs:
//!
//! * **Retention fold**: `P_ret` = the largest index `i` with
//!   `entries[i].timestamp_ms ≤ cutoff` (`cutoff = now − retention`).
//!   Positionally-earlier entries with later timestamps (clock
//!   weirdness) fold too — they are positionally older, which is the
//!   order that matters.
//! * **Size fold** (fires when the file length exceeds
//!   `oplog_compaction_threshold_bytes` OR the entry count exceeds
//!   `oplog_compaction_threshold_entries`): start by retaining
//!   `K = min(len − 1, threshold_entries / 2)` tail entries, then
//!   retain fewer until the retained entries' encoded size fits in
//!   `threshold_bytes / 2` or only the tail entry remains. This
//!   deliberately folds save-points *within* retention for
//!   pathologically hot files — sanctioned by `05` §7.5.
//!
//! `P = max(P_ret, P_size)` — fold the most that any trigger demands.
//! Nothing to fold (or a fold that cannot shrink the log — a single
//! oversized snapshot payload) reports [`CompactionOutcome::Futile`];
//! the session records the futility in its append state so the log is
//! not re-enqueued until it grows again — **no livelock**. Idempotence
//! follows: a second run over the folded log computes the same P and
//! finds the fold is the identity, reported as
//! [`CompactionOutcome::AlreadyCompact`] with the file untouched (the
//! mtime-unchanged test pins this).
//!
//! # The rewrite
//!
//! Under the per-log **sidecar** mutation lock (`lock_oplog`, shared
//! with `append_entry` — #928: the log file itself is never OS-locked,
//! because `File::lock` is mandatory on Windows and would fail
//! lock-free readers): new file = v2 header (path = the file's current
//! vault path, **generation + 1** — this is where v1 logs upgrade and
//! stale header paths heal) + anchor + retained entries, written to
//! `<stem>.oplog.tmp`, `sync_data`, renamed over the log (the source
//! handle is closed before the rename — Windows refuses to replace a
//! file the process holds open), `fsync_dir`, release. A torn trailing
//! entry in the source log is healed by the rewrite (retained entries
//! are re-framed from the parsed clean prefix).
//!
//! The anchor's reconstruction is **integrity-verified** before
//! anything is written: its bytes must hash to entry P's `hash_after`,
//! else the log is corrupt and compaction refuses loudly — wrong bytes
//! are never anchored.

use std::fs::{self, OpenOptions};
use std::io::{self, Write};
use std::path::Path;

use crate::oplog::{
    OpLogEntry, frame_entry, fsync_dir, oplog_dir, oplog_path_for_name, read_entries_stream,
    read_header, v2_header_block,
};
use crate::vault::content_hash;

/// Thresholds + retention for one compaction run (a snapshot of the
/// session config; retention is runtime-mutable via the session's
/// atomic so O-5's settings change applies to the next run).
#[derive(Debug, Clone, Copy)]
pub struct CompactionLimits {
    pub threshold_bytes: u64,
    pub threshold_entries: u64,
    pub retention_days: u32,
}

/// What a compaction run did.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum CompactionOutcome {
    /// The log was rewritten: `[0..=p]` folded into an anchor.
    Rewritten { folded: usize, post_len: u64 },
    /// Neither trigger demands a fold, or the fold cannot shrink the
    /// log (single oversized entry). The caller marks the log futile
    /// until its next append.
    Futile,
    /// The fold is the identity (already anchor + tail) — nothing
    /// written, mtime untouched.
    AlreadyCompact,
    /// The log vanished before the lock was acquired (deleted or
    /// never created) — nothing to do.
    Missing,
}

/// Test-only fault hook: forces the next rewrite to fail with an
/// injected IO error (the error-dispatch test drives the
/// `VaultEventListener` channel through this).
#[cfg(test)]
pub(crate) static FAIL_NEXT_COMPACTION_FOR: std::sync::Mutex<Option<std::path::PathBuf>> =
    std::sync::Mutex::new(None);

/// Test-only stall hook: hold the per-log lock for this many
/// milliseconds before rewriting (the editor-blocking assertion drives
/// an append against a mid-flight compaction through this).
#[cfg(test)]
pub(crate) static HOLD_LOCK_FOR: std::sync::Mutex<Option<(std::path::PathBuf, u64)>> =
    std::sync::Mutex::new(None);

const MS_PER_DAY: i64 = 24 * 60 * 60 * 1000;

/// The fold boundary `P = max(P_ret, P_size)`, or `None` when no
/// trigger demands a fold. Pure arithmetic over the parsed entries —
/// separately testable from the rewrite.
pub(crate) fn fold_boundary(
    entries: &[OpLogEntry],
    file_len: u64,
    limits: &CompactionLimits,
    now_ms: i64,
) -> Option<usize> {
    if entries.is_empty() {
        return None;
    }
    let cutoff = now_ms - i64::from(limits.retention_days) * MS_PER_DAY;
    let p_ret: Option<usize> = entries.iter().rposition(|e| e.timestamp_ms <= cutoff);

    let size_triggered =
        file_len > limits.threshold_bytes || entries.len() as u64 > limits.threshold_entries;
    let p_size: Option<usize> = if size_triggered {
        // Retain at most threshold_entries/2 tail entries…
        let mut retained = ((limits.threshold_entries / 2) as usize).min(entries.len() - 1);
        // …then fewer, until they fit in threshold_bytes/2 or only the
        // tail entry remains. One arithmetic pass (Codoki PR #791):
        // the running suffix sum uses exact frame sizes computed
        // without allocating or hashing, so the shrink walk is O(K),
        // not O(K²) frame builds.
        let byte_budget = limits.threshold_bytes / 2;
        let mut retained_bytes: u64 = entries[entries.len() - retained..]
            .iter()
            .map(crate::oplog::frame_size)
            .sum();
        while retained_bytes > byte_budget && retained > 1 {
            retained_bytes -= crate::oplog::frame_size(&entries[entries.len() - retained]);
            retained -= 1;
        }
        Some(entries.len() - 1 - retained)
    } else {
        None
    };

    match (p_ret, p_size) {
        (None, None) => None,
        (a, b) => Some(a.unwrap_or(0).max(b.unwrap_or(0))),
    }
}

/// Synthesize the fold of `entries[0..=p]`: the anchor entry, verified.
///
/// Errors when the prefix doesn't reconstruct or its bytes don't hash
/// to entry `p`'s `hash_after` — corruption; the caller surfaces it and
/// nothing is written.
fn synthesize_anchor(entries: &[OpLogEntry], p: usize) -> io::Result<OpLogEntry> {
    let content = crate::oplog::reconstruct_at_tail(&entries[..=p]).map_err(|e| {
        io::Error::new(
            io::ErrorKind::InvalidData,
            format!("fold prefix does not reconstruct: {e}"),
        )
    })?;
    let expected = &entries[p].content_hash_after;
    let actual = content_hash(content.as_bytes());
    if &actual != expected {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "fold prefix reconstruction failed integrity verification",
        ));
    }
    Ok(OpLogEntry {
        timestamp_ms: entries[p].timestamp_ms,
        user_actor_id: entries[p].user_actor_id.clone(),
        op_kind: crate::oplog::OpKind::WholeFileReplace,
        content_hash_before: expected.clone(),
        content_hash_after: expected.clone(),
        payload_bytes: content.into_bytes(),
    })
}

/// Run one compaction over `<cache_dir>/oplog/<log_name>.oplog`.
///
/// `current_path` is the file's current vault-relative path (written
/// into the rewritten v2 header — where stale creation paths heal).
/// The whole read-fold-rewrite runs under the per-log **sidecar**
/// mutation lock (`lock_oplog`, #928), so appenders, marker writers,
/// the event-regeneration read, and other compactors serialize with
/// it; an appender blocked on the lock opens the log only AFTER the
/// rename-over lands, so it addresses the new file by construction —
/// no inode verification needed, on any platform.
pub fn compact_log(
    cache_dir: &Path,
    log_name: &str,
    current_path: &str,
    limits: &CompactionLimits,
    now_ms: i64,
) -> io::Result<CompactionOutcome> {
    let path = oplog_path_for_name(cache_dir, log_name);
    let lock = crate::oplog::lock_oplog(&path)?;
    compact_log_with_lock(cache_dir, log_name, current_path, limits, now_ms, &lock)
}

/// Compact with the caller's per-log mutation guard already held.
///
/// The background worker acquires this guard before its durable staleness
/// marker and retains it through post-rewrite event regeneration. This closes
/// the window where a scan rebuild could clear a marker against the old log
/// before the worker acquired the lock and published the rewrite.
pub(crate) fn compact_log_with_lock(
    cache_dir: &Path,
    log_name: &str,
    current_path: &str,
    limits: &CompactionLimits,
    now_ms: i64,
    _lock: &crate::oplog::OplogLock,
) -> io::Result<CompactionOutcome> {
    let path = oplog_path_for_name(cache_dir, log_name);
    // Read-only: the rewrite goes through the tmp file, never this
    // handle. A missing log is a no-op run.
    let mut file = match OpenOptions::new().read(true).open(&path) {
        Ok(f) => f,
        Err(e) if e.kind() == io::ErrorKind::NotFound => {
            return Ok(CompactionOutcome::Missing);
        }
        Err(e) => return Err(e),
    };

    #[cfg(test)]
    {
        let hold = HOLD_LOCK_FOR
            .lock()
            .unwrap()
            .as_ref()
            .filter(|(dir, _)| dir == cache_dir)
            .map(|(_, ms)| *ms);
        if let Some(ms) = hold {
            std::thread::sleep(std::time::Duration::from_millis(ms));
        }
    }

    let file_len = file.metadata()?.len();
    if file_len == 0 {
        return Ok(CompactionOutcome::Futile); // header-less empty file
    }
    let header = match read_header(&mut file, &path)? {
        Some(h) => h,
        None => return Ok(CompactionOutcome::Futile),
    };
    let entries = read_entries_stream(&mut file, log_name, &path)?;
    // Everything below works from the parsed entries; close the source
    // handle NOW so the rename-over further down never has to replace
    // a file this process still holds open (a sharing-violation hazard
    // on Windows, #928 — the sidecar lock is what keeps the log stable
    // in between, not this handle).
    drop(file);
    if entries.is_empty() {
        return Ok(CompactionOutcome::Futile);
    }

    let Some(p) = fold_boundary(&entries, file_len, limits, now_ms) else {
        return Ok(CompactionOutcome::Futile);
    };

    let anchor = synthesize_anchor(&entries, p)?;
    let retained = &entries[p + 1..];

    // Identity fold: the log is already `[anchor, tail…]` in exactly
    // the shape we'd write. Skip the rewrite so repeated runs are
    // byte- and mtime-stable (the idempotence pin), and report
    // AlreadyCompact — the caller treats it like Futile for
    // re-enqueue purposes.
    if p == 0 && entries[0] == anchor && header.version == 2 {
        return Ok(CompactionOutcome::AlreadyCompact);
    }

    // Tail preservation is structural: the last retained entry (or the
    // anchor, when everything folded) carries the original tail hash.
    let original_tail = &entries[entries.len() - 1].content_hash_after;
    let new_tail = retained
        .last()
        .map(|e| &e.content_hash_after)
        .unwrap_or(&anchor.content_hash_after);
    debug_assert_eq!(original_tail, new_tail);
    if original_tail != new_tail {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "compaction would change the log tail — refusing",
        ));
    }

    #[cfg(test)]
    {
        let mut fail_for = FAIL_NEXT_COMPACTION_FOR.lock().unwrap();
        if fail_for.as_deref() == Some(cache_dir) {
            *fail_for = None;
            return Err(io::Error::other("injected compaction fault (test hook)"));
        }
    }

    // Rewrite: tmp + rename-over, all while holding the sidecar lock.
    let tmp_path = oplog_dir(cache_dir).join(format!("{log_name}.oplog.tmp"));
    let mut out = Vec::with_capacity(file_len as usize / 2);
    out.extend_from_slice(&v2_header_block(
        current_path,
        header.generation.wrapping_add(1),
    ));
    out.extend_from_slice(&frame_entry(&anchor));
    for entry in retained {
        out.extend_from_slice(&frame_entry(entry));
    }
    let post_len = out.len() as u64;
    let write_result = (|| -> io::Result<()> {
        let mut tmp = fs::File::create(&tmp_path)?;
        tmp.write_all(&out)?;
        tmp.sync_data()?;
        fs::rename(&tmp_path, &path)?;
        let _ = fsync_dir(&oplog_dir(cache_dir));
        Ok(())
    })();
    if let Err(e) = write_result {
        let _ = fs::remove_file(&tmp_path); // best-effort cleanup
        return Err(e);
    }

    Ok(CompactionOutcome::Rewritten {
        folded: p + 1,
        post_len,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::oplog::{
        OpAnnotation, OpKind, append_entry, encode_annotated, encode_edit_batch,
        oplog_path_for_name, read_oplog, read_oplog_with_header, reconstruct_at_tail,
        try_create_log, write_v1_log_for_tests,
    };

    const DAY_MS: i64 = 24 * 60 * 60 * 1000;

    fn snapshot(ts: i64, content: &str) -> OpLogEntry {
        OpLogEntry {
            timestamp_ms: ts,
            user_actor_id: "t".into(),
            op_kind: OpKind::WholeFileReplace,
            content_hash_before: String::new(),
            content_hash_after: content_hash(content.as_bytes()),
            payload_bytes: content.as_bytes().to_vec(),
        }
    }

    fn batch(ts: i64, old: &str, new: &str) -> OpLogEntry {
        OpLogEntry {
            timestamp_ms: ts,
            user_actor_id: "t".into(),
            op_kind: OpKind::EditBatch,
            content_hash_before: content_hash(old.as_bytes()),
            content_hash_after: content_hash(new.as_bytes()),
            payload_bytes: encode_edit_batch(&crate::diff::diff_to_ops(old, new)),
        }
    }

    fn limits(bytes: u64, entries: u64, days: u32) -> CompactionLimits {
        CompactionLimits {
            threshold_bytes: bytes,
            threshold_entries: entries,
            retention_days: days,
        }
    }

    /// Chain of contents "v0".."vN" with 1-day-apart timestamps
    /// starting at day 1.
    fn chain(n: usize) -> (Vec<OpLogEntry>, Vec<String>) {
        let contents: Vec<String> = (0..=n).map(|i| format!("line {i}\ncontent\n")).collect();
        let mut entries = vec![snapshot(DAY_MS, &contents[0])];
        for i in 1..=n {
            entries.push(batch(
                (i as i64 + 1) * DAY_MS,
                &contents[i - 1],
                &contents[i],
            ));
        }
        (entries, contents)
    }

    // --- fold_boundary arithmetic ------------------------------------

    #[test]
    fn retention_fold_picks_last_position_at_or_before_cutoff() {
        let (entries, _) = chain(5); // days 1..=6
        // now = day 10, retention 6 days → cutoff = day 4 → entries at
        // days 1..=4 fold (indices 0..=3).
        let p = fold_boundary(&entries, 100, &limits(u64::MAX, u64::MAX, 6), 10 * DAY_MS);
        assert_eq!(p, Some(3));
        // Everything within retention → no fold.
        let p = fold_boundary(&entries, 100, &limits(u64::MAX, u64::MAX, 30), 10 * DAY_MS);
        assert_eq!(p, None);
    }

    #[test]
    fn retention_fold_is_positional_over_backwards_clocks() {
        // Entry 2 has a LATER timestamp than entry 3 (clock step).
        // If entry 3 is at/before the cutoff, positionally-earlier
        // entry 2 folds too — position, not timestamp, is the order.
        let c: Vec<String> = (0..5).map(|i| format!("v{i}\n")).collect();
        let entries = vec![
            snapshot(DAY_MS, &c[0]),
            batch(2 * DAY_MS, &c[0], &c[1]),
            batch(9 * DAY_MS, &c[1], &c[2]), // clock jumped forward
            batch(3 * DAY_MS, &c[2], &c[3]), // and back
            batch(20 * DAY_MS, &c[3], &c[4]),
        ];
        // cutoff = day 4: rposition(ts <= day4) = index 3.
        let p = fold_boundary(&entries, 100, &limits(u64::MAX, u64::MAX, 6), 10 * DAY_MS);
        assert_eq!(
            p,
            Some(3),
            "the fold is positional: index 2 folds despite ts=day9"
        );
    }

    #[test]
    fn size_fold_retains_half_the_entry_budget_then_shrinks_to_byte_budget() {
        let (entries, _) = chain(9); // 10 entries
        // Entry-count trigger: threshold 6 → K = 3 → P = 10-1-3 = 6.
        let p = fold_boundary(&entries, 100, &limits(u64::MAX, 6, u32::MAX), 0);
        assert_eq!(p, Some(6));
        // Byte trigger with a tiny byte budget: retained shrinks to 1.
        let p = fold_boundary(&entries, 10_000, &limits(10, 6, u32::MAX), 0);
        assert_eq!(p, Some(entries.len() - 2), "retain only the tail entry");
    }

    #[test]
    fn no_trigger_means_no_fold() {
        let (entries, _) = chain(3);
        assert_eq!(
            fold_boundary(&entries, 10, &limits(u64::MAX, u64::MAX, u32::MAX), 0),
            None
        );
    }

    // --- compact_log end to end ---------------------------------------

    fn write_log(cache: &std::path::Path, stem: &str, entries: &[OpLogEntry]) {
        assert!(try_create_log(cache, stem, "notes/n.md").unwrap());
        for e in entries {
            append_entry(cache, stem, "notes/n.md", e).unwrap();
        }
    }

    #[test]
    fn retention_compaction_folds_prefix_and_preserves_tail() {
        let tmp = tempfile::tempdir().unwrap();
        let (entries, contents) = chain(5);
        write_log(tmp.path(), "log", &entries);

        let outcome = compact_log(
            tmp.path(),
            "log",
            "notes/current.md",
            &limits(u64::MAX, u64::MAX, 6),
            10 * DAY_MS,
        )
        .unwrap();
        assert!(matches!(
            outcome,
            CompactionOutcome::Rewritten { folded: 4, .. }
        ));

        let (header, folded) = read_oplog_with_header(tmp.path(), "log").unwrap();
        assert_eq!(header.version, 2);
        assert_eq!(header.generation, 1, "rewrite bumps the generation");
        assert_eq!(
            header.created_path.as_deref(),
            Some("notes/current.md"),
            "the rewrite heals the header path to the file's current path"
        );
        // Anchor: content of entry 3, its hash and timestamp.
        assert_eq!(folded[0].op_kind, OpKind::WholeFileReplace);
        assert_eq!(folded[0].payload_bytes, contents[3].as_bytes());
        assert_eq!(folded[0].content_hash_before, folded[0].content_hash_after);
        assert_eq!(folded[0].content_hash_after, entries[3].content_hash_after);
        assert_eq!(folded[0].timestamp_ms, entries[3].timestamp_ms);
        // Retained tail verbatim.
        assert_eq!(&folded[1..], &entries[4..]);
        // Reconstruction unchanged.
        assert_eq!(reconstruct_at_tail(&folded).unwrap(), contents[5]);
    }

    #[test]
    fn second_run_is_identity_and_leaves_mtime_untouched() {
        let tmp = tempfile::tempdir().unwrap();
        let (entries, _) = chain(5);
        write_log(tmp.path(), "log", &entries);
        let lim = limits(u64::MAX, u64::MAX, 6);
        let now = 10 * DAY_MS;
        assert!(matches!(
            compact_log(tmp.path(), "log", "n.md", &lim, now).unwrap(),
            CompactionOutcome::Rewritten { .. }
        ));

        let path = oplog_path_for_name(tmp.path(), "log");
        let mtime_before = std::fs::metadata(&path).unwrap().modified().unwrap();
        let bytes_before = std::fs::read(&path).unwrap();
        assert_eq!(
            compact_log(tmp.path(), "log", "n.md", &lim, now).unwrap(),
            CompactionOutcome::AlreadyCompact
        );
        assert_eq!(
            std::fs::metadata(&path).unwrap().modified().unwrap(),
            mtime_before
        );
        assert_eq!(std::fs::read(&path).unwrap(), bytes_before);
    }

    #[test]
    fn v1_log_upgrades_to_v2_on_rewrite() {
        let tmp = tempfile::tempdir().unwrap();
        let (entries, contents) = chain(4);
        write_v1_log_for_tests(tmp.path(), "42", &entries);

        let outcome = compact_log(
            tmp.path(),
            "42",
            "renamed/current.md",
            &limits(u64::MAX, u64::MAX, 6),
            10 * DAY_MS,
        )
        .unwrap();
        assert!(matches!(outcome, CompactionOutcome::Rewritten { .. }));
        let (header, folded) = read_oplog_with_header(tmp.path(), "42").unwrap();
        assert_eq!(header.version, 2, "compaction is where v1 logs upgrade");
        assert_eq!(header.created_path.as_deref(), Some("renamed/current.md"));
        assert_eq!(header.generation, 1);
        assert_eq!(reconstruct_at_tail(&folded).unwrap(), contents[4]);
    }

    #[test]
    fn futile_when_single_oversized_entry_and_when_nothing_to_fold() {
        let tmp = tempfile::tempdir().unwrap();
        // One giant snapshot beyond the byte threshold: nothing can
        // shrink → Futile (no rewrite, no livelock).
        let big = "x".repeat(4096);
        write_log(tmp.path(), "giant", &[snapshot(DAY_MS, &big)]);
        let lim = limits(1024, u64::MAX, u32::MAX);
        // First run may canonicalize the lone snapshot into anchor
        // form (hash_before == hash_after) — a bounded, one-time
        // rewrite. The SECOND run must be the identity: that is the
        // no-livelock guarantee for a log that cannot shrink below its
        // trigger.
        let first = compact_log(tmp.path(), "giant", "n.md", &lim, 10 * DAY_MS).unwrap();
        assert!(
            matches!(
                first,
                CompactionOutcome::Rewritten { .. }
                    | CompactionOutcome::Futile
                    | CompactionOutcome::AlreadyCompact
            ),
            "got {first:?}"
        );
        let second = compact_log(tmp.path(), "giant", "n.md", &lim, 10 * DAY_MS).unwrap();
        assert!(
            matches!(
                second,
                CompactionOutcome::Futile | CompactionOutcome::AlreadyCompact
            ),
            "second run must not rewrite again, got {second:?}"
        );

        // No triggers at all → Futile.
        write_log(tmp.path(), "small", &[snapshot(DAY_MS, "tiny\n")]);
        assert_eq!(
            compact_log(
                tmp.path(),
                "small",
                "n.md",
                &limits(u64::MAX, u64::MAX, u32::MAX),
                10 * DAY_MS
            )
            .unwrap(),
            CompactionOutcome::Futile
        );
    }

    #[test]
    fn annotations_and_canvas_records_fold_away_cleanly() {
        let tmp = tempfile::tempdir().unwrap();
        let c0 = "base\n";
        let c1 = "base\nmore\n";
        let c2 = "base\nmore\nend\n";
        let annotated = OpLogEntry {
            timestamp_ms: 2 * DAY_MS,
            user_actor_id: "t".into(),
            op_kind: OpKind::Annotated,
            content_hash_before: content_hash(c0.as_bytes()),
            content_hash_after: content_hash(c1.as_bytes()),
            payload_bytes: encode_annotated(
                OpKind::EditBatch,
                &encode_edit_batch(&crate::diff::diff_to_ops(c0, c1)),
                &[OpAnnotation::SetProperty {
                    key: "k".into(),
                    value_json: "1".into(),
                }],
            ),
        };
        let entries = vec![snapshot(DAY_MS, c0), annotated, batch(3 * DAY_MS, c1, c2)];
        write_log(tmp.path(), "ann", &entries);
        // Fold the first two (retention): the annotated entry's edit is
        // absorbed into the anchor; its annotation is discarded.
        let outcome = compact_log(
            tmp.path(),
            "ann",
            "n.md",
            &limits(u64::MAX, u64::MAX, 8),
            10 * DAY_MS,
        )
        .unwrap();
        assert!(matches!(
            outcome,
            CompactionOutcome::Rewritten { folded: 2, .. }
        ));
        let folded = read_oplog(tmp.path(), "ann").unwrap();
        assert_eq!(folded.len(), 2);
        assert_eq!(folded[0].payload_bytes, c1.as_bytes());
        assert_eq!(reconstruct_at_tail(&folded).unwrap(), c2);
    }

    #[test]
    fn corrupt_prefix_refuses_to_compact() {
        let tmp = tempfile::tempdir().unwrap();
        // A batch whose recorded hash_after does NOT match its real
        // reconstruction — the anchor synthesis must refuse.
        let c0 = "base\n";
        let mut bad = batch(2 * DAY_MS, c0, "real result\n");
        bad.content_hash_after = content_hash(b"claimed different bytes");
        let entries = vec![
            snapshot(DAY_MS, c0),
            bad,
            batch(3 * DAY_MS, "real result\n", "end\n"),
        ];
        write_log(tmp.path(), "corrupt", &entries);
        let err = compact_log(
            tmp.path(),
            "corrupt",
            "n.md",
            &limits(u64::MAX, u64::MAX, 8),
            10 * DAY_MS,
        )
        .unwrap_err();
        assert!(err.to_string().contains("integrity"), "got: {err}");
        // Nothing was rewritten.
        let on_disk = read_oplog(tmp.path(), "corrupt").unwrap();
        assert_eq!(on_disk.len(), 3);
    }

    /// `HOLD_LOCK_FOR` is a process-global last-writer-wins slot; the
    /// tests that set it serialize through this so a plain
    /// `cargo test` (threads in one process, unlike nextest's
    /// process-per-test) can't have one test clear another's hold
    /// mid-flight.
    static HOLD_HOOK_SERIAL: std::sync::Mutex<()> = std::sync::Mutex::new(());

    #[test]
    fn append_during_held_compaction_completes() {
        let _serial = HOLD_HOOK_SERIAL.lock().unwrap_or_else(|p| p.into_inner());
        // Editor-blocking gate: an append against a mid-flight
        // compaction waits on the per-log lock and completes.
        let tmp = tempfile::tempdir().unwrap();
        let (entries, contents) = chain(4);
        write_log(tmp.path(), "held", &entries);

        *HOLD_LOCK_FOR.lock().unwrap() = Some((tmp.path().to_path_buf(), 300));
        let cache = tmp.path().to_path_buf();
        let compactor = std::thread::spawn(move || {
            compact_log(
                &cache,
                "held",
                "n.md",
                &limits(u64::MAX, u64::MAX, 6),
                10 * DAY_MS,
            )
            .unwrap()
        });
        // Give the compactor a beat to take the lock.
        std::thread::sleep(std::time::Duration::from_millis(50));
        let started = std::time::Instant::now();
        let last = contents.last().unwrap().clone();
        let newer = format!("{last}appended during compaction\n");
        append_entry(
            tmp.path(),
            "held",
            "n.md",
            &batch(20 * DAY_MS, &last, &newer),
        )
        .unwrap();
        let waited = started.elapsed();
        *HOLD_LOCK_FOR.lock().unwrap() = None;
        let outcome = compactor.join().unwrap();
        assert!(matches!(outcome, CompactionOutcome::Rewritten { .. }));
        assert!(
            waited < std::time::Duration::from_secs(5),
            "append blocked too long: {waited:?}"
        );
        // The appended entry landed on the POST-rewrite file (the
        // appender acquires the sidecar lock first and only then opens
        // the path, so it addresses the new file by construction) and
        // the log reconstructs with it.
        let final_entries = read_oplog(tmp.path(), "held").unwrap();
        assert_eq!(reconstruct_at_tail(&final_entries).unwrap(), newer);
    }

    /// #928 discipline pin: the per-log mutation lock lives on the
    /// SIDECAR (`<stem>.oplog.lock`), never on the log itself.
    /// `File::lock` is **mandatory** `LockFileEx` on Windows — a lock
    /// on the log would make every lock-free reader (`read_oplog`,
    /// history scans, the event-regeneration second handle) fail with
    /// a lock violation mid-compaction, and the pre-#928
    /// lock-then-verify-inode protocol silently lost appends through
    /// its no-op non-unix inode check. On unix `try_lock` OBSERVES
    /// `flock` state (independent opens contend, even in-process), so
    /// this pins the load-bearing Windows property on every platform:
    /// while a compaction holds the mutation lock, the sidecar is held
    /// and the log file itself is unlocked and readable through an
    /// independent handle.
    #[test]
    fn mutation_lock_lives_on_the_sidecar_not_the_log() {
        use std::fs::TryLockError;
        let _serial = HOLD_HOOK_SERIAL.lock().unwrap_or_else(|p| p.into_inner());
        let tmp = tempfile::tempdir().unwrap();
        let (entries, _) = chain(4);
        write_log(tmp.path(), "sidecar", &entries);
        let log_path = oplog_path_for_name(tmp.path(), "sidecar");
        let sidecar_path = crate::oplog::sidecar_lock_path(&log_path);

        *HOLD_LOCK_FOR.lock().unwrap() = Some((tmp.path().to_path_buf(), 2_000));
        let cache = tmp.path().to_path_buf();
        let compactor = std::thread::spawn(move || {
            compact_log(
                &cache,
                "sidecar",
                "n.md",
                &limits(u64::MAX, u64::MAX, 6),
                10 * DAY_MS,
            )
            .unwrap()
        });

        // Wait (bounded) until the compactor provably holds the
        // sidecar: our own try_lock on it reports WouldBlock. A
        // momentary Ok just means the compactor hasn't acquired yet —
        // release and retry.
        let deadline = std::time::Instant::now() + std::time::Duration::from_secs(20);
        loop {
            let probe = fs::OpenOptions::new()
                .write(true)
                .open(&sidecar_path)
                .expect("lock_oplog created the sidecar");
            match probe.try_lock() {
                Err(TryLockError::WouldBlock) => break, // compactor holds it
                Err(e) => panic!("sidecar try_lock probe errored: {e:?}"),
                Ok(()) => {
                    probe.unlock().unwrap();
                    assert!(
                        std::time::Instant::now() < deadline,
                        "compactor never acquired the sidecar lock"
                    );
                    std::thread::yield_now();
                }
            }
        }

        // The mutation lock is held — yet the LOG file is not OS-locked
        // (an independent handle can lock/unlock it freely)...
        let log_probe = fs::OpenOptions::new()
            .read(true)
            .write(true)
            .open(&log_path)
            .unwrap();
        log_probe
            .try_lock()
            .expect("the log file must never be OS-locked while the mutation lock is held (#928)");
        log_probe.unlock().unwrap();
        // ...and lock-free reads proceed — the exact operation
        // mandatory `LockFileEx` on the log would fail on Windows.
        let read_mid_hold = read_oplog(tmp.path(), "sidecar").unwrap();
        assert!(!read_mid_hold.is_empty());

        *HOLD_LOCK_FOR.lock().unwrap() = None;
        let outcome = compactor.join().unwrap();
        assert!(matches!(outcome, CompactionOutcome::Rewritten { .. }));
    }

    // --- censuses ------------------------------------------------------

    struct SplitMix64(u64);
    impl SplitMix64 {
        fn next(&mut self) -> u64 {
            self.0 = self.0.wrapping_add(0x9E37_79B9_7F4A_7C15);
            let mut z = self.0;
            z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
            z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
            z ^ (z >> 31)
        }
        fn below(&mut self, n: usize) -> usize {
            (self.next() % n as u64) as usize
        }
    }

    fn census_scale() -> u64 {
        if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
            400
        } else {
            48
        }
    }

    /// Random histories — including backwards-clock segments — with
    /// compactions at random points interleaved with more edits and
    /// repeated compactions: reconstruction is byte-equal before/after
    /// every compaction, every retained hash still reconstructs
    /// byte-identically, and the generation strictly increases across
    /// rewrites.
    #[test]
    fn census_compaction_preserves_history() {
        for seed in 0..census_scale() {
            let mut rng = SplitMix64(seed.wrapping_mul(0xDEAD_BEEF).wrapping_add(7));
            let tmp = tempfile::tempdir().unwrap();
            let stem = "census";
            assert!(try_create_log(tmp.path(), stem, "census.md").unwrap());

            let mut doc = format!("seed {seed}\n");
            let mut ts: i64 = 30 * DAY_MS;
            append_entry(tmp.path(), stem, "census.md", &snapshot(ts, &doc)).unwrap();
            let mut last_generation = 0u32;

            for step in 0..(6 + rng.below(10)) {
                // Clock walk: mostly forward, sometimes a hard step back.
                ts += match rng.below(5) {
                    0 => -(rng.below(3 * DAY_MS as usize) as i64), // backwards
                    _ => (1 + rng.below(2 * DAY_MS as usize)) as i64,
                };
                let new = format!("{doc}step {step} edit {}\n", rng.next() % 1000);
                append_entry(tmp.path(), stem, "census.md", &batch(ts, &doc, &new)).unwrap();
                doc = new;

                if rng.below(3) == 0 {
                    let before = read_oplog(tmp.path(), stem).unwrap();
                    let before_text = reconstruct_at_tail(&before).unwrap();
                    let lim = limits(
                        if rng.below(2) == 0 { 512 } else { u64::MAX },
                        if rng.below(2) == 0 { 3 } else { u64::MAX },
                        rng.below(30) as u32 + 1,
                    );
                    let now = ts + (rng.below(40) as i64) * DAY_MS;
                    if let CompactionOutcome::Rewritten { .. } =
                        compact_log(tmp.path(), stem, "census.md", &lim, now).unwrap()
                    {
                        {
                            let (header, after) = read_oplog_with_header(tmp.path(), stem).unwrap();
                            assert!(
                                header.generation > last_generation,
                                "seed {seed}: generation must strictly increase"
                            );
                            last_generation = header.generation;
                            assert_eq!(
                                reconstruct_at_tail(&after).unwrap(),
                                before_text,
                                "seed {seed} step {step}: compaction changed the document"
                            );
                            // Every retained hash still reconstructs.
                            for i in 0..after.len() {
                                let prefix = reconstruct_at_tail(&after[..=i]).unwrap();
                                assert_eq!(
                                    content_hash(prefix.as_bytes()),
                                    after[i].content_hash_after,
                                    "seed {seed} step {step}: retained entry {i} broke identity"
                                );
                            }
                        }
                    }
                }
            }
            let final_entries = read_oplog(tmp.path(), stem).unwrap();
            assert_eq!(
                reconstruct_at_tail(&final_entries).unwrap(),
                doc,
                "seed {seed}: final state diverged from the reference model"
            );
        }
    }

    /// Writers appending while a compactor loops — both on the real
    /// locked code paths, no mocks. After quiescence: the retained log
    /// is a clean suffix (no lost, duplicated, or reordered entries),
    /// it reconstructs to the reference model, and the identity axiom
    /// holds for every retained entry.
    #[test]
    fn census_append_compact_race() {
        use std::sync::atomic::{AtomicBool, Ordering};
        let rounds = if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
            12
        } else {
            3
        };
        for seed in 0..rounds {
            let tmp = tempfile::tempdir().unwrap();
            let cache = tmp.path().to_path_buf();
            let stem = "raced";
            assert!(try_create_log(&cache, stem, "raced.md").unwrap());
            append_entry(&cache, stem, "raced.md", &snapshot(DAY_MS, "seed\n")).unwrap();

            let stop = std::sync::Arc::new(AtomicBool::new(false));
            let compactor = {
                let cache = cache.clone();
                let stop = std::sync::Arc::clone(&stop);
                std::thread::spawn(move || {
                    let lim = limits(2048, 6, u32::MAX);
                    while !stop.load(Ordering::SeqCst) {
                        let _ = compact_log(&cache, stem, "raced.md", &lim, 100 * DAY_MS);
                    }
                })
            };

            // One writer appends a KNOWN global sequence of snapshots
            // (self-anchoring — chaining is irrelevant to the race
            // being tested, which is entry loss/duplication).
            let total = 60 + seed * 10;
            let mut expected: Vec<String> = Vec::new();
            for i in 0..total {
                let content = format!("snapshot {seed}/{i}\n");
                append_entry(
                    &cache,
                    stem,
                    "raced.md",
                    &snapshot((2 + i as i64) * DAY_MS, &content),
                )
                .unwrap();
                expected.push(content);
            }
            stop.store(true, Ordering::SeqCst);
            compactor.join().unwrap();

            let entries = read_oplog(&cache, stem).unwrap();
            // Tail is exactly the last write; the document reconstructs
            // to it (nothing lost after the final fold point).
            assert_eq!(
                reconstruct_at_tail(&entries).unwrap(),
                *expected.last().unwrap(),
                "seed {seed}: reference model diverged"
            );
            // Retained snapshot payloads must be a clean SUFFIX of the
            // appended sequence: in order, no duplicates, no gaps.
            let retained: Vec<String> = entries
                .iter()
                .filter(|e| e.op_kind == OpKind::WholeFileReplace)
                .map(|e| String::from_utf8(e.payload_bytes.clone()).unwrap())
                .filter(|c| c.starts_with("snapshot")) // skip seed + anchors of it
                .collect();
            // Anchors synthesized from a fold carry a payload equal to
            // some appended snapshot, so `retained` may repeat the
            // suffix start — dedup adjacent duplicates before the
            // suffix check.
            let mut deduped: Vec<&String> = Vec::new();
            for c in &retained {
                if deduped.last().map(|l| *l != c).unwrap_or(true) {
                    deduped.push(c);
                }
            }
            let suffix_start = expected.len() - deduped.len();
            assert_eq!(
                deduped,
                expected[suffix_start..].iter().collect::<Vec<_>>(),
                "seed {seed}: retained entries are not a clean suffix of the appends"
            );
            // Identity axiom over the survivor.
            for i in 0..entries.len() {
                let prefix = reconstruct_at_tail(&entries[..=i]).unwrap();
                assert_eq!(
                    content_hash(prefix.as_bytes()),
                    entries[i].content_hash_after,
                    "seed {seed}: identity axiom broken at {i}"
                );
            }
        }
    }

    /// §9.3.3's scale gate in test form: five years of daily edits with
    /// compaction firing on the byte trigger — the final log
    /// reconstructs, and total on-disk size stays under a ceiling
    /// computed from the thresholds.
    #[test]
    fn census_five_years_daily_editing() {
        let days: usize = if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
            5 * 365
        } else {
            300
        };
        let tmp = tempfile::tempdir().unwrap();
        let stem = "daily";
        assert!(try_create_log(tmp.path(), stem, "daily.md").unwrap());
        let lim = limits(64 * 1024, 10_000, 90);

        let mut doc = String::from("# Daily note\n");
        append_entry(tmp.path(), stem, "daily.md", &snapshot(DAY_MS, &doc)).unwrap();
        let mut last_len;
        for day in 1..days {
            let ts = (1 + day as i64) * DAY_MS;
            let new = format!("{doc}day {day}: wrote a line of thoughts today\n");
            last_len = append_entry(tmp.path(), stem, "daily.md", &batch(ts, &doc, &new)).unwrap();
            doc = new;
            if last_len > lim.threshold_bytes {
                let outcome = compact_log(tmp.path(), stem, "daily.md", &lim, ts).unwrap();
                assert!(
                    !matches!(outcome, CompactionOutcome::Missing),
                    "day {day}: log vanished"
                );
            }
            // The doc itself grows unbounded in this synthetic; cap it
            // so the snapshot payloads stay bounded like a real note.
            if doc.len() > 8 * 1024 {
                doc = String::from("# Daily note (rotated)\n");
                let ts2 = ts + 1;
                append_entry(tmp.path(), stem, "daily.md", &snapshot(ts2, &doc)).unwrap();
            }
        }
        let entries = read_oplog(tmp.path(), stem).unwrap();
        assert_eq!(reconstruct_at_tail(&entries).unwrap(), doc);
        // Concrete ceiling: one full threshold of un-compacted growth
        // on top of a post-compaction floor of threshold/2 + one
        // max-size entry (snapshot of the capped doc + framing).
        let max_entry = 8 * 1024 + 512;
        let ceiling = lim.threshold_bytes + lim.threshold_bytes / 2 + max_entry;
        let size = std::fs::metadata(oplog_path_for_name(tmp.path(), stem))
            .unwrap()
            .len();
        assert!(
            size <= ceiling,
            "five-years log size {size} exceeded the ceiling {ceiling}"
        );
    }
}
