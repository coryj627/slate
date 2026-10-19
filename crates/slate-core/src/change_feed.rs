// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! The change feed (plan 43 PR 21a, #1293; contract 40 AR-30 and AR-31;
//! design `docs/plans/43_mac_backlog_delivery/21_change_feed_design.md`).
//!
//! The index records what is current. Migration 040's triggers make it also
//! record what changed and who changed it: every mutation of `files` or
//! `dirs` by any Slate connection, in any process, writes one `change_feed`
//! row inside the same statement, while at least one session holds a cursor
//! (§4.1). This module owns everything the feed does with those rows:
//!
//! - **Attribution.** The two connection functions the triggers call
//!   ([`register_default_feed_functions`], [`ChangeFeedSeat::install`]) and
//!   the origin scope a session takes around its own announced writes
//!   ([`ChangeFeedSeat::write_scope`]): a row is a session's **own write**
//!   when its `writer` is that session and its `origin` is Write — the host
//!   already applied it from its file-change event.
//! - **Cursors** (§4.2): registration, the heartbeat, the 24-hour liveness,
//!   the trim below every live cursor, the 100,000-row cap, and the gaps
//!   (no cursor, a cursor below the trim floor or above the head, a pending
//!   re-sync), whose window re-creates the cursor at the head so the
//!   triggers stay on during the host's full re-sync.
//! - **Windows** (§4.3): one read transaction from `after` (the session's
//!   cursor when `None`) to the head; a range with no row that is not an own
//!   write is an empty exact window however large; a range of more than
//!   [`CHANGE_FEED_WINDOW_ROW_LIMIT`] rows is [`ChangeFeedStatus::TooLarge`]
//!   and is not read; otherwise the per-path derivation ([`Derivation`])
//!   gives the records and the counts, and the records are materialized,
//!   with their `list_files` summaries as of the target, into a
//!   connection-local TEMP table that [`page`] reads by keyset,
//!   removal-first.
//!
//! The counts are always taken from the session's cursor, whatever `after`
//! is, so a run's later pass reports the run's cumulative counts. Under the
//! owner's decision Q2 (design §11) hosts speak those counts only on an
//! explicit Refresh; a foreground or watcher run applies its window
//! silently and speaks only its own scan's changes.
//!
//! Logs about the feed carry no paths (the #507 rule).

use std::collections::HashMap;
use std::marker::PhantomData;
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, AtomicU8, AtomicU64, Ordering};

use rusqlite::{Connection, OptionalExtension};

use crate::VaultError;
use crate::session::{CancelToken, FileSummary};

/// The most records one [`page`] returns (design §4.3, Q5; locked decision
/// 05 §9.3.1): a limit of 0 or above it is refused before any row is read.
pub const MAX_CHANGE_FEED_PAGE_LIMIT: u32 = 10_000;

/// A window whose range holds more rows than this, at least one of them not
/// the session's own write, is [`ChangeFeedStatus::TooLarge`]: the host runs
/// the full re-sync and speaks the uncounted sentence (design §4.3, Q5).
pub const CHANGE_FEED_WINDOW_ROW_LIMIT: u64 = 10_000;

/// The feed holds at most this many rows after an acknowledgment; the oldest
/// beyond it are trimmed, and a cursor below them is a gap (design §4.2, Q5).
pub const CHANGE_FEED_MAX_ROWS: u64 = 100_000;

/// A cursor whose heartbeat is older than this is reaped by the next
/// registration or acknowledgment of any session (design §4.2, Q5).
pub const CHANGE_FEED_CURSOR_LIVENESS_MS: i64 = 24 * 60 * 60 * 1000;

/// A run makes at most this many passes (design §4.6, Q5): after its last
/// window it reads the head once more, and opens a further window from that
/// window's target while the head moved.
pub const CHANGE_FEED_MAX_PASSES: u32 = 3;

/// The schema version that created the feed (migration 040).
pub(crate) const FEED_MIGRATION_VERSION: u32 = 40;

/// The writer every Slate connection that is not a session's own writes as:
/// the compaction worker, migrations, tools and test connections.
pub(crate) const UNATTRIBUTED_WRITER: &str = "unattributed";

const ORIGIN_RECONCILE: u8 = 0;
const ORIGIN_WRITE: u8 = 1;

const KIND_CREATED: i64 = 1;
const KIND_MODIFIED: i64 = 2;
const KIND_DELETED: i64 = 3;
const KIND_RENAMED: i64 = 4;

const PHASE_RENAMED: i64 = 0;
const PHASE_REMOVED: i64 = 1;
const PHASE_PRESENT: i64 = 2;

const MAX_TOKEN_BYTES: usize = 128;
const MAX_PAGE_CURSOR_BYTES: usize = 64 * 1024;

// --- The public records -------------------------------------------------------

/// Why a window could not be read exactly (design §4.2).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ChangeFeedGap {
    /// This session holds no cursor: it never registered, its cursor was
    /// reaped, or the cache was rebuilt or migrated under it.
    NoCursor,
    /// Rows the window needs were trimmed (the cursor is below the trim
    /// floor: the 100,000-row cap, or rows only an absent cursor held).
    Trimmed,
    /// The window starts above the head (a cache restored from a backup).
    Reset,
    /// A gap re-created this cursor and the host has not acknowledged its
    /// full re-sync yet (a cancelled re-sync is never taken for a finished
    /// one).
    ResyncPending,
}

/// What a window is (design §4.3).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ChangeFeedStatus {
    /// The window's records and counts are exact.
    Exact,
    /// More than [`CHANGE_FEED_WINDOW_ROW_LIMIT`] rows, not all this
    /// session's own writes; nothing was read. The host runs the full
    /// re-sync.
    TooLarge,
    /// The feed cannot answer; the host runs the full re-sync. The window
    /// re-created the session's cursor at the head, marked pending.
    Gap(ChangeFeedGap),
}

/// One opened window of the feed (design §4.3). `TooLarge` and `Gap` carry
/// no records and no counts.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ChangeFeedWindow {
    /// Opaque: pass it to [`crate::VaultSession::change_feed_page`] and
    /// [`crate::VaultSession::acknowledge_change_feed`]. Bound to this
    /// session and to this window; the next window replaces it.
    pub token: String,
    /// The `seq` the window starts after.
    pub from_seq: u64,
    /// The window's target: the head it read up to.
    pub to_seq: u64,
    pub status: ChangeFeedStatus,
    /// The counts are exact. False on a gap, a too-large window, and any
    /// window of a run whose range from the cursor is past the row limit.
    pub counted: bool,
    /// New or changed files from the session's cursor to the target (the
    /// run's cumulative count): R-9's "new or changed".
    pub changed: u64,
    /// Removed files from the session's cursor to the target.
    pub removed: u64,
    /// How many records the window's pages hold.
    pub record_count: u64,
}

/// What a record names.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub enum ChangeEntry {
    File,
    Directory,
}

/// What a record asks the host to do (design §4.3).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ChangeEffect {
    /// The content the host's tabs at `renamed_from` hold (`from_hash`) now
    /// lives at `path`: move the tabs whose baseline equals `from_hash`.
    Renamed,
    /// `path` is absent from the index at the window's target.
    Removed,
    /// `path` is present at the window's target, with `content_hash` and
    /// `summary`.
    Present,
}

/// How a record's path counts, from the session's cursor (design §4.3).
/// Directories are never counted.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ChangeCount {
    Created,
    Modified,
    Removed,
    Uncounted,
}

/// One per-path record of a window (design §4.3).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ChangeRecord {
    /// Vault-relative.
    pub path: String,
    pub entry: ChangeEntry,
    pub effect: ChangeEffect,
    /// `Renamed` only: the origin path.
    pub renamed_from: Option<String>,
    /// `Renamed` only: the baseline the host's tabs at the origin hold.
    pub from_hash: Option<String>,
    pub counted: ChangeCount,
    /// `Present` files: the content hash at the window's target.
    pub content_hash: Option<String>,
    /// `Present` files: the `list_files` summary as of the target.
    pub summary: Option<FileSummary>,
    /// Core's openable-document classification of `path`.
    pub openable: bool,
    /// The path's last row in the window (for a `Renamed` record, the
    /// chain's last rename), for diagnostics.
    pub seq: u64,
}

