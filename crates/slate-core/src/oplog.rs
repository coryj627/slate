// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Append-only binary save journal for vault notes.
//!
//! Each indexed note gets its own log at
//! `<cache_dir>/oplog/<file_id>.oplog`. Keying on `files.id` rather
//! than path means a rename — which keeps the same row in SQLite —
//! does not lose history.
//!
//! Two op kinds are recorded (#378 §7.1):
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
//!
//! [`reconstruct_at_tail`] materialises the current document by seeding
//! the last snapshot and replaying every later batch — the read side
//! for change-tracking / conflict consumers. Compaction *execution*
//! (see `SessionConfig::oplog_compaction_threshold_*`) is still a V1.x
//! concern; this module decides snapshot *cadence* against that
//! threshold but never rewrites or truncates a log.
//!
//! # Format
//!
//! ```text
//! File header (8 bytes, written when the file is first created):
//!   0..4   magic "YOLG"
//!   4      format_version = 1
//!   5..8   reserved (0 0 0)
//!
//! Entry (variable length, repeated to end of file):
//!   body_len:      u32 LE
//!   body (body_len bytes):
//!     timestamp_ms:     i64 LE
//!     op_kind:          u8 (1 = WholeFileReplace, 2 = EditBatch)
//!     actor_id_len:     u16 LE
//!     actor_id_bytes:   actor_id_len bytes (UTF-8)
//!     hash_before_len:  u16 LE
//!     hash_before:      hash_before_len bytes (ASCII hex)
//!     hash_after_len:   u16 LE
//!     hash_after:       hash_after_len bytes (ASCII hex)
//!     payload_len:      u32 LE
//!     payload:          payload_len bytes
//!   body_checksum: u32 LE (first 4 bytes of blake3(body) — torn-write canary)
//! ```
//!
//! All multi-byte integers are little-endian, fixed at write time so
//! a vault written on one platform is portable to another.
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
const FORMAT_VERSION: u8 = 1;
const HEADER_LEN: usize = 8;

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
/// lower one it would strand. 3..=8 are reserved for the semantic ops
/// (`SetProperty`, `RemoveProperty`, `InsertHeading`, `MoveListItem`, …)
/// that land later; `try_from_u8` returns `None` for them until then.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum OpKind {
    /// Whole-file replace: the payload is the full new file contents.
    /// Snapshot / replay anchor.
    WholeFileReplace,
    /// A single save's fine-grained edit ops, encoded as a batch (see
    /// [`encode_edit_batch`] / [`decode_edit_batch`]).
    EditBatch,
}

impl OpKind {
    fn as_u8(self) -> u8 {
        match self {
            OpKind::WholeFileReplace => 1,
            OpKind::EditBatch => 2,
        }
    }

