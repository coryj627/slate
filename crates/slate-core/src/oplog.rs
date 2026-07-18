// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Append-only binary save journal for vault notes.
//!
//! Each indexed note gets its own log at
//! `<cache_dir>/oplog/<log_name>.oplog`. Since O-1 (#539) the log name
//! is an opaque stem bound to the file through the `files.oplog_name`
//! column — **never derived from `files.id`**, whose rowids SQLite can
//! recycle after a delete (see the hazard note on the session's
//! `OplogAppendState`). Legacy v1 logs keep their historical
//! `<file_id>.oplog` names; migration 027 stamps those bindings into
//! the column at upgrade time, and new logs get collision-proof random
//! stems (see [`try_create_log`]).
//!
//! Op kinds recorded (#378 §7.1, #372, #539):
//! - `WholeFileReplace` — payload is the full new file; used as the
//!   periodic **snapshot / replay anchor** (and for the first save of a
//!   file each session, and for CLI/scripted `None`-hash saves).
//! - `EditBatch` — payload is the encoded vector of fine-grained
//!   Insert/Delete/Replace [`EditOp`]s a single save produced (via
//!   [`crate::diff::diff_to_ops`]). **One entry per save** so a save is
//!   atomic at the framing layer (a torn save is one bad trailing entry,
//!   and prefix recovery returns a valid older state — splitting a save
//!   across N entries would let recovery stop mid-save and replay a
//!   document that never existed).
//! - `CanvasApply` — one committed canvas action (Milestone T #372);
//!   semantic audit record, skipped by text replay.
//! - `Annotated` — an annotated wrapper around a single inner
//!   snapshot/batch entry (O-1 #539), so a save that carries semantic
//!   intent (`SetProperty`, `PathChanged`, …) is still **one atomic
//!   entry**. See [`OpAnnotation`].
//!
//! [`reconstruct_at_tail`] materialises the current document by seeding
//! the last snapshot and replaying every later batch — the read side
//! for change-tracking / history consumers. Compaction *execution*
//! (see `SessionConfig::oplog_compaction_threshold_*`) lands with O-2
//! (#540); this module decides snapshot *cadence* against that
//! threshold but never rewrites or truncates a log.
//!
//! # Format
//!
//! ```text
//! File header (fixed 8 bytes, written when the file is first created):
//!   0..4   magic "YOLG"
//!   4      format_version (1 or 2)
//!   5..8   reserved (0 0 0)
//!
//! v2 header extension (immediately after the fixed header; v2 files only):
//!   path_len:    u16 LE
//!   path:        path_len bytes (UTF-8, vault-relative, as at creation)
//!   generation:  u32 LE (0 at creation; incremented by each compaction
//!                rewrite — O-2 consumes it for paging-cursor invalidation)
//!
//! Entry (variable length, repeated to end of file):
//!   body_len:      u32 LE
//!   body (body_len bytes):
//!     timestamp_ms:     i64 LE
//!     op_kind:          u8 (1 = WholeFileReplace, 2 = EditBatch,
//!                           3 = CanvasApply, 4 = Annotated)
//!     actor_id_len:     u16 LE
//!     actor_id_bytes:   actor_id_len bytes (UTF-8)
//!     hash_before_len:  u16 LE
//!     hash_before:      hash_before_len bytes (ASCII hex)
//!     hash_after_len:   u16 LE
//!     hash_after:       hash_after_len bytes (ASCII hex)
//!     payload_len:      u32 LE
//!     payload:          payload_len bytes
//!   body_checksum: u32 LE (first 4 bytes of blake3(body) — torn-write canary)
//!
//! Annotated (kind 4) payload:
//!   inner_kind:    u8 (1 = WholeFileReplace, 2 = EditBatch — only)
//!   inner_len:     u32 LE
//!   inner_payload: inner_len bytes (interpreted per inner_kind)
//!   ann_count:     u16 LE
//!   annotations (ann_count times):
//!     ann_tag:     u8
//!     ann_len:     u32 LE
//!     ann_body:    ann_len bytes (UTF-8 JSON)
//! ```
//!
//! Unknown `ann_tag`s are **skipped** using `ann_len` — the annotation
//! vocabulary is forward-extensible without a format bump, unlike op
//! kinds, where unknown = truncate-to-prefix.
//!
//! All multi-byte integers are little-endian, fixed at write time so
//! a vault written on one platform is portable to another.
//!
//! # Downgrade note
//!
//! Builds older than O stop reading a log at the first kind-4 entry
//! (the prefix rule) and fail hard on v2 headers. Acceptable:
//! same-machine downgrades mid-vault are already unsupported; the
//! failure mode is "history unavailable", never corruption.
//!
//! # Durability + concurrency
//!
//! Appends open the file with `O_APPEND` so successive writes always
//! land at the current end, even if multiple processes ever touch the
//! same log. The body+checksum buffer is assembled in memory and
//! handed to a single `write_all`, so within a process the OS sees one
//! append per entry. After writing, the file is `sync_data`'d so the
//! entry survives a crash.
//!
//! Concurrent writers within a single process are serialized by the
//! `VaultSession`'s connection mutex (which is held across the whole
//! save path), so individual entries cannot interleave.
//!
//! # Recovery
//!
//! `read_oplog` validates the header and walks entries one by one.
//! Any mid-entry failure — short read, body_checksum mismatch,
//! unparseable body — stops the read at that point and returns the
//! well-formed prefix. A crashing writer that died mid-entry will
//! produce one bad trailing entry; the next reader sees every
//! previously committed entry and treats the trailing junk as if it
//! were never written.

use std::fs::{self, OpenOptions};
use std::io::{self, Read, Write};
use std::path::{Path, PathBuf};

use crate::vault::content_hash;

const MAGIC: [u8; 4] = *b"YOLG";
const FORMAT_VERSION_V1: u8 = 1;
const FORMAT_VERSION_V2: u8 = 2;
const HEADER_LEN: usize = 8;

/// Parsed log header. v1 files (legacy, `<file_id>.oplog`-named) carry
/// no path record and read as `created_path: None, generation: 0`; new
/// logs are always created v2. There is **no eager migration** — v1
/// logs are upgraded only when a compaction rewrite (O-2) replaces
/// them wholesale.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct OplogHeader {
    pub version: u8,
    /// Vault-relative path of the file at log creation (v2 only).
    /// Rename/move continuity comes from `PathChanged` annotations,
    /// not from rewriting this record.
    pub created_path: Option<String>,
    /// Compaction rewrite counter (v2 only; 0 at creation). O-2 bumps
    /// it on every rewrite; O-3's paging cursors embed it so a
    /// mid-drain compaction surfaces as a typed error, never a
    /// silently-shifted page.
    pub generation: u32,
}

/// Sanity ceiling on a single entry body. Defends against corrupt
/// `body_len` fields that would otherwise try to allocate gigabytes
/// before we even look at the content. 64 MiB comfortably exceeds
/// the `large_file_refuse_bytes` ceiling (50 MiB) plus framing
/// overhead, so a legitimate `WholeFileReplace` entry will always fit.
const MAX_PLAUSIBLE_BODY_LEN: usize = 64 * 1024 * 1024;

/// Kind of operation recorded in an op-log entry.
///
/// Discriminants are **append-only and monotonic**: a reader that meets
/// a kind it doesn't understand stops and returns the clean prefix
/// (`try_from_u8` → `None` → `read_oplog` `break`). That degrades
/// gracefully only because a newer writer appends newer kinds *after*
/// older ones — a writer must never emit a higher discriminant before a
/// lower one it would strand. 5..=8 remain reserved for future kinds;
/// `try_from_u8` returns `None` for them until then. Semantic intent
/// does NOT get new kinds — it rides [`OpAnnotation`]s inside kind 4,
/// whose tag space is forward-extensible without stranding readers.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum OpKind {
    /// Whole-file replace: the payload is the full new file contents.
    /// Snapshot / replay anchor.
    WholeFileReplace,
    /// A single save's fine-grained edit ops, encoded as a batch (see
    /// [`encode_edit_batch`] / [`decode_edit_batch`]).
    EditBatch,
    /// One committed canvas action (Milestone T #372): payload is the
    /// JSON encoding of `{ name, action, inverse }` from
    /// `canvas::apply`. Semantic audit record alongside the byte-level
    /// text entries the canvas save also writes; enables named,
    /// canvas-aware document revert. Additive: pre-T readers stop at
    /// the first such entry (the documented unknown-kind degradation)
    /// — entries before it still read.
    CanvasApply,
    /// An annotated wrapper around a single inner snapshot/batch entry
    /// (O-1 #539): the payload is `encode_annotated(inner_kind,
    /// inner_payload, annotations)`. Every save stays **one atomic
    /// entry** — a cadence-forced snapshot that also set a property is
    /// one kind-4 entry wrapping a kind-1 payload, never an entry
    /// pair. A kind-4 entry wrapping an **empty batch** with
    /// `hash_before == hash_after` is a pure marker (the `PathChanged`
    /// case).
    Annotated,
}

impl OpKind {
    fn as_u8(self) -> u8 {
        match self {
            OpKind::WholeFileReplace => 1,
            OpKind::EditBatch => 2,
            OpKind::CanvasApply => 3,
            OpKind::Annotated => 4,
        }
    }

    fn try_from_u8(v: u8) -> Option<Self> {
        match v {
            1 => Some(OpKind::WholeFileReplace),
            2 => Some(OpKind::EditBatch),
            3 => Some(OpKind::CanvasApply),
            4 => Some(OpKind::Annotated),
            _ => None,
        }
    }
}

// --- Annotations (O-1 #539) ------------------------------------------

const ANN_TAG_SET_PROPERTY: u8 = 1;
const ANN_TAG_REMOVE_PROPERTY: u8 = 2;
const ANN_TAG_TOGGLE_TASK: u8 = 3;
const ANN_TAG_FRONTMATTER_REPLACE: u8 = 4;
const ANN_TAG_PATH_CHANGED: u8 = 5;

/// Semantic intent recorded alongside a save, where the write path
/// actually knows it (plan decision #2): property edits, frontmatter
/// replacement, task toggles, and rename/move (`PathChanged`).
/// Free-typed edits carry no annotations — their semantic
/// classification happens at read time in the structured-diff engine
/// (O-4).
///
/// Bodies are UTF-8 JSON so the vocabulary can grow fields without a
/// tag bump; unknown tags are skipped on decode via their length
/// prefix.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum OpAnnotation {
    /// `set_property`: `value_json` is the JSON encoding of the value
    /// as written into the frontmatter.
    SetProperty { key: String, value_json: String },
    /// `delete_property`.
    RemoveProperty { key: String },
    /// `toggle_task_status`: `ordinal` is the task's document ordinal,
    /// `new_status` the raw status character written between `[` `]`.
    ToggleTask { ordinal: u32, new_status: char },
    /// `set_frontmatter_source` (U3-4 show-source whole-frontmatter
    /// edit). No fields — the diff engine derives the key-level story.
    FrontmatterReplace,
    /// Rename/move marker. Appended as a **pure marker** (kind 4
    /// wrapping an empty batch, `hash_before == hash_after` = the
    /// log's tail hash — see the marker hash rule in o_spec §O-1).
    PathChanged { from: String, to: String },
}

impl OpAnnotation {
    fn tag(&self) -> u8 {
        match self {
            OpAnnotation::SetProperty { .. } => ANN_TAG_SET_PROPERTY,
            OpAnnotation::RemoveProperty { .. } => ANN_TAG_REMOVE_PROPERTY,
            OpAnnotation::ToggleTask { .. } => ANN_TAG_TOGGLE_TASK,
            OpAnnotation::FrontmatterReplace => ANN_TAG_FRONTMATTER_REPLACE,
            OpAnnotation::PathChanged { .. } => ANN_TAG_PATH_CHANGED,
        }
    }

    fn body_json(&self) -> String {
        match self {
            OpAnnotation::SetProperty { key, value_json } => {
                serde_json::json!({ "key": key, "value_json": value_json }).to_string()
            }
            OpAnnotation::RemoveProperty { key } => serde_json::json!({ "key": key }).to_string(),
            OpAnnotation::ToggleTask {
                ordinal,
                new_status,
            } => serde_json::json!({
                "ordinal": ordinal,
                "new_status": new_status.to_string(),
            })
            .to_string(),
            OpAnnotation::FrontmatterReplace => "{}".to_string(),
            OpAnnotation::PathChanged { from, to } => {
                serde_json::json!({ "from": from, "to": to }).to_string()
            }
        }
    }

    /// Decode one known-tag body. Malformed JSON or missing fields on
    /// a KNOWN tag is corruption → descriptive error (unknown tags
    /// never reach here — they're skipped by length).
    fn from_tag_and_body(tag: u8, body: &[u8]) -> Result<Self, String> {
        let value: serde_json::Value = serde_json::from_slice(body)
            .map_err(|e| format!("annotation tag {tag}: malformed JSON body ({e})"))?;
        let str_field = |name: &str| -> Result<String, String> {
            value
                .get(name)
                .and_then(serde_json::Value::as_str)
                .map(str::to_string)
                .ok_or_else(|| format!("annotation tag {tag}: missing field {name:?}"))
        };
        match tag {
            ANN_TAG_SET_PROPERTY => Ok(OpAnnotation::SetProperty {
                key: str_field("key")?,
                value_json: str_field("value_json")?,
            }),
            ANN_TAG_REMOVE_PROPERTY => Ok(OpAnnotation::RemoveProperty {
                key: str_field("key")?,
            }),
            ANN_TAG_TOGGLE_TASK => {
                let ordinal = value
                    .get("ordinal")
                    .and_then(serde_json::Value::as_u64)
                    .and_then(|v| u32::try_from(v).ok())
                    .ok_or_else(|| format!("annotation tag {tag}: missing field \"ordinal\""))?;
                let status = str_field("new_status")?;
                let mut chars = status.chars();
                let (Some(new_status), None) = (chars.next(), chars.next()) else {
                    return Err(format!(
                        "annotation tag {tag}: new_status must be exactly one character"
                    ));
                };
                Ok(OpAnnotation::ToggleTask {
                    ordinal,
                    new_status,
                })
            }
            ANN_TAG_FRONTMATTER_REPLACE => Ok(OpAnnotation::FrontmatterReplace),
            ANN_TAG_PATH_CHANGED => Ok(OpAnnotation::PathChanged {
                from: str_field("from")?,
                to: str_field("to")?,
            }),
            other => Err(format!(
                "decode_annotated: internal error, tag {other} is unknown"
            )),
        }
    }
}