/// One bounded page of a window's records: renames in `seq` order first,
/// then removals by path, then the rest by path, so pages are removal-first
/// across page boundaries.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ChangeFeedPage {
    pub records: Vec<ChangeRecord>,
    /// Opaque keyset continuation; `None` on the last page.
    pub next_cursor: Option<String>,
}

// --- Attribution: the connection functions and the origin scope --------------

/// Register `slate_feed_writer()` and `slate_feed_origin()` with their
/// defaults ("unattributed", reconcile) — on every Slate connection, through
/// [`crate::db::register_connection_functions`].
pub(crate) fn register_default_feed_functions(conn: &Connection) -> rusqlite::Result<()> {
    register_feed_functions(conn, UNATTRIBUTED_WRITER.to_string(), None)
}

/// Both functions are `SQLITE_INNOCUOUS` (they have no side effects), so the
/// triggers keep working if a later SQLite build turns `trusted_schema` off,
/// and neither is deterministic: the origin changes between statements.
fn register_feed_functions(
    conn: &Connection,
    writer: String,
    origin: Option<Arc<AtomicU8>>,
) -> rusqlite::Result<()> {
    use rusqlite::functions::FunctionFlags;
    let flags = FunctionFlags::SQLITE_UTF8 | FunctionFlags::SQLITE_INNOCUOUS;
    conn.create_scalar_function("slate_feed_writer", 0, flags, move |_| Ok(writer.clone()))?;
    conn.create_scalar_function("slate_feed_origin", 0, flags, move |_| {
        Ok(i64::from(
            origin
                .as_ref()
                .map_or(ORIGIN_RECONCILE, |cell| cell.load(Ordering::Relaxed)),
        ))
    })
}

/// A session's feed identity and its connection-local window state.
pub(crate) struct ChangeFeedSeat {
    /// `"<actor>:<pid>:<nonce>"`: the op-log actor alone cannot tell two
    /// windows apart (both are `"local"`).
    writer: String,
    /// The session's nonce, which binds a window token to this session.
    nonce: u64,
    /// Write while the session is inside one of its own announced write
    /// statements, reconcile otherwise. Read by `slate_feed_origin()` on the
    /// session's connection; set and read only with the connection lock
    /// held.
    origin: Arc<AtomicU8>,
    /// The materialized window's generation; 0 before the first window.
    generation: AtomicU64,
    /// This session wrote its cursor row (registration or a gap), so its
    /// `Drop` deletes it.
    registered: AtomicBool,
}

impl ChangeFeedSeat {
    pub(crate) fn new(actor: &str, nonce: u64) -> Self {
        Self {
            writer: format!("{actor}:{}:{nonce:016x}", std::process::id()),
            nonce,
            origin: Arc::new(AtomicU8::new(ORIGIN_RECONCILE)),
            generation: AtomicU64::new(0),
            registered: AtomicBool::new(false),
        }
    }

    pub(crate) fn writer(&self) -> &str {
        &self.writer
    }

    /// Register this session's writer and origin cell on its connection,
    /// overriding the defaults [`crate::db::register_connection_functions`]
    /// installed. The one call site is right after the session's open
    /// migrates (`VaultSession::open`); PR 22a's descriptor-relative open
    /// keeps it the single seam (design §7).
    pub(crate) fn install(&self, conn: &Connection) -> rusqlite::Result<()> {
        register_feed_functions(conn, self.writer.clone(), Some(Arc::clone(&self.origin)))
    }

    /// Mark the statements run while the scope lives as this session's own
    /// announced writes (design §4.1). Borrowing the connection (or the
    /// transaction, which derefs to it) ties the scope to the held
    /// connection lock: it cannot outlive the borrow, so it is dropped
    /// before the transaction commits and before the lock is released.
    /// Scopes nest; dropping one restores the origin it found.
    pub(crate) fn write_scope<'c>(&'c self, _held: &'c Connection) -> FeedWriteScope<'c> {
        let previous = self.origin.swap(ORIGIN_WRITE, Ordering::Relaxed);
        FeedWriteScope {
            cell: &self.origin,
            previous,
            _held: PhantomData,
        }
    }

    pub(crate) fn registered(&self) -> bool {
        self.registered.load(Ordering::Relaxed)
    }

    fn next_generation(&self) -> u64 {
        self.generation.fetch_add(1, Ordering::Relaxed) + 1
    }

    fn current_generation(&self) -> u64 {
        self.generation.load(Ordering::Relaxed)
    }

    /// Decode a window token and check it names this session's current
    /// window; a replaced window's token is stale.
    fn current_window(&self, token: &str) -> Result<WindowToken, VaultError> {
        let token = WindowToken::decode(token)?;
        if token.nonce != self.nonce {
            return Err(invalid("change feed window belongs to a different session"));
        }
        if token.generation != self.current_generation() {
            return Err(invalid("change feed window is stale"));
        }
        Ok(token)
    }
}

/// See [`ChangeFeedSeat::write_scope`].
pub(crate) struct FeedWriteScope<'c> {
    cell: &'c AtomicU8,
    previous: u8,
    _held: PhantomData<&'c Connection>,
}

impl Drop for FeedWriteScope<'_> {
    fn drop(&mut self) {
        self.cell.store(self.previous, Ordering::Relaxed);
    }
}

// --- Tokens -------------------------------------------------------------------

