//! Append-only binary save journal for vault notes.
//!
//! Each indexed note gets its own log at
//! `<cache_dir>/oplog/<file_id>.oplog`. Keying on `files.id` rather
//! than path means a rename — which keeps the same row in SQLite —
//! does not lose history.
//!
//! V1.F records exactly one op kind, `WholeFileReplace`, with the full
//! new contents as the payload. Per-keystroke ops, structural deltas,
//! and compaction (see `SessionConfig::oplog_compaction_threshold_*`)
//! are V1.x concerns; this module's job is to lay down the on-disk
//! format and the reader so V2's accessible conflict-resolution UI has
//! something to consume.
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
//!     op_kind:          u8 (1 = WholeFileReplace)
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
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum OpKind {
    /// Whole-file replace: the payload is the full new file contents.
    WholeFileReplace,
}

impl OpKind {
    fn as_u8(self) -> u8 {
        match self {
            OpKind::WholeFileReplace => 1,
        }
    }

    fn try_from_u8(v: u8) -> Option<Self> {
        match v {
            1 => Some(OpKind::WholeFileReplace),
            _ => None,
        }
    }
}

/// One recorded operation. V1.F always uses `OpKind::WholeFileReplace`
/// and stores the full new file in `payload_bytes`.
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

    // First-write race (PR #105 Codoki feedback). The naive shape —
    // open(append|create), then check len==0, then write the header —
    // lets two writers both observe an empty file and both append the
    // 8-byte header, leaving the file with a duplicate header where
    // the reader expects an entry's `body_len`. `read_oplog` would
    // then interpret "YOLG" as a u32 (~1.2 GiB), trip the plausibility
    // check, and discard every subsequent entry.
    //
    // `create_new(true)` is the OS-atomic test-and-create primitive:
    // exactly one caller wins the create and is responsible for the
    // header; everyone else gets `AlreadyExists` and opens the file
    // in pure-append mode without touching the prefix. Within a
    // single process the session mutex already serializes appends, so
    // this guard is for cross-process safety today and future-proofs
    // multi-process scenarios.
    let (mut file, is_new_file) = match OpenOptions::new().append(true).create_new(true).open(&path)
    {
        Ok(mut f) => {
            let mut header = [0u8; HEADER_LEN];
            header[0..4].copy_from_slice(&MAGIC);
            header[4] = FORMAT_VERSION;
            // bytes 5..8 are reserved and already zero.
            f.write_all(&header)?;
            (f, true)
        }
        Err(e) if e.kind() == io::ErrorKind::AlreadyExists => {
            (OpenOptions::new().append(true).open(&path)?, false)
        }
        Err(e) => return Err(e),
    };

    let body = serialize_body(entry);
    let body_len = body.len() as u32;
    let checksum = body_checksum(&body);

    // Assemble the entire framed record in memory and write it with a
    // single syscall. Under O_APPEND, the kernel is responsible for
    // making each write append atomically; using one write() (rather
    // than several) keeps individual entries from interleaving with
    // concurrent writers up to the OS's atomic-append guarantee.
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
    // content_hash returns blake3 hex (64 chars). Take the first 8 hex
    // characters → 4 bytes → u32 LE. Cheap, no extra dependency, and
    // collisions on a 32-bit window are still vanishingly unlikely for
    // detecting accidental truncation.
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
        _ => 0, // content_hash always yields lowercase hex; this branch is unreachable
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
        // Codoki-flagged race regression: two threads race to be the
        // first append on a brand-new file_id. The naive
        // "open-append, check len, write header if 0" pattern lets
        // both win, leaving a duplicate 8-byte header where the
        // reader expects the next entry's body_len. With
        // `create_new` only one thread wins the creation and writes
        // the header; the other falls through to plain append. Both
        // entries must round-trip cleanly afterward.
        //
        // Provoking the race deterministically in a unit test is
        // best-effort — a barrier nudges both threads into the
        // critical region at the same time, but the actual interleave
        // is up to the scheduler. Even a single observed pass shows
        // the OS-level create_new gate is doing its job (the naive
        // pattern produced a flaky double-header on this same shape
        // before the fix).
        use std::sync::Barrier;
        let tmp = tempfile::tempdir().unwrap();
        let tmp_path = tmp.path().to_path_buf();
        let entry_a = entry(b"thread-a");
        let entry_b = entry(b"thread-b");

        let barrier = Arc::new(Barrier::new(2));
        let p1 = tmp_path.clone();
        let p2 = tmp_path.clone();
        let b1 = Arc::clone(&barrier);
        let b2 = Arc::clone(&barrier);

        let t1 = std::thread::spawn(move || {
            b1.wait();
            append_entry(&p1, 42, &entry_a).unwrap();
        });
        let t2 = std::thread::spawn(move || {
            b2.wait();
            append_entry(&p2, 42, &entry_b).unwrap();
        });
        t1.join().unwrap();
        t2.join().unwrap();

        let entries = read_oplog(&tmp_path, 42).unwrap();
        // Both threads' entries must be present and intact. If a
        // double header had landed, the second one would be
        // interpreted as `body_len = "YOLG" as u32` ≈ 1.2 GiB,
        // tripping MAX_PLAUSIBLE_BODY_LEN and zeroing the count.
        assert_eq!(
            entries.len(),
            2,
            "both entries should survive the race; got {entries:?}"
        );
    }

    use std::sync::Arc;
}