    fn try_from_u8(v: u8) -> Option<Self> {
        match v {
            1 => Some(OpKind::WholeFileReplace),
            2 => Some(OpKind::EditBatch),
            _ => None,
        }
    }
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

/// Materialise the document represented by `entries` (the in-order result
/// of [`read_oplog`]): seed the last [`OpKind::WholeFileReplace`]
/// snapshot, then replay every later [`OpKind::EditBatch`] in order.
///
/// Within a batch, ops apply in **descending old-offset order** so an
/// earlier edit never shifts the offsets a later edit in the same batch
/// was computed against (the batch's ops are all in one old-content
/// space; disjoint and document-ordered from the diff). An op whose range
/// is out of bounds for the running buffer is a corruption signal →
/// typed error; we do **not** lean on `TextBuffer`'s silent clamping,
/// which would mask it. An empty log reconstructs to `""`.
pub fn reconstruct_at_tail(entries: &[OpLogEntry]) -> Result<String, String> {
    let Some(snapshot_idx) = entries
        .iter()
        .rposition(|e| e.op_kind == OpKind::WholeFileReplace)
    else {
        if entries.is_empty() {
            return Ok(String::new());
        }
        return Err("op log has edit batches but no snapshot anchor".to_string());
    };

    let seed = |bytes: &[u8]| -> Result<crate::text_buffer::TextBuffer, String> {
        std::str::from_utf8(bytes)
            .map(crate::text_buffer::TextBuffer::from_str)
            .map_err(|_| "snapshot payload is not valid UTF-8".to_string())
    };

    let mut buf = seed(&entries[snapshot_idx].payload_bytes)?;
    for entry in &entries[snapshot_idx + 1..] {
        match entry.op_kind {
            // Defensive: a later snapshot (shouldn't occur after rposition
            // found the last one) re-seeds.
            OpKind::WholeFileReplace => buf = seed(&entry.payload_bytes)?,
            OpKind::EditBatch => {
                let mut ops = decode_edit_batch(&entry.payload_bytes)?;
                ops.sort_by_key(|op| std::cmp::Reverse(op.old_offset()));
                for op in &ops {
                    apply_op(&mut buf, op)?;
                }
            }
        }
    }
    Ok(buf.to_string())
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

/// Path to the oplog file for `file_id` under the given cache dir.
/// The directory itself is created lazily by `append_entry`.
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

pub fn oplog_path(cache_dir: &Path, file_id: i64) -> PathBuf {
    cache_dir.join("oplog").join(format!("{file_id}.oplog"))
}

/// Append a single entry to `<cache_dir>/oplog/<file_id>.oplog`.
///
/// Creates the directory and writes the file header on first use.
/// Returns once the entry's bytes are durably on disk
/// (`sync_data`-flushed).
pub fn append_entry(cache_dir: &Path, file_id: i64, entry: &OpLogEntry) -> io::Result<()> {
    let dir = cache_dir.join("oplog");
    fs::create_dir_all(&dir)?;
    let path = oplog_path(cache_dir, file_id);

    // First-write race (PR #105 Codoki feedback, refined here).
    //
    // The earlier `create_new(true)` shape is *almost* race-free: only
    // one caller wins the create. But the dirent becomes visible to
    // every other thread *immediately* — before the create-winner has
    // written the 8-byte header. A loser arriving in that window
    // opens the file in pure-append mode and writes its entry body
    // ahead of the header, leaving the file with the loser's
    // `body_len` u32 where MAGIC should be. `read_oplog` then
    // rejects the file with "bad magic" and every previously committed
    // entry is unrecoverable.
    //
    // Fix: hold an OS-level exclusive lock across "decide whether
    // to write the header" *and* "write our entry." Whoever gets the
    // lock first sees the empty file and writes the header before
    // appending. Subsequent lock-holders see a non-empty file with a
    // valid header already in place and just append. The lock uses
    // OFD locks on Linux (per fd, cross-thread-safe) and `LockFileEx`
    // on Windows — `File::lock` papers over the platform difference.
    // Lock is released when `file` is dropped at the end of this
    // function.
    let mut file = OpenOptions::new().create(true).append(true).open(&path)?;
    file.lock()?;

    let len = file.metadata()?.len();
    let is_new_file = len == 0;
    if !is_new_file && (len as usize) < HEADER_LEN {
        // A previous writer crashed between create and header write
        // (only reachable from pre-fix data, or from a different
        // writer that doesn't take the lock). Surface it as a hard
        // error rather than producing a tail-only entry that
        // `read_oplog` will reject anyway.
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            format!("oplog {path:?}: torn header (size {len})"),
        ));
    }
    if is_new_file {
        let mut header = [0u8; HEADER_LEN];
        header[0..4].copy_from_slice(&MAGIC);
        header[4] = FORMAT_VERSION;
        // bytes 5..8 are reserved and already zero.
        file.write_all(&header)?;
    }

    let body = serialize_body(entry);
    let body_len = body.len() as u32;
    let checksum = body_checksum(&body);