/// Encode an [`OpKind::Annotated`] payload wrapping one inner entry.
///
/// `inner_kind` must be `WholeFileReplace` or `EditBatch` — the wrapper
/// exists so a save with intent stays one atomic entry, and only those
/// two kinds are save shapes. Debug builds assert; release builds
/// encode what they're given (the decoder rejects other inner kinds,
/// so a bad caller surfaces as a read-side error, never silent
/// corruption).
pub fn encode_annotated(
    inner_kind: OpKind,
    inner_payload: &[u8],
    anns: &[OpAnnotation],
) -> Vec<u8> {
    debug_assert!(
        matches!(inner_kind, OpKind::WholeFileReplace | OpKind::EditBatch),
        "Annotated may only wrap a snapshot or a batch"
    );
    let bodies: Vec<(u8, String)> = anns.iter().map(|a| (a.tag(), a.body_json())).collect();
    let ann_bytes: usize = bodies.iter().map(|(_, b)| 1 + 4 + b.len()).sum();
    let mut buf = Vec::with_capacity(1 + 4 + inner_payload.len() + 2 + ann_bytes);
    buf.push(inner_kind.as_u8());
    buf.extend_from_slice(&(inner_payload.len() as u32).to_le_bytes());
    buf.extend_from_slice(inner_payload);
    buf.extend_from_slice(&(bodies.len() as u16).to_le_bytes());
    for (tag, body) in &bodies {
        buf.push(*tag);
        buf.extend_from_slice(&(body.len() as u32).to_le_bytes());
        buf.extend_from_slice(body.as_bytes());
    }
    buf
}

/// Decode an [`OpKind::Annotated`] payload into `(inner_kind,
/// inner_payload, annotations)`. Unknown annotation tags are skipped
/// via their length prefix (forward-extensible vocabulary); any
/// truncation or a malformed KNOWN-tag body is a descriptive error —
/// never a panic.
pub fn decode_annotated(payload: &[u8]) -> Result<(OpKind, Vec<u8>, Vec<OpAnnotation>), String> {
    let mut cur = Cursor::new(payload);
    let inner_kind_raw = cur.read_u8()?;
    let inner_kind = match OpKind::try_from_u8(inner_kind_raw) {
        Some(k @ (OpKind::WholeFileReplace | OpKind::EditBatch)) => k,
        _ => {
            return Err(format!(
                "annotated inner kind must be 1 (snapshot) or 2 (batch), got {inner_kind_raw}"
            ));
        }
    };
    let inner_len = cur.read_u32()? as usize;
    if inner_len > MAX_PLAUSIBLE_BODY_LEN {
        return Err(format!(
            "implausible annotated inner_len {inner_len} (max {MAX_PLAUSIBLE_BODY_LEN})"
        ));
    }
    let inner_payload = cur.read_bytes(inner_len)?;
    let ann_count = cur.read_u16()? as usize;
    let mut anns = Vec::with_capacity(ann_count.min(64));
    for _ in 0..ann_count {
        let tag = cur.read_u8()?;
        let ann_len = cur.read_u32()? as usize;
        if ann_len > MAX_PLAUSIBLE_BODY_LEN {
            return Err(format!(
                "implausible annotation body length {ann_len} (max {MAX_PLAUSIBLE_BODY_LEN})"
            ));
        }
        let body = cur.read_bytes(ann_len)?;
        // Unknown tags fall through: their body was consumed above, so
        // a future writer's new annotation can't strand this reader.
        if let tag @ ANN_TAG_SET_PROPERTY..=ANN_TAG_PATH_CHANGED = tag {
            anns.push(OpAnnotation::from_tag_and_body(tag, &body)?);
        }
    }
    cur.expect_eof()?;
    Ok((inner_kind, inner_payload, anns))
}

/// One fine-grained edit within an [`OpKind::EditBatch`]. Offsets are
/// **UTF-8 byte offsets in the OLD-content space** (the document state
/// just before the save that produced the batch). No removed/old text is
/// stored — replay is always forward from a snapshot, so `Delete` /
/// `Replace` need only the range (matches `05` §7.4's shapes and halves
/// the payload). Produced by [`crate::diff::diff_to_ops`].
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum EditOp {
    /// Insert `text` at byte `pos`.
    Insert { pos: usize, text: String },
    /// Delete the half-open byte range `[start, end)`.
    Delete { start: usize, end: usize },
    /// Replace the half-open byte range `[start, end)` with `text`.
    Replace {
        start: usize,
        end: usize,
        text: String,
    },
}

impl EditOp {
    /// The op's anchor offset in old-content space — the key
    /// [`reconstruct_at_tail`] sorts a batch by (descending) so earlier
    /// edits don't shift the offsets of later ones during replay.
    fn old_offset(&self) -> usize {
        match self {
            EditOp::Insert { pos, .. } => *pos,
            EditOp::Delete { start, .. } | EditOp::Replace { start, .. } => *start,
        }
    }
}

const OP_TAG_INSERT: u8 = 1;
const OP_TAG_DELETE: u8 = 2;
const OP_TAG_REPLACE: u8 = 3;

/// Sanity ceiling on an `op_count` field, mirroring
/// [`MAX_PLAUSIBLE_BODY_LEN`]'s intent: a corrupt count must not drive a
/// huge pre-allocation before we've validated any op bytes. One op is at
/// least 9 bytes (tag + u64), so a body can't legitimately carry more
/// than `MAX_PLAUSIBLE_BODY_LEN / 9` ops.
const MAX_PLAUSIBLE_OP_COUNT: usize = MAX_PLAUSIBLE_BODY_LEN / 9;

/// Encode a save's edit ops into an [`OpKind::EditBatch`] payload:
/// `op_count:u32 | [op_tag:u8 | fields]*`. Each text blob is length-
/// prefixed (`u32`) because ops are packed back-to-back, so a decoder
/// must know where each op ends:
/// - Insert: `pos:u64 | text_len:u32 | text`
/// - Delete: `start:u64 | end:u64`
/// - Replace: `start:u64 | end:u64 | text_len:u32 | text`
///
/// All integers little-endian, matching the rest of the format.
pub fn encode_edit_batch(ops: &[EditOp]) -> Vec<u8> {
    let mut buf = Vec::new();
    buf.extend_from_slice(&(ops.len() as u32).to_le_bytes());
    for op in ops {
        match op {
            EditOp::Insert { pos, text } => {
                buf.push(OP_TAG_INSERT);
                buf.extend_from_slice(&(*pos as u64).to_le_bytes());
                buf.extend_from_slice(&(text.len() as u32).to_le_bytes());
                buf.extend_from_slice(text.as_bytes());
            }
            EditOp::Delete { start, end } => {
                buf.push(OP_TAG_DELETE);
                buf.extend_from_slice(&(*start as u64).to_le_bytes());
                buf.extend_from_slice(&(*end as u64).to_le_bytes());
            }
            EditOp::Replace { start, end, text } => {
                buf.push(OP_TAG_REPLACE);
                buf.extend_from_slice(&(*start as u64).to_le_bytes());
                buf.extend_from_slice(&(*end as u64).to_le_bytes());
                buf.extend_from_slice(&(text.len() as u32).to_le_bytes());
                buf.extend_from_slice(text.as_bytes());
            }
        }
    }
    buf
}

/// Decode an [`OpKind::EditBatch`] payload back into its ops. Returns a
/// descriptive error (never panics) on any malformed/truncated payload,
/// so a corrupt entry is rejected exactly like any other parse failure.
pub fn decode_edit_batch(payload: &[u8]) -> Result<Vec<EditOp>, String> {
    let mut cur = Cursor::new(payload);
    let count = cur.read_u32()? as usize;
    if count > MAX_PLAUSIBLE_OP_COUNT {
        return Err(format!(
            "implausible op_count {count} (max {MAX_PLAUSIBLE_OP_COUNT})"
        ));
    }
    let mut ops = Vec::with_capacity(count.min(1024));
    for _ in 0..count {
        let op = match cur.read_u8()? {
            OP_TAG_INSERT => {
                let pos = cur.read_u64()? as usize;
                let len = cur.read_u32()? as usize;
                EditOp::Insert {
                    pos,
                    text: cur.read_string(len)?,
                }
            }
            OP_TAG_DELETE => EditOp::Delete {
                start: cur.read_u64()? as usize,
                end: cur.read_u64()? as usize,
            },
            OP_TAG_REPLACE => {
                let start = cur.read_u64()? as usize;
                let end = cur.read_u64()? as usize;
                let len = cur.read_u32()? as usize;
                EditOp::Replace {
                    start,
                    end,
                    text: cur.read_string(len)?,
                }
            }
            other => return Err(format!("unknown edit-op tag {other}")),
        };
        ops.push(op);
    }
    cur.expect_eof()?;
    Ok(ops)
}

/// One entry's contribution to text replay, with `Annotated` wrappers
/// unwrapped to their inner shape. Payloads stay borrowed for bare
/// entries; only the annotated case owns (the inner slice has to be
/// decoded out of the wrapper).
enum ReplayOp<'a> {
    Snapshot(std::borrow::Cow<'a, [u8]>),
    Batch(std::borrow::Cow<'a, [u8]>),
    /// `CanvasApply` — semantic record, no text contribution.
    Skip,
}

/// Unwrap one entry to its replay contribution. Decoding an annotated
/// entry decodes its annotation block too (cheap — annotations are
/// small JSON bodies), so a corrupt wrapper surfaces here as a typed
/// error rather than a silent misread.
fn replay_op(entry: &OpLogEntry) -> Result<ReplayOp<'_>, String> {
    use std::borrow::Cow;
    match entry.op_kind {
        OpKind::WholeFileReplace => Ok(ReplayOp::Snapshot(Cow::Borrowed(&entry.payload_bytes))),
        OpKind::EditBatch => Ok(ReplayOp::Batch(Cow::Borrowed(&entry.payload_bytes))),
        OpKind::CanvasApply => Ok(ReplayOp::Skip),
        OpKind::Annotated => {
            let (inner_kind, inner_payload, _anns) = decode_annotated(&entry.payload_bytes)?;
            match inner_kind {
                OpKind::WholeFileReplace => Ok(ReplayOp::Snapshot(Cow::Owned(inner_payload))),
                OpKind::EditBatch => Ok(ReplayOp::Batch(Cow::Owned(inner_payload))),
                // decode_annotated only admits 1 | 2.
                _ => unreachable!("decode_annotated admits only snapshot/batch inner kinds"),
            }
        }
    }
}

/// Materialise the document represented by `entries` (the in-order result
/// of [`read_oplog`]): seed the last snapshot — a bare
/// [`OpKind::WholeFileReplace`] **or** an [`OpKind::Annotated`] entry
/// wrapping one — then replay every later batch in order (annotated
/// batches unwrap; `CanvasApply` records are skipped; a pure marker is
/// an empty batch and contributes nothing).
///
/// Within a batch, ops apply in **descending old-offset order** so an
/// earlier edit never shifts the offsets a later edit in the same batch
/// was computed against (the batch's ops are all in one old-content
/// space; disjoint and document-ordered from the diff). An op whose range
/// is out of bounds for the running buffer is a corruption signal →
/// typed error; we do **not** lean on `TextBuffer`'s silent clamping,
/// which would mask it. An empty log reconstructs to `""`.
///
/// The anchor search walks from the tail and only decodes annotated
/// entries it actually meets, so corruption in a pre-anchor entry that
/// replay never touches stays inert (matching the pre-O-1 behavior
/// where pre-anchor batches were never decoded).
pub fn reconstruct_at_tail(entries: &[OpLogEntry]) -> Result<String, String> {
    if entries.is_empty() {
        return Ok(String::new());
    }

    // Find the last snapshot-shaped entry, unwrapping annotated
    // wrappers as we meet them (tail-first, so only entries at or
    // after the anchor are ever decoded).
    let mut anchor: Option<(usize, std::borrow::Cow<'_, [u8]>)> = None;
    for (idx, entry) in entries.iter().enumerate().rev() {
        match replay_op(entry)? {
            ReplayOp::Snapshot(payload) => {
                anchor = Some((idx, payload));
                break;
            }
            ReplayOp::Batch(_) | ReplayOp::Skip => {}
        }
    }
    let Some((snapshot_idx, snapshot_payload)) = anchor else {
        return Err("op log has edit batches but no snapshot anchor".to_string());
    };

    let seed = |bytes: &[u8]| -> Result<crate::text_buffer::TextBuffer, String> {
        std::str::from_utf8(bytes)
            .map(crate::text_buffer::TextBuffer::from_str)
            .map_err(|_| "snapshot payload is not valid UTF-8".to_string())
    };

    let mut buf = seed(snapshot_payload.as_ref())?;
    for entry in &entries[snapshot_idx + 1..] {
        match replay_op(entry)? {
            // Defensive: a later snapshot (shouldn't occur after the
            // reverse walk found the last one) re-seeds.
            ReplayOp::Snapshot(payload) => buf = seed(payload.as_ref())?,
            ReplayOp::Batch(payload) => {
                let mut ops = decode_edit_batch(payload.as_ref())?;
                ops.sort_by_key(|op| std::cmp::Reverse(op.old_offset()));
                for op in &ops {
                    apply_op(&mut buf, op)?;
                }
            }
            ReplayOp::Skip => {}
        }
    }
    Ok(buf.to_string())
}

/// Advance a running replay buffer by ONE entry — the O(entry)
/// incremental form of [`reconstruct_at_tail`] for full-log walks
/// (O-6's event rebuild). A per-prefix `reconstruct_at_tail` over a
/// compacted log — one synthesized anchor followed by thousands of
/// batches — re-replays the whole prefix each step, O(n²) end to end
/// (measured in whole seconds per hot log; adversarial round 3).
/// `None` = no snapshot seen yet: a batch in that state is the same
/// "batches but no anchor" divergence `reconstruct_at_tail` reports.
/// Marker/`CanvasApply` entries leave the buffer untouched.
pub(crate) fn replay_advance(
    buf: &mut Option<crate::text_buffer::TextBuffer>,
    entry: &OpLogEntry,
) -> Result<(), String> {
    match replay_op(entry)? {
        ReplayOp::Snapshot(payload) => {
            let text = std::str::from_utf8(payload.as_ref())
                .map_err(|_| "snapshot payload is not valid UTF-8".to_string())?;
            *buf = Some(crate::text_buffer::TextBuffer::from_str(text));
        }
        ReplayOp::Batch(payload) => {
            let Some(buf) = buf.as_mut() else {
                return Err("edit batch before any snapshot anchor".to_string());
            };
            let mut ops = decode_edit_batch(payload.as_ref())?;
            ops.sort_by_key(|op| std::cmp::Reverse(op.old_offset()));
            for op in &ops {
                apply_op(buf, op)?;
            }
        }
        ReplayOp::Skip => {}
    }
    Ok(())
}

/// Apply one op to the running replay buffer, bounds-checking against the
/// *current* buffer length so a corrupt offset surfaces as an error
/// rather than being silently clamped by `TextBuffer`.
fn apply_op(buf: &mut crate::text_buffer::TextBuffer, op: &EditOp) -> Result<(), String> {
    let len = buf.len_bytes();
    match op {
        EditOp::Insert { pos, text } => {
            if *pos > len {
                return Err(format!("insert pos {pos} past end {len}"));
            }
            buf.insert(*pos, text);
        }
        EditOp::Delete { start, end } => {
            if start > end || *end > len {
                return Err(format!(
                    "delete range {start}..{end} out of bounds (len {len})"
                ));
            }
            buf.delete(*start..*end);
        }
        EditOp::Replace { start, end, text } => {
            if start > end || *end > len {
                return Err(format!(
                    "replace range {start}..{end} out of bounds (len {len})"
                ));
            }
            buf.replace(*start..*end, text);
        }
    }
    Ok(())
}

/// One recorded operation. `payload_bytes` is interpreted per `op_kind`:
/// the full new file for `WholeFileReplace`, or the encoded
/// [`encode_edit_batch`] op-vector for `EditBatch`.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct OpLogEntry {
    pub timestamp_ms: i64,
    pub user_actor_id: String,
    pub op_kind: OpKind,
    pub content_hash_before: String,
    pub content_hash_after: String,
    pub payload_bytes: Vec<u8>,
}