fn invalid(message: &str) -> VaultError {
    VaultError::InvalidArgument {
        message: message.to_string(),
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
struct WindowToken {
    nonce: u64,
    from: u64,
    to: u64,
    generation: u64,
}

impl WindowToken {
    fn encode(self) -> String {
        format!(
            "cfw1:{:016x}:{}:{}:{}",
            self.nonce, self.from, self.to, self.generation
        )
    }

    fn decode(value: &str) -> Result<Self, VaultError> {
        let malformed = || invalid("malformed change feed window token");
        if value.len() > MAX_TOKEN_BYTES {
            return Err(malformed());
        }
        let mut parts = value.split(':');
        if parts.next() != Some("cfw1") {
            return Err(malformed());
        }
        let nonce = u64::from_str_radix(parts.next().ok_or_else(malformed)?, 16)
            .map_err(|_| malformed())?;
        let mut number = || -> Result<u64, VaultError> {
            parts
                .next()
                .ok_or_else(malformed)?
                .parse::<u64>()
                .map_err(|_| malformed())
        };
        let from = number()?;
        let to = number()?;
        let generation = number()?;
        if parts.next().is_some() || from > to {
            return Err(malformed());
        }
        Ok(Self {
            nonce,
            from,
            to,
            generation,
        })
    }
}

/// The keyset position of a page: (phase, rename seq, path, entry).
#[derive(Debug, Clone, PartialEq, Eq)]
struct PageCursor {
    generation: u64,
    phase: i64,
    sort_seq: i64,
    entry: i64,
    path: String,
}

impl PageCursor {
    fn encode(&self) -> String {
        format!(
            "cfp1:{}:{}:{}:{}:{}",
            self.generation, self.phase, self.sort_seq, self.entry, self.path
        )
    }

    fn decode(value: &str) -> Result<Self, VaultError> {
        let malformed = || invalid("malformed change feed page cursor");
        if value.len() > MAX_PAGE_CURSOR_BYTES {
            return Err(malformed());
        }
        let mut parts = value.splitn(6, ':');
        if parts.next() != Some("cfp1") {
            return Err(malformed());
        }
        let mut number = || -> Result<i64, VaultError> {
            parts
                .next()
                .ok_or_else(malformed)?
                .parse::<i64>()
                .map_err(|_| malformed())
        };
        let generation = u64::try_from(number()?).map_err(|_| malformed())?;
        let phase = number()?;
        let sort_seq = number()?;
        let entry = number()?;
        let path = parts.next().ok_or_else(malformed)?.to_string();
        if !(PHASE_RENAMED..=PHASE_PRESENT).contains(&phase) || !(0..=1).contains(&entry) {
            return Err(malformed());
        }
        Ok(Self {
            generation,
            phase,
            sort_seq,
            entry,
            path,
        })
    }
}

// --- Shared SQL ---------------------------------------------------------------

fn now_ms() -> i64 {
    std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map(|duration| duration.as_millis() as i64)
        .unwrap_or(0)
}

fn to_u64(value: i64) -> u64 {
    u64::try_from(value).unwrap_or(0)
}

fn to_i64(value: u64) -> i64 {
    i64::try_from(value).unwrap_or(i64::MAX)
}

/// The feed's head: AUTOINCREMENT's high-water mark, 0 before the first
/// row. Trimming every row never moves it back (design §4.1).
pub(crate) fn head(conn: &Connection) -> rusqlite::Result<u64> {
    conn.query_row(
        "SELECT COALESCE((SELECT seq FROM sqlite_sequence WHERE name = 'change_feed'), 0)",
        [],
        |row| row.get::<_, i64>(0),
    )
    .map(to_u64)
}

fn trimmed_through(conn: &Connection) -> rusqlite::Result<u64> {
    conn.query_row(
        "SELECT trimmed_through FROM change_feed_meta WHERE id = 1",
        [],
        |row| row.get::<_, i64>(0),
    )
    .map(to_u64)
}

/// Every cursor and every row is cleared and the head recorded as trimmed
/// (design §4.2: "migrations clear the cursors"), inside `migrate`'s
/// transaction.
pub(crate) fn clear_after_migration(conn: &Connection) -> rusqlite::Result<()> {
    conn.execute_batch(
        "DELETE FROM change_feed_cursors;
         DELETE FROM change_feed;
         UPDATE change_feed_meta
            SET trimmed_through =
                COALESCE((SELECT seq FROM sqlite_sequence WHERE name = 'change_feed'), 0)
          WHERE id = 1;",
    )
}

/// Reap every other session's cursor whose heartbeat is older than the
/// liveness window.
fn reap_stale_cursors(conn: &Connection, me: &str, now: i64) -> rusqlite::Result<usize> {
    conn.execute(
        "DELETE FROM change_feed_cursors WHERE writer != ?1 AND heartbeat_ms < ?2",
        rusqlite::params![me, now.saturating_sub(CHANGE_FEED_CURSOR_LIVENESS_MS)],
    )
}

/// Set this session's cursor, creating it when absent.
fn upsert_cursor(
    conn: &Connection,
    me: &str,
    seq: u64,
    now: i64,
    resync_pending: bool,
) -> rusqlite::Result<()> {
    conn.execute(
        "INSERT INTO change_feed_cursors (writer, seq, heartbeat_ms, resync_pending)
         VALUES (?1, ?2, ?3, ?4)
         ON CONFLICT(writer) DO UPDATE SET
            seq = excluded.seq,
            heartbeat_ms = excluded.heartbeat_ms,
            resync_pending = excluded.resync_pending",
        rusqlite::params![me, to_i64(seq), now, resync_pending as i64],
    )?;
    Ok(())
}

/// Whether a row in `(from, to]` is not `me`'s own write: one streaming
/// pass over the range that holds nothing in memory and stops at the first.
fn has_foreign_row(conn: &Connection, from: u64, to: u64, me: &str) -> rusqlite::Result<bool> {
    conn.prepare_cached(
        "SELECT EXISTS(
            SELECT 1 FROM change_feed
             WHERE seq > ?1 AND seq <= ?2 AND NOT (writer = ?3 AND origin = 1))",
    )?
    .query_row(rusqlite::params![to_i64(from), to_i64(to), me], |row| {
        row.get::<_, bool>(0)
    })
}

/// Stream the rows of `(from, to]` in `seq` order into `derivation`.
fn derive_range(
    conn: &Connection,
    from: u64,
    to: u64,
    me: &str,
    cancel: &CancelToken,
    derivation: &mut Derivation,
) -> Result<(), VaultError> {
    let mut stmt = conn.prepare_cached(
        "SELECT seq, kind, entry, path, previous_path, hash_before, hash_after,
                (writer = ?3 AND origin = 1)
           FROM change_feed
          WHERE seq > ?1 AND seq <= ?2
          ORDER BY seq",
    )?;
    let mut rows = stmt.query(rusqlite::params![to_i64(from), to_i64(to), me])?;
    while let Some(row) = rows.next()? {
        cancel.check()?;
        #[cfg(test)]
        window_test_hook::count_row();
        let entry = match row.get::<_, i64>(2)? {
            0 => ChangeEntry::File,
            1 => ChangeEntry::Directory,
            _ => return Err(invalid("change feed row names an unknown entry")),
        };
        derivation.apply(FeedRow {
            seq: to_u64(row.get::<_, i64>(0)?),
            kind: row.get(1)?,
            entry,
            path: row.get(3)?,
            previous_path: row.get(4)?,
            hash_before: row.get(5)?,
            hash_after: row.get(6)?,
            own: row.get(7)?,
        })?;
    }
    Ok(())
}

// --- Registration and acknowledgment -------------------------------------------

/// `register_change_feed`'s body (design §4.2), inside the caller's fenced
/// transaction: reap stale cursors, then set this session's cursor to the
/// head with a fresh heartbeat. Returns the head.
pub(crate) fn register(conn: &Connection, seat: &ChangeFeedSeat) -> Result<u64, VaultError> {
    let now = now_ms();
    reap_stale_cursors(conn, &seat.writer, now)?;
    let head = head(conn)?;
    upsert_cursor(conn, &seat.writer, head, now, false)?;
    Ok(head)
}

/// Called after the registration committed: the cursor row exists, and any
/// window opened before it is stale.
pub(crate) fn registered(seat: &ChangeFeedSeat) {
    seat.registered.store(true, Ordering::Relaxed);
    seat.next_generation();
}

/// The validated window an acknowledgment moves the cursor to.
pub(crate) struct AcknowledgedWindow(WindowToken);

pub(crate) fn acknowledged_window(
    seat: &ChangeFeedSeat,
    token: &str,
) -> Result<AcknowledgedWindow, VaultError> {
    seat.current_window(token).map(AcknowledgedWindow)
}

/// `acknowledge_change_feed`'s body (design §4.2), inside the caller's
/// fenced, cancellable transaction: this session's heartbeat and cursor
/// first, then the trim — reap stale cursors; delete the rows at or below
/// the lowest live cursor (all rows when none is live); delete the oldest
/// beyond [`CHANGE_FEED_MAX_ROWS`]; record the highest deleted `seq`.
///
/// A cursor another session reaped meanwhile is not re-created here: the
/// triggers may have been off since, so the next window must be a gap.
pub(crate) fn acknowledge(
    conn: &Connection,
    seat: &ChangeFeedSeat,
    window: &AcknowledgedWindow,
    cancel: &CancelToken,
) -> Result<(), VaultError> {
    let now = now_ms();
    conn.execute(
        "UPDATE change_feed_cursors
            SET seq = ?2, heartbeat_ms = ?3, resync_pending = 0
          WHERE writer = ?1",
        rusqlite::params![seat.writer, to_i64(window.0.to), now],
    )?;
    cancel.check()?;
    reap_stale_cursors(conn, &seat.writer, now)?;
    trim(conn, cancel)
}

fn trim(conn: &Connection, cancel: &CancelToken) -> Result<(), VaultError> {
    let lowest_live: Option<i64> =
        conn.query_row("SELECT MIN(seq) FROM change_feed_cursors", [], |row| {
            row.get(0)
        })?;
    let mut highest_deleted: Option<i64> = None;
    let below: Option<i64> = match lowest_live {
        Some(lowest) => conn.query_row(
            "SELECT MAX(seq) FROM change_feed WHERE seq <= ?1",
            [lowest],
            |row| row.get(0),
        )?,
        None => conn.query_row("SELECT MAX(seq) FROM change_feed", [], |row| row.get(0))?,
    };
    if let Some(below) = below {
        cancel.check()?;
        conn.execute("DELETE FROM change_feed WHERE seq <= ?1", [below])?;
        highest_deleted = Some(below);
    }
    cancel.check()?;
    let remaining: i64 =
        conn.query_row("SELECT COUNT(*) FROM change_feed", [], |row| row.get(0))?;
    let cap = to_i64(CHANGE_FEED_MAX_ROWS);
    if remaining > cap {
        let cutoff: i64 = conn.query_row(
            "SELECT seq FROM change_feed ORDER BY seq LIMIT 1 OFFSET ?1",
            [remaining - cap - 1],
            |row| row.get(0),
        )?;
        cancel.check()?;
        conn.execute("DELETE FROM change_feed WHERE seq <= ?1", [cutoff])?;
        highest_deleted = Some(highest_deleted.map_or(cutoff, |deleted| deleted.max(cutoff)));
    }
    if let Some(deleted) = highest_deleted {
        conn.execute(
            "UPDATE change_feed_meta SET trimmed_through = MAX(trimmed_through, ?1) WHERE id = 1",
            [deleted],
        )?;
    }
    Ok(())
}

// --- Windows ------------------------------------------------------------------

/// Test seam: a callback run on the calling thread inside a window's read
/// transaction, after its rows are derived and before its summaries are
/// read (`the_window_is_one_snapshot`).
#[cfg(test)]
pub(crate) mod window_test_hook {
    use std::cell::{Cell, RefCell};

    type WindowHook = Box<dyn Fn()>;

    thread_local! {
        static HOOK: RefCell<Option<WindowHook>> = const { RefCell::new(None) };
        static ROWS_DERIVED: Cell<u64> = const { Cell::new(0) };
    }

    /// Feed rows the derivation read on this thread since the last call:
    /// how a fact proves an oversized window read nothing.
    pub(crate) fn take_rows_derived() -> u64 {
        ROWS_DERIVED.with(|count| count.replace(0))
    }

    pub(super) fn count_row() {
        ROWS_DERIVED.with(|count| count.set(count.get() + 1));
    }

    pub(crate) fn install(hook: WindowHook) {
        HOOK.with(|slot| *slot.borrow_mut() = Some(hook));
    }

    pub(crate) fn clear() {
        HOOK.with(|slot| *slot.borrow_mut() = None);
    }

    pub(super) fn fire() {
        HOOK.with(|slot| {
            if let Some(hook) = slot.borrow().as_ref() {
                hook();
            }
        });
    }
}

const CREATE_MATERIALIZATION: &str = "
    CREATE TEMP TABLE IF NOT EXISTS change_feed_records (
        phase          INTEGER NOT NULL,
        sort_seq       INTEGER NOT NULL,
        path           TEXT NOT NULL,
        entry          INTEGER NOT NULL,
        effect         INTEGER NOT NULL,
        renamed_from   TEXT,
        from_hash      TEXT,
        counted        INTEGER NOT NULL,
        content_hash   TEXT,
        openable       INTEGER NOT NULL,
        seq            INTEGER NOT NULL,
        has_summary    INTEGER NOT NULL,
        s_name         TEXT,
        s_mtime_ms     INTEGER,
        s_size_bytes   INTEGER,
        s_is_markdown  INTEGER,
        s_display_name TEXT,
        s_created_date TEXT,
        s_created_ms   INTEGER,
        s_word_count   INTEGER,
        s_preview      TEXT,
        s_task_total   INTEGER,
        s_task_open    INTEGER,
        PRIMARY KEY (phase, sort_seq, path, entry)
    ) WITHOUT ROWID;
    CREATE TEMP TABLE IF NOT EXISTS change_feed_summary_paths (
        path TEXT PRIMARY KEY
    ) WITHOUT ROWID;";

/// Empty the connection-local materialization (a new window replaces it).
fn clear_materialization(conn: &Connection) -> rusqlite::Result<()> {
    conn.execute_batch(CREATE_MATERIALIZATION)?;
    conn.execute_batch(
        "DELETE FROM temp.change_feed_records;
         DELETE FROM temp.change_feed_summary_paths;",
    )
}

/// What the window's first reads found.
struct WindowStart {
    cursor: Option<(u64, bool)>,
    trimmed: u64,
    head: u64,
}

fn gap_of(start: &WindowStart, after: Option<u64>) -> Result<Option<ChangeFeedGap>, VaultError> {
    let Some((cursor, pending)) = start.cursor else {
        return Ok(Some(ChangeFeedGap::NoCursor));
    };
    if let Some(after) = after
        && after < cursor
    {
        return Err(invalid(
            "a change feed window must start at or after the session's cursor",
        ));
    }
    let from = after.unwrap_or(cursor);
    Ok(if after.is_none() && pending {
        Some(ChangeFeedGap::ResyncPending)
    } else if from < start.trimmed {
        Some(ChangeFeedGap::Trimmed)
    } else if from > start.head || cursor > start.head {
        Some(ChangeFeedGap::Reset)
    } else {
        None
    })
}

/// What [`open_window`] found: a window, or a gap whose cursor the caller
/// re-creates in a fenced write ([`record_gap`], then [`gap_window`]).
pub(crate) enum OpenedWindow {
    Window(ChangeFeedWindow),
    Gap(PendingGap),
}

/// A gap [`open_window`] found, before its cursor is re-created.
pub(crate) struct PendingGap {
    gap: ChangeFeedGap,
    requested_from: Option<u64>,
}

/// `change_feed_window`'s read (design §4.2, §4.3), on the session's locked
/// connection under SQLite's progress-handler cancellation.
pub(crate) fn open_window(
    conn: &Connection,
    seat: &ChangeFeedSeat,
    after: Option<u64>,
    cancel: &CancelToken,
) -> Result<OpenedWindow, VaultError> {
    let me = seat.writer.as_str();
    // One read transaction: every read below sees one snapshot (§3.4). The
    // first statement reads the main database, which pins it.
    let read = conn.unchecked_transaction()?;
    let start = WindowStart {
        cursor: read
            .query_row(
                "SELECT seq, resync_pending FROM change_feed_cursors WHERE writer = ?1",
                [me],
                |row| Ok((to_u64(row.get::<_, i64>(0)?), row.get::<_, bool>(1)?)),
            )
            .optional()?,
        trimmed: trimmed_through(&read)?,
        head: head(&read)?,
    };

    if let Some(gap) = gap_of(&start, after)? {
        return Ok(OpenedWindow::Gap(PendingGap {
            gap,
            requested_from: after.or(start.cursor.map(|(seq, _)| seq)),
        }));
    }
    let (cursor, pending) = start.cursor.expect("no gap means a cursor");
    let from = after.unwrap_or(cursor);
    let to = start.head;
    cancel.check()?;

    // The records: from `from` (the cursor, or the previous pass's target).
    let mut status = ChangeFeedStatus::Exact;
    let mut records_derivation = None;
    if has_foreign_row(&read, from, to, me)? {
        if to - from > CHANGE_FEED_WINDOW_ROW_LIMIT {
            status = ChangeFeedStatus::TooLarge;
        } else {
            let mut derivation = Derivation::default();
            derive_range(&read, from, to, me, cancel, &mut derivation)?;
            records_derivation = Some(derivation);
        }
    }

    // The counts: always from the session's cursor, so a later pass reports
    // the run's cumulative counts. A run past the row limit, a pending
    // re-sync and a cursor below the trim floor are uncounted.
    let counts: Option<PathCounts> =
        if pending || cursor < start.trimmed || status == ChangeFeedStatus::TooLarge {
            None
        } else if from == cursor {
            Some(
                records_derivation
                    .as_ref()
                    .map(Derivation::counts)
                    .unwrap_or_default(),
            )
        } else if !has_foreign_row(&read, cursor, to, me)? {
            Some(PathCounts::default())
        } else if to - cursor > CHANGE_FEED_WINDOW_ROW_LIMIT {
            None
        } else {
            let mut derivation = Derivation::default();
            derive_range(&read, cursor, to, me, cancel, &mut derivation)?;
            Some(derivation.counts())
        };

    #[cfg(test)]
    window_test_hook::fire();

    let records = records_derivation
        .as_ref()
        .map(|derivation| derivation.records(counts.as_ref()))
        .unwrap_or_default();
    clear_materialization(&read)?;
    let summaries = present_file_summaries(&read, &records, cancel)?;
    materialize(&read, &records, &summaries, cancel)?;
    read.commit()?;
    let generation = seat.next_generation();
    Ok(OpenedWindow::Window(ChangeFeedWindow {
        token: WindowToken {
            nonce: seat.nonce,
            from,
            to,
            generation,
        }
        .encode(),
        from_seq: from,
        to_seq: to,
        status,
        counted: counts.is_some(),
        changed: counts.as_ref().map_or(0, |counts| counts.changed),
        removed: counts.as_ref().map_or(0, |counts| counts.removed),
        record_count: records.len() as u64,
    }))
}

/// A gap's write (design §4.3), inside the caller's fenced transaction:
/// re-create the session's cursor at the head, marked `resync_pending`, so
/// the triggers stay on during the host's full re-sync and later passes
/// have a cursor to count from, and empty the materialization. Returns the
/// head.
pub(crate) fn record_gap(conn: &Connection, seat: &ChangeFeedSeat) -> Result<u64, VaultError> {
    let head = head(conn)?;
    upsert_cursor(conn, &seat.writer, head, now_ms(), true)?;
    clear_materialization(conn)?;
    Ok(head)
}

/// The gap's window, once [`record_gap`] committed: no records, no counts.
pub(crate) fn gap_window(
    seat: &ChangeFeedSeat,
    pending: &PendingGap,
    head: u64,
) -> ChangeFeedWindow {
    seat.registered.store(true, Ordering::Relaxed);
    let generation = seat.next_generation();
    let from = pending.requested_from.unwrap_or(head).min(head);
    log::info!(
        "change feed window is a gap ({:?}); the host re-syncs in full",
        pending.gap
    );
    ChangeFeedWindow {
        token: WindowToken {
            nonce: seat.nonce,
            from,
            to: head,
            generation,
        }
        .encode(),
        from_seq: from,
        to_seq: head,
        status: ChangeFeedStatus::Gap(pending.gap),
        counted: false,
        changed: 0,
        removed: 0,
        record_count: 0,
    }
}

/// The `list_files` summaries of the window's present files, as of its
/// target: one statement through the shared summary projection, inside the
/// window's read transaction.
fn present_file_summaries(
    conn: &Connection,
    records: &[DerivedRecord],
    cancel: &CancelToken,
) -> Result<HashMap<String, FileSummary>, VaultError> {
    let mut summaries = HashMap::new();
    let present: Vec<&str> = records
        .iter()
        .filter(|record| {
            record.entry == ChangeEntry::File && record.effect == ChangeEffect::Present
        })
        .map(|record| record.path.as_str())
        .collect();
    if present.is_empty() {
        return Ok(summaries);
    }
    {
        let mut insert = conn.prepare_cached(
            "INSERT OR IGNORE INTO temp.change_feed_summary_paths (path) VALUES (?1)",
        )?;
        for path in &present {
            cancel.check()?;
            insert.execute([path])?;
        }
    }
    let query = format!(
        "WITH candidates AS (
             SELECT id, path, name, mtime_ms, size_bytes, is_markdown, birthtime_ms
               FROM files
              WHERE path IN (SELECT path FROM temp.change_feed_summary_paths)
         ),
         totals AS (SELECT COUNT(*) AS total_filtered FROM candidates)
         {}",
        crate::session::file_summary_query_tail()
    );
    let mut stmt = conn.prepare_cached(&query)?;
    let rows = stmt.query_map([], crate::session::decode_file_summary_query_row)?;
    for row in rows {
        cancel.check()?;
        let (summary, _) = row?;
        if let Some(summary) = summary {
            summaries.insert(summary.path.clone(), summary);
        }
    }
    if summaries.len() != present.len() {
        log::warn!(
            "change feed: {} present records have no index row at the target",
            present.len() - summaries.len()
        );
    }
    Ok(summaries)
}