    // Assemble the entire framed record in memory and write it with a
    // single syscall. Under O_APPEND, the kernel is responsible for
    // making each write append atomically; using one write() (rather
    // than several) keeps individual entries from interleaving with
    // concurrent writers up to the OS's atomic-append guarantee. The
    // exclusive lock above is the load-bearing serializer; this
    // single-syscall pattern is belt-and-braces.
    let mut framed = Vec::with_capacity(4 + body.len() + 4);
    framed.extend_from_slice(&body_len.to_le_bytes());
    framed.extend_from_slice(&body);
    framed.extend_from_slice(&checksum.to_le_bytes());
    file.write_all(&framed)?;
    file.sync_data()?;
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
    Ok(())
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
fn fsync_dir(dir: &Path) -> io::Result<()> {
    fs::File::open(dir)?.sync_all()
}

#[cfg(not(unix))]
fn fsync_dir(_dir: &Path) -> io::Result<()> {
    Ok(())
}

/// Read all well-formed entries from `<cache_dir>/oplog/<file_id>.oplog`.
///
/// A missing file is normal — it just means no save has been recorded
/// yet — and returns `Ok(Vec::new())`. A header mismatch (wrong magic
/// or unsupported version) is fatal because we don't know how to
/// interpret the file at all. Per-entry corruption (short read,
/// checksum mismatch, unparseable body) stops the walk at that point
/// and returns the prefix that read cleanly; a warning is printed to
/// stderr.
pub fn read_oplog(cache_dir: &Path, file_id: i64) -> io::Result<Vec<OpLogEntry>> {
    let path = oplog_path(cache_dir, file_id);
    let mut file = match fs::File::open(&path) {
        Ok(f) => f,
        Err(e) if e.kind() == io::ErrorKind::NotFound => return Ok(Vec::new()),
        Err(e) => return Err(e),
    };

    let mut header = [0u8; HEADER_LEN];
    match read_fully(&mut file, &mut header)? {
        ReadOutcome::Full => {}
        ReadOutcome::Eof => return Ok(Vec::new()),
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
    if header[4] != FORMAT_VERSION {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            format!(
                "oplog {path:?}: unsupported format version {}, this build understands {FORMAT_VERSION}",
                header[4]
            ),
        ));
    }