/// Reconstruct the file's bytes as they stood immediately AFTER the entry
/// whose `hash_after` equals `target_hash` (U2-2 undo: restore a rewritten
/// file to its recorded pre-op state). Walks prefix reconstructions in
/// order, so a hash that occurs more than once resolves to its FIRST
/// occurrence — any occurrence has identical bytes by the hash contract.
/// `None` when no entry produced that hash (journal/op-log divergence —
/// the caller surfaces a typed error, never a clobber).
pub fn reconstruct_at_hash(entries: &[OpLogEntry], target_hash: &str) -> Option<String> {
    let position = entries
        .iter()
        .position(|e| e.content_hash_after == target_hash)?;
    reconstruct_at_tail(&entries[..=position]).ok()
}

/// The per-vault op-log directory: `<cache_dir>/oplog`.
pub fn oplog_dir(cache_dir: &Path) -> PathBuf {
    cache_dir.join("oplog")
}

/// Path to the log file with the given name stem. Since O-1 the stem is
/// **only** meaningful through the `files.oplog_name` binding column —
/// log paths are never derived from `files.id` (rowid reuse would let a
/// new note silently inherit a dead note's history).
pub fn oplog_path_for_name(cache_dir: &Path, log_name: &str) -> PathBuf {
    oplog_dir(cache_dir).join(format!("{log_name}.oplog"))
}

/// The serialized v2 header block (fixed header + path record +
/// generation) for a fresh log.
/// A `created_path` longer than the u16 length prefix can carry is
/// written as an EMPTY path record rather than silently truncated —
/// `path_len as u16` would wrap and desynchronize the entry stream
/// (Codoki review, PR #790). An empty record reads back as
/// `created_path: None`, so the log degrades to marker/salvage-based
/// identity exactly like a v1 log — safe, never corrupt. (Real vault
/// paths are nowhere near 64 KiB; this is a safety rail, not a case.)
pub(crate) fn v2_header_block(created_path: &str, generation: u32) -> Vec<u8> {
    let path_bytes = if created_path.len() <= u16::MAX as usize {
        created_path.as_bytes()
    } else {
        &[]
    };
    let mut block = Vec::with_capacity(HEADER_LEN + 2 + path_bytes.len() + 4);
    block.extend_from_slice(&MAGIC);
    block.push(FORMAT_VERSION_V2);
    block.extend_from_slice(&[0, 0, 0]); // reserved
    block.extend_from_slice(&(path_bytes.len() as u16).to_le_bytes());
    block.extend_from_slice(path_bytes);
    block.extend_from_slice(&generation.to_le_bytes());
    block
}

/// Create `<log_name>.oplog` with a fresh v2 header if it doesn't
/// already exist. Returns `Ok(true)` when this call created it,
/// `Ok(false)` when a log with that stem already exists.
///
/// Existence-check + create are serialized by a **directory-level lock
/// file** (`<cache_dir>/oplog/.dir.lock`, OS exclusive lock) — the
/// per-log-file locks in [`append_entry`] can't cover a creation race
/// between two allocators that derived the same stem. Stem collisions
/// are astronomically unlikely (the session derives stems from
/// `blake3(path ‖ now_ms ‖ counter)`), so the lock is cheap insurance,
/// not a hot path.
pub fn try_create_log(cache_dir: &Path, log_name: &str, created_path: &str) -> io::Result<bool> {
    let dir = oplog_dir(cache_dir);
    fs::create_dir_all(&dir)?;
    let lock_file = fs::OpenOptions::new()
        .create(true)
        .truncate(false)
        .write(true)
        .open(dir.join(".dir.lock"))?;
    lock_file.lock()?;

    let path = oplog_path_for_name(cache_dir, log_name);
    match OpenOptions::new().create_new(true).write(true).open(&path) {
        Ok(mut file) => {
            file.write_all(&v2_header_block(created_path, 0))?;
            file.sync_data()?;
            let _ = fsync_dir(&dir);
            Ok(true)
        }
        Err(e) if e.kind() == io::ErrorKind::AlreadyExists => Ok(false),
        Err(e) => Err(e),
    }
    // `lock_file` drops here, releasing the directory lock.
}

/// Validate the header of an open log and return the parsed
/// [`OplogHeader`] plus the byte offset where entries begin. The
/// reader is expected to be positioned at the start of the file.
/// Any torn or malformed header — including a v2 header extension the
/// file is too short to contain — is a hard `InvalidData` error: we
/// can't locate the entry stream, so there is no clean prefix to
/// salvage.
pub(crate) fn read_header(file: &mut fs::File, path: &Path) -> io::Result<Option<OplogHeader>> {
    let mut header = [0u8; HEADER_LEN];
    match read_fully(file, &mut header)? {
        ReadOutcome::Full => {}
        ReadOutcome::Eof => return Ok(None),
        ReadOutcome::Partial => {
            return Err(io::Error::new(
                io::ErrorKind::InvalidData,
                format!("oplog {path:?}: truncated header"),
            ));
        }
    }
    if header[0..4] != MAGIC {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            format!("oplog {path:?}: bad magic, not a Slate op-log"),
        ));
    }
    match header[4] {
        FORMAT_VERSION_V1 => Ok(Some(OplogHeader {
            version: FORMAT_VERSION_V1,
            created_path: None,
            generation: 0,
        })),
        FORMAT_VERSION_V2 => {
            let torn = |what: &str| {
                io::Error::new(
                    io::ErrorKind::InvalidData,
                    format!("oplog {path:?}: torn v2 header ({what})"),
                )
            };
            let mut len_buf = [0u8; 2];
            match read_fully(file, &mut len_buf)? {
                ReadOutcome::Full => {}
                _ => return Err(torn("missing path length")),
            }
            let path_len = u16::from_le_bytes(len_buf) as usize;
            let mut path_buf = vec![0u8; path_len];
            match read_fully(file, &mut path_buf)? {
                ReadOutcome::Full => {}
                _ => return Err(torn("short path record")),
            }
            let created_path =
                String::from_utf8(path_buf).map_err(|_| torn("path record is not valid UTF-8"))?;
            // An empty record means "no usable creation path" (the
            // oversized-path safety rail above) — surface it as None
            // so consumers fall back to markers/salvage, matching v1.
            let created_path = (!created_path.is_empty()).then_some(created_path);
            let mut generation_buf = [0u8; 4];
            match read_fully(file, &mut generation_buf)? {
                ReadOutcome::Full => {}
                _ => return Err(torn("missing generation")),
            }
            Ok(Some(OplogHeader {
                version: FORMAT_VERSION_V2,
                created_path,
                generation: u32::from_le_bytes(generation_buf),
            }))
        }
        other => Err(io::Error::new(
            io::ErrorKind::InvalidData,
            format!(
                "oplog {path:?}: unsupported format version {other}, this build understands {FORMAT_VERSION_V1} and {FORMAT_VERSION_V2}"
            ),
        )),
    }
}

/// The per-log **sidecar lock file**: `<stem>.oplog.lock`, next to the
/// log. The `.lock` suffix keeps it out of every `.oplog` enumeration
/// (`reconcile_oplogs` strips exactly `".oplog"`).
pub(crate) fn sidecar_lock_path(log_path: &Path) -> std::path::PathBuf {
    let mut name = log_path.file_name().unwrap_or_default().to_os_string();
    name.push(".lock");
    log_path.with_file_name(name)
}