fn effect_code(effect: ChangeEffect) -> i64 {
    match effect {
        ChangeEffect::Renamed => PHASE_RENAMED,
        ChangeEffect::Removed => PHASE_REMOVED,
        ChangeEffect::Present => PHASE_PRESENT,
    }
}

fn count_code(count: ChangeCount) -> i64 {
    match count {
        ChangeCount::Created => 0,
        ChangeCount::Modified => 1,
        ChangeCount::Removed => 2,
        ChangeCount::Uncounted => 3,
    }
}

fn count_of(code: i64) -> ChangeCount {
    match code {
        0 => ChangeCount::Created,
        1 => ChangeCount::Modified,
        2 => ChangeCount::Removed,
        _ => ChangeCount::Uncounted,
    }
}

fn entry_code(entry: ChangeEntry) -> i64 {
    match entry {
        ChangeEntry::File => 0,
        ChangeEntry::Directory => 1,
    }
}

fn materialize(
    conn: &Connection,
    records: &[DerivedRecord],
    summaries: &HashMap<String, FileSummary>,
    cancel: &CancelToken,
) -> Result<(), VaultError> {
    let mut insert = conn.prepare_cached(
        "INSERT INTO temp.change_feed_records
            (phase, sort_seq, path, entry, effect, renamed_from, from_hash, counted,
             content_hash, openable, seq, has_summary, s_name, s_mtime_ms, s_size_bytes,
             s_is_markdown, s_display_name, s_created_date, s_created_ms, s_word_count,
             s_preview, s_task_total, s_task_open)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12, ?13, ?14, ?15, ?16,
                 ?17, ?18, ?19, ?20, ?21, ?22, ?23)",
    )?;
    for record in records {
        cancel.check()?;
        let summary = (record.entry == ChangeEntry::File && record.effect == ChangeEffect::Present)
            .then(|| summaries.get(&record.path))
            .flatten();
        let phase = effect_code(record.effect);
        let sort_seq = if record.effect == ChangeEffect::Renamed {
            to_i64(record.seq)
        } else {
            0
        };
        let openable =
            record.entry == ChangeEntry::File && crate::session::is_openable_document(&record.path);
        insert.execute(rusqlite::params![
            phase,
            sort_seq,
            record.path,
            entry_code(record.entry),
            phase,
            record.renamed_from,
            record.from_hash,
            count_code(record.counted),
            record.content_hash,
            openable as i64,
            to_i64(record.seq),
            summary.is_some() as i64,
            summary.map(|s| s.name.clone()),
            summary.map(|s| s.mtime_ms),
            summary.map(|s| to_i64(s.size_bytes)),
            summary.map(|s| s.is_markdown as i64),
            summary.and_then(|s| s.display_name.clone()),
            summary.and_then(|s| s.created_date.clone()),
            summary.and_then(|s| s.created_ms),
            summary.and_then(|s| s.word_count),
            summary.and_then(|s| s.preview.clone()),
            summary.map(|s| s.task_total),
            summary.map(|s| s.task_open),
        ])?;
    }
    Ok(())
}