    let mut entries: Vec<OpLogEntry> = Vec::new();
    loop {
        let mut len_buf = [0u8; 4];
        match read_fully(&mut file, &mut len_buf)? {
            ReadOutcome::Eof => break,
            ReadOutcome::Partial => {
                eprintln!("oplog {path:?}: trailing torn body-length, skipping");
                break;
            }
            ReadOutcome::Full => {}
        }
        let body_len = u32::from_le_bytes(len_buf) as usize;
        if body_len > MAX_PLAUSIBLE_BODY_LEN {
            eprintln!(
                "oplog {path:?}: implausible body_len={body_len} (max {MAX_PLAUSIBLE_BODY_LEN}), \
                 skipping rest"
            );
            break;
        }

        let mut body = vec![0u8; body_len];
        match read_fully(&mut file, &mut body)? {
            ReadOutcome::Full => {}
            _ => {
                eprintln!("oplog {path:?}: trailing torn body, skipping");
                break;
            }
        }

        let mut sum_buf = [0u8; 4];
        match read_fully(&mut file, &mut sum_buf)? {
            ReadOutcome::Full => {}
            _ => {
                eprintln!("oplog {path:?}: trailing missing checksum, skipping");
                break;
            }
        }
        let recorded = u32::from_le_bytes(sum_buf);
        if body_checksum(&body) != recorded {
            eprintln!("oplog {path:?}: checksum mismatch on trailing entry, skipping");
            break;
        }

        match parse_body(&body) {
            Ok(entry) => entries.push(entry),
            Err(e) => {
                eprintln!("oplog {path:?}: malformed entry body ({e}), skipping rest");
                break;
            }
        }
    }
    Ok(entries)
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
        append_entry(tmp.path(), 1, &snapshot_entry("a\nb\nc\n")).unwrap();
        append_entry(tmp.path(), 1, &batch_entry("a\nb\nc\n", "a\nB\nc\n")).unwrap();
        let entries = read_oplog(tmp.path(), 1).unwrap();
        assert_eq!(entries.len(), 2);
        assert_eq!(entries[1].op_kind, OpKind::EditBatch);
        assert_eq!(reconstruct_at_tail(&entries).unwrap(), "a\nB\nc\n");
    }

    #[test]
    fn corrupt_trailing_edit_batch_returns_well_formed_prefix() {
        // Crash-safety is preserved for the new kind: a torn EditBatch
        // trailing entry leaves the snapshot + earlier batch intact.
        let tmp = tempfile::tempdir().unwrap();
        append_entry(tmp.path(), 1, &snapshot_entry("base\n")).unwrap();
        append_entry(tmp.path(), 1, &batch_entry("base\n", "BASE\n")).unwrap();
        // Append a framed-looking EditBatch with a bogus oversized body_len.
        let path = oplog_path(tmp.path(), 1);
        let mut handle = OpenOptions::new().append(true).open(&path).unwrap();
        let bogus_len: u32 = (MAX_PLAUSIBLE_BODY_LEN as u32).saturating_add(1);
        handle.write_all(&bogus_len.to_le_bytes()).unwrap();
        handle.write_all(b"junk").unwrap();
        let entries = read_oplog(tmp.path(), 1).unwrap();
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
        append_entry(tmp.path(), 1, &snapshot_entry("base\n")).unwrap();
        append_entry(tmp.path(), 1, &batch_entry("base\n", "BASE\n")).unwrap();
        forge_raw_entry(&oplog_path(tmp.path(), 1), 7, b"future");
        append_entry(tmp.path(), 1, &batch_entry("BASE\n", "BASE!\n")).unwrap();

        let entries = read_oplog(tmp.path(), 1).unwrap();
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
        let entries = read_oplog(tmp.path(), 1).unwrap();
        assert!(entries.is_empty());
    }

    #[test]
    fn append_then_read_round_trips_single_entry() {
        let tmp = tempfile::tempdir().unwrap();
        let original = entry(b"hello vault");
        append_entry(tmp.path(), 42, &original).unwrap();

        let entries = read_oplog(tmp.path(), 42).unwrap();
        assert_eq!(entries, vec![original]);
    }

    #[test]
    fn multiple_entries_preserve_order() {
        let tmp = tempfile::tempdir().unwrap();
        let a = entry(b"first");
        let b = entry(b"second");
        let c = entry(b"third");
        append_entry(tmp.path(), 7, &a).unwrap();
        append_entry(tmp.path(), 7, &b).unwrap();
        append_entry(tmp.path(), 7, &c).unwrap();

        let entries = read_oplog(tmp.path(), 7).unwrap();
        assert_eq!(entries, vec![a, b, c]);
    }

    #[test]
    fn corrupt_trailing_entry_returns_well_formed_prefix() {
        let tmp = tempfile::tempdir().unwrap();
        let a = entry(b"good-1");
        let b = entry(b"good-2");
        append_entry(tmp.path(), 5, &a).unwrap();
        append_entry(tmp.path(), 5, &b).unwrap();

        // Append junk that masquerades as the start of an entry: a
        // huge `body_len` followed by some bytes. The reader should
        // see the implausible length and stop, returning only [a, b].
        let path = oplog_path(tmp.path(), 5);
        let mut handle = OpenOptions::new().append(true).open(&path).unwrap();
        let bogus_len: u32 = (MAX_PLAUSIBLE_BODY_LEN as u32).saturating_add(1);
        handle.write_all(&bogus_len.to_le_bytes()).unwrap();
        handle.write_all(b"junk").unwrap();

        let entries = read_oplog(tmp.path(), 5).unwrap();
        assert_eq!(entries, vec![a, b]);
    }

    #[test]
    fn truncated_body_returns_prefix() {
        let tmp = tempfile::tempdir().unwrap();
        let a = entry(b"good");
        append_entry(tmp.path(), 9, &a).unwrap();

        // Write a body_len that promises 1000 more bytes, then only
        // write 10 → reader sees short body, stops.
        let path = oplog_path(tmp.path(), 9);
        let mut handle = OpenOptions::new().append(true).open(&path).unwrap();
        handle.write_all(&1_000u32.to_le_bytes()).unwrap();
        handle.write_all(&[0u8; 10]).unwrap();

        let entries = read_oplog(tmp.path(), 9).unwrap();
        assert_eq!(entries, vec![a]);
    }

    #[test]
    fn checksum_mismatch_on_trailing_entry_returns_prefix() {
        let tmp = tempfile::tempdir().unwrap();
        let a = entry(b"good");
        append_entry(tmp.path(), 11, &a).unwrap();

        // Write a well-framed but corrupt entry: valid body bytes,
        // but with a wrong checksum. The body parses, the checksum
        // doesn't match, so the reader rejects it.
        let body = serialize_body(&entry(b"would-have-been-fine"));
        let path = oplog_path(tmp.path(), 11);
        let mut handle = OpenOptions::new().append(true).open(&path).unwrap();
        handle
            .write_all(&(body.len() as u32).to_le_bytes())
            .unwrap();
        handle.write_all(&body).unwrap();
        handle.write_all(&0xDEAD_BEEF_u32.to_le_bytes()).unwrap();

        let entries = read_oplog(tmp.path(), 11).unwrap();
        assert_eq!(entries, vec![a]);
    }

    #[test]
    fn empty_file_after_header_is_well_formed() {
        // A file that contains only the header (e.g. crashed between
        // header write and first entry) reads as zero entries, not as
        // an error.
        let tmp = tempfile::tempdir().unwrap();
        let path = oplog_path(tmp.path(), 1);
        std::fs::create_dir_all(path.parent().unwrap()).unwrap();
        let mut header = [0u8; HEADER_LEN];
        header[0..4].copy_from_slice(&MAGIC);
        header[4] = FORMAT_VERSION;
        std::fs::write(&path, header).unwrap();

        let entries = read_oplog(tmp.path(), 1).unwrap();
        assert!(entries.is_empty());
    }

    #[test]
    fn bad_magic_is_a_hard_error() {
        let tmp = tempfile::tempdir().unwrap();
        let path = oplog_path(tmp.path(), 3);
        std::fs::create_dir_all(path.parent().unwrap()).unwrap();
        std::fs::write(&path, b"NOPENOPE").unwrap();

        let err = read_oplog(tmp.path(), 3).unwrap_err();
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
        let path = oplog_path(tmp.path(), 13);
        std::fs::create_dir_all(path.parent().unwrap()).unwrap();
        // 4 bytes — half a header.
        std::fs::write(&path, MAGIC).unwrap();

        let err = read_oplog(tmp.path(), 13).unwrap_err();
        assert_eq!(err.kind(), io::ErrorKind::InvalidData);
        assert!(
            err.to_string().contains("truncated header"),
            "expected truncated-header diagnostic, got {err}"
        );
    }

    #[test]
    fn unsupported_version_is_a_hard_error() {
        let tmp = tempfile::tempdir().unwrap();
        let path = oplog_path(tmp.path(), 4);
        std::fs::create_dir_all(path.parent().unwrap()).unwrap();
        let mut header = [0u8; HEADER_LEN];
        header[0..4].copy_from_slice(&MAGIC);
        header[4] = 99; // some future version this build doesn't know
        std::fs::write(&path, header).unwrap();

        let err = read_oplog(tmp.path(), 4).unwrap_err();
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
        append_entry(tmp.path(), 100, &original).unwrap();

        let entries = read_oplog(tmp.path(), 100).unwrap();
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
        // The fix takes an OS-level exclusive lock (`File::lock`)
        // across the "is the header in place?" check *and* the
        // entry append, so whichever thread gets the lock first
        // observes an empty file and writes the header before
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
                    append_entry(&p, 42, &entry(&payload)).unwrap();
                })
            })
            .collect();
        for h in handles {
            h.join().unwrap();
        }

        let entries = read_oplog(&tmp_path, 42).unwrap();
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
        let path = oplog_path(tmp.path(), 7);
        // Write 4 bytes (less than HEADER_LEN = 8) to simulate a
        // torn header.
        std::fs::write(&path, b"YOLG").unwrap();

        let err = append_entry(tmp.path(), 7, &entry(b"after-torn-header")).unwrap_err();
        assert_eq!(err.kind(), io::ErrorKind::InvalidData);
        assert!(err.to_string().contains("torn header"));
    }

    use std::sync::Arc;
}