/// Held for the duration of one oplog mutation (append / marker /
/// compaction rewrite / event-index regeneration). Dropping it closes
/// the sidecar handle, which releases the OS lock on every platform.
#[derive(Debug)]
pub(crate) struct OplogLock(#[allow(dead_code)] fs::File);

/// Acquire the per-log exclusive mutation lock — on the **sidecar**
/// file, never on the log itself (#928, supersedes O-2's
/// lock-then-verify-inode).
///
/// Why a sidecar: `File::lock` is advisory `flock(2)` on unix but
/// **mandatory** `LockFileEx` on Windows — while a lock on the log
/// itself is held, *any other handle's* read or write of the log fails
/// with a lock violation instead of proceeding (unix) or blocking.
/// That broke the choreography twice on the first Windows run: the
/// lock-free readers (`read_oplog`, history scans — including the
/// event-regeneration path reading through a second handle while
/// itself holding the lock) failed mid-compaction, and the old
/// lock-then-verify-inode protocol silently lost appends because the
/// non-unix inode check was a no-op. Locking a file whose bytes nobody
/// reads or writes makes mandatory-ness unobservable, so both
/// platforms get identical, advisory-shaped semantics.
///
/// Why no verify-retry anymore: the inode race existed because writers
/// opened the log **before** acquiring its lock, so a compaction
/// rename could swap the binding in between. With the sidecar the lock
/// is acquired **before** the log is opened, and every path→file
/// mutation (the compaction rename-over) happens under this same lock
/// — so a handle opened under the lock is the current file by
/// construction.
///
/// Creates the oplog directory if needed (so lock-first ordering works
/// for the compact-a-never-created-log case). The sidecar is created
/// once and never deleted while its log lives; reclamation removes it
/// best-effort together with the log.
pub(crate) fn lock_oplog(log_path: &Path) -> io::Result<OplogLock> {
    if let Some(dir) = log_path.parent() {
        fs::create_dir_all(dir)?;
    }
    let sidecar = fs::OpenOptions::new()
        .create(true)
        .truncate(false)
        .write(true)
        .open(sidecar_lock_path(log_path))?;
    sidecar.lock()?;
    Ok(OplogLock(sidecar))
}

/// Append a single entry to `<cache_dir>/oplog/<log_name>.oplog`.
///
/// Creates the directory and writes a fresh **v2** header carrying
/// `created_path` on first use (a pre-existing v1 or v2 header is left
/// exactly as found — no eager migration). Returns the post-append file
/// length once the entry's bytes are durably on disk
/// (`sync_data`-flushed) — O-2's compaction trigger check consumes the
/// length so the save path never pays an extra syscall.
pub fn append_entry(
    cache_dir: &Path,
    log_name: &str,
    created_path: &str,
    entry: &OpLogEntry,
) -> io::Result<u64> {
    let dir = oplog_dir(cache_dir);
    fs::create_dir_all(&dir)?;
    let path = oplog_path_for_name(cache_dir, log_name);

    // First-write race (PR #105 Codoki feedback, refined here).
    //
    // The earlier `create_new(true)` shape is *almost* race-free: only
    // one caller wins the create. But the dirent becomes visible to
    // every other thread *immediately* — before the create-winner has
    // written the header. A loser arriving in that window opens the
    // file in pure-append mode and writes its entry body ahead of the
    // header, leaving the file with the loser's `body_len` u32 where
    // MAGIC should be. `read_oplog` then rejects the file with "bad
    // magic" and every previously committed entry is unrecoverable.
    //
    // Fix: hold the per-log exclusive mutation lock (the SIDECAR lock
    // — see `lock_oplog`, #928: never an OS lock on the log itself,
    // which is mandatory on Windows and fails other handles' reads)
    // across "decide whether to write the header" *and* "write our
    // entry." Whoever gets the lock first sees the empty file and
    // writes the header before appending. Subsequent lock-holders see
    // a non-empty file with a valid header already in place and just
    // append. Acquiring the lock BEFORE opening the log means a
    // compaction rename can't swap the path→file binding in between —
    // this handle is the current file by construction (the race the
    // old lock-then-verify-inode retry existed for). The lock is
    // released when `_lock` drops at the end of this function.
    let _lock = lock_oplog(&path)?;
    let mut file = OpenOptions::new()
        .create(true)
        .read(true)
        .append(true)
        .open(&path)?;

    let len = file.metadata()?.len();
    let is_new_file = len == 0;
    let mut bytes_before_entry = len;
    if is_new_file {
        let header = v2_header_block(created_path, 0);
        file.write_all(&header)?;
        bytes_before_entry = header.len() as u64;
    } else {
        // Validate the existing header under the lock. This covers the
        // v1 torn-header case (fewer than 8 bytes) AND a v2 header cut
        // mid-path-record — in either state appending would produce a
        // tail-only entry `read_oplog` rejects anyway, so refuse
        // loudly instead. Reads use the file cursor (position 0 on
        // open); O_APPEND only forces *writes* to the end.
        read_header(&mut file, &path)?;
    }

    // Assemble the entire framed record in memory and write it with a
    // single syscall. Under O_APPEND, the kernel is responsible for
    // making each write append atomically; using one write() (rather
    // than several) keeps individual entries from interleaving with
    // concurrent writers up to the OS's atomic-append guarantee. The
    // exclusive lock above is the load-bearing serializer; this
    // single-syscall pattern is belt-and-braces.
    let framed = frame_entry(entry);
    file.write_all(&framed)?;
    file.sync_data()?;
    // Arithmetic, not a second stat: the exclusive lock guarantees no
    // interleaved writer, so pre-entry length + our frame IS the file
    // length (O-2's trigger check must cost the save path zero extra
    // syscalls).
    let post_append_len = bytes_before_entry + framed.len() as u64;
    // First-append dirent durability (PR #105 Codoki feedback).
    // `sync_data` flushes the file's content blocks but does NOT
    // guarantee the directory entry pointing at our newly-created
    // inode is on disk. Without this extra step, a power loss right
    // after the first append could leave the parent directory in a
    // state where our file doesn't exist — losing the user's first
    // save despite our own fsync. Gating to `is_new_file` keeps the
    // cost off subsequent appends, which only need their data
    // pages flushed (the dirent is already durable from this first
    // pass).
    if is_new_file {
        let _ = fsync_dir(&dir);
    }
    Ok(post_append_len)
}

/// The exact on-disk frame size for one entry, computed arithmetically
/// — no allocation, no hashing (Codoki PR #791: the size fold needs
/// per-entry sizes without building frames).
pub(crate) fn frame_size(entry: &OpLogEntry) -> u64 {
    let body = 8 // timestamp
        + 1 // op_kind
        + 2 + entry.user_actor_id.len()
        + 2 + entry.content_hash_before.len()
        + 2 + entry.content_hash_after.len()
        + 4 + entry.payload_bytes.len();
    (4 + body + 4) as u64
}

/// The on-disk frame for one entry: `body_len | body | body_checksum`.
pub(crate) fn frame_entry(entry: &OpLogEntry) -> Vec<u8> {
    let body = serialize_body(entry);
    let mut framed = Vec::with_capacity(4 + body.len() + 4);
    framed.extend_from_slice(&(body.len() as u32).to_le_bytes());
    framed.extend_from_slice(&body);
    framed.extend_from_slice(&body_checksum(&body).to_le_bytes());
    framed
}

/// Atomically append a pure `PathChanged` marker (O-1 #539): the tail
/// read and the marker append happen under ONE exclusive file lock, so
/// no concurrent writer — this process or another (the `slate` CLI) —
/// can slip an entry between "observe the tail hash" and "append the
/// marker carrying it". Without that atomicity the marker could land
/// after a newer entry while carrying an older tail hash, and its
/// prefix reconstruction would no longer hash to its `hash_after` —
/// the identity-axiom violation the marker hash rule exists to
/// prevent.
///
/// Returns `Ok(None)` — no marker written — when the log is missing or
/// has no clean entries (there is no history to re-path);
/// `Ok(Some(post_append_len))` on success. A torn trailing entry is
/// tolerated exactly as saves tolerate it: the marker chains onto the
/// clean prefix's tail (it lands after the torn bytes and stays
/// invisible to readers until compaction heals the log — the same
/// degradation every post-torn append shares).
pub fn append_path_changed_marker(
    cache_dir: &Path,
    log_name: &str,
    from: &str,
    to: &str,
    user_actor_id: &str,
    timestamp_ms: i64,
) -> io::Result<Option<u64>> {
    let path = oplog_path_for_name(cache_dir, log_name);
    // Sidecar mutation lock first, then open (#928) — a missing log
    // stays "nothing to re-path", it just surfaces from the open now.
    let _lock = lock_oplog(&path)?;
    let mut file = match OpenOptions::new().read(true).append(true).open(&path) {
        Ok(f) => f,
        Err(e) if e.kind() == io::ErrorKind::NotFound => return Ok(None),
        Err(e) => return Err(e),
    };

    let len = file.metadata()?.len();
    if len == 0 {
        return Ok(None); // header-less empty file — nothing to re-path
    }
    read_header(&mut file, &path)?;
    let entries = read_entries_stream(&mut file, log_name, &path)?;
    let Some(tail) = entries.last() else {
        return Ok(None); // no entries — nothing to re-path
    };
    let tail_hash = tail.content_hash_after.clone();

    let entry = OpLogEntry {
        timestamp_ms,
        user_actor_id: user_actor_id.to_string(),
        op_kind: OpKind::Annotated,
        content_hash_before: tail_hash.clone(),
        content_hash_after: tail_hash,
        payload_bytes: encode_annotated(
            OpKind::EditBatch,
            &encode_edit_batch(&[]),
            &[OpAnnotation::PathChanged {
                from: from.to_string(),
                to: to.to_string(),
            }],
        ),
    };
    let framed = frame_entry(&entry);
    file.write_all(&framed)?;
    file.sync_data()?;
    Ok(Some(len + framed.len() as u64))
}

/// Best-effort directory fsync. On Unix, opening the directory as a
/// file and calling `sync_all` makes the directory's metadata
/// (including just-added dirents) durable. On non-Unix targets the
/// equivalent semantics don't apply (NTFS commits dirents via its
/// filesystem journal; sandboxed iOS doesn't expose a directory fsync
/// anyway), so we no-op there. Errors are returned via the call site,
/// which currently ignores them — a failed dir-sync still leaves the
/// data durable; the worst case is the file disappears on power loss
/// before the next save_text re-creates it.
#[cfg(unix)]
pub(crate) fn fsync_dir(dir: &Path) -> io::Result<()> {
    fs::File::open(dir)?.sync_all()
}

#[cfg(not(unix))]
pub(crate) fn fsync_dir(_dir: &Path) -> io::Result<()> {
    Ok(())
}

/// Read all well-formed entries from `<cache_dir>/oplog/<log_name>.oplog`.
///
/// A missing file is normal — it just means no save has been recorded
/// yet — and returns `Ok(Vec::new())`. See
/// [`read_oplog_with_header`] for the header-carrying variant and the
/// full degradation contract.
pub fn read_oplog(cache_dir: &Path, log_name: &str) -> io::Result<Vec<OpLogEntry>> {
    read_oplog_with_header(cache_dir, log_name).map(|(_, entries)| entries)
}

/// Read the header plus all well-formed entries from
/// `<cache_dir>/oplog/<log_name>.oplog`.
///
/// A missing or zero-length file returns an empty v1-shaped header and
/// no entries. A header mismatch (wrong magic, unsupported version, or
/// a torn v2 header extension) is fatal because we don't know how to
/// interpret the file at all. Per-entry corruption (short read,
/// checksum mismatch, unparseable body) stops the walk at that point
/// and returns the prefix that read cleanly; the truncation is reported
/// through the [`log`] facade (#507) — a `warn!` carrying the log's
/// name stem and corruption kind, with the on-disk cache path on a
/// separate `debug!` line so it stays out of shipped host logs (see the
/// crate-root privacy rule). The stem is either a legacy numeric id or
/// an opaque hash-derived token, never a vault path; the full cache
/// path can still carry the host's home directory / username, so it
/// gets the debug-only treatment.
pub fn read_oplog_with_header(
    cache_dir: &Path,
    log_name: &str,
) -> io::Result<(OplogHeader, Vec<OpLogEntry>)> {
    let empty_header = || OplogHeader {
        version: FORMAT_VERSION_V1,
        created_path: None,
        generation: 0,
    };
    let path = oplog_path_for_name(cache_dir, log_name);
    let mut file = match fs::File::open(&path) {
        Ok(f) => f,
        Err(e) if e.kind() == io::ErrorKind::NotFound => {
            return Ok((empty_header(), Vec::new()));
        }
        Err(e) => return Err(e),
    };

    let header = match read_header(&mut file, &path)? {
        Some(h) => h,
        None => return Ok((empty_header(), Vec::new())),
    };
    let entries = read_entries_stream(&mut file, log_name, &path)?;
    Ok((header, entries))
}

/// Walk the entry stream of an already-header-validated open log,
/// returning the clean prefix. Any torn/corrupt trailing material stops
/// the walk (reported through the facade — see
/// [`read_oplog_with_header`]'s degradation contract).
pub(crate) fn read_entries_stream(
    file: &mut fs::File,
    log_name: &str,
    path: &Path,
) -> io::Result<Vec<OpLogEntry>> {
    let mut entries: Vec<OpLogEntry> = Vec::new();
    loop {
        let mut len_buf = [0u8; 4];
        match read_fully(file, &mut len_buf)? {
            ReadOutcome::Eof => break,
            ReadOutcome::Partial => {
                warn_torn_oplog(log_name, path, "trailing torn body-length");
                break;
            }
            ReadOutcome::Full => {}
        }
        let body_len = u32::from_le_bytes(len_buf) as usize;
        if body_len > MAX_PLAUSIBLE_BODY_LEN {
            warn_torn_oplog(
                log_name,
                path,
                &format!("implausible body_len={body_len} (max {MAX_PLAUSIBLE_BODY_LEN})"),
            );
            break;
        }

        let mut body = vec![0u8; body_len];
        match read_fully(file, &mut body)? {
            ReadOutcome::Full => {}
            _ => {
                warn_torn_oplog(log_name, path, "trailing torn body");
                break;
            }
        }

        let mut sum_buf = [0u8; 4];
        match read_fully(file, &mut sum_buf)? {
            ReadOutcome::Full => {}
            _ => {
                warn_torn_oplog(log_name, path, "trailing missing checksum");
                break;
            }
        }
        let recorded = u32::from_le_bytes(sum_buf);
        if body_checksum(&body) != recorded {
            warn_torn_oplog(log_name, path, "checksum mismatch on trailing entry");
            break;
        }

        match parse_body(&body) {
            Ok(entry) => entries.push(entry),
            Err(e) => {
                warn_torn_oplog(log_name, path, &format!("malformed entry body ({e})"));
                break;
            }
        }
    }
    Ok(entries)
}

/// Report a recoverable op-log truncation through the [`log`] facade
/// (#507). `kind` is a non-identifying corruption description; it
/// rides a `warn!` with the log's name stem (a legacy numeric id or an
/// opaque hash token — never a vault path). The on-disk cache `path`
/// goes on a separate `debug!` line so it never reaches shipped host
/// logs — hosts don't enable debug in release (see the crate-root
/// privacy rule). This is a walk-stopping degradation, never data
/// loss: the well-formed prefix is still returned.
fn warn_torn_oplog(log_name: &str, path: &Path, kind: &str) {
    log::warn!("oplog {log_name}: {kind}, skipping rest");
    log::debug!("oplog truncation for {log_name} was at path {path:?}");
}

/// Test-only: write a **v1** log (legacy 8-byte header, no path record)
/// with the given entries. Production code never writes v1 headers
/// after O-1; migration/adoption tests need to manufacture the legacy
/// on-disk state this module must keep reading.
#[cfg(test)]
pub(crate) fn write_v1_log_for_tests(cache_dir: &Path, log_name: &str, entries: &[OpLogEntry]) {
    let dir = oplog_dir(cache_dir);
    fs::create_dir_all(&dir).unwrap();
    let mut bytes = Vec::new();
    bytes.extend_from_slice(&MAGIC);
    bytes.push(FORMAT_VERSION_V1);
    bytes.extend_from_slice(&[0, 0, 0]);
    for entry in entries {
        let body = serialize_body(entry);
        bytes.extend_from_slice(&(body.len() as u32).to_le_bytes());
        bytes.extend_from_slice(&body);
        bytes.extend_from_slice(&body_checksum(&body).to_le_bytes());
    }
    fs::write(oplog_path_for_name(cache_dir, log_name), bytes).unwrap();
}

fn serialize_body(entry: &OpLogEntry) -> Vec<u8> {
    let actor = entry.user_actor_id.as_bytes();
    let before = entry.content_hash_before.as_bytes();
    let after = entry.content_hash_after.as_bytes();
    let mut buf = Vec::with_capacity(
        8 + 1
            + 2
            + actor.len()
            + 2
            + before.len()
            + 2
            + after.len()
            + 4
            + entry.payload_bytes.len(),
    );
    buf.extend_from_slice(&entry.timestamp_ms.to_le_bytes());
    buf.push(entry.op_kind.as_u8());
    push_u16_prefixed(&mut buf, actor);
    push_u16_prefixed(&mut buf, before);
    push_u16_prefixed(&mut buf, after);
    buf.extend_from_slice(&(entry.payload_bytes.len() as u32).to_le_bytes());
    buf.extend_from_slice(&entry.payload_bytes);
    buf
}

fn push_u16_prefixed(buf: &mut Vec<u8>, bytes: &[u8]) {
    // Caller is responsible for keeping these under u16::MAX. Actor
    // ids and blake3 hex digests both fit comfortably.
    buf.extend_from_slice(&(bytes.len() as u16).to_le_bytes());
    buf.extend_from_slice(bytes);
}

fn body_checksum(body: &[u8]) -> u32 {
    let hash = content_hash(body);
    // Take the first 8 hex characters → 4 bytes → u32 LE. Cheap, no
    // extra dependency, and collisions on a 32-bit window are still
    // vanishingly unlikely for detecting accidental truncation.
    //
    // Relies on `content_hash`'s contract (see `vault::fs::content_hash`):
    // lowercase blake3 hex, exactly 64 characters. If that contract
    // ever changes, this slice + the lowercase-only `hex_nibble`
    // fallback below need to be revisited.
    let head = &hash.as_bytes()[..8];
    let mut nibbles = [0u8; 4];
    for (i, pair) in head.chunks_exact(2).enumerate() {
        nibbles[i] = (hex_nibble(pair[0]) << 4) | hex_nibble(pair[1]);
    }
    u32::from_le_bytes(nibbles)
}

fn hex_nibble(c: u8) -> u8 {
    match c {
        b'0'..=b'9' => c - b'0',
        b'a'..=b'f' => c - b'a' + 10,
        b'A'..=b'F' => c - b'A' + 10,
        _ => 0, // `content_hash` always yields lowercase hex (see its docstring); this branch is unreachable
    }
}

fn parse_body(body: &[u8]) -> Result<OpLogEntry, String> {
    let mut cur = Cursor::new(body);
    let timestamp_ms = cur.read_i64()?;
    let op_kind_raw = cur.read_u8()?;
    let op_kind =
        OpKind::try_from_u8(op_kind_raw).ok_or_else(|| format!("unknown op_kind {op_kind_raw}"))?;
    let actor = cur.read_str_u16()?;
    let before = cur.read_str_u16()?;
    let after = cur.read_str_u16()?;
    let payload_len = cur.read_u32()? as usize;
    let payload = cur.read_bytes(payload_len)?;
    cur.expect_eof()?;
    Ok(OpLogEntry {
        timestamp_ms,
        user_actor_id: actor,
        op_kind,
        content_hash_before: before,
        content_hash_after: after,
        payload_bytes: payload,
    })
}

enum ReadOutcome {
    Full,
    Eof,
    Partial,
}

fn read_fully(file: &mut fs::File, buf: &mut [u8]) -> io::Result<ReadOutcome> {
    let mut total = 0;
    while total < buf.len() {
        match file.read(&mut buf[total..]) {
            Ok(0) if total == 0 => return Ok(ReadOutcome::Eof),
            Ok(0) => return Ok(ReadOutcome::Partial),
            Ok(n) => total += n,
            Err(e) if e.kind() == io::ErrorKind::Interrupted => continue,
            Err(e) => return Err(e),
        }
    }
    Ok(ReadOutcome::Full)
}

struct Cursor<'a> {
    buf: &'a [u8],
    pos: usize,
}