// --- Pages --------------------------------------------------------------------

/// `change_feed_page`'s body (design §4.3): at most `limit` records of the
/// session's current window, by keyset on (phase, rename seq, path, entry).
/// `limit` is 1 to [`MAX_CHANGE_FEED_PAGE_LIMIT`]:
/// `VaultSession::change_feed_page` refuses any other before it takes the
/// connection lock, so a zero limit can never end the paging early here.
pub(crate) fn page(
    conn: &Connection,
    seat: &ChangeFeedSeat,
    window: &str,
    cursor: Option<&str>,
    limit: u32,
    cancel: &CancelToken,
) -> Result<ChangeFeedPage, VaultError> {
    debug_assert!(
        (1..=MAX_CHANGE_FEED_PAGE_LIMIT).contains(&limit),
        "change_feed_page validates the page limit"
    );
    let token = seat.current_window(window)?;
    let after = cursor.map(PageCursor::decode).transpose()?;
    if after
        .as_ref()
        .is_some_and(|after| after.generation != token.generation)
    {
        return Err(invalid("change feed page cursor belongs to another window"));
    }
    conn.execute_batch(CREATE_MATERIALIZATION)?;
    let (phase, sort_seq, path, entry) = after.as_ref().map_or((-1, -1, "", -1), |after| {
        (
            after.phase,
            after.sort_seq,
            after.path.as_str(),
            after.entry,
        )
    });
    let mut stmt = conn.prepare_cached(
        "SELECT phase, sort_seq, path, entry, effect, renamed_from, from_hash, counted,
                content_hash, openable, seq, has_summary, s_name, s_mtime_ms, s_size_bytes,
                s_is_markdown, s_display_name, s_created_date, s_created_ms, s_word_count,
                s_preview, s_task_total, s_task_open
           FROM temp.change_feed_records
          WHERE (phase, sort_seq, path, entry) > (?1, ?2, ?3, ?4)
          ORDER BY phase, sort_seq, path, entry
          LIMIT ?5",
    )?;
    let mut rows = stmt.query(rusqlite::params![
        phase,
        sort_seq,
        path,
        entry,
        i64::from(limit) + 1
    ])?;
    let mut records = Vec::new();
    let mut last: Option<PageCursor> = None;
    let mut more = false;
    while let Some(row) = rows.next()? {
        cancel.check()?;
        if records.len() == limit as usize {
            more = true;
            break;
        }
        let path: String = row.get(2)?;
        let entry_code: i64 = row.get(3)?;
        let entry = if entry_code == 0 {
            ChangeEntry::File
        } else {
            ChangeEntry::Directory
        };
        let effect = match row.get::<_, i64>(4)? {
            PHASE_RENAMED => ChangeEffect::Renamed,
            PHASE_REMOVED => ChangeEffect::Removed,
            _ => ChangeEffect::Present,
        };
        let summary = if row.get::<_, bool>(11)? {
            Some(FileSummary {
                path: path.clone(),
                name: row.get(12)?,
                mtime_ms: row.get(13)?,
                size_bytes: to_u64(row.get(14)?),
                is_markdown: row.get::<_, i64>(15)? != 0,
                display_name: row.get(16)?,
                created_date: row.get(17)?,
                created_ms: row.get(18)?,
                word_count: row.get(19)?,
                preview: row.get(20)?,
                task_total: row.get(21)?,
                task_open: row.get(22)?,
            })
        } else {
            None
        };
        last = Some(PageCursor {
            generation: token.generation,
            phase: row.get(0)?,
            sort_seq: row.get(1)?,
            entry: entry_code,
            path: path.clone(),
        });
        records.push(ChangeRecord {
            path,
            entry,
            effect,
            renamed_from: row.get(5)?,
            from_hash: row.get(6)?,
            counted: count_of(row.get(7)?),
            content_hash: row.get(8)?,
            summary,
            openable: row.get::<_, i64>(9)? != 0,
            seq: to_u64(row.get(10)?),
        });
    }
    Ok(ChangeFeedPage {
        records,
        next_cursor: if more {
            last.map(|cursor| cursor.encode())
        } else {
            None
        },
    })
}