impl<'a> Cursor<'a> {
    fn new(buf: &'a [u8]) -> Self {
        Self { buf, pos: 0 }
    }
    fn read_u8(&mut self) -> Result<u8, String> {
        let v = *self
            .buf
            .get(self.pos)
            .ok_or_else(|| "short read: u8".to_string())?;
        self.pos += 1;
        Ok(v)
    }
    fn read_u16(&mut self) -> Result<u16, String> {
        let slice = self
            .buf
            .get(self.pos..self.pos + 2)
            .ok_or_else(|| "short read: u16".to_string())?;
        self.pos += 2;
        Ok(u16::from_le_bytes([slice[0], slice[1]]))
    }
    fn read_u32(&mut self) -> Result<u32, String> {
        let slice = self
            .buf
            .get(self.pos..self.pos + 4)
            .ok_or_else(|| "short read: u32".to_string())?;
        self.pos += 4;
        Ok(u32::from_le_bytes([slice[0], slice[1], slice[2], slice[3]]))
    }
    fn read_i64(&mut self) -> Result<i64, String> {
        let slice = self
            .buf
            .get(self.pos..self.pos + 8)
            .ok_or_else(|| "short read: i64".to_string())?;
        self.pos += 8;
        let arr = <[u8; 8]>::try_from(slice).unwrap();
        Ok(i64::from_le_bytes(arr))
    }
    fn read_u64(&mut self) -> Result<u64, String> {
        let slice = self
            .buf
            .get(self.pos..self.pos + 8)
            .ok_or_else(|| "short read: u64".to_string())?;
        self.pos += 8;
        let arr = <[u8; 8]>::try_from(slice).unwrap();
        Ok(u64::from_le_bytes(arr))
    }
    fn read_bytes(&mut self, n: usize) -> Result<Vec<u8>, String> {
        let slice = self
            .buf
            .get(self.pos..self.pos + n)
            .ok_or_else(|| format!("short read: {n} bytes"))?;
        self.pos += n;
        Ok(slice.to_vec())
    }
    fn read_str_u16(&mut self) -> Result<String, String> {
        let len = self.read_u16()? as usize;
        let bytes = self.read_bytes(len)?;
        String::from_utf8(bytes).map_err(|_| "non-utf8 string field".to_string())
    }
    fn read_string(&mut self, len: usize) -> Result<String, String> {
        let bytes = self.read_bytes(len)?;
        String::from_utf8(bytes).map_err(|_| "non-utf8 string field".to_string())
    }
    fn expect_eof(&self) -> Result<(), String> {
        if self.pos != self.buf.len() {
            Err(format!(
                "trailing bytes in entry body: {} unread",
                self.buf.len() - self.pos
            ))
        } else {
            Ok(())
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn entry(payload: &[u8]) -> OpLogEntry {
        OpLogEntry {
            timestamp_ms: 1_700_000_000_000,
            user_actor_id: "tester".into(),
            op_kind: OpKind::WholeFileReplace,
            content_hash_before: content_hash(b"old"),
            content_hash_after: content_hash(payload),
            payload_bytes: payload.to_vec(),
        }
    }

    // --- EditBatch encode/decode + replay (#378) ---------------------

    fn snapshot_entry(content: &str) -> OpLogEntry {
        OpLogEntry {
            timestamp_ms: 1,
            user_actor_id: "t".into(),
            op_kind: OpKind::WholeFileReplace,
            content_hash_before: String::new(),
            content_hash_after: content_hash(content.as_bytes()),
            payload_bytes: content.as_bytes().to_vec(),
        }
    }

    fn batch_entry(old: &str, new: &str) -> OpLogEntry {
        OpLogEntry {
            timestamp_ms: 2,
            user_actor_id: "t".into(),
            op_kind: OpKind::EditBatch,
            content_hash_before: content_hash(old.as_bytes()),
            content_hash_after: content_hash(new.as_bytes()),
            payload_bytes: encode_edit_batch(&crate::diff::diff_to_ops(old, new)),
        }
    }

    #[test]
    fn edit_batch_payload_round_trips() {
        let ops = vec![
            EditOp::Insert {
                pos: 0,
                text: "x".into(),
            },
            EditOp::Delete { start: 5, end: 9 },
            EditOp::Replace {
                start: 10,
                end: 12,
                text: "中".into(),
            },
        ];
        assert_eq!(decode_edit_batch(&encode_edit_batch(&ops)).unwrap(), ops);
        // Empty batch round-trips too.
        assert!(
            decode_edit_batch(&encode_edit_batch(&[]))
                .unwrap()
                .is_empty()
        );
    }

    #[test]
    fn reconstruct_replays_snapshot_plus_batch_for_every_shape() {
        // The load-bearing invariant: snapshot(old) + batch(old→new)
        // reconstructs to exactly `new`, across every edit shape.
        let cases = [
            ("", "hello\n"),
            ("a\nb\nc\n", "a\nb\nc\n"),     // identical → empty batch
            ("a\nb\nc\n", ""),              // delete all
            ("- [ ] x\n", "- [x] x\n"),     // single-char
            ("a\n中\nb\n", "a\n中中\nb\n"), // multibyte
            ("a\r\nb\r\n", "a\r\nB\r\n"),   // CRLF
            ("x😀y\n", "x😀😀y\n"),         // astral
            // multi-hunk, non-adjacent (the descending-apply guard):
            (
                "one\ntwo\nthree\nfour\nfive\n",
                "ZERO\none\ntwo\nTHREE\nfour\n",
            ),
        ];
        for (old, new) in cases {
            let entries = vec![snapshot_entry(old), batch_entry(old, new)];
            let got = reconstruct_at_tail(&entries).unwrap();
            assert_eq!(got, new, "case old={old:?} new={new:?}");
            assert_eq!(
                content_hash(got.as_bytes()),
                entries[1].content_hash_after,
                "reconstructed hash must equal the batch's recorded hash_after"
            );
        }
    }

    #[test]
    fn reconstruct_replays_sequential_batches() {
        let v0 = "alpha\nbeta\ngamma\n";
        let v1 = "alpha\nBETA\ngamma\n";
        let v2 = "alpha\nBETA\ngamma\ndelta\n";
        let entries = vec![snapshot_entry(v0), batch_entry(v0, v1), batch_entry(v1, v2)];
        assert_eq!(reconstruct_at_tail(&entries).unwrap(), v2);
    }

    #[test]
    fn reconstruct_handles_insert_after_an_equal_run() {
        // Regression for the D…E…I diff bug (red-team #378): an insertion
        // that follows an `Equal` run must anchor *after* it. These cases
        // silently corrupted the reconstruction before the fix.
        for (old, new) in [
            ("a\n\n", "\n\na"),
            ("Heading\n\n", "\n\nHeading"),
            ("中\n  \n😀\na", "  \n  \n😀\n😀\na\nb"),
        ] {
            let entries = vec![snapshot_entry(old), batch_entry(old, new)];
            assert_eq!(
                reconstruct_at_tail(&entries).unwrap(),
                new,
                "old={old:?} new={new:?}"
            );
        }
    }

    proptest::proptest! {
        // Capped cases: this is a focused regression for the D…E…I shape,
        // not exhaustive fuzzing — 64 short newline-heavy pairs cover it,
        // and keeping the case count low avoids loading the shared test
        // runner (cargo runs tests in parallel; a heavy proptest can
        // starve a sibling timing-sensitive test on a 2-core CI box).
        #![proptest_config(proptest::prelude::ProptestConfig::with_cases(64))]

        /// The load-bearing invariant over random newline/blank-heavy
        /// content — the shape that provokes `similar`'s D…E…I covers:
        /// for any (old, new), snapshot(old) + batch(old→new) reconstructs
        /// to exactly `new`.
        #[test]
        fn reconstruct_round_trips_for_arbitrary_liney_edits(
            old in "[ab \n]{0,32}",
            new in "[ab \n]{0,32}",
        ) {
            let entries = vec![snapshot_entry(&old), batch_entry(&old, &new)];
            proptest::prop_assert_eq!(reconstruct_at_tail(&entries).unwrap(), new);
        }
    }

    #[test]
    fn reconstruct_uses_the_last_snapshot_as_anchor() {
        let entries = vec![
            snapshot_entry("old\n"),
            batch_entry("old\n", "older\n"),
            snapshot_entry("fresh\n"),
        ];
        assert_eq!(reconstruct_at_tail(&entries).unwrap(), "fresh\n");
    }

    #[test]
    fn reconstruct_empty_log_is_empty_string() {
        assert_eq!(reconstruct_at_tail(&[]).unwrap(), "");
    }

    #[test]
    fn reconstruct_rejects_out_of_bounds_op_instead_of_clamping() {
        let entries = vec![
            snapshot_entry("short\n"),
            OpLogEntry {
                timestamp_ms: 2,
                user_actor_id: "t".into(),
                op_kind: OpKind::EditBatch,
                content_hash_before: String::new(),
                content_hash_after: String::new(),
                payload_bytes: encode_edit_batch(&[EditOp::Delete {
                    start: 100,
                    end: 200,
                }]),
            },
        ];
        assert!(
            reconstruct_at_tail(&entries).is_err(),
            "an op past the buffer end must be a typed error, not a silent clamp"
        );
    }

    #[test]
    fn append_then_read_round_trips_edit_batch_through_disk() {
        let tmp = tempfile::tempdir().unwrap();
        append_entry(tmp.path(), "1", "note.md", &snapshot_entry("a\nb\nc\n")).unwrap();
        append_entry(
            tmp.path(),
            "1",
            "note.md",
            &batch_entry("a\nb\nc\n", "a\nB\nc\n"),
        )
        .unwrap();
        let entries = read_oplog(tmp.path(), "1").unwrap();
        assert_eq!(entries.len(), 2);
        assert_eq!(entries[1].op_kind, OpKind::EditBatch);
        assert_eq!(reconstruct_at_tail(&entries).unwrap(), "a\nB\nc\n");
    }

    #[test]
    fn corrupt_trailing_edit_batch_returns_well_formed_prefix() {
        // Crash-safety is preserved for the new kind: a torn EditBatch
        // trailing entry leaves the snapshot + earlier batch intact.
        let tmp = tempfile::tempdir().unwrap();
        append_entry(tmp.path(), "1", "note.md", &snapshot_entry("base\n")).unwrap();
        append_entry(tmp.path(), "1", "note.md", &batch_entry("base\n", "BASE\n")).unwrap();
        // Append a framed-looking EditBatch with a bogus oversized body_len.
        let path = oplog_path_for_name(tmp.path(), "1");
        let mut handle = OpenOptions::new().append(true).open(&path).unwrap();
        let bogus_len: u32 = (MAX_PLAUSIBLE_BODY_LEN as u32).saturating_add(1);
        handle.write_all(&bogus_len.to_le_bytes()).unwrap();
        handle.write_all(b"junk").unwrap();
        let entries = read_oplog(tmp.path(), "1").unwrap();
        assert_eq!(entries.len(), 2, "prefix recovery keeps snapshot + batch");
        assert_eq!(reconstruct_at_tail(&entries).unwrap(), "BASE\n");
    }

    #[test]
    fn unknown_future_op_kind_mid_log_truncates_to_prefix() {
        // Monotonic-kind canary: a well-framed entry with an unknown kind
        // (7) appearing BEFORE a later EditBatch makes the reader stop at
        // it and return only the clean prefix — pins the positional
        // recovery so a future semantic-op writer must append at the tail.
        let tmp = tempfile::tempdir().unwrap();
        append_entry(tmp.path(), "1", "note.md", &snapshot_entry("base\n")).unwrap();
        append_entry(tmp.path(), "1", "note.md", &batch_entry("base\n", "BASE\n")).unwrap();
        forge_raw_entry(&oplog_path_for_name(tmp.path(), "1"), 7, b"future");
        append_entry(
            tmp.path(),
            "1",
            "note.md",
            &batch_entry("BASE\n", "BASE!\n"),
        )
        .unwrap();

        let entries = read_oplog(tmp.path(), "1").unwrap();
        assert_eq!(
            entries.len(),
            2,
            "reader stops at the unknown kind 7, dropping it and everything after"
        );
        assert!(
            entries.iter().all(|e| e.op_kind != OpKind::EditBatch
                || e.content_hash_after == content_hash(b"BASE\n"))
        );
    }

    /// Append a well-framed entry with an arbitrary raw `op_kind` byte
    /// (used to forge a kind no current build understands).
    fn forge_raw_entry(path: &Path, kind_byte: u8, payload: &[u8]) {
        let mut body = Vec::new();
        body.extend_from_slice(&0i64.to_le_bytes()); // timestamp
        body.push(kind_byte);
        body.extend_from_slice(&0u16.to_le_bytes()); // actor len 0
        body.extend_from_slice(&0u16.to_le_bytes()); // hash_before len 0
        body.extend_from_slice(&0u16.to_le_bytes()); // hash_after len 0
        body.extend_from_slice(&(payload.len() as u32).to_le_bytes());
        body.extend_from_slice(payload);
        let checksum = body_checksum(&body);
        let mut handle = OpenOptions::new().append(true).open(path).unwrap();
        handle
            .write_all(&(body.len() as u32).to_le_bytes())
            .unwrap();
        handle.write_all(&body).unwrap();
        handle.write_all(&checksum.to_le_bytes()).unwrap();
    }

    #[test]
    fn read_missing_oplog_returns_empty() {
        let tmp = tempfile::tempdir().unwrap();
        let entries = read_oplog(tmp.path(), "1").unwrap();
        assert!(entries.is_empty());
    }

    #[test]
    fn append_then_read_round_trips_single_entry() {
        let tmp = tempfile::tempdir().unwrap();
        let original = entry(b"hello vault");
        append_entry(tmp.path(), "42", "note.md", &original).unwrap();

        let entries = read_oplog(tmp.path(), "42").unwrap();
        assert_eq!(entries, vec![original]);
    }

    #[test]
    fn multiple_entries_preserve_order() {
        let tmp = tempfile::tempdir().unwrap();
        let a = entry(b"first");
        let b = entry(b"second");
        let c = entry(b"third");
        append_entry(tmp.path(), "7", "note.md", &a).unwrap();
        append_entry(tmp.path(), "7", "note.md", &b).unwrap();
        append_entry(tmp.path(), "7", "note.md", &c).unwrap();

        let entries = read_oplog(tmp.path(), "7").unwrap();
        assert_eq!(entries, vec![a, b, c]);
    }

    #[test]
    fn corrupt_trailing_entry_returns_well_formed_prefix() {
        let tmp = tempfile::tempdir().unwrap();
        let a = entry(b"good-1");
        let b = entry(b"good-2");
        append_entry(tmp.path(), "5", "note.md", &a).unwrap();
        append_entry(tmp.path(), "5", "note.md", &b).unwrap();

        // Append junk that masquerades as the start of an entry: a
        // huge `body_len` followed by some bytes. The reader should
        // see the implausible length and stop, returning only [a, b].
        let path = oplog_path_for_name(tmp.path(), "5");
        let mut handle = OpenOptions::new().append(true).open(&path).unwrap();
        let bogus_len: u32 = (MAX_PLAUSIBLE_BODY_LEN as u32).saturating_add(1);
        handle.write_all(&bogus_len.to_le_bytes()).unwrap();
        handle.write_all(b"junk").unwrap();

        let entries = read_oplog(tmp.path(), "5").unwrap();
        assert_eq!(entries, vec![a, b]);
    }

    #[test]
    fn truncated_body_returns_prefix() {
        let tmp = tempfile::tempdir().unwrap();
        let a = entry(b"good");
        append_entry(tmp.path(), "9", "note.md", &a).unwrap();

        // Write a body_len that promises 1000 more bytes, then only
        // write 10 → reader sees short body, stops.
        let path = oplog_path_for_name(tmp.path(), "9");
        let mut handle = OpenOptions::new().append(true).open(&path).unwrap();
        handle.write_all(&1_000u32.to_le_bytes()).unwrap();
        handle.write_all(&[0u8; 10]).unwrap();

        let entries = read_oplog(tmp.path(), "9").unwrap();
        assert_eq!(entries, vec![a]);
    }

    #[test]
    fn checksum_mismatch_on_trailing_entry_returns_prefix() {
        let tmp = tempfile::tempdir().unwrap();
        let a = entry(b"good");
        append_entry(tmp.path(), "11", "note.md", &a).unwrap();

        // Write a well-framed but corrupt entry: valid body bytes,
        // but with a wrong checksum. The body parses, the checksum
        // doesn't match, so the reader rejects it.
        let body = serialize_body(&entry(b"would-have-been-fine"));
        let path = oplog_path_for_name(tmp.path(), "11");
        let mut handle = OpenOptions::new().append(true).open(&path).unwrap();
        handle
            .write_all(&(body.len() as u32).to_le_bytes())
            .unwrap();
        handle.write_all(&body).unwrap();
        handle.write_all(&0xDEAD_BEEF_u32.to_le_bytes()).unwrap();

        let entries = read_oplog(tmp.path(), "11").unwrap();
        assert_eq!(entries, vec![a]);
    }

    #[test]
    fn empty_file_after_header_is_well_formed() {
        // A file that contains only the header (e.g. crashed between
        // header write and first entry) reads as zero entries, not as
        // an error.
        let tmp = tempfile::tempdir().unwrap();
        let path = oplog_path_for_name(tmp.path(), "1");
        std::fs::create_dir_all(path.parent().unwrap()).unwrap();
        let mut header = [0u8; HEADER_LEN];
        header[0..4].copy_from_slice(&MAGIC);
        header[4] = FORMAT_VERSION_V1;
        std::fs::write(&path, header).unwrap();

        let entries = read_oplog(tmp.path(), "1").unwrap();
        assert!(entries.is_empty());
    }

    #[test]
    fn bad_magic_is_a_hard_error() {
        let tmp = tempfile::tempdir().unwrap();
        let path = oplog_path_for_name(tmp.path(), "3");
        std::fs::create_dir_all(path.parent().unwrap()).unwrap();
        std::fs::write(&path, b"NOPENOPE").unwrap();

        let err = read_oplog(tmp.path(), "3").unwrap_err();
        assert_eq!(err.kind(), io::ErrorKind::InvalidData);
        assert!(err.to_string().contains("bad magic"));
    }

    #[test]
    fn truncated_header_is_a_hard_error() {
        // Simulate a crash between create-new and the 8-byte header
        // write: the file exists but contains fewer than HEADER_LEN
        // bytes. `read_oplog` can't trust any of it (the magic
        // hasn't even fully landed) so it must surface InvalidData
        // rather than silently treating the prefix as the start of
        // an entry. Codoki PR-105 suggestion locking this in.
        let tmp = tempfile::tempdir().unwrap();
        let path = oplog_path_for_name(tmp.path(), "13");
        std::fs::create_dir_all(path.parent().unwrap()).unwrap();
        // 4 bytes — half a header.
        std::fs::write(&path, MAGIC).unwrap();

        let err = read_oplog(tmp.path(), "13").unwrap_err();
        assert_eq!(err.kind(), io::ErrorKind::InvalidData);
        assert!(
            err.to_string().contains("truncated header"),
            "expected truncated-header diagnostic, got {err}"
        );
    }

    #[test]
    fn unsupported_version_is_a_hard_error() {
        let tmp = tempfile::tempdir().unwrap();
        let path = oplog_path_for_name(tmp.path(), "4");
        std::fs::create_dir_all(path.parent().unwrap()).unwrap();
        let mut header = [0u8; HEADER_LEN];
        header[0..4].copy_from_slice(&MAGIC);
        header[4] = 99; // some future version this build doesn't know
        std::fs::write(&path, header).unwrap();

        let err = read_oplog(tmp.path(), "4").unwrap_err();
        assert_eq!(err.kind(), io::ErrorKind::InvalidData);
        assert!(err.to_string().contains("unsupported format version 99"));
    }

    #[test]
    fn large_payload_round_trips() {
        // 1 MiB payload — big enough to exceed any reasonable
        // PIPE_BUF atomicity, exercising the framing logic.
        let tmp = tempfile::tempdir().unwrap();
        let big = vec![0xABu8; 1024 * 1024];
        let original = entry(&big);
        append_entry(tmp.path(), "100", "note.md", &original).unwrap();

        let entries = read_oplog(tmp.path(), "100").unwrap();
        assert_eq!(entries.len(), 1);
        assert_eq!(entries[0].payload_bytes.len(), big.len());
        assert_eq!(entries[0], original);
    }

    #[test]
    fn concurrent_first_appends_write_a_single_header() {
        // Race regression: N threads race to be the first append on a
        // brand-new file_id. The earlier `create_new`-only shape was
        // *almost* right — only one thread won the create — but the
        // dirent became visible to other threads before the create-
        // winner had written the 8-byte header. A loser arriving in
        // that window opened the file in pure-append mode and wrote
        // its body bytes ahead of the header, leaving the file with
        // the loser's `body_len` u32 where MAGIC should be.
        // `read_oplog` then rejected the whole file with "bad magic".
        //
        // The fix takes the per-log exclusive mutation lock (the
        // #928 sidecar) across the "is the header in place?" check
        // *and* the entry append, so whichever thread gets the lock
        // first observes an empty file and writes the header before
        // releasing. With the lock, this test is deterministic; with
        // the pre-fix code it was flaky under `cargo test --workspace`
        // (~10% reproduction rate on macOS) and visibly broken under
        // higher thread counts.
        //
        // We use eight threads (rather than two) because a brief race
        // window with two threads might never get hit by the scheduler
        // on a cold cache; eight makes it overwhelmingly likely that
        // *something* lands in the window if the lock is missing.
        use std::sync::Barrier;
        const THREADS: usize = 8;
        let tmp = tempfile::tempdir().unwrap();
        let tmp_path = tmp.path().to_path_buf();

        let barrier = Arc::new(Barrier::new(THREADS));
        let handles: Vec<_> = (0..THREADS)
            .map(|i| {
                let p = tmp_path.clone();
                let b = Arc::clone(&barrier);
                let payload = format!("thread-{i}").into_bytes();
                std::thread::spawn(move || {
                    b.wait();
                    append_entry(&p, "42", "note.md", &entry(&payload)).unwrap();
                })
            })
            .collect();
        for h in handles {
            h.join().unwrap();
        }

        let entries = read_oplog(&tmp_path, "42").unwrap();
        // Every thread's entry must be present and intact. If a
        // torn header had landed, the next byte would be interpreted
        // as a body_len u32 = "YOLG" ≈ 1.2 GiB, tripping
        // MAX_PLAUSIBLE_BODY_LEN — `read_oplog` would either return a
        // hard error or zero entries depending on where the cracking
        // happened.
        assert_eq!(
            entries.len(),
            THREADS,
            "every entry should survive the race; got {entries:?}"
        );
    }

    #[test]
    fn append_rejects_file_with_torn_header() {
        // Legacy / sabotaged on-disk state: a file exists with
        // fewer than HEADER_LEN bytes (i.e. a writer that didn't
        // take our lock crashed mid-header). The next appender must
        // refuse to write rather than produce a tail-only entry
        // that `read_oplog` will reject anyway.
        let tmp = tempfile::tempdir().unwrap();
        let dir = tmp.path().join("oplog");
        std::fs::create_dir_all(&dir).unwrap();
        let path = oplog_path_for_name(tmp.path(), "7");
        // Write 4 bytes (less than HEADER_LEN = 8) to simulate a
        // torn header.
        std::fs::write(&path, b"YOLG").unwrap();

        let err =
            append_entry(tmp.path(), "7", "note.md", &entry(b"after-torn-header")).unwrap_err();
        assert_eq!(err.kind(), io::ErrorKind::InvalidData);
        assert!(err.to_string().contains("truncated header"));
    }

    use std::sync::Arc;

    // --- v2 format + annotations (O-1 #539) ---------------------------

    /// Annotated entry wrapping `inner` with `anns`, hashes as given.
    fn annotated_entry(
        timestamp_ms: i64,
        inner_kind: OpKind,
        inner_payload: &[u8],
        anns: &[OpAnnotation],
        hash_before: &str,
        hash_after: &str,
    ) -> OpLogEntry {
        OpLogEntry {
            timestamp_ms,
            user_actor_id: "t".into(),
            op_kind: OpKind::Annotated,
            content_hash_before: hash_before.to_string(),
            content_hash_after: hash_after.to_string(),
            payload_bytes: encode_annotated(inner_kind, inner_payload, anns),
        }
    }

    /// A pure PathChanged marker at `tail_hash` (the marker hash rule).
    fn marker_entry(timestamp_ms: i64, from: &str, to: &str, tail_hash: &str) -> OpLogEntry {
        annotated_entry(
            timestamp_ms,
            OpKind::EditBatch,
            &encode_edit_batch(&[]),
            &[OpAnnotation::PathChanged {
                from: from.into(),
                to: to.into(),
            }],
            tail_hash,
            tail_hash,
        )
    }

    fn all_annotations() -> Vec<OpAnnotation> {
        vec![
            OpAnnotation::SetProperty {
                key: "status".into(),
                value_json: "\"final\"".into(),
            },
            OpAnnotation::RemoveProperty {
                key: "draft".into(),
            },
            OpAnnotation::ToggleTask {
                ordinal: 3,
                new_status: '✓', // multi-byte scalar exercises the 1-char rule
            },
            OpAnnotation::FrontmatterReplace,
            OpAnnotation::PathChanged {
                from: "a/old.md".into(),
                to: "b/new.md".into(),
            },
        ]
    }

    #[test]
    fn annotated_round_trips_every_tag() {
        let inner = encode_edit_batch(&[EditOp::Insert {
            pos: 0,
            text: "x".into(),
        }]);
        let anns = all_annotations();
        let payload = encode_annotated(OpKind::EditBatch, &inner, &anns);
        let (kind, inner_out, anns_out) = decode_annotated(&payload).unwrap();
        assert_eq!(kind, OpKind::EditBatch);
        assert_eq!(inner_out, inner);
        assert_eq!(anns_out, anns);

        // Snapshot inner too.
        let payload = encode_annotated(OpKind::WholeFileReplace, b"full contents", &anns);
        let (kind, inner_out, anns_out) = decode_annotated(&payload).unwrap();
        assert_eq!(kind, OpKind::WholeFileReplace);
        assert_eq!(inner_out, b"full contents");
        assert_eq!(anns_out, anns);
    }

    #[test]
    fn annotated_unknown_tag_is_skipped_and_inner_still_applies() {
        // Hand-assemble: known inner batch, then ann tag 250 (unknown)
        // followed by a known PathChanged — the decoder must skip the
        // unknown by length and still return the known one.
        let inner = encode_edit_batch(&[EditOp::Insert {
            pos: 5,
            text: "!".into(),
        }]);
        let known = OpAnnotation::PathChanged {
            from: "x.md".into(),
            to: "y.md".into(),
        };
        let known_body = known.body_json();
        let mut payload = Vec::new();
        payload.push(OpKind::EditBatch.as_u8());
        payload.extend_from_slice(&(inner.len() as u32).to_le_bytes());
        payload.extend_from_slice(&inner);
        payload.extend_from_slice(&2u16.to_le_bytes());
        payload.push(250); // future tag this build doesn't know
        payload.extend_from_slice(&(11u32).to_le_bytes());
        payload.extend_from_slice(b"future-body");
        payload.push(5); // ANN_TAG_PATH_CHANGED
        payload.extend_from_slice(&(known_body.len() as u32).to_le_bytes());
        payload.extend_from_slice(known_body.as_bytes());

        let (kind, inner_out, anns) = decode_annotated(&payload).unwrap();
        assert_eq!(kind, OpKind::EditBatch);
        assert_eq!(inner_out, inner);
        assert_eq!(anns, vec![known]);

        // And the wrapped batch still replays.
        let old = "hello";
        let entries = vec![
            snapshot_entry(old),
            OpLogEntry {
                timestamp_ms: 2,
                user_actor_id: "t".into(),
                op_kind: OpKind::Annotated,
                content_hash_before: content_hash(old.as_bytes()),
                content_hash_after: content_hash(b"hello!"),
                payload_bytes: payload,
            },
        ];
        assert_eq!(reconstruct_at_tail(&entries).unwrap(), "hello!");
    }

    #[test]
    fn annotated_truncation_and_corruption_are_descriptive_errors() {
        let payload = encode_annotated(
            OpKind::EditBatch,
            &encode_edit_batch(&[]),
            &all_annotations(),
        );
        // Every proper prefix must fail cleanly (or — for the empty
        // annotations boundary — decode to fewer annotations is NOT
        // acceptable either: the count field promises them).
        for cut in 0..payload.len() {
            let truncated = &payload[..cut];
            assert!(
                decode_annotated(truncated).is_err(),
                "truncation at {cut} must be an error, not a partial decode"
            );
        }
        // Malformed KNOWN-tag body is corruption, not a skip.
        let mut bad = Vec::new();
        bad.push(OpKind::EditBatch.as_u8());
        bad.extend_from_slice(&0u32.to_le_bytes());
        bad.extend_from_slice(&1u16.to_le_bytes());
        bad.push(1); // SetProperty
        bad.extend_from_slice(&(9u32).to_le_bytes());
        bad.extend_from_slice(b"not json!");
        let err = decode_annotated(&bad).unwrap_err();
        assert!(err.contains("malformed JSON"), "got: {err}");

        // Inner kind must be a save shape.
        let mut bad_inner = Vec::new();
        bad_inner.push(3); // CanvasApply — not wrappable
        bad_inner.extend_from_slice(&0u32.to_le_bytes());
        bad_inner.extend_from_slice(&0u16.to_le_bytes());
        let err = decode_annotated(&bad_inner).unwrap_err();
        assert!(err.contains("inner kind"), "got: {err}");
    }

    #[test]
    fn reconstruct_uses_annotated_snapshot_as_anchor() {
        // The anchor search must see through the kind-4 wrapper: an
        // annotated snapshot after a bare one wins as the replay seed.
        let v2 = "fresh\n";
        let entries = vec![
            snapshot_entry("old\n"),
            batch_entry("old\n", "older\n"),
            annotated_entry(
                3,
                OpKind::WholeFileReplace,
                v2.as_bytes(),
                &[OpAnnotation::FrontmatterReplace],
                &content_hash(b"older\n"),
                &content_hash(v2.as_bytes()),
            ),
            batch_entry(v2, "fresh!\n"),
        ];
        assert_eq!(reconstruct_at_tail(&entries).unwrap(), "fresh!\n");
    }

    #[test]
    fn pure_marker_is_replay_noop_and_identity_axiom_holds() {
        let a = "alpha\n";
        let b = "alpha\nbeta\n";
        let c = "alpha\nbeta\ngamma\n";
        let entries = vec![
            snapshot_entry(a),
            batch_entry(a, b),
            marker_entry(3, "old.md", "new.md", &content_hash(b.as_bytes())),
            batch_entry(b, c),
        ];
        assert_eq!(reconstruct_at_tail(&entries).unwrap(), c);
        // Identity axiom: every hash_after prefix-reconstructs to bytes
        // whose blake3 IS that hash — including the marker's.
        for i in 0..entries.len() {
            let prefix = reconstruct_at_tail(&entries[..=i]).unwrap();
            assert_eq!(
                content_hash(prefix.as_bytes()),
                entries[i].content_hash_after,
                "identity axiom broken at entry {i}"
            );
        }
    }

    #[test]
    fn canvas_apply_entries_are_skipped_in_replay() {
        let a = "{}\n";
        let b = "{\"nodes\":[]}\n";
        let canvas = OpLogEntry {
            timestamp_ms: 3,
            user_actor_id: "t".into(),
            op_kind: OpKind::CanvasApply,
            content_hash_before: content_hash(a.as_bytes()),
            content_hash_after: content_hash(b.as_bytes()),
            payload_bytes: br#"{"name":"move"}"#.to_vec(),
        };
        let entries = vec![snapshot_entry(a), batch_entry(a, b), canvas];
        assert_eq!(reconstruct_at_tail(&entries).unwrap(), b);
    }

    #[test]
    fn try_create_log_writes_v2_header_and_detects_existing() {
        let tmp = tempfile::tempdir().unwrap();
        assert!(try_create_log(tmp.path(), "abc123", "notes/a.md").unwrap());
        assert!(!try_create_log(tmp.path(), "abc123", "other.md").unwrap());
        let (header, entries) = read_oplog_with_header(tmp.path(), "abc123").unwrap();
        assert_eq!(header.version, FORMAT_VERSION_V2);
        assert_eq!(header.created_path.as_deref(), Some("notes/a.md"));
        assert_eq!(header.generation, 0);
        assert!(entries.is_empty());
    }

    #[test]
    fn concurrent_try_create_log_creates_exactly_once() {
        use std::sync::Barrier;
        use std::sync::atomic::{AtomicU32, Ordering};
        const THREADS: usize = 8;
        let tmp = tempfile::tempdir().unwrap();
        let tmp_path = tmp.path().to_path_buf();
        let barrier = Arc::new(Barrier::new(THREADS));
        let created = Arc::new(AtomicU32::new(0));
        let handles: Vec<_> = (0..THREADS)
            .map(|_| {
                let p = tmp_path.clone();
                let b = Arc::clone(&barrier);
                let c = Arc::clone(&created);
                std::thread::spawn(move || {
                    b.wait();
                    if try_create_log(&p, "raced", "race.md").unwrap() {
                        c.fetch_add(1, Ordering::SeqCst);
                    }
                })
            })
            .collect();
        for h in handles {
            h.join().unwrap();
        }
        assert_eq!(created.load(Ordering::SeqCst), 1);
        // And the winner's header is intact.
        let (header, _) = read_oplog_with_header(&tmp_path, "raced").unwrap();
        assert_eq!(header.created_path.as_deref(), Some("race.md"));
    }

    #[test]
    fn append_creates_v2_header_and_returns_post_append_length() {
        let tmp = tempfile::tempdir().unwrap();
        let len1 = append_entry(tmp.path(), "fresh", "notes/n.md", &snapshot_entry("a\n")).unwrap();
        let on_disk = std::fs::metadata(oplog_path_for_name(tmp.path(), "fresh"))
            .unwrap()
            .len();
        assert_eq!(len1, on_disk, "returned length must be the real file size");
        let len2 = append_entry(
            tmp.path(),
            "fresh",
            "notes/n.md",
            &batch_entry("a\n", "ab\n"),
        )
        .unwrap();
        assert!(len2 > len1);
        let (header, entries) = read_oplog_with_header(tmp.path(), "fresh").unwrap();
        assert_eq!(header.version, FORMAT_VERSION_V2);
        assert_eq!(header.created_path.as_deref(), Some("notes/n.md"));
        assert_eq!(entries.len(), 2);
        assert_eq!(reconstruct_at_tail(&entries).unwrap(), "ab\n");
    }

    #[test]
    fn append_preserves_v1_header_no_eager_migration() {
        let tmp = tempfile::tempdir().unwrap();
        write_v1_log_for_tests(tmp.path(), "42", &[snapshot_entry("legacy\n")]);
        append_entry(
            tmp.path(),
            "42",
            "ignored.md",
            &batch_entry("legacy\n", "legacy!\n"),
        )
        .unwrap();
        let (header, entries) = read_oplog_with_header(tmp.path(), "42").unwrap();
        assert_eq!(
            header.version, FORMAT_VERSION_V1,
            "no eager v1→v2 migration"
        );
        assert_eq!(header.created_path, None);
        assert_eq!(entries.len(), 2);
        assert_eq!(reconstruct_at_tail(&entries).unwrap(), "legacy!\n");
    }

    #[test]
    fn torn_v2_header_extension_is_a_hard_error_for_read_and_append() {
        let tmp = tempfile::tempdir().unwrap();
        let dir = tmp.path().join("oplog");
        std::fs::create_dir_all(&dir).unwrap();
        let path = oplog_path_for_name(tmp.path(), "torn");
        // Fixed header promises v2, path_len says 20 bytes, but the
        // file ends after 4 path bytes.
        let mut bytes = Vec::new();
        bytes.extend_from_slice(&MAGIC);
        bytes.push(FORMAT_VERSION_V2);
        bytes.extend_from_slice(&[0, 0, 0]);
        bytes.extend_from_slice(&20u16.to_le_bytes());
        bytes.extend_from_slice(b"note");
        std::fs::write(&path, bytes).unwrap();

        let read_err = read_oplog(tmp.path(), "torn").unwrap_err();
        assert_eq!(read_err.kind(), io::ErrorKind::InvalidData);
        assert!(read_err.to_string().contains("torn v2 header"));

        let append_err =
            append_entry(tmp.path(), "torn", "note.md", &snapshot_entry("x")).unwrap_err();
        assert_eq!(append_err.kind(), io::ErrorKind::InvalidData);
    }

    #[test]
    fn concurrent_appends_and_markers_preserve_identity_axiom() {
        // Adversarial-review regression: the marker's tail read and its
        // append must be ONE lock-held operation. If a concurrent
        // writer could slip an entry between "observe tail" and
        // "append marker", the marker would carry a stale hash and its
        // prefix reconstruction would no longer hash to its own
        // hash_after. Writers here append self-anchoring snapshots
        // (their hash_after is their own payload's hash), so the axiom
        // must hold for EVERY entry in any interleaving — a stale
        // marker is the only way it can break.
        use std::sync::Barrier;
        const WRITERS: usize = 4;
        const MARKERS: usize = 3;
        const ROUNDS: usize = 12;

        let tmp = tempfile::tempdir().unwrap();
        let tmp_path = tmp.path().to_path_buf();
        assert!(try_create_log(&tmp_path, "raced", "race.md").unwrap());
        append_entry(&tmp_path, "raced", "race.md", &snapshot_entry("seed\n")).unwrap();

        let barrier = Arc::new(Barrier::new(WRITERS + MARKERS));
        let mut handles = Vec::new();
        for w in 0..WRITERS {
            let p = tmp_path.clone();
            let b = Arc::clone(&barrier);
            handles.push(std::thread::spawn(move || {
                b.wait();
                for r in 0..ROUNDS {
                    let content = format!("writer {w} round {r}\n");
                    let mut e = snapshot_entry(&content);
                    e.timestamp_ms = (w * ROUNDS + r) as i64;
                    append_entry(&p, "raced", "race.md", &e).unwrap();
                }
            }));
        }
        for m in 0..MARKERS {
            let p = tmp_path.clone();
            let b = Arc::clone(&barrier);
            handles.push(std::thread::spawn(move || {
                b.wait();
                for r in 0..ROUNDS {
                    append_path_changed_marker(
                        &p,
                        "raced",
                        "race.md",
                        "race2.md",
                        "marker",
                        (m * ROUNDS + r) as i64,
                    )
                    .unwrap();
                }
            }));
        }
        for h in handles {
            h.join().unwrap();
        }

        let entries = read_oplog(&tmp_path, "raced").unwrap();
        assert_eq!(entries.len(), 1 + WRITERS * ROUNDS + MARKERS * ROUNDS);
        for i in 0..entries.len() {
            let prefix = reconstruct_at_tail(&entries[..=i]).unwrap();
            assert_eq!(
                content_hash(prefix.as_bytes()),
                entries[i].content_hash_after,
                "identity axiom broken at entry {i} (kind {:?})",
                entries[i].op_kind
            );
        }
    }

    #[test]
    fn oversized_created_path_degrades_to_no_path_record() {
        // Codoki (PR #790): a path longer than the u16 length prefix
        // must not wrap-truncate the header. It is written as an empty
        // record instead, reads back as None, and the entry stream
        // stays perfectly aligned.
        let tmp = tempfile::tempdir().unwrap();
        let huge_path = format!("dir/{}.md", "x".repeat(70_000));
        assert!(try_create_log(tmp.path(), "huge", &huge_path).unwrap());
        append_entry(tmp.path(), "huge", &huge_path, &snapshot_entry("body\n")).unwrap();
        let (header, entries) = read_oplog_with_header(tmp.path(), "huge").unwrap();
        assert_eq!(header.version, FORMAT_VERSION_V2);
        assert_eq!(
            header.created_path, None,
            "an unwritable path degrades to no record, never truncation"
        );
        assert_eq!(entries.len(), 1);
        assert_eq!(reconstruct_at_tail(&entries).unwrap(), "body\n");
    }

    #[test]
    fn frame_size_matches_frame_entry_exactly() {
        // The arithmetic size (used by the O-2 size fold, Codoki PR
        // #791) must equal the built frame byte-for-byte.
        for entry in v2_fixture_entries()
            .iter()
            .chain(v1_fixture_entries().iter())
        {
            assert_eq!(frame_size(entry), frame_entry(entry).len() as u64);
        }
    }

    #[test]
    fn marker_on_missing_or_empty_log_is_a_noop() {
        let tmp = tempfile::tempdir().unwrap();
        // Missing file → None.
        assert_eq!(
            append_path_changed_marker(tmp.path(), "ghost", "a.md", "b.md", "t", 1).unwrap(),
            None
        );
        // Header-only log (no entries) → None.
        assert!(try_create_log(tmp.path(), "hdr", "a.md").unwrap());
        assert_eq!(
            append_path_changed_marker(tmp.path(), "hdr", "a.md", "b.md", "t", 1).unwrap(),
            None
        );
        assert!(read_oplog(tmp.path(), "hdr").unwrap().is_empty());
    }

    // --- Wire-format fixtures (checked in, not generated) -------------
    //
    // The fixture BYTES are committed under tests/fixtures/oplog/ and
    // pin the formats both ways: the reader must parse them exactly,
    // and the writer must reproduce them byte-for-byte from the same
    // logical entries. Regenerate (after a deliberate, versioned format
    // change only!) with:
    //   SLATE_REGEN_OPLOG_FIXTURES=1 cargo test -p slate-core --lib \
    //     oplog::tests::regenerate_wire_fixtures

    fn v1_fixture_entries() -> Vec<OpLogEntry> {
        let old = "alpha\nbeta\n";
        let new = "alpha\nBETA\n";
        vec![
            OpLogEntry {
                timestamp_ms: 1_700_000_000_000,
                user_actor_id: "fixture".into(),
                op_kind: OpKind::WholeFileReplace,
                content_hash_before: String::new(),
                content_hash_after: content_hash(old.as_bytes()),
                payload_bytes: old.as_bytes().to_vec(),
            },
            OpLogEntry {
                timestamp_ms: 1_700_000_000_001,
                user_actor_id: "fixture".into(),
                op_kind: OpKind::EditBatch,
                content_hash_before: content_hash(old.as_bytes()),
                content_hash_after: content_hash(new.as_bytes()),
                payload_bytes: encode_edit_batch(&crate::diff::diff_to_ops(old, new)),
            },
        ]
    }

    /// v2 fixture: every current kind — bare snapshot, annotated batch,
    /// canvas record, pure marker — with fixed timestamps.
    fn v2_fixture_entries() -> Vec<OpLogEntry> {
        let v1 = "# Pinned\n";
        let v2 = "# Pinned\nstatus: final\n";
        let v3 = "# Pinned\nstatus: final\nmore\n";
        let mut entries = vec![
            OpLogEntry {
                timestamp_ms: 1_700_000_000_000,
                user_actor_id: "fixture".into(),
                op_kind: OpKind::WholeFileReplace,
                content_hash_before: String::new(),
                content_hash_after: content_hash(v1.as_bytes()),
                payload_bytes: v1.as_bytes().to_vec(),
            },
            annotated_entry(
                1_700_000_000_001,
                OpKind::EditBatch,
                &encode_edit_batch(&crate::diff::diff_to_ops(v1, v2)),
                &[OpAnnotation::SetProperty {
                    key: "status".into(),
                    value_json: "\"final\"".into(),
                }],
                &content_hash(v1.as_bytes()),
                &content_hash(v2.as_bytes()),
            ),
            OpLogEntry {
                timestamp_ms: 1_700_000_000_002,
                user_actor_id: "fixture".into(),
                op_kind: OpKind::EditBatch,
                content_hash_before: content_hash(v2.as_bytes()),
                content_hash_after: content_hash(v3.as_bytes()),
                payload_bytes: encode_edit_batch(&crate::diff::diff_to_ops(v2, v3)),
            },
            OpLogEntry {
                timestamp_ms: 1_700_000_000_003,
                user_actor_id: "fixture".into(),
                op_kind: OpKind::CanvasApply,
                content_hash_before: content_hash(v2.as_bytes()),
                content_hash_after: content_hash(v3.as_bytes()),
                payload_bytes: br#"{"name":"pinned"}"#.to_vec(),
            },
        ];
        entries.push(marker_entry(
            1_700_000_000_004,
            "notes/old-pinned.md",
            "notes/pinned.md",
            &content_hash(v3.as_bytes()),
        ));
        // Fixture entries must all carry the fixture actor.
        for e in &mut entries {
            e.user_actor_id = "fixture".into();
        }
        entries
    }

    const V1_FIXTURE: &[u8] = include_bytes!("../tests/fixtures/oplog/v1_two_entries.oplog");
    const V2_FIXTURE: &[u8] = include_bytes!("../tests/fixtures/oplog/v2_annotated.oplog");

    /// Env-gated regenerator (a no-op test in normal runs). Writes the
    /// fixture files from the logical entries above.
    #[test]
    fn regenerate_wire_fixtures() {
        if std::env::var("SLATE_REGEN_OPLOG_FIXTURES").as_deref() != Ok("1") {
            return;
        }
        let out_dir = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/oplog");
        std::fs::create_dir_all(&out_dir).unwrap();

        let tmp = tempfile::tempdir().unwrap();
        write_v1_log_for_tests(tmp.path(), "v1", &v1_fixture_entries());
        std::fs::copy(
            oplog_path_for_name(tmp.path(), "v1"),
            out_dir.join("v1_two_entries.oplog"),
        )
        .unwrap();

        assert!(try_create_log(tmp.path(), "v2", "notes/old-pinned.md").unwrap());
        for entry in v2_fixture_entries() {
            append_entry(tmp.path(), "v2", "notes/old-pinned.md", &entry).unwrap();
        }
        std::fs::copy(
            oplog_path_for_name(tmp.path(), "v2"),
            out_dir.join("v2_annotated.oplog"),
        )
        .unwrap();
    }

    #[test]
    fn v1_fixture_bytes_are_pinned_both_ways() {
        // Reader pin: the committed bytes parse to exactly the logical
        // entries.
        let tmp = tempfile::tempdir().unwrap();
        let dir = tmp.path().join("oplog");
        std::fs::create_dir_all(&dir).unwrap();
        std::fs::write(oplog_path_for_name(tmp.path(), "v1"), V1_FIXTURE).unwrap();
        let (header, entries) = read_oplog_with_header(tmp.path(), "v1").unwrap();
        assert_eq!(header.version, FORMAT_VERSION_V1);
        assert_eq!(header.created_path, None);
        assert_eq!(header.generation, 0);
        assert_eq!(entries, v1_fixture_entries());
        assert_eq!(reconstruct_at_tail(&entries).unwrap(), "alpha\nBETA\n");

        // Writer pin: the v1 test writer reproduces the bytes exactly.
        write_v1_log_for_tests(tmp.path(), "v1-again", &v1_fixture_entries());
        let rewritten = std::fs::read(oplog_path_for_name(tmp.path(), "v1-again")).unwrap();
        assert_eq!(rewritten, V1_FIXTURE, "v1 wire format drifted");
    }

    #[test]
    fn v2_fixture_bytes_are_pinned_both_ways() {
        let tmp = tempfile::tempdir().unwrap();
        let dir = tmp.path().join("oplog");
        std::fs::create_dir_all(&dir).unwrap();
        std::fs::write(oplog_path_for_name(tmp.path(), "v2"), V2_FIXTURE).unwrap();
        let (header, entries) = read_oplog_with_header(tmp.path(), "v2").unwrap();
        assert_eq!(header.version, FORMAT_VERSION_V2);
        assert_eq!(header.created_path.as_deref(), Some("notes/old-pinned.md"));
        assert_eq!(header.generation, 0);
        assert_eq!(entries, v2_fixture_entries());
        // Identity axiom over the whole fixture (marker included).
        for i in 0..entries.len() {
            let prefix = reconstruct_at_tail(&entries[..=i]).unwrap();
            assert_eq!(
                content_hash(prefix.as_bytes()),
                entries[i].content_hash_after,
                "identity axiom broken at fixture entry {i}"
            );
        }

        // Writer pin: creating + appending the same logical entries
        // reproduces the committed bytes exactly.
        assert!(try_create_log(tmp.path(), "v2-again", "notes/old-pinned.md").unwrap());
        for entry in v2_fixture_entries() {
            append_entry(tmp.path(), "v2-again", "notes/old-pinned.md", &entry).unwrap();
        }
        let rewritten = std::fs::read(oplog_path_for_name(tmp.path(), "v2-again")).unwrap();
        assert_eq!(rewritten, V2_FIXTURE, "v2 wire format drifted");
    }

    // --- census_oplog_v2_reconstruct (O-1 #539) ------------------------

    /// SplitMix64 — deterministic, replayable (same as the session
    /// censuses).
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
            100_000
        } else {
            2_500
        }
    }

    /// Random-document mutator: returns a new document derived from
    /// `doc` (may be identical — the caller skips writing then).
    fn mutate_doc(rng: &mut SplitMix64, doc: &str) -> String {
        const SNIPPETS: &[&str] = &[
            "hello\n",
            "中文段落\n",
            "- [ ] task\n",
            "x😀y",
            "line\r\n",
            "  \n",
            "# Heading\n",
            "",
        ];
        match rng.below(4) {
            0 => {
                // Insert a snippet at a char boundary.
                let boundaries: Vec<usize> = doc
                    .char_indices()
                    .map(|(i, _)| i)
                    .chain(std::iter::once(doc.len()))
                    .collect();
                let at = boundaries[rng.below(boundaries.len())];
                let snippet = SNIPPETS[rng.below(SNIPPETS.len())];
                format!("{}{}{}", &doc[..at], snippet, &doc[at..])
            }
            1 => {
                // Delete a random char range.
                let boundaries: Vec<usize> = doc
                    .char_indices()
                    .map(|(i, _)| i)
                    .chain(std::iter::once(doc.len()))
                    .collect();
                let a = boundaries[rng.below(boundaries.len())];
                let b = boundaries[rng.below(boundaries.len())];
                let (lo, hi) = if a <= b { (a, b) } else { (b, a) };
                format!("{}{}", &doc[..lo], &doc[hi..])
            }
            2 => String::new(), // clear
            _ => {
                // Replace wholesale with a fresh mix.
                let mut out = String::new();
                for _ in 0..rng.below(5) {
                    out.push_str(SNIPPETS[rng.below(SNIPPETS.len())]);
                }
                out
            }
        }
    }