// --- The per-path derivation (design §4.3) -------------------------------------

/// One `change_feed` row, as the derivation reads it.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct FeedRow {
    pub(crate) seq: u64,
    pub(crate) kind: i64,
    pub(crate) entry: ChangeEntry,
    pub(crate) path: String,
    pub(crate) previous_path: Option<String>,
    pub(crate) hash_before: Option<String>,
    pub(crate) hash_after: Option<String>,
    /// The deriving session's own write: its writer, origin Write.
    pub(crate) own: bool,
}

/// What the host shows at a path: absent, or present with the hash its tabs
/// hold as their baseline (none for a directory).
#[derive(Debug, Clone, PartialEq, Eq)]
enum Shown {
    Absent,
    Present(Option<String>),
}

/// Where a path's content was when the host last saw it, when it arrived by
/// renames the host did not follow.
#[derive(Debug, Clone, PartialEq, Eq)]
struct ContentOrigin {
    path: String,
    hash: Option<String>,
    /// The chain's last rename, which orders the `Renamed` record.
    rename_seq: u64,
}

/// What the index holds at a path.
#[derive(Debug, Clone, PartialEq, Eq)]
enum Indexed {
    Absent,
    Present {
        hash: Option<String>,
        origin: Option<ContentOrigin>,
    },
}

impl Indexed {
    fn origin(&self) -> Option<ContentOrigin> {
        match self {
            Indexed::Present { origin, .. } => origin.clone(),
            Indexed::Absent => None,
        }
    }
}

#[derive(Debug, Clone)]
struct PathState {
    shown: Shown,
    index: Indexed,
    /// A row that is not an own write touched the path.
    foreign: bool,
    last_seq: u64,
}

impl PathState {
    /// The two states agree at the end: the same presence and hash, and no
    /// content arriving from elsewhere.
    fn agrees(&self) -> bool {
        match (&self.shown, &self.index) {
            (Shown::Absent, Indexed::Absent) => true,
            (Shown::Present(shown), Indexed::Present { hash, origin }) => {
                shown == hash && origin.is_none()
            }
            _ => false,
        }
    }

    fn count(&self) -> ChangeCount {
        match (&self.shown, &self.index) {
            (Shown::Absent, Indexed::Present { .. }) => ChangeCount::Created,
            (Shown::Present(shown), Indexed::Present { hash, .. }) if shown != hash => {
                ChangeCount::Modified
            }
            (Shown::Present(_), Indexed::Absent) => ChangeCount::Removed,
            _ => ChangeCount::Uncounted,
        }
    }
}

/// The before-state a row implies for a path it is the first to touch: at
/// the window's start the host's view and the index agree (design §4.3).
#[derive(Clone, Copy)]
enum Before<'r> {
    Absent,
    Present(&'r Option<String>),
}

/// The per-path state machine of design §4.3, fed rows in `seq` order. Per
/// path it keeps what the host shows and what the index holds; an own write
/// changes the shown state the way the host's own event handling does, and
/// every row sets the index state.
#[derive(Debug, Default)]
pub(crate) struct Derivation {
    states: HashMap<(ChangeEntry, String), PathState>,
}

/// The counts a derivation gives, with each file path's classification.
#[derive(Debug, Default, Clone, PartialEq, Eq)]
pub(crate) struct PathCounts {
    pub(crate) changed: u64,
    pub(crate) removed: u64,
    by_path: HashMap<String, ChangeCount>,
}

impl PathCounts {
    fn of(&self, entry: ChangeEntry, path: &str) -> ChangeCount {
        if entry != ChangeEntry::File {
            return ChangeCount::Uncounted;
        }
        self.by_path
            .get(path)
            .copied()
            .unwrap_or(ChangeCount::Uncounted)
    }
}

/// A record before its summary is attached.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct DerivedRecord {
    pub(crate) path: String,
    pub(crate) entry: ChangeEntry,
    pub(crate) effect: ChangeEffect,
    pub(crate) renamed_from: Option<String>,
    pub(crate) from_hash: Option<String>,
    pub(crate) counted: ChangeCount,
    pub(crate) content_hash: Option<String>,
    pub(crate) seq: u64,
}

impl Derivation {
    fn state(&mut self, entry: ChangeEntry, path: &str, before: Before<'_>) -> &mut PathState {
        self.states
            .entry((entry, path.to_string()))
            .or_insert_with(|| {
                let (shown, index) = match before {
                    Before::Absent => (Shown::Absent, Indexed::Absent),
                    Before::Present(hash) => (
                        Shown::Present(hash.clone()),
                        Indexed::Present {
                            hash: hash.clone(),
                            origin: None,
                        },
                    ),
                };
                PathState {
                    shown,
                    index,
                    foreign: false,
                    last_seq: 0,
                }
            })
    }