    /// Random histories over every entry kind (bare/annotated
    /// snapshots+batches, pure markers, canvas records) reconstruct to
    /// a plain-String reference model, and every prefix satisfies the
    /// identity axiom. 100k histories at full scale
    /// (`SLATE_CENSUS_FULL=1`, the pre-push release run).
    #[test]
    fn census_oplog_v2_reconstruct() {
        let histories = census_scale();
        for seed in 0..histories {
            let mut rng = SplitMix64(seed.wrapping_mul(0x9E37_79B9).wrapping_add(17));
            let mut doc = String::new();
            let mut entries: Vec<OpLogEntry> = Vec::new();
            let steps = 1 + rng.below(8);
            let mut ts = 1_000;

            // First entry is always a snapshot (the session invariant:
            // first save of a file this session snapshots).
            let first = mutate_doc(&mut rng, &doc);
            entries.push(snapshot_entry(&first));
            doc = first;

            for _ in 0..steps {
                ts += 1;
                match rng.below(10) {
                    // Pure marker at the tail hash.
                    0 => entries.push(marker_entry(
                        ts,
                        "a.md",
                        "b.md",
                        &content_hash(doc.as_bytes()),
                    )),
                    // Canvas record duplicating the last transition
                    // shape (skipped in replay).
                    1 => entries.push(OpLogEntry {
                        timestamp_ms: ts,
                        user_actor_id: "t".into(),
                        op_kind: OpKind::CanvasApply,
                        content_hash_before: content_hash(doc.as_bytes()),
                        content_hash_after: content_hash(doc.as_bytes()),
                        payload_bytes: br#"{"name":"census"}"#.to_vec(),
                    }),
                    // Snapshot (sometimes annotated).
                    2 | 3 => {
                        let new = mutate_doc(&mut rng, &doc);
                        let hb = content_hash(doc.as_bytes());
                        let ha = content_hash(new.as_bytes());
                        if rng.below(2) == 0 {
                            entries.push(annotated_entry(
                                ts,
                                OpKind::WholeFileReplace,
                                new.as_bytes(),
                                &[all_annotations()[rng.below(5)].clone()],
                                &hb,
                                &ha,
                            ));
                        } else {
                            let mut e = snapshot_entry(&new);
                            e.timestamp_ms = ts;
                            e.content_hash_before = hb;
                            entries.push(e);
                        }
                        doc = new;
                    }
                    // Batch (sometimes annotated).
                    _ => {
                        let new = mutate_doc(&mut rng, &doc);
                        if new == doc {
                            continue; // identical saves write nothing
                        }
                        let payload = encode_edit_batch(&crate::diff::diff_to_ops(&doc, &new));
                        let hb = content_hash(doc.as_bytes());
                        let ha = content_hash(new.as_bytes());
                        if rng.below(2) == 0 {
                            entries.push(annotated_entry(
                                ts,
                                OpKind::EditBatch,
                                &payload,
                                &[all_annotations()[rng.below(5)].clone()],
                                &hb,
                                &ha,
                            ));
                        } else {
                            entries.push(OpLogEntry {
                                timestamp_ms: ts,
                                user_actor_id: "t".into(),
                                op_kind: OpKind::EditBatch,
                                content_hash_before: hb,
                                content_hash_after: ha,
                                payload_bytes: payload,
                            });
                        }
                        doc = new;
                    }
                }
            }

            assert_eq!(
                reconstruct_at_tail(&entries).unwrap(),
                doc,
                "seed {seed}: tail reconstruction diverged from reference"
            );
            // Identity axiom over every prefix.
            for i in 0..entries.len() {
                let prefix = reconstruct_at_tail(&entries[..=i]).unwrap();
                assert_eq!(
                    content_hash(prefix.as_bytes()),
                    entries[i].content_hash_after,
                    "seed {seed}: identity axiom broken at entry {i}"
                );
            }

            // Round-trip through disk on a subsample (framing layer
            // participates without dominating runtime).
            if seed % 97 == 0 {
                let tmp = tempfile::tempdir().unwrap();
                for e in &entries {
                    append_entry(tmp.path(), "census", "census.md", e).unwrap();
                }
                let read_back = read_oplog(tmp.path(), "census").unwrap();
                assert_eq!(read_back, entries, "seed {seed}: disk round trip diverged");
            }
        }
    }
}