    pub(crate) fn apply(&mut self, row: FeedRow) -> Result<(), VaultError> {
        match row.kind {
            KIND_CREATED => {
                let state = self.state(row.entry, &row.path, Before::Absent);
                state.index = Indexed::Present {
                    hash: row.hash_after.clone(),
                    origin: None,
                };
                if row.own {
                    state.shown = Shown::Present(row.hash_after);
                } else {
                    state.foreign = true;
                }
                state.last_seq = row.seq;
            }
            KIND_MODIFIED => {
                let state = self.state(row.entry, &row.path, Before::Present(&row.hash_before));
                let origin = state.index.origin();
                state.index = Indexed::Present {
                    hash: row.hash_after.clone(),
                    origin,
                };
                if row.own {
                    // A save adds nothing to Quick Open or the tree: it
                    // updates the baseline of a path the host shows and
                    // leaves an unshown path unshown.
                    if let Shown::Present(shown) = &mut state.shown {
                        *shown = row.hash_after;
                    }
                } else {
                    state.foreign = true;
                }
                state.last_seq = row.seq;
            }
            KIND_DELETED => {
                let state = self.state(row.entry, &row.path, Before::Present(&row.hash_before));
                state.index = Indexed::Absent;
                if row.own {
                    state.shown = Shown::Absent;
                } else {
                    state.foreign = true;
                }
                state.last_seq = row.seq;
            }
            KIND_RENAMED => {
                let Some(from) = row.previous_path.as_deref() else {
                    return Err(invalid("change feed rename row has no previous path"));
                };
                let source = self.state(row.entry, from, Before::Present(&row.hash_before));
                let source_shown = source.shown.clone();
                let source_origin = source.index.origin();
                source.index = Indexed::Absent;
                if row.own {
                    source.shown = Shown::Absent;
                } else {
                    source.foreign = true;
                }
                source.last_seq = row.seq;

                // An own rename carries the content's origin, if it has one,
                // to the destination: the host's retarget moved its tabs. A
                // rename the host did not follow carries the source's origin,
                // or else the source path and its shown hash when the host
                // shows the source. Content that returns home has no origin.
                let carried = if row.own {
                    source_origin
                } else {
                    source_origin.or_else(|| match source_shown.clone() {
                        Shown::Present(hash) => Some(ContentOrigin {
                            path: from.to_string(),
                            hash,
                            rename_seq: 0,
                        }),
                        Shown::Absent => None,
                    })
                }
                .map(|origin| ContentOrigin {
                    rename_seq: row.seq,
                    ..origin
                })
                .filter(|origin| origin.path != row.path);

                let destination = self.state(row.entry, &row.path, Before::Absent);
                destination.index = Indexed::Present {
                    hash: row.hash_after,
                    origin: carried,
                };
                if row.own {
                    destination.shown = source_shown;
                } else {
                    destination.foreign = true;
                }
                destination.last_seq = row.seq;
            }
            _ => return Err(invalid("change feed row has an unknown kind")),
        }
        Ok(())
    }

    /// The counts (files only): `Created` (shown absent, index present),
    /// `Modified` (both present, different hashes), `Removed` (shown
    /// present, index absent). `changed` = Created + Modified.
    pub(crate) fn counts(&self) -> PathCounts {
        let mut counts = PathCounts::default();
        for ((entry, path), state) in &self.states {
            if *entry != ChangeEntry::File {
                continue;
            }
            let count = state.count();
            match count {
                ChangeCount::Created | ChangeCount::Modified => counts.changed += 1,
                ChangeCount::Removed => counts.removed += 1,
                ChangeCount::Uncounted => continue,
            }
            counts.by_path.insert(path.clone(), count);
        }
        counts
    }

    /// The records: a path gets one when a row that is not an own write
    /// touched it or when its two states differ at the end. Its effect
    /// converges the host to the index at the target; a final path whose
    /// content carries an origin also yields a `Renamed` record from the
    /// origin, with the origin's shown hash. `counts` are the cursor's
    /// (`None` when the run is uncounted).
    pub(crate) fn records(&self, counts: Option<&PathCounts>) -> Vec<DerivedRecord> {
        let mut records = Vec::new();
        for ((entry, path), state) in &self.states {
            if !state.foreign && state.agrees() {
                continue;
            }
            let counted = counts.map_or(ChangeCount::Uncounted, |counts| counts.of(*entry, path));
            match &state.index {
                Indexed::Absent => records.push(DerivedRecord {
                    path: path.clone(),
                    entry: *entry,
                    effect: ChangeEffect::Removed,
                    renamed_from: None,
                    from_hash: None,
                    counted,
                    content_hash: None,
                    seq: state.last_seq,
                }),
                Indexed::Present { hash, origin } => {
                    if let Some(origin) = origin {
                        records.push(DerivedRecord {
                            path: path.clone(),
                            entry: *entry,
                            effect: ChangeEffect::Renamed,
                            renamed_from: Some(origin.path.clone()),
                            from_hash: origin.hash.clone(),
                            counted: ChangeCount::Uncounted,
                            content_hash: None,
                            seq: origin.rename_seq,
                        });
                    }
                    records.push(DerivedRecord {
                        path: path.clone(),
                        entry: *entry,
                        effect: ChangeEffect::Present,
                        renamed_from: None,
                        from_hash: None,
                        counted,
                        content_hash: if *entry == ChangeEntry::File {
                            hash.clone()
                        } else {
                            None
                        },
                        seq: state.last_seq,
                    });
                }
            }
        }
        records
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use ChangeCount::{Created, Modified, Uncounted};
    use ChangeEffect::{Present, Renamed};

    /// The `Removed` effect and the `Removed` count, apart.
    const GONE: ChangeEffect = ChangeEffect::Removed;
    const REMOVED: ChangeCount = ChangeCount::Removed;

    fn hash(value: &str) -> Option<String> {
        Some(value.to_string())
    }

    fn row(seq: u64, kind: i64, path: &str, before: Option<&str>, after: Option<&str>) -> FeedRow {
        FeedRow {
            seq,
            kind,
            entry: ChangeEntry::File,
            path: path.to_string(),
            previous_path: None,
            hash_before: before.map(str::to_string),
            hash_after: after.map(str::to_string),
            own: false,
        }
    }

    fn rename(seq: u64, from: &str, to: &str, content: &str) -> FeedRow {
        FeedRow {
            previous_path: Some(from.to_string()),
            ..row(seq, KIND_RENAMED, to, Some(content), Some(content))
        }
    }

    fn own(row: FeedRow) -> FeedRow {
        FeedRow { own: true, ..row }
    }

    fn derive(rows: Vec<FeedRow>) -> (PathCounts, Vec<DerivedRecord>) {
        let mut derivation = Derivation::default();
        for row in rows {
            derivation.apply(row).unwrap();
        }
        let counts = derivation.counts();
        let mut records = derivation.records(Some(&counts));
        // The pages' keyset: renames by their chain's last rename, then
        // removals and the rest by path.
        let key = |record: &DerivedRecord| {
            let sort_seq = if record.effect == Renamed {
                record.seq
            } else {
                0
            };
            (effect_code(record.effect), sort_seq, record.path.clone())
        };
        records.sort_by_key(key);
        (counts, records)
    }

    fn effects(records: &[DerivedRecord]) -> Vec<(ChangeEffect, &str, ChangeCount)> {
        records
            .iter()
            .map(|record| (record.effect, record.path.as_str(), record.counted))
            .collect()
    }

    /// The window token is opaque but strict: a token of another shape is
    /// malformed, and the round trip is exact.
    #[test]
    fn window_tokens_round_trip_and_refuse_other_shapes() {
        let token = WindowToken {
            nonce: 0x9f3c_2a7b_11d0_4e55,
            from: 3,
            to: 9,
            generation: 4,
        };
        assert_eq!(WindowToken::decode(&token.encode()).unwrap(), token);
        for bad in [
            "",
            "cfw0:9f3c2a7b11d04e55:3:9:4",
            "cfw1:zz:3:9:4",
            "cfw1:9f3c2a7b11d04e55:9:3:4",
            "cfw1:9f3c2a7b11d04e55:3:9:4:5",
            "cfw1:9f3c2a7b11d04e55:3:9",
        ] {
            assert!(WindowToken::decode(bad).is_err(), "{bad:?}");
        }
        let cursor = PageCursor {
            generation: 4,
            phase: PHASE_PRESENT,
            sort_seq: 0,
            entry: 1,
            path: "a:b/c.md".into(),
        };
        assert_eq!(PageCursor::decode(&cursor.encode()).unwrap(), cursor);
        assert!(PageCursor::decode("cfp1:4:9:0:0:x").is_err());
    }

    /// Design §4.3's table, row by row, on the derivation alone (the
    /// session facts in `session/tests/change_feed.rs` drive the same rows
    /// through real sessions).
    #[test]
    fn the_design_table_derives_row_by_row() {
        // B edits X; then A's user renames X to Y.
        let (counts, records) = derive(vec![
            row(1, KIND_MODIFIED, "X", Some("h1"), Some("h2")),
            own(rename(2, "X", "Y", "h2")),
        ]);
        assert_eq!(
            effects(&records),
            [(GONE, "X", Uncounted), (Present, "Y", Modified)]
        );
        assert_eq!(records[1].content_hash, hash("h2"));
        assert_eq!((counts.changed, counts.removed), (1, 0));

        // B edits X; then A's user deletes X.
        let (counts, records) = derive(vec![
            row(1, KIND_MODIFIED, "X", Some("h1"), Some("h2")),
            own(row(2, KIND_DELETED, "X", Some("h2"), None)),
        ]);
        assert_eq!(effects(&records), [(GONE, "X", Uncounted)]);
        assert_eq!((counts.changed, counts.removed), (0, 0));

        // A creates X; then B deletes it.
        let (counts, records) = derive(vec![
            own(row(1, KIND_CREATED, "X", None, Some("h1"))),
            row(2, KIND_DELETED, "X", Some("h1"), None),
        ]);
        assert_eq!(effects(&records), [(GONE, "X", REMOVED)]);
        assert_eq!((counts.changed, counts.removed), (0, 1));

        // B renames X to Y.
        let (counts, records) = derive(vec![rename(1, "X", "Y", "h1")]);
        assert_eq!(
            effects(&records),
            [
                (Renamed, "Y", Uncounted),
                (GONE, "X", REMOVED),
                (Present, "Y", Created)
            ]
        );
        assert_eq!(records[0].renamed_from.as_deref(), Some("X"));
        assert_eq!(records[0].from_hash, hash("h1"));
        assert_eq!((counts.changed, counts.removed), (1, 1));

        // B renames X to Y; then A's user renames Y to Z.
        let (counts, records) = derive(vec![
            rename(1, "X", "Y", "h1"),
            own(rename(2, "Y", "Z", "h1")),
        ]);
        assert_eq!(
            effects(&records),
            [
                (Renamed, "Z", Uncounted),
                (GONE, "X", REMOVED),
                (GONE, "Y", Uncounted),
                (Present, "Z", Created)
            ]
        );
        assert_eq!(records[0].renamed_from.as_deref(), Some("X"));
        assert_eq!(records[0].seq, 2, "the chain's last rename orders it");
        assert_eq!((counts.changed, counts.removed), (1, 1));

        // B renames readme.md to README.md; A's next save binds to README.md.
        let (counts, records) = derive(vec![
            rename(1, "readme.md", "README.md", "h1"),
            own(row(2, KIND_MODIFIED, "README.md", Some("h1"), Some("h2"))),
        ]);
        assert_eq!(
            effects(&records),
            [
                (Renamed, "README.md", Uncounted),
                (GONE, "readme.md", REMOVED),
                (Present, "README.md", Created)
            ]
        );
        assert_eq!(records[0].renamed_from.as_deref(), Some("readme.md"));
        assert_eq!((counts.changed, counts.removed), (1, 1));

        // B creates X; A's user opens and saves it before the run.
        let (counts, records) = derive(vec![
            row(1, KIND_CREATED, "X", None, Some("h1")),
            own(row(2, KIND_MODIFIED, "X", Some("h1"), Some("h2"))),
        ]);
        assert_eq!(effects(&records), [(Present, "X", Created)]);
        assert_eq!(records[0].content_hash, hash("h2"));
        assert_eq!((counts.changed, counts.removed), (1, 0));

        // B renames X to Y; then A's user renames another note W to X.
        let (counts, records) = derive(vec![
            rename(1, "X", "Y", "hx"),
            own(rename(2, "W", "X", "hw")),
        ]);
        assert_eq!(
            effects(&records),
            [
                (Renamed, "Y", Uncounted),
                (Present, "X", Uncounted),
                (Present, "Y", Created)
            ]
        );
        assert_eq!(records[0].renamed_from.as_deref(), Some("X"));
        assert_eq!(records[0].from_hash, hash("hx"), "only the old note's tab");
        assert_eq!((counts.changed, counts.removed), (1, 0));

        // B edits X, then edits it back.
        let (counts, records) = derive(vec![
            row(1, KIND_MODIFIED, "X", Some("h1"), Some("h2")),
            row(2, KIND_MODIFIED, "X", Some("h2"), Some("h1")),
        ]);
        assert_eq!(effects(&records), [(Present, "X", Uncounted)]);
        assert_eq!((counts.changed, counts.removed), (0, 0));

        // A's scan finds X edited outside Slate: a reconcile row is never an
        // own write.
        let (counts, records) = derive(vec![row(1, KIND_MODIFIED, "X", Some("h1"), Some("h2"))]);
        assert_eq!(effects(&records), [(Present, "X", Modified)]);
        assert_eq!((counts.changed, counts.removed), (1, 0));

        // A saves X.
        let (counts, records) = derive(vec![own(row(
            1,
            KIND_MODIFIED,
            "X",
            Some("h1"),
            Some("h2"),
        ))]);
        assert!(records.is_empty());
        assert_eq!((counts.changed, counts.removed), (0, 0));
    }

    /// Content renamed away and back has no origin left: no `Renamed`
    /// record from a path to itself.
    #[test]
    fn content_renamed_back_home_has_no_renamed_record() {
        let (counts, records) = derive(vec![rename(1, "X", "Y", "h1"), rename(2, "Y", "X", "h1")]);
        assert_eq!(
            effects(&records),
            [(GONE, "Y", Uncounted), (Present, "X", Uncounted)]
        );
        assert_eq!((counts.changed, counts.removed), (0, 0));
    }

    /// Directories get records but never counts, and carry no hashes.
    #[test]
    fn directories_are_recorded_and_never_counted() {
        let mut derivation = Derivation::default();
        derivation
            .apply(FeedRow {
                entry: ChangeEntry::Directory,
                ..row(1, KIND_CREATED, "Dir", None, None)
            })
            .unwrap();
        let counts = derivation.counts();
        assert_eq!((counts.changed, counts.removed), (0, 0));
        let records = derivation.records(Some(&counts));
        assert_eq!(records.len(), 1);
        assert_eq!(records[0].entry, ChangeEntry::Directory);
        assert_eq!(records[0].effect, ChangeEffect::Present);
        assert_eq!(records[0].counted, ChangeCount::Uncounted);
        assert_eq!(records[0].content_hash, None);
    }

    /// A row of an unknown kind, or a rename without its source, is refused
    /// rather than skipped: a skipped row would make the counts lie.
    #[test]
    fn a_malformed_row_is_refused() {
        let mut derivation = Derivation::default();
        assert!(derivation.apply(row(1, 9, "X", None, None)).is_err());
        assert!(
            derivation
                .apply(row(2, KIND_RENAMED, "X", Some("h"), Some("h")))
                .is_err()
        );
    }

    /// The origin scope sets Write while it lives and restores what it
    /// found, so nested scopes unwind correctly.
    #[test]
    fn write_scopes_nest_and_restore() {
        let conn = Connection::open_in_memory().unwrap();
        let seat = ChangeFeedSeat::new("local", 7);
        assert_eq!(seat.origin.load(Ordering::Relaxed), ORIGIN_RECONCILE);
        {
            let _outer = seat.write_scope(&conn);
            assert_eq!(seat.origin.load(Ordering::Relaxed), ORIGIN_WRITE);
            {
                let _inner = seat.write_scope(&conn);
                assert_eq!(seat.origin.load(Ordering::Relaxed), ORIGIN_WRITE);
            }
            assert_eq!(seat.origin.load(Ordering::Relaxed), ORIGIN_WRITE);
        }
        assert_eq!(seat.origin.load(Ordering::Relaxed), ORIGIN_RECONCILE);
        assert!(
            seat.writer()
                .starts_with(&format!("local:{}:", std::process::id())),
            "{}",
            seat.writer()
        );
        assert!(seat.writer().ends_with("0000000000000007"));
    }
}
